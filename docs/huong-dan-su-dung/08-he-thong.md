# 8. Hệ thống và phân quyền

Dành cho người quản trị. Các màn hình nằm ở phân hệ **Hệ thống**.

## 8.1 Mô hình phân quyền

Quyền của một người = **vai trò** được gán **tại từng công ty** + **phạm vi dữ liệu** tại công ty đó:

| Lớp | Ý nghĩa |
| --- | --- |
| Công ty | Chỉ làm việc được ở công ty mình được gán. Mỗi công ty là pháp nhân độc lập: không thấy chứng từ công ty khác (trừ báo cáo toàn tập đoàn có quyền riêng) |
| Vai trò | Tập quyền chức năng dạng `Module.Đối tượng.Hành động`, ví dụ `Purchase.PO.Approve`. Vai trò **ADMIN** luôn đủ quyền |
| Phạm vi kho | Chỉ thấy/ghi chứng từ, tồn kho của các kho được gán (để trống = mọi kho) |
| Phạm vi nhóm hàng | Chỉ thấy mặt hàng, dòng chứng từ thuộc các nhóm được gán **và nhóm con** (để trống = mọi nhóm) |

Màn hình, nút lệnh người dùng không có quyền thì bị ẩn; mở thẳng đường dẫn thì báo không có quyền. Máy chủ kiểm tra lại mọi lệnh ghi.

## 8.2 Người dùng (USER)

1. **Tạo tài khoản**: tên đăng nhập (không đổi được), họ tên, email, mật khẩu ban đầu.
2. Tab gán quyền **tại công ty đang làm việc**: chọn **vai trò**, **kho**, **nhóm hàng**. Muốn gán ở công ty khác thì đổi công ty trên thanh
   trên cùng rồi gán.
3. **Đặt lại mật khẩu** (cũng mở khóa tạm), **Ngừng dùng** tài khoản.

Chặn tự nâng quyền: chỉ gán được vai trò có quyền nằm trong quyền của chính mình; không tự sửa quyền của mình.

## 8.3 Vai trò và phân quyền (ROLE)

- Tạo/sửa vai trò, **tick quyền theo module**. Vai trò ADMIN không sửa được.
- Nút **Bộ vai trò mẫu…** tạo nhanh các vai trò còn thiếu; ô **Bộ mẫu** chọn: tất cả, **Ban cung ứng theo sơ đồ phê duyệt** (bảng dưới) hoặc
  **Vị trí ký biểu mẫu CUVT** (bảng thứ hai). Vai trò đã có giữ nguyên quyền.

Bộ Ban cung ứng:

| Mã | Vai trò |
| --- | --- |
| TRUONGBANCUVT | Trưởng ban Cung ứng |
| PHOBAN | Phó ban cung ứng (Hội đồng) |
| HOIDONG | Hội đồng xét chọn & duyệt giá |
| CHUTICH | Chủ tịch HĐQT / Tổng Giám đốc – duyệt ngoại lệ |
| DAMPHAN | Nhóm Đàm phán & quản trị giá |
| VANHANH | Nhóm Vận hành mua hàng |
| VPBDIEUPHOI, VPBPHANTICH | Văn phòng Ban: điều phối; phân tích giá, đề xuất NCC |
| CUCTRUONGPHONG, CUCDATHANG | Phòng cung ứng chung: trưởng phòng; nhân viên cung ứng |
| THAMDINHCL, THAMDINHPL | Thẩm định chất lượng; thẩm định pháp lý |
| DENGHI, DONVIDATHANG | Người đề nghị mua (bộ phận); đơn vị đặt hàng |
| KHO | Kho |
| KETOAN | Kế toán |
| PHAPCHE | Pháp chế |
| KSNB | Kiểm soát nội bộ (chỉ xem, toàn tập đoàn) |
| LANHDAO | Lãnh đạo nhận báo cáo KSNB |

Bộ vị trí ký biểu mẫu CUVT (nhóm A, B, DV – từ 0.22.0):

