# Ghi nhận bản 0.27.0 – để người dùng phân tích, yêu cầu tiếp

> Lập 13/11/2026 theo yêu cầu "ghi nhận lại bản 0.27.0 để tôi phân tích và yêu cầu sau". Tài liệu tổng hợp, không thay thế bản gốc:
> quy tắc chi tiết ở `docs/design.md` mục 19.3 (Y8, Y9, Y11), 19.5 (P3–P5), 19.6 (lộ trình); danh sách thay đổi ở `CHANGELOG.md` [0.27.0];
> nhật ký từng bước ở `docs/TIEN_DO.md`. Mã quy tắc (P3-1, Y8-2…) dùng để người dùng chỉ đúng điểm cần sửa.

## 1. Tổng quan

| Mục | Nội dung |
| --- | --- |
| Phát hành | 13/11/2026, tag `v0.27.0`, gói `procurement-0.27.0.zip` (GitHub Releases) |
| Gộp lộ trình | 0.25.0 (P3), 0.26.0 (P4, P5) và 0.27.0 (Y8, Y9, Y11) – 0.25.0, 0.26.0 không phát hành riêng |
| Bỏ khỏi lộ trình | P6a hóa đơn điện tử trên Portal (công ty có kênh riêng) – hóa đơn lấy từ XNH ("Cập nhật hóa đơn") |
| CSDL | 10 migration (92 → 102), script `db/scripts/0.27.0.sql` (chạy lại được) |
| Kiểm tra trước phát hành | build 0 cảnh báo; `dotnet test` 785/785; Playwright 147/147; tsc, oxlint, `npm run build` |
| Bản test 5007 | Đã nâng tại chỗ lên 0.27.0 (sao lưu `D:\project\procurement_kiemtra_0150_truoc_0.27.0.dump`); `/health` Healthy; đã gán quyền mới cho vai trò trùng mã vai trò mẫu |
| Chưa làm trên 5007 | Chưa áp lại "Cấu hình duyệt mẫu" CUVT cho DNT / BKT / TVB → hiện duyệt **một cấp** (người test tự áp ở Quy trình duyệt) |

Hai mảng chính:

1. **Portal nhà cung cấp** – NCC báo giá online niêm phong, gửi file, hỏi đáp, nhận email; xác nhận / từ chối đơn hàng; nhận kết quả chọn thầu;
   báo giao hàng.
2. **Thanh toán** – đề nghị thanh toán 5A/5B theo PO, chi phí tạp vụ 2C → 1C, theo dõi thanh toán, xuất chứng từ mua cho kế toán.

## 2. Luồng nghiệp vụ sau 0.27.0

```
Mua theo PO (A công trình / B hàng hóa / DV dịch vụ)
  Đề nghị mua → Bảng tổng hợp → RFQ ──(Portal: NCC báo giá niêm phong, hỏi đáp, file)──► Mở báo giá → Chọn thầu → Duyệt
      → [Gửi kết quả cho NCC – Phòng CUVT tự bấm]
  → PO duyệt ──(Portal: thư đơn mới; NCC Xác nhận / Từ chối; Phòng CUVT mở lại phản hồi)
      → (Portal, không bắt buộc) NCC lập phiếu báo giao BGH
      → XNH xác nhận nhận hàng (điền sẵn từ BGH nếu có) → "Cập nhật hóa đơn" trên XNH
      → DNT 5A (loại A) / 5B (B, DV): nhiều dòng = nhiều XNH có hóa đơn → duyệt GĐ khối → TP TC-NQ-KT → GĐ TC-NQ-KT → Chủ tịch
      → Kế toán ghi "Đã chi"
  Báo cáo: TTPO theo dõi thanh toán (theo PO / NCC); ACCP xuất Excel PO + XNH + DNT cho phần mềm kế toán

Tạp vụ (loại C – MẪU GIẢ LẬP, chỉ nhập thử)
  Bảng kê 2C (BKT, theo bộ phận, chi phí đã phát sinh) → kiểm định mức tháng (vượt: thêm chặng TVB, không chặn)
      → duyệt Trưởng phòng ban → GĐ khối → TP TC-NQ-KT → Chủ tịch
  → DNT 1C gộp nhiều 2C đã duyệt của một bộ phận, trả người nhận tiền → duyệt như 5A/5B → Đã chi
```

## 3. Màn hình, phiếu, quyền mới

