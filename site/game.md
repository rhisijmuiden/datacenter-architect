---
title: Install, updates and saves
kicker: Help
lead: How to install the game, keep it up to date, switch between the stable and the test version, and where your saves live.
description: Installing Datacenter Architect, updates, stable and test channel, savegames, options and controls.
---

## Install on Windows

1. Go to the [latest release]({{ site.repo }}/releases/latest) and download `DatacenterArchitect-<version>-Setup.exe`.
2. Run it. It installs for your Windows user only, so it does not ask for administrator rights.
3. Start **Datacenter Architect** from the Start menu.

Requirements: Windows 11 (64-bit) and a graphics card that supports Vulkan. The game runs fully offline; only the update check uses the internet.

## Install on macOS

1. Go to the [latest release]({{ site.repo }}/releases/latest) and download `DatacenterArchitect-<version>-macOS.dmg`.
2. Open the .dmg and drag **Datacenter Architect** onto the **Applications** folder.
3. Open the game once by hand, because it is not signed with an Apple developer certificate yet and macOS blocks it the first time:
   - **macOS 15 Sequoia and newer**: double-click the game, click **Done** in the warning, open **System Settings › Privacy & Security**, scroll down to *"Datacenter Architect" was blocked* and click **Open Anyway**. Confirm with your password or Touch ID.
   - **macOS 12 to 14**: in Finder, <kbd>Control</kbd>-click (or right-click) the game in Applications, choose **Open** and click **Open** again.
4. After that it starts normally from Launchpad or Applications.

Requirements: macOS 12 Monterey or newer, on an Apple Silicon (M1 or later) or Intel Mac. One app runs natively on both. A mouse is easiest; on a trackpad, two-finger scrolling zooms.

> **Updates on a Mac** come from the Info screen too. Keep the game in **Applications** (not in Downloads): the updater replaces the app there and opens the new version. If it cannot (for example when your user may not change Applications), it opens the downloaded zip so you can drag the new version in yourself.

## Updates and channels

Open the **Info** screen (the ⓘ button at the bottom of the left rail, or Info in the main menu). It shows the version you are running and lets you:

- **Check for updates** – the game asks GitHub for the newest release on your channel.
- **Update** – downloads the installer (Windows) or the app (macOS), checks its size and SHA-256 fingerprint, installs it in the background and restarts the game.
- **Switch channel**:
  - **Stable** gets the tested releases (from the `main` branch).
  - **Test** also gets the test builds (from the `test` branch): new features first, sometimes a rough edge.

You can switch back from test to stable at any time; the next update takes you to the newest stable version.

{% include shot.html src="info" nl=true caption="The Info screen: version, update check, install update and the switch between the stable and test channel." %}

## Savegames

- Saves live in `%APPDATA%\Datacenter Architect\saves` on Windows and in `~/Library/Application Support/Datacenter Architect/saves` on macOS. Give them any name with **Save** in the game menu (<kbd>Esc</kbd> or ☰ at the bottom of the left rail).
- <kbd>Ctrl</kbd>+<kbd>S</kbd> saves quickly (on a Mac <kbd>Cmd</kbd> works as well as <kbd>Ctrl</kbd> for save, undo and redo).
- **Autosave**: three rotating autosaves, by default every 5 minutes and when you quit. Change how often under Options › Game.
- **Continue** in the main menu loads your latest save.
- Older saves keep working after an update; new features that would change an old game (like weather effects) stay off for it.

Achievements and your best scenario medals are kept in a profile over all saves. Timelapse pictures are stored per game.

## Options

Options open from the main menu, the pause menu and the gear at the bottom of the left rail. Everything applies straight away.

| Tab | What you can set |
|---|---|
| Display | Window, borderless or fullscreen, resolution (4:3 up to 32:9 and 4K), screen, interface size (75–150%), V-Sync, frame rate limit. <kbd>Alt</kbd>+<kbd>Enter</kbd> or <kbd>F11</kbd> toggles fullscreen |
| Graphics | Performance / Balanced / Quality presets, shadows, ambient occlusion, fog, anti-aliasing, lighting quality, grass, blinking LEDs, moving parts, smooth camera, day/night, weather, seasons |
| Audio | Master volume, music, server room, interface; sound effects on or off; mute in the background |
| Game | Language (English or Dutch), autosave interval, mouse sensitivity and inverted look for walking |

## Controls

| Key | Action |
|---|---|
| Right-drag | Rotate the camera |
| Mouse wheel | Zoom |
| <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> or middle-drag | Pan |
| <kbd>Q</kbd> / <kbd>E</kbd> | Turn 90° |
| <kbd>R</kbd> | Rotate what you are placing |
| <kbd>T</kbd> | Roofs on or off |
| <kbd>Page Up</kbd> / <kbd>Page Down</kbd> | Floor up or down |
| <kbd>P</kbd> / <kbd>H</kbd> / <kbd>N</kbd> | Power, cooling and network overlay |
| <kbd>B</kbd> | Plan mode |
| <kbd>F</kbd> | Walk around (first person) |
| <kbd>F9</kbd> | Photo mode |
| <kbd>F1</kbd> – <kbd>F8</kbd> | Overview, Company, Customers, Infrastructure, Staff, Reputation and research, Growth, Progress |
| <kbd>Space</kbd> | Pause / play |
| <kbd>1</kbd> <kbd>2</kbd> <kbd>3</kbd> | Game speed |
| <kbd>Ctrl</kbd>+<kbd>Z</kbd> / <kbd>Ctrl</kbd>+<kbd>Y</kbd> | Undo / redo |
| <kbd>Ctrl</kbd>+<kbd>S</kbd> | Quick save |
| <kbd>[</kbd> / <kbd>]</kbd> | Narrower or wider curved road |
| <kbd>Esc</kbd> | Close a screen, cancel, or open the game menu |

## Problems?

Found a bug or have an idea? Open an [issue on GitHub]({{ site.repo }}/issues). Log files are in `%APPDATA%\Datacenter Architect\logs` (Windows) or `~/Library/Application Support/Datacenter Architect/logs` (macOS).
