# Hướng dẫn cài đặt chi tiết

Áp dụng cho **phiên bản 0.21.0**. Viết ngày 10/11/2026. Một bản build chạy được cả trên **Windows Server (IIS)** và **Linux (Docker)** – mọi khác
biệt nằm trong file cấu hình hoặc biến môi trường, không sửa code.

| Bạn muốn | Làm theo |
| --- | --- |
| Lập trình, chạy thử trên máy cá nhân Windows | [Phần A – Máy dev](#phần-a--máy-dev-windows-11) |
| Chạy thật trên máy chủ Windows Server | [Phần B – Windows Server + IIS](#phần-b--windows-server--iis-chạy-thật) |
| Chạy thật trên máy chủ Linux | [Phần C – Ubuntu + Docker](#phần-c--ubuntu--docker-chạy-thật) |
| Dựng nhanh bản test cho người dùng thử trong mạng nội bộ | [Phần D – Bản test trong mạng nội bộ](#phần-d--bản-test-trong-mạng-nội-bộ-có-dữ-liệu-thử) |
| Sao lưu, nâng cấp, quay lại bản cũ | [Phần E](#phần-e--sao-lưu-khôi-phục-nâng-cấp-quay-lại-bản-cũ) |
| Tra cứu khóa cấu hình | [Phần F](#phần-f--bảng-cấu-hình) |
| Gỡ lỗi | [Phần G](#phần-g--xử-lý-sự-cố) |

Sau khi cài: xem [Hướng dẫn sử dụng](huong-dan-su-dung/README.md), mục 08 – "Việc cần làm của quản trị khi bắt đầu dùng".

---

## Phần A – Máy dev (Windows 11)

### A.1 Cài phần mềm

| Phần mềm | Phiên bản | Ghi chú |
| --- | --- | --- |
| Git for Windows | mới nhất | https://git-scm.com/download/win |
| .NET SDK | **10.0.400** trở lên (file `global.json`) | https://dotnet.microsoft.com/download/dotnet/10.0 |
| Node.js | **22 LTS** | https://nodejs.org – có sẵn `npm` |
| PostgreSQL | **18** | Bộ cài EDB https://www.postgresql.org/download/windows/ – nhớ mật khẩu tài khoản `postgres`, cổng 5432 |
| Trình soạn thảo | VS Code / Visual Studio 2026 / Rider | Tùy chọn |

Kiểm tra trong PowerShell:

```powershell
git --version
dotnet --version        # 10.0.4xx
node --version          # v22.x
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" --version
```

### A.2 Tạo tài khoản và CSDL PostgreSQL

Mật khẩu dưới đây **chỉ dùng cho máy dev** (đã ghi sẵn trong `appsettings.Development.json`, `appsettings.Testing.json`):

```powershell
$env:PGCLIENTENCODING = 'UTF8'
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
& $psql -U postgres -c "CREATE ROLE proc LOGIN PASSWORD 'proc_dev_only'"
& $psql -U postgres -c "CREATE DATABASE procurement OWNER proc ENCODING 'UTF8' TEMPLATE template0"
& $psql -U postgres -c "CREATE DATABASE procurement_test OWNER proc ENCODING 'UTF8' TEMPLATE template0"
```

- `procurement`: CSDL dev; API tự tạo bảng và dữ liệu mẫu khi chạy lần đầu (`Database:MigrateOnStartup = true` trong Development).
- `procurement_test`: CSDL riêng cho test tích hợp (bị xóa và tạo lại khi chạy test – không dùng để nhập liệu).

### A.3 Lấy mã nguồn

```powershell
cd D:\Project
git clone https://github.com/dangvinhlap-dev/procurement-system.git
cd procurement-system
dotnet tool restore                 # công cụ dotnet-ef
cd src\Proc.Web; npm install; cd ..\..
```

### A.4 Chạy

Mở **hai** cửa sổ PowerShell tại thư mục gốc repo:

```powershell
# Cửa sổ 1 – API (http://localhost:5000)
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src\Proc.Api --no-launch-profile --urls http://localhost:5000
```

```powershell
# Cửa sổ 2 – giao diện (http://localhost:5173)
cd src\Proc.Web
npm run dev
```

- Kiểm tra API: mở http://localhost:5000/health → `"status":"Healthy"` và số phiên bản.
- Mở http://localhost:5173, đăng nhập **admin / Admin@2026**. Tài khoản mẫu thử phân quyền (mật khẩu **Demo@2026**): `thukho.bha` (thủ kho
  công ty BHA, chỉ kho KP1, nhóm hàng VT), `muahang.nmi` (nhân viên mua hàng NMI).
- Dữ liệu mẫu dev: tổng công ty TCT và 3 công ty BHA, NMI, DTV; kho, nhóm hàng, ĐVT, nhóm NCC; lịch kỳ năm hiện tại, mở sẵn tháng hiện tại.

### A.5 Kiểm tra trước khi commit (CLAUDE.md mục 2)

```powershell
dotnet build                        # phải 0 cảnh báo, 0 lỗi
dotnet test                         # test tích hợp trên procurement_test
cd src\Proc.Web
npm run build
npx tsc -b
npx oxlint src e2e
npx playwright install chromium     # lần đầu
npx playwright test                 # tự bật API (môi trường Testing) và Vite nếu chưa chạy
```

### A.6 Thay đổi CSDL (migration)

```powershell
# Thêm migration (tên rõ nghĩa, luôn có Down())
dotnet ef migrations add V0_22_0_TenRoNghia --project src\Proc.Infrastructure --startup-project src\Proc.Api
# Áp vào CSDL dev
dotnet ef database update --project src\Proc.Infrastructure --startup-project src\Proc.Api
```

Khi phát hành, script SQL cho máy chủ thật sinh vào `db\scripts\<phiên bản>.sql` – làm đủ các bước trong `docs\phat-hanh.md`.

Gỡ rối máy dev (khóa DLL khi build, Vite không nhận file sửa…): `docs\moi-truong-dev.md`.

---

## Phần B – Windows Server + IIS (chạy thật)

Bản tóm tắt gốc: `docs\deploy-windows.md`. Phần này viết đầy đủ từng bước.

### B.1 Yêu cầu

| Thành phần | Yêu cầu |
| --- | --- |
| Máy chủ | Windows Server 2019 trở lên (2016 vẫn chạy); 2 CPU, 4 GB RAM trở lên; ổ dữ liệu riêng (ví dụ `D:`) |
| IIS | Role **Web Server (IIS)** – không cần ASP.NET 4.x, không cần WebDAV |
| ASP.NET Core **Hosting Bundle 10** | https://dotnet.microsoft.com/download/dotnet/10.0 → mục "Hosting Bundle". Cài **sau** IIS (cài trước thì chạy lại bộ cài, chọn Repair) |
| PostgreSQL 18 | Bộ cài EDB, có sẵn `psql`, `pg_dump`, `pg_restore` |
| Gói cài | `procurement-0.21.0.zip` (B.2) |
| Máy build (máy dev) | .NET SDK 10, Node.js 22 |

Phần mềm phải là **một website riêng** trong IIS (gốc `/`), không đặt dưới đường dẫn con kiểu `/procurement`.

Thư mục ví dụ (đổi theo máy chủ):

| Thư mục | Dùng cho | Quyền App Pool |
| --- | --- | --- |
| `D:\Procurement\app` | Website | Đọc |
| `D:\Procurement\tools` | Script sao lưu | – |
| `D:\ProcurementData\attachments` | File đính kèm (**ngoài** thư mục website) | Sửa |
| `D:\ProcurementData\logs` | Log kỹ thuật, mỗi ngày một file, giữ 30 ngày | Sửa |
| `E:\ProcurementBackup` | Bản sao lưu (nên ở ổ/máy khác) | – |

### B.2 Tạo gói cài (trên máy build)

```powershell
cd D:\Project\procurement-system
git checkout v0.21.0                  # đúng tag phiên bản cần cài
.\deploy\windows\publish.ps1          # thêm -NoInstall nếu Vite đang chạy giữ node_modules
```

Kết quả `artifacts\procurement-0.21.0.zip`, giải nén gồm:

| Thư mục/file | Nội dung |
| --- | --- |
| `app\` | Website (Proc.Api.dll, web.config, wwwroot chứa giao diện) |
| `db-scripts\` | Script CSDL theo phiên bản (0.1.0.sql … 0.21.0.sql) |
| `tools\backup.ps1` | Sao lưu CSDL + file đính kèm |
| `tools\du-lieu-thu\` | Công cụ tạo dữ liệu thử (chỉ cho môi trường test) |
| `appsettings.Production.example.json` | Mẫu cấu hình |

Chép zip lên máy chủ, giải nén vào ví dụ `C:\Setup\procurement-0.21.0`.

### B.3 Cài IIS và Hosting Bundle

PowerShell **Run as Administrator**:

```powershell
Install-WindowsFeature Web-Server, Web-Mgmt-Console     # Windows Server
# Sau đó chạy bộ cài dotnet-hosting-10.x.x-win.exe rồi:
net stop was /y; net start w3svc
```

### B.4 PostgreSQL

1. Chạy bộ cài EDB, đặt mật khẩu `postgres`, giữ cổng 5432, chỉ nghe `localhost` (CSDL cùng máy web).
2. Tạo tài khoản ứng dụng và CSDL (đặt **mật khẩu mạnh**, ghi lại):

```powershell
$env:PGCLIENTENCODING = 'UTF8'
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
& $psql -U postgres -c "CREATE ROLE proc LOGIN PASSWORD '<mật khẩu CSDL>'"
& $psql -U postgres -c "CREATE DATABASE procurement OWNER proc ENCODING 'UTF8' TEMPLATE template0"
```

### B.5 Tạo bảng – chạy lần lượt mọi script

Mỗi script nâng từ bản trước lên; chạy lại nhiều lần không sao. **Phải đặt `PGCLIENTENCODING=UTF8`** (psql trên Windows mặc định WIN1252).

```powershell
cd C:\Setup\procurement-0.21.0
$env:PGCLIENTENCODING = 'UTF8'
Get-ChildItem .\db-scripts\*.sql | Sort-Object { [version]$_.BaseName } | ForEach-Object {
  & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U proc -d procurement -v ON_ERROR_STOP=1 -f $_.FullName
  if ($LASTEXITCODE -ne 0) { throw "Lỗi ở $($_.Name)" }
}
```

### B.6 Chép website, tạo thư mục dữ liệu

```powershell
New-Item -ItemType Directory -Force D:\Procurement, D:\Procurement\tools, D:\ProcurementData\attachments, D:\ProcurementData\logs | Out-Null
Copy-Item -Recurse .\app D:\Procurement\app
Copy-Item .\tools\backup.ps1 D:\Procurement\tools\
Copy-Item .\appsettings.Production.example.json D:\Procurement\app\appsettings.Production.json
```

### B.7 Cấu hình `D:\Procurement\app\appsettings.Production.json`

| Khóa | Đặt |
| --- | --- |
| `ConnectionStrings:Procurement` | `Host=localhost;Port=5432;Database=procurement;Username=proc;Password=<mật khẩu CSDL>` |
| `Jwt:SigningKey` | Chuỗi ngẫu nhiên ≥ 32 ký tự (lệnh dưới bảng). Đổi khóa thì mọi người phải đăng nhập lại |
| `Database:MigrateOnStartup` | Giữ **false** – CSDL chỉ nâng bằng script sau khi đã sao lưu |
| `Seed:AdminUserName`, `Seed:AdminPassword` | Tài khoản quản trị tạo lần đầu (đã có thì không đổi). **Đổi mật khẩu ngay** sau lần đăng nhập đầu |
| `Seed:HeadOffice` | Mã, tên tổng công ty và kho tổng (mã công ty nằm trong số chứng từ, chọn ngắn: ví dụ `TCT`) |
| `Attachments:RootPath` | `D:\\ProcurementData\\attachments` (JSON viết `\\`) |
| `Attachments:MaxFileSizeMb` | Mặc định 2 (cho phép 1–25) |
| `Serilog:WriteTo` → `File` → `path` | `D:\\ProcurementData\\logs\\proc-.log` |
| `Printing:FontDirectories` | Để trống (Windows có Arial tiếng Việt). Server Core không có phông thì khai thư mục chứa `.ttf` |

Sinh khóa JWT:

```powershell
$bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes); [Convert]::ToBase64String($bytes)
```

Mọi khóa cũng đặt được bằng biến môi trường (thay `:` bằng `__`, ví dụ `ConnectionStrings__Procurement`).

### B.8 Tạo App Pool, website, phân quyền thư mục

```powershell
Import-Module WebAdministration
New-WebAppPool -Name Procurement
Set-ItemProperty IIS:\AppPools\Procurement -Name managedRuntimeVersion -Value ''        # No Managed Code
Set-ItemProperty IIS:\AppPools\Procurement -Name startMode -Value AlwaysRunning
Set-ItemProperty IIS:\AppPools\Procurement -Name processModel.idleTimeout -Value '00:00:00'
New-Website -Name Procurement -PhysicalPath D:\Procurement\app -ApplicationPool Procurement -Port 80 -HostHeader ''

icacls D:\Procurement\app /grant "IIS AppPool\Procurement:(OI)(CI)RX"
icacls D:\ProcurementData /grant "IIS AppPool\Procurement:(OI)(CI)M"
icacls D:\Procurement\app\appsettings.Production.json /inheritance:r /grant "Administrators:F" "IIS AppPool\Procurement:R"
```

- "Default Web Site" đang dùng cổng 80 thì dừng nó (`Stop-Website 'Default Web Site'`) hoặc đổi binding.
- **HTTPS**: IIS Manager → Sites → Procurement → Bindings → Add → https, cổng 443, chọn chứng chỉ của đơn vị; sau đó bỏ binding 80 hoặc
  chuyển hướng sang HTTPS.
- Mở tường lửa cho máy trong mạng: `New-NetFirewallRule -DisplayName "Procurement HTTP" -Direction Inbound -Protocol TCP -LocalPort 80,443 -Action Allow -Profile Domain,Private`.

`web.config` trong gói đã gỡ WebDAV (tránh lỗi 405 khi sửa/xóa) và cho phép tải lên tới 30 MB.

### B.9 Kiểm tra

1. Mở `http://<máy chủ>/health` → `"status":"Healthy"`, `"version":"0.21.0"`.
2. Mở `http://<máy chủ>`, đăng nhập tài khoản quản trị ở B.7 → **đổi mật khẩu ngay**.
3. Xem `D:\ProcurementData\logs\proc-<ngày>.log`: không có dòng `[ERR]`. Có dòng
   `[WRN] Thư mục file đính kèm … nằm trong thư mục ứng dụng` nghĩa là quên đặt `Attachments:RootPath` ra ngoài.
4. Làm các việc khởi đầu của quản trị (Hướng dẫn sử dụng – mục 8.10).

### B.10 Sao lưu hằng đêm

1. Tạo tài khoản Windows chạy sao lưu (ví dụ `svc-backup`), đăng nhập một lần bằng tài khoản đó rồi tạo file
   `%APPDATA%\postgresql\pgpass.conf` một dòng: `localhost:5432:procurement:proc:<mật khẩu CSDL>`; chỉ cho tài khoản đó đọc file.
2. Tạo tác vụ 1 giờ sáng:

```powershell
$action  = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File D:\Procurement\tools\backup.ps1 -BackupDir E:\ProcurementBackup -AttachmentsDir D:\ProcurementData\attachments'
$trigger = New-ScheduledTaskTrigger -Daily -At 1:00
Register-ScheduledTask -TaskName 'Procurement backup' -Action $action -Trigger $trigger -User '<máy>\svc-backup' -Password '<mật khẩu>' -RunLevel Highest
```

3. Chạy thử: `Start-ScheduledTask 'Procurement backup'`, kiểm tra có file `.dump` mới trong `E:\ProcurementBackup` và `backup.log`.
4. Chép `E:\ProcurementBackup` ra máy khác định kỳ. Tác vụ báo "Last Run Result" khác 0 khi lỗi.

`backup.ps1`: `pg_dump` CSDL (giữ 14 ngày) + chép bổ sung file đính kèm vào `<BackupDir>\attachments`.

---

## Phần C – Ubuntu + Docker (chạy thật)

Một `docker-compose.yml` gồm 5 dịch vụ:

| Dịch vụ | Image | Vai trò |
| --- | --- | --- |
| `proxy` | caddy:2 | Cổng 80/443, tự cấp HTTPS khi có tên miền |
| `app` | proc-app:<APP_VERSION> | API + giao diện (một image) |
| `db` | postgres:18 | CSDL (volume `pg_data`) |
| `seq` | datalust/seq | Xem log kỹ thuật (chỉ mở ở 127.0.0.1:5341) |
| `backup` | postgres:18 | `pg_dump` mỗi đêm vào `./backups`, giữ 14 ngày; chép bổ sung file đính kèm |

### C.1 Chuẩn bị máy chủ

- Ubuntu 22.04/24.04 LTS, 2 CPU, 4 GB RAM trở lên.
- Cài Docker Engine + Compose plugin (https://docs.docker.com/engine/install/ubuntu/).
- Tường lửa chỉ mở 22, 80, 443: `sudo ufw allow 22,80,443/tcp && sudo ufw enable`.

### C.2 Lấy mã nguồn và cấu hình

```bash
git clone https://github.com/dangvinhlap-dev/procurement-system.git
cd procurement-system
git checkout v0.21.0
cp .env.example .env
nano .env
```

Sửa trong `.env`:

| Biến | Đặt |
| --- | --- |
| `APP_VERSION` | `0.21.0` |
| `SITE_ADDRESS` | Tên miền (ví dụ `muahang.congty.vn` – Caddy tự lấy HTTPS) hoặc `:80` trong mạng nội bộ |
| `POSTGRES_PASSWORD` | Mật khẩu CSDL mạnh |
| `JWT_SIGNING_KEY` | `openssl rand -base64 48` |
| `ADMIN_USERNAME`, `ADMIN_PASSWORD` | Tài khoản quản trị tạo lần đầu |
| `HEAD_OFFICE_CODE`, `HEAD_OFFICE_NAME` | Mã, tên tổng công ty |
| `SEQ_ADMIN_PASSWORD` | Mật khẩu Seq |
| `BACKUP_TIME`, `BACKUP_KEEP_DAYS` | Giờ sao lưu, số ngày giữ |
| `ATTACHMENT_MAX_MB` (thêm nếu cần) | Giới hạn file đính kèm, mặc định 2 |

### C.3 Build, tạo bảng, chạy

```bash
docker compose build app
docker compose up -d db
# Chạy lần lượt mọi script theo thứ tự phiên bản
for f in $(ls db/scripts/*.sql | sort -V); do
  docker compose exec -T db psql -U proc -d procurement -v ON_ERROR_STOP=1 -f /db/scripts/$(basename $f) || break
done
docker compose up -d
docker compose ps
```

Mở `https://<tên miền>/health` (hoặc `http://<IP>/health`), rồi đăng nhập và **đổi mật khẩu quản trị ngay**.

### C.4 Xem log

`ssh -L 5341:localhost:5341 <máy chủ>` rồi mở http://localhost:5341 (tài khoản `admin`, mật khẩu `SEQ_ADMIN_PASSWORD`). Hoặc
`docker compose logs -f app`.

### C.5 Sao lưu

- Container `backup` tự sao lưu mỗi đêm vào `./backups` (giữ 14 ngày) và chép file đính kèm vào `./backups/attachments`.
- Sao lưu tay: `docker compose exec backup sh -c 'pg_dump -Fc -f /backups/tay-$(date +%F).dump'`.
- Chép thư mục `backups/` ra máy khác định kỳ (rsync, ổ mạng).

---

## Phần D – Bản test trong mạng nội bộ (có dữ liệu thử)

Dùng cho người dùng thử nghiệm, **không dùng CSDL thật**.

1. Tạo CSDL test riêng (ví dụ `procurement_uat`), chạy các script như B.5.
2. Giải nén gói cài vào ví dụ `D:\ProcurementTest`, tạo `app\appsettings.Production.json` như B.7 nhưng trỏ CSDL test; mục `Seed` lấy mẫu trong
   `tools\du-lieu-thu\README.md` (bước 1 – có sẵn các công ty, kho mẫu).
3. Chạy thẳng không qua IIS, nghe mọi địa chỉ ở một cổng, mở tường lửa (PowerShell Administrator):

```powershell
cd D:\ProcurementTest
$env:ASPNETCORE_ENVIRONMENT = 'Production'
.\app\Proc.Api.exe --urls http://0.0.0.0:5006
New-NetFirewallRule -DisplayName "Procurement test 5006" -Direction Inbound -Protocol TCP -LocalPort 5006 -Action Allow -Profile Private,Domain
```

4. Tạo dữ liệu thử (cần Node.js 22): 

```powershell
node .\tools\du-lieu-thu\tao-du-lieu-thu.mjs --url http://<IP máy chủ>:5006 --user admin --password <mật khẩu admin> --out ket-qua.json
```

   Tạo bộ phận, vai trò mẫu, 38 tài khoản test (mật khẩu `Test@2026`), NCC, vật tư, quy trình duyệt… Chạy lại không tạo trùng.
   Kịch bản test: `docs\DuLieuThu_KichBanTest_0.19.0.docx`.
5. Máy khác mở `http://<IP máy chủ>:5006`.

---

## Phần E – Sao lưu, khôi phục, nâng cấp, quay lại bản cũ

### E.1 Nâng cấp lên bản mới

**Luôn đọc `CHANGELOG.md` mục "Lưu ý khi nâng cấp" của bản mới trước.**

Windows/IIS:

1. **Sao lưu**: `Start-ScheduledTask 'Procurement backup'`, kiểm tra có file `.dump` mới.
2. Tạm dừng website: chép file `app_offline.htm` (nội dung "Đang nâng cấp, vui lòng quay lại sau ít phút") vào `D:\Procurement\app`.
3. Nếu bản mới có thay đổi CSDL: chạy các script trong `db-scripts` **mới hơn** bản đang chạy (cách chạy như B.5, chỉ các file mới).
4. Chép đè nội dung `app\` của gói mới vào `D:\Procurement\app` (**giữ** `appsettings.Production.json` – gói không chứa file này).
5. Xóa `app_offline.htm`, mở `/health` kiểm tra số phiên bản mới.
6. Gán các quyền mới (nếu CHANGELOG nêu) cho vai trò đang dùng. Người dùng tải lại trang bằng **Ctrl+F5**.

Docker:

```bash
docker compose exec backup sh -c 'pg_dump -Fc -f /backups/truoc-nang-cap.dump'
git fetch --tags && git checkout v<bản mới>
nano .env                      # đổi APP_VERSION
docker compose build app
# nếu có script CSDL mới: docker compose exec -T db psql -U proc -d procurement -v ON_ERROR_STOP=1 -f /db/scripts/<bản mới>.sql
docker compose up -d
```

### E.2 Khôi phục từ bản sao lưu

Windows:

```powershell
# Dừng website (app_offline.htm như E.1 bước 2), rồi:
$env:PGCLIENTENCODING = 'UTF8'
& "C:\Program Files\PostgreSQL\18\bin\pg_restore.exe" -h localhost -U proc -d procurement --clean --if-exists E:\ProcurementBackup\<file>.dump
robocopy E:\ProcurementBackup\attachments D:\ProcurementData\attachments /E /XC /XN /XO
# Xóa app_offline.htm
```

Docker:

```bash
docker compose stop app
docker compose exec backup pg_restore --clean --if-exists -d procurement /backups/<file>.dump
docker compose cp backups/attachments/. app:/data/attachments
docker compose up -d
```

### E.3 Quay lại phiên bản cũ

Không xóa lịch sử; luôn sao lưu trước.

- **Cách 1 (an toàn)**: khôi phục bản sao lưu chụp **trước khi nâng cấp** (E.2), rồi cài lại gói của tag cũ (Windows: chép lại `app\` của gói
  cũ; Docker: đổi `APP_VERSION` về bản cũ, `docker compose up -d`).
- **Cách 2**: gỡ migration về bản cũ bằng `dotnet ef database update <migration của bản cũ>` – CHANGELOG mỗi bản ghi tên migration và **dữ
  liệu sẽ mất**. Chỉ làm khi đã sao lưu.

Bản không đổi CSDL (ví dụ 0.13.1, 0.14.0, 0.16.0) chỉ cần cài lại gói cũ.

---

## Phần F – Bảng cấu hình

Đặt trong `appsettings.Production.json` (Windows) hoặc biến môi trường (Docker; thay `:` bằng `__`).

| Khóa | Ý nghĩa | Mặc định |
| --- | --- | --- |
| `ConnectionStrings:Procurement` | Chuỗi kết nối PostgreSQL | (bắt buộc) |
| `Jwt:SigningKey` | Khóa ký phiên đăng nhập, ≥ 32 ký tự | (bắt buộc) |
| `Database:MigrateOnStartup` | Tự nâng CSDL khi khởi động – **chỉ bật ở dev/test** | false |
| `Seed:Enabled` | Tạo tài khoản quản trị, tổng công ty lần đầu | true |
| `Seed:AdminUserName` / `AdminFullName` / `AdminPassword` | Tài khoản quản trị ban đầu | – |
| `Seed:HeadOffice` | Mã, tên, kho của tổng công ty | – |
| `Seed:DemoCompanies` | Công ty mẫu (chỉ môi trường test) | – |
| `Attachments:RootPath` | Thư mục lưu file đính kèm (để **ngoài** thư mục ứng dụng) | Docker: `/data/attachments` |
| `Attachments:MaxFileSizeMb` | Giới hạn mỗi file (1–25 MB) | 2 |
| `Printing:FontDirectories` | Thư mục phông `.ttf` có tiếng Việt cho in PDF (Arial, DejaVu Sans, Liberation Sans) | tự tìm |
| `Serilog:WriteTo` | Nơi ghi log: Console, File, Seq | Console |
| `ASPNETCORE_ENVIRONMENT` | `Production` / `Development` / `Testing` | Production |

---

## Phần G – Xử lý sự cố

| Hiện tượng | Nguyên nhân / cách xử lý |
| --- | --- |
| HTTP 500.19 | Chưa cài Hosting Bundle hoặc cài trước IIS → chạy lại bộ cài, chọn Repair, `iisreset` |
| HTTP 500.30 / 500.31 | Ứng dụng không khởi động: xem log ở `D:\ProcurementData\logs`. Chưa có log: sửa `web.config` tạm `stdoutLogEnabled="true"`, tạo `D:\Procurement\app\logs` (App Pool quyền ghi), xem `stdout_*.log`, xong đặt lại `false` |
| Log báo "Thiếu chuỗi kết nối" | Sai tên file `appsettings.Production.json`, hoặc `ASPNETCORE_ENVIRONMENT` khác `Production` |
| HTTP 405 khi lưu/xóa | WebDAV chưa gỡ – kiểm tra `web.config` còn `<remove name="WebDAVModule" />` |
| HTTP 413 khi tải file | File vượt `Attachments:MaxFileSizeMb` |
| Không lưu được đính kèm, không có log | App Pool thiếu quyền Modify trên `D:\ProcurementData` |
| Không kết nối được CSDL | Dịch vụ `postgresql-x64-18` chưa chạy; sai mật khẩu; CSDL ở máy khác thì mở `listen_addresses` trong `postgresql.conf`, thêm IP máy web vào `pg_hba.conf`, mở cổng 5432 chỉ cho máy web |
| Script SQL báo lỗi ký tự ở dòng đầu / lỗi mã hóa | Chưa đặt `$env:PGCLIENTENCODING='UTF8'` |
| Máy khác không mở được bản test | Chạy với `--urls http://0.0.0.0:<cổng>` và mở cổng trên tường lửa (Phần D) |
| In phiếu báo thiếu phông | Chép phông `.ttf` có tiếng Việt vào một thư mục, khai ở `Printing:FontDirectories` |
| Sau nâng cấp giao diện lỗi lạ | Người dùng tải lại bằng Ctrl+F5 |
| Dev: `dotnet build` báo khóa DLL (MSB3027) | API đang chạy giữ file – dừng API rồi build (xem `docs\moi-truong-dev.md`) |
