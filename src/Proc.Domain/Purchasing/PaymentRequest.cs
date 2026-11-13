using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Purchasing;

/// <summary>Biểu mẫu đề nghị thanh toán (design.md mục 19.2, 19.3 bước 7 – lát Y8): theo loại mua của PO.</summary>
public enum PaymentRequestForm
{
    /// <summary>5A – vật tư công trình (PO loại A): đầu phiếu theo dự toán – lũy kế – còn lại của công trình.</summary>
    Construction = 1,

    /// <summary>5B – hàng hóa, dịch vụ (PO loại B, DV): đầu phiếu theo ngân sách (định mức/dự trù) bộ phận × tháng.</summary>
    Goods = 2,
}

/// <summary>Hình thức thanh toán – biểu mẫu "Phiếu đề nghị thanh toán tiền mặt" / "… chuyển khoản".</summary>
public enum PaymentMethod
{
    /// <summary>Tiền mặt.</summary>
    Cash = 1,

    /// <summary>Chuyển khoản – bắt buộc tài khoản ngân hàng của NCC.</summary>
    BankTransfer = 2,
}

/// <summary>Đầu phiếu đề nghị thanh toán do người lập nhập.</summary>
/// <param name="Date">Ngày đề nghị.</param>
/// <param name="Method">Hình thức thanh toán.</param>
/// <param name="DeductionAmount">Số tiền giảm trừ (tạm ứng đã chi, phạt chậm giao…) – không âm.</param>
/// <param name="DeductionReason">Lý do giảm trừ – bắt buộc khi có giảm trừ.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PaymentRequestHeader(DateOnly Date, PaymentMethod Method, decimal DeductionAmount, string? DeductionReason, string? Note);

/// <summary>Tài khoản ngân hàng của NCC (danh mục NCC – thay đổi tài khoản đã qua duyệt NCC, design.md mục 18.6) chụp vào phiếu.</summary>
/// <param name="Id">Tài khoản trong danh mục.</param>
/// <param name="BankName">Tên ngân hàng.</param>
/// <param name="Branch">Chi nhánh.</param>
/// <param name="AccountNumber">Số tài khoản.</param>
/// <param name="AccountName">Chủ tài khoản.</param>
public sealed record PaymentBankAccount(Guid Id, string BankName, string? Branch, string AccountNumber, string? AccountName);

/// <summary>Dữ liệu một dòng do người lập chọn.</summary>
/// <param name="ReceiptConfirmationId">XNH đã xác nhận có hóa đơn.</param>
/// <param name="Description">Nội dung thanh toán (trống thì hệ thống tự ghi).</param>
public sealed record PaymentRequestLineData(Guid ReceiptConfirmationId, string? Description);

/// <summary>
/// Thông tin XNH mà dòng đề nghị thanh toán lấy về (tầng trên nạp): hóa đơn nhập trên XNH (người dùng chốt 13/11/2026 – bỏ bước nhận hóa đơn
/// điện tử trên Portal, design.md mục 19.5).
/// </summary>
/// <param name="ReceiptConfirmationId">XNH.</param>
/// <param name="ReceiptNo">Số XNH.</param>
/// <param name="PurchaseOrderId">PO của XNH.</param>
/// <param name="Status">Trạng thái XNH.</param>
/// <param name="InvoiceNo">Số hóa đơn trên XNH.</param>
/// <param name="InvoiceSeries">Ký hiệu hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="InvoiceAmount">Tiền hóa đơn gồm VAT.</param>
public sealed record PaymentRequestLineSource(
    Guid ReceiptConfirmationId, string ReceiptNo, Guid PurchaseOrderId, ReceiptConfirmationStatus Status, string? InvoiceNo, string? InvoiceSeries,
    DateOnly? InvoiceDate, decimal? InvoiceAmount);

