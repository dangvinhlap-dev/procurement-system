# Kế hoạch thực hiện module Quản trị vận hành (QT) – lát QT1 → QT9

Lập 14/11/2026. **Thiết kế gốc và quyết định đã chốt: `docs/design.md` mục 21** (21.1 quyết định E1–E9, Q2–Q7, B1–B4; 21.2 nguyên tắc N1–N6;
21.3 ba môi trường; 21.3a bảo vệ TEST như PROD; 21.4 màn hình; 21.5 sao lưu; 21.6 phiếu phát hành; 21.6a quay về bản cũ ba mức; 21.7 chia lát;
21.8 đưa chính module QT vào vận hành). File này là **kế hoạch chi tiết từng lát** để làm theo; khi chi tiết lúc code khác kế hoạch thì sửa file
này và design.md mục 21 trong cùng commit. Bản PDF cùng nội dung (để người dùng đọc): `D:\project\PhanTich_KeHoach_QuanTriVanHanh_14-11-2026_v2.pdf`.

## 1. Quy trình chung mỗi lát (CLAUDE.md mục 1, 7)

1. `git pull` main → nhánh `feature/qt<n>-<tên-ngắn>`; **trình kế hoạch ngắn** (việc, file, test) cho người dùng trước khi code.
2. Viết test trước (domain + tích hợp trên PostgreSQL thật) → code → `dotnet build`, `dotnet test`; giao diện: `npm run build`, `npx tsc -b`,
   `npx oxlint src e2e`, `npx playwright test`.
3. Màn hình mới: chạy lên, chụp bằng Playwright, đối chiếu `docs/ui-style.md` (AppShell, ListPage + DataGrid, DocumentForm, DetailDrawer, icon
   theo mức quan trọng, ô ngày, chữ tab in hoa).
4. Thay đổi CSDL: migration tên rõ nghĩa có `Down()`; theo kiểu "thêm trước – bỏ sau" (design.md 21.6a) để bản quay về được mức 1.
5. Mỗi bước nhỏ commit + push, ghi `docs/TIEN_DO.md`; xong lát merge main, xóa nhánh.

Quy ước chung:
- **Mã quyền** theo quy ước có sẵn `System.Ops.*` (khai trong `Proc.Domain/Organization/Permissions.cs`, vai trò mẫu Quản trị hệ thống).
  design.md 21.6 ghi tắt `Ops.*` – sửa thành `System.Ops.*` ở QT1. Mọi quyền QT chỉ dùng được khi làm việc ở **tổng công ty**, từ dải IP cho
  phép (QT7), không có trên site Portal.
- **API** dưới `/api/ops/v1/...` – chỉ **thêm** trường, không sửa / bỏ (TEST có thể chạy bản mới hơn PROD – design.md 21.8 T3).
- Không viết cứng đường dẫn, cổng; mọi đường dẫn từ Options, ghép bằng `Path.Combine` (CLAUDE.md mục 3). Windows trước, Docker sau (Q4).
- Việc nguy hiểm (phục hồi, hoán đổi, cài PROD, quay về bản cũ, ngắt phiên, làm mới TEST): **nhập lại mật khẩu + lý do + gõ chữ xác nhận** (Q6).
- Mọi thao tác QT ghi **nhật ký vận hành** (CSDL + file ngoài CSDL – N3).

## 2. Các lát

### QT1 – Nhận diện môi trường, khung module, nhật ký vận hành, Tổng quan (SYS)
Bản 0.32.0 · khối lượng Vừa · GĐ1 (cài tay).

