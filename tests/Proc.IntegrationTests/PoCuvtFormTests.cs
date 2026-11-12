using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Printing;
using Proc.Application.Purchasing;
using Proc.Infrastructure.Printing;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y7 (design.md mục 19.1 Q10, 19.3): PO có loại mua theo dòng đề nghị nguồn (A cùng một công trình, không trộn với B/DV); in bảng theo dõi
/// mua hàng 4A/4B khổ ngang – giá cũ gần nhất, giá mới tăng / bằng–giảm, lũy kế = đã mua trước đây + đơn này, còn lại theo dự toán/định mức.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>Nội dung 4A/4B của PO đọc trong phạm vi công ty fixture (để kiểm số liệu – file PDF không đọc lại được chữ).</summary>
    private async Task<CuvtForm> PoFormAsync(Fixture f, Guid poId)
    {
        using var scope = _factory.Services.CreateScope();
        var companyId = await _factory.GetCompanyIdAsync(f.CompanyCode);
        ((CurrentUserScope)scope.ServiceProvider.GetRequiredService<Proc.Application.Common.ICurrentUserScope>())
            .Initialize(Guid.CreateVersion7(), companyId, [], true, [], true, []);
        var service = (DocumentPdfService)scope.ServiceProvider.GetRequiredService<IDocumentPdfService>();
        return await service.PurchaseOrderFormAsync(poId, Ct);
    }

    private static Task<HttpResponseMessage> CreatePoLinesAsync(Fixture f, params SavePurchaseOrderLineRequest[] lines) =>
        f.Buyer.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, f.SupplierId, f.Kt, Today.AddDays(5), "Chuyển khoản 30 ngày", null, "Kho công trình", 0, null, lines), Ct);

    [Fact]
    public async Task Po_LoaiMuaTheoDeNghi_BangTheoDoi4A_4B_LuyKeGomDonNay()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var project = await ProjectAsync(f, 1_000_000m);
        var prA = await ApprovedPrAsync(f, approver, "Construction", project.Id, 8);
        await _factory.ApproveSummaryForRequestAsync(prA.Id);
        var prB = await ApprovedPrAsync(f, approver, "Goods", null, 5);
        await _factory.ApproveSummaryForRequestAsync(prB.Id);

        // Không gộp dòng đề nghị loại A với dòng đề nghị B trong một đơn (mỗi đơn in một biểu mẫu).
        Assert.Contains("Không gộp", await TitleAsync(await CreatePoLinesAsync(f,
            new(null, f.ItemId, f.Cai, 3, 100_000, 5, "10", null, null, prA.Lines[0].Id),
            new(null, f.ItemId, f.Cai, 5, 100_000, 5, "10", null, null, prB.Lines[0].Id))));

        // PO 1 (loại A): 3 × 100.000, CK 5% → 285.000 chưa VAT, VAT 28.500. Duyệt rồi in lại: lũy kế không tính trùng chính đơn này.
        var po1 = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 3, prA.Lines[0].Id));
        Assert.Equal(("Construction", (Guid?)project.Id, "4A"), (po1.Category, po1.ConstructionProjectId, po1.FormCode));
        Assert.Contains(project.Name, po1.ConstructionProjectName);
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po1.Id}/submit", null, Ct));
        await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po1.Id, "approve"));
        var form1 = await PoFormAsync(f, po1.Id);
        Assert.Equal(("4A", true, (string?)null, "BẢNG THEO DÕI MUA HÀNG"), (form1.FormNo, form1.Landscape, form1.StatusNote, form1.Title));
        Assert.Equal(["Lần đầu", "", "95.000", "285.000"], form1.Rows[0][5..9]);
        Assert.Equal(("285.000", "715.000"), (form1.Rows[0][10], form1.Rows[0][11]));
        Assert.Equal("285.000 đ", form1.Info.Single(i => i.Label == "Lũy kế số tiền đã mua").Value);
        Assert.Equal(("Người lập", "Đã duyệt"), (form1.Signers[0].Title, form1.Signers[1].Detail![..8]));

        // PO 2 (nháp): 5 × 120.000, CK 5% → giá mới 114.000 > giá cũ 95.000 → cột "tăng"; lũy kế = 285.000 + 570.000 = 855.000, còn lại 145.000.
        var po2 = await ReadAsync<PurchaseOrderDto>(await CreatePoLinesAsync(f, new SavePurchaseOrderLineRequest(null, f.ItemId, f.Cai, 5, 120_000, 5, "10", null, null, prA.Lines[0].Id)));
        var form2 = await PoFormAsync(f, po2.Id);
        Assert.Equal(("4A", "BẢN NHÁP – chưa duyệt"), (form2.FormNo, form2.StatusNote));
        Assert.Equal(["95.000", "114.000", "", "570.000"], form2.Rows[0][5..9]);
        Assert.StartsWith("NCC mua hàng", form2.Rows[0][9]);
        Assert.Equal(("855.000", "145.000"), (form2.Rows[0][10], form2.Rows[0][11]));
        Assert.Equal(("Tên công trình", project.Name), (form2.Info[0].Label, form2.Info[0].Value));
        Assert.Equal(("1.000.000 đ", "855.000 đ", "145.000 đ"),
            (form2.Info[2].Value, form2.Info.Single(i => i.Label == "Lũy kế số tiền đã mua").Value, form2.Info.Single(i => i.Label == "Số tiền còn lại").Value));
        Assert.Equal(["", "", "570.000", "", "855.000", "145.000"], form2.TotalCells);
        // VAT 57.000; lũy kế VAT 28.500 + 57.000 = 85.500; tổng gồm VAT 627.000, lũy kế gồm VAT 940.500.
        Assert.Equal(("Thuế VAT 10%", "57.000", "85.500"), (form2.ExtraRows![0].Label, form2.ExtraRows[0].Cells[2].Text, form2.ExtraRows[0].Cells[4].Text));
        Assert.Equal(("627.000", "940.500"), (form2.ExtraRows[1].Cells[2].Text, form2.ExtraRows[1].Cells[4].Text));
        Assert.Equal(form2.Columns.Count - form2.TotalSpan, form2.TotalCells.Count);
        Assert.DoesNotContain(form2.Info, i => i.Value.Contains('…'));

        // PO loại B: định mức chưa khai báo ghi rõ chữ; tên hàng hóa = mục đích đề nghị; PO 1 loại A không tính vào lũy kế bộ phận;
        // giá mới 95.000 bằng giá cũ → cột "bằng–giảm".
        var poB = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 5, prB.Lines[0].Id));
        Assert.Equal(("Goods", (Guid?)null, "4B"), (poB.Category, poB.ConstructionProjectId, poB.FormCode));
        var formB = await PoFormAsync(f, poB.Id);
        Assert.Equal(("4B", "Tên hàng hóa", "Thi công"), (formB.FormNo, formB.Info[0].Label, formB.Info[0].Value));
        Assert.Contains("Xưởng cán", formB.Info[1].Value);
        Assert.Equal(("Chưa khai báo", "475.000 đ", "Chưa khai báo"),
            (formB.Info[2].Value, formB.Info[3].Value, formB.Info[4].Value));
        Assert.Equal(["95.000", "", "95.000", "475.000"], formB.Rows[0][5..9]);

        // PO mua thẳng (không có dòng đề nghị) là loại B.
        var direct = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 1, null));
        Assert.Equal("4B", direct.FormCode);

        // In qua API: mặc định 4A/4B khổ ngang; form=supplier in đơn gửi NCC; biểu mẫu không khớp loại mua hoặc lạ thì báo lỗi.
        var pdf = await PdfOfAsync(await f.Buyer.GetAsync($"/api/purchase-orders/{po2.Id}/pdf", Ct));
        Assert.Equal($"BẢNG THEO DÕI MUA HÀNG {po2.No}", pdf.Info.Title);
        Assert.True(pdf.Pages[0].Width.Point > pdf.Pages[0].Height.Point, "Mẫu 4A khổ ngang.");
        await PdfOfAsync(await f.Buyer.GetAsync($"/api/purchase-orders/{po2.Id}/pdf?form=4A", Ct));
        var supplierPdf = await PdfOfAsync(await f.Buyer.GetAsync($"/api/purchase-orders/{po2.Id}/pdf?form=supplier", Ct));
        Assert.Equal($"ĐƠN ĐẶT HÀNG {po2.No}", supplierPdf.Info.Title);
        Assert.Contains("4A", await TitleAsync(await f.Buyer.GetAsync($"/api/purchase-orders/{po2.Id}/pdf?form=4B", Ct)));
        Assert.Contains("không hợp lệ", await TitleAsync(await f.Buyer.GetAsync($"/api/purchase-orders/{po2.Id}/pdf?form=5A", Ct)));
    }
}
