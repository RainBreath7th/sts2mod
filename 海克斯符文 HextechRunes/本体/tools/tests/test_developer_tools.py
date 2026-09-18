#!/usr/bin/env python3
"""只检查维护工具的关键边界；使用临时文件，不触碰游戏或现有发行包。"""
from __future__ import annotations

import argparse
from contextlib import redirect_stdout
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import hextech_dev as dev
import package_release as packaging


class DeveloperToolTests(unittest.TestCase):
    def test_localization_copy_preserves_unrelated_values_and_formatting(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            path = root / "assets/localization/zhs/relics.json"
            path.parent.mkdir(parents=True)
            text = '{\n  "from": "[blue]{Stacks}[/blue] \\"引号\\"",\n  "to" : "旧",\n  "keep": "旧"\n}\n'
            path.write_text(text, encoding="utf-8")
            [(changed, before, after)] = dev.localization_changes(root, "relics", "from", "to", None)
            self.assertEqual(changed, path)
            self.assertEqual(path.read_text(), text)  # 默认预览不写入
            self.assertIn('  "keep": "旧"', after)
            self.assertIn('  "to" : ', after)
            self.assertEqual(json.loads(after)["to"], json.loads(before)["from"])
            path.write_text(after, encoding="utf-8")
            self.assertEqual(dev.localization_changes(root, "relics", "from", "to", None), [])

    def test_missing_key_in_later_locale_does_not_partially_write(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            originals = {}
            for locale, data in (("eng", {"from": "new", "to": "old"}), ("zhs", {"from": "新"})):
                path = root / f"assets/localization/{locale}/relics.json"
                path.parent.mkdir(parents=True)
                path.write_text(json.dumps(data, indent=2), encoding="utf-8")
                originals[path] = path.read_bytes()
            args = argparse.Namespace(table="relics", source="from", destination="to",
                                      locale=None, apply=True, check=False)
            with patch.object(dev, "ROOT", root), self.assertRaisesRegex(ValueError, "to"):
                dev.sync_localization(args)
            self.assertTrue(all(path.read_bytes() == content for path, content in originals.items()))

    def test_focused_runner_rejects_typo_and_stops_after_build_failure(self):
        args = argparse.Namespace(list=False, name=["MisspelledTest"], target="0.111.0", run=True, match="")
        with patch.object(dev.subprocess, "run") as run:
            with self.assertRaisesRegex(ValueError, "未知测试名"):
                dev.focused_tests(args)
            run.assert_not_called()
        args.name = ["HopperEscapeSurvivesTheNextNativeMoveRoll"]
        with patch.object(dev.subprocess, "run", side_effect=subprocess.CalledProcessError(1, "dotnet")) as run:
            with patch.object(Path, "is_file", return_value=True), redirect_stdout(io.StringIO()):
                with self.assertRaises(subprocess.CalledProcessError):
                    dev.focused_tests(args)
            self.assertEqual(run.call_count, 1)
            self.assertEqual(run.call_args.args[0][1], "build")

    def make_bundle(self, root):
        dist = root / "dist"
        dist.mkdir()
        (dist / "HextechRunes.json").write_text(json.dumps({"id": "HextechRunes", "has_dll": True,
            "has_pck": True, "min_game_version": "0.107.1"}))
        (dist / "HextechRunes.dll").write_bytes(b"test loader")
        (dist / "HextechRunes.pck").write_bytes(b"test pck")
        variants = []
        for target in ("0.107.1", "0.110.0", "0.111.0"):
            sub = dist / "lib" / target
            sub.mkdir(parents=True)
            payload = target.encode()
            (sub / "HextechRunes.dll").write_bytes(payload)
            (sub / "compat-target.txt").write_text(target)
            variants.append({"compatTarget": target, "directory": "lib/" + target,
                             "assembly": "HextechRunes.dll", "sha256": hashlib.sha256(payload).hexdigest()})
        (dist / packaging.MANIFEST).write_text(json.dumps({"schema": 1, "variants": variants}))
        (dist / "更新日志.txt").write_text("不要收入包")
        (dist / ".DS_Store").write_bytes(b"metadata")
        return dist

    def test_package_contains_all_variants_and_excludes_notes(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            dist = self.make_bundle(root)
            output = root / "release.zip"
            packaging.write_package(dist, output)
            with zipfile.ZipFile(output) as archive:
                self.assertEqual(len(archive.namelist()), 10)
                for target in ("0.107.1", "0.110.0", "0.111.0"):
                    self.assertEqual(archive.read(f"HextechRunes/lib/{target}/HextechRunes.dll"), target.encode())
                self.assertFalse(any("更新日志" in name or ".DS_Store" in name for name in archive.namelist()))

    def test_corrupt_variant_preserves_previous_zip(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            dist = self.make_bundle(root)
            output = root / "release.zip"
            output.write_bytes(b"previous release")
            (dist / "lib/0.111.0/HextechRunes.dll").write_bytes(b"corrupt")
            with self.assertRaises(subprocess.CalledProcessError):
                packaging.write_package(dist, output)
            self.assertEqual(output.read_bytes(), b"previous release")


if __name__ == "__main__":
    unittest.main()
