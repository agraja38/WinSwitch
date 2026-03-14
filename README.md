# WinSwitch

`WinSwitch` is a Windows app switcher built to feel closer to the macOS fullscreen swipe gesture.

Created by Agraja.

## What changed in 1.0.08

- Replaced the failing `Alt+Tab` path with `Ctrl+Alt+Left` and `Ctrl+Alt+Right` keyboard shortcuts.
- Added an update progress UI for download and install, plus a completion message after install finishes.
- Kept the safer update shutdown/restart flow.

## How it works

- `Ctrl+Alt+Left` and `Ctrl+Alt+Right` switch apps from the keyboard.
- Three-finger touchpad swipes work after mapping Windows advanced gestures to the same shortcuts.
- When both the current app and target app cover the full display, WinSwitch animates a fullscreen slide between them.
- If fullscreen mode is required in settings and either app is not fullscreen-like, WinSwitch falls back to a direct switch without the animation.

## Settings GUI

Open the tray icon and click `Settings`.

Users can configure:

- whether keyboard shortcuts are enabled
- whether mapped touchpad swipes are enabled
- whether fullscreen-only animation is required
- swipe commit delay
- animation duration
- update checks on launch

## Touchpad setup

Windows does not expose raw global three-finger touchpad swipes to desktop apps in a reliable way, so WinSwitch uses the Windows-supported hotkey mapping route.

1. Open `Settings > Bluetooth & devices > Touchpad > Advanced gestures`.
2. Map three-finger swipe left to `Ctrl+Alt+Left`.
3. Map three-finger swipe right to `Ctrl+Alt+Right`.
4. Start WinSwitch.

## Updates

WinSwitch checks [GitHub Releases](https://github.com/agraja38/WinSwitch/releases) and downloads:

- `WinSwitch-Setup-x64.exe`
- `WinSwitch-Setup-ARM64.exe`

Direct installer links:

- [Latest x64 installer](https://github.com/agraja38/WinSwitch/releases/latest/download/WinSwitch-Setup-x64.exe)
- [Latest ARM64 installer](https://github.com/agraja38/WinSwitch/releases/latest/download/WinSwitch-Setup-ARM64.exe)

The repo owner and name are already configured in [UpdateConfiguration.cs](C:/Users/agraj/iCloudDrive/Codex/WinSwitch/UpdateConfiguration.cs).

## Build

Requirements:

- .NET 8 SDK
- Inno Setup 6

Commands:

```powershell
dotnet build
dotnet run
```

## Build installers

```powershell
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
```

Generated installers:

- `installer\output\WinSwitch-Setup-x64.exe`
- `installer\output\WinSwitch-Setup-ARM64.exe`

## Publish a release

```powershell
powershell -ExecutionPolicy Bypass -File .\publish-release.ps1 -Version 1.0.08
```

## Notes

- The fullscreen swipe is an app-level visual transition, not a true Windows virtual-desktop compositor feature. That means it can closely mimic the macOS feel for fullscreen apps, but Windows still controls the real windows underneath.
- The project targets `net8.0-windows` and must be built on Windows.
