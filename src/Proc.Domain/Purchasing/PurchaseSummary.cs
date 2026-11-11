using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Purchasing;

/// <summary>Biểu mẫu bảng tổng hợp đề xuất mua (design.md mục 19.2, 19.3 – lát Y5b).</summary>
public enum PurchaseSummaryForm
{
    /// <summary>2A – công trình: dòng của đề nghị loại A, cùng một công trình; đầu phiếu theo dự toán – lũy kế – còn lại của công trình.</summary>
    Construction = 1,

    /// <summary>2B – hàng hóa, dịch vụ: dòng của đề nghị loại B hoặc DV; đầu phiếu theo ngân sách (định mức/dự trù) bộ phận × tháng.</summary>
    Goods = 2,
}

/// <summary>Đầu phiếu bảng tổng hợp đề xuất mua.</summary>
/// <param name="Date">Ngày lập – tháng của ngày này là tháng ngân sách của biểu mẫu 2B.</param>
/// <param name="Form">Biểu mẫu 2A / 2B.</param>
/// <param name="DepartmentId">Bộ phận đề xuất – mọi dòng phải thuộc đề nghị của bộ phận này; GĐ khối duyệt theo khối của bộ phận.</param>
/// <param name="ConstructionProjectId">Công trình – bắt buộc với 2A, phải trống với 2B.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PurchaseSummaryHeader(DateOnly Date, PurchaseSummaryForm Form, Guid DepartmentId, Guid? ConstructionProjectId, string? Note);

/// <summary>Dữ liệu một dòng do người lập chọn.</summary>
/// <param name="RequestLineId">Dòng đề nghị mua đã duyệt.</param>
/// <param name="PurchasingUnitId">Đơn vị mua được giao mua dòng này (danh mục đơn vị mua – lát Y5a).</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record PurchaseSummaryLineData(Guid RequestLineId, Guid PurchasingUnitId, string? Note);

/// <summary>Thông tin dòng đề nghị mua mà dòng bảng tổng hợp lấy về (tầng trên nạp, tính SL còn cần mua).</summary>
/// <param name="RequestLineId">Dòng đề nghị.</param>
/// <param name="RequestId">Đề nghị mua.</param>
/// <param name="RequestNo">Số đề nghị (để báo lỗi).</param>
/// <param name="Category">Loại mua của đề nghị.</param>
/// <param name="DepartmentId">Bộ phận đề nghị.</param>
/// <param name="ConstructionProjectId">Công trình của đề nghị (loại A).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng (để báo lỗi).</param>
/// <param name="UomId">ĐVT của dòng đề nghị.</param>
/// <param name="UomFactor">Hệ số ĐVT cơ sở.</param>
/// <param name="OpenQty">SL còn cần mua theo ĐVT dòng = SL cần mua − SL đã lên PO chưa hủy.</param>
/// <param name="EstPrice">Đơn giá tham khảo của dòng đề nghị.</param>
/// <param name="NeededDate">Ngày cần (của dòng hoặc của đề nghị).</param>
public sealed record PurchaseSummaryLineSource(
    Guid RequestLineId, Guid RequestId, string RequestNo, PurchaseCategory Category, Guid? DepartmentId, Guid? ConstructionProjectId,
    Guid ItemId, string ItemCode, Guid UomId, decimal UomFactor, decimal OpenQty, decimal EstPrice, DateOnly? NeededDate);

/// <summary>
/// Bảng tổng hợp đề xuất mua 2A/2B – số loại <see cref="DocumentTypes.PurchaseSummary"/> (design.md mục 19.3 bước 2, lát Y5b). Phòng CUVT chọn
/// các dòng đề nghị mua <b>đã duyệt còn cần mua</b> của <b>một bộ phận</b> (2A: thêm điều kiện cùng một công trình), gán <b>đơn vị mua</b> từng
/// dòng rồi trình duyệt theo bộ cấp BTH (mẫu CUVT: TP CUVT → GĐ khối → Phó CT UBCL&amp;PT → Chủ tịch). SL, đơn giá chụp từ dòng đề nghị lúc lưu
/// nháp (SL còn cần mua lúc đó) – sau khi gửi duyệt không đổi theo đề nghị nữa. Mỗi dòng đề nghị chỉ thuộc một bảng tổng hợp chưa hủy (tầng trên
/// kiểm tra dưới khóa dòng đề nghị).
/// </summary>
[Auditable("Bảng tổng hợp đề xuất mua", LabelProperty = nameof(No))]
public sealed class PurchaseSummary : ApprovableDocument
{
    /// <summary>Độ dài ghi chú phiếu tối đa.</summary>
    public const int NoteMaxLength = 1000;

    /// <summary>Độ dài ghi chú dòng tối đa.</summary>
    public const int LineNoteMaxLength = 500;

    private readonly List<PurchaseSummaryLine> _lines = [];

    private PurchaseSummary()
    {
    }

    /// <summary>Biểu mẫu 2A / 2B.</summary>
    public PurchaseSummaryForm Form { get; private set; }

    /// <summary>Bộ phận đề xuất.</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>Công trình (chỉ 2A).</summary>
    public Guid? ConstructionProjectId { get; private set; }

