# 9. Quy trình mẫu và câu hỏi thường gặp

## 9.1 Mua hàng thường (có bảng giá / hợp đồng)

| Bước | Ai | Màn hình | Làm gì |
| --- | --- | --- | --- |
| 1 | Bộ phận | PR | Lập PR (bộ phận, mặt hàng, SL, ngày cần) → Gửi duyệt |
| 2 | Người duyệt PR | TODO / PR | Duyệt PR |
| 3 | Phòng mua | PO | Tạo PO, lấy dòng từ PR, chọn NCC → **Áp giá đã duyệt** → Gửi duyệt |
| 4 | Người duyệt PO | TODO / PO | Duyệt PO (và các chặng ngoại lệ nếu có) |
| 5 | Phòng mua | PO | In PO gửi NCC |
| 6 | Thủ kho | GRN | Hàng về: tạo phiếu nhập, chọn PO, **Lấy các dòng còn lại của PO**, nhập SL thực nhận, kiểm tra chất lượng → **Ghi sổ**; in biên bản giao nhận |
| 7 | Đơn vị đặt hàng | GRN | **Xác nhận nghiệm thu** Đạt/Không đạt |
| 8 | Kế toán | GRN / MATCH3 | Cập nhật hóa đơn; đối chiếu PO – nhận – hóa đơn |
| 9 | Thủ kho | RTV | Hàng lỗi: lập phiếu trả theo phiếu nhập gốc → Ghi sổ |

## 9.2 Mua hàng chưa có giá – đấu thầu

1. PR đã duyệt → **RFQ** (lấy dòng từ PR), mời ≥ 3 NCC → **Gửi RFQ**.
2. Nhập báo giá từng NCC, chấm điểm; hoàn thiện **Hồ sơ trình**.
3. **Chọn thầu, gửi duyệt** → hội đồng duyệt kết quả (thành viên chuyên môn ghi thẩm định).
4. **Tạo PO** (mua ngay) và/hoặc **Tạo bảng giá** + **Tạo hợp đồng** (mua lâu dài) → gửi duyệt bảng giá, hợp đồng.

## 9.3 Chu trình tháng của Ban cung ứng

| Thời điểm | Việc | Màn hình |
| --- | --- | --- |
| Đầu năm / khi điều chỉnh | Các công ty lập và chốt kế hoạch nhu cầu | KHN |
| Hằng tháng | Văn phòng Ban tổng hợp nhu cầu, khảo sát giá thị trường, kiểm tra NCC đủ điều kiện | KHNTH, KSG, SUPCOV |
| Trước tháng áp dụng | Lập phiếu duyệt NCC và giá tháng theo nhóm hàng (NCC chính/dự phòng, giá, hạn mức, giá chuẩn/trần/mục tiêu) → Ban duyệt | GTH |
| Trong tháng | Phòng cung ứng chung đặt hàng gộp, phân bổ, **Tạo PO** cho từng công ty; các công ty duyệt PO | DHG, PO |
| Trong tháng | Theo dõi lũy kế chiết khấu sản lượng gộp | CKBT |
| Sau kỳ chiết khấu | Ghi nhận chiết khấu thực nhận | CKB → tab Thực nhận |
| Cuối quý | Đánh giá NCC định kỳ | SUPREV |
| Cuối tháng | Báo cáo tháng cho Ban, KPI, dấu hiệu bất thường | BCBAN, KPI, ANOM |

## 9.4 Mua bán nội bộ giữa hai công ty

1. Công ty bán khai **bảng giá nội bộ** (IPR).
2. Công ty mua tra **Tồn tập đoàn** (GSTK) để biết công ty nào còn hàng.
3. Công ty bán lập **ICS** từ kho tổng → Ghi sổ → hệ thống sinh **ICR chờ nhận** ở công ty mua.
4. Thủ kho công ty mua nhận thông báo, mở ICR, nhập SL thực nhận → Ghi sổ.
5. Theo dõi **Hàng đi đường** (TRANSIT), **Đối chiếu nội bộ** (ICREC).

## 9.5 Cuối tháng – đóng kỳ kho

1. Ghi sổ hoặc hủy hết phiếu kho **nháp** trong tháng (Tra cứu chứng từ → lọc Chưa hoàn thành).
2. Kiểm kê (ADJ – Bảng kiểm kê) nếu cần, ghi sổ phiếu điều chỉnh.
3. Kỳ kho (PER) → **Đóng kỳ** tháng (tháng trước phải đã đóng).
4. Xuất chứng từ cho kế toán (ACCX) → nhập vào phần mềm kế toán.
5. Phát hiện sai sau khi đóng: mở lại kỳ đóng gần nhất (ghi lý do) rồi sửa; hoặc lập phiếu điều chỉnh ở kỳ đang mở.

## 9.6 Câu hỏi thường gặp

| Hiện tượng | Nguyên nhân và cách xử lý |
| --- | --- |
| Không chọn được NCC khi lập PO / phiếu nhập | NCC chưa **Đã duyệt** hoặc chưa **mở rộng** cho công ty đang làm việc (02 – mục 2.9) |
| Ghi sổ báo "kỳ chưa mở" / "đã đóng" | Ngày phiếu không thuộc kỳ Đang mở – nhờ kế toán mở kỳ (PER), hoặc sửa ngày phiếu |
| Ghi sổ báo tồn âm | Không đủ tồn ở kho xuất; kiểm tra thẻ kho, ghi sổ phiếu nhập trước |
| Không đóng được kỳ | Còn phiếu nháp trong kỳ, hoặc kỳ tháng trước chưa đóng |
| Gửi duyệt PO bị chặn "vượt hạn mức tháng" | Tổng đặt của các công ty vượt hạn mức trên phiếu GTH – Ban điều chỉnh phiếu GTH |
| Không thấy nút Duyệt | Không có quyền duyệt hoặc không được gán vai trò của cấp đang chờ; là người lập phiếu; đã duyệt cấp trước trong lượt này; có kê khai xung đột lợi ích với NCC |
| Phiếu chờ duyệt mà không ai duyệt được | Mọi người ở cấp đó đều vướng (người lập, xung đột lợi ích…) – quản trị gán thêm người vào vai trò của cấp |
| "Dữ liệu vừa được người khác thay đổi – hãy tải lại trang" | Người khác vừa lưu cùng phiếu – tải lại (F5) rồi làm lại |
| Không thấy mặt hàng / phiếu | Ngoài phạm vi kho hoặc nhóm hàng được gán; hoặc đang chọn nhầm công ty |
| Tải file đính kèm bị từ chối | File quá cỡ (mặc định 2 MB) hoặc loại file không được phép |
| Thêm NCC/hàng hóa báo chưa cấu hình dải mã | Quản trị cấu hình ở Đánh số danh mục (DSDM) |
| Thêm danh mục bị từ chối ở công ty thành viên | Thêm danh mục dùng chung chỉ làm ở tổng công ty |
| Bị khóa đăng nhập | Sai mật khẩu 5 lần – chờ 15 phút hoặc nhờ quản trị đặt lại mật khẩu |
| Giao diện hiển thị lạ sau khi nâng cấp | Tải lại trang bằng **Ctrl+F5** |
