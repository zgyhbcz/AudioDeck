<#
    声控台 AudioDeck - 卸载脚本
    不需要管理员权限。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\AudioDeck'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AudioDeck'
$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\声控台.lnk'

Write-Host '正在卸载声控台...' -ForegroundColor Cyan

Get-Process -Name 'AudioDeck' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 350

Remove-ItemProperty -Path $runKey -Name 'AudioDeck' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue
Remove-Item -Path $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'AudioDeck') -Recurse -Force -ErrorAction SilentlyContinue

$installedExe = Join-Path $installDir 'AudioDeck.exe'
$notifyRoot = 'HKCU:\Control Panel\NotifyIconSettings'
if (Test-Path $notifyRoot) {
    Get-ChildItem $notifyRoot -ErrorAction SilentlyContinue | ForEach-Object {
        $entry = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
        if ($entry -and $entry.ExecutablePath -and
            [string]::Equals([string]$entry.ExecutablePath, $installedExe,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -Path $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Set-Location $env:TEMP

$insideInstallDir = $false
try {
    $insideInstallDir = (Resolve-Path -LiteralPath $PSScriptRoot).Path -eq (Resolve-Path -LiteralPath $installDir).Path
} catch {
}

if ($insideInstallDir) {
    # 从安装目录运行时，让 cmd 在 PowerShell 退出后再删除目录（包括本脚本自身）。
    $command = 'timeout /t 2 /nobreak >nul & rmdir /s /q "' + $installDir + '"'
    Start-Process -FilePath $env:ComSpec -ArgumentList '/c', $command -WindowStyle Hidden
} else {
    Remove-Item -LiteralPath $installDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host '声控台已卸载。' -ForegroundColor Green