| Hạng mục | Nội dung |
| --- | --- |
| Mục tiêu | Mỗi máy biết mình là DEV / TEST / PROD; TEST không bao giờ gửi email thật; có khung module, quyền, nhật ký vận hành, màn hình tổng quan cho môi trường đang chạy |
| Cấu hình | `App:Environment` (`Dev` / `Test` / `Prod`; mặc định `Dev` ở appsettings.Development, `Prod` ở file mẫu Production) · `Email:RedirectAllTo` · `Operations:LogDirectory` (ngoài thư mục ứng dụng) |
| Backend | Domain `Operations/OperationLog` (chỉ thêm: giờ, người, môi trường, loại việc, đối tượng, lý do, kết quả, chi tiết) · `IOperationLog` ghi CSDL + file mỗi ngày · `ISystemOverview`: phiên bản, migration đã chạy / đang chờ (`GetAppliedMigrationsAsync`, `GetPendingMigrationsAsync`), `pg_database_size`, số kết nối (`pg_stat_activity`), dung lượng trống ổ chứa đính kèm / log, nhịp chạy dịch vụ nền (`IBackgroundHeartbeat` do từng BackgroundService cập nhật) · `EmailBackgroundService`: có `RedirectAllTo` thì đổi người nhận, thêm "[TEST] gửi cho: …" vào tiêu đề · bộ lọc endpoint bắt buộc tổng công ty cho mọi API QT |
| API | `GET /api/ops/v1/overview` · `GET /api/ops/v1/operation-logs` · `GET /api/ops/v1/environment` (ẩn danh, chỉ trả tên môi trường để màn hình đăng nhập vẽ dải màu) |
| Quyền | `System.Ops.View` |
| Giao diện | `EnvironmentBanner` (dải xám DEV / cam TEST) trong AppShell, trang đăng nhập, trang điện thoại `/duyet`, Portal; tiêu đề tab "[TEST]" · màn hình **SYS** `/he-thong/van-hanh` · **Nhật ký vận hành** `/he-thong/nhat-ky-van-hanh` · biến màu `--erp-env-dev`, `--erp-env-test` + mục mới trong ui-style.md |
| CSDL | Migration `V0_32_0_NhatKyVanHanh` |
| Test | Tích hợp `OperationsOverviewTests` (quyền, chỉ tổng công ty, số migration), `EmailRedirectTests` (môi trường Test chuyển người nhận), `OperationLogTests` (chỉ thêm, có ghi file) · e2e `ops-overview.spec.ts` (SYS hiện phiên bản; dải TEST khi API báo Test) |
| Tài liệu | design.md 21 (mã quyền `System.Ops.*`), ui-style.md, deploy-windows.md (cấu hình mới), `appsettings.Production.example.json` |
| Xong khi | Máy cấu hình Test hiện dải cam ở mọi màn hình và mọi email đi về hộp thư test; SYS đúng số liệu; mọi thao tác QT có dòng nhật ký ở CSDL và file |

### QT2 – Nhật ký lỗi (ERR)
Bản 0.32.0 · Vừa · GĐ1.

| Hạng mục | Nội dung |
| --- | --- |
| Mục tiêu | Mọi lỗi hệ thống (máy chủ và giao diện) được ghi, gộp nhóm, báo người phụ trách, theo dõi tới khi xử lý xong |
| Backend | Domain `ErrorGroup` (dấu vết = băm loại lỗi + vị trí đầu ngăn xếp + đường dẫn chuẩn hóa; lần đầu / lần cuối, số lần, trạng thái Mới / Đang xử lý / Đã xử lý, người xử lý, ghi chú; Đã xử lý mà lặp lại thì tự mở lại Mới) + `ErrorOccurrence` (giờ, người, công ty, đường dẫn, phương thức, mã theo dõi, nội dung, chi tiết kỹ thuật cắt 16 KB, phiên bản, môi trường, nguồn Server/Client, trình duyệt) · `ErrorRecorder` gọi từ `ProblemExceptionHandler` cho lỗi 500 – phạm vi CSDL riêng, không bao giờ ném lỗi tiếp · email người có quyền quản lý khi có **nhóm lỗi mới** (qua hàng chờ email sẵn có) · dịch vụ nền xóa lỗi cũ (tham số `ERRLOG_KEEP_DAYS`, mặc định 90) |
| API | `POST /api/ops/v1/client-errors` (đã đăng nhập, ≤ 8 KB, 30 lần/phút/người) · danh sách nhóm, chi tiết, lần xảy ra, đổi trạng thái |
| Quyền | `System.Ops.ErrorLog.View`, `System.Ops.ErrorLog.Manage` |
| Giao diện | `ErrorBoundary` React + bắt `window.onerror`, `unhandledrejection` (chống gửi trùng) · **ERR** `/he-thong/nhat-ky-loi`: ListPage + DetailDrawer (tab Lần xảy ra, Chi tiết kỹ thuật, Ghi chú) · thanh trạng thái hiện mã theo dõi khi lỗi |
| CSDL | Migration `V0_32_0_NhatKyLoi`; tham số `ERRLOG_KEEP_DAYS` |
| Test | Tích hợp: lỗi 500 tạo nhóm, gộp lỗi trùng, mở lại khi lặp, quyền, 429 ở client-errors, xóa theo hạn, email nhóm mới · e2e `error-log.spec.ts` |
| Xong khi | Gây lỗi thử ở máy chủ và giao diện đều thấy trong ERR, mã theo dõi trùng với câu báo lỗi người dùng thấy |

