using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Proc.Application.Documents;
using Proc.Application.Purchasing;
using Proc.Domain.Documents;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Z1 (design.md mục 20, quyết định D1): ý kiến + đính kèm ở bước duyệt; đính kèm của chứng từ có phê duyệt bị khóa – file của bước
/// duyệt và file bổ sung sau duyệt không xóa được; từ khi gửi duyệt mọi file khóa; phiếu nháp chưa có bước duyệt xong chỉ người thêm file
/// mới xóa được.
/// </summary>
public partial class PurchasingTests
{
    private static Task<HttpResponseMessage> AttachAsync(HttpClient client, Guid poId, string fileName, bool approvalStep = false)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(AttachmentOwnerTypes.PurchaseOrder), "ownerType" },
            { new StringContent(poId.ToString()), "ownerId" },
        };
        if (approvalStep) form.Add(new StringContent("true"), "approvalStep");
        var file = new ByteArrayContent("%PDF-1.4 thu"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return client.PostAsync("/api/attachments", form, Ct);
    }

    private static async Task<List<AttachmentDto>> AttachmentsAsync(HttpClient client, Guid poId) =>
        (await client.GetFromJsonAsync<List<AttachmentDto>>($"/api/attachments?ownerType={AttachmentOwnerTypes.PurchaseOrder}&ownerId={poId}", Ct))!;

    [Fact]
    public async Task DinhKemBuocDuyet_KhoaSauKhiGuiDuyet_BoSungSauDuyetKhongXoaDuoc()
    {
        var f = await ArrangeAsync();
        var (approver, roleId) = await ApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, roleId), Ct));
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));

        // Nháp: người thêm file xóa được; người duyệt không có quyền sửa PO thì không thêm file thường được.
        var draftFile = await ReadAsync<AttachmentDto>(await AttachAsync(f.Buyer, po.Id, "bao-gia.pdf"));
        Assert.True(draftFile.CanDelete);
        Assert.Equal(HttpStatusCode.Forbidden, (await AttachAsync(approver, po.Id, "khong-quyen.pdf")).StatusCode);
        // Chưa duyệt / từ chối bước nào thì không gắn được file vào bước duyệt.
        Assert.Contains("chưa duyệt / từ chối", await TitleAsync(await AttachAsync(approver, po.Id, "som.pdf", approvalStep: true)));

        // Gửi duyệt: mọi file đã có bị khóa.
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        var locked = Assert.Single(await AttachmentsAsync(f.Buyer, po.Id));
        Assert.False(locked.CanDelete);
        Assert.Contains("đã gửi duyệt", locked.LockReason);
        Assert.Contains("đã gửi duyệt", await TitleAsync(await f.Buyer.DeleteAsync($"/api/attachments/{draftFile.Id}", Ct)));

        // Người duyệt duyệt kèm ý kiến rồi đính kèm vào bước vừa duyệt – chỉ cần quyền xem PO.
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve", "Đồng ý mua, giá hợp lý"));
        Assert.Equal("Approved", po.Status);
        Assert.Equal("Đồng ý mua, giá hợp lý", po.Approvals.Single(a => a.Status == "Approved").Comment);
        var stepFile = await ReadAsync<AttachmentDto>(await AttachAsync(approver, po.Id, "y-kien-duyet.pdf", approvalStep: true));
        Assert.Equal((1, false, false), (stepFile.ApprovalStepLevel, stepFile.AddedAfterApproval, stepFile.CanDelete));

        // Đã duyệt xong: người lập thêm file được, đánh dấu "bổ sung sau duyệt", không xóa được.
        var later = await ReadAsync<AttachmentDto>(await AttachAsync(f.Buyer, po.Id, "hoa-don.pdf"));
        Assert.Equal((null, true, false), (later.ApprovalStepLevel, later.AddedAfterApproval, later.CanDelete));
        Assert.Contains("bổ sung sau duyệt", await TitleAsync(await f.Buyer.DeleteAsync($"/api/attachments/{later.Id}", Ct)));
        Assert.Contains("bước duyệt", await TitleAsync(await f.Buyer.DeleteAsync($"/api/attachments/{stepFile.Id}", Ct)));
        Assert.Equal(3, (await AttachmentsAsync(f.Buyer, po.Id)).Count);
    }

    [Fact]
    public async Task TuChoiVeNhap_ChuaCoBuocDuyetXong_NguoiThemFileXoaDuocFileCuaMinh()
    {
        var f = await ArrangeAsync();
        var (approver, roleId) = await ApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, roleId), Ct));
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        var mine = await ReadAsync<AttachmentDto>(await AttachAsync(f.Buyer, po.Id, "bao-gia-cu.pdf"));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));

        // Từ chối kèm ý kiến và file: phiếu về nháp, chưa có bước nào duyệt xong.
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "reject", "Thiếu báo giá thứ hai"));
        Assert.Equal("Draft", po.Status);
        var rejectFile = await ReadAsync<AttachmentDto>(await AttachAsync(approver, po.Id, "ly-do-tu-choi.pdf", approvalStep: true));
        Assert.False(rejectFile.CanDelete);

        // Người lập xóa được file của chính mình; file của bước từ chối vẫn khóa.
        var list = await AttachmentsAsync(f.Buyer, po.Id);
        Assert.True(list.Single(a => a.Id == mine.Id).CanDelete);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Buyer.DeleteAsync($"/api/attachments/{mine.Id}", Ct)).StatusCode);
        Assert.False((await f.Buyer.DeleteAsync($"/api/attachments/{rejectFile.Id}", Ct)).IsSuccessStatusCode);
    }
}
