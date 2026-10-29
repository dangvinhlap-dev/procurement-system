using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>
/// Kho người dùng được phép tại một công ty (design.md mục 9.2). Không có dòng nào cho (người dùng, công ty)
/// nghĩa là được mọi kho của công ty đó.
/// </summary>
[Auditable("Phạm vi kho")]
public sealed class UserWarehouseScope : AuditableEntity
{
    private UserWarehouseScope()
    {
    }

    /// <summary>Người dùng.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Công ty của kho – có khóa ngoại kép (WarehouseId, CompanyId) để không gán nhầm kho công ty khác.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Kho được phép.</summary>
    public Guid WarehouseId { get; private set; }

    /// <summary>
    /// Cho phép người dùng làm việc với một kho.
    /// </summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="warehouse">Kho được phép.</param>
    /// <returns>Dòng phạm vi kho.</returns>
    public static UserWarehouseScope Allow(Guid userId, Warehouse warehouse) =>
        new() { UserId = userId, CompanyId = warehouse.CompanyId, WarehouseId = warehouse.Id };
}

/// <summary>
/// Nhóm hàng người dùng được phép tại một công ty, kế thừa xuống nhóm con (design.md mục 9.2).
/// Không có dòng nào cho (người dùng, công ty) nghĩa là được mọi nhóm hàng.
/// Gán theo công ty (giống phạm vi kho) vì cùng một người có thể quản lý nhóm hàng khác nhau ở từng công ty.
/// </summary>
[Auditable("Phạm vi nhóm hàng")]
public sealed class UserItemCategoryScope : AuditableEntity
{
    private UserItemCategoryScope()
    {
    }

    /// <summary>Người dùng.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Công ty áp dụng phạm vi.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Nhóm hàng được phép (gồm cả các nhóm con).</summary>
    public Guid ItemCategoryId { get; private set; }

    /// <summary>
    /// Cho phép người dùng làm việc với một nhóm hàng (và các nhóm con) tại một công ty.
    /// </summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="companyId">Công ty.</param>
    /// <param name="itemCategoryId">Nhóm hàng.</param>
    /// <returns>Dòng phạm vi nhóm hàng.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty.</exception>
    public static UserItemCategoryScope Allow(Guid userId, Guid companyId, Guid itemCategoryId)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Phạm vi nhóm hàng phải gắn với một công ty.");
        return new UserItemCategoryScope { UserId = userId, CompanyId = companyId, ItemCategoryId = itemCategoryId };
    }
}
