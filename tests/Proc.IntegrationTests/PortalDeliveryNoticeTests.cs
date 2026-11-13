using System.Net;
using System.Net.Http.Json;
using Proc.Application.Documents;
using Proc.Application.Portal;
using Proc.Application.Purchasing;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P5 (design.md mục 19.5, P5-1…P5-6): NCC báo giao hàng trên Portal (SL không vượt phần còn giao được, kể cả dung sai), file kèm, hủy có lý
/// do; nơi nhận thấy phiếu ở tab "Giao hàng NCC" của PO, lập XNH theo phiếu (một XNH chưa hủy / phiếu); NCC thấy trạng thái và SL thực nhận.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task PortalBaoGiao_SlConGiao_HuyCoLyDo_LapXnhTheoPhieu_NccThayThucNhan()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var ncc = await PortalClientAsync(f.Buyer, f.SupplierId);

        // PO 10 cái, dung sai 10% → NCC báo giao được tối đa 11.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null, tolerance: 10));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Approved", po.Status);
        var orderUrl = $"/api/portal/orders/{po.Id}";
        var noticesUrl = $"{orderUrl}/delivery-notices";
        var detail = await ReadAsync<PortalOrderDto>(await ncc.GetAsync(orderUrl, Ct));
        var line = Assert.Single(detail.Lines);
        Assert.Equal((true, 0m, 11m), (detail.CanNotifyDelivery, line.ReceivedQty, line.DeliverableQty));

        PortalDeliveryNoticeRequest Request(string? noteNo, decimal qty) =>
            new(Today.AddDays(2), noteNo, "Xe 51C-123.45", null, [new(line.PurchaseOrderLineId, qty, "Lô 11/2026")]);

        Assert.Contains("vượt SL còn giao được", await TitleAsync(await ncc.PostAsJsonAsync(noticesUrl, Request("PGH-01", 12), Ct)));
        Assert.Contains("số phiếu giao", await TitleAsync(await ncc.PostAsJsonAsync(noticesUrl, Request(" ", 6), Ct)));

        // Báo giao 6: thủ kho nơi nhận / người lập PO nhận thông báo; còn giao được 5.
        var first = await ReadAsync<PortalDeliveryNoticeDto>(await ncc.PostAsJsonAsync(noticesUrl, Request("PGH-01", 6), Ct));
        Assert.Equal(("Submitted", true, "PGH-01"), (first.Status, first.CanCancel, first.DeliveryNoteNo));
        Assert.StartsWith("BGH", first.No, StringComparison.Ordinal);
        var note = Assert.Single(await NotificationApi.MineAsync(f.Buyer, Ct), n => n.TargetId == po.Id && n.Type == "PortalDeliveryNotice");
        Assert.Contains("PGH-01", note.Message, StringComparison.Ordinal);
        detail = await ReadAsync<PortalOrderDto>(await ncc.GetAsync(orderUrl, Ct));
        Assert.Equal(5m, detail.Lines[0].DeliverableQty);

        // File kèm phiếu: NCC thêm khi chưa nhận; nội bộ chỉ xem.
        AttachmentDto file;
        using (var form = PdfForm("phieu-giao.pdf"))
            file = await ReadAsync<AttachmentDto>(await ncc.PostAsync($"{noticesUrl}/{first.Id}/attachments", form, Ct));
        Assert.True(file.CanDelete);
        Assert.False(Assert.Single(await ReadAsync<List<AttachmentDto>>(await f.Buyer.GetAsync(
            $"/api/attachments?ownerType=DeliveryNoticeFile&ownerId={first.Id}", Ct))).CanDelete);

        // Báo giao tiếp 5 → hết phần còn giao; hủy phiếu thứ hai (bắt buộc lý do) thì còn giao lại 5.
        var second = await ReadAsync<PortalDeliveryNoticeDto>(await ncc.PostAsJsonAsync(noticesUrl, Request("PGH-02", 5), Ct));
        detail = await ReadAsync<PortalOrderDto>(await ncc.GetAsync(orderUrl, Ct));
        Assert.Equal((false, 0m), (detail.CanNotifyDelivery, detail.Lines[0].DeliverableQty));
        Assert.Contains("vượt SL còn giao được", await TitleAsync(await ncc.PostAsJsonAsync(noticesUrl, Request("PGH-03", 1), Ct)));
        Assert.Contains("lý do", await TitleAsync(await ncc.PostAsJsonAsync($"{noticesUrl}/{second.Id}/cancel",
            new PortalCancelDeliveryNoticeRequest(" "), Ct)));
        detail = await ReadAsync<PortalOrderDto>(await ncc.PostAsJsonAsync($"{noticesUrl}/{second.Id}/cancel",
            new PortalCancelDeliveryNoticeRequest("Xe hỏng, giao lại sau"), Ct));
        Assert.Equal((true, 5m), (detail.CanNotifyDelivery, detail.Lines[0].DeliverableQty));
        Assert.Equal("Cancelled", detail.DeliveryNotices.Single(n => n.Id == second.Id).Status);

        // Nội bộ: tab "Giao hàng NCC" của PO; lập XNH theo phiếu thứ nhất (SL thực tế: đạt 5, không đạt 1).
        var internalNotices = await ReadAsync<List<DeliveryNoticeDto>>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}/delivery-notices", Ct));
        Assert.Equal(2, internalNotices.Count);
        var internalFirst = internalNotices.Single(n => n.Id == first.Id);
        Assert.Equal(("Submitted", true, 6m), (internalFirst.Status, internalFirst.CanCreateReceipt, internalFirst.Lines[0].Qty));
        Assert.False(internalNotices.Single(n => n.Id == second.Id).CanCreateReceipt);
        Assert.Equal(first.No, (await ReadAsync<DeliveryNoticeDto>(await f.Buyer.GetAsync($"/api/delivery-notices/{first.Id}", Ct))).No);

        SaveReceiptConfirmationRequest Receipt(Guid? noticeId) =>
            new(po.Id, Today, "PGH-01", "Xe 51C-123.45", null, null, null, null, null,
                [new(line.PurchaseOrderLineId, 6, 5, 1, "Failed", "Móp vỏ 1 cái", null)], noticeId);
        Assert.Contains("đã hủy", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/receipt-confirmations", Receipt(second.Id), Ct)));
        var xnh = await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PostAsJsonAsync("/api/receipt-confirmations", Receipt(first.Id), Ct));
        Assert.Equal((first.Id, first.No), (xnh.DeliveryNoticeId, xnh.DeliveryNoticeNo));
        Assert.Contains(xnh.No, await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/receipt-confirmations", Receipt(first.Id), Ct)));

        // NCC: phiếu "Đang nhận" – không hủy, không thêm / xóa file được.
        detail = await ReadAsync<PortalOrderDto>(await ncc.GetAsync(orderUrl, Ct));
        var receiving = detail.DeliveryNotices.Single(n => n.Id == first.Id);
        Assert.Equal(("Receiving", false, xnh.No), (receiving.Status, receiving.CanCancel, receiving.ReceiptNo));
        Assert.Null(receiving.Lines[0].ReceivedQty);
        Assert.Contains("phiếu nhận hàng", await TitleAsync(await ncc.PostAsJsonAsync($"{noticesUrl}/{first.Id}/cancel",
            new PortalCancelDeliveryNoticeRequest("Đổi ý"), Ct)));
        Assert.False((await ncc.DeleteAsync($"{noticesUrl}/{first.Id}/attachments/{file.Id}", Ct)).IsSuccessStatusCode);

        // Xác nhận XNH → NCC thấy "Đã nhận", SL đạt / không đạt; dòng đơn đã nhận 5, còn giao được 11 − 5 = 6.
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PostAsync($"/api/receipt-confirmations/{xnh.Id}/confirm", null, Ct));
        detail = await ReadAsync<PortalOrderDto>(await ncc.GetAsync(orderUrl, Ct));
        var received = detail.DeliveryNotices.Single(n => n.Id == first.Id);
        Assert.Equal(("Received", Today, 5m, 1m), (received.Status, received.ReceivedDate, received.Lines[0].ReceivedQty, received.Lines[0].RejectedQty));
        Assert.Equal((5m, 6m), (detail.Lines[0].ReceivedQty, detail.Lines[0].DeliverableQty));

        // NCC khác không báo giao được đơn này (404 như không tồn tại).
        var otherHead = $"hd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(otherHead, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(otherHead, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<Proc.Application.MasterData.SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var other = await ReadAsync<Proc.Application.MasterData.SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new Proc.Application.MasterData.SaveSupplierRequest(null, groupId, "NCC khác – báo giao", null, false, null, null, null, null, null, null,
                null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(other.Id);
        var otherNcc = await PortalClientAsync(head, other.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await otherNcc.PostAsJsonAsync(noticesUrl, Request("PGH-X", 1), Ct)).StatusCode);
    }
}
