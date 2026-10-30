using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng Company (design.md mục 5.1).</summary>
internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Company");
        builder.Property(x => x.Code).HasMaxLength(Company.CodeMaxLength).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ShortName).HasMaxLength(100);
        builder.Property(x => x.TaxCode).HasMaxLength(20);
        // Mỗi công ty là một pháp nhân có mã số thuế riêng (design.md mục 5.6); công ty chưa khai báo MST thì bỏ qua.
        builder.HasIndex(x => x.TaxCode).IsUnique().HasFilter("\"TaxCode\" IS NOT NULL");
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Representative).HasMaxLength(200);
        builder.Property(x => x.AccountingCurrency).HasMaxLength(3).IsFixedLength().IsRequired();

        // Chỉ có đúng một tổng công ty (design.md mục 5.1): chỉ mục duy nhất trên các dòng IsHeadOffice = true.
        builder.HasIndex(x => x.IsHeadOffice).IsUnique().HasFilter("\"IsHeadOffice\"");
    }
}

/// <summary>Cấu hình bảng Role (design.md mục 9.1).</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}

/// <summary>Cấu hình bảng UserCompanyRole (design.md mục 9.2).</summary>
internal sealed class UserCompanyRoleConfiguration : IEntityTypeConfiguration<UserCompanyRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserCompanyRole> builder)
    {
        builder.ToTable("UserCompanyRole");
        builder.HasIndex(x => new { x.UserId, x.CompanyId, x.RoleId }).IsUnique();

        // Restrict: không xóa dây chuyền – công ty, vai trò, tài khoản đều chỉ ngừng dùng, không xóa cứng.
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng User (tài khoản Identity).</summary>
internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("User");
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.DefaultCompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
