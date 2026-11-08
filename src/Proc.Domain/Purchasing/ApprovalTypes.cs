using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;

namespace Proc.Domain.Purchasing;

/// <summary>Một loại đối tượng đi qua quy trình duyệt dùng chung.</summary>
/// <param name="Code">Mã loại (lưu ở <c>ApprovalRule.DocType</c>, <c>ApprovalStep.DocType</c>).</param>
/// <param name="Name">Tên hiển thị, dùng trong câu thông báo ("Bảng giá NCC PRL-0001 chờ bạn duyệt").</param>
/// <param name="ApprovePermission">Quyền duyệt loại này (người duyệt cần quyền, cấp có vai trò thì cần thêm vai trò).</param>
/// <param name="AmountLabel">Ý nghĩa giá trị xét ngưỡng; null = loại không có giá trị tiền (ngưỡng luôn 0).</param>
public sealed record ApprovalTypeInfo(string Code, string Name, string ApprovePermission, string? AmountLabel);

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

    /// <summary>Vượt ngân sách đơn mua hàng (lát C3, design.md mục 18.24) – như <see cref="PurchaseRequestOverBudget"/>, cùng quyền duyệt PO.</summary>
    public const string PurchaseOrderOverBudget = "POB";

    /// <summary>
    /// Mua ngoài danh sách/giá đã duyệt (lát G3, design.md mục 18.25): PO có dòng NCC ngoài NCC chính/dự phòng của phiếu duyệt tháng hoặc
    /// dòng không có giá đã duyệt qua thêm chặng duyệt này (luôn ít nhất một cấp), cùng quyền duyệt PO. Chỉ là bộ cấp cấu hình ở APRL.
    /// </summary>
    public const string PurchaseOrderOffList = "POL";

    /// <summary>Chặng ngoại lệ giá PO (design.md mục 18.10) trong <c>ApprovalStep.Stage</c>.</summary>
    public const int PriceExceptionStage = 2;

    /// <summary>Chặng vượt ngân sách (design.md mục 18.24) – sau chặng ngoại lệ giá nếu có, cùng số chặng cho PR và PO.</summary>
    public const int OverBudgetStage = 3;

    /// <summary>Chặng mua ngoài danh sách/giá đã duyệt (design.md mục 18.25) – sau chặng vượt ngân sách nếu có.</summary>
    public const int OffListStage = 4;

    /// <summary>Mọi loại có duyệt, theo thứ tự hiển thị.</summary>
    public static readonly IReadOnlyList<ApprovalTypeInfo> All =
    [
        new(DocumentTypes.PurchaseRequest, "Đề nghị mua hàng", Permissions.Purchase.PrApprove, "Tổng dự kiến"),
        new(DocumentTypes.PurchaseOrder, "Đơn mua hàng", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT"),
        new(PurchaseOrderPriceException, "Ngoại lệ giá đơn mua hàng", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT"),
        new(PurchaseOrderEmergency, "Đơn mua hàng khẩn cấp", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT"),
        new(PurchaseRequestOverBudget, "Vượt ngân sách đề nghị mua hàng", Permissions.Purchase.PrApprove, "Tổng dự kiến"),
        new(PurchaseOrderOverBudget, "Vượt ngân sách đơn mua hàng", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT"),
        new(PurchaseOrderOffList, "Mua ngoài danh sách/giá đã duyệt", Permissions.Purchase.PoApprove, "Tổng thanh toán gồm VAT"),
        new(DocumentTypes.PriceList, "Bảng giá NCC", Permissions.Pricing.PriceListApprove, null),
        new(DocumentTypes.RequestForQuotation, "Kết quả chọn thầu", Permissions.Sourcing.RfqApproveAward, "Giá trị trúng thầu"),
        new(Supplier, "Hồ sơ nhà cung cấp", Permissions.MasterData.SupplierApprove, null),
        new(DocumentTypes.Contract, "Hợp đồng nguyên tắc", Permissions.Purchase.ContractApprove, "Giá trị dự kiến"),
        new(ContractAmendment, "Phụ lục hợp đồng nguyên tắc", Permissions.Purchase.ContractApprove, "Giá trị dự kiến sau phụ lục"),
        new(DocumentTypes.SoleSource, "Phê duyệt NCC độc quyền", Permissions.MasterData.SoleSourceApprove, null),
        new(DocumentTypes.MonthlySupplierApproval, "Duyệt NCC và giá tháng", Permissions.Pricing.MonthlyApprovalApprove, null),
        new(DocumentTypes.VolumeRebate, "Thỏa thuận chiết khấu sản lượng gộp", Permissions.Pricing.VolumeRebateApprove, null),
    ];

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
}
