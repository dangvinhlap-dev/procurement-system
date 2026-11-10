using Microsoft.EntityFrameworkCore;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Tìm người duyệt động của bước duyệt (lát O1, design.md mục 19.3): Trưởng phòng ban của bộ phận trên phiếu hoặc Giám đốc khối của khối
/// chứa bộ phận. Đọc danh mục <b>hiện hành</b> lúc duyệt (không chụp lúc gửi) – đổi Trưởng phòng ban khi phiếu đang chờ thì người mới duyệt.
/// Bộ phận/khối đọc bỏ filter công ty nhưng chỉ đúng các bộ phận của bước người dùng đã thấy được (bước thuộc phiếu đã qua phạm vi).
/// </summary>
internal sealed class DynamicApprovers
{
    /// <summary>Tên loại người duyệt "Trưởng phòng ban" trên cấu hình APRL.</summary>
    public const string DepartmentHeadLabel = "Trưởng phòng ban của phiếu";

    /// <summary>Tên loại người duyệt "Giám đốc khối" trên cấu hình APRL.</summary>
    public const string DivisionDirectorLabel = "GĐ khối của phiếu";

    private readonly ProcDbContext _db;

    /// <summary>Tạo bộ tìm người duyệt động.</summary>
    /// <param name="db">DbContext.</param>
    public DynamicApprovers(ProcDbContext db) => _db = db;

    /// <summary>Người duyệt động của một bước.</summary>
    /// <param name="UserId">Người duyệt; null khi chưa khai báo đủ (xem <paramref name="Problem"/>).</param>
    /// <param name="UserName">Họ tên người duyệt.</param>
    /// <param name="Label">Nhãn cấp trong lịch sử duyệt, ví dụ "Trưởng phòng ban PX1", "GĐ khối HCNS".</param>
    /// <param name="Problem">Lý do chưa có người duyệt (câu đầy đủ, chỉ chỗ sửa) hoặc null.</param>
    public sealed record Resolution(Guid? UserId, string? UserName, string Label, string? Problem);

    /// <summary>Bước có người duyệt động không.</summary>
    /// <param name="step">Bước duyệt.</param>
    public static bool IsDynamic(ApprovalStep step) => step.Kind != ApproverKind.Role;

    /// <summary>
    /// Người duyệt động của các bước (bỏ qua bước theo vai trò).
    /// Ví dụ bộ phận PX1 (Trưởng phòng ban: Nguyễn An) thuộc khối SX (GĐ khối: chưa khai): bước Trưởng phòng ban → (An, "Trưởng phòng ban PX1");
    /// bước GĐ khối → (null, "GĐ khối SX", "Khối SX chưa khai Giám đốc khối …").
    /// </summary>
    /// <param name="steps">Các bước.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo Id bước; chỉ có bước người duyệt động.</returns>
    public async Task<Dictionary<Guid, Resolution>> ResolveAsync(IEnumerable<ApprovalStep> steps, CancellationToken cancellationToken)
    {
        var dynamic = steps.Where(IsDynamic).ToList();
        var result = new Dictionary<Guid, Resolution>();
        if (dynamic.Count == 0) return result;

        var departmentIds = dynamic.Select(s => s.DepartmentId).OfType<Guid>().Distinct().ToList();
        var departments = await _db.Departments.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(d => departmentIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Code, d.ManagerUserId, d.DivisionId })
            .ToDictionaryAsync(d => d.Id, cancellationToken);
        var divisionIds = departments.Values.Select(d => d.DivisionId).OfType<Guid>().Distinct().ToList();
        var divisions = await _db.Divisions.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(d => divisionIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Code, d.DirectorUserId })
            .ToDictionaryAsync(d => d.Id, cancellationToken);
        var userIds = departments.Values.Select(d => d.ManagerUserId).Concat(divisions.Values.Select(d => d.DirectorUserId)).OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.IsActive })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        foreach (var step in dynamic)
        {
            if (step.DepartmentId is not { } departmentId || !departments.TryGetValue(departmentId, out var department))
            {
                result[step.Id] = new(null, null, KindLabel(step.Kind), "Không tìm thấy bộ phận của phiếu.");
                continue;
            }

            Guid? userId;
            string label;
            string missing;
            if (step.Kind == ApproverKind.DepartmentHead)
            {
                userId = department.ManagerUserId;
                label = $"Trưởng phòng ban {department.Code}";
                missing = $"Bộ phận {department.Code} chưa khai Trưởng phòng ban (Hệ thống → Bộ phận).";
            }
            else if (department.DivisionId is not { } divisionId || !divisions.TryGetValue(divisionId, out var division))
            {
                result[step.Id] = new(null, null, $"GĐ khối (bộ phận {department.Code})", $"Bộ phận {department.Code} chưa gán khối (Hệ thống → Bộ phận).");
                continue;
            }
            else
            {
                userId = division.DirectorUserId;
                label = $"GĐ khối {division.Code}";
                missing = $"Khối {division.Code} chưa khai Giám đốc khối (Hệ thống → Khối).";
            }

            if (userId is not { } id)
                result[step.Id] = new(null, null, label, missing);
            else if (!users.TryGetValue(id, out var user) || !user.IsActive)
                result[step.Id] = new(null, user?.FullName, label, $"Tài khoản {label} ({user?.FullName}) đã ngừng dùng – hãy khai người khác.");
            else
                result[step.Id] = new(id, user.FullName, label, null);
        }

        return result;
    }

    /// <summary>Tên loại người duyệt động khi chưa biết bộ phận.</summary>
    /// <param name="kind">Loại người duyệt.</param>
    public static string KindLabel(ApproverKind kind) => kind == ApproverKind.DivisionDirector ? DivisionDirectorLabel : DepartmentHeadLabel;
}
