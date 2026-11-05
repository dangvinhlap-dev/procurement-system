using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Ngân sách bộ phận × tháng (lát C3, design.md mục 18.24). Mọi truy vấn đi qua global query filter công ty – bộ phận, ngân sách, phiếu
/// của công ty khác như không tồn tại.
/// </summary>
public sealed class BudgetService : IBudgetService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly BudgetUsage _usage;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người thao tác.</param>
    public BudgetService(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
        _usage = new BudgetUsage(db);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentBudgetYearDto>> YearAsync(int year, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        DepartmentBudget.EnsurePeriod(year, 1);
        return await BuildAsync(year, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DepartmentBudgetYearDto> SaveAsync(Guid departmentId, int year, SaveDepartmentBudgetRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        DepartmentBudget.EnsurePeriod(year, 1);
        if (request.Months is not { Count: 12 }) throw new BusinessRuleException("Ngân sách phải gửi đủ 12 tháng.");
        if (!await _db.Departments.AnyAsync(d => d.Id == departmentId, cancellationToken)) throw new NotFoundException("Không tìm thấy bộ phận.");

        var existing = await _db.DepartmentBudgets.Where(b => b.DepartmentId == departmentId && b.Year == year).ToListAsync(cancellationToken);
        for (var month = 1; month <= 12; month++)
        {
            var amount = request.Months[month - 1];
            var row = existing.SingleOrDefault(b => b.Month == month);
            // Bỏ trống = tháng đó không kiểm soát ngân sách: xóa dòng (nhật ký vẫn giữ giá trị cũ). Phiếu đã gửi giữ số liệu lúc gửi.
            if (amount is null)
            {
                if (row is not null) _db.DepartmentBudgets.Remove(row);
            }
            else if (row is null) _db.DepartmentBudgets.Add(DepartmentBudget.Create(companyId, departmentId, year, month, amount.Value));
            else row.SetAmount(amount.Value);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await BuildAsync(year, departmentId, cancellationToken)).Single();
    }

    private async Task<List<DepartmentBudgetYearDto>> BuildAsync(int year, Guid? departmentId, CancellationToken cancellationToken)
    {
        var budgets = await _db.DepartmentBudgets.AsNoTracking()
            .Where(b => b.Year == year && (departmentId == null || b.DepartmentId == departmentId))
            .ToListAsync(cancellationToken);
        var withBudget = budgets.Select(b => b.DepartmentId).ToHashSet();
        var used = await _usage.UsedAsync(year, departmentId, null, null, cancellationToken);
        var withUsage = used.Keys.Select(k => k.DepartmentId).ToHashSet();
        // Bộ phận ngừng dùng vẫn hiện khi năm đó còn ngân sách hoặc phiếu – không mất số liệu cũ.
        var departments = await _db.Departments.AsNoTracking()
            .Where(d => departmentId == null || d.Id == departmentId)
            .OrderBy(d => d.Code)
            .Select(d => new { d.Id, d.Code, d.Name, d.IsActive })
            .ToListAsync(cancellationToken);
        return departments
            .Where(d => d.IsActive || withBudget.Contains(d.Id) || withUsage.Contains(d.Id) || departmentId is not null)
            .Select(d =>
            {
                var months = Enumerable.Range(1, 12).Select(m =>
                {
                    var budget = budgets.SingleOrDefault(b => b.DepartmentId == d.Id && b.Month == m)?.Amount;
                    var spent = used.GetValueOrDefault((d.Id, m));
                    return new BudgetMonthDto(m, budget, spent, budget - spent);
                }).ToList();
                return new DepartmentBudgetYearDto(
                    d.Id, d.Code, d.Name, d.IsActive, months, months.Sum(m => m.Budget ?? 0m), months.Sum(m => m.Used),
                    months.Count(m => m.Remaining < 0));
            })
            .ToList();
    }

    /// <summary>
    /// Số liệu ngân sách đã lưu trên phiếu (lúc lưu/gửi duyệt gần nhất) để hiện ở đầu phiếu; null khi phiếu không kiểm soát ngân sách.
    /// </summary>
    /// <param name="document">PR hoặc PO.</param>
    /// <returns>Số liệu kiểm tra.</returns>
    public static BudgetCheckDto? ToDto(ApprovableDocument document)
    {
        if (document.BudgetAmount is not { } budget) return null;
        var used = document.BudgetUsedAmount ?? 0m;
        var over = Math.Max(0m, used + document.BudgetedAmount - budget);
        return new BudgetCheckDto(document.Date.Year, document.Date.Month, budget, used, document.BudgetedAmount, document.IsOverBudget, over);
    }
}
