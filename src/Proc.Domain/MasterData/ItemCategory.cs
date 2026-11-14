using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>
/// Nhóm hàng dạng cây tối đa <see cref="MaxLevel"/> cấp, dùng chung toàn tập đoàn (design.md mục 5.2, 5.7 – quy tắc N4).
/// Dùng để phân quyền theo nhóm hàng: được phép một nhóm thì được phép mọi nhóm con (design.md mục 9.2).
/// </summary>
[Auditable("Nhóm hàng", LabelProperty = nameof(Code))]
public sealed class ItemCategory : AuditableEntity
{
    /// <summary>Số cấp tối đa của cây nhóm hàng (người dùng chốt 14/11/2026 – design.md mục 5.7, N4-1).</summary>
    public const int MaxLevel = 4;

    private static readonly string[] LevelNames = ["Ngành hàng", "Nhóm hàng", "Loại hàng", "Chủng loại"];

    private ItemCategory()
    {
    }

    /// <summary>Mã nhóm, duy nhất toàn tập đoàn – người dùng tự gõ khi thêm (N4-7), không đổi sau khi tạo.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên nhóm.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Nhóm cha; null là nhóm gốc.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// Cấp trên cây: 1 = nhóm gốc (Ngành hàng) … 4 = Chủng loại. Lưu sẵn (N4-2) để CSDL ràng buộc 1–4 và lọc theo cấp không phải dựng cây;
    /// đổi nhóm cha thì tầng Infrastructure tính lại cả nhánh con qua <see cref="FollowParent"/>.
    /// </summary>
    public int Level { get; private set; } = 1;

    /// <summary>Tài khoản kho mặc định (ví dụ 152, 153, 156) – gợi ý khi xuất dữ liệu sang phần mềm kế toán.</summary>
    public string? InventoryAccount { get; private set; }

