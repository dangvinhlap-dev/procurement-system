using System.Text.RegularExpressions;
using Proc.Domain.Common;

namespace Proc.Domain.Operations;

/// <summary>Trạng thái phiếu phát hành (design.md mục 21.6).</summary>
public enum ReleaseStatus
{
    /// <summary>Đã nhận gói, chưa cài TEST.</summary>
    Received = 0,

    /// <summary>Đã cài TEST, người dùng đang kiểm thử.</summary>
    UatInProgress = 1,

    /// <summary>Đã xác nhận UAT đạt – được cài PROD.</summary>
    UatPassed = 2,

    /// <summary>Đã cài PROD – phiếu khóa, chỉ thêm ghi chú.</summary>
    ProdInstalled = 3,

    /// <summary>Đã đóng sau khi theo dõi.</summary>
    Closed = 4,

    /// <summary>Đã hủy (chưa cài PROD).</summary>
    Cancelled = 5,

    /// <summary>Đã quay về bản cũ trên PROD.</summary>
    RolledBack = 6,
}

/// <summary>Nguồn gói.</summary>
public enum ReleaseSource
{
    /// <summary>Tải lên từ máy người dùng.</summary>
    Upload = 0,

    /// <summary>Tài sản của GitHub Release.</summary>
    GitHubRelease = 1,
}

/// <summary>Mức quay về bản cũ (design.md mục 21.6a).</summary>
public enum RollbackLevel
{
    /// <summary>Chỉ trả code – bản không đổi CSDL hoặc chỉ thêm.</summary>
    CodeOnly = 1,

    /// <summary>Trả code + chạy script gỡ cấu trúc (mất dữ liệu ở cột / bảng bị gỡ).</summary>
    RemoveSchema = 2,

    /// <summary>Trả code + khôi phục bản sao lưu trước lần cài (mất dữ liệu nhập sau mốc).</summary>
    RestoreDatabase = 3,
}

/// <summary>Kết quả kiểm thử một mục.</summary>
public enum ReleaseTestResult
{
    /// <summary>Chưa kiểm thử.</summary>
    NotTested = 0,

    /// <summary>Đạt.</summary>
    Passed = 1,

    /// <summary>Không đạt.</summary>
    Failed = 2,
}

/// <summary>Trạng thái lỗi phát hiện trong UAT.</summary>
public enum ReleaseDefectStatus
{
    /// <summary>Mới, chưa xử lý.</summary>
    Open = 0,

    /// <summary>Đã sửa (ở bản sau).</summary>
    Fixed = 1,

    /// <summary>Không sửa / không phải lỗi.</summary>
    Rejected = 2,
}

/// <summary>Loại lần cài.</summary>
public enum ReleaseInstallKind
{
    /// <summary>Cài bản của phiếu.</summary>
    Deploy = 0,

    /// <summary>Quay về bản cũ.</summary>
    Rollback = 1,
}

/// <summary>Trạng thái lần cài (theo lệnh bộ thực thi).</summary>
public enum ReleaseInstallStatus
{
    /// <summary>Đã giao lệnh, bộ thực thi chưa báo.</summary>
    Pending = 0,

    /// <summary>Cài xong, /health và kiểm thử nhanh đạt.</summary>
    Succeeded = 1,

    /// <summary>Lỗi – bộ thực thi đã tự trả về bản cũ.</summary>
    Failed = 2,
}

/// <summary>
/// Phiếu phát hành (lát QT8 – design.md mục 21.6, 21.6a): một phiên bản gói đi qua Nhận gói → Cài TEST → UAT đạt → Cài PROD → Đóng. Không áp chuỗi
/// duyệt mục 20 (E3): mỗi bước là một quyền. Từ <see cref="ReleaseStatus.ProdInstalled"/> phiếu khóa – mục kiểm thử, đính kèm không sửa; chỉ thêm
/// ghi chú, lỗi phát hiện, quay về bản cũ, đóng. Thuộc tổng công ty, không thuộc công ty thành viên (bảng vận hành – như <see cref="RestoreRun"/>).
/// </summary>
[Auditable("Phiếu phát hành", EntityType = "ReleaseTicket", LabelProperty = nameof(Version))]
public sealed class ReleaseTicket : AuditableEntity
{
    /// <summary>Độ dài tối đa phiên bản.</summary>
    public const int VersionMaxLength = 32;

    /// <summary>Độ dài tối đa CHANGELOG của bản.</summary>
    public const int ChangelogMaxLength = 20_000;

    /// <summary>Độ dài tối đa các ô chữ dài (thay đổi CSDL, rủi ro, cách quay lại, ghi chú, ý kiến).</summary>
    public const int TextMaxLength = 4000;