    /// <summary>Tổng đề xuất = Σ thành tiền dòng (SL × đơn giá tham khảo) – numeric(19,2).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Các dòng.</summary>
    public IReadOnlyList<PurchaseSummaryLine> Lines => _lines;

    /// <inheritdoc />
    public override decimal ApprovalAmount => TotalAmount;

    /// <inheritdoc />
    public override string DocType => DocumentTypes.PurchaseSummary;

    /// <summary>
    /// Bộ phận đề xuất – dùng cho người duyệt động GĐ khối của phiếu (lát O1). Bảng tổng hợp không ghi ngân sách (các dòng đã tính ở đề nghị mua –
    /// tính lại sẽ đếm hai lần) nên không gọi <see cref="ApprovableDocument.ApplyBudgetCheck"/>.
    /// </summary>
    public override Guid? BudgetDepartmentId => DepartmentId;

    /// <inheritdoc />
    public override decimal BudgetedAmount => TotalAmount;

    /// <inheritdoc />
    protected override int LineCount => _lines.Count;

    /// <summary>Lập bảng tổng hợp nháp.</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="no">Số phiếu.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <returns>Phiếu nháp chưa có dòng.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu phiếu sai.</exception>
    public static PurchaseSummary Create(Guid companyId, string no, PurchaseSummaryHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Thiếu công ty lập phiếu.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Phiếu phải có số.");
        var summary = new PurchaseSummary { CompanyId = companyId, No = no };
        summary.Update(header);
        return summary;
    }

    /// <summary>Sửa đầu phiếu khi còn nháp. Đổi biểu mẫu/bộ phận/công trình thì dòng phải chọn lại cho khớp (kiểm tra ở <see cref="SetLines"/>).</summary>
    /// <param name="header">Đầu phiếu.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dữ liệu sai.</exception>
    public void Update(PurchaseSummaryHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (!Enum.IsDefined(header.Form)) throw new BusinessRuleException("Biểu mẫu phải là 2A (công trình) hoặc 2B (hàng hóa, dịch vụ).");
        if (header.DepartmentId == Guid.Empty) throw new BusinessRuleException("Hãy chọn bộ phận đề xuất.");
        // 2A theo dự toán của một công trình (design.md mục 19.2); 2B không gắn công trình.
        if (header.Form == PurchaseSummaryForm.Construction && (header.ConstructionProjectId is not { } projectId || projectId == Guid.Empty))
            throw new BusinessRuleException("Bảng tổng hợp 2A phải chọn công trình.");
        if (header.Form == PurchaseSummaryForm.Goods && header.ConstructionProjectId is not null)
            throw new BusinessRuleException("Bảng tổng hợp 2B không gắn công trình.");
        var note = Clean(header.Note);
        if (note?.Length > NoteMaxLength) throw new BusinessRuleException($"Ghi chú tối đa {NoteMaxLength} ký tự.");
        Date = header.Date;
        Form = header.Form;
        DepartmentId = header.DepartmentId;
        ConstructionProjectId = header.ConstructionProjectId;
        Note = note;
    }

    /// <summary>Đầu phiếu hiện tại (để kiểm tra lại lúc gửi duyệt).</summary>
    /// <returns>Bản sao đầu phiếu.</returns>
    public PurchaseSummaryHeader HeaderData() => new(Date, Form, DepartmentId, ConstructionProjectId, Note);

