# 4. Giá và nhà cung cấp

Danh mục NCC, nhóm NCC, hàng hóa, nhóm hàng, ĐVT, hồ sơ NCC mới và duyệt NCC: xem [02 – Danh mục](02-danh-muc.md).

## 4.1 Bảng giá NCC (PRL) – Giá và NCC → Bảng giá NCC

- Thuộc **công ty lập**. Lập ở **tổng công ty** thì đánh dấu được **"áp dụng toàn tập đoàn"** – mọi công ty xem được (chỉ xem).
- **Vòng đời**: Nháp → Chờ duyệt → **Hiệu lực** → Ngừng. Mọi bảng giá phải duyệt (loại **PRL** ở APRL) mới hiệu lực; người lập không tự
  duyệt; từ chối bắt buộc lý do và bảng về Nháp. Bảng đã gửi/hiệu lực không sửa – muốn đổi giá thì **ngừng** và lập bảng mới.
- Đầu phiếu: NCC, tiêu đề, toàn tập đoàn, hiệu lực từ – đến, điều kiện giao hàng, ghi chú.
- Dòng: mặt hàng, ĐVT, **bậc số lượng** (áp khi SL đặt ≥ bậc; 0 = mọi SL), đơn giá, % chiết khấu, VAT (trống = theo mặt hàng).
- Danh sách và phiếu cảnh báo bảng **sắp hết hiệu lực** (còn ≤ 30 ngày).
- **Nhập Excel**: mỗi dòng file là một bậc giá; các dòng cùng **Mã NCC + Tên bảng giá** là một bảng; nhập vào trạng thái Nháp rồi gửi duyệt.
- PO dùng bảng giá làm **giá đã duyệt** (bảng của công ty ưu tiên hơn bảng toàn tập đoàn, rồi bảng hiệu lực mới nhất; chọn bậc lớn nhất ≤ SL đặt).
- Quyền `Pricing.PriceList.View/Manage/Approve`.

## 4.2 Bảng giá nội bộ (IPR) – Giá và NCC → Bảng giá nội bộ

Giá bán giữa các công ty trong tập đoàn, thuộc **công ty bán**.

- Dòng: mặt hàng tồn kho, **công ty mua** (trống = mọi công ty), giá chưa VAT theo **ĐVT cơ sở**, thuế suất, hiệu lực từ – đến.
- Cùng mặt hàng, cùng công ty mua không chồng hiệu lực; giá riêng cho một công ty mua được ưu tiên hơn giá chung.
- **Gợi ý giá** = giá vốn kỳ đã đóng gần nhất × (1 + % markup nội bộ) – % markup là tham số công ty (SEC).
- Công ty mua không thấy bảng giá nội bộ của công ty bán.
- Khai báo bảng giá nội bộ **trước** khi lập phiếu xuất bán nội bộ (ICS).

## 4.3 So sánh giá (PCMP) – Giá và NCC → So sánh giá

Chọn mặt hàng để xem:

- Mọi giá quy về **ĐVT cơ sở, trước VAT, VND**.
- Mỗi **công ty × NCC** một dòng: giá gần nhất (phiếu nhập hoặc PO), thấp nhất / cao nhất / bình quân 12 tháng (trên phiếu nhập).
- Tô màu dòng có giá cao hơn giá thấp nhất quá X% (mặc định 10%, đổi trên màn hình).
- Giá bảng giá đang hiệu lực; lịch sử giá 12 tháng (bảng giá, PO, phiếu nhập, báo giá trúng thầu).
- Xem toàn tập đoàn cần quyền `Report.Price.ViewAllCompanies`.

## 4.4 Duyệt NCC và giá tháng (GTH) – Giá và NCC → Duyệt NCC và giá tháng

Kết quả hằng tháng của Ban cung ứng: mỗi mặt hàng trong tháng mua của NCC nào, giá nào. Phiếu thuộc **tổng công ty**.

1. **Tạo mới**: **tháng áp dụng** (tháng hiện tại trở đi), **nhóm hàng**, **công ty áp dụng** (một hoặc nhiều, có thể gồm tổng công ty),
   ghi chú; đính kèm biên bản họp Ban.
2. Dòng = một **mặt hàng**: ĐVT, **NCC chính + giá**, **NCC dự phòng + giá** (có NCC dự phòng thì bắt buộc giá), **hạn mức SL tháng**
   (gộp các công ty; trống = không giới hạn), **giá chuẩn**, **giá trần**, **giá mục tiêu**, ghi chú. Giá VND, trước VAT, đã trừ chiết khấu.
   - NCC chính, dự phòng phải **Đã duyệt**.
   - Có giá trần thì các giá khác không vượt trần.
   - Cột **Giá thị trường** (chỉ xem): giá thấp nhất của phiếu khảo sát đã chốt gần nhất trong 180 ngày; rê chuột xem từng nguồn.
