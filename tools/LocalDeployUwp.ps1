# 本机（桌面）部署并启动 UWP Shell（Win10 / 本机测试）
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$env:TEMP = Join-Path $ProjectRoot '_tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null

$msbCandidates = @(
    'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe'
)
$msb = $msbCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $msb) { throw 'MSBuild not found' }

$proj = Join-Path $ProjectRoot 'src\MoonWeChat\MoonWeChat.csproj'
Write-Host "Build $proj ($Platform) ..."
& $msb $proj /t:Build /p:Configuration=Debug /p:Platform=$Platform /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed $LASTEXITCODE" }

$out = Join-Path $ProjectRoot "src\MoonWeChat\bin\$Platform\Debug"
$manifest = Join-Path $out 'AppxManifest.xml'
if (-not (Test-Path $manifest)) { throw "Missing $manifest" }

try {
    Add-AppxPackage -Register $manifest -ErrorAction Stop
    Write-Host 'Registered layout package'
} catch {
    Write-Host "Register note: $($_.Exception.Message)"
}

$pkg = Get-AppxPackage | Where-Object { $_.Name -eq 'MoonWeChat.DaYueZhuiLuoKuangXiang.WP10' -or $_.Name -eq 'MoonWeChat.DaYueZhuiLuoKuangXiang' } | Select-Object -First 1
if (-not $pkg) { throw 'Package not installed' }

Get-Process MoonWeChat -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
$aumid = $pkg.PackageFamilyName + '!App'
Write-Host "Launch $aumid"
Start-Process explorer.exe -ArgumentList "shell:AppsFolder\$aumid"

Start-Sleep -Seconds 3
if (-not (Get-Process MoonWeChat -ErrorAction SilentlyContinue)) {
    throw 'MoonWeChat process not running after launch'
}
Write-Host 'OK: MoonWeChat is running on this PC.'
Write-Host "InstallLocation: $($pkg.InstallLocation)"
