using Proc.Domain.Documents;

namespace Proc.Domain.Purchasing;

/// <summary>Một cấp duyệt mẫu.</summary>
/// <param name="DocType">Loại phiếu duyệt (<see cref="ApprovalTypes"/>).</param>
/// <param name="Level">Cấp, từ 1.</param>
/// <param name="RoleCode">Mã vai trò mẫu duyệt cấp này (<see cref="Organization.RoleTemplates"/>).</param>
/// <param name="HeadOfficeOnly">Chỉ áp khi công ty đang làm việc là tổng công ty (phiếu chung của Ban: GTH, CKB, hồ sơ NCC chặng cuối).</param>
/// <param name="Note">Căn cứ trên sơ đồ, hiện cho người quản trị.</param>
public sealed record ApprovalRuleTemplate(string DocType, int Level, string RoleCode, bool HeadOfficeOnly, string Note);

/// <summary>
/// Cấu hình duyệt mẫu theo sơ đồ tổ chức & lưu đồ phê duyệt mua sắm của Ban & Phòng cung ứng chung (lát G10 – design.md mục 18.25). Mọi cấp
/// ngưỡng 0 (mọi phiếu qua đủ cấp) – người quản trị sửa ngưỡng theo phân quyền nội bộ của tập đoàn sau khi áp. Chỉ là gợi ý: áp cho loại phiếu
/// chưa cấu hình, không sửa cấu hình đã có (nguyên tắc "duyệt mấy cấp do người quản trị cấu hình" – mục 18.6). Biểu quyết 3 thành viên Hội
/// đồng tạm thay bằng 3 cấp tuần tự, mỗi cấp một người khác (G9 hoãn, lát R3).
/// </summary>
public static class ApprovalRuleTemplates
{
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
        new(ApprovalTypes.PurchaseOrderPriceException, 1, PhoBan, false, "Ngoại lệ lệch giá đã duyệt: Hội đồng xem xét"),
        new(ApprovalTypes.PurchaseOrderPriceException, 2, ChuTich, false, "Ngoại lệ: Chủ tịch/TGĐ phê duyệt"),
        new(ApprovalTypes.PurchaseOrderEmergency, 1, PhoBan, false, "Ngoại lệ khẩn cấp: Hội đồng xem xét"),
        new(ApprovalTypes.PurchaseOrderEmergency, 2, ChuTich, false, "Ngoại lệ: Chủ tịch/TGĐ phê duyệt"),
        new(DocumentTypes.SoleSource, 1, PhoBan, false, "Ngoại lệ độc quyền: Hội đồng xem xét"),
        new(DocumentTypes.SoleSource, 2, ChuTich, false, "Ngoại lệ: Chủ tịch/TGĐ phê duyệt"),
        new(DocumentTypes.MonthlySupplierApproval, 1, PhoBan, true, "Bước 3: Hội đồng duyệt NCC & giá tháng – Phó ban thứ nhất"),
        new(DocumentTypes.MonthlySupplierApproval, 2, PhoBan, true, "Bước 3: Phó ban thứ hai"),
        new(DocumentTypes.MonthlySupplierApproval, 3, TruongBan, true, "Bước 3: Trưởng ban (thay biểu quyết 3 thành viên – G9 hoãn)"),
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
}