| Mã | Vai trò |
| --- | --- |
| TRUONGPHONGBAN | Trưởng phòng/ban – duyệt đề nghị mua của bộ phận (người duyệt động: khai ở danh mục Bộ phận) |
| GDKHOI | Giám đốc khối (người duyệt động: khai ở danh mục Khối) |
| TPCUVT, NVCUVT | Phòng Cung ứng vật tư: trưởng phòng (duyệt); nhân viên (NCC, RFQ, PO – không duyệt) |
| TPTCKT, GDTCKT | Trưởng phòng, Giám đốc Tài chính – Nguồn quỹ – Kế toán |
| TPHC | Trưởng phòng Hành chính (kiểm định mức nhóm B – cấp duyệt thêm khi đề nghị mua có loại mua) |
| PHOCTUBCL | Phó Chủ tịch UBCL&PT |
| DENGHI, KHO, CHUTICH | Dùng chung với bộ Ban cung ứng |

> Khi nâng cấp lên bản mới có quyền mới, **vai trò đã tạo trước không tự có quyền mới** – xem mục "Lưu ý khi nâng cấp" trong `CHANGELOG.md`
> và tick thêm quyền cho vai trò đang dùng.

## 8.4 Quy trình duyệt (APRL)

Cấu hình theo **công ty đang làm việc × loại phiếu**: mỗi cấp có **số cấp** (1–9), **ngưỡng giá trị** và **vai trò** được duyệt; ngưỡng tăng
dần theo cấp. Phiếu phải qua mọi cấp có ngưỡng ≤ giá trị phiếu, lần lượt từ thấp lên cao.

**Người duyệt** của cấp (từ 0.22.0): *Theo vai trò*, *Trưởng phòng ban của phiếu* hoặc *GĐ khối của phiếu* (hai loại sau chỉ cho PR, PO và các
bộ cấp phụ của chúng). Người duyệt động lấy theo bộ phận trên phiếu (khai ở Bộ phận, Khối) tại thời điểm duyệt. Phiếu không gửi duyệt được
nếu bộ phận chưa khai Trưởng phòng ban / chưa gán khối / khối chưa có GĐ khối, người đó chưa có quyền duyệt, là người lập phiếu, hoặc một người
vừa là Trưởng phòng ban vừa là GĐ khối – thông báo chỉ rõ chỗ cần sửa.

| Mã | Loại | Giá trị xét ngưỡng |
| --- | --- | --- |
| PR | Đề nghị mua hàng | Tổng dự kiến |
| PO | Đơn mua hàng | Tổng thanh toán gồm VAT |
| POX | Ngoại lệ giá đơn mua hàng | Tổng thanh toán gồm VAT |
| POK | Đơn mua hàng khẩn cấp | Tổng thanh toán gồm VAT |
| PRB / POB | Vượt ngân sách PR / PO | Tổng dự kiến / tổng thanh toán |
| POL | Mua ngoài danh sách/giá đã duyệt | Tổng thanh toán gồm VAT |
| POC | Đơn mua hàng vượt giá trần | Tổng thanh toán gồm VAT |
| PRL | Bảng giá NCC | Không xét giá trị |
| RFQ | Kết quả chọn thầu | Giá trị trúng thầu |
| NCC | Hồ sơ nhà cung cấp | Không xét giá trị |
| CTR / CTA | Hợp đồng nguyên tắc / phụ lục | Giá trị dự kiến |
| NDQ | Phê duyệt NCC độc quyền | Không xét giá trị |
| GTH / GTX | Duyệt NCC và giá tháng / ngoại lệ (chỉ tổng công ty) | Không xét giá trị |
| CKB | Thỏa thuận chiết khấu sản lượng gộp (chỉ tổng công ty) | Không xét giá trị |

Quy tắc:

- Công ty **chưa cấu hình** loại nào → phiếu loại đó **một cấp**, ai có quyền duyệt cũng duyệt được. Có cấu hình mà giá trị dưới mọi ngưỡng →
  **duyệt ngay khi gửi**.
- Người duyệt một cấp phải có **quyền duyệt** loại phiếu **và** được gán **vai trò** của cấp đó tại công ty.
- Mỗi cấp một người khác nhau trong một lượt; người lập không tự duyệt. Công ty chưa gán vai trò riêng cho các chặng ngoại lệ cần ít nhất 2
  người có quyền duyệt PO.
