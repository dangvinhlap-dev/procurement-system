using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>Loại quan hệ lợi ích giữa nhân sự và NCC (design.md mục 18.8).</summary>
public enum ConflictRelation
{
    /// <summary>Người thân (vợ/chồng, cha mẹ, con, anh chị em…) sở hữu, điều hành hoặc làm việc tại NCC.</summary>
    Family = 1,

    /// <summary>Bản thân góp vốn, nắm cổ phần của NCC.</summary>
    Ownership = 2,

    /// <summary>Bản thân đang hoặc từng làm việc, cộng tác với NCC.</summary>
    Employment = 3,

    /// <summary>Quan hệ khác có thể ảnh hưởng tới tính khách quan.</summary>
    Other = 9,
}

/// <summary>
/// Kê khai xung đột lợi ích (design.md mục 18.8 – đề xuất Ban CUVT N4): một người dùng có quan hệ lợi ích với một NCC. Khi còn hiệu lực,
/// người đó không chấm thầu, chọn thầu, duyệt phiếu có NCC này. Kê khai gắn với con người nên không thuộc công ty nào (không lọc theo
/// công ty). Không xóa, không sửa nội dung: hết quan hệ thì người có quyền quản lý kết thúc hiệu lực kèm lý do – giữ dấu vết cho KSNB.
/// </summary>
[Auditable("Kê khai xung đột lợi ích", LabelProperty = nameof(Description))]
public sealed class ConflictDeclaration : AuditableEntity
{
    /// <summary>Độ dài mô tả, lý do kết thúc tối đa.</summary>
    public const int TextMaxLength = 500;

    private ConflictDeclaration()
    {
    }

    /// <summary>Người có quan hệ lợi ích.</summary>
    public Guid UserId { get; private set; }

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Loại quan hệ.</summary>
    public ConflictRelation Relation { get; private set; }

    /// <summary>Mô tả quan hệ, ví dụ "Em trai là giám đốc kinh doanh".</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Hiệu lực từ ngày.</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>Hiệu lực đến ngày (null = chưa kết thúc).</summary>
    public DateOnly? ValidTo { get; private set; }

    /// <summary>Lý do kết thúc hiệu lực.</summary>
    public string? EndReason { get; private set; }

    /// <summary>
    /// Tạo kê khai.
    /// </summary>
    /// <param name="userId">Người có quan hệ.</param>
    /// <param name="supplierId">NCC.</param>
    /// <param name="relation">Loại quan hệ.</param>
    /// <param name="description">Mô tả, bắt buộc.</param>
    /// <param name="validFrom">Hiệu lực từ ngày.</param>
    /// <returns>Kê khai chưa kết thúc.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu người, NCC, mô tả hoặc loại quan hệ sai.</exception>
    public static ConflictDeclaration Create(Guid userId, Guid supplierId, ConflictRelation relation, string description, DateOnly validFrom)
    {
        if (userId == Guid.Empty) throw new BusinessRuleException("Phải chọn người kê khai.");
        if (supplierId == Guid.Empty) throw new BusinessRuleException("Phải chọn nhà cung cấp.");
        if (!Enum.IsDefined(relation)) throw new BusinessRuleException("Loại quan hệ không hợp lệ.");
        if (string.IsNullOrWhiteSpace(description)) throw new BusinessRuleException("Hãy mô tả quan hệ lợi ích.");
        return new ConflictDeclaration
        {
            UserId = userId, SupplierId = supplierId, Relation = relation, Description = description.Trim(), ValidFrom = validFrom,
        };
    }

    /// <summary>Còn hiệu lực vào ngày <paramref name="date"/>: từ ngày ≤ date và chưa kết thúc hoặc ngày kết thúc ≥ date.</summary>
    /// <param name="date">Ngày xét (ngày Việt Nam).</param>
    public bool IsActiveOn(DateOnly date) => ValidFrom <= date && (ValidTo is null || ValidTo >= date);

    /// <summary>
    /// Kết thúc hiệu lực (hết quan hệ). Ngày kết thúc thường là hôm qua để từ hôm nay người đó được tham gia lại.
    /// </summary>
    /// <param name="validTo">
    /// Ngày cuối còn quan hệ; sớm nhất là ngày trước ngày bắt đầu (kê khai nhầm – coi như chưa từng hiệu lực).
    /// </param>
    /// <param name="reason">Lý do, bắt buộc.</param>
    /// <exception cref="BusinessRuleException">Khi đã kết thúc, ngày trước ngày bắt đầu hoặc thiếu lý do.</exception>
    public void End(DateOnly validTo, string? reason)
    {
        if (ValidTo is not null) throw new BusinessRuleException("Kê khai này đã kết thúc hiệu lực.");
        if (validTo < ValidFrom.AddDays(-1)) throw new BusinessRuleException("Ngày kết thúc quá sớm so với ngày bắt đầu hiệu lực.");
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Hãy nhập lý do kết thúc kê khai.");
        ValidTo = validTo;
        EndReason = reason.Trim();
    }
}
