using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Phụ lục hợp đồng nguyên tắc (lát B4b – design.md mục 18.7): gia hạn, đổi bảng giá dùng chung, đổi điều khoản/hạn mức của hợp đồng đang
/// hiệu lực. Duyệt qua <see cref="ApprovalEngine"/> theo quy trình loại CTA, xét giá trị dự kiến sau phụ lục; duyệt cấp cuối thì áp vào hợp
/// đồng trong cùng lần lưu (cùng transaction) – hợp đồng không bao giờ ở trạng thái "đã duyệt phụ lục mà chưa đổi".
/// </summary>
public sealed partial class ContractService : IContractAmendmentService
{
    /// <summary>Kết quả tính phụ lục: giá mới theo Id dòng hợp đồng, giá trị dự kiến sau phụ lục, tóm tắt thay đổi.</summary>
    private sealed record AmendmentEvaluation(Dictionary<Guid, ContractLinePrice> LinePrices, decimal ExpectedAmount, string Summary);

    /// <inheritdoc />
    public async Task<ContractAmendmentDto> GetAmendmentAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var amendment = await _db.PurchaseContractAmendments.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phụ lục hợp đồng.");
        var contract = await Query().AsNoTracking().SingleAsync(c => c.Id == amendment.ContractId, cancellationToken);
        return await ToAmendmentDtoAsync(amendment, contract, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractAmendmentDto> CreateAmendmentAsync(Guid contractId, SaveContractAmendmentRequest request, CancellationToken cancellationToken)
    {
        var contract = await LoadAsync(contractId, cancellationToken);
        // Mỗi lúc một phụ lục đang mở: hai phụ lục cùng chụp giá trị hiện hành rồi lần lượt áp sẽ đè nhau. Chỉ mục duy nhất trong CSDL
        // chặn thêm trường hợp hai người lập cùng lúc.
        if (await _db.PurchaseContractAmendments.AnyAsync(
                a => a.ContractId == contractId && (a.Status == ContractAmendmentStatus.Draft || a.Status == ContractAmendmentStatus.Submitted), cancellationToken))
            throw new BusinessRuleException($"Hợp đồng {contract.No} đang có phụ lục chưa duyệt xong – hoàn tất hoặc hủy phụ lục đó trước.");
        var priceList = await AmendmentPriceListAsync(contract, request.NewPriceListId, cancellationToken);
        var seq = await _db.PurchaseContractAmendments.CountAsync(a => a.ContractId == contractId, cancellationToken) + 1;
        var amendment = PurchaseContractAmendment.Create(contract, seq, ToAmendmentData(request), _clock.TodayInVietnam());
        var evaluation = await EvaluateAsync(amendment, contract, priceList, cancellationToken);
        amendment.SetEvaluation(evaluation.ExpectedAmount, evaluation.Summary);
        _db.PurchaseContractAmendments.Add(amendment);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAmendmentAsync(amendment.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractAmendmentDto> UpdateAmendmentAsync(Guid id, SaveContractAmendmentRequest request, CancellationToken cancellationToken)
    {
        var (amendment, contract) = await LoadAmendmentAsync(id, cancellationToken);
        var priceList = await AmendmentPriceListAsync(contract, request.NewPriceListId, cancellationToken);
        amendment.Update(contract, ToAmendmentData(request), _clock.TodayInVietnam());
        var evaluation = await EvaluateAsync(amendment, contract, priceList, cancellationToken);
        amendment.SetEvaluation(evaluation.ExpectedAmount, evaluation.Summary);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAmendmentAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractAmendmentDto> SubmitAmendmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var (amendment, contract) = await LoadAmendmentAsync(id, cancellationToken);
        // Tính lại lúc gửi: giá trong bảng giá có thể đã đổi từ lúc lưu nháp – ngưỡng duyệt xét theo giá trị mới nhất.
        amendment.EnsureChanges(contract);
        var priceList = await AmendmentPriceListAsync(contract, amendment.NewPriceListId, cancellationToken);
        var evaluation = await EvaluateAsync(amendment, contract, priceList, cancellationToken);
        await CheckAmendmentReadyAsync(amendment, contract, priceList, cancellationToken);
        amendment.SetEvaluation(evaluation.ExpectedAmount, evaluation.Summary);
        await _approvals.SubmitAsync(amendment, cancellationToken);
        // Giá trị dưới mọi ngưỡng đã cấu hình của loại CTA thì phụ lục được duyệt ngay khi gửi → áp luôn vào hợp đồng.
        if (amendment.Status == ContractAmendmentStatus.Approved) contract.ApplyAmendment(amendment, evaluation.LinePrices);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAmendmentAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractAmendmentDto> ApproveAmendmentAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var (amendment, contract) = await LoadAmendmentAsync(id, cancellationToken);
        // Kiểm tra lại lúc duyệt: NCC, bảng giá, hợp đồng trùng thời gian có thể đã đổi từ lúc gửi. Giá trị xét ngưỡng giữ như lúc gửi.
        var priceList = await AmendmentPriceListAsync(contract, amendment.NewPriceListId, cancellationToken);
        var evaluation = await EvaluateAsync(amendment, contract, priceList, cancellationToken);
        await CheckAmendmentReadyAsync(amendment, contract, priceList, cancellationToken);
        await _approvals.ApproveAsync(amendment, request.Comment, cancellationToken);
        if (amendment.Status == ContractAmendmentStatus.Approved) contract.ApplyAmendment(amendment, evaluation.LinePrices);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAmendmentAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractAmendmentDto> RejectAmendmentAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var (amendment, _) = await LoadAmendmentAsync(id, cancellationToken);
        await _approvals.RejectAsync(amendment, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAmendmentAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractAmendmentDto> RecallAmendmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var (amendment, _) = await LoadAmendmentAsync(id, cancellationToken);
        await _approvals.SkipPendingAsync(amendment, cancellationToken);
        amendment.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAmendmentAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractAmendmentDto> CancelAmendmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var (amendment, _) = await LoadAmendmentAsync(id, cancellationToken);
        amendment.Cancel();
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAmendmentAsync(id, cancellationToken);
    }

    /// <summary>Nạp phụ lục (filter công ty tự áp) cùng hợp đồng của nó (kiểm tra phạm vi nhóm hàng như hợp đồng).</summary>
    private async Task<(PurchaseContractAmendment Amendment, PurchaseContract Contract)> LoadAmendmentAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var amendment = await _db.PurchaseContractAmendments.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phụ lục hợp đồng.");
        return (amendment, await LoadAsync(amendment.ContractId, cancellationToken));
    }

    /// <summary>
    /// Bảng giá sau phụ lục: giữ bảng hiện hành thì đọc chính bảng đó; đổi bảng thì phải là bảng dùng được của công ty (như lúc lập hợp
    /// đồng) và cùng NCC với hợp đồng.
    /// </summary>
    private async Task<SupplierPriceList> AmendmentPriceListAsync(PurchaseContract contract, Guid priceListId, CancellationToken cancellationToken)
    {
        var source = priceListId == contract.PriceListId
            ? _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            : UsablePriceLists();
        var priceList = await source.AsNoTracking().Include(p => p.Lines).SingleOrDefaultAsync(p => p.Id == priceListId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bảng giá (hoặc bảng giá đã ngừng / chưa hiệu lực).");
        if (priceList.SupplierId != contract.SupplierId) throw new BusinessRuleException($"Bảng giá {priceList.No} không phải của NCC của hợp đồng {contract.No}.");
        return priceList;
    }

    /// <summary>
    /// Tính phụ lục. Giữ bảng giá: giá dòng giữ nguyên. Đổi bảng giá: giá mỗi dòng lấy bậc có SL tối thiểu nhỏ nhất của cùng mặt hàng × ĐVT
    /// trong bảng mới (như lúc lập hợp đồng); thiếu mặt hàng thì chặn. Giá trị dự kiến = Σ SL × đơn giá × (100 − %CK)/100. Ví dụ 1.000 cái,
    /// bảng mới 48.000 CK 5% → 45.600.000 (trước 47.500.000).
    /// </summary>
    private async Task<AmendmentEvaluation> EvaluateAsync(
        PurchaseContractAmendment amendment, PurchaseContract contract, SupplierPriceList priceList, CancellationToken cancellationToken)
    {
        var prices = new Dictionary<Guid, ContractLinePrice>();
        var parts = new List<string>();
        if (amendment.NewValidTo != contract.ValidTo)
            parts.Add(amendment.NewValidTo > contract.ValidTo
                ? $"Gia hạn đến {Date(amendment.NewValidTo)} (trước: {Date(contract.ValidTo)})"
                : $"Rút ngắn hiệu lực đến {Date(amendment.NewValidTo)} (trước: {Date(contract.ValidTo)})");
        if (amendment.NewPriceListId != contract.PriceListId)
        {
            var itemIds = contract.Lines.Select(l => l.ItemId).Distinct().ToList();
            var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.Code, cancellationToken);
            foreach (var line in contract.Lines.OrderBy(l => l.LineNo))
            {
                var tier = priceList.Lines.Where(p => p.ItemId == line.ItemId && p.UomId == line.UomId).MinBy(p => p.MinQty)
                    ?? throw new BusinessRuleException(
                        $"Dòng {line.LineNo}: mặt hàng {items.GetValueOrDefault(line.ItemId)} (theo ĐVT của hợp đồng) không có trong bảng giá {priceList.No} – bảng giá mới phải có đủ mặt hàng của hợp đồng.");
                prices[line.Id] = new ContractLinePrice(tier.UnitPrice, tier.DiscountPct);
            }

            var oldNo = await _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
                .Where(p => p.Id == contract.PriceListId).Select(p => p.No).SingleAsync(cancellationToken);
            parts.Add($"Đổi bảng giá {oldNo} → {priceList.No}");
        }

        var terms = amendment.NewTerms.DifferencesFrom(contract.Terms);
        if (terms.Count > 0) parts.Add($"Đổi {string.Join(", ", terms)}");
        var expected = prices.Count == 0
            ? contract.ExpectedAmount
            : contract.Lines.Sum(l => PurchaseContractLine.AmountOf(l.ExpectedQty, prices[l.Id]));
        return new AmendmentEvaluation(prices, expected, string.Join("; ", parts));
    }

    /// <summary>
    /// Điều kiện gửi/duyệt phụ lục: hợp đồng còn hiệu lực; NCC đã duyệt (design.md mục 5.8); bảng giá mới (nếu đổi) phải đang hiệu lực – phụ
    /// lục áp ngay khi duyệt xong nên không chờ duyệt bảng giá sau như hợp đồng mới; gia hạn không được chồng lên hợp đồng khác (chờ
    /// duyệt/hiệu lực) của cùng công ty × NCC – mỗi pháp nhân một hợp đồng với NCC trong một thời kỳ (phương án 2).
    /// </summary>
    private async Task CheckAmendmentReadyAsync(
        PurchaseContractAmendment amendment, PurchaseContract contract, SupplierPriceList priceList, CancellationToken cancellationToken)
    {
        if (contract.Status != ContractStatus.Active)
            throw new BusinessRuleException($"Hợp đồng {contract.No} không còn hiệu lực – phụ lục không áp được.");
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == contract.SupplierId).Select(s => new { s.Code, s.Status }).SingleAsync(cancellationToken);
        if (supplier.Status != SupplierStatus.Approved)
            throw new BusinessRuleException($"NCC {supplier.Code} chưa ở trạng thái Đã duyệt – chưa ký phụ lục được.");
        if (amendment.NewPriceListId != contract.PriceListId && priceList.Status != PriceListStatus.Active)
            throw new BusinessRuleException($"Bảng giá {priceList.No} chưa hiệu lực – duyệt bảng giá trước khi gửi phụ lục đổi bảng giá.");
        if (amendment.NewValidTo > contract.ValidTo && await OverlappingContractAsync(contract, contract.ValidFrom, amendment.NewValidTo, cancellationToken) is { } overlap)
            throw new BusinessRuleException($"Gia hạn trùng thời gian với hợp đồng {overlap} của cùng NCC – mỗi công ty một hợp đồng với NCC trong một thời kỳ.");
    }

    private async Task<ContractAmendmentDto> ToAmendmentDtoAsync(PurchaseContractAmendment a, PurchaseContract contract, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == contract.SupplierId).Select(s => new { s.Code, s.Name }).SingleAsync(cancellationToken);
        // Bảng giá đọc bỏ filter công ty: bảng toàn tập đoàn thuộc tổng công ty; phụ lục đã được nạp qua phạm vi người dùng.
        var lists = await _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
            .Where(p => p.Id == a.OldPriceListId || p.Id == a.NewPriceListId).Select(p => new { p.Id, p.No, p.Title, p.Status })
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var createdBy = a.CreatedBy is { } uid
            ? await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var itemIds = contract.Lines.Select(l => l.ItemId).Distinct().ToList();
        var inScope = await _db.Items.CountAsync(i => itemIds.Contains(i.Id), cancellationToken) == itemIds.Count;
        string? readOnly = !inScope ? "Hợp đồng có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !_scope.HasPermission(Permissions.Purchase.ContractManage) ? "Bạn không có quyền lập, sửa phụ lục hợp đồng." : null;
        var canEdit = a.Status == ContractAmendmentStatus.Draft && readOnly is null;
        var canApprove = a.IsAwaitingApproval && inScope
            && await _approvals.ApproveBlockerAsync(a, await _approvals.CurrentStepAsync(a, cancellationToken), cancellationToken) is null;
        var newList = lists[a.NewPriceListId];
        return new ContractAmendmentDto(
            a.Id, a.No, contract.Id, contract.No, contract.Title, supplier.Code, supplier.Name, a.PaperNo, a.Reason, a.SignDate, contract.ValidFrom,
            a.OldValidTo, a.NewValidTo, a.OldPriceListId, lists[a.OldPriceListId].No, a.NewPriceListId, newList.No, newList.Title, newList.Status.ToString(),
            TermsDto(a.NewTerms), TermsDto(contract.Terms), a.Note, contract.ExpectedAmount, a.ExpectedAmount, a.ChangeSummary, a.Status.ToString(),
            a.RejectReason, a.CreatedAt, createdBy, a.ApprovedAt, await _approvals.HistoryAsync(a.DocType, a.Id, cancellationToken, a),
            new ContractAmendmentActions(canEdit, canEdit, canApprove, a.Status == ContractAmendmentStatus.Submitted && readOnly is null, canEdit, readOnly)) { Version = a.Version };
    }

    /// <summary>Phụ lục của hợp đồng cho tab Phụ lục, mới nhất trước.</summary>
    private async Task<List<ContractAmendmentListItemDto>> AmendmentsOfAsync(Guid contractId, CancellationToken cancellationToken) =>
        await (
                from a in _db.PurchaseContractAmendments.AsNoTracking()
                where a.ContractId == contractId
                join u in _db.Users on a.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.Seq descending
                select new ContractAmendmentListItemDto(
                    a.Id, a.No, a.PaperNo, a.SignDate, a.Reason, a.ChangeSummary, a.ExpectedAmount, a.Status.ToString(), a.CreatedAt,
                    u == null ? null : u.FullName, a.ApprovedAt))
            .ToListAsync(cancellationToken);

    private static ContractTermsDto TermsDto(ContractTerms t) =>
        new(t.PriceAdjustmentTerms, t.PaymentTerms, t.CreditLimit, t.DeliveryTerms, t.DiscountTerms, t.SponsorshipTerms, t.PenaltyTerms, t.OtherTerms);

    private static ContractAmendmentData ToAmendmentData(SaveContractAmendmentRequest r) =>
        new(r.PaperNo, r.Reason, r.SignDate, r.NewValidTo, r.NewPriceListId,
            new ContractTerms(r.PriceAdjustmentTerms, r.PaymentTerms, r.CreditLimit, r.DeliveryTerms, r.DiscountTerms, r.SponsorshipTerms, r.PenaltyTerms, r.OtherTerms),
            r.Note);

    /// <summary>Ngày dd/MM/yyyy trong tóm tắt thay đổi (CLAUDE.md mục 5).</summary>
    private static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
