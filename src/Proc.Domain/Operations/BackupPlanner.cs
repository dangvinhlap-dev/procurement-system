namespace Proc.Domain.Operations;

/// <summary>Tham số lịch sao lưu (của tổng công ty – B1, design.md mục 21.1).</summary>
/// <param name="FullDay">Ngày chạy Full: 1 = Thứ hai … 7 = Chủ nhật.</param>
/// <param name="Time">Giờ chạy (giờ Việt Nam).</param>
/// <param name="KeepWeeks">Số tuần giữ bản Full (kèm Diff của nó).</param>
/// <param name="DumpKeepDays">Số ngày giữ bản pg_dump.</param>
public sealed record BackupScheduleSettings(int FullDay, TimeOnly Time, int KeepWeeks, int DumpKeepDays);

/// <summary>
/// Quy tắc lịch và thời hạn giữ bản sao lưu (lát QT3b-2, design.md mục 21.5 – hàm thuần, test không cần PostgreSQL):
/// <list type="bullet">
/// <item><b>Lượt lịch</b>: mỗi ngày một lượt lúc <c>BACKUP_TIME</c> giờ Việt Nam. Ứng dụng tắt đúng giờ đó thì chạy bù khi bật lại <b>trong cùng
/// ngày</b> (không bù các ngày trước – bản mới hơn thay được). Mỗi lượt: Full (ngày <c>BACKUP_FULL_DAY</c>, hoặc khi chưa có bản Full dùng được)
/// hay Diff, rồi pg_dump, rồi đính kèm.</item>
/// <item><b>Giữ</b>: Full cũ hơn <c>BACKUP_KEEP_WEEKS</c> tuần dọn cùng các Diff lấy nó làm gốc; Diff tự hết hạn cùng mốc đó; dump cũ hơn
/// <c>DUMP_KEEP_DAYS</c> ngày. <b>Luôn giữ bản Full dùng được mới nhất</b> (và các Diff của nó) cùng bản dump dùng được mới nhất – sao lưu lỗi
/// nhiều ngày liền cũng không dọn mất bản cuối cùng còn dùng được. Bản đính kèm chép bổ sung, không dọn.</item>
/// <item><b>WAL</b>: giữ từ file WAL đầu của bản Full cũ nhất còn giữ (design.md B1 "WAL liên tục từ bản Full cũ nhất còn giữ").</item>
/// </list>
/// </summary>
public static class BackupPlanner
{
    /// <summary>Ngày của lượt lịch đã tới giờ chạy (giờ Việt Nam); null khi hôm nay chưa tới giờ.</summary>
    /// <param name="nowVietnam">Giờ hiện tại theo giờ Việt Nam.</param>
    /// <param name="time">Giờ chạy.</param>
    /// <returns>Ngày lượt lịch.</returns>
    public static DateOnly? DueSlot(DateTime nowVietnam, TimeOnly time) =>
        TimeOnly.FromDateTime(nowVietnam) >= time ? DateOnly.FromDateTime(nowVietnam) : null;

    /// <summary>Thứ theo ISO: 1 = Thứ hai … 7 = Chủ nhật.</summary>
    /// <param name="date">Ngày.</param>
    /// <returns>1–7.</returns>
    public static int IsoDay(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    /// <summary>
    /// Kiểu sao lưu CSDL của lượt lịch: Full vào ngày Full, hoặc khi chưa có bản Full dùng được (Diff không có gốc); còn lại Diff.
    /// </summary>
    /// <param name="slot">Ngày lượt lịch.</param>
    /// <param name="fullDay">Ngày chạy Full (1–7).</param>
    /// <param name="hasUsableFull">Đang có bản Full dùng được.</param>
    /// <returns>Full hoặc Diff.</returns>
    public static BackupKind BaseKind(DateOnly slot, int fullDay, bool hasUsableFull) =>
        IsoDay(slot) == fullDay || !hasUsableFull ? BackupKind.Full : BackupKind.Diff;

    /// <summary>Chọn các lần sao lưu đã xong cần dọn file theo hạn giữ.</summary>
    /// <param name="runs">Các lần sao lưu (mọi trạng thái; lần đã dọn, lỗi, đính kèm tự bỏ qua).</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <param name="settings">Tham số giữ.</param>
    /// <returns>Các lần cần dọn.</returns>
    public static IReadOnlyList<BackupRun> SelectPurge(IEnumerable<BackupRun> runs, DateTimeOffset now, BackupScheduleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var live = runs.Where(r => r.Status == BackupRunStatus.Succeeded && r.PurgedAt is null && r.Kind != BackupKind.Attachments)
            .OrderByDescending(r => r.FinishedAt)
            .ToList();
        var fullCutoff = now.AddDays(-7 * settings.KeepWeeks);
        var dumpCutoff = now.AddDays(-settings.DumpKeepDays);

        var newestFull = live.FirstOrDefault(r => r.Kind == BackupKind.Full && r.IsUsable);
        var newestDump = live.FirstOrDefault(r => r.Kind == BackupKind.Dump && r.IsUsable);
        var purgedFulls = live
            .Where(r => r.Kind == BackupKind.Full && r != newestFull && r.FinishedAt < fullCutoff)
            .Select(r => r.Id)
            .ToHashSet();

        var result = new List<BackupRun>();
        foreach (var r in live)
        {
            var purge = r.Kind switch
            {
                BackupKind.Full => purgedFulls.Contains(r.Id),
                // Diff đi theo bản Full gốc: gốc bị dọn (hoặc đã dọn từ trước) thì Diff vô dụng; gốc còn giữ thì Diff giữ theo cùng mốc, trừ khi là
                // Diff của bản Full mới nhất (bản Full đó luôn giữ nên các Diff của nó cũng giữ).
                BackupKind.Diff => r.BaseFullRunId is not { } baseId
                                   || purgedFulls.Contains(baseId)
                                   || !live.Any(f => f.Id == baseId)
                                   || (baseId != newestFull?.Id && r.FinishedAt < fullCutoff),
                BackupKind.Dump => r != newestDump && r.FinishedAt < dumpCutoff,
                _ => false,
            };
            if (purge) result.Add(r);
        }

        return result;
    }

    /// <summary>
    /// File WAL cũ nhất còn cần: file WAL đầu của bản Full cũ nhất còn giữ (dùng được, có ghi file WAL). WAL cũ hơn file này dọn được bằng
    /// <c>pg_archivecleanup</c>. Null = không dọn (chưa có bản Full nào ghi được file WAL – dọn lúc đó có thể mất WAL cần cho khôi phục).
    /// </summary>
    /// <param name="keptRuns">Các lần sao lưu còn giữ sau khi dọn.</param>
    /// <returns>Tên file WAL.</returns>
    public static string? OldestNeededWal(IEnumerable<BackupRun> keptRuns) =>
        keptRuns.Where(r => r.Kind == BackupKind.Full && r.IsUsable && r.StartWalFile is not null)
            .OrderBy(r => r.FinishedAt)
            .Select(r => r.StartWalFile)
            .FirstOrDefault();
}
