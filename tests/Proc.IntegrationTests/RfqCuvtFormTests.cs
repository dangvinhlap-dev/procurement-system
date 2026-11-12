using System.Net;
using System.Net.Http.Json;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Infrastructure.Printing;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y6 (design.md mục 19.1 Q7, 19.3): loại mua của RFQ theo dòng đề nghị nguồn; biểu mẫu 3A / 3a / 3B chốt lúc chọn thầu theo ngưỡng so sánh
/// 3 NCC của công ty; 3A bắt buộc lý do chọn; in khổ ngang theo mẫu Excel.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>RFQ một NCC (mời thiếu có giải trình), trọng số mặc định.</summary>
    private static SaveRfqRequest RfqOf(Fixture f, string title, params SaveRfqLineRequest[] lines) =>
        new(Today, title, Today.AddDays(7), null, new RfqWeightsDto(60, 20, 10, 5, 5), null, lines, [f.SupplierId]);

    /// <summary>Gửi RFQ (giải trình mời thiếu NCC) rồi nhập báo giá của NCC fixture: mỗi dòng một đơn giá, VAT 10%, giao 2 ngày.</summary>
    private static async Task<RfqDto> QuoteAsync(Fixture f, RfqDto rfq, params decimal[] prices)
    {
        await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/send", new SendRfqRequest("Chỉ một NCC cung cấp"), Ct));
        return await ReadAsync<RfqDto>(await f.Buyer.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{f.SupplierId}",
            new SaveQuotationRequest(Today, null, "Chuyển khoản sau 7 ngày", 8, 8, 8, 8, "Giá đã gồm vận chuyển",
                rfq.Lines.Select((l, i) => new SaveQuotationLineRequest(l.Id, prices[i], 0, "10", 2, null)).ToList()), Ct));
    }

    [Fact]
    public async Task Rfq_LoaiMuaTheoDeNghi_BieuMau3A_3a_3B_InKhoNgang()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var project = await ProjectAsync(f, 1_000_000_000m);
        var prA = await ApprovedPrAsync(f, approver, "Construction", project.Id, 8);
        await _factory.ApproveSummaryForRequestAsync(prA.Id);
        var prB = await ApprovedPrAsync(f, approver, "Goods", null, 5);
        await _factory.ApproveSummaryForRequestAsync(prB.Id);

        // Không gộp dòng đề nghị loại A với dòng đề nghị B trong một RFQ.
        Assert.Contains("Không gộp", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/rfqs", RfqOf(f, "Gộp",
            new(null, f.ItemId, f.Cai, 8, prA.Lines[0].Id, null), new(null, f.ItemId, f.Cai, 5, prB.Lines[0].Id, null)), Ct)));

        // RFQ loại A: dòng đề nghị A + dòng gõ tay (vận chuyển); chưa chọn thầu thì chưa biết 3A hay 3a.
        var rfqA = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync("/api/rfqs", RfqOf(f, "Xà gồ nhà tứ diện",
            new(null, f.ItemId, f.Cai, 8, prA.Lines[0].Id, null), new(null, f.ItemId, f.Cai, 1, null, "Vận chuyển")), Ct));
        Assert.Equal(("Construction", (Guid?)project.Id, (string?)null, 0m), (rfqA.Category, rfqA.ConstructionProjectId, rfqA.FormCode, rfqA.ComparisonThreshold));
        Assert.Contains(project.Name, rfqA.ConstructionProjectName);
        rfqA = await QuoteAsync(f, rfqA, 330_000, 100_000);

        // In trước khi chọn thầu: bảng so sánh 3A, khổ ngang, ghi chưa chọn thầu.
        var preview = await PdfOfAsync(await f.Buyer.GetAsync($"/api/rfqs/{rfqA.Id}/pdf", Ct));
        Assert.Equal($"SO SÁNH GIÁ VÀ CHỌN NHÀ CUNG CẤP {rfqA.No}", preview.Info.Title);
        Assert.True(preview.Pages[0].Width.Point > preview.Pages[0].Height.Point, "Mẫu 3A khổ ngang.");

        // Ngưỡng mặc định 0 → 3A: thiếu lý do chọn thì chặn.
        var award = new AwardRfqRequest(rfqA.Lines.Select(l => new AwardRfqLineRequest(l.Id, f.SupplierId, null)).ToList(), "Chỉ một NCC cung cấp");
        Assert.Contains("lý do chọn", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfqA.Id}/award", award, Ct)));
        rfqA = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfqA.Id}/award", award with { SelectionReason = "Giá tốt, giao nhanh" }, Ct));
        Assert.Equal(("AwardPending", "3A", "Giá tốt, giao nhanh"), (rfqA.Status, rfqA.FormCode, rfqA.SelectionReason));

        var context = new RfqPrintContext("Công ty thử", Today, project.Name, 1_000_000_000m, 0, "Xưởng cán", null, "Thi công",
            new Dictionary<Guid, RfqPrintSupplier> { [f.SupplierId] = new("QL 51, Biên Hòa", null, false) },
            new Dictionary<Guid, decimal> { [rfqA.Lines[0].Id] = 95_000 }, rfqA.Lines.ToDictionary(l => l.Id, _ => (string?)"10"));
        var form3A = CuvtPrintForms.Rfq(rfqA, context, null).Single();
        Assert.Equal(("3A", true, "ĐANG CHỜ DUYỆT – chưa có hiệu lực"), (form3A.FormNo, form3A.Landscape, form3A.StatusNote));
        // 8 × 330.000 + 1 × 100.000 = 2.740.000; VAT 274.000; gồm thuế 3.014.000. NCC chưa có PO đã duyệt → "MỚI", không điện thoại → "Chưa ghi".
        Assert.Equal(["", "2.740.000"], form3A.TotalCells);
        Assert.Equal("274.000", form3A.ExtraRows![0].Cells[1].Text);
        Assert.Equal("3.014.000", form3A.ExtraRows[1].Cells[1].Text);
        Assert.Contains("Cũ/mới: MỚI", form3A.Columns[4].Group);
        Assert.Contains("Điện thoại: Chưa ghi", form3A.Columns[4].Group);
        Assert.Equal(("Đề xuất chọn", "NCC mua hàng"), (form3A.AfterTable![0].Label, form3A.AfterTable[0].Value));
        Assert.Equal("Giá tốt, giao nhanh", form3A.AfterTable[1].Value);
        Assert.Equal("1.000.000.000 đ", form3A.Info.Single(i => i.Label == "Định mức/tổng dự toán").Value);
        Assert.Equal("2 ngày sau khi đặt hàng", form3A.ExtraRows[3].Cells[0].Text);
        Assert.DoesNotContain(form3A.Info, i => i.Value.Contains('…'));

        // In bảng trình giá 3a: rút về, đặt ngưỡng 5.000.000 > giá trị trúng thầu → 3a, không bắt buộc lý do chọn.
        await ReadAsync<RfqDto>(await f.Buyer.PostAsync($"/api/rfqs/{rfqA.Id}/recall", null, Ct));
        (await f.Buyer.PutAsJsonAsync($"/api/system-parameters/{SystemParameterCodes.RfqComparisonThreshold}", new UpdateSystemParameterRequest("5.000.000"), Ct))
            .EnsureSuccessStatusCode();
        rfqA = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfqA.Id}/award", award, Ct));
        Assert.Equal(("3a", 5_000_000m), (rfqA.FormCode, rfqA.ComparisonThreshold));
        var pdf3a = await PdfOfAsync(await f.Buyer.GetAsync($"/api/rfqs/{rfqA.Id}/pdf", Ct));
        Assert.Equal($"BẢNG TRÌNH DUYỆT GIÁ KHÁCH HÀNG {rfqA.No}", pdf3a.Info.Title);
        var form3a = CuvtPrintForms.Rfq(rfqA, context, null).Single();
        Assert.Equal(("3a", "Cộng", 7), (form3a.FormNo, form3a.TotalLabel, form3a.Columns.Count));
        Assert.Equal(("Tên công trình", project.Name), (form3a.Info[0].Label, form3a.Info[0].Value));
        Assert.Equal("95.000", form3a.Rows[0][6]);
        Assert.Equal(("Thuế VAT (10%)", "274.000"), (form3a.ExtraRows![0].Label, form3a.ExtraRows[0].Cells[0].Text));
        Assert.StartsWith("Ba triệu", form3a.AfterTable![0].Value);
        Assert.Equal("Chuyển khoản sau 7 ngày", form3a.AfterTable[1].Value);
        // Vẫn in được bảng so sánh 3A theo yêu cầu.
        await PdfOfAsync(await f.Buyer.GetAsync($"/api/rfqs/{rfqA.Id}/pdf?form=3A", Ct));

        // RFQ loại B (dòng đề nghị hàng hóa) → 3B, không bắt buộc lý do chọn; không in 3A cho loại B.
        var rfqB = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync("/api/rfqs", RfqOf(f, "Dây curoa tháng 11",
            new SaveRfqLineRequest(null, f.ItemId, f.Cai, 5, prB.Lines[0].Id, null)), Ct));
        Assert.Equal(("Goods", "3B"), (rfqB.Category, rfqB.FormCode));
        rfqB = await QuoteAsync(f, rfqB, 90_000);
        rfqB = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfqB.Id}/award",
            new AwardRfqRequest([new(rfqB.Lines[0].Id, f.SupplierId, null)], "Chỉ một NCC cung cấp"), Ct));
        Assert.Equal("3B", rfqB.FormCode);
        var pdf3B = await PdfOfAsync(await f.Buyer.GetAsync($"/api/rfqs/{rfqB.Id}/pdf", Ct));
        Assert.True(pdf3B.Pages[0].Width.Point > pdf3B.Pages[0].Height.Point, "Mẫu 3B khổ ngang.");
        Assert.Contains("loại A", await TitleAsync(await f.Buyer.GetAsync($"/api/rfqs/{rfqB.Id}/pdf?form=3A", Ct)));

        // Người không có quyền xem RFQ không in được.
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.GetAsync($"/api/rfqs/{rfqB.Id}/pdf", Ct)).StatusCode);
    }
}
