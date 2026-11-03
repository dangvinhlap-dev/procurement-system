using Proc.Domain.Common;

namespace Proc.Domain.Documents;

/// <summary>
/// Loại đối tượng được đính kèm file (design.md mục 18.4). Lưu dạng chữ trong CSDL để thêm loại mới (hợp đồng nguyên tắc – lát B4)
/// không phải đổi dữ liệu cũ.
/// </summary>
public static class AttachmentOwnerTypes
{
    /// <summary>Nhà cung cấp (hồ sơ pháp lý, năng lực) – danh mục dùng chung, không thuộc công ty nào.</summary>
    public const string Supplier = "Supplier";

    /// <summary>Bảng giá nhà cung cấp (báo giá gốc).</summary>
    public const string SupplierPriceList = "SupplierPriceList";

    /// <summary>Yêu cầu báo giá (hồ sơ thầu, biên bản).</summary>
    public const string Rfq = "Rfq";

    /// <summary>Đề nghị mua hàng.</summary>
    public const string PurchaseRequest = "PurchaseRequest";

    /// <summary>Đơn mua hàng (hợp đồng, xác nhận đặt hàng).</summary>
    public const string PurchaseOrder = "PurchaseOrder";

    /// <summary>Phiếu kho mọi loại: nhập mua, trả NCC, chuyển kho, ICS, ICR (hóa đơn, biên bản giao nhận).</summary>
    public const string InventoryDocument = "InventoryDocument";

    /// <summary>Hợp đồng nguyên tắc (bản scan hợp đồng, phụ lục – lát B4a).</summary>
    public const string PurchaseContract = "PurchaseContract";

    /// <summary>Phê duyệt NCC độc quyền (văn bản xác nhận độc quyền, phân phối – lát B5b).</summary>
    public const string SoleSourceApproval = "SoleSourceApproval";

    /// <summary>Phiếu tìm nguồn (báo giá sơ bộ, catalogue – lát A4, mọi form có đính kèm).</summary>
    public const string SourcingRequest = "SourcingRequest";

    /// <summary>Phụ lục hợp đồng nguyên tắc (bản scan phụ lục đã ký – lát A4).</summary>
    public const string ContractAmendment = "ContractAmendment";

    /// <summary>Phiếu đánh giá NCC định kỳ (biên bản, bằng chứng chấm điểm – lát A4).</summary>
    public const string SupplierReview = "SupplierReview";

    /// <summary>Vi phạm KSNB ghi nhận (bằng chứng, biên bản làm việc – lát A4).</summary>
    public const string ControlFinding = "ControlFinding";

    /// <summary>Kê khai xung đột lợi ích (văn bản kê khai có chữ ký – lát A4).</summary>
    public const string ConflictDeclaration = "ConflictDeclaration";

    /// <summary>Mọi loại hợp lệ.</summary>
    public static readonly IReadOnlyList<string> All = [Supplier, SupplierPriceList, Rfq, PurchaseRequest, PurchaseOrder, InventoryDocument, PurchaseContract,
        SoleSourceApproval, SourcingRequest, ContractAmendment, SupplierReview, ControlFinding, ConflictDeclaration];
}

/// <summary>
/// Một file đính kèm (design.md mục 18.4, quyết định Q6): nội dung nằm trong thư mục trên máy chủ, CSDL chỉ lưu đường dẫn và thông tin
/// file. Không là <see cref="ICompanyOwned"/> vì NCC là danh mục dùng chung (CompanyId trống); quyền xem/sửa đi theo đối tượng chủ,
/// dịch vụ đọc đối tượng chủ qua global query filter trước khi cho truy cập file.
/// </summary>
[Auditable("Tệp đính kèm", LabelProperty = nameof(FileName), Ignore = [nameof(StoragePath), nameof(Sha256)])]
public sealed class Attachment : AuditableEntity
{
    /// <summary>
    /// Dung lượng tối đa một file mặc định: 2 MB (người dùng chốt 03/11/2026, thay 20 MB của quyết định Q6). Máy chủ đổi được qua
    /// <c>Attachments:MaxFileSizeMb</c> trong appsettings – xem design.md mục 18.4.
    /// </summary>
    public const int DefaultMaxFileSizeMb = 2;

