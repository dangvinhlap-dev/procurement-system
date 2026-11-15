using System.Globalization;

namespace Proc.Domain.Operations;

/// <summary>Bản sao lưu Full / Diff dùng được – đầu vào của <see cref="RestorePlanner"/>.</summary>
/// <param name="Id">Id lần sao lưu.</param>
/// <param name="Kind">Full hoặc Diff.</param>
/// <param name="FinishedAt">Lúc sao lưu xong – bản chỉ nhất quán từ lúc này, mốc khôi phục phải sau.</param>
/// <param name="StartWalFile">File WAL đầu tiên bản cần.</param>
/// <param name="BaseFullRunId">Bản Full gốc (Diff).</param>
/// <param name="RelativePath">Thư mục bản sao lưu (tương đối so với thư mục sao lưu).</param>
public sealed record RestoreBackupCandidate(
    Guid Id, BackupKind Kind, DateTimeOffset FinishedAt, string? StartWalFile, Guid? BaseFullRunId, string? RelativePath);

/// <summary>Một file trong thư mục WAL.</summary>
/// <param name="Name">Tên file.</param>
/// <param name="LastWriteUtc">Giờ sửa cuối – PostgreSQL ghi xong một file WAL rồi mới chép, nên giờ này ≈ lúc kết thúc đoạn WAL.</param>
public sealed record WalArchiveFile(string Name, DateTimeOffset LastWriteUtc);

/// <summary>
/// Kế hoạch khôi phục về mốc thời gian.
/// </summary>
/// <param name="TargetTime">Mốc.</param>
/// <param name="Feasible">Đủ chuỗi để khôi phục.</param>
/// <param name="FullRunId">Bản Full dùng.</param>
/// <param name="FullFinishedAt">Lúc bản Full xong.</param>
/// <param name="FullPath">Thư mục bản Full.</param>
/// <param name="DiffRunId">Bản Diff dùng (null = không có Diff sau Full mà trước mốc).</param>
/// <param name="DiffFinishedAt">Lúc bản Diff xong.</param>
/// <param name="DiffPath">Thư mục bản Diff.</param>
/// <param name="FirstWal">File WAL đầu cần phát lại.</param>
/// <param name="LastWal">File WAL chứa mốc (file đầu tiên có giờ ghi ≥ mốc).</param>
/// <param name="WalCount">Số file WAL cần.</param>
/// <param name="LastArchivedAt">Giờ file WAL mới nhất đã lưu.</param>
/// <param name="Problems">Lý do không khôi phục được (rỗng khi <paramref name="Feasible"/>).</param>
/// <param name="Warnings">Lưu ý (không chặn).</param>
public sealed record RestorePlan(
    DateTimeOffset TargetTime, bool Feasible, Guid? FullRunId, DateTimeOffset? FullFinishedAt, string? FullPath, Guid? DiffRunId,
    DateTimeOffset? DiffFinishedAt, string? DiffPath, string? FirstWal, string? LastWal, int WalCount, DateTimeOffset? LastArchivedAt,
    IReadOnlyList<string> Problems, IReadOnlyList<string> Warnings);

