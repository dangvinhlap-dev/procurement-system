using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Documents;

/// <summary>
/// Đính kèm file dùng chung (design.md mục 18.4). File nằm trong thư mục <see cref="AttachmentOptions.RootPath"/>, CSDL lưu đường dẫn
/// tương đối và thông tin file. Quyền đi theo đối tượng chủ; chủ đọc qua global query filter (công ty, kho) nên phiếu ngoài phạm vi
/// trả 404 giống như không tồn tại.
/// </summary>
public sealed class AttachmentService : IAttachmentService
{
    /// <summary>Quyền xem và quyền sửa của từng loại chủ – không thêm mã quyền riêng cho đính kèm (design.md mục 18.4).</summary>
    private static readonly IReadOnlyDictionary<string, (string View, string Manage)> OwnerPermissions =
        new Dictionary<string, (string View, string Manage)>
        {
            [AttachmentOwnerTypes.Supplier] = (Permissions.MasterData.SupplierView, Permissions.MasterData.SupplierManage),
            [AttachmentOwnerTypes.SupplierPriceList] = (Permissions.Pricing.PriceListView, Permissions.Pricing.PriceListManage),
            [AttachmentOwnerTypes.Rfq] = (Permissions.Sourcing.RfqView, Permissions.Sourcing.RfqManage),
            [AttachmentOwnerTypes.PurchaseRequest] = (Permissions.Purchase.PrView, Permissions.Purchase.PrCreate),
            [AttachmentOwnerTypes.PurchaseOrder] = (Permissions.Purchase.PoView, Permissions.Purchase.PoCreate),
            [AttachmentOwnerTypes.InventoryDocument] = (Permissions.Inventory.DocumentView, Permissions.Inventory.DocumentCreate),
            // Phiếu xác nhận nhận hàng XNH (lát K2): thủ kho / phòng ban đề xuất đính kèm phiếu giao, biên bản nghiệm thu, ảnh hàng.
            [AttachmentOwnerTypes.GoodsReceiptConfirmation] = (Permissions.Purchase.ReceiptView, Permissions.Purchase.ReceiptManage),
            [AttachmentOwnerTypes.PurchaseContract] = (Permissions.Purchase.ContractView, Permissions.Purchase.ContractManage),
            [AttachmentOwnerTypes.SoleSourceApproval] = (Permissions.MasterData.SoleSourceView, Permissions.MasterData.SoleSourceManage),
            // Lát A4 (mọi form có đính kèm): quyền theo màn hình chủ, giống các service tương ứng.
            [AttachmentOwnerTypes.SourcingRequest] = (Permissions.Sourcing.RfqView, Permissions.Sourcing.RfqManage),
            [AttachmentOwnerTypes.ContractAmendment] = (Permissions.Purchase.ContractView, Permissions.Purchase.ContractManage),
            [AttachmentOwnerTypes.SupplierReview] = (Permissions.MasterData.SupplierReviewView, Permissions.MasterData.SupplierReviewManage),
            // Hồ sơ NCC mới (G8): Phòng cung ứng chung đính kèm giấy tờ; Văn phòng Ban, Hội đồng xem.
            [AttachmentOwnerTypes.SupplierOnboarding] = (Permissions.MasterData.SupplierOnboardingView, Permissions.MasterData.SupplierOnboardingManage),
            [AttachmentOwnerTypes.ControlFinding] = (Permissions.Control.AnomalyView, Permissions.Control.FindingManage),
            [AttachmentOwnerTypes.MonthlySupplierApproval] = (Permissions.Pricing.MonthlyApprovalView, Permissions.Pricing.MonthlyApprovalManage),
            [AttachmentOwnerTypes.VolumeRebateAgreement] = (Permissions.Pricing.VolumeRebateView, Permissions.Pricing.VolumeRebateManage),
            [AttachmentOwnerTypes.VolumeRebateReceipt] = (Permissions.Pricing.VolumeRebateView, Permissions.Pricing.VolumeRebateManage),
            [AttachmentOwnerTypes.MarketPriceSurvey] = (Permissions.Pricing.PriceBankView, Permissions.Pricing.PriceBankManage),
            [AttachmentOwnerTypes.PooledOrder] = (Permissions.Purchase.PooledOrderView, Permissions.Purchase.PooledOrderManage),
            [AttachmentOwnerTypes.DemandPlan] = (Permissions.Purchase.DemandPlanView, Permissions.Purchase.DemandPlanManage),
            // Kê khai xung đột lợi ích: ai cũng tự kê khai được (không mã quyền) – kiểm tra riêng theo người kê khai, xem RequireOwnerAsync.
            [AttachmentOwnerTypes.ConflictDeclaration] = (string.Empty, string.Empty),
            // Ý kiến thẩm định (lát H1): quyền theo chủ của ý kiến và người ghi – kiểm tra riêng qua IAppraisalOpinionService.
            [AttachmentOwnerTypes.AppraisalOpinion] = (string.Empty, string.Empty),
        };

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IAppraisalOpinionService _appraisals;
    private readonly string _root;
    private readonly int _maxFileSizeMb;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="options">Thư mục gốc (Api đã đổi đường dẫn tương đối thành tuyệt đối).</param>
    /// <param name="appraisals">Ý kiến thẩm định – kiểm tra quyền đính kèm của ý kiến (lát H1).</param>
    public AttachmentService(
        ProcDbContext db, ICurrentUserScope scope, IClock clock, IOptions<AttachmentOptions> options, IAppraisalOpinionService appraisals)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _appraisals = appraisals;
        _root = Path.GetFullPath(options.Value.RootPath);
        _maxFileSizeMb = options.Value.MaxFileSizeMb;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        await RequireOwnerAsync(ownerType, ownerId, manage: false, cancellationToken);
        return await (
                from a in _db.Attachments.AsNoTracking()
                where a.OwnerType == ownerType && a.OwnerId == ownerId && !a.IsDeleted
                join u in _db.Users on a.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.CreatedAt
                select new AttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.Note, a.CreatedAt, u == null ? null : u.FullName))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AttachmentDto> UploadAsync(
        string ownerType, Guid ownerId, string fileName, long sizeBytes, Stream content, string? note, CancellationToken cancellationToken)
    {
        var companyId = await RequireOwnerAsync(ownerType, ownerId, manage: true, cancellationToken);
        Attachment.ValidateFile(fileName, sizeBytes, _maxFileSizeMb);

        // Ghi ra file tạm cùng thư mục gốc (đổi tên cùng ổ đĩa là nguyên tử), đo dung lượng thật và băm SHA-256 trong lúc ghi;
        // không tin dung lượng client báo.
        Directory.CreateDirectory(_root);
        var tempPath = Path.Combine(_root, $".upload-{Guid.NewGuid():N}.tmp");
        string? finalPath = null;
        try
        {
            long written;
            string sha256;
            await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                written = await CopyLimitedAsync(content, file, hash, _maxFileSizeMb, cancellationToken);
                sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            var attachment = Attachment.Create(ownerType, ownerId, companyId, fileName, written, sha256, _clock.UtcNow, note, _maxFileSizeMb);
            finalPath = ResolvePath(attachment.StoragePath);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            File.Move(tempPath, finalPath);

            _db.Attachments.Add(attachment);
            await _db.SaveChangesAsync(cancellationToken);

            var uploader = await _db.Users.Where(u => u.Id == attachment.CreatedBy).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
            return new AttachmentDto(
                attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.Note, attachment.CreatedAt, uploader);
        }
        catch
        {
            // Lưu CSDL hoặc kiểm tra lỗi: xóa file đã ghi để thư mục không có file "mồ côi" không bản ghi nào trỏ tới.
            TryDelete(tempPath);
            if (finalPath is not null) TryDelete(finalPath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<AttachmentContent> OpenAsync(Guid id, CancellationToken cancellationToken)
    {
        var attachment = await FindAsync(id, cancellationToken);
        await RequireOwnerAsync(attachment.OwnerType, attachment.OwnerId, manage: false, cancellationToken);

        var path = ResolvePath(attachment.StoragePath);
        if (!File.Exists(path)) throw new NotFoundException("Không tìm thấy file trên máy chủ. Hãy báo quản trị hệ thống kiểm tra thư mục đính kèm.");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return new AttachmentContent(attachment.FileName, attachment.ContentType, stream);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var attachment = await FindAsync(id, cancellationToken);
        await RequireOwnerAsync(attachment.OwnerType, attachment.OwnerId, manage: true, cancellationToken);
        attachment.Delete(_scope.UserId ?? Guid.Empty, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Tìm file đính kèm chưa xóa theo Id; không thấy thì báo lỗi 404.</summary>
    private async Task<Attachment> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Attachments.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy file đính kèm.");

    /// <summary>
    /// Kiểm tra quyền theo loại chủ rồi đọc chủ qua global query filter (công ty đang làm việc, phạm vi kho của phiếu kho).
    /// </summary>
    /// <returns>CompanyId của chủ; null với NCC (danh mục dùng chung).</returns>
    private async Task<Guid?> RequireOwnerAsync(string ownerType, Guid ownerId, bool manage, CancellationToken cancellationToken)
    {
        if (!OwnerPermissions.TryGetValue(ownerType, out var permissions))
            throw new BusinessRuleException($"Loại đối tượng đính kèm '{ownerType}' không hợp lệ.");
        _scope.RequireCompany();
        // Phiếu điều chỉnh / kiểm kê (ADJ) có quyền lập riêng (design.md mục 18.23): người kiểm kê đính kèm biên bản scan theo quyền lập
        // ADJ, không cần quyền lập chứng từ kho thường – kiểm tra sau khi biết loại phiếu.
        var adjustmentAware = manage && ownerType == AttachmentOwnerTypes.InventoryDocument;
        if (ownerType == AttachmentOwnerTypes.ConflictDeclaration) return await RequireConflictDeclarationAsync(ownerId, manage, cancellationToken);
        if (ownerType == AttachmentOwnerTypes.AppraisalOpinion) return await _appraisals.RequireAttachmentAccessAsync(ownerId, manage, cancellationToken);
        if (!adjustmentAware) _scope.EnsurePermission(manage ? permissions.Manage : permissions.View);

        // Select CompanyId dạng Guid? để một kiểu chung cho mọi loại chủ; NCC trả null có chủ đích (danh mục dùng chung – mục 5).
        var owner = ownerType switch
        {
            AttachmentOwnerTypes.Supplier => await _db.Suppliers.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow((Guid?)null)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.SupplierPriceList => await _db.SupplierPriceLists.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.Rfq => await _db.Rfqs.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.PurchaseRequest => await _db.PurchaseRequests.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.PurchaseOrder => await _db.PurchaseOrders.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.PurchaseContract => await _db.PurchaseContracts.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.SoleSourceApproval => await _db.SoleSourceApprovals.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.SourcingRequest => await _db.SourcingRequests.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.ContractAmendment => await _db.PurchaseContractAmendments.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.SupplierReview => await _db.SupplierReviews.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.SupplierOnboarding => await _db.SupplierOnboardings.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.GoodsReceiptConfirmation => await _db.GoodsReceiptConfirmations.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.ControlFinding => await _db.ControlFindings.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            // Phiếu duyệt tháng thuộc tổng công ty: đính kèm (biên bản họp Ban) xem, thêm khi đang làm việc ở tổng công ty.
            // Phiếu khảo sát giá thuộc tổng công ty (filter công ty giữ nguyên).
            AttachmentOwnerTypes.MarketPriceSurvey => await _db.MarketPriceSurveys.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            // Phiếu đặt hàng gộp thuộc tổng công ty (filter công ty giữ nguyên) – lát G7.
            AttachmentOwnerTypes.PooledOrder => await _db.PooledOrders.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.MonthlySupplierApproval => await _db.MonthlySupplierApprovals.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            // Thỏa thuận chiết khấu sản lượng gộp thuộc tổng công ty: đính kèm (văn bản thỏa thuận) xem, thêm khi đang làm việc ở tổng công ty.
            AttachmentOwnerTypes.VolumeRebateAgreement => await _db.VolumeRebateAgreements.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            // Thực nhận chiết khấu sản lượng gộp thuộc tổng công ty (như thỏa thuận): xem, thêm khi đang làm việc ở tổng công ty.
            AttachmentOwnerTypes.VolumeRebateReceipt => await _db.VolumeRebateReceipts.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwnerTypes.DemandPlan => await _db.DemandPlans.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
            _ => await _db.InventoryDocuments.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId, x.DocType)).FirstOrDefaultAsync(cancellationToken),
        };
        if (owner is null) throw new NotFoundException("Không tìm thấy đối tượng cần đính kèm hoặc ngoài phạm vi được xem.");
        if (adjustmentAware)
            _scope.EnsurePermission(owner.DocType == DocumentTypes.Adjustment ? Permissions.Inventory.AdjustmentCreate : permissions.Manage);
        if (owner.CompanyId is { } companyId) _scope.EnsureCompany(companyId);
        return owner.CompanyId;
    }

    /// <summary>
    /// Chủ là kê khai xung đột lợi ích (design.md mục 18.8): không thuộc công ty, không có mã quyền xem riêng. Người kê khai xem/đính kèm
    /// được kê khai của chính mình; người khác cần quyền xem toàn bộ (xem) hoặc quyền quản lý (đính kèm, xóa) – giống
    /// ConflictDeclarationService.
    /// </summary>
    private async Task<Guid?> RequireConflictDeclarationAsync(Guid ownerId, bool manage, CancellationToken cancellationToken)
    {
        var userId = await _db.ConflictDeclarations.Where(x => x.Id == ownerId).Select(x => (Guid?)x.UserId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đối tượng cần đính kèm hoặc ngoài phạm vi được xem.");
        if (userId == _scope.UserId) return null;
        var allowed = manage
            ? _scope.HasPermission(Permissions.MasterData.ConflictManage)
            : _scope.HasPermission(Permissions.MasterData.ConflictView) || _scope.HasPermission(Permissions.MasterData.ConflictManage);
        if (!allowed) throw new ForbiddenAccessException("Bạn không có quyền với kê khai xung đột lợi ích của người khác.");
        return null;
    }

    /// <summary>
    /// Đổi đường dẫn tương đối lưu trong CSDL thành đường dẫn tuyệt đối, chặn mọi đường dẫn thoát khỏi thư mục gốc (phòng dữ liệu CSDL bị
    /// sửa tay).
    /// </summary>
    private string ResolvePath(string storagePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException("Đường dẫn file đính kèm không hợp lệ.");
        return full;
    }

    /// <summary>Chép luồng, dừng khi vượt dung lượng tối đa (client có thể báo sai dung lượng).</summary>
    private static async Task<long> CopyLimitedAsync(
        Stream source, Stream target, IncrementalHash hash, int maxFileSizeMb, CancellationToken cancellationToken)
    {
        var maxBytes = Attachment.MegabytesToBytes(maxFileSizeMb);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes) throw Attachment.TooLarge(maxFileSizeMb);
            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return total;
    }

    /// <summary>Xóa file trên đĩa nếu có; lỗi xóa file không làm hỏng thao tác chính (file mồ côi dọn sau).</summary>
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // Không xóa được (đang bị khóa) thì để lại: file mồ côi vô hại, không được che lỗi gốc.
        }
    }

    /// <summary>Chủ đính kèm: công ty (null với NCC) và loại phiếu (chỉ phiếu kho – phân biệt quyền lập ADJ).</summary>
    private sealed record OwnerRow(Guid? CompanyId, string? DocType = null);
}
