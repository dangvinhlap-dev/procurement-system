# Cài đặt trên Windows Server (IIS)

Cách thứ hai để chạy phần mềm, bên cạnh Docker trên Ubuntu (README.md mục "Triển khai server"). Cùng một mã nguồn, không sửa code:
mọi khác biệt (chuỗi kết nối, thư mục dữ liệu, file log) nằm trong `appsettings.Production.json`. Viết ngày 01/11/2026.

## 1. Yêu cầu

| Thành phần | Ghi chú |
| --- | --- |
| Windows Server 2019 trở lên (2016 vẫn chạy) | 2 CPU, 4 GB RAM trở lên; ổ dữ liệu riêng (ví dụ `D:`) |
| IIS (Web Server role) | Không cần cài ASP.NET 4.x, không cần WebDAV |
| **ASP.NET Core Hosting Bundle 10** | Tải ở https://dotnet.microsoft.com/download/dotnet/10.0 (mục "Hosting Bundle"). Cài **sau** IIS; nếu đã cài trước IIS thì chạy lại bộ cài, chọn *Repair* |
| PostgreSQL 18 cho Windows | Bộ cài EDB: https://www.postgresql.org/download/windows/ – có sẵn `psql`, `pg_dump` |
| Gói cài `procurement-<phiên bản>.zip` | Tạo bằng `deploy\windows\publish.ps1` (mục 2) |

Giới hạn: phần mềm phải là **một website riêng** trong IIS (gốc `/`), không đặt dưới đường dẫn con kiểu `/procurement`, vì giao diện
React được build cho đường dẫn gốc.

Ví dụ thư mục dùng trong tài liệu này (đổi tùy server):

| Thư mục | Dùng cho | Quyền của App Pool |
| --- | --- | --- |
| `D:\Procurement\app` | Website (nội dung thư mục `app` trong gói cài) | Đọc |
| `D:\ProcurementData\attachments` | File đính kèm (design.md mục 18.4) – **ngoài** thư mục website để cài bản mới không làm mất file | Sửa (Modify) |
| `D:\ProcurementData\logs` | File log kỹ thuật, mỗi ngày một file, giữ 30 ngày | Sửa (Modify) |
| `E:\ProcurementBackup` | Bản sao lưu (nên ở ổ/máy khác) | – (tác vụ sao lưu dùng tài khoản riêng) |

## 2. Tạo gói cài (trên máy build có .NET SDK 10 và Node.js 22)

```powershell
cd <thư mục repo>
.\deploy\windows\publish.ps1
```

