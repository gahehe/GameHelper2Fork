# Step 1 — Clarify requirements

Read this file fully. Execute this step end-to-end now. Do not read the next step until this one is done.

## Definition

Turn the user's goal (screenshot, paste, or affix list) into a complete requirement set. Incomplete requirements are not a craft.

## Instruction

1. Read the user message and any image. Extract base name, item class, item level, rarity, and every affix line you can see.
2. Classify each affix as **required** or **optional**. If the user did not say, ask.
3. For every required affix, get the **minimum tier** (or exact value range). If missing, ask.
4. Get the **base type**. If missing, ask. Do not assume a base from the screenshot's implicit unless the user confirms.
5. Optional: budget, lock method (fracture vs not), whether a near-match is acceptable.
6. Write a requirement block:
   - base
   - required affixes with min tier
   - optional affixes with min tier
7. If any of base / required list / min tiers is missing, **stop and ask**. Do not continue.

## Good example

User sends a staff screenshot. Agent: "Required is +Level of all Spell Skills. Confirm: is that T1 only, or is essence +5 enough? Which of Cast Speed / Spell Crit / Extra Cold are required vs optional, and min tier for each? Base is Gelid Staff — confirm?"

## Bad example

User sends a screenshot. Agent immediately writes a 6-mod crafting tree, treating every line on the screenshot as required T1.

## Pitfalls

- Do not invent min item level, sockets, or quality. Those come from CoE in later steps.
- Do not treat "nice to have" as required.
- Do not skip asking because the screenshot "looks complete".
- Two gem-level families may be mutually exclusive. Do not promise both until step 3 checks families.

If this step cannot be completed, stop and ask the user.
If this step is complete, read `steps/02-sync-coe-data.md` and execute that step end-to-end.
