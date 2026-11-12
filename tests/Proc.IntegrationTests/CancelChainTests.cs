using System.Net;
using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Z4b (design.md mục 20.2 D3, 20.2a): hủy dây chuyền PR → PO → phiếu xác nhận nhận hàng – xem trước trình tự từ phiếu cuối về phiếu đầu,
/// hủy cả chuỗi trong một giao dịch, PR khác của PO gộp được cảnh báo và trả SL về "còn lại chưa đặt"; chỉ quản trị hủy phiếu đã gửi duyệt.
/// Người dùng chốt 11/11/2026: chuỗi đã có XNH đã xác nhận thì chặn hủy – chỉ điều chỉnh. Lát Y5c: bảng tổng hợp 2A/2B nằm giữa PR và PO.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>PR đã duyệt, SL <paramref name="qty"/>.</summary>
    private async Task<PurchaseRequestDto> ApprovedPrAsync(Fixture f, HttpClient approver, decimal qty)
    {
        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, qty));
        await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(approver, "purchase-requests", pr.Id, "approve"));
        // Bắt buộc qua bảng tổng hợp đã duyệt trước khi lên PO (lát Y5c).
        await _factory.ApproveSummaryForRequestAsync(pr.Id);
        return pr;
    }

    [Fact]
    public async Task HuyDayChuyen_TuPr_HuyXnhNhapRoiPoRoiPr_PrKhacTraVeConLai()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var pr1 = await ApprovedPrAsync(f, approver, 10);
        var pr2 = await ApprovedPrAsync(f, approver, 5);

        // PO gộp dòng của hai PR, đã duyệt; có phiếu nhận hàng còn nháp (hàng chưa xác nhận về).
        var po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, f.SupplierId, f.Kt, Today.AddDays(5), "30 ngày", null, null, 0, null,
                [new(null, f.ItemId, f.Cai, 10, 100_000, 5, "10", null, null, pr1.Lines[0].Id),
                 new(null, f.ItemId, f.Cai, 5, 100_000, 5, "10", null, null, pr2.Lines[0].Id)]), Ct));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));
        var xnh = await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 4));

        // Xem trước: phiếu cuối trước – XNH → PO → bảng tổng hợp của PR1 → PR1; cảnh báo PR2 bị ảnh hưởng.
        var preview = await f.Buyer.GetFromJsonAsync<CancelChainDto>($"/api/cancel-chain/PR/{pr1.Id}", Ct);
        Assert.Equal([xnh.No, po.No], preview!.Items.Take(2).Select(i => i.No));
        Assert.Equal(pr1.No, preview.Items[^1].No);
        Assert.Equal([1, 2, 3, 4], preview.Items.Select(i => i.Order));
        Assert.Equal(["XNH", "PO", "BTH", "PR"], preview.Items.Select(i => i.DocType));
        var summaryId = preview.Items[2].Id;
        Assert.Contains(pr2.No, Assert.Single(preview.Warnings));
        Assert.Null(preview.BlockedReason);

        // Người mua thường (không phải quản trị) thấy lý do bị chặn, gọi hủy thì 403; thiếu lý do thì 400. Không phiếu nào bị hủy.
        var (plain, _) = await PlainBuyerAsync(f);
        Assert.NotNull((await plain.GetFromJsonAsync<CancelChainDto>($"/api/cancel-chain/PR/{pr1.Id}", Ct))!.BlockedReason);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await plain.PostAsJsonAsync($"/api/cancel-chain/PR/{pr1.Id}", new CancelChainRequest("Thôi"), Ct)).StatusCode);
        Assert.Contains("lý do hủy", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/cancel-chain/PR/{pr1.Id}", new CancelChainRequest(" "), Ct)));
        Assert.Equal("Approved", (await f.Buyer.GetFromJsonAsync<PurchaseOrderDto>($"/api/purchase-orders/{po.Id}", Ct))!.Status);

        // Quản trị hủy cả chuỗi: XNH nháp, PO, PR1 đã hủy kèm lý do; PR2 còn nguyên, SL trở về còn lại chưa đặt.
        var done = await ReadAsync<CancelChainDto>(await f.Buyer.PostAsJsonAsync($"/api/cancel-chain/PR/{pr1.Id}", new CancelChainRequest("Đổi phương án mua"), Ct));
        Assert.Equal(4, done.Items.Count);
        Assert.Equal("Cancelled", (await f.Buyer.GetFromJsonAsync<PurchaseSummaryDto>($"/api/purchase-summaries/{summaryId}", Ct))!.Status);
        var xnhAfter = await f.Buyer.GetFromJsonAsync<ReceiptConfirmationDto>($"/api/receipt-confirmations/{xnh.Id}", Ct);
        var poAfter = await f.Buyer.GetFromJsonAsync<PurchaseOrderDto>($"/api/purchase-orders/{po.Id}", Ct);
        var pr1After = await f.Buyer.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr1.Id}", Ct);
        Assert.Equal(("Cancelled", "Cancelled", "Cancelled"), (xnhAfter!.Status, poAfter!.Status, pr1After!.Status));
        Assert.Equal(("Đổi phương án mua", "Đổi phương án mua"), (poAfter.CancelReason, pr1After.CancelReason));
        var pr2After = await f.Buyer.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr2.Id}", Ct);
        Assert.Equal("Approved", pr2After!.Status);
        var open = await f.Buyer.GetFromJsonAsync<List<OpenRequestLineDto>>("/api/purchase-requests/open-lines", Ct);
        Assert.Equal(5m, open!.Single(l => l.RequestId == pr2.Id).RemainingQty);
        Assert.DoesNotContain(open!, l => l.RequestId == pr1.Id);

        // Hủy lại phiếu đã hủy thì báo lỗi.
        Assert.Contains("đã hủy", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/cancel-chain/PR/{pr1.Id}", new CancelChainRequest("Lần 2"), Ct)));
    }

    [Fact]
    public async Task HuyDayChuyen_TuBangTongHop_RfqChuaHuyThiChan_HuyPoRoiBang_DongDeNghiKhongConMo()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var pr = await ApprovedPrAsync(f, approver, 6);
        var summaryId = (await f.Buyer.GetFromJsonAsync<CancelChainDto>($"/api/cancel-chain/PR/{pr.Id}", Ct))!.Items.Single(i => i.DocType == "BTH").Id;
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 6, pr.Lines[0].Id));

        // RFQ chưa hủy hỏi giá dòng của bảng: chặn – RFQ có chuỗi riêng, hủy RFQ trước.
        var rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync("/api/rfqs", new SaveRfqRequest(Today, "Hỏi giá", Today.AddDays(7), null,
            new RfqWeightsDto(60, 20, 10, 5, 5), null, [new SaveRfqLineRequest(null, f.ItemId, f.Cai, 6, pr.Lines[0].Id, null)], [f.SupplierId]), Ct));
        var preview = (await f.Buyer.GetFromJsonAsync<CancelChainDto>($"/api/cancel-chain/BTH/{summaryId}", Ct))!;
        Assert.Contains(rfq.No, preview.BlockedReason);
        Assert.Equal(["PO", "BTH"], preview.Items.Select(i => i.DocType));
        await ReadAsync<CancelChainDto>(await f.Buyer.PostAsJsonAsync($"/api/cancel-chain/RFQ/{rfq.Id}", new CancelChainRequest("Không hỏi giá"), Ct));

        // Hủy từ bảng: PO rồi bảng; đề nghị còn nguyên nhưng dòng không còn là dòng mở (phải lập bảng tổng hợp khác).
        var done = await ReadAsync<CancelChainDto>(await f.Buyer.PostAsJsonAsync($"/api/cancel-chain/BTH/{summaryId}", new CancelChainRequest("Lập lại bảng"), Ct));
        Assert.Equal([po.No], done.Items.Where(i => i.DocType == "PO").Select(i => i.No));
        Assert.Equal("Cancelled", (await f.Buyer.GetFromJsonAsync<PurchaseOrderDto>($"/api/purchase-orders/{po.Id}", Ct))!.Status);
        var summary = (await f.Buyer.GetFromJsonAsync<PurchaseSummaryDto>($"/api/purchase-summaries/{summaryId}", Ct))!;
        Assert.Equal(("Cancelled", "Lập lại bảng"), (summary.Status, summary.CancelReason));
        Assert.Equal("Approved", (await f.Buyer.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr.Id}", Ct))!.Status);
        Assert.DoesNotContain((await f.Buyer.GetFromJsonAsync<List<OpenRequestLineDto>>("/api/purchase-requests/open-lines", Ct))!, l => l.RequestId == pr.Id);
    }

    [Fact]
    public async Task HuyDayChuyen_ChuoiCoXnhDaXacNhan_ChanHuy_ChiDieuChinh()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var pr = await ApprovedPrAsync(f, approver, 8);
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 8, pr.Lines[0].Id));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));
        var xnh = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 3)));
        Assert.False(xnh.Actions.CanCancel);
        Assert.False((await f.Buyer.GetFromJsonAsync<PurchaseOrderDto>($"/api/purchase-orders/{po.Id}", Ct))!.Actions.CanCancel);

        // Hàng đã về (XNH đã xác nhận): xem trước báo chặn kèm số XNH, hủy từ PR, PO hay XNH đều bị chặn – không phiếu nào đổi.
        foreach (var (type, id) in new[] { ("PR", pr.Id), ("PO", po.Id), ("XNH", xnh.Id) })
        {
            var preview = await f.Buyer.GetFromJsonAsync<CancelChainDto>($"/api/cancel-chain/{type}/{id}", Ct);
            Assert.Contains(xnh.No, preview!.BlockedReason);
            Assert.Contains("Điều chỉnh SL thực nhận", preview.BlockedReason);
            Assert.Contains("không hủy được", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/cancel-chain/{type}/{id}", new CancelChainRequest("Thử"), Ct)));
        }

        Assert.Equal("Confirmed", (await f.Buyer.GetFromJsonAsync<ReceiptConfirmationDto>($"/api/receipt-confirmations/{xnh.Id}", Ct))!.Status);
        Assert.Equal("Approved", (await f.Buyer.GetFromJsonAsync<PurchaseOrderDto>($"/api/purchase-orders/{po.Id}", Ct))!.Status);
        Assert.Equal("Approved", (await f.Buyer.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr.Id}", Ct))!.Status);
    }
}
