# Pack clean UWP/WP10 appx (ARM/x64/x86)
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [ValidateSet('ARM', 'x64', 'x86')]
    [string]$Platform = 'ARM',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$env:TEMP = Join-Path $ProjectRoot '_tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null

$msb = @(
    'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $msb) { throw 'MSBuild not found' }

$makeappx = @(
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe',
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
$signtool = @(
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe',
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $makeappx -or -not $signtool) { throw 'makeappx/signtool not found' }

$proj = Join-Path $ProjectRoot 'src\MoonWeChat\MoonWeChat.csproj'
Write-Host "Build $Configuration|$Platform ..."
& $msb $proj /t:Rebuild /p:Configuration=$Configuration /p:Platform=$Platform /p:AppxPackageSigningEnabled=false /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed $LASTEXITCODE" }

$out = Join-Path $ProjectRoot "src\MoonWeChat\bin\$Platform\$Configuration"
$coreJunk = Join-Path $out 'Core'
if (Test-Path $coreJunk) { Remove-Item $coreJunk -Recurse -Force }
Get-ChildItem $out -Filter '*.pdb' -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $out -Filter '*.appxrecipe' -ErrorAction SilentlyContinue | Remove-Item -Force

$manifest = Join-Path $out 'AppxManifest.xml'
if (-not (Test-Path $manifest)) { throw "Missing $manifest" }

$xml = [xml](Get-Content $manifest -Raw -Encoding UTF8)
$ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace('a', $xml.DocumentElement.NamespaceURI)
$deps = $xml.SelectSingleNode('//a:Dependencies', $ns)
if ($null -eq $deps) { throw 'No Dependencies node' }
$hasCore = $false
foreach ($n in $deps.ChildNodes) {
    if ($n.LocalName -eq 'PackageDependency' -and $n.GetAttribute('Name') -eq 'Microsoft.NET.CoreRuntime.1.1') {
        $hasCore = $true
        break
    }
}
if (-not $hasCore) {
    $pd = $xml.CreateElement('PackageDependency', $xml.DocumentElement.NamespaceURI)
    $pd.SetAttribute('Name', 'Microsoft.NET.CoreRuntime.1.1')
    $pd.SetAttribute('MinVersion', '1.1.0.0')
    $pd.SetAttribute('Publisher', 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US')
    [void]$deps.AppendChild($pd)
    $xml.Save($manifest)
    Write-Host 'Injected CoreRuntime PackageDependency'
}

$pkgDir = Join-Path $ProjectRoot "src\MoonWeChat\AppPackages\MoonWeChat.WP10_1.0.0.0_${Platform}_${Configuration}"
New-Item -ItemType Directory -Path $pkgDir -Force | Out-Null
$appx = Join-Path $pkgDir "MoonWeChat.WP10_1.0.0.0_${Platform}_${Configuration}.appx"
if (Test-Path $appx) { Remove-Item $appx -Force }

Write-Host "Pack $appx"
& $makeappx pack /d $out /p $appx /o
if (-not (Test-Path $appx)) { throw 'makeappx failed' }

$pfx = Join-Path $ProjectRoot 'src\MoonWeChat.WP81\AppPackages\MoonWeChatDev.pfx'
# NOTE: do NOT name this $pwd — that is a PowerShell automatic variable holding the
# current directory (a PathInfo). Overwriting it silently breaks any later
# Join-Path/Resolve-Path that relies on it, and the breakage is far from here.
$pfxPassword = 'moon-wp81'
if (-not (Test-Path $pfx)) {
    $pfx = Join-Path $pkgDir 'MoonWeChatDev.pfx'
    $cerNew = Join-Path $pkgDir 'MoonWeChatDev.cer'
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=MoonWeChatDev' -KeyExportPolicy Exportable -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5)
    $sec = ConvertTo-SecureString $pfxPassword -AsPlainText -Force
    Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $sec | Out-Null
    Export-Certificate -Cert $cert -FilePath $cerNew | Out-Null
}

& $signtool sign /fd SHA256 /a /f $pfx /p $pfxPassword $appx
if ($LASTEXITCODE -ne 0) { throw 'sign failed' }

$cer = [IO.Path]::ChangeExtension($pfx, '.cer')
Copy-Item $pfx (Join-Path $pkgDir 'MoonWeChatDev.pfx') -Force
if (Test-Path $cer) { Copy-Item $cer (Join-Path $pkgDir 'MoonWeChatDev.cer') -Force }

$archFolder = if ($Platform -eq 'ARM') { 'ARM' } else { $Platform }
$depDir = Join-Path $pkgDir "Dependencies\$archFolder"
New-Item -ItemType Directory -Path $depDir -Force | Out-Null
$coreRoots = @{
    ARM = 'C:\Program Files (x86)\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\Microsoft.NET.CoreRuntime\1.1\AppX\arm\Microsoft.NET.CoreRuntime.1.1.appx'
    x64 = 'C:\Program Files (x86)\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\Microsoft.NET.CoreRuntime\1.1\AppX\x64\Microsoft.NET.CoreRuntime.1.1.appx'
    x86 = 'C:\Program Files (x86)\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\Microsoft.NET.CoreRuntime\1.1\AppX\x86\Microsoft.NET.CoreRuntime.1.1.appx'
}
$core = $coreRoots[$Platform]
if (Test-Path $core) { Copy-Item $core $depDir -Force }

Write-Host "OK $appx size=$((Get-Item $appx).Length)"
