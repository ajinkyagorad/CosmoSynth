"""A reader for glTF 2.0 binary (.glb) files, in the standard library alone.

NASA publishes its 3D models as .glb, so this is the format the spacecraft pipeline has to
read. The parsing is genuinely simple — a JSON chunk describing a binary chunk — and doing it
here rather than through a package keeps the importer inspectable and lets it *verify* what it
decoded, which turned out to matter more than the parsing itself. See ``validate_decoded``.

What this deliberately does not do is decompress ``KHR_draco_mesh_compression``. Draco is an
entropy-coded connectivity format and a correct decoder is thousands of lines; almost every
NASA model uses it. ``build_spacecraft.py`` shells out to gltf-transform for that step and
hands the resulting plain .glb here.

Reference: https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html
"""

from __future__ import annotations

import json
import struct
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Sequence, Tuple

GLB_MAGIC = b"glTF"
CHUNK_JSON = 0x4E4F534A
CHUNK_BIN = 0x004E4942

#: glTF accessor component types, mapped to (struct code, byte size).
COMPONENT_TYPES: Dict[int, Tuple[str, int]] = {
    5120: ("b", 1),   # BYTE
    5121: ("B", 1),   # UNSIGNED_BYTE
    5122: ("h", 2),   # SHORT
    5123: ("H", 2),   # UNSIGNED_SHORT
    5125: ("I", 4),   # UNSIGNED_INT
    5126: ("f", 4),   # FLOAT
}

#: Number of components per accessor type.
TYPE_COUNTS: Dict[str, int] = {
    "SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT2": 4, "MAT3": 9, "MAT4": 16,
}

#: glTF primitive mode 4. The others (strips, fans, lines, points) do not appear in these
#: models, and silently treating a strip as a triangle soup would produce shredded geometry.
MODE_TRIANGLES = 4


class GltfError(RuntimeError):
    """Raised when a file is not readable as glTF, or uses a feature we do not implement."""


@dataclass
class Material:
    """The subset of a glTF PBR material that survives into a Standard-shader material."""

    name: str = ""
    base_color: Tuple[float, float, float, float] = (0.8, 0.8, 0.8, 1.0)
    metallic: float = 0.0
    roughness: float = 0.7

    @property
    def smoothness(self) -> float:
        """Unity's Standard shader parameterises gloss, which is the complement of roughness."""
        return 1.0 - self.roughness


@dataclass
class Primitive:
    """One drawable run of triangles, with its vertices already in scene coordinates."""

    positions: List[Tuple[float, float, float]] = field(default_factory=list)
    normals: List[Tuple[float, float, float]] = field(default_factory=list)
    indices: List[int] = field(default_factory=list)
    material: Material = field(default_factory=Material)

    @property
    def triangle_count(self) -> int:
        return len(self.indices) // 3


def _identity() -> List[float]:
    return [1.0, 0.0, 0.0, 0.0,
            0.0, 1.0, 0.0, 0.0,
            0.0, 0.0, 1.0, 0.0,
            0.0, 0.0, 0.0, 1.0]


def _matrix_multiply(a: Sequence[float], b: Sequence[float]) -> List[float]:
    """Row-major 4x4 product a*b."""
    out = [0.0] * 16
    for row in range(4):
        for col in range(4):
            out[row * 4 + col] = sum(a[row * 4 + k] * b[k * 4 + col] for k in range(4))
    return out


def _compose(translation: Sequence[float], rotation: Sequence[float],
             scale: Sequence[float]) -> List[float]:
    """Build a row-major TRS matrix from glTF's separate components.

    The quaternion is stored xyzw, not wxyz. Reading it in the other order gives a rotation
    that is plausible but wrong, which on a symmetric spacecraft is almost impossible to spot.
    """
    x, y, z, w = rotation
    sx, sy, sz = scale
    tx, ty, tz = translation

    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z

    return [
        (1 - 2 * (yy + zz)) * sx, 2 * (xy - wz) * sy, 2 * (xz + wy) * sz, tx,
        2 * (xy + wz) * sx, (1 - 2 * (xx + zz)) * sy, 2 * (yz - wx) * sz, ty,
        2 * (xz - wy) * sx, 2 * (yz + wx) * sy, (1 - 2 * (xx + yy)) * sz, tz,
        0.0, 0.0, 0.0, 1.0,
    ]


