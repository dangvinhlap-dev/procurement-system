using System.Net;
using System.Net.Http.Json;
using Proc.Application.MasterData;
using Proc.Application.Purchasing;

namespace Proc.IntegrationTests;

/// <summary>
/// Xem trước duyệt NCC khi lưu hồ sơ (yêu cầu người dùng 03/11/2026): đổi trạng thái/tài khoản ngân hàng sinh yêu cầu duyệt – chạy thử lệnh lưu
/// rồi hủy, trả các yêu cầu sẽ sinh và người duyệt; không đổi gì cần duyệt thì không có yêu cầu nào.
/// </summary>
public partial class SupplierApprovalTests
{
    [Fact]
    public async Task XemTruocDuyetNcc_KhiLuuHoSo_KhongLuuGi()
    {
        var head = await AdminAtAsync("TCT");
        await ApproverAtAsync("TCT");
        var supplier = await NewSupplierAsync(head);
        var url = "/api/approvals/preview";

        // Đổi tên (không cần duyệt): không có yêu cầu nào – lưu thẳng.
        var none = await ReadAsync<ApprovalPreviewDto>(await head.PostAsJsonAsync(url,
            new ApprovalPreviewRequest("NCC", supplier.Id, Supplier: Edit(supplier) with { Name = supplier.Name + " (sửa)" }), Ct));
        Assert.Empty(none.Groups!);

        // Đưa vào danh sách đen + đổi tài khoản: hai yêu cầu, chặng Tổng công ty có người duyệt.
        var change = Edit(supplier) with
        {
            Status = "Blacklisted", StatusNote = "Hàng giả",
            BankAccounts = [new(null, "Techcombank", null, "9999 8888", null, true)],
        };
        var preview = await ReadAsync<ApprovalPreviewDto>(await head.PostAsJsonAsync(url, new ApprovalPreviewRequest("ncc", supplier.Id, Supplier: change), Ct));
        Assert.Equal(2, preview.Groups!.Count);
        Assert.All(preview.Groups, g => Assert.False(g.ApprovedImmediately));
        Assert.All(preview.Groups, g => Assert.Contains(g.Steps, s => s.CompanyCode == "TCT" && (s.NextApprovers ?? s.UpcomingApprovers)!.Count > 0));

        // Chạy thử không lưu gì: trạng thái giữ nguyên, chưa có yêu cầu chờ duyệt.
        var after = await ReadAsync<SupplierDetailDto>(await head.GetAsync($"/api/suppliers/{supplier.Id}", Ct));
        Assert.Equal("Potential", after.Status);
        Assert.Empty(after.PendingRequests!);

        // Lập NCC mới ở trạng thái Đã duyệt cũng sinh yêu cầu – xem trước được khi chưa có Id; NCC chưa được tạo.
        var fresh = Edit(supplier) with { Name = $"NCC mới {Guid.NewGuid():N}"[..20], Status = "Approved", BankAccounts = [] };
        var createPreview = await ReadAsync<ApprovalPreviewDto>(await head.PostAsJsonAsync(url, new ApprovalPreviewRequest("NCC", Guid.Empty, Supplier: fresh), Ct));
        Assert.Single(createPreview.Groups!);
        var list = await head.GetFromJsonAsync<SupplierListResult>($"/api/suppliers?search={Uri.EscapeDataString(fresh.Name)}", Ct);
        Assert.Empty(list!.Items);

        // Lưu thật đổi cả trạng thái lẫn tài khoản: hai yêu cầu khác số (trước đây trùng số – lỗi 409).
        var saved = await ReadAsync<SupplierDetailDto>(await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}", change, Ct));
        Assert.Equal(2, saved.PendingRequests!.Select(r => r.No).Distinct().Count());

        // Người không có quyền sửa NCC không xem trước được.
        var approver = await ApproverAtAsync("TCT");
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostAsJsonAsync(url, new ApprovalPreviewRequest("NCC", supplier.Id, Supplier: change), Ct)).StatusCode);
    }
}
