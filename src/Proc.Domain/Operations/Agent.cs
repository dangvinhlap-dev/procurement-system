using Proc.Domain.Common;

namespace Proc.Domain.Operations;

/// <summary>
/// Chế độ bảo trì của máy chủ (lát QT5, ke-hoach-qt.md QT5): bật thì mọi request của người dùng thường nhận 503 kèm câu thông báo, chỉ người có quyền
/// quản trị vận hành (<c>System.Ops*</c>) vào được – dùng khi cài bản mới, khôi phục, hoán đổi CSDL (N2). Bảng chỉ có <b>một dòng</b>
/// (<see cref="SingletonId"/>); lịch sử bật / tắt nằm ở nhật ký vận hành.
/// </summary>
public sealed class MaintenanceModeState : Entity
{
    /// <summary>Id cố định của dòng duy nhất.</summary>
    public static readonly Guid SingletonId = new("0195f0a0-0000-7000-8000-00000000b7a1");

    /// <summary>Độ dài tối đa câu thông báo.</summary>
    public const int MessageMaxLength = 500;

    /// <summary>Độ dài tối đa lý do.</summary>
    public const int ReasonMaxLength = 500;

    /// <summary>Độ dài tối thiểu lý do – bật / tắt chặn mọi người dùng nên phải ghi rõ vì sao (như Q6).</summary>
    public const int ReasonMinLength = 5;

    /// <summary>Câu thông báo mặc định khi người bật không ghi câu riêng.</summary>
    public const string DefaultMessage = "Phần mềm đang bảo trì. Vui lòng quay lại sau ít phút.";

    private MaintenanceModeState()
    {
    }

    /// <summary>Đang bật.</summary>
    public bool IsOn { get; private set; }

    /// <summary>Câu thông báo người dùng thấy (dải đỏ, câu báo 503).</summary>
    public string? Message { get; private set; }

    /// <summary>Lý do lần bật / tắt gần nhất.</summary>
    public string? Reason { get; private set; }

    /// <summary>Lúc bật (UTC); null khi đang tắt.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>Tên đăng nhập người bật / tắt gần nhất (bộ thực thi: "agent:&lt;môi trường&gt;").</summary>
    public string? ChangedByName { get; private set; }

    /// <summary>Lúc bật / tắt gần nhất (UTC).</summary>
    public DateTimeOffset? ChangedAt { get; private set; }

    /// <summary>Dòng ban đầu (đang tắt) – tạo khi chưa có.</summary>
    /// <returns>Trạng thái tắt.</returns>
    public static MaintenanceModeState CreateDefault() => new() { Id = SingletonId };

    /// <summary>Bật chế độ bảo trì (đang bật thì cập nhật câu thông báo, giữ giờ bật).</summary>
    /// <param name="message">Câu thông báo; trống = <see cref="DefaultMessage"/>.</param>
    /// <param name="reason">Lý do (≥ <see cref="ReasonMinLength"/> ký tự).</param>
    /// <param name="byName">Người bật.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Thiếu lý do.</exception>
    public void TurnOn(string? message, string? reason, string? byName, DateTimeOffset now)
    {
        Reason = RequireReason(reason, "bật");
        Message = LogText.Cut(string.IsNullOrWhiteSpace(message) ? DefaultMessage : message.Trim(), MessageMaxLength);
        StartedAt = IsOn ? StartedAt : now;
        IsOn = true;
        ChangedByName = LogText.Cut(byName, 256);
        ChangedAt = now;
    }

    /// <summary>Tắt chế độ bảo trì.</summary>
    /// <param name="reason">Lý do (≥ <see cref="ReasonMinLength"/> ký tự).</param>
    /// <param name="byName">Người tắt.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Đang tắt, thiếu lý do.</exception>
    public void TurnOff(string? reason, string? byName, DateTimeOffset now)
    {
        if (!IsOn) throw new BusinessRuleException("Chế độ bảo trì đang tắt.");
        Reason = RequireReason(reason, "tắt");
        IsOn = false;
        StartedAt = null;
        ChangedByName = LogText.Cut(byName, 256);
        ChangedAt = now;
    }

