# Hướng dẫn giao diện – SAP Fiori, theme Horizon

Từ 31/10/2026 giao diện theo quy chuẩn thiết kế **SAP Fiori (Horizon Theme)**, thay cho phong cách Windows XP trước đây (xem lịch sử Git
nếu cần đối chiếu). Mọi màn hình mới hoặc sửa đều theo tài liệu này.

## Quyết định áp dụng (31/10/2026)
- **Công nghệ giữ nguyên**: React 19 + Ant Design + TanStack Query. Dựng phong cách Fiori bằng theme token của Ant Design
  (`src/Proc.Web/src/theme.ts`) và CSS dùng biến token – **không** dùng SAPUI5/UI5 Web Components.
- **Phông chữ**: khai báo `"72", "72full", Arial, Helvetica, sans-serif`. Phần mềm **không kèm file phông "72"** (phông có giấy phép riêng
  của SAP); máy nào đã cài "72" thì hiện "72", còn lại dùng Arial/Helvetica.
- **Lộ trình chuyển đổi**: lát UI-a đổi theme (màu, chữ, bo góc, đổ bóng, khoảng cách) và giữ bố cục cũ; lát UI-b đổi bố cục sang Shellbar
  + Dynamic Page (đã làm 31/10/2026: bỏ thanh công cụ, thanh trạng thái và chế độ INS/OVR). Mỗi lát có ảnh chụp Playwright để duyệt.
- Giữ các tính năng thao tác nhanh đã có: ô lệnh (gõ mã màn hình), phím tắt, cây điều hướng có số đếm.

## 1. Chữ (Typography)
| Dùng cho | Cỡ | Độ đậm |
| --- | --- | --- |
| Chữ thường, nội dung bảng (mặc định) | 14px (0.875rem) | 400 |
| Nhãn, tiêu đề đối tượng nhỏ, chú thích | 12px (0.75rem) | 400 |
| Tiêu đề nhóm / khối form (sub-header) | 16px (1rem) | 600 |
| Tiêu đề trang (H1) | 24–28px (dùng 24px) | 700 |

## 2. Bảng màu Horizon
| Vai trò | Mã màu |
| --- | --- |
| Chữ chính | #1d2d3d |
| Chữ phụ, ghi chú | #647987 |
| Nền ứng dụng | #f5f6f7 |
| Nền đối tượng (bảng, khối form, thẻ) | #ffffff |
| Màu nhấn (nút chính, liên kết, mục chọn) | #0a6ed1 (đậm hơn khi rê/nhấn: #005a9e) |
| Lỗi / khẩn cấp | chữ #b00e12, nền nhạt #fff1f1 |
| Cảnh báo | chữ #b25900, nền nhạt #fff8eb |
| Thành công / hợp lệ | chữ #1a7a24, nền nhạt #f0f9f1 |
| Thông tin | chữ #02629b, nền nhạt #f2f9ff |

Màu khai báo thành theme token, **không viết màu cứng trong component** (CLAUDE.md mục 9). Trạng thái luôn có chữ đi kèm, màu không là
thông tin duy nhất.

## 3. Hình khối và khoảng cách
- Khoảng cách theo **bội số của 4**: chủ yếu 8px (0.5rem) và 16px (1rem); 4px cho khoảng rất sát (biểu tượng – chữ).
- Bo góc: 8px cho khối lớn (thẻ, bảng, khối form, hộp thoại); 4px cho ô nhập, nút, thẻ trạng thái.
- Đổ bóng cực nhẹ cho khối nội dung: `0 0 2px rgba(34,53,72,.15), 0 2px 4px rgba(34,53,72,.15)`; bóng đậm hơn một chút cho lớp nổi
  (menu thả xuống, hộp thoại).
- Chiều cao: ô nhập, nút 28px; dòng bảng 32px; tiêu đề cột bảng 36px (mật độ "compact" của Fiori cho dữ liệu lớn, chuột + bàn phím).

## 4. Bố cục (lát UI-b)
1. **Shellbar** trên cùng: logo + tên ứng dụng, ô tìm/ô lệnh (gõ mã màn hình như PO, PR, NXT rồi Enter), công ty đang làm việc,
   thông báo, ảnh đại diện người dùng (menu: đổi mật khẩu, đăng xuất). Menu module (Hằng ngày, Mua hàng, Kho, Giá và NCC, Đấu thầu,
   Báo cáo, Hệ thống) mở từ Shellbar hoặc cây điều hướng.
