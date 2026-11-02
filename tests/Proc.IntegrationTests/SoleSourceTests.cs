using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Phê duyệt NCC độc quyền (lát B5b, design.md mục 18.9 – đề xuất Ban CUVT N6): lập phiếu NCC × mặt hàng kèm giải trình và hạn đánh giá
/// lại, duyệt riêng theo quy trình NDQ (người lập không tự duyệt), kết thúc sớm, đánh giá lại; phiếu còn tác dụng thay cho giải trình khi
/// gửi RFQ mời ít NCC.
/// </summary>
[Collection(ApiCollection.Name)]
public class SoleSourceTests
{
    private readonly ProcApiFactory _factory;

    public SoleSourceTests(ProcApiFactory factory) => _factory = factory;

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

    /// <summary>Quản trị riêng của test (vai trò quản trị tại tổng công ty – có mọi quyền).</summary>
    private async Task<HttpClient> ManagerAsync()
    {
        var userName = $"dq.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    /// <summary>Người dùng có đúng các quyền đã cho tại tổng công ty.</summary>
    private async Task<HttpClient> UserWithAsync(params string[] permissions)
    {
        var roleCode = UniqueCode("D");
        await _factory.CreateRoleAsync(roleCode, permissions);
        var userName = $"du.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    /// <summary>NCC đã duyệt, áp dụng cho tổng công ty.</summary>
    private async Task<Guid> SupplierAsync(HttpClient head, string name)
    {
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<SupplierCompanyDto>(await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        return supplier.Id;
    }

    private async Task<(Guid ItemId, Guid UomId)> ItemAsync(HttpClient head, string name)
    {
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm {name}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, []), Ct));
        return (item.Id, cai);
    }

    private static SaveSoleSourceRequest Request(Guid supplierId, Guid itemId, DateOnly? from = null, DateOnly? review = null) =>
        new(supplierId, itemId, "Chỉ hãng này sản xuất phụ tùng thay thế cho máy hiện có", from ?? Today, review ?? (from ?? Today).AddMonths(6));

