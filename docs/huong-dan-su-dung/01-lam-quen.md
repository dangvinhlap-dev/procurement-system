# 1. Làm quen với phần mềm

## 1.1 Đăng nhập, đổi mật khẩu, chọn công ty

1. Mở trình duyệt (Chrome, Edge) vào địa chỉ do quản trị cung cấp, ví dụ `http://procurement.congty.vn` hoặc `http://<IP máy chủ>`.
2. Nhập **Tên đăng nhập** và **Mật khẩu**, bấm **Đăng nhập**. Phiên làm việc kéo dài 8 giờ, hết hạn phải đăng nhập lại.
3. Sai mật khẩu **5 lần liên tiếp** thì tài khoản bị **khóa tạm 15 phút**; màn hình báo giờ mở khóa. Cần mở ngay thì nhờ quản trị
   **Đặt lại mật khẩu** (Hệ thống → Người dùng).
4. Lần đầu đăng nhập: bấm tên mình ở góc phải trên → **Đổi mật khẩu**.
5. **Công ty đang làm việc**: ô chọn công ty trên thanh trên cùng. Mọi danh sách, chứng từ, báo cáo chỉ hiện dữ liệu của công ty
   đang chọn (trừ báo cáo "toàn tập đoàn" khi có quyền). Chỉ thấy những công ty mình được gán vai trò.
6. Đăng xuất: bấm tên mình → **Đăng xuất**.

> Mỗi lần đăng nhập, đăng xuất, sai mật khẩu, đổi mật khẩu, đổi công ty đều được ghi vào Nhật ký truy cập.

## 1.2 Bố cục màn hình

Giao diện theo phong cách ERP desktop cổ điển: chữ nhỏ, dày đặc thông tin, kẻ ô rõ ràng.

| Vùng | Nội dung |
| --- | --- |
| Hàng 1 – menu chữ | Các phân hệ: **Hằng ngày, Mua hàng, Kho, Giá và NCC, Đấu thầu, Báo cáo, Hệ thống**. Chữ gạch chân là phím tắt Alt (ví dụ Alt+M mở Mua hàng) |
| Hàng 2 – thanh công cụ | **Ô lệnh** (gõ mã màn hình), ô chọn công ty, chuông thông báo (số chưa đọc), tên người dùng |
| Cây điều hướng (bên trái) | Danh sách màn hình của phân hệ, có số đếm việc đang chờ; kéo mép để đổi độ rộng, ẩn/hiện được (trình duyệt nhớ lại) |
| Vùng làm việc | Danh sách hoặc chứng từ đang mở |

Chỉ màn hình mình **có quyền** mới hiện trên menu, cây điều hướng và ô lệnh.

### Ô lệnh – mở nhanh màn hình bằng mã

Gõ mã rồi Enter, ví dụ `PO` mở danh sách đơn mua hàng, `GRN` mở phiếu nhập mua. Gõ `*` rồi Enter để liệt kê **mọi màn hình** được
phép; dùng mũi tên lên/xuống + Enter để mở. Bảng mã đầy đủ:

