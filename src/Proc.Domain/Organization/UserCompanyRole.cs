using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>
/// Gán vai trò cho người dùng theo từng công ty (design.md mục 9.1, 9.2): một người có thể là Thủ kho ở công ty A
/// nhưng chỉ Xem báo cáo ở công ty B. Người dùng chỉ chọn được công ty mà mình có ít nhất một dòng gán ở đây.
/// </summary>
public sealed class UserCompanyRole : AuditableEntity
{
    private UserCompanyRole()
    {
    }

    /// <summary>Người dùng (bảng User do Identity quản lý).</summary>
    public Guid UserId { get; private set; }

    /// <summary>Công ty được làm việc.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Vai trò tại công ty đó.</summary>
    public Guid RoleId { get; private set; }

    /// <summary>Công ty (điều hướng).</summary>
    public Company? Company { get; private set; }

    /// <summary>Vai trò (điều hướng).</summary>
    public Role? Role { get; private set; }

    /// <summary>
    /// Gán vai trò tại một công ty cho người dùng.
    /// </summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="companyId">Công ty.</param>
    /// <param name="roleId">Vai trò.</param>
    /// <returns>Dòng gán mới.</returns>
    public static UserCompanyRole Assign(Guid userId, Guid companyId, Guid roleId) =>
        new() { UserId = userId, CompanyId = companyId, RoleId = roleId };
}
