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

## Đăng nhập trên máy dev
Lần đầu chạy API, CSDL `procurement` tự tạo bảng và dữ liệu mẫu. Đăng nhập bằng tài khoản **admin**,
mật khẩu **Admin@2026** (chỉ dùng cho dev, khai báo trong `src\Proc.Api\appsettings.Development.json`).
Tài khoản này được gán vào Tổng công ty (TCT) và 3 công ty mẫu (BHA, NMI, DTV).

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
| Cài trình duyệt cho Playwright (lần đầu) | `cd src\Proc.Web; npx playwright install chromium` |
| Test giao diện (tự chạy API và Vite nếu chưa chạy) | `cd src\Proc.Web; npm run test:e2e` |