    /// <summary>Độ dài tối thiểu lý do (hủy, quay về, xác nhận UAT khi còn mục không đạt).</summary>
    public const int ReasonMinLength = 5;

    /// <summary>SHA256 dạng hex chữ thường.</summary>
    public const int Sha256Length = 64;

    private static readonly Regex VersionPattern = new(@"^\d{1,4}\.\d{1,5}\.\d{1,6}$", RegexOptions.CultureInvariant);
    private static readonly Regex HexPattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    private readonly List<ReleaseTestItem> _items = [];
    private readonly List<ReleaseDefect> _defects = [];
    private readonly List<ReleaseInstall> _installs = [];

    private ReleaseTicket()
    {
    }

    /// <summary>Phiên bản (x.y.z).</summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>Trạng thái.</summary>
    public ReleaseStatus Status { get; private set; }

    /// <summary>Nguồn gói.</summary>
    public ReleaseSource Source { get; private set; }

    /// <summary>Địa chỉ nguồn (trang GitHub Release) – null khi tải lên.</summary>
    public string? SourceUrl { get; private set; }

    /// <summary>Tên file gói.</summary>
    public string PackageFileName { get; private set; } = string.Empty;

    /// <summary>Dung lượng gói (byte).</summary>
    public long PackageSize { get; private set; }

    /// <summary>SHA256 của gói (hex chữ thường) – PROD chỉ cài đúng gói này.</summary>
    public string Sha256 { get; private set; } = string.Empty;

    /// <summary>Mục CHANGELOG của bản.</summary>
    public string? Changelog { get; private set; }

    /// <summary>Thay đổi CSDL (danh sách migration của bản).</summary>
    public string? DatabaseChanges { get; private set; }

    /// <summary>Mức quay về tối thiểu so với bản trước (từ <c>rollback.json</c> của gói).</summary>
    public RollbackLevel MinRollbackLevel { get; private set; } = RollbackLevel.CodeOnly;

    /// <summary>Rủi ro (người nhận gói ghi).</summary>
    public string? Risks { get; private set; }

    /// <summary>Cách quay lại bản trước (mục CHANGELOG / người nhận gói ghi).</summary>
    public string? RollbackPlan { get; private set; }

    /// <summary>Ghi chú (nhập được khi tạo; sau khi khóa vẫn thêm được).</summary>
    public string? Note { get; private set; }

    /// <summary>Người nhận gói.</summary>
    public string? ReceivedByName { get; private set; }

    /// <summary>Lúc nhận gói (UTC).</summary>
    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Người xác nhận UAT đạt.</summary>
    public string? UatSignedByName { get; private set; }

    /// <summary>Lúc xác nhận UAT đạt (UTC).</summary>
    public DateTimeOffset? UatSignedAt { get; private set; }

    /// <summary>Ý kiến khi xác nhận UAT (bắt buộc khi còn mục không đạt / lỗi chưa xử lý).</summary>
    public string? UatComment { get; private set; }

    /// <summary>Xác nhận UAT ở chế độ một môi trường (không có máy TEST).</summary>
    public bool UatWithoutTest { get; private set; }

    /// <summary>Lúc cài PROD xong (UTC).</summary>
    public DateTimeOffset? ProdInstalledAt { get; private set; }

    /// <summary>Lúc đóng / hủy / quay về (UTC).</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Người đóng / hủy / quay về.</summary>
    public string? EndedByName { get; private set; }

    /// <summary>Lý do hủy / quay về.</summary>
    public string? EndReason { get; private set; }

    /// <summary>Mục kiểm thử (mỗi mục CHANGELOG một dòng).</summary>
    public IReadOnlyList<ReleaseTestItem> Items => _items;

    /// <summary>Lỗi phát hiện trong UAT.</summary>
    public IReadOnlyList<ReleaseDefect> Defects => _defects;

    /// <summary>Lịch sử cài / quay về.</summary>
    public IReadOnlyList<ReleaseInstall> Installs => _installs;

    /// <summary>Phiếu đã khóa (từ khi cài PROD, hoặc đã hủy).</summary>
    public bool IsLocked => Status is ReleaseStatus.ProdInstalled or ReleaseStatus.Closed or ReleaseStatus.RolledBack or ReleaseStatus.Cancelled;

    /// <summary>Đang có lần cài chờ bộ thực thi báo – không giao lệnh khác.</summary>
    public bool HasPendingInstall => _installs.Any(i => i.Status == ReleaseInstallStatus.Pending);

