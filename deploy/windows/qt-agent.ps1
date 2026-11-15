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
  Từ bản 1.1.0 (lát QT6 – phục hồi, màn hình RST):
    RestorePointInTime – ghép Full + Diff (pg_combinebackup) ra <Backup:RootPath>\restore\<giờ>\data, phát lại WAL tới mốc, chạy máy chủ CSDL tạm
                         (cổng Restore:TempPort, archive_mode=off) để người dùng kiểm tra.
    DiscardPointInTime – dừng máy chủ tạm, xóa thư mục khôi phục.
    SwapDatabase       – (bản pg_dump) xin máy chủ chuẩn bị (bật bảo trì + chép bảng vận hành), dừng app pool, đổi tên CSDL đang chạy → bản cũ,
                         bản khôi phục → CSDL đang chạy, chạy lại, kiểm tra /health; lỗi thì đổi tên ngược lại.
    SwapCluster        – (mốc thời gian) chuẩn bị như trên, dừng site + máy chủ tạm + dịch vụ PostgreSQL, cất thư mục dữ liệu cũ (thêm hậu tố),
                         đưa thư mục khôi phục vào, chạy lại, chép file .history vào thư mục WAL, kiểm tra /health.
  Lệnh lạ (bộ thực thi cũ hơn máy chủ): báo Lỗi "chưa biết lệnh" – cài lại bộ thực thi bản mới.

  -DryRun: chỉ XEM lệnh kế tiếp (máy chủ không đánh dấu nhận lệnh), in ra màn hình, không chạy, không gửi kết quả.

  File cấu hình JSON (qt-agent-install.ps1 tạo, chỉ Administrators / SYSTEM / tài khoản bộ thực thi đọc được):
    { "ServerUrl": "https://procurement.congty.local", "AgentKey": "qta_…", "AppPath": "C:\inetpub\procurement",
      "StatePath": "D:\ProcurementData\agent",
      // từ 1.1.0 – phục hồi:
      "PgBinPath": "C:\Program Files\PostgreSQL\18\bin", "PgHost": "localhost", "PgPort": 5432, "PgUser": "proc_maint", "PgPassword": "…",
      "AppPoolName": "Procurement", "HealthUrl": "https://procurement.congty.local/health",
      "PgServiceName": "postgresql-x64-18", "PgDataPath": "D:\PostgreSQL\18\data" }
  Khóa có thể đặt ở biến môi trường PROC_AGENT_KEY thay cho "AgentKey"; mật khẩu PostgreSQL ở PROC_AGENT_PGPASSWORD thay cho "PgPassword".

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
$AgentVersion = '1.1.0'
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

# ---------- Lát QT6: phục hồi (RST) – khôi phục về mốc thời gian, hoán đổi CSDL / máy chủ CSDL ----------
# Cấu hình thêm (qt-agent.json): PgBinPath (thư mục bin PostgreSQL 18), PgHost (localhost), PgPort (5432), PgUser (proc_maint), PgPassword
# (hoặc biến môi trường PROC_AGENT_PGPASSWORD), AppPoolName (app pool IIS của site), HealthUrl (mặc định ServerUrl/health), PgServiceName (dịch
# vụ Windows của PostgreSQL, ví dụ postgresql-x64-18), PgDataPath (thư mục dữ liệu PostgreSQL chính).

$pgBin = [string]$config.PgBinPath
$pgHost = if ($config.PgHost) { [string]$config.PgHost } else { 'localhost' }
$pgPort = if ($config.PgPort) { [int]$config.PgPort } else { 5432 }
$pgUser = if ($config.PgUser) { [string]$config.PgUser } else { 'proc_maint' }
$pgPassword = if ($env:PROC_AGENT_PGPASSWORD) { $env:PROC_AGENT_PGPASSWORD } else { [string]$config.PgPassword }
$appPool = [string]$config.AppPoolName
$healthUrl = if ($config.HealthUrl) { [string]$config.HealthUrl } else { "$serverUrl/health" }

function Get-PgTool([string]$Name) {
    if ($pgBin) { return Join-Path $pgBin "$Name.exe" }
    $found = Get-Command "$Name.exe" -ErrorAction SilentlyContinue
    if ($found) { return $found.Source }
    $root = Join-Path $env:ProgramFiles 'PostgreSQL'
    $dir = Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [double]($_.Name -replace '[^0-9.]', '') } -Descending | Select-Object -First 1
    if ($dir) { return Join-Path $dir.FullName "bin\$Name.exe" }
    throw "Không thấy $Name.exe – khai PgBinPath trong qt-agent.json."
}

