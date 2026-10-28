# Procurement & Inventory – quy tắc dự án

Phần mềm quản lý nhà cung cấp, giá, mua hàng, đấu thầu và kho cho tổng công ty và các công ty thành viên.

Tài liệu bắt buộc đọc trước khi làm mỗi tính năng (đọc đúng mục liên quan):
- `docs/design.md` – đặc tả nghiệp vụ, form, CSDL, lộ trình (bản gốc của thiết kế).
- `docs/ui-guide.md` – đặc tả giao diện; ảnh mẫu là các file .png trong `docs/`.

## 1. Giao tiếp
- Luôn trả lời, lập kế hoạch và báo cáo cho tôi bằng tiếng Việt có dấu.
- Trước mỗi tính năng: trình bày kế hoạch ngắn (việc sẽ làm, file sẽ sửa, test sẽ viết) rồi mới code.
- Hỏi tôi trước khi: xóa file hoặc dữ liệu, đổi cấu trúc thư mục lớn, đổi công nghệ/thư viện chính,
  thêm thư viện trả phí, sửa quy tắc nghiệp vụ trong docs/design.md mà tôi chưa yêu cầu.
- Gặp yêu cầu mâu thuẫn với docs/design.md: dừng lại, nêu rõ mâu thuẫn và hỏi tôi.

## 2. Công nghệ
- Backend: .NET 10, ASP.NET Core Web API, EF Core 10 + Npgsql, PostgreSQL 18, FluentValidation,
  Hangfire, Serilog, ClosedXML (Excel), QuestPDF (in phiếu).
- Frontend: React 19 + TypeScript + Vite + Ant Design + TanStack Query.
- Test: xUnit + Testcontainers (PostgreSQL thật); Playwright cho giao diện
  (lần đầu cài trình duyệt: `npx playwright install --with-deps chromium`).
- Chỉ dùng thư viện miễn phí/mã nguồn mở. Không dùng AG Grid Enterprise hoặc thư viện cần license trả phí.
- Môi trường dev: GitHub Codespaces. CSDL chạy bằng `docker-compose.dev.yml`:
  `Host=localhost;Port=5432;Database=procurement;Username=proc;Password=proc_dev_only`
  (mật khẩu này chỉ dùng cho dev).
- Cổng cố định: API http://localhost:5000, Web (Vite) http://localhost:5173.

## 3. Kiến trúc
- Modular monolith, Clean Architecture: Domain → Application → Infrastructure → Api, cộng Proc.Web.
- Cấu trúc thư mục theo mục 13.1 của docs/design.md. Module chỉ gọi nhau qua interface của lớp Application.
- Ghi sổ kho là nguyên tử: lưu chứng từ, ghi StockLedger, cập nhật StockOnHand, ghi audit trong một transaction.

## 4. Quy tắc nghiệp vụ bất biến
- Mỗi công ty là pháp nhân độc lập. Mọi bảng giao dịch có CompanyId.
- Luôn đi qua ICurrentUserScope; không truy vấn bỏ qua global query filter (công ty, kho, nhóm hàng).
  Lệnh ghi phải kiểm tra lại phạm vi ở tầng Application, không tin dữ liệu từ client.
- Mỗi công ty có đúng một kho Main; kho Sub thuộc một công ty.
- Liên công ty chỉ đi kho Main → kho Main, qua cặp chứng từ ICS (xuất bán nội bộ) / ICR (nhập mua nội bộ),
  giá = giá bán nội bộ (docs/design.md mục 4, 6.3).
- Kho Sub chỉ chuyển trong cùng công ty (TRF). Cấm chuyển kho Sub sang bất kỳ kho nào của công ty khác.
- Chỉ ghi sổ khi ngày chứng từ thuộc kỳ Open của công ty đó. Chứng từ đã ghi sổ không sửa trực tiếp:
  bỏ ghi sổ khi kỳ còn mở, hoặc lập chứng từ điều chỉnh khi kỳ đã đóng.
- Đóng kỳ tuần tự (tháng N chỉ đóng khi tháng N−1 đã đóng); chỉ mở lại kỳ đã đóng gần nhất.
- Giá vốn: bình quân gia quyền cuối kỳ tháng theo Company × Item (docs/design.md mục 7).
- Không cho tồn âm (trừ khi tham số kho cho phép).
- Kiểu số: số lượng numeric(18,4), đơn giá numeric(19,6), tiền numeric(19,2). Không dùng double/float.
- Mọi thay đổi entity [Auditable] phải vào AuditLog qua SaveChangesInterceptor; AuditLog chỉ thêm, không sửa/xóa.

## 5. Quy ước hiển thị
- Giao diện hoàn toàn tiếng Việt. Tên code, bảng, cột, API bằng tiếng Anh.
- Ngày dd/MM/yyyy; giờ HH:mm; múi giờ Asia/Ho_Chi_Minh (lưu UTC trong CSDL, ngày chứng từ kiểu date).
- Số theo kiểu Việt Nam: 1.234.567,89; tiền VND hiển thị không số lẻ.
- Tìm kiếm phải tìm được cả khi gõ có dấu hoặc không dấu.

