using Proc.Domain.Operations;

namespace Proc.Application.Operations;

/// <summary>Lỗi máy chủ cần ghi (lỗi 500 – <c>ProblemExceptionHandler</c>).</summary>
/// <param name="Exception">Exception gốc.</param>
/// <param name="Path">Đường dẫn API.</param>
/// <param name="Method">Phương thức HTTP.</param>
/// <param name="UserAgent">Trình duyệt.</param>
/// <param name="CompanyId">Công ty đang làm việc (header X-Company-Id), nếu có.</param>
public sealed record ServerErrorReport(Exception Exception, string? Path, string? Method, string? UserAgent, Guid? CompanyId);

/// <summary>Lỗi giao diện trình duyệt gửi về (<c>POST /api/ops/v1/client-errors</c>).</summary>
/// <param name="Kind">Loại: render (ErrorBoundary), error (window.onerror), rejection (unhandledrejection), manual.</param>
/// <param name="Name">Tên lỗi JavaScript, ví dụ TypeError.</param>
/// <param name="Message">Nội dung.</param>
/// <param name="Stack">Ngăn xếp.</param>
/// <param name="ComponentStack">Cây component React (ErrorBoundary).</param>
/// <param name="Url">Trang đang mở.</param>
public sealed record ClientErrorRequest(string? Kind, string? Name, string? Message, string? Stack, string? ComponentStack, string? Url);

/// <summary>Kết quả ghi lỗi: mã theo dõi để hiện cho người dùng.</summary>
/// <param name="TrackingCode">Mã theo dõi, ví dụ ERR-7K3F9Q2A.</param>
public sealed record ErrorTrackingDto(string TrackingCode);

/// <summary>
/// Ghi nhật ký lỗi (lát QT2, design.md mục 21.4 ERR): gộp nhóm theo dấu vết, nhóm mới / mở lại thì báo người quản lý. Chạy trong scope CSDL
/// riêng và <b>không bao giờ ném lỗi</b> – ghi lỗi hỏng chỉ ghi log kỹ thuật (Serilog có mã theo dõi), người dùng vẫn nhận câu báo lỗi kèm mã.
/// </summary>
public interface IErrorRecorder
{
    /// <summary>Ghi lỗi máy chủ.</summary>
    /// <param name="report">Lỗi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Mã theo dõi (luôn có, kể cả khi ghi CSDL hỏng).</returns>
    Task<string> RecordServerAsync(ServerErrorReport report, CancellationToken cancellationToken);

    /// <summary>Ghi lỗi giao diện.</summary>
    /// <param name="request">Lỗi trình duyệt gửi.</param>
    /// <param name="userAgent">Trình duyệt.</param>
    /// <param name="companyId">Công ty đang làm việc (lấy từ phạm vi đã kiểm tra của request, không tin dữ liệu client).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Mã theo dõi.</returns>
    Task<string> RecordClientAsync(ClientErrorRequest request, string? userAgent, Guid? companyId, CancellationToken cancellationToken);
}

/// <summary>Một nhóm lỗi trên lưới ERR.</summary>
/// <param name="Id">Id.</param>
/// <param name="Source">Server / Client.</param>
/// <param name="Status">New / InProgress / Resolved.</param>
/// <param name="ErrorType">Loại lỗi.</param>
/// <param name="Message">Nội dung lần đầu.</param>
/// <param name="Location">Vị trí đầu ngăn xếp.</param>
/// <param name="Path">Đường dẫn chuẩn hóa.</param>
/// <param name="FirstSeenAt">Lần đầu.</param>
/// <param name="LastSeenAt">Lần cuối.</param>
/// <param name="OccurrenceCount">Số lần.</param>
/// <param name="ReopenCount">Số lần tự mở lại.</param>
/// <param name="HandledByName">Người đổi trạng thái gần nhất.</param>
/// <param name="HandledAt">Lúc đổi trạng thái.</param>
/// <param name="Note">Ghi chú xử lý.</param>
/// <param name="LastTrackingCode">Mã theo dõi lần gần nhất (còn giữ).</param>
/// <param name="LastVersion">Phiên bản lần gần nhất.</param>
public sealed record ErrorGroupItemDto(
    Guid Id, string Source, string Status, string ErrorType, string Message, string? Location, string? Path, DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt, int OccurrenceCount, int ReopenCount, string? HandledByName, DateTimeOffset? HandledAt, string? Note,
    string? LastTrackingCode, string? LastVersion);

