# Craft of Exile local data

Source: `https://beta.craftofexile.com`

| File | Role |
|---|---|
| `settings.json` | CoE UI settings dump |
| `poe2/<patch>/data.json` | `coedata` (bases, mods, groups, essences, methods) |
| `poe2/<patch>/english.json` | `coelang` string table |
| `currency-constraints.json` | Flattened currency/omen constraints extracted from `methods.crafting` |

Refresh:

```
python .cursor/skills/crafting-guide/scripts/fetch_coe_data.py
```

`data.json` / `english.json` are JS assignments (`coedata=...`, `coelang=...`). Use `query_coe.py` to read them.
