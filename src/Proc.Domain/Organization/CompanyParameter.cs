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

    /// <summary>% markup nội bộ mặc định khi công ty chưa khai báo: 0% = gợi ý bán đúng giá vốn.</summary>
    public const decimal DefaultInternalMarkupPercent = 0m;

    /// <summary>Giới hạn trên của % markup nội bộ (gấp 11 lần giá vốn) – quá mức này gần như chắc chắn là gõ nhầm.</summary>
    public const decimal MaxInternalMarkupPercent = 1000m;

    private CompanyParameter()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số NCC phải mời tối thiểu mới gửi được RFQ (1–20).</summary>
    public int RfqMinSuppliers { get; private set; } = DefaultRfqMinSuppliers;

    /// <summary>
    /// % markup nội bộ mặc định – numeric(9,4). Dùng gợi ý giá bán nội bộ khi mặt hàng chưa có trong bảng giá nội bộ
    /// (design.md mục 1 câu hỏi 2): giá gợi ý = giá vốn kỳ đã đóng gần nhất × (1 + % markup).
    /// </summary>
    public decimal InternalMarkupPercent { get; private set; } = DefaultInternalMarkupPercent;

    /// <summary>
    /// Bắt buộc mọi dòng phiếu nhập mua có kết quả kiểm tra chất lượng mới ghi sổ được (design.md mục 18.14 – V7). Mặc định tắt để công ty
    /// chưa tổ chức kiểm tra không bị chặn nhận hàng.
    /// </summary>
    public bool RequireReceiptQc { get; private set; }

    /// <summary>
    /// Người lập đơn mua hàng không được ghi sổ phiếu nhập của chính đơn đó (design.md mục 18.14 – G4: người đặt hàng không một mình xác
    /// nhận giao nhận). Mặc định tắt (công ty nhỏ một người kiêm).
    /// </summary>
    public bool SeparatePoCreatorFromReceipt { get; private set; }

    /// <summary>Bật/tắt bắt buộc kiểm tra chất lượng khi ghi sổ nhận hàng; null = về mặc định (tắt).</summary>
    public void ChangeRequireReceiptQc(bool? value) => RequireReceiptQc = value ?? false;

    /// <summary>Bật/tắt tách người lập PO và người ghi sổ nhận hàng; null = về mặc định (tắt).</summary>
    public void ChangeSeparatePoCreatorFromReceipt(bool? value) => SeparatePoCreatorFromReceipt = value ?? false;

    /// <summary>SLA mặc định (ngày làm việc): hàng đã có hợp đồng/bảng giá – phát hành PO trong 1 ngày sau khi PR đủ duyệt (Ban CUVT).</summary>
    public const int DefaultSlaPricedDays = 1;

    /// <summary>SLA mặc định: hàng chưa có NCC được duyệt – 3–5 ngày tìm nguồn, so sánh, trình duyệt; lấy mức trên 5 ngày.</summary>
    public const int DefaultSlaSourcingDays = 5;

    /// <summary>SLA mặc định: hàng khẩn (PR mức ưu tiên Khẩn) – tiếp nhận ngay, 1 ngày làm việc.</summary>
    public const int DefaultSlaUrgentDays = 1;

    /// <summary>SLA tối đa (ngày làm việc) – lớn hơn gần như chắc chắn là gõ nhầm.</summary>
    public const int MaxSlaDays = 60;

    /// <summary>SLA hàng đã có hợp đồng/bảng giá (ngày làm việc từ lúc PR đủ duyệt đến lúc PO được duyệt) – design.md mục 18.13.</summary>
    public int SlaPricedDays { get; private set; } = DefaultSlaPricedDays;

    /// <summary>SLA hàng chưa có NCC được duyệt (ngày làm việc).</summary>
    public int SlaSourcingDays { get; private set; } = DefaultSlaSourcingDays;

    /// <summary>SLA hàng khẩn (ngày làm việc; 0 = trong ngày PR được duyệt).</summary>
    public int SlaUrgentDays { get; private set; } = DefaultSlaUrgentDays;

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

    /// <summary>
    /// Đổi % markup nội bộ mặc định.
    /// </summary>
    /// <param name="value">Giá trị mới (làm tròn 4 số lẻ theo cột numeric(9,4)); null = về mặc định <see cref="DefaultInternalMarkupPercent"/>.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng 0–<see cref="MaxInternalMarkupPercent"/>.</exception>
    public void ChangeInternalMarkupPercent(decimal? value)
    {
        var v = Math.Round(value ?? DefaultInternalMarkupPercent, 4, MidpointRounding.AwayFromZero);
        // Không cho âm: bán nội bộ dưới giá vốn phải là quyết định riêng từng mặt hàng ở bảng giá, không phải mặc định.
        if (v < 0 || v > MaxInternalMarkupPercent)
            throw new BusinessRuleException($"% markup nội bộ phải từ 0 đến {MaxInternalMarkupPercent:0}.");
        InternalMarkupPercent = v;
    }

    /// <summary>Đổi SLA một loại yêu cầu (design.md mục 18.13).</summary>
    /// <param name="category">Loại yêu cầu.</param>
    /// <param name="days">Số ngày làm việc; null = về mặc định.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng 0–<see cref="MaxSlaDays"/>.</exception>
    public void ChangeSlaDays(Purchasing.SlaCategory category, int? days)
    {
        var v = days ?? category switch
        {
            Purchasing.SlaCategory.Priced => DefaultSlaPricedDays,
            Purchasing.SlaCategory.Sourcing => DefaultSlaSourcingDays,
            _ => DefaultSlaUrgentDays,
        };
        if (v < 0 || v > MaxSlaDays) throw new BusinessRuleException($"SLA phải từ 0 đến {MaxSlaDays} ngày làm việc.");
        switch (category)
        {
            case Purchasing.SlaCategory.Priced: SlaPricedDays = v; break;
            case Purchasing.SlaCategory.Sourcing: SlaSourcingDays = v; break;
            default: SlaUrgentDays = v; break;
        }
    }
}
