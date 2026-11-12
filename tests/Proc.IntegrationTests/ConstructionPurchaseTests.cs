using System.Net;
using System.Net.Http.Json;
using Proc.Application.Purchasing;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y3a (design.md mục 19.3): đề nghị mua có loại mua A/B/DV; loại A bắt buộc chọn công trình đang thực hiện; lũy kế đã mua của công trình
/// = dòng PO đã duyệt lấy từ đề nghị loại A (giá chưa VAT, kèm số có VAT – Q4). Người dùng chốt 11/11/2026: mọi khoản mua cho công trình bắt buộc
/// qua đề nghị loại A – PO không lấy dòng từ đề nghị loại A không tính vào công trình.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>Công trình đang thực hiện của công ty fixture, dự toán <paramref name="estimate"/>.</summary>
    private static async Task<ConstructionProjectDto> ProjectAsync(Fixture f, decimal estimate) =>
        await ReadAsync<ConstructionProjectDto>(await f.Buyer.PostAsJsonAsync("/api/construction-projects",
            new CreateConstructionProjectRequest(UniqueCode("CT"), "Nhà xưởng số 2", f.DepartmentId, "Khu B", Today, null, estimate, "QĐ 01", null), Ct));

    private static Task<HttpResponseMessage> CreatePrOfAsync(Fixture f, string? category, Guid? projectId, decimal qty = 8) =>
        f.Buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, f.DepartmentId, "Thi công", Today.AddDays(7), f.Kt, "Normal", null,
                [new(null, f.ItemId, f.Cai, qty, f.SupplierId, null, null)], null, category, projectId), Ct);

    [Fact]
    public async Task DeNghiLoaiA_BatBuocCongTrinh_LuyKeTheoPoDaDuyet_ChuaVaCoVat()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var project = await ProjectAsync(f, 1_000_000m);

        // Loại A thiếu công trình, loại B/DV gắn công trình, loại mua lạ: bị chặn.
        Assert.Contains("phải chọn công trình", await TitleAsync(await CreatePrOfAsync(f, "Construction", null)));
        Assert.Contains("Chỉ đề nghị mua loại A", await TitleAsync(await CreatePrOfAsync(f, "Service", project.Id)));
        Assert.Equal(HttpStatusCode.BadRequest, (await CreatePrOfAsync(f, "X", null)).StatusCode);

        // Phiếu cũ / không gửi loại mua = B hàng hóa, không có khung công trình.
        var goods = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 1));
        Assert.Equal(("Goods", (ConstructionBudgetDto?)null), (goods.Category, goods.Construction));

        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, "construction", project.Id));
        Assert.Equal("Construction", pr.Category);
        Assert.Equal((project.Code, 1_000_000m, 0m, 1_000_000m, pr.TotalAmount),
            (pr.Construction!.Code, pr.Construction.EstimateAmount, pr.Construction.SpentAmount, pr.Construction.RemainingAmount, pr.Construction.RequestAmount));
        await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(approver, "purchase-requests", pr.Id, "approve"));
        Assert.Equal(0m, pr.Construction!.RequestAmount);
        await _factory.ApproveSummaryForRequestAsync(pr.Id);

        // PO từ dòng đề nghị A: 8 × 100.000, CK 5% → 760.000 chưa VAT, VAT 10% 76.000. PO nháp chưa tính; duyệt rồi mới vào lũy kế.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 8, pr.Lines[0].Id));
        Assert.Equal(0m, (await f.Buyer.GetFromJsonAsync<ConstructionProjectDto>($"/api/construction-projects/{project.Id}", Ct))!.SpentAmount);
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));

        // PO không lấy dòng từ đề nghị A (mua thẳng, hoặc từ đề nghị B) không tính vào công trình.
        var direct = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 3, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{direct.Id}/submit", null, Ct));
        await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", direct.Id, "approve"));

        var after = await f.Buyer.GetFromJsonAsync<ConstructionProjectDto>($"/api/construction-projects/{project.Id}", Ct);
        Assert.Equal((760_000m, 836_000m, 240_000m), (after!.SpentAmount, after.SpentAmountWithVat, after.RemainingAmount));
        var listed = (await f.Buyer.GetFromJsonAsync<List<ConstructionProjectDto>>("/api/construction-projects", Ct))!.Single(p => p.Id == project.Id);
        Assert.Equal(760_000m, listed.SpentAmount);
        var prAfter = await f.Buyer.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr.Id}", Ct);
        Assert.Equal((760_000m, 836_000m, 240_000m), (prAfter!.Construction!.SpentAmount, prAfter.Construction.SpentAmountWithVat, prAfter.Construction.RemainingAmount));

        // Danh sách PR có cột loại mua.
        var search = await f.Buyer.GetFromJsonAsync<PurchaseDocumentListResult>("/api/purchase-requests", Ct);
        Assert.Equal("Construction", search!.Items.Single(i => i.Id == pr.Id).Category);
        Assert.Equal("Goods", search.Items.Single(i => i.Id == goods.Id).Category);
    }

    [Fact]
    public async Task DeNghiLoaiA_CongTrinhKhongConThucHien_KhongChonMoi_KhongGuiDuyet()
    {
        var f = await ArrangeAsync();
        var project = await ProjectAsync(f, 500_000m);
        var draft = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, "Construction", project.Id, 1));

        await ReadAsync<ConstructionProjectDto>(await f.Buyer.PutAsJsonAsync($"/api/construction-projects/{project.Id}",
            new UpdateConstructionProjectRequest(project.Name, f.DepartmentId, project.Location, project.StartDate, null, "Suspended", null), Ct));

        // Phiếu mới không chọn được; phiếu nháp đã gắn lưu lại được nhưng không gửi duyệt được.
        Assert.Contains("Đang thực hiện", await TitleAsync(await CreatePrOfAsync(f, "Construction", project.Id)));
        var saved = await f.Buyer.PutAsJsonAsync($"/api/purchase-requests/{draft.Id}",
            new SavePurchaseRequestRequest(Today, f.DepartmentId, "Thi công đợt 2", null, f.Kt, "Normal", null,
                [new(draft.Lines[0].Id, f.ItemId, f.Cai, 2, null, null, null)], null, "Construction", project.Id), Ct);
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync(Ct));
        var submit = await f.Buyer.PostAsync($"/api/purchase-requests/{draft.Id}/submit", null, Ct);
        Assert.Contains("không gửi duyệt", await TitleAsync(submit));

        // Công trình của công ty khác (Id lạ) không tìm thấy.
        Assert.Equal(HttpStatusCode.NotFound, (await CreatePrOfAsync(f, "Construction", Guid.NewGuid())).StatusCode);
    }
}
