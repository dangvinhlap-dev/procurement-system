namespace Proc.Application.Organization;

/// <summary>Mã các tham số hiển thị trên màn hình Tham số hệ thống (SEC).</summary>
public static class SystemParameterCodes
{
    /// <summary>Số NCC tối thiểu khi gửi RFQ – sửa được.</summary>
    public const string RfqMinSuppliers = "RFQ_MIN_SUPPLIERS";

    /// <summary>% markup nội bộ mặc định – sửa được; dùng gợi ý giá bán nội bộ khi chưa có bảng giá.</summary>
    public const string InternalMarkupPercent = "INTERNAL_MARKUP_PERCENT";

    /// <summary>Bắt buộc kiểm tra chất lượng khi ghi sổ nhận hàng – bật/tắt (design.md mục 18.14).</summary>
    public const string ReceiptQcRequired = "RECEIPT_QC_REQUIRED";

    /// <summary>Người lập PO không ghi sổ phiếu nhập của PO đó – bật/tắt (design.md mục 18.14).</summary>
    public const string ReceiptSeparatePoCreator = "RECEIPT_SEPARATE_PO_CREATOR";

    /// <summary>SLA hàng đã có hợp đồng/bảng giá (ngày làm việc) – sửa được (design.md mục 18.13).</summary>
    public const string SlaPricedDays = "SLA_PRICED_DAYS";

    /// <summary>SLA hàng chưa có NCC được duyệt (ngày làm việc) – sửa được.</summary>
    public const string SlaSourcingDays = "SLA_SOURCING_DAYS";

    /// <summary>SLA hàng khẩn (ngày làm việc) – sửa được.</summary>
    public const string SlaUrgentDays = "SLA_URGENT_DAYS";

    /// <summary>Tiền tố mã tham số mục tiêu KPI: KPI_TARGET_ + tên <see cref="Proc.Domain.Organization.KpiCode"/> in hoa, ví dụ
    /// KPI_TARGET_CONTRACTCOMPLIANCE (design.md mục 18.18).</summary>
    public const string KpiTargetPrefix = "KPI_TARGET_";

    /// <summary>Số ngày không xuất để tính KPI tồn chậm luân chuyển – sửa được.</summary>
    public const string KpiSlowMovingDays = "KPI_SLOW_MOVING_DAYS";

    /// <summary>Mã tham số mục tiêu của một KPI.</summary>
    /// <param name="code">Mã chỉ số.</param>
    /// <returns>Ví dụ KPI_TARGET_SAVING.</returns>
    public static string KpiTarget(Proc.Domain.Organization.KpiCode code) => KpiTargetPrefix + code.ToString().ToUpperInvariant();

    /// <summary>Phương pháp giá vốn – chỉ xem (cố định bình quân gia quyền tháng, design.md mục 7).</summary>
    public const string CostingMethod = "COSTING_METHOD";

    /// <summary>Kho cho phép tồn âm – chỉ xem, khai báo trên từng kho.</summary>
    public const string NegativeStock = "NEGATIVE_STOCK";
}

/// <summary>Một tham số tại công ty đang làm việc.</summary>
/// <param name="Code">Mã tham số (<see cref="SystemParameterCodes"/>).</param>
/// <param name="Group">Nhóm hiển thị, ví dụ "Đấu thầu".</param>
/// <param name="Name">Tên tham số.</param>
/// <param name="Value">Giá trị dạng chữ để hiển thị.</param>
/// <param name="DefaultValue">Giá trị mặc định; null nếu tham số không có mặc định (chỉ xem).</param>
/// <param name="IsDefault">true khi đang dùng giá trị mặc định.</param>
/// <param name="Editable">true khi sửa được trên màn hình này.</param>
/// <param name="Note">Giải thích: ý nghĩa, giới hạn, nơi khai báo nếu chỉ xem.</param>
/// <param name="AuditId">Khóa dòng tham số công ty lưu giá trị (null khi chưa lưu lần nào) – để F1 xem lịch sử (design.md mục 9.3a).</param>
/// <param name="AuditField">Trường của dòng tham số công ty lưu giá trị này; null = không lưu ở bảng tham số công ty.</param>
public sealed record SystemParameterDto(
    string Code, string Group, string Name, string Value, string? DefaultValue, bool IsDefault, bool Editable, string Note,
    Guid? AuditId = null, string? AuditField = null);

/// <summary>Đổi một tham số; Value trống = về mặc định.</summary>
/// <param name="Value">Giá trị mới dạng chữ.</param>
public sealed record UpdateSystemParameterRequest(string? Value);

/// <summary>
/// Tham số hệ thống theo công ty đang làm việc (design.md mục 5.4 "Tham số công ty").
/// </summary>
public interface ISystemParameterService
{
    /// <summary>
    /// Danh sách tham số tại công ty đang làm việc.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo thứ tự cố định trên màn hình.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi chưa chọn công ty làm việc.</exception>
    Task<IReadOnlyList<SystemParameterDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Đổi một tham số sửa được tại công ty đang làm việc.
    /// </summary>
    /// <param name="code">Mã tham số.</param>
    /// <param name="request">Giá trị mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tham số sau khi đổi.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có tham số mã này.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi tham số chỉ xem hoặc giá trị không hợp lệ.</exception>
    Task<SystemParameterDto> UpdateAsync(string code, UpdateSystemParameterRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Số NCC tối thiểu khi gửi RFQ của công ty đang làm việc (dùng khi gửi và hiển thị RFQ).
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Giá trị tham số, hoặc mặc định nếu công ty chưa khai báo.</returns>
    Task<int> RfqMinSuppliersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// % markup nội bộ mặc định của công ty đang làm việc (dùng gợi ý giá bán nội bộ).
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Giá trị tham số, hoặc mặc định nếu công ty chưa khai báo.</returns>
    Task<decimal> InternalMarkupPercentAsync(CancellationToken cancellationToken);
}