def _transform_point(m: Sequence[float], p: Sequence[float]) -> Tuple[float, float, float]:
    return (m[0] * p[0] + m[1] * p[1] + m[2] * p[2] + m[3],
            m[4] * p[0] + m[5] * p[1] + m[6] * p[2] + m[7],
            m[8] * p[0] + m[9] * p[1] + m[10] * p[2] + m[11])


def _determinant3(m: Sequence[float]) -> float:
    """Determinant of the upper-left 3x3 block of a row-major 4x4."""
    a, b, c = m[0], m[1], m[2]
    d, e, f = m[4], m[5], m[6]
    g, h, i = m[8], m[9], m[10]
    return a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g)


def _normal_matrix(m: Sequence[float]) -> List[float]:
    """The 3x3 a normal must be transformed by: the inverse transpose of the upper-left block.

    Under uniform scale this is the matrix itself up to a factor the renormalisation removes —
    for any orthogonal M, proper or improper, M^-T is M/s^2 — so the cheap path is exact even
    for a mirror. Under non-uniform scale it is not: squashing a node along one axis tilts its
    surfaces one way and its normals the other, and using the matrix directly lights a
    flattened panel as though it were still square. Several NASA models do exactly this —
    Perseverance and the InSight lander both carry non-uniformly scaled nodes.

    The sign matters and used to be wrong. The cofactor matrix is det(M) * M^-T, and for a
    *mirroring* transform det is negative, so the cofactor is the inverse transpose with every
    normal reversed — a model lit from behind every surface. Multiplying by the sign of the
    determinant puts it back. See ``_mesh_primitives`` for the other half of the same problem.
    """
    a, b, c = m[0], m[1], m[2]
    d, e, f = m[4], m[5], m[6]
    g, h, i = m[8], m[9], m[10]

    lengths = [(a * a + d * d + g * g) ** 0.5,
               (b * b + e * e + h * h) ** 0.5,
               (c * c + f * f + i * i) ** 0.5]
    longest, shortest = max(lengths), min(lengths)
    if shortest > 0.0 and longest / shortest <= 1.001:
        return [a, b, c, d, e, f, g, h, i]

    cofactor = [
        e * i - f * h, f * g - d * i, d * h - e * g,
        c * h - b * i, a * i - c * g, b * g - a * h,
        b * f - c * e, c * d - a * f, a * e - b * d,
    ]
    if _determinant3(m) < 0.0:
        return [-value for value in cofactor]
    return cofactor


def _transform_normal(n: Sequence[float], v: Sequence[float]) -> Tuple[float, float, float]:
    """Apply a 3x3 normal matrix to a direction."""
    return (n[0] * v[0] + n[1] * v[1] + n[2] * v[2],
            n[3] * v[0] + n[4] * v[1] + n[5] * v[2],
            n[6] * v[0] + n[7] * v[1] + n[8] * v[2])


def _node_matrix(node: Dict[str, Any]) -> List[float]:
    if "matrix" in node:
        # glTF stores matrices column-major; this module works row-major throughout.
        c = node["matrix"]
        return [c[0], c[4], c[8], c[12],
                c[1], c[5], c[9], c[13],
                c[2], c[6], c[10], c[14],
                c[3], c[7], c[11], c[15]]

    return _compose(node.get("translation", (0.0, 0.0, 0.0)),
                    node.get("rotation", (0.0, 0.0, 0.0, 1.0)),
                    node.get("scale", (1.0, 1.0, 1.0)))


