# Script SQL theo phiên bản

Mỗi bản có thay đổi CSDL sinh script bằng
`dotnet ef migrations script <migration-bản-trước> <migration-bản-mới> --idempotent -p src\Proc.Infrastructure -s src\Proc.Api -o db\scripts\<phiên-bản>.sql`
(CLAUDE.md mục 8).