Kết quả: `artifacts\procurement-<phiên bản>.zip`, giải nén gồm `app\` (website), `db-scripts\` (script CSDL), `tools\backup.ps1`,
`appsettings.Production.example.json`. Gói không chứa cấu hình dev/test.

## 3. Cài lần đầu

Chạy PowerShell **Run as Administrator** trên server.

**3.1. PostgreSQL** – cài bằng bộ cài EDB (đặt mật khẩu cho tài khoản `postgres`), mặc định chỉ nghe `localhost` là đủ. Tạo tài khoản và CSDL:

```powershell
$env:PGCLIENTENCODING = 'UTF8'
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -c "CREATE ROLE proc LOGIN PASSWORD '<mật khẩu CSDL>'"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -c "CREATE DATABASE procurement OWNER proc ENCODING 'UTF8' TEMPLATE template0"
```

**3.2. Tạo bảng** – chạy **lần lượt mọi script** trong `db-scripts` theo thứ tự phiên bản (mỗi script nâng từ bản trước lên; chạy lại
nhiều lần không sao):

```powershell
$env:PGCLIENTENCODING = 'UTF8'
Get-ChildItem .\db-scripts\*.sql | Sort-Object { [version]$_.BaseName } | ForEach-Object {
  & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U proc -d procurement -v ON_ERROR_STOP=1 -f $_.FullName
  if ($LASTEXITCODE -ne 0) { throw "Lỗi ở $($_.Name)" }
}
```

**3.3. Chép website và tạo thư mục dữ liệu**

```powershell
New-Item -ItemType Directory -Force D:\Procurement, D:\ProcurementData\attachments, D:\ProcurementData\logs | Out-Null
Copy-Item -Recurse .\app D:\Procurement\app
Copy-Item .\appsettings.Production.example.json D:\Procurement\app\appsettings.Production.json
```

**3.4. Cấu hình** – sửa `D:\Procurement\app\appsettings.Production.json`:

| Mục | Đặt |
| --- | --- |
| `App:Environment` | **Bắt buộc** (lát QT1): `Prod` trên máy chạy thật, `Test` trên máy thử nghiệm. Bỏ trống thì máy coi là "chưa khai": **không gửi email nào**, mọi màn hình hiện dải đỏ cảnh báo; khai sai tên thì ứng dụng không khởi động |
| `Operations:LogDirectory` | Thư mục file nhật ký vận hành, ngoài thư mục ứng dụng: `D:\ProcurementData\operations-log` |
| `ConnectionStrings:Procurement` | Mật khẩu CSDL của tài khoản `proc` |
| `Jwt:SigningKey` | Chuỗi ngẫu nhiên ≥ 32 ký tự, sinh bằng lệnh ngay dưới bảng |
| `Seed:AdminPassword`, `Seed:HeadOffice` | Mật khẩu quản trị ban đầu (đổi ngay sau lần đăng nhập đầu), mã/tên tổng công ty |
| `Attachments:RootPath` | `D:\\ProcurementData\\attachments` (trong JSON viết `\\`) |
| `Serilog:WriteTo` (mục `File`) | `path`: `D:\\ProcurementData\\logs\\proc-.log` |
| `Printing:FontDirectories` | Để trống – Windows có sẵn Arial có dấu tiếng Việt |
| `Email:*` | Gửi email bản sao thông báo (chờ duyệt, đã duyệt, bị từ chối…) qua SMTP của công ty: `Enabled` = `true`, `Host`, `Port` (587 STARTTLS / 465 SSL), `UserName`, `Password`, `FromAddress`, `AppBaseUrl` = địa chỉ người dùng mở phần mềm (nút "Mở trên phần mềm" trong thư). Để `false` thì không gửi thư |
| `Portal:*` | Portal nhà cung cấp (lát P1): `BaseUrl` = địa chỉ NCC mở Portal, ghi vào link mời / đặt lại mật khẩu trong email (ví dụ tên miền Portal mở ra internet; trống thì lấy `Email:AppBaseUrl`, vẫn trống thì lấy địa chỉ trang người mời đang mở – đủ khi thử trong LAN). Đăng nhập Portal cần **email bật** để gửi mã OTP. `Audience` phải khác `Jwt:Audience`. `ExposeCodesForTesting` chỉ có tác dụng ở Development/Testing – server thật luôn tắt; máy chủ **thử nghiệm trong LAN** chưa có email đặt thêm `TestServer` = `true` để mã OTP / link đặt lại mật khẩu hiện trên màn hình Portal (ghi cảnh báo vào log; không khởi động nếu đồng thời đặt `Hosts`). Các thời hạn (`InviteValidHours` 168, `OtpValidMinutes` 10, `LockoutMinutes` 15…) thường không cần sửa. Lát P1b: `Hosts` = tên miền Portal mở ra internet (nhiều tên cách dấu phẩy, ví dụ `ncc.tapdoan.vn`) – vào bằng tên này chỉ mở được trang `/ncc` và API Portal (xem 3.5b); `RateLimit:AuthPermitPerMinute` (20) / `RateLimit:ApiPermitPerMinute` (300) = số yêu cầu tối đa mỗi phút mỗi IP vào đăng nhập Portal / API Portal khác, vượt thì báo 429 "Quá nhiều yêu cầu" |
| `Email:RedirectAllTo` | Chỉ máy **TEST**: hộp thư nhận thay mọi thư (tiêu đề có "[TEST] gửi cho: người nhận gốc"). Máy TEST bật email mà để trống thì không gửi thư nào. Máy PROD để trống |
| `ApprovalReminder:*` | Nhắc người duyệt khi phiếu chờ quá 1 ngày làm việc (thông báo + email nếu bật email): mặc định `Enabled` = `true`, quét mỗi `PollMinutes` = 15 phút, tối đa `MaxReminders` = 3 lần mỗi cấp. Thường không cần sửa |

Sinh `Jwt:SigningKey` (bộ sinh số ngẫu nhiên dùng cho mật mã):

```powershell
$bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes); [Convert]::ToBase64String($bytes)
```

`Database:MigrateOnStartup` giữ `false`: CSDL chỉ nâng bằng script sau khi đã sao lưu (CLAUDE.md mục 8). Giá trị nào cũng có thể đặt
bằng biến môi trường thay cho file (ví dụ `ConnectionStrings__Procurement`), như bản Docker.

File này chứa mật khẩu: chỉ cho Administrators và App Pool đọc (lệnh `icacls` ở bước 3.5).

**3.5. IIS** – tạo App Pool và website (đổi tên miền/cổng theo thực tế):

```powershell
Import-Module WebAdministration
New-WebAppPool -Name Procurement
# Ứng dụng .NET 10 tự chạy trong tiến trình IIS qua ASP.NET Core Module: App Pool không dùng CLR của .NET Framework.
Set-ItemProperty IIS:\AppPools\Procurement -Name managedRuntimeVersion -Value ''
# Không tắt ứng dụng khi rảnh: lần mở đầu tiên sau giờ nghỉ không phải chờ khởi động.
Set-ItemProperty IIS:\AppPools\Procurement -Name startMode -Value AlwaysRunning
Set-ItemProperty IIS:\AppPools\Procurement -Name processModel.idleTimeout -Value '00:00:00'
New-Website -Name Procurement -PhysicalPath D:\Procurement\app -ApplicationPool Procurement -Port 80 -HostHeader ''

