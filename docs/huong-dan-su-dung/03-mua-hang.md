# 3. Mua hàng

Chứng từ mua hàng (kế hoạch nhu cầu, PR, PO, hợp đồng, đặt hàng gộp) **không làm thay đổi tồn kho**. Tồn kho chỉ đổi khi ghi sổ phiếu kho
(06 – Kho).

## 3.1 Kế hoạch nhu cầu (KHN) – Mua hàng → Kế hoạch nhu cầu

Mỗi công ty tự khai nhu cầu năm theo nhóm hàng; là căn cứ để Ban cung ứng thương lượng sản lượng gộp, phân bổ đặt hàng gộp và tính định
mức trên PR.

1. **Tạo mới**: chọn **năm kế hoạch** (năm nay đến 2 năm tới), **nhóm hàng**, ghi chú; đính kèm định mức, đề xuất của kho/bộ phận.
2. Lưới dòng: mỗi dòng một **mặt hàng** (thuộc nhóm hàng của phiếu hoặc nhóm con, không trùng), ĐVT, **số lượng tháng 1–12**. Tổng quý và
   cả năm tự tính.
3. **Chốt** khi số liệu đúng (không qua duyệt). Phiếu Đã chốt không sửa.
4. Cần sửa: **Điều chỉnh** → hệ thống lập phiếu nháp mới chép nội dung; chốt phiếu điều chỉnh thì phiếu cũ thành **Đã thay**.
5. **Hủy** (nháp hoặc đã chốt) bắt buộc lý do.

Mỗi mặt hàng × năm × công ty chỉ thuộc một phiếu còn hiệu lực. Quyền `Purchase.DemandPlan.View/Manage`.

## 3.2 Tổng hợp nhu cầu (KHNTH) – Mua hàng → Tổng hợp nhu cầu

- Cộng các phiếu KHN **Đã chốt** của năm, quy về **ĐVT cơ sở** (ví dụ 2,5 thùng × 12 = 30 cái).
- Lưới: mã hàng, tên, ĐVT cơ sở, nhóm hàng, **Tổng**, số công ty, **một cột mỗi công ty**. Ô **Kỳ** chọn Cả năm / Quý / Tháng.
- Bấm đúp dòng: chi tiết 12 tháng, quý, cả năm × từng công ty, kèm số phiếu.
- Đầu báo cáo cảnh báo công ty **còn phiếu nháp chưa chốt** hoặc chưa có kế hoạch.
- Ô **Toàn tập đoàn** cần quyền `Purchase.DemandPlan.ViewAllCompanies`; không có quyền thì chỉ thấy công ty đang làm việc.

## 3.3 Đề nghị mua hàng (PR) – Mua hàng → Đề nghị mua hàng

**Vòng đời**: Nháp → Chờ duyệt → Đã duyệt → (lên PO) · Bị từ chối → Nháp · Đã hủy · Đã đóng phần còn lại.

### Lập PR

1. **Tạo mới**. Đầu phiếu: ngày, **bộ phận** (bắt buộc, chọn từ danh mục), mục đích, ngày cần, kho nhận, **mức ưu tiên** (Thường/Khẩn).
2. Tab dòng hàng: mặt hàng, ĐVT, số lượng, ngày cần, NCC đề xuất (nếu có – dòng tự lấy **ĐVT mua** và **mã hàng NCC** của NCC đó).
   - **Đơn giá tham khảo** do hệ thống lấy = giá nhập gần nhất của công ty (chưa từng nhập thì 0). Cột tồn hiện tại chỉ để xem.
   - Cột **Đang đặt**: số lượng đã lên PO.
   - Cột **Định mức LK**: lũy kế từ kế hoạch nhu cầu đã chốt; vượt định mức thì đầu phiếu **cảnh báo** (không chặn).
3. Tab **Hàng mới chưa có mã**: hàng chưa có trong danh mục – ghi mô tả, quy cách, số lượng, ĐVT. Được duyệt cùng PR; sau khi duyệt, phòng
   mua bấm **Tìm nguồn** (lập phiếu tìm nguồn) hoặc **Gắn mã** (gắn vào mặt hàng vừa tạo) rồi mới lên PO/RFQ.
4. Lưu → **Gửi duyệt**.

### Ngân sách

Nếu công ty khai **ngân sách bộ phận** (3.7): đầu phiếu hiện ngân sách / đã dùng / còn lại của tháng; PR vượt ngân sách đi thêm chặng
duyệt **"Vượt ngân sách" (PRB)**. Công ty không khai ngân sách thì không ảnh hưởng.

