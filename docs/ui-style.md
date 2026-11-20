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
| 1 | Thanh menu chữ | `MenuBar` | Phiếu, Sửa, Đi tới, Hệ thống, Trợ giúp; chữ gạch chân là phím Alt (AppShell: Hằng ngày … Hệ thống, Quản trị hệ thống – design.md 17.1) |
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
- **Nhiều số cùng loại** (19/11/2026 – PR ô "Đơn mua hàng" + cột "Số PO", PO ô "Đề nghị mua"): `<DocNoList items docType empty? />` cùng file –
  các ô `DocNo` trên một hàng cách nhau 4px, tự xuống dòng (`.erp-doc-no-list`); không có số thì chữ mờ `empty` (`.erp-doc-no-empty`).
- Hộp chọn dòng đề nghị của BTH: dòng không chọn chung được (khác nhóm biểu mẫu – bộ phận – công trình) khóa ô chọn, chữ mờ
  (`.erp-pick-row-blocked`), rê chuột xem lý do; cột Biểu mẫu, Bộ phận đề xuất, Công trình có nút lọc trên tiêu đề cột.

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
- **Thanh toán** (lát P6b, `src/portal/PortalPaymentPages.tsx`): trang `/ncc/thanh-toan` là lưới đơn (đèn thanh toán kèm chữ, cột Còn phải trả ngay
  sau đèn) + tổng còn phải trả canh phải; bấm đúp mở `/ncc/thanh-toan/:id`. Khối `PortalPaymentPanel` (dùng cả trên trang đơn): tổng hợp là lưới
  nhãn – số **hai cặp trên một hàng** (`.erp-portal-pay-summary`, số canh phải phông đơn cách, "Còn phải trả" đậm; màn hình hẹp một cặp), rồi lưới
  "Hóa đơn – phiếu nhận hàng" và "Đợt thanh toán"; cột quan trọng (trạng thái, tiền) đặt trước để nằm trong khung 1000px, cột phụ cuộn ngang.

