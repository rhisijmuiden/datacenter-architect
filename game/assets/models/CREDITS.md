# 3D model credits

All third-party models in this folder are public domain (CC0 1.0). Credit is not required, but given gladly.

| Folder | Models | Author | Licence | Source |
|---|---|---|---|---|
| `made/` | Trees, shrubs, grass tufts, flowers, all datacenter equipment (generator, transformer, switchgear, UPS, batteries, PDU, tanks, chiller, dry cooler, cooling tower, CRAH, core router, spine, ODF, battery container), street furniture and fences | Made for this game with `tools/models/blender/` | CC0 1.0 (same as the game's own art) | this repository |
| `furniture/` | Furniture Kit: desks, chairs, tables, sofa, bookcase, kitchen units, screens | [Kenney](https://kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | opengameart.org/content/furniture-kit |
| `vehicles/` | Car Kit 3.1: sedans, SUVs, hatchback, van, delivery vans, trucks | [Kenney](https://kenney.nl) | CC0 1.0 | opengameart.org/content/car-kit |
| `city/` | City Kit (Commercial) 2.1: office blocks and towers | [Kenney](https://kenney.nl) | CC0 1.0 | opengameart.org/content/city-kit-commercial |
| `suburb/` | City Kit (Suburban) 2.0: houses | [Kenney](https://kenney.nl) | CC0 1.0 | opengameart.org/content/city-kit-suburban |
| `industry/` | City Kit (Industrial) 2.0: sheds, tanks, chimneys | [Kenney](https://kenney.nl) | CC0 1.0 | opengameart.org/content/city-kit-industrial |
| `../textures/ground/` | Photo textures: leafy grass, asphalt 02, square concrete pavers, herringbone pavement, red brick pavers, raked dirt, gravel floor, concrete floor 02, red brick, corrugated iron 02, concrete wall 008 | [Poly Haven](https://polyhaven.com) | CC0 1.0 | polyhaven.com (1k JPG) |
| `people/` | Ultimate Modular Men and Ultimate Modular Women: Worker, Suit, Casual, Casual 2, Casual Hoodie, Farmer, Formal (rigged, with Idle, Walk, Run, Interact and Wave animations) | [Quaternius](https://quaternius.com) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | quaternius.com; copied from the CC0 mirror in github.com/LuanDucate/Ducz.CharacterCreator |

## How the files were prepared

`tools/models/prep.mjs` (gltf-transform) keeps only the animations the game uses, removes the pistol that the
Suit outfit carries, and prunes unused data. `tools/models/smooth.py` replaces the flat-shaded normals with smooth
ones, so the low-poly figures read as rounded people instead of facets:

```
node tools/models/prep.mjs Male/Worker.glb /tmp/p.glb
python3 tools/models/smooth.py /tmp/p.glb game/assets/models/people/Male_Worker.glb
```

Colours of clothes, skin and hair are set in the game (`World/People.cs`), not in the files.

The game's own models are built by Python scripts that run in Blender 4.2+ (or with the `bpy` module from PyPI):

```
python3 tools/models/blender/plants.py game/assets/models/made   # trees, shrubs, grass, flowers
python3 tools/models/blender/infra.py game/assets/models/made    # equipment, street furniture, fences
python3 tools/models/blender/preview.py out.png a.glb b.glb      # contact sheet (Cycles, CPU)
```

The ground and wall photo textures are prepared with `tools/textures/prep_ground.py`: it keeps the detail but takes
out most of the photo's own colour, so the game can tint them with its palette and the seasons.
Inside the game, `--gallery made/a.glb,made/b.glb` with `--screenshot` shows models in a row on an empty plot.

