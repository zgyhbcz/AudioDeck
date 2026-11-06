# Contributing to AudioDeck

Thanks for taking the time to contribute.

## Before you start

- Search existing [issues](https://github.com/zgyhbcz/AudioDeck/issues) before opening a new one.
- For bugs, include Windows version/build, the affected output devices, and steps to reproduce.
- For UI changes, include before/after screenshots (light and dark themes where relevant).
- For security issues, follow [SECURITY.md](SECURITY.md) instead of opening a public issue.

## Development setup

Requirements:

- Windows 10/11 x64.
- .NET Framework 4.8.
- PowerShell 5.1 or newer.
- No Visual Studio or .NET SDK is required.

Build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Create the release zip:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

The app is written in C# 5 because it is compiled with the .NET Framework `csc.exe`.
Avoid language features that require a newer compiler, and do not add NuGet dependencies
without discussing them first.

## Manual test checklist

Before opening a pull request, please verify on real Windows hardware:

- [ ] The panel opens from the tray icon and with `Ctrl+Alt+A`.
- [ ] The device list matches Windows sound settings.
- [ ] Switching output changes the default device for console, multimedia, and communications.
- [ ] Volume and mute controls work, including mouse wheel and dragging.
- [ ] Feedback sounds play and respect the "System Sounds" volume.
- [ ] Clicking outside or pressing Esc closes the panel.
- [ ] Light and dark themes render correctly.
- [ ] The app survives unplugging a USB/Bluetooth output device.
- [ ] `build.ps1` and `package.ps1` complete without errors.
- [ ] `install.ps1` and `uninstall.ps1` leave no unexpected registry entries or files.

## Code style

- Keep the existing file layout and namespace style.
- Prefer small, focused changes.
- Dispose GDI+ objects and COM interfaces.
- Do not introduce network access or telemetry.
- Do not commit generated build output (`dist/`, `release/`, `*.zip`).
- Do not commit tokens, personal paths, or machine-specific files.

## Commit messages

Use short imperative subjects, for example:

```text
Add per-device volume feedback
Fix tray icon position on high-DPI displays
Document Windows 11 tray overflow behavior
```

## Pull requests

1. Fork the repository and create a topic branch.
2. Make the change and update documentation if needed.
3. Run the manual checklist above.
4. Open a pull request using the template and describe the motivation and test results.

By contributing, you agree that your contribution is licensed under the
[MIT License](LICENSE).