- **Hồ sơ công ty** (lát P2a, `src/portal/PortalProfilePages.tsx`): khối "Hồ sơ hiện tại" là lưới nhãn – giá trị **hai cặp trên một hàng**
  (`.erp-portal-profile-fields`, màn hình hẹp một cặp) + lưới người liên hệ, tài khoản; dòng gợi ý trái – nút **Đề nghị cập nhật hồ sơ…** (vàng)
  phải. Form đề nghị: ô hai cột (`.erp-portal-profile-head`, tên / địa chỉ / phạm vi chiếm cả hàng), mã số thuế ô chỉ đọc, hai `LineGrid` nhập
  được (người liên hệ, tài khoản), file chờ như phiếu báo giao; **Bỏ qua** (đỏ) trái – **Gửi đề nghị** (xanh) phải, gửi hỏi lại. Đề nghị đang
  chọn: khối so sánh `SupplierProfileCompare` (`src/components/SupplierProfile/` – dùng chung với ngăn kéo nội bộ HSC, nội bộ thêm cột "Hiện
  tại"), nút **Rút đề nghị…** (icon đỏ), khối file `PortalFiles kind="profile-requests"`.
- **Bảng giá** (lát P7a, `src/portal/PortalPriceListPages.tsx`): trang `/ncc/bang-gia` là lưới bảng giá (đèn tình trạng kèm chữ đặt sau tên),
  bấm đúp mở `/ncc/bang-gia/:id`: khối "Thông tin bảng giá" là lưới nhãn – giá trị hai cặp trên một hàng (dùng lại `.erp-portal-pay-summary`),
  khối "Dòng giá" là `DataGrid` (đơn giá, giá sau CK canh phải phông đơn cách, cột Tiền tệ riêng ngay sau đơn giá) + dòng gợi ý dưới lưới.

## 5g. Luồng hỏi đáp `QaThread` (lát P3b-2, 12/11/2026)

- Component `src/classic/QaThread.tsx`, CSS `.erp-qa*` trong `classic-app.css`; dùng chung cho Portal ("Hỏi đáp với bên mời") và tab
  "Hỏi đáp NCC" của RFQ. Mỗi mục là khung viền 1px `--erp-grid-line`, bo 0, không bóng.
- Dải đầu mục nền `--erp-grid-head` (thông báo làm rõ nền `--erp-tab-bg`): loại ("Câu hỏi n" / "Thông báo làm rõ") đậm `--erp-title`, người,
  giờ phông đơn cách `--erp-text-muted`; câu chưa trả lời có ô vuông `--erp-light-yellow` kèm chữ "Chưa trả lời"; nút thao tác (ví dụ
  "Trả lời…") sát phải.
- Câu trả lời thụt 24px, vạch trái 3px `--erp-subtotal`, nền `--erp-panel-bg`, dòng đầu "Trả lời – người – giờ" chữ `--erp-subtotal` đậm.
- Khoảng cách viết thẳng 2/4/8px (biến `--erp-gap*` chỉ có trong `ClassicScreen`, không có trong `AppShell`).

## 5h. Ô số cấp và cây nhóm hàng 4 cấp (lát N4b, 14/11/2026 – design.md mục 5.7, N4-1…N4-9)

- **Ô số cấp** `LevelBadge` (`src/classic/LevelBadge.tsx`, hằng số cấp ở `classic/categoryLevels.ts`): ô bo 2px cao 14px, chữ số đậm phông đơn cách
  11px; nền theo cấp `--erp-level-1…4` (xanh đậm → nhạt: #1f3d6b, #2a5db0, #4f86c6, #8fb0d8), chữ trắng, riêng cấp 4 chữ `--erp-level-4-text`.
  Luôn có chú thích "Cấp n – tên cấp" (Ngành hàng / Nhóm hàng / Loại hàng / Chủng loại). Biến thể chữ rộng "Cấp n/4" đặt sau tiêu đề ngăn kéo
  (`DetailDrawer` prop `titleExtra`).
- **Ô chọn nhóm hàng** (`TreeSelect` cổ điển, cây dựng bằng `buildCategoryTree`): mỗi nút có ô số cấp trước "mã – tên"; nút bị khóa ghi lý do mờ
  sau tên (`· cấp cuối`, `· nhánh sẽ vượt 4 cấp`). Ô chọn **nhóm cha** khóa nhóm cấp 4 và nhóm làm nhánh vượt 4 cấp (`parentForDepth`). Ô **Nhóm hàng của mặt hàng** chỉ chọn được nhóm cấp 4 (`itemLevelOnly`): nhóm cấp 1–3 vẫn hiện để mở nhánh, khóa kèm `· chọn nhóm cấp 4`; dưới ô ghi "Chỉ chọn nhóm cấp 4 – Chủng loại." (N4-5, lát N4c).
- **Màn hình CAT** (`ListPage` + `DataGrid`): cột đầu "Cấp" (ô số + tên cấp), cột tên thụt 16px mỗi cấp, nút tam giác ▾/▸ mở/thu nhánh ngay
  trong ô (`GridColumn.render`; dòng không có nhóm con chừa ô trống cùng rộng để tên thẳng hàng); cột "Số nhóm con". Ô lọc **Cấp** (lọc thì lưới
  thành danh sách phẳng, không mở/thu nhánh). Nút lệnh sau "Tạo mới": **Thêm nhóm con** (icon xanh, thao tác trên dòng đang chọn – `ListPage`
  prop `onSelectRow`), **Mở hết nhánh**, **Thu gọn nhánh** (vàng). Dòng gợi ý trên lưới: đèn xanh + cấp của nhóm con sẽ tạo, hoặc đèn vàng + lý do
  không thêm được (nhóm cấp 4, ngoài phạm vi, ngừng dùng); nút tự khóa, chú thích nêu lý do.
- **Ngăn kéo nhóm hàng**: dải **Đường dẫn** (nền `--erp-readonly-bg`, viền 1px) – nhóm tổ tiên chữ `--erp-subtotal` gạch chấm, bấm mở nhóm đó;
  nhóm đang mở đậm. Ô **Cấp** chỉ đọc cạnh Mã nhóm, tự đổi theo nhóm cha. Tab **NHÓM CON (n)** (`lazy`): dòng gợi ý + nút Thêm nhóm con, lưới
  nhóm con (bấm đúp mở); nhóm cấp 4 thay nút bằng đèn vàng + lý do.
- **Báo cáo gộp theo cấp** (lát N4d, N4-10): ô lọc **Cấp nhóm** ("Nhóm của mặt hàng", "1 – Ngành hàng" … "4 – Chủng loại") chỉ hiện khi
  "Tổng hợp theo" là Nhóm hàng; lưới thêm cột đầu **Cấp** (ô số cấp + tên cấp như màn hình CAT) và cột **Đường dẫn** sau tên nhóm; dòng phụ
  đề file Excel ghi cấp đã chọn.

## 5i. File Excel xuất ra (yêu cầu 14/11/2026 – design.md mục 11.2)

Ảnh mẫu: `docs/diagrams/formatexportExcel.png` (lưới Tra cứu chứng từ). **Mọi file .xlsx** của phần mềm có cùng hình thức lưới trên màn hình,
dựng bằng `ExcelGridStyle` (`src/Proc.Infrastructure/Excel/ExcelGridStyle.cs`) – không tự tô màu riêng trong từng chỗ xuất:

| Phần | Định dạng |
| --- | --- |
| Chữ chung | Tahoma 9pt (= 12px), màu `--erp-text` #1B2A3A |
| Tiêu đề báo cáo (dòng 1) | 13,5pt (= 18px) đậm, `--erp-title` #1F3D6B; dòng 2 (lọc, giờ xuất) nghiêng `--erp-text-muted` #5A6B7D |
| Dòng tiêu đề cột | nền `--erp-grid-head` #D3DDEA, chữ **thường** (không đậm), canh trái; cố định khi cuộn, có lọc |
| Kẻ ô | 1px `--erp-grid-line` #B8C5D6 đủ dọc ngang cả bảng; nền trắng; dòng cao 22px (16,5pt); tắt lưới mờ của Excel (trừ file mẫu nhập danh mục – giữ lưới để gõ thêm dòng) |
| Số lượng, đơn giá, tiền | ô số, Consolas, canh phải (1.234.567) |
| Ngày | ô ngày dd/MM/yyyy, canh trái như lưới |
| Số chứng từ | ô chữ, Consolas, chữ `--erp-title` (lưới: cột có `docRef` gửi kiểu `docno`) |
| Trạng thái | **chỉ tô màu chữ** theo đèn, không thêm ký tự (người dùng chốt 14/11/2026) – xanh lá #1E8F2E; vàng #A86F00; đỏ #D0281E; trung tính chữ thường |

Không có dòng chọn vàng, vùng "Kéo tiêu đề cột vào đây để nhóm" – đó là thao tác trên màn hình, không đưa vào file.

## 5j. Dải nhận diện môi trường (lát QT1, 14/11/2026 – design.md mục 21.3)

- Component `src/classic/EnvironmentBanner.tsx`, CSS `.erp-env-banner` trong `classic-app.css`; màu biến `--erp-env-dev` (xám #6b7785),
  `--erp-env-test` (cam #d9730d), `--erp-env-warning` (= `--erp-light-red`). Dải cao 20px, chữ trắng 12px đậm, canh giữa, một dòng (dài thì
  "…", rê chuột xem đủ), không bóng, bo 0.
- Đặt **trên cùng** AppShell (trên thanh menu chữ), trang điện thoại `/duyet`, Portal NCC; trang đăng nhập dùng dải nổi (`floating`) trên đầu
  trang. Nội dung: DEV "DEV – máy lập trình, dữ liệu mẫu"; TEST "MÔI TRƯỜNG TEST – dữ liệu thử nghiệm, email chuyển về hộp thư test" + tiêu
  đề tab trình duyệt bắt đầu "[TEST] "; PROD không có dải.
- **Thiếu cấu hình** (chưa khai `App:Environment`, TEST chưa khai hộp thư test – người dùng chốt 14/11/2026): dải **đỏ** ghi cảnh báo "email
  không được gửi", vai trò `alert`.
- Màn hình **SYS** (Tổng quan hệ thống) và **Nhật ký vận hành** theo khuôn `ListPage` + `DataGrid` (cột Tình trạng / Kết quả là đèn kèm chữ),
  nút "Làm mới" icon vàng.

## 5k. Khối báo lỗi giao diện và màn hình ERR (lát QT2, 15/11/2026 – design.md mục 21.10)

- Component `src/classic/ErrorBoundary.tsx` (`ErrorBoundary`, `ErrorFallback`, `RouteErrorFallback`), CSS `.erp-error-fallback*` trong
  `classic-app.css`. Màn hình lỗi khi vẽ thì **chỉ vùng nội dung** thay bằng khối báo lỗi: khung viền 1px `--erp-grid-line`, vạch trái 3px
  `--erp-light-red`, nền `--erp-panel-bg`, rộng tối đa 640px; dòng tiêu đề 14px đậm `--erp-title` có tam giác đỏ "Màn hình gặp lỗi giao diện";
  **mã theo dõi** trong ô chữ phông đơn cách nền `--erp-readonly-bg` (bấm chọn cả mã để sao chép); nút **Thử lại**, **Tải lại trang** (icon vàng).
  Lỗi ngoài vùng nội dung của AppShell (đăng nhập, Portal, `/duyet`) dùng cùng khối trên nền `--erp-bar-bg-top`.
- Câu báo lỗi 500 của máy chủ hiện như mọi lỗi (thông báo / thanh trạng thái), có sẵn "mã theo dõi ERR-…".
- Màn hình **ERR** theo khuôn `ListPage` + `DataGrid` (cột Trạng thái là đèn kèm chữ: Mới đỏ, Đang xử lý vàng, Đã xử lý xanh; dòng Mới tô dấu),
  ô lọc Mã theo dõi; ngăn kéo `DetailDrawer` ba tab, chi tiết kỹ thuật trong khung `.erp-error-detail` (phông đơn cách 11px, cuộn). Nút "Thử lỗi
  máy chủ", "Thử lỗi giao diện" icon vàng.

## 5l. Màn hình BAK – Sao lưu (lát QT3a, 15/11/2026 – design.md mục 21.11)

- Khuôn `ListPage` + `DataGrid` như SYS, dùng chung cột `overviewColumns` (`pages/system/overviewColumns.ts`): Nhóm, Hạng mục, Giá trị, Tình trạng
  (đèn ô vuông kèm chữ Ổn / Cần xem / Lỗi / Thông tin), Ghi chú (lý do đèn vàng / đỏ và cách xử lý). Tên tham số PostgreSQL giữ nguyên tiếng Anh
  (`wal_level`, `archive_mode`…) vì là tên kỹ thuật người quản trị gõ lại.
- Thông báo đầu trang (`notice`): "Lưu WAL: <trạng thái> · số liệu lúc dd/MM/yyyy HH:mm" + câu tóm tắt; màu theo đèn (đỏ lỗi / trễ, vàng chờ
  khởi động lại, xanh đang chạy, xanh dương thông tin – máy DEV chưa bật).
- Nút "Làm mới" icon vàng (lấy lại số liệu tab đang xem).
- **Lát QT3b**: thanh tab của `ListPage` (prop `viewTabs` – thay bộ tab mặc định Danh sách / Tổng hợp / Phân tích): **Lịch sử** (mặc định), **Lưu
  WAL**, **Lịch và thời hạn giữ**, **Nơi lưu**; mỗi tab vẫn là `DataGrid`. Lịch sử: ô lọc Từ ngày – Đến ngày, cột Tình trạng là đèn kèm chữ (Đang
  chờ / Đang chạy trung tính, Xong xanh, Xong – chưa chép nơi thứ hai vàng, Lỗi / kiểm tra không đạt đỏ, Đã dọn trung tính), dòng lỗi tô dấu,
  Dung lượng / Số file canh phải phông đơn cách; còn lần đang chờ / đang chạy thì lưới tự làm mới 5 giây. Hai tab sau dùng `overviewColumns`.
- Nút **Sao lưu ngay** (icon `CloudUploadOutlined` xanh, mũi tên mở menu) đặt trước "Làm mới", chỉ hiện khi có quyền chạy: menu chọn Full / Diff /
  pg_dump / Đính kèm (mỗi mục một câu giải thích), đặt xong báo "Đã đặt … vào hàng chờ" và chuyển sang tab Lịch sử. Không có nút tải file sao lưu.

## 5m. Màn hình DBM – Bảo trì CSDL (lát QT4, 15/11/2026 – design.md mục 21.13)

- Khuôn `ListPage` + `DataGrid`, thanh tab `viewTabs` như BAK: **Bảng** (mặc định), **Phiên và khóa**, **Lịch sử**. Cột Tình trạng là đèn ô vuông
  kèm chữ (Ổn / Cần xem; lịch sử Đang chờ / Đang chạy trung tính, Xong xanh, Lỗi đỏ), dòng đèn vàng / đỏ tô dấu; dung lượng, số dòng, pid canh
  phải phông đơn cách (`formatBytes` dùng chung `pages/system/opsFormat.ts`).
- Thông báo đầu trang: tab Bảng "CSDL <tên>: <dung lượng> · n bảng · số liệu lúc …" (vàng khi có bảng cần xem); tab Phiên "n phiên · n phiên chờ
  khóa" + tài khoản đang đọc; tab Lịch sử lịch hằng tuần và lượt kế tiếp (hoặc "Lịch hằng tuần đang tắt trên máy này").
- Nút lệnh (quyền chạy): tab Bảng **VACUUM ANALYZE**, **VACUUM cả CSDL** (icon `ClearOutlined` vàng), **REINDEX** (`ToolOutlined` vàng) – thao tác
  trên dòng đang chọn, chưa chọn thì khóa kèm chú thích; mỗi nút hỏi lại ("Chạy" / "Không chạy") rồi chuyển sang tab Lịch sử. Tab Phiên **Ngắt
  phiên…** (`StopOutlined` đỏ), khóa khi chưa chọn hoặc là phiên đang đọc. Mọi tab có **Làm mới** (vàng).
- Hộp thoại **Ngắt phiên n** (việc nguy hiểm Q6): khối cảnh báo vàng (tài khoản · ứng dụng · máy · trạng thái + hậu quả), câu lệnh của phiên
  trong ô chỉ đọc, lỗi máy chủ là khối đỏ trong hộp thoại (không đóng); ô **Lý do**, **Mật khẩu của bạn**, **Gõ "NGẮT PHIÊN" để xác nhận**; nút
  **Ngắt phiên** đỏ (icon dừng) bên phải, **Không ngắt** bên trái. Các việc nguy hiểm QT sau dùng cùng bố cục, đổi chữ xác nhận.

## 5n. Màn hình AGT – Bộ thực thi, chế độ bảo trì (lát QT5, 15/11/2026 – design.md mục 21.14)

- AGT theo khuôn `ListPage` + `DataGrid`, thanh tab `viewTabs`: **Môi trường** (mặc định), **Lệnh**. Cột Tình trạng là đèn ô vuông kèm chữ: môi
  trường Đang liên lạc xanh, Mất liên lạc vàng, Ngừng dùng / Chưa có khóa đỏ, Chưa liên lạc trung tính; lệnh Đang chờ / Đang chạy trung tính,
  Xong xanh, Lỗi đỏ, Hết hạn / Đã hủy vàng. Dòng đèn vàng / đỏ tô dấu. Môi trường của chính máy đang mở ghi "(máy này)" sau tên; khóa chỉ hiện
  "…4 ký tự cuối".
- Nút (quyền quản lý, thao tác trên dòng đang chọn, chưa chọn thì khóa kèm chú thích): **Khai môi trường…** (icon cộng xanh), **Sửa…** (vàng),
  **Tạo khóa…** (icon chìa khóa vàng), **Lệnh thử** (icon gửi xanh); tab Lệnh **Hủy lệnh** (icon dừng đỏ, chỉ lệnh đang chờ, hỏi lại). Mọi tab có
  **Làm mới**. Còn lệnh chờ / chạy thì lưới tự làm mới 5 giây.
- Hộp thoại **Tạo khóa** theo bố cục việc nguy hiểm 5m: khối cảnh báo vàng (khóa cũ hết hiệu lực), lỗi máy chủ khối đỏ trong hộp thoại, ô **Mật
  khẩu của bạn**, **Gõ "TẠO KHÓA" để xác nhận**. Tạo xong hộp thoại đổi sang **khóa hiện một lần**: cảnh báo vàng "chỉ hiện một lần", khóa trong
  ô chỉ đọc phông đơn cách, nút **Sao chép khóa**, nút đóng "Đã chép khóa – đóng".
- Bấm đúp dòng lệnh mở `DetailDrawer` chỉ xem: tab Thông tin, **Nhật ký bộ thực thi** (khung `.erp-error-detail`, phông đơn cách, cuộn).
- **Chế độ bảo trì**: dải môi trường (mục 5j) **đỏ** "ĐANG BẢO TRÌ: <câu thông báo>" (đứng sau "DEV" / "MÔI TRƯỜNG TEST", trước cảnh báo cấu hình)
  ở mọi màn hình kể cả đăng nhập, Portal, `/duyet`. SYS: thông báo đỏ đầu trang (giờ bật, người bật, câu thông báo, lý do) + nút **Bật chế độ bảo
  trì…** (icon cờ lê vàng; nút OK hộp thoại đỏ "Bật bảo trì") / **Tắt chế độ bảo trì…** (icon dấu tích xanh); hộp thoại có ô câu thông báo (khi
  bật) và **Lý do** bắt buộc. Lát QT7 đã chuyển nút sang tab Bảo trì của CFG (mục 5p).

## 5o. Màn hình RST – Phục hồi (lát QT6, 15/11/2026 – design.md mục 21.15)

- Khuôn `ListPage` + `DataGrid`, thanh tab `viewTabs`: **Lịch sử** (mặc định), **Bản pg_dump**. Cột Tình trạng là đèn ô vuông kèm chữ: Đang chờ /
  Đang khôi phục / Đang hoán đổi / Đã bỏ trung tính, **Chờ kiểm tra** vàng, Đã hoán đổi xanh, Lỗi đỏ; dòng Chờ kiểm tra / Lỗi tô dấu. Còn lần đang
  chạy thì lưới tự làm mới 5 giây.
- Thông báo đầu trang: "CSDL đang chạy: … · bộ thực thi máy này (…): sẵn sàng / chưa sẵn sàng" (xanh dương / vàng kèm lý do); máy chủ PostgreSQL có
  CSDL khác thì thêm khối **vàng** liệt kê tên và câu "khôi phục về mốc thời gian đưa CẢ các CSDL này về mốc".
- Nút (quyền chạy, thao tác trên dòng đang chọn, chưa chọn / không hợp thì khóa kèm chú thích): tab Lịch sử **Về mốc thời gian…** (icon đồng hồ
  vàng), **So sánh lại** (vàng), **Hoán đổi…** (icon `SwapOutlined` **đỏ**), **Bỏ bản khôi phục** (icon đỏ, hỏi lại); tab Bản pg_dump **Khôi phục từ
  bản này…** (icon `CloudDownloadOutlined` **xanh**). Mọi tab có **Làm mới**.
- Hộp thoại **Về mốc thời gian**: ô Ngày (`DateInput`) + Giờ (HH:mm) + nút **Lập kế hoạch**; khối kế hoạch xanh (Full + Diff + số file WAL, tên đoạn
  đầu → cuối) hoặc đỏ (lý do: chưa có Full, đứt WAL, mốc sau WAL cuối, chưa khai thư mục WAL), cảnh báo vàng từng dòng; đủ chuỗi mới hiện ô Lý do,
  nút **Khôi phục** mới mở.
- Hộp thoại **Hoán đổi bản khôi phục** theo bố cục việc nguy hiểm 5m: khối cảnh báo vàng (việc bộ thực thi sẽ làm, dữ liệu nhập sau thời điểm bản
  khôi phục không còn, bảng vận hành giữ nguyên; mốc thời gian thêm câu **đậm** về CSDL khác cùng máy chủ), lỗi máy chủ khối đỏ trong hộp thoại, ô
  **Lý do**, **Mật khẩu của bạn**, **Gõ "KHÔI PHỤC" để xác nhận**; nút **Hoán đổi** đỏ (icon) bên phải, **Không hoán đổi** bên trái.
- Bấm đúp dòng mở `DetailDrawer` chỉ xem: tab Thông tin, **So sánh số dòng** (`lazy` – dải thông báo giờ so sánh + phiên bản CSDL hai bên, lưới Bảng
  / So sánh (đèn: Bằng nhau, Khác, Bảng vận hành) / Đang chạy / Bản khôi phục / Chênh lệch, số canh phải phông đơn cách), **Kế hoạch** (mốc thời gian).

## 5p. Màn hình CFG – Cấu hình truy cập (lát QT7, 15/11/2026 – design.md mục 21.16)

- Khuôn `ListPage` + `DataGrid`, thanh tab `viewTabs`: **Chính sách** (mặc định), **Bảo mật**, **Bảo trì**; mọi tab dùng chung cột `overviewColumns`
  (Nhóm, Hạng mục, Giá trị, Tình trạng – đèn ô vuông kèm chữ, Ghi chú) như SYS. Chính sách: đèn vàng khi lỏng hơn mức khuyên dùng cho TEST / PROD
  (mật khẩu < 10 ký tự, tắt chặn mật khẩu phổ biến, chưa khai dải IP); Bảo mật: header, HTTPS (vàng khi HTTP), proxy, giới hạn tần suất – đỏ khi đang
  bỏ kiểm tra dải IP bằng file cấu hình; Bảo trì: trạng thái (đỏ khi đang bật), câu thông báo, lần bật / tắt gần nhất.
- Thông báo đầu trang: "Lưu lần cuối dd/MM/yyyy HH:mm – người lưu" (hoặc "Đang dùng giá trị mặc định") + số hạng mục cần xem (vàng / xanh dương);
  tab Bảo trì khi đang bật: thông báo đỏ "Đang bảo trì: …".
- Nút: tab Chính sách **Sửa cấu hình…** (icon bút, vàng), **Đăng xuất tất cả…** (icon đăng xuất, **đỏ**) – quyền quản lý CFG; tab Bảo trì **Bật / Tắt
  chế độ bảo trì…** (như mục 5n); mọi tab **Làm mới** (vàng).
- Hộp thoại **Sửa cấu hình truy cập** (rộng 640px): ô số hai cột (`.erp-cfg-form-grid`, màn hình hẹp một cột), dưới mỗi ô ghi khoảng cho phép; ô
  đánh dấu "Chặn mật khẩu phổ biến (n mật khẩu nhúng sẵn)"; ô nhiều dòng Mật khẩu cấm thêm, Dải IP (gợi ý ghi "IP của bạn: …"); **Lý do** bắt buộc;
  lỗi máy chủ là khối đỏ đầu hộp thoại, không đóng; **Bỏ qua** trái – **Lưu** (icon dấu tích xanh) phải.
- Hộp thoại **Đăng xuất tất cả** theo bố cục việc nguy hiểm mục 5m: cảnh báo vàng (mọi người phải đăng nhập lại, phiếu nhập dở mất; phiên của bạn
  giữ lại), ô **Lý do**, **Mật khẩu của bạn**, **Gõ "ĐĂNG XUẤT TẤT CẢ" để xác nhận**; nút **Đăng xuất tất cả** đỏ (icon) phải, **Không đăng xuất** trái.

## 5q. Màn hình REL – Trung tâm phát hành (lát QT8, 15/11/2026 – design.md mục 21.17)

- Danh sách: khuôn `ListPage` + `DataGrid` – Phiên bản, Trạng thái (đèn kèm chữ: Đã nhận / Đã đóng / Đã hủy trung tính, Đang UAT vàng, UAT đạt / Đã
  cài PROD xanh, Đã quay về đỏ), Kiểm thử "đã / tổng", Lỗi chưa xử lý, Nguồn, Dung lượng, Nhận lúc, Người nhận, Cài PROD lúc. Thông báo đầu trang:
  "Máy này: <môi trường> – phiên bản …" + bộ thực thi từng môi trường (đang liên lạc / mất liên lạc), "Chế độ một môi trường" khi chưa khai TEST; máy
  TEST thông báo vàng chỉ xem. Nút **Nhận gói tải lên…** (icon tải lên xanh), **Nhận từ GitHub…** (icon tải về xanh, khi đã khai kho), **Làm mới**.
  Bấm đúp mở phiếu.
- Hộp thoại nhận gói: thông báo xanh dương cách đóng gói, nút **Chọn file…** (dòng dưới ghi tên file – dung lượng), ô SHA256 (64 ký tự hex), Ghi chú;
  **Bỏ qua** trái – **Nhận gói** phải.
- Phiếu: khuôn `DocumentForm`, số phiếu là **phiên bản** trong ô chữ; đầu phiếu ba cột (Gói, Mức quay về tối thiểu, UAT) rồi các ô rộng: SHA256
  (phông đơn cách `.erp-mono`), Thay đổi CSDL (chỉ đọc), Rủi ro, Cách quay lại, Ghi chú (sửa được tới khi khóa). Tab: **MỤC KIỂM THỬ** (lưới + nút
  dưới lưới Đạt xanh, Không đạt… đỏ, Chưa kiểm thử vàng – thao tác trên dòng đang chọn), **LỖI PHÁT HIỆN (n)** (Thêm lỗi… xanh, Xử lý lỗi… vàng),
  **LỊCH SỬ CÀI (n)** (Kết quả đèn kèm chữ: Đang chờ bộ thực thi trung tính, Xong xanh, Lỗi đỏ), **CHANGELOG** (khung `.erp-error-detail`), **ĐÍNH KÈM**.
  Lưới trong tab cao 280px (`.erp-rel-grid`), nút dưới lưới `.erp-rel-grid-buttons`.
- Nút bước ở thanh chân theo trạng thái và quyền: **Lưu** (xanh), **Cài TEST…** / **Cài PROD…** (icon tải lên xanh), **Xác nhận UAT đạt…** (dấu tích
  xanh), **Quay về bản cũ…** (icon quay lại **đỏ**), **Đóng phiếu**, **Thêm ghi chú…** (vàng), **Hủy phiếu…** (đỏ). Còn lần cài chờ thì nút bước
  khóa, thông báo xanh dương "Đang chờ bộ thực thi…" và trang tự làm mới 5 giây.
- Hộp thoại **Cài bản … lên TEST / PROD** và **Quay về** theo bố cục việc nguy hiểm mục 5m: cảnh báo vàng việc bộ thực thi sẽ làm, ô **Lý do**, **Mật
  khẩu của bạn**, **Gõ "CÀI x.y.z"** / **"QUAY VỀ"**; nút Cài PROD / Quay về **đỏ**, Cài TEST thường; quay về có ô chọn mức (chỉ mức ≥ tối thiểu, kèm hậu
  quả dữ liệu). Xác nhận UAT: cảnh báo vàng điều còn vướng, ô Ý kiến bắt buộc khi còn vướng.

## 5r. Màn hình TST – Làm mới TEST, nút "Báo lỗi / góp ý" (lát QT9, 16/11/2026 – design.md mục 21.18)

- TST theo khuôn `ListPage` + `DataGrid`, thanh tab `viewTabs`: **Tình trạng** (mặc định – cột `overviewColumns` như SYS: Nguồn dữ liệu / Máy TEST /
  Sau khi làm mới; dòng đèn vàng / đỏ tô dấu), **Lịch sử** (lệnh làm mới: Ra lệnh lúc, Tình trạng đèn kèm chữ, Người, Lý do, Thời gian chạy, Kết quả;
  ô lọc Từ ngày – Đến ngày, mặc định 90 ngày). Thông báo đầu trang: đỏ "Chưa làm mới được" + danh sách điều chặn (`.erp-tst-list`); vàng / xanh "Đủ điều
  kiện làm mới – TEST sẽ có dữ liệu … lúc …" + lưu ý; xanh dương khi đang chờ bộ thực thi (tự làm mới 5 giây); máy TEST vàng "chỉ xem".
- Nút **Làm mới TEST…** (icon đồng bộ) khóa khi còn điều chặn, chú thích nêu lý do; **Làm mới** (vàng). Hộp thoại theo bố cục việc nguy hiểm mục 5m:
  cảnh báo vàng việc bộ thực thi sẽ làm và dữ liệu TEST mất, ô **Lý do**, **Mật khẩu của bạn**, **Gõ "LÀM MỚI TEST"**; nút **Làm mới TEST** đỏ phải,
  **Không làm mới** trái. Ô lý do / mật khẩu / chữ xác nhận dùng chung `pages/system/DangerFields.tsx` (REL, TST).
- **Báo lỗi / góp ý** (chỉ máy TEST đã khai): nút chữ có icon con bọ trên thanh trên AppShell, trước chuông thông báo. Hộp thoại 560px: thông báo xanh
  dương (lỗi về phiếu phát hành, cách chụp Win+Shift+S), **Tiêu đề** (bắt buộc), **Mô tả**, **Màn hình** (điền sẵn đường dẫn trang), ô **Ảnh màn hình**
  `.erp-feedback-paste` (viền 1px gạch, nền `--erp-readonly-bg`, bấm vào rồi Ctrl+V; có ảnh thì xem trước tối đa 240px + tên file), nút **Chọn ảnh…** /
  **Bỏ ảnh**; **Bỏ qua** trái – **Gửi** (icon gửi xanh) phải; lỗi máy chủ là khối đỏ trong hộp thoại.
- AGT thêm nút **Tạo khóa dịch vụ…** (icon chìa khóa vàng) và cột "Khóa dịch vụ" (…4 ký tự cuối); hộp thoại như "Tạo khóa", chữ theo loại khóa.
- REL: phiếu Đang UAT / UAT đạt mà TEST đang chạy bản của phiếu có nút **Quay về bản cũ trên TEST…** (icon quay lại đỏ), hộp thoại như quay về PROD
  thêm câu "phiếu trở lại Đã nhận".

## 5s. Xem đề nghị, chữ ký người dùng, logo công ty (lát IN1, 18/11/2026 – design.md mục 20.4)

- **Xem đề nghị**: `DocumentForm` prop `printPreview` – nút chữ có icon mắt (vàng) thay nút "In phiếu" (Ctrl+P vẫn mở). Hộp thoại
  `PrintPreviewModal` (`components/PrintPreview/`) rộng 940px: hàng nút **In…** (icon máy in), **Tải PDF** (icon tải về), **Làm mới**, dòng chữ mờ
  "Đã in n lần · lần gần nhất … – người" / "Chưa in lần nào" sát phải, **Đóng** (đỏ); vùng xem nền `--erp-print-viewer-bg`, viền 1px, cao 72vh,
  trang PDF trong khung (ẩn thanh công cụ trình xem PDF – in qua nút In để đếm lần in). In bằng khung ẩn `.erp-print-frame`.
  **Phiếu nhiều mẫu** (đề nghị mua loại B: 1B và 2B – lát IN1-13, 20/11/2026): đầu hàng nút có ô **Mẫu in:** (`PrintPreviewProps.forms`,
  `.erp-print-form`) – nút ô vuông cao 22px nền `--erp-tab-bg`, mẫu đang chọn nền `--erp-tab-active` chữ trắng đậm, rê chuột thấy tên mẫu đầy đủ,
  vạch dọc ngăn với nhóm nút In; đổi mẫu thì bản xem, In, Tải PDF đều theo mẫu đang chọn (mặc định mẫu đầu).
- **Xem bảng theo dõi** (PO, lát IN2), **Xem bảng tổng hợp** (BTH), **Xem kết quả hỏi giá** (RFQ): cùng `printPreview` + `PrintPreviewModal`, tiêu đề
  "Xem bảng theo dõi mua hàng <số> – mẫu 4A/4B", "Xem bảng tổng hợp <số> – mẫu 2A/2B", "Xem kết quả hỏi giá <số> – mẫu 3a/3A/3B"; **Xem đề nghị
  thanh toán** ("… – mẫu 5A/5B/1C"), **Xem bảng kê** ("Xem bảng kê chi phí tạp vụ <số> – mẫu 2C").
- **Trang in** (PDF, không phải màn hình): giữ nguyên sheet biểu mẫu (mục 20.4 IN1-1, IN2-1 – 1A/1B, 4A/4B); góc dưới phải mọi trang khung viền mảnh, chữ 9 phông không
  chân (giờ in, người in, số phiếu, lần in, trang, tình trạng phê duyệt); dấu chìm đỏ nhạt nghiêng khi chưa duyệt.
- **CKY – Chữ ký của tôi**: khuôn `ListPage` + `DataGrid`, một tab "LỊCH SỬ CHỮ KÝ" (`viewTabs`), không có "Tạo mới". Thông báo đầu trang
  `SignatureCurrent`: ô ký thử đúng cỡ in (`.erp-signature-sample` – rộng 46 mm, viền đen, nền `--erp-paper`, phông có chân như bản in) + đèn ô
  vuông kèm chữ "Đang dùng chữ ký phiên bản n – từ …" (xanh) / "Chưa có chữ ký đang dùng" (vàng). Lưới: Phiên bản, Tình trạng (đèn: Đang dùng
  xanh, Hết hiệu lực trung tính), Hiệu lực từ / Đến, Người tải lên, Cách tải, Lý do, Ngừng dùng. Nút **Tải chữ ký mới…** (icon tải lên xanh),
  **Ngừng dùng chữ ký…** (icon dừng đỏ, hỏi lại).
- Hộp thoại **Tải chữ ký** 860px: ba khung cùng hàng (màn hình hẹp một cột) có dải tiêu đề `--erp-grid-head`: Ảnh gốc · Sau khi phần mềm xử lý
  (nền ô vuông `--erp-check-tile` để thấy phần trong suốt, viền gạch đỏ quanh ảnh đã cắt) · Xem thử trên ô ký; hàng tùy chọn Màu mực (Xanh / Đen
  / Giữ màu gốc), thanh trượt Tách nền 1–10, nút xoay trái / phải; ô **Lý do tải thay** (dấu * đỏ) khi làm thay người khác; dòng gợi ý chữ mờ;
  chân hộp thoại **Chọn ảnh…** (icon thư mục) trái – **Bỏ qua** (đỏ), **Lưu chữ ký** (xanh, khóa khi chưa có ảnh / thiếu lý do) phải.
- Ngăn kéo người dùng: ô **Danh xưng** sau Chức vụ; tab **CHỮ KÝ** (`lazy`, chỉ khi có quyền) – khối cảnh báo viền `--erp-light-yellow` "Bạn đang quản
  lý chữ ký của …", ô ký thử, lưới phiên bản cao 200px, nút **Tải chữ ký thay…** / **Ngừng dùng chữ ký…**.
- Ngăn kéo công ty: tab **LOGO** (`lazy`) – khung ảnh 160 × 120px viền 1px nền `--erp-paper`, dòng gợi ý, nút **Tải logo… / Thay logo…** (xanh),
  **Xóa logo** (đỏ, hỏi lại).

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

## 8a. Một màn hình trong vùng nội dung, hỏi lại khi còn phiếu chưa lưu

- **Bỏ thẻ màn hình** (người dùng yêu cầu 15/11/2026 – trả lại như trước 13/11/2026): vùng nội dung của `AppShell` chỉ hiện **một màn hình**;
  bấm cây điều hướng, menu, ô lệnh, bấm đúp ô số chứng từ thì mở thay màn hình đang xem; nút **Quay lại** dùng lịch sử trình duyệt. Không còn
  thanh thẻ, Alt+1…6, Shift+bấm, ô lệnh `/n<mã>` / `/o<mã>`. Lỗi giao diện chỉ làm hỏng vùng nội dung (`ErrorBoundary` bọc `Outlet` – mục 5k).
- **Hỏi lại khi còn phiếu chưa lưu** (yêu cầu 13/11/2026, giữ lại): phiếu (`DocumentForm`) hoặc ngăn kéo đang sửa (`DetailDrawer`) mà người dùng
  đã sửa ô, chưa lưu thì:
  - rời trang (cây điều hướng, menu, ô lệnh, Quay lại, nút Đóng, Back trình duyệt) → hộp thoại "Rời trang khi phiếu chưa lưu": **Rời trang**
    (nút đỏ) / **Ở lại**;
  - đổi công ty, đăng xuất → "Còn phiếu chưa lưu": **Bỏ thay đổi** (nút đỏ) / **Ở lại**;
  - tải lại (F5) hoặc đóng tab trình duyệt → câu hỏi của trình duyệt.
  Dấu "chưa lưu" (`utils/formDirty.ts`) bật khi người dùng sửa ô, tắt khi nạp phiếu từ máy chủ, lưu thành công (`useDocumentFormSync`) hoặc mở
  lại ngăn kéo; trang khác dùng `useUnsavedCheck` (`app/unsaved.ts`); khung hỏi ở `components/AppShell/useUnsavedGuard.ts`.
