using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Proc.Domain.Common;

namespace Proc.Domain.Operations;

/// <summary>Nguồn lỗi (design.md mục 21.4 – màn hình ERR).</summary>
public enum ErrorSource
{
    /// <summary>Lỗi máy chủ (HTTP 500).</summary>
    Server = 0,

    /// <summary>Lỗi giao diện do trình duyệt gửi về.</summary>
    Client = 1,
}

/// <summary>Trạng thái xử lý một nhóm lỗi.</summary>
public enum ErrorGroupStatus
{
    /// <summary>Mới – chưa ai nhận xử lý (hoặc đã xử lý mà lặp lại).</summary>
    New = 0,

    /// <summary>Đang xử lý.</summary>
    InProgress = 1,

    /// <summary>Đã xử lý.</summary>
    Resolved = 2,
}

/// <summary>
/// Nhóm lỗi (lát QT2, design.md mục 21.4 ERR): mọi lần xảy ra có cùng dấu vết (nguồn + loại lỗi + vị trí đầu ngăn xếp + đường dẫn chuẩn hóa)
/// gộp một nhóm để người phụ trách theo dõi một lần, kèm số lần, lần đầu / lần cuối. Nhóm Đã xử lý mà lặp lại thì tự mở lại Mới – lỗi chưa
/// thật sự hết. Không thuộc công ty nào: lỗi là của hệ thống, chỉ xem ở tổng công ty (N3).
/// </summary>
public sealed class ErrorGroup : Entity
{
    /// <summary>Độ dài dấu vết (SHA-256 dạng hex).</summary>
    public const int FingerprintLength = 64;

    /// <summary>Độ dài tối đa của loại lỗi.</summary>
    public const int TypeMaxLength = 200;

    /// <summary>Độ dài tối đa của nội dung, vị trí.</summary>
    public const int TextMaxLength = 1000;

    /// <summary>Độ dài tối đa của đường dẫn.</summary>
    public const int PathMaxLength = 500;

    /// <summary>Độ dài tối đa của ghi chú xử lý.</summary>
    public const int NoteMaxLength = 2000;

    private ErrorGroup()
    {
    }

    /// <summary>Dấu vết – khóa gộp nhóm (duy nhất).</summary>
    public string Fingerprint { get; private set; } = string.Empty;

    /// <summary>Nguồn lỗi.</summary>
    public ErrorSource Source { get; private set; }

    /// <summary>Loại lỗi, ví dụ System.InvalidOperationException hoặc TypeError.</summary>
    public string ErrorType { get; private set; } = string.Empty;

    /// <summary>Nội dung của lần xảy ra đầu tiên.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Vị trí đầu ngăn xếp đã chuẩn hóa (bỏ số dòng, mã băm file) – phần của dấu vết.</summary>
    public string? Location { get; private set; }

    /// <summary>Đường dẫn chuẩn hóa (Id → {id}, số → {n}).</summary>
    public string? Path { get; private set; }

    /// <summary>Lần đầu (UTC).</summary>
    public DateTimeOffset FirstSeenAt { get; private set; }

    /// <summary>Lần cuối (UTC).</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Số lần xảy ra (kể cả lần đã bị dọn theo hạn giữ).</summary>
    public int OccurrenceCount { get; private set; }

    /// <summary>Trạng thái xử lý.</summary>
    public ErrorGroupStatus Status { get; private set; }

    /// <summary>Người đổi trạng thái gần nhất (tên đăng nhập).</summary>
    public string? HandledByName { get; private set; }

    /// <summary>Lúc đổi trạng thái gần nhất (UTC).</summary>
    public DateTimeOffset? HandledAt { get; private set; }

    /// <summary>Ghi chú xử lý.</summary>
    public string? Note { get; private set; }

    /// <summary>Số lần nhóm tự mở lại vì lỗi lặp sau khi đã xử lý.</summary>
    public int ReopenCount { get; private set; }

