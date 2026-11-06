using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Proc.Application.Common;
using Proc.Domain.Common;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// Chống ghi đè khi sửa đồng thời (lát R2, design.md mục 18.26). Concurrency token xmin (<see cref="IVersioned"/>) đã chặn hai lệnh lưu
/// cùng lúc; interceptor này chặn thêm trường hợp người dùng thao tác trên bản cũ: request mang phiên bản đã xem (If-Match) mà entity đọc
/// lên trong request có phiên bản khác thì báo xung đột, không lưu.
/// </summary>
/// <remarks>
/// Chỉ so ở lần lưu đầu tiên có entity đó: các lần lưu sau trong cùng request thấy phiên bản đã đổi do chính request (ví dụ đóng kỳ lưu
/// trạng thái Đang tính giá rồi mới lưu Đã đóng). Entity không được đọc có theo dõi trong request thì không so (lệnh không sửa nó).
/// </remarks>
public sealed class VersionCheckInterceptor : SaveChangesInterceptor
{
    private readonly IExpectedVersion _expected;

    /// <summary>Tạo interceptor.</summary>
    /// <param name="expected">Phiên bản mong đợi của request.</param>
    public VersionCheckInterceptor(IExpectedVersion expected) => _expected = expected;

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>So phiên bản đọc lên với phiên bản người dùng đã xem.</summary>
    /// <exception cref="ConcurrencyConflictException">Khi hai phiên bản khác nhau.</exception>
    private void Check(DbContext? context)
    {
        if (context is null || _expected.Pending is not { } pending) return;
        var entry = context.ChangeTracker.Entries<IVersioned>()
            .FirstOrDefault(e => e.Entity.Id == pending.Id && e.State != EntityState.Added);
        if (entry is null) return;
        _expected.MarkChecked();
        if (entry.Property(e => e.Version).OriginalValue != pending.Version) throw new ConcurrencyConflictException();
    }
}