/// <summary>Một lần xảy ra lỗi.</summary>
/// <param name="Id">Id.</param>
/// <param name="OccurredAt">Thời điểm.</param>
/// <param name="TrackingCode">Mã theo dõi.</param>
/// <param name="UserName">Người dùng.</param>
/// <param name="CompanyCode">Công ty.</param>
/// <param name="Path">Đường dẫn thật.</param>
/// <param name="Method">Phương thức.</param>
/// <param name="Message">Nội dung.</param>
/// <param name="Detail">Chi tiết kỹ thuật.</param>
/// <param name="Version">Phiên bản.</param>
/// <param name="Environment">Môi trường.</param>
/// <param name="UserAgent">Trình duyệt.</param>
public sealed record ErrorOccurrenceDto(
    Guid Id, DateTimeOffset OccurredAt, string TrackingCode, string? UserName, string? CompanyCode, string? Path, string? Method, string Message,
    string? Detail, string? Version, string? Environment, string? UserAgent);

/// <summary>Chi tiết nhóm lỗi: nhóm + các lần xảy ra gần nhất.</summary>
/// <param name="Group">Nhóm.</param>
/// <param name="Occurrences">Tối đa <see cref="IErrorLogService.MaxOccurrences"/> lần gần nhất.</param>
public sealed record ErrorGroupDetailDto(ErrorGroupItemDto Group, IReadOnlyList<ErrorOccurrenceDto> Occurrences);

/// <summary>Kết quả tìm nhóm lỗi.</summary>
/// <param name="Items">Nhóm lỗi, lần cuối mới nhất trước.</param>
/// <param name="Truncated">Còn nhóm chưa hiện (quá <see cref="IErrorLogService.MaxGroups"/>).</param>
public sealed record ErrorGroupPage(IReadOnlyList<ErrorGroupItemDto> Items, bool Truncated);

/// <summary>Đổi trạng thái nhóm lỗi.</summary>
/// <param name="Status">Tên trạng thái: New / InProgress / Resolved.</param>
/// <param name="Note">Ghi chú; bỏ trống = giữ ghi chú cũ.</param>
public sealed record ChangeErrorGroupStatusRequest(string Status, string? Note);

/// <summary>Màn hình ERR: tìm, xem, đổi trạng thái nhóm lỗi; dọn lỗi quá hạn.</summary>
public interface IErrorLogService
{
    /// <summary>Số nhóm tối đa mỗi lần tìm.</summary>
    const int MaxGroups = 2000;

    /// <summary>Số lần xảy ra tối đa trả về trong chi tiết.</summary>
    const int MaxOccurrences = 200;

    /// <summary>Tìm nhóm lỗi theo lần cuối trong khoảng ngày, trạng thái, hoặc mã theo dõi (có mã thì bỏ qua khoảng ngày).</summary>
    /// <param name="from">Từ ngày (giờ Việt Nam).</param>
    /// <param name="to">Đến ngày.</param>
    /// <param name="status">Trạng thái; null = mọi trạng thái.</param>
    /// <param name="trackingCode">Mã theo dõi người dùng báo.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nhóm lỗi.</returns>
    Task<ErrorGroupPage> SearchAsync(
        DateOnly from, DateOnly to, ErrorGroupStatus? status, string? trackingCode, CancellationToken cancellationToken);

    /// <summary>Chi tiết một nhóm.</summary>
    /// <param name="id">Id nhóm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    Task<ErrorGroupDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đổi trạng thái (ghi nhật ký vận hành).</summary>
    /// <param name="id">Id nhóm.</param>
    /// <param name="request">Trạng thái, ghi chú.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nhóm sau khi đổi.</returns>
    Task<ErrorGroupItemDto> ChangeStatusAsync(Guid id, ChangeErrorGroupStatusRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Xóa lần xảy ra cũ hơn số ngày giữ (tham số ERRLOG_KEEP_DAYS của tổng công ty) và nhóm không còn lần xảy ra nào; có xóa thì ghi nhật ký
    /// vận hành.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số lần xảy ra và số nhóm đã xóa.</returns>
    Task<(int Occurrences, int Groups)> PurgeAsync(CancellationToken cancellationToken);
}