    /// <summary>Đã cài TEST thành công ít nhất một lần.</summary>
    public bool InstalledOnTest => _installs.Any(i =>
        i.Kind == ReleaseInstallKind.Deploy && i.Environment == AppEnvironments.Test && i.Status == ReleaseInstallStatus.Succeeded);

    /// <summary>Nhận gói: tạo phiếu Đã nhận và mục kiểm thử từ CHANGELOG.</summary>
    /// <param name="package">Thông tin gói đã kiểm.</param>
    /// <param name="testItems">Mục kiểm thử (nhóm, nội dung) đọc từ CHANGELOG.</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="byName">Người nhận.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Phiếu mới.</returns>
    /// <exception cref="BusinessRuleException">Phiên bản, SHA256, tên file sai.</exception>
    public static ReleaseTicket Receive(
        ReleasePackageInfo package, IEnumerable<(string Section, string Content)> testItems, string? note, string? byName, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(testItems);
        if (!VersionPattern.IsMatch(package.Version ?? string.Empty))
            throw new BusinessRuleException($"Phiên bản \"{package.Version}\" không đúng dạng x.y.z.");
        var sha = package.Sha256?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!HexPattern.IsMatch(sha)) throw new BusinessRuleException("SHA256 của gói không hợp lệ.");
        if (string.IsNullOrWhiteSpace(package.FileName)) throw new BusinessRuleException("Thiếu tên file gói.");
        if (package.Size <= 0) throw new BusinessRuleException("Gói rỗng.");

        var ticket = new ReleaseTicket
        {
            Version = package.Version!,
            Status = ReleaseStatus.Received,
            Source = package.Source,
            SourceUrl = LogText.Cut(package.SourceUrl, 500),
            PackageFileName = LogText.Cut(package.FileName.Trim(), 260)!,
            PackageSize = package.Size,
            Sha256 = sha,
            Changelog = LogText.Cut(package.Changelog, ChangelogMaxLength),
            DatabaseChanges = LogText.Cut(package.DatabaseChanges, TextMaxLength),
            MinRollbackLevel = Enum.IsDefined(package.MinRollbackLevel) ? package.MinRollbackLevel : RollbackLevel.RestoreDatabase,
            RollbackPlan = LogText.Cut(package.RollbackPlan, TextMaxLength),
            Note = Clean(note),
            ReceivedByName = LogText.Cut(byName, 256),
            ReceivedAt = now,
        };
        var seq = 0;
        foreach (var (section, content) in testItems)
        {
            if (string.IsNullOrWhiteSpace(content)) continue;
            ticket._items.Add(ReleaseTestItem.Create(ticket.Id, ++seq, section, content));
        }

