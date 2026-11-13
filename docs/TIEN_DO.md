# Tiến độ dự án

Cập nhật: 13/11/2026 (Y9: theo dõi thanh toán TTPO, xuất chứng từ mua cho kế toán ACCP – bản 0.27.0; Y8: đề nghị thanh toán 5A/5B; P4b: báo NCC kết quả chọn thầu – Phòng CUVT tự bấm gửi; 12/11/2026: P4a: NCC xác nhận / từ chối đơn hàng trên Portal, Phòng CUVT mở lại phản hồi, thư đơn hàng mới / hủy – bắt đầu 0.26.0; P3b-3: email báo NCC qua Portal; P3b-2: hỏi đáp làm rõ RFQ trên Portal – trả lời riêng NCC hỏi, thông báo làm rõ công khai; P3b-1: file đính kèm báo giá Portal; P3a: báo giá online niêm phong trên Portal, hạn nộp kèm giờ, xem trước giờ mở theo quyền – bắt đầu 0.25.0; phát hành 0.24.0 kèm Portal P1, P1b; P1b: hạ tầng Portal – giới hạn tần suất, tên miền riêng, kiểm file; P1: Portal NCC tài khoản; Y6: RFQ biểu mẫu 3A/3a/3B – bắt đầu 0.24.0; phát hành 0.23.0; Y10: mẫu in biểu mẫu CUVT 1A/1B/DV, 2A/2B; Y5c-2: giao diện phiếu bảng tổng hợp BTH, duyệt gọn, hủy dây chuyền; Y5c-1: bắt buộc qua bảng tổng hợp; Y3c: chuỗi duyệt theo loại mua, bước Thủ kho nơi nhận, chặng vượt dự toán – phần vượt xem trước Chủ tịch; Y3b: thủ kho nhập tồn; Y3a: loại mua A/B/DV + công trình trên đề nghị mua, lũy kế công trình; Y2: công trình và dự toán CTDT – bắt đầu 0.23.0; Z1–Z4c quy tắc chung chứng từ; bản test 5007 nâng lên 0.22.0; O3: vai trò mẫu + cấu hình duyệt mẫu theo biểu mẫu CUVT; O2: ủy quyền, email, nhắc quá hạn, duyệt trên điện thoại; 10/11: O1: khối, Trưởng phòng ban, GĐ khối, người duyệt động; K2: phiếu xác nhận nhận hàng XNH, báo cáo OTIF; K1: tắt phân hệ kho – bắt đầu hệ thống mới từ 0.22.0, design.md mục 19; tài liệu hướng dẫn sử dụng, hướng dẫn cài đặt chi tiết, ghi chú hàm nội bộ; trước đó 09/11/2026: phát hành 0.21.0, bản test 5007 nâng lên 0.21.0; G12: báo cáo tháng cho Ban BCBAN; sửa lỗi e2e volume-rebate – focus dòng mới của LineGrid; G8: hồ sơ đánh giá NCC mới HSN – backend + giao diện; G1b: đếm tối thiểu 03 NCC theo nhóm hàng; H4: đơn vị đặt hàng xác nhận nghiệm thu phiếu nhập; H3: PO vượt giá trần lên Chủ tịch/TGĐ; H2: phiếu GTH có ngoại lệ lên Chủ tịch/TGĐ; H1: ý kiến thẩm định của thành viên chuyên môn; G7c: quyền đặc biệt "Tạo PO cho công ty thành viên"; 08/11: G7: đặt hàng gộp DHG – phân bổ trong biên độ, tạo PO từng công ty; G10: vai trò mẫu + cấu hình duyệt mẫu theo sơ đồ; quyết định Q1–Q6; G6b: thực nhận chiết khấu sản lượng gộp, chia phần công ty, KSNB; G6a: thỏa thuận chiết khấu bậc sản lượng gộp CKB + theo dõi bậc CKBT; 07/11: G5c: SL kế hoạch chia theo ngày + xử lý SL lẻ; G5c lấy SL kế hoạch cho RFQ/hợp đồng; G5b tổng hợp nhu cầu gộp + quyền xem toàn tập đoàn; phát hành 0.20.0, bản test 5007 nâng lên 0.20.0; mở rộng NCC cho công ty; G5a phiếu kế hoạch nhu cầu; ngành hàng NCC từ danh mục Nhóm hàng; đồng bộ ô tìm *; mã hàng theo NCC + ĐVT mua trên PR/PO/RFQ; phát hành 0.19.0; trước đó: rà soát lỗi R1–R8, lát C3, phát hành 0.15.0–0.18.0, A4, nhập bảng giá Excel, cảnh báo giá cao, hợp đồng từ kết quả thầu, in PR có hàng mới). Đọc file này đầu mỗi phiên để biết đang ở đâu; cập nhật sau mỗi lát (CLAUDE.md mục 0).

## Đã xong (đã commit, push, merge vào `main`)

