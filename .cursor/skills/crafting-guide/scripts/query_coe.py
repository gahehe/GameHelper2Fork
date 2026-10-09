#!/usr/bin/env python3
"""Query local Craft of Exile PoE2 data.json / english.json."""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "data"


def load_js(path: Path, prefix: str):
    text = path.read_text(encoding="utf-8").strip()
    if text.startswith(prefix):
        text = text[len(prefix) :]
    if text.endswith(";"):
        text = text[:-1]
    return json.loads(text)


def latest_patch_dir() -> Path:
    meta_files = list((DATA / "poe2").glob("*/meta.json"))
    if meta_files:
        return max(meta_files, key=lambda p: p.stat().st_mtime).parent
    dirs = [p for p in (DATA / "poe2").iterdir() if p.is_dir()]
    if not dirs:
        raise SystemExit("No data/poe2/<patch>/ found. Run fetch_coe_data.py.")
    return sorted(dirs)[-1]


def load() -> tuple[dict, list]:
    patch = latest_patch_dir()
    data = load_js(patch / "data.json", "coedata=")
    lang = load_js(patch / "english.json", "coelang=")
    return data, lang


def label(lang: list, idx) -> str:
    if idx is None:
        return ""
    try:
        i = int(idx)
    except (TypeError, ValueError):
        return str(idx)
    if 0 <= i < len(lang):
        return str(lang[i])
    return str(idx)


def normalize(text: str) -> str:
    text = re.sub(r"\[([^\]|]+)(?:\|[^\]]*)?\]", r"\1", text or "")
    return text.lower()


def cmd_find_base(data: dict, lang: list, q: str) -> None:
    qn = q.lower()
    hits = []
    for it in data["items"]["entries"]:
        name = label(lang, it.get("label"))
        key = it.get("key") or ""
        if qn in name.lower() or qn in key.lower():
            hits.append(
                {
                    "id": it.get("id"),
                    "name": name,
                    "key": key,
                    "class": it.get("class"),
                    "reqs": it.get("reqs"),
                    "tags": it.get("tags"),
                }
            )
    print(json.dumps(hits[:30], indent=2))


def stat_text(lang: list, m: dict) -> str:
    parts = []
    for st in m.get("stats") or []:
        parts.append(label(lang, st.get("label")))
    return " ".join(parts)


def cmd_find_mod(data: dict, lang: list, q: str) -> None:
    qn = q.lower()
    groups = {g["id"]: g for g in data["modgroups"]["entries"]}
    hits = []
    for m in data["mods"]["entries"]:
        name = label(lang, m.get("label"))
        key = m.get("key") or ""
        stats = stat_text(lang, m)
        blob = normalize(f"{name} {key} {stats}")
        if normalize(q) not in blob:
            continue
        g = groups.get(m.get("group")) or {}
        hits.append(
            {
                "id": m.get("id"),
                "key": key,
                "name": name,
                "stat": stats,
                "group": m.get("group"),
                "families": g.get("families"),
                "type": g.get("type"),
                "minlvl": m.get("minlvl"),
                "range": (m.get("stats") or [{}])[0].get("range") if m.get("stats") else None,
            }
        )
    print(json.dumps(hits[:40], indent=2))


def cmd_can_spawn(data: dict, class_id: int, group: int) -> None:
    weights = data["classmods"].get(str(class_id)) or {}
    mods = [m for m in data["mods"]["entries"] if m.get("group") == group]
    rows = []
    total = 0
    for m in mods:
        w = int(weights.get(str(m["id"]), 0) or 0)
        total += w
        if w:
            rows.append({"id": m["id"], "key": m["key"], "minlvl": m.get("minlvl"), "weight": w})
    print(json.dumps({"class_id": class_id, "group": group, "weight_sum": total, "mods": rows}, indent=2))


def cmd_families(data: dict, group: int) -> None:
    g = next((x for x in data["modgroups"]["entries"] if x.get("id") == group), None)
    fam_index = {f["id"]: f for f in data["families"]["entries"]}
    fams = []
    for fid in (g or {}).get("families") or []:
        f = fam_index.get(fid) or {}
        fams.append({"id": fid, "key": f.get("key")})
    print(json.dumps({"group": group, "type": (g or {}).get("type"), "tags": (g or {}).get("tags"), "families": fams}, indent=2))


def cmd_essence(data: dict, lang: list, q: str) -> None:
    qn = q.lower()
    mods = {m["id"]: m for m in data["mods"]["entries"]}
    by = data["essences"].get("byessences") or {}
    hits = []
    for ess in data["essences"]["entries"]:
        name = label(lang, ess.get("label"))
        blob = (name + " " + str(ess.get("key") or "")).lower()
        if qn not in blob and qn not in str(ess.get("id")):
            # also match if guaranteed mod name matches
            pass
        classmods = by.get(str(ess.get("id"))) or {}
        guaranteed = []
        for cls, ids in classmods.items() if isinstance(classmods, dict) else []:
            for mid in ids if isinstance(ids, list) else [ids]:
                m = mods.get(mid)
                if not m:
                    continue
                mname = label(lang, m.get("label"))
                mstat = stat_text(lang, m)
                guaranteed.append({"class": cls, "mod_id": mid, "key": m.get("key"), "name": mname, "stat": mstat, "group": m.get("group")})
        match = normalize(q) in normalize(blob) or any(
            normalize(q) in normalize(g["name"] + " " + g["key"] + " " + g.get("stat", "")) for g in guaranteed
        )
        if not match:
            continue
        hits.append({"id": ess.get("id"), "type": ess.get("type"), "name": name, "guaranteed": guaranteed[:12]})
    print(json.dumps(hits[:20], indent=2))


def cmd_currency() -> None:
    p = DATA / "currency-constraints.json"
    if not p.exists():
        raise SystemExit("data/currency-constraints.json missing. Run fetch_coe_data.py.")
    print(p.read_text(encoding="utf-8"))


def main() -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("find-base")
    p.add_argument("query")
    p = sub.add_parser("find-mod")
    p.add_argument("query")
    p = sub.add_parser("can-spawn")
    p.add_argument("--class-id", type=int, required=True)
    p.add_argument("--group", type=int, required=True)
    p = sub.add_parser("families")
    p.add_argument("--group", type=int, required=True)
    p = sub.add_parser("essence")
    p.add_argument("--text", required=True)
    sub.add_parser("currency")
    args = ap.parse_args()
    if args.cmd == "currency":
        cmd_currency()
        return 0
    data, lang = load()
    if args.cmd == "find-base":
        cmd_find_base(data, lang, args.query)
    elif args.cmd == "find-mod":
        cmd_find_mod(data, lang, args.query)
    elif args.cmd == "can-spawn":
        cmd_can_spawn(data, args.class_id, args.group)
    elif args.cmd == "families":
        cmd_families(data, args.group)
    elif args.cmd == "essence":
        cmd_essence(data, lang, args.text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
