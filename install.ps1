<#
    声控台 AudioDeck - 安装脚本
    不需要管理员权限。
#>
[CmdletBinding()]
param(
    [switch]$NoLaunch,
    [switch]$NoStartup
)

$ErrorActionPreference = 'Stop'
$sourceDir = $PSScriptRoot
$sourceExe = Join-Path $sourceDir 'AudioDeck.exe'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\AudioDeck'
$installedExe = Join-Path $installDir 'AudioDeck.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AudioDeck'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $startMenu '声控台.lnk'

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "安装包不完整：找不到 $sourceExe"
}

$displayVersion = (Get-Item -LiteralPath $sourceExe).VersionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($displayVersion)) {
    $displayVersion = '1.0.1'
}

$release = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
if ($release -and $release -lt 528040) {
    throw '需要 .NET Framework 4.8 或更高版本。Windows 11 已内置。'
}

Write-Host '正在安装声控台...' -ForegroundColor Cyan

Get-Process -Name 'AudioDeck' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 350

New-Item -ItemType Directory -Force -Path $installDir | Out-Null

if ((Resolve-Path -LiteralPath $sourceDir).Path -ne (Resolve-Path -LiteralPath $installDir).Path) {
    Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force
    $configSource = Join-Path $sourceDir 'AudioDeck.exe.config'
    if (Test-Path -LiteralPath $configSource) {
        Copy-Item -LiteralPath $configSource -Destination (Join-Path $installDir 'AudioDeck.exe.config') -Force
    }

    foreach ($name in @('uninstall.ps1', 'README.md', 'README.en.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'CHANGELOG.md')) {
        $file = Join-Path $sourceDir $name
        if (Test-Path -LiteralPath $file) {
            Copy-Item -LiteralPath $file -Destination (Join-Path $installDir $name) -Force
        }
    }

    $previewDir = Join-Path $sourceDir 'previews'
    if (Test-Path -LiteralPath $previewDir) {
        Copy-Item -LiteralPath $previewDir -Destination (Join-Path $installDir 'previews') -Recurse -Force
    }
}

# 开机启动（可通过托盘菜单随时关闭）
if (-not $NoStartup) {
    if (-not (Test-Path $runKey)) {
        New-Item -Path $runKey -Force | Out-Null
    }
    Set-ItemProperty -Path $runKey -Name 'AudioDeck' -Value ('"' + $installedExe + '" --startup')
} else {
    Remove-ItemProperty -Path $runKey -Name 'AudioDeck' -ErrorAction SilentlyContinue
}

# 开始菜单快捷方式
if (-not (Test-Path -LiteralPath $startMenu)) {
    New-Item -ItemType Directory -Force -Path $startMenu | Out-Null
}
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$installedExe,0"
$shortcut.Description = '声控台 - 输出设备与系统音量'
$shortcut.Save()

# 控制面板“应用和功能”里的卸载入口
if (-not (Test-Path $uninstallKey)) {
    New-Item -Path $uninstallKey -Force | Out-Null
}
$uninstallString = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $installDir 'uninstall.ps1') + '"'
Set-ItemProperty -Path $uninstallKey -Name 'DisplayName' -Value '声控台 AudioDeck'
Set-ItemProperty -Path $uninstallKey -Name 'DisplayVersion' -Value $displayVersion
Set-ItemProperty -Path $uninstallKey -Name 'Publisher' -Value 'AudioDeck'
Set-ItemProperty -Path $uninstallKey -Name 'DisplayIcon' -Value "$installedExe,0"
Set-ItemProperty -Path $uninstallKey -Name 'InstallLocation' -Value $installDir
Set-ItemProperty -Path $uninstallKey -Name 'UninstallString' -Value $uninstallString
Set-ItemProperty -Path $uninstallKey -Name 'QuietUninstallString' -Value $uninstallString
Set-ItemProperty -Path $uninstallKey -Name 'NoModify' -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name 'NoRepair' -Value 1 -Type DWord
$sizeKb = [int]((Get-Item -LiteralPath $installedExe).Length / 1KB)
Set-ItemProperty -Path $uninstallKey -Name 'EstimatedSize' -Value $sizeKb -Type DWord

if (-not $NoLaunch) {
    Start-Process -FilePath $installedExe
    Start-Sleep -Seconds 2

    # Windows 11 会记住每个程序的托盘图标是否放在任务栏上。首次运行时把它设为显示，
    # 这样用户不用去“其他系统托盘图标”里手动打开。
    $promoted = $false
    $notifyRoot = 'HKCU:\Control Panel\NotifyIconSettings'
    if (Test-Path $notifyRoot) {
        $keys = @(Get-ChildItem $notifyRoot -ErrorAction SilentlyContinue)
        foreach ($key in $keys) {
            $entry = Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue
            if ($entry -and $entry.ExecutablePath -and
                [string]::Equals([string]$entry.ExecutablePath, $installedExe,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                Set-ItemProperty -Path $key.PSPath -Name 'IsPromoted' -Value 1 -Type DWord
                $promoted = $true
                break
            }
        }
    }

    if ($promoted) {
        Start-Sleep -Milliseconds 350
        Get-Process -Name 'AudioDeck' -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Milliseconds 450
        Start-Process -FilePath $installedExe
    }
}

Write-Host ''
Write-Host '安装完成。' -ForegroundColor Green
Write-Host "程序位置：$installDir"
Write-Host '单击任务栏右下角的蓝色扬声器图标，打开设备与音量面板。'
Write-Host '快捷键：Ctrl+Alt+A'
Write-Host ''
Write-Host '如果任务栏没有显示图标：点击任务栏的 ^，把“声控台”拖到任务栏上；'
Write-Host '或打开 设置 > 个性化 > 任务栏 > 其他系统托盘图标，将声控台设为“开”。'
Write-Host '也可以把开始菜单里的“声控台”右键固定到任务栏。'
