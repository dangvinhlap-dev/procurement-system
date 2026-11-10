using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Theo dõi chiết khấu doanh số/tài trợ thực nhận theo hợp đồng nguyên tắc (lát F4b – design.md mục 18.21). Hợp đồng đọc qua filter công ty
/// nên chỉ thấy hợp đồng của công ty đang làm việc; ghi nhận/hủy cần quyền sửa hợp đồng và mặt hàng của hợp đồng trong phạm vi nhóm hàng.
/// </summary>
public sealed class ContractRebateService : IContractRebateService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    public ContractRebateService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ContractRebateTrackingDto> GetTrackingAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var contract = await LoadAsync(contractId, cancellationToken);
        var rows = await ContractRebateTracker.TrackAsync(_db, [contract], false, _clock.TodayInVietnam(), cancellationToken);
        var lineNos = contract.Rebates.ToDictionary(r => r.Id, r => r.LineNo);
        var receipts = await (
                from r in _db.ContractRebateReceipts.AsNoTracking()
                where r.ContractId == contractId
                join u in _db.Users on r.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby r.PeriodFrom, r.ReceivedOn, r.CreatedAt
                select new { r, Name = u == null ? null : u.FullName })
            .ToListAsync(cancellationToken);
        var canRecord = contract.Status is ContractStatus.Active or ContractStatus.Terminated
                        && _scope.HasPermission(Permissions.Purchase.ContractManage) && await ItemsInScopeAsync(contract, cancellationToken);
        return new ContractRebateTrackingDto(
            ContractRebateTracker.GraceDays,
            rows.Select(x => new ContractRebatePeriodDto(
                x.Rebate.Id, x.Rebate.LineNo, x.Rebate.Kind.ToString(), x.Rebate.Description, x.From, x.To, x.Due, x.Purchase, x.Expected, x.Received,
                x.Short, x.Status)).ToList(),
            receipts.Select(x => new ContractRebateReceiptDto(
                x.r.Id, x.r.RebateId, lineNos.GetValueOrDefault(x.r.RebateId), x.r.PeriodFrom, x.r.ReceivedOn, x.r.Amount, x.r.Form.ToString(), x.r.RefNo,
                x.r.Note, x.r.IsCancelled, x.r.CancelReason, x.r.CreatedAt, x.Name)).ToList(),
            canRecord);
    }

    /// <inheritdoc />
    public async Task<ContractRebateTrackingDto> RecordAsync(Guid contractId, SaveContractRebateReceiptRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var contract = await LoadAsync(contractId, cancellationToken);
        await EnsureItemsInScopeAsync(contract, cancellationToken);
        var form = Enum.TryParse<RebateReceiptForm>(request.Form, true, out var f) && Enum.IsDefined(f)
            ? f
            : throw new BusinessRuleException("Hình thức nhận không hợp lệ.");
        var receipt = ContractRebateReceipt.Create(
            contract, request.RebateId,
            new ContractRebateReceiptData(request.PeriodFrom, request.ReceivedOn, request.Amount, form, request.RefNo, request.Note),
            _clock.TodayInVietnam());
        _db.ContractRebateReceipts.Add(receipt);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetTrackingAsync(contractId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractRebateTrackingDto> CancelReceiptAsync(Guid receiptId, CancelContractRebateReceiptRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _scope.RequireCompany();
        var receipt = await _db.ContractRebateReceipts.SingleOrDefaultAsync(r => r.Id == receiptId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bản ghi thực nhận.");
        var contract = await LoadAsync(receipt.ContractId, cancellationToken);
        await EnsureItemsInScopeAsync(contract, cancellationToken);
        receipt.Cancel(request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetTrackingAsync(contract.Id, cancellationToken);
    }

    /// <summary>Nạp hợp đồng kèm dòng và cam kết chiết khấu (chỉ đọc); không thấy thì báo lỗi 404.</summary>
    private async Task<PurchaseContract> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.PurchaseContracts.AsNoTracking().Include(c => c.Rebates).Include(c => c.Lines).AsSplitQuery()
                   .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
               ?? throw new NotFoundException("Không tìm thấy hợp đồng.");
    }

    /// <summary>Mặt hàng của hợp đồng đều trong phạm vi nhóm hàng của người dùng (như khi sửa hợp đồng).</summary>
    private async Task<bool> ItemsInScopeAsync(PurchaseContract contract, CancellationToken cancellationToken)
    {
        var ids = contract.Lines.Select(l => l.ItemId).Distinct().ToList();
        return await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) == ids.Count;
    }

    /// <summary>Chặn khi hợp đồng có mặt hàng ngoài phạm vi nhóm hàng của người dùng.</summary>
    private async Task EnsureItemsInScopeAsync(PurchaseContract contract, CancellationToken cancellationToken)
    {
        if (!await ItemsInScopeAsync(contract, cancellationToken))
            throw new ForbiddenAccessException("Hợp đồng có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
    }
}
