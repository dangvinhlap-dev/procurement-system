using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Pricing;

/// <summary>Trạng thái phiếu duyệt NCC và giá tháng (design.md mục 18.25 – lát G2).</summary>
public enum MonthlyApprovalStatus
{
    /// <summary>Nháp – đang soạn, sửa được.</summary>
    Draft = 1,

    /// <summary>Chờ duyệt – không sửa được; người lập rút về được.</summary>
    Submitted = 2,

    /// <summary>Đã duyệt – là kết quả tháng mà Phòng cung ứng chung phải đặt hàng theo.</summary>
    Approved = 3,

    /// <summary>Đã được thay bằng phiếu điều chỉnh đã duyệt – giữ để tra cứu.</summary>
    Replaced = 4,

    /// <summary>Đã hủy (kèm lý do) – không còn tác dụng.</summary>
    Cancelled = 5,
}

/// <summary>Đầu phiếu duyệt NCC và giá tháng.</summary>
/// <param name="Period">Tháng áp dụng – ngày đầu tháng.</param>
/// <param name="ItemCategoryId">Nhóm hàng.</param>
/// <param name="CompanyIds">Các công ty áp dụng (ít nhất một).</param>
/// <param name="Note">Ghi chú.</param>
public sealed record MonthlyApprovalHeader(DateOnly Period, Guid ItemCategoryId, IReadOnlyList<Guid> CompanyIds, string? Note);

/// <summary>Dữ liệu một dòng phiếu duyệt tháng.</summary>
/// <param name="Id">Id dòng đang có (null = dòng mới).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT của giá và hạn mức (cơ sở hoặc quy đổi).</param>
/// <param name="MainSupplierId">NCC chính.</param>
/// <param name="UnitPrice">Giá duyệt của NCC chính (VND, trước VAT, đã trừ chiết khấu).</param>
/// <param name="BackupSupplierId">NCC dự phòng (không bắt buộc).</param>
/// <param name="BackupUnitPrice">Giá duyệt của NCC dự phòng – bắt buộc khi có NCC dự phòng.</param>
/// <param name="QuantityLimit">Hạn mức số lượng trong tháng (theo ĐVT dòng, gộp các công ty áp dụng); null = không giới hạn.</param>
/// <param name="Note">Ghi chú dòng.</param>
/// <param name="StandardPrice">Giá chuẩn (ngân hàng giá – lát G4b): mốc tính tiết kiệm; không bắt buộc.</param>
/// <param name="CeilingPrice">Giá trần: giá chính, dự phòng, chuẩn, mục tiêu không vượt; PO vượt thì cảnh báo; không bắt buộc.</param>
/// <param name="TargetPrice">Giá mục tiêu để đàm phán; không bắt buộc.</param>
public sealed record MonthlyApprovalLineData(
    Guid? Id, Guid ItemId, Guid UomId, Guid MainSupplierId, decimal UnitPrice, Guid? BackupSupplierId, decimal? BackupUnitPrice,
    decimal? QuantityLimit, string? Note, decimal? StandardPrice = null, decimal? CeilingPrice = null, decimal? TargetPrice = null);

/// <summary>
/// Phiếu duyệt NCC và giá tháng – số loại <see cref="DocumentTypes.MonthlySupplierApproval"/> (design.md mục 18.25, lát G2). Quyết định thành
/// lập Ban cung ứng: mỗi tháng Ban chọn NCC cho từng nhóm hàng, kèm giá, hạn mức mua và NCC dự phòng; Phòng cung ứng chung đặt hàng cho các
/// công ty đúng kết quả đó. Phiếu lập ở tổng công ty (<see cref="CompanyId"/>) theo tháng × nhóm hàng, áp cho các công ty chọn, mỗi dòng một
/// mặt hàng. Nháp → Chờ duyệt → Đã duyệt (APRL, người lập không tự duyệt); từ chối/rút về Nháp; đã duyệt không sửa – điều chỉnh bằng phiếu mới
/// (<see cref="ReplacesId"/>), duyệt xong thì phiếu cũ thành Đã thay; hoặc hủy kèm lý do.
/// </summary>
[Auditable("Phiếu duyệt NCC và giá tháng", LabelProperty = nameof(No))]
public sealed class MonthlySupplierApproval : AuditableEntity, ICompanyOwned, IHasDocumentNumber, IApprovalSubject, IVersioned
{
    /// <summary>Phiên bản dòng (cột hệ thống xmin của PostgreSQL) – chống ghi đè khi hai người cùng sửa (<see cref="IVersioned"/>).</summary>
    public uint Version { get; private set; }