| Phần | Nội dung | Merge |
| --- | --- | --- |
| K2 (10/11/2026) | **Phiếu xác nhận nhận hàng XNH thay phiếu nhập kho** (design.md mục 19.4 – lát K2). Domain `GoodsReceiptConfirmation` (dòng theo dòng PO: SL phiếu giao / đạt = thực nhận / không đạt, kết quả kiểm tra; hóa đơn cập nhật được sau xác nhận; nghiệm thu của đơn vị đặt hàng – người lập/xác nhận không tự nghiệm thu; điều chỉnh SL thực nhận có lý do + lịch sử `GoodsReceiptAdjustment` thay trả NCC), loại chứng từ **XNH**, quyền `Purchase.Receipt.View/Manage/Accept` + vai trò mẫu (KHO, DENGHI, DONVIDATHANG, KETOAN, nhóm xem mua hàng), migration `V0_22_0_XacNhanNhanHang` (3 bảng, Down mất phiếu XNH). `/api/receipt-confirmations` (lập, sửa, confirm, cancel, invoice, acceptance, adjustments); lập/xác nhận cần nơi nhận trong phạm vi kho; SL đặt + dung sai; tham số QC bắt buộc, tách người lập PO. Nguồn "SL đã nhận" = XNH (`PurchaseOrderReceipts.ReceivedBaseByLineAsync`; code kho cũ dùng `LegacyGrnReceivedBaseByLineAsync`): PO (nhận đủ tự đóng, hủy/hoàn thành PO xét XNH), theo dõi giao hàng, ngân sách, PR đang đặt, giá nhận gần nhất, so sánh giá, QCDEV, MATCH3 + KSNB (màn hình XNH), chi tiêu/đánh giá NCC, đánh giá NCC định kỳ. Báo cáo mới **SL đặt – SL thực nhận (OTIF)** `/api/reports/order-fulfillment`. Web: màn hình Mua hàng → Xác nhận nhận hàng (`ReceiptConfirmationPages.tsx`, lưới cuộn ngang, tab Lịch sử điều chỉnh, đính kèm), nút "Lập phiếu nhận hàng" trên PO, báo cáo OTIF (theo dòng / theo NCC), liên kết QCDEV/MATCH3/KSNB/tra cứu chứng từ sang XNH. `appsettings.Testing.json` giữ `InventoryEnabled=true` cho test code kho cũ đến K3. Test: domain 7, tích hợp 3 mới + chuyển 14 test cũ sang XNH (helper `TestReceipts`); e2e `receipt-confirmation.spec.ts` + chuyển 7 spec (helper `receiptHelpers.ts`). Kiểm tra 10/11: dotnet build, dotnet test 620/620, npm run build, tsc, oxlint, Playwright 117/117 (API 5010 + Vite 5180; Vite bị crash một lần khi chạy cả bộ 8 luồng – chạy lại `--workers=4` qua). Chưa làm: in phiếu XNH ra PDF. | feature/k2-xnh |
| K1 (10/11/2026) | **Tắt phân hệ kho** – bắt đầu hệ thống mới từ 0.22.0 (người dùng đồng ý toàn bộ đề xuất Q1–Q18 của bản phân tích `PhanTich_KeHoach_0.22.0_10-11-2026.pdf`; bổ sung Q3: đơn vị mua là danh mục người dùng được cấp quyền tạo, như bộ phận; Q14: Portal có tên miền ra internet, lập trình/test trong LAN, máy chủ Windows/IIS). Ghi quyết định: **design.md mục 19** (Q1–Q18, loại mua A/B/DV/C, 7 bước + chuỗi duyệt theo biểu mẫu CUVT, bỏ kho, Portal, lộ trình 0.22.0–0.28.0), ghi chú ngừng dùng ở mục 4, 6, 7; **CLAUDE.md mục 3, 4** bỏ quy tắc bất biến về kho. Code: cấu hình `Modules:InventoryEnabled` (`ModuleOptions`, mặc định **false**; `appsettings.Testing.json` tạm **true** đến K2), `Permissions.InventoryModule` (mọi `Inventory.*`, `Report.Stock.View`, `Report.Stock.ViewAllCompanies`, `Report.Accounting.Export`) bị bỏ khi nạp phạm vi (`UserScopeLoader`), khi trả quyền cho giao diện (`AuthService`), trên danh mục quyền, vai trò mẫu, kiểm tra cấp quyền (`GrantChecker`); sửa vai trò **giữ nguyên** dòng quyền kho cũ (không xóa dữ liệu). API kho trả 403 "Phân hệ kho đã ngừng dùng từ bản 0.22.0…"; menu ẩn hẳn nhóm Kho khi không còn màn hình; `/api/group-companies` nhận thêm quyền xem bảng giá (IPR vẫn dùng). Không migration. Test: domain `InventoryModuleTests` (2), tích hợp `InventoryModuleDisabledTests` (2, API dựng với cấu hình tắt); kiểm tra tay bằng Playwright với API tắt kho (menu không còn Kho, /kho/grn báo không có quyền). Còn lại cho K2: báo cáo mua hàng (theo dõi giao hàng, MATCH3, KPI…) vẫn đọc phiếu nhập cũ và có liên kết sang /kho/grn – **người dùng chấp nhận khoảng trống này** đến K2 (10/11/2026) | feature/k1-tat-kho |
| TL-1 (10/11/2026) | **Tài liệu và ghi chú code** (người dùng yêu cầu trước khi bắt đầu phân tích viết lại từ 0.22.0): bộ **hướng dẫn sử dụng** `docs/huong-dan-su-dung/` (9 phần: làm quen, danh mục, mua hàng, giá và NCC, đấu thầu, kho, báo cáo, hệ thống, quy trình mẫu + câu hỏi thường gặp – theo bản 0.21.0); **hướng dẫn cài đặt chi tiết** `docs/huong-dan-cai-dat.md` (máy dev, Windows Server/IIS, Ubuntu/Docker, bản test mạng nội bộ, nâng cấp/sao lưu/khôi phục/quay lại, bảng cấu hình, xử lý sự cố); README trỏ tới hai tài liệu. **Ghi chú code**: 308 hàm/kiểu private/internal C# thêm `/// <summary>`, 84 hàm TS (src, e2e) thêm JSDoc – quét lại không còn hàm thiếu ghi chú; public C# đã đủ XML doc (build 0 cảnh báo). Không đổi logic. Kiểm tra: `dotnet build` 0 cảnh báo, `dotnet test` qua, `tsc`, `oxlint` qua. 2 file `docs/CUVT nhóm A/B 08.01.2025. (final).xlsx` đã lên GitHub (cùng commit O2b); người dùng xác nhận 11/11/2026 là dữ liệu test, được đưa lên | feature/tai-lieu-ghi-chu |
| G8 (09/11/2026) | **Hồ sơ đánh giá NCC mới – màn hình HSN** (design.md 18.25 mục G8, Q2; người dùng duyệt kế hoạch: bắt buộc với NCC tạo mới, dùng lại luồng duyệt NCC, đề xuất là bước riêng). G8a backend: `SupplierOnboarding` (7 trạng thái, 5 giấy tờ JSON, 6 tiêu chí), `/api/supplier-onboardings` (lập, sửa, evaluate, reopen, propose, withdraw, cancel); đề xuất lập `SupplierChangeRequest` sang Đã duyệt, `SupplierOnboardingSync` đổi hồ sơ khi yêu cầu duyệt/từ chối/rút (duyệt → thêm ngành hàng); `Supplier.RequiresOnboarding`; tham số tổng công ty `SUPPLIER_ONBOARDING_OPTIONAL` (mặc định tắt; CSDL test bật trong `ProcApiFactory`, e2e bật ở `e2e/global-setup.ts`); quyền View/Manage/Propose + vai trò mẫu; đính kèm, sổ chứng từ. G8b giao diện: `SupplierOnboardingPages.tsx` (ListPage + DocumentForm, lưới Hồ sơ pháp lý cố định, tab Đánh giá/Đề xuất/Thẩm định/Đính kèm), liên kết hồ sơ ở ngăn kéo SUPAPR (`SupplierChangeRequestDto.Onboarding`) và ngăn kéo NCC (`RequiresOnboarding`, `LatestOnboarding`), Tra cứu chứng từ loại HSN. Migration `V0_20_0_HoSoDanhGiaNccMoi`. Test domain 6, tích hợp 2, e2e `supplier-onboarding.spec.ts`; e2e `system-parameters` 24 dòng, `document-numbering` 21 loại. Kiểm tra 09/11: dotnet test 604/604, npm build, tsc, oxlint, playwright 114/114 | feature/g8-ho-so-ncc-moi |
| G1b (09/11/2026) | **Đếm tối thiểu 03 NCC theo nhóm hàng** (Q1, design.md 18.25 mục G1, H2). `MasterData/QualifiedSuppliers.ByCategoryAsync`: NCC cung cấp nhóm X = ngành hàng (`SupplierCategory`) là X hoặc nhóm cha, hoặc NCC – sản phẩm của mặt hàng thuộc trực tiếp X. SUPCOV mỗi dòng một nhóm hàng (Mã nhóm, Số mặt hàng, NCC đã duyệt/tiềm năng/tạm ngưng, Còn thiếu, MH độc quyền, số phiếu NDQ); "Miễn" khi mọi mặt hàng của nhóm có NDQ còn tác dụng; DTO `SupplierCoverageRowDto`/`SupplierCoverageResult` đổi sang `CategoryId…`/`Categories`. Ngoại lệ GTH (H2) đếm theo nhóm hàng của mặt hàng + NCC chính/dự phòng; câu "Nhóm hàng chỉ có N NCC đủ điều kiện (tối thiểu 3)". Không migration. Test: tích hợp `DemTheoNhomHang_NganhHangNhomCha_DocQuyenDuocMien_LocChiThieu_CanQuyenXemNcc`, `CoNgoaiLe_…` (mặt hàng dòng 2 ở nhóm con), domain `NgoaiLe_…`; e2e `supplier-coverage`, `monthly-approval`. Kiểm tra: `dotnet test` 596/596; Playwright 113/113 (cả `volume-rebate` qua); build, tsc, oxlint qua. | `feature/g1b-ncc-theo-nhom-hang` |
| H4 (09/11/2026) | **Đơn vị đặt hàng xác nhận nghiệm thu trên phiếu nhập** (design.md 18.25 mục H4 – sơ đồ mục 3). `InventoryDocument.ConfirmAcceptance` (phiếu nhập mua đã ghi sổ; Đạt / Không đạt bắt buộc ý kiến ≤ 500; người lập/ghi sổ không tự xác nhận; xác nhận lại được), `MarkUnposted` xóa xác nhận; cột `Acceptance`, `AcceptanceNote`, `AcceptedBy`, `AcceptedAt` (migration `V0_20_0_XacNhanNghiemThu`). Quyền `Inventory.Receipt.Accept`; vai trò mẫu DENGHI thêm xem chứng từ kho + nghiệm thu. `POST /api/goods-receipts/{id}/acceptance`; DTO phiếu kho có trạng thái nghiệm thu, `CanConfirmAcceptance`. MATCH3: cột `Acceptance`, cảnh báo `AcceptanceDisputed`. Web: nút "Xác nhận nghiệm thu" + `ReceiptAcceptanceDialog`, thông báo nghiệm thu trên phiếu nhập, cột Nghiệm thu (đèn) trên MATCH3. Test: domain `NghiemThu_SauGhiSo_KhongDatPhaiCoYKien_ThuKhoKhongTuXacNhan_BoGhiSoThiXoa`, tích hợp `NghiemThu_DonViDatHangXacNhan_ThuKhoKhongTuXacNhan_HienTrenDoiChieu`; e2e `receipt-acceptance.spec.ts`, `administration` (nhóm Kho 12 quyền). Kiểm tra: `dotnet test` 596/596; Playwright 111/113 – `volume-rebate` lỗi ổn định (ô % bậc 2 gõ nối vào ô ngưỡng → "20.002"; màn hình CKB không đổi trong lát này – cần xem riêng), build, tsc, oxlint qua. API 5000 bản cũ không dừng được → build/test `--artifacts-path`, migration `--configuration Release`, Playwright API 5010 + Vite 5180. | `feature/h4-xac-nhan-nghiem-thu` |
| H3 (09/11/2026) | **PO vượt giá trần → chặng Chủ tịch/TGĐ** (design.md 18.25 mục H3 – Q3, thay "chỉ cảnh báo" của G4b). Loại duyệt **POC** "Đơn mua hàng vượt giá trần" (chặng 5 sau POL, luôn ≥ 1 cấp, quyền duyệt PO); `PurchaseOrderService.MonthlyRefsAsync` (số phiếu tháng, trần quy ĐVT, cờ vượt trần – dùng cho DTO và gửi duyệt); `ApprovalPlan.WithoutPlannedRoles` + tham số `skipPlannedRoleTypes` của `SubmitWithStagesAsync`: bỏ cấp POC có vai trò đã có ở chặng trước (Chủ tịch đã duyệt PO thì không thêm – tránh kẹt R3). Mẫu POC – CHUTICH (mọi công ty). Không migration. Web: nhãn chặng 5 "Vượt giá trần", cảnh báo vàng mới, `ApprovalChain` nhận `stageLabels` – popup gửi duyệt PR/PO/GTH ghi tên chặng. Test: domain `ChangVuotGiaTran_BoCapCoVaiTroDaDuyetChangTruoc` + mẫu POC; tích hợp `PoVuotGiaTran_LenChuTich_KhongLapLaiKhiChuTichDaDuyetPo`, test G3 (chặng 1,4,5), tiết kiệm giá chuẩn (3 người duyệt luân phiên); e2e `po-over-ceiling.spec.ts`. Kiểm tra: `dotnet test` 594/594, Playwright 112/112 (`volume-rebate` lỗi một lần khi chạy cả bộ, chạy lại riêng qua), build, tsc, oxlint qua. API cổng 5000 bản cũ không dừng được → build/test `--artifacts-path`, Playwright API 5010 + Vite 5180. Ghi chú rủi ro: POX/POL (mẫu cấp 2 Chủ tịch) chưa bỏ cấp trùng vai trò. | `feature/h3-po-vuot-gia-tran` |
| H2 (09/11/2026) | **Phiếu GTH có ngoại lệ → chặng Chủ tịch/TGĐ** (design.md 18.25 mục H2 – Q6). `MonthlyApprovalExceptions.ForLine` (dưới 03 NCC đủ điều kiện = NCC Đã duyệt có dòng NCC – sản phẩm + NCC chính/dự phòng; độc quyền NDQ; giá chính/dự phòng vượt giá mục tiêu), `MonthlySupplierApproval.PrepareSubmit` (có ngoại lệ mà chưa giải trình → chặn gửi; giải trình không kèm ngoại lệ = tự khai), cột `ExceptionNote`, `HasException` (migration `V0_20_0_GthNgoaiLe`, 69 → 70). Loại duyệt **GTX** (chặng 2, `SubmitWithStagesAsync`, luôn ≥ 1 cấp), mẫu GTX – CHUTICH (chỉ tổng công ty), vai trò mẫu CHUTICH có quyền duyệt GTH. Web: ô "Giải trình ngoại lệ", cột "Ngoại lệ" (tam giác vàng) trên lưới GTH, cảnh báo vàng đầu phiếu, popup Duyệt nêu ngoại lệ, lịch sử duyệt nhãn chặng "Ngoại lệ – Chủ tịch/TGĐ", danh sách có cột Ngoại lệ. Test: domain `NgoaiLe_ItNcc_DocQuyen_VuotMucTieu_BatBuocGiaiTrinh_TuKhaiNgoaiLe`, tích hợp `CoNgoaiLe_BatBuocGiaiTrinh_ThemChangChuTich_NguoiDuyetHoiDongKhongDuyetTiep`; test GTH cũ thêm NCC thứ ba/giải trình, `ProcApiFactory.FinishMonthlyApprovalAsync`; e2e `monthly-approval` (chặn gửi khi chưa giải trình, chặng 2), `pooled-order` (duyệt 2 chặng). Kiểm tra: `dotnet test` 592/592, Playwright 111/111, `npm run build`, `tsc`, `oxlint` qua. API dev cổng 5000 đang chạy bản cũ (không được dừng) nên build/test dùng `--artifacts-path` thư mục tạm, migration tạo bằng `--configuration Release`, Playwright chạy API mới cổng 5010 + Vite 5174. |
| H1 (09/11/2026) | **Ý kiến thẩm định của thành viên chuyên môn** (design.md 18.25 mục H1 – không phải cấp duyệt). H1a: `AppraisalOpinion` (khía cạnh Chất lượng/Pháp lý/Khác, Đạt/Không đạt, nội dung bắt buộc; chỉ thêm, người ghi rút có lý do), `AppraisalOpinionService`, API `/api/appraisal-opinions` (GET, POST, `/{id}/withdraw`), quyền `Sourcing.Appraisal.Give` (vai trò mẫu THAMDINHCL, THAMDINHPL, PHAPCHE); chủ GTH, RFQ, hợp đồng, NCC – chỉ ghi khi chủ chưa có quyết định cuối cùng, người lập không tự thẩm định; đính kèm loại chủ `AppraisalOpinion` (chỉ người ghi thêm). Migration `V0_20_0_YKienThamDinh` (68 → 69, bảng mới). H1b: `components/AppraisalPanel` – tab THẨM ĐỊNH (nhãn kèm số ý kiến/không đạt) trên GTH, RFQ, hợp đồng, ngăn kéo NCC, màn hình duyệt NCC; hộp thoại ghi ý kiến có đính kèm chờ; rút có lý do; `AppraisalSummary` trong hộp thoại Duyệt (nhận dữ liệu trang đã tải vì modal.confirm nằm ngoài QueryClientProvider). Sửa kèm: biến `--erp-light-*` khai báo ở `:root` của `classic-app.css` (đèn trong panel trước đây hiện ô trắng), icon `plus-circle` màu xanh lá. Test: domain `AppraisalOpinionTests` (3), tích hợp `AppraisalOpinionTests` (2), Playwright `appraisal.spec.ts`. Kiểm tra: `dotnet test` 590/590, Playwright 111/111, `npm run build`, `tsc`, `oxlint` qua. | `feature/h1-y-kien-tham-dinh` |
| G7c (09/11/2026) | **Quyền đặc biệt tạo PO cho công ty thành viên** (người dùng yêu cầu "tổng công ty có thể tạo PO cho công ty thành viên nhưng phải được cấp quyền đặc biệt"; design.md 18.25 mục G7): quyền mới `Purchase.PooledOrder.CreateMemberPo` cấp ở vai trò tại tổng công ty (không thuộc vai trò mẫu, ADMIN có sẵn). Có quyền thì "Tạo PO" trên DHG tạo PO cho công ty thành viên **không cần vai trò ở đó** – `ICompanyScopeRunner.RunOnBehalfAsync` (thay `RunAsync`): tạm quyền xem/tạo PO tại công ty nhận, mọi kho, nhóm hàng như ở tổng công ty; vẫn kiểm tra kho, NCC áp dụng, ngân sách, giá theo công ty nhận. Không có quyền thì 403 trước khi tạo PO nào (báo mã công ty), nút "Tạo PO" khóa + thông báo lý do (`actions.createOrdersBlockedReason`). Phần PO của tổng công ty cần `Purchase.PO.Create`. Test tích hợp `PooledOrderTests` sửa: không quyền đặc biệt → 403; có quyền, không vai trò ở A/B → tạo được; B ngừng áp dụng NCC → không PO nào. Kiểm tra: `dotnet test` 585/585, Playwright 110/110, build, tsc, oxlint qua. Không có migration. | `feature/g7c-quyen-tao-po-thanh-vien` |
| G7 (08/11/2026) | **Đặt hàng gộp và phân bổ – màn hình DHG** (design.md 18.25 mục G7, quy tắc tự đặt – người dùng "làm tiếp"): phiếu `PooledOrder` ở tổng công ty theo phiếu GTH đã duyệt + NCC chính/dự phòng, giá Ban duyệt; SL phân bổ từng công ty, đề xuất theo tỷ lệ kế hoạch nhu cầu tháng, biên độ `POOLED_MARGIN_PERCENT` (SEC, mặc định 10%; dung sai max(tổng × %, 1 ĐVT)), không có kế hoạch thì bắt buộc lý do; "Tạo PO" sinh mỗi công ty một PO nháp dưới phạm vi người dùng tại công ty đó (`ICompanyScopeRunner`), một giao dịch – một công ty lỗi thì không PO nào. Quyền `Purchase.PooledOrder.View/Manage`, loại số DHG, đính kèm, tra cứu chứng từ. Web: `PooledOrderPages.tsx` (cột SL theo công ty, Tổng SL tự chia, cảnh báo vượt biên độ, tab Công ty nhận: kho, bộ phận, PO đã tạo). Migration `V0_20_0_DatHangGop`. Test domain `PooledOrderTests` (6), tích hợp `PooledOrderTests`, Playwright `pooled-order.spec` (+ đếm 20 loại số, 23 tham số SEC). Kiểm tra: `dotnet test` 585/585, Playwright 110/110, build, tsc, oxlint qua. Ghi nhận: `volume-rebate.spec` chập chờn khi chạy cả bộ (2/3 lần lỗi ở bước "Đã lưu thỏa thuận", chạy riêng luôn qua) – chưa rõ nguyên nhân, không liên quan G7. | `feature/g7-dat-hang-gop` |
| G10 (08/11/2026) | **Vai trò mẫu và cấu hình duyệt mẫu theo sơ đồ phê duyệt mua sắm** (design.md 18.25): 9 vai trò mẫu mới (CHUTICH, PHOBAN, THAMDINHCL, THAMDINHPL, VPBDIEUPHOI, VPBPHANTICH, CUCTRUONGPHONG, CUCDATHANG, DONVIDATHANG; Trưởng ban dùng TRUONGBANCUVT) – Phòng cung ứng tìm NCC (Q2); hộp thoại "Cấu hình duyệt mẫu theo sơ đồ…" trên màn hình Quy trình duyệt (`GET/POST /api/approval-rules/template`): xem trước, chỉ tạo loại phiếu chưa cấu hình, ngưỡng 0. Không migration. Test domain `ApprovalRuleTemplateTests` (3), tích hợp `ApprovalRuleTemplateTests`, Playwright `approval-rule-template.spec`. Kiểm tra: `dotnet test` 578/578, Playwright 109/109, build, tsc, oxlint qua. | `feature/g10-vai-tro-mau` |
| G6b (08/11/2026) | **Thực nhận chiết khấu sản lượng gộp** (design.md 18.25): tab **Thực nhận** của CKB – mỗi kỳ phải nhận/đã nhận/còn thiếu/hạn ghi nhận (cuối kỳ + 30 ngày)/tình trạng; tổng công ty ghi nhận (**chỉ kỳ đã kết thúc, ngày nhận sau cuối kỳ** – người dùng chốt 08/11/2026), Ghi chú + Đính kèm (`VolumeRebateReceipt`), hủy có lý do; phần chia từng công ty theo tỷ lệ doanh số kỳ lúc ghi, lưu cố định (`VolumeRebateReceiptShare`); công ty áp dụng chỉ thấy phần mình. CKBT thêm "Đã nhận kỳ này", "Thiếu thực nhận quá hạn" (tính vào lọc cảnh báo). KSNB dấu hiệu `UnrecordedVolumeRebate` ở công ty sở hữu thỏa thuận. Tình trạng kỳ dùng chung `RebateSettlement` với chiết khấu hợp đồng (F4b). Migration `V0_20_0_ThucNhanChietKhauSanLuong`. Test domain `VolumeRebateReceiptTests` (5), tích hợp `VolumeRebateReceiptTests`, Playwright `volume-rebate-receipt.spec`. Kiểm tra: `dotnet test` 574/574, Playwright 108/108 (một lần chạy cả bộ `volume-rebate.spec` lỗi chập chờn, chạy riêng và chạy lại cả bộ đều qua), `npm run build`, `tsc`, `oxlint` qua. | `feature/g6b-thuc-nhan-ckb` |
| G6a (08/11/2026) | **Chiết khấu bậc theo sản lượng gộp** (design.md 18.25): thỏa thuận **CKB** của tổng công ty (NCC × nhóm hàng, công ty áp dụng, kỳ Tháng/Quý/Năm/Cả thỏa thuận, cơ sở Số lượng – cùng ĐVT / Doanh số, bậc ngưỡng → % tăng dần, bậc mục tiêu, Ghi chú, Đính kèm), duyệt APRL loại CKB, hủy có lý do, chép thành thỏa thuận mới; chặn trùng NCC × mặt hàng × công ty × thời hạn. Tab **Lũy kế**: PO Đã duyệt/Đã đóng VND các công ty theo kỳ, bậc toàn phần, CK phải nhận, chia theo tỷ lệ doanh số (dồn số lẻ), dự báo, sắp chạm bậc (X%), nguy cơ/hụt bậc mục tiêu; công ty áp dụng chỉ thấy phần mình. Màn **CKBT** theo dõi kỳ hiện tại. Quyền `Pricing.VolumeRebate.*`, vai trò mẫu. Migration `V0_20_0_ChietKhauSanLuongGop`. Test domain 8, tích hợp `VolumeRebateTests`, Playwright `volume-rebate.spec`. Quy tắc tự đặt ghi ở design.md (bậc toàn phần, % trên doanh số, X = 10%, dự báo theo nhịp mua, thành viên chỉ thấy phần mình). | `feature/g6a-chiet-khau-bac` |
| G5c+ (07/11/2026) | **SL kế hoạch chính xác hơn** (người dùng duyệt): khoảng theo **ngày** thay cho tháng – tháng đầu/cuối không trọn chia theo số ngày (15/01 tính 17/31), tối đa 36 tháng tính theo ngày; hộp thoại ghi số tháng và số ngày được tính. **SL lẻ** theo ĐVT dòng: tam giác vàng + chọn Làm tròn lên (mặc định) / Đổi dòng sang ĐVT cơ sở (chỉ RFQ) / Giữ số lẻ; cột SL theo ĐVT cơ sở, SL áp dụng. API: `FromDate/ToDate`, kết quả thêm `BaseUomId/BaseUomCode/BaseQty`. Mặc định hợp đồng = đúng hiệu lực, RFQ = trọn 12 tháng từ tháng sau. Không migration; design.md 18.25 | feature/sl-ke-hoach-chia-ngay |
| G5c (07/11/2026) | **Lấy SL kế hoạch** làm SL dự kiến: nút "Lấy SL kế hoạch" trên lưới RFQ và hợp đồng nguyên tắc (quyền `Purchase.DemandPlan.View`), hộp thoại chung `PlannedQuantityDialog` – từ tháng/đến tháng (≤ 36 tháng, qua nhiều năm), toàn tập đoàn khi có quyền ViewAllCompanies (RFQ ở tổng công ty bật sẵn, hợp đồng tắt), xem trước SL hiện tại ↔ SL kế hoạch theo công ty/số phiếu, áp dụng chép vào lưới (vẫn phải Lưu). API `POST /api/demand-plans/quantities` (phiếu Đã chốt, quy ĐVT cơ sở rồi về ĐVT dòng, 4 số lẻ). CSS: cột số trong bảng của hộp thoại dùng phông đơn cách. Không migration; design.md 18.25 | feature/lay-sl-ke-hoach |
| G5b (07/11/2026) | **Tổng hợp nhu cầu gộp** (màn hình KHNTH, Mua hàng → Tổng hợp nhu cầu, `GET /api/demand-plans/summary?year=&categoryId=&allCompanies=`): chỉ phiếu Đã chốt, SL quy ĐVT cơ sở (SL × hệ số, 4 số lẻ); lưới mặt hàng × Tổng + một cột mỗi công ty, ô Kỳ (cả năm/quý/tháng), lọc năm, nhóm hàng (gồm nhóm con); ngăn kéo tháng/quý/năm × công ty + số phiếu; đầu báo cáo nêu công ty còn phiếu nháp/chưa có kế hoạch. **Quyền mới `Purchase.DemandPlan.ViewAllCompanies`** "Xem kế hoạch nhu cầu toàn tập đoàn" (người dùng yêu cầu giữa lát; vai trò mẫu TRUONGBANCUVT, DAMPHAN, KSNB) – không có quyền chỉ xem công ty đang làm việc, kể cả ở tổng công ty. Không migration; design.md 18.25. Kiểm tra: `dotnet build` 0 cảnh báo, `dotnet test` 557/557, Playwright 105/105, build/tsc/oxlint qua. **Cần gán quyền mới cho vai trò đang dùng** (vai trò mẫu chỉ áp khi tạo/cập nhật bộ vai trò mẫu) | feature/tong-hop-nhu-cau |
| G5a (07/11/2026) | **Phiếu kế hoạch nhu cầu** (màn hình KHN, Mua hàng → Kế hoạch nhu cầu, số `KHN-…`): mỗi công ty lập theo năm (năm nay → +2) × nhóm hàng; dòng mặt hàng × ĐVT, SL 12 tháng, tổng quý/cả năm tự tính; Nháp → Đã chốt (không duyệt); Điều chỉnh = phiếu nháp mới, chốt xong phiếu cũ Đã thay; hủy bắt buộc lý do; mỗi mặt hàng × năm × công ty một phiếu còn hiệu lực; quyền `Purchase.DemandPlan.View/Manage` (vai trò mẫu DAMPHAN lập), đính kèm, tra cứu chứng từ. `LineGrid` thêm `scrollX` (giữ độ rộng cột, cuộn ngang – chỉ bật cho lưới 12 tháng). Migration `V0_20_0_KeHoachNhuCau`; design.md 18.25, ui-style.md 5. Kiểm tra: `dotnet build` 0 cảnh báo, `dotnet test` 553/553, Playwright 103/103, build/tsc/oxlint qua | feature/ke-hoach-nhu-cau |
| NCC-MR (07/11/2026) | **Mở rộng NCC cho công ty** (yêu cầu người dùng: NCC000026 không chọn được ở PO của APH vì chưa áp dụng tại APH; một NCC bán cho nhiều công ty là bình thường – mở rộng, không tạo mới): ngăn kéo NCC thêm tab "Mở rộng cho công ty" – bảng mọi công ty với tình trạng Đang giao dịch/Ngừng/Chưa mở rộng, mã tại công ty, ngày bắt đầu; nút "Mở rộng cho công ty…"/"Ngừng giao dịch…" mở hộp thoại chọn một, vài hoặc tất cả công ty, chỉ ở tổng công ty với quyền `MasterData.Supplier.Manage`; mở lại giữ mã/ngày công ty đã khai. API `GET/PUT /api/suppliers/{id}/extensions`; không migration; ngoại lệ filter công ty ghi ở design.md 5.9, 9.2. Kiểm tra: `dotnet build` 0 cảnh báo, `dotnet test` 556/556, Playwright `supplier-links` + `suppliers` 4/4, build/tsc/oxlint qua. Bản test 5007 (0.19.0) chưa có – cần phát hành bản sau; trước mắt APH vẫn tự áp dụng ở tab "Áp dụng tại APH" | feature/ncc-mo-rong-cong-ty |
| NCC-NH (07/11/2026) | **Ngành hàng NCC chọn từ danh mục Nhóm hàng** (người dùng chọn cách A): bảng `SupplierCategory` (NCC × nhóm hàng, nhật ký), chọn nhiều nhóm ở ngăn kéo NCC, nút "Thêm nhóm hàng" tạo ngay; lưu không cần duyệt; danh sách NCC có cột + lọc ngành hàng (gồm nhóm con); Excel cột Ngành hàng = mã nhóm; chữ `SupplyScope` cũ ngừng dùng (chỉ xem). **Danh mục nhóm hàng ai cũng xem đủ** (cờ `IsInScope`, nhóm ngoài phạm vi chỉ xem; mặt hàng/tồn kho/chứng từ vẫn lọc phạm vi – CLAUDE.md mục 4 ghi ngoại lệ); **chặn trùng tên nhóm trong cùng nhóm cha** (so không dấu) + cảnh báo ngay khi gõ. Sửa lỗi tiềm ẩn: Esc đóng hộp thoại F1 đóng luôn ngăn kéo (DetailDrawer nghe Esc ở pha capture). Test: tên nhóm hàng trong test thêm hậu tố ngẫu nhiên. Migration `V0_20_0_NganhHangNcc`; design.md 5.3, 5.7, 5.9, 5.10, 9.2. Kiểm tra: `dotnet build` 0 cảnh báo, `dotnet test` 549/549, Playwright 102/102, build/tsc/oxlint qua | feature/nganh-hang-ncc |
| NCC-SP2 (07/11/2026) | **Mã hàng theo NCC + ĐVT mua cho PR, RFQ**: PR lấy ĐVT mua theo NCC đề xuất của dòng, cột "Mã hàng NCC"; hộp thoại báo giá RFQ có cột "Mã hàng NCC" của NCC đó; API riêng của PO thay bằng API tra chung `/api/supplier-items/lookup` (chỉ cần đăng nhập, lọc nhóm hàng); design.md 5.9. Không migration mới. Kiểm tra: `dotnet test` 546/546, Playwright 101/101, build/tsc/oxlint qua | feature/pr-rfq-ma-hang-ncc |
| TIM-* (07/11/2026) | **Đồng bộ ô tìm `*` mọi form** (yêu cầu người dùng): `Select` cổ điển mặc định bật tìm (22 màn hình còn dùng `Select` antd chuyển sang – ô lọc nhóm NCC, kho, công ty, trạng thái… gõ được); `TreeSelect` cổ điển mới cho 9 ô cây nhóm hàng (`*`, không dấu); ô lệnh gõ `*` liệt kê mọi màn hình, Enter không tự mở; ô mặt hàng NCC độc quyền báo đúng; ui-style.md 5b. Máy chủ đã hiểu `*` từ trước. Kiểm tra: `dotnet test` 546/546, Playwright 101/101, build/tsc/oxlint qua | fix/tim-sao-toan-bo |
| NCC-SP (07/11/2026) | **Mã hàng theo NCC và ĐVT mua có tác dụng** (người dùng chọn phương án A): hộp thoại "Thêm mặt hàng NCC bán" sửa lỗi gõ `*` không liệt kê mặt hàng, ĐVT mua tự điền ĐVT cơ sở, nhãn + chú thích rõ; PO chọn mặt hàng tự lấy ĐVT mua của NCC, cột "Mã hàng NCC", máy chủ chép mã vào dòng PO khi lưu/gửi (PO đã gửi giữ mã), phiếu in PO ghi mã NCC; API `/api/purchase-orders/supplier-items`; migration `V0_20_0_MaHangNccTrenPo` (chưa phát hành); design.md 5.9. Kiểm tra: `dotnet build` 0 cảnh báo, `dotnet test` 546/546, Playwright 100/100, `npm run build`, `tsc`, `oxlint` qua. Còn chờ người dùng quyết: ngành hàng NCC chọn từ danh mục Nhóm hàng hay danh mục riêng; gán sản phẩm có cần duyệt không | feature/ma-hang-dvt-ncc |
| Rà soát R1–R8 (06/11/2026) | Ghi sổ đồng thời (khóa dòng), chống ghi đè (xmin + If-Match → 409), mỗi cấp duyệt một người, tham số nhập mua theo PO, RTV trừ SL đã nhận PO, đóng phần còn lại PR/PO, RFQ hạn nộp/gia hạn/giải trình ít báo giá, lý do hủy phiếu đã duyệt, build 0 cảnh báo; 4 migration 0.19.0 (`KhoaSuaTrung` không SQL, `NhapMuaTheoPo`, `DongPhanConLai`, `RfqHanNopLyDoHuy`) – chưa phát hành. Kiểm tra trên `main`: `dotnet build` 0 cảnh báo, `dotnet test` 545/545, Playwright 100/100, `npm run build`, `tsc`, `oxlint` qua | 21e0232 … f176790 |
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
| DUYET-NCC | **Popup xem trước duyệt NCC khi lưu hồ sơ** (người dùng yêu cầu 03/11/2026): chạy thử lệnh lưu NCC rồi hủy, liệt kê yêu cầu duyệt sẽ sinh + người duyệt từng chặng/công ty; không có thay đổi cần duyệt thì lưu thẳng. **Bảng giá toàn tập đoàn: công ty khác xem được lịch sử duyệt** (đổi quy tắc design.md 8.5). **Sửa lỗi cũ**: đổi cả trạng thái lẫn tài khoản ngân hàng NCC trong một lần lưu bị trùng số yêu cầu (409). Cấp chưa gán vai trò liệt kê mọi người có quyền – người dùng đồng ý, gán vai trò ở APRL. Không migration | feature/xem-truoc-duyet-ncc |
| DUYET-HIEN | **Hiện người duyệt trên mọi phiếu có duyệt** (yêu cầu người dùng 03/11/2026): trạng thái "Chờ duyệt: tên"; cây lịch sử phê duyệt (lập phiếu → lượt gửi → từng cấp, cấp sau "Sẽ duyệt: …", ngày giờ); popup Gửi duyệt liệt kê người duyệt (server chạy thử rồi hủy – `POST /api/approvals/preview`); popup Duyệt nêu người lập + người duyệt sau mình; migration `V0_17_0_ThoiDiemGuiDuyet`; design.md 18.6. Chưa có popup xem trước khi lưu hồ sơ NCC (duyệt NCC gửi tự động) | feature/hien-nguoi-duyet |
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

