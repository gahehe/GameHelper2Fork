---
name: crafting-guide
description: Calculates an optimal Path of Exile 2 crafting process from a goal item using Craft of Exile. Use when the user wants a crafting guide, crafting process, CoE simulation, or provides an item screenshot or affix list to craft toward.
---

# Crafting Guide

When invoking this skill, user will provide the goal item. It can be an image of the item or list of affixes to have on the item. You have to ask user questions to clear out the item requirement if not already provided, about which affixes are required to have, the minimum tiers for each affixes etc...
After that you use Craft Of Exile online tool to calculate.

**Do this now:** read `steps/01-clarify-requirements.md` and execute that step end-to-end. Do not skip ahead.

## Rules

- Craft of Exile PoE2 (`https://beta.craftofexile.com/?game=poe2`) is the verifier. Drive it with playwright-cli (`steps/coe-playwright.md`). Local `data/` is lookup only. If CoE was not run for a claim, do not write that claim.
- Family / cannot-coexist is checked before any remove-then-add omen. Same-family before the required mod is on = dump the base. Do not Annul during setup.
- Do not recommend an omen that does not change the outcomes at the current open-slot state.
- Do not invent item requirements. If CoE has no constraint for it, omit it.
- Human output: prerequisites with happy-path quantities, then steps a person can follow. Put the dangerous "do not do X yet" line before the tempting action.
- The only leftover file is the instruction markdown. Scratch dumps, process JSON, eval logs, and `.playwright-cli/` are deleted (see `steps/06-emit-process.md`).

## Output

Write one file: `temp/crafting-process/<base-slug>.md` via `scripts/render_process.py`. Paste that same markdown in chat. Do not write `eval-log.md`, `EVALUATE.md`, or any second markdown.

## Steps

1. `steps/01-clarify-requirements.md`
2. `steps/02-sync-coe-data.md`
3. `steps/03-validate-base-and-affixes.md`
4. `steps/04-evaluate-setup-and-lock.md`
5. `steps/05-evaluate-finish.md`
6. `steps/06-emit-process.md`
