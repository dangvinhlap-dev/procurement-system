using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>
/// Tham số riêng của một công ty (design.md mục 5.4 "Tham số công ty"). Mỗi công ty tối đa một dòng; chưa có dòng nghĩa là
/// mọi tham số đang ở giá trị mặc định. Mỗi công ty là pháp nhân độc lập nên tự quyết tham số của mình (CLAUDE.md mục 4).
/// </summary>
[Auditable("Tham số công ty")]
public sealed class CompanyParameter : AuditableEntity, ICompanyOwned
{
    /// <summary>Số NCC tối thiểu mặc định khi gửi RFQ – design.md mục 8.4 bước 2 ("≥ 3 NCC").</summary>
    public const int DefaultRfqMinSuppliers = 3;

    /// <summary>Giới hạn trên của số NCC tối thiểu – tránh nhập nhầm khiến không RFQ nào gửi được.</summary>
    public const int MaxRfqMinSuppliers = 20;

    private CompanyParameter()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số NCC phải mời tối thiểu mới gửi được RFQ (1–20).</summary>
    public int RfqMinSuppliers { get; private set; } = DefaultRfqMinSuppliers;

    /// <summary>
    /// Tạo bộ tham số với giá trị mặc định.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <returns>Tham số mới.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty.</exception>
    public static CompanyParameter Create(Guid companyId)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Tham số phải thuộc một công ty.");
        return new CompanyParameter { CompanyId = companyId };
    }

    /// <summary>
    /// Đổi số NCC tối thiểu khi gửi RFQ.
    /// </summary>
    /// <param name="value">Giá trị mới; null = về mặc định <see cref="DefaultRfqMinSuppliers"/>.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng 1–<see cref="MaxRfqMinSuppliers"/>.</exception>
    public void ChangeRfqMinSuppliers(int? value)
    {
        var v = value ?? DefaultRfqMinSuppliers;
        // Cho phép 1 (chỉ định thầu) vì là quyết định của từng công ty; trên 20 gần như chắc chắn là gõ nhầm.
        if (v < 1 || v > MaxRfqMinSuppliers)
            throw new BusinessRuleException($"Số NCC tối thiểu khi gửi RFQ phải từ 1 đến {MaxRfqMinSuppliers}.");
        RfqMinSuppliers = v;
    }
}
