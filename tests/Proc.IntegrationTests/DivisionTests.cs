using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Administration;
using Proc.Application.Common;
using Proc.Application.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Khối – Trưởng phòng ban – Giám đốc khối – người dùng thuộc bộ phận (lát O1, design.md mục 19.3). Mỗi test một công ty mới để danh mục
/// không lẫn nhau.
/// </summary>
[Collection(ApiCollection.Name)]
public class DivisionTests
{
    private readonly ProcApiFactory _factory;

    public DivisionTests(ProcApiFactory factory) => _factory = factory;

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

    /// <summary>Công ty mới; người thao tác là quản trị của công ty đó; thêm hai tài khoản có quyền tại công ty.</summary>
    private async Task<(HttpClient Client, string CompanyCode, Guid Head, Guid Director)> ArrangeAsync()
    {
        var userName = $"kh.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("K"), "Công ty thử khối", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var manager = await _factory.CreateUserAsync($"tp.{Guid.NewGuid():N}"[..16], ProcApiFactory.TestPassword, [company.Code]);
        var director = await _factory.CreateUserAsync($"gd.{Guid.NewGuid():N}"[..16], ProcApiFactory.TestPassword, [company.Code]);
        return (client, company.Code, manager, director);
    }

    [Fact]
    public async Task Khoi_GiamDocKhoi_TruongPhongBan_ChiTaiKhoanCoQuyenTaiCongTy()
    {
        var (client, _, managerId, directorId) = await ArrangeAsync();
        var options = (await client.GetFromJsonAsync<List<OrganizationUserOptionDto>>("/api/departments/user-options", Ct))!;
        Assert.Contains(options, o => o.Id == managerId);
        Assert.Contains(options, o => o.Id == directorId);

        var division = await ReadAsync<DivisionDto>(await client.PostAsJsonAsync("/api/divisions",
            new CreateDivisionRequest(" hcns ", "Khối HCNS-CNTT", directorId), Ct));
        Assert.Equal(("HCNS", directorId, true, 0), (division.Code, division.DirectorUserId, division.IsActive, division.DepartmentCount));
        Assert.StartsWith("Người thử gd.", division.DirectorName);
        var duplicate = await client.PostAsJsonAsync("/api/divisions", new CreateDivisionRequest("HCNS", "Trùng", null), Ct);
        Assert.Equal("Mã khối HCNS đã có ở công ty này.", await TitleAsync(duplicate));

        // Tài khoản không có vai trò tại công ty không làm được Giám đốc khối / Trưởng phòng ban – phiếu sẽ kẹt ở cấp không ai duyệt được.
        var outsider = await _factory.CreateUserAsync($"ng.{Guid.NewGuid():N}"[..16], ProcApiFactory.TestPassword, ["BHA"]);
        Assert.DoesNotContain(options, o => o.Id == outsider);
        var badDirector = await client.PutAsJsonAsync($"/api/divisions/{division.Id}", new UpdateDivisionRequest("Khối HCNS-CNTT", outsider, true), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, badDirector.StatusCode);
        Assert.Equal("Giám đốc khối phải là tài khoản còn hoạt động và đã được phân quyền tại công ty đang làm việc.", await TitleAsync(badDirector));
        var badManager = await client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("HC", "Phòng HC", null, division.Id, outsider), Ct);
        Assert.Equal("Trưởng phòng ban phải là tài khoản còn hoạt động và đã được phân quyền tại công ty đang làm việc.", await TitleAsync(badManager));

        var department = await ReadAsync<DepartmentDto>(await client.PostAsJsonAsync("/api/departments",
            new CreateDepartmentRequest("HC", "Phòng hành chính", null, division.Id, managerId), Ct));
        Assert.Equal((division.Id, "HCNS – Khối HCNS-CNTT", managerId), (department.DivisionId, department.DivisionName, department.ManagerUserId));
        Assert.StartsWith("Người thử tp.", department.ManagerUserName);
        Assert.Equal(division.DirectorName, department.DirectorName);
        Assert.Equal(1, (await client.GetFromJsonAsync<DivisionDto>($"/api/divisions/{division.Id}", Ct))!.DepartmentCount);

