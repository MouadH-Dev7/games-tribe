"""Read-only, bounded Phase 12D INSTALL manifest inspector.

Uses only Python's standard library. All game-file opens are binary read-only;
all outputs are confined to this script's sibling ``out`` directory. This was
written for manual execution by the user and has not been executed here.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import importlib.util
import re
import struct
import sys
import zlib
from pathlib import Path

MAX_TEXT = 4 * 1024 * 1024
MAX_INDEX_FILE = 256 * 1024 * 1024
EXPECTED_INDEX_SIZE = 65536
LOCAL_DATA_HEADER_SIZE = 30
MAX_BLOB = 4 * 1024 * 1024
MAX_DECODED = 64 * 1024 * 1024
MAX_CHUNKS = 4096
MAX_TAIL_SCAN = 64
EXPECTED_PRODUCT = "lazr"
EXPECTED_VERSION = "1.1.2.1279292"
EXPECTED_BUILD_NAME = "CRC_1279292A_retail_signed_ship"
EXPECTED_BUILD_KEY = "ebd38497ab7dd9895822a094a31c0457"
EXPECTED_CDN_KEY = "29dcfc9413388701c4144c2e75aee912"
EXPECTED_INSTALL_EKEY = "9f86d96c2ae820eb5164d6e12f95ebf6"
EXPECTED_INSTALL_CKEY = "3a64e6f6c3d81ac35f9bf6d7c86e6a00"
EXPECTED_DECODED_SIZE = 663
EXPECTED_ENCODED_SIZE = 670


class StopPhase(Exception):
    pass


def status(message: str) -> None:
    print(message, flush=True)


def read_game(path: Path, offset: int, length: int, log_path: Path) -> bytes:
    if offset < 0 or length < 0 or length > MAX_INDEX_FILE:
        raise StopPhase(f"Refusing out-of-bounds read request: {path} offset={offset} length={length}")
    with path.open("rb") as stream:
        stream.seek(offset)
        data = stream.read(length)
    digest = hashlib.sha256(data).hexdigest().upper()
    with log_path.open("a", encoding="utf-8", newline="\n") as log:
        log.write(f"{path}\t{offset}\t{len(data)}\t{digest}\n")
    if len(data) != length:
        raise StopPhase(f"Short read: {path} requested={length} got={len(data)}; partial read logged")
    return data


def parse_build_info(data: bytes) -> tuple[dict[str, str], list[str]]:
    text = data.decode("utf-8-sig", "strict")
    rows = list(csv.reader(text.splitlines(), delimiter="|"))
    if len(rows) < 2:
        raise StopPhase(".build.info has no data rows")
    header = [re.sub(r"!.*$", "", cell.strip()) for cell in rows[0]]
    row = next((r for r in rows[1:] if len(r) == len(header) and r[header.index("Active")].strip() == "1"), None)
    if row is None:
        raise StopPhase("No active .build.info row found")
    values = dict(zip(header, (x.strip() for x in row)))
    required = ("Branch", "Build Key", "CDN Key", "Install Key", "Version", "Tags", "Active", "CDN Path")
    missing = [key for key in required if key not in values]
    if missing:
        raise StopPhase(f".build.info missing required column(s): {', '.join(missing)}")
    # TACT Tags is a conditional expression (e.g. "EU? acct-RUS? ... arSA text?")
    # rather than a comma-separated list. Capture locale/resource alternatives
    # without claiming the conditions are active for the current installation.
    tags = list(dict.fromkeys(
        f"{locale} {kind}"
        for locale, kind in re.findall(r"\b([a-z]{2}[A-Z]{2})\s+(speech|text)\??", values["Tags"])
    ))
    return values, tags


def parse_config(data: bytes) -> tuple[str, str, int, int, str, str]:
    text = data.decode("utf-8-sig", "strict")
    found: dict[str, str] = {}
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = (part.strip() for part in line.split("=", 1))
        if key in {"install", "install-size", "build-name", "build-product"}:
            found[key] = value
    try:
        ckey, ekey = found["install"].split()
        decoded_text, encoded_text = found["install-size"].split()
        decoded, encoded = int(decoded_text), int(encoded_text)
        build_name, product = found["build-name"], found["build-product"]
    except (KeyError, ValueError) as exc:
        raise StopPhase(f"Build config has missing or malformed install fields: {exc}") from exc
    for label, key in (("CKey", ckey), ("EKey", ekey)):
        if not re.fullmatch(r"[0-9a-fA-F]{32}", key):
            raise StopPhase(f"Build config {label} is not a 16-byte hex key")
    if min(decoded, encoded) <= 0 or max(decoded, encoded) > MAX_BLOB:
        raise StopPhase(f"Unexpected install-size values: decoded={decoded}, encoded={encoded}")
    return ckey.lower(), ekey.lower(), decoded, encoded, build_name, product


# Jenkins lookup3 hashlittle implementation used by CascLib guarded records.
def _rot(x: int, k: int) -> int:
    return ((x << k) & 0xFFFFFFFF) | (x >> (32 - k))


def _mix(a: int, b: int, c: int) -> tuple[int, int, int]:
    a = (a - c) & 0xFFFFFFFF; a ^= _rot(c, 4); c = (c + b) & 0xFFFFFFFF
    b = (b - a) & 0xFFFFFFFF; b ^= _rot(a, 6); a = (a + c) & 0xFFFFFFFF
    c = (c - b) & 0xFFFFFFFF; c ^= _rot(b, 8); b = (b + a) & 0xFFFFFFFF
    a = (a - c) & 0xFFFFFFFF; a ^= _rot(c, 16); c = (c + b) & 0xFFFFFFFF
    b = (b - a) & 0xFFFFFFFF; b ^= _rot(a, 19); a = (a + c) & 0xFFFFFFFF
    c = (c - b) & 0xFFFFFFFF; c ^= _rot(b, 4); b = (b + a) & 0xFFFFFFFF
    return a, b, c


def _final(a: int, b: int, c: int) -> tuple[int, int, int]:
    c ^= b; c = (c - _rot(b, 14)) & 0xFFFFFFFF
    a ^= c; a = (a - _rot(c, 11)) & 0xFFFFFFFF
    b ^= a; b = (b - _rot(a, 25)) & 0xFFFFFFFF
    c ^= b; c = (c - _rot(b, 16)) & 0xFFFFFFFF
    a ^= c; a = (a - _rot(c, 4)) & 0xFFFFFFFF
    b ^= a; b = (b - _rot(a, 14)) & 0xFFFFFFFF
    c ^= b; c = (c - _rot(b, 24)) & 0xFFFFFFFF
    return a, b, c


def hashlittle(data: bytes, seed: int = 0) -> int:
    length = len(data)
    a = b = c = (0xDEADBEEF + length + seed) & 0xFFFFFFFF
    offset = 0
    while length > 12:
        a = (a + int.from_bytes(data[offset:offset + 4], "little")) & 0xFFFFFFFF
        b = (b + int.from_bytes(data[offset + 4:offset + 8], "little")) & 0xFFFFFFFF
        c = (c + int.from_bytes(data[offset + 8:offset + 12], "little")) & 0xFFFFFFFF
        a, b, c = _mix(a, b, c)
        offset += 12; length -= 12
    tail = data[offset:]
    if length >= 12: c += tail[11] << 24
    if length >= 11: c += tail[10] << 16
    if length >= 10: c += tail[9] << 8
    if length >= 9: c += tail[8]
    if length >= 8: b += tail[7] << 24
    if length >= 7: b += tail[6] << 16
    if length >= 6: b += tail[5] << 8
    if length >= 5: b += tail[4]
    if length >= 4: a += tail[3] << 24
    if length >= 3: a += tail[2] << 16
    if length >= 2: a += tail[1] << 8
    if length >= 1: a += tail[0]
    if length == 0:
        return c & 0xFFFFFFFF
    return _final(a & 0xFFFFFFFF, b & 0xFFFFFFFF, c & 0xFFFFFFFF)[2]


def hashlittle2_chain(entries: bytes, entry_length: int) -> tuple[int, int]:
    # CascLib calls hashlittle2 successively with the preceding (pc,pb) seeds.
    pc = pb = 0
    for at in range(0, len(entries), entry_length):
        block = entries[at:at + entry_length]
        if len(block) != entry_length:
            raise StopPhase("Guarded index block is not an integral number of entries")
        # lookup3 hashlittle2's seed convention: pc initializes c; pb initializes b.
        length = len(block)
        a = b = c = (0xDEADBEEF + length) & 0xFFFFFFFF
        c = (c + pc) & 0xFFFFFFFF
        b = (b + pb) & 0xFFFFFFFF
        off = 0; rem = length
        while rem > 12:
            a = (a + int.from_bytes(block[off:off+4], "little")) & 0xFFFFFFFF
            b = (b + int.from_bytes(block[off+4:off+8], "little")) & 0xFFFFFFFF
            c = (c + int.from_bytes(block[off+8:off+12], "little")) & 0xFFFFFFFF
            a, b, c = _mix(a, b, c); off += 12; rem -= 12
        tail = block[off:]
        if rem >= 12: c += tail[11] << 24
        if rem >= 11: c += tail[10] << 16
        if rem >= 10: c += tail[9] << 8
        if rem >= 9: c += tail[8]
        if rem >= 8: b += tail[7] << 24
        if rem >= 7: b += tail[6] << 16
        if rem >= 6: b += tail[5] << 8
        if rem >= 5: b += tail[4]
        if rem >= 4: a += tail[3] << 24
        if rem >= 3: a += tail[2] << 16
        if rem >= 2: a += tail[1] << 8
        if rem >= 1: a += tail[0]
        if rem:
            pc, pb = _final(a & 0xFFFFFFFF, b & 0xFFFFFFFF, c & 0xFFFFFFFF)[2], _final(a & 0xFFFFFFFF, b & 0xFFFFFFFF, c & 0xFFFFFFFF)[1]
        else:
            pc, pb = c & 0xFFFFFFFF, b & 0xFFFFFFFF
    return pc, pb


def hashlittle_chain(entries: bytes, entry_length: int) -> int:
    value = 0
    for at in range(0, len(entries), entry_length):
        block = entries[at:at + entry_length]
        if len(block) != entry_length:
            raise StopPhase("Guarded index block is not an integral number of entries")
        value = hashlittle(block, value)
    return value


def _idx_structure_error(path: Path, data: bytes, block_offset: int, reason: str) -> StopPhase:
    return StopPhase(
        f"Index entry-block structural check failed: {path}; reason={reason}; "
        f"idx_first128={data[:128].hex(' ')}; "
        f"entry_block_at_{block_offset}_first64={data[block_offset:block_offset+64].hex(' ')}"
    )


def parse_idx(path: Path, data: bytes, ekey_prefix: bytes, expected_bucket: int, hash_warning_state: dict[str, bool]) -> list[dict[str, int | str]]:
    if len(data) < 40:
        raise StopPhase(f"Index too short for V2 structures: {path}")
    guard_size = int.from_bytes(data[0:4], "little")
    guard_hash = int.from_bytes(data[4:8], "little")
    if guard_size != 16 or 8 + guard_size > len(data):
        raise StopPhase(f"Unexpected index header guarded block size in {path}: {guard_size}")
    header = data[8:24]
    computed_header_hash = hashlittle(header)
    if computed_header_hash != guard_hash:
        raise StopPhase(f"Index header Jenkins hash mismatch: {path}; expected=0x{guard_hash:08x}, computed=0x{computed_header_hash:08x}; first32={data[:32].hex(' ')}")
    revision = int.from_bytes(header[0:2], "little")
    bucket, flags, size_width, offset_width, key_width, segment_bits = header[2:8]
    max_file_offset = int.from_bytes(header[8:16], "little")
    name_bucket = int(path.name[:2], 16)
    if revision != 7 or bucket != expected_bucket or bucket != name_bucket or flags != 0:
        raise StopPhase(f"Unsupported/mismatched index header {path}: revision={revision}, bucket={bucket}, filename_bucket={name_bucket}, flags={flags}")
    if (key_width, offset_width, size_width) != (9, 5, 4):
        raise StopPhase(f"Unsupported .idx widths in {path}: key={key_width}, offset={offset_width}, size={size_width}")
    if segment_bits != 30 or max_file_offset <= 0:
        raise StopPhase(f"Invalid offset packing metadata in {path}: segment_bits={segment_bits}, max_file_offset={max_file_offset}")
    entry_len = key_width + offset_width + size_width
    # CascLib V2: first record group is an 8-byte guarded-block prefix followed
    # by contiguous entries. Later page form uses per-entry guards.
    at = 32
    matches: list[dict[str, int | str]] = []
    if at + 8 > len(data):
        raise _idx_structure_error(path, data, at, "missing guarded entry-block header")
    block_len = int.from_bytes(data[at:at+4], "little")
    block_hash = int.from_bytes(data[at+4:at+8], "little")
    end = at + 8 + block_len
    if block_len <= 0:
        raise _idx_structure_error(path, data, at, f"non-positive block size {block_len}")
    if block_len % entry_len != 0:
        raise _idx_structure_error(path, data, at, f"block size {block_len} is not divisible by validated entry width {entry_len}")
    if end > len(data):
        raise _idx_structure_error(path, data, at, f"entry area ends at {end}, beyond file length {len(data)}")
    block = data[at+8:end]
    previous_key: bytes | None = None
    for pos in range(0, len(block), entry_len):
        key = block[pos:pos+key_width]
        if len(key) != key_width:
            raise _idx_structure_error(path, data, at, f"truncated key at entry offset {pos}")
        if previous_key is not None and key < previous_key:
            raise _idx_structure_error(path, data, at, f"EKey order decreases at entry {pos // entry_len}: {previous_key.hex()} > {key.hex()}")
        previous_key = key
    pc, pb = hashlittle2_chain(block, entry_len)
    blizzget_hash = hashlittle_chain(block, entry_len)
    hash_mismatch = pc != block_hash and blizzget_hash != block_hash
    if hash_mismatch and not hash_warning_state["detail_printed"]:
        status(f"WARNING: guarded entry-block Jenkins mismatch in {path.name}; expected=0x{block_hash:08x}, computed_hashlittle2=0x{pc:08x}, computed_hashlittle_chain=0x{blizzget_hash:08x}; first32={data[at:at+32].hex(' ')}; structural checks passed (size={block_len}, entries={block_len // entry_len}, entry_width={entry_len}, keys non-decreasing)")
        hash_warning_state["detail_printed"] = True
    else:
        status(f"Guarded entry-block hash: {path.name}; mismatch={'yes' if hash_mismatch else 'no'}")
    matches.extend(_match_entries(block, entry_len, ekey_prefix, segment_bits, path))
    status(f"Validated {path.name}: rev={revision}, bucket={bucket:02x}, widths={key_width}/{offset_width}/{size_width}, segment_bits={segment_bits}, matches={len(matches)}")
    return matches


def _match_entries(block: bytes, entry_len: int, prefix: bytes, segment_bits: int, path: Path) -> list[dict[str, int | str]]:
    result = []
    for pos in range(0, len(block), entry_len):
        rec = block[pos:pos+entry_len]
        if rec[:9] != prefix:
            continue
        packed = int.from_bytes(rec[9:14], "big")
        archive = packed >> segment_bits
        offset = packed & ((1 << segment_bits) - 1)
        size = int.from_bytes(rec[14:18], "little")
        result.append({"idx": str(path), "archive": archive, "offset": offset, "size": size})
    return result


def validate_local_data_header(region: bytes, install_ekey: str, index_size: int, config_encoded_size: int) -> bytes:
    """Validate CascLib's 30-byte local header and return the BLTE stream."""
    header_len = LOCAL_DATA_HEADER_SIZE
    if len(region) != index_size or index_size != config_encoded_size + header_len:
        raise StopPhase(f"Local data region length mismatch; index_size={index_size}, region_bytes={len(region)}, expected=config_encoded_size+30={config_encoded_size + header_len}; first64={region[:64].hex(' ')}")
    expected_key = bytes.fromhex(install_ekey)
    stored_key = region[:16]
    matches_forward = stored_key == expected_key
    matches_reversed = stored_key == expected_key[::-1]
    if matches_forward and matches_reversed:
        key_order = "both (identical byte sequence)"
    elif matches_forward:
        key_order = "forward"
    elif matches_reversed:
        key_order = "byte-reversed"
    else:
        raise StopPhase(f"Local data header EKey matches neither forward nor byte-reversed install EKey; stored={stored_key.hex()}, expected={expected_key.hex()}, reversed_expected={expected_key[::-1].hex()}; first64={region[:64].hex(' ')}")
    status(f"Local header EKey: matched {key_order} byte order")
    header_encoded_size = int.from_bytes(region[16:20], "little")
    expected_full_size = config_encoded_size + LOCAL_DATA_HEADER_SIZE
    status(f"Local header EncodedSize: {header_encoded_size}; expected full indexed region={index_size}, config encoded size + local header={expected_full_size}")
    status(f"Local header auxiliary fields (not validated): bytes20-21={region[20:22].hex()}, Jenkins[22-25]={region[22:26].hex()}, checksum[26-29]={region[26:30].hex()}")
    if header_encoded_size != index_size or header_encoded_size != expected_full_size:
        raise StopPhase(f"Local header EncodedSize mismatch; field={header_encoded_size}, expected index_size={index_size} and config_encoded_size+30={expected_full_size}; first64={region[:64].hex(' ')}")
    if region[header_len:header_len+4] != b"BLTE":
        raise StopPhase(f"Local header did not lead to BLTE at offset {header_len}; found={region[header_len:header_len+4].hex(' ')}; first64={region[:64].hex(' ')}")
    status(f"Local header validation passed: 30 bytes; BLTE magic at offset {header_len}")
    return region[header_len:]