    [Fact]
    public async Task LapGuiDuyet_NguoiLapKhongTuDuyet_TrungThoiGianBiChan_KetThucVaDanhGiaLai()
    {
        var head = await ManagerAsync();
        var supplierId = await SupplierAsync(head, "NCC độc quyền");
        var (itemId, _) = await ItemAsync(head, "Phụ tùng máy nén");

        // Hạn đánh giá lại phải sau ngày hiệu lực và tối đa 12 tháng; giải trình bắt buộc.
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync("/api/sole-sources", Request(supplierId, itemId, Today, Today), Ct)).StatusCode);
        Assert.Contains("tối đa 12 tháng", await TitleAsync(await head.PostAsJsonAsync("/api/sole-sources",
            Request(supplierId, itemId, Today, Today.AddMonths(13)), Ct)));
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync("/api/sole-sources",
            Request(supplierId, itemId) with { Reason = " " }, Ct)).StatusCode);

        var draft = await ReadAsync<SoleSourceDto>(await head.PostAsJsonAsync("/api/sole-sources", Request(supplierId, itemId), Ct));
        Assert.StartsWith("NDQ", draft.No);
        Assert.Equal(("Draft", false, true), (draft.Status, draft.IsEffective, draft.Actions.CanEdit));
        Assert.Contains("trùng thời gian", await TitleAsync(await head.PostAsJsonAsync("/api/sole-sources",
            Request(supplierId, itemId, Today.AddMonths(1)), Ct)));

        // Người lập gửi duyệt, không tự duyệt; người duyệt thấy ở Việc cần làm và duyệt.
        var submitted = await ReadAsync<SoleSourceDto>(await head.PostAsync($"/api/sole-sources/{draft.Id}/submit", null, Ct));
        Assert.Equal(("Submitted", false), (submitted.Status, submitted.Actions.CanApprove));
        Assert.Contains("không tự duyệt", await TitleAsync(await head.PostAsJsonAsync($"/api/sole-sources/{draft.Id}/approve",
            new ApprovalActionRequest(null), Ct)));
        var approver = await UserWithAsync(Permissions.MasterData.SoleSourceView, Permissions.MasterData.SoleSourceApprove);
        Assert.Contains((await approver.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!,
            p => p.DocumentId == draft.Id && p.DocType == "NDQ");
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostAsJsonAsync("/api/sole-sources", Request(supplierId, itemId), Ct)).StatusCode);
        var approved = await ReadAsync<SoleSourceDto>(await approver.PostAsJsonAsync($"/api/sole-sources/{draft.Id}/approve",
            new ApprovalActionRequest("Đồng ý"), Ct));
        Assert.Equal(("Approved", true, false), (approved.Status, approved.IsEffective, approved.Actions.CanEdit));
        Assert.Contains(approved.Approvals, a => a.Status == "Approved");
        var row = (await head.GetFromJsonAsync<List<SoleSourceListItemDto>>("/api/sole-sources", Ct))!.Single(r => r.Id == draft.Id);
        Assert.Equal((true, (Today.AddMonths(6).DayNumber - Today.DayNumber)), (row.IsEffective, row.DaysToReview!.Value));

        // Đánh giá lại: phiếu nháp mới nối tiếp ngay sau hạn cũ, hạn mới +12 tháng; chỉ một phiếu đánh giá lại.
        var renewal = await ReadAsync<SoleSourceDto>(await head.PostAsync($"/api/sole-sources/{draft.Id}/renew", null, Ct));
        Assert.Equal(("Draft", approved.ReviewDate.AddDays(1), approved.ReviewDate.AddDays(1).AddMonths(12), approved.No),
            (renewal.Status, renewal.ValidFrom, renewal.ReviewDate, renewal.RenewedFromNo));
        Assert.Contains("đã có phiếu đánh giá lại", await TitleAsync(await head.PostAsync($"/api/sole-sources/{draft.Id}/renew", null, Ct)));
        Assert.Equal(renewal.Id, (await ReadAsync<SoleSourceDto>(await head.GetAsync($"/api/sole-sources/{draft.Id}", Ct))).RenewedById);

        // Kết thúc sớm bắt buộc lý do; sau đó hết tác dụng.
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"/api/sole-sources/{draft.Id}/end", new EndSoleSourceRequest(""), Ct)).StatusCode);
        var ended = await ReadAsync<SoleSourceDto>(await head.PostAsJsonAsync($"/api/sole-sources/{draft.Id}/end",
            new EndSoleSourceRequest("Đã có nhà phân phối thứ hai"), Ct));
        Assert.Equal(("Ended", false, Today), (ended.Status, ended.IsEffective, ended.EndedOn!.Value));
    }

    [Fact]
    public async Task TuChoi_VeNhapGiuLyDo_NguoiKhongCoQuyenXemBiChan()
    {
        var head = await ManagerAsync();
        var supplierId = await SupplierAsync(head, "NCC độc quyền từ chối");
        var (itemId, _) = await ItemAsync(head, "Chip điều khiển");
        var draft = await ReadAsync<SoleSourceDto>(await head.PostAsJsonAsync("/api/sole-sources", Request(supplierId, itemId), Ct));
        await ReadAsync<SoleSourceDto>(await head.PostAsync($"/api/sole-sources/{draft.Id}/submit", null, Ct));

        var outsider = await UserWithAsync(Permissions.Purchase.PrView);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/sole-sources", Ct)).StatusCode);

        var approver = await UserWithAsync(Permissions.MasterData.SoleSourceView, Permissions.MasterData.SoleSourceApprove);
        Assert.Contains("lý do", await TitleAsync(await approver.PostAsJsonAsync($"/api/sole-sources/{draft.Id}/reject",
            new ApprovalActionRequest(null), Ct)));
        var rejected = await ReadAsync<SoleSourceDto>(await approver.PostAsJsonAsync($"/api/sole-sources/{draft.Id}/reject",
            new ApprovalActionRequest("Chưa có văn bản xác nhận độc quyền của hãng"), Ct));
        Assert.Equal(("Draft", "Chưa có văn bản xác nhận độc quyền của hãng"), (rejected.Status, rejected.RejectReason));

        // Sửa rồi gửi lại được; rút về khi đang chờ.
        var updated = await ReadAsync<SoleSourceDto>(await head.PutAsJsonAsync($"/api/sole-sources/{draft.Id}",
            Request(supplierId, itemId) with { Reason = "Hãng xác nhận độc quyền tại Việt Nam (văn bản đính kèm)" }, Ct));
        Assert.StartsWith("Hãng xác nhận", updated.Reason);
        await ReadAsync<SoleSourceDto>(await head.PostAsync($"/api/sole-sources/{draft.Id}/submit", null, Ct));
        Assert.Equal("Draft", (await ReadAsync<SoleSourceDto>(await head.PostAsync($"/api/sole-sources/{draft.Id}/recall", null, Ct))).Status);
    }

    [Fact]
    public async Task RfqMoiMotNcc_CoPhieuDocQuyenConTacDung_KhongCanGiaiTrinh()
    {
        var head = await ManagerAsync();
        var supplierId = await SupplierAsync(head, "NCC độc quyền thầu");
        var (itemId, cai) = await ItemAsync(head, "Cảm biến áp suất");
        var (otherItemId, _) = await ItemAsync(head, "Van an toàn");
        async Task<RfqDto> CreateRfqAsync(params Guid[] items) =>
            await ReadAsync<RfqDto>(await head.PostAsJsonAsync("/api/rfqs", new SaveRfqRequest(Today, "Mua độc quyền", Today.AddDays(7), null,
                new RfqWeightsDto(60, 20, 10, 5, 5), null, items.Select(i => new SaveRfqLineRequest(null, i, cai, 5, null, null)).ToList(),
                [supplierId]), Ct));

        // Chưa có phiếu duyệt: mời 1 NCC thì phải giải trình.
        var rfq = await CreateRfqAsync(itemId);
        Assert.Contains("phải ghi giải trình", await TitleAsync(await head.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct)));

        var sole = await ReadAsync<SoleSourceDto>(await head.PostAsJsonAsync("/api/sole-sources", Request(supplierId, itemId), Ct));
        await ReadAsync<SoleSourceDto>(await head.PostAsync($"/api/sole-sources/{sole.Id}/submit", null, Ct));
        var approver = await UserWithAsync(Permissions.MasterData.SoleSourceView, Permissions.MasterData.SoleSourceApprove);
        await ReadAsync<SoleSourceDto>(await approver.PostAsJsonAsync($"/api/sole-sources/{sole.Id}/approve", new ApprovalActionRequest(null), Ct));

        // Có phiếu còn tác dụng cho mọi dòng: gửi được, giải trình tự ghi số phiếu.
        rfq = await ReadAsync<RfqDto>(await head.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct));
        Assert.Equal(("Sent", $"NCC độc quyền đã duyệt: {sole.No}"), (rfq.Status, rfq.FewSuppliersReason));

        // Một dòng không có phiếu độc quyền thì vẫn phải giải trình.
        var mixed = await CreateRfqAsync(itemId, otherItemId);
        Assert.Contains("phải ghi giải trình", await TitleAsync(await head.PostAsync($"/api/rfqs/{mixed.Id}/send", null, Ct)));
    }
}