class GlbFile:
    """A parsed .glb: the JSON document plus its binary buffer."""

    def __init__(self, document: Dict[str, Any], binary: bytes):
        self.document = document
        self.binary = binary

    # ---- Container ----------------------------------------------------------

    @classmethod
    def from_bytes(cls, data: bytes) -> "GlbFile":
        if len(data) < 12 or data[:4] != GLB_MAGIC:
            # NASA's asset hosts answer wrong paths with HTTP 200 and an HTML error page, so a
            # successful download proves nothing at all about what arrived.
            raise GltfError(
                f"not a .glb: expected magic {GLB_MAGIC!r}, got {data[:4]!r} "
                f"({len(data)} bytes). A 200 response is not evidence of a model.")

        _, version, total_length = struct.unpack_from("<4sII", data, 0)
        if version != 2:
            raise GltfError(f"glb container version {version}; only version 2 is supported.")
        if total_length > len(data):
            raise GltfError(
                f"glb header declares {total_length} bytes but the file holds {len(data)}; "
                "the download was truncated.")

        document: Optional[Dict[str, Any]] = None
        binary = b""

        offset = 12
        while offset + 8 <= total_length:
            chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
            body = data[offset + 8: offset + 8 + chunk_length]

            if chunk_type == CHUNK_JSON:
                document = json.loads(body.decode("utf-8"))
            elif chunk_type == CHUNK_BIN:
                binary = body

            # Chunks are padded to a four-byte boundary.
            offset += 8 + chunk_length
            offset += (4 - offset % 4) % 4

        if document is None:
            raise GltfError("glb contains no JSON chunk.")

        required = document.get("extensionsRequired", [])
        unsupported = [name for name in required if name not in _SUPPORTED_EXTENSIONS]
        if unsupported:
            raise GltfError(
                f"file requires unsupported glTF extensions {unsupported}. "
                "Draco-compressed models must be decompressed before they reach this reader "
                "(build_spacecraft.py does this with gltf-transform).")

        return cls(document, binary)

    @classmethod
    def from_file(cls, path: str) -> "GlbFile":
        with open(path, "rb") as handle:
            return cls.from_bytes(handle.read())

    # ---- Accessors ----------------------------------------------------------

    def read_accessor(self, index: int) -> List[Tuple[float, ...]]:
        """Read one accessor as a list of component tuples."""
        accessor = self.document["accessors"][index]

        if "sparse" in accessor:
            raise GltfError("sparse accessors are not supported.")

        component_type = accessor["componentType"]
        if component_type not in COMPONENT_TYPES:
            raise GltfError(f"unknown accessor componentType {component_type}.")

        code, component_size = COMPONENT_TYPES[component_type]
        components = TYPE_COUNTS[accessor["type"]]
        count = accessor["count"]

        # An accessor with no bufferView reads as zeros by specification. That is exactly the
        # shape of a silently-failed Draco decode, so refuse it rather than returning a mesh
        # collapsed to the origin.
        if "bufferView" not in accessor:
            raise GltfError(
                "accessor has no bufferView, which would decode to all-zero data. "
                "This is what a failed Draco decompression looks like.")

        view = self.document["bufferViews"][accessor["bufferView"]]
        base = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        stride = view.get("byteStride") or components * component_size

        values: List[Tuple[float, ...]] = []
        element_format = "<" + code * components
        for i in range(count):
            start = base + i * stride
            values.append(struct.unpack_from(element_format, self.binary, start))

        if accessor.get("normalized"):
            scale = {"b": 127.0, "B": 255.0, "h": 32767.0, "H": 65535.0}.get(code)
            if scale:
                values = [tuple(max(v / scale, -1.0) for v in element) for element in values]

        return values

    # ---- Materials ----------------------------------------------------------

    def material(self, index: Optional[int]) -> Material:
        if index is None:
            return Material()

        source = self.document.get("materials", [])[index]
        pbr = source.get("pbrMetallicRoughness", {})
        colour = pbr.get("baseColorFactor", [0.8, 0.8, 0.8, 1.0])

        return Material(
            name=source.get("name", f"material{index}"),
            base_color=(float(colour[0]), float(colour[1]), float(colour[2]),
                        float(colour[3]) if len(colour) > 3 else 1.0),
            metallic=float(pbr.get("metallicFactor", 1.0)),
            roughness=float(pbr.get("roughnessFactor", 1.0)),
        )

    # ---- Scene --------------------------------------------------------------

    def primitives(self) -> List[Primitive]:
        """Flatten the default scene into world-space triangle runs.

        Node transforms are baked into the vertices here rather than carried alongside them.
        The runtime draws each spacecraft as a single static mesh, so a hierarchy it can never
        articulate is only a source of error.
        """
        document = self.document
        nodes = document.get("nodes", [])
        scene_index = document.get("scene", 0)
        scenes = document.get("scenes", [])
        roots = scenes[scene_index]["nodes"] if scenes else list(range(len(nodes)))

        results: List[Primitive] = []
        stack: List[Tuple[int, List[float]]] = [(index, _identity()) for index in reversed(roots)]
        visited = set()

        while stack:
            node_index, parent_matrix = stack.pop()
            if node_index in visited:
                raise GltfError("node graph contains a cycle.")
            visited.add(node_index)

            node = nodes[node_index]
            world = _matrix_multiply(parent_matrix, _node_matrix(node))

            if "mesh" in node:
                results.extend(self._mesh_primitives(node["mesh"], world))

            for child in reversed(node.get("children", [])):
                stack.append((child, world))

        return results

    def _mesh_primitives(self, mesh_index: int, world: Sequence[float]) -> List[Primitive]:
        mesh = self.document["meshes"][mesh_index]
        results: List[Primitive] = []

        for primitive in mesh["primitives"]:
            if primitive.get("mode", MODE_TRIANGLES) != MODE_TRIANGLES:
                continue

            attributes = primitive.get("attributes", {})
            if "POSITION" not in attributes:
                continue

            raw_positions = self.read_accessor(attributes["POSITION"])
            positions = [_transform_point(world, p) for p in raw_positions]

            if "NORMAL" in attributes:
                normal_matrix = _normal_matrix(world)
                raw_normals = self.read_accessor(attributes["NORMAL"])
                normals = [_normalise(_transform_normal(normal_matrix, n)) for n in raw_normals]
            else:
                normals = []

            if "indices" in primitive:
                indices = [int(value[0]) for value in self.read_accessor(primitive["indices"])]
            else:
                indices = list(range(len(positions)))

            # A node whose transform mirrors must have its winding reversed.
            #
            # This is in the specification -- "when the determinant of the node's global
            # transform is negative, the winding order of the mesh triangle faces should be
            # reversed" -- and it is not a corner case: mirroring one half of a symmetric
            # spacecraft is how these models are authored. Baking a mirror into the vertices
            # without it leaves every face pointing inwards, which renders as a hollow shell
            # you can see the inside of and reads as "the model failed to load".
            #
            # SOHO shipped exactly this way: 2% of its triangles agreed with their own normals,
            # against 63-97% for every other model in the set.
            if _determinant3(world) < 0.0:
                indices = [indices[i + offset]
                           for i in range(0, len(indices) - 2, 3)
                           for offset in (0, 2, 1)]

            results.append(Primitive(
                positions=positions,
                normals=normals,
                indices=indices,
                material=self.material(primitive.get("material")),
            ))

        return results