/// <summary>
/// Đề nghị thanh toán 5A/5B – số loại <see cref="DocumentTypes.PaymentRequest"/> (design.md mục 19.3 bước 7, lát Y8). Lập cho <b>một PO</b> đã duyệt /
/// đã đóng: mỗi dòng là một XNH đã xác nhận có hóa đơn (số, ký hiệu, ngày, tiền nhập trên XNH), số tiền dòng = tiền hóa đơn. Tổng − giảm trừ =
/// số tiền đề nghị. Chuyển khoản thì chụp tài khoản ngân hàng NCC vào phiếu (đổi danh mục sau không làm sai phiếu). Duyệt theo bộ cấp DNT (mẫu
/// CUVT: GĐ khối của phiếu → TP TC-NQ-KT → GĐ TC-NQ-KT → Chủ tịch); duyệt xong kế toán ghi <b>Đã chi</b>. Mỗi XNH chỉ thuộc một đề nghị chưa hủy
/// (tầng trên kiểm tra dưới khóa PO).
/// </summary>
[Auditable("Đề nghị thanh toán", LabelProperty = nameof(No))]
public sealed class PaymentRequest : ApprovableDocument
{
    /// <summary>Độ dài ghi chú, lý do tối đa.</summary>
    public const int NoteMaxLength = 1000;

    /// <summary>Độ dài nội dung thanh toán của dòng tối đa.</summary>
    public const int DescriptionMaxLength = 500;

    private readonly List<PaymentRequestLine> _lines = [];

    private PaymentRequest()
    {
    }

    /// <summary>Đơn mua hàng.</summary>
    public Guid PurchaseOrderId { get; private set; }

    /// <summary>NCC của PO.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Biểu mẫu 5A / 5B (theo loại mua của PO lúc lập).</summary>
    public PaymentRequestForm Form { get; private set; }

    /// <summary>Bộ phận đặt hàng của PO – người duyệt động GĐ khối của phiếu (lát O1); 5B lấy ngân sách tháng của bộ phận này.</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Công trình (5A).</summary>
    public Guid? ConstructionProjectId { get; private set; }

    /// <summary>Hình thức thanh toán.</summary>
    public PaymentMethod Method { get; private set; } = PaymentMethod.BankTransfer;

    /// <summary>Tài khoản NCC đã chọn (chuyển khoản).</summary>
    public Guid? BankAccountId { get; private set; }

    /// <summary>Tên ngân hàng (chụp lúc lưu).</summary>
    public string? BankName { get; private set; }

    /// <summary>Chi nhánh (chụp lúc lưu).</summary>
    public string? BankBranch { get; private set; }

    /// <summary>Số tài khoản (chụp lúc lưu).</summary>
    public string? BankAccountNumber { get; private set; }

    /// <summary>Chủ tài khoản (chụp lúc lưu).</summary>
    public string? BankAccountName { get; private set; }

    /// <summary>Tổng tiền các chứng từ = Σ tiền dòng – numeric(19,2).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Số tiền giảm trừ – numeric(19,2).</summary>
    public decimal DeductionAmount { get; private set; }

    /// <summary>Lý do giảm trừ.</summary>
    public string? DeductionReason { get; private set; }

    /// <summary>Số tiền đề nghị thanh toán = tổng − giảm trừ – numeric(19,2).</summary>
    public decimal PayableAmount { get; private set; }

    /// <summary>Ngày kế toán chi (null = chưa chi).</summary>
    public DateOnly? PaidDate { get; private set; }

    /// <summary>Người ghi Đã chi.</summary>
    public Guid? PaidBy { get; private set; }

    /// <summary>Thời điểm ghi Đã chi (UTC).</summary>
    public DateTimeOffset? PaidRecordedAt { get; private set; }

    /// <summary>Ghi chú khi chi (số ủy nhiệm chi, số phiếu chi…).</summary>
    public string? PaymentNote { get; private set; }

    /// <summary>Đã chi.</summary>
    public bool IsPaid => PaidDate is not null;

    /// <summary>Các dòng.</summary>
    public IReadOnlyList<PaymentRequestLine> Lines => _lines;

    /// <inheritdoc />
    public override decimal ApprovalAmount => PayableAmount;

    /// <inheritdoc />
    public override string DocType => DocumentTypes.PaymentRequest;

