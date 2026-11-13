namespace Proc.Domain.Documents;

/// <summary>Một loại chứng từ.</summary>
/// <param name="Code">Mã loại, cũng là tiền tố số mặc định, ví dụ PO.</param>
/// <param name="Name">Tên tiếng Việt.</param>
public sealed record DocumentTypeInfo(string Code, string Name);

/// <summary>
/// Danh mục loại chứng từ được đánh số (design.md mục 6.1, 6.2, 8.4). Khai báo một nơi trong code như danh mục quyền;
/// thêm loại chứng từ mới thì thêm ở đây.
/// </summary>
public static class DocumentTypes
{
    /// <summary>Đề nghị mua hàng.</summary>
    public const string PurchaseRequest = "PR";

    /// <summary>Yêu cầu báo giá.</summary>
    public const string RequestForQuotation = "RFQ";

    /// <summary>Đơn mua hàng.</summary>
    public const string PurchaseOrder = "PO";

    /// <summary>Hợp đồng nguyên tắc (lát B4a).</summary>
    public const string Contract = "CTR";

    /// <summary>Phiếu nhập mua từ NCC.</summary>
    public const string GoodsReceipt = "GRN";

    /// <summary>Phiếu xuất trả NCC.</summary>
    public const string ReturnToVendor = "RTV";

    /// <summary>Phiếu chuyển kho nội bộ.</summary>
    public const string Transfer = "TRF";

    /// <summary>Phiếu xuất bán nội bộ.</summary>
    public const string IntercompanySale = "ICS";

    /// <summary>Phiếu nhập mua nội bộ.</summary>
    public const string IntercompanyReceipt = "ICR";

    /// <summary>Phiếu điều chỉnh / kiểm kê.</summary>
    public const string Adjustment = "ADJ";

    /// <summary>Bảng giá nhà cung cấp (design.md mục 8.1).</summary>
    public const string PriceList = "PRL";

    /// <summary>Danh sách tìm nguồn (design.md mục 8.3).</summary>
    public const string Sourcing = "SRC";

    /// <summary>Phê duyệt NCC độc quyền (lát B5b, design.md mục 18.9).</summary>
    public const string SoleSource = "NDQ";

    /// <summary>Phiếu duyệt NCC và giá tháng (lát G2, design.md mục 18.25).</summary>
    public const string MonthlySupplierApproval = "GTH";

    /// <summary>Thỏa thuận chiết khấu bậc sản lượng gộp (lát G6a, design.md mục 18.25).</summary>
    public const string VolumeRebate = "CKB";

    /// <summary>Phiếu khảo sát giá thị trường – ngân hàng giá (lát G4a, design.md mục 18.25).</summary>
    public const string MarketPriceSurvey = "KSG";

    /// <summary>Phiếu kế hoạch nhu cầu (lát G5a, design.md mục 18.25).</summary>
    public const string DemandPlan = "KHN";

    /// <summary>Phiếu đặt hàng gộp cho các công ty (lát G7, design.md mục 18.25).</summary>
    public const string PooledOrder = "DHG";

    /// <summary>Vi phạm do Kiểm soát nội bộ ghi nhận (lát F3, design.md mục 18.19).</summary>
    public const string ControlFinding = "VP";

    /// <summary>Phiếu đánh giá NCC định kỳ theo quý (lát F4a, design.md mục 18.20).</summary>
    public const string SupplierReview = "DGN";

    /// <summary>Hồ sơ đánh giá NCC mới (lát G8, design.md mục 18.25).</summary>
    public const string SupplierOnboarding = "HSN";

    /// <summary>Bảng tổng hợp đề xuất mua 2A/2B (lát Y5b, design.md mục 19.3).</summary>
    public const string PurchaseSummary = "BTH";

    /// <summary>Phiếu xác nhận nhận hàng theo PO – thay phiếu nhập kho từ bản 0.22.0 (lát K2, design.md mục 19.4).</summary>
    public const string GoodsReceiptConfirmation = "XNH";

    /// <summary>Phiếu báo giao hàng NCC gửi trên Portal (lát P5, design.md mục 19.5) – nơi nhận lập XNH từ phiếu.</summary>
    public const string DeliveryNotice = "BGH";

    /// <summary>Đề nghị thanh toán 5A/5B (lát Y8, design.md mục 19.3 bước 7).</summary>
    public const string PaymentRequest = "DNT";

    /// <summary>Mọi loại chứng từ, theo thứ tự hiển thị.</summary>
    public static readonly IReadOnlyList<DocumentTypeInfo> All =
    [
        new(PurchaseRequest, "Đề nghị mua hàng"),
        new(PurchaseSummary, "Bảng tổng hợp đề xuất mua"),
        new(RequestForQuotation, "Yêu cầu báo giá"),
        new(PurchaseOrder, "Đơn mua hàng"),
        new(Contract, "Hợp đồng nguyên tắc"),
        new(GoodsReceiptConfirmation, "Xác nhận nhận hàng"),
        new(DeliveryNotice, "Báo giao hàng của NCC"),
        new(PaymentRequest, "Đề nghị thanh toán"),
        new(GoodsReceipt, "Phiếu nhập mua từ NCC"),
        new(ReturnToVendor, "Phiếu xuất trả NCC"),
        new(Transfer, "Phiếu chuyển kho nội bộ"),
        new(IntercompanySale, "Phiếu xuất bán nội bộ"),
        new(IntercompanyReceipt, "Phiếu nhập mua nội bộ"),
        new(Adjustment, "Phiếu điều chỉnh, kiểm kê"),
        new(PriceList, "Bảng giá nhà cung cấp"),
        new(Sourcing, "Danh sách tìm nguồn"),
        new(SoleSource, "Phê duyệt NCC độc quyền"),
        new(MonthlySupplierApproval, "Duyệt NCC và giá tháng"),
        new(VolumeRebate, "Thỏa thuận chiết khấu sản lượng gộp"),
        new(MarketPriceSurvey, "Khảo sát giá thị trường"),
        new(DemandPlan, "Kế hoạch nhu cầu"),
        new(PooledOrder, "Đặt hàng gộp"),
        new(ControlFinding, "Vi phạm kiểm soát nội bộ"),
        new(SupplierReview, "Đánh giá nhà cung cấp định kỳ"),
        new(SupplierOnboarding, "Hồ sơ đánh giá nhà cung cấp mới"),
    ];

    /// <summary>
    /// Kiểm tra mã loại chứng từ có trong danh mục.
    /// </summary>
    /// <param name="code">Mã đã chuẩn hóa chữ hoa.</param>
    /// <returns>true nếu có.</returns>
    public static bool IsKnown(string code) => All.Any(t => t.Code == code);
}
