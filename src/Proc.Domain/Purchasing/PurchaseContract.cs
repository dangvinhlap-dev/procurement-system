using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;

namespace Proc.Domain.Purchasing;

/// <summary>Trạng thái hợp đồng nguyên tắc.</summary>
public enum ContractStatus
{
    /// <summary>Nháp – đang soạn.</summary>
    Draft = 1,

    /// <summary>Chờ duyệt – đã gửi, không sửa được.</summary>
    Submitted = 2,

    /// <summary>Hiệu lực – đã duyệt; áp dụng trong khoảng hiệu lực.</summary>
    Active = 3,

    /// <summary>Chấm dứt (thanh lý trước hạn) – giữ để tra cứu.</summary>
    Terminated = 4,

    /// <summary>Đã hủy khi còn nháp.</summary>
    Cancelled = 5,
}

/// <summary>
/// Đầu và điều khoản hợp đồng nguyên tắc (các nội dung bắt buộc khi đàm phán – đề xuất Ban CUVT Bước 11, design.md mục 18.7).
/// </summary>
/// <param name="SupplierId">Nhà cung cấp.</param>
/// <param name="PriceListId">Bảng giá NCC dùng chung (cùng NCC; bảng toàn tập đoàn hoặc bảng của công ty).</param>
/// <param name="ContractNo">Số hợp đồng trên giấy (do hai bên ký).</param>
/// <param name="Title">Tên hợp đồng.</param>
/// <param name="SignDate">Ngày ký.</param>
/// <param name="ValidFrom">Hiệu lực từ ngày.</param>
/// <param name="ValidTo">Hiệu lực đến ngày (bắt buộc – hợp đồng có thời hạn).</param>
/// <param name="PriceAdjustmentTerms">Cơ chế điều chỉnh giá.</param>
/// <param name="PaymentTerms">Phương thức, thời hạn thanh toán.</param>
/// <param name="CreditLimit">Hạn mức công nợ (VND, không bắt buộc).</param>
/// <param name="DeliveryTerms">Thời gian, địa điểm giao hàng.</param>
/// <param name="DiscountTerms">Chiết khấu thương mại, theo sản lượng, doanh số.</param>
/// <param name="SponsorshipTerms">Tài trợ, hỗ trợ, khuyến mại.</param>
/// <param name="PenaltyTerms">Chế tài chậm giao, giao sai, hàng lỗi.</param>
/// <param name="OtherTerms">Bảo mật, xung đột lợi ích, chấm dứt, gia hạn, tranh chấp…</param>
/// <param name="Note">Ghi chú.</param>
public sealed record ContractHeader(
    Guid SupplierId, Guid PriceListId, string? ContractNo, string Title, DateOnly? SignDate, DateOnly ValidFrom, DateOnly ValidTo,
    string? PriceAdjustmentTerms, string? PaymentTerms, decimal? CreditLimit, string? DeliveryTerms, string? DiscountTerms,
    string? SponsorshipTerms, string? PenaltyTerms, string? OtherTerms, string? Note);