function Assert-DbName([string]$Name) {
    # Tên CSDL do phần mềm đặt (RestoreNames): chỉ chữ thường không dấu, số, gạch dưới – đưa thẳng vào câu lệnh, không cần ngoặc kép (PowerShell
    # 5.1 làm mất ngoặc kép trong tham số gửi cho chương trình ngoài).
    if ($Name -cnotmatch '^[a-z_][a-z0-9_]{0,62}$') { throw "Tên CSDL không hợp lệ: $Name" }
}

function Invoke-Pg([string]$Tool, [string[]]$Arguments, [string]$WorkDir) {
    # Chạy công cụ PostgreSQL, mật khẩu qua PGPASSWORD (không vào tham số), thông báo tiếng Anh (LC_MESSAGES=C) cho nhật ký.
    $exe = Get-PgTool $Tool
    $old = @{ PGPASSWORD = $env:PGPASSWORD; LC_MESSAGES = $env:LC_MESSAGES }
    $env:PGPASSWORD = $pgPassword; $env:LC_MESSAGES = 'C'
    Push-Location -LiteralPath $(if ($WorkDir) { $WorkDir } else { $statePath })
    # PowerShell 5.1 biến mỗi dòng stderr của chương trình ngoài (kể cả NOTICE của psql) thành lỗi – với ErrorActionPreference = Stop là dừng
    # script. Tạm để Continue, đổi bản ghi lỗi thành chữ; đúng / sai xét theo mã thoát.
    $eap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & $exe @Arguments 2>&1 | ForEach-Object { "$_" } | Out-String
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $eap
        Pop-Location
        $env:PGPASSWORD = $old.PGPASSWORD; $env:LC_MESSAGES = $old.LC_MESSAGES
    }
    $script:restoreLog.Add("> $Tool $($Arguments -join ' ')`n$($out.Trim())")
    if ($code -ne 0) { throw "$Tool báo lỗi (mã $code): $($out.Trim())" }
    return $out.Trim()
}

function Invoke-Sql([string]$Database, [string]$Sql, [int]$Port = $pgPort) {
    return Invoke-Pg 'psql' @('-h', $pgHost, '-p', "$Port", '-U', $pgUser, '-d', $Database, '-w', '-X', '-At', '-v', 'ON_ERROR_STOP=1', '-c', $Sql)
}

function Step([string]$Message) {
    $script:restoreLog.Add((Get-Date).ToString('HH:mm:ss') + ' ' + $Message)
    Write-Journal $Message
}

function Stop-Site {
    if (-not $appPool) { Step 'Không khai AppPoolName – bỏ qua dừng site (chỉ dùng khi thử).'; return }
    Import-Module WebAdministration
    if ((Get-WebAppPoolState -Name $appPool).Value -ne 'Stopped') { Stop-WebAppPool -Name $appPool }
    for ($i = 0; $i -lt 60 -and (Get-WebAppPoolState -Name $appPool).Value -ne 'Stopped'; $i++) { Start-Sleep -Seconds 1 }
    Step "Đã dừng app pool $appPool."
}

function Start-Site {
    if (-not $appPool) { return }
    Import-Module WebAdministration
    if ((Get-WebAppPoolState -Name $appPool).Value -ne 'Started') { Start-WebAppPool -Name $appPool }
    Step "Đã chạy app pool $appPool."
}

function Test-Health {
    if (-not $appPool) { return $true }
    for ($i = 0; $i -lt 36; $i++) {
        try {
            $r = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 20
            if ($r.StatusCode -eq 200) { Step "/health trả 200: $($r.Content)"; return $true }
        }
        catch { }
        Start-Sleep -Seconds 5
    }
    Step "/health không trả 200 sau 3 phút ($healthUrl)."
    return $false
}

function Request-PrepareSwap($Command) {
    # Máy chủ bật chế độ bảo trì + chép bảng vận hành sang bản khôi phục. Lỗi thì dừng ngay – chưa đổi gì trên máy.
    $null = Invoke-Agent 'POST' "/api/ops/v1/agent/commands/$($Command.id)/prepare-swap" $null
    Step 'Máy chủ đã bật chế độ bảo trì và chép bảng vận hành sang bản khôi phục.'
}

