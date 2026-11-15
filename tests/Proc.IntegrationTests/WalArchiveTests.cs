using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT3a – tình trạng lưu WAL (design.md mục 21.5, ke-hoach-qt.md QT3a): API màn hình BAK đọc đúng tham số thật của PostgreSQL đang chạy
/// test, quyền <c>System.OpsBackup.View</c> chỉ ở tổng công ty, dòng "Lưu WAL" trên SYS; bộ đánh giá trạng thái (hàm thuần) cho từng trường hợp
/// mà máy test không dựng được (đang chạy, trễ, lỗi, chờ khởi động lại).
/// </summary>
[Collection(ApiCollection.Name)]
public class WalArchiveTests
{
    private const string WalStatusPath = "/api/ops/v1/backup/wal-status";

    private readonly ProcApiFactory _factory;

    public WalArchiveTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>
    /// Đọc thật trên máy CSDL test: trạng thái khớp <c>archive_mode</c> / <c>wal_level</c> của máy (máy dev chưa bật thì "Chưa bật", đèn trung
    /// tính vì môi trường Dev); tài khoản ứng dụng không xem được <c>archive_command</c> nếu không phải quản trị CSDL; SYS có dòng "Lưu WAL".
    /// </summary>
    [Fact]
    public async Task DocTrangThaiThat_KhopThamSoMayCsdl_SysCoDongLuuWal()
    {
        string mode, level;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            mode = await db.Database.SqlQueryRaw<string>("SELECT current_setting('archive_mode') AS \"Value\"").SingleAsync(Ct);
            level = await db.Database.SqlQueryRaw<string>("SELECT current_setting('wal_level') AS \"Value\"").SingleAsync(Ct);
        }

        var head = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var wal = await ReadAsync<WalArchiveStatusDto>(await head.GetAsync(WalStatusPath, Ct));

        var enabled = mode is "on" or "always" && level is "replica" or "logical";
        if (enabled) Assert.NotEqual(WalArchiveStates.NotEnabled, wal.State);
        else
        {
            Assert.Contains(wal.State, new[] { WalArchiveStates.NotEnabled, WalArchiveStates.PendingRestart });
            if (wal.State == WalArchiveStates.NotEnabled) Assert.Equal("Neutral", wal.Status);
        }

        Assert.Equal(WalArchiveEvaluator.Label(wal.State), wal.StateLabel);
        Assert.Contains(wal.Rows, r => r.Item == "archive_mode" && r.Value == mode);
        Assert.Contains(wal.Rows, r => r.Item == "wal_level" && r.Value == level);
        Assert.Contains(wal.Rows, r => r.Item.StartsWith("Thư mục WAL", StringComparison.Ordinal));
        Assert.Contains(wal.Rows, r => r.Item.StartsWith("Tài khoản bảo trì", StringComparison.Ordinal) && r.Value == "Chưa khai");