- Nút **Cấu hình duyệt mẫu…**, ô **Bộ mẫu** chọn một trong hai bộ:
  - **Theo biểu mẫu CUVT**: PR (1B) – Thủ kho nơi nhận → Trưởng phòng ban của phiếu → TPCUVT → TPTCKT → TPHC → GĐ khối của phiếu → PHOCTUBCL
    → CHUTICH; PRA (1A – công trình) như PR nhưng không có TPHC; PRS (dịch vụ) như PR nhưng không có Thủ kho; PRE (vượt dự toán) – GDTCKT;
    RFQ (3a/3A/3B) – TPCUVT → TPTCKT → GDTCKT → PHOCTUBCL → CHUTICH; PO (4A/4B) – TPCUVT → TPTCKT → GDTCKT → CHUTICH. Khai Trưởng phòng ban,
    GĐ khối ở danh mục Bộ phận, Khối trước khi gửi duyệt. Nên áp bộ này trước rồi áp bộ theo sơ đồ để có thêm chặng ngoại lệ, hợp đồng.
  - **Theo sơ đồ phê duyệt của Ban cung ứng** (PR – Trưởng phòng cung ứng; PO – Phó ban → Phó ban → Chủ tịch;
    POL, POX, POK, NDQ – Phó ban → Chủ tịch; POC – Chủ tịch; RFQ – Phó ban → Trưởng ban; PRL – Trưởng ban; CTR, CTA – Phó ban → Chủ tịch; ở
    tổng công ty thêm GTH, CKB – Phó ban → Phó ban → Trưởng ban, GTX – Chủ tịch, hồ sơ NCC – Trưởng ban). Chỉ tạo cho loại **chưa có cấp nào**,
    mọi cấp **ngưỡng 0** – sửa ngưỡng sau theo phân quyền nội bộ.
- **Người duyệt "Thủ kho nơi nhận"** (chỉ loại PR, PRA): người có quyền nhập tồn trên đề nghị mua và phạm vi kho chứa nơi nhận của phiếu.
  Thủ kho bấm **Nhập tồn** rồi mới **Duyệt** được; đề nghị dịch vụ tự bỏ cấp này. Đề nghị có cấp Thủ kho phải chọn nơi nhận mới gửi duyệt được.
- Đề nghị mua có **phần vượt** (vượt ngân sách PRB, vượt dự toán công trình PRE): chặng vượt được duyệt **trước cấp cao nhất** (Chủ tịch) của
  chuỗi thường – cấp cao nhất duyệt sau cùng.
- Công ty đã áp bộ PR cũ thì bộ mẫu không ghi đè: xóa các cấp PR cũ rồi áp lại nếu muốn chuỗi mới.
- Quyền sửa: `System.Parameter.Manage`.

Tài liệu chi tiết cách cấu hình duyệt PO nhiều cấp: `docs/HuongDan_PheDuyetPO_NhieuCap_v2.docx`, `docs/HuongDan_DuyetPO_A_B_C_D.docx`.

## 8.5 Tham số hệ thống (SEC)

Theo **công ty đang làm việc**:

| Nhóm | Tham số | Mặc định |
| --- | --- | --- |
| Đấu thầu | Số NCC tối thiểu khi gửi RFQ (1–20) | 3 |
| Đấu thầu | Bắt buộc đủ hồ sơ trình khi chọn thầu (`RFQ_DOSSIER_REQUIRED`) | Tắt |
| Mua bán nội bộ | % markup nội bộ (gợi ý giá bán nội bộ = giá vốn × (1 + %)) | 0% |
| Nhận hàng | Bắt buộc kiểm tra chất lượng khi nhận hàng | Tắt |
| Nhận hàng | Người lập PO không ghi sổ nhận hàng của PO đó | Tắt |
| Nhận hàng | Nhập mua bắt buộc theo đơn mua hàng (`RECEIPT_PO_REQUIRED`) | Tắt |
| Mua hàng | % cảnh báo giá cao trên PO (`PO_HIGH_PRICE_WARN_PERCENT`) | 10% |
| Mua hàng | % biên độ phân bổ đặt hàng gộp (`POOLED_MARGIN_PERCENT`) | 10% |
| Mua hàng | SLA (ngày làm việc): hàng đã có hợp đồng/bảng giá · hàng chưa có NCC · hàng khẩn | 1 · 5 · 1 |
| NCC (tổng công ty) | Cho duyệt NCC mới không qua hồ sơ HSN (`SUPPLIER_ONBOARDING_OPTIONAL`) | Tắt |
| KPI | Mục tiêu 9 chỉ số KPI (tiết kiệm, mua theo giá đã duyệt, SLA, giao đúng hạn, chất lượng, mua khẩn, chậm luân chuyển, đánh giá NCC, vi phạm) và số ngày tính chậm luân chuyển | Theo danh mục KPI |

