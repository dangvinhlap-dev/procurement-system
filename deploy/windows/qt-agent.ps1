<#
.SYNOPSIS
  Bộ thực thi của module Quản trị vận hành (lát QT5 – design.md mục 21.2 N1, docs/ke-hoach-qt.md QT5). Chạy mỗi phút bằng Task Scheduler
  (cài bằng qt-agent-install.ps1) với tài khoản riêng có quyền cao trên máy: hỏi máy chủ phần mềm lấy lệnh, chạy lệnh, báo kết quả.

.DESCRIPTION
  Ứng dụng web chạy tài khoản ít quyền nên không tự cài bản mới, không tự dừng PostgreSQL (N1) – việc đó do bộ thực thi làm theo lệnh. Bộ thực
  thi CHỦ ĐỘNG hỏi máy chủ (không mở cổng vào máy này), xác thực bằng khóa riêng (header X-Ops-Agent-Key, tạo trên màn hình AGT).

  Mỗi lần chạy:
  1. Giữ khóa file <StatePath>\qt-agent.lock – lần chạy trước chưa xong thì thoát ngay (không chạy chồng).
  2. Gửi lại kết quả còn treo trong <StatePath>\pending (lần trước mất mạng / máy chủ đang tắt sau khi chạy lệnh).
  3. GET /api/ops/v1/agent/commands: 204 = không có việc; 200 = một lệnh (máy chủ đã đánh dấu Đang chạy).
  4. Chạy lệnh, GHI KẾT QUẢ RA FILE TRƯỚC (design.md 21.8 T2 – web có thể đang tắt), rồi POST …/commands/{id}/result, gửi được thì xóa file.
  Nhật ký: <StatePath>\agent-journal.log (UTF-8, mỗi dòng một sự kiện, quá 5 MB thì đổi tên thành .1). Không có việc thì không ghi nhật ký.

  Lệnh hiện có: Ping ("Lệnh thử" – báo phiên bản bộ thực thi, phiên bản phần mềm, tên máy, tài khoản chạy; không đổi gì trên máy).
  Lệnh lạ (bộ thực thi cũ hơn máy chủ): báo Lỗi "chưa biết lệnh" – cài lại bộ thực thi bản mới.

  -DryRun: chỉ XEM lệnh kế tiếp (máy chủ không đánh dấu nhận lệnh), in ra màn hình, không chạy, không gửi kết quả.

  File cấu hình JSON (qt-agent-install.ps1 tạo, chỉ Administrators / SYSTEM / tài khoản bộ thực thi đọc được):
    { "ServerUrl": "https://procurement.congty.local", "AgentKey": "qta_…", "AppPath": "C:\inetpub\procurement",
      "StatePath": "D:\ProcurementData\agent" }
  Khóa có thể đặt ở biến môi trường PROC_AGENT_KEY thay cho "AgentKey".

.EXAMPLE
  .\qt-agent.ps1 -ConfigPath C:\ProcurementAgent\qt-agent.json
  .\qt-agent.ps1 -ConfigPath C:\ProcurementAgent\qt-agent.json -DryRun
#>
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'qt-agent.json'),
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
# Phiên bản script – báo về máy chủ mỗi lần hỏi lệnh (màn hình AGT cột "Bộ thực thi"). Tăng khi sửa script.
$AgentVersion = '1.0.0'
# Máy chủ cũ / Windows Server 2016 mặc định chưa bật TLS 1.2.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

if (-not (Test-Path -LiteralPath $ConfigPath)) { throw "Không thấy file cấu hình $ConfigPath (chạy qt-agent-install.ps1 trước)." }
$config = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
$serverUrl = ([string]$config.ServerUrl).TrimEnd('/')
if (-not $serverUrl) { throw 'Cấu hình thiếu ServerUrl.' }
$agentKey = if ($env:PROC_AGENT_KEY) { $env:PROC_AGENT_KEY } else { [string]$config.AgentKey }
if (-not $agentKey) { throw 'Cấu hình thiếu AgentKey (hoặc biến môi trường PROC_AGENT_KEY).' }
$statePath = if ($config.StatePath) { [string]$config.StatePath } else { Join-Path $PSScriptRoot 'state' }
$appPath = [string]$config.AppPath
$pendingPath = Join-Path $statePath 'pending'
$journalPath = Join-Path $statePath 'agent-journal.log'
$null = New-Item -ItemType Directory -Force -Path $pendingPath

function Write-Journal([string]$Message) {
    # Đổi tên khi quá 5 MB – giữ một bản cũ, không để file lớn mãi.
    if ((Test-Path -LiteralPath $journalPath) -and (Get-Item -LiteralPath $journalPath).Length -gt 5MB) {
        Move-Item -LiteralPath $journalPath -Destination "$journalPath.1" -Force
    }
    $line = '{0} {1}' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'), $Message
    [IO.File]::AppendAllText($journalPath, $line + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    Write-Output $line
}

function Get-AppVersion {
    # Phiên bản phần mềm cài trên máy: InformationalVersion của Proc.Api.dll, bỏ phần "+mã commit".
    if (-not $appPath) { return $null }
    $dll = Join-Path $appPath 'Proc.Api.dll'
    if (-not (Test-Path -LiteralPath $dll)) { return $null }
    return ([Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion -split '\+')[0]
}

function Invoke-Agent([string]$Method, [string]$Path, $Body) {
    $params = @{
        Uri             = "$serverUrl$Path"
        Method          = $Method
        Headers         = @{ 'X-Ops-Agent-Key' = $agentKey }
        UseBasicParsing = $true
        TimeoutSec      = 60
    }
    if ($null -ne $Body) {
        # PowerShell 5.1 gửi chuỗi theo ISO-8859-1 – gửi byte UTF-8 để giữ dấu tiếng Việt.
        $params.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 5 -Compress))
        $params.ContentType = 'application/json; charset=utf-8'
    }
    return Invoke-WebRequest @params
}