icacls D:\Procurement\app /grant "IIS AppPool\Procurement:(OI)(CI)RX"
icacls D:\ProcurementData /grant "IIS AppPool\Procurement:(OI)(CI)M"
icacls D:\Procurement\app\appsettings.Production.json /inheritance:r /grant "Administrators:F" "IIS AppPool\Procurement:R"
```

Nếu "Default Web Site" đang dùng cổng 80 thì dừng nó hoặc đặt binding khác. HTTPS: thêm binding 443 với chứng chỉ của đơn vị
(IIS Manager → Bindings), rồi bỏ binding 80 hoặc chuyển hướng sang HTTPS.

`web.config` trong gói đã: gỡ WebDAV (nếu không IIS chặn PUT/DELETE, báo lỗi 405 khi sửa/xóa), cho phép tải lên tới 30 MB (file
đính kèm tối đa mặc định 2 MB, đổi bằng `Attachments:MaxFileSizeMb` trong `appsettings.Production.json`, cho phép 1–25 MB; ngoài khoảng
này ứng dụng báo lỗi khi khởi động).

**3.5b. Tên miền Portal NCC mở ra internet** (lát P1b, design.md 19.5 – quyết định Q14: Portal có tên miền riêng, phần nội bộ chỉ LAN/VPN).
Cùng website, cùng bản cài; chỉ thêm binding và cấu hình:

1. Trỏ tên miền Portal (ví dụ `ncc.tapdoan.vn`) về IP công khai của server; tường lửa chỉ mở 443 (và 80 nếu cần chuyển hướng) tới IP đó.
2. IIS Manager → website Procurement → Bindings: thêm `https`, IP công khai, cổng 443, **Host name** `ncc.tapdoan.vn`, chọn chứng chỉ của tên
   miền, tích **Require Server Name Indication**. Binding nội bộ (ví dụ `http` cổng 80) gắn **IP LAN** của server, không để "All Unassigned",
   để từ internet không vào được phần nội bộ.
3. `appsettings.Production.json`: `Portal:Hosts` = `ncc.tapdoan.vn`, `Portal:BaseUrl` = `https://ncc.tapdoan.vn` (link trong email mời).
4. Thử từ máy ngoài mạng: `https://ncc.tapdoan.vn` tự chuyển sang `/ncc` (trang đăng nhập NCC); `https://ncc.tapdoan.vn/health` và
   `https://ncc.tapdoan.vn/api/auth/me` phải báo 404.

