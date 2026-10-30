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

Tài khoản mẫu để thử phân quyền (mật khẩu **Demo@2026**):

| Tài khoản | Vai trò | Công ty | Phạm vi |
| --- | --- | --- | --- |
| `thukho.bha` | Thủ kho | BHA | Chỉ kho KP1, chỉ nhóm hàng Vật tư (VT) và nhóm con |
| `muahang.nmi` | Nhân viên mua hàng | NMI | Mọi kho, mọi nhóm hàng |

Thêm tài khoản, gán vai trò, kho, nhóm hàng ở **Hệ thống → Người dùng** (gán tại công ty đang chọn trên thanh menu);
sửa quyền của vai trò ở **Hệ thống → Vai trò**.

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

## Triển khai server (Ubuntu + Docker)
Chi tiết hạ tầng: docs/design.md mục 14. Các lệnh dưới đây chạy trên server Linux, tại thư mục chứa repo.

**Cài lần đầu**
1. Cài Docker Engine + Compose plugin; tường lửa chỉ mở cổng 22, 80, 443.
2. `cp .env.example .env` rồi sửa `.env`: đổi mọi mật khẩu, `JWT_SIGNING_KEY` (sinh bằng `openssl rand -base64 48`),
   `SITE_ADDRESS` (tên miền để có HTTPS, hoặc `:80` trong mạng nội bộ), `APP_VERSION` (ví dụ `0.1.0`).
3. Build image: `docker compose build app`.
4. Khởi động CSDL rồi tạo bảng bằng script của phiên bản:
   `docker compose up -d db` rồi
   `docker compose exec db psql -U proc -d procurement -v ON_ERROR_STOP=1 -f /db/scripts/0.1.0.sql`.
5. `docker compose up -d`, mở trang web, đăng nhập bằng `ADMIN_USERNAME` / `ADMIN_PASSWORD` rồi **đổi mật khẩu ngay**.

**Nâng cấp lên bản mới** (xem CHANGELOG.md mục "Lưu ý khi nâng cấp" của bản đó)
1. Sao lưu trước: `docker compose exec backup sh -c 'pg_dump -Fc -f /backups/truoc-nang-cap.dump'`.
2. `git pull`, đổi `APP_VERSION` trong `.env`, `docker compose build app`.
3. Nếu bản mới có thay đổi CSDL: chạy `db/scripts/<phiên-bản>.sql` như bước 4 ở trên (script chạy lại nhiều lần không sao).
4. `docker compose up -d`.

**Sao lưu và khôi phục**
- Container `backup` tự sao lưu mỗi đêm vào thư mục `backups/`, giữ 14 ngày; chép thư mục này ra máy khác.
- Khôi phục vào CSDL trống: dừng app (`docker compose stop app`), rồi
  `docker compose exec backup pg_restore --clean --if-exists -d procurement /backups/<file>.dump`, sau đó `docker compose up -d`.

**Xem log kỹ thuật (Seq)**: `ssh -L 5341:localhost:5341 <server>` rồi mở http://localhost:5341
(tài khoản `admin`, mật khẩu `SEQ_ADMIN_PASSWORD`).