        return ticket;
    }

    /// <summary>Sửa phần người nhận gói ghi (rủi ro, cách quay lại, ghi chú) – trước khi khóa.</summary>
    /// <param name="risks">Rủi ro.</param>
    /// <param name="rollbackPlan">Cách quay lại.</param>
    /// <param name="note">Ghi chú.</param>
    /// <exception cref="BusinessRuleException">Phiếu đã khóa.</exception>
    public void UpdateInfo(string? risks, string? rollbackPlan, string? note)
    {
        EnsureNotLocked();
        Risks = Clean(risks);
        RollbackPlan = Clean(rollbackPlan);
        Note = Clean(note);
    }

    /// <summary>Thêm ghi chú (được cả khi phiếu đã khóa – design.md 21.6) – nối vào cuối, kèm người và giờ.</summary>
    /// <param name="text">Nội dung.</param>
    /// <param name="byName">Người ghi.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Nội dung trống, ghi chú quá dài.</exception>
    public void AppendNote(string? text, string? byName, DateTimeOffset now)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) throw new BusinessRuleException("Nhập nội dung ghi chú.");
        var line = $"[{now:yyyy-MM-dd HH:mm} UTC – {byName}] {text}";
        var merged = string.IsNullOrEmpty(Note) ? line : Note + "\n" + line;
        if (merged.Length > TextMaxLength) throw new BusinessRuleException($"Ghi chú dài quá {TextMaxLength} ký tự.");
        Note = merged;
    }

    /// <summary>Ghi kết quả một mục kiểm thử (Đã nhận / Đang UAT).</summary>
    /// <param name="itemId">Mục.</param>
    /// <param name="result">Kết quả.</param>
    /// <param name="comment">Ý kiến (bắt buộc khi Không đạt).</param>
    /// <param name="byName">Người kiểm thử.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Phiếu không ở bước kiểm thử, không có mục, Không đạt thiếu ý kiến.</exception>
    public void RecordTest(Guid itemId, ReleaseTestResult result, string? comment, string? byName, DateTimeOffset now)
    {
        if (Status is not (ReleaseStatus.Received or ReleaseStatus.UatInProgress))
            throw new BusinessRuleException("Chỉ ghi kết quả kiểm thử khi phiếu Đã nhận hoặc Đang UAT.");
        var item = _items.FirstOrDefault(i => i.Id == itemId) ?? throw new BusinessRuleException("Không có mục kiểm thử này trên phiếu.");
        item.Record(result, comment, byName, now);
    }

    /// <summary>Thêm lỗi phát hiện (mọi trạng thái trừ đã hủy – lỗi tìm thấy sau khi cài PROD vẫn ghi để theo dõi).</summary>
    /// <param name="title">Tiêu đề.</param>
    /// <param name="description">Mô tả.</param>
    /// <param name="screen">Màn hình đang mở (nếu có).</param>
    /// <param name="reporterName">Người báo.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Lỗi mới.</returns>
    /// <exception cref="BusinessRuleException">Phiếu đã hủy, thiếu tiêu đề.</exception>
    public ReleaseDefect AddDefect(string? title, string? description, string? screen, string? reporterName, DateTimeOffset now)
    {
        if (Status == ReleaseStatus.Cancelled) throw new BusinessRuleException("Phiếu đã hủy – không thêm lỗi.");
        var defect = ReleaseDefect.Create(Id, title, description, screen, reporterName, now);
        _defects.Add(defect);
        return defect;
    }

    /// <summary>Đổi trạng thái một lỗi.</summary>
    /// <param name="defectId">Lỗi.</param>
    /// <param name="status">Trạng thái mới.</param>
    /// <param name="resolution">Cách xử lý (bắt buộc khi Đã sửa / Không sửa).</param>
    /// <param name="byName">Người xử lý.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Không có lỗi này, thiếu cách xử lý.</exception>
    public void ResolveDefect(Guid defectId, ReleaseDefectStatus status, string? resolution, string? byName, DateTimeOffset now)
    {
        var defect = _defects.FirstOrDefault(d => d.Id == defectId) ?? throw new BusinessRuleException("Không có lỗi này trên phiếu.");
        defect.Resolve(status, resolution, byName, now);
    }

    /// <summary>
    /// Giao lệnh cài lên một môi trường (bước 2 – TEST, bước 4 – PROD). PROD chỉ khi UAT đạt; TEST khi Đã nhận / Đang UAT (cài lại được).
    /// </summary>
    /// <param name="environment">Test hoặc Prod.</param>
    /// <param name="agentCommandId">Lệnh bộ thực thi.</param>
    /// <param name="previousVersion">Phiên bản đang chạy trên môi trường đó (để quay về).</param>
    /// <param name="reason">Lý do.</param>
    /// <param name="byName">Người giao.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Lần cài đang chờ.</returns>
    /// <exception cref="BusinessRuleException">Sai bước, môi trường, đang có lần cài chờ.</exception>
    public ReleaseInstall StartDeploy(
        string environment, Guid agentCommandId, string? previousVersion, string? reason, string? byName, DateTimeOffset now)
    {
        if (HasPendingInstall) throw new BusinessRuleException("Đang có lần cài chưa xong – chờ bộ thực thi báo kết quả.");
        switch (environment)
        {
            case AppEnvironments.Test when Status is ReleaseStatus.Received or ReleaseStatus.UatInProgress:
                break;
            case AppEnvironments.Test:
                throw new BusinessRuleException("Chỉ cài TEST khi phiếu Đã nhận hoặc Đang UAT.");
            case AppEnvironments.Prod when Status == ReleaseStatus.UatPassed:
                break;
            case AppEnvironments.Prod:
                throw new BusinessRuleException("Chỉ cài PROD khi phiếu đã xác nhận UAT đạt.");
            default:
                throw new BusinessRuleException("Chỉ cài lên môi trường TEST hoặc PROD.");
        }

        var install = ReleaseInstall.Create(Id, environment, ReleaseInstallKind.Deploy, agentCommandId, previousVersion, null, reason, byName, now);
        _installs.Add(install);
        return install;
    }

    /// <summary>Giao lệnh quay về bản cũ trên PROD (Đã cài PROD / Đã đóng), mức không thấp hơn mức tối thiểu của gói.</summary>
    /// <param name="level">Mức quay về.</param>
    /// <param name="agentCommandId">Lệnh bộ thực thi.</param>
    /// <param name="targetVersion">Bản quay về.</param>
    /// <param name="reason">Lý do (≥ <see cref="ReasonMinLength"/> ký tự).</param>
    /// <param name="byName">Người giao.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Lần quay về đang chờ.</returns>
    /// <exception cref="BusinessRuleException">Sai bước, mức thấp hơn tối thiểu, thiếu lý do, đang có lần cài chờ.</exception>
    public ReleaseInstall StartRollback(
        RollbackLevel level, Guid agentCommandId, string? targetVersion, string? reason, string? byName, DateTimeOffset now)
    {
        if (Status is not (ReleaseStatus.ProdInstalled or ReleaseStatus.Closed))
            throw new BusinessRuleException("Chỉ quay về bản cũ khi phiếu Đã cài PROD hoặc Đã đóng.");
        if (HasPendingInstall) throw new BusinessRuleException("Đang có lần cài chưa xong – chờ bộ thực thi báo kết quả.");
        if (!Enum.IsDefined(level)) throw new BusinessRuleException("Mức quay về không hợp lệ.");
        if (level < MinRollbackLevel)
            throw new BusinessRuleException($"Bản {Version} chỉ quay về được từ mức {(int)MinRollbackLevel} trở lên (rollback.json của gói).");
        if (string.IsNullOrWhiteSpace(targetVersion)) throw new BusinessRuleException("Không biết bản cũ để quay về.");
        var install = ReleaseInstall.Create(
            Id, AppEnvironments.Prod, ReleaseInstallKind.Rollback, agentCommandId, Version, level, RequireReason(reason, "quay về bản cũ"), byName, now);
        install.SetTargetVersion(targetVersion);
        _installs.Add(install);
        return install;
    }

    /// <summary>
    /// Bộ thực thi báo kết quả một lần cài: cài TEST đạt → Đang UAT; cài PROD đạt (đúng SHA256 của phiếu) → Đã cài PROD; quay về đạt → Đã quay về.
    /// Lỗi thì phiếu giữ trạng thái (bộ thực thi đã tự trả về bản cũ).
    /// </summary>
    /// <param name="installId">Lần cài.</param>
    /// <param name="succeeded">Thành công.</param>
    /// <param name="installedSha256">SHA256 gói bộ thực thi đã cài (kiểm khi cài).</param>
    /// <param name="message">Kết quả.</param>
    /// <param name="byName">Người (bộ thực thi) báo.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Không có lần cài, lần cài đã báo.</exception>
    public void CompleteInstall(Guid installId, bool succeeded, string? installedSha256, string? message, string? byName, DateTimeOffset now)
    {
        var install = _installs.FirstOrDefault(i => i.Id == installId) ?? throw new BusinessRuleException("Không có lần cài này trên phiếu.");
        // Cài thành công nhưng gói khác SHA256 của phiếu (gói bị thay giữa chừng) – coi như lỗi, không chuyển bước.
        if (succeeded && install.Kind == ReleaseInstallKind.Deploy
            && !string.Equals(installedSha256?.Trim(), Sha256, StringComparison.OrdinalIgnoreCase))
        {
            succeeded = false;
            message = $"SHA256 gói đã cài ({installedSha256 ?? "không báo"}) khác gói của phiếu ({Sha256}). {message}";
        }

        install.Complete(succeeded, installedSha256, message, now);
        if (!succeeded) return;
        switch (install.Kind, install.Environment)
        {
            case (ReleaseInstallKind.Deploy, AppEnvironments.Test) when Status == ReleaseStatus.Received:
                Status = ReleaseStatus.UatInProgress;
                break;
            case (ReleaseInstallKind.Deploy, AppEnvironments.Prod):
                Status = ReleaseStatus.ProdInstalled;
                ProdInstalledAt = now;
                break;
            case (ReleaseInstallKind.Rollback, _):
                Status = ReleaseStatus.RolledBack;
                EndedAt = now;
                EndedByName = install.ByName ?? LogText.Cut(byName, 256);
                EndReason = install.Reason;
                break;
        }
    }

    /// <summary>
    /// Xác nhận UAT đạt (bước 3). Thường từ Đang UAT; chế độ một môi trường (không có máy TEST) từ Đã nhận. Còn mục Không đạt / chưa kiểm thử hoặc
    /// lỗi chưa xử lý thì vẫn xác nhận được nhưng bắt buộc ý kiến (lý do) – design.md 21.6.
    /// </summary>
    /// <param name="comment">Ý kiến.</param>
    /// <param name="singleEnvironment">Không có máy TEST (chưa khai môi trường Test).</param>
    /// <param name="byName">Người xác nhận.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Sai bước, thiếu lý do khi còn vướng.</exception>
    public void SignOffUat(string? comment, bool singleEnvironment, string? byName, DateTimeOffset now)
    {
        if (HasPendingInstall) throw new BusinessRuleException("Đang có lần cài chưa xong – chờ bộ thực thi báo kết quả.");
        if (Status == ReleaseStatus.Received && !singleEnvironment)
            throw new BusinessRuleException("Cài TEST trước khi xác nhận UAT (máy có môi trường TEST).");
        if (Status is not (ReleaseStatus.Received or ReleaseStatus.UatInProgress))
            throw new BusinessRuleException("Chỉ xác nhận UAT khi phiếu Đang UAT.");
        comment = Clean(comment);
        var blockers = OpenIssues();
        if (blockers.Count > 0 && (comment is null || comment.Length < ReasonMinLength))
            throw new BusinessRuleException($"Còn {string.Join(", ", blockers)} – ghi lý do vẫn xác nhận UAT đạt (ít nhất {ReasonMinLength} ký tự).");
        Status = ReleaseStatus.UatPassed;
        UatSignedAt = now;
        UatSignedByName = LogText.Cut(byName, 256);
        UatComment = comment;
        UatWithoutTest = !InstalledOnTest;
    }

    /// <summary>Những điều còn vướng trước khi xác nhận UAT (mục không đạt, chưa kiểm thử, lỗi chưa xử lý).</summary>
    /// <returns>Danh sách chữ, rỗng khi không vướng.</returns>
    public IReadOnlyList<string> OpenIssues()
    {
        var list = new List<string>();
        var failed = _items.Count(i => i.Result == ReleaseTestResult.Failed);
        var untested = _items.Count(i => i.Result == ReleaseTestResult.NotTested);
        var open = _defects.Count(d => d.Status == ReleaseDefectStatus.Open);
        if (failed > 0) list.Add($"{failed} mục không đạt");
        if (untested > 0) list.Add($"{untested} mục chưa kiểm thử");
        if (open > 0) list.Add($"{open} lỗi chưa xử lý");
        return list;
    }

    /// <summary>Đóng phiếu (bước 5) sau khi theo dõi.</summary>
    /// <param name="byName">Người đóng.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Phiếu chưa cài PROD.</exception>
    public void Close(string? byName, DateTimeOffset now)
    {
        if (Status != ReleaseStatus.ProdInstalled) throw new BusinessRuleException("Chỉ đóng phiếu Đã cài PROD.");
        if (HasPendingInstall) throw new BusinessRuleException("Đang có lần cài chưa xong – chờ bộ thực thi báo kết quả.");
        Status = ReleaseStatus.Closed;
        EndedAt = now;
        EndedByName = LogText.Cut(byName, 256);
    }

    /// <summary>Hủy phiếu – không hủy được phiếu đã cài PROD; bắt buộc lý do.</summary>
    /// <param name="reason">Lý do.</param>
    /// <param name="byName">Người hủy.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Phiếu đã cài PROD / đã kết thúc, đang có lần cài chờ, thiếu lý do.</exception>
    public void Cancel(string? reason, string? byName, DateTimeOffset now)
    {
        if (Status is not (ReleaseStatus.Received or ReleaseStatus.UatInProgress or ReleaseStatus.UatPassed))
            throw new BusinessRuleException("Không hủy được phiếu đã cài PROD hoặc đã kết thúc.");
        if (HasPendingInstall) throw new BusinessRuleException("Đang có lần cài chưa xong – chờ bộ thực thi báo kết quả.");
        EndReason = RequireReason(reason, "hủy phiếu");
        Status = ReleaseStatus.Cancelled;
        EndedAt = now;
        EndedByName = LogText.Cut(byName, 256);
    }

    /// <summary>Chặn sửa khi phiếu đã khóa.</summary>
    /// <exception cref="BusinessRuleException">Phiếu đã khóa.</exception>
    public void EnsureNotLocked()
    {
        if (IsLocked) throw new BusinessRuleException("Phiếu phát hành đã cài PROD hoặc đã kết thúc – chỉ thêm ghi chú.");
    }

    private static string RequireReason(string? reason, string action)
    {
        reason = reason?.Trim();
        if (reason is null || reason.Length < ReasonMinLength)
            throw new BusinessRuleException($"Nhập lý do {action} (ít nhất {ReasonMinLength} ký tự).");
        return LogText.Cut(reason, TextMaxLength)!;
    }

    private static string? Clean(string? text)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > TextMaxLength) throw new BusinessRuleException($"Nội dung dài quá {TextMaxLength} ký tự.");
        return text;
    }
}

