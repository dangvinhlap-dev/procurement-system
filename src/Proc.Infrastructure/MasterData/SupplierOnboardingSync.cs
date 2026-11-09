using Microsoft.EntityFrameworkCore;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Đồng bộ hồ sơ đánh giá NCC mới (HSN, lát G8 – design.md mục 18.25) theo yêu cầu duyệt NCC đi kèm: hồ sơ không có luồng duyệt riêng mà
/// dùng yêu cầu duyệt NCC hai chặng (mục 18.6), nên mỗi khi yêu cầu kết thúc thì hồ sơ đổi theo trong cùng lần lưu.
/// </summary>
internal static class SupplierOnboardingSync
{
    /// <summary>
    /// Cập nhật hồ sơ gắn với yêu cầu (nếu có): Đã duyệt → hồ sơ Đã duyệt và NCC có ngành hàng là nhóm đề xuất; Từ chối → hồ sơ Từ chối;
    /// Rút → hồ sơ về Đã đánh giá. Yêu cầu còn chờ hoặc không gắn hồ sơ thì không làm gì. Chưa lưu – người gọi lưu.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="change">Yêu cầu duyệt NCC vừa đổi trạng thái.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task.</returns>
    public static async Task AfterChangeRequestAsync(ProcDbContext db, SupplierChangeRequest change, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (change.Status == SupplierChangeRequestStatus.Submitted) return;
        // Hồ sơ thuộc tổng công ty nhưng công ty liên quan có thể từ chối ở chặng 1 khi đang làm việc ở công ty mình: bỏ filter công ty, chỉ
        // đọc đúng hồ sơ gắn với yêu cầu này để đồng bộ (không trả dữ liệu cho người dùng).
        var onboarding = await db.SupplierOnboardings.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .SingleOrDefaultAsync(o => o.ChangeRequestId == change.Id && o.Status == SupplierOnboardingStatus.Submitted, cancellationToken);
        if (onboarding is null) return;

        switch (change.Status)
        {
            case SupplierChangeRequestStatus.Approved:
                onboarding.MarkApproved(now);
                await AddCategoryAsync(db, onboarding, cancellationToken);
                break;
            case SupplierChangeRequestStatus.Rejected:
                onboarding.MarkRejected(change.RejectReason);
                break;
            case SupplierChangeRequestStatus.Cancelled:
                onboarding.Withdraw();
                break;
        }
    }

    /// <summary>
    /// Ghi nhận ngành hàng đề xuất cho NCC để NCC được đếm vào danh sách NCC đủ điều kiện của nhóm (SUPCOV – G1b). Đã có thì thôi; nhóm hàng
    /// đã ngừng dùng thì bỏ qua (không chặn việc duyệt NCC – ngành hàng sửa sau trên form NCC được, không cần duyệt).
    /// </summary>
    private static async Task AddCategoryAsync(ProcDbContext db, SupplierOnboarding onboarding, CancellationToken cancellationToken)
    {
        if (await db.SupplierCategories.AnyAsync(c => c.SupplierId == onboarding.SupplierId && c.ItemCategoryId == onboarding.CategoryId, cancellationToken))
            return;
        // Ngành hàng NCC không lọc theo phạm vi nhóm hàng của người duyệt (như SetCategoriesAsync – mục 5.3).
        var category = await db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).SingleOrDefaultAsync(c => c.Id == onboarding.CategoryId, cancellationToken);
        if (category is not { IsActive: true }) return;
        db.SupplierCategories.Add(SupplierCategory.Create(onboarding.SupplierId, category));
    }
}