    /// <summary>Tạo nhóm lỗi từ lần xảy ra đầu tiên.</summary>
    /// <param name="fingerprint">Dấu vết (<see cref="ErrorFingerprint.Compute"/>).</param>
    /// <param name="source">Nguồn.</param>
    /// <param name="errorType">Loại lỗi.</param>
    /// <param name="message">Nội dung.</param>
    /// <param name="location">Vị trí đầu ngăn xếp đã chuẩn hóa.</param>
    /// <param name="path">Đường dẫn chuẩn hóa.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Nhóm lỗi trạng thái Mới, 1 lần.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu dấu vết.</exception>
    public static ErrorGroup Create(
        string fingerprint, ErrorSource source, string? errorType, string? message, string? location, string? path, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(fingerprint)) throw new BusinessRuleException("Nhóm lỗi phải có dấu vết.");
        return new ErrorGroup
        {
            Fingerprint = fingerprint,
            Source = source,
            ErrorType = LogText.Cut(errorType, TypeMaxLength) ?? "Error",
            Message = LogText.Cut(message, TextMaxLength) ?? "(không có nội dung)",
            Location = LogText.Cut(location, TextMaxLength),
            Path = LogText.Cut(path, PathMaxLength),
            FirstSeenAt = now,
            LastSeenAt = now,
            OccurrenceCount = 1,
            Status = ErrorGroupStatus.New,
        };
    }

    /// <summary>
    /// Đổi trạng thái xử lý. Ghi chú bỏ trống thì giữ ghi chú cũ – người xử lý chỉ đổi trạng thái không làm mất ghi chú trước đó.
    /// </summary>
    /// <param name="status">Trạng thái mới.</param>
    /// <param name="note">Ghi chú (null/rỗng = giữ nguyên).</param>
    /// <param name="userName">Người đổi.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    public void ChangeStatus(ErrorGroupStatus status, string? note, string? userName, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status)) throw new BusinessRuleException("Trạng thái nhóm lỗi không hợp lệ.");
        Status = status;
        if (LogText.Cut(note, NoteMaxLength) is { } n) Note = n;
        HandledByName = LogText.Cut(userName, 256);
        HandledAt = now;
    }
}

/// <summary>
/// Một lần xảy ra lỗi (lát QT2): mã theo dõi (trùng mã người dùng thấy trên câu báo lỗi), người, công ty, đường dẫn, chi tiết kỹ thuật cắt
/// 16 KB, phiên bản, môi trường. Dọn theo tham số <c>ERRLOG_KEEP_DAYS</c>.
/// </summary>
public sealed class ErrorOccurrence : Entity
{
    /// <summary>Độ dài tối đa chi tiết kỹ thuật (16 KB ký tự).</summary>
    public const int DetailMaxLength = 16 * 1024;

    /// <summary>Độ dài tối đa của mã theo dõi.</summary>
    public const int TrackingCodeMaxLength = 20;

    private ErrorOccurrence()
    {
    }

    /// <summary>Nhóm lỗi.</summary>
    public Guid GroupId { get; private set; }

    /// <summary>Thời điểm (UTC).</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Mã theo dõi, ví dụ ERR-7K3F9Q2A.</summary>
    public string TrackingCode { get; private set; } = string.Empty;

    /// <summary>Nguồn.</summary>
    public ErrorSource Source { get; private set; }

    /// <summary>Người dùng; null = chưa đăng nhập / hệ thống.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Tên đăng nhập.</summary>
    public string? UserName { get; private set; }

    /// <summary>Công ty đang làm việc (không ràng buộc khóa ngoại – nhật ký giữ được cả khi công ty đổi).</summary>
    public Guid? CompanyId { get; private set; }

    /// <summary>Mã công ty lúc xảy ra.</summary>
    public string? CompanyCode { get; private set; }

    /// <summary>Đường dẫn thật (API hoặc trang giao diện).</summary>
    public string? Path { get; private set; }

    /// <summary>Phương thức HTTP (lỗi giao diện để trống).</summary>
    public string? Method { get; private set; }

    /// <summary>Nội dung lỗi.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Chi tiết kỹ thuật (ngăn xếp…), tối đa 16 KB.</summary>
    public string? Detail { get; private set; }

    /// <summary>Phiên bản phần mềm.</summary>
    public string? Version { get; private set; }

    /// <summary>Môi trường (Dev / Test / Prod / None).</summary>
    public string? Environment { get; private set; }

    /// <summary>Trình duyệt (User-Agent).</summary>
    public string? UserAgent { get; private set; }

