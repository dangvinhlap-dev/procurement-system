using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>
/// Đơn vị mua của một công ty (ví dụ "VP NDD", "Phòng CUVT") – nơi được giao đi mua từng dòng trên bảng tổng hợp đề xuất mua 2A/2B. Người dùng
/// chốt 10/11/2026 (design.md mục 19.1 – Q3, lát Y5a): đơn vị mua là một danh mục người được cấp quyền tự tạo, quản lý như danh mục bộ phận.
/// </summary>
[Auditable("Đơn vị mua", LabelProperty = nameof(Code))]
public sealed class PurchasingUnit : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài tên tối đa.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Độ dài ghi chú tối đa.</summary>
    public const int NoteMaxLength = 500;

    private PurchasingUnit()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Mã, duy nhất trong công ty, không đổi sau khi tạo.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên đơn vị mua.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Còn sử dụng. Đơn vị ngừng dùng không chọn mới được trên bảng tổng hợp nhưng dòng đã gán giữ nguyên – không xóa để giữ lịch sử
    /// (design.md mục 19.3, lát Y5a).
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Tạo đơn vị mua.</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="code">Mã 2–20 ký tự chữ không dấu hoặc số (tự viết hoa).</param>
    /// <param name="name">Tên, bắt buộc.</param>
    /// <param name="note">Ghi chú (tùy chọn).</param>
    /// <returns>Đơn vị mua đang dùng.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, mã sai quy tắc, tên rỗng hoặc quá dài.</exception>
    public static PurchasingUnit Create(Guid companyId, string code, string name, string? note)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Đơn vị mua phải thuộc một công ty.");
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 2, 20))
            throw new BusinessRuleException("Mã đơn vị mua phải gồm 2–20 ký tự chữ không dấu (A–Z) hoặc số.");
        var unit = new PurchasingUnit { CompanyId = companyId, Code = normalized };
        unit.Update(name, note, isActive: true);
        return unit;
    }

    /// <summary>Sửa tên, ghi chú, ngừng dùng hoặc dùng lại. Mã không đổi.</summary>
    /// <param name="name">Tên, bắt buộc.</param>
    /// <param name="note">Ghi chú; rỗng = bỏ.</param>
    /// <param name="isActive">Còn sử dụng.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng, tên hoặc ghi chú quá dài.</exception>
    public void Update(string name, string? note, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên đơn vị mua không được để trống.");
        if (name.Trim().Length > NameMaxLength) throw new BusinessRuleException($"Tên đơn vị mua tối đa {NameMaxLength} ký tự.");
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote?.Length > NoteMaxLength) throw new BusinessRuleException($"Ghi chú tối đa {NoteMaxLength} ký tự.");
        Name = name.Trim();
        Note = trimmedNote;
        IsActive = isActive;
    }
}
