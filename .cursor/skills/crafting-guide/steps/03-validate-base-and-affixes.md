# Step 3 — Validate base and affixes

Read this file fully. Execute this step end-to-end now. Do not read the next step until this one is done.

## Definition

Prove the required affixes can exist on the chosen base, including family conflicts, tags that zero a pool, and min item level from CoE data.

## Instruction

1. Run `python .cursor/skills/crafting-guide/scripts/query_coe.py find-base "<name>"`.
2. For each required and optional affix, run `query_coe.py find-mod "<text>"` then `query_coe.py can-spawn --class-id <id> --group <gid>`.
3. Check families: `query_coe.py families --group <gid>`. If two required affixes share a family, they cannot coexist. **Stop and tell the user.** Do not design a craft that needs both.
4. Read min ilvl from the mod/group in local data. The base prerequisite ilvl is the maximum min-ilvl among required natural T1s you still plan to slam (essence-guaranteed mods use the essence, not that T1 ilvl).
5. On the live `-s=coe` session (see `steps/coe-playwright.md`), set the chosen base with ids from this step. Wait until `Calculator.filteredModpool.groupData` exists, dump it with `--raw eval --filename=temp/crafting-process/.scratch/pool.json`, and check required groups for missing / weight 0. Tags like `no_lightning_spell_mods` can zero a line that exists in generic class data.
6. Look up essence / currency that guarantees a required mod (`query_coe.py essence --text "<mod>"`).
7. Write: base id/class, spawnable required mods, blocked mods, family conflicts, min ilvl.

If a required affix cannot spawn and has no legal guaranteed craft, **stop and ask**.

## Good example

Required +Lightning Spell Skills on Gelid Staff. Live pool weight 0 because of `no_lightning_spell_mods`. Agent stops and says it is impossible on this base.

## Bad example

Agent assumes all spell-skill lines coexist because they are different groups, then later hits "Similar modifier already present!".

## Pitfalls

- Different groups can share one family. Family is the coexistence rule.
- Essence mods and natural mods in the same family still conflict.
- Do not quote T1 ranges from memory. Read them from `data/` or live `coedata.mods`.
- Snapshot refs from picking one base are invalid after the next click or item change. Re-snapshot or re-eval.

If this step cannot be completed, close `-s=coe`, run `python .cursor/skills/crafting-guide/scripts/cleanup_temp.py`, and ask the user.
If this step is complete, read `steps/04-evaluate-setup-and-lock.md` and execute that step end-to-end.
