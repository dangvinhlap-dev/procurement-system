# Design system – phong cách ERP desktop cổ điển

> **Chính thức từ 01/11/2026** (người dùng duyệt bản thử). Thay cho `docs/ui-guide.md` (SAP Fiori Horizon) – tài liệu đó chỉ còn để đối chiếu
> lịch sử. Toàn bộ màn hình đã chuyển qua khung dùng chung (mục 8); màn hình mới làm theo CLAUDE.md mục 9;
> màn hình mẫu chuẩn: `/thu-giao-dien/tao-po` (`src/Proc.Web/src/classic/preview/CreatePurchaseOrderPreview.tsx`).
> Không trộn phong cách khác vào một màn hình.

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

**Màu icon theo mức quan trọng** (yêu cầu 01/11/2026) – khai báo `tone` trên `Toolbar`/`CommandButton`, icon 16px trên thanh công cụ:

| Mức | Màu | Biến | Dùng cho |
| --- | --- | --- | --- |
| `danger` – không hoàn tác / mất dữ liệu đang nhập | đỏ #d0281e | `--erp-icon-danger` | Hủy, Thoát, Xóa dòng |
| `success` – ghi hoặc tạo dữ liệu | xanh lá #1e8f2e | `--erp-icon-success` | Lưu, Thêm dòng |
| `warning` – thao tác thường | vàng #c48a00 | `--erp-icon-warning` | Quay lại, In, Tìm, Tổng quan, Giữ, Xem trước in, Thông báo, Chọn/Bỏ chọn |

Icon luôn có chú thích (title) – màu không là thông tin duy nhất.

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
- Thanh cuộn ngang khi nhiều cột; tiêu đề cột dính trên. `LineGrid` mặc định co cột cho vừa khung; lưới rất nhiều cột (ví dụ 12 tháng của
  kế hoạch nhu cầu) bật `scrollX` để giữ đúng độ rộng cột và cuộn ngang.
- **Co giãn cột**: kéo mép phải tiêu đề cột (tối thiểu 32px); bấm đúp mép cột để trả về độ rộng mặc định.
- **Ô ngày** (`DateInput`, mọi ô ngày – header và lưới): gõ tay dd/MM/yyyy (gõ nhanh `05122026`, `5/12/2026`, `05.12.2026`)
  hoặc chọn trên lịch; không dùng ô ngày của trình duyệt (hiển thị theo ngôn ngữ hệ điều hành).

## 5b. Ô chọn danh mục – tìm kiểu SAP ECC (02/11/2026)

Mọi ô chọn có tìm kiếm (NCC, mặt hàng, kho, bộ phận, nhóm…) dùng `Select` của `src/classic/Select.tsx` (thay `Select` của antd, cùng thuộc tính):

- Gõ **`*` rồi Enter**: liệt kê **toàn bộ** danh mục để chọn (ô tìm trên máy chủ trả tối đa 500 dòng). Mẫu có `*` thì Enter chỉ liệt kê,
  không tự chọn dòng đầu; chọn bằng mũi tên + Enter hoặc bấm chuột.
- `*` giữa từ khóa là ký tự đại diện: `b*long` khớp "Bu lông"; áp cả ô Tìm kiếm của mọi danh sách (`matchesKeyword`) và API tìm (`LikePatterns.Contains`).
- Tìm không phân biệt có dấu/không dấu, nhiều từ không cần đúng thứ tự.
- **Đồng bộ mọi form** (yêu cầu người dùng 07/11/2026): `Select` cổ điển **mặc định bật tìm** – kể cả ô lọc trạng thái/nhóm/kho/công ty trên
  danh sách (chỉ `showSearch={false}` mới tắt); không import `Select`/`TreeSelect` trực tiếp từ antd. Ô cây (nhóm hàng) dùng `TreeSelect` của
  `src/classic/TreeSelect.tsx` – cùng cách tìm `*`, không dấu. **Ô lệnh** (tên màn hình) gõ `*` rồi Enter liệt kê mọi màn hình được phép, mũi
  tên + Enter mới mở. Ô tìm trên máy chủ gọi API khi gõ ≥ 2 ký tự **hoặc** có `*`.

