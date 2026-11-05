using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>
/// Ngân sách mua hàng của một bộ phận trong một tháng (lát C3, design.md mục 18.24 – quyết định Q5): công ty × bộ phận × tháng.
/// Không có dòng = tháng đó bộ phận không kiểm soát ngân sách. Vượt ngân sách không chặn PR/PO mà thêm chặng duyệt "vượt ngân sách".
/// </summary>
[Auditable("Ngân sách bộ phận", LabelProperty = nameof(Label))]
public sealed class DepartmentBudget : AuditableEntity, ICompanyOwned
{
    private DepartmentBudget()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Bộ phận (cùng công ty).</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>Năm.</summary>
    public int Year { get; private set; }

    /// <summary>Tháng 1–12.</summary>
    public int Month { get; private set; }

    /// <summary>Số tiền ngân sách (VND, chưa VAT) – numeric(19,2), không âm.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Nhãn trong nhật ký, ví dụ "05/2026".</summary>
    public string Label => $"{Month:00}/{Year}";

    /// <summary>
    /// Lập ngân sách tháng.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="departmentId">Bộ phận.</param>
    /// <param name="year">Năm 2000–2100.</param>
    /// <param name="month">Tháng 1–12.</param>
    /// <param name="amount">Số tiền ≥ 0 (làm tròn 2 số lẻ).</param>
    /// <returns>Dòng ngân sách.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty/bộ phận, năm/tháng sai hoặc số tiền âm.</exception>
    public static DepartmentBudget Create(Guid companyId, Guid departmentId, int year, int month, decimal amount)
    {
        if (companyId == Guid.Empty || departmentId == Guid.Empty) throw new BusinessRuleException("Ngân sách phải thuộc một bộ phận của công ty.");
        EnsurePeriod(year, month);
        var budget = new DepartmentBudget { CompanyId = companyId, DepartmentId = departmentId, Year = year, Month = month };
        budget.SetAmount(amount);
        return budget;
    }

    /// <summary>Đổi số tiền ngân sách.</summary>
    /// <param name="amount">Số tiền ≥ 0.</param>
    /// <exception cref="BusinessRuleException">Khi số tiền âm.</exception>
    public void SetAmount(decimal amount)
    {
        if (amount < 0) throw new BusinessRuleException($"Ngân sách tháng {Month:00}/{Year} không được âm.");
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Kiểm tra năm, tháng hợp lệ.</summary>
    /// <exception cref="BusinessRuleException">Khi năm ngoài 2000–2100 hoặc tháng ngoài 1–12.</exception>
    public static void EnsurePeriod(int year, int month)
    {
        if (year is < 2000 or > 2100) throw new BusinessRuleException("Năm ngân sách phải từ 2000 đến 2100.");
        if (month is < 1 or > 12) throw new BusinessRuleException("Tháng ngân sách phải từ 1 đến 12.");
    }
}

/// <summary>
/// Kết quả kiểm tra ngân sách của một phiếu (design.md mục 18.24) lúc lưu/gửi duyệt.
/// </summary>
/// <param name="BudgetAmount">Ngân sách bộ phận × tháng của ngày phiếu.</param>
/// <param name="UsedAmount">Đã dùng bởi các phiếu khác (không tính phiếu đang xét).</param>
public sealed record BudgetCheck(decimal BudgetAmount, decimal UsedAmount);
