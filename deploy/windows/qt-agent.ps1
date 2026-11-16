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
  Từ bản 1.2.0 (lát QT8 – trung tâm phát hành REL):
    DeployRelease   – tải gói qua API bộ thực thi, kiểm SHA256, sao lưu CSDL (pg_dump + đọc lại), app_offline.htm, cất bản đang chạy vào
                      <ReleasesPath>\<bản>, chép bản mới (giữ appsettings.Production.json), chạy script CSDL mới hơn bản đang chạy, mở site, chờ
                      /health đúng phiên bản + kiểm thử nhanh; lỗi thì tự trả bản cũ + CSDL vừa sao lưu. Đạt thì chép bộ thực thi của gói cho lần sau.
    RollbackRelease – quay về bản đã cất: mức 1 chỉ trả code; mức 2 chạy script gỡ db-scripts\down\<bản>.sql; mức 3 khôi phục bản sao lưu trước
                      lần cài (design.md 21.6a). Chạy được cả trên TEST (quay về khi UAT không đạt – lát QT9).
  Từ bản 1.3.0 (lát QT9 – làm mới TEST từ PROD, chỉ máy TEST):
    RefreshTest     – tải bản pg_dump của PROD (kiểm SHA256), khôi phục ra CSDL <CSDL>_refresh_<giờ>, tắt site, giữ bảng vận hành của chính TEST
                      và đặt lại phần riêng TEST (thư chờ gửi, phiên đăng nhập), đổi tên: CSDL cũ → <CSDL>_prerefresh_<giờ>, bản mới → <CSDL>,
                      chép bổ sung file đính kèm, mở site, chờ /health; không lên thì trả CSDL cũ. Chỉ giữ một bản _prerefresh_.
  Lệnh lạ (bộ thực thi cũ hơn máy chủ): báo Lỗi "chưa biết lệnh" – cài lại bộ thực thi bản mới.

  -DryRun: chỉ XEM lệnh kế tiếp (máy chủ không đánh dấu nhận lệnh), in ra màn hình, không chạy, không gửi kết quả.

  File cấu hình JSON (qt-agent-install.ps1 tạo, chỉ Administrators / SYSTEM / tài khoản bộ thực thi đọc được):
    { "ServerUrl": "https://procurement.congty.local", "AgentKey": "qta_…", "AppPath": "C:\inetpub\procurement",
      "StatePath": "D:\ProcurementData\agent",
      // từ 1.1.0 – phục hồi:
      "PgBinPath": "C:\Program Files\PostgreSQL\18\bin", "PgHost": "localhost", "PgPort": 5432, "PgUser": "proc_maint", "PgPassword": "…",
      "AppPoolName": "Procurement", "HealthUrl": "https://procurement.congty.local/health",
      "PgServiceName": "postgresql-x64-18", "PgDataPath": "D:\PostgreSQL\18\data",
      // từ 1.2.0 – cài bản phát hành:
      "DatabaseName": "procurement", "ReleasesPath": "D:\ProcurementData\releases",
      // từ 1.3.0 – làm mới TEST (chỉ máy TEST):
      "AttachmentsPath": "D:\ProcurementData\attachments", "AppDbUser": "proc" }
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
$AgentVersion = '1.3.0'
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
            installedSha256 = [string]$pending.InstalledSha256
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
    # InstalledSha256: lệnh cài bản phát hành (1.2.0) báo SHA256 gói đã cài – máy chủ so với phiếu.
    return @{ Succeeded = $Ok; Message = $Message; Journal = ($script:restoreLog -join "`n"); InstalledSha256 = $script:installedSha }
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

# ---------------- Từ 1.2.0 (lát QT8 – trung tâm phát hành REL, design.md 21.6, 21.6a, 21.8) ----------------
# Cấu hình thêm: DatabaseName (CSDL của phần mềm, mặc định procurement), ReleasesPath (nơi cất thư mục ứng dụng các bản đã cài – mặc định
# <StatePath>\releases). Gói tải về ở <StatePath>\packages\<bản>, bản sao lưu trước khi cài ở <StatePath>\predeploy.

