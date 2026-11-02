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
            [AttachmentOwnerTypes.PurchaseContract] = (Permissions.Purchase.ContractView, Permissions.Purchase.ContractManage),
            [AttachmentOwnerTypes.SoleSourceApproval] = (Permissions.MasterData.SoleSourceView, Permissions.MasterData.SoleSourceManage),
        };

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly string _root;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="options">Thư mục gốc (Api đã đổi đường dẫn tương đối thành tuyệt đối).</param>
    public AttachmentService(ProcDbContext db, ICurrentUserScope scope, IClock clock, IOptions<AttachmentOptions> options)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _root = Path.GetFullPath(options.Value.RootPath);
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
        Attachment.ValidateFile(fileName, sizeBytes);

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
                written = await CopyLimitedAsync(content, file, hash, cancellationToken);
                sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            var attachment = Attachment.Create(ownerType, ownerId, companyId, fileName, written, sha256, _clock.UtcNow, note);
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
        _scope.EnsurePermission(manage ? permissions.Manage : permissions.View);

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
            _ => await _db.InventoryDocuments.Where(x => x.Id == ownerId)
                .Select(x => new OwnerRow(x.CompanyId)).FirstOrDefaultAsync(cancellationToken),
        };
        if (owner is null) throw new NotFoundException("Không tìm thấy đối tượng cần đính kèm hoặc ngoài phạm vi được xem.");
        if (owner.CompanyId is { } companyId) _scope.EnsureCompany(companyId);
        return owner.CompanyId;
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

    /// <summary>Chép luồng, dừng khi vượt 20 MB (client có thể báo sai dung lượng).</summary>
    private static async Task<long> CopyLimitedAsync(Stream source, Stream target, IncrementalHash hash, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > Attachment.MaxFileBytes) throw new BusinessRuleException("File quá 20 MB. Hãy nén hoặc chia nhỏ file.");
            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return total;
    }

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

    private sealed record OwnerRow(Guid? CompanyId);
}
