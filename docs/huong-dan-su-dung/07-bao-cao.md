# 7. Báo cáo

Mọi báo cáo: lọc trên đầu trang, **Xuất Excel** đúng cột đang hiện và dòng đã lọc, bấm đúp dòng để xem chi tiết hoặc mở chứng từ gốc. Báo cáo
"toàn tập đoàn" chỉ hiện ô chọn khi có quyền `…ViewAllCompanies` tương ứng; không có quyền thì chỉ thấy công ty đang làm việc.

## 7.1 Báo cáo kho (quyền `Report.Stock.View`)

| Mã | Báo cáo | Nội dung |
| --- | --- | --- |
| NXT | Nhập – xuất – tồn | Theo kỳ tháng, kho × mặt hàng: tồn đầu, nhập, xuất, tồn cuối (SL, giá trị) |
| SLOW | Hàng chậm luân chuyển | Mặt hàng còn tồn mà không xuất trong N ngày |
| EXP | Hàng sắp hết hạn | Lô đã/sắp hết hạn dùng |
| REORD | Gợi ý đặt hàng | Mặt hàng tồn dưới **điểm đặt hàng lại** / tồn an toàn; nút **Lập đề nghị mua** điền sẵn PR |
| GSTK | Tồn tập đoàn | Tồn một mặt hàng ở mọi công ty (chỉ số lượng), gợi ý mua nội bộ – quyền `Report.Stock.ViewAllCompanies` |
| TRANSIT | Hàng đi đường nội bộ | ICS đã ghi sổ mà ICR chưa ghi sổ, cả chiều bán ra và mua vào |
| ICREC | Đối chiếu mua bán nội bộ | Theo khoảng ngày: bên này bán – bên kia đã nhận và ngược lại (giá trị trước VAT) |

## 7.2 Báo cáo mua hàng (quyền `Report.Purchase.View`; toàn tập đoàn `Report.Purchase.ViewAllCompanies`)

| Mã | Báo cáo | Nội dung |
| --- | --- | --- |
| PRPEND | PR chưa xử lý | Dòng PR đã duyệt chưa lên PO đủ |
| SLA | SLA mua hàng | Số ngày làm việc từ PR đủ duyệt đến PO được duyệt, so SLA theo loại: hàng đã có giá, hàng chưa có NCC, hàng khẩn (tham số SEC) |
| QCDEV | Sai lệch nhận hàng | Dòng nhập có hàng không đạt, thiếu/thừa; **% đạt chất lượng** |
| MATCH3 | Đối chiếu PO – nhận – hóa đơn | Mỗi phiếu nhập đã ghi sổ: PO, tiền PO, tiền nhận, hóa đơn, chênh lệch; kết quả Chưa có hóa đơn / Lệch tiền / Khớp (dung sai nhập trên bộ lọc); cảnh báo nhận/hóa đơn trước ngày PO, hóa đơn tính cả hàng thiếu; kết quả nghiệm thu |
| TTPO | Theo dõi thanh toán | Mỗi PO đã duyệt: giá trị PO, đã nhận, tiền hóa đơn trên phiếu nhận hàng (XNH), hóa đơn chưa đề nghị, đề nghị đang lập / chờ duyệt / đã duyệt chờ chi, đã chi, giảm trừ, **còn phải trả**; xem theo PO hoặc NCC |
| RPSUP | Mua hàng theo NCC | Giá trị mua theo NCC / nhóm hàng / mặt hàng / công ty |
| SUPEVAL | Đánh giá NCC | Tỷ lệ trả hàng, tỷ lệ giao đúng hạn, **xếp loại** theo ngưỡng cấu hình ở RATE |
| SAVE | Tiết kiệm từ đấu thầu | So giá trúng thầu với giá tham chiếu trên các RFQ đã duyệt kết quả |
| PRCW | Giá mua & mua ngoài hợp đồng | Theo tuần: tỷ lệ mua theo giá đã duyệt (duyệt tháng, thầu, hợp đồng, bảng giá) và mua ngoài hợp đồng, lệch giá |
| PRVAR | Biến động giá mua | Giá mua mặt hàng thay đổi giữa các kỳ |
| SAVSTD | Tiết kiệm so giá chuẩn | Theo mặt hàng: giá trị theo giá chuẩn (phiếu GTH) − thành tiền mua; khoảng cách tới giá mục tiêu; số dòng vượt trần; tỷ lệ giá trị mua có giá chuẩn |
| KPI | Bảng KPI cung ứng | 9 chỉ số so với kỳ trước và **mục tiêu** (tham số công ty): tiết kiệm, mua theo giá/hợp đồng đã duyệt, đơn hàng đúng SLA, NCC giao đúng hạn, chất lượng, tỷ lệ mua khẩn, hàng chậm luân chuyển, hoàn thành đánh giá NCC, vi phạm nghiêm trọng |
| BCBAN | Báo cáo tháng cho Ban | Ghép tổng quan mua, KPI, tiết kiệm, mua ngoài giá đã duyệt, biến động giá, chiết khấu sản lượng gộp, NCC đủ điều kiện, hồ sơ NCC mới, KSNB. Mặc định toàn tập đoàn nếu có quyền. **Xuất gói Excel** – mỗi mục một sheet |

