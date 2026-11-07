using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Kê khai xung đột lợi ích (lát B5a, design.md mục 18.8 – đề xuất Ban CUVT N4): ai cũng tự kê khai, chỉ xem kê khai của mình; KSNB xem
/// toàn bộ; ghi thay và kết thúc cần quyền quản lý. Kê khai còn hiệu lực chặn duyệt (không báo, không hiện ở Việc cần làm) và chặn nhập
/// báo giá, chọn thầu RFQ có NCC đó.
/// </summary>
[Collection(ApiCollection.Name)]
public class ConflictDeclarationTests
{
    private readonly ProcApiFactory _factory;

    public ConflictDeclarationTests(ProcApiFactory factory) => _factory = factory;

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
        var userName = $"xd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    /// <summary>Người dùng có đúng các quyền đã cho tại tổng công ty.</summary>
    private async Task<(HttpClient Client, Guid UserId)> UserWithAsync(params string[] permissions)
    {
        var roleCode = UniqueCode("X");
        await _factory.CreateRoleAsync(roleCode, permissions);
        var userName = $"xu.{Guid.NewGuid():N}"[..16];
        var userId = await _factory.CreateScopedUserAsync(userName, "TCT", roleCode);
        return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT"), userId);
    }

    /// <summary>NCC đã duyệt dùng chung.</summary>
    private async Task<Guid> SupplierAsync(HttpClient head, string name)
    {
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        return supplier.Id;
    }

    private static Task<HttpResponseMessage> DeclareAsync(HttpClient client, Guid supplierId, Guid? userId = null, DateOnly? from = null) =>
        client.PostAsJsonAsync("/api/conflict-declarations",
            new CreateConflictDeclarationRequest(userId, supplierId, "Family", "Em trai là giám đốc kinh doanh", from), Ct);

