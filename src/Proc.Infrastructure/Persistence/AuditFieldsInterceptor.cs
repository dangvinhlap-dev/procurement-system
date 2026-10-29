using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Proc.Application.Common;
using Proc.Domain.Common;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// Tự điền CreatedAt/By, UpdatedAt/By cho mọi AuditableEntity khi lưu, để code nghiệp vụ không phải nhớ gán.
/// Nhật ký thay đổi chi tiết (AuditLog, giá trị cũ/mới) làm ở lát ghi log – design.md mục 9.3.
/// </summary>
public sealed class AuditFieldsInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo interceptor.
    /// </summary>
    /// <param name="currentUser">Người dùng đang thao tác.</param>
    /// <param name="clock">Đồng hồ hệ thống.</param>
    public AuditFieldsInterceptor(ICurrentUser currentUser, IClock clock)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;
        var now = _clock.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = _currentUser.UserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = _currentUser.UserId;
            }
        }
    }
}
