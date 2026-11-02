<#
.SYNOPSIS
  Đóng gói bản cài trên Windows/IIS (docs/deploy-windows.md): build giao diện React, publish API .NET, gộp vào một thư mục và nén zip.

.DESCRIPTION
  Chạy trên máy build (có .NET SDK 10 và Node.js 22) tại bất kỳ thư mục nào; mọi đường dẫn tính từ vị trí script.
  Kết quả: <Output>\procurement-<phiên bản>\ gồm
    app\                    – chép vào thư mục website IIS (Proc.Api.dll, web.config, wwwroot\ chứa giao diện)
    db-scripts\             – script nâng cấp CSDL theo phiên bản (db/scripts)
    tools\backup.ps1        – sao lưu CSDL + file đính kèm
    tools\du-lieu-thu\      – công cụ tạo dữ liệu thử cho môi trường test (Node.js 22; không chạy trên CSDL thật)
    appsettings.Production.example.json – mẫu cấu hình, đổi tên thành app\appsettings.Production.json khi cài lần đầu
  và file procurement-<phiên bản>.zip cùng nội dung.
  Bản publish là framework-dependent: server cần cài ASP.NET Core Hosting Bundle 10 (có sẵn runtime và module IIS).

.PARAMETER Output
  Thư mục chứa bản đóng gói; mặc định artifacts\ ở gốc repo (đã có trong .gitignore).

.PARAMETER NoInstall
  Bỏ bước npm ci, dùng node_modules đang có (máy dev đang chạy Vite: npm ci xóa node_modules sẽ vướng file bị khóa).

.EXAMPLE
  .\deploy\windows\publish.ps1
#>
param(
    [string]$Output,
    [switch]$NoInstall
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $Output) { $Output = Join-Path $repo 'artifacts' }

# Số phiên bản chỉ ghi ở Directory.Build.props (CLAUDE.md mục 8).
[xml]$props = Get-Content -Raw -Encoding UTF8 (Join-Path $repo 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw 'Không đọc được <Version> trong Directory.Build.props.' }

$package = Join-Path $Output "procurement-$version"
$app = Join-Path $package 'app'
if (Test-Path $package) { Remove-Item -Recurse -Force $package }
New-Item -ItemType Directory -Force $app | Out-Null

Write-Host "Đóng gói phiên bản $version vào $package"

# 1. Giao diện React – giống bước 1 của Dockerfile.
$web = Join-Path $repo 'src\Proc.Web'
Push-Location $web
try {
    if (-not $NoInstall) {
        npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'npm ci lỗi.' }
    }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'npm run build lỗi.' }
}
finally { Pop-Location }

# 2. API – publish kèm web.config (gỡ WebDAV, giới hạn 30 MB) cho IIS.
dotnet publish (Join-Path $repo 'src\Proc.Api\Proc.Api.csproj') -c Release -o $app
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish lỗi.' }

# 3. Gộp giao diện vào wwwroot: API tự phục vụ giao diện khi có wwwroot\index.html (Program.cs).
Copy-Item -Recurse (Join-Path $web 'dist') (Join-Path $app 'wwwroot')

# Không mang cấu hình dev/test lên server (chứa mật khẩu mẫu, bật dữ liệu mẫu).
Get-ChildItem $app -Filter 'appsettings.*.json' | Where-Object { $_.Name -ne 'appsettings.json' } | Remove-Item -Force

# 4. Script CSDL, công cụ, cấu hình mẫu.
Copy-Item -Recurse (Join-Path $repo 'db\scripts') (Join-Path $package 'db-scripts')
New-Item -ItemType Directory -Force (Join-Path $package 'tools') | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'backup.ps1') (Join-Path $package 'tools')
Copy-Item -Recurse (Join-Path $repo 'tools\du-lieu-thu') (Join-Path $package 'tools\du-lieu-thu')
Copy-Item (Join-Path $PSScriptRoot 'appsettings.Production.example.json') $package

$zip = Join-Path $Output "procurement-$version.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
# ZipFile thay cho Compress-Archive: Compress-Archive của PowerShell 5.1 báo lỗi khi phần mềm diệt virus còn đang quét DLL vừa ghi.
# Vẫn vướng khóa thì chờ rồi thử lại vài lần.
Add-Type -AssemblyName System.IO.Compression.FileSystem
for ($attempt = 1; ; $attempt++) {
    try {
        [System.IO.Compression.ZipFile]::CreateFromDirectory($package, $zip)
        break
    }
    catch [System.IO.IOException] {
        if (Test-Path $zip) { Remove-Item -Force $zip }
        if ($attempt -ge 5) { throw }
        Start-Sleep -Seconds 5
    }
}
Write-Host "Xong: $zip"
