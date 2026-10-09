#!/usr/bin/env python3
"""Download Craft of Exile PoE2 data into this skill's data/ folder."""
from __future__ import annotations

import json
import re
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "data"
BASE = "https://beta.craftofexile.com"
UA = "crafting-guide/1.0"


def get(url: str) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read()


def detect_patch() -> str:
    html = get(f"{BASE}/?game=poe2").decode("utf-8", "replace")
    m = re.search(r"json/poe2/([0-9.]+)/data\.json", html)
    if m:
        return m.group(1)
    existing = sorted((DATA / "poe2").glob("*"))
    if existing:
        return existing[-1].name
    raise SystemExit("Could not detect CoE patch and no local copy exists.")


def load_js_object(text: str, prefix: str):
    text = text.strip()
    if text.startswith(prefix):
        text = text[len(prefix) :]
    if text.endswith(";"):
        text = text[:-1]
    return json.loads(text)


def extract_currency_constraints(data: dict, out: Path) -> None:
    crafting = data.get("methods", {}).get("crafting") or []
    rows = []

    def walk(el):
        if not isinstance(el, dict):
            return
        rows.append(
            {
                "id": el.get("id"),
                "handler": el.get("handler"),
                "item": el.get("item"),
                "constraints": el.get("constraints") or [],
                "omens": el.get("omens") or [],
            }
        )
        for child in el.get("elements") or []:
            walk(child)

    for mod in crafting:
        if not isinstance(mod, dict):
            continue
        for el in mod.get("elements") or []:
            walk(el)
    out.write_text(json.dumps(rows, indent=2), encoding="utf-8")


def main() -> int:
    DATA.mkdir(parents=True, exist_ok=True)
    (DATA / "settings.json").write_bytes(get(f"{BASE}/json/settings.json"))
    patch = detect_patch()
    dest = DATA / "poe2" / patch
    dest.mkdir(parents=True, exist_ok=True)
    data_bytes = get(f"{BASE}/json/poe2/{patch}/data.json")
    lang_bytes = get(f"{BASE}/json/poe2/{patch}/localization/english.json")
    (dest / "data.json").write_bytes(data_bytes)
    (dest / "english.json").write_bytes(lang_bytes)
    meta = {"patch": patch, "source": BASE, "files": ["data.json", "english.json"]}
    (dest / "meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    data = load_js_object(data_bytes.decode("utf-8"), "coedata=")
    extract_currency_constraints(data, DATA / "currency-constraints.json")
    print(f"OK patch={patch} -> {dest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
