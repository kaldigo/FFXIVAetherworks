# Aetherworks feature plan

Agreed plan, saved 2026-10-18. Implementation has begun; this document defines the target behaviour, not a claim that every feature is available. See the project README for current implementation status. Shared collectors now cover inventory, regular saddlebag, named retainer bags/crystals, dresser (including partial outfits), and armoire. Simple exact/close matching and the independent Glamour Ownership Tooltips toggle are implemented; in-game validation remains pending.

Aetherworks provides individually toggleable conveniences. All feature toggles start disabled. ReAction is the replacement target; Dispeller, Pandora's Box and Penumbra are research references.

## Individual toggles

| Toggle | Behaviour |
|---|---|
| Auto Dismount | Reproduce ReAction's behaviour: dismount before using an action. |
| Auto Cast Cancel | Reproduce ReAction's behaviour: cancel supported casts when the existing target can no longer receive the action; exclude ground targets. |
| Auto Target | Reproduce ReAction's targeting when attempting a targeted attack without a target. |
| Auto Change Target | Reproduce replacement of an unsuitable target; preserve its dependency on Auto Target initially. |
| Item Location Tooltips | Show quantities and locations of the hovered item, for all item types. |
| Glamour Ownership Tooltips | Show exact and close appearance matches for applicable gear. |
| Prevent Duplicate Glamour Storage | Block items whose exact appearance is already collected in the dresser or armoire. |
| Recipe Material Locations | Indicate recipe ingredients owned outside inventory and show quantities by location. |
| Saddlebag Entrust Duplicates | Add a button to deposit eligible duplicate items into the saddlebag. |
| FC Chest Entrust Duplicates | Add a button to deposit eligible duplicate items into the current FC chest tab. |

Additional options: **Include FC Chest** in tracking/results, and **Show All-Tabs Deposit Button**, which visits accessible item tabs sequentially (implemented; in-game validation pending).

## Shared ownership store

- Current character and their retainers only.
- Required locations: inventory, saddlebag, retainers, glamour dresser and armoire.
- Continue collecting while Aetherworks runs, independently of display feature toggles.
- Retain snapshots between sessions; refresh when storage data becomes available.
- Combine HQ and NQ in ownership quantities and totals. Preserve distinctions required by the game for moving/stacking.
- FC chest inclusion is optional.
- **Include FC Chest** is implemented as a separate, default-off option for item tooltips and crafting material results. Shared tracking remembers each opened item tab independently after loaded contents settle. Snapshots are scoped to character/company/tab; unopened tabs are Missing and known-empty tabs are hidden. The five item tabs are covered; gil and the separate crystal compartment are excluded. In-game collection and tab-readiness validation remain pending.
- Proposed additions still to confirm: armoury chest, equipped gear, premium saddlebag, retainer equipment and sale listings.

Show **Missing** when a location has no snapshot. Hide locations with a snapshot but no matching item. Show no timestamps. If nothing matches and no relevant snapshots are missing, add nothing. Missing means missing tracking data, not a missing item.

## Simple glamour matching

Original game item data only; no material analysis, texture inspection or dye simulation.

| Classification | Rule |
|---|---|
| Exact | Same equipment slot, full model identifiers, variant and dye-channel count. |
| Close | Same base model and slot but different variant or dye-channel count. Additional model-component differences also prevent an exact match. |
| None | Different base model or incompatible slot. |

Ignore applied dye colours. Different names/IDs can be exact matches. Some recolours that could look identical after dyeing remain close because variants differ: this is an accepted limitation. Penumbra is a reference for interpreting fields, not a ready-made equivalence system.

## Glamour ownership and protection

Collected means stored in the **dresser or armoire**. Flag equivalent gear owned elsewhere separately, to avoid both duplicate purchases and duplicate storage.

- Block the same item or an exact appearance already collected.
- Do not block close matches or provide an intentional-duplicate exception.
- Do not automatically delete or reorganise existing duplicates.
- Count only actually stored outfit pieces, never all pieces of a partial outfit.
- Include outfit pieces in matching/protection; allow genuinely missing appearances to be added.
- Preserve consolidation into outfits: reorganising existing storage is not a duplicate deposit. Verify the outfit UI workflow in game.

Implementation status: the independent protection toggle now covers the dresser creation list, armoire deposit confirmation and native new/existing outfit hand-ins. It uses loaded ownership when available and snapshots otherwise; missing data does not establish a duplicate. In-game validation of UI selection, outfit consolidation and toggle cleanup remains pending.

## Item tooltip presentation

Readable individual location lines; no technical model details or timestamps.

> **Glamour:** Exact match — Glamour Dresser  
> **Inventory:** 2  
> **Saddlebag:** 3  
> **Retainer Alice:** 1  
> **Retainer Bob:** Missing

Close matches show alternative name, location and a short difference:

> **Close match:** Alternative Item — Retainer Alice · 1 dye channel instead of 2

Use **Different variant** rather than inventing a visual explanation. Proposed default: suppress close alternatives when an exact collected match exists. Final grouping/order can be settled during UI review.

## Recipe material locations

Show an ingredient indicator whenever copies are known outside inventory, even if inventory has enough for one craft. Support batch planning.

> **Iron Ore**  
> **Inventory:** 12  
> **Retainer Alice:** 30  
> **Saddlebag:** 8  
> **Total owned:** 50

- Actual ingredient matches only, never glamour equivalents.
- Combine HQ/NQ; keep the game's normal available-inventory count unchanged.
- Hide known-empty locations; show Missing for absent snapshots.
- Use **Total known** when relevant snapshots are missing.
- Missing-data information remains accessible when no outside copies have been found.