def validate_blte_layout(stream: bytes) -> None:
    """Validate the expected two-chunk INSTALL BLTE envelope before decoding."""
    if len(stream) != EXPECTED_ENCODED_SIZE:
        raise StopPhase(f"BLTE stream length mismatch: got={len(stream)}, expected={EXPECTED_ENCODED_SIZE}")
    if len(stream) < 12 or stream[:4] != b"BLTE":
        raise StopPhase(f"BLTE stream prefix invalid/truncated: first64={stream[:64].hex(' ')}")
    header_size = int.from_bytes(stream[4:8], "big")
    if stream[8] != 0x0F:
        raise StopPhase(f"BLTE chunk-table marker mismatch: got=0x{stream[8]:02x}, expected=0x0f; first64={stream[:64].hex(' ')}")
    chunk_count = int.from_bytes(stream[9:12], "big")
    expected_header_size = 12 + 24 * chunk_count
    if header_size != 60 or chunk_count != 2 or header_size != expected_header_size:
        raise StopPhase(f"BLTE header layout mismatch: header_size={header_size}, chunk_count={chunk_count}, 12+24*count={expected_header_size}; first64={stream[:64].hex(' ')}")
    if header_size > len(stream):
        raise StopPhase(f"BLTE chunk table exceeds stream: header_size={header_size}, stream_length={len(stream)}; first64={stream[:64].hex(' ')}")
    encoded_total = 0
    pos = 12
    chunks: list[tuple[int, int, int]] = []
    for i in range(chunk_count):
        encoded_size = int.from_bytes(stream[pos:pos+4], "big")
        decoded_size = int.from_bytes(stream[pos+4:pos+8], "big")
        if encoded_size <= 0 or decoded_size <= 0 or encoded_size > MAX_BLOB or decoded_size > MAX_DECODED:
            raise StopPhase(f"BLTE chunk #{i} size outside limits: encoded={encoded_size}, decoded={decoded_size}; first64={stream[:64].hex(' ')}")
        chunks.append((encoded_size, decoded_size, pos))
        encoded_total += encoded_size
        pos += 24
    if header_size + encoded_total != len(stream):
        raise StopPhase(f"BLTE encoded lengths do not fill stream: header={header_size}, sum_encoded={encoded_total}, stream={len(stream)}; first64={stream[:64].hex(' ')}")
    payload_pos = header_size
    for i, (encoded_size, decoded_size, _) in enumerate(chunks):
        mode = stream[payload_pos:payload_pos+1]
        status(f"BLTE chunk #{i}: encoded={encoded_size}, decoded={decoded_size}, mode={mode!r}")
        if mode not in (b"N", b"Z"):
            raise StopPhase(f"Unsupported BLTE mode {mode!r} in chunk #{i}; first64={stream[:64].hex(' ')}")
        payload_pos += encoded_size


