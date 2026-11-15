using Proc.Domain.Operations;

namespace Proc.Application.Operations;

/// <summary>Một lần sao lưu trên màn hình BAK (tab Lịch sử – lát QT3b).</summary>
/// <param name="Id">Id.</param>
/// <param name="Kind">Kiểu: Full, Diff, Dump, Attachments.</param>
/// <param name="KindLabel">Chữ hiển thị của kiểu.</param>
/// <param name="Trigger">Manual / Schedule.</param>
/// <param name="Status">Queued, Running, Succeeded, Failed.</param>
/// <param name="StatusLabel">Chữ hiển thị tình trạng (kể cả "Xong – có cảnh báo", "Đã dọn").</param>
/// <param name="Light">Đèn: Ok, Warning, Error, Neutral (đang chờ / đang chạy / đã dọn).</param>
/// <param name="ScheduledDate">Ngày lượt lịch.</param>
/// <param name="RequestedByName">Người bấm chạy tay.</param>
/// <param name="QueuedAt">Lúc vào hàng chờ (UTC).</param>
/// <param name="StartedAt">Lúc bắt đầu (UTC).</param>
/// <param name="FinishedAt">Lúc kết thúc (UTC).</param>
/// <param name="DurationSeconds">Thời gian chạy (giây).</param>
/// <param name="RelativePath">Đường dẫn tương đối so với thư mục sao lưu.</param>
/// <param name="SizeBytes">Dung lượng.</param>
/// <param name="FileCount">Số file.</param>
/// <param name="Sha256">SHA256.</param>
/// <param name="BaseFullRunId">Bản Full làm gốc (Diff).</param>
/// <param name="StartWalFile">File WAL đầu tiên cần.</param>
/// <param name="Verified">Kết quả kiểm tra.</param>
/// <param name="VerifyMessage">Ghi chú kiểm tra.</param>
/// <param name="SecondaryCopied">Đã chép nơi thứ hai (null = không khai).</param>
/// <param name="SecondaryMessage">Ghi chú chép nơi thứ hai.</param>
/// <param name="ErrorMessage">Lỗi.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="PurgedAt">Lúc dọn file theo hạn giữ.</param>
public sealed record BackupRunDto(
    Guid Id, string Kind, string KindLabel, string Trigger, string Status, string StatusLabel, string Light, DateOnly? ScheduledDate,
    string? RequestedByName, DateTimeOffset QueuedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, long? DurationSeconds,
    string? RelativePath, long? SizeBytes, int? FileCount, string? Sha256, Guid? BaseFullRunId, string? StartWalFile, bool? Verified,
    string? VerifyMessage, bool? SecondaryCopied, string? SecondaryMessage, string? ErrorMessage, string? Note, DateTimeOffset? PurgedAt);

/// <summary>Kết quả tìm lịch sử sao lưu.</summary>
/// <param name="Items">Các lần sao lưu, mới nhất trước.</param>
/// <param name="Truncated">Còn dòng chưa trả – thu hẹp khoảng ngày.</param>
public sealed record BackupRunSearchResult(IReadOnlyList<BackupRunDto> Items, bool Truncated);

/// <summary>Yêu cầu "Sao lưu ngay".</summary>
/// <param name="Kind">Kiểu sao lưu.</param>
public sealed record QueueBackupRequest(BackupKind Kind);

/// <summary>Chữ hiển thị và đèn của lần sao lưu (dùng chung API, thông báo, nhật ký vận hành).</summary>
public static class BackupLabels
{
    /// <summary>Chữ của kiểu sao lưu.</summary>
    /// <param name="kind">Kiểu.</param>
    /// <returns>Ví dụ "Full", "Diff", "pg_dump", "Đính kèm".</returns>
    public static string Kind(BackupKind kind) => kind switch
    {
        BackupKind.Full => "Full",
        BackupKind.Diff => "Diff",
        BackupKind.Dump => "pg_dump",
        BackupKind.Attachments => "Đính kèm",
        _ => kind.ToString(),
    };

    /// <summary>
    /// Tình trạng và đèn: Đang chờ / Đang chạy (trung tính) · Lỗi (đỏ) · Xong mà kiểm tra không đạt hoặc chép nơi thứ hai lỗi – "Xong – có cảnh
    /// báo" (vàng) · Xong (xanh) · đã dọn file theo hạn giữ – "Đã dọn" (trung tính).
    /// </summary>
    /// <param name="run">Lần sao lưu.</param>
    /// <returns>(chữ, đèn Ok / Warning / Error / Neutral).</returns>
    public static (string Label, string Light) Status(BackupRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return run.Status switch
        {
            BackupRunStatus.Queued => ("Đang chờ", "Neutral"),
            BackupRunStatus.Running => ("Đang chạy", "Neutral"),
            BackupRunStatus.Failed => ("Lỗi", "Error"),
            _ when run.PurgedAt is not null => ("Đã dọn", "Neutral"),
            _ when run.Verified == false => ("Xong – kiểm tra không đạt", "Error"),
            _ when run.SecondaryCopied == false => ("Xong – chưa chép nơi thứ hai", "Warning"),
            _ => ("Xong", "Ok"),
        };
    }
}

