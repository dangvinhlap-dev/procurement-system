using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Proc.Application.Operations;

namespace Proc.Infrastructure.Operations;

/// <summary>Thông tin kết nối truyền cho công cụ PostgreSQL (tách từ chuỗi kết nối Npgsql).</summary>
/// <param name="Host">Máy chủ (bỏ trống = mặc định của công cụ).</param>
/// <param name="Port">Cổng.</param>
/// <param name="Username">Tài khoản.</param>
/// <param name="Password">Mật khẩu – chỉ truyền qua biến môi trường <c>PGPASSWORD</c> của tiến trình con, không vào tham số lệnh.</param>
/// <param name="Database">Database trong chuỗi kết nối.</param>
public sealed record PgConnectionInfo(string? Host, int Port, string? Username, string? Password, string? Database)
{
    /// <summary>Tách chuỗi kết nối Npgsql.</summary>
    /// <param name="connectionString">Chuỗi kết nối.</param>
    /// <returns>Thông tin kết nối.</returns>
    public static PgConnectionInfo Parse(string connectionString)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        return new PgConnectionInfo(b.Host, b.Port, b.Username, b.Password, b.Database);
    }
}

/// <summary>Kết quả chạy một công cụ PostgreSQL.</summary>
/// <param name="ExitCode">Mã thoát (-1 khi quá giờ / không chạy được).</param>
/// <param name="StdOut">Đầu ra chuẩn (đã cắt bớt khi dài).</param>
/// <param name="StdErr">Đầu ra lỗi (giữ phần cuối khi dài – thông báo lỗi thật thường ở cuối).</param>
/// <param name="StdOutLines">Số dòng đầu ra chuẩn (đếm đủ, kể cả phần đã cắt).</param>
/// <param name="TimedOut">Quá thời gian <c>Backup:ToolTimeoutMinutes</c>.</param>
public sealed record PgToolResult(int ExitCode, string StdOut, string StdErr, int StdOutLines, bool TimedOut)
{
    /// <summary>Chạy xong, mã thoát 0.</summary>
    public bool Succeeded => ExitCode == 0 && !TimedOut;

    /// <summary>Câu lỗi ngắn để ghi vào lần sao lưu.</summary>
    /// <param name="tool">Tên công cụ.</param>
    /// <returns>Câu lỗi.</returns>
    public string Describe(string tool) => TimedOut
        ? $"{tool} chạy quá thời gian cho phép (Backup:ToolTimeoutMinutes) nên đã bị dừng."
        : $"{tool} báo lỗi (mã {ExitCode.ToString(CultureInfo.InvariantCulture)}): {(StdErr.Length > 0 ? StdErr : StdOut)}".Trim();
}

/// <summary>
/// Dựng tham số lệnh cho công cụ PostgreSQL (hàm thuần – test được không cần PostgreSQL). Mỗi phần tử là một tham số riêng, truyền qua
/// <see cref="ProcessStartInfo.ArgumentList"/> nên đường dẫn có dấu cách, dấu tiếng Việt không cần tự thêm ngoặc kép và không qua shell
/// (không có nguy cơ chèn lệnh).
/// </summary>
public static class PgToolArgs
{
    /// <summary>
    /// <c>pg_basebackup</c> Full / Diff: thư mục plain (<c>-Fp</c> – <c>pg_combinebackup</c> chỉ ghép được thư mục), kèm WAL của chính lần sao lưu
    /// (<c>-X stream</c>) để bản tự khôi phục được tới thời điểm sao lưu xong, checkpoint nhanh (<c>-c fast</c>), không hỏi mật khẩu (<c>-w</c>).
    /// Diff: <c>--incremental &lt;backup_manifest của bản Full&gt;</c> – luôn lấy bản Full làm gốc (design.md 21.5).
    /// </summary>
    /// <param name="conn">Kết nối tài khoản có quyền REPLICATION.</param>
    /// <param name="targetDir">Thư mục đích (chưa có hoặc rỗng) – tương đối so với thư mục làm việc, không dấu.</param>
    /// <param name="label">Nhãn lần sao lưu (ghi trong backup_label).</param>
    /// <param name="incrementalManifest">File <c>backup_manifest</c> của bản Full gốc (tương đối, không dấu); null = Full.</param>
    /// <returns>Tham số.</returns>
    public static IReadOnlyList<string> BaseBackup(PgConnectionInfo conn, string targetDir, string label, string? incrementalManifest)
    {
        ArgumentNullException.ThrowIfNull(conn);
        var args = Connection(conn);
        args.AddRange(["-D", targetDir, "-Fp", "-X", "stream", "-c", "fast", "-l", label, "-w"]);
        if (incrementalManifest is not null) args.AddRange(["--incremental", incrementalManifest]);
        return args;
    }

