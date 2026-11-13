using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;

namespace Proc.Domain.Purchasing;

/// <summary>Một loại đối tượng đi qua quy trình duyệt dùng chung.</summary>
/// <param name="Code">Mã loại (lưu ở <c>ApprovalRule.DocType</c>, <c>ApprovalStep.DocType</c>).</param>
/// <param name="Name">Tên hiển thị, dùng trong câu thông báo ("Bảng giá NCC PRL-0001 chờ bạn duyệt").</param>
/// <param name="ApprovePermission">Quyền duyệt loại này (người duyệt cần quyền, cấp có vai trò thì cần thêm vai trò).</param>
/// <param name="AmountLabel">Ý nghĩa giá trị xét ngưỡng; null = loại không có giá trị tiền (ngưỡng luôn 0).</param>
/// <param name="HasDepartment">
/// Phiếu có bộ phận (PR, PO và các bộ cấp phụ của chúng) – cấu hình được người duyệt động Trưởng phòng ban / GĐ khối (lát O1).
/// </param>
/// <param name="HasStorekeeper">Bộ cấp đề nghị mua có nơi nhận (PR, PRA) – cấu hình được cấp "Thủ kho nơi nhận" (lát Y3c).</param>
public sealed record ApprovalTypeInfo(
    string Code, string Name, string ApprovePermission, string? AmountLabel, bool HasDepartment = false, bool HasStorekeeper = false);

/// <summary>
/// Các loại đối tượng có duyệt (design.md mục 6.2, 18.6). Lát A3 mở rộng từ PR, PO sang bảng giá NCC (PRL), kết quả chọn thầu (RFQ),
/// hồ sơ NCC (NCC) và hợp đồng nguyên tắc (CTR) – cấu hình cấp duyệt được ngay, nghiệp vụ gửi duyệt của từng loại làm ở lát B1–B4.
/// </summary>
public static class ApprovalTypes
{
    /// <summary>Hồ sơ nhà cung cấp: đưa vào Đã duyệt/Danh sách đen, đổi tài khoản ngân hàng (lát B2). Mã trùng mã màn hình NCC.</summary>
    public const string Supplier = "NCC";

    /// <summary>
    /// Phụ lục hợp đồng nguyên tắc (lát B4b): cấu hình cấp duyệt riêng với hợp đồng (phụ lục gia hạn thường duyệt nhẹ hơn ký mới), cùng
    /// quyền duyệt hợp đồng. Chưa cấu hình cấp thì một cấp – ai có quyền duyệt hợp đồng tại công ty đều duyệt được (design.md mục 6.2).
    /// </summary>
    public const string ContractAmendment = "CTA";

    /// <summary>
    /// Ngoại lệ giá đơn mua hàng (lát C1, design.md mục 18.10): PO có dòng đơn giá khác giá đã duyệt phải qua thêm chặng duyệt này sau
    /// các cấp duyệt PO thường, cùng quyền duyệt PO. Không phải loại phiếu riêng – chỉ là bộ cấp duyệt cấu hình riêng ở APRL.
    /// </summary>
    public const string PurchaseOrderPriceException = "POX";

    /// <summary>
    /// Đơn mua hàng khẩn cấp (lát C2a, design.md mục 18.11): PO khẩn duyệt theo bộ cấp riêng (thường gọn hơn) thay cho cấp duyệt PO
    /// thường; chưa cấu hình thì dùng cấp duyệt PO. Cùng quyền duyệt PO.
    /// </summary>
    public const string PurchaseOrderEmergency = "POK";

    /// <summary>
    /// Vượt ngân sách đề nghị mua hàng (lát C3, design.md mục 18.24): PR vượt ngân sách bộ phận × tháng qua thêm chặng duyệt này sau các
    /// cấp duyệt PR thường, cùng quyền duyệt PR. Chỉ là bộ cấp duyệt cấu hình riêng ở APRL.
    /// </summary>
    public const string PurchaseRequestOverBudget = "PRB";

    /// <summary>
    /// Đề nghị mua loại A – công trình (lát Y3c, design.md mục 19.3 – Q6): bộ cấp duyệt thay cho PR khi đề nghị là loại A (1A – không có TP HC);
    /// chưa cấu hình thì dùng cấp duyệt PR. Cùng quyền duyệt PR.
    /// </summary>
    public const string PurchaseRequestConstruction = "PRA";

    /// <summary>
    /// Đề nghị mua dịch vụ (lát Y3c – Q6): bộ cấp duyệt thay cho PR khi đề nghị là dịch vụ (không có bước Thủ kho); chưa cấu hình thì dùng cấp
    /// duyệt PR và tự bỏ cấp Thủ kho. Cùng quyền duyệt PR.
    /// </summary>
    public const string PurchaseRequestService = "PRS";

