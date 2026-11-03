# Tiến độ dự án

Cập nhật: 03/11/2026 (phát hành 0.15.0; lát A4 ghi chú + đính kèm mọi form; dọn main; phát hành 0.16.0; nhập bảng giá NCC bằng Excel; cảnh báo giá cao trên PO; hợp đồng từ kết quả thầu). Đọc file này đầu mỗi phiên để biết đang ở đâu; cập nhật sau mỗi lát (CLAUDE.md mục 0).

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
| C5a | **Kiểm tra chất lượng, sai lệch nhận hàng**: dòng phiếu nhập mua có SL chứng từ giao, SL không đạt (không nhập kho), kết quả kiểm tra, ghi chú (bắt buộc khi không đạt/thiếu thừa), cột Thiếu/thừa; tham số bật/tắt (mặc định tắt) "bắt buộc kiểm tra khi nhận hàng" và "người lập PO không ghi sổ nhận hàng của PO đó" (G4); báo cáo **Sai lệch nhận hàng** (QCDEV, % đạt chất lượng); ngăn kéo tham số có công tắc bật/tắt; migration `V0_13_0_KiemTraNhanHang`; design.md 18.14 | feature/nhan-hang-kiem-tra |
| C4 | **SLA mua hàng và hàng đang đặt**: tham số SLA ở SEC (hàng có HĐ/bảng giá 1, chưa có NCC duyệt 5, khẩn 1 ngày làm việc); báo cáo **SLA mua hàng** (màn hình SLA): loại yêu cầu tự xếp, hạn = ngày PR duyệt + N ngày làm việc (thứ 2–6), so với ngày PO đầu tiên được duyệt, trạng thái Đúng hạn/Trễ/Đang xử lý/Quá hạn, % đúng SLA; cột **Đang đặt** trên dòng PR (PO đã duyệt chưa nhận đủ); migration `V0_13_0_ThamSoSla`; design.md 18.13 | feature/sla-mua-hang |
| C2b | **Hàng ngoài danh mục trên PR**: tab "Hàng mới chưa có mã" (mô tả, quy cách, ĐVT, SL, giá dự kiến), duyệt cùng PR, không lên PO/RFQ; PR đã duyệt: nút Tìm nguồn lập phiếu tìm nguồn, nút Gắn mã sinh dòng PR có mã (lên PO được, tổng không đổi); người lập PR đọc được danh mục ĐVT; migration `V0_13_0_HangMoiChuaCoMa`; design.md 18.12 | feature/hang-ngoai-danh-muc |
| C2a | **Mua khẩn cấp** trên PO: Loại mua Thường/Khẩn cấp, lý do + hạn bổ sung hồ sơ (≤ 30 ngày, mặc định +7); duyệt theo bộ cấp loại **POK** nếu đã cấu hình (không thì cấp PO); không gửi duyệt PO khẩn lập lùi ngày; lệnh "Đã bổ sung hồ sơ" (ghi chú, người, thời điểm), cờ quá hạn ở đầu phiếu và cột Loại mua của danh sách; API `/api/purchase-orders/{id}/complete-docs`; migration `V0_13_0_MuaKhanCap`; design.md 18.11 | feature/mua-khan-cap |
| C1b | **Bộ phận đặt hàng trên PO** (tự lấy theo PR nguồn, không trộn PR nhiều bộ phận); hợp đồng giới hạn bộ phận được phép đặt thì PO bộ phận khác/chưa chọn không tính giá hợp đồng (ghi chú trên PO nháp, không chặn); dòng PO ghi phiếu **NCC độc quyền** còn tác dụng vào ngày PO (cột Nguồn giá); người lập PO đọc được danh mục bộ phận; migration `V0_13_0_BoPhanPoDocQuyen`; design.md 18.10 | feature/po-bo-phan-doc-quyen |
| C1 | **PO theo giá đã duyệt**: dòng PO có nguồn giá (kết quả chọn thầu → hợp đồng nguyên tắc hiệu lực → bảng giá → ngoài hợp đồng), giá đã duyệt, số RFQ/HĐ/bảng giá; lệch giá (so sau CK) bắt buộc lý do khi gửi, PO thêm chặng duyệt ngoại lệ loại **POX** (APRL, luôn ≥ 1 cấp); cờ Ngoài hợp đồng/Ngoại lệ giá trên PO + cột Nguồn giá ở danh sách; trang PO cột Giá đã duyệt (tam giác vàng), Nguồn giá, Lý do lệch giá, nút "Áp giá đã duyệt"; API `/api/purchase-orders/approved-prices`; dòng chưa có giá chỉ gắn cờ; migration `V0_12_0_NguonGiaPo`; design.md 18.10 | feature/po-gia-da-duyet |
| B5b | **Phê duyệt NCC độc quyền** (màn hình NDQ, Giá và NCC → NCC độc quyền): `SoleSourceApproval` công ty × NCC × mặt hàng, giải trình, hạn đánh giá lại ≤ 12 tháng; Nháp → Chờ duyệt → Đã duyệt theo quy trình loại NDQ (người lập không tự duyệt, vướng kê khai xung đột không duyệt), kết thúc sớm, đánh giá lại; không trùng thời gian; RFQ mời thiếu NCC mà mọi dòng có phiếu còn tác dụng thì tự ghi giải trình; quyền `MasterData.SoleSource.View/Manage/Approve`; migration `V0_12_0_NccDocQuyen`; design.md 18.9 | feature/ncc-doc-quyen |
| B4b | **Phụ lục hợp đồng** (tab Phụ lục + ngăn kéo trên CTR): gia hạn, đổi bảng giá (giá dòng lấy lại theo bảng mới, giá trị dự kiến sau phụ lục), đổi điều khoản/hạn mức; điền sẵn giá trị hiện hành, hiện "Hiện hành: …" khi đổi; chỉ cho hợp đồng hiệu lực, một phụ lục mở mỗi lúc; loại duyệt riêng **CTA**, duyệt cấp cuối áp vào hợp đồng cùng giao dịch; chặn chấm dứt khi có phụ lục mở; thông báo/Việc cần làm mở `/mua-hang/hop-dong-khung/phu-luc/<Id>`; `DetailDrawer` thêm `formName` (tránh trùng id ô); migration `V0_12_0_PhuLucHopDong`; design.md 18.7 | feature/phu-luc-hop-dong |
| B4a | **Hợp đồng nguyên tắc** phương án 2 (màn hình CTR thay trang tạm): mỗi công ty một hợp đồng với NCC, gắn bảng giá dùng chung (toàn tập đoàn hiệu lực hoặc của công ty), điều khoản (điều chỉnh giá, thanh toán, giao hàng, chiết khấu, tài trợ, chế tài, khác), hạn mức công nợ; sản lượng dự kiến – giá lấy từ bảng giá, SL cam kết không bắt buộc, giá trị dự kiến xét ngưỡng duyệt CTR; bộ phận được phép đặt; Nháp → Chờ duyệt → Hiệu lực → Chấm dứt; NCC phải Đã duyệt, không chồng hiệu lực cùng công ty × NCC, duyệt cần bảng giá hiệu lực; cảnh báo ≤ 30 ngày; đính kèm, Việc cần làm CTR; migration `V0_12_0_HopDongNguyenTac`; Testing thêm `duyethd.bha`; design.md 18.7 | feature/hop-dong-nguyen-tac |
| B3b | RFQ: tab **Hồ sơ trình (x/7)** – bảng so sánh tự đánh dấu, 6 mục đánh dấu tay + ghi chú, sửa khi Nháp/Đang nhận báo giá, khóa khi gửi duyệt; thiếu mục không chặn nhưng hiện "đủ x/7, còn thiếu…" cho hội đồng (thông báo + hộp thoại duyệt); nút **Tạo bảng giá** – bảng giá NCC nháp cho từng NCC trúng thầu từ RFQ đã duyệt (giá/CK/VAT theo báo giá, hiệu lực tới hết hiệu lực báo giá), mỗi NCC một bảng; migration `V0_12_0_HoSoTrinhThau`; design.md 8.6 | feature/ho-so-trinh-thau |
| B3a | RFQ: mời ít hơn số NCC tối thiểu gửi được khi có **giải trình**; chọn thầu → **Chờ duyệt kết quả** qua quy trình duyệt dùng chung loại RFQ (xét giá trị trúng thầu), người lập RFQ và người chọn thầu không tự duyệt (`IApprovalSubject.IsPreparedBy`), từ chối bắt buộc lý do về Đang nhận báo giá, rút về; giá trúng thầu vào lịch sử giá, tạo PO chỉ sau khi duyệt; Việc cần làm loại RFQ, tab Lịch sử duyệt; migration `V0_12_0_DuyetKetQuaChonThau`; Testing thêm `duyetthau.bha`; design.md 8.6, 18.6 | feature/duyet-chon-thau |
| B1 | Duyệt bảng giá NCC: Nháp → Chờ duyệt → Hiệu lực qua cơ chế duyệt dùng chung (tại công ty lập, quy trình PRL), người lập không tự duyệt, từ chối bắt buộc lý do về nháp, rút về nháp; bỏ "Đưa vào hiệu lực"; bảng toàn tập đoàn chưa duyệt công ty khác không thấy; cảnh báo sắp hết hiệu lực (≤ 30 ngày) ở danh sách và phiếu; Việc cần làm loại PRL; hộp thoại từ chối dùng chung `confirmReject`; migration `V0_11_0_DuyetBangGiaNcc`; Testing thêm `duyetgia.tct`; design.md 8.5, 18.6 | feature/duyet-bang-gia |
| HD-THAU | **Sinh hợp đồng nguyên tắc từ kết quả thầu**: nút "Tạo hợp đồng" trên RFQ đã duyệt (quyền `Purchase.Contract.Manage`) – mỗi NCC trúng thầu một hợp đồng nháp gắn bảng giá sinh từ RFQ (phải "Tạo bảng giá" trước), SL dự kiến = SL trúng thầu, hiệu lực theo bảng giá, thanh toán theo báo giá; hợp đồng đã hủy thì tạo lại được; cột Hợp đồng nguyên tắc ở bảng điểm NCC; RFQ ở tổng công ty thì "Tạo bảng giá" đánh dấu sẵn toàn tập đoàn (yêu cầu người dùng 03/11/2026); API `/api/rfqs/{id}/create-contracts`; migration `V0_17_0_HopDongTuKetQuaThau`; design.md 8.6 | feature/hop-dong-tu-ket-qua-thau |
| CB-GIA | **Cảnh báo giá cao trên PO**: cột "Giá thấp nhất TĐ" trên dòng PO (giá gần nhất 12 tháng thấp nhất giữa các công ty × NCC, như PCMP), tam giác vàng khi giá sau CK cao hơn quá X% – chỉ cảnh báo; tham số SEC `PO_HIGH_PRICE_WARN_PERCENT` (mặc định 10%); chi tiết công ty/NCC/số phiếu chỉ hiện khi có quyền xem giá toàn tập đoàn; API `/api/purchase-orders/group-min-prices`; sửa `netPrice` trang PO (chia sai 10⁹ → 10⁶); migration `V0_17_0_CanhBaoGiaCaoPo`; design.md 8.5 | feature/canh-bao-gia-cao-po |
| BG-X | **Nhập Excel bảng giá NCC** (nút Nhập Excel trên danh sách PRL, quyền `Pricing.PriceList.Manage`, theo mẫu B4c): mỗi dòng một bậc giá, gộp theo Mã NCC + Tên bảng giá; cột đầu bảng ở dòng đầu (dòng sau trống/giống hệt); nhập thành bảng **Nháp** của công ty đang làm việc qua `PriceListService.CreateAsync` (dùng chung giao dịch của file); bảng cùng NCC + tên + ngày hiệu lực chưa ngừng thì lỗi; API `/api/price-lists/import-template`, `/api/price-lists/import`; không migration; design.md 8.5 | feature/nhap-bang-gia-excel |
| A4 | **Ghi chú + đính kèm cho mọi form** (quy tắc người dùng 03/11/2026, ghi ở CLAUDE.md mục 4, design.md 18.4): đính kèm **ngay khi tạo** (hàng chờ trên trình duyệt, lưu xong tự tải lên – `usePendingAttachments`, `uploadTo`); giới hạn **2 MB/file** mặc định, cấu hình `Attachments:MaxFileSizeMb` 1–25 (Docker `ATTACHMENT_MAX_MB`), API `/api/attachments/limits`; thêm loại chủ + tab Đính kèm: phiếu tìm nguồn, phụ lục HĐ, đánh giá NCC, vi phạm KSNB, kê khai xung đột lợi ích (người kê khai tự đính kèm); NCC độc quyền đính kèm được khi lập; không migration | feature/ghi-chu-dinh-kem-moi-form |
| UI-c | Dấu * đỏ sau nhãn ở mọi ô bắt buộc (form, ngăn kéo, hộp thoại, tiêu đề cột lưới dòng), ẩn khi chỉ xem | feature/dau-bat-buoc |
| Tối ưu Claude Code | CLAUDE.md gọn (124 → 95 dòng), tách `docs/phat-hanh.md`, `docs/moi-truong-dev.md`; `docs/TIEN_DO.md`; `.claude/settings.json`; `.gitignore` | chore/toi-uu-claude |