    /// <summary>Tạo một lần xảy ra; chữ dài quá cột thì cắt – nhật ký lỗi không được gây lỗi tiếp.</summary>
    /// <param name="groupId">Nhóm lỗi.</param>
    /// <param name="occurredAt">Thời điểm (UTC).</param>
    /// <param name="trackingCode">Mã theo dõi.</param>
    /// <param name="source">Nguồn.</param>
    /// <param name="userId">Người dùng.</param>
    /// <param name="userName">Tên đăng nhập.</param>
    /// <param name="companyId">Công ty.</param>
    /// <param name="companyCode">Mã công ty.</param>
    /// <param name="path">Đường dẫn.</param>
    /// <param name="method">Phương thức.</param>
    /// <param name="message">Nội dung.</param>
    /// <param name="detail">Chi tiết kỹ thuật.</param>
    /// <param name="version">Phiên bản.</param>
    /// <param name="environment">Môi trường.</param>
    /// <param name="userAgent">Trình duyệt.</param>
    /// <returns>Lần xảy ra.</returns>
    public static ErrorOccurrence Create(
        Guid groupId, DateTimeOffset occurredAt, string trackingCode, ErrorSource source, Guid? userId, string? userName, Guid? companyId,
        string? companyCode, string? path, string? method, string? message, string? detail, string? version, string? environment,
        string? userAgent) =>
        new()
        {
            GroupId = groupId,
            OccurredAt = occurredAt,
            TrackingCode = LogText.Cut(trackingCode, TrackingCodeMaxLength) ?? throw new BusinessRuleException("Lần xảy ra lỗi phải có mã theo dõi."),
            Source = source,
            UserId = userId,
            UserName = LogText.Cut(userName, 256),
            CompanyId = companyId,
            CompanyCode = LogText.Cut(companyCode, 50),
            Path = LogText.Cut(path, ErrorGroup.PathMaxLength),
            Method = LogText.Cut(method, 10),
            Message = LogText.Cut(message, ErrorGroup.TextMaxLength) ?? "(không có nội dung)",
            Detail = LogText.Cut(detail, DetailMaxLength),
            Version = LogText.Cut(version, 50),
            Environment = LogText.Cut(environment, 10),
            UserAgent = LogText.Cut(userAgent, 500),
        };
}

/// <summary>
/// Tính dấu vết gộp nhóm lỗi (lát QT2). Chuẩn hóa để cùng một lỗi ở các lần khác nhau / bản build khác nhau vẫn ra một dấu vết:
/// đường dẫn đổi Id thành {id}, số thành {n}; vị trí bỏ số dòng, cột, mã băm tên file của bản build giao diện.
/// </summary>
public static partial class ErrorFingerprint
{
    /// <summary>
    /// Dấu vết = SHA-256 (hex thường) của "nguồn|loại lỗi|vị trí|đường dẫn", ví dụ Server|System.InvalidOperationException|
    /// at Proc.Infrastructure.X.DoAsync()|/api/po/{id}.
    /// </summary>
    /// <param name="source">Nguồn.</param>
    /// <param name="errorType">Loại lỗi.</param>
    /// <param name="location">Vị trí đã chuẩn hóa.</param>
    /// <param name="normalizedPath">Đường dẫn đã chuẩn hóa.</param>
    /// <returns>64 ký tự hex.</returns>
    public static string Compute(ErrorSource source, string? errorType, string? location, string? normalizedPath)
    {
        var raw = $"{source}|{errorType}|{location}|{normalizedPath}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    /// <summary>
    /// Chuẩn hóa đường dẫn: bỏ phần ?query và #, chữ thường, đoạn là GUID → {id}, đoạn toàn số → {n}. Ví dụ
    /// /api/purchase-orders/0193A…/lines/12?x=1 → /api/purchase-orders/{id}/lines/{n}.
    /// </summary>
    /// <param name="path">Đường dẫn thật.</param>
    /// <returns>Đường dẫn chuẩn hóa; null khi trống.</returns>
    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var p = path.Trim();
        var cut = p.IndexOfAny(['?', '#']);
        if (cut >= 0) p = p[..cut];
        var segments = p.ToLowerInvariant().Split('/')
            .Select(s => Guid.TryParse(s, out _) ? "{id}" : s.Length > 0 && s.All(char.IsAsciiDigit) ? "{n}" : s);
        return LogText.Cut(string.Join('/', segments), ErrorGroup.PathMaxLength);
    }

