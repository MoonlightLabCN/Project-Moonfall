#Requires -RunAsAdministrator
<#
.SYNOPSIS
  修复 WP 8.1 模拟器 Hyper-V 无法设置 VHD 安全信息 (0x80070005) 的问题。
  症状: IpOverUsbEnum 一直 "No connected partners found"，AppDeploy Connect 超时。
.NOTES
  必须以管理员 PowerShell 运行。
#>
$ErrorActionPreference = 'Stop'

$targets = @(
  'C:\Program Files (x86)\Microsoft SDKs\Windows Phone\v8.1\Emulation\Images',
  (Join-Path $env:LOCALAPPDATA 'Microsoft\XDE\8.1')
)

# NT VIRTUAL MACHINE\Virtual Machines
$vmSid = '*S-1-5-83-0'

foreach ($p in $targets) {
  if (-not (Test-Path $p)) {
    Write-Warning "Skip missing: $p"
    continue
  }
  Write-Host "Grant FullControl to Virtual Machines + SYSTEM on $p"
  icacls $p /grant "${vmSid}:(OI)(CI)F" /T /C
  icacls $p /grant "SYSTEM:(OI)(CI)F" /T /C
}

Write-Host ''
Write-Host 'Done. Next:'
Write-Host '  1. Close all XDE windows'
Write-Host '  2. Stop-VM all "Emulator 8.1*" if running'
Write-Host '  3. Start Emulator from VS2017 (MoonWeChat.WP81 F5)'
Write-Host '  4. Or: AppDeployCmd /installlaunch <appx> /targetdevice:7'
Write-Host ''
Write-Host 'Verify partners:'
Write-Host '  & "C:\Program Files (x86)\Common Files\Microsoft Shared\Phone Tools\CoreCon\11.0\bin\IpOverUsbEnum.exe"'
