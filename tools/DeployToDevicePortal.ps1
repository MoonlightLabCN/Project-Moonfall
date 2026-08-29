# Deploy MoonWeChat UWP (ARM) to Windows Device Portal (W10M / W10)
# Example:
#   powershell -ExecutionPolicy Bypass -File tools\DeployToDevicePortal.ps1 -DeviceHost 192.168.3.151
#   powershell -ExecutionPolicy Bypass -File tools\DeployToDevicePortal.ps1 -DeviceHost 192.168.3.151 -WaitMinutes 15 -Launch

param(
    [string]$DeviceHost = '192.168.3.151',
    [int]$Port = 80,
    [switch]$Https,
    [string]$User = '',
    [string]$Password = '',
    [int]$WaitMinutes = 0,
    [switch]$Launch,
    [switch]$SkipCert,
    [switch]$SkipDeps,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
# curl.exe uses -k for Device Portal's self-signed HTTPS certificate. The old
# ICertificatePolicy hook is unavailable in modern PowerShell/.NET runtimes.

$scheme = if ($Https) { 'https' } else { 'http' }
$base = "${scheme}://${DeviceHost}:$Port"
$pkgDirCandidates = @(
    (Join-Path $ProjectRoot 'src\MoonWeChat\AppPackages\MoonWeChat.WP10_1.0.0.0_ARM_Debug'),
    (Join-Path $ProjectRoot 'src\MoonWeChat\AppPackages\MoonWeChat_1.0.0.0_ARM_Debug_Test')
)
$pkgDir = $pkgDirCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $pkgDir) { throw "No WP10 ARM AppPackages folder under src\MoonWeChat\AppPackages" }
$appx = @(
    (Join-Path $pkgDir 'MoonWeChat.WP10_1.0.0.0_ARM_Debug.appx'),
    (Join-Path $pkgDir 'MoonWeChat_1.0.0.0_ARM_Debug.appx')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
$cer = Join-Path $pkgDir 'MoonWeChatDev.cer'
$coreRuntime = 'C:\Program Files (x86)\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\Microsoft.NET.CoreRuntime\1.1\AppX\arm\Microsoft.NET.CoreRuntime.1.1.appx'

if (-not (Test-Path $appx)) { throw "Missing appx: $appx`nBuild ARM package first (MSBuild Platform=ARM /t:Build + CreateAppPackage or VS Store package)." }
if (-not (Test-Path $cer)) { Write-Warning "Missing cert: $cer" }

function Get-PortalCredential {
    if ([string]::IsNullOrWhiteSpace($User)) { return $null }
    $sec = ConvertTo-SecureString $Password -AsPlainText -Force
    return New-Object System.Management.Automation.PSCredential ($User, $sec)
}

$cred = Get-PortalCredential

function Invoke-Portal {
    param(
        [string]$Method = 'GET',
        [string]$Path,
        [hashtable]$Headers = @{},
        [string]$InFile = $null,
        [string]$ContentType = $null,
        [int]$TimeoutSec = 120,
        [string]$OutFile = $null
    )
    $uri = "$base$Path"
    $args = @(
        '-sS', '-m', "$TimeoutSec",
        '-X', $Method,
        '-w', "`n__HTTP__:%{http_code}"
    )
    if ($Https) { $args += '-k' }
    if ($User) { $args += @('-u', "${User}:${Password}") }
    if ($ContentType) { $args += @('-H', "Content-Type: $ContentType") }
    foreach ($k in $Headers.Keys) { $args += @('-H', "${k}: $($Headers[$k])") }
    if ($InFile) { $args += @('--data-binary', "@$InFile") }
    if (-not $InFile -and $Method -in @('POST', 'PUT', 'PATCH')) { $args += @('--data-raw', '') }
    if ($OutFile) { $args += @('-o', $OutFile) } else { $args += @('-o', '-') }
    $args += $uri

    $raw = & curl.exe @args 2>&1 | Out-String
    if ($raw -match '__HTTP__:(\d+)') {
        $code = [int]$Matches[1]
        $body = ($raw -replace '(?s)\n__HTTP__:\d+\s*$', '').TrimEnd()
        return [pscustomobject]@{ Code = $code; Body = $body; Raw = $raw }
    }
    return [pscustomobject]@{ Code = 0; Body = $raw; Raw = $raw }
}

function Test-PortalUp {
    $r = Invoke-Portal -Path '/api/os/info' -TimeoutSec 6
    return ($r.Code -ge 200 -and $r.Code -lt 500)
}

function Wait-Portal {
    param([int]$Minutes)
    if ($Minutes -le 0) {
        if (-not (Test-PortalUp)) { throw "Device Portal not responding at $base (try reboot phone / toggle Device Portal)." }
        return
    }
    $deadline = (Get-Date).AddMinutes($Minutes)
    $i = 0
    while ((Get-Date) -lt $deadline) {
        $i++
        if (Test-PortalUp) {
            Write-Host "Portal UP after attempt $i"
            return
        }
        Write-Host "Portal down attempt $i ..."
        Start-Sleep -Seconds 5
    }
    throw "Portal still down after $Minutes min at $base"
}

function Install-AppxViaPortal {
    param(
        [Parameter(Mandatory)][string]$PackagePath,
        [string[]]$DependencyPaths = @()
    )
    if (-not (Test-Path $PackagePath)) { throw "Missing $PackagePath" }
    $name = [IO.Path]::GetFileName($PackagePath)
    $boundary = "----MoonWeChatBoundary$([guid]::NewGuid().ToString('N'))"
    $tmp = Join-Path $env:TEMP ("portal_upload_{0}.bin" -f ([guid]::NewGuid().ToString('N')))

    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    $fs = [IO.File]::Open($tmp, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    try {
        function Write-Text([string]$t) {
            $b = $utf8NoBom.GetBytes($t)
            $fs.Write($b, 0, $b.Length)
        }
        function Write-FilePart([string]$field, [string]$filePath) {
            $fn = [IO.Path]::GetFileName($filePath)
            Write-Text "--$boundary`r`n"
            Write-Text "Content-Disposition: form-data; name=`"$field`"; filename=`"$fn`"`r`n"
            Write-Text "Content-Type: application/octet-stream`r`n`r`n"
            $bytes = [IO.File]::ReadAllBytes($filePath)
            $fs.Write($bytes, 0, $bytes.Length)
            Write-Text "`r`n"
        }

        # Device Portal expects main package as form field often named with package query, body as multipart
        Write-FilePart -field $name -filePath $PackagePath
        $depQuery = ''
        foreach ($dep in $DependencyPaths) {
            if (-not (Test-Path $dep)) { Write-Warning "Skip missing dep $dep"; continue }
            $dname = [IO.Path]::GetFileName($dep)
            Write-FilePart -field $dname -filePath $dep
            $depQuery += "&dependency=" + [uri]::EscapeDataString($dname)
        }
        Write-Text "--$boundary--`r`n"
    } finally {
        $fs.Close()
    }

    $path = "/api/app/packagemanager/package?package=$([uri]::EscapeDataString($name))$depQuery"
    Write-Host "POST $path  size=$((Get-Item $tmp).Length) bytes"
    $r = Invoke-Portal -Method POST -Path $path -InFile $tmp -ContentType "multipart/form-data; boundary=$boundary" -TimeoutSec 300
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    Write-Host "Install response HTTP $($r.Code)"
    if ($r.Body) { Write-Host $r.Body.Substring(0, [Math]::Min(500, $r.Body.Length)) }
    if ($r.Code -lt 200 -or $r.Code -ge 300) {
        throw "Package install failed HTTP $($r.Code): $($r.Body)"
    }
    return $r
}

function Install-CertViaPortal {
    param([string]$CerPath)
    if (-not (Test-Path $CerPath)) { return }
    $name = [IO.Path]::GetFileName($CerPath)
    $boundary = "----MoonCert$([guid]::NewGuid().ToString('N'))"
    $tmp = Join-Path $env:TEMP ("portal_cer_{0}.bin" -f ([guid]::NewGuid().ToString('N')))
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    $fs = [IO.File]::Open($tmp, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    try {
        $header = "--$boundary`r`nContent-Disposition: form-data; name=`"$name`"; filename=`"$name`"`r`nContent-Type: application/x-x509-ca-cert`r`n`r`n"
        $hb = $utf8NoBom.GetBytes($header)
        $fs.Write($hb, 0, $hb.Length)
        $cb = [IO.File]::ReadAllBytes($CerPath)
        $fs.Write($cb, 0, $cb.Length)
        $end = $utf8NoBom.GetBytes("`r`n--$boundary--`r`n")
        $fs.Write($end, 0, $end.Length)
    } finally { $fs.Close() }

    # W10M Device Portal certificate endpoints vary; try both
    foreach ($p in @(
            "/api/app/packagemanager/certificate?package=$([uri]::EscapeDataString($name))",
            "/api/appx/cert?filename=$([uri]::EscapeDataString($name))"
        )) {
        Write-Host "Try cert POST $p"
        $r = Invoke-Portal -Method POST -Path $p -InFile $tmp -ContentType "multipart/form-data; boundary=$boundary" -TimeoutSec 60
        Write-Host "Cert HTTP $($r.Code) $($r.Body.Substring(0, [Math]::Min(200, $r.Body.Length)))"
        if ($r.Code -ge 200 -and $r.Code -lt 300) {
            Remove-Item $tmp -Force -ErrorAction SilentlyContinue
            return $r
        }
    }
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    Write-Warning 'Certificate install via portal failed (device may already trust, or install .cer manually in Device Portal).'
}

function Wait-InstallIdle {
    for ($i = 1; $i -le 60; $i++) {
        $r = Invoke-Portal -Path '/api/app/packagemanager/state' -TimeoutSec 15
        if ($r.Code -eq 200) {
            Write-Host "state: $($r.Body)"
            if ($r.Body -match '"Success"\s*:\s*false') {
                throw "Device Portal reported deployment failure: $($r.Body)"
            }
            if ($r.Body -notmatch '"IsRunning"\s*:\s*true' -and
                $r.Body -notmatch '"State"\s*:\s*1' -and
                $r.Body -notmatch 'Running') {
                return
            }
        } elseif ($r.Code -eq 404) {
            return
        }
        Start-Sleep -Seconds 2
    }
}

function Get-InstalledPackages {
    $r = Invoke-Portal -Path '/api/app/packagemanager/packages' -TimeoutSec 30
    if ($r.Code -ne 200) { throw "List packages failed HTTP $($r.Code)" }
    return $r.Body
}

function Start-AppOnDevice {
    param(
        [Parameter(Mandatory)][string]$PackageRelativeId,
        [Parameter(Mandatory)][string]$PackageFullName
    )
    # Device Portal calls these values hex64, which is its name for Base64.
    $appid64 = [uri]::EscapeDataString([Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($PackageRelativeId)))
    $package64 = [uri]::EscapeDataString([Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($PackageFullName)))
    $path = "/api/taskmanager/app?appid=$appid64&package=$package64"
    $r = Invoke-Portal -Method POST -Path $path -TimeoutSec 30
    Write-Host "Launch HTTP $($r.Code) $($r.Body)"
    return $r
}

Write-Host "Target: $base"
Write-Host "Appx:   $appx  ($([math]::Round((Get-Item $appx).Length/1MB,2)) MB)"

Wait-Portal -Minutes $WaitMinutes

$info = Invoke-Portal -Path '/api/os/info' -TimeoutSec 10
Write-Host "OS: $($info.Body)"

if (-not $SkipCert -and (Test-Path $cer)) {
    Install-CertViaPortal -CerPath $cer
}

if (-not $SkipDeps -and (Test-Path $coreRuntime)) {
    Write-Host "Installing CoreRuntime ARM..."
    try {
        Install-AppxViaPortal -PackagePath $coreRuntime
        Wait-InstallIdle
    } catch {
        Write-Warning "CoreRuntime install note: $($_.Exception.Message)"
    }
} elseif (-not $SkipDeps) {
    Write-Warning "CoreRuntime ARM appx not found at $coreRuntime"
}

Write-Host 'Installing MoonWeChat...'
Install-AppxViaPortal -PackagePath $appx
Wait-InstallIdle

$pkgs = Get-InstalledPackages
if ($pkgs -match 'MoonWeChat') {
    Write-Host 'SUCCESS: MoonWeChat is listed in installed packages.'
} else {
    Write-Warning 'MoonWeChat string not found in package list response (check body).'
    Write-Host $pkgs.Substring(0, [Math]::Min(800, $pkgs.Length))
}

if ($Launch) {
    $package = ($pkgs | ConvertFrom-Json).InstalledPackages |
        Where-Object { $_.PackageFamilyName -like 'MoonWeChat*WP10*' -or $_.Name -like '*月微信 WP10*' } |
        Select-Object -First 1
    if ($package) {
        Write-Host "Launching $($package.PackageRelativeId)"
        Start-AppOnDevice -PackageRelativeId $package.PackageRelativeId -PackageFullName $package.PackageFullName | Out-Null
    } else {
        Write-Warning 'Could not resolve the installed WP10 package for launch.'
    }
}

Write-Host 'Done.'