    /// <summary>
    /// Thay toàn bộ dòng (khi còn nháp). SL, đơn giá lấy lại từ dòng đề nghị (SL còn cần mua lúc lưu); dòng đã có giữ Id theo dòng đề nghị.
    /// </summary>
    /// <param name="lines">Dòng người lập chọn kèm thông tin dòng đề nghị.</param>
    /// <exception cref="BusinessRuleException">
    /// Khi không còn nháp, trùng dòng đề nghị, thiếu đơn vị mua, dòng không đúng biểu mẫu/bộ phận/công trình, hoặc đã đặt hết.
    /// </exception>
    public void SetLines(IReadOnlyList<(PurchaseSummaryLineData Data, PurchaseSummaryLineSource Source)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        EnsureDraft();
        if (lines.GroupBy(l => l.Data.RequestLineId).Any(g => g.Count() > 1))
            throw new BusinessRuleException("Mỗi dòng đề nghị chỉ chọn một lần trên bảng tổng hợp.");
        var keep = new List<PurchaseSummaryLine>();
        var lineNo = 0;
        foreach (var (data, source) in lines)
        {
            if (data.RequestLineId != source.RequestLineId) throw new BusinessRuleException("Dòng đề nghị không khớp dữ liệu nạp.");
            var where = $"Dòng {source.ItemCode} của đề nghị {source.RequestNo}";
            CheckSource(source, where);
            if (data.PurchasingUnitId == Guid.Empty) throw new BusinessRuleException($"{where}: hãy chọn đơn vị mua.");
            var note = Clean(data.Note);
            if (note?.Length > LineNoteMaxLength) throw new BusinessRuleException($"{where}: ghi chú tối đa {LineNoteMaxLength} ký tự.");
            var line = _lines.FirstOrDefault(l => l.RequestLineId == data.RequestLineId);
            if (line is null)
            {
                line = new PurchaseSummaryLine(Id, CompanyId);
                _lines.Add(line);
            }

            line.Set(++lineNo, source, data.PurchasingUnitId, note);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
        // Tổng = Σ thành tiền dòng đã làm tròn 2 số (như đề nghị mua): 3 × 12.345,678 = 37.037,03.
        TotalAmount = _lines.Sum(l => l.Amount);
    }

    /// <summary>
    /// Dòng đề nghị phải đúng biểu mẫu, bộ phận, công trình của phiếu và còn cần mua (design.md mục 19.3, lát Y5b): 2A chỉ đề nghị loại A cùng
    /// công trình; 2B đề nghị loại B hoặc dịch vụ.
    /// </summary>
    private void CheckSource(PurchaseSummaryLineSource source, string where)
    {
        var formOk = Form == PurchaseSummaryForm.Construction
            ? source.Category == PurchaseCategory.Construction
            : source.Category is PurchaseCategory.Goods or PurchaseCategory.Service;
        if (!formOk)
            throw new BusinessRuleException(Form == PurchaseSummaryForm.Construction
                ? $"{where}: bảng tổng hợp 2A chỉ lấy đề nghị loại A (công trình)."
                : $"{where}: bảng tổng hợp 2B chỉ lấy đề nghị loại B (hàng hóa) hoặc dịch vụ.");
        if (source.DepartmentId != DepartmentId) throw new BusinessRuleException($"{where}: đề nghị thuộc bộ phận khác với bảng tổng hợp.");
        if (Form == PurchaseSummaryForm.Construction && source.ConstructionProjectId != ConstructionProjectId)
            throw new BusinessRuleException($"{where}: đề nghị thuộc công trình khác với bảng tổng hợp.");
        if (source.OpenQty <= 0) throw new BusinessRuleException($"{where}: đã lên đơn mua hàng hết, không còn cần mua.");
        if (source.UomFactor <= 0) throw new BusinessRuleException($"{where}: hệ số ĐVT không hợp lệ.");
    }
}

/// <summary>Dòng bảng tổng hợp đề xuất mua – một dòng đề nghị mua đã duyệt kèm đơn vị mua được giao.</summary>
[Auditable("Dòng bảng tổng hợp đề xuất mua")]
public sealed class PurchaseSummaryLine : Entity
{
    private PurchaseSummaryLine()
    {
    }

    internal PurchaseSummaryLine(Guid summaryId, Guid companyId)
    {
        SummaryId = summaryId;
        CompanyId = companyId;
    }

    /// <summary>Bảng tổng hợp.</summary>
    public Guid SummaryId { get; private set; }

    /// <summary>Công ty của bảng tổng hợp – để khóa ngoại tới đơn vị mua kèm công ty (CSDL chặn gán đơn vị của công ty khác).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Đề nghị mua.</summary>
    public Guid RequestId { get; private set; }

    /// <summary>Dòng đề nghị mua.</summary>
    public Guid RequestLineId { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT (của dòng đề nghị).</summary>
    public Guid UomId { get; private set; }

    /// <summary>SL đề xuất mua = SL còn cần mua của dòng đề nghị lúc lưu – numeric(18,4).</summary>
    public decimal Qty { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6).</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>SL theo ĐVT cơ sở – numeric(18,4).</summary>
    public decimal BaseQty { get; private set; }

    /// <summary>Đơn giá tham khảo (của dòng đề nghị) – numeric(19,6).</summary>
    public decimal EstPrice { get; private set; }

    /// <summary>Thành tiền = SL × đơn giá, làm tròn 2 số – numeric(19,2).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Ngày cần.</summary>
    public DateOnly? NeededDate { get; private set; }

    /// <summary>Đơn vị mua được giao.</summary>
    public Guid PurchasingUnitId { get; private set; }

    /// <summary>Ghi chú dòng.</summary>
    public string? Note { get; private set; }

    /// <summary>Ghi dữ liệu dòng từ dòng đề nghị (SL còn cần mua) và đơn vị mua.</summary>
    internal void Set(int lineNo, PurchaseSummaryLineSource source, Guid purchasingUnitId, string? note)
    {
        LineNo = lineNo;
        RequestId = source.RequestId;
        RequestLineId = source.RequestLineId;
        ItemId = source.ItemId;
        UomId = source.UomId;
        Qty = source.OpenQty;
        UomFactor = source.UomFactor;
        // SL cơ sở = SL × hệ số, làm tròn 4 số như dòng đề nghị: 2,5 thùng × 24 = 60 cái.
        BaseQty = decimal.Round(source.OpenQty * source.UomFactor, 4, MidpointRounding.AwayFromZero);
        EstPrice = source.EstPrice;
        Amount = decimal.Round(source.OpenQty * source.EstPrice, 2, MidpointRounding.AwayFromZero);
        NeededDate = source.NeededDate;
        PurchasingUnitId = purchasingUnitId;
        Note = note;
    }
}