3. Mỗi mặt hàng × tháng × công ty chỉ thuộc một phiếu còn hiệu lực.
4. **Gửi duyệt** theo loại **GTH** của tổng công ty. Phiếu **có ngoại lệ** – dưới 03 NCC đủ điều kiện, NCC độc quyền, giá vượt giá mục
   tiêu – phải ghi **giải trình** mới gửi được và đi thêm chặng **GTX** (Chủ tịch/TGĐ).
5. Đã duyệt không sửa. **Điều chỉnh** lập phiếu nháp mới (duyệt xong phiếu cũ thành **Đã thay**); **Hủy** bắt buộc lý do; **Chép sang tháng
   sau** để lập phiếu tháng kế tiếp nhanh.
6. Công ty áp dụng **chỉ xem** phiếu Đã duyệt áp cho mình.

Tác dụng lên PO: xem [03 – mục 3.4](03-mua-hang.md#34-đơn-mua-hàng-po--mua-hàng--đơn-mua-hàng) (nguồn giá "Duyệt tháng", chặng POL, POC,
chặn vượt hạn mức). Quyền `Pricing.MonthlyApproval.View/Manage/Approve`.

## 4.5 Khảo sát giá thị trường (KSG) – ngân hàng giá

Văn phòng Ban (ở tổng công ty) ghi giá khảo sát để Ban tham khảo khi duyệt giá tháng.

- Đầu phiếu: **ngày khảo sát** (không sau hôm nay), nhóm hàng, ghi chú; đính kèm báo giá, ảnh bảng giá.
- Dòng = một giá của **một nguồn**: mặt hàng, ĐVT, **NCC trong danh mục** (mọi trạng thái) **hoặc tên nguồn ngoài**, đơn giá trước VAT.
  Một mặt hàng nhiều nguồn được, không trùng nguồn.
- **Nháp → Đã chốt** (không qua duyệt). Đã chốt không sửa; sai thì **Hủy** (bắt buộc lý do) và lập phiếu mới.
- Quyền `Pricing.PriceBank.View/Manage`.

## 4.6 Chiết khấu sản lượng gộp (CKB) và Theo dõi bậc (CKBT)

Thỏa thuận chiết khấu theo bậc với NCC, tính trên **sản lượng gộp các công ty**. Thuộc **tổng công ty**.

### Lập thỏa thuận

- Đầu: tên, **NCC** (Đã duyệt), nhóm hàng, hiệu lực từ – đến (tối đa 5 năm), **kỳ tính bậc** (Tháng / Quý / Năm / Cả thỏa thuận),
  **cơ sở tính** (Số lượng gộp / Doanh số gộp), **bậc mục tiêu**, **công ty áp dụng**; đính kèm văn bản thỏa thuận.
- Dòng: mặt hàng tính sản lượng + ĐVT (tính theo số lượng thì mọi dòng **cùng ĐVT**).
- **Bậc** (1–10): ngưỡng và % chiết khấu, cùng **tăng dần**.
- **Bậc toàn phần**: đạt bậc nào thì cả kỳ hưởng % của bậc đó. Ví dụ bậc 100 cái 1%, 200 cái 2%; mua 220 cái, doanh số 2.200.000 → chiết
  khấu 44.000.
- Nháp → Chờ duyệt → **Hiệu lực** (loại **CKB**). Hiệu lực không sửa – đổi bậc/gia hạn thì **Chép thành thỏa thuận mới**; **Hủy** bắt buộc lý do.

### Tab Lũy kế

Cộng sản lượng/doanh số các **PO đã duyệt** của các công ty áp dụng với NCC × mặt hàng trong kỳ; **dự báo cuối kỳ**; trạng thái so bậc mục
tiêu: Đã đạt / Dự báo đạt / **Nguy cơ hụt** / **Hụt bậc**. Chiết khấu chia cho từng công ty theo tỷ lệ doanh số. Công ty thành viên chỉ thấy
phần của mình.

### Tab Thực nhận

Sau khi **kỳ kết thúc**, tổng công ty **ghi nhận** khoản chiết khấu đã nhận: ngày nhận, số tiền, hình thức (giảm trừ công nợ, chuyển khoản,
hàng, khác), số chứng từ đối chiếu, đính kèm. Số tiền tự chia cho các công ty theo doanh số kỳ lúc ghi. Mỗi kỳ hiện **phải nhận – đã nhận –
còn thiếu**, hạn ghi nhận = cuối kỳ + 30 ngày. Ghi sai thì **Hủy** (bắt buộc lý do), không xóa.

