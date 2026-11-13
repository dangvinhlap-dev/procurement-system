using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Purchasing;

/// <summary>Đầu bảng kê chi phí tạp vụ do người lập nhập.</summary>
/// <param name="Date">Ngày bảng kê – tháng của ngày này là tháng tính định mức (ngân sách) của bộ phận.</param>
/// <param name="DepartmentId">Bộ phận phát sinh chi phí – Trưởng phòng ban / GĐ khối duyệt theo bộ phận này.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record SundryExpenseSheetHeader(DateOnly Date, Guid DepartmentId, string? Note);

/// <summary>Một khoản chi tạp vụ đã phát sinh.</summary>
/// <param name="ExpenseDate">Ngày chi.</param>
/// <param name="Description">Nội dung chi (bắt buộc).</param>
/// <param name="Vendor">Nơi mua / người bán.</param>
/// <param name="ReceiptNo">Số hóa đơn / chứng từ chi.</param>
/// <param name="Amount">Số tiền đã chi (gồm thuế nếu có).</param>
public sealed record SundryExpenseLineData(DateOnly ExpenseDate, string? Description, string? Vendor, string? ReceiptNo, decimal Amount);

/// <summary>
/// Bảng kê chi phí tạp vụ 2C – số loại <see cref="DocumentTypes.SundryExpenseSheet"/> (design.md mục 19.2 loại C, 19.3 – lát Y11a). Bộ phận
/// liệt kê các chi phí lặt vặt <b>đã phát sinh</b> (không qua PR/RFQ/PO) để thanh toán gộp qua đề nghị thanh toán tổng hợp 1C. Khung chi phí là
/// <b>định mức tháng của phòng ban</b>: tổng bảng kê tính vào ngân sách bộ phận × tháng của ngày bảng kê (mục 18.24); vượt thì không chặn mà
/// thêm chặng duyệt TVB (Q5). Duyệt theo bộ cấp BKT (mẫu CUVT: Trưởng phòng ban → GĐ khối → Phòng TC-NQ-KT → Chủ tịch).
/// </summary>
[Auditable("Bảng kê chi phí tạp vụ", LabelProperty = nameof(No))]
public sealed class SundryExpenseSheet : ApprovableDocument
{
    /// <summary>Độ dài ghi chú phiếu tối đa.</summary>
    public const int NoteMaxLength = 1000;

    /// <summary>Độ dài nội dung chi tối đa.</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>Độ dài nơi mua / người bán tối đa.</summary>
    public const int VendorMaxLength = 200;

    /// <summary>Độ dài số hóa đơn / chứng từ tối đa.</summary>
    public const int ReceiptNoMaxLength = 50;

    /// <summary>Số dòng tối đa một bảng kê.</summary>
    public const int MaxLines = 200;

    private readonly List<SundryExpenseLine> _lines = [];

    private SundryExpenseSheet()
    {
    }

    /// <summary>Bộ phận phát sinh chi phí.</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>Tổng tiền = Σ số tiền dòng – numeric(19,2).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Các khoản chi.</summary>
    public IReadOnlyList<SundryExpenseLine> Lines => _lines;

    /// <inheritdoc />
    public override decimal ApprovalAmount => TotalAmount;

    /// <inheritdoc />
    public override string DocType => DocumentTypes.SundryExpenseSheet;

    /// <summary>Bộ phận của phiếu: tính định mức tháng (ngân sách) và người duyệt động Trưởng phòng ban / GĐ khối (lát O1).</summary>
    public override Guid? BudgetDepartmentId => DepartmentId;

    /// <summary>
    /// Giá trị tính vào định mức = tổng số tiền đã chi. Khác PR/PO (tiền hàng chưa VAT): chi phí tạp vụ thường là hóa đơn bán lẻ không tách thuế
    /// nên tính theo số tiền thực chi (tự đặt – design.md lát Y11a).
    /// </summary>
    public override decimal BudgetedAmount => TotalAmount;

    /// <inheritdoc />
    protected override int LineCount => _lines.Count;

    /// <summary>Lập bảng kê nháp.</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="no">Số phiếu.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <returns>Phiếu nháp chưa có dòng.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu phiếu sai.</exception>
    public static SundryExpenseSheet Create(Guid companyId, string no, SundryExpenseSheetHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Thiếu công ty lập phiếu.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Phiếu phải có số.");
        var sheet = new SundryExpenseSheet { CompanyId = companyId, No = no };
        sheet.Update(header);
        return sheet;
    }

