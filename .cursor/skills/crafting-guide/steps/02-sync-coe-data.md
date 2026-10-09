# Step 2 — Sync Craft of Exile data

Read this file fully. Execute this step end-to-end now. Do not read the next step until this one is done.

## Definition

Make local CoE data current and open the live PoE2 tool so later steps can query and emulate.

## Instruction

1. Run from the GameHelper2 repo root:
   `python .cursor/skills/crafting-guide/scripts/fetch_coe_data.py`
2. Confirm `data/poe2/<patch>/data.json` and `english.json` exist. Record the patch folder name.
3. Read `steps/coe-playwright.md`. Open CoE with playwright-cli session `-s=coe` and wait until eval shows `window.coedata`, `window.Emulator`, and `Calculator`. Keep that session for steps 3-5.
4. If fetch fails, keep using the checked-in `data/` copy and say so. If the live page fails, **stop and ask** — you cannot evaluate crafts without CoE.
5. Local `data/` is for lookup (bases, mods, families, essence maps, currency constraints). Live CoE is for handlers and trials.

## Good example

Fetch reports patch `4.5.5.3`. playwright-cli `-s=coe` eval shows `coedata` and `Emulator`. Agent records both and continues.

## Bad example

Agent skips fetch, uses last week's memory of weights, and never opens CoE.

## Pitfalls

- `data.json` is `coedata={...}` (JS assignment), not raw JSON. Use `scripts/query_coe.py`, do not `json.load` it blindly.
- Live emulator may apply a craft that CoE UI constraints would block. Always read the method `constraints` as well as running the handler.
- Do not treat `prices.json` as required for mechanics.
- Drive CoE with playwright-cli session `-s=coe`. Do not use the Cursor IDE browser MCP.

If this step cannot be completed, close `-s=coe`, run `python .cursor/skills/crafting-guide/scripts/cleanup_temp.py`, and ask the user.
If this step is complete, read `steps/03-validate-base-and-affixes.md` and execute that step end-to-end.