        // Khối ngừng dùng: bộ phận đang gán giữ khối (sửa tên vẫn được), bộ phận khác không gán mới được.
        await ReadAsync<DivisionDto>(await client.PutAsJsonAsync($"/api/divisions/{division.Id}", new UpdateDivisionRequest("Khối HCNS", directorId, false), Ct));
        department = await ReadAsync<DepartmentDto>(await client.PutAsJsonAsync($"/api/departments/{department.Id}",
            new UpdateDepartmentRequest("Phòng HC", null, true, division.Id, managerId), Ct));
        Assert.Equal(division.Id, department.DivisionId);
        var inactive = await client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("KT", "Phòng KT", null, division.Id, null), Ct);
        Assert.Equal("Khối HCNS đã ngừng dùng.", await TitleAsync(inactive));

        // Khối của công ty khác như không tồn tại.
        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.NotFound, (await bha.GetAsync($"/api/divisions/{division.Id}", Ct)).StatusCode);
        Assert.DoesNotContain((await bha.GetFromJsonAsync<List<DivisionDto>>("/api/divisions", Ct))!, d => d.Id == division.Id);
        var foreign = await bha.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest(UniqueCode("X"), "BP BHA", null, division.Id, null), Ct);
        Assert.Equal("Khối không thuộc công ty đang làm việc.", await TitleAsync(foreign));
    }

    [Fact]
    public async Task NguoiDung_ThuocBoPhan_TheoCongTy_DienSanKhiLapPr()
    {
        var (client, companyCode, managerId, _) = await ArrangeAsync();
        var department = await ReadAsync<DepartmentDto>(await client.PostAsJsonAsync("/api/departments",
            new CreateDepartmentRequest("BEP", "Bếp trung tâm", null), Ct));
        var detail = (await client.GetFromJsonAsync<UserDetailDto>($"/api/users/{managerId}", Ct))!;
        Assert.Null(detail.Access.DepartmentId);
        var options = (await client.GetFromJsonAsync<UserAccessOptionsDto>("/api/users/access-options", Ct))!;
        Assert.Contains(options.Departments!, d => d.Id == department.Id);

        detail = await ReadAsync<UserDetailDto>(await client.PutAsJsonAsync($"/api/users/{managerId}/access",
            detail.Access with { DepartmentId = department.Id }, Ct));
        Assert.Equal(department.Id, detail.Access.DepartmentId);
        Assert.Equal(1, (await client.GetFromJsonAsync<DepartmentDto>($"/api/departments/{department.Id}", Ct))!.MemberCount);

        var userName = (await client.GetFromJsonAsync<UserDetailDto>($"/api/users/{managerId}", Ct))!.UserName;
        var own = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, companyCode);
        Assert.Equal(department.Id, (await own.GetFromJsonAsync<MineResult>("/api/departments/mine", Ct))!.DepartmentId);
        // Công ty khác: chưa gán bộ phận.
        var tct = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        Assert.Null((await tct.GetFromJsonAsync<MineResult>("/api/departments/mine", Ct))!.DepartmentId);

        // Bộ phận ngừng dùng: không điền sẵn nữa; bỏ hết vai trò tại công ty thì bỏ luôn bộ phận.
        await ReadAsync<DepartmentDto>(await client.PutAsJsonAsync($"/api/departments/{department.Id}",
            new UpdateDepartmentRequest(department.Name, null, false), Ct));
        Assert.Null((await own.GetFromJsonAsync<MineResult>("/api/departments/mine", Ct))!.DepartmentId);
        detail = await ReadAsync<UserDetailDto>(await client.PutAsJsonAsync($"/api/users/{managerId}/access",
            detail.Access with { RoleIds = [] }, Ct));
        Assert.Null(detail.Access.DepartmentId);
    }

    /// <summary>Kết quả GET /api/departments/mine.</summary>
    private sealed record MineResult(Guid? DepartmentId);
}
