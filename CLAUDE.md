# Procurement & Inventory – quy tắc dự án

Khi bắt đầu phiên mới, đọc docs/TIEN_DO.md để biết tiến độ và tiếp tục từ đó.

Phần mềm quản lý nhà cung cấp, giá, mua hàng, đấu thầu và kho cho tổng công ty và các công ty thành viên.

Đọc đúng mục liên quan trước khi làm (dùng grep tìm mục, không đọc cả file):
- Nghiệp vụ, form, CSDL, lộ trình: `docs/design.md` (bản gốc của thiết kế).
- Giao diện: `docs/ui-style.md` – phong cách ERP desktop cổ điển (nạp sẵn ở cuối file này); ảnh mẫu `docs/imageSAP.png`.
  `docs/ui-guide.md` (Fiori) chỉ còn là lịch sử/tham chiếu hành vi.
- Khi phát hành hoặc quay lại phiên bản cũ: đọc `docs/phat-hanh.md` (bắt buộc làm đủ các bước trong đó).
- Khi sửa cách triển khai (Docker, Windows/IIS), cấu hình, đường dẫn file: đọc `docs/deploy-windows.md` và README mục "Triển khai server".
- Khi chạy, cài đặt, gỡ rối môi trường dev (chuỗi kết nối, cổng, migration, lỗi khóa file): đọc `docs/moi-truong-dev.md`.

## 0. Cách làm việc tiết kiệm token
- Tìm bằng grep/tìm kiếm có phạm vi (thư mục, mẫu file); chỉ đọc đoạn file cần, không đọc cả file lớn.
- Chạy build/test và chỉ báo phần lỗi (lọc kết quả), không in toàn bộ.
- Trả lời ngắn gọn, không lặp lại code vừa viết.
- Xong mỗi lát: cập nhật docs/TIEN_DO.md, commit, rồi nhắc người dùng dùng /clear.

## 1. Giao tiếp
- Luôn trả lời, lập kế hoạch và báo cáo cho tôi bằng tiếng Việt có dấu.
- Trước mỗi tính năng: trình bày kế hoạch ngắn (việc sẽ làm, file sẽ sửa, test sẽ viết) rồi mới code.
- Hỏi tôi trước khi: xóa file hoặc dữ liệu, đổi cấu trúc thư mục lớn, đổi công nghệ/thư viện chính,
  thêm thư viện trả phí, sửa quy tắc nghiệp vụ trong docs/design.md mà tôi chưa yêu cầu.
- Gặp yêu cầu mâu thuẫn với docs/design.md: dừng lại, nêu rõ mâu thuẫn và hỏi tôi.

## 2. Công nghệ và lệnh
- Backend: .NET 10, ASP.NET Core Web API, EF Core 10 + Npgsql, PostgreSQL 18, FluentValidation, Serilog,
  ClosedXML (Excel), PDFsharp-MigraDoc 6.2.4 – bản MIT (in phiếu).
- Frontend: React 19 + TypeScript + Vite + Ant Design + TanStack Query. Test: xUnit (tích hợp trên PostgreSQL thật,
  database `procurement_test`), Playwright cho giao diện.
- Chỉ dùng thư viện miễn phí/mã nguồn mở. Không dùng AG Grid Enterprise hoặc thư viện cần license trả phí.
- Lệnh chạy trong PowerShell; đường dẫn file dùng kiểu Windows khi hướng dẫn tôi. Cổng: API 5000, Web 5173.
- Kiểm tra mỗi lát (thư mục gốc): `dotnet build`, `dotnet test`; trong `src/Proc.Web`: `npm run build`, `npx tsc -b`,
  `npx oxlint src e2e`, `npx playwright test`.

