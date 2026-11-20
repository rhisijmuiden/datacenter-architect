"""Replace flat normals with smooth, area-weighted normals (per primitive, vertices grouped by position)."""
import sys, numpy as np, pygltflib
src, dst = sys.argv[1], sys.argv[2]
g = pygltflib.GLTF2().load(src)
blob = bytearray(g.binary_blob())
def view(acc_i, comps, dtype):
    a = g.accessors[acc_i]; bv = g.bufferViews[a.bufferView]
    size = comps * np.dtype(dtype).itemsize
    stride = bv.byteStride or size
    off = (bv.byteOffset or 0) + (a.byteOffset or 0)
    rows = [np.frombuffer(bytes(blob[off + i * stride: off + i * stride + size]), dtype=dtype) for i in range(a.count)]
    return (off, stride), np.array(rows).reshape(a.count, comps)
IDX = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32}
for m in g.meshes:
    for p in m.primitives:
        if p.attributes.NORMAL is None or p.indices is None: continue
        _, pos = view(p.attributes.POSITION, 3, np.float32)
        noff, nrm = view(p.attributes.NORMAL, 3, np.float32)
        ia = g.accessors[p.indices]; _, idx = view(p.indices, 1, IDX[ia.componentType])
        tri = idx.reshape(-1, 3).astype(np.int64)
        fn = np.cross(pos[tri[:, 1]] - pos[tri[:, 0]], pos[tri[:, 2]] - pos[tri[:, 0]])  # area weighted
        key = np.round(pos * 2000).astype(np.int64)
        _, grp = np.unique(key, axis=0, return_inverse=True)
        grp = grp.reshape(-1)
        acc = np.zeros((grp.max() + 1, 3))
        for k in range(3): np.add.at(acc, grp[tri[:, k]], fn)
        sm = acc[grp]
        # keep hard edges where the smooth normal turns more than ~60 degrees away from the original
        ln = np.linalg.norm(sm, axis=1, keepdims=True); ln[ln == 0] = 1; sm = sm / ln
        dot = (sm * nrm).sum(1)
        out = np.where(dot[:, None] > 0.5, sm, nrm).astype(np.float32)
        o, st = noff
        for i in range(len(out)): blob[o + i * st: o + i * st + 12] = out[i].tobytes()
g.set_binary_blob(bytes(blob))
g.save_binary(dst)
