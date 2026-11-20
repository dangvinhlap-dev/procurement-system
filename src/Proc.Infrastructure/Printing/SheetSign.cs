using System.Globalization;
using Proc.Application.Common;
using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Printing;

/// <summary>Một ô ký cố định của khối ký trong sheet biểu mẫu.</summary>
/// <param name="Key">
/// Khóa ghép bước duyệt: "Storekeeper", "DepartmentHead", "DivisionDirector" (người duyệt động) hoặc mã vai trò mẫu (TPCUVT, TPTCKT, GDTCKT, TPHC,
/// PHOCTUBCL); ô đầu (người lập / đề xuất) và ô Chủ tịch không cần khóa.
/// </param>
/// <param name="Title">Chức danh nguyên văn ảnh khối ký trong sheet.</param>
/// <param name="Caption">Dòng nghiêng nguyên văn ("(Ký đề nghị)"); rỗng = sheet không có dòng này.</param>
public sealed record SheetSignSlot(string Key, string Title, string Caption);

/// <summary>Khuôn khối ký của một sheet biểu mẫu (lát IN1 – 1A/1B, lát IN2 – 2A/2B, 3a/3A/3B, 4A/4B, 5A/5B – design.md mục 20.4).</summary>
/// <param name="Heading">Dòng đầu khối ký nguyên văn ("Kính trình Chủ Tịch HĐQT/TGĐ xem xét và ký duyệt").</param>
/// <param name="First">Ô người lập / người đề xuất (ô đầu).</param>
/// <param name="Slots">Ô duyệt theo thứ tự sheet, trừ ô đầu và ô Chủ tịch.</param>
/// <param name="Chairman">Ô Chủ tịch HĐQT/TGĐ (cấp cao nhất của chuỗi thường).</param>
/// <param name="ChairmanName">Tên ô Chủ tịch trong dòng tình trạng phê duyệt ("Chủ tịch HĐQT/TGĐ").</param>
public sealed record SheetSignSpec(
    string Heading, SheetSignSlot First, IReadOnlyList<SheetSignSlot> Slots, SheetSignSlot Chairman, string ChairmanName = "Chủ tịch HĐQT/TGĐ");

/// <summary>
/// Ghép bước duyệt vào ô ký của khối ký theo sheet (tách từ lát IN1 để mọi biểu mẫu dùng chung – lát IN2). Bước duyệt vào ô theo loại người duyệt /
/// mã vai trò mẫu (<see cref="SheetSignSlot.Key"/>); cấp cao nhất của chuỗi thường không khớp mã nào thì vào ô Chủ tịch; bước còn lại (chặng vượt,
/// vai trò tự tạo, hai cấp trùng ô) in hàng ô phát sinh. Ô đã duyệt: ảnh chữ ký → họ tên → giờ; từ chối: họ tên + giờ, không chữ ký.
/// </summary>
public static class SheetSign
{
    /// <summary>Dòng đầu khối ký chung của mọi sheet CUVT – nguyên văn.</summary>
    public const string Heading = "Kính trình Chủ Tịch HĐQT/TGĐ xem xét và ký duyệt";