- **Đợt Z – quy tắc chung chứng từ (người dùng chốt 11/11/2026, design.md mục 20)**: Z1 ý kiến + đính kèm từng bước duyệt, khóa file
  sau duyệt → Z2 ổ khóa vàng từng bước + "Kiểm tra phê duyệt" (bước 0…n, tên đăng nhập, họ tên, chức vụ, giờ ngày) + trường Chức vụ →
  Z3 ô số chứng từ (sao chép, bấm đúp mở phiếu gốc) → Z4 quyền Hủy đặc biệt **chỉ cấp cho admin** (người lập không hủy được, bắt buộc lý
  do, kể cả nháp) + hủy dây chuyền từ phiếu cuối về đầu có hộp thoại trình tự. Làm trước các lát Y của 0.23.0.
  - **Z1 (nhánh `feature/z1-y-kien-dinh-kem-duyet`)**: backend – `Attachment.ApprovalStepId`, `AddedAfterApproval` (migration
    `V0_23_0_DinhKemBuocDuyet`), `AttachmentService` quy tắc khóa D1, tải file bước duyệt chỉ cần quyền xem (`approvalStep=true`); giao
    diện – `approvalDialogs.tsx` + `ApprovalInput.tsx` (ý kiến + file), áp cho PR, PO, RFQ, bảng giá, hợp đồng, phụ lục, NCC độc quyền,
    duyệt tháng, chiết khấu sản lượng, yêu cầu duyệt NCC (chỉ ý kiến); tab Đính kèm cột "Bước duyệt", "Đã khóa". `queryClient` tách ra
    `app/queryClient.ts` (hộp thoại nằm ngoài QueryClientProvider). Test: tích hợp `ApprovalAttachmentTests` (2), e2e
    `approval-attachments.spec.ts`; `dotnet test` 642/642.
  - **Z2 (nhánh `feature/z2-o-khoa-kiem-tra-duyet`)**: backend – `User.Position` (Chức vụ, migration `V0_23_0_ChucVuNguoiDung`), DTO/validator
    người dùng; `ApprovalStepDto` thêm tên đăng nhập + chức vụ người duyệt/người gửi (chức vụ trống → tên vai trò tại công ty của bước).
    Giao diện – `classic/ApprovalTrail.tsx` (`ApprovalLocks` ổ khóa vàng đóng/mở theo bước của lượt gần nhất, `ApprovalCheckButton` hộp thoại
    Bước 0…n), gắn qua prop `approval` của `DocumentForm`/`DetailDrawer` cho PR, PO, RFQ, hợp đồng, phụ lục, bảng giá, NCC độc quyền, duyệt
    tháng, CKB, yêu cầu duyệt NCC; Người dùng có ô + cột Chức vụ. design.md 20 (đoạn "Lát Z2"), ui-style.md mục 5d. Test: tích hợp
    `KiemTraPheDuyet_TenDangNhap_ChucVu_RongThiLayVaiTro`, e2e `approval-check.spec.ts`; `dotnet test` 643/643, build 0 cảnh báo, tsc, oxlint, `npm run build` qua. Playwright đủ bộ 126/126 (auth, po-list chạy lại trên Vite mới cổng
    5174 – Vite 5173 còn chạy từ 0.20.0; `suppliers.spec` sửa bộ chọn nút "Duyệt" thành `exact` vì khớp cả nút "Kiểm tra phê duyệt"). **Đã merge.**
  - **Z3 (nhánh `feature/z3-o-so-chung-tu`)**: `classic/DocNo.tsx` – ô số chứng từ (bấm sao chép, bấm đúp/Enter mở phiếu gốc qua
    `documentPath`, thêm `DHG`); đầu mọi `DocumentForm`, số liên quan trên form PR/PO/RFQ/đặt hàng gộp/NCC; người dùng yêu cầu thêm
    **danh sách và báo cáo** → `DataGrid` cột `docRef` (bấm đúp ô mở phiếu gốc, không mở dòng). Backend: Id phiếu gốc trên dòng PO, dòng RFQ,
    `RfqAwardPriceDto.RfqId`. design.md 20 (đoạn "Lát Z3"), ui-style.md 5e. Test: `PurchaseOrderPriceSourceTests` thêm kiểm tra Id, e2e
    `doc-no.spec.ts`; e2e `purchasing`, `suppliers` sửa theo ô số chứng từ, `three-way-match` bấm đúp đúng ô số XNH. `dotnet test` 643/643, build 0
    cảnh báo, tsc, oxlint, `npm run build`; Playwright đủ bộ 127/127 (Vite mới cổng 5174). **Đã merge.**
  - **Z4a (nhánh `feature/z4a-quyen-huy-phieu`)**: quyền `System.Document.Cancel` chỉ ADMIN (ẩn khỏi phân quyền, chặn gán);
    `Application/Common/DocumentCancelPolicy` (nháp – người lập; còn lại – quản trị; bắt buộc lý do) áp cho 15 lệnh hủy + cờ CanCancel;
    migration `V0_23_0_LyDoHuyPhieu` (CancelReason cho hợp đồng, phụ lục, RFQ, tìm nguồn); người dùng yêu cầu thêm **Rút về nháp** → PR, PO
    `POST …/recall` + nút trong `useApprovalCommands`. Giao diện: hộp thoại hủy luôn hỏi lý do (PR, PO, hợp đồng, phụ lục, RFQ, tìm nguồn).
    Test: tích hợp `DocumentCancelPolicyTests` (2), sửa 10 test cũ (gửi lý do, danh mục quyền −1, HSN đã gửi → 403); e2e `recall-cancel.spec.ts`. `dotnet test` 645/645, build 0 cảnh báo, tsc,
    oxlint, `npm run build`; Playwright đủ bộ 128/128. **Đã merge.**
  - **Z4b (nhánh `feature/z4b-huy-day-chuyen`)**: hủy dây chuyền PR → PO → XNH – `Application/Common/CancelChain.cs`,
    `Infrastructure/Purchasing/CancelChainService.cs`, `GET|POST /api/cancel-chain/{loại}/{id}`; người dùng chốt: chuỗi có XNH đã xác nhận thì
    **chặn hủy, chỉ điều chỉnh**. Domain: `ApprovableDocument.Cancel(reason, inChain)` (PR đã đóng hủy được trong chuỗi). Giao diện:
    `api/cancelChain.ts`, `pages/purchasing/cancelChainDialog.tsx` cho PR, PO, XNH. Sửa cảnh báo XML doc còn sót của Z4a. Test: tích hợp
    `CancelChainTests` (2), e2e `cancel-chain.spec.ts`. Đợt sau: chuỗi RFQ, bảng giá → hợp đồng → phụ lục, phiếu tháng / đặt hàng gộp → PO.
    Build 0 cảnh báo, `dotnet test` 647/647, tsc, oxlint, `npm run build`; Playwright đủ bộ 129/129. **Đã merge.**
  - **Z4c (nhánh `feature/z4c-huy-day-chuyen-mo-rong`)**: người dùng đồng ý cả 4 đề xuất E1–E4 (design.md 20.2). Hủy dây chuyền DHG → PO
    (mọi công ty) → XNH; RFQ → PO → XNH + hợp đồng / bảng giá nháp tạo từ RFQ (đã duyệt thì chặn); bảng giá thêm trạng thái Đã hủy (migration
    `V0_23_0_HuyBangGia`); phiếu tháng đang được DHG / PO dùng thì chặn hủy. Giao diện DHG, RFQ dùng hộp thoại trình tự. Test: tích hợp
    `CancelChainRfqTests`, bổ sung E1 + E4 vào `PooledOrderTests`; e2e `cancel-chain.spec.ts` thêm hủy RFQ đã gửi NCC (chuỗi một phiếu – không
    hiện bảng, lý do bắt buộc). Sửa e2e `administration.spec.ts`: CSDL test đã 567 tài khoản, lưới cuộn ảo không vẽ dòng `thukho.bha` → lọc
    bằng ô Tìm kiếm. Build 0 cảnh báo, `dotnet test` 648/648, tsc, oxlint, `npm run build`; Playwright đủ bộ 129/130 (Vite 5174) – test lỗi
    duy nhất là `administration` ở trên, sửa xong chạy lại cả file qua 7/7; `notifications.spec` có lượt lỗi khi chạy song song, chạy riêng qua.
    **Đã merge.** Đợt Z (Z1–Z4c) hoàn tất – tiếp các lát Y của 0.23.0.
- **Lát Y2 – công trình và dự toán (11/11/2026, nhánh `feature/y2-cong-trinh-du-toan`)** – bắt đầu 0.23.0 (design.md 19.3 đoạn "Lát Y2").
  Backend: `ConstructionProject` + `ConstructionProjectEstimateRevision` (lần 0 ban đầu, điều chỉnh bắt buộc lý do, chỉ thêm), API
  `/api/construction-projects` (+ `/options`, `/{id}/estimate-revisions`), quyền `Purchase.Project.View/Manage` (xem thêm vào các vai trò
  mẫu có xem ngân sách; quản lý chỉ quản trị gán), đính kèm `ConstructionProject`, migration `V0_23_0_CongTrinhDuToan`. Giao diện: màn hình
  **CTDT** (Mua hàng → Công trình – dự toán), ngăn kéo + tab Dự toán + hộp thoại "Điều chỉnh dự toán". Test: domain + tích hợp
  `ConstructionProjectTests` (12), e2e `construction-projects.spec.ts`. `dotnet test` 660/660, tsc, oxlint, `npm run build` qua.
  **Môi trường**: API dev cổng 5000 (PID 23848, chạy từ `bin\Debug` từ phiên trước) giữ DLL – không được phép dừng nên build/test bằng
  `--artifacts-path` ra thư mục scratchpad, migration tạo bằng `--configuration Release`; e2e chạy trên API thử cổng 5012 (Testing, bản
  artifacts) + Vite 5186 (cổng 5182 đã có Vite cũ). Lũy kế đã mua / còn lại của công trình làm ở **Y3**.
- **Lát Y3a – loại mua + công trình trên đề nghị mua (11/11/2026, nhánh `feature/y3a-loai-mua-cong-trinh`)** – design.md 19.3 đoạn "Lát Y3a".
  Y3 tách 3 lát: Y3a (xong) → **Y3b** Thủ kho nhập tồn (Cần sử dụng, Tồn, Cần mua) → **Y3c** chuỗi duyệt theo loại mua + chặng vượt dự toán.
  Người dùng chốt: **mọi khoản mua cho công trình bắt buộc qua đề nghị loại A** (lũy kế chỉ tính dòng PO lấy từ đề nghị A). Backend:
  `PurchaseCategory` (Construction/Goods/Service), `PurchaseRequest.Category` + `ConstructionProjectId` (migration `V0_23_0_LoaiMuaDeNghi`),
  công trình phải đang thực hiện (phiếu nháp đã gắn thì lưu được, chặn gửi duyệt), `Infrastructure/Purchasing/ConstructionSpending.cs` (lũy kế
  PO đã duyệt/đã đóng, chưa VAT + VAT), `PurchaseRequestDto.Category/Construction` (`ConstructionBudgetDto`), cột Category ở danh sách PR,
  `ConstructionProjectDto.SpentAmount/SpentAmountWithVat/RemainingAmount`. Giao diện: form PR ô Loại mua + Công trình (điền sẵn phòng ban của
  công trình), `ConstructionBudgetNotice.tsx`, cột Loại mua ở danh sách PR, cột lũy kế/còn lại ở CTDT + ô chỉ đọc trong ngăn kéo. Test: domain
  `PurchaseRequestCategoryTests` (2), tích hợp `ConstructionPurchaseTests` (2), e2e `purchase-category.spec.ts`. Build 0 cảnh báo, `dotnet test`
  664/664, tsc, oxlint, `npm run build`; Playwright đủ bộ 132/132 (API thử 5012 Testing bản artifacts + Vite 5186).
- **Lát Y3b – Thủ kho nhập tồn trên đề nghị mua (11/11/2026, nhánh `feature/y3b-thu-kho-nhap-ton`)** – design.md 19.3 đoạn "Lát Y3b";
  người dùng đồng ý kế hoạch. Dòng PR: SL cần sử dụng (`RequiredQty`), Tồn (`StockQty`), Cần mua = max(cần − tồn, 0) là `Qty` cũ (PO, RFQ,
  ngân sách, định mức theo cần mua); đầu PR `StockConfirmedBy/At`; migration `V0_23_0_ThuKhoNhapTon`. Quyền `Purchase.PR.EnterStock` (vai trò
  mẫu KHO + xem PR); chỉ A/B, chờ duyệt chưa có cấp nào duyệt trong lượt, nơi nhận trong phạm vi kho; `POST /api/purchase-requests/{id}/stock`;
  gợi ý tồn đã xác nhận gần nhất cùng nơi nhận; PR thôi đọc `StockOnHand`. Giao diện: cột SL cần sử dụng / Tồn (thủ kho) / Cần mua, nút
  "Nhập tồn" (icon xanh) + `StockEntryDialog.tsx`, thông báo đã/chưa xác nhận tồn, ô "Nơi nhận (kho)". Test: domain `PurchaseRequestStockTests`
  (4), tích hợp `ThuKhoNoiNhan_NhapTon_...`, e2e `pr-stock-entry.spec.ts`; `purchasing.spec` sửa nhãn ô SL. Build 0 cảnh báo, `dotnet test`
  669/669, tsc, oxlint, `npm run build` qua; Playwright đủ bộ 133/133 (API thử cổng 5013 Testing, bản artifacts trong scratchpad + Vite 5187). **Đã merge.**
- **Lát Y3c – chuỗi duyệt theo loại mua, bước Thủ kho, chặng vượt dự toán (11/11/2026, nhánh `feature/y3c-chuoi-duyet-loai-mua`)** –
  design.md 19.3 đoạn "Lát Y3c". Bộ cấp **PRA** (loại A), **PRS** (dịch vụ), **PRE** (vượt dự toán, chặng 6) – chưa cấu hình PRA/PRS thì dùng PR.
  Loại người duyệt **Storekeeper** "Thủ kho nơi nhận" (chỉ PR, PRA): quyền nhập tồn + phạm vi kho nơi nhận (không cần quyền duyệt PR – API
  approve/reject PR nhận cả quyền nhập tồn), phải nhập tồn trong lượt rồi mới Duyệt (`ApproveReadyBlocker`), không ủy quyền; dịch vụ tự bỏ cấp;
  chặn gửi khi thiếu nơi nhận / chưa có thủ kho; thông báo "chờ bạn nhập tồn"; lịch sử "Thủ kho KT". Migration `V0_23_0_BuocThuKho`
  (`ApprovalStep.WarehouseId`). **Người dùng yêu cầu giữa lát: "phần vượt được xem trước khi trình lên Chủ tịch"** → đề nghị mua có chặng vượt
  (PRB, PRE) thì cấp cao nhất của chuỗi thường dời sang chặng 9 (sau chặng vượt); PO giữ thứ tự cũ. Mẫu CUVT: PR 8 cấp, PRA 7, PRS 7, PRE
  GDTCKT (vai trò mẫu GDTCKT thêm quyền duyệt PR). Giao diện: APRL ô Người duyệt thêm "Thủ kho nơi nhận", nhãn chặng 6 "Vượt dự toán".
  Test: domain `CapThuKhoNoiNhan_…`, tích hợp `PurchaseRequestChainTests` (2), sửa `BudgetTests` (thứ tự chặng), e2e `pr-storekeeper-step.spec.ts`;
  `approval-rule-template.spec` (32 cấp), `administration.spec` (gõ tìm loại phiếu – danh sách cuộn ảo). Build 0 cảnh báo, `dotnet test`
  672/672, tsc, oxlint, `npm run build`; Playwright đủ bộ 134/134 (API thử 5014 Testing bản artifacts scratchpad + Vite 5188). Hướng dẫn sử
  dụng 08-he-thong cập nhật. **Đã merge.** Tiếp theo: các lát còn lại của 0.23.0 (Y5 bảng tổng hợp 2A/2B, Y10 khung in…).
- **Lát Y5 – bảng tổng hợp đề xuất mua 2A/2B (11/11/2026)** – design.md 19.3 đoạn "Lát Y5"; tách **Y5a** danh mục đơn vị mua → **Y5b** phiếu BTH
  backend + duyệt (bộ cấp BTH, mẫu CUVT TP CUVT → GĐ khối → Phó CT UBCL&PT → Chủ tịch) → **Y5c** giao diện phiếu, tham số "bắt buộc qua bảng
  tổng hợp", hủy dây chuyền.
  - **Y5a (nhánh `feature/y5a-don-vi-mua`)**: `PurchasingUnit` (mã 2–20 không đổi, ngừng dùng thay xóa), migration `V0_23_0_DonViMua`, API
    `/api/purchasing-units` (+ `/options` mở cho quyền xem PR), quyền `Purchase.Unit.View/Manage` (vai trò mẫu: xem trong nhóm xem nghiệp vụ mua,
    NVCUVT quản lý). Giao diện màn hình **DVM** (Mua hàng → Đơn vị mua) `PurchasingUnitListPage` + `PurchasingUnitDrawer`, `api/purchasingUnits.ts`;
    hướng dẫn 02-danh-muc mục 2.4. Test: domain `PurchasingUnitTests` (6), tích hợp `PurchasingUnitTests` (2), e2e `purchasing-units.spec.ts`.
    Build 0 cảnh báo, `dotnet test` 680/680, tsc, oxlint, `npm run build`; Playwright đủ bộ 135/135 (API thử 5015 Testing bản artifacts scratchpad
    + Vite 5189). **Đã merge.** Tiếp: **Y5b**.
  - **Y5b (nhánh `feature/y5b-bang-tong-hop`)**: backend phiếu **BTH** – `PurchaseSummary` + dòng (migration `V0_23_0_BangTongHopDeXuat`), 2A cùng
    công trình / 2B đề nghị B, DV; cùng bộ phận; dòng đề nghị đã duyệt còn cần mua, mỗi dòng một bảng chưa hủy (khóa đề nghị), đơn vị mua từng dòng;
    khung dự toán / ngân sách đầu phiếu; bộ cấp **BTH** (mẫu CUVT 4 cấp, bộ CUVT 36 cấp), quyền `Purchase.Summary.View/Manage/Approve` + vai trò mẫu;
    API `/api/purchase-summaries`; nối Việc cần làm, nhắc duyệt, xem trước chuỗi, tra cứu chứng từ, đính kèm; PR trong bảng chưa hủy thì chặn hủy
    (cả hủy dây chuyền). Web: hằng quyền, nhãn BTH ở tra cứu; e2e `approval-rule-template` (36 cấp), `document-numbering` (23 loại). design.md 19.3
    đoạn "Lát Y5b". Test: domain `PurchaseSummaryTests` (4), tích hợp `BangTongHop2B_…`, `BangTongHop2A_…`; sửa danh sách loại duyệt ở 2 test cũ.
    Build 0 cảnh báo, `dotnet test` 686/686, tsc, oxlint, `npm run build`; Playwright đủ bộ 135/135 (API thử 5016 Testing bản artifacts scratchpad +
    Vite 5191). Tiếp: **Y5c** (giao diện phiếu, tham số bắt buộc qua bảng tổng hợp, hủy dây chuyền).
  - **Y5c-1 (nhánh `feature/y5c-bat-buoc-bang-tong-hop`, 12/11/2026)**: người dùng chốt "công ty nào cũng phải qua bảng tổng hợp, bật sẵn khi
    phát hành" (thay Q3 "bật/tắt bằng tham số"; dữ liệu hiện có là dữ liệu thử). Dòng đề nghị chỉ lên RFQ/PO khi thuộc bảng tổng hợp **đã duyệt**:
    kiểm ở `PurchaseOrderService.CheckRequestQuantitiesAsync` (lưu + gửi PO), `RfqService.ResolveLinesAsync`, lọc `OpenLinesAsync`
    (`PurchaseSummaryService.ApprovedSummaryLineIds` / `EnsureApprovedSummaryAsync`). Hủy bảng đơn lẻ chặn khi dòng đã lên RFQ/PO chưa hủy. Hủy dây
    chuyền: PR → BTH → PO → XNH và gốc BTH (`/api/cancel-chain/BTH/{id}`), RFQ chưa hủy thì chặn. Web: chú thích + chữ trống hộp chọn dòng PR trên
    PO. design.md 19.3 (Q3, "Lát Y5c-1"). Test: helper `ProcApiFactory.ApproveSummaryForRequestAsync` (bảng đã duyệt trong CSDL) cho 12 test cũ
    lấy dòng PR lên PO; `BangTongHop2B_…` viết lại theo quy tắc mới (PO/RFQ bị chặn khi bảng nháp, hủy bảng chặn khi đã lên PO); `LuongPrPo…`
    thêm kiểm chặn; mới `HuyDayChuyen_TuBangTongHop_…`; e2e `purchasing.spec` lập + duyệt bảng qua API (`e2e/summaryHelpers.ts`). Build 0 cảnh
    báo, `dotnet test` 687/687, tsc, oxlint, `npm run build`; Playwright đủ bộ 134/135 lần đầu (lỗi đúng ở `purchasing.spec` – PO từ PR chưa qua
    bảng), sửa xong chạy lại `purchasing.spec` 3/3 (API thử 5016 Testing bản artifacts scratchpad + Vite 5191). Tiếp: **Y5c-2** (giao diện phiếu BTH, trang duyệt gọn, hộp hủy dây chuyền).
  - **Y5c-2 (nhánh `feature/y5c2-giao-dien-bang-tong-hop`, 12/11/2026)**: màn hình **BTH** (Mua hàng → Bảng tổng hợp đề xuất) – danh sách +
    form `PurchaseSummaryPages.tsx`, `api/purchaseSummaries.ts`; hộp "Chọn dòng đề nghị" (Thêm dòng/F2 qua thuộc tính mới `LineGrid.addLines`)
    chọn sẵn đơn vị mua; đổi biểu mẫu/bộ phận/công trình thì bỏ dòng; khung chi phí đầu phiếu; tab Lịch sử duyệt, Đính kèm; lệnh qua
    `useApprovalCommands` loại BTH (đính kèm của người duyệt gắn `PurchaseSummary`, Hủy = hộp hủy dây chuyền). Nối Việc cần làm, `documentPath`,
    trang duyệt gọn `/duyet/BTH/{id}` (khối `SummaryDetail`). **Sửa lỗi** xem trước chuỗi duyệt BTH lỗi 500 (lệnh gửi mở giao dịch thứ hai) →
    `RowLocks.BeginOrJoinAsync`. In mẫu 2A/2B chuyển sang Y10. design.md 19.3 "Lát Y5c-2", hướng dẫn 03-mua-hang mục 3.3a. Test: tích hợp
    `BangTongHop2B_…` thêm kiểm xem trước chuỗi duyệt (đỏ trước khi sửa); e2e mới `purchase-summary.spec.ts` (lập 2B → gửi → duyệt trên điện
    thoại → hủy). Build 0 cảnh báo, `dotnet test` 687/687, tsc, oxlint, `npm run build`; Playwright đủ bộ 136/136 (API thử 5017 Testing bản
    build scratchpad + Vite 5192). Tiếp: **Y10** (khung mẫu in + in 1A/1B/DV, 2A/2B).