$databaseName = if ($config.DatabaseName) { [string]$config.DatabaseName } else { 'procurement' }
$releasesPath = if ($config.ReleasesPath) { [string]$config.ReleasesPath } else { Join-Path $statePath 'releases' }
$packagesPath = Join-Path $statePath 'packages'
$predeployPath = Join-Path $statePath 'predeploy'

function Assert-Version([string]$Version) {
    if ($Version -notmatch '^\d{1,4}\.\d{1,5}\.\d{1,6}$') { throw "Phiên bản không hợp lệ: $Version" }
}

function Invoke-Robocopy([string]$From, [string]$To) {
    # /MIR: thư mục đích giống hệt nguồn; giữ cấu hình máy (appsettings.Production.json), file tắt site, thư mục log của IIS.
    $null = New-Item -ItemType Directory -Force -Path $To
    $out = & robocopy $From $To /MIR /R:2 /W:2 /NP /NFL /NDL /XF appsettings.Production.json app_offline.htm /XD logs 2>&1 | Out-String
    # Mã robocopy < 8 là thành công (0 – không đổi, 1 – có chép, 2/3 – có file thừa / khác).
    if ($LASTEXITCODE -ge 8) { throw "robocopy $From → $To lỗi (mã $LASTEXITCODE): $out" }
}

function Set-AppOffline([bool]$On, [string]$Message) {
    $file = Join-Path $appPath 'app_offline.htm'
    if ($On) {
        $html = "<!doctype html><html lang=""vi""><meta charset=""utf-8""><title>Đang cài bản mới</title>" +
            "<body style=""font-family:Tahoma,sans-serif;padding:40px""><h2>Phần mềm đang cài bản mới</h2><p>$Message</p></body></html>"
        [IO.File]::WriteAllText($file, $html, [Text.UTF8Encoding]::new($false))
        # ANCM tắt ứng dụng khi thấy app_offline.htm – chờ tiến trình nhả file DLL.
        Start-Sleep -Seconds 5
        Step 'Đã tắt site (app_offline.htm).'
    }
    elseif (Test-Path -LiteralPath $file) {
        Remove-Item -LiteralPath $file -Force
        Step 'Đã mở lại site (bỏ app_offline.htm).'
    }
}

function Test-HealthVersion([string]$Expected) {
    # /health ẩn danh trả {status, version} (lát QT7) – đạt khi Healthy và đúng phiên bản vừa cài, chờ tối đa 3 phút.
    for ($i = 0; $i -lt 36; $i++) {
        try {
            $r = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 20
            $h = $r.Content | ConvertFrom-Json
            if ($r.StatusCode -eq 200 -and $h.status -eq 'Healthy' -and $h.version -eq $Expected) { Step "/health: Healthy, phiên bản $($h.version)."; return $true }
        }
        catch { }
        Start-Sleep -Seconds 5
    }
    Step "/health không báo Healthy phiên bản $Expected sau 3 phút ($healthUrl)."
    return $false
}

function Test-Smoke {
    # Kiểm thử nhanh trên máy chủ vừa cài: truy vấn CSDL, danh sách chính, trung tâm phát hành mở được (design.md 21.8 T2).
    try {
        $s = (Invoke-Agent 'GET' '/api/ops/v1/agent/smoke' $null).Content | ConvertFrom-Json
        foreach ($c in $s.checks) { Step ("Kiểm thử nhanh – {0}: {1} {2}" -f $c.name, $(if ($c.ok) { 'đạt' } else { 'KHÔNG ĐẠT' }), $c.detail) }
        return [bool]$s.ok
    }
    catch {
        Step "Kiểm thử nhanh lỗi: $($_.Exception.Message)"
        return $false
    }
}