## 5c. Trang điện thoại – duyệt gọn (lát O2d, 11/11/2026)

Trang `/duyet` (danh sách phiếu chờ tôi duyệt) và `/duyet/:loại/:id` (xem + Duyệt / Từ chối) dành cho màn hình hẹp, đứng ngoài `AppShell`:

- Bọc bằng `ClassicScreen className="erp-mobile"` + theme `classicMobileTheme` (`classicTheme.ts`); CSS ở `src/classic/classic-mobile.css`, chỉ
  dùng biến `--erp-*`. Giữ màu, viền 1px, bo 2px, không bóng của phong cách cổ điển.
- **Ngoại lệ có chủ đích** so với mục 3–4: chữ **14px**, ô/nút cao **36px** (nút Duyệt / Từ chối 40px) để bấm bằng ngón tay; một cột, rộng tối đa 720px.
- Thanh tiêu đề dải màu dính trên (nút quay lại, tiêu đề, "Bản đầy đủ"); dòng người dùng + ô chọn công ty + đăng xuất.
- Danh sách là **thẻ** (số phiếu đậm, loại phiếu, nội dung, ngày · cấp · người lập, số tiền phông đơn cách canh phải); khối thông tin có dải
  tiêu đề như "Thông tin phiếu"; cảnh báo chữ đỏ đậm có nội dung; đèn trạng thái kèm chữ.
- Vùng Ý kiến + nút **dính đáy màn hình**; Từ chối (icon đỏ) bên trái, Duyệt (icon xanh) bên phải; cả hai hỏi xác nhận.
- Link "Mở trên phần mềm" trong email mở trên màn hình ≤ 768px tự chuyển sang trang này.

## 5d. Ổ khóa phê duyệt và "Kiểm tra phê duyệt" (lát Z2, 11/11/2026 – design.md mục 20.1 R3, R4)

- Component `src/classic/ApprovalTrail.tsx`: `ApprovalLocks` (dải ổ khóa) và `ApprovalCheckButton` (nút + hộp thoại). Không dùng trực tiếp
  trong trang: truyền `approval={{ steps, createdByName, createdAt, stageLabels?, byCompany? }}` cho `DocumentForm` hoặc `DetailDrawer`.
- Ổ khóa đặt ngay sau thẻ trạng thái trên tiêu đề phiếu, icon 14px màu `--erp-light-yellow`, cách nhau 2px: **đóng** (`LockFilled`) = bước đã
  duyệt, **mở** (`UnlockOutlined`) = chưa duyệt. Luôn có chú thích (title/aria-label) ghi cấp, kết quả, người duyệt, giờ ngày.
- Nút "Kiểm tra phê duyệt" nằm ở vùng nút bên phải tiêu đề (cạnh "Lịch sử thay đổi", "In phiếu"), bấm được cả khi phiếu chỉ xem; hộp thoại
  là bảng kẻ ô: (Lượt), Bước, Nội dung, Kết quả, Tên đăng nhập, Họ tên, Chức vụ, Giờ ngày (dd/MM/yyyy HH:mm), Ý kiến.

## 5e. Ô số chứng từ (lát Z3, 11/11/2026 – design.md mục 20.1 R5)

- Mọi số chứng từ (đầu phiếu, số phiếu liên quan trên form, cột số phiếu ở **danh sách và báo cáo**) hiện bằng `DocNo`
  (`src/classic/DocNo.tsx`): ô chữ chỉ đọc cao 18px, viền 1px `--erp-input-border`, nền `--erp-readonly-bg`, phông đơn cách, bo 2px.
- **Bấm** = sao chép số (thông báo "Đã sao chép số …"; máy chủ http không cho clipboard thì dùng cách dự phòng); **bấm đúp** hoặc **Enter**
  = mở form chứng từ gốc (`documentPath`). Ô mở được có chữ xanh đậm gạch chấm (`is-link`); không có Id phiếu gốc thì chỉ sao chép.
