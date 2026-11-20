using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Printing;

/// <summary>Một bước duyệt của lượt hiện tại, đủ thông tin để ghép vào ô ký của mẫu 1A/1B (lát IN1).</summary>
/// <param name="Stage">Chặng (1 = duyệt thường, 9 = cấp cao nhất dời ra sau, còn lại là chặng vượt – <see cref="ApprovalTypes"/>).</param>
/// <param name="Level">Cấp trong chặng.</param>
/// <param name="Kind">Loại người duyệt (vai trò, Trưởng phòng ban, GĐ khối, Thủ kho).</param>
/// <param name="RoleCode">Mã vai trò của cấp (vai trò mẫu: TPCUVT, TPTCKT, TPHC, PHOCTUBCL, CHUTICH…).</param>
/// <param name="RoleName">Tên vai trò / nhãn bước – dùng làm chức danh ô ký phát sinh.</param>
/// <param name="Status">"Pending", "Approved", "Rejected" (bước "Skipped" đã bỏ trước khi gọi).</param>
/// <param name="ActorName">Họ tên in dưới chữ ký (đã gồm danh xưng, "(thay …)" khi duyệt theo ủy quyền).</param>
/// <param name="ActedAt">Thời điểm duyệt / từ chối.</param>
/// <param name="Signature">Ảnh chữ ký có hiệu lực tại thời điểm duyệt; null = chưa tải chữ ký.</param>
public sealed record SheetApprovalStep(
    int Stage, int Level, ApproverKind Kind, string? RoleCode, string? RoleName, string Status, string? ActorName, DateTimeOffset? ActedAt,
    SheetImage? Signature);

/// <summary>Ảnh in (chữ ký, logo) kèm kích thước để giữ tỉ lệ.</summary>
/// <param name="Content">Nội dung PNG/JPEG.</param>
/// <param name="Width">Rộng (điểm ảnh).</param>
/// <param name="Height">Cao (điểm ảnh).</param>
public sealed record SheetImage(byte[] Content, int Width, int Height);

/// <summary>Một ô ký của khối ký.</summary>
/// <param name="Title">Chức danh in đậm – nguyên văn mẫu ("Thủ kho", "Trưởng Phòng/Ban"…).</param>
/// <param name="Caption">Dòng nghiêng trong ngoặc – nguyên văn mẫu ("(Xác nhận hàng tồn kho)").</param>
/// <param name="Name">Họ tên dưới chữ ký; null = chưa ký.</param>
/// <param name="Detail">Dòng nhỏ: "Đã duyệt 09:05 07/11/2026", "Chưa duyệt", "Không thuộc chuỗi duyệt".</param>
/// <param name="Signature">Ảnh chữ ký; null = không in ảnh.</param>
/// <param name="InChain">Phiếu có bước duyệt cho ô này.</param>
public sealed record SheetSignBox(string Title, string Caption, string? Name, string? Detail, SheetImage? Signature, bool InChain = true);

/// <summary>Khối ký theo mẫu: dòng "Kính trình…", 8 ô cố định (2 hàng × 4), ô phát sinh (chặng vượt…), ô Chủ tịch cả hàng.</summary>
/// <param name="Heading">Dòng đầu khối – nguyên văn mẫu.</param>
/// <param name="Boxes">8 ô cố định theo thứ tự mẫu.</param>
/// <param name="Extra">Ô của bước không có chỗ trên mẫu (chặng vượt dự toán / ngân sách, vai trò tự tạo) – in một hàng trước ô Chủ tịch.</param>
/// <param name="Final">Ô Chủ tịch HĐQT/TGĐ.</param>
/// <param name="ApprovalStatus">Tình trạng phê duyệt dạng chữ cho góc dưới trái trang cuối ("Đã duyệt đủ 8/8 bước…").</param>
public sealed record SheetSignBlock(
    string Heading, IReadOnlyList<SheetSignBox> Boxes, IReadOnlyList<SheetSignBox> Extra, SheetSignBox Final, string ApprovalStatus);

