using Proc.Domain.Common;

namespace Proc.Domain.Documents;

/// <summary>
/// Loại đối tượng nhận ý kiến thẩm định (lát H1 – design.md mục 18.25): phiếu duyệt NCC và giá tháng (GTH), yêu cầu báo giá (RFQ), hợp đồng
/// nguyên tắc, hồ sơ NCC. Dùng lại tên loại chủ của đính kèm để hai bảng nói cùng một ngôn ngữ.
/// </summary>
public static class AppraisalOwnerTypes
{
    /// <summary>Phiếu duyệt NCC và giá tháng (GTH).</summary>
    public const string MonthlySupplierApproval = AttachmentOwnerTypes.MonthlySupplierApproval;

    /// <summary>Yêu cầu báo giá / hồ sơ thầu (RFQ).</summary>
    public const string Rfq = AttachmentOwnerTypes.Rfq;

    /// <summary>Hợp đồng nguyên tắc (CTR).</summary>
    public const string PurchaseContract = AttachmentOwnerTypes.PurchaseContract;

    /// <summary>Hồ sơ nhà cung cấp (danh mục dùng chung).</summary>
    public const string Supplier = AttachmentOwnerTypes.Supplier;

    /// <summary>Mọi loại hợp lệ.</summary>
    public static readonly IReadOnlyList<string> All = [MonthlySupplierApproval, Rfq, PurchaseContract, Supplier];
}

/// <summary>Khía cạnh thẩm định – theo sơ đồ: thành viên chuyên môn thẩm định chất lượng và điều khoản pháp lý.</summary>
public enum AppraisalAspect
{
    /// <summary>Chất lượng hàng, năng lực NCC.</summary>
    Quality = 1,

    /// <summary>Điều khoản pháp lý (hồ sơ pháp lý NCC, điều kiện hợp đồng, hồ sơ thầu).</summary>
    Legal = 2,

    /// <summary>Khía cạnh khác (kỹ thuật, tài chính…).</summary>
    Other = 3,
}

/// <summary>Kết luận thẩm định.</summary>
public enum AppraisalResult
{
    /// <summary>Đạt.</summary>
    Pass = 1,

    /// <summary>Không đạt.</summary>
    Fail = 2,
}

/// <summary>
/// Tình trạng đối tượng chủ lúc ghi/rút ý kiến – dịch vụ đọc từ bảng của chủ rồi truyền vào để quy tắc nằm ở một chỗ.
/// </summary>
/// <param name="CompanyId">Công ty của chủ; null với NCC (danh mục dùng chung).</param>
/// <param name="IsOpen">Chủ còn ở giai đoạn thẩm định (chưa có quyết định cuối cùng).</param>
/// <param name="CreatedBy">Người lập chủ (null với dữ liệu hệ thống tạo).</param>
/// <param name="Label">Tên hiển thị trong thông báo lỗi, ví dụ "Phiếu GTH-TCT-2611-0001".</param>
public sealed record AppraisalOwnerState(Guid? CompanyId, bool IsOpen, Guid? CreatedBy, string Label);

/// <summary>
/// Ý kiến thẩm định của thành viên chuyên môn (lát H1 – design.md mục 18.25): Đạt/Không đạt kèm nội dung trên phiếu GTH, RFQ, hợp đồng
/// nguyên tắc, hồ sơ NCC. <b>Không phải cấp duyệt</b> – theo sơ đồ, thành viên chuyên môn thẩm định trước khi Hội đồng quyết nhưng không biểu
/// quyết: ý kiến không chặn, không thay lượt duyệt; người duyệt thấy trước khi duyệt. Chỉ thêm, không sửa, không xóa: ghi sai thì người ghi
/// rút lại (bắt buộc lý do) khi chủ còn mở, bản ghi giữ để tra cứu.
/// </summary>
[Auditable("Ý kiến thẩm định", LabelProperty = nameof(Content))]
public sealed class AppraisalOpinion : AuditableEntity
{
    /// <summary>Độ dài nội dung tối đa.</summary>
    public const int ContentMaxLength = 2000;

    /// <summary>Độ dài lý do rút tối đa.</summary>
    public const int ReasonMaxLength = 500;

    private AppraisalOpinion()
    {
    }

    /// <summary>Loại chủ – xem <see cref="AppraisalOwnerTypes"/>.</summary>
    public string OwnerType { get; private set; } = string.Empty;

    /// <summary>Id chủ.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Công ty của chủ; null với NCC.</summary>
    public Guid? CompanyId { get; private set; }

    /// <summary>Khía cạnh thẩm định.</summary>
    public AppraisalAspect Aspect { get; private set; }

