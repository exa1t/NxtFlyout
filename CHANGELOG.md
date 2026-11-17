# Changelog

## 1.1.0

- **One-click install.** Every release now ships `NxtFlyout-Setup-<version>.exe`, built
  automatically on GitHub. It bundles everything it needs (no .NET install), needs no
  administrator rights, and can start NxtFlyout at sign-in.
- Shorter setup wizard: options, install, done. Optional desktop shortcut.
- Upgrades close the running copy and keep settings; a silent upgrade restarts the app.
- Portable zip and SHA-256 checksums alongside the installer.
- Faster start-up: the app is precompiled (ReadyToRun).
- CI builds and tests every push to `main`.

## 1.0.0

- Media, volume, lock keys and Up Next flyouts; taskbar widget and visualizer; Fluent settings
  window; Mica/Acrylic; multi-monitor and DPI support; English localization.