### QT3a – Bật lưu WAL và theo dõi
Bản 0.33.0 · Nhỏ · GĐ1.

| Hạng mục | Nội dung |
| --- | --- |
| Mục tiêu | "Log backup" liên tục, mất tối đa 5 phút dữ liệu; nền tảng cho Diff và khôi phục theo thời điểm |
| Script | `deploy/windows/qt-enable-wal.ps1` (UTF-8 BOM) + bản Docker (tham số `command` dịch vụ `db` + volume WAL): `wal_level=replica`, `archive_mode=on`, `archive_timeout=300`, `summarize_wal=on`, `wal_summary_keep_time` dài hơn chu kỳ giữ Full; `archive_command` không ghi đè file đã có (Windows `if not exist … copy`; Linux `test ! -f … && cp`); tạo vai trò `proc_maint` (REPLICATION, CREATEDB) + dòng `pg_hba.conf`; nhắc khởi động lại PostgreSQL một lần |
| Backend | `IWalArchiveStatus`: đọc `pg_stat_archiver` + `SHOW` tham số → "Chưa bật" / "Đang chạy" / "Trễ > 15 phút" / "Có lỗi" |
| Cấu hình | `ConnectionStrings:Maintenance` (tài khoản `proc_maint`), `Backup:WalArchivePath` |
| Giao diện | **BAK** `/he-thong/sao-luu` – tab "Lưu WAL" (đèn kèm chữ); SYS thêm cột độ trễ WAL |
| Test | Tích hợp đọc trạng thái khi chưa bật · kiểm tra tay script trên máy TEST theo danh sách trong deploy-windows.md |

### QT3b – Sao lưu Full / Diff / pg_dump / đính kèm (BAK)
Bản 0.33.0 · Lớn · GĐ1.