- **Lát Y10 – khung mẫu in biểu mẫu CUVT (12/11/2026, nhánh `feature/y10-mau-in-cuvt`)** – design.md 19.3 "Lát Y10", 10.10. Khuôn
  `Printing/CuvtFormLayout.cs` (Times New Roman / Liberation Serif / DejaVu Serif qua `PdfFonts.SerifFamily`, số biểu mẫu góc trên cỡ 25, tiêu
  đề canh giữa, cỡ chữ theo biểu mẫu × 0,8, bảng tiêu đề hai tầng, khối ký sinh từ chuỗi duyệt: chức danh, họ tên, giờ duyệt) + nội dung
  `Printing/CuvtPrintForms.cs`. PR in 1A (A), 1B (B), 1B bỏ cột tồn (DV) – thay khuôn chung; bảng tổng hợp in 2A/2B khổ ngang
  (`GET /api/purchase-summaries/{id}/pdf`, nút "In phiếu" trên form BTH), đơn vị mua mỗi đơn vị một cột "x"; dòng BTH thêm `RequestDate`,
  `RequestPurpose`. **Người dùng chốt giữa lát**: "Lũy kế số tiền đã mua trong tháng" (1B) chỉ tính PO đã duyệt trong tháng, chưa VAT, giống
  1A → `Purchasing/DepartmentSpending.cs`; 2B lũy kế = số đó + tổng đề xuất (theo ghi chú biểu mẫu 2B – tự áp cùng cách). Logo công ty chưa
  làm (chưa có trường logo). Test: tích hợp `InBieuMauCuvt_1A_1B_DichVu_2A`, `LuyKeTrongThang_1B_ChiTinhPoDaDuyet_ChuaVat`, sửa `PrintingTests`
  (tiêu đề + dòng PR theo mẫu 1B); e2e `purchase-summary.spec` thêm bấm In phiếu. Build 0 cảnh báo, `dotnet test` 689/689 (2 test thông báo lỗi
  khi chạy song song với Playwright, chạy lại riêng qua), tsc, oxlint, `npm run build`; Playwright đủ bộ 136/136 (API thử 5018 Testing bản
  build scratchpad + Vite 5193). Tiếp theo: các lát 0.24.0 (Y6 so sánh 3A / trình giá 3a, 3B; Y7 PO theo 4A/4B; X7) hoặc phát hành 0.23.0 khi
  người dùng yêu cầu.
