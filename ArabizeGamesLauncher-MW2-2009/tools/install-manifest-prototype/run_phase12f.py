"""Read-only, bounded TVFS path inventory for user-controlled local execution.

This tool reads build metadata, V2 index metadata, and exactly one VFS-root
data region. It never resolves or reads referenced game assets.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import importlib.util
import json
import re
import sys
from pathlib import Path

EXPECTED_BRANCH = "eu"
EXPECTED_PRODUCT = "lazr"
EXPECTED_VERSION = "1.1.2.1279292"
EXPECTED_BUILD_NAME = "CRC_1279292A_retail_signed_ship"
EXPECTED_BUILD_KEY = "ebd38497ab7dd9895822a094a31c0457"
EXPECTED_CDN_KEY = "29dcfc9413388701c4144c2e75aee912"
EXPECTED_ROOT_CKEY = "8626c2a5e40d949d2ada146db5c19f11"
EXPECTED_VFS_CKEY = "2ef5b3818d20176f9c191156c1b1a50a"
EXPECTED_VFS_EKEY = "669cd685bcdd0641b325680c28aa6e03"
EXPECTED_VFS_DECODED = 57384
EXPECTED_VFS_ENCODED = 27106
LOCAL_HEADER_SIZE = 30
MAX_METADATA = 256 * 1024 * 1024
MAX_CONFIG = 4 * 1024 * 1024
MAX_VFS_ENCODED = 64 * 1024 * 1024
MAX_VFS_DECODED = 128 * 1024 * 1024
MAX_PATHS = 2_000_000
MAX_DEPTH = 1024
MAX_COMPONENT = 255
MAX_TOTAL_PATH = 32768
TVFS_BASE_HEADER = 38
TVFS_EXTENDED_HEADER = 46
TVFS_SIGNATURE = b"TVFS"


class InventoryError(Exception):
    pass


def say(message: str) -> None:
    print(message, flush=True)


def read_input(path: Path, offset: int, length: int, log: list[dict[str, object]], cap: int) -> bytes:
    if offset < 0 or length < 0 or length > cap:
        raise InventoryError(f"Refusing read outside bound: {path}, offset={offset}, length={length}, cap={cap}")
    with path.open("rb") as stream:
        stream.seek(offset)
        data = stream.read(length)
    log.append({"path": str(path), "offset": offset, "length": len(data), "sha256": hashlib.sha256(data).hexdigest()})
    if len(data) != length:
        raise InventoryError(f"Short read: {path}, offset={offset}, requested={length}, got={len(data)}")
    return data


def parse_build_info(raw: bytes) -> dict[str, str]:
    try:
        rows = list(csv.reader(raw.decode("utf-8-sig", "strict").splitlines(), delimiter="|"))
    except (UnicodeError, csv.Error) as exc:
        raise InventoryError(f"Cannot parse .build.info: {exc}") from exc
    if len(rows) < 2:
        raise InventoryError(".build.info has no data rows")
    headers = [re.sub(r"!.*$", "", h.strip()) for h in rows[0]]
    required = ("Branch", "Build Key", "CDN Key", "Version", "Active")
    if any(name not in headers for name in required):
        raise InventoryError(".build.info is missing required identity columns")
    active = [r for r in rows[1:] if len(r) == len(headers) and r[headers.index("Active")].strip() == "1"]
    if len(active) != 1:
        raise InventoryError(f"Expected exactly one active .build.info row, found {len(active)}")
    result = dict(zip(headers, (v.strip() for v in active[0])))
    checks = {"Branch": EXPECTED_BRANCH, "Build Key": EXPECTED_BUILD_KEY, "CDN Key": EXPECTED_CDN_KEY, "Version": EXPECTED_VERSION}
    differences = [f"{key}={result.get(key)!r}, expected {value!r}" for key, value in checks.items() if result.get(key, "").lower() != value.lower()]
    if differences:
        raise InventoryError("Active build identity differs from expected target: " + "; ".join(differences))
    return result


def parse_build_config(raw: bytes) -> dict[str, tuple[str, ...]]:
    try:
        text = raw.decode("utf-8-sig", "strict")
    except UnicodeError as exc:
        raise InventoryError(f"Build Config is not strict UTF-8: {exc}") from exc
    found: dict[str, tuple[str, ...]] = {}
    for line_no, line in enumerate(text.splitlines(), 1):
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = (piece.strip() for piece in line.split("=", 1))
        if key in {"root", "vfs-root", "vfs-1", "build-name", "build-product"}:
            if key in found:
                raise InventoryError(f"Duplicate {key!r} in Build Config at line {line_no}")
            found[key] = tuple(value.split())
    for key in ("root", "vfs-root", "vfs-1", "build-name", "build-product"):
        if key not in found:
            raise InventoryError(f"Build Config lacks required {key!r} field")
    if len(found["root"]) != 1 or not re.fullmatch(r"[0-9a-fA-F]{32}", found["root"][0]):
        raise InventoryError("Build Config root must contain one 16-byte CKey")
    for key in ("vfs-root", "vfs-1"):
        if len(found[key]) != 2 or any(not re.fullmatch(r"[0-9a-fA-F]{32}", item) for item in found[key]):
            raise InventoryError(f"Build Config {key!r} must contain CKey and EKey")
    if found["build-name"] != (EXPECTED_BUILD_NAME,) or found["build-product"] != (EXPECTED_PRODUCT,):
        raise InventoryError(f"Build Config identity mismatch: build-name={found['build-name']}, build-product={found['build-product']}")
    if found["root"][0].lower() != EXPECTED_ROOT_CKEY:
        raise InventoryError(f"Build Config root CKey mismatch: {found['root'][0]}")
    for field in ("vfs-root", "vfs-1"):
        if tuple(x.lower() for x in found[field]) != (EXPECTED_VFS_CKEY, EXPECTED_VFS_EKEY):
            raise InventoryError(f"Build Config {field} key differs from reference: {found[field]}")
    return found


def parse_sizes(raw: bytes) -> dict[str, tuple[int, int]]:
    text = raw.decode("utf-8-sig", "strict")
    sizes: dict[str, tuple[int, int]] = {}
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = (part.strip() for part in line.split("=", 1))
        if key.endswith("-size") and key[:-5] in {"root", "vfs-root", "vfs-1"}:
            parts = value.split()
            if len(parts) == 2:
                try:
                    sizes[key[:-5]] = (int(parts[0]), int(parts[1]))
                except ValueError as exc:
                    raise InventoryError(f"Invalid {key} values") from exc
    for key in ("vfs-root", "vfs-1"):
        if sizes.get(key) != (EXPECTED_VFS_DECODED, EXPECTED_VFS_ENCODED):
            raise InventoryError(f"Build Config {key}-size differs from reference: {sizes.get(key)}")
    return sizes


def load_phase12d(script_dir: Path):
    # Reuse the already-reviewed bounded V2 index and BLTE routines. Suppress
    # bytecode output so importing the module does not create project files.
    sys.dont_write_bytecode = True
    path = script_dir / "run_phase12d.py"
    spec = importlib.util.spec_from_file_location("phase12d_readonly_helpers", path)
    if spec is None or spec.loader is None:
        raise InventoryError(f"Cannot load reviewed Phase 12D helpers: {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def validate_blte_stream(stream: bytes, expected_size: int) -> None:
    if len(stream) != expected_size or len(stream) < 9 or stream[:4] != b"BLTE":
        raise InventoryError(f"BLTE stream length/signature mismatch: length={len(stream)}, expected={expected_size}")
    header_size = int.from_bytes(stream[4:8], "big")
    if header_size == 0:
        mode = stream[8:9]
        if mode not in (b"N", b"Z") or len(stream) <= 8:
            raise InventoryError(f"Unsupported or empty single-chunk BLTE stream: mode={mode!r}")
        say(f"BLTE single chunk: encoded={len(stream) - 8}, decoded_size=not declared, mode={mode.decode('ascii')}")
        return
    if header_size < 12 or header_size > len(stream) or stream[8] != 0x0F:
        raise InventoryError(f"Invalid BLTE table header: header_size={header_size}, marker={stream[8]:02x}")
    count = int.from_bytes(stream[9:12], "big")
    if count < 1 or count > 4096 or header_size != 12 + 24 * count:
        raise InventoryError(f"Invalid BLTE chunk table dimensions: count={count}, header_size={header_size}")
    encoded_total = 0
    chunks = []
    for i in range(count):
        at = 12 + 24 * i
        encoded = int.from_bytes(stream[at:at + 4], "big")
        decoded = int.from_bytes(stream[at + 4:at + 8], "big")
        if encoded <= 0 or decoded <= 0 or encoded > MAX_VFS_ENCODED or decoded > MAX_VFS_DECODED:
            raise InventoryError(f"BLTE chunk #{i} exceeds size limits: encoded={encoded}, decoded={decoded}")
        chunks.append((encoded, decoded))
        encoded_total += encoded
    if header_size + encoded_total != len(stream):
        raise InventoryError(f"BLTE chunk sizes do not fill stream: header={header_size}, encoded_sum={encoded_total}, stream={len(stream)}")
    pos = header_size
    for i, (encoded, decoded) in enumerate(chunks):
        mode = stream[pos:pos + 1]
        if mode not in (b"N", b"Z"):
            raise InventoryError(f"Unsupported BLTE mode in chunk #{i}: {mode!r}")
        say(f"BLTE chunk #{i}: encoded={encoded}, decoded={decoded}, mode={mode.decode('ascii')}")
        pos += encoded


def validate_decoded_root(decoded: bytes, expected_ckey: str, expected_size: int) -> str:
    actual_ckey = hashlib.md5(decoded).hexdigest()
    if len(decoded) != expected_size or len(decoded) > MAX_VFS_DECODED or actual_ckey != expected_ckey.lower():
        raise InventoryError(f"VFS-root decoded validation failed: length={len(decoded)}/{expected_size}, MD5={actual_ckey}/{expected_ckey.lower()}")
    return actual_ckey


def read_vfs_region(game: Path, config: dict[str, tuple[str, ...]], sizes: dict[str, tuple[int, int]],
                    log: list[dict[str, object]], helper, out_dir: Path) -> tuple[bytes, dict[str, object]]:
    ekey = config["vfs-root"][1].lower()
    idx_dir = game / "Data" / "data"
    idx_files = [p for p in idx_dir.iterdir() if p.is_file() and re.fullmatch(r"[0-9a-fA-F]{10}\.idx", p.name)]
    if not idx_files:
        raise InventoryError(f"No V2 .idx files in {idx_dir}")
    newest: dict[int, tuple[int, Path]] = {}
    for path in idx_files:
        bucket, version = int(path.name[:2], 16), int(path.name[2:10], 16)
        if bucket > 15:
            raise InventoryError(f"Index bucket outside 00..0f: {path.name}")
        if bucket not in newest or version > newest[bucket][0]:
            newest[bucket] = (version, path)
        elif version == newest[bucket][0]:
            raise InventoryError(f"Ambiguous newest .idx for bucket {bucket:02x}")
    out_dir.mkdir(parents=True, exist_ok=True)
    status_detail = {"indexes": [], "index_match_count": 0}
    matches: list[dict[str, object]] = []
    prefix = bytes.fromhex(ekey)[:9]
    hash_state = {"detail_printed": False}
    for bucket in range(16):
        if bucket not in newest:
            continue
        version, path = newest[bucket]
        length = path.stat().st_size
        if length != 65536:
            raise InventoryError(f"Index size outside expected 64 KiB format: {path.name}={length}")
        raw = read_input(path, 0, length, log, MAX_METADATA)
        status_detail["indexes"].append({"name": path.name, "version": f"{version:08x}", "sha256": log[-1]["sha256"]})
        matches.extend(helper.parse_idx(path, raw, prefix, bucket, hash_state))
    status_detail["index_match_count"] = len(matches)
    if len(matches) != 1:
        raise InventoryError(f"VFS-root EKey requires exactly one .idx entry, found {len(matches)}")
    entry = matches[0]
    decoded_size, encoded_size = sizes["vfs-root"]
    if int(entry["size"]) != encoded_size + LOCAL_HEADER_SIZE:
        raise InventoryError(f"VFS-root index size mismatch: {entry['size']} vs config encoded size + 30 = {encoded_size + LOCAL_HEADER_SIZE}")
    if int(entry["archive"]) > 0xFFF or int(entry["offset"]) < 0:
        raise InventoryError(f"VFS-root index location outside bounds: {entry}")
    shard = idx_dir / f"data.{int(entry['archive']):03d}"
    if not shard.is_file() or int(entry["offset"]) + int(entry["size"]) > shard.stat().st_size:
        raise InventoryError(f"VFS-root region exceeds or lacks referenced shard {shard}")
    region = read_input(shard, int(entry["offset"]), int(entry["size"]), log, MAX_VFS_ENCODED + LOCAL_HEADER_SIZE)
    stream = helper.validate_local_data_header(region, ekey, int(entry["size"]), encoded_size)
    if len(stream) != encoded_size:
        raise InventoryError(f"BLTE stream length mismatch: {len(stream)} vs config encoded size {encoded_size}")
    if len(stream) < 12 or stream[:4] != b"BLTE":
        raise InventoryError("VFS-root BLTE magic/header is truncated")
    validate_blte_stream(stream, encoded_size)
    decoded = helper.decode_blte(stream)
    expected_ckey = config["vfs-root"][0].lower()
    actual_ckey = validate_decoded_root(decoded, expected_ckey, decoded_size)
    return decoded, {"path": str(shard), "archive": int(entry["archive"]), "offset": int(entry["offset"]),
                     "indexed_size": int(entry["size"]), "stream_size": len(stream), "decoded_size": len(decoded),
                     "content_key_md5": actual_ckey, "index": str(entry["idx"]), "index_eKey_prefix": prefix.hex(),
                     "index_scan": status_detail}


def checked_slice(data: bytes, start: int, length: int, field: str) -> bytes:
    if start < 0 or length < 0 or start > len(data) or length > len(data) - start:
        raise InventoryError(f"TVFS bounds error for {field}: offset={start}, length={length}, file_size={len(data)}")
    return data[start:start + length]


def parse_tvfs_header(data: bytes, fields_so_far: dict[str, int] | None = None) -> dict[str, int]:
    parsed = fields_so_far if fields_so_far is not None else {}
    checked_slice(data, 0, 8, "fixed header prefix")
    if data[:4] != TVFS_SIGNATURE:
        raise InventoryError(f"TVFS signature mismatch at offset 0: {data[:4].hex()}")
    version, header_size, ekey_size, patch_key_size = data[4:8]
    parsed.update({"version": version, "header_size": header_size, "ekey_size": ekey_size, "patch_key_size": patch_key_size})
    if version != 1:
        raise InventoryError(f"Unsupported TVFS version={version}")
    if ekey_size not in (9, 16) or patch_key_size != ekey_size:
        raise InventoryError(f"Unsupported TVFS key widths: EKey={ekey_size}, patch={patch_key_size}")
    checked_slice(data, 8, 4, "flags")
    flags = int.from_bytes(data[8:12], "little")
    parsed["flags"] = flags
    expected_header_size = TVFS_EXTENDED_HEADER if flags & 0x0006 else TVFS_BASE_HEADER
    if header_size != expected_header_size or header_size > len(data):
        raise InventoryError(f"TVFS header-size/flags mismatch or truncation: version={version}, header_size={header_size}, flags=0x{flags:08x}, expected={expected_header_size}, data_length={len(data)}")
    checked_slice(data, 12, 26, "base offset table")
    fields = [int.from_bytes(data[i:i + 4], "big") for i in (12, 16, 20, 24, 28, 32)]
    max_depth = int.from_bytes(data[36:38], "big")
    est_offset = est_size = 0
    if header_size == TVFS_EXTENDED_HEADER:
        checked_slice(data, 38, 8, "EST offset table")
        est_offset = int.from_bytes(data[38:42], "big")
        est_size = int.from_bytes(data[42:46], "big")
    path_offset, path_size, vfs_offset, vfs_size, cft_offset, cft_size = fields
    parsed.update({"flags": flags, "path_offset": path_offset, "path_size": path_size, "vfs_offset": vfs_offset,
                   "vfs_size": vfs_size, "cft_offset": cft_offset, "cft_size": cft_size,
                   "max_depth": max_depth, "est_offset": est_offset, "est_size": est_size})
    ranges = {"path": (path_offset, path_size), "vfs": (vfs_offset, vfs_size), "cft": (cft_offset, cft_size)}
    if header_size == TVFS_EXTENDED_HEADER:
        ranges["est"] = (est_offset, est_size)
    for name, (offset, size) in ranges.items():
        if name == "est" and size == 0 and offset == 0:
            continue
        if size == 0 or offset < header_size or offset > len(data) or size > len(data) - offset:
            raise InventoryError(f"TVFS {name} table outside decoded root: offset={offset}, size={size}, root={len(data)}")
    ordered = sorted((off, off + size, name) for name, (off, size) in ranges.items())
    for left, right in zip(ordered, ordered[1:]):
        if left[1] > right[0]:
            raise InventoryError(f"TVFS tables overlap: {left[2]} and {right[2]}")
    cft_offset_width = 4 if cft_size > 0xFFFFFF else 3 if cft_size > 0xFFFF else 2 if cft_size > 0xFF else 1
    parsed["cft_offset_width"] = cft_offset_width
    return parsed


def valid_component(raw: bytes, at: int) -> str:
    if len(raw) > MAX_COMPONENT:
        raise InventoryError(f"TVFS path component exceeds {MAX_COMPONENT} bytes at offset {at}")
    try:
        value = raw.decode("utf-8", "strict")
    except UnicodeDecodeError as exc:
        raise InventoryError(f"Invalid UTF-8 path component at offset {at}") from exc
    if any(ord(ch) < 32 or ch in "<>\"|?*" for ch in value) or "\\" in value:
        raise InventoryError(f"Disallowed character in TVFS path component at offset {at}: {value!r}")
    if value in (".", "..") or ":" in value:
        raise InventoryError(f"Unsafe TVFS path component at offset {at}: {value!r}")
    return value


def parse_tvfs_paths(data: bytes, header: dict[str, int]) -> tuple[list[dict[str, object]], dict[str, object]]:
    path_start, path_size = header["path_offset"], header["path_size"]
    path_end = path_start + path_size
    vfs_start, vfs_size = header["vfs_offset"], header["vfs_size"]
    cft_start, cft_size = header["cft_offset"], header["cft_size"]
    cft_width, ekey_size = header["cft_offset_width"], header["ekey_size"]
    rows: list[dict[str, object]] = []
    paths: set[str] = set()
    stats = {"duplicate_paths": 0, "invalid_paths": 0, "unresolved_or_unsupported": [], "parsed_path_count": 0}

    def parse_dir(start: int, end: int, prefix: str, depth: int) -> None:
        if depth > MAX_DEPTH:
            raise InventoryError(f"TVFS directory nesting exceeds limit at path {prefix!r}")
        pos = start
        while pos < end:
            record_at = pos
            pre_sep = False
            if data[pos] == 0:
                pre_sep = True
                pos += 1
                if pos >= end:
                    raise InventoryError(f"Truncated path separator at offset {record_at}")
            if data[pos] == 0xFF:
                name = ""
            else:
                name_len = data[pos]
                pos += 1
                if name_len > end - pos:
                    raise InventoryError(f"Path name crosses directory boundary at offset {pos}")
                name = valid_component(checked_slice(data, pos, name_len, "path name"), pos)
                pos += name_len
            post_sep = False
            if pos < end and data[pos] == 0:
                post_sep = True
                pos += 1
            node_value = None
            if pos < end and data[pos] == 0xFF:
                if end - pos < 5:
                    raise InventoryError(f"Truncated node value at offset {pos}")
                checked_slice(data, pos, 5, "node value")
                node_value = int.from_bytes(data[pos + 1:pos + 5], "big")
                pos += 5
            elif pos < end:
                post_sep = True
            piece = ("/" if pre_sep and prefix else "") + name + ("/" if post_sep else "")
            current = prefix + piece
            if len(current) > MAX_TOTAL_PATH:
                raise InventoryError(f"TVFS path exceeds limit at offset {record_at}")
            if node_value is not None:
                if node_value & 0x80000000:
                    folder_len = node_value & 0x7FFFFFFF
                    if folder_len < 4:
                        raise InventoryError(f"Invalid folder length at offset {record_at}: {folder_len}")
                    child_start = pos
                    child_end = child_start + folder_len - 4
                    if child_end > end or child_end < child_start:
                        raise InventoryError(f"Folder range outside path table at offset {record_at}")
                    parse_dir(child_start, child_end, current, depth + 1)
                    pos = child_end
                    continue
                file_path = current.rstrip("/")
                if not file_path or file_path.startswith("/") or any(part in ("", ".", "..") for part in file_path.split("/")):
                    raise InventoryError(f"Invalid reconstructed TVFS path at offset {record_at}: {file_path!r}")
                if node_value >= vfs_size:
                    raise InventoryError(f"VFS offset reference outside VFS table at path {file_path!r}: {node_value}")
                span_count = data[vfs_start + node_value]
                if not 1 <= span_count <= 224:
                    raise InventoryError(f"Unsupported TVFS span count {span_count} for {file_path!r}")
                item_size = 8 + cft_width
                spans_at = vfs_start + node_value + 1
                if spans_at > vfs_start + vfs_size or span_count > (vfs_start + vfs_size - spans_at) // item_size:
                    raise InventoryError(f"VFS span records exceed table for {file_path!r}")
                spans = []
                for index in range(span_count):
                    at = spans_at + index * item_size
                    span_offset = int.from_bytes(checked_slice(data, at, 4, "span file offset"), "big")
                    content_size = int.from_bytes(checked_slice(data, at + 4, 4, "span content size"), "big")
                    cft_rel = int.from_bytes(checked_slice(data, at + 8, cft_width, "CFT reference"), "big")
                    if cft_rel > cft_size or ekey_size + 4 > cft_size - cft_rel:
                        raise InventoryError(f"CFT reference outside table for {file_path!r}: {cft_rel}")
                    cft_at = cft_start + cft_rel
                    ekey = checked_slice(data, cft_at, ekey_size, "CFT EKey").hex()
                    encoded_size = int.from_bytes(checked_slice(data, cft_at + ekey_size, 4, "CFT encoded size"), "big")
                    spans.append({"span_offset": span_offset, "content_size": content_size, "cft_offset": cft_rel,
                                  "ekey": ekey, "encoded_size": encoded_size})
                if stats["parsed_path_count"] >= MAX_PATHS:
                    raise InventoryError(f"TVFS path count exceeds cap {MAX_PATHS}")
                if file_path in paths:
                    stats["duplicate_paths"] += 1
                    raise InventoryError(f"Duplicate TVFS path rejected: {file_path!r}")
                paths.add(file_path)
                rows.append({"resource_path": file_path, "path_extension": Path(file_path).suffix.lower(),
                             "content_key": "", "referenced_metadata": json.dumps(spans, separators=(",", ":")),
                             "parsing_status": "TVFS path and span/CFT references parsed; asset payload not read"})
                stats["parsed_path_count"] += 1
            elif pos <= record_at:
                raise InventoryError(f"TVFS parser made no progress at offset {record_at}")

    root_start = path_start
    root_end = path_end
    if path_size >= 5 and data[root_start] == 0xFF:
        value = int.from_bytes(data[root_start + 1:root_start + 5], "big")
        if not value & 0x80000000:
            raise InventoryError("TVFS root directory marker lacks folder bit")
        root_len = value & 0x7FFFFFFF
        root_start += 5
        root_end = path_start + 1 + root_len
        if root_len < 4 or root_end > path_end:
            raise InventoryError(f"TVFS root folder length outside path table: {root_len}")
    parse_dir(root_start, root_end, "", 0)
    stats["path_table_bytes"] = path_size
    stats["vfs_table_bytes"] = vfs_size
    stats["cft_table_bytes"] = cft_size
    return rows, stats


def write_outputs(out_dir: Path, rows: list[dict[str, object]], report: dict[str, object]) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    csv_path = out_dir / "vfs_resource_inventory.csv"
    with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        fields = ["resource_path", "path_extension", "content_key", "referenced_metadata", "parsing_status"]
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="raise")
        writer.writeheader()
        writer.writerows(rows)
    (out_dir / "execution_report.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def parse_and_report(decoded: bytes, out_dir: Path, base_report: dict[str, object]) -> None:
    partial_header: dict[str, int] = {}
    try:
        header = parse_tvfs_header(decoded, partial_header)
        rows, parse_stats = parse_tvfs_paths(decoded, header)
    except Exception as exc:
        say(f"TVFS header/parse failure: {exc}")
        say(f"Decoded TVFS first 64 bytes: {decoded[:64].hex(' ')}")
        say("TVFS header fields parsed so far: " + json.dumps(partial_header, sort_keys=True))
        raise
    report = dict(base_report)
    report["tvfs_header"] = header
    report["inventory"] = parse_stats
    report["duplicate_path_count"] = parse_stats["duplicate_paths"]
    report["invalid_path_count"] = parse_stats["invalid_paths"]
    report["unsupported_or_unresolved"] = parse_stats["unresolved_or_unsupported"]
    report.setdefault("validation", {})
    report["validation"]["TVFS_bounds_and_paths"] = True
    write_outputs(out_dir, rows, report)
    say(f"Inventory complete: {len(rows)} paths; CSV and JSON report saved under {out_dir}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Read-only CASC TVFS path inventory; does not extract assets")
    parser.add_argument("game_dir", nargs="?", help="MW2 Campaign Remastered installation directory")
    parser.add_argument("--from-file", dest="from_file", help="Parse only the previously saved out/phase12f/vfs_root.bin")
    args = parser.parse_args()
    if bool(args.game_dir) == bool(args.from_file):
        parser.error("provide exactly one of the game directory argument or --from-file")
    script_dir = Path(__file__).resolve().parent
    out_dir = script_dir / "out" / "phase12f"
    log: list[dict[str, object]] = []
    if args.from_file:
        expected_blob = (out_dir / "vfs_root.bin").resolve()
        supplied_blob = Path(args.from_file).expanduser().resolve(strict=True)
        if supplied_blob != expected_blob:
            raise InventoryError(f"Offline mode accepts only the saved VFS-root blob: {expected_blob}")
        if not supplied_blob.is_file() or supplied_blob.stat().st_size > MAX_VFS_DECODED:
            raise InventoryError(f"Missing or oversized offline VFS-root blob: {supplied_blob}")
        decoded = read_input(supplied_blob, 0, supplied_blob.stat().st_size, log, MAX_VFS_DECODED)
        base_report: dict[str, object] = {
            "status": "offline TVFS parse; no game directory opened",
            "active_build": "NOT VERIFIED in offline mode; blob provenance is external to this parse",
            "vfs_root_region": {"source": str(supplied_blob), "decoded_size": len(decoded), "sha256": log[-1]["sha256"]},
            "input_reads": log,
            "validation": {"build_identity": False, "unique_index_match": False, "local_header": False,
                           "BLTE_chunk_hashes": False, "decoded_vfs_root_MD5_and_size": False,
                           "referenced_assets_read": False},
        }
        parse_and_report(decoded, out_dir, base_report)
        return 0

    game = Path(args.game_dir).expanduser().resolve(strict=True)
    if not game.is_dir():
        raise InventoryError("Game path is not a directory")
    helper = load_phase12d(script_dir)
    build_info_path = game / ".build.info"
    if not build_info_path.is_file() or build_info_path.stat().st_size > MAX_CONFIG:
        raise InventoryError("Missing or oversized .build.info")
    build_values = parse_build_info(read_input(build_info_path, 0, build_info_path.stat().st_size, log, MAX_CONFIG))
    config_path = game / "Data" / "config" / EXPECTED_BUILD_KEY[:2] / EXPECTED_BUILD_KEY[2:4] / EXPECTED_BUILD_KEY
    if not config_path.is_file() or config_path.stat().st_size > MAX_CONFIG:
        raise InventoryError(f"Missing or oversized active Build Config: {config_path}")
    config_raw = read_input(config_path, 0, config_path.stat().st_size, log, MAX_CONFIG)
    config = parse_build_config(config_raw)
    sizes = parse_sizes(config_raw)
    say(f"Verified build: branch={build_values['Branch']}; product={EXPECTED_PRODUCT}; version={build_values['Version']}; build={EXPECTED_BUILD_NAME}")
    say(f"root CKey={config['root'][0]}; vfs-root={config['vfs-root']}; vfs-1={config['vfs-1']}; sizes={sizes}")
    decoded, region_info = read_vfs_region(game, config, sizes, log, helper, out_dir)
    say(f"Verified VFS-root content: MD5={region_info['content_key_md5']}; decoded_size={region_info['decoded_size']}")
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "vfs_root.bin").write_bytes(decoded)
    say(f"Saved decoded VFS-root to {out_dir / 'vfs_root.bin'} before TVFS parsing")
    base_report = {"status": "completed local metadata and TVFS path parse; no referenced assets read",
                   "active_build": {"branch": EXPECTED_BRANCH, "product": EXPECTED_PRODUCT, "version": EXPECTED_VERSION,
                                    "build_name": EXPECTED_BUILD_NAME, "build_key": build_values["Build Key"], "cdn_key": build_values["CDN Key"]},
                   "config": {key: list(value) for key, value in config.items()},
                   "config_sizes": {key: list(value) for key, value in sizes.items()},
                   "vfs_root_region": region_info, "input_reads": log,
                   "saved_vfs_root": {"path": str(out_dir / "vfs_root.bin"), "size": len(decoded),
                                      "sha256": hashlib.sha256(decoded).hexdigest()},
                   "validation": {"config_identity": True, "single_unique_index_match": True, "local_header": True,
                                  "BLTE_chunk_hashes": True, "decoded_vfs_root_MD5_and_size": True,
                                  "referenced_assets_read": False}}
    parse_and_report(decoded, out_dir, base_report)
    say("No referenced asset payloads were read. Review JSON input_reads for paths, offsets, lengths, and SHA-256 hashes.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"STOP: {exc}", file=sys.stderr, flush=True)
        raise SystemExit(2)
