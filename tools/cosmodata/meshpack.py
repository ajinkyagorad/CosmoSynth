"""Turn parsed glTF primitives into the compact mesh the headset loads.

The models NASA publishes are authored for offline rendering: the Perseverance rover is
200 000 triangles, and every triangle carries its own unshared vertices because that is what
falls out of a Draco decode. A Quest draws these things at a few dozen pixels across for most
of any session. So the importer welds, decimates to a budget, merges materials down to a
handful of submeshes, and writes a flat binary the runtime can upload with no parsing at all.

Three things the packer must get right that are invisible in any count of triangles, and each
of which shipped wrong once:

* **Every vertex must be referenced.** Decimation leaves orphans behind, and the bounding box
  is what sets both the model's scale and its declared length. See ``compact``.
* **Alpha and metallic are consumed by a renderer that cannot honour them.** See
  ``MIN_VISIBLE_ALPHA`` and ``MAX_METALLIC``.
* **Winding has to survive a handedness change.** See ``to_unity_axes``, and
  ``gltf.winding_agreement`` for the check that proves it did.

The output format (".csmesh") is deliberately trivial — a fixed header, then three arrays.
Everything is little-endian, positions are metres, and the axes are already Unity's.

    magic        4     "CSM1"
    flags        u32   bit 0: indices are 32-bit rather than 16-bit
    vertices     u32   vertex count
    indices      u32   index count
    submeshes    u32   submesh count
    boundsMin    3xf32 metres
    boundsMax    3xf32 metres
    lengthM      f32   longest axis, metres
    ----
    vertices     vertexCount x (position 3xf32, normal 3xf32)
    indices      indexCount x (u16 or u32)
    submeshes    submeshCount x (start u32, count u32, rgba 4xf32, metallic f32, smoothness f32)
"""

from __future__ import annotations

import struct
from dataclasses import dataclass
from typing import Dict, List, Sequence, Tuple

import gltf

MAGIC = b"CSM1"
HEADER_FORMAT = "<4sIIII3f3ff"

#: Fixed header size. The runtime reader hard-codes 48; anything that reads a .csmesh back
#: needs the same number, so it is stated here once rather than repeated as a literal.
HEADER_BYTES = struct.calcsize(HEADER_FORMAT)

#: Beyond this many submeshes a spacecraft costs more draw calls than it is worth. Materials
#: are clustered by colour until it fits.
MAX_SUBMESHES = 6

#: Below this base-colour alpha a material is invisible in the source and must not be shipped.
#:
#: Unity's Standard shader is opaque by default and ignores the alpha channel entirely, so a
#: fully transparent glTF material arrives as *solid* geometry. Juno shipped with a
#: (1, 1, 1, 0.00) submesh drawn as a solid white panel across the spacecraft. Dropping it here
#: is the fix that does not depend on the renderer: geometry the author made invisible is not
#: geometry, and no blend mode has to be negotiated to leave it out.
MIN_VISIBLE_ALPHA = 0.02

#: Ceiling on the metallic factor written into a submesh.
#:
#: glTF's default metallicFactor is 1.0 and several of these models set it explicitly, which is
#: correct for the image-based lighting they were authored under. This application renders with
#: the Built-in pipeline, flat ambient of about 0.05 and no reflection probe, and a Standard
#: material at metallic 1 has *no diffuse albedo at all* -- it is lit only by reflections of an
#: environment that is essentially black. Perseverance carried metallic 1.0 on four of its six
#: submeshes and the ISS on one, so both would draw as silhouettes.
#:
#: 0.35 keeps about two thirds of the diffuse term, which is enough to read the shape, while
#: leaving metals visibly glossier than the painted parts. If a reflection probe is ever added,
#: raise this and rebuild rather than compensating in the shader.
MAX_METALLIC = 0.35

Vector3 = Tuple[float, float, float]


@dataclass
class Submesh:
    start: int
    count: int
    color: Tuple[float, float, float, float]
    metallic: float
    smoothness: float


