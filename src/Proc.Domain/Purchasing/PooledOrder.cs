using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Purchasing;

/// <summary>Trạng thái phiếu đặt hàng gộp (design.md mục 18.25, lát G7).</summary>
public enum PooledOrderStatus
{
    /// <summary>Nháp – đang soạn phân bổ, sửa được.</summary>
    Draft = 1,

    /// <summary>Đã tạo PO – mỗi công ty có SL đã sinh một PO nháp; phiếu không sửa nữa.</summary>
    Ordered = 2,

    /// <summary>Đã hủy (kèm lý do) khi còn nháp.</summary>
    Cancelled = 3,
}

/// <summary>Đầu phiếu đặt hàng gộp.</summary>
/// <param name="OrderDate">Ngày đặt hàng – cũng là ngày của các PO sinh ra; phải thuộc tháng của phiếu duyệt tháng.</param>
/// <param name="MonthlyApprovalId">Phiếu duyệt NCC và giá tháng (GTH) đã duyệt.</param>
/// <param name="ItemCategoryId">Nhóm hàng của phiếu GTH (dùng cho phạm vi nhóm hàng).</param>
/// <param name="SupplierId">NCC đặt – NCC chính hoặc dự phòng trên phiếu GTH.</param>
/// <param name="ExpectedDate">Ngày giao dự kiến (không bắt buộc).</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PooledOrderHeader(
    DateOnly OrderDate, Guid MonthlyApprovalId, Guid ItemCategoryId, Guid SupplierId, DateOnly? ExpectedDate, string? Note);

/// <summary>Công ty nhận hàng của phiếu đặt gộp.</summary>
/// <param name="CompanyId">Công ty (thuộc các công ty áp dụng của phiếu GTH).</param>
/// <param name="WarehouseId">Kho nhận của công ty đó.</param>
/// <param name="DepartmentId">Bộ phận đặt hàng của công ty đó (không bắt buộc – dùng cho ngân sách bộ phận).</param>
public sealed record PooledOrderCompanyData(Guid CompanyId, Guid WarehouseId, Guid? DepartmentId);

/// <summary>Thông tin dòng phiếu GTH mà dòng đặt gộp dựa vào (tầng trên nạp và tính giá cho NCC đã chọn).</summary>
/// <param name="MonthlyApprovalLineId">Dòng phiếu GTH.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng (để báo lỗi).</param>
/// <param name="UomId">ĐVT của dòng GTH – cũng là ĐVT đặt.</param>
/// <param name="UomFactor">Hệ số ĐVT cơ sở.</param>
/// <param name="UnitPrice">Giá Ban đã duyệt cho NCC của phiếu (giá chính hoặc giá dự phòng).</param>
public sealed record PooledOrderLineSource(Guid MonthlyApprovalLineId, Guid ItemId, string ItemCode, Guid UomId, decimal UomFactor, decimal UnitPrice);

/// <summary>Dữ liệu một dòng đặt gộp.</summary>
/// <param name="Id">Id dòng đang có (null = dòng mới).</param>
/// <param name="MonthlyApprovalLineId">Dòng phiếu GTH.</param>
/// <param name="Allocations">SL phân bổ cho từng công ty theo ĐVT dòng (công ty không có trong danh sách = 0).</param>
/// <param name="AllocationReason">Lý do phân bổ – bắt buộc khi mặt hàng không có kế hoạch nhu cầu tháng ở công ty nào.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record PooledOrderLineData(
    Guid? Id, Guid MonthlyApprovalLineId, IReadOnlyList<PooledAllocationData> Allocations, string? AllocationReason, string? Note);

/// <summary>SL phân bổ cho một công ty.</summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="Qty">Số lượng theo ĐVT dòng.</param>
public sealed record PooledAllocationData(Guid CompanyId, decimal Qty);

/// <summary>Kế hoạch nhu cầu tháng của một công ty cho một mặt hàng, quy về ĐVT dòng đặt gộp.</summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="Plan">SL kế hoạch (≥ 0).</param>
public sealed record CompanyPlan(Guid CompanyId, decimal Plan);