def _parse_page_entries(data: bytes, prefix: bytes, segment_bits: int, path: Path, entry_len: int) -> list[dict[str, int | str]]:
    result = []
    page_size = 0x200
    aligned = (entry_len + 3) & ~3
    for page_start in range(0x1000, len(data), page_size):
        page_end = min(page_start + page_size, len(data))
        pos = page_start
        while pos + 4 + entry_len + 1 <= page_end:
            stored_hash = int.from_bytes(data[pos:pos+4], "little")
            if stored_hash == 0:
                break
            rec = data[pos+4:pos+4+entry_len]
            computed_hash = hashlittle(data[pos+4:pos+4+entry_len+1]) | 0x80000000
            if computed_hash != stored_hash:
                raise StopPhase(f"Per-entry guarded Jenkins hash mismatch at {path} offset {pos}; expected=0x{stored_hash:08x}, computed=0x{computed_hash:08x}; first32={data[pos:pos+32].hex(' ')}")
            if rec[:9] == prefix:
                packed = int.from_bytes(rec[9:14], "big")
                result.append({"idx": str(path), "archive": packed >> segment_bits, "offset": packed & ((1 << segment_bits)-1), "size": int.from_bytes(rec[14:18], "little")})
            pos += 4 + aligned
    return result


def decode_blte(region: bytes) -> bytes:
    candidates = [m.start() for m in re.finditer(b"BLTE", region[:MAX_TAIL_SCAN])]
    if not candidates:
        raise StopPhase(f"BLTE magic not found in first {MAX_TAIL_SCAN} bytes of the selected data region")
    if len(candidates) != 1:
        raise StopPhase(f"Ambiguous BLTE magic offsets in selected region: {candidates}")
    start = candidates[0]
    stream = region[start:]
    if len(stream) < 9:
        raise StopPhase("Truncated BLTE stream header")
    header_size = int.from_bytes(stream[4:8], "big")
    chunks: list[tuple[int, int, bytes]] = []
    if header_size == 0:
        payload = stream[8:]
        if not payload:
            raise StopPhase("Empty single-chunk BLTE payload")
        chunks.append((len(payload), -1, b""))
        payload_start = 8
    else:
        if stream[8] != 0x0F or header_size < 12 or header_size > len(stream):
            raise StopPhase(f"Invalid BLTE chunk-table header: header_size={header_size}, flag={stream[8]:02x}")
        count = int.from_bytes(stream[9:12], "big")
        if not 1 <= count <= MAX_CHUNKS or header_size != 12 + count * 24:
            raise StopPhase(f"Unexpected BLTE chunk table: count={count}, header_size={header_size}")
        pos = 12
        for _ in range(count):
            enc = int.from_bytes(stream[pos:pos+4], "big")
            dec = int.from_bytes(stream[pos+4:pos+8], "big")
            md5 = stream[pos+8:pos+24]
            if enc <= 0 or dec <= 0 or enc > MAX_BLOB or dec > MAX_DECODED:
                raise StopPhase(f"BLTE chunk size exceeds limits: encoded={enc}, decoded={dec}")
            chunks.append((enc, dec, md5)); pos += 24
        payload_start = header_size
    if payload_start > len(stream):
        raise StopPhase("BLTE header extends beyond encoded region")
    output = bytearray()
    pos = payload_start
    for i, (enc_size, expected_dec, expected_md5) in enumerate(chunks):
        end = pos + enc_size
        if end > len(stream):
            raise StopPhase(f"BLTE chunk #{i} exceeds selected encoded region")
        encoded = stream[pos:end]
        if expected_md5 and hashlib.md5(encoded).digest() != expected_md5:
            raise StopPhase(f"BLTE chunk #{i} MD5 mismatch")
        if not encoded:
            raise StopPhase(f"BLTE chunk #{i} is empty")
        mode = encoded[0:1]
        body = encoded[1:]
        limit = expected_dec if expected_dec >= 0 else MAX_DECODED - len(output)
        if mode == b"N":
            decoded = body
        elif mode == b"Z":
            inflater = zlib.decompressobj()
            decoded = inflater.decompress(body, limit + 1)
            if len(decoded) > limit or inflater.unconsumed_tail or not inflater.eof or inflater.unused_data:
                raise StopPhase(f"BLTE chunk #{i} zlib output/stream exceeds declared bounds or is incomplete")
        else:
            raise StopPhase(f"Unsupported BLTE mode {mode!r} in chunk #{i}; only N and Z are allowed")
        if expected_dec >= 0 and len(decoded) != expected_dec:
            raise StopPhase(f"BLTE chunk #{i} decoded length mismatch: expected={expected_dec}, got={len(decoded)}")
        if len(output) + len(decoded) > MAX_DECODED:
            raise StopPhase("Decoded BLTE data exceeds global output limit")
        output.extend(decoded); pos = end
    if pos != len(stream):
        raise StopPhase(f"Unexpected trailing bytes after BLTE chunks: {len(stream)-pos}")
    return bytes(output)