## 6. Ghi chú trong code (bắt buộc)
- Mọi ghi chú viết bằng tiếng Việt có dấu; file lưu UTF-8.
- Mỗi lớp và hàm public có XML doc comment (`/// <summary>`): làm gì, tham số, giá trị trả về, ngoại lệ.
- Mỗi quy tắc nghiệp vụ ghi rõ lý do và mục tương ứng trong docs/design.md
  (ví dụ: `// Kho Sub không được chuyển sang công ty khác – xem design.md mục 4`).
- Đoạn tính toán phức tạp (giá vốn, phân bổ chi phí, làm tròn) ghi công thức và ví dụ số.
- React/TypeScript: JSDoc (`/** ... */`) tiếng Việt cho component và hàm.
- Ghi chú giải thích "tại sao", không lặp lại tên hàm; không để ghi chú lỗi thời sau khi sửa code.

## 7. Cách làm việc với Git
- Mỗi tính năng hoặc sửa lỗi làm trên nhánh riêng: `feature/<ten-ngan>` hoặc `fix/<ten-ngan>`.
- Làm từng lát nhỏ. Mỗi lát: viết test trước → code → `dotnet build` và `dotnet test` (và `npm run build`
  nếu có sửa Proc.Web) đều chạy qua → commit với ghi chú tiếng Việt → `git push`.
- Khi tính năng hoàn tất và test chạy qua: merge nhánh vào `main`, push `main`, rồi xóa nhánh.
  `main` luôn ở trạng thái build và test chạy được.
- Trước khi bắt đầu việc mới: `git pull` để lấy thay đổi tôi có thể đã sửa trên GitHub.
- Không commit mật khẩu thật, khóa API, file `.env`, thư mục `bin/`, `obj/`, `node_modules/`.
- Cấm: `git push --force`, `git reset --hard` trên main, xóa hoặc sửa tag đã tạo, viết lại lịch sử commit.
- Khi đổi quy tắc nghiệp vụ hoặc cấu trúc dữ liệu: cập nhật docs/design.md trong cùng commit với code.

## 8. Quản lý phiên bản và quay lại bản cũ (bắt buộc)
- Số phiên bản MAJOR.MINOR.PATCH (Semantic Versioning):
  PATCH = sửa lỗi; MINOR = thêm chức năng; MAJOR = thay đổi lớn, 1.0.0 = bản chạy thật đầu tiên.
- Số phiên bản ghi trong `Directory.Build.props` (thẻ `<Version>`) và hiện ở thanh trạng thái của giao diện.
- Chỉ phát hành khi tôi yêu cầu. Mỗi lần phát hành làm đủ:
  1. Cập nhật `CHANGELOG.md` (tiếng Việt), mẫu `## [x.y.z] - YYYY-MM-DD` với các mục:
     Thêm mới / Thay đổi / Sửa lỗi / Thay đổi CSDL / Lưu ý khi nâng cấp / Cách quay lại bản trước.
  2. Cập nhật `<Version>` trong `Directory.Build.props`.
  3. Commit `Phát hành phiên bản x.y.z`.
  4. `git tag -a vx.y.z -m "Phiên bản x.y.z: <tóm tắt>"`.
  5. `git push` và `git push --tags`.
  6. Báo cho tôi: số phiên bản, tóm tắt thay đổi, có thay đổi CSDL hay không, có cần sao lưu trước khi nâng cấp không.
- Thay đổi CSDL chỉ qua EF Core migration, tên rõ nghĩa (ví dụ `V0_4_0_ThemBangGiaNoiBo`),
  luôn có hàm `Down()` gỡ được; ghi chú nếu `Down()` làm mất dữ liệu.
- Không xóa cột/bảng đang có dữ liệu trong cùng một bản: đánh dấu ngừng dùng trước, xóa ở bản sau.
- Mỗi bản có thay đổi CSDL: sinh script `dotnet ef migrations script <bản-trước> <bản-mới>`
  vào `db/scripts/<phiên-bản>.sql`.
- Khi tôi yêu cầu "quay về phiên bản X":
  1. Không xóa lịch sử. Liệt kê thay đổi giữa bản hiện tại và bản X (code và migration),
     nêu rủi ro mất dữ liệu, chờ tôi đồng ý.
  2. Sau khi tôi đồng ý: tạo nhánh `rollback/vX` từ tag X hoặc dùng `git revert` cho các commit cần gỡ;
     chạy build và test.
  3. Chỉ gỡ migration (`dotnet ef database update <migration-của-bản-X>`) khi tôi xác nhận đã sao lưu CSDL.

## 9. Giao diện (bắt buộc)
- Phong cách ứng dụng desktop kiểu Windows XP theo `docs/ui-guide.md`.
  Ảnh mẫu: `docs/mau-man-hinh-danh-sach.png` – xem ảnh trước khi làm bất kỳ màn hình nào.
- Mọi trang nằm trong `AppShell`; mọi danh sách dùng `ListPage` + `DataGrid`;
  mọi chứng từ dùng `DocumentForm` (docs/design.md mục 10.1). Không tự tạo bố cục riêng cho từng màn hình.
- Màu, cỡ chữ, khoảng cách lấy từ theme token của Ant Design `ConfigProvider`; không viết màu cứng trong component.
- Ưu tiên mật độ thông tin cao, gọn, thao tác được bằng bàn phím.
- Làm xong một màn hình: chạy lên, chụp bằng Playwright, so với ảnh mẫu, sửa đến khi giống bố cục.
- Khi tôi đưa ảnh mẫu mới vào `docs/`, làm theo đúng ảnh đó.
