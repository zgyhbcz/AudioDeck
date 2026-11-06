param(
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'
$releaseRoot = Join-Path $root 'release'
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "版本号必须形如 1.2.3，当前为: $Version"
}
$zip = Join-Path $releaseRoot ('AudioDeck-' + $Version + '.zip')
$exe = Join-Path $dist 'AudioDeck.exe'

if (-not (Test-Path -LiteralPath $exe)) {
    throw "找不到 $exe，请先运行 build.ps1"
}

$exeVersion = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
if ($exeVersion -ne $Version) {
    throw "dist\AudioDeck.exe 的版本是 $exeVersion，但打包版本是 $Version。请先按相同版本运行 build.ps1。"
}

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ('AudioDeck-package-' + [Guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'previews') | Out-Null

    Copy-Item -LiteralPath $exe -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'AudioDeck.exe.config') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'install.ps1') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'uninstall.ps1') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'README.en.md') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'CHANGELOG.md') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'previews\light.png') -Destination (Join-Path $staging 'previews')
    Copy-Item -LiteralPath (Join-Path $root 'previews\dark.png') -Destination (Join-Path $staging 'previews')

    if (Test-Path -LiteralPath $zip) {
        Remove-Item -LiteralPath $zip -Force
    }

    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -CompressionLevel Optimal
} finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$size = (Get-Item -LiteralPath $zip).Length
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
$hashLine = '{0}  AudioDeck-{1}.zip' -f $hash, $Version
[System.IO.File]::WriteAllText((Join-Path $releaseRoot 'SHA256.txt'), $hashLine + "`r`n",
    (New-Object System.Text.UTF8Encoding($false)))

Write-Host ("打包完成: {0} ({1:N0} bytes)" -f $zip, $size)
Write-Host ("SHA256: {0}" -f $hash)