/// <summary>
/// Thông tin hệ thống chữ 9: khối đầy đủ ở góc dưới trái trang cuối, các trang trước một dòng nhỏ "Số phiếu … · Trang x/y" (người dùng yêu cầu
/// 18/11/2026, đổi vị trí 20/11/2026).
/// </summary>
/// <param name="Lines">Các dòng của khối đầy đủ; "{PAGE}" / "{PAGES}" thay bằng số trang.</param>
/// <param name="Watermark">Dấu chìm giữa trang ("BẢN NHÁP", "CHƯA DUYỆT", "ĐÃ HỦY"); null = phiếu đã duyệt.</param>
/// <param name="DocNo">Số phiếu cho dòng nhỏ ở các trang trước trang cuối; null = không in dòng đó.</param>
public sealed record SheetStamp(IReadOnlyList<string> Lines, string? Watermark, string? DocNo = null);

/// <summary>
/// Ghép ô ký của đề nghị mua theo mẫu 1A/1B (lát IN1 – design.md mục 20.4). Khối ký giữ <b>nguyên văn</b> ảnh trong file Excel biểu mẫu (người dùng
/// chốt 18/11/2026: "form như thế nào thì giữ nguyên như vậy"); bước duyệt ghép vào ô theo loại người duyệt / mã vai trò mẫu:
/// Thủ kho nơi nhận → "Thủ kho"; Trưởng phòng ban của phiếu → "Trưởng Phòng/Ban"; GĐ khối của phiếu → "GĐ Khối phòng ban"; vai trò TPCUVT →
/// "Phòng CUVT"; TPTCKT → "Phòng TC-NQ-KT"; TPHC → "Trưởng phòng HC"; PHOCTUBCL → "Phó CTUBCL &amp; PT"; CHUTICH → "CHỦ TỊCH HĐQT/TGĐ". Cấp cao nhất
/// của chuỗi thường không khớp mã nào thì vào ô Chủ tịch; bước còn lại (chặng vượt, vai trò tự tạo, hai cấp trùng ô) in ô phát sinh.
/// </summary>
public static class PurchaseRequestSheet
{
    /// <summary>Dòng đầu khối ký – nguyên văn mẫu (câu 9, người dùng chốt 18/11/2026).</summary>
    public const string Heading = SheetSign.Heading;

    /// <summary>Khối ký 1A/1B nguyên văn ảnh trong sheet: 8 ô (2 hàng × 4) + ô Chủ tịch cả hàng.</summary>
    public static readonly SheetSignSpec Spec = new(
        Heading,
        new("", "Người đề xuất", "(Trình ký đề xuất)"),
        [
            new("Storekeeper", "Thủ kho", "(Xác nhận hàng tồn kho)"),
            new("DepartmentHead", "Trưởng Phòng/Ban", "(Ký kiểm tra đề nghị)"),
            new("TPCUVT", "Phòng CUVT", "(Kiểm tra mua hàng)"),
            new("TPTCKT", "Phòng TC-NQ-KT", "(Kiểm tra dự toán, hàng tồn)"),
            new("TPHC", "Trưởng phòng HC", "(Kiểm tra định mức)"),
            new("DivisionDirector", "GĐ Khối phòng ban", "(Ký duyệt đề nghị)"),
            new("PHOCTUBCL", "Phó CTUBCL & PT", "(Ký duyệt đề nghị)"),
        ],
        new("", "CHỦ TỊCH HĐQT/TGĐ", "(Duyệt đồng ý)"));

    /// <summary>Dựng khối ký.</summary>
    /// <param name="status">Trạng thái phiếu (Draft, Submitted, Approved, Rejected, Cancelled…).</param>
    /// <param name="requesterName">Họ tên người đề xuất (đã gồm danh xưng).</param>
    /// <param name="submittedAt">Lúc gửi duyệt của lượt hiện tại; null = chưa gửi.</param>
    /// <param name="requesterSignature">Chữ ký người đề xuất có hiệu lực lúc gửi (chỉ khi chính người đề xuất gửi).</param>
    /// <param name="submittedByOther">Người gửi duyệt khi khác người đề xuất (in "Gửi duyệt bởi …").</param>
    /// <param name="steps">Bước duyệt của lượt hiện tại (đã bỏ bước "Skipped").</param>
    /// <returns>Khối ký.</returns>
    public static SheetSignBlock Build(
        string status, string? requesterName, DateTimeOffset? submittedAt, SheetImage? requesterSignature, string? submittedByOther,
        IReadOnlyList<SheetApprovalStep> steps) =>
        SheetSign.Build(Spec, status, requesterName, submittedAt, requesterSignature, submittedByOther, steps);
}