Kiểm tra gần nhất (A4, 03/11/2026): `dotnet test` 514 qua; Playwright attachments + conflicts/contract/control/sole-sources/sourcing/supplier-review/purchasing/inventory 14/14; `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (UI-ERP, 01/11/2026): `dotnet test` 441 qua; Playwright **71/71** (chạy trên Vite mới cổng 5175 bằng `E2E_WEB_URL` vì Vite
cổng 5173 chạy từ trước bản 0.10.0 còn báo "Phiên bản 0.9.0" – khởi động lại Vite 5173 là hết); `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (B1, 01/11/2026): `dotnet test` 441 qua; Playwright 65/68 – 2 lỗi "Phiên bản 0.10.0" (máy chủ Vite cũ vẫn giữ cổng
[::1]:5173 nên Playwright dùng lại – tắt hẳn Vite cũ hoặc khởi động lại máy), `supplier-links` chập chờn (chạy riêng cùng `pricing` 2/2 qua);
`npm run build`, `tsc`, `oxlint` qua. Test tích hợp dựng bảng giá hiệu lực bằng `ProcApiFactory.FinishPriceListApprovalAsync`, e2e bằng
`approvePriceList`. Lưu ý: test tích hợp không dùng tài khoản `admin` để tạo công ty (AuthTests đếm 4 công ty của admin) – `SupplierApprovalTests`
(B2) đang dùng admin tạo công ty – **đã sửa 03/11/2026** (cùng `ChangeHistoryTests`, `SharedCatalogEditTests`).

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