- **Phát hành 0.23.0 (12/11/2026)**: tag `v0.23.0` – đợt Z (Z1–Z4c quy tắc chung chứng từ) + Y2, Y3a–c, Y5a–c, Y10. 10 migration (78 → 88),
  script `db/scripts/0.23.0.sql` (idempotent) chạy thử 2 lần trên bản sao `procurement_kiemtra_0230` (từ CSDL bản test 5007 0.22.2), không lỗi,
  số dòng PR/PO/AuditLog giữ nguyên. Build 0 cảnh báo, `dotnet test` 689/689. Đóng gói `publish.ps1 -NoInstall` →
  `artifacts\procurement-0.23.0.zip`; Release https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.23.0.
  **Bản test 5007 nâng lên 0.23.0 (12/11/2026)**: `app` = 0.23.0, `app-0.22.2` = bản cũ, gói `goi\0.23.0`, sao lưu
  `D:\project\procurement_kiemtra_0150_truoc_0.23.0.dump`; chạy `0.23.0.sql` (88 migration); PID 18204 trong `pid.txt`; `/health` Healthy 0.23.0
  (cả qua http://192.168.1.199:5007). Gán quyền mới qua API (script scratchpad `roles_0230.py`): vai trò trùng mã mẫu nhận quyền mới theo
  `RoleTemplates` (CHUTICH, GDKHOI, TPCUVT, PHOCTUBCL duyệt BTH; NVCUVT lập BTH + đơn vị mua; KHO nhập tồn; các vai trò khác xem), TOANQUYEN
  đủ 8 quyền, POLAP xem BTH + đơn vị mua. Chưa khai bộ cấp duyệt BTH, đơn vị mua, công trình (chưa cấu hình thì BTH duyệt một cấp) – người test
  tự khai hoặc dùng "Cấu hình duyệt mẫu…" bộ CUVT.
- **Lát Y6 – so sánh 3A / trình giá 3a, 3B (12/11/2026, nhánh `feature/y6-so-sanh-trinh-gia`, bản 0.24.0)** – design.md 19.3 "Lát Y6", Q7.
  RFQ có loại mua suy từ dòng đề nghị nguồn (A cùng một công trình, không trộn với B/DV; dòng gõ tay đi được cả hai), biểu mẫu chốt lúc chọn
  thầu: A ≥ ngưỡng → 3A (bắt buộc lý do chọn), A dưới ngưỡng → 3a, B → 3B; tham số công ty mới `RFQ_COMPARISON_THRESHOLD` (mặc định 0 = mọi A
  là 3A). **Giữ** quy tắc mời / chào giá tối thiểu NCC (trình giá 1 NCC vẫn cần giải trình). In `GET /api/rfqs/{id}/pdf` (`?form=3A`) khổ ngang:
  3A bảng so sánh NCC (cũ/mới, điện thoại, VAT, thanh toán, thời gian giao, đề xuất + lý do chọn); 3a/3B mỗi NCC trúng thầu một trang (giá cũ
  đã mua gần nhất, lũy kế, định mức, bằng chữ). Khuôn `CuvtFormLayout` thêm dòng tổng phụ / dòng chữ sau bảng / nhiều trang. Web: ô "Loại mua –
  biểu mẫu", "Lý do chọn (3A)", hỏi lý do chọn khi chọn thầu, nút In phiếu. Migration `V0_24_0_RfqBieuMau3`. Test: domain 2
  (`BieuMau_TheoLoaiMuaVaNguong`, `ChonThau_BieuMau3A_…`), tích hợp `Rfq_LoaiMuaTheoDeNghi_BieuMau3A_3a_3B_InKhoNgang`, e2e `rfq-deadline.spec`
  thêm bước In phiếu 3B, `system-parameters.spec` đếm 25 tham số. Kiểm tra: build 0 cảnh báo, `dotnet test` 692/692, tsc, oxlint,
  `npm run build`; Playwright đủ bộ 134/136 (API thử 5018 Testing + Vite 5193) – `volume-rebate-receipt` chập chờn chạy lại qua,
  `system-parameters` lỗi thật do thêm tham số (đã sửa, chạy lại qua). **Đã merge.** Tiếp theo: Y7 (PO theo 4A/4B), X7 (mua nội bộ qua PO).
- **Lát Y7 – PO theo bảng theo dõi mua hàng 4A / 4B (12/11/2026, nhánh `feature/y7-po-bieu-mau-4`, bản 0.24.0)** – design.md 19.3 "Lát Y7", Q10.
  PO có loại mua + công trình (`PurchaseOrder.Category`, `ConstructionProjectId`, migration `V0_24_0_PoBieuMau4` – điền lại PO cũ có dòng đề nghị
  A) tính mỗi lần lưu nháp theo dòng đề nghị nguồn như RFQ: A cùng một công trình, không trộn với B/DV. In `GET /api/purchase-orders/{id}/pdf`
  mặc định "BẢNG THEO DÕI MUA HÀNG" 4A/4B khổ ngang (`Printing/CuvtPoPrintForms.cs`): giá cũ gần nhất (hàm chung `LastPricesAsync` với 3a/3B),
  giá mới cột tăng / bằng–giảm, NCC, lũy kế = đã mua trước đây (không gồm chính đơn – `ConstructionSpending`/`DepartmentSpending.MonthDetailAsync`
  thêm tham số bỏ PO) + đơn này, còn lại theo dự toán / ngân sách tháng; ba cột cuối gộp ô dọc (`CuvtColumn.MergeRows`, bảng > 15 dòng không gộp).
  `?form=supplier` in đơn gửi NCC (khuôn cũ) – nút "In đơn gửi NCC"; form PO ô "Loại mua – biểu mẫu". Hướng dẫn 03-mua-hang mục 3.4. Test: domain
  `PurchaseOrderCategoryTests`, tích hợp `Po_LoaiMuaTheoDeNghi_BangTheoDoi4A_4B_LuyKeGomDonNay`, `PrintingTests` (đơn gửi NCC qua `?form=supplier`,
  4B 60 dòng sang trang), e2e `po-print-4ab.spec.ts`. Build 0 cảnh báo, `dotnet test` 694/694 (lần đủ bộ 693 + test domain mới chạy riêng), tsc, oxlint,
  `npm run build`; Playwright đủ bộ 137/137 (API Testing 5000 + Vite 5173 do Playwright tự bật). **Đã merge.** Tiếp theo: X7 (mua nội bộ qua PO).
- **Lát X7 – mua nội bộ qua PO (12/11/2026, nhánh `feature/x7-mua-noi-bo`, bản 0.24.0)** – design.md 19.3 "Lát X7". Người dùng chốt giữa lát
  **"cho nhập tay"**: giá bán nội bộ chỉ là gợi ý (tự điền), không chặn gửi, không bắt lý do lệch giá, không chặng POX/POL.
  - **Bước 1 (backend + giao diện, chưa e2e)**: Domain `PurchasePriceSource.Internal`, `PurchaseOrder.InternalSellerCompanyId` + `SetInternalSeller`
    (chặn NCC nội bộ của chính mình) + `ApplyInternalPrices`, `IsPriceDeviation` bỏ dòng Internal; entity `InternalSaleConfirmation` (thuộc công ty
    bán, mỗi PO một quyết định); quyền `Purchase.InternalSale.View/Manage` (vai trò mẫu: xem trong nhóm xem nghiệp vụ mua, NVCUVT quản lý);
    thông báo `InternalSaleOrder` (PO duyệt xong → công ty bán), `InternalSaleDecision` (→ người lập PO). Infrastructure `Pricing/InternalPriceLookup`
    (dùng chung với ICS cũ), `PurchaseOrderService.Internal.cs` (giá gợi ý, API approved-prices trả nguồn Internal, trạng thái bên bán trên PO),
    `InternalSaleService` + API `/api/internal-sales` (danh sách, chi tiết, confirm, reject); báo cáo PRCW nguồn "Internal" tính là mua theo quy
    định. Migration `V0_24_0_DonBanNoiBo` (điền lại công ty bán cho PO cũ). Web: `api/internalSales.ts`, màn hình **DBN**
    `pages/purchasing/InternalSalePages.tsx` (danh sách + ngăn kéo, `/mua-hang/don-ban-noi-bo/:id` từ thông báo), PO ô "Bên bán (mua nội bộ)",
    nguồn giá "Giá nội bộ (gợi ý)". Test: domain `InternalPurchaseTests` (3), tích hợp `InternalPurchaseTests` (1). Build 0 cảnh báo, `dotnet test`
    698/698 (trước khi thêm kiểm tra PRCW; chạy lại riêng qua), tsc, oxlint qua.
  - **Bước 2 (e2e + hướng dẫn)**: e2e `internal-purchase.spec.ts` (BHA mua của NMI – NCC nội bộ NMI dùng lại nếu CSDL test đã có; PO gõ tay
    16.000 khác giá gợi ý 15.000; NMI xác nhận ở DBN; PO hiện "Đã xác nhận"); ngăn kéo DBN chỉ khóa khi không được quyết định. Hướng dẫn 03-mua-hang
    mục 3.4 "Mua nội bộ", 06-kho ghi chú thay ICS/ICR. tsc, oxlint, `npm run build` qua; e2e mới qua (API Testing 5000 + Vite 5173 Playwright tự bật).
    Kiểm tra cuối: build 0 cảnh báo, `dotnet test` 698/698, Playwright đủ bộ 138/138 (Playwright tự bật API Testing 5000 + Vite 5173).
    Người dùng giữ mặc định: DBN chỉ ô ghi chú (không tab Đính kèm), PRCW tính mua nội bộ là mua đúng quy định. **Đã merge.**
    Đợt 0.24.0 (Y6, Y7, X7) hoàn tất – chờ người dùng yêu cầu phát hành 0.24.0; sau đó các lát 0.25.0 (Portal P1, P1b, P3).
- **Lát P1 – Portal NCC: tài khoản NCC (12/11/2026, nhánh `feature/p1-portal-tai-khoan-ncc`, bản 0.25.0)** – design.md 19.5 "Lát P1" (quy tắc
  tự đặt P1-1…P1-6), ui-style.md 5f. Người dùng bảo "làm tiếp" → làm theo lộ trình 19.6.
  Backend: Domain `SupplierPortalUser` (Đã mời / Đang dùng / Đã khóa, email duy nhất, khóa tạm, dấu bảo mật) + `SupplierPortalCode` (link mời,
  đặt lại mật khẩu, OTP – chỉ lưu băm SHA-256); migration `V0_25_0_TaiKhoanPortalNcc`; `Infrastructure/Portal` (`PortalOptions` mục `Portal`,
  `PortalTokenService` – JWT audience riêng, `SupplierPortalService`); scheme JWT "Portal" trong Program.cs (audience phải khác `Jwt:Audience`,
  `ExposeCodesForTesting` chỉ Development/Testing); `CompanyScopeMiddleware` bỏ qua phiên Portal; endpoint `PortalEndpoints.cs`: nội bộ
  `/api/suppliers/{id}/portal-users` (xem: SupplierView; mời/gửi lại/khóa/mở: quyền mới `MasterData.Supplier.Portal`, vai trò mẫu NVCUVT),
  Portal `/api/portal/auth/*` (ẩn danh: invite, activate, login → OTP, verify-otp, forgot/reset-password) và `/api/portal/*` (me, change-password,
  users – quản trị NCC; filter kiểm phiên mỗi request). Gửi email trực tiếp (OTP cần tới ngay); chưa bật email thì người mời thấy link.
  Web: tab **Tài khoản Portal** trong ngăn kéo NCC (`SupplierPortalTab`), component dùng chung `PortalUserPanel`; trang `/ncc` (`src/portal/`:
  đăng nhập 2 bước, kích hoạt, quên/đặt lại mật khẩu, trang chủ, Người dùng, đổi mật khẩu) – phiên Portal riêng (`proc.portal.session`), không
  dùng `apiFetch` nội bộ. `DataGrid` thêm prop `onSelectRow`; `copyText` tách ra `utils/clipboard.ts`. Cấu hình: `Portal:*` (deploy-windows.md),
  `PORTAL_BASE_URL` (docker-compose, .env.example, README). Hướng dẫn 02-danh-muc mục "Tài khoản Portal nhà cung cấp".
  Test: domain `SupplierPortalUserTests` (5), tích hợp `SupplierPortalAccountTests` (2: mời → kích hoạt → OTP, token Portal/nội bộ không
  dùng chéo, quyền; người dùng con, khóa / đổi mật khẩu làm token cũ hết hiệu lực, quên mật khẩu), e2e `supplier-portal.spec.ts` (tab NCC
  ở mép dải tab: bấm bằng `dispatchEvent` – chuột Playwright lệch do cuộn dải tab). Kiểm tra: build 0 cảnh báo, `dotnet test` 711/711,
  tsc, oxlint, `npm run build` qua; Playwright đủ bộ 137/139 – `field-help-catalog`, `suppliers` lỗi vì ngăn kéo vẽ sẵn mọi tab,
  tiêu đề cột "Họ tên", "Trạng thái" của bảng Portal trùng nhãn ô form NCC → `DetailTab.lazy` (tab không có ô form chỉ vẽ khi mở), tab
  Portal dùng `lazy`; chạy lại 4 spec NCC (field-help-catalog, suppliers, supplier-portal, supplier-links) qua 6/6. **Đã merge.**
  Tiếp theo: P1b (hạ tầng Portal: giới hạn tần suất, tên miền riêng, kiểm file), P3 (báo giá online) – hoặc phát hành 0.24.0 khi người dùng yêu cầu.
- **Lát P1b – hạ tầng Portal NCC (12/11/2026, nhánh `feature/p1b-ha-tang-portal`, bản 0.25.0)** – design.md 19.5 "Lát P1b" (quy tắc tự
  đặt P1b-1…P1b-5). Người dùng bảo "làm tiếp" → làm theo lộ trình. Không migration, không sửa giao diện.
  Backend: `Api/Security/PortalRateLimiting.cs` (RateLimiter toàn cục theo IP, cửa sổ 1 phút: `/api/portal/auth/*` 20, API Portal khác 300 –
  `Portal:RateLimit:*`; 429 + Retry-After + ProblemDetails tiếng Việt, log cảnh báo); `Api/Security/PortalHostMiddleware.cs` (tên miền
  `Portal:Hosts` chỉ mở `/ncc`, file tĩnh, `/api/portal`; API nội bộ / health 404; trang khác chuyển `/ncc`) + `SecurityHeadersMiddleware`
  (nosniff, X-Frame-Options SAMEORIGIN, Referrer-Policy same-origin); `Domain/Documents/FileSignature.cs` – kiểm chữ ký đầu file khớp đuôi,
  chặn file chạy được dù đổi đuôi, gắn vào `AttachmentService.UploadAsync` (giữ 1.024 byte đầu khi chép, lỗi thì xóa file tạm).
  `appsettings.Testing.json` nâng giới hạn tần suất (test, e2e chạy nhiều lượt từ một IP). Tài liệu: deploy-windows.md mục 3.5b (binding IIS tên
  miền Portal), docker-compose `PORTAL_HOSTS`, .env.example, README, hướng dẫn 01-lam-quen (kiểm nội dung file).
  Test: domain `FileSignatureTests` (31), tích hợp `PortalInfrastructureTests` (4: 429, tên miền Portal, tiêu đề bảo mật, duyệt endpoint
  Portal / nội bộ), `AttachmentTests` thêm HTML đổi đuôi .pdf, chương trình đổi đuôi .txt; sửa 4 test tải file giả nội dung.
  Người dùng yêu cầu phát hành 0.24.0 kèm Portal để test → thêm `Portal:TestServer` (P1b-6: máy chủ thử LAN chưa có email hiện mã OTP / link
  trên màn hình khi cùng bật `ExposeCodesForTesting`; không khởi động nếu đồng thời có `Portal:Hosts`; log cảnh báo), test
  `LoMaOtp_ChiMayDevTestHoacMayChuThuNghiem` (4). Kiểm tra: build 0 cảnh báo, `dotnet test` 746/746 (trước khi thêm 4 test TestServer; chạy
  lại nhóm Portal 10/10), tsc, oxlint qua (không sửa giao diện); Playwright đủ bộ 139/139 (Playwright tự bật API Testing 5000 + Vite 5173).
  **Đã merge.**
- **Phát hành 0.24.0 (12/11/2026)** – người dùng: "phát hành 0.24.0, chấp nhận gói này có kèm Portal thiết lập sẵn để tôi test". Tag `v0.24.0`
  – Y6, Y7, X7 + Portal P1, P1b (lộ trình design.md 19.6 sửa: P1, P1b vào 0.24.0; 0.25.0 còn P3). 4 migration (88 → 92; migration Portal giữ tên
  `V0_25_0_TaiKhoanPortalNcc`), script `db/scripts/0.24.0.sql` (idempotent) chạy thử 2 lần trên bản sao `procurement_kiemtra_0240` (từ CSDL bản
  test 5007 0.23.0), không lỗi, số dòng PR/PO/AuditLog/NCC giữ nguyên. Kiểm tra: build 0 cảnh báo, `dotnet test` 746/746 + nhóm Portal 10/10,
  Playwright 139/139. Đóng gói `publish.ps1 -NoInstall` (chạy bằng `powershell.exe -File` – PowerShell trong phiên dừng vì cảnh báo Vite ở
  stderr) → `artifacts\procurement-0.24.0.zip`; Release https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.24.0.
  **Bản test 5007 nâng lên 0.24.0 (12/11/2026)**: `app` = 0.24.0, `app-0.23.0` = bản cũ, gói `goi\0.24.0`, sao lưu
  `D:\project\procurement_kiemtra_0150_truoc_0.24.0.dump`; chạy `0.24.0.sql` (92 migration); PID 19172 trong `pid.txt`; `/health` Healthy 0.24.0
  (cả qua http://192.168.1.199:5007). `appsettings.Production.json` thêm mục `Portal`: `BaseUrl` http://192.168.1.199:5007,
  `ExposeCodesForTesting` + `TestServer` = true (chưa có email – mã OTP hiện trên màn hình Portal). Gán quyền mới qua API (script scratchpad
  `roles_0240.py`): NVCUVT, TOANQUYEN nhận `MasterData.Supplier.Portal` + `Purchase.InternalSale.View/Manage`; 12 vai trò xem nghiệp vụ (CHUTICH,
  DAMPHAN, GDTCKT, HOIDONG, KSNB, LANHDAO, PHOCTUBCL, POLAP, TPCUVT, TPTCKT, TRUONGBANCUVT, VANHANH) nhận xem đơn bán nội bộ. Tạo sẵn tài khoản
  Portal thử (`portal_0240.py`): **ncc.thu@vidu.vn / Ncc@2026**, quản trị NCC của NCC000001 (Phú Thịnh) – đăng nhập http://192.168.1.199:5007/ncc,
  đã thử mời → kích hoạt → mật khẩu → OTP → trang chủ qua.
- **Dòng "Đăng nhập Portal nhà cung cấp" trên màn hình đăng nhập nội bộ (12/11/2026, nhánh `feature/link-portal-dang-nhap`)** – người dùng
  test 0.24.0 vào nhầm `…/ncc,` (dấu phẩy dính vào link trong tin nhắn của Claude) → ra trang đăng nhập nội bộ, báo sai tên đăng nhập; người
  dùng đồng ý thêm dòng chỉ đường. `LoginPage.tsx` + `.css`: "Bạn là nhà cung cấp? Đăng nhập Portal nhà cung cấp" → `/ncc/dang-nhap`. E2e
  `auth.spec.ts` thêm 1 test; tsc, oxlint qua; e2e auth + supplier-portal 8/8; chụp màn hình đối chiếu. Chưa phát hành (vào bản sau). **Đã merge.**
- **Lát P3a – báo giá online niêm phong trên Portal (12/11/2026, nhánh `feature/p3a-bao-gia-portal`, bản 0.25.0)** – design.md 19.5 "Lát P3a"
  (P3-1…P3-8). P3 tách P3a / P3b (P3b: đính kèm file báo giá Portal, hỏi đáp, email thư mời). Người dùng yêu cầu giữa lát: "phải kèm giờ, trước
  giờ mở chỉ người có quyền mới được xem" → hạn nộp ngày + giờ (`Rfq.DeadlineTime`, RFQ cũ 23:59; mọi kiểm tra hạn nộp so thời điểm – domain nhận
  `DateTime now` giờ VN, `IClock.NowInVietnam()`), quyền mới `Sourcing.Rfq.ViewSealed` (xem giá trước giờ mở, ghi nhật ký truy cập sự kiện
  `SealedQuoteViewed`). Domain: `RfqPortalSubmission` + dòng (không [Auditable], mã băm SHA-256 biên nhận), `Rfq.OpenPortalQuotes`,
  `ScoreQuotation`, `RfqSupplier.IsFromPortal`, `QuotationLine.Normalize` dùng chung. Backend: `PortalRfqService` (`/api/portal/rfqs`, lọc theo
  NCC của phiên, bỏ bộ lọc công ty), `RfqService` mở / chấm điểm / xem niêm phong, chặn nhập thay + chọn thầu khi còn niêm phong. Migration
  `V0_25_0_BaoGiaPortalNiemPhong` (92 → 93). Web: Portal "Thư mời báo giá" (`portal/PortalRfqPages.tsx`), RFQ nội bộ ô giờ hạn nộp, gia hạn kèm
  giờ, dòng "Nộp qua Portal", nút Mở báo giá Portal / Xem báo giá niêm phong / Chấm điểm; nhãn nhật ký truy cập. Hướng dẫn 05-dau-thau.
  Test: domain `RfqPortalSubmissionTests` (3) + sửa `SourcingTests` theo chữ ký mới, tích hợp `BaoGiaPortal_NiemPhong_XemCanQuyen_MoSauGioHan`,
  e2e `portal-rfq-quote.spec.ts`. Kiểm tra: `dotnet test` 754/754, tsc, oxlint, `npm run build` qua; e2e nhóm RFQ/Portal/quản trị 34/34. Playwright đủ bộ 141/141, build 0 cảnh báo. **Đã merge.**
- **Lát P3b-1 – file đính kèm báo giá Portal (12/11/2026, nhánh `feature/p3b1-dinh-kem-bao-gia-portal`, bản 0.25.0)** – design.md 19.5 "Lát
  P3b-1" (P3-9, P3-10). P3b tách P3b-1 / P3b-2 hỏi đáp / P3b-3 email thư mời. Loại chủ đính kèm `RfqPortalQuote` (chủ = `RfqSupplier.Id`), cột
  `Attachment.UploadedByPortalUserId` (migration `V0_25_0_DinhKemBaoGiaPortal`, 93 → 94). `AttachmentService`: tách `StoreAsync` dùng chung,
  hàm Portal (`ListForPortalAsync`, `UploadForPortalAsync`, `OpenForPortalAsync`, `DeleteForPortalAsync`), nhánh quyền `RequireRfqPortalQuoteAsync`
  (nội bộ chỉ xem; niêm phong cần `ViewSealed`, tải file ghi nhật ký). `PortalRfqService` + API `/api/portal/rfqs/{id}/attachments…` (thêm, xóa
  đến giờ hạn). Web: khối "File đính kèm báo giá" trên Portal (lưới + Tải file lên / Tải xuống / Xóa file), dòng "File báo giá Portal" + hộp xem
  file trên bảng so sánh RFQ. Test: tích hợp `FileBaoGiaPortal_NccTaiLen_NoiBoChiXemTheoNiemPhong`, e2e `portal-rfq-quote.spec` thêm bước file.
  Kiểm tra: build 0 cảnh báo, `dotnet test` 755/755, tsc, oxlint qua; e2e portal-rfq-quote + attachments qua; Playwright đủ bộ 141/141. **Đã merge.**
- **Lát P3b-2 – hỏi đáp làm rõ RFQ trên Portal (12/11/2026, nhánh `feature/p3b2-hoi-dap-portal`, bản 0.25.0)** – design.md 19.5 "Lát P3b-2"
  (P3-11…P3-14). Người dùng chốt giữa lát: **thông báo của công ty công khai cho mọi NCC; câu hỏi NCC thì Phòng CUVT trả lời riêng, chỉ NCC hỏi
  thấy, bảo mật với NCC khác** → bỏ lựa chọn "công khai" khi trả lời. Domain `RfqClarification` (Ask / Announce / Reply, không [Auditable], trả
  lời một lần – `AnsweredAt` là concurrency token), migration `V0_25_0_HoiDapBaoGiaPortal` (94 → 95). `PortalRfqService.AskAsync` (thông báo
  `PortalQuestion` cho người lập RFQ), `PortalRfqDto.CanAsk/Clarifications` (chỉ câu của NCC mình + thông báo); `RfqClarificationService` +
  API `/api/rfqs/{id}/clarifications`. Web: component `classic/QaThread.tsx` (ui-style.md 5g), khối "Hỏi đáp với bên mời" trên Portal, tab
  "Hỏi đáp NCC (n chưa trả lời)" trên RFQ, nhãn thông báo "Câu hỏi NCC". Hướng dẫn 05-dau-thau. Test: tích hợp
  `HoiDapPortal_TraLoiRiengNccHoi_ThongBaoCongKhai` (NCC thứ hai không thấy câu hỏi/trả lời), e2e `portal-rfq-quote.spec` test hỏi đáp.
  Kiểm tra: build 0 cảnh báo, `dotnet test` 756/756, tsc, oxlint, `npm run build` qua; Playwright đủ bộ 142/142. **Đã merge.**
- **Lát P3b-3 – email báo NCC qua Portal (12/11/2026, nhánh `feature/p3b3-email-thu-moi-ncc`, bản 0.25.0)** – design.md 19.5 "Lát P3b-3"
  (P3-15…P3-18, tự đặt). Người dùng bảo "làm tiếp". Domain `SupplierPortalEmail` (hàng chờ thư, CompanyId, không [Auditable], trạng thái dùng
  chung `NotificationEmailState`), migration `V0_25_0_EmailPortalNcc` (95 → 96). `Application/Portal/PortalEmailModels.cs` (`IPortalEmailService`),
  `Infrastructure/Portal/PortalEmailService.cs`: soạn thư cùng giao dịch cho tài khoản Portal **đang dùng** của NCC (không danh sách đen) – thư
  mời (`RfqService.SendAsync`), gia hạn (không lý do), hủy RFQ đã gửi (`CancelAsync` + `CancelChainService`), trả lời (chỉ NCC hỏi, kèm câu hỏi),
  thông báo làm rõ (mọi NCC); `EmailBackgroundService` gửi kèm sau email thông báo (thử lại, quá `MaxAgeHours` thôi gửi, kiểm lại tài khoản lúc
  gửi). API `GET /api/rfqs/{id}/portal-emails`. Web: tab **"Email NCC (n)"** trên RFQ đã gửi (`PortalEmailView`, đèn trạng thái), hook
  `useRfqPortalEmails`. Hướng dẫn 05-dau-thau. Test: domain `SupplierPortalEmailTests` (2), tích hợp
  `EmailPortal_ThuMoiGiaHanTraLoiThongBaoHuy_ChiTaiKhoanDangDung` (tài khoản chưa kích hoạt / bị khóa không nhận, thư không có lý do, không nêu
  NCC khác), e2e `portal-rfq-quote.spec` thêm bước tab Email NCC. Kiểm tra: build 0 cảnh báo, `dotnet test` 759/759, tsc, oxlint,
  `npm run build` qua; Playwright đủ bộ 142/142. **Đã merge.** Các lát P3 của 0.25.0 xong – tiếp theo: phát hành 0.25.0 khi người dùng yêu
  cầu, hoặc các lát 0.26.0 (P4 xác nhận đơn, P5 giao hàng, P6a hóa đơn) theo lộ trình design.md 19.6.
- **Lát P4a – NCC xác nhận đơn hàng trên Portal (12/11/2026, nhánh `feature/p4a-ncc-xac-nhan-po`, bản 0.26.0)** – design.md 19.5 "Lát P4a"
  (P4-1…P4-6). Người dùng bảo "làm tiếp", giữa lát yêu cầu **"cho Phòng CUVT mở lại phản hồi"** (P4-2b). Domain `PurchaseOrderSupplierResponse`
  (Create / Reopen, không [Auditable], chỉ mục duy nhất có điều kiện `ReopenedAt IS NULL`, `ReopenedAt` là concurrency token), loại chủ đính kèm
  `PoSupplierFile`, thông báo `PortalOrderResponse`, loại thư `PurchaseOrderIssued/Cancelled/Reopened`; migration `V0_26_0_NccXacNhanDonHang`
  (96 → 97). `PortalOrderService` + API `/api/portal/orders` (danh sách, chi tiết, phản hồi, file); `AttachmentService` các hàm Portal nhận loại
  chủ (dùng chung RFQ / PO); `PortalEmailService.QueuePurchaseOrderAsync` / `ListForPurchaseOrderAsync`; `PurchaseOrderService` (thư khi duyệt xong /
  hủy PO đã duyệt, `SupplierResponse` trên DTO, `ReopenSupplierResponseAsync`), `CancelChainService` thư hủy PO; API nội bộ
  `POST /api/purchase-orders/{id}/reopen-supplier-response`, `GET …/portal-emails`. Web: trang Portal **Đơn đặt hàng** (`PortalOrderPages.tsx`),
  khối dùng chung `PortalParts.tsx` (`Section`, `PortalFiles` – trang thư mời dùng lại), `components/PortalEmailView.tsx` (tách từ RfqPages); PO có
  ô "Xác nhận của NCC" + tab **"Xác nhận NCC"** (`SupplierResponsePanel.tsx` – mở lại phản hồi, file NCC, thư). Hướng dẫn 03-mua-hang, ui-style
  5f. Test: domain `PurchaseOrderSupplierResponseTests` (4), tích hợp `PortalDonHang_XacNhanTuChoiMoLai_FileKhoa_ThuBaoNcc_CoLapNcc`, e2e
  `portal-order.spec.ts`. Kiểm tra: build 0 cảnh báo, `dotnet test` 764/764, tsc, oxlint, `npm run build` qua; Playwright đủ bộ 143/143. **Đã merge.**
  Tiếp theo: P4b (báo NCC kết quả chọn thầu), P5 (báo giao hàng → XNH), P6a (hóa đơn) – hoặc phát hành 0.25.0 khi người dùng yêu cầu (bản
  phát hành tiếp theo sẽ gồm cả P4a vì đã ở `main`).
- **Lát P4b – báo NCC kết quả chọn thầu (13/11/2026, nhánh `feature/p4b-ket-qua-chon-thau`, bản 0.26.0)** – design.md 19.5 "Lát P4b"
  (P4-7…P4-9). Người dùng bảo "làm tiếp", giữa lát yêu cầu **"để phòng CUVT tự bấm gửi"** → duyệt xong không tự gửi; nút **Gửi kết quả cho
  NCC** trên RFQ đã chọn thầu (quyền `Sourcing.Rfq.Manage`, hỏi lại, một lần, ghi người – giờ gửi trên đầu phiếu). Domain: `Rfq.ResultFor`
  (Trúng / Trúng một phần / Không trúng / Không báo giá, dòng trúng kèm giá của chính NCC), `Rfq.MarkResultSent`, cột `ResultSentAt/By` (migration
  `V0_26_0_GuiKetQuaChonThau`, 97 → 98); loại thư `RfqAwarded`, `RfqNotAwarded` (không giá, không nêu NCC trúng; NCC không báo giá không có thư).
  `RfqService.SendResultAsync` + API `POST /api/rfqs/{id}/send-result`; `PortalRfqService` – chưa gửi thì NCC vẫn "Đang xét chọn", gửi rồi trả
  `Outcome` (danh sách) và `Result` (chi tiết). Web: Portal cột "Kết quả" + khối "Kết quả chọn thầu"; RFQ nút gửi, nhãn thư mới ở tab Email NCC.
  Hướng dẫn 05-dau-thau. Test: domain `RfqSupplierResultTests` (2), tích hợp `KetQuaChonThau_SauDuyetXong_NccChiThayKetQuaCuaMinh_ThuBaoKetQua`,
  e2e `portal-rfq-quote.spec.ts` (thêm 1). Kiểm tra: build 0 cảnh báo, `dotnet test` 767/767, tsc, oxlint, `npm run build`; Playwright đủ bộ
  144/144. **Đã merge.** Tiếp theo: P5 (NCC báo giao hàng → XNH), P6a (hóa đơn) – hoặc phát hành khi người dùng yêu cầu.
- **Lát P5 – NCC báo giao hàng trên Portal → XNH (13/11/2026, nhánh `feature/p5-bao-giao-hang`, bản 0.26.0)** – design.md 19.5 "Lát P5"
  (P5-1…P5-7, quy tắc tự đặt). Người dùng bảo "làm tiếp". Các bước:
  - **Bước 1 (xong)**: Domain `PurchaseOrderDeliveryNotice` + dòng (số loại **BGH**, `DocumentTypes.DeliveryNotice`; Create / Cancel, không
    [Auditable]), `GoodsReceiptConfirmation.DeliveryNoticeId` + `LinkDeliveryNotice`, loại chủ đính kèm `DeliveryNoticeFile`; cấu hình EF (chỉ mục
    duy nhất XNH chưa hủy theo phiếu báo giao), migration `V0_26_0_NccBaoGiaoHang` (98 → 99). Test domain `PurchaseOrderDeliveryNoticeTests` (4) qua.
  - **Bước 2 (xong)**: `DeliveryNoticeQueries` (XNH theo phiếu, trạng thái Submitted/Receiving/Received/Cancelled, SL còn báo giao = SL đặt ×
    (1 + dung sai) − đã nhận − phiếu chưa nhận), `PortalDeliveryNoticeService` (gửi – khóa PO, chặn khi NCC đang từ chối đơn, cấp số BGH theo
    công ty đặt, báo thủ kho nơi nhận + người lập PO `PortalDeliveryNotice`; hủy – khóa phiếu, chặn khi có XNH; file `DeliveryNoticeFile`),
    `PortalOrderService` (dòng thêm Id, đã nhận, còn giao; `CanNotifyDelivery`, danh sách phiếu), `DeliveryNoticeService` nội bộ (quyền xem PO
    hoặc XNH), XNH nhận `DeliveryNoticeId` khi lập (khóa phiếu, một XNH chưa hủy / phiếu), DTO XNH có số BGH. API Portal
    `/api/portal/orders/{id}/delivery-notices` (+ `/cancel`, file), nội bộ `GET /api/purchase-orders/{id}/delivery-notices`,
    `GET /api/delivery-notices/{id}`. Test tích hợp `PortalBaoGiao_SlConGiao_HuyCoLyDo_LapXnhTheoPhieu_NccThayThucNhan`; `dotnet test` 772/772.
  - **Bước 3 (xong)**: Portal `PortalDeliveryParts.tsx` (form "Lập phiếu báo giao hàng" – LineGrid SL giao, file chờ tải khi gửi; danh sách phiếu
    + dòng + Hủy phiếu báo giao + khối file), trang đơn có nút **Báo giao hàng…**, cột Đã nhận / Còn giao được; `PortalFiles kind="delivery-notices"`.
    Nội bộ: tab **"Giao hàng NCC"** của PO (`DeliveryNoticePanel.tsx`, nút Lập XNH → `/mua-hang/xac-nhan-nhan-hang/moi?po=…&bgh=…`), XNH điền
    sẵn theo phiếu báo giao + ô "Theo phiếu báo giao NCC"; nhãn thông báo "NCC báo giao hàng". e2e `portal-delivery.spec.ts`, hàm đăng nhập Portal
    tách ra `e2e/portalHelpers.ts` (portal-order dùng chung); `document-numbering.spec` 23 → 24 loại (thêm BGH).
  - **Bước 4 (xong)**: design.md 19.5 "Lát P5" (P5-1…P5-7) + lộ trình 0.26.0, ui-style.md 5f, hướng dẫn 03-mua-hang.
  - Kiểm tra: build 0 cảnh báo, `dotnet test` 772/772, tsc, oxlint, `npm run build` qua; Playwright đủ bộ 144/145 (document-numbering đếm loại
    chứng từ – sửa 24, chạy lại qua). **Đã merge.** Tiếp theo: ~~P6a~~ (bỏ 13/11/2026) – hoặc phát hành 0.25.0/0.26.0 khi người
    dùng yêu cầu (main đã gồm P3–P5).
  - **Người dùng chốt sau lát (13/11/2026)**: NCC giao theo quy trình đang có, **báo giao trên Portal không bắt buộc** – ghi nhận để xem lại sau
    (design.md 19.5 P5-8, hướng dẫn 03-mua-hang). Code không đổi: XNH vẫn lập thẳng từ PO, không cần phiếu báo giao.
- **Lát P6a – hóa đơn điện tử trên Portal: BỎ (người dùng chốt 13/11/2026)** – "công ty tôi có kênh riêng để tiếp nhận, tôi không yêu cầu bạn
  làm". Đã làm dở bước 1–3 (domain HDN + đọc XML, migration `V0_26_0_HoaDonNcc`, dịch vụ + API + test tích hợp, giao diện Portal + màn hình
  Hóa đơn NCC + e2e chưa chạy) – **chỉ trên nhánh `feature/p6a-hoa-don-ncc`** (đã push), **không merge**; `main` không có code, migration
  của P6a. Nhánh đã xóa (local + GitHub) theo đồng ý của người dùng 13/11/2026. design.md 19.3 bước 6, 19.5, 19.6 đã ghi bỏ. **Đã chốt**:
  đề nghị thanh toán Y8 lấy thông tin hóa đơn từ XNH (số, ký hiệu, ngày, tiền hóa đơn nhập trên XNH).
