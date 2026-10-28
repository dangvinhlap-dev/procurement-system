# Procurement & Inventory – quy tắc dự án

Tài liệu thiết kế đầy đủ: `docs/design.md`. Đọc mục liên quan trước khi làm mỗi tính năng.

## Công nghệ
- .NET 10, ASP.NET Core Web API, EF Core 10 + Npgsql, PostgreSQL 18.
- Frontend: React 19 + TypeScript + Vite + Ant Design + TanStack Query.
- Test: xUnit + Testcontainers (PostgreSQL thật).
- Môi trường dev: GitHub Codespaces; CSDL chạy bằng `docker-compose.dev.yml`
  (Host=localhost;Port=5432;Database=procurement;Username=proc;Password=proc_dev_only).

## Kiến trúc
- Modular monolith, Clean Architecture: Domain → Application → Infrastructure → Api, cộng Proc.Web.
- Cấu trúc thư mục theo mục 13.1 của docs/design.md.

## Quy tắc nghiệp vụ bất biến
- Mỗi công ty là pháp nhân độc lập. Mọi bảng giao dịch có CompanyId.
- Luôn đi qua ICurrentUserScope; không truy vấn bỏ qua global query filter (công ty, kho, nhóm hàng).
- Liên công ty chỉ đi kho Main → kho Main, qua cặp chứng từ ICS (xuất bán nội bộ) / ICR (nhập mua nội bộ).
- Kho Sub chỉ chuyển trong cùng công ty (TRF). Cấm chuyển kho Sub sang kho của công ty khác.
- Chỉ ghi sổ khi ngày chứng từ thuộc kỳ Open. Chứng từ đã ghi sổ không sửa trực tiếp.
- Giá vốn: bình quân gia quyền cuối kỳ tháng theo Company × Item (mục 7 docs/design.md).
- Số lượng numeric(18,4), đơn giá numeric(19,6), tiền numeric(19,2). Không dùng double/float cho tiền, số lượng.
- Mọi thay đổi entity [Auditable] phải vào AuditLog qua SaveChangesInterceptor.

## Cách làm việc
- Mỗi tính năng: lập kế hoạch trước, viết test trước, chạy `dotnet test` trước khi báo xong.
- Làm từng lát nhỏ, commit sau mỗi lát chạy được.
- Giao diện tiếng Việt; tên code, bảng, cột bằng tiếng Anh.
- Không commit mật khẩu thật hay file .env.
## Ghi chú trong code (bắt buộc)
- Mọi ghi chú (comment) viết bằng tiếng Việt có dấu, file lưu UTF-8.
- Tên biến, hàm, lớp, bảng, cột vẫn bằng tiếng Anh; chỉ phần ghi chú là tiếng Việt.
- Mỗi lớp và mỗi hàm public có XML doc comment (`/// <summary>`) giải thích: làm gì, tham số, giá trị trả về, ngoại lệ.
- Mỗi quy tắc nghiệp vụ trong code ghi rõ lý do và mục tương ứng trong docs/design.md
  (ví dụ: `// Kho Sub không được chuyển sang công ty khác – xem design.md mục 4`).
- Đoạn tính toán phức tạp (giá vốn, phân bổ chi phí, làm tròn) ghi công thức và ví dụ số.
- React/TypeScript: dùng JSDoc (`/** ... */`) tiếng Việt cho component và hàm.
- Không ghi chú thừa kiểu lặp lại tên hàm; ghi chú phải giải thích "tại sao", không chỉ "làm gì".
- Ghi chú commit (commit message) cũng viết bằng tiếng Việt.
## Quản lý phiên bản và quay lại bản cũ (bắt buộc)

### Nhánh làm việc
- Không code trực tiếp trên `main`. Mỗi tính năng/sửa lỗi làm trên nhánh riêng:
  `feature/<ten-ngan>` hoặc `fix/<ten-ngan>`.
