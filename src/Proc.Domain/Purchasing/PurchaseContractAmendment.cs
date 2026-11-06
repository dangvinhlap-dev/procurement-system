using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>Trạng thái phụ lục hợp đồng nguyên tắc.</summary>
public enum ContractAmendmentStatus
{
    /// <summary>Nháp – đang soạn, chưa áp vào hợp đồng.</summary>
    Draft = 1,

    /// <summary>Chờ duyệt – không sửa được.</summary>
    Submitted = 2,

    /// <summary>Đã duyệt đủ – nội dung đã áp vào hợp đồng.</summary>
    Approved = 3,

    /// <summary>Đã hủy khi còn nháp.</summary>
    Cancelled = 5,
}

/// <summary>Nội dung phụ lục: giá trị mới của hợp đồng sau phụ lục (điền sẵn bằng giá trị hiện hành, đổi phần cần đổi).</summary>
/// <param name="PaperNo">Số phụ lục trên giấy.</param>
/// <param name="Reason">Lý do lập phụ lục (bắt buộc).</param>
/// <param name="SignDate">Ngày ký phụ lục.</param>
/// <param name="NewValidTo">Ngày hết hiệu lực sau phụ lục (đổi = gia hạn).</param>
/// <param name="NewPriceListId">Bảng giá dùng chung sau phụ lục (đổi = đổi bảng giá).</param>
/// <param name="Terms">Điều khoản, hạn mức công nợ sau phụ lục.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record ContractAmendmentData(
    string? PaperNo, string Reason, DateOnly? SignDate, DateOnly NewValidTo, Guid NewPriceListId, ContractTerms Terms, string? Note);

/// <summary>
/// Phụ lục hợp đồng nguyên tắc (lát B4b – design.md mục 18.7): gia hạn, đổi bảng giá dùng chung, đổi điều khoản/hạn mức công nợ của hợp
/// đồng đang hiệu lực. Mỗi phụ lục phải duyệt (quyết định Q4) theo quy trình loại <see cref="ApprovalTypes.ContractAmendment"/>, xét giá
/// trị dự kiến sau phụ lục; duyệt đủ thì tầng trên áp vào hợp đồng (<see cref="PurchaseContract.ApplyAmendment"/>) trong cùng giao dịch.
/// Vòng đời: Nháp → Chờ duyệt → Đã duyệt; từ chối/rút về Nháp; hủy khi Nháp. Mỗi hợp đồng một lúc chỉ một phụ lục đang mở (nháp/chờ
/// duyệt) để các phụ lục không đè nhau.
/// </summary>
[Auditable("Phụ lục hợp đồng nguyên tắc", LabelProperty = nameof(No))]
public sealed class PurchaseContractAmendment : AuditableEntity, ICompanyOwned, IApprovalSubject, IVersioned
{
    /// <summary>Phiên bản dòng (cột hệ thống xmin của PostgreSQL) – chống ghi đè khi hai người cùng sửa (<see cref="IVersioned"/>).</summary>
    public uint Version { get; private set; }

    private PurchaseContractAmendment()
    {
    }

    /// <summary>Pháp nhân ký (cùng công ty với hợp đồng).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Hợp đồng được sửa đổi.</summary>
    public Guid ContractId { get; private set; }

    /// <summary>Thứ tự phụ lục trong hợp đồng (1, 2, …) – tính cả phụ lục đã hủy để số không lặp lại.</summary>
    public int Seq { get; private set; }

    /// <summary>Số phụ lục: số hợp đồng + PL + thứ tự, ví dụ CTR-H01-2611-0001-PL01.</summary>
    public string No { get; private set; } = string.Empty;

    /// <summary>Số phụ lục trên giấy.</summary>
    public string? PaperNo { get; private set; }

    /// <summary>Lý do lập phụ lục.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Ngày ký phụ lục.</summary>
    public DateOnly? SignDate { get; private set; }

    /// <summary>Ngày hết hiệu lực của hợp đồng lúc lập/sửa phụ lục (để so sánh và ghi lịch sử).</summary>
    public DateOnly OldValidTo { get; private set; }

    /// <summary>Ngày hết hiệu lực sau phụ lục.</summary>
    public DateOnly NewValidTo { get; private set; }

    /// <summary>Bảng giá của hợp đồng lúc lập/sửa phụ lục.</summary>
    public Guid OldPriceListId { get; private set; }

    /// <summary>Bảng giá sau phụ lục.</summary>
    public Guid NewPriceListId { get; private set; }

    /// <summary>Cơ chế điều chỉnh giá sau phụ lục.</summary>
    public string? PriceAdjustmentTerms { get; private set; }

