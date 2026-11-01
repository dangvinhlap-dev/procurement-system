namespace Proc.Infrastructure.Documents;

/// <summary>Cấu hình lưu file đính kèm – mục <c>Attachments</c> trong appsettings (design.md mục 18.4, quyết định Q6).</summary>
public sealed class AttachmentOptions
{
    /// <summary>
    /// Thư mục gốc chứa file đính kèm. Đường dẫn tương đối tính từ thư mục ứng dụng (ContentRoot); Docker gắn thư mục của server vào
    /// <c>/app/data/attachments</c> để file còn nguyên khi nâng cấp image. Thư mục này phải được sao lưu cùng CSDL.
    /// </summary>
    public string RootPath { get; set; } = "data/attachments";
}
