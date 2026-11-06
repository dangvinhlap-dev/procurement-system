namespace Proc.Application.Common;

/// <summary>
/// Dữ liệu đã bị người khác thay đổi kể từ lúc người dùng mở ra (phiên bản gửi lên khác phiên bản hiện tại, hoặc hai lệnh lưu cùng một
/// dòng cùng lúc). API trả 409 kèm thông điệp tiếng Việt – người dùng tải lại rồi làm lại (design.md mục 18.26, lát R2).
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Thông điệp mặc định cho người dùng.</summary>
    public const string DefaultMessage = "Dữ liệu vừa được người khác thay đổi – hãy tải lại trang rồi thao tác lại.";

    /// <summary>Tạo lỗi xung đột phiên bản với thông điệp mặc định.</summary>
    public ConcurrencyConflictException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Tạo lỗi xung đột phiên bản.</summary>
    /// <param name="message">Thông điệp tiếng Việt cho người dùng.</param>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }
}
