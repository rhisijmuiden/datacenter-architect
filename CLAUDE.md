# Datacenter Architect: working notes

- Offline Windows 11 game, Godot 4 with C#. No mail server and no remote backups (deliberate; offline game). Local savegames only.
- Default language English; every player-facing string must also have a Dutch translation. Keep technical terms (rack, CRAH, UPS, N+1) untranslated.
- Branches: `main` = stable channel, `test` = test channel. Work lands on `test` first, then is promoted to `main`.
- Follow the brand in `brand/BRAND.md` and `brand/tokens.json`. Power = amber, cooling = cyan, fiber = orange, everywhere.
- Build menus sit on the left side of the screen.
- Game rules live in `docs/game-design.md`; update it when gameplay decisions change.
