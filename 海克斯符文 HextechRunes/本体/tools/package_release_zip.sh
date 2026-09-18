#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# 保留旧命令入口；文件集合由多版本清单决定，不能再只打包根目录三个文件。
exec python3 "$SCRIPT_DIR/package_release.py" "$@"