/// <summary>Thông tin gói đã kiểm khi nhận (SHA256, CHANGELOG, rollback.json).</summary>
/// <param name="Version">Phiên bản.</param>
/// <param name="FileName">Tên file gói.</param>
/// <param name="Size">Dung lượng (byte).</param>
/// <param name="Sha256">SHA256 (hex).</param>
/// <param name="Source">Nguồn.</param>
/// <param name="SourceUrl">Địa chỉ nguồn.</param>
/// <param name="Changelog">Mục CHANGELOG của bản.</param>
/// <param name="DatabaseChanges">Thay đổi CSDL.</param>
/// <param name="MinRollbackLevel">Mức quay về tối thiểu.</param>
/// <param name="RollbackPlan">Cách quay lại (mục CHANGELOG).</param>
public sealed record ReleasePackageInfo(
    string? Version, string FileName, long Size, string? Sha256, ReleaseSource Source, string? SourceUrl, string? Changelog,
    string? DatabaseChanges, RollbackLevel MinRollbackLevel, string? RollbackPlan);

/// <summary>Một mục kiểm thử UAT (mỗi dòng CHANGELOG một mục).</summary>
[Auditable("Mục kiểm thử phát hành", EntityType = "ReleaseTestItem", LabelProperty = nameof(Content))]
public sealed class ReleaseTestItem : Entity
{
    /// <summary>Độ dài tối đa nội dung.</summary>
    public const int ContentMaxLength = 2000;