Giới hạn tần suất đếm theo IP thật của NCC: IIS chạy ứng dụng trong tiến trình nên đã có sẵn IP. Nếu đặt thêm reverse proxy / cân bằng tải
phía trước IIS thì phải bật forwarded headers (biến môi trường `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`), nếu không mọi NCC bị đếm chung
theo IP của proxy. File đính kèm (nội bộ và Portal) được kiểm nội dung khớp đuôi, chặn chương trình đổi đuôi (design.md 19.5 P1b-3).

**3.6. Kiểm tra** – mở `http://<server>/health` phải thấy `"status":"Healthy"` và số phiên bản; mở `http://<server>`, đăng nhập
bằng tài khoản quản trị ở bước 3.4 rồi **đổi mật khẩu ngay**. Xem `D:\ProcurementData\logs\proc-<ngày>.log` không có dòng `[ERR]`;
có dòng `[WRN] Thư mục file đính kèm … nằm trong thư mục ứng dụng` nghĩa là quên đặt `Attachments:RootPath` ra ngoài.
Đã bật email: gọi `POST /api/notifications/email/test` (quản trị, quyền sửa tham số hệ thống) để gửi thư thử tới email của chính mình;
`GET /api/notifications/email/status` xem số thư chờ gửi / lỗi. Lỗi gửi ghi ở log (`Gửi email thông báo … lỗi`). Email chạy trong tiến trình
ứng dụng nên App Pool phải `AlwaysRunning` (bước 3.5) và **chỉ một worker** – nhiều worker có thể gửi trùng thư.

## 4. Sao lưu hằng đêm

`tools\backup.ps1` (chép vào ví dụ `D:\Procurement\tools`): `pg_dump` CSDL vào `<BackupDir>`, giữ 14 ngày; chép bổ sung file đính kèm
vào `<BackupDir>\attachments` (file đính kèm chỉ thêm, không sửa/xóa trên đĩa nên chép bổ sung là đủ); ghi `backup.log`.

Mật khẩu CSDL không ghi trong script: tạo file `%APPDATA%\postgresql\pgpass.conf` **của tài khoản chạy tác vụ** với một dòng
`localhost:5432:procurement:proc:<mật khẩu CSDL>` và chỉ cho tài khoản đó đọc. Tạo tác vụ chạy 1 giờ sáng:

```powershell
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File D:\Procurement\tools\backup.ps1 -BackupDir E:\ProcurementBackup -AttachmentsDir D:\ProcurementData\attachments'
$trigger = New-ScheduledTaskTrigger -Daily -At 1:00
Register-ScheduledTask -TaskName 'Procurement backup' -Action $action -Trigger $trigger -User '<tài khoản sao lưu>' -Password '<mật khẩu>' -RunLevel Highest
```

Chép `E:\ProcurementBackup` ra máy khác định kỳ. Tác vụ báo "Last Run Result" khác 0 khi sao lưu lỗi.

## 5. Nâng cấp lên bản mới

Đọc CHANGELOG.md mục "Lưu ý khi nâng cấp" của bản đó trước.

1. Sao lưu: chạy tay tác vụ "Procurement backup" (hoặc lệnh `backup.ps1` ở mục 4) và kiểm tra có file `.dump` mới.
2. Tạm dừng website: chép một file `app_offline.htm` (nội dung "Đang nâng cấp, vui lòng quay lại sau ít phút") vào
   `D:\Procurement\app` – IIS dừng ứng dụng, người dùng thấy trang này.