- Lưới `DataGrid`: khai báo `docRef: (r) => ({ docType, id })` trên cột – bấm đúp ô mở phiếu gốc của ô, không mở dòng.
- Form: số phiếu đầu `DocumentForm` tự là ô chữ; số phiếu liên quan dùng `<DocNo no docType id />` thay cho link chữ.

## 5f. Portal nhà cung cấp `/ncc` (lát P1, 12/11/2026)

- Trang cho NCC, **đứng ngoài AppShell** (NCC không thấy menu, cây điều hướng nội bộ): `ClassicScreen className="erp-portal"`, CSS ở
  `src/portal/portal.css`, chỉ dùng biến `--erp-*`; chữ 12px, viền 1px, bo ≤ 2px, không bóng như mọi màn hình.
- Thanh trên dải màu: "Portal nhà cung cấp", đường dẫn trang (trang đang mở nền `--erp-tab-active` chữ trắng), họ tên – tên NCC, nút
  Đăng xuất (icon đỏ). Nội dung rộng tối đa 1000px, giữa màn hình; tiêu đề trang 18px đậm `--erp-title`; khối thông tin có dải tiêu đề.
- Đăng nhập, OTP, kích hoạt, quên / đặt lại mật khẩu: **hộp giữa màn hình** có thanh tiêu đề dải màu, chân ghi phiên bản.
- Bảng người dùng (cả tab "Tài khoản Portal" của ngăn kéo NCC) dùng chung `PortalUserPanel`: `DataGrid` + nút dưới lưới Mời (icon xanh),
  Gửi lại lời mời (vàng), Khóa (đỏ), Mở khóa (xanh) – thao tác trên dòng đang chọn (`DataGrid` prop `onSelectRow`). Tab ngăn kéo không có ô form (như "Tài khoản Portal") khai báo `lazy`
  (`DetailTab`) – chỉ vẽ khi mở, để tiêu đề cột không trùng nhãn ô form ở tab khác.
- Khối có dải tiêu đề (`Section`) và khối file NCC (`PortalFiles` – lưới file + Tải lên / Tải xuống / Xóa) dùng chung ở `src/portal/PortalParts.tsx`
  cho trang Thư mời báo giá và Đơn đặt hàng (lát P4a). Trang đơn hàng: tổng tiền canh phải dưới lưới (`.erp-portal-totals`); khối phản hồi có
  nút **Từ chối đơn** (icon đỏ) bên trái, **Xác nhận đơn** (icon xanh) bên phải, cả hai hỏi lại.
- **Báo giao hàng** (lát P5, `src/portal/PortalDeliveryParts.tsx`): nút **Báo giao hàng…** (icon xe, xanh) sát phải dưới khối phản hồi, bên trái là
  dòng gợi ý / lý do chưa báo giao được. Form "Lập phiếu báo giao hàng" là khối có dải tiêu đề: đầu phiếu bốn ô trên **một hàng**
  (`.erp-portal-delivery-head`, màn hình hẹp một cột), lưới `LineGrid` cố định dòng (SL giao nhập trong lưới), file chờ là ô viền 1px nền
  `--erp-readonly-bg` có nút bỏ; **Bỏ qua** (đỏ) trái – **Gửi phiếu báo giao** (xanh) phải, gửi hỏi lại. Danh sách phiếu: lưới phiếu + lưới dòng
  của phiếu đang chọn, **Hủy phiếu báo giao** (đỏ, hộp thoại lý do bắt buộc), khối file của phiếu (`PortalFiles kind="delivery-notices"`).

## 5g. Luồng hỏi đáp `QaThread` (lát P3b-2, 12/11/2026)

- Component `src/classic/QaThread.tsx`, CSS `.erp-qa*` trong `classic-app.css`; dùng chung cho Portal ("Hỏi đáp với bên mời") và tab
  "Hỏi đáp NCC" của RFQ. Mỗi mục là khung viền 1px `--erp-grid-line`, bo 0, không bóng.