Kiểm tra gần nhất (C5a, 02/11/2026): `dotnet test` 475 qua; Playwright 77/77 cả bộ (system-parameters: 9 tham số; purchasing: chọn Đạt khi
nhận hàng); `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (C4, 02/11/2026): `dotnet test` 473 qua; Playwright 77/77 cả bộ (e2e pricing: tải lại trang trước khi gõ lý do lệch giá –
xem lỗi nạp đè form ở mục Đang làm; e2e system-parameters: 7 tham số); `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (C2b, 02/11/2026): `dotnet test` 471 qua; Playwright 77/77 cả bộ (e2e pricing: chờ thông báo lỗi gửi duyệt tới 20 giây khi
máy bận); `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (C2a, 02/11/2026): `dotnet test` 469 qua; Playwright 76/76 cả bộ; `npm run build`, `tsc`, `oxlint` qua. Lưu ý e2e: ô Select
không có tìm kiếm (ví dụ Loại mua) thì bấm ô rồi `pickOption`, không dùng `chooseOption` (gõ chữ).

Kiểm tra trước (C1b, 02/11/2026): `dotnet test` 467 qua; Playwright 76/76 cả bộ (e2e pricing chờ đúng phản hồi PUT khi lưu lần hai);
`npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (C1, 02/11/2026): `dotnet test` 466 qua; Playwright 76/76 cả bộ (e2e pricing: chờ URL rời `/moi` sau lưu phiếu mới và
chờ trạng thái "Chờ duyệt" thay vì thông báo – tránh chập chờn khi chạy song song); `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (B5b, 02/11/2026): `dotnet test` 461 qua (sửa 2 test `AuthTests` phụ thuộc thứ tự chạy); Playwright 75/76 cả bộ +
`document-numbering` chạy lại qua (đếm thêm loại NDQ); `npm run build`, `tsc`, `oxlint` qua.

Kiểm tra trước (B5a, 01/11/2026): `dotnet test` 458 qua; Playwright 75/75 qua; `npm run build`, `tsc`, `oxlint` qua. Thêm endpoint thì dừng API
Testing, `dotnet build`, bật lại rồi mới chạy e2e (API chạy `--no-build` không có endpoint mới).

Kiểm tra trước (B3a, 01/11/2026): `dotnet test` 443 qua; Playwright 69/71 – 2 lỗi "Phiên bản 0.11.0" do máy chủ Vite dev đang chạy từ
bản 0.9.0 (khởi động lại `npm run dev` là hết); `npm run build`, `tsc`, `oxlint` qua. Test tích hợp dựng RFQ đã chọn thầu bằng
`ProcApiFactory.FinishRfqAwardApprovalAsync`, e2e duyệt bằng `approveRfqAward` (tài khoản `duyetthau.bha`).

Kiểm tra gần nhất (C6, 02/11/2026): `dotnet test` 477 qua; Playwright 79/79 qua; `npm run build`, `tsc`, `oxlint` qua. Ảnh:
`e2e/screenshots/three-way-match.png`, `grn-update-invoice.png`.

Kiểm tra trước (C5b, 02/11/2026): `dotnet test` 475 qua; Playwright 78/78 qua; `npm run build`, `tsc`, `oxlint` qua. Mẫu biên bản PDF
lưu ở `src/Proc.Web/e2e/screenshots/bien-ban-giao-nhan.pdf` khi chạy e2e `printing`.

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

- **Kế hoạch người dùng duyệt 03/11/2026**: (1) dọn `main` → (2) phát hành 0.16.0 có A4 (chờ người dùng xác nhận phát hành) →
  (3) nhập bảng giá NCC bằng Excel → (4) cảnh báo giá cao trên PO → (5) sinh hợp đồng nguyên tắc từ kết quả thầu.
  - **Bước 1 (xong, nhánh `chore/don-main`)**: commit tài liệu hướng dẫn `docs/HuongDan_*.docx` (duyệt PO nhiều cấp), `docs/Bancungung.*`;
    test tích hợp `SupplierApprovalTests`, `ChangeHistoryTests`, `SharedCatalogEditTests` thôi dùng `admin` tạo công ty (người quản trị
    riêng của test); `.gitignore` bỏ `/test-results/`. Kiểm tra: `dotnet build`, `dotnet test` 514/514.
  - **Bước 2 (xong) – phát hành 0.16.0 (03/11/2026)**: tag `v0.16.0`, https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.16.0
    (gói `procurement-0.16.0.zip`); không đổi CSDL (49 migration), không có script 0.16.0. **Bản test cổng 5007 đã nâng tại chỗ lên 0.16.0**:
    thư mục vẫn tên `D:\project\procurement-thu-0.15.0` (`app` = 0.16.0, `app-0.15.0` = bản cũ để quay lại), CSDL `procurement_kiemtra_0150`,
    sao lưu trước nâng `D:\project\procurement_kiemtra_0150_truoc_0.16.0.dump`; PID trong `pid.txt`. Bản 0.12.0/0.13.0/0.14.0 vẫn chạy.
    Lúc đóng gói: Vite cũ cổng 5179/5180 (phiên e2e trước) khóa `node_modules` làm `npm ci` lỗi EPERM – người dùng cho dừng; dừng Vite
    trước khi chạy `publish.ps1`. Kiểm tra bằng `Get-Process`/`Get-CimInstance` (netstat lọc theo cổng dễ sót).
  - **Bước 3 (xong, nhánh `feature/nhap-bang-gia-excel`)**: nhập Excel bảng giá NCC (xem bảng Đã xong, dòng BG-X). Test: tích hợp
    `SupplierPricingTests.NhapExcel_BangGia_*` (2), e2e `pricing.spec.ts` "hộp thoại nhập Excel". Kiểm tra: `dotnet build`, `dotnet test` 516/516;
    Playwright pricing + contract 6/6; `npm run build`, `tsc`, `oxlint` qua.
  - **Bước 4 (xong, nhánh `feature/canh-bao-gia-cao-po`)**: cảnh báo giá cao trên PO (bảng Đã xong, dòng CB-GIA). Test: tích hợp
    `CanhBaoGiaCaoPo_*`, e2e `pricing.spec.ts` bước 3b; `system-parameters.spec.ts` đếm 20 tham số. Kiểm tra: `dotnet build`, `dotnet test` 517/517; Playwright pricing +
    purchasing + system-parameters 6/6; `npm run build`, `tsc`, `oxlint` qua.
  - **Bước 5 (xong, nhánh `feature/hop-dong-tu-ket-qua-thau`)**: sinh hợp đồng từ kết quả thầu (bảng Đã xong, dòng HD-THAU). Test: tích hợp
    `TaoHopDongNguyenTacTuKetQuaThau`, `TaoBangGiaTuRfqTongCongTy_*`, e2e `sourcing.spec.ts` bước 7. Kiểm tra: `dotnet build`, `dotnet test` 519/519; Playwright sourcing +
    contract 5/5; `npm run build`, `tsc`, `oxlint` qua.
  - **Kế hoạch 03/11/2026 đã xong cả 5 bước.** Bản 0.17.0 (bước 3–5, 2 migration) chưa phát hành – chờ người dùng yêu cầu.
- **Yêu cầu người dùng sau khi test 0.14.0 (02/11/2026) – XONG TC-1 → TC-4, đã merge `main` (02/11/2026), chưa phát hành** – gồm:
  - **TC-1 (xong)**: ô chọn danh mục tìm kiểu SAP ECC – gõ `*` + Enter liệt kê toàn bộ (ô hàng tối đa 500 dòng), `*` là ký tự đại diện ở
    mọi ô tìm (`classic/Select.tsx` thay `Select` antd ở 29 file, `matchesKeyword`, `LikePatterns.Contains`); ui-style.md mục 5b.
    Test: tích hợp `TimHang_DauSao_…`, e2e `lookup-wildcard.spec.ts`. Kiểm tra: `dotnet test` 506 qua; Playwright 88/88.
  - **TC-2 (xong)**: màn hình **Tra cứu chứng từ** (DOCS, Hằng ngày; API `GET /api/document-registry`, `DocumentRegistryService`) – 17
    loại (PR, PO, GRN, RTV, TRF, ICS, ICR, ADJ, PRL, SRC, RFQ, CTR, CTA, NCC, NDQ, DGN, VP) theo quyền xem từng loại, tình trạng Chưa hoàn
    thành/Hoàn thành/Đã hủy, trạng thái gốc, cấp đang chờ duyệt, lọc Tôi lập/Tôi đã duyệt (phiếu kho = tôi ghi sổ); bấm đúp mở trang phiếu;
    design.md 18.22. Test: tích hợp `TraCuuChungTu_…`, e2e `document-registry.spec.ts`. Kiểm tra: `dotnet test` 507; Playwright 89/89.
  - **TC-3 (xong)**: thông báo, Việc cần làm, Tra cứu chứng từ, liên kết KSNB mở **trang đầy đủ** thay ngăn kéo cho yêu cầu duyệt NCC
    (`/gia-ncc/duyet-ncc/:id`, thêm tab HỒ SƠ NCC + HỒ SƠ ĐÍNH KÈM chỉ xem – `SupplierProfile.tsx`), NDQ (`/gia-ncc/ncc-doc-quyen/:id`),
    vi phạm (`/bao-cao/so-vi-pham/:id`), phụ lục HĐ (`/mua-hang/hop-dong-khung/phu-luc/:id` – thôi chuyển hướng). `DetailDrawer` thêm
    `asPage` (khung tiêu đề + nút lệnh như DocumentForm, nút Quay lại, Esc không đóng). design.md 18.22. Chỉ sửa Proc.Web, không migration.
    e2e sửa `suppliers`, `sole-sources`, `control`, `contract`. Kiểm tra: Playwright 89/89; `npm run build`, `tsc`, `oxlint` qua.
  - **DGN trang đầy đủ (xong 02/11/2026, nhánh `feature/trang-dgn`)**: `/gia-ncc/danh-gia-ncc-dinh-ky/:id` (`SupplierReviewDetailPage`),
    Tra cứu chứng từ mở trang này thay ngăn kéo. e2e `supplier-review`. Kiểm tra: Playwright 89/89; `tsc`, `oxlint`, `npm run build` qua.
  - **ADJ – phiếu điều chỉnh/kiểm kê kho**: phát hiện chỉ mới có mã loại, chưa có chức năng; người dùng chốt 02/11/2026 làm thành
    **tính năng riêng** (nhánh `feature/dieu-chinh-kho`), 3 lát – design.md 18.23:
    - **ADJ-1 (xong)** backend: `InventoryDocument.CreateAdjustment` (lý do, số biên bản), dòng `Direction` ±1, `StockLedgerEntry.Adjustment`,
      `PeriodCosting.IsPriced` (dòng tăng = nhập có giá), quyền riêng `Inventory.Adjustment.Create/Post` (KHO lập, KETOAN ghi sổ), API
      `/api/stock-adjustments`, thẻ kho, xuất kế toán; migration `V0_15_0_PhieuDieuChinhKho`. Sửa kèm: bỏ ghi sổ đếm trùng kho khi kho xuất =
      kho nhập. Test tích hợp `StockAdjustmentTests`. `dotnet test` 509 qua.
    - **ADJ-2 (xong)** giao diện: màn hình ADJ `/kho/dieu-chinh` (`InventoryDocumentListPage` cột riêng + `createPermission`), trang
      `StockAdjustmentPage.tsx`, `useDocumentCommands.postPermission`, quyền web `InventoryAdjustmentCreate/Post`; danh sách phiếu kho thêm
      `Reason`, `CountReportNo`; Tra cứu chứng từ, xuất kế toán mở được ADJ. e2e `stock-adjustment.spec.ts`, sửa `administration` (nhóm Kho
      11 quyền). Kiểm tra: `dotnet test` 509; Playwright 90/90; `npm run build`, `tsc`, `oxlint` qua.
    - **ADJ-3 (xong, 03/11/2026)** kiểm kê: dòng ADJ thêm `BookQty`/`CountedQty` (migration `V0_15_0_SoLieuKiemKe`), server lấy tồn sổ
      lúc lưu và tự tính chiều/SL (`InventoryDocumentLine.ApplyCount`); API bảng kiểm kê `/api/stock-adjustments/count-sheet`; in PDF
      "Biên bản kiểm kê, điều chỉnh kho" (`DocumentPdfService.AdjustmentAsync`). Web: `CountSheetDialog.tsx` (nút Bảng kiểm kê), cột Thực
      đếm, nút In phiếu. Test `KiemKe_NhapSoThucDem_TuTinhChenhLechSoVoiTonSo_InBienBan`, e2e "bảng kiểm kê". Kiểm tra: `dotnet test`
      510/510; Playwright 91/91 (sửa kèm `administration`: số quyền vai trò 8 → 10); `npm run build`, `tsc`, `oxlint` qua. Merge `main`, xóa nhánh.
    - **Sửa đính kèm ADJ (xong, 03/11/2026, nhánh `fix/dinh-kem-adj`)**: `AttachmentService` – tải lên/xóa đính kèm phiếu ADJ theo quyền lập
      ADJ (trước đòi quyền lập chứng từ kho thường); web tab Đính kèm theo `InventoryAdjustmentCreate`. Test
      `DinhKemBienBan_QuyenLapAdj_…`. Kiểm tra: `dotnet test` 511/511; e2e `stock-adjustment`, `attachments` qua; build/tsc/oxlint qua.
  - **Phát hành 0.15.0 (03/11/2026)**: tag `v0.15.0`, https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.15.0 (gói
    `procurement-0.15.0.zip`), script `db/scripts/0.15.0.sql` (49 migration; đã chạy thử 2 lần trên bản sao CSDL test 0.14.0).
  - **Bản test 0.15.0**: `D:\project\procurement-thu-0.15.0`, CSDL `procurement_kiemtra_0150` (bản sao `procurement_uat` của bản test 0.14.0,
    đã chạy script 0.15.0, đã gán quyền ADJ cho vai trò TOANQUYEN/KHO/KETOAN), chạy `app\Proc.Api.exe --urls http://0.0.0.0:5007` →
    http://192.168.1.199:5007; admin / Admin@2026, tài khoản test `<tct|aph|bmi>.<lap|duyet|kho|ketoan|…>` / Test@2026; dừng:
    `Stop-Process -Id (Get-Content D:\project\procurement-thu-0.15.0\pid.txt)`. Bản 0.14.0 (cổng 5006) vẫn chạy – không được phép dừng
    từ phiên Claude. Sao lưu `procurement_uat` trước nâng cấp: `D:\project\procurement_uat_truoc_0.15.0.dump`. Tường lửa cổng 5007 chưa mở.
  - Việc còn lại chờ người dùng chọn: C3 (ngân sách, hoãn chờ lệnh), việc "làm khi có yêu cầu" ở design.md.
  - **TC-4 (xong)**: **người duyệt tiếp theo** – `NextApproverResolver` (Infrastructure/Purchasing) dùng chung cho thông báo chờ duyệt, tab
    Lịch sử duyệt (`ApprovalStepDto.NextApprovers`, `HistoryAsync(..., document)`) và Tra cứu chứng từ (`DocumentRegistryRowDto.NextApprovers`,
    cột mới): quyền + vai trò cấp, bỏ người lập, bỏ người vướng xung đột lợi ích; chỉ bước đang đến lượt. Giao diện hiện 5 tên + "và N người
    khác". design.md 18.22. Không migration. Test: tích hợp `NguoiDuyetTiepTheo_TheoCapDangCho_BoNguoiLapVaXungDot`, e2e `purchasing`
    (người lập có họ tên riêng). Kiểm tra: `dotnet test` 508; Playwright 89/89; `npm run build`, `tsc`, `oxlint` qua.
  - Môi trường lần này: API dev (5000) và API Testing cũ (5010) cùng khóa `src/Proc.Api/bin` – không được dừng; build/test bằng
    `--artifacts-path <scratchpad>rtifacts`, e2e chạy API Testing bản mới từ đó ở cổng 5011 + Vite 5180.
  - Môi trường: 02/11/2026 người dùng yêu cầu tắt API 5000, 5010, 5011 (đã tắt – build không còn vướng khóa bin). Trước đó: API dev cũ (PID 28216) giữ cổng 5000; e2e chạy trên API Testing cổng 5010 + Vite 5179
    (`E2E_WEB_URL`, `E2E_API_URL`, `VITE_API_URL`).