3. Nếu bản mới có thay đổi CSDL: chạy các script trong `db-scripts` **mới hơn** bản đang chạy, như bước 3.2.
4. Chép đè nội dung thư mục `app` của gói mới vào `D:\Procurement\app`. **Giữ nguyên** `appsettings.Production.json` (gói không chứa
   file này nên chép đè không mất); file đính kèm nằm ở `D:\ProcurementData` nên không bị ảnh hưởng.
5. Xóa `app_offline.htm`, mở `/health` kiểm tra phiên bản mới.

**Từ bản 0.32.0** (lát QT1): trước khi chép bản mới, thêm `App:Environment` (`Prod` / `Test`) và `Operations:LogDirectory` vào
`appsettings.Production.json`; máy TEST thêm `Email:RedirectAllTo`. Quên khai `App:Environment` thì phần mềm vẫn chạy nhưng không gửi email và
hiện dải đỏ cảnh báo cho tới khi khai rồi khởi động lại App Pool. Sau khi nâng, màn hình **Hệ thống → Tổng quan hệ thống** (SYS) phải không còn
migration chờ, dòng môi trường đúng.

## 6. Khôi phục

- CSDL: dừng website (bước 5.2), rồi
  `pg_restore -h localhost -U proc -d procurement --clean --if-exists E:\ProcurementBackup\<file>.dump`, xóa `app_offline.htm`.
- File đính kèm: `robocopy E:\ProcurementBackup\attachments D:\ProcurementData\attachments /E /XC /XN /XO`.

## 7. Xử lý sự cố

| Hiện tượng | Nguyên nhân thường gặp |
| --- | --- |
| HTTP 500.19 | Chưa cài Hosting Bundle, hoặc cài trước IIS → chạy lại bộ cài, chọn Repair, rồi `iisreset` |
| HTTP 500.30 / 500.31 | Ứng dụng không khởi động: xem log ở `D:\ProcurementData\logs`; chưa có log thì sửa `web.config` tạm đặt `stdoutLogEnabled="true"`, tạo thư mục `D:\Procurement\app\logs` cho App Pool quyền ghi, xem file `stdout_*.log`, xong đặt lại `false` |
| "Thiếu chuỗi kết nối" trong log | Thiếu/sai tên file `appsettings.Production.json`, hoặc App Pool đặt `ASPNETCORE_ENVIRONMENT` khác `Production` |
| HTTP 405 khi lưu, xóa | WebDAV chưa gỡ – kiểm tra `web.config` còn thẻ `<remove name="WebDAVModule" />` |
| HTTP 413 khi tải file | File vượt `Attachments:MaxFileSizeMb` (mặc định 2 MB – ứng dụng báo lỗi tiếng Việt), hoặc `requestLimits` trong `web.config` bị sửa nhỏ đi |
| Không lưu được file đính kèm, không có file log | App Pool thiếu quyền Modify trên `D:\ProcurementData` (bước 3.5) |
| Không kết nối được CSDL | Dịch vụ `postgresql-x64-18` chưa chạy; sai mật khẩu; CSDL ở máy khác thì mở `listen_addresses` trong `postgresql.conf`, thêm dòng cho IP máy web trong `pg_hba.conf` và mở cổng 5432 trên tường lửa chỉ cho máy web |
| Máy khác trong mạng không mở được trang (bản test chạy thẳng `Proc.Api.exe`, không qua IIS) | Chạy với `--urls http://0.0.0.0:<cổng>` (không viết cứng IP trong cấu hình code) và mở cổng đó trên tường lửa Windows bằng PowerShell Administrator: `New-NetFirewallRule -DisplayName "Procurement test <cổng>" -Direction Inbound -Protocol TCP -LocalPort <cổng> -Action Allow -Profile Private,Domain`. Máy test mở `http://<IP máy chủ>:<cổng>` – xem `tools/du-lieu-thu/README.md` |
| In phiếu báo thiếu phông | Server bản Core không có `C:\Windows\Fonts\arial.ttf`: chép phông .ttf có tiếng Việt vào một thư mục và khai báo ở `Printing:FontDirectories` |
