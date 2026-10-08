# 999 Challenge Tracker

A SMAPI mod that shows live, in-game progress toward a "999 of everything" challenge: one checklist row per
item, with a live Have count (summed across your inventory and every chest, fridge, Auto-Grabber and Junimo
chest on the farm), the item's own true cross-sheet **Required** target (not just a flat 999 - crafting
ingredients like Wood or Ancient Fruit need extra to cover everything made from them elsewhere on the
checklist), a live **Total** progress mirror, and a Totals dashboard rolling all of that up per category and
for the whole challenge.

Press **U** (configurable via Generic Mod Config Menu, if installed) during gameplay to open the tracker.

## How it works

The mod ships with a bundled, pre-extracted snapshot of the challenge's item list and requirement formulas at
`assets/checklist999.json` - it never reads a spreadsheet at runtime, so it works for anyone who installs it,
not just the person who built the original checklist. Everything else (your live Have counts, the Total
progress, completion state) is computed fresh from your save every time you open the tracker.

## Updating the checklist data

If you rebalance the underlying "Stardew Valley 999 Challenge" spreadsheet later (different items, different
targets, different crafting ratios), regenerate the bundled JSON instead of changing the mod's code:

1. Run `tools/generate_checklist_json.py` (dev-only - not included in the release zip) against your `.ods`
   checklist to produce a fresh `checklist999.json`.
2. Replace `Challenge999Tracker/assets/checklist999.json` with the new file.
3. Rebuild the mod.

The mod itself has no code that knows about spreadsheet formulas or file formats - it only ever reads the
already-resolved JSON.

## Credits

The collection-board UI (tabbed categories, grid/hover presentation, All/Incomplete/Complete visibility
toggle) was structurally adapted from Peter Carpenter's MIT-licensed "Qi's Perfection Tracker" - see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
