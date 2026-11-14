using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Thỏa thuận chiết khấu bậc sản lượng gộp (lát G6a, design.md mục 18.25): tổng công ty lập cho NCC × nhóm hàng, áp cho các công ty, Ban duyệt
/// theo quy trình CKB; lũy kế PO đã duyệt của các công ty so với bậc, chia chiết khấu theo tỷ lệ doanh số; công ty áp dụng chỉ xem thỏa thuận
/// hiệu lực và phần của mình; không trùng NCC × mặt hàng × công ty × thời hạn.
/// </summary>
[Collection(ApiCollection.Name)]
public class VolumeRebateTests
{
    private const string Url = "/api/volume-rebates";
    private readonly ProcApiFactory _factory;

    public VolumeRebateTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static DateOnly ThisMonth => new(Today.Year, Today.Month, 1);

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task LapDuyet_LuyKeHaiCongTy_ChiaChietKhau_CongTyApDungChiThayPhanMinh()
    {
        // Quản trị riêng của test ở tổng công ty; hai công ty mới cùng mua của một NCC.
        var userName = $"vr.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        async Task<(CompanyDetailDto Company, HttpClient Client, Guid Warehouse)> CompanyAsync(string name)
        {
            var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
                new CreateCompanyRequest(UniqueCode("V"), name, null, null, null, null, ThisMonth, "KT", "Kho tổng"), Ct));
            var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
            var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
            return (company, client, warehouse);
        }

        var a = await CompanyAsync("Công ty A chiết khấu gộp");
        var b = await CompanyAsync("Công ty B chiết khấu gộp");
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm chiết khấu gộp {Guid.NewGuid():N}", null, null), Ct));
        async Task<Guid> ItemAsync(string name) => (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var item1 = await ItemAsync("Gạch ốp G6 loại 1");
        var item2 = await ItemAsync("Gạch ốp G6 loại 2");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<Guid> SupplierAsync(string name, bool approved)
        {
            var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null,
                    approved ? "Approved" : "Potential", null, null, [], []), Ct));
            if (!approved) return supplier.Id;
            await _factory.FinishSupplierApprovalAsync(supplier.Id);
            foreach (var c in new[] { a.Client, b.Client })
                await ReadAsync<SupplierCompanyDto>(await c.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return supplier.Id;
        }

        var supplierId = await SupplierAsync("NCC gạch chiết khấu gộp", true);
        var potential = await SupplierAsync("NCC gạch tiềm năng G6", false);

        // Thỏa thuận theo tháng, từ đầu tháng trước đến cuối tháng sau; bậc 100 cái 1% – 200 cái 2%, mục tiêu bậc 2.
        var from = ThisMonth.AddMonths(-1);
        var to = ThisMonth.AddMonths(2).AddDays(-1);
        SaveVolumeRebateRequest Request(Guid supplier, params SaveVolumeRebateTierRequest[] tiers) => new(
            "Chiết khấu gạch ốp năm", supplier, category.Id, from, to, "Month", "Quantity", 2, [a.Company.Id, b.Company.Id], "Theo thư chính sách NCC",
            [new(null, item1, cai, null), new(null, item2, cai, null)], tiers.Length == 0 ? [new(200, 2), new(100, 1)] : tiers);

        // Công ty thành viên không lập được; NCC chưa duyệt; bậc sai.
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Client.PostAsJsonAsync(Url, Request(supplierId), Ct)).StatusCode);
        Assert.Contains("chưa ở trạng thái Đã duyệt", await TitleAsync(await head.PostAsJsonAsync(Url, Request(potential), Ct)));
        Assert.Contains("% chiết khấu phải lớn hơn bậc 1", await TitleAsync(await head.PostAsJsonAsync(Url, Request(supplierId, new(100, 2), new(200, 1)), Ct)));

        var draft = await ReadAsync<VolumeRebateDto>(await head.PostAsJsonAsync(Url, Request(supplierId), Ct));
        Assert.StartsWith("CKB", draft.No);
        Assert.Equal(("Draft", 2, true), (draft.Status, draft.Lines.Count, draft.Actions.CanEdit));
        Assert.Equal([(1, 100m, 1m), (2, 200m, 2m)], draft.Tiers.Select(t => (t.TierNo, t.FromValue, t.Percent)));

        // Cùng NCC × mặt hàng × công ty, thời hạn giao nhau → chặn (một lượng mua không tính bậc hai lần).
        Assert.Contains($"đã có trong thỏa thuận {draft.No}", await TitleAsync(await head.PostAsJsonAsync(Url,
            Request(supplierId) with { ValidFrom = to, ValidTo = to.AddMonths(1), CompanyIds = [b.Company.Id] }, Ct)));

        // Nháp chưa công bố: công ty áp dụng không thấy.
        Assert.Equal(HttpStatusCode.NotFound, (await a.Client.GetAsync($"{Url}/{draft.Id}", Ct)).StatusCode);

        // Gửi duyệt; người lập không tự duyệt; Ban duyệt → hiệu lực.
        var submitted = await ReadAsync<VolumeRebateDto>(await head.PostAsync($"{Url}/{draft.Id}/submit", null, Ct));
        Assert.Equal(("Submitted", false), (submitted.Status, submitted.Actions.CanApprove));
        var boardRole = UniqueCode("B");
        await _factory.CreateRoleAsync(boardRole, Permissions.Pricing.VolumeRebateView, Permissions.Pricing.VolumeRebateApprove);
        var boardUser = $"vb.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(boardUser, "TCT", boardRole);
        var board = await _factory.CreateClientInCompanyAsync(boardUser, ProcApiFactory.TestPassword, "TCT");
        Assert.Contains(await ReadAsync<List<PendingApprovalDto>>(await board.GetAsync("/api/approvals/pending", Ct)), p => p.DocumentId == draft.Id);
        var active = await ReadAsync<VolumeRebateDto>(await board.PostAsJsonAsync($"{Url}/{draft.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Active", active.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PutAsJsonAsync($"{Url}/{draft.Id}", Request(supplierId), Ct)).StatusCode);

        // PO đã duyệt tháng trước: A 120 cái × 10.000 = 1.200.000; B 100 cái × 10.000 = 1.000.000; PO nháp của B không tính.
        async Task<Guid> PoAsync((CompanyDetailDto Company, HttpClient Client, Guid Warehouse) c, DateOnly date, Guid item, decimal qty, bool approve = true)
        {
            var po = await ReadAsync<PurchaseOrderDto>(await c.Client.PostAsJsonAsync("/api/purchase-orders",
                new SavePurchaseOrderRequest(date, supplierId, c.Warehouse, null, null, null, null, 0, null,
                    [new SavePurchaseOrderLineRequest(null, item, cai, qty, 10_000, 0, "10", null, null, null)]), Ct));
            if (!approve) return po.Id;
            await ReadAsync<PurchaseOrderDto>(await c.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
            await _factory.FinishPurchaseOrderApprovalAsync(po.Id);
            return po.Id;
        }

        await PoAsync(a, from.AddDays(3), item1, 120);
        await PoAsync(b, from.AddDays(5), item2, 100);
        await PoAsync(b, from.AddDays(6), item1, 500, approve: false);
        // Tháng này A đã mua 95 cái: sắp chạm bậc 1 (≥ 90% của 100).
        await PoAsync(a, Today, item1, 95);

        var progress = await ReadAsync<VolumeRebateProgressDto>(await head.GetAsync($"{Url}/{draft.Id}/progress", Ct));
        Assert.Equal(("Quantity", "CAI", 3), (progress.Basis, progress.UomCode, progress.Periods.Count));
        var last = progress.Periods[0];
        // 220 cái → bậc 2 (2%): chiết khấu 2.200.000 × 2% = 44.000, chia A 24.000 (54,5455%), B 20.000.
        Assert.Equal(("Ended", 220m, 2, 44_000m, "Reached"), (last.State, last.Volume, last.TierNo, last.RebateAmount, last.TargetStatus));
        Assert.Equal([(a.Company.Code, 120m, 24_000m), (b.Company.Code, 100m, 20_000m)],
            last.Companies.Select(s => (s.CompanyCode, s.Quantity, s.RebateShare)).OrderBy(s => s.Item1 == a.Company.Code ? 0 : 1));
        var current = progress.Periods[1];
        Assert.Equal(("Open", 95m, (int?)null, 0m, 1, true), (current.State, current.Volume, current.TierNo, current.RebateAmount, current.NextTierNo, current.IsNearNextTier));
        Assert.Contains(current.TargetStatus, new[] { "OnTrack", "AtRisk" });
        Assert.Equal(("Future", "NotStarted"), (progress.Periods[2].State, progress.Periods[2].TargetStatus));

        // Công ty áp dụng thấy thỏa thuận hiệu lực, tổng gộp, nhưng chỉ phần của mình; không thao tác được.
        var seen = await ReadAsync<VolumeRebateDto>(await a.Client.GetAsync($"{Url}/{draft.Id}", Ct));
        Assert.Equal((false, false), (seen.Actions.CanEdit, seen.Actions.CanCancel));
        var memberProgress = await ReadAsync<VolumeRebateProgressDto>(await a.Client.GetAsync($"{Url}/{draft.Id}/progress", Ct));
        Assert.Equal((220m, 44_000m), (memberProgress.Periods[0].Volume, memberProgress.Periods[0].RebateAmount));
        Assert.Equal([a.Company.Code], memberProgress.Periods[0].Companies.Select(s => s.CompanyCode));
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Client.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelVolumeRebateRequest("x"), Ct)).StatusCode);

        // Theo dõi bậc: kỳ hiện tại của thỏa thuận hiệu lực; lọc cảnh báo vẫn có (sắp chạm bậc).
        var tracking = await ReadAsync<List<VolumeRebateTrackingRowDto>>(await head.GetAsync($"{Url}/tracking?onlyAlerts=true", Ct));
        var row = tracking.Single(r => r.AgreementId == draft.Id);
        Assert.Equal((ThisMonth, 95m, true), (row.Period.PeriodFrom, row.Period.Volume, row.Period.IsNearNextTier));

        // Chép: thỏa thuận nháp mới nối tiếp ngay sau, cùng bậc và mặt hàng.
        var copy = await ReadAsync<VolumeRebateDto>(await head.PostAsync($"{Url}/{draft.Id}/copy", null, Ct));
        Assert.Equal(("Draft", to.AddDays(1), 2), (copy.Status, copy.ValidFrom, copy.Tiers.Count));

        // Hủy bắt buộc lý do; hủy xong công ty áp dụng không thấy, không còn theo dõi.
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelVolumeRebateRequest(" "), Ct)).StatusCode);
        var cancelled = await ReadAsync<VolumeRebateDto>(await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelVolumeRebateRequest("NCC đổi chính sách"), Ct));
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Client.GetAsync($"{Url}/{draft.Id}", Ct)).StatusCode);
        Assert.DoesNotContain(await ReadAsync<List<VolumeRebateTrackingRowDto>>(await head.GetAsync($"{Url}/tracking", Ct)), r => r.AgreementId == draft.Id);
    }
}
