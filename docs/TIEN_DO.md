# Tiến độ dự án

Cập nhật: 11/11/2026 (bản test 5007 nâng lên 0.22.0; O3: vai trò mẫu + cấu hình duyệt mẫu theo biểu mẫu CUVT; O2: ủy quyền, email, nhắc quá hạn, duyệt trên điện thoại; 10/11: O1: khối, Trưởng phòng ban, GĐ khối, người duyệt động; K2: phiếu xác nhận nhận hàng XNH, báo cáo OTIF; K1: tắt phân hệ kho – bắt đầu hệ thống mới từ 0.22.0, design.md mục 19; tài liệu hướng dẫn sử dụng, hướng dẫn cài đặt chi tiết, ghi chú hàm nội bộ; trước đó 09/11/2026: phát hành 0.21.0, bản test 5007 nâng lên 0.21.0; G12: báo cáo tháng cho Ban BCBAN; sửa lỗi e2e volume-rebate – focus dòng mới của LineGrid; G8: hồ sơ đánh giá NCC mới HSN – backend + giao diện; G1b: đếm tối thiểu 03 NCC theo nhóm hàng; H4: đơn vị đặt hàng xác nhận nghiệm thu phiếu nhập; H3: PO vượt giá trần lên Chủ tịch/TGĐ; H2: phiếu GTH có ngoại lệ lên Chủ tịch/TGĐ; H1: ý kiến thẩm định của thành viên chuyên môn; G7c: quyền đặc biệt "Tạo PO cho công ty thành viên"; 08/11: G7: đặt hàng gộp DHG – phân bổ trong biên độ, tạo PO từng công ty; G10: vai trò mẫu + cấu hình duyệt mẫu theo sơ đồ; quyết định Q1–Q6; G6b: thực nhận chiết khấu sản lượng gộp, chia phần công ty, KSNB; G6a: thỏa thuận chiết khấu bậc sản lượng gộp CKB + theo dõi bậc CKBT; 07/11: G5c: SL kế hoạch chia theo ngày + xử lý SL lẻ; G5c lấy SL kế hoạch cho RFQ/hợp đồng; G5b tổng hợp nhu cầu gộp + quyền xem toàn tập đoàn; phát hành 0.20.0, bản test 5007 nâng lên 0.20.0; mở rộng NCC cho công ty; G5a phiếu kế hoạch nhu cầu; ngành hàng NCC từ danh mục Nhóm hàng; đồng bộ ô tìm *; mã hàng theo NCC + ĐVT mua trên PR/PO/RFQ; phát hành 0.19.0; trước đó: rà soát lỗi R1–R8, lát C3, phát hành 0.15.0–0.18.0, A4, nhập bảng giá Excel, cảnh báo giá cao, hợp đồng từ kết quả thầu, in PR có hàng mới). Đọc file này đầu mỗi phiên để biết đang ở đâu; cập nhật sau mỗi lát (CLAUDE.md mục 0).

## Đã xong (đã commit, push, merge vào `main`)

