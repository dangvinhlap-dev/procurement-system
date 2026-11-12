# Nhật ký thay đổi

Ghi theo [Semantic Versioning](https://semver.org/lang/vi/) – xem CLAUDE.md mục 8.
Mỗi bản phát hành có Git tag `vx.y.z`.

## [0.23.0] - 2026-11-12

Quy tắc chung cho chứng từ có phê duyệt (design.md mục 20) và đợt hai của quy trình biểu mẫu CUVT (design.md mục 19.3): công trình – dự
toán, đề nghị mua theo loại A/B/DV có Thủ kho nhập tồn, bảng tổng hợp đề xuất mua 2A/2B, in theo biểu mẫu CUVT.

### Thêm mới
- **Ý kiến + đính kèm từng bước duyệt**: hộp thoại Duyệt / Từ chối có ô ý kiến và đính kèm file; tab Đính kèm có cột "Bước duyệt", "Đã khóa".
  Phiếu đã duyệt thì khóa đính kèm (không thêm/xóa).
- **Ổ khóa vàng từng bước** trên tiêu đề phiếu (đóng = đã duyệt, mở = chưa) và nút **"Kiểm tra phê duyệt"** (bước 0…n: tên đăng nhập, họ
  tên, chức vụ, giờ ngày, ý kiến). Người dùng có thêm ô **Chức vụ** (trống thì lấy tên vai trò).
- **Ô số chứng từ**: bấm để sao chép, bấm đúp (hoặc Enter) mở phiếu gốc – trên form, danh sách và báo cáo.
- **Hủy dây chuyền** từ phiếu cuối về phiếu đầu, có hộp thoại trình tự: PR → BTH → PO → XNH, DHG → PO (mọi công ty) → XNH, RFQ → PO → XNH
  + hợp đồng / bảng giá nháp tạo từ RFQ. Chuỗi có XNH đã xác nhận hoặc hợp đồng / bảng giá đã duyệt thì chặn hủy. Bảng giá có trạng thái Đã hủy.
- Nút **Rút về nháp** cho đề nghị mua (PR) và đơn mua hàng (PO).
- Màn hình **CTDT – Công trình – dự toán** (Mua hàng): danh mục công trình theo công ty, điều chỉnh dự toán có lý do và lịch sử, lũy kế đã
  mua / còn lại theo PO đã duyệt (chỉ dòng lấy từ đề nghị loại A).
- Đề nghị mua có **Loại mua** (A công trình, B hàng hóa, DV dịch vụ) và **Công trình** (bắt buộc với loại A); thông báo khung dự toán.
- **Thủ kho nơi nhận nhập tồn** trên đề nghị mua (loại A/B): cột SL cần sử dụng, Tồn, Cần mua = max(cần − tồn, 0); nút "Nhập tồn".
- Chuỗi duyệt đề nghị mua **theo loại mua**: bộ cấp PRA (loại A), PRS (dịch vụ), PRE (vượt dự toán); loại người duyệt mới **"Thủ kho nơi
  nhận"** (phải nhập tồn rồi mới duyệt). Phần vượt dự toán / ngân sách được xem trước khi trình cấp cao nhất.
- Màn hình **DVM – Đơn vị mua** (Mua hàng): danh mục đơn vị mua theo công ty.
- Màn hình **BTH – Bảng tổng hợp đề xuất mua** (2A theo công trình, 2B hàng hóa/dịch vụ): chọn dòng đề nghị đã duyệt, gán đơn vị mua từng
  dòng, khung chi phí đầu phiếu, duyệt theo bộ cấp BTH (mẫu CUVT: TP CUVT → GĐ khối → Phó CT UBCL&PT → Chủ tịch), duyệt trên điện thoại.
- **In theo biểu mẫu CUVT**: đề nghị mua in mẫu 1A / 1B / DV; bảng tổng hợp in mẫu 2A / 2B khổ ngang; khối ký lấy từ chuỗi duyệt.
  "Lũy kế số tiền đã mua trong tháng" chỉ tính PO đã duyệt trong tháng, chưa VAT.
- Quyền mới: `Purchase.Project.View/Manage`, `Purchase.PR.EnterStock`, `Purchase.Unit.View/Manage`, `Purchase.Summary.View/Manage/Approve`.

### Thay đổi
- **Hủy phiếu chỉ quản trị** (quyền `System.Document.Cancel` chỉ có ở ADMIN, không gán được cho vai trò khác); phiếu **Nháp** thì người lập tự
  hủy được; mọi lần hủy **bắt buộc lý do**.
- **Mọi công ty bắt buộc qua bảng tổng hợp**: dòng đề nghị mua chỉ lên RFQ / PO khi thuộc bảng tổng hợp **đã duyệt**. Đề nghị đang nằm
  trong bảng tổng hợp chưa hủy thì không hủy riêng được (dùng hủy dây chuyền).
- Đề nghị mua thôi đọc tồn kho của phân hệ kho cũ; tồn do Thủ kho nhập tay.
- Mẫu cấu hình duyệt CUVT: PR 8 cấp, PRA 7, PRS 7, PRE, BTH 4 cấp; vai trò mẫu thêm các quyền mới.

### Sửa lỗi
- Xem trước chuỗi duyệt bảng tổng hợp báo lỗi 500 (lệnh gửi mở giao dịch thứ hai).

### Thay đổi CSDL
- 10 migration mới (78 → 88): `V0_23_0_DinhKemBuocDuyet`, `V0_23_0_ChucVuNguoiDung`, `V0_23_0_LyDoHuyPhieu`, `V0_23_0_HuyBangGia`,
  `V0_23_0_CongTrinhDuToan`, `V0_23_0_LoaiMuaDeNghi`, `V0_23_0_ThuKhoNhapTon`, `V0_23_0_BuocThuKho`, `V0_23_0_DonViMua`,
  `V0_23_0_BangTongHopDeXuat`. Chỉ thêm bảng/cột; `ThuKhoNhapTon` điền SL cần sử dụng = SL đề nghị cho dòng cũ.
  Script: `db/scripts/0.23.0.sql` (đã chạy thử 2 lần trên bản sao CSDL bản test 0.22.2, không lỗi, số dòng PR/PO/AuditLog giữ nguyên).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước** (`backup.ps1` / `db/backup.sh`), rồi chạy `db/scripts/0.23.0.sql`, sau đó thay gói ứng dụng, giữ
  `appsettings.Production.json`. Nếu nâng từ bản cũ hơn 0.22.0: chạy `0.22.0.sql` trước.
- **Bảng tổng hợp bắt buộc ngay sau khi nâng**: đề nghị mua đã duyệt nhưng chưa lên PO/RFQ phải lập bảng tổng hợp (BTH) và duyệt trước;
  PO nháp đang lấy dòng đề nghị chưa qua bảng sẽ bị chặn khi lưu / gửi duyệt.
- Cấu hình duyệt: khai bộ cấp **BTH** (bắt buộc để duyệt bảng tổng hợp); PRA / PRS / PRE tùy chọn (chưa khai PRA/PRS thì dùng PR). Có thể dùng
  "Cấu hình duyệt mẫu…" bộ CUVT. Chuỗi có cấp "Thủ kho nơi nhận" cần đề nghị ghi Nơi nhận (kho) và kho đó có thủ kho được phân phạm vi.
- Gán quyền mới cho vai trò đang dùng (hoặc dùng "Bộ vai trò mẫu…"): xem/quản lý công trình, đơn vị mua, bảng tổng hợp; nhập tồn cho thủ kho.
  Khai danh mục **Đơn vị mua** (DVM) và **Công trình** (CTDT) trước khi lập bảng tổng hợp / đề nghị loại A.
- Quyền Hủy phiếu cũ đã gán cho vai trò khác ADMIN không còn tác dụng. Điền **Chức vụ** cho người duyệt để in khối ký đúng.
- Người dùng tải lại trang (Ctrl+F5) để trình duyệt nhận giao diện mới.

### Cách quay lại bản trước
- Cách 1: khôi phục bản sao lưu trước nâng cấp và triển khai lại tag `v0.22.2`.
- Cách 2: gỡ migration về `20261110100424_V0_22_0_NhacQuaHanDuyet`. `Down()` xóa bảng công trình – dự toán, đơn vị mua, bảng tổng hợp và
  các cột mới (chức vụ, lý do hủy, loại mua, công trình, SL cần sử dụng / tồn, bước thủ kho, liên kết đính kèm – bước duyệt), nên **mất dữ
  liệu các chức năng mới đã nhập**. Bảng giá đã hủy ở 0.23.0 sẽ mang trạng thái bản cũ không biết – nên dùng cách 1.

## [0.22.2] - 2026-11-11

Bản vá thứ hai cho 0.22.0 (phản ánh khi test bản 0.22.0).

### Thêm mới
- Đơn mua hàng (PO): chọn mặt hàng hoặc đổi ĐVT mà mặt hàng có giá đã duyệt (phiếu duyệt tháng, kết quả chọn thầu, hợp đồng, bảng giá) thì
  **tự điền** đơn giá, % CK, thuế suất – không cần bấm "Áp giá đã duyệt". Đã gõ đơn giá trong lúc giá đang tải thì giữ số đã gõ.

### Thay đổi
- Thanh menu chữ chỉ hiện nhóm có màn hình được phép, như cây điều hướng (trước đây nhóm trống vẫn hiện, bấm vào báo "Không có màn hình
  được phép" – ví dụ thủ kho thấy Đấu thầu, Báo cáo).
- Tài liệu test `docs/DuLieuThu_KichBanTest_0.22.0.docx`: sửa đường dẫn menu (K04, K06), tên nút (K09), tên cột "SL thực nhận", bước nhập
  đơn giá (K02), ghi phiên bản 0.22.2.

### Sửa lỗi
- Không có thêm (sửa lỗi cột SL thực nhận ở 0.22.1).

### Thay đổi CSDL
- Không có (vẫn 78 migration như 0.22.0, không có script 0.22.2).

### Lưu ý khi nâng cấp
- Từ 0.22.0 hoặc 0.22.1: chỉ thay gói ứng dụng, giữ `appsettings.Production.json`; không cần chạy script CSDL. Nên sao lưu CSDL theo thói quen.
- Từ bản cũ hơn 0.22.0: làm theo mục "Lưu ý khi nâng cấp" của 0.22.0 (chạy `db/scripts/0.22.0.sql`), rồi cài gói 0.22.2.
- Người dùng tải lại trang (Ctrl+F5) để trình duyệt nhận giao diện mới.

### Cách quay lại bản trước
- Triển khai lại gói tag `v0.22.1`; không cần thao tác CSDL.

## [0.22.1] - 2026-11-11

Bản vá cho 0.22.0 (phản ánh khi test bản 0.22.0).

### Thêm mới
- Không có.

### Thay đổi
- Lưới dòng hàng của đơn mua hàng (PO) cuộn ngang khi nhiều cột, các cột giữ độ rộng mặc định thay vì bị ép hẹp.

### Sửa lỗi
- PO đã nhận hàng nhưng người dùng không thấy SL thực nhận: cột "SL thực nhận", "Còn lại" nằm cuối lưới 17 cột nên bị đẩy khỏi khung
  nhìn. Hai cột nay đứng ngay sau "SL đặt". Số liệu nhận hàng (từ phiếu XNH đã xác nhận) vốn đã đúng.

### Thay đổi CSDL
- Không có (vẫn 78 migration như 0.22.0, không có script 0.22.1).

### Lưu ý khi nâng cấp
- Từ 0.22.0: chỉ thay gói ứng dụng, giữ `appsettings.Production.json`; không cần chạy script CSDL. Nên sao lưu CSDL theo thói quen.
- Từ bản cũ hơn 0.22.0: làm theo mục "Lưu ý khi nâng cấp" của 0.22.0 (chạy `db/scripts/0.22.0.sql`), rồi cài gói 0.22.1.

### Cách quay lại bản trước
- Triển khai lại gói tag `v0.22.0`; không cần thao tác CSDL.

## [0.22.0] - 2026-11-11

Bản đầu của hệ thống mới theo biểu mẫu CUVT (design.md mục 19): **tắt phân hệ kho** – công ty dùng phần mềm kho riêng.

### Thêm mới
- **Phiếu xác nhận nhận hàng XNH** (màn hình XNH, Mua hàng → Xác nhận nhận hàng; lát K2): lập theo một PO đã duyệt, mỗi dòng ghi SL theo phiếu giao, SL
  đạt (= SL thực nhận), SL không đạt, kết quả kiểm tra; hóa đơn NCC (cập nhật được sau khi xác nhận); Nháp → Đã xác nhận. PO tự chuyển "Đã
  nhận đủ" khi nhận đủ. **Điều chỉnh SL thực nhận** có lý do và lịch sử, thay cho phiếu trả NCC. Đơn vị đặt hàng **nghiệm thu** Đạt/Không đạt.
  Quyền mới `Purchase.Receipt.View/Manage/Accept`.
- **Báo cáo SL đặt – SL thực nhận / OTIF** (màn hình OTIF, Báo cáo → SL đặt – thực nhận; lát K2): từng dòng PO đã duyệt, còn thiếu, đúng hạn, đủ; % đủ, % đúng hạn, % OTIF theo NCC.
- **Danh mục Khối** (màn hình DIV, Hệ thống → Khối; lát O1) có Giám đốc khối. Bộ phận thêm **Khối** và **Trưởng phòng ban (tài khoản)**;
  người dùng thuộc một bộ phận ở mỗi công ty (tab Phân quyền của Người dùng) – đề nghị mua mới tự điền bộ phận của người lập.
- **Người duyệt động** ở Quy trình duyệt (lát O1): cấp duyệt chọn được "Trưởng phòng ban của phiếu" hoặc "GĐ khối của phiếu" (PR, PO và
  các chặng phụ). Người duyệt xác định theo danh mục lúc duyệt; thiếu khai báo thì chặn gửi duyệt, báo rõ chỗ sửa.
- **Ủy quyền duyệt có thời hạn** (màn hình UQD, Hằng ngày → Ủy quyền duyệt; lát O2a): ủy quyền một loại phiếu hoặc mọi loại, tối đa 90 ngày,
  có lý do và đính kèm; người được ủy quyền duyệt thay, lịch sử hiện "Bình (thay An)"; thu hồi được. Quyền mới
  `Organization.Delegation.Manage` (lập thay người khác, xem mọi ủy quyền).
- **Email bản sao thông báo** qua SMTP (lát O2b): mọi thông báo gửi thêm thư tới email của người dùng, nút "Mở trên phần mềm". Mặc định
  **tắt** – bật bằng mục `Email` trong appsettings (Host, Port, tài khoản, `AppBaseUrl`). API kiểm tra: `GET /api/notifications/email/status`,
  `POST /api/notifications/email/test`.
- **Nhắc quá hạn duyệt** (lát O2c): bước chờ quá 1 ngày làm việc (thứ 2–6) thì nhắc người duyệt, nhắc lại mỗi ngày làm việc, tối đa 3 lần.
  Cấu hình mục `ApprovalReminder` (mặc định bật).
- **Duyệt trên điện thoại** (trang `/duyet`; lát O2d): thẻ phiếu chờ tôi duyệt, xem tóm tắt (dòng hàng, cảnh báo, lịch sử duyệt), Duyệt /
  Từ chối. Việc cần làm có nút "Duyệt trên điện thoại"; link trong email mở trên màn hình nhỏ tự chuyển sang trang này.
- **Vai trò mẫu và cấu hình duyệt mẫu theo biểu mẫu CUVT** (lát O3): hộp thoại "Bộ vai trò mẫu…" và "Cấu hình duyệt mẫu…" có ô chọn bộ
  mẫu. Bộ CUVT thêm 8 vai trò (Trưởng phòng ban, GĐ khối, TP/NV CUVT, TP/GĐ TC-NQ-KT, TP Hành chính, Phó CT UBCL&PT) và chuỗi duyệt PR 6 cấp,
  RFQ 5 cấp, PO 4 cấp theo biểu mẫu.

### Thay đổi
- **Tắt phân hệ kho** (lát K1, cấu hình `Modules:InventoryEnabled`, mặc định `false`): ẩn menu, màn hình, báo cáo kho; API kho trả 403.
  Quyền kho bị bỏ qua khi đăng nhập (dữ liệu quyền, phiếu kho, sổ kho cũ **giữ nguyên**). Danh mục kho giữ làm nơi nhận hàng. Đặt `true` để
  dùng lại như 0.21.0.
- **SL đã nhận** của PO, theo dõi giao hàng, ngân sách, MATCH3, KSNB, đánh giá NCC, báo cáo mua hàng đọc từ **phiếu XNH** thay phiếu nhập kho.
- Nút "Cấu hình duyệt mẫu theo sơ đồ…" đổi tên thành "Cấu hình duyệt mẫu…"; vai trò mẫu CHUTICH thêm quyền duyệt kết quả chọn thầu.
- Hướng dẫn sử dụng (`docs/huong-dan-su-dung`) và hướng dẫn cài đặt chi tiết (`docs/huong-dan-cai-dat.md`).

### Sửa lỗi
- Migration `V0_22_0_EmailThongBao`: câu SQL viết tay thiếu dấu `;` làm script idempotent lỗi cú pháp (phát hiện khi chạy thử script, đã sửa
  trước khi phát hành).

### Thay đổi CSDL
- 6 migration mới (72 → 78): `V0_22_0_XacNhanNhanHang`, `V0_22_0_KhoiPhongBan`, `V0_22_0_DuyetDong`, `V0_22_0_UyQuyenDuyet`,
  `V0_22_0_EmailThongBao`, `V0_22_0_NhacQuaHanDuyet`. Chỉ thêm bảng/cột; thông báo cũ đánh dấu "Bỏ qua" gửi email. Không xóa bảng kho.
  Script: `db/scripts/0.22.0.sql` (đã chạy thử 2 lần trên bản sao CSDL test 0.21.0, không lỗi, dữ liệu giữ nguyên).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước** (`backup.ps1` / `db/backup.sh`), rồi chạy `db/scripts/0.22.0.sql`, sau đó thay gói ứng dụng. Nếu nâng từ bản
  cũ hơn 0.21.0: chạy lần lượt các script trước đó.
- **Phân hệ kho tắt ngay sau khi nâng**. SL đã nhận của PO chỉ tính phiếu XNH: PO đã nhận bằng phiếu nhập kho cũ sẽ hiện SL nhận = 0 – lập
  XNH cho phần đã nhận hoặc "Hoàn thành PO" nếu không cần theo dõi tiếp.
- Gán quyền mới cho vai trò đang dùng: `Purchase.Receipt.View/Manage/Accept` (thủ kho, phòng ban đề xuất, đơn vị đặt hàng),
  `Organization.Delegation.Manage` (quản trị). Có thể dùng "Bộ vai trò mẫu…" (bộ Vị trí ký biểu mẫu CUVT) và "Cấu hình duyệt mẫu…".
- Dùng người duyệt động: khai Khối, Giám đốc khối, Trưởng phòng ban của bộ phận trước khi gửi duyệt phiếu.
- Email: điền mục `Email` ở `appsettings.Production.json` hoặc biến môi trường `Email__...` (mật khẩu chỉ đặt trên máy chủ), `Enabled=true`.
- Nhắc quá hạn chạy ngay sau khi nâng: các phiếu đang chờ lâu sẽ nhận nhắc (tối đa 3 lần). Muốn tắt: `ApprovalReminder:Enabled=false`.

### Cách quay lại bản trước
- Cách 1: khôi phục bản sao lưu trước nâng cấp và triển khai lại tag `v0.21.0`.
- Cách 2: gỡ migration về `20261109062314_V0_20_0_HoSoDanhGiaNccMoi`. `Down()` xóa bảng XNH, khối, ủy quyền, nhắc quá hạn và các cột mới
  (Trưởng phòng ban, người duyệt động, trạng thái email), nên **mất dữ liệu các chức năng mới đã nhập**. Bảng kho không bị ảnh hưởng.

## [0.21.0] - 2026-11-09

### Thêm mới
- **Tổng hợp nhu cầu gộp** (màn hình KHNTH, Mua hàng → Tổng hợp nhu cầu; lát G5b): cộng các phiếu kế hoạch nhu cầu đã chốt, quy về ĐVT cơ
  sở, mỗi công ty một cột, theo năm, quý hoặc tháng, lọc theo nhóm hàng. Quyền mới `Purchase.DemandPlan.ViewAllCompanies` cho phép xem toàn tập đoàn.
- **Lấy SL kế hoạch** cho RFQ và hợp đồng nguyên tắc (lát G5c): chọn khoảng ngày (tối đa 36 tháng, tháng không trọn tính theo số ngày),
  xem trước số lượng theo từng công ty rồi chép vào lưới. Số lượng lẻ theo ĐVT dòng có ba cách xử lý: làm tròn lên, đổi sang ĐVT cơ sở (chỉ RFQ) hoặc giữ số lẻ.
- **Chiết khấu bậc theo sản lượng gộp** (màn hình CKB và báo cáo theo dõi CKBT; lát G6a, G6b): tổng công ty lập thỏa thuận theo NCC × nhóm
  hàng, gồm bậc ngưỡng và % chiết khấu, rồi trình duyệt. Tab Lũy kế cộng sản lượng PO của các công ty và dự báo bậc đạt được. Tab Thực nhận ghi
  khoản chiết khấu đã nhận sau khi kỳ kết thúc, chia cho từng công ty theo tỷ lệ doanh số và cảnh báo khoản thiếu đã quá hạn. Quyền mới
  `Pricing.VolumeRebate.View/Manage/Approve`.
- **Đặt hàng gộp và phân bổ** (màn hình DHG; lát G7, G7c): tổng công ty đặt hàng theo phiếu GTH đã duyệt, phân bổ số lượng cho từng công ty
  (đề xuất theo kế hoạch nhu cầu, biên độ theo tham số `POOLED_MARGIN_PERCENT`, mặc định 10%). Nút "Tạo PO" lập mỗi công ty một PO nháp.
  Quyền mới `Purchase.PooledOrder.View/Manage`. Quyền đặc biệt `Purchase.PooledOrder.CreateMemberPo` cho phép tạo PO cho công ty thành viên
  dù người dùng không có vai trò ở công ty đó.
- **Vai trò mẫu và cấu hình duyệt mẫu theo sơ đồ phê duyệt** (lát G10): thêm 9 vai trò mẫu (Chủ tịch/TGĐ, Phó ban, thẩm định chất lượng/pháp
  lý, Văn phòng Ban, Phòng cung ứng…) và hộp thoại "Cấu hình duyệt mẫu theo sơ đồ…" ở màn hình Quy trình duyệt. Hộp thoại chỉ tạo cấu hình
  cho loại phiếu chưa có, mọi cấp đặt ngưỡng 0.
- **Ý kiến thẩm định của thành viên chuyên môn** (tab THẨM ĐỊNH trên GTH, RFQ, hợp đồng, NCC; lát H1): ghi Đạt hoặc Không đạt, có nội dung
  và đính kèm. Ý kiến không phải một cấp duyệt nhưng hiện trong hộp thoại Duyệt. Quyền mới `Sourcing.Appraisal.Give`.
- **Phiếu GTH có ngoại lệ phải lên Chủ tịch/TGĐ** (lát H2): ngoại lệ gồm dưới 03 NCC đủ điều kiện, NCC độc quyền, giá vượt giá mục tiêu.
  Phiếu có ngoại lệ phải giải trình mới gửi được và thêm chặng duyệt GTX.
- **PO vượt giá trần phải lên Chủ tịch/TGĐ** (lát H3): thêm chặng duyệt POC. Trước đây vượt giá trần chỉ cảnh báo.
- **Đơn vị đặt hàng xác nhận nghiệm thu** trên phiếu nhập mua đã ghi sổ (lát H4): chọn Đạt hoặc Không đạt (Không đạt phải ghi ý kiến).
  Người lập hoặc ghi sổ phiếu không tự xác nhận được. Kết quả hiện trên màn hình đối chiếu 3 chiều MATCH3. Quyền mới `Inventory.Receipt.Accept`.
- **Hồ sơ đánh giá NCC mới** (màn hình HSN; lát G8): giấy tờ pháp lý, 6 tiêu chí đánh giá, đề xuất duyệt qua luồng duyệt NCC. NCC tạo từ bản
  này phải có hồ sơ được duyệt mới chuyển sang Đã duyệt được; tham số tổng công ty `SUPPLIER_ONBOARDING_OPTIONAL` cho phép bỏ qua.
  Quyền mới `MasterData.SupplierOnboarding.View/Manage/Propose`.
- **Định mức sử dụng trên PR** (lát G11): cột "Định mức LK" tính lũy kế từ kế hoạch nhu cầu đã chốt; cảnh báo đầu phiếu khi đề nghị vượt
  định mức. Chỉ cảnh báo, không chặn.
- **Báo cáo tháng cho Ban** (màn hình BCBAN, Báo cáo → Báo cáo tháng cho Ban; lát G12): ghép số liệu tổng quan mua, KPI, tiết kiệm, mua ngoài
  giá đã duyệt, biến động giá, CKB, NCC đủ điều kiện, hồ sơ NCC mới và KSNB. Mặc định xem toàn tập đoàn nếu người dùng có quyền. Nút
  "Xuất gói Excel" xuất mỗi mục một sheet.

### Thay đổi
- Màn hình SUPCOV đếm số NCC tối thiểu (03) **theo nhóm hàng** thay vì theo mặt hàng (lát G1b).

### Sửa lỗi
- Lưới dòng (LineGrid): khi bấm "Thêm dòng" hoặc F2, con trỏ có lúc nhảy về ô khác trong lúc đang gõ (gặp trên máy chậm hoặc form có hai lưới).
- Đèn trạng thái trong một số khung hiện thành ô trắng.

### Thay đổi CSDL
- 7 migration mới (65 → 72): `V0_20_0_ChietKhauSanLuongGop`, `V0_20_0_ThucNhanChietKhauSanLuong`, `V0_20_0_DatHangGop`,
  `V0_20_0_YKienThamDinh`, `V0_20_0_GthNgoaiLe`, `V0_20_0_XacNhanNghiemThu`, `V0_20_0_HoSoDanhGiaNccMoi` (tên mang tiền tố 0.20.0 vì
  được lập trước khi chốt số bản). Các migration chỉ thêm bảng/cột, không xóa dữ liệu. Script: `db/scripts/0.21.0.sql` (đã chạy thử 2 lần
  trên bản sao CSDL test 0.20.0, không lỗi, dữ liệu giữ nguyên).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước** (`backup.ps1` / `db/backup.sh`), rồi chạy `db/scripts/0.21.0.sql`, sau đó thay gói ứng dụng. Nếu nâng từ bản
  cũ hơn 0.20.0: chạy lần lượt các script trước đó.
- Gán các quyền mới nêu ở trên cho vai trò đang dùng. Có thể dùng "Bộ vai trò mẫu…" và "Cấu hình duyệt mẫu theo sơ đồ…" để tạo nhanh.
- Các loại duyệt mới CKB, GTX, POC, hồ sơ NCC: nếu công ty chưa cấu hình ở Quy trình duyệt thì phiếu chỉ cần duyệt một cấp, ai có quyền duyệt cũng duyệt được.
- NCC tạo mới sau khi nâng cấp phải có hồ sơ HSN được duyệt. Nếu chưa muốn áp dụng, bật tham số `SUPPLIER_ONBOARDING_OPTIONAL` ở tổng công ty.

### Cách quay lại bản trước
- Cách 1: khôi phục bản sao lưu trước nâng cấp và triển khai lại tag `v0.20.0`.
- Cách 2: gỡ migration về `20261107031744_V0_20_0_KeHoachNhuCau`. `Down()` xóa các bảng CKB, đặt hàng gộp, ý kiến thẩm định, hồ sơ NCC mới
  và cột ngoại lệ GTH, nghiệm thu phiếu nhập, nên **mất dữ liệu các chức năng mới đã nhập**.

## [0.20.0] - 2026-11-07

### Thêm mới
- **Mở rộng NCC cho công ty** (tab "Mở rộng cho công ty" ở ngăn kéo NCC): bảng mọi công ty với tình trạng Đang giao dịch / Ngừng
  giao dịch / Chưa mở rộng, mã NCC tại công ty, ngày bắt đầu. Ở tổng công ty, người có quyền `MasterData.Supplier.Manage` mở rộng NCC
  sẵn có cho một, vài hoặc tất cả công ty thành viên (không phải tạo NCC mới), hoặc ngừng giao dịch; mở lại giữ mã và ngày công ty đã khai.
- **Phiếu kế hoạch nhu cầu** (màn hình KHN, Mua hàng → Kế hoạch nhu cầu, số `KHN-…`; lát G5a): mỗi công ty lập theo năm × nhóm hàng,
  dòng mặt hàng × ĐVT với số lượng 12 tháng, tổng quý/cả năm tự tính; Nháp → Đã chốt; điều chỉnh bằng phiếu mới (phiếu cũ thành Đã thay);
  hủy phải ghi lý do; quyền mới `Purchase.DemandPlan.View/Manage`.
- **Ngành hàng NCC chọn từ danh mục Nhóm hàng**: chọn nhiều nhóm ở ngăn kéo NCC, nút "Thêm nhóm hàng" tạo ngay; danh sách NCC có cột và
  bộ lọc Ngành hàng (gồm nhóm con); Excel cột Ngành hàng ghi mã nhóm.
- **Mã hàng theo NCC và ĐVT mua** trên PO, PR, báo giá RFQ: chọn mặt hàng tự lấy ĐVT mua của NCC, cột "Mã hàng NCC"; PO chép mã vào dòng
  khi lưu/gửi, phiếu in PO ghi mã NCC.

### Thay đổi
- Danh mục nhóm hàng ai cũng xem đủ (nhóm ngoài phạm vi chỉ xem, không sửa, không gán cho mặt hàng); mặt hàng, tồn kho, chứng từ vẫn lọc
  theo phạm vi nhóm hàng.
- Không cho trùng tên nhóm hàng trong cùng nhóm cha (so không dấu), cảnh báo ngay khi gõ.
- Mọi ô chọn có tìm kiếm: gõ `*` rồi Enter liệt kê toàn bộ danh mục; ô cây nhóm hàng tìm được không dấu; ô lệnh gõ `*` liệt kê mọi màn hình.
- Chữ tự do "Phạm vi cung cấp" cũ của NCC chỉ còn để xem.

### Sửa lỗi
- Hộp thoại "Thêm mặt hàng NCC bán": gõ `*` không liệt kê mặt hàng; ĐVT mua chưa tự điền ĐVT cơ sở.
- Bấm Esc đóng hộp thoại F1 làm đóng luôn ngăn kéo chi tiết.

### Thay đổi CSDL
- 3 migration mới (62 → 65): `V0_20_0_MaHangNccTrenPo`, `V0_20_0_NganhHangNcc`, `V0_20_0_KeHoachNhuCau`. Chỉ thêm bảng/cột, không xóa
  dữ liệu. Script: `db/scripts/0.20.0.sql` (đã chạy thử 2 lần trên bản sao CSDL test 0.19.0, không lỗi, dữ liệu giữ nguyên).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước** (`backup.ps1` / `db/backup.sh`), rồi chạy `db/scripts/0.20.0.sql`, sau đó thay gói ứng dụng.
  Từ bản cũ hơn 0.19.0: chạy lần lượt các script trước đó.
- Gán quyền mới `Purchase.DemandPlan.View/Manage` cho vai trò đang dùng (vai trò mẫu DAMPHAN đã có sẵn khi tạo mới).
- NCC mà công ty thành viên đang mua nhưng chưa áp dụng tại công ty đó sẽ không chọn được khi lập PO: mở rộng từ tổng công ty ở tab
  "Mở rộng cho công ty".

### Cách quay lại bản trước
- Khôi phục bản sao lưu trước nâng cấp và triển khai lại tag `v0.19.0`; hoặc gỡ migration về `20261106104102_V0_19_0_RfqHanNopLyDoHuy`
  (`Down()` xóa bảng kế hoạch nhu cầu, ngành hàng NCC và cột mã hàng NCC trên dòng PO – **mất phiếu KHN đã lập và ngành hàng NCC đã chọn**).

## [0.19.0] - 2026-11-07

### Thêm mới
- **Đợt QĐ Ban cung ứng (design.md 18.25), lát G1–G4c:**
  - **NCC đủ điều kiện theo mặt hàng** (màn hình SUPCOV): đếm NCC Đã duyệt theo NCC – sản phẩm, mặc định tối thiểu 3, miễn khi có NCC
    độc quyền còn tác dụng.
  - **Phiếu duyệt NCC và giá tháng** (màn hình GTH; quyền `Pricing.MonthlyApproval.View/Manage/Approve`): mỗi mặt hàng × tháng × công ty
    áp dụng một phiếu – NCC chính/dự phòng (phải Đã duyệt), giá, hạn mức số lượng tháng (gộp các công ty) để đạt bậc chiết khấu; duyệt
    theo quy trình loại GTH, có đính kèm, tra cứu chứng từ, Việc cần làm.
  - **PO kiểm tra theo phiếu tháng**: nguồn giá "Duyệt tháng" đứng đầu; NCC ngoài danh sách tháng hoặc dòng không có giá đã duyệt → thêm
    chặng duyệt **POL** "Mua ngoài danh sách/giá đã duyệt"; vượt hạn mức số lượng tháng thì chặn gửi duyệt.
  - **Phiếu khảo sát giá thị trường** (màn hình KSG; quyền `Pricing.PriceBank.View/Manage`) – ngân hàng giá.
  - **Giá chuẩn / giá trần / giá mục tiêu** trên dòng phiếu GTH, cột Giá thị trường (KSG đã chốt gần nhất). PO vượt giá trần (giá ròng sau
    chiết khấu, trước VAT) **chỉ cảnh báo vàng, không chặn**.
  - **Báo cáo tiết kiệm so giá chuẩn** (màn hình SAVSTD): tiết kiệm = giá trị chuẩn − thành tiền, khoảng cách mục tiêu, số dòng vượt trần,
    tỷ lệ giá trị mua có giá chuẩn (PO VND đã duyệt).
- **Đóng phần còn lại** trên PO nhận thiếu và PR đặt một phần (bắt buộc lý do): PO thôi chờ hàng, PR thôi chờ đặt; ngân sách chỉ tính phần
  đã nhận/đã đặt. Trạng thái hiện "Đã đóng phần còn lại".
- **Gia hạn nhận báo giá RFQ** (nút "Gia hạn nhận báo giá", bắt buộc lý do, hiện trên phiếu).
- **Tham số "Nhập mua bắt buộc theo đơn mua hàng"** (`RECEIPT_PO_REQUIRED`, màn hình SEC, mặc định Tắt): bật thì phiếu nhập mua phải theo PO.

### Thay đổi
- **Mỗi cấp duyệt một người khác nhau trong cùng một lượt** (cả chặng ngoại lệ POX/POL/POB): người đã duyệt một cấp không duyệt tiếp cấp sau.
- **RFQ quá hạn nộp không nhập/sửa báo giá** được nữa (phải gia hạn). Chọn thầu khi số NCC chào giá ít hơn tối thiểu **phải giải trình**
  (trừ khi đã giải trình mời thiếu NCC và mọi NCC mời đều chào giá); đếm theo NCC thực chào giá thay vì NCC được mời.
- **Hủy PR/PO đã duyệt bắt buộc lý do** (hiện trên phiếu).
- **Trả NCC trừ vào số đã nhận của PO**: PO đã nhận đủ mà trả lại thì mở lại để NCC giao bù.

### Sửa lỗi
- Bấm Ghi sổ / Bỏ ghi sổ / Hủy / Xác nhận nhập nội bộ (ICR) **đồng thời** có thể ghi sổ hai lần (tồn kho gấp đôi); hai phiếu nhập cùng PO
  hoặc hai PO cùng dòng PR gửi cùng lúc có thể vượt số lượng – nay khóa dòng, chỉ một lệnh được thực hiện.
- Hai người cùng sửa một phiếu/danh mục: người lưu sau ghi đè mất thay đổi của người trước – nay báo "Dữ liệu vừa được người khác thay đổi
  – hãy tải lại trang" (409). Hai người duyệt cùng lúc chỉ một người được ghi.
- Dọn toàn bộ cảnh báo build (chú thích XML, nullable).

### Thay đổi CSDL
- 8 migration mới (54 → 62): `V0_19_0_PhieuDuyetNccGiaThang`, `V0_19_0_PoTheoPhieuThang`, `V0_19_0_KhaoSatGiaThiTruong`,
  `V0_19_0_GiaChuanTranMucTieu`, `V0_19_0_KhoaSuaTrung` (không sinh SQL – dùng cột hệ thống `xmin`), `V0_19_0_NhapMuaTheoPo`,
  `V0_19_0_DongPhanConLai`, `V0_19_0_RfqHanNopLyDoHuy`. Chỉ thêm bảng/cột, không xóa dữ liệu. Script: `db/scripts/0.19.0.sql` (đã chạy thử
  3 lần trên bản sao CSDL test 0.18.0, không lỗi, dữ liệu giữ nguyên).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước** (`backup.ps1` / `db/backup.sh`), rồi chạy `db/scripts/0.19.0.sql`, sau đó thay gói ứng dụng.
  Từ bản cũ hơn 0.18.0: chạy lần lượt các script trước đó.
- **Cấu hình chặng POL ở APRL trước khi dùng**: từ bản này mọi PO có dòng không có giá đã duyệt (không theo phiếu tháng) đều thêm một
  lượt duyệt POL.
- **Công ty chưa gán vai trò riêng cho chặng POX/POL/POB cần ít nhất 2 người có quyền duyệt PO** (mỗi cấp một người khác nhau).
- Gán quyền mới cho vai trò đang dùng: `Pricing.MonthlyApproval.View/Manage/Approve`, `Pricing.PriceBank.View/Manage` (vai trò mẫu
  DAMPHAN, TRUONGBANCUVT, HOIDONG đã có sẵn khi tạo mới).
- RFQ đang nhận báo giá mà đã quá hạn nộp: phải gia hạn mới nhập tiếp được báo giá.

### Cách quay lại bản trước
- Khôi phục bản sao lưu trước nâng cấp và triển khai lại tag `v0.18.0`; hoặc gỡ migration về `20261105063638_V0_18_0_BatBuocHoSoTrinh`
  (`Down()` xóa các bảng phiếu duyệt tháng, khảo sát giá và các cột mới – **mất phiếu GTH/KSG đã lập**, giá chuẩn/trần, lý do đóng/hủy/gia hạn).

## [0.18.0] - 2026-11-06

### Thêm mới
- **Ngân sách bộ phận theo tháng** (màn hình BUDG, Mua hàng → Ngân sách; quyền `Purchase.Budget.View/Manage`): khai ngân sách bộ phận ×
  tháng; đã dùng = PO + phần PR chưa lên PO. PR/PO vượt ngân sách thêm chặng duyệt "Vượt ngân sách" (cấu hình loại PRB/POB ở APRL), đầu
  phiếu hiện ngân sách/đã dùng/còn lại. **Nghiệp vụ chưa chốt** (design.md 18.24) – công ty không khai ngân sách thì không ảnh hưởng gì;
  có thể đổi khi chốt điều kiện chặn.
- **Tham số "Bắt buộc đủ hồ sơ trình khi chọn thầu"** (`RFQ_DOSSIER_REQUIRED`, màn hình SEC, mặc định Tắt): bật thì chọn thầu RFQ bị
  chặn khi hồ sơ trình còn thiếu mục, báo rõ mục thiếu.
- **In PR (PDF) có hàng mới chưa có mã**: in nối sau dòng có mã, cột mã "Chưa có mã", tên gồm mô tả + quy cách + ghi chú.

### Thay đổi
- Không có.

### Sửa lỗi
- Không có.

### Thay đổi CSDL
- 2 migration mới (52 → 54): `V0_18_0_NganSachBoPhan` (bảng `DepartmentBudget`, cột ngân sách trên PR/PO), `V0_18_0_BatBuocHoSoTrinh`
  (tham số công ty). Chỉ thêm bảng/cột, không xóa dữ liệu. Script: `db/scripts/0.18.0.sql` (đã chạy thử trên bản sao CSDL 0.17.0, chạy lại
  lần hai không lỗi).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước** (`backup.ps1` / `db/backup.sh`), rồi chạy `db/scripts/0.18.0.sql`, sau đó thay gói ứng dụng.
  Từ bản cũ hơn 0.17.0: chạy lần lượt các script trước đó.
- Muốn dùng ngân sách: gán quyền `Purchase.Budget.*` cho vai trò và cấu hình cấp duyệt loại PRB/POB ở APRL.

### Cách quay lại bản trước
- Khôi phục bản sao lưu trước nâng cấp và triển khai lại tag `v0.17.0`; hoặc gỡ migration về `20261103141843_V0_17_0_ThoiDiemGuiDuyet`
  (`Down()` xóa bảng `DepartmentBudget` và các cột mới – **mất ngân sách đã khai** và tham số hồ sơ trình).

## [0.17.0] - 2026-11-04

### Thêm mới
- **Nhập Excel bảng giá NCC** (màn hình bảng giá NCC → Nhập Excel): tải file mẫu, kiểm tra trước rồi mới ghi; nhiều bảng giá trong một
  file (gom theo Mã NCC + Tên bảng giá), báo lỗi theo từng dòng Excel; tối đa 5.000 dòng, 10 MB.
- **Cảnh báo giá cao trên PO**: cột "Giá thấp nhất TĐ" trên dòng PO, tam giác vàng khi đơn giá vượt giá thấp nhất tập đoàn quá ngưỡng
  (tham số `PO_HIGH_PRICE_WARN_PERCENT`, mặc định 10%, sửa ở màn hình SEC). Người không có quyền xem giá mọi công ty chỉ thấy con số,
  không thấy công ty/NCC/chứng từ nguồn.
- **Sinh hợp đồng nguyên tắc từ kết quả thầu** (nút "Tạo hợp đồng" trên RFQ đã duyệt chọn thầu): mỗi NCC trúng thầu một hợp đồng gắn
  bảng giá tạo từ RFQ, sản lượng = SL trúng thầu, điều khoản thanh toán theo báo giá. RFQ lập ở Tổng công ty: bảng giá mặc định
  "Toàn tập đoàn" (bỏ đánh dấu được khi còn nháp).
- **Hiện người duyệt trên mọi phiếu có duyệt** (PR, PO, bảng giá, RFQ chọn thầu, hợp đồng, phụ lục, NCC độc quyền, duyệt NCC):
  - Trạng thái ghi rõ "Chờ duyệt: <tên người>".
  - **Popup trước khi gửi duyệt** liệt kê từng cấp và người duyệt được; lưu hồ sơ NCC có thay đổi cần duyệt cũng hiện popup
    "Lưu NCC và gửi duyệt thay đổi?".
  - Popup khi duyệt cho biết người lập, thời điểm gửi và ai duyệt tiếp sau mình.
  - Tab Lịch sử duyệt dạng **cây**: Lập phiếu → từng lượt gửi duyệt (người gửi, ngày giờ) → từng cấp (người duyệt, ngày giờ, ý kiến;
    "Chờ duyệt"/"Sẽ duyệt" cho cấp chưa tới).

### Thay đổi
- Bảng giá toàn tập đoàn: công ty khác cũng xem được lịch sử duyệt (design.md mục 8.5).

### Sửa lỗi
- Lưu NCC đổi **cả** trạng thái lẫn tài khoản ngân hàng trong một lần bị lỗi trùng số yêu cầu duyệt (409).
- Đơn giá sau chiết khấu trên dòng PO (giao diện) tính sai tỉ lệ.

### Thay đổi CSDL
- 3 migration mới (49 → 52): `V0_17_0_CanhBaoGiaCaoPo` (tham số công ty ngưỡng cảnh báo giá cao), `V0_17_0_HopDongTuKetQuaThau`
  (liên kết NCC trúng thầu → hợp đồng), `V0_17_0_ThoiDiemGuiDuyet` (thời điểm, người gửi duyệt trên bước duyệt). Chỉ thêm cột, không
  xóa dữ liệu. Script: `db/scripts/0.17.0.sql` (đã chạy thử trên bản sao CSDL 0.16.0, chạy lại lần hai không lỗi).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước** (`backup.ps1` / `db/backup.sh`), rồi chạy `db/scripts/0.17.0.sql`, sau đó thay gói ứng dụng.
  Từ bản cũ hơn 0.15.0: chạy lần lượt các script đến `0.15.0.sql` trước.
- Bước duyệt của các phiếu gửi duyệt trước khi nâng cấp không có thời điểm gửi – cây lịch sử ghi "gửi duyệt" không kèm ngày giờ.

### Cách quay lại bản trước
- Khôi phục bản sao lưu trước nâng cấp và triển khai lại tag `v0.16.0`; hoặc gỡ migration về `20261103002518_V0_15_0_SoLieuKiemKe`
  (`Down()` xóa 3 cột mới – mất ngưỡng cảnh báo đã sửa, liên kết hợp đồng từ RFQ và thời điểm gửi duyệt; hợp đồng đã tạo vẫn giữ).

## [0.16.0] - 2026-11-03

### Thêm mới
- **Ghi chú + đính kèm cho mọi form chứng từ/phiếu** (quy tắc chung, design.md 18.4):
  - **Đính kèm ngay khi tạo phiếu**: chọn file trước khi lưu (hàng chờ trên trình duyệt), lưu xong tự tải lên.
  - Thêm tab **Đính kèm** cho: phiếu tìm nguồn, phụ lục hợp đồng, đánh giá NCC định kỳ, sổ vi phạm KSNB, kê khai xung đột lợi ích
    (người kê khai tự đính kèm); NCC độc quyền đính kèm được ngay khi lập.
- Tài liệu hướng dẫn cấu hình duyệt PO nhiều cấp (`docs/HuongDan_PheDuyetPO_NhieuCap_v2.docx`, `docs/HuongDan_DuyetPO_A_B_C_D.docx`).

### Thay đổi
- Giới hạn đính kèm **2 MB/file** mặc định (trước 20 MB), cấu hình được `Attachments:MaxFileSizeMb` (1–25; Docker biến
  `ATTACHMENT_MAX_MB`); giao diện đọc giới hạn từ máy chủ (`GET /api/attachments/limits`) và báo ngay khi chọn file quá cỡ.

### Sửa lỗi
- Không có.

### Thay đổi CSDL
- Không có (vẫn 49 migration như 0.15.0). Không có script `db/scripts/0.16.0.sql`.

### Lưu ý khi nâng cấp
- Từ 0.15.0: chỉ thay gói ứng dụng, không chạy script CSDL (vẫn nên sao lưu theo thói quen). Từ bản cũ hơn: chạy các script đến
  `db/scripts/0.15.0.sql` trước.
- Giới hạn đính kèm mặc định giảm còn **2 MB/file**; file đã tải lên trước đó giữ nguyên. Cần giới hạn khác thì thêm
  `"MaxFileSizeMb": <số>` vào mục `Attachments` của `appsettings.Production.json` (Windows) hoặc đặt `ATTACHMENT_MAX_MB` (Docker).

### Cách quay lại bản trước
- Triển khai lại tag `v0.15.0` (CSDL không đổi nên không cần gỡ migration). File đính kèm của 5 loại phiếu mới vẫn nằm trong thư mục
  đính kèm và CSDL nhưng bản 0.15.0 không hiển thị.

## [0.15.0] - 2026-11-03

### Thêm mới
- **Phiếu điều chỉnh / kiểm kê kho (ADJ)** – màn hình Kho → Điều chỉnh, kiểm kê (`/kho/dieu-chinh`, mã lệnh `ADJ`):
  - Một kho bất kỳ của công ty; bắt buộc lý do và số biên bản kiểm kê; bản scan biên bản ở tab Đính kèm.
  - Mỗi dòng là **Tăng** (hàng thừa, nhập giá tay – tham gia giá vốn kỳ như nhập có giá) hoặc **Giảm** (hàng thiếu, xuất theo giá vốn,
    chốt khi đóng kỳ); cột tồn hiện tại, cảnh báo giảm vượt tồn; ghi sổ chặn tồn âm.
  - **Bảng kiểm kê**: lấy tồn sổ của kho, nhập SL thực đếm, tự lập dòng chênh lệch; server lấy tồn sổ lúc lưu và tự tính chiều, SL.
  - **In "Biên bản kiểm kê, điều chỉnh kho"** (PDF): tồn sổ, thực đếm, tăng, giảm, giá trị tăng, 4 chỗ ký.
  - **Quyền riêng**: `Inventory.Adjustment.Create` (lập/sửa/hủy, đính kèm) và `Inventory.Adjustment.Post` (ghi sổ/bỏ ghi sổ) – vai trò
    mẫu Kho có quyền lập, Kế toán có quyền ghi sổ.
  - Thẻ kho ghi "Điều chỉnh tăng/giảm"; xuất dữ liệu kế toán có loại ADJ; Tra cứu chứng từ mở được phiếu ADJ.
- **Tra cứu chứng từ** (màn hình DOCS, Hằng ngày): 17 loại chứng từ theo quyền xem, lọc Chưa hoàn thành/Hoàn thành/Đã hủy, Tôi lập/Tôi
  đã duyệt, cấp đang chờ duyệt, cột **Người duyệt tiếp theo**; bấm đúp mở trang chứng từ.
- **Người duyệt tiếp theo** trên tab Lịch sử duyệt ("Chờ: …", tối đa 5 tên + "và N người khác"): theo quyền + vai trò của cấp đang chờ,
  bỏ người lập và người vướng kê khai xung đột lợi ích.
- **Ô chọn danh mục tìm kiểu SAP ECC**: gõ `*` rồi Enter liệt kê toàn bộ danh mục (tối đa 500 dòng); `*` là ký tự đại diện ở mọi ô tìm
  (ví dụ `b*long` khớp "Bu lông").

### Thay đổi
- Thông báo, Việc cần làm, Tra cứu chứng từ và liên kết KSNB mở **trang đầy đủ** thay ngăn kéo cho: duyệt NCC (thêm tab Hồ sơ NCC, Hồ sơ
  đính kèm chỉ xem), NCC độc quyền, sổ vi phạm, phụ lục hợp đồng, đánh giá NCC định kỳ.

### Sửa lỗi
- Bỏ ghi sổ phiếu có kho xuất = kho nhập không còn báo nhầm "kho ngoài phạm vi".

### Thay đổi CSDL
- 2 migration mới (tổng 49): `V0_15_0_PhieuDieuChinhKho` (lý do, số biên bản trên phiếu kho; chiều tăng/giảm trên dòng),
  `V0_15_0_SoLieuKiemKe` (tồn sổ, thực đếm trên dòng). Chỉ thêm cột, không xóa dữ liệu. Script: `db/scripts/0.15.0.sql` (chạy được nhiều lần).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước khi nâng cấp**, rồi chạy `db/scripts/0.15.0.sql` (từ 0.13.x/0.14.0), sau đó thay gói ứng dụng.
- Vai trò đã tạo từ trước **không tự có** 2 quyền mới: gán `Inventory.Adjustment.Create` cho vai trò kho và `Inventory.Adjustment.Post`
  cho vai trò kế toán ở màn hình Vai trò (vai trò ADMIN luôn đủ quyền).

### Cách quay lại bản trước
- Triển khai lại tag `v0.14.0`. CSDL 0.15.0 vẫn chạy được với ứng dụng 0.14.0 (chỉ thêm cột, cột mới cho phép trống) nên không bắt buộc
  gỡ migration; nếu muốn gỡ: sao lưu rồi `dotnet ef database update V0_13_0_ChietKhauTaiTroHopDong` – **mất dữ liệu phiếu ADJ** (phiếu
  ADJ đã ghi sổ phải bỏ ghi sổ/xử lý trước).

## [0.14.0] - 2026-11-02

### Thêm mới
- **Công cụ tạo dữ liệu thử** cho môi trường test (`tools/du-lieu-thu`, có trong gói cài ở `tools\du-lieu-thu`): script Node.js 22 chạy
  được trên Windows và Linux, tạo dữ liệu qua API (đi qua mọi quy tắc nghiệp vụ, NCC được duyệt bằng tài khoản khác người lập; chạy lại
  không tạo trùng) – bộ phận, vai trò (bộ vai trò mẫu Ban Cung ứng + vai trò toàn quyền), 38 tài khoản test, xếp loại NCC, quy trình duyệt
  PO 2 cấp, 22 nhà cung cấp + 3 NCC nội bộ, 30 vật tư, NCC – sản phẩm. Dữ liệu ở `du-lieu.json`; hướng dẫn và mục `Seed` mẫu ở `README.md`.
- **Tài liệu dữ liệu thử, tài khoản và 38 kịch bản test**: `docs/DuLieuThu_KichBanTest_0.14.0.docx`.
- Hướng dẫn mở ứng dụng cho máy khác trong mạng nội bộ (nghe `--urls http://0.0.0.0:<cổng>`, mở cổng tường lửa) – không viết cứng địa
  chỉ IP trong code.

### Thay đổi
- Gói cài Windows có thêm thư mục `tools\du-lieu-thu`.

### Sửa lỗi
- Không có.

### Thay đổi CSDL
- Không có (giữ nguyên 47 migration của 0.13.x, không có script `db/scripts/0.14.0.sql`).

### Lưu ý khi nâng cấp
- Chỉ thay gói ứng dụng; không cần chạy script CSDL. **Không chạy công cụ tạo dữ liệu thử trên CSDL thật.**

### Cách quay lại bản trước
- Triển khai lại tag `v0.13.1`. Không cần đụng CSDL.

## [0.13.1] - 2026-11-02

### Thêm mới
- Không có.

### Thay đổi
- Không có.

### Sửa lỗi
- **Form chứng từ ghi đè chữ đang gõ**: sau khi lưu, lượt tải lại phiếu không còn nạp đè nội dung người dùng vừa gõ (PO, PR, phiếu nhập
  mua, trả hàng, ICS, ICR, TRF, bảng giá NCC, hợp đồng, RFQ, phiếu tìm nguồn).
- **Tạo trùng phiếu**: bấm lệnh (ví dụ Gửi duyệt) ngay sau khi tạo phiếu mới lúc máy bận không còn tạo thêm một phiếu.
- **Trang trắng sau đăng xuất**: bấm Quay lại ngay sau khi đăng xuất nay về màn hình đăng nhập.
- Nút **"Lấy các dòng còn lại của PO"** (phiếu nhập mua) và **"Lấy tất cả"** (trả hàng) bị bỏ qua cú bấm khi đang tải lại – nay nút khóa
  trong lúc tải, bấm sau khi tải xong là lấy được dòng.

### Thay đổi CSDL
- Không có (giữ nguyên 47 migration của 0.13.0, không có script `db/scripts/0.13.1.sql`).

### Lưu ý khi nâng cấp
- Chỉ thay gói ứng dụng (giao diện); không cần chạy script CSDL. Vẫn nên sao lưu CSDL theo thói quen trước mỗi lần nâng cấp.
- Trình duyệt: tải lại trang bằng Ctrl+F5. Không đổi cấu hình triển khai (Docker, IIS).

### Cách quay lại bản trước
- Triển khai lại tag `v0.13.0` (Docker: `APP_VERSION=0.13.0`; Windows: chép lại gói 0.13.0). Không cần đụng CSDL.

## [0.13.0] - 2026-11-02

### Thêm mới
- **Bộ phận đặt hàng trên PO** (design.md mục 18.10): tự lấy theo PR nguồn; hợp đồng giới hạn bộ phận được đặt thì PO của bộ phận khác
  không tính giá hợp đồng (ghi chú, không chặn); dòng PO ghi phiếu **NCC độc quyền** còn tác dụng vào ngày PO.
- **Mua khẩn cấp** trên PO (mục 18.11): loại mua Thường/Khẩn cấp, lý do, hạn bổ sung hồ sơ (≤ 30 ngày); duyệt theo loại **POK** nếu đã
  cấu hình; không gửi duyệt PO khẩn lập lùi ngày; lệnh "Đã bổ sung hồ sơ", cờ quá hạn.
- **Hàng mới chưa có mã trên PR** (mục 18.12): tab "Hàng mới chưa có mã", duyệt cùng PR; PR đã duyệt có nút Tìm nguồn và Gắn mã.
- **SLA mua hàng** (màn hình SLA, mục 18.13) theo tham số ngày làm việc; cột **Đang đặt** trên dòng PR.
- **Kiểm tra chất lượng, sai lệch nhận hàng** (mục 18.14): SL chứng từ giao, SL không đạt, kết quả kiểm tra trên phiếu nhập mua; tham số
  bắt buộc kiểm tra và "người lập PO không ghi sổ nhận hàng của PO đó"; báo cáo **Sai lệch nhận hàng** (QCDEV).
- **In biên bản giao nhận và kiểm tra hàng hóa** (PDF) từ phiếu nhập mua.
- **Đối chiếu PO – nhận hàng – hóa đơn** (MATCH3, mục 18.15): ghi tiền hóa đơn NCC trên phiếu nhập, cập nhật hóa đơn sau ghi sổ.
- **Phím F1 xem lịch sử ô** (mục 9.3a): ai tạo/sửa/xóa, giá trị cũ → mới trên mọi chứng từ, ngăn kéo danh mục và lưới danh sách; nút
  "Lịch sử thay đổi" cả phiếu (kể cả dòng đã xóa).
- **Điểm đặt hàng lại, tồn an toàn, vật tư trọng yếu** trên mặt hàng (mục 18.16) và báo cáo **Gợi ý đặt hàng** (REORD) – nút
  "Lập đề nghị mua" điền sẵn PR.
- Báo cáo **Giá mua & mua ngoài hợp đồng** theo tuần (PRCW) và **Biến động giá mua** (PRVAR) (mục 18.17).
- **Bảng KPI cung ứng** 9 chỉ số (màn hình KPI, mục 18.18) so với kỳ trước; mục tiêu KPI là tham số công ty.
- **Kiểm soát nội bộ** (mục 18.19): màn hình **Dấu hiệu bất thường** (ANOM – giá cao lặp lại, PO sát ngưỡng duyệt, đổi TK ngân hàng NCC,
  mua ngoài hợp đồng cao, hóa đơn tính cả hàng thiếu, chứng từ trước PO, xung đột lợi ích, chiết khấu/tài trợ chưa ghi nhận) và **Sổ vi
  phạm** (VIOL) với thông báo thẳng lãnh đạo. Vai trò mẫu mới **LANHDAO**.
- **Đánh giá NCC định kỳ theo quý** (màn hình SUPREV, mục 18.20): 6 tiêu chí 1–5, kết luận, số liệu hệ thống chốt khi hoàn thành.
- **Chiết khấu doanh số/tài trợ trên hợp đồng** (tab Chiết khấu – tài trợ của CTR, mục 18.21): cam kết theo % doanh số hoặc số tiền cố
  định, kỳ Quý/Năm/Cả hợp đồng; theo dõi phải nhận – đã nhận – còn thiếu theo kỳ; ghi nhận và hủy thực nhận.
- Quyền mới: `Control.Anomaly.View`, `Control.Finding.Manage`, `Control.Finding.Receive`, `MasterData.SupplierReview.View`,
  `MasterData.SupplierReview.Manage`. Loại duyệt mới ở APRL: **POK** (PO khẩn). Loại số chứng từ mới: VP, DGN.

### Thay đổi
- Bảng KPI: chỉ số "Hoàn thành đánh giá NCC" và "Vi phạm nghiêm trọng" nay đo được (trước là "Chưa đo").
- Excel hàng hóa thêm 3 cột (tồn an toàn, điểm đặt hàng lại, trọng yếu); file mẫu cũ thiếu cột thì giữ giá trị đang có.

### Sửa lỗi
- Không có lỗi người dùng báo trong bản này. Còn biết: form chứng từ có thể ghi đè chữ vừa gõ nếu gõ trước khi phiếu tải lại xong sau khi
  lưu; bấm Quay lại ngay sau khi đăng xuất có thể để trang trắng (tải lại trang là hết).

### Thay đổi CSDL
- 11 migration, chỉ **thêm** bảng/cột, không xóa dữ liệu cũ: `V0_13_0_BoPhanPoDocQuyen`, `V0_13_0_MuaKhanCap`, `V0_13_0_HangMoiChuaCoMa`,
  `V0_13_0_ThamSoSla`, `V0_13_0_KiemTraNhanHang`, `V0_13_0_TienHoaDonPhieuNhap`, `V0_13_0_DiemDatHangLai`, `V0_13_0_MucTieuKpi`,
  `V0_13_0_GhiNhanViPham`, `V0_13_0_DanhGiaNccDinhKy`, `V0_13_0_ChietKhauTaiTroHopDong`.
- `Down()` của từng migration **mất** dữ liệu tạo ở bản này (bộ phận/loại mua/hồ sơ khẩn trên PO, hàng mới trên PR, kết quả kiểm tra nhận
  hàng, tiền hóa đơn, điểm đặt hàng lại, mục tiêu KPI, vi phạm KSNB, phiếu đánh giá NCC, cam kết chiết khấu và thực nhận); AuditLog vẫn giữ.
- Script nâng cấp: `db/scripts/0.13.0.sql` (từ 0.12.0) – đã chạy thử chuỗi 0.1.0 → 0.13.0 trên CSDL trống (47 migration).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước**, rồi chạy `db/scripts/0.13.0.sql`.
- Gán quyền mới cho vai trò cần dùng (vai trò Quản trị tự có; vai trò đã tạo trước bản này không tự thêm quyền – gán tay cho KSNB, Lãnh
  đạo, Trưởng ban, Đàm phán, Vận hành). Khai báo quy tắc duyệt **POK** ở APRL nếu cần luồng duyệt khẩn riêng.
- Tham số mới (SLA, kiểm tra nhận hàng, mục tiêu KPI) có giá trị mặc định; xem lại ở Tham số hệ thống.
- Trình duyệt: tải lại trang bằng Ctrl+F5. Không đổi cấu hình triển khai (Docker, IIS).

### Cách quay lại bản trước
- Code: triển khai lại tag `v0.12.0` (Docker: `APP_VERSION=0.12.0`; Windows: chép lại gói 0.12.0).
- CSDL: chỉ khi đã sao lưu – khôi phục bản sao lưu trước nâng cấp, hoặc
  `dotnet ef database update 20261102011909_V0_12_0_NguonGiaPo` (mất dữ liệu như mục "Thay đổi CSDL").

## [0.12.0] - 2026-11-02

### Thêm mới
- Duyệt kết quả chọn thầu (design.md mục 8.6): RFQ mời ít hơn số NCC tối thiểu vẫn gửi được khi ghi giải trình; chọn thầu xong RFQ chuyển
  **Chờ duyệt kết quả** theo quy trình loại RFQ (xét giá trị trúng thầu); người lập RFQ và người chọn thầu không tự duyệt; từ chối phải
  ghi lý do. Chỉ tạo PO sau khi kết quả được duyệt.
- Hồ sơ trình thầu: tab **Hồ sơ trình (x/7)** trên RFQ (bảng so sánh + 6 mục đánh dấu, ghi chú), hội đồng thấy mục còn thiếu; nút
  **Tạo bảng giá** sinh bảng giá NCC nháp từ kết quả trúng thầu.
- **Hợp đồng nguyên tắc** (màn hình CTR, Mua hàng → Hợp đồng nguyên tắc – design.md mục 18.7): mỗi công ty một hợp đồng với NCC, dùng
  chung bảng giá, điều khoản (điều chỉnh giá, thanh toán, giao hàng, chiết khấu, tài trợ, chế tài), hạn mức công nợ, sản lượng dự kiến,
  bộ phận được phép đặt; Nháp → Chờ duyệt → Hiệu lực → Chấm dứt; cảnh báo sắp hết hiệu lực; đính kèm.
  - **Phụ lục hợp đồng**: gia hạn, đổi bảng giá, đổi điều khoản; loại duyệt riêng **CTA**; duyệt xong tự áp vào hợp đồng.
  - **Nhập Excel hợp đồng hiện hữu** (mẫu tải trên màn hình, kiểm tra thử rồi nhập thật, vào trạng thái Nháp).
- **Kê khai xung đột lợi ích** (màn hình COI, Giá và NCC → Xung đột lợi ích – design.md mục 18.8): ai cũng tự kê khai quan hệ với NCC;
  người có kê khai còn hiệu lực không duyệt được PO, bảng giá, NCC, hợp đồng, phụ lục, kết quả chọn thầu liên quan NCC đó, không nhập báo
  giá/chọn thầu RFQ có NCC đó.
- **Phê duyệt NCC độc quyền** (màn hình NDQ, Giá và NCC → NCC độc quyền – design.md mục 18.9): giải trình NCC × mặt hàng, duyệt theo
  loại NDQ, hạn đánh giá lại tối đa 12 tháng, kết thúc sớm, đánh giá lại; RFQ mời thiếu NCC mà mọi dòng có độc quyền đã duyệt thì tự ghi
  giải trình.
- **PO theo giá đã duyệt** (design.md mục 18.10): mỗi dòng PO có **nguồn giá** – kết quả chọn thầu → hợp đồng nguyên tắc → bảng giá →
  ngoài hợp đồng; cột Giá đã duyệt, Nguồn giá, Lý do lệch giá; nút "Áp giá đã duyệt". Dòng **lệch giá đã duyệt** (tam giác vàng) phải
  ghi lý do khi gửi duyệt và PO qua thêm **chặng duyệt ngoại lệ giá** (loại duyệt mới **POX** ở APRL). Danh sách PO có cột Nguồn giá
  ("Ngoài hợp đồng" / "Lệch giá đã duyệt").
- Quyền mới: `MasterData.Conflict.View`, `MasterData.Conflict.Manage`, `MasterData.SoleSource.View`, `MasterData.SoleSource.Manage`,
  `MasterData.SoleSource.Approve`. Loại duyệt mới ở APRL: CTA, NDQ, POX.

### Thay đổi
- PO: đơn giá không còn "nhập tay tự do" – sửa khác giá hợp đồng/bảng giá/kết quả chọn thầu thì phải có lý do và duyệt ngoại lệ.
  Dòng chưa có giá đã duyệt chỉ gắn cờ "Ngoài hợp đồng", không chặn.
- Lịch sử duyệt PO có cột Chặng ("Ngoại lệ giá") khi PO qua chặng ngoại lệ.

### Sửa lỗi
- Không có lỗi người dùng báo trong bản này. Còn biết: bấm Quay lại ngay sau khi đăng xuất có thể để trang trắng (tải lại trang là hết).

### Thay đổi CSDL
- 7 migration, chỉ **thêm** bảng/cột, không xóa dữ liệu cũ: `V0_12_0_DuyetKetQuaChonThau`, `V0_12_0_HoSoTrinhThau`,
  `V0_12_0_HopDongNguyenTac`, `V0_12_0_PhuLucHopDong`, `V0_12_0_KeKhaiXungDotLoiIch`, `V0_12_0_NccDocQuyen`, `V0_12_0_NguonGiaPo`.
- `Down()` của từng migration **mất** dữ liệu tạo ở bản này: kết quả chọn thầu chờ duyệt/hồ sơ trình, hợp đồng nguyên tắc và phụ lục,
  kê khai xung đột lợi ích, phiếu NCC độc quyền, nguồn giá và lý do lệch giá trên PO (AuditLog vẫn giữ).
- PO lập trước bản này có nguồn giá "không xác định", không gắn cờ.
- Script nâng cấp: `db/scripts/0.12.0.sql` (từ 0.11.0).

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước**, rồi chạy `db/scripts/0.12.0.sql`.
- Gán quyền mới cho vai trò cần dùng (vai trò Quản trị tự có). Khai báo ở APRL quy tắc duyệt cho CTR, CTA, NDQ và **POX** – nên dùng vai
  trò khác cấp duyệt PO thường để tách người duyệt ngoại lệ giá.
- RFQ đang "Đã chọn thầu" từ bản trước giữ nguyên (không phải duyệt lại).
- Trình duyệt: tải lại trang bằng Ctrl+F5. Không đổi cấu hình triển khai (Docker, IIS).

### Cách quay lại bản trước
- Code: triển khai lại tag `v0.11.0` (Docker: `APP_VERSION=0.11.0`; Windows: chép lại gói 0.11.0).
- CSDL: chỉ khi đã sao lưu – khôi phục bản sao lưu trước nâng cấp, hoặc
  `dotnet ef database update 20261101101508_V0_11_0_DuyetBangGiaNcc` (mất dữ liệu như mục "Thay đổi CSDL").

## [0.11.0] - 2026-11-01

### Thêm mới
- Danh mục bộ phận theo công ty (design.md mục 18.5, màn hình Hệ thống → Bộ phận – DEPT): thêm/sửa/ngừng dùng bộ phận của từng công ty.
  Phiếu đề nghị mua hàng (PR) **bắt buộc** chọn bộ phận trong danh mục; PR cũ vẫn hiện tên bộ phận đã gõ trước đây.
  Quyền mới `Organization.Department.View`, `Organization.Department.Manage`.
- Quy trình duyệt dùng chung (design.md mục 18.6): màn hình Quy tắc duyệt (APRL) khai báo được cho 6 loại – PR, PO, bảng giá NCC (PRL),
  RFQ, NCC, hợp đồng khung (CTR); số cấp do người dùng cấu hình, ai duyệt do phân quyền. Quyền duyệt riêng từng loại:
  `MasterData.Supplier.Approve`, `Pricing.PriceList.Approve`, `Sourcing.Rfq.ApproveAward`, `Purchase.Contract.Approve`.
  Bộ 9 vai trò mẫu của Ban Cung ứng vật tư tạo nhanh ở màn hình Vai trò.
- Duyệt nhà cung cấp hai chặng (design.md mục 18.6, màn hình Giá và NCC → Duyệt NCC – SUPAPR): đổi trạng thái Đã duyệt / Danh sách đen
  và đổi tài khoản ngân hàng của NCC thành yêu cầu duyệt, duyệt xong mới áp vào NCC. Chặng 1: các công ty đang mua của NCC cùng duyệt
  (2 công ty thì cả 2); chặng 2: Tổng công ty duyệt lần cuối. Một công ty từ chối là kết thúc yêu cầu; người lập rút được yêu cầu đang chờ.
  Ngăn kéo NCC báo yêu cầu đang chờ; có trong Việc cần làm và thông báo.
- Duyệt bảng giá NCC (design.md mục 8.5): Nháp → Chờ duyệt → Hiệu lực tại công ty lập bảng giá; người lập không tự duyệt; từ chối phải
  ghi lý do và bảng giá về Nháp; người lập rút về Nháp được. Bỏ nút "Đưa vào hiệu lực". Bảng giá toàn tập đoàn chưa duyệt thì công ty
  khác chưa thấy. Cảnh báo bảng giá sắp hết hiệu lực (≤ 30 ngày) ở danh sách và trên phiếu.

### Thay đổi
- **Giao diện mới – phong cách ERP desktop cổ điển** cho toàn bộ ứng dụng (docs/ui-style.md, thay SAP Fiori):
  - Thanh trên hai hàng: menu chữ (Hằng ngày, Mua hàng, Kho…) và thanh công cụ (ô lệnh, công ty, thông báo, người dùng); cây điều hướng gọn.
  - Chữ Tahoma 12px, nền xanh xám, tiêu đề xanh đậm; viền 1px, góc vuông, không đổ bóng, không hiệu ứng chuyển động.
  - Lưới danh sách và lưới dòng chứng từ kẻ ô, dòng 22px, cột số canh phải phông đơn cách, dòng chọn vàng nhạt, đèn trạng thái ô vuông;
    **kéo mép tiêu đề cột để co giãn**, bấm đúp để trả về độ rộng gốc.
  - Tab ô vuông chữ in hoa; nút lệnh nền be viền nâu; icon tô màu theo mức quan trọng – đỏ (xóa, hủy, đóng, bỏ ghi sổ), xanh lá
    (lưu, thêm, duyệt, gửi duyệt), vàng (thao tác thường).
  - Mọi ô ngày gõ tay được dd/MM/yyyy, gõ nhanh `05122026` hoặc `5/12/2026`, hoặc chọn trên lịch.
  - Màn hình mẫu `/thu-giao-dien/tao-po` (Tạo đơn mua hàng, dữ liệu mẫu, không ghi CSDL) làm chuẩn hình thức cho các màn hình sau.
- Màn hình đăng nhập giữ kiểu cũ (thẻ trắng).

### Sửa lỗi
- Không có lỗi người dùng báo trong bản này. Đã biết: bấm Quay lại ngay lập tức (trong ~40ms) sau khi đăng xuất có thể để trang trắng –
  không lộ dữ liệu (phiên đã xóa); tải lại trang là về màn hình đăng nhập. Sẽ sửa ở bản sau.

### Thay đổi CSDL
- `V0_11_0_DanhMucBoPhan`: bảng `Department`, cột `PurchaseRequest.DepartmentId`; cột chữ cũ `PurchaseRequest.Department` giữ nguyên
  (ngừng dùng, xóa ở bản sau). `Down()` **mất** danh mục bộ phận và bộ phận đã chọn trên PR (chữ bộ phận cũ vẫn còn).
- `V0_11_0_DuyetNhaCungCap`: bảng `SupplierChangeRequest`, cột `ApprovalStep.Stage` (bước duyệt cũ của PR/PO là chặng 1).
  `Down()` **mất** các yêu cầu duyệt NCC và thông tin chặng; NCC giữ nguyên trạng thái và tài khoản đã áp.
- `V0_11_0_DuyetBangGiaNcc`: cột lượt gửi duyệt và lý do từ chối của bảng giá NCC; trạng thái mới Chờ duyệt. Bảng giá đã hiệu lực trước
  bản này giữ nguyên, không phải duyệt lại. `Down()` đưa bảng giá đang chờ duyệt về Nháp, **mất** lượt gửi và lý do từ chối.
- Script nâng cấp: `db/scripts/0.11.0.sql` (từ 0.10.0); cài mới thì chạy lần lượt mọi script trong `db/scripts`. Đã chạy thử toàn bộ
  chuỗi 0.1.0 → 0.11.0 trên CSDL trống (29 migration) và chạy lại 0.11.0 lần hai không lỗi.

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước**, rồi chạy `db/scripts/0.11.0.sql`.
- Sau nâng cấp: khai báo bộ phận cho từng công ty (màn hình DEPT) **trước khi** người dùng lập PR mới – PR bắt buộc chọn bộ phận.
- Gán quyền mới cho vai trò cần dùng: `Organization.Department.*`, `MasterData.Supplier.Approve`, `Pricing.PriceList.Approve`,
  `Sourcing.Rfq.ApproveAward`, `Purchase.Contract.Approve` (vai trò Quản trị tự có). Khai báo quy tắc duyệt NCC và bảng giá NCC ở APRL.
- Bảng giá NCC lập mới phải qua duyệt mới có hiệu lực; bảng giá đang hiệu lực không bị ảnh hưởng.
- Trình duyệt: tải lại trang bằng Ctrl+F5 để nhận giao diện mới. Không đổi cấu hình triển khai (Docker, IIS).

### Cách quay lại bản trước
- Code: triển khai lại tag `v0.10.0` (Docker: `APP_VERSION=0.10.0`; Windows: chép lại gói 0.10.0).
- CSDL: chỉ khi đã sao lưu – khôi phục bản sao lưu trước nâng cấp, hoặc
  `dotnet ef database update 20261101070150_V0_10_0_DinhKemFile` (mất dữ liệu như mục "Thay đổi CSDL").

## [0.10.0] - 2026-11-01

### Thêm mới
- Dữ liệu mua lịch sử (design.md mục 18.1, màn hình Báo cáo → Dữ liệu mua lịch sử – PHIS): nhập Excel dữ liệu mua 12–24 tháng của từng
  pháp nhân (tối đa 50.000 dòng, 20 MB/file; Kiểm tra thử rồi Nhập thật), tự khớp mã vật tư/MST với danh mục, lọc theo lô/ngày/trạng thái
  khớp, xóa lô (có nhật ký). Chỉ để phân tích: không ghi sổ kho, không ảnh hưởng tồn, giá vốn, bảng giá.
- Ánh xạ dữ liệu mua (design.md mục 18.2, màn hình PHMAP): mã vật tư cũ → mã hàng chung, NCC cũ (MST/tên) → NCC; gợi ý trùng gần đúng
  theo tên không dấu; lưu ánh xạ áp cho mọi dòng cùng mã và cho lần nhập sau.
- Phân tích giá hợp nhất (design.md mục 18.3, màn hình PHAN): so giá mua cùng mặt hàng giữa các pháp nhân – SL, giá trị, giá bình quân,
  thấp/cao nhất, giá gần nhất, chênh lệch %, NCC giá tốt nhất, giá mục tiêu (giảm X% từ giá bình quân thấp nhất), tiết kiệm dự kiến;
  ngăn kéo chi tiết từng pháp nhân.
- Quyền mới: `Analysis.PurchaseHistory.View` (xem dữ liệu mua lịch sử), `Analysis.PurchaseHistory.Import` (nhập, xóa lô, ánh xạ),
  `Analysis.PurchaseHistory.ViewAllCompanies` (phân tích toàn tập đoàn). Vai trò Quản trị tự có; vai trò khác gán tại Hệ thống → Vai trò.
- Đính kèm file (design.md mục 18.4): NCC (tab "Hồ sơ đính kèm"), bảng giá NCC, RFQ, PR, PO và mọi phiếu kho (tab "Đính kèm (n)").
  Tối đa 20 MB/file, chỉ nhận tài liệu, ảnh, file nén, thư; quyền theo quyền xem/sửa phiếu; đính kèm được cả khi phiếu đã duyệt/ghi sổ;
  xóa mềm (file vẫn giữ), có nhật ký.
- % markup nội bộ mặc định (tham số công ty, màn hình SEC): gợi ý giá bán nội bộ = giá vốn kỳ đã đóng gần nhất × (1 + %) ở Bảng giá nội bộ
  và phiếu xuất bán nội bộ.
- Cài trên Windows Server (IIS) thay cho Docker: hướng dẫn `docs/deploy-windows.md`; gói cài `procurement-0.10.0.zip` tạo bằng
  `deploy\windows\publish.ps1`; sao lưu bằng `deploy\windows\backup.ps1` + Task Scheduler. Log kỹ thuật ghi được ra file.

### Thay đổi
- Dấu * đỏ sau nhãn ở mọi ô bắt buộc (form phiếu, ngăn kéo, hộp thoại, tiêu đề cột lưới dòng); ẩn khi chỉ xem.
- Múi giờ Việt Nam tìm được trên cả Linux và Windows (kể cả Windows Server 2016).
- Docker: file đính kèm lưu trong volume `attachments` gắn vào `/data/attachments`; container backup chép bổ sung vào `backups/attachments/` mỗi đêm.

### Sửa lỗi
- Đọc số trong file Excel nhập: "2.5" được hiểu là 2,5.
- Test bảng giá NCC báo lỗi khi chạy vào ngày mùng 1.

### Thay đổi CSDL
- `V0_10_0_MarkupNoiBo`: cột % markup nội bộ trong tham số công ty. `Down()` xóa cột – mất % đã khai báo; bảng giá, chứng từ không đổi.
- `V0_10_0_DuLieuMuaLichSu`: bảng `PurchaseHistoryBatch`, `PurchaseHistoryRecord`. `Down()` **mất** toàn bộ dữ liệu mua lịch sử đã nhập
  (nhập lại được từ file Excel gốc).
- `V0_10_0_AnhXaDuLieuMua`: bảng ánh xạ mã/NCC cũ. `Down()` **mất** các ánh xạ đã lưu; dòng mua lịch sử giữ mặt hàng/NCC đã gán.
- `V0_10_0_DinhKemFile`: bảng `Attachment`. `Down()` **mất** thông tin file đính kèm (file trên đĩa vẫn còn).
- Script nâng cấp: `db/scripts/0.10.0.sql` (từ 0.9.0); cài mới thì chạy lần lượt mọi script trong `db/scripts`.

### Lưu ý khi nâng cấp
- **Sao lưu CSDL trước**, rồi chạy `db/scripts/0.10.0.sql`.
- Docker: `docker-compose.yml` và `db/backup.sh` có thay đổi (volume `attachments`, biến `Attachments__RootPath`) – `git pull` trước khi
  `docker compose build app` và `docker compose up -d`.
- Windows/IIS: đặt `Attachments:RootPath` và đường dẫn log ra **ngoài** thư mục website (docs/deploy-windows.md mục 3.4).
- Gán quyền `Analysis.PurchaseHistory.*` cho vai trò cần dùng dữ liệu mua lịch sử.

### Cách quay lại bản trước
- Code: triển khai lại tag `v0.9.0` (Docker: `APP_VERSION=0.9.0`; Windows: chép lại gói 0.9.0).
- CSDL: chỉ khi đã sao lưu – khôi phục bản sao lưu trước nâng cấp, hoặc
  `dotnet ef database update 20261031145516_V0_9_0_ThamSoCongTy` (mất dữ liệu như mục "Thay đổi CSDL").

## [0.9.0] - 2026-10-31

### Thêm mới
- Quyền mới `MasterData.Catalog.EditShared` – "Sửa thông tin danh mục dùng chung (trừ mã)" (design.md mục 5.6, 5.7, 5.9): sửa tên và
  thông tin công ty, đơn vị tính, nhóm hàng, hàng hóa, nhóm NCC, NCC ở bất kỳ công ty nào đang làm việc; mã không sửa được; thêm mới vẫn chỉ ở
  tổng công ty. Vai trò Quản trị tự có; vai trò khác gán tại Hệ thống → Vai trò. Màn hình danh mục báo rõ "chỉ xem" hay "được sửa thông tin".
- Đánh số danh mục tự động (design.md mục 5.11): màn hình Hệ thống → Đánh số danh mục (DSDM) cấu hình dải mã (tiền tố, số chữ số, từ số – đến số)
  cho nhà cung cấp, nhóm NCC, nhóm hàng, hàng hóa (riêng từng nhóm hàng hoặc dòng mặc định với `{NHOM}`). Thêm mới thì hệ thống cấp mã,
  người dùng không tự đặt; chưa cấu hình dải thì không thêm được. Nhập Excel: để trống mã khi thêm mới. API `/api/catalog-numberings`.
- Thông báo (design.md mục 11.6): màn hình Hằng ngày → Thông báo (NOTI) và chuông trên Shellbar có số chưa đọc. Hệ thống báo khi
  PR/PO chờ mình duyệt (theo cấp và vai trò), phiếu mình lập được duyệt hoặc bị từ chối (kèm lý do), có phiếu nhập mua nội bộ chờ nhận
  (thủ kho công ty mua), kỳ kho được mở lại (người có quyền xuất dữ liệu kế toán). Bấm thông báo mở thẳng phiếu và đánh dấu đã đọc;
  có "Đánh dấu tất cả đã đọc". API `/api/notifications`.
- Tham số hệ thống (design.md mục 5.4): màn hình Hệ thống → Tham số hệ thống (SEC) theo công ty đang làm việc. Sửa được "Số NCC tối thiểu
  khi gửi RFQ" (1–20, mặc định 3, mỗi công ty một giá trị); xem phương pháp giá vốn và các kho cho phép tồn âm. API `/api/system-parameters`.

### Thay đổi
- Ô mã trên form thêm NCC, nhóm NCC, nhóm hàng, hàng hóa bị khóa, ghi "(tự cấp khi lưu)"; cột mã trong file Excel mẫu không còn bắt buộc.
- Giao diện chuyển sang quy chuẩn SAP Fiori, theme Horizon – lát UI-a (docs/ui-guide.md, design.md mục 17): chữ 14px, bảng màu Horizon
  (chữ #1d2d3d, nền #f5f6f7, màu nhấn #0a6ed1, màu trạng thái ngữ nghĩa), bo góc 4/8px, đổ bóng nhẹ, khoảng cách 8/16px, dòng bảng 32px,
  thanh tab gạch chân, khối nội dung/form bo góc; màn hình đăng nhập dạng thẻ. Giữ React + Ant Design, bố cục khung giữ nguyên (lát UI-b
  sẽ đổi sang Shellbar + Dynamic Page). CLAUDE.md mục 9 nạp toàn văn docs/ui-guide.md mỗi phiên.
- Giao diện Fiori – lát UI-b (ui-guide.md mục 4–6, design.md mục 17): Shellbar 48px thay thanh menu, thanh công cụ và thanh trạng thái
  (logo, Quay lại, menu module với Alt + chữ gạch chân, ô lệnh, công ty, thông báo, avatar – menu người dùng có ngày làm việc và phiên bản);
  điều hướng trái kiểu Side Navigation; danh sách dạng Dynamic Page (phần đầu thu gọn được để ẩn bộ lọc) và mũi tên ">" cuối dòng để mở chi tiết;
  chứng từ dạng Dynamic Page: nút In phiếu ở phải tiêu đề, khối "Thông tin phiếu" thu gọn được, phiếu đang sửa có thanh chân chứa Lưu / Gửi
  duyệt / Ghi sổ; ngăn kéo chi tiết có nhãn "Chỉ xem". Bỏ chế độ INS/OVR (phím Insert).
- Gửi RFQ: số NCC tối thiểu lấy theo tham số công ty thay cho con số 3 cố định; RFQ đã gửi không bị ảnh hưởng.
- Đơn mua hàng: khi chưa có NCC chọn được, phiếu báo rõ chỉ NCC đã duyệt và đang áp dụng tại công ty mới đặt hàng được, kèm liên kết tới
  Danh mục nhà cung cấp.

### Thay đổi CSDL
- Migration `V0_9_0_DanhSoDanhMuc`: bảng mới `CatalogNumbering` (dải đánh số danh mục). `Down()` xóa bảng – mất cấu hình dải, mã đã cấp không đổi.
- Migration `V0_9_0_DaiMaMacDinh`: thêm 4 dải mặc định (NCC + 6 số, GN + 4 số, NH + 6 số, `{NHOM}-` + 5 số) cho danh mục chưa có dải mặc định;
  không ghi đè dải đã có. `Down()` chỉ xóa các dải do migration này tạo và chưa bị sửa – không mất dữ liệu người dùng.
- Migration `V0_9_0_ThongBao`: bảng mới `Notification`. `Down()` xóa bảng – mất các thông báo đã gửi (chứng từ, nhật ký không đổi).
- Migration `V0_9_0_ThamSoCongTy`: bảng mới `CompanyParameter`. `Down()` xóa bảng – mất tham số đã sửa, mọi công ty về mặc định 3 NCC.

### Lưu ý khi nâng cấp
- **Có thay đổi CSDL – sao lưu trước khi nâng cấp** (4 migration mới, chỉ thêm bảng và dải mã mặc định, không sửa dữ liệu cũ):
  `pg_dump -h localhost -U proc -F c -f procurement_truoc_0.9.0.dump procurement`.
- Môi trường Development tự áp migration khi chạy API. Môi trường chạy thật chạy script:
  `$env:PGCLIENTENCODING='UTF8'; psql -h <máy> -U <user> -d procurement -v ON_ERROR_STOP=1 -f db\scripts\0.9.0.sql`
  (script `--idempotent`, chạy lại không lỗi; đã chạy thử trên CSDL ở bản 0.8.0).
- Từ bản này mã nhà cung cấp, nhóm NCC, nhóm hàng, hàng hóa do hệ thống tự cấp. Ngay sau khi nâng cấp, quản trị đăng nhập ở tổng công ty,
  gõ `DSDM` (Hệ thống → Đánh số danh mục), xem 4 dải mặc định và sửa tiền tố, số chữ số, khoảng số cho đúng quy ước mã của công ty trước khi
  nhân viên thêm danh mục mới. Mã đang có không đổi; mã kế tiếp = số lớn nhất đang có trong dải + 1.
- Cấp quyền "Sửa thông tin danh mục dùng chung (trừ mã)" cho vai trò cần sửa tên danh mục ở công ty thành viên (vai trò Quản trị tự có).
- Giao diện đổi sang SAP Fiori (Shellbar + Dynamic Page): báo người dùng biết thanh công cụ, thanh trạng thái không còn; phím tắt, ô lệnh giữ nguyên.

### Cách quay lại bản trước
- Code: không xóa lịch sử; tạo nhánh từ tag `git switch -c rollback/v0.8.0 v0.8.0`, build và chạy.
- CSDL (chỉ khi đã sao lưu): `dotnet ef database update V0_8_0_XepLoaiNcc -p src\Proc.Infrastructure -s src\Proc.Api`.
  **Mất dữ liệu**: dải đánh số danh mục, thông báo, tham số công ty đã sửa (không ảnh hưởng chứng từ, danh mục, mã đã cấp). Muốn giữ dữ liệu
  thì khôi phục bản sao lưu: `pg_restore -h localhost -U proc -d procurement --clean procurement_truoc_0.9.0.dump`.

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