    private static string RequireReason(string? reason, string verb)
    {
        reason = reason?.Trim();
        if (reason is null || reason.Length < ReasonMinLength)
            throw new BusinessRuleException($"Nhập lý do {verb} chế độ bảo trì (ít nhất {ReasonMinLength} ký tự).");
        return LogText.Cut(reason, ReasonMaxLength)!;
    }
}

/// <summary>
/// Một môi trường có bộ thực thi (lát QT5 – design.md mục 21.3, 21.8): DEV / TEST / PROD. Bộ thực thi trên máy đó chủ động hỏi máy chủ lấy lệnh
/// (không mở cổng vào máy), xác thực bằng khóa riêng gửi trong header <c>X-Ops-Agent-Key</c>; CSDL chỉ lưu <b>băm SHA256</b> của khóa (khóa ngẫu
/// nhiên 256 bit nên băm một lần là đủ – không phải mật khẩu người gõ). Khóa chỉ hiện một lần lúc tạo.
/// </summary>
public sealed class AgentEnvironment : Entity
{
    /// <summary>Độ dài tối đa tên.</summary>
    public const int NameMaxLength = 100;

    /// <summary>Độ dài tối đa địa chỉ.</summary>
    public const int AddressMaxLength = 300;

    /// <summary>Độ dài băm khóa (SHA256 dạng hex).</summary>
    public const int KeyHashLength = 64;

    /// <summary>Độ dài tối đa phiên bản, tên máy bộ thực thi báo về.</summary>
    public const int InfoMaxLength = 100;

    private AgentEnvironment()
    {
    }

    /// <summary>Mã môi trường: Dev / Test / Prod (<see cref="AppEnvironments"/>) – mỗi mã một dòng.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên hiển thị, ví dụ "Máy TEST – phòng máy tầng 3".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Địa chỉ web của môi trường (ví dụ https://test.congty.local) – để người quản trị mở nhanh; QT8 đọc trạng thái qua địa chỉ này.</summary>
    public string? Address { get; private set; }

    /// <summary>Đang dùng – ngừng thì khóa bộ thực thi bị từ chối.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Băm SHA256 (hex chữ thường) của khóa bộ thực thi; null = chưa tạo khóa.</summary>
    public string? AgentKeyHash { get; private set; }

    /// <summary>4 ký tự cuối của khóa – để đối chiếu với file cấu hình trên máy mà không lộ khóa.</summary>
    public string? AgentKeyHint { get; private set; }

    /// <summary>Lúc tạo khóa (UTC).</summary>
    public DateTimeOffset? KeyCreatedAt { get; private set; }

    /// <summary>Người tạo khóa.</summary>
    public string? KeyCreatedByName { get; private set; }

    /// <summary>Lần bộ thực thi liên lạc gần nhất (UTC).</summary>
    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>Phiên bản script bộ thực thi báo về.</summary>
    public string? AgentVersion { get; private set; }

    /// <summary>Phiên bản phần mềm đang cài trên máy đó (bộ thực thi đọc từ file Proc.Api.dll).</summary>
    public string? AppVersion { get; private set; }

    /// <summary>Tên máy chạy bộ thực thi.</summary>
    public string? HostName { get; private set; }

    /// <summary>Khai môi trường mới.</summary>
    /// <param name="code">Dev / Test / Prod.</param>
    /// <param name="name">Tên hiển thị.</param>
    /// <param name="address">Địa chỉ web (không bắt buộc, phải là http/https).</param>
    /// <returns>Môi trường chưa có khóa.</returns>
    /// <exception cref="BusinessRuleException">Mã sai, thiếu tên, địa chỉ sai.</exception>
    public static AgentEnvironment Register(string? code, string? name, string? address)
    {
        var env = new AgentEnvironment
        {
            Code = AppEnvironments.Normalize(code) ?? throw new BusinessRuleException("Môi trường phải là Dev, Test hoặc Prod."),
        };
        env.Update(name, address, true);
        return env;
    }

