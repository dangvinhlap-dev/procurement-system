<#
.SYNOPSIS
  Sao lưu CSDL PostgreSQL và file đính kèm trên Windows Server – tương đương container "backup" của Docker (db/backup.sh,
  design.md mục 14.5, 18.4). Chạy mỗi đêm bằng Task Scheduler (docs/deploy-windows.md).

.DESCRIPTION
  1. pg_dump định dạng custom ra <BackupDir>\<Database>_yyyyMMdd_HHmmss.dump (ghi file .part rồi mới đổi tên: bản dở dang không bị
     nhầm là bản tốt); xóa bản cũ hơn KeepDays ngày.
  2. Chép bổ sung thư mục đính kèm vào <BackupDir>\attachments bằng robocopy, chỉ file mới: file đính kèm chỉ thêm, không bao giờ sửa
     hay xóa trên đĩa (xóa là xóa mềm trong CSDL) nên không cần chép lại toàn bộ.
  Mật khẩu CSDL không ghi trong script: đặt trong %APPDATA%\postgresql\pgpass.conf của tài khoản chạy tác vụ, hoặc biến PGPASSWORD.
  Kết quả ghi thêm vào <BackupDir>\backup.log; lỗi thì thoát mã 1 để Task Scheduler báo "Last Run Result" khác 0.

.EXAMPLE
  .\backup.ps1 -BackupDir E:\ProcurementBackup -AttachmentsDir D:\ProcurementData\attachments
#>
param(
    [Parameter(Mandatory)][string]$BackupDir,
    [string]$AttachmentsDir,
    [string]$Database = 'procurement',
    [string]$DbHost = 'localhost',
    [int]$Port = 5432,
    [string]$User = 'proc',
    [int]$KeepDays = 14,
    # Thư mục bin của PostgreSQL; bỏ trống thì tìm pg_dump trong PATH rồi trong C:\Program Files\PostgreSQL\<bản mới nhất>\bin.
    [string]$PgBin
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $BackupDir | Out-Null
$log = Join-Path $BackupDir 'backup.log'
function Write-Log([string]$message) {
    $line = "$(Get-Date -Format 'dd/MM/yyyy HH:mm') $message"
    Write-Host $line
    Add-Content -Path $log -Value $line -Encoding UTF8
}

function Find-PgDump {
    if ($PgBin) { return Join-Path $PgBin 'pg_dump.exe' }
    $inPath = Get-Command pg_dump.exe -ErrorAction SilentlyContinue
    if ($inPath) { return $inPath.Source }
    $root = Join-Path $env:ProgramFiles 'PostgreSQL'
    $latest = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [int]($_.Name -replace '\D', '') } -Descending | Select-Object -First 1
    if ($latest) { return Join-Path $latest.FullName 'bin\pg_dump.exe' }
    throw 'Không tìm thấy pg_dump.exe – truyền -PgBin "C:\Program Files\PostgreSQL\18\bin".'
}

$failed = $false

# 1. CSDL
$file = Join-Path $BackupDir ("{0}_{1}.dump" -f $Database, (Get-Date -Format 'yyyyMMdd_HHmmss'))
$part = "$file.part"
& (Find-PgDump) --host=$DbHost --port=$Port --username=$User --no-password --format=custom --file=$part $Database
if ($LASTEXITCODE -eq 0) {
    Move-Item $part $file
    Write-Log "Đã sao lưu $file"
}
else {
    Remove-Item $part -ErrorAction SilentlyContinue
    Write-Log "LỖI sao lưu CSDL $Database (pg_dump mã $LASTEXITCODE)"
    $failed = $true
}
Get-ChildItem $BackupDir -Filter "${Database}_*.dump" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } | Remove-Item -Force

# 2. File đính kèm
if ($AttachmentsDir) {
    $target = Join-Path $BackupDir 'attachments'
    # /E: cả thư mục con; /XC /XN /XO: bỏ qua file đã có (không ghi đè); /R:2 /W:5: thử lại ít khi file đang bị khóa.
    robocopy $AttachmentsDir $target /E /XC /XN /XO /R:2 /W:5 /NP /NFL /NDL | Out-Null
    # robocopy: mã < 8 là thành công (0 = không có gì mới, 1 = có chép file mới…).
    if ($LASTEXITCODE -lt 8) { Write-Log "Đã chép file đính kèm vào $target" }
    else { Write-Log "LỖI chép file đính kèm (robocopy mã $LASTEXITCODE)"; $failed = $true }
}

if ($failed) { exit 1 }
exit 0
