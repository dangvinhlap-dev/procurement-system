# 6. Kho

> **Từ bản 0.22.0 phân hệ kho ngừng dùng** – công ty dùng phần mềm kho riêng. Các màn hình dưới đây không còn trên menu; nhận hàng
> làm bằng phiếu xác nhận nhận hàng theo PO (XNH). Phần này chỉ áp dụng cho bản 0.21.0 trở về trước.

## 6.1 Nguyên tắc chung

- Mọi phiếu kho đi qua **một sổ kho chung**; chỉ khi **Ghi sổ** tồn kho mới thay đổi. Trạng thái: **Nháp → Đã ghi sổ** · Đã hủy.
- **Chỉ ghi sổ khi ngày chứng từ thuộc kỳ kho Đang mở** của công ty (xem 6.9). Hệ thống báo rõ "chưa có lịch / chưa mở / đang tính giá / đã đóng".
- Phiếu đã ghi sổ **không sửa trực tiếp**: bấm **Bỏ ghi sổ** (khi kỳ còn mở) rồi sửa; kỳ đã đóng thì lập phiếu điều chỉnh (ADJ) ở kỳ đang mở.
- **Không cho tồn âm**, trừ kho được bật "cho phép tồn âm".
- Tồn kho tính theo **ĐVT cơ sở**; phiếu nhập theo ĐVT nào cũng được, hệ thống quy đổi (ví dụ 2 thùng × 24 = 48 chai).
- Mặt hàng quản lý lô/hạn dùng thì bắt buộc nhập lô, hạn dùng.
- Danh sách, phiếu lọc theo **kho được phép** của người dùng. Phiếu có mặt hàng ngoài phạm vi nhóm hàng của người xem hiện "(ngoài phạm vi)",
  chỉ xem.
- Nút **Ghi sổ** tự lưu nháp trước. Số phiếu cấp khi lưu lần đầu; phiếu nháp hủy giữ số.

| Phiếu | Mã | Tác động tồn | Giá ghi sổ |
| --- | --- | --- | --- |
| Nhập mua NCC | GRN | + kho nhập | Đơn giá mua trước VAT (sau chiết khấu) |
| Xuất trả NCC | RTV | − kho đã nhập | Giá nhập gốc |
| Chuyển kho nội bộ | TRF | − kho đi, + kho đến (cùng công ty) | Giá vốn tạm tính, chốt khi đóng kỳ |
| Xuất bán nội bộ | ICS | − kho tổng công ty bán | Giá vốn công ty bán |
| Nhập mua nội bộ | ICR | + kho tổng công ty mua | Giá bán nội bộ |
| Điều chỉnh, kiểm kê | ADJ | ± một kho | Tăng: giá nhập tay; Giảm: giá vốn |

## 6.2 Nhập mua từ NCC (GRN) – Kho → Nhập mua NCC

### Lập phiếu

1. **Tạo mới**. Đầu phiếu: ngày nhập, **kho nhập**, **NCC** (Đã duyệt, đang giao dịch tại công ty), **PO** (nếu nhập theo đơn), số/ký hiệu/ngày
   hóa đơn, số phiếu giao hàng, người giao, ghi chú.
   - Tham số `RECEIPT_PO_REQUIRED` bật thì **bắt buộc theo PO**.
2. Nhập theo PO: bấm **Lấy các dòng còn lại của PO**. Mỗi dòng trỏ một dòng PO; mặt hàng, ĐVT, **đơn giá = giá thực mua sau chiết khấu**, VAT
   theo PO. SL nhận (đã ghi sổ + phiếu này) ≤ SL đặt × (1 + dung sai %).
3. Dòng: SL thực nhận, lô, hạn dùng; **kiểm tra chất lượng**:
   - **SL theo chứng từ giao** của NCC, **SL không đạt** (không nhập kho), **kết quả** (Chưa kiểm tra / Đạt / Không đạt), **ghi chú kiểm tra**.
   - Thiếu/thừa = SL thực nhận + SL không đạt − SL chứng từ. Không đạt hoặc có thiếu/thừa thì bắt buộc ghi chú.
4. **Tiền hóa đơn (gồm VAT)** để đối chiếu với kế toán.
5. **Ghi sổ** → tồn tăng, PO cập nhật SL đã nhận (đủ thì PO Đã nhận đủ).

