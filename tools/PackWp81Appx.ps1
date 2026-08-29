# 将 MoonWeChat.WP81 打成已签名的 .appx（Windows Phone 8.1 WinRT 标准包格式）
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [ValidateSet('x86','ARM')]
    [string]$Platform = 'x86',
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$env:TEMP = Join-Path $ProjectRoot '_tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null

$msb = @(
    'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe',
    'C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $msb) { throw 'MSBuild (VS2015/2017) not found for WP8.1' }

$makeappx = @(
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.17763.0\x64\makeappx.exe',
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.15063.0\x64\makeappx.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
$signtool = @(
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.15063.0\x64\signtool.exe',
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.17763.0\x64\signtool.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $makeappx -or -not $signtool) { throw 'MakeAppx/SignTool not found' }

$proj = Join-Path $ProjectRoot 'src\MoonWeChat.WP81\MoonWeChat.WP81.csproj'
$out = Join-Path $ProjectRoot "src\MoonWeChat.WP81\bin\$Platform\$Configuration"
$pkgDir = Join-Path $ProjectRoot 'src\MoonWeChat.WP81\AppPackages'
New-Item -ItemType Directory -Path $pkgDir -Force | Out-Null

Write-Host "Building WP81 $Configuration|$Platform ..."
& $msb $proj /t:Rebuild /p:Configuration=$Configuration /p:Platform=$Platform /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }

# Assets into layout
$assetsSrc = Join-Path $ProjectRoot 'src\MoonWeChat.WP81\Assets'
$assetsDst = Join-Path $out 'Assets'
New-Item -ItemType Directory -Path $assetsDst -Force | Out-Null
@(
    'Logo.png','SmallLogo.png','SplashScreen.png',
    'Square71x71Logo.png','StoreLogo.png','WideLogo.png'
) | ForEach-Object {
    Copy-Item (Join-Path $assetsSrc $_) (Join-Path $assetsDst $_) -Force
}

# Strip non-payload clutter
Remove-Item (Join-Path $out 'MoonWeChat.pdb') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $out 'MoonWeChat.WP81.build.appxrecipe') -Force -ErrorAction SilentlyContinue

$appxName = "MoonWeChat.WP81_1.0.0.0_${Platform}_${Configuration}.appx"
$appx = Join-Path $pkgDir $appxName
if (Test-Path $appx) { Remove-Item $appx -Force }

Write-Host "Packing $appx ..."
& $makeappx pack /d $out /p $appx /o
if (-not (Test-Path $appx)) { throw 'MakeAppx failed' }

# Self-signed cert matching Package.appxmanifest Publisher=CN=MoonWeChatDev
$pfx = Join-Path $pkgDir 'MoonWeChatDev.pfx'
$cer = Join-Path $pkgDir 'MoonWeChatDev.cer'
$pwdPlain = 'moon-wp81'
if (-not (Test-Path $pfx)) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=MoonWeChatDev' `
        -KeyExportPolicy Exportable -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(5)
    $pwd = ConvertTo-SecureString $pwdPlain -AsPlainText -Force
    Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $pwd | Out-Null
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null
    Write-Host "Created cert $($cert.Thumbprint)"
}

Write-Host 'Signing ...'
& $signtool sign /fd SHA256 /a /f $pfx /p $pwdPlain $appx
if ($LASTEXITCODE -ne 0) { throw "Sign failed: $LASTEXITCODE" }

$item = Get-Item $appx
Write-Host ''
Write-Host "OK: $($item.FullName)"
Write-Host "Size: $($item.Length) bytes"
Write-Host ''
Write-Host 'Install on WP 8.1 (dev unlocked):'
Write-Host "  AppDeployCmd /installlaunch `"$($item.FullName)`" /targetdevice:de   # USB 真机"
Write-Host "  AppDeployCmd /installlaunch `"$($item.FullName)`" /targetdevice:7    # Emulator 8.1 512MB"
Write-Host ''
Write-Host "Trust cert on device if needed: $cer"
Write-Host "  （pfx 口令写在本脚本里，需要时打开 tools\PackWp81Appx.ps1 查看；"
Write-Host "    不打印到 stdout —— 打包输出经常被重定向进日志文件。）"
Write-Host "Publisher must match manifest: CN=MoonWeChatDev"