Implementation: **Recipe Material Locations** now marks native ingredient rows with **+** for outside stock or **?** for missing tracking data. Hover the material for quantities by location, Missing values and the total inside its native item tooltip. There are no ownership or crafting overlay windows. It includes crystal ingredients and remains independent of the tooltip toggles. In-game layout and recipe-selection validation are pending.

## Duplicate transfers

Explicit button actions: find stackable items already present at the destination and move all eligible inventory quantities, subject to capacity/game restrictions. Use current destination data and exact stack compatibility, never appearance matching. No keep-back quantities or custom sorting rules initially.

Implementation: the regular-saddlebag button and independent toggle now queue the game's native Entrust action, following Pandora's approach. The game handles stack placement; Aetherworks rechecks source slots and waits for updates between actions. In-game action routing, full-bag behaviour and UI validation remain pending. Premium saddlebag support is deferred.

FC chest buttons: **Entrust Duplicates** for the current item tab, and optional **Entrust Duplicates — All Tabs**. Select permitted tabs, wait for contents, process and continue. Source research supports the approach; if in-game testing finds it unreliable, retain current-tab support only.

Implementation: **FC Chest Entrust Duplicates** now has its own toggle and current-item-tab button. It uses Pandora's native chest-move approach and the shared sequential queue. Whole stacks go into a compatible stack with sufficient room or an empty slot; automated splitting is excluded. Manual tab changes cancel the run. The separate, default-off **Show FC Chest All-Tabs Deposit Button** option now adds the all-tabs button while retaining the current-tab button. It visits visible/enabled item tabs in order 1–5 using the ECommons radio-button event approach, waits for loaded contents to settle, and reuses the existing live transfer queue. Whole stacks without room are skipped so later tabs can accept them. No ownership snapshot is used for deposits. Disabling either setting cancels all-tabs work. View-only/no-access tabs are skipped using current-character FC rank permissions; Full Access and Deposit Only are accepted. Unknown permission data stops with guidance to load FC member/rank details. An unconfirmed/rejected move still stops the run rather than retrying or moving on. Current-tab deposits now also skip unsent stacks without room and continue checking later duplicates. Native tab identification, loading, permission failures and move behaviour require in-game validation.

## ReAction replacement

Initially reproduce the four selected features faithfully, including existing dependencies/exceptions. Adapt later. Verify behaviour alongside the user's existing plugins before replacing ReAction.

Implementation: Auto Dismount, Auto Cast Cancel, Auto Target and Auto Change Target now have separate default-off toggles. Behaviour was checked against ReAction revision `9436e0c72c569b5518c67bda2e29f44822c68ea0`. Auto Change Target remains dependent on Auto Target. Dismount retains the two-second replay limit and mount/Teleport/Return exclusions. Targeting retains the nearest attackable living target within the 126-degree camera-facing cone and strictly under 30 yalms, while preserving explicit alternative targets. Cast cancellation tests action-target validity, not just HP, and does nothing when the cast target cannot be resolved. Native in-game parity validation remains pending; other ReAction functionality is outside this implementation.

## Remaining details and validation

- Confirm additional storage categories listed above.
- Validate matching on armour, accessories, recolours and multi-part weapons.
- Verify partial-outfit ownership and consolidation.
- Verify FC loading, permissions, capacity and transfer completion.
- All-tabs destination order is implemented as tabs 1–5; verify it in game.
- Transfers currently use ordinary inventory bags and item destinations only; separate crystal compartments are excluded. Crystal ownership is tracked in player/retainer inventories. Any crystal-transfer support is additional scope.
- Review tooltip ordering and crafting Missing presentation.
- Verify the selected ReAction behaviours in game.

No material-level appearance analysis or automatic cleanup of existing glamour duplicates is planned.


## Implementation review — 2026-10-18

All ten feature toggles and both additional options are registered. Settings, saved toggle state, shared ownership snapshots, simple matching, tooltip presentation, duplicate protection, recipe material locations, transfers and the four action conveniences have implementations. This is implementation coverage, not in-game acceptance. Release build succeeds and 157 automated checks pass; NuGet vulnerability metadata is unavailable (NU1900).

The two FC deposit gaps identified in this review have now been addressed (193 automated checks pass):

- Current-tab deposits share the all-tabs capacity check: skip unsent stacks without room and continue with later duplicates.
- All-tabs checks FC rank permissions before starting each selected tab and again before each move. View-only/no-access tabs are skipped; Full Access and Deposit Only are allowed. Unknown member/rank data stops the run. Permission layout and freshness still need in-game validation; see README research notes. No request is retried or ignored after an unconfirmed native move.
No other major planned feature is absent. Armoury chest, equipped gear, premium saddlebag, retainer equipment and sale listings remain optional/unconfirmed extensions, not included in current ownership coverage. All item types refers to supported storage contents, not every possible storage category.

First in-game pass should cover settings and shared tracking, then tooltips/matching and crafting, then glamour protection and transfers, and finally ReAction parity with only one plugin providing those action behaviours at a time. Native signatures, UI events, container freshness, outfit consolidation, controller input and UI scaling cannot be established by the pure automated checks. The initial review changed documentation only; its two transfer follow-ups are now implemented.



Version 0.1.2 records visible FC chest item tabs after a stable observation while the chest is open, independently of deposit rights and feature toggles. View-only tabs remain included. Unavailable tabs are excluded from tooltips and crafting totals; their old contents are retained without being counted. Available tabs without contents snapshots show Missing. Before availability is recorded, including after upgrading an older snapshot, one FC Chest: Missing line is shown instead of assuming five tabs. Open an item tab for a few seconds after updating, then close the chest and reload to check persistence. For a four-tab FC, Tab 5 should never appear once availability has been recorded. Validate view-only tabs, newly unlocked tabs and switching FCs in game.
