# Startup verification (`--verify-startup`)

Permanent developer diagnostic for "start with Windows", the Run key, and single-instance behavior.

## Usage

```powershell
# Close any running WorkBuddy first.
$p = Start-Process .\WorkBuddy.exe -ArgumentList "--verify-startup" -PassThru -Wait
$p.ExitCode   # 0 = all passed
```

The run takes about 10 seconds. It briefly starts second copies of WorkBuddy to test single-instance behavior.
`--autostart` always overrides this switch. Output: `%LOCALAPPDATA%\WorkBuddy\startup-verification.log`.

## Safety

Destructive state tests (missing, moved, mismatched and Windows-disabled entries) use a scratch key
`HKCU\Software\WorkBuddy\VerifyStartup`, deleted afterwards. The real Run value is exercised only with a normal
enable/disable, then restored to what it was before. The user's settings are restored too.

## What it checks

| Area | Checks |
|---|---|
| Registration | Writes `"<exe>" --autostart` under HKCU; status becomes Registered; no administrator rights |
| Removal | Value deleted; removing twice is harmless |
| Detection | NotRegistered, InvalidPath (file missing), PathMismatch (other exe or arguments), DisabledByWindows (Task Manager or Settings turned it off) |
| Synchronization | Moved or deleted install repaired; wanted-but-missing created; unwanted removed; a user's choice in Windows is not overridden |
| Settings | Checkbox reads the registry; setting and registry kept together; a stale setting cannot make the checkbox lie |
| Autostart | `--autostart` overrides diagnostic switches; no window opens |
| Single Instance | Second launch exits, shows the hidden pet, opens Settings in the first instance, leaves one process; a duplicate `--autostart` exits silently |

## Expected result

exit code `0`, 37 checks.