### Sau khi duyệt

- PR đã duyệt phải vào **bảng tổng hợp đề xuất mua đã duyệt** (3.3a) – mọi công ty – rồi mới lấy dòng lên PO (tổng SL đặt trên các PO
  chưa hủy không vượt SL đề nghị) hoặc lên RFQ.
- **Đóng phần còn lại** (bắt buộc lý do): PR đặt một phần không còn chờ đặt nữa; ngân sách chỉ tính phần đã đặt.
- **Hủy**: PR đã lên PO (chưa hủy) không hủy được; PR đang ở bảng tổng hợp chưa hủy thì hủy bảng trước (hoặc hủy dây chuyền); hủy PR đã
  duyệt bắt buộc lý do.
- **In PR** (PDF) có cả hàng mới chưa có mã.

## 3.3a Bảng tổng hợp đề xuất mua (BTH) – Mua hàng → Bảng tổng hợp đề xuất

Phòng CUVT gom các dòng đề nghị mua **đã duyệt còn cần mua** của một bộ phận thành bảng tổng hợp theo biểu mẫu, gán **đơn vị mua** từng
dòng (danh mục Mua hàng → Đơn vị mua) rồi trình duyệt. Dòng đề nghị chỉ lên RFQ / PO khi thuộc bảng **đã duyệt**.

**Vòng đời**: Nháp → Chờ duyệt → Đã duyệt · Bị từ chối → Nháp · Rút về nháp · Đã hủy.

1. **Tạo mới**. Đầu phiếu: ngày lập, **biểu mẫu** – **2A Công trình** (đề nghị loại A của **một công trình**, phải chọn công trình đang
   thực hiện) hoặc **2B Hàng hóa, dịch vụ** (đề nghị loại B, DV) –, **bộ phận đề xuất** (bắt buộc), ghi chú. Đổi biểu mẫu / bộ phận /
   công trình thì các dòng đã chọn bị bỏ, chọn lại.
2. Bấm **Chọn dòng đề nghị** (hoặc Thêm dòng / F2): hộp chọn liệt kê dòng đề nghị đã duyệt còn cần mua của bộ phận (và công trình), chưa
   nằm trong bảng tổng hợp nào chưa hủy. Chọn sẵn **Đơn vị mua cho các dòng chọn** (hoặc gán sau trên từng dòng), tích dòng → **Thêm**.
3. Mỗi dòng bắt buộc **Đơn vị mua**; SL đề xuất mua, đơn giá tham khảo, thành tiền do hệ thống chụp lúc lưu và chụp lại lúc gửi duyệt.
   Bấm đúp số đề nghị để mở phiếu gốc.
4. Lưu → **Gửi duyệt** (xem trước chuỗi duyệt). Đầu phiếu hiện khung chi phí: 2A – dự toán, lũy kế đã mua, tổng đề xuất, còn lại;
   2B – ngân sách tháng của bộ phận, lũy kế đã dùng, còn lại. Vượt thì cảnh báo vàng, không chặn.
5. Người duyệt duyệt trên form, ở **Việc cần làm**, hoặc trên điện thoại (link trong email mở trang duyệt gọn).
6. **Hủy phiếu** dùng hộp hủy dây chuyền: bảng → các PO lấy dòng của bảng → XNH (hủy từ phiếu cuối về bảng). RFQ chưa hủy có dòng của
   bảng thì phải hủy RFQ trước. Hủy xong các dòng đề nghị trở lại chọn được ở bảng khác.

7. **In phiếu** (Ctrl+P): in theo biểu mẫu **2A / 2B** khổ ngang – mỗi đơn vị mua một cột, dòng thuộc đơn vị nào đánh "x"; khối ký in
   người lập và từng cấp duyệt (họ tên, giờ ngày đã duyệt), vẫn chừa chỗ ký tay.

**In đề nghị mua** (nút In phiếu trên form PR): loại A in mẫu **1A**, loại B mẫu **1B**, dịch vụ mẫu 1B không có cột tồn kho. Dòng
"Lũy kế số tiền đã mua" của 1A là PO đã duyệt của công trình; của 1B là **PO đã duyệt trong tháng** của bộ phận (chưa VAT).

## 3.4 Đơn mua hàng (PO) – Mua hàng → Đơn mua hàng

**Vòng đời**: Nháp → Chờ duyệt → Đã duyệt → Đã nhận đủ · Đã đóng phần còn lại · Đã hủy.

### Lập PO

