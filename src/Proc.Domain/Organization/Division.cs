using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>
/// Khối của một công ty (ví dụ Khối HCNS-CNTT) – nhóm các bộ phận, có Giám đốc khối là người duyệt động "GĐ khối của phiếu" trong chuỗi
/// duyệt theo biểu mẫu CUVT (design.md mục 19.3, lát O1). Mỗi công ty tự quản lý danh mục của mình như danh mục bộ phận.
/// </summary>
[Auditable("Khối", LabelProperty = nameof(Code))]
public sealed class Division : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài tên tối đa.</summary>
    public const int NameMaxLength = 200;

    private Division()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Mã khối, duy nhất trong công ty, không đổi sau khi tạo.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên khối.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Giám đốc khối (tài khoản người dùng có quyền tại công ty) – người duyệt cấp "GĐ khối của phiếu"; null = chưa khai, phiếu cần cấp
    /// này bị chặn gửi duyệt.
    /// </summary>
    public Guid? DirectorUserId { get; private set; }

    /// <summary>Còn sử dụng. Khối ngừng dùng không gán được cho bộ phận nữa nhưng bộ phận đang gán vẫn giữ.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Tạo khối.</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="code">Mã 2–20 ký tự chữ không dấu hoặc số (tự viết hoa).</param>
    /// <param name="name">Tên, bắt buộc.</param>
    /// <param name="directorUserId">Giám đốc khối (tùy chọn).</param>
    /// <returns>Khối đang dùng.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, mã sai quy tắc hoặc tên rỗng.</exception>
    public static Division Create(Guid companyId, string code, string name, Guid? directorUserId)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Khối phải thuộc một công ty.");
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 2, 20))
            throw new BusinessRuleException("Mã khối phải gồm 2–20 ký tự chữ không dấu (A–Z) hoặc số.");
        var division = new Division { CompanyId = companyId, Code = normalized };
        division.Update(name, directorUserId, isActive: true);
        return division;
    }

    /// <summary>Sửa tên, Giám đốc khối, ngừng dùng hoặc dùng lại. Mã không đổi.</summary>
    /// <param name="name">Tên, bắt buộc.</param>
    /// <param name="directorUserId">Giám đốc khối.</param>
    /// <param name="isActive">Còn sử dụng.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng hoặc quá dài.</exception>
    public void Update(string name, Guid? directorUserId, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên khối không được để trống.");
        if (name.Trim().Length > NameMaxLength) throw new BusinessRuleException($"Tên khối tối đa {NameMaxLength} ký tự.");
        Name = name.Trim();
        DirectorUserId = directorUserId == Guid.Empty ? null : directorUserId;
        IsActive = isActive;
    }
}

/// <summary>
/// Bộ phận của người dùng tại một công ty (lát O1, design.md mục 19.3): mỗi người tối đa một bộ phận ở mỗi công ty. Dùng điền sẵn bộ phận
/// khi lập đề nghị mua; không ảnh hưởng phạm vi dữ liệu (phạm vi vẫn theo vai trò, kho, nhóm hàng).
/// </summary>
[Auditable("Bộ phận của người dùng")]
public sealed class UserDepartment : AuditableEntity
{
    private UserDepartment()
    {
    }

    /// <summary>Người dùng.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Công ty – có khóa ngoại kép (DepartmentId, CompanyId) để không gán nhầm bộ phận công ty khác.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Bộ phận.</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>Gán người dùng vào bộ phận.</summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="department">Bộ phận (lấy công ty theo bộ phận).</param>
    /// <returns>Dòng gán.</returns>
    public static UserDepartment Assign(Guid userId, Department department) =>
        new() { UserId = userId, CompanyId = department.CompanyId, DepartmentId = department.Id };

    /// <summary>Chuyển sang bộ phận khác cùng công ty.</summary>
    /// <param name="department">Bộ phận mới.</param>
    /// <exception cref="BusinessRuleException">Khi bộ phận thuộc công ty khác.</exception>
    public void MoveTo(Department department)
    {
        if (department.CompanyId != CompanyId) throw new BusinessRuleException("Bộ phận phải thuộc cùng công ty.");
        DepartmentId = department.Id;
    }
}

/// <summary>
/// Phó phòng ban (tài khoản) của một bộ phận (người dùng chốt 19/11/2026 – design.md mục 19.3 "gộp bước người lập – Trưởng phòng ban"). Một
/// bộ phận có nhiều Phó phòng ban. Phó phòng ban <b>không</b> duyệt thay Trưởng phòng ban trên phiếu của người khác; chỉ khi chính họ lập phiếu
/// của bộ phận <b>chưa có Trưởng phòng ban</b> thì bước "Trưởng phòng ban của phiếu" gộp với người lập (bộ phận có Trưởng phòng ban thì
/// Trưởng phòng ban vẫn duyệt – người lập nhỏ hơn).
/// </summary>
[Auditable("Phó phòng ban")]
public sealed class DepartmentDeputy : AuditableEntity, ICompanyOwned
{
    private DepartmentDeputy()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Bộ phận.</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>Tài khoản Phó phòng ban.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gán Phó phòng ban cho bộ phận (lấy công ty theo bộ phận).</summary>
    /// <param name="department">Bộ phận.</param>
    /// <param name="userId">Tài khoản.</param>
    /// <returns>Dòng gán.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu tài khoản.</exception>
    public static DepartmentDeputy Assign(Department department, Guid userId)
    {
        if (userId == Guid.Empty) throw new BusinessRuleException("Hãy chọn tài khoản Phó phòng ban.");
        return new() { CompanyId = department.CompanyId, DepartmentId = department.Id, UserId = userId };
    }
}