/// <summary>
/// Chọn chuỗi khôi phục về mốc thời gian (lát QT6, design.md mục 21.4 RST – hàm thuần, test không cần PostgreSQL):
/// <list type="number">
/// <item>Bản <b>Full</b> dùng được xong <b>trước mốc</b>, mới nhất.</item>
/// <item>Bản <b>Diff</b> dùng được của chính Full đó, xong trước mốc, mới nhất (Diff luôn lấy Full làm gốc – không nối Diff với nhau). Không có thì
/// chỉ Full (phát lại nhiều WAL hơn).</item>
/// <item><b>WAL liên tục</b> từ file WAL đầu của bản gốc (Diff nếu có, không thì Full) tới file đầu tiên có giờ ghi ≥ mốc. Thiếu một file giữa chừng
/// là <b>đứt WAL</b> – chỉ khôi phục được tới trước chỗ đứt. Chưa có file nào ghi sau mốc (mốc quá gần, WAL chưa lưu – tối đa
/// <c>archive_timeout</c> 5 phút) thì PostgreSQL sẽ dừng lỗi "recovery ended before configured recovery target was reached" nên cũng chặn.</item>
/// </list>
/// Số thứ tự đoạn WAL: tên file <c>TTTTTTTTLLLLLLLLSSSSSSSS</c> (timeline, log, segment, hex) với đoạn 16 MB mặc định thì mỗi "log" có 256 đoạn –
/// số thứ tự = L × 256 + S. Ví dụ <c>00000001000000020000000A</c> → 2 × 256 + 10 = 522; đoạn sau là <c>…000000020000000B</c> (523), sau
/// <c>…000000FF</c> là <c>…0000000300000000</c>. Sau một lần khôi phục, timeline tăng (file <c>00000002…</c>) – đoạn ở timeline nào cũng tính,
/// miễn không thấp hơn timeline của bản gốc.
/// </summary>
public static class RestorePlanner
{
    /// <summary>Số đoạn WAL mỗi "log" khi đoạn 16 MB (mặc định của PostgreSQL, <c>qt-enable-wal.ps1</c> không đổi).</summary>
    public const long SegmentsPerLog = 0x100;

