using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>
/// Bộ phận (phòng ban, phân xưởng) của một công ty – đơn vị đề nghị mua hàng trên PR, sau này dùng cho ngân sách theo
/// công ty × bộ phận × tháng (design.md mục 5.1, 18.5). Mỗi công ty tự quản lý danh mục của mình.
/// </summary>
[Auditable("Bộ phận", LabelProperty = nameof(Code))]
public sealed class Department : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài tên tối đa.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Độ dài tên trưởng bộ phận tối đa.</summary>
    public const int ManagerNameMaxLength = 200;

    private Department()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Mã bộ phận, duy nhất trong công ty, không đổi sau khi tạo (đã nằm trên PR).</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên bộ phận.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Trưởng bộ phận (họ tên in trên phiếu).</summary>
    public string? ManagerName { get; private set; }

    /// <summary>
    /// Khối của bộ phận (lát O1, design.md mục 19.3) – Giám đốc khối là người duyệt cấp "GĐ khối của phiếu"; null = chưa gán khối.
    /// </summary>
    public Guid? DivisionId { get; private set; }

    /// <summary>
    /// Trưởng phòng ban (tài khoản người dùng có quyền tại công ty) – người duyệt cấp "Trưởng phòng ban của phiếu" (lát O1); null = chưa khai,
    /// phiếu cần cấp này bị chặn gửi duyệt.
    /// </summary>
    public Guid? ManagerUserId { get; private set; }

    /// <summary>Còn sử dụng. Bộ phận ngừng dùng không chọn được trên PR mới nhưng vẫn hiện trên phiếu cũ.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tạo bộ phận.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="code">Mã 2–20 ký tự chữ không dấu hoặc số (tự viết hoa).</param>
    /// <param name="name">Tên, bắt buộc.</param>
    /// <param name="managerName">Trưởng bộ phận.</param>
    /// <returns>Bộ phận đang dùng.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, mã sai quy tắc hoặc tên rỗng.</exception>
    public static Department Create(Guid companyId, string code, string name, string? managerName)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Bộ phận phải thuộc một công ty.");
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 2, 20))
            throw new BusinessRuleException("Mã bộ phận phải gồm 2–20 ký tự chữ không dấu (A–Z) hoặc số.");
        var department = new Department { CompanyId = companyId, Code = normalized };
        department.Update(name, managerName, isActive: true);
        return department;
    }

    /// <summary>
    /// Sửa tên, trưởng bộ phận, ngừng dùng hoặc dùng lại. Mã không đổi.
    /// </summary>
    /// <param name="name">Tên, bắt buộc.</param>
    /// <param name="managerName">Trưởng bộ phận.</param>
    /// <param name="isActive">Còn sử dụng.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng.</exception>
    public void Update(string name, string? managerName, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên bộ phận không được để trống.");
        Name = name.Trim();
        ManagerName = string.IsNullOrWhiteSpace(managerName) ? null : managerName.Trim();
        IsActive = isActive;
    }

    /// <summary>
    /// Gán khối và Trưởng phòng ban (tài khoản) – dữ liệu cho người duyệt động (lát O1). Dịch vụ kiểm tra khối cùng công ty và tài khoản có
    /// quyền tại công ty trước khi gọi.
    /// </summary>
    /// <param name="divisionId">Khối; null = bỏ gán.</param>
    /// <param name="managerUserId">Trưởng phòng ban; null = chưa khai.</param>
    public void AssignOrganization(Guid? divisionId, Guid? managerUserId)
    {
        DivisionId = divisionId == Guid.Empty ? null : divisionId;
        ManagerUserId = managerUserId == Guid.Empty ? null : managerUserId;
    }
}
