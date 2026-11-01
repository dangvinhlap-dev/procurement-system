# Design system – phong cách ERP desktop cổ điển (BẢN THỬ)

> **Trạng thái: bản thử, chờ người dùng duyệt (01/11/2026).** Chỉ có trên nhánh `feature/thu-giao-dien-erp`, áp dụng cho một màn hình
> thử `/thu-giao-dien/tao-po`. Khi chưa duyệt, giao diện chính thức vẫn là `docs/ui-guide.md` (SAP Fiori Horizon).
> Nếu được duyệt: thay `docs/ui-guide.md`, chuyển dần từng màn hình sang các component trong `src/Proc.Web/src/classic/`.

Ảnh tham khảo: `docs/imageSAP.png` (màn hình tạo đơn mua hàng của một ERP desktop). **Chỉ học phong cách** – không dùng logo, tên,
biểu tượng hay nhận diện thương hiệu của hãng nào; mọi chữ trên giao diện là tiếng Việt của phần mềm này.

## 1. Bố cục màn hình (từ trên xuống)

| # | Vùng | Component | Nội dung |
| --- | --- | --- | --- |
| 1 | Thanh menu chữ | `MenuBar` | Phiếu, Sửa, Đi tới, Hệ thống, Trợ giúp; chữ gạch chân là phím Alt |
| 2 | Thanh công cụ icon | `Toolbar` | Ô lệnh (gõ mã màn hình) + nút icon 16px: Lưu, Quay lại, Thoát, Hủy, In, Tìm; nhóm cách nhau bằng vạch dọc |
| 3 | Tiêu đề màn hình | `ScreenTitle` | Chữ đậm 18px, xanh đậm, ví dụ "Tạo đơn mua hàng" |
| 4 | Thanh lệnh phụ | `CommandBar` + `CommandButton` | Tổng quan chứng từ, Giữ, Xem trước in, Thông báo… |
| 5 | Header chứng từ | `HeaderFields` | Loại PO, Nhà cung cấp, Ngày chứng từ trên **một dòng** |
| 6 | Tab thông tin header | `TabStrip` | Giao hàng/Hóa đơn, Điều kiện giá, Ghi chú, Địa chỉ, Đối tác, Thông tin thêm, Tổ chức, Trạng thái |
| 7 | Lưới dòng hàng | `EntryGrid` | STT, Mã hàng, Tên hàng, SL, ĐVT, Ngày giao, Đơn giá, Tiền tệ, Nhóm hàng, Kho |
| 8 | Nút dưới lưới | `GridButtons` | Thêm dòng, Xóa dòng, Chọn tất cả, Bỏ chọn |
| 9 | Thanh trạng thái | `StatusBar` | Thông báo sau thao tác (đã lưu, lỗi…) với đèn màu + chữ |

Khung chung bọc màn hình: `ClassicScreen` (gắn lớp `.erp-classic`, nạp biến CSS, bắt phím tắt).

Mỗi khối (vùng 5–7) có **nút thu gọn** nhỏ ở mép trái như ảnh mẫu.

## 2. Màu sắc

Khai báo một lần ở `src/Proc.Web/src/classic/classic.css` (biến `--erp-*`) và `classicTheme.ts` (token antd cho hộp thoại, ô chọn
ngày khi dùng antd bên trong). Component không viết màu cứng.

