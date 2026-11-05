namespace Proc.Application.Purchasing;

/// <summary>Một tháng ngân sách của bộ phận.</summary>
/// <param name="Month">Tháng 1–12.</param>
/// <param name="Budget">Ngân sách; null = tháng này bộ phận không kiểm soát ngân sách.</param>
/// <param name="Used">Đã dùng (PO + phần PR chưa lên PO – design.md mục 18.24).</param>
/// <param name="Remaining">Còn lại = ngân sách − đã dùng (âm khi vượt); null khi không có ngân sách.</param>
public sealed record BudgetMonthDto(int Month, decimal? Budget, decimal Used, decimal? Remaining);

/// <summary>Ngân sách cả năm của một bộ phận.</summary>
/// <param name="DepartmentId">Bộ phận.</param>
/// <param name="DepartmentCode">Mã bộ phận.</param>
/// <param name="DepartmentName">Tên bộ phận.</param>
/// <param name="IsActive">Bộ phận còn dùng.</param>
/// <param name="Months">12 tháng, theo thứ tự 1 → 12.</param>
/// <param name="TotalBudget">Tổng ngân sách các tháng có ngân sách.</param>
/// <param name="TotalUsed">Tổng đã dùng cả năm.</param>
/// <param name="OverMonths">Số tháng vượt ngân sách (đã dùng &gt; ngân sách).</param>
public sealed record DepartmentBudgetYearDto(
    Guid DepartmentId, string DepartmentCode, string DepartmentName, bool IsActive, IReadOnlyList<BudgetMonthDto> Months, decimal TotalBudget,
    decimal TotalUsed, int OverMonths);

/// <summary>Lưu ngân sách 12 tháng của một bộ phận.</summary>
/// <param name="Months">Đúng 12 phần tử (tháng 1 → 12); null = bỏ ngân sách tháng đó (không kiểm soát).</param>
public sealed record SaveDepartmentBudgetRequest(IReadOnlyList<decimal?> Months);

/// <summary>Kiểm tra ngân sách của một PR/PO (design.md mục 18.24), hiện ở đầu phiếu và cho người duyệt.</summary>
/// <param name="Year">Năm của ngày phiếu.</param>
/// <param name="Month">Tháng của ngày phiếu.</param>
/// <param name="BudgetAmount">Ngân sách bộ phận × tháng lúc kiểm tra.</param>
/// <param name="UsedAmount">Đã dùng của các phiếu khác lúc kiểm tra.</param>
/// <param name="DocumentAmount">Giá trị phiếu tính vào ngân sách (chưa VAT).</param>
/// <param name="IsOver">Vượt ngân sách.</param>
/// <param name="OverAmount">Số vượt = đã dùng + phiếu − ngân sách (0 khi không vượt).</param>
public sealed record BudgetCheckDto(
    int Year, int Month, decimal BudgetAmount, decimal UsedAmount, decimal DocumentAmount, bool IsOver, decimal OverAmount);

/// <summary>
/// Ngân sách mua hàng theo bộ phận × tháng của công ty đang làm việc (lát C3, design.md mục 18.24). Bộ phận, phiếu của công ty khác như
/// không tồn tại (global query filter).
/// </summary>
public interface IBudgetService
{
    /// <summary>Ngân sách và đã dùng của mọi bộ phận (đang dùng, hoặc đã ngừng mà có ngân sách) trong một năm.</summary>
    /// <param name="year">Năm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi năm sai.</exception>
    Task<IReadOnlyList<DepartmentBudgetYearDto>> YearAsync(int year, CancellationToken cancellationToken);

    /// <summary>Lưu ngân sách 12 tháng của một bộ phận; trả lại dòng của bộ phận sau khi lưu.</summary>
    /// <param name="departmentId">Bộ phận của công ty đang làm việc.</param>
    /// <param name="year">Năm.</param>
    /// <param name="request">12 tháng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi không đủ 12 tháng, số âm hoặc năm sai.</exception>
    /// <exception cref="Proc.Application.Common.NotFoundException">Khi không thấy bộ phận.</exception>
    Task<DepartmentBudgetYearDto> SaveAsync(Guid departmentId, int year, SaveDepartmentBudgetRequest request, CancellationToken cancellationToken);
}
