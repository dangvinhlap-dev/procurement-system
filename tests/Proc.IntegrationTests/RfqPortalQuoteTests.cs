using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Application.Sourcing;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P3a (design.md mục 19.5, Q15): NCC thấy thư mời trên Portal, nộp / nộp lại báo giá niêm phong đến giờ hạn; nội bộ chỉ thấy giờ nộp –
/// xem giá trước giờ mở cần quyền riêng và ghi nhật ký; không nhập thay, không chọn thầu khi còn niêm phong; mở sau giờ hạn chép bản cuối;
/// NCC khác không thấy RFQ.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>Mời một tài khoản Portal cho NCC, kích hoạt, đăng nhập 2 bước; trả client Portal.</summary>
    private async Task<HttpClient> PortalClientAsync(HttpClient head, Guid supplierId)
    {
        var email = $"{Guid.NewGuid():N}"[..10] + "@ncc.vn";
        var invite = await ReadAsync<PortalInviteResult>(await head.PostAsJsonAsync(
            $"/api/suppliers/{supplierId}/portal-users", new PortalInviteRequest(email, "Người bán hàng", null, null), Ct));
        var token = Uri.UnescapeDataString(invite.InviteUrl[(invite.InviteUrl.IndexOf("token=", StringComparison.Ordinal) + 6)..]);
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent,
            (await anon.PostAsJsonAsync("/api/portal/auth/activate", new PortalSetPasswordRequest(token, "ncc12345"), Ct)).StatusCode);
        var challenge = await ReadAsync<PortalLoginChallenge>(
            await anon.PostAsJsonAsync("/api/portal/auth/login", new PortalLoginRequest(email, "ncc12345"), Ct));
        var login = await ReadAsync<PortalTokenResult>(
            await anon.PostAsJsonAsync("/api/portal/auth/verify-otp", new PortalVerifyOtpRequest(challenge.ChallengeId, challenge.DevCode!), Ct));
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    /// <summary>Dời hạn nộp RFQ về hôm qua (thay cho chờ đến giờ thật) – ghi thẳng CSDL.</summary>
    private async Task MoveDeadlineToPastAsync(Guid rfqId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        await db.Rfqs.IgnoreQueryFilters().Where(r => r.Id == rfqId)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Deadline, Today.AddDays(-1)).SetProperty(r => r.Date, Today.AddDays(-1)), Ct);
    }

    [Fact]
    public async Task BaoGiaPortal_NiemPhong_XemCanQuyen_MoSauGioHan()
    {
        var f = await ArrangeAsync();
        var rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync("/api/rfqs",
            RfqOf(f, "Dây curoa – báo giá online", new SaveRfqLineRequest(null, f.ItemId, f.Cai, 40, null, null)) with
            {
                Deadline = Today.AddDays(1), DeadlineTime = new TimeOnly(16, 30),
            }, Ct));
        Assert.Equal(new TimeOnly(16, 30), rfq.DeadlineTime);
        var ncc = await PortalClientAsync(f.Buyer, f.SupplierId);
        // RFQ nháp chưa hiện trên Portal.
        Assert.DoesNotContain((await ncc.GetFromJsonAsync<List<PortalRfqListItemDto>>("/api/portal/rfqs", Ct))!, r => r.Id == rfq.Id);
        await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/send", new SendRfqRequest("Chỉ một NCC cung cấp"), Ct));

        var invited = (await ncc.GetFromJsonAsync<List<PortalRfqListItemDto>>("/api/portal/rfqs", Ct))!.Single(r => r.Id == rfq.Id);
        Assert.Equal(("Open", "Công ty thử mua hàng", Today.AddDays(1).ToDateTime(new TimeOnly(16, 30))),
            (invited.Status, invited.CompanyName, invited.DeadlineAt));
        var detail = await ReadAsync<PortalRfqDto>(await ncc.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct));
        Assert.True(detail.CanSubmit);
        var lineId = detail.Lines.Single().Id;
        Assert.Equal(("Dây curoa", 40m), (detail.Lines[0].ItemName, detail.Lines[0].Qty));

        // Nộp rồi nộp lại: bản cuối có hiệu lực, có biên nhận mã băm.
        await ReadAsync<PortalRfqDto>(await ncc.PostAsJsonAsync($"/api/portal/rfqs/{rfq.Id}/quotes",
            new PortalSubmitQuoteRequest(null, "Chuyển khoản", null, [new(lineId, 120_000, 0, "10", 3, null)]), Ct));
        detail = await ReadAsync<PortalRfqDto>(await ncc.PostAsJsonAsync($"/api/portal/rfqs/{rfq.Id}/quotes",
            new PortalSubmitQuoteRequest(Today.AddDays(30), "Chuyển khoản 15 ngày", "Giá gồm vận chuyển", [new(lineId, 110_000, 2, "10", 3, null)]), Ct));
        Assert.Equal((2, 110_000m, 2), (detail.Latest!.Sequence, detail.Latest.Lines[0].UnitPrice, detail.Submissions.Count));
        Assert.Equal(4_312_000m, detail.Latest.Lines[0].Amount); // 40 × 110.000 × 98%.
        Assert.Equal(64, detail.Latest.ContentHash.Length);

        // Nội bộ: chỉ thấy đã nộp lúc nào, không có giá; không nhập thay, không chọn thầu, chưa mở được.
        rfq = await ReadAsync<RfqDto>(await f.Buyer.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        var row = rfq.Suppliers.Single();
        Assert.Equal((false, 2, 0, 1), (row.HasQuoted, row.PortalSubmissionCount, row.Lines.Count, rfq.PortalSubmittedSupplierCount));
        Assert.Null(row.PortalContentHash);
        Assert.False(rfq.Actions.CanOpenPortalQuotes);
        Assert.True(rfq.Actions.CanViewSealedQuotes);
        Assert.Contains("đã nộp báo giá qua Portal", await TitleAsync(await f.Buyer.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{f.SupplierId}",
            new SaveQuotationRequest(Today, null, null, 8, 8, 8, 8, null, [new(lineId, 1, 0, "10", 1, null)]), Ct)));
        Assert.Contains("niêm phong", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award",
            new AwardRfqRequest([new(lineId, f.SupplierId, null)], "Chỉ một NCC"), Ct)));
        Assert.Contains("Chưa đến giờ mở", await TitleAsync(await f.Buyer.PostAsync($"/api/rfqs/{rfq.Id}/open-portal-quotes", null, Ct)));

        // Xem giá trước giờ mở: người có quyền (quản trị) xem được và bị ghi nhật ký; người chỉ có quyền xem RFQ thì không.
        var sealedQuotes = await ReadAsync<List<SealedQuoteDto>>(await f.Buyer.GetAsync($"/api/rfqs/{rfq.Id}/sealed-quotes", Ct));
        Assert.Equal((110_000m, 2, 4_312_000m), (sealedQuotes.Single().Lines[0].UnitPrice, sealedQuotes[0].Sequence, sealedQuotes[0].TotalAmount));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.Contains(await db.AccessLogs.Where(l => l.Event == "SealedQuoteViewed").Select(l => l.Detail).ToListAsync(Ct),
                d => d!.Contains(rfq.No, StringComparison.Ordinal));
        }

        var roleCode = UniqueCode("XRFQ");
        await _factory.CreateRoleAsync(roleCode, Permissions.Sourcing.RfqView, Permissions.Sourcing.RfqManage);
        var viewerName = $"xr.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, f.CompanyCode, roleCode, [], []);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/rfqs/{rfq.Id}/sealed-quotes", Ct)).StatusCode);
        Assert.False((await ReadAsync<RfqDto>(await viewer.GetAsync($"/api/rfqs/{rfq.Id}", Ct))).Actions.CanViewSealedQuotes);

        // NCC khác (có tài khoản Portal) không thấy thư mời này. NCC lập ở tổng công ty.
        var headName = $"hq.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(headName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(headName, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var other = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC không được mời", null, false, null, null, null, null, null, null, null, null, "Approved", null,
                null, [], []), Ct));
        var outsider = await PortalClientAsync(head, other.Id);
        Assert.Empty((await outsider.GetFromJsonAsync<List<PortalRfqListItemDto>>("/api/portal/rfqs", Ct))!);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync($"/api/portal/rfqs/{rfq.Id}/quotes",
            new PortalSubmitQuoteRequest(null, null, null, [new(lineId, 1, 0, null, null, null)]), Ct)).StatusCode);
        // Token nội bộ không gọi được API Portal.
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Buyer.GetAsync("/api/portal/rfqs", Ct)).StatusCode);

        // Qua giờ hạn: NCC không nộp được; mở báo giá chép bản cuối.
        await MoveDeadlineToPastAsync(rfq.Id);
        Assert.Contains("hết hạn", await TitleAsync(await ncc.PostAsJsonAsync($"/api/portal/rfqs/{rfq.Id}/quotes",
            new PortalSubmitQuoteRequest(null, null, null, [new(lineId, 90_000, 0, "10", 3, null)]), Ct)));
        Assert.Equal("Closed", (await ReadAsync<PortalRfqDto>(await ncc.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct))).Status);
        rfq = await ReadAsync<RfqDto>(await f.Buyer.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        Assert.True(rfq.Actions.CanOpenPortalQuotes);
        rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsync($"/api/rfqs/{rfq.Id}/open-portal-quotes", null, Ct));
        row = rfq.Suppliers.Single();
        Assert.Equal((true, true, 110_000m, 2m), (row.HasQuoted, row.IsFromPortal, row.Lines[0].UnitPrice, row.Lines[0].DiscountPct));
        Assert.Equal(detail.Latest.ContentHash, row.PortalContentHash);
        Assert.NotNull(rfq.PortalQuotesOpenedAt);
        Assert.NotNull(rfq.PortalQuotesOpenedByName);
        Assert.False(rfq.Actions.CanViewSealedQuotes);
        Assert.Equal("Reviewing", (await ReadAsync<PortalRfqDto>(await ncc.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct))).Status);

        // Sau mở: chấm điểm được (cả sau hạn), không sửa giá thay NCC; chọn thầu được.
        rfq = await ReadAsync<RfqDto>(await f.Buyer.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/scores/{f.SupplierId}",
            new ScoreQuotationRequest(9, 8, 7, 6), Ct));
        Assert.Equal(9m, rfq.Suppliers.Single().QualityScore);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Buyer.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{f.SupplierId}",
            new SaveQuotationRequest(Today, null, null, 8, 8, 8, 8, null, [new(lineId, 1, 0, "10", 1, null)]), Ct)).StatusCode);
        rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award",
            new AwardRfqRequest([new(lineId, f.SupplierId, null)], "Chỉ một NCC"), Ct));
        Assert.Equal("AwardPending", rfq.Status);
        Assert.Equal("Reviewing", (await ReadAsync<PortalRfqDto>(await ncc.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct))).Status);
    }
}