| Hạng mục | Nội dung |
| --- | --- |
| Mục tiêu | Tự sao lưu theo lịch B1, kiểm tra từng bản, giữ đúng hạn, chép nơi thứ hai, có lịch sử và cảnh báo |
| Backend | `PgToolRunner`: chạy `pg_basebackup`, `pg_verifybackup`, `pg_dump`, `pg_restore --list`, `pg_archivecleanup` bằng `ProcessStartInfo.ArgumentList` (không qua shell), mật khẩu qua biến môi trường tiến trình con, giới hạn thời gian, ghi stderr vào nhật ký · `BackupService`: Full (định dạng thư mục plain vì `pg_combinebackup` cần thư mục), Diff (`--incremental=<Full>/backup_manifest`, luôn gốc Full), pg_dump, đính kèm (chép file mới, chạy được Windows / Linux), SHA256, chép nơi thứ hai · `BackupScheduler` (dịch vụ nền, `pg_advisory_lock` chống chạy trùng) · dọn: Full quá 4 tuần kèm Diff của nó, WAL cũ hơn Full cũ nhất còn giữ · cảnh báo ổ còn < 15% |
| Domain | `BackupRun`: kiểu (Full / Diff / Dump / Attachments), trạng thái, giờ bắt đầu / kết thúc, đường dẫn, dung lượng, SHA256, Full gốc, đã kiểm tra, người chạy / lịch, thông báo lỗi |
| Cấu hình | `Backup:RootPath`, `Backup:SecondaryPath`, `Backup:PgBinPath` · tham số `BACKUP_FULL_DAY`, `BACKUP_TIME`, `BACKUP_KEEP_WEEKS`, `DUMP_KEEP_DAYS` |
| API, quyền | Danh sách lần sao lưu, chạy ngay theo kiểu · `System.Ops.Backup.Run` · **không có API tải file sao lưu** (Q5) |
| Giao diện | BAK: tab Lịch sử, Lưu WAL, Lịch và thời hạn giữ, Nơi lưu; nút "Sao lưu ngay" (icon xanh) chọn kiểu |
| CSDL | Migration `V0_33_0_SaoLuu` |
| Test | Đơn vị: tính lịch, chọn bản cần xóa, tạo tham số lệnh (đường dẫn có dấu cách, có dấu tiếng Việt) · tích hợp: pg_dump + kiểm tra trên CSDL test (bỏ qua có ghi chú khi máy không có công cụ PostgreSQL) · diễn tập D1 |
| Tài liệu | deploy-windows.md mục 4: tắt lịch `backup.ps1` cũ khi bật lịch trong phần mềm; Dockerfile thêm `postgresql-client-18` (kho PGDG) khi làm phần Docker |

### QT4 – Bảo trì CSDL (DBM)
Bản 0.33.0 · Vừa · GĐ1.

| Hạng mục | Nội dung |
| --- | --- |
| Backend | Dung lượng từng bảng (`pg_total_relation_size`), dòng chết và lần dọn cuối (`pg_stat_user_tables`), phiên (`pg_stat_activity`), khóa đang chờ (`pg_locks`) · VACUUM (ANALYZE) một bảng / tất cả, `REINDEX TABLE CONCURRENTLY`, `pg_terminate_backend` (xác nhận Q6) · `MaintenanceScheduler` hằng tuần · domain `MaintenanceRun` |
| Quyền, giao diện | `System.Ops.Maintenance.Run` · **DBM** `/he-thong/bao-tri-csdl`: tab Bảng, Phiên và khóa, Lịch sử |
| CSDL | Migration `V0_33_0_BaoTriCsdl` |
| Test | Tích hợp: VACUUM bảng thử, ngắt kết nối thử, từ chối khi thiếu mật khẩu xác nhận / lý do, quyền · e2e `db-maintenance.spec.ts` |

### QT5 – Bộ thực thi và chế độ bảo trì
Bản 0.34.0 · Lớn · GĐ2.

| Hạng mục | Nội dung |
| --- | --- |
| Mục tiêu | "Cánh tay" quyền cao trên mỗi máy để cài đặt, khôi phục, dừng / chạy dịch vụ – web chỉ ra lệnh (N1) |
| Chế độ bảo trì | `MaintenanceModeMiddleware`: bật thì mọi request trả 503 kèm câu thông báo, trừ người có quyền `System.Ops.*`; dải đỏ trên màn hình; trạng thái lưu CSDL + đệm ngắn |
| Bộ thực thi | `deploy/windows/qt-agent.ps1` + `qt-agent-install.ps1` (Task Scheduler mỗi phút, tài khoản riêng có quyền sửa thư mục website, điều khiển dịch vụ PostgreSQL, thư mục sao lưu) · khóa file chống chạy chồng · `agent-journal.log` · lệnh hết hạn sau 1 giờ · lệnh đầu tiên `Ping` (báo phiên bản, không cài) · chế độ `-DryRun` |
| Backend, API | Domain `AgentCommand` (môi trường, loại, tham số, trạng thái Chờ / Đang chạy / Xong / Lỗi, người tạo, lý do, kết quả, nhật ký) + `EnvironmentRegistration` (tên, địa chỉ, băm khóa bộ thực thi, băm khóa trạng thái, lần liên lạc cuối, phiên bản) · `GET /api/ops/v1/agent/commands`, `POST /api/ops/v1/agent/commands/{id}/result` xác thực bằng header `X-Ops-Agent-Key` (băm lưu CSDL, không phải JWT) |
| CSDL | Migration `V0_34_0_BoThucThi` |
| Test | Tích hợp: khóa sai / hết hạn bị từ chối, một môi trường chỉ một lệnh đang chạy, ghi kết quả, chế độ bảo trì chặn người thường cho qua admin · kiểm tra tay bộ thực thi `-DryRun` trên máy TEST |

