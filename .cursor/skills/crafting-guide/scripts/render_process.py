#!/usr/bin/env python3
"""Render a verified crafting process JSON to markdown."""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


def render(doc: dict) -> str:
    lines = []
    title = doc.get("title") or "Crafting process"
    lines.append(f"# {title}")
    lines.append("")
    if doc.get("goal"):
        lines.append(doc["goal"])
        lines.append("")
    lines.append("## Prerequisites")
    lines.append("")
    base = doc.get("base") or {}
    lines.append("### Base")
    name = base.get("name") or "?"
    ilvl = base.get("ilvl_min")
    rarity = base.get("rarity")
    bit = f"- 1x {name}"
    if ilvl is not None:
        bit += f", item level {ilvl} or higher"
    if rarity:
        bit += f", {rarity}"
    lines.append(bit)
    for n in base.get("notes") or []:
        lines.append(f"- {n}")
    lines.append("")
    lines.append("### Currency and omens (happy path: each step succeeds once)")
    lines.append("")
    lines.append("| Item | Qty |")
    lines.append("|---|---|")
    for row in doc.get("happy_path") or []:
        if int(row.get("qty") or 0) <= 0:
            continue
        lines.append(f"| {row['item']} | {row['qty']} |")
    lines.append("")
    if doc.get("happy_path_note"):
        lines.append(doc["happy_path_note"])
        lines.append("")
    lines.append("## Steps")
    lines.append("")
    for i, step in enumerate(doc.get("steps") or [], 1):
        lines.append(f"### {i}. {step.get('title') or 'Step'}")
        if step.get("warning"):
            lines.append("")
            lines.append(step["warning"])
        lines.append("")
        for para in step.get("body") or []:
            lines.append(para)
            lines.append("")
    keepers = doc.get("keepers") or {}
    if keepers.get("suffix") or keepers.get("prefix"):
        lines.append("## What to keep")
        lines.append("")
        if keepers.get("suffix"):
            lines.append("Suffix: " + ", ".join(keepers["suffix"]) + ".")
        if keepers.get("prefix"):
            lines.append("Prefix: " + ", ".join(keepers["prefix"]) + ".")
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("json_path")
    ap.add_argument("-o", "--output", required=True)
    args = ap.parse_args()
    doc = json.loads(Path(args.json_path).read_text(encoding="utf-8-sig"))
    text = render(doc)
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text, encoding="utf-8")
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
