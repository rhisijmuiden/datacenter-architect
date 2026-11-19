<p align="center"><img src="brand/logo-wordmark.svg" alt="Datacenter Architect" width="360"></p>

# Datacenter Architect

An offline building and management sim for Windows 11. Buy land, design the whole site from parking lot to data hall, keep power, cooling and fiber in balance, and grow your datacenter.

*Een offline bouw- en managementsimulatie voor Windows 11. Koop grond, ontwerp het hele terrein van parkeerplaats tot datazaal, houd stroom, koeling en glasvezel in balans en laat je datacenter groeien.*

## Status

Step 3: building and creative mode. Pave the site, fence it, mark a materials yard, and watch trucks and construction workers put up buildings with floors, rooms and doors. Racks are delivered to the loading dock, placed by fitters and connected by engineers. Creative mode switches off money and limits.

![Construction](docs/screenshots/step3-build.png)
![Data hall](docs/screenshots/step3-hall.png)

Design material:

- [Game design document](docs/game-design.md) (Dutch)
- [Brand guide](brand/BRAND.md) and [design tokens](brand/tokens.json)
- Interactive sketches in [docs/concept](docs/concept) (open the `.html` files in a browser): concept, art style, room designer, site planner

## Tech

- Godot 4.5 with C# (.NET 8), project in [`game/`](game)
- Windows 11 installer (Inno Setup, per-user install, no admin prompt)
- English by default, fully translated to Dutch: [`game/locale/strings.tsv`](game/locale/strings.tsv)
- Local savegames and three rotating autosaves in `%APPDATA%\Datacenter Architect\saves`
- Updates via GitHub Releases, checked and installed from the in-game Info screen

## Developing

```bash
# once: install Godot 4.5.1 (.NET edition) and the .NET 8 SDK
godot --headless --path game --import        # import assets
dotnet build game/DatacenterArchitect.csproj # compile C#
godot --path game                            # run the game
godot --headless --path game -- --run-tests  # run the tests (exit code 0 = pass)
```

Controls: right-drag rotates, mouse wheel zooms, WASD or middle-drag pans, Q/E turn 90°, R rotates the object you are placing, T shows or hides roofs, Page Up/Down change floor, Space pauses, 1/2/3 set the speed, F5 saves, Esc opens the menu.

## Releasing

Releases are built on GitHub by [`.github/workflows/release.yml`](.github/workflows/release.yml) when a tag is pushed, or when you run the workflow from the Actions tab with a version number. It stamps the version and channel, runs the tests, exports the Windows build and attaches `DatacenterArchitect-<version>-Setup.exe` to a GitHub Release.

```bash
git checkout test && git tag v0.2.0-test.1 && git push origin v0.2.0-test.1   # test channel (pre-release)
git checkout main && git tag v0.2.0 && git push origin v0.2.0                 # stable channel
```

The game's Info screen reads these releases: the Stable channel sees normal releases, the Test channel also sees pre-releases.

## Branches

| Branch | Channel | Purpose |
|---|---|---|
| `main` | Stable | Production releases |
| `test` | Test | Pre-releases for testing |

The in-game Info screen lets the player choose the channel and update from there.
