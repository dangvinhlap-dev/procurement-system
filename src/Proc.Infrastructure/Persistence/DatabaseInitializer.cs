using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Security;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// Tùy chọn khởi tạo CSDL, đọc từ mục "Database" của appsettings.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Tự chạy migration khi khởi động. Chỉ bật ở Development/Testing; server thật nâng cấp CSDL bằng script
    /// trong db/scripts sau khi đã sao lưu (CLAUDE.md mục 8).
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}

/// <summary>Một kho cần tạo sẵn.</summary>
public sealed class SeedWarehouse
{
    /// <summary>Mã kho.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên kho.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>"Main" hoặc "Sub"; kho Sub đặt dưới kho Main của cùng công ty.</summary>
    public string Type { get; set; } = "Main";
}

/// <summary>Một công ty cần tạo sẵn.</summary>
public sealed class SeedCompany
{
    /// <summary>Mã công ty.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên công ty.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Kho của công ty; kho Main khai báo trước kho Sub.</summary>
    public List<SeedWarehouse> Warehouses { get; set; } = [];
}

/// <summary>Một nhóm hàng cần tạo sẵn.</summary>
public sealed class SeedItemCategory
{
    /// <summary>Mã nhóm.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên nhóm.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mã nhóm cha (khai báo trước); trống là nhóm gốc.</summary>
    public string? Parent { get; set; }
}

/// <summary>Một vai trò mẫu.</summary>
public sealed class SeedRole
{
    /// <summary>Mã vai trò.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên vai trò.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mã quyền của vai trò.</summary>
    public List<string> Permissions { get; set; } = [];
}

/// <summary>Gán vai trò tại một công ty cho người dùng mẫu, kèm phạm vi kho/nhóm hàng (trống = tất cả).</summary>
public sealed class SeedAssignment
{
    /// <summary>Mã công ty.</summary>
    public string Company { get; set; } = string.Empty;

    /// <summary>Mã vai trò.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Mã kho được phép; trống = mọi kho của công ty.</summary>
    public List<string> Warehouses { get; set; } = [];

    /// <summary>Mã nhóm hàng được phép; trống = mọi nhóm.</summary>
    public List<string> ItemCategories { get; set; } = [];
}

/// <summary>Một người dùng mẫu.</summary>
public sealed class SeedUser
{
    /// <summary>Tên đăng nhập.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Họ tên.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Mật khẩu ban đầu.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Các vai trò theo công ty.</summary>
    public List<SeedAssignment> Assignments { get; set; } = [];
}

/// <summary>
/// Dữ liệu tạo sẵn để đăng nhập lần đầu, đọc từ mục "Seed" của appsettings. Mật khẩu trong appsettings.Development.json
/// chỉ dùng cho máy dev; server thật đặt qua biến môi trường Seed__AdminPassword rồi đổi ngay sau lần đăng nhập đầu.
/// Các mục Demo… chỉ khai báo ở môi trường dev/test.
/// </summary>
public sealed class SeedOptions
{
    /// <summary>Bật tạo dữ liệu khi khởi động.</summary>
    public bool Enabled { get; set; }

    /// <summary>Tên đăng nhập quản trị.</summary>
    public string AdminUserName { get; set; } = "admin";

    /// <summary>Họ tên quản trị.</summary>
    public string AdminFullName { get; set; } = "Quản trị hệ thống";

    /// <summary>Mật khẩu quản trị ban đầu.</summary>
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>Tổng công ty.</summary>
    public SeedCompany HeadOffice { get; set; } = new() { Code = "TCT", Name = "Tổng công ty" };

    /// <summary>Công ty thành viên mẫu.</summary>
    public List<SeedCompany> DemoCompanies { get; set; } = [];

    /// <summary>Nhóm hàng mẫu.</summary>
    public List<SeedItemCategory> DemoItemCategories { get; set; } = [];

    /// <summary>Vai trò mẫu.</summary>
    public List<SeedRole> DemoRoles { get; set; } = [];