    /// <summary>Thanh toán sau phụ lục.</summary>
    public string? PaymentTerms { get; private set; }

    /// <summary>Hạn mức công nợ (VND) sau phụ lục – numeric(19,2).</summary>
    public decimal? CreditLimit { get; private set; }

    /// <summary>Giao hàng sau phụ lục.</summary>
    public string? DeliveryTerms { get; private set; }

    /// <summary>Chiết khấu sau phụ lục.</summary>
    public string? DiscountTerms { get; private set; }

    /// <summary>Tài trợ sau phụ lục.</summary>
    public string? SponsorshipTerms { get; private set; }

    /// <summary>Chế tài sau phụ lục.</summary>
    public string? PenaltyTerms { get; private set; }

    /// <summary>Điều khoản khác sau phụ lục.</summary>
    public string? OtherTerms { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Giá trị dự kiến của hợp đồng sau phụ lục (theo bảng giá mới nếu đổi) – numeric(19,2); tầng trên tính lúc lưu và lúc gửi, dùng xét
    /// ngưỡng duyệt.
    /// </summary>
    public decimal ExpectedAmount { get; private set; }

    /// <summary>Tóm tắt thay đổi, ví dụ "Gia hạn đến 30/06/2027; Đổi bảng giá PRL-1 → PRL-2" – chốt lại khi duyệt xong để tra cứu.</summary>
    public string ChangeSummary { get; private set; } = string.Empty;

    /// <summary>Trạng thái.</summary>
    public ContractAmendmentStatus Status { get; private set; } = ContractAmendmentStatus.Draft;

    /// <inheritdoc />
    public int ApprovalRound { get; private set; }

    /// <summary>Lý do từ chối gần nhất (xóa khi gửi lại).</summary>
    public string? RejectReason { get; private set; }

    /// <summary>Thời điểm duyệt đủ – áp vào hợp đồng (UTC).</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <inheritdoc />
    public string DocType => ApprovalTypes.ContractAmendment;

    /// <inheritdoc />
    public decimal ApprovalAmount => ExpectedAmount;

    /// <inheritdoc />
    public bool IsAwaitingApproval => Status == ContractAmendmentStatus.Submitted;

    /// <summary>Phụ lục còn mở (nháp hoặc chờ duyệt) – chặn lập phụ lục khác, chặn chấm dứt hợp đồng.</summary>
    public bool IsOpen => Status is ContractAmendmentStatus.Draft or ContractAmendmentStatus.Submitted;

    /// <summary>Điều khoản sau phụ lục.</summary>
    public ContractTerms NewTerms =>
        new(PriceAdjustmentTerms, PaymentTerms, CreditLimit, DeliveryTerms, DiscountTerms, SponsorshipTerms, PenaltyTerms, OtherTerms);

    /// <summary>Lập phụ lục nháp cho hợp đồng đang hiệu lực.</summary>
    /// <param name="contract">Hợp đồng (đang hiệu lực).</param>
    /// <param name="seq">Thứ tự phụ lục (số phụ lục đã có của hợp đồng + 1).</param>
    /// <param name="data">Nội dung.</param>
    /// <param name="today">Hôm nay (giờ Việt Nam) – ngày hết hiệu lực mới không được trước hôm nay.</param>
    /// <exception cref="BusinessRuleException">Khi hợp đồng không hiệu lực, thiếu lý do, không đổi gì, ngày/hạn mức sai.</exception>
    public static PurchaseContractAmendment Create(PurchaseContract contract, int seq, ContractAmendmentData data, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (seq < 1) throw new BusinessRuleException("Thứ tự phụ lục phải từ 1.");
        var amendment = new PurchaseContractAmendment
        {
            CompanyId = contract.CompanyId, ContractId = contract.Id, Seq = seq, No = $"{contract.No}-PL{seq:00}",
        };
        amendment.Update(contract, data, today);
        return amendment;
    }

    /// <summary>Sửa phụ lục nháp; chụp lại giá trị hiện hành của hợp đồng để so sánh.</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, hợp đồng không hiệu lực, thiếu lý do, không đổi gì, ngày/hạn mức sai.</exception>
    public void Update(PurchaseContract contract, ContractAmendmentData data, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(data);
        EnsureDraft();
        if (contract.Id != ContractId) throw new BusinessRuleException($"Phụ lục {No} không thuộc hợp đồng {contract.No}.");
        if (contract.Status != ContractStatus.Active)
            throw new BusinessRuleException($"Hợp đồng {contract.No} không ở trạng thái hiệu lực – chỉ lập phụ lục cho hợp đồng đang hiệu lực.");
        var reason = Clean(data.Reason) ?? throw new BusinessRuleException("Phụ lục phải ghi lý do.");
        if (data.NewPriceListId == Guid.Empty) throw new BusinessRuleException("Hãy chọn bảng giá dùng chung sau phụ lục.");
        if (data.NewValidTo < contract.ValidFrom) throw new BusinessRuleException("Ngày hết hiệu lực mới không được trước ngày bắt đầu hiệu lực.");
        // Rút ngắn về trước hôm nay là kết thúc hợp đồng – dùng Chấm dứt (có ngày và lý do) cho rõ, không làm qua phụ lục.
        if (data.NewValidTo != contract.ValidTo && data.NewValidTo < today)
            throw new BusinessRuleException("Ngày hết hiệu lực mới không được trước hôm nay – muốn kết thúc sớm thì chấm dứt hợp đồng.");
        var terms = data.Terms.Normalized();
        terms.EnsureValid();
        PaperNo = Clean(data.PaperNo);
        Reason = reason;
        SignDate = data.SignDate;
        OldValidTo = contract.ValidTo;
        NewValidTo = data.NewValidTo;
        OldPriceListId = contract.PriceListId;
        NewPriceListId = data.NewPriceListId;
        PriceAdjustmentTerms = terms.PriceAdjustmentTerms;
        PaymentTerms = terms.PaymentTerms;
        CreditLimit = terms.CreditLimit;
        DeliveryTerms = terms.DeliveryTerms;
        DiscountTerms = terms.DiscountTerms;
        SponsorshipTerms = terms.SponsorshipTerms;
        PenaltyTerms = terms.PenaltyTerms;
        OtherTerms = terms.OtherTerms;
        Note = Clean(data.Note);
        EnsureChanges(contract);
    }

    /// <summary>
    /// Phụ lục phải đổi ít nhất một trong: ngày hết hiệu lực, bảng giá, điều khoản/hạn mức – so với hợp đồng hiện hành.
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi không đổi gì.</exception>
    public void EnsureChanges(PurchaseContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (NewValidTo == contract.ValidTo && NewPriceListId == contract.PriceListId && NewTerms.DifferencesFrom(contract.Terms).Count == 0)
            throw new BusinessRuleException("Phụ lục chưa thay đổi gì so với hợp đồng (hiệu lực, bảng giá, điều khoản).");
    }

    /// <summary>Ghi giá trị dự kiến sau phụ lục và tóm tắt thay đổi (tầng trên tính từ bảng giá mới, số bảng giá).</summary>
    /// <param name="expectedAmount">Giá trị dự kiến sau phụ lục.</param>
    /// <param name="changeSummary">Tóm tắt thay đổi.</param>
    /// <exception cref="BusinessRuleException">Khi phụ lục đã duyệt/hủy.</exception>
    public void SetEvaluation(decimal expectedAmount, string changeSummary)
    {
        if (!IsOpen) throw new BusinessRuleException($"Phụ lục {No} đã kết thúc – không tính lại được.");
        ExpectedAmount = expectedAmount;
        ChangeSummary = changeSummary.Trim();
    }

    /// <inheritdoc />
    /// <exception cref="BusinessRuleException">Khi không còn nháp.</exception>
    public int Submit()
    {
        EnsureDraft();
        Status = ContractAmendmentStatus.Submitted;
        RejectReason = null;
        return ++ApprovalRound;
    }

    /// <inheritdoc />
    /// <exception cref="BusinessRuleException">Khi không chờ duyệt.</exception>
    public void MarkApproved(DateTimeOffset at)
    {
        EnsureSubmitted();
        Status = ContractAmendmentStatus.Approved;
        ApprovedAt = at;
    }

    /// <inheritdoc />
    /// <exception cref="BusinessRuleException">Khi không chờ duyệt.</exception>
    public void ReturnToDraft(string? reason)
    {
        EnsureSubmitted();
        Status = ContractAmendmentStatus.Draft;
        RejectReason = Clean(reason);
    }

    /// <summary>Hủy phụ lục nháp (giữ số và lịch sử).</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp.</exception>
    public void Cancel()
    {
        EnsureDraft();
        Status = ContractAmendmentStatus.Cancelled;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureDraft()
    {
        if (Status != ContractAmendmentStatus.Draft) throw new BusinessRuleException($"Phụ lục {No} không còn ở trạng thái nháp – không sửa được.");
    }

    private void EnsureSubmitted()
    {
        if (Status != ContractAmendmentStatus.Submitted) throw new BusinessRuleException($"Phụ lục {No} không ở trạng thái chờ duyệt.");
    }
}
