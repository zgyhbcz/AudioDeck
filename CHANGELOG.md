# Changelog

All notable changes to AudioDeck are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-11-06

### Added

- System-style feedback sound while changing the master volume.
- Windows "Device connect" feedback sound after switching the default output device.
- Automatic promotion of the tray icon on Windows 11 during installation.
- English README, contribution guide, code of conduct, security policy, and issue templates.

### Changed

- Volume feedback is throttled while dragging so repeated sounds do not overlap excessively.
- The installer now reads its display version from the executable metadata.

## [1.0.0] - 2026-11-06

### Added

- Initial public release.
- Tray popup for switching active audio output devices.
- Master volume slider, mute toggle, and mouse-wheel control.
- Global `Ctrl+Alt+A` hotkey.
- Per-monitor DPI-aware, light/dark themed custom UI.
- Single-instance behavior, auto-start support, and an uninstaller.
- Portable installer that does not require administrator rights.

[1.0.1]: https://github.com/zgyhbcz/AudioDeck/releases/tag/v1.0.1
