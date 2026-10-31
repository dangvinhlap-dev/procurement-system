# Nhật ký thay đổi

Ghi theo [Semantic Versioning](https://semver.org/lang/vi/) – xem CLAUDE.md mục 8.
Mỗi bản phát hành có Git tag `vx.y.z`.

## [Chưa phát hành]

### Thay đổi
- Giao diện chuyển sang quy chuẩn SAP Fiori, theme Horizon – lát UI-a (docs/ui-guide.md, design.md mục 17): chữ 14px, bảng màu Horizon
  (chữ #1d2d3d, nền #f5f6f7, màu nhấn #0a6ed1, màu trạng thái ngữ nghĩa), bo góc 4/8px, đổ bóng nhẹ, khoảng cách 8/16px, dòng bảng 32px,
  thanh tab gạch chân, khối nội dung/form bo góc; màn hình đăng nhập dạng thẻ. Giữ React + Ant Design, bố cục khung giữ nguyên (lát UI-b
  sẽ đổi sang Shellbar + Dynamic Page). CLAUDE.md mục 9 nạp toàn văn docs/ui-guide.md mỗi phiên.

## [0.8.0] - 2026-10-31

### Thêm mới
- Xuất Excel mọi danh sách, báo cáo (design.md mục 11.2): nút "Xuất Excel" xuất đúng các cột đang hiện và các dòng đã lọc ra file
  .xlsx (ô số, ô ngày đúng kiểu; dòng tiêu đề cố định, có lọc). API `POST /api/excel/grid`.
- Báo cáo Hàng chậm luân chuyển (SLOW), Hàng sắp hết hạn (EXP), Tồn tập đoàn (GSTK – tồn một mặt hàng ở mọi công ty, chỉ số lượng,
  gợi ý mua nội bộ). Quyền mới `Report.Stock.ViewAllCompanies`. API `/api/reports/slow-moving`, `/api/reports/expiring`,
  `/api/reports/group-stock`.
- Báo cáo mua hàng (design.md mục 11.3): PR chưa xử lý (PRPEND), Mua hàng theo NCC / nhóm hàng / mặt hàng / công ty (RPSUP),
  Đánh giá NCC – tỷ lệ trả, tỷ lệ giao đúng hạn (SUPEVAL), Tiết kiệm từ đấu thầu (SAVE). Quyền mới `Report.Purchase.ViewAllCompanies`.
  API `/api/reports/pending-requests`, `/api/reports/purchase-spend`, `/api/reports/supplier-evaluation`, `/api/reports/sourcing-savings`.
- Xếp loại nhà cung cấp theo ngưỡng do người dùng cấu hình (design.md mục 11.3): màn hình Hệ thống → Xếp loại NCC (RATE) thêm, sửa,
  xóa hạng (mã, thứ tự, % đúng hạn tối thiểu, % trả hàng tối đa); báo cáo Đánh giá NCC có cột Xếp loại. API `/api/supplier-rating-rules`.
- In phiếu PDF (design.md mục 10.10): PO, PR, phiếu nhập mua, trả NCC, chuyển kho, xuất bán / nhập mua nội bộ – nút "In phiếu" (Ctrl+P)
  trên mọi phiếu đã lưu, có số tiền bằng chữ và ô ký tên. Thư viện PDFsharp-MigraDoc 6.2.4 (MIT, ghim bản – bản mới hơn là AGPL).
  API `GET /api/purchase-orders/{id}/pdf`, `/api/purchase-requests/{id}/pdf`, `/api/inventory-documents/{id}/pdf`.
- Trang chủ (design.md mục 11.4): ô số liệu của công ty đang làm việc theo quyền – phiếu chờ duyệt, PR chưa xử lý, PO chờ giao/trễ,
  RFQ, chứng từ kho nháp, hàng dưới tồn tối thiểu, lô hết hạn/sắp hết hạn, hàng đi đường, kỳ kho; mua hàng tháng này so với tháng trước,
  top 5 NCC. Bấm ô mở màn hình chi tiết. API `GET /api/dashboard`.
- Xuất chứng từ cho kế toán (design.md mục 11.5): màn hình Báo cáo → Xuất cho kế toán (ACCX) xem trước chứng từ kho đã ghi sổ và tải file
  Excel (sheet Chứng từ, Tổng hợp, Hướng dẫn) có mã NCC tại công ty, MST, hóa đơn, TK kho theo nhóm hàng, giá vốn chính thức/tạm tính.
  Quyền mới `Report.Accounting.Export`. API `/api/accounting-export/documents`, `/api/accounting-export/file`.

### Thay đổi
- Hiệu năng (design.md mục 14.6, đo trên 200.000 dòng sổ kho): đóng kỳ từ 39 giây còn 7–10 giây (ghi giá vốn một lượt, bỏ quét lặp);
  báo cáo NXT từ 1,2 giây còn 0,3 giây. Script đo ở `db/perf/`.
- Số đếm trên cây điều hướng là số thật (trước đây là dữ liệu mẫu), tự cập nhật; bỏ số thông báo giả trên thanh menu.
- Image Docker cài thêm phông `fonts-dejavu-core` để in PDF tiếng Việt. Máy chủ không dùng Docker cần có Arial, DejaVu Sans hoặc
  Liberation Sans, hoặc khai báo thư mục phông ở `Printing:FontDirectories`.

### Sửa lỗi
- Đóng kỳ, báo cáo NXT chậm khi sổ kho lớn (quét lặp cả sổ kho cho từng mặt hàng) – xem mục Thay đổi.

### Thay đổi CSDL
- Migration `V0_8_0_XepLoaiNcc`: bảng mới `SupplierRatingRule`. `Down()` xóa bảng – mất các hạng đã cấu hình (không ảnh hưởng chứng từ).

### Lưu ý khi nâng cấp
- **Có thay đổi CSDL – sao lưu trước khi nâng cấp** (1 migration mới `V0_8_0_XepLoaiNcc`, chỉ thêm bảng, không đổi dữ liệu cũ):
  `pg_dump -h localhost -U proc -F c -f procurement_truoc_0.8.0.dump procurement`.
- Môi trường Development tự áp migration khi chạy API. Môi trường chạy thật chạy script:
  `$env:PGCLIENTENCODING='UTF8'; psql -h <máy> -U <user> -d procurement -v ON_ERROR_STOP=1 -f db\scripts\0.8.0.sql`
  (script `--idempotent`, chạy lại không lỗi).
- Quyền mới (vai trò Quản trị tự có; vai trò khác gán tại Hệ thống → Vai trò nếu cần): `Report.Stock.ViewAllCompanies`,
  `Report.Purchase.ViewAllCompanies`, `Report.Accounting.Export`.
- Cấu hình hạng xếp loại NCC ở tổng công ty (Hệ thống → Xếp loại NCC) – chưa cấu hình thì báo cáo Đánh giá NCC chưa xếp loại.
- In PDF cần phông chữ tiếng Việt trên server: image Docker đã cài `fonts-dejavu-core`; server không dùng Docker cần Arial, DejaVu Sans hoặc
  Liberation Sans, hoặc khai báo thư mục phông ở `Printing:FontDirectories`.
- Thư viện PDFsharp-MigraDoc ghim ở 6.2.4 (bản MIT cuối cùng) – không nâng lên 6.5 trở đi (AGPL).

### Cách quay lại bản trước
- Code: không xóa lịch sử; tạo nhánh từ tag `git switch -c rollback/v0.7.0 v0.7.0`, build và chạy.
- CSDL (chỉ khi đã sao lưu): `dotnet ef database update V0_7_0_TimNguonDauThau -p src\Proc.Infrastructure -s src\Proc.Api`.
  **Mất dữ liệu**: các hạng xếp loại NCC đã cấu hình (không ảnh hưởng chứng từ). Muốn giữ dữ liệu thì khôi phục bản sao lưu:
  `pg_restore -h localhost -U proc -d procurement --clean procurement_truoc_0.8.0.dump`.

## [0.7.0] - 2026-10-31

### Thêm mới
- Mua hàng (design.md mục 6.2, 6.7, 10.2, 10.3): quy trình duyệt nhiều cấp theo ngưỡng giá trị và vai trò (Hệ thống → Quy trình
  duyệt); đề nghị mua hàng (PR) với giá tham khảo là giá nhập gần nhất; đơn mua hàng (PO) lấy dòng từ PR đã duyệt, chiết khấu, VAT,
  dung sai nhận hàng; gửi duyệt, duyệt, từ chối có lý do; phiếu nhập mua theo PO (giá thực mua sau chiết khấu, chặn nhận vượt);
  PO tự đóng khi nhận đủ; Việc cần làm (phiếu chờ tôi duyệt); Theo dõi giao hàng. API `/api/approval-rules`,
  `/api/approvals/pending`, `/api/purchase-requests`, `/api/purchase-orders`.
- Bảng giá NCC (design.md mục 8.1, 8.5): bảng giá theo công ty hoặc toàn tập đoàn (tổng công ty lập), bậc số lượng, chiết khấu,
  VAT; Nháp → Hiệu lực → Ngừng. Đơn mua hàng gợi ý giá theo bảng giá (cột "Giá bảng giá", nút "Áp giá bảng giá").
  API `/api/price-lists`.
- So sánh giá (design.md mục 8.2): theo mặt hàng – giá thực mua theo công ty × NCC, thấp nhất/cao nhất/bình quân 12 tháng, tô màu
  công ty mua cao hơn giá thấp nhất quá X%; giá bảng giá đang hiệu lực; lịch sử giá 12 tháng. Xem toàn tập đoàn cần quyền
  `Report.Price.ViewAllCompanies`. API `/api/price-comparison`.
- Tìm nguồn và đấu thầu (design.md mục 8.3, 8.4, 8.6): phiếu tìm nguồn với NCC ứng viên; RFQ (từ PR đã duyệt hoặc nhập tay, trọng số
  5 tiêu chí, mời ≥ 3 NCC); nhập báo giá từng NCC; bảng so sánh đánh dấu giá thấp nhất, chấm điểm theo trọng số; chọn thầu theo dòng
  (không chọn giá thấp nhất phải ghi lý do); tạo PO nháp từ kết quả; giá trúng thầu vào lịch sử giá. Màn hình Tìm nguồn, Yêu cầu
  báo giá, So sánh báo giá. API `/api/sourcing`, `/api/rfqs`.

### Thay đổi
- Danh sách đơn mua hàng `/mua-hang/po` hiện dữ liệu thật; danh sách 30 dòng mẫu dùng so bố cục chuyển sang `/mua-hang/po/mau`.
- Khi mở một phiếu, cây điều hướng đánh dấu màn hình danh sách cha của phiếu.
- Khuôn phiếu `DocumentForm` cho đặt nhãn tab dòng và tab mở sẵn.

### Sửa lỗi
- Giá tham khảo trên đề nghị mua hàng lấy cả giá nhập của công ty khác; nay chỉ lấy giá nhập của công ty đang làm việc.

### Thay đổi CSDL
- Migration `V0_6_0_MuaHang`: bảng `ApprovalRule`, `ApprovalStep`, `PurchaseRequest`, `PurchaseRequestLine`, `PurchaseOrder`,
  `PurchaseOrderLine`; cột `InventoryDocument.PurchaseOrderId`, `InventoryDocumentLine.PurchaseOrderLineId`. `Down()` xóa các
  bảng và cột – mất quy trình duyệt, lịch sử duyệt, mọi PR, PO và liên kết phiếu nhập ↔ PO.
- Migration `V0_7_0_BangGiaNcc`: bảng `SupplierPriceList`, `SupplierPriceListLine`. `Down()` xóa hai bảng – mất mọi bảng giá NCC.
- Migration `V0_7_0_TimNguonDauThau`: bảng `SourcingRequest`, `SourcingCandidate`, `Rfq`, `RfqLine`, `RfqSupplier`, `QuotationLine`.
  `Down()` xóa các bảng – mất phiếu tìm nguồn, RFQ, báo giá, kết quả chọn thầu (PO đã tạo vẫn giữ).
- Danh mục loại chứng từ thêm `PRL` (bảng giá NCC) và `SRC` (tìm nguồn) – không đổi bảng.

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước khi nâng cấp** (bản này có 3 migration mới: `V0_6_0_MuaHang`, `V0_7_0_BangGiaNcc`, `V0_7_0_TimNguonDauThau`):
  `pg_dump -h localhost -U proc -F c -f procurement_truoc_0.7.0.dump procurement`.
- Môi trường Development tự áp migration khi chạy API. Môi trường chạy thật chạy script:
  `$env:PGCLIENTENCODING='UTF8'; psql -h <máy> -U <user> -d procurement -v ON_ERROR_STOP=1 -f db\scripts\0.7.0.sql`
  (script `--idempotent`, chạy lại không lỗi).
- Không có bản 0.6.0 riêng: phần mua hàng (giai đoạn 5) phát hành cùng bản này.
- Sau khi nâng cấp: cấu hình quy trình duyệt (Hệ thống → Quy trình duyệt) cho từng công ty nếu cần duyệt nhiều cấp – chưa cấu hình
  thì PR/PO một cấp, ai có quyền duyệt cũng duyệt được. Gán quyền mới cho vai trò nếu cần: `Purchase.PR.*`, `Purchase.PO.*`,
  `Pricing.PriceList.*`, `Pricing.Compare.View`, `Sourcing.Rfq.View/Manage/Award`, `Report.Price.ViewAllCompanies`.

### Cách quay lại bản trước
- Code: không xóa lịch sử; tạo nhánh từ tag `git switch -c rollback/v0.5.0 v0.5.0`, build và chạy.
- CSDL (chỉ khi đã sao lưu): `dotnet ef database update V0_5_0_MuaBanNoiBo -p src\Proc.Infrastructure -s src\Proc.Api`.
  **Mất dữ liệu**: quy trình duyệt, lịch sử duyệt, PR, PO, liên kết phiếu nhập ↔ PO, bảng giá NCC, phiếu tìm nguồn, RFQ, báo giá.
  Muốn giữ dữ liệu thì khôi phục bản sao lưu: `pg_restore -h localhost -U proc -d procurement --clean procurement_truoc_0.7.0.dump`.

## [0.5.0] - 2026-10-30

### Thêm mới
- Màn hình Hệ thống → Công ty (COMP) và Hệ thống → Kho (WH) theo khuôn danh sách + ngăn kéo chi tiết (design.md mục 5.1, 5.6):
  thêm công ty kèm kho tổng ở tổng công ty, người thêm được gán vào công ty mới; sửa thông tin, mã số thuế, ngày bắt đầu,
  ngừng dùng; thêm kho phụ, đổi kho cha (chặn vòng lặp), thủ kho, địa chỉ, cho phép tồn âm, ngừng dùng.
- Màn hình Giá và NCC → Hàng hóa (ITEM), Nhóm hàng (CAT), Đơn vị tính (UOM) (design.md mục 5.2, 5.7): hàng hóa có loại hàng,
  nhóm hàng, ĐVT cơ sở, quy cách, thương hiệu, xuất xứ, mã vạch, thuế suất mặc định, quản lý lô/hạn dùng, tồn tối thiểu/tối đa,
  quy đổi ĐVT, thông số kỹ thuật; tìm không dấu ở server, lọc theo nhóm (gồm nhóm con) và loại hàng; nhóm hàng dạng cây có
  tài khoản kho mặc định. Thêm, sửa ở tổng công ty; hàng hóa lọc theo phạm vi nhóm hàng.
- API `/api/uoms`, `/api/item-categories` (thêm POST, PUT; GET trả thêm đường dẫn, cấp, tài khoản kho), `/api/items`.
- Trùng mã duy nhất trong CSDL (kể cả bản ghi ngoài phạm vi người dùng) trả 409 kèm thông báo tiếng Việt thay vì lỗi hệ thống.
- Màn hình Giá và NCC → Nhà cung cấp (SUP) và Nhóm NCC (SGRP) (design.md mục 5.3, 5.8): NCC có nhóm, mã số thuế không trùng
  (trong nước/nước ngoài), liên hệ, điều khoản thanh toán, ngành hàng, xếp hạng, trạng thái (danh sách đen phải có lý do),
  NCC nội bộ liên kết công ty thành viên; tab Người liên hệ và Ngân hàng. API `/api/supplier-groups`, `/api/suppliers`.
- NCC áp dụng cho công ty và NCC – sản phẩm (design.md mục 5.9): tab "Sản phẩm/dịch vụ" và "Áp dụng tại …" trong ngăn kéo NCC,
  tab "Nhà cung cấp" trong ngăn kéo hàng hóa; mỗi mặt hàng một NCC ưu tiên; danh sách NCC lọc "NCC công ty đang mua".
  API `/api/suppliers/{id}/company`, `/api/suppliers/{id}/items`, `/api/supplier-items/{id}`, `/api/items/{id}/suppliers`.
- Nhập/xuất Excel (ClosedXML) cho đơn vị tính, nhóm hàng, hàng hóa, nhóm NCC, nhà cung cấp (design.md mục 5.10): nút
  "Xuất Excel" và "Nhập Excel" trên danh sách; file mẫu có hướng dẫn; kiểm tra thử báo lỗi theo dòng, nhập thật chỉ lưu khi
  không có lỗi. API `GET /api/excel/{danh-mục}`, `POST /api/excel/{danh-mục}/import?commit=`.
- Kỳ kho (design.md mục 7.6): màn hình Hệ thống → Kỳ kho (PER) – tạo lịch năm (13 kỳ), mở kỳ tháng tuần tự; quyền mới
  `Inventory.Period.Open`; kiểm tra "ngày chứng từ thuộc kỳ đang mở" cho ghi sổ. API `/api/inventory-periods`.
- Chứng từ kho (design.md mục 6.4): khuôn chung `DocumentForm` (thanh lệnh, trạng thái, đầu phiếu 3 cột, lưới dòng nhập như
  Excel – Enter sang ô kế, F2 thêm dòng, Ctrl+S lưu – chân phiếu tổng tiền); màn hình Kho → Nhập mua NCC (GRN): danh sách theo
  khoảng ngày và phiếu nhập nháp (cấp số khi lưu, tính SL quy đổi, thành tiền, VAT, hủy phiếu nháp). API `/api/goods-receipts`,
  `/api/inventory-documents`, `/api/items/lookup`, `/api/suppliers/lookup`.
- Ghi sổ, bỏ ghi sổ phiếu nhập mua (design.md mục 12.3): sổ kho `StockLedger`, tồn tức thời `StockOnHand` cập nhật trong
  cùng giao dịch, kiểm tra kỳ đang mở, chặn tồn âm; nút Ghi sổ (tự lưu nháp trước) và Bỏ ghi sổ trên phiếu. API
  `POST /api/inventory-documents/{id}/post`, `/unpost`.
- Màn hình Kho → Xuất trả NCC (RTV) và Kho → Chuyển kho nội bộ (TRF) (design.md mục 6.5, 10.5, 10.6): phiếu trả chọn phiếu
  nhập gốc đã ghi sổ, chọn dòng nhập, hiện số trả tối đa, nút "Lấy mọi dòng còn trả được", đơn giá theo giá nhập gốc; phiếu
  chuyển chọn kho xuất/kho nhập cùng công ty, hiện tồn tại kho xuất và cảnh báo khi vượt tồn. Ghi sổ, bỏ ghi sổ cả hai loại
  (TRF theo giá vốn tạm tính). Danh sách chứng từ kho dùng chung cho GRN, RTV, TRF. API `/api/goods-returns`,
  `/api/stock-transfers`, `GET /api/goods-receipts/{id}/returnable`, `GET /api/stock-on-hand/available`.
- Màn hình Kho → Tồn kho hiện tại (STK) và Kho → Thẻ kho (CARD) (design.md mục 11.1): tồn theo kho × mặt hàng × lô, giá trị
  và đơn giá bình quân theo sổ kho, cảnh báo dưới mức tối thiểu/tồn âm, lọc kho, nhóm hàng, dòng tồn 0; bấm đúp mở thẻ kho của
  mặt hàng với tồn đầu, phát sinh, tồn lũy kế, tồn cuối; bấm đúp phát sinh mở phiếu gốc. API `GET /api/stock-on-hand`,
  `GET /api/stock-card`.
- Đóng kỳ kho, giá vốn bình quân gia quyền cuối kỳ, mở lại kỳ (design.md mục 7.2–7.4a): kiểm tra kỳ trước đã đóng và không còn
  phiếu nháp; khóa ghi sổ khi đang tính giá; gán giá vốn chính thức cho phiếu chuyển kho; chốt số dư theo kho × hàng × lô; mở lại
  kỳ đã đóng gần nhất kèm lý do. Nút Đóng kỳ, Mở lại kỳ trên màn hình Kỳ kho. API `POST /api/inventory-periods/{id}/close`,
  `/reopen`.
- Báo cáo nhập – xuất – tồn (NXT) theo kỳ tháng, kho × mặt hàng (design.md mục 11.1). API `GET /api/reports/stock-movement`.
- Thẻ kho, báo cáo NXT đọc tồn đầu từ số dư kỳ đã đóng thay vì cộng từ đầu sổ kho.
- Mua bán nội bộ (design.md mục 6.3, 6.6): bảng giá nội bộ theo công ty bán (giá riêng/giá chung, hiệu lực); phiếu xuất bán nội
  bộ (ICS) chỉ từ kho tổng, giá lấy từ bảng giá; ghi sổ ICS tự sinh phiếu nhập mua nội bộ (ICR) chờ nhận ở công ty mua; bên mua
  xác nhận SL thực nhận (nhận thiếu) rồi ghi sổ theo giá bán nội bộ; chặn bỏ ghi sổ ICS khi bên mua đã nhận; báo cáo hàng đi đường
  và đối chiếu nội bộ. API `/api/internal-prices`, `/api/group-companies`, `/api/intercompany-sales`, `/api/intercompany-receipts`,
  `/api/reports/goods-in-transit`, `/api/reports/intercompany-reconciliation`.
- Dữ liệu mẫu dev/test: 7 đơn vị tính, 3 nhóm NCC (Trong nước, Nhập khẩu, Nội bộ); lịch kỳ năm hiện tại, mở tháng hiện tại.
- API `GET/POST /api/companies`, `GET/PUT /api/companies/{id}`, `POST /api/warehouses`, `GET/PUT /api/warehouses/{id}`;
  `GET /api/warehouses` trả thêm kho cha, địa chỉ, thủ kho, cho phép tồn âm.

### Thay đổi
- Thanh menu cập nhật danh sách công ty ngay sau khi thêm/sửa công ty (không phải đăng nhập lại).
- Không bỏ ghi sổ được phiếu nhập mua khi đã có phiếu trả NCC ghi sổ theo nó.
- Cảnh báo "dưới mức tối thiểu" ở Tồn kho hiện tại so với tổng tồn của mặt hàng trên mọi kho của công ty, không theo kho
  đang lọc – lọc riêng kho phụ hay thủ kho một kho không còn bị báo động sai.

### Sửa lỗi
- Ngăn kéo chi tiết không đóng bằng Esc khi vừa chọn xong một mục trong ô chọn (danh sách thả xuống còn đang ẩn dần).
- Build còn 41 cảnh báo thiếu ghi chú XML ở danh mục quyền (`Permissions.cs`); nay build không còn cảnh báo.
- Bỏ ký tự BOM ở đầu `db/scripts/0.1.0.sql` – `psql` báo lỗi cú pháp ở dòng đầu khi chạy file có BOM.

### Thay đổi CSDL
- Migration `V0_2_0_CongTyKho`: chỉ mục duy nhất mã số thuế công ty (bỏ qua công ty chưa có mã số thuế).
  `Down()` chỉ bỏ chỉ mục, không mất dữ liệu.

- Migration `V0_2_0_HangHoa`: bảng `Uom`, `Item`, `ItemUomConversion`; cột `InventoryAccount` của `ItemCategory`.
  `Down()` xóa ba bảng và cột đó – mất toàn bộ hàng hóa, đơn vị tính, quy đổi đã nhập.
- Migration `V0_2_0_NhaCungCap`: bảng `SupplierGroup`, `Supplier`, `SupplierContact`, `SupplierBankAccount`.
  `Down()` xóa bốn bảng – mất toàn bộ NCC đã nhập.
- Migration `V0_2_0_NccCongTySanPham`: bảng `SupplierCompany`, `SupplierItem`. `Down()` xóa hai bảng – mất danh sách NCC
  từng công ty đang mua và mặt hàng từng NCC bán.
- Migration `V0_3_0_KyKho`: bảng `InventoryPeriod`. `Down()` xóa bảng – mất lịch kỳ và trạng thái kỳ.
- Migration `V0_3_0_ChungTuKho`: bảng `InventoryDocument`, `InventoryDocumentLine`. `Down()` xóa hai bảng – mất mọi chứng từ kho.
- Migration `V0_3_0_SoKho`: bảng `StockLedger`, `StockOnHand`. `Down()` xóa hai bảng – mất sổ kho và số tồn.
- Migration `V0_3_0_TraNccChuyenKho`: cột `Receiver`, `LinkedDocumentId` (phiếu nhập gốc của phiếu trả) ở `InventoryDocument`,
  `SourceLineId` ở `InventoryDocumentLine`. `Down()` xóa ba cột – mất người nhận và liên kết phiếu trả ↔ phiếu nhập.
- Migration `V0_4_0_DongKyGiaVon`: bảng `StockPeriodBalance`, `ItemPeriodCost`, cột `InventoryPeriod.ReopenReason`. `Down()` xóa
  hai bảng và cột – mất số dư, giá vốn các kỳ đã đóng và lý do mở lại kỳ (nên mở lại các kỳ đã đóng trước khi gỡ).
- Migration `V0_5_0_MuaBanNoiBo`: bảng `InternalPrice`, cột `InventoryDocument.CounterCompanyId`. `Down()` xóa bảng và cột – mất
  bảng giá nội bộ và công ty đối ứng trên phiếu bán/nhập nội bộ.

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước khi nâng cấp** (bản này có 10 migration mới, từ `V0_2_0_CongTyKho` đến `V0_5_0_MuaBanNoiBo`):
  `pg_dump -h localhost -U proc -F c -f procurement_truoc_0.5.0.dump procurement`.
- Môi trường Development tự áp migration khi chạy API (`MigrateOnStartup: true`). Môi trường chạy thật
  (`MigrateOnStartup: false`) chạy script: `$env:PGCLIENTENCODING='UTF8'; psql -h <máy> -U <user> -d procurement -v ON_ERROR_STOP=1 -f db\scripts\0.5.0.sql`
  (script `--idempotent`, chạy lại không lỗi; phải đặt bảng mã UTF8 vì `psql` trên Windows mặc định WIN1252).
- Nếu đã có hai công ty trùng mã số thuế thì migration lỗi – sửa dữ liệu trước khi nâng cấp.
- Sau khi nâng cấp: tạo lịch kỳ kho và mở kỳ tháng (Hệ thống → Kỳ kho) cho từng công ty trước khi ghi sổ chứng từ kho; khai báo
  bảng giá nội bộ (Giá và NCC → Bảng giá nội bộ) trước khi lập phiếu xuất bán nội bộ.
- Vai trò có quyền mới cần gán lại cho người dùng nếu muốn dùng: `Inventory.Period.Open/Close/Reopen`, `Inventory.Stock.View`,
  `Report.Stock.View`, `Pricing.PriceList.View/Manage`.

### Cách quay lại bản trước
- Code: không xóa lịch sử; tạo nhánh từ tag `git switch -c rollback/v0.1.0 v0.1.0`, build và chạy.
- CSDL (chỉ khi đã sao lưu): `dotnet ef database update V0_1_0_DanhSoChungTu -p src\Proc.Infrastructure -s src\Proc.Api`.
  **Mất dữ liệu**: toàn bộ hàng hóa, NCC, kỳ kho, chứng từ kho, sổ kho, tồn kho, số dư, giá vốn kỳ, bảng giá nội bộ (các bảng thêm
  từ 0.2.0 đến 0.5.0). Muốn giữ dữ liệu thì khôi phục bản sao lưu: `pg_restore -h localhost -U proc -d procurement --clean procurement_truoc_0.5.0.dump`.

## [0.1.0] - 2026-10-30

### Thêm mới
- Khung solution theo docs/design.md mục 13.1: Proc.Api, Proc.Application, Proc.Domain,
  Proc.Infrastructure (.NET 10), Proc.Web (React 19 + TypeScript + Vite + Ant Design + TanStack Query),
  Proc.Domain.Tests, Proc.IntegrationTests.
- Kết nối PostgreSQL qua EF Core 10 + Npgsql (`ProcDbContext`, chưa có bảng).
- Endpoint `GET /health` kiểm tra kết nối CSDL, trả trạng thái và số phiên bản.
- Log kỹ thuật bằng Serilog (ghi ra console).
- Test kiến trúc (Domain/Application không tham chiếu hạ tầng) và test tích hợp `/health`
  trên database `procurement_test`.
- `Directory.Build.props` (phiên bản 0.1.0), `Directory.Packages.props` (quản lý gói tập trung),
  `global.json` (dùng Microsoft.Testing.Platform cho `dotnet test`), công cụ local `dotnet-ef`.
- Khung giao diện theo docs/ui-guide.md: `AppShell` (thanh menu có Alt + chữ gạch chân, thanh công cụ
  có ô lệnh, cây điều hướng có nhóm Ghim và số đếm, kéo rộng/ẩn được, thanh trạng thái hiện phiên bản),
  `ListPage` (tìm kiếm có/không dấu, bộ lọc, Nhóm theo, chọn cột, xuất CSV) và `DataGrid`
  (sắp xếp, kéo đổi rộng/thứ tự cột, kéo cột để nhóm, trạng thái chấm màu, phím mũi tên/Enter,
  cuộn ảo trên 500 dòng).
- Màn hình Danh sách đơn mua hàng (PO) với 30 dòng dữ liệu giả.
- Test giao diện Playwright (`npm run test:e2e` trong `src\Proc.Web`).
- Đăng nhập (design.md mục 9.4): ASP.NET Core Identity + JWT 8 giờ, khóa tạm 15 phút sau 5 lần sai,
  thông báo lỗi tiếng Việt; màn hình Đăng nhập; mọi API bắt buộc đăng nhập trừ `/health`.
- Danh mục Công ty, Vai trò và gán vai trò theo công ty (UserCompanyRole); chọn công ty làm việc
  trên thanh menu, gửi kèm mọi request qua header `X-Company-Id`; menu người dùng có Đăng xuất.
- Tự điền CreatedAt/By, UpdatedAt/By khi lưu danh mục.
- Phân quyền (design.md mục 9.1, 9.2): danh mục 42 quyền dạng Module.Đối tượng.Hành động, quyền theo vai trò,
  vai trò ADMIN luôn đủ quyền; phạm vi kho và nhóm hàng (kế thừa nhóm con) theo từng công ty;
  `ICurrentUserScope` nạp theo header `X-Company-Id` (công ty không được gán → 403);
  global query filter theo công ty/kho/nhóm hàng, mặc định từ chối khi chưa nạp phạm vi.
- Danh mục Kho (Main/Sub, mỗi công ty một kho tổng) và Nhóm hàng (dạng cây) ở mức dữ liệu; API
  `GET /api/warehouses`, `GET /api/item-categories`.
- Đổi mật khẩu (`POST /api/auth/change-password`, hộp thoại trong menu người dùng); thông báo lỗi mật khẩu tiếng Việt.
- Giao diện ẩn màn hình không có quyền trên menu, cây điều hướng, ô lệnh; mở thẳng đường dẫn thì báo không có quyền.
- Dữ liệu mẫu dev/test: kho cho 4 công ty, cây nhóm hàng, vai trò Thủ kho và Nhân viên mua hàng, 2 tài khoản mẫu.
- Màn hình Người dùng và Vai trò và phân quyền (design.md mục 9.5, 10.9): danh sách + ngăn kéo chi tiết có tab;
  tạo tài khoản, sửa thông tin, ngừng dùng, đặt lại mật khẩu (mở khóa tạm); gán vai trò, kho, nhóm hàng tại công ty
  đang làm việc; tạo/sửa vai trò và tick quyền theo module. Chặn tự nâng quyền: chỉ gán vai trò và cấp quyền nằm trong
  quyền của chính mình, không tự sửa quyền của mình, vai trò ADMIN không sửa được.
- API `/api/users`, `/api/roles`, `/api/permissions`; lỗi không tìm thấy trả 404 tiếng Việt.
- Component dùng chung `DetailDrawer` (ngăn kéo chi tiết của form danh mục, Ctrl+S lưu, Esc đóng).
- Nhật ký thao tác (design.md mục 9.3): mọi thay đổi của entity `[Auditable]` (công ty, vai trò, quyền của vai trò,
  kho, nhóm hàng, gán vai trò, phạm vi, tài khoản) tự ghi vào `AuditLog` trong cùng giao dịch, kèm người, công ty,
  IP, trình duyệt, mã liên kết request và giá trị cũ/mới; mã băm mật khẩu chỉ ghi `***`.
- Nhật ký truy cập: đăng nhập, đăng nhập sai, bị khóa tạm, đăng xuất, đổi mật khẩu, đổi công ty làm việc
  (API `POST /api/auth/logout`, `POST /api/auth/working-company`).
- Màn hình Nhật ký thao tác (lọc khoảng ngày, loại đối tượng, hành động; xem trước/sau) và Nhật ký truy cập;
  API `GET /api/audit-logs`, `GET /api/audit-logs/{id}`, `GET /api/access-logs`.
- Đánh số chứng từ (design.md mục 5.5): mẫu số theo công ty và loại chứng từ (mặc định `{LOẠI}-{CTY}-{YYMM}-{0000}`,
  ví dụ PO-BHA-2610-0001), đếm lại theo tháng/năm của ngày chứng từ; cấp số nguyên tử trong giao dịch của chứng từ
  (không trùng khi lưu đồng thời, lưu lỗi thì trả lại số). Màn hình Hệ thống → Đánh số chứng từ; API `/api/document-numbering`.
- Triển khai server bằng Docker (design.md mục 14): `Dockerfile` (một image chứa API + giao diện React đã build,
  chạy bằng user không phải root), `docker-compose.yml` (Caddy HTTPS, app, PostgreSQL 18, Seq xem log, sao lưu
  `pg_dump` mỗi đêm vào `./backups` giữ 14 ngày), `Caddyfile`, `.env.example`; hướng dẫn cài và nâng cấp trong README.
- API phục vụ giao diện khi có `wwwroot` (đường dẫn của giao diện đều trả `index.html`, không lưu đệm `index.html`);
  `/api` không tồn tại trả 404. Log kỹ thuật gửi được sang Seq (gói `Serilog.Sinks.Seq`).

### Thay đổi
- Không có.

### Sửa lỗi
- Cùng một tài khoản đăng nhập đồng thời (nhiều tab/máy, có lần sai mật khẩu) có thể trả lỗi hệ thống 500 do xung đột
  ghi bộ đếm sai mật khẩu; nay tự đọc lại và thử lại.
- Script SQL dạng `--idempotent` lỗi "query has no destination for result data" ở migration `V0_1_0_NhatKyThaoTac`
  (lời gọi tạo phân vùng nằm trong khối DO); nay gọi qua `PERFORM`.

### Thay đổi CSDL
- Migration `V0_1_0_DangNhapVaCongTy`: bảng `Company`, `Role`, `UserCompanyRole`, `User`, `UserClaim`,
  `UserLogin`, `UserToken`. `Down()` xóa các bảng này cùng dữ liệu.
- Migration `V0_1_0_PhanQuyenVaPhamVi`: bảng `Permission`, `RolePermission`, `Warehouse`, `ItemCategory`,
  `UserWarehouseScope`, `UserItemCategoryScope`. `Down()` xóa các bảng này cùng dữ liệu.
- Migration `V0_1_0_NhatKyThaoTac`: bảng `AuditLog` (phân vùng theo tháng, phân vùng mặc định `AuditLog_default`,
  hàm `audit_log_ensure_partitions`) và `AccessLog`; trigger chặn UPDATE/DELETE trên cả hai bảng.
  `Down()` xóa hai bảng nhật ký cùng toàn bộ nhật ký đã ghi.
- Migration `V0_1_0_DanhSoChungTu`: bảng `DocumentNumbering` (mẫu số theo công ty) và `DocumentSequence` (bộ đếm theo
  công ty, loại chứng từ, kỳ). `Down()` xóa hai bảng – mất cấu hình mẫu số và số hiện tại.
- Script cài mới `db/scripts/0.1.0.sql` (gồm cả 4 migration trên, dạng idempotent – chạy lại nhiều lần không sao).

### Lưu ý khi nâng cấp
- Cần tạo sẵn database `procurement` và `procurement_test`, user `proc` (README.md).
- Server thật (cài mới bằng Docker, README.md mục "Triển khai server"): copy `.env.example` thành `.env`, đặt
  `JWT_SIGNING_KEY` (tối thiểu 32 ký tự), `ADMIN_PASSWORD`, mật khẩu CSDL và Seq; tạo bảng bằng `db/scripts/0.1.0.sql`
  trước khi khởi động app (dev/test tự chạy migration khi khởi động). Đăng nhập lần đầu rồi đổi mật khẩu quản trị ngay.
- Bản đầu tiên, chưa có dữ liệu cũ nên không cần sao lưu trước khi cài.
- API tạo sẵn phân vùng nhật ký cho tháng hiện tại và 2 tháng tới mỗi lần khởi động; server chạy liên tục
  nhiều tháng không khởi động lại thì nhật ký vào phân vùng mặc định (không lỗi) – sẽ thêm job hằng tháng.

### Cách quay lại bản trước
- Đây là bản đầu tiên, không có bản trước để quay lại. Gỡ hẳn: `docker compose down` (giữ dữ liệu) hoặc
  `docker compose down -v` (xóa luôn CSDL – chỉ làm khi đã sao lưu).
