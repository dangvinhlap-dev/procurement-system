# Môi trường dev và lệnh thường dùng

Tách từ CLAUDE.md (mục 2 cũ) ngày 31/10/2026. Đọc khi cần chạy, cài đặt hoặc gỡ rối môi trường.

## Máy dev
- Windows 11 Pro, thư mục `D:\Project\procurement-system`, PostgreSQL 18 cài trực tiếp trên máy (không Docker).
- CSDL dev: `Host=localhost;Port=5432;Database=procurement;Username=proc;Password=proc_dev_only`
- CSDL test: `Host=localhost;Port=5432;Database=procurement_test;Username=proc;Password=proc_dev_only`
  (mật khẩu chỉ dùng cho dev). Chuỗi kết nối để trong `appsettings.Development.json` / `appsettings.Testing.json`.
- Test tích hợp chạy trên PostgreSQL thật, database riêng `procurement_test` (không dùng Testcontainers vì máy dev không có Docker).
- Playwright lần đầu cài trình duyệt: `npx playwright install chromium`.
- Docker, docker-compose và `.devcontainer/` chỉ dùng khi triển khai server hoặc Codespaces (dự phòng) – xem design.md mục 14, 15.
- Cổng cố định: API http://localhost:5000, Web (Vite) http://localhost:5173. Tài khoản dev: `admin` / `Admin@2026`.

## Chạy
- API dev: `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --project src/Proc.Api --no-launch-profile --urls http://localhost:5000`
- Web: `cd src/Proc.Web; npm run dev`
- Áp migration cho CSDL dev: `dotnet ef database update --project src/Proc.Infrastructure --startup-project src/Proc.Api`
- Thêm migration: `dotnet ef migrations add V<x>_<y>_<z>_<TenRoNghia> --project src/Proc.Infrastructure --startup-project src/Proc.Api`

## Gỡ rối hay gặp
- `dotnet build` báo khóa DLL (MSB3027): API đang chạy giữ file → dừng API rồi build; chạy lại API bằng `--no-build`.
- Khóa file `obj/*.dswa.cache.json`: đổi tên các file cache đó rồi build lại.
- Playwright tự `dotnet run` (có build) – nếu vướng khóa file, bật sẵn API môi trường Testing bằng `--no-build` rồi chạy
  `npx playwright test` (cấu hình dùng lại server đang chạy).
- Ảnh chụp Playwright bị mờ: chờ hiệu ứng chạy xong (`document.getAnimations()`) trước khi chụp.