def write_csv(path: Path, rows: list[dict[str, object]], fields: list[str]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=fields, extrasaction="ignore")
        writer.writeheader(); writer.writerows(rows)


def tail_bit_status(data: bytes, bit_order: str) -> tuple[bool, list[str]]:
    """Check unused final-byte bits in each tag bitmap without parsing entries."""
    tag_count = int.from_bytes(data[4:6], "big")
    entry_count = int.from_bytes(data[6:10], "big")
    bitmap_size = (entry_count + 7) // 8
    at = 10
    bad: list[str] = []
    for i in range(tag_count):
        end = data.find(b"\x00", at, min(len(data), at + 4097))
        if end < 0:
            raise StopPhase(f"Cannot identify tag name #{i} while checking bitmap tail bits")
        name = data[at:end].decode("utf-8", "strict")
        at = end + 1
        if at + 2 + bitmap_size > len(data):
            raise StopPhase(f"Truncated tag record #{i} while checking bitmap tail bits")
        at += 2
        bitmap = data[at:at + bitmap_size]
        at += bitmap_size
        rem = entry_count % 8
        if rem and bitmap:
            valid_mask = ((0xFF << (8 - rem)) & 0xFF) if bit_order == "msb" else ((1 << rem) - 1)
            if bitmap[-1] & (~valid_mask & 0xFF):
                bad.append(name)
    return not bad, bad