        var overview = await ReadAsync<SystemOverviewDto>(await head.GetAsync("/api/ops/v1/overview", Ct));
        var row = Assert.Single(overview.Rows, r => r.Item == "Lưu WAL");
        Assert.StartsWith(wal.StateLabel, row.Value, StringComparison.Ordinal);
    }

    /// <summary>Công ty thành viên 403 (kể cả quản trị); người chỉ có System.Ops.View 403; người có System.OpsBackup.View xem được.</summary>
    [Fact]
    public async Task Quyen_ChiTongCongTy_CanQuyenXemSaoLuu()
    {
        var member = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync(WalStatusPath, Ct)).StatusCode);

        var opsRole = $"W{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(opsRole, Permissions.System.OpsView);
        var opsName = $"wo.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(opsName, "TCT", opsRole);
        var ops = await _factory.CreateClientInCompanyAsync(opsName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.Forbidden, (await ops.GetAsync(WalStatusPath, Ct)).StatusCode);

        var backupRole = $"V{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(backupRole, Permissions.System.OpsBackupView);
        var viewerName = $"wv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "TCT", backupRole);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(WalStatusPath, Ct)).StatusCode);
    }

    /// <summary>Số thứ tự file WAL (16 MB: 256 file mỗi "log"), file chờ chép, tên lạ.</summary>
    [Fact]
    public void SoThuTuFileWal_VaFileChoChep()
    {
        const long size16 = 16L * 1024 * 1024;
        Assert.Equal(2 * 256 + 3, WalArchiveEvaluator.SegmentNumber("000000010000000200000003", size16));
        Assert.Equal(2 * 256 + 3, WalArchiveEvaluator.SegmentNumber("000000010000000200000003.partial", size16));
        Assert.Equal(1 * 4096 + 0x10, WalArchiveEvaluator.SegmentNumber("000000020000000100000010", 1024 * 1024)); // 1 MB: 4096 file mỗi log
        Assert.Null(WalArchiveEvaluator.SegmentNumber("00000002.history", size16));
        Assert.Null(WalArchiveEvaluator.SegmentNumber("00000001000000020000000X", size16));
        // Đang ghi 0x2FF (log 2, 255) → tiếp theo log 3, 0: chép cuối 0x2FE → chờ 0 file; đang ghi log 3 file 1 → chờ 1 file (log 3 file 0).
        Assert.Equal(0, WalArchiveEvaluator.PendingSegments("0000000100000002000000FF", "0000000100000002000000FE", size16));
        Assert.Equal(1, WalArchiveEvaluator.PendingSegments("000000010000000300000001", "0000000100000002000000FF", size16));
        Assert.Null(WalArchiveEvaluator.PendingSegments(null, "0000000100000002000000FF", size16));
    }

    /// <summary>Chưa bật: TEST / PROD đèn đỏ, DEV trung tính; đã đổi trong file mà chưa khởi động lại thì "Chờ khởi động lại" đèn vàng.</summary>
    [Fact]
    public void ChuaBat_VaChoKhoiDongLai()
    {
        var off = Settings(mode: "off");
        Assert.Equal(("NotEnabled", "Error"), StateOf(off, Stats(), env: AppEnvironments.Prod));
        Assert.Equal(("NotEnabled", "Neutral"), StateOf(off, Stats(), env: AppEnvironments.Dev));
        Assert.Equal(("NotEnabled", "Error"), StateOf(Settings(level: "minimal"), Stats(), env: AppEnvironments.Test));
        // archive_mode tắt thì PostgreSQL hiện "(disabled)" thay lệnh – không phải lỗi của lệnh.
        Assert.Equal(("(disabled)", "Neutral"), Pick(Evaluate(Settings(mode: "off", command: "(disabled)"), Stats(), AppEnvironments.Prod), "archive_command"));

        var pending = Settings(mode: "off", modePending: true);
        var result = Evaluate(pending, Stats(), AppEnvironments.Prod);
        Assert.Equal(WalArchiveStates.PendingRestart, result.State);
        Assert.Equal("Warning", result.Status);
        Assert.Contains("chờ khởi động lại", Assert.Single(result.Rows, r => r.Item == "archive_mode").Note, StringComparison.Ordinal);
        Assert.Null(result.LagSeconds);
    }

    /// <summary>
    /// Đang chạy: chép cuối 2 phút trước, chỉ còn file đang ghi → đèn xanh, trễ 0. Còn file đầy chờ nhưng lần chép cuối mới 10 phút → vẫn chạy
    /// (đang đuổi kịp); quá 15 phút → Trễ (đèn đỏ, độ trễ = giây từ lần chép cuối). Đã bật mà chưa chép file nào → chạy, đèn vàng.
    /// </summary>
    [Fact]
    public void DangChay_VaTre()
    {
        var on = Settings();
        var ok = Evaluate(on, Stats(last: "000000010000000000000010", lastAt: Now.AddMinutes(-2)), AppEnvironments.Prod, current: "000000010000000000000011");
        Assert.Equal((WalArchiveStates.Running, "Ok", 0L), (ok.State, ok.Status, ok.LagSeconds!.Value));

        var catching = Evaluate(on, Stats(last: "000000010000000000000010", lastAt: Now.AddMinutes(-10)), AppEnvironments.Prod,
            current: "000000010000000000000014");
        Assert.Equal(WalArchiveStates.Running, catching.State);
        Assert.Equal(600, catching.LagSeconds);
        Assert.Equal("Warning", Assert.Single(catching.Rows, r => r.Item == "File WAL chờ chép").Status);

        var late = Evaluate(on, Stats(last: "000000010000000000000010", lastAt: Now.AddMinutes(-20)), AppEnvironments.Prod,
            current: "000000010000000000000014");
        Assert.Equal((WalArchiveStates.Lagging, "Error", 1200L), (late.State, late.Status, late.LagSeconds!.Value));
        Assert.Contains("3 file", late.Summary, StringComparison.Ordinal);

        // Ít ghi dữ liệu: chép cuối 2 giờ trước nhưng không có file đầy chờ → vẫn chạy, không trễ.
        var idle = Evaluate(on, Stats(last: "000000010000000000000010", lastAt: Now.AddHours(-2)), AppEnvironments.Prod, current: "000000010000000000000011");
        Assert.Equal((WalArchiveStates.Running, 0L), (idle.State, idle.LagSeconds!.Value));

        var fresh = Evaluate(on, Stats(), AppEnvironments.Prod);
        Assert.Equal((WalArchiveStates.Running, "Warning"), (fresh.State, fresh.Status));
        Assert.Null(fresh.LagSeconds);
    }

    /// <summary>
    /// Có lỗi: lỗi sau lần chép thành công cuối; bật mà archive_command trống. Lỗi cũ đã chép lại được thì vẫn chạy. Tài khoản thiếu quyền
    /// (không thấy archive_command) không bị coi là trống.
    /// </summary>
    [Fact]
    public void CoLoi_VaKhongXemDuocLenhChep()
    {
        var on = Settings();
        var failing = Evaluate(on, Stats(last: "000000010000000000000010", lastAt: Now.AddMinutes(-30), failed: "000000010000000000000011",
            failedAt: Now.AddMinutes(-1)), AppEnvironments.Prod, current: "000000010000000000000012");
        Assert.Equal((WalArchiveStates.Failing, "Error"), (failing.State, failing.Status));
        Assert.Equal("Error", Assert.Single(failing.Rows, r => r.Item == "Lần chép lỗi cuối").Status);

        var recovered = Evaluate(on, Stats(last: "000000010000000000000011", lastAt: Now.AddMinutes(-1), failed: "000000010000000000000011",
            failedAt: Now.AddMinutes(-3)), AppEnvironments.Prod, current: "000000010000000000000012");
        Assert.Equal(WalArchiveStates.Running, recovered.State);

        var empty = Evaluate(Settings(command: ""), Stats(), AppEnvironments.Prod);
        Assert.Equal(WalArchiveStates.Failing, empty.State);
        Assert.Contains("archive_command trống", empty.Summary, StringComparison.Ordinal);

        var hidden = Evaluate(Settings(command: null), Stats(), AppEnvironments.Prod);
        Assert.Equal(WalArchiveStates.Running, hidden.State);
        Assert.Equal("(không xem được)", Assert.Single(hidden.Rows, r => r.Item == "archive_command").Value);
    }

    /// <summary>Tham số phụ: archive_timeout 0 hoặc &gt; 300 giây, summarize_wal tắt, giữ tóm tắt WAL dưới 8 ngày → đèn vàng; 35 ngày đạt.</summary>
    [Fact]
    public void ThamSoPhu_CanhBaoKhiChuaDat()
    {
        var bad = Evaluate(Settings(timeout: "0", summarize: "off", keepMinutes: "10080"), Stats(), AppEnvironments.Prod);
        Assert.Equal("Warning", Assert.Single(bad.Rows, r => r.Item == "archive_timeout").Status);
        Assert.Equal("Warning", Assert.Single(bad.Rows, r => r.Item == "summarize_wal").Status);
        var keep = Assert.Single(bad.Rows, r => r.Item == "wal_summary_keep_time");
        Assert.Equal(("7 ngày", "Warning"), (keep.Value, keep.Status));

        var good = Evaluate(Settings(), Stats(), AppEnvironments.Prod);
        Assert.Equal(("300 giây", "Ok"), Pick(good, "archive_timeout"));
        Assert.Equal(("35 ngày", "Ok"), Pick(good, "wal_summary_keep_time"));
    }

    private static (string, string) Pick(WalArchiveStatusDto dto, string item)
    {
        var row = Assert.Single(dto.Rows, r => r.Item == item);
        return (row.Value, row.Status);
    }

    private static (string, string) StateOf(Dictionary<string, WalSetting> s, WalArchiverStats a, string env)
    {
        var r = Evaluate(s, a, env);
        return (r.State, r.Status);
    }

    private static WalArchiveStatusDto Evaluate(Dictionary<string, WalSetting> s, WalArchiverStats a, string env, string? current = "000000010000000000000001") =>
        WalArchiveEvaluator.Evaluate(new WalArchiveSnapshot(s, a, current),
            new WalArchiveContext(env, new SystemOverviewRowDto("Nơi lưu", "Thư mục WAL (Backup:WalArchivePath)", "Chưa khai", "Warning", null), false, null),
            Now);

    private static WalArchiverStats Stats(string? last = null, DateTimeOffset? lastAt = null, string? failed = null, DateTimeOffset? failedAt = null) =>
        new(last is null ? 0 : 10, last, lastAt, failed is null ? 0 : 1, failed, failedAt);

    /// <summary>Tham số như sau khi chạy qt-enable-wal.ps1; <paramref name="command"/> null = tài khoản không xem được archive_command.</summary>
    private static Dictionary<string, WalSetting> Settings(
        string mode = "on", string level = "replica", bool modePending = false, string? command = "if not exist \"E:\\wal\\%f\" copy \"%p\" \"E:\\wal\\%f\"",
        string timeout = "300", string summarize = "on", string keepMinutes = "50400")
    {
        var s = new Dictionary<string, WalSetting>
        {
            ["wal_level"] = new(level, null, false),
            ["archive_mode"] = new(mode, null, modePending),
            ["archive_library"] = new("", null, false),
            ["archive_timeout"] = new(timeout, "s", false),
            ["summarize_wal"] = new(summarize, null, false),
            ["wal_summary_keep_time"] = new(keepMinutes, "min", false),
            ["wal_segment_size"] = new((16 * 1024 * 1024).ToString(System.Globalization.CultureInfo.InvariantCulture), "B", false),
        };
        if (command is not null) s["archive_command"] = new(command, null, false);
        return s;
    }
}
