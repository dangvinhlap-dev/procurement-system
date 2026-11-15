<#
.SYNOPSIS
  Bật lưu WAL liên tục cho PostgreSQL trên Windows Server (module Quản trị vận hành – lát QT3a, design.md mục 21.5,
  docs/ke-hoach-qt.md). Chạy MỘT LẦN trên máy chủ CSDL (TEST trước, PROD sau), bằng PowerShell "Run as Administrator".

.DESCRIPTION
  1. Tạo thư mục WAL (nên ở ổ khác ổ dữ liệu CSDL), cấp quyền sửa cho tài khoản chạy dịch vụ PostgreSQL.
  2. ALTER SYSTEM (ghi postgresql.auto.conf, không sửa tay postgresql.conf):
       wal_level = replica, archive_mode = on, archive_timeout = 300 (mất tối đa 5 phút dữ liệu),
       summarize_wal = on, wal_summary_keep_time = 35d (sao lưu Diff của lát QT3b cần tóm tắt WAL từ bản Full gốc – Full mỗi tuần),
       archive_command: KHÔNG ghi đè file đã có; chép ra <file>.tmp rồi đổi tên để file dở dang (mất điện giữa chừng) không bị coi là đủ.
  3. Tạo / cập nhật vai trò bảo trì proc_maint: LOGIN REPLICATION CREATEDB + pg_monitor, pg_read_all_settings, pg_read_all_data,
     pg_signal_backend (pg_basebackup, pg_dump, khôi phục ra CSDL tạm ở các lát sau; ngắt phiên treo trên màn hình DBM – lát QT4). Mật khẩu hỏi khi chạy, không ghi ra file / log.
  4. Thêm dòng pg_hba.conf cho proc_maint từ 127.0.0.1 / ::1 (kết nối replication của pg_basebackup) – sao lưu file trước khi sửa.
  5. Nạp lại cấu hình. wal_level, archive_mode chỉ có hiệu lực sau khi KHỞI ĐỘNG LẠI PostgreSQL MỘT LẦN: truyền -Restart hoặc tự khởi động
     lại dịch vụ vào giờ vắng.
  Chạy lại nhiều lần không sao (mọi bước kiểm tra trước khi thêm). Sau khi chạy: khai ConnectionStrings:Maintenance, Backup:WalArchivePath
  trong appsettings.Production.json, mở màn hình BAK thấy "Đang chạy" (docs/deploy-windows.md).
  Mật khẩu siêu quản trị (postgres): lấy từ biến PGPASSWORD nếu có, không thì hỏi một lần.

.EXAMPLE
  .\qt-enable-wal.ps1 -WalArchivePath E:\ProcurementBackup\wal
  .\qt-enable-wal.ps1 -WalArchivePath E:\ProcurementBackup\wal -Restart
  .\qt-enable-wal.ps1 -WalArchivePath E:\ProcurementBackup\wal -DryRun      # chỉ in các việc sẽ làm, không đổi gì
#>
param(
    [Parameter(Mandatory)][string]$WalArchivePath,
    [string]$DbHost = 'localhost',
    [int]$Port = 5432,
    [string]$SuperUser = 'postgres',
    [string]$Database = 'postgres',
    [string]$MaintUser = 'proc_maint',
    [SecureString]$MaintPassword,
    # Giữ tóm tắt WAL: phải dài hơn chu kỳ Full (7 ngày) – mặc định 35 ngày cho dư.
    [string]$SummaryKeepTime = '35d',
    # Tên dịch vụ Windows của PostgreSQL; bỏ trống thì tìm dịch vụ tên "postgresql*" (phải có đúng một).
    [string]$ServiceName,
    # Thư mục bin của PostgreSQL; bỏ trống thì tìm psql trong PATH rồi trong C:\Program Files\PostgreSQL\<bản mới nhất>\bin.
    [string]$PgBin,
    [switch]$Restart,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Find-Psql {
    if ($PgBin) { return Join-Path $PgBin 'psql.exe' }
    $inPath = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($inPath) { return $inPath.Source }
    $root = Join-Path $env:ProgramFiles 'PostgreSQL'
    $latest = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [int]($_.Name -replace '\D', '') } -Descending | Select-Object -First 1
    if ($latest) { return Join-Path $latest.FullName 'bin\psql.exe' }
    throw 'Không tìm thấy psql.exe – truyền -PgBin "C:\Program Files\PostgreSQL\18\bin".'
}

function ConvertTo-Plain([SecureString]$secure) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

# Chuỗi SQL trong nháy đơn: nháy đơn nhân đôi.
function Quote-Sql([string]$text) { return "'" + $text.Replace("'", "''") + "'" }

