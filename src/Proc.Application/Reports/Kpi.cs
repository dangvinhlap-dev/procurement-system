namespace Proc.Application.Reports;

/// <summary>Một chỉ số trên bảng KPI (design.md mục 18.18).</summary>
/// <param name="Code">Mã chỉ số (tên <see cref="Proc.Domain.Organization.KpiCode"/>, ví dụ "ContractCompliance").</param>
/// <param name="Name">Tên chỉ số.</param>
/// <param name="Formula">Cách đo.</param>
/// <param name="HigherIsBetter">true: đạt khi ≥ mục tiêu; false: đạt khi ≤ mục tiêu.</param>
/// <param name="IsCount">true: số vụ; false: tỷ lệ %.</param>
/// <param name="Target">Mục tiêu của công ty (tham số hệ thống nhóm KPI).</param>
/// <param name="Measured">false: chỉ số chưa đo được trong phần mềm (chờ lát sau) – Value luôn null.</param>
/// <param name="Value">Kết quả kỳ này (%, 2 số lẻ); null khi chưa đo hoặc kỳ không có dữ liệu (mẫu số = 0).</param>
/// <param name="PreviousValue">Kết quả kỳ trước liền trước; null khi không có hoặc chỉ số tính tại hôm nay.</param>
/// <param name="Numerator">Tử số kỳ này (tiền hoặc số dòng theo <paramref name="Unit"/>).</param>
/// <param name="Denominator">Mẫu số kỳ này.</param>
/// <param name="Unit">Đơn vị tử/mẫu số: Money (VND) hoặc Count (số dòng/số vụ).</param>
/// <param name="Met">Đạt mục tiêu; null khi chưa có kết quả.</param>
/// <param name="Note">Ghi chú: phạm vi tính, lý do chưa đo.</param>
/// <param name="Report">Mã màn hình báo cáo chi tiết (PRCW, SLA, QCDEV…); null khi không có.</param>
public sealed record KpiRowDto(
    string Code, string Name, string Formula, bool HigherIsBetter, bool IsCount, decimal Target, bool Measured, decimal? Value,
    decimal? PreviousValue, decimal? Numerator, decimal? Denominator, string Unit, bool? Met, string? Note, string? Report);

/// <summary>Bảng KPI của công ty đang làm việc trong một kỳ.</summary>
/// <param name="From">Từ ngày.</param>
/// <param name="To">Đến ngày.</param>
/// <param name="PreviousFrom">Từ ngày kỳ trước (so sánh).</param>
/// <param name="PreviousTo">Đến ngày kỳ trước.</param>
/// <param name="MeasuredCount">Số chỉ số có kết quả.</param>
/// <param name="MetCount">Số chỉ số đạt mục tiêu.</param>
/// <param name="Items">9 chỉ số theo thứ tự đề xuất Ban CUVT.</param>
public sealed record KpiResult(
    DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo, int MeasuredCount, int MetCount, IReadOnlyList<KpiRowDto> Items);

/// <summary>Bảng KPI Ban Cung ứng (design.md mục 18.18).</summary>
public interface IKpiService
{
    /// <summary>
    /// Tính 9 KPI của công ty đang làm việc trong khoảng ngày và kỳ trước liền trước, so với mục tiêu ở tham số công ty.
    /// </summary>
    /// <param name="from">Từ ngày.</param>
    /// <param name="to">Đến ngày (tối đa hai năm).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng KPI.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khoảng ngày sai hoặc quá hai năm.</exception>
    Task<KpiResult> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
