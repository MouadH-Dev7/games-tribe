"""Synthetic-only unit tests for Phase 12F; never opens game files."""
import hashlib
import sys
import unittest
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import run_phase12f as phase12f


def make_tvfs(path_table: bytes, *, vfs_table: bytes | None = None, cft_table: bytes | None = None,
              flags: int = 0, header_size: int | None = None) -> bytes:
    if header_size is None:
        header_size = 46 if flags & 0x0006 else 38
    if vfs_table is None:
        vfs_table = b"\x01" + (0).to_bytes(4, "big") + (5).to_bytes(4, "big") + b"\x00"
    if cft_table is None:
        cft_table = b"\x11" * 9 + (7).to_bytes(4, "big")
    est_table = b"E" if header_size == 46 and flags & 0x0006 else b""
    path_offset = header_size
    vfs_offset = path_offset + len(path_table)
    cft_offset = vfs_offset + len(vfs_table)
    est_offset = cft_offset + len(cft_table) if est_table else 0
    header = (
        b"TVFS" + bytes((1, header_size, 9, 9)) + flags.to_bytes(4, "little")
        + path_offset.to_bytes(4, "big") + len(path_table).to_bytes(4, "big")
        + vfs_offset.to_bytes(4, "big") + len(vfs_table).to_bytes(4, "big")
        + cft_offset.to_bytes(4, "big") + len(cft_table).to_bytes(4, "big")
        + (1).to_bytes(2, "big")
    )
    if header_size == 46:
        header += est_offset.to_bytes(4, "big") + len(est_table).to_bytes(4, "big")
    assert len(header) == header_size
    return header + path_table + vfs_table + cft_table + est_table


def leaf(name: bytes, node: int = 0) -> bytes:
    return bytes((len(name),)) + name + b"\xff" + node.to_bytes(4, "big")


class Phase12fSyntheticTests(unittest.TestCase):
    def test_valid_minimal_path_and_cft_reference(self):
        rows, stats = phase12f.parse_tvfs_paths(make_tvfs(leaf(b"x")), phase12f.parse_tvfs_header(make_tvfs(leaf(b"x"))))
        self.assertEqual(rows[0]["resource_path"], "x")
        self.assertEqual(stats["parsed_path_count"], 1)

    def test_invalid_signature(self):
        with self.assertRaises(phase12f.InventoryError):
            phase12f.parse_tvfs_header(b"NOPE" + b"\x00" * 50)

    def test_truncated_header(self):
        with self.assertRaises(phase12f.InventoryError):
            phase12f.parse_tvfs_header(b"TVFS\x01")

    def test_header_size_38_without_est(self):
        raw = make_tvfs(leaf(b"x"), header_size=38)
        self.assertEqual(phase12f.parse_tvfs_header(raw)["header_size"], 38)

    def test_header_size_46_with_est(self):
        raw = make_tvfs(leaf(b"x"), flags=0x0002, header_size=46)
        parsed = phase12f.parse_tvfs_header(raw)
        self.assertEqual(parsed["header_size"], 46)
        self.assertEqual(parsed["est_size"], 1)

    def test_truncated_38_byte_header(self):
        raw = make_tvfs(leaf(b"x"), header_size=38)[:37]
        with self.assertRaises(phase12f.InventoryError):
            phase12f.parse_tvfs_header(raw)

    def test_truncated_46_byte_header(self):
        raw = make_tvfs(leaf(b"x"), flags=0x0002, header_size=46)[:45]
        with self.assertRaises(phase12f.InventoryError):
            phase12f.parse_tvfs_header(raw)

    def test_invalid_vfs_reference(self):
        raw = make_tvfs(leaf(b"x", node=100))
        with self.assertRaises(phase12f.InventoryError):
            phase12f.parse_tvfs_paths(raw, phase12f.parse_tvfs_header(raw))

    def test_duplicate_path_rejected(self):
        paths = leaf(b"x", 0) + leaf(b"x", 10)
        vfs = b"\x01" + b"\x00" * 8 + b"\x01" + b"\x00" * 4 + b"\x00\x00\x00\x05\x00"
        raw = make_tvfs(paths, vfs_table=vfs)
        with self.assertRaises(phase12f.InventoryError):
            phase12f.parse_tvfs_paths(raw, phase12f.parse_tvfs_header(raw))

    def test_invalid_utf8_component_rejected(self):
        raw = make_tvfs(leaf(b"\xff"))
        with self.assertRaises(phase12f.InventoryError):
            phase12f.parse_tvfs_paths(raw, phase12f.parse_tvfs_header(raw))

    def test_unsupported_span_count_rejected(self):
        vfs = bytes((225,)) + b"\x00" * 9
        raw = make_tvfs(leaf(b"x"), vfs_table=vfs)
        with self.assertRaises(phase12f.InventoryError):
            phase12f.parse_tvfs_paths(raw, phase12f.parse_tvfs_header(raw))

    def test_bad_blte_dimensions_rejected(self):
        malformed = b"BLTE" + (36).to_bytes(4, "big") + b"\x0f\x00\x00\x01"
        with self.assertRaises(phase12f.InventoryError):
            phase12f.validate_blte_stream(malformed, len(malformed))

    def test_decoded_key_and_size_are_gated(self):
        sample = b"synthetic-root"
        self.assertEqual(phase12f.validate_decoded_root(sample, hashlib.md5(sample).hexdigest(), len(sample)), hashlib.md5(sample).hexdigest())
        with self.assertRaises(phase12f.InventoryError):
            phase12f.validate_decoded_root(sample, "0" * 32, len(sample))
        with self.assertRaises(phase12f.InventoryError):
            phase12f.validate_decoded_root(sample, hashlib.md5(sample).hexdigest(), len(sample) + 1)


if __name__ == "__main__":
    unittest.main()