1. **Tạo mới** hoặc **lấy dòng từ PR đã duyệt** / tạo từ kết quả chọn thầu RFQ / sinh từ đặt hàng gộp.
2. Đầu phiếu: ngày đặt, **NCC** (chỉ NCC **Đã duyệt** và **đang giao dịch tại công ty**), kho nhận, **bộ phận đặt hàng** (tự lấy theo PR
   nguồn), ngày giao dự kiến, điều khoản thanh toán, giao hàng, địa chỉ giao, **dung sai nhận hàng %** (mặc định 0, tối đa 20%), **loại mua**
   (Thường / Khẩn cấp), ghi chú. Chỉ VND.
3. Dòng: mặt hàng (tự lấy ĐVT mua và mã hàng NCC), SL, đơn giá, chiết khấu %, VAT. Hệ thống tính thành tiền, VAT, tổng thanh toán.

### Giá đã duyệt và nguồn giá

Mỗi dòng PO có cột **Giá đã duyệt** và **Nguồn giá**, theo thứ tự ưu tiên:

1. **Duyệt tháng** – phiếu GTH đã duyệt của tháng đặt hàng, áp cho công ty (NCC chính → giá chính, NCC dự phòng → giá dự phòng).
2. **Kết quả chọn thầu** RFQ đã duyệt.
3. **Hợp đồng nguyên tắc** đang hiệu lực (nếu hợp đồng giới hạn bộ phận được đặt thì PO bộ phận khác không tính giá hợp đồng).
4. **Bảng giá NCC** đang hiệu lực.
5. **Ngoài hợp đồng** – không có giá đã duyệt.

Bấm **Áp giá đã duyệt** để chép giá vào dòng. Cột **Giá thấp nhất TĐ**: giá thấp nhất tập đoàn của mặt hàng; tam giác vàng khi đơn giá vượt
quá ngưỡng `PO_HIGH_PRICE_WARN_PERCENT` (mặc định 10%).

### Chặng duyệt thêm (ngoại lệ)

Khi gửi duyệt, PO đi các cấp duyệt thường (loại **PO**) rồi thêm các chặng sau nếu có ngoại lệ:

| Chặng | Loại ở APRL | Khi nào | Người lập phải làm |
| --- | --- | --- | --- |
| Ngoại lệ giá | **POX** | Đơn giá **lệch giá đã duyệt** (tam giác vàng) | Ghi **lý do lệch giá** từng dòng |
| Vượt ngân sách | **POB** | PO vượt ngân sách bộ phận tháng | – |
| Ngoài danh sách / giá đã duyệt | **POL** | NCC không phải NCC chính/dự phòng trên phiếu GTH, hoặc dòng không có giá đã duyệt | – |
| Vượt giá trần | **POC** | Giá ròng (sau chiết khấu, trước VAT) vượt **giá trần** trên phiếu GTH – lên Chủ tịch/TGĐ | – |
| PO khẩn | **POK** | Loại mua Khẩn cấp (thay bộ cấp PO thường nếu đã cấu hình) | Ghi lý do khẩn, hạn bổ sung hồ sơ |

**Chặn gửi duyệt** khi PO làm **vượt hạn mức số lượng tháng** trên phiếu GTH (tính gộp các công ty áp dụng) – báo dòng, số phiếu, hạn mức,
đã đặt. Khi đó cần Ban điều chỉnh phiếu GTH.

Dòng có phiếu **NCC độc quyền** còn tác dụng vào ngày PO được ghi số phiếu độc quyền.

### Mua khẩn cấp

Chọn loại mua **Khẩn cấp**, ghi lý do và **hạn bổ sung hồ sơ** (≤ 30 ngày). Không gửi duyệt PO khẩn lập lùi ngày. Bổ sung đủ hồ sơ thì
bấm **Đã bổ sung hồ sơ**; quá hạn thì PO bị gắn cờ quá hạn.

### Sau khi duyệt

- **In phiếu** (Ctrl+P) in **bảng theo dõi mua hàng 4A** (PO có dòng từ đề nghị loại A – công trình) hoặc **4B** (hàng hóa, dịch vụ), khổ
  ngang: giá cũ gần nhất, giá mới ghi cột **tăng** hoặc **bằng–giảm**, lũy kế (đã mua trước đây + đơn này), còn lại theo dự toán / định mức;
  khối ký là các bước duyệt PO. Ô **Loại mua – biểu mẫu** trên PO cho biết đơn in mẫu nào. PO không gộp dòng đề nghị loại A với dòng đề nghị
  B/DV, và dòng loại A phải cùng một công trình.
