using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Phiếu duyệt NCC và giá tháng (lát G2, design.md mục 18.25 – quyết định thành lập Ban cung ứng): tổng công ty lập theo tháng × nhóm hàng
/// cho các công ty chọn, Ban duyệt theo quy trình GTH (người lập không tự duyệt); công ty áp dụng chỉ xem phiếu đã duyệt; điều chỉnh thay
/// phiếu cũ, hủy, chép sang tháng sau; không trùng mặt hàng × tháng × công ty.
/// </summary>
[Collection(ApiCollection.Name)]
public class MonthlySupplierApprovalTests
{
    private const string Url = "/api/monthly-supplier-approvals";
    private readonly ProcApiFactory _factory;

    public MonthlySupplierApprovalTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..8];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static DateOnly ThisMonth => new(Today.Year, Today.Month, 1);

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Quản trị riêng của test ở tổng công ty và công ty BHA.</summary>
    private async Task<(HttpClient Head, HttpClient Member)> ManagerAsync()
    {
        var userName = $"gt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT"),
            await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA"));
    }

    /// <summary>Người dùng có đúng các quyền đã cho tại tổng công ty.</summary>
    private async Task<HttpClient> UserWithAsync(params string[] permissions)
    {
        var roleCode = UniqueCode("G");
        await _factory.CreateRoleAsync(roleCode, permissions);
        var userName = $"gu.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    private async Task<Guid> SupplierAsync(HttpClient head, string name, bool approved = true)
    {
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null,
                approved ? "Approved" : "Potential", null, null, [], []), Ct));
        if (approved) await _factory.FinishSupplierApprovalAsync(supplier.Id);
        return supplier.Id;
    }

    /// <summary>Thêm dòng NCC – sản phẩm: NCC Đã duyệt có dòng này là NCC đủ điều kiện của mặt hàng (lát G1, H2).</summary>
    private async Task LinkAsync(HttpClient head, Guid supplierId, Guid itemId, Guid uomId) =>
        await ReadAsync<SupplierItemDto>(await head.PostAsJsonAsync($"/api/suppliers/{supplierId}/items",
            new SaveSupplierItemRequest(itemId, null, uomId, null, null, false), Ct));

    /// <summary>Nhóm hàng mới với hai mặt hàng (mỗi test một nhóm để không trùng phiếu của test khác).</summary>
    private async Task<(Guid CategoryId, Guid FirstItem, Guid SecondItem, Guid Uom)> CategoryAsync(HttpClient head, string name)
    {
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm {name}", null, null), Ct));
        async Task<Guid> ItemAsync(string itemName) => (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, itemName, null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, []), Ct))).Id;
        return (category.Id, await ItemAsync($"{name} 1"), await ItemAsync($"{name} 2"), cai);
    }

    [Fact]
    public async Task LapGuiDuyet_CongTyApDungChiXem_DieuChinhThayPhieuCu_ChepThangSau()
    {
        var (head, member) = await ManagerAsync();
        var main = await SupplierAsync(head, "NCC chính tháng");
        var backup = await SupplierAsync(head, "NCC dự phòng tháng");
        var potential = await SupplierAsync(head, "NCC tiềm năng tháng", approved: false);
        var third = await SupplierAsync(head, "NCC thứ ba tháng");
        var (categoryId, item1, item2, cai) = await CategoryAsync(head, "Bao bì tháng");
        // Đủ 03 NCC đủ điều kiện mỗi mặt hàng (chính, dự phòng + một NCC có dòng NCC – sản phẩm) – phiếu không có ngoại lệ (lát H2).
        await LinkAsync(head, third, item1, cai);
        await LinkAsync(head, third, item2, cai);
        var tct = await _factory.GetCompanyIdAsync("TCT");
        var bha = await _factory.GetCompanyIdAsync("BHA");
        SaveMonthlyApprovalRequest Request(DateOnly period, params SaveMonthlyApprovalLineRequest[] lines) =>
            new(period, categoryId, [tct, bha], "Theo biên bản họp Ban", lines);
        SaveMonthlyApprovalLineRequest Line(Guid item, Guid supplier, decimal price = 12_000m) =>
            new(null, item, cai, supplier, price, backup, 12_500m, 1_000m, null);

        // Chỉ chọn NCC Đã duyệt; tháng đã qua không lập được; công ty thành viên không lập được.
        Assert.Contains("chưa ở trạng thái Đã duyệt", await TitleAsync(await head.PostAsJsonAsync(Url, Request(ThisMonth, Line(item1, potential)), Ct)));
        Assert.Contains("đã qua", await TitleAsync(await head.PostAsJsonAsync(Url, Request(ThisMonth.AddMonths(-1), Line(item1, main)), Ct)));
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync(Url, Request(ThisMonth, Line(item1, main)), Ct)).StatusCode);

        // Ngày bất kỳ trong tháng lưu thành ngày đầu tháng.
        var draft = await ReadAsync<MonthlyApprovalDto>(await head.PostAsJsonAsync(Url, Request(ThisMonth.AddDays(9), Line(item1, main), Line(item2, main)), Ct));
        Assert.StartsWith("GTH", draft.No);
        Assert.Equal((ThisMonth, "Draft", 2, true), (draft.Period, draft.Status, draft.Lines.Count, draft.Actions.CanEdit));
        Assert.Equal(["TCT", "BHA"], draft.Companies.Select(c => c.Code));
        Assert.Equal((12_500m, 1_000m), (draft.Lines[0].BackupUnitPrice!.Value, draft.Lines[0].QuantityLimit!.Value));

        // Cùng mặt hàng × tháng × công ty ở phiếu khác bị chặn.
        Assert.Contains($"đã có trong phiếu {draft.No}", await TitleAsync(await head.PostAsJsonAsync(Url, Request(ThisMonth, Line(item2, main)), Ct)));

        // Nháp chưa công bố: công ty áp dụng không thấy.
        Assert.DoesNotContain((await member.GetFromJsonAsync<List<MonthlyApprovalListItemDto>>(Url, Ct))!, r => r.Id == draft.Id);

        // Người lập gửi, không tự duyệt; người duyệt (Ban) thấy ở Việc cần làm và duyệt.
        Assert.All(draft.Lines, l => Assert.Empty(l.Exceptions!));
        var submitted = await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{Url}/{draft.Id}/submit", null, Ct));
        Assert.Equal(("Submitted", false, false), (submitted.Status, submitted.Actions.CanApprove, submitted.HasException));
        Assert.All(submitted.Approvals, s => Assert.Equal(1, s.Stage));
        Assert.Contains("không tự duyệt", await TitleAsync(await head.PostAsJsonAsync($"{Url}/{draft.Id}/approve", new ApprovalActionRequest(null), Ct)));
        var board = await UserWithAsync(Permissions.Pricing.MonthlyApprovalView, Permissions.Pricing.MonthlyApprovalApprove);
        Assert.Contains((await board.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!,
            p => p.DocumentId == draft.Id && p.DocType == "GTH");
        Assert.Equal(HttpStatusCode.Forbidden, (await board.PostAsJsonAsync(Url, Request(ThisMonth, Line(item1, main)), Ct)).StatusCode);
        var approved = await ReadAsync<MonthlyApprovalDto>(await board.PostAsJsonAsync($"{Url}/{draft.Id}/approve", new ApprovalActionRequest("Đồng ý"), Ct));
        Assert.Equal(("Approved", false, false), (approved.Status, approved.Actions.CanEdit, approved.Actions.CanAdjust));
        // Người duyệt không có quyền lập nên không điều chỉnh; người lập (Văn phòng Ban) thì có.
        var ownView = await ReadAsync<MonthlyApprovalDto>(await head.GetAsync($"{Url}/{draft.Id}", Ct));
        Assert.Equal((true, true, true), (ownView.Actions.CanAdjust, ownView.Actions.CanCancel, ownView.Actions.CanCopyNextMonth));

        // Công ty áp dụng thấy phiếu đã duyệt, chỉ xem.
        Assert.Contains((await member.GetFromJsonAsync<List<MonthlyApprovalListItemDto>>(Url, Ct))!, r => r.Id == draft.Id && r.CompanyCodes == "BHA, TCT");
        var seen = await ReadAsync<MonthlyApprovalDto>(await member.GetAsync($"{Url}/{draft.Id}", Ct));
        Assert.Equal((false, false), (seen.Actions.CanAdjust, seen.Actions.CanCancel));
        Assert.Contains("chỉ xem", seen.Actions.ReadOnlyReason);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync($"{Url}/{draft.Id}/adjust", null, Ct)).StatusCode);

        // Điều chỉnh: phiếu nháp mới cùng tháng, sửa giá; chỉ một phiếu điều chỉnh; phiếu cũ không hủy khi đang có phiếu điều chỉnh.
        var adjustment = await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{Url}/{draft.Id}/adjust", null, Ct));
        Assert.Equal(("Draft", draft.No, ThisMonth, 2), (adjustment.Status, adjustment.ReplacesNo, adjustment.Period, adjustment.Lines.Count));
        Assert.Contains("đã có phiếu điều chỉnh", await TitleAsync(await head.PostAsync($"{Url}/{draft.Id}/adjust", null, Ct)));
        Assert.Contains("phiếu điều chỉnh", await TitleAsync(await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelMonthlyApprovalRequest("x"), Ct)));
        adjustment = await ReadAsync<MonthlyApprovalDto>(await head.PutAsJsonAsync($"{Url}/{adjustment.Id}",
            Request(ThisMonth, Line(item1, main, 11_800m), Line(item2, main)), Ct));
        await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{Url}/{adjustment.Id}/submit", null, Ct));
        await ReadAsync<MonthlyApprovalDto>(await board.PostAsJsonAsync($"{Url}/{adjustment.Id}/approve", new ApprovalActionRequest(null), Ct));
        var old = await ReadAsync<MonthlyApprovalDto>(await head.GetAsync($"{Url}/{draft.Id}", Ct));
        Assert.Equal(("Replaced", adjustment.No), (old.Status, old.ReplacedByNo));

        // Chép sang tháng sau: nháp mới, cùng nhóm hàng, công ty và giá đã điều chỉnh.
        var next = await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{Url}/{adjustment.Id}/copy-next-month", null, Ct));
        Assert.Equal(("Draft", ThisMonth.AddMonths(1), 11_800m, (Guid?)null), (next.Status, next.Period, next.Lines[0].UnitPrice, next.ReplacesId));
        Assert.Contains("đã có trong phiếu", await TitleAsync(await head.PostAsync($"{Url}/{adjustment.Id}/copy-next-month", null, Ct)));
    }

    [Fact]
    public async Task TuChoi_VeNhapGiuLyDo_RutVe_Huy_NguoiKhongCoQuyenBiChan()
    {
        var (head, _) = await ManagerAsync();
        var main = await SupplierAsync(head, "NCC tháng từ chối");
        var (categoryId, item1, _, cai) = await CategoryAsync(head, "Hóa chất tháng");
        var tct = await _factory.GetCompanyIdAsync("TCT");
        var request = new SaveMonthlyApprovalRequest(ThisMonth.AddMonths(1), categoryId, [tct], null,
            [new SaveMonthlyApprovalLineRequest(null, item1, cai, main, 30_000m, null, null, null, "Không giới hạn")], "Hóa chất mới, một NCC");
        var draft = await ReadAsync<MonthlyApprovalDto>(await head.PostAsJsonAsync(Url, request, Ct));
        Assert.Null(draft.Lines[0].QuantityLimit);
        await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{Url}/{draft.Id}/submit", null, Ct));

        var outsider = await UserWithAsync(Permissions.Purchase.PrView);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(Url, Ct)).StatusCode);

        var board = await UserWithAsync(Permissions.Pricing.MonthlyApprovalView, Permissions.Pricing.MonthlyApprovalApprove);
        Assert.Contains("lý do", await TitleAsync(await board.PostAsJsonAsync($"{Url}/{draft.Id}/reject", new ApprovalActionRequest(null), Ct)));
        var rejected = await ReadAsync<MonthlyApprovalDto>(await board.PostAsJsonAsync($"{Url}/{draft.Id}/reject",
            new ApprovalActionRequest("Thiếu báo giá so sánh"), Ct));
        Assert.Equal(("Draft", "Thiếu báo giá so sánh"), (rejected.Status, rejected.RejectReason));

        await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{Url}/{draft.Id}/submit", null, Ct));
        Assert.Contains("chỉ hủy được", await TitleAsync(await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelMonthlyApprovalRequest("Không mua"), Ct)));
        Assert.Equal("Draft", (await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{Url}/{draft.Id}/recall", null, Ct))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelMonthlyApprovalRequest(""), Ct)).StatusCode);
        var cancelled = await ReadAsync<MonthlyApprovalDto>(await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel",
            new CancelMonthlyApprovalRequest("Tháng sau không mua nhóm này"), Ct));
        Assert.Equal(("Cancelled", Today), (cancelled.Status, cancelled.CancelledOn!.Value));

        // Phiếu đã hủy không chặn lập phiếu mới cùng mặt hàng × tháng.
        await ReadAsync<MonthlyApprovalDto>(await head.PostAsJsonAsync(Url, request, Ct));
    }

    [Fact]
    public async Task CoNgoaiLe_BatBuocGiaiTrinh_ThemChangChuTich_NguoiDuyetHoiDongKhongDuyetTiep()
    {
        var (head, _) = await ManagerAsync();
        var main = await SupplierAsync(head, "NCC chính ngoại lệ");
        var backup = await SupplierAsync(head, "NCC dự phòng ngoại lệ");
        var third = await SupplierAsync(head, "NCC thứ ba ngoại lệ");
        var (categoryId, item1, _, cai) = await CategoryAsync(head, "Phụ tùng ngoại lệ");
        await LinkAsync(head, third, item1, cai);
        // Đếm theo nhóm hàng (G1b): mặt hàng dòng 2 ở nhóm con – NCC thứ ba có dòng NCC – sản phẩm ở nhóm cha không tính cho nhóm con.
        var child = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm con ngoại lệ {UniqueCode("")}", categoryId, null), Ct));
        var item2 = (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Phụ tùng nhóm con", null, "Inventory", child.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, []), Ct))).Id;
        var tct = await _factory.GetCompanyIdAsync("TCT");
        // Dòng 1 đủ 3 NCC, giá không vượt mục tiêu; dòng 2 nhóm con chỉ có NCC chính + dự phòng, giá chính 12.000 vượt mục tiêu 11.500.
        SaveMonthlyApprovalRequest Request(string? exceptionNote) => new(ThisMonth.AddMonths(1), categoryId, [tct], null,
        [
            new(null, item1, cai, main, 12_000m, backup, 12_500m, null, null, null, null, 12_500m),
            new(null, item2, cai, main, 12_000m, backup, 11_000m, null, null, null, null, 11_500m),
        ], exceptionNote);
        var draft = await ReadAsync<MonthlyApprovalDto>(await head.PostAsJsonAsync(Url, Request(null), Ct));
        Assert.Empty(draft.Lines[0].Exceptions!);
        Assert.Equal(["Nhóm hàng chỉ có 2 NCC đủ điều kiện (tối thiểu 3)", "Giá NCC chính 12.000 vượt giá mục tiêu 11.500"], draft.Lines[1].Exceptions);

        // Chưa giải trình thì không gửi được (kể cả chạy thử gửi duyệt cho popup).
        Assert.Contains("giải trình ngoại lệ", await TitleAsync(await head.PostAsync($"{Url}/{draft.Id}/submit", null, Ct)));
        draft = await ReadAsync<MonthlyApprovalDto>(await head.PutAsJsonAsync($"{Url}/{draft.Id}", Request("Mặt hàng mới, giá thị trường tăng"), Ct));
        Assert.Equal("Mặt hàng mới, giá thị trường tăng", draft.ExceptionNote);

        // Gửi: chặng 1 Hội đồng (GTH) rồi chặng 2 Chủ tịch/TGĐ (GTX – chưa cấu hình thì một cấp).
        var submitted = await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{Url}/{draft.Id}/submit", null, Ct));
        Assert.True(submitted.HasException);
        Assert.Equal([1, 2], submitted.Approvals.Select(s => s.Stage).Order());
        Assert.Contains((await head.GetFromJsonAsync<List<MonthlyApprovalListItemDto>>(Url, Ct))!, r => r.Id == draft.Id && r.HasException);

        // Thành viên Hội đồng duyệt chặng 1; không duyệt tiếp chặng Chủ tịch (mỗi cấp một người – R3).
        var board = await UserWithAsync(Permissions.Pricing.MonthlyApprovalView, Permissions.Pricing.MonthlyApprovalApprove);
        var afterBoard = await ReadAsync<MonthlyApprovalDto>(await board.PostAsJsonAsync($"{Url}/{draft.Id}/approve", new ApprovalActionRequest("Hội đồng đồng ý"), Ct));
        Assert.Equal(("Submitted", false), (afterBoard.Status, afterBoard.Actions.CanApprove));
        Assert.Contains("người khác duyệt", await TitleAsync(await board.PostAsJsonAsync($"{Url}/{draft.Id}/approve", new ApprovalActionRequest(null), Ct)));

        // Chủ tịch/TGĐ thấy ở Việc cần làm và duyệt cấp cuối.
        var chairman = await UserWithAsync(Permissions.Pricing.MonthlyApprovalView, Permissions.Pricing.MonthlyApprovalApprove);
        Assert.Contains((await chairman.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == draft.Id);
        var approved = await ReadAsync<MonthlyApprovalDto>(await chairman.PostAsJsonAsync($"{Url}/{draft.Id}/approve", new ApprovalActionRequest("Phê duyệt ngoại lệ"), Ct));
        Assert.Equal(("Approved", true), (approved.Status, approved.HasException));
        Assert.All(approved.Lines, l => Assert.Empty(l.Exceptions!));

        // Loại duyệt GTX có trong APRL.
        Assert.Contains((await head.GetFromJsonAsync<List<ApprovalTypeDto>>("/api/approval-rules/types", Ct))!, t => t.Code == "GTX");
    }
}