# --- 1. Kiểm tra tham số --------------------------------------------------------------------------------------------------------
if (-not [IO.Path]::IsPathRooted($WalArchivePath)) { throw "-WalArchivePath phải là đường dẫn đầy đủ, ví dụ E:\ProcurementBackup\wal." }
$WalArchivePath = [IO.Path]::GetFullPath($WalArchivePath).TrimEnd('\')
# Dấu " và % làm hỏng lệnh cmd của archive_command (%f, %p là chỗ PostgreSQL thay tên file).
if ($WalArchivePath -match '["%]') { throw 'Đường dẫn thư mục WAL không được chứa dấu " hoặc %.' }
if ($MaintUser -notmatch '^[a-z_][a-z0-9_]*$') { throw "-MaintUser chỉ gồm chữ thường không dấu, số, gạch dưới." }

# Join-Path của PowerShell 5.1 đòi ổ đĩa phải tồn tại (chạy thử trên máy khác) – dùng Path.Combine.
$target = [IO.Path]::Combine($WalArchivePath, '%f')
$archiveCommand = "if not exist `"$target`" (copy /y `"%p`" `"$target.tmp`" >nul && move /y `"$target.tmp`" `"$target`" >nul)"
$hbaLines = @(
    "# QT3a - $MaintUser (pg_basebackup, pg_dump) - added by qt-enable-wal.ps1",
    "host    replication     $MaintUser      127.0.0.1/32            scram-sha-256",
    "host    replication     $MaintUser      ::1/128                 scram-sha-256",
    "host    all             $MaintUser      127.0.0.1/32            scram-sha-256",
    "host    all             $MaintUser      ::1/128                 scram-sha-256"
)
$settings = [ordered]@{
    wal_level             = 'replica'
    archive_mode          = 'on'
    archive_command       = $archiveCommand
    archive_timeout       = '300'
    summarize_wal         = 'on'
    wal_summary_keep_time = $SummaryKeepTime
}

if ($DryRun) {
    Write-Host '== Chạy thử (-DryRun): không đổi gì ==' -ForegroundColor Yellow
    Write-Host "Tạo thư mục $WalArchivePath, cấp quyền sửa cho tài khoản dịch vụ PostgreSQL."
    foreach ($k in $settings.Keys) { Write-Host "ALTER SYSTEM SET $k = $(Quote-Sql $settings[$k]);" }
    Write-Host "Tạo / cập nhật vai trò $MaintUser LOGIN REPLICATION CREATEDB; GRANT pg_monitor, pg_read_all_settings, pg_read_all_data, pg_signal_backend."
    Write-Host 'Thêm vào pg_hba.conf (đường dẫn lấy bằng SHOW hba_file) các dòng còn thiếu:'
    $hbaLines | ForEach-Object { Write-Host "  $_" }
    Write-Host 'SELECT pg_reload_conf(); rồi khởi động lại PostgreSQL (-Restart) để wal_level, archive_mode có hiệu lực.'
    return
}

$psql = Find-Psql
if (-not (Test-Path $psql)) { throw "Không thấy $psql." }

# --- 2. Dịch vụ PostgreSQL và thư mục WAL ---------------------------------------------------------------------------------------
$services = @(Get-CimInstance Win32_Service | Where-Object { $_.Name -like 'postgresql*' })
if ($ServiceName) { $services = @($services | Where-Object { $_.Name -eq $ServiceName }) }
if ($services.Count -ne 1) {
    throw "Không xác định được dịch vụ PostgreSQL (thấy $($services.Count)) – truyền -ServiceName, ví dụ postgresql-x64-18."
}
$service = $services[0]

New-Item -ItemType Directory -Force $WalArchivePath | Out-Null
$account = $service.StartName
if ($account -and $account -ne 'LocalSystem') {
    # (OI)(CI)M: quyền sửa cho thư mục, thư mục con và file – PostgreSQL ghi file .tmp rồi đổi tên.
    & icacls $WalArchivePath /grant "${account}:(OI)(CI)M" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls cấp quyền cho $account lỗi (mã $LASTEXITCODE)." }
    Write-Host "Đã cấp quyền sửa $WalArchivePath cho $account."
}

# --- 3. Mật khẩu ------------------------------------------------------------------------------------------------------------------
$hadPgPassword = [bool]$env:PGPASSWORD
if (-not $hadPgPassword) {
    $env:PGPASSWORD = ConvertTo-Plain (Read-Host "Mật khẩu $SuperUser của PostgreSQL" -AsSecureString)
}
if (-not $MaintPassword) {
    $MaintPassword = Read-Host "Đặt mật khẩu cho $MaintUser (ghi vào ConnectionStrings:Maintenance)" -AsSecureString
}
$maintPlain = ConvertTo-Plain $MaintPassword
if ($maintPlain.Length -lt 12) { throw "Mật khẩu $MaintUser tối thiểu 12 ký tự." }

function Invoke-Psql([string]$sql) {
    # SQL đi qua stdin (không nằm trên dòng lệnh – không lộ trong danh sách tiến trình); -X bỏ psqlrc; dừng ở lỗi đầu tiên.
    $output = $sql | & $psql -X -q -t -A -v ON_ERROR_STOP=1 -h $DbHost -p $Port -U $SuperUser -d $Database -f -
    if ($LASTEXITCODE -ne 0) { throw "psql lỗi (mã $LASTEXITCODE)." }
    return $output
}