    /// <summary>Sửa tên, địa chỉ, ngừng / dùng lại.</summary>
    /// <param name="name">Tên hiển thị.</param>
    /// <param name="address">Địa chỉ web.</param>
    /// <param name="isActive">Đang dùng.</param>
    /// <exception cref="BusinessRuleException">Thiếu tên, địa chỉ không phải http/https.</exception>
    public void Update(string? name, string? address, bool isActive)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new BusinessRuleException("Nhập tên môi trường.");
        if (name.Length > NameMaxLength) throw new BusinessRuleException($"Tên môi trường tối đa {NameMaxLength} ký tự.");
        address = string.IsNullOrWhiteSpace(address) ? null : address.Trim().TrimEnd('/');
        if (address is not null && (address.Length > AddressMaxLength
                || !Uri.TryCreate(address, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            throw new BusinessRuleException("Địa chỉ phải dạng http(s)://tên-máy[:cổng].");
        Name = name;
        Address = address;
        IsActive = isActive;
    }

    /// <summary>Gắn khóa mới (khóa cũ hết hiệu lực ngay).</summary>
    /// <param name="keyHash">Băm SHA256 hex của khóa.</param>
    /// <param name="hint">4 ký tự cuối của khóa.</param>
    /// <param name="byName">Người tạo.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Băm sai độ dài; môi trường ngừng dùng.</exception>
    public void SetKey(string keyHash, string hint, string? byName, DateTimeOffset now)
    {
        if (!IsActive) throw new BusinessRuleException("Môi trường đang ngừng dùng – bật lại trước khi tạo khóa.");
        if (keyHash is not { Length: KeyHashLength }) throw new BusinessRuleException("Băm khóa không hợp lệ.");
        AgentKeyHash = keyHash;
        AgentKeyHint = LogText.Cut(hint, 8);
        KeyCreatedAt = now;
        KeyCreatedByName = LogText.Cut(byName, 256);
    }

    /// <summary>Bộ thực thi vừa liên lạc: ghi giờ, phiên bản, tên máy (bỏ trống thì giữ giá trị cũ).</summary>
    /// <param name="agentVersion">Phiên bản bộ thực thi.</param>
    /// <param name="appVersion">Phiên bản phần mềm trên máy.</param>
    /// <param name="hostName">Tên máy.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    public void RecordContact(string? agentVersion, string? appVersion, string? hostName, DateTimeOffset now)
    {
        LastSeenAt = now;
        AgentVersion = Clean(agentVersion) ?? AgentVersion;
        AppVersion = Clean(appVersion) ?? AppVersion;
        HostName = Clean(hostName) ?? HostName;

        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : LogText.Cut(value.Trim(), InfoMaxLength);
    }
}

/// <summary>Loại lệnh cho bộ thực thi. Các lát sau thêm (QT6 khôi phục / hoán đổi, QT8 cài bản, QT9 làm mới TEST) – chỉ thêm, không đổi số.</summary>
public enum AgentCommandKind
{
    /// <summary>"Lệnh thử": bộ thực thi báo phiên bản, tên máy, tài khoản chạy – không thay đổi gì trên máy (design.md 21.8 GĐ2).</summary>
    Ping = 0,
}

/// <summary>Trạng thái lệnh bộ thực thi.</summary>
public enum AgentCommandStatus
{
    /// <summary>Chờ bộ thực thi lấy.</summary>
    Queued = 0,

    /// <summary>Bộ thực thi đã lấy, đang chạy.</summary>
    Running = 1,

    /// <summary>Xong.</summary>
    Succeeded = 2,

    /// <summary>Lỗi (bộ thực thi báo lỗi, hoặc chạy quá giờ không báo kết quả).</summary>
    Failed = 3,

    /// <summary>Hết hạn – quá 1 giờ bộ thực thi chưa lấy (máy tắt, mất mạng): không chạy lệnh cũ bất ngờ khi máy bật lại.</summary>
    Expired = 4,

    /// <summary>Người quản trị hủy khi còn chờ.</summary>
    Cancelled = 5,
}

/// <summary>
/// Một lệnh giao bộ thực thi (lát QT5, N1): web chỉ ghi lệnh, bộ thực thi trên máy đích lấy lệnh, chạy với quyền cao rồi báo kết quả. Quy tắc:
/// lệnh <b>hết hạn</b> sau <see cref="DefaultLifetime"/> nếu chưa được lấy; mỗi môi trường <b>chỉ một lệnh đang chạy</b> (dịch vụ kiểm); chỉ lệnh
/// đang chạy mới nhận kết quả. Dòng không xóa – là lịch sử ai ra lệnh gì, kết quả ra sao.
/// </summary>
public sealed class AgentCommand : Entity
{
    /// <summary>Thời gian chờ bộ thực thi lấy lệnh trước khi hết hạn.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);

    /// <summary>Độ dài tối đa tham số (JSON).</summary>
    public const int ParametersMaxLength = 4000;

    /// <summary>Độ dài tối đa lý do.</summary>
    public const int ReasonMaxLength = 500;

    /// <summary>Độ dài tối thiểu lý do (lệnh chạy quyền cao trên máy chủ – ghi rõ vì sao).</summary>
    public const int ReasonMinLength = 5;

    /// <summary>Độ dài tối đa kết quả.</summary>
    public const int MessageMaxLength = 4000;

    /// <summary>Độ dài tối đa nhật ký bộ thực thi gửi về (giữ phần cuối – lỗi thường ở cuối).</summary>
    public const int JournalMaxLength = 64_000;

    private AgentCommand()
    {
    }

    /// <summary>Môi trường nhận lệnh.</summary>
    public Guid EnvironmentId { get; private set; }

    /// <summary>Loại lệnh.</summary>
    public AgentCommandKind Kind { get; private set; }

    /// <summary>Tham số (JSON) – Ping không có.</summary>
    public string? Parameters { get; private set; }

    /// <summary>Trạng thái.</summary>
    public AgentCommandStatus Status { get; private set; }

    /// <summary>Lý do ra lệnh.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Người ra lệnh.</summary>
    public string? CreatedByName { get; private set; }

    /// <summary>Lúc ra lệnh (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Hạn lấy lệnh (UTC).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Lúc bộ thực thi lấy lệnh (UTC).</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>Lúc kết thúc (UTC).</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Kết quả / lỗi tóm tắt.</summary>
    public string? ResultMessage { get; private set; }

    /// <summary>Nhật ký chi tiết bộ thực thi gửi về (cắt giữ phần cuối <see cref="JournalMaxLength"/> ký tự).</summary>
    public string? Journal { get; private set; }

    /// <summary>Người hủy (khi Đã hủy).</summary>
    public string? CancelledByName { get; private set; }

    /// <summary>Ra lệnh mới (trạng thái Chờ).</summary>
    /// <param name="environmentId">Môi trường.</param>
    /// <param name="kind">Loại lệnh.</param>
    /// <param name="parameters">Tham số JSON.</param>
    /// <param name="reason">Lý do (≥ <see cref="ReasonMinLength"/> ký tự).</param>
    /// <param name="createdByName">Người ra lệnh.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Lệnh chờ, hạn lấy = now + <see cref="DefaultLifetime"/>.</returns>
    /// <exception cref="BusinessRuleException">Loại lệnh sai, thiếu lý do, tham số quá dài.</exception>
    public static AgentCommand Create(
        Guid environmentId, AgentCommandKind kind, string? parameters, string? reason, string? createdByName, DateTimeOffset now)
    {
        if (!Enum.IsDefined(kind)) throw new BusinessRuleException("Loại lệnh bộ thực thi không hợp lệ.");
        reason = reason?.Trim();
        if (reason is null || reason.Length < ReasonMinLength)
            throw new BusinessRuleException($"Nhập lý do ra lệnh (ít nhất {ReasonMinLength} ký tự).");
        parameters = string.IsNullOrWhiteSpace(parameters) ? null : parameters;
        if (parameters?.Length > ParametersMaxLength) throw new BusinessRuleException("Tham số lệnh quá dài.");
        return new AgentCommand
        {
            EnvironmentId = environmentId,
            Kind = kind,
            Parameters = parameters,
            Status = AgentCommandStatus.Queued,
            Reason = LogText.Cut(reason, ReasonMaxLength)!,
            CreatedByName = LogText.Cut(createdByName, 256),
            CreatedAt = now,
            ExpiresAt = now + DefaultLifetime,
        };
    }

    /// <summary>Lệnh chờ đã quá hạn lấy.</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>true khi còn Chờ và now ≥ hạn.</returns>
    public bool IsExpired(DateTimeOffset now) => Status == AgentCommandStatus.Queued && now >= ExpiresAt;

    /// <summary>Bộ thực thi lấy lệnh: Chờ → Đang chạy.</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Không ở trạng thái Chờ, hoặc đã quá hạn.</exception>
    public void Claim(DateTimeOffset now)
    {
        if (Status != AgentCommandStatus.Queued) throw new BusinessRuleException("Chỉ lệnh đang chờ mới lấy được.");
        if (now >= ExpiresAt) throw new BusinessRuleException("Lệnh đã hết hạn.");
        Status = AgentCommandStatus.Running;
        StartedAt = now;
    }

    /// <summary>Đánh dấu hết hạn (lệnh chờ quá hạn).</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Không phải lệnh chờ đã quá hạn.</exception>
    public void Expire(DateTimeOffset now)
    {
        if (!IsExpired(now)) throw new BusinessRuleException("Chỉ lệnh chờ đã quá hạn mới đánh dấu hết hạn.");
        Status = AgentCommandStatus.Expired;
        ResultMessage = "Bộ thực thi không lấy lệnh trong hạn – lệnh không chạy.";
        FinishedAt = now;
    }

    /// <summary>Bộ thực thi báo kết quả: Đang chạy → Xong / Lỗi.</summary>
    /// <param name="succeeded">Chạy được.</param>
    /// <param name="message">Kết quả / lỗi tóm tắt.</param>
    /// <param name="journal">Nhật ký chi tiết.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Lệnh không đang chạy (đã báo rồi, đã hủy, hết hạn).</exception>
    public void Report(bool succeeded, string? message, string? journal, DateTimeOffset now)
    {
        if (Status != AgentCommandStatus.Running) throw new BusinessRuleException("Chỉ lệnh đang chạy mới nhận kết quả.");
        Status = succeeded ? AgentCommandStatus.Succeeded : AgentCommandStatus.Failed;
        ResultMessage = LogText.Cut(string.IsNullOrWhiteSpace(message) ? (succeeded ? "Xong." : "Lỗi không rõ.") : message.Trim(), MessageMaxLength);
        Journal = KeepTail(journal);
        FinishedAt = now;
    }

    /// <summary>Lệnh đang chạy quá lâu không báo kết quả (bộ thực thi chết giữa chừng) → Lỗi, để môi trường nhận được lệnh mới.</summary>
    /// <param name="message">Lý do.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Lệnh không đang chạy.</exception>
    public void FailStale(string message, DateTimeOffset now)
    {
        if (Status != AgentCommandStatus.Running) throw new BusinessRuleException("Chỉ lệnh đang chạy mới ghi lỗi quá giờ.");
        Status = AgentCommandStatus.Failed;
        ResultMessage = LogText.Cut(message, MessageMaxLength);
        FinishedAt = now;
    }

    /// <summary>Hủy lệnh còn chờ.</summary>
    /// <param name="byName">Người hủy.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Lệnh không còn chờ (đã chạy thì không hủy được – bộ thực thi đang làm trên máy).</exception>
    public void Cancel(string? byName, DateTimeOffset now)
    {
        if (Status != AgentCommandStatus.Queued) throw new BusinessRuleException("Chỉ hủy được lệnh đang chờ.");
        Status = AgentCommandStatus.Cancelled;
        CancelledByName = LogText.Cut(byName, 256);
        ResultMessage = "Đã hủy trước khi bộ thực thi lấy lệnh.";
        FinishedAt = now;
    }

    /// <summary>Giữ phần cuối nhật ký (lỗi thường ở cuối), thêm dấu "…" ở đầu khi đã cắt.</summary>
    private static string? KeepTail(string? journal)
    {
        if (string.IsNullOrEmpty(journal)) return null;
        return journal.Length <= JournalMaxLength ? journal : "…" + journal[^(JournalMaxLength - 1)..];
    }
}
