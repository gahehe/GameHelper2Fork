# Step 4 — Evaluate setup and lock

Read this file fully. Execute this step end-to-end now. Do not read the next step until this one is done.

## Definition

Find how to put the required affix on the item and lock it. Every currency/omen must be proven on CoE at that item state.

## Instruction

Work on a copy of the item in the same playwright-cli `-s=coe` session (`steps/coe-playwright.md`). After every candidate action, record: rarity, explicit count, prefix/suffix counts, open prefix/suffix, families, fractured mods, required present yes/no.

For each candidate action, in order:

1. **State first.** Write the state block.
2. **Cheap action first.** Run the plain currency (Alchemy, Essence, Fracture, dump). Read the live handler with eval `String(Emulator.<handler>)`. Run trials via eval on item copies. Confirm the live `Emulator.emulate` signature with `String(Emulator.emulate)` before looping.
3. **Omen only if it changes a still-open choice.** Same state, with omen. Keep the omen only if it forces prefix vs suffix while both are open, forces which mod is removed while several are removable, or removes a bad outcome the cheap action still produces. If open slots already force the result, omit the omen.
4. **Family.** If the guaranteed add shares a family already on the item, dump. Do not use a removal omen as an overwrite. Family check runs before the omen.
5. **No Annul.** Setup fail = new base until the required affix is locked.

Alchemy constraint in CoE: `rarity_not_rare`, `can_be_rare`. Do not Alch a Rare.

Compare legal sequences by: (1) dump rate before the required affix is on, (2) lock hit rate (Fracture is one uniform pick among current explicits; CoE also requires rare + 4 explicits), (3) zero wasted omens.

If Perfect/Greater essence: read `poe2_essence`. Do not assume an open slot means "add without removing".

## Good example

3 prefixes + 1 suffix, no family conflict. Cheap Perfect Essence removes a random explicit (often a prefix). Dextral Crystallisation changes that to suffix-only. Omen is kept. Count stays 4.

## Bad example

"Open suffix slot, so essence adds a 5th affix, then Dextral Exaltation to guarantee suffix on a 3-prefix item." Neither claim was checked.

## Pitfalls

- Live `poe2_alchemy` may ignore rarity; UI constraint `rarity_not_rare` is the real rule.
- Crystallisation does not overwrite a conflicting family.
- Fracture miss before the required affix is locked = dump, not Annul.
- Do not add extra slams "to reach 4 explicits" if the item already has 4.
- Do not replay clicks from a previous item or session. Fresh snapshot/eval on the current item only.

If this step cannot be completed, close `-s=coe`, run `python .cursor/skills/crafting-guide/scripts/cleanup_temp.py`, and ask the user.
If this step is complete, read `steps/05-evaluate-finish.md` and execute that step end-to-end.