| Mã màn hình / phiếu | Tên | Nơi mở | Quyền |
| --- | --- | --- | --- |
| `/ncc/bao-gia` | Thư mời báo giá (Portal) | Portal NCC | Tài khoản Portal đang dùng |
| `/ncc/don-hang` | Đơn đặt hàng + báo giao (Portal) | Portal NCC | Tài khoản Portal đang dùng |
| BGH | Phiếu báo giao hàng của NCC | Portal; tab "Giao hàng NCC" trên PO | NCC lập; nội bộ xem theo quyền xem PO / XNH |
| DNT | Đề nghị thanh toán 5A / 5B / 1C | Mua hàng → Đề nghị thanh toán; nút trên PO, bảng kê | `Purchase.Payment.View/Create/Approve/Pay` |
| BKT | Bảng kê chi phí tạp vụ 2C | Mua hàng → Bảng kê tạp vụ | `Purchase.Sundry.View/Create/Approve` |
| TTPO | Theo dõi thanh toán | Báo cáo → Theo dõi thanh toán | `Report.Purchase.View` (toàn tập đoàn: `.ViewAllCompanies`) |
| ACCP | Xuất chứng từ mua cho kế toán (thay ACCX) | Báo cáo → Xuất chứng từ mua | `Report.PurchaseAccounting.Export` |
| – | Xem báo giá niêm phong trước giờ mở | Nút trên RFQ | `Sourcing.Rfq.ViewSealed` (không có trong vai trò mẫu – quản trị cấp riêng, mỗi lần xem ghi nhật ký) |

Thay đổi trên màn hình cũ: RFQ có giờ hạn nộp, gia hạn kèm giờ, dòng "Nộp qua Portal", nút **Mở báo giá Portal**, **Chấm điểm**, **Gửi kết quả cho
NCC**, tab "Hỏi đáp NCC", "Email NCC"; PO có ô "Xác nhận của NCC", tab "Xác nhận NCC", "Giao hàng NCC", nút **Lập đề nghị thanh toán**; XNH có ô
"Theo phiếu báo giao NCC", khóa "Cập nhật hóa đơn" khi đã vào DNT; trang đăng nhập nội bộ có link "Đăng nhập Portal nhà cung cấp".

## 4. Portal NCC – quy tắc chính (design.md 19.5)

### 4.1 Báo giá niêm phong (P3a, P3-1 … P3-8)
- Hạn nộp gồm **ngày + giờ** (gợi ý 17:00; RFQ cũ = 23:59); mọi kiểm tra hạn so thời điểm.
- NCC nộp lại nhiều lần trước hạn, **bản cuối có hiệu lực**; mỗi lần có biên nhận mã băm SHA-256.
- Trước giờ mở nội bộ chỉ thấy giờ nộp, số lần nộp – không thấy giá; bản nộp **không ghi AuditLog** (tránh lộ giá).
- Phòng CUVT không nhập thay cho NCC đã nộp Portal; không chọn thầu khi còn báo giá niêm phong.
- **Mở báo giá Portal** một lần, sau giờ hạn; đã mở thì không gia hạn, NCC không nộp lại; giá NCC tự nộp không sửa thay, chỉ chấm điểm.

### 4.2 File báo giá, hỏi đáp, email (P3b, P3-9 … P3-18)
- File báo giá niêm phong như giá; nội bộ chỉ xem/tải sau khi mở (hoặc người có quyền ViewSealed, ghi nhật ký); nội bộ không thêm/xóa file NCC.
- Hỏi đáp: NCC hỏi đến giờ hạn; **trả lời riêng NCC hỏi** (một lần, không sửa); **thông báo làm rõ công khai** cho mọi NCC được mời.
- Email gửi mọi tài khoản Portal đang dùng: thư mời, gia hạn (không gửi lý do), hủy (không gửi lý do), trả lời, thông báo làm rõ. Thư không có giá.
  Thư soạn cùng giao dịch, gửi nền, quá 24 giờ thì thôi gửi.

### 4.3 Đơn hàng và kết quả chọn thầu (P4, P4-1 … P4-9)
- NCC thấy PO đã duyệt của mình (không thấy PO nội bộ, nháp). Xác nhận (bắt buộc ngày giao cam kết) hoặc Từ chối (bắt buộc lý do); một phản hồi
  có hiệu lực, không sửa. NCC **không sửa** SL/giá/ngày trên PO.