### QT6 – Phục hồi (RST)
Bản 0.34.0 · Lớn · GĐ2.

| Hạng mục | Nội dung |
| --- | --- |
| Từ pg_dump | Khôi phục vào CSDL mới `procurement_restore_yyyyMMdd_HHmm` (tài khoản Maintenance) → so sánh số dòng các bảng chính với CSDL đang chạy → "Hoán đổi" giao bộ thực thi: bật bảo trì, dừng site, đổi tên CSDL cũ thành `procurement_old_…`, đổi tên bản khôi phục thành `procurement`, chạy site, kiểm tra `/health` |
| Về mốc thời gian | Chọn mốc → tìm Full gần nhất trước mốc, Diff gần nhất sau Full đó, kiểm tra WAL liên tục tới mốc → hiện kế hoạch → bộ thực thi: `pg_combinebackup` ra thư mục tạm, đặt `restore_command` + `recovery_target_time`, chạy máy chủ CSDL tạm cổng 5434 → người dùng kiểm tra → "Hoán đổi": dừng PostgreSQL chính, cất thư mục dữ liệu cũ, đưa thư mục khôi phục vào, chạy lại, kiểm tra |
| Bảo vệ | `System.Ops.Restore.Run`; Q6 (gõ "KHÔI PHỤC"); nhật ký ghi ra file **trước** khi hoán đổi; giữ bản cũ tới khi người dùng xóa tay |
| CSDL | Migration `V0_34_0_PhucHoi` (`RestoreRun`) |
| Test | Tích hợp: khôi phục pg_dump sang CSDL tên tạm + so sánh số dòng (không hoán đổi CSDL test) · đơn vị: chọn chuỗi Full → Diff → WAL, phát hiện đứt WAL · diễn tập D2 bắt buộc |

### QT7 – Cấu hình truy cập và bảo mật (CFG)
Bản 0.35.0 · Vừa · GĐ2.

| Hạng mục | Nội dung |
| --- | --- |
| Giới hạn tần suất | Dùng lại bộ giới hạn của Portal (`Proc.Api/Security/PortalRateLimiting.cs`) cho phần nội bộ: `/api/auth/login` theo IP và theo tên đăng nhập, giới hạn chung theo người dùng, giới hạn đồng thời nhập Excel / in PDF / báo cáo nặng; 429 kèm câu tiếng Việt |
| Header bảo mật | HSTS (khi HTTPS), CSP (`default-src 'self'`, ảnh `data: blob:`, style inline cho Ant Design, `frame-ancestors 'none'`), `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` |
| Thu hồi phiên | Lưu SecurityStamp (hiện `AppUser` đang Ignore) → claim trong JWT → kiểm ở `UserScopeLoader` (đệm 1 phút); đổi / đặt lại mật khẩu, khóa tài khoản, "Đăng xuất tất cả" làm token cũ hết hiệu lực |
| Chính sách | Tham số: độ dài mật khẩu tối thiểu (mặc định 10), chặn mật khẩu phổ biến (danh sách nhúng), số lần sai, phút khóa, hạn token; dải IP được dùng chức năng QT; chế độ bảo trì chuyển vào CFG |
| Khác | `/health` ẩn danh chỉ trả trạng thái, chi tiết cho người có quyền; `ForwardedHeaders` chỉ tin proxy khai trong cấu hình |
| CSDL, quyền | Migration `V0_35_0_CauHinhTruyCap` · `System.Ops.Config.Manage` |
| Test | Tích hợp: 429, đủ header, token cũ bị từ chối sau đổi mật khẩu, IP ngoài danh sách bị chặn QT, mật khẩu yếu bị từ chối · e2e `access-config.spec.ts` |

