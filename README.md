# WinSwitch

`WinSwitch` is a Windows desktop app switcher that aims to feel closer to the macOS app-switching gesture flow than the stock Windows `Alt+Tab`.

## Features

- Runs in the background with a tray icon.
- Replaces the standard `Alt+Tab` flow with a centered app switcher overlay.
- Switches by app/process, so grouped windows behave more like macOS app entries.
- Supports `Alt+Tab` / `Shift+Alt+Tab`.
- Supports holding the mouse middle button and swiping left or right.
- Supports three-finger touchpad swipes after mapping Windows advanced gestures to hotkeys.
- Checks GitHub Releases for updates and installs the newest matching installer.

## Touchpad setup

Windows does not reliably expose raw global three-finger precision-touchpad swipes directly to third-party desktop apps. The supported path is to map those gestures to custom shortcuts and let WinSwitch respond to them.

1. Open `Settings > Bluetooth & devices > Touchpad > Advanced gestures`.
2. Set the three-finger swipe left gesture to `Ctrl+Alt+Left`.
3. Set the three-finger swipe right gesture to `Ctrl+Alt+Right`.
4. Start WinSwitch.

Each swipe advances the macOS-style overlay, and WinSwitch commits the highlighted app automatically after a short pause.

## Updates

WinSwitch can update itself from GitHub Releases.

1. Open `UpdateConfiguration.cs`.
2. Replace `YOUR_GITHUB_USERNAME` with your GitHub username or organization.
3. Keep the repository name in sync with the repo you publish.
4. Build the installer and publish GitHub releases with version tags like `v0.2.0`.

At runtime, WinSwitch looks for:

- `WinSwitch-Setup-x64.exe`
- `WinSwitch-Setup-ARM64.exe`

in the latest GitHub release and downloads the correct one for the current machine.

## Build on Windows

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
.\build-installer.ps1
```

Installers are written to:

- `installer\output\WinSwitch-Setup-x64.exe`
- `installer\output\WinSwitch-Setup-ARM64.exe`

## Publish a release

If `git` and GitHub CLI (`gh`) are installed and authenticated:

```powershell
.\publish-release.ps1 -Version 0.2.0
```

That script will:

1. Build the installers.
2. Commit the current repo state.
3. Create a `v0.2.0` git tag.
4. Push the branch and tag.
5. Create a GitHub release and upload both installer assets.

## Notes

- The app targets `net8.0-windows` and uses WPF plus Win32 interop, so it must be built on Windows.
- The middle-button gesture is intentionally captured globally while the button is held so the swipe feels dedicated to switching.
