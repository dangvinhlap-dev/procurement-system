namespace Proc.Infrastructure.Documents;

/// <summary>Cấu hình lưu file đính kèm – mục <c>Attachments</c> trong appsettings (design.md mục 18.4, quyết định Q6).</summary>
public sealed class AttachmentOptions
{
    /// <summary>
    /// Thư mục gốc chứa file đính kèm. Đường dẫn tương đối tính từ thư mục ứng dụng (ContentRoot) – chỉ hợp cho máy dev. Server thật
    /// đặt ra ngoài thư mục ứng dụng để cài bản mới không làm mất file: Docker <c>/data/attachments</c> (volume), Windows/IIS ví dụ
    /// <c>D:\ProcurementData\attachments</c> (docs/deploy-windows.md). Thư mục này phải được sao lưu cùng CSDL.
    /// </summary>
    public string RootPath { get; set; } = "data/attachments";

    /// <summary>
    /// Dung lượng tối đa một file (MB), mặc định 2 MB (người dùng chốt 03/11/2026). Cho phép 1–25 MB; muốn trên 25 MB phải nâng cả giới
    /// hạn request của Kestrel/IIS (<c>web.config</c> maxAllowedContentLength) – docs/deploy-windows.md.
    /// </summary>
    public int MaxFileSizeMb { get; set; } = Proc.Domain.Documents.Attachment.DefaultMaxFileSizeMb;
}
