namespace Proc.Application.Organization;

/// <summary>Mã các tham số hiển thị trên màn hình Tham số hệ thống (SEC).</summary>
public static class SystemParameterCodes
{
    /// <summary>Số NCC tối thiểu khi gửi RFQ – sửa được.</summary>
    public const string RfqMinSuppliers = "RFQ_MIN_SUPPLIERS";

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
public sealed record SystemParameterDto(
    string Code, string Group, string Name, string Value, string? DefaultValue, bool IsDefault, bool Editable, string Note);

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
}