    /// <summary>
    /// Vị trí đầu ngăn xếp của lỗi máy chủ: dòng "at …" đầu tiên thuộc code của phần mềm (namespace Proc.), không có thì dòng đầu tiên; bỏ
    /// phần " in file:line N" để sửa code chỗ khác (lệch số dòng) không tách nhóm.
    /// </summary>
    /// <param name="stackTrace">Ngăn xếp của exception (lỗi trong cùng).</param>
    /// <returns>Ví dụ "at Proc.Infrastructure.Purchasing.PoService.ApproveAsync(Guid id, CancellationToken ct)"; null khi không có.</returns>
    public static string? ServerLocation(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace)) return null;
        var frames = stackTrace.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("at ", StringComparison.Ordinal)).ToList();
        var frame = frames.FirstOrDefault(f => f.StartsWith("at Proc.", StringComparison.Ordinal)) ?? frames.FirstOrDefault();
        if (frame is null) return null;
        var inFile = frame.IndexOf(" in ", StringComparison.Ordinal);
        return LogText.Cut(inFile > 0 ? frame[..inFile] : frame, ErrorGroup.TextMaxLength);
    }

    /// <summary>
    /// Vị trí đầu ngăn xếp của lỗi giao diện: dòng đầu có tên file (bỏ dòng "TypeError: …"), bỏ :dòng:cột, bỏ máy chủ (http://x:5173), bỏ mã
    /// băm tên file của Vite (index-B3xk9Qa1.js → index.js) và ?v=… – bản build mới vẫn cùng nhóm. Không có ngăn xếp thì dùng nội dung lỗi đã
    /// thay số bằng {n}.
    /// </summary>
    /// <param name="stack">Ngăn xếp trình duyệt gửi về.</param>
    /// <param name="message">Nội dung lỗi.</param>
    /// <returns>Vị trí chuẩn hóa.</returns>
    public static string? ClientLocation(string? stack, string? message)
    {
        var frame = stack?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Contains(".js", StringComparison.Ordinal)
            || l.Contains(".ts", StringComparison.Ordinal));
        if (frame is null)
            return LogText.Cut(message is null ? null : Digits().Replace(message, "{n}"), ErrorGroup.TextMaxLength);
        var f = Origin().Replace(frame, string.Empty);
        f = QueryString().Replace(f, string.Empty);
        f = LineColumn().Replace(f, string.Empty);
        f = ViteHash().Replace(f, "$1$2");
        return LogText.Cut(f, ErrorGroup.TextMaxLength);
    }

    /// <summary>Mã theo dõi mới: "ERR-" + 8 ký tự Crockford base32 (không I, L, O, U – đọc qua điện thoại không nhầm).</summary>
    /// <returns>Ví dụ ERR-7K3F9Q2A.</returns>
    public static string NewTrackingCode()
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[8];
        for (var i = 0; i < 8; i++) chars[i] = alphabet[bytes[i] & 31];
        return "ERR-" + new string(chars);
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    [GeneratedRegex(@"https?://[^/\s)]+")]
    private static partial Regex Origin();

    [GeneratedRegex(@"\?[^:\s)]*")]
    private static partial Regex QueryString();

    [GeneratedRegex(@":\d+(:\d+)?")]
    private static partial Regex LineColumn();

    [GeneratedRegex(@"([A-Za-z0-9_]+)-[A-Za-z0-9_-]{8}(\.js)")]
    private static partial Regex ViteHash();
}

/// <summary>Cắt chữ cho các bảng nhật ký.</summary>
internal static class LogText
{
    /// <summary>Bỏ khoảng trắng hai đầu, chữ rỗng thành null, dài quá thì cắt.</summary>
    /// <param name="value">Chữ.</param>
    /// <param name="max">Độ dài tối đa.</param>
    /// <returns>Chữ đã cắt hoặc null.</returns>
    public static string? Cut(string? value, int max) =>
        value?.Trim() is { Length: > 0 } v ? v[..Math.Min(v.Length, max)] : null;
}