#: Extensions we can ignore without changing the geometry we read. Anything listed in a file's
#: ``extensionsRequired`` and absent here is a hard error.
_SUPPORTED_EXTENSIONS = {
    "KHR_materials_unlit",
    "KHR_materials_emissive_strength",
    "KHR_texture_transform",
    "KHR_materials_specular",
    "KHR_materials_ior",
}


def required_extensions(path: str) -> List[str]:
    """Read only a file's ``extensionsRequired``, without validating anything else.

    ``GlbFile.from_bytes`` refuses a file that needs an extension it cannot honour, which is
    the behaviour we want everywhere except in the one place that has to *detect* Draco in
    order to go and decompress it.
    """
    with open(path, "rb") as handle:
        head = handle.read(1 << 20)

    if head[:4] != GLB_MAGIC:
        raise GltfError(f"not a .glb: magic is {head[:4]!r}.")

    chunk_length, chunk_type = struct.unpack_from("<II", head, 12)
    if chunk_type != CHUNK_JSON:
        raise GltfError("first glb chunk is not JSON.")

    if 20 + chunk_length > len(head):
        with open(path, "rb") as handle:
            head = handle.read(20 + chunk_length)

    document = json.loads(head[20:20 + chunk_length].decode("utf-8"))
    return list(document.get("extensionsRequired", []))


def _normalise(v: Sequence[float]) -> Tuple[float, float, float]:
    length = (v[0] * v[0] + v[1] * v[1] + v[2] * v[2]) ** 0.5
    if length <= 0.0:
        return (0.0, 1.0, 0.0)
    return (v[0] / length, v[1] / length, v[2] / length)


