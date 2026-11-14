using System.Net;
using System.Net.Http.Json;
using Proc.Application.Documents;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Phiếu xác nhận nhận hàng XNH (lát K2, design.md mục 19.4): nhận từng phần theo PO, SL thực nhận là nguồn "đã nhận" của PO, nhận đủ thì
/// PO đóng, điều chỉnh giảm thì PO mở lại; nghiệm thu của đơn vị đặt hàng; phạm vi kho nơi nhận.
/// </summary>
public partial class PurchasingTests
{
    private static SaveReceiptConfirmationRequest Xnh(
        PurchaseOrderDto po, decimal delivered, decimal accepted, decimal rejected = 0, string? result = "Passed", string? checkNote = null) =>
        new(po.Id, Today, "PG-01", "Xe 51C", null, null, null, null, null,
            [new(po.Lines[0].Id, delivered, accepted, rejected, result, checkNote, null)]);

    private async Task<PurchaseOrderDto> ApprovedPoAsync(Fixture f, decimal qty)
    {
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, qty, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        await _factory.FinishPurchaseOrderApprovalAsync(po.Id);
        return await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
    }

    [Fact]
    public async Task Xnh_NhanTungPhan_NhanDuThiPoDong_DieuChinhGiamThiMoLai_HoanThanhPo()
    {
        var f = await ArrangeAsync();
        var po = await ApprovedPoAsync(f, 10);
        Assert.Equal("Approved", po.Status);

        // Phiếu 1: giao 6, đạt 5, không đạt 1 → nháp chưa tính vào PO.
        Assert.Contains("ghi chú", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/receipt-confirmations", Xnh(po, 6, 5, 1, "Failed"), Ct)));
        var first = await ReadAsync<ReceiptConfirmationDto>(
            await f.Buyer.PostAsJsonAsync("/api/receipt-confirmations", Xnh(po, 6, 5, 1, "Failed", "1 cái vỡ"), Ct));
        Assert.StartsWith("XNH", first.No);
        Assert.Equal("Draft", first.Status);
        Assert.Equal(10m, first.Lines[0].OrderedQty);
        // Giá thực mua theo PO: 100.000 − 5% = 95.000 × 5 = 475.000, VAT 10%.
        Assert.Equal(475_000m, first.TotalAmount);
        Assert.Equal(47_500m, first.TotalVat);
        Assert.Equal(0m, (await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct))).Lines[0].ReceivedQty);

