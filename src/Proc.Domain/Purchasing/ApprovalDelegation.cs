using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>
/// Ủy quyền duyệt có thời hạn (lát O2, design.md mục 19.1 Q9 và 19.3): trong khoảng ngày hiệu lực, người được ủy quyền duyệt/từ chối thay
/// người ủy quyền ở những cấp mà người ủy quyền đang giữ (vai trò của cấp, Trưởng phòng ban / GĐ khối của phiếu). Thuộc một công ty vì vai trò,
/// bộ phận, khối đều theo công ty. Không sửa nội dung: sai thì thu hồi rồi lập lại – giữ dấu vết ai được duyệt thay vào lúc nào.
/// </summary>
[Auditable("Ủy quyền duyệt", LabelProperty = nameof(Reason))]
public sealed class ApprovalDelegation : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài lý do, ghi chú tối đa.</summary>
    public const int TextMaxLength = 500;

    /// <summary>
    /// Thời hạn tối đa của một lần ủy quyền (ngày, tính cả hai đầu). Ủy quyền là việc tạm thời (đi công tác, nghỉ phép) – lâu hơn thì phải đổi
    /// người giữ vị trí (vai trò, Trưởng phòng ban) cho đúng thực tế (tự đặt).
    /// </summary>
    public const int MaxDays = 90;

    private ApprovalDelegation()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Người ủy quyền (người giữ cấp duyệt).</summary>
    public Guid DelegatorUserId { get; private set; }

    /// <summary>Người được ủy quyền (người duyệt thay).</summary>
    public Guid DelegateUserId { get; private set; }

    /// <summary>Loại phiếu được duyệt thay (mã trong ApprovalTypes); null = mọi loại phiếu.</summary>
    public string? DocType { get; private set; }

    /// <summary>Hiệu lực từ ngày (ngày Việt Nam).</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>Hiệu lực đến hết ngày (ngày Việt Nam).</summary>
    public DateOnly ValidTo { get; private set; }

    /// <summary>Lý do ủy quyền, ví dụ "Đi công tác Hà Nội 15–20/11".</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Ghi chú thêm.</summary>
    public string? Note { get; private set; }

    /// <summary>Thời điểm thu hồi (UTC); null = chưa thu hồi.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Người thu hồi.</summary>
    public Guid? RevokedBy { get; private set; }

    /// <summary>
    /// Lập ủy quyền.
    /// Ví dụ hôm nay 10/11/2026: từ 12/11 đến 20/11 hợp lệ; từ 09/11 (quá khứ) hoặc từ 12/11 đến 15/02/2027 (quá 90 ngày) bị chặn.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="delegatorUserId">Người ủy quyền.</param>
    /// <param name="delegateUserId">Người được ủy quyền.</param>
    /// <param name="docType">Loại phiếu; null = mọi loại.</param>
    /// <param name="validFrom">Từ ngày – không trước hôm nay.</param>
    /// <param name="validTo">Đến ngày – không trước từ ngày, tối đa <see cref="MaxDays"/> ngày.</param>
    /// <param name="reason">Lý do, bắt buộc.</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="today">Hôm nay (ngày Việt Nam).</param>
    /// <returns>Ủy quyền chưa thu hồi.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu người, tự ủy quyền cho mình, ngày sai hoặc thiếu lý do.</exception>
    public static ApprovalDelegation Create(
        Guid companyId, Guid delegatorUserId, Guid delegateUserId, string? docType, DateOnly validFrom, DateOnly validTo, string reason, string? note,
        DateOnly today)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Ủy quyền phải thuộc một công ty.");
        if (delegatorUserId == Guid.Empty) throw new BusinessRuleException("Hãy chọn người ủy quyền.");
        if (delegateUserId == Guid.Empty) throw new BusinessRuleException("Hãy chọn người được ủy quyền.");
        if (delegatorUserId == delegateUserId) throw new BusinessRuleException("Không ủy quyền cho chính mình.");
        // Không lập lùi ngày: ủy quyền chỉ có tác dụng từ lúc lập, phiếu đã duyệt trước đó không đổi người duyệt.
        if (validFrom < today) throw new BusinessRuleException("Ngày bắt đầu ủy quyền không được trước hôm nay.");
        if (validTo < validFrom) throw new BusinessRuleException("Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        if (validTo.DayNumber - validFrom.DayNumber + 1 > MaxDays)
            throw new BusinessRuleException($"Mỗi lần ủy quyền tối đa {MaxDays} ngày – lâu hơn thì đổi người giữ vị trí duyệt.");
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Hãy nhập lý do ủy quyền.");
        if (reason.Trim().Length > TextMaxLength) throw new BusinessRuleException($"Lý do tối đa {TextMaxLength} ký tự.");
        if (note?.Trim().Length > TextMaxLength) throw new BusinessRuleException($"Ghi chú tối đa {TextMaxLength} ký tự.");
        return new ApprovalDelegation
        {
            CompanyId = companyId, DelegatorUserId = delegatorUserId, DelegateUserId = delegateUserId,
            DocType = string.IsNullOrWhiteSpace(docType) ? null : docType.Trim(), ValidFrom = validFrom, ValidTo = validTo, Reason = reason.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
    }

    /// <summary>Có tác dụng vào ngày <paramref name="date"/>: chưa thu hồi và từ ngày ≤ date ≤ đến ngày.</summary>
    /// <param name="date">Ngày xét (ngày Việt Nam).</param>
    public bool IsActiveOn(DateOnly date) => RevokedAt is null && ValidFrom <= date && date <= ValidTo;

    /// <summary>Áp cho loại phiếu: ủy quyền mọi loại hoặc đúng loại.</summary>
    /// <param name="docType">Mã loại phiếu duyệt.</param>
    public bool Covers(string docType) => DocType is null || DocType == docType;

    /// <summary>Thu hồi (về sớm, ủy quyền nhầm). Từ lúc thu hồi người được ủy quyền không duyệt thay nữa; bước đã duyệt thay giữ nguyên.</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="by">Người thu hồi.</param>
    /// <param name="today">Hôm nay (ngày Việt Nam).</param>
    /// <exception cref="BusinessRuleException">Khi đã thu hồi hoặc đã hết hạn.</exception>
    public void Revoke(DateTimeOffset at, Guid by, DateOnly today)
    {
        if (RevokedAt is not null) throw new BusinessRuleException("Ủy quyền này đã thu hồi.");
        if (ValidTo < today) throw new BusinessRuleException("Ủy quyền này đã hết hạn, không cần thu hồi.");
        RevokedAt = at;
        RevokedBy = by;
    }
}
