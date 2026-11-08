using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Pricing;

/// <summary>Trạng thái thỏa thuận chiết khấu bậc sản lượng gộp (design.md mục 18.25 – lát G6a).</summary>
public enum VolumeRebateStatus
{
    /// <summary>Nháp – đang soạn, sửa được.</summary>
    Draft = 1,

    /// <summary>Chờ duyệt – không sửa được; người lập rút về được.</summary>
    Submitted = 2,

    /// <summary>Hiệu lực – đã duyệt, được theo dõi lũy kế; không sửa.</summary>
    Active = 3,

    /// <summary>Đã hủy (kèm lý do) – không còn theo dõi.</summary>
    Cancelled = 4,
}

/// <summary>Cơ sở tính bậc chiết khấu.</summary>
public enum VolumeRebateBasis
{
    /// <summary>Theo số lượng gộp (mọi dòng cùng một ĐVT để cộng được).</summary>
    Quantity = 1,

    /// <summary>Theo doanh số mua gộp (VND, sau chiết khấu trên đơn, trước VAT).</summary>
    Amount = 2,
}

/// <summary>Kỳ tính bậc (lũy kế bắt đầu lại từ 0 mỗi kỳ).</summary>
public enum VolumeRebatePeriod
{
    /// <summary>Theo tháng dương lịch.</summary>
    Month = 1,

    /// <summary>Theo quý dương lịch.</summary>
    Quarter = 2,

    /// <summary>Theo năm dương lịch.</summary>
    Year = 3,

    /// <summary>Một kỳ cho cả thời hạn thỏa thuận.</summary>
    Whole = 4,
}

/// <summary>Đầu thỏa thuận chiết khấu bậc.</summary>
/// <param name="Title">Tên thỏa thuận (bắt buộc).</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="ItemCategoryId">Nhóm hàng (mặt hàng trên dòng thuộc nhóm này hoặc nhóm con).</param>
/// <param name="ValidFrom">Hiệu lực từ.</param>
/// <param name="ValidTo">Hiệu lực đến.</param>
/// <param name="Period">Kỳ tính bậc.</param>
/// <param name="Basis">Cơ sở tính bậc.</param>
/// <param name="TargetTierNo">Bậc mục tiêu (bậc đã đàm phán dựa trên sản lượng dự kiến); null = không đặt.</param>
/// <param name="CompanyIds">Các công ty áp dụng (gộp sản lượng).</param>
/// <param name="Note">Ghi chú.</param>
public sealed record VolumeRebateHeader(
    string Title, Guid SupplierId, Guid ItemCategoryId, DateOnly ValidFrom, DateOnly ValidTo, VolumeRebatePeriod Period, VolumeRebateBasis Basis,
    int? TargetTierNo, IReadOnlyList<Guid> CompanyIds, string? Note);

/// <summary>Dữ liệu một dòng mặt hàng của thỏa thuận.</summary>
/// <param name="Id">Id dòng đang có (null = dòng mới).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT tính sản lượng (cơ sở hoặc quy đổi).</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record VolumeRebateLineData(Guid? Id, Guid ItemId, Guid UomId, string? Note);

/// <summary>Một bậc chiết khấu.</summary>
/// <param name="FromValue">Ngưỡng từ (SL theo ĐVT dòng hoặc doanh số VND) – đạt ngưỡng thì cả kỳ hưởng % của bậc.</param>
/// <param name="Percent">% chiết khấu trên doanh số kỳ.</param>
public sealed record VolumeRebateTierData(decimal FromValue, decimal Percent);

/// <summary>
/// Thỏa thuận chiết khấu bậc theo sản lượng gộp – số loại <see cref="DocumentTypes.VolumeRebate"/> (design.md mục 18.25, lát G6a). Quyết định
/// thành lập Ban cung ứng: Ban thương lượng với NCC bậc chiết khấu theo <b>sản lượng gộp</b> của các công ty, Phòng cung ứng chung theo dõi lũy kế
/// so với bậc, cảnh báo sắp chạm/hụt bậc và chia chiết khấu cho từng công ty theo tỷ lệ doanh số. Thỏa thuận lập ở tổng công ty
/// (<see cref="CompanyId"/>) cho một NCC × nhóm hàng, áp cho các công ty chọn; dòng là mặt hàng tính sản lượng, bậc là ngưỡng → %.
/// Nháp → Chờ duyệt → Hiệu lực (APRL loại CKB, người lập không tự duyệt); hiệu lực không sửa – đổi thì hủy (kèm lý do) và lập thỏa thuận mới.
/// </summary>
[Auditable("Thỏa thuận chiết khấu sản lượng gộp", LabelProperty = nameof(No))]
public sealed class VolumeRebateAgreement : AuditableEntity, ICompanyOwned, IHasDocumentNumber, IApprovalSubject, IVersioned
{
    /// <summary>Độ dài ghi chú, lý do tối đa.</summary>
    public const int TextMaxLength = 1000;

