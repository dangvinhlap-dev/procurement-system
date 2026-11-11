using Proc.Domain.Documents;

namespace Proc.Domain.Purchasing;

/// <summary>Một cấp duyệt mẫu.</summary>
/// <param name="DocType">Loại phiếu duyệt (<see cref="ApprovalTypes"/>).</param>
/// <param name="Level">Cấp, từ 1.</param>
/// <param name="RoleCode">Mã vai trò mẫu duyệt cấp này (<see cref="Organization.RoleTemplates"/>); null khi người duyệt động.</param>
/// <param name="HeadOfficeOnly">Chỉ áp khi công ty đang làm việc là tổng công ty (phiếu chung của Ban: GTH, CKB, hồ sơ NCC chặng cuối).</param>
/// <param name="Note">Căn cứ trên sơ đồ/biểu mẫu, hiện cho người quản trị.</param>
/// <param name="Kind">Ai duyệt: theo vai trò (mặc định) hoặc Trưởng phòng ban / GĐ khối của phiếu (lát O1).</param>
public sealed record ApprovalRuleTemplate(
    string DocType, int Level, string? RoleCode, bool HeadOfficeOnly, string Note, ApproverKind Kind = ApproverKind.Role);

/// <summary>Mã các bộ cấu hình duyệt mẫu (lát O3).</summary>
public static class ApprovalRuleTemplateSets
{
    /// <summary>Theo sơ đồ phê duyệt mua sắm của Ban và Phòng cung ứng chung (lát G10, mục 18.25) – mặc định.</summary>
    public const string SoDo = "SODO";

    /// <summary>Theo chuỗi ký của biểu mẫu CUVT nhóm A, B, DV (lát O3, mục 19.3).</summary>
    public const string Cuvt = "CUVT";
}

/// <summary>
/// Cấu hình duyệt mẫu theo sơ đồ tổ chức và lưu đồ phê duyệt mua sắm của Ban và Phòng cung ứng chung (lát G10 – design.md mục 18.25). Mọi cấp
/// ngưỡng 0 (mọi phiếu qua đủ cấp) – người quản trị sửa ngưỡng theo phân quyền nội bộ của tập đoàn sau khi áp. Chỉ là gợi ý: áp cho loại phiếu
/// chưa cấu hình, không sửa cấu hình đã có (nguyên tắc "duyệt mấy cấp do người quản trị cấu hình" – mục 18.6). Biểu quyết 3 thành viên Hội
/// đồng tạm thay bằng 3 cấp tuần tự, mỗi cấp một người khác (G9 hoãn, lát R3).
/// </summary>
public static class ApprovalRuleTemplates
{
    /// <summary>
    /// Các cấp mẫu của một bộ.
    /// </summary>
    /// <param name="set">Mã bộ (<see cref="ApprovalRuleTemplateSets"/>, không phân biệt hoa thường); trống = theo sơ đồ (G10).</param>
    /// <returns>Các cấp mẫu theo loại phiếu, cấp.</returns>
    /// <exception cref="Common.BusinessRuleException">Khi mã bộ không có.</exception>
    public static IReadOnlyList<ApprovalRuleTemplate> InSet(string? set) => set?.Trim().ToUpperInvariant() switch
    {
        null or "" or ApprovalRuleTemplateSets.SoDo => Procurement,
        ApprovalRuleTemplateSets.Cuvt => CuvtForms,
        _ => throw new Common.BusinessRuleException($"Không có bộ cấu hình duyệt mẫu \"{set}\"."),
    };

    private const string PhoBan = "PHOBAN";
    private const string TruongBan = "TRUONGBANCUVT";
    private const string ChuTich = "CHUTICH";

