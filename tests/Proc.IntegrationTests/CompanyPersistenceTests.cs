using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Ràng buộc trong CSDL của bảng Company (design.md mục 5.1) và việc tự điền ngày tạo/sửa.
/// </summary>
[Collection(ApiCollection.Name)]
public class CompanyPersistenceTests
{
    private readonly ProcApiFactory _factory;

    public CompanyPersistenceTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChiCoMotTongCongTy()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        db.Companies.Add(Company.Create("TCT2", "Tổng công ty thứ hai", isHeadOffice: true));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task MaCongTyKhongDuocTrung()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        db.Companies.Add(Company.Create("BHA", "Trùng mã với công ty mẫu", isHeadOffice: false));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task LuuCongTy_TuDienNgayTaoVaNgaySua()
    {
        var code = $"T{Random.Shared.Next(1000, 9999)}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            db.Companies.Add(Company.Create(code, "Công ty thử ngày tạo", isHeadOffice: false));
            await db.SaveChangesAsync(Ct);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var company = await db.Companies.SingleAsync(c => c.Code == code, Ct);
            Assert.True(company.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
            Assert.Null(company.UpdatedAt);

            company.Rename("Công ty thử đã đổi tên", null);
            await db.SaveChangesAsync(Ct);
            Assert.NotNull(company.UpdatedAt);
        }
    }
}
