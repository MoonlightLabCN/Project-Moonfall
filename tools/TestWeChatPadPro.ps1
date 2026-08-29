# MoonWeChat WeChatPadPro backend smoke test
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\TestWeChatPadPro.ps1 -BaseUrl http://host:8062 [-AdminKey ...] [-Token ...]
#   powershell -ExecutionPolicy Bypass -File tools\TestWeChatPadPro.ps1 -CheckAdmin   # also verify the AdminKey
# If BaseUrl is omitted, the script tries common local addresses and reads _wechatpadpro\MOON_CREDS.txt.
#
# Credentials are never echoed: this script's stdout is routinely redirected into a
# log file, and the response bodies contain tokens / wxid / nickname verbatim.
param(
    [string]$BaseUrl = "",
    [string]$AdminKey = "",
    [string]$Token = "",
    # Off by default: /Admin/GenAuthKey MINTS A REAL TOKEN as a side effect.
    [switch]$CheckAdmin
)

$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$credsFile = Join-Path $root '_wechatpadpro\MOON_CREDS.txt'

function Parse-CredsFile([string]$path) {
    if (-not (Test-Path $path)) { return }
    Get-Content $path -Encoding UTF8 | ForEach-Object {
        if ($_ -match '^\s*AdminKey\s*=\s*(\S+)') { $script:AdminKey = $matches[1] }
        if ($_ -match '^\s*Token\s*=\s*(\S+)') { $script:Token = $matches[1] }
        if ($_ -match '^\s*BaseUrl\s*=\s*(https?://\S+)') { $script:BaseUrl = $matches[1] }
    }
}

if ([string]::IsNullOrWhiteSpace($AdminKey) -or [string]::IsNullOrWhiteSpace($Token)) {
    Parse-CredsFile $credsFile
}

$candidates = @()
if (-not [string]::IsNullOrWhiteSpace($BaseUrl)) {
    $candidates += $BaseUrl
} else {
    $candidates += @('http://127.0.0.1:8062', 'http://127.0.0.1:1238')
}
$candidates = $candidates | Select-Object -Unique

function Test-Api([string]$base) {
    $base = $base.TrimEnd('/')
    Write-Output "== $base =="

    # 1) Root reachability (no auth)
    try {
        $r = Invoke-WebRequest -Uri "$base/" -Method Get -TimeoutSec 8 -UseBasicParsing
        Write-Output ("  root: HTTP {0}" -f $r.StatusCode)
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        Write-Output ("  root: FAIL {0} {1}" -f $code, $_.Exception.Message)
    }

    # 2) Access token check: GET /api/Login/GetCacheInfo (v8 swagger) then legacy /Login/GetLoginStatus
    #    Only the status line and a verdict are printed. The body carries wxid /
    #    nickname / session fields, and this script's stdout routinely gets
    #    redirected into a log file.
    foreach ($path in @('/api/Login/GetCacheInfo', '/Login/GetLoginStatus')) {
        try {
            $r = Invoke-WebRequest -Uri "$base$path" -Method Get -Headers @{ 'X-Access-Token' = $Token } -TimeoutSec 8 -UseBasicParsing
            $verdict = 'unexpected body'
            if ($r.Content -match '"?Success"?\s*:\s*true') { $verdict = 'token accepted' }
            elseif ($r.Content -match '不存在|未登录|NoLogin') { $verdict = 'token rejected / not logged in' }
            Write-Output ("  token {0}: HTTP {1} -> {2} ({3} bytes)" -f $path, $r.StatusCode, $verdict, $r.Content.Length)
            break
        } catch {
            $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
            Write-Output ("  token {0}: FAIL {1} {2}" -f $path, $code, $_.Exception.Message)
        }
    }

    # 3) Admin credential check.
    #    NOTE: /Admin/GenAuthKey MINTS A REAL TOKEN as a side effect — it is not a
    #    read-only probe. So it is opt-in (-CheckAdmin) rather than something a
    #    "smoke test" does on every run, and the minted token is never printed:
    #    the response body contains it verbatim.
    if (-not $CheckAdmin) {
        Write-Output '  admin: skipped (pass -CheckAdmin to verify the AdminKey; it mints a token)'
        return
    }
    try {
        $body = '{"count":1,"days":1,"remark":"moonwechat-smoke"}'
        $r = Invoke-WebRequest -Uri "$base/api/Admin/GenAuthKey" -Method Post -Headers @{ 'X-Admin-Token' = $AdminKey } -ContentType 'application/json' -Body $body -TimeoutSec 10 -UseBasicParsing
        $verdict = 'unexpected body'
        if ($r.Content -match '"?Success"?\s*:\s*true') { $verdict = 'AdminKey accepted (a new token was minted, not shown)' }
        elseif ($r.Content -match '无效|invalid|-2') { $verdict = 'AdminKey rejected' }
        Write-Output ("  admin: HTTP {0} -> {1}" -f $r.StatusCode, $verdict)
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        Write-Output ("  admin: FAIL {0} {1}" -f $code, $_.Exception.Message)
    }
}

foreach ($base in $candidates) {
    Test-Api $base
}
