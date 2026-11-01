using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Proc.Application.Analysis;
using Proc.Application.MasterData;
using Proc.Domain.Organization;
using static Proc.IntegrationTests.PurchaseHistoryTests;

namespace Proc.IntegrationTests;

/// <summary>
/// Phân tích giá hợp nhất (design.md mục 18.3, lát E3): gom theo mặt hàng chung × ĐVT giữa các pháp nhân, giá mục tiêu, tiết kiệm dự
/// kiến, bỏ dòng chưa ánh xạ/ngoại tệ/đơn giá 0, khoảng ngày, quyền xem toàn tập đoàn, phạm vi nhóm hàng.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchaseAnalysisTests
{
    private static readonly string[] Headers =
        ["Mã vật tư *", "Tên vật tư *", "ĐVT *", "Ngày mua *", "Số lượng *", "Đơn giá trước VAT *", "Tên NCC *", "MST NCC", "Tiền tệ"];

    private readonly ProcApiFactory _factory;

    public PurchaseAnalysisTests(ProcApiFactory factory) => _factory = factory;

    /// <summary>Một dòng mua: mã vật tư, ĐVT, ngày (dd/MM/yyyy), SL, đơn giá, NCC, MST, tiền tệ.</summary>
    private static string?[] Line(string code, string uom, string date, string qty, string price, string supplier = "NCC Một",
        string? taxCode = null, string? currency = null) =>
        [code, $"Hàng {code}", uom, date, qty, price, supplier, taxCode, currency];

    private static byte[] File(params string?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Dữ liệu mua");
        for (var c = 0; c < Headers.Length; c++) sheet.Cell(1, c + 1).Value = Headers[c];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                if (rows[r][c] is { } value) sheet.Cell(r + 2, c + 1).SetValue(value);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<PurchaseAnalysisDto> AnalyzeAsync(HttpClient client, string query) =>
        await ReadAsync<PurchaseAnalysisDto>(await client.GetAsync($"/api/purchase-history/analysis?{query}", Ct));

    [Fact]
    public async Task HaiPhapNhan_ViDuDeXuat_GiaMucTieuVaTietKiem()
    {
        var (head, bha) = await AdminAsync(_factory);
        var item = await NewItemAsync(head);
        var supplier = await NewSupplierAsync(head);
        // Ví dụ đề xuất: tổng công ty 12.000 × 100.000 đ (hai lần mua), BHA 8.000 × 110.000 đ; giảm 5% → 95.000 đ, tiết kiệm 180 triệu.
        await ImportAsync(head, File(
            Line(item.Code, "Cái", "10/01/2026", "4.000", "95.000", supplier.Name, supplier.TaxCode),
            Line(item.Code, "cái", "20/02/2026", "8.000", "102.500", "NCC Hai")), commit: true);
        await ImportAsync(bha, File(Line(item.Code, "CÁI", "15/02/2026", "8.000", "110.000", "NCC Ba")), commit: true);

        var result = await AnalyzeAsync(head, "from=2026-01-01&to=2026-03-31&allCompanies=true");

        var row = Assert.Single(result.Rows, r => r.ItemId == item.Id);
        Assert.Equal((2, 20_000m, 2_080_000_000m, 95_000m, 180_000_000m, 8.65m),
            (row.CompanyCount, row.Quantity, row.Amount, row.TargetPrice, row.Savings, row.SavingsPct));
        Assert.Equal((95_000m, 110_000m, 104_000m, 10m, 3, 3), (row.MinPrice, row.MaxPrice, row.AvgPrice, row.PriceGapPct, row.PurchaseCount, row.SupplierCount));
        // "Cái", "cái", "CÁI" là một ĐVT; NCC giá tốt nhất là NCC đã ánh xạ (95.000 đ) – hiện mã – tên.
        Assert.Equal($"{supplier.Code} – {supplier.Name}", row.BestSupplierName);
        Assert.Equal(95_000m, row.BestSupplierPrice);
        var tct = row.Companies[0];
        Assert.Equal(("TCT", 100_000m, 102_500m, new DateOnly(2026, 2, 20), 2, 60_000_000m),
            (tct.CompanyCode, tct.AvgPrice, tct.LastPrice, tct.LastDate, tct.SupplierCount, tct.Savings));
        Assert.Equal(("BHA", 120_000_000m), (row.Companies[1].CompanyCode, row.Companies[1].Savings));
        Assert.True(result.AllCompanies);
        Assert.True(result.Savings >= row.Savings);

        // Công ty đang làm việc: chỉ số liệu của BHA, không có gì để hợp nhất → tiết kiệm 0 ở giảm 0%.
        var own = Assert.Single((await AnalyzeAsync(bha, "from=2026-01-01&to=2026-03-31&targetDiscountPct=0")).Rows, r => r.ItemId == item.Id);
        Assert.Equal((1, "BHA", 110_000m, 0m), (own.CompanyCount, own.Companies[0].CompanyCode, own.TargetPrice, own.Savings));

        // Khoảng ngày chỉ gồm 10/01: còn một dòng của tổng công ty.
        var january = Assert.Single((await AnalyzeAsync(head, "from=2026-01-01&to=2026-01-31&allCompanies=true")).Rows, r => r.ItemId == item.Id);
        Assert.Equal((1, 4_000m), (january.CompanyCount, january.Quantity));
    }

    [Fact]
    public async Task KhacDvt_TachDong_BoDongChuaAnhXaNgoaiTeGiaKhong()
    {
        var (head, bha) = await AdminAsync(_factory);
        var item = await NewItemAsync(head);
        var unknown = $"CU{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var before = await AnalyzeAsync(bha, "from=2025-06-01&to=2025-06-30");

        await ImportAsync(bha, File(
            Line(item.Code, "Cái", "05/06/2025", "10", "1.000"),
            Line(item.Code, "Hộp", "05/06/2025", "2", "9.000"),
            Line(item.Code, "Cái", "06/06/2025", "5", "40", currency: "USD"),
            Line(item.Code, "Cái", "07/06/2025", "1", "0"),
            Line(unknown, "Cái", "08/06/2025", "3", "2.000")), commit: true);

        var result = await AnalyzeAsync(bha, "from=2025-06-01&to=2025-06-30");

        var rows = result.Rows.Where(r => r.ItemId == item.Id).OrderBy(r => r.Uom).ToList();
        Assert.Equal([("Cái", 10m, 10_000m), ("Hộp", 2m, 18_000m)], rows.Select(r => (r.Uom, r.Quantity, r.Amount)));
        Assert.Equal(before.UnmappedRecords + 1, result.UnmappedRecords);
        Assert.Equal(before.UnmappedAmount + 6_000m, result.UnmappedAmount);
        Assert.Equal(before.ForeignCurrencyRecords + 1, result.ForeignCurrencyRecords);
        Assert.Equal(before.ZeroPriceRecords + 1, result.ZeroPriceRecords);
    }

    [Fact]
    public async Task ToanTapDoan_CanQuyen_NhomHangNgoaiPhamViKhongHien()
    {
        var (head, bha) = await AdminAsync(_factory);
        var visible = await NewItemAsync(head);
        var hidden = await NewItemAsync(head);
        await ImportAsync(bha, File(
            Line(visible.Code, "Cái", "12/07/2025", "1", "1.000"),
            Line(hidden.Code, "Cái", "12/07/2025", "1", "1.000")), commit: true);
        var categories = (await head.GetFromJsonAsync<List<ItemCategoryDetailDto>>("/api/item-categories", Ct))!;
        var category = categories.Single(c => c.Id == visible.CategoryId).Code;

        var roleCode = $"PAV{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(roleCode, Permissions.Analysis.PurchaseHistoryView);
        var userName = $"pa.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "BHA", roleCode, categoryCodes: [category]);
        var viewer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.GetAsync("/api/purchase-history/analysis?allCompanies=true", Ct)).StatusCode);
        var rows = (await AnalyzeAsync(viewer, "from=2025-07-12&to=2025-07-12")).Rows;
        Assert.Contains(rows, r => r.ItemId == visible.Id);
        Assert.DoesNotContain(rows, r => r.ItemId == hidden.Id);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await viewer.GetAsync("/api/purchase-history/analysis?from=2025-08-01&to=2025-07-01", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await viewer.GetAsync("/api/purchase-history/analysis?targetDiscountPct=100", Ct)).StatusCode);
    }
}
