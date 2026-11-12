using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Proc.Application.Purchasing;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Printing;
using Proc.Infrastructure.Purchasing;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y10 (design.md mục 10.10, 19.1 Q13): in theo biểu mẫu CUVT – đề nghị mua loại A → 1A, B → 1B, dịch vụ → 1B không cột tồn; bảng tổng
/// hợp 2A/2B khổ ngang, mỗi đơn vị mua một cột đánh "x"; khối ký sinh từ chuỗi duyệt (họ tên + thời điểm người đã duyệt).
/// </summary>
public partial class PurchasingTests
{
    /// <summary>Đọc file PDF trả về: đúng kiểu nội dung, mở được bằng PDFsharp.</summary>
    private static async Task<PdfDocument> PdfOfAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        return PdfReader.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync(Ct)), PdfDocumentOpenMode.Import);
    }

    [Fact]
    public async Task InBieuMauCuvt_1A_1B_DichVu_2A()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var project = await ProjectAsync(f, 1_000_000_000m);

        // 1A: công trình, dự toán, lũy kế quá khứ; bảng có nhóm "Số lượng" (Cần sử dụng – Tồn kho – Cần mua); người duyệt in tên + giờ duyệt.
        var prA = await ApprovedPrAsync(f, approver, "Construction", project.Id, 8);
        var pdfA = await PdfOfAsync(await f.Buyer.GetAsync($"/api/purchase-requests/{prA.Id}/pdf", Ct));
        Assert.Equal($"ĐỀ NGHỊ MUA VẬT TƯ CÁC CÔNG TRÌNH {prA.No}", pdfA.Info.Title);
        Assert.True(pdfA.Pages[0].Height.Point > pdfA.Pages[0].Width.Point, "Mẫu 1A khổ dọc.");
        var formA = CuvtPrintForms.PurchaseRequest(prA, "Công ty thử", new Dictionary<Guid, string?>());
        Assert.Equal("1A", formA.FormNo);
        Assert.Equal(("Tên công trình", project.Name), (formA.Info[0].Label, formA.Info[0].Value));
        Assert.Equal("1.000.000.000 đ", formA.Info[1].Value);
        Assert.True(formA.Info[1].Strong, "Số tiền phải in đậm.");
        Assert.Equal(3, formA.Columns.Count(c => c.Group == "Số lượng"));
        Assert.Equal("8", formA.Rows.Single()[6]);
        Assert.Equal("Người đề nghị", formA.Signers[0].Title);
        var approved = formA.Signers[1];
        Assert.NotNull(approved.Name);
        Assert.StartsWith("Đã duyệt ", approved.Detail);
        Assert.DoesNotContain(formA.Info, i => i.Value.Contains('…'));

        // 1B (hàng hóa): ngân sách chưa khai báo ghi rõ chữ, không để "…"; dịch vụ: mẫu 1B bỏ ba cột tồn.
        var prB = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, "Goods", null, 5));
        var pdfB = await PdfOfAsync(await f.Buyer.GetAsync($"/api/purchase-requests/{prB.Id}/pdf", Ct));
        Assert.Equal($"ĐỀ NGHỊ MUA HÀNG HÓA THIẾT BỊ {prB.No}", pdfB.Info.Title);
        var formB = CuvtPrintForms.PurchaseRequest(prB, "Công ty thử", new Dictionary<Guid, string?>());
        Assert.Equal(("1B", 10, "BẢN NHÁP – chưa duyệt"), (formB.FormNo, formB.Columns.Count, formB.StatusNote));
        Assert.Equal("Thi công", formB.Info.Single(i => i.Label == "Mục đích sử dụng").Value);
        var prS = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, "Service", null, 2));
        await PdfOfAsync(await f.Buyer.GetAsync($"/api/purchase-requests/{prS.Id}/pdf", Ct));
        var formS = CuvtPrintForms.PurchaseRequest(prS, "Công ty thử", new Dictionary<Guid, string?>());
        Assert.Equal(("1B", 8), (formS.FormNo, formS.Columns.Count));
        Assert.DoesNotContain(formS.Columns, c => c.Group is not null);

        // 2A: khổ ngang; ngày đề xuất, số phiếu, mục đích lấy từ đề nghị; đơn vị mua là cột đánh "x"; lũy kế = đã mua + đề xuất.
        var unit = await UnitAsync(f);
        var summary = await ReadAsync<PurchaseSummaryDto>(await SaveSummaryAsync(f, "2A", f.DepartmentId, project.Id, [new(prA.Lines[0].Id, unit.Id, null)]));
        Assert.Equal((prA.Date, "Thi công"), (summary.Lines[0].RequestDate, summary.Lines[0].RequestPurpose));
        var pdf2A = await PdfOfAsync(await f.Buyer.GetAsync($"/api/purchase-summaries/{summary.Id}/pdf", Ct));
        Assert.Equal($"BẢNG TỔNG HỢP CÁC ĐỀ XUẤT MUA VẬT TƯ {summary.No}", pdf2A.Info.Title);
        Assert.True(pdf2A.Pages[0].Width.Point > pdf2A.Pages[0].Height.Point, "Mẫu 2A khổ ngang.");
        var form2A = CuvtPrintForms.PurchaseSummary(summary, "Công ty thử");
        Assert.Equal("2A", form2A.FormNo);
        Assert.Equal(("Phòng CUVT", "Đơn vị mua"), (form2A.Columns[^1].Title, form2A.Columns[^1].Group));
        var row = form2A.Rows.Single();
        Assert.Equal((prA.Date.ToString("dd/MM/yyyy"), prA.No, "x"), (row[0], row[1], row[^1]));
        Assert.Equal(
            (summary.Frame.AccumulatedAmount + summary.TotalAmount).ToString("#,##0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " đ",
            form2A.Info.Single(i => i.Label == "Lũy kế số tiền đã mua").Value);
        Assert.Equal(form2A.Columns.Count - form2A.TotalSpan, form2A.TotalCells.Count);
        Assert.Equal(["Người lập"], form2A.Signers.Select(s => s.Title));

        // Không có quyền xem bảng tổng hợp: không in được.
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.GetAsync($"/api/purchase-summaries/{summary.Id}/pdf", Ct)).StatusCode);
    }

    [Fact]
    public async Task LuyKeTrongThang_1B_ChiTinhPoDaDuyet_ChuaVat()
    {
        // Người dùng chốt 12/11/2026: dòng "Lũy kế số tiền đã mua trong tháng" của 1B chỉ tính PO đã duyệt trong tháng, chưa VAT – giống 1A.
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var pr = await ApprovedPrAsync(f, approver, "Goods", null, 10);
        await _factory.ApproveSummaryForRequestAsync(pr.Id);

        // PO 8 × 100.000, CK 5% → 760.000 chưa VAT (đã duyệt – tính); PO 2 cái chỉ gửi duyệt – không tính.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 8, pr.Lines[0].Id));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));
        var pending = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 2, pr.Lines[0].Id));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{pending.Id}/submit", null, Ct));

        using var scope = _factory.Services.CreateScope();
        var companyId = await _factory.GetCompanyIdAsync(f.CompanyCode);
        ((CurrentUserScope)scope.ServiceProvider.GetRequiredService<Proc.Application.Common.ICurrentUserScope>())
            .Initialize(Guid.CreateVersion7(), companyId, [], true, [], true, []);
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        Assert.Equal(760_000m, await DepartmentSpending.MonthAsync(db, f.DepartmentId, Today, Ct));
        Assert.Equal(0m, await DepartmentSpending.MonthAsync(db, f.DepartmentId, Today.AddMonths(-1), Ct));

        // In 1B: dòng lũy kế lấy đúng số này (in đậm, đơn vị "đ").
        var form = CuvtPrintForms.PurchaseRequest(pr, "Công ty thử", new Dictionary<Guid, string?>(), 760_000m);
        Assert.Equal("760.000 đ", form.Info.Single(i => i.Label == "Lũy kế số tiền đã mua trong tháng").Value);
        await PdfOfAsync(await f.Buyer.GetAsync($"/api/purchase-requests/{pr.Id}/pdf", Ct));
    }
}
