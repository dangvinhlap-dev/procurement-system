using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Kỳ kho (design.md mục 7, 7.6): lịch năm liền nhau, mở kỳ tuần tự, kiểm tra kỳ mở khi ghi sổ.
/// Mỗi test tạo công ty mới để không đụng lịch kỳ mẫu của BHA, NMI.
/// </summary>
[Collection(ApiCollection.Name)]
public class InventoryPeriodTests
{
    private readonly ProcApiFactory _factory;

    public InventoryPeriodTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Công ty mới (chưa có lịch kỳ) và client của người tạo đang làm việc ở công ty đó.</summary>
    private async Task<(CompanyDetailDto Company, HttpClient Client)> NewCompanyAsync(DateOnly? startDate = null)
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("C"), "Công ty thử kỳ kho", null, null, null, null, startDate, "KT", "Kho tổng"), Ct));
        return (company, await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code));
    }

    private static async Task<InventoryCalendarDto> CreateYearAsync(HttpClient client, int year) =>
        await ReadAsync<InventoryCalendarDto>(await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(year), Ct));

    private static Task<HttpResponseMessage> OpenAsync(HttpClient client, InventoryCalendarDto calendar, int month) =>
        client.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == month).Id}/open", null, Ct);

    [Fact]
    public async Task DuLieuMau_ThangHienTaiDangMo_ChiThangSauMoDuoc()
    {
        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

        var calendar = await bha.GetFromJsonAsync<InventoryCalendarDto>("/api/inventory-periods", Ct);

        Assert.Equal(today.Year, calendar!.Year);
        Assert.Equal(13, calendar.Periods.Count);
        Assert.Equal(("Open", $"{today.Month:00}/{today.Year}"), (calendar.Periods.Single(p => p.Month == today.Month).Status, calendar.Periods.Single(p => p.Month == today.Month).Label));
        var canOpen = calendar.Periods.Where(p => p.CanOpen).Select(p => p.Month).ToList();
        Assert.Equal(today.Month < 12 ? [today.Month + 1] : [], canOpen);
    }

    [Fact]
    public async Task TaoLichNam_LienNhau_KhongTrung()
    {
        var (_, client) = await NewCompanyAsync();
        var empty = await client.GetFromJsonAsync<InventoryCalendarDto>("/api/inventory-periods", Ct);
        Assert.Empty(empty!.Years);

        var calendar = await CreateYearAsync(client, 2027);
        Assert.Equal([2027], calendar.Years);
        Assert.Equal((2027, 13, 2028), (calendar.Year, calendar.Periods.Count, calendar.NextYear));
        Assert.Equal(("Năm 2027", "Open"), (calendar.Periods[0].Label, calendar.Periods[0].Status));

        Assert.Equal("Đã có lịch kỳ kho năm 2027.", await TitleAsync(
            await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(2027), Ct)));
        Assert.Contains("năm tiếp theo cần tạo là 2028", await TitleAsync(
            await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(2029), Ct)));
        await CreateYearAsync(client, 2028);
    }

    [Fact]
    public async Task MoKy_TuanTuTheoThang_KhongMoNguocVaKhongBoTrong()
    {
        var (_, client) = await NewCompanyAsync();
        var calendar = await CreateYearAsync(client, 2027);

        // Kỳ đầu tiên tự chọn; sau đó phải liền nhau.
        Assert.Equal(HttpStatusCode.OK, (await OpenAsync(client, calendar, 3)).StatusCode);
        var skip = await OpenAsync(client, calendar, 5);
        Assert.Equal("Phải mở kỳ 04/2027 trước (mở kỳ tuần tự theo tháng).", await TitleAsync(skip));
        Assert.Contains("không mở ngược", await TitleAsync(await OpenAsync(client, calendar, 2)));
        Assert.Contains("không ở trạng thái Chưa mở", await TitleAsync(await OpenAsync(client, calendar, 3)));

        var april = await ReadAsync<InventoryPeriodDto>(await OpenAsync(client, calendar, 4));
        Assert.Equal("Open", april.Status);

        // Sang năm sau: tháng 1/2028 chỉ mở được khi đã mở tới tháng 12/2027.
        var next = await CreateYearAsync(client, 2028);
        Assert.Contains("Phải mở kỳ 05/2027", await TitleAsync(await OpenAsync(client, next, 1)));
    }

    [Fact]
    public async Task MoKy_KhongTruocNgayBatDauDungHeThong()
    {
        var (_, client) = await NewCompanyAsync(new DateOnly(2027, 6, 15));
        var calendar = await CreateYearAsync(client, 2027);

        Assert.Contains("15/06/2027", await TitleAsync(await OpenAsync(client, calendar, 5)));
        Assert.Equal(HttpStatusCode.OK, (await OpenAsync(client, calendar, 6)).StatusCode);
    }

    [Fact]
    public async Task KyMoKhiGhiSo_BaoLyDoTiengViet()
    {
        var (company, client) = await NewCompanyAsync();
        var calendar = await CreateYearAsync(client, 2027);
        await ReadAsync<InventoryPeriodDto>(await OpenAsync(client, calendar, 7));

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentUserScope>()
            .Initialize(Guid.Empty, company.Id, [], allWarehouses: true, [], allItemCategories: true, []);
        var periods = scope.ServiceProvider.GetRequiredService<IInventoryPeriodService>();

        Assert.Equal(calendar.Periods.Single(p => p.Month == 7).Id, await periods.RequireOpenPeriodAsync(new DateOnly(2027, 7, 31), Ct));
        var notOpened = await Assert.ThrowsAsync<BusinessRuleException>(() => periods.RequireOpenPeriodAsync(new DateOnly(2027, 8, 1), Ct));
        Assert.Equal("Kỳ kho 08/2027 (ngày 01/08/2027) chưa mở.", notOpened.Message);
        var noCalendar = await Assert.ThrowsAsync<BusinessRuleException>(() => periods.RequireOpenPeriodAsync(new DateOnly(2030, 1, 1), Ct));
        Assert.StartsWith("Chưa có lịch kỳ kho năm 2030", noCalendar.Message);
    }

    [Fact]
    public async Task ThuKho_KhongCoQuyenKyKho_Tra403()
    {
        var thukho = await _factory.CreateClientInCompanyAsync("thukho.bha", "Demo@2026", "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await thukho.GetAsync("/api/inventory-periods", Ct)).StatusCode);
    }
}