| Vai trò | Biến | Mã màu |
| --- | --- | --- |
| Nền màn hình | `--erp-bg` | #e3eaf2 |
| Nền thanh menu / công cụ | `--erp-bar-bg` | #eef3f9 → #d6e1ee (dải dọc nhẹ) |
| Chữ chính | `--erp-text` | #1b2a3a |
| Tiêu đề màn hình | `--erp-title` | #1f3d6b |
| Tab thường | `--erp-tab-bg` | #b9cde8 |
| Tab đang chọn | `--erp-tab-active` | #7fa2d1 (chữ trắng) |
| Nút lệnh: nền / viền | `--erp-btn-bg` / `--erp-btn-border` | #f3efdf / #b5a97f |
| Header lưới | `--erp-grid-head` | #d3ddea |
| Đường kẻ lưới | `--erp-grid-line` | #b8c5d6 |
| Dòng chọn trong lưới | `--erp-grid-selected` | #fff6c8 |
| Ô nhập: nền / viền | `--erp-input-bg` / `--erp-input-border` | #ffffff / #7f9db9 |
| Ô đang nhập (focus) | `--erp-input-focus` | viền #1f5fbf |
| Ô chỉ đọc | `--erp-readonly-bg` | #eef2f6 |
| Dòng tổng / tạm tính | `--erp-subtotal` | #2a5db0 |
| Đèn trạng thái | `--erp-light-green/yellow/red` | #2e9e3a / #e8b400 / #d0281e |
| Ô bắt buộc (dấu *) | `--erp-required` | #b00e12 |

Trạng thái luôn có chữ đi kèm (tooltip/nhãn), màu không là thông tin duy nhất.

## 3. Chữ

- Chữ chung: `Tahoma, "Segoe UI", Arial, sans-serif`, **12px**, độ đậm 400. Nhãn ô: 12px.
- Tiêu đề màn hình: 18px/700, màu `--erp-title`.
- Số tiền, số lượng: **canh phải**, phông đơn cách `Consolas, "Courier New", monospace`, định dạng vi-VN (1.912.500; số lượng 1.234,5).
- Tiền tệ là **cột riêng** đặt ngay sau cột số tiền (VND), không ghép vào chuỗi số.
- Ngày dd/MM/yyyy (CLAUDE.md mục 5).

## 4. Hình khối và khoảng cách

- Dày đặc: dòng lưới 20px, ô nhập cao 20px, nút lệnh cao 22px, thanh công cụ 28px.
- Khoảng cách 2px / 4px / 8px. Bo góc 0 hoặc tối đa **2px**.
- Viền 1px rõ ràng; **không đổ bóng**, không hiệu ứng chuyển động, không bo tròn lớn.
- Nút icon: 22×22px, viền chỉ hiện khi rê chuột; nút bị khóa mờ 50%.

## 5. Lưới nhập (EntryGrid)

- Cột đầu: ô chọn dòng (bấm để chọn, Shift+bấm chọn nhiều) + cột đèn/cảnh báo.
- Dòng lỗi: **tam giác vàng** ở đầu dòng, rê chuột thấy lý do (ví dụ "Chưa nhập số lượng").
- Ô nhập ngay trong lưới (như Excel); ô chỉ đọc nền `--erp-readonly-bg`.
- Có sẵn vài dòng trống cuối lưới để gõ tiếp.
- Dòng tổng chữ `--erp-subtotal`, đậm.
- Thanh cuộn ngang khi nhiều cột; tiêu đề cột dính trên.

## 6. Bàn phím

| Phím | Tác dụng |
| --- | --- |
| Tab / Shift+Tab | Chuyển ô (thứ tự trái → phải, trên → dưới) |
| Enter trong lưới | Xuống ô cùng cột ở dòng dưới; dòng cuối thì thêm dòng mới |
| Ctrl+S | Lưu |
| F2 | Thêm dòng |
| Ctrl+P | Xem trước in |
| F3 | Quay lại |
| Esc | Đóng hộp thoại |

## 7. Đối chiếu với quy định hiện hành

- Các quy tắc ở CLAUDE.md mục 5 (tiếng Việt, ngày, số, tìm có dấu/không dấu) giữ nguyên.
- Mâu thuẫn với `docs/ui-guide.md` (Fiori: chữ 14px, bo 8px, đổ bóng, Shellbar, Dynamic Page) – đây là lựa chọn thay thế, chỉ chốt khi
  người dùng duyệt bản thử.
