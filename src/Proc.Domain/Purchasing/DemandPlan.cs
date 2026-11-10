using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;

namespace Proc.Domain.Purchasing;

/// <summary>Trạng thái phiếu kế hoạch nhu cầu (design.md mục 18.25 – lát G5a).</summary>
public enum DemandPlanStatus
{
    /// <summary>Nháp – đang nhập, sửa được.</summary>
    Draft = 1,

    /// <summary>Đã chốt – số liệu dùng để tổng hợp nhu cầu gộp, không sửa.</summary>
    Confirmed = 2,

    /// <summary>Đã thay – có phiếu điều chỉnh đã chốt thay thế.</summary>
    Replaced = 3,

    /// <summary>Đã hủy (kèm lý do).</summary>
    Cancelled = 4,
}

/// <summary>Đầu phiếu kế hoạch nhu cầu.</summary>
/// <param name="Year">Năm kế hoạch.</param>
/// <param name="ItemCategoryId">Nhóm hàng.</param>
/// <param name="Note">Ghi chú (căn cứ lập: định mức, nhu cầu kho, bộ phận sử dụng…).</param>
public sealed record DemandPlanHeader(int Year, Guid ItemCategoryId, string? Note);

/// <summary>Dữ liệu một dòng kế hoạch nhu cầu.</summary>
/// <param name="Id">Id dòng đang có (null = dòng mới).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT (cơ sở hoặc quy đổi).</param>
/// <param name="Months">Số lượng 12 tháng, phần tử 0 là tháng 1.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record DemandPlanLineData(Guid? Id, Guid ItemId, Guid UomId, IReadOnlyList<decimal> Months, string? Note);

/// <summary>
/// Phiếu kế hoạch nhu cầu – số loại <see cref="DocumentTypes.DemandPlan"/> (design.md mục 18.25, lát G5a). Quyết định thành lập Ban cung ứng:
/// Văn phòng Ban tổng hợp, cân đối nhu cầu năm, quý, tháng của các công ty theo nhóm hàng, dự báo sản lượng gộp làm cơ sở thương lượng.
/// Mỗi công ty lập phiếu của mình theo năm × nhóm hàng; dòng là một mặt hàng với số lượng 12 tháng (quý, năm cộng từ tháng). Nháp → Đã chốt
/// (không qua duyệt – kế hoạch là số liệu đề xuất, Ban quyết định ở phiếu duyệt tháng). Sửa sau khi chốt: lập phiếu điều chỉnh
/// (<see cref="ReplacesId"/>), chốt xong phiếu cũ thành Đã thay; hoặc hủy kèm lý do.
/// </summary>
[Auditable("Phiếu kế hoạch nhu cầu", LabelProperty = nameof(No))]
public sealed class DemandPlan : AuditableEntity, ICompanyOwned, IHasDocumentNumber, IVersioned
{
    /// <summary>Độ dài ghi chú, lý do tối đa.</summary>
    public const int TextMaxLength = 1000;

    /// <summary>Số năm tới tối đa được lập kế hoạch (tính từ năm hiện tại).</summary>
    public const int MaxYearsAhead = 2;

    private readonly List<DemandPlanLine> _lines = [];

    private DemandPlan()
    {
    }

    /// <summary>Công ty lập (pháp nhân có nhu cầu).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu, ví dụ KHN-BHA-2611-0001.</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <inheritdoc />
    public uint Version { get; private set; }

    /// <summary>Năm kế hoạch.</summary>
    public int Year { get; private set; }