/// <summary>
/// Lịch sử sao lưu và đặt sao lưu chạy tay (màn hình BAK – lát QT3b). Chỉ ở tổng công ty; endpoint kiểm tra quyền.
/// </summary>
public interface IBackupService
{
    /// <summary>Tìm các lần sao lưu vào hàng chờ trong khoảng ngày (giờ Việt Nam).</summary>
    /// <param name="from">Từ ngày.</param>
    /// <param name="to">Đến ngày.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các lần sao lưu, mới nhất trước.</returns>
    Task<BackupRunSearchResult> SearchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Một lần sao lưu.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần sao lưu.</returns>
    /// <exception cref="Proc.Application.Common.NotFoundException">Khi không có.</exception>
    Task<BackupRunDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// "Sao lưu ngay": đặt một lần sao lưu vào hàng chờ, bộ chạy nền chạy lần lượt. Không đặt khi chưa khai thư mục sao lưu, hoặc đã có lần cùng
    /// kiểu đang chờ / đang chạy (bấm hai lần không sao lưu hai lần).
    /// </summary>
    /// <param name="request">Kiểu sao lưu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần sao lưu vừa đặt.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi chưa khai thư mục, đang có lần cùng kiểu.</exception>
    Task<BackupRunDto> QueueAsync(QueueBackupRequest request, CancellationToken cancellationToken);

    /// <summary>Lịch, thời hạn giữ và nơi lưu (tab "Lịch và thời hạn giữ", "Nơi lưu" của BAK).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hai bảng dòng.</returns>
    Task<BackupOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Bộ chạy sao lưu: lấy lần đang chờ cũ nhất, chạy, ghi kết quả – lần lượt đến hết hàng chờ. Giữ khóa <c>pg_try_advisory_lock</c> trong lúc chạy
/// nên nhiều tiến trình ứng dụng (IIS tái chế chồng, chạy nhiều máy) không sao lưu chồng nhau.
/// </summary>
public interface IBackupExecutor
{
    /// <summary>Chạy hết hàng chờ.</summary>
    /// <param name="cancellationToken">Hủy thao tác (ứng dụng dừng).</param>
    /// <returns>Số lần sao lưu đã chạy; 0 khi hàng chờ trống hoặc tiến trình khác đang giữ khóa.</returns>
    Task<int> ProcessQueueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lúc ứng dụng khởi động: lần sao lưu còn "Đang chạy" là của tiến trình cũ đã dừng giữa chừng – ghi lỗi để không treo mãi (chỉ khi lấy được
    /// khóa, tức không tiến trình nào khác đang chạy sao lưu).
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số lần đã ghi lỗi.</returns>
    Task<int> FailInterruptedAsync(CancellationToken cancellationToken);
}

/// <summary>Tín hiệu đánh thức bộ chạy nền khi có lần sao lưu mới vào hàng chờ (singleton).</summary>
public interface IBackupQueueSignal
{
    /// <summary>Báo có việc mới.</summary>
    void Notify();

    /// <summary>Chờ tín hiệu hoặc hết thời gian.</summary>
    /// <param name="timeout">Thời gian chờ tối đa.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task xong khi có tín hiệu / hết giờ.</returns>
    Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>Kết quả một lần dọn bản sao lưu quá hạn giữ (lát QT3b-2).</summary>
/// <param name="PurgedRuns">Số lần sao lưu đã dọn file.</param>
/// <param name="OldestNeededWal">File WAL cũ nhất còn giữ (WAL cũ hơn đã dọn); null = không dọn WAL.</param>
/// <param name="Messages">Ghi chú / lỗi từng bước (xóa file, pg_archivecleanup, chỗ trống ổ).</param>
public sealed record BackupCleanupResult(int PurgedRuns, string? OldestNeededWal, IReadOnlyList<string> Messages);

/// <summary>
/// Lịch sao lưu và dọn bản cũ (lát QT3b-2, design.md mục 21.5, B1): dịch vụ nền gọi mỗi phút. Chỉ chạy khi máy có khai thư mục sao lưu và bật
/// <c>Backup:ScheduleEnabled</c>.
/// </summary>
public interface IBackupScheduler
{
    /// <summary>Đặt các lần sao lưu của lượt lịch hôm nay khi đã tới giờ và chưa đặt (nhiều tiến trình cùng đặt thì chỉ một lần thành công).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số lần vừa đặt.</returns>
    Task<int> PlanAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Dọn file các bản quá hạn giữ (nơi chính và nơi thứ hai), dọn WAL cũ hơn bản Full cũ nhất còn giữ, kiểm tra chỗ trống ổ sao lưu (dưới 15 %
    /// thì thông báo, mỗi ngày một lần).
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả.</returns>
    Task<BackupCleanupResult> CleanupAsync(CancellationToken cancellationToken);
}

/// <summary>Tab "Lịch và thời hạn giữ", "Nơi lưu" của màn hình BAK (lát QT3b-3) – bảng nhóm / hạng mục / giá trị / đèn / ghi chú như SYS.</summary>
/// <param name="ScheduleRows">Lịch tự động, tham số, lượt kế tiếp, số bản đang giữ.</param>
/// <param name="StorageRows">Thư mục sao lưu, nơi thứ hai, thư mục WAL, chỗ trống ổ, công cụ PostgreSQL, tài khoản bảo trì.</param>
public sealed record BackupOverviewDto(IReadOnlyList<SystemOverviewRowDto> ScheduleRows, IReadOnlyList<SystemOverviewRowDto> StorageRows);
