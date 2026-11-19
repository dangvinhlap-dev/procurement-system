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

    /// <summary>Tên loại người duyệt "Thủ kho nơi nhận" trên cấu hình APRL (lát Y3c).</summary>
    public const string StorekeeperLabel = "Thủ kho nơi nhận";

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

    /// <summary>
    /// Bước có người duyệt động theo bộ phận (Trưởng phòng ban / GĐ khối – một người) không. Bước Thủ kho nơi nhận (lát Y3c) không tính: nhiều
    /// thủ kho cùng một kho đều duyệt được, tìm theo quyền nhập tồn + phạm vi kho (<see cref="ApprovalEngine.StepPermission"/>).
    /// </summary>
    /// <param name="step">Bước duyệt.</param>
    public static bool IsDynamic(ApprovalStep step) => step.Kind is ApproverKind.DepartmentHead or ApproverKind.DivisionDirector;

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

    /// <summary>
    /// Chức danh của người lập nếu họ là người <b>cao nhất</b> của bộ phận trên phiếu – khi đó bước "Trưởng phòng ban của phiếu" gộp với người
    /// lập (người dùng chốt 19/11/2026 – design.md mục 19.3): người lập là Trưởng phòng ban; hoặc là Phó phòng ban khi bộ phận <b>chưa có</b>
    /// Trưởng phòng ban. Người lập thấp hơn Trưởng phòng ban (nhân viên, Phó phòng ban khi đã có Trưởng phòng ban) thì Trưởng phòng ban vẫn
    /// duyệt. Đọc danh mục hiện hành lúc gửi.
    /// Ví dụ PX1 có Trưởng phòng An, Phó phòng Bình: An lập → gộp; Bình lập → An duyệt. PX2 chưa khai Trưởng phòng, Phó phòng Cường lập → gộp.
    /// </summary>
    /// <param name="departmentId">Bộ phận của phiếu.</param>
    /// <param name="creatorId">Người lập.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>"Trưởng phòng ban PX1", "Phó phòng ban PX2" hoặc null (không gộp).</returns>
    public async Task<string?> CreatorTitleAsync(Guid departmentId, Guid creatorId, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(d => d.Id == departmentId).Select(d => new { d.Code, d.ManagerUserId }).SingleOrDefaultAsync(cancellationToken);
        if (department is null) return null;
        if (department.ManagerUserId == creatorId) return $"Trưởng phòng ban {department.Code}";
        // Đã có Trưởng phòng ban thì người lập (kể cả Phó phòng ban) nhỏ hơn – Trưởng phòng ban phải duyệt.
        if (department.ManagerUserId is not null) return null;
        var deputy = await _db.DepartmentDeputies.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .AnyAsync(d => d.DepartmentId == departmentId && d.UserId == creatorId, cancellationToken);
        return deputy ? $"Phó phòng ban {department.Code}" : null;
    }

    /// <summary>Tên loại người duyệt (không theo vai trò) như trên cấu hình APRL, khi chưa biết bộ phận / nơi nhận.</summary>
    /// <param name="kind">Loại người duyệt.</param>
    public static string KindLabel(ApproverKind kind) => kind switch
    {
        ApproverKind.DivisionDirector => DivisionDirectorLabel,
        ApproverKind.Storekeeper => StorekeeperLabel,
        _ => DepartmentHeadLabel,
    };
}