    /// <summary><c>pg_verifybackup -q</c>: kiểm tra mã từng file theo manifest và đọc được WAL đi kèm; đạt thì không in gì.</summary>
    /// <param name="backupDir">Thư mục bản sao lưu.</param>
    /// <returns>Tham số.</returns>
    public static IReadOnlyList<string> VerifyBackup(string backupDir) => ["-q", backupDir];

    /// <summary><c>pg_dump -Fc</c> (định dạng nén, khôi phục chọn lọc được từng bảng) ra file.</summary>
    /// <param name="conn">Kết nối.</param>
    /// <param name="database">Database cần dump.</param>
    /// <param name="file">File đích.</param>
    /// <returns>Tham số.</returns>
    public static IReadOnlyList<string> Dump(PgConnectionInfo conn, string database, string file)
    {
        ArgumentNullException.ThrowIfNull(conn);
        var args = Connection(conn);
        args.AddRange(["-d", database, "-Fc", "-f", file, "-w"]);
        return args;
    }

    /// <summary><c>pg_restore --list</c>: đọc mục lục của file dump – đọc được hết là file không hỏng phần đầu / mục lục.</summary>
    /// <param name="file">File dump.</param>
    /// <returns>Tham số.</returns>
    public static IReadOnlyList<string> RestoreList(string file) => ["--list", file];

    private static List<string> Connection(PgConnectionInfo conn)
    {
        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(conn.Host)) args.AddRange(["-h", conn.Host]);
        args.AddRange(["-p", conn.Port.ToString(CultureInfo.InvariantCulture)]);
        if (!string.IsNullOrWhiteSpace(conn.Username)) args.AddRange(["-U", conn.Username]);
        return args;
    }

    /// <summary>
    /// Đọc file WAL bắt đầu từ <c>backup_label</c>, dòng dạng <c>START WAL LOCATION: 0/2000028 (file 000000010000000000000002)</c>.
    /// </summary>
    /// <param name="backupLabel">Nội dung file backup_label.</param>
    /// <returns>Tên file WAL (24 ký tự hex); null khi không đọc được.</returns>
    public static string? ParseStartWalFile(string? backupLabel)
    {
        if (backupLabel is null) return null;
        var m = Regex.Match(backupLabel, @"^START WAL LOCATION: .*\(file ([0-9A-Fa-f]{24})\)", RegexOptions.Multiline, TimeSpan.FromSeconds(1));
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }
}

/// <summary>
/// Chạy công cụ PostgreSQL (lát QT3b): tìm file chạy (<c>Backup:PgBinPath</c> → PATH → Program Files\PostgreSQL\&lt;bản cao nhất&gt;\bin trên
/// Windows), truyền mật khẩu qua biến môi trường tiến trình con, giới hạn thời gian, đọc stdout / stderr song song (không kẹt khi đầu ra lớn).
/// </summary>
public sealed class PgToolRunner
{
    private const int MaxCapturedChars = 4000;

    private readonly BackupOptions _options;
    private readonly ILogger<PgToolRunner> _logger;

