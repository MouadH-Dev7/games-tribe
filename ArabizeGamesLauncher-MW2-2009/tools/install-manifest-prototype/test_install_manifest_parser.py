"""Synthetic-only tests for install_manifest_parser; no real game data used."""

import struct
import unittest

from install_manifest_parser import (
    ARABIC_TAGS,
    MAX_ENTRIES,
    ManifestParseError,
    bitmap_length,
    entries_for_arabic,
    parse_install_manifest,
)


HASH_SIZE = 16


def _bitmap(entry_count, indices, bit_order):
    result = bytearray(bitmap_length(entry_count))
    for index in indices:
        if bit_order == "msb":
            result[index // 8] |= 0x80 >> (index % 8)
        else:
            result[index // 8] |= 1 << (index % 8)
    return bytes(result)


def build_manifest(entries, tags, *, bit_order="msb", version=1, hash_size=HASH_SIZE):
    """Build bytes from (path, ckey_bytes, size) and (name, type, indices)."""
    header = b"IN" + bytes((version, hash_size)) + struct.pack(">HI", len(tags), len(entries))
    data = bytearray(header)
    for name, tag_type, indices in tags:
        data.extend(name.encode("utf-8") + b"\x00")
        data.extend(struct.pack(">H", tag_type))
        data.extend(_bitmap(len(entries), indices, bit_order))
    for path, ckey, size in entries:
        data.extend(path.encode("utf-8") + b"\x00")
        data.extend(ckey)
        data.extend(struct.pack(">I", size))
    return bytes(data)


def sample_entries():
    names = [
        "synthetic/ui_strings.bin",
        "synthetic/voice_bank.bin",
        "synthetic/shared_locale.bin",
        "synthetic/common.bin",
        "synthetic/unknown_tag.bin",
        "synthetic/unrelated.bin",
    ]
    return [(name, bytes([index + 1]) * HASH_SIZE, 100 + index) for index, name in enumerate(names)]


def sample_tags(bit_order):
    return [
        ("arSA text", 1, [0, 2]),
        ("arSA speech", 2, [1, 2]),
        ("zzZZ diagnostic", 65535, [4]),
    ]


class InstallManifestParserTests(unittest.TestCase):
    def test_valid_msb_membership_and_arabic_filter(self):
        parsed = parse_install_manifest(build_manifest(sample_entries(), sample_tags("msb")), bit_order="msb")
        self.assertEqual(len(parsed.entries), 6)
        self.assertEqual([e.index for e in parsed.entries], list(range(6)))
        self.assertEqual([t["name"] for t in parsed.entries[0].tags], ["arSA text"])
        self.assertEqual([t["name"] for t in parsed.entries[1].tags], ["arSA speech"])
        self.assertEqual([t["name"] for t in parsed.entries[2].tags], list(ARABIC_TAGS))
        self.assertEqual(parsed.entries[3].tags, ())
        self.assertEqual([t["name"] for t in parsed.entries[4].tags], ["zzZZ diagnostic"])
        self.assertEqual([e["index"] for e in entries_for_arabic(parsed)], [0, 1, 2])
        self.assertEqual([e["index"] for e in entries_for_arabic(parsed, "arSA text")], [0, 2])
        self.assertEqual([e["index"] for e in entries_for_arabic(parsed, "arSA speech")], [1, 2])

    def test_valid_lsb_membership_and_arabic_filter(self):
        parsed = parse_install_manifest(build_manifest(sample_entries(), sample_tags("lsb"), bit_order="lsb"), bit_order="lsb")
        self.assertEqual([t["name"] for t in parsed.entries[0].tags], ["arSA text"])
        self.assertEqual([t["name"] for t in parsed.entries[1].tags], ["arSA speech"])
        self.assertEqual([t["name"] for t in parsed.entries[2].tags], list(ARABIC_TAGS))
        self.assertEqual([e["index"] for e in entries_for_arabic(parsed)], [0, 1, 2])

    def test_bit_order_must_be_explicit(self):
        data = build_manifest(sample_entries(), sample_tags("msb"))
        with self.assertRaisesRegex(ManifestParseError, "explicitly set"):
            parse_install_manifest(data, bit_order="guess")

    def test_entry_fields_include_path_ckey_size_and_index(self):
        parsed = parse_install_manifest(build_manifest(sample_entries(), []), bit_order="msb")
        first = parsed.entries[0].as_dict()
        self.assertEqual(first["index"], 0)
        self.assertEqual(first["path"], "synthetic/ui_strings.bin")
        self.assertEqual(first["ckey"], (bytes([1]) * HASH_SIZE).hex())
        self.assertEqual(first["size"], 100)
        self.assertEqual(first["tags"], [])

    def test_unknown_tag_is_preserved_without_arabic_classification(self):
        parsed = parse_install_manifest(build_manifest(sample_entries(), sample_tags("msb")), bit_order="msb")
        self.assertEqual(parsed.tags[2].name, "zzZZ diagnostic")
        self.assertEqual(entries_for_arabic(parsed, "arSA text")[0]["index"], 0)
        self.assertNotIn(4, [row["index"] for row in entries_for_arabic(parsed)])

    def test_duplicate_paths_are_preserved_and_reported(self):
        entries = sample_entries()
        entries[5] = (entries[0][0], b"Z" * HASH_SIZE, 999)
        parsed = parse_install_manifest(build_manifest(entries, []), bit_order="msb")
        self.assertEqual(len(parsed.entries), 6)
        self.assertEqual(parsed.duplicate_paths, ("synthetic/ui_strings.bin",))
        self.assertNotEqual(parsed.entries[0].ckey, parsed.entries[5].ckey)

    def test_duplicate_tag_names_are_reported_and_arabic_filter_fails_closed(self):
        entries = sample_entries()
        tags = [("arSA text", 1, [0]), ("arSA text", 2, [1])]
        parsed = parse_install_manifest(build_manifest(entries, tags), bit_order="msb")
        self.assertEqual(parsed.duplicate_tag_names, ("arSA text",))
        self.assertEqual(len(parsed.entries[0].tags), 1)
        with self.assertRaisesRegex(ManifestParseError, "ambiguous duplicate"):
            entries_for_arabic(parsed, "arSA text")

    def test_truncated_bitmap_rejected(self):
        data = b"IN\x01\x10" + struct.pack(">HI", 1, 1) + b"arSA text\x00\x00\x01"
        with self.assertRaisesRegex(ManifestParseError, "truncated bitmap"):
            parse_install_manifest(data, bit_order="msb")

    def test_truncated_tag_type_rejected(self):
        data = b"IN\x01\x10" + struct.pack(">HI", 1, 1) + b"tag\x00\x01"
        with self.assertRaisesRegex(ManifestParseError, "truncated tag type"):
            parse_install_manifest(data, bit_order="msb")

    def test_unterminated_tag_name_rejected(self):
        data = b"IN\x01\x10" + struct.pack(">HI", 1, 0) + b"unterminated"
        with self.assertRaisesRegex(ManifestParseError, "unterminated tag name"):
            parse_install_manifest(data, bit_order="msb")

    def test_unterminated_file_path_rejected(self):
        data = b"IN\x01\x10" + struct.pack(">HI", 0, 1) + b"no-null-terminator"
        with self.assertRaisesRegex(ManifestParseError, "unterminated file path"):
            parse_install_manifest(data, bit_order="msb")

    def test_invalid_magic_rejected(self):
        data = b"DL\x01\x10" + struct.pack(">HI", 0, 0)
        with self.assertRaisesRegex(ManifestParseError, "invalid INSTALL magic"):
            parse_install_manifest(data, bit_order="msb")

    def test_unsupported_version_rejected(self):
        data = build_manifest([], [], version=2)
        with self.assertRaisesRegex(ManifestParseError, "unsupported INSTALL version"):
            parse_install_manifest(data, bit_order="msb")

    def test_invalid_hash_sizes_rejected(self):
        for size in (0, 15, 17, 255):
            with self.subTest(size=size):
                data = build_manifest([], [], hash_size=size)
                with self.assertRaisesRegex(ManifestParseError, "unsupported hash size"):
                    parse_install_manifest(data, bit_order="msb")

    def test_incomplete_ckey_rejected(self):
        data = b"IN\x01\x10" + struct.pack(">HI", 0, 1) + b"path\x00" + b"K" * 15
        with self.assertRaisesRegex(ManifestParseError, "incomplete CKey"):
            parse_install_manifest(data, bit_order="msb")

    def test_incomplete_file_size_rejected(self):
        data = b"IN\x01\x10" + struct.pack(">HI", 0, 1) + b"path\x00" + b"K" * 16 + b"\x00\x00\x00"
        with self.assertRaisesRegex(ManifestParseError, "incomplete 4-byte size"):
            parse_install_manifest(data, bit_order="msb")

    def test_unexpected_trailing_bytes_rejected(self):
        data = build_manifest([], [], bit_order="msb") + b"\x00"
        with self.assertRaisesRegex(ManifestParseError, "unexpected trailing bytes"):
            parse_install_manifest(data, bit_order="msb")

    def test_six_entries_use_one_bitmap_byte(self):
        self.assertEqual(bitmap_length(6), 1)
        parsed = parse_install_manifest(build_manifest(sample_entries(), sample_tags("msb")), bit_order="msb")
        self.assertEqual(len(parsed.entries), 6)

    def test_unused_tail_bits_rejected_for_both_orders(self):
        for bit_order, invalid_bitmap in (("msb", b"\x01"), ("lsb", b"\x80")):
            with self.subTest(bit_order=bit_order):
                header = b"IN\x01\x10" + struct.pack(">HI", 1, 6)
                data = header + b"tag\x00\x00\x01" + invalid_bitmap
                with self.assertRaisesRegex(ManifestParseError, "beyond entry_count"):
                    parse_install_manifest(data, bit_order=bit_order)

    def test_entry_count_limit_rejected_before_entry_loop(self):
        data = b"IN\x01\x10" + struct.pack(">HI", 0, MAX_ENTRIES + 1)
        with self.assertRaisesRegex(ManifestParseError, "exceeds limit"):
            parse_install_manifest(data, bit_order="msb")

    def test_tag_and_entry_overlap_preserves_all_memberships(self):
        parsed = parse_install_manifest(build_manifest(sample_entries(), sample_tags("msb")), bit_order="msb")
        shared = parsed.entries[2]
        self.assertEqual({tag["name"] for tag in shared.tags}, {"arSA text", "arSA speech"})


if __name__ == "__main__":
    unittest.main(verbosity=2)