## 7.3 Dữ liệu mua lịch sử và phân tích giá (quyền `Analysis.PurchaseHistory.*`)

Chỉ để phân tích – **không** ghi sổ kho, không ảnh hưởng tồn, giá vốn, bảng giá.

| Mã | Màn hình | Cách dùng |
| --- | --- | --- |
| PHIS | Dữ liệu mua lịch sử | **Nhập Excel** dữ liệu mua 12–24 tháng của từng pháp nhân (tối đa 50.000 dòng, 20 MB/file; Kiểm tra thử rồi Nhập thật). Hệ thống tự khớp mã vật tư/MST với danh mục. Lọc theo lô, ngày, trạng thái khớp; xóa lô (có nhật ký) |
| PHMAP | Ánh xạ dữ liệu mua | Gắn mã vật tư cũ → mã hàng chung, NCC cũ (MST/tên) → NCC; gợi ý trùng gần đúng theo tên không dấu. Lưu ánh xạ áp cho mọi dòng cùng mã và lần nhập sau |
| PHAN | Phân tích giá hợp nhất | So giá cùng mặt hàng giữa các pháp nhân: SL, giá trị, giá bình quân, thấp/cao nhất, giá gần nhất, chênh lệch %, NCC giá tốt nhất, **giá mục tiêu** (giảm X% từ giá bình quân thấp nhất), **tiết kiệm dự kiến** |

## 7.4 Kiểm soát nội bộ (KSNB)

| Mã | Màn hình | Nội dung |
| --- | --- | --- |
| ANOM | Dấu hiệu bất thường | Tự phát hiện: giá cao lặp lại, PO sát ngưỡng duyệt, đổi tài khoản ngân hàng NCC, mua ngoài hợp đồng cao, hóa đơn tính cả hàng thiếu, chứng từ trước PO, xung đột lợi ích, chiết khấu/tài trợ chưa ghi nhận, chiết khấu sản lượng gộp chưa ghi nhận. Từ một dấu hiệu bấm **Ghi vi phạm** để lập vào sổ. Quyền `Control.Anomaly.View` |
| VIOL | Sổ vi phạm | Ghi nhận vi phạm (ngày, mức độ, tiêu đề, mô tả, NCC liên quan, đính kèm); thông báo thẳng lãnh đạo. Quyền `Control.Finding.Manage` (ghi), `Control.Finding.Receive` (nhận báo cáo) |

## 7.5 Xuất chứng từ cho kế toán (ACCX)

Quyền `Report.Accounting.Export`.

1. Chọn khoảng ngày → xem trước các chứng từ kho **đã ghi sổ** (GRN, RTV, TRF, ICS, ICR, ADJ).
2. Tải file Excel gồm sheet **Chứng từ** (chi tiết dòng), **Tổng hợp** (mỗi phiếu một dòng), **Hướng dẫn**: mã NCC tại công ty, MST, hóa đơn,
   **tài khoản kho theo nhóm hàng**, giá vốn chính thức (kỳ đã đóng) hoặc tạm tính.
3. Nhập file vào phần mềm kế toán của công ty. Khi kỳ được mở lại, người có quyền này nhận thông báo để xuất lại.

Công nợ và thanh toán NCC vẫn làm ở phần mềm kế toán.
