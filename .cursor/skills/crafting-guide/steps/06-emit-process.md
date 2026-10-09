# Step 6 — Emit process

Read this file fully. Execute this step end-to-end now. Do not read the next step until this one is done.

## Definition

Write one human crafting-guide markdown from verified steps only. Same text to that file and to chat.

## Instruction

1. Build a process JSON with only actions that passed steps 4–5. Schema is in `scripts/render_process.py`. Write it to `temp/crafting-process/.scratch/process.json`.
2. Prerequisites:
   - Base: name, min item level from step 3, rarity Alchemy/the first currency actually allows (from CoE constraints). Nothing else unless CoE requires it.
   - Table of orbs/omens/essences with **happy-path quantity** (each listed step succeeds once).
   - Do not list qty 0 items. Do not list sockets/quality/etc. if unused.
3. Steps: imperative, one action per step. "Use X + Y. You now have ..."
   - No trial ratios, handler names, or open-slot dumps in the player-facing file.
   - Fail branches in plain language (vendor, new base).
   - The first sentence of a finish step is the warning if acting too early would brick the rest.
4. Run `python .cursor/skills/crafting-guide/scripts/render_process.py temp/crafting-process/.scratch/process.json -o temp/crafting-process/<slug>.md`
5. Paste that markdown in chat. Do not add a parallel technical process, eval log, or second markdown file.
6. Close CoE and delete scratch (session first so files are not locked):
   `playwright-cli -s=coe close`
   `python .cursor/skills/crafting-guide/scripts/cleanup_temp.py --keep temp/crafting-process/<slug>.md`
   After this, `temp/crafting-process/` contains only that instruction `.md`.

## Good example

Prerequisites start with "1x Gelid Staff, item level 81 or higher, Normal or Magic (Alchemy cannot be used on Rare)". Step 1: "Use Orb of Alchemy + Omen of Sinistral Alchemy. You get a Rare with 3 Prefixes and 1 Suffix."

## Bad example

"White/normal is fine. No socket or quality requirement." Then a wall of 16/120 vs 7/120. Then Exalt listed before "Annul prefixes first".

## Pitfalls

- Happy-path qty is not expected value. Extra bases for dumps stay out of the table, or one sentence under it, not mixed into the table as invented counts.
- Do not include omens you evaluated and rejected.
- Do not leave `eval-log.md`, process JSON, playwright snapshots, or `run-code` scripts on disk.

If this step cannot be completed, close `-s=coe`, run `python .cursor/skills/crafting-guide/scripts/cleanup_temp.py`, and ask the user.
This is the last step. Do not invent a follow-up craft tree.
