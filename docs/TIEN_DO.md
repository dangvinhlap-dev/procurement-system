# Tiến độ dự án

Cập nhật: 02/11/2026 (C1). Đọc file này đầu mỗi phiên để biết đang ở đâu; cập nhật sau mỗi lát (CLAUDE.md mục 0).

## Đã xong (đã commit, push, merge vào `main`)

| Phần | Nội dung | Merge |
| --- | --- | --- |
| Giai đoạn 0 | Khung giao diện, đăng nhập + công ty làm việc, phân quyền + phạm vi, người dùng/vai trò, nhật ký, đánh số chứng từ, Docker | 2ef17fa … 1fb997f |
| Giai đoạn 1 | Danh mục công ty/kho, hàng hóa, NCC, NCC–công ty–sản phẩm, nhập/xuất Excel | 8d80f0f … 31718fb |
| Giai đoạn 2 | Kỳ kho, chứng từ kho, ghi sổ, trả NCC, chuyển kho, tồn kho + thẻ kho | f404824 … 8a63474 |
| Giai đoạn 3–4 | Đóng kỳ, giá vốn, NXT; mua bán nội bộ | 668a96e, 569b561 |
| Giai đoạn 5–6 | Mua hàng (PR, duyệt, PO); giá và đấu thầu | 3acd25b, c51b5c9 |
| Giai đoạn 7 | 7a–7g: báo cáo kho, báo cáo mua hàng, xếp loại NCC, in PDF, trang chủ, xuất kế toán, hiệu năng | b433f06 … 7609e3a |
| Phát hành | v0.1.0, v0.5.0, v0.7.0, v0.8.0, v0.9.0, v0.10.0, v0.11.0 (bản mới nhất 01/11/2026: A2, A3, B1, B2, giao diện ERP cổ điển; script `db/scripts/0.11.0.sql`, gói Windows trên GitHub Release https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.11.0) | tag |
| UI-a, UI-b | Giao diện SAP Fiori Horizon: theme; Shellbar + Dynamic Page | 0baa4ca, ae24f71 |
| UI-ERP | Giao diện **ERP desktop cổ điển** chính thức từ 01/11/2026 (người dùng duyệt bản thử): `docs/ui-style.md`, component `src/Proc.Web/src/classic/` (ô ngày gõ/lịch, co giãn cột, icon đỏ/xanh lá/vàng, tab in hoa), màn hình mẫu `/thu-giao-dien/tao-po`; **toàn bộ màn hình** đổi qua khung dùng chung (`theme.ts`, `classic/classic-app.css`, `LineGrid` co giãn cột, `DATE_INPUT_FORMATS` cho mọi ô ngày); ui-guide.md (Fiori) chỉ còn là lịch sử | 75a4724 (ghi chú commit nhầm "Lát B2"), 8b3d50f, 3ac74b2, nhánh feature/thu-giao-dien-erp |
| DM-1, DM-2, DM-3 | Sửa danh mục theo quyền (trừ mã); đánh số danh mục tự động theo dải; dải mặc định khi nâng cấp | eed25dc, 6d177b1, 1cda3a5 |
| TB-1 | Thông báo: chuông Shellbar, màn hình NOTI, báo duyệt / ICR chờ nhận / mở lại kỳ | d1a4550 |
| SEC-1 | Tham số hệ thống: bảng `CompanyParameter`, số NCC tối thiểu khi gửi RFQ theo công ty (sửa được); giá vốn, kho cho tồn âm (chỉ xem) | feature/tham-so-he-thong |
| SEC-2 | % markup nội bộ mặc định (tham số công ty, màn hình SEC); gợi ý giá bán nội bộ = giá vốn kỳ đã đóng gần nhất × (1 + %) ở Bảng giá nội bộ và phiếu ICS; migration `V0_10_0_MarkupNoiBo` | feature/markup-noi-bo |
| E1 | Dữ liệu mua lịch sử (PHIS): nhập Excel tối đa 50.000 dòng/20 MB (kiểm tra thử, COPY nhị phân), tự khớp mã vật tư/MST, lọc lô/ngày/khớp, xóa lô có nhật ký; quyền `Analysis.PurchaseHistory.*`; migration `V0_10_0_DuLieuMuaLichSu`; design.md 18.1. Sửa đọc số Excel: "2.5" = 2,5 | feature/du-lieu-mua-lich-su |
| E2 | Ánh xạ dữ liệu mua (PHMAP): mã vật tư cũ → mã hàng chung, NCC cũ (MST/tên không dấu) → NCC; gợi ý trùng gần đúng (Dice trên từ không dấu), lưu ánh xạ gán mọi dòng cùng mã + áp cho lần nhập sau, bỏ ánh xạ quay về tự khớp, nhật ký; migration `V0_10_0_AnhXaDuLieuMua`; design.md 18.2 | feature/anh-xa-du-lieu-mua |
| E3 | Phân tích giá hợp nhất (PHAN): gom mặt hàng chung × ĐVT giữa các pháp nhân – SL, giá trị, BQGQ, thấp/cao, giá gần nhất, chênh lệch %, NCC giá tốt nhất, giá mục tiêu (giảm X% từ BQGQ thấp nhất), tiết kiệm dự kiến; ngăn kéo từng pháp nhân; quyền mới `Analysis.PurchaseHistory.ViewAllCompanies`; không migration; design.md 18.3 | feature/phan-tich-hop-nhat |
| A1 | Đính kèm file dùng chung: NCC (tab Hồ sơ đính kèm), bảng giá, RFQ, PR, PO, mọi phiếu kho (tab Đính kèm (n)); thư mục máy chủ `yyyy/MM/{Id}{đuôi}`, tối đa 20 MB, chỉ đuôi tài liệu/ảnh/nén, quyền theo đối tượng chủ, xóa mềm giữ file, nhật ký; Docker volume `attachments` + backup chép bổ sung; migration `V0_10_0_DinhKemFile`; design.md 18.4 | feature/dinh-kem-file |
| WIN | Triển khai cả Docker và Windows/IIS không sửa code: múi giờ VN có tên Windows dự phòng (`VietnamTime.Zone` dùng chung); log ra file (Serilog.Sinks.File, có sẵn qua Serilog.AspNetCore); cảnh báo khi thư mục đính kèm nằm trong thư mục ứng dụng; Docker đính kèm ở `/data/attachments`; `web.config` (gỡ WebDAV, 30 MB); `deploy/windows/publish.ps1`, `backup.ps1`, cấu hình mẫu; vite/playwright/perf đọc biến môi trường; `docs/deploy-windows.md`; quy tắc CLAUDE.md mục 3 | feature/trien-khai-windows |
| A2 | Danh mục bộ phận theo công ty (màn hình DEPT, quyền `Organization.Department.View/Manage`); PR **bắt buộc** chọn bộ phận (yêu cầu người dùng 01/11/2026); cột chữ cũ ngừng dùng, hiển thị chữ cũ cho PR cũ; migration `V0_11_0_DanhMucBoPhan`; design.md 18.5 | feature/danh-muc-bo-phan |
| A3 | Quy trình duyệt dùng chung: loại duyệt PRL, RFQ, NCC, CTR + quyền duyệt riêng từng loại; `IApprovalSubject` cho cơ chế duyệt; APRL chọn 6 loại (PRL, NCC ngưỡng = 0); bộ vai trò mẫu Ban CUVT (9 vai trò) ở màn hình Vai trò. Nguyên tắc người dùng chốt: ai duyệt và công ty nào duyệt do phân quyền, số cấp do người dùng cấu hình; không migration; design.md 18.6 | feature/duyet-dung-chung |
| B2 | Duyệt NCC hai chặng (yêu cầu người dùng): công ty liên quan (đang mua NCC) duyệt xong – 2 công ty thì cả 2 – rồi Tổng công ty duyệt cuối; đổi trạng thái Đã duyệt/Danh sách đen, đổi tài khoản ngân hàng thành `SupplierChangeRequest`, duyệt xong mới áp; màn hình SUPAPR, cảnh báo trên ngăn kéo NCC, Việc cần làm/thông báo; `ApprovalStep.Stage`; migration `V0_11_0_DuyetNhaCungCap`; Testing thêm tài khoản `duyetncc.tct`; design.md 18.6 | feature/duyet-ncc |
| B4c | **Nhập Excel hợp đồng hiện hữu** (nút Nhập Excel trên danh sách CTR, quyền `Purchase.Contract.Manage`): mỗi dòng một mặt hàng × ĐVT, gộp theo Số HĐ giấy; cột đầu ghi ở dòng đầu (dòng sau trống hoặc giống hệt); nhập thành hợp đồng **Nháp** của công ty đang làm việc qua `ContractService.CreateAsync` (dùng chung giao dịch của file), rồi gửi duyệt như lập tay; số HĐ giấy đã có cùng NCC thì lỗi; kiểm tra thử/nhập thật, lỗi theo dòng Excel; API `/api/contracts/import-template`, `/api/contracts/import`; không migration; design.md 18.7 | feature/nhap-hop-dong-excel |
| B5a | **Kê khai xung đột lợi ích** (màn hình COI, Giá và NCC → Xung đột lợi ích): `ConflictDeclaration` người dùng × NCC, không xóa/sửa, kết thúc kèm lý do (quyền `MasterData.Conflict.Manage`); ai cũng tự kê khai, `MasterData.Conflict.View` xem toàn bộ (KSNB, Trưởng ban); kê khai còn hiệu lực chặn duyệt PO/PRL/NCC/CTR/CTA/RFQ (không báo, ẩn khỏi Việc cần làm) và chặn nhập báo giá, chọn thầu RFQ có NCC đó; migration `V0_12_0_KeKhaiXungDotLoiIch`; design.md 18.8 | feature/xung-dot-loi-ich |
| C1 | **PO theo giá đã duyệt**: dòng PO có nguồn giá (kết quả chọn thầu → hợp đồng nguyên tắc hiệu lực → bảng giá → ngoài hợp đồng), giá đã duyệt, số RFQ/HĐ/bảng giá; lệch giá (so sau CK) bắt buộc lý do khi gửi, PO thêm chặng duyệt ngoại lệ loại **POX** (APRL, luôn ≥ 1 cấp); cờ Ngoài hợp đồng/Ngoại lệ giá trên PO + cột Nguồn giá ở danh sách; trang PO cột Giá đã duyệt (tam giác vàng), Nguồn giá, Lý do lệch giá, nút "Áp giá đã duyệt"; API `/api/purchase-orders/approved-prices`; dòng chưa có giá chỉ gắn cờ; migration `V0_12_0_NguonGiaPo`; design.md 18.10 | feature/po-gia-da-duyet |
| B5b | **Phê duyệt NCC độc quyền** (màn hình NDQ, Giá và NCC → NCC độc quyền): `SoleSourceApproval` công ty × NCC × mặt hàng, giải trình, hạn đánh giá lại ≤ 12 tháng; Nháp → Chờ duyệt → Đã duyệt theo quy trình loại NDQ (người lập không tự duyệt, vướng kê khai xung đột không duyệt), kết thúc sớm, đánh giá lại; không trùng thời gian; RFQ mời thiếu NCC mà mọi dòng có phiếu còn tác dụng thì tự ghi giải trình; quyền `MasterData.SoleSource.View/Manage/Approve`; migration `V0_12_0_NccDocQuyen`; design.md 18.9 | feature/ncc-doc-quyen |
| B4b | **Phụ lục hợp đồng** (tab Phụ lục + ngăn kéo trên CTR): gia hạn, đổi bảng giá (giá dòng lấy lại theo bảng mới, giá trị dự kiến sau phụ lục), đổi điều khoản/hạn mức; điền sẵn giá trị hiện hành, hiện "Hiện hành: …" khi đổi; chỉ cho hợp đồng hiệu lực, một phụ lục mở mỗi lúc; loại duyệt riêng **CTA**, duyệt cấp cuối áp vào hợp đồng cùng giao dịch; chặn chấm dứt khi có phụ lục mở; thông báo/Việc cần làm mở `/mua-hang/hop-dong-khung/phu-luc/<Id>`; `DetailDrawer` thêm `formName` (tránh trùng id ô); migration `V0_12_0_PhuLucHopDong`; design.md 18.7 | feature/phu-luc-hop-dong |
| B4a | **Hợp đồng nguyên tắc** phương án 2 (màn hình CTR thay trang tạm): mỗi công ty một hợp đồng với NCC, gắn bảng giá dùng chung (toàn tập đoàn hiệu lực hoặc của công ty), điều khoản (điều chỉnh giá, thanh toán, giao hàng, chiết khấu, tài trợ, chế tài, khác), hạn mức công nợ; sản lượng dự kiến – giá lấy từ bảng giá, SL cam kết không bắt buộc, giá trị dự kiến xét ngưỡng duyệt CTR; bộ phận được phép đặt; Nháp → Chờ duyệt → Hiệu lực → Chấm dứt; NCC phải Đã duyệt, không chồng hiệu lực cùng công ty × NCC, duyệt cần bảng giá hiệu lực; cảnh báo ≤ 30 ngày; đính kèm, Việc cần làm CTR; migration `V0_12_0_HopDongNguyenTac`; Testing thêm `duyethd.bha`; design.md 18.7 | feature/hop-dong-nguyen-tac |
| B3b | RFQ: tab **Hồ sơ trình (x/7)** – bảng so sánh tự đánh dấu, 6 mục đánh dấu tay + ghi chú, sửa khi Nháp/Đang nhận báo giá, khóa khi gửi duyệt; thiếu mục không chặn nhưng hiện "đủ x/7, còn thiếu…" cho hội đồng (thông báo + hộp thoại duyệt); nút **Tạo bảng giá** – bảng giá NCC nháp cho từng NCC trúng thầu từ RFQ đã duyệt (giá/CK/VAT theo báo giá, hiệu lực tới hết hiệu lực báo giá), mỗi NCC một bảng; migration `V0_12_0_HoSoTrinhThau`; design.md 8.6 | feature/ho-so-trinh-thau |
| B3a | RFQ: mời ít hơn số NCC tối thiểu gửi được khi có **giải trình**; chọn thầu → **Chờ duyệt kết quả** qua quy trình duyệt dùng chung loại RFQ (xét giá trị trúng thầu), người lập RFQ và người chọn thầu không tự duyệt (`IApprovalSubject.IsPreparedBy`), từ chối bắt buộc lý do về Đang nhận báo giá, rút về; giá trúng thầu vào lịch sử giá, tạo PO chỉ sau khi duyệt; Việc cần làm loại RFQ, tab Lịch sử duyệt; migration `V0_12_0_DuyetKetQuaChonThau`; Testing thêm `duyetthau.bha`; design.md 8.6, 18.6 | feature/duyet-chon-thau |
| B1 | Duyệt bảng giá NCC: Nháp → Chờ duyệt → Hiệu lực qua cơ chế duyệt dùng chung (tại công ty lập, quy trình PRL), người lập không tự duyệt, từ chối bắt buộc lý do về nháp, rút về nháp; bỏ "Đưa vào hiệu lực"; bảng toàn tập đoàn chưa duyệt công ty khác không thấy; cảnh báo sắp hết hiệu lực (≤ 30 ngày) ở danh sách và phiếu; Việc cần làm loại PRL; hộp thoại từ chối dùng chung `confirmReject`; migration `V0_11_0_DuyetBangGiaNcc`; Testing thêm `duyetgia.tct`; design.md 8.5, 18.6 | feature/duyet-bang-gia |
| UI-c | Dấu * đỏ sau nhãn ở mọi ô bắt buộc (form, ngăn kéo, hộp thoại, tiêu đề cột lưới dòng), ẩn khi chỉ xem | feature/dau-bat-buoc |
| Tối ưu Claude Code | CLAUDE.md gọn (124 → 95 dòng), tách `docs/phat-hanh.md`, `docs/moi-truong-dev.md`; `docs/TIEN_DO.md`; `.claude/settings.json`; `.gitignore` | chore/toi-uu-claude |

