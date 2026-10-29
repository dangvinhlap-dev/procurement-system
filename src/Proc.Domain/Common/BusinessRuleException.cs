namespace Proc.Domain.Common;

/// <summary>
/// Lỗi vi phạm quy tắc nghiệp vụ. Thông điệp viết bằng tiếng Việt để hiển thị thẳng cho người dùng;
/// API trả về mã 400 kèm thông điệp này.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    /// <summary>
    /// Tạo lỗi nghiệp vụ.
    /// </summary>
    /// <param name="message">Thông điệp tiếng Việt cho người dùng.</param>
    public BusinessRuleException(string message)
        : base(message)
    {
    }
}