| Phân hệ | Mã màn hình |
| --- | --- |
| Hằng ngày | `HOME` Trang chủ · `TODO` Việc cần làm · `NOTI` Thông báo · `DOCS` Tra cứu chứng từ |
| Mua hàng | `PR` Đề nghị mua · `PO` Đơn mua hàng · `POTRK` Theo dõi giao hàng · `KHN` Kế hoạch nhu cầu · `KHNTH` Tổng hợp nhu cầu · `DHG` Đặt hàng gộp · `BUDG` Ngân sách bộ phận · `CTR` Hợp đồng nguyên tắc |
| Kho | `GRN` Nhập mua NCC · `RTV` Xuất trả NCC · `TRF` Chuyển kho nội bộ · `ICS` Xuất bán nội bộ · `ICR` Nhập mua nội bộ · `ADJ` Điều chỉnh, kiểm kê · `STK` Tồn kho hiện tại · `CARD` Thẻ kho |
| Giá và NCC | `SUP` Nhà cung cấp · `SUPAPR` Duyệt NCC · `HSN` Hồ sơ NCC mới · `SGRP` Nhóm NCC · `COI` Xung đột lợi ích · `NDQ` NCC độc quyền · `GTH` Duyệt NCC và giá tháng · `KSG` Khảo sát giá thị trường · `CKB` Chiết khấu sản lượng gộp · `CKBT` Theo dõi bậc chiết khấu · `SUPCOV` NCC đủ điều kiện · `SUPREV` Đánh giá NCC định kỳ · `ITEM` Hàng hóa · `CAT` Nhóm hàng · `UOM` Đơn vị tính · `IPR` Bảng giá nội bộ · `PRL` Bảng giá NCC · `PCMP` So sánh giá |
| Đấu thầu | `SRC` Tìm nguồn · `RFQ` Yêu cầu báo giá · `QCMP` So sánh báo giá |
| Báo cáo | `NXT` Nhập – xuất – tồn · `SLOW` Chậm luân chuyển · `EXP` Sắp hết hạn · `REORD` Gợi ý đặt hàng · `GSTK` Tồn tập đoàn · `TRANSIT` Hàng đi đường · `ICREC` Đối chiếu nội bộ · `PRPEND` PR chưa xử lý · `SLA` SLA mua hàng · `QCDEV` Sai lệch nhận hàng · `MATCH3` Đối chiếu PO – nhận – HĐ · `RPSUP` Mua hàng theo NCC · `SUPEVAL` Đánh giá NCC · `SAVE` Tiết kiệm đấu thầu · `PRCW` Mua ngoài hợp đồng · `BCBAN` Báo cáo tháng cho Ban · `KPI` Bảng KPI · `PRVAR` Biến động giá mua · `SAVSTD` Tiết kiệm so giá chuẩn · `PHIS` Dữ liệu mua lịch sử · `PHMAP` Ánh xạ dữ liệu mua · `ANOM` Dấu hiệu bất thường · `VIOL` Sổ vi phạm · `PHAN` Phân tích giá hợp nhất · `ACCX` Xuất cho kế toán |
| Hệ thống | `COMP` Công ty · `WH` Kho · `DEPT` Bộ phận · `USER` Người dùng · `ROLE` Vai trò · `APRL` Quy trình duyệt · `PER` Kỳ kho · `LOG` Nhật ký thao tác · `ACC` Nhật ký truy cập · `RATE` Xếp loại NCC · `DSDM` Đánh số danh mục · `DSCT` Đánh số chứng từ · `SEC` Tham số hệ thống |

## 1.3 Danh sách (lưới dữ liệu)

Mọi màn hình danh sách có cùng cách dùng:

- **Tìm kiếm**: gõ vào ô Tìm kiếm – tìm được cả khi gõ **có dấu hoặc không dấu**, nhiều từ không cần đúng thứ tự. Dấu `*` là ký tự đại
  diện: `b*long` khớp "Bu lông".
- **Bộ lọc** riêng từng màn hình (trạng thái, khoảng ngày, kho, nhóm hàng…). Ô lọc ngày gõ tay được (xem 1.5).
- **Sắp xếp**: bấm tiêu đề cột. **Co giãn cột**: kéo mép phải tiêu đề cột; bấm đúp mép cột để trả về độ rộng mặc định.
- **Chọn cột** hiện/ẩn và **Nhóm theo** một cột (kéo tiêu đề cột vào vùng nhóm).
- **Mở chi tiết**: bấm đúp dòng hoặc chọn dòng rồi Enter. Dòng đang chọn tô vàng nhạt.
- **Đèn trạng thái** (ô vuông màu) luôn kèm chữ: xanh lá – hoàn thành/hiệu lực, vàng – đang chờ, đỏ – hủy/lỗi.
- **Xuất Excel**: xuất đúng các cột đang hiện và các dòng đã lọc ra file `.xlsx` (ô số, ô ngày đúng kiểu, có dòng tiêu đề cố định).
- Nút **Tạo mới** và các nút riêng của màn hình nằm trên đầu danh sách.

## 1.4 Form chứng từ (phiếu)

Mọi phiếu (PR, PO, RFQ, hợp đồng, phiếu kho…) có cùng khuôn:

| Phần | Nội dung |
| --- | --- |
| Tiêu đề + thanh lệnh | Số phiếu, trạng thái; các nút Lưu, Gửi duyệt, Duyệt, Từ chối, Ghi sổ, Bỏ ghi sổ, Hủy, In phiếu… tùy trạng thái và quyền |
| Thông tin phiếu (đầu phiếu) | Ngày chứng từ, NCC, kho, bộ phận… Ô có dấu **\*** đỏ là bắt buộc. Thu gọn được bằng nút nhỏ ở mép trái |
| Tab dòng hàng | Lưới nhập như Excel |
| Tab phụ | **Ghi chú**, **Đính kèm (n)**, **Lịch sử duyệt**, **Thẩm định**… tùy loại phiếu |
| Thanh chân | Tổng tiền hàng, VAT, tổng thanh toán; thông báo sau thao tác (đèn + chữ) |

**Màu icon nút** theo mức quan trọng: **đỏ** – không hoàn tác được/mất dữ liệu (Xóa, Hủy, Đóng, Bỏ ghi sổ); **xanh lá** – ghi/tạo dữ liệu
(Lưu, Thêm, Duyệt, Gửi duyệt); **vàng** – thao tác thường (Quay lại, In, Tìm…).