    private ReleaseTestItem()
    {
    }

    /// <summary>Phiếu.</summary>
    public Guid TicketId { get; private set; }

    /// <summary>Thứ tự.</summary>
    public int Seq { get; private set; }

    /// <summary>Nhóm trong CHANGELOG (Thêm mới / Thay đổi / Sửa lỗi…).</summary>
    public string Section { get; private set; } = string.Empty;

    /// <summary>Nội dung cần kiểm thử.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Kết quả.</summary>
    public ReleaseTestResult Result { get; private set; }

    /// <summary>Người kiểm thử.</summary>
    public string? TesterName { get; private set; }

    /// <summary>Lúc ghi kết quả (UTC).</summary>
    public DateTimeOffset? TestedAt { get; private set; }

    /// <summary>Ý kiến.</summary>
    public string? Comment { get; private set; }

    internal static ReleaseTestItem Create(Guid ticketId, int seq, string? section, string content) => new()
    {
        TicketId = ticketId,
        Seq = seq,
        Section = LogText.Cut(string.IsNullOrWhiteSpace(section) ? "Khác" : section.Trim(), 100)!,
        Content = LogText.Cut(content.Trim(), ContentMaxLength)!,
    };

    internal void Record(ReleaseTestResult result, string? comment, string? byName, DateTimeOffset now)
    {
        if (!Enum.IsDefined(result)) throw new BusinessRuleException("Kết quả kiểm thử không hợp lệ.");
        comment = comment?.Trim();
        if (result == ReleaseTestResult.Failed && string.IsNullOrEmpty(comment))
            throw new BusinessRuleException("Mục Không đạt phải ghi ý kiến (lỗi gì, ở màn hình nào).");
        Result = result;
        Comment = LogText.Cut(string.IsNullOrEmpty(comment) ? null : comment, ReleaseTicket.TextMaxLength);
        TesterName = result == ReleaseTestResult.NotTested ? null : LogText.Cut(byName, 256);
        TestedAt = result == ReleaseTestResult.NotTested ? null : now;
    }
}

