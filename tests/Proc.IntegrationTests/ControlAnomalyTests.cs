using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Control;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Notifications;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Kiểm soát nội bộ – dấu hiệu bất thường (lát F3, design.md mục 18.19).
/// </summary>
[Collection(ApiCollection.Name)]
public class ControlAnomalyTests
{
    private readonly ProcApiFactory _factory;

    public ControlAnomalyTests(ProcApiFactory factory) => _factory = factory;

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
        HttpClient Buyer, string CompanyCode, Guid Kt, Guid Item1, Guid Item2, Guid Cai, SupplierDetailDto SupplierA, Guid SupplierB, string UserName);

    /// <summary>
    /// Công ty mới (kỳ tháng hiện tại mở), hai mặt hàng (cái), NCC A (có tài khoản ngân hàng) và NCC B đã duyệt, áp dụng tại công ty; không có
    /// bảng giá – mọi dòng PO là "không có giá đã duyệt". Người lập: người dùng quản trị mới.
    /// </summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"ks.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("K"), "Công ty thử KSNB", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var calendar = await ReadAsync<InventoryCalendarDto>(await buyer.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(Today.Year), Ct));
        await ReadAsync<InventoryPeriodDto>(await buyer.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == Today.Month).Id}/open", null, Ct));
        var kt = (await buyer.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;

        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Nhóm KSNB", null, null), Ct));
        async Task<Guid> ItemAsync(string name) => (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<SupplierDetailDto> SupplierAsync(string name)
        {
            var s = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [],
                    [new(null, "Vietcombank", null, "0011 2233", null, true)]), Ct));
            await _factory.FinishSupplierApprovalAsync(s.Id);
            await ReadAsync<SupplierCompanyDto>(await buyer.PutAsJsonAsync($"/api/suppliers/{s.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return (await buyer.GetFromJsonAsync<SupplierDetailDto>($"/api/suppliers/{s.Id}", Ct))!;
        }

        var a = await SupplierAsync("NCC KSNB A");
        var b = await SupplierAsync("NCC KSNB B");
        return new Fixture(buyer, company.Code, kt, await ItemAsync("Bạc đạn KSNB"), await ItemAsync("Dầu thủy lực KSNB"), cai, a, b.Id, userName);
    }

    [Fact]
    public async Task DauHieuBatThuong_GiaCao_SatNguong_DoiTaiKhoan_NgoaiHopDong_HoaDon_XungDotLoiIch()
    {
        var f = await ArrangeAsync();
        var roleCode = UniqueCode("D");
        var roleId = await _factory.CreateRoleAsync(roleCode, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var approverName = $"dy.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(approverName, f.CompanyCode, roleCode);
        var approver = await _factory.CreateClientInCompanyAsync(approverName, ProcApiFactory.TestPassword, f.CompanyCode);
        // Quy trình duyệt PO: cấp 1 từ 0 đ, cấp 2 từ 1.000.000 đ (gồm VAT).
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, roleId), Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 2, 1_000_000, roleId), Ct));

        async Task<PurchaseOrderDto> ApprovedAsync(Guid supplierId, Guid itemId, decimal qty, decimal price, decimal discount)
        {
            var po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-orders",
                new SavePurchaseOrderRequest(Today, supplierId, f.Kt, null, null, null, null, 0, null,
                    [new(null, itemId, f.Cai, qty, price, discount, "10", null, null, null)]), Ct));
            po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
            po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
            Assert.Equal("Approved", po.Status);
            return po;
        }

        // Mặt hàng 1: NCC A 2 PO × 5 cái giá ròng 95.000 (475.000 trước VAT); NCC B 1 PO × 5 cái 80.000 → A cao hơn 18,75%, chênh 150.000.
        var a1 = await ApprovedAsync(f.SupplierA.Id, f.Item1, 5, 100_000, 5);
        var a2 = await ApprovedAsync(f.SupplierA.Id, f.Item1, 5, 100_000, 5);
        await ApprovedAsync(f.SupplierB, f.Item1, 5, 80_000, 0);
        // Mặt hàng 2: 3 PO NCC B 820.000 + VAT = 902.000 – sát dưới ngưỡng cấp 2 (từ 900.000 đến dưới 1.000.000).
        var near = new List<PurchaseOrderDto>();
        for (var i = 0; i < 3; i++) near.Add(await ApprovedAsync(f.SupplierB, f.Item2, 1, 820_000, 0));

        // Phiếu nhập PO a1: giao 5, đạt 4, không đạt 1; hóa đơn tính đủ 5 cái (522.500) và ghi ngày trước ngày PO.
        var grn = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, f.Kt, f.SupplierA.Id, "0000777", "1C26TAA", Today.AddDays(-1), null, null, null,
                [new(null, f.Item1, f.Cai, 4, 1, null, null, null, null, a1.Lines[0].Id, 5, 1, "Failed", "1 cái vỡ")], a1.Id, 522_500), Ct));
        await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsync($"/api/inventory-documents/{grn.Id}/post", null, Ct));

        // Đổi tài khoản ngân hàng NCC A (qua duyệt), người lập PO kê khai quan hệ lợi ích với NCC B từ hôm nay.
        var supplier = f.SupplierA;
        await ReadAsync<SupplierDetailDto>(await f.Buyer.PutAsJsonAsync($"/api/suppliers/{supplier.Id}",
            new SaveSupplierRequest(null, supplier.GroupId, supplier.Name, supplier.ShortName, supplier.IsForeign, supplier.TaxCode, supplier.Address,
                supplier.Phone, supplier.Email, supplier.Website, supplier.PaymentTerms, supplier.SupplyScope, supplier.Rating, supplier.Status,
                supplier.StatusNote, supplier.InternalCompanyId, supplier.Contacts, [new(null, "ACB", null, "9988 7766", null, true)]), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<ConflictDeclarationDto>(await f.Buyer.PostAsJsonAsync("/api/conflict-declarations",
            new CreateConflictDeclarationRequest(null, f.SupplierB, "Family", "Em trai là giám đốc kinh doanh", Today), Ct));

        var url = $"/api/control/anomalies?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var result = await ReadAsync<AnomalyResult>(await f.Buyer.GetAsync(url, Ct));
        int Count(string code) => result.Summary.Single(s => s.Code == code).Count;
        Assert.Equal(
            [
                (AnomalyCodes.HighPriceRepeat, 1), (AnomalyCodes.NearThreshold, 1), (AnomalyCodes.BankAccountChange, 1), (AnomalyCodes.OffContract, 1),
                (AnomalyCodes.InvoiceShortage, 1), (AnomalyCodes.BackdatedOrder, 1), (AnomalyCodes.ConflictOfInterest, 4), (AnomalyCodes.UnrecordedRebate, 0),
            ],
            result.Summary.Select(s => (s.Code, s.Count)));
        Assert.True(result.Summary.Single(s => s.Code == AnomalyCodes.UnrecordedRebate).Measured);

        var price = result.Items.Single(i => i.Code == AnomalyCodes.HighPriceRepeat);
        Assert.Equal((AnomalySeverities.Medium, "PO", (decimal?)150_000m, f.SupplierA.Code), (price.Severity, price.ScreenCode, price.Amount, price.SupplierCode));
        Assert.Equal(new[] { a1.No, a2.No }.Max(), price.RefNo);
        Assert.Contains("18,75%", price.Detail);

        var nearRow = result.Items.Single(i => i.Code == AnomalyCodes.NearThreshold);
        Assert.Equal((AnomalySeverities.Medium, (decimal?)2_706_000m, near[^1].Id), (nearRow.Severity, nearRow.Amount, nearRow.TargetId));
        Assert.Contains("1.000.000", nearRow.Detail);

        var bank = result.Items.Single(i => i.Code == AnomalyCodes.BankAccountChange);
        Assert.Equal(("SUPAPR", f.SupplierA.Code), (bank.ScreenCode, bank.SupplierCode));
        Assert.Contains("ACB 99887766", bank.Detail);

        // Không có bảng giá: mọi dòng ngoài giá đã duyệt – 475.000 × 2 + 400.000 + 820.000 × 3 = 3.810.000 (100%).
        var off = result.Items.Single(i => i.Code == AnomalyCodes.OffContract);
        Assert.Equal((AnomalySeverities.High, (decimal?)3_810_000m, "PRCW"), (off.Severity, off.Amount, off.ScreenCode));
        Assert.Contains("100%", off.Detail);

        var shortage = result.Items.Single(i => i.Code == AnomalyCodes.InvoiceShortage);
        Assert.Equal((AnomalySeverities.High, "GRN", grn.Id, (decimal?)104_500m), (shortage.Severity, shortage.ScreenCode, shortage.TargetId, shortage.Amount));
        var backdated = result.Items.Single(i => i.Code == AnomalyCodes.BackdatedOrder);
        Assert.Equal((AnomalySeverities.High, grn.Id), (backdated.Severity, backdated.TargetId));
        Assert.Contains("hóa đơn", backdated.Detail);

        Assert.All(result.Items.Where(i => i.Code == AnomalyCodes.ConflictOfInterest),
            i => Assert.Equal((AnomalySeverities.High, f.CompanyCode), (i.Severity, i.CompanyCode)));
        // Mức cao lên đầu danh sách.
        Assert.Equal(AnomalySeverities.High, result.Items[0].Severity);

        // Ngưỡng chặt hơn: chênh giá từ 20% không còn dấu hiệu giá cao; cần 4 PO sát ngưỡng thì không còn dấu hiệu chia nhỏ đơn.
        var strict = await ReadAsync<AnomalyResult>(await f.Buyer.GetAsync(url + "&priceGapPct=20&nearThresholdCount=4", Ct));
        Assert.Equal((0, 0), (strict.Summary.Single(s => s.Code == AnomalyCodes.HighPriceRepeat).Count, strict.Summary.Single(s => s.Code == AnomalyCodes.NearThreshold).Count));
        Assert.Contains("1 năm", await TitleAsync(await f.Buyer.GetAsync($"/api/control/anomalies?from={Today.AddYears(-2):yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct)));

        // Quyền: không có quyền KSNB → 403; có quyền KSNB mà không có quyền toàn tập đoàn → 403 khi xem toàn tập đoàn.
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.GetAsync(url, Ct)).StatusCode);
        var ksnbRole = UniqueCode("K");
        await _factory.CreateRoleAsync(ksnbRole, Permissions.Control.AnomalyView);
        var ksnbName = $"kn.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(ksnbName, f.CompanyCode, ksnbRole);
        var ksnb = await _factory.CreateClientInCompanyAsync(ksnbName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.Equal(8, (await ReadAsync<AnomalyResult>(await ksnb.GetAsync(url, Ct))).Summary.Count);
        Assert.Equal(HttpStatusCode.Forbidden, (await ksnb.GetAsync(url + "&allCompanies=true", Ct)).StatusCode);
        // Toàn tập đoàn (quản trị): vẫn thấy dấu hiệu của công ty này.
        var all = await ReadAsync<AnomalyResult>(await f.Buyer.GetAsync(url + "&allCompanies=true", Ct));
        Assert.Contains(all.Items, i => i.Code == AnomalyCodes.InvoiceShortage && i.TargetId == grn.Id);
    }
    /// <summary>Người dùng mới tại công ty của fixture với vai trò mới có các quyền cho trước.</summary>
    private async Task<HttpClient> UserAsync(Fixture f, params string[] permissions)
    {
        var role = UniqueCode("R");
        await _factory.CreateRoleAsync(role, permissions);
        var name = $"u.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(name, f.CompanyCode, role);
        return await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, f.CompanyCode);
    }

    [Fact]
    public async Task SoViPham_KsnbGhiNhan_LanhDaoNhanThongBao_KetLuanKhoaPhieu_TinhKpi()
    {
        var f = await ArrangeAsync();
        var ksnb = await UserAsync(f, Permissions.Control.AnomalyView, Permissions.Control.FindingManage);
        var leader = await UserAsync(f, Permissions.Control.FindingReceive, Permissions.Report.PurchaseView);
        var staff = await UserAsync(f, Permissions.Purchase.PoView);
        var refId = Guid.NewGuid();
        var request = new SaveControlFindingRequest(Today, "Serious", "Chia nhỏ PO né cấp duyệt", "3 PO sát ngưỡng cùng NCC trong tuần", f.SupplierB,
            AnomalyCodes.NearThreshold, "PO", refId, "PO-THU-0003");

        Assert.Equal(HttpStatusCode.BadRequest, (await ksnb.PostAsJsonAsync("/api/control/findings", request with { Title = "" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leader.PostAsJsonAsync("/api/control/findings", request, Ct)).StatusCode);
        var created = await ReadAsync<ControlFindingDto>(await ksnb.PostAsJsonAsync("/api/control/findings", request, Ct));
        Assert.Matches($"^VP-{f.CompanyCode}-{Today:yyMM}-\\d{{4}}$", created.No);
        Assert.Equal(("Open", "Serious", refId, f.SupplierB), (created.Status, created.Severity, created.RefId, created.SupplierId));

        // Lãnh đạo nhận thông báo thẳng, mở màn hình sổ vi phạm; người ghi nhận không tự nhận thông báo của mình.
        var notice = Assert.Single((await leader.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Ct))!, n => n.TargetId == created.Id);
        Assert.Equal(("ControlFinding", "VIOL"), (notice.Type, notice.ScreenCode));
        Assert.Contains(created.No, notice.Title);
        Assert.DoesNotContain((await ksnb.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Ct))!, n => n.TargetId == created.Id);

        // Xem: KSNB và lãnh đạo; người khác không.
        Assert.Contains((await leader.GetFromJsonAsync<List<ControlFindingDto>>("/api/control/findings", Ct))!, x => x.Id == created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/control/findings", Ct)).StatusCode);

        var updated = await ReadAsync<ControlFindingDto>(await ksnb.PutAsJsonAsync($"/api/control/findings/{created.Id}",
            request with { Title = "Chia nhỏ PO – đã đối chiếu hồ sơ", RefId = null }, Ct));
        Assert.Equal(("Chia nhỏ PO – đã đối chiếu hồ sơ", (Guid?)refId), (updated.Title, updated.RefId));

        // KPI trước khi kết luận: chưa có vi phạm nghiêm trọng đã xác minh → 0 vụ, đạt.
        var kpiUrl = $"/api/reports/kpi?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var violation = (await ReadAsync<KpiResult>(await leader.GetAsync(kpiUrl, Ct))).Items.Single(i => i.Code == "Violation");
        Assert.Equal((true, (decimal?)0m, (decimal?)1m, (bool?)true), (violation.Measured, violation.Value, violation.Denominator, violation.Met));

        Assert.Contains("kết luận", await TitleAsync(await ksnb.PostAsJsonAsync($"/api/control/findings/{created.Id}/conclude",
            new ConcludeControlFindingRequest(" ", "Serious"), Ct)));
        var concluded = await ReadAsync<ControlFindingDto>(await ksnb.PostAsJsonAsync($"/api/control/findings/{created.Id}/conclude",
            new ConcludeControlFindingRequest("Xác minh: cố ý chia nhỏ đơn; đề nghị kỷ luật người lập", "Serious"), Ct));
        Assert.Equal(("Concluded", "Serious"), (concluded.Status, concluded.Severity));
        Assert.NotNull(concluded.ConcludedByName);
        Assert.Contains("đã kết luận", await TitleAsync(await ksnb.PutAsJsonAsync($"/api/control/findings/{created.Id}", request, Ct)));
        Assert.Equal(2, (await leader.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Ct))!.Count(n => n.TargetId == created.Id));

        violation = (await ReadAsync<KpiResult>(await leader.GetAsync(kpiUrl, Ct))).Items.Single(i => i.Code == "Violation");
        Assert.Equal(((decimal?)1m, (decimal?)0m, (bool?)false, "VIOL"), (violation.Value, violation.PreviousValue, violation.Met, violation.Report));
    }
}