    /// <summary>Độ dài ghi chú, lý do tối đa.</summary>
    public const int TextMaxLength = 1000;

    private readonly List<MonthlySupplierApprovalLine> _lines = [];
    private readonly List<MonthlySupplierApprovalCompany> _companies = [];

    private MonthlySupplierApproval()
    {
    }

    /// <summary>Công ty lập (tổng công ty).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu, ví dụ GTH-TCT-2612-0001.</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Tháng áp dụng (ngày đầu tháng).</summary>
    public DateOnly Period { get; private set; }

    /// <summary>Nhóm hàng của phiếu (mặt hàng trên dòng thuộc nhóm này hoặc nhóm con).</summary>
    public Guid ItemCategoryId { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public MonthlyApprovalStatus Status { get; private set; } = MonthlyApprovalStatus.Draft;

    /// <inheritdoc />
    public int ApprovalRound { get; private set; }

    /// <summary>Lý do từ chối gần nhất.</summary>
    public string? RejectReason { get; private set; }

    /// <summary>Thời điểm duyệt đủ (UTC).</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>Phiếu đã duyệt mà phiếu này điều chỉnh (null = lập mới).</summary>
    public Guid? ReplacesId { get; private set; }

    /// <summary>Ngày hủy.</summary>
    public DateOnly? CancelledOn { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>Các dòng mặt hàng.</summary>
    public IReadOnlyList<MonthlySupplierApprovalLine> Lines => _lines;

    /// <summary>Các công ty áp dụng.</summary>
    public IReadOnlyList<MonthlySupplierApprovalCompany> Companies => _companies;

    /// <inheritdoc />
    public string DocType => DocumentTypes.MonthlySupplierApproval;

    /// <inheritdoc />
    /// <remarks>Không xét giá trị: mọi phiếu qua đủ các cấp đã cấu hình (như bảng giá – design.md mục 18.6).</remarks>
    public decimal ApprovalAmount => 0;

    /// <inheritdoc />
    public bool IsAwaitingApproval => Status == MonthlyApprovalStatus.Submitted;

    /// <inheritdoc />
    /// <remarks>Mọi NCC chính và dự phòng trên phiếu: người có kê khai xung đột lợi ích với NCC nào không được duyệt (mục 18.8).</remarks>
    public IReadOnlyCollection<Guid> RelatedSupplierIds =>
        _lines.Select(l => l.MainSupplierId).Concat(_lines.Select(l => l.BackupSupplierId).OfType<Guid>()).Distinct().ToList();

    /// <summary>Còn tác dụng hoặc đang trên đường duyệt – dùng để chặn trùng mặt hàng × tháng × công ty.</summary>
    public bool IsLive => Status is MonthlyApprovalStatus.Draft or MonthlyApprovalStatus.Submitted or MonthlyApprovalStatus.Approved;

    /// <summary>Lập phiếu nháp.</summary>
    /// <param name="companyId">Công ty lập (tổng công ty – tầng trên kiểm tra).</param>
    /// <param name="no">Số phiếu.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <param name="replacesId">Phiếu đã duyệt được điều chỉnh (nếu có).</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu phiếu sai.</exception>
    public static MonthlySupplierApproval Create(Guid companyId, string no, MonthlyApprovalHeader header, Guid? replacesId = null)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Thiếu công ty lập phiếu.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Phiếu phải có số.");
        var approval = new MonthlySupplierApproval { CompanyId = companyId, No = no, ReplacesId = replacesId };
        approval.Update(header);
        return approval;
    }

    /// <summary>Sửa đầu phiếu khi còn nháp.</summary>
    /// <param name="header">Đầu phiếu mới.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dữ liệu sai.</exception>
    public void Update(MonthlyApprovalHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureStatus(MonthlyApprovalStatus.Draft, "nháp");
        if (header.Period.Day != 1) throw new BusinessRuleException("Tháng áp dụng phải là ngày đầu tháng.");
        if (header.ItemCategoryId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhóm hàng.");
        var companies = header.CompanyIds.Where(c => c != Guid.Empty).Distinct().ToList();
        if (companies.Count == 0) throw new BusinessRuleException("Hãy chọn ít nhất một công ty áp dụng.");
        var note = Clean(header.Note);
        if (note is { Length: > TextMaxLength }) throw new BusinessRuleException($"Ghi chú tối đa {TextMaxLength} ký tự.");
        Period = header.Period;
        ItemCategoryId = header.ItemCategoryId;
        Note = note;
        _companies.RemoveAll(c => !companies.Contains(c.CompanyId));
        foreach (var id in companies.Where(id => _companies.All(c => c.CompanyId != id)))
            _companies.Add(new MonthlySupplierApprovalCompany(Id, id));
    }

    /// <summary>
    /// Thay toàn bộ dòng. Mỗi mặt hàng một dòng (quyết định 06/11/2026: phiếu theo nhóm hàng, mỗi dòng một mặt hàng).
    /// </summary>
    /// <param name="lines">Dòng kèm mặt hàng đã nạp (có ĐVT quy đổi).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có dòng, trùng mặt hàng hoặc dòng sai.</exception>
    public void SetLines(IReadOnlyList<(MonthlyApprovalLineData Data, Item Item)> lines)
    {
        EnsureStatus(MonthlyApprovalStatus.Draft, "nháp");
        if (lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một mặt hàng.");
        var duplicate = lines.GroupBy(l => l.Data.ItemId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new BusinessRuleException($"Mặt hàng {duplicate.First().Item.Code} có hai dòng – mỗi mặt hàng một dòng.");
        var keep = new List<MonthlySupplierApprovalLine>();
        var lineNo = 0;
        foreach (var (data, item) in lines)
        {
            lineNo++;
            MonthlySupplierApprovalLine line;
            if (data.Id is { } id)
            {
                line = _lines.FirstOrDefault(l => l.Id == id) ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại phiếu.");
            }
            else
            {
                line = new MonthlySupplierApprovalLine(Id);
                _lines.Add(line);
            }

            line.Set(lineNo, data, item);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
    }

    /// <summary>Gửi duyệt: Nháp → Chờ duyệt, xóa lý do từ chối cũ.</summary>
    /// <returns>Số lượt gửi mới.</returns>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc chưa có dòng.</exception>
    public int Submit()
    {
        EnsureStatus(MonthlyApprovalStatus.Draft, "nháp");
        if (_lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một mặt hàng mới gửi duyệt được.");
        Status = MonthlyApprovalStatus.Submitted;
        RejectReason = null;
        return ++ApprovalRound;
    }

    /// <inheritdoc />
    public void MarkApproved(DateTimeOffset at)
    {
        EnsureStatus(MonthlyApprovalStatus.Submitted, "chờ duyệt");
        Status = MonthlyApprovalStatus.Approved;
        ApprovedAt = at;
    }

    /// <summary>Bị từ chối hoặc người lập rút lại: về Nháp để sửa, giữ lý do.</summary>
    /// <param name="reason">Lý do (rút lại thì null).</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không chờ duyệt.</exception>
    public void ReturnToDraft(string? reason)
    {
        EnsureStatus(MonthlyApprovalStatus.Submitted, "chờ duyệt");
        Status = MonthlyApprovalStatus.Draft;
        RejectReason = Clean(reason);
    }

    /// <summary>Phiếu điều chỉnh của phiếu này đã duyệt: Đã duyệt → Đã thay.</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu không ở trạng thái đã duyệt.</exception>
    public void MarkReplaced()
    {
        EnsureStatus(MonthlyApprovalStatus.Approved, "đã duyệt");
        Status = MonthlyApprovalStatus.Replaced;
    }

    /// <summary>
    /// Hủy phiếu nháp hoặc đã duyệt (ví dụ nhóm hàng không mua trong tháng, NCC bị đình chỉ mà chưa có NCC thay) – bắt buộc lý do.
    /// Phiếu đang chờ duyệt phải rút về nháp trước để các cấp duyệt không còn việc treo.
    /// </summary>
    /// <param name="today">Ngày hôm nay (giờ Việt Nam).</param>
    /// <param name="reason">Lý do.</param>
    /// <exception cref="BusinessRuleException">Khi trạng thái không hủy được hoặc thiếu lý do.</exception>
    public void Cancel(DateOnly today, string? reason)
    {
        if (Status is not (MonthlyApprovalStatus.Draft or MonthlyApprovalStatus.Approved))
            throw new BusinessRuleException($"Phiếu {No} chỉ hủy được khi đang nháp hoặc đã duyệt.");
        var clean = Clean(reason) ?? throw new BusinessRuleException("Hãy nhập lý do hủy.");
        if (clean.Length > TextMaxLength) throw new BusinessRuleException($"Lý do tối đa {TextMaxLength} ký tự.");
        Status = MonthlyApprovalStatus.Cancelled;
        CancelledOn = today;
        CancelReason = clean;
    }

    /// <summary>Đầu phiếu hiện tại (để chép sang phiếu điều chỉnh / tháng sau).</summary>
    /// <param name="period">Tháng của phiếu mới.</param>
    public MonthlyApprovalHeader HeaderFor(DateOnly period) => new(period, ItemCategoryId, _companies.Select(c => c.CompanyId).ToList(), Note);

    /// <summary>Dòng hiện tại dưới dạng dữ liệu dòng mới (Id null) – để chép sang phiếu khác.</summary>
    public IReadOnlyList<MonthlyApprovalLineData> CopyLines() =>
        _lines.OrderBy(l => l.LineNo)
            .Select(l => new MonthlyApprovalLineData(null, l.ItemId, l.UomId, l.MainSupplierId, l.UnitPrice, l.BackupSupplierId, l.BackupUnitPrice,
                l.QuantityLimit, l.Note, l.StandardPrice, l.CeilingPrice, l.TargetPrice))
            .ToList();

    private void EnsureStatus(MonthlyApprovalStatus expected, string name)
    {
        if (Status != expected) throw new BusinessRuleException($"Phiếu {No} không ở trạng thái {name}.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Một công ty áp dụng của phiếu duyệt tháng – không phải <see cref="ICompanyOwned"/> vì công ty thành viên phải đọc được phiếu của tổng công ty.</summary>
[Auditable("Công ty áp dụng phiếu duyệt tháng")]
public sealed class MonthlySupplierApprovalCompany : Entity
{
    private MonthlySupplierApprovalCompany()
    {
    }

    internal MonthlySupplierApprovalCompany(Guid approvalId, Guid companyId)
    {
        ApprovalId = approvalId;
        CompanyId = companyId;
    }

    /// <summary>Phiếu.</summary>
    public Guid ApprovalId { get; private set; }

    /// <summary>Công ty áp dụng.</summary>
    public Guid CompanyId { get; private set; }
}

/// <summary>Dòng phiếu duyệt tháng: một mặt hàng với NCC chính, giá, NCC dự phòng và hạn mức.</summary>
[Auditable("Dòng phiếu duyệt NCC và giá tháng")]
public sealed class MonthlySupplierApprovalLine : Entity
{
    /// <summary>Giới hạn đơn giá (numeric(19,6) – CLAUDE.md mục 4).</summary>
    private const decimal MaxPrice = 10_000_000_000_000m;

    /// <summary>Giới hạn số lượng (numeric(18,4)).</summary>
    private const decimal MaxQty = 100_000_000_000_000m;

    private MonthlySupplierApprovalLine()
    {
    }

    internal MonthlySupplierApprovalLine(Guid approvalId) => ApprovalId = approvalId;

    /// <summary>Phiếu.</summary>
    public Guid ApprovalId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT của giá và hạn mức.</summary>
    public Guid UomId { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6).</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>NCC chính.</summary>
    public Guid MainSupplierId { get; private set; }

    /// <summary>Giá duyệt NCC chính (VND, trước VAT, đã trừ chiết khấu) – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>NCC dự phòng.</summary>
    public Guid? BackupSupplierId { get; private set; }

    /// <summary>Giá duyệt NCC dự phòng – numeric(19,6).</summary>
    public decimal? BackupUnitPrice { get; private set; }

    /// <summary>Hạn mức SL trong tháng theo ĐVT dòng (gộp các công ty áp dụng); null = không giới hạn – numeric(18,4).</summary>
    public decimal? QuantityLimit { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Giá chuẩn theo ĐVT dòng (VND, trước VAT, đã trừ chiết khấu) – mốc tính tiết kiệm (lát G4b, G4c); null = chưa đặt – numeric(19,6).</summary>
    public decimal? StandardPrice { get; private set; }

    /// <summary>Giá trần theo ĐVT dòng – PO giá ròng vượt thì cảnh báo, không chặn (người dùng chốt 06/11/2026); null = không có trần.</summary>
    public decimal? CeilingPrice { get; private set; }

    /// <summary>Giá mục tiêu theo ĐVT dòng – Phòng cung ứng đàm phán hướng tới; null = chưa đặt.</summary>
    public decimal? TargetPrice { get; private set; }

    internal void Set(int lineNo, MonthlyApprovalLineData data, Item item)
    {
        var prefix = $"Dòng {lineNo} ({item.Code})";
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        if (!item.IsActive && item.Id != ItemId) throw new BusinessRuleException($"{prefix}: mặt hàng đã ngừng dùng.");
        if (data.MainSupplierId == Guid.Empty) throw new BusinessRuleException($"{prefix}: hãy chọn NCC chính.");
        CheckPrice(prefix, "giá NCC chính", data.UnitPrice);
        if (data.BackupSupplierId is { } backup)
        {
            if (backup == data.MainSupplierId) throw new BusinessRuleException($"{prefix}: NCC dự phòng phải khác NCC chính.");
            // Có NCC dự phòng mà không có giá thì khi phải mua của NCC dự phòng lại thành tự thỏa thuận giá – quyết định cấm.
            if (data.BackupUnitPrice is not { } backupPrice) throw new BusinessRuleException($"{prefix}: hãy nhập giá NCC dự phòng.");
            CheckPrice(prefix, "giá NCC dự phòng", backupPrice);
        }
        else if (data.BackupUnitPrice is not null)
        {
            throw new BusinessRuleException($"{prefix}: có giá dự phòng thì phải chọn NCC dự phòng.");
        }

        if (data.StandardPrice is { } standard) CheckPrice(prefix, "giá chuẩn", standard);
        if (data.TargetPrice is { } target) CheckPrice(prefix, "giá mục tiêu", target);
        if (data.CeilingPrice is { } ceiling)
        {
            CheckPrice(prefix, "giá trần", ceiling);
            // Giá trần là mức cao nhất Ban chấp nhận – chính phiếu duyệt không được duyệt giá vượt trần (design.md mục 18.25, G4b).
            if (data.UnitPrice > ceiling) throw new BusinessRuleException($"{prefix}: giá NCC chính vượt giá trần.");
            if (data.BackupSupplierId is not null && data.BackupUnitPrice > ceiling)
                throw new BusinessRuleException($"{prefix}: giá NCC dự phòng vượt giá trần.");
            if (data.StandardPrice > ceiling) throw new BusinessRuleException($"{prefix}: giá chuẩn vượt giá trần.");
            if (data.TargetPrice > ceiling) throw new BusinessRuleException($"{prefix}: giá mục tiêu vượt giá trần.");
        }

        if (data.QuantityLimit is { } limit && (limit <= 0 || decimal.Round(limit, 4) != limit || limit >= MaxQty))
            throw new BusinessRuleException($"{prefix}: hạn mức phải > 0, tối đa 4 chữ số thập phân (để trống = không giới hạn).");
        var note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
        if (note is { Length: > 500 }) throw new BusinessRuleException($"{prefix}: ghi chú tối đa 500 ký tự.");
        LineNo = lineNo;
        ItemId = item.Id;
        UomId = data.UomId;
        UomFactor = data.UomId == item.BaseUomId ? 1m
            : item.Conversions.FirstOrDefault(c => c.UomId == data.UomId)?.Factor
              ?? throw new BusinessRuleException($"{prefix}: ĐVT phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.");
        MainSupplierId = data.MainSupplierId;
        UnitPrice = data.UnitPrice;
        BackupSupplierId = data.BackupSupplierId;
        BackupUnitPrice = data.BackupSupplierId is null ? null : data.BackupUnitPrice;
        QuantityLimit = data.QuantityLimit;
        Note = note;
        StandardPrice = data.StandardPrice;
        CeilingPrice = data.CeilingPrice;
        TargetPrice = data.TargetPrice;
    }

    private static void CheckPrice(string prefix, string what, decimal price)
    {
        if (price < 0 || decimal.Round(price, 6) != price || price >= MaxPrice)
            throw new BusinessRuleException($"{prefix}: {what} phải ≥ 0, tối đa 6 chữ số thập phân.");
    }
}