function Get-StatusCode($ErrorRecord) {
    $response = $ErrorRecord.Exception.Response
    if ($null -eq $response) { return $null }
    return [int]$response.StatusCode
}

function Send-Result([string]$File) {
    $pending = Get-Content -LiteralPath $File -Raw -Encoding UTF8 | ConvertFrom-Json
    try {
        $null = Invoke-Agent 'POST' "/api/ops/v1/agent/commands/$($pending.CommandId)/result" @{
            succeeded = [bool]$pending.Succeeded; message = [string]$pending.Message; journal = [string]$pending.Journal
        }
        Remove-Item -LiteralPath $File -Force
        Write-Journal "Đã gửi kết quả lệnh $($pending.CommandId)."
    }
    catch {
        $code = Get-StatusCode $_
        # 400 / 404: máy chủ đã có kết quả, lệnh đã quá giờ hoặc không còn – giữ file cũng không gửi được nữa.
        if ($code -in 400, 404) {
            Move-Item -LiteralPath $File -Destination "$File.rejected" -Force
            Write-Journal "Máy chủ không nhận kết quả lệnh $($pending.CommandId) (HTTP $code) – lưu lại $File.rejected."
        }
        else {
            Write-Journal "Chưa gửi được kết quả lệnh $($pending.CommandId): $($_.Exception.Message) – thử lại lần sau."
        }
    }
}

function Invoke-Ping($Command) {
    $os = (Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue).Caption
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $app = Get-AppVersion
    $lines = @(
        "Bộ thực thi: $AgentVersion",
        "Máy: $env:COMPUTERNAME",
        "Tài khoản chạy: $user",
        "Hệ điều hành: $os",
        "PowerShell: $($PSVersionTable.PSVersion)",
        "Thư mục phần mềm: $appPath",
        "Phiên bản phần mềm: $(if ($app) { $app } else { '(không đọc được Proc.Api.dll)' })",
        "Thư mục trạng thái: $statePath"
    )
    return @{
        Succeeded = $true
        Message   = "Bộ thực thi $AgentVersion trên $env:COMPUTERNAME ($user) – phần mềm $(if ($app) { $app } else { 'không rõ' })."
        Journal   = ($lines -join "`n")
    }
}

$lockFile = Join-Path $statePath 'qt-agent.lock'
try {
    $lock = [IO.File]::Open($lockFile, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
}
catch {
    Write-Output 'Lần chạy trước của bộ thực thi chưa xong – bỏ qua lần này.'
    exit 0
}

try {
    $query = "?agentVersion=$AgentVersion&appVersion=$([uri]::EscapeDataString([string](Get-AppVersion)))" +
        "&host=$([uri]::EscapeDataString($env:COMPUTERNAME))"
    if ($DryRun) {
        $pendingCount = @(Get-ChildItem -LiteralPath $pendingPath -Filter '*.json' -File).Count
        Write-Output "Chạy thử (-DryRun) – máy chủ $serverUrl, kết quả còn treo: $pendingCount."
        $response = Invoke-Agent 'GET' "/api/ops/v1/agent/commands$query&peek=true" $null
        if ($response.StatusCode -eq 204) { Write-Output 'Kết nối được, khóa đúng. Không có lệnh đang chờ.'; exit 0 }
        $work = $response.Content | ConvertFrom-Json
        Write-Output "Lệnh kế tiếp: $($work.kind) – lý do: $($work.reason) – người ra lệnh: $($work.createdByName) (không chạy ở chế độ -DryRun)."
        exit 0
    }

    foreach ($file in Get-ChildItem -LiteralPath $pendingPath -Filter '*.json' -File) { Send-Result $file.FullName }

    try {
        $response = Invoke-Agent 'GET' "/api/ops/v1/agent/commands$query" $null
    }
    catch {
        $code = Get-StatusCode $_
        if ($code -eq 401) { Write-Journal 'Máy chủ từ chối khóa bộ thực thi (HTTP 401) – tạo khóa mới trên màn hình AGT, dán vào cấu hình.' }
        else { Write-Journal "Không hỏi được lệnh từ $serverUrl : $($_.Exception.Message)" }
        exit 1
    }
    if ($response.StatusCode -eq 204) { exit 0 }

    $work = $response.Content | ConvertFrom-Json
    Write-Journal "Nhận lệnh $($work.id) – $($work.kind) – lý do: $($work.reason) – người ra lệnh: $($work.createdByName)."
    try {
        $result = switch ($work.kind) {
            'Ping' { Invoke-Ping $work }
            default {
                @{ Succeeded = $false; Message = "Bộ thực thi $AgentVersion chưa biết lệnh '$($work.kind)' – cài bộ thực thi bản mới (qt-agent-install.ps1)."; Journal = '' }
            }
        }
    }
    catch {
        $result = @{ Succeeded = $false; Message = "Lỗi khi chạy lệnh: $($_.Exception.Message)"; Journal = ($_ | Out-String) }
    }

    # Ghi kết quả ra file trước khi gửi (T2): máy chủ không nhận được thì lần chạy sau gửi lại.
    $pendingFile = Join-Path $pendingPath "$($work.id).json"
    $payload = @{ CommandId = $work.id; Succeeded = $result.Succeeded; Message = $result.Message; Journal = $result.Journal } |
        ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($pendingFile, $payload, [Text.UTF8Encoding]::new($false))
    Write-Journal "Lệnh $($work.id): $(if ($result.Succeeded) { 'Xong' } else { 'Lỗi' }) – $($result.Message)"
    Send-Result $pendingFile
}
finally {
    $lock.Dispose()
}
