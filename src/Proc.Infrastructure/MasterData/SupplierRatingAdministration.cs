using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Cấu hình hạng xếp loại NCC (design.md mục 11.3). Bảng dùng chung toàn tập đoàn (không filter công ty) nên lệnh ghi bắt buộc làm việc ở
/// tổng công ty – như các danh mục dùng chung khác (mục 5.7). Mọi thay đổi vào nhật ký qua interceptor ([Auditable]).
/// </summary>
public sealed class SupplierRatingAdministration : ISupplierRatingAdministration
{
    private const string What = "hạng xếp loại nhà cung cấp";

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    public SupplierRatingAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierRatingRuleDto>> ListAsync(CancellationToken cancellationToken) =>
        (await _db.SupplierRatingRules.AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync(cancellationToken)).Select(ToDto).ToList();

    /// <inheritdoc />
    public async Task<SupplierRatingRuleDto> CreateAsync(SaveSupplierRatingRuleRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, What, cancellationToken);
        var rule = SupplierRatingRule.Create(request.Code, request.Name, request.SortOrder, request.MinOnTimePct, request.MaxReturnPct);
        var all = await _db.SupplierRatingRules.ToListAsync(cancellationToken);
        SupplierRating.ValidateSet([.. all, rule]);
        _db.SupplierRatingRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(rule);
    }

    /// <inheritdoc />
    public async Task<SupplierRatingRuleDto> UpdateAsync(Guid id, SaveSupplierRatingRuleRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, What, cancellationToken);
        var all = await _db.SupplierRatingRules.ToListAsync(cancellationToken);
        var rule = all.SingleOrDefault(r => r.Id == id) ?? throw new NotFoundException("Không tìm thấy hạng xếp loại.");
        rule.Update(request.Name, request.SortOrder, request.MinOnTimePct, request.MaxReturnPct);
        SupplierRating.ValidateSet(all);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(rule);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, What, cancellationToken);
        var rule = await _db.SupplierRatingRules.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hạng xếp loại.");
        // Bỏ một hạng không làm các hạng còn lại sai thứ tự ngưỡng nên không cần kiểm tra lại cả bộ.
        _db.SupplierRatingRules.Remove(rule);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static SupplierRatingRuleDto ToDto(SupplierRatingRule r) => new(r.Id, r.Code, r.Name, r.SortOrder, r.MinOnTimePct, r.MaxReturnPct);
}
