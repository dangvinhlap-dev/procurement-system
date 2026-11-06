namespace Proc.Application.Common;

/// <summary>
/// Phiên bản chứng từ mà người dùng đã xem khi gửi lệnh (header If-Match của request, gắn với Id trên đường dẫn) – lát R2, design.md
/// mục 18.26. Tầng lưu dữ liệu so với phiên bản lúc đọc: khác thì báo <see cref="ConcurrencyConflictException"/> thay vì ghi đè.
/// </summary>
public interface IExpectedVersion
{
    /// <summary>Ghi nhận phiên bản mong đợi của entity.</summary>
    /// <param name="id">Id entity (Id trên đường dẫn).</param>
    /// <param name="version">Phiên bản người dùng đã xem.</param>
    void Expect(Guid id, uint version);

    /// <summary>Phiên bản mong đợi còn chờ kiểm tra (null = không có hoặc đã kiểm tra ở lần lưu trước trong request).</summary>
    (Guid Id, uint Version)? Pending { get; }

    /// <summary>Đánh dấu đã kiểm tra – các lần lưu sau trong cùng request (phiên bản đã đổi do chính request này) không so nữa.</summary>
    void MarkChecked();
}

/// <summary>Cài đặt mặc định, sống theo một request (scoped).</summary>
public sealed class ExpectedVersion : IExpectedVersion
{
    /// <inheritdoc />
    public (Guid Id, uint Version)? Pending { get; private set; }

    /// <inheritdoc />
    public void Expect(Guid id, uint version) => Pending = (id, version);

    /// <inheritdoc />
    public void MarkChecked() => Pending = null;
}
