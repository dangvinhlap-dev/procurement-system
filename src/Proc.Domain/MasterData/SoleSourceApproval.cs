using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;

namespace Proc.Domain.MasterData;

/// <summary>Trạng thái phê duyệt NCC độc quyền (design.md mục 18.9).</summary>
public enum SoleSourceStatus
{
    /// <summary>Nháp – đang soạn giải trình, sửa được.</summary>
    Draft = 1,

    /// <summary>Chờ duyệt – không sửa được; người lập rút về được.</summary>
    Submitted = 2,

    /// <summary>Đã duyệt – có tác dụng từ ngày hiệu lực tới hạn đánh giá lại.</summary>
    Approved = 3,

    /// <summary>Đã kết thúc sớm (hết độc quyền, có NCC khác) – giữ để tra cứu.</summary>
    Ended = 4,
}

/// <summary>Nội dung phiếu NCC độc quyền gửi vào khi lập/sửa.</summary>
/// <param name="SupplierId">NCC độc quyền.</param>
/// <param name="ItemId">Mặt hàng chỉ NCC này cung cấp được.</param>
/// <param name="Reason">Giải trình vì sao chỉ một NCC (bắt buộc).</param>
/// <param name="ValidFrom">Hiệu lực từ ngày.</param>
/// <param name="ReviewDate">Hạn đánh giá lại – qua ngày này phiếu hết tác dụng.</param>
public sealed record SoleSourceData(Guid SupplierId, Guid ItemId, string Reason, DateOnly ValidFrom, DateOnly ReviewDate);

/// <summary>
/// Phê duyệt NCC độc quyền (design.md mục 18.9 – đề xuất Ban CUVT N6): một công ty xác nhận một mặt hàng chỉ mua được của một NCC, kèm
/// giải trình, qua quy trình duyệt riêng (loại <see cref="DocumentTypes.SoleSource"/>) và phải đánh giá lại định kỳ. Thuộc công ty lập vì mỗi
/// công ty là pháp nhân tự chịu trách nhiệm về quyết định mua của mình (CLAUDE.md mục 4). Đã duyệt không sửa: hết hạn thì lập phiếu mới
/// (đánh giá lại), hết độc quyền thì kết thúc sớm kèm lý do.
/// </summary>
[Auditable("Phê duyệt NCC độc quyền", LabelProperty = nameof(No))]
public sealed class SoleSourceApproval : AuditableEntity, ICompanyOwned, IHasDocumentNumber, IApprovalSubject
{
    /// <summary>Độ dài giải trình, lý do tối đa.</summary>
    public const int TextMaxLength = 1000;

    /// <summary>
    /// Hạn đánh giá lại xa nhất tính từ ngày hiệu lực (12 tháng) – đánh giá định kỳ tối thiểu hằng năm theo đề xuất Ban CUVT N6.
    /// </summary>
    public const int MaxReviewMonths = 12;

    private SoleSourceApproval()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu, ví dụ NDQ-TCT-2611-0001.</summary>
    public string No { get; private set; } = string.Empty;

    /// <summary>NCC độc quyền.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Giải trình.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Hiệu lực từ ngày.</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>Hạn đánh giá lại (ngày cuối còn tác dụng).</summary>
    public DateOnly ReviewDate { get; private set; }

    /// <summary>Trạng thái.</summary>
    public SoleSourceStatus Status { get; private set; } = SoleSourceStatus.Draft;

    /// <inheritdoc />
    public int ApprovalRound { get; private set; }

    /// <summary>Lý do từ chối gần nhất.</summary>
    public string? RejectReason { get; private set; }

    /// <summary>Thời điểm duyệt đủ (UTC).</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>Ngày kết thúc sớm.</summary>
    public DateOnly? EndedOn { get; private set; }

    /// <summary>Lý do kết thúc sớm.</summary>
    public string? EndReason { get; private set; }

    /// <summary>Phiếu trước mà phiếu này đánh giá lại (null = lập mới).</summary>
    public Guid? RenewedFromId { get; private set; }

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <inheritdoc />
    public string DocType => DocumentTypes.SoleSource;

    /// <inheritdoc />
    /// <remarks>Không có giá trị tiền: ngưỡng duyệt luôn 0 (design.md mục 6.2).</remarks>
    public decimal ApprovalAmount => 0;

    /// <inheritdoc />
    public bool IsAwaitingApproval => Status == SoleSourceStatus.Submitted;

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> RelatedSupplierIds => [SupplierId];

