using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Công trình và dự toán (lát Y2, design.md mục 19.2): tạo kèm dự toán ban đầu, mã duy nhất trong công ty, điều chỉnh dự toán ghi lịch sử,
/// công trình hoàn thành không điều chỉnh / không chọn được, phạm vi công ty và quyền xem / quản lý. Mỗi test một công ty mới.
/// </summary>
[Collection(ApiCollection.Name)]
public class ConstructionProjectTests
{
    private readonly ProcApiFactory _factory;

    public ConstructionProjectTests(ProcApiFactory factory) => _factory = factory;

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

    /// <summary>Công ty mới (người dùng là quản trị) + bộ phận QLXD.</summary>
    private async Task<(HttpClient Client, string CompanyCode, DepartmentDto Department)> ArrangeAsync()
    {
        var userName = $"ct.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var tct = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await tct.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("C"), "Công ty thử công trình", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var department = await ReadAsync<DepartmentDto>(await client.PostAsJsonAsync("/api/departments",
            new CreateDepartmentRequest("QLXD", "Phòng Quản lý xây dựng", null), Ct));
        return (client, company.Code, department);
    }

    [Fact]
    public async Task TaoCongTrinh_DieuChinhDuToan_GhiLichSu_HoanThanhThiKhongDieuChinh()
    {
        var (client, _, department) = await ArrangeAsync();

        var created = await ReadAsync<ConstructionProjectDto>(await client.PostAsJsonAsync("/api/construction-projects",
            new CreateConstructionProjectRequest("ct-2026-01", "Thi công Vua Trời Phạm Thiên", department.Id, "Khu A", Today, Today.AddMonths(3),
                64_000_000m, "QĐ 12/QĐ-CT", "Ghi chú thử"), Ct));
        Assert.Equal("CT-2026-01", created.Code);
        Assert.Equal(64_000_000m, created.EstimateAmount);
        Assert.Equal("InProgress", created.Status);
        Assert.Equal("QLXD", created.DepartmentCode);
        var initial = Assert.Single(created.Revisions);
        Assert.Equal(0, initial.RevisionNo);
        Assert.Equal("QĐ 12/QĐ-CT", initial.Basis);

        // Mã trùng trong cùng công ty bị chặn (không phân biệt hoa thường – mã tự viết hoa).
        var duplicate = await client.PostAsJsonAsync("/api/construction-projects",
            new CreateConstructionProjectRequest("CT-2026-01", "Trùng mã", department.Id, null, null, null, 1_000_000m, null, null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("đã có", await TitleAsync(duplicate));

        // Điều chỉnh tăng: dòng lịch sử lần 1, chênh +6.000.000.
        var revised = await ReadAsync<ConstructionProjectDto>(await client.PostAsJsonAsync(
            $"/api/construction-projects/{created.Id}/estimate-revisions",
            new ReviseConstructionEstimateRequest(70_000_000m, Today, "QĐ 15/QĐ-CT", "Bổ sung hạng mục mái che"), Ct));
        Assert.Equal(70_000_000m, revised.EstimateAmount);
        Assert.Equal(64_000_000m, revised.InitialEstimateAmount);
        Assert.Equal(1, revised.RevisionCount);
        var revision = revised.Revisions.Single(r => r.RevisionNo == 1);
        Assert.Equal(64_000_000m, revision.PreviousAmount);
        Assert.Equal(6_000_000m, revision.Difference);
        Assert.Equal("Bổ sung hạng mục mái che", revision.Reason);

        // Không đổi số tiền, thiếu lý do: bị chặn.
        Assert.Contains("phải khác", await TitleAsync(await client.PostAsJsonAsync($"/api/construction-projects/{created.Id}/estimate-revisions",
            new ReviseConstructionEstimateRequest(70_000_000m, Today, null, "Không đổi"), Ct)));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/construction-projects/{created.Id}/estimate-revisions",
            new ReviseConstructionEstimateRequest(80_000_000m, Today, null, ""), Ct)).StatusCode);

        // Hoàn thành: không điều chỉnh dự toán, không còn trong danh sách chọn trên phiếu; dự toán giữ nguyên.
        var completed = await ReadAsync<ConstructionProjectDto>(await client.PutAsJsonAsync($"/api/construction-projects/{created.Id}",
            new UpdateConstructionProjectRequest("Thi công Vua Trời Phạm Thiên", department.Id, "Khu A", Today, Today.AddMonths(3), "Completed", null), Ct));
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(70_000_000m, completed.EstimateAmount);
        Assert.Contains("đang thực hiện", await TitleAsync(await client.PostAsJsonAsync($"/api/construction-projects/{created.Id}/estimate-revisions",
            new ReviseConstructionEstimateRequest(90_000_000m, Today, null, "Thử"), Ct)));
        var options = await client.GetFromJsonAsync<List<ConstructionProjectOptionDto>>("/api/construction-projects/options", Ct);
        Assert.DoesNotContain(options!, o => o.Id == created.Id);
        var list = await client.GetFromJsonAsync<List<ConstructionProjectDto>>("/api/construction-projects", Ct);
        Assert.Contains(list!, p => p.Id == created.Id && p.RevisionCount == 1);

        // Ngày dự kiến hoàn thành trước ngày khởi công: bị chặn.
        Assert.Contains("không được trước", await TitleAsync(await client.PostAsJsonAsync("/api/construction-projects",
            new CreateConstructionProjectRequest("CT-2026-02", "Sai ngày", department.Id, null, Today, Today.AddDays(-1), 1_000_000m, null, null), Ct)));
    }

    [Fact]
    public async Task CongTrinh_TheoCongTy_VaQuyenXemQuanLy()
    {
        var (client, companyCode, department) = await ArrangeAsync();
        var project = await ReadAsync<ConstructionProjectDto>(await client.PostAsJsonAsync("/api/construction-projects",
            new CreateConstructionProjectRequest("CT-01", "Nhà kho mới", department.Id, null, null, null, 500_000_000m, null, null), Ct));

        // Công ty khác (TCT) không thấy công trình, không gắn được phòng ban của công ty này.
        var adminName = $"ct2.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(adminName, ProcApiFactory.TestPassword, ["TCT"]);
        var tct = await _factory.CreateClientInCompanyAsync(adminName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.NotFound, (await tct.GetAsync($"/api/construction-projects/{project.Id}", Ct)).StatusCode);
        Assert.DoesNotContain((await tct.GetFromJsonAsync<List<ConstructionProjectDto>>("/api/construction-projects", Ct))!, p => p.Id == project.Id);
        var foreignDepartment = await tct.PostAsJsonAsync("/api/construction-projects",
            new CreateConstructionProjectRequest("CT-01", "Nhà kho mới", department.Id, null, null, null, 1_000_000m, null, null), Ct);
        Assert.Contains("không thuộc công ty", await TitleAsync(foreignDepartment));

        // Chỉ quyền xem: đọc được, không tạo / điều chỉnh được.
        var viewerRole = $"CTV{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await _factory.CreateRoleAsync(viewerRole, Permissions.Purchase.ProjectView);
        var viewerName = $"ctv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, companyCode, viewerRole);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, companyCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/construction-projects/{project.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"/api/construction-projects/{project.Id}/estimate-revisions",
            new ReviseConstructionEstimateRequest(600_000_000m, Today, null, "Thử"), Ct)).StatusCode);

        // Người lập đề nghị mua: chỉ lấy danh sách chọn, không xem danh mục.
        var requesterRole = $"CTR{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await _factory.CreateRoleAsync(requesterRole, Permissions.Purchase.PrView, Permissions.Purchase.PrCreate);
        var requesterName = $"ctr.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(requesterName, companyCode, requesterRole);
        var requester = await _factory.CreateClientInCompanyAsync(requesterName, ProcApiFactory.TestPassword, companyCode);
        var options = await requester.GetFromJsonAsync<List<ConstructionProjectOptionDto>>("/api/construction-projects/options", Ct);
        Assert.Contains(options!, o => o.Id == project.Id && o.EstimateAmount == 500_000_000m);
        Assert.Equal(HttpStatusCode.Forbidden, (await requester.GetAsync("/api/construction-projects", Ct)).StatusCode);
    }
}
