using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Reports;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Đánh giá NCC định kỳ theo quý (lát F4a, design.md mục 18.20) và KPI "Hoàn thành đánh giá NCC" (mục 18.18).
/// </summary>
[Collection(ApiCollection.Name)]
public class SupplierReviewTests
{
    private readonly ProcApiFactory _factory;

    public SupplierReviewTests(ProcApiFactory factory) => _factory = factory;

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

    [Fact]
    public async Task DanhGiaNccTheoQuy_NccThuocDien_MotPhieuMoiQuy_HoanThanh_TinhKpi()
    {
        // Công ty mới (kỳ tháng hiện tại mở), NCC A có phiếu nhập mua đã ghi sổ trong quý (thuộc diện), NCC B không mua (không thuộc diện).
        var userName = $"dg.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("G"), "Công ty thử đánh giá NCC", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var kt = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(head,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm đánh giá NCC {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bu lông đánh giá NCC", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<Guid> SupplierAsync(string name)
        {
            var s = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(s.Id);
            await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{s.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return s.Id;
        }

        var a = await SupplierAsync("NCC đánh giá A");
        var b = await SupplierAsync("NCC đánh giá B");
        // Nhận hàng NCC A (PO + XNH – design.md mục 19.4): 10 cái × 100.000, kiểm tra Đạt.
        await TestReceipts.ReceiveAsync(_factory, client, a, kt, [new(item.Id, cai, 10, 100_000)], Today);

        var (year, quarter) = (Today.Year, (Today.Month - 1) / 3 + 1);
        var boardUrl = $"/api/supplier-reviews?year={year}&quarter={quarter}";
        var board = await ReadAsync<SupplierReviewBoard>(await client.GetAsync(boardUrl, Ct));
        var row = Assert.Single(board.Rows);
        Assert.Equal((1, 0, (decimal?)0m), (board.DueCount, board.CompletedCount, board.CompletedPct));
        Assert.Equal((a, true, 1_000_000m, (decimal?)100m, (string?)null), (row.SupplierId, row.IsDue, row.ReceiptAmount, row.QualityPct, row.Status));

        SaveSupplierReviewRequest Request(Guid supplierId, int? compliance = 4, string? result = "Continue", string? comment = null, int q = 0) =>
            new(supplierId, q == 0 ? year : year + 1, q == 0 ? quarter : q, Today, 4, 5, 4, 3, 4, compliance, result, comment);

        // Lập phiếu: số liệu hệ thống chốt theo quý; một NCC × quý chỉ một phiếu chưa hủy; quý chưa bắt đầu bị chặn.
        var review = await ReadAsync<SupplierReviewDto>(await client.PostAsJsonAsync("/api/supplier-reviews", Request(a, compliance: null), Ct));
        Assert.Equal(("Draft", 1_000_000m, (decimal?)100m), (review.Status, review.ReceiptAmount, review.QualityPct));
        Assert.StartsWith("DGN-", review.No);
        var duplicate = await client.PostAsJsonAsync("/api/supplier-reviews", Request(a), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("đã có phiếu đánh giá", await TitleAsync(duplicate));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/supplier-reviews", Request(b, q: 1), Ct)).StatusCode);

        // Hoàn thành: thiếu điểm bị chặn; kết luận "Theo dõi" bắt buộc nhận xét; đủ thì khóa, điểm = (4+5+4+3+4+2) ÷ 6 = 3,67.
        var missing = await client.PostAsJsonAsync($"/api/supplier-reviews/{review.Id}/complete", Request(a, compliance: null), Ct);
        Assert.Contains("chấm đủ 6 tiêu chí", await TitleAsync(missing));
        var noComment = await client.PostAsJsonAsync($"/api/supplier-reviews/{review.Id}/complete", Request(a, 2, "Monitor"), Ct);
        Assert.Contains("nhận xét", await TitleAsync(noComment));
        review = await ReadAsync<SupplierReviewDto>(await client.PostAsJsonAsync($"/api/supplier-reviews/{review.Id}/complete",
            Request(a, 2, "Monitor", "Hồ sơ năng lực tài chính chưa cập nhật"), Ct));
        Assert.Equal(("Completed", (decimal?)3.67m, "Monitor"), (review.Status, review.Score, review.Result));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/supplier-reviews/{review.Id}", Request(a), Ct)).StatusCode);

        // NCC B không thuộc diện vẫn đánh giá được; hủy phiếu nháp thì lập lại được.
        var other = await ReadAsync<SupplierReviewDto>(await client.PostAsJsonAsync("/api/supplier-reviews", Request(b), Ct));
        other = await ReadAsync<SupplierReviewDto>(await client.PostAsJsonAsync($"/api/supplier-reviews/{other.Id}/cancel",
            new CancelSupplierReviewRequest("Lập nhầm NCC"), Ct));
        Assert.Equal("Cancelled", other.Status);
        await ReadAsync<SupplierReviewDto>(await client.PostAsJsonAsync("/api/supplier-reviews", Request(b), Ct));

        board = await ReadAsync<SupplierReviewBoard>(await client.GetAsync(boardUrl, Ct));
        Assert.Equal((1, 1, (decimal?)100m, 2), (board.DueCount, board.CompletedCount, board.CompletedPct, board.Rows.Count));
        Assert.Equal((false, "Draft"), (board.Rows.Single(r => r.SupplierId == b).IsDue, board.Rows.Single(r => r.SupplierId == b).Status));

        // KPI: tính theo quý kết thúc trong kỳ – kỳ là cả quý hiện tại thì 1/1 = 100%; kỳ chỉ là tháng đầu quý thì chưa có dữ liệu.
        var (start, end) = SupplierReview.QuarterRange(year, quarter);
        var kpi = await ReadAsync<KpiResult>(await client.GetAsync($"/api/reports/kpi?from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}", Ct));
        var reviewKpi = kpi.Items.Single(i => i.Code == "SupplierReview");
        Assert.Equal((true, (decimal?)100m, (decimal?)1m, (decimal?)1m, (bool?)true, "SUPREV"),
            (reviewKpi.Measured, reviewKpi.Value, reviewKpi.Numerator, reviewKpi.Denominator, reviewKpi.Met, reviewKpi.Report));
        kpi = await ReadAsync<KpiResult>(await client.GetAsync($"/api/reports/kpi?from={start:yyyy-MM-dd}&to={start.AddMonths(1).AddDays(-1):yyyy-MM-dd}", Ct));
        Assert.Null(kpi.Items.Single(i => i.Code == "SupplierReview").Value);

        // Người không có quyền đánh giá NCC không xem được bảng.
        var roleCode = UniqueCode("R");
        await _factory.CreateRoleAsync(roleCode, Permissions.Purchase.PoView);
        var outsiderName = $"ng.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(outsiderName, company.Code, roleCode);
        var outsider = await _factory.CreateClientInCompanyAsync(outsiderName, ProcApiFactory.TestPassword, company.Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(boardUrl, Ct)).StatusCode);
    }
}