## 3. Kiến trúc
- Modular monolith, Clean Architecture: Domain → Application → Infrastructure → Api, cộng Proc.Web.
- Cấu trúc thư mục theo mục 13.1 của docs/design.md. Module chỉ gọi nhau qua interface của lớp Application.
- Ghi sổ kho là nguyên tử: lưu chứng từ, ghi StockLedger, cập nhật StockOnHand, ghi audit trong một transaction.
- Chạy được cả trên Linux container (Docker) và Windows Server (IIS) từ cùng một bản build, không sửa code:
  - Không viết cứng đường dẫn, host, cổng, chuỗi kết nối trong code; lấy từ appsettings/biến môi trường (Options), có giá trị mặc định
    cho máy dev. Đường dẫn tương đối tính từ ContentRoot, không từ thư mục đang đứng.
  - Ghép đường dẫn bằng `Path.Combine`/`Path.GetFullPath`, không nối chuỗi với `/` hay `\`; tên file so sánh theo hệ điều hành
    (Linux phân biệt hoa thường). Dữ liệu ghi ra đĩa (đính kèm, log) để ngoài thư mục ứng dụng trên server.
  - Không dùng API chỉ có trên một hệ điều hành (registry, tên múi giờ chỉ Linux/Windows – dùng `VietnamTime.Zone`); script triển khai
    có bản cho cả hai (`db/backup.sh` – `deploy/windows/backup.ps1`). Script `.ps1` lưu UTF-8 có BOM.

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
- Ghi chú tiếng Việt có dấu, file UTF-8. Ghi chú giải thích "tại sao"; không để ghi chú lỗi thời sau khi sửa code.
- Lớp và hàm public có XML doc (`/// <summary>`: làm gì, tham số, trả về, ngoại lệ); React/TypeScript có JSDoc (`/** */`).
- Quy tắc nghiệp vụ ghi lý do và mục design.md (ví dụ `// Kho Sub không được chuyển sang công ty khác – xem design.md mục 4`).
- Tính toán phức tạp (giá vốn, phân bổ chi phí, làm tròn) ghi công thức và ví dụ số.

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

## 8. Phiên bản và CSDL (bắt buộc – chi tiết ở docs/phat-hanh.md)
- Semantic Versioning, số phiên bản trong `Directory.Build.props`. Chỉ phát hành khi tôi yêu cầu, làm đủ các bước trong docs/phat-hanh.md.
- Thay đổi CSDL chỉ qua EF Core migration tên rõ nghĩa (ví dụ `V0_4_0_ThemBangGiaNoiBo`), luôn có `Down()`; ghi chú nếu `Down()` mất dữ liệu.
- Không xóa cột/bảng đang có dữ liệu trong cùng một bản: đánh dấu ngừng dùng trước, xóa ở bản sau.
- "Quay về phiên bản X": không xóa lịch sử, liệt kê thay đổi và rủi ro, chờ tôi đồng ý; chỉ gỡ migration khi tôi xác nhận đã sao lưu.

## 9. Giao diện (bắt buộc)
- Từ 01/11/2026: **phong cách ERP desktop cổ điển** theo `docs/ui-style.md` (ảnh mẫu `docs/imageSAP.png` – chỉ học phong cách, không dùng
  logo/tên/nhận diện thương hiệu). Thay cho SAP Fiori (`docs/ui-guide.md` chỉ còn là lịch sử). Giữ React + Ant Design.
- Toàn bộ ứng dụng đã mang phong cách cổ điển (01/11/2026) qua khung dùng chung: mọi trang nằm trong `AppShell` (thanh menu chữ + thanh
  công cụ + cây điều hướng); mọi danh sách dùng `ListPage` + `DataGrid`; mọi chứng từ dùng `DocumentForm` + `LineGrid` (design.md mục 10.1);
  ngăn kéo chi tiết dùng `DetailDrawer`. Hình khối cổ điển của các component này nằm ở `src/classic/classic-app.css` + `src/theme.ts`.
  Không tự tạo bố cục riêng cho từng màn hình.
- Component trong `src/Proc.Web/src/classic/` (`TabStrip`, `EntryGrid`/`SummaryRow`/`GridButtons`, `StatusLight`, `StatusBar`, `DateInput`,
  `HeaderFields`/`Field`/`Section`, `CommandButton`, `ClassicScreen`…) dùng khi màn hình cần đúng các thành phần đó; mẫu chuẩn về hình thức:
  `/thu-giao-dien/tao-po`. Thiếu thành phần thì bổ sung vào `classic/` và `docs/ui-style.md` trước.
- Màu, cỡ chữ, khoảng cách lấy từ theme token (`src/theme.ts`) và biến `--erp-*` (`classic-app.css`, `classic.css`); không viết màu cứng trong
  component. Icon trong nút tự có màu theo mức quan trọng (đỏ: xóa/hủy/đóng/ngừng/bỏ ghi sổ; xanh lá: lưu/thêm/duyệt/gửi; vàng: còn lại);
  mọi ô ngày dùng `format={DATE_INPUT_FORMATS}` (gõ tay hoặc chọn lịch) hoặc `DateInput`; cột lưới co giãn được; chữ tab in hoa.
- Làm xong một màn hình: chạy lên, chụp bằng Playwright, đối chiếu `docs/ui-style.md`, sửa đến khi đúng quy chuẩn.
  Khi tôi đưa ảnh mẫu mới vào `docs/`, làm theo đúng ảnh đó.

Toàn văn quy chuẩn giao diện (nạp tự động mỗi phiên):
@docs/ui-style.md
