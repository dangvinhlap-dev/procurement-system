using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>Loại cam kết chiết khấu/tài trợ trên hợp đồng nguyên tắc (design.md mục 18.21).</summary>
public enum RebateKind
{
    /// <summary>Chiết khấu theo doanh số/sản lượng mua trong kỳ.</summary>
    SalesVolume = 1,

    /// <summary>Tài trợ, hỗ trợ (tiền hoặc hàng) của NCC.</summary>
    Sponsorship = 2,

    /// <summary>Chiết khấu, hỗ trợ khác (thưởng thanh toán sớm…).</summary>
    Other = 3,
}

/// <summary>Cách tính số phải nhận mỗi kỳ.</summary>
public enum RebateMethod
{
    /// <summary>% trên doanh số mua của kỳ.</summary>
    Percent = 1,

    /// <summary>Số tiền cố định mỗi kỳ.</summary>
    FixedAmount = 2,
}

/// <summary>Kỳ quyết toán chiết khấu/tài trợ.</summary>
public enum RebatePeriod
{
    /// <summary>Theo quý dương lịch.</summary>
    Quarter = 1,

    /// <summary>Theo năm dương lịch.</summary>
    Year = 2,

    /// <summary>Một lần cho cả thời hạn hợp đồng.</summary>
    WholeContract = 3,
}

/// <summary>Hình thức NCC trả chiết khấu/tài trợ.</summary>
public enum RebateReceiptForm
{
    /// <summary>Giảm trừ công nợ phải trả.</summary>
    DebtOffset = 1,

    /// <summary>Chuyển khoản/tiền mặt.</summary>
    Transfer = 2,

    /// <summary>Hàng tặng, hàng khuyến mại (quy ra tiền).</summary>
    Goods = 3,

    /// <summary>Hình thức khác.</summary>
    Other = 4,
}

/// <summary>Nội dung một cam kết chiết khấu/tài trợ khi lưu hợp đồng.</summary>
/// <param name="Id">Id cam kết đang có (null = mới).</param>
/// <param name="Kind">Loại.</param>
/// <param name="Method">Cách tính.</param>
/// <param name="Value">% (Percent, 0 &lt; % ≤ 100, 4 số lẻ) hoặc số tiền mỗi kỳ (FixedAmount, &gt; 0, 2 số lẻ).</param>
/// <param name="MinPurchaseAmount">Doanh số tối thiểu trong kỳ mới được hưởng (VND, null = không điều kiện).</param>
/// <param name="Period">Kỳ quyết toán.</param>
/// <param name="Description">Nội dung cam kết theo hợp đồng.</param>
public sealed record ContractRebateData(
    Guid? Id, RebateKind Kind, RebateMethod Method, decimal Value, decimal? MinPurchaseAmount, RebatePeriod Period, string Description);

/// <summary>
/// Cam kết chiết khấu doanh số/tài trợ ghi trên hợp đồng nguyên tắc (lát F4b – đề xuất Ban CUVT P10: ghi nhận minh bạch). Là một phần nội
/// dung hợp đồng nên chỉ sửa khi hợp đồng còn Nháp, đi qua duyệt cùng hợp đồng.
/// </summary>
[Auditable("Cam kết chiết khấu/tài trợ hợp đồng")]
public sealed class PurchaseContractRebate : Entity
{
    /// <summary>Độ dài nội dung tối đa.</summary>
    public const int DescriptionMaxLength = 500;

    private PurchaseContractRebate()
    {
    }

    internal PurchaseContractRebate(Guid contractId) => ContractId = contractId;

    /// <summary>Hợp đồng.</summary>
    public Guid ContractId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Loại.</summary>
    public RebateKind Kind { get; private set; }

    /// <summary>Cách tính.</summary>
    public RebateMethod Method { get; private set; }

    /// <summary>% trên doanh số (cách tính Percent).</summary>
    public decimal? Rate { get; private set; }

    /// <summary>Số tiền mỗi kỳ (cách tính FixedAmount) – numeric(19,2).</summary>
    public decimal? Amount { get; private set; }

    /// <summary>Doanh số tối thiểu trong kỳ để được hưởng (null = không điều kiện).</summary>
    public decimal? MinPurchaseAmount { get; private set; }

    /// <summary>Kỳ quyết toán.</summary>
    public RebatePeriod Period { get; private set; }