    /// <summary>
    /// Đề nghị mua loại A vượt dự toán công trình (lát Y3c – Q5): thêm chặng duyệt này (luôn ít nhất một cấp) khi lũy kế đã mua + giá trị đề nghị
    /// vượt dự toán; không chặn gửi. Cùng quyền duyệt PR. Chỉ là bộ cấp cấu hình ở APRL.
    /// </summary>
    public const string PurchaseRequestOverEstimate = "PRE";

    /// <summary>Vượt ngân sách đơn mua hàng (lát C3, design.md mục 18.24) – như <see cref="PurchaseRequestOverBudget"/>, cùng quyền duyệt PO.</summary>
    public const string PurchaseOrderOverBudget = "POB";

    /// <summary>
    /// Mua ngoài danh sách/giá đã duyệt (lát G3, design.md mục 18.25): PO có dòng NCC ngoài NCC chính/dự phòng của phiếu duyệt tháng hoặc
    /// dòng không có giá đã duyệt qua thêm chặng duyệt này (luôn ít nhất một cấp), cùng quyền duyệt PO. Chỉ là bộ cấp cấu hình ở APRL.
    /// </summary>
    public const string PurchaseOrderOffList = "POL";

    /// <summary>
    /// PO vượt giá trần (lát H3, design.md mục 18.25 – quyết định Q3): PO có dòng giá ròng vượt giá trần của phiếu duyệt NCC và giá tháng phải
    /// lên Chủ tịch/TGĐ duyệt – thêm chặng này sau mọi chặng khác (luôn ít nhất một cấp, bỏ cấp có vai trò đã duyệt ở chặng trước), cùng quyền
    /// duyệt PO. Chỉ là bộ cấp cấu hình ở APRL.
    /// </summary>
    public const string PurchaseOrderOverCeiling = "POC";

    /// <summary>
    /// Ngoại lệ phiếu duyệt NCC và giá tháng (lát H2, design.md mục 18.25 – quyết định Q6): phiếu GTH có ngoại lệ (mặt hàng chưa đủ 03 NCC đủ
    /// điều kiện, độc quyền, giá vượt giá mục tiêu, hoặc người lập khai ngoại lệ) qua thêm chặng Chủ tịch/TGĐ sau các cấp Hội đồng, cùng quyền
    /// duyệt GTH. Chỉ là bộ cấp cấu hình ở APRL của tổng công ty.
    /// </summary>
    public const string MonthlyApprovalException = "GTX";

    /// <summary>Chặng ngoại lệ của phiếu GTH (Chủ tịch/TGĐ – lát H2) trong <c>ApprovalStep.Stage</c>, sau chặng Hội đồng.</summary>
    public const int MonthlyApprovalExceptionStage = 2;

    /// <summary>Chặng ngoại lệ giá PO (design.md mục 18.10) trong <c>ApprovalStep.Stage</c>.</summary>
    public const int PriceExceptionStage = 2;

    /// <summary>Chặng vượt ngân sách (design.md mục 18.24) – sau chặng ngoại lệ giá nếu có, cùng số chặng cho PR và PO.</summary>
    public const int OverBudgetStage = 3;

    /// <summary>Chặng mua ngoài danh sách/giá đã duyệt (design.md mục 18.25) – sau chặng vượt ngân sách nếu có.</summary>
    public const int OffListStage = 4;

    /// <summary>Chặng PO vượt giá trần – Chủ tịch/TGĐ (lát H3), sau chặng ngoài danh sách/giá nếu có.</summary>
    public const int OverCeilingStage = 5;

    /// <summary>Chặng đề nghị mua vượt dự toán công trình (lát Y3c), sau chặng vượt ngân sách nếu có.</summary>
    public const int OverEstimateStage = 6;

    /// <summary>
    /// Chặng của cấp cao nhất trong chuỗi duyệt thường khi đề nghị mua có chặng vượt (vượt ngân sách/định mức PRB, vượt dự toán PRE): cấp đó
    /// (Chủ tịch theo biểu mẫu) duyệt sau cùng để phần vượt được xem trước khi trình lên (người dùng yêu cầu 11/11/2026, lát Y3c). Không gắn
    /// tên chặng riêng – giao diện hiện là "Duyệt thường".
    /// </summary>
    public const int TopLevelStage = 9;

