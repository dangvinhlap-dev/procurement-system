using Proc.Domain.Organization;

namespace Proc.Application.Common;

/// <summary>
/// Bật/tắt phân hệ – mục <c>Modules</c> trong appsettings. Từ bản 0.22.0 phân hệ kho tắt mặc định vì công ty dùng phần mềm kho riêng
/// (người dùng chốt 10/11/2026 – design.md mục 19.4, lát K1). Tắt bằng cách bỏ quyền của phân hệ, không xóa dữ liệu: đặt lại <c>true</c>
/// là quay về như bản 0.21.0. Code và bảng kho gỡ hẳn ở 0.28.0 (lát K3).
/// </summary>
public sealed class ModuleOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Modules";

    /// <summary>
    /// Phân hệ kho còn dùng không. Mặc định <c>false</c>. CSDL test tạm đặt <c>true</c> đến khi lát K2 chuyển nguồn "đã nhận" sang XNH.
    /// </summary>
    public bool InventoryEnabled { get; set; }

    /// <summary>
    /// Quyền có bị tắt theo phân hệ không.
    /// </summary>
    /// <param name="permissionCode">Mã quyền.</param>
    /// <returns>true nếu quyền thuộc phân hệ đang tắt.</returns>
    public bool IsDisabled(string permissionCode) => !InventoryEnabled && Permissions.InventoryModule.Contains(permissionCode);

    /// <summary>
    /// Bỏ các quyền thuộc phân hệ đang tắt.
    /// </summary>
    /// <param name="permissionCodes">Mã quyền.</param>
    /// <returns>Các mã quyền còn hiệu lực, giữ nguyên thứ tự.</returns>
    public IEnumerable<string> Filter(IEnumerable<string> permissionCodes) => permissionCodes.Where(c => !IsDisabled(c));
}