function Backup-Database([string]$File) {
    $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $File)
    $leaf = Split-Path -Leaf $File
    $null = Invoke-Pg 'pg_dump' @('-h', $pgHost, '-p', "$pgPort", '-U', $pgUser, '-w', '-Fc', '-f', $leaf, $databaseName) (Split-Path -Parent $File)
    # Đọc lại mục lục bản sao lưu – hỏng thì dừng trước khi đụng tới site.
    $null = Invoke-Pg 'pg_restore' @('--list', $leaf) (Split-Path -Parent $File)
    Step "Đã sao lưu $databaseName → $File ($([math]::Round((Get-Item -LiteralPath $File).Length / 1MB, 1)) MB), đọc lại được."
}

function Restore-Database([string]$File) {
    Assert-DbName $databaseName
    $null = Invoke-Sql 'postgres' "SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE datname = '$databaseName' AND pid <> pg_backend_pid()"
    $null = Invoke-Pg 'pg_restore' @('-h', $pgHost, '-p', "$pgPort", '-U', $pgUser, '-w', '--clean', '--if-exists', '--no-owner', '--exit-on-error',
        '-d', $databaseName, (Split-Path -Leaf $File)) (Split-Path -Parent $File)
    Step "Đã khôi phục $databaseName từ $File."
}

function Invoke-DbScripts([string]$ScriptDir, [string]$From, [string]$To) {
    # Chạy các script nâng cấp có phiên bản > bản đang chạy và ≤ bản mới, theo thứ tự tăng dần (script idempotent – chạy lại không hại).
    $ran = 0
    $scripts = Get-ChildItem -LiteralPath $ScriptDir -Filter '*.sql' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.BaseName -match '^\d+\.\d+\.\d+$' -and [version]$_.BaseName -gt [version]$From -and [version]$_.BaseName -le [version]$To } |
        Sort-Object { [version]$_.BaseName }
    foreach ($s in $scripts) {
        $old = $env:PGCLIENTENCODING; $env:PGCLIENTENCODING = 'UTF8'
        try {
            $null = Invoke-Pg 'psql' @('-h', $pgHost, '-p', "$pgPort", '-U', $pgUser, '-d', $databaseName, '-w', '-X', '-q', '-v', 'ON_ERROR_STOP=1',
                '-f', $s.Name) $ScriptDir
        }
        finally { $env:PGCLIENTENCODING = $old }
        Step "Đã chạy script CSDL $($s.Name)."
        $ran++
    }
    if ($ran -eq 0) { Step 'Không có script CSDL mới hơn bản đang chạy.' }
    return $ran
}

