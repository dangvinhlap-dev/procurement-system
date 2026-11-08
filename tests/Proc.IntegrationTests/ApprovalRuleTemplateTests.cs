using System.Net;
using System.Net.Http.Json;
using Proc.Application.Administration;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Purchasing;

namespace Proc.IntegrationTests;

/// <summary>
/// Cấu hình duyệt mẫu theo sơ đồ phê duyệt mua sắm (lát G10, design.md mục 18.25): xem trước, áp cho công ty đang làm việc – chỉ tạo loại phiếu
/// chưa cấu hình, phiếu chung của Ban (GTH, CKB, hồ sơ NCC) chỉ áp ở tổng công ty, gọi lại không tạo trùng.
/// </summary>
[Collection(ApiCollection.Name)]
public class ApprovalRuleTemplateTests
{
    private readonly ProcApiFactory _factory;

    public ApprovalRuleTemplateTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task CauHinhDuyetMau_ApChoCongTyMoi_ChiLoaiChuaCauHinh_GoiLaiKhongTrung()
    {
        var userName = $"ar.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        // Vai trò mẫu phải có trước (CSDL test dùng chung có thể đã có).
        await ReadAsync<ApplyRoleTemplatesResult>(await head.PostAsync("/api/roles/templates", null, Ct));

        var code = $"R{Guid.NewGuid():N}".ToUpperInvariant()[..10];
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));
        await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(code, "Công ty thử cấu hình duyệt mẫu", null, null, null, null, new DateOnly(today.Year, today.Month, 1), "KT", "Kho tổng"), Ct));
        var member = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, code);

        // Công ty mới: mọi loại chưa cấu hình → sẽ tạo; phiếu chung của Ban chỉ áp ở tổng công ty.
        var preview = await ReadAsync<List<ApprovalRuleTemplateDto>>(await member.GetAsync("/api/approval-rules/template", Ct));
        Assert.Equal(ApprovalRuleTemplates.Procurement.Count, preview.Count);
        Assert.All(preview.Where(r => r.DocType is "GTH" or "CKB" or "NCC"), r => Assert.Equal("HeadOfficeOnly", r.Status));
        Assert.All(preview.Where(r => r.DocType is not ("GTH" or "CKB" or "NCC")), r => Assert.Equal("Create", r.Status));
        Assert.Contains(preview, r => r is { DocType: "PO", Level: 2, RoleCode: "PHOBAN" } && r.RoleName == "Phó ban cung ứng (Hội đồng)");

        // Có sẵn cấp PR do người quản trị cấu hình → giữ nguyên PR, không thêm cấp mẫu.
        var roles = await ReadAsync<List<RoleListItemDto>>(await member.GetAsync("/api/roles", Ct));
        var existing = await ReadAsync<ApprovalRuleDto>(await member.PostAsJsonAsync("/api/approval-rules",
            new SaveApprovalRuleRequest("PR", 1, 0, roles.Single(r => r.Code == "TRUONGBANCUVT").Id), Ct));

        var applied = await ReadAsync<ApplyApprovalRuleTemplateResult>(await member.PostAsync("/api/approval-rules/template", null, Ct));
        var expected = ApprovalRuleTemplates.Procurement.Count(t => !t.HeadOfficeOnly && t.DocType != "PR");
        Assert.Equal(expected, applied.Created);
        Assert.All(applied.Rows.Where(r => !(r.DocType is "GTH" or "CKB" or "NCC")), r => Assert.Equal("Configured", r.Status));

        var rules = await ReadAsync<List<ApprovalRuleDto>>(await member.GetAsync("/api/approval-rules", Ct));
        Assert.Equal([(1, "PHOBAN", 0m), (2, "PHOBAN", 0m), (3, "CHUTICH", 0m)],
            rules.Where(r => r.DocType == "PO").OrderBy(r => r.Level).Select(r => (r.Level, r.RoleCode, r.MinAmount)));
        Assert.Equal([existing.Id], rules.Where(r => r.DocType == "PR").Select(r => r.Id));
        Assert.DoesNotContain(rules, r => r.DocType == "GTH");

        // Gọi lại: không tạo trùng.
        Assert.Equal(0, (await ReadAsync<ApplyApprovalRuleTemplateResult>(await member.PostAsync("/api/approval-rules/template", null, Ct))).Created);

        // Chỉ người có quyền sửa tham số mới áp được.
        var viewer = $"av.{Guid.NewGuid():N}"[..16];
        var viewRole = $"V{Guid.NewGuid():N}".ToUpperInvariant()[..10];
        await _factory.CreateRoleAsync(viewRole, Proc.Domain.Organization.Permissions.System.ParameterView);
        await _factory.CreateScopedUserAsync(viewer, code, viewRole);
        var viewerClient = await _factory.CreateClientInCompanyAsync(viewer, ProcApiFactory.TestPassword, code);
        Assert.Equal(HttpStatusCode.OK, (await viewerClient.GetAsync("/api/approval-rules/template", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.PostAsync("/api/approval-rules/template", null, Ct)).StatusCode);
    }
}