    private const string NotInChain = "Không thuộc chuỗi duyệt";
    private const string ChairmanKey = "CHUTICH";
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Dựng khối ký.</summary>
    /// <param name="spec">Khuôn khối ký của sheet.</param>
    /// <param name="status">Trạng thái phiếu (Draft, Submitted, Approved, Rejected, Cancelled…).</param>
    /// <param name="firstName">Họ tên người lập / đề xuất (đã gồm danh xưng).</param>
    /// <param name="submittedAt">Lúc gửi duyệt của lượt hiện tại; null = chưa gửi.</param>
    /// <param name="firstSignature">Chữ ký người lập: có hiệu lực lúc gửi (chỉ khi chính người đó gửi); phiếu nháp = chữ ký đang dùng.</param>
    /// <param name="submittedByOther">Người gửi duyệt khi khác người lập (in "Gửi duyệt bởi …").</param>
    /// <param name="steps">Bước duyệt của lượt hiện tại (đã bỏ bước "Skipped").</param>
    /// <returns>Khối ký: <see cref="SheetSignBlock.Boxes"/> = ô đầu + ô cố định theo thứ tự sheet, <see cref="SheetSignBlock.Final"/> = ô Chủ tịch.</returns>
    public static SheetSignBlock Build(
        SheetSignSpec spec, string status, string? firstName, DateTimeOffset? submittedAt, SheetImage? firstSignature, string? submittedByOther,
        IReadOnlyList<SheetApprovalStep> steps)
    {
        var ordered = steps.OrderBy(s => s.Stage).ThenBy(s => s.Level).ToList();
        // Cấp cao nhất của chuỗi thường: chặng 9 nếu đã dời ra sau (có chặng vượt), không thì cấp lớn nhất của chặng 1.
        var top = ordered.Where(s => s.Stage == ApprovalTypes.TopLevelStage).LastOrDefault()
                  ?? ordered.Where(s => s.Stage == 1).LastOrDefault();
        var keys = spec.Slots.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        var assigned = new Dictionary<string, SheetApprovalStep>();
        var extra = new List<SheetApprovalStep>();
        foreach (var step in ordered)
        {
            var slot = SlotOf(step);
            if (slot is not null && slot != ChairmanKey && !keys.Contains(slot)) slot = null;
            if (slot is null && step == top && !assigned.ContainsKey(ChairmanKey)) slot = ChairmanKey;
            if (slot is not null && step.Stage is 1 or ApprovalTypes.TopLevelStage && assigned.TryAdd(slot, step)) continue;
            extra.Add(step);
        }

        var draft = status == "Draft" || submittedAt is null;
        var firstDetail = draft ? "Chưa gửi duyệt"
            : submittedByOther is not null ? $"Gửi duyệt bởi {submittedByOther} {Time(submittedAt)}"
            : $"Gửi duyệt {Time(submittedAt)}";
        var boxes = new List<SheetSignBox>
        {
            // Phiếu nháp vẫn in chữ ký người lập (chữ ký đang dùng – người dùng chốt 19/11/2026, design.md 20.4 IN1-5); nơi gọi quyết định có chữ ký hay không.
            new(spec.First.Title, spec.First.Caption, firstName, firstDetail, firstSignature),
        };
        boxes.AddRange(spec.Slots.Select(s => assigned.TryGetValue(s.Key, out var step)
            ? Box(s.Title, s.Caption, step, draft)
            : Missing(s.Title, s.Caption, ordered.Count == 0)));
        // Tên người duyệt ô Chủ tịch in hoa như mọi ảnh khối ký trong sheet ("AHLĐ. ĐINH VĂN VUI").
        var final = assigned.TryGetValue(ChairmanKey, out var chairman)
            ? Box(spec.Chairman.Title, spec.Chairman.Caption, chairman, draft, upper: true)
            : Missing(spec.Chairman.Title, spec.Chairman.Caption, ordered.Count == 0);
        var extraBoxes = extra.Select(s => Box(s.RoleName ?? $"Cấp {s.Level}", ExtraCaption(s.Stage), s, draft)).ToList();
        string TitleOf(SheetApprovalStep step) =>
            step == assigned.GetValueOrDefault(ChairmanKey) ? spec.ChairmanName
            : spec.Slots.FirstOrDefault(s => assigned.GetValueOrDefault(s.Key) == step)?.Title ?? step.RoleName ?? $"cấp {step.Level}";
        return new SheetSignBlock(spec.Heading, boxes, extraBoxes, final, StatusText(status, draft, ordered, TitleOf));
    }