function New-Result([bool]$Ok, [string]$Message) {
    return @{ Succeeded = $Ok; Message = $Message; Journal = ($script:restoreLog -join "`n") }
}

function Invoke-SwapDatabase($Command) {
    $p = $Command.parameters | ConvertFrom-Json
    $live = [string]$p.liveDatabase; $restore = [string]$p.restoreDatabase; $old = [string]$p.oldDatabase
    foreach ($n in $live, $restore, $old) { Assert-DbName $n }
    $exists = Invoke-Sql 'postgres' "SELECT string_agg(datname, ',') FROM pg_database WHERE datname IN ('$live', '$restore', '$old')"
    $names = @($exists -split ',')
    if ($names -notcontains $live -or $names -notcontains $restore) { throw "Thiếu CSDL $live hoặc $restore trên máy chủ." }
    if ($names -contains $old) { throw "CSDL $old đã có – không đổi tên được." }

    Request-PrepareSwap $Command
    Step "Hoán đổi: $live → $old, $restore → $live."
    Stop-Site
    $terminate = "SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE datname IN ('$live', '$restore') AND pid <> pg_backend_pid()"
    $renamedLive = $false
    try {
        $null = Invoke-Sql 'postgres' $terminate
        $null = Invoke-Sql 'postgres' "ALTER DATABASE $live RENAME TO $old"
        $renamedLive = $true
        $null = Invoke-Sql 'postgres' "ALTER DATABASE $restore RENAME TO $live"
        Step 'Đã đổi tên hai CSDL.'
    }
    catch {
        Step "Đổi tên lỗi: $($_.Exception.Message) – trả lại như cũ."
        if ($renamedLive) { $null = Invoke-Sql 'postgres' "ALTER DATABASE $old RENAME TO $live" }
        Start-Site
        return New-Result $false "Không đổi tên được CSDL – đã trả về như cũ: $($_.Exception.Message)"
    }

    Start-Site
    if (Test-Health) { return New-Result $true "Đã hoán đổi: $restore thành $live, CSDL cũ giữ tên $old. Chế độ bảo trì còn bật – kiểm tra rồi tắt ở CFG (Cấu hình truy cập, tab Bảo trì)." }

    # Site không lên với CSDL mới: trả lại như cũ (dữ liệu không mất – đang bảo trì, chưa ai nhập).
    Stop-Site
    $null = Invoke-Sql 'postgres' $terminate
    $null = Invoke-Sql 'postgres' "ALTER DATABASE $live RENAME TO $restore"
    $null = Invoke-Sql 'postgres' "ALTER DATABASE $old RENAME TO $live"
    Start-Site
    $null = Test-Health
    return New-Result $false 'Site không chạy được với bản khôi phục (/health) – đã trả CSDL cũ về như trước.'
}

