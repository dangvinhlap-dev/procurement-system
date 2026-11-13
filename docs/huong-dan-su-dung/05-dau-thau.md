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

1. **Tạo mới** (hoặc từ phiếu tìm nguồn). Đầu phiếu: ngày, tiêu đề, **hạn nộp báo giá – ngày và giờ** (gợi ý 17:00, giờ Việt Nam), phiếu tìm nguồn (nếu có), **trọng số 5 tiêu chí**
   (giá, chất lượng, giao hàng, thanh toán, bảo hành – cộng bằng 100, mặc định 60/20/10/5/5).
2. Dòng: nhập tay hoặc **lấy từ PR đã duyệt**; mặt hàng, ĐVT, SL. Nút **Lấy SL kế hoạch** lấy SL từ kế hoạch nhu cầu (xem 03 – mục 3.9).
3. Tab **NCC mời**: tối thiểu theo tham số công ty (mặc định **3**). Mời ít hơn vẫn gửi được khi ghi **giải trình** (nếu mọi dòng có NCC độc
   quyền đã duyệt thì hệ thống tự ghi giải trình). NCC Tiềm năng mời được; NCC danh sách đen không.
4. **Gửi RFQ** → Đang nhận báo giá.

### Nhập báo giá

- Bấm vào NCC → **Nhập báo giá**: ngày, hiệu lực, điều kiện thanh toán; từng dòng: **mã hàng NCC**, đơn giá, % CK, VAT, thời gian giao
  (để trống = không chào dòng đó); điểm 0–10 cho chất lượng, giao hàng, thanh toán, bảo hành.
- **Quá giờ hạn nộp** (ngày + giờ) thì không nhập/sửa báo giá được nữa – bấm **Gia hạn nhận báo giá** (chọn ngày, giờ mới; bắt buộc lý do,
  hiện trên phiếu). Sau hạn vẫn **Chấm điểm** được (chỉ đổi điểm, không đổi giá).
- Người có kê khai xung đột lợi ích với bất kỳ NCC được mời nào không nhập báo giá, không chọn thầu.

### Báo giá NCC nộp trên Portal – niêm phong (từ bản 0.25.0)

- NCC có tài khoản Portal (xem 02 – Tài khoản Portal) thấy RFQ đã gửi có mời mình ở mục **Thư mời báo giá** của Portal, tự nhập giá và
  **Nộp báo giá**; nộp lại được nhiều lần đến giờ hạn, bản cuối có hiệu lực, mỗi lần có **mã biên nhận**. NCC không thấy NCC khác.
- Trên RFQ, tab So sánh có dòng **Nộp qua Portal**: "Niêm phong – nộp lúc … (n lần)" – **chưa có giá**. Không nhập báo giá thay cho NCC đã
  nộp Portal, không chọn thầu được khi còn báo giá niêm phong.
- **Xem báo giá niêm phong** (chỉ người được cấp quyền "Xem báo giá Portal trước giờ mở"): hỏi lại rồi hiện giá; **mỗi lần xem ghi nhật ký
  truy cập** (Hệ thống → Nhật ký truy cập, sự kiện "Xem báo giá niêm phong").
- NCC tải **file báo giá** (bảng giá chi tiết, catalogue…, tối đa 2 MB/file) lên Portal, thêm/xóa đến giờ hạn. Trên RFQ dòng **File báo giá
  Portal** chỉ hiện số file khi còn niêm phong; bấm "n file" để xem (sau khi mở, hoặc người có quyền xem trước giờ mở – mỗi lần tải ghi nhật ký).
  Phần mềm nội bộ không thêm, xóa file của NCC.
- **Hỏi đáp NCC** (tab "Hỏi đáp NCC" của RFQ đã gửi): NCC gửi câu hỏi làm rõ trên Portal đến giờ hạn nộp; người lập RFQ nhận thông báo
  (chuông). Bấm **Trả lời…** trên câu hỏi – câu trả lời **chỉ NCC hỏi thấy**, NCC khác không thấy câu hỏi lẫn câu trả lời. Cần báo chung cho
  mọi NCC được mời (đính chính quy cách, điều kiện giao…) thì bấm **Đăng thông báo làm rõ…** – không nêu NCC nào hỏi. Trả lời, thông báo gửi
  rồi không sửa, xóa được; tiêu đề tab hiện số câu chưa trả lời.
- **Email báo NCC** (tab "Email NCC" của RFQ đã gửi): phần mềm tự gửi thư tới mọi tài khoản Portal **đang dùng** của NCC được mời khi
  **gửi RFQ** (thư mời, kèm hạn nộp), **gia hạn** (hạn mới – không gửi lý do gia hạn), **hủy RFQ**, **trả lời câu hỏi** (chỉ NCC hỏi) và
  **đăng thông báo làm rõ** (mọi NCC). Thư không có giá, không nêu NCC khác; có nút mở Portal khi máy chủ đã cấu hình `Portal:BaseUrl`.
  Tab liệt kê từng thư: loại thư, NCC, người nhận, trạng thái (Chờ gửi / Đã gửi / Thôi gửi / Lỗi). NCC chưa có tài khoản Portal đang dùng thì
  không có thư – Phòng CUVT báo NCC theo cách cũ. Máy chủ chưa bật email (`Email:Enabled`) thì thư nằm ở "Chờ gửi"; thư chờ quá 24 giờ thì
  thôi gửi (tránh báo hạn nộp muộn).
- **Sau giờ hạn nộp**: bấm **Mở báo giá Portal** (một lần) → giá các NCC hiện trên bảng so sánh, hệ thống ghi người và giờ mở; kiểm mã biên
  nhận với NCC nếu cần. Sau khi mở không gia hạn được nữa; giá NCC tự nộp không sửa thay – chỉ **Chấm điểm** rồi chọn thầu như thường.
- **Báo NCC kết quả chọn thầu** (từ bản 0.26.0): hội đồng **duyệt xong** kết quả thì phần mềm **chưa tự báo** – Phòng CUVT bấm **Gửi kết quả
  cho NCC** trên RFQ (hỏi lại, chỉ gửi một lần; đầu phiếu ghi người và giờ gửi). Từ lúc đó mỗi NCC được mời thấy trên Portal kết quả **của riêng
  mình** – cột "Kết quả" ở danh sách Thư mời báo giá và khối **Kết quả chọn thầu** (Trúng thầu / Trúng một phần / Không trúng thầu / Không nộp
  báo giá; dòng trúng kèm đơn giá, thành tiền theo báo giá của chính NCC). NCC không thấy NCC khác, giá khác, điểm, lý do chọn. Lúc đang chờ
  duyệt hoặc chưa bấm gửi, NCC chỉ thấy "Đang xét chọn". Lúc bấm gửi, phần mềm gửi thư **Báo trúng thầu** / **Báo không trúng thầu** cho NCC **đã có báo giá** (cả báo giá
  Phòng CUVT nhập thay) – thư không có giá, không nêu NCC trúng; NCC không báo giá không có thư. Xem thư ở tab "Email NCC".

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
