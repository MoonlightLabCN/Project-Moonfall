param(
    [int]$Port = 18765,
    [switch]$Mock
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    Write-Error "未找到 python。请先安装 Python 3.10+ 并勾选 Add to PATH。"
}

$argsList = @("gateway.py", "--host", "0.0.0.0", "--port", "$Port")
if ($Mock) {
    $argsList += "--mock"
}

Write-Host "启动 pyweixin 网关: http://0.0.0.0:$Port"
Write-Host "手机请填本机局域网 IP，例如 http://192.168.x.x:$Port"
python @argsList