    [Fact]
    public async Task KeKhai_TuKeKhai_PhamViXem_GhiThayVaKetThucCanQuyenQuanLy()
    {
        var head = await ManagerAsync();
        var supplierId = await SupplierAsync(head, "NCC kê khai");
        var (staff, staffId) = await UserWithAsync(Permissions.Purchase.PrView);
        var (other, otherId) = await UserWithAsync(Permissions.Purchase.PrView);
        var (auditor, _) = await UserWithAsync(Permissions.MasterData.ConflictView);

        // Tự kê khai không cần quyền; đang hiệu lực từ hôm nay; trùng kê khai đang mở bị chặn.
        var mine = await ReadAsync<ConflictDeclarationDto>(await DeclareAsync(staff, supplierId));
        Assert.Equal((staffId, "Family", Today, true), (mine.UserId, mine.Relation, mine.ValidFrom, mine.IsActive));
        Assert.Contains("đã có kê khai chưa kết thúc", await TitleAsync(await DeclareAsync(staff, supplierId)));
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync("/api/conflict-declarations",
            new CreateConflictDeclarationRequest(null, supplierId, "Khac", "x", null), Ct)).StatusCode);

        // Không ghi thay, không kết thúc, không xem kê khai của người khác khi thiếu quyền.
        Assert.Equal(HttpStatusCode.Forbidden, (await DeclareAsync(staff, supplierId, otherId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/conflict-declarations/{mine.Id}/end",
            new EndConflictDeclarationRequest(Today, "Hết quan hệ"), Ct)).StatusCode);
        Assert.DoesNotContain((await other.GetFromJsonAsync<List<ConflictDeclarationDto>>("/api/conflict-declarations", Ct))!, d => d.Id == mine.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/conflict-declarations/{mine.Id}", Ct)).StatusCode);

        // KSNB xem toàn bộ nhưng không ghi thay được; quản lý ghi thay và kết thúc (bắt buộc lý do).
        Assert.Contains((await auditor.GetFromJsonAsync<List<ConflictDeclarationDto>>("/api/conflict-declarations", Ct))!, d => d.Id == mine.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await DeclareAsync(auditor, supplierId, otherId)).StatusCode);
        var recorded = await ReadAsync<ConflictDeclarationDto>(await DeclareAsync(head, supplierId, otherId, Today.AddDays(5)));
        Assert.Equal((otherId, false), (recorded.UserId, recorded.IsActive));
        Assert.Contains(await head.GetFromJsonAsync<List<ConflictUserOptionDto>>("/api/conflict-declarations/users", Ct) ?? [], u => u.Id == otherId);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"/api/conflict-declarations/{mine.Id}/end",
            new EndConflictDeclarationRequest(Today, ""), Ct)).StatusCode);
        var ended = await ReadAsync<ConflictDeclarationDto>(await head.PostAsJsonAsync($"/api/conflict-declarations/{mine.Id}/end",
            new EndConflictDeclarationRequest(Today.AddDays(-1), "Em trai đã nghỉ việc ở NCC"), Ct));
        Assert.Equal((Today.AddDays(-1), "Em trai đã nghỉ việc ở NCC", false), (ended.ValidTo, ended.EndReason, ended.IsActive));
        Assert.Contains("đã kết thúc", await TitleAsync(await head.PostAsJsonAsync($"/api/conflict-declarations/{mine.Id}/end",
            new EndConflictDeclarationRequest(Today, "Lần nữa"), Ct)));
    }

    [Fact]
    public async Task KeKhaiConHieuLuc_ChanDuyetBangGia_KetThucThiDuyetDuoc()
    {
        var head = await ManagerAsync();
        var supplierId = await SupplierAsync(head, "NCC có quan hệ");
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm kê khai {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bulong M12", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, []), Ct));
        var list = await ReadAsync<PriceListDto>(await head.PostAsJsonAsync("/api/price-lists",
            new SavePriceListRequest(supplierId, "Bảng giá bulong", false, Today, null, null, null, [new(null, item.Id, cai, 0, 1_000, 0, null, null)]), Ct));
        var (approver, _) = await UserWithAsync(Permissions.Pricing.PriceListView, Permissions.Pricing.PriceListApprove);
        var declaration = await ReadAsync<ConflictDeclarationDto>(await DeclareAsync(approver, supplierId));

        await ReadAsync<PriceListDto>(await head.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        // Không hiện ở Việc cần làm, không có nút Duyệt, gọi thẳng API bị chặn kèm mã NCC.
        Assert.DoesNotContain((await approver.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == list.Id);
        Assert.False((await ReadAsync<PriceListDto>(await approver.GetAsync($"/api/price-lists/{list.Id}", Ct))).Actions.CanApprove);
        Assert.Contains("kê khai quan hệ lợi ích với NCC", await TitleAsync(await approver.PostAsJsonAsync($"/api/price-lists/{list.Id}/approve",
            new ApprovalActionRequest(null), Ct)));
        Assert.Contains("kê khai quan hệ lợi ích", await TitleAsync(await approver.PostAsJsonAsync($"/api/price-lists/{list.Id}/reject",
            new ApprovalActionRequest("Giá cao"), Ct)));

        // Kết thúc kê khai từ hôm qua → hôm nay duyệt được.
        await ReadAsync<ConflictDeclarationDto>(await head.PostAsJsonAsync($"/api/conflict-declarations/{declaration.Id}/end",
            new EndConflictDeclarationRequest(Today.AddDays(-1), "Hết quan hệ"), Ct));
        Assert.Contains((await approver.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == list.Id);
        Assert.Equal("Active", (await ReadAsync<PriceListDto>(await approver.PostAsJsonAsync($"/api/price-lists/{list.Id}/approve",
            new ApprovalActionRequest(null), Ct))).Status);
    }

    [Fact]
    public async Task KeKhaiVoiNccDuThau_ChanNhapBaoGiaVaChonThau()
    {
        var head = await ManagerAsync();
        var suppliers = new List<Guid>();
        foreach (var name in new[] { "A", "B", "C" })
        {
            var id = await SupplierAsync(head, $"NCC thầu kê khai {name}");
            await ReadAsync<SupplierCompanyDto>(await head.PutAsJsonAsync($"/api/suppliers/{id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            suppliers.Add(id);
        }

        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm thầu kê khai {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Gioăng cao su", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, []), Ct));
        var rfq = await ReadAsync<RfqDto>(await head.PostAsJsonAsync("/api/rfqs", new SaveRfqRequest(Today, "Gioăng", Today.AddDays(7), null,
            new RfqWeightsDto(60, 20, 10, 5, 5), null, [new(null, item.Id, cai, 10, null, null)], suppliers), Ct));
        rfq = await ReadAsync<RfqDto>(await head.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct));
        var lineId = rfq.Lines.Single().Id;
        var quote = new SaveQuotationRequest(Today, Today.AddDays(30), null, 8, 8, 8, 8, null, [new(lineId, 5_000, 0, null, null, null)]);
        rfq = await ReadAsync<RfqDto>(await head.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{suppliers[0]}", quote, Ct));
        Assert.Null(rfq.Actions.ConflictReason);

        // Người chấm thầu kê khai quan hệ với NCC B (chưa trúng): vẫn bị chặn nhập báo giá mọi NCC và chọn thầu.
        await ReadAsync<ConflictDeclarationDto>(await head.PostAsJsonAsync("/api/conflict-declarations",
            new CreateConflictDeclarationRequest(null, suppliers[1], "Ownership", "Góp 10% vốn", null), Ct));
        rfq = await ReadAsync<RfqDto>(await head.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        Assert.Contains("kê khai quan hệ lợi ích", rfq.Actions.ConflictReason);
        Assert.False(rfq.Actions.CanQuote);
        Assert.False(rfq.Actions.CanAward);
        Assert.Contains("kê khai quan hệ lợi ích", await TitleAsync(await head.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{suppliers[2]}", quote, Ct)));
        Assert.Contains("kê khai quan hệ lợi ích", await TitleAsync(await head.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award",
            new AwardRfqRequest([new(lineId, suppliers[0], null)], "Thử nghiệm: NCC khác không chào giá"), Ct)));

        // Người khác không vướng kê khai thì chọn thầu được.
        var colleague = await ManagerAsync();
        rfq = await ReadAsync<RfqDto>(await colleague.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", new AwardRfqRequest([new(lineId, suppliers[0], null)], "Thử nghiệm: NCC khác không chào giá"), Ct));
        Assert.Equal("AwardPending", rfq.Status);
    }
}
