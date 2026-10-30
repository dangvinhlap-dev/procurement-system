using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// Đổi lỗi trùng khóa của PostgreSQL (mã 23505) thành thông điệp tiếng Việt.
/// Dịch vụ luôn kiểm tra trùng trước khi lưu, nhưng truy vấn đó đi qua global query filter: bản ghi ngoài phạm vi người dùng
/// (ví dụ mã hàng thuộc nhóm hàng người đó không được xem) không thấy được, và hai người lưu cùng lúc cũng lọt qua.
/// Chỉ mục duy nhất trong CSDL là chốt chặn cuối; ở đây chỉ dịch lỗi của nó cho người dùng đọc được.
/// </summary>
public static class DuplicateKeyMessages
{
    private static readonly Dictionary<string, string> ByConstraint = new(StringComparer.Ordinal)
    {
        ["IX_Company_Code"] = "Mã công ty đã có.",
        ["IX_Company_TaxCode"] = "Mã số thuế đã dùng cho công ty khác.",
        ["IX_Warehouse_CompanyId_Code"] = "Mã kho đã có ở công ty này.",
        ["IX_ItemCategory_Code"] = "Mã nhóm hàng đã có (có thể thuộc nhóm bạn không được xem).",
        ["IX_Uom_Code"] = "Mã đơn vị tính đã có.",
        ["IX_Item_Code"] = "Mã hàng đã có (có thể thuộc nhóm hàng bạn không được xem).",
        ["IX_Role_Code"] = "Mã vai trò đã có.",
        ["IX_SupplierGroup_Code"] = "Mã nhóm NCC đã có.",
        ["IX_Supplier_Code"] = "Mã NCC đã có.",
        ["IX_Supplier_TaxCode"] = "Mã số thuế đã dùng cho NCC khác.",
        ["IX_Supplier_InternalCompanyId"] = "Công ty này đã có NCC nội bộ.",
    };

    /// <summary>
    /// Thông điệp cho lỗi trùng khóa.
    /// </summary>
    /// <param name="exception">Lỗi bất kỳ.</param>
    /// <returns>Thông điệp tiếng Việt nếu là lỗi trùng khóa; null nếu là lỗi khác.</returns>
    public static string? Describe(Exception exception)
    {
        if (exception is not DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg })
            return null;
        return pg.ConstraintName is { } name && ByConstraint.TryGetValue(name, out var message)
            ? message
            : "Dữ liệu bị trùng với bản ghi đã có. Hãy tải lại màn hình rồi kiểm tra.";
    }
}