    /// <inheritdoc />
    public override IReadOnlyCollection<Guid> RelatedSupplierIds => [SupplierId];

    /// <summary>
    /// Bộ phận của phiếu – người duyệt động GĐ khối (lát O1). Đề nghị thanh toán không ghi ngân sách (PO đã tính lúc duyệt – tính lại sẽ đếm hai
    /// lần) nên không gọi <see cref="ApprovableDocument.ApplyBudgetCheck"/>.
    /// </summary>
    public override Guid? BudgetDepartmentId => DepartmentId;

    /// <inheritdoc />
    public override decimal BudgetedAmount => PayableAmount;

    /// <inheritdoc />
    protected override int LineCount => _lines.Count;

    /// <summary>
    /// Lập đề nghị nháp cho một PO (Y8-1): PO Đã duyệt hoặc đã đóng (nhận đủ / đóng phần còn lại); biểu mẫu theo loại mua của PO (A → 5A, B/DV → 5B).
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="no">Số phiếu.</param>
    /// <param name="order">PO.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <param name="bankAccount">Tài khoản NCC (chuyển khoản).</param>
    /// <returns>Phiếu nháp chưa có dòng.</returns>
    /// <exception cref="BusinessRuleException">Khi PO không hợp lệ hoặc đầu phiếu sai.</exception>
    public static PaymentRequest Create(Guid companyId, string no, PurchaseOrder order, PaymentRequestHeader header, PaymentBankAccount? bankAccount)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (companyId == Guid.Empty) throw new BusinessRuleException("Thiếu công ty lập phiếu.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Phiếu phải có số.");
        if (order.CompanyId != companyId) throw new BusinessRuleException("Đơn mua hàng thuộc công ty khác.");
        if (order.Status is not (ApprovalDocumentStatus.Approved or ApprovalDocumentStatus.Closed))
            throw new BusinessRuleException($"Đơn {order.No} chưa duyệt hoặc đã hủy – chỉ đề nghị thanh toán cho đơn đã duyệt.");
        var request = new PaymentRequest
        {
            CompanyId = companyId,
            No = no,
            PurchaseOrderId = order.Id,
            SupplierId = order.SupplierId,
            Form = order.Category == PurchaseCategory.Construction ? PaymentRequestForm.Construction : PaymentRequestForm.Goods,
            DepartmentId = order.DepartmentId,
            ConstructionProjectId = order.Category == PurchaseCategory.Construction ? order.ConstructionProjectId : null,
        };
        request.Update(header, bankAccount);
        return request;
    }

    /// <summary>Sửa đầu phiếu khi còn nháp (Y8-3, Y8-4).</summary>
    /// <param name="header">Đầu phiếu.</param>
    /// <param name="bankAccount">Tài khoản NCC – bắt buộc khi chuyển khoản, bỏ qua khi tiền mặt.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dữ liệu sai.</exception>
    public void Update(PaymentRequestHeader header, PaymentBankAccount? bankAccount)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (!Enum.IsDefined(header.Method)) throw new BusinessRuleException("Hình thức thanh toán phải là tiền mặt hoặc chuyển khoản.");
        if (header.Method == PaymentMethod.BankTransfer && bankAccount is null)
            throw new BusinessRuleException("Thanh toán chuyển khoản phải chọn tài khoản ngân hàng của NCC (khai ở danh mục NCC).");
        if (header.DeductionAmount < 0) throw new BusinessRuleException("Số tiền giảm trừ không được âm.");
        var deductionReason = Clean(header.DeductionReason);
        if (header.DeductionAmount > 0 && deductionReason is null)
            throw new BusinessRuleException("Có giảm trừ thì phải ghi lý do (tạm ứng đã chi, phạt chậm giao…).");
        if (deductionReason?.Length > NoteMaxLength) throw new BusinessRuleException($"Lý do giảm trừ tối đa {NoteMaxLength} ký tự.");
        var note = Clean(header.Note);
        if (note?.Length > NoteMaxLength) throw new BusinessRuleException($"Ghi chú tối đa {NoteMaxLength} ký tự.");
        Date = header.Date;
        Method = header.Method;
        var bank = header.Method == PaymentMethod.BankTransfer ? bankAccount : null;
        BankAccountId = bank?.Id;
        BankName = bank?.BankName;
        BankBranch = bank?.Branch;
        BankAccountNumber = bank?.AccountNumber;
        BankAccountName = bank?.AccountName;
        DeductionAmount = decimal.Round(header.DeductionAmount, 2, MidpointRounding.AwayFromZero);
        DeductionReason = header.DeductionAmount > 0 ? deductionReason : null;
        Note = note;
        Recalculate();
    }

    /// <summary>Đầu phiếu hiện tại.</summary>
    /// <returns>Bản sao đầu phiếu.</returns>
    public PaymentRequestHeader HeaderData() => new(Date, Method, DeductionAmount, DeductionReason, Note);

    /// <summary>
    /// Thay toàn bộ dòng khi còn nháp (Y8-2): mỗi XNH một lần, thuộc PO của phiếu, đã xác nhận, có số hóa đơn và tiền hóa đơn &gt; 0. Thông tin hóa
    /// đơn chụp từ XNH lúc lưu (lưu lại lúc gửi duyệt) – sau khi gửi không đổi theo XNH nữa.
    /// </summary>
    /// <param name="lines">Dòng người lập chọn kèm thông tin XNH.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dòng không hợp lệ.</exception>
    public void SetLines(IReadOnlyList<(PaymentRequestLineData Data, PaymentRequestLineSource Source)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        EnsureDraft();
        if (lines.GroupBy(l => l.Data.ReceiptConfirmationId).Any(g => g.Count() > 1))
            throw new BusinessRuleException("Mỗi phiếu nhận hàng chỉ chọn một lần trên đề nghị thanh toán.");
        var keep = new List<PaymentRequestLine>();
        var lineNo = 0;
        foreach (var (data, source) in lines)
        {
            if (data.ReceiptConfirmationId != source.ReceiptConfirmationId) throw new BusinessRuleException("Phiếu nhận hàng không khớp dữ liệu nạp.");
            var where = $"Phiếu nhận hàng {source.ReceiptNo}";
            if (source.PurchaseOrderId != PurchaseOrderId) throw new BusinessRuleException($"{where} không thuộc đơn mua hàng của đề nghị.");
            if (source.Status != ReceiptConfirmationStatus.Confirmed) throw new BusinessRuleException($"{where} chưa xác nhận hoặc đã hủy.");
            // Q16: chỉ thanh toán hàng đã nhận, có hóa đơn – hóa đơn nhập ở XNH ("Cập nhật hóa đơn").
            if (string.IsNullOrWhiteSpace(source.InvoiceNo) || source.InvoiceAmount is not > 0)
                throw new BusinessRuleException($"{where} chưa có hóa đơn (số và tiền hóa đơn) – cập nhật hóa đơn trên phiếu nhận hàng trước.");
            var description = Clean(data.Description);
            if (description?.Length > DescriptionMaxLength)
                throw new BusinessRuleException($"{where}: nội dung thanh toán tối đa {DescriptionMaxLength} ký tự.");
            var line = _lines.FirstOrDefault(l => l.ReceiptConfirmationId == data.ReceiptConfirmationId);
            if (line is null)
            {
                line = new PaymentRequestLine(Id);
                _lines.Add(line);
            }

            var series = string.IsNullOrWhiteSpace(source.InvoiceSeries) ? string.Empty : $" {source.InvoiceSeries.Trim()}";
            line.Set(++lineNo, source, description ?? $"Thanh toán hóa đơn{series} số {source.InvoiceNo!.Trim()} theo phiếu nhận hàng {source.ReceiptNo}");
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
        Recalculate();
    }

    /// <summary>
    /// Kiểm tra trước khi gửi duyệt: số tiền đề nghị phải &gt; 0 (giảm trừ không vượt tổng).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi số tiền đề nghị ≤ 0.</exception>
    public void EnsureSubmittable()
    {
        if (PayableAmount <= 0)
            throw new BusinessRuleException("Số tiền đề nghị thanh toán (tổng − giảm trừ) phải lớn hơn 0.");
    }

    /// <summary>Kế toán ghi Đã chi (Y8-6): phiếu Đã duyệt, chưa chi; ngày chi không trước ngày duyệt (giờ Việt Nam do tầng trên đổi) và không sau hôm nay.</summary>
    /// <param name="paidDate">Ngày chi.</param>
    /// <param name="note">Ghi chú (số ủy nhiệm chi, phiếu chi…).</param>
    /// <param name="userId">Người ghi.</param>
    /// <param name="approvedDate">Ngày duyệt xong (giờ Việt Nam).</param>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <param name="at">Thời điểm ghi (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi phiếu chưa duyệt, đã chi, hoặc ngày chi sai.</exception>
    public void MarkPaid(DateOnly paidDate, string? note, Guid userId, DateOnly approvedDate, DateOnly today, DateTimeOffset at)
    {
        if (Status != ApprovalDocumentStatus.Approved) throw new BusinessRuleException($"Đề nghị {No} chưa duyệt xong hoặc đã hủy – chưa chi được.");
        if (IsPaid) throw new BusinessRuleException($"Đề nghị {No} đã ghi Đã chi ngày {PaidDate:dd/MM/yyyy}.");
        if (paidDate > today) throw new BusinessRuleException("Ngày chi không được sau hôm nay.");
        if (paidDate < approvedDate) throw new BusinessRuleException($"Ngày chi không được trước ngày duyệt {approvedDate:dd/MM/yyyy}.");
        var cleaned = Clean(note);
        if (cleaned?.Length > NoteMaxLength) throw new BusinessRuleException($"Ghi chú chi tối đa {NoteMaxLength} ký tự.");
        PaidDate = paidDate;
        PaymentNote = cleaned;
        PaidBy = userId;
        PaidRecordedAt = at;
    }

    /// <summary>Tổng, số tiền đề nghị. Ví dụ hai hóa đơn 1.100.000 + 550.000, giảm trừ 150.000 → đề nghị 1.500.000.</summary>
    private void Recalculate()
    {
        TotalAmount = _lines.Sum(l => l.Amount);
        PayableAmount = TotalAmount - DeductionAmount;
    }
}

