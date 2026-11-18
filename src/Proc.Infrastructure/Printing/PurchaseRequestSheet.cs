using System.Globalization;
using Proc.Application.Common;
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
/// <param name="ApprovalStatus">Tình trạng phê duyệt dạng chữ cho góc dưới phải ("Đã duyệt đủ 8/8 bước…").</param>
public sealed record SheetSignBlock(
    string Heading, IReadOnlyList<SheetSignBox> Boxes, IReadOnlyList<SheetSignBox> Extra, SheetSignBox Final, string ApprovalStatus);

/// <summary>Thông tin hệ thống in góc dưới phải mọi trang, chữ 9 (người dùng yêu cầu 18/11/2026).</summary>
/// <param name="Lines">Các dòng; "{PAGE}" / "{PAGES}" thay bằng số trang.</param>
/// <param name="Watermark">Dấu chìm giữa trang ("BẢN NHÁP", "CHƯA DUYỆT", "ĐÃ HỦY"); null = phiếu đã duyệt.</param>
public sealed record SheetStamp(IReadOnlyList<string> Lines, string? Watermark);

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
    public const string Heading = "Kính trình Chủ Tịch HĐQT/TGĐ xem xét và ký duyệt";

    private const string NotInChain = "Không thuộc chuỗi duyệt";

    /// <summary>Ô cố định theo thứ tự mẫu (trừ "Người đề xuất" ở đầu, "Chủ tịch" ở cuối) – chức danh, chú thích nguyên văn.</summary>
    private static readonly (string Slot, string Title, string Caption)[] Slots =
    [
        ("Storekeeper", "Thủ kho", "(Xác nhận hàng tồn kho)"),
        ("DepartmentHead", "Trưởng Phòng/Ban", "(Ký kiểm tra đề nghị)"),
        ("TPCUVT", "Phòng CUVT", "(Kiểm tra mua hàng)"),
        ("TPTCKT", "Phòng TC-NQ-KT", "(Kiểm tra dự toán, hàng tồn)"),
        ("TPHC", "Trưởng phòng HC", "(Kiểm tra định mức)"),
        ("DivisionDirector", "GĐ Khối phòng ban", "(Ký duyệt đề nghị)"),
        ("PHOCTUBCL", "Phó CTUBCL & PT", "(Ký duyệt đề nghị)"),
    ];

    private const string ChairmanSlot = "CHUTICH";

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
        IReadOnlyList<SheetApprovalStep> steps)
    {
        var ordered = steps.OrderBy(s => s.Stage).ThenBy(s => s.Level).ToList();
        // Cấp cao nhất của chuỗi thường: chặng 9 nếu đã dời ra sau (có chặng vượt), không thì cấp lớn nhất của chặng 1.
        var top = ordered.Where(s => s.Stage == ApprovalTypes.TopLevelStage).LastOrDefault()
                  ?? ordered.Where(s => s.Stage == 1).LastOrDefault();
        var assigned = new Dictionary<string, SheetApprovalStep>();
        var extra = new List<SheetApprovalStep>();
        foreach (var step in ordered)
        {
            var slot = SlotOf(step);
            if (slot is null && step == top && !assigned.ContainsKey(ChairmanSlot)) slot = ChairmanSlot;
            if (slot is not null && step.Stage is 1 or ApprovalTypes.TopLevelStage && assigned.TryAdd(slot, step)) continue;
            extra.Add(step);
        }

        var draft = status == "Draft" || submittedAt is null;
        var requesterDetail = draft ? "Chưa gửi duyệt"
            : submittedByOther is not null ? $"Gửi duyệt bởi {submittedByOther} {Time(submittedAt)}"
            : $"Gửi duyệt {Time(submittedAt)}";
        var boxes = new List<SheetSignBox>
        {
            new("Người đề xuất", "(Trình ký đề xuất)", requesterName, requesterDetail, draft ? null : requesterSignature),
        };
        boxes.AddRange(Slots.Select(s => assigned.TryGetValue(s.Slot, out var step)
            ? Box(s.Title, s.Caption, step, draft)
            : Missing(s.Title, s.Caption, ordered.Count == 0)));
        var final = assigned.TryGetValue(ChairmanSlot, out var chairman)
            ? Box("CHỦ TỊCH HĐQT/TGĐ", "(Duyệt đồng ý)", chairman, draft, upper: true)
            : Missing("CHỦ TỊCH HĐQT/TGĐ", "(Duyệt đồng ý)", ordered.Count == 0);
        var extraBoxes = extra.Select(s => Box(s.RoleName ?? $"Cấp {s.Level}", ExtraCaption(s.Stage), s, draft)).ToList();
        return new SheetSignBlock(Heading, boxes, extraBoxes, final, StatusText(status, draft, ordered, assigned));
    }

    /// <summary>Ô của một bước: đã duyệt → chữ ký + họ tên + giờ; từ chối → họ tên + giờ (không chữ ký); chưa duyệt → trống.</summary>
    private static SheetSignBox Box(string title, string caption, SheetApprovalStep step, bool draft, bool upper = false) => step.Status switch
    {
        _ when draft => new SheetSignBox(title, caption, null, null, null),
        "Approved" => new SheetSignBox(
            title, caption, upper ? step.ActorName?.ToUpper(Vi) : step.ActorName,
            step.Signature is null ? $"Đã duyệt điện tử {Time(step.ActedAt)}" : $"Đã duyệt {Time(step.ActedAt)}", step.Signature),
        "Rejected" => new SheetSignBox(title, caption, upper ? step.ActorName?.ToUpper(Vi) : step.ActorName, $"Từ chối {Time(step.ActedAt)}", null),
        _ => new SheetSignBox(title, caption, null, "Chưa duyệt", null),
    };

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>
    /// Ô không có bước duyệt: phiếu đã gửi duyệt thì ghi "Không thuộc chuỗi duyệt"; phiếu nháp chưa có chuỗi duyệt (chuỗi lập lúc gửi) thì để
    /// trống như mẫu giấy.
    /// </summary>
    private static SheetSignBox Missing(string title, string caption, bool noChainYet) =>
        new(title, caption, null, noChainYet ? null : NotInChain, null, InChain: noChainYet);

    private static string? SlotOf(SheetApprovalStep step) => step.Kind switch
    {
        ApproverKind.Storekeeper => "Storekeeper",
        ApproverKind.DepartmentHead => "DepartmentHead",
        ApproverKind.DivisionDirector => "DivisionDirector",
        _ => step.RoleCode?.ToUpperInvariant() switch
        {
            "TRUONGPHONGBAN" => "DepartmentHead",
            "GDKHOI" => "DivisionDirector",
            "TPCUVT" => "TPCUVT",
            "TPTCKT" => "TPTCKT",
            "TPHC" => "TPHC",
            "PHOCTUBCL" => "PHOCTUBCL",
            "CHUTICH" => ChairmanSlot,
            _ => null,
        },
    };

    private static string ExtraCaption(int stage) => stage switch
    {
        ApprovalTypes.OverBudgetStage => "(Duyệt vượt ngân sách/định mức)",
        ApprovalTypes.OverEstimateStage => "(Duyệt vượt dự toán)",
        ApprovalTypes.OffListStage => "(Duyệt mua ngoài danh sách)",
        _ => "(Ký duyệt)",
    };

    /// <summary>
    /// Tình trạng phê duyệt cho góc dưới phải: "Nháp – chưa gửi duyệt"; "Đang chờ duyệt – đã duyệt 3/8 bước, chờ Phòng TC-NQ-KT";
    /// "Bị từ chối tại Phòng CUVT"; "Đã duyệt đủ 8/8 bước – 16:20 07/11/2026"; "Đã hủy".
    /// </summary>
    private static string StatusText(
        string status, bool draft, IReadOnlyList<SheetApprovalStep> ordered, IReadOnlyDictionary<string, SheetApprovalStep> assigned)
    {
        if (status == "Cancelled") return "Đã hủy";
        if (draft) return "Nháp – chưa gửi duyệt";
        string TitleOf(SheetApprovalStep step) =>
            step == assigned.GetValueOrDefault(ChairmanSlot) ? "Chủ tịch HĐQT/TGĐ"
            : Slots.FirstOrDefault(s => assigned.GetValueOrDefault(s.Slot) == step).Title ?? step.RoleName ?? $"cấp {step.Level}";
        var approved = ordered.Count(s => s.Status == "Approved");
        if (ordered.FirstOrDefault(s => s.Status == "Rejected") is { } rejected) return $"Bị từ chối tại {TitleOf(rejected)} {Time(rejected.ActedAt)}";
        if (ordered.Count > 0 && approved == ordered.Count)
            return $"Đã duyệt đủ {approved}/{ordered.Count} bước – {Time(ordered.Max(s => s.ActedAt))}";
        var next = ordered.FirstOrDefault(s => s.Status == "Pending");
        return next is null
            ? $"Đang chờ duyệt – đã duyệt {approved}/{ordered.Count} bước"
            : $"Đang chờ duyệt – đã duyệt {approved}/{ordered.Count} bước, chờ {TitleOf(next)}";
    }

    /// <summary>Giờ ngày Việt Nam "HH:mm dd/MM/yyyy".</summary>
    internal static string Time(DateTimeOffset? value) =>
        value is { } v ? TimeZoneInfo.ConvertTime(v, VietnamTime.Zone).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture) : "";
}
