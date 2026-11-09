using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Security;

/// <summary>
/// Đổi tạm <see cref="CurrentUserScope"/> của request sang công ty thành viên để chạy thao tác thay mặt tổng công ty, rồi trả lại phạm vi cũ
/// trong <c>finally</c> – kể cả khi thao tác lỗi. Global query filter của DbContext đọc phạm vi lúc chạy câu lệnh nên tự theo công ty mới.
/// </summary>
public sealed class CompanyScopeRunner : ICompanyScopeRunner
{
    private readonly CurrentUserScope _scope;
    private readonly ProcDbContext _db;

    /// <summary>Tạo bộ chạy.</summary>
    /// <param name="scope">Phạm vi của request.</param>
    /// <param name="db">CSDL – kiểm tra công ty còn hoạt động.</param>
    public CompanyScopeRunner(CurrentUserScope scope, ProcDbContext db)
    {
        _scope = scope;
        _db = db;
    }

    /// <inheritdoc />
    public async Task<T> RunOnBehalfAsync<T>(
        Guid companyId, IReadOnlyCollection<string> permissions, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(action);
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        if (!await _db.Companies.AsNoTracking().AnyAsync(c => c.Id == companyId && c.IsActive, cancellationToken))
            throw new ForbiddenAccessException("Công ty không tồn tại hoặc đã ngừng hoạt động.");
        var saved = _scope.Capture();
        try
        {
            // Mọi kho của công ty nhận: kho đã được nghiệp vụ gọi kiểm tra thuộc đúng công ty, còn dùng. Nhóm hàng giữ phạm vi ở tổng công ty –
            // người lập chỉ đặt được mặt hàng trong phạm vi của mình như khi lập phiếu.
            _scope.Initialize(userId, companyId, permissions, allWarehouses: true, [], saved.AllItemCategories, saved.ItemCategoryIds);
            return await action();
        }
        finally
        {
            _scope.Restore(saved);
        }
    }
}
