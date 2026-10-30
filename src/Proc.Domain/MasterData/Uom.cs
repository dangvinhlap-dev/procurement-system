using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>
/// Đơn vị tính dùng chung toàn tập đoàn: cái, hộp, thùng, kg… (design.md mục 5.2).
/// </summary>
[Auditable("Đơn vị tính", LabelProperty = nameof(Code))]
public sealed class Uom : AuditableEntity
{
    /// <summary>Độ dài tối đa của mã.</summary>
    public const int CodeMaxLength = 10;

    private Uom()
    {
    }

    /// <summary>Mã đơn vị tính, duy nhất, ví dụ CAI, HOP, KG.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên hiển thị trên chứng từ, ví dụ "Cái", "Hộp".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Còn sử dụng.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tạo đơn vị tính.
    /// </summary>
    /// <param name="code">Mã (1–10 chữ không dấu hoặc số).</param>
    /// <param name="name">Tên.</param>
    /// <returns>Đơn vị tính mới.</returns>
    /// <exception cref="BusinessRuleException">Khi mã hoặc tên không hợp lệ.</exception>
    public static Uom Create(string code, string name)
    {
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 1, CodeMaxLength))
            throw new BusinessRuleException($"Mã đơn vị tính gồm 1–{CodeMaxLength} ký tự chữ không dấu (A–Z) hoặc số.");
        var uom = new Uom { Code = normalized };
        uom.Rename(name);
        return uom;
    }

    /// <summary>
    /// Đổi tên.
    /// </summary>
    /// <param name="name">Tên mới.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng.</exception>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên đơn vị tính không được để trống.");
        Name = name.Trim();
    }

    /// <summary>Ngừng dùng (kiểm tra còn hàng đang dùng làm ĐVT cơ sở do tầng Infrastructure làm).</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Dùng lại.</summary>
    public void Activate() => IsActive = true;
}
