# UI themes (v0.2.0-B)

WorkBuddy has three theme choices: **System** (default, follows the Windows app theme), **Light** and **Dark**. The choice is `GeneralSettings.Theme` in settings.json, so `ISettingsService` is the single source of truth. Changes apply live, with no restart.

## How it works
- `ThemePreference` and the pure `ThemeResolver.Resolve(preference, systemPrefersLight)` live in `FocusCatAI.Application.Settings`. Unknown or invalid values in settings.json (for example `"Neon"` or `99`) load as `System` via `ThemePreferenceConverter`; they never fail loading the file.
- `ISystemThemeProvider` (Application) is implemented by `WindowsSystemThemeProvider` (Infrastructure). It reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`; a missing or unreadable value means Light.
- `ThemeApplier` (Presentation) is the only class that touches `Application.Resources.MergedDictionaries`. It starts before the first window, follows `ISettingsService.Changed` and Windows theme changes (only acts on those while the preference is System), marshals to the UI thread, and replaces the one theme dictionary in place.
- Every brush in the themed windows and in `Themes/Controls.xaml` is a `DynamicResource`, so open windows restyle immediately without being recreated.
- `WindowChromeTheme` sets the title bar through `DwmSetWindowAttribute` (attribute 20, fallback 19) and repaints the frame with `SetWindowPos(SWP_FRAMECHANGED ...)`. Unsupported Windows builds keep the default title bar.

## Not themed on purpose
`PetWindow` and `SpeechBubbleWindow` (the pet's art style), and native `MessageBox` dialogs (they follow Windows).

## Files
- `Themes/Theme.Light.xaml`, `Themes/Theme.Dark.xaml`: identical semantic keys (`Brush.Window.Background`, `Brush.Text.Primary`, ...). A unit test enforces key parity.
- `Themes/Controls.xaml`: implicit styles for Button, CheckBox, TextBox, ListBoxItem, ComboBox(Item) and Slider. Required parts (`PART_Popup`, `PART_Track`, `PART_ContentHost`) are kept and checked by tests.

## Adding a themed control or window
Use `{DynamicResource Brush.*}` only, never a literal `#` color or `StaticResource` for a theme brush. To add a brush, define it in **both** theme files. Tests (`ThemeXamlTests`) fail on literal colors in `SettingsWindow.xaml`, `AboutWindow.xaml` and `Controls.xaml`, on missing keys, and on key mismatches between the two themes.

## Verification
- `--verify-settings`, **Theme** area: see SETTINGS-VERIFICATION.md.
- Unit tests: `ThemeTests.cs`.
