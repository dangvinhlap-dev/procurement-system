# procurement-system

Phần mềm quản lý nhà cung cấp, giá, mua hàng, đấu thầu và kho cho tổng công ty và các công ty thành viên.

- Thiết kế: [docs/design.md](docs/design.md)
- Giao diện: [docs/ui-guide.md](docs/ui-guide.md)
- Quy tắc cho Claude Code: [CLAUDE.md](CLAUDE.md)
- Nhật ký phiên bản: [CHANGELOG.md](CHANGELOG.md)

## Bắt đầu trên máy Windows
1. Cài: Git, .NET SDK 10, Node.js LTS, PostgreSQL 18 (xem docs/design.md mục 15).
2. Tạo user `proc` / mật khẩu `proc_dev_only`, database `procurement` và `procurement_test`.
3. Mở PowerShell: `cd D:\Project\procurement-system` rồi gõ `claude`.

## Lệnh thường dùng (PowerShell, tại thư mục gốc)
| Việc | Lệnh |
| --- | --- |
| Khôi phục công cụ (dotnet-ef) | `dotnet tool restore` |
| Build backend | `dotnet build` |
| Chạy toàn bộ test | `dotnet test` |
| Chạy API (http://localhost:5000) | `dotnet run --project src\Proc.Api` |
| Kiểm tra API và CSDL | mở http://localhost:5000/health |
| Cài gói giao diện (lần đầu) | `cd src\Proc.Web; npm install` |
| Chạy giao diện (http://localhost:5173) | `cd src\Proc.Web; npm run dev` |
| Build giao diện | `cd src\Proc.Web; npm run build` |
