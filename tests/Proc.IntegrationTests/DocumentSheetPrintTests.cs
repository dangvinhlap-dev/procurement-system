using System.Net;
using System.Net.Http.Json;
using Proc.Application.Documents;
using Proc.Application.Purchasing;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Printing;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát IN2 (design.md mục 20.4): các phiếu khác in giữ nguyên sheet biểu mẫu như 1A – chữ ký người đã duyệt theo phiên bản, góc thông tin hệ thống,
/// nhật ký in (In / Tải PDF ghi lần in, Xem thì không). Đợt IN2a: bảng theo dõi mua hàng 4A/4B của PO.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>Máy dev đặt biến môi trường <c>PROC_PRINT_OUT</c> (thư mục) để lưu PDF ra đối chiếu bằng mắt với sheet Excel.</summary>
    private static void SavePdfForReview(string name, byte[] pdf)
    {
        var folder = Environment.GetEnvironmentVariable("PROC_PRINT_OUT");
        if (!string.IsNullOrWhiteSpace(folder)) File.WriteAllBytes(Path.Combine(folder, name), pdf);
    }

    [Fact]
    public async Task Po_BangTheoDoiGiuNguyenSheet_ChuKyNguoiDuyet_DemLanIn()
    {
        var f = await ArrangeAsync();
        var (approver, roleId) = await ApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, roleId), Ct));
        await ReadAsync<UserSignatureDto>(await f.Buyer.PostAsync("/api/me/signature", ImageForm(TestPng.Create(300, 100)), Ct));
        await ReadAsync<UserSignatureDto>(await approver.PostAsync("/api/me/signature", ImageForm(TestPng.Create(240, 80)), Ct));

        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        // Nháp: xem được, chưa có chữ ký nào (dấu chìm "BẢN NHÁP").
        var draft = await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}/pdf", Ct);
        Assert.Equal("application/pdf", draft.Content.Headers.ContentType?.MediaType);
        var draftPdf = await draft.Content.ReadAsByteArrayAsync(Ct);
        SavePdfForReview($"{po.FormCode}-nhap.pdf", draftPdf);
        Assert.DoesNotContain((300, 100), PdfImages(draftPdf));

        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));
        Assert.Equal("Approved", po.Status);

        // Người lập ký lúc gửi duyệt, người duyệt ký lúc duyệt.
        var view = await (await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}/pdf", Ct)).Content.ReadAsByteArrayAsync(Ct);
        SavePdfForReview($"{po.FormCode}-da-duyet.pdf", view);
        var images = PdfImages(view);
        Assert.Contains((300, 100), images);
        Assert.Contains((240, 80), images);

        // Xem không ghi; In và Tải PDF ghi lần in 1, 2; phiếu không có → 404.
        Assert.Empty((await f.Buyer.GetFromJsonAsync<List<DocumentPrintDto>>($"/api/purchase-orders/{po.Id}/prints", Ct))!);
        Assert.True((await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/print", null, Ct)).IsSuccessStatusCode);
        Assert.True((await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/print?kind=download", null, Ct)).IsSuccessStatusCode);
        var prints = (await f.Buyer.GetFromJsonAsync<List<DocumentPrintDto>>($"/api/purchase-orders/{po.Id}/prints", Ct))!;
        Assert.Equal([(2, "Download"), (1, "Print")], prints.Select(p => (p.PrintNo, p.Kind)));
        Assert.StartsWith("Đã duyệt đủ 1/1 bước", prints[0].ApprovalStatus);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Buyer.GetAsync($"/api/purchase-orders/{Guid.CreateVersion7()}/prints", Ct)).StatusCode);
    }

    [Fact]
    public void KhoiKy4A_GhepTheoMaVaiTroMau_ChuTichCotCuoi()
    {
        var at = new DateTimeOffset(2026, 11, 18, 2, 30, 0, TimeSpan.Zero);
        var sig = new SheetImage([1], 10, 5);
        SheetApprovalStep Step(int level, string code, string status = "Approved") =>
            new(1, level, ApproverKind.Role, code, code, status, $"Người {level}", status == "Pending" ? null : at, status == "Approved" ? sig : null);
        var block = SheetSign.Build(DocumentPdfService.Sign4, "Submitted", "Lan", at, sig, null,
            [Step(1, "TPCUVT"), Step(2, "TPTCKT"), Step(3, "GDTCKT", "Pending"), Step(4, "CHUTICH", "Pending")]);
        // Chức danh nguyên văn ảnh khối ký trong sheet 4A/4B (kể cả "TC-NQ- KT" có khoảng trắng như mẫu).
        Assert.Equal(["Người lập bảng", "Trưởng phòng CUVT", "Trưởng phòng TC-NQ- KT", "GĐ Phòng TC-NQ-KT"], block.Boxes.Select(b => b.Title));
        Assert.Equal(("Chủ tịch HĐQT/TGĐ", "Chưa duyệt"), (block.Final.Title, block.Final.Detail));
        Assert.Equal(["Người 1", "Người 2"], block.Boxes.Skip(1).Take(2).Select(b => b.Name));
        Assert.Empty(block.Extra);
        Assert.Equal("Đang chờ duyệt – đã duyệt 2/4 bước, chờ GĐ Phòng TC-NQ-KT", block.ApprovalStatus);
    }
}
