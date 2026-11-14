namespace Proc.Application.Portal;

/// <summary>
/// Một bảng giá NCC thấy trên Portal (lát P7a – design.md 19.5, P7-2, P7-3). Status theo ngày hôm nay (giờ Việt Nam): "Current" đang áp
/// dụng, "Upcoming" chưa đến ngày hiệu lực, "Expired" hết hạn, "Inactive" bên mua đã ngừng áp dụng.
/// </summary>
/// <param name="Id">Bảng giá.</param>
/// <param name="No">Số bảng giá.</param>
/// <param name="Title">Tên bảng giá.</param>
/// <param name="CompanyName">Công ty lập (quản lý) bảng giá.</param>
/// <param name="IsGroupWide">Áp dụng cho mọi công ty trong tập đoàn.</param>
/// <param name="ValidFrom">Hiệu lực từ ngày.</param>
/// <param name="ValidTo">Hiệu lực đến ngày; null = tới khi ngừng.</param>
/// <param name="DeliveryTerms">Điều kiện giao hàng.</param>
/// <param name="ApprovedDate">Ngày duyệt (đưa vào hiệu lực).</param>
/// <param name="LineCount">Số dòng giá.</param>
/// <param name="Status">Tình trạng.</param>
public sealed record PortalPriceListDto(
    Guid Id, string No, string Title, string CompanyName, bool IsGroupWide, DateOnly ValidFrom, DateOnly? ValidTo, string? DeliveryTerms,
    DateOnly? ApprovedDate, int LineCount, string Status);

/// <summary>Một dòng giá trên Portal (P7-4). Tiền VND, chưa VAT.</summary>
/// <param name="LineNo">STT.</param>
/// <param name="ItemCode">Mã hàng bên mua.</param>
/// <param name="SupplierItemCode">Mã hàng của NCC (khai ở danh mục NCC – mặt hàng).</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomCode">ĐVT báo giá.</param>
/// <param name="MinQty">Áp dụng khi SL đặt từ (0 = mọi số lượng).</param>
/// <param name="UnitPrice">Đơn giá trước chiết khấu.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
/// <param name="NetPrice">Đơn giá sau chiết khấu theo ĐVT dòng.</param>
/// <param name="VatCode">Thuế suất (dòng trống thì theo mặt hàng).</param>
public sealed record PortalPriceListLineDto(
    int LineNo, string ItemCode, string? SupplierItemCode, string ItemName, string UomCode, decimal MinQty, decimal UnitPrice, decimal DiscountPct,
    decimal NetPrice, string? VatCode);

/// <summary>Chi tiết một bảng giá trên Portal.</summary>
/// <param name="Header">Đầu bảng giá.</param>
/// <param name="Lines">Dòng giá theo STT.</param>
public sealed record PortalPriceListDetailDto(PortalPriceListDto Header, IReadOnlyList<PortalPriceListLineDto> Lines);

/// <summary>
/// Bảng giá trên Portal (lát P7a – design.md 19.5). Chỉ đọc; NCC lấy từ phiên (P1b-4); chỉ bảng giá của chính NCC đã duyệt (P7-2).
/// </summary>
public interface IPortalPriceListService
{
    /// <summary>Các bảng giá NCC thấy được: đang áp dụng, chưa đến ngày hiệu lực trước; trong nhóm mới hiệu lực trước.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách bảng giá.</returns>
    Task<IReadOnlyList<PortalPriceListDto>> ListAsync(PortalSession session, CancellationToken cancellationToken);

    /// <summary>Chi tiết một bảng giá.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">Bảng giá.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Bảng giá không thuộc NCC, chưa duyệt hoặc đã hủy.</exception>
    Task<PortalPriceListDetailDto> GetAsync(PortalSession session, Guid id, CancellationToken cancellationToken);
}
