using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Pricing;

/// <summary>Nội dung ghi nhận một khoản chiết khấu sản lượng gộp NCC thực trả.</summary>
/// <param name="PeriodFrom">Ngày bắt đầu kỳ tính bậc (phải là đầu một kỳ của thỏa thuận).</param>
/// <param name="ReceivedOn">Ngày nhận.</param>
/// <param name="Amount">Số tiền thực nhận gộp (hàng quy ra tiền), VND.</param>
/// <param name="Form">Hình thức.</param>
/// <param name="RefNo">Số chứng từ đối chiếu.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record VolumeRebateReceiptData(
    DateOnly PeriodFrom, DateOnly ReceivedOn, decimal Amount, RebateReceiptForm Form, string? RefNo, string? Note);

/// <summary>
/// Một lần ghi nhận chiết khấu sản lượng gộp NCC thực trả cho một kỳ của thỏa thuận (lát G6b – design.md mục 18.25). Thuộc tổng công ty (người
/// thương lượng và đối chiếu với NCC); phần của từng công ty áp dụng chia theo tỷ lệ doanh số kỳ <b>lúc ghi</b> và lưu cố định trong
/// <see cref="Shares"/> – kế toán từng pháp nhân hạch toán theo số đó, không đổi khi PO về sau thay đổi. Không xóa: ghi sai thì hủy có lý do
/// rồi ghi lại (KSNB đối chiếu được – mục 18.19).
/// </summary>
[Auditable("Chiết khấu sản lượng gộp thực nhận")]
public sealed class VolumeRebateReceipt : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài số chứng từ tối đa.</summary>
    public const int RefNoMaxLength = 100;

    /// <summary>Độ dài ghi chú, lý do hủy tối đa.</summary>
    public const int NoteMaxLength = 500;

    private readonly List<VolumeRebateReceiptShare> _shares = [];

    private VolumeRebateReceipt()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Thỏa thuận.</summary>
    public Guid AgreementId { get; private set; }

    /// <summary>Ngày bắt đầu kỳ tính bậc – khóa nhận diện kỳ.</summary>
    public DateOnly PeriodFrom { get; private set; }

    /// <summary>Ngày nhận.</summary>
    public DateOnly ReceivedOn { get; private set; }

    /// <summary>Số tiền thực nhận gộp – numeric(19,2).</summary>
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

    /// <summary>Phần của từng công ty áp dụng (tổng = <see cref="Amount"/>).</summary>
    public IReadOnlyList<VolumeRebateReceiptShare> Shares => _shares;

    /// <summary>
    /// Ghi nhận thực nhận và chia cho các công ty theo tỷ lệ doanh số kỳ (<see cref="VolumeRebateCalculation.Allocate(IReadOnlyList{VolumeRebateCompanyVolume}, decimal)"/>):
    /// phần = thực nhận × doanh số công ty ÷ doanh số gộp, làm tròn 2 số, số lẻ dồn vào công ty doanh số lớn nhất. Ví dụ nhận 100.000, ba công
    /// ty doanh số bằng nhau → 33.333,34 / 33.333,33 / 33.333,33.
    /// </summary>
    /// <param name="agreement">Thỏa thuận (đã nạp công ty áp dụng).</param>
    /// <param name="data">Nội dung.</param>
    /// <param name="periodVolumes">Doanh số từng công ty áp dụng trong kỳ, tính lúc ghi.</param>
    /// <param name="today">Hôm nay theo giờ Việt Nam.</param>
    /// <returns>Bản ghi thực nhận kèm phần từng công ty.</returns>
    /// <exception cref="BusinessRuleException">
    /// Khi thỏa thuận chưa hiệu lực, kỳ không thuộc thỏa thuận hoặc chưa kết thúc, ngày nhận không sau cuối kỳ, số tiền/hình thức sai, hoặc kỳ
    /// chưa có doanh số để chia.
    /// </exception>
    public static VolumeRebateReceipt Create(
        VolumeRebateAgreement agreement, VolumeRebateReceiptData data, IReadOnlyList<VolumeRebateCompanyVolume> periodVolumes, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(agreement);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(periodVolumes);
        // Chỉ thỏa thuận Hiệu lực mới được theo dõi chiết khấu; thỏa thuận đã hủy không còn theo dõi (design.md 18.25, G6a).
        if (agreement.Status != VolumeRebateStatus.Active)
            throw new BusinessRuleException($"Thỏa thuận {agreement.No} không ở trạng thái Hiệu lực – không ghi nhận thực nhận được.");
        var period = agreement.Periods().FirstOrDefault(p => p.From == data.PeriodFrom);
        if (period == default) throw new BusinessRuleException("Kỳ tính bậc không thuộc thỏa thuận – hãy chọn kỳ trong danh sách.");
        // Chỉ ghi thực nhận sau khi kỳ kết thúc (người dùng chốt 08/11/2026): bậc và số phải nhận chỉ chốt khi hết kỳ, ghi sớm thì phần chia
        // theo doanh số dở dang của kỳ sẽ sai. Ngày nhận cũng phải sau ngày cuối kỳ vì cùng lý do.
        if (today <= period.To)
            throw new BusinessRuleException($"Kỳ {period.From:dd/MM/yyyy} – {period.To:dd/MM/yyyy} chưa kết thúc – chỉ ghi thực nhận sau ngày cuối kỳ.");
        if (!Enum.IsDefined(data.Form)) throw new BusinessRuleException("Hình thức nhận không hợp lệ.");
        if (data.ReceivedOn <= period.To) throw new BusinessRuleException($"Ngày nhận phải sau ngày cuối kỳ ({period.To:dd/MM/yyyy}).");
        if (data.ReceivedOn > today) throw new BusinessRuleException("Ngày nhận không được sau hôm nay.");
        if (data.Amount <= 0 || decimal.Round(data.Amount, 2) != data.Amount || data.Amount >= 10_000_000_000_000m)
            throw new BusinessRuleException("Số tiền thực nhận phải lớn hơn 0, tối đa 2 chữ số thập phân.");
        var refNo = Clean(data.RefNo);
        if (refNo?.Length > RefNoMaxLength) throw new BusinessRuleException($"Số chứng từ tối đa {RefNoMaxLength} ký tự.");
        var note = Clean(data.Note);
        if (note?.Length > NoteMaxLength) throw new BusinessRuleException($"Ghi chú tối đa {NoteMaxLength} ký tự.");

        // Chỉ chia cho công ty áp dụng có doanh số trong kỳ; kỳ không có doanh số thì không có căn cứ chia (NCC trả nhầm kỳ?).
        var applied = agreement.Companies.Select(c => c.CompanyId).ToHashSet();
        var volumes = periodVolumes.Where(v => applied.Contains(v.CompanyId) && v.Amount > 0).ToList();
        if (volumes.Count == 0)
            throw new BusinessRuleException("Kỳ này chưa có doanh số mua của công ty áp dụng – không chia thực nhận cho công ty được.");

        var receipt = new VolumeRebateReceipt
        {
            CompanyId = agreement.CompanyId, AgreementId = agreement.Id, PeriodFrom = data.PeriodFrom, ReceivedOn = data.ReceivedOn,
            Amount = data.Amount, Form = data.Form, RefNo = refNo, Note = note,
        };
        foreach (var share in VolumeRebateCalculation.Allocate(volumes, data.Amount))
            receipt._shares.Add(new VolumeRebateReceiptShare(receipt.Id, share.CompanyId, share.Amount, share.SharePercent, share.Rebate));
        return receipt;
    }

    /// <summary>Hủy ghi nhận (ghi sai) – bắt buộc lý do; bản ghi và phần chia giữ lại để tra cứu nhưng không tính.</summary>
    /// <param name="reason">Lý do.</param>
    /// <exception cref="BusinessRuleException">Khi đã hủy hoặc thiếu lý do.</exception>
    public void Cancel(string? reason)
    {
        if (IsCancelled) throw new BusinessRuleException("Bản ghi thực nhận đã hủy.");
        var clean = Clean(reason) ?? throw new BusinessRuleException("Hãy nhập lý do hủy.");
        if (clean.Length > NoteMaxLength) throw new BusinessRuleException($"Lý do hủy tối đa {NoteMaxLength} ký tự.");
        IsCancelled = true;
        CancelReason = clean;
    }

    /// <summary>Bỏ khoảng trắng hai đầu; chuỗi rỗng hoặc toàn khoảng trắng thành null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Phần thực nhận của một công ty áp dụng – chia theo tỷ lệ doanh số kỳ lúc ghi (lát G6b).</summary>
[Auditable("Phần chiết khấu sản lượng gộp thực nhận")]
public sealed class VolumeRebateReceiptShare : Entity
{
    private VolumeRebateReceiptShare()
    {
    }

    internal VolumeRebateReceiptShare(Guid receiptId, Guid companyId, decimal purchaseAmount, decimal sharePercent, decimal amount)
    {
        ReceiptId = receiptId;
        CompanyId = companyId;
        PurchaseAmount = purchaseAmount;
        SharePercent = sharePercent;
        Amount = amount;
    }

    /// <summary>Bản ghi thực nhận.</summary>
    public Guid ReceiptId { get; private set; }

    /// <summary>Công ty được chia.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Doanh số kỳ của công ty lúc ghi (căn cứ chia) – numeric(19,2).</summary>
    public decimal PurchaseAmount { get; private set; }

    /// <summary>Tỷ lệ doanh số (%, 4 số lẻ).</summary>
    public decimal SharePercent { get; private set; }

    /// <summary>Phần thực nhận được chia – numeric(19,2).</summary>
    public decimal Amount { get; private set; }
}
