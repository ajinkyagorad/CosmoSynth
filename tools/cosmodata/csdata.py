#!/usr/bin/env python3
"""The one column-store container every new CosmoSynth binary uses (design B section 1.1/1.3).

Ten datasets would otherwise mean ten hand-offset parsers on both sides of the language
boundary. Instead: one header, one JSON meta block, one array of 8-byte-aligned columns, and
one reader in each language that refuses anything it does not recognise.

    from csdata import write, read, Column, pack
    write(path, "CSLV", meta, [Column("pos", "f32", 3, pack("f32", xyz))])

Layout, all integers little-endian:

    0   4   magic         4 ASCII bytes naming the kind ("CSLV", "CSPT", ...)
    4   4   version u32   layout version of that kind
    8   4   rows u32      rows of the primary table
    12  4   flags u32     reserved, must be 0
    16  4   metaBytes u32 UTF-8 JSON meta block length, a multiple of 8, space padded
    20  4   reserved u32  must be 0
    24  8   payloadBytes  u64
    32      meta JSON
    +       columns, each starting 8-byte aligned from the payload start

Standard library only.

WHY THE WRITE IS PARANOID
    build_spacecraft.py shipped a mesh that parsed and was wrong, because the artefact on
    disk outlived the run that failed. So: write to a temporary name, fsync it, read it back
    through this module's own reader, check the magic, the exact length and the first and
    last element of every column, and only then os.replace. On any failure delete both the
    temporary file and the stale target, so "a file exists" can never be mistaken for
    "the importer succeeded".
"""

from __future__ import annotations

import array
import hashlib
import json
import os
import struct
import subprocess
import sys
from typing import Dict, List, Optional, Sequence

HEADER = "<4sIIIIIQ"
HEADER_BYTES = struct.calcsize(HEADER)          # 32
assert HEADER_BYTES == 32, HEADER_BYTES

#: name -> (struct code, bytes, array typecode). No f16: System.Half is not in
#: netstandard 2.1 and the saving does not pay for a hand-written converter in C#.
TYPES: Dict[str, tuple] = {
    "u8": ("B", 1, "B"), "i8": ("b", 1, "b"),
    "u16": ("H", 2, "H"), "i16": ("h", 2, "h"),
    "u32": ("I", 4, "I"), "i32": ("i", 4, "i"),
    "f32": ("f", 4, "f"), "f64": ("d", 8, "d"),
}

ALIGN = 8


class Column:
    """One column of the payload. `data` is already-packed little-endian bytes."""

    def __init__(self, name: str, type_: str, components: int, data: bytes,
                 unit: Optional[str] = None, extra: Optional[dict] = None,
                 table: Optional[str] = None, rows: Optional[int] = None):
        if type_ not in TYPES:
            raise ValueError(f"column {name!r}: unknown type {type_!r}")
        if components < 1:
            raise ValueError(f"column {name!r}: components must be >= 1")
        width = TYPES[type_][1] * components
        if len(data) % width:
            raise ValueError(
                f"column {name!r}: {len(data)} bytes is not a whole number of "
                f"{width}-byte elements")
        self.name = name
        self.type = type_
        self.components = components
        self.data = data
        self.unit = unit
        self.extra = extra or {}
        self.table = table
        self.rows = rows if rows is not None else len(data) // width

    @property
    def element_bytes(self) -> int:
        return TYPES[self.type][1] * self.components

    def to_meta(self, offset: int) -> dict:
        m = {"name": self.name, "type": self.type, "offset": offset}
        if self.components != 1:
            m["components"] = self.components
        if self.unit:
            m["unit"] = self.unit
        if self.table:
            m["table"] = self.table
            m["rows"] = self.rows
        m.update(self.extra)
        return m


def pack(type_: str, values: Sequence) -> bytes:
    """Pack a flat sequence into a column payload.

    Uses `array`, not `struct.pack("<%d%s")`, because the large columns here are hundreds of
    thousands of elements and struct would need that many positional arguments.
    """
    if type_ not in TYPES:
        raise ValueError(f"unknown type {type_!r}")
    buf = array.array(TYPES[type_][2], values)
    if sys.byteorder != "little":
        buf.byteswap()
    return buf.tobytes()


def unpack(type_: str, data: bytes) -> array.array:
    buf = array.array(TYPES[type_][2])
    buf.frombytes(data)
    if sys.byteorder != "little":
        buf.byteswap()
    return buf


def _encode_meta(meta: dict) -> bytes:
    text = json.dumps(meta, separators=(",", ":"), ensure_ascii=False, sort_keys=False)
    raw = text.encode("utf-8")
    pad = (-len(raw)) % ALIGN                      # a multiple of 8, padded with spaces
    return raw + b" " * pad


REQUIRED_META = ("kind", "schema", "generator", "generatedUtc", "licenceProfile",
                 "frame", "sources", "provenance")


