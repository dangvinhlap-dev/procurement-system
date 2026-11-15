using Proc.Domain.Common;

namespace Proc.Domain.Operations;

/// <summary>Kiểu sao lưu (design.md mục 21.5, lát QT3b).</summary>
public enum BackupKind
{
    /// <summary>Full – <c>pg_basebackup</c> chụp cả máy chủ CSDL (thư mục plain, có <c>backup_manifest</c>).</summary>
    Full = 0,

    /// <summary>Diff – <c>pg_basebackup --incremental</c> lấy bản Full gần nhất làm gốc (không nối tiếp Diff trước).</summary>
    Diff = 1,

    /// <summary>Bản logic <c>pg_dump -Fc</c> của database ứng dụng.</summary>
    Dump = 2,

    /// <summary>Chép bổ sung thư mục file đính kèm (file mới / đổi sau lần chép trước).</summary>
    Attachments = 3,
}

/// <summary>Ai đặt lần sao lưu.</summary>
public enum BackupTrigger
{
    /// <summary>Người dùng bấm "Sao lưu ngay" trên màn hình BAK.</summary>
    Manual = 0,

    /// <summary>Lịch sao lưu (dịch vụ nền – lát QT3b-2).</summary>
    Schedule = 1,
}

/// <summary>Trạng thái một lần sao lưu.</summary>
public enum BackupRunStatus
{
    /// <summary>Đang chờ – các lần sao lưu chạy lần lượt từng lần một.</summary>
    Queued = 0,

    /// <summary>Đang chạy.</summary>
    Running = 1,

    /// <summary>Xong (có thể kèm cảnh báo: kiểm tra lỗi, chép nơi thứ hai lỗi – xem các cột tương ứng).</summary>
    Succeeded = 2,

    /// <summary>Lỗi – không có bản sao lưu dùng được.</summary>
    Failed = 3,
}

/// <summary>
/// Một lần sao lưu (lát QT3b, design.md mục 21.4 BAK). Không thuộc công ty nào – sao lưu cả hệ thống, chỉ xem / chạy ở tổng công ty (N3). Bảng
/// lịch sử: dòng giữ lại cả khi file sao lưu đã dọn theo hạn giữ (<see cref="PurgedAt"/>) để biết đã từng có bản nào. Đường dẫn lưu
/// <b>tương đối</b> so với <c>Backup:RootPath</c> (đổi ổ sao lưu chỉ cần chép thư mục và đổi cấu hình).
/// </summary>
public sealed class BackupRun : Entity
{
    /// <summary>Độ dài tối đa đường dẫn tương đối.</summary>
    public const int PathMaxLength = 500;

    /// <summary>Độ dài tối đa thông báo (lỗi, kiểm tra, chép nơi thứ hai, ghi chú).</summary>
    public const int MessageMaxLength = 4000;

    /// <summary>Độ dài tên file WAL (24 ký tự hex).</summary>
    public const int WalFileLength = 24;

    private BackupRun()
    {
    }

    /// <summary>Kiểu sao lưu.</summary>
    public BackupKind Kind { get; private set; }

    /// <summary>Ai đặt.</summary>
    public BackupTrigger Trigger { get; private set; }

    /// <summary>Trạng thái.</summary>
    public BackupRunStatus Status { get; private set; }

    /// <summary>Ngày (giờ Việt Nam) của lượt lịch đã sinh ra lần này – chống sinh trùng một lượt lịch; null khi chạy tay.</summary>
    public DateOnly? ScheduledDate { get; private set; }

    /// <summary>Tên đăng nhập người bấm "Sao lưu ngay"; null khi chạy theo lịch.</summary>
    public string? RequestedByName { get; private set; }

    /// <summary>Lúc vào hàng chờ (UTC).</summary>
    public DateTimeOffset QueuedAt { get; private set; }

    /// <summary>Lúc bắt đầu chạy (UTC).</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>Lúc xong / lỗi (UTC).</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Đường dẫn bản sao lưu tương đối so với <c>Backup:RootPath</c> (thư mục Full / Diff, file dump, thư mục đính kèm).</summary>
    public string? RelativePath { get; private set; }