    /// <summary>Lập kế hoạch.</summary>
    /// <param name="targetTime">Mốc cần về.</param>
    /// <param name="now">Giờ hiện tại.</param>
    /// <param name="backups">Các bản Full / Diff dùng được (đã xong, kiểm tra không lỗi, chưa dọn).</param>
    /// <param name="walFiles">File trong thư mục WAL.</param>
    /// <param name="displayZone">Múi giờ ghi giờ trong câu báo (giờ Việt Nam).</param>
    /// <returns>Kế hoạch (có lý do khi không khôi phục được).</returns>
    public static RestorePlan Plan(
        DateTimeOffset targetTime, DateTimeOffset now, IEnumerable<RestoreBackupCandidate> backups, IEnumerable<WalArchiveFile> walFiles,
        TimeZoneInfo displayZone)
    {
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(walFiles);
        ArgumentNullException.ThrowIfNull(displayZone);
        string Show(DateTimeOffset t) => TimeZoneInfo.ConvertTime(t, displayZone).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
        var problems = new List<string>();
        var warnings = new List<string>();
        var list = backups.ToList();
        var segments = walFiles
            .Select(f => (File: f, Seg: Parse(f.Name)))
            .Where(x => x.Seg is not null)
            .Select(x => (x.File, Seg: x.Seg!.Value))
            .ToList();
        DateTimeOffset? lastArchived = segments.Count == 0 ? null : segments.Max(s => s.File.LastWriteUtc);

        RestorePlan Result(RestoreBackupCandidate? full, RestoreBackupCandidate? diff, string? first, string? last, int count) => new(
            targetTime, problems.Count == 0 && full is not null, full?.Id, full?.FinishedAt, full?.RelativePath, diff?.Id, diff?.FinishedAt,
            diff?.RelativePath, first, last, count, lastArchived, problems, warnings);

        if (targetTime > now)
        {
            problems.Add("Mốc khôi phục ở tương lai – chọn mốc đã qua.");
            return Result(null, null, null, null, 0);
        }

        var full = list.Where(b => b.Kind == BackupKind.Full && b.FinishedAt <= targetTime).MaxBy(b => b.FinishedAt);
        if (full is null)
        {
            var oldest = list.Where(b => b.Kind == BackupKind.Full).MinBy(b => b.FinishedAt);
            problems.Add(oldest is null
                ? "Chưa có bản sao lưu Full nào dùng được."
                : $"Không có bản Full nào xong trước mốc – bản Full cũ nhất còn giữ xong lúc {Show(oldest.FinishedAt)}; chọn mốc sau giờ đó.");
            return Result(null, null, null, null, 0);
        }

        var diff = list.Where(b => b.Kind == BackupKind.Diff && b.BaseFullRunId == full.Id && b.FinishedAt <= targetTime).MaxBy(b => b.FinishedAt);
        if (diff is null && list.Any(b => b.Kind == BackupKind.Diff && b.BaseFullRunId == full.Id))
            warnings.Add("Các bản Diff của bản Full này đều xong sau mốc – khôi phục từ bản Full, phát lại nhiều WAL hơn.");
        var baseRun = diff ?? full;
        var start = Parse(baseRun.StartWalFile ?? string.Empty);
        if (start is null)
        {
            problems.Add($"Bản {(diff is null ? "Full" : "Diff")} xong lúc {Show(baseRun.FinishedAt)} không ghi file WAL đầu (thiếu backup_label) – " +
                "không biết phát lại WAL từ đâu.");
            return Result(full, diff, null, null, 0);
        }

        // Đoạn WAL theo số thứ tự (timeline không thấp hơn bản gốc). Cùng số ở hai timeline (chỗ đổi timeline) thì lấy timeline cao hơn – đoạn đã
        // phát lại sau lần khôi phục trước.
        var bySeg = segments
            .Where(s => s.Seg.Timeline >= start.Value.Timeline && s.Seg.Number >= start.Value.Number)
            .GroupBy(s => s.Seg.Number)
            .ToDictionary(g => g.Key, g => g.MaxBy(s => s.Seg.Timeline));

        var number = start.Value.Number;
        string? firstName = null;
        string? lastName = null;
        var count = 0;
        while (true)
        {
            if (!bySeg.TryGetValue(number, out var seg))
            {
                if (bySeg.Keys.Any(k => k > number))
                {
                    problems.Add($"Đứt WAL: thiếu file đoạn {SegmentName(start.Value.Timeline, number)}" +
                        (lastName is null ? " (ngay đoạn đầu bản sao lưu cần)" : $" sau {lastName}") +
                        " – chỉ khôi phục được tới trước chỗ đứt. Chọn mốc sớm hơn hoặc dùng bản Full / Diff mới hơn.");
                }
                else
                {
                    problems.Add(lastArchived is null
                        ? "Thư mục WAL chưa có file nào – kiểm tra lưu WAL ở màn hình BAK."
                        : $"Mốc sau lần lưu WAL gần nhất ({Show(lastArchived.Value)}) – chọn mốc sớm hơn hoặc chờ WAL được lưu (tối đa 5 phút).");
                }

                return Result(full, diff, firstName, lastName, count);
            }

            firstName ??= seg.File.Name;
            lastName = seg.File.Name;
            count++;
            if (seg.File.LastWriteUtc >= targetTime) break;
            number++;
        }

        if (diff is null && !warnings.Any())
            warnings.Add("Không có bản Diff sau bản Full mà trước mốc – khôi phục từ bản Full, phát lại nhiều WAL hơn.");
        return Result(full, diff, firstName, lastName, count);
    }

    /// <summary>Đọc tên file WAL đoạn thường (24 ký tự hex); file khác (<c>.history</c>, <c>.partial</c>, <c>.backup</c>, <c>.tmp</c>) trả null.</summary>
    /// <param name="name">Tên file.</param>
    /// <returns>(timeline, số thứ tự đoạn) hoặc null.</returns>
    public static (long Timeline, long Number)? Parse(string name)
    {
        if (name is not { Length: BackupRun.WalFileLength } || !name.All(char.IsAsciiHexDigit)) return null;
        var tl = long.Parse(name.AsSpan(0, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var log = long.Parse(name.AsSpan(8, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var seg = long.Parse(name.AsSpan(16, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (seg >= SegmentsPerLog) return null;
        return (tl, log * SegmentsPerLog + seg);
    }

    /// <summary>Tên file của đoạn WAL.</summary>
    /// <param name="timeline">Timeline.</param>
    /// <param name="number">Số thứ tự đoạn.</param>
    /// <returns>24 ký tự hex in hoa.</returns>
    public static string SegmentName(long timeline, long number) => string.Create(CultureInfo.InvariantCulture,
        $"{timeline:X8}{number / SegmentsPerLog:X8}{number % SegmentsPerLog:X8}");
}