function Invoke-DeployRelease($Command) {
    if (-not $appPath) { throw 'Cấu hình thiếu AppPath (thư mục website).' }
    $p = $Command.parameters | ConvertFrom-Json
    $version = [string]$p.version; Assert-Version $version
    $sha = ([string]$p.sha256).ToLowerInvariant()
    $current = Get-AppVersion
    if (-not $current) { throw "Không đọc được phiên bản đang chạy ở $appPath\Proc.Api.dll." }
    Assert-Version $current
    Step "Cài bản $version (đang chạy $current) vào $appPath."

    # 1. Tải gói qua API bộ thực thi, kiểm SHA256 – sai thì dừng, chưa đổi gì trên máy.
    $pkgDir = Join-Path $packagesPath $version
    if (Test-Path -LiteralPath $pkgDir) { Remove-Item -LiteralPath $pkgDir -Recurse -Force }
    $null = New-Item -ItemType Directory -Force -Path $pkgDir
    $zip = Join-Path $pkgDir ([IO.Path]::GetFileName([string]$p.fileName))
    Invoke-WebRequest -Uri "$serverUrl$([string]$p.packagePath)" -Headers @{ 'X-Ops-Agent-Key' = $agentKey } -OutFile $zip -UseBasicParsing -TimeoutSec 1800
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant()
    if ($actual -ne $sha) { return New-Result $false "SHA256 gói tải về ($actual) khác phiếu ($sha) – không cài." }
    $script:installedSha = $actual
    Step "Đã tải gói, SHA256 đúng ($actual)."
    $pkg = Join-Path $pkgDir 'pkg'
    Expand-Archive -LiteralPath $zip -DestinationPath $pkg -Force
    if (-not (Test-Path -LiteralPath (Join-Path $pkg 'app\Proc.Api.dll'))) { return New-Result $false 'Gói không có app\Proc.Api.dll – không cài.' }

    # 2. Sao lưu CSDL (đọc lại được) – để tự quay về mức 3 nếu cài lỗi (đang tắt site, chưa ai nhập liệu nên không mất gì).
    $dump = Join-Path $predeployPath "$version-from-$current.dump"
    Backup-Database $dump

    # 3. Tắt site, cất bản đang chạy, cài bản mới, chạy script CSDL.
    $saved = Join-Path $releasesPath $current
    $scriptsRan = 0
    Set-AppOffline $true "Đang cài bản $version, vui lòng quay lại sau vài phút."
    try {
        Invoke-Robocopy $appPath $saved
        Step "Đã cất bản $current vào $saved."
        Invoke-Robocopy (Join-Path $pkg 'app') $appPath
        Step "Đã chép bản $version vào $appPath (giữ appsettings.Production.json)."
        $scriptsRan = Invoke-DbScripts (Join-Path $pkg 'db-scripts') $current $version
        Set-AppOffline $false ''
        if (-not (Test-HealthVersion $version)) { throw "/health không đạt phiên bản $version." }
        if (-not (Test-Smoke)) { throw 'Kiểm thử nhanh không đạt.' }
    }
    catch {
        $why = $_.Exception.Message
        Step "Cài lỗi: $why – tự trả về bản $current."
        try {
            Set-AppOffline $true "Đang trả về bản $current."
            if ($scriptsRan -gt 0) { Restore-Database $dump }
            Invoke-Robocopy $saved $appPath
            Set-AppOffline $false ''
            $back = Test-HealthVersion $current
            return New-Result $false "Cài $version lỗi: $why Đã trả về bản $current$(if ($scriptsRan -gt 0) { ' và CSDL vừa sao lưu' })$(if (-not $back) { ' NHƯNG /health chưa đạt – kiểm tra tay (deploy-windows.md mục 6)' })."
        }
        catch {
            return New-Result $false "Cài $version lỗi: $why; trả về bản $current cũng lỗi: $($_.Exception.Message). Cài tay theo deploy-windows.md mục 6 (bản cũ ở $saved, CSDL sao lưu $dump)."
        }
    }

    # 4. Tự cập nhật bộ thực thi cho lần chạy sau (T1) – chỉ sau khi bản mới chạy được; script đang chạy đã nạp xong nên chép đè được.
    $newAgent = Join-Path $pkg 'tools\qt-agent.ps1'
    if (Test-Path -LiteralPath $newAgent) {
        Copy-Item -LiteralPath $newAgent -Destination $PSCommandPath -Force
        Step 'Đã chép bộ thực thi của gói cho lần chạy sau.'
    }
    return New-Result $true "Đã cài bản $version (trước: $current), $scriptsRan script CSDL, /health và kiểm thử nhanh đạt."
}

