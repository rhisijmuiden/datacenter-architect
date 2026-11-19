# Performance

The game has to stay smooth with very large data halls (thousands of racks). This note records how the
3D world and the simulation are kept cheap, and how to measure it.

## Benchmark

```
godot --path game -- --bench 10000 [--bench-seconds 8] [--bench-out result.json]
```

Builds one hall with N live racks (rows of 60, hot and cold aisles), starts the game at normal speed with the roof
off and reports, as JSON:

- `place_rack_ms`: placing one more rack and rebuilding the 3D world, as the player feels it
- `full_touch_ms`: a change that also rebuilds walls, paving and fences
- per view (`overview`, `closeup`): frame times (p50, p95), FPS, and the simulation and world update time per frame

Run it on a real PC for real frame rates. In the cloud the GPU is emulated in software (lavapipe), so only the
relative numbers and the CPU-side timings mean anything there.

## How the world stays cheap

- **Racks are instanced per chunk.** `World/RackRenderer.cs` cuts the halls into chunks of 16 × 16 cells. Each chunk
  is one MultiMesh for rack bodies, one for fronts, one for racks still on their way and one for all servers.
  A chunk only rebuilds when one of its racks changes (a signature of id, position, rotation and status).
- **Servers are one quad each.** `World/ServerFill.cs` picks a fixed mix of 1U to 7U units per rack from its id.
  Every unit is a single instanced quad; its bays, vents, ports and LEDs are drawn by one shader, and the LEDs blink
  on the GPU from `TIME` and per-instance data. No script touches an LED per frame.
- **Level of detail:** server quads have a visibility range (`RackRenderer.ServerRange`); further away only the
  plain rack fronts are drawn. Chunks are frustum-culled by the engine.
- **Lookups are O(1).** `GameState.ObjectAt` uses a cell index that is rebuilt when the state changes, and is
  extended in place when an object is added, so placing a row of racks does not get slower as the hall grows.
- **Placing a rack does not rebuild the layout.** Only gates, lifts and roof plant change walls, roofs or fences
  (`GameState.ShapesLayout`), so other objects touch the state without a layout rebuild.
- **The HUD checklist** only rebuilds its labels when a goal changes, and the ambient sound hands its samples to
  the engine in one buffer per frame instead of one call per sample.

## Measurements (cloud, software GPU, debug build, 640 × 360)

| Racks | Place one rack, before → after | Frame (overview), before → after |
|---|---|---|
| 1 000 | 115 ms → 6 ms | 222 ms → 207 ms |
| 5 000 | 915 ms → 19 ms | 334 ms → 278 ms |
| 10 000 | 3 240 ms → 43 ms | 442 ms → 314 ms |

"After" also draws about twenty servers per rack (around 200 000 server fronts at 10 000 racks), which "before"
did not have. Simulation per frame at 10 000 racks: about 3 ms on average (debug build; release builds are faster).
