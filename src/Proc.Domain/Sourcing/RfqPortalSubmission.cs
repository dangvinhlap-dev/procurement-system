using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Proc.Domain.Common;

namespace Proc.Domain.Sourcing;

/// <summary>Đầu báo giá NCC nộp trên Portal (lát P3a): không có điểm chấm – điểm do Phòng CUVT chấm sau khi mở.</summary>
/// <param name="ValidUntil">Báo giá hiệu lực đến (không trước hôm nay).</param>
/// <param name="PaymentTerms">Điều kiện thanh toán NCC chào.</param>
/// <param name="Note">Ghi chú của NCC.</param>
public sealed record PortalQuoteHeader(DateOnly? ValidUntil, string? PaymentTerms, string? Note);

/// <summary>
/// Một lần NCC nộp báo giá cho RFQ trên Portal (lát P3a – design.md mục 19.5, Q15). Mỗi lần nộp (kể cả nộp lại) là một bản mới, bản có
/// <see cref="Sequence"/> lớn nhất là bản có hiệu lực. Bản nộp **niêm phong** đến khi Phòng CUVT mở sau thời điểm hạn nộp
/// (<see cref="Rfq.OpenPortalQuotes"/>): API nội bộ không trả giá trước giờ mở, trừ người có quyền "Xem báo giá Portal trước giờ mở"
/// (mỗi lần xem ghi nhật ký truy cập). Vì vậy bảng này **không** gắn [Auditable] – nhật ký thay đổi ai cũng đọc được sẽ làm lộ giá; dấu vết
/// nộp là chính bản ghi (người nộp, giờ nộp, mã băm) vì bản nộp không sửa, không xóa.
/// </summary>
public sealed class RfqPortalSubmission : Entity, ICompanyOwned
{
    private readonly List<RfqPortalSubmissionLine> _lines = [];

    private RfqPortalSubmission()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>RFQ.</summary>
    public Guid RfqId { get; private set; }

    /// <summary>NCC nộp (lấy từ phiên Portal, không nhận từ client).</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Lần nộp thứ mấy của NCC cho RFQ này (1, 2, …).</summary>
    public int Sequence { get; private set; }

    /// <summary>Thời điểm nộp (UTC).</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>Tài khoản Portal nộp.</summary>
    public Guid SubmittedByPortalUserId { get; private set; }

    /// <summary>Email tài khoản Portal lúc nộp (giữ lại dù tài khoản đổi sau này).</summary>
    public string SubmittedByEmail { get; private set; } = string.Empty;

    /// <summary>Ngày báo giá = ngày nộp theo giờ Việt Nam.</summary>
    public DateOnly QuoteDate { get; private set; }

    /// <summary>Hiệu lực báo giá đến.</summary>
    public DateOnly? ValidUntil { get; private set; }

    /// <summary>Điều kiện thanh toán.</summary>
    public string? PaymentTerms { get; private set; }

    /// <summary>Ghi chú của NCC.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Mã băm SHA-256 (64 ký tự hex) của nội dung bản nộp – NCC giữ làm biên nhận, đối chiếu được bản Phòng CUVT mở có đúng bản đã nộp
    /// (<see cref="ComputeHash"/>).
    /// </summary>
    public string ContentHash { get; private set; } = string.Empty;

    /// <summary>Giá từng dòng.</summary>
    public IReadOnlyList<RfqPortalSubmissionLine> Lines => _lines;

