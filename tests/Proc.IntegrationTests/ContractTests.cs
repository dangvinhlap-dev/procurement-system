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
/// Hợp đồng nguyên tắc phương án 2 (lát B4a – design.md mục 18.7): mỗi công ty một hợp đồng với NCC, dùng chung bảng giá toàn tập đoàn;
/// sản lượng dự kiến lấy giá theo bảng giá; duyệt loại CTR; không chồng hiệu lực.
/// </summary>
[Collection(ApiCollection.Name)]
public partial class ContractTests
{
    private readonly ProcApiFactory _factory;

    public ContractTests(ProcApiFactory factory) => _factory = factory;

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

    private sealed record Fixture(
        HttpClient Head, HttpClient Client, string CompanyCode, Guid ItemId, Guid Cai, Guid Thung, Guid SupplierId, Guid OtherSupplierId,
        Guid GroupPriceListId, Guid DepartmentId);

    /// <summary>
    /// Công ty mới; mặt hàng (cái, thùng = 12 cái); NCC A đã duyệt áp dụng tại công ty; bảng giá toàn tập đoàn của A đã hiệu lực
    /// (cái 50.000 CK 5%; thùng 600.000); NCC B (Tiềm năng); một bộ phận. Người dùng: quản trị (không dùng admin – AuthTests đếm công ty).
    /// </summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"hd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("H"), "Công ty thử hợp đồng", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(head,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm hợp đồng {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Vòng bi 6306", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var a = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC hợp đồng A", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(a.Id);
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{a.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var b = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC hợp đồng B", null, false, null, null, null, null, null, null, null, null, "Potential", null, null, [], []), Ct));
        var list = await ReadAsync<PriceListDto>(await head.PostAsJsonAsync("/api/price-lists",
            new SavePriceListRequest(a.Id, "Bảng giá vòng bi tập đoàn", true, Today, Today.AddYears(1), null, null,
                [new(null, item.Id, cai, 0, 50_000, 5, null, null), new(null, item.Id, cai, 1_000, 48_000, 5, null, null), new(null, item.Id, thung, 0, 600_000, 0, null, null)]), Ct));
        await ReadAsync<PriceListDto>(await head.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        await _factory.FinishPriceListApprovalAsync(list.Id);
        var department = await ReadAsync<DepartmentDto>(await client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("PX1", "Phân xưởng 1", null), Ct));
        return new Fixture(head, client, company.Code, item.Id, cai, thung, a.Id, b.Id, list.Id, department.Id);
    }

    private static SaveContractRequest Contract(Fixture f, DateOnly from, DateOnly to, params SaveContractLineRequest[] lines) =>
        new(f.SupplierId, f.GroupPriceListId, "HĐNT 01/2026", "Hợp đồng nguyên tắc vòng bi", Today, from, to, "Điều chỉnh theo quý", "30 ngày",
            500_000_000, "Giao tại kho tổng", "2% doanh số năm", null, "Phạt 0,1%/ngày chậm giao", null, null, [f.DepartmentId], lines);

    /// <summary>Người duyệt hợp đồng tại công ty: chỉ có quyền xem và duyệt hợp đồng.</summary>
    private async Task<HttpClient> ApproverAsync(Fixture f)
    {
        var roleCode = UniqueCode("D");
        await _factory.CreateRoleAsync(roleCode, Permissions.Purchase.ContractView, Permissions.Purchase.ContractApprove);
        var userName = $"dh.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);
    }

