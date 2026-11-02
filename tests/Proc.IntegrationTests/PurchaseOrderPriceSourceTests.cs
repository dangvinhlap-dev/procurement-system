using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Application.Sourcing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// PO theo giá đã duyệt (lát C1 – design.md mục 18.10): nguồn giá hợp đồng/bảng giá/ngoài hợp đồng, lệch giá bắt buộc lý do và qua chặng
/// duyệt ngoại lệ (loại POX).
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchaseOrderPriceSourceTests
{
    private readonly ProcApiFactory _factory;

    public PurchaseOrderPriceSourceTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private sealed record Fixture(
        HttpClient Head, HttpClient Client, string CompanyCode, Guid ItemId, Guid Cai, Guid Thung, Guid SupplierId, Guid NoPriceSupplierId,
        Guid PriceListId, string PriceListNo, Guid WarehouseId);

    /// <summary>
    /// Công ty mới; mặt hàng (cái, thùng = 12 cái); NCC A và NCC C đã duyệt, áp dụng tại công ty; bảng giá toàn tập đoàn của A đã hiệu
    /// lực (cái 50.000 CK 5%, bậc 1.000 cái 48.000 CK 5%; thùng 600.000). NCC C không có bảng giá.
    /// </summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"ng.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("G"), "Công ty thử nguồn giá", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Nhóm nguồn giá", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Vòng bi 6207", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<Guid> SupplierAsync(string name)
        {
            var s = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(s.Id);
            await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{s.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return s.Id;
        }

        var a = await SupplierAsync("NCC nguồn giá A");
        var c = await SupplierAsync("NCC nguồn giá C");
        var list = await ReadAsync<PriceListDto>(await head.PostAsJsonAsync("/api/price-lists",
            new SavePriceListRequest(a, "Bảng giá vòng bi tập đoàn", true, Today, Today.AddYears(1), null, null,
                [new(null, item.Id, cai, 0, 50_000, 5, null, null), new(null, item.Id, cai, 1_000, 48_000, 5, null, null), new(null, item.Id, thung, 0, 600_000, 0, null, null)]), Ct));
        await ReadAsync<PriceListDto>(await head.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        await _factory.FinishPriceListApprovalAsync(list.Id);
        var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        return new Fixture(head, client, company.Code, item.Id, cai, thung, a, c, list.Id, list.No, warehouse);
    }

    private static SavePurchaseOrderRequest Po(Fixture f, Guid supplierId, params SavePurchaseOrderLineRequest[] lines) =>
        new(Today, supplierId, f.WarehouseId, null, null, null, null, 0, null, lines);

    private static SavePurchaseOrderLineRequest Line(Fixture f, decimal qty, decimal price, decimal discount, string? reason = null, Guid? uomId = null) =>
        new(null, f.ItemId, uomId ?? f.Cai, qty, price, discount, "10", null, null, null, reason);

    /// <summary>Người dùng có quyền xem và duyệt PO (và hợp đồng), có thể gán vai trò cho trước.</summary>
    private async Task<HttpClient> ApproverAsync(Fixture f, string roleCode)
    {
        var userName = $"dg.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);
    }

    private async Task<string> RoleAsync()
    {
        var code = UniqueCode("R");
        await _factory.CreateRoleAsync(code, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove, Permissions.Purchase.ContractView,
            Permissions.Purchase.ContractApprove, Permissions.Purchase.PrView, Permissions.Purchase.PrApprove, Permissions.MasterData.SoleSourceView,
            Permissions.MasterData.SoleSourceApprove);
        return code;
    }

    [Fact]
    public async Task Sla_DangDat_ThamSo_BaoCaoDungHan()
    {
        var f = await ArrangeAsync();
        var px1 = await ReadAsync<DepartmentDto>(await f.Client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("PX1", "Phân xưởng 1", null), Ct));
        var approver = await ApproverAsync(f, await RoleAsync());

        // Tham số SLA ở màn hình Tham số hệ thống: mặc định 1/5/1, sửa được 0–60.
        var parameters = await ReadAsync<List<SystemParameterDto>>(await f.Client.GetAsync("/api/system-parameters", Ct));
        Assert.Equal(("1", "5", "1"), (parameters.Single(p => p.Code == "SLA_PRICED_DAYS").Value, parameters.Single(p => p.Code == "SLA_SOURCING_DAYS").Value,
            parameters.Single(p => p.Code == "SLA_URGENT_DAYS").Value));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest,
            (await f.Client.PutAsJsonAsync("/api/system-parameters/SLA_SOURCING_DAYS", new UpdateSystemParameterRequest("-1"), Ct)).StatusCode);
        Assert.Equal("3", (await ReadAsync<SystemParameterDto>(await f.Client.PutAsJsonAsync("/api/system-parameters/SLA_SOURCING_DAYS",
            new UpdateSystemParameterRequest("3"), Ct))).Value);

        // PR1: hàng có bảng giá → loại Priced; PR2 khẩn chỉ có hàng mới → Urgent, chưa có PO.
        async Task<PurchaseRequestDto> ApprovedPrAsync(SavePurchaseRequestRequest request)
        {
            var created = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync("/api/purchase-requests", request, Ct));
            await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsync($"/api/purchase-requests/{created.Id}/submit", null, Ct));
            return await ReadAsync<PurchaseRequestDto>(await approver.PostAsJsonAsync($"/api/purchase-requests/{created.Id}/approve", new ApprovalActionRequest(null), Ct));
        }

        var pr1 = await ApprovedPrAsync(new SavePurchaseRequestRequest(Today, px1.Id, "Bảo trì", null, f.WarehouseId, "Normal", null,
            [new(null, f.ItemId, f.Cai, 10, null, null, null)]));
        var pr2 = await ApprovedPrAsync(new SavePurchaseRequestRequest(Today, px1.Id, "Sự cố", null, f.WarehouseId, "Urgent", null, [],
            [new(null, "Phớt chặn dầu đặc chủng", null, f.Cai, 2, 0, null, null)]));

        // PO từ PR1 được duyệt ngay hôm nay → đúng hạn; dòng PR khác cùng mặt hàng thấy "đang đặt" 10.
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            Po(f, f.SupplierId, new SavePurchaseOrderLineRequest(null, f.ItemId, f.Cai, 10, 50_000, 5, "10", null, null, pr1.Lines[0].Id)), Ct));
        await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Approved", po.Status);
        var draft = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, px1.Id, "Kiểm tra", null, f.WarehouseId, "Normal", null, [new(null, f.ItemId, f.Cai, 1, null, null, null)]), Ct));
        Assert.Equal(10m, draft.Lines[0].OnOrderQty);

        var report = await ReadAsync<PurchaseSlaResult>(await f.Client.GetAsync($"/api/reports/purchase-sla?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        var r1 = report.Rows.Single(r => r.RequestId == pr1.Id);
        Assert.Equal(("Priced", 1, po.No, "OnTime"), (r1.Category, r1.SlaDays, r1.OrderNo, r1.Status));
        var r2 = report.Rows.Single(r => r.RequestId == pr2.Id);
        Assert.Equal(("Urgent", "(hàng mới)", "InProgress"), (r2.Category, r2.ItemCode, r2.Status));
        Assert.Equal((1, 0, 0, 100m), (report.OnTime, report.Late, report.Overdue, report.OnTimePct!.Value));
    }

    [Fact]
    public async Task HangMoiChuaCoMa_DuyetCungPr_TaoPhieuTimNguon_GanMaThiLenPo()
    {
        var f = await ArrangeAsync();
        var px1 = await ReadAsync<DepartmentDto>(await f.Client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("PX1", "Phân xưởng 1", null), Ct));
        var approver = await ApproverAsync(f, await RoleAsync());

        // PR chỉ có hàng mới chưa có mã: tổng = 4 × 250.000 = 1.000.000; chưa duyệt thì chưa tìm nguồn được.
        var pr = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, px1.Id, "Thay thế linh kiện máy mới", Today.AddDays(10), f.WarehouseId, "Normal", null, [],
                [new(null, "Vòng bi gốm chịu nhiệt 6207-CE", "Gốm Si3N4, 400°C", f.Cai, 4, 250_000, Today.AddDays(10), null)]), Ct));
        Assert.Equal((0, 1, 1_000_000m), (pr.Lines.Count, pr.NewItems.Count, pr.TotalAmount));
        var newItem = pr.NewItems[0];
        Assert.Contains("chưa được duyệt", await TitleAsync(await f.Client.PostAsync($"/api/purchase-requests/{pr.Id}/new-items/{newItem.Id}/sourcing", null, Ct)));
        await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await approver.PostAsJsonAsync($"/api/purchase-requests/{pr.Id}/approve", new ApprovalActionRequest(null), Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await f.Client.GetAsync($"/api/purchase-requests/{pr.Id}", Ct));
        Assert.Equal(("Approved", true, true), (pr.Status, pr.CanCreateSourcing, pr.CanLinkItem));
        // Chưa có mã thì không lên PO: không có dòng mở để lấy.
        var open = await ReadAsync<List<OpenRequestLineDto>>(await f.Client.GetAsync("/api/purchase-requests/open-lines", Ct));
        Assert.DoesNotContain(open, l => l.RequestId == pr.Id);

        // Lập phiếu tìm nguồn từ dòng hàng mới; lần hai báo đã có.
        pr = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsync($"/api/purchase-requests/{pr.Id}/new-items/{newItem.Id}/sourcing", null, Ct));
        Assert.NotNull(pr.NewItems[0].SourcingNo);
        var sourcing = await ReadAsync<SourcingDto>(await f.Client.GetAsync($"/api/sourcing/{pr.NewItems[0].SourcingRequestId}", Ct));
        Assert.Equal(("Vòng bi gốm chịu nhiệt 6207-CE", (Guid?)null), (sourcing.Description, sourcing.ItemId));
        Assert.Contains(pr.No, sourcing.Note ?? "");
        Assert.Contains("đã có phiếu tìm nguồn", await TitleAsync(await f.Client.PostAsync($"/api/purchase-requests/{pr.Id}/new-items/{newItem.Id}/sourcing", null, Ct)));

        // Gắn mã: sinh dòng có mã cùng SL, ĐVT, giá dự kiến; tổng không đổi; lên PO được.
        pr = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync($"/api/purchase-requests/{pr.Id}/new-items/{newItem.Id}/link-item",
            new LinkNewItemRequest(f.ItemId), Ct));
        Assert.Equal((1, 1_000_000m, 4m, 250_000m), (pr.Lines.Count, pr.TotalAmount, pr.Lines[0].Qty, pr.Lines[0].EstPrice));
        Assert.NotNull(pr.NewItems[0].ItemCode);
        Assert.Contains("đã gắn mã", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/purchase-requests/{pr.Id}/new-items/{newItem.Id}/link-item",
            new LinkNewItemRequest(f.ItemId), Ct)));
        open = await ReadAsync<List<OpenRequestLineDto>>(await f.Client.GetAsync("/api/purchase-requests/open-lines", Ct));
        Assert.Equal(4m, open.Single(l => l.RequestId == pr.Id).RemainingQty);
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            Po(f, f.SupplierId, new SavePurchaseOrderLineRequest(null, f.ItemId, f.Cai, 4, 50_000, 5, "10", null, null, pr.Lines[0].Id)), Ct));
        Assert.Equal(pr.No, po.Lines[0].SourcePrNo);
    }

    [Fact]
    public async Task MuaKhanCap_DuyetTheoCapPok_KhongLapLuiNgay_BoSungHoSoSau()
    {
        var f = await ArrangeAsync();
        var urgentRole = await RoleAsync();
        await ReadAsync<ApprovalRuleDto>(await f.Client.PostAsJsonAsync("/api/approval-rules",
            new SaveApprovalRuleRequest("POK", 1, 0, await _factory.GetRoleIdAsync(urgentRole)), Ct));
        var emergency = Po(f, f.SupplierId, Line(f, 10, 50_000, 5)) with { PurchaseType = "Emergency", EmergencyReason = "Hỏng vòng bi máy cán", DocsDueDate = Today.AddDays(7) };

        Assert.Contains("lý do khẩn cấp", await TitleAsync(await f.Client.PostAsJsonAsync("/api/purchase-orders", emergency with { EmergencyReason = null }, Ct)));
        // Lập lùi ngày: lưu nháp được, gửi duyệt bị chặn (không hợp thức hóa hàng đã mua).
        var backdated = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            emergency with { Date = Today.AddDays(-1), DocsDueDate = Today.AddDays(5) }, Ct));
        Assert.Contains("không được lập lùi ngày", await TitleAsync(await f.Client.PostAsync($"/api/purchase-orders/{backdated.Id}/submit", null, Ct)));

        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", emergency, Ct));
        Assert.Equal(("Emergency", "Hỏng vòng bi máy cán", (DateOnly?)Today.AddDays(7), false), (po.PurchaseType, po.EmergencyReason, po.DocsDueDate, po.CanCompleteDocs));
        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        // Duyệt theo cấp POK (vai trò riêng), không phải "ai có quyền duyệt PO".
        Assert.NotNull(Assert.Single(po.Approvals).RoleName);
        var urgentApprover = await ApproverAsync(f, urgentRole);
        po = await ReadAsync<PurchaseOrderDto>(await urgentApprover.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal(("Approved", false), (po.Status, po.CanCompleteDocs));
        // Người lập PO (quyền lập) mới xác nhận bổ sung hồ sơ.
        po = await ReadAsync<PurchaseOrderDto>(await f.Client.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal((true, false), (po.CanCompleteDocs, po.IsDocsOverdue));

        var list = await ReadAsync<PurchaseDocumentListResult>(await f.Client.GetAsync("/api/purchase-orders", Ct));
        Assert.Equal(("Emergency", (bool?)false), (list.Items.Single(i => i.Id == po.Id).PurchaseType, list.Items.Single(i => i.Id == po.Id).DocsOverdue));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest,
            (await f.Client.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/complete-docs", new CompleteDocsRequest(null), Ct)).StatusCode);
        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/complete-docs",
            new CompleteDocsRequest("Biên bản sự cố, 3 báo giá so sánh"), Ct));
        Assert.Equal(("Biên bản sự cố, 3 báo giá so sánh", false), (po.DocsCompletionNote, po.CanCompleteDocs));
        Assert.NotNull(po.DocsCompletedAt);

        // PO thường không dùng cấp POK: chưa cấu hình cấp PO → một cấp, ai có quyền duyệt.
        var normal = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", Po(f, f.SupplierId, Line(f, 1, 50_000, 5)), Ct));
        normal = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{normal.Id}/submit", null, Ct));
        Assert.Equal(("Normal", (string?)null), (normal.PurchaseType, Assert.Single(normal.Approvals).RoleName));
    }

    [Fact]
    public async Task BoPhanNgoaiPhamViHopDong_KhongTinhGiaHopDong_GhiNhanNccDocQuyen()
    {
        var f = await ArrangeAsync();
        var px1 = await ReadAsync<DepartmentDto>(await f.Client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("PX1", "Phân xưởng 1", null), Ct));
        var px2 = await ReadAsync<DepartmentDto>(await f.Client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("PX2", "Phân xưởng 2", null), Ct));
        var approver = await ApproverAsync(f, await RoleAsync());

        // Hợp đồng chỉ cho PX1 đặt hàng.
        var contract = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", new SaveContractRequest(
            f.SupplierId, f.PriceListId, "HĐNT 03/2026", "Hợp đồng vòng bi PX1", Today, Today, Today.AddMonths(6), null, null, null, null, null, null, null,
            null, null, [px1.Id], [new SaveContractLineRequest(null, f.ItemId, f.Cai, 1_000, null, null)]), Ct));
        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.Id}/submit", null, Ct));
        contract = await ReadAsync<ContractDto>(await approver.PostAsJsonAsync($"/api/contracts/{contract.Id}/approve", new ApprovalActionRequest(null), Ct));

        // NCC độc quyền đã duyệt cho mặt hàng.
        var sole = await ReadAsync<SoleSourceDto>(await f.Client.PostAsJsonAsync("/api/sole-sources",
            new SaveSoleSourceRequest(f.SupplierId, f.ItemId, "Chỉ hãng này sản xuất vòng bi 6207 chịu nhiệt", Today, Today.AddMonths(12)), Ct));
        await ReadAsync<SoleSourceDto>(await f.Client.PostAsync($"/api/sole-sources/{sole.Id}/submit", null, Ct));
        sole = await ReadAsync<SoleSourceDto>(await approver.PostAsJsonAsync($"/api/sole-sources/{sole.Id}/approve", new ApprovalActionRequest(null), Ct));

        // PX1: giá hợp đồng; PX2 và chưa chọn bộ phận: bảng giá + ghi chú phạm vi. Mọi dòng ghi nhận phiếu độc quyền.
        var po1 = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", Po(f, f.SupplierId, Line(f, 10, 50_000, 5)) with { DepartmentId = px1.Id }, Ct));
        Assert.Equal(("Contract", "PX1 – Phân xưởng 1", (string?)null, sole.No), (po1.Lines[0].PriceSource, po1.DepartmentName, po1.ContractScopeNote, po1.Lines[0].SoleSourceNo));
        var po2 = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", Po(f, f.SupplierId, Line(f, 10, 50_000, 5)) with { DepartmentId = px2.Id }, Ct));
        Assert.Equal(("PriceList", sole.No), (po2.Lines[0].PriceSource, po2.Lines[0].SoleSourceNo));
        Assert.Equal($"Hợp đồng {contract.No} chỉ cho bộ phận PX1 đặt hàng – đơn của bộ phận PX2 không tính giá theo hợp đồng.", po2.ContractScopeNote);
        var po3 = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", Po(f, f.SupplierId, Line(f, 10, 50_000, 5)), Ct));
        Assert.Equal("PriceList", po3.Lines[0].PriceSource);
        Assert.Contains("đơn chưa chọn bộ phận đặt hàng", po3.ContractScopeNote);
        var prices = await ReadAsync<List<PoApprovedPriceDto>>(await f.Client.GetAsync(
            $"/api/purchase-orders/approved-prices?supplierId={f.SupplierId}&date={Today:yyyy-MM-dd}&itemIds={f.ItemId}&departmentId={px2.Id}", Ct));
        Assert.All(prices, p => Assert.Equal("PriceList", p.Source));

        // Dòng từ PR: PO lấy bộ phận của PR; PR khác bộ phận đã chọn thì chặn.
        var pr = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, px1.Id, "Bảo trì", Today.AddDays(7), f.WarehouseId, "Normal", null, [new(null, f.ItemId, f.Cai, 20, null, null, null)]), Ct));
        await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await approver.PostAsJsonAsync($"/api/purchase-requests/{pr.Id}/approve", new ApprovalActionRequest(null), Ct));
        var fromPr = Po(f, f.SupplierId, new SavePurchaseOrderLineRequest(null, f.ItemId, f.Cai, 5, 50_000, 5, "10", null, null, pr.Lines[0].Id));
        Assert.Contains("khác bộ phận đặt hàng PX2", await TitleAsync(await f.Client.PostAsJsonAsync("/api/purchase-orders", fromPr with { DepartmentId = px2.Id }, Ct)));
        var po4 = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", fromPr, Ct));
        Assert.Equal((px1.Id, "Contract"), (po4.DepartmentId!.Value, po4.Lines[0].PriceSource));
    }

    [Fact]
    public async Task NguonGia_BangGia_HopDong_NgoaiHopDong()
    {
        var f = await ArrangeAsync();
        var prices = await ReadAsync<List<PoApprovedPriceDto>>(await f.Client.GetAsync(
            $"/api/purchase-orders/approved-prices?supplierId={f.SupplierId}&date={Today:yyyy-MM-dd}&itemIds={f.ItemId}", Ct));
        Assert.Equal(3, prices.Count);
        Assert.All(prices, p => Assert.Equal(("PriceList", f.PriceListNo, (string?)null), (p.Source, p.PriceListNo, p.ContractNo)));

        // Bảng giá: đúng giá bậc 0 cái; 1.200 cái lấy bậc 1.000 (48.000); 2 thùng có bậc riêng 600.000. Giá 47.500 CK 0% = 50.000 CK 5%.
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            Po(f, f.SupplierId, Line(f, 10, 50_000, 5), Line(f, 1_200, 48_000, 5), Line(f, 2, 600_000, 0, uomId: f.Thung), Line(f, 5, 47_500, 0, "Không cần")), Ct));
        Assert.All(po.Lines, l => Assert.Equal(("PriceList", f.PriceListNo, false, false), (l.PriceSource, l.PriceListNo, l.IsPriceDeviation, l.IsOutOfContract)));
        Assert.Equal([50_000m, 48_000m, 600_000m, 50_000m], po.Lines.Select(l => l.ApprovedUnitPrice!.Value));
        Assert.Null(po.Lines[3].PriceDeviationReason);
        Assert.Equal((false, false), (po.HasPriceException, po.IsOutOfContract));

        // NCC không có bảng giá: ngoài hợp đồng, không phải ngoại lệ giá (không có giá đã duyệt để lệch).
        var outside = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", Po(f, f.NoPriceSupplierId, Line(f, 1, 70_000, 0)), Ct));
        Assert.Equal(("None", (decimal?)null, true), (outside.Lines[0].PriceSource, outside.Lines[0].ApprovedUnitPrice, outside.Lines[0].IsOutOfContract));
        Assert.Equal((false, true), (outside.HasPriceException, outside.IsOutOfContract));

        // Hợp đồng nguyên tắc hiệu lực dùng bảng giá đó → nguồn chuyển sang Hợp đồng khi lưu lại.
        var role = await RoleAsync();
        var contract = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", new SaveContractRequest(
            f.SupplierId, f.PriceListId, "HĐNT 02/2026", "Hợp đồng vòng bi", Today, Today, Today.AddMonths(6), null, null, null, null, null, null, null,
            null, null, [], [new SaveContractLineRequest(null, f.ItemId, f.Cai, 1_000, null, null)]), Ct));
        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.Id}/submit", null, Ct));
        var approver = await ApproverAsync(f, role);
        contract = await ReadAsync<ContractDto>(await approver.PostAsJsonAsync($"/api/contracts/{contract.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Active", contract.Status);

        prices = await ReadAsync<List<PoApprovedPriceDto>>(await f.Client.GetAsync(
            $"/api/purchase-orders/approved-prices?supplierId={f.SupplierId}&date={Today:yyyy-MM-dd}&itemIds={f.ItemId}", Ct));
        Assert.All(prices, p => Assert.Equal(("Contract", contract.No), (p.Source, p.ContractNo)));
        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PutAsJsonAsync($"/api/purchase-orders/{po.Id}",
            Po(f, f.SupplierId, po.Lines.Select(l => new SavePurchaseOrderLineRequest(l.Id, l.ItemId, l.UomId, l.Qty, l.UnitPrice, l.DiscountPct, l.VatCode,
                null, null, null)).ToArray()), Ct));
        Assert.All(po.Lines, l => Assert.Equal(("Contract", contract.No, f.PriceListNo), (l.PriceSource, l.ContractNo, l.PriceListNo)));

        // Danh sách PO có cờ ngoài hợp đồng / ngoại lệ giá.
        var listResult = await ReadAsync<PurchaseDocumentListResult>(await f.Client.GetAsync("/api/purchase-orders", Ct));
        Assert.Equal((false, false), (listResult.Items.Single(i => i.Id == po.Id).OutOfContract, listResult.Items.Single(i => i.Id == po.Id).PriceException));
        Assert.True(listResult.Items.Single(i => i.Id == outside.Id).OutOfContract);
    }

    [Fact]
    public async Task LechGia_BatBuocLyDo_QuaChangDuyetNgoaiLe()
    {
        var f = await ArrangeAsync();
        // Chặng ngoại lệ do vai trò riêng duyệt (cấu hình loại POX ở APRL).
        var exceptionRole = await RoleAsync();
        var types = await ReadAsync<List<ApprovalTypeDto>>(await f.Client.GetAsync("/api/approval-rules/types", Ct));
        Assert.Contains(types, t => t.Code == "POX");
        await ReadAsync<ApprovalRuleDto>(await f.Client.PostAsJsonAsync("/api/approval-rules",
            new SaveApprovalRuleRequest("POX", 1, 1_000_000_000, await _factory.GetRoleIdAsync(exceptionRole)), Ct));

        // Giá 52.000 CK 5% khác giá bảng giá 50.000 CK 5% → lệch giá, ngoài hợp đồng; lưu nháp được, gửi duyệt thiếu lý do bị chặn.
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            Po(f, f.SupplierId, Line(f, 10, 50_000, 5), Line(f, 10, 52_000, 5)), Ct));
        Assert.Equal([false, true], po.Lines.Select(l => l.IsPriceDeviation));
        Assert.Equal((true, true), (po.HasPriceException, po.IsOutOfContract));
        Assert.Contains("Dòng 2: đơn giá khác giá đã duyệt", await TitleAsync(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct)));

        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PutAsJsonAsync($"/api/purchase-orders/{po.Id}",
            Po(f, f.SupplierId, Line(f, 10, 50_000, 5), Line(f, 10, 52_000, 5, "NCC tăng giá thép, chờ đàm phán lại")), Ct));
        Assert.Equal("NCC tăng giá thép, chờ đàm phán lại", po.Lines[1].PriceDeviationReason);
        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        // Chưa cấu hình cấp PO thường → một cấp ai có quyền duyệt; chặng 2 ngoại lệ: dưới ngưỡng POX vẫn qua cấp thấp nhất.
        Assert.Equal([(1, 1, (string?)null), (2, 1, "pox")], po.Approvals.Select(s => (s.Stage, s.Level, s.RoleName is null ? null : "pox")));

        var plainRole = UniqueCode("P");
        await _factory.CreateRoleAsync(plainRole, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var plain = await ApproverAsync(f, plainRole);
        po = await ReadAsync<PurchaseOrderDto>(await plain.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Submitted", po.Status);
        Assert.Contains("vai trò", await TitleAsync(await plain.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct)));
        var exceptionApprover = await ApproverAsync(f, exceptionRole);
        po = await ReadAsync<PurchaseOrderDto>(await exceptionApprover.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest("Chấp nhận tạm"), Ct));
        Assert.Equal("Approved", po.Status);

        // Đúng giá đã duyệt thì lý do cũ bị xóa, không qua chặng ngoại lệ.
        var normal = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            Po(f, f.SupplierId, Line(f, 10, 50_000, 5, "Lý do thừa")), Ct));
        Assert.Null(normal.Lines[0].PriceDeviationReason);
        normal = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{normal.Id}/submit", null, Ct));
        Assert.Equal([1], normal.Approvals.Select(s => s.Stage));
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-31", "2026-09-01", "2026-09-30")]
    [InlineData("2026-10-01", "2026-12-31", "2026-07-01", "2026-09-30")]
    [InlineData("2026-03-01", "2026-03-31", "2026-02-01", "2026-02-28")]
    [InlineData("2026-11-09", "2026-11-15", "2026-11-02", "2026-11-08")]
    [InlineData("2026-10-05", "2026-10-31", "2026-09-08", "2026-10-04")]
    public void BienDongGia_KyTruoc_TronThangLuiTheoThang_ConLaiLuiTheoNgay(string from, string to, string prevFrom, string prevTo) =>
        Assert.Equal((DateOnly.Parse(prevFrom), DateOnly.Parse(prevTo)),
            Proc.Infrastructure.Reports.PurchaseReportService.PreviousPeriod(DateOnly.Parse(from), DateOnly.Parse(to)));

    [Fact]
    public async Task BaoCaoTuan_GiaMuaNgoaiHopDong_BienDongGia()
    {
        var f = await ArrangeAsync();
        var approver = await ApproverAsync(f, await RoleAsync());
        async Task<PurchaseOrderDto> ApprovedAsync(SavePurchaseOrderRequest request)
        {
            var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", request, Ct));
            po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
            // PO lệch giá có thêm chặng ngoại lệ (chưa cấu hình POX → một cấp, ai có quyền duyệt PO): duyệt đến khi xong.
            while (po.Status == "Submitted")
                po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
            Assert.Equal("Approved", po.Status);
            return po;
        }

        // Kỳ này (hôm nay): NCC A theo bảng giá 47.500 sau CK (475.000) + lệch giá 49.400 (494.000); NCC C không có giá 60.000 × 5 (300.000).
        var po1 = await ApprovedAsync(Po(f, f.SupplierId, Line(f, 10, 50_000, 5), Line(f, 10, 52_000, 5, "NCC tăng giá thép")));
        await ApprovedAsync(Po(f, f.NoPriceSupplierId, Line(f, 5, 60_000, 0)));
        // PO nháp không tính; kỳ trước (hôm qua): 10 cái 45.000.
        await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", Po(f, f.SupplierId, Line(f, 100, 10_000, 0)), Ct));
        await ApprovedAsync(Po(f, f.NoPriceSupplierId, Line(f, 10, 45_000, 0)) with { Date = Today.AddDays(-1) });

        var report = await ReadAsync<ContractComplianceResult>(await f.Client.GetAsync(
            $"/api/reports/contract-compliance?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        Assert.Equal((2, 1_269_000m, 37.43m), (report.OrderCount, report.TotalAmount, report.CompliancePct));
        Assert.Equal([("PriceList", 475_000m), ("Deviation", 494_000m), ("None", 300_000m)], report.Sources.Select(s => (s.Source, s.Amount)));
        var deviation = report.Lines[0];
        Assert.Equal((po1.No, "Deviation", 49_400m, 47_500m, 4.00m, 19_000m, "NCC tăng giá thép"),
            (deviation.OrderNo, deviation.Kind, deviation.NetUnitPrice, deviation.ApprovedNetPrice, deviation.DeviationPct, deviation.DeviationAmount, deviation.Reason));
        Assert.Equal(("None", (decimal?)null, 300_000m), (report.Lines[1].Kind, report.Lines[1].ApprovedNetPrice, report.Lines[1].Amount));
        Assert.Equal(2, report.Lines.Count);

        var variance = await ReadAsync<PriceVarianceResult>(await f.Client.GetAsync(
            $"/api/reports/price-variance?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        Assert.Equal((Today.AddDays(-1), Today.AddDays(-1), 144_000m), (variance.PreviousFrom, variance.PreviousTo, variance.ImpactAmount));
        var row = Assert.Single(variance.Items);
        Assert.Equal((10m, 45_000m, 25m, 50_760m, 47_500m, 60_000m, 12.80m, 2, 2),
            (row.PreviousQty, row.PreviousAvgPrice, row.CurrentQty, row.CurrentAvgPrice, row.MinPrice, row.MaxPrice, row.ChangePct, row.OrderCount, row.SupplierCount));

        // Xem toàn tập đoàn cần quyền riêng.
        var plainRole = UniqueCode("V");
        await _factory.CreateRoleAsync(plainRole, Permissions.Report.PurchaseView);
        var viewer = await ApproverAsync(f, plainRole);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await viewer.GetAsync(
            $"/api/reports/contract-compliance?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&allCompanies=true", Ct)).StatusCode);
    }

    [Fact]
    public async Task BangKpi_TheoKy_SoKyTruoc_MucTieuCauHinhDuoc()
    {
        var f = await ArrangeAsync();
        var approver = await ApproverAsync(f, await RoleAsync());
        async Task ApprovedAsync(SavePurchaseOrderRequest request)
        {
            var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", request, Ct));
            po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
            while (po.Status == "Submitted")
                po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
            Assert.Equal("Approved", po.Status);
        }

        // Kỳ này: theo bảng giá 475.000 + lệch giá 494.000; PO khẩn không có giá 300.000. Kỳ trước (hôm qua): PO thường không có giá 450.000.
        await ApprovedAsync(Po(f, f.SupplierId, Line(f, 10, 50_000, 5), Line(f, 10, 52_000, 5, "NCC tăng giá thép")));
        await ApprovedAsync(Po(f, f.NoPriceSupplierId, Line(f, 5, 60_000, 0)) with
        {
            PurchaseType = "Emergency", EmergencyReason = "Hỏng vòng bi máy cán", DocsDueDate = Today.AddDays(7),
        });
        await ApprovedAsync(Po(f, f.NoPriceSupplierId, Line(f, 10, 45_000, 0)) with { Date = Today.AddDays(-1) });

        var url = $"/api/reports/kpi?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var kpi = await ReadAsync<KpiResult>(await f.Client.GetAsync(url, Ct));
        Assert.Equal((Today.AddDays(-1), Today.AddDays(-1)), (kpi.PreviousFrom, kpi.PreviousTo));
        Assert.Equal(
            ["Saving", "ContractCompliance", "PurchaseSla", "OnTimeDelivery", "Quality", "Emergency", "SlowMoving", "SupplierReview", "Violation"],
            kpi.Items.Select(i => i.Code));
        var contract = kpi.Items.Single(i => i.Code == "ContractCompliance");
        // 475.000 ÷ 1.269.000 = 37,43% – khớp báo cáo PRCW; kỳ trước chỉ có dòng không có giá đã duyệt → 0%.
        Assert.Equal((37.43m, (decimal?)0m, (decimal?)475_000m, (decimal?)1_269_000m, 95m, (bool?)false),
            (contract.Value, contract.PreviousValue, contract.Numerator, contract.Denominator, contract.Target, contract.Met));
        var emergency = kpi.Items.Single(i => i.Code == "Emergency");
        // 300.000 ÷ 1.269.000 = 23,64% > trần 5%.
        Assert.Equal((23.64m, (decimal?)0m, 5m, (bool?)false), (emergency.Value, emergency.PreviousValue, emergency.Target, emergency.Met));
        // Chưa có RFQ, PR, phiếu nhập, tồn kho: chưa có kết quả; đánh giá NCC chưa đo (lát F4); chưa có vi phạm KSNB → 0 vụ, đạt.
        Assert.All(kpi.Items.Where(i => i.Code is "Saving" or "PurchaseSla" or "OnTimeDelivery" or "Quality" or "SlowMoving"),
            i => Assert.Equal((true, (decimal?)null, (bool?)null), (i.Measured, i.Value, i.Met)));
        Assert.False(kpi.Items.Single(i => i.Code == "SupplierReview").Measured);
        var violation = kpi.Items.Single(i => i.Code == "Violation");
        Assert.Equal((true, (decimal?)0m, (bool?)true), (violation.Measured, violation.Value, violation.Met));
        Assert.Equal((3, 1), (kpi.MeasuredCount, kpi.MetCount));

        // Mục tiêu sửa được ở Tham số hệ thống (nhóm KPI): hạ mục tiêu mua theo hợp đồng xuống 30% → đạt.
        var code = SystemParameterCodes.KpiTarget(KpiCode.ContractCompliance);
        var saved = await ReadAsync<SystemParameterDto>(await f.Client.PutAsJsonAsync($"/api/system-parameters/{code}", new UpdateSystemParameterRequest("30"), Ct));
        Assert.Equal(("KPI", "30", false), (saved.Group, saved.Value, saved.IsDefault));
        kpi = await ReadAsync<KpiResult>(await f.Client.GetAsync(url, Ct));
        Assert.Equal((30m, (bool?)true), (kpi.Items.Single(i => i.Code == "ContractCompliance").Target, kpi.Items.Single(i => i.Code == "ContractCompliance").Met));
        Assert.Equal(2, kpi.MetCount);
        Assert.Contains("0 đến 100%", await TitleAsync(await f.Client.PutAsJsonAsync(
            $"/api/system-parameters/{SystemParameterCodes.KpiTarget(KpiCode.Emergency)}", new UpdateSystemParameterRequest("150"), Ct)));
        Assert.Contains("số nguyên", await TitleAsync(await f.Client.PutAsJsonAsync(
            $"/api/system-parameters/{SystemParameterCodes.KpiTarget(KpiCode.Violation)}", new UpdateSystemParameterRequest("1,5"), Ct)));
        var parameters = (await f.Client.GetFromJsonAsync<List<SystemParameterDto>>("/api/system-parameters", Ct))!;
        Assert.Equal(10, parameters.Count(p => p.Group == "KPI"));
        Assert.Contains("trước hoặc bằng", await TitleAsync(await f.Client.GetAsync(
            $"/api/reports/kpi?from={Today:yyyy-MM-dd}&to={Today.AddDays(-1):yyyy-MM-dd}", Ct)));
    }
}
