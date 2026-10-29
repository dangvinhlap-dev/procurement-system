# Hướng dẫn giao diện – phong cách ứng dụng desktop kiểu Windows XP

Ảnh mẫu: `docs/mau-man-hinh-danh-sach.png` – mọi màn hình danh sách phải giống ảnh này về bố cục.

## Tinh thần chung
- Cảm giác như phần mềm kế toán/ERP chạy trên Windows XP: dày thông tin, nhiều dữ liệu trên một màn hình,
  thao tác nhanh bằng bàn phím, không có khoảng trắng thừa.
- Giữ bố cục và cảm giác Windows XP (thanh menu, thanh công cụ, cây điều hướng, thanh trạng thái,
  viền ô rõ ràng) nhưng vẽ phẳng, sạch như ảnh mẫu: không gradient xanh Luna đậm, không bo góc lớn, không đổ bóng.
- Chạy trên trình duyệt, tối ưu cho màn hình 1366×768 trở lên.

## Khung ứng dụng (AppShell) – cố định cho mọi trang
1. Thanh menu trên cùng (cao 28px): nhóm chức năng theo module (Hằng ngày, Mua hàng, Kho, Giá và NCC,
   Đấu thầu, Báo cáo, Hệ thống). Bấm mở menu thả xuống. Bên phải: công ty đang làm việc (chọn đổi được),
   chuông thông báo có số, ngôn ngữ, tên người dùng.
2. Thanh công cụ (cao 32px): ô lệnh "Ô lệnh – tên màn hình" (gõ mã hoặc tên màn hình rồi Enter để mở nhanh,
   ví dụ PO, PR, NXT), sau đó các nút biểu tượng: Lưu, Quay lại, Trang chủ, Hủy, In, Tìm kiếm.
   Nút không dùng được thì mờ đi.
3. Cột trái – cây điều hướng (rộng 240px, kéo đổi được, thu gọn được):
   - Nút "Thu gọn tất cả" và ghim ở đầu.
   - Nhóm "Ghim" (màn hình người dùng tự ghim), sau đó các nhóm theo module.
   - Mỗi mục: biểu tượng nhỏ + tên + số đếm bên phải (badge xám; đỏ nếu cần chú ý).
   - Mục đang mở: nền xanh đậm, chữ trắng.
4. Vùng nội dung chính (phải).
5. Thanh trạng thái dưới cùng (cao 22px): bên trái là thông báo hệ thống; bên phải là các ô có viền:
   tên màn hình hiện tại | tên người dùng | ngày làm việc | phiên bản phần mềm | INS.

## Mẫu màn hình danh sách (ListPage + DataGrid)
Dùng cho PR, PO, phiếu kho, NCC, hàng hóa… Từ trên xuống:
1. Tiêu đề màn hình (đậm, 18px) + bộ chọn "Mẫu hiển thị" (Chuẩn, mẫu đã lưu) + dòng nhỏ "Tổng N bản ghi".
2. Hàng nút lệnh: nút chính "+ Tạo mới" (màu xanh, duy nhất có màu đậm); nút phụ dạng chữ + biểu tượng:
   Xuất CSV, Xuất Excel, Nhập CSV/Excel.
3. Tab kiểu Windows (có viền, tab đang chọn liền với nội dung): Danh sách | Tổng hợp | Phân tích.
4. Khung bộ lọc (có viền, nền xám rất nhạt): ô Tìm kiếm (gõ có dấu hoặc không dấu đều tìm được),
   các ô lọc theo nghiệp vụ, "Nhóm theo", nút "Cột" (chọn cột hiển thị, có số cột đang ẩn), nút "Xóa bộ lọc".
5. Vùng "Kéo tiêu đề cột vào đây để nhóm" (viền đứt nét) – tự viết bằng thư viện miễn phí,
   không dùng AG Grid Enterprise.
6. Lưới dữ liệu:
   - Dòng cao 24px, chữ 12px, đường kẻ ngang và dọc mảnh màu xám, dòng xen kẽ hơi xám.
   - Tiêu đề cột nền xám nhạt, bấm để sắp xếp (▲▼), kéo để đổi độ rộng và thứ tự.
   - Cột đầu tiên nhỏ: biểu tượng ghim/đánh dấu.
   - Cột Trạng thái: chấm tròn màu + chữ (xanh lá = đã xử lý/hoàn tất, xám = còn hạn,
     đỏ = quá hạn/lỗi, cam = chờ duyệt). Màu không là thông tin duy nhất – luôn có chữ.
   - Ngày dd/MM/yyyy, số kiểu 1.234.567,89, cột số căn phải.
   - Chữ dài bị cắt "…" và hiện đầy đủ khi rê chuột.
   - Bấm đúp hoặc Enter để mở chi tiết; mũi tên lên/xuống để di chuyển; dòng chọn nền xanh nhạt.
   - Cuộn ảo hoặc phân trang khi trên 500 dòng.

## Màn hình chứng từ (DocumentForm)
Theo docs/design.md mục 10.1, cùng phong cách: thanh lệnh của phiếu, phần đầu phiếu 2–3 cột,
lưới dòng nhập như Excel, chân phiếu tổng tiền, tab phụ (Đính kèm, Lịch sử duyệt, Nhật ký).

## Màu sắc và chữ (khai báo thành theme token, không viết màu cứng trong component)
- Font: "Segoe UI", Tahoma, sans-serif; cỡ cơ bản 12px; tiêu đề 18px.
- Nền ứng dụng #F3F3F3; nền vùng nội dung và lưới #FFFFFF; viền #C8C8C8.
- Màu chính (nút chính, mục chọn, liên kết): #1F4FA0. Dòng chọn trong lưới: #DCE8F8.
- Trạng thái: thành công #2E8B3E, cảnh báo #D9822B, lỗi/quá hạn #C62828, trung tính #8A8A8A.
- Bo góc 2px; nút cao 26px; ô nhập cao 26px.

## Bàn phím
F2 thêm dòng, F3 tìm, Ctrl+S lưu, Esc đóng hộp thoại, Ctrl+Enter gửi duyệt,
Alt + chữ gạch chân để mở menu. Mọi thao tác chính làm được không cần chuột.

## Kỹ thuật
- Ant Design với ConfigProvider: theme token theo bảng màu trên, bật thuật toán compact, locale vi_VN.
- Component dùng chung: `AppShell`, `ListPage`, `DataGrid`, `DocumentForm`.
- Ảnh mẫu khác đặt trong `docs/` với tên rõ nghĩa, ví dụ `mau-form-don-mua-hang.png`.