2. **Điều hướng bên trái** (Side Navigation): nhóm Ghim + nhóm theo module, số đếm bên phải (đỏ khi cần chú ý), thu gọn được.
3. **Dynamic Page** cho vùng nội dung:
   - **Header** (đóng/mở được): tiêu đề trang (H1), dòng phụ (tổng số bản ghi, trạng thái phiếu), các chỉ số/tóm tắt, khu bộ lọc.
   - **Content**: bảng (danh sách) hoặc form (chứng từ) trên khối nền trắng bo góc 8px, đổ bóng nhẹ.
   - **Thanh lệnh**: nút chính (màu nhấn) ở phải tiêu đề; nút phụ dạng "transparent"; trên form chứng từ có thanh chân (footer bar) chứa
     Lưu / Gửi duyệt khi đang sửa.
4. Thông tin phiên bản, ngày làm việc chuyển vào menu người dùng (bỏ thanh trạng thái kiểu Windows ở lát UI-b).
5. Kích thước: Shellbar 48px; điều hướng trái 240px, dòng mục 32px; Shellbar chỉ hiện avatar người dùng (họ tên trong menu), tên ứng dụng ẩn khi
   màn hình hẹp hơn 1440px để menu module và ô lệnh đủ chỗ.

## 5. Danh sách (ListPage + DataGrid)
- Header: tiêu đề trang, "N bản ghi"; nút "Tạo mới" (nút chính duy nhất), Xuất CSV, Xuất Excel, Nhập Excel.
- Khu bộ lọc trong header: Tìm kiếm (có dấu hoặc không dấu đều tìm được), các ô lọc nghiệp vụ, Nhóm theo, Cột, Xóa bộ lọc.
- Bảng: nền trắng; tiêu đề cột chữ phụ đậm 600 trên nền trắng, kẻ dưới; dòng 32px, chữ 14px, kẻ ngang mảnh; dòng chọn nền xanh rất nhạt;
  cột số căn phải; ngày dd/MM/yyyy; số kiểu 1.234.567,89; chữ dài cắt "…" và hiện đủ khi rê chuột; bấm đúp, Enter hoặc mũi tên ">" cuối
  dòng để mở;
  mũi tên lên/xuống để di chuyển; cột trạng thái dạng chấm/nhãn màu ngữ nghĩa + chữ; vùng kéo tiêu đề cột để nhóm; cuộn ảo khi nhiều dòng.

## 6. Chứng từ (DocumentForm)
Theo docs/design.md mục 10.1: header là số phiếu, trạng thái (nhãn màu ngữ nghĩa), người lập, nút In phiếu bên phải; khối "Thông tin phiếu"
dạng form 3 cột với tiêu đề khối 16px/600, thu gọn được; lưới dòng nhập như Excel; khối tổng tiền; tab phụ (Lịch sử duyệt, Nhật ký…).
Phiếu đang sửa: Lưu / Gửi duyệt / Ghi sổ… ở thanh chân dính đáy; phiếu chỉ xem: nút lệnh ở phải tiêu đề. Ngăn kéo chi tiết có nhãn "Chỉ xem"
khi người dùng không được sửa.

## 7. Đăng nhập
Ngoại lệ không nằm trong khung ứng dụng: thẻ trắng giữa nền #f5f6f7, bo góc 8px, đổ bóng nhẹ; tiêu đề ứng dụng, Tên đăng nhập, Mật khẩu,
nút "Đăng nhập" màu nhấn; dòng phiên bản ở chân thẻ; lỗi hiện ngay trên form bằng tiếng Việt.

## 8. Bàn phím (giữ nguyên)
F2 thêm dòng, F3 tìm, Ctrl+S lưu, Ctrl+P in phiếu, Esc đóng hộp thoại, Ctrl+Enter gửi duyệt, Alt + chữ gạch chân mở menu module trên
Shellbar, Enter ở ô lệnh mở màn hình. Mọi thao tác chính làm được không cần chuột.

## 9. Kỹ thuật
- Ant Design `ConfigProvider` với theme token ở `src/Proc.Web/src/theme.ts` (màu, chữ, bo góc, đổ bóng, chiều cao); kích thước khung
  (Shellbar, cây điều hướng, dòng bảng) ở `layoutTokens` cùng file, đưa thành biến CSS `--app-*`.
- Component dùng chung: `AppShell`, `ListPage`, `DataGrid`, `DocumentForm`, `DetailDrawer` – không tự tạo bố cục riêng từng màn hình.
- Làm xong một màn hình: chạy lên, chụp bằng Playwright (`e2e/screenshots`), đối chiếu tài liệu này (và ảnh mẫu Fiori nếu có trong `docs/`).