- Dải đầu mục nền `--erp-grid-head` (thông báo làm rõ nền `--erp-tab-bg`): loại ("Câu hỏi n" / "Thông báo làm rõ") đậm `--erp-title`, người,
  giờ phông đơn cách `--erp-text-muted`; câu chưa trả lời có ô vuông `--erp-light-yellow` kèm chữ "Chưa trả lời"; nút thao tác (ví dụ
  "Trả lời…") sát phải.
- Câu trả lời thụt 24px, vạch trái 3px `--erp-subtotal`, nền `--erp-panel-bg`, dòng đầu "Trả lời – người – giờ" chữ `--erp-subtotal` đậm.
- Khoảng cách viết thẳng 2/4/8px (biến `--erp-gap*` chỉ có trong `ClassicScreen`, không có trong `AppShell`).

## 5a. Tab

- Chữ trên tab **in hoa** mặc định (CSS `text-transform: uppercase`, tên trong code viết thường).

## 6. Bàn phím

| Phím | Tác dụng |
| --- | --- |
| F1 | Trợ giúp ô: lịch sử thay đổi (ai tạo/sửa/xóa, giá trị cũ → mới) và thông tin ô – ô đang có con trỏ hoặc ô dưới chuột (design.md mục 9.3a) |
| Tab / Shift+Tab | Chuyển ô (thứ tự trái → phải, trên → dưới) |
| Enter trong lưới | Xuống ô cùng cột ở dòng dưới; dòng cuối thì thêm dòng mới |
| Ctrl+S | Lưu |
| F2 | Thêm dòng |
| Ctrl+P | Xem trước in |
| F3 | Quay lại |
| Esc | Đóng hộp thoại |

## 7. Đối chiếu với quy định hiện hành

- Các quy tắc ở CLAUDE.md mục 5 (tiếng Việt, ngày, số, tìm có dấu/không dấu) giữ nguyên.
- Thay thế `docs/ui-guide.md` (Fiori: chữ 14px, bo 8px, đổ bóng, Shellbar, Dynamic Page) từ 01/11/2026.
- Đăng nhập chưa đổi (vẫn thẻ trắng giữa nền) – bổ sung khi có yêu cầu.

## 8. Áp dụng cho toàn ứng dụng (01/11/2026)

Mọi màn hình đổi sang phong cách này qua khung dùng chung, không viết lại từng trang:

| Thành phần | Cách đổi |
| --- | --- |
| Token antd (chữ, màu, bo góc, chiều cao, nút, tab, bảng, hộp thoại) | `src/theme.ts` |
| Khung `AppShell`: hàng 1 menu module chữ, hàng 2 ô lệnh + công ty + thông báo + người dùng (54px); cây điều hướng 22px/dòng | `classic-app.css` |
| `ListPage` + `DataGrid`: tiêu đề 18px xanh đậm, lưới kẻ ô, dòng 22px, cột số phông đơn cách, dòng chọn vàng nhạt, đèn trạng thái ô vuông, co giãn cột; nút lệnh riêng của màn hình truyền qua `actions` (đặt sau "Tạo mới", ví dụ "Lập đề nghị mua") | `classic-app.css` |
| `DocumentForm` + `LineGrid`: khối "Thông tin phiếu" có dải tiêu đề, lưới dòng kẻ ô co giãn cột, thanh chân dải màu | `classic-app.css`, `LineGrid.tsx` |
| `DetailDrawer`, hộp thoại: thanh tiêu đề dải màu, viền 1px | `classic-app.css` |
| Tab mọi nơi: ô vuông xanh, tab chọn đậm, chữ in hoa | `classic-app.css` |
| Nút: nền be viền nâu (cả nút chính và nút chữ trên thanh lệnh); icon tô màu theo mức quan trọng | `classic-app.css` |
| Ô ngày: `DATE_INPUT_FORMATS` (`utils/format.ts`) – gõ dd/MM/yyyy, ddMMyyyy, d/M/yyyy hoặc chọn lịch | các trang |

Không còn hiệu ứng chuyển động (`motion: false`).