Màn hình cũng hiện phương pháp giá vốn (bình quân gia quyền cuối kỳ tháng) và các kho cho phép tồn âm.

## 8.6 Đánh số chứng từ (DSCT)

Mẫu số theo **công ty × loại chứng từ**. Mặc định `{LOẠI}-{CTY}-{YYMM}-{0000}` (ví dụ PO-BHA-2611-0001). Ký hiệu: `{CTY}` mã công ty, `{YYYY}`
/`{YY}` năm, `{MM}` tháng, `{YYMM}`, `{0000}` số thứ tự (số chữ số = số chữ 0). Số đếm lại theo tháng/năm của ngày chứng từ. Màn hình hiện số
mẫu minh họa.

## 8.7 Đánh số danh mục (DSDM)

Dải mã tự cấp cho **NCC, nhóm NCC, nhóm hàng, hàng hóa** (dùng chung toàn tập đoàn, sửa ở tổng công ty):

- Dải = **tiền tố** + **số chữ số** + **từ số – đến số**. Ví dụ NCC, 6 chữ số → NCC000001.
- Hàng hóa: dải riêng từng nhóm hàng, hoặc dòng mặc định với tiền tố `{NHOM}-` (thay bằng mã nhóm, ví dụ VT-00001).
- Mã kế tiếp = số lớn nhất đang có trong dải + 1; hết dải thì mở rộng dải.
- Chưa có dải thì không thêm mới được. Dải mặc định: NCC + 6 số, GN + 4 số, NH + 6 số, `{NHOM}-` + 5 số – **sửa cho đúng quy ước mã của công
  ty trước khi nhập liệu**.

## 8.8 Xếp loại NCC (RATE)

Hạng xếp loại NCC (mã, thứ tự, **% giao đúng hạn tối thiểu**, **% trả hàng tối đa**) – dùng cho báo cáo Đánh giá NCC (SUPEVAL). Cấu hình ở
tổng công ty.

## 8.9 Nhật ký

| Màn hình | Nội dung |
| --- | --- |
| **Nhật ký thao tác (LOG)** | Mọi thay đổi dữ liệu (thêm/sửa/xóa) kèm người, công ty, thời điểm, IP, trình duyệt, **giá trị cũ → mới** từng trường. Lọc khoảng ngày, loại đối tượng, hành động. Mật khẩu chỉ ghi `***`. Nhật ký **chỉ thêm**, không ai sửa/xóa được |
| **Nhật ký truy cập (ACC)** | Đăng nhập, đăng nhập sai, bị khóa tạm, đăng xuất, đổi mật khẩu, đổi công ty làm việc |

Xem lịch sử một ô cụ thể: đặt con trỏ vào ô và bấm **F1** (01 – mục 1.6).

## 8.10 Việc cần làm của quản trị khi bắt đầu dùng

1. Đăng nhập tài khoản quản trị ban đầu, **đổi mật khẩu**.
2. **COMP**: kiểm tra tổng công ty, thêm các công ty thành viên (mỗi công ty tự có kho tổng).
3. **WH**: thêm kho phụ; **DEPT**: khai bộ phận từng công ty.
4. **DSDM**: sửa dải mã danh mục; **DSCT**: kiểm tra mẫu số chứng từ.
5. **ROLE**: tạo **Bộ vai trò mẫu…** hoặc vai trò riêng; **USER**: tạo tài khoản, gán vai trò/kho/nhóm hàng tại từng công ty.
6. **APRL**: cấu hình quy trình duyệt từng công ty (có thể dùng cấu hình mẫu rồi sửa ngưỡng).
7. **SEC**: rà các tham số.
8. **PER**: tạo lịch kỳ năm và **mở kỳ tháng** đầu tiên cho từng công ty trước khi ghi sổ chứng từ kho.
9. Danh mục: ĐVT, nhóm hàng, hàng hóa, nhóm NCC, NCC (nhập Excel cho nhanh); mở rộng NCC cho các công ty; bảng giá nội bộ (nếu có mua bán nội bộ).
10. **RATE**: hạng xếp loại NCC.
