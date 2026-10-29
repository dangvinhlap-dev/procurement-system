namespace Proc.Application.Common;

/// <summary>
/// Phạm vi của người dùng trong request hiện tại (design.md mục 9.2): công ty đang làm việc (header X-Company-Id),
/// quyền chức năng tại công ty đó, kho và nhóm hàng được phép. Nạp một lần mỗi request.
/// Global query filter của EF Core đọc phạm vi này để tự lọc dữ liệu; lệnh ghi phải gọi các hàm Ensure… để kiểm tra lại,
/// không tin dữ liệu từ client (CLAUDE.md mục 4).
/// </summary>
public interface ICurrentUserScope
{
    /// <summary>Người dùng; null khi chưa đăng nhập.</summary>
    Guid? UserId { get; }

    /// <summary>Công ty đang làm việc; null khi request không gửi X-Company-Id.</summary>
    Guid? CompanyId { get; }

    /// <summary>Mã quyền của người dùng tại công ty đang làm việc.</summary>
    IReadOnlySet<string> Permissions { get; }

    /// <summary>true khi được mọi kho của công ty (không có dòng UserWarehouseScope nào).</summary>
    bool AllWarehouses { get; }

    /// <summary>Kho được phép khi AllWarehouses = false.</summary>
    IReadOnlyList<Guid> WarehouseIds { get; }

    /// <summary>true khi được mọi nhóm hàng (không có dòng UserItemCategoryScope nào tại công ty).</summary>
    bool AllItemCategories { get; }

    /// <summary>Nhóm hàng được phép khi AllItemCategories = false, đã gồm mọi nhóm con.</summary>
    IReadOnlyList<Guid> ItemCategoryIds { get; }

    /// <summary>
    /// Có quyền chức năng tại công ty đang làm việc không.
    /// </summary>
    /// <param name="permission">Mã quyền, ví dụ Purchase.PO.Create.</param>
    /// <returns>true nếu có.</returns>
    bool HasPermission(string permission);

    /// <summary>
    /// Lấy công ty đang làm việc, bắt buộc phải có.
    /// </summary>
    /// <returns>Id công ty.</returns>
    /// <exception cref="ForbiddenAccessException">Khi request chưa chọn công ty.</exception>
    Guid RequireCompany();

    /// <summary>
    /// Bắt buộc có quyền chức năng.
    /// </summary>
    /// <param name="permission">Mã quyền.</param>
    /// <exception cref="ForbiddenAccessException">Khi chưa chọn công ty hoặc không có quyền.</exception>
    void EnsurePermission(string permission);

    /// <summary>
    /// Kiểm tra bản ghi cần ghi thuộc công ty đang làm việc – chặn client gửi CompanyId của công ty khác.
    /// </summary>
    /// <param name="companyId">CompanyId của bản ghi.</param>
    /// <exception cref="ForbiddenAccessException">Khi khác công ty đang làm việc.</exception>
    void EnsureCompany(Guid companyId);

    /// <summary>
    /// Kiểm tra kho nằm trong phạm vi kho được phép (chỉ kiểm tra phạm vi; việc kho thuộc công ty nào
    /// do global query filter và EnsureCompany đảm bảo).
    /// </summary>
    /// <param name="warehouseId">Kho.</param>
    /// <exception cref="ForbiddenAccessException">Khi kho ngoài phạm vi.</exception>
    void EnsureWarehouse(Guid warehouseId);
}

/// <summary>
/// Không đủ quyền hoặc ngoài phạm vi dữ liệu. API trả 403 kèm thông điệp tiếng Việt.
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    /// <summary>
    /// Tạo lỗi không đủ quyền.
    /// </summary>
    /// <param name="message">Thông điệp tiếng Việt cho người dùng.</param>
    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}