function Invoke-RollbackRelease($Command) {
    if (-not $appPath) { throw 'Cấu hình thiếu AppPath (thư mục website).' }
    $p = $Command.parameters | ConvertFrom-Json
    $version = [string]$p.version; $target = [string]$p.targetVersion; $level = [int]$p.level
    Assert-Version $version; Assert-Version $target
    $saved = Join-Path $releasesPath $target
    if (-not (Test-Path -LiteralPath (Join-Path $saved 'Proc.Api.dll'))) { return New-Result $false "Không còn bản $target ở $saved – cài tay theo deploy-windows.md." }
    $down = Join-Path $packagesPath "$version\pkg\db-scripts\down\$version.sql"
    $dump = Join-Path $predeployPath "$version-from-$target.dump"
    if ($level -eq 2 -and -not (Test-Path -LiteralPath $down)) { return New-Result $false "Không có script gỡ $down – chọn mức 3." }
    if ($level -eq 3 -and -not (Test-Path -LiteralPath $dump)) { return New-Result $false "Không có bản sao lưu trước khi cài $dump." }
    Step "Quay về bản $target từ $version, mức $level."

    Set-AppOffline $true "Đang quay về bản $target."
    # Cất bản đang chạy để cài lại được nếu cần.
    Invoke-Robocopy $appPath (Join-Path $releasesPath $version)
    if ($level -eq 2) {
        $old = $env:PGCLIENTENCODING; $env:PGCLIENTENCODING = 'UTF8'
        try {
            $null = Invoke-Pg 'psql' @('-h', $pgHost, '-p', "$pgPort", '-U', $pgUser, '-d', $databaseName, '-w', '-X', '-q', '-v', 'ON_ERROR_STOP=1',
                '-f', (Split-Path -Leaf $down)) (Split-Path -Parent $down)
        }
        finally { $env:PGCLIENTENCODING = $old }
        Step "Đã chạy script gỡ $down."
    }
    elseif ($level -eq 3) {
        Restore-Database $dump
    }
    Invoke-Robocopy $saved $appPath
    Step "Đã chép bản $target vào $appPath."
    Set-AppOffline $false ''
    if (Test-HealthVersion $target) { return New-Result $true "Đã quay về bản $target (mức $level)." }
    return New-Result $false "Đã chép bản $target nhưng /health chưa đạt – kiểm tra tay (deploy-windows.md mục 6)."
}

# ---------------- Từ 1.3.0 (lát QT9 – làm mới TEST từ PROD, design.md 21.5, 21.18) ----------------
# Cấu hình thêm (chỉ máy TEST): AttachmentsPath (thư mục đính kèm của TEST – Attachments:RootPath), AppDbUser (tài khoản CSDL của ứng dụng, chủ CSDL
# mới – mặc định proc). Thư mục làm việc <StatePath>\refresh\<giờ> (bản dump tải về, file bảng giữ lại) – xong thì xóa.

$attachmentsPath = [string]$config.AttachmentsPath
$appDbUser = if ($config.AppDbUser) { [string]$config.AppDbUser } else { 'proc' }

function Invoke-SqlFile([string]$Database, [string]$Sql, [string]$WorkDir, [string]$Name) {
    # Câu lệnh có dấu tiếng Việt / dấu ngoặc kép: ghi file UTF-8 rồi psql -f (tham số dòng lệnh Windows đọc theo bảng mã ANSI, PowerShell 5.1 làm mất
    # ngoặc kép). Chạy trong thư mục làm việc để \copy dùng tên file tương đối.
    [IO.File]::WriteAllText((Join-Path $WorkDir $Name), $Sql, [Text.UTF8Encoding]::new($false))
    $old = $env:PGCLIENTENCODING; $env:PGCLIENTENCODING = 'UTF8'
    try {
        return Invoke-Pg 'psql' @('-h', $pgHost, '-p', "$pgPort", '-U', $pgUser, '-d', $Database, '-w', '-X', '-q', '-At', '-v', 'ON_ERROR_STOP=1', '-f', $Name) $WorkDir
    }
    finally { $env:PGCLIENTENCODING = $old }
}