Tham số liên quan (SEC): **Bắt buộc kiểm tra chất lượng khi nhận hàng** (mọi dòng phải có kết quả mới ghi sổ); **Người lập PO không ghi sổ
nhận hàng của PO đó**.

### Sau khi ghi sổ

- **Cập nhật hóa đơn**: số, ký hiệu, ngày, tiền hóa đơn sửa được cả khi đã ghi sổ, kỳ đã đóng (không đổi sổ kho).
- **In biên bản giao nhận và kiểm tra hàng hóa** (PDF) và **In phiếu nhập**.
- **Xác nhận nghiệm thu** của đơn vị đặt hàng: chọn **Đạt / Không đạt** (Không đạt phải ghi ý kiến). Người lập hoặc người ghi sổ phiếu không
  tự xác nhận. Quyền `Inventory.Receipt.Accept`. Kết quả hiện ở báo cáo MATCH3.
- Không bỏ ghi sổ được khi đã có phiếu trả NCC ghi sổ theo phiếu này.

## 6.3 Xuất trả NCC (RTV) – Kho → Xuất trả NCC

1. Chọn **phiếu nhập gốc đã ghi sổ** (kho xuất, NCC lấy theo phiếu nhập; ngày trả không trước ngày nhập).
2. Chọn dòng nhập (hoặc **Lấy tất cả** các dòng còn trả được); mặt hàng, ĐVT, lô, đơn giá, VAT chép từ dòng nhập, chỉ nhập **số lượng trả**.
3. SL trả ≤ **còn trả được** = SL nhập − SL các phiếu trả đã ghi sổ khác.
4. **Ghi sổ** → trừ tồn theo giá nhập gốc; trừ vào SL đã nhận của PO (PO đã nhận đủ thì mở lại để NCC giao bù).

## 6.4 Chuyển kho nội bộ (TRF) – Kho → Chuyển kho nội bộ

- Hai kho **khác nhau, cùng công ty**, đều trong phạm vi của người lập. Kho phụ **không** chuyển sang công ty khác (liên công ty dùng ICS/ICR).
- Dòng không có đơn giá, không VAT; hiện tồn tại kho xuất, cảnh báo khi vượt tồn.
- Ghi sổ theo **giá vốn tạm tính** = tổng giá trị tồn ÷ tổng SL tồn của mặt hàng trên mọi kho của công ty; chốt giá vốn chính thức khi đóng kỳ.
- Bỏ ghi sổ cần thấy cả hai kho.

## 6.5 Mua bán nội bộ (ICS / ICR)

Liên công ty chỉ đi **kho tổng → kho tổng**, giá = **giá bán nội bộ** (4.2).

**Bên bán – Xuất bán nội bộ (ICS)**

1. Kho xuất = **kho tổng** công ty bán; chọn **công ty mua**.
2. Dòng: mặt hàng, SL; **đơn giá, VAT do hệ thống lấy** từ bảng giá nội bộ theo ngày phiếu (chưa có giá thì không lưu được).
3. **Ghi sổ** → trừ tồn kho tổng bên bán; hệ thống **tự sinh phiếu ICR "Chờ nhận"** ở công ty mua và báo thủ kho bên mua.
4. Bỏ ghi sổ ICS: bị chặn nếu bên mua đã ghi sổ ICR; nếu ICR còn chờ nhận thì ICR bị hủy.

**Bên mua – Nhập mua nội bộ (ICR)**

1. Mở ICR chờ nhận (từ Thông báo hoặc danh sách ICR).
2. Nhập **ngày nhận** (không trước ngày xuất), **SL thực nhận** (0 → SL gửi), lô, ghi chú chênh lệch. Giá không sửa.
3. **Ghi sổ** → tăng tồn kho tổng bên mua theo giá bán nội bộ. Trạng thái nhận: Đã nhận đủ / **Nhận thiếu**.

ICR chờ nhận là **hàng đi đường** – xem báo cáo TRANSIT; không chặn đóng kỳ bên mua. Đối chiếu hai bên ở báo cáo ICREC.

## 6.6 Điều chỉnh, kiểm kê (ADJ) – Kho → Điều chỉnh, kiểm kê