- **Bản 0.14.0 (02/11/2026): dữ liệu thử + kịch bản test cho người dùng test qua mạng LAN** (theo yêu cầu người dùng).
  - Công cụ `tools/du-lieu-thu` (Node.js, gọi API, chạy lại không trùng): bộ phận, vai trò (mẫu Ban CUVT + TOANQUYEN), 38 tài khoản
    (mỗi công ty 12: cặp lap/duyet toàn quyền + 10 vai trò; aph.khokp1, bmi.denghi.bhld giới hạn phạm vi; mật khẩu Test@2026), xếp loại
    NCC A/B/C, duyệt PO 2 cấp ở APH/BMI (cấp 1 TOANQUYEN, cấp 2 TRUONGBANCUVT từ 200 triệu), 22 NCC (4 dùng chung + 6 riêng/công ty; 1
    chưa duyệt ở TCT, 1 tạm ngưng ở BMI) + 3 NCC nội bộ, 30 vật tư (10/công ty theo nhóm), NCC – sản phẩm (chỉ khai ở TCT).
  - Tài liệu `docs/DuLieuThu_KichBanTest_0.14.0.docx` (38 kịch bản). Người dùng chốt: thêm tài khoản để test đủ quy trình (người lập
    không tự duyệt – không gỡ quy tắc).
  - Bản test: `D:\project\procurement-thu-0.14.0` (Production, CSDL `procurement_uat`, 47 migration), chạy
    `app\Proc.Api.exe --urls http://0.0.0.0:5006` → http://192.168.1.199:5006; admin / Admin@2026; dừng:
    `Stop-Process -Id (Get-Content D:\project\procurement-thu-0.14.0\pid.txt)`. Mã sinh ra: `ket-qua.json` cùng thư mục.
    **Tường lửa cổng 5006 chưa mở** (cần quyền Administrator – người dùng tự chạy lệnh trong README công cụ).