/// <summary>
/// Phiếu đặt hàng gộp – số loại <see cref="DocumentTypes.PooledOrder"/> (design.md mục 18.25, lát G7). Quyết định thành lập Ban cung ứng:
/// Phòng cung ứng chung đặt hàng cho các công ty đúng NCC và giá Ban đã duyệt trong tháng (phiếu GTH) và phân bổ về từng công ty trong biên
/// độ cho phép. Người dùng chốt 06/11/2026: mỗi pháp nhân vẫn một PO – phiếu này chỉ là công cụ đặt gộp: lập ở tổng công ty, chọn phiếu
/// GTH + NCC, nhập SL phân bổ cho từng công ty; "Tạo PO" sinh mỗi công ty một PO nháp (giá GTH) rồi phiếu thành Đã tạo PO.
/// </summary>
[Auditable("Phiếu đặt hàng gộp", LabelProperty = nameof(No))]
public sealed class PooledOrder : AuditableEntity, ICompanyOwned, IHasDocumentNumber, IVersioned
{
    /// <summary>Độ dài ghi chú, lý do tối đa.</summary>
    public const int TextMaxLength = 1000;

    private readonly List<PooledOrderLine> _lines = [];
    private readonly List<PooledOrderCompany> _companies = [];

    private PooledOrder()
    {
    }

    /// <summary>Phiên bản dòng (xmin) – chống ghi đè khi hai người cùng sửa (<see cref="IVersioned"/>).</summary>
    public uint Version { get; private set; }

    /// <summary>Công ty lập (tổng công ty).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu, ví dụ DHG-TCT-2611-0001.</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Ngày đặt hàng.</summary>
    public DateOnly OrderDate { get; private set; }

    /// <summary>Phiếu duyệt NCC và giá tháng.</summary>
    public Guid MonthlyApprovalId { get; private set; }

    /// <summary>Nhóm hàng của phiếu GTH.</summary>
    public Guid ItemCategoryId { get; private set; }