    /// <summary>Các cấp mẫu theo loại phiếu và cấp.</summary>
    public static readonly IReadOnlyList<ApprovalRuleTemplate> Procurement =
    [
        new(DocumentTypes.PurchaseRequest, 1, "CUCTRUONGPHONG", false, "Bước 1: Phòng cung ứng tiếp nhận, kiểm tra hồ sơ, quy cách"),
        new(DocumentTypes.PurchaseOrder, 1, PhoBan, false, "Bước 4: Hội đồng (Phó ban thứ nhất) kiểm tra, xác nhận"),
        new(DocumentTypes.PurchaseOrder, 2, PhoBan, false, "Bước 4: Hội đồng (Phó ban thứ hai) kiểm tra, xác nhận"),
        new(DocumentTypes.PurchaseOrder, 3, ChuTich, false, "Bước 4: Chủ tịch/TGĐ duyệt PO theo phân quyền – sửa ngưỡng theo phân quyền nội bộ"),
        new(ApprovalTypes.PurchaseOrderOffList, 1, PhoBan, false, "Ngoại lệ mua ngoài danh sách/giá: Hội đồng xem xét"),
        new(ApprovalTypes.PurchaseOrderOffList, 2, ChuTich, false, "Ngoại lệ: Chủ tịch/TGĐ phê duyệt"),
        new(ApprovalTypes.PurchaseOrderOverCeiling, 1, ChuTich, false, "PO vượt giá trần → Chủ tịch/TGĐ phê duyệt (Q3, lát H3)"),
        new(ApprovalTypes.PurchaseOrderPriceException, 1, PhoBan, false, "Ngoại lệ lệch giá đã duyệt: Hội đồng xem xét"),
        new(ApprovalTypes.PurchaseOrderPriceException, 2, ChuTich, false, "Ngoại lệ: Chủ tịch/TGĐ phê duyệt"),
        new(ApprovalTypes.PurchaseOrderEmergency, 1, PhoBan, false, "Ngoại lệ khẩn cấp: Hội đồng xem xét"),
        new(ApprovalTypes.PurchaseOrderEmergency, 2, ChuTich, false, "Ngoại lệ: Chủ tịch/TGĐ phê duyệt"),
        new(DocumentTypes.SoleSource, 1, PhoBan, false, "Ngoại lệ độc quyền: Hội đồng xem xét"),
        new(DocumentTypes.SoleSource, 2, ChuTich, false, "Ngoại lệ: Chủ tịch/TGĐ phê duyệt"),
        new(DocumentTypes.MonthlySupplierApproval, 1, PhoBan, true, "Bước 3: Hội đồng duyệt NCC & giá tháng – Phó ban thứ nhất"),
        new(DocumentTypes.MonthlySupplierApproval, 2, PhoBan, true, "Bước 3: Phó ban thứ hai"),
        new(DocumentTypes.MonthlySupplierApproval, 3, TruongBan, true, "Bước 3: Trưởng ban (thay biểu quyết 3 thành viên – G9 hoãn)"),
        new(ApprovalTypes.MonthlyApprovalException, 1, ChuTich, true, "Bước 3: có ngoại lệ → Chủ tịch/TGĐ phê duyệt (Q6, lát H2)"),
        new(DocumentTypes.VolumeRebate, 1, PhoBan, true, "Hội đồng duyệt thỏa thuận chiết khấu – Phó ban thứ nhất"),
        new(DocumentTypes.VolumeRebate, 2, PhoBan, true, "Phó ban thứ hai"),
        new(DocumentTypes.VolumeRebate, 3, TruongBan, true, "Trưởng ban"),
        new(ApprovalTypes.Supplier, 1, TruongBan, true, "Hội đồng quyết định danh sách NCC (chặng tổng công ty)"),
        new(DocumentTypes.RequestForQuotation, 1, PhoBan, false, "Hội đồng duyệt kết quả chọn thầu – Phó ban"),
        new(DocumentTypes.RequestForQuotation, 2, TruongBan, false, "Trưởng ban"),
        new(DocumentTypes.PriceList, 1, TruongBan, false, "Hội đồng duyệt giá – Trưởng ban"),
        new(DocumentTypes.Contract, 1, PhoBan, false, "Hợp đồng khung: Hội đồng xác nhận"),
        new(DocumentTypes.Contract, 2, ChuTich, false, "Hợp đồng khung: Chủ tịch/TGĐ phê duyệt theo phân quyền"),
        new(ApprovalTypes.ContractAmendment, 1, PhoBan, false, "Phụ lục hợp đồng: Hội đồng xác nhận"),
        new(ApprovalTypes.ContractAmendment, 2, ChuTich, false, "Phụ lục hợp đồng: Chủ tịch/TGĐ phê duyệt"),
    ];

