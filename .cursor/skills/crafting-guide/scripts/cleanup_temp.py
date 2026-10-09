#!/usr/bin/env python3
"""Delete crafting-guide scratch files and playwright-cli artifacts."""
from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
OUT_DIR = ROOT / "temp" / "crafting-process"
SCRATCH = OUT_DIR / ".scratch"
PLAYWRIGHT_DIRS = (
    ROOT / ".playwright-cli",
    Path(__file__).resolve().parents[1] / ".playwright-cli",
)


def rm(path: Path) -> None:
    if not path.exists():
        return
    if path.is_dir():
        shutil.rmtree(path)
    else:
        path.unlink()


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--keep",
        help="Instruction markdown to keep under temp/crafting-process/. "
        "When set, every other file in that folder is deleted.",
    )
    args = ap.parse_args()
    rm(SCRATCH)
    for d in PLAYWRIGHT_DIRS:
        rm(d)
    if not args.keep:
        return 0
    keep = Path(args.keep)
    if not keep.is_absolute():
        keep = ROOT / keep
    keep = keep.resolve()
    if keep.parent != OUT_DIR.resolve() or keep.suffix.lower() != ".md":
        raise SystemExit(f"--keep must be a .md inside {OUT_DIR}")
    if not keep.is_file():
        raise SystemExit(f"--keep not found: {keep}")
    for child in OUT_DIR.iterdir():
        if child.resolve() == keep:
            continue
        rm(child)
    return 0


if __name__ == "__main__":
    sys.exit(main())