- **Lát Y8 – đề nghị thanh toán 5A/5B (13/11/2026, nhánh `feature/y8-de-nghi-thanh-toan`, bản 0.27.0)** – người dùng chọn "B. Làm Y8, Y11, Y9
  xong rồi mới phát hành 0.27.0". Quy tắc tự đặt Y8-1…Y8-8 (ghi vào design.md ở bước tài liệu): mỗi phiếu một PO đã duyệt / đóng, 5A theo PO
  loại A, 5B theo B/DV; dòng = XNH đã xác nhận có hóa đơn (chụp số, ký hiệu, ngày, tiền từ XNH); giảm trừ có lý do; chuyển khoản chụp tài khoản
  NCC; duyệt GĐ khối của phiếu → TP TC-NQ-KT → GĐ TC-NQ-KT → Chủ tịch; kế toán ghi Đã chi; in 5A/5B. Các bước:
  - **Bước 1 (xong)**: Domain `PaymentRequest` + dòng (số **DNT**, `ApprovableDocument`; Create / Update / SetLines / EnsureSubmittable /
    MarkPaid), quyền `Purchase.Payment.View/Create/Approve/Pay` (vai trò mẫu: xem trong ProcurementView; NVCUVT lập; GDKHOI, TPTCKT, GDTCKT,
    CHUTICH duyệt; KETOAN xem + ghi Đã chi), loại duyệt DNT + bộ mẫu CUVT 4 cấp, loại đính kèm `PaymentRequest`; EF + migration
    `V0_27_0_DeNghiThanhToan` (99 → 100). Test domain `PaymentRequestTests` (3), thứ tự loại duyệt thêm DNT; 403/403.
  - Người dùng xác nhận giữa lát (13/11/2026): phiếu đề nghị thanh toán **có nhiều dòng hóa đơn** – đúng Y8-2 (mỗi dòng một hóa đơn của
    một XNH, không giới hạn số dòng).
  - **Bước 2 (xong)**: `PaymentRequestService` (danh sách lọc ngày; PO lập được – còn XNH có hóa đơn chưa đề nghị; dữ liệu chọn – tài khoản NCC,
    XNH kèm đề nghị đang giữ; lập / sửa / gửi dưới khóa PO, chụp lại hóa đơn từ XNH lúc gửi; duyệt / từ chối / rút / hủy – đã chi thì không hủy;
    ghi Đã chi), API `/api/payment-requests` (+ `/orders`, `/source`, `/submit`, `/approve`, `/reject`, `/recall`, `/cancel`, `/pay`). Nối vào
    tra cứu chứng từ, xem trước chuỗi duyệt, nhắc quá hạn, Việc cần làm, đính kèm có phê duyệt (`PaymentRequest`); XNH đã ở đề nghị chưa hủy
    thì khóa "Cập nhật hóa đơn". Test tích hợp `DeNghiThanhToan_NhieuHoaDonXnh_KhoaHoaDon_Duyet_DaChi`; `dotnet test` 776/776.
  - **Bước 2b (xong)**: hủy dây chuyền nhận DNT (chuỗi chỉ gồm chính phiếu; đã chi thì chặn).
  - **Bước 3 (xong)**: web `api/paymentRequests.ts`, màn hình **DNT** (`PaymentRequestPages.tsx` – danh sách lọc ngày; form DocumentForm: đơn mua
    hàng, hình thức, tài khoản NCC, giảm trừ + lý do, ghi chú, lưới nhiều dòng hóa đơn chọn qua hộp "Chọn phiếu nhận hàng", tổng – giảm trừ – số
    tiền đề nghị, tab Lịch sử duyệt + Đính kèm, nút **Ghi Đã chi** của kế toán), nút **Lập đề nghị thanh toán** trên PO (điền sẵn đơn, tài khoản
    mặc định / tiền mặt, mọi XNH có hóa đơn), quyền, loại đính kèm, Việc cần làm / duyệt gọn điện thoại / tra cứu chứng từ. e2e
    `payment-request.spec.ts`; `document-numbering` 24 → 25 loại, `approval-rule-template` 36 → 40 cấp, `divisions` gõ để lọc loại phiếu.
  - **Bước 4 (xong)**: in 5A/5B (`CuvtPaymentPrintForms.cs`, `GET /api/payment-requests/{id}/pdf`): khung chi phí PO tách thành `OrderFrameAsync`
    dùng chung 4A/4B; người in chỉ cần quyền đề nghị thanh toán. Test trong `DeNghiThanhToan_…` (PDF + nội dung mẫu).
  - **Bước 5 (xong)**: design.md 19.3 "Lát Y8" (Y8-1…Y8-8) + lộ trình 0.27.0; hướng dẫn 03-mua-hang.
  - Kiểm tra: build 0 cảnh báo, `dotnet test` 776/776, tsc, oxlint, `npm run build`; Playwright đủ bộ 144/146 (2 spec đếm cấp mẫu CUVT / chọn loại
    phiếu – sửa theo DNT, chạy lại qua cùng payment-request: 5/5). **Đã merge.** Tiếp theo: Y11 (tạp vụ 1C/2C) – chờ người dùng gửi mẫu 1C/2C
    hoặc "đồng ý" đề xuất (bảng kê 2C theo bộ phận × tháng; 1C gộp nhiều 2C đã duyệt), rồi Y9 (báo cáo thanh toán), rồi phát hành 0.27.0.
- **Lát Y9 – báo cáo thanh toán (13/11/2026, nhánh `feature/y9-bao-cao-thanh-toan`, bản 0.27.0)** – làm trước Y11 vì Y11 còn chờ mẫu 1C/2C
  (không có trong `docs/`, file CUVT nhóm A/B chỉ có sheet 1A–5A, 1B–5B). Quy tắc tự đặt Y9-1…Y9-3 (design.md 19.3 "Lát Y9").
  - **Y9a bước 1 (xong)**: `GET /api/reports/payment-tracking` (`PurchaseReportService.GetPaymentTrackingAsync`): mỗi PO đã duyệt / đóng theo
    ngày PO – giá trị PO, đã nhận, hóa đơn XNH, XNH chưa HĐ, HĐ chưa đề nghị, đang lập / chờ duyệt, đã duyệt chờ chi, đã chi, giảm trừ, còn
    phải trả; tổng hợp NCC + dòng tổng. Test `TheoDoiThanhToan_TheoPo_HoaDon_DeNghi_DaChi_ConPhaiTra`.
  - **Y9a bước 2 (xong)**: màn hình **TTPO** (Báo cáo → Theo dõi thanh toán, `PaymentTrackingReportPage`): xem theo PO / NCC, đèn tình trạng
    thanh toán, ô số PO; e2e `payment-request.spec.ts` thêm bước báo cáo (qua). Tài liệu: design.md 19.3 + 19.6, hướng dẫn 07-bao-cao.
  - **Y9b (xong)**: **xuất chứng từ mua cho kế toán** thay ACCX (design.md 19.3 Y9-4…Y9-6): `PurchaseAccountingExportService`,
    `GET /api/purchase-accounting-export/documents|file`; PO theo ngày duyệt, XNH theo ngày nhận (kèm hóa đơn), DNT đã duyệt theo ngày duyệt;
    Excel 5 sheet. **Quyền mới `Report.PurchaseAccounting.Export`** (vai trò mẫu KETOAN – **cần gán cho vai trò Kế toán đang dùng**, vai trò
    mẫu chỉ áp khi tạo/cập nhật bộ vai trò mẫu). Màn hình **ACCP** (Báo cáo → Xuất chứng từ mua). Test `XuatChungTuMuaKeToan_Po_Xnh_DeNghiThanhToan`;
    e2e `payment-request.spec.ts` thêm bước ACCP + tải file. Không migration. Tài liệu: design.md 19.3, 19.4, 19.6; hướng dẫn 07-bao-cao 7.6.
  - Kiểm tra: build 0 cảnh báo, `dotnet test` 778/778, tsc, oxlint, `npm run build`; e2e payment-request qua.
    Tiếp theo: **Y11** (tạp vụ 1C/2C) – người dùng trả lời "đồng ý" 13/11/2026 giữa lát Y9 → làm theo đề xuất (bảng kê 2C theo bộ phận ×
    tháng; 1C gộp nhiều 2C đã duyệt), rồi phát hành 0.27.0 khi người dùng yêu cầu.
- **Ẩn menu trống + tự điền giá đã duyệt trên PO (11/11/2026)** – người dùng đồng ý sau khi test 0.22.0.
  - `fix/an-menu-trong` (đã merge): thanh menu chữ (`ShellBar.tsx`) chỉ hiện nhóm có màn hình được phép, như cây điều hướng (trước đây nhóm
    trống báo "Không có màn hình được phép"); e2e `permissions.spec.ts` sửa theo.
  - `feature/po-tu-dien-gia`: chọn mặt hàng / đổi ĐVT trên PO mà có giá đã duyệt thì tự điền đơn giá, % CK, thuế suất (chờ API giá tải lại
    xong; người dùng đã gõ đơn giá trong lúc chờ thì giữ); nút "Áp giá đã duyệt" giữ nguyên. design.md mục 18.10 ghi bổ sung; e2e
    `pricing.spec.ts` kiểm tra tự điền 47.000. Kiểm tra: tsc, oxlint, `npm run build`; Playwright đủ bộ 124/124 (2 test số phiên bản lỗi
    do máy chủ Vite 5173 chạy từ thời 0.20.0 – Vite đọc phiên bản lúc khởi động; chạy lại trên Vite mới cổng 5174 thì qua). Backend không
    đổi (`dotnet test` 640/640 lần trước).
  - **Tài liệu test 0.22.0 đã sửa** (người dùng đóng Word): K04 Mua hàng → Theo dõi giao hàng; K06 xem báo cáo bằng bmi.lap; K09 "Đóng phần
    còn lại"; K02 nhập đơn giá (tự điền nếu có giá đã duyệt); "SL thực nhận"; K01 menu ẩn nhóm trống; môi trường 0.22.2.
- **Phát hành 0.22.2 (11/11/2026)** – bản vá: ẩn nhóm menu trống, PO tự điền đơn giá đã duyệt, sửa tài liệu test. Người dùng viết "phát hành
  0.22.1" – 0.22.1 đã có tag nên hiểu là bản vá kế tiếp 0.22.2. Tag `v0.22.2`,
  https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.22.2; không đổi CSDL. Kiểm tra: build 0 cảnh báo, `dotnet test`
  640/640, Playwright đủ bộ 124/124. **Bản test 5007 nâng lên 0.22.2**: `app` = 0.22.2, `app-0.22.1` = bản cũ, gói `goi\0.22.2`, sao lưu
  `D:\project\procurement_kiemtra_0150_truoc_0.22.2.dump`, PID 11864; `/health` Healthy 0.22.2; bmi.kho không còn thấy Đấu thầu, Báo cáo.
  Máy chủ Vite dev 5173 chạy từ thời 0.20.0 (hiện sai số phiên bản) – nên khởi động lại.
- **Mẫu in theo biểu mẫu CUVT (11/11/2026)**: người dùng hỏi vì sao chưa in theo biểu mẫu Excel – đã giải thích để theo lộ trình design.md 19.6
  (Y10 khung in + 1A/1B/DV, 2A/2B ở 0.23.0; 3A/3a/3B, 4A/4B ở 0.24.0; 5A/5B, 1C/2C ở 0.27.0). Người dùng chốt **giữ lộ trình**.
- **Phát hành 0.22.1 (11/11/2026)** – bản vá: cột SL thực nhận/Còn lại trên PO đứng sau SL đặt, lưới PO cuộn ngang. Tag `v0.22.1`,
  https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.22.1 (gói `procurement-0.22.1.zip`); không đổi CSDL (78 migration).
  **Bản test 5007 nâng lên 0.22.1**: `app` = 0.22.1, `app-0.22.0` = bản cũ, gói ở `goi\0.22.1`, sao lưu
  `D:\project\procurement_kiemtra_0150_truoc_0.22.1.dump`, PID 6556; `/health` Healthy 0.22.1.
- **Sửa lỗi cột SL thực nhận trên PO (11/11/2026, nhánh `fix/po-cot-thuc-nhan`)** – người dùng test 0.22.0 báo PO-BMI-2611-0003 đã nhận
  hàng nhưng PO không ghi nhận SL thực nhận. Kiểm tra: dữ liệu đúng (XNH-BMI-2611-0002 đã xác nhận 10 + 5, XNH-0001 đã hủy; API trả SL thực
  nhận 10/5, PO "Đã nhận đủ") – lỗi bố trí: cột "SL thực nhận"/"Còn lại" ở cuối lưới 17 cột, bị đẩy khỏi khung nhìn, cột bị ép hẹp. Sửa
  `PurchaseOrderPage.tsx`: hai cột đặt ngay sau "SL đặt", lưới PO bật `scrollX` (giữ độ rộng cột, cuộn ngang). E2e mới
  `po-received-column.spec.ts`. Kiểm tra: `dotnet build` 0 cảnh báo, `dotnet test` 640/640, `npm run build`, tsc, oxlint, Playwright
  (po-received-column, receipt-confirmation, po-over-ceiling, close-remaining, pricing) qua. Bản test 5007 chưa có – vào bản phát hành sau.
  Cũng ghi nhận khi test K01/K02: thanh menu chữ vẫn hiện "Đấu thầu", "Báo cáo" trống với bmi.kho (đề xuất `fix/an-menu-trong`, chờ người
  dùng); tài liệu test K04 ghi "Báo cáo → Theo dõi giao hàng" (đúng là Mua hàng → Theo dõi giao hàng); PO không tự điền đơn giá đã duyệt
  khi chọn mặt hàng (phải bấm "Áp giá đã duyệt") – chờ người dùng chọn.