    /// <summary>Kết luận.</summary>
    public AppraisalResult Result { get; private set; }

    /// <summary>Nội dung ý kiến (bắt buộc).</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Người ghi rút lại ý kiến (không tính vào tổng hợp).</summary>
    public bool IsWithdrawn { get; private set; }

    /// <summary>Lý do rút.</summary>
    public string? WithdrawReason { get; private set; }

    /// <summary>Thời điểm rút (UTC).</summary>
    public DateTimeOffset? WithdrawnAt { get; private set; }

    /// <summary>
    /// Ghi một ý kiến thẩm định.
    /// </summary>
    /// <param name="ownerType">Loại chủ.</param>
    /// <param name="ownerId">Id chủ.</param>
    /// <param name="owner">Tình trạng chủ lúc ghi.</param>
    /// <param name="authorId">Người ghi.</param>
    /// <param name="aspect">Khía cạnh.</param>
    /// <param name="result">Kết luận.</param>
    /// <param name="content">Nội dung.</param>
    /// <returns>Ý kiến mới.</returns>
    /// <exception cref="BusinessRuleException">
    /// Khi loại chủ/khía cạnh/kết luận sai, chủ đã có quyết định cuối cùng, người ghi là người lập chủ, hoặc thiếu/quá dài nội dung.
    /// </exception>
    public static AppraisalOpinion Create(
        string ownerType, Guid ownerId, AppraisalOwnerState owner, Guid authorId, AppraisalAspect aspect, AppraisalResult result, string? content)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!AppraisalOwnerTypes.All.Contains(ownerType)) throw new BusinessRuleException($"Loại đối tượng thẩm định '{ownerType}' không hợp lệ.");
        if (!Enum.IsDefined(aspect)) throw new BusinessRuleException("Khía cạnh thẩm định không hợp lệ.");
        if (!Enum.IsDefined(result)) throw new BusinessRuleException("Kết luận thẩm định phải là Đạt hoặc Không đạt.");
        // Thẩm định để Hội đồng tham khảo trước khi quyết – chủ đã duyệt/hủy/chấm dứt thì ý kiến không còn tác dụng (design.md 18.25, H1).
        if (!owner.IsOpen) throw new BusinessRuleException($"{owner.Label} đã có quyết định cuối cùng – không ghi ý kiến thẩm định được nữa.");
        // Không kiêm nhiệm: người lập không tự thẩm định phiếu của mình (cùng nguyên tắc "người lập không tự duyệt" – mục 18.6).
        if (owner.CreatedBy == authorId) throw new BusinessRuleException("Người lập không tự ghi ý kiến thẩm định cho phiếu của mình.");
        var clean = Clean(content) ?? throw new BusinessRuleException("Hãy nhập nội dung ý kiến thẩm định.");
        if (clean.Length > ContentMaxLength) throw new BusinessRuleException($"Nội dung ý kiến tối đa {ContentMaxLength} ký tự.");
        return new AppraisalOpinion
        {
            OwnerType = ownerType, OwnerId = ownerId, CompanyId = owner.CompanyId, Aspect = aspect, Result = result, Content = clean,
        };
    }

    /// <summary>
    /// Người ghi rút lại ý kiến (ghi nhầm, đổi kết luận thì rút rồi ghi ý kiến mới).
    /// </summary>
    /// <param name="owner">Tình trạng chủ lúc rút.</param>
    /// <param name="userId">Người thao tác.</param>
    /// <param name="reason">Lý do.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi đã rút, không phải người ghi, chủ đã có quyết định cuối cùng, hoặc thiếu lý do.</exception>
    public void Withdraw(AppraisalOwnerState owner, Guid userId, string? reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (IsWithdrawn) throw new BusinessRuleException("Ý kiến thẩm định đã được rút.");
        if (CreatedBy != userId) throw new BusinessRuleException("Chỉ người ghi mới rút được ý kiến thẩm định của mình.");
        // Hội đồng đã quyết dựa trên ý kiến này – rút sau đó làm mất căn cứ của quyết định (KSNB đối chiếu – mục 18.19).
        if (!owner.IsOpen) throw new BusinessRuleException($"{owner.Label} đã có quyết định cuối cùng – không rút ý kiến thẩm định được.");
        var clean = Clean(reason) ?? throw new BusinessRuleException("Hãy nhập lý do rút ý kiến.");
        if (clean.Length > ReasonMaxLength) throw new BusinessRuleException($"Lý do rút tối đa {ReasonMaxLength} ký tự.");
        IsWithdrawn = true;
        WithdrawReason = clean;
        WithdrawnAt = now;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
