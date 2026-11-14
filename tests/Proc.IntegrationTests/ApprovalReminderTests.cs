using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Notifications;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.IntegrationTests;

/// <summary>
/// Nhắc quá hạn duyệt (lát O2c, design.md mục 19.3): bước chờ quá 1 ngày làm việc thì người duyệt được bước nhận thông báo nhắc; nhắc lại cách
/// 1 ngày làm việc, tối đa số lần cấu hình; cấp sau tính từ lúc cấp trước duyệt; người đã duyệt cấp trước không nhận nhắc cấp sau. Dùng đồng hồ
/// giả và chỉ quét công ty của test.
/// </summary>
[Collection(ApiCollection.Name)]
public class ApprovalReminderTests
{
    private readonly ProcApiFactory _factory;

    public ApprovalReminderTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    /// <summary>Giờ Việt Nam tháng 11/2026 (09/11 là thứ 2).</summary>
    private static DateTimeOffset Vn(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(7));

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now.ToUniversalTime();
    }

    private sealed class FixedOptions(ApprovalReminderOptions value) : IOptionsMonitor<ApprovalReminderOptions>
    {
        public ApprovalReminderOptions CurrentValue => value;

        public ApprovalReminderOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<ApprovalReminderOptions, string?> listener) => null;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Quét nhắc một lần ở thời điểm cho trước, chỉ công ty của test, tối đa 2 lần nhắc mỗi bước.</summary>
    private async Task<ApprovalReminderResult> RemindAtAsync(Guid companyId, DateTimeOffset at)
    {
        using var scope = _factory.Services.CreateScope();
        var service = new ApprovalReminderService(scope.ServiceProvider.GetRequiredService<ProcDbContext>(),
            scope.ServiceProvider.GetRequiredService<ICurrentUserScope>(), new FixedClock(at),
            new FixedOptions(new ApprovalReminderOptions { MaxReminders = 2 }));
        return await service.RemindAsync(Ct, [companyId]);
    }

    /// <summary>Thông báo nhắc của một người cho phiếu.</summary>
    private async Task<List<Notification>> RemindersAsync(Guid userId, Guid documentId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ProcDbContext>().Notifications.IgnoreQueryFilters().AsNoTracking()
            .Where(n => n.UserId == userId && n.TargetId == documentId && n.Type == NotificationTypes.ApprovalOverdue)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(Ct);
    }

    [Fact]
    public async Task BuocChoQua1NgayLamViec_NhacNguoiDuyet_NhacLaiToiDa_CapSauTinhTuLucCapTruocDuyet()
    {
        // Công ty mới (danh mục nhóm hàng, mặt hàng lập ở tổng công ty), người lập là quản trị; hai người duyệt quản trị A, B; PR hai cấp ai có quyền duyệt cũng duyệt được (mỗi người một cấp).
        var buyerName = $"nh.{Guid.NewGuid():N}"[..16];
        var buyerId = await _factory.CreateUserAsync(buyerName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(buyerName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest($"N{Guid.NewGuid():N}".ToUpperInvariant()[..10], "Công ty thử nhắc duyệt", null, null, null, null,
                new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var buyer = await _factory.CreateClientInCompanyAsync(buyerName, ProcApiFactory.TestPassword, company.Code);
        var kt = (await buyer.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var cai = (await buyer.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm thử nhắc duyệt {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bulong", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct));
        var department = await ReadAsync<DepartmentDto>(await buyer.PostAsJsonAsync("/api/departments",
            new CreateDepartmentRequest("PX1", "Phân xưởng 1", null), Ct));
        Guid adminRole;
        using (var scope = _factory.Services.CreateScope())
            adminRole = await scope.ServiceProvider.GetRequiredService<ProcDbContext>().Roles
                .Where(r => r.Code == Proc.Domain.Organization.Role.AdminCode).Select(r => r.Id).SingleAsync(Ct);
        await ReadAsync<ApprovalRuleDto>(await buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 1, 0, adminRole), Ct));
        await ReadAsync<ApprovalRuleDto>(await buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 2, 0, adminRole), Ct));
        var aName = $"na.{Guid.NewGuid():N}"[..16];
        var a = await _factory.CreateUserAsync(aName, ProcApiFactory.TestPassword, [company.Code]);
        var aClient = await _factory.CreateClientInCompanyAsync(aName, ProcApiFactory.TestPassword, company.Code);
        var b = await _factory.CreateUserAsync($"nb.{Guid.NewGuid():N}"[..16], ProcApiFactory.TestPassword, [company.Code]);

        var pr = await ReadAsync<PurchaseRequestDto>(await buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, department.Id, "Bảo trì", Today.AddDays(7), kt, "Normal", null, [new(null, item.Id, cai, 2, null, null, null)]), Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        Assert.Equal("Submitted", pr.Status);

        // Đặt lại thời điểm gửi: thứ 2 09/11/2026 08:00 giờ Việt Nam.
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ProcDbContext>().ApprovalSteps.IgnoreQueryFilters()
                .Where(s => s.DocumentId == pr.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.SubmittedAt, Vn(9, 8).ToUniversalTime()), Ct);

        // Chưa đủ 1 ngày làm việc: không nhắc.
        Assert.Equal(0, (await RemindAtAsync(company.Id, Vn(10, 7, 59))).Steps);

        // Thứ 3 08:30: nhắc lần 1 cho cả A và B (cấp 1), người lập không nhận.
        Assert.Equal(1, (await RemindAtAsync(company.Id, Vn(10, 8, 30))).Steps);
        var first = Assert.Single(await RemindersAsync(a, pr.Id));
        Assert.Equal($"Nhắc: Đề nghị mua hàng {pr.No} chờ bạn duyệt quá hạn", first.Title);
        Assert.Contains("đang chờ duyệt cấp 1 từ 09/11/2026 08:00, đã quá 1 ngày làm việc (nhắc lần 1)", first.Message);
        Assert.Single(await RemindersAsync(b, pr.Id));
        Assert.Empty(await RemindersAsync(buyerId, pr.Id));

        // Quét lại cùng lúc, hoặc chưa đủ 1 ngày làm việc kể từ lần nhắc trước: không nhắc thêm.
        Assert.Equal(0, (await RemindAtAsync(company.Id, Vn(10, 8, 45))).Steps);
        Assert.Equal(0, (await RemindAtAsync(company.Id, Vn(10, 23))).Steps);
        // Thứ 4 08:30: nhắc lần 2.
        Assert.Equal(1, (await RemindAtAsync(company.Id, Vn(11, 8, 30))).Steps);
        Assert.Contains("(nhắc lần 2)", (await RemindersAsync(a, pr.Id))[^1].Message);
        // Đủ số lần tối đa (2) và ngày nghỉ: không nhắc nữa.
        Assert.Equal(0, (await RemindAtAsync(company.Id, Vn(12, 9))).Steps);
        Assert.Equal(0, (await RemindAtAsync(company.Id, Vn(14, 9))).Steps);

        // A duyệt cấp 1 (giờ thật): cấp 2 tính từ lúc A duyệt; A đã duyệt một cấp nên chỉ B được nhắc.
        pr = await ReadAsync<PurchaseRequestDto>(await aClient.PostAsJsonAsync($"/api/purchase-requests/{pr.Id}/approve", new ApprovalActionRequest(null), Ct));
        DateTimeOffset actedAt;
        using (var scope = _factory.Services.CreateScope())
            actedAt = (await scope.ServiceProvider.GetRequiredService<ProcDbContext>().ApprovalSteps.IgnoreQueryFilters()
                .SingleAsync(s => s.DocumentId == pr.Id && s.Level == 1, Ct)).ActedAt!.Value;
        var due = ApprovalOverdue.AddWorkingDay(actedAt, VietnamTime.Zone);
        Assert.Equal(0, (await RemindAtAsync(company.Id, due.AddMinutes(-1))).Steps);
        var result = await RemindAtAsync(company.Id, due);
        Assert.Equal((1, 1), (result.Steps, result.Recipients));
        Assert.Contains("đang chờ duyệt cấp 2", (await RemindersAsync(b, pr.Id))[^1].Message);
        Assert.Equal(2, (await RemindersAsync(a, pr.Id)).Count);
    }
}