/// <summary>Lỗi phát hiện trong UAT (QT9: gửi từ nút "Báo lỗi / góp ý" trên TEST).</summary>
[Auditable("Lỗi phát hiện khi phát hành", EntityType = "ReleaseDefect", LabelProperty = nameof(Title))]
public sealed class ReleaseDefect : Entity
{
    /// <summary>Độ dài tối đa tiêu đề.</summary>
    public const int TitleMaxLength = 300;

    private ReleaseDefect()
    {
    }

    /// <summary>Phiếu.</summary>
    public Guid TicketId { get; private set; }

    /// <summary>Tiêu đề.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Mô tả.</summary>
    public string? Description { get; private set; }

    /// <summary>Màn hình đang mở khi gặp lỗi.</summary>
    public string? Screen { get; private set; }

    /// <summary>Người báo.</summary>
    public string? ReporterName { get; private set; }

    /// <summary>Lúc báo (UTC).</summary>
    public DateTimeOffset ReportedAt { get; private set; }

    /// <summary>Trạng thái.</summary>
    public ReleaseDefectStatus Status { get; private set; }

    /// <summary>Cách xử lý.</summary>
    public string? Resolution { get; private set; }

    /// <summary>Người xử lý.</summary>
    public string? ResolvedByName { get; private set; }