    /// <summary>Người dùng mẫu.</summary>
    public List<SeedUser> DemoUsers { get; set; } = [];
}

/// <summary>
/// Chạy migration và tạo dữ liệu ban đầu khi API khởi động (tùy cấu hình).
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Chạy migration (nếu Database:MigrateOnStartup), tạo phân vùng nhật ký tháng tới, đồng bộ danh mục quyền, tạo dữ liệu mẫu (nếu Seed:Enabled).
    /// Gọi nhiều lần không sinh dữ liệu trùng.
    /// </summary>
    /// <param name="services">Service provider gốc của ứng dụng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi xong.</returns>
    /// <exception cref="InvalidOperationException">Khi không tạo được tài khoản (ví dụ mật khẩu không đạt quy tắc).</exception>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        if (sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
            await sp.GetRequiredService<ProcDbContext>().Database.MigrateAsync(cancellationToken);

        await EnsureAuditLogPartitionsAsync(sp.GetRequiredService<ProcDbContext>(), cancellationToken);
        await SeedAsync(sp, cancellationToken);
    }

    /// <summary>
    /// Đồng bộ bảng Permission theo code, tạo dữ liệu mẫu nếu Seed:Enabled, và bảo đảm vai trò ADMIN có đủ mọi quyền.
    /// </summary>
    /// <param name="sp">Service provider của một scope DI (dùng chung DbContext và phạm vi).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi xong.</returns>
    /// <exception cref="InvalidOperationException">Khi không tạo được tài khoản.</exception>
    public static async Task SeedAsync(IServiceProvider sp, CancellationToken cancellationToken)
    {
        var db = sp.GetRequiredService<ProcDbContext>();
        await SyncPermissionsAsync(db, cancellationToken);

        var seed = sp.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (seed.Enabled)
        {
            await new DataSeeder(
                db,
                sp.GetRequiredService<UserManager<AppUser>>(),
                sp.GetRequiredService<IClock>(),
                sp.GetRequiredService<CurrentUserScope>(),
                seed).RunAsync(cancellationToken);
        }

        await GrantAllPermissionsToAdminAsync(db, cancellationToken);
    }

    /// <summary>
    /// Tạo sẵn phân vùng AuditLog cho tháng này và 2 tháng tới (design.md mục 9.3). Khởi động lại API ít nhất mỗi tháng
    /// là đủ; nếu quên, dòng nhật ký rơi vào phân vùng mặc định chứ không lỗi. Sau này chuyển thành job Hangfire hằng tháng.
    /// </summary>
    private static async Task EnsureAuditLogPartitionsAsync(ProcDbContext db, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN IF to_regproc('audit_log_ensure_partitions') IS NOT NULL THEN PERFORM audit_log_ensure_partitions(2); END IF; END $$;",
            cancellationToken);

    /// <summary>
    /// Bảng Permission luôn khớp <see cref="Permissions.All"/>: thêm quyền mới, cập nhật tên, xóa quyền đã bỏ
    /// (RolePermission của quyền bị xóa xóa theo nhờ cascade).
    /// </summary>
    private static async Task SyncPermissionsAsync(ProcDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Code, cancellationToken);
        foreach (var definition in Permissions.All)
        {
            if (existing.Remove(definition.Code, out var permission)) permission.UpdateFrom(definition);
            else db.Permissions.Add(Permission.From(definition));
        }

        db.Permissions.RemoveRange(existing.Values);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Vai trò Quản trị hệ thống luôn có đủ mọi quyền, kể cả quyền mới thêm ở bản sau.</summary>
    private static async Task GrantAllPermissionsToAdminAsync(ProcDbContext db, CancellationToken cancellationToken)
    {
        var admin = await db.Roles.FirstOrDefaultAsync(r => r.Code == Role.AdminCode, cancellationToken);
        if (admin is null) return;
        var granted = await db.RolePermissions
            .Where(rp => rp.RoleId == admin.Id)
            .Select(rp => rp.PermissionCode)
            .ToListAsync(cancellationToken);
        foreach (var code in Permissions.All.Select(p => p.Code).Except(granted))
            db.RolePermissions.Add(RolePermission.Grant(admin.Id, code));
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Tạo dữ liệu mẫu từ SeedOptions: công ty và kho, nhóm hàng, vai trò, tài khoản admin và người dùng mẫu.
/// Chỉ thêm phần còn thiếu nên chạy lại được nhiều lần.
/// </summary>
internal sealed class DataSeeder
{
    private readonly ProcDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IClock _clock;
    private readonly CurrentUserScope _scope;
    private readonly SeedOptions _seed;

    public DataSeeder(ProcDbContext db, UserManager<AppUser> users, IClock clock, CurrentUserScope scope, SeedOptions seed)
    {
        _db = db;
        _users = users;
        _clock = clock;
        _scope = scope;
        _seed = seed;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var companies = new List<Company>();
        var warehouses = new Dictionary<(string Company, string Code), Warehouse>();
        foreach (var (item, isHeadOffice) in new[] { (_seed.HeadOffice, true) }.Concat(_seed.DemoCompanies.Select(c => (c, false))))
        {
            var company = await EnsureCompanyAsync(item, isHeadOffice, ct);
            companies.Add(company);
            foreach (var w in await EnsureWarehousesAsync(company, item.Warehouses, ct))
                warehouses[(company.Code, w.Code)] = w;
        }

        var categories = await EnsureItemCategoriesAsync(ct);

        var roles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase)
        {
            [Role.AdminCode] = await EnsureRoleAsync(Role.AdminCode, "Quản trị hệ thống", [], ct),
        };
        foreach (var r in _seed.DemoRoles)
            roles[r.Code] = await EnsureRoleAsync(r.Code, r.Name, r.Permissions, ct);

        var admin = await EnsureUserAsync(_seed.AdminUserName, _seed.AdminFullName, _seed.AdminPassword, companies[0].Id, ct);
        foreach (var company in companies)
            await EnsureAssignmentAsync(admin.Id, company, roles[Role.AdminCode], [], [], ct);

        foreach (var u in _seed.DemoUsers)
        {
            var first = companies.First(c => c.Code == u.Assignments[0].Company);
            var user = await EnsureUserAsync(u.UserName, u.FullName, u.Password, first.Id, ct);
            foreach (var a in u.Assignments)
            {
                var company = companies.First(c => c.Code == a.Company);
                await EnsureAssignmentAsync(
                    user.Id,
                    company,
                    roles[a.Role],
                    a.Warehouses.Select(code => warehouses[(company.Code, code.ToUpperInvariant())]).ToList(),
                    a.ItemCategories.Select(code => categories[code.ToUpperInvariant()]).ToList(),
                    ct);
            }
        }
    }

    /// <summary>
    /// Đặt phạm vi "toàn quyền dữ liệu" của đúng một công ty trước khi đọc/ghi kho của công ty đó,
    /// để đi qua global query filter như mọi truy vấn khác thay vì tắt filter.
    /// </summary>
    private void WorkIn(Guid? companyId) =>
        _scope.Initialize(Guid.Empty, companyId, [], allWarehouses: true, [], allItemCategories: true, []);

    private async Task<Company> EnsureCompanyAsync(SeedCompany item, bool isHeadOffice, CancellationToken ct)
    {
        var code = item.Code.Trim().ToUpperInvariant();
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Code == code, ct);
        if (company is null)
        {
            company = Company.Create(code, item.Name, isHeadOffice);
            _db.Companies.Add(company);
            await _db.SaveChangesAsync(ct);
        }

        return company;
    }

    private async Task<List<Warehouse>> EnsureWarehousesAsync(Company company, List<SeedWarehouse> items, CancellationToken ct)
    {
        WorkIn(company.Id);
        var existing = await _db.Warehouses.ToListAsync(ct);
        foreach (var item in items)
        {
            var code = item.Code.Trim().ToUpperInvariant();
            if (existing.Any(w => w.Code == code)) continue;
            var warehouse = item.Type.Equals("Sub", StringComparison.OrdinalIgnoreCase)
                ? Warehouse.CreateSub(existing.First(w => w.Type == WarehouseType.Main), code, item.Name)
                : Warehouse.CreateMain(company.Id, code, item.Name);
            _db.Warehouses.Add(warehouse);
            existing.Add(warehouse);
        }

        await _db.SaveChangesAsync(ct);
        WorkIn(null);
        return existing;
    }

    private async Task<Dictionary<string, ItemCategory>> EnsureItemCategoriesAsync(CancellationToken ct)
    {
        // Nhóm hàng dùng chung toàn tập đoàn (không có CompanyId) nên chỉ cần mở phạm vi nhóm hàng.
        _scope.Initialize(Guid.Empty, null, [], false, [], allItemCategories: true, []);
        var existing = await _db.ItemCategories.ToDictionaryAsync(c => c.Code, ct);
        foreach (var item in _seed.DemoItemCategories)
        {
            var code = item.Code.Trim().ToUpperInvariant();
            if (existing.ContainsKey(code)) continue;
            var parent = item.Parent is null ? null : existing[item.Parent.Trim().ToUpperInvariant()];
            var category = ItemCategory.Create(code, item.Name, parent);
            _db.ItemCategories.Add(category);
            existing[code] = category;
        }

        await _db.SaveChangesAsync(ct);
        WorkIn(null);
        return existing;
    }

    private async Task<Role> EnsureRoleAsync(string code, string name, List<string> permissions, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == normalized, ct);
        if (role is null)
        {
            role = Role.Create(normalized, name);
            _db.Roles.Add(role);
            await _db.SaveChangesAsync(ct);
        }

        var granted = await _db.RolePermissions.Where(rp => rp.RoleId == role.Id).Select(rp => rp.PermissionCode).ToListAsync(ct);
        foreach (var permission in permissions.Except(granted))
        {
            if (Permissions.All.All(p => p.Code != permission))
                throw new InvalidOperationException($"Seed: quyền '{permission}' của vai trò {normalized} không có trong Permissions.All.");
            _db.RolePermissions.Add(RolePermission.Grant(role.Id, permission));
        }

        await _db.SaveChangesAsync(ct);
        return role;
    }

    private async Task<AppUser> EnsureUserAsync(string userName, string fullName, string password, Guid defaultCompanyId, CancellationToken ct)
    {
        var user = await _users.FindByNameAsync(userName);
        if (user is not null) return user;

        user = new AppUser { UserName = userName, FullName = fullName, DefaultCompanyId = defaultCompanyId, CreatedAt = _clock.UtcNow };
        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Không tạo được tài khoản {userName}: " + string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        return user;
    }

    private async Task EnsureAssignmentAsync(
        Guid userId,
        Company company,
        Role role,
        List<Warehouse> warehouses,
        List<ItemCategory> categories,
        CancellationToken ct)
    {
        if (!await _db.UserCompanyRoles.AnyAsync(x => x.UserId == userId && x.CompanyId == company.Id && x.RoleId == role.Id, ct))
            _db.UserCompanyRoles.Add(UserCompanyRole.Assign(userId, company.Id, role.Id));

        foreach (var w in warehouses)
        {
            if (!await _db.UserWarehouseScopes.AnyAsync(x => x.UserId == userId && x.WarehouseId == w.Id, ct))
                _db.UserWarehouseScopes.Add(UserWarehouseScope.Allow(userId, w));
        }

        foreach (var c in categories)
        {
            if (!await _db.UserItemCategoryScopes.AnyAsync(
                    x => x.UserId == userId && x.CompanyId == company.Id && x.ItemCategoryId == c.Id, ct))
                _db.UserItemCategoryScopes.Add(UserItemCategoryScope.Allow(userId, company.Id, c.Id));
        }

        await _db.SaveChangesAsync(ct);
    }
}
