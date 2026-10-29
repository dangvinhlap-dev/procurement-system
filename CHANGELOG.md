# Nhật ký thay đổi

Ghi theo [Semantic Versioning](https://semver.org/lang/vi/) – xem CLAUDE.md mục 8.
Mỗi bản phát hành có Git tag `vx.y.z`.

## [0.1.0] - Chưa phát hành

### Thêm mới
- Khung solution theo docs/design.md mục 13.1: Proc.Api, Proc.Application, Proc.Domain,
  Proc.Infrastructure (.NET 10), Proc.Web (React 19 + TypeScript + Vite + Ant Design + TanStack Query),
  Proc.Domain.Tests, Proc.IntegrationTests.
- Kết nối PostgreSQL qua EF Core 10 + Npgsql (`ProcDbContext`, chưa có bảng).
- Endpoint `GET /health` kiểm tra kết nối CSDL, trả trạng thái và số phiên bản.
- Log kỹ thuật bằng Serilog (ghi ra console).
- Test kiến trúc (Domain/Application không tham chiếu hạ tầng) và test tích hợp `/health`
  trên database `procurement_test`.
- `Directory.Build.props` (phiên bản 0.1.0), `Directory.Packages.props` (quản lý gói tập trung),
  `global.json` (dùng Microsoft.Testing.Platform cho `dotnet test`), công cụ local `dotnet-ef`.
- Khung giao diện theo docs/ui-guide.md: `AppShell` (thanh menu có Alt + chữ gạch chân, thanh công cụ
  có ô lệnh, cây điều hướng có nhóm Ghim và số đếm, kéo rộng/ẩn được, thanh trạng thái hiện phiên bản),
  `ListPage` (tìm kiếm có/không dấu, bộ lọc, Nhóm theo, chọn cột, xuất CSV) và `DataGrid`
  (sắp xếp, kéo đổi rộng/thứ tự cột, kéo cột để nhóm, trạng thái chấm màu, phím mũi tên/Enter,
  cuộn ảo trên 500 dòng).
- Màn hình Danh sách đơn mua hàng (PO) với 30 dòng dữ liệu giả.
- Test giao diện Playwright (`npm run test:e2e` trong `src\Proc.Web`).
- Đăng nhập (design.md mục 9.4): ASP.NET Core Identity + JWT 8 giờ, khóa tạm 15 phút sau 5 lần sai,
  thông báo lỗi tiếng Việt; màn hình Đăng nhập; mọi API bắt buộc đăng nhập trừ `/health`.
- Danh mục Công ty, Vai trò và gán vai trò theo công ty (UserCompanyRole); chọn công ty làm việc
  trên thanh menu, gửi kèm mọi request qua header `X-Company-Id`; menu người dùng có Đăng xuất.
- Tự điền CreatedAt/By, UpdatedAt/By khi lưu danh mục.

### Thay đổi
- Không có.

### Sửa lỗi
- Không có.

### Thay đổi CSDL
- Migration `V0_1_0_DangNhapVaCongTy`: bảng `Company`, `Role`, `UserCompanyRole`, `User`, `UserClaim`,
  `UserLogin`, `UserToken`. `Down()` xóa các bảng này cùng dữ liệu.

### Lưu ý khi nâng cấp
- Cần tạo sẵn database `procurement` và `procurement_test`, user `proc` (README.md).
- Server thật: đặt biến môi trường `Jwt__SigningKey` (tối thiểu 32 ký tự) và `Seed__AdminPassword`;
  nâng cấp CSDL bằng script `db/scripts/0.1.0.sql` (dev/test tự chạy migration khi khởi động).

### Cách quay lại bản trước
- Đây là bản đầu tiên.
