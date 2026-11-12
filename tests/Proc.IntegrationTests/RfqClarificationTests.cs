using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Application.Sourcing;
using Proc.Domain.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P3b-2 (design.md mục 19.5, P3-11…P3-14): NCC hỏi làm rõ RFQ trên Portal đến giờ hạn; Phòng CUVT trả lời **riêng NCC hỏi** (NCC khác
/// không thấy câu hỏi lẫn câu trả lời); thông báo làm rõ **công khai** cho mọi NCC được mời; người lập RFQ nhận thông báo khi có câu hỏi.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task HoiDapPortal_TraLoiRiengNccHoi_ThongBaoCongKhai()
    {
        var f = await ArrangeAsync();

        // NCC thứ hai được mời cùng RFQ.
        var headName = $"hq.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(headName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(headName, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var second = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC thứ hai – hỏi đáp", null, false, null, null, null, null, null, null, null, null, "Approved",
                null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(second.Id);
        await ReadAsync<SupplierCompanyDto>(await f.Buyer.PutAsJsonAsync($"/api/suppliers/{second.Id}/company",
            new SaveSupplierCompanyRequest(null, null, true), Ct));

        var rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync("/api/rfqs",
            RfqOf(f, "Ống thép – hỏi đáp", new SaveRfqLineRequest(null, f.ItemId, f.Cai, 10, null, null)) with
            {
                Deadline = Today.AddDays(1), DeadlineTime = new TimeOnly(17, 0), SupplierIds = [f.SupplierId, second.Id],
            }, Ct));
        await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/send", new SendRfqRequest("Chỉ hai NCC cung cấp"), Ct));
        var ncc1 = await PortalClientAsync(f.Buyer, f.SupplierId);
        var ncc2 = await PortalClientAsync(head, second.Id);
        var portalUrl = $"/api/portal/rfqs/{rfq.Id}";

        // NCC 1 hỏi; câu hỏi trống bị chặn.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ncc1.PostAsJsonAsync($"{portalUrl}/questions", new PortalAskRequest("  "), Ct)).StatusCode);
        var asked = await ReadAsync<PortalRfqDto>(await ncc1.PostAsJsonAsync($"{portalUrl}/questions",
            new PortalAskRequest("Ống có cần mạ kẽm nhúng nóng không?"), Ct));
        Assert.True(asked.CanAsk);
        var question = asked.Clarifications.Single();
        Assert.Null(question.Answer);

        // Người lập RFQ nhận thông báo kèm tên NCC và câu hỏi.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var note = await db.Notifications.IgnoreQueryFilters()
                .SingleAsync(n => n.Type == NotificationTypes.PortalQuestion && n.TargetId == rfq.Id, Ct);
            Assert.Contains("mạ kẽm", note.Message, StringComparison.Ordinal);
            Assert.Equal("RFQ", note.ScreenCode);
        }

        // Nội bộ thấy NCC hỏi; trả lời riêng NCC hỏi, trả lời lần hai bị chặn.
        var url = $"/api/rfqs/{rfq.Id}/clarifications";
        var list = await ReadAsync<List<RfqClarificationDto>>(await f.Buyer.GetAsync(url, Ct));
        Assert.Equal((f.SupplierId, "Ống có cần mạ kẽm nhúng nóng không?"), (list.Single().SupplierId, list[0].Question));
        list = await ReadAsync<List<RfqClarificationDto>>(await f.Buyer.PostAsJsonAsync($"{url}/{question.Id}/answer",
            new ReplyClarificationRequest("Có, mạ kẽm nhúng nóng theo TCVN 5408."), Ct));
        Assert.NotNull(list.Single().AnsweredByName);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Buyer.PostAsJsonAsync($"{url}/{question.Id}/answer",
            new ReplyClarificationRequest("Sửa lại"), Ct)).StatusCode);

        // Thông báo làm rõ công khai.
        await ReadAsync<List<RfqClarificationDto>>(await f.Buyer.PostAsJsonAsync(url,
            new AnnounceClarificationRequest("Giá chào đã gồm vận chuyển đến kho công ty."), Ct));

        // NCC 1: thấy câu hỏi – trả lời của mình và thông báo. NCC 2: chỉ thấy thông báo, không thấy câu hỏi lẫn câu trả lời của NCC 1.
        var view1 = await ReadAsync<PortalRfqDto>(await ncc1.GetAsync(portalUrl, Ct));
        Assert.Equal(2, view1.Clarifications.Count);
        Assert.Equal("Có, mạ kẽm nhúng nóng theo TCVN 5408.", view1.Clarifications.Single(c => !c.IsNotice).Answer);
        var view2 = await ReadAsync<PortalRfqDto>(await ncc2.GetAsync(portalUrl, Ct));
        var only = Assert.Single(view2.Clarifications);
        Assert.True(only.IsNotice);
        Assert.Equal("Giá chào đã gồm vận chuyển đến kho công ty.", only.Answer);

        // Qua giờ hạn: NCC không hỏi được nữa.
        await MoveDeadlineToPastAsync(rfq.Id);
        Assert.False((await ReadAsync<PortalRfqDto>(await ncc2.GetAsync(portalUrl, Ct))).CanAsk);
        Assert.Contains("không còn nhận câu hỏi", await TitleAsync(await ncc2.PostAsJsonAsync($"{portalUrl}/questions",
            new PortalAskRequest("Hỏi muộn"), Ct)) ?? "", StringComparison.Ordinal);
    }
}