    /// <summary>Độ dài tên thỏa thuận tối đa.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Số bậc tối đa – thỏa thuận thực tế chỉ vài bậc; chặn nhập nhầm.</summary>
    public const int MaxTiers = 10;

    /// <summary>Giới hạn ngưỡng (vừa numeric(19,4)).</summary>
    private const decimal MaxThreshold = 100_000_000_000_000m;

    private readonly List<VolumeRebateLine> _lines = [];
    private readonly List<VolumeRebateTier> _tiers = [];
    private readonly List<VolumeRebateCompany> _companies = [];

    private VolumeRebateAgreement()
    {
    }

    /// <summary>Phiên bản dòng (cột hệ thống xmin của PostgreSQL) – chống ghi đè khi hai người cùng sửa (<see cref="IVersioned"/>).</summary>
    public uint Version { get; private set; }

    /// <summary>Công ty lập (tổng công ty).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số thỏa thuận, ví dụ CKB-TCT-2601-0001 (theo ngày hiệu lực từ).</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Tên thỏa thuận.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Nhóm hàng.</summary>
    public Guid ItemCategoryId { get; private set; }

    /// <summary>Hiệu lực từ.</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>Hiệu lực đến.</summary>
    public DateOnly ValidTo { get; private set; }

    /// <summary>Kỳ tính bậc.</summary>
    public VolumeRebatePeriod Period { get; private set; }

    /// <summary>Cơ sở tính bậc.</summary>
    public VolumeRebateBasis Basis { get; private set; }

