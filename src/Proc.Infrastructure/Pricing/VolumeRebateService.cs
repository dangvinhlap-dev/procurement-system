using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Pricing;

/// <summary>
/// Thỏa thuận chiết khấu bậc sản lượng gộp (design.md mục 18.25 – lát G6a). Thỏa thuận thuộc tổng công ty (lập, sửa, duyệt khi đang làm việc
/// ở tổng công ty, qua <see cref="ApprovalEngine"/> theo quy trình loại CKB); công ty thành viên chỉ đọc thỏa thuận hiệu lực áp cho mình và chỉ
/// thấy phần sản lượng/chiết khấu của mình. Lũy kế đọc PO của mọi công ty áp dụng – các truy vấn đó bỏ filter công ty có điều kiện, ghi rõ tại chỗ.
/// </summary>
public sealed class VolumeRebateService : IVolumeRebateService
{
    /// <summary>Thỏa thuận đã hết hạn trong số ngày này vẫn hiện trên màn hình theo dõi (để đối chiếu chiết khấu kỳ cuối với NCC).</summary>
    private const int RecentlyEndedDays = 31;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;
    private readonly ApprovalEngine _approvals;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="clock">Đồng hồ.</param>
    public VolumeRebateService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VolumeRebateListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await (
                from x in Visible().AsNoTracking()
                join c in _db.ItemCategories on x.ItemCategoryId equals c.Id
                join s in _db.Suppliers on x.SupplierId equals s.Id
                orderby x.ValidFrom descending, x.No descending
                select new
                {
                    x.Id, x.No, x.Title, SupplierCode = s.Code, SupplierName = s.Name, CategoryCode = c.Code, CategoryName = c.Name, x.ValidFrom,
                    x.ValidTo, x.Period, x.Basis, Lines = x.Lines.Count, Tiers = x.Tiers.Count, x.Status, x.CreatedBy,
                })
            .Take(2000)
            .ToListAsync(cancellationToken);
        var companies = await CompanyCodesAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        var users = await UserNamesAsync(rows.Select(r => r.CreatedBy), cancellationToken);
        return rows.Select(r => new VolumeRebateListItemDto(
                r.Id, r.No, r.Title, r.SupplierCode, r.SupplierName, r.CategoryCode, r.CategoryName, r.ValidFrom, r.ValidTo, r.Period.ToString(),
                r.Basis.ToString(), string.Join(", ", companies[r.Id]), r.Lines, r.Tiers, r.Status.ToString(),
                r.CreatedBy is { } u ? users.GetValueOrDefault(u) : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<VolumeRebateDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var agreement = await LoadVisibleAsync(id, cancellationToken);
        return await ToDtoAsync(agreement, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VolumeRebateDto> CreateAsync(SaveVolumeRebateRequest request, CancellationToken cancellationToken)
    {
        var header = ToHeader(request);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        var agreement = await NewAsync(header, lines, request.Tiers.Select(t => new VolumeRebateTierData(t.FromValue, t.Percent)).ToList(), cancellationToken);
        return await GetAsync(agreement.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VolumeRebateDto> UpdateAsync(Guid id, SaveVolumeRebateRequest request, CancellationToken cancellationToken)
    {
        var agreement = await FindOwnAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(agreement, cancellationToken);
        var header = ToHeader(request);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        await CheckAsync(header, lines.Select(l => l.Data.ItemId).ToList(), agreement.Id, cancellationToken);
        agreement.Update(header);
        agreement.SetLines(lines);
        agreement.SetTiers(request.Tiers.Select(t => new VolumeRebateTierData(t.FromValue, t.Percent)).ToList());
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VolumeRebateDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var agreement = await FindOwnAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(agreement, cancellationToken);
        // Kiểm tra lại lúc gửi: NCC có thể đã bị tạm ngưng, hoặc đã có thỏa thuận khác trùng từ lúc soạn nháp.
        await CheckAsync(agreement.Header(), agreement.Lines.Select(l => l.ItemId).ToList(), agreement.Id, cancellationToken);
        await _approvals.SubmitAsync(agreement, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VolumeRebateDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var agreement = await FindOwnAsync(id, cancellationToken);
        // Duyệt cấp cuối là cam kết thương mại có tác dụng: NCC vừa bị tạm ngưng/đưa vào danh sách đen thì không cho hiệu lực (mục 5.8).
        await CheckSupplierAsync(agreement.SupplierId, cancellationToken);
        await _approvals.ApproveAsync(agreement, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VolumeRebateDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var agreement = await FindOwnAsync(id, cancellationToken);
        await _approvals.RejectAsync(agreement, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VolumeRebateDto> RecallAsync(Guid id, CancellationToken cancellationToken)
    {
        var agreement = await FindOwnAsync(id, cancellationToken);
        await _approvals.SkipPendingAsync(agreement, cancellationToken);
        agreement.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VolumeRebateDto> CancelAsync(Guid id, CancelVolumeRebateRequest request, CancellationToken cancellationToken)
    {
        var agreement = await FindOwnAsync(id, cancellationToken);
        // Quyền hủy + lý do bắt buộc (design.md mục 20.2a): nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị.
        var reason = DocumentCancelPolicy.Authorize(
            _scope, $"thỏa thuận {agreement.No}", agreement.Status == VolumeRebateStatus.Draft, agreement.CreatedBy, request.Reason);
        agreement.Cancel(_clock.TodayInVietnam(), reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VolumeRebateDto> CopyAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await FindOwnAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(source, cancellationToken);
        // Thời hạn nối tiếp ngay sau thỏa thuận nguồn, cùng độ dài (gia hạn năm sau) – không trùng thời hạn nên không vướng quy tắc chống trùng.
        var length = source.ValidTo.DayNumber - source.ValidFrom.DayNumber;
        var from = source.ValidTo.AddDays(1);
        var header = source.Header() with { ValidFrom = from, ValidTo = from.AddDays(length) };
        var lines = await ResolveLinesAsync(source.CopyLines().Select(l => new SaveVolumeRebateLineRequest(null, l.ItemId, l.UomId, l.Note)).ToList(), cancellationToken);
        var agreement = await NewAsync(header, lines, source.CopyTiers(), cancellationToken);
        return await GetAsync(agreement.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VolumeRebateProgressDto> ProgressAsync(Guid id, decimal? nearPercent, CancellationToken cancellationToken)
    {
        var agreement = await LoadVisibleAsync(id, cancellationToken);
        var near = Near(nearPercent);
        var today = _clock.TodayInVietnam();
        var volumes = await VolumeRebateTracker.VolumesAsync(_db, [agreement], cancellationToken);
        var received = await VolumeRebateTracker.ReceivedAsync(_db, [agreement.Id], cancellationToken);
        var codes = await CompanyCodeMapAsync(agreement.Companies.Select(c => c.CompanyId), cancellationToken);
        var periods = agreement.Periods().Select(p => ToPeriodDto(agreement, p, volumes, received, today, near, codes)).ToList();
        var canRecord = agreement.Status == VolumeRebateStatus.Active && agreement.CompanyId == _scope.RequireCompany()
                        && _scope.HasPermission(Permissions.Pricing.VolumeRebateManage) && await ItemsVisibleAsync(agreement, cancellationToken);
        return new VolumeRebateProgressDto(
            agreement.Id, agreement.Basis.ToString(), await UomCodeAsync(agreement, cancellationToken), periods, RebateSettlement.GraceDays, canRecord,
            await ReceiptsAsync(agreement, null, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<VolumeRebateReceiptDto> RecordReceiptAsync(Guid id, SaveVolumeRebateReceiptRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var agreement = await FindOwnAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(agreement, cancellationToken);
        var form = Enum.TryParse<RebateReceiptForm>(request.Form, true, out var f) && Enum.IsDefined(f)
            ? f
            : throw new BusinessRuleException("Hình thức nhận không hợp lệ.");
        var period = agreement.Periods().FirstOrDefault(p => p.From == request.PeriodFrom);
        // Doanh số kỳ tính lúc ghi để chia phần thực nhận; kỳ không thuộc thỏa thuận thì domain báo lỗi (để trống doanh số).
        var volumes = period == default
            ? []
            : VolumeRebateTracker.PeriodVolumes(
                agreement, period.From, period.To, await VolumeRebateTracker.VolumesAsync(_db, [agreement], cancellationToken),
                await CompanyCodeMapAsync(agreement.Companies.Select(c => c.CompanyId), cancellationToken));
        var receipt = VolumeRebateReceipt.Create(
            agreement, new VolumeRebateReceiptData(request.PeriodFrom, request.ReceivedOn, request.Amount, form, request.RefNo, request.Note),
            volumes, _clock.TodayInVietnam());
        _db.VolumeRebateReceipts.Add(receipt);
        await _db.SaveChangesAsync(cancellationToken);
        return (await ReceiptsAsync(agreement, receipt.Id, cancellationToken)).Single();
    }

    /// <inheritdoc />
    public async Task<VolumeRebateReceiptDto> CancelReceiptAsync(Guid receiptId, CancelVolumeRebateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _scope.RequireCompany();
        // Filter công ty giữ nguyên: bản ghi thuộc tổng công ty, công ty áp dụng không hủy được.
        var receipt = await _db.VolumeRebateReceipts.SingleOrDefaultAsync(r => r.Id == receiptId, cancellationToken)
                      ?? throw new NotFoundException("Không tìm thấy bản ghi thực nhận.");
        var agreement = await FindOwnAsync(receipt.AgreementId, cancellationToken);
        await EnsureItemsVisibleAsync(agreement, cancellationToken);
        receipt.Cancel(request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return (await ReceiptsAsync(agreement, receipt.Id, cancellationToken)).Single();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VolumeRebateTrackingRowDto>> TrackingAsync(decimal? nearPercent, bool onlyAlerts, CancellationToken cancellationToken)
    {
        var near = Near(nearPercent);
        var today = _clock.TodayInVietnam();
        var since = today.AddDays(-RecentlyEndedDays);
        var agreements = await Visible().AsNoTracking().Include(x => x.Lines).Include(x => x.Tiers).Include(x => x.Companies).AsSplitQuery()
            .Where(x => x.Status == VolumeRebateStatus.Active && x.ValidTo >= since)
            .OrderBy(x => x.No)
            .ToListAsync(cancellationToken);
        if (agreements.Count == 0) return [];
        var volumes = await VolumeRebateTracker.VolumesAsync(_db, agreements, cancellationToken);
        var received = await VolumeRebateTracker.ReceivedAsync(_db, agreements.Select(a => a.Id).ToList(), cancellationToken);
        var codes = await CompanyCodeMapAsync(agreements.SelectMany(a => a.Companies.Select(c => c.CompanyId)), cancellationToken);
        var supplierIds = agreements.Select(a => a.SupplierId).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => new { s.Code, s.Name }, cancellationToken);
        var uomIds = agreements.Select(a => a.Lines.FirstOrDefault()?.UomId).OfType<Guid>().Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);

        var rows = new List<VolumeRebateTrackingRowDto>();
        foreach (var a in agreements)
        {
            var periods = a.Periods();
            // Kỳ hiện tại = kỳ chứa hôm nay; chưa tới hiệu lực thì kỳ đầu; đã hết hạn thì kỳ cuối (đối chiếu chiết khấu với NCC).
            var current = periods.FirstOrDefault(p => p.From <= today && today <= p.To);
            if (current == default) current = today < a.ValidFrom ? periods[0] : periods[^1];
            var period = ToPeriodDto(a, current, volumes, received, today, near, codes);
            // Kỳ đã qua hạn ghi nhận mà còn thiếu thực nhận (lát G6b) – cảnh báo cả khi kỳ hiện tại đang chạy bình thường.
            var unreceived = periods.Where(p => p.To < today)
                .Select(p => p == current ? period : ToPeriodDto(a, p, volumes, received, today, near, codes))
                .Where(p => p.ReceiptStatus == RebateSettlement.Short)
                .ToList();
            if (onlyAlerts && !(period.IsNearNextTier || period.TargetStatus is "AtRisk" or "Missed" || unreceived.Count > 0)) continue;
            var supplier = suppliers.GetValueOrDefault(a.SupplierId);
            var uom = a.Basis == VolumeRebateBasis.Quantity && a.Lines.Count > 0 ? uoms.GetValueOrDefault(a.Lines[0].UomId) : null;
            rows.Add(new VolumeRebateTrackingRowDto(
                a.Id, a.No, a.Title, supplier?.Code ?? "", supplier?.Name ?? "", a.Basis.ToString(), uom, a.TargetTierNo, period, unreceived.Count,
                unreceived.Sum(p => p.ShortAmount)));
        }

        return rows;
    }

    /// <summary>
    /// Thỏa thuận đọc được ở công ty đang làm việc: của công ty mình (tổng công ty thấy mọi thỏa thuận nó lập) cộng thỏa thuận Hiệu lực áp cho
    /// công ty mình. Bỏ filter công ty ở nhánh thứ hai vì thỏa thuận thuộc tổng công ty nhưng chiết khấu là quyền lợi của công ty áp dụng
    /// (design.md 18.25); nháp, chờ duyệt, đã hủy thì công ty thành viên không thấy. Filter nhóm hàng giữ nguyên.
    /// </summary>
    private IQueryable<VolumeRebateAgreement> Visible()
    {
        var companyId = _scope.RequireCompany();
        return _db.VolumeRebateAgreements.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(x => x.CompanyId == companyId
                        || (x.Status == VolumeRebateStatus.Active && x.Companies.Any(c => c.CompanyId == companyId)));
    }

    /// <summary>Nạp thỏa thuận chiết khấu bậc mà công ty đang làm việc được xem (kèm dòng, bậc, công ty áp dụng).</summary>
    private async Task<VolumeRebateAgreement> LoadVisibleAsync(Guid id, CancellationToken cancellationToken) =>
        await Visible().AsNoTracking().Include(x => x.Lines).Include(x => x.Tiers).Include(x => x.Companies).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy thỏa thuận chiết khấu sản lượng gộp.");

    /// <summary>Cấp số và lưu thỏa thuận nháp trong một transaction (số không bị nhảy khi lưu lỗi). Chỉ tổng công ty lập.</summary>
    private async Task<VolumeRebateAgreement> NewAsync(
        VolumeRebateHeader header, List<(VolumeRebateLineData Data, Item Item)> lines, IReadOnlyList<VolumeRebateTierData> tiers,
        CancellationToken cancellationToken)
    {
        var companyId = await RequireHeadOfficeAsync(cancellationToken);
        await CheckAsync(header, lines.Select(l => l.Data.ItemId).ToList(), null, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.VolumeRebate, header.ValidFrom, cancellationToken);
        var agreement = VolumeRebateAgreement.Create(companyId, no, header);
        agreement.SetLines(lines);
        agreement.SetTiers(tiers);
        _db.VolumeRebateAgreements.Add(agreement);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return agreement;
    }

    /// <summary>
    /// Quy tắc của thỏa thuận (design.md 18.25, G6a): nhóm hàng trong phạm vi; công ty áp dụng đang hoạt động; mặt hàng thuộc nhóm hàng (hoặc
    /// nhóm con); NCC Đã duyệt; không trùng NCC × mặt hàng × công ty với thỏa thuận khác còn hiệu lực (nháp, chờ duyệt, hiệu lực) có thời hạn
    /// giao nhau – một lượng mua không được tính bậc hai lần.
    /// </summary>
    private async Task CheckAsync(VolumeRebateHeader header, IReadOnlyList<Guid> itemIds, Guid? exceptId, CancellationToken cancellationToken)
    {
        var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
        if (!parents.ContainsKey(header.ItemCategoryId))
            throw new BusinessRuleException("Nhóm hàng không tồn tại hoặc ngoài phạm vi nhóm hàng của bạn.");
        var companyIds = header.CompanyIds.Distinct().ToList();
        var activeCompanies = await _db.Companies.AsNoTracking().CountAsync(c => companyIds.Contains(c.Id) && c.IsActive, cancellationToken);
        if (activeCompanies < companyIds.Count) throw new BusinessRuleException("Có công ty áp dụng không tồn tại hoặc đã ngừng dùng.");
        var ids = itemIds.Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => ids.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.CategoryId }).ToListAsync(cancellationToken);
        var outside = items.FirstOrDefault(i => !ItemAdministration.IsUnder(i.CategoryId, header.ItemCategoryId, parents));
        if (outside is not null) throw new BusinessRuleException($"Mặt hàng {outside.Code} không thuộc nhóm hàng của thỏa thuận.");
        await CheckSupplierAsync(header.SupplierId, cancellationToken);

        // Bỏ filter nhóm hàng khi dò trùng: thỏa thuận nhóm hàng khác (người lập không được xem) vẫn có thể chứa cùng mặt hàng ở nhóm con.
        var conflict = await _db.VolumeRebateAgreements.AsNoTracking().IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter])
            .Where(x => x.Id != exceptId && x.SupplierId == header.SupplierId
                        && (x.Status == VolumeRebateStatus.Draft || x.Status == VolumeRebateStatus.Submitted || x.Status == VolumeRebateStatus.Active)
                        && x.ValidFrom <= header.ValidTo && header.ValidFrom <= x.ValidTo
                        && x.Companies.Any(c => companyIds.Contains(c.CompanyId)))
            .SelectMany(x => x.Lines.Where(l => ids.Contains(l.ItemId)).Select(l => new { x.No, l.ItemId }))
            .FirstOrDefaultAsync(cancellationToken);
        if (conflict is not null)
        {
            var code = items.FirstOrDefault(i => i.Id == conflict.ItemId)?.Code;
            throw new BusinessRuleException(
                $"Mặt hàng {code} đã có trong thỏa thuận {conflict.No} cùng NCC, cùng công ty, thời hạn giao nhau – sửa thỏa thuận đó hoặc hủy trước.");
        }
    }

    /// <summary>NCC phải Đã duyệt – Ban chỉ thỏa thuận với NCC trong danh sách đủ điều kiện (design.md 18.25, G1).</summary>
    private async Task CheckSupplierAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == supplierId).Select(s => new { s.Code, s.Status }).SingleOrDefaultAsync(cancellationToken)
                       ?? throw new BusinessRuleException("Nhà cung cấp không tồn tại.");
        if (supplier.Status != SupplierStatus.Approved)
            throw new BusinessRuleException($"NCC {supplier.Code} chưa ở trạng thái Đã duyệt – không lập thỏa thuận chiết khấu được.");
    }

    /// <summary>
    /// Tính một kỳ: gom sản lượng từng công ty (<see cref="VolumeRebateTracker.PeriodVolumes"/>), gọi <see cref="VolumeRebateCalculation.Evaluate"/>,
    /// ghép thực nhận (lát G6b): phải nhận = chiết khấu gộp của bậc đạt, tình trạng theo <see cref="RebateSettlement.StatusOf"/>. Ở công ty
    /// thành viên chỉ trả phần của công ty mình (tổng gộp vẫn hiện) – doanh số từng pháp nhân khác là thông tin riêng của họ.
    /// </summary>
    private VolumeRebatePeriodDto ToPeriodDto(
        VolumeRebateAgreement a, (DateOnly From, DateOnly To) p, IReadOnlyList<VolumeRebateTracker.VolumeRow> rows,
        VolumeRebateTracker.Received received, DateOnly today, decimal near, IReadOnlyDictionary<Guid, string> codes)
    {
        var volumes = VolumeRebateTracker.PeriodVolumes(a, p.From, p.To, rows, codes);
        var r = a.Evaluate(p.From, p.To, volumes, today, near);
        var companyId = _scope.RequireCompany();
        var shares = r.Shares.Where(s => a.CompanyId == companyId || s.CompanyId == companyId)
            .Select(s => new VolumeRebateCompanyShareDto(
                s.CompanyId, codes.GetValueOrDefault(s.CompanyId) ?? "", s.Quantity, s.Amount, s.SharePercent, s.Rebate,
                received.ByCompany.GetValueOrDefault((a.Id, p.From, s.CompanyId))))
            .ToList();
        var got = received.Total.GetValueOrDefault((a.Id, p.From));
        return new VolumeRebatePeriodDto(
            r.From, r.To, r.State, r.Volume, r.Amount, r.Tier?.TierNo, r.Tier?.Percent ?? 0m, r.Rebate, r.Next?.TierNo, r.Next?.FromValue, r.Shortfall,
            r.Forecast, r.Target?.FromValue, r.TargetStatus, r.IsNearNextTier, shares, RebateSettlement.DueDate(p.To), got, Math.Max(0m, r.Rebate - got),
            RebateSettlement.StatusOf(today, p.To, r.Rebate, got));
    }

    /// <summary>
    /// Các lần ghi nhận thực nhận của thỏa thuận (hoặc chỉ bản ghi <paramref name="onlyId"/>). Bỏ filter công ty: bản ghi thuộc tổng công ty,
    /// công ty áp dụng đã được xem thỏa thuận (người gọi kiểm tra) nhưng chỉ thấy phần chia của mình.
    /// </summary>
    private async Task<List<VolumeRebateReceiptDto>> ReceiptsAsync(VolumeRebateAgreement a, Guid? onlyId, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var receipts = await _db.VolumeRebateReceipts.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking().Include(r => r.Shares)
            .Where(r => r.AgreementId == a.Id && (onlyId == null || r.Id == onlyId))
            .OrderBy(r => r.PeriodFrom).ThenBy(r => r.ReceivedOn).ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
        var codes = await CompanyCodeMapAsync(receipts.SelectMany(r => r.Shares.Select(s => s.CompanyId)), cancellationToken);
        var users = await UserNamesAsync(receipts.Select(r => r.CreatedBy), cancellationToken);
        var ends = a.Periods().ToDictionary(p => p.From, p => p.To);
        return receipts.Select(r => new VolumeRebateReceiptDto(
                r.Id, r.AgreementId, r.PeriodFrom, ends.GetValueOrDefault(r.PeriodFrom, r.PeriodFrom), r.ReceivedOn, r.Amount, r.Form.ToString(), r.RefNo,
                r.Note, r.IsCancelled, r.CancelReason, r.CreatedAt, r.CreatedBy is { } u ? users.GetValueOrDefault(u) : null,
                r.Shares.Where(s => a.CompanyId == companyId || s.CompanyId == companyId)
                    .OrderBy(s => codes.GetValueOrDefault(s.CompanyId), StringComparer.Ordinal)
                    .Select(s => new VolumeRebateReceiptShareDto(s.CompanyId, codes.GetValueOrDefault(s.CompanyId) ?? "", s.PurchaseAmount, s.SharePercent, s.Amount))
                    .ToList()))
            .ToList();
    }

    /// <summary>Ngưỡng "sắp đạt bậc" (%) – không truyền thì dùng mặc định, giới hạn trong khoảng hợp lệ.</summary>
    private static decimal Near(decimal? nearPercent) => Math.Clamp(nearPercent ?? VolumeRebateCalculation.DefaultNearPercent, 1m, 50m);

    /// <summary>ĐVT tính sản lượng của thỏa thuận (chỉ khi tính theo số lượng).</summary>
    private async Task<string?> UomCodeAsync(VolumeRebateAgreement a, CancellationToken cancellationToken)
    {
        if (a.Basis != VolumeRebateBasis.Quantity || a.Lines.Count == 0) return null;
        var uomId = a.Lines[0].UomId;
        return await _db.Uoms.AsNoTracking().Where(u => u.Id == uomId).Select(u => u.Code).SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>Bắt buộc đang làm việc ở tổng công ty (thỏa thuận chiết khấu sản lượng do tổng công ty lập).</summary>
    private async Task<Guid> RequireHeadOfficeAsync(CancellationToken cancellationToken)
    {
        if (!await HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken))
            throw new ForbiddenAccessException("Thỏa thuận chiết khấu sản lượng gộp chỉ lập khi đang làm việc ở tổng công ty.");
        return _scope.RequireCompany();
    }

    /// <summary>Thỏa thuận của công ty đang làm việc (tổng công ty) để thao tác; thấy được nhưng của công ty khác thì báo chỉ xem.</summary>
    private async Task<VolumeRebateAgreement> FindOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var agreement = await _db.VolumeRebateAgreements.Include(x => x.Lines).Include(x => x.Tiers).Include(x => x.Companies).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (agreement is not null) return agreement;
        if (await Visible().AnyAsync(x => x.Id == id, cancellationToken))
            throw new ForbiddenAccessException("Thỏa thuận do tổng công ty lập – công ty áp dụng chỉ xem.");
        throw new NotFoundException("Không tìm thấy thỏa thuận chiết khấu sản lượng gộp.");
    }

    private async Task<List<(VolumeRebateLineData Data, Item Item)>> ResolveLinesAsync(
        IReadOnlyList<SaveVolumeRebateLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return lines.Select((l, index) => (
                new VolumeRebateLineData(l.Id, l.ItemId, l.UomId, l.Note),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
    }

    /// <summary>Mọi mặt hàng của thỏa thuận có nằm trong phạm vi nhóm hàng của người dùng không.</summary>
    private async Task<bool> ItemsVisibleAsync(VolumeRebateAgreement agreement, CancellationToken cancellationToken)
    {
        var ids = agreement.Lines.Select(l => l.ItemId).Distinct().ToList();
        return await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) == ids.Count;
    }

    /// <summary>Chặn khi thỏa thuận có mặt hàng ngoài phạm vi nhóm hàng của người dùng.</summary>
    private async Task EnsureItemsVisibleAsync(VolumeRebateAgreement agreement, CancellationToken cancellationToken)
    {
        if (!await ItemsVisibleAsync(agreement, cancellationToken))
            throw new ForbiddenAccessException("Thỏa thuận có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
    }

    /// <summary>Lấy mã các công ty áp dụng của từng thỏa thuận.</summary>
    private async Task<ILookup<Guid, string>> CompanyCodesAsync(IReadOnlyList<Guid> agreementIds, CancellationToken cancellationToken) =>
        (await (
                from a in _db.VolumeRebateCompanies.AsNoTracking()
                where agreementIds.Contains(a.AgreementId)
                join c in _db.Companies on a.CompanyId equals c.Id
                orderby c.Code
                select new { a.AgreementId, c.Code })
            .ToListAsync(cancellationToken))
        .ToLookup(x => x.AgreementId, x => x.Code);

    /// <summary>Lấy mã công ty theo danh sách Id.</summary>
    private async Task<Dictionary<Guid, string>> CompanyCodeMapAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await _db.Companies.AsNoTracking().Where(c => list.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
    }

    /// <summary>Lấy họ tên theo danh sách Id người dùng (bỏ null, bỏ trùng).</summary>
    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    /// <summary>Đổi thỏa thuận chiết khấu bậc sang DTO kèm mặt hàng, bậc, công ty áp dụng, người lập/duyệt.</summary>
    private async Task<VolumeRebateDto> ToDtoAsync(VolumeRebateAgreement x, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var itemIds = x.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = x.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == x.SupplierId).Select(s => new { s.Code, s.Name }).SingleOrDefaultAsync(cancellationToken);
        var category = await _db.ItemCategories.AsNoTracking().Where(c => c.Id == x.ItemCategoryId).Select(c => new { c.Code, c.Name }).SingleOrDefaultAsync(cancellationToken);
        var appliedIds = x.Companies.Select(c => c.CompanyId).ToList();
        var companies = await _db.Companies.AsNoTracking().Where(c => appliedIds.Contains(c.Id) || c.Id == x.CompanyId)
            .Select(c => new { c.Id, c.Code, c.Name, c.IsHeadOffice }).ToListAsync(cancellationToken);
        var users = await UserNamesAsync([x.CreatedBy], cancellationToken);

        var own = x.CompanyId == companyId;
        var canManage = own && _scope.HasPermission(Permissions.Pricing.VolumeRebateManage);
        string? readOnly = !own ? "Thỏa thuận do tổng công ty lập, Ban cung ứng đã duyệt – công ty áp dụng chỉ xem."
            : items.Count < itemIds.Count ? "Thỏa thuận có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !canManage ? "Bạn không có quyền lập, sửa thỏa thuận chiết khấu sản lượng gộp." : null;
        var manageable = readOnly is null;
        var canApprove = own && x.IsAwaitingApproval
            && await _approvals.ApproveBlockerAsync(x, await _approvals.CurrentStepAsync(x, cancellationToken), cancellationToken) is null;
        var actions = new VolumeRebateActions(
            manageable && x.Status == VolumeRebateStatus.Draft,
            canApprove,
            manageable && x.Status == VolumeRebateStatus.Submitted,
            // Hủy: nháp – người lập; còn lại – chỉ quản trị (design.md mục 20.2a).
            manageable && x.Status is VolumeRebateStatus.Draft or VolumeRebateStatus.Active
                && DocumentCancelPolicy.CanCancel(_scope, x.Status == VolumeRebateStatus.Draft, x.CreatedBy),
            manageable && x.Status != VolumeRebateStatus.Submitted,
            readOnly);

        var lines = x.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            return new VolumeRebateLineDto(l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId,
                uoms.GetValueOrDefault(l.UomId) ?? "", l.Note);
        }).ToList();
        var tiers = x.Tiers.OrderBy(t => t.TierNo).Select(t => new VolumeRebateTierDto(t.TierNo, t.FromValue, t.Percent)).ToList();
        var approvals = await _approvals.HistoryAsync(x.DocType, x.Id, cancellationToken, x);
        return new VolumeRebateDto(
            x.Id, x.No, companies.FirstOrDefault(c => c.Id == x.CompanyId)?.Code ?? "", x.Title, x.SupplierId, supplier?.Code ?? "", supplier?.Name ?? "",
            x.ItemCategoryId, category?.Code ?? "", category?.Name ?? "", x.ValidFrom, x.ValidTo, x.Period.ToString(), x.Basis.ToString(), x.TargetTierNo,
            companies.Where(c => appliedIds.Contains(c.Id)).OrderByDescending(c => c.IsHeadOffice).ThenBy(c => c.Code)
                .Select(c => new VolumeRebateCompanyDto(c.Id, c.Code, c.Name)).ToList(),
            x.Note, x.Status.ToString(), x.RejectReason, x.ApprovedAt, x.CancelledOn, x.CancelReason, x.CreatedAt,
            x.CreatedBy is { } u ? users.GetValueOrDefault(u) : null, lines, tiers, approvals, actions);
    }

    /// <summary>Kỳ, cơ sở gửi lên dạng chữ (Month/Quarter/Year/Whole, Quantity/Amount).</summary>
    private static VolumeRebateHeader ToHeader(SaveVolumeRebateRequest r)
    {
        if (!Enum.TryParse<VolumeRebatePeriod>(r.Period, true, out var period) || !Enum.IsDefined(period))
            throw new BusinessRuleException("Kỳ tính bậc không hợp lệ.");
        if (!Enum.TryParse<VolumeRebateBasis>(r.Basis, true, out var basis) || !Enum.IsDefined(basis))
            throw new BusinessRuleException("Cơ sở tính bậc không hợp lệ.");
        return new VolumeRebateHeader(r.Title, r.SupplierId, r.ItemCategoryId, r.ValidFrom, r.ValidTo, period, basis, r.TargetTierNo, r.CompanyIds, r.Note);
    }
}