- **In đơn gửi NCC** (nút trên PO đã lưu): đơn đặt hàng PDF có mã hàng NCC, giá, chiết khấu, thuế, điều khoản giao – gửi NCC.
- Thủ kho lập **phiếu nhập mua theo PO** (06 – Kho). PO tự chuyển **Đã nhận đủ** khi nhận đủ mọi dòng; bỏ ghi sổ phiếu nhập làm thiếu lại
  thì PO về Đã duyệt. Trả NCC trừ vào số đã nhận – PO đã nhận đủ mà trả lại thì mở lại để NCC giao bù.
- **Đóng phần còn lại** (bắt buộc lý do): PO nhận thiếu thôi chờ hàng; ngân sách chỉ tính phần đã nhận.
- **Hủy**: PO đã có phiếu nhập (chưa hủy) không hủy được; hủy PO đã duyệt bắt buộc lý do.

### Mua nội bộ (từ bản 0.24.0 – thay phiếu xuất bán / nhập mua nội bộ ICS, ICR)

Mua hàng của công ty khác trong tập đoàn = lập **PO** cho **NCC nội bộ** của công ty đó (NCC có ô "Công ty thành viên" – 02 Danh mục).

- **Bên mua (lập PO)**: chọn NCC nội bộ; chọn mặt hàng thì đơn giá tự điền **giá bán nội bộ** của công ty bán (cột Nguồn giá ghi "Giá nội bộ
  … (gợi ý)"). Giá này **chỉ là gợi ý – gõ giá khác được**, không phải nhập lý do lệch giá, không thêm chặng duyệt ngoại lệ. Không đặt mua NCC
  nội bộ của chính công ty mình. PO duyệt như PO thường; nhận hàng bằng **XNH** như thường.
- Ô **Bên bán (mua nội bộ)** trên PO: công ty bán và trạng thái – *Chưa gửi* (PO chưa duyệt), *Chờ xác nhận*, *Đã xác nhận*, *Từ chối* (kèm
  người, giờ, ghi chú / lý do). Bị từ chối thì hủy PO (quản trị) hoặc **Đóng phần còn lại**.
- **Bên bán – Mua hàng → Đơn bán nội bộ (DBN)**: PO đã duyệt của công ty khác đặt cho công ty mình (người có quyền nhận thông báo khi PO duyệt
  xong). Bấm đúp mở đơn xem đầu đơn, dòng hàng, SL bên mua đã nhận; nhập ghi chú rồi **Xác nhận đơn**, hoặc nhập lý do rồi **Từ chối**. Mỗi đơn
  quyết định **một lần**, không đổi; người lập PO nhận thông báo. PO đã hủy / đã đóng chỉ xem.

## 3.5 Theo dõi giao hàng (POTRK)

Dòng PO đã duyệt còn chưa nhận đủ: số đặt, đã nhận, còn lại, hạn giao (dòng hoặc đầu phiếu), **quá hạn**. Bấm đúp mở PO.

## 3.6 Đặt hàng gộp (DHG) – Mua hàng → Đặt hàng gộp

Phòng cung ứng chung (ở **tổng công ty**) đặt hàng cho nhiều công ty theo NCC và giá Ban đã duyệt trong tháng; **mỗi công ty vẫn một PO**.

1. **Tạo mới**: ngày đặt, **phiếu GTH đã duyệt** của tháng, **NCC** (NCC chính hoặc dự phòng trên phiếu GTH), ngày giao dự kiến.
2. **Công ty nhận hàng** (thuộc các công ty áp dụng phiếu GTH): mỗi công ty một kho nhận (mặc định kho tổng) và bộ phận.
3. Dòng = dòng phiếu GTH; **đơn giá = giá Ban duyệt**, không sửa. Nhập SL phân bổ từng công ty.
4. **Phân bổ theo kế hoạch**: chia tổng theo tỷ lệ kế hoạch nhu cầu tháng đã chốt của từng công ty (ví dụ tổng 10, KH A 2, B 1 → A 7, B 3).
5. **Biên độ**: mỗi công ty được lệch đề xuất tối đa max(tổng × `POOLED_MARGIN_PERCENT` %, 1 ĐVT) (mặc định 10%). Mặt hàng không có kế
   hoạch ở công ty nào thì phân bổ tự do nhưng phải ghi **lý do phân bổ**.
6. **Tạo PO**: sinh mỗi công ty có SL **một PO nháp** tại chính công ty đó, ghi chú "Đặt hàng gộp theo phiếu DHG-…". Một công ty lỗi thì
   không PO nào được tạo. Sau đó mỗi PO đi duyệt theo quy trình của công ty nó.

Quyền: `Purchase.PooledOrder.View/Manage`; tạo PO cho công ty thành viên cần **quyền đặc biệt** `Purchase.PooledOrder.CreateMemberPo` ở vai
trò tại tổng công ty (không cần vai trò ở công ty thành viên). Phiếu đã tạo PO không sửa, không hủy – PO sai thì hủy PO ở công ty của nó.

## 3.7 Ngân sách bộ phận (BUDG) – Mua hàng → Ngân sách bộ phận

- Khai ngân sách **bộ phận × tháng** của năm.
- **Đã dùng** = giá trị PO + phần PR chưa lên PO. Màn hình hiện ngân sách, đã dùng, còn lại.
- PR/PO vượt ngân sách đi thêm chặng **PRB/POB** (cấu hình ở APRL).
- Quyền `Purchase.Budget.View/Manage`. Công ty không khai ngân sách thì không ảnh hưởng gì.

> Nghiệp vụ ngân sách chưa chốt điều kiện chặn (design.md mục 18.24) – hiện chỉ thêm chặng duyệt.

## 3.8 Hợp đồng nguyên tắc (CTR) – Mua hàng → Hợp đồng nguyên tắc

Mỗi công ty một hợp đồng với NCC; dùng chung bảng giá.

**Vòng đời**: Nháp → Chờ duyệt → **Hiệu lực** → Chấm dứt. Danh sách cảnh báo hợp đồng **sắp hết hiệu lực**.

| Tab | Nội dung |
| --- | --- |
| Đầu phiếu | NCC, **bảng giá** gắn kèm, số hợp đồng giấy, tiêu đề, ngày ký, hiệu lực từ – đến, hạn mức công nợ |
| Điều khoản | Điều chỉnh giá, thanh toán, giao hàng, chiết khấu, tài trợ, chế tài, khác |
| Dòng | Mặt hàng, ĐVT, **sản lượng dự kiến**, SL cam kết tối thiểu; giá theo bảng giá. Nút **Lấy SL kế hoạch** (3.9) |
| Bộ phận được đặt | Giới hạn bộ phận được dùng giá hợp đồng (để trống = mọi bộ phận) |
| Chiết khấu – tài trợ | Cam kết theo **% doanh số** hoặc **số tiền cố định**, kỳ Quý / Năm / Cả hợp đồng; theo dõi **phải nhận – đã nhận – còn thiếu** từng kỳ; **ghi nhận** thực nhận (ngày, số tiền, hình thức: giảm trừ công nợ, chuyển khoản…) và hủy thực nhận (bắt buộc lý do) |
| Đính kèm, Thẩm định, Lịch sử duyệt | |

- **Phụ lục hợp đồng**: gia hạn, đổi bảng giá, đổi điều khoản; duyệt theo loại **CTA**; duyệt xong tự áp vào hợp đồng.
- **Nhập Excel hợp đồng hiện hữu**: tải mẫu, mỗi dòng một mặt hàng × ĐVT, gộp theo số HĐ giấy; vào trạng thái Nháp rồi gửi duyệt như lập tay.
- **Tạo từ kết quả thầu**: nút "Tạo hợp đồng" trên RFQ đã duyệt chọn thầu (05 – Đấu thầu).
- Quyền `Purchase.Contract.View/Manage/Approve`.

## 3.9 Lấy SL kế hoạch (trên RFQ và hợp đồng)

Nút **Lấy SL kế hoạch** trên lưới dòng RFQ (SL mời thầu) và hợp đồng (SL dự kiến):

1. Chọn **Từ ngày – Đến ngày** (tối đa 36 tháng) và **Toàn tập đoàn** (nếu có quyền). Mặc định: hợp đồng = đúng thời hạn hiệu lực; RFQ =
   12 tháng kể từ tháng sau ngày lập.
2. Tháng đầu/cuối không trọn tính theo số ngày (ví dụ 15/01 – 14/03, kế hoạch 31/50/31 cái → 31 × 17/31 + 50 + 31 × 14/31 = 81 cái).
3. Dòng có SL lẻ theo ĐVT dòng (tam giác vàng) chọn một cách xử lý: **Làm tròn lên** (mặc định), **Đổi sang ĐVT cơ sở** (chỉ RFQ) hoặc
   **Giữ số lẻ**.
4. Xem trước theo từng công ty → **Áp dụng** chép SL vào lưới; vẫn phải bấm Lưu phiếu.
