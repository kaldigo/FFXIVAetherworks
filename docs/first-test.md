# First in-game test: settings and ownership

Install Aetherworks from the existing HotbarTools repository in `/xlplugins`, then open `/aetherworks`. Refresh/reopen the installer if needed. Disable any development copy before enabling the repository copy.

1. Open settings through the command and installer configuration button. All 12 switches start off on a fresh configuration. Search, clear search, resize and scroll. Enable only **Item Location Tooltips**, close/reopen settings and check the choice remains saved. Check for tracking/saving errors.
2. Hover a material. Inventory combines HQ/NQ; unrecorded storage shows **Missing**; known-empty locations are hidden. No timestamps or irrelevant glamour storage rows should appear.
3. Open the regular saddlebag for a few seconds. Check a stored item's count, move some manually and check again. Close it: the remembered count should remain.
4. Open the retainer list, then each retainer inventory for a few seconds. Check names/counts using an item held by two retainers. Discovered but unvisited retainers show Missing.
5. Open the dresser and armoire. Hover known stored gear. For a partial outfit, only stored pieces should count.
6. Turn tooltips off, change quantities with storage open, wait a few seconds, then turn them on. Collection should have continued.
7. Wait at least five seconds, disable/re-enable the plugin, and check the saved toggle and remembered closed-storage quantities. Another character must not inherit these snapshots.
8. Optionally enable **Include FC Chest**, leaving deposit features off. Visit each item tab for a few seconds and compare counts. Unopened tabs show Missing. Close the chest and confirm remembered results; turn inclusion off and confirm FC lines disappear.

Armoury chest, equipped gear, premium saddlebag, retainer equipment/sale listings and FC crystal/gil compartments are not covered. Action conveniences, deposits and glamour protection are separate later test passes.

Report the failing step, item, location, expected/displayed counts and any settings/log error. Start by confirming installation and settings before continuing through storage checks.

Version 0.1.2 records visible FC chest item tabs after a stable observation while the chest is open, independently of deposit rights and feature toggles. View-only tabs remain included. Unavailable tabs are excluded from tooltips and crafting totals; their old contents are retained without being counted. Available tabs without contents snapshots show Missing. Before availability is recorded, including after upgrading an older snapshot, one FC Chest: Missing line is shown instead of assuming five tabs. Open an item tab for a few seconds after updating, then close the chest and reload to check persistence. For a four-tab FC, Tab 5 should never appear once availability has been recorded. Validate view-only tabs, newly unlocked tabs and switching FCs in game.