1. **Tạo mới**: ngày, **một kho** của công ty, **lý do** (bắt buộc), **số biên bản kiểm kê** (bắt buộc), ghi chú; đính kèm bản scan biên bản.
2. Dòng nhập tay: mặt hàng, ĐVT, **Tăng / Giảm**, SL, lô, hạn dùng (dòng tăng).
   - **Tăng** (hàng thừa): nhập đơn giá tay – tham gia giá vốn như nhập có giá.
   - **Giảm** (hàng thiếu): không nhập giá, xuất theo giá vốn (chốt khi đóng kỳ). Cột tồn hiện tại, cảnh báo giảm vượt tồn.
3. Hoặc bấm **Bảng kiểm kê** (cần chọn kho): hệ thống liệt kê tồn sổ của kho theo mặt hàng, lô; gõ **SL thực đếm** – chỉ dòng có chênh lệch được
   lập vào phiếu. Hệ thống lấy tồn sổ lúc lưu và tự tính chiều Tăng/Giảm, SL.
4. **Ghi sổ** (quyền riêng). **In "Biên bản kiểm kê, điều chỉnh kho"** có tồn sổ, thực đếm, tăng, giảm, giá trị tăng và 4 chỗ ký.

Quyền riêng: `Inventory.Adjustment.Create` (lập, sửa, hủy – thường cho thủ kho) và `Inventory.Adjustment.Post` (ghi sổ, bỏ ghi sổ – thường
cho kế toán).

## 6.7 Tồn kho hiện tại (STK)

Tồn theo **kho × mặt hàng × lô**: SL, giá trị, đơn giá bình quân. Cảnh báo dưới mức tối thiểu (so với tổng tồn trên mọi kho của công ty), tồn
âm. Lọc kho, nhóm hàng, hiện dòng tồn 0. Bấm đúp dòng mở **thẻ kho** của mặt hàng.

## 6.8 Thẻ kho (CARD)

Một mặt hàng (một kho hoặc mọi kho) trong khoảng ngày: tồn đầu, từng phát sinh nhập/xuất, tồn lũy kế, tồn cuối. Bấm đúp phát sinh để mở phiếu
gốc.

## 6.9 Kỳ kho, đóng/mở kỳ và giá vốn – Hệ thống → Kỳ kho (PER)

### Lịch kỳ

- Mỗi công ty một lịch: **kỳ năm** gồm 12 **kỳ tháng**. Trạng thái: Chưa mở → **Đang mở** → Đang tính giá → **Đã đóng**.
- **Tạo lịch năm** (năm phải liền nhau). **Mở kỳ tháng** tuần tự (kỳ đầu tự chọn, các kỳ sau liền sau kỳ đã mở). Có thể mở nhiều tháng cùng lúc.
- Quyền: `Inventory.Period.Open` (tạo lịch, mở kỳ), `Inventory.Period.Close`, `Inventory.Period.Reopen`.

### Đóng kỳ

Điều kiện: kỳ Đang mở; **kỳ tháng trước đã đóng** (đóng tuần tự); **không còn phiếu kho nháp** trong kỳ (ICR chờ nhận không tính).

Khi bấm **Đóng kỳ**, hệ thống: khóa ghi sổ kỳ đó → tính **giá vốn bình quân gia quyền cuối kỳ** cho từng mặt hàng của công ty → gán giá vốn
cho mọi dòng xuất (chuyển kho, xuất bán nội bộ, điều chỉnh giảm) → chốt số dư kho × hàng × lô → **Đã đóng**. Lỗi (ví dụ tồn cuối kỳ âm) thì kỳ
về Đang mở.

> **Giá vốn đơn vị kỳ = (Giá trị tồn đầu kỳ + Giá trị nhập có giá trong kỳ) ÷ (SL tồn đầu kỳ + SL nhập có giá trong kỳ)**
>
> Nhập có giá: nhập mua NCC, nhập mua nội bộ, điều chỉnh tăng; trừ trả NCC theo giá đích danh. Chuyển kho không tham gia.
>
> Ví dụ: tồn đầu 100 × 10.000; nhập 50 × 13.000; trả NCC 10 từ lô 13.000; xuất 120. Giá vốn = (1.000.000 + 650.000 − 130.000) ÷ (100 + 50 − 10)
> = 1.520.000 ÷ 140 ≈ 10.857,14 đ/đơn vị.

### Mở lại kỳ

Chỉ mở lại **kỳ đã đóng gần nhất**, bắt buộc lý do. Số dư và giá vốn kỳ bị xóa, dòng xuất về tạm tính tới lần đóng sau; người có quyền xuất
dữ liệu kế toán nhận thông báo.
