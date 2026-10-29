namespace Proc.Application.Common;

/// <summary>
/// Không tìm thấy bản ghi (hoặc bản ghi nằm ngoài phạm vi nên coi như không có). API trả 404 kèm thông điệp tiếng Việt.
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>
    /// Tạo lỗi không tìm thấy.
    /// </summary>
    /// <param name="message">Thông điệp tiếng Việt cho người dùng.</param>
    public NotFoundException(string message)
        : base(message)
    {
    }
}
