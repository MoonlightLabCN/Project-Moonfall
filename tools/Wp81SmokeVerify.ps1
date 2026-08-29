# WP 8.1 shell smoke verification against REAL build outputs.
# Exit 0 = pass. Does not mock AppNavigation / ThemeService / package contents.
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$msbCandidates = @(
    'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe',
    'C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe'
)
$msb = $msbCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $msb) {
    Write-Host "FAIL: MSBuild for WP 8.1 not found. Tried:" -ForegroundColor Red
    $msbCandidates | ForEach-Object { Write-Host "  $_" }
    exit 1
}
$proj = Join-Path $ProjectRoot 'src\MoonWeChat.WP81\MoonWeChat.WP81.csproj'
$out = Join-Path $ProjectRoot 'src\MoonWeChat.WP81\bin\x86\Debug'
$failures = New-Object System.Collections.Generic.List[string]

function Fail($msg) { [void]$failures.Add($msg); Write-Host "FAIL: $msg" -ForegroundColor Red }
function Ok($msg) { Write-Host "OK: $msg" -ForegroundColor Green }

# --- 1) Rebuild shipped WP81 binary ---
$env:TEMP = Join-Path $ProjectRoot '_tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
& $msb $proj /t:Rebuild /p:Configuration=Debug /p:Platform=x86 /v:m /nologo
if ($LASTEXITCODE -ne 0) { Fail "MSBuild failed exit=$LASTEXITCODE" } else { Ok "MSBuild Debug|x86" }

$exe = Join-Path $out 'MoonWeChat.exe'
$manifest = Join-Path $out 'AppxManifest.xml'
if (-not (Test-Path $exe)) { Fail "Missing $exe" } else { Ok "MoonWeChat.exe exists ($((Get-Item $exe).Length) bytes)" }
if (-not (Test-Path $manifest)) { Fail "Missing AppxManifest.xml" } else { Ok "AppxManifest.xml exists" }

# --- 2) Manifest is WP 8.1 WinRT ---
[xml]$mx = Get-Content $manifest
$idName = $mx.Package.Identity.Name
$osMin = $mx.Package.Prerequisites.OSMinVersion
if ($idName -notmatch 'WP81') { Fail "Identity.Name should mark WP81 package, got $idName" } else { Ok "Identity.Name=$idName" }
if ($osMin -ne '6.3.1') { Fail "OSMinVersion expected 6.3.1 got $osMin" } else { Ok "OSMinVersion=6.3.1" }
$tfm = Select-String -Path $manifest -Pattern 'WindowsPhoneApp,Version=v8.1' -SimpleMatch
if (-not $tfm) { Fail 'TargetFrameworkMoniker WindowsPhoneApp v8.1 missing in manifest metadata' } else { Ok 'TargetFrameworkMoniker WindowsPhoneApp v8.1' }

# --- 3) Assets must ship (splash/logo) ---
$requiredAssets = @(
    'Assets\Logo.png','Assets\SmallLogo.png','Assets\SplashScreen.png',
    'Assets\Square71x71Logo.png','Assets\StoreLogo.png','Assets\WideLogo.png'
)
foreach ($a in $requiredAssets) {
    $p = Join-Path $out $a
    if (-not (Test-Path $p)) { Fail "Missing packaged asset $a" }
    elseif ((Get-Item $p).Length -lt 10) { Fail "Asset too small: $a" }
    else { Ok "Asset $a ($((Get-Item $p).Length)b)" }
}

# --- 4) Compiled XBF for entry pages must exist ---
foreach ($page in @('App.xbf','Views\WelcomePage.xbf','Views\ChatListPage.xbf','Views\LoginPage.xbf')) {
    $p = Join-Path $out $page
    if (-not (Test-Path $p)) { Fail "Missing $page" } else { Ok "XBF $page" }
}

# --- 5) Source gate: themes must NOT be hot-swapped before the first frame ---
# The real crash (0xc000027b) came from touching
# Application.Resources.MergedDictionaries in the App constructor / before the
# window is activated. Applying the saved theme AFTER Window.Current.Activate()
# is both safe and required — otherwise the user's chosen theme is lost on every
# restart. So the gate checks *position*, not mere presence.
$appCs = Join-Path $ProjectRoot 'src\MoonWeChat.WP81\App.xaml.cs'
$appText = Get-Content $appCs -Raw

$activateIdx = $appText.IndexOf('Window.Current.Activate()', [StringComparison]::Ordinal)
$themeMatches = @([regex]::Matches($appText, 'ThemeService\.ApplyFromSettings\s*\('))
# Drop occurrences that sit inside a comment line.
$realThemeCalls = @($themeMatches | Where-Object {
    $lineStart = $appText.LastIndexOf("`n", $_.Index) + 1
    $line = $appText.Substring($lineStart, $_.Index - $lineStart)
    $line.Trim() -notmatch '^//'
})

