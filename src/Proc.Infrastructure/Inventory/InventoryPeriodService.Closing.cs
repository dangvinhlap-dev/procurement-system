using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Inventory;

/// <summary>
/// Đóng kỳ, tính giá vốn, mở lại kỳ (design.md mục 7.2–7.4). Giá vốn tính theo công ty × mặt hàng trên MỌI kho, mọi nhóm hàng
/// của công ty nên các truy vấn ở đây bỏ qua bộ lọc kho và nhóm hàng (vẫn giữ lọc công ty) – người đóng kỳ là kế toán kho trưởng
/// có quyền đóng kỳ, không phụ thuộc phạm vi xem của họ.
/// </summary>
public sealed partial class InventoryPeriodService
{
    /// <summary>Bộ lọc bỏ qua khi tính giá vốn: kho và nhóm hàng (giữ lọc công ty).</summary>
    private static readonly string[] CompanyWide = [ProcDbContext.WarehouseFilter, ProcDbContext.ItemCategoryFilter];

    /// <inheritdoc />
    public async Task<InventoryPeriodDto> CloseAsync(Guid id, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var periods = await _db.InventoryPeriods.ToListAsync(cancellationToken);
        var period = periods.SingleOrDefault(p => p.Id == id) ?? throw new NotFoundException("Không tìm thấy kỳ kho.");
        if (CloseBlocker(period, periods) is { } reason) throw new BusinessRuleException(reason);

        // Kiểm tra trước đóng (design.md mục 7.2 bước 1): không còn phiếu nháp trong kỳ.
        var drafts = await _db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters([ProcDbContext.WarehouseFilter])
            .Where(d => d.Status == DocumentStatus.Draft && d.Date >= period.StartDate && d.Date <= period.EndDate)
            .OrderBy(d => d.No).Select(d => d.No).ToListAsync(cancellationToken);
        if (drafts.Count > 0)
            throw new BusinessRuleException(
                $"Kỳ {period.Label} còn {drafts.Count} phiếu nháp ({string.Join(", ", drafts.Take(5))}{(drafts.Count > 5 ? ", …" : "")}) – ghi sổ hoặc hủy trước khi đóng kỳ.");

        // Bước 2: chuyển Đang tính giá và lưu ngay (giao dịch riêng). Ghi sổ/bỏ ghi sổ khóa dòng kỳ (FOR SHARE) và đọc lại trạng thái
        // trong giao dịch của nó, nên phiếu đang ghi dở xong trước, phiếu sau bị chặn với thông báo "đang tính giá vốn".
        period.StartCosting();
        await _db.SaveChangesAsync(cancellationToken);
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var previous = periods.SingleOrDefault(p => !p.IsYear && p.StartDate == period.StartDate.AddMonths(-1) && p.Status is PeriodStatus.Closed or PeriodStatus.Locked);
            await CalculateAsync(companyId, period, previous, cancellationToken);
            period.Close(_scope.UserId ?? Guid.Empty, _clock.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Tính giá lỗi (tồn âm…): bỏ mọi thay đổi chưa lưu, trả kỳ về Đang mở để sửa rồi đóng lại.
            _db.ChangeTracker.Clear();
            var reloaded = await _db.InventoryPeriods.SingleAsync(p => p.Id == id, CancellationToken.None);
            reloaded.CancelCosting();
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        return ToDto(period, canOpen: false);
    }

    /// <inheritdoc />
    public async Task<InventoryPeriodDto> ReopenAsync(Guid id, ReopenPeriodRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var periods = await _db.InventoryPeriods.ToListAsync(cancellationToken);
        var period = periods.SingleOrDefault(p => p.Id == id) ?? throw new NotFoundException("Không tìm thấy kỳ kho.");
        if (ReopenBlocker(period, periods) is { } reason) throw new BusinessRuleException(reason);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        period.Reopen(request.Reason);
        // Số dư, giá vốn của kỳ không còn đúng khi kỳ mở lại: xóa, tính lại khi đóng lần sau. Giá xuất về tạm tính (giữ số cũ).
        await _db.StockPeriodBalances.IgnoreQueryFilters(CompanyWide).Where(b => b.PeriodId == id).ExecuteDeleteAsync(cancellationToken);
        await _db.ItemPeriodCosts.IgnoreQueryFilters(CompanyWide).Where(c => c.PeriodId == id).ExecuteDeleteAsync(cancellationToken);
        var priced = PeriodCosting.PricedDocTypes.ToList();
        await _db.StockLedger.IgnoreQueryFilters(CompanyWide)
            .Where(l => l.PeriodId == id && !priced.Contains(l.DocType))
            .ExecuteUpdateAsync(u => u.SetProperty(l => l.IsCostFinal, false), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(period, canOpen: false);
    }

    /// <summary>
    /// Tính giá vốn và chốt số dư một kỳ (design.md mục 7.2 bước 3–5, 7.3).
    /// <para>
    /// Với mỗi mặt hàng: giá = (giá trị đầu kỳ + giá trị nhập có giá) ÷ (SL đầu kỳ + SL nhập có giá); gán giá cho mọi dòng sổ chưa chốt
    /// (chuyển kho đi và đến cùng giá nên giá trị công ty không đổi); chênh lệch làm tròn dồn vào dòng xuất tiêu hao cuối kỳ để
    /// giá trị tồn cuối = SL tồn cuối × giá. Số dư theo kho × hàng × lô = đầu kỳ (số dư kỳ trước) + nhập − xuất.
    /// </para>
    /// </summary>
    private async Task CalculateAsync(Guid companyId, InventoryPeriod period, InventoryPeriod? previous, CancellationToken cancellationToken)
    {
        var opening = previous is null
            ? []
            : await _db.StockPeriodBalances.AsNoTracking().IgnoreQueryFilters(CompanyWide).Where(b => b.PeriodId == previous.Id).ToListAsync(cancellationToken);
        var previousCosts = previous is null
            ? []
            : await _db.ItemPeriodCosts.AsNoTracking().IgnoreQueryFilters(CompanyWide).Where(c => c.PeriodId == previous.Id)
                .ToDictionaryAsync(c => c.ItemId, c => c.UnitCost, cancellationToken);
        var ledger = await _db.StockLedger.IgnoreQueryFilters(CompanyWide)
            .Where(l => l.PeriodId == period.Id)
            .OrderBy(l => l.PostingDate).ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var itemIds = opening.Select(b => b.ItemId).Concat(ledger.Select(l => l.ItemId)).Distinct();
        foreach (var itemId in itemIds)
        {
            var open = opening.Where(b => b.ItemId == itemId).ToList();
            var rows = ledger.Where(l => l.ItemId == itemId).ToList();
            var openQty = open.Sum(b => b.CloseQty);
            var openAmount = open.Sum(b => b.CloseAmount);
            var pricedRows = rows.Where(l => PeriodCosting.PricedDocTypes.Contains(l.DocType)).ToList();
            var cost = PeriodCosting.UnitCost(
                openQty, openAmount, pricedRows.Sum(l => l.Direction * l.BaseQty), pricedRows.Sum(l => l.Direction * l.Amount),
                previousCosts.GetValueOrDefault(itemId));

            foreach (var row in rows.Where(l => !l.IsCostFinal)) row.ApplyPeriodCost(cost);

            // Chênh lệch làm tròn (design.md mục 7.3): ví dụ tồn đầu 100 × 10.000, nhập 50 × 13.000, trả 10 × 13.000, xuất 120 theo
            // giá 10.857,142857 = 1.302.857,14; tồn cuối 20 phải bằng 20 × 10.857,142857 = 217.142,86, nên dòng xuất cuối
            // được cộng/trừ phần lệch (ở đây 1.520.000 − 1.302.857,14 − 217.142,86 = 0).
            var closeQty = openQty + rows.Sum(l => l.Direction * l.BaseQty);
            var closeAmount = openAmount + rows.Sum(l => l.Direction * l.Amount);
            var target = Math.Round(closeQty * cost, 2, MidpointRounding.AwayFromZero);
            var lastConsumption = rows.LastOrDefault(l =>
                l.Direction < 0 && !PeriodCosting.PricedDocTypes.Contains(l.DocType) && l.DocType != DocumentTypes.Transfer);
            if (closeAmount != target && lastConsumption is not null) lastConsumption.AdjustAmount(lastConsumption.Amount + closeAmount - target);

            _db.ItemPeriodCosts.Add(ItemPeriodCost.Create(companyId, period.Id, itemId, cost, now));

            var keys = open.Select(b => (b.WarehouseId, b.LotNo)).Concat(rows.Select(l => (l.WarehouseId, l.LotNo))).Distinct();
            foreach (var (warehouseId, lotNo) in keys)
            {
                var start = open.Where(b => b.WarehouseId == warehouseId && b.LotNo == lotNo).ToList();
                var moves = rows.Where(l => l.WarehouseId == warehouseId && l.LotNo == lotNo).ToList();
                var balance = StockPeriodBalance.Create(
                    companyId, period.Id, warehouseId, itemId, lotNo,
                    start.Sum(b => b.CloseQty), start.Sum(b => b.CloseAmount),
                    moves.Where(l => l.Direction > 0).Sum(l => l.BaseQty), moves.Where(l => l.Direction > 0).Sum(l => l.Amount),
                    moves.Where(l => l.Direction < 0).Sum(l => l.BaseQty), moves.Where(l => l.Direction < 0).Sum(l => l.Amount));
                // Không chốt kỳ có tồn âm (design.md mục 7.2 bước 1) – sửa chứng từ rồi đóng lại.
                if (balance.CloseQty < 0)
                    throw new BusinessRuleException($"Không đóng được kỳ {period.Label}: tồn cuối kỳ bị âm ({balance.CloseQty:0.####}) – kiểm tra phiếu xuất trong kỳ.");
                if (balance is { OpenQty: 0, OpenAmount: 0, InQty: 0, OutQty: 0, CloseAmount: 0 }) continue;
                _db.StockPeriodBalances.Add(balance);
            }
        }
    }

    /// <summary>
    /// Lý do chưa đóng được kỳ (null = đóng được): kỳ tháng Đang mở; kỳ tháng trước phải đã đóng, trừ khi chưa từng mở (kỳ đầu tiên
    /// công ty dùng hệ thống) – đóng tuần tự (CLAUDE.md mục 4, design.md mục 7.1).
    /// </summary>
    private static string? CloseBlocker(InventoryPeriod period, IReadOnlyList<InventoryPeriod> all)
    {
        if (period.IsYear) return "Chỉ đóng từng kỳ tháng.";
        if (period.Status != PeriodStatus.Open) return $"Kỳ {period.Label} không ở trạng thái Đang mở.";
        var previous = all.SingleOrDefault(p => !p.IsYear && p.StartDate == period.StartDate.AddMonths(-1));
        return previous is { Status: PeriodStatus.Open or PeriodStatus.Costing }
            ? $"Phải đóng kỳ {previous.Label} trước (đóng kỳ tuần tự theo tháng)."
            : null;
    }

    /// <summary>
    /// Lý do chưa mở lại được kỳ (null = mở lại được): chỉ kỳ đã đóng gần nhất – không có kỳ tháng sau nó đã đóng hoặc đang tính giá
    /// (design.md mục 7.4).
    /// </summary>
    private static string? ReopenBlocker(InventoryPeriod period, IReadOnlyList<InventoryPeriod> all)
    {
        if (period.IsYear || period.Status != PeriodStatus.Closed) return $"Kỳ {period.Label} không ở trạng thái Đã đóng.";
        var later = all.Where(p => !p.IsYear && p.StartDate > period.StartDate && p.Status is PeriodStatus.Closed or PeriodStatus.Costing or PeriodStatus.Locked)
            .MaxBy(p => p.StartDate);
        return later is null ? null : $"Phải mở lại kỳ {later.Label} trước (chỉ mở lại kỳ đã đóng gần nhất).";
    }
}