        first = await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PostAsync($"/api/receipt-confirmations/{first.Id}/confirm", null, Ct));
        Assert.Equal("Confirmed", first.Status);
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal(5m, po.Lines[0].ReceivedQty);
        Assert.Equal(5m, po.Lines[0].RemainingQty);
        Assert.Equal("Approved", po.Status);
        Assert.Contains(await f.Buyer.GetFromJsonAsync<List<DeliveryTrackingDto>>("/api/purchase-orders/tracking", Ct) ?? [],
            t => t.OrderId == po.Id && t.RemainingQty == 5);

        // Vượt SL còn nhận (dung sai 0%).
        Assert.Contains("vượt SL còn nhận", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/receipt-confirmations", Xnh(po, 6, 6), Ct)));
        var second = await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PostAsJsonAsync("/api/receipt-confirmations", Xnh(po, 5, 5), Ct));
        Assert.Equal(5m, second.Lines[0].OtherReceivedQty);
        // Còn phiếu nháp thì chưa hoàn thành PO được.
        Assert.Contains("nháp", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/close-remaining",
            new CloseRemainingRequest("NCC hết hàng"), Ct)));
        second = await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PostAsync($"/api/receipt-confirmations/{second.Id}/confirm", null, Ct));
        Assert.Equal("Closed", (await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct))).Status);
        Assert.Contains("đã xác nhận", await TitleAsync(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{second.Id}", Xnh(po, 5, 4), Ct)));

        // Trả NCC 2 cái → PO mở lại, còn nhận 2; ghi tăng vượt SL đặt thì chặn.
        Assert.Contains("lý do", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/receipt-confirmations/{second.Id}/adjustments",
            new AdjustReceiptRequest(second.Lines[0].Id, -2, null), Ct)));
        second = await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PostAsJsonAsync($"/api/receipt-confirmations/{second.Id}/adjustments",
            new AdjustReceiptRequest(second.Lines[0].Id, -2, "Trả NCC 2 cái sai quy cách"), Ct));
        Assert.Equal(3m, second.Lines[0].ReceivedQty);
        Assert.Equal("Trả NCC 2 cái sai quy cách", Assert.Single(second.Adjustments).Reason);
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal("Approved", po.Status);
        Assert.Equal(8m, po.Lines[0].ReceivedQty);
        Assert.Contains("vượt SL còn nhận", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/receipt-confirmations/{second.Id}/adjustments",
            new AdjustReceiptRequest(second.Lines[0].Id, 3, "Ghi nhầm"), Ct)));

        // Có phiếu nhận hàng thì không hủy PO – dùng Hoàn thành PO (đóng phần còn lại).
        Assert.Contains("phiếu nhận hàng", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/cancel",
            new CancelDocumentRequest("Không mua nữa"), Ct)));
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/close-remaining",
            new CloseRemainingRequest("NCC không giao bù"), Ct));
        Assert.Equal("Closed", po.Status);
        Assert.Contains("đã hoàn thành", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/receipt-confirmations/{second.Id}/adjustments",
            new AdjustReceiptRequest(second.Lines[0].Id, 1, "Giao bù"), Ct)));
        var list = await f.Buyer.GetFromJsonAsync<ReceiptConfirmationListResult>($"/api/receipt-confirmations?purchaseOrderId={po.Id}", Ct);
        Assert.Equal(2, list!.Items.Count);
    }

    [Fact]
    public async Task Xnh_NghiemThuCuaDonViDatHang_ThuKhoKhongTuNghiemThu_PhamViKhoNoiNhan()
    {
        var f = await ArrangeAsync();
        var po = await ApprovedPoAsync(f, 4);

        // Thủ kho kho khác (KP) không lập được XNH cho PO nhận về KT; thủ kho KT lập được.
        await ReadAsync<WarehouseDetailDto>(await f.Buyer.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest("KP", "Kho phụ", f.Kt, null, null, false), Ct));
        var keeperRole = UniqueCode("K");
        await _factory.CreateRoleAsync(keeperRole, Permissions.Purchase.ReceiptView, Permissions.Purchase.ReceiptManage);
        async Task<HttpClient> KeeperAsync(string warehouse)
        {
            var name = $"tk.{Guid.NewGuid():N}"[..16];
            await _factory.CreateScopedUserAsync(name, f.CompanyCode, keeperRole, [warehouse]);
            return await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, f.CompanyCode);
        }

        var other = await KeeperAsync("KP");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/receipt-confirmations", Xnh(po, 4, 4), Ct)).StatusCode);
        var keeper = await KeeperAsync("KT");
        var xnh = await ReadAsync<ReceiptConfirmationDto>(await keeper.PostAsJsonAsync("/api/receipt-confirmations", Xnh(po, 4, 4), Ct));
        Assert.True(xnh.Actions.CanConfirm);
        xnh = await ReadAsync<ReceiptConfirmationDto>(await keeper.PostAsync($"/api/receipt-confirmations/{xnh.Id}/confirm", null, Ct));
        Assert.True(xnh.Actions.CanAdjust);
        Assert.False(xnh.Actions.CanAccept);
        // Thủ kho kho khác vẫn xem được (xem theo công ty) nhưng không thao tác.
        Assert.False((await ReadAsync<ReceiptConfirmationDto>(await other.GetAsync($"/api/receipt-confirmations/{xnh.Id}", Ct))).Actions.CanAdjust);

        // Đơn vị đặt hàng (không phạm vi kho) nghiệm thu; người nhận hàng không tự nghiệm thu.
        var unitRole = UniqueCode("U");
        await _factory.CreateRoleAsync(unitRole, Permissions.Purchase.ReceiptView, Permissions.Purchase.ReceiptAccept);
        var unitName = $"dv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(unitName, f.CompanyCode, unitRole);
        var unit = await _factory.CreateClientInCompanyAsync(unitName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await keeper.PostAsJsonAsync($"/api/receipt-confirmations/{xnh.Id}/acceptance", new ReceiptAcceptanceRequest(true, null), Ct)).StatusCode);
        Assert.Contains("ý kiến", await TitleAsync(await unit.PostAsJsonAsync($"/api/receipt-confirmations/{xnh.Id}/acceptance",
            new ReceiptAcceptanceRequest(false, null), Ct)));
        Assert.True((await ReadAsync<ReceiptConfirmationDto>(await unit.GetAsync($"/api/receipt-confirmations/{xnh.Id}", Ct))).Actions.CanAccept);
        xnh = await ReadAsync<ReceiptConfirmationDto>(await unit.PostAsJsonAsync($"/api/receipt-confirmations/{xnh.Id}/acceptance",
            new ReceiptAcceptanceRequest(false, "Thiếu phụ kiện"), Ct));
        Assert.Equal("Disputed", xnh.Acceptance);
        Assert.Equal(HttpStatusCode.Forbidden, (await unit.PostAsync($"/api/receipt-confirmations/{xnh.Id}/confirm", null, Ct)).StatusCode);

        // Hóa đơn về sau – cập nhật khi đã xác nhận; tra cứu chứng từ có loại XNH.
        xnh = await ReadAsync<ReceiptConfirmationDto>(await keeper.PutAsJsonAsync($"/api/receipt-confirmations/{xnh.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("0000123", "1C26TAA", Today, 418_000), Ct));
        Assert.Equal(418_000m, xnh.InvoiceAmount);
        var registry = await keeper.GetFromJsonAsync<DocumentRegistryResult>(
            $"/api/document-registry?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&docTypes=XNH", Ct);
        Assert.Contains(registry!.Rows, d => d.Id == xnh.Id && d.Progress == DocumentProgress.Done && d.StatusName == "Đã xác nhận");
    }

    /// <summary>
    /// In XNH ra PDF (lát K2b, design.md mục 10.10, 19.4): biên bản giao nhận và kiểm tra hàng hóa, in được cả nháp và sau điều chỉnh; quyền
    /// như xem XNH (cả người chỉ có quyền nghiệm thu); XNH công ty khác → 404; không có quyền XNH → 403.
    /// </summary>
    [Fact]
    public async Task InXnh_BienBanGiaoNhan_PhamViVaQuyen()
    {
        var f = await ArrangeAsync();
        var po = await ApprovedPoAsync(f, 10);
        var xnh = await ReadAsync<ReceiptConfirmationDto>(
            await f.Buyer.PostAsJsonAsync("/api/receipt-confirmations", Xnh(po, 8, 6, 1, "Failed", "Vỡ 1, thiếu 1 so phiếu giao"), Ct));

        static async Task<PdfSharp.Pdf.PdfDocument> PdfAsync(HttpResponseMessage response)
        {
            Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
            return PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(bytes), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        }

        // Nháp in được (dòng đỏ "BẢN NHÁP"); tên file theo số phiếu.
        var draftResponse = await f.Buyer.GetAsync($"/api/receipt-confirmations/{xnh.Id}/pdf", Ct);
        Assert.Contains(xnh.No, draftResponse.Content.Headers.ContentDisposition?.ToString() ?? "");
        Assert.Equal($"BIÊN BẢN GIAO NHẬN VÀ KIỂM TRA HÀNG HÓA {xnh.No}", (await PdfAsync(draftResponse)).Info.Title);

        // Đã xác nhận + điều chỉnh giảm: vẫn in được (cột SL thực nhận sau điều chỉnh).
        xnh = await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PostAsync($"/api/receipt-confirmations/{xnh.Id}/confirm", null, Ct));
        xnh = await TestReceipts.AdjustAsync(f.Buyer, xnh, -2);
        Assert.Equal(4m, xnh.Lines[0].ReceivedQty);
        await PdfAsync(await f.Buyer.GetAsync($"/api/receipt-confirmations/{xnh.Id}/pdf", Ct));

        // Người chỉ có quyền nghiệm thu in được; người không có quyền XNH → 403.
        var unitRole = UniqueCode("U");
        await _factory.CreateRoleAsync(unitRole, Permissions.Purchase.ReceiptAccept);
        var unitName = $"dv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(unitName, f.CompanyCode, unitRole);
        var unit = await _factory.CreateClientInCompanyAsync(unitName, ProcApiFactory.TestPassword, f.CompanyCode);
        await PdfAsync(await unit.GetAsync($"/api/receipt-confirmations/{xnh.Id}/pdf", Ct));
        var poRole = UniqueCode("P");
        await _factory.CreateRoleAsync(poRole, Permissions.Purchase.PoView);
        var poName = $"po.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(poName, f.CompanyCode, poRole);
        var poOnly = await _factory.CreateClientInCompanyAsync(poName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await poOnly.GetAsync($"/api/receipt-confirmations/{xnh.Id}/pdf", Ct)).StatusCode);

        // XNH của công ty khác: không thấy (404) – như khi mở phiếu.
        var head = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        Assert.Equal(HttpStatusCode.NotFound, (await head.GetAsync($"/api/receipt-confirmations/{xnh.Id}/pdf", Ct)).StatusCode);
    }

    /// <summary>Báo cáo SL đặt – SL thực nhận / OTIF (design.md mục 19.4): đủ, thiếu, trễ hạn; tổng hợp theo NCC.</summary>
    [Fact]
    public async Task BaoCaoOtif_SlDatSlThucNhan_DungHanVaDu()
    {
        var f = await ArrangeAsync();
        // PO1 hẹn giao sau 5 ngày: nhận đủ 10 hôm nay → đúng hạn, đủ. PO2: nhận 3/4 → đúng hạn, thiếu 1.
        var po1 = await ApprovedPoAsync(f, 10);
        await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po1, 10)));
        var po2 = await ApprovedPoAsync(f, 4);
        await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po2, 3)));
        // PO3 hẹn giao hôm qua: nhận đủ 2 hôm nay → trễ.
        var (po3, _) = await TestReceipts.ReceiveAsync(_factory, f.Buyer, f.SupplierId, f.Kt, [new(f.ItemId, f.Cai, 2, 100_000)], Today,
            orderDate: Today.AddDays(-3), expectedDate: Today.AddDays(-1));

        var report = await ReadAsync<OrderFulfillmentResult>(await f.Buyer.GetAsync(
            $"/api/reports/order-fulfillment?from={Today.AddDays(-3):yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        var rows = report.Rows.ToDictionary(r => r.OrderId);
        Assert.Equal((10m, 0m, true, true, (DateOnly?)Today), (rows[po1.Id].ReceivedQty, rows[po1.Id].ShortQty, rows[po1.Id].OnTime, rows[po1.Id].InFull, rows[po1.Id].LastReceiptDate));
        Assert.Equal((3m, 1m, true, false), (rows[po2.Id].ReceivedQty, rows[po2.Id].ShortQty, rows[po2.Id].OnTime, rows[po2.Id].InFull));
        Assert.Equal(((bool?)false, true), (rows[po3.Id].OnTime, rows[po3.Id].InFull));
        // 3 dòng: đủ 2 (66,7%), đúng hạn 2/3 (66,7%), OTIF 1/3 (33,3%).
        var supplier = Assert.Single(report.Suppliers);
        Assert.Equal((3, 2, 3, 2, 1, 66.7m, 66.7m, 33.3m),
            (supplier.Lines, supplier.InFullLines, supplier.ScoredLines, supplier.OnTimeLines, supplier.OtifLines, supplier.InFullPct!.Value,
                supplier.OnTimePct!.Value, supplier.OtifPct!.Value));
        Assert.Equal(33.3m, report.Total.OtifPct);
    }
}
