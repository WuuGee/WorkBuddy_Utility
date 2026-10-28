# Release verification (`--verify-release`)

Checks that the release candidate is consistent and complete.

## Usage

1. `installer\Package-Release.ps1` (builds the installer and `artifacts\release`).
2. `WorkBuddy.exe --verify-release`, then read `%LOCALAPPDATA%\WorkBuddy\release-verification.log`. Exit code 0 means all passed.

## Coverage

- exe metadata (product, company, version, copyright) and About dialog constants
- version consistency between csproj, installer script and AppInfo
- `LICENSE` (MIT and holder), README, release notes, RC checklist
- installer exists, plausible size, version metadata
- `artifacts\release` contents and SHA-256 file match the installer

It does not install anything and is safe to run at any time. It must run from a build inside the repository (it locates the repo root).

## Installer verifier additions

`--verify-installer` also covers legacy FocusCatAI data (kept by default, kept when only WorkBuddy data is deleted, removed only with the test switch `/DELETELEGACY`, scratch path `/LEGACYDIR=<path>`).
