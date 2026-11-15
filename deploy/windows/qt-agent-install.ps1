<#
.SYNOPSIS
  Cài bộ thực thi Quản trị vận hành (lát QT5 – docs/ke-hoach-qt.md QT5, docs/deploy-windows.md mục 4d) lên một máy chủ Windows: chép
  qt-agent.ps1, tạo file cấu hình có khóa, đăng ký tác vụ Task Scheduler "Procurement QT agent" chạy mỗi phút. Chạy bằng PowerShell
  "Run as Administrator", trên TEST trước, PROD sau.

.DESCRIPTION
  1. Chép qt-agent.ps1 (cùng thư mục với script này) vào -InstallPath.
  2. Tạo -StatePath (nhật ký agent-journal.log, kết quả treo, khóa chạy) – nên ở ổ dữ liệu, ngoài thư mục website.
  3. Ghi <InstallPath>\qt-agent.json: ServerUrl, AgentKey, AppPath, StatePath. File chứa khóa nên tắt kế thừa quyền, chỉ Administrators,
     SYSTEM (toàn quyền) và tài khoản bộ thực thi (đọc) mở được.
  4. Cấp quyền Sửa cho tài khoản bộ thực thi trên -StatePath và các thư mục -GrantModify (thư mục website, sao lưu – các lệnh cài bản, khôi
     phục của QT6 / QT8 cần; lệnh Ping của QT5 không cần).
  5. Đăng ký tác vụ: chạy mỗi phút, tài khoản -TaskUser (chạy cả khi không đăng nhập, quyền cao nhất), không chạy chồng (IgnoreNew).
  6. Chạy thử qt-agent.ps1 -DryRun để kiểm tra kết nối và khóa.
  Chạy lại nhiều lần được (đổi khóa, đổi địa chỉ): tác vụ cũ được thay. -Uninstall gỡ tác vụ (giữ file nhật ký, cấu hình – xóa tay nếu muốn).

  Khóa: trên màn hình AGT (Quản trị hệ thống → Bộ thực thi) chọn môi trường → "Tạo khóa…" → chép khóa (chỉ hiện một lần). Không truyền
  -AgentKey thì script hỏi (gõ không hiện chữ).
  Tài khoản -TaskUser: tạo riêng (ví dụ .\proc-agent), mật khẩu mạnh, không dùng tài khoản người. QT5 chỉ cần quyền đọc file cấu hình + sửa
  StatePath; từ QT6 / QT8 cần thêm quyền sửa thư mục website / sao lưu (-GrantModify) và điều khiển dịch vụ PostgreSQL, IIS – xem deploy-windows.md.

.EXAMPLE
  .\qt-agent-install.ps1 -ServerUrl https://procurement.congty.local -TaskUser .\proc-agent -AppPath C:\inetpub\procurement `
      -StatePath D:\ProcurementData\agent
  .\qt-agent-install.ps1 -ServerUrl https://procurement.congty.local -TaskUser .\proc-agent -DryRun    # chỉ in các việc sẽ làm
  .\qt-agent-install.ps1 -Uninstall
#>
param(
    [string]$ServerUrl,
    [SecureString]$AgentKey,
    [string]$TaskUser,
    [SecureString]$TaskPassword,
    [string]$InstallPath = 'C:\ProcurementAgent',
    [string]$StatePath = 'D:\ProcurementData\agent',
    [string]$AppPath = 'C:\inetpub\procurement',
    [string[]]$GrantModify = @(),
    [string]$TaskName = 'Procurement QT agent',
    [switch]$Uninstall,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Step([string]$Text) { Write-Host "-> $Text" }

function ConvertTo-Plain([SecureString]$Secure) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin -and -not $DryRun) { throw 'Chạy PowerShell bằng "Run as Administrator".' }

if ($Uninstall) {
    Step "Gỡ tác vụ '$TaskName' (giữ $InstallPath, $StatePath – xóa tay nếu muốn)."
    if (-not $DryRun -and (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue)) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    }
    return
}

