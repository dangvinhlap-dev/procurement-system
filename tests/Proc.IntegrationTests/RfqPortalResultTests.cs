using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P4b (design.md mục 19.5, P4-7…P4-9): báo NCC kết quả chọn thầu. Kết quả chỉ lộ trên Portal khi hội đồng **duyệt xong** và Phòng CUVT
/// **bấm gửi kết quả** (người dùng yêu cầu 13/11/2026); mỗi NCC chỉ thấy
/// kết quả của mình (trúng – kèm giá của chính NCC, không trúng, không báo giá); thư kết quả gửi NCC đã có báo giá, không có giá, không nêu NCC
/// trúng.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task KetQuaChonThau_SauDuyetXong_NccChiThayKetQuaCuaMinh_ThuBaoKetQua()
    {
        var f = await ArrangeAsync();
        var headName = $"kq.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(headName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(headName, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var others = new List<Guid>();
        foreach (var name in new[] { "NCC trượt thầu", "NCC không báo giá" })
        {
            var s = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(s.Id);
            await ReadAsync<SupplierCompanyDto>(await f.Buyer.PutAsJsonAsync($"/api/suppliers/{s.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            others.Add(s.Id);
        }

        var (loser, silent) = (others[0], others[1]);
        var winnerName = (await ReadAsync<SupplierDetailDto>(await f.Buyer.GetAsync($"/api/suppliers/{f.SupplierId}", Ct))).Name;
        var nccWin = await PortalClientAsync(f.Buyer, f.SupplierId);
        var nccLose = await PortalClientAsync(f.Buyer, loser);
        var nccSilent = await PortalClientAsync(f.Buyer, silent);

        var rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync("/api/rfqs",
            RfqOf(f, "Dây curoa – kết quả", new SaveRfqLineRequest(null, f.ItemId, f.Cai, 10, null, null)) with
            {
                Deadline = Today.AddDays(1), DeadlineTime = new TimeOnly(17, 0), SupplierIds = [f.SupplierId, loser, silent],
            }, Ct));
        rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/send", new SendRfqRequest(null), Ct));
        var lineId = rfq.Lines[0].Id;
        // Phòng CUVT nhập báo giá thay hai NCC (kết quả áp cả báo giá nhập thay, không riêng báo giá Portal).
        await ReadAsync<RfqDto>(await f.Buyer.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{f.SupplierId}",
            new SaveQuotationRequest(Today, null, null, 8, 8, 8, 8, null, [new(lineId, 90_000, 5, "10", 3, null)]), Ct));
        await ReadAsync<RfqDto>(await f.Buyer.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{loser}",
            new SaveQuotationRequest(Today, null, null, 8, 8, 8, 8, null, [new(lineId, 99_000, 0, "10", 3, null)]), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award",
            new AwardRfqRequest([new(lineId, f.SupplierId, null)], "Chỉ hai NCC báo giá"), Ct));
        Assert.Equal("AwardPending", rfq.Status);

        // Đang chờ hội đồng duyệt: chưa lộ kết quả, chưa có thư kết quả.
        var pending = await ReadAsync<PortalRfqDto>(await nccWin.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct));
        Assert.Equal(("Reviewing", null), (pending.Status, pending.Result));
        Assert.Null((await nccWin.GetFromJsonAsync<List<PortalRfqListItemDto>>("/api/portal/rfqs", Ct))!.Single(r => r.Id == rfq.Id).Outcome);
        var url = $"/api/rfqs/{rfq.Id}/portal-emails";
        Assert.DoesNotContain(await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(url, Ct)),
            m => m.Kind is "RfqAwarded" or "RfqNotAwarded");

        var approverRole = UniqueCode("H");
        await _factory.CreateRoleAsync(approverRole, Permissions.Sourcing.RfqView, Permissions.Sourcing.RfqApproveAward);
        var approverName = $"hd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(approverName, f.CompanyCode, approverRole);
        var approver = await _factory.CreateClientInCompanyAsync(approverName, ProcApiFactory.TestPassword, f.CompanyCode);
        rfq = await ReadAsync<RfqDto>(await approver.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/approve", new ApprovalActionRequest("Đồng ý"), Ct));
        Assert.Equal("Awarded", rfq.Status);
        // Người duyệt không có quyền lập RFQ: không thấy nút gửi kết quả, gọi API bị chặn.
        Assert.False(rfq.Actions.CanSendResult);
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostAsync($"/api/rfqs/{rfq.Id}/send-result", null, Ct)).StatusCode);

        // Duyệt xong nhưng Phòng CUVT chưa bấm gửi: NCC vẫn "Đang xét chọn", chưa có thư (người dùng yêu cầu 13/11/2026).
        var notSent = await ReadAsync<PortalRfqDto>(await nccWin.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct));
        Assert.Equal(("Reviewing", null), (notSent.Status, notSent.Result));
        Assert.DoesNotContain(await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(url, Ct)),
            m => m.Kind is "RfqAwarded" or "RfqNotAwarded");
        rfq = await ReadAsync<RfqDto>(await f.Buyer.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        Assert.True(rfq.Actions.CanSendResult);

        // Phòng CUVT bấm gửi kết quả – một lần.
        rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsync($"/api/rfqs/{rfq.Id}/send-result", null, Ct));
        Assert.NotNull(rfq.ResultSentAt);
        Assert.NotNull(rfq.ResultSentByName);
        Assert.False(rfq.Actions.CanSendResult);
        Assert.Contains("đã gửi", await TitleAsync(await f.Buyer.PostAsync($"/api/rfqs/{rfq.Id}/send-result", null, Ct)));

        // NCC trúng: thấy dòng trúng với giá của chính mình – 10 × 90.000 × 95% = 855.000.
        var won = await ReadAsync<PortalRfqDto>(await nccWin.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct));
        Assert.Equal(("Awarded", "Won", 1, 855_000m), (won.Status, won.Result!.Outcome, won.Result.QuotedLineCount, won.Result.WonAmount));
        Assert.NotNull(won.Result.AwardedAt);
        var wonLine = Assert.Single(won.Result.WonLines);
        Assert.Equal((lineId, 90_000m, 5m, "10"), (wonLine.RfqLineId, wonLine.UnitPrice, wonLine.DiscountPct, wonLine.VatCode));
        Assert.Equal("Won", (await nccWin.GetFromJsonAsync<List<PortalRfqListItemDto>>("/api/portal/rfqs", Ct))!.Single(r => r.Id == rfq.Id).Outcome);

        // NCC trượt: chỉ biết không trúng – không có giá NCC trúng, tên NCC trúng.
        var lostResponse = await nccLose.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct);
        var lostJson = await lostResponse.Content.ReadAsStringAsync(Ct);
        var lost = await ReadAsync<PortalRfqDto>(lostResponse);
        Assert.Equal(("Lost", 0), (lost.Result!.Outcome, lost.Result.WonLines.Count));
        Assert.DoesNotContain(winnerName, lostJson, StringComparison.Ordinal);
        Assert.DoesNotContain("90000", lostJson, StringComparison.Ordinal);
        Assert.Equal("NoQuote", (await ReadAsync<PortalRfqDto>(await nccSilent.GetAsync($"/api/portal/rfqs/{rfq.Id}", Ct))).Result!.Outcome);

        // Thư kết quả: trúng → NCC trúng; không trúng → NCC trượt; NCC không báo giá không có thư.
        var mails = await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(url, Ct));
        Assert.All(mails.Where(m => m.Kind == "RfqAwarded"), m => Assert.Equal(f.SupplierId, m.SupplierId));
        Assert.All(mails.Where(m => m.Kind == "RfqNotAwarded"), m => Assert.Equal(loser, m.SupplierId));
        Assert.Equal((await ActivePortalEmailsAsync(f.SupplierId)).Count, mails.Count(m => m.Kind == "RfqAwarded"));
        Assert.Equal((await ActivePortalEmailsAsync(loser)).Count, mails.Count(m => m.Kind == "RfqNotAwarded"));
        Assert.DoesNotContain(mails, m => m.SupplierId == silent && m.Kind is "RfqAwarded" or "RfqNotAwarded");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var stored = await db.SupplierPortalEmails.IgnoreQueryFilters()
            .Where(m => m.DocId == rfq.Id && (m.Kind == SupplierPortalEmailKind.RfqAwarded || m.Kind == SupplierPortalEmailKind.RfqNotAwarded))
            .ToListAsync(Ct);
        var awardedMail = stored.First(m => m.Kind == SupplierPortalEmailKind.RfqAwarded);
        Assert.Contains("1/1", awardedMail.TextBody, StringComparison.Ordinal);
        // Thư không có giá; thư cảm ơn không nêu NCC trúng.
        Assert.All(stored, m => Assert.DoesNotContain("90.000", m.HtmlBody + m.TextBody, StringComparison.Ordinal));
        Assert.DoesNotContain(winnerName, stored.First(m => m.Kind == SupplierPortalEmailKind.RfqNotAwarded).TextBody, StringComparison.Ordinal);
    }
}
