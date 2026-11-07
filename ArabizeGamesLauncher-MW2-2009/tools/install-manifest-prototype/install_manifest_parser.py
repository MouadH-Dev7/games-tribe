"""Bounded parser for synthetic Blizzard TACT INSTALL v1 manifest buffers.

This module only parses bytes supplied by the caller. It has no game path,
CASC access, network, decompression, extraction, or payload resolution logic.
The caller must explicitly choose MSB-first or LSB-first tag bitmap ordering;
INSTALL bitmap bit order is not verified by the locally available source.
"""

from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
from typing import Any, Iterable

INSTALL_MAGIC = b"IN"
SUPPORTED_VERSION = 1
EXPECTED_HASH_SIZE = 16
HEADER_SIZE = 10
MAX_MANIFEST_BYTES = 64 * 1024 * 1024
MAX_TAGS = 4096
MAX_ENTRIES = 1_000_000
MAX_STRING_BYTES = 4096
BIT_ORDERS = frozenset({"msb", "lsb"})
ARABIC_TAGS = ("arSA text", "arSA speech")


class ManifestParseError(ValueError):
    """A malformed or unsupported manifest was rejected safely."""


@dataclass(frozen=True)
class Tag:
    name: str
    tag_type: int
    bitmap: bytes


@dataclass(frozen=True)
class Entry:
    index: int
    path: str
    ckey: str
    size: int
    tags: tuple[dict[str, Any], ...]

    def as_dict(self) -> dict[str, Any]:
        return {
            "index": self.index,
            "path": self.path,
            "ckey": self.ckey,
            "size": self.size,
            "tags": [dict(tag) for tag in self.tags],
        }


@dataclass(frozen=True)
class ParsedManifest:
    version: int
    hash_size: int
    tags: tuple[Tag, ...]
    entries: tuple[Entry, ...]
    bit_order: str
    duplicate_tag_names: tuple[str, ...]
    duplicate_paths: tuple[str, ...]

    def as_dict(self) -> dict[str, Any]:
        return {
            "version": self.version,
            "hash_size": self.hash_size,
            "bit_order": self.bit_order,
            "duplicate_tag_names": list(self.duplicate_tag_names),
            "duplicate_paths": list(self.duplicate_paths),
            "entries": [entry.as_dict() for entry in self.entries],
        }


def bitmap_length(entry_count: int) -> int:
    """Return ceil(entry_count / 8) without allocating from the count."""
    if not isinstance(entry_count, int) or entry_count < 0:
        raise ManifestParseError("entry_count must be a non-negative integer")
    return (entry_count + 7) // 8


def _read_cstring(data: bytes, offset: int, end: int, field: str) -> tuple[str, int]:
    if offset < 0 or offset >= end:
        raise ManifestParseError(f"missing {field} at byte offset {offset}")
    search_end = min(end, offset + MAX_STRING_BYTES + 1)
    terminator = data.find(b"\x00", offset, search_end)
    if terminator < 0:
        if search_end < end:
            raise ManifestParseError(f"{field} exceeds {MAX_STRING_BYTES} bytes")
        raise ManifestParseError(f"unterminated {field}")
    if terminator == offset:
        raise ManifestParseError(f"empty {field}")
    raw = data[offset:terminator]
    try:
        value = raw.decode("utf-8", errors="strict")
    except UnicodeDecodeError as exc:
        raise ManifestParseError(f"{field} is not valid UTF-8") from exc
    return value, terminator + 1


