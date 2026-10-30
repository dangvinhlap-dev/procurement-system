namespace Proc.Application.Inventory;

/// <summary>Một kỳ kho.</summary>
/// <param name="Id">Id.</param>
/// <param name="Year">Năm.</param>
/// <param name="Month">Tháng; 0 = kỳ năm.</param>
/// <param name="Label">"10/2026" hoặc "Năm 2026".</param>
/// <param name="StartDate">Ngày đầu kỳ.</param>
/// <param name="EndDate">Ngày cuối kỳ.</param>
/// <param name="Status">NotOpened, Open, Costing, Closed, Locked.</param>
/// <param name="ClosedAt">Thời điểm đóng kỳ.</param>
/// <param name="CanOpen">Kỳ này mở được ngay bây giờ (đúng thứ tự, người xem có quyền).</param>
/// <param name="CanClose">Kỳ đang mở, kỳ trước đã đóng (hoặc chưa từng dùng), người xem có quyền đóng kỳ.</param>
/// <param name="CanReopen">Kỳ đã đóng gần nhất, người xem có quyền mở lại kỳ.</param>
/// <param name="ReopenReason">Lý do lần mở lại gần nhất.</param>
public sealed record InventoryPeriodDto(
    Guid Id, int Year, int Month, string Label, DateOnly StartDate, DateOnly EndDate, string Status, DateTimeOffset? ClosedAt, bool CanOpen,
    bool CanClose = false, bool CanReopen = false, string? ReopenReason = null);

/// <summary>Mở lại kỳ đã đóng.</summary>
/// <param name="Reason">Lý do (bắt buộc – design.md mục 7.4).</param>
public sealed record ReopenPeriodRequest(string Reason);

/// <summary>Lịch kỳ của công ty đang làm việc.</summary>
/// <param name="Years">Các năm đã tạo lịch, tăng dần.</param>
/// <param name="Year">Năm đang xem (null khi chưa có lịch).</param>
/// <param name="Periods">Kỳ năm rồi 12 kỳ tháng của năm đang xem.</param>
/// <param name="NextYear">Năm sẽ tạo nếu bấm "Tạo lịch năm".</param>
public sealed record InventoryCalendarDto(IReadOnlyList<int> Years, int? Year, IReadOnlyList<InventoryPeriodDto> Periods, int NextYear);

/// <summary>Tạo lịch kỳ một năm.</summary>
/// <param name="Year">Năm.</param>
public sealed record CreateInventoryYearRequest(int Year);

/// <summary>
/// Kỳ kho của công ty đang làm việc (design.md mục 7, 7.6). Mỗi công ty một lịch kỳ riêng.
/// </summary>
public interface IInventoryPeriodService
{
    /// <summary>Lịch kỳ một năm.</summary>
    /// <param name="year">Năm; null = năm có kỳ đang mở gần nhất (hoặc năm mới nhất).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lịch kỳ.</returns>
    Task<InventoryCalendarDto> GetCalendarAsync(int? year, CancellationToken cancellationToken);

    /// <summary>Tạo lịch kỳ một năm (13 kỳ). Năm phải liền sau năm mới nhất đã có, hoặc là năm đầu tiên.</summary>
    /// <param name="request">Năm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lịch kỳ của năm vừa tạo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi năm đã có hoặc không liền sau năm mới nhất.</exception>
    Task<InventoryCalendarDto> CreateYearAsync(CreateInventoryYearRequest request, CancellationToken cancellationToken);

    /// <summary>Mở một kỳ tháng (tuần tự: ngay sau kỳ tháng đã mở gần nhất; kỳ đầu tiên không trước tháng bắt đầu dùng hệ thống).</summary>
    /// <param name="id">Id kỳ tháng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kỳ sau khi mở.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi không đúng thứ tự hoặc kỳ không ở trạng thái Chưa mở.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có kỳ.</exception>
    Task<InventoryPeriodDto> OpenAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Đóng kỳ tháng (design.md mục 7.2): kiểm tra trước đóng, khóa ghi sổ (Đang tính giá), tính giá vốn bình quân theo công ty ×
    /// mặt hàng, gán giá cho dòng xuất chưa chốt, chốt số dư theo kho × hàng × lô, rồi Đã đóng.
    /// </summary>
    /// <param name="id">Id kỳ tháng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kỳ sau khi đóng.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi kỳ trước chưa đóng, còn phiếu nháp, tồn âm, hoặc kỳ không Đang mở.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có kỳ.</exception>
    Task<InventoryPeriodDto> CloseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Mở lại kỳ đã đóng gần nhất (design.md mục 7.4): xóa số dư, giá vốn kỳ; giá vốn xuất về tạm tính; ghi lý do.
    /// </summary>
    /// <param name="id">Id kỳ tháng.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kỳ sau khi mở lại.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi không phải kỳ đã đóng gần nhất, đã khóa, hoặc thiếu lý do.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có kỳ.</exception>
    Task<InventoryPeriodDto> ReopenAsync(Guid id, ReopenPeriodRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Kỳ tháng đang mở chứa ngày chứng từ – gọi trước khi ghi sổ (CLAUDE.md mục 4: chỉ ghi sổ khi ngày chứng từ thuộc kỳ Open).
    /// </summary>
    /// <param name="documentDate">Ngày chứng từ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Id kỳ tháng.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi chưa có kỳ, kỳ chưa mở, đang tính giá, đã đóng hoặc đã khóa.</exception>
    Task<Guid> RequireOpenPeriodAsync(DateOnly documentDate, CancellationToken cancellationToken);
}