@dataclass
class PackedMesh:
    positions: List[Vector3]
    normals: List[Vector3]
    indices: List[int]
    submeshes: List[Submesh]

    @property
    def triangle_count(self) -> int:
        return len(self.indices) // 3

    def bounds(self) -> Tuple[Vector3, Vector3]:
        low = [float("inf")] * 3
        high = [float("-inf")] * 3
        for point in self.positions:
            for axis in range(3):
                low[axis] = min(low[axis], point[axis])
                high[axis] = max(high[axis], point[axis])
        if low[0] > high[0]:
            return (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)
        return (low[0], low[1], low[2]), (high[0], high[1], high[2])

    def longest_axis(self) -> float:
        low, high = self.bounds()
        return max(high[axis] - low[axis] for axis in range(3))


def _normalise(v: Sequence[float]) -> Vector3:
    length = (v[0] * v[0] + v[1] * v[1] + v[2] * v[2]) ** 0.5
    if length <= 1e-20:
        return (0.0, 1.0, 0.0)
    return (v[0] / length, v[1] / length, v[2] / length)


def _face_normal(a: Vector3, b: Vector3, c: Vector3) -> Vector3:
    u = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
    v = (c[0] - a[0], c[1] - a[1], c[2] - a[2])
    return _normalise((u[1] * v[2] - u[2] * v[1],
                       u[2] * v[0] - u[0] * v[2],
                       u[0] * v[1] - u[1] * v[0]))


def _material_key(material: gltf.Material) -> Tuple[int, int, int, int, int]:
    """Quantised material identity, so near-identical materials merge into one submesh."""
    r, g, b, a = material.base_color
    return (round(r * 32), round(g * 32), round(b * 32), round(a * 32),
            round(material.metallic * 4))


def to_unity_axes(primitives: Sequence[gltf.Primitive]) -> List[gltf.Primitive]:
    """Convert from glTF's right-handed axes to Unity's left-handed ones.

    glTF looks down -Z and Unity looks down +Z, so the conversion is a Z negation. That flips
    handedness, which means the triangle winding has to be reversed as well or every face
    points inwards — a model that renders as a hollow shell you can see the inside of, which
    reads as "the model failed to load" rather than as a winding bug.
    """
    converted: List[gltf.Primitive] = []

    for primitive in primitives:
        positions = [(p[0], p[1], -p[2]) for p in primitive.positions]
        normals = [(n[0], n[1], -n[2]) for n in primitive.normals]

        indices: List[int] = []
        for i in range(0, len(primitive.indices) - 2, 3):
            a, b, c = primitive.indices[i], primitive.indices[i + 1], primitive.indices[i + 2]
            indices.extend((a, c, b))

        converted.append(gltf.Primitive(positions=positions, normals=normals,
                                        indices=indices, material=primitive.material))

    return converted