    /// <summary>Giới hạn trên cho phép cấu hình (MB) – quá mức này phải nâng cả giới hạn request của IIS/Kestrel (docs/deploy-windows.md).</summary>
    public const int MaxConfigurableFileSizeMb = 25;

    /// <summary>Đổi MB sang byte (1 MB = 1024 × 1024 byte; ví dụ 2 MB = 2.097.152 byte).</summary>
    /// <param name="megabytes">Số MB.</param>
    /// <returns>Số byte.</returns>
    public static long MegabytesToBytes(int megabytes) => megabytes * 1024L * 1024L;

    /// <summary>Độ dài tối đa của tên file gốc.</summary>
    public const int FileNameMaxLength = 255;

    /// <summary>Độ dài tối đa của ghi chú.</summary>
    public const int NoteMaxLength = 500;

    /// <summary>
    /// Đuôi file được nhận (chữ thường, có dấu chấm) kèm loại nội dung trả về khi tải xuống. Chỉ nhận tài liệu, ảnh, file nén thông dụng;
    /// không nhận file chạy được, html, svg vì có thể chứa mã độc hoặc script chạy trong trình duyệt.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllowedExtensions = new Dictionary<string, string>
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".csv"] = "text/csv",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".zip"] = "application/zip",
        [".rar"] = "application/vnd.rar",
        [".7z"] = "application/x-7z-compressed",
        [".msg"] = "application/vnd.ms-outlook",
        [".eml"] = "message/rfc822",
        [".xml"] = "application/xml",
    };

    private Attachment()
    {
    }

    /// <summary>Loại đối tượng chủ – xem <see cref="AttachmentOwnerTypes"/>.</summary>
    public string OwnerType { get; private set; } = string.Empty;

    /// <summary>Id đối tượng chủ.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Công ty của đối tượng chủ; null với NCC (danh mục dùng chung).</summary>
    public Guid? CompanyId { get; private set; }

    /// <summary>Tên file gốc người dùng tải lên (chỉ để hiển thị và đặt tên khi tải xuống).</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Đuôi file chữ thường, có dấu chấm, ví dụ ".pdf".</summary>
    public string Extension { get; private set; } = string.Empty;

    /// <summary>Loại nội dung suy ra từ đuôi (không lấy theo client gửi).</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>Dung lượng (byte).</summary>
    public long SizeBytes { get; private set; }

    /// <summary>Mã băm SHA-256 (hex chữ thường) để kiểm tra file trên đĩa không bị đổi.</summary>
    public string Sha256 { get; private set; } = string.Empty;

    /// <summary>
    /// Đường dẫn tương đối trong thư mục đính kèm, dạng "yyyy/MM/{Id}{đuôi}" (dấu "/"). Tên trên đĩa không dùng tên người dùng gửi
    /// để tránh ký tự lạ và "../" thoát khỏi thư mục.
    /// </summary>
    public string StoragePath { get; private set; } = string.Empty;

    /// <summary>Ghi chú (ví dụ "Giấy phép kinh doanh 2026").</summary>
    public string? Note { get; private set; }

    /// <summary>Đã xóa (xóa mềm: ẩn khỏi danh sách, file vẫn giữ trên đĩa để đối chiếu).</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Thời điểm xóa (UTC).</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Người xóa.</summary>
    public Guid? DeletedBy { get; private set; }

    /// <summary>
    /// Tạo bản ghi đính kèm sau khi đã kiểm tra file.
    /// </summary>
    /// <param name="ownerType">Loại đối tượng chủ.</param>
    /// <param name="ownerId">Id đối tượng chủ.</param>
    /// <param name="companyId">Công ty của đối tượng chủ (null với NCC).</param>
    /// <param name="fileName">Tên file gốc.</param>
    /// <param name="sizeBytes">Dung lượng.</param>
    /// <param name="sha256">Mã băm SHA-256.</param>
    /// <param name="uploadedAt">Thời điểm tải lên (UTC) – dùng làm thư mục năm/tháng.</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="maxFileSizeMb">Dung lượng tối đa một file (MB) theo cấu hình máy chủ.</param>
    /// <returns>Bản ghi mới.</returns>
    /// <exception cref="BusinessRuleException">Khi loại chủ, tên, đuôi, dung lượng hoặc ghi chú không hợp lệ.</exception>
    public static Attachment Create(
        string ownerType, Guid ownerId, Guid? companyId, string fileName, long sizeBytes, string sha256, DateTimeOffset uploadedAt, string? note,
        int maxFileSizeMb = DefaultMaxFileSizeMb)
    {
        if (!AttachmentOwnerTypes.All.Contains(ownerType)) throw new BusinessRuleException($"Loại đối tượng đính kèm '{ownerType}' không hợp lệ.");
        var name = NormalizeFileName(fileName);
        var extension = ValidateFile(name, sizeBytes, maxFileSizeMb);
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > NoteMaxLength }) throw new BusinessRuleException($"Ghi chú tối đa {NoteMaxLength} ký tự.");

        var attachment = new Attachment
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            CompanyId = companyId,
            FileName = name,
            Extension = extension,
            ContentType = AllowedExtensions[extension],
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Note = trimmedNote,
        };
        attachment.StoragePath = $"{uploadedAt.UtcDateTime:yyyy}/{uploadedAt.UtcDateTime:MM}/{attachment.Id:N}{extension}";
        return attachment;
    }

    /// <summary>
    /// Kiểm tra file trước khi ghi đĩa (dùng cả ở API để chặn sớm, chưa đọc hết nội dung).
    /// </summary>
    /// <param name="fileName">Tên file gốc.</param>
    /// <param name="sizeBytes">Dung lượng.</param>
    /// <param name="maxFileSizeMb">Dung lượng tối đa một file (MB) theo cấu hình máy chủ.</param>
    /// <returns>Đuôi file chữ thường.</returns>
    /// <exception cref="BusinessRuleException">Khi file rỗng, quá dung lượng tối đa, tên quá dài hoặc đuôi không được nhận.</exception>
    public static string ValidateFile(string fileName, long sizeBytes, int maxFileSizeMb = DefaultMaxFileSizeMb)
    {
        var name = NormalizeFileName(fileName);
        if (sizeBytes <= 0) throw new BusinessRuleException("File rỗng.");
        if (sizeBytes > MegabytesToBytes(maxFileSizeMb)) throw TooLarge(maxFileSizeMb);
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!AllowedExtensions.ContainsKey(extension))
            throw new BusinessRuleException(
                $"Không nhận file '{(extension.Length == 0 ? "không có đuôi" : extension)}'. Chỉ nhận tài liệu (pdf, Word, Excel, PowerPoint, txt, csv, xml), " +
                "ảnh (jpg, png, gif, bmp, tif), file nén (zip, rar, 7z) và thư (msg, eml).");
        return extension;
    }

    /// <summary>Lỗi file quá dung lượng tối đa – một câu chung cho kiểm tra sớm và kiểm tra khi ghi đĩa.</summary>
    /// <param name="maxFileSizeMb">Dung lượng tối đa (MB).</param>
    /// <returns>Ngoại lệ để ném.</returns>
    public static BusinessRuleException TooLarge(int maxFileSizeMb) =>
        new($"File quá {maxFileSizeMb} MB. Hãy nén, giảm độ phân giải ảnh hoặc chia nhỏ file.");

    /// <summary>
    /// Xóa mềm: ẩn khỏi danh sách; file trên đĩa giữ lại (đối chiếu, khôi phục khi xóa nhầm).
    /// </summary>
    /// <param name="userId">Người xóa.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi đã xóa trước đó.</exception>
    public void Delete(Guid userId, DateTimeOffset now)
    {
        if (IsDeleted) throw new BusinessRuleException("File đính kèm đã bị xóa.");
        IsDeleted = true;
        DeletedAt = now;
        DeletedBy = userId;
    }

    /// <summary>
    /// Lấy phần tên file (bỏ đường dẫn thư mục mà vài trình duyệt cũ gửi kèm), bỏ khoảng trắng thừa, kiểm tra độ dài.
    /// </summary>
    private static string NormalizeFileName(string fileName)
    {
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..].Trim();
        if (name.Length == 0) throw new BusinessRuleException("Thiếu tên file.");
        if (name.Length > FileNameMaxLength) throw new BusinessRuleException($"Tên file tối đa {FileNameMaxLength} ký tự.");
        return name;
    }
}