    /// <summary>Tạo bộ chạy.</summary>
    /// <param name="options">Cấu hình sao lưu.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public PgToolRunner(IOptions<BackupOptions> options, ILogger<PgToolRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Đường dẫn file chạy của công cụ.</summary>
    /// <param name="tool">Tên công cụ, ví dụ <c>pg_dump</c>.</param>
    /// <returns>Đường dẫn đầy đủ khi tìm thấy; không thấy thì trả tên công cụ (để hệ điều hành tự tìm, lỗi sẽ báo khi chạy).</returns>
    public string Resolve(string tool) => Locate(tool, _options.PgBinPath, Environment.GetEnvironmentVariable("PATH"),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)) ?? tool;

    /// <summary>Tìm file chạy (hàm tĩnh để test).</summary>
    /// <param name="tool">Tên công cụ.</param>
    /// <param name="binPath">Thư mục cấu hình.</param>
    /// <param name="pathVariable">Biến PATH.</param>
    /// <param name="programFiles">Thư mục Program Files (Windows).</param>
    /// <returns>Đường dẫn đầy đủ hoặc null.</returns>
    public static string? Locate(string tool, string? binPath, string? pathVariable, string? programFiles)
    {
        var file = OperatingSystem.IsWindows() ? tool + ".exe" : tool;
        if (!string.IsNullOrWhiteSpace(binPath)) return Path.Combine(binPath, file);

        foreach (var dir in (pathVariable ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim('"'), file);
            if (File.Exists(candidate)) return candidate;
        }

        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(programFiles)) return null;
        var root = Path.Combine(programFiles, "PostgreSQL");
        if (!Directory.Exists(root)) return null;
        // Bản cao nhất trước (18 > 17 > 9.6): so theo số, không theo chữ.
        return Directory.EnumerateDirectories(root)
            .Select(d => (Dir: d, Version: decimal.TryParse(Path.GetFileName(d), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : -1))
            .OrderByDescending(x => x.Version)
            .Select(x => Path.Combine(x.Dir, "bin", file))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Chạy công cụ. <b>Đường dẫn file trong tham số phải là đường dẫn tương đối không dấu, tính từ <paramref name="workingDirectory"/></b>:
    /// trên Windows công cụ PostgreSQL đọc tham số theo bảng mã ANSI nên chữ có dấu tiếng Việt bị đổi (thử thật 15/11/2026: "sao lưu" thành
    /// "sao luu", pg_dump báo không mở được file); thư mục làm việc thì Windows truyền dạng Unicode nên thư mục sao lưu đặt tên có dấu vẫn chạy.
    /// </summary>
    /// <param name="tool">Tên công cụ.</param>
    /// <param name="args">Tham số (<see cref="PgToolArgs"/>).</param>
    /// <param name="password">Mật khẩu (biến môi trường PGPASSWORD); null = không đặt.</param>
    /// <param name="workingDirectory">Thư mục làm việc của tiến trình con (phải có sẵn).</param>
    /// <param name="cancellationToken">Hủy (ứng dụng dừng) – dừng cả tiến trình con.</param>
    /// <returns>Kết quả; không chạy được (thiếu file) thì mã -1 kèm lỗi.</returns>
    /// <exception cref="ArgumentException">Khi tham số có ký tự ngoài ASCII (chỉ tên đăng nhập, nhãn, đường dẫn do phần mềm tự đặt).</exception>
    public async Task<PgToolResult> RunAsync(
        string tool, IReadOnlyList<string> args, string? password, string workingDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.FirstOrDefault(a => !System.Text.Ascii.IsValid(a)) is { } bad)
            throw new ArgumentException($"Tham số lệnh {tool} có ký tự ngoài ASCII ({bad}) – công cụ PostgreSQL trên Windows đọc sai.", nameof(args));
        var psi = new ProcessStartInfo(Resolve(tool))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (password is not null) psi.Environment["PGPASSWORD"] = password;
        psi.Environment["PGCONNECT_TIMEOUT"] = "30";
        // Thông báo tiếng Anh: tránh chữ bản địa theo bảng mã của hệ điều hành bị lỗi font khi ghi vào CSDL.
        psi.Environment["LC_MESSAGES"] = "C";
        psi.Environment["LANG"] = "C";

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(Math.Max(1, _options.ToolTimeoutMinutes)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new PgToolResult(-1, string.Empty,
                $"Không chạy được {tool} ({psi.FileName}): {ex.Message}. Khai Backup:PgBinPath là thư mục bin của PostgreSQL 18.", 0, false);
        }

        var stdout = ReadAsync(process.StandardOutput, keepTail: false);
        var stderr = ReadAsync(process.StandardError, keepTail: true);
        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Tiến trình vừa tự thoát.
            }

            if (cancellationToken.IsCancellationRequested) throw;
            _logger.LogWarning("{Tool} quá thời gian {Minutes} phút, đã dừng.", tool, _options.ToolTimeoutMinutes);
            var (o, _) = await stdout;
            var (e, _) = await stderr;
            return new PgToolResult(-1, o, e, 0, true);
        }

        var (outText, outLines) = await stdout;
        var (errText, _) = await stderr;
        return new PgToolResult(process.ExitCode, outText, errText, outLines, false);
    }

    /// <summary>Đọc hết một luồng, giữ phần đầu (stdout) hoặc phần cuối (stderr) tối đa 4000 ký tự, đếm số dòng.</summary>
    private static async Task<(string Text, int Lines)> ReadAsync(StreamReader reader, bool keepTail)
    {
        var sb = new StringBuilder();
        var lines = 0;
        while (await reader.ReadLineAsync() is { } line)
        {
            lines++;
            if (keepTail)
            {
                sb.AppendLine(line);
                if (sb.Length > MaxCapturedChars * 2) sb.Remove(0, sb.Length - MaxCapturedChars);
            }
            else if (sb.Length < MaxCapturedChars)
            {
                sb.AppendLine(line);
            }
        }

        var text = sb.ToString().Trim();
        if (text.Length > MaxCapturedChars) text = keepTail ? text[^MaxCapturedChars..] : text[..MaxCapturedChars];
        return (text, lines);
    }
}
