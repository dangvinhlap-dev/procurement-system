using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Duyệt bảng giá NCC (lát B1, design.md mục 8.5, 18.6 – quyết định M2): bảng giá (cả bảng riêng công ty) phải duyệt mới hiệu lực, người
/// lập không tự duyệt; từ chối bắt buộc lý do và về nháp; rút về nháp khi đang chờ; bảng toàn tập đoàn chưa duyệt thì công ty khác không thấy.
/// </summary>
[Collection(ApiCollection.Name)]
public class PriceListApprovalTests
{
    private readonly ProcApiFactory _factory;

    public PriceListApprovalTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..8];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>
    /// Người dùng quản trị riêng của test ở tổng công ty – không dùng tài khoản admin chung vì công ty tạo mới tự gán cho người tạo
    /// (AuthTests đếm số công ty của admin).
    /// </summary>
    private async Task<string> NewManagerAsync()
    {
        var userName = $"ql.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        return userName;
    }

    private Task<HttpClient> ClientAtAsync(string userName, string companyCode) =>
        _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, companyCode);

    /// <summary>Người chỉ có quyền xem và duyệt bảng giá (không sửa được) tại một công ty.</summary>
    private async Task<HttpClient> ApproverAtAsync(string companyCode)
    {
        var roleCode = UniqueCode("G");
        await _factory.CreateRoleAsync(roleCode, Permissions.Pricing.PriceListView, Permissions.Pricing.PriceListApprove);
        var userName = $"dg.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, companyCode, roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, companyCode);
    }

    private sealed record Catalog(Guid ItemId, Guid Cai, Guid SupplierId);

    /// <summary>Một mặt hàng (cái) và một NCC đã duyệt dùng chung.</summary>
    private async Task<Catalog> CatalogAsync(HttpClient head)
    {
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm duyệt bảng giá {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Dây curoa B52", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC dây curoa", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        return new Catalog(item.Id, cai, supplier.Id);
    }

    private static SavePriceListRequest PriceList(Catalog c, bool groupWide, decimal price, DateOnly? validTo = null) =>
        new(c.SupplierId, "Bảng giá dây curoa", groupWide, Today.AddDays(-1), validTo, null, null,
            [new(null, c.ItemId, c.Cai, 0, price, 0, null, null)]);

    private static Task<HttpResponseMessage> ActAsync(HttpClient client, Guid id, string action, string? comment = null) =>
        client.PostAsJsonAsync($"/api/price-lists/{id}/{action}", new ApprovalActionRequest(comment), Ct);

    private static async Task<List<ResolvedSupplierPriceDto>> ResolveAsync(HttpClient client, Catalog c) =>
        (await client.GetFromJsonAsync<List<ResolvedSupplierPriceDto>>(
            $"/api/price-lists/resolve?supplierId={c.SupplierId}&date={Today:yyyy-MM-dd}&itemIds={c.ItemId}", Ct))!;

    [Fact]
    public async Task BangGiaRiengCongTy_GuiDuyet_NguoiLapKhongTuDuyet_TuChoiVeNhap_DuyetXongMoiHieuLuc()
    {
        var manager = await NewManagerAsync();
        var head = await ClientAtAsync(manager, "TCT");
        var company = (await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("B"), "Công ty thử duyệt bảng giá", null, null, null, null, null, "KT", "Kho tổng"), Ct))).Code;
        var catalog = await CatalogAsync(head);
        var creator = await ClientAtAsync(manager, company);
        var approver = await ApproverAtAsync(company);

        var list = await ReadAsync<PriceListDto>(await creator.PostAsJsonAsync("/api/price-lists", PriceList(catalog, false, 30_000, Today.AddDays(10)), Ct));
        Assert.True(list.Actions.CanSubmit);

        // Gửi duyệt: Chờ duyệt, chưa gợi ý giá, không sửa được; người lập không tự duyệt.
        list = await ReadAsync<PriceListDto>(await creator.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        Assert.Equal("Submitted", list.Status);
        Assert.False(list.Actions.CanEdit);
        Assert.False(list.Actions.CanApprove);
        Assert.True(list.Actions.CanRecall);
        Assert.Empty(await ResolveAsync(creator, catalog));
        Assert.Contains("không còn ở trạng thái nháp", await TitleAsync(await creator.PutAsJsonAsync($"/api/price-lists/{list.Id}",
            PriceList(catalog, false, 1), Ct)));
        Assert.Contains("Người lập không tự duyệt", await TitleAsync(await ActAsync(creator, list.Id, "approve")));

        // Người duyệt (chỉ có quyền xem + duyệt): thấy trong Việc cần làm, nút Duyệt; không sửa, không rút được.
        var pending = (await approver.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!;
        Assert.Contains(pending, p => p.DocType == "PRL" && p.DocumentId == list.Id && p.Level == 1);
        var seen = await ReadAsync<PriceListDto>(await approver.GetAsync($"/api/price-lists/{list.Id}", Ct));
        Assert.True(seen.Actions.CanApprove);
        Assert.False(seen.Actions.CanRecall);
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostAsync($"/api/price-lists/{list.Id}/recall", null, Ct)).StatusCode);

        // Từ chối phải có lý do; từ chối xong về Nháp kèm lý do, lịch sử ghi bước bị từ chối.
        Assert.Contains("lý do", await TitleAsync(await ActAsync(approver, list.Id, "reject", " ")));
        list = await ReadAsync<PriceListDto>(await ActAsync(approver, list.Id, "reject", "Giá cao hơn năm trước"));
        Assert.Equal(("Draft", "Giá cao hơn năm trước"), (list.Status, list.RejectReason));
        Assert.Equal([("Rejected", 1)], list.Approvals.Select(a => (a.Status, a.Round)));

        // Gửi lại rồi người lập rút về nháp: bước chờ bỏ qua; gửi lần ba thì người duyệt duyệt xong → Hiệu lực, PO gợi ý giá.
        await ReadAsync<PriceListDto>(await creator.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        list = await ReadAsync<PriceListDto>(await creator.PostAsync($"/api/price-lists/{list.Id}/recall", null, Ct));
        Assert.Equal(("Draft", (string?)null), (list.Status, list.RejectReason));
        await ReadAsync<PriceListDto>(await creator.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        list = await ReadAsync<PriceListDto>(await ActAsync(approver, list.Id, "approve", "Đồng ý"));
        Assert.Equal("Active", list.Status);
        Assert.NotNull(list.ActivatedAt);
        Assert.Equal(["Rejected", "Skipped", "Approved"], list.Approvals.Select(a => a.Status));
        Assert.Equal([30_000m], (await ResolveAsync(creator, catalog)).Select(r => r.UnitPrice));
        Assert.DoesNotContain((await approver.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == list.Id);

        // Hết hiệu lực sau 10 ngày → cảnh báo sắp hết hiệu lực (≤ 30 ngày) ở phiếu và danh sách.
        Assert.Equal((10, true), (list.DaysToExpiry, list.ExpiringSoon));
        var row = (await creator.GetFromJsonAsync<List<PriceListListItemDto>>("/api/price-lists", Ct))!.Single(l => l.Id == list.Id);
        Assert.True(row.ExpiringSoon);
    }

    [Fact]
    public async Task BangToanTapDoan_ChuaDuyet_CongTyKhacKhongThay()
    {
        var manager = await NewManagerAsync();
        var head = await ClientAtAsync(manager, "TCT");
        var catalog = await CatalogAsync(head);
        var member = await ClientAtAsync(manager, (await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("B"), "Công ty thử xem bảng giá", null, null, null, null, null, "KT", "Kho tổng"), Ct))).Code);
        var approver = await ApproverAtAsync("TCT");

        var list = await ReadAsync<PriceListDto>(await head.PostAsJsonAsync("/api/price-lists", PriceList(catalog, true, 25_000), Ct));
        await ReadAsync<PriceListDto>(await head.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/price-lists/{list.Id}", Ct)).StatusCode);
        Assert.DoesNotContain((await member.GetFromJsonAsync<List<PriceListListItemDto>>("/api/price-lists", Ct))!, l => l.Id == list.Id);

        await ReadAsync<PriceListDto>(await ActAsync(approver, list.Id, "approve"));
        var seen = await ReadAsync<PriceListDto>(await member.GetAsync($"/api/price-lists/{list.Id}", Ct));
        Assert.Equal("Active", seen.Status);
        // Công ty khác xem được lịch sử duyệt của bảng toàn tập đoàn (yêu cầu người dùng 03/11/2026 – ai mở phiếu cũng thấy ai lập, ai duyệt).
        var step = Assert.Single(seen.Approvals);
        Assert.Equal("Approved", step.Status);
        Assert.False(string.IsNullOrEmpty(step.ActedByName));
        Assert.Equal([25_000m], (await ResolveAsync(member, catalog)).Select(r => r.UnitPrice));
    }
}
