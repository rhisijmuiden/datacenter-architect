# 3D model credits

All third-party models in this folder are public domain (CC0 1.0). Credit is not required, but given gladly.

| Folder | Models | Author | Licence | Source |
|---|---|---|---|---|
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