    /// <summary>Dung lượng (byte): cả thư mục (Full / Diff), file (dump), phần mới chép (đính kèm).</summary>
    public long? SizeBytes { get; private set; }

    /// <summary>Số file: trong thư mục sao lưu (Full / Diff) hoặc số file mới chép (đính kèm).</summary>
    public int? FileCount { get; private set; }

    /// <summary>
    /// SHA256 (hex thường): của file dump; của <c>backup_manifest</c> với Full / Diff (manifest đã chứa mã kiểm tra từng file, nên trùng mã
    /// manifest là cả bản không bị sửa); null với đính kèm.
    /// </summary>
    public string? Sha256 { get; private set; }

    /// <summary>Bản Full làm gốc của bản Diff.</summary>
    public Guid? BaseFullRunId { get; private set; }

    /// <summary>File WAL đầu tiên bản Full / Diff cần (đọc từ <c>backup_label</c>) – WAL cũ hơn file này của bản Full cũ nhất còn giữ thì dọn được.</summary>
    public string? StartWalFile { get; private set; }

    /// <summary>Kết quả kiểm tra bản sao lưu (<c>pg_verifybackup</c>, <c>pg_restore --list</c>, đối chiếu file đính kèm); null = chưa kiểm tra.</summary>
    public bool? Verified { get; private set; }

    /// <summary>Ghi chú kiểm tra (lỗi khi không đạt).</summary>
    public string? VerifyMessage { get; private set; }

    /// <summary>Đã chép sang nơi lưu thứ hai (<c>Backup:SecondaryPath</c>); null = không khai nơi thứ hai.</summary>
    public bool? SecondaryCopied { get; private set; }

    /// <summary>Ghi chú chép nơi thứ hai (lỗi khi không chép được).</summary>
    public string? SecondaryMessage { get; private set; }

    /// <summary>Lỗi khi trạng thái <see cref="BackupRunStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Ghi chú thêm (ví dụ tóm tắt số file đính kèm).</summary>
    public string? Note { get; private set; }

    /// <summary>Lúc file sao lưu bị dọn theo hạn giữ (UTC); null = còn file.</summary>
    public DateTimeOffset? PurgedAt { get; private set; }

    /// <summary>
    /// Bản sao lưu dùng được để khôi phục / làm gốc Diff: đã xong, kiểm tra không lỗi, chưa bị dọn.
    /// </summary>
    public bool IsUsable => Status == BackupRunStatus.Succeeded && Verified != false && PurgedAt is null;

    /// <summary>Đặt một lần sao lưu vào hàng chờ.</summary>
    /// <param name="kind">Kiểu.</param>
    /// <param name="trigger">Ai đặt.</param>
    /// <param name="scheduledDate">Ngày lượt lịch (bắt buộc khi theo lịch, bỏ trống khi chạy tay).</param>
    /// <param name="requestedByName">Người bấm (chạy tay).</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Lần sao lưu trạng thái Đang chờ.</returns>
    /// <exception cref="BusinessRuleException">Kiểu không hợp lệ; theo lịch mà thiếu ngày lượt lịch.</exception>
    public static BackupRun Queue(BackupKind kind, BackupTrigger trigger, DateOnly? scheduledDate, string? requestedByName, DateTimeOffset now)
    {
        if (!Enum.IsDefined(kind)) throw new BusinessRuleException("Kiểu sao lưu không hợp lệ.");
        if (!Enum.IsDefined(trigger)) throw new BusinessRuleException("Nguồn đặt sao lưu không hợp lệ.");
        if (trigger == BackupTrigger.Schedule && scheduledDate is null)
            throw new BusinessRuleException("Sao lưu theo lịch phải có ngày của lượt lịch.");
        return new BackupRun
        {
            Kind = kind,
            Trigger = trigger,
            Status = BackupRunStatus.Queued,
            ScheduledDate = trigger == BackupTrigger.Schedule ? scheduledDate : null,
            RequestedByName = LogText.Cut(requestedByName, 256),
            QueuedAt = now,
        };
    }