**Số phiếu** do hệ thống cấp khi **lưu lần đầu**, theo mẫu của công ty (mặc định `LOẠI-CTY-YYMM-0000`, ví dụ `PO-BHA-2611-0001`). Phiếu
hủy giữ số, không dùng lại.

### Lưới dòng hàng

- Gõ thẳng vào ô như Excel; **Enter** xuống ô cùng cột dòng dưới, ở dòng cuối thì thêm dòng mới; **Tab/Shift+Tab** sang ô kế/trước.
- **F2** hoặc nút **Thêm dòng** thêm dòng; chọn dòng (bấm ô đầu dòng, Shift+bấm chọn nhiều) rồi **Xóa dòng**.
- Dòng có lỗi hiện **tam giác vàng** ở đầu dòng, rê chuột để xem lý do (ví dụ "Chưa nhập số lượng"). Dòng vượt giá/ngoài hợp đồng cũng
  có tam giác vàng kèm giải thích.
- Ô chỉ đọc có nền xám nhạt. Cột số canh phải, định dạng Việt Nam (1.234.567,89); tiền VND không số lẻ.

### Ô chọn danh mục (NCC, mặt hàng, kho, bộ phận…)

- Gõ ≥ 2 ký tự để tìm (có dấu/không dấu, theo mã hoặc tên).
- Gõ **`*` rồi Enter** để liệt kê **toàn bộ** danh mục (tối đa 500 dòng), rồi chọn bằng mũi tên + Enter hoặc bấm chuột.
- `*` giữa từ khóa là ký tự đại diện.

## 1.5 Ô ngày

Ngày hiển thị **dd/MM/yyyy**. Gõ tay được: `05/12/2026`, `5/12/2026`, `05.12.2026` hoặc gõ nhanh `05122026`; hoặc bấm biểu tượng lịch để
chọn. Giờ hiển thị theo giờ Việt Nam (HH:mm).

## 1.6 Phím tắt

| Phím | Tác dụng |
| --- | --- |
| **F1** | Trợ giúp ô: **lịch sử thay đổi** của ô đang có con trỏ hoặc ô dưới chuột (ai tạo/sửa/xóa, lúc nào, giá trị cũ → mới) và thông tin ô (bảng, cột, kiểu dữ liệu) |
| Ctrl+S | Lưu |
| F2 | Thêm dòng |
| Ctrl+P | Xem trước in / In phiếu |
| F3 | Quay lại |
| Esc | Đóng hộp thoại, ngăn kéo |
| Enter (trong lưới) | Xuống ô cùng cột dòng dưới; dòng cuối thì thêm dòng |
| Tab / Shift+Tab | Sang ô kế / ô trước |
| Alt + chữ gạch chân | Mở phân hệ trên menu |

Nút **Lịch sử thay đổi** trên phiếu xem toàn bộ thay đổi của cả phiếu, kể cả dòng đã xóa.

## 1.7 Ghi chú và đính kèm

Mọi form chứng từ/phiếu có:

- Ô **Ghi chú** nhập được ngay khi tạo phiếu.
- Tab **Đính kèm (n)**: chọn file **ngay khi tạo phiếu** (file vào hàng chờ, lưu phiếu xong tự tải lên); phiếu đã duyệt/ghi sổ vẫn đính
  kèm thêm được. Mặc định **tối đa 2 MB/file** (quản trị đổi được), chỉ nhận tài liệu, ảnh, file nén, thư điện tử. File chọn quá cỡ bị
  báo ngay.
- Xóa đính kèm là xóa mềm (file vẫn giữ trên máy chủ, có nhật ký). Quyền xem/thêm đính kèm theo quyền xem/sửa phiếu.

## 1.8 Gửi duyệt và duyệt phiếu

Áp dụng cho PR, PO, bảng giá NCC, kết quả chọn thầu RFQ, hợp đồng, phụ lục, NCC, NCC độc quyền, phiếu GTH, CKB, hồ sơ NCC mới…

1. Người lập lưu phiếu (trạng thái **Nháp**) rồi bấm **Gửi duyệt**. Hộp thoại liệt kê **từng cấp duyệt và những người duyệt được** trước
   khi gửi.
2. Phiếu chuyển **Chờ duyệt**; trạng thái ghi rõ "Chờ duyệt: <tên người>". Người duyệt nhận **thông báo** và thấy phiếu trong
   **Việc cần làm**.
3. Người duyệt mở phiếu → **Duyệt** (ghi ý kiến nếu cần) hoặc **Từ chối** (bắt buộc lý do). Hộp thoại cho biết người lập, thời điểm gửi
   và ai duyệt tiếp sau mình.