### QT8 – Trung tâm phát hành (REL)
Bản 0.36.0 · Lớn · GĐ2 (lần cài tay cuối).

| Hạng mục | Nội dung |
| --- | --- |
| Domain | `ReleaseTicket` (phiên bản, gói, SHA256, nguồn, CHANGELOG, thay đổi CSDL, rủi ro, cách quay lại, Ghi chú; trạng thái Đã nhận → Đang UAT → UAT đạt → Đã cài PROD → Đã đóng, cộng Đã hủy, Đã quay về) + `ReleaseTestItem`, `ReleaseDefect`, `ReleaseInstall`; khóa từ Đã cài PROD; loại chủ đính kèm mới trong `AttachmentOwnerTypes` + `AttachmentService`. Không áp chuỗi duyệt mục 20 (E3) |
| Nhận gói | GitHub Release (API công khai – tài sản `.zip` + `.sha256`) hoặc tải lên (endpoint riêng tới 200 MB) · `publish.ps1` sinh thêm `.sha256`, đưa `CHANGELOG.md` + `rollback.json` + `db-scripts\down\<bản>.sql` vào gói · đọc mục CHANGELOG của bản để tạo mục kiểm thử · lưu ở `Operations:PackageRoot` |
| Lệnh cài | Kiểm SHA256 → sao lưu (Full + kiểm tra trên PROD, pg_dump trên TEST) → `app_offline.htm` → cất thư mục ứng dụng hiện tại vào `releases\<bản cũ>` → giải nén bản mới, giữ `appsettings.Production.json` → chạy các `db-scripts` mới hơn bản CSDL hiện tại (`psql -v ON_ERROR_STOP=1`) → bỏ `app_offline.htm` → chờ `/health` báo đúng phiên bản → kiểm thử nhanh (`/api/ops/v1/smoke`: truy vấn CSDL, các danh sách chính, REL mở được) → báo kết quả; hỏng thì quay về (mức 3 – không mất dữ liệu vì đang bảo trì) · bộ thực thi tự cập nhật sau cùng (T1) |
| Quy tắc | Cài PROD chỉ khi UAT đạt (hoặc chế độ một môi trường) và SHA256 trùng gói đã cài TEST; thông báo người dùng trong phần mềm + email trước khi cài (số phút theo tham số); **quay về bản cũ ba mức – mặc định chỉ trả code** (design.md 21.6a): REL đề xuất mức thấp nhất theo `rollback.json`, liệt kê dữ liệu sẽ mất ở mức 2, 3 |
| Quyền | `System.Ops.Release.Manage`, `.DeployTest`, `.UatSignOff`, `.DeployProd` |
| Giao diện | **REL** `/he-thong/phat-hanh`: bảng môi trường + ListPage phiếu + DocumentForm (tab Mục kiểm thử, Lỗi phát hiện, Lịch sử cài, Đính kèm); chỉ hoạt động khi `App:Environment=Prod` |
| CSDL | Migration `V0_36_0_TrungTamPhatHanh` |
| Test | Domain: máy trạng thái, khóa phiếu, chọn mức quay về · tích hợp: nhận gói (nguồn giả lập từ file), sai SHA256 bị từ chối, chặn cài PROD khi chưa UAT đạt, quyền, tương thích API v1 · bộ thực thi `-DryRun` · e2e `release-center.spec.ts` · diễn tập D3, D4 |
| Tài liệu | Viết lại `docs/phat-hanh.md`, CLAUDE.md mục 8, deploy-windows.md (cài bộ thực thi, khai môi trường) – **trình người dùng trước khi đổi quy tắc** |