- Chỉ gộp (merge) vào `main` khi `dotnet build` và `dotnet test` đều chạy qua.
- `main` luôn ở trạng thái chạy được.
- Tuyệt đối không dùng `git push --force`, `git reset --hard` trên `main`,
  không xóa hay sửa tag đã tạo, không viết lại lịch sử commit.

### Đánh số phiên bản (Semantic Versioning: MAJOR.MINOR.PATCH)
- PATCH (0.3.0 → 0.3.1): sửa lỗi, không đổi chức năng.
- MINOR (0.3.1 → 0.4.0): thêm chức năng, tương thích ngược.
- MAJOR (0.x → 1.0.0): thay đổi lớn, không tương thích; 1.0.0 = bản chạy thật đầu tiên.
- Số phiên bản ghi trong `Directory.Build.props` (thẻ `<Version>`) và hiện ở chân trang giao diện.

### Mỗi lần phát hành phiên bản mới, làm đủ các bước
1. Cập nhật `CHANGELOG.md` (tiếng Việt) theo mẫu:
   `## [0.4.0] - YYYY-MM-DD` với các mục: Thêm mới / Thay đổi / Sửa lỗi /
   Thay đổi CSDL / Lưu ý khi nâng cấp / Cách quay lại bản trước.
2. Cập nhật `<Version>` trong `Directory.Build.props`.
3. Commit: `Phát hành phiên bản 0.4.0`.
4. Tạo tag có ghi chú: `git tag -a v0.4.0 -m "Phiên bản 0.4.0: <tóm tắt>"`.
5. `git push` và `git push --tags`.
6. Báo cho tôi: số phiên bản, tóm tắt thay đổi, có thay đổi CSDL hay không.

### Thay đổi CSDL
- Mọi thay đổi cấu trúc qua EF Core migration, đặt tên rõ nghĩa
  (ví dụ `V0_4_0_ThemBangGiaNoiBo`).
- Mỗi migration phải có hàm `Down()` gỡ được thay đổi; ghi chú nếu `Down()` làm mất dữ liệu.
- Không xóa cột/bảng có dữ liệu ngay trong một bản; đánh dấu ngừng dùng trước,
  xóa ở bản sau.
- Sinh script SQL cho mỗi bản: `dotnet ef migrations script <bản-trước> <bản-mới>`
  lưu vào `db/scripts/<phiên-bản>.sql`.
- Trong CHANGELOG ghi rõ: cần sao lưu CSDL trước khi nâng cấp.

### Khi tôi yêu cầu "quay về phiên bản X"
- Không xóa lịch sử. Liệt kê cho tôi các thay đổi giữa bản hiện tại và bản X
  (code và migration), nêu rủi ro mất dữ liệu, chờ tôi đồng ý.
- Sau khi tôi đồng ý: tạo nhánh `rollback/vX` từ tag X, hoặc dùng `git revert`
  cho các commit cần gỡ; chạy build và test; gỡ migration bằng `dotnet ef database update <migration-của-bản-X>`
  chỉ khi tôi xác nhận đã sao lưu CSDL.
## Giao diện (bắt buộc)
- Mọi màn hình theo phong cách ứng dụng desktop kiểu Windows XP, đặc tả tại docs/ui-guide.md;
  ảnh mẫu tại docs/ui/mau-man-hinh-danh-sach.png. Xem ảnh mẫu trước khi làm bất kỳ màn hình nào.
- Mọi trang nằm trong AppShell; mọi danh sách dùng ListPage + DataGrid; mọi chứng từ dùng DocumentForm.
  Không tự tạo bố cục riêng cho từng màn hình.
- Màu, cỡ chữ, khoảng cách lấy từ theme token; không viết màu cứng trong component.
- Ưu tiên mật độ thông tin cao, gọn, thao tác bằng bàn phím.
- Sau khi làm xong một màn hình, chụp màn hình (Playwright) và so với ảnh mẫu, sửa đến khi giống bố cục.