def write(path: str, magic: str, meta: dict, columns: List[Column], version: int = 1,
          rows: Optional[int] = None) -> int:
    """Write the container, verify it by reading it back, then os.replace into place.

    Returns the file size in bytes. Raises on any inconsistency, having deleted both the
    temporary file and any stale file at `path`.
    """
    if len(magic) != 4:
        raise ValueError(f"magic must be 4 ASCII bytes, got {magic!r}")
    for key in REQUIRED_META:
        if key not in meta:
            raise ValueError(f"meta is missing the required key {key!r}")
    if not columns:
        raise ValueError("a container with no columns is refused")

    primary = rows if rows is not None else columns[0].rows

    # Lay the payload out, each column starting 8-byte aligned from the payload start.
    payload = bytearray()
    meta_columns = []
    for column in columns:
        pad = (-len(payload)) % ALIGN
        payload.extend(b"\0" * pad)
        meta_columns.append(column.to_meta(len(payload)))
        payload.extend(column.data)

    meta = dict(meta)
    meta["columns"] = meta_columns
    meta_bytes = _encode_meta(meta)

    header = struct.pack(HEADER, magic.encode("ascii"), version, primary, 0,
                         len(meta_bytes), 0, len(payload))

    tmp = path + ".tmp"
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    try:
        with open(tmp, "wb") as handle:
            handle.write(header)
            handle.write(meta_bytes)
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())

        # Read it back through the reader that ships, not through the variables we still
        # have in memory. A writer that verifies against its own intent verifies nothing.
        back = read(tmp, expect_magic=magic)
        if back.rows != primary:
            raise ValueError(f"readback rows {back.rows} != {primary}")
        if os.path.getsize(tmp) != HEADER_BYTES + len(meta_bytes) + len(payload):
            raise ValueError("readback size disagrees with the header")
        for column in columns:
            got = back.column_bytes(column.name)
            if len(got) != len(column.data):
                raise ValueError(f"column {column.name!r} readback length differs")
            n = column.element_bytes
            if column.data and (got[:n] != column.data[:n] or got[-n:] != column.data[-n:]):
                raise ValueError(f"column {column.name!r} first/last element differs")
    except BaseException:
        for victim in (tmp, path):
            try:
                os.unlink(victim)
            except OSError:
                pass
        raise

    os.replace(tmp, path)
    return HEADER_BYTES + len(meta_bytes) + len(payload)


class CsData:
    """Reader. Mirrors Core/Data/CsData.cs; the fixture keeps the two honest."""

    def __init__(self, magic: str, version: int, rows: int, meta: dict, payload: bytes):
        self.magic, self.version, self.rows, self.meta = magic, version, rows, meta
        self._payload = payload
        self._columns = {c["name"]: c for c in meta.get("columns", [])}

    def has(self, name: str) -> bool:
        return name in self._columns

    def column_bytes(self, name: str) -> bytes:
        c = self._columns[name]
        width = TYPES[c["type"]][1] * c.get("components", 1)
        n = c.get("rows", self.rows)
        return self._payload[c["offset"]:c["offset"] + width * n]

    def column(self, name: str) -> list:
        c = self._columns[name]
        return list(unpack(c["type"], self.column_bytes(name)))


def read(path: str, expect_magic: Optional[str] = None, max_version: int = 1) -> CsData:
    with open(path, "rb") as handle:
        blob = handle.read()
    if len(blob) < HEADER_BYTES:
        raise ValueError(f"{path}: shorter than a {HEADER_BYTES}-byte header")
    magic, version, rows, flags, meta_bytes, reserved, payload_bytes = \
        struct.unpack(HEADER, blob[:HEADER_BYTES])
    magic = magic.decode("ascii", "replace")
    if expect_magic and magic != expect_magic:
        raise ValueError(f"{path}: magic is {magic!r}, expected {expect_magic!r}")
    if version > max_version:
        raise ValueError(f"{path}: version {version} is newer than {max_version}")
    if flags or reserved:
        raise ValueError(f"{path}: reserved header fields must be zero")
    if meta_bytes % ALIGN:
        raise ValueError(f"{path}: metaBytes {meta_bytes} is not a multiple of {ALIGN}")
    want = HEADER_BYTES + meta_bytes + payload_bytes
    if len(blob) != want:
        raise ValueError(f"{path}: header declares {want} bytes, file is {len(blob)}")
    meta = json.loads(blob[HEADER_BYTES:HEADER_BYTES + meta_bytes].decode("utf-8"))
    payload = blob[HEADER_BYTES + meta_bytes:]
    for c in meta.get("columns", []):
        if c["type"] not in TYPES:
            raise ValueError(f"{path}: column {c['name']!r} has unknown type {c['type']!r}")
        if c["offset"] % ALIGN:
            raise ValueError(f"{path}: column {c['name']!r} offset is not 8-byte aligned")
        width = TYPES[c["type"]][1] * c.get("components", 1)
        end = c["offset"] + width * c.get("rows", rows)
        if end > len(payload):
            raise ValueError(f"{path}: column {c['name']!r} runs {end - len(payload)} bytes past the end")
    return CsData(magic, version, rows, meta, payload)


