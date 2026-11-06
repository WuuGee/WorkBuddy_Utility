# AI Companion (v0.4.0)

- `AppSettings.Ai` (`Enabled`, `Device`): Auto/Cpu/Gpu/Npu. `AiDeviceResolver` falls back to Cpu when the requested device is not detected.
- `PetViewModel.AskCompanion(prompt)` returns null when disabled or the prompt is empty; otherwise the pet thinks, speaks the reply and the reply is returned.
- Entry points: tray **Ask Companion** (after Pet) and Settings > AI Companion (Ask box and button).
- Local models are listed from the models folder; the built-in fallback engine answers when no model is present.
- Settings UI uses Brush.* resource keys only (no hex colors).
- Verification: `--verify-settings` (AI Companion area) and `--verify-tray` (Ask Companion area).
