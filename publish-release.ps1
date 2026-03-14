param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$git = (Get-Command git -ErrorAction Stop).Source
$gh = (Get-Command gh -ErrorAction Stop).Source
$tag = "v$Version"
$releaseTitle = "WinSwitch $Version"
$assetDir = Join-Path $projectRoot "installer\output"
$x64Installer = Join-Path $assetDir "WinSwitch-Setup-x64.exe"
$arm64Installer = Join-Path $assetDir "WinSwitch-Setup-ARM64.exe"

Write-Host "Building installers..."
& powershell -ExecutionPolicy Bypass -File (Join-Path $projectRoot "build-installer.ps1")

if (-not (Test-Path $x64Installer) -or -not (Test-Path $arm64Installer)) {
    throw "Expected installer assets were not found in $assetDir."
}

Push-Location $projectRoot
try {
    & $git add .
    & $git commit -m "Release $tag"
    & $git tag $tag
    & $git push origin $Branch
    & $git push origin $tag
    & $gh release create $tag $x64Installer $arm64Installer --title $releaseTitle --generate-notes
}
finally {
    Pop-Location
}