- Phòng CUVT **mở lại phản hồi** khi PO còn Đã duyệt, lý do gửi NCC; phản hồi cũ giữ làm lịch sử.
- Thư: đơn mới (khi duyệt xong), đề nghị phản hồi lại, hủy đơn (không ghi lý do).
- Kết quả chọn thầu: duyệt xong **không tự gửi** – Phòng CUVT bấm "Gửi kết quả cho NCC" (một lần). Mỗi NCC chỉ thấy kết quả của mình (trúng có
  giá của mình; không trúng không giá, không nêu NCC trúng).

### 4.4 Báo giao hàng (P5, P5-1 … P5-8)
- NCC lập BGH khi PO Đã duyệt và không đang từ chối; SL theo "còn giao được"; kèm file (phiếu giao, CO/CQ, ảnh).
- Trạng thái tính từ XNH: chờ nhận → đang nhận → đã nhận; NCC hủy được khi chưa có XNH.
- **Không bắt buộc**: XNH vẫn lập thẳng từ PO, không cảnh báo khi thiếu BGH.
- **Việc sau đã đồng ý, chưa làm**: BGH "chờ nhận" tự đóng khi PO đã nhận đủ / hoàn thành.

## 5. Thanh toán – quy tắc chính (design.md 19.3)

### 5.1 Đề nghị thanh toán 5A/5B (Y8-1 … Y8-8)
- Một DNT cho **một PO** đã duyệt/đã đóng; A → 5A, B/DV → 5B; tiền mặt hoặc chuyển khoản.
- Nhiều dòng; mỗi dòng = một **XNH đã có hóa đơn**; số, ký hiệu, ngày, tiền hóa đơn chụp vào dòng lúc lưu và gửi. Mỗi XNH chỉ ở một DNT chưa hủy.
- Giảm trừ (tạm ứng, phạt…) bắt buộc lý do; số đề nghị = tổng − giảm trừ > 0.
- Chuyển khoản bắt buộc chọn tài khoản NCC trong danh mục (chụp vào phiếu); NCC chưa có tài khoản chỉ chọn tiền mặt.
- Duyệt: **GĐ khối** (khối của bộ phận đặt hàng của PO) → **TP TC-NQ-KT** → **GĐ TC-NQ-KT** → **Chủ tịch**. Không ghi ngân sách (PO đã tính).
- Kế toán ghi **Đã chi** (ngày chi, ghi chú số UNC/phiếu chi); đã chi thì **không hủy** được (kể cả quản trị).
- In theo mẫu Excel 5A/5B: lũy kế đã mua, số còn lại (cùng số liệu 4A/4B), bảng hóa đơn, giảm trừ, bằng chữ, khối ký.

### 5.2 Tạp vụ 2C và 1C (Y11-1 … Y11-9) – **mẫu giả lập**
- 2C (BKT): theo bộ phận, dòng = ngày chi, nội dung, nơi mua, số HĐ/chứng từ, số tiền (gồm thuế); tối đa 200 dòng.
- Định mức = ngân sách bộ phận × tháng (mục 18.24) của tháng bảng kê; vượt **không chặn**, thêm chặng **TVB** (GĐ TC-NQ-KT).
- Duyệt 2C: Trưởng phòng ban → GĐ khối → TP TC-NQ-KT → Chủ tịch.
- 1C: DNT biểu mẫu 1C, **không gắn PO/NCC**, một bộ phận, dòng = bảng kê 2C đã duyệt; trả **người nhận tiền** (ngân hàng nhập tay).
- Hủy bảng kê kéo theo DNT 1C đang giữ (hủy dây chuyền); DNT đã chi thì chặn.
- Màn hình hiện cảnh báo vàng; bản in có dòng đỏ "MẪU GIẢ LẬP – CHỈ DÙNG THỬ". Có mẫu thật: đặt `CuvtPrintForms.SundryFormsSimulated` và
  `SUNDRY_FORMS_SIMULATED` về false, sửa in và quy tắc.

### 5.3 Báo cáo (Y9-1 … Y9-6)
- **TTPO**: mỗi dòng một PO đã duyệt/đóng – giá trị PO, đã nhận, tiền hóa đơn, XNH chưa HĐ, HĐ chưa đề nghị, đang lập/chờ duyệt, chờ chi,
  đã chi, giảm trừ, còn phải trả; đèn Chưa có HĐ / Chưa chi / Chi một phần / Đã trả đủ HĐ; xem theo PO hoặc NCC; xuất Excel. **Không gồm 1C**.
