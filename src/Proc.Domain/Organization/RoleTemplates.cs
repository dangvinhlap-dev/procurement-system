namespace Proc.Domain.Organization;

/// <summary>Một vai trò mẫu.</summary>
/// <param name="Code">Mã vai trò.</param>
/// <param name="Name">Tên vai trò.</param>
/// <param name="Description">Việc của vai trò, hiện trên màn hình để người quản trị chọn đúng.</param>
/// <param name="Permissions">Mã quyền.</param>
public sealed record RoleTemplate(string Code, string Name, string Description, IReadOnlyList<string> Permissions);

/// <summary>
/// Bộ vai trò mẫu theo cơ cấu Ban Cung ứng Vật tư tập đoàn (design.md mục 18.6; đề xuất <c>docs/Bancungung.pdf</c>, phân tích G2, G3).
/// Chỉ là dữ liệu khởi tạo: tạo vai trò còn thiếu, không sửa vai trò đã có (người quản trị có thể đã chỉnh quyền), không đổi cơ chế
/// phân quyền. Nguyên tắc không kiêm nhiệm: người đàm phán/chọn NCC (DAMPHAN) không có quyền duyệt giá, duyệt thầu, duyệt hồ sơ NCC –
/// các quyền duyệt đó thuộc Hội đồng (HOIDONG) và Trưởng ban; KSNB chỉ xem.
/// </summary>
public static class RoleTemplates
{
    /// <summary>Quyền xem danh mục dùng chung mà hầu hết vai trò cần.</summary>
    private static readonly string[] CatalogView =
    [
        Permissions.Organization.DepartmentView, Permissions.Organization.WarehouseView,
        Permissions.MasterData.ItemCategoryView, Permissions.MasterData.ItemView,
        Permissions.MasterData.SupplierView,
    ];

    /// <summary>Quyền xem nghiệp vụ mua – giá – thầu – hợp đồng.</summary>
    private static readonly string[] ProcurementView =
    [
        Permissions.Purchase.PrView, Permissions.Purchase.PoView, Permissions.Purchase.ContractView,
        Permissions.Pricing.PriceListView, Permissions.Pricing.CompareView, Permissions.Sourcing.RfqView,
        Permissions.MasterData.SoleSourceView, Permissions.MasterData.SupplierReviewView, Permissions.Pricing.MonthlyApprovalView,
        Permissions.Pricing.PriceBankView, Permissions.Purchase.DemandPlanView, Permissions.Pricing.VolumeRebateView,
        Permissions.Purchase.PooledOrderView,
    ];

