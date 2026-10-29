using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Documents;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Đánh số chứng từ PO-{CTY}-{YYMM}-{0000} (design.md mục 5.4, 12.2): không trùng khi lưu đồng thời,
/// đếm riêng theo công ty và kỳ, giao dịch hủy thì trả lại số.
/// Mỗi test dùng một năm riêng (2091, 2092…) để bộ đếm không lẫn giữa các test.
/// </summary>
[Collection(ApiCollection.Name)]
public class DocumentNumberingTests
{
    private readonly ProcApiFactory _factory;

    public DocumentNumberingTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Lấy số trong một scope DI riêng (như một request riêng) làm việc tại công ty cho trước.</summary>
    private async Task<string> NextAsync(string company, string docType, DateOnly date)
    {
        var companyId = await _factory.GetCompanyIdAsync(company);
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentUserScope>()
            .Initialize(Guid.Empty, companyId, [], allWarehouses: true, [], allItemCategories: true, []);
        return await scope.ServiceProvider.GetRequiredService<IDocumentNumberService>().NextAsync(docType, date, Ct);
    }

    [Fact]
    public async Task LayDongThoi_50Lan_KhongTrungVaLienTuc()
    {
        var date = new DateOnly(2091, 3, 15);

        var numbers = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => NextAsync("BHA", "PO", date), Ct)));

        Assert.Equal(50, numbers.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, 50).Select(i => $"PO-BHA-9103-{i:0000}"),
            numbers.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DemRiengTheoCongTyVaThang()
    {
        Assert.Equal("PO-BHA-9201-0001", await NextAsync("BHA", "PO", new DateOnly(2092, 1, 5)));
        Assert.Equal("PO-NMI-9201-0001", await NextAsync("NMI", "PO", new DateOnly(2092, 1, 5)));
        Assert.Equal("PO-BHA-9202-0001", await NextAsync("BHA", "PO", new DateOnly(2092, 2, 1)));
        Assert.Equal("PO-BHA-9201-0002", await NextAsync("BHA", "PO", new DateOnly(2092, 1, 31)));
        // Loại chứng từ khác có bộ đếm riêng.
        Assert.Equal("GRN-BHA-9201-0001", await NextAsync("BHA", "GRN", new DateOnly(2092, 1, 5)));
    }

    [Fact]
    public async Task GiaoDichHuy_SoDuocTraLai()
    {
        var date = new DateOnly(2093, 6, 1);
        var companyId = await _factory.GetCompanyIdAsync("BHA");
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentUserScope>()
                .Initialize(Guid.Empty, companyId, [], allWarehouses: true, [], allItemCategories: true, []);
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            Assert.Equal("PO-BHA-9306-0001",
                await scope.ServiceProvider.GetRequiredService<IDocumentNumberService>().NextAsync("PO", date, Ct));
            // Lưu chứng từ lỗi → hủy giao dịch: số 0001 chưa dùng thì phải cấp lại, không nhảy số.
            await transaction.RollbackAsync(Ct);
        }

        Assert.Equal("PO-BHA-9306-0001", await NextAsync("BHA", "PO", date));
    }

    [Fact]
    public async Task DoiMauSo_QuaApi_ApDungRiengCongTyDo()
    {
        var nmi = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "NMI");

        var response = await nmi.PutAsJsonAsync("/api/document-numbering/grn",
            new UpdateDocumentNumberingRequest("nk/{yyyy}/{00000}"), Ct);
        var updated = await response.Content.ReadFromJsonAsync<DocumentNumberingDto>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("NK/{YYYY}/{00000}", updated!.Format);
        Assert.False(updated.IsDefault);
        Assert.Equal("NK/2094/00001", await NextAsync("NMI", "GRN", new DateOnly(2094, 5, 1)));
        Assert.Equal("GRN-BHA-9405-0001", await NextAsync("BHA", "GRN", new DateOnly(2094, 5, 1)));

        var list = await nmi.GetFromJsonAsync<List<DocumentNumberingDto>>("/api/document-numbering", Ct);
        Assert.Equal("NK/{YYYY}/{00000}", list!.Single(x => x.DocType == "GRN").Format);
        Assert.True(list!.Single(x => x.DocType == "PO").IsDefault);

        // Để trống = quay về mẫu mặc định.
        var reset = await (await nmi.PutAsJsonAsync("/api/document-numbering/GRN", new UpdateDocumentNumberingRequest(null), Ct))
            .Content.ReadFromJsonAsync<DocumentNumberingDto>(Ct);
        Assert.True(reset!.IsDefault);
        Assert.Equal("GRN-{CTY}-{YYMM}-{0000}", reset.Format);
    }

    [Fact]
    public async Task DanhSach_HienSoGanNhatVaSoKeTiep()
    {
        var admin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "DTV");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));
        var issued = await NextAsync("DTV", "TRF", today);

        var list = await admin.GetFromJsonAsync<List<DocumentNumberingDto>>("/api/document-numbering", Ct);

        var trf = list!.Single(x => x.DocType == "TRF");
        Assert.Equal("Phiếu chuyển kho nội bộ", trf.Name);
        Assert.Equal(issued, trf.LastNumber);
        Assert.Equal(issued[..^4] + (int.Parse(issued[^4..]) + 1).ToString("0000"), trf.NextNumber);
    }

    [Fact]
    public async Task MauKhongHopLe_Tra400TiengViet()
    {
        var admin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");

        var response = await admin.PutAsJsonAsync("/api/document-numbering/PO", new UpdateDocumentNumberingRequest("PO-{MM}-{0000}"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("năm", (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title);
    }

    [Fact]
    public async Task LoaiChungTuLa_Tra404()
    {
        var admin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var response = await admin.PutAsJsonAsync("/api/document-numbering/XYZ", new UpdateDocumentNumberingRequest("XYZ-{0000}"), Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task KhongCoQuyenThamSo_Tra403()
    {
        var thukho = await _factory.CreateClientInCompanyAsync("thukho.bha", "Demo@2026", "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await thukho.GetAsync("/api/document-numbering", Ct)).StatusCode);
    }
}