    /// <summary>Lúc xử lý (UTC).</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    internal static ReleaseDefect Create(Guid ticketId, string? title, string? description, string? screen, string? reporterName, DateTimeOffset now)
    {
        title = title?.Trim();
        if (string.IsNullOrEmpty(title)) throw new BusinessRuleException("Nhập tiêu đề lỗi.");
        return new ReleaseDefect
        {
            TicketId = ticketId,
            Title = LogText.Cut(title, TitleMaxLength)!,
            Description = LogText.Cut(string.IsNullOrWhiteSpace(description) ? null : description.Trim(), ReleaseTicket.TextMaxLength),
            Screen = LogText.Cut(string.IsNullOrWhiteSpace(screen) ? null : screen.Trim(), 200),
            ReporterName = LogText.Cut(reporterName, 256),
            ReportedAt = now,
            Status = ReleaseDefectStatus.Open,
        };
    }

    internal void Resolve(ReleaseDefectStatus status, string? resolution, string? byName, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status)) throw new BusinessRuleException("Trạng thái lỗi không hợp lệ.");
        resolution = resolution?.Trim();
        if (status != ReleaseDefectStatus.Open && string.IsNullOrEmpty(resolution))
            throw new BusinessRuleException("Ghi cách xử lý lỗi.");
        Status = status;
        Resolution = LogText.Cut(string.IsNullOrEmpty(resolution) ? null : resolution, ReleaseTicket.TextMaxLength);
        ResolvedByName = status == ReleaseDefectStatus.Open ? null : LogText.Cut(byName, 256);
        ResolvedAt = status == ReleaseDefectStatus.Open ? null : now;
    }
}

/// <summary>Một lần cài / quay về (lịch sử cài của phiếu) – gắn lệnh bộ thực thi.</summary>
public sealed class ReleaseInstall : Entity
{
    private ReleaseInstall()
    {
    }

    /// <summary>Phiếu.</summary>
    public Guid TicketId { get; private set; }

    /// <summary>Môi trường (Test / Prod).</summary>
    public string Environment { get; private set; } = string.Empty;

    /// <summary>Cài hay quay về.</summary>
    public ReleaseInstallKind Kind { get; private set; }

    /// <summary>Lệnh bộ thực thi.</summary>
    public Guid AgentCommandId { get; private set; }

    /// <summary>Phiên bản đang chạy trước lần này.</summary>
    public string? PreviousVersion { get; private set; }

    /// <summary>Bản quay về (khi quay về).</summary>
    public string? TargetVersion { get; private set; }

    /// <summary>Mức quay về (khi quay về).</summary>
    public RollbackLevel? Level { get; private set; }

    /// <summary>Trạng thái.</summary>
    public ReleaseInstallStatus Status { get; private set; }

    /// <summary>SHA256 gói bộ thực thi báo đã cài.</summary>
    public string? InstalledSha256 { get; private set; }

    /// <summary>Lý do.</summary>
    public string? Reason { get; private set; }

    /// <summary>Người giao lệnh.</summary>
    public string? ByName { get; private set; }

    /// <summary>Lúc giao (UTC).</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Lúc xong (UTC).</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Kết quả.</summary>
    public string? Message { get; private set; }

    internal static ReleaseInstall Create(
        Guid ticketId, string environment, ReleaseInstallKind kind, Guid agentCommandId, string? previousVersion, RollbackLevel? level,
        string? reason, string? byName, DateTimeOffset now) => new()
    {
        TicketId = ticketId,
        Environment = environment,
        Kind = kind,
        AgentCommandId = agentCommandId,
        PreviousVersion = LogText.Cut(previousVersion, ReleaseTicket.VersionMaxLength),
        Level = level,
        Status = ReleaseInstallStatus.Pending,
        Reason = LogText.Cut(string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), ReleaseTicket.TextMaxLength),
        ByName = LogText.Cut(byName, 256),
        StartedAt = now,
    };

    internal void SetTargetVersion(string version) => TargetVersion = LogText.Cut(version.Trim(), ReleaseTicket.VersionMaxLength);

    internal void Complete(bool succeeded, string? installedSha256, string? message, DateTimeOffset now)
    {
        if (Status != ReleaseInstallStatus.Pending) throw new BusinessRuleException("Lần cài này đã có kết quả.");
        Status = succeeded ? ReleaseInstallStatus.Succeeded : ReleaseInstallStatus.Failed;
        InstalledSha256 = LogText.Cut(installedSha256?.Trim().ToLowerInvariant(), ReleaseTicket.Sha256Length);
        Message = LogText.Cut(string.IsNullOrWhiteSpace(message) ? (succeeded ? "Xong." : "Lỗi không rõ.") : message.Trim(), ReleaseTicket.TextMaxLength);
        FinishedAt = now;
    }
}