# --------------------------------------------------------------------------------------
# Small shared utilities every importer in this family needs.
# --------------------------------------------------------------------------------------

def sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def generator_commit(repo_root: str) -> str:
    """'<short sha>' or '<short sha>+dirty', or 'unknown' outside a checkout."""
    try:
        sha = subprocess.check_output(["git", "-C", repo_root, "rev-parse", "--short", "HEAD"],
                                      stderr=subprocess.DEVNULL).decode().strip()
        dirty = subprocess.check_output(["git", "-C", repo_root, "status", "--porcelain"],
                                        stderr=subprocess.DEVNULL).decode().strip()
        return sha + ("+dirty" if dirty else "")
    except Exception:
        return "unknown"


def _guid(path: str) -> str:
    """A guid derived from the asset path, so rebuilding data does not churn the repo."""
    rel = path.replace("\\", "/").split("Assets/", 1)[-1]
    return hashlib.md5(("cosmosynth:" + rel).encode("utf-8")).hexdigest()


def write_unity_meta(path: str, folder: bool = False) -> None:
    """Write the .meta Unity would generate on import.

    A lane that never opens the editor has to write these itself, or the file is simply not
    in the build: StreamingAssets is packed from the asset database, not from the directory.
    """
    body = "folderAsset: yes\n" if folder else ""
    with open(path + ".meta", "w", newline="\n") as handle:
        handle.write("fileFormatVersion: 2\nguid: %s\n%sDefaultImporter:\n"
                     "  externalObjects: {}\n  userData: \n  assetBundleName: \n"
                     "  assetBundleVariant: \n" % (_guid(path), body))


# --------------------------------------------------------------------------------------
# The cross-language contract: one small file, committed, parsed by both readers.
# --------------------------------------------------------------------------------------

def selftest(out: str) -> None:
    meta = {
        "kind": "fixture", "schema": 1,
        "generator": "tools/cosmodata/csdata.py --selftest", "generatorCommit": "fixture",
        "generatedUtc": "2026-09-12T00:00:00Z", "licenceProfile": "commercial",
        "frame": "ICRF", "epoch": "J2000.0",
        "sources": [{"id": "none", "name": "synthetic", "licence": "CC0", "licenceTag": "cc0"}],
        "provenance": {"f32c3": {"class": "Procedural", "source": "csdata selftest"}},
        "provenanceBits": [{"bit": 0, "field": "f32c3", "class": "Inferred", "note": "bit 0 set"}],
        "grid": {"dims": [3, 1, 1], "sampling": "nodes", "origin": [0, 0, 0],
                 "step": [1, 1, 1], "unit": "pc", "frame": "galactic-helio"},
        "counts": {"rows": 3},
        "assertions": [{"name": "rows", "expected": 3, "got": 3, "ok": True}],
    }
    columns = [
        Column("f32c3", "f32", 3, pack("f32", [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0]), unit="pc"),
        Column("f64", "f64", 1, pack("f64", [-1.5, 0.0, 1.5])),
        Column("u8", "u8", 1, pack("u8", [0, 128, 255]),
               extra={"encoding": {"kind": "linear", "scale": 0.5, "offset": -1.0}}),
        Column("i8", "i8", 1, pack("i8", [-128, 0, 127])),
        Column("u16", "u16", 1, pack("u16", [0, 1000, 65535]),
               extra={"encoding": {"kind": "log8", "lo": 0.01, "hi": 100.0}}),
        Column("i16", "i16", 1, pack("i16", [-32768, 0, 32767])),
        Column("u32", "u32", 1, pack("u32", [0, 123456, 4294967295])),
        Column("i32", "i32", 1, pack("i32", [-2147483648, 0, 2147483647])),
        Column("flags", "u32", 1, pack("u32", [0, 1, 3])),
    ]
    size = write(out, "CSDF", meta, columns)
    back = read(out, "CSDF")
    assert back.rows == 3 and back.column("f64") == [-1.5, 0.0, 1.5], "fixture readback failed"
    print(f"wrote {out} ({size} bytes, {back.rows} rows, {len(columns)} columns)")


if __name__ == "__main__":
    if len(sys.argv) >= 2 and sys.argv[1] == "--selftest":
        here = os.path.dirname(os.path.abspath(__file__))
        default = os.path.join(here, "..", "..", "tests", "CosmoSynth.Core.Tests",
                               "fixtures", "csdata_fixture.csd")
        selftest(os.path.abspath(sys.argv[2] if len(sys.argv) > 2 else default))
    else:
        print(__doc__)