try {
    $version = [int](Invoke-Psql 'SHOW server_version_num;')
    if ($version -lt 170000) { throw "Cần PostgreSQL 17 trở lên (summarize_wal); máy đang chạy $version." }
    $hbaFile = (Invoke-Psql 'SHOW hba_file;').Trim()

    # --- 4. Tham số và vai trò bảo trì -----------------------------------------------------------------------------------------
    $sql = New-Object System.Text.StringBuilder
    # Không để lệnh có mật khẩu vào log của PostgreSQL (log_statement = all/ddl ghi nguyên câu lệnh).
    [void]$sql.AppendLine("SET log_statement = 'none'; SET log_min_duration_statement = -1;")
    foreach ($k in $settings.Keys) { [void]$sql.AppendLine("ALTER SYSTEM SET $k = $(Quote-Sql $settings[$k]);") }
    [void]$sql.AppendLine("SELECT 'CREATE ROLE $MaintUser LOGIN' WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$MaintUser') \gexec")
    [void]$sql.AppendLine("ALTER ROLE $MaintUser WITH LOGIN REPLICATION CREATEDB PASSWORD $(Quote-Sql $maintPlain);")
    [void]$sql.AppendLine("GRANT pg_monitor, pg_read_all_settings, pg_read_all_data, pg_signal_backend TO $MaintUser;")
    Invoke-Psql $sql.ToString() | Out-Null
    Write-Host 'Đã đặt tham số lưu WAL (postgresql.auto.conf) và vai trò bảo trì.'

    # --- 5. pg_hba.conf ----------------------------------------------------------------------------------------------------------
    if (-not (Test-Path $hbaFile)) { throw "Không thấy $hbaFile – chạy script trên chính máy chủ CSDL." }
    $existing = Get-Content $hbaFile
    $normalized = $existing | ForEach-Object { ($_ -replace '\s+', ' ').Trim() }
    $missing = @($hbaLines | Select-Object -Skip 1 | Where-Object { $normalized -notcontains (($_ -replace '\s+', ' ').Trim()) })
    if ($missing.Count -gt 0) {
        $backup = "$hbaFile.$(Get-Date -Format 'yyyyMMddHHmmss').bak"
        Copy-Item $hbaFile $backup
        Add-Content -Path $hbaFile -Value (@('', $hbaLines[0]) + $missing) -Encoding ascii
        Write-Host "Đã thêm $($missing.Count) dòng vào $hbaFile (bản cũ: $backup)."
    }
    else { Write-Host 'pg_hba.conf đã có đủ dòng cho tài khoản bảo trì.' }

    Invoke-Psql 'SELECT pg_reload_conf();' | Out-Null
    $hbaErrors = Invoke-Psql 'SELECT count(*) FROM pg_hba_file_rules WHERE error IS NOT NULL;'
    if ([int]$hbaErrors -gt 0) { throw "pg_hba.conf có $hbaErrors dòng lỗi (xem pg_hba_file_rules) – sửa trước khi khởi động lại." }

    # --- 6. Khởi động lại ----------------------------------------------------------------------------------------------------
    if ($Restart) {
        Write-Host "Khởi động lại dịch vụ $($service.Name)…"
        Restart-Service -Name $service.Name -Force
        (Get-Service -Name $service.Name).WaitForStatus('Running', [TimeSpan]::FromMinutes(2))
        Start-Sleep -Seconds 3
        $mode = (Invoke-Psql 'SHOW archive_mode;').Trim()
        $level = (Invoke-Psql 'SHOW wal_level;').Trim()
        if ($mode -ne 'on' -or $level -ne 'replica') { throw "Sau khởi động lại: archive_mode = $mode, wal_level = $level – kiểm tra log PostgreSQL." }
        # Chép ngay một file để màn hình BAK thấy lần chép đầu tiên.
        Invoke-Psql 'SELECT pg_switch_wal();' | Out-Null
        Write-Host 'Lưu WAL đã chạy: archive_mode = on, wal_level = replica.' -ForegroundColor Green
    }
    else {
        Write-Host "CHƯA CÓ HIỆU LỰC: khởi động lại dịch vụ $($service.Name) một lần vào giờ vắng (hoặc chạy lại script với -Restart)." -ForegroundColor Yellow
    }
}
finally {
    if (-not $hadPgPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
    $maintPlain = $null
}

Write-Host ''
Write-Host 'Việc tiếp theo (docs/deploy-windows.md – Lưu WAL):'
Write-Host '  1. appsettings.Production.json:'
Write-Host "       `"ConnectionStrings`": { `"Maintenance`": `"Host=localhost;Port=$Port;Database=procurement;Username=$MaintUser;Password=<mật khẩu vừa đặt>`" }"
Write-Host "       `"Backup`": { `"WalArchivePath`": `"$($WalArchivePath.Replace('\', '\\'))`" }"
Write-Host '  2. Khởi động lại website, mở màn hình BAK (Sao lưu): Lưu WAL "Đang chạy".'