def cluster_decimate(positions: Sequence[Vector3], normals: Sequence[Vector3],
                     indices: Sequence[int], cell_size: float
                     ) -> Tuple[List[Vector3], List[Vector3], List[int]]:
    """Vertex-cluster decimation: snap vertices onto a grid and collapse each cell to a point.

    Chosen over quadric-error simplification because it is short, has no failure modes on the
    non-manifold, self-intersecting geometry that CAD exports are full of, and is entirely
    adequate for objects that occupy tens of pixels. Its known weakness is thin structure —
    Voyager's magnetometer boom is a 13 m rod a few centimetres thick — so the budgets below
    are set to keep cells small enough that booms survive as lines of cells rather than being
    welded into the bus.

    The representative vertex is the mean of the cell's members rather than the cell centre,
    which keeps flat panels flat instead of quantising them into terraces.
    """
    if cell_size <= 0.0:
        return list(positions), list(normals), list(indices)

    cells: Dict[Tuple[int, int, int], int] = {}
    sums: List[List[float]] = []
    normal_sums: List[List[float]] = []
    first_normals: List[Vector3] = []
    counts: List[int] = []
    remap: List[int] = []

    for i, point in enumerate(positions):
        key = (int(point[0] // cell_size), int(point[1] // cell_size), int(point[2] // cell_size))
        index = cells.get(key)
        if index is None:
            index = len(sums)
            cells[key] = index
            sums.append([0.0, 0.0, 0.0])
            normal_sums.append([0.0, 0.0, 0.0])
            first_normals.append((0.0, 1.0, 0.0))
            counts.append(0)

        for axis in range(3):
            sums[index][axis] += point[axis]
        if i < len(normals):
            if counts[index] == 0:
                first_normals[index] = normals[i]
            for axis in range(3):
                normal_sums[index][axis] += normals[i][axis]
        counts[index] += 1
        remap.append(index)

    new_positions = [(sums[i][0] / counts[i], sums[i][1] / counts[i], sums[i][2] / counts[i])
                     for i in range(len(sums))]

    # Averaging the members' normals is right almost everywhere and cancels exactly where a
    # cell straddles both faces of a thin panel -- which on a spacecraft is most of the solar
    # array. Falling back to a fixed up-vector there invents a normal, and it showed: 256 of
    # LRO's 2194 vertices were shaded as though facing straight up. Keeping the first member's
    # normal instead is arbitrary between two real answers rather than a third wrong one.
    new_normals: List[Vector3] = []
    for i in range(len(normal_sums)):
        total = normal_sums[i]
        if total[0] * total[0] + total[1] * total[1] + total[2] * total[2] <= 1e-12:
            new_normals.append(_normalise(first_normals[i]))
        else:
            new_normals.append(_normalise(total))

    # Clustering maps many source triangles onto the same three cells. Those repeats are not
    # extra detail, they are the same face drawn again: MAVEN shipped 3133 triangles of which
    # 37% were byte-identical to another, spending a decimation budget on nothing. Duplicates
    # are compared with the winding preserved, so the front and back faces of a welded-flat
    # panel both survive -- dropping one would make the panel vanish from one side.
    new_indices: List[int] = []
    seen = set()
    for i in range(0, len(indices) - 2, 3):
        a, b, c = remap[indices[i]], remap[indices[i + 1]], remap[indices[i + 2]]
        # A triangle whose corners landed in fewer than three cells has collapsed to a line.
        if a == b or b == c or a == c:
            continue

        smallest = min(a, b, c)
        key = (a, b, c) if smallest == a else ((b, c, a) if smallest == b else (c, a, b))
        if key in seen:
            continue
        seen.add(key)
        new_indices.extend((a, b, c))

    return new_positions, new_normals, new_indices


def compact(positions: Sequence[Vector3], normals: Sequence[Vector3], indices: Sequence[int]
            ) -> Tuple[List[Vector3], List[Vector3], List[int]]:
    """Drop every vertex no triangle refers to, and renumber what is left.

    Decimation leaves these behind by construction: a cell whose every triangle collapsed to a
    line keeps its averaged position and loses all of its faces. They are not merely wasted
    bytes. ``PackedMesh.bounds`` and ``longest_axis`` walk the position array, so an orphan
    outside the surviving hull sets the model's size -- and both the scale applied by
    ``_centre_and_scale`` and the ``lengthM`` written into the header are computed from it.

    Ulysses shipped at 1.656 m against a declared 3.200 m for exactly this reason: eight
    orphans, one bounding box nearly twice the size of the geometry, everything downstream
    dividing by the wrong number. Unity's RecalculateBounds walks the same array, so the
    culling volume was wrong too.
    """
    used = sorted(set(indices))
    if len(used) == len(positions):
        return list(positions), list(normals), list(indices)

    renumber = {old: new for new, old in enumerate(used)}
    return ([positions[old] for old in used],
            [normals[old] for old in used],
            [renumber[index] for index in indices])


def build(primitives: Sequence[gltf.Primitive], triangle_budget: int,
          target_length_metres: float) -> PackedMesh:
    """Merge, decimate, scale and centre a model into its final runtime form."""
    primitives = to_unity_axes(primitives)

    # --- Leave out anything the author made invisible --------------------------------------
    # Dropped before grouping, not after: _limit_submeshes folds small groups into the nearest
    # surviving colour, so a transparent group left in here would be merged into an opaque one
    # and reappear as solid geometry with no material of its own to identify it by.
    visible = [p for p in primitives if p.material.base_color[3] >= MIN_VISIBLE_ALPHA]
    if visible:
        primitives = visible

    # --- Group by material, so the merged mesh has one submesh per distinct appearance ----
    groups: Dict[Tuple[int, ...], List[gltf.Primitive]] = {}
    for primitive in primitives:
        groups.setdefault(_material_key(primitive.material), []).append(primitive)

    groups = _limit_submeshes(groups, MAX_SUBMESHES)

    # --- Decimate each group to its share of the budget ------------------------------------
    total_triangles = sum(p.triangle_count for group in groups.values() for p in group)
    low, high = gltf.bounds(primitives)
    diagonal = max(1e-9, sum((high[a] - low[a]) ** 2 for a in range(3)) ** 0.5)

    positions: List[Vector3] = []
    normals: List[Vector3] = []
    indices: List[int] = []
    submeshes: List[Submesh] = []

    for key, group in groups.items():
        group_positions: List[Vector3] = []
        group_normals: List[Vector3] = []
        group_indices: List[int] = []

        for primitive in group:
            offset = len(group_positions)
            group_positions.extend(primitive.positions)
            if primitive.normals:
                group_normals.extend(primitive.normals)
            else:
                group_normals.extend(_derive_normals(primitive))
            group_indices.extend(index + offset for index in primitive.indices)

        share = max(64, int(triangle_budget * (len(group_indices) / 3) / max(1, total_triangles)))
        group_positions, group_normals, group_indices = _decimate_to_budget(
            group_positions, group_normals, group_indices, share, diagonal)

        if not group_indices:
            continue

        start = len(indices)
        base = len(positions)
        positions.extend(group_positions)
        normals.extend(group_normals)
        indices.extend(index + base for index in group_indices)

        material = group[0].material
        submeshes.append(Submesh(
            start=start,
            count=len(group_indices),
            color=material.base_color,
            metallic=min(material.metallic, MAX_METALLIC),
            smoothness=material.smoothness,
        ))

    # Orphans have to go before the size is measured, not after: the scale and the declared
    # length are both taken from the bounding box. See compact().
    positions, normals, indices = compact(positions, normals, indices)

    mesh = PackedMesh(positions=positions, normals=normals, indices=indices, submeshes=submeshes)
    _centre_and_scale(mesh, target_length_metres)
    return mesh


def _derive_normals(primitive: gltf.Primitive) -> List[Vector3]:
    """Area-weighted vertex normals, for the rare primitive that ships without any."""
    accumulated = [[0.0, 0.0, 0.0] for _ in primitive.positions]

    for i in range(0, len(primitive.indices) - 2, 3):
        a, b, c = primitive.indices[i], primitive.indices[i + 1], primitive.indices[i + 2]
        normal = _face_normal(primitive.positions[a], primitive.positions[b], primitive.positions[c])
        for index in (a, b, c):
            for axis in range(3):
                accumulated[index][axis] += normal[axis]

    return [_normalise(value) for value in accumulated]


def _decimate_to_budget(positions: List[Vector3], normals: List[Vector3], indices: List[int],
                        budget: int, diagonal: float
                        ) -> Tuple[List[Vector3], List[Vector3], List[int]]:
    """Weld first, then coarsen the grid until the triangle count fits.

    The initial weld uses a cell far below any visible feature size, so it only removes the
    duplicate vertices a Draco decode leaves behind — typically two thirds of them — without
    changing the shape at all.
    """
    positions, normals, indices = cluster_decimate(positions, normals, indices, diagonal * 1e-5)

    cell = diagonal * 0.002
    for _ in range(24):
        if len(indices) // 3 <= budget:
            break
        candidate = cluster_decimate(positions, normals, indices, cell)
        if not candidate[2]:
            break
        positions, normals, indices = candidate
        cell *= 1.6

    return positions, normals, indices


def _limit_submeshes(groups: Dict[Tuple[int, ...], List[gltf.Primitive]], limit: int
                     ) -> Dict[Tuple[int, ...], List[gltf.Primitive]]:
    """Fold the smallest material groups into the nearest surviving colour."""
    if len(groups) <= limit:
        return groups

    ordered = sorted(groups.items(),
                     key=lambda item: sum(p.triangle_count for p in item[1]), reverse=True)
    kept = dict(ordered[:limit])

    for key, group in ordered[limit:]:
        colour = group[0].material.base_color
        nearest = min(kept, key=lambda k: sum(
            (kept[k][0].material.base_color[i] - colour[i]) ** 2 for i in range(3)))
        kept[nearest].extend(group)

    return kept


def _centre_and_scale(mesh: PackedMesh, target_length_metres: float) -> None:
    """Put the model's centre at the origin and its longest axis at the mission's real size.

    The published models carry no consistent unit — the Voyager model is a third of a unit
    across, and the real spacecraft is a few metres — so scale has to come from the mission
    table, where it is a cited measurement rather than whatever the exporter happened to emit.
    """
    if not mesh.positions:
        return

    low, high = mesh.bounds()
    centre = [(low[axis] + high[axis]) * 0.5 for axis in range(3)]
    longest = max(high[axis] - low[axis] for axis in range(3))

    scale = (target_length_metres / longest) if (longest > 0.0 and target_length_metres > 0.0) else 1.0

    mesh.positions = [((p[0] - centre[0]) * scale,
                       (p[1] - centre[1]) * scale,
                       (p[2] - centre[2]) * scale) for p in mesh.positions]


def write(mesh: PackedMesh, path: str) -> int:
    """Write a .csmesh and return its size in bytes."""
    if not mesh.positions or not mesh.indices:
        raise ValueError("refusing to write an empty mesh")
    if len(mesh.positions) != len(mesh.normals):
        raise ValueError(
            f"vertex/normal count mismatch: {len(mesh.positions)} vs {len(mesh.normals)}")

    # Every vertex must be reachable from a triangle. An unreferenced one is invisible and
    # still sets the bounding box, so it silently rescales the whole model -- see compact().
    referenced = set(mesh.indices)
    if len(referenced) != len(mesh.positions):
        raise ValueError(
            f"{len(mesh.positions) - len(referenced)} of {len(mesh.positions)} vertices are "
            "not referenced by any triangle; they would set the bounds and rescale the model. "
            "meshpack.compact() should have removed them.")
    if referenced and max(referenced) >= len(mesh.positions):
        raise ValueError(
            f"index {max(referenced)} is out of range for {len(mesh.positions)} vertices.")

    # Submeshes must tile the index buffer exactly, because the runtime slices it by
    # (start, count) and a gap or an overlap draws the wrong triangles with the wrong material.
    expected_start = 0
    for submesh in mesh.submeshes:
        if submesh.start != expected_start:
            raise ValueError(
                f"submesh starts at {submesh.start} but the previous one ends at {expected_start}.")
        expected_start += submesh.count
    if expected_start != len(mesh.indices):
        raise ValueError(
            f"submeshes cover {expected_start} indices of {len(mesh.indices)}.")

    wide = len(mesh.positions) > 65535
    low, high = mesh.bounds()

    payload = bytearray()
    payload += struct.pack(
        HEADER_FORMAT, MAGIC, 1 if wide else 0,
        len(mesh.positions), len(mesh.indices), len(mesh.submeshes),
        low[0], low[1], low[2], high[0], high[1], high[2],
        mesh.longest_axis())

    vertex_data = bytearray()
    for position, normal in zip(mesh.positions, mesh.normals):
        vertex_data += struct.pack("<6f", position[0], position[1], position[2],
                                   normal[0], normal[1], normal[2])
    payload += vertex_data

    index_format = "<I" if wide else "<H"
    index_data = bytearray()
    for index in mesh.indices:
        index_data += struct.pack(index_format, index)
    payload += index_data

    for submesh in mesh.submeshes:
        payload += struct.pack("<II6f", submesh.start, submesh.count,
                               submesh.color[0], submesh.color[1], submesh.color[2],
                               submesh.color[3], submesh.metallic, submesh.smoothness)

    with open(path, "wb") as handle:
        handle.write(payload)

    return len(payload)