if ($realThemeCalls.Count -eq 0) {
    Ok 'App.xaml.cs does not call ThemeService.ApplyFromSettings()'
} elseif ($activateIdx -lt 0) {
    Fail 'App.xaml.cs calls ApplyFromSettings() but never calls Window.Current.Activate()'
} else {
    $early = @($realThemeCalls | Where-Object { $_.Index -lt $activateIdx })
    if ($early.Count -gt 0) {
        Fail 'ThemeService.ApplyFromSettings() called BEFORE Window.Current.Activate() (0xc000027b crash path)'
    } else {
        Ok 'ThemeService.ApplyFromSettings() only after Window.Current.Activate()'
    }
}
if ($appText -notmatch 'InitializeComponent\s*\(') {
    Fail 'App.xaml.cs missing InitializeComponent'
} else { Ok 'App.xaml.cs has InitializeComponent' }
if ($appText -notmatch 'AppNavigation\.ResolveLaunchPage') {
    Fail 'App.xaml.cs must navigate via AppNavigation.ResolveLaunchPage'
} else { Ok 'Launch uses AppNavigation.ResolveLaunchPage' }

# --- 6) Shipped PE must contain critical type/method name tokens (no mock) ---
$bytes = [System.IO.File]::ReadAllBytes($exe)
# UTF-8 / UTF-16LE search for managed metadata strings
$textUtf8 = [System.Text.Encoding]::UTF8.GetString($bytes)
$textUtf16 = [System.Text.Encoding]::Unicode.GetString($bytes)
$blob = $textUtf8 + "`n" + $textUtf16
$needles = @(
    'MoonWeChat.App',
    'AppNavigation',
    'ResolveLaunchPage',
    'WelcomePage',
    'ChatListPage',
    'WeChatPadApiClient',
    'SessionBootstrap',
    'HardwareButtons',
    'FilePickerContinuation'
)
foreach ($n in $needles) {
    if ($blob.IndexOf($n, [StringComparison]::Ordinal) -lt 0) {
        Fail "Shipped MoonWeChat.exe missing token: $n"
    } else {
        Ok "Binary contains token: $n"
    }
}

# --- 7) ThemeService.ApplyFromSettings must not be the only path; ensure Apply exists for settings page ---
$coreTheme = Join-Path $ProjectRoot 'src\MoonWeChat.Core\Services\ThemeService.cs'
$themeText = Get-Content $coreTheme -Raw
if ($themeText -notmatch 'public static void Apply\(') { Fail 'ThemeService.Apply missing in Core' } else { Ok 'ThemeService.Apply present in Core' }

# --- 8) Shared Core is imported by WP81 csproj ---
$csproj = Get-Content (Join-Path $ProjectRoot 'src\MoonWeChat.WP81\MoonWeChat.WP81.csproj') -Raw
if ($csproj -notmatch 'MoonWeChat\.Core\.projitems') { Fail 'WP81 csproj does not import Core projitems' } else { Ok 'WP81 imports MoonWeChat.Core' }
if ($csproj -notmatch 'WINDOWS_PHONE_APP') { Fail 'WP81 missing WINDOWS_PHONE_APP define' } else { Ok 'WINDOWS_PHONE_APP defined' }

# --- 9) Real MakeAppx pack of layout (proves package payload is valid) ---
$makeappx = @(
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.17763.0\x64\makeappx.exe',
    'C:\Program Files (x86)\Windows Kits\10\bin\10.0.15063.0\x64\makeappx.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
$appxOut = Join-Path $env:TEMP ('MoonWeChat.WP81.smoke.' + [guid]::NewGuid().ToString('N') + '.appx')
if ($makeappx) {
    & $makeappx pack /d $out /p $appxOut /o | Out-Null
    if ((Test-Path $appxOut) -and ((Get-Item $appxOut).Length -gt 50000)) {
        Ok ("MakeAppx pack ok size=" + (Get-Item $appxOut).Length)
    } else {
        Fail 'MakeAppx pack failed or package too small'
    }
    Remove-Item $appxOut -Force -ErrorAction SilentlyContinue
} else {
    Fail 'MakeAppx.exe not found'
}

# --- 10) AppDeploy device list (live tool path). Emulator CoreCon may still timeout on host. ---
$deploy = 'C:\Program Files (x86)\Microsoft SDKs\Windows Phone\v8.1\Tools\AppDeploy\AppDeployCmd.exe'
$deployNote = 'skipped'
if (Test-Path $deploy) {
    $enum = & $deploy /EnumerateDevices 2>&1 | Out-String
    if ($enum -match 'Emulator 8\.1') {
        Ok 'AppDeploy sees Emulator 8.1 targets'
        $deployNote = 'emulator-listed'
    } else {
        Fail 'AppDeploy did not list Emulator 8.1'
    }
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "WP81 SMOKE FAILED ($($failures.Count))" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host " - $_" }
    exit 1
}
Write-Host "WP81 SMOKE PASSED (deploy=$deployNote)" -ForegroundColor Green
exit 0