if (-not $ServerUrl) { throw 'Truyền -ServerUrl, ví dụ https://procurement.congty.local (máy chủ có màn hình AGT – PROD, hoặc chính máy này khi chỉ có một môi trường).' }
if (-not ($ServerUrl -match '^https?://')) { throw '-ServerUrl phải bắt đầu bằng http:// hoặc https://.' }
if (-not $TaskUser) { throw 'Truyền -TaskUser – tài khoản Windows riêng cho bộ thực thi, ví dụ .\proc-agent.' }
$source = Join-Path $PSScriptRoot 'qt-agent.ps1'
if (-not (Test-Path -LiteralPath $source)) { throw "Không thấy $source – chạy script này từ thư mục deploy\windows của gói cài." }

$configPath = Join-Path $InstallPath 'qt-agent.json'
$target = Join-Path $InstallPath 'qt-agent.ps1'
Step "Chép qt-agent.ps1 vào $InstallPath."
Step "Tạo thư mục trạng thái $StatePath, cấp quyền Sửa cho $TaskUser."
Step "Ghi $configPath (ServerUrl $ServerUrl, AppPath $AppPath) – chỉ Administrators, SYSTEM, $TaskUser đọc được."
foreach ($path in $GrantModify) { Step "Cấp quyền Sửa thư mục $path cho $TaskUser." }
Step "Đăng ký tác vụ '$TaskName' chạy mỗi phút bằng $TaskUser."
if ($DryRun) { Write-Host 'Chạy thử (-DryRun) – không đổi gì.'; return }

if (-not $AgentKey) { $AgentKey = Read-Host -AsSecureString 'Khóa bộ thực thi (qta_…, chép từ màn hình AGT)' }
$key = (ConvertTo-Plain $AgentKey).Trim()
if (-not $key.StartsWith('qta_')) { throw 'Khóa bộ thực thi phải bắt đầu bằng qta_.' }
if (-not $TaskPassword) { $TaskPassword = Read-Host -AsSecureString "Mật khẩu tài khoản $TaskUser" }

$null = New-Item -ItemType Directory -Force -Path $InstallPath
$null = New-Item -ItemType Directory -Force -Path $StatePath
Copy-Item -LiteralPath $source -Destination $target -Force

$config = [ordered]@{ ServerUrl = $ServerUrl.TrimEnd('/'); AgentKey = $key; AppPath = $AppPath; StatePath = $StatePath }
[IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
# File có khóa: bỏ kế thừa, chỉ quản trị máy, SYSTEM, tài khoản bộ thực thi.
& icacls $configPath /inheritance:r /grant:r '*S-1-5-32-544:(F)' '*S-1-5-18:(F)' "${TaskUser}:(R)" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls không đặt được quyền cho $configPath." }
foreach ($path in @($StatePath) + $GrantModify) {
    & icacls $path /grant "${TaskUser}:(OI)(CI)M" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls không cấp được quyền Sửa thư mục $path." }
}

$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$target`" -ConfigPath `"$configPath`""
# Lặp mỗi phút trong 10 năm (bản Windows cũ không nhận "vô hạn" qua TimeSpan.MaxValue).
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1) `
    -RepetitionDuration (New-TimeSpan -Days 3650)
# IgnoreNew: lần trước chưa xong thì bỏ lần mới (script cũng tự giữ khóa file); giới hạn 12 giờ cho lệnh dài (khôi phục CSDL lớn – QT6).
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 12) `
    -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) { Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false }
$null = Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest `
    -User $TaskUser -Password (ConvertTo-Plain $TaskPassword) `
    -Description 'Bộ thực thi Quản trị vận hành phần mềm mua hàng – hỏi lệnh mỗi phút (docs/deploy-windows.md mục 4d).'

Step 'Chạy thử kết nối (-DryRun):'
& $target -ConfigPath $configPath -DryRun
Write-Host "Xong. Màn hình AGT sẽ báo 'Đang liên lạc' sau 1–2 phút; bấm 'Lệnh thử' để kiểm tra bộ thực thi chạy lệnh được."
