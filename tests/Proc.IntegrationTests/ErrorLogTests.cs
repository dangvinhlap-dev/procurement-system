using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Domain.Notifications;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT2 – nhật ký lỗi (màn hình ERR, design.md mục 21.4): lỗi 500 vào nhóm kèm mã theo dõi trùng câu báo lỗi, gộp lỗi trùng, mở lại khi lặp
/// sau khi đã xử lý (kèm thông báo người quản lý), lỗi giao diện (giới hạn 8 KB, 30 lần/phút), quyền, dọn theo hạn giữ.
/// </summary>
[Collection(ApiCollection.Name)]
public class ErrorLogTests
{
    private const string TestErrorPath = "/api/ops/v1/error-groups/test-server-error";

    private readonly ProcApiFactory _factory;

    public ErrorLogTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private sealed record Problem(string Title, string? TrackingCode);

    private Task<HttpClient> HeadAsync() =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");

    /// <summary>Gây lỗi thử, trả mã theo dõi (kiểm tra câu báo lỗi có đúng mã đó).</summary>
    private static async Task<string> CauseServerErrorAsync(HttpClient client)
    {
        var response = await client.PostAsync(TestErrorPath, null, Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<Problem>(Ct))!;
        Assert.Matches("^ERR-[0-9A-Z]{8}$", problem.TrackingCode!);
        Assert.Contains($"mã theo dõi {problem.TrackingCode}", problem.Title);
        Assert.DoesNotContain("InvalidOperationException", problem.Title);
        return problem.TrackingCode!;
    }

    private static async Task<ErrorGroupItemDto> FindByCodeAsync(HttpClient client, string code) =>
        Assert.Single((await ReadAsync<ErrorGroupPage>(await client.GetAsync(
            $"/api/ops/v1/error-groups?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&trackingCode={code.ToLowerInvariant()[4..]}", Ct))).Items);

    private async Task<int> NotificationCountAsync(Guid groupId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        return await db.Notifications.IgnoreQueryFilters().CountAsync(n => n.Type == NotificationTypes.ErrorLogNew && n.TargetId == groupId, Ct);
    }

