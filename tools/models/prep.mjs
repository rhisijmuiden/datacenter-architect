import { NodeIO } from '@gltf-transform/core';
import { prune, weld, normals, dedup } from '@gltf-transform/functions';
const [,, src, dst, smooth] = process.argv;
const KEEP = new Set(['Idle', 'Idle_Neutral', 'Walk', 'Run', 'Interact', 'Wave']);
const io = new NodeIO();
const doc = await io.read(src);
const root = doc.getRoot();
// the suit and swat outfits come with a pistol: not in a datacenter
for (const n of root.listNodes()) if (n.getName() === 'Pistol') n.dispose();
for (const a of root.listAnimations()) if (!KEEP.has(a.getName())) a.dispose();
await doc.transform(dedup(), prune());
await io.write(dst, doc);
