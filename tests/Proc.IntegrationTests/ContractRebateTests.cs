using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Control;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>Chiết khấu doanh số/tài trợ trên hợp đồng nguyên tắc và theo dõi thực nhận (lát F4b – design.md mục 18.21).</summary>
[Collection(ApiCollection.Name)]
public class ContractRebateTests
{
    private readonly ProcApiFactory _factory;

    public ContractRebateTests(ProcApiFactory factory) => _factory = factory;

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

    private sealed record Fixture(HttpClient Client, string CompanyCode, Guid ItemId, Guid Cai, Guid SupplierId, Guid PriceListId, Guid WarehouseId);

    /// <summary>Công ty mới; mặt hàng; NCC đã duyệt áp dụng tại công ty; bảng giá toàn tập đoàn của NCC đã hiệu lực (cái 50.000 CK 5%).</summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"ck.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("K"), "Công ty thử chiết khấu", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(head,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm chiết khấu {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Vòng bi 6208", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC chiết khấu", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var list = await ReadAsync<PriceListDto>(await head.PostAsJsonAsync("/api/price-lists",
            new SavePriceListRequest(supplier.Id, "Bảng giá chiết khấu", true, Today, Today.AddYears(1), null, null,
                [new(null, item.Id, cai, 0, 50_000, 5, null, null)]), Ct));
        await ReadAsync<PriceListDto>(await head.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        await _factory.FinishPriceListApprovalAsync(list.Id);
        var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        return new Fixture(client, company.Code, item.Id, cai, supplier.Id, list.Id, warehouse);
    }

    private async Task<HttpClient> UserAsync(Fixture f, params string[] permissions)
    {
        var role = UniqueCode("R");
        await _factory.CreateRoleAsync(role, permissions);
        var userName = $"dg.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, role);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);
    }

    [Fact]
    public async Task CamKetChietKhau_TheoDoiTheoKy_GhiNhanThucNhan_Huy()
    {
        var f = await ArrangeAsync();
        var approver = await UserAsync(f, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove, Permissions.Purchase.ContractView,
            Permissions.Purchase.ContractApprove);
        // Mỗi cấp/chặng một người duyệt khác nhau (lát R3, design.md mục 18.26): hai người cùng vai trò duyệt luân phiên.
        var approver2 = await UserAsync(f, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);

        // Hiệu lực từ 200 ngày trước: kỳ quý đầu tiên chắc chắn đã quá hạn ghi nhận (cuối kỳ + 30 ngày).
        var from = Today.AddDays(-200);
        var rebates = new List<SaveContractRebateRequest>
        {
            new(null, "SalesVolume", "Percent", 2, null, "Quarter", "Chiết khấu 2% doanh số mỗi quý"),
            new(null, "Sponsorship", "FixedAmount", 5_000_000, 1_000_000_000, "WholeContract", "Tài trợ hội nghị khi doanh số ≥ 1 tỷ"),
        };
        var request = new SaveContractRequest(
            f.SupplierId, f.PriceListId, "HĐNT 05/2026", "Hợp đồng vòng bi có chiết khấu", from, from, Today.AddDays(165), null, null, null, null,
            "2% doanh số quý", "Tài trợ hội nghị", null, null, null, [], [new SaveContractLineRequest(null, f.ItemId, f.Cai, 1_000, null, null)], rebates);
        Assert.Equal("Cam kết 1: cách tính không hợp lệ.", await TitleAsync(await f.Client.PostAsJsonAsync("/api/contracts",
            request with { Rebates = [rebates[0] with { Method = "Xyz" }] }, Ct)));
        var contract = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", request, Ct));
        Assert.Equal([("SalesVolume", "Percent", 2m, "Quarter"), ("Sponsorship", "FixedAmount", 5_000_000m, "WholeContract")],
            contract.Rebates.Select(r => (r.Kind, r.Method, r.Value, r.Period)));
        Assert.Equal(1_000_000_000m, contract.Rebates[1].MinPurchaseAmount);

        // Nháp: chưa theo dõi, chưa ghi nhận được.
        var tracking = await ReadAsync<ContractRebateTrackingDto>(await f.Client.GetAsync($"/api/contracts/{contract.Id}/rebates", Ct));
        Assert.Equal((0, false, 30), (tracking.Periods.Count, tracking.CanRecord, tracking.GraceDays));

        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.Id}/submit", null, Ct));
        contract = await ReadAsync<ContractDto>(await approver.PostAsJsonAsync($"/api/contracts/{contract.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Active", contract.Status);
        // Hiệu lực rồi thì cam kết không sửa được (đổi qua phụ lục – chưa hỗ trợ).
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PutAsJsonAsync($"/api/contracts/{contract.Id}", request with { Rebates = [] }, Ct)).StatusCode);

        // PO đã duyệt ngày đầu hiệu lực: 10 cái × 50.000 × 95% = 475.000; PO nháp không tính.
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(from, f.SupplierId, f.WarehouseId, null, null, null, null, 0, null,
                [new SavePurchaseOrderLineRequest(null, f.ItemId, f.Cai, 10, 50_000, 5, "10", null, null, null)]), Ct));
        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        for (var i = 0; po.Status == "Submitted"; i++)
            po = await ReadAsync<PurchaseOrderDto>(await (i % 2 == 0 ? approver : approver2).PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(from, f.SupplierId, f.WarehouseId, null, null, null, null, 0, null,
                [new SavePurchaseOrderLineRequest(null, f.ItemId, f.Cai, 100, 50_000, 0, "10", null, null, null)]), Ct));

        tracking = await ReadAsync<ContractRebateTrackingDto>(await f.Client.GetAsync($"/api/contracts/{contract.Id}/rebates", Ct));
        Assert.True(tracking.CanRecord);
        var first = tracking.Periods.First(p => p.RebateLineNo == 1);
        // 475.000 × 2% = 9.500; kỳ đã quá hạn ghi nhận → thiếu.
        Assert.Equal((from, 475_000m, 9_500m, 0m, 9_500m, "Short"),
            (first.PeriodFrom, first.PurchaseAmount, first.ExpectedAmount, first.ReceivedAmount, first.ShortAmount, first.Status));
        Assert.Equal(first.PeriodTo.AddDays(30), first.DueDate);
        // Các kỳ sau không có mua: phải nhận 0.
        Assert.All(tracking.Periods.Where(p => p.RebateLineNo == 1 && p.PeriodFrom > from), p => Assert.Equal(0m, p.ExpectedAmount));
        // Tài trợ cả hợp đồng: kỳ chưa kết thúc, chưa đạt 1 tỷ → phải nhận 0, chưa đến hạn.
        var sponsor = Assert.Single(tracking.Periods, p => p.RebateLineNo == 2);
        Assert.Equal((from, Today.AddDays(165), 0m, "NotDue"), (sponsor.PeriodFrom, sponsor.PeriodTo, sponsor.ExpectedAmount, sponsor.Status));

        // Ghi nhận: kỳ không hợp lệ bị chặn; ghi 4.000 rồi 5.500 → đủ; hủy lần đầu → thiếu lại 4.000.
        var rebateId = contract.Rebates[0].Id;
        Assert.Equal("Kỳ quyết toán không thuộc cam kết – hãy chọn kỳ trong danh sách.", await TitleAsync(await f.Client.PostAsJsonAsync(
            $"/api/contracts/{contract.Id}/rebate-receipts",
            new SaveContractRebateReceiptRequest(rebateId, from.AddDays(1), Today, 4_000, "DebtOffset", null, null), Ct)));
        tracking = await ReadAsync<ContractRebateTrackingDto>(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/rebate-receipts",
            new SaveContractRebateReceiptRequest(rebateId, from, Today, 4_000, "DebtOffset", "BB-01", null), Ct));
        Assert.Equal((4_000m, 5_500m, "Short"), (tracking.Periods[0].ReceivedAmount, tracking.Periods[0].ShortAmount, tracking.Periods[0].Status));
        tracking = await ReadAsync<ContractRebateTrackingDto>(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/rebate-receipts",
            new SaveContractRebateReceiptRequest(rebateId, from, Today, 5_500, "Transfer", "UNC-02", "Chuyển khoản bổ sung"), Ct));
        Assert.Equal((9_500m, 0m, "Received"), (tracking.Periods[0].ReceivedAmount, tracking.Periods[0].ShortAmount, tracking.Periods[0].Status));
        Assert.Equal(2, tracking.Receipts.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsJsonAsync(
            $"/api/contract-rebate-receipts/{tracking.Receipts[0].Id}/cancel", new CancelContractRebateReceiptRequest(" "), Ct)).StatusCode);
        tracking = await ReadAsync<ContractRebateTrackingDto>(await f.Client.PostAsJsonAsync(
            $"/api/contract-rebate-receipts/{tracking.Receipts[0].Id}/cancel", new CancelContractRebateReceiptRequest("Ghi trùng biên bản"), Ct));
        Assert.Equal((5_500m, 4_000m, "Short"), (tracking.Periods[0].ReceivedAmount, tracking.Periods[0].ShortAmount, tracking.Periods[0].Status));
        Assert.Equal((true, "Ghi trùng biên bản", "BB-01"), (tracking.Receipts[0].IsCancelled, tracking.Receipts[0].CancelReason, tracking.Receipts[0].RefNo));

        // Dấu hiệu KSNB (mục 18.19): kỳ quá hạn mà còn thiếu 4.000 → Trung bình (đã nhận một phần); khoảng xem phải chứa hạn ghi nhận.
        var ksnb = await UserAsync(f, Permissions.Control.AnomalyView);
        var due = first.DueDate;
        var anomalies = await ReadAsync<AnomalyResult>(await ksnb.GetAsync(
            $"/api/control/anomalies?from={due:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        var sign = Assert.Single(anomalies.Items, i => i.Code == AnomalyCodes.UnrecordedRebate);
        Assert.Equal((AnomalySeverities.Medium, "CTR", contract.Id, contract.No, 4_000m, due),
            (sign.Severity, sign.ScreenCode, sign.TargetId, sign.RefNo, sign.Amount, sign.Date));
        Assert.Contains("phải nhận 9.500", sign.Detail);
        Assert.DoesNotContain(anomalies.Items, i => i.Code == AnomalyCodes.UnrecordedRebate && i.Date < due);
        anomalies = await ReadAsync<AnomalyResult>(await ksnb.GetAsync(
            $"/api/control/anomalies?from={due.AddDays(1):yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        Assert.DoesNotContain(anomalies.Items, i => i.Code == AnomalyCodes.UnrecordedRebate);

        // Chỉ có quyền xem: xem được, không ghi nhận được.
        var viewer = await UserAsync(f, Permissions.Purchase.ContractView);
        Assert.False((await ReadAsync<ContractRebateTrackingDto>(await viewer.GetAsync($"/api/contracts/{contract.Id}/rebates", Ct))).CanRecord);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"/api/contracts/{contract.Id}/rebate-receipts",
            new SaveContractRebateReceiptRequest(rebateId, from, Today, 1_000, "Other", null, null), Ct)).StatusCode);
    }
}
