<#
.SYNOPSIS
    Builds the WorkBuddy installer: publishes a self-contained win-x64 build, then compiles it with Inno Setup.

.DESCRIPTION
    Output: artifacts\publish\ (the app), artifacts\installer\WorkBuddy-Setup-<version>.exe, plus a SHA-256 file.
    The version comes from the Presentation project, so there is a single place to change it.

.PARAMETER SkipTests
    Skip the unit tests that normally gate the build.
#>
param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$project = 'src\FocusCatAI.Presentation\FocusCatAI.Presentation.csproj'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> found in $project" }

$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }

if (-not $SkipTests) {
    Write-Host '== Tests' -ForegroundColor Cyan
    dotnet test tests\FocusCatAI.Application.Tests --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed; not building an installer.' }
}

$publish = Join-Path $root 'artifacts\publish'
$out = Join-Path $root 'artifacts\installer'
Remove-Item $publish, $out -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "== Publish $version (self-contained, win-x64)" -ForegroundColor Cyan
dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish --nologo -v q `
    -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

Write-Host '== Inno Setup' -ForegroundColor Cyan
& $iscc "/DAppVersion=$version" "/DPublishDir=$publish" "/DOutputDir=$out" installer\WorkBuddy.iss
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }

$setup = Join-Path $out "WorkBuddy-Setup-$version.exe"
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash *WorkBuddy-Setup-$version.exe" | Set-Content "$setup.sha256" -Encoding ascii
Write-Host "== Done: $setup ($([int]((Get-Item $setup).Length / 1MB)) MB)`n   SHA-256 $hash" -ForegroundColor Green
