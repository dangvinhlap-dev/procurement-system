using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Documents;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Application.Sourcing;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P3b-1 (design.md mục 19.5, P3-9): NCC tải file báo giá lên Portal (2 MB, kiểm nội dung), xóa được đến giờ hạn; nội bộ chỉ xem – trước
/// giờ mở cần quyền xem niêm phong, tải file ghi nhật ký; nội bộ không thêm, xóa file của NCC; NCC khác không thấy.
/// </summary>
public partial class PurchasingTests
{
    private static MultipartFormDataContent FileForm(string fileName, byte[] content, string? note = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(content), "file", fileName } };
        if (note is not null) form.Add(new StringContent(note), "note");
        return form;
    }

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% bao gia\n%%EOF\n");

    [Fact]
    public async Task FileBaoGiaPortal_NccTaiLen_NoiBoChiXemTheoNiemPhong()
    {
        var f = await ArrangeAsync();
        var rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync("/api/rfqs",
            RfqOf(f, "Dây curoa – file báo giá", new SaveRfqLineRequest(null, f.ItemId, f.Cai, 10, null, null)) with
            {
                Deadline = Today.AddDays(1), DeadlineTime = new TimeOnly(17, 0),
            }, Ct));
        await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/send", new SendRfqRequest("Chỉ một NCC cung cấp"), Ct));
        var ncc = await PortalClientAsync(f.Buyer, f.SupplierId);
        var url = $"/api/portal/rfqs/{rfq.Id}/attachments";

        // NCC tải hai file, xóa một; file chạy được đổi đuôi .pdf bị chặn.
        var kept = await ReadAsync<AttachmentDto>(await ncc.PostAsync(url, FileForm("BaoGia.pdf", Pdf, "Bảng giá chi tiết"), Ct));
        var removed = await ReadAsync<AttachmentDto>(await ncc.PostAsync(url, FileForm("Catalogue.pdf", Pdf), Ct));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ncc.PostAsync(url, FileForm("virus.pdf", [0x4D, 0x5A, 0x90, 0x00, 0x03]), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ncc.DeleteAsync($"{url}/{removed.Id}", Ct)).StatusCode);
        var files = await ReadAsync<List<AttachmentDto>>(await ncc.GetAsync(url, Ct));
        Assert.Equal(("BaoGia.pdf", "Người bán hàng", true), (files.Single().FileName, files[0].UploadedByName, files[0].CanDelete));
        Assert.Equal(Pdf, await ncc.GetByteArrayAsync($"{url}/{kept.Id}/content", Ct));
        var lineId = rfq.Lines.Single().Id;
        await ReadAsync<PortalRfqDto>(await ncc.PostAsJsonAsync($"/api/portal/rfqs/{rfq.Id}/quotes",
            new PortalSubmitQuoteRequest(null, null, null, [new(lineId, 100_000, 0, "10", 2, null)]), Ct));

        // Nội bộ: thấy số file; quản trị (có quyền xem niêm phong) xem, tải được – lần tải ghi nhật ký; không thêm, xóa file của NCC.
        rfq = await ReadAsync<RfqDto>(await f.Buyer.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        var invitation = rfq.Suppliers.Single();
        Assert.Equal(1, invitation.PortalAttachmentCount);
        var internalUrl = $"/api/attachments?ownerType=RfqPortalQuote&ownerId={invitation.InvitationId}";
        var internalFiles = await ReadAsync<List<AttachmentDto>>(await f.Buyer.GetAsync(internalUrl, Ct));
        Assert.Equal(("NCC: Người bán hàng", false), (internalFiles.Single().UploadedByName, internalFiles[0].CanDelete));
        Assert.Equal(Pdf, await f.Buyer.GetByteArrayAsync($"/api/attachments/{kept.Id}/content", Ct));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.Contains(await db.AccessLogs.Where(l => l.Event == "SealedQuoteViewed").Select(l => l.Detail).ToListAsync(Ct),
                d => d!.Contains("BaoGia.pdf", StringComparison.Ordinal) && d.Contains(rfq.No, StringComparison.Ordinal));
        }

        var upload = new MultipartFormDataContent
        {
            { new StringContent("RfqPortalQuote"), "ownerType" }, { new StringContent(invitation.InvitationId.ToString()), "ownerId" },
            { new ByteArrayContent(Pdf), "file", "ThemThay.pdf" },
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Buyer.PostAsync("/api/attachments", upload, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Buyer.DeleteAsync($"/api/attachments/{kept.Id}", Ct)).StatusCode);

        // Người chỉ có quyền xem/lập RFQ: trước giờ mở không xem được file.
        var roleCode = UniqueCode("XFILE");
        await _factory.CreateRoleAsync(roleCode, Permissions.Sourcing.RfqView, Permissions.Sourcing.RfqManage);
        var viewerName = $"xf.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, f.CompanyCode, roleCode, [], []);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(internalUrl, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/attachments/{kept.Id}/content", Ct)).StatusCode);

        // NCC khác không thấy file của thư mời này.
        var headName = $"hf.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(headName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(headName, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var other = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC khác – file", null, false, null, null, null, null, null, null, null, null, "Approved", null,
                null, [], []), Ct));
        var outsider = await PortalClientAsync(head, other.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(url, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"{url}/{kept.Id}/content", Ct)).StatusCode);

        // Qua giờ hạn: NCC không thêm, xóa; vẫn tải xuống được. Mở báo giá xong người chỉ có quyền xem RFQ xem được file.
        await MoveDeadlineToPastAsync(rfq.Id);
        Assert.Contains("hết hạn", await TitleAsync(await ncc.PostAsync(url, FileForm("Muon.pdf", Pdf), Ct)));
        Assert.Equal(HttpStatusCode.BadRequest, (await ncc.DeleteAsync($"{url}/{kept.Id}", Ct)).StatusCode);
        Assert.False((await ReadAsync<List<AttachmentDto>>(await ncc.GetAsync(url, Ct))).Single().CanDelete);
        await ReadAsync<RfqDto>(await f.Buyer.PostAsync($"/api/rfqs/{rfq.Id}/open-portal-quotes", null, Ct));
        Assert.Single(await ReadAsync<List<AttachmentDto>>(await viewer.GetAsync(internalUrl, Ct)));
    }
}
