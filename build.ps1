param(
    [switch]$Clean,
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$icon = Join-Path $root 'app.ico'
$manifest = Join-Path $root 'app.manifest'
$exe = Join-Path $dist 'AudioDeck.exe'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "版本号必须形如 1.2.3，当前为: $Version"
}

$assemblyVersion = $Version + '.0'

if (-not (Test-Path $csc)) {
    throw "未找到 .NET Framework 4.x 编译器: $csc"
}

if ($Clean -and (Test-Path $dist)) {
    Remove-Item -LiteralPath $dist -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

if (-not (Test-Path $icon)) {
    & (Join-Path $root 'tools\make-icon.ps1') -Output $icon
}

$generatedVersion = Join-Path $dist 'GeneratedVersionInfo.cs'
$versionSource = @"
using System.Reflection;

[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$assemblyVersion")]
[assembly: AssemblyInformationalVersion("$Version")]
"@
[System.IO.File]::WriteAllText($generatedVersion, $versionSource,
    (New-Object System.Text.UTF8Encoding($false)))

$sources = @(Get-ChildItem -LiteralPath $src -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName })
$sources += $generatedVersion
if ($sources.Count -eq 1) {
    throw '没有找到任何源文件。'
}

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    '/codepage:65001',
    ('/win32icon:{0}' -f $icon),
    ('/win32manifest:{0}' -f $manifest),
    ('/out:{0}' -f $exe),
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
) + $sources

& $csc $arguments
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码 $LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $root 'AudioDeck.exe.config') -Destination (Join-Path $dist 'AudioDeck.exe.config') -Force

$size = (Get-Item -LiteralPath $exe).Length
$fileVersion = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
Write-Host ("构建完成: {0} ({1:N0} bytes, version {2})" -f $exe, $size, $fileVersion)