    /// <summary>Các vai trò mẫu Ban CUVT, theo thứ tự hiển thị.</summary>
    public static readonly IReadOnlyList<RoleTemplate> Procurement =
    [
        new("TRUONGBANCUVT", "Trưởng ban Cung ứng", "Điều hành Ban; duyệt PR, PO, bảng giá, kết quả thầu, hồ sơ NCC, hợp đồng; xem báo cáo toàn tập đoàn",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Purchase.PrApprove, Permissions.Purchase.PoApprove,
            Permissions.Purchase.ContractApprove, Permissions.Pricing.PriceListApprove,
            Permissions.Sourcing.RfqApproveAward, Permissions.MasterData.SupplierApprove,
            Permissions.Inventory.StockView, Permissions.Report.StockView, Permissions.Report.PurchaseView,
            Permissions.Report.PriceViewAllCompanies, Permissions.Report.StockViewAllCompanies,
            Permissions.Report.PurchaseViewAllCompanies, Permissions.Analysis.PurchaseHistoryView,
            Permissions.Analysis.PurchaseHistoryViewAllCompanies,
            Permissions.MasterData.ConflictView, Permissions.MasterData.ConflictManage, Permissions.MasterData.SoleSourceApprove,
            Permissions.MasterData.SupplierReviewManage, Permissions.Pricing.MonthlyApprovalApprove,
            Permissions.Purchase.DemandPlanViewAllCompanies, Permissions.Pricing.VolumeRebateApprove,
        ]),
        new("DAMPHAN", "Nhóm Đàm phán & quản trị giá", "Quản lý hồ sơ NCC, lập bảng giá, RFQ, đề xuất chọn thầu, soạn hợp đồng, phân tích dữ liệu mua – không tự duyệt",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.MasterData.SupplierManage, Permissions.MasterData.ItemManage,
            Permissions.Pricing.PriceListManage, Permissions.Sourcing.RfqManage, Permissions.Sourcing.RfqAward,
            Permissions.Purchase.ContractManage, Permissions.Report.PurchaseView,
            Permissions.Report.PriceViewAllCompanies, Permissions.Analysis.PurchaseHistoryView,
            Permissions.Analysis.PurchaseHistoryImport, Permissions.Analysis.PurchaseHistoryViewAllCompanies,
            Permissions.MasterData.SoleSourceManage, Permissions.MasterData.SupplierReviewManage,
            Permissions.Pricing.MonthlyApprovalManage, Permissions.Pricing.PriceBankManage, Permissions.Purchase.DemandPlanManage,
            Permissions.Purchase.DemandPlanViewAllCompanies, Permissions.Pricing.VolumeRebateManage,
        ]),
        new("VANHANH", "Nhóm Vận hành mua hàng", "Lập PO theo giá đã duyệt cho từng pháp nhân, theo dõi giao hàng",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Purchase.PoCreate, Permissions.Inventory.DocumentView, Permissions.Inventory.StockView,
            Permissions.Report.PurchaseView, Permissions.MasterData.SupplierReviewManage, Permissions.Purchase.PooledOrderManage,
        ]),
        new("HOIDONG", "Hội đồng xét chọn & duyệt giá", "Duyệt kết quả chọn thầu, bảng giá, hồ sơ NCC, hợp đồng nguyên tắc",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Sourcing.RfqApproveAward, Permissions.Pricing.PriceListApprove,
            Permissions.MasterData.SupplierApprove, Permissions.Purchase.ContractApprove,
            Permissions.Report.PriceViewAllCompanies, Permissions.MasterData.SoleSourceApprove,
            Permissions.Pricing.MonthlyApprovalApprove, Permissions.Pricing.VolumeRebateApprove,
        ]),
        new("DENGHI", "Người đề nghị mua (bộ phận)", "Lập đề nghị mua hàng cho bộ phận",
        [
            .. CatalogView, Permissions.Purchase.PrView, Permissions.Purchase.PrCreate,
            Permissions.Inventory.StockView,
        ]),
        new("KHO", "Kho", "Nhận hàng, ghi sổ phiếu kho, xem tồn",
        [
            .. CatalogView, Permissions.Purchase.PoView, Permissions.Inventory.DocumentView,
            Permissions.Inventory.DocumentCreate, Permissions.Inventory.DocumentPost, Permissions.Inventory.AdjustmentCreate,
            Permissions.Inventory.StockView, Permissions.Inventory.PeriodView, Permissions.Report.StockView,
        ]),
        new("KETOAN", "Kế toán", "Đối chiếu PO – phiếu nhận, đóng kỳ kho, xuất chứng từ cho phần mềm kế toán",
        [
            .. CatalogView, Permissions.Purchase.PoView, Permissions.Purchase.ContractView,
            Permissions.Pricing.PriceListView, Permissions.Inventory.DocumentView, Permissions.Inventory.StockView,
            Permissions.Inventory.PeriodView, Permissions.Inventory.PeriodClose,
            // Kho lập phiếu điều chỉnh/kiểm kê, kế toán ghi sổ – tách người kiểm kê và người ghi tăng/giảm tồn (design.md mục 6.1).
            Permissions.Inventory.AdjustmentPost,
            Permissions.Report.StockView, Permissions.Report.PurchaseView, Permissions.Report.AccountingExport,
        ]),
        new("PHAPCHE", "Pháp chế", "Xem hồ sơ NCC, hồ sơ thầu, hợp đồng để rà soát pháp lý",
        [
            .. CatalogView, Permissions.Purchase.ContractView, Permissions.Pricing.PriceListView,
            Permissions.Sourcing.RfqView, Permissions.Sourcing.AppraisalGive,
        ]),
        new("KSNB", "Kiểm soát nội bộ", "Giám sát độc lập: chỉ xem mọi nghiệp vụ, báo cáo toàn tập đoàn và nhật ký thao tác; rà dấu hiệu bất thường, ghi nhận vi phạm",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Inventory.DocumentView, Permissions.Inventory.StockView, Permissions.Inventory.PeriodView,
            Permissions.Report.StockView, Permissions.Report.PurchaseView,
            Permissions.Report.PriceViewAllCompanies, Permissions.Report.StockViewAllCompanies,
            Permissions.Report.PurchaseViewAllCompanies, Permissions.Analysis.PurchaseHistoryView,
            Permissions.Analysis.PurchaseHistoryViewAllCompanies, Permissions.System.LogView, Permissions.MasterData.ConflictView,
            Permissions.Control.AnomalyView, Permissions.Control.FindingManage, Permissions.Purchase.DemandPlanViewAllCompanies,
        ]),
        // Kết quả KSNB gửi thẳng lãnh đạo, không qua Nhóm Đàm phán/Vận hành – nên không gán quyền nhận cho Trưởng ban (design.md mục 18.19).
        new("LANHDAO", "Lãnh đạo (nhận báo cáo KSNB)", "Ban giám đốc/HĐQT: nhận thông báo vi phạm của KSNB, xem sổ vi phạm, báo cáo toàn tập đoàn",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Report.StockView, Permissions.Report.PurchaseView, Permissions.Report.PurchaseViewAllCompanies,
            Permissions.Control.FindingReceive,
        ]),

        // ---- Theo sơ đồ tổ chức & lưu đồ phê duyệt mua sắm của Ban & Phòng cung ứng chung (lát G10 – design.md mục 18.25). Trưởng ban
        // dùng TRUONGBANCUVT ở trên. Nguyên tắc: Phòng cung ứng chung **tìm kiếm, đánh giá NCC** (người dùng chốt 08/11/2026 – Q2, khớp bước 2
        // của sơ đồ) và đặt hàng đúng NCC/giá đã duyệt, không lập/duyệt giá; Văn phòng Ban phân tích giá, đề xuất danh sách NCC và giá (không
        // duyệt, không đặt hàng, không lập hồ sơ NCC); thành viên chuyên môn ghi ý kiến thẩm định (lát H1), không có quyền duyệt (không biểu quyết).
        new("CHUTICH", "Chủ tịch HĐQT / Tổng Giám đốc", "Phê duyệt ngoại lệ (ngoài danh sách/giá, lệch giá, khẩn cấp, độc quyền, phiếu duyệt NCC & giá tháng có ngoại lệ), duyệt PO, hợp đồng theo phân quyền nội bộ; nhận báo cáo KSNB",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Purchase.PrApprove, Permissions.Purchase.PoApprove, Permissions.Purchase.ContractApprove,
            Permissions.MasterData.SoleSourceApprove,
            // Phê duyệt ngoại lệ ở bước Hội đồng duyệt NCC & giá tháng (Q6 – chặng GTX, lát H2).
            Permissions.Pricing.MonthlyApprovalApprove,
            Permissions.Inventory.StockView, Permissions.Report.StockView, Permissions.Report.PurchaseView,
            Permissions.Report.PriceViewAllCompanies, Permissions.Report.StockViewAllCompanies, Permissions.Report.PurchaseViewAllCompanies,
            Permissions.Purchase.DemandPlanViewAllCompanies, Permissions.Control.FindingReceive,
        ]),
        new("PHOBAN", "Phó ban cung ứng (Hội đồng)", "Thành viên Hội đồng đàm phán, duyệt giá: duyệt danh sách NCC, giá tháng, kết quả thầu, hợp đồng; kiểm tra, xác nhận PO; xem xét ngoại lệ",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Purchase.PoApprove, Permissions.Purchase.ContractApprove, Permissions.Pricing.PriceListApprove,
            Permissions.Sourcing.RfqApproveAward, Permissions.MasterData.SupplierApprove, Permissions.MasterData.SoleSourceApprove,
            Permissions.Pricing.MonthlyApprovalApprove, Permissions.Pricing.VolumeRebateApprove,
            Permissions.Report.PurchaseView, Permissions.Report.PriceViewAllCompanies, Permissions.Report.PurchaseViewAllCompanies,
            Permissions.Purchase.DemandPlanViewAllCompanies, Permissions.MasterData.ConflictView,
        ]),
        new("THAMDINHCL", "Thẩm định chất lượng (thành viên chuyên môn)", "Thẩm định chất lượng hàng, NCC trước khi Hội đồng quyết; đánh giá NCC – không biểu quyết, không duyệt",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.MasterData.SupplierReviewManage, Permissions.Inventory.DocumentView, Permissions.Report.PurchaseView,
            Permissions.Sourcing.AppraisalGive,
        ]),
        new("THAMDINHPL", "Thẩm định pháp lý (thành viên chuyên môn)", "Thẩm định điều khoản pháp lý của hồ sơ NCC, hồ sơ thầu, hợp đồng – không biểu quyết, không duyệt",
        [
            .. CatalogView, .. ProcurementView, Permissions.MasterData.ConflictView, Permissions.Sourcing.AppraisalGive,
        ]),
        new("VPBDIEUPHOI", "Điều phối Văn phòng Ban", "Trưởng phòng/bộ phận điều phối Văn phòng Ban: tổng hợp nhu cầu, phân tích giá, đề xuất danh sách NCC và giá tháng, ngân hàng giá, thỏa thuận chiết khấu – không duyệt",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.MasterData.ItemManage, Permissions.Pricing.PriceListManage, Permissions.Sourcing.RfqAward,
            Permissions.Purchase.ContractManage, Permissions.MasterData.SoleSourceManage, Permissions.MasterData.SupplierReviewManage,
            Permissions.Pricing.MonthlyApprovalManage, Permissions.Pricing.PriceBankManage, Permissions.Pricing.VolumeRebateManage,
            Permissions.Purchase.DemandPlanViewAllCompanies,
            Permissions.Report.PurchaseView, Permissions.Report.PriceViewAllCompanies, Permissions.Report.PurchaseViewAllCompanies,
            Permissions.Analysis.PurchaseHistoryView, Permissions.Analysis.PurchaseHistoryImport, Permissions.Analysis.PurchaseHistoryViewAllCompanies,
        ]),
        new("VPBPHANTICH", "Nhân viên phân tích giá, đề xuất NCC (Văn phòng Ban)", "Phân tích, so sánh giá, khảo sát giá thị trường, đề xuất danh sách NCC và giá mục tiêu, đề xuất chọn thầu – không lập hồ sơ NCC, không chọn NCC, không đặt hàng",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Sourcing.RfqAward, Permissions.MasterData.SoleSourceManage, Permissions.MasterData.SupplierReviewManage,
            Permissions.Pricing.PriceBankManage, Permissions.Pricing.MonthlyApprovalManage,
            Permissions.Report.PriceViewAllCompanies, Permissions.Analysis.PurchaseHistoryView, Permissions.Analysis.PurchaseHistoryViewAllCompanies,
        ]),
        new("CUCTRUONGPHONG", "Trưởng phòng cung ứng chung", "Tiếp nhận, kiểm tra hồ sơ, quy cách đề nghị mua; điều hành tìm kiếm, đánh giá NCC (≥ 03 NCC mỗi nhóm hàng); đặt hàng cho các công ty đúng NCC và giá đã duyệt, theo dõi giao hàng",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Purchase.PrApprove, Permissions.Purchase.PoCreate, Permissions.MasterData.SupplierManage, Permissions.Sourcing.RfqManage,
            Permissions.MasterData.SupplierReviewManage,
            Permissions.Inventory.DocumentView, Permissions.Inventory.StockView,
            Permissions.Report.PurchaseView, Permissions.Report.PurchaseViewAllCompanies, Permissions.Purchase.DemandPlanViewAllCompanies,
            Permissions.Purchase.PooledOrderManage,
        ]),
        new("CUCDATHANG", "Nhân viên cung ứng (Phòng cung ứng chung)", "Nhóm cung ứng F&B/VT: tìm kiếm, đánh giá NCC, hỏi giá (RFQ); lập PO theo NCC và giá Ban đã duyệt, theo dõi giao hàng – không tự đổi NCC/giá",
        [
            .. CatalogView, .. ProcurementView,
            Permissions.Purchase.PoCreate, Permissions.MasterData.SupplierManage, Permissions.Sourcing.RfqManage,
            Permissions.MasterData.SupplierReviewManage,
            Permissions.Inventory.DocumentView, Permissions.Inventory.StockView, Permissions.Report.PurchaseView,
            Permissions.Purchase.PooledOrderManage,
        ]),
        new("DONVIDATHANG", "Đơn vị đặt hàng (nhà hàng, NDAT, F&B…)", "Lập nhu cầu (đề nghị mua, kế hoạch nhu cầu); xem phiếu nhập để xác nhận số lượng, chất lượng",
        [
            .. CatalogView, Permissions.Purchase.PrView, Permissions.Purchase.PrCreate,
            Permissions.Purchase.DemandPlanView, Permissions.Purchase.DemandPlanManage, Permissions.Purchase.BudgetView,
            Permissions.Inventory.DocumentView, Permissions.Inventory.StockView,
        ]),
    ];
}
