using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của hệ thống, dùng chung cho mọi module (modular monolith, một CSDL – design.md mục 3).
/// Kế thừa IdentityUserContext để chứa bảng tài khoản của ASP.NET Core Identity (không gồm bảng vai trò của Identity,
/// vì vai trò gán theo công ty ở bảng UserCompanyRole – design.md mục 9.1).
/// </summary>
public sealed class ProcDbContext : IdentityUserContext<AppUser, Guid>
{
    /// <summary>
    /// Khởi tạo DbContext với cấu hình do DI cung cấp.
    /// </summary>
    /// <param name="options">Tùy chọn kết nối (Npgsql, chuỗi kết nối, interceptor).</param>
    public ProcDbContext(DbContextOptions<ProcDbContext> options)
        : base(options)
    {
    }

    /// <summary>Công ty.</summary>
    public DbSet<Company> Companies => Set<Company>();

    /// <summary>Vai trò.</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>Gán vai trò cho người dùng theo công ty.</summary>
    public DbSet<UserCompanyRole> UserCompanyRoles => Set<UserCompanyRole>();

    /// <summary>
    /// Cấu hình bảng Identity rồi áp dụng các lớp IEntityTypeConfiguration trong assembly Infrastructure.
    /// </summary>
    /// <param name="modelBuilder">Bộ dựng mô hình của EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Đặt lại tên bảng Identity cho cùng kiểu với các bảng khác (số ít, không tiền tố AspNet).
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaim");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogin");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserToken");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcDbContext).Assembly);
    }
}