/// <summary>Dòng đề nghị thanh toán – một XNH đã xác nhận có hóa đơn.</summary>
[Auditable("Dòng đề nghị thanh toán")]
public sealed class PaymentRequestLine : Entity
{
    private PaymentRequestLine()
    {
    }

    internal PaymentRequestLine(Guid requestId) => RequestId = requestId;

    /// <summary>Đề nghị thanh toán.</summary>
    public Guid RequestId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>XNH.</summary>
    public Guid ReceiptConfirmationId { get; private set; }

    /// <summary>Số hóa đơn (chụp từ XNH).</summary>
    public string InvoiceNo { get; private set; } = string.Empty;

    /// <summary>Ký hiệu hóa đơn (chụp từ XNH).</summary>
    public string? InvoiceSeries { get; private set; }

    /// <summary>Ngày hóa đơn (chụp từ XNH).</summary>
    public DateOnly? InvoiceDate { get; private set; }

    /// <summary>Số tiền = tiền hóa đơn gồm VAT – numeric(19,2).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Nội dung thanh toán.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Ghi dữ liệu dòng từ XNH.</summary>
    internal void Set(int lineNo, PaymentRequestLineSource source, string description)
    {
        LineNo = lineNo;
        ReceiptConfirmationId = source.ReceiptConfirmationId;
        InvoiceNo = source.InvoiceNo!.Trim();
        InvoiceSeries = string.IsNullOrWhiteSpace(source.InvoiceSeries) ? null : source.InvoiceSeries.Trim();
        InvoiceDate = source.InvoiceDate;
        Amount = decimal.Round(source.InvoiceAmount!.Value, 2, MidpointRounding.AwayFromZero);
        Description = description;
    }
}
