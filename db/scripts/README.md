# Script SQL theo phiên bản

Mỗi bản có thay đổi CSDL sinh script bằng
`dotnet ef migrations script <migration-bản-trước> <migration-bản-mới> --idempotent -p src\Proc.Infrastructure -s src\Proc.Api -o db\scripts\<phiên-bản>.sql`
(CLAUDE.md mục 8).

Script `--idempotent` đặt mỗi migration trong khối `DO`, nên SQL viết tay trong migration không được có `SELECT` trần
(gọi hàm thì dùng `DO $p$ BEGIN PERFORM ...; END $p$;`). Chạy thử script trên CSDL trống trước khi phát hành.
