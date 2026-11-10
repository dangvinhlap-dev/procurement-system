# 5. Tìm nguồn và đấu thầu

Luồng: **Phiếu tìm nguồn** (khi chưa có NCC phù hợp) → **RFQ** mời NCC báo giá → nhập **báo giá** → **so sánh, chấm điểm** → **chọn thầu**
→ hội đồng **duyệt kết quả** → **Tạo PO / Tạo bảng giá / Tạo hợp đồng**.

## 5.1 Phiếu tìm nguồn (SRC) – Đấu thầu → Tìm nguồn

- Đầu phiếu: **mặt hàng** hoặc **mô tả** (hàng chưa có mã), yêu cầu kỹ thuật, SL dự kiến, hạn chót, người phụ trách, ghi chú; đính kèm.
- Tab **NCC ứng viên**: mỗi NCC một dòng, trạng thái (Đạt / Không đạt / Đã gửi RFQ / Đã báo giá), ghi chú. NCC chưa có trong danh mục thì
  thêm ở danh mục NCC (trạng thái Tiềm năng) rồi chọn.
- Trạng thái: **Đang tìm** → Đã xong / Đã hủy.
- Bấm **Lập RFQ** để tạo RFQ mang sẵn mặt hàng, SL và các NCC chưa "Không đạt".
- PR đã duyệt có hàng mới chưa có mã → nút **Tìm nguồn** trên PR lập phiếu tìm nguồn.

## 5.2 Yêu cầu báo giá (RFQ) – Đấu thầu → Yêu cầu báo giá

**Vòng đời**: Nháp → **Đang nhận báo giá** → **Chờ duyệt kết quả** → **Đã chọn thầu** · Đã hủy.

### Lập và gửi RFQ

1. **Tạo mới** (hoặc từ phiếu tìm nguồn). Đầu phiếu: ngày, tiêu đề, **hạn nộp báo giá**, phiếu tìm nguồn (nếu có), **trọng số 5 tiêu chí**
   (giá, chất lượng, giao hàng, thanh toán, bảo hành – cộng bằng 100, mặc định 60/20/10/5/5).
2. Dòng: nhập tay hoặc **lấy từ PR đã duyệt**; mặt hàng, ĐVT, SL. Nút **Lấy SL kế hoạch** lấy SL từ kế hoạch nhu cầu (xem 03 – mục 3.9).
3. Tab **NCC mời**: tối thiểu theo tham số công ty (mặc định **3**). Mời ít hơn vẫn gửi được khi ghi **giải trình** (nếu mọi dòng có NCC độc
   quyền đã duyệt thì hệ thống tự ghi giải trình). NCC Tiềm năng mời được; NCC danh sách đen không.
4. **Gửi RFQ** → Đang nhận báo giá.

### Nhập báo giá

- Bấm vào NCC → **Nhập báo giá**: ngày, hiệu lực, điều kiện thanh toán; từng dòng: **mã hàng NCC**, đơn giá, % CK, VAT, thời gian giao
  (để trống = không chào dòng đó); điểm 0–10 cho chất lượng, giao hàng, thanh toán, bảo hành.
- **Quá hạn nộp** thì không nhập/sửa báo giá được nữa – bấm **Gia hạn nhận báo giá** (bắt buộc lý do, hiện trên phiếu).
- Người có kê khai xung đột lợi ích với bất kỳ NCC được mời nào không nhập báo giá, không chọn thầu.

### So sánh và chấm điểm

Tab **So sánh** (hoặc màn hình **So sánh báo giá – QCMP**): lưới dòng × NCC, gắn nhãn **giá thấp nhất** (quy về ĐVT cơ sở, trước VAT).
- Điểm giá NCC = trung bình (giá thấp nhất dòng ÷ giá NCC) × 10 trên các dòng NCC chào.
- **Tổng điểm** = Σ trọng số × điểm ÷ 100 (thang 10). Ví dụ trọng số 60/20/10/5/5, điểm 9/8/10/10/6 → 8,8.

### Hồ sơ trình (tab "Hồ sơ trình (x/7)")

7 mục: bảng so sánh báo giá (tự đánh dấu khi có báo giá), biên bản đàm phán, đánh giá kỹ thuật, phân tích chi phí, hồ sơ pháp lý NCC, tờ
trình đề xuất, xác nhận ngân sách. Mỗi mục ô **Đã có** + ghi chú; file đính kèm ở tab Đính kèm. Thiếu mục mặc định **không chặn** (hội đồng
thấy "đủ x/7, còn thiếu…"); công ty bật tham số `RFQ_DOSSIER_REQUIRED` thì chọn thầu bị chặn khi thiếu.

### Chọn thầu

1. Chọn NCC trúng thầu **theo từng dòng** (có thể tách nhiều NCC; dòng không trao thì để trống).
2. Không chọn NCC giá thấp nhất của dòng → **bắt buộc lý do**.
3. Số NCC thực chào giá ít hơn tối thiểu → **bắt buộc giải trình** (trừ khi đã giải trình mời thiếu và mọi NCC mời đều chào giá).
4. Bấm **Chọn thầu, gửi duyệt** → Chờ duyệt kết quả (quyền `Sourcing.Rfq.Award`).

### Duyệt kết quả chọn thầu

- Theo quy trình loại **RFQ** ở APRL, ngưỡng theo **giá trị trúng thầu** (trước VAT). Người duyệt cần `Sourcing.Rfq.ApproveAward`.
- **Người lập RFQ và người chọn thầu không tự duyệt.**
- Từ chối bắt buộc lý do → RFQ về Đang nhận báo giá (giữ lựa chọn cũ làm gợi ý). Người chọn thầu rút về được khi còn chờ.
- Thành viên chuyên môn ghi **ý kiến thẩm định** (tab THẨM ĐỊNH, xem 5.3) – hiện trong hộp thoại Duyệt.

### Sau khi kết quả được duyệt

| Nút | Kết quả |
| --- | --- |
| **Tạo PO** | Mỗi NCC trúng thầu một **PO nháp** (giá, CK, VAT theo báo giá; ngày giao = hôm nay + thời gian giao; kho nhận = kho tổng). NCC phải Đã duyệt và áp dụng tại công ty |
| **Tạo bảng giá** | Mỗi NCC trúng thầu một **bảng giá NCC nháp**; RFQ lập ở tổng công ty thì đánh dấu sẵn **toàn tập đoàn**. Bảng giá vẫn phải gửi duyệt |
| **Tạo hợp đồng** | Mỗi NCC trúng thầu một **hợp đồng nguyên tắc nháp** gắn bảng giá sinh từ chính RFQ (phải Tạo bảng giá trước); sản lượng dự kiến = SL trúng thầu. Bổ sung số HĐ giấy, ngày ký, điều khoản rồi gửi duyệt |

Giá trúng thầu vào lịch sử giá và báo cáo **Tiết kiệm từ đấu thầu (SAVE)**.

Quyền: `Sourcing.Rfq.View/Manage/Award/ApproveAward`.

## 5.3 Ý kiến thẩm định

Tab **THẨM ĐỊNH** trên phiếu GTH, RFQ, hợp đồng, NCC: thành viên chuyên môn (chất lượng, pháp lý…) ghi **Đạt / Không đạt**, nội dung, đính
kèm. Ý kiến thẩm định **không phải một cấp duyệt** nhưng người duyệt thấy trong hộp thoại Duyệt. Quyền `Sourcing.Appraisal.Give`.
