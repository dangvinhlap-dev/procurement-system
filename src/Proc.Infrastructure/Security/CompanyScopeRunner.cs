using Proc.Application.Common;

namespace Proc.Infrastructure.Security;

/// <summary>
/// Đổi tạm <see cref="CurrentUserScope"/> của request sang công ty khác bằng chính <see cref="UserScopeLoader"/> (như khi người dùng gửi
/// X-Company-Id của công ty đó), chạy thao tác rồi trả lại phạm vi cũ trong <c>finally</c> – kể cả khi thao tác lỗi. Global query filter của
/// DbContext đọc phạm vi lúc chạy câu lệnh nên tự theo công ty mới.
/// </summary>
public sealed class CompanyScopeRunner : ICompanyScopeRunner
{
    private readonly CurrentUserScope _scope;
    private readonly UserScopeLoader _loader;

    /// <summary>Tạo bộ chạy.</summary>
    /// <param name="scope">Phạm vi của request.</param>
    /// <param name="loader">Bộ nạp phạm vi.</param>
    public CompanyScopeRunner(CurrentUserScope scope, UserScopeLoader loader)
    {
        _scope = scope;
        _loader = loader;
    }

    /// <inheritdoc />
    public async Task<T> RunAsync<T>(Guid companyId, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var saved = _scope.Capture();
        try
        {
            var result = await _loader.LoadAsync(userId, companyId, cancellationToken);
            if (result != ScopeLoadResult.Ok)
                throw new ForbiddenAccessException("Bạn không có vai trò đang dùng tại công ty này (hoặc công ty đã ngừng hoạt động).");
            return await action();
        }
        finally
        {
            _scope.Restore(saved);
        }
    }
}
