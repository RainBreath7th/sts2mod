#!/usr/bin/env python3
"""将已验证的多版本 dist 打包；白名单来自变体清单，不夹带日志或开发文件。"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = "hextech-runes-variants.manifest"


def package_files(dist: Path) -> list[Path]:
    entries = [Path("HextechRunes.json"), Path("HextechRunes.dll"),
               Path("HextechRunes.pck"), Path(MANIFEST)]
    manifest = json.loads((dist / MANIFEST).read_text(encoding="utf-8"))
    for variant in manifest["variants"]:
        folder = Path(variant["directory"])
        entries.extend([folder / variant["assembly"], folder / "compat-target.txt"])
    return entries


def write_package(dist: Path, destination: Path) -> None:
    result = subprocess.run([sys.executable, str(ROOT / "tools/multi_version/validate_variant_bundle.py"),
                    "--dist", str(dist), "--mod-id", "HextechRunes", "--manifest-name", MANIFEST],
                   check=True, capture_output=True, text=True)
    print(result.stdout.strip())
    entries = package_files(dist)
    destination = destination.resolve()
    if destination in {(dist / entry).resolve() for entry in entries}:
        raise ValueError("输出路径不能覆盖发行输入文件")
    destination.parent.mkdir(parents=True, exist_ok=True)
    # 先生成并验证临时 ZIP，成功后才替换已有发行包。
    with tempfile.TemporaryDirectory(prefix="hextech-package-", dir=destination.parent) as temporary:
        staged = Path(temporary) / "HextechRunes.zip"
        with zipfile.ZipFile(staged, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for entry in entries:
                archive.write(dist / entry, (Path("HextechRunes") / entry).as_posix())
        with zipfile.ZipFile(staged) as archive:
            if archive.testzip() is not None:
                raise ValueError("ZIP CRC 校验失败")
            expected = [(Path("HextechRunes") / entry).as_posix() for entry in entries]
            if archive.namelist() != expected:
                raise ValueError("ZIP 内容与发行白名单不一致")
        staged.replace(destination)
    print(f"已打包 {len(entries)} 个文件: {destination}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", nargs="?", type=Path, default=ROOT / "dist/HextechRunes.zip")
    parser.add_argument("--dist", type=Path, default=ROOT / "dist")
    args = parser.parse_args()
    try:
        write_package(args.dist.resolve(), args.output)
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f"打包失败: {error}", file=sys.stderr)
        if isinstance(error, subprocess.CalledProcessError) and error.stderr:
            print(error.stderr.strip(), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
