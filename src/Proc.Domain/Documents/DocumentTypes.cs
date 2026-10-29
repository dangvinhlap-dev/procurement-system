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

    /// <summary>Hợp đồng khung.</summary>
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

    /// <summary>Mọi loại chứng từ, theo thứ tự hiển thị.</summary>
    public static readonly IReadOnlyList<DocumentTypeInfo> All =
    [
        new(PurchaseRequest, "Đề nghị mua hàng"),
        new(RequestForQuotation, "Yêu cầu báo giá"),
        new(PurchaseOrder, "Đơn mua hàng"),
        new(Contract, "Hợp đồng khung"),
        new(GoodsReceipt, "Phiếu nhập mua từ NCC"),
        new(ReturnToVendor, "Phiếu xuất trả NCC"),
        new(Transfer, "Phiếu chuyển kho nội bộ"),
        new(IntercompanySale, "Phiếu xuất bán nội bộ"),
        new(IntercompanyReceipt, "Phiếu nhập mua nội bộ"),
        new(Adjustment, "Phiếu điều chỉnh, kiểm kê"),
    ];

    /// <summary>
    /// Kiểm tra mã loại chứng từ có trong danh mục.
    /// </summary>
    /// <param name="code">Mã đã chuẩn hóa chữ hoa.</param>
    /// <returns>true nếu có.</returns>
    public static bool IsKnown(string code) => All.Any(t => t.Code == code);
}