function Get-RestoreLayout($p) {
    # workDir = <Backup:RootPath>\restore\<giờ>: công cụ chạy trong thư mục sao lưu với đường dẫn tương đối (tham số lệnh PostgreSQL trên
    # Windows đọc theo bảng mã ANSI – thư mục có dấu tiếng Việt thì chỉ dùng được làm thư mục làm việc).
    $work = [string]$p.workDir
    $root = Split-Path -Parent (Split-Path -Parent $work)
    $rel = { param($path) $path.Substring($root.Length).TrimStart('\') }.GetNewClosure()
    return @{ Root = $root; Work = $work; Data = (Join-Path $work 'data'); Rel = $rel; Port = [int]$p.port }
}

function Invoke-RestorePointInTime($Command) {
    $p = $Command.parameters | ConvertFrom-Json
    $l = Get-RestoreLayout $p
    if (Test-Path -LiteralPath $l.Work) { throw "Thư mục $($l.Work) đã có." }
    $wal = [string]$p.walArchivePath
    if (-not (Test-Path -LiteralPath $wal)) { throw "Không thấy thư mục WAL $wal." }
    $null = New-Item -ItemType Directory -Force -Path $l.Work
    $dataRel = & $l.Rel $l.Data
    if ($p.diffPath) {
        Step "Ghép Full + Diff bằng pg_combinebackup ra $($l.Data)."
        $null = Invoke-Pg 'pg_combinebackup' @((& $l.Rel ([string]$p.fullPath)), (& $l.Rel ([string]$p.diffPath)), '-o', $dataRel) $l.Root
    }
    else {
        Step "Chép bản Full ra $($l.Data)."
        $null = robocopy ([string]$p.fullPath) $l.Data /E /NFL /NDL /NJH /NJS /R:1 /W:1
        if ($LASTEXITCODE -ge 8) { throw "robocopy lỗi (mã $LASTEXITCODE)." }
    }
    foreach ($f in 'postmaster.pid', 'standby.signal', 'backup_manifest') { Remove-Item -LiteralPath (Join-Path $l.Data $f) -Force -ErrorAction SilentlyContinue }
    # Phát lại WAL tới mốc rồi lên máy chủ thường (promote). Cổng, tắt lưu WAL chỉ truyền khi chạy (-o) – không ghi vào file cấu hình, để sau khi
    # hoán đổi máy chủ chính vẫn cổng cũ, vẫn lưu WAL.
    $restoreCmd = "copy `"$wal\%f`" `"%p`""
    $conf = @(
        '', '# Lát QT6 – khôi phục về mốc (bộ thực thi ghi, hoán đổi thì xóa)',
        "restore_command = '$($restoreCmd -replace "'", "''")'",
        "recovery_target_time = '$($p.targetTime)'",
        "recovery_target_action = 'promote'"
    )
    [IO.File]::AppendAllText((Join-Path $l.Data 'postgresql.auto.conf'), ($conf -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $l.Data 'recovery.signal'), '')
    Step "Chạy máy chủ CSDL tạm cổng $($l.Port), phát lại WAL tới $($p.targetTime)."
    $null = Invoke-Pg 'pg_ctl' @('start', '-D', $dataRel, '-l', (& $l.Rel (Join-Path $l.Work 'postgres.log')), '-w', '-t', '600',
        '-o', "-p $($l.Port) -c archive_mode=off -c listen_addresses=localhost") $l.Root
    for ($i = 0; $i -lt 720; $i++) {
        try { if ((Invoke-Sql 'postgres' 'SELECT pg_is_in_recovery()' $l.Port) -eq 'f') { break } } catch { }
        Start-Sleep -Seconds 5
    }
    if ((Invoke-Sql 'postgres' 'SELECT pg_is_in_recovery()' $l.Port) -ne 'f') { throw 'Máy chủ tạm chưa phát lại xong WAL sau 1 giờ – xem postgres.log.' }
    $at = Invoke-Sql 'postgres' "SELECT to_char(now(), 'HH24:MI DD/MM/YYYY')" $l.Port
    return New-Result $true "Máy chủ CSDL tạm cổng $($l.Port) đã về mốc $($p.targetTime) (lên lúc $at) – kiểm tra rồi hoán đổi hoặc bỏ."
}

function Stop-TempServer($l) {
    if (Test-Path -LiteralPath (Join-Path $l.Data 'postmaster.pid')) {
        try { $null = Invoke-Pg 'pg_ctl' @('stop', '-D', (& $l.Rel $l.Data), '-m', 'fast', '-w') $l.Root; Step 'Đã dừng máy chủ CSDL tạm.' }
        catch { Step "Dừng máy chủ tạm lỗi: $($_.Exception.Message)" }
    }
}

function Invoke-DiscardPointInTime($Command) {
    $p = $Command.parameters | ConvertFrom-Json
    $l = Get-RestoreLayout $p
    if (Test-Path -LiteralPath $l.Work) {
        Stop-TempServer $l
        Remove-Item -LiteralPath $l.Work -Recurse -Force
        Step "Đã xóa $($l.Work)."
    }
    return New-Result $true 'Đã bỏ bản khôi phục theo mốc (dừng máy chủ tạm, xóa thư mục).'
}

function Invoke-SwapCluster($Command) {
    $p = $Command.parameters | ConvertFrom-Json
    $l = Get-RestoreLayout $p
    $service = [string]$config.PgServiceName
    $dataPath = [string]$config.PgDataPath
    if (-not $service -or -not $dataPath) { throw 'Cấu hình thiếu PgServiceName hoặc PgDataPath (thư mục dữ liệu PostgreSQL chính).' }
    if (-not (Test-Path -LiteralPath $l.Data)) { throw "Không thấy bản khôi phục $($l.Data)." }
    $oldPath = "${dataPath}_$($p.oldSuffix)"
    if (Test-Path -LiteralPath $oldPath) { throw "$oldPath đã có." }

    Request-PrepareSwap $Command
    Stop-Site
    Stop-TempServer $l
    # Bỏ các dòng phục hồi đã ghi vào postgresql.auto.conf – máy chủ chính không cần nữa.
    $auto = Join-Path $l.Data 'postgresql.auto.conf'
    $kept = Get-Content -LiteralPath $auto -Encoding UTF8 | Where-Object { $_ -notmatch '^(restore_command|recovery_target_time|recovery_target_action) =|^# Lát QT6' }
    [IO.File]::WriteAllLines($auto, [string[]]$kept, [Text.UTF8Encoding]::new($false))
    Remove-Item -LiteralPath (Join-Path $l.Data 'recovery.signal') -Force -ErrorAction SilentlyContinue

    Step "Dừng dịch vụ $service."
    Stop-Service -Name $service -Force
    Rename-Item -LiteralPath $dataPath -NewName (Split-Path -Leaf $oldPath)
    Step "Đã cất thư mục dữ liệu cũ thành $oldPath."
    try {
        $null = robocopy $l.Data $dataPath /E /MOVE /COPY:DAT /NFL /NDL /NJH /NJS /R:1 /W:1
        if ($LASTEXITCODE -ge 8) { throw "robocopy lỗi (mã $LASTEXITCODE)." }
        # Quyền thư mục dữ liệu như thư mục cũ (tài khoản chạy dịch vụ PostgreSQL phải đọc / ghi được).
        Get-Acl -LiteralPath $oldPath | Set-Acl -LiteralPath $dataPath
        Start-Service -Name $service
        Step "Đã chạy lại $service với thư mục dữ liệu khôi phục."
    }
    catch {
        Step "Đưa bản khôi phục vào lỗi: $($_.Exception.Message) – trả thư mục dữ liệu cũ về."
        if (Test-Path -LiteralPath $dataPath) { Rename-Item -LiteralPath $dataPath -NewName ((Split-Path -Leaf $dataPath) + "_failed_$($p.oldSuffix)") }
        Rename-Item -LiteralPath $oldPath -NewName (Split-Path -Leaf $dataPath)
        Start-Service -Name $service
        Start-Site
        return New-Result $false "Không hoán đổi được máy chủ CSDL – đã trả thư mục dữ liệu cũ: $($_.Exception.Message)"
    }

    # File lịch sử timeline mới (00000002.history…) – máy chủ tạm chạy với archive_mode=off nên chưa lưu; chép vào thư mục WAL để lần khôi phục
    # sau đi được qua timeline mới.
    if ($p.walArchivePath) {
        Get-ChildItem -LiteralPath (Join-Path $dataPath 'pg_wal') -Filter '*.history' -File | ForEach-Object {
            $target = Join-Path ([string]$p.walArchivePath) $_.Name
            if (-not (Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath $_.FullName -Destination $target }
        }
    }
    Start-Site
    if (Test-Health) {
        Remove-Item -LiteralPath $l.Work -Recurse -Force -ErrorAction SilentlyContinue
        return New-Result $true "Đã hoán đổi máy chủ CSDL về mốc; thư mục dữ liệu cũ giữ ở $oldPath. Chế độ bảo trì còn bật – kiểm tra rồi tắt ở CFG (Cấu hình truy cập, tab Bảo trì)."
    }
    return New-Result $false "Đã đưa bản khôi phục vào nhưng site không trả /health – xem log PostgreSQL; thư mục dữ liệu cũ ở $oldPath (đổi tên lại để quay về)."
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
    # Nhật ký chi tiết của lệnh (các bước, đầu ra công cụ PostgreSQL) – gửi về máy chủ cùng kết quả.
    $script:restoreLog = [Collections.Generic.List[string]]::new()
    try {
        $result = switch ($work.kind) {
            'Ping' { Invoke-Ping $work }
            'RestorePointInTime' { Invoke-RestorePointInTime $work }
            'DiscardPointInTime' { Invoke-DiscardPointInTime $work }
            'SwapDatabase' { Invoke-SwapDatabase $work }
            'SwapCluster' { Invoke-SwapCluster $work }
            default {
                @{ Succeeded = $false; Message = "Bộ thực thi $AgentVersion chưa biết lệnh '$($work.kind)' – cài bộ thực thi bản mới (qt-agent-install.ps1)."; Journal = '' }
            }
        }
    }
    catch {
        $result = @{
            Succeeded = $false
            Message   = "Lỗi khi chạy lệnh: $($_.Exception.Message)"
            Journal   = (($script:restoreLog -join "`n") + "`n" + ($_ | Out-String))
        }
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
