using Proc.Domain.Common;

namespace Proc.Domain.Documents;

/// <summary>
/// Cấu hình đánh số một loại chứng từ tại một công ty (design.md mục 5.4). Công ty chưa có dòng cấu hình cho loại
/// chứng từ nào thì dùng mẫu mặc định <see cref="DocumentNumberFormat.Default"/>.
/// Số hiện tại không nằm ở đây mà ở <see cref="DocumentSequence"/>, vì mỗi kỳ (tháng/năm) có một bộ đếm riêng.
/// </summary>
[Auditable("Đánh số chứng từ", LabelProperty = nameof(DocType))]
public sealed class DocumentNumbering : AuditableEntity, ICompanyOwned
{
    private DocumentNumbering()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Mã loại chứng từ, ví dụ PO.</summary>
    public string DocType { get; private set; } = string.Empty;

    /// <summary>Mẫu số đã chuẩn hóa, ví dụ PO-{CTY}-{YYMM}-{0000}.</summary>
    public string Format { get; private set; } = string.Empty;

    /// <summary>
    /// Tạo cấu hình đánh số.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="docType">Mã loại chứng từ (có trong <see cref="DocumentTypes.All"/>).</param>
    /// <param name="format">Mẫu số.</param>
    /// <returns>Cấu hình mới.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, loại chứng từ lạ hoặc mẫu không hợp lệ.</exception>
    public static DocumentNumbering Create(Guid companyId, string docType, string format)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Cấu hình đánh số phải thuộc một công ty.");
        var type = CodeRules.Normalize(docType);
        if (!DocumentTypes.IsKnown(type)) throw new BusinessRuleException($"Không có loại chứng từ {type}.");
        var numbering = new DocumentNumbering { CompanyId = companyId, DocType = type };
        numbering.ChangeFormat(format);
        return numbering;
    }

    /// <summary>
    /// Đổi mẫu số. Số đã cấp không đổi theo; chứng từ mới lấy số theo mẫu mới.
    /// </summary>
    /// <param name="format">Mẫu mới.</param>
    /// <exception cref="BusinessRuleException">Khi mẫu không hợp lệ.</exception>
    public void ChangeFormat(string format) => Format = DocumentNumberFormat.Parse(format).Pattern;
}

/// <summary>
/// Bộ đếm số chứng từ theo (công ty, loại chứng từ, kỳ). Kỳ là "yyyyMM", "yyyy" hoặc rỗng tùy mẫu số
/// (xem <see cref="DocumentNumberFormat.PeriodKey"/>). Chỉ tăng bằng một câu SQL nguyên tử ở Infrastructure.
/// </summary>
public sealed class DocumentSequence : ICompanyOwned
{
    private DocumentSequence()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Mã loại chứng từ.</summary>
    public string DocType { get; private set; } = string.Empty;

    /// <summary>Kỳ đếm.</summary>
    public string Period { get; private set; } = string.Empty;

    /// <summary>Số đã cấp gần nhất trong kỳ.</summary>
    public long CurrentNumber { get; private set; }
}