| Phần | Nội dung | Merge |
| --- | --- | --- |
| K2 (10/11/2026) | **Phiếu xác nhận nhận hàng XNH thay phiếu nhập kho** (design.md mục 19.4 – lát K2). Domain `GoodsReceiptConfirmation` (dòng theo dòng PO: SL phiếu giao / đạt = thực nhận / không đạt, kết quả kiểm tra; hóa đơn cập nhật được sau xác nhận; nghiệm thu của đơn vị đặt hàng – người lập/xác nhận không tự nghiệm thu; điều chỉnh SL thực nhận có lý do + lịch sử `GoodsReceiptAdjustment` thay trả NCC), loại chứng từ **XNH**, quyền `Purchase.Receipt.View/Manage/Accept` + vai trò mẫu (KHO, DENGHI, DONVIDATHANG, KETOAN, nhóm xem mua hàng), migration `V0_22_0_XacNhanNhanHang` (3 bảng, Down mất phiếu XNH). `/api/receipt-confirmations` (lập, sửa, confirm, cancel, invoice, acceptance, adjustments); lập/xác nhận cần nơi nhận trong phạm vi kho; SL đặt + dung sai; tham số QC bắt buộc, tách người lập PO. Nguồn "SL đã nhận" = XNH (`PurchaseOrderReceipts.ReceivedBaseByLineAsync`; code kho cũ dùng `LegacyGrnReceivedBaseByLineAsync`): PO (nhận đủ tự đóng, hủy/hoàn thành PO xét XNH), theo dõi giao hàng, ngân sách, PR đang đặt, giá nhận gần nhất, so sánh giá, QCDEV, MATCH3 + KSNB (màn hình XNH), chi tiêu/đánh giá NCC, đánh giá NCC định kỳ. Báo cáo mới **SL đặt – SL thực nhận (OTIF)** `/api/reports/order-fulfillment`. Web: màn hình Mua hàng → Xác nhận nhận hàng (`ReceiptConfirmationPages.tsx`, lưới cuộn ngang, tab Lịch sử điều chỉnh, đính kèm), nút "Lập phiếu nhận hàng" trên PO, báo cáo OTIF (theo dòng / theo NCC), liên kết QCDEV/MATCH3/KSNB/tra cứu chứng từ sang XNH. `appsettings.Testing.json` giữ `InventoryEnabled=true` cho test code kho cũ đến K3. Test: domain 7, tích hợp 3 mới + chuyển 14 test cũ sang XNH (helper `TestReceipts`); e2e `receipt-confirmation.spec.ts` + chuyển 7 spec (helper `receiptHelpers.ts`). Kiểm tra 10/11: dotnet build, dotnet test 620/620, npm run build, tsc, oxlint, Playwright 117/117 (API 5010 + Vite 5180; Vite bị crash một lần khi chạy cả bộ 8 luồng – chạy lại `--workers=4` qua). Chưa làm: in phiếu XNH ra PDF. | feature/k2-xnh |
| K1 (10/11/2026) | **Tắt phân hệ kho** – bắt đầu hệ thống mới từ 0.22.0 (người dùng đồng ý toàn bộ đề xuất Q1–Q18 của bản phân tích `PhanTich_KeHoach_0.22.0_10-11-2026.pdf`; bổ sung Q3: đơn vị mua là danh mục người dùng được cấp quyền tạo, như bộ phận; Q14: Portal có tên miền ra internet, lập trình/test trong LAN, máy chủ Windows/IIS). Ghi quyết định: **design.md mục 19** (Q1–Q18, loại mua A/B/DV/C, 7 bước + chuỗi duyệt theo biểu mẫu CUVT, bỏ kho, Portal, lộ trình 0.22.0–0.28.0), ghi chú ngừng dùng ở mục 4, 6, 7; **CLAUDE.md mục 3, 4** bỏ quy tắc bất biến về kho. Code: cấu hình `Modules:InventoryEnabled` (`ModuleOptions`, mặc định **false**; `appsettings.Testing.json` tạm **true** đến K2), `Permissions.InventoryModule` (mọi `Inventory.*`, `Report.Stock.View`, `Report.Stock.ViewAllCompanies`, `Report.Accounting.Export`) bị bỏ khi nạp phạm vi (`UserScopeLoader`), khi trả quyền cho giao diện (`AuthService`), trên danh mục quyền, vai trò mẫu, kiểm tra cấp quyền (`GrantChecker`); sửa vai trò **giữ nguyên** dòng quyền kho cũ (không xóa dữ liệu). API kho trả 403 "Phân hệ kho đã ngừng dùng từ bản 0.22.0…"; menu ẩn hẳn nhóm Kho khi không còn màn hình; `/api/group-companies` nhận thêm quyền xem bảng giá (IPR vẫn dùng). Không migration. Test: domain `InventoryModuleTests` (2), tích hợp `InventoryModuleDisabledTests` (2, API dựng với cấu hình tắt); kiểm tra tay bằng Playwright với API tắt kho (menu không còn Kho, /kho/grn báo không có quyền). Còn lại cho K2: báo cáo mua hàng (theo dõi giao hàng, MATCH3, KPI…) vẫn đọc phiếu nhập cũ và có liên kết sang /kho/grn – **người dùng chấp nhận khoảng trống này** đến K2 (10/11/2026) | feature/k1-tat-kho |
| TL-1 (10/11/2026) | **Tài liệu và ghi chú code** (người dùng yêu cầu trước khi bắt đầu phân tích viết lại từ 0.22.0): bộ **hướng dẫn sử dụng** `docs/huong-dan-su-dung/` (9 phần: làm quen, danh mục, mua hàng, giá và NCC, đấu thầu, kho, báo cáo, hệ thống, quy trình mẫu + câu hỏi thường gặp – theo bản 0.21.0); **hướng dẫn cài đặt chi tiết** `docs/huong-dan-cai-dat.md` (máy dev, Windows Server/IIS, Ubuntu/Docker, bản test mạng nội bộ, nâng cấp/sao lưu/khôi phục/quay lại, bảng cấu hình, xử lý sự cố); README trỏ tới hai tài liệu. **Ghi chú code**: 308 hàm/kiểu private/internal C# thêm `/// <summary>`, 84 hàm TS (src, e2e) thêm JSDoc – quét lại không còn hàm thiếu ghi chú; public C# đã đủ XML doc (build 0 cảnh báo). Không đổi logic. Kiểm tra: `dotnet build` 0 cảnh báo, `dotnet test` qua, `tsc`, `oxlint` qua. **Chưa đẩy lên GitHub** 2 file `docs/CUVT nhóm A/B 08.01.2025. (final).xlsx` vì repo đang **công khai** và file chứa dữ liệu mua thật – chờ người dùng quyết | feature/tai-lieu-ghi-chu |
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