    /// <summary>Mọi loại có duyệt, theo thứ tự hiển thị.</summary>
    public static readonly IReadOnlyList<ApprovalTypeInfo> All =
    [
        new(DocumentTypes.PurchaseRequest, "Đề nghị mua hàng", Permissions.Purchase.PrApprove, "Tổng dự kiến", HasDepartment: true, HasStorekeeper: true),
        new(PurchaseRequestConstruction, "Đề nghị mua công trình (loại A)", Permissions.Purchase.PrApprove, "Tổng dự kiến", HasDepartment: true, HasStorekeeper: true),
        new(PurchaseRequestService, "Đề nghị mua dịch vụ", Permissions.Purchase.PrApprove, "Tổng dự kiến", HasDepartment: true),
        new(PurchaseRequestOverEstimate, "Đề nghị mua vượt dự toán công trình", Permissions.Purchase.PrApprove, "Tổng dự kiến", HasDepartment: true),
        new(DocumentTypes.PurchaseOrder, "Đơn mua hàng", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT", HasDepartment: true),
        new(PurchaseOrderPriceException, "Ngoại lệ giá đơn mua hàng", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT", HasDepartment: true),
        new(PurchaseOrderEmergency, "Đơn mua hàng khẩn cấp", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT", HasDepartment: true),
        new(PurchaseRequestOverBudget, "Vượt ngân sách đề nghị mua hàng", Permissions.Purchase.PrApprove, "Tổng dự kiến", HasDepartment: true),
        new(PurchaseOrderOverBudget, "Vượt ngân sách đơn mua hàng", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT", HasDepartment: true),
        new(PurchaseOrderOffList, "Mua ngoài danh sách/giá đã duyệt", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT", HasDepartment: true),
        new(PurchaseOrderOverCeiling, "Đơn mua hàng vượt giá trần", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT", HasDepartment: true),
        // Bảng tổng hợp 2A/2B (lát Y5b): có bộ phận đề xuất – cấu hình được GĐ khối của phiếu như biểu mẫu CUVT.
        new(DocumentTypes.PurchaseSummary, "Bảng tổng hợp đề xuất mua", Permissions.Purchase.SummaryApprove, "Tổng đề xuất", HasDepartment: true),
        // Đề nghị thanh toán 5A/5B (lát Y8): bộ phận đặt hàng của PO – cấp đầu là GĐ khối của phiếu như biểu mẫu CUVT.
        new(DocumentTypes.PaymentRequest, "Đề nghị thanh toán", Permissions.Purchase.PaymentApprove, "Số tiền đề nghị", HasDepartment: true),
        new(DocumentTypes.PriceList, "Bảng giá NCC", Permissions.Pricing.PriceListApprove, null),
        new(DocumentTypes.RequestForQuotation, "Kết quả chọn thầu", Permissions.Sourcing.RfqApproveAward, "Giá trị trúng thầu"),
        new(Supplier, "Hồ sơ nhà cung cấp", Permissions.MasterData.SupplierApprove, null),
        new(DocumentTypes.Contract, "Hợp đồng nguyên tắc", Permissions.Purchase.ContractApprove, "Giá trị dự kiến"),
        new(ContractAmendment, "Phụ lục hợp đồng nguyên tắc", Permissions.Purchase.ContractApprove, "Giá trị dự kiến sau phụ lục"),
        new(DocumentTypes.SoleSource, "Phê duyệt NCC độc quyền", Permissions.MasterData.SoleSourceApprove, null),
        new(DocumentTypes.MonthlySupplierApproval, "Duyệt NCC và giá tháng", Permissions.Pricing.MonthlyApprovalApprove, null),
        new(MonthlyApprovalException, "Ngoại lệ duyệt NCC và giá tháng", Permissions.Pricing.MonthlyApprovalApprove, null),
        new(DocumentTypes.VolumeRebate, "Thỏa thuận chiết khấu sản lượng gộp", Permissions.Pricing.VolumeRebateApprove, null),
    ];

    /// <summary>
    /// Mã chỉ là bộ cấp duyệt phụ/thay thế (chặng ngoại lệ, PO khẩn cấp…) – bước duyệt của chúng vẫn mang loại phiếu gốc (PR, PO, GTH). Dùng lọc
    /// danh sách loại phiếu khi lập ủy quyền duyệt (lát O2): ủy quyền "Đơn mua hàng" đã gồm mọi chặng của PO.
    /// </summary>
    public static readonly IReadOnlySet<string> RuleOnlyCodes = new HashSet<string>
    {
        PurchaseOrderPriceException, PurchaseOrderEmergency, PurchaseRequestOverBudget, PurchaseRequestConstruction, PurchaseRequestService,
        PurchaseRequestOverEstimate, PurchaseOrderOverBudget, PurchaseOrderOffList,
        PurchaseOrderOverCeiling, MonthlyApprovalException,
    };

    /// <summary>Tìm loại theo mã (không phân biệt hoa thường, bỏ khoảng trắng); null nếu không có duyệt.</summary>
    public static ApprovalTypeInfo? Find(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        return All.FirstOrDefault(t => t.Code == normalized);
    }

    /// <summary>Thông tin loại theo mã.</summary>
    /// <exception cref="BusinessRuleException">Khi loại không có duyệt.</exception>
    public static ApprovalTypeInfo Get(string? code) =>
        Find(code) ?? throw new BusinessRuleException($"Loại phiếu duyệt phải là {string.Join(", ", All.Select(t => t.Code))}.");
}

/// <summary>
/// Đối tượng đi qua quy trình duyệt dùng chung (design.md mục 18.6): PR, PO hôm nay; bảng giá, kết quả chọn thầu, yêu cầu sửa NCC,
/// hợp đồng ở lát B. Đối tượng tự giữ trạng thái của mình; cơ chế duyệt chỉ cần các thông tin dưới đây để sinh bước, kiểm tra người
/// duyệt và gửi thông báo.
/// </summary>
public interface IApprovalSubject
{
    /// <summary>Id đối tượng (<c>ApprovalStep.DocumentId</c>).</summary>
    Guid Id { get; }

    /// <summary>Công ty – quy trình duyệt và người duyệt tính theo công ty này.</summary>
    Guid CompanyId { get; }

    /// <summary>Mã loại trong <see cref="ApprovalTypes"/>.</summary>
    string DocType { get; }

    /// <summary>Số phiếu / mã hiển thị trong thông báo.</summary>
    string No { get; }

    /// <summary>Người lập – không tự duyệt (tách người lập và người duyệt).</summary>
    Guid? CreatedBy { get; }

    /// <summary>Giá trị xét ngưỡng duyệt (loại không có tiền trả 0).</summary>
    decimal ApprovalAmount { get; }

    /// <summary>Lượt gửi duyệt hiện tại.</summary>
    int ApprovalRound { get; }

    /// <summary>Đang chờ duyệt (mới duyệt/từ chối được).</summary>
    bool IsAwaitingApproval { get; }

    /// <summary>Bắt đầu lượt gửi duyệt mới: chuyển sang chờ duyệt.</summary>
    /// <returns>Số lượt mới.</returns>
    /// <exception cref="BusinessRuleException">Khi đối tượng chưa đủ điều kiện gửi.</exception>
    int Submit();

    /// <summary>Duyệt đủ các cấp.</summary>
    void MarkApproved(DateTimeOffset at);

    /// <summary>Bị từ chối: về trạng thái sửa được, giữ lý do.</summary>
    void ReturnToDraft(string? reason);

    /// <summary>
    /// Người dùng có phải người lập (không được tự duyệt, không nhận thông báo chờ duyệt) không. Mặc định là người tạo; đối tượng có
    /// người đề xuất riêng (ví dụ người chọn thầu của RFQ) thì tính cả người đó.
    /// </summary>
    /// <param name="userId">Người dùng.</param>
    bool IsPreparedBy(Guid userId) => CreatedBy == userId;

    /// <summary>
    /// NCC của đối tượng duyệt – người có kê khai xung đột lợi ích còn hiệu lực với một trong các NCC này không được duyệt
    /// (design.md mục 18.8). Mặc định không có NCC (PR).
    /// </summary>
    IReadOnlyCollection<Guid> RelatedSupplierIds => [];

    /// <summary>
    /// Bộ phận của phiếu – để tìm người duyệt động Trưởng phòng ban / Giám đốc khối (lát O1, design.md mục 19.3). Mặc định không có (loại
    /// phiếu không có bộ phận chỉ cấu hình được người duyệt theo vai trò).
    /// </summary>
    Guid? ApprovalDepartmentId => null;

    /// <summary>
    /// Nơi nhận của phiếu – để tìm Thủ kho nơi nhận (lát Y3c, design.md mục 19.3). Mặc định không có (loại phiếu không có bước Thủ kho).
    /// </summary>
    Guid? ApprovalWarehouseId => null;

    /// <summary>
    /// Phiếu có bước Thủ kho không – false thì cấp "Thủ kho nơi nhận" của bộ cấp bị bỏ khi gửi (đề nghị dịch vụ dùng bộ PR – lát Y3c).
    /// </summary>
    bool HasStorekeeperStep => false;

    /// <summary>
    /// Điều kiện của chính phiếu để duyệt bước (ngoài quyền, vai trò của người duyệt): null = duyệt được. Ví dụ bước Thủ kho của đề nghị mua
    /// phải nhập tồn trong lượt này trước (lát Y3c). Không chặn từ chối.
    /// </summary>
    /// <param name="step">Bước đang đến lượt.</param>
    string? ApproveReadyBlocker(ApprovalStep step) => null;
}