    /// <summary>Sửa đầu phiếu khi còn nháp.</summary>
    /// <param name="header">Đầu phiếu.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dữ liệu sai.</exception>
    public void Update(SundryExpenseSheetHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (header.DepartmentId == Guid.Empty) throw new BusinessRuleException("Hãy chọn bộ phận phát sinh chi phí.");
        var note = Clean(header.Note);
        if (note?.Length > NoteMaxLength) throw new BusinessRuleException($"Ghi chú tối đa {NoteMaxLength} ký tự.");
        // Đổi ngày bảng kê thì ngày chi các dòng đang có phải kiểm lại (không sau ngày bảng kê) – kiểm trước khi ghi để phiếu không đổi dở dang.
        foreach (var line in _lines) EnsureExpenseDate(line.LineNo, line.ExpenseDate, header.Date);
        Date = header.Date;
        DepartmentId = header.DepartmentId;
        Note = note;
    }

    /// <summary>Đầu phiếu hiện tại.</summary>
    /// <returns>Bản sao đầu phiếu.</returns>
    public SundryExpenseSheetHeader HeaderData() => new(Date, DepartmentId, Note);

    /// <summary>
    /// Thay toàn bộ dòng khi còn nháp: ngày chi không sau ngày bảng kê (chỉ kê chi phí <b>đã phát sinh</b>), nội dung bắt buộc, số tiền &gt; 0
    /// (làm tròn 2 số lẻ), tối đa <see cref="MaxLines"/> dòng.
    /// </summary>
    /// <param name="lines">Các khoản chi theo thứ tự.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dòng sai.</exception>
    public void SetLines(IReadOnlyList<SundryExpenseLineData> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        EnsureDraft();
        if (lines.Count > MaxLines) throw new BusinessRuleException($"Bảng kê tối đa {MaxLines} dòng – lập thêm bảng kê khác.");
        var keep = new List<SundryExpenseLine>();
        for (var i = 0; i < lines.Count; i++)
        {
            var data = lines[i];
            var lineNo = i + 1;
            var where = $"Dòng {lineNo}";
            EnsureExpenseDate(lineNo, data.ExpenseDate, Date);
            var description = Clean(data.Description) ?? throw new BusinessRuleException($"{where}: hãy nhập nội dung chi.");
            if (description.Length > DescriptionMaxLength) throw new BusinessRuleException($"{where}: nội dung chi tối đa {DescriptionMaxLength} ký tự.");
            var vendor = Clean(data.Vendor);
            if (vendor?.Length > VendorMaxLength) throw new BusinessRuleException($"{where}: nơi mua / người bán tối đa {VendorMaxLength} ký tự.");
            var receiptNo = Clean(data.ReceiptNo);
            if (receiptNo?.Length > ReceiptNoMaxLength) throw new BusinessRuleException($"{where}: số hóa đơn / chứng từ tối đa {ReceiptNoMaxLength} ký tự.");
            var amount = decimal.Round(data.Amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0) throw new BusinessRuleException($"{where}: số tiền phải lớn hơn 0.");
            // Giữ lại dòng cũ cùng vị trí để nhật ký thay đổi ghi "sửa" thay vì "xóa + thêm".
            var line = i < _lines.Count ? _lines[i] : new SundryExpenseLine(Id);
            if (i >= _lines.Count) _lines.Add(line);
            line.Set(lineNo, data.ExpenseDate, description, vendor, receiptNo, amount);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
        TotalAmount = _lines.Sum(l => l.Amount);
    }

    /// <summary>Ngày chi không sau ngày bảng kê – bảng kê chỉ kê chi phí đã phát sinh (design.md mục 19.2 loại C).</summary>
    private static void EnsureExpenseDate(int lineNo, DateOnly expenseDate, DateOnly sheetDate)
    {
        if (expenseDate > sheetDate)
            throw new BusinessRuleException($"Dòng {lineNo}: ngày chi {expenseDate:dd/MM/yyyy} sau ngày bảng kê {sheetDate:dd/MM/yyyy} – chỉ kê chi phí đã phát sinh.");
    }
}

/// <summary>Một khoản chi trên bảng kê chi phí tạp vụ 2C.</summary>
[Auditable("Dòng bảng kê chi phí tạp vụ")]
public sealed class SundryExpenseLine : Entity
{
    private SundryExpenseLine()
    {
    }

    internal SundryExpenseLine(Guid sheetId) => SheetId = sheetId;

    /// <summary>Bảng kê.</summary>
    public Guid SheetId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Ngày chi.</summary>
    public DateOnly ExpenseDate { get; private set; }

    /// <summary>Nội dung chi.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Nơi mua / người bán.</summary>
    public string? Vendor { get; private set; }

    /// <summary>Số hóa đơn / chứng từ chi.</summary>
    public string? ReceiptNo { get; private set; }

    /// <summary>Số tiền đã chi (gồm thuế nếu có) – numeric(19,2).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Ghi dữ liệu dòng (đã kiểm tra ở bảng kê).</summary>
    internal void Set(int lineNo, DateOnly expenseDate, string description, string? vendor, string? receiptNo, decimal amount)
    {
        LineNo = lineNo;
        ExpenseDate = expenseDate;
        Description = description;
        Vendor = vendor;
        ReceiptNo = receiptNo;
        Amount = amount;
    }
}
