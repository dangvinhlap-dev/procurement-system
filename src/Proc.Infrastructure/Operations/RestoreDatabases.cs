using System.Globalization;
using System.Text.Json;
using Npgsql;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Thao tác CSDL của lát QT6 (phục hồi): tạo / xóa CSDL khôi phục, đếm số dòng để so sánh. Tên CSDL đều do phần mềm đặt
/// (<see cref="RestoreNames"/>), vẫn kiểm tra lại và đưa vào câu lệnh qua <c>format('%I')</c> của PostgreSQL – không ghép chữ tự do.
/// </summary>
public static class RestoreDatabases
{
    /// <summary>Đọc / ghi JSON bảng so sánh (lưu trên lần phục hồi).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Đổi chuỗi kết nối sang database khác (và cổng khác khi khôi phục về mốc thời gian – máy chủ tạm).</summary>
    /// <param name="connectionString">Chuỗi kết nối gốc.</param>
    /// <param name="database">Database.</param>
    /// <param name="port">Cổng; null = giữ nguyên.</param>
    /// <returns>Chuỗi kết nối mới, không dùng pool (kết nối hiếm khi dùng, không để phiên treo chặn DROP / đổi tên CSDL).</returns>
    public static string ForDatabase(string connectionString, string database, int? port = null)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString) { Database = database, Pooling = false };
        if (port is { } p) b.Port = p;
        return b.ConnectionString;
    }

    /// <summary>CSDL đã có trên máy chủ chưa.</summary>
    /// <param name="adminConnectionString">Kết nối bất kỳ tới máy chủ.</param>
    /// <param name="name">Tên CSDL.</param>
    /// <param name="ct">Hủy thao tác.</param>
    /// <returns>true khi đã có.</returns>
    public static async Task<bool> ExistsAsync(string adminConnectionString, string name, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminConnectionString) { Pooling = false }.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @n)", conn);
        cmd.Parameters.AddWithValue("n", name);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    /// <summary>
    /// CSDL khác trên cùng máy chủ PostgreSQL: bỏ <c>postgres</c>, template, CSDL đang chạy và các bản <c>&lt;CSDL&gt;_restore_…</c> /
    /// <c>&lt;CSDL&gt;_old_…</c> của chính phần mềm. Khôi phục về mốc thời gian quay lùi cả các CSDL này (bản Full là cả thư mục dữ liệu).
    /// </summary>
    /// <param name="connectionString">Kết nối bất kỳ tới máy chủ.</param>
    /// <param name="liveDatabase">CSDL đang chạy.</param>
    /// <param name="ct">Hủy thao tác.</param>
    /// <returns>Tên CSDL, theo thứ tự chữ.</returns>
    public static async Task<IReadOnlyList<string>> OtherDatabasesAsync(string connectionString, string liveDatabase, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE NOT datistemplate AND datname <> 'postgres' ORDER BY datname", conn);
        var names = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var n = reader.GetString(0);
            if (n == liveDatabase || n.StartsWith(liveDatabase + "_restore_", StringComparison.Ordinal)
                || n.StartsWith(liveDatabase + "_old_", StringComparison.Ordinal)) continue;
            names.Add(n);
        }

        return names;
    }

    /// <summary>
    /// Tạo CSDL khôi phục rỗng, <b>cùng bảng mã và cách so sánh chữ</b> với CSDL đang chạy (từ <c>template0</c> – không mang theo đối tượng của
    /// template1). Tài khoản bảo trì tạo thì đặt chủ là tài khoản ứng dụng (<paramref name="owner"/> – tài khoản bảo trì phải là thành viên, cấp
    /// bằng <c>qt-enable-wal.ps1</c>); tài khoản ứng dụng tự tạo thì chính nó là chủ.
    /// </summary>
    /// <param name="adminConnectionString">Kết nối tạo CSDL (cần quyền CREATEDB).</param>
    /// <param name="name">Tên CSDL mới.</param>
    /// <param name="likeDatabase">CSDL đang chạy (lấy bảng mã, locale).</param>
    /// <param name="owner">Chủ CSDL; null = tài khoản đang kết nối.</param>
    /// <param name="ct">Hủy thao tác.</param>
    /// <returns>Task.</returns>
    /// <exception cref="BusinessRuleException">Tên không hợp lệ, CSDL mẫu không có.</exception>
    public static async Task CreateAsync(string adminConnectionString, string name, string likeDatabase, string? owner, CancellationToken ct)
    {
        if (!RestoreNames.IsValidDatabaseName(name)) throw new BusinessRuleException("Tên CSDL khôi phục không hợp lệ.");
        await using var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminConnectionString) { Pooling = false }.ConnectionString);
        await conn.OpenAsync(ct);
        // Câu lệnh do PostgreSQL dựng (format %I / %L tự thêm ngoặc kép, nháy) từ thông tin của CSDL đang chạy. datlocprovider: c = libc,
        // i = icu, b = builtin (PostgreSQL 17+); với icu / builtin thêm LOCALE_PROVIDER + locale riêng.
        const string build = """
            SELECT format('CREATE DATABASE %I', @name)
                || CASE WHEN @owner::text IS NULL THEN '' ELSE format(' OWNER %I', @owner::text) END
                || format(' TEMPLATE template0 ENCODING %L LC_COLLATE %L LC_CTYPE %L', pg_encoding_to_char(d.encoding), d.datcollate, d.datctype)
                || CASE d.datlocprovider
                     WHEN 'i' THEN format(' LOCALE_PROVIDER icu ICU_LOCALE %L', d.datlocale)
                     WHEN 'b' THEN format(' LOCALE_PROVIDER builtin BUILTIN_LOCALE %L', d.datlocale)
                     ELSE '' END
            FROM pg_database d WHERE d.datname = @like
            """;
        string sql;
        await using (var cmd = new NpgsqlCommand(build, conn))
        {
            cmd.Parameters.AddWithValue("name", name);
            cmd.Parameters.AddWithValue("owner", (object?)owner ?? DBNull.Value);
            cmd.Parameters.AddWithValue("like", likeDatabase);
            sql = await cmd.ExecuteScalarAsync(ct) as string
                ?? throw new BusinessRuleException($"Không thấy CSDL {likeDatabase} trên máy chủ.");
        }

        await using var create = new NpgsqlCommand(sql, conn);
        await create.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Xóa CSDL khôi phục (<c>DROP DATABASE … WITH (FORCE)</c> – ngắt phiên còn mở vào CSDL đó). Không bao giờ xóa CSDL đang chạy.
    /// </summary>
    /// <param name="adminConnectionString">Kết nối (chủ CSDL hoặc thành viên của chủ).</param>
    /// <param name="name">Tên CSDL.</param>
    /// <param name="liveDatabase">CSDL đang chạy – trùng thì từ chối.</param>
    /// <param name="ct">Hủy thao tác.</param>
    /// <returns>true khi đã xóa; false khi không có CSDL đó.</returns>
    /// <exception cref="BusinessRuleException">Tên không hợp lệ hoặc là CSDL đang chạy.</exception>
    public static async Task<bool> DropAsync(string adminConnectionString, string name, string liveDatabase, CancellationToken ct)
    {
        if (!RestoreNames.IsValidDatabaseName(name) || string.Equals(name, liveDatabase, StringComparison.Ordinal))
            throw new BusinessRuleException("Không xóa được CSDL này.");
        if (!await ExistsAsync(adminConnectionString, name, ct)) return false;
        await using var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminConnectionString) { Pooling = false }.ConnectionString);
        await conn.OpenAsync(ct);
        string sql;
        await using (var cmd = new NpgsqlCommand("SELECT format('DROP DATABASE %I WITH (FORCE)', @n)", conn))
        {
            cmd.Parameters.AddWithValue("n", name);
            sql = (string)(await cmd.ExecuteScalarAsync(ct))!;
        }

        await using var drop = new NpgsqlCommand(sql, conn);
        await drop.ExecuteNonQueryAsync(ct);
        return true;
    }

    /// <summary>
    /// Đếm đúng số dòng mọi bảng (<c>count(*)</c>, không dùng số ước tính) và đọc migration cuối của một CSDL.
    /// </summary>
    /// <param name="connectionString">Kết nối tới CSDL cần đếm.</param>
    /// <param name="ct">Hủy thao tác.</param>
    /// <returns>(migration cuối, số dòng theo "schema.bảng").</returns>
    public static async Task<(string? Migration, Dictionary<(string Schema, string Table), long> Rows)> CountAsync(
        string connectionString, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var tables = new List<(string Schema, string Table)>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT n.nspname, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind IN ('r', 'p') AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\_toast%'
            ORDER BY 1, 2
            """, conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        var rows = new Dictionary<(string, string), long>();
        string? migration = null;
        foreach (var t in tables)
        {
            if (t.Table == "__EFMigrationsHistory")
            {
                await using var m = new NpgsqlCommand($"SELECT max(\"MigrationId\") FROM {Quote(t.Schema)}.\"__EFMigrationsHistory\"", conn);
                migration = await m.ExecuteScalarAsync(ct) as string;
                continue;
            }

            // Tên bảng lấy từ danh mục PostgreSQL, đưa vào câu lệnh bằng ngoặc kép (ngoặc kép trong tên nhân đôi).
            await using var c = new NpgsqlCommand($"SELECT count(*) FROM {Quote(t.Schema)}.{Quote(t.Table)}", conn) { CommandTimeout = 600 };
            rows[t] = (long)(await c.ExecuteScalarAsync(ct))!;
        }

        return (migration, rows);
    }

    /// <summary>
    /// So sánh CSDL đang chạy với bản khôi phục: mỗi bảng một dòng, bảng nghiệp vụ khác số dòng trước. Hoán đổi được khi cùng migration cuối
    /// (bản dump của phiên bản phần mềm khác thì ứng dụng hiện tại không chạy đúng trên đó – dùng để tra cứu / lấy lại dữ liệu, hoặc quay về bản cũ
    /// mức 3 ở QT8).
    /// </summary>
    /// <param name="liveConnectionString">CSDL đang chạy.</param>
    /// <param name="restoreConnectionString">Bản khôi phục.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <param name="ct">Hủy thao tác.</param>
    /// <returns>(bảng so sánh, hoán đổi được, câu tóm tắt).</returns>
    public static async Task<(RestoreComparisonDto Comparison, bool CanSwap, string Note)> CompareAsync(
        string liveConnectionString, string restoreConnectionString, DateTimeOffset now, CancellationToken ct)
    {
        var live = await CountAsync(liveConnectionString, ct);
        var restored = await CountAsync(restoreConnectionString, ct);
        var rows = live.Rows.Keys.Union(restored.Rows.Keys)
            .Select(k =>
            {
                long? l = live.Rows.TryGetValue(k, out var a) ? a : null;
                long? r = restored.Rows.TryGetValue(k, out var b) ? b : null;
                var operational = k.Schema == "public" && RestoreCarryOver.Contains(k.Table);
                var light = operational ? "Neutral" : l == r ? "Ok" : "Warning";
                return new RestoreCompareRowDto(k.Schema, k.Table, l, r, operational, light);
            })
            .OrderBy(r => r.Light == "Warning" ? 0 : 1)
            .ThenBy(r => r.Schema, StringComparer.Ordinal)
            .ThenBy(r => r.Table, StringComparer.Ordinal)
            .ToList();

        var canSwap = live.Migration is not null && string.Equals(live.Migration, restored.Migration, StringComparison.Ordinal);
        var differ = rows.Count(r => r.Light == "Warning");
        var ops = rows.Count(r => r.Operational);
        var note = string.Create(CultureInfo.InvariantCulture,
            $"{rows.Count} bảng: {differ} bảng dữ liệu khác số dòng, {ops} bảng vận hành (giữ theo CSDL đang chạy khi hoán đổi). ");
        note += canSwap
            ? $"Cùng phiên bản CSDL ({Short(live.Migration)}) – hoán đổi được."
            : $"Khác phiên bản CSDL: bản khôi phục {Short(restored.Migration) ?? "(không có lịch sử migration)"}, CSDL đang chạy " +
              $"{Short(live.Migration)} – không hoán đổi được; dùng để tra cứu, lấy lại dữ liệu.";
        return (new RestoreComparisonDto(now, live.Migration, restored.Migration, rows), canSwap, note);
    }

    /// <summary>Tên migration bỏ phần giờ đầu: <c>20261115045740_V0_34_0_BoThucThi</c> → <c>V0_34_0_BoThucThi</c>.</summary>
    private static string? Short(string? migration) =>
        migration is { Length: > 15 } && migration[14] == '_' ? migration[15..] : migration;

    /// <summary>Đặt tên trong ngoặc kép, nhân đôi ngoặc kép bên trong.</summary>
    /// <param name="identifier">Tên.</param>
    /// <returns>Tên đã đặt trong ngoặc kép.</returns>
    public static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
