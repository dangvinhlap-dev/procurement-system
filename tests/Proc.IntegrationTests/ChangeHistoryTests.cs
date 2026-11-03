using System.Net.Http.Json;
using Proc.Application.Logging;
using Proc.Application.MasterData;
using Proc.Application.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lịch sử thay đổi theo ô (F1) trên danh mục – design.md mục 9.3a: dòng con kiểu "owned" (người liên hệ NCC) tra được theo khóa riêng,
/// lịch sử cả NCC gồm cả người liên hệ.
/// </summary>
[Collection(ApiCollection.Name)]
public class ChangeHistoryTests
{
    private readonly ProcApiFactory _factory;

    public ChangeHistoryTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NguoiLienHeNcc_XemLichSuTheoO_VaTrongLichSuNcc()
    {
        var admin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var groupId = (await admin.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var response = await admin.PostAsJsonAsync("/api/suppliers", new SaveSupplierRequest(
            null, groupId, "NCC lịch sử người liên hệ", null, false, null, null, null, null, null, null, null, null, "Potential", null, null,
            [new SupplierContactDto(null, "Trần Liên Hệ", "Kế toán", null, null, true)], []), Ct);
        response.EnsureSuccessStatusCode();
        var supplier = (await response.Content.ReadFromJsonAsync<SupplierDetailDto>(Ct))!;
        var contactId = supplier.Contacts.Single().Id;

        var name = await admin.GetFromJsonAsync<FieldHistoryDto>(
            $"/api/change-history/field?entityType=SupplierContact&entityId={contactId}&field=name", Ct);
        Assert.Equal("SupplierContact", name!.Info.EntityType);
        Assert.Equal(("Create", "Trần Liên Hệ"), (name.Changes.Single().Action, name.Changes.Single().NewValue));

        var history = await admin.GetFromJsonAsync<RecordHistoryDto>(
            $"/api/change-history/record?entityType=Supplier&entityId={supplier.Id}", Ct);
        Assert.Contains(history!.Changes, c => c.EntityType == "SupplierContact" && c.Field == "Position" && c.NewValue == "Kế toán");
    }

    /// <summary>Tham số hệ thống lưu ở dòng tham số công ty: danh sách trả khóa dòng và cột để F1 xem lịch sử giá trị.</summary>
    [Fact]
    public async Task ThamSoHeThong_TraKhoaDong_XemLichSuGiaTri()
    {
        // Người quản trị riêng (không dùng admin – AuthTests đếm số công ty của admin).
        var userName = $"ls.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var code = $"H{Guid.NewGuid():N}".ToUpperInvariant()[..8];
        var created = await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(code, "Công ty thử lịch sử tham số", null, null, null, null, new DateOnly(2026, 1, 1), "KT", "Kho tổng"), Ct);
        created.EnsureSuccessStatusCode();
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, code);

        (await client.PutAsJsonAsync($"/api/system-parameters/{SystemParameterCodes.RfqMinSuppliers}", new UpdateSystemParameterRequest("4"), Ct))
            .EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/system-parameters/{SystemParameterCodes.RfqMinSuppliers}", new UpdateSystemParameterRequest("5"), Ct))
            .EnsureSuccessStatusCode();
        var rfq = (await client.GetFromJsonAsync<List<SystemParameterDto>>("/api/system-parameters", Ct))!
            .Single(p => p.Code == SystemParameterCodes.RfqMinSuppliers);
        Assert.Equal("RfqMinSuppliers", rfq.AuditField);

        var history = await client.GetFromJsonAsync<FieldHistoryDto>(
            $"/api/change-history/field?entityType=CompanyParameter&entityId={rfq.AuditId}&field={rfq.AuditField}", Ct);
        Assert.Equal(("4", "5"), (history!.Changes[0].OldValue, history.Changes[0].NewValue));
    }
}