- Không có lát đang dở. Đợt đề xuất Ban Cung ứng (E1 → F4) đã xong hết trừ **C3 (hoãn chờ lệnh)**. Bản **0.13.0** đã phát hành
  02/11/2026: https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.13.0 (bản cài thử ở http://localhost:5005).
  Bản **0.13.1** (sửa lỗi giao diện, không đổi CSDL) phát hành 02/11/2026:
  https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.13.1 – gồm: form nạp đè chữ đang gõ/tạo trùng phiếu, trang trắng
  sau đăng xuất, nút lấy dòng PO/trả hàng bỏ qua cú bấm. Việc tiếp theo: chờ người dùng chọn (C3 khi có lệnh, hoặc việc "làm khi có yêu
  cầu" ở design.md).
- **Sửa lỗi nút lấy dòng bị bỏ qua cú bấm (02/11/2026, cùng nhánh)**: nút "Lấy các dòng còn lại của PO" (GRN) và "Lấy tất cả" (trả hàng)
  có `loading` khi đang tải lại nền nhưng vẫn bật – antd bỏ qua cú bấm lúc loading, người dùng bấm mà không có dòng (lộ ra ở e2e
  `purchasing` khi ép tải 10 luồng: hỏng 3/6). Nay khóa nút khi đang tải lại. Ép tải lại 6/6 qua; cả bộ Playwright 87/87.
- **Sửa lỗi trang trắng sau đăng xuất (02/11/2026, nhánh `fix/dang-xuat-trang-trang`)**: đăng xuất xong router đổi URL sang
  `/dang-nhap` nhưng lượt vẽ (transition) chưa xong; bấm Quay lại ngay thì router trở về đúng vị trí `/` nó còn giữ nên không vẽ lại,
  `<Navigate>` không chạy lại → trang trắng. `RequireAuth` nay tự chuyển trang trong hiệu ứng, đọc đường dẫn từ `window.location`,
  bắt thêm `popstate`; bỏ qua khi URL đã là `/dang-nhap` (StrictMode chạy hiệu ứng hai lần). e2e `auth.spec.ts` bấm Quay lại ngay khi URL
  đổi (không chờ form) – tắt bản sửa thì hỏng 3/6 lần, có bản sửa 36/36 qua. `npm run build`, `tsc`, `oxlint` qua.
- **F4b (xong, đã merge `main` 02/11/2026): chiết khấu doanh số/tài trợ trên hợp đồng + theo dõi thực nhận (P10)** – design.md mục 18.21.
  - `PurchaseContractRebate` (cam kết: loại, % hoặc số tiền cố định mỗi kỳ, doanh số tối thiểu, kỳ Quý/Năm/Cả HĐ – sửa khi Nháp qua
    `SaveContractRequest.Rebates`, null = giữ nguyên), `ContractRebateReceipt` (thực nhận, hủy có lý do, kỳ nhận diện bằng ngày đầu kỳ),
    `ContractRebateTracker` (doanh số = PO Đã duyệt/Đã đóng công ty × NCC trong kỳ; NotDue/Waiting/Received/NotReached/Short, hạn 30 ngày);
    API `GET /api/contracts/{id}/rebates`, `POST .../rebate-receipts`, `POST /api/contract-rebate-receipts/{id}/cancel`; migration
    `V0_13_0_ChietKhauTaiTroHopDong`. Dấu hiệu KSNB "Chiết khấu/tài trợ chưa ghi nhận" đã đo (mở hợp đồng – `controlRefPath` `CTR`).
  - Giao diện: tab CHIẾT KHẤU – TÀI TRỢ trên hợp đồng (`ContractRebatePanel.tsx`: lưới cam kết; hợp đồng hiệu lực/chấm dứt có lưới theo
    dõi theo kỳ, hộp thoại ghi nhận, hủy ghi nhận). Đổi cam kết qua phụ lục: làm khi có yêu cầu.
  - Test: domain + tích hợp `ContractRebateTests`, sửa `ControlAnomalyTests`; e2e `contract.spec.ts` (test mới), sửa `control.spec.ts`.
  - Kiểm tra cuối (02/11/2026): `dotnet test` 505 qua; Playwright 87/87; `npm run build`, `tsc`, `oxlint` qua.
- **F4a (xong, đã merge `main` 02/11/2026): đánh giá NCC định kỳ theo quý (N5)** – design.md mục 18.20.
  - `SupplierReview` (số DGN, 6 tiêu chí 1–5, kết luận, số liệu SUPEVAL/QCDEV chốt lúc hoàn thành, Nháp → Hoàn thành/Hủy), quyền
    `MasterData.SupplierReview.View/Manage` (vai trò mẫu; vai trò đã tạo trước phải gán tay), API `/api/supplier-reviews`, migration
    `V0_13_0_DanhGiaNccDinhKy`; KPI "Hoàn thành đánh giá NCC" đo theo quý kết thúc trong kỳ (bấm đúp mở SUPREV).
  - Giao diện: màn hình SUPREV (Giá và NCC → Đánh giá NCC định kỳ, mặc định quý trước), ngăn kéo phiếu 2 tab (ĐÁNH GIÁ, SỐ LIỆU HỆ THỐNG).
  - Test: domain + tích hợp `SupplierReviewTests`, sửa `BangKpi_…`; e2e `supplier-review.spec.ts`, sửa `kpi`, `document-numbering` (15 loại).
  - Kiểm tra cuối (02/11/2026): `dotnet test` 500 qua; Playwright 86/86; `npm run build`, `tsc`, `oxlint` qua.
- **F3 (xong, đã merge `main` 02/11/2026): Kiểm soát nội bộ – dấu hiệu bất thường, sổ vi phạm, thông báo thẳng lãnh đạo** – design.md mục 18.19.
  - Quyền `Control.Anomaly.View`/`Control.Finding.Manage`/`Control.Finding.Receive`; vai trò mẫu KSNB thêm quyền, vai trò mẫu mới LANHDAO
    (vai trò KSNB đã tạo trước đó phải gán tay quyền mới).
  - API `GET /api/control/anomalies` (`ControlAnomalyService`): giá cao lặp lại 1 NCC, PO sát dưới ngưỡng duyệt, đổi TK ngân hàng NCC,
    mua ngoài hợp đồng cao theo người lập, hóa đơn tính cả hàng thiếu, hàng/hóa đơn trước PO, xung đột lợi ích; chiết khấu "Chưa đo" (F4).
    `PurchaseReportService.OrderLinesAsync`/`ThreeWayRowsAsync` thành hàm tĩnh dùng chung.
  - Sổ vi phạm `ControlFinding` (số loại VP, migration `V0_13_0_GhiNhanViPham`), `/api/control/findings`, thông báo lãnh đạo khi ghi nhận và
    kết luận; KPI "Vi phạm nghiêm trọng" đo từ sổ (vụ Nghiêm trọng đã kết luận).
  - Giao diện: màn hình ANOM, VIOL (nhóm Báo cáo); `Screen.anyPermission`; thông báo loại "Vi phạm KSNB" mở sổ vi phạm.
  - Test: domain `ControlFindingTests`, tích hợp `ControlAnomalyTests` (2), sửa `RoleAdministrationTests`, `BangKpi_…`; e2e `control.spec.ts`,
    sửa `administration` (lọc dòng KSNB), `document-numbering` (14 loại).
  - Kiểm tra cuối (02/11/2026): `dotnet test` 494 qua; Playwright 85/85 (administration sửa rồi chạy lại qua); `npm run build`, `tsc`, `oxlint` qua.
- **F2 (xong, đã merge `main` 02/11/2026): bảng KPI cung ứng 9 chỉ số** – design.md mục 18.18.
  - API `GET /api/reports/kpi` (`KpiService` ghép PRCW, SLA, QCDEV, đánh giá NCC, tiết kiệm thầu, tồn chậm, tỷ lệ PO khẩn; so kỳ trước);
    màn hình KPI (Báo cáo, mặc định tháng trước, bấm đúp mở báo cáo chi tiết, nút "Mục tiêu KPI"). Đánh giá NCC (F4), vi phạm (F3): "Chưa đo".
  - Mục tiêu 9 KPI + số ngày tồn chậm: tham số công ty nhóm KPI (`KpiCatalog`, `CompanyParameter.ChangeKpiTarget`); migration
    `V0_13_0_MucTieuKpi`. `SlowMovingResult` thêm `StockAmount`.
  - Test: domain `MucTieuKpi_…`, tích hợp `BangKpi_TheoKy_SoKyTruoc_MucTieuCauHinhDuoc`, e2e `kpi.spec.ts`; `system-parameters` đếm 19 dòng.
  - Kiểm tra cuối (02/11/2026): `dotnet test` 490 qua; Playwright 84/84 (system-parameters sửa rồi chạy lại qua); `npm run build`, `tsc`, `oxlint` qua.
- **F1 (xong, đã merge `main` 02/11/2026): báo cáo tuần giá mua & mua ngoài hợp đồng, biến động giá** – design.md mục 18.17.
  - API `GET /api/reports/contract-compliance` (màn hình PRCW, mặc định tuần trước): dòng PO Đã duyệt/Đã đóng theo nguồn giá (chọn thầu,
    hợp đồng, bảng giá, lệch giá, không có giá đã duyệt, không xác định), tỷ lệ mua theo giá đã duyệt (cảnh báo khi < 95%), dòng ngoài hợp
    đồng kèm giá đã duyệt, % lệch, tiền chênh, lý do. Bấm đúp mở PO.
  - API `GET /api/reports/price-variance` (màn hình PRVAR, mặc định tháng trước, lọc nhóm hàng): giá BQ gia quyền ĐVT cơ sở kỳ này so kỳ
    trước (trọn tháng lùi theo tháng, còn lại cùng số ngày – `PurchaseReportService.PreviousPeriod`), % thay đổi, ảnh hưởng tiền.
  - Test: tích hợp `BaoCaoTuan_GiaMuaNgoaiHopDong_BienDongGia`, `BienDongGia_KyTruoc_…`; e2e `price-reports.spec.ts`
    (`seedPostedReceipt` trả thêm `supplierId`). Không có migration.
  - Kiểm tra cuối (02/11/2026): `dotnet test` 488 qua; Playwright 82/83 – `po-list` "nhóm theo công ty" lỗi khi chạy song song, chạy riêng
    9/9 qua (chập chờn, theo dõi); `npm run build`, `tsc`, `oxlint` qua.
- **D1 (xong, đã merge `main` 02/11/2026): điểm đặt hàng lại, tồn an toàn, vật tư trọng yếu (K1)** – design.md mục 18.16.
  - Mặt hàng thêm `SafetyStock`, `ReorderPoint`, `IsCritical` (chỉ hàng tồn kho; an toàn ≤ đặt lại ≤ tối đa); migration
    `V0_13_0_DiemDatHangLai`; Excel hàng hóa thêm 3 cột (file mẫu cũ thiếu cột thì giữ giá trị đang có – `ExcelRowReader.HasColumn`).
  - Báo cáo **Gợi ý đặt hàng** (REORD, `GET /api/reports/reorder-suggestions`): khả dụng = tồn mọi kho + đang đặt (PO nháp/chờ duyệt cả SL,
    PO đã duyệt phần chưa nhận) + đề nghị còn mở (PR chưa hủy, phần chưa lên PO); ngưỡng = điểm đặt lại, chưa có thì tồn tối thiểu;
    SL gợi ý lên tồn tối đa; khẩn khi tồn < tồn an toàn. Nút "Lập đề nghị mua" mở PR mới điền sẵn (state điều hướng `PrPrefillState`).
    `ListPage` thêm prop `actions` (nút lệnh riêng).
  - Test: domain `DatHangLai_…`, tích hợp `ReorderSuggestionTests`, e2e `reorder-suggestions.spec.ts`; sửa `ExcelTransferTests` (cột quy đổi
    dời sang 19).
  - Kiểm tra cuối (02/11/2026): `dotnet test` 482 qua; Playwright 82/82; `npm run build`, `tsc`, `oxlint` qua.
  - Môi trường: build hay vướng khóa `obj/*.dswa.cache.json` → đổi tên file rồi build `-m:1`, test `--no-build`.
- **G1–G3 (xong, đã merge `main` 02/11/2026): phím F1 xem lịch sử tạo/sửa/xóa từng ô (kiểu ERP)** – người dùng chốt 02/11/2026: hộp thoại
  trợ giúp ô kiểu SAP (tab LỊCH SỬ THAY ĐỔI + THÔNG TIN Ô); ai đăng nhập cũng xem được (vẫn theo phạm vi công ty/kho/nhóm hàng của bản ghi);
  mọi ô do người dùng nhập đều xem được; làm G1 → G3.
  - Bước 1 (xong): API `GET /api/change-history/field` và `/record` (`ChangeHistoryQueries`) – lịch sử theo ô, cả phiếu kèm dòng đã xóa,
    tham chiếu đổi sang mã – tên, thông tin kỹ thuật ô; test tích hợp `LichSuThayDoi_TheoO_TheoPhieu_CaDongDaXoa_ChanNgoaiPhamVi`.
  - Bước 2 (xong – G1): `FieldHelpHost` (F1 trong AppShell, ô có con trỏ hoặc ô dưới chuột), hộp thoại "Trợ giúp ô" 2 tab, nút
    "Lịch sử thay đổi" (cả phiếu, cả dòng đã xóa, lọc ô/người/ngày); khung dùng chung DocumentForm/LineGrid/DetailDrawer/DataGrid nhận
    `audit`/`auditEntity`; gắn cho 11 chứng từ. design.md mục 9.3a, ui-style.md mục 6 (F1). e2e `field-help.spec.ts`. Kiểm tra: Playwright 80/80.
  - Bước 3 (xong – G2 + G3): 21 ngăn kéo danh mục/hệ thống khai báo `audit`; dòng con là bản ghi riêng (người liên hệ, TK ngân hàng NCC,
    quy đổi ĐVT, NCC áp dụng tại công ty, NCC – sản phẩm) gắn vùng riêng; entity owned tra qua bản ghi chủ; tham số hệ thống và đánh số
    chứng từ trả khóa dòng + cột. F1 trên ô lưới danh sách của 26 màn hình (`auditEntity` của ListPage/DataGrid). Test tích hợp
    `ChangeHistoryTests`, e2e `field-help-catalog.spec.ts`.
  - Kiểm tra cuối (02/11/2026): `dotnet test` 480 qua; Playwright 81/81; `npm run build`, `tsc`, `oxlint` qua. Không có migration.
- **Sửa lỗi form chứng từ nạp đè chữ đang gõ (xong 02/11/2026, nhánh `fix/form-nap-de`)**: hook dùng chung
  `components/DocumentForm/useDocumentFormSync.ts` – chỉ nạp form khi giá trị form tính từ phiếu khác lần nạp trước; lưu thành công thì
  nạp ngay phiếu vừa lưu (Id dòng mới) nên lượt tải lại sau đó bị bỏ qua. Áp cho PO, PR, GRN, trả hàng, ICS, ICR, TRF, bảng giá NCC,
  hợp đồng, RFQ, phiếu tìm nguồn. Sửa kèm lỗi tiềm ẩn **tạo trùng phiếu**: sau khi tạo, trang chuyển sang Id mới trong chuyển trang
  nền nên lúc máy bận nút lệnh còn giữ `isNew` cũ – bấm Gửi duyệt ngay thì gọi tạo lần nữa; nay `persist` lưu theo `targetId()` (Id phiếu
  đã lưu giữ trong ref). Ngăn kéo phụ lục hợp đồng (`ContractAmendmentDrawer`) và ngăn kéo danh mục chưa đổi (nạp theo cách
  khác – làm khi có lỗi). e2e `pricing` bỏ bước tải lại trang: làm chậm lượt tải phiếu, gõ lý do lệch giá trước khi tải xong (đã thử tắt
  cơ chế → test hỏng đúng như lỗi cũ). Kiểm tra: Playwright 87/87, `pricing` lặp 8 lần × 4 luồng qua; `npm run build`, `tsc`, `oxlint` qua;
  không sửa backend nên không chạy lại `dotnet test` (API đang chạy khóa DLL).
- **HOÃN – chờ lệnh người dùng: lát C3 (ngân sách công ty × bộ phận × tháng)**. Người dùng yêu cầu 02/11/2026: không làm ngân sách lúc
  này, **chỉ làm khi người dùng ra lệnh**. Nội dung đã chốt để làm sau: ngân sách theo công ty × bộ phận × tháng; PR/PO kiểm tra, vượt
  ngân sách không chặn mà thêm cấp duyệt "vượt ngân sách" (design.md mục 18 bảng quyết định; Phantich V1). Không tự đề xuất làm C3 khi
  "làm tiếp" – bỏ qua sang lát kế tiếp.
- Ghi nhớ cho **C6** (đối chiếu PO – nhận – hóa đơn) / **F3**: chặn lùi ngày PO khẩn (C2a) không phải kiểm soát chống hợp thức hóa; PO thường
  vẫn lập lùi ngày được – kiểm soát thật là đối chiếu ngày nhận/hóa đơn với ngày PO (design.md 18.11).
  Từ nay mọi màn hình làm theo docs/ui-style.md.
- Đang chờ người dùng: hồ sơ trình thiếu mục hiện **không chặn** gửi duyệt (mặc định an toàn) – đổi thành chặn nếu người dùng yêu cầu.

## Việc tiếp theo

1. **Theo đề xuất Ban Cung ứng** (đã chốt 01/11/2026 – `docs/Phantich_01112026.md` mục 5–6, design.md mục 18): làm lần lượt
   E1 → E2 → E3 → A1 → A2 → A3 → B1 … → F4. E1, E2, E3, A1, A2, A3, B1, B2, B3a, B3b, B4a, B4b, B4c, B5a, B5b, C1, C1b, C2a, C2b, C4, C5a, C5b, C6, D1, F1, F2, F3 đã xong; **C3 hoãn chờ lệnh người dùng**; G1–G3 (F1 lịch sử ô) đã xong; F4a, F4b đã xong. Bản **0.13.0** phát hành 02/11/2026 (C1b–F4b, G1–G3):
   https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.13.0
   Bản 0.10.0 đã phát hành 01/11/2026 (SEC-2, đợt E, A1, WIN). Bản **0.12.0** phát hành 02/11/2026 (B3a–B5b, C1):
   https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.12.0
2. Hợp đồng nguyên tắc (CTR): B4a, B4b (phụ lục), B4c (nhập Excel) xong (design.md 18.7). PO lấy giá theo hợp đồng: C1 xong (18.10);
   phạm vi bộ phận được đặt của hợp đồng: C1b xong.
3. **Không xóa** dữ liệu/CSDL nào (`procurement_perf`, `procurement_perf_mau`, `procurement_kiemtra_090` – CSDL thử script 0.9.0, `procurement_kiemtra_windows` – CSDL thử bản publish Windows, `procurement_kiemtra_0100` – CSDL thử script 0.10.0 và bản cài thử, `procurement_kiemtra_0110` – CSDL thử chuỗi script 0.1.0 → 0.11.0, `procurement_kiemtra_0130` – CSDL bản cài thử 0.13.0,
   file `D:\project\procurement_perf_nguon.dump`) cho đến khi người dùng chỉ định.
4. ~~Lỗi trang trắng sau đăng xuất~~ – đã sửa 02/11/2026 (nhánh `fix/dang-xuat-trang-trang`, xem mục Đang làm).
5. ~~Test Playwright `supplier-links` chập chờn~~ – 02/11/2026 chạy riêng 12/12, ép tải 10 luồng 6/6, cả bộ 87/87 đều qua, không tái
   hiện được; khi ép tải tìm ra lỗi thật ở nút "Lấy các dòng còn lại của PO" (đã sửa, xem mục Đang làm). Lỗi lại thì xem tiếp.
6. Việc "làm khi có yêu cầu" ghi trong design.md (gửi email, ngoại tệ, cổng NCC, gộp PR mua tập trung…).

## Ghi nhớ môi trường dev

- API dev: `dotnet run --no-build --project src/Proc.Api --no-launch-profile --urls http://localhost:5000` (ASPNETCORE_ENVIRONMENT=Development);
  Web: `npm run dev` trong `src/Proc.Web` (cổng 5173). Đăng nhập dev: `admin` / `Admin@2026`.
- Bản cài thử 0.13.0 (chế độ Production, từ gói Release): `D:\project\procurement-thu-0.13.0`, chạy ở http://localhost:5005, CSDL
  `procurement_kiemtra_0130` (chạy đủ script 0.1.0 → 0.13.0, 47 migration, dữ liệu khởi tạo), đăng nhập `admin` / `Admin@2026`;
  dừng: `Stop-Process -Id (Get-Content D:\project\procurement-thu-0.13.0\pid.txt)`.
- Bản cài thử 0.12.0 (chế độ Production, từ gói Release): `D:\project\procurement-thu-0.12.0`, chạy ở http://localhost:5004, CSDL
  `procurement_kiemtra_0120` (chạy đủ script 0.1.0 → 0.12.0, 36 migration, dữ liệu khởi tạo), đăng nhập `admin` / `Admin@2026`;
  dừng: `Stop-Process -Id (Get-Content D:\project\procurement-thu-0.12.0\pid.txt)`.
- Phát hành: `publish.ps1` chạy bằng `powershell -File` (PowerShell 5.1 trong phiên coi cảnh báo Vite ở stderr là lỗi); máy chủ Vite còn chạy
  thì dùng `-NoInstall` (npm ci xóa dở node_modules sẽ làm hỏng cả Vite đang chạy – phải dừng Vite rồi `npm ci` lại).
- Bản cài thử 0.11.0 (chế độ Production, từ gói Release): `D:\project\procurement-thu-0.11.0`, chạy ở http://localhost:5003, CSDL
  `procurement_kiemtra_0110` (chạy đủ script 0.1.0 → 0.11.0, dữ liệu khởi tạo), đăng nhập `admin` / `Admin@2026`;
  dừng: `Stop-Process -Id (Get-Content D:\project\procurement-thu-0.11.0\pid.txt)`.
- Bản cài thử 0.10.0 (chế độ Production, như cài Windows): `D:\project\procurement-thu-0.10.0`, chạy ở http://localhost:5002, CSDL
  `procurement_kiemtra_0100`, đăng nhập `admin` / `Admin@2026`; dừng: `Stop-Process -Id (Get-Content D:\project\procurement-thu-0.10.0\pid.txt)`.
- Build bị khóa DLL khi API đang chạy → dừng API trước khi build; chạy Playwright khi API môi trường Testing đã bật sẵn (`--no-build`).
