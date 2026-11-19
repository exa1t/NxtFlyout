# Changelog

## 1.2.0

- **Taskbar stats.** Optional CPU, memory, GPU, network and disk figures in the free space next
  to the notification icons, with GPU memory and GPU temperature as extras. Each figure can be
  switched on or off, the refresh pace is adjustable, and a click opens Task Manager. Off by
  default: turn it on in Settings → Flyouts → Taskbar stats.

## 1.1.2

- **Volume keys work while music plays.** With the taskbar visualizer running, the volume keys
  failed and were handed back to Windows, so the Windows volume overlay appeared instead of
  NxtFlyout's flyout. Fixed.
- **Taskbar widget stays clear of the taskbar's icons.** It now finds where the Start button,
  app icons, Widgets button and tray are, and only uses the free space between them. When space
  runs short it narrows the title, then hides the visualizer, then the text, and hides itself if
  even the cover and buttons do not fit. With left-aligned icons it moves after the last icon.

## 1.1.1

- NxtFlyout is now MIT-licensed (`LICENSE`). The installer and portable zip include `LICENSE.txt`
  and `THIRD-PARTY-NOTICES.txt` with the licences of the bundled libraries.

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
