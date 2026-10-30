using Proc.Domain.Common;

namespace Proc.Domain.Inventory;

/// <summary>Trạng thái kỳ kho (design.md mục 7.1).</summary>
public enum PeriodStatus
{
    /// <summary>Chưa mở: kỳ tương lai, chưa nhận chứng từ.</summary>
    NotOpened = 1,

    /// <summary>Đang mở: được lập, sửa, ghi sổ chứng từ.</summary>
    Open = 2,

    /// <summary>Đang tính giá: khóa ghi sổ tạm thời trong lúc tính giá vốn (giai đoạn 3).</summary>
    Costing = 3,

    /// <summary>Đã đóng: có giá vốn thực tế, số dư cuối kỳ; không sửa được chứng từ.</summary>
    Closed = 4,

    /// <summary>Khóa vĩnh viễn sau quyết toán/kiểm toán, không mở lại được.</summary>
    Locked = 5,
}

/// <summary>
/// Kỳ kho của một công ty (design.md mục 7): mỗi năm một kỳ năm (Month = 0) và 12 kỳ tháng. Chứng từ kho chỉ ghi sổ được khi
/// ngày chứng từ rơi vào kỳ tháng đang mở của công ty đó (CLAUDE.md mục 4).
/// </summary>
[Auditable("Kỳ kho", LabelProperty = nameof(Label))]
public sealed class InventoryPeriod : AuditableEntity, ICompanyOwned
{
    /// <summary>Năm nhỏ nhất được tạo lịch.</summary>
    public const int MinYear = 2000;

    /// <summary>Năm lớn nhất được tạo lịch.</summary>
    public const int MaxYear = 2100;

    private InventoryPeriod()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Năm.</summary>
    public int Year { get; private set; }

    /// <summary>Tháng 1–12; 0 = kỳ năm.</summary>
    public int Month { get; private set; }

    /// <summary>Ngày đầu kỳ.</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Ngày cuối kỳ.</summary>
    public DateOnly EndDate { get; private set; }

    /// <summary>Trạng thái.</summary>
    public PeriodStatus Status { get; private set; }

    /// <summary>Thời điểm đóng kỳ (UTC).</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Người đóng kỳ.</summary>
    public Guid? ClosedBy { get; private set; }

    /// <summary>Lý do mở lại kỳ gần nhất (bắt buộc khi mở lại – design.md mục 7.4).</summary>
    public string? ReopenReason { get; private set; }

    /// <summary>Là kỳ năm.</summary>
    public bool IsYear => Month == 0;

    /// <summary>Nhãn hiển thị: "10/2026" hoặc "Năm 2026".</summary>
    public string Label => IsYear ? $"Năm {Year}" : $"{Month:00}/{Year}";

    /// <summary>
    /// Tạo lịch kỳ một năm: kỳ năm ở trạng thái Đang mở (năm đang dùng) và 12 kỳ tháng Chưa mở.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="year">Năm (2000–2100).</param>
    /// <returns>13 kỳ: kỳ năm trước, rồi tháng 1 → 12.</returns>
    /// <exception cref="BusinessRuleException">Khi năm ngoài khoảng cho phép.</exception>
    public static IReadOnlyList<InventoryPeriod> CreateYear(Guid companyId, int year)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Kỳ kho phải thuộc một công ty.");
        if (year is < MinYear or > MaxYear) throw new BusinessRuleException($"Năm kỳ kho phải từ {MinYear} đến {MaxYear}.");

        var periods = new List<InventoryPeriod>
        {
            new()
            {
                CompanyId = companyId, Year = year, Month = 0, Status = PeriodStatus.Open,
                StartDate = new DateOnly(year, 1, 1), EndDate = new DateOnly(year, 12, 31),
            },
        };
        for (var month = 1; month <= 12; month++)
        {
            var start = new DateOnly(year, month, 1);
            periods.Add(new InventoryPeriod
            {
                CompanyId = companyId, Year = year, Month = month, Status = PeriodStatus.NotOpened,
                StartDate = start, EndDate = start.AddMonths(1).AddDays(-1),
            });
        }

        return periods;
    }

    /// <summary>
    /// Mở kỳ tháng để nhận chứng từ. Thứ tự mở (tuần tự theo tháng) cần biết các kỳ khác nên tầng Infrastructure kiểm tra.
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi là kỳ năm hoặc kỳ không ở trạng thái Chưa mở.</exception>
    public void Open()
    {
        if (IsYear) throw new BusinessRuleException("Kỳ năm tự mở khi tạo lịch; chỉ mở từng kỳ tháng.");
        if (Status != PeriodStatus.NotOpened) throw new BusinessRuleException($"Kỳ {Label} không ở trạng thái Chưa mở.");
        Status = PeriodStatus.Open;
    }

    /// <summary>
    /// Bắt đầu đóng kỳ: chuyển Đang tính giá, khóa ghi sổ kỳ này trong lúc tính giá vốn (design.md mục 7.2 bước 2).
    /// Điều kiện liên quan kỳ khác (kỳ trước đã đóng) do tầng Infrastructure kiểm tra.
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi là kỳ năm hoặc kỳ không Đang mở.</exception>
    public void StartCosting()
    {
        if (IsYear) throw new BusinessRuleException("Chỉ đóng từng kỳ tháng.");
        if (Status != PeriodStatus.Open) throw new BusinessRuleException($"Kỳ {Label} không ở trạng thái Đang mở.");
        Status = PeriodStatus.Costing;
    }

    /// <summary>Tính giá lỗi: trả kỳ về Đang mở để người dùng sửa rồi đóng lại.</summary>
    public void CancelCosting()
    {
        if (Status == PeriodStatus.Costing) Status = PeriodStatus.Open;
    }

    /// <summary>Kết thúc đóng kỳ (design.md mục 7.2 bước 6).</summary>
    /// <param name="userId">Người đóng.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi kỳ không ở trạng thái Đang tính giá.</exception>
    public void Close(Guid userId, DateTimeOffset at)
    {
        if (Status != PeriodStatus.Costing) throw new BusinessRuleException($"Kỳ {Label} chưa qua bước tính giá.");
        Status = PeriodStatus.Closed;
        ClosedAt = at;
        ClosedBy = userId;
    }

    /// <summary>
    /// Mở lại kỳ đã đóng (design.md mục 7.4). "Chỉ kỳ đã đóng gần nhất" cần biết các kỳ khác nên tầng Infrastructure kiểm tra.
    /// </summary>
    /// <param name="reason">Lý do (bắt buộc, ghi nhật ký).</param>
    /// <exception cref="BusinessRuleException">Khi thiếu lý do, kỳ chưa đóng hoặc đã khóa vĩnh viễn.</exception>
    public void Reopen(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Phải nhập lý do mở lại kỳ.");
        if (reason.Trim().Length > 500) throw new BusinessRuleException("Lý do mở lại kỳ tối đa 500 ký tự.");
        if (Status == PeriodStatus.Locked) throw new BusinessRuleException($"Kỳ {Label} đã khóa vĩnh viễn, không mở lại được.");
        if (Status != PeriodStatus.Closed) throw new BusinessRuleException($"Kỳ {Label} chưa đóng.");
        Status = PeriodStatus.Open;
        ReopenReason = reason.Trim();
        ClosedAt = null;
        ClosedBy = null;
    }

    /// <summary>
    /// Ngày có thuộc kỳ này không.
    /// </summary>
    /// <param name="date">Ngày chứng từ.</param>
    /// <returns>true nếu StartDate ≤ date ≤ EndDate.</returns>
    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;
}
