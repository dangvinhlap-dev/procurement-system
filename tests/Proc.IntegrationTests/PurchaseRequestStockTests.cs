using System.Net;
using System.Net.Http.Json;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y3b (design.md mục 19.3 – Q2): thủ kho nơi nhận nhập tồn trên đề nghị mua loại A, B đang chờ duyệt; Cần mua = Cần sử dụng − Tồn (không
/// âm); chỉ thủ kho có phạm vi kho nơi nhận; đã có cấp duyệt trong lượt thì khóa (mục 20); gợi ý tồn đã xác nhận gần nhất cùng nơi nhận.
/// </summary>
public partial class PurchasingTests
{
    private static Task<HttpResponseMessage> EnterStockAsync(HttpClient client, PurchaseRequestDto pr, params decimal[] stock) =>
        client.PostAsJsonAsync($"/api/purchase-requests/{pr.Id}/stock",
            new EnterPurchaseRequestStockRequest([.. pr.Lines.Select((l, i) => new PurchaseRequestStockLineRequest(l.Id, stock[i]))]), Ct);

    [Fact]
    public async Task ThuKhoNoiNhan_NhapTon_TinhCanMua_KhoaSauCapDuyet_GoiYTonGanNhat()
    {
        var f = await ArrangeAsync();
        var (head, headRole) = await ApproverAsync(f);
        var (director, directorRole) = await ApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 1, 0, headRole), Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 2, 0, directorRole), Ct));

        await ReadAsync<WarehouseDetailDto>(await f.Buyer.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest("KP", "Kho phụ", f.Kt, null, null, false), Ct));
        var keeperRole = UniqueCode("K");
        await _factory.CreateRoleAsync(keeperRole, Permissions.Purchase.PrView, Permissions.Purchase.PrEnterStock);
        async Task<HttpClient> KeeperAsync(string warehouse)
        {
            var name = $"tk.{Guid.NewGuid():N}"[..16];
            await _factory.CreateScopedUserAsync(name, f.CompanyCode, keeperRole, [warehouse]);
            return await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, f.CompanyCode);
        }

        var keeper = await KeeperAsync("KT");
        var other = await KeeperAsync("KP");

        // Phiếu nháp chưa nhập tồn: cần mua = cần sử dụng.
        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 10));
        Assert.Equal((10m, (decimal?)null, 10m), (pr.Lines[0].RequiredQty, pr.Lines[0].StockQty, pr.Lines[0].Qty));
        Assert.Contains("đang chờ duyệt", await TitleAsync(await EnterStockAsync(keeper, pr, 4)));
        await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));

        // Thủ kho kho khác: không thấy nút, gọi thẳng API bị 403; người duyệt không có quyền nhập tồn: 403.
        Assert.False((await other.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr.Id}", Ct))!.CanEnterStock);
        Assert.Equal(HttpStatusCode.Forbidden, (await EnterStockAsync(other, pr, 4)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await EnterStockAsync(head, pr, 4)).StatusCode);

        // Thủ kho nơi nhận: chưa có tồn gợi ý; nhập tồn 4 → cần mua 6.
        var view = await keeper.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr.Id}", Ct);
        Assert.True(view!.CanEnterStock);
        Assert.Equal("Kho tổng", view.WarehouseName);
        Assert.Null(view.Lines[0].LastStockQty);
        Assert.Contains("tồn phải ≥ 0", await TitleAsync(await EnterStockAsync(keeper, pr, -1)));
        pr = await ReadAsync<PurchaseRequestDto>(await EnterStockAsync(keeper, pr, 4));
        Assert.Equal((10m, (decimal?)4m, 6m), (pr.Lines[0].RequiredQty, pr.Lines[0].StockQty, pr.Lines[0].Qty));
        Assert.NotNull(pr.StockConfirmedByName);
        Assert.NotNull(pr.StockConfirmedAt);

        // Cấp 1 duyệt → tồn khóa: không còn nút, gọi API báo lỗi.
        await ReadAsync<PurchaseRequestDto>(await ActAsync(head, "purchase-requests", pr.Id, "approve"));
        view = await keeper.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr.Id}", Ct);
        Assert.False(view!.CanEnterStock);
        Assert.Contains("tồn đã khóa", view.EnterStockBlocker);
        Assert.Contains("tồn đã khóa", await TitleAsync(await EnterStockAsync(keeper, pr, 0)));
        await ReadAsync<PurchaseRequestDto>(await ActAsync(director, "purchase-requests", pr.Id, "approve"));

        // Đề nghị sau cùng mặt hàng, cùng nơi nhận: gợi ý tồn 4 của đề nghị trước. Tồn 12 ≥ cần 10 → cần mua 0, dòng không lên PO.
        var second = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 10));
        await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{second.Id}/submit", null, Ct));
        view = await keeper.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{second.Id}", Ct);
        Assert.Equal((4m, pr.No, (Guid?)pr.Id), (view!.Lines[0].LastStockQty, view.Lines[0].LastStockRequestNo, view.Lines[0].LastStockRequestId));
        second = await ReadAsync<PurchaseRequestDto>(await EnterStockAsync(keeper, second, 12));
        Assert.Equal((0m, 0m), (second.Lines[0].Qty, second.TotalAmount));
        await ReadAsync<PurchaseRequestDto>(await ActAsync(head, "purchase-requests", second.Id, "approve"));
        await ReadAsync<PurchaseRequestDto>(await ActAsync(director, "purchase-requests", second.Id, "approve"));
        var open = (await f.Buyer.GetFromJsonAsync<List<OpenRequestLineDto>>("/api/purchase-requests/open-lines", Ct))!;
        Assert.Equal(6m, open.Single(l => l.RequestId == pr.Id).RemainingQty);
        Assert.DoesNotContain(open, l => l.RequestId == second.Id);

        // Đề nghị dịch vụ không có bước Thủ kho.
        var service = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, "Service", null, 2));
        await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{service.Id}/submit", null, Ct));
        Assert.False((await keeper.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{service.Id}", Ct))!.CanEnterStock);
        Assert.Contains("dịch vụ", await TitleAsync(await EnterStockAsync(keeper, service, 1)));
    }
}