def bounds(primitives: Sequence[Primitive]) -> Tuple[Tuple[float, float, float],
                                                     Tuple[float, float, float]]:
    """Axis-aligned bounds over every vertex actually referenced by an index."""
    low = [float("inf")] * 3
    high = [float("-inf")] * 3

    for primitive in primitives:
        for index in primitive.indices:
            point = primitive.positions[index]
            for axis in range(3):
                low[axis] = min(low[axis], point[axis])
                high[axis] = max(high[axis], point[axis])

    if low[0] > high[0]:
        return (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)
    return (low[0], low[1], low[2]), (high[0], high[1], high[2])


def validate_decoded(primitives: Sequence[Primitive], label: str) -> None:
    """Refuse a mesh that parsed cleanly but holds no geometry.

    This exists because of a specific near-miss. A well-known mesh library, asked to read one
    of these Draco-compressed models without a Draco decoder present, returned a scene with
    the correct number of geometries and *exactly* the right triangle count — and every single
    vertex at the origin. Triangle counts matched the file header, so every cheap sanity check
    passed while the model was a point.

    Counting triangles therefore proves nothing. The only honest check is that the thing
    occupies space.
    """
    if not primitives:
        raise GltfError(f"{label}: no triangle primitives found.")

    triangles = sum(p.triangle_count for p in primitives)
    if triangles == 0:
        raise GltfError(f"{label}: primitives contain no triangles.")

    low, high = bounds(primitives)
    size = [high[axis] - low[axis] for axis in range(3)]
    longest = max(size)

    if longest <= 1e-9:
        raise GltfError(
            f"{label}: {triangles} triangles decoded to a degenerate bounding box {size}. "
            "Every vertex is at the same point, which means the geometry was never actually "
            "decoded — the usual cause is Draco compression that was silently skipped.")

    # A model flattened on one axis is a plane, not a spacecraft.
    if min(size) <= longest * 1e-6:
        raise GltfError(f"{label}: bounding box {size} is degenerate on at least one axis.")

    agreement = winding_agreement(primitives)
    if agreement is not None and agreement < 0.5:
        raise GltfError(
            f"{label}: only {agreement:.1%} of triangles are wound to match their own normals, "
            "so the model is inside out. The usual cause is a mirroring node transform whose "
            "winding reversal was skipped; see GlbFile._mesh_primitives.")


def signed_volume(primitives: Sequence[Primitive]) -> float:
    """Volume enclosed by the triangles, signed by their winding, in scene units cubed.

    The divergence theorem applied to a triangle soup: sum a . (b x c) / 6 over every face. For
    a closed surface wound outwards this is the volume it encloses, and for one wound inwards it
    is minus that. It is worth having because it reads the *geometry* and nothing else — no
    normals, no material flags, no node metadata — so it is an independent second opinion on the
    question ``winding_agreement`` answers from the normals' point of view.

    It means nothing for an open surface, where the answer drifts around zero. Both signals are
    therefore required before anything is changed on the strength of them.
    """
    total = 0.0
    for primitive in primitives:
        points = primitive.positions
        indices = primitive.indices
        for i in range(0, len(indices) - 2, 3):
            a, b, c = points[indices[i]], points[indices[i + 1]], points[indices[i + 2]]
            total += (a[0] * (b[1] * c[2] - b[2] * c[1])
                      - a[1] * (b[0] * c[2] - b[2] * c[0])
                      + a[2] * (b[0] * c[1] - b[1] * c[0])) / 6.0
    return total


#: How decisively the enclosed volume has to point one way before it is allowed to arbitrate,
#: as a fraction of the bounding box cubed. Measured over the 24 shipped source models: the
#: ones whose winding and normals agree run from 0.0006 to 0.089, SOHO — which disagrees —
#: is +0.0149, and an open shell sits near zero. 1e-3 is inside that gap on both sides.
VOLUME_DECISIVE = 1e-3


