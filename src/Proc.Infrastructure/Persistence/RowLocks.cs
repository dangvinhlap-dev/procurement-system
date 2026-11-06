using Microsoft.EntityFrameworkCore;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// Khóa dòng (<c>SELECT … FOR UPDATE</c>) để các lệnh đổi trạng thái chứng từ (ghi sổ, bỏ ghi sổ, hủy, xác nhận, gửi duyệt) chạy lần lượt
/// trên cùng một chứng từ. Không khóa thì hai lệnh cùng lúc đều đọc thấy "Nháp" và cùng ghi sổ – tồn bị cộng hai lần (lỗi L1, 06/11/2026).
/// </summary>
/// <remarks>
/// Phải gọi trong một giao dịch đang mở: khóa giữ tới khi giao dịch kết thúc. Các Id được khóa theo thứ tự tăng dần nên hai giao dịch
/// cùng cần nhiều dòng (ví dụ cặp ICS–ICR) không khóa chéo nhau (deadlock). Câu lệnh đi thẳng xuống bảng, bỏ qua global query filter –
/// chỉ khóa, không trả dữ liệu; việc đọc chứng từ sau đó vẫn qua filter phạm vi như thường.
/// </remarks>
internal static class RowLocks
{
    /// <summary>
    /// Khóa các dòng của bảng ứng với <typeparamref name="TEntity"/> theo Id, chờ nếu giao dịch khác đang giữ khóa.
    /// </summary>
    /// <typeparam name="TEntity">Entity có khóa chính cột <c>Id</c>.</typeparam>
    /// <param name="db">DbContext đang có giao dịch mở.</param>
    /// <param name="ids">Id cần khóa (bỏ trùng, Id rỗng bị bỏ qua).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="InvalidOperationException">Khi gọi ngoài giao dịch (khóa sẽ nhả ngay, không có tác dụng).</exception>
    public static async Task LockAsync<TEntity>(ProcDbContext db, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Khóa dòng phải nằm trong một giao dịch đang mở.");
        var list = ids.Where(id => id != Guid.Empty).Distinct().Order().ToArray();
        if (list.Length == 0) return;
        var entity = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} không thuộc mô hình dữ liệu.");
        var table = entity.GetTableName();
        var schema = entity.GetSchema();
        var name = schema is null ? $"\"{table}\"" : $"\"{schema}\".\"{table}\"";
        // Tên bảng lấy từ mô hình EF (không phải dữ liệu người dùng) nên ghép chuỗi an toàn; danh sách Id truyền bằng tham số.
#pragma warning disable EF1002
        await db.Database
            .SqlQueryRaw<Guid>($"SELECT \"Id\" AS \"Value\" FROM {name} WHERE \"Id\" = ANY({{0}}) ORDER BY \"Id\" FOR UPDATE", list)
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002
    }

    /// <summary>
    /// Mở giao dịch mới, hoặc dùng giao dịch đang mở của lời gọi bên ngoài (ví dụ xem trước người duyệt chạy lệnh gửi duyệt trong một giao
    /// dịch rồi hủy). Giao dịch dùng chung thì <see cref="OwnedTransaction.CommitAsync"/> không làm gì – bên mở giao dịch tự quyết định.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Giao dịch; hủy (dispose) mà chưa commit thì giao dịch tự mở bị rollback.</returns>
    public static async Task<OwnedTransaction> BeginOrJoinAsync(ProcDbContext db, CancellationToken cancellationToken) =>
        new(db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null);
}

/// <summary>Giao dịch do <see cref="RowLocks.BeginOrJoinAsync"/> trả về: chỉ commit/rollback khi chính nó mở.</summary>
/// <param name="owned">Giao dịch tự mở, hoặc null khi đang dùng giao dịch của lời gọi bên ngoài.</param>
internal sealed class OwnedTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? owned) : IAsyncDisposable
{
    /// <summary>Commit nếu giao dịch do chính đối tượng này mở.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    public Task CommitAsync(CancellationToken cancellationToken) => owned?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => owned?.DisposeAsync() ?? ValueTask.CompletedTask;
}