4. Duyệt đủ mọi cấp → **Đã duyệt**. Bị từ chối → phiếu về **Nháp** kèm lý do; sửa rồi gửi lại là một **lượt** mới.
5. Người lập rút được phiếu đang chờ (với các loại có lệnh Rút).

Quy tắc chung:

- **Người lập không tự duyệt** phiếu của mình; trong một lượt, **mỗi cấp một người khác nhau** (người đã duyệt cấp trước không duyệt cấp sau).
- Người có **kê khai xung đột lợi ích** còn hiệu lực với NCC trên phiếu không duyệt được phiếu đó (xem 04 – mục Xung đột lợi ích).
- Số cấp, ngưỡng giá trị, vai trò từng cấp do quản trị cấu hình ở **Quy trình duyệt (APRL)**. Công ty chưa cấu hình loại phiếu nào thì
  phiếu đó chỉ cần **một cấp**, ai có quyền duyệt cũng duyệt được.
- Một số phiếu có **chặng duyệt thêm** khi có ngoại lệ (lệch giá, vượt ngân sách, mua ngoài danh sách tháng, vượt giá trần…) – xem 03.
- Tab **Lịch sử duyệt** dạng cây: Lập phiếu → từng lượt gửi (người gửi, ngày giờ) → từng cấp (người duyệt, ngày giờ, ý kiến; "Chờ duyệt"
  hoặc "Sẽ duyệt" cho cấp chưa tới), kèm danh sách **người duyệt tiếp theo**.
- Hai người cùng duyệt/sửa một phiếu cùng lúc: chỉ người đầu được ghi, người sau nhận thông báo "Dữ liệu vừa được người khác thay đổi
  – hãy tải lại trang".

## 1.9 Hằng ngày

| Màn hình | Dùng để |
| --- | --- |
| **Trang chủ** (HOME) | Ô số liệu của công ty đang làm việc theo quyền: phiếu chờ tôi duyệt, PR chưa xử lý, PO chờ giao/trễ hạn, RFQ, chứng từ kho nháp, hàng dưới tồn tối thiểu, lô hết/sắp hết hạn, hàng đi đường, kỳ kho; mua hàng tháng này so với tháng trước, top 5 NCC. Bấm ô để mở màn hình chi tiết |
| **Việc cần làm** (TODO) | Mọi phiếu đang chờ **chính mình** duyệt ở công ty đang làm việc |
| **Thông báo** (NOTI) | Phiếu chờ mình duyệt, phiếu mình lập được duyệt/bị từ chối (kèm lý do), ICR chờ nhận, kỳ kho mở lại, vi phạm KSNB… Bấm thông báo mở thẳng phiếu và đánh dấu đã đọc; có "Đánh dấu tất cả đã đọc". Chuông trên thanh trên cùng hiện số chưa đọc |
| **Tra cứu chứng từ** (DOCS) | Tìm mọi loại chứng từ được quyền xem; lọc Chưa hoàn thành / Hoàn thành / Đã hủy, Tôi lập / Tôi đã duyệt, cấp đang chờ; cột **Người duyệt tiếp theo**; bấm đúp mở phiếu |

## 1.10 In phiếu

Nút **In phiếu** (Ctrl+P) trên mọi phiếu đã lưu: PO, PR (kể cả hàng mới chưa có mã), phiếu nhập mua, biên bản giao nhận và kiểm tra hàng
hóa, trả NCC, chuyển kho, xuất bán / nhập mua nội bộ, biên bản kiểm kê. File PDF có số tiền bằng chữ và ô ký tên; tải về hoặc in từ trình
xem PDF của trình duyệt.

## 1.11 Nhập/xuất Excel

- **Xuất Excel**: mọi danh sách, báo cáo.
- **Nhập Excel**: danh mục (ĐVT, nhóm hàng, hàng hóa, nhóm NCC, NCC), bảng giá NCC, hợp đồng hiện hữu, dữ liệu mua lịch sử.
  Cách làm chung:
  1. Bấm **Tải file mẫu** – file có sheet Hướng dẫn; cột có dấu `*` là bắt buộc; dữ liệu từ dòng 2.
  2. Điền dữ liệu, giữ nguyên dòng tiêu đề.
  3. **Kiểm tra thử**: hệ thống báo lỗi theo từng dòng Excel, chưa ghi gì.
  4. Sửa hết lỗi rồi **Nhập thật** – chỉ ghi khi toàn file không còn lỗi.
- Số trong Excel đọc được cả kiểu `1.234,5` và `1234.5`. Mã danh mục để trống khi thêm mới (hệ thống tự cấp mã).