- **ACCP**: chỉ công ty đang làm việc; PO (theo ngày duyệt), XNH (theo ngày nhận, kèm hóa đơn), DNT đã duyệt (theo ngày duyệt) → Excel 5 sheet;
  tối đa 2 năm, 50.000 dòng/loại. Không làm sổ cái – kế toán tự định khoản.

## 6. Điểm do phần mềm **tự đặt** – người dùng nên xem lại khi phân tích

Các quy tắc dưới đây chưa có biểu mẫu/quyết định chính thức; đang chạy theo đề xuất, người dùng chỉnh được:

| # | Điểm | Đang làm | Câu hỏi gợi ý |
| --- | --- | --- | --- |
| 1 | Giờ hạn nộp mặc định (P3-1) | Gợi ý 17:00; RFQ cũ 23:59 | Giờ chuẩn công ty? |
| 2 | Quyền xem niêm phong (P3-6) | Không có trong vai trò mẫu, quản trị cấp riêng | Ai được giao quyền này (KSNB? Trưởng ban)? |
| 3 | Hỏi đáp (P3-11, P3-14) | Chỉ hỏi đến giờ hạn; chưa có đính kèm file trong hỏi đáp | Có cần đính kèm, cần hỏi sau hạn? |
| 4 | Email (P3-16, P4-5) | Không gửi lý do gia hạn / hủy | Có cần ghi lý do cho NCC? |
| 5 | Phản hồi PO (P4-2) | NCC chỉ xác nhận/từ chối cả đơn | Cần NCC đề xuất sửa từng dòng (SL, giá, ngày)? Nhắc NCC chưa xác nhận sau n ngày? |
| 6 | Kết quả thầu (P4-7) | NCC không trúng không thấy giá, không biết NCC trúng | Đúng chính sách công bố? |
| 7 | Báo giao (P5-8) | Không bắt buộc | Bắt buộc với NCC có tài khoản Portal? |
| 8 | DNT (Y8-1) | Một phiếu một PO | Cần một DNT gộp nhiều PO cùng NCC? |
| 9 | DNT (Y8-5) | Chuỗi 4 cấp cho mọi số tiền | Có ngưỡng tiền để rút gọn chuỗi? |
| 10 | Quyền lập DNT (Y8-8) | NVCUVT lập | Hay kế toán / bộ phận đặt hàng lập? |
| 11 | Định mức tạp vụ (Y11-3) | Tính theo số tiền đã chi gồm thuế; vượt không chặn | Có chặn? Ai duyệt phần vượt (đang GĐ TC-NQ-KT)? |
| 12 | Người lập 2C (Y11-5) | Vai trò DENGHI | Đúng người lập? |
| 13 | In 1C (Y11-9) | Không in định mức; một 1C gộp được bảng kê nhiều tháng | Khi có mẫu thật thì đối chiếu |
| 14 | TTPO (Y9) | Chỉ theo PO, không gồm 1C | Cần báo cáo riêng cho tạp vụ? |
| 15 | ACCP (Y9-6) | Cấu trúc 5 sheet tự đặt | Cần khớp mẫu nhập của phần mềm kế toán nào? |

## 7. Việc còn lại sau 0.27.0

- BGH "chờ nhận" tự đóng khi PO nhận đủ / hoàn thành (đã đồng ý, chưa làm).
- **P6b**: trạng thái thanh toán trên Portal cho NCC.
- **0.28.0**: K3 gỡ hẳn code + bảng kho (cần xác nhận sao lưu), P2 NCC tự quản lý hồ sơ / đăng ký, P7 bảng giá, tin nhắn, thẻ điểm.
- Mẫu 1C/2C thật khi công ty gửi → sửa in, trường dữ liệu, quy tắc Y11.
- Trên bản test 5007: áp "Cấu hình duyệt mẫu" CUVT cho DNT / BKT / TVB nếu muốn thử chuỗi nhiều cấp.

## 8. Quay lại bản trước (tóm tắt – chi tiết `CHANGELOG.md`)

Cách an toàn: thay lại gói 0.24.0 + khôi phục bản sao lưu CSDL trước khi nâng. Gỡ migration về `V0_25_0_TaiKhoanPortalNcc` sẽ **mất** báo giá /
file / hỏi đáp / thư Portal, phản hồi đơn hàng, phiếu báo giao, đề nghị thanh toán, bảng kê tạp vụ.