    /// <summary>
    /// Lỗi 500: câu báo lỗi có mã theo dõi, nhóm lỗi có lần xảy ra mang đúng mã (đường dẫn, người, công ty, phiên bản, môi trường); lần sau cùng
    /// chỗ gộp vào nhóm (tăng số lần). Đã xử lý mà lặp lại thì tự mở lại Mới và báo người có quyền xử lý ở tổng công ty; đổi trạng thái ghi nhật
    /// ký vận hành.
    /// </summary>
    [Fact]
    public async Task LoiMayChu_CoMaTheoDoi_GopNhom_MoLaiKhiLap()
    {
        var head = await HeadAsync();
        var first = await CauseServerErrorAsync(head);
        var group = await FindByCodeAsync(head, first);
        Assert.Equal(("Server", "System.InvalidOperationException", "/api/ops/v1/error-groups/test-server-error"),
            (group.Source, group.ErrorType, group.Path));
        Assert.Contains("ThrowTestServerError", group.Location);

        var detail = await ReadAsync<ErrorGroupDetailDto>(await head.GetAsync($"/api/ops/v1/error-groups/{group.Id}", Ct));
        var occurrence = Assert.Single(detail.Occurrences, o => o.TrackingCode == first);
        Assert.Equal((ProcApiFactory.AdminUserName, "TCT", "POST", AppEnvironments.Dev),
            (occurrence.UserName, occurrence.CompanyCode, occurrence.Method, occurrence.Environment));
        Assert.Contains("InvalidOperationException", occurrence.Detail);
        Assert.False(string.IsNullOrEmpty(occurrence.Version));

        var second = await CauseServerErrorAsync(head);
        var merged = await FindByCodeAsync(head, second);
        Assert.Equal(group.Id, merged.Id);
        Assert.Equal(group.OccurrenceCount + 1, merged.OccurrenceCount);
        Assert.Equal(second, merged.LastTrackingCode);

        var resolved = await ReadAsync<ErrorGroupItemDto>(await head.PutAsJsonAsync($"/api/ops/v1/error-groups/{group.Id}/status",
            new ChangeErrorGroupStatusRequest("Resolved", "Đã sửa ở bản sau"), Ct));
        Assert.Equal(("Resolved", "Đã sửa ở bản sau", ProcApiFactory.AdminUserName), (resolved.Status, resolved.Note, resolved.HandledByName));
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PutAsJsonAsync($"/api/ops/v1/error-groups/{group.Id}/status",
            new ChangeErrorGroupStatusRequest("2", null), Ct)).StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.ErrorGroupStatusChanged && l.Reason == "Đã sửa ở bản sau", Ct));
        }

        var notificationsBefore = await NotificationCountAsync(group.Id);
        var third = await CauseServerErrorAsync(head);
        var reopened = await FindByCodeAsync(head, third);
        Assert.Equal(("New", resolved.ReopenCount + 1, "Đã sửa ở bản sau"), (reopened.Status, reopened.ReopenCount, reopened.Note));
        // Admin có mọi quyền ở tổng công ty nên nhận thông báo (kèm email qua hàng chờ) "lỗi lặp lại sau khi đã xử lý".
        Assert.True(await NotificationCountAsync(group.Id) > notificationsBefore);
    }

    /// <summary>
    /// Lỗi giao diện: nhóm mới nguồn Client (đường dẫn trang chuẩn hóa, công ty lấy từ phạm vi request) và có thông báo nhóm mới; báo lỗi quá 8 KB
    /// trả 413, sai định dạng 400; một phiên gửi quá 30 lần/phút thì 429.
    /// </summary>
    [Fact]
    public async Task LoiGiaoDien_NhomMoi_GioiHanKichThuocVaTanSuat()
    {
        var member = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var unique = Guid.NewGuid().ToString("N")[..8];
        var request = new ClientErrorRequest("render", "TypeError", $"x{unique} is undefined",
            $"TypeError: x{unique} is undefined\n    at Row{unique} (http://localhost:5173/src/pages/Po{unique}.tsx:10:5)", "    at Row\n    at Grid",
            "http://localhost:5173/mua-hang/po/0193a3b2-1c2d-7e8f-9a0b-1c2d3e4f5a6b?tab=1");
        var code = (await ReadAsync<ErrorTrackingDto>(await member.PostAsJsonAsync("/api/ops/v1/client-errors", request, Ct))).TrackingCode;

        var head = await HeadAsync();
        var group = await FindByCodeAsync(head, code);
        Assert.Equal(("Client", "TypeError", "/mua-hang/po/{id}", "New"), (group.Source, group.ErrorType, group.Path, group.Status));
        var detail = await ReadAsync<ErrorGroupDetailDto>(await head.GetAsync($"/api/ops/v1/error-groups/{group.Id}", Ct));
        var occurrence = Assert.Single(detail.Occurrences);
        Assert.Equal(("BHA", (string?)null), (occurrence.CompanyCode, occurrence.Method));
        Assert.Contains("Cây component", occurrence.Detail);
        Assert.Equal(1, await NotificationCountAsync(group.Id));

        var big = new string('x', Proc.Api.Endpoints.OperationsEndpoints.ClientErrorMaxBytes);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await member.PostAsJsonAsync("/api/ops/v1/client-errors",
            request with { Stack = big }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsync("/api/ops/v1/client-errors",
            new StringContent("{không phải json", Encoding.UTF8, "application/json"), Ct)).StatusCode);

        // Phiên mới (token mới) để không lẫn số lượt với các lần gửi ở trên.
        var flood = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 31; i++)
            statuses.Add((await flood.PostAsJsonAsync("/api/ops/v1/client-errors", request with { Message = "lặp" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, statuses[0]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    /// <summary>
    /// Quyền: công ty thành viên 403; người chỉ có quyền xem thì xem được, không đổi trạng thái / gây lỗi thử được; người không có quyền xem 403.
    /// </summary>
    [Fact]
    public async Task Quyen_ChiTongCongTy_XemVaXuLyTach()
    {
        var range = $"from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var member = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/ops/v1/error-groups?{range}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync(TestErrorPath, null, Ct)).StatusCode);

        var roleCode = $"E{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(roleCode, Permissions.System.OpsErrorLogView);
        var viewerName = $"ev.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "TCT", roleCode);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "TCT");
        var page = await ReadAsync<ErrorGroupPage>(await viewer.GetAsync($"/api/ops/v1/error-groups?{range}&status=New", Ct));
        Assert.All(page.Items, g => Assert.Equal("New", g.Status));
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync(TestErrorPath, null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync($"/api/ops/v1/error-groups/{Guid.NewGuid()}/status",
            new ChangeErrorGroupStatusRequest("Resolved", null), Ct)).StatusCode);

        var otherRole = $"F{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(otherRole, Permissions.System.OpsView);
        var otherName = $"eo.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(otherName, "TCT", otherRole);
        var other = await _factory.CreateClientInCompanyAsync(otherName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/ops/v1/error-groups?{range}", Ct)).StatusCode);
    }

    /// <summary>
    /// Dọn theo tham số ERRLOG_KEEP_DAYS (tổng công ty, mặc định 90): lần xảy ra cũ hơn hạn bị xóa, nhóm không còn lần nào xóa theo; nhóm còn lần
    /// mới giữ lại. Tham số ngoài 7–3650 bị từ chối.
    /// </summary>
    [Fact]
    public async Task DonTheoHanGiu_ThamSoTongCongTy()
    {
        var head = await HeadAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PutAsJsonAsync($"/api/system-parameters/{SystemParameterCodes.ErrorLogKeepDays}",
            new UpdateSystemParameterRequest("5"), Ct)).StatusCode);
        await ReadAsync<SystemParameterDto>(await head.PutAsJsonAsync($"/api/system-parameters/{SystemParameterCodes.ErrorLogKeepDays}",
            new UpdateSystemParameterRequest("30"), Ct));

        var now = DateTimeOffset.UtcNow;
        Guid oldGroup, mixedGroup;
        Guid oldInMixed, recentInMixed;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var old = ErrorGroup.Create(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), ErrorSource.Server, "Old", "cũ", null, null,
                now.AddDays(-40));
            var mixed = ErrorGroup.Create(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), ErrorSource.Client, "Mixed", "trộn", null, null,
                now.AddDays(-40));
            db.ErrorGroups.AddRange(old, mixed);
            await db.SaveChangesAsync(Ct);
            var o1 = ErrorOccurrence.Create(old.Id, now.AddDays(-40), "ERR-OLD00001", ErrorSource.Server, null, null, null, null, null, null, "cũ",
                null, null, null, null);
            var o2 = ErrorOccurrence.Create(mixed.Id, now.AddDays(-40), "ERR-OLD00002", ErrorSource.Client, null, null, null, null, null, null, "cũ",
                null, null, null, null);
            var o3 = ErrorOccurrence.Create(mixed.Id, now.AddDays(-1), "ERR-NEW00003", ErrorSource.Client, null, null, null, null, null, null, "mới",
                null, null, null, null);
            db.ErrorOccurrences.AddRange(o1, o2, o3);
            await db.SaveChangesAsync(Ct);
            (oldGroup, mixedGroup, oldInMixed, recentInMixed) = (old.Id, mixed.Id, o2.Id, o3.Id);
        }

        try
        {
            using var scope = _factory.Services.CreateScope();
            var (occurrences, groups) = await scope.ServiceProvider.GetRequiredService<IErrorLogService>().PurgeAsync(Ct);
            Assert.True(occurrences >= 2 && groups >= 1, $"{occurrences} lần, {groups} nhóm");
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.False(await db.ErrorGroups.AnyAsync(g => g.Id == oldGroup, Ct));
            Assert.True(await db.ErrorGroups.AnyAsync(g => g.Id == mixedGroup, Ct));
            Assert.False(await db.ErrorOccurrences.AnyAsync(o => o.Id == oldInMixed, Ct));
            Assert.True(await db.ErrorOccurrences.AnyAsync(o => o.Id == recentInMixed, Ct));
            Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.ErrorLogPurged && l.Reason == "Giữ 30 ngày", Ct));
        }
        finally
        {
            await head.PutAsJsonAsync($"/api/system-parameters/{SystemParameterCodes.ErrorLogKeepDays}", new UpdateSystemParameterRequest(""), Ct);
        }
    }
}