    /// <summary>Nhóm hàng (mặt hàng trên dòng thuộc nhóm này hoặc nhóm con).</summary>
    public Guid ItemCategoryId { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public DemandPlanStatus Status { get; private set; } = DemandPlanStatus.Draft;

    /// <summary>Phiếu đã chốt mà phiếu này điều chỉnh (null = phiếu gốc).</summary>
    public Guid? ReplacesId { get; private set; }

    /// <summary>Thời điểm chốt (UTC).</summary>
    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Người chốt.</summary>
    public Guid? ConfirmedBy { get; private set; }

    /// <summary>Ngày hủy.</summary>
    public DateOnly? CancelledOn { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>Các dòng mặt hàng.</summary>
    public IReadOnlyList<DemandPlanLine> Lines => _lines;

    /// <summary>Lập phiếu nháp.</summary>
    /// <param name="companyId">Công ty lập.</param>
    /// <param name="no">Số phiếu.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <param name="replacesId">Phiếu đã chốt được điều chỉnh (null = phiếu gốc).</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu phiếu sai.</exception>
    public static DemandPlan Create(Guid companyId, string no, DemandPlanHeader header, DateOnly today, Guid? replacesId = null)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Thiếu công ty lập phiếu.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Phiếu phải có số.");
        var plan = new DemandPlan { CompanyId = companyId, No = no, ReplacesId = replacesId };
        plan.Update(header, today);
        return plan;
    }

    /// <summary>Sửa đầu phiếu khi còn nháp.</summary>
    /// <param name="header">Đầu phiếu mới.</param>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dữ liệu sai.</exception>
    public void Update(DemandPlanHeader header, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        EnsureYear(header.Year, today);
        if (header.ItemCategoryId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhóm hàng.");
        var note = Clean(header.Note);
        if (note is { Length: > TextMaxLength }) throw new BusinessRuleException($"Ghi chú tối đa {TextMaxLength} ký tự.");
        Year = header.Year;
        ItemCategoryId = header.ItemCategoryId;
        Note = note;
    }

    /// <summary>Thay toàn bộ dòng; mỗi mặt hàng một dòng.</summary>
    /// <param name="lines">Dòng kèm mặt hàng đã nạp (có ĐVT quy đổi).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có dòng, trùng mặt hàng hoặc dòng sai.</exception>
    public void SetLines(IReadOnlyList<(DemandPlanLineData Data, Item Item)> lines)
    {
        EnsureDraft();
        if (lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một mặt hàng.");
        var keep = new List<DemandPlanLine>();
        var lineNo = 0;
        foreach (var (data, item) in lines)
        {
            lineNo++;
            DemandPlanLine line;
            if (data.Id is { } id)
            {
                line = _lines.FirstOrDefault(l => l.Id == id) ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại phiếu.");
            }
            else
            {
                line = new DemandPlanLine(Id);
                _lines.Add(line);
            }

            line.Set(lineNo, data, item);
            keep.Add(line);
        }

        // Một mặt hàng một dòng: tổng hợp gộp cộng theo mặt hàng, hai dòng cùng mặt hàng dễ nhập trùng nhu cầu.
        var duplicate = keep.GroupBy(l => l.ItemId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new BusinessRuleException($"Dòng {string.Join(", ", duplicate.Select(l => l.LineNo))}: trùng mặt hàng – mỗi mặt hàng một dòng.");
        _lines.RemoveAll(l => !keep.Contains(l));
    }

    /// <summary>Chốt phiếu: số liệu vào tổng hợp nhu cầu, từ đây không sửa (sửa bằng phiếu điều chỉnh).</summary>
    /// <param name="at">Thời điểm chốt (UTC).</param>
    /// <param name="userId">Người chốt.</param>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, năm đã qua hoặc chưa có dòng.</exception>
    public void Confirm(DateTimeOffset at, Guid userId, DateOnly today)
    {
        EnsureDraft();
        EnsureYear(Year, today);
        if (_lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một mặt hàng mới chốt được.");
        Status = DemandPlanStatus.Confirmed;
        ConfirmedAt = at;
        ConfirmedBy = userId;
    }

    /// <summary>Phiếu điều chỉnh của phiếu này đã chốt: phiếu này thành Đã thay (thôi tính vào tổng hợp).</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu không ở trạng thái đã chốt.</exception>
    public void MarkReplaced()
    {
        if (Status != DemandPlanStatus.Confirmed) throw new BusinessRuleException($"Phiếu {No} không ở trạng thái đã chốt.");
        Status = DemandPlanStatus.Replaced;
    }

    /// <summary>Hủy phiếu nháp hoặc đã chốt – bắt buộc lý do; số liệu thôi tính vào tổng hợp.</summary>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <param name="reason">Lý do.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu đã hủy, đã thay hoặc thiếu lý do.</exception>
    public void Cancel(DateOnly today, string? reason)
    {
        if (Status is DemandPlanStatus.Cancelled or DemandPlanStatus.Replaced)
            throw new BusinessRuleException($"Phiếu {No} đã {(Status == DemandPlanStatus.Cancelled ? "hủy" : "được thay")} – không hủy được.");
        var clean = Clean(reason) ?? throw new BusinessRuleException("Hãy nhập lý do hủy.");
        if (clean.Length > TextMaxLength) throw new BusinessRuleException($"Lý do tối đa {TextMaxLength} ký tự.");
        Status = DemandPlanStatus.Cancelled;
        CancelledOn = today;
        CancelReason = clean;
    }

    /// <summary>Chép đầu phiếu (để lập phiếu điều chỉnh).</summary>
    /// <returns>Đầu phiếu.</returns>
    public DemandPlanHeader CopyHeader() => new(Year, ItemCategoryId, Note);

    /// <summary>Chép các dòng thành dữ liệu dòng mới (Id null).</summary>
    /// <returns>Dữ liệu dòng.</returns>
    public IReadOnlyList<DemandPlanLineData> CopyLines() =>
        _lines.OrderBy(l => l.LineNo).Select(l => new DemandPlanLineData(null, l.ItemId, l.UomId, l.Months, l.Note)).ToList();

    /// <summary>
    /// Năm kế hoạch từ năm hiện tại đến <see cref="MaxYearsAhead"/> năm tới: năm đã qua thì kế hoạch không còn dùng để thương lượng.
    /// </summary>
    private static void EnsureYear(int year, DateOnly today)
    {
        if (year < today.Year) throw new BusinessRuleException($"Năm {year} đã qua – chỉ lập, chốt kế hoạch cho năm {today.Year} trở đi.");
        if (year > today.Year + MaxYearsAhead) throw new BusinessRuleException($"Chỉ lập kế hoạch tối đa đến năm {today.Year + MaxYearsAhead}.");
    }

    /// <summary>Chặn sửa khi phiếu không còn ở trạng thái Nháp.</summary>
    private void EnsureDraft()
    {
        if (Status != DemandPlanStatus.Draft) throw new BusinessRuleException($"Phiếu {No} không ở trạng thái nháp.");
    }

    /// <summary>Bỏ khoảng trắng hai đầu; chuỗi rỗng hoặc toàn khoảng trắng thành null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Dòng kế hoạch nhu cầu: một mặt hàng, số lượng 12 tháng theo ĐVT dòng.</summary>
[Auditable("Dòng phiếu kế hoạch nhu cầu")]
public sealed class DemandPlanLine : Entity
{
    /// <summary>Giới hạn số lượng (numeric(18,4) – CLAUDE.md mục 4).</summary>
    private const decimal MaxQuantity = 100_000_000_000_000m;

    private DemandPlanLine()
    {
    }

    internal DemandPlanLine(Guid planId) => PlanId = planId;

    /// <summary>Phiếu.</summary>
    public Guid PlanId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT của số lượng.</summary>
    public Guid UomId { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6); dùng quy số lượng về ĐVT cơ sở khi tổng hợp.</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>SL tháng 1.</summary>
    public decimal Qty01 { get; private set; }

    /// <summary>SL tháng 2.</summary>
    public decimal Qty02 { get; private set; }

    /// <summary>SL tháng 3.</summary>
    public decimal Qty03 { get; private set; }

    /// <summary>SL tháng 4.</summary>
    public decimal Qty04 { get; private set; }

    /// <summary>SL tháng 5.</summary>
    public decimal Qty05 { get; private set; }

    /// <summary>SL tháng 6.</summary>
    public decimal Qty06 { get; private set; }

    /// <summary>SL tháng 7.</summary>
    public decimal Qty07 { get; private set; }

    /// <summary>SL tháng 8.</summary>
    public decimal Qty08 { get; private set; }

    /// <summary>SL tháng 9.</summary>
    public decimal Qty09 { get; private set; }

    /// <summary>SL tháng 10.</summary>
    public decimal Qty10 { get; private set; }

    /// <summary>SL tháng 11.</summary>
    public decimal Qty11 { get; private set; }

    /// <summary>SL tháng 12.</summary>
    public decimal Qty12 { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Số lượng 12 tháng theo ĐVT dòng, phần tử 0 là tháng 1.</summary>
    public IReadOnlyList<decimal> Months => [Qty01, Qty02, Qty03, Qty04, Qty05, Qty06, Qty07, Qty08, Qty09, Qty10, Qty11, Qty12];

    /// <summary>Tổng năm theo ĐVT dòng.</summary>
    public decimal Total => Months.Sum();

    /// <summary>Kiểm tra và gán dữ liệu một dòng kế hoạch nhu cầu (mặt hàng, ĐVT, số lượng 12 tháng).</summary>
    internal void Set(int lineNo, DemandPlanLineData data, Item item)
    {
        var prefix = $"Dòng {lineNo} ({item.Code})";
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        if (!item.IsActive && item.Id != ItemId) throw new BusinessRuleException($"{prefix}: mặt hàng đã ngừng dùng.");
        if (data.Months is not { Count: 12 }) throw new BusinessRuleException($"{prefix}: phải có số lượng đủ 12 tháng.");
        for (var m = 0; m < 12; m++)
        {
            var q = data.Months[m];
            if (q < 0 || decimal.Round(q, 4) != q || q >= MaxQuantity)
                throw new BusinessRuleException($"{prefix}: số lượng tháng {m + 1} phải ≥ 0, tối đa 4 chữ số thập phân.");
        }

        // Dòng toàn số 0 không phải nhu cầu – bỏ dòng thay vì để tổng hợp đếm mặt hàng không cần mua.
        if (data.Months.All(q => q == 0)) throw new BusinessRuleException($"{prefix}: hãy nhập số lượng ít nhất một tháng.");
        var note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
        if (note is { Length: > 500 }) throw new BusinessRuleException($"{prefix}: ghi chú tối đa 500 ký tự.");
        LineNo = lineNo;
        ItemId = item.Id;
        UomId = data.UomId;
        UomFactor = data.UomId == item.BaseUomId ? 1m
            : item.Conversions.FirstOrDefault(c => c.UomId == data.UomId)?.Factor
              ?? throw new BusinessRuleException($"{prefix}: ĐVT phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.");
        (Qty01, Qty02, Qty03, Qty04, Qty05, Qty06) = (data.Months[0], data.Months[1], data.Months[2], data.Months[3], data.Months[4], data.Months[5]);
        (Qty07, Qty08, Qty09, Qty10, Qty11, Qty12) = (data.Months[6], data.Months[7], data.Months[8], data.Months[9], data.Months[10], data.Months[11]);
        Note = note;
    }
}
