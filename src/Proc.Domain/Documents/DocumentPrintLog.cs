using Proc.Domain.Common;

namespace Proc.Domain.Documents;

/// <summary>Cách lấy bản in được đếm là một lần in (lát IN1 – người dùng đồng ý câu 3 ngày 18/11/2026).</summary>
public enum PrintKind
{
    /// <summary>Bấm "In…" trong hộp thoại xem phiếu.</summary>
    Print = 1,

    /// <summary>Bấm "Tải PDF" – file tải về in được nên cũng tính.</summary>
    Download = 2,
}

/// <summary>
/// Nhật ký in chứng từ (lát IN1 – design.md mục 20.3): mỗi lần In / Tải PDF một dòng, số lần in đánh liên tục theo phiếu để in vào góc dưới
/// phải "Lần in thứ n". Chỉ xem trên màn hình không ghi. Bảng chỉ thêm – không sửa, không xóa (như AuditLog).
/// </summary>
public sealed class DocumentPrintLog : Entity, ICompanyOwned
{
    private DocumentPrintLog()
    {
    }

    /// <summary>Công ty của phiếu.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Loại phiếu (<see cref="DocumentTypes"/>, ví dụ "PR").</summary>
    public string DocType { get; private set; } = string.Empty;

    /// <summary>Id phiếu.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Lần in thứ mấy của phiếu, từ 1 (duy nhất theo phiếu).</summary>
    public int PrintNo { get; private set; }

    /// <summary>In hay tải PDF.</summary>
    public PrintKind Kind { get; private set; }

    /// <summary>Người in.</summary>
    public Guid PrintedBy { get; private set; }

    /// <summary>Thời điểm in (UTC).</summary>
    public DateTimeOffset PrintedAt { get; private set; }

    /// <summary>Tình trạng phê duyệt in trên bản này, ví dụ "Đã duyệt đủ 8/8 bước" – để tra lại bản đã phát ra lúc nào ở trạng thái nào.</summary>
    public string ApprovalStatus { get; private set; } = string.Empty;

    /// <summary>Ghi một lần in.</summary>
    /// <param name="companyId">Công ty của phiếu.</param>
    /// <param name="docType">Loại phiếu.</param>
    /// <param name="documentId">Id phiếu.</param>
    /// <param name="printNo">Lần in thứ (lớn nhất hiện có + 1).</param>
    /// <param name="kind">In / tải.</param>
    /// <param name="printedBy">Người in.</param>
    /// <param name="printedAt">Thời điểm (UTC).</param>
    /// <param name="approvalStatus">Tình trạng phê duyệt in trên bản (≤ 200 ký tự).</param>
    /// <returns>Dòng nhật ký.</returns>
    public static DocumentPrintLog Create(
        Guid companyId, string docType, Guid documentId, int printNo, PrintKind kind, Guid printedBy, DateTimeOffset printedAt, string approvalStatus) =>
        new()
        {
            CompanyId = companyId,
            DocType = docType,
            DocumentId = documentId,
            PrintNo = printNo,
            Kind = kind,
            PrintedBy = printedBy,
            PrintedAt = printedAt,
            ApprovalStatus = approvalStatus.Length > 200 ? approvalStatus[..200] : approvalStatus,
        };
}
