using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.Common;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Hàng mới chưa có mã của PR đã duyệt (design.md mục 18.12 – đề xuất Ban CUVT V3): chuyển nhóm Đàm phán tìm nguồn bằng phiếu tìm nguồn
/// (qua <see cref="ISourcingService"/> của module tìm nguồn), rồi gắn mã hàng khi đã có mã để lên PO/RFQ.
/// </summary>
public sealed class PurchaseRequestNewItemService : IPurchaseRequestNewItemService
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly ISourcingService _sourcing;
    private readonly PurchaseRequestService _requests;

    /// <summary>Tạo dịch vụ.</summary>
    public PurchaseRequestNewItemService(
        ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock, ISourcingService sourcing)
    {
        _db = db;
        _clock = clock;
        _sourcing = sourcing;
        _requests = new PurchaseRequestService(db, scope, numbers, clock);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> CreateSourcingAsync(Guid requestId, Guid newItemId, CancellationToken cancellationToken)
    {
        var pr = await _requests.LoadAsync(requestId, cancellationToken);
        var row = pr.PrepareSourcing(newItemId);
        var uom = await _db.Uoms.AsNoTracking().Where(u => u.Id == row.UomId).Select(u => u.Code).SingleOrDefaultAsync(cancellationToken);
        // Hàng chưa có mã nên phiếu tìm nguồn ghi mô tả; SL kèm ĐVT ghi vào ghi chú vì SL phiếu tìm nguồn hiểu theo ĐVT cơ sở của mặt hàng.
        // Phiếu tìm nguồn lưu trong giao dịch riêng của dịch vụ tìm nguồn; lỗi khi gắn vào dòng thì phiếu vẫn còn, người dùng thấy ở TIMNGUON.
        var sourcing = await _sourcing.CreateAsync(
            new SaveSourcingRequest(
                _clock.TodayInVietnam(), null, row.Description, row.Specification, row.Qty, row.NeededDate, null,
                $"Từ đề nghị {pr.No} – hàng mới dòng {row.LineNo}: {row.Qty:0.####} {uom}", []),
            cancellationToken);
        pr.LinkSourcing(newItemId, sourcing.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return await _requests.ToDtoAsync(pr, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> LinkItemAsync(Guid requestId, Guid newItemId, LinkNewItemRequest request, CancellationToken cancellationToken)
    {
        var pr = await _requests.LoadAsync(requestId, cancellationToken);
        // Đi qua filter nhóm hàng: chỉ gắn được mặt hàng trong phạm vi của người dùng (CLAUDE.md mục 4).
        var item = await _db.Items.Include(i => i.Conversions).SingleOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");
        pr.LinkNewItem(newItemId, item);
        await _db.SaveChangesAsync(cancellationToken);
        return await _requests.ToDtoAsync(pr, cancellationToken);
    }
}