    /// <summary>Ô của một bước: đã duyệt → chữ ký + họ tên + giờ; từ chối → họ tên + giờ (không chữ ký); chưa duyệt → trống.</summary>
    /// <summary>
    /// Chỉ giữ các bước có ô trên khối ký <paramref name="spec"/> – dùng khi in trang 2B kèm đề nghị 1B (người dùng yêu cầu 20/11/2026): chuỗi 1B
    /// đã gồm TP CUVT, GĐ khối, Phó CT UBCL&amp;PT, Chủ tịch (ô của 2B); Thủ kho, Trưởng phòng ban, TC-NQ-KT, HC đã ký ở trang 1B nên không in
    /// thành ô phát sinh trên 2B. Bước chặng vượt (ngân sách, dự toán, ngoài danh sách) và cấp cao nhất vẫn giữ.
    /// </summary>
    /// <param name="spec">Khối ký của sheet đích.</param>
    /// <param name="steps">Bước duyệt của phiếu.</param>
    /// <returns>Bước được in trên khối ký đó.</returns>
    public static IReadOnlyList<SheetApprovalStep> OnlySlotsOf(SheetSignSpec spec, IReadOnlyList<SheetApprovalStep> steps)
    {
        var ordered = steps.OrderBy(s => s.Stage).ThenBy(s => s.Level).ToList();
        var top = ordered.Where(s => s.Stage == ApprovalTypes.TopLevelStage).LastOrDefault()
                  ?? ordered.Where(s => s.Stage == 1).LastOrDefault();
        var keys = spec.Slots.Select(s => s.Key).Append(ChairmanKey).ToHashSet(StringComparer.Ordinal);
        return ordered.Where(s => s.Stage is not (1 or ApprovalTypes.TopLevelStage) || s == top || SlotOf(s) is { } k && keys.Contains(k)).ToList();
    }

    private static SheetSignBox Box(string title, string caption, SheetApprovalStep step, bool draft, bool upper = false) => step.Status switch
    {
        _ when draft => new SheetSignBox(title, caption, null, null, null),
        "Approved" => new SheetSignBox(
            title, caption, upper ? step.ActorName?.ToUpper(Vi) : step.ActorName,
            step.Signature is null ? $"Đã duyệt điện tử {Time(step.ActedAt)}" : $"Đã duyệt {Time(step.ActedAt)}", step.Signature),
        "Rejected" => new SheetSignBox(title, caption, upper ? step.ActorName?.ToUpper(Vi) : step.ActorName, $"Từ chối {Time(step.ActedAt)}", null),
        _ => new SheetSignBox(title, caption, null, "Chưa duyệt", null),
    };

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
            "TPCUVT" or "TPTCKT" or "GDTCKT" or "TPHC" or "PHOCTUBCL" => step.RoleCode.ToUpperInvariant(),
            "CHUTICH" => ChairmanKey,
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
    /// Tình trạng phê duyệt cho khung thông tin hệ thống (góc dưới trái trang cuối): "Nháp – chưa gửi duyệt"; "Đang chờ duyệt – đã duyệt 3/8 bước, chờ Phòng TC-NQ-KT";
    /// "Bị từ chối tại Phòng CUVT"; "Đã duyệt đủ 8/8 bước – 16:20 07/11/2026"; "Đã hủy".
    /// </summary>
    private static string StatusText(string status, bool draft, IReadOnlyList<SheetApprovalStep> ordered, Func<SheetApprovalStep, string> titleOf)
    {
        if (status == "Cancelled") return "Đã hủy";
        if (draft) return "Nháp – chưa gửi duyệt";
        var approved = ordered.Count(s => s.Status == "Approved");
        if (ordered.FirstOrDefault(s => s.Status == "Rejected") is { } rejected) return $"Bị từ chối tại {titleOf(rejected)} {Time(rejected.ActedAt)}";
        if (ordered.Count > 0 && approved == ordered.Count)
            return $"Đã duyệt đủ {approved}/{ordered.Count} bước – {Time(ordered.Max(s => s.ActedAt))}";
        var next = ordered.FirstOrDefault(s => s.Status == "Pending");
        return next is null
            ? $"Đang chờ duyệt – đã duyệt {approved}/{ordered.Count} bước"
            : $"Đang chờ duyệt – đã duyệt {approved}/{ordered.Count} bước, chờ {titleOf(next)}";
    }

    /// <summary>Giờ ngày Việt Nam "HH:mm dd/MM/yyyy".</summary>
    internal static string Time(DateTimeOffset? value) =>
        value is { } v ? TimeZoneInfo.ConvertTime(v, VietnamTime.Zone).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture) : "";
}