### QT9 – Làm mới TEST từ PROD (nguyên trạng) và nút báo lỗi trên TEST
Bản 0.37.0 · Vừa · GĐ4 (cài qua trung tâm).

| Hạng mục | Nội dung |
| --- | --- |
| Làm mới TEST | Lệnh `RefreshTest`: bộ thực thi PROD lấy pg_dump mới nhất (hoặc tạo mới) → bộ thực thi TEST tải qua endpoint chỉ dành cho bộ thực thi (khóa riêng, không mở cho trình duyệt) → bảo trì → khôi phục **nguyên trạng** → chép bổ sung file đính kèm → đặt lại phần riêng của TEST (`App:Environment=Test`, email chuyển hướng, khóa bộ thực thi), xóa hàng chờ email chưa gửi, phiên đăng nhập → `/health` |
| Dữ liệu, quyền | **Không làm mờ** (E6). Người dùng, vai trò, phạm vi, mật khẩu như PROD (E8); quyền admin cấp thêm trên TEST mất ở lần làm mới sau |
| Báo lỗi trên TEST | Nút "Báo lỗi / góp ý" trong AppShell khi môi trường Test: mô tả, ảnh màn hình (đính kèm), màn hình đang mở → API TEST chuyển tiếp máy chủ – máy chủ sang PROD → dòng Lỗi phát hiện của phiếu phát hành đang UAT đúng phiên bản |
| Quyền, test | `System.Ops.TestRefresh.Run` · tích hợp: dữ liệu sau làm mới khớp PROD, cấu hình riêng TEST còn nguyên, chặn chạy khi môi trường không phải Test, chuyển tiếp báo lỗi · diễn tập làm mới trên máy TEST |

## 3. Lộ trình phiên bản (dự kiến)

| Bản | Nội dung | GĐ | Cách đưa lên TEST → PROD |
| --- | --- | --- | --- |
| 0.31.0 | K3 – gỡ phân hệ kho (đã merge) | – | Như hiện nay |
| 0.32.0 | QT1 + QT2 | GĐ1 | Cài tay; đặt `App:Environment`, `Email:RedirectAllTo` (TEST) |
| 0.33.0 | QT3a + QT3b + QT4 | GĐ1 | Cài tay; `qt-enable-wal.ps1` + khởi động lại PostgreSQL; tắt lịch sao lưu cũ; diễn tập D1 |
| 0.34.0 | QT5 + QT6 | GĐ2 | Cài tay; cài bộ thực thi trên TEST (thử `Ping`); diễn tập D2 |
| 0.35.0 | QT7 | GĐ2 | Cài tay; đặt dải IP quản trị, chính sách mật khẩu (TEST giống PROD – 21.3a) |
| 0.36.0 | QT8 | GĐ2 | **Lần cài tay cuối**; bộ thực thi PROD; khai môi trường + khóa ở REL; "Lệnh thử"; diễn tập D3, D4 |
| 0.36.1 | Bản vá kiểm chứng | GĐ3 | **Lần đầu qua trung tâm phát hành**, có người theo dõi |
| 0.37.0 | QT9 | GĐ4 | Qua trung tâm phát hành |

Hiện chưa có máy TEST / PROD thật (bản test 5007 trên máy lập trình, dữ liệu thử). Code không phụ thuộc số máy; chưa đủ ba máy thì cài như
hiện nay lên 5007 và diễn tập trên đó. Phát hành chỉ khi người dùng yêu cầu (docs/phat-hanh.md).

## 4. Việc làm một lần trên mỗi máy chủ (TEST trước, PROD sau)

