using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Documents;

/// <summary>
/// Đánh số chứng từ (design.md mục 5.4, 12.2). Cấu hình đọc qua global query filter (chỉ công ty đang làm việc);
/// bộ đếm tăng bằng một câu SQL nguyên tử.
/// </summary>
public sealed class DocumentNumberService : IDocumentNumberService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ đánh số.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi (công ty đang làm việc).</param>
    public DocumentNumberService(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<string> NextAsync(string docType, DateOnly documentDate, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var type = CodeRules.Normalize(docType);
        if (!DocumentTypes.IsKnown(type)) throw new BusinessRuleException($"Không có loại chứng từ {type}.");

        var format = await FormatOfAsync(type, cancellationToken);
        var period = format.PeriodKey(documentDate);

        // Một câu lệnh vừa tạo bộ đếm (nếu chưa có) vừa tăng và trả số: PostgreSQL khóa dòng bộ đếm đến hết giao dịch,
        // nên hai người lưu cùng lúc phải lần lượt, không bao giờ nhận cùng số. Chạy trong giao dịch của chứng từ
        // (nếu có): chứng từ lưu lỗi thì lần tăng cũng bị hủy, số được cấp lại – không nhảy số.
        // Không ghép thêm toán tử LINQ vào SqlQuery (PostgreSQL không cho INSERT trong truy vấn con).
        var numbers = await _db.Database
            .SqlQuery<long>($"""
                INSERT INTO "DocumentSequence" ("CompanyId", "DocType", "Period", "CurrentNumber")
                VALUES ({companyId}, {type}, {period}, 1)
                ON CONFLICT ("CompanyId", "DocType", "Period")
                DO UPDATE SET "CurrentNumber" = "DocumentSequence"."CurrentNumber" + 1
                RETURNING "CurrentNumber" AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return format.Render(await CompanyCodeAsync(companyId, cancellationToken), documentDate, numbers.Single());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentNumberingDto>> ListAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var companyCode = await CompanyCodeAsync(companyId, cancellationToken);
        var configured = await _db.DocumentNumberings.AsNoTracking()
            .ToDictionaryAsync(x => x.DocType, x => x.Format, cancellationToken);
        var sequences = await _db.DocumentSequences.AsNoTracking()
            .ToDictionaryAsync(x => (x.DocType, x.Period), x => x.CurrentNumber, cancellationToken);

        return DocumentTypes.All
            .Select(t => ToDto(t, configured.GetValueOrDefault(t.Code), companyCode, today, sequences))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DocumentNumberingDto> UpdateAsync(
        string docType,
        UpdateDocumentNumberingRequest request,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var type = CodeRules.Normalize(docType);
        var info = DocumentTypes.All.FirstOrDefault(t => t.Code == type)
            ?? throw new NotFoundException($"Không có loại chứng từ {type}.");

        var existing = await _db.DocumentNumberings.FirstOrDefaultAsync(x => x.DocType == type, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Format))
        {
            // Về mẫu mặc định: bỏ dòng cấu hình riêng (xóa được ghi vào nhật ký thao tác).
            if (existing is not null) _db.DocumentNumberings.Remove(existing);
        }
        else if (existing is null)
        {
            _db.DocumentNumberings.Add(DocumentNumbering.Create(companyId, type, request.Format));
        }
        else
        {
            existing.ChangeFormat(request.Format);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await ListAsync(today, cancellationToken)).Single(x => x.DocType == info.Code);
    }

    private async Task<DocumentNumberFormat> FormatOfAsync(string type, CancellationToken cancellationToken)
    {
        var pattern = await _db.DocumentNumberings.AsNoTracking()
            .Where(x => x.DocType == type)
            .Select(x => x.Format)
            .FirstOrDefaultAsync(cancellationToken);
        return pattern is null ? DocumentNumberFormat.Default(type) : DocumentNumberFormat.Parse(pattern);
    }

    private async Task<string> CompanyCodeAsync(Guid companyId, CancellationToken cancellationToken) =>
        await _db.Companies.Where(c => c.Id == companyId).Select(c => c.Code).SingleAsync(cancellationToken);

    private static DocumentNumberingDto ToDto(
        DocumentTypeInfo type,
        string? configuredFormat,
        string companyCode,
        DateOnly today,
        Dictionary<(string DocType, string Period), long> sequences)
    {
        var format = configuredFormat is null ? DocumentNumberFormat.Default(type.Code) : DocumentNumberFormat.Parse(configuredFormat);
        var current = sequences.GetValueOrDefault((type.Code, format.PeriodKey(today)));
        return new DocumentNumberingDto(
            type.Code,
            type.Name,
            format.Pattern,
            configuredFormat is null,
            current > 0 ? format.Render(companyCode, today, current) : null,
            format.Render(companyCode, today, current + 1));
    }
}