/// <summary>
/// Điều khoản thương mại và hạn mức công nợ của hợp đồng – phần phụ lục được phép đổi (design.md mục 18.7). So sánh bằng giá trị sau
/// <see cref="Normalized"/> (bỏ khoảng trắng thừa, chuỗi rỗng = null).
/// </summary>
public sealed record ContractTerms(
    string? PriceAdjustmentTerms, string? PaymentTerms, decimal? CreditLimit, string? DeliveryTerms, string? DiscountTerms, string? SponsorshipTerms,
    string? PenaltyTerms, string? OtherTerms)
{
    /// <summary>Bản đã chuẩn hóa để lưu và so sánh.</summary>
    public ContractTerms Normalized() =>
        new(Clean(PriceAdjustmentTerms), Clean(PaymentTerms), CreditLimit, Clean(DeliveryTerms), Clean(DiscountTerms), Clean(SponsorshipTerms),
            Clean(PenaltyTerms), Clean(OtherTerms));

    /// <summary>Tên các điều khoản khác nhau giữa hai bản (đã chuẩn hóa), ví dụ ["thanh toán", "hạn mức công nợ"].</summary>
    /// <param name="other">Bản so sánh.</param>
    public IReadOnlyList<string> DifferencesFrom(ContractTerms other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var (a, b) = (Normalized(), other.Normalized());
        var names = new List<string>();
        if (a.PriceAdjustmentTerms != b.PriceAdjustmentTerms) names.Add("điều chỉnh giá");
        if (a.PaymentTerms != b.PaymentTerms) names.Add("thanh toán");
        if (a.CreditLimit != b.CreditLimit) names.Add("hạn mức công nợ");
        if (a.DeliveryTerms != b.DeliveryTerms) names.Add("giao hàng");
        if (a.DiscountTerms != b.DiscountTerms) names.Add("chiết khấu");
        if (a.SponsorshipTerms != b.SponsorshipTerms) names.Add("tài trợ");
        if (a.PenaltyTerms != b.PenaltyTerms) names.Add("chế tài");
        if (a.OtherTerms != b.OtherTerms) names.Add("điều khoản khác");
        return names;
    }

    /// <summary>Hạn mức công nợ hợp lệ: ≥ 0, tối đa 2 số lẻ (tiền numeric(19,2)).</summary>
    /// <exception cref="BusinessRuleException">Khi hạn mức sai.</exception>
    public void EnsureValid()
    {
        if (CreditLimit is { } limit && (limit < 0 || decimal.Round(limit, 2) != limit))
            throw new BusinessRuleException("Hạn mức công nợ phải ≥ 0, tối đa 2 chữ số thập phân.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Sản lượng dự kiến một mặt hàng.</summary>
/// <param name="Id">Id dòng đang có (null = dòng mới).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT (phải có giá trong bảng giá gắn với hợp đồng).</param>
/// <param name="ExpectedQty">SL dự kiến trong thời hạn hợp đồng.</param>
/// <param name="MinCommitQty">SL cam kết mua tối thiểu (null = không cam kết – đề xuất Bước 13: không mặc nhiên cam kết).</param>
/// <param name="Note">Ghi chú.</param>
public sealed record ContractLineData(Guid? Id, Guid ItemId, Guid UomId, decimal ExpectedQty, decimal? MinCommitQty, string? Note);

/// <summary>Giá của dòng lấy từ bảng giá gắn với hợp đồng (bậc thấp nhất của mặt hàng × ĐVT).</summary>
/// <param name="UnitPrice">Đơn giá trước CK, VAT.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
public sealed record ContractLinePrice(decimal UnitPrice, decimal DiscountPct);

/// <summary>
/// Hợp đồng nguyên tắc theo phương án 2 của đề xuất Ban CUVT (quyết định Q4 – design.md mục 18.7): mỗi pháp nhân (công ty) ký một hợp
/// đồng riêng với NCC, dùng chung bảng giá và điều kiện đã đàm phán. Ghi điều khoản thương mại, sản lượng dự kiến (không bắt buộc cam kết),
/// bộ phận được phép đặt hàng. Vòng đời: Nháp → Chờ duyệt → Hiệu lực → Chấm dứt; từ chối/rút về Nháp; hủy khi Nháp.
/// Duyệt qua quy trình dùng chung loại CTR, xét giá trị dự kiến (design.md mục 18.6).
/// </summary>
[Auditable("Hợp đồng nguyên tắc", LabelProperty = nameof(No))]
public sealed class PurchaseContract : AuditableEntity, ICompanyOwned, IHasDocumentNumber, IApprovalSubject
{
    private readonly List<PurchaseContractLine> _lines = [];
    private readonly List<PurchaseContractDepartment> _departments = [];
    private readonly List<PurchaseContractRebate> _rebates = [];

    private PurchaseContract()
    {
    }

    /// <summary>Pháp nhân ký hợp đồng.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số hợp đồng trong hệ thống (CTR-…).</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Nhà cung cấp.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Bảng giá NCC dùng chung.</summary>
    public Guid PriceListId { get; private set; }

    /// <summary>Số hợp đồng trên giấy.</summary>
    public string? ContractNo { get; private set; }

    /// <summary>Tên hợp đồng.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Ngày ký.</summary>
    public DateOnly? SignDate { get; private set; }

    /// <summary>Hiệu lực từ ngày.</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>Hiệu lực đến ngày.</summary>
    public DateOnly ValidTo { get; private set; }

    /// <summary>Cơ chế điều chỉnh giá.</summary>
    public string? PriceAdjustmentTerms { get; private set; }

    /// <summary>Thanh toán.</summary>
    public string? PaymentTerms { get; private set; }

    /// <summary>Hạn mức công nợ (VND) – numeric(19,2).</summary>
    public decimal? CreditLimit { get; private set; }

    /// <summary>Giao hàng.</summary>
    public string? DeliveryTerms { get; private set; }

    /// <summary>Chiết khấu.</summary>
    public string? DiscountTerms { get; private set; }

    /// <summary>Tài trợ, hỗ trợ.</summary>
    public string? SponsorshipTerms { get; private set; }

    /// <summary>Chế tài.</summary>
    public string? PenaltyTerms { get; private set; }

    /// <summary>Điều khoản khác.</summary>
    public string? OtherTerms { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public ContractStatus Status { get; private set; } = ContractStatus.Draft;

    /// <summary>Thời điểm duyệt xong – có hiệu lực (UTC).</summary>
    public DateTimeOffset? ActivatedAt { get; private set; }

    /// <summary>Ngày chấm dứt trước hạn.</summary>
    public DateOnly? TerminatedOn { get; private set; }

    /// <summary>Lý do chấm dứt.</summary>
    public string? TerminationReason { get; private set; }

    /// <inheritdoc />
    public int ApprovalRound { get; private set; }

    /// <summary>Lý do từ chối gần nhất (xóa khi gửi lại).</summary>
    public string? RejectReason { get; private set; }

    /// <inheritdoc />
    public string DocType => DocumentTypes.Contract;

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> RelatedSupplierIds => [SupplierId];

    /// <inheritdoc />
    /// <remarks>Giá trị dự kiến = Σ thành tiền dự kiến các dòng (xem <see cref="PurchaseContractLine.ExpectedAmount"/>).</remarks>
    public decimal ApprovalAmount => ExpectedAmount;

    /// <inheritdoc />
    public bool IsAwaitingApproval => Status == ContractStatus.Submitted;

    /// <summary>Giá trị dự kiến của hợp đồng (sau CK, trước VAT).</summary>
    public decimal ExpectedAmount => _lines.Sum(l => l.ExpectedAmount);

    /// <summary>Điều khoản, hạn mức công nợ hiện hành (phụ lục so sánh và thay phần này).</summary>
    public ContractTerms Terms =>
        new(PriceAdjustmentTerms, PaymentTerms, CreditLimit, DeliveryTerms, DiscountTerms, SponsorshipTerms, PenaltyTerms, OtherTerms);

    /// <summary>Sản lượng dự kiến theo mặt hàng.</summary>
    public IReadOnlyList<PurchaseContractLine> Lines => _lines;

    /// <summary>Bộ phận được phép đặt hàng theo hợp đồng (trống = mọi bộ phận của công ty).</summary>
    public IReadOnlyList<PurchaseContractDepartment> Departments => _departments;

    /// <summary>Cam kết chiết khấu doanh số/tài trợ (lát F4b – design.md mục 18.21).</summary>
    public IReadOnlyList<PurchaseContractRebate> Rebates => _rebates;

    /// <summary>Ngày kết thúc thực tế: ngày chấm dứt nếu đã chấm dứt, không thì ngày hết hiệu lực – giới hạn các kỳ quyết toán chiết khấu.</summary>
    public DateOnly EndDate => TerminatedOn ?? ValidTo;

    /// <summary>Lập hợp đồng nháp.</summary>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu hợp đồng sai.</exception>
    public static PurchaseContract Create(Guid companyId, string no, ContractHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Hợp đồng phải thuộc một pháp nhân (công ty).");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Hợp đồng phải có số.");
        var contract = new PurchaseContract { CompanyId = companyId, No = no };
        contract.Update(header);
        return contract;
    }

    /// <summary>Sửa đầu, điều khoản (nháp).</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, thiếu tên/NCC/bảng giá, hiệu lực sai, hạn mức âm.</exception>
    public void Update(ContractHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (header.SupplierId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhà cung cấp.");
        if (header.PriceListId == Guid.Empty) throw new BusinessRuleException("Hãy chọn bảng giá NCC dùng chung cho hợp đồng.");
        if (string.IsNullOrWhiteSpace(header.Title)) throw new BusinessRuleException("Hãy nhập tên hợp đồng.");
        if (header.ValidTo < header.ValidFrom) throw new BusinessRuleException("Ngày hết hiệu lực không được trước ngày bắt đầu.");
        ToTerms(header).EnsureValid();
        if (_lines.Count > 0 && header.SupplierId != SupplierId) throw new BusinessRuleException("Đổi NCC thì hãy xóa hết dòng sản lượng trước.");
        SupplierId = header.SupplierId;
        PriceListId = header.PriceListId;
        ContractNo = Clean(header.ContractNo);
        Title = header.Title.Trim();
        SignDate = header.SignDate;
        ValidFrom = header.ValidFrom;
        ValidTo = header.ValidTo;
        SetTerms(ToTerms(header));
        Note = Clean(header.Note);
    }

    /// <summary>
    /// Thay toàn bộ dòng sản lượng dự kiến (nháp). Giá mỗi dòng lấy từ bảng giá gắn với hợp đồng (tầng trên tra và truyền vào) – hợp đồng
    /// dùng chung bảng giá, không ghi giá riêng.
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, trùng mặt hàng × ĐVT, SL sai.</exception>
    public void SetLines(IReadOnlyList<(ContractLineData Data, Item Item, ContractLinePrice Price)> lines)
    {
        EnsureDraft();
        if (lines.GroupBy(l => (l.Data.ItemId, l.Data.UomId)).Any(g => g.Count() > 1))
            throw new BusinessRuleException("Mỗi mặt hàng × ĐVT chỉ một dòng sản lượng.");
        var keep = new List<PurchaseContractLine>();
        var lineNo = 0;
        foreach (var (data, item, price) in lines)
        {
            lineNo++;
            var line = data.Id is { } id
                ? _lines.FirstOrDefault(l => l.Id == id) ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại hợp đồng.")
                : AddLine();
            line.Set(lineNo, data, item, price);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
    }

    /// <summary>Thay danh sách bộ phận được phép đặt hàng (nháp); trống = mọi bộ phận (G5 đề xuất Ban CUVT).</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc trùng bộ phận.</exception>
    public void SetDepartments(IReadOnlyList<Guid> departmentIds)
    {
        EnsureDraft();
        if (departmentIds.Distinct().Count() != departmentIds.Count) throw new BusinessRuleException("Mỗi bộ phận chỉ chọn một lần.");
        _departments.RemoveAll(d => !departmentIds.Contains(d.DepartmentId));
        foreach (var id in departmentIds.Where(id => _departments.All(d => d.DepartmentId != id))) _departments.Add(new PurchaseContractDepartment(Id, id));
    }

    /// <summary>
    /// Thay toàn bộ cam kết chiết khấu doanh số/tài trợ (nháp) – là nội dung hợp đồng nên duyệt cùng hợp đồng (P10 đề xuất Ban CUVT).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không tìm thấy cam kết cần sửa hoặc dữ liệu sai.</exception>
    public void SetRebates(IReadOnlyList<ContractRebateData> rebates)
    {
        ArgumentNullException.ThrowIfNull(rebates);
        EnsureDraft();
        var keep = new List<PurchaseContractRebate>();
        var lineNo = 0;
        foreach (var data in rebates)
        {
            lineNo++;
            PurchaseContractRebate rebate;
            if (data.Id is { } id)
            {
                rebate = _rebates.FirstOrDefault(r => r.Id == id)
                    ?? throw new BusinessRuleException($"Cam kết {lineNo}: không tìm thấy cam kết cần sửa – hãy tải lại hợp đồng.");
            }
            else
            {
                rebate = new PurchaseContractRebate(Id);
                _rebates.Add(rebate);
            }

            rebate.Set(lineNo, data);
            keep.Add(rebate);
        }

        _rebates.RemoveAll(r => !keep.Contains(r));
    }

    /// <summary>Gửi duyệt: Nháp → Chờ duyệt, tăng lượt, xóa lý do từ chối cũ.</summary>
    /// <returns>Số lượt gửi mới.</returns>
    /// <exception cref="BusinessRuleException">Khi không còn nháp.</exception>
    public int Submit()
    {
        EnsureDraft();
        Status = ContractStatus.Submitted;
        RejectReason = null;
        return ++ApprovalRound;
    }

    /// <summary>Duyệt đủ các cấp: Chờ duyệt → Hiệu lực.</summary>
    /// <exception cref="BusinessRuleException">Khi không chờ duyệt.</exception>
    public void MarkApproved(DateTimeOffset at)
    {
        EnsureSubmitted();
        Status = ContractStatus.Active;
        ActivatedAt = at;
    }

    /// <summary>Bị từ chối hoặc người lập rút lại: về Nháp, giữ lý do.</summary>
    /// <exception cref="BusinessRuleException">Khi không chờ duyệt.</exception>
    public void ReturnToDraft(string? reason)
    {
        EnsureSubmitted();
        Status = ContractStatus.Draft;
        RejectReason = Clean(reason);
    }

    /// <summary>Chấm dứt (thanh lý) hợp đồng đang hiệu lực trước hạn: bắt buộc ngày và lý do.</summary>
    /// <param name="on">Ngày chấm dứt (trong khoảng hiệu lực).</param>
    /// <param name="reason">Lý do.</param>
    /// <exception cref="BusinessRuleException">Khi không hiệu lực, thiếu lý do hoặc ngày ngoài khoảng hiệu lực.</exception>
    public void Terminate(DateOnly on, string? reason)
    {
        if (Status != ContractStatus.Active) throw new BusinessRuleException($"Hợp đồng {No} không ở trạng thái hiệu lực.");
        var why = Clean(reason) ?? throw new BusinessRuleException("Chấm dứt hợp đồng phải ghi lý do.");
        if (on < ValidFrom || on > ValidTo) throw new BusinessRuleException("Ngày chấm dứt phải trong khoảng hiệu lực của hợp đồng.");
        Status = ContractStatus.Terminated;
        TerminatedOn = on;
        TerminationReason = why;
    }

    /// <summary>
    /// Áp phụ lục đã duyệt đủ vào hợp đồng đang hiệu lực (lát B4b – design.md mục 18.7): ngày hết hiệu lực, bảng giá, điều khoản. Đổi bảng
    /// giá thì giá mọi dòng sản lượng lấy lại từ bảng giá mới (tầng trên tra theo Id dòng) – hợp đồng không ghi giá riêng ngoài bảng giá.
    /// </summary>
    /// <param name="amendment">Phụ lục đã duyệt của chính hợp đồng này.</param>
    /// <param name="linePrices">Giá mới theo Id dòng (chỉ cần khi đổi bảng giá).</param>
    /// <exception cref="BusinessRuleException">Khi hợp đồng không hiệu lực, phụ lục chưa duyệt/không thuộc hợp đồng, thiếu giá dòng.</exception>
    public void ApplyAmendment(PurchaseContractAmendment amendment, IReadOnlyDictionary<Guid, ContractLinePrice> linePrices)
    {
        ArgumentNullException.ThrowIfNull(amendment);
        ArgumentNullException.ThrowIfNull(linePrices);
        if (amendment.ContractId != Id) throw new BusinessRuleException($"Phụ lục {amendment.No} không thuộc hợp đồng {No}.");
        if (amendment.Status != ContractAmendmentStatus.Approved) throw new BusinessRuleException($"Phụ lục {amendment.No} chưa được duyệt.");
        if (Status != ContractStatus.Active) throw new BusinessRuleException($"Hợp đồng {No} không ở trạng thái hiệu lực.");
        if (amendment.NewValidTo < ValidFrom) throw new BusinessRuleException("Ngày hết hiệu lực mới không được trước ngày bắt đầu hiệu lực.");
        if (amendment.NewPriceListId != PriceListId)
        {
            foreach (var line in _lines)
                line.Reprice(linePrices.GetValueOrDefault(line.Id)
                    ?? throw new BusinessRuleException($"Dòng {line.LineNo}: mặt hàng không có trong bảng giá mới của phụ lục {amendment.No}."));
            PriceListId = amendment.NewPriceListId;
        }

        ValidTo = amendment.NewValidTo;
        SetTerms(amendment.NewTerms);
    }

    /// <summary>Hủy hợp đồng nháp (giữ số và lịch sử).</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp.</exception>
    public void Cancel()
    {
        EnsureDraft();
        Status = ContractStatus.Cancelled;
    }

    /// <summary>Hợp đồng có hiệu lực vào ngày <paramref name="date"/> không (đã duyệt, trong khoảng hiệu lực).</summary>
    public bool AppliesOn(DateOnly date) => Status == ContractStatus.Active && ValidFrom <= date && ValidTo >= date;

    /// <summary>Khoảng hiệu lực có chồng lên khoảng [<paramref name="from"/>, <paramref name="to"/>] không.</summary>
    public bool Overlaps(DateOnly from, DateOnly to) => ValidFrom <= to && from <= ValidTo;

    /// <summary>Số ngày còn tới ngày hết hiệu lực (âm = đã quá hạn).</summary>
    public int DaysToExpiry(DateOnly today) => ValidTo.DayNumber - today.DayNumber;

    /// <summary>
    /// Hợp đồng hiệu lực sắp hết hạn: còn 0–<paramref name="days"/> ngày – cảnh báo để kịp đàm phán, gia hạn. Ví dụ hết hạn 30/11, hôm nay
    /// 01/11, ngưỡng 30 → còn 29 ngày → cảnh báo.
    /// </summary>
    public bool ExpiresWithin(DateOnly today, int days) => Status == ContractStatus.Active && DaysToExpiry(today) >= 0 && DaysToExpiry(today) <= days;

    private PurchaseContractLine AddLine()
    {
        var line = new PurchaseContractLine(Id);
        _lines.Add(line);
        return line;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ContractTerms ToTerms(ContractHeader h) =>
        new(h.PriceAdjustmentTerms, h.PaymentTerms, h.CreditLimit, h.DeliveryTerms, h.DiscountTerms, h.SponsorshipTerms, h.PenaltyTerms, h.OtherTerms);

    private void SetTerms(ContractTerms terms)
    {
        var t = terms.Normalized();
        PriceAdjustmentTerms = t.PriceAdjustmentTerms;
        PaymentTerms = t.PaymentTerms;
        CreditLimit = t.CreditLimit;
        DeliveryTerms = t.DeliveryTerms;
        DiscountTerms = t.DiscountTerms;
        SponsorshipTerms = t.SponsorshipTerms;
        PenaltyTerms = t.PenaltyTerms;
        OtherTerms = t.OtherTerms;
    }

    private void EnsureDraft()
    {
        if (Status != ContractStatus.Draft) throw new BusinessRuleException($"Hợp đồng {No} không còn ở trạng thái nháp – không sửa được.");
    }

    private void EnsureSubmitted()
    {
        if (Status != ContractStatus.Submitted) throw new BusinessRuleException($"Hợp đồng {No} không ở trạng thái chờ duyệt.");
    }
}

/// <summary>Sản lượng dự kiến một mặt hàng trong hợp đồng nguyên tắc.</summary>
[Auditable("Dòng hợp đồng nguyên tắc")]
public sealed class PurchaseContractLine : Entity
{
    private PurchaseContractLine()
    {
    }

    internal PurchaseContractLine(Guid contractId) => ContractId = contractId;

    /// <summary>Hợp đồng.</summary>
    public Guid ContractId { get; private set; }

    /// <summary>Số thứ tự dòng.</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT.</summary>
    public Guid UomId { get; private set; }

    /// <summary>SL dự kiến – numeric(18,4).</summary>
    public decimal ExpectedQty { get; private set; }

    /// <summary>SL cam kết tối thiểu (null = không cam kết).</summary>
    public decimal? MinCommitQty { get; private set; }

    /// <summary>Đơn giá theo bảng giá lúc lưu – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>% chiết khấu theo bảng giá.</summary>
    public decimal DiscountPct { get; private set; }

    /// <summary>
    /// Thành tiền dự kiến = SL × đơn giá × (100 − %CK)/100, làm tròn 2 số. Ví dụ 1.000 cái × 50.000 × 95% = 47.500.000.
    /// </summary>
    public decimal ExpectedAmount { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    internal void Set(int lineNo, ContractLineData data, Item item, ContractLinePrice price)
    {
        var prefix = $"Dòng {lineNo}";
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        if (item.BaseUomId != data.UomId && item.Conversions.All(c => c.UomId != data.UomId))
            throw new BusinessRuleException($"{prefix}: ĐVT không thuộc mặt hàng {item.Code}.");
        if (data.ExpectedQty <= 0 || decimal.Round(data.ExpectedQty, 4) != data.ExpectedQty || data.ExpectedQty >= 100_000_000_000_000m)
            throw new BusinessRuleException($"{prefix}: SL dự kiến phải > 0, tối đa 4 chữ số thập phân.");
        if (data.MinCommitQty is { } min && (min < 0 || decimal.Round(min, 4) != min || min > data.ExpectedQty))
            throw new BusinessRuleException($"{prefix}: SL cam kết tối thiểu phải từ 0 đến SL dự kiến.");
        LineNo = lineNo;
        ItemId = data.ItemId;
        UomId = data.UomId;
        ExpectedQty = data.ExpectedQty;
        MinCommitQty = data.MinCommitQty;
        Reprice(price);
        Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
    }

    /// <summary>Lấy giá mới (bảng giá của phụ lục), tính lại thành tiền dự kiến – công thức ở <see cref="ExpectedAmount"/>.</summary>
    internal void Reprice(ContractLinePrice price)
    {
        UnitPrice = price.UnitPrice;
        DiscountPct = price.DiscountPct;
        ExpectedAmount = AmountOf(ExpectedQty, price);
    }

    /// <summary>
    /// Thành tiền dự kiến = SL × đơn giá × (100 − %CK)/100, làm tròn 2 số; dùng chung cho dòng và giá trị dự kiến sau phụ lục.
    /// Ví dụ 1.000 × 48.000 × 95% = 45.600.000.
    /// </summary>
    /// <param name="qty">SL dự kiến.</param>
    /// <param name="price">Giá theo bảng giá.</param>
    public static decimal AmountOf(decimal qty, ContractLinePrice price)
    {
        ArgumentNullException.ThrowIfNull(price);
        return Math.Round(qty * price.UnitPrice * (100m - price.DiscountPct) / 100m, 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Bộ phận được phép đặt hàng theo hợp đồng.</summary>
[Auditable("Bộ phận được đặt theo hợp đồng")]
public sealed class PurchaseContractDepartment : Entity
{
    private PurchaseContractDepartment()
    {
    }

    internal PurchaseContractDepartment(Guid contractId, Guid departmentId)
    {
        ContractId = contractId;
        DepartmentId = departmentId;
    }

    /// <summary>Hợp đồng.</summary>
    public Guid ContractId { get; private set; }

    /// <summary>Bộ phận.</summary>
    public Guid DepartmentId { get; private set; }
}
