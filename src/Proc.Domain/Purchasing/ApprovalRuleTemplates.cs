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
    /// Q6), người quản trị chỉnh sau. Đề nghị mua theo loại mua (lát Y3c): 1B (PR) 8 cấp có Thủ kho nơi nhận và Trưởng phòng HC kiểm định mức nhóm
    /// B; 1A (PRA) bỏ TP HC; DV (PRS) bỏ Thủ kho; vượt dự toán công trình (PRE) thêm Giám đốc TC-NQ-KT (tự đặt – chỉnh ở APRL); bảng tổng hợp
    /// 2A/2B (BTH – lát Y5b) 4 cấp; đề nghị thanh toán 5A/5B (DNT – lát Y8) 4 cấp, 1C dùng chung bộ DNT (lát Y11b); bảng kê tạp vụ 2C (BKT – lát Y11a) 4 cấp, vượt định
    /// mức (TVB) thêm Giám đốc TC-NQ-KT (tự đặt – chỉnh ở APRL).
    /// </summary>
    public static readonly IReadOnlyList<ApprovalRuleTemplate> CuvtForms =
    [
        new(DocumentTypes.PurchaseRequest, 1, null, false, "1B: Thủ kho nơi nhận nhập tồn, xác nhận", ApproverKind.Storekeeper),
        new(DocumentTypes.PurchaseRequest, 2, null, false, "1B: Trưởng phòng/ban của bộ phận đề nghị", ApproverKind.DepartmentHead),
        new(DocumentTypes.PurchaseRequest, 3, "TPCUVT", false, "1B: Phòng CUVT kiểm tra"),
        new(DocumentTypes.PurchaseRequest, 4, "TPTCKT", false, "1B: Phòng TC-NQ-KT kiểm định mức"),
        new(DocumentTypes.PurchaseRequest, 5, "TPHC", false, "1B: Trưởng phòng HC kiểm định mức (chỉ nhóm B – Q6)"),
        new(DocumentTypes.PurchaseRequest, 6, null, false, "1B: Giám đốc khối của bộ phận đề nghị", ApproverKind.DivisionDirector),
        new(DocumentTypes.PurchaseRequest, 7, "PHOCTUBCL", false, "1B: Phó Chủ tịch UBCL&PT"),
        new(DocumentTypes.PurchaseRequest, 8, "CHUTICH", false, "1B: Chủ tịch duyệt (ngưỡng 0 như biểu mẫu – Q6)"),
        new(ApprovalTypes.PurchaseRequestConstruction, 1, null, false, "1A: Thủ kho nơi nhận nhập tồn, xác nhận", ApproverKind.Storekeeper),
        new(ApprovalTypes.PurchaseRequestConstruction, 2, null, false, "1A: Trưởng phòng/ban của bộ phận đề nghị", ApproverKind.DepartmentHead),
        new(ApprovalTypes.PurchaseRequestConstruction, 3, "TPCUVT", false, "1A: Phòng CUVT kiểm tra"),
        new(ApprovalTypes.PurchaseRequestConstruction, 4, "TPTCKT", false, "1A: Phòng TC-NQ-KT kiểm dự toán"),
        new(ApprovalTypes.PurchaseRequestConstruction, 5, null, false, "1A: Giám đốc khối của bộ phận đề nghị", ApproverKind.DivisionDirector),
        new(ApprovalTypes.PurchaseRequestConstruction, 6, "PHOCTUBCL", false, "1A: Phó Chủ tịch UBCL&PT"),
        new(ApprovalTypes.PurchaseRequestConstruction, 7, "CHUTICH", false, "1A: Chủ tịch duyệt (ngưỡng 0 như biểu mẫu – Q6)"),
        new(ApprovalTypes.PurchaseRequestService, 1, null, false, "DV: Trưởng phòng/ban của bộ phận đề nghị", ApproverKind.DepartmentHead),
        new(ApprovalTypes.PurchaseRequestService, 2, "TPCUVT", false, "DV: Phòng CUVT kiểm tra"),
        new(ApprovalTypes.PurchaseRequestService, 3, "TPTCKT", false, "DV: Phòng TC-NQ-KT chỉ kiểm dự toán (Q6)"),
        new(ApprovalTypes.PurchaseRequestService, 4, "TPHC", false, "DV: Trưởng phòng HC kiểm định mức"),
        new(ApprovalTypes.PurchaseRequestService, 5, null, false, "DV: Giám đốc khối của bộ phận đề nghị", ApproverKind.DivisionDirector),
        new(ApprovalTypes.PurchaseRequestService, 6, "PHOCTUBCL", false, "DV: Phó Chủ tịch UBCL&PT"),
        new(ApprovalTypes.PurchaseRequestService, 7, "CHUTICH", false, "DV: Chủ tịch duyệt (ngưỡng 0 như biểu mẫu – Q6)"),
        new(ApprovalTypes.PurchaseRequestOverEstimate, 1, "GDTCKT", false, "1A vượt dự toán công trình (Q5): Giám đốc TC-NQ-KT xem xét (tự đặt)"),
        new(DocumentTypes.PurchaseSummary, 1, "TPCUVT", false, "2A/2B: Trưởng phòng CUVT"),
        new(DocumentTypes.PurchaseSummary, 2, null, false, "2A/2B: Giám đốc khối của bộ phận đề xuất", ApproverKind.DivisionDirector),
        new(DocumentTypes.PurchaseSummary, 3, "PHOCTUBCL", false, "2A/2B: Phó Chủ tịch UBCL&PT"),
        new(DocumentTypes.PurchaseSummary, 4, "CHUTICH", false, "2A/2B: Chủ tịch duyệt (ngưỡng 0 như biểu mẫu – Q6)"),
        new(DocumentTypes.RequestForQuotation, 1, "TPCUVT", false, "3a/3A/3B: Trưởng phòng CUVT"),
        new(DocumentTypes.RequestForQuotation, 2, "TPTCKT", false, "3a/3A/3B: Trưởng phòng TC-NQ-KT"),
        new(DocumentTypes.RequestForQuotation, 3, "GDTCKT", false, "3a/3A/3B: Giám đốc TC-NQ-KT"),
        new(DocumentTypes.RequestForQuotation, 4, "PHOCTUBCL", false, "3a/3A/3B: Phó Chủ tịch UBCL&PT"),
        new(DocumentTypes.RequestForQuotation, 5, "CHUTICH", false, "3a/3A/3B: Chủ tịch duyệt"),
        new(DocumentTypes.PurchaseOrder, 1, "TPCUVT", false, "4A/4B (ký trước khi đặt hàng = duyệt PO – Q10): Trưởng phòng CUVT"),
        new(DocumentTypes.PurchaseOrder, 2, "TPTCKT", false, "4A/4B: Trưởng phòng TC-NQ-KT"),
        new(DocumentTypes.PurchaseOrder, 3, "GDTCKT", false, "4A/4B: Giám đốc TC-NQ-KT"),
        new(DocumentTypes.PurchaseOrder, 4, "CHUTICH", false, "4A/4B: Chủ tịch duyệt"),
        new(DocumentTypes.PaymentRequest, 1, null, false, "5A/5B: Giám đốc khối đề nghị (khối của bộ phận đặt hàng)", ApproverKind.DivisionDirector),
        new(DocumentTypes.PaymentRequest, 2, "TPTCKT", false, "5A/5B: Trưởng phòng TC-NQ-KT"),
        new(DocumentTypes.PaymentRequest, 3, "GDTCKT", false, "5A/5B: Giám đốc TC-NQ-KT"),
        new(DocumentTypes.PaymentRequest, 4, "CHUTICH", false, "5A/5B: Chủ tịch duyệt"),
        new(DocumentTypes.SundryExpenseSheet, 1, null, false, "2C: Trưởng phòng/ban của bộ phận", ApproverKind.DepartmentHead),
        new(DocumentTypes.SundryExpenseSheet, 2, null, false, "2C: Giám đốc khối của bộ phận", ApproverKind.DivisionDirector),
        new(DocumentTypes.SundryExpenseSheet, 3, "TPTCKT", false, "2C: Phòng TC-NQ-KT kiểm định mức"),
        new(DocumentTypes.SundryExpenseSheet, 4, "CHUTICH", false, "2C: Chủ tịch duyệt (ngưỡng 0 như biểu mẫu – Q6)"),
        new(ApprovalTypes.SundryOverBudget, 1, "GDTCKT", false, "2C vượt định mức tháng (Q5): Giám đốc TC-NQ-KT xem xét (tự đặt)"),
    ];
}