def orient_outward(primitives: Sequence[Primitive]) -> Tuple[List[Primitive], str]:
    """Repair a model whose winding and normals disagree. Returns (primitives, what was done).

    Some published models are authored inside out, and no amount of correct importing changes
    that. SOHO is one: with the mirroring rule in ``_mesh_primitives`` applied, its triangles
    still disagreed with their own normals on 100.0% of faces. Refusing it costs the mission its
    geometry, and shipping it costs the user a spacecraft lit from inside.

    glTF fixes both conventions at once — counter-clockwise front faces, normals pointing away
    from them — so when they disagree exactly one of the two is wrong, and which one decides
    what to change. ``signed_volume`` is the arbiter because it reads the geometry and nothing
    else: no normals, no material flags, no metadata.

    * Volume decisively **positive**: the faces enclose the solid the right way round, so the
      winding is correct and the normals are inverted. Negate the normals. This is SOHO, whose
      source encloses +0.0149 of its bounding box cubed — squarely among the models that need
      no repair at all — while agreeing with its own normals 0.0% of the time.
    * Volume decisively **negative**: the normals are right and the triangles are wound inwards.
      Reverse the winding.
    * Volume near zero: an open shell, where the quantity means nothing. Nothing here can tell
      which half is wrong, so nothing is changed and ``validate_decoded`` refuses the model
      rather than guessing.

    Note the two failures are not the same to look at, which is worth knowing when one is
    reported: inverted normals draw a solid object lit from behind, while inverted winding draws
    an object whose front faces are culled and which you therefore see the inside of.
    """
    agreement = winding_agreement(primitives)
    if agreement is None or agreement >= 0.5:
        return list(primitives), ""

    low, high = bounds(primitives)
    extent = max(high[axis] - low[axis] for axis in range(3))
    if extent <= 0.0:
        return list(primitives), ""

    enclosed = signed_volume(primitives) / extent ** 3

    if enclosed > VOLUME_DECISIVE:
        return [
            Primitive(positions=p.positions, indices=p.indices, material=p.material,
                      normals=[(-n[0], -n[1], -n[2]) for n in p.normals])
            for p in primitives
        ], "normals negated"

    if enclosed < -VOLUME_DECISIVE:
        return [
            Primitive(positions=p.positions, normals=p.normals, material=p.material,
                      indices=[p.indices[i + offset]
                               for i in range(0, len(p.indices) - 2, 3)
                               for offset in (0, 2, 1)])
            for p in primitives
        ], "winding reversed"

    # Open geometry: the volume is not a statement about anything. Leave it alone and let
    # validate_decoded refuse it, because a coin flip between two halves of a model is not a
    # repair, it is a 50% chance of shipping the same bug with a different cause.
    return list(primitives), ""


def winding_agreement(primitives: Sequence[Primitive]) -> Optional[float]:
    """Fraction of triangles whose winding agrees with their vertex normals, or None.

    glTF fixes both conventions: front faces are counter-clockwise, and NORMAL points away
    from the front face. So for a correctly oriented triangle ``cross(b-a, c-a)`` and the
    vertex normals point the same way, and the dot product is positive.

    This is the cheapest test that separates a model which decoded correctly from one that
    decoded inside out, and it is the mesh equivalent of ``validate_decoded``'s extent check:
    triangle counts, bounding boxes and file sizes are all identical either way. It is a
    fraction rather than a hard rule because real CAD exports contain open shells, coincident
    two-sided panels and a few genuinely broken faces -- the good models here score 63-97%,
    and a mirrored one scores 2%, so the two populations do not overlap anywhere near half.

    Returns None when no primitive carries normals, where the question does not arise.
    """
    agreeing = 0
    total = 0

    for primitive in primitives:
        if not primitive.normals:
            continue
        for i in range(0, len(primitive.indices) - 2, 3):
            a, b, c = primitive.indices[i], primitive.indices[i + 1], primitive.indices[i + 2]
            pa, pb, pc = primitive.positions[a], primitive.positions[b], primitive.positions[c]
            u = (pb[0] - pa[0], pb[1] - pa[1], pb[2] - pa[2])
            v = (pc[0] - pa[0], pc[1] - pa[1], pc[2] - pa[2])
            cross = (u[1] * v[2] - u[2] * v[1],
                     u[2] * v[0] - u[0] * v[2],
                     u[0] * v[1] - u[1] * v[0])

            normal = [primitive.normals[a][axis] + primitive.normals[b][axis]
                      + primitive.normals[c][axis] for axis in range(3)]

            total += 1
            if sum(cross[axis] * normal[axis] for axis in range(3)) > 0.0:
                agreeing += 1

    if total == 0:
        return None
    return agreeing / total
