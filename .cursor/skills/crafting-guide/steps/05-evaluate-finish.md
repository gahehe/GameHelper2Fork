# Step 5 — Evaluate finish

Read this file fully. Execute this step end-to-end now. Do not read the next step until this one is done.

## Definition

After the required affix is locked, decide how to fill or clean the other slots. Order matters. The first line of this finish plan is whatever the user must **not** do next.

## Instruction

1. Record post-lock state: open prefixes, open suffixes, which mods are fractured (Annul skips fractured).
2. If one side has 0 open slots, plain Exalt/Annul is already forced to the other side. Do not add a side-lock omen. Prove with CoE trials in the same `-s=coe` session (`steps/coe-playwright.md`).
3. If only fractured mods sit on one side, vanilla Annul can only hit the other side. A same-side Annul omen is redundant; the opposite-side Annul omen fails.
4. Decide order before naming Exalt. If prefix cleanup must happen while extra suffixes do not exist yet, **Annul prefixes first**. Put that instruction above any Exalt line. Exalting first adds a 5th affix and Annul can then hit the new suffix.
5. After an Annul opens both sides, re-test vanilla Exalt. If it mixed prefix/suffix, a side-lock omen now earns its keep. Re-check; do not copy the pre-Annul omen rule.
6. Homogenising and similar omens: keep only if CoE trials show a better keeper rate, not because the name sounds relevant.
7. Annul is allowed now (required affix is locked). Still do not Annul to salvage a missed fracture on a cheap base if the process says dump.

## Good example

Post-lock: 3 prefixes, 1 fractured suffix, open 0/2. Finish doc starts with: "Do not Exalt yet. Look at the 3 Prefixes. Annul junk prefixes now." Then Exalted Orb with no suffix omen.

## Bad example

Post-lock section leads with "Suffix: Exalted Orb" so the user slams a 5th affix before cleaning prefixes.

## Pitfalls

- Sinistral vs Dextral: verify the omen key. Max prefixes on Alchemy is `OmenOnAlchemyMaximumPrefixes` (Sinistral). Do not swap names.
- After both sides are open, yesterday's "plain Exalt is fine" is false. Re-run CoE trials on the current item state in `-s=coe`.
- Do not list Greater/Homogenising as required without trials.

If this step cannot be completed, close `-s=coe`, run `python .cursor/skills/crafting-guide/scripts/cleanup_temp.py`, and ask the user.
If this step is complete, read `steps/06-emit-process.md` and execute that step end-to-end.