Kiểm tra gần nhất (UI-ERP, 01/11/2026): `dotnet test` 441 qua; Playwright **71/71** (chạy trên Vite mới cổng 5175 bằng `E2E_WEB_URL` vì Vite
cổng 5173 chạy từ trước bản 0.10.0 còn báo "Phiên bản 0.9.0" – khởi động lại Vite 5173 là hết); `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (B1, 01/11/2026): `dotnet test` 441 qua; Playwright 65/68 – 2 lỗi "Phiên bản 0.10.0" (máy chủ Vite cũ vẫn giữ cổng
[::1]:5173 nên Playwright dùng lại – tắt hẳn Vite cũ hoặc khởi động lại máy), `supplier-links` chập chờn (chạy riêng cùng `pricing` 2/2 qua);
`npm run build`, `tsc`, `oxlint` qua. Test tích hợp dựng bảng giá hiệu lực bằng `ProcApiFactory.FinishPriceListApprovalAsync`, e2e bằng
`approvePriceList`. Lưu ý: test tích hợp không dùng tài khoản `admin` để tạo công ty (AuthTests đếm 4 công ty của admin) – `SupplierApprovalTests`
(B2) đang dùng admin tạo công ty, chưa lỗi nhờ thứ tự chạy, nên sửa khi đụng tới.

Kiểm tra gần nhất (B4c, 01/11/2026): `dotnet test` 455 qua; Playwright 72/74 cả bộ – 2 lỗi "Phiên bản 0.11.0" do Vite cũ giữ cổng
[::1]:5173 (PID ẩn, `netstat` báo PID 0); chạy lại trên Vite mới cổng 5178 (`E2E_WEB_URL`) auth/dashboard/classic-preview 10/10 qua;
`npm run build`, `tsc`, `oxlint` qua. Khởi động lại máy để giải phóng cổng 5173.

Kiểm tra trước (B4b, 01/11/2026): `dotnet test` 453 qua; Playwright 71/73 – vẫn 2 lỗi "Phiên bản 0.11.0" do Vite dev cũ
(PID giữ cổng [::1]:5173); `npm run build`, `tsc`, `oxlint` qua. Build Api lỗi khóa `*.dswa.cache.json` thì xóa các file đó rồi build lại.

Kiểm tra trước (B4a, 01/11/2026): `dotnet test` 450 qua; Playwright 70/72 – vẫn 2 lỗi "Phiên bản 0.11.0" do Vite dev cũ;
`npm run build`, `tsc`, `oxlint` qua. Lưu ý chạy API Testing nền bằng PowerShell phải dùng đường dẫn tuyệt đối `--project
D:\project\procurement-system\src\Proc.Api` (thư mục làm việc của lệnh nền khác thư mục dự án).

Kiểm tra trước (B3b, 01/11/2026): `dotnet test` 445 qua; Playwright 69/71 – vẫn 2 lỗi "Phiên bản 0.11.0" do Vite dev cũ (như B3a);
`npm run build`, `tsc`, `oxlint` qua.

Kiểm tra gần nhất (C1, 02/11/2026): `dotnet test` 466 qua; Playwright 76/76 cả bộ (e2e pricing: chờ URL rời `/moi` sau lưu phiếu mới và
chờ trạng thái "Chờ duyệt" thay vì thông báo – tránh chập chờn khi chạy song song); `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (B5b, 02/11/2026): `dotnet test` 461 qua (sửa 2 test `AuthTests` phụ thuộc thứ tự chạy); Playwright 75/76 cả bộ +
`document-numbering` chạy lại qua (đếm thêm loại NDQ); `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (B5a, 01/11/2026): `dotnet test` 458 qua; Playwright 75/75 qua; `npm run build`, `tsc`, `oxlint` qua. Thêm endpoint thì dừng API
Testing, `dotnet build`, bật lại rồi mới chạy e2e (API chạy `--no-build` không có endpoint mới).

Kiểm tra trước (B3a, 01/11/2026): `dotnet test` 443 qua; Playwright 69/71 – 2 lỗi "Phiên bản 0.11.0" do máy chủ Vite dev đang chạy từ
bản 0.9.0 (khởi động lại `npm run dev` là hết); `npm run build`, `tsc`, `oxlint` qua. Test tích hợp dựng RFQ đã chọn thầu bằng
`ProcApiFactory.FinishRfqAwardApprovalAsync`, e2e duyệt bằng `approveRfqAward` (tài khoản `duyetthau.bha`).

Kiểm tra trước (B2, 01/11/2026): `dotnet test` 438 qua; Playwright 66/68 – 2 lỗi "Phiên bản 0.10.0" do Vite dev cũ; `npm run build`, `tsc`,
`oxlint` qua. Test tích hợp dựng NCC "Đã duyệt" bằng `ProcApiFactory.FinishSupplierApprovalAsync`, e2e bằng `approveSupplier` (inventoryHelpers).

Kiểm tra trước (A3, 01/11/2026): `dotnet test` 432 qua; Playwright 66/68 – 2 lỗi "Phiên bản 0.10.0" do Vite dev cũ (như A2);
`npm run build`, `tsc`, `oxlint` qua. Lưu ý: build có lúc lỗi khóa `src/Proc.Api/obj/Debug/net10.0/*.dswa.cache.json` → xóa các file đó rồi build lại.

Kiểm tra trước (A2, 01/11/2026): `dotnet test` 428 qua; Playwright 62/66 cả bộ – 2 lỗi "Phiên bản 0.10.0" do máy chủ Vite dev đang chạy
từ trước bản 0.10.0 (phiên bản đọc lúc Vite khởi động – khởi động lại `npm run dev`), `purchasing` đã sửa, `supplier-links` chập chờn; chạy lại
4 test liên quan 5/5 qua; `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra gần nhất (WIN, 01/11/2026): `dotnet test` 416 qua; Playwright 64/65 lần chạy cả bộ – `supplier-links` hết thời gian chờ khi chạy song song,
chạy riêng 3/3 qua (chập chờn, theo dõi); `npm run build`, `tsc`, `oxlint` qua. Đã chạy thử bản publish Production bằng `Proc.Api.exe`
(health, giao diện, đính kèm, PUT/DELETE, log file, cảnh báo, `backup.ps1`); **chưa thử qua IIS** (máy dev chưa có Hosting Bundle).
Đã sửa test `SupplierPricingTests` lỗi vào ngày mùng 1 (số bảng giá theo ngày hiệu lực = hôm qua).

## Đang làm

- Không có. C1 (PO theo giá đã duyệt) đã xong. Lát kế tiếp **C1b** (chặn PR/PO ngoài bộ phận được phép đặt của hợp đồng – cần thêm
  bộ phận cho PO; ghi nhận mua theo NCC độc quyền trên PO – design.md 18.10 dòng "Chưa làm"), rồi **C2** – lập kế hoạch ngắn trước khi code.
  Từ nay mọi màn hình làm theo docs/ui-style.md.
- Đang chờ người dùng: hồ sơ trình thiếu mục hiện **không chặn** gửi duyệt (mặc định an toàn) – đổi thành chặn nếu người dùng yêu cầu.

## Việc tiếp theo

1. **Theo đề xuất Ban Cung ứng** (đã chốt 01/11/2026 – `docs/Phantich_01112026.md` mục 5–6, design.md mục 18): làm lần lượt
   E1 → E2 → E3 → A1 → A2 → A3 → B1 … → F4. E1, E2, E3, A1, A2, A3, B1, B2, B3a, B3b, B4a, B4b, B4c, B5a, B5b, C1 đã xong; lát kế tiếp: **C1b**, rồi C2.
   Bản 0.10.0 đã phát hành 01/11/2026 (SEC-2, đợt E, A1, WIN).
2. Hợp đồng nguyên tắc (CTR): B4a, B4b (phụ lục), B4c (nhập Excel) xong (design.md 18.7). PO lấy giá theo hợp đồng: C1 xong (18.10);
   chặn bộ phận ngoài phạm vi hợp đồng: C1b.
3. **Không xóa** dữ liệu/CSDL nào (`procurement_perf`, `procurement_perf_mau`, `procurement_kiemtra_090` – CSDL thử script 0.9.0, `procurement_kiemtra_windows` – CSDL thử bản publish Windows, `procurement_kiemtra_0100` – CSDL thử script 0.10.0 và bản cài thử, `procurement_kiemtra_0110` – CSDL thử chuỗi script 0.1.0 → 0.11.0,
   file `D:\project\procurement_perf_nguon.dump`) cho đến khi người dùng chỉ định.
4. **Lỗi tiềm ẩn đăng xuất**: bấm Quay lại trong vòng ~40ms sau khi đăng xuất (router còn đang chuyển trang) để lại trang trắng ở `/`
   (không lộ dữ liệu, phiên đã xóa). Có từ trước, lộ ra khi giao diện cổ điển tắt hiệu ứng; test `auth.spec.ts` đã chờ form đăng nhập. Cần sửa.
5. Test Playwright `supplier-links` chập chờn khi chạy cả bộ song song (hết thời gian ở bước chọn mặt hàng tìm từ server) – cần sửa cho ổn định.
6. Việc "làm khi có yêu cầu" ghi trong design.md (gửi email, ngoại tệ, cổng NCC, gộp PR mua tập trung…).

## Ghi nhớ môi trường dev

- API dev: `dotnet run --no-build --project src/Proc.Api --no-launch-profile --urls http://localhost:5000` (ASPNETCORE_ENVIRONMENT=Development);
  Web: `npm run dev` trong `src/Proc.Web` (cổng 5173). Đăng nhập dev: `admin` / `Admin@2026`.
- Bản cài thử 0.11.0 (chế độ Production, từ gói Release): `D:\project\procurement-thu-0.11.0`, chạy ở http://localhost:5003, CSDL
  `procurement_kiemtra_0110` (chạy đủ script 0.1.0 → 0.11.0, dữ liệu khởi tạo), đăng nhập `admin` / `Admin@2026`;
  dừng: `Stop-Process -Id (Get-Content D:\project\procurement-thu-0.11.0\pid.txt)`.
- Bản cài thử 0.10.0 (chế độ Production, như cài Windows): `D:\project\procurement-thu-0.10.0`, chạy ở http://localhost:5002, CSDL
  `procurement_kiemtra_0100`, đăng nhập `admin` / `Admin@2026`; dừng: `Stop-Process -Id (Get-Content D:\project\procurement-thu-0.10.0\pid.txt)`.
- Build bị khóa DLL khi API đang chạy → dừng API trước khi build; chạy Playwright khi API môi trường Testing đã bật sẵn (`--no-build`).
