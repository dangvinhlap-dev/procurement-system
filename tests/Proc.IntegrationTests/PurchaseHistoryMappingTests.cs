using System.Net;
using System.Net.Http.Json;
using Proc.Application.Analysis;
using Proc.Application.Logging;
using Proc.Domain.Organization;
using static Proc.IntegrationTests.PurchaseHistoryTests;

namespace Proc.IntegrationTests;

/// <summary>
/// Ánh xạ dữ liệu mua lịch sử (design.md mục 18.2, lát E2): gom mã cũ/NCC cũ, gợi ý trùng gần đúng, lưu ánh xạ gán mọi dòng, lần nhập
/// sau tự áp, bỏ ánh xạ quay về tự khớp, nhật ký, phạm vi công ty, quyền.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchaseHistoryMappingTests
{
    private readonly ProcApiFactory _factory;

    public PurchaseHistoryMappingTests(ProcApiFactory factory) => _factory = factory;

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<List<PurchaseHistorySourceDto>> SourcesAsync(HttpClient client, string kind) =>
        (await client.GetFromJsonAsync<List<PurchaseHistorySourceDto>>($"/api/purchase-history/sources?kind={kind}", Ct))!;

    private static async Task<List<PurchaseHistoryCandidateDto>> CandidatesAsync(HttpClient client, string kind, string key, string? search = null) =>
        await ReadAsync<List<PurchaseHistoryCandidateDto>>(await client.GetAsync(
            $"/api/purchase-history/candidates?kind={kind}&key={Uri.EscapeDataString(key)}&search={Uri.EscapeDataString(search ?? "")}", Ct));

    private static Task<HttpResponseMessage> MapAsync(HttpClient client, string kind, string key, Guid targetId) =>
        client.PutAsJsonAsync("/api/purchase-history/mappings", new SavePurchaseHistoryMappingRequest(kind, key, targetId), Ct);

    [Fact]
    public async Task AnhXaMaVatTu_GanMoiDong_LanNhapSauTuApDung_BoAnhXaQuayVe()
    {
        var (head, bha) = await AdminAsync(_factory);
        var word = Unique("VAN");
        var item = await NewItemAsync(head, $"Van bi inox {word} DN25");
        var oldCode = Unique("CU-");
        await ImportAsync(bha, Workbook(Row(oldCode, itemName: $"Van bi Inox {word} DN25"), Row(oldCode.ToLowerInvariant(), itemName: $"van bi inox {word} DN25")), commit: true);

        var source = Assert.Single(await SourcesAsync(bha, PurchaseHistoryMappingKinds.Item), s => s.Key == oldCode);
        Assert.Equal((2, 0, false, (Guid?)null), (source.RecordCount, source.MatchedCount, source.HasMapping, source.TargetId));

        // Gợi ý theo tên không dấu, không phân biệt hoa thường: mặt hàng chung tên giống hệt đứng đầu.
        var suggestion = (await CandidatesAsync(bha, PurchaseHistoryMappingKinds.Item, oldCode))[0];
        Assert.Equal((item.Id, 100), (suggestion.Id, suggestion.Score));
        Assert.Contains(await CandidatesAsync(bha, PurchaseHistoryMappingKinds.Item, oldCode, word.ToLowerInvariant()), c => c.Id == item.Id);

        var saved = await ReadAsync<PurchaseHistoryMappingResult>(await MapAsync(bha, PurchaseHistoryMappingKinds.Item, oldCode, item.Id));

        Assert.Equal(2, saved.UpdatedRecords);
        Assert.All((await ListAsync(bha, $"search={oldCode}")).Rows, r => Assert.Equal(item.Id, r.ItemId));

        // Lần nhập sau: mã cũ tự gán theo ánh xạ đã lưu.
        var again = await ImportAsync(bha, Workbook(Row(oldCode)), commit: true);
        Assert.Equal(1, again.MatchedItems);
        source = Assert.Single(await SourcesAsync(bha, PurchaseHistoryMappingKinds.Item), s => s.Key == oldCode);
        Assert.Equal((3, 3, true, item.Id, item.Code), (source.RecordCount, source.MatchedCount, source.HasMapping, source.TargetId, source.TargetCode));

        var removed = await ReadAsync<PurchaseHistoryMappingResult>(
            await bha.DeleteAsync($"/api/purchase-history/mappings?kind=item&key={oldCode}", Ct));

        Assert.Equal(3, removed.UpdatedRecords);
        Assert.All((await ListAsync(bha, $"search={oldCode}")).Rows, r => Assert.Null(r.ItemId));
        var audit = (await bha.GetFromJsonAsync<LogPage<AuditLogItemDto>>(
            $"/api/audit-logs?from={DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)):yyyy-MM-dd}&to={DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)):yyyy-MM-dd}&entityType=PurchaseHistoryItemMapping", Ct))!.Items;
        Assert.Contains(audit, a => a.Action == "Create");
        Assert.Contains(audit, a => a.Action == "Delete");
    }

    [Fact]
    public async Task BoAnhXa_MaCuTrungMaChung_QuayVeTuKhop()
    {
        var (head, bha) = await AdminAsync(_factory);
        var direct = await NewItemAsync(head);
        var other = await NewItemAsync(head);
        await ImportAsync(bha, Workbook(Row(direct.Code)), commit: true);

        // Ghi đè kết quả tự khớp bằng ánh xạ (hai pháp nhân trùng mã nhưng khác hàng), rồi bỏ ánh xạ.
        await ReadAsync<PurchaseHistoryMappingResult>(await MapAsync(bha, PurchaseHistoryMappingKinds.Item, direct.Code, other.Id));
        Assert.Equal(other.Id, Assert.Single((await ListAsync(bha, $"search={direct.Code}")).Rows).ItemId);
        await ReadAsync<PurchaseHistoryMappingResult>(await bha.DeleteAsync($"/api/purchase-history/mappings?kind=item&key={direct.Code}", Ct));

        Assert.Equal(direct.Id, Assert.Single((await ListAsync(bha, $"search={direct.Code}")).Rows).ItemId);
    }

    [Fact]
    public async Task AnhXaNcc_KhongMst_GomTheoTenKhongDau()
    {
        var (head, bha) = await AdminAsync(_factory);
        var word = Unique("TV");
        var supplier = await NewSupplierAsync(head, $"Công ty TNHH Thép {word}");
        var code = Unique("NC-");
        await ImportAsync(bha, Workbook(
            Row(code, supplierName: $"Công ty Thép {word}"),
            Row(code, supplierName: $"CONG TY  THEP {word.ToLowerInvariant()}")), commit: true);
        var key = $"TEN:cong ty thep {word.ToLowerInvariant()}";

        var source = Assert.Single(await SourcesAsync(bha, PurchaseHistoryMappingKinds.Supplier), s => s.Key == key);
        Assert.Equal((2, (string?)null), (source.RecordCount, source.Code));
        // Bỏ "Công ty", "TNHH" khi so tên NCC.
        Assert.Equal((supplier.Id, 100), ((await CandidatesAsync(bha, PurchaseHistoryMappingKinds.Supplier, key))[0] is var c ? (c.Id, c.Score) : default));

        var saved = await ReadAsync<PurchaseHistoryMappingResult>(await MapAsync(bha, PurchaseHistoryMappingKinds.Supplier, key, supplier.Id));

        Assert.Equal(2, saved.UpdatedRecords);
        Assert.All((await ListAsync(bha, $"search={code}")).Rows, r => Assert.Equal(supplier.Id, r.SupplierId));
        var again = await ImportAsync(bha, Workbook(Row(code, supplierName: $"công ty thép {word}")), commit: true);
        Assert.Equal(1, again.MatchedSuppliers);
    }

    [Fact]
    public async Task PhamViCongTy_CongTyKhacKhongThayKhongAnhXaDuoc()
    {
        var (head, bha) = await AdminAsync(_factory);
        var item = await NewItemAsync(head);
        var code = Unique("PV-");
        await ImportAsync(bha, Workbook(Row(code)), commit: true);

        Assert.DoesNotContain(await SourcesAsync(head, PurchaseHistoryMappingKinds.Item), s => s.Key == code);
        Assert.Equal(HttpStatusCode.NotFound,
            (await head.GetAsync($"/api/purchase-history/candidates?kind=item&key={code}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await MapAsync(head, PurchaseHistoryMappingKinds.Item, code, item.Id)).StatusCode);
        Assert.Null(Assert.Single((await ListAsync(bha, $"search={code}")).Rows).ItemId);
    }

    [Fact]
    public async Task DichKhongCo_LoaiSai_BaoLoi()
    {
        var (_, bha) = await AdminAsync(_factory);
        var code = Unique("DK-");
        await ImportAsync(bha, Workbook(Row(code)), commit: true);

        Assert.Equal(HttpStatusCode.NotFound, (await MapAsync(bha, PurchaseHistoryMappingKinds.Item, code, Guid.NewGuid())).StatusCode);
        Assert.False((await bha.GetAsync("/api/purchase-history/sources?kind=abc", Ct)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bha.DeleteAsync($"/api/purchase-history/mappings?kind=item&key={code}", Ct)).StatusCode);
    }

    [Fact]
    public async Task ChiQuyenXem_XemDuoc_KhongAnhXaDuoc()
    {
        var roleCode = $"PMV{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(roleCode, Permissions.Analysis.PurchaseHistoryView);
        var userName = $"pm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "BHA", roleCode);
        var viewer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/purchase-history/sources?kind=item", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await MapAsync(viewer, PurchaseHistoryMappingKinds.Item, "X1", Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.DeleteAsync("/api/purchase-history/mappings?kind=item&key=X1", Ct)).StatusCode);
    }
}
