using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using Proc.Application.Documents;
using Proc.Application.Notifications;
using Proc.Application.Purchasing;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Printing;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát IN1 (design.md mục 20.3): "Xem đề nghị" in đề nghị mua giữ nguyên mẫu 1A – chữ ký người đã duyệt theo phiên bản có hiệu lực lúc duyệt,
/// In / Tải PDF ghi nhật ký in (lần in thứ n), xem thì không; admin tải chữ ký thay (bắt buộc lý do, báo người được tải thay).
/// </summary>
public partial class PurchasingTests
{
    private static MultipartFormDataContent ImageForm(byte[] png, string? reason = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "chu-ky.png");
        if (reason is not null) form.Add(new StringContent(reason), "reason");
        return form;
    }

    /// <summary>Kích thước các ảnh (XObject /Image) trên mọi trang của PDF.</summary>
    private static List<(int Width, int Height)> PdfImages(byte[] pdf)
    {
        var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.ReadOnly);
        var images = new List<(int, int)>();
        foreach (var page in document.Pages)
        {
            var xobjects = page.Resources.Elements.GetDictionary("/XObject");
            if (xobjects is null) continue;
            foreach (var item in xobjects.Elements.Values)
                if ((item as PdfReference)?.Value is PdfDictionary d && d.Elements.GetName("/Subtype") == "/Image")
                    images.Add((d.Elements.GetInteger("/Width"), d.Elements.GetInteger("/Height")));
        }

        return images;
    }

    [Fact]
    public async Task XemDeNghi_ChuKyTheoPhienBanLucDuyet_DemLanIn_AdminTaiThay()
    {
        var f = await ArrangeAsync();
        var (head, headRole) = await ApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 1, 0, headRole, null), Ct));

        // Người đề xuất và người duyệt tự tải chữ ký (không cần quyền); chữ ký người duyệt v1 240×80.
        var mine = await ReadAsync<UserSignatureDto>(await f.Buyer.PostAsync("/api/me/signature", ImageForm(TestPng.Create(300, 100)), Ct));
        Assert.Equal((1, (int?)1), (mine.Versions.Single().VersionNo, mine.CurrentVersionNo));
        var headSignature = await ReadAsync<UserSignatureDto>(await head.PostAsync("/api/me/signature", ImageForm(TestPng.Create(240, 80)), Ct));
        Assert.Equal(HttpStatusCode.OK, (await head.GetAsync("/api/me/signature/image", Ct)).StatusCode);
        // Chỉ nhận PNG.
        Assert.Contains("PNG", await TitleAsync(await head.PostAsync("/api/me/signature", ImageForm([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]), Ct)));

        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 2));
        pr = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(head, "purchase-requests", pr.Id, "approve"));
        Assert.Equal("Approved", pr.Status);

        // Thay chữ ký sau khi đã duyệt: phiếu cũ vẫn in chữ ký cũ (người dùng chốt 18/11/2026).
        headSignature = await ReadAsync<UserSignatureDto>(await head.PostAsync("/api/me/signature", ImageForm(TestPng.Create(200, 200)), Ct));
        Assert.Equal([2, 1], headSignature.Versions.Select(v => v.VersionNo));
        Assert.NotNull(headSignature.Versions[1].ValidTo);

        var view = await f.Buyer.GetAsync($"/api/purchase-requests/{pr.Id}/pdf", Ct);
        Assert.Equal("application/pdf", view.Content.Headers.ContentType?.MediaType);
        var images = PdfImages(await view.Content.ReadAsByteArrayAsync(Ct));
        Assert.Contains((300, 100), images);
        Assert.Contains((240, 80), images);
        Assert.DoesNotContain((200, 200), images);
        // Xem không ghi; In và Tải PDF ghi lần in 1, 2.
        Assert.Empty((await f.Buyer.GetFromJsonAsync<List<DocumentPrintDto>>($"/api/purchase-requests/{pr.Id}/prints", Ct))!);
        Assert.True((await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/print", null, Ct)).IsSuccessStatusCode);
        Assert.True((await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/print?kind=download", null, Ct)).IsSuccessStatusCode);
        var prints = (await f.Buyer.GetFromJsonAsync<List<DocumentPrintDto>>($"/api/purchase-requests/{pr.Id}/prints", Ct))!;
        Assert.Equal([(2, "Download"), (1, "Print")], prints.Select(p => (p.PrintNo, p.Kind)));
        Assert.StartsWith("Đã duyệt đủ 1/1 bước", prints[0].ApprovalStatus);

        // Admin (người lập là quản trị) tải chữ ký thay: bắt buộc lý do; người được tải thay nhận thông báo. Người không có quyền → 403.
        var headId = headSignature.UserId;
        Assert.Contains("lý do", await TitleAsync(await f.Buyer.PostAsync($"/api/users/{headId}/signature", ImageForm(TestPng.Create(120, 40)), Ct)));
        var byAdmin = await ReadAsync<UserSignatureDto>(
            await f.Buyer.PostAsync($"/api/users/{headId}/signature", ImageForm(TestPng.Create(120, 40), "Theo bản ký mẫu"), Ct));
        Assert.True(byAdmin.Versions[0].ByAdmin);
        Assert.Contains((await head.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Ct))!, n => n.Title.Contains("Chữ ký của bạn"));
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/users/{mine.UserId}/signature", Ct)).StatusCode);
        // Danh xưng nhập ở danh mục người dùng (in trước họ tên dưới chữ ký, ví dụ ô Chủ tịch "AHLĐ. …").
        var headUser = (await f.Buyer.GetFromJsonAsync<Proc.Application.Administration.UserDetailDto>($"/api/users/{headId}", Ct))!;
        headUser = await ReadAsync<Proc.Application.Administration.UserDetailDto>(await f.Buyer.PutAsJsonAsync($"/api/users/{headId}",
            new Proc.Application.Administration.UpdateUserRequest(headUser.FullName, headUser.Email, true, headUser.Position, " AHLĐ. "), Ct));
        Assert.Equal("AHLĐ.", headUser.Honorific);
        Assert.Equal("AHLĐ.", (await head.GetFromJsonAsync<UserSignatureDto>("/api/me/signature", Ct))!.Honorific);
        // Ngừng dùng chữ ký của mình: không còn chữ ký đang dùng.
        var retired = await ReadAsync<UserSignatureDto>(await f.Buyer.PostAsJsonAsync("/api/me/signature/retire", new SignatureReasonRequest(null), Ct));
        Assert.Null(retired.CurrentVersionNo);
    }

    [Fact]
    public async Task LogoCongTy_TaiThayXoa_InVaoPhieu()
    {
        var f = await ArrangeAsync();
        var companyId = (await f.Buyer.GetFromJsonAsync<List<Proc.Application.Organization.CompanyListItemDto>>("/api/companies", Ct))!
            .Single(c => c.Code == f.CompanyCode).Id;
        Assert.False((await f.Buyer.GetFromJsonAsync<CompanyLogoDto>($"/api/companies/{companyId}/logo", Ct))!.HasLogo);
        var logo = await ReadAsync<CompanyLogoDto>(await f.Buyer.PostAsync($"/api/companies/{companyId}/logo", ImageForm(TestPng.Create(160, 150)), Ct));
        Assert.Equal((true, 160, 150), (logo.HasLogo, logo.Width, logo.Height));
        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 1));
        var pdf = await (await f.Buyer.GetAsync($"/api/purchase-requests/{pr.Id}/pdf", Ct)).Content.ReadAsByteArrayAsync(Ct);
        Assert.Contains((160, 150), PdfImages(pdf));
        Assert.Equal(HttpStatusCode.NoContent, (await f.Buyer.DeleteAsync($"/api/companies/{companyId}/logo", Ct)).StatusCode);
        Assert.False((await f.Buyer.GetFromJsonAsync<CompanyLogoDto>($"/api/companies/{companyId}/logo", Ct))!.HasLogo);
    }

    [Fact]
    public void KhoiKy_GhepBuocTheoLoaiNguoiDuyetVaMaVaiTro_BuocLaVaoOPhatSinh()
    {
        var at = new DateTimeOffset(2026, 11, 7, 9, 5, 0, TimeSpan.Zero);
        var sig = new SheetImage([1], 10, 5);
        SheetApprovalStep Step(int stage, int level, ApproverKind kind, string? code, string status = "Approved") =>
            new(stage, level, kind, code, code ?? kind.ToString(), status, $"Người {level}", status == "Pending" ? null : at, status == "Approved" ? sig : null);
        var block = PurchaseRequestSheet.Build("Submitted", "AHLĐ. Đinh Văn Vui", at, sig, null,
        [
            Step(1, 1, ApproverKind.Storekeeper, null), Step(1, 2, ApproverKind.DepartmentHead, null), Step(1, 3, ApproverKind.Role, "TPCUVT"),
            Step(1, 4, ApproverKind.Role, "TPTCKT", "Pending"), Step(1, 5, ApproverKind.DivisionDirector, null, "Pending"),
            Step(6, 1, ApproverKind.Role, "GDTCKT", "Pending"), Step(9, 6, ApproverKind.Role, "CHUTICHMOI", "Pending"),
        ]);
        Assert.Equal(PurchaseRequestSheet.Heading, block.Heading);
        Assert.Equal(
            ["Người đề xuất", "Thủ kho", "Trưởng Phòng/Ban", "Phòng CUVT", "Phòng TC-NQ-KT", "Trưởng phòng HC", "GĐ Khối phòng ban", "Phó CTUBCL & PT"],
            block.Boxes.Select(b => b.Title));
        Assert.Equal(["Người 1", "Người 2", "Người 3"], block.Boxes.Skip(1).Take(3).Select(b => b.Name));
        Assert.NotNull(block.Boxes[0].Signature);
        // TP HC, Phó CT không có bước: ô giữ nguyên mẫu, ghi "Không thuộc chuỗi duyệt".
        Assert.False(block.Boxes[5].InChain);
        Assert.Equal("Chưa duyệt", block.Boxes[4].Detail);
        // Chặng vượt dự toán vào ô phát sinh; cấp cao nhất (chặng 9) mã vai trò lạ vẫn vào ô Chủ tịch.
        Assert.Equal(("GDTCKT", "(Duyệt vượt dự toán)"), (block.Extra.Single().Title, block.Extra.Single().Caption));
        Assert.True(block.Final.InChain);
        Assert.Equal("Đang chờ duyệt – đã duyệt 3/7 bước, chờ Phòng TC-NQ-KT", block.ApprovalStatus);
        // Nháp: không in chữ ký nào.
        var draft = PurchaseRequestSheet.Build("Draft", "An", null, sig, null, []);
        Assert.Null(draft.Boxes[0].Signature);
        Assert.Equal("Nháp – chưa gửi duyệt", draft.ApprovalStatus);
    }
}