    /// <summary>Nội dung cam kết.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Các kỳ quyết toán trong thời hạn [<paramref name="from"/>, <paramref name="to"/>] của hợp đồng, cắt theo thời hạn. Ví dụ hiệu lực
    /// 15/02/2026 – 10/08/2026, theo quý → 15/02–31/03, 01/04–30/06, 01/07–10/08; theo năm → 15/02–10/08; cả hợp đồng → 15/02–10/08.
    /// </summary>
    /// <param name="from">Ngày bắt đầu hiệu lực.</param>
    /// <param name="to">Ngày kết thúc (hết hiệu lực hoặc ngày chấm dứt).</param>
    /// <returns>Các kỳ theo thứ tự thời gian.</returns>
    public IReadOnlyList<(DateOnly From, DateOnly To)> PeriodsWithin(DateOnly from, DateOnly to)
    {
        var periods = new List<(DateOnly, DateOnly)>();
        if (to < from) return periods;
        if (Period == RebatePeriod.WholeContract) return [(from, to)];
        var start = from;
        while (start <= to)
        {
            var end = Period == RebatePeriod.Quarter
                ? new DateOnly(start.Year, (start.Month - 1) / 3 * 3 + 1, 1).AddMonths(3).AddDays(-1)
                : new DateOnly(start.Year, 12, 31);
            if (end > to) end = to;
            periods.Add((start, end));
            start = end.AddDays(1);
        }

        return periods;
    }

    /// <summary>
    /// Số phải nhận của một kỳ theo doanh số mua: chưa đạt doanh số tối thiểu → 0; theo % = doanh số × % ÷ 100 làm tròn 2 số; cố định = số
    /// tiền mỗi kỳ (không chia theo số ngày khi kỳ bị cắt). Ví dụ doanh số 1.250.000.000, 2% → 25.000.000; tối thiểu 1.500.000.000 → 0.
    /// </summary>
    /// <param name="purchaseAmount">Doanh số mua trước VAT của kỳ.</param>
    /// <returns>Số tiền phải nhận.</returns>
    public decimal ExpectedFor(decimal purchaseAmount)
    {
        if (MinPurchaseAmount is { } min && purchaseAmount < min) return 0m;
        return Method == RebateMethod.Percent
            ? Math.Round(purchaseAmount * (Rate ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero)
            : Amount ?? 0m;
    }

    internal void Set(int lineNo, ContractRebateData data)
    {
        var prefix = $"Cam kết {lineNo}";
        if (!Enum.IsDefined(data.Kind)) throw new BusinessRuleException($"{prefix}: loại chiết khấu/tài trợ không hợp lệ.");
        if (!Enum.IsDefined(data.Method)) throw new BusinessRuleException($"{prefix}: cách tính không hợp lệ.");
        if (!Enum.IsDefined(data.Period)) throw new BusinessRuleException($"{prefix}: kỳ quyết toán không hợp lệ.");
        if (string.IsNullOrWhiteSpace(data.Description)) throw new BusinessRuleException($"{prefix}: hãy ghi nội dung cam kết.");
        if (data.Description.Trim().Length > DescriptionMaxLength) throw new BusinessRuleException($"{prefix}: nội dung tối đa {DescriptionMaxLength} ký tự.");
        if (data.Method == RebateMethod.Percent && (data.Value <= 0 || data.Value > 100 || decimal.Round(data.Value, 4) != data.Value))
            throw new BusinessRuleException($"{prefix}: tỷ lệ phải lớn hơn 0 và không quá 100%, tối đa 4 chữ số thập phân.");
        if (data.Method == RebateMethod.FixedAmount && (data.Value <= 0 || decimal.Round(data.Value, 2) != data.Value || data.Value >= 10_000_000_000_000m))
            throw new BusinessRuleException($"{prefix}: số tiền mỗi kỳ phải lớn hơn 0, tối đa 2 chữ số thập phân.");
        if (data.MinPurchaseAmount is { } min && (min < 0 || decimal.Round(min, 2) != min || min >= 10_000_000_000_000m))
            throw new BusinessRuleException($"{prefix}: doanh số tối thiểu phải ≥ 0, tối đa 2 chữ số thập phân.");
        LineNo = lineNo;
        Kind = data.Kind;
        Method = data.Method;
        Rate = data.Method == RebateMethod.Percent ? data.Value : null;
        Amount = data.Method == RebateMethod.FixedAmount ? data.Value : null;
        MinPurchaseAmount = data.MinPurchaseAmount is > 0 ? data.MinPurchaseAmount : null;
        Period = data.Period;
        Description = data.Description.Trim();
    }
}

/// <summary>Nội dung ghi nhận một khoản chiết khấu/tài trợ thực nhận.</summary>
/// <param name="PeriodFrom">Ngày bắt đầu kỳ quyết toán (phải là đầu một kỳ của cam kết).</param>
/// <param name="ReceivedOn">Ngày nhận.</param>
/// <param name="Amount">Số tiền thực nhận (hàng tặng quy ra tiền).</param>
/// <param name="Form">Hình thức.</param>
/// <param name="RefNo">Số chứng từ đối chiếu (biên bản, phiếu kế toán…).</param>
/// <param name="Note">Ghi chú.</param>
public sealed record ContractRebateReceiptData(
    DateOnly PeriodFrom, DateOnly ReceivedOn, decimal Amount, RebateReceiptForm Form, string? RefNo, string? Note);

/// <summary>
/// Một lần ghi nhận chiết khấu/tài trợ NCC thực trả cho một kỳ của cam kết trên hợp đồng (lát F4b – P10). Không xóa: ghi sai thì hủy có lý
/// do và ghi lại – KSNB đối chiếu được (mục 18.19). Mọi thay đổi vào AuditLog.
/// </summary>
[Auditable("Chiết khấu/tài trợ thực nhận")]
public sealed class ContractRebateReceipt : AuditableEntity, ICompanyOwned
{
    private ContractRebateReceipt()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Hợp đồng.</summary>
    public Guid ContractId { get; private set; }