def _bit_is_set(bitmap: bytes, entry_index: int, bit_order: str) -> bool:
    byte = bitmap[entry_index // 8]
    bit = entry_index % 8
    mask = (0x80 >> bit) if bit_order == "msb" else (1 << bit)
    return bool(byte & mask)


def _validate_unused_bitmap_bits(bitmap: bytes, entry_count: int, bit_order: str, tag_name: str) -> None:
    remainder = entry_count % 8
    if remainder == 0 or not bitmap:
        return
    valid_mask = ((0xFF << (8 - remainder)) & 0xFF) if bit_order == "msb" else ((1 << remainder) - 1)
    if bitmap[-1] & (~valid_mask & 0xFF):
        raise ManifestParseError(f"tag {tag_name!r} has set bits beyond entry_count")


def parse_install_manifest(data: bytes | bytearray | memoryview, *, bit_order: str) -> ParsedManifest:
    """Parse one standalone INSTALL v1 byte buffer.

    `bit_order` is deliberately required. Accepted values are `msb` and `lsb`.
    The local CascLib source confirms bitmap sizing but does not establish the
    INSTALL-specific bit ordering. File-size interpretation as big-endian u32
    follows the schema requested for this prototype; local CascLib skips this
    field without decoding it, so that semantic is not independently verified.
    """
    if bit_order not in BIT_ORDERS:
        raise ManifestParseError("bit_order must be explicitly set to 'msb' or 'lsb'")
    if not isinstance(data, (bytes, bytearray, memoryview)):
        raise TypeError("data must be a byte buffer")
    if len(data) > MAX_MANIFEST_BYTES:
        raise ManifestParseError(f"manifest exceeds {MAX_MANIFEST_BYTES} bytes")
    view = memoryview(data)
    raw = view.tobytes()
    end = len(raw)
    if end < HEADER_SIZE:
        raise ManifestParseError(f"truncated header: need {HEADER_SIZE} bytes")
    if raw[:2] != INSTALL_MAGIC:
        raise ManifestParseError(f"invalid INSTALL magic: expected {INSTALL_MAGIC!r}")
    version = raw[2]
    if version != SUPPORTED_VERSION:
        raise ManifestParseError(f"unsupported INSTALL version {version}")
    hash_size = raw[3]
    if hash_size != EXPECTED_HASH_SIZE:
        raise ManifestParseError(f"unsupported hash size {hash_size}; expected {EXPECTED_HASH_SIZE}")
    tag_count = int.from_bytes(raw[4:6], "big")
    entry_count = int.from_bytes(raw[6:10], "big")
    if tag_count > MAX_TAGS:
        raise ManifestParseError(f"tag_count {tag_count} exceeds limit {MAX_TAGS}")
    if entry_count > MAX_ENTRIES:
        raise ManifestParseError(f"entry_count {entry_count} exceeds limit {MAX_ENTRIES}")
    expected_bitmap_length = bitmap_length(entry_count)
    offset = HEADER_SIZE
    tags: list[Tag] = []

    for tag_index in range(tag_count):
        name, offset = _read_cstring(raw, offset, end, f"tag name #{tag_index}")
        if end - offset < 2:
            raise ManifestParseError(f"truncated tag type for {name!r}")
        tag_type = int.from_bytes(raw[offset:offset + 2], "big")
        offset += 2
        if end - offset < expected_bitmap_length:
            raise ManifestParseError(f"truncated bitmap for tag {name!r}")
        bitmap = raw[offset:offset + expected_bitmap_length]
        offset += expected_bitmap_length
        _validate_unused_bitmap_bits(bitmap, entry_count, bit_order, name)
        tags.append(Tag(name=name, tag_type=tag_type, bitmap=bitmap))

    tag_counts = Counter(tag.name for tag in tags)
    duplicate_tag_names = tuple(sorted(name for name, count in tag_counts.items() if count > 1))
    entries: list[Entry] = []
    paths: list[str] = []

    for entry_index in range(entry_count):
        path, offset = _read_cstring(raw, offset, end, f"file path #{entry_index}")
        if end - offset < hash_size:
            raise ManifestParseError(f"incomplete CKey for entry #{entry_index} ({path!r})")
        ckey = raw[offset:offset + hash_size].hex()
        offset += hash_size
        if end - offset < 4:
            raise ManifestParseError(f"incomplete 4-byte size field for entry #{entry_index} ({path!r})")
        size = int.from_bytes(raw[offset:offset + 4], "big")
        offset += 4
        membership = tuple(
            {"name": tag.name, "type": tag.tag_type}
            for tag in tags
            if _bit_is_set(tag.bitmap, entry_index, bit_order)
        )
        entries.append(Entry(entry_index, path, ckey, size, membership))
        paths.append(path)

    if offset != end:
        raise ManifestParseError(f"unexpected trailing bytes: {end - offset}")
    path_counts = Counter(paths)
    duplicate_paths = tuple(sorted(path for path, count in path_counts.items() if count > 1))
    return ParsedManifest(
        version=version,
        hash_size=hash_size,
        tags=tuple(tags),
        entries=tuple(entries),
        bit_order=bit_order,
        duplicate_tag_names=duplicate_tag_names,
        duplicate_paths=duplicate_paths,
    )


def entries_for_arabic(manifest: ParsedManifest, tag_name: str | None = None) -> list[dict[str, Any]]:
    """Return entries matching arSA text, arSA speech, or one requested tag.

    Duplicate definitions of a requested tag are ambiguous and fail closed.
    Unknown tag names remain in entry results but are not classified as Arabic.
    """
    requested = (tag_name,) if tag_name is not None else ARABIC_TAGS
    if any(name not in ARABIC_TAGS for name in requested):
        raise ManifestParseError("Arabic filter must be 'arSA text' or 'arSA speech'")
    duplicate_requested = set(requested).intersection(manifest.duplicate_tag_names)
    if duplicate_requested:
        names = ", ".join(sorted(duplicate_requested))
        raise ManifestParseError(f"ambiguous duplicate Arabic tag definition(s): {names}")
    return [
        entry.as_dict()
        for entry in manifest.entries
        if any(tag["name"] in requested for tag in entry.tags)
    ]
