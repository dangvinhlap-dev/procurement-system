namespace Proc.Application.Common;

/// <summary>
/// Chạy một thao tác <b>thay mặt tổng công ty</b> tại một công ty thành viên: phạm vi tạm đổi sang công ty đó với đúng các quyền được truyền
/// vào, mọi kho của công ty đó, phạm vi nhóm hàng giữ như ở tổng công ty; xong thì trả lại phạm vi cũ. Dùng khi tổng công ty lập chứng từ
/// cho công ty thành viên – ví dụ đặt hàng gộp sinh mỗi pháp nhân một PO (design.md mục 18.25, lát G7/G7c): người gọi phải tự kiểm tra
/// quyền đặc biệt ở tổng công ty trước khi gọi. Chứng từ vẫn đi qua service thường nên global query filter và kiểm tra của công ty nhận
/// (kho, NCC áp dụng, ngân sách, giá) vẫn áp dụng – không có đường vòng qua phạm vi (CLAUDE.md mục 4).
/// </summary>
public interface ICompanyScopeRunner
{
    /// <summary>
    /// Chạy thao tác tại công ty <paramref name="companyId"/> thay mặt tổng công ty.
    /// </summary>
    /// <typeparam name="T">Kết quả.</typeparam>
    /// <param name="companyId">Công ty thành viên.</param>
    /// <param name="permissions">Quyền tạm có tại công ty đó – chỉ những quyền thao tác cần (ví dụ tạo, xem PO).</param>
    /// <param name="action">Thao tác; chạy tuần tự, không song song với thao tác khác trên cùng request.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả của thao tác.</returns>
    /// <exception cref="ForbiddenAccessException">Khi chưa đăng nhập hoặc công ty không tồn tại, đã ngừng hoạt động.</exception>
    Task<T> RunOnBehalfAsync<T>(Guid companyId, IReadOnlyCollection<string> permissions, Func<Task<T>> action, CancellationToken cancellationToken);
}
