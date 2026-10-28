param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
if (-not $SkipBuild) { & "$PSScriptRoot\Build-Installer.ps1" -SkipTests }
$ver = ([xml](Get-Content src\FocusCatAI.Presentation\FocusCatAI.Presentation.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$name = "WorkBuddy-Setup-$ver.exe"
$out = Join-Path $root 'artifacts\release'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item $out -ItemType Directory | Out-Null
Copy-Item "artifacts\installer\$name" $out
$hash = (Get-FileHash "$out\$name" -Algorithm SHA256).Hash.ToLower()
"$hash  $name" | Set-Content "$out\$name.sha256" -Encoding ASCII
Copy-Item LICENSE $out
Copy-Item "docs\release\RELEASE-NOTES-$ver.md" "$out\RELEASE-NOTES.md"
Write-Host "Release folder: $out"; Get-ChildItem $out | Select-Object Name, Length