    /// <summary>Còn sử dụng.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tạo nhóm hàng.
    /// </summary>
    /// <param name="code">Mã nhóm.</param>
    /// <param name="name">Tên nhóm.</param>
    /// <param name="parent">Nhóm cha; null nếu là nhóm gốc.</param>
    /// <returns>Nhóm hàng mới.</returns>
    /// <exception cref="BusinessRuleException">Khi mã hoặc tên không hợp lệ, hoặc nhóm cha đã ở cấp cuối (<see cref="MaxLevel"/>).</exception>
    public static ItemCategory Create(string code, string name, ItemCategory? parent = null)
    {
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 2, 20))
            throw new BusinessRuleException("Mã nhóm hàng phải gồm 2–20 ký tự chữ không dấu (A–Z) hoặc số.");
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên nhóm hàng không được để trống.");
        // N4-3: nhóm cấp 4 là cấp cuối, không có nhóm con.
        if (parent is not null && parent.Level >= MaxLevel)
            throw new BusinessRuleException(
                $"Nhóm {parent.Code} – {parent.Name} đã ở cấp {MaxLevel} (cấp cuối), không thêm nhóm con được – nhóm hàng tối đa {MaxLevel} cấp.");
        return new ItemCategory { Code = normalized, Name = name.Trim(), ParentId = parent?.Id, Level = (parent?.Level ?? 0) + 1 };
    }

    /// <summary>Tên gọi của một cấp (N4, người dùng chốt 14/11/2026): 1 Ngành hàng, 2 Nhóm hàng, 3 Loại hàng, 4 Chủng loại.</summary>
    /// <param name="level">Cấp 1–4.</param>
    /// <returns>Tên cấp; ngoài 1–4 thì "Cấp n".</returns>
    public static string LevelName(int level) => level is >= 1 and <= MaxLevel ? LevelNames[level - 1] : $"Cấp {level}";

    /// <summary>
    /// Sửa tên và tài khoản kho mặc định. Mã không đổi: đã dùng trong phân quyền và báo cáo.
    /// </summary>
    /// <param name="name">Tên nhóm.</param>
    /// <param name="inventoryAccount">Tài khoản kho mặc định (chỉ số, 3–10 ký tự); trống = không có.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng hoặc tài khoản sai định dạng.</exception>
    public void Update(string name, string? inventoryAccount)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên nhóm hàng không được để trống.");
        var account = string.IsNullOrWhiteSpace(inventoryAccount) ? null : inventoryAccount.Trim();
        // Số hiệu tài khoản kế toán Việt Nam là dãy số (152, 1521…), không có chữ.
        if (account is not null && (account.Length is < 3 or > 10 || !account.All(char.IsAsciiDigit)))
            throw new BusinessRuleException("Tài khoản kho gồm 3–10 chữ số, ví dụ 152 hoặc 1521.");
        Name = name.Trim();
        InventoryAccount = account;
    }

    /// <summary>
    /// Đổi nhóm cha. Chặn vòng lặp (nhóm cha là nhóm con, cháu của chính nó) cần cả cây nên tầng Infrastructure kiểm tra trước.
    /// </summary>
    /// <param name="parent">Nhóm cha mới; null = thành nhóm gốc.</param>
    /// <param name="branchDepth">
    /// Số cấp của nhánh con bên dưới nhóm này (0 = không có nhóm con, 1 = chỉ có con, 2 = có cháu…) – tầng Infrastructure đếm trên cả cây.
    /// </param>
    /// <exception cref="BusinessRuleException">Khi nhóm cha là chính nó, hoặc chuyển xong nhóm cuối nhánh vượt cấp <see cref="MaxLevel"/> (N4-4).</exception>
    public void MoveTo(ItemCategory? parent, int branchDepth = 0)
    {
        if (parent?.Id == Id) throw new BusinessRuleException("Nhóm hàng không thể là nhóm cha của chính nó.");
        var level = (parent?.Level ?? 0) + 1;
        // Chuyển cả nhánh: nhóm sâu nhất của nhánh = cấp mới + số cấp con. Ví dụ nhóm có con và cháu (sâu 2) vào nhóm cấp 3 → cháu ở cấp 6.
        if (level + branchDepth > MaxLevel)
        {
            var allowedParentLevel = MaxLevel - 1 - branchDepth;
            var hint = allowedParentLevel <= 0
                ? "chỉ để được làm nhóm cấp 1"
                : allowedParentLevel == 1 ? "chỉ chuyển được làm nhóm cấp 1 hoặc vào nhóm cấp 1"
                : $"chỉ chuyển được làm nhóm cấp 1 hoặc vào nhóm cấp 1–{allowedParentLevel}";
            throw new BusinessRuleException(branchDepth == 0
                ? $"Nhóm {parent!.Code} – {parent.Name} đã ở cấp {parent.Level} (cấp cuối), không làm nhóm cha được – nhóm hàng tối đa {MaxLevel} cấp."
                : $"Nhóm {Code} – {Name} có nhánh con sâu {branchDepth} cấp. Chuyển vào nhóm {parent!.Code} (cấp {parent.Level}) thì nhóm cuối nhánh sẽ ở "
                  + $"cấp {level + branchDepth} – nhóm hàng tối đa {MaxLevel} cấp: {hint}.");
        }

        ParentId = parent?.Id;
        Level = level;
    }

    /// <summary>
    /// Tính lại cấp theo nhóm cha sau khi nhánh được chuyển (gọi cho từng nhóm con, cháu theo thứ tự từ trên xuống). Giới hạn cấp đã kiểm tra ở
    /// <see cref="MoveTo"/> cho cả nhánh; kiểm tra lại để không bao giờ lưu cấp ngoài 1–4.
    /// </summary>
    /// <param name="parent">Nhóm cha (đã có cấp mới).</param>
    /// <exception cref="BusinessRuleException">Khi nhóm không phải con của <paramref name="parent"/> hoặc vượt cấp tối đa.</exception>
    public void FollowParent(ItemCategory parent)
    {
        if (ParentId != parent.Id) throw new BusinessRuleException($"Nhóm {Code} không phải nhóm con của {parent.Code}.");
        if (parent.Level >= MaxLevel)
            throw new BusinessRuleException($"Nhóm {Code} sẽ vượt cấp {MaxLevel} – nhóm hàng tối đa {MaxLevel} cấp.");
        Level = parent.Level + 1;
    }

    /// <summary>Ngừng dùng (kiểm tra nhóm con, hàng hóa đang dùng do tầng Infrastructure làm).</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Dùng lại.</summary>
    public void Activate() => IsActive = true;
}
