# Hướng dẫn sử dụng – Phần mềm Mua hàng & Kho tập đoàn

Áp dụng cho **phiên bản 0.21.0** (09/11/2026). Viết ngày 10/11/2026.

Phần mềm quản lý nhà cung cấp (NCC), giá, mua hàng, đấu thầu và kho cho tổng công ty và các công ty thành viên. Mỗi công ty là một
pháp nhân độc lập: chứng từ, kho, kỳ kho, quy trình duyệt và tham số đều tách theo công ty; tổng công ty quản lý danh mục dùng chung
(hàng hóa, nhóm hàng, NCC) và các nghiệp vụ của Ban cung ứng vật tư.

## Mục lục

| # | Tài liệu | Dành cho |
| --- | --- | --- |
| 1 | [Làm quen với phần mềm](01-lam-quen.md) – đăng nhập, màn hình, phím tắt, tìm kiếm, duyệt phiếu, đính kèm, in, xuất Excel | Mọi người |
| 2 | [Danh mục](02-danh-muc.md) – công ty, kho, bộ phận, ĐVT, nhóm hàng, hàng hóa, nhóm NCC, NCC, hồ sơ NCC mới, duyệt NCC | Văn phòng Ban, quản trị danh mục |
| 3 | [Mua hàng](03-mua-hang.md) – kế hoạch nhu cầu, PR, PO, ngân sách, hợp đồng nguyên tắc, đặt hàng gộp | Bộ phận đề nghị, phòng mua, Phòng cung ứng |
| 4 | [Giá và nhà cung cấp](04-gia-va-ncc.md) – bảng giá, so sánh giá, duyệt NCC và giá tháng, khảo sát giá, chiết khấu sản lượng, đánh giá NCC | Văn phòng Ban, Ban cung ứng |
| 5 | [Tìm nguồn và đấu thầu](05-dau-thau.md) – phiếu tìm nguồn, RFQ, báo giá, chọn thầu, hồ sơ trình, thẩm định | Phòng cung ứng, Hội đồng |
| 6 | [Kho](06-kho.md) – nhập mua, trả NCC, chuyển kho, mua bán nội bộ, điều chỉnh/kiểm kê, tồn kho, kỳ kho và giá vốn | Thủ kho, kế toán kho |
| 7 | [Báo cáo](07-bao-cao.md) – báo cáo kho, mua hàng, KPI, kiểm soát nội bộ, báo cáo tháng cho Ban, xuất kế toán | Lãnh đạo, KSNB, kế toán |
| 8 | [Hệ thống và phân quyền](08-he-thong.md) – người dùng, vai trò, quy trình duyệt, tham số, đánh số, nhật ký | Quản trị hệ thống |
| 9 | [Quy trình mẫu từ đầu đến cuối và câu hỏi thường gặp](09-quy-trinh-mau.md) | Mọi người |

Cài đặt và vận hành máy chủ: xem [`docs/huong-dan-cai-dat.md`](../huong-dan-cai-dat.md).

## Bản đồ nghiệp vụ

```
 Kế hoạch nhu cầu (KHN) ──► Tổng hợp nhu cầu (KHNTH) ──► Duyệt NCC và giá tháng (GTH) ──► Đặt hàng gộp (DHG)
                                                                   │                              │
 Hồ sơ NCC mới (HSN) ──► Duyệt NCC (SUPAPR) ──► NCC Đã duyệt ──────┘                              ▼
                                                                                       PO từng công ty
 Đề nghị mua (PR) ──► [Tìm nguồn (SRC) ──► RFQ ──► chọn thầu ──► duyệt kết quả] ──► Đơn mua hàng (PO)
                                              │                                          │
                                              ├──► Bảng giá NCC (PRL)                     ▼
                                              └──► Hợp đồng nguyên tắc (CTR)     Nhập mua (GRN) ──► Trả NCC (RTV)
                                                                                         │
                                       Chuyển kho (TRF) · Xuất/nhập nội bộ (ICS/ICR) · Điều chỉnh (ADJ)
                                                                                         ▼
                                                    Đóng kỳ kho ──► Giá vốn ──► Xuất chứng từ cho kế toán (ACCX)
```

## Từ viết tắt

| Viết tắt | Nghĩa |
| --- | --- |
| NCC | Nhà cung cấp |
| ĐVT | Đơn vị tính (ĐVT cơ sở = đơn vị nhỏ nhất dùng tính tồn kho) |
| PR | Đề nghị mua hàng (Purchase Request) |
| PO | Đơn mua hàng (Purchase Order) |
| RFQ | Yêu cầu báo giá (Request for Quotation) |
| GRN / RTV / TRF | Phiếu nhập mua NCC / xuất trả NCC / chuyển kho nội bộ |
| ICS / ICR | Phiếu xuất bán nội bộ / nhập mua nội bộ (giữa hai công ty trong tập đoàn) |
| ADJ | Phiếu điều chỉnh, kiểm kê kho |
| GTH | Phiếu duyệt NCC và giá tháng |
| KSG | Phiếu khảo sát giá thị trường (ngân hàng giá) |
| CKB | Thỏa thuận chiết khấu bậc theo sản lượng gộp |
| KHN | Phiếu kế hoạch nhu cầu |
| DHG | Phiếu đặt hàng gộp |
| HSN | Hồ sơ đánh giá NCC mới |
| KSNB | Kiểm soát nội bộ |
| TĐ | Tập đoàn |
