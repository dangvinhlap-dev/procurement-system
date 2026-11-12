using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Documents;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Application.Purchasing;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P4a (design.md mục 19.5, P4-1…P4-5): NCC xem PO đã duyệt trên Portal, xác nhận (ngày giao cam kết) hoặc từ chối (lý do), file gửi kèm
/// khóa sau phản hồi; Phòng CUVT thấy phản hồi trên PO, mở lại phản hồi (lý do gửi NCC – yêu cầu người dùng 12/11/2026); thư đơn hàng mới,
/// mở lại, hủy; NCC khác không thấy đơn.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task PortalDonHang_XacNhanTuChoiMoLai_FileKhoa_ThuBaoNcc_CoLapNcc()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var ncc = await PortalClientAsync(f.Buyer, f.SupplierId);

        // NCC khác (có tài khoản Portal) không thấy đơn của NCC này.
        var headName = $"hd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(headName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(headName, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var other = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC khác – đơn Portal", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(other.Id);
        var otherNcc = await PortalClientAsync(head, other.Id);

        // PO nháp / chờ duyệt: NCC chưa thấy.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        Assert.Equal("NotSent", po.SupplierResponse!.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await ncc.GetAsync($"/api/portal/orders/{po.Id}", Ct)).StatusCode);
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        Assert.Equal("Submitted", po.Status);
        Assert.DoesNotContain(await ReadAsync<List<PortalOrderListItemDto>>(await ncc.GetAsync("/api/portal/orders", Ct)), o => o.Id == po.Id);

        // Duyệt xong → thư "Đơn đặt hàng mới" cho tài khoản Portal đang dùng; NCC thấy đơn, chờ xác nhận.
        po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal(("Approved", "Pending", 1), (po.Status, po.SupplierResponse!.Status, po.SupplierResponse.ActivePortalUsers));
        var mailsUrl = $"/api/purchase-orders/{po.Id}/portal-emails";
        var issued = Assert.Single(await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(mailsUrl, Ct)));
        Assert.Equal(("PurchaseOrderIssued", f.SupplierId), (issued.Kind, issued.SupplierId));
        Assert.Contains(po.No, issued.Subject, StringComparison.Ordinal);

        var row = Assert.Single(await ReadAsync<List<PortalOrderListItemDto>>(await ncc.GetAsync("/api/portal/orders", Ct)), o => o.Id == po.Id);
        Assert.Equal(("Pending", 1), (row.Status, row.LineCount));
        Assert.Equal(po.TotalAmount + po.TotalVat, row.TotalPayable);
        var detail = await ReadAsync<PortalOrderDto>(await ncc.GetAsync($"/api/portal/orders/{po.Id}", Ct));
        Assert.True(detail.CanRespond);
        Assert.Equal((10m, 100_000m, 5m), (detail.Lines[0].Qty, detail.Lines[0].UnitPrice, detail.Lines[0].DiscountPct));
        Assert.Equal(HttpStatusCode.NotFound, (await otherNcc.GetAsync($"/api/portal/orders/{po.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherNcc.PostAsJsonAsync($"/api/portal/orders/{po.Id}/respond",
            new PortalOrderRespondRequest("Confirmed", Today.AddDays(3), null), Ct)).StatusCode);

        // File gửi kèm: thêm trước khi phản hồi; nội bộ chỉ xem, không thêm được.
        var file = await UploadPortalOrderFileAsync(ncc, po.Id, "bao-gia-xac-nhan.pdf");
        Assert.True(file.CanDelete);
        using (var form = PdfForm("noi-bo.pdf"))
        {
            form.Add(new StringContent("PoSupplierFile"), "ownerType");
            form.Add(new StringContent(po.Id.ToString()), "ownerId");
            Assert.Equal(HttpStatusCode.Forbidden, (await f.Buyer.PostAsync("/api/attachments", form, Ct)).StatusCode);
        }

        // Từ chối thiếu lý do → lỗi; từ chối có lý do → thông báo người lập, file khóa.
        Assert.Contains("lý do", await TitleAsync(await ncc.PostAsJsonAsync($"/api/portal/orders/{po.Id}/respond",
            new PortalOrderRespondRequest("Rejected", null, " "), Ct)));
        detail = await ReadAsync<PortalOrderDto>(await ncc.PostAsJsonAsync($"/api/portal/orders/{po.Id}/respond",
            new PortalOrderRespondRequest("Rejected", null, "Hết hàng đến cuối tháng"), Ct));
        Assert.Equal(("Rejected", false), (detail.Status, detail.CanRespond));
        Assert.Contains("đã được phản hồi", await TitleAsync(await ncc.PostAsJsonAsync($"/api/portal/orders/{po.Id}/respond",
            new PortalOrderRespondRequest("Confirmed", Today.AddDays(3), null), Ct)));
        Assert.False(Assert.Single(await ReadAsync<List<AttachmentDto>>(await ncc.GetAsync($"/api/portal/orders/{po.Id}/attachments", Ct))).CanDelete);
        Assert.False((await ncc.DeleteAsync($"/api/portal/orders/{po.Id}/attachments/{file.Id}", Ct)).IsSuccessStatusCode);
        var note = Assert.Single(await NotificationApi.MineAsync(f.Buyer, Ct), n => n.TargetId == po.Id && n.Type == "PortalOrderResponse");
        Assert.Contains("Hết hàng", note.Message, StringComparison.Ordinal);

        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal(("Rejected", true), (po.SupplierResponse!.Status, po.SupplierResponse.CanReopen));
        Assert.Equal("Hết hàng đến cuối tháng", po.SupplierResponse.Responses.Single().Note);
        // Nội bộ thấy file của NCC (chỉ xem).
        Assert.False(Assert.Single(await ReadAsync<List<AttachmentDto>>(await f.Buyer.GetAsync(
            $"/api/attachments?ownerType=PoSupplierFile&ownerId={po.Id}", Ct))).CanDelete);

        // Mở lại: bắt buộc lý do; người chỉ có quyền xem / duyệt không mở lại được.
        var reopenUrl = $"/api/purchase-orders/{po.Id}/reopen-supplier-response";
        Assert.Contains("lý do", await TitleAsync(await f.Buyer.PostAsJsonAsync(reopenUrl, new ReopenSupplierResponseRequest(""), Ct)));
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostAsJsonAsync(reopenUrl, new ReopenSupplierResponseRequest("Đổi ngày"), Ct)).StatusCode);
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync(reopenUrl,
            new ReopenSupplierResponseRequest("Bên mua chấp nhận giao chậm 10 ngày"), Ct));
        Assert.Equal(("Pending", false, 1), (po.SupplierResponse!.Status, po.SupplierResponse.CanReopen, po.SupplierResponse.Responses.Count));
        Assert.NotNull(po.SupplierResponse.Responses[0].ReopenedAt);
        var reopened = Assert.Single(await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(mailsUrl, Ct)),
            m => m.Kind == "PurchaseOrderReopened");
        using (var scope = _factory.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<ProcDbContext>().SupplierPortalEmails.IgnoreQueryFilters()
                .SingleAsync(m => m.Id == reopened.Id, Ct);
            Assert.Contains("Lý do: Bên mua chấp nhận giao chậm 10 ngày", stored.TextBody, StringComparison.Ordinal);
        }

        // NCC thấy lý do mở lại, phản hồi lại: file cũ vẫn khóa, file mới xóa được trước khi phản hồi.
        detail = await ReadAsync<PortalOrderDto>(await ncc.GetAsync($"/api/portal/orders/{po.Id}", Ct));
        Assert.Equal(("Pending", true), (detail.Status, detail.CanRespond));
        Assert.Equal("Bên mua chấp nhận giao chậm 10 ngày", detail.Responses.Single().ReopenReason);
        var second = await UploadPortalOrderFileAsync(ncc, po.Id, "lich-giao.pdf");
        var files = await ReadAsync<List<AttachmentDto>>(await ncc.GetAsync($"/api/portal/orders/{po.Id}/attachments", Ct));
        Assert.Equal([false, true], files.OrderBy(x => x.UploadedAt).Select(x => x.CanDelete));
        Assert.False((await ncc.DeleteAsync($"/api/portal/orders/{po.Id}/attachments/{file.Id}", Ct)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ncc.DeleteAsync($"/api/portal/orders/{po.Id}/attachments/{second.Id}", Ct)).StatusCode);

        // Xác nhận: ngày giao không trước ngày đặt hàng.
        Assert.Contains("ngày đặt hàng", await TitleAsync(await ncc.PostAsJsonAsync($"/api/portal/orders/{po.Id}/respond",
            new PortalOrderRespondRequest("Confirmed", po.Date.AddDays(-1), null), Ct)));
        detail = await ReadAsync<PortalOrderDto>(await ncc.PostAsJsonAsync($"/api/portal/orders/{po.Id}/respond",
            new PortalOrderRespondRequest("Confirmed", Today.AddDays(15), "Giao một lần"), Ct));
        Assert.Equal(("Confirmed", 2), (detail.Status, detail.Responses.Count));
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal(("Confirmed", Today.AddDays(15)), (po.SupplierResponse!.Status, po.SupplierResponse.Responses[0].PromisedDate));

        // Hủy PO đã duyệt (người mua của bộ test có quyền Hủy phiếu) → thư hủy không có lý do; NCC thấy "Đã hủy", không phản hồi được.
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/cancel",
            new CancelDocumentRequest("Lý do nội bộ bí mật"), Ct));
        var cancelled = Assert.Single(await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(mailsUrl, Ct)),
            m => m.Kind == "PurchaseOrderCancelled");
        using (var scope = _factory.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<ProcDbContext>().SupplierPortalEmails.IgnoreQueryFilters()
                .SingleAsync(m => m.Id == cancelled.Id, Ct);
            Assert.DoesNotContain("bí mật", stored.HtmlBody + stored.TextBody, StringComparison.Ordinal);
            Assert.Equal(SupplierPortalEmailKind.PurchaseOrderCancelled, stored.Kind);
        }

        detail = await ReadAsync<PortalOrderDto>(await ncc.GetAsync($"/api/portal/orders/{po.Id}", Ct));
        Assert.Equal(("Cancelled", false), (detail.Status, detail.CanRespond));
    }

    private async Task<AttachmentDto> UploadPortalOrderFileAsync(HttpClient ncc, Guid orderId, string fileName)
    {
        using var form = PdfForm(fileName);
        return await ReadAsync<AttachmentDto>(await ncc.PostAsync($"/api/portal/orders/{orderId}/attachments", form, Ct));
    }

    /// <summary>Form tải lên một file PDF nhỏ hợp lệ (byte đầu "%PDF-" – kiểm nội dung P1b-3).</summary>
    private static MultipartFormDataContent PdfForm(string fileName)
    {
        var content = new ByteArrayContent("%PDF-1.4\n%test\n"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { content, "file", fileName } };
    }
}
