# Drive Craft of Exile with playwright-cli

Use `playwright-cli` for every live CoE action. Do not use the Cursor IDE browser MCP.

Page controls and snapshot refs change with the selected base, class, and session. Treat every snapshot as one-shot. Prefer `eval` of live CoE JS (`window.coedata`, `Calculator`, `Emulator`, `methods`) over clicking. When a click is required, `snapshot` or `find` first, then use only refs from that output.

## Session

Reuse one named session across steps 2-5:

```
playwright-cli -s=coe <command>
```

If `playwright-cli` is missing: `npm install -g @playwright/cli@latest`.

If the session is gone, open the page again and wait until ready. Do not continue on a blank or wrong-game page.

Create `temp/crafting-process/.scratch/` once (PowerShell: `New-Item -ItemType Directory -Force temp/crafting-process/.scratch`). All dumps go there.

PowerShell: quote the URL. If a URL contains `&`, use `playwright-cli --%` so the query string is not split.

Write eval/snapshot dumps and long `run-code` scripts under `temp/crafting-process/.scratch/` with `--filename=`. Use `--raw` when piping or saving JSON. That folder is deleted at the end of the run. Do not write those files next to the instruction markdown.

## Open and wait

```
playwright-cli -s=coe open "https://beta.craftofexile.com/?game=poe2"
playwright-cli -s=coe run-code "async page => { await page.waitForFunction(() => window.coedata && window.Emulator); return 'ready'; }"
playwright-cli -s=coe --raw eval "() => JSON.stringify({ coedata: !!window.coedata, emulator: !!window.Emulator, calculator: typeof Calculator !== 'undefined' })"
```

Stop if that eval does not show all three as true.

## Eval vs UI

Short reads:

```
playwright-cli -s=coe --raw eval "() => <expression>"
```

Longer scripts: write `temp/crafting-process/.scratch/coe-<name>.js` as `async page => { return await page.evaluate(() => { ... }); }` then:

```
playwright-cli -s=coe --raw run-code --filename=temp/crafting-process/.scratch/coe-<name>.js
```

Look up base/mod/handler ids with `scripts/query_coe.py` and `data/currency-constraints.json`. Pass those ids into eval. Do not copy ids, method paths, or omen keys from a previous craft.

When the UI is required (search box, import, a control with no JS hook):

```
playwright-cli -s=coe snapshot
playwright-cli -s=coe find "<visible text for this item>"
playwright-cli -s=coe click <ref from that snapshot>
```

After any click, navigation, or eval that mutates the item, snapshot or eval again. Never reuse a ref from an earlier item or step.

Read live function signatures before calling them (`String(Emulator.emulate)`, `String(Emulator.<handler>)`, `String(calculatorChooseItem)`). Do not assume argument order from memory.

## Close and cleanup

Always close the session and delete scratch, including when you stop early:

```
playwright-cli -s=coe close
python .cursor/skills/crafting-guide/scripts/cleanup_temp.py
```

`cleanup_temp.py` removes `temp/crafting-process/.scratch/` and `.playwright-cli/`. It does not delete instruction `.md` files unless you pass `--keep` (step 6), which then leaves only that one file in `temp/crafting-process/`. Run it after the instruction file is written, or immediately (without `--keep`) if the run stops before that.