- **Phát hành 0.22.0 (11/11/2026)**: tag `v0.22.0`, https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.22.0 (gói
  `procurement-0.22.0.zip`) – K1, K2, O1, O2a–d, O3; 6 migration (72 → 78), script `db/scripts/0.22.0.sql` chạy thử 2 lần trên bản sao
  `procurement_kiemtra_0220` (từ CSDL bản test 5007; số dòng PO/NCC/AuditLog/thông báo giữ nguyên). Chạy thử script lần đầu **phát hiện lỗi**:
  SQL viết tay của `V0_22_0_EmailThongBao` thiếu `;` → khối `DO` lỗi cú pháp; đã sửa migration, ghi lưu ý ở `db/scripts/README.md`.
  Đóng gói `publish.ps1 -NoInstall` (Vite dev đang chạy khóa `node_modules`; lần chạy `npm ci` hỏng đã xóa dở `node_modules` – cài bù bằng
  `npm install`, lockfile không đổi). Gói chạy thử cổng 5099 trên `procurement_kiemtra_0220`: `/health` Healthy 0.22.0; script chuẩn bị dữ liệu
  (scratchpad `chuan_bi_0220.py`: gán quyền XNH/ủy quyền cho vai trò cũ, tạo bộ vai trò CUVT, công ty thử **CUV** + bộ phận VT, KTH + 12 tài
  khoản `cuv.*` mật khẩu Test@2026) và luồng PR 6 cấp CUVT chạy đúng. Sao lưu CSDL bản test: `D:\project\procurement_kiemtra_0150_truoc_0.22.0.dump`.
  Tài liệu test `docs/DuLieuThu_KichBanTest_0.22.0.docx` (K01–K09 ở BMI, O01–O12 ở CUV, R01–R03 hồi quy; dựng bằng script trên khuôn file 0.19.0).
  **Bản test cổng 5007 nâng tại chỗ lên 0.22.0 (11/11/2026)**: `app` = 0.22.0, `app-0.21.0` = bản cũ, gói chép vào `goi\0.22.0`; sao lưu
  ngay trước nâng `D:\project\procurement_kiemtra_0150_truoc_0.22.0_lan2.dump`; chạy `0.22.0.sql` (78 migration); PID 5124 trong `pid.txt`;
  `/health` Healthy 0.22.0 (cả qua http://192.168.1.199:5007). Đã chạy `chuan_bi_0220.py`: gán quyền XNH/ủy quyền cho 8 vai trò cũ, tạo 9 vai trò
  mẫu CUVT, công ty **CUV** (bộ phận KTH, VT), 12 tài khoản `cuv.*` / Test@2026 (đăng nhập thử qua). Chưa áp cấu hình duyệt mẫu – người test làm ở kịch bản.

- **O3 – vai trò mẫu và cấu hình duyệt mẫu theo biểu mẫu CUVT** (design.md mục 19.3 "Lát O3", lộ trình 0.22.0; nhánh
  `feature/o3-vai-tro-mau-bieu-mau`, xong 11/11/2026). Mở rộng cơ chế mẫu G10, không migration:
  - Bộ vai trò mẫu có ô chọn bộ (`?set=BAN|CUVT`, trống = tất cả): bộ CUVT thêm TRUONGPHONGBAN, GDKHOI, TPCUVT, NVCUVT (không duyệt), TPTCKT,
    GDTCKT, TPHC, PHOCTUBCL + dùng chung DENGHI, KHO, CHUTICH; CHUTICH thêm quyền duyệt kết quả chọn thầu (vai trò cũ giữ nguyên – thêm tay).
  - Cấu hình duyệt mẫu có ô chọn bộ (`?set=SODO|CUVT`, trống = SODO; nút đổi tên "Cấu hình duyệt mẫu…"): bộ CUVT – PR 6 cấp (Trưởng phòng ban
    của phiếu → TPCUVT → TPTCKT → GĐ khối của phiếu → PHOCTUBCL → CHUTICH), RFQ 5 cấp, PO 4 cấp; cấp người duyệt động không cần vai trò mẫu.
  - Chưa có (thêm ở lát sau): Thủ kho, TP HC nhóm B (Y3); 2A/2B (Y5); 5A/5B, 1C, 2C (Y8, Y11).
  - Test: domain `ApprovalRuleTemplateTests` (+3), tích hợp `ApprovalRuleTemplateTests` (+1, áp CUVT rồi SODO), Playwright
    `approval-rule-template.spec` (+1, ảnh `approval-rule-template-cuvt.png`, `role-templates-cuvt.png`); cập nhật hướng dẫn sử dụng 08-he-thong.
  - Kiểm tra: dotnet build 0 cảnh báo, dotnet test 640/640, tsc, oxlint, npm run build qua; Playwright đủ bộ 121/123 – 2 lỗi (một test thấy
    "Phiên bản 0.20.0", một không thấy phần tử) do Vite cũ giữ cổng 5173 như ở O2d; chạy lại với Vite mới cổng 5197 (`E2E_WEB_URL=http://127.0.0.1:5197`) đều qua.
  - **O3 xong → các lát 0.22.0 (K1, K2, O1, O2, O3) hoàn tất.** Tiếp theo: phát hành 0.22.0 khi người dùng yêu cầu (docs/phat-hanh.md), hoặc
    sang 0.23.0 (Y2 công trình + dự toán, Y3 đề nghị mua 1A/1B/DV + Thủ kho nhập tồn, Y5, Y10).

- **O2 – ủy quyền duyệt, email, nhắc quá hạn, duyệt trên điện thoại** (design.md mục 19.1 Q9, 19.3; lộ trình 0.22.0; nhánh
  `feature/o2-uy-quyen-email`). Chia bước: O2a ủy quyền duyệt → O2b email SMTP (MailKit, gửi bản sao thông báo qua dịch vụ nền) → O2c nhắc
  quá hạn 1 ngày làm việc → O2d trang duyệt gọn cho điện thoại.
  - **O2a (xong 10/11/2026)**: bảng `ApprovalDelegation` (công ty, người ủy quyền → người được ủy quyền, loại phiếu hoặc mọi loại, từ–đến ngày
    ≤ 90 ngày, lý do, ghi chú, thu hồi), màn hình **UQD** (Việc hằng ngày → Ủy quyền duyệt) có tab đính kèm; quyền mới
    `Organization.Delegation.Manage` (lập thay, thu hồi, xem toàn công ty – tự ủy quyền không cần quyền). `ApprovalEngine.ActorAsync`: không giữ
    vị trí thì xét ủy quyền đang tác dụng; bước ghi `OnBehalfOf`, lịch sử "B (thay A)", R3 tính cả người được duyệt thay; thông báo "chờ bạn
    duyệt thay A" và "đã được duyệt thay bạn" (`DelegatedAction`); người duyệt tiếp theo (`NextApproverResolver.CandidatesAsync`) gồm người được
    ủy quyền. Trưởng phòng ban tự lập phiếu gửi được khi đang ủy quyền. Migration `V0_22_0_UyQuyenDuyet` (Down mất ủy quyền + dấu duyệt thay).
    Test tích hợp `ApprovalDelegationTests` (2), e2e `delegations.spec.ts`. Kiểm tra: dotnet build 0 cảnh báo, dotnet test 627/627, tsc, oxlint,
    npm run build, Playwright delegations/divisions/notifications 4/4 (API 5010 + Vite 127.0.0.1:5191 – cổng 5190 còn Vite cũ).
    Lưu ý: Google Drive giữ `src\Proc.Api\obj\...\rjsmrazor.dswa.cache.json` rất lâu (vòng chờ 30 giây qua ở lần thứ 15).
  - **O2b (xong 10/11/2026)**: email bản sao thông báo qua SMTP (MailKit 4.18.2, MIT). `Notification` thêm trạng thái gửi email (Chờ / Đã gửi /
    Bỏ qua / Lỗi, số lần thử, ghi chú); `NotificationEmailService.DispatchAsync` + `EmailBackgroundService` (quét mỗi `Email:PollSeconds`), bỏ
    qua người chưa khai email / ngừng dùng / thông báo > 24 giờ, thử lại tối đa 5 lần. Cấu hình mục `Email` (mặc định tắt) trong appsettings,
    `deploy/windows/appsettings.Production.example.json`, `docker-compose.yml` + `.env.example`; hướng dẫn ở deploy-windows.md, README.
    API quản trị `GET /api/notifications/email/status`, `POST /api/notifications/email/test`. Thư có nút "Mở trên phần mềm" →
    trang `/mo/:screen/:id?cty=` (đổi công ty rồi mở phiếu; hàm chung `app/notificationTarget.ts`). Migration `V0_22_0_EmailThongBao`
    (thông báo cũ = Bỏ qua). Test tích hợp `NotificationEmailTests` (bộ gửi giả), e2e `notifications.spec.ts` thêm đường dẫn email.
    Sửa cảnh báo xUnit2013 ở DynamicApproverTests. Kiểm tra: build 0 cảnh báo, dotnet test 628/628, tsc, oxlint, npm run build, Playwright
    notifications + delegations 3/3. Chưa làm: màn hình quản trị email (đang chỉ có API).
  - **O2c (xong 10/11/2026)**: nhắc quá hạn duyệt – bước đến lượt quá **1 ngày làm việc** (thứ 2–6, chưa trừ lễ; mốc = lúc gửi hoặc lúc cấp
    trước/chặng trước duyệt xong – `ApprovalOverdue.WaitingSince`, `AddWorkingDay`) thì thông báo loại `ApprovalOverdue` "Quá hạn duyệt" (kèm
    email bản sao O2b) cho người duyệt được bước lúc nhắc (gồm người được ủy quyền); nhắc lại cách 1 ngày làm việc, tối đa 3 lần/bước, không nhắc
    thứ 7/CN. `ApprovalEngine.RemindOverdueAsync` (dùng chung đường gửi với thông báo chờ duyệt), `ApprovalReminderService` +
    `ApprovalReminderBackgroundService` (mỗi 15 phút), cấu hình mục `ApprovalReminder` (mặc định bật, Testing tắt), bảng `ApprovalReminder`
    (khóa duy nhất bước + lần) – migration `V0_22_0_NhacQuaHanDuyet`. `SupplierIdsAsync` của phụ lục HĐ bỏ filter công ty (chạy nền). Ghi
    design.md 19.3, deploy-windows.md, README, appsettings.Production.example.json. Test: domain `ApprovalOverdueTests` (7), tích hợp
    `ApprovalReminderTests` (đồng hồ giả). Kiểm tra: build 0 cảnh báo, dotnet test 636/636, tsc, oxlint, npm run build qua; không chạy Playwright
    (giao diện chỉ thêm nhãn loại thông báo, dịch vụ nhắc tắt ở môi trường test).
  - **O2d (xong 11/11/2026)**: trang **duyệt gọn cho điện thoại** `/duyet` (thẻ phiếu chờ tôi duyệt, chọn công ty) và `/duyet/:loại/:id` (tóm tắt;
    PR/PO thêm dòng hàng, cảnh báo, tổng tiền, lịch sử duyệt lượt hiện tại; Duyệt có ý kiến / Từ chối bắt buộc lý do, hỏi xác nhận). Đứng ngoài
    AppShell, `ClassicScreen className="erp-mobile"` + `classicMobileTheme` + `classic/classic-mobile.css` (chữ 14px, ô 36px – ui-style.md mục 5c).
    Gọi thẳng API approve/reject sẵn có theo loại (`app/approvalRoutes.ts` – bảng đường dẫn đầy đủ dùng chung với Việc cần làm, `api/mobileApprovals.ts`);
    không đổi backend, không migration. Link email `/mo/...` trên màn hình ≤ 768px chuyển sang trang duyệt gọn; Việc cần làm có nút "Duyệt trên
    điện thoại". Ghi design.md 19.3 (O2d), ui-style.md 5c. Test e2e `mobile-approval.spec.ts` (390×844). Kiểm tra: dotnet build 0 cảnh báo,
    dotnet test 636/636, tsc, oxlint, npm run build; Playwright mobile-approval + notifications + delegations + purchasing + dashboard 8/8
    (dashboard chập chờn khi chạy song song – chạy lại qua). Playwright đủ bộ 119/122; 3 lỗi còn lại (auth, po-list "Phiên bản 0.20.0",
    purchase-analysis) do **Vite cũ còn giữ cổng 5173/5191** từ trước bản 0.21.0 (define phiên bản cũ) và tải song song – chạy lại với Vite mới
    `npx vite --port 5197 --host 127.0.0.1` + `E2E_WEB_URL=http://127.0.0.1:5197` đều qua.
  - **O2 hoàn tất** (O2a–O2d), merge vào `main` 11/11/2026. Tiếp theo theo lộ trình 0.22.0 (design.md 19.6): O3.

- **O1 – khối, Trưởng phòng ban, GĐ khối, duyệt động** (design.md mục 19.3, lộ trình 0.22.0; nhánh `feature/o1-khoi-duyet-dong`). Chia bước:
  O1a tổ chức (backend + giao diện) → O1b cấp duyệt động ở APRL (backend + giao diện) → tài liệu. "Chuỗi duyệt theo loại mua" làm cùng Y3
  (phiếu chưa có trường loại mua).
  - **O1a (xong 10/11/2026)**: danh mục **Khối** (`Division`: mã, tên, Giám đốc khối = tài khoản, ngừng dùng) – màn hình **DIV**
    (Hệ thống → Khối), cùng quyền danh mục bộ phận; bộ phận thêm Khối + **Trưởng phòng ban (tài khoản)** (giữ ô "Trưởng bộ phận (tên in)");
    người dùng thuộc bộ phận theo công ty (`UserDepartment`, ô "Bộ phận" ở tab Phân quyền của Người dùng) – PR mới tự điền bộ phận của người lập
    (`GET /api/departments/mine`). Trưởng phòng ban / GĐ khối phải là tài khoản còn hoạt động có vai trò tại công ty
    (`GET /api/departments/user-options`). Migration `V0_22_0_KhoiPhongBan` (Down mất danh mục khối, gán người/bộ phận). Test tích hợp
    `DivisionTests` (2), e2e `divisions.spec.ts`. Kiểm tra: dotnet build 0 cảnh báo, dotnet test 323 + 299 qua, tsc, oxlint, npm run build,
    Playwright divisions + departments + administration 9/9 (API 5010 + Vite 127.0.0.1:5190 – cổng 5180/5181 vướng Vite cũ).
    Lưu ý môi trường: **Google Drive đồng bộ thư mục dự án giữ file `obj\...\MvcTestingAppManifest.json`** làm `dotnet test` báo khóa file –
    chờ vài chục giây rồi build lại là qua.
  - **O1b (xong 10/11/2026)**: APRL chọn **người duyệt** của cấp: Theo vai trò / **Trưởng phòng ban của phiếu** / **GĐ khối của phiếu** (chỉ loại có
    bộ phận: PR, PO, POX, POK, PRB, POB, POL, POC – `ApprovalTypeInfo.HasDepartment`). `ApprovalRule.Kind` (RoleId cho phép null),
    `ApprovalStep.Kind` + `DepartmentId`; `DynamicApprovers` tìm người theo danh mục **hiện hành lúc duyệt**; `ApprovalEngine` chặn gửi khi thiếu
    bộ phận / Trưởng phòng ban / khối / GĐ khối, tài khoản ngừng dùng, chưa có quyền duyệt, là người lập, hoặc một người giữ hai cấp động; nút
    Duyệt, Việc cần làm, người duyệt tiếp theo (lịch sử duyệt, tra cứu chứng từ) theo đúng người. Lịch sử hiện "Trưởng phòng ban PX1",
    "GĐ khối SX". Migration `V0_22_0_DuyetDong` (Kind mặc định 1; Down xóa cấp động). Ghi quy tắc design.md mục 19.3 (lát O1), hướng dẫn sử
    dụng 01/02/08. Test: domain `NguoiDuyetDong_…`, tích hợp `DynamicApproverTests` (2), e2e `divisions.spec.ts` thêm APRL. Kiểm tra: build
    0 cảnh báo, dotnet test 325 tích hợp + 300 domain qua, tsc, oxlint, npm run build, Playwright divisions/departments/administration/
    approval-rule-template/purchasing/document-registry 15/15.
- **Rà soát lỗi 06/11/2026 – người dùng duyệt làm lát R1 → R8** (design.md 18.26). Lỗi tìm được: L1 ghi sổ trùng đồng thời (đã kiểm chứng
  tồn ×2), L2 không khóa sửa trùng, L3 một người duyệt nhiều cấp, L4 GRN ngoài PO, L5 RTV không trừ PO, L6/L7 không đóng phần còn lại PO/PR,
  L8 RFQ đếm NCC mời thay báo giá + báo giá sau hạn, L9 hủy PO đã duyệt không lý do, cảnh báo CS1573.
  - **R1 (xong, nhánh `fix/ghi-so-dong-thoi`)**: `Persistence/RowLocks.cs` (khóa `FOR UPDATE` theo thứ tự Id, `BeginOrJoinAsync`); ghi sổ/bỏ
    ghi sổ/hủy/xác nhận ICR khóa phiếu + phiếu liên kết; khóa PO khi ghi sổ phiếu nhập; gửi duyệt PO khóa PO + PR nguồn. Test
    `GhiSoCungPhieuHaiLanDongThoi_ChiGhiMotLan`, `GhiSoTrungPhieuBan_ChiMotPhieuChoNhan_BoGhiSoDuaVoiNhan_DungMotLenh`. `dotnet test` 538/538.
  - **R2 (xong, nhánh `fix/khoa-sua-trung`)**: `IVersioned` (xmin) cho 14 loại entity gốc, migration `V0_19_0_KhoaSuaTrung` (không SQL);
    `ExpectedVersionMiddleware` đọc If-Match + `{id}`, `VersionCheckInterceptor` so phiên bản, 409 cho `ConcurrencyConflictException`/
    `DbUpdateConcurrencyException`; 9 DTO có `Version`; web `apiFetch` nhớ version, tự gửi If-Match. Test `SuaTrenBanCu_Bao409_HaiNguoiDuyetCungLuc_ChiMotNguoi`,
    e2e `concurrency.spec.ts`. `dotnet test` 539/539; Playwright 98/98; build, tsc, oxlint qua.
  - **R3 (xong, nhánh `fix/duyet-khac-nguoi`)**: `ApprovalEngine.ApproveBlockerAsync` chặn người đã duyệt một cấp trong lượt; `ApprovedBy` loại họ
    khỏi người duyệt tiếp theo (engine, lịch sử duyệt, `DocumentRegistryService`). Test `DuyetNhieuCap_MoiCapMotNguoiKhacNhau_TrongMotLuot`; 7 test
    tích hợp + e2e `purchasing`, `price-reports` đổi sang hai người duyệt luân phiên. `dotnet test` 540/540; e2e duyệt 21/21.
  - **R4 (xong, nhánh `feature/grn-bat-buoc-po`)**: tham số `RECEIPT_PO_REQUIRED` (`CompanyParameter.RequireReceiptPo`, migration
    `V0_19_0_NhapMuaTheoPo`), chặn ở `CheckReceiptHeaderAsync` (lưu + ghi sổ); SEC bật/tắt. Test `NhapMuaBatBuocTheoPo_ChanPhieuNhapThang`, e2e
    `system-parameters` đếm 22 tham số. `dotnet test` 541/541; e2e system-parameters + goods-receipt qua.
  - **R5 (xong, nhánh `fix/tra-ncc-tru-po`)**: `PurchaseOrderReceipts.ReceivedBaseByLineAsync` trừ RTV; `SyncOrderStatusAsync` chạy cả với RTV (PO
    của phiếu nhập gốc), đọc SL sau khi đã ghi phiếu. Test `TraNcc_TruSlDaNhanCuaPo_MoLaiDeGiaoBu`. `dotnet test` 542/542; e2e return-transfer,
    three-way-match, purchasing, po-list 14/14.
  - **R6 (xong, nhánh `feature/dong-phan-con-lai`)**: `ApprovableDocument.CloseRemaining` + cột ClosedReason/By/At (migration `V0_19_0_DongPhanConLai`);
    `CloseRemainingAsync` PR/PO, endpoint `close-remaining`, `PurchaseDocumentActions.CanCloseRemaining`, `BudgetUsage` tính phần đã nhận;
    web nút "Đóng phần còn lại" (`confirmWithReason`), `ClosedRemainingNotice`, nhãn `purchaseStatusText`. Test `DongPhanConLai_PoNhanThieu_PrDatMotPhan`,
    e2e `close-remaining.spec.ts`. `dotnet test` 543/543; e2e mua hàng 15/15; build, tsc, oxlint qua.
  - **R7 (xong, nhánh `fix/rfq-han-nop`)**: `Rfq.SaveQuotation` chặn quá hạn, `Rfq.ExtendDeadline`, `Rfq.Award` đếm NCC chào giá + `FewQuotesReason`;
    `ApprovableDocument.Cancel(reason)` bắt lý do khi đã duyệt; endpoint `extend-deadline`, cancel nhận thân tùy chọn; migration
    `V0_19_0_RfqHanNopLyDoHuy`. Web: nút Gia hạn, hỏi giải trình ít báo giá, hỏi lý do hủy phiếu đã duyệt. Test domain
    `Rfq_HanNop_GiaHan_ItBaoGiaPhaiGiaiTrinh`, tích hợp `HuyPoDaDuyet_BatBuocLyDo` (test chọn thầu cũ thêm giải trình), e2e `rfq-deadline.spec.ts`.
    `dotnet test` 545/545; e2e đấu thầu/mua hàng 10/10; build, tsc, oxlint qua.
  - **R8 (xong, nhánh `chore/xml-doc`)**: bổ sung `<param>` cho 10 bản ghi (PO/PR/báo cáo SLA, chất lượng nhận hàng), sửa XML `&` → `&amp;`,
    chuyển chú thích `CreateSale` về đúng hàm, EF1003 ở đóng kỳ (tên bảng từ mô hình), cảnh báo nullable/hàm thừa trong test. `dotnet build`
    0 cảnh báo (trước: 90 cảnh báo khác nhau); `dotnet test` 545/545.
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
  - **Kế hoạch 03/11/2026 đã xong cả 5 bước.** Thêm DUYET-HIEN (hiện người duyệt). Bản 0.17.0 (bước 3–5 + DUYET-HIEN, 3 migration) chưa
    phát hành – chờ người dùng yêu cầu. Kiểm tra DUYET-HIEN: `dotnet test` 520/520; Playwright cả bộ 93/93; build, tsc, oxlint qua.
  - DUYET-NCC xong (03/11/2026): `dotnet test` 521/521; Playwright cả bộ 93/93 (suppliers.spec cập nhật theo popup); build, tsc, oxlint qua.
  - **Phát hành 0.17.0 (04/11/2026)**: tag `v0.17.0`, https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.17.0
    (gói `procurement-0.17.0.zip`); 3 migration (49 → 52), script `db/scripts/0.17.0.sql` chạy thử trên bản sao CSDL 0.16.0
    (`procurement_kiemtra_0170`, chạy lại lần hai không lỗi). **Bản test cổng 5007 đã nâng tại chỗ lên 0.17.0**: thư mục
    `D:\project\procurement-thu-0.15.0` (`app` = 0.17.0, `app-0.16.0`/`app-0.15.0` = bản cũ), CSDL `procurement_kiemtra_0150`, sao lưu trước
    nâng `D:\project\procurement_kiemtra_0150_truoc_0.17.0.dump`; PID trong `pid.txt`. `publish.ps1` chạy bằng `powershell -File` (gọi `&`
    trong phiên có chuyển hướng `*>` thì cảnh báo Vite ở stderr làm script dừng).
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
- **Kế hoạch 05/11/2026 (người dùng: "làm từng bước")**: (1) C3 ngân sách → (2) chặn gửi duyệt khi hồ sơ trình thiếu mục → (3) các việc
  "làm khi có yêu cầu" ở design.md (mục 4 – góp ý sau test 0.17.0 – chờ người dùng).
  - **Bước 1 – C3 (xong, nhánh `feature/ngan-sach`)**: ngân sách bộ phận × tháng (design.md 18.24): bảng `DepartmentBudget`, quyền
    `Purchase.Budget.View/Manage`, API `/api/budgets`, màn hình BUDG (`BudgetListPage`, `BudgetDrawer`); đã dùng = PO + phần PR chưa lên PO
    (`BudgetUsage`); PR/PO kiểm tra lúc lưu và gửi, vượt thì thêm chặng 3 "Vượt ngân sách" (loại PRB/POB, `ApprovalEngine.SubmitWithStagesAsync`
    thay `SubmitWithExceptionAsync`); đầu phiếu `BudgetNotice`; lịch sử duyệt `stageLabels`. Migration `V0_18_0_NganSachBoPhan` (52 → 53).
    Test: tích hợp `BudgetTests` (2), e2e `budget.spec.ts`; sửa `PurchasingTests` (danh sách loại duyệt thêm PRB, POB). Kiểm tra: `dotnet test`
    523/523; Playwright budget + pricing + purchasing + administration + permissions 18/18; `npm run build`, `tsc`, `oxlint` qua.
  - **Bước 2 (xong, nhánh `feature/chan-ho-so-trinh`)**: tham số công ty "Bắt buộc đủ hồ sơ trình khi chọn thầu" (`RFQ_DOSSIER_REQUIRED`,
    `CompanyParameter.RequireRfqDossierComplete`, mặc định Tắt – giữ hành vi cũ); bật thì `RfqService.AwardAsync` chặn, báo mục còn thiếu
    (design.md 8.6). Migration `V0_18_0_BatBuocHoSoTrinh` (53 → 54). Test: `SourcingTests.HoSoTrinh_VaTaoBangGiaTuKetQuaThau` thêm bước
    bật tham số; e2e `system-parameters` đếm 21 tham số.
  - **Ngân sách tạm dừng (người dùng 05/11/2026: "bỏ qua phần ngân sách, ghi nhận lại")**: chưa biết chặn ở PR hay PO và điều kiện chặn.
    Giữ nguyên code bước 1 (không khai ngân sách thì không ảnh hưởng). Không làm thêm việc ngân sách (bỏ "nhập ngân sách bằng Excel")
    cho tới khi người dùng chốt 8 câu hỏi ở design.md 18.24 dòng "Chưa chốt nghiệp vụ". Khi chốt có thể phải sửa `BudgetUsage`,
    `ApplyBudgetCheck`, submit PR/PO. Người dùng nhắc lại 05/11/2026: **hoãn, chỉ làm khi người dùng yêu cầu** – chưa xác nhận được
    lúc này, không hỏi lại 8 câu và không đề xuất việc ngân sách trong các đề xuất tiếp theo.
  - **Bước 3 – việc "làm khi có yêu cầu" (05/11/2026, nhánh `feature/in-pr-hang-moi`)**: người dùng "làm tiếp" không chỉ định – chọn việc
    nhỏ, an toàn: **in PR (PDF) có hàng mới chưa có mã** (`DocumentPdfService.PurchaseRequestRows`, design.md 18.12). Không migration.
    Test: `PrintingTests` thêm hàng mới vào PR, kiểm tra dòng in. Kiểm tra: `dotnet build`, `dotnet test` 523/523. Còn lại ở 18.12: phiếu tìm nguồn hiện ngược PR nguồn.
- **Phát hành 0.18.0 (06/11/2026)**: tag `v0.18.0`, https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.18.0 (gói
  `procurement-0.18.0.zip`) – C3 ngân sách, `RFQ_DOSSIER_REQUIRED`, in PR có hàng mới; 2 migration (52 → 54), script `db/scripts/0.18.0.sql`
  chạy thử 2 lần trên bản sao `procurement_kiemtra_0180` (từ CSDL test 0.17.0). **Bản test cổng 5007 nâng tại chỗ lên 0.18.0**: `app` = 0.18.0,
  `app-0.17.0` = bản cũ, sao lưu trước nâng `D:\project\procurement_kiemtra_0150_truoc_0.18.0.dump`, PID trong `pid.txt`.
- **Phát hành 0.19.0 (07/11/2026)**: tag `v0.19.0`, https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.19.0 (gói
  `procurement-0.19.0.zip`) – đợt QĐ G1–G4c (SUPCOV, GTH, PO theo phiếu tháng/POL, KSG, giá chuẩn/trần/mục tiêu, SAVSTD) + rà soát R1–R8;
  8 migration (54 → 62), script `db/scripts/0.19.0.sql` chạy thử 3 lần trên bản sao `procurement_kiemtra_0190` (từ CSDL bản test 5007,
  0.18.0), gói chạy thử trên bản sao đó: `/health` Healthy 0.19.0. Kiểm tra trước phát hành: build 0 cảnh báo, `dotnet test` 545/545.
  **Bản test cổng 5007 nâng tại chỗ lên 0.19.0 (07/11/2026)**: `app` = 0.19.0, `app-0.18.0` = bản cũ, sao lưu trước nâng
  `D:\project\procurement_kiemtra_0150_truoc_0.19.0.dump`, PID trong `pid.txt`; đã gán quyền `Pricing.MonthlyApproval.*`/`Pricing.PriceBank.*`
  qua API cho TOANQUYEN (đủ 5), TRUONGBANCUVT/HOIDONG (xem + duyệt GTH, xem KSG), DAMPHAN (lập GTH/KSG), VANHANH (xem). Chưa khai POL ở APRL:
  PO có dòng không có giá đã duyệt (nguồn "Ngoài hợp đồng") hoặc NCC ngoài danh sách GTH thêm chặng POL – người có quyền duyệt PO, khác
  người đã duyệt cấp trước (CSDL test chưa có bảng giá nào nên PO trước TC14 đều có POL).
- **Phát hành 0.20.0 (07/11/2026)**: tag `v0.20.0`, https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.20.0 (gói
  `procurement-0.20.0.zip`) – mở rộng NCC cho công ty, G5a kế hoạch nhu cầu (KHN), ngành hàng NCC từ nhóm hàng, mã hàng NCC + ĐVT mua
  trên PO/PR/RFQ, đồng bộ ô tìm `*`; 3 migration (62 → 65), script `db/scripts/0.20.0.sql` chạy thử 2 lần trên bản sao
  `procurement_kiemtra_0200` (từ CSDL bản test 5007). Kiểm tra trước phát hành: build 0 cảnh báo, `dotnet test` 556/556, Playwright
  103/104 (`auth.spec` "đổi công ty" chập chờn khi chạy song song – chạy riêng 3 lượt qua hết).
  **Bản test cổng 5007 nâng tại chỗ lên 0.20.0 (07/11/2026)**: `app` = 0.20.0, `app-0.19.0` = bản cũ, sao lưu trước nâng
  `D:\project\procurement_kiemtra_0150_truoc_0.20.0.dump`, PID trong `pid.txt`; `/health` Healthy 0.20.0; đã gán quyền
  `Purchase.DemandPlan.*` qua API: TOANQUYEN, DAMPHAN (xem + lập), TRUONGBANCUVT, HOIDONG, VANHANH (xem). NCC000026 đang giao dịch ở TCT,
  chưa mở rộng APH/BMI (để người dùng tự thử). CSDL bản sao `procurement_kiemtra_0200` còn giữ – xóa khi người dùng đồng ý.
- **Kịch bản test 0.19.0 (07/11/2026)**: `docs/DuLieuThu_KichBanTest_0.19.0.docx` – 66 kịch bản: TC01–TC38 + N01–N16 (sửa theo POL, mỗi cấp
  một người, RFQ hạn nộp) và nhóm I M01–M12 (KSG → GTH → PO theo phiếu tháng → vượt trần/hạn mức → SAVSTD trên mặt hàng VPP-00004; SUPCOV;
  R1–R7). Dựng bằng script `kb019.py` (scratchpad, không commit) trên khung file 0.14.0.
- **Tài liệu trình bày (06/11/2026)**: `docs/TrinhBay_HeThong_0.18.0.pptx` – 50 slide: quy trình, quy tắc từng phân hệ/form (theo design.md),
  từ viết tắt, 70 mã màn hình, tài khoản test và 3 kịch bản demo trên bản test 5007. Dựng bằng pptxgenjs (script ở scratchpad, không commit).
- Ghi nhớ cho **C6** (đối chiếu PO – nhận – hóa đơn) / **F3**: chặn lùi ngày PO khẩn (C2a) không phải kiểm soát chống hợp thức hóa; PO thường
  vẫn lập lùi ngày được – kiểm soát thật là đối chiếu ngày nhận/hóa đơn với ngày PO (design.md 18.11).
  Từ nay mọi màn hình làm theo docs/ui-style.md.
- Hồ sơ trình thiếu mục: từ 05/11/2026 mỗi công ty tự bật chặn ở Tham số hệ thống (`RFQ_DOSSIER_REQUIRED`, mặc định Tắt).
- **Quyết định thành lập Ban & Phòng cung ứng chung (06/11/2026)**: phân tích `docs/Purchasing.docx`, tóm tắt + quyết định người dùng ở
  `docs/tomtatquyetdinh.docx` và design.md mục 18.25 (đợt "QĐ", lát G1 → G12 – khác nhóm G1–G3 cũ của F1 lịch sử ô).
  - **G1 xong**: màn hình SUPCOV "NCC đủ điều kiện theo mặt hàng" (`/api/reports/supplier-coverage`) – đếm NCC Đã duyệt theo NCC – sản phẩm,
    mặc định tối thiểu 3, miễn khi có NCC độc quyền còn tác dụng; test tích hợp `SupplierCoverageTests`, Playwright `supplier-coverage.spec.ts`.
  - **G2 xong (06/11/2026, nhánh `feature/g2-duyet-ncc-gia-thang`)**: phiếu duyệt NCC và giá tháng – màn hình **GTH** (`MonthlyApprovalPages.tsx`),
    `MonthlySupplierApprovalService`, API `/api/monthly-supplier-approvals`, loại duyệt/đánh số GTH, quyền `Pricing.MonthlyApproval.*`, đính kèm,
    tra cứu chứng từ, Việc cần làm. Người dùng chốt: **hạn mức tháng = số lượng (gộp các công ty) để đạt bậc chiết khấu**. Migration
    `V0_19_0_PhieuDuyetNccGiaThang` (54 → 55). Test: domain `MonthlySupplierApprovalTests`, tích hợp `MonthlySupplierApprovalTests` (2),
    Playwright `monthly-approval.spec.ts`; sửa `PurchasingTests` (thêm GTH), `document-numbering.spec` (16 loại). Kiểm tra: `dotnet test` 529/529,
    Playwright 97/97, `npm run build`, `tsc`, `oxlint` qua. Người dùng đồng ý (06/11/2026) các quy tắc tự đặt: NCC chính/dự phòng phải Đã duyệt,
    không lập tháng đã qua, mỗi mặt hàng × tháng × công ty một phiếu còn hiệu lực, vai trò mẫu DAMPHAN lập – TRUONGBANCUVT/HOIDONG duyệt.
  - **G3 xong (06/11/2026, nhánh `feature/g3-po-theo-phieu-thang`)**: PO kiểm tra theo phiếu tháng – nguồn giá **Duyệt tháng** đứng đầu
    (`PurchasePriceSource.MonthlyApproval`, NCC chính/dự phòng, quy ĐVT); NCC ngoài danh sách tháng hoặc dòng không có giá đã duyệt →
    chặng 4 loại duyệt **POL** "Mua ngoài danh sách/giá đã duyệt" (thay quy tắc "chỉ gắn cờ" của 18.10); vượt hạn mức SL tháng gộp các công ty
    áp dụng → chặn gửi (`CheckMonthlyLimitsAsync`); cảnh báo đầu PO, cột Nguồn giá "Ngoài danh sách GTH-…", lịch sử duyệt chặng 4; báo cáo
    PRCW/KPI tính "Duyệt tháng" là theo giá đã duyệt. Migration `V0_19_0_PoTheoPhieuThang` (55 → 56). Test: tích hợp
    `PurchaseOrderMonthlyApprovalTests`; sửa test cũ theo quy tắc mới (danh sách loại duyệt thêm POL; fixture `PurchasingTests` có bảng giá
    đã duyệt; Dashboard/PurchaseReport/ControlAnomaly duyệt thêm chặng 4; e2e `purchasing`, `price-reports` duyệt hai lượt). Kiểm tra:
    `dotnet test` 530/530, Playwright 96/96 (2 spec sửa chạy lại qua), `npm run build`, `tsc`, `oxlint` qua.
    **Lưu ý triển khai**: từ bản này mọi PO có dòng không có giá đã duyệt đều thêm một lượt duyệt – nên cấu hình POL (APRL) trước khi phát hành.
    Quy tắc tự đặt của G3 (người dùng "làm tiếp" 06/11/2026, chưa phản đối): hạn mức đếm PO đã gửi/duyệt/đóng của mọi NCC ở các công ty áp
    dụng; chỉ kiểm tra lúc gửi; NCC ngoài danh sách vẫn tính giá theo hợp đồng/bảng giá nhưng gắn cờ và qua POL.
  - **G4 – ngân hàng giá**, kế hoạch chia lát (06/11/2026): G4a phiếu khảo sát giá (KSG) → G4b giá chuẩn/trần/mục tiêu trên dòng GTH, PO vượt
    giá trần **chỉ cảnh báo, không chặn, mọi mặt hàng** (người dùng chốt 06/11/2026 – bỏ tham số công ty) → G4c báo cáo tiết kiệm so giá chuẩn.
    Chỉ số nguyên liệu để sau.
  - **G4a xong (06/11/2026, nhánh `feature/g4a-khao-sat-gia`)**: phiếu khảo sát giá thị trường – màn hình **KSG** (`MarketSurveyPages.tsx`),
    `MarketPriceSurveyService`, API `/api/market-price-surveys` (+ `/prices` tra giá đã chốt), quyền `Pricing.PriceBank.View/Manage`, đính kèm,
    tra cứu chứng từ. Migration `V0_19_0_KhaoSatGiaThiTruong` (56 → 57). Test: domain `MarketPriceSurveyTests` (3), tích hợp
    `MarketPriceSurveyTests`, Playwright `market-survey.spec.ts`; `document-numbering.spec` 17 loại. Kiểm tra: `dotnet test` 534/534,
    Playwright 97/97, `npm run build`, `tsc`, `oxlint` qua.
  - **G4b xong (06/11/2026, nhánh `feature/g4b-gia-tran`)**: dòng phiếu GTH thêm giá chuẩn, giá trần, giá mục tiêu (không bắt buộc; giá chính,
    dự phòng, chuẩn, mục tiêu không vượt trần), cột Giá thị trường (KSG đã chốt gần nhất, giá thấp nhất, rê chuột thấy nguồn); PO có
    `CeilingPrice`/`IsOverCeiling` trên dòng và `IsOverCeiling` đầu phiếu (tính lúc đọc từ dòng phiếu tháng, chỉ PO VND), cảnh báo vàng + cột
    Nguồn giá "Vượt giá trần …". Migration `V0_19_0_GiaChuanTranMucTieu` (57 → 58). Test: domain `GiaChuanTranMucTieu_…`, tích hợp
    `PurchaseOrderMonthlyApprovalTests` (trần, cảnh báo không chặn), Playwright `monthly-approval.spec` nhập giá chuẩn/trần. Kiểm tra:
    `dotnet test` 535/535, Playwright 97/97, `npm run build`, `tsc`, `oxlint` qua.
    Quy tắc tự đặt: giá chuẩn/mục tiêu cũng không vượt trần; vượt trần so **giá ròng sau chiết khấu, trước VAT**, áp cả NCC ngoài danh sách.
  - **G4c xong (06/11/2026, nhánh `feature/g4c-tiet-kiem-gia-chuan`)**: báo cáo **SAVSTD** "Tiết kiệm so giá chuẩn"
    (`/api/reports/standard-savings`, `PurchaseReportService.GetStandardSavingsAsync`, trang `StandardSavingsReportPage`): dòng PO đã duyệt bằng
    VND có giá chuẩn trên dòng GTH, gộp theo mặt hàng – tiết kiệm = giá trị chuẩn − thành tiền, giá trị/khoảng cách mục tiêu, số dòng vượt
    trần, tỷ lệ giá trị mua có giá chuẩn. Không migration. Test: tích hợp `StandardSavingsReportTests`, Playwright `price-reports.spec` mở SAVSTD.
    Quy tắc tự đặt: chỉ PO VND; tính cả dòng NCC ngoài danh sách (có dòng phiếu tháng); tỷ lệ phủ tính trên mọi dòng PO VND đã duyệt cùng nhóm hàng.

## Việc tiếp theo

**P6a (hóa đơn điện tử Portal) đã bỏ 13/11/2026** – các lát còn lại theo design.md 19.6: P6b (trạng thái thanh toán), 0.27.0 (Y8, Y11, Y9), hoặc
phát hành 0.25.0/0.26.0 khi người dùng yêu cầu.

**Việc sau của lát P5 (người dùng đồng ý 13/11/2026 – design.md 19.5 P5-8):** phiếu báo giao NCC còn "chờ nhận" (chưa có XNH) tự đóng khi
PO đã nhận đủ / hoàn thành – trạng thái "Đã đóng (đơn đã nhận đủ)" trên Portal và tab "Giao hàng NCC", NCC không hủy / thêm file; tính từ
trạng thái PO (`DeliveryNoticeQueries.StatusOf`), không migration. Làm khi người dùng bảo làm.

**Các lát chưa làm (ghi nhận 08/11/2026 theo yêu cầu người dùng):**
- Phân tích sơ đồ phê duyệt (xong 08/11/2026): `docs/PhanTich_SoDoPheDuyet_08112026.docx` (chỉ lưu trên máy). Người dùng chốt Q1–Q6
  (design.md 18.25). Thứ tự làm: **G10 (xong)** → **G7 (xong)** → **H1 (xong)** → **H2 (xong)** → **H3 (xong)** → **H4 (xong)** → **G1b (xong)** → **G8 (xong)** → **G11 (xong)** → **G12 (xong)**; G9 hoãn.
- ~~**H1** ý kiến thẩm định của thành viên chuyên môn (GTH, RFQ, hợp đồng, hồ sơ NCC) – không phải cấp duyệt.~~ Xong 09/11/2026.
- ~~**H2** phiếu GTH có ngoại lệ → thêm chặng Chủ tịch/TGĐ (Q6).~~ Xong 09/11/2026.
- ~~**H3** PO vượt giá trần → phải Chủ tịch/TGĐ duyệt (Q3).~~ Xong 09/11/2026.
- ~~**H4** đơn vị đặt hàng xác nhận nghiệm thu trên phiếu nhập.~~ Xong 09/11/2026.
- ~~**Lỗi e2e `volume-rebate`** (CKB) chập chờn – chữ số "% chiết khấu bậc 2" rơi vào ô ngưỡng.~~ Sửa 09/11/2026 (nhánh
  `fix/e2e-volume-rebate`): nguyên nhân ở `LineGrid` – "Thêm dòng"/F2 đặt con trỏ vào dòng mới bằng `setTimeout` 50 ms và tìm dòng toàn trang;
  máy chậm thì con trỏ nhảy về ô ngưỡng giữa lúc đang gõ ô % (người dùng gõ nhanh cũng gặp; form hai lưới có thể đặt nhầm lưới). Nay đặt
  con trỏ trong `useLayoutEffect` ngay khi dòng mới vẽ xong, tìm trong chính lưới (ref); test chờ số dòng tăng rồi mới gõ.
  `dotnet test` 605/605; Playwright 113/115 lần đủ bộ (auth chụp đăng nhập, sole-sources chập chờn do tải – chạy lại qua, ép tải 12/12),
  volume-rebate ép tải 8 luồng qua; build, tsc, oxlint qua.
- ~~**G1b** đếm tối thiểu 03 NCC theo nhóm hàng (Q1).~~ Xong 09/11/2026.
- ~~**G7** đặt hàng gộp và phân bổ.~~ Xong 08/11/2026.
- ~~**G8** hồ sơ đánh giá NCC mới.~~ Xong 09/11/2026 (xem bảng Đã xong).
- **G9** biểu quyết Ban – **hoãn** (tạm dùng duyệt nhiều cấp APRL).
- ~~**G11** định mức sử dụng (căn cứ kế hoạch nhu cầu).~~ Xong 09/11/2026 (nhánh `feature/g11-dinh-muc-su-dung`): PR tính định mức lũy kế
  từ kế hoạch nhu cầu đã chốt (`PurchaseRequestService.UsageNormsAsync`, DTO dòng `NormStatus/NormQty/NormRequestedQty/NormThrough`), cột
  "Định mức LK" + cảnh báo đầu phiếu (`UsageNormNotice.tsx`), chỉ cảnh báo; không migration. Test tích hợp `UsageNormTests`, e2e
  `usage-norm.spec.ts`. `dotnet test` 605/605; Playwright 114/115 (lỗi sẵn có volume-rebate); build, tsc, oxlint qua. Quy tắc tự đặt ghi ở design.md 18.25 (G11).
- ~~**G12** báo cáo tháng cho Ban.~~ Xong 09/11/2026 (nhánh `feature/g12-bao-cao-thang-ban`, design.md 18.25 mục G12): màn hình **BCBAN**
  (Báo cáo → Báo cáo tháng cho Ban) ghép đúng số các báo cáo chi tiết – tổng quan mua + tỷ lệ mua theo giá đã duyệt (PRCW), 9 KPI, tiết kiệm
  (SAVE, SAVSTD), mua ngoài giá đã duyệt top 10, biến động giá top 10, CKB gộp (CKBT), NCC đủ điều kiện (SUPCOV), hồ sơ NCC mới (HSN), KSNB
  (chỉ người có quyền KSNB); mục thiếu quyền ghi "Không có quyền xem". Người có quyền xem toàn tập đoàn mặc định **Toàn tập đoàn** (cả 02
  công ty – người dùng yêu cầu 09/11/2026); KPI vẫn chỉ công ty đang làm việc (ghi chú trên mục). Bấm đúp mở ngăn kéo lưới cùng cột báo cáo
  chi tiết; "Xuất gói Excel" mỗi mục một sheet (`GET /api/reports/board-monthly/excel`). Không migration. DataGrid không vẽ chấm trạng thái
  khi ô trống. Test tích hợp `BoardMonthlyReportTests`, e2e `board-monthly.spec.ts`. `dotnet test` 606/606; Playwright 115/116 lần đủ bộ
  (contract.spec hộp thoại nhập Excel chập chờn – chạy lại qua); build, tsc, oxlint qua.
- **Phát hành 0.21.0 (09/11/2026)**: tag `v0.21.0`, https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.21.0 (gói
  `procurement-0.21.0.zip`) – G5b, G5c, G6a, G6b, G10, G7, G7c, H1, H2, H3, H4, G1b, G8, G11, G12; 7 migration (65 → 72), script
  `db/scripts/0.21.0.sql` chạy thử 2 lần trên bản sao `procurement_kiemtra_0210` (từ CSDL bản test 5007; số dòng PO/NCC/AuditLog giữ nguyên).
  Kiểm tra trước phát hành: build 0 cảnh báo (bổ sung XML doc `PurchaseRequestLineDto`), `dotnet test` 606/606, `npm run build`, tsc, oxlint
  qua; Playwright lần đủ bộ gần nhất 115/116 (G12). **Bản test cổng 5007 nâng tại chỗ lên 0.21.0**: `app` = 0.21.0, `app-0.20.0` = bản cũ,
  sao lưu trước nâng `D:\project\procurement_kiemtra_0150_truoc_0.21.0.dump`, PID trong `pid.txt`; `/health` Healthy 0.21.0; gán quyền mới
  qua API: TOANQUYEN (đủ 12), DAMPHAN (CKB lập, DHG lập, HSN lập/đề xuất, xem toàn tập đoàn), TRUONGBANCUVT/HOIDONG (CKB xem + duyệt, DHG
  xem, HSN xem, xem toàn tập đoàn), VANHANH/KSNB (xem CKB, DHG, HSN), DUYETNCC (xem HSN), PHAPCHE (ghi ý kiến thẩm định, xem HSN), DENGHI
  (xem chứng từ kho + nghiệm thu). Chưa tạo vai trò mẫu mới, chưa cấu hình duyệt CKB/GTX/POC/HSN (để người dùng tự thử; chưa cấu hình thì
  duyệt một cấp). CSDL bản sao `procurement_kiemtra_0200`, `procurement_kiemtra_0210` còn giữ – xóa khi người dùng đồng ý.
- Nhánh cũ đã merge `feature/d1-diem-dat-hang-lai`, `feature/h1-y-kien-tham-dinh` chưa xóa (lệnh xóa bị chặn – chờ người dùng).



0. **Đợt QĐ Ban cung ứng** (design.md 18.25): G1, G2, G3, G4a, G4b, G4c, G5a, G5b, **G5c** xong (người dùng cho làm tiếp 07/11/2026).
   G5c xong (07/11/2026), bổ sung chia theo ngày + xử lý SL lẻ (G5c+). **G6a**, **G6b**, **G10**, **G7** xong (08/11/2026). **H1**, **H2**, **H3**, **H4**, **G1b**, **G8**, **G11**, **G12** xong (09/11/2026) – đợt G1–G12 hoàn tất (G9 hoãn). G5a đã phát hành trong 0.20.0;
   G5b, G5c, G6a, G6b, G10, G7 chưa phát hành. G1–G4c đã phát hành trong 0.19.0 (bản test 5007 đã nâng lên 0.19.0).
   Đã lập `docs/DuLieuThu_KichBanTest_0.18.0.docx` (06/11/2026) cho người dùng gửi user test bản 0.18.0 cổng 5007: 38 kịch bản hồi quy
   TC01–TC38 (cập nhật: đính kèm 2 MB, PO APH duyệt 3 cấp aph.nguoib/c/d, PO nhiều cấp theo giá trị chuyển sang BMI) + 16 kịch bản mới
   N01–N16 (0.15.0–0.18.0). N01 nhắc admin gán quyền `Purchase.Budget.*` cho TOANQUYEN (CSDL test chưa có) và khai PRB/POB ở BMI.
   Dựng bằng script Python trên khung file 0.14.0 (script ở scratchpad, không commit).
1. **Theo đề xuất Ban Cung ứng** (đã chốt 01/11/2026 – `docs/Phantich_01112026.md` mục 5–6, design.md mục 18): làm lần lượt
   E1 → E2 → E3 → A1 → A2 → A3 → B1 … → F4. E1, E2, E3, A1, A2, A3, B1, B2, B3a, B3b, B4a, B4b, B4c, B5a, B5b, C1, C1b, C2a, C2b, C3, C4, C5a, C5b, C6, D1, F1, F2, F3 đã xong (C3 làm 05/11/2026); G1–G3 (F1 lịch sử ô) đã xong; F4a, F4b đã xong. Bản **0.13.0** phát hành 02/11/2026 (C1b–F4b, G1–G3):
   https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.13.0
   Bản 0.10.0 đã phát hành 01/11/2026 (SEC-2, đợt E, A1, WIN). Bản **0.12.0** phát hành 02/11/2026 (B3a–B5b, C1):
   https://github.com/dangvinhlap-dev/procurement-system/releases/tag/v0.12.0
2. Hợp đồng nguyên tắc (CTR): B4a, B4b (phụ lục), B4c (nhập Excel) xong (design.md 18.7). PO lấy giá theo hợp đồng: C1 xong (18.10);
   phạm vi bộ phận được đặt của hợp đồng: C1b xong.
3. **Không xóa** dữ liệu/CSDL nào (`procurement_perf`, `procurement_perf_mau`, `procurement_kiemtra_090` – CSDL thử script 0.9.0, `procurement_kiemtra_windows` – CSDL thử bản publish Windows, `procurement_kiemtra_0100` – CSDL thử script 0.10.0 và bản cài thử, `procurement_kiemtra_0110` – CSDL thử chuỗi script 0.1.0 → 0.11.0, `procurement_kiemtra_0130` – CSDL bản cài thử 0.13.0, `procurement_kiemtra_0170` – CSDL thử script 0.17.0,
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
- `git pull` lỗi "bad object refs/heads/main (1)" (05/11/2026): file ref trùng tên kiểu `main (1)` trong `.git/refs` (do công cụ sao chép/đồng bộ
  tạo ra). Kiểm tra commit trong đó đã nằm trong lịch sử (`git merge-base --is-ancestor`) rồi chuyển file ra ngoài `.git` – không mất gì.