    /// <summary>
    /// Lập phiếu nháp.
    /// </summary>
    /// <param name="companyId">Công ty lập.</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="data">Nội dung.</param>
    /// <param name="renewedFromId">Phiếu được đánh giá lại (nếu có).</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi nội dung không hợp lệ.</exception>
    public static SoleSourceApproval Create(Guid companyId, string no, SoleSourceData data, Guid? renewedFromId = null)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Thiếu công ty lập phiếu.");
        var approval = new SoleSourceApproval { CompanyId = companyId, No = no, RenewedFromId = renewedFromId };
        approval.Apply(data);
        return approval;
    }

    /// <summary>
    /// Sửa nội dung khi còn nháp.
    /// </summary>
    /// <param name="data">Nội dung mới.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc nội dung không hợp lệ.</exception>
    public void Update(SoleSourceData data)
    {
        EnsureStatus(SoleSourceStatus.Draft, "nháp");
        Apply(data);
    }

    /// <summary>Gửi duyệt: Nháp → Chờ duyệt, xóa lý do từ chối cũ.</summary>
    /// <returns>Số lượt gửi mới.</returns>
    /// <exception cref="BusinessRuleException">Khi không còn nháp.</exception>
    public int Submit()
    {
        EnsureStatus(SoleSourceStatus.Draft, "nháp");
        Status = SoleSourceStatus.Submitted;
        RejectReason = null;
        return ++ApprovalRound;
    }

    /// <inheritdoc />
    public void MarkApproved(DateTimeOffset at)
    {
        EnsureStatus(SoleSourceStatus.Submitted, "chờ duyệt");
        Status = SoleSourceStatus.Approved;
        ApprovedAt = at;
    }

    /// <summary>Bị từ chối hoặc người lập rút lại: về Nháp để sửa, giữ lý do.</summary>
    /// <param name="reason">Lý do (rút lại thì null).</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không chờ duyệt.</exception>
    public void ReturnToDraft(string? reason)
    {
        EnsureStatus(SoleSourceStatus.Submitted, "chờ duyệt");
        Status = SoleSourceStatus.Draft;
        RejectReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>
    /// Kết thúc sớm phiếu đã duyệt (hết độc quyền, đã có NCC khác) – từ hôm nay không còn tác dụng.
    /// </summary>
    /// <param name="today">Ngày hôm nay (giờ Việt Nam).</param>
    /// <param name="reason">Lý do, bắt buộc.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu chưa duyệt hoặc thiếu lý do.</exception>
    public void End(DateOnly today, string? reason)
    {
        EnsureStatus(SoleSourceStatus.Approved, "đã duyệt");
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Hãy nhập lý do kết thúc.");
        if (reason.Trim().Length > TextMaxLength) throw new BusinessRuleException($"Lý do tối đa {TextMaxLength} ký tự.");
        Status = SoleSourceStatus.Ended;
        EndedOn = today;
        EndReason = reason.Trim();
    }

    /// <summary>Có tác dụng vào ngày <paramref name="date"/>: đã duyệt, đã tới ngày hiệu lực và chưa qua hạn đánh giá lại.</summary>
    /// <param name="date">Ngày xét (ngày Việt Nam).</param>
    public bool IsEffectiveOn(DateOnly date) => Status == SoleSourceStatus.Approved && ValidFrom <= date && ReviewDate >= date;

    /// <summary>Đã duyệt nhưng qua hạn đánh giá lại – phải lập phiếu đánh giá lại.</summary>
    /// <param name="today">Ngày hôm nay.</param>
    public bool IsOverdueOn(DateOnly today) => Status == SoleSourceStatus.Approved && ReviewDate < today;

    private void Apply(SoleSourceData data)
    {
        if (data.SupplierId == Guid.Empty) throw new BusinessRuleException("Phải chọn nhà cung cấp.");
        if (data.ItemId == Guid.Empty) throw new BusinessRuleException("Phải chọn mặt hàng.");
        if (string.IsNullOrWhiteSpace(data.Reason)) throw new BusinessRuleException("Hãy ghi giải trình vì sao chỉ mua được của NCC này.");
        var reason = data.Reason.Trim();
        if (reason.Length > TextMaxLength) throw new BusinessRuleException($"Giải trình tối đa {TextMaxLength} ký tự.");
        if (data.ReviewDate <= data.ValidFrom) throw new BusinessRuleException("Hạn đánh giá lại phải sau ngày hiệu lực.");
        // Đánh giá lại tối thiểu hằng năm – design.md mục 18.9.
        if (data.ReviewDate > data.ValidFrom.AddMonths(MaxReviewMonths))
            throw new BusinessRuleException($"Hạn đánh giá lại tối đa {MaxReviewMonths} tháng kể từ ngày hiệu lực.");
        SupplierId = data.SupplierId;
        ItemId = data.ItemId;
        Reason = reason;
        ValidFrom = data.ValidFrom;
        ReviewDate = data.ReviewDate;
    }

    private void EnsureStatus(SoleSourceStatus expected, string name)
    {
        if (Status != expected) throw new BusinessRuleException($"Phiếu {No} không ở trạng thái {name}.");
    }
}