### CKBT – Theo dõi bậc chiết khấu

Kỳ hiện tại của mọi thỏa thuận hiệu lực: lũy kế, bậc đã đạt, cảnh báo **sắp chạm bậc** (trong X% – mặc định 10%), nguy cơ hụt, **thiếu thực
nhận quá hạn**. Lọc "Chỉ dòng có cảnh báo".

Quyền `Pricing.VolumeRebate.View/Manage/Approve`.

## 4.7 NCC đủ điều kiện (SUPCOV)

Đếm số NCC **Đã duyệt** cung cấp từng **nhóm hàng** (theo ngành hàng NCC, gồm nhóm cha, hoặc NCC – sản phẩm), so với **tối thiểu 3**
(đổi được trên màn hình, 1–20). Nhóm ít hơn mức tối thiểu là **Thiếu**, trừ khi mọi mặt hàng của nhóm có phiếu **NCC độc quyền** còn tác
dụng (**Miễn**). Mặc định chỉ hiện nhóm thiếu, thiếu nhiều lên đầu.

## 4.8 Đánh giá NCC định kỳ (SUPREV)

Mỗi công ty đánh giá NCC của mình theo **quý**.

1. Chọn năm, quý (mặc định quý trước). Lưới hiện NCC **thuộc diện** (có phiếu nhập mua đã ghi sổ trong quý) và NCC đã có phiếu; dòng tóm
   tắt "Đã hoàn thành x/y NCC thuộc diện".
2. Bấm đúp NCC để mở/lập phiếu (số liệu hệ thống điền sẵn: giá trị nhập, % đúng hạn, % trả hàng, xếp loại, % kiểm tra đạt).
3. Chấm **6 tiêu chí** thang 1–5: giá/tổng chi phí, chất lượng, giao hàng, bảo hành/hỗ trợ, công nợ/thanh toán, năng lực tài chính/tuân thủ.
   Điểm = bình quân 6 tiêu chí.
4. Kết luận: **Tiếp tục hợp tác / Theo dõi, yêu cầu cải thiện / Đề xuất ngừng hợp tác**. Kết luận khác "Tiếp tục" hoặc có tiêu chí ≤ 2
   điểm thì bắt buộc nhận xét.
5. **Hoàn thành** (khóa phiếu, chốt số liệu hệ thống). Phiếu nháp hủy được (bắt buộc lý do).

Quyền `MasterData.SupplierReview.View/Manage`.

## 4.9 NCC độc quyền (NDQ)

Giải trình và duyệt riêng khi chỉ có **một NCC** cung cấp được một mặt hàng.

- Phiếu = **một NCC × một mặt hàng** của công ty lập: giải trình (bắt buộc), hiệu lực từ ngày, **hạn đánh giá lại** (tối đa 12 tháng);
  đính kèm văn bản xác nhận độc quyền.
- Nháp → Chờ duyệt → **Đã duyệt** (loại **NDQ**). Danh sách nhắc phiếu quá hạn hoặc còn ≤ 30 ngày tới hạn.
- **Đánh giá lại**: lập phiếu mới nối tiếp. **Kết thúc sớm** khi hết độc quyền (bắt buộc lý do).
- Tác dụng: RFQ mời ít hơn số NCC tối thiểu mà mọi dòng có độc quyền đã duyệt thì tự ghi giải trình; PO ghi số phiếu độc quyền; SUPCOV
  tính "Miễn"; phiếu GTH có NCC độc quyền là ngoại lệ.
- Quyền `MasterData.SoleSource.View/Manage/Approve`.

## 4.10 Kê khai xung đột lợi ích (COI)

- **Ai cũng tự kê khai** (không cần quyền) quan hệ của mình với NCC: loại quan hệ (người thân sở hữu/làm việc tại NCC, góp vốn, đang/từng
  làm cho NCC, khác), mô tả, hiệu lực từ – đến; đính kèm được.
- Kê khai gắn với **con người**, chặn ở mọi công ty. Không xóa, không sửa nội dung; người kê khai **không tự kết thúc** – người có quyền
  `MasterData.Conflict.Manage` kết thúc kèm lý do.
- Tác dụng khi còn hiệu lực: không duyệt/từ chối PO, bảng giá, yêu cầu duyệt NCC, hợp đồng, phụ lục, phiếu GTH, CKB, kết quả chọn thầu
  liên quan NCC đó; không nhập báo giá, chọn thầu RFQ có mời NCC đó; không nhận thông báo chờ duyệt. Nếu cả cấp duyệt đều vướng thì quản trị
  gán thêm người duyệt.
- `MasterData.Conflict.View` (KSNB, Trưởng ban) xem mọi kê khai.
