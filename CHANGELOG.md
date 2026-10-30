# Nhật ký thay đổi

Ghi theo [Semantic Versioning](https://semver.org/lang/vi/) – xem CLAUDE.md mục 8.
Mỗi bản phát hành có Git tag `vx.y.z`.

## [Chưa phát hành]

### Thêm mới
- Màn hình Hệ thống → Công ty (COMP) và Hệ thống → Kho (WH) theo khuôn danh sách + ngăn kéo chi tiết (design.md mục 5.1, 5.6):
  thêm công ty kèm kho tổng ở tổng công ty, người thêm được gán vào công ty mới; sửa thông tin, mã số thuế, ngày bắt đầu,
  ngừng dùng; thêm kho phụ, đổi kho cha (chặn vòng lặp), thủ kho, địa chỉ, cho phép tồn âm, ngừng dùng.
- Màn hình Giá và NCC → Hàng hóa (ITEM), Nhóm hàng (CAT), Đơn vị tính (UOM) (design.md mục 5.2, 5.7): hàng hóa có loại hàng,
  nhóm hàng, ĐVT cơ sở, quy cách, thương hiệu, xuất xứ, mã vạch, thuế suất mặc định, quản lý lô/hạn dùng, tồn tối thiểu/tối đa,
  quy đổi ĐVT, thông số kỹ thuật; tìm không dấu ở server, lọc theo nhóm (gồm nhóm con) và loại hàng; nhóm hàng dạng cây có
  tài khoản kho mặc định. Thêm, sửa ở tổng công ty; hàng hóa lọc theo phạm vi nhóm hàng.
- API `/api/uoms`, `/api/item-categories` (thêm POST, PUT; GET trả thêm đường dẫn, cấp, tài khoản kho), `/api/items`.
- Trùng mã duy nhất trong CSDL (kể cả bản ghi ngoài phạm vi người dùng) trả 409 kèm thông báo tiếng Việt thay vì lỗi hệ thống.
- Dữ liệu mẫu dev/test: 7 đơn vị tính.
- API `GET/POST /api/companies`, `GET/PUT /api/companies/{id}`, `POST /api/warehouses`, `GET/PUT /api/warehouses/{id}`;
  `GET /api/warehouses` trả thêm kho cha, địa chỉ, thủ kho, cho phép tồn âm.

### Thay đổi
- Thanh menu cập nhật danh sách công ty ngay sau khi thêm/sửa công ty (không phải đăng nhập lại).

### Thay đổi CSDL
- Migration `V0_2_0_CongTyKho`: chỉ mục duy nhất mã số thuế công ty (bỏ qua công ty chưa có mã số thuế).
  `Down()` chỉ bỏ chỉ mục, không mất dữ liệu.

- Migration `V0_2_0_HangHoa`: bảng `Uom`, `Item`, `ItemUomConversion`; cột `InventoryAccount` của `ItemCategory`.
  `Down()` xóa ba bảng và cột đó – mất toàn bộ hàng hóa, đơn vị tính, quy đổi đã nhập.

### Lưu ý khi nâng cấp
- Nếu đã có hai công ty trùng mã số thuế thì migration lỗi – sửa dữ liệu trước khi nâng cấp.

## [0.1.0] - 2026-10-30

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
- Phân quyền (design.md mục 9.1, 9.2): danh mục 42 quyền dạng Module.Đối tượng.Hành động, quyền theo vai trò,
  vai trò ADMIN luôn đủ quyền; phạm vi kho và nhóm hàng (kế thừa nhóm con) theo từng công ty;
  `ICurrentUserScope` nạp theo header `X-Company-Id` (công ty không được gán → 403);
  global query filter theo công ty/kho/nhóm hàng, mặc định từ chối khi chưa nạp phạm vi.
- Danh mục Kho (Main/Sub, mỗi công ty một kho tổng) và Nhóm hàng (dạng cây) ở mức dữ liệu; API
  `GET /api/warehouses`, `GET /api/item-categories`.
- Đổi mật khẩu (`POST /api/auth/change-password`, hộp thoại trong menu người dùng); thông báo lỗi mật khẩu tiếng Việt.
- Giao diện ẩn màn hình không có quyền trên menu, cây điều hướng, ô lệnh; mở thẳng đường dẫn thì báo không có quyền.
- Dữ liệu mẫu dev/test: kho cho 4 công ty, cây nhóm hàng, vai trò Thủ kho và Nhân viên mua hàng, 2 tài khoản mẫu.
- Màn hình Người dùng và Vai trò và phân quyền (design.md mục 9.5, 10.9): danh sách + ngăn kéo chi tiết có tab;
  tạo tài khoản, sửa thông tin, ngừng dùng, đặt lại mật khẩu (mở khóa tạm); gán vai trò, kho, nhóm hàng tại công ty
  đang làm việc; tạo/sửa vai trò và tick quyền theo module. Chặn tự nâng quyền: chỉ gán vai trò và cấp quyền nằm trong
  quyền của chính mình, không tự sửa quyền của mình, vai trò ADMIN không sửa được.
- API `/api/users`, `/api/roles`, `/api/permissions`; lỗi không tìm thấy trả 404 tiếng Việt.
- Component dùng chung `DetailDrawer` (ngăn kéo chi tiết của form danh mục, Ctrl+S lưu, Esc đóng).
- Nhật ký thao tác (design.md mục 9.3): mọi thay đổi của entity `[Auditable]` (công ty, vai trò, quyền của vai trò,
  kho, nhóm hàng, gán vai trò, phạm vi, tài khoản) tự ghi vào `AuditLog` trong cùng giao dịch, kèm người, công ty,
  IP, trình duyệt, mã liên kết request và giá trị cũ/mới; mã băm mật khẩu chỉ ghi `***`.
- Nhật ký truy cập: đăng nhập, đăng nhập sai, bị khóa tạm, đăng xuất, đổi mật khẩu, đổi công ty làm việc
  (API `POST /api/auth/logout`, `POST /api/auth/working-company`).
- Màn hình Nhật ký thao tác (lọc khoảng ngày, loại đối tượng, hành động; xem trước/sau) và Nhật ký truy cập;
  API `GET /api/audit-logs`, `GET /api/audit-logs/{id}`, `GET /api/access-logs`.
- Đánh số chứng từ (design.md mục 5.5): mẫu số theo công ty và loại chứng từ (mặc định `{LOẠI}-{CTY}-{YYMM}-{0000}`,
  ví dụ PO-BHA-2610-0001), đếm lại theo tháng/năm của ngày chứng từ; cấp số nguyên tử trong giao dịch của chứng từ
  (không trùng khi lưu đồng thời, lưu lỗi thì trả lại số). Màn hình Hệ thống → Đánh số chứng từ; API `/api/document-numbering`.
- Triển khai server bằng Docker (design.md mục 14): `Dockerfile` (một image chứa API + giao diện React đã build,
  chạy bằng user không phải root), `docker-compose.yml` (Caddy HTTPS, app, PostgreSQL 18, Seq xem log, sao lưu
  `pg_dump` mỗi đêm vào `./backups` giữ 14 ngày), `Caddyfile`, `.env.example`; hướng dẫn cài và nâng cấp trong README.
- API phục vụ giao diện khi có `wwwroot` (đường dẫn của giao diện đều trả `index.html`, không lưu đệm `index.html`);
  `/api` không tồn tại trả 404. Log kỹ thuật gửi được sang Seq (gói `Serilog.Sinks.Seq`).

### Thay đổi
- Không có.

### Sửa lỗi
- Cùng một tài khoản đăng nhập đồng thời (nhiều tab/máy, có lần sai mật khẩu) có thể trả lỗi hệ thống 500 do xung đột
  ghi bộ đếm sai mật khẩu; nay tự đọc lại và thử lại.
- Script SQL dạng `--idempotent` lỗi "query has no destination for result data" ở migration `V0_1_0_NhatKyThaoTac`
  (lời gọi tạo phân vùng nằm trong khối DO); nay gọi qua `PERFORM`.

### Thay đổi CSDL
- Migration `V0_1_0_DangNhapVaCongTy`: bảng `Company`, `Role`, `UserCompanyRole`, `User`, `UserClaim`,
  `UserLogin`, `UserToken`. `Down()` xóa các bảng này cùng dữ liệu.
- Migration `V0_1_0_PhanQuyenVaPhamVi`: bảng `Permission`, `RolePermission`, `Warehouse`, `ItemCategory`,
  `UserWarehouseScope`, `UserItemCategoryScope`. `Down()` xóa các bảng này cùng dữ liệu.
- Migration `V0_1_0_NhatKyThaoTac`: bảng `AuditLog` (phân vùng theo tháng, phân vùng mặc định `AuditLog_default`,
  hàm `audit_log_ensure_partitions`) và `AccessLog`; trigger chặn UPDATE/DELETE trên cả hai bảng.
  `Down()` xóa hai bảng nhật ký cùng toàn bộ nhật ký đã ghi.
- Migration `V0_1_0_DanhSoChungTu`: bảng `DocumentNumbering` (mẫu số theo công ty) và `DocumentSequence` (bộ đếm theo
  công ty, loại chứng từ, kỳ). `Down()` xóa hai bảng – mất cấu hình mẫu số và số hiện tại.
- Script cài mới `db/scripts/0.1.0.sql` (gồm cả 4 migration trên, dạng idempotent – chạy lại nhiều lần không sao).

### Lưu ý khi nâng cấp
- Cần tạo sẵn database `procurement` và `procurement_test`, user `proc` (README.md).
- Server thật (cài mới bằng Docker, README.md mục "Triển khai server"): copy `.env.example` thành `.env`, đặt
  `JWT_SIGNING_KEY` (tối thiểu 32 ký tự), `ADMIN_PASSWORD`, mật khẩu CSDL và Seq; tạo bảng bằng `db/scripts/0.1.0.sql`
  trước khi khởi động app (dev/test tự chạy migration khi khởi động). Đăng nhập lần đầu rồi đổi mật khẩu quản trị ngay.
- Bản đầu tiên, chưa có dữ liệu cũ nên không cần sao lưu trước khi cài.
- API tạo sẵn phân vùng nhật ký cho tháng hiện tại và 2 tháng tới mỗi lần khởi động; server chạy liên tục
  nhiều tháng không khởi động lại thì nhật ký vào phân vùng mặc định (không lỗi) – sẽ thêm job hằng tháng.

### Cách quay lại bản trước
- Đây là bản đầu tiên, không có bản trước để quay lại. Gỡ hẳn: `docker compose down` (giữ dữ liệu) hoặc
  `docker compose down -v` (xóa luôn CSDL – chỉ làm khi đã sao lưu).
