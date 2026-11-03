using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proc.Application.Documents;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Domain.Documents;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Persistence;
using Proc.Application.Common;

namespace Proc.IntegrationTests;

/// <summary>
/// File đính kèm dùng chung (design.md mục 18.4): tải lên/xuống, giới hạn dung lượng (mặc định 2 MB) và đuôi file, quyền theo đối tượng chủ, phạm vi công ty
/// và kho, xóa mềm giữ file, nhật ký.
/// </summary>
[Collection(ApiCollection.Name)]
public class AttachmentTests
{
    private readonly ProcApiFactory _factory;

    public AttachmentTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client, string ownerType, Guid ownerId, string fileName, byte[] content, string? note = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(ownerType), "ownerType" },
            { new StringContent(ownerId.ToString()), "ownerId" },
        };
        if (note is not null) form.Add(new StringContent(note), "note");
        var file = new ByteArrayContent(content);
        // Client gửi loại nội dung sai cố ý: máy chủ phải lấy theo đuôi file.
        file.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        form.Add(file, "file", fileName);
        return client.PostAsync("/api/attachments", form, Ct);
    }

    private static Task<HttpResponseMessage> ListAsync(HttpClient client, string ownerType, Guid ownerId) =>
        client.GetAsync($"/api/attachments?ownerType={ownerType}&ownerId={ownerId}", Ct);

    /// <summary>Quản trị ở TCT và BHA, NCC đã duyệt áp dụng tại BHA, một mặt hàng; trả client hai công ty.</summary>
    private sealed record Fixture(HttpClient Head, HttpClient Bha, SupplierDetailDto Supplier, ItemDetailDto Item, Guid Hop);

    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"dk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.ToDictionary(u => u.Code, u => u.Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Nhóm đính kèm", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Vật tư đính kèm", null, "Inventory", category.Id, uoms["HOP"], null, null, null,
                null, "10", null, false, false, null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC đính kèm", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<SupplierCompanyDto>(await bha.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        return new Fixture(head, bha, supplier, item, uoms["HOP"]);
    }

    /// <summary>Phiếu nhập nháp ở kho <paramref name="warehouseCode"/> của BHA.</summary>
    private static async Task<InventoryDocumentDto> ReceiptAsync(Fixture f, string warehouseCode)
    {
        var warehouseId = (await f.Bha.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single(w => w.Code == warehouseCode).Id;
        return await ReadAsync<InventoryDocumentDto>(await f.Bha.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, warehouseId, f.Supplier.Id, null, null, null, null, null, null,
                [new(null, f.Item.Id, f.Hop, 1, 10_000, "10", null, null, null)]), Ct));
    }

    private string FullPath(string storagePath) =>
        Path.Combine(_factory.Services.GetRequiredService<IOptions<AttachmentOptions>>().Value.RootPath, storagePath);

    [Fact]
    public async Task TaiLenTaiXuongXoaMem_GiuFileVaGhiNhatKy()
    {
        var f = await ArrangeAsync();
        var receipt = await ReceiptAsync(f, "KT");
        var content = "%PDF-1.4 hóa đơn"u8.ToArray();

        var uploaded = await ReadAsync<AttachmentDto>(
            await UploadAsync(f.Bha, AttachmentOwnerTypes.InventoryDocument, receipt.Id, "Hóa đơn 0001.pdf", content, "Hóa đơn GTGT"));

        Assert.Equal(("Hóa đơn 0001.pdf", "application/pdf", content.LongLength, "Hóa đơn GTGT"),
            (uploaded.FileName, uploaded.ContentType, uploaded.SizeBytes, uploaded.Note));
        Assert.NotNull(uploaded.UploadedByName);
        var list = await ReadAsync<List<AttachmentDto>>(await ListAsync(f.Bha, AttachmentOwnerTypes.InventoryDocument, receipt.Id));
        Assert.Equal([uploaded.Id], list.Select(a => a.Id));

        var download = await f.Bha.GetAsync($"/api/attachments/{uploaded.Id}/content", Ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(content, await download.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var row = await db.Attachments.AsNoTracking().SingleAsync(a => a.Id == uploaded.Id, Ct);
        Assert.Equal(await _factory.GetCompanyIdAsync("BHA"), row.CompanyId);
        Assert.Equal(64, row.Sha256.Length);
        Assert.True(File.Exists(FullPath(row.StoragePath)));

        Assert.Equal(HttpStatusCode.NoContent, (await f.Bha.DeleteAsync($"/api/attachments/{uploaded.Id}", Ct)).StatusCode);
        Assert.Empty(await ReadAsync<List<AttachmentDto>>(await ListAsync(f.Bha, AttachmentOwnerTypes.InventoryDocument, receipt.Id)));
        Assert.Equal(HttpStatusCode.NotFound, (await f.Bha.GetAsync($"/api/attachments/{uploaded.Id}/content", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Bha.DeleteAsync($"/api/attachments/{uploaded.Id}", Ct)).StatusCode);
        // Xóa mềm: file vẫn trên đĩa để đối chiếu/khôi phục.
        Assert.True(File.Exists(FullPath(row.StoragePath)));
        var actions = await db.AuditLogs.AsNoTracking()
            .Where(l => l.EntityType == nameof(Attachment) && l.EntityId == uploaded.Id.ToString())
            .OrderBy(l => l.OccurredAt).Select(l => l.Action).ToListAsync(Ct);
        Assert.Equal(2, actions.Count);
    }

    [Fact]
    public async Task DuoiBiChan_Va_Qua2MB_BaoLoiKhongGhiFile()
    {
        var f = await ArrangeAsync();

        var exe = await UploadAsync(f.Head, AttachmentOwnerTypes.Supplier, f.Supplier.Id, "setup.exe", [1, 2, 3]);
        Assert.Equal(HttpStatusCode.BadRequest, exe.StatusCode);
        Assert.Contains(".exe", await TitleAsync(exe));

        var big = await UploadAsync(f.Head, AttachmentOwnerTypes.Supplier, f.Supplier.Id, "lon.zip", new byte[Attachment.MegabytesToBytes(Attachment.DefaultMaxFileSizeMb) + 1]);
        Assert.Equal(HttpStatusCode.BadRequest, big.StatusCode);
        Assert.Contains("2 MB", await TitleAsync(big));

        // Giao diện đọc giới hạn từ máy chủ để kiểm tra trước khi gửi.
        var limits = await ReadAsync<AttachmentLimitsDto>(await f.Head.GetAsync("/api/attachments/limits", Ct));
        Assert.Equal(2, limits.MaxFileSizeMb);

        Assert.Empty(await ReadAsync<List<AttachmentDto>>(await ListAsync(f.Head, AttachmentOwnerTypes.Supplier, f.Supplier.Id)));
        var unknown = await ListAsync(f.Head, "Khac", f.Supplier.Id);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task NccDungChung_CongTyKhacXemDuoc_ThieuQuyenSuaThi403()
    {
        var f = await ArrangeAsync();
        var uploaded = await ReadAsync<AttachmentDto>(
            await UploadAsync(f.Head, AttachmentOwnerTypes.Supplier, f.Supplier.Id, "giay-phep.png", [137, 80, 78, 71]));

        // NVMUA ở NMI: có quyền xem NCC, không có quyền sửa NCC.
        var buyer = $"dk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(buyer, "NMI", "NVMUA");
        var nmi = await _factory.CreateClientInCompanyAsync(buyer, ProcApiFactory.TestPassword, "NMI");

        Assert.Equal([uploaded.Id], (await ReadAsync<List<AttachmentDto>>(await ListAsync(nmi, AttachmentOwnerTypes.Supplier, f.Supplier.Id))).Select(a => a.Id));
        Assert.Equal(HttpStatusCode.OK, (await nmi.GetAsync($"/api/attachments/{uploaded.Id}/content", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(nmi, AttachmentOwnerTypes.Supplier, f.Supplier.Id, "b.pdf", [1])).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nmi.DeleteAsync($"/api/attachments/{uploaded.Id}", Ct)).StatusCode);
        // Không có quyền xem phiếu kho: 403 trước khi biết phiếu có tồn tại hay không.
        Assert.Equal(HttpStatusCode.Forbidden, (await ListAsync(nmi, AttachmentOwnerTypes.InventoryDocument, Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task PhieuCongTyKhac_Va_KhoNgoaiPhamVi_NhuKhongTonTai()
    {
        var f = await ArrangeAsync();
        var atKp2 = await ReceiptAsync(f, "KP2");
        var uploaded = await ReadAsync<AttachmentDto>(
            await UploadAsync(f.Bha, AttachmentOwnerTypes.InventoryDocument, atKp2.Id, "bien-ban.docx", [80, 75]));

        // Quản trị NMI (cùng quyền) nhưng phiếu thuộc BHA.
        var nmiAdmin = $"dk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(nmiAdmin, ProcApiFactory.TestPassword, ["NMI"]);
        var nmi = await _factory.CreateClientInCompanyAsync(nmiAdmin, ProcApiFactory.TestPassword, "NMI");
        Assert.Equal(HttpStatusCode.NotFound, (await ListAsync(nmi, AttachmentOwnerTypes.InventoryDocument, atKp2.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await nmi.GetAsync($"/api/attachments/{uploaded.Id}/content", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(nmi, AttachmentOwnerTypes.InventoryDocument, atKp2.Id, "x.pdf", [1])).StatusCode);

        // Thủ kho BHA chỉ được kho KP1: phiếu ở KP2 ngoài phạm vi.
        var keeper = $"dk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(keeper, "BHA", "THUKHO", warehouseCodes: ["KP1"]);
        var bhaKeeper = await _factory.CreateClientInCompanyAsync(keeper, ProcApiFactory.TestPassword, "BHA");
        Assert.Equal(HttpStatusCode.NotFound, (await ListAsync(bhaKeeper, AttachmentOwnerTypes.InventoryDocument, atKp2.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bhaKeeper.DeleteAsync($"/api/attachments/{uploaded.Id}", Ct)).StatusCode);
    }
    [Fact]
    public async Task LoaiChuMoi_DuocNhan_KhongCoChuThi404()
    {
        // Lát A4 (mọi form có đính kèm): loại chủ mới hợp lệ – id không tồn tại trả 404, không phải 400 "loại không hợp lệ".
        var f = await ArrangeAsync();
        string[] types = [AttachmentOwnerTypes.SourcingRequest, AttachmentOwnerTypes.ContractAmendment, AttachmentOwnerTypes.SupplierReview,
            AttachmentOwnerTypes.ControlFinding, AttachmentOwnerTypes.ConflictDeclaration];
        foreach (var type in types)
            Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(f.Bha, type, Guid.NewGuid(), "a.pdf", [1])).StatusCode);
    }

    [Fact]
    public async Task KeKhaiXungDot_NguoiKeKhaiDinhKem_NguoiKhacKhongQuyenThi403()
    {
        var f = await ArrangeAsync();
        var staffName = $"dk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(staffName, "NMI", "NVMUA");
        var staff = await _factory.CreateClientInCompanyAsync(staffName, ProcApiFactory.TestPassword, "NMI");
        var declaration = await ReadAsync<ConflictDeclarationDto>(await staff.PostAsJsonAsync("/api/conflict-declarations",
            new CreateConflictDeclarationRequest(null, f.Supplier.Id, "Family", "Em trai là giám đốc kinh doanh", null), Ct));

        // Người kê khai tự đính kèm văn bản kê khai có chữ ký, không cần mã quyền.
        var uploaded = await ReadAsync<AttachmentDto>(
            await UploadAsync(staff, AttachmentOwnerTypes.ConflictDeclaration, declaration.Id, "ke-khai.pdf", [37, 80, 68, 70]));
        Assert.Equal("ke-khai.pdf", uploaded.FileName);

        // Nhân viên khác không có quyền xem kê khai của người khác.
        var otherName = $"dk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(otherName, "NMI", "NVMUA");
        var other = await _factory.CreateClientInCompanyAsync(otherName, ProcApiFactory.TestPassword, "NMI");
        Assert.Equal(HttpStatusCode.Forbidden, (await ListAsync(other, AttachmentOwnerTypes.ConflictDeclaration, declaration.Id)).StatusCode);

        // Quản trị (có quyền xem toàn bộ) xem được.
        Assert.Single(await ReadAsync<List<AttachmentDto>>(await ListAsync(f.Head, AttachmentOwnerTypes.ConflictDeclaration, declaration.Id)));
    }
}