    /// <summary>Bậc mục tiêu (số thứ tự bậc, từ 1); null = không đặt – khi đó không cảnh báo hụt bậc.</summary>
    public int? TargetTierNo { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public VolumeRebateStatus Status { get; private set; } = VolumeRebateStatus.Draft;

    /// <inheritdoc />
    public int ApprovalRound { get; private set; }

    /// <summary>Lý do từ chối gần nhất.</summary>
    public string? RejectReason { get; private set; }

    /// <summary>Thời điểm duyệt đủ (UTC).</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>Ngày hủy.</summary>
    public DateOnly? CancelledOn { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>Các dòng mặt hàng.</summary>
    public IReadOnlyList<VolumeRebateLine> Lines => _lines;

    /// <summary>Các bậc, theo ngưỡng tăng dần.</summary>
    public IReadOnlyList<VolumeRebateTier> Tiers => _tiers;

    /// <summary>Các công ty áp dụng.</summary>
    public IReadOnlyList<VolumeRebateCompany> Companies => _companies;

    /// <inheritdoc />
    public string DocType => DocumentTypes.VolumeRebate;

    /// <inheritdoc />
    /// <remarks>Không xét giá trị: mọi thỏa thuận qua đủ các cấp đã cấu hình (như phiếu duyệt tháng).</remarks>
    public decimal ApprovalAmount => 0;

    /// <inheritdoc />
    public bool IsAwaitingApproval => Status == VolumeRebateStatus.Submitted;

    /// <inheritdoc />
    /// <remarks>Người có kê khai xung đột lợi ích với NCC của thỏa thuận không được duyệt (mục 18.8).</remarks>
    public IReadOnlyCollection<Guid> RelatedSupplierIds => [SupplierId];

    /// <summary>Còn tác dụng hoặc đang trên đường duyệt – dùng để chặn trùng NCC × mặt hàng × công ty × thời hạn.</summary>
    public bool IsLive => Status is VolumeRebateStatus.Draft or VolumeRebateStatus.Submitted or VolumeRebateStatus.Active;

    /// <summary>Lập thỏa thuận nháp.</summary>
    /// <param name="companyId">Công ty lập (tổng công ty – tầng trên kiểm tra).</param>
    /// <param name="no">Số thỏa thuận.</param>
    /// <param name="header">Đầu thỏa thuận.</param>
    /// <returns>Thỏa thuận nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu thỏa thuận sai.</exception>
    public static VolumeRebateAgreement Create(Guid companyId, string no, VolumeRebateHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Thiếu công ty lập thỏa thuận.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Thỏa thuận phải có số.");
        var agreement = new VolumeRebateAgreement { CompanyId = companyId, No = no };
        agreement.Update(header);
        return agreement;
    }

    /// <summary>Sửa đầu thỏa thuận khi còn nháp.</summary>
    /// <param name="header">Đầu mới.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dữ liệu sai.</exception>
    public void Update(VolumeRebateHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureStatus(VolumeRebateStatus.Draft, "nháp");
        var title = Clean(header.Title) ?? throw new BusinessRuleException("Hãy nhập tên thỏa thuận.");
        if (title.Length > TitleMaxLength) throw new BusinessRuleException($"Tên thỏa thuận tối đa {TitleMaxLength} ký tự.");
        if (header.SupplierId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhà cung cấp.");
        if (header.ItemCategoryId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhóm hàng.");
        if (header.ValidTo < header.ValidFrom) throw new BusinessRuleException("Ngày hiệu lực đến phải sau hoặc bằng ngày hiệu lực từ.");
        // Thỏa thuận khung tối đa vài năm; chặn gõ nhầm năm làm sinh hàng trăm kỳ tháng.
        if (header.ValidTo > header.ValidFrom.AddYears(5)) throw new BusinessRuleException("Thời hạn thỏa thuận tối đa 5 năm.");
        if (!Enum.IsDefined(header.Period)) throw new BusinessRuleException("Kỳ tính bậc không hợp lệ.");
        if (!Enum.IsDefined(header.Basis)) throw new BusinessRuleException("Cơ sở tính bậc không hợp lệ.");
        if (header.TargetTierNo is < 1 or > MaxTiers) throw new BusinessRuleException($"Bậc mục tiêu phải từ 1 đến {MaxTiers}.");
        var companies = header.CompanyIds.Where(c => c != Guid.Empty).Distinct().ToList();
        if (companies.Count == 0) throw new BusinessRuleException("Hãy chọn ít nhất một công ty áp dụng.");
        var note = Clean(header.Note);
        if (note is { Length: > TextMaxLength }) throw new BusinessRuleException($"Ghi chú tối đa {TextMaxLength} ký tự.");
        Title = title;
        SupplierId = header.SupplierId;
        ItemCategoryId = header.ItemCategoryId;
        ValidFrom = header.ValidFrom;
        ValidTo = header.ValidTo;
        Period = header.Period;
        Basis = header.Basis;
        TargetTierNo = header.TargetTierNo;
        Note = note;
        _companies.RemoveAll(c => !companies.Contains(c.CompanyId));
        foreach (var id in companies.Where(id => _companies.All(c => c.CompanyId != id)))
            _companies.Add(new VolumeRebateCompany(Id, id));
    }

    /// <summary>
    /// Thay toàn bộ dòng mặt hàng. Mỗi mặt hàng một dòng; tính theo số lượng thì mọi dòng cùng một ĐVT – sản lượng gộp phải cộng được
    /// (ví dụ cùng "tấn" cho các loại xi măng).
    /// </summary>
    /// <param name="lines">Dòng kèm mặt hàng đã nạp (có ĐVT quy đổi).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có dòng, trùng mặt hàng, khác ĐVT hoặc dòng sai.</exception>
    public void SetLines(IReadOnlyList<(VolumeRebateLineData Data, Item Item)> lines)
    {
        EnsureStatus(VolumeRebateStatus.Draft, "nháp");
        if (lines.Count == 0) throw new BusinessRuleException("Thỏa thuận phải có ít nhất một mặt hàng.");
        var duplicate = lines.GroupBy(l => l.Data.ItemId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new BusinessRuleException($"Mặt hàng {duplicate.First().Item.Code} có hai dòng – mỗi mặt hàng một dòng.");
        if (Basis == VolumeRebateBasis.Quantity && lines.Select(l => l.Data.UomId).Distinct().Count() > 1)
            throw new BusinessRuleException("Tính bậc theo số lượng thì mọi dòng phải cùng một ĐVT để cộng sản lượng gộp.");
        var keep = new List<VolumeRebateLine>();
        var lineNo = 0;
        foreach (var (data, item) in lines)
        {
            lineNo++;
            VolumeRebateLine line;
            if (data.Id is { } id)
            {
                line = _lines.FirstOrDefault(l => l.Id == id) ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại thỏa thuận.");
            }
            else
            {
                line = new VolumeRebateLine(Id);
                _lines.Add(line);
            }

            line.Set(lineNo, data, item);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
    }

    /// <summary>
    /// Thay toàn bộ bậc. Ngưỡng và % đều tăng dần nghiêm ngặt (bậc cao hơn phải mua nhiều hơn và được chiết khấu nhiều hơn – ngược lại là nhập
    /// nhầm); ngưỡng bậc 1 &gt; 0; 0 &lt; % ≤ 100 (4 số lẻ); ngưỡng theo số lượng 4 số lẻ, theo doanh số 2 số lẻ (CLAUDE.md mục 4).
    /// </summary>
    /// <param name="tiers">Các bậc (thứ tự bất kỳ – sắp theo ngưỡng).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có bậc, quá số bậc, bậc sai hoặc bậc mục tiêu vượt số bậc.</exception>
    public void SetTiers(IReadOnlyList<VolumeRebateTierData> tiers)
    {
        EnsureStatus(VolumeRebateStatus.Draft, "nháp");
        if (tiers.Count == 0) throw new BusinessRuleException("Thỏa thuận phải có ít nhất một bậc chiết khấu.");
        if (tiers.Count > MaxTiers) throw new BusinessRuleException($"Tối đa {MaxTiers} bậc chiết khấu.");
        var decimals = Basis == VolumeRebateBasis.Quantity ? 4 : 2;
        var sorted = tiers.OrderBy(t => t.FromValue).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var t = sorted[i];
            var prefix = $"Bậc {i + 1}";
            if (t.FromValue <= 0 || decimal.Round(t.FromValue, decimals) != t.FromValue || t.FromValue >= MaxThreshold)
                throw new BusinessRuleException($"{prefix}: ngưỡng phải > 0, tối đa {decimals} chữ số thập phân.");
            if (t.Percent <= 0 || t.Percent > 100 || decimal.Round(t.Percent, 4) != t.Percent)
                throw new BusinessRuleException($"{prefix}: % chiết khấu phải > 0 và ≤ 100, tối đa 4 chữ số thập phân.");
            if (i > 0 && t.FromValue == sorted[i - 1].FromValue) throw new BusinessRuleException($"{prefix}: trùng ngưỡng với bậc {i}.");
            if (i > 0 && t.Percent <= sorted[i - 1].Percent)
                throw new BusinessRuleException($"{prefix}: % chiết khấu phải lớn hơn bậc {i} (ngưỡng cao hơn thì chiết khấu cao hơn).");
        }

        if (TargetTierNo > sorted.Count) throw new BusinessRuleException($"Bậc mục tiêu {TargetTierNo} vượt số bậc ({sorted.Count}).");
        _tiers.Clear();
        for (var i = 0; i < sorted.Count; i++) _tiers.Add(new VolumeRebateTier(Id, i + 1, sorted[i].FromValue, sorted[i].Percent));
    }

    /// <summary>Gửi duyệt: Nháp → Chờ duyệt, xóa lý do từ chối cũ.</summary>
    /// <returns>Số lượt gửi mới.</returns>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc thiếu dòng/bậc.</exception>
    public int Submit()
    {
        EnsureStatus(VolumeRebateStatus.Draft, "nháp");
        if (_lines.Count == 0) throw new BusinessRuleException("Thỏa thuận phải có ít nhất một mặt hàng mới gửi duyệt được.");
        if (_tiers.Count == 0) throw new BusinessRuleException("Thỏa thuận phải có ít nhất một bậc chiết khấu mới gửi duyệt được.");
        Status = VolumeRebateStatus.Submitted;
        RejectReason = null;
        return ++ApprovalRound;
    }

    /// <inheritdoc />
    public void MarkApproved(DateTimeOffset at)
    {
        EnsureStatus(VolumeRebateStatus.Submitted, "chờ duyệt");
        Status = VolumeRebateStatus.Active;
        ApprovedAt = at;
    }

    /// <summary>Bị từ chối hoặc người lập rút lại: về Nháp để sửa, giữ lý do.</summary>
    /// <param name="reason">Lý do (rút lại thì null).</param>
    /// <exception cref="BusinessRuleException">Khi thỏa thuận không chờ duyệt.</exception>
    public void ReturnToDraft(string? reason)
    {
        EnsureStatus(VolumeRebateStatus.Submitted, "chờ duyệt");
        Status = VolumeRebateStatus.Draft;
        RejectReason = Clean(reason);
    }

    /// <summary>
    /// Hủy thỏa thuận nháp hoặc hiệu lực (ví dụ NCC đổi chính sách, lập thỏa thuận mới thay) – bắt buộc lý do. Đang chờ duyệt thì rút về nháp
    /// trước để các cấp duyệt không còn việc treo.
    /// </summary>
    /// <param name="today">Ngày hôm nay (giờ Việt Nam).</param>
    /// <param name="reason">Lý do.</param>
    /// <exception cref="BusinessRuleException">Khi trạng thái không hủy được hoặc thiếu lý do.</exception>
    public void Cancel(DateOnly today, string? reason)
    {
        if (Status is not (VolumeRebateStatus.Draft or VolumeRebateStatus.Active))
            throw new BusinessRuleException($"Thỏa thuận {No} chỉ hủy được khi đang nháp hoặc hiệu lực.");
        var clean = Clean(reason) ?? throw new BusinessRuleException("Hãy nhập lý do hủy.");
        if (clean.Length > TextMaxLength) throw new BusinessRuleException($"Lý do tối đa {TextMaxLength} ký tự.");
        Status = VolumeRebateStatus.Cancelled;
        CancelledOn = today;
        CancelReason = clean;
    }

    /// <summary>Đầu thỏa thuận hiện tại (để kiểm tra lại lúc gửi/duyệt hoặc chép sang thỏa thuận mới).</summary>
    public VolumeRebateHeader Header() =>
        new(Title, SupplierId, ItemCategoryId, ValidFrom, ValidTo, Period, Basis, TargetTierNo, _companies.Select(c => c.CompanyId).ToList(), Note);

    /// <summary>Dòng hiện tại dưới dạng dữ liệu dòng mới (Id null).</summary>
    public IReadOnlyList<VolumeRebateLineData> CopyLines() =>
        _lines.OrderBy(l => l.LineNo).Select(l => new VolumeRebateLineData(null, l.ItemId, l.UomId, l.Note)).ToList();

    /// <summary>Bậc hiện tại dưới dạng dữ liệu.</summary>
    public IReadOnlyList<VolumeRebateTierData> CopyTiers() =>
        _tiers.OrderBy(t => t.TierNo).Select(t => new VolumeRebateTierData(t.FromValue, t.Percent)).ToList();

    /// <summary>
    /// Các kỳ tính bậc trong thời hạn, cắt theo thời hạn. Ví dụ hiệu lực 15/02/2026 – 10/08/2026: theo tháng → 15/02–28/02, 01/03–31/03, …,
    /// 01/08–10/08; theo quý → 15/02–31/03, 01/04–30/06, 01/07–10/08; theo năm hoặc cả thỏa thuận → 15/02–10/08.
    /// </summary>
    /// <returns>Các kỳ theo thứ tự thời gian.</returns>
    public IReadOnlyList<(DateOnly From, DateOnly To)> Periods()
    {
        if (Period == VolumeRebatePeriod.Whole) return [(ValidFrom, ValidTo)];
        var periods = new List<(DateOnly, DateOnly)>();
        var start = ValidFrom;
        while (start <= ValidTo)
        {
            var end = Period switch
            {
                VolumeRebatePeriod.Month => new DateOnly(start.Year, start.Month, 1).AddMonths(1).AddDays(-1),
                VolumeRebatePeriod.Quarter => new DateOnly(start.Year, (start.Month - 1) / 3 * 3 + 1, 1).AddMonths(3).AddDays(-1),
                _ => new DateOnly(start.Year, 12, 31),
            };
            if (end > ValidTo) end = ValidTo;
            periods.Add((start, end));
            start = end.AddDays(1);
        }

        return periods;
    }

    /// <summary>
    /// Bậc đạt được với sản lượng/doanh số <paramref name="volume"/>: bậc có ngưỡng lớn nhất ≤ volume (bậc toàn phần – đạt bậc thì cả kỳ hưởng
    /// % của bậc, không chia lũy tiến). Ví dụ bậc 1: 100 tấn 1%, bậc 2: 200 tấn 2%; mua 250 tấn → bậc 2; 80 tấn → chưa đạt bậc nào (null).
    /// </summary>
    /// <param name="volume">Lũy kế kỳ theo cơ sở tính bậc.</param>
    /// <returns>Bậc đạt được hoặc null.</returns>
    public VolumeRebateTier? TierFor(decimal volume) => _tiers.Where(t => t.FromValue <= volume).MaxBy(t => t.FromValue);

    private void EnsureStatus(VolumeRebateStatus expected, string name)
    {
        if (Status != expected) throw new BusinessRuleException($"Thỏa thuận {No} không ở trạng thái {name}.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Một công ty áp dụng thỏa thuận – không phải <see cref="ICompanyOwned"/> vì công ty thành viên phải đọc được thỏa thuận của tổng công ty.</summary>
[Auditable("Công ty áp dụng thỏa thuận chiết khấu sản lượng gộp")]
public sealed class VolumeRebateCompany : Entity
{
    private VolumeRebateCompany()
    {
    }

    internal VolumeRebateCompany(Guid agreementId, Guid companyId)
    {
        AgreementId = agreementId;
        CompanyId = companyId;
    }

    /// <summary>Thỏa thuận.</summary>
    public Guid AgreementId { get; private set; }

    /// <summary>Công ty áp dụng.</summary>
    public Guid CompanyId { get; private set; }
}

/// <summary>Một bậc chiết khấu của thỏa thuận.</summary>
[Auditable("Bậc chiết khấu sản lượng gộp")]
public sealed class VolumeRebateTier : Entity
{
    private VolumeRebateTier()
    {
    }

    internal VolumeRebateTier(Guid agreementId, int tierNo, decimal fromValue, decimal percent)
    {
        AgreementId = agreementId;
        TierNo = tierNo;
        FromValue = fromValue;
        Percent = percent;
    }

    /// <summary>Thỏa thuận.</summary>
    public Guid AgreementId { get; private set; }

    /// <summary>Số thứ tự bậc (1 = ngưỡng thấp nhất).</summary>
    public int TierNo { get; private set; }

    /// <summary>Ngưỡng từ (SL theo ĐVT dòng hoặc doanh số VND) – numeric(19,4).</summary>
    public decimal FromValue { get; private set; }

    /// <summary>% chiết khấu trên doanh số kỳ – numeric(9,4).</summary>
    public decimal Percent { get; private set; }
}

/// <summary>Dòng thỏa thuận: một mặt hàng được tính vào sản lượng gộp.</summary>
[Auditable("Dòng thỏa thuận chiết khấu sản lượng gộp")]
public sealed class VolumeRebateLine : Entity
{
    private VolumeRebateLine()
    {
    }

    internal VolumeRebateLine(Guid agreementId) => AgreementId = agreementId;

    /// <summary>Thỏa thuận.</summary>
    public Guid AgreementId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT tính sản lượng.</summary>
    public Guid UomId { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6). SL theo ĐVT dòng = SL cơ sở ÷ hệ số.</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    internal void Set(int lineNo, VolumeRebateLineData data, Item item)
    {
        var prefix = $"Dòng {lineNo} ({item.Code})";
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        if (!item.IsActive && item.Id != ItemId) throw new BusinessRuleException($"{prefix}: mặt hàng đã ngừng dùng.");
        var note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
        if (note is { Length: > 500 }) throw new BusinessRuleException($"{prefix}: ghi chú tối đa 500 ký tự.");
        LineNo = lineNo;
        ItemId = item.Id;
        UomId = data.UomId;
        UomFactor = data.UomId == item.BaseUomId ? 1m
            : item.Conversions.FirstOrDefault(c => c.UomId == data.UomId)?.Factor
              ?? throw new BusinessRuleException($"{prefix}: ĐVT phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.");
        Note = note;
    }
}