function Copy-KeptTables([string]$From, [string]$To, [string[]]$Tables, [string]$ResetSql, [string]$WorkDir) {
    # Bảng vận hành của chính TEST (sao lưu, bộ thực thi, bảo trì, phục hồi, nhật ký vận hành / lỗi): xuất từ CSDL đang chạy ra file nhị phân, nạp
    # vào CSDL mới thay bản của PROD – trong một giao dịch cùng câu đặt lại phần riêng TEST. OperationLog chỉ thêm (trigger chặn xóa) nên chỉ chèn
    # dòng chưa có – như hoán đổi phục hồi (QT6).
    $export = [Collections.Generic.List[string]]::new()
    $import = [Collections.Generic.List[string]]::new()
    $import.Add('BEGIN;')
    $cols = @{}
    foreach ($t in $Tables) {
        if ($t -cnotmatch '^[A-Za-z][A-Za-z0-9_]{0,62}$') { throw "Tên bảng không hợp lệ: $t" }
        $c = Invoke-Sql $From "SELECT string_agg(quote_ident(column_name), ',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '$t'"
        if (-not $c) { Step "Bảng $t không có trên CSDL đang chạy – bỏ qua."; continue }
        $cols[$t] = $c
        $export.Add("\copy public.""$t"" ($c) TO 'keep_$t.bin' WITH (FORMAT binary)")
    }
    $null = Invoke-SqlFile $From ($export -join "`n") $WorkDir 'export.sql'
    $kept = @($Tables | Where-Object { $cols.ContainsKey($_) })
    # Xóa bảng con trước bảng cha (AgentCommand → AgentEnvironment, ErrorOccurrence → ErrorGroup), nạp bảng cha trước.
    for ($i = $kept.Count - 1; $i -ge 0; $i--) {
        if ($kept[$i] -ne 'OperationLog') { $import.Add("DELETE FROM public.""$($kept[$i])"";") }
    }
    foreach ($t in $kept) {
        $c = $cols[$t]
        $import.Add("CREATE TEMP TABLE ""keep_$t"" (LIKE public.""$t"") ON COMMIT DROP;")
        $import.Add("\copy ""keep_$t"" ($c) FROM 'keep_$t.bin' WITH (FORMAT binary)")
        if ($t -eq 'OperationLog') {
            $import.Add("INSERT INTO public.""$t"" ($c) SELECT $c FROM ""keep_$t"" k WHERE NOT EXISTS (SELECT 1 FROM public.""$t"" x WHERE x.""Id"" = k.""Id"");")
        }
        else {
            $import.Add("INSERT INTO public.""$t"" ($c) SELECT $c FROM ""keep_$t"";")
        }
    }
    $import.Add($ResetSql)
    $import.Add('COMMIT;')
    $null = Invoke-SqlFile $To ($import -join "`n") $WorkDir 'import.sql'
    Step "Đã giữ bảng của TEST: $($kept -join ', '); đã đặt lại hàng chờ thư và phiên đăng nhập."
}

