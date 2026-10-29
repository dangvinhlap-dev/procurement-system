using Microsoft.EntityFrameworkCore;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của hệ thống, dùng chung cho mọi module (modular monolith, một CSDL – design.md mục 3).
/// Hiện chưa có entity; các module sẽ thêm DbSet và cấu hình ở các lát sau.
/// </summary>
public sealed class ProcDbContext : DbContext
{
    /// <summary>
    /// Khởi tạo DbContext với cấu hình do DI cung cấp.
    /// </summary>
    /// <param name="options">Tùy chọn kết nối (Npgsql, chuỗi kết nối, interceptor).</param>
    public ProcDbContext(DbContextOptions<ProcDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Áp dụng cấu hình entity từ assembly Infrastructure (các lớp IEntityTypeConfiguration).
    /// </summary>
    /// <param name="modelBuilder">Bộ dựng mô hình của EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcDbContext).Assembly);
    }
}
