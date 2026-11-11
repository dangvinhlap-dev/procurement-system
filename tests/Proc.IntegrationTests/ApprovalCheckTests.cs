using System.Net;
using System.Net.Http.Json;
using Proc.Application.Administration;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// "Kiểm tra phê duyệt" (lát Z2, design.md mục 20.1 R4, quyết định D5): mỗi bước duyệt có tên đăng nhập, họ tên, chức vụ, thời điểm của
/// người duyệt và người gửi; người dùng có trường Chức vụ, chưa nhập thì hiện tên vai trò tại công ty của bước.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task KiemTraPheDuyet_TenDangNhap_ChucVu_RongThiLayVaiTro()
    {
        var f = await ArrangeAsync();
        var admin = f.Buyer; // người lập công ty thử là quản trị của công ty đó

        var headCode = UniqueCode("T");
        var headRole = await _factory.CreateRoleAsync(headCode, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var directorCode = UniqueCode("G");
        var directorRole = await _factory.CreateRoleAsync(directorCode, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var headLogin = $"kt.{Guid.NewGuid():N}"[..16];
        var headId = await _factory.CreateScopedUserAsync(headLogin, f.CompanyCode, headCode);
        var directorLogin = $"kt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(directorLogin, f.CompanyCode, directorCode);
        var head = await _factory.CreateClientInCompanyAsync(headLogin, ProcApiFactory.TestPassword, f.CompanyCode);
        var director = await _factory.CreateClientInCompanyAsync(directorLogin, ProcApiFactory.TestPassword, f.CompanyCode);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, headRole), Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 2, 0, directorRole), Ct));

        // Quản trị nhập chức vụ cho trưởng phòng (khoảng trắng hai đầu bị cắt); giám đốc để trống.
        var detail = await ReadAsync<UserDetailDto>(await admin.PutAsJsonAsync($"/api/users/{headId}",
            new UpdateUserRequest($"Người thử {headLogin}", null, true, "  Trưởng phòng Vật tư  "), Ct));
        Assert.Equal("Trưởng phòng Vật tư", detail.Position);
        var list = await admin.GetFromJsonAsync<List<UserListItemDto>>("/api/users", Ct);
        Assert.Equal("Trưởng phòng Vật tư", Assert.Single(list!, u => u.Id == headId).Position);
        // Chức vụ quá 100 ký tự bị chặn.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{headId}",
            new UpdateUserRequest($"Người thử {headLogin}", null, true, new string('x', 101)), Ct)).StatusCode);

        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        // Bước 0: người gửi có tên đăng nhập; chức vụ trống thì là tên vai trò của họ tại công ty (không bỏ trống ô).
        var l1 = po.Approvals.Single(a => a.Level == 1);
        Assert.False(string.IsNullOrEmpty(l1.SubmittedByUserName));
        Assert.False(string.IsNullOrEmpty(l1.SubmittedByPosition));
        Assert.Null(l1.ActedByUserName);

        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(head, "purchase-orders", po.Id, "approve"));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(director, "purchase-orders", po.Id, "approve"));
        Assert.Equal("Approved", po.Status);
        l1 = po.Approvals.Single(a => a.Level == 1);
        var l2 = po.Approvals.Single(a => a.Level == 2);
        Assert.Equal((headLogin, "Trưởng phòng Vật tư"), (l1.ActedByUserName, l1.ActedByPosition));
        // Giám đốc chưa nhập chức vụ → tên vai trò tại công ty (CreateRoleAsync đặt tên vai trò theo mã).
        Assert.Equal(directorLogin, l2.ActedByUserName);
        Assert.Contains(directorCode, l2.ActedByPosition);
    }
}