function Sync-Attachments([string]$ManifestPath) {
    # Chép bổ sung file đính kèm của PROD: file chưa có hoặc khác dung lượng; không xóa file của TEST. Lỗi từng file ghi nhật ký, không dừng cả lệnh.
    if (-not $attachmentsPath) { Step 'Không khai AttachmentsPath – bỏ qua chép file đính kèm.'; return @{ Copied = 0; Failed = 0; Skipped = $true } }
    $files = @((Invoke-Agent 'GET' $ManifestPath $null).Content | ConvertFrom-Json)
    $copied = 0; $failed = 0
    foreach ($f in $files) {
        $rel = [string]$f.path
        if (-not $rel -or $rel.Contains('..') -or [IO.Path]::IsPathRooted($rel)) { $failed++; continue }
        $local = Join-Path $attachmentsPath ($rel -replace '/', '\')
        if ((Test-Path -LiteralPath $local) -and (Get-Item -LiteralPath $local).Length -eq [long]$f.size) { continue }
        try {
            $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $local)
            Invoke-WebRequest -Uri "$serverUrl$ManifestPath/file?path=$([uri]::EscapeDataString($rel))" -Headers @{ 'X-Ops-Agent-Key' = $agentKey } `
                -OutFile $local -UseBasicParsing -TimeoutSec 600
            $copied++
        }
        catch {
            $failed++
            Step "Chép $rel lỗi: $($_.Exception.Message)"
        }
    }
    Step "File đính kèm: PROD có $($files.Count), đã chép $copied$(if ($failed) { ", lỗi $failed" })."
    return @{ Copied = $copied; Failed = $failed; Skipped = $false }
}

function Invoke-RefreshTest($Command) {
    if (-not $appPath) { throw 'Cấu hình thiếu AppPath (thư mục website).' }
    $p = $Command.parameters | ConvertFrom-Json
    Assert-DbName $databaseName
    if ($appDbUser -cnotmatch '^[a-z_][a-z0-9_]{0,62}$') { throw "AppDbUser không hợp lệ: $appDbUser" }
    $source = [string]$p.sourceVersion; Assert-Version $source
    $current = Get-AppVersion
    # Cấu trúc CSDL theo phiên bản: TEST phải chạy đúng bản của máy nguồn (máy chủ đã chặn khi giao lệnh; kiểm lại vì có thể vừa cài bản khác).
    if ($current -ne $source) { return New-Result $false "TEST đang chạy bản $current, bản dump của bản $source – không làm mới (đưa TEST về bản $source trước)." }
    $stamp = (Get-Date).ToString('yyyyMMdd_HHmm')
    $newDb = "${databaseName}_refresh_$stamp"; $oldDb = "${databaseName}_prerefresh_$stamp"
    Assert-DbName $newDb; Assert-DbName $oldDb
    $work = Join-Path $statePath "refresh\$stamp"
    $null = New-Item -ItemType Directory -Force -Path $work
    Step "Làm mới TEST từ $($p.sourceEnvironment) bản $source (pg_dump $($p.dumpFileName))."

    # 1. Tải bản dump, kiểm SHA256 – sai thì dừng, chưa đổi gì trên máy.
    $dumpFile = Join-Path $work 'source.dump'
    Invoke-WebRequest -Uri "$serverUrl$([string]$p.dumpPath)" -Headers @{ 'X-Ops-Agent-Key' = $agentKey } -OutFile $dumpFile -UseBasicParsing -TimeoutSec 7200
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $dumpFile).Hash.ToLowerInvariant()
    if ($actual -ne ([string]$p.sha256).ToLowerInvariant()) {
        Remove-Item -LiteralPath $work -Recurse -Force
        return New-Result $false "SHA256 bản dump tải về ($actual) khác máy chủ báo ($($p.sha256)) – không làm mới."
    }
    Step "Đã tải bản dump ($([math]::Round((Get-Item -LiteralPath $dumpFile).Length / 1MB, 1)) MB), SHA256 đúng."

    # 2. Khôi phục ra CSDL mới (site TEST vẫn chạy): cùng bảng mã / locale CSDL đang chạy, chủ là tài khoản ứng dụng.
    $info = (Invoke-Sql 'postgres' "SELECT pg_encoding_to_char(encoding) || '|' || datcollate || '|' || datctype FROM pg_database WHERE datname = '$databaseName'") -split '\|'
    if ($info.Count -ne 3) { throw "Không thấy CSDL $databaseName trên máy chủ." }
    $null = Invoke-Sql 'postgres' "CREATE DATABASE $newDb OWNER $appDbUser TEMPLATE template0 ENCODING '$($info[0])' LC_COLLATE '$($info[1])' LC_CTYPE '$($info[2])'"
    $offline = $false
    try {
        $null = Invoke-Pg 'pg_restore' @('-h', $pgHost, '-p', "$pgPort", '-U', $pgUser, '-w', '--no-owner', "--role=$appDbUser", '--exit-on-error',
            '-d', $newDb, 'source.dump') $work
        Step "Đã khôi phục bản dump vào $newDb."

        # 3. Tắt site, giữ bảng của TEST + đặt lại phần riêng, đổi tên CSDL.
        Set-AppOffline $true 'Đang làm mới dữ liệu TEST từ PROD, vui lòng quay lại sau ít phút.'
        $offline = $true
        Copy-KeptTables $databaseName $newDb ([string[]]$p.keepTables) ([string]$p.resetSql) $work
        $null = Invoke-Sql 'postgres' "SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE datname IN ('$databaseName', '$newDb') AND pid <> pg_backend_pid()"
        $null = Invoke-Sql 'postgres' "ALTER DATABASE $databaseName RENAME TO $oldDb"
        try { $null = Invoke-Sql 'postgres' "ALTER DATABASE $newDb RENAME TO $databaseName" }
        catch { $null = Invoke-Sql 'postgres' "ALTER DATABASE $oldDb RENAME TO $databaseName"; throw }
        Step "Đã hoán đổi: $databaseName cũ → $oldDb, bản mới → $databaseName."
    }
    catch {
        $why = $_.Exception.Message
        Step "Làm mới lỗi: $why – giữ CSDL TEST như cũ."
        try { $null = Invoke-Sql 'postgres' "DROP DATABASE IF EXISTS $newDb WITH (FORCE)" } catch { Step "Xóa $newDb lỗi: $($_.Exception.Message)" }
        if ($offline) { Set-AppOffline $false '' }
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
        return New-Result $false "Không làm mới được TEST (CSDL TEST giữ nguyên): $why"
    }

    # 4. Chép bổ sung file đính kèm (site còn tắt), mở site, kiểm tra; không lên thì trả CSDL cũ.
    $att = Sync-Attachments ([string]$p.attachmentsPath)
    Set-AppOffline $false ''
    if (-not (Test-HealthVersion $current)) {
        Step 'Site không lên với dữ liệu mới – trả CSDL TEST cũ.'
        Set-AppOffline $true 'Đang trả dữ liệu TEST cũ.'
        $null = Invoke-Sql 'postgres' "SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE datname = '$databaseName' AND pid <> pg_backend_pid()"
        $null = Invoke-Sql 'postgres' "ALTER DATABASE $databaseName RENAME TO $newDb"
        $null = Invoke-Sql 'postgres' "ALTER DATABASE $oldDb RENAME TO $databaseName"
        Set-AppOffline $false ''
        $null = Test-HealthVersion $current
        return New-Result $false "Site không chạy được với dữ liệu PROD (/health) – đã trả CSDL TEST cũ; bản làm mới giữ tên $newDb để xem lỗi."
    }

    # 5. Dọn: chỉ giữ một CSDL TEST trước lần làm mới (dữ liệu thử – bản cũ hơn xóa), xóa thư mục làm việc (bản dump lớn).
    $prefix = "${databaseName}_prerefresh_"
    $olds = @(((Invoke-Sql 'postgres' "SELECT string_agg(datname, ',' ORDER BY datname) FROM pg_database WHERE left(datname, $($prefix.Length)) = '$prefix' AND datname <> '$oldDb'") -split ',') | Where-Object { $_ })
    foreach ($o in $olds) {
        try { Assert-DbName $o; $null = Invoke-Sql 'postgres' "DROP DATABASE IF EXISTS $o WITH (FORCE)"; Step "Đã xóa CSDL TEST cũ $o." }
        catch { Step "Xóa $o lỗi: $($_.Exception.Message)" }
    }
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    $attText = if ($att.Skipped) { 'chưa khai AttachmentsPath nên không chép file đính kèm' } else { "chép $($att.Copied) file đính kèm$(if ($att.Failed) { " ($($att.Failed) file lỗi – xem nhật ký)" })" }
    return New-Result $true "Đã làm mới TEST từ $($p.sourceEnvironment) (pg_dump $($p.dumpFileName)), $attText; CSDL TEST trước đó giữ tên $oldDb. Người dùng đăng nhập lại bằng mật khẩu PROD."
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
    $script:installedSha = $null
    try {
        $result = switch ($work.kind) {
            'Ping' { Invoke-Ping $work }
            'RestorePointInTime' { Invoke-RestorePointInTime $work }
            'DiscardPointInTime' { Invoke-DiscardPointInTime $work }
            'SwapDatabase' { Invoke-SwapDatabase $work }
            'SwapCluster' { Invoke-SwapCluster $work }
            'DeployRelease' { Invoke-DeployRelease $work }
            'RollbackRelease' { Invoke-RollbackRelease $work }
            'RefreshTest' { Invoke-RefreshTest $work }
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
    $payload = @{
        CommandId = $work.id; Succeeded = $result.Succeeded; Message = $result.Message; Journal = $result.Journal
        InstalledSha256 = $result.InstalledSha256
    } |
        ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($pendingFile, $payload, [Text.UTF8Encoding]::new($false))
    Write-Journal "Lệnh $($work.id): $(if ($result.Succeeded) { 'Xong' } else { 'Lỗi' }) – $($result.Message)"
    Send-Result $pendingFile
}
finally {
    $lock.Dispose()
}