    /// <summary>Bắt đầu chạy.</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi không ở trạng thái Đang chờ.</exception>
    public void Start(DateTimeOffset now)
    {
        if (Status != BackupRunStatus.Queued) throw new BusinessRuleException("Chỉ lần sao lưu đang chờ mới bắt đầu chạy được.");
        Status = BackupRunStatus.Running;
        StartedAt = now;
    }

    /// <summary>Ghi bản Full làm gốc của bản Diff.</summary>
    /// <param name="fullRunId">Id lần sao lưu Full.</param>
    /// <exception cref="BusinessRuleException">Khi không phải bản Diff.</exception>
    public void SetBaseFull(Guid fullRunId)
    {
        if (Kind != BackupKind.Diff) throw new BusinessRuleException("Chỉ bản Diff mới có bản Full làm gốc.");
        BaseFullRunId = fullRunId;
    }

    /// <summary>Ghi kết quả chạy xong.</summary>
    /// <param name="relativePath">Đường dẫn tương đối so với thư mục sao lưu.</param>
    /// <param name="sizeBytes">Dung lượng.</param>
    /// <param name="fileCount">Số file.</param>
    /// <param name="sha256">SHA256 (hex); null với đính kèm.</param>
    /// <param name="startWalFile">File WAL đầu tiên (Full / Diff).</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi không đang chạy.</exception>
    public void Complete(
        string? relativePath, long? sizeBytes, int? fileCount, string? sha256, string? startWalFile, string? note, DateTimeOffset now)
    {
        if (Status != BackupRunStatus.Running) throw new BusinessRuleException("Chỉ lần sao lưu đang chạy mới ghi kết quả được.");
        Status = BackupRunStatus.Succeeded;
        RelativePath = LogText.Cut(relativePath, PathMaxLength);
        SizeBytes = sizeBytes;
        FileCount = fileCount;
        Sha256 = sha256?.ToLowerInvariant();
        StartWalFile = startWalFile is { Length: WalFileLength } ? startWalFile.ToUpperInvariant() : null;
        Note = LogText.Cut(note, MessageMaxLength);
        FinishedAt = now;
    }

    /// <summary>Ghi kết quả kiểm tra bản sao lưu.</summary>
    /// <param name="passed">Đạt hay không.</param>
    /// <param name="message">Ghi chú / lỗi.</param>
    public void SetVerification(bool passed, string? message)
    {
        Verified = passed;
        VerifyMessage = LogText.Cut(message, MessageMaxLength);
    }

    /// <summary>Ghi kết quả chép sang nơi lưu thứ hai.</summary>
    /// <param name="copied">Chép được hay không.</param>
    /// <param name="message">Ghi chú / lỗi.</param>
    public void SetSecondary(bool copied, string? message)
    {
        SecondaryCopied = copied;
        SecondaryMessage = LogText.Cut(message, MessageMaxLength);
    }

    /// <summary>Ghi lỗi – kể cả khi đang chờ (ví dụ ứng dụng khởi động lại giữa chừng, cấu hình thiếu).</summary>
    /// <param name="message">Lỗi.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi lần sao lưu đã kết thúc.</exception>
    public void Fail(string message, DateTimeOffset now)
    {
        if (Status is BackupRunStatus.Succeeded or BackupRunStatus.Failed)
            throw new BusinessRuleException("Lần sao lưu đã kết thúc.");
        Status = BackupRunStatus.Failed;
        ErrorMessage = LogText.Cut(message, MessageMaxLength) ?? "Lỗi không rõ.";
        StartedAt ??= now;
        FinishedAt = now;
    }

    /// <summary>Ghi nhận file sao lưu đã bị dọn theo hạn giữ (dòng lịch sử vẫn giữ).</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    public void MarkPurged(DateTimeOffset now) => PurgedAt ??= now;
}