    /// <summary>
    /// Chuỗi ký của biểu mẫu CUVT (lát O3 – design.md mục 19.3, Q6, Q10, Q18), áp cho từng công ty. Ngưỡng 0 như biểu mẫu (mọi phiếu lên Chủ tịch –
    /// Q6), người quản trị chỉnh sau. Chưa có trong mẫu (thêm khi làm lát tương ứng): bước Thủ kho nhập tồn và Trưởng phòng HC kiểm định mức
    /// nhóm B của đề nghị mua (lát Y3 – đề nghị mua có loại mua); bảng tổng hợp 2A/2B (Y5); đề nghị thanh toán 5A/5B, 1C, bảng kê 2C (Y8, Y11).
    /// Đề nghị DV khác 1B ở bước Thủ kho và TP HC – nên trước Y3 dùng chung chuỗi PR dưới đây.
    /// </summary>
    public static readonly IReadOnlyList<ApprovalRuleTemplate> CuvtForms =
    [
        new(DocumentTypes.PurchaseRequest, 1, null, false, "1A/1B/DV: Trưởng phòng/ban của bộ phận đề nghị", ApproverKind.DepartmentHead),
        new(DocumentTypes.PurchaseRequest, 2, "TPCUVT", false, "1A/1B/DV: Phòng CUVT kiểm tra"),
        new(DocumentTypes.PurchaseRequest, 3, "TPTCKT", false, "1A/1B/DV: Phòng TC-NQ-KT kiểm dự toán/định mức"),
        new(DocumentTypes.PurchaseRequest, 4, null, false, "1A/1B/DV: Giám đốc khối của bộ phận đề nghị", ApproverKind.DivisionDirector),
        new(DocumentTypes.PurchaseRequest, 5, "PHOCTUBCL", false, "1A/1B/DV: Phó Chủ tịch UBCL&PT"),
        new(DocumentTypes.PurchaseRequest, 6, "CHUTICH", false, "1A/1B/DV: Chủ tịch duyệt (ngưỡng 0 như biểu mẫu – Q6)"),
        new(DocumentTypes.RequestForQuotation, 1, "TPCUVT", false, "3a/3A/3B: Trưởng phòng CUVT"),
        new(DocumentTypes.RequestForQuotation, 2, "TPTCKT", false, "3a/3A/3B: Trưởng phòng TC-NQ-KT"),
        new(DocumentTypes.RequestForQuotation, 3, "GDTCKT", false, "3a/3A/3B: Giám đốc TC-NQ-KT"),
        new(DocumentTypes.RequestForQuotation, 4, "PHOCTUBCL", false, "3a/3A/3B: Phó Chủ tịch UBCL&PT"),
        new(DocumentTypes.RequestForQuotation, 5, "CHUTICH", false, "3a/3A/3B: Chủ tịch duyệt"),
        new(DocumentTypes.PurchaseOrder, 1, "TPCUVT", false, "4A/4B (ký trước khi đặt hàng = duyệt PO – Q10): Trưởng phòng CUVT"),
        new(DocumentTypes.PurchaseOrder, 2, "TPTCKT", false, "4A/4B: Trưởng phòng TC-NQ-KT"),
        new(DocumentTypes.PurchaseOrder, 3, "GDTCKT", false, "4A/4B: Giám đốc TC-NQ-KT"),
        new(DocumentTypes.PurchaseOrder, 4, "CHUTICH", false, "4A/4B: Chủ tịch duyệt"),
    ];
}