    /// <summary>NCC đặt.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Ngày giao dự kiến.</summary>
    public DateOnly? ExpectedDate { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public PooledOrderStatus Status { get; private set; } = PooledOrderStatus.Draft;

    /// <summary>% biên độ phân bổ áp dụng lúc tạo PO (chụp từ tham số, để tra cứu về sau) – numeric(9,4).</summary>
    public decimal? MarginPercent { get; private set; }

    /// <summary>Thời điểm tạo PO (UTC).</summary>
    public DateTimeOffset? OrderedAt { get; private set; }

    /// <summary>Người tạo PO.</summary>
    public Guid? OrderedBy { get; private set; }

    /// <summary>Ngày hủy.</summary>
    public DateOnly? CancelledOn { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>Các dòng mặt hàng.</summary>
    public IReadOnlyList<PooledOrderLine> Lines => _lines;

    /// <summary>Các công ty nhận hàng.</summary>
    public IReadOnlyList<PooledOrderCompany> Companies => _companies;

    /// <summary>Lập phiếu nháp.</summary>
    /// <param name="companyId">Công ty lập (tổng công ty – tầng trên kiểm tra).</param>
    /// <param name="no">Số phiếu.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu phiếu sai.</exception>
    public static PooledOrder Create(Guid companyId, string no, PooledOrderHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Thiếu công ty lập phiếu.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Phiếu phải có số.");
        var order = new PooledOrder { CompanyId = companyId, No = no };
        order.Update(header);
        return order;
    }

    /// <summary>Sửa đầu phiếu khi còn nháp.</summary>
    /// <param name="header">Đầu phiếu mới.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dữ liệu sai.</exception>
    public void Update(PooledOrderHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (header.MonthlyApprovalId == Guid.Empty) throw new BusinessRuleException("Hãy chọn phiếu duyệt NCC và giá tháng.");
        if (header.ItemCategoryId == Guid.Empty) throw new BusinessRuleException("Phiếu duyệt tháng thiếu nhóm hàng.");
        if (header.SupplierId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhà cung cấp.");
        if (header.ExpectedDate < header.OrderDate) throw new BusinessRuleException("Ngày giao dự kiến không được trước ngày đặt hàng.");
        var note = Clean(header.Note);
        if (note is { Length: > TextMaxLength }) throw new BusinessRuleException($"Ghi chú tối đa {TextMaxLength} ký tự.");
        // Đổi phiếu GTH hoặc NCC thì giá và dòng GTH cũ không còn đúng – tầng trên gửi lại dòng theo phiếu/NCC mới.
        OrderDate = header.OrderDate;
        MonthlyApprovalId = header.MonthlyApprovalId;
        ItemCategoryId = header.ItemCategoryId;
        SupplierId = header.SupplierId;
        ExpectedDate = header.ExpectedDate;
        Note = note;
    }

    /// <summary>Thay danh sách công ty nhận hàng (giữ dòng đã có theo công ty).</summary>
    /// <param name="companies">Công ty, kho, bộ phận – ít nhất một, không trùng công ty.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, rỗng, trùng hoặc thiếu kho.</exception>
    public void SetCompanies(IReadOnlyList<PooledOrderCompanyData> companies)
    {
        ArgumentNullException.ThrowIfNull(companies);
        EnsureDraft();
        if (companies.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một công ty nhận hàng.");
        if (companies.Any(c => c.CompanyId == Guid.Empty)) throw new BusinessRuleException("Có dòng công ty chưa chọn công ty.");
        if (companies.GroupBy(c => c.CompanyId).Any(g => g.Count() > 1)) throw new BusinessRuleException("Mỗi công ty chỉ một dòng.");
        if (companies.Any(c => c.WarehouseId == Guid.Empty)) throw new BusinessRuleException("Hãy chọn kho nhận cho mọi công ty.");
        var keep = new List<PooledOrderCompany>();
        foreach (var data in companies)
        {
            var row = _companies.FirstOrDefault(c => c.CompanyId == data.CompanyId);
            if (row is null)
            {
                row = new PooledOrderCompany(Id, data.CompanyId);
                _companies.Add(row);
            }

            row.Set(data.WarehouseId, data.DepartmentId);
            keep.Add(row);
        }

        _companies.RemoveAll(c => !keep.Contains(c));
    }

    /// <summary>
    /// Thay toàn bộ dòng. Mỗi dòng phiếu GTH chỉ một dòng; SL phân bổ ≥ 0, tối đa 4 số lẻ, chỉ cho các công ty nhận hàng của phiếu; tổng
    /// SL dòng &gt; 0. Gọi sau <see cref="SetCompanies"/>.
    /// </summary>
    /// <param name="lines">Dòng kèm thông tin dòng GTH đã nạp.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có dòng, trùng dòng GTH hoặc SL sai.</exception>
    public void SetLines(IReadOnlyList<(PooledOrderLineData Data, PooledOrderLineSource Source)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        EnsureDraft();
        if (lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một dòng hàng.");
        var duplicate = lines.GroupBy(l => l.Source.MonthlyApprovalLineId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new BusinessRuleException($"Mặt hàng {duplicate.First().Source.ItemCode} có hơn một dòng – mỗi mặt hàng một dòng, phân bổ trên cùng dòng.");
        var companyIds = _companies.Select(c => c.CompanyId).ToHashSet();
        var keep = new List<PooledOrderLine>();
        var lineNo = 0;
        foreach (var (data, source) in lines)
        {
            lineNo++;
            PooledOrderLine line;
            if (data.Id is { } id)
            {
                line = _lines.FirstOrDefault(l => l.Id == id) ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại phiếu.");
            }
            else
            {
                line = new PooledOrderLine(Id);
                _lines.Add(line);
            }

            line.Set(lineNo, data, source, companyIds);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
    }

    /// <summary>
    /// Kiểm tra phân bổ trong biên độ (design.md mục 18.25, G7): dòng có kế hoạch nhu cầu thì SL mỗi công ty lệch so với đề xuất theo tỷ lệ
    /// kế hoạch không quá dung sai <see cref="PooledAllocation.Tolerance"/>; dòng không có kế hoạch ở công ty nào thì phải có lý do phân bổ.
    /// </summary>
    /// <param name="plans">Kế hoạch tháng của các công ty cho dòng (theo ĐVT dòng).</param>
    /// <param name="marginPercent">% biên độ.</param>
    /// <param name="companyCode">Mã công ty để báo lỗi.</param>
    /// <exception cref="BusinessRuleException">Khi có dòng vượt biên độ hoặc thiếu lý do (báo dòng, công ty, SL, đề xuất, dung sai).</exception>
    public void EnsureWithinMargin(Func<PooledOrderLine, IReadOnlyList<CompanyPlan>> plans, decimal marginPercent, Func<Guid, string> companyCode)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(companyCode);
        var errors = new List<string>();
        foreach (var line in _lines.OrderBy(l => l.LineNo))
        {
            var linePlans = plans(line);
            var prefix = $"Dòng {line.LineNo}";
            if (linePlans.Sum(p => Math.Max(p.Plan, 0)) <= 0)
            {
                // Không có kế hoạch thì không có tỷ lệ để so – phân bổ do Phòng cung ứng quyết, nhưng phải để lại lý do để hậu kiểm.
                if (line.AllocationReason is null) errors.Add($"{prefix}: mặt hàng chưa có kế hoạch nhu cầu tháng ở công ty nào – hãy nhập lý do phân bổ.");
                continue;
            }

            var total = line.TotalQty;
            var tolerance = PooledAllocation.Tolerance(total, marginPercent);
            foreach (var company in _companies)
            {
                var qty = line.QtyFor(company.CompanyId);
                var expected = PooledAllocation.Expected(total, company.CompanyId, linePlans);
                if (Math.Abs(qty - expected) > tolerance)
                    errors.Add($"{prefix}, {companyCode(company.CompanyId)}: phân bổ {qty:0.####}, đề xuất theo kế hoạch {expected:0.####} ± {tolerance:0.####}");
            }
        }

        if (errors.Count > 0)
            throw new BusinessRuleException(
                $"Phân bổ vượt biên độ {marginPercent:0.##}% so với kế hoạch nhu cầu (sửa SL hoặc đề xuất Ban điều chỉnh): {string.Join("; ", errors.Take(5))}" +
                (errors.Count > 5 ? $"; … và {errors.Count - 5} chỗ khác." : "."));
    }

    /// <summary>Ghi nhận đã tạo PO cho các công ty – từ đây phiếu không sửa, không hủy (PO sai thì hủy PO ở từng công ty).</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="userId">Người tạo.</param>
    /// <param name="marginPercent">% biên độ đã áp.</param>
    /// <param name="orders">PO đã sinh theo công ty.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc không có PO nào.</exception>
    public void MarkOrdered(DateTimeOffset at, Guid userId, decimal marginPercent, IReadOnlyDictionary<Guid, Guid> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        EnsureDraft();
        if (orders.Count == 0) throw new BusinessRuleException("Không có công ty nào có số lượng để tạo PO.");
        foreach (var company in _companies)
            if (orders.TryGetValue(company.CompanyId, out var poId)) company.LinkOrder(poId);
        Status = PooledOrderStatus.Ordered;
        OrderedAt = at;
        OrderedBy = userId;
        MarginPercent = marginPercent;
    }

    /// <summary>Hủy phiếu nháp – bắt buộc lý do.</summary>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <param name="reason">Lý do.</param>
    /// <param name="inChain">
    /// Hủy theo dây chuyền (người dùng chốt 11/11/2026, design.md mục 20.2 E1): quản trị hủy phiếu đã tạo PO thì các PO ở mọi công ty đã hủy
    /// trước trong cùng giao dịch. Ngoài dây chuyền, phiếu đã tạo PO không hủy được.
    /// </param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp (ngoài dây chuyền), đã hủy hoặc thiếu lý do.</exception>
    public void Cancel(DateOnly today, string? reason, bool inChain = false)
    {
        if (Status == PooledOrderStatus.Ordered && !inChain)
            throw new BusinessRuleException($"Phiếu {No} đã tạo PO – hủy theo dây chuyền (hủy các PO đã tạo trước) bằng lệnh Hủy phiếu của quản trị.");
        if (Status == PooledOrderStatus.Cancelled) throw new BusinessRuleException($"Phiếu {No} đã hủy.");
        if (!inChain) EnsureDraft();
        var clean = Clean(reason) ?? throw new BusinessRuleException("Hãy nhập lý do hủy.");
        if (clean.Length > TextMaxLength) throw new BusinessRuleException($"Lý do tối đa {TextMaxLength} ký tự.");
        Status = PooledOrderStatus.Cancelled;
        CancelledOn = today;
        CancelReason = clean;
    }

    /// <summary>Chặn sửa khi phiếu không còn ở trạng thái Nháp.</summary>
    private void EnsureDraft()
    {
        if (Status != PooledOrderStatus.Draft) throw new BusinessRuleException($"Phiếu {No} không ở trạng thái nháp.");
    }

    /// <summary>Bỏ khoảng trắng hai đầu; chuỗi rỗng hoặc toàn khoảng trắng thành null.</summary>
    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Công ty nhận hàng của phiếu đặt gộp, kèm PO đã sinh.</summary>
[Auditable("Công ty nhận hàng đặt gộp")]
public sealed class PooledOrderCompany : Entity
{
    private PooledOrderCompany()
    {
    }

    internal PooledOrderCompany(Guid pooledOrderId, Guid companyId)
    {
        PooledOrderId = pooledOrderId;
        CompanyId = companyId;
    }

    /// <summary>Phiếu.</summary>
    public Guid PooledOrderId { get; private set; }

    /// <summary>Công ty.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Kho nhận của công ty.</summary>
    public Guid WarehouseId { get; private set; }

    /// <summary>Bộ phận đặt hàng của công ty.</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>PO đã sinh cho công ty (null = chưa tạo hoặc công ty không có SL).</summary>
    public Guid? PurchaseOrderId { get; private set; }

    /// <summary>Gán kho nhận và bộ phận đặt hàng của công ty nhận.</summary>
    internal void Set(Guid warehouseId, Guid? departmentId)
    {
        WarehouseId = warehouseId;
        DepartmentId = departmentId;
    }

    /// <summary>Gắn PO đã sinh cho công ty nhận khi phát hành phiếu đặt gộp.</summary>
    internal void LinkOrder(Guid purchaseOrderId) => PurchaseOrderId = purchaseOrderId;
}

/// <summary>Dòng phiếu đặt gộp: một mặt hàng của phiếu GTH với SL phân bổ cho từng công ty.</summary>
[Auditable("Dòng phiếu đặt hàng gộp")]
public sealed class PooledOrderLine : Entity
{
    /// <summary>Giới hạn số lượng (numeric(18,4)).</summary>
    private const decimal MaxQty = 100_000_000_000_000m;

    private readonly List<PooledOrderAllocation> _allocations = [];

    private PooledOrderLine()
    {
    }

    internal PooledOrderLine(Guid pooledOrderId) => PooledOrderId = pooledOrderId;

    /// <summary>Phiếu.</summary>
    public Guid PooledOrderId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Dòng phiếu GTH.</summary>
    public Guid MonthlyApprovalLineId { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT đặt (ĐVT của dòng GTH).</summary>
    public Guid UomId { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6).</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>Giá Ban đã duyệt cho NCC của phiếu, theo ĐVT dòng (VND, trước VAT, đã trừ chiết khấu) – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Lý do phân bổ (bắt buộc khi không có kế hoạch nhu cầu).</summary>
    public string? AllocationReason { get; private set; }

    /// <summary>Ghi chú dòng.</summary>
    public string? Note { get; private set; }

    /// <summary>SL phân bổ &gt; 0 theo công ty.</summary>
    public IReadOnlyList<PooledOrderAllocation> Allocations => _allocations;

    /// <summary>Tổng SL đặt của dòng.</summary>
    public decimal TotalQty => _allocations.Sum(a => a.Qty);

    /// <summary>SL phân bổ cho một công ty (0 nếu không có).</summary>
    /// <param name="companyId">Công ty.</param>
    /// <returns>Số lượng theo ĐVT dòng.</returns>
    public decimal QtyFor(Guid companyId) => _allocations.FirstOrDefault(a => a.CompanyId == companyId)?.Qty ?? 0m;

    /// <summary>Kiểm tra và gán một dòng đặt gộp: khớp dòng phiếu GTH nguồn, chỉ phân bổ cho công ty áp dụng.</summary>
    internal void Set(int lineNo, PooledOrderLineData data, PooledOrderLineSource source, IReadOnlySet<Guid> companyIds)
    {
        var prefix = $"Dòng {lineNo} ({source.ItemCode})";
        if (data.MonthlyApprovalLineId != source.MonthlyApprovalLineId) throw new BusinessRuleException($"{prefix}: dòng phiếu duyệt tháng không khớp.");
        if (data.Allocations.GroupBy(a => a.CompanyId).Any(g => g.Count() > 1)) throw new BusinessRuleException($"{prefix}: trùng công ty phân bổ.");
        foreach (var a in data.Allocations)
        {
            if (!companyIds.Contains(a.CompanyId)) throw new BusinessRuleException($"{prefix}: phân bổ cho công ty không có trong danh sách công ty nhận hàng.");
            if (a.Qty < 0 || decimal.Round(a.Qty, 4) != a.Qty || a.Qty >= MaxQty)
                throw new BusinessRuleException($"{prefix}: SL phân bổ phải ≥ 0, tối đa 4 chữ số thập phân.");
        }

        if (data.Allocations.Sum(a => a.Qty) <= 0) throw new BusinessRuleException($"{prefix}: hãy nhập SL phân bổ cho ít nhất một công ty.");
        var reason = PooledOrder.Clean(data.AllocationReason);
        if (reason is { Length: > 500 }) throw new BusinessRuleException($"{prefix}: lý do phân bổ tối đa 500 ký tự.");
        var note = PooledOrder.Clean(data.Note);
        if (note is { Length: > 500 }) throw new BusinessRuleException($"{prefix}: ghi chú tối đa 500 ký tự.");
        LineNo = lineNo;
        MonthlyApprovalLineId = source.MonthlyApprovalLineId;
        ItemId = source.ItemId;
        UomId = source.UomId;
        UomFactor = source.UomFactor;
        UnitPrice = source.UnitPrice;
        AllocationReason = reason;
        Note = note;
        var wanted = data.Allocations.Where(a => a.Qty > 0).ToList();
        _allocations.RemoveAll(x => wanted.All(a => a.CompanyId != x.CompanyId));
        foreach (var a in wanted)
        {
            var row = _allocations.FirstOrDefault(x => x.CompanyId == a.CompanyId);
            if (row is null)
            {
                row = new PooledOrderAllocation(Id, a.CompanyId);
                _allocations.Add(row);
            }

            row.Qty = a.Qty;
        }
    }
}

/// <summary>SL phân bổ một dòng đặt gộp cho một công ty.</summary>
[Auditable("Phân bổ đặt hàng gộp")]
public sealed class PooledOrderAllocation : Entity
{
    private PooledOrderAllocation()
    {
    }

    internal PooledOrderAllocation(Guid lineId, Guid companyId)
    {
        LineId = lineId;
        CompanyId = companyId;
    }

    /// <summary>Dòng.</summary>
    public Guid LineId { get; private set; }

    /// <summary>Công ty.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số lượng theo ĐVT dòng – numeric(18,4).</summary>
    public decimal Qty { get; internal set; }
}

/// <summary>
/// Phân bổ SL đặt gộp theo kế hoạch nhu cầu (design.md mục 18.25, G7). Quy tắc phân bổ của Ban: chia theo tỷ lệ kế hoạch nhu cầu tháng đã
/// chốt của từng công ty; Phòng cung ứng chung được điều chỉnh trong biên độ cho phép.
/// </summary>
public static class PooledAllocation
{
    /// <summary>
    /// SL đề xuất chính xác (chưa làm tròn) của một công ty = tổng × kế hoạch công ty ÷ tổng kế hoạch. Ví dụ tổng 100, kế hoạch A 60, B 40
    /// → A 60, B 40; tổng 10, kế hoạch A 2, B 1 → A 6,6667, B 3,3333. Không có kế hoạch → 0.
    /// </summary>
    /// <param name="total">Tổng SL đặt.</param>
    /// <param name="companyId">Công ty.</param>
    /// <param name="plans">Kế hoạch các công ty.</param>
    /// <returns>SL đề xuất.</returns>
    public static decimal Expected(decimal total, Guid companyId, IReadOnlyList<CompanyPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        var sum = plans.Sum(p => Math.Max(p.Plan, 0));
        if (sum <= 0) return 0m;
        var plan = Math.Max(plans.Where(p => p.CompanyId == companyId).Sum(p => p.Plan), 0);
        return total * plan / sum;
    }

    /// <summary>
    /// Dung sai cho phép mỗi công ty = max(tổng × biên độ % ÷ 100, 1 ĐVT), làm tròn 4 số. Mức sàn 1 ĐVT để phân bổ số nguyên luôn làm
    /// tròn được. Ví dụ tổng 100, biên độ 10% → ±10; tổng 5, biên độ 10% → ±1 (0,5 &lt; 1).
    /// </summary>
    /// <param name="total">Tổng SL dòng.</param>
    /// <param name="marginPercent">% biên độ.</param>
    /// <returns>Dung sai theo ĐVT dòng.</returns>
    public static decimal Tolerance(decimal total, decimal marginPercent) =>
        Math.Max(Math.Round(total * marginPercent / 100m, 4, MidpointRounding.AwayFromZero), 1m);

    /// <summary>
    /// Chia tổng SL theo tỷ lệ kế hoạch, cộng lại đúng bằng tổng. Tổng là số nguyên thì chia số nguyên (phần dư từng đơn vị cho công ty
    /// có phần lẻ lớn nhất, bằng nhau thì kế hoạch lớn hơn); có số lẻ thì chia 4 số lẻ, phần dư dồn vào công ty kế hoạch lớn nhất.
    /// Ví dụ tổng 10, kế hoạch A 2, B 1 → 6,667 / 3,333 → phần nguyên 6 / 3, dư 1 cho A → A 7, B 3.
    /// </summary>
    /// <param name="total">Tổng SL đặt (≥ 0).</param>
    /// <param name="plans">Kế hoạch các công ty.</param>
    /// <returns>SL theo công ty; rỗng khi không có kế hoạch nào.</returns>
    public static IReadOnlyDictionary<Guid, decimal> Suggest(decimal total, IReadOnlyList<CompanyPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        var positive = plans.Where(p => p.Plan > 0).GroupBy(p => p.CompanyId).Select(g => new CompanyPlan(g.Key, g.Sum(p => p.Plan))).ToList();
        var sum = positive.Sum(p => p.Plan);
        if (sum <= 0 || total <= 0) return new Dictionary<Guid, decimal>();
        var integer = decimal.Truncate(total) == total;
        var unit = integer ? 1m : 0.0001m;
        var exact = positive.Select(p => (p.CompanyId, p.Plan, Value: total * p.Plan / sum)).ToList();
        var result = exact.ToDictionary(e => e.CompanyId, e => decimal.Floor(e.Value / unit) * unit);
        var remainder = total - result.Values.Sum();
        if (integer)
        {
            foreach (var e in exact.OrderByDescending(e => e.Value - result[e.CompanyId]).ThenByDescending(e => e.Plan))
            {
                if (remainder <= 0) break;
                result[e.CompanyId] += 1m;
                remainder -= 1m;
            }
        }
        else if (remainder != 0)
        {
            result[exact.MaxBy(e => e.Plan).CompanyId] += remainder;
        }

        return result;
    }
}
