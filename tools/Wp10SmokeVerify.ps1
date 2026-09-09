param([string]$Configuration = 'Debug')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\MoonWeChat\MoonWeChat.csproj'
$tmp = Join-Path $root '_tmp'
$env:TEMP = $tmp
$env:TMP = $tmp
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$failures = New-Object System.Collections.Generic.List[string]

function Ok($message) {
    Write-Host "OK   $message" -ForegroundColor Green
}

function Fail($message) {
    $failures.Add($message)
    Write-Host "FAIL $message" -ForegroundColor Red
}

function Test-BinaryToken($path, $token) {
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    $bytes = [IO.File]::ReadAllBytes($path)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    if ($ascii.Contains($token)) { return $true }
    $unicode = [Text.Encoding]::Unicode.GetString($bytes)
    return $unicode.Contains($token)
}

foreach ($platform in @('x86', 'x64', 'ARM')) {
    try {
        & $msbuild $project /t:Rebuild "/p:Configuration=$Configuration" "/p:Platform=$platform" /v:minimal | Out-Null
        if ($LASTEXITCODE -eq 0) { Ok "Rebuild $platform" } else { Fail "Rebuild $platform" }
    }
    catch {
        Fail "Rebuild $platform ($($_.Exception.Message))"
    }
}

$output = Join-Path $root "src\MoonWeChat\bin\x64\$Configuration"
$binaryLayout = $output
$layout = Join-Path $output 'AppX'
if (-not (Test-Path -LiteralPath $layout)) {
    # Some UWP targets stage a directly deployable layout at bin\<platform>\<configuration>.
    $layout = $output
}

# Visual Studio's UWP targets commonly put the deployable layout only inside
# the generated AppPackages\*.appx archive. Extract that archive when the
# intermediate bin directory does not contain assets, so checks exercise the
# same files that would be installed on a device.
$extractRoot = Join-Path $tmp ('Wp10Smoke.' + [guid]::NewGuid().ToString('N'))
if (-not (Test-Path -LiteralPath (Join-Path $layout 'Assets'))) {
    $builtAppx = Get-ChildItem (Join-Path $root 'src\MoonWeChat\AppPackages') -Filter "*_x64_${Configuration}.appx" -Recurse -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($builtAppx) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($builtAppx.FullName, $extractRoot)
        $layout = $extractRoot
    }
}

$manifest = Join-Path $layout 'AppxManifest.xml'
if (Test-Path -LiteralPath $manifest) {
    [xml]$xml = Get-Content -LiteralPath $manifest
    if ($xml.Package.Identity.Name -like '*WP10*') { Ok 'Identity.Name contains WP10' } else { Fail 'Identity.Name missing WP10' }
    if ($xml.Package.Dependencies.TargetDeviceFamily.MinVersion -eq '10.0.10240.0') { Ok 'MinVersion 10.0.10240.0' } else { Fail 'MinVersion incorrect' }
}
else {
    Fail 'AppxManifest.xml missing'
}

foreach ($asset in @('Square44x44Logo.png', 'Square71x71Logo.png', 'Square150x150Logo.png', 'Square310x310Logo.png', 'Wide310x150Logo.png', 'StoreLogo.png', 'SplashScreen.png', 'LockScreenLogo.png')) {
    $path = Join-Path $layout "Assets\$asset"
    if ((Test-Path -LiteralPath $path) -and (Get-Item -LiteralPath $path).Length -gt 0) { Ok "asset $asset" } else { Fail "asset $asset" }
}

$entryXbf = Join-Path $binaryLayout 'Views\ShellPage.xbf'
if (Test-Path -LiteralPath $entryXbf) { Ok 'Views\ShellPage.xbf exists' } else { Fail 'Views\ShellPage.xbf missing' }

$exe = Join-Path $binaryLayout 'MoonWeChat.exe'
foreach ($token in @('ShellPage', 'ChatViewModel', 'AppNavigation', 'TryGoBack')) {
    if (Test-BinaryToken $exe $token) { Ok "binary token $token" } else { Fail "binary token $token" }
}

if ((Get-Content -Raw -LiteralPath $project) -match 'MoonWeChat.Core\\MoonWeChat.Core.projitems') { Ok 'Core projitems imported' } else { Fail 'Core projitems import missing' }

$makeappx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
$appx = Join-Path $tmp ('MoonWeChat.WP10.' + [guid]::NewGuid().ToString('N') + '.appx')
if ($makeappx) {
    & $makeappx.FullName pack /d $layout /p $appx /o | Out-Null
    if (($LASTEXITCODE -eq 0) -and (Test-Path -LiteralPath $appx) -and (Get-Item -LiteralPath $appx).Length -gt 50000) { Ok 'MakeAppx pack' } else { Fail 'MakeAppx pack failed' }
    Remove-Item -LiteralPath $appx -Force -ErrorAction SilentlyContinue
}
else {
    Fail 'MakeAppx.exe not found'
}

if (Test-Path -LiteralPath $extractRoot) {
    Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count) {
    Write-Host "WP10 SMOKE FAILED ($($failures.Count))"
    $failures
    exit 1
}

Write-Host 'WP10 SMOKE PASSED' -ForegroundColor Green