def main() -> int:
    cli = argparse.ArgumentParser(description="Read-only bounded INSTALL manifest inspection")
    cli.add_argument("game_dir", type=Path, help="MW2 Campaign Remastered installation directory")
    args = cli.parse_args()
    game_dir = args.game_dir.resolve(strict=True)
    if not game_dir.is_dir():
        raise StopPhase("Game path is not a directory")
    script_dir = Path(__file__).resolve().parent
    output_dir = script_dir / "out"
    output_dir.mkdir(parents=True, exist_ok=True)
    log_path = output_dir / "run.log"
    log_path.write_text("Game file\tOffset\tLength\tSHA-256(bytes read)\n", encoding="utf-8", newline="\n")

    build_path = game_dir / ".build.info"
    build_bytes = read_game(build_path, 0, min(build_path.stat().st_size, MAX_TEXT), log_path)
    values, tags = parse_build_info(build_bytes)
    status("[1/8] Active build row: " + ", ".join(f"{k}={values[k]}" for k in ("Branch", "Build Key", "CDN Key", "Install Key", "Version", "Active")) + f"; locale/resource alternatives in Tags={'; '.join(tags)}")
    if values["Build Key"].lower() != EXPECTED_BUILD_KEY or values["CDN Key"].lower() != EXPECTED_CDN_KEY or values["Install Key"].lower() != EXPECTED_INSTALL_EKEY or values["Version"] != EXPECTED_VERSION:
        raise StopPhase("Active .build.info identity differs from confirmed target; stop without index access")
    raw_tags = values["Tags"]
    for locale_tag in ("arSA text", "arSA speech"):
        status(f".build.info substring {locale_tag!r}: {'PRESENT' if locale_tag in raw_tags else 'ABSENT'} (conditional metadata only)")

    config_path = game_dir / "Data" / "config" / values["Build Key"][:2] / values["Build Key"][2:4] / values["Build Key"]
    if not config_path.is_file():
        raise StopPhase(f"Active build config not found at expected key path: {config_path}")
    config_bytes = read_game(config_path, 0, min(config_path.stat().st_size, MAX_TEXT), log_path)
    ckey, ekey, decoded_size, encoded_size, build_name, product = parse_config(config_bytes)
    status(f"[2/8] INSTALL config: CKey={ckey}, EKey={ekey}, decoded={decoded_size}, encoded={encoded_size}, build={build_name}, product={product}")
    if (ekey != values["Install Key"].lower() or ekey != EXPECTED_INSTALL_EKEY or ckey != EXPECTED_INSTALL_CKEY or decoded_size != EXPECTED_DECODED_SIZE or encoded_size != EXPECTED_ENCODED_SIZE or build_name != EXPECTED_BUILD_NAME or product != EXPECTED_PRODUCT):
        raise StopPhase("Build config INSTALL identity/size differs from prior verified values")

    index_dir = game_dir / "Data" / "data"
    if not index_dir.is_dir():
        raise StopPhase(f"CASC index directory not found: {index_dir}")
    candidates = [p for p in index_dir.iterdir() if p.is_file() and re.fullmatch(r"[0-9a-fA-F]{10}\.idx", p.name)]
    if not candidates:
        raise StopPhase(f"No V2 index files found in {index_dir}")
    status(f"[3/8] .idx inventory in {index_dir} (name; bytes):")
    for candidate in sorted(candidates, key=lambda item: item.name.lower()):
        status(f"  {candidate.name}; {candidate.stat().st_size}")
    latest: dict[int, tuple[int, list[Path]]] = {}
    for p in candidates:
        bucket, version = int(p.name[:2], 16), int(p.name[2:10], 16)
        if bucket >= 16:
            raise StopPhase(f"Index filename bucket is outside CascLib's 00..0f range: {p.name}")
        if bucket not in latest or version > latest[bucket][0]:
            latest[bucket] = version, [p]
        elif version == latest[bucket][0]:
            latest[bucket][1].append(p)
    if not latest:
        raise StopPhase("No index files with CascLib V2 bucket numbers 00..0f")
    prefix = bytes.fromhex(ekey)[:9]
    status(f"[4/8] Searching EKey prefix {prefix.hex()} across newest available .idx version(s) in all 16 bucket slots")
    matches: list[dict[str, int | str]] = []
    hash_warning_state = {"detail_printed": False}
    for bucket in range(16):
        if bucket not in latest:
            status(f"  Bucket {bucket:02x}: no .idx file present")
            continue
        version, bucket_paths = latest[bucket]
        if len(bucket_paths) != 1:
            raise StopPhase(f"Multiple equally-new .idx files for bucket {bucket:02x}, version={version:08x}: {[str(p) for p in bucket_paths]}")
        selected_idx = bucket_paths[0]
        length = selected_idx.stat().st_size
        if length != EXPECTED_INDEX_SIZE:
            raise StopPhase(f"Index file size differs from expected bounded 64 KiB: {selected_idx.name} size={length}, expected={EXPECTED_INDEX_SIZE}")
        if length <= 0 or length > MAX_INDEX_FILE:
            raise StopPhase(f"Index size exceeds safety cap: {selected_idx} size={length}")
        status(f"  Reading newest bucket {bucket:02x} index {selected_idx.name}, version={version:08x}, bytes={length}")
        raw = read_game(selected_idx, 0, length, log_path)
        matches.extend(parse_idx(selected_idx, raw, prefix, bucket, hash_warning_state))
    if len(matches) != 1:
        raise StopPhase(f"EKey lookup requires exactly one match across all available bucket indexes; found {len(matches)}. No shard was opened.")
    found = matches[0]
    expected_index_size = encoded_size + 30
    if found["size"] != expected_index_size:
        raise StopPhase(f"Index encoded size {found['size']} differs from config encoded size plus 30-byte local header: {encoded_size}+30={expected_index_size}")
    if found["size"] <= 0 or found["size"] > MAX_BLOB or found["archive"] > 0xFFF:
        raise StopPhase(f"Index location exceeds safety limits: {found}")
    status(f"[5/8] Unique index match: {found['idx']}; archive={found['archive']:03d}; byte_offset={found['offset']}; index_size={found['size']}")

    shard = index_dir / f"data.{found['archive']:03d}"
    if not shard.is_file():
        raise StopPhase(f"Referenced shard missing: {shard}")
    if found["offset"] + found["size"] > shard.stat().st_size:
        raise StopPhase("INSTALL region extends beyond shard size")
    region = read_game(shard, int(found["offset"]), int(found["size"]), log_path)
    status(f"[6/8] Read exactly one INSTALL region: {len(region)} bytes; shard={shard.name}")
    blte_stream = validate_local_data_header(region, ekey, int(found["size"]), encoded_size)
    validate_blte_layout(blte_stream)
    decoded = decode_blte(blte_stream)
    actual_ckey = hashlib.md5(decoded).hexdigest()
    if actual_ckey != ckey or len(decoded) != decoded_size:
        raise StopPhase(f"Decoded INSTALL mismatch: MD5={actual_ckey} expected={ckey}; length={len(decoded)} expected={decoded_size}")
    manifest_path = output_dir / "install_manifest.bin"
    manifest_path.write_bytes(decoded)
    status(f"[7/8] BLTE decoded: MD5={actual_ckey}; size={len(decoded)}; SHA-256={hashlib.sha256(decoded).hexdigest()}")
    status(f"Manifest first 64 bytes: {decoded[:64].hex(' ')}")
    if len(decoded) < 10:
        raise StopPhase(f"Manifest shorter than fixed 10-byte header: {decoded.hex()}")
    magic, version, hash_size = decoded[:2], decoded[2], decoded[3]
    tag_count, entry_count = int.from_bytes(decoded[4:6], "big"), int.from_bytes(decoded[6:10], "big")
    status(f"Manifest header: magic={magic!r}, version={version}, hash_size={hash_size}, tag_count={tag_count}, entry_count={entry_count}")
    if magic != b"IN" or version != 1:
        raise StopPhase("Not INSTALL v1; saved decoded bytes and printed header, no tag parsing performed")

    parser_path = script_dir / "install_manifest_parser.py"
    spec = importlib.util.spec_from_file_location("install_manifest_parser", parser_path)
    if spec is None or spec.loader is None:
        raise StopPhase(f"Could not load existing parser: {parser_path}")
    parser = importlib.util.module_from_spec(spec); sys.modules[spec.name] = parser; spec.loader.exec_module(parser)
    parsed_by_order = {}
    for order in ("msb", "lsb"):
        tail_ok, tail_bad = tail_bit_status(decoded, order)
        status(f"Unused tail bits {order}: {'VALID' if tail_ok else 'INVALID'}" + (f"; tags with out-of-range bits={tail_bad}" if tail_bad else ""))
        try:
            parsed = parser.parse_install_manifest(decoded, bit_order=order)
        except Exception as exc:
            status(f"[7/8] {order}: INVALID ({type(exc).__name__}: {exc})")
            continue
        parsed_by_order[order] = parsed
        status(f"[7/8] {order}: VALID; tags=" + "; ".join(f"{t.name!r}/type={t.tag_type}/entries={sum(1 for e in parsed.entries if any(x['name']==t.name and x['type']==t.tag_type for x in e.tags))}" for t in parsed.tags))
        for tag in parsed.tags:
            print(f"  tag: name={tag.name!r} type={tag.tag_type}", flush=True)
        rows = [{"index": e.index, "path": e.path, "ckey": e.ckey, "size": e.size, "tags": ";".join(f"{t['name']}[{t['type']}]" for t in e.tags), "bit_order": order} for e in parsed.entries]
        write_csv(output_dir / f"manifest_entries_{order}.csv", rows, ["index", "path", "ckey", "size", "tags", "bit_order"])

    # Tail-bit checks independently examine serialized bitmaps. The existing
    # parser rejects any ordering with set unused bits, providing the same proof.
    passing = list(parsed_by_order)
    exe_suffix = (".exe", ".dll", ".pdb")
    evidence = {}
    for order, parsed in parsed_by_order.items():
        executable = [e for e in parsed.entries if e.path.lower().endswith(exe_suffix)]
        platform_tags = {t.name for t in parsed.tags if any(k in t.name.lower() for k in ("windows", "win32", "win64", "platform", "pc"))}
        assigned = sum(1 for e in executable if any(t["name"] in platform_tags for t in e.tags))
        evidence[order] = (len(executable), assigned, sorted(platform_tags))
        status(f"Platform sanity {order}: executable-like={len(executable)}, assigned-to-platform-named-tag={assigned}, platform-tags={sorted(platform_tags)}")
    if len(passing) == 1:
        selected = passing[0]
        status(f"Bit order consistent with unused-tail-bit validity: {selected}; platform sanity is corroboration only")
    elif len(passing) == 2 and evidence["msb"][1] != evidence["lsb"][1]:
        selected = "msb" if evidence["msb"][1] > evidence["lsb"][1] else "lsb"
        status(f"Bit order evidence favors {selected} by platform sanity only; tail bits are valid in both, so overall result remains INCONCLUSIVE")
    else:
        status("Bit order is INCONCLUSIVE: both orders parse or neither order supplies differentiating platform evidence")

    arabic_rows = []
    for order, parsed in parsed_by_order.items():
        names = {t.name for t in parsed.tags}
        for label in ("arSA text", "arSA speech", "enUS text", "enUS speech"):
            if label in names:
                try:
                    selected_entries = parser.entries_for_arabic(parsed, label) if label.startswith("arSA") else [e.as_dict() for e in parsed.entries if any(t["name"] == label for t in e.tags)]
                except Exception as exc:
                    status(f"Arabic classification ambiguous for {order}/{label}: {exc}"); continue
                if label.startswith("arSA"):
                    arabic_rows.extend({"bit_order": order, "locale_tag": label, "path": e["path"], "size": e["size"], "tags": ";".join(f"{t['name']}[{t['type']}]" for t in e["tags"])} for e in selected_entries)
                status(f"Locale count {order} {label}: {len(selected_entries)}")
    write_csv(output_dir / "arSA_files.csv", arabic_rows, ["bit_order", "locale_tag", "path", "size", "tags"])
    status("[8/8] CSV outputs written under " + str(output_dir))
    status("SUMMARY: read-only diagnostic complete; see this output and out\\run.log. No listed asset payloads were read.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except StopPhase as exc:
        print(f"STOP: {exc}", file=sys.stderr, flush=True)
        raise SystemExit(2)
    except (OSError, UnicodeError, ValueError, struct.error, zlib.error) as exc:
        print(f"STOP: {type(exc).__name__}: {exc}", file=sys.stderr, flush=True)
        raise SystemExit(2)