| Khi | Việc |
| --- | --- |
| 0.32.0 | Đặt `App:Environment`, `Operations:LogDirectory`; TEST đặt `Email:RedirectAllTo` |
| 0.33.0 | `qt-enable-wal.ps1` (Run as Administrator) → khởi động lại PostgreSQL → thư mục sao lưu ổ khác, thư mục WAL, (NAS) → khai `ConnectionStrings:Maintenance`, `Backup:*` → tắt tác vụ `backup.ps1` cũ → BAK báo WAL "Đang chạy" |
| 0.34.0 | Tạo tài khoản Windows riêng cho bộ thực thi, cấp quyền thư mục website / sao lưu / dịch vụ PostgreSQL → `qt-agent-install.ps1` |
| 0.36.0 | Trên PROD màn hình REL: khai TEST (và DEV nếu có), tạo khóa bộ thực thi + khóa trạng thái → dán vào cấu hình từng máy → "Lệnh thử" → SYS hiện đủ môi trường |
| Luôn | Bảo vệ TEST như PROD (design.md 21.3a): LAN/VPN, HTTPS, tường lửa 443, cùng CFG, bí mật riêng, cập nhật Windows, diệt virus, BitLocker |

## 5. Diễn tập nghiệm thu (bắt buộc trước khi coi giai đoạn là xong)

| # | Sau bản | Diễn tập | Đạt khi |
| --- | --- | --- | --- |
| D1 | 0.33.0 | Khôi phục Full + Diff + WAL của TEST ra máy chủ CSDL tạm bằng tay theo tài liệu | Số dòng các bảng chính khớp; ghi lại thời gian khôi phục |
| D2 | 0.34.0 | Tạo phiếu thử, ghi giờ, xóa phiếu; RST khôi phục về trước giờ xóa, hoán đổi | Phiếu có lại; dữ liệu sau mốc không còn; bản cũ còn giữ |
| D3 | 0.36.0 | Cài qua REL một gói cố ý hỏng (`/health` không đạt) lên TEST | Bộ thực thi tự quay về, site chạy bản cũ, phiếu ghi lỗi |
| D4 | 0.36.0 | Quay về bản cũ trên TEST ở mức 1 (nhập phiếu sau nâng cấp rồi quay về) và mức 2 (bản đổi CSDL không tương thích) | Mức 1: bản cũ chạy, phiếu sau nâng cấp còn nguyên; mức 2: đúng dữ liệu bị gỡ như đã liệt kê |

## 6. Rủi ro và cách giảm

| Rủi ro | Cách giảm |
| --- | --- |
| Hoán đổi CSDL / thư mục dữ liệu làm mất dữ liệu thật | Luôn khôi phục ra chỗ tạm trước, giữ bản cũ, xác nhận Q6, diễn tập D1–D2 trước khi dùng trên PROD |
| Thư mục WAL đầy ổ, PostgreSQL ngừng ghi | Dọn WAL theo Full còn giữ, cảnh báo ổ < 15% trên SYS + email, nơi lưu ở ổ riêng |
| Bộ thực thi quyền cao bị lạm dụng | Chỉ chạy loại lệnh có trong danh sách, khóa riêng từng máy (băm trong CSDL), lệnh hết hạn 1 giờ, nhật ký file + CSDL, không nhận lệnh từ Portal |
| Bản mới làm hỏng chính trung tâm phát hành | T2: bộ thực thi độc lập web, kiểm thử nhanh có bước "REL mở được", tự quay về; dự phòng cài tay deploy-windows.md mục 6 |
| Script CSDL lỗi giữa chừng trên PROD | Script idempotent, đã chạy trên TEST cùng gói; `ON_ERROR_STOP`; lỗi thì khôi phục bản sao lưu vừa tạo |
| TEST chứa dữ liệu thật (không làm mờ) bị lộ | Bảo vệ TEST như PROD (21.3a); SYS báo đèn đỏ khi cấu hình bảo mật TEST lỏng hơn PROD; email TEST luôn chuyển về hộp thư test |
| Khác biệt Windows / Docker | Windows trước; Docker sau với script tương ứng, cùng code |

## 7. Việc chờ người dùng

- Ra lệnh bắt đầu QT1 (không tự làm khi chưa có lệnh).
- Cung cấp khi tới lát: địa chỉ hộp thư test (QT1), đường dẫn ổ sao lưu / NAS (QT3), dải IP quản trị (QT7), địa chỉ ba máy chủ (QT8).
- Cân nhắc chuyển repo GitHub sang Private (code công khai).
