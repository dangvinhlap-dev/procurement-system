using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Gói báo cáo tháng cho Ban (lát G12, design.md mục 18.25): mỗi mục khớp đúng báo cáo chi tiết; mục thiếu quyền để trống kèm ghi chú;
/// kiểm soát nội bộ chỉ hiện với người có quyền KSNB; xuất Excel một sheet mỗi mục.
/// </summary>
[Collection(ApiCollection.Name)]
public class BoardMonthlyReportTests
{
    private const string Url = "/api/reports/board-monthly";
    private readonly ProcApiFactory _factory;

    public BoardMonthlyReportTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task BaoCaoThangChoBan_KhopBaoCaoChiTiet_MucThieuQuyenDeTrong_XuatExcel()
    {
        var userName = $"g12.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("B"), "Công ty thử báo cáo tháng Ban", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm thử báo cáo tháng {Guid.NewGuid():N}", null, null), Ct));
        var item = (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bóng đèn G12", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC báo cáo tháng G12", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;

        // Người duyệt PO luân phiên (mỗi chặng một người – lát R3); PO không có giá đã duyệt qua thêm chặng mua ngoài danh sách (lát G3).
        var poRole = UniqueCode("D");
        await _factory.CreateRoleAsync(poRole, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        async Task<HttpClient> ApproverAsync()
        {
            var name = $"bd.{Guid.NewGuid():N}"[..16];
            await _factory.CreateScopedUserAsync(name, company.Code, poRole);
            return await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, company.Code);
        }

        HttpClient[] approvers = [await ApproverAsync(), await ApproverAsync(), await ApproverAsync()];
        var po = await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, supplier.Id, warehouse, null, null, null, null, 0, null, [new(null, item, cai, 10, 5_000, 0, "10", null, null, null)]), Ct));
        po = await ReadAsync<PurchaseOrderDto>(await client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        for (var i = 0; i < 5 && po.Status == "Submitted"; i++)
            po = await ReadAsync<PurchaseOrderDto>(await approvers[i % approvers.Length].PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Approved", po.Status);

        var to = new DateOnly(Today.Year, Today.Month, 1).AddMonths(1).AddDays(-1);
        var report = await ReadAsync<BoardMonthlyReportDto>(await client.GetAsync($"{Url}?year={Today.Year}&month={Today.Month}", Ct));
        var compliance = await ReadAsync<ContractComplianceResult>(await client.GetAsync(
            $"/api/reports/contract-compliance?from={Today:yyyy-MM-01}&to={to:yyyy-MM-dd}", Ct));

        // Tổng quan và dòng mua ngoài giá đã duyệt khớp PRCW: 10 × 5.000 = 50.000, không có giá đã duyệt → 0% theo giá đã duyệt.
        Assert.Equal((new DateOnly(Today.Year, Today.Month, 1), to, company.Code), (report.From, report.To, report.CompanyCode));
        var purchase = report.Purchase!;
        Assert.Equal((compliance.OrderCount, compliance.TotalAmount, compliance.CompliancePct), (purchase.OrderCount, purchase.TotalAmount, purchase.CompliancePct));
        Assert.Equal((1, 50_000m, (decimal?)0m, 50_000m), (purchase.OrderCount, purchase.TotalAmount, purchase.CompliancePct, purchase.OutOfContractAmount));
        var line = Assert.Single(report.OutOfContract!);
        Assert.Equal((po.No, "None", 1), (line.OrderNo, line.Kind, report.OutOfContractCount));
        // KPI "mua theo giá đã duyệt" cùng số với mục tổng quan.
        Assert.Equal(0m, report.Kpi!.Items.Single(k => k.Code == nameof(KpiCode.ContractCompliance)).Value);
        Assert.Equal((0, 0m), (report.Savings!.RfqCount, report.Savings.SourcingSaving));
        Assert.NotNull(report.PriceVariance);
        Assert.NotNull(report.VolumeRebates);
        Assert.NotNull(report.SupplierCoverage);
        Assert.NotNull(report.Onboarding);
        // Quản trị có quyền KSNB nên thấy mục kiểm soát nội bộ.
        Assert.NotNull(report.Control);
        Assert.Equal(
            [BoardReportSections.Purchase, BoardReportSections.Kpi, BoardReportSections.Savings, BoardReportSections.OutOfContract,
             BoardReportSections.PriceVariance, BoardReportSections.VolumeRebates, BoardReportSections.SupplierCoverage, BoardReportSections.Onboarding,
             BoardReportSections.Control],
            report.Sections.Select(s => s.Code));
        Assert.All(report.Sections, s => Assert.True(s.Available));

        // Người chỉ có quyền báo cáo mua: mục CKB, NCC đủ điều kiện, hồ sơ NCC mới để trống kèm ghi chú; không có mục KSNB; không xem toàn tập đoàn.
        var viewerRole = UniqueCode("V");
        await _factory.CreateRoleAsync(viewerRole, Permissions.Report.PurchaseView);
        var viewerName = $"bv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, company.Code, viewerRole);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, company.Code);
        var limited = await ReadAsync<BoardMonthlyReportDto>(await viewer.GetAsync($"{Url}?year={Today.Year}&month={Today.Month}", Ct));
        Assert.Equal(purchase.TotalAmount, limited.Purchase!.TotalAmount);
        Assert.Equal((null, null, null, null), ((object?)limited.VolumeRebates, (object?)limited.SupplierCoverage, (object?)limited.Onboarding, (object?)limited.Control));
        Assert.Equal(
            [BoardReportSections.VolumeRebates, BoardReportSections.SupplierCoverage, BoardReportSections.Onboarding],
            limited.Sections.Where(s => !s.Available).Select(s => s.Code));
        Assert.DoesNotContain(limited.Sections, s => s.Code == BoardReportSections.Control);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"{Url}?year={Today.Year}&month={Today.Month}&allCompanies=true", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Url}?year={Today.Year}&month=13", Ct)).StatusCode);

        // Excel: sheet Tổng quan + mỗi mục có số liệu một sheet; người thiếu quyền không có sheet của mục đó.
        var file = await client.GetAsync($"{Url}/excel?year={Today.Year}&month={Today.Month}", Ct);
        Assert.True(file.IsSuccessStatusCode);
        Assert.Equal($"BaoCaoThangBan_{Today.Year}-{Today.Month:00}_{company.Code}.xlsx", file.Content.Headers.ContentDisposition!.FileNameStar);
        using (var workbook = new XLWorkbook(await file.Content.ReadAsStreamAsync(Ct)))
        {
            Assert.Equal(
                ["Tổng quan", "KPI", "Mua ngoài giá duyệt", "Biến động giá", "Chiết khấu gộp", "NCC đủ điều kiện", "Hồ sơ NCC mới", "Kiểm soát nội bộ"],
                workbook.Worksheets.Select(w => w.Name));
            var sheet = workbook.Worksheet("Mua ngoài giá duyệt");
            Assert.Equal(po.No, sheet.Cell(3, 1).GetString());
            // Ô số giữ kiểu số: SL 10 (cột 6), giá thực 5.000 (cột 8).
            Assert.Equal((10m, 5_000m), (sheet.Cell(3, 6).GetValue<decimal>(), sheet.Cell(3, 8).GetValue<decimal>()));
        }

        using var limitedBook = new XLWorkbook(await (await viewer.GetAsync($"{Url}/excel?year={Today.Year}&month={Today.Month}", Ct)).Content.ReadAsStreamAsync(Ct));
        Assert.Equal(["Tổng quan", "KPI", "Mua ngoài giá duyệt", "Biến động giá"], limitedBook.Worksheets.Select(w => w.Name));
    }
}
