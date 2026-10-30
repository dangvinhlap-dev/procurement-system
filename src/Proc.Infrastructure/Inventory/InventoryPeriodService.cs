using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Inventory;

/// <summary>
/// Kỳ kho (design.md mục 7, 7.6). Bảng InventoryPeriod có CompanyId nên mọi truy vấn tự lọc theo công ty đang làm việc.
/// </summary>
public sealed partial class InventoryPeriodService : IInventoryPeriodService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="clock">Đồng hồ (năm mặc định khi chưa có lịch).</param>
    public InventoryPeriodService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<InventoryCalendarDto> GetCalendarAsync(int? year, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var periods = await _db.InventoryPeriods.AsNoTracking().ToListAsync(cancellationToken);
        var years = periods.Where(p => p.IsYear).Select(p => p.Year).Order().ToList();
        var nextYear = years.Count > 0 ? years[^1] + 1 : _clock.TodayInVietnam().Year;
        if (years.Count == 0) return new InventoryCalendarDto(years, null, [], nextYear);

        // Mặc định xem năm có kỳ tháng đang mở gần nhất – năm người dùng đang nhập liệu.
        var shown = year ?? periods.Where(p => !p.IsYear && p.Status == PeriodStatus.Open).Select(p => (int?)p.Year).Max() ?? years[^1];
        var canOpen = _scope.HasPermission(Permissions.Inventory.PeriodOpen);
        var canClose = _scope.HasPermission(Permissions.Inventory.PeriodClose);
        var canReopen = _scope.HasPermission(Permissions.Inventory.PeriodReopen);
        var company = await CompanyAsync(cancellationToken);
        var rows = periods
            .Where(p => p.Year == shown)
            .OrderBy(p => p.Month)
            .Select(p => ToDto(
                p,
                canOpen && OpenBlocker(p, periods, company.StartDate) is null,
                canClose && CloseBlocker(p, periods) is null,
                canReopen && ReopenBlocker(p, periods) is null))
            .ToList();
        return new InventoryCalendarDto(years, shown, rows, nextYear);
    }

    /// <inheritdoc />
    public async Task<InventoryCalendarDto> CreateYearAsync(CreateInventoryYearRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var years = await _db.InventoryPeriods.Where(p => p.Month == 0).Select(p => p.Year).ToListAsync(cancellationToken);
        if (years.Contains(request.Year)) throw new BusinessRuleException($"Đã có lịch kỳ kho năm {request.Year}.");
        // Lịch kỳ liền mạch, không có năm trống ở giữa: số dư cuối năm này là số dư đầu năm sau (design.md mục 7).
        if (years.Count > 0 && request.Year != years.Max() + 1)
            throw new BusinessRuleException($"Lịch kỳ kho phải liền nhau: năm tiếp theo cần tạo là {years.Max() + 1}.");

        _db.InventoryPeriods.AddRange(InventoryPeriod.CreateYear(companyId, request.Year));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetCalendarAsync(request.Year, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryPeriodDto> OpenAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var periods = await _db.InventoryPeriods.ToListAsync(cancellationToken);
        var period = periods.SingleOrDefault(p => p.Id == id) ?? throw new NotFoundException("Không tìm thấy kỳ kho.");
        var company = await CompanyAsync(cancellationToken);
        if (OpenBlocker(period, periods, company.StartDate) is { } reason) throw new BusinessRuleException(reason);

        period.Open();
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(period, canOpen: false);
    }

    /// <inheritdoc />
    public async Task<Guid> RequireOpenPeriodAsync(DateOnly documentDate, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var period = await _db.InventoryPeriods.AsNoTracking()
            .Where(p => p.Month > 0 && p.StartDate <= documentDate && p.EndDate >= documentDate)
            .SingleOrDefaultAsync(cancellationToken);
        var date = documentDate.ToString("dd/MM/yyyy");
        return period switch
        {
            null => throw new BusinessRuleException($"Chưa có lịch kỳ kho năm {documentDate.Year} cho ngày {date}. Tạo lịch ở Hệ thống → Kỳ kho."),
            { Status: PeriodStatus.Open } => period.Id,
            { Status: PeriodStatus.NotOpened } => throw new BusinessRuleException($"Kỳ kho {period.Label} (ngày {date}) chưa mở."),
            { Status: PeriodStatus.Costing } => throw new BusinessRuleException($"Kỳ kho {period.Label} đang tính giá vốn, tạm khóa ghi sổ."),
            // Kỳ đã đóng không sửa trực tiếp – lập chứng từ điều chỉnh ở kỳ đang mở (CLAUDE.md mục 4).
            _ => throw new BusinessRuleException($"Kỳ kho {period.Label} đã đóng – không ghi sổ được. Lập chứng từ điều chỉnh ở kỳ đang mở."),
        };
    }

    /// <summary>
    /// Lý do chưa mở được một kỳ tháng (null = mở được). Mở tuần tự: kỳ đầu tiên của công ty tự chọn (không trước tháng bắt đầu
    /// dùng hệ thống), các kỳ sau phải liền sau kỳ đã mở gần nhất – không để kỳ trống ở giữa, vì số dư cuối kỳ trước là đầu kỳ sau
    /// (design.md mục 7, 7.6).
    /// </summary>
    private static string? OpenBlocker(InventoryPeriod period, IReadOnlyList<InventoryPeriod> all, DateOnly? companyStart)
    {
        if (period.IsYear || period.Status != PeriodStatus.NotOpened) return $"Kỳ {period.Label} không ở trạng thái Chưa mở.";
        var year = all.Single(p => p.IsYear && p.Year == period.Year);
        if (year.Status != PeriodStatus.Open) return $"Kỳ năm {period.Year} đã đóng.";

        var opened = all.Where(p => !p.IsYear && p.Status != PeriodStatus.NotOpened).ToList();
        if (opened.Count == 0)
        {
            return companyStart is { } start && period.EndDate < start
                ? $"Công ty bắt đầu dùng hệ thống từ {start:dd/MM/yyyy}; không mở kỳ trước tháng đó."
                : null;
        }

        var latest = opened.MaxBy(p => p.StartDate)!;
        if (period.StartDate == latest.StartDate.AddMonths(1)) return null;
        return period.StartDate < latest.StartDate
            ? $"Kỳ {period.Label} nằm trước kỳ đầu tiên đã mở; không mở ngược về trước."
            : $"Phải mở kỳ {latest.StartDate.AddMonths(1):MM/yyyy} trước (mở kỳ tuần tự theo tháng).";
    }

    private async Task<Company> CompanyAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        return await _db.Companies.AsNoTracking().SingleAsync(c => c.Id == companyId, cancellationToken);
    }

    private static InventoryPeriodDto ToDto(InventoryPeriod p, bool canOpen, bool canClose = false, bool canReopen = false) =>
        new(p.Id, p.Year, p.Month, p.Label, p.StartDate, p.EndDate, p.Status.ToString(), p.ClosedAt, canOpen, canClose, canReopen, p.ReopenReason);
}