    [Fact]
    public async Task HopDong_DungChungBangGiaTapDoan_DuyetThiHieuLuc()
    {
        var f = await ArrangeAsync();
        // Mặt hàng × ĐVT không có trong bảng giá thì chặn; NCC khác bảng giá thì chặn.
        var kg = (await f.Head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.First(u => u.Code != "CAI" && u.Code != "THUNG").Id;
        Assert.Contains("không có trong bảng giá", await TitleAsync(await f.Client.PostAsJsonAsync("/api/contracts",
            Contract(f, Today, Today.AddMonths(6), new SaveContractLineRequest(null, f.ItemId, kg, 1, null, null)), Ct)));
        Assert.Contains("không phải của NCC", await TitleAsync(await f.Client.PostAsJsonAsync("/api/contracts",
            Contract(f, Today, Today.AddMonths(6)) with { SupplierId = f.OtherSupplierId }, Ct)));

        // Giá theo bậc cơ bản của bảng giá: 1.000 cái × 50.000 × 95% = 47.500.000; 10 thùng × 600.000 = 6.000.000.
        var contract = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", Contract(f, Today, Today.AddMonths(6),
            new SaveContractLineRequest(null, f.ItemId, f.Cai, 1_000, 200, "Dự kiến 6 tháng"), new SaveContractLineRequest(null, f.ItemId, f.Thung, 10, null, null)), Ct));
        Assert.Matches($"^CTR-{f.CompanyCode}-{Today:yyMM}-\\d{{4}}$", contract.No);
        Assert.Equal(("Draft", 53_500_000m, 50_000m, 5m), (contract.Status, contract.ExpectedAmount, contract.Lines[0].UnitPrice, contract.Lines[0].DiscountPct));
        Assert.Equal(["PX1 – Phân xưởng 1"], contract.DepartmentNames);
        Assert.Equal("Active", contract.PriceListStatus);

        // Gửi duyệt; người lập không tự duyệt; người duyệt (chỉ xem + duyệt) từ chối bắt buộc lý do → về nháp.
        contract = await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.Id}/submit", null, Ct));
        Assert.Equal(("Submitted", false), (contract.Status, contract.Actions.CanApprove));
        Assert.Contains("không tự duyệt", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/approve", new ApprovalActionRequest(null), Ct)));
        var approver = await ApproverAsync(f);
        Assert.True((await ReadAsync<ContractDto>(await approver.GetAsync($"/api/contracts/{contract.Id}", Ct))).Actions.CanApprove);
        Assert.Equal(HttpStatusCode.BadRequest, (await approver.PostAsJsonAsync($"/api/contracts/{contract.Id}/reject", new ApprovalActionRequest(null), Ct)).StatusCode);
        contract = await ReadAsync<ContractDto>(await approver.PostAsJsonAsync($"/api/contracts/{contract.Id}/reject", new ApprovalActionRequest("Thiếu biên bản đàm phán"), Ct));
        Assert.Equal(("Draft", "Thiếu biên bản đàm phán"), (contract.Status, contract.RejectReason));

        // Việc cần làm của người duyệt có loại CTR.
        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.Id}/submit", null, Ct));
        var pending = await ReadAsync<List<PendingApprovalDto>>(await approver.GetAsync("/api/approvals/pending", Ct));
        Assert.Contains(pending, p => p.DocType == "CTR" && p.DocumentId == contract.Id && p.Amount == 53_500_000m);
        contract = await ReadAsync<ContractDto>(await approver.PostAsJsonAsync($"/api/contracts/{contract.Id}/approve", new ApprovalActionRequest("Đồng ý"), Ct));
        Assert.Equal("Active", contract.Status);

        // Hợp đồng thứ hai cùng NCC trùng thời gian: lưu nháp được, gửi duyệt bị chặn; nối tiếp sau ngày hết hạn thì gửi được.
        var second = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", Contract(f, Today.AddMonths(3), Today.AddYears(1)), Ct));
        Assert.Contains("trùng thời gian", await TitleAsync(await f.Client.PostAsync($"/api/contracts/{second.Id}/submit", null, Ct)));
        await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync($"/api/contracts/{second.Id}/cancel", new CancelDocumentRequest("Thử hủy"), Ct));
        var next = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", Contract(f, Today.AddMonths(6).AddDays(1), Today.AddYears(1)), Ct));
        Assert.Equal("Submitted", (await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{next.Id}/submit", null, Ct))).Status);

        // Chấm dứt: bắt buộc lý do; danh sách thấy trạng thái.
        Assert.Contains("lý do", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/terminate", new TerminateContractRequest(Today, " "), Ct)));
        contract = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/terminate", new TerminateContractRequest(Today, "NCC vi phạm chất lượng"), Ct));
        Assert.Equal(("Terminated", Today), (contract.Status, contract.TerminatedOn));
        var list = await ReadAsync<List<ContractListItemDto>>(await f.Client.GetAsync("/api/contracts", Ct));
        Assert.Equal(["Submitted", "Cancelled", "Terminated"], list.OrderByDescending(c => c.ValidFrom).ThenByDescending(c => c.No).Select(c => c.Status));
    }

    /// <summary>Phụ lục điền sẵn giá trị hiện hành của hợp đồng (người dùng chỉ đổi phần cần đổi).</summary>
    private static SaveContractAmendmentRequest Amendment(ContractDto c) =>
        new("PL 01/2026", "Gia hạn và đổi bảng giá theo đàm phán", Today, c.ValidTo, c.PriceListId, c.PriceAdjustmentTerms, c.PaymentTerms, c.CreditLimit,
            c.DeliveryTerms, c.DiscountTerms, c.SponsorshipTerms, c.PenaltyTerms, c.OtherTerms, null);

    /// <summary>Bảng giá toàn tập đoàn khác của NCC A, đã hiệu lực.</summary>
    private async Task<Guid> GroupPriceListAsync(Fixture f, string title, params SavePriceListLineRequest[] lines)
    {
        var list = await ReadAsync<PriceListDto>(await f.Head.PostAsJsonAsync("/api/price-lists",
            new SavePriceListRequest(f.SupplierId, title, true, Today, Today.AddYears(1), null, null, lines), Ct));
        await ReadAsync<PriceListDto>(await f.Head.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        await _factory.FinishPriceListApprovalAsync(list.Id);
        return list.Id;
    }

    [Fact]
    public async Task PhuLuc_GiaHan_DoiBangGia_DoiDieuKhoan_DuyetThiApVaoHopDong()
    {
        var f = await ArrangeAsync();
        var approver = await ApproverAsync(f);
        var contract = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", Contract(f, Today, Today.AddMonths(6),
            new SaveContractLineRequest(null, f.ItemId, f.Cai, 1_000, null, null)), Ct));
        // Hợp đồng chưa hiệu lực: không lập phụ lục.
        Assert.False(contract.Actions.CanAmend);
        Assert.Contains("không ở trạng thái hiệu lực", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/amendments",
            Amendment(contract) with { NewValidTo = Today.AddYears(1) }, Ct)));
        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.Id}/submit", null, Ct));
        // Người duyệt (không có quyền sửa) không lập phụ lục được; người lập thì được.
        Assert.False((await ReadAsync<ContractDto>(await approver.PostAsJsonAsync($"/api/contracts/{contract.Id}/approve", new ApprovalActionRequest(null), Ct))).Actions.CanAmend);
        contract = await ReadAsync<ContractDto>(await f.Client.GetAsync($"/api/contracts/{contract.Id}", Ct));
        Assert.Equal(("Active", true, 47_500_000m), (contract.Status, contract.Actions.CanAmend, contract.ExpectedAmount));

        // Không đổi gì thì chặn; bảng giá mới thiếu mặt hàng của hợp đồng thì chặn.
        Assert.Contains("chưa thay đổi gì", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/amendments", Amendment(contract), Ct)));
        var thungOnly = await GroupPriceListAsync(f, "Bảng giá chỉ có thùng", new SavePriceListLineRequest(null, f.ItemId, f.Thung, 0, 580_000, 0, null, null));
        Assert.Contains("không có trong bảng giá", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/amendments",
            Amendment(contract) with { NewPriceListId = thungOnly }, Ct)));

        // Gia hạn 1 năm, đổi bảng giá (cái 48.000 CK 5%), đổi thanh toán + hạn mức: 1.000 × 48.000 × 95% = 45.600.000.
        var newList = await GroupPriceListAsync(f, "Bảng giá vòng bi 2027", new SavePriceListLineRequest(null, f.ItemId, f.Cai, 0, 48_000, 5, null, null));
        var amendment = await ReadAsync<ContractAmendmentDto>(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/amendments",
            Amendment(contract) with { NewValidTo = Today.AddYears(1), NewPriceListId = newList, PaymentTerms = "45 ngày", CreditLimit = 800_000_000 }, Ct));
        Assert.Equal(($"{contract.No}-PL01", "Draft", 45_600_000m, 47_500_000m), (amendment.No, amendment.Status, amendment.ExpectedAmount, amendment.CurrentExpectedAmount));
        Assert.Contains($"Gia hạn đến {Today.AddYears(1):dd/MM/yyyy}", amendment.ChangeSummary);
        Assert.Contains("Đổi bảng giá", amendment.ChangeSummary);
        Assert.Contains("Đổi thanh toán, hạn mức công nợ", amendment.ChangeSummary);
        // Công ty khác không thấy phụ lục.
        Assert.Equal(HttpStatusCode.NotFound, (await f.Head.GetAsync($"/api/contract-amendments/{amendment.Id}", Ct)).StatusCode);

        // Đang có phụ lục mở: không lập phụ lục khác, không chấm dứt hợp đồng.
        Assert.Contains("đang có phụ lục", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/amendments",
            Amendment(contract) with { PaymentTerms = "60 ngày" }, Ct)));
        contract = await ReadAsync<ContractDto>(await f.Client.GetAsync($"/api/contracts/{contract.Id}", Ct));
        Assert.Equal((false, false, "Draft"), (contract.Actions.CanAmend, contract.Actions.CanTerminate, contract.Amendments.Single().Status));
        Assert.Contains("đang có phụ lục", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/terminate",
            new TerminateContractRequest(Today, "Thử"), Ct)));

        // Gửi duyệt; người lập không tự duyệt; người duyệt thấy ở Việc cần làm loại CTA; từ chối về nháp, gửi lại, duyệt.
        amendment = await ReadAsync<ContractAmendmentDto>(await f.Client.PostAsync($"/api/contract-amendments/{amendment.Id}/submit", null, Ct));
        Assert.Equal(("Submitted", false), (amendment.Status, amendment.Actions.CanApprove));
        Assert.Contains("không tự duyệt", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/contract-amendments/{amendment.Id}/approve", new ApprovalActionRequest(null), Ct)));
        var pending = await ReadAsync<List<PendingApprovalDto>>(await approver.GetAsync("/api/approvals/pending", Ct));
        Assert.Contains(pending, p => p.DocType == "CTA" && p.DocumentId == amendment.Id && p.Amount == 45_600_000m);
        amendment = await ReadAsync<ContractAmendmentDto>(await approver.PostAsJsonAsync($"/api/contract-amendments/{amendment.Id}/reject",
            new ApprovalActionRequest("Thiếu biên bản đàm phán giá"), Ct));
        Assert.Equal(("Draft", "Thiếu biên bản đàm phán giá"), (amendment.Status, amendment.RejectReason));
        await ReadAsync<ContractAmendmentDto>(await f.Client.PostAsync($"/api/contract-amendments/{amendment.Id}/submit", null, Ct));
        amendment = await ReadAsync<ContractAmendmentDto>(await approver.PostAsJsonAsync($"/api/contract-amendments/{amendment.Id}/approve",
            new ApprovalActionRequest("Đồng ý"), Ct));
        Assert.Equal("Approved", amendment.Status);

        // Duyệt xong: hợp đồng đổi hiệu lực, bảng giá, điều khoản; giá dòng lấy lại theo bảng mới.
        contract = await ReadAsync<ContractDto>(await f.Client.GetAsync($"/api/contracts/{contract.Id}", Ct));
        Assert.Equal((Today.AddYears(1), newList, "45 ngày", 800_000_000m, 45_600_000m, 48_000m),
            (contract.ValidTo, contract.PriceListId, contract.PaymentTerms, contract.CreditLimit, contract.ExpectedAmount, contract.Lines[0].UnitPrice));
        Assert.Equal((true, "Approved"), (contract.Actions.CanAmend, contract.Amendments.Single().Status));

        // Gia hạn chồng lên hợp đồng khác (chờ duyệt) của cùng NCC thì gửi bị chặn; hủy phụ lục nháp thì lập được phụ lục mới (số PL03).
        var next = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", Contract(f, Today.AddYears(1).AddDays(1), Today.AddYears(2)) with { PriceListId = newList }, Ct));
        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{next.Id}/submit", null, Ct));
        var extend = await ReadAsync<ContractAmendmentDto>(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/amendments",
            Amendment(contract) with { NewValidTo = Today.AddYears(1).AddMonths(3) }, Ct));
        Assert.Contains("trùng thời gian", await TitleAsync(await f.Client.PostAsync($"/api/contract-amendments/{extend.Id}/submit", null, Ct)));
        Assert.Equal("Cancelled", (await ReadAsync<ContractAmendmentDto>(await f.Client.PostAsJsonAsync($"/api/contract-amendments/{extend.Id}/cancel", new CancelDocumentRequest("Thử hủy"), Ct))).Status);
        var terms = await ReadAsync<ContractAmendmentDto>(await f.Client.PostAsJsonAsync($"/api/contracts/{contract.Id}/amendments",
            Amendment(contract) with { PenaltyTerms = "Phạt 0,2%/ngày chậm giao" }, Ct));
        Assert.Equal(($"{contract.No}-PL03", "Đổi chế tài"), (terms.No, terms.ChangeSummary));
    }

    [Fact]
    public async Task GuiDuyet_NccChuaDuyet_BiChan_CongTyKhacKhongThay()
    {
        var f = await ArrangeAsync();
        var contract = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", Contract(f, Today, Today.AddMonths(6)), Ct));
        // Công ty khác (tổng công ty) không thấy hợp đồng của công ty thành viên.
        Assert.Equal(HttpStatusCode.NotFound, (await f.Head.GetAsync($"/api/contracts/{contract.Id}", Ct)).StatusCode);

        // NCC chuyển Tạm ngưng (áp ngay) → gửi duyệt bị chặn.
        var supplier = await ReadAsync<SupplierDetailDto>(await f.Head.GetAsync($"/api/suppliers/{f.SupplierId}", Ct));
        Assert.Equal("Approved", supplier.Status);
        var groupId = (await f.Head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        await ReadAsync<SupplierDetailDto>(await f.Head.PutAsJsonAsync($"/api/suppliers/{f.SupplierId}",
            new SaveSupplierRequest(supplier.Code, groupId, "NCC hợp đồng A", null, false, null, null, null, null, null, null, null, null, "Suspended", "Tạm ngưng thử", null, [], []), Ct));
        Assert.Contains("chưa ở trạng thái Đã duyệt", await TitleAsync(await f.Client.PostAsync($"/api/contracts/{contract.Id}/submit", null, Ct)));

        // Người không có quyền sửa: chỉ xem.
        var roleCode = UniqueCode("X");
        await _factory.CreateRoleAsync(roleCode, Permissions.Purchase.ContractView);
        var userName = $"xh.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        var viewer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);
        var seen = await ReadAsync<ContractDto>(await viewer.GetAsync($"/api/contracts/{contract.Id}", Ct));
        Assert.False(seen.Actions.CanEdit);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/contracts/{contract.Id}/submit", null, Ct)).StatusCode);
    }
}
