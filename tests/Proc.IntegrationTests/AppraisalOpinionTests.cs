using Proc.Application.Purchasing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Application.Documents;
using Proc.Application.MasterData;
using Proc.Application.Sourcing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Ý kiến thẩm định của thành viên chuyên môn (lát H1 – design.md mục 18.25): ghi Đạt/Không đạt + nội dung trên RFQ, hồ sơ NCC…; không phải cấp
/// duyệt; người lập không tự thẩm định; người ghi rút có lý do; chủ đã quyết thì khóa; đính kèm của ý kiến chỉ người ghi thêm.
/// </summary>
[Collection(ApiCollection.Name)]
public class AppraisalOpinionTests
{
    private const string Url = "/api/appraisal-opinions";
    private readonly ProcApiFactory _factory;

    public AppraisalOpinionTests(ProcApiFactory factory) => _factory = factory;

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

    /// <summary>Người dùng có đúng các quyền đã cho tại tổng công ty (vai trò thử tên "Vai trò thử {mã}").</summary>
    private async Task<(HttpClient Client, string RoleName)> UserWithAsync(params string[] permissions)
    {
        var roleCode = UniqueCode("TD");
        await _factory.CreateRoleAsync(roleCode, permissions);
        var userName = $"td.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", roleCode);
        return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT"), $"Vai trò thử {roleCode}");
    }

    private async Task<HttpClient> AdminAsync()
    {
        var userName = $"ta.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    private static Task<HttpResponseMessage> GiveAsync(HttpClient client, string ownerType, Guid ownerId, string aspect, string result, string? content) =>
        client.PostAsJsonAsync(Url, new GiveAppraisalOpinionRequest(ownerType, ownerId, aspect, result, content), Ct);

    private static async Task<AppraisalOpinionListDto> ListAsync(HttpClient client, string ownerType, Guid ownerId) =>
        await ReadAsync<AppraisalOpinionListDto>(await client.GetAsync($"{Url}?ownerType={ownerType}&ownerId={ownerId}", Ct));

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid opinionId)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("AppraisalOpinion"), "ownerType" },
            { new StringContent(opinionId.ToString()), "ownerId" },
        };
        var file = new ByteArrayContent("%PDF-1.4 biên bản thẩm định"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "bien-ban.pdf");
        return client.PostAsync("/api/attachments", form, Ct);
    }

    /// <summary>RFQ nháp một dòng hàng ở công ty đang làm việc của <paramref name="client"/>.</summary>
    private async Task<RfqDto> RfqAsync(HttpClient client, IReadOnlyList<Guid> supplierIds)
    {
        var cai = (await client.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await client.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm thẩm định {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await client.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Van an toàn H1", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, []), Ct));
        return await ReadAsync<RfqDto>(await client.PostAsJsonAsync("/api/rfqs", new SaveRfqRequest(Today, "Van an toàn", Today.AddDays(7), null,
            new RfqWeightsDto(60, 20, 10, 5, 5), null, [new(null, item.Id, cai, 10, null, null)], supplierIds), Ct));
    }

    [Fact]
    public async Task ThamDinhRfq_DatKhongDat_NguoiLapKhongTuThamDinh_RutCoLyDo_HuyThiKhoa()
    {
        var author = await AdminAsync();
        var groupId = (await author.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await author.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC thẩm định H1", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<SupplierCompanyDto>(await author.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var rfq = await RfqAsync(author, [supplier.Id]);

        var (legal, legalRole) = await UserWithAsync(
            Permissions.Sourcing.RfqView, Permissions.MasterData.SupplierView, Permissions.Sourcing.AppraisalGive);
        var (quality, _) = await UserWithAsync(
            Permissions.Sourcing.RfqView, Permissions.MasterData.SupplierView, Permissions.Sourcing.AppraisalGive);
        var (viewer, _) = await UserWithAsync(Permissions.Sourcing.RfqView);

        // Người lập RFQ không tự thẩm định (không kiêm nhiệm); nút ghi bị khóa kèm lý do.
        Assert.Contains("Người lập", await TitleAsync(await GiveAsync(author, "Rfq", rfq.Id, "Legal", "Pass", "Tự thẩm định")));
        Assert.Contains("Người lập", (await ListAsync(author, "Rfq", rfq.Id)).GiveBlockedReason);
        // Không có quyền ghi ý kiến: chỉ xem được.
        Assert.Equal(HttpStatusCode.Forbidden, (await GiveAsync(viewer, "Rfq", rfq.Id, "Legal", "Pass", "Không có quyền")).StatusCode);
        Assert.False((await ListAsync(viewer, "Rfq", rfq.Id)).CanGive);
        Assert.Contains("nội dung", await TitleAsync(await GiveAsync(legal, "Rfq", rfq.Id, "Legal", "Fail", " ")));

        var fail = await ReadAsync<AppraisalOpinionDto>(await GiveAsync(legal, "Rfq", rfq.Id, "Legal", "Fail", "Điều khoản phạt chậm giao chưa có"));
        Assert.Equal(legalRole, fail.AuthorRoles);
        Assert.True(fail.CanWithdraw);
        await ReadAsync<AppraisalOpinionDto>(await GiveAsync(quality, "Rfq", rfq.Id, "Quality", "Pass", "Mẫu van đạt áp lực thử"));

        // Người duyệt/người lập thấy tổng hợp trước khi duyệt: 1 Đạt, 1 Không đạt; không rút được ý kiến của người khác.
        var list = await ListAsync(author, "Rfq", rfq.Id);
        Assert.Equal((1, 1), (list.PassCount, list.FailCount));
        Assert.Equal(["Fail", "Pass"], list.Opinions.Select(o => o.Result));
        Assert.All(list.Opinions, o => Assert.False(o.CanWithdraw));
        Assert.Contains("người ghi", await TitleAsync(await author.PostAsJsonAsync($"{Url}/{fail.Id}/withdraw",
            new WithdrawAppraisalOpinionRequest("Không đồng ý"), Ct)));

        // Đính kèm bằng chứng của ý kiến: người ghi được, người khác không; người xem RFQ tải danh sách được.
        await ReadAsync<AttachmentDto>(await UploadAsync(legal, fail.Id));
        Assert.Contains("người ghi", await TitleAsync(await UploadAsync(quality, fail.Id)));
        Assert.Single(await ReadAsync<List<AttachmentDto>>(await viewer.GetAsync($"/api/attachments?ownerType=AppraisalOpinion&ownerId={fail.Id}", Ct)));

        // Rút: bắt buộc lý do; ý kiến đã rút không tính vào tổng hợp, vẫn hiện để tra cứu.
        Assert.Contains("lý do", await TitleAsync(await legal.PostAsJsonAsync($"{Url}/{fail.Id}/withdraw", new WithdrawAppraisalOpinionRequest(null), Ct)));
        var withdrawn = await ReadAsync<AppraisalOpinionDto>(await legal.PostAsJsonAsync($"{Url}/{fail.Id}/withdraw",
            new WithdrawAppraisalOpinionRequest("NCC đã bổ sung điều khoản"), Ct));
        Assert.True(withdrawn.IsWithdrawn);
        list = await ListAsync(viewer, "Rfq", rfq.Id);
        Assert.Equal((1, 0, 2), (list.PassCount, list.FailCount, list.Opinions.Count));

        // RFQ đã hủy (có quyết định cuối cùng): khóa ghi, rút ý kiến.
        await ReadAsync<RfqDto>(await author.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/cancel", new CancelDocumentRequest("Thử hủy"), Ct));
        Assert.Contains("quyết định cuối cùng", await TitleAsync(await GiveAsync(legal, "Rfq", rfq.Id, "Legal", "Pass", "Sau khi hủy")));
        var passId = list.Opinions.Single(o => o.Result == "Pass").Id;
        Assert.Contains("quyết định cuối cùng", await TitleAsync(await quality.PostAsJsonAsync($"{Url}/{passId}/withdraw",
            new WithdrawAppraisalOpinionRequest("Sau khi hủy"), Ct)));
        Assert.False((await ListAsync(quality, "Rfq", rfq.Id)).CanGive);

        // Hồ sơ NCC (danh mục dùng chung): thẩm định được; loại chủ không hỗ trợ thì báo lỗi.
        var onSupplier = await ReadAsync<AppraisalOpinionDto>(await GiveAsync(quality, "Supplier", supplier.Id, "Quality", "Pass", "Nhà máy đạt ISO 9001"));
        Assert.Equal("Pass", onSupplier.Result);
        Assert.Equal(1, (await ListAsync(legal, "Supplier", supplier.Id)).PassCount);
        Assert.Contains("không hợp lệ", await TitleAsync(await GiveAsync(quality, "PurchaseOrder", rfq.Id, "Quality", "Pass", "PO")));
    }

    [Fact]
    public async Task ThamDinh_ChuNgoaiPhamViCongTy_KhongThay()
    {
        var head = await AdminAsync();
        var month = new DateOnly(Today.Year, Today.Month, 1);
        var other = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("H"), "Công ty khác H1", null, null, null, null, month, "KT", "Kho tổng"), Ct));
        var userName = $"to.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, [other.Code]);
        var otherClient = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, other.Code);
        var rfq = await RfqAsync(head, []);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"{Url}?ownerType=Rfq&ownerId={rfq.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GiveAsync(otherClient, "Rfq", rfq.Id, "Legal", "Pass", "Ngoài phạm vi")).StatusCode);
    }
}
