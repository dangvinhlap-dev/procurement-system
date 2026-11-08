namespace Proc.Application.Common;

/// <summary>
/// Chạy một thao tác dưới phạm vi của người dùng tại một công ty khác với công ty đang làm việc (quyền, kho, nhóm hàng của chính người đó
/// tại công ty kia), xong thì trả lại phạm vi cũ. Dùng khi một nghiệp vụ ở tổng công ty phải lập chứng từ cho công ty thành viên – ví dụ
/// đặt hàng gộp sinh mỗi pháp nhân một PO (design.md mục 18.25, lát G7): chứng từ vẫn lập đúng như người đó tự chuyển sang công ty kia
/// mà lập, mọi filter và kiểm tra của công ty kia vẫn áp dụng – không có đường vòng qua phạm vi (CLAUDE.md mục 4).
/// </summary>
public interface ICompanyScopeRunner
{
    /// <summary>
    /// Chạy thao tác tại công ty <paramref name="companyId"/>.
    /// </summary>
    /// <typeparam name="T">Kết quả.</typeparam>
    /// <param name="companyId">Công ty.</param>
    /// <param name="action">Thao tác; chạy tuần tự, không song song với thao tác khác trên cùng request.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả của thao tác.</returns>
    /// <exception cref="ForbiddenAccessException">Khi người dùng không có vai trò đang dùng tại công ty đó (hoặc công ty ngừng hoạt động).</exception>
    Task<T> RunAsync<T>(Guid companyId, Func<Task<T>> action, CancellationToken cancellationToken);
}
