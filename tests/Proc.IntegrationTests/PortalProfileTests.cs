using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Domain.MasterData;
using Proc.Domain.Notifications;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P2a (design.md mục 19.5, P2-1…P2-6): quản trị NCC gửi đề nghị cập nhật hồ sơ trên Portal (không có mã số thuế); Phòng CUVT ở tổng công ty
/// chấp nhận (thông tin chung, người liên hệ áp ngay; tài khoản ngân hàng thành yêu cầu duyệt NCC) hoặc từ chối; NCC rút được khi còn chờ.
/// </summary>
public partial class PurchasingTests
{
    private static PortalProfileUpdateRequest ProfileOf(PortalProfileDto p) => new(
        p.Name, p.ShortName, p.Address, p.Phone, p.Email, p.Website, p.SupplyScope, p.Contacts, p.BankAccounts, null);

    private static Task<HttpResponseMessage> UploadProfileFileAsync(HttpClient ncc, Guid requestId)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 giay phep"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "giay-phep.pdf");
        return ncc.PostAsync($"/api/portal/profile/requests/{requestId}/attachments", form, Ct);
    }

    [Fact]
    public async Task PortalHoSo_GuiChapNhan_TaiKhoanQuaDuyet_TuChoi_Rut_Quyen()
    {
        var f = await ArrangeAsync();
        var headName = $"hs.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(headName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(headName, ProcApiFactory.TestPassword, "TCT");
        var ncc = await PortalClientAsync(f.Buyer, f.SupplierId);

        var profile = await ReadAsync<PortalProfileDto>(await ncc.GetAsync("/api/portal/profile", Ct));
        Assert.True(profile.CanSubmit);
        Assert.Equal("NCC mua hàng", profile.Name);

        // Không đổi gì → báo lỗi.
        Assert.Equal(HttpStatusCode.BadRequest, (await ncc.PostAsJsonAsync("/api/portal/profile/requests", ProfileOf(profile), Ct)).StatusCode);

        var sent = await ReadAsync<SupplierProfileRequestDto>(await ncc.PostAsJsonAsync("/api/portal/profile/requests", ProfileOf(profile) with
        {
            Address = "12 Lê Lợi, Hải Phòng",
            Contacts = [.. profile.Contacts, new SupplierProfileContactDto(null, "Chị Bán Hàng", "Kinh doanh", "0901", "kd@ncc.vn", true)],
            BankAccounts = [new SupplierProfileBankDto(null, "BIDV", "Hải Phòng", "1234 5678", "NCC mua hàng", true)],
            Note = "Đổi trụ sở",
        }, Ct));
        Assert.EndsWith("-HS01", sent.No, StringComparison.Ordinal);
        Assert.Equal(("Submitted", "Địa chỉ; người liên hệ; tài khoản ngân hàng", true, false),
            (sent.Status, sent.Summary, sent.CanWithdraw, sent.CanProcess));
        Assert.Null(sent.ReviewedByName);

        // Một đề nghị chờ mỗi NCC; file kèm khi còn chờ.
        Assert.Equal(HttpStatusCode.BadRequest, (await ncc.PostAsJsonAsync("/api/portal/profile/requests",
            ProfileOf(profile) with { Phone = "0909" }, Ct)).StatusCode);
        Assert.True((await UploadProfileFileAsync(ncc, sent.Id)).IsSuccessStatusCode);
        Assert.False((await ReadAsync<PortalProfileDto>(await ncc.GetAsync("/api/portal/profile", Ct))).CanSubmit);

        // Báo người sửa được NCC ở tổng công ty; nội bộ xem so sánh, file NCC chỉ xem.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.True(await db.Notifications.IgnoreQueryFilters()
                .AnyAsync(n => n.Type == NotificationTypes.PortalProfileRequest && n.TargetId == sent.Id, Ct));
        }

        var detail = await ReadAsync<SupplierProfileRequestDto>(await head.GetAsync($"/api/supplier-profile-requests/{sent.Id}", Ct));
        Assert.True(detail.CanProcess);
        var field = Assert.Single(detail.Fields);
        Assert.Equal(("Address", (string?)null, "12 Lê Lợi, Hải Phòng"), (field.Field, field.Current, field.New));
        var files = await ReadAsync<List<Proc.Application.Documents.AttachmentDto>>(await head.GetAsync(
            $"/api/attachments?ownerType={Proc.Domain.Documents.AttachmentOwnerTypes.SupplierProfileFile}&ownerId={sent.Id}", Ct));
        Assert.False(Assert.Single(files).CanDelete);

        // Chỉ xem NCC thì không xử lý được.
        var viewRole = UniqueCode("V");
        await _factory.CreateRoleAsync(viewRole, Permissions.MasterData.SupplierView);
        var viewName = $"xv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewName, "TCT", viewRole);
        var viewer = await _factory.CreateClientInCompanyAsync(viewName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"/api/supplier-profile-requests/{sent.Id}/accept",
            new SupplierProfileCommentRequest(null), Ct)).StatusCode);

        var accepted = await ReadAsync<SupplierProfileRequestDto>(await head.PostAsJsonAsync($"/api/supplier-profile-requests/{sent.Id}/accept",
            new SupplierProfileCommentRequest("Đã đối chiếu giấy phép"), Ct));
        Assert.Equal("Accepted", accepted.Status);
        Assert.NotNull(accepted.BankChangeRequestNo);
        Assert.NotNull(accepted.BankChangeRequestId);
        var supplier = await ReadAsync<SupplierDetailDto>(await head.GetAsync($"/api/suppliers/{f.SupplierId}", Ct));
        Assert.Equal("12 Lê Lợi, Hải Phòng", supplier.Address);
        Assert.Contains(supplier.Contacts, c => c.Name == "Chị Bán Hàng" && c.IsPrimary);
        // Tài khoản ngân hàng chưa áp – chờ yêu cầu duyệt NCC (Q7).
        Assert.Empty(supplier.BankAccounts);
        Assert.Contains(supplier.PendingRequests!, r => r.No == accepted.BankChangeRequestNo && r.Kind == "BankAccounts");

        // Đã xử lý thì khóa: không thêm file, không rút, không xử lý lại.
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadProfileFileAsync(ncc, sent.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ncc.PostAsJsonAsync($"/api/portal/profile/requests/{sent.Id}/withdraw",
            new SupplierProfileCommentRequest(null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"/api/supplier-profile-requests/{sent.Id}/reject",
            new SupplierProfileCommentRequest("x"), Ct)).StatusCode);

        // Đề nghị 2: NCC rút. Đề nghị 3: từ chối phải có lý do.
        profile = await ReadAsync<PortalProfileDto>(await ncc.GetAsync("/api/portal/profile", Ct));
        var second = await ReadAsync<SupplierProfileRequestDto>(await ncc.PostAsJsonAsync("/api/portal/profile/requests",
            ProfileOf(profile) with { Phone = "0225 123" }, Ct));
        Assert.Equal("Withdrawn", (await ReadAsync<SupplierProfileRequestDto>(await ncc.PostAsJsonAsync(
            $"/api/portal/profile/requests/{second.Id}/withdraw", new SupplierProfileCommentRequest("Gửi nhầm"), Ct))).Status);
        var third = await ReadAsync<SupplierProfileRequestDto>(await ncc.PostAsJsonAsync("/api/portal/profile/requests",
            ProfileOf(profile) with { Website = "ncc.vn" }, Ct));
        Assert.EndsWith("-HS03", third.No, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"/api/supplier-profile-requests/{third.Id}/reject",
            new SupplierProfileCommentRequest(" "), Ct)).StatusCode);
        var rejected = await ReadAsync<SupplierProfileRequestDto>(await head.PostAsJsonAsync($"/api/supplier-profile-requests/{third.Id}/reject",
            new SupplierProfileCommentRequest("Website không hoạt động"), Ct));
        Assert.Equal(("Rejected", "Website không hoạt động"), (rejected.Status, rejected.ReviewComment));
        Assert.Null((await ReadAsync<SupplierDetailDto>(await head.GetAsync($"/api/suppliers/{f.SupplierId}", Ct))).Website);

        // Thư báo kết quả cho NCC (chấp nhận, từ chối); rút không có thư.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var mails = await db.SupplierPortalEmails.IgnoreQueryFilters().Where(m => m.SupplierId == f.SupplierId && m.DocType == "HSC")
                .ToListAsync(Ct);
            Assert.Contains(mails, m => m.DocId == sent.Id && m.Kind == SupplierPortalEmailKind.ProfileRequestAccepted
                                        && m.TextBody.Contains("tài khoản ngân hàng", StringComparison.Ordinal));
            Assert.Contains(mails, m => m.DocId == third.Id && m.Kind == SupplierPortalEmailKind.ProfileRequestRejected
                                        && m.TextBody.Contains("Website không hoạt động", StringComparison.Ordinal));
            Assert.DoesNotContain(mails, m => m.DocId == second.Id);
        }

        // Danh sách nội bộ lọc theo trạng thái; Portal thấy đủ lịch sử, không lộ tên người xử lý nội bộ.
        var pendingList = await ReadAsync<List<SupplierProfileRequestDto>>(await head.GetAsync(
            $"/api/supplier-profile-requests?status=Accepted&supplierId={f.SupplierId}", Ct));
        Assert.Equal(sent.Id, Assert.Single(pendingList).Id);
        Assert.Equal($"Người thử {headName}", pendingList[0].ReviewedByName);
        profile = await ReadAsync<PortalProfileDto>(await ncc.GetAsync("/api/portal/profile", Ct));
        Assert.Equal(3, profile.Requests.Count);
        Assert.All(profile.Requests, r => Assert.Null(r.ReviewedByName));
    }

    [Fact]
    public async Task PortalHoSo_NguoiDungCon_ChiXem_KhongGuiDuoc()
    {
        var f = await ArrangeAsync();
        var admin = await PortalClientAsync(f.Buyer, f.SupplierId);
        // Quản trị NCC mời người dùng con.
        var email = $"{Guid.NewGuid():N}"[..10] + "@ncc.vn";
        var invite = await ReadAsync<PortalInviteResult>(await admin.PostAsJsonAsync("/api/portal/users",
            new PortalInviteRequest(email, "Nhân viên NCC", null, null), Ct));
        var token = Uri.UnescapeDataString(invite.InviteUrl[(invite.InviteUrl.IndexOf("token=", StringComparison.Ordinal) + 6)..]);
        var anon = _factory.CreateClient();
        await anon.PostAsJsonAsync("/api/portal/auth/activate", new PortalSetPasswordRequest(token, "ncc12345"), Ct);
        var challenge = await ReadAsync<PortalLoginChallenge>(
            await anon.PostAsJsonAsync("/api/portal/auth/login", new PortalLoginRequest(email, "ncc12345"), Ct));
        var login = await ReadAsync<PortalTokenResult>(
            await anon.PostAsJsonAsync("/api/portal/auth/verify-otp", new PortalVerifyOtpRequest(challenge.ChallengeId, challenge.DevCode!), Ct));
        var staff = _factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var profile = await ReadAsync<PortalProfileDto>(await staff.GetAsync("/api/portal/profile", Ct));
        Assert.False(profile.CanSubmit);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/portal/profile/requests",
            ProfileOf(profile) with { Phone = "0909" }, Ct)).StatusCode);

        // Đề nghị của quản trị: người dùng con thấy nhưng không rút được.
        var sent = await ReadAsync<SupplierProfileRequestDto>(await admin.PostAsJsonAsync("/api/portal/profile/requests",
            ProfileOf(profile) with { Phone = "0909" }, Ct));
        var seen = Assert.Single((await ReadAsync<PortalProfileDto>(await staff.GetAsync("/api/portal/profile", Ct))).Requests);
        Assert.Equal((sent.Id, false), (seen.Id, seen.CanWithdraw));
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/portal/profile/requests/{sent.Id}/withdraw",
            new SupplierProfileCommentRequest(null), Ct)).StatusCode);
    }
}
