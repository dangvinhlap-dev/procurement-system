using System.Net.Http.Json;
using Proc.Application.Purchasing;

namespace Proc.IntegrationTests;

/// <summary>
/// Tạo số liệu "đã nhận hàng" cho test báo cáo từ bản 0.22.0: lập PO → gửi → duyệt xong trong CSDL → phiếu xác nhận nhận hàng XNH → xác nhận
/// (thay phiếu nhập kho – design.md mục 19.4). Luồng XNH thật kiểm tra ở <c>ReceiptConfirmationTests</c>.
/// </summary>
internal static class TestReceipts
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Một dòng hàng cần nhận.</summary>
    /// <param name="ItemId">Mặt hàng.</param>
    /// <param name="UomId">ĐVT.</param>
    /// <param name="Qty">SL đặt.</param>
    /// <param name="UnitPrice">Đơn giá trước VAT (không chiết khấu).</param>
    /// <param name="AcceptedQty">SL đạt (null = nhận đủ SL đặt).</param>
    /// <param name="RejectedQty">SL không đạt (kết quả Không đạt).</param>
    /// <param name="VatCode">Thuế suất.</param>
    /// <param name="DeliveredQty">SL theo phiếu giao (null = đạt + không đạt).</param>
    /// <param name="CheckResult">Kết quả kiểm tra (null = Đạt, hoặc Không đạt nếu có SL không đạt).</param>
    public sealed record Line(
        Guid ItemId, Guid UomId, decimal Qty, decimal UnitPrice, decimal? AcceptedQty = null, decimal RejectedQty = 0, string VatCode = "10",
        decimal? DeliveredQty = null, string? CheckResult = null);

    /// <summary>
    /// Lập PO đã duyệt và XNH nháp nhận các dòng (chưa xác nhận).
    /// </summary>
    /// <param name="factory">Factory (duyệt PO trong CSDL).</param>
    /// <param name="client">Client lập PO, XNH (quyền PO.Create, Receipt.Manage, phạm vi kho nhận).</param>
    /// <param name="supplierId">NCC đã duyệt, áp dụng tại công ty.</param>
    /// <param name="warehouseId">Nơi nhận.</param>
    /// <param name="lines">Các dòng.</param>
    /// <param name="orderDate">Ngày PO (mặc định = ngày nhận).</param>
    /// <param name="receiptDate">Ngày nhận.</param>
    /// <param name="expectedDate">Ngày giao dự kiến của PO.</param>
    /// <param name="invoiceAmount">Tiền hóa đơn (gồm VAT) ghi trên XNH.</param>
    /// <returns>PO đã duyệt và XNH nháp.</returns>
    public static async Task<(PurchaseOrderDto Order, ReceiptConfirmationDto Receipt)> DraftAsync(
        ProcApiFactory factory, HttpClient client, Guid supplierId, Guid warehouseId, IReadOnlyList<Line> lines, DateOnly receiptDate,
        DateOnly? orderDate = null, DateOnly? expectedDate = null, decimal? invoiceAmount = null, string? invoiceNo = null)
    {
        var order = await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(orderDate ?? receiptDate, supplierId, warehouseId, expectedDate, null, null, null, 0, null,
                lines.Select(l => new SavePurchaseOrderLineRequest(null, l.ItemId, l.UomId, l.Qty, l.UnitPrice, 0, l.VatCode, null, null, null, "Giá thử")).ToList()), Ct));
        await ReadAsync<PurchaseOrderDto>(await client.PostAsync($"/api/purchase-orders/{order.Id}/submit", null, Ct));
        await factory.FinishPurchaseOrderApprovalAsync(order.Id);
        order = await ReadAsync<PurchaseOrderDto>(await client.GetAsync($"/api/purchase-orders/{order.Id}", Ct));
        var receipt = await ReadAsync<ReceiptConfirmationDto>(await client.PostAsJsonAsync("/api/receipt-confirmations",
            new SaveReceiptConfirmationRequest(order.Id, receiptDate, null, null, null, invoiceNo ?? (invoiceAmount is null ? null : "HD-THU"), null,
                invoiceAmount is null ? null : receiptDate, invoiceAmount,
                lines.Select((l, i) =>
                {
                    var accepted = l.AcceptedQty ?? l.Qty;
                    return new SaveReceiptConfirmationLineRequest(order.Lines[i].Id, l.DeliveredQty ?? accepted + l.RejectedQty, accepted, l.RejectedQty,
                        l.CheckResult ?? (l.RejectedQty > 0 ? "Failed" : "Passed"), l.RejectedQty > 0 ? "Hàng lỗi" : null, null);
                }).ToList()), Ct));
        return (order, receipt);
    }

    /// <summary>Như <see cref="DraftAsync"/> rồi xác nhận XNH.</summary>
    /// <returns>PO và XNH đã xác nhận.</returns>
    public static async Task<(PurchaseOrderDto Order, ReceiptConfirmationDto Receipt)> ReceiveAsync(
        ProcApiFactory factory, HttpClient client, Guid supplierId, Guid warehouseId, IReadOnlyList<Line> lines, DateOnly receiptDate,
        DateOnly? orderDate = null, DateOnly? expectedDate = null, decimal? invoiceAmount = null, string? invoiceNo = null)
    {
        var (order, draft) = await DraftAsync(factory, client, supplierId, warehouseId, lines, receiptDate, orderDate, expectedDate, invoiceAmount, invoiceNo);
        return (order, await ReadAsync<ReceiptConfirmationDto>(await client.PostAsync($"/api/receipt-confirmations/{draft.Id}/confirm", null, Ct)));
    }

    /// <summary>Điều chỉnh SL thực nhận dòng 1 (âm = trả NCC).</summary>
    /// <returns>XNH sau điều chỉnh.</returns>
    public static async Task<ReceiptConfirmationDto> AdjustAsync(HttpClient client, ReceiptConfirmationDto receipt, decimal qty, int lineIndex = 0) =>
        await ReadAsync<ReceiptConfirmationDto>(await client.PostAsJsonAsync($"/api/receipt-confirmations/{receipt.Id}/adjustments",
            new AdjustReceiptRequest(receipt.Lines[lineIndex].Id, qty, "Trả NCC hàng lỗi"), Ct));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