    /// <summary>
    /// Lập bản nộp: RFQ đang nhận báo giá, chưa mở báo giá Portal, chưa qua thời điểm hạn nộp; NCC được mời; mỗi dòng thuộc RFQ, không
    /// trùng; ít nhất một dòng có giá. Dòng không gửi = NCC không chào dòng đó.
    /// </summary>
    /// <param name="rfq">RFQ (đã nạp dòng và NCC được mời).</param>
    /// <param name="supplierId">NCC của phiên Portal.</param>
    /// <param name="sequence">Lần nộp (số lần đã nộp + 1).</param>
    /// <param name="portalUserId">Tài khoản Portal nộp.</param>
    /// <param name="email">Email tài khoản.</param>
    /// <param name="at">Thời điểm nộp (UTC).</param>
    /// <param name="now">Bây giờ theo giờ Việt Nam.</param>
    /// <param name="header">Đầu báo giá.</param>
    /// <param name="lines">Giá từng dòng.</param>
    /// <returns>Bản nộp mới.</returns>
    /// <exception cref="BusinessRuleException">Khi vi phạm một điều kiện trên.</exception>
    public static RfqPortalSubmission Create(
        Rfq rfq, Guid supplierId, int sequence, Guid portalUserId, string email, DateTimeOffset at, DateTime now, PortalQuoteHeader header,
        IReadOnlyList<QuotationLineData> lines)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);
        if (rfq.Status != RfqStatus.Sent || rfq.PortalQuotesOpenedAt is not null)
            throw new BusinessRuleException($"RFQ {rfq.No} không còn nhận báo giá.");
        if (now > rfq.DeadlineAt) throw new BusinessRuleException($"Đã hết hạn nộp báo giá ({rfq.DeadlineText}).");
        if (rfq.Suppliers.All(s => s.SupplierId != supplierId)) throw new BusinessRuleException("Nhà cung cấp không được mời báo giá RFQ này.");
        if (sequence < 1) throw new BusinessRuleException("Lần nộp không hợp lệ.");
        if (lines.Count == 0) throw new BusinessRuleException("Hãy nhập giá cho ít nhất một dòng hàng.");
        if (lines.GroupBy(l => l.RfqLineId).Any(g => g.Count() > 1)) throw new BusinessRuleException("Mỗi dòng hàng chỉ một giá.");
        if (lines.Any(l => rfq.Lines.All(r => r.Id != l.RfqLineId)))
            throw new BusinessRuleException("Báo giá có dòng không thuộc RFQ – hãy tải lại trang.");
        var quoteDate = DateOnly.FromDateTime(now);
        if (header.ValidUntil is { } until && until < quoteDate) throw new BusinessRuleException("Hiệu lực báo giá không được trước hôm nay.");
        var terms = Clean(header.PaymentTerms);
        if (terms?.Length > 200) throw new BusinessRuleException("Điều kiện thanh toán tối đa 200 ký tự.");
        var note = Clean(header.Note);
        if (note?.Length > 1000) throw new BusinessRuleException("Ghi chú báo giá tối đa 1.000 ký tự.");

        var submission = new RfqPortalSubmission
        {
            CompanyId = rfq.CompanyId,
            RfqId = rfq.Id,
            SupplierId = supplierId,
            Sequence = sequence,
            SubmittedAt = at,
            SubmittedByPortalUserId = portalUserId,
            SubmittedByEmail = email,
            QuoteDate = quoteDate,
            ValidUntil = header.ValidUntil,
            PaymentTerms = terms,
            Note = note,
        };
        // Lưu theo thứ tự dòng của RFQ để mã băm không phụ thuộc thứ tự client gửi.
        foreach (var data in lines.OrderBy(l => rfq.Lines.First(r => r.Id == l.RfqLineId).LineNo))
            submission._lines.Add(new RfqPortalSubmissionLine(submission.Id, QuotationLine.Normalize(data)));
        submission.ContentHash = submission.ComputeHash();
        return submission;
    }

    /// <summary>
    /// Mã băm SHA-256 của nội dung: RFQ, NCC, lần nộp, giờ nộp (UTC, đến mili giây), ngày báo giá, hiệu lực, điều kiện thanh toán, ghi chú và
    /// từng dòng (Id dòng RFQ, đơn giá, %CK, thuế, ngày giao, ghi chú) nối bằng ký tự "|" theo văn hóa bất biến. Đổi bất kỳ giá trị nào thì
    /// mã khác hẳn – NCC so mã trên biên nhận với mã Phòng CUVT thấy khi mở.
    /// </summary>
    /// <returns>64 ký tự hex chữ thường.</returns>
    public string ComputeHash()
    {
        var c = CultureInfo.InvariantCulture;
        var text = new StringBuilder()
            .Append(RfqId.ToString("N")).Append('|').Append(SupplierId.ToString("N")).Append('|').Append(Sequence.ToString(c)).Append('|')
            .Append(SubmittedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", c)).Append('|')
            .Append(QuoteDate.ToString("yyyy-MM-dd", c)).Append('|').Append(ValidUntil?.ToString("yyyy-MM-dd", c)).Append('|')
            .Append(PaymentTerms).Append('|').Append(Note);
        foreach (var l in _lines)
            text.Append("\n").Append(l.RfqLineId.ToString("N")).Append('|').Append(l.UnitPrice.ToString("0.######", c)).Append('|')
                .Append(l.DiscountPct.ToString("0.####", c)).Append('|').Append(l.VatCode).Append('|')
                .Append(l.LeadTimeDays?.ToString(c)).Append('|').Append(l.Note);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Giá một dòng RFQ trong bản nộp Portal (theo ĐVT của dòng RFQ).</summary>
public sealed class RfqPortalSubmissionLine : Entity
{
    private RfqPortalSubmissionLine()
    {
    }

    internal RfqPortalSubmissionLine(Guid submissionId, QuotationLineData data)
    {
        SubmissionId = submissionId;
        RfqLineId = data.RfqLineId;
        UnitPrice = data.UnitPrice;
        DiscountPct = data.DiscountPct;
        VatCode = data.VatCode;
        LeadTimeDays = data.LeadTimeDays;
        Note = data.Note;
    }

    /// <summary>Bản nộp.</summary>
    public Guid SubmissionId { get; private set; }

    /// <summary>Dòng RFQ.</summary>
    public Guid RfqLineId { get; private set; }

    /// <summary>Đơn giá trước VAT – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>% chiết khấu.</summary>
    public decimal DiscountPct { get; private set; }

    /// <summary>Mã thuế suất.</summary>
    public string? VatCode { get; private set; }

    /// <summary>Thời gian giao (ngày).</summary>
    public int? LeadTimeDays { get; private set; }

    /// <summary>Ghi chú dòng.</summary>
    public string? Note { get; private set; }
}
