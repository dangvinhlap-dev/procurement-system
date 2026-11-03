using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Duyệt NCC hai chặng (design.md mục 18.6, yêu cầu người dùng 01/11/2026): công ty liên quan (đang mua của NCC) duyệt xong – hai công ty
/// thì cả hai – rồi mới tới Tổng công ty duyệt lần cuối; duyệt xong mới áp trạng thái / tài khoản ngân hàng vào NCC.
/// </summary>
[Collection(ApiCollection.Name)]
public partial class SupplierApprovalTests
{
    private readonly ProcApiFactory _factory;

    public SupplierApprovalTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..8];

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Người quản trị riêng của mỗi test (xUnit tạo lớp mới cho từng test).</summary>
    private string? _managerUserName;

    /// <summary>
    /// Đăng nhập người quản trị riêng của test tại một công ty. Không dùng tài khoản <c>admin</c>: công ty do admin tạo làm lệch số công ty
    /// mà <c>AuthTests</c> đếm (test chỉ qua nhờ thứ tự chạy). Người tạo công ty được làm việc ở công ty đó như các test khác.
    /// </summary>
    private async Task<HttpClient> AdminAtAsync(string companyCode)
    {
        if (_managerUserName is null)
        {
            _managerUserName = $"dn.{Guid.NewGuid():N}"[..16];
            await _factory.CreateUserAsync(_managerUserName, ProcApiFactory.TestPassword, ["TCT"]);
        }
        return await _factory.CreateClientInCompanyAsync(_managerUserName, ProcApiFactory.TestPassword, companyCode);
    }

    /// <summary>Công ty thành viên mới (mã ngẫu nhiên) – không lẫn dữ liệu với test khác.</summary>
    private async Task<string> NewCompanyAsync(HttpClient head)
    {
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("D"), "Công ty thử duyệt NCC", null, null, null, null, null, "KT", "Kho tổng"), Ct));
        return company.Code;
    }

    /// <summary>Người có quyền duyệt NCC tại một công ty (vai trò riêng của test).</summary>
    private async Task<HttpClient> ApproverAtAsync(string companyCode)
    {
        var roleCode = UniqueCode("R");
        await _factory.CreateRoleAsync(roleCode, Permissions.MasterData.SupplierView, Permissions.MasterData.SupplierApprove);
        var userName = $"nd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, companyCode, roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, companyCode);
    }

    private static async Task<SupplierDetailDto> NewSupplierAsync(HttpClient head)
    {
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        return await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, $"NCC duyệt {Guid.NewGuid():N}"[..20], null, false, null, null, null, null, null, null, null, null,
                "Potential", null, null, [], [new(null, "Vietcombank", null, "0011 2233", null, true)]), Ct));
    }

    private static SaveSupplierRequest Edit(SupplierDetailDto d) =>
        new(null, d.GroupId, d.Name, d.ShortName, d.IsForeign, d.TaxCode, d.Address, d.Phone, d.Email, d.Website, d.PaymentTerms,
            d.SupplyScope, d.Rating, d.Status, d.StatusNote, d.InternalCompanyId, d.Contacts, d.BankAccounts);

    private static Task<HttpResponseMessage> ActAsync(HttpClient client, Guid id, string action, string? comment = null) =>
        client.PostAsJsonAsync($"/api/supplier-change-requests/{id}/{action}", new ApprovalActionRequest(comment), Ct);

    [Fact]
    public async Task DuyetTrangThai_HaiCongTyLienQuanDuyetXong_RoiTongCongTyDuyetCuoi()
    {
        var head = await AdminAtAsync("TCT");
        var (c1, c2) = (await NewCompanyAsync(head), await NewCompanyAsync(head));
        var supplier = await NewSupplierAsync(head);
        foreach (var code in new[] { c1, c2 })
            await ReadAsync<SupplierCompanyDto>(await (await AdminAtAsync(code)).PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var (approver1, approver2, approverTct) = (await ApproverAtAsync(c1), await ApproverAtAsync(c2), await ApproverAtAsync("TCT"));

        // Lưu NCC với trạng thái Đã duyệt: NCC chưa đổi, sinh yêu cầu chờ hai công ty liên quan.
        var saved = await ReadAsync<SupplierDetailDto>(await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}", Edit(supplier) with { Status = "Approved" }, Ct));
        Assert.Equal("Potential", saved.Status);
        var request = Assert.Single(saved.PendingRequests!);
        Assert.Equal(("Status", "Tiềm năng", "Đã duyệt"), (request.Kind, request.CurrentSummary, request.ProposedSummary));
        Assert.Equal(string.Join(", ", new[] { c1, c2 }.Order(StringComparer.Ordinal)), request.WaitingFor);
        Assert.Contains("đang có yêu cầu đổi trạng thái chờ duyệt",
            await TitleAsync(await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}", Edit(saved) with { Status = "Approved" }, Ct)));

        // Tổng công ty chưa đến lượt; người lập không tự duyệt.
        Assert.Contains("chưa đến lượt", await TitleAsync(await ActAsync(approverTct, request.Id, "approve")));
        Assert.DoesNotContain((await approverTct.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == request.Id);
        Assert.Equal("Người lập không tự duyệt phiếu của mình.", await TitleAsync(await ActAsync(await AdminAtAsync(c1), request.Id, "approve")));

        // Công ty 1 duyệt: còn chờ công ty 2; công ty 2 duyệt: chuyển Tổng công ty.
        Assert.Contains((await approver1.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == request.Id && p.DocType == "NCC");
        var afterC1 = await ReadAsync<SupplierChangeRequestDto>(await ActAsync(approver1, request.Id, "approve", "Đồng ý"));
        Assert.Equal(c2, afterC1.WaitingFor);
        Assert.Contains("chưa đến lượt", await TitleAsync(await ActAsync(approverTct, request.Id, "approve")));
        var afterC2 = await ReadAsync<SupplierChangeRequestDto>(await ActAsync(approver2, request.Id, "approve"));
        Assert.Equal("TCT", afterC2.WaitingFor);
        Assert.Contains((await approverTct.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == request.Id);

        var done = await ReadAsync<SupplierChangeRequestDto>(await ActAsync(approverTct, request.Id, "approve", "Duyệt lần cuối"));
        Assert.Equal(("Approved", (string?)null), (done.Status, done.WaitingFor));
        Assert.Equal(new[] { (1, c1), (1, c2), (2, "TCT") }.OrderBy(a => a.Item1).ThenBy(a => a.Item2, StringComparer.Ordinal),
            done.Approvals.Select(a => (a.Stage, a.CompanyCode!)).OrderBy(a => a.Item1).ThenBy(a => a.Item2, StringComparer.Ordinal));
        Assert.All(done.Approvals, a => Assert.Equal("Approved", a.Status));
        var after = await head.GetFromJsonAsync<SupplierDetailDto>($"/api/suppliers/{supplier.Id}", Ct);
        Assert.Equal("Approved", after!.Status);
        Assert.Empty(after.PendingRequests!);
    }

    [Fact]
    public async Task DoiTaiKhoan_CongTyLienQuanTuChoi_KetThucYeuCau_TaiKhoanGiuNguyen()
    {
        var head = await AdminAtAsync("TCT");
        var c1 = await NewCompanyAsync(head);
        var supplier = await NewSupplierAsync(head);
        await ReadAsync<SupplierCompanyDto>(await (await AdminAtAsync(c1)).PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var (approver1, approverTct) = (await ApproverAtAsync(c1), await ApproverAtAsync("TCT"));

        var saved = await ReadAsync<SupplierDetailDto>(await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}",
            Edit(supplier) with { BankAccounts = [new(null, "ACB", "Hà Nội", "9988 7766", "CTY MOI", true)] }, Ct));
        Assert.Equal("00112233", Assert.Single(saved.BankAccounts).AccountNumber);
        var request = Assert.Single(saved.PendingRequests!);
        Assert.Equal(("BankAccounts", "Vietcombank 00112233 (mặc định)", "ACB 99887766 (mặc định)", c1),
            (request.Kind, request.CurrentSummary, request.ProposedSummary, request.WaitingFor));

        Assert.Equal("Phải nhập lý do từ chối.", await TitleAsync(await ActAsync(approver1, request.Id, "reject")));
        var rejected = await ReadAsync<SupplierChangeRequestDto>(await ActAsync(approver1, request.Id, "reject", "Chưa có xác nhận của NCC"));
        Assert.Equal(("Rejected", "Chưa có xác nhận của NCC"), (rejected.Status, rejected.RejectReason));
        Assert.Contains(rejected.Approvals, a => a.CompanyCode == "TCT" && a.Status == "Skipped");
        Assert.Contains("không ở trạng thái chờ duyệt", await TitleAsync(await ActAsync(approverTct, request.Id, "approve")));
        var after = await head.GetFromJsonAsync<SupplierDetailDto>($"/api/suppliers/{supplier.Id}", Ct);
        Assert.Equal("00112233", Assert.Single(after!.BankAccounts).AccountNumber);
    }

    [Fact]
    public async Task KhongCongTyNaoLienQuan_ChiTongCongTyDuyet_NguoiLapRutDuoc()
    {
        var head = await AdminAtAsync("TCT");
        var approverTct = await ApproverAtAsync("TCT");
        var supplier = await NewSupplierAsync(head);

        var first = Assert.Single((await ReadAsync<SupplierDetailDto>(await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}",
            Edit(supplier) with { Status = "Blacklisted", StatusNote = "Hàng giả" }, Ct))).PendingRequests!);
        Assert.Equal("TCT", first.WaitingFor);
        Assert.True(first.CanCancel);
        var cancelled = await ReadAsync<SupplierChangeRequestDto>(await head.PostAsync($"/api/supplier-change-requests/{first.Id}/cancel", null, Ct));
        Assert.Equal("Cancelled", cancelled.Status);

        var second = Assert.Single((await ReadAsync<SupplierDetailDto>(await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}",
            Edit(supplier) with { Status = "Blacklisted", StatusNote = "Hàng giả" }, Ct))).PendingRequests!);
        Assert.NotEqual(first.No, second.No);
        await ReadAsync<SupplierChangeRequestDto>(await ActAsync(approverTct, second.Id, "approve"));
        var after = await head.GetFromJsonAsync<SupplierDetailDto>($"/api/suppliers/{supplier.Id}", Ct);
        Assert.Equal(("Blacklisted", "Hàng giả"), (after!.Status, after.StatusNote));

        // Tạm ngưng không cần duyệt (chặn mua kịp thời) – áp ngay.
        var suspended = await ReadAsync<SupplierDetailDto>(await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}",
            Edit(after) with { Status = "Suspended", StatusNote = "Đang khiếu nại" }, Ct));
        Assert.Equal("Suspended", suspended.Status);
        Assert.Empty(suspended.PendingRequests!);
    }
}