    /// <summary>Cam kết chiết khấu/tài trợ.</summary>
    public Guid RebateId { get; private set; }

    /// <summary>
    /// Ngày bắt đầu kỳ quyết toán – khóa nhận diện kỳ. Dùng ngày bắt đầu vì không đổi khi phụ lục gia hạn hoặc chấm dứt hợp đồng (chỉ ngày
    /// cuối kỳ cuối thay đổi), nên số đã ghi nhận vẫn gắn đúng kỳ.
    /// </summary>
    public DateOnly PeriodFrom { get; private set; }

    /// <summary>Ngày nhận.</summary>
    public DateOnly ReceivedOn { get; private set; }

    /// <summary>Số tiền thực nhận – numeric(19,2).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Hình thức.</summary>
    public RebateReceiptForm Form { get; private set; }

    /// <summary>Số chứng từ đối chiếu.</summary>
    public string? RefNo { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Đã hủy (không tính vào thực nhận).</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>
    /// Ghi nhận thực nhận. Hợp đồng phải đã hiệu lực (Hiệu lực hoặc Chấm dứt); kỳ phải là một kỳ quyết toán của cam kết; ngày nhận không
    /// trước ngày bắt đầu kỳ và không sau <paramref name="today"/>.
    /// </summary>
    /// <param name="contract">Hợp đồng (đã nạp cam kết).</param>
    /// <param name="rebateId">Cam kết.</param>
    /// <param name="data">Nội dung.</param>
    /// <param name="today">Hôm nay theo giờ Việt Nam.</param>
    /// <returns>Bản ghi thực nhận.</returns>
    /// <exception cref="BusinessRuleException">Khi hợp đồng chưa hiệu lực, kỳ/ngày/số tiền sai.</exception>
    public static ContractRebateReceipt Create(PurchaseContract contract, Guid rebateId, ContractRebateReceiptData data, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(data);
        if (contract.Status is not (ContractStatus.Active or ContractStatus.Terminated))
            throw new BusinessRuleException($"Hợp đồng {contract.No} chưa hiệu lực – chưa ghi nhận chiết khấu/tài trợ thực nhận được.");
        var rebate = contract.Rebates.FirstOrDefault(r => r.Id == rebateId)
            ?? throw new BusinessRuleException("Không tìm thấy cam kết chiết khấu/tài trợ trên hợp đồng – hãy tải lại hợp đồng.");
        if (rebate.PeriodsWithin(contract.ValidFrom, contract.EndDate).All(p => p.From != data.PeriodFrom))
            throw new BusinessRuleException("Kỳ quyết toán không thuộc cam kết – hãy chọn kỳ trong danh sách.");
        if (!Enum.IsDefined(data.Form)) throw new BusinessRuleException("Hình thức nhận không hợp lệ.");
        if (data.ReceivedOn < data.PeriodFrom) throw new BusinessRuleException("Ngày nhận không được trước ngày bắt đầu kỳ.");
        if (data.ReceivedOn > today) throw new BusinessRuleException("Ngày nhận không được sau hôm nay.");
        if (data.Amount <= 0 || decimal.Round(data.Amount, 2) != data.Amount || data.Amount >= 10_000_000_000_000m)
            throw new BusinessRuleException("Số tiền thực nhận phải lớn hơn 0, tối đa 2 chữ số thập phân.");
        return new ContractRebateReceipt
        {
            CompanyId = contract.CompanyId, ContractId = contract.Id, RebateId = rebate.Id, PeriodFrom = data.PeriodFrom,
            ReceivedOn = data.ReceivedOn, Amount = data.Amount, Form = data.Form,
            RefNo = string.IsNullOrWhiteSpace(data.RefNo) ? null : data.RefNo.Trim(),
            Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim(),
        };
    }

    /// <summary>Hủy ghi nhận (ghi sai) – bắt buộc lý do; bản ghi giữ lại để tra cứu.</summary>
    /// <param name="reason">Lý do.</param>
    /// <exception cref="BusinessRuleException">Khi đã hủy hoặc thiếu lý do.</exception>
    public void Cancel(string? reason)
    {
        if (IsCancelled) throw new BusinessRuleException("Bản ghi thực nhận đã hủy.");
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Hãy nhập lý do hủy.");
        if (reason.Trim().Length > 500) throw new BusinessRuleException("Lý do hủy tối đa 500 ký tự.");
        IsCancelled = true;
        CancelReason = reason.Trim();
    }
}
