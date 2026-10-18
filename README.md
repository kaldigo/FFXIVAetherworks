# Aetherworks

![Aetherworks icon](../assets/icons/aetherworks-512.png)

A modular, all-in-one Dalamud plugin for Final Fantasy XIV, bringing useful tweaks and quality-of-life tools together in one place.

Inspired by the collection-of-tools approach of Pandora's Box and Simple Tweaks.

## Project status

The plugin foundation targets Dalamud API 15 and .NET 10. Open settings with `/aetherworks` or the plugin installer's configuration button. The searchable settings window automatically lists registered tweaks by category, each with its own toggle. Choices are saved when changed and restored on plugin load. New tweaks start disabled.

The [agreed feature plan](docs/feature-plan.md) is the source of truth for planned behaviour.

**Item Location Tooltips**, disabled by default, adds ownership lines inside the game's item tooltip before its native layout. It tracks inventory, the regular saddlebag, named retainer inventories and crystals, glamour dresser pieces, and armoire ownership. It combines HQ/NQ, hides known-empty results, and shows **Missing** for storage without a snapshot. No timestamps are displayed. Dresser/armoire rows are shown only for relevant items.

Open the retainer list briefly to discover names, then each retainer's inventory to record its contents. The active retainer identity must match, all bag/crystal containers must be loaded, and repeated observations must settle before committing. Retainer equipment and sale listings are not included. Names persist with stable retainer IDs; authoritative roster refreshes update renamed retainers and remove dismissed retainers.

Open the glamour dresser to record its contents. Outfit sets expand into only their unlocked pieces, preserving original set-slot indices; set tokens do not imply ownership of locked pieces. Armoire data is recorded only once the game reports the cabinet loaded, using Cabinet sheet row IDs for the native ownership check. These collectors continue while the tooltip toggle is disabled and retain snapshots when windows close or data becomes unavailable.

The shared ownership service runs independently of the tooltip toggle. Snapshots are isolated by character, saved locally in the Dalamud plugin configuration directory under `ownership`, and retained across reloads. Incomplete container reads never replace a snapshot. Unreadable snapshot files are preserved and reported in settings.

**Glamour Ownership Tooltips**, independently disabled by default, adds appearance ownership inside the same native item tooltip. Exact matches share equipment slot, full model fields and dye channel count; close matches share the slot and base model but differ in variant, secondary model or dye channel count. Weapon categories must also agree. Applied dye colours are ignored. This is a practical guide using original game data, without material analysis or modded appearances.

Exact appearances in the dresser or armoire produce a collected-glamour summary and hide close matches. Matching gear held elsewhere is labelled separately. Close matches name the item, location and known difference. Missing snapshots remain **Missing**; known absent results remain hidden. Both tooltip toggles can be used separately or together, and snapshot collection continues with both disabled.

**Prevent Duplicate Glamour Storage**, independently disabled by default, disables duplicate entries in the dresser's creation list and the armoire's own deposit confirmation. It also checks new/existing outfit deposits before submission: an incoming duplicate blocks the whole hand-in, with a chat explanation, while pieces already in storage can still be consolidated. Exact appearances and identical items count; close appearances and gear held outside glamour storage do not. No existing items are deleted or reorganised.

Protection uses loaded dresser/armoire data when available, including only unlocked outfit pieces, and remembered ownership otherwise. Missing snapshots cannot prove a duplicate and do not block storage. Open both stores to establish ownership. Turning protection off restores controls it disabled and removes its outfit hooks; tracking remains active. The dresser list uses localized item labels to connect native rows to item IDs; ambiguous labels are disabled only when all candidates are duplicates.

**Recipe Material Locations**, independently disabled by default, marks ingredient rows inside the crafting log for the selected recipe. A + prefix means copies are held elsewhere; ? means tracking data is missing. Crystal inventory labels receive the same compact marker. Ingredients with known retainer/saddlebag stock show the quantity **elsewhere**, regardless of inventory sufficiency. Hover the material for inventory and named storage quantities inside its normal item tooltip and **Total owned**. Missing snapshots remain accessible as **Missing** rows, and incomplete totals use **Total known**. Known-empty locations are hidden. Recipe ingredients, including crystals, use actual item IDs and combined HQ/NQ counts; glamour equivalents and dresser/armoire collections are excluded. The game's available-material counts are unchanged. Indicators refresh with recipe, character, roster or quantity changes and are restored when disabled or the crafting log closes. No separate overlay window is created.

**Saddlebag Entrust Duplicates**, independently disabled by default, adds an **Entrust Duplicates** button below the regular saddlebag. It queues stackable inventory items already present there with matching HQ/NQ flags, then invokes the game's existing Entrust action. The game chooses stack placement and enforces space/restrictions. This follows [Pandora's approach](https://github.com/PunishXIV/PandorasBox/blob/master/PandorasBox/Features/UI/EntrustChocoboDuplicates.cs), without a custom destination-slot planner. Unique/nonstackable and collectable items are excluded. Slot data is read live, not from remembered ownership snapshots. The queue waits for each source stack to update and stops on unavailable actions, timeout, window/tab closure, character change, disabling the feature, or **Stop**. An already-sent action may still finish. Premium saddlebag support is not included in this first version.

**FC Chest Entrust Duplicates**, independently disabled by default, adds a button below the chest for the selected item tab only. It reuses the saddlebag queue and live slot reader, filters out untradable/bound, unique, nonstackable and collectable items, and preserves HQ/NQ stack compatibility. Following [Pandora's FC Chest Quick Deposit](https://github.com/PunishXIV/PandorasBox/blob/master/PandorasBox/Features/UI/FCChestQuickDeposit.cs), it invokes the native chest move into a matching stack with enough room for the whole source or an empty slot. It does not split stacks to fill small remaining gaps. Stacks without room are skipped while later duplicates are still checked. Changing tabs, closing the chest, disabling the feature or pressing Stop cancels remaining work. Permissions and chest locks are enforced by the game; an unconfirmed move stops the queue. A move already sent can still finish. Gil/crystal tabs are excluded.

**Show FC Chest All-Tabs Deposit Button**, separately disabled by default, adds **Entrust Duplicates — All Tabs** alongside the current-tab button when FC Chest Entrust Duplicates is enabled. It visits visible, enabled item tabs in order (1–5), waits for the selected tab to be loaded with stable contents for one second, and uses live inventory only. Whole stacks without room are skipped; subsequent tabs can still accept them if they contain a compatible duplicate. Tab selection follows [ECommons FreeCompanyChest](https://github.com/NightmareXIV/ECommons/blob/master/ECommons/UIHelpers/AddonMasterImplementations/FreeCompanyChest.cs) and its registered radio-button events. Manual tab changes, closing the chest, character changes, Stop, or disabling either option cancel the run. View-only/no-access tabs are skipped before any deposit; Full Access and Deposit Only tabs are eligible. Permissions are checked again before each move. Unknown permission data stops the run with a message to open the FC member list and rank details. Rejected/unconfirmed moves still stop the entire run. The last processed tab remains selected. Native tab selection, loading and permissions still require in-game validation.

**Include FC Chest**, disabled by default, includes remembered FC chest item-tab quantities in item ownership tooltips and recipe-material breakdowns/totals. Open each tab briefly to record it: only the visible, loaded tab is read, and repeated observations must settle before its snapshot is replaced. Unopened tabs show **Missing**, observed-empty tabs are hidden, and crafting totals remain **Total known** while relevant snapshots are absent. FC contents are shared stock, not a promise that the character can withdraw them. Other members can change contents while the chest is closed; reopening a tab refreshes it.

Snapshots remain in the character's existing ownership file and use company ID plus tab as their key. An unavailable company identity shows **Missing**, never a previous company's quantities. Collection continues with inclusion and tooltip toggles off; disabling inclusion hides FC results without deleting snapshots. Deposit actions continue using live data, independently of this option. This covers the five item tabs; gil and the separate crystal compartment are excluded.

Four independent, default-off action toggles now implement the selected [ReAction behaviours](https://github.com/UnknownX7/ReAction/tree/9436e0c72c569b5518c67bda2e29f44822c68ea0/Modules):

- **Auto Dismount**: when a mounted eligible action is unavailable, issue Dismount and retry the requested action once after dismounting if no more than two seconds elapsed. Normal mount-category actions, Teleport and Return are excluded; general Limit Break and Sprint are included. Replay uses the original action/target/mode/route with the extra parameter reset to zero, matching ReAction.
- **Auto Cast Cancel**: cancel a supported, non-ground-targeted regular cast when its existing target can no longer receive the action. An unresolved/disappeared target alone does not trigger cancellation. Cancellation fires once until the cast state clears.
- **Auto Target**: for supported hostile regular actions without a usable target, choose the nearest living attackable player/battle NPC at strictly less than 30 yalms, inside a camera-facing 126-degree cone. Distance ties retain object-table order. Ground-targeted actions and explicit alternative targets are preserved.
- **Auto Change Target**: permits that targeting behaviour to replace an unsuitable selected target. It has its own saved toggle but requires Auto Target; enabling it alone does nothing.

The action-sheet filters, mount exceptions, target validation, camera/look-back handling and timing follow ReAction revision `9436e0c72c569b5518c67bda2e29f44822c68ea0`, with native API adapters for this plugin. Other ReAction features (action stacks, macro queueing and related patches) are not included. Hooks and update subscriptions exist only while the relevant toggles are enabled. Disabling dismount clears its pending replay; disabling cast cancel clears its one-cast guard. Character changes cannot replay another character's queued action.

All planned feature toggles are implemented. In-game acceptance is pending; automated checks do not verify game-memory readiness, native hook execution or UI behaviour. The single-item protection covers the normal game UI, not external plugins that submit storage requests directly.

## Starting points

- Verify item hover placement at different UI scales; compare inventory totals for HQ/NQ stacks.
- Before opening the saddlebag, confirm Missing; open it, move items, close it, and confirm remembered counts.
- Disable tooltips, change inventory, then enable them and confirm collection continued.
- Reload the plugin and switch characters: confirm saved contents return only for the correct character.
- Open the retainer list: unopened retainers should show Missing. Visit two retainers with distinct items and switch quickly between them; verify names and quantities never cross over. Change stacks and reload the plugin to verify persistence.
- Check full and partial dresser outfits, including an outfit with empty columns: only actual stored pieces should appear. Add/remove pieces and confirm ownership updates without relying on occupied-slot count. Zone away and confirm snapshots remain.
- Before loading the armoire it should show Missing for eligible items; after opening it, verify present/absent items against the game. Check additions/removals and known-empty behaviour.
- Confirm ordinary materials do not acquire irrelevant dresser/armoire Missing rows. Confirm snapshot collection continues with tooltips disabled.
- Enable each tooltip toggle separately, then together: confirm a single panel, no repeated hovered-item location, and no glamour rows for materials.
- Compare identical models, variants and one/two-channel versions, including differently dyed copies. Verify exact collected appearances suppress close rows; matching retainer gear is labelled as held rather than collected. Check an alternate appearance stored in the armoire even when the hovered item itself is not eligible.
- Enable protection with both tooltip features off: same-item and exact-alternative dresser rows should be disabled, including matches in partial outfits or the armoire. Close matches and items owned only by retainers should remain selectable. Test mouse and controller input, scrolling, filtering, and HQ names.
- Test an exact dresser appearance against an armoire deposit: only that deposit's Yes button should be disabled; withdrawals and unrelated confirmations must work. Disable protection with a dialog open and verify normal controls return.
- Test new and existing partial outfits: missing pieces are allowed, an incoming exact duplicate rejects the entire hand-in, and consolidation of already-stored pieces still succeeds. Verify rapid deposits/withdrawals, closing/reopening windows, and toggle/unload restoration. These native paths require in-game validation before relying on protection.
- Enable Recipe Material Locations with tooltip features disabled. Select a recipe with ample inventory and additional materials on a retainer/saddlebag: the outside quantity must still show. Hover for the per-location breakdown; check crystals, combined HQ/NQ stacks, unknown retainers, and known-empty storage. Switch recipes through categories and search, change quantities, close/reopen the log, and test panel placement/scrolling at different UI scales. Toggle it off and verify the panel disappears while tracking continues.
- Test Entrust Duplicates with several inventory stacks, matching HQ/NQ copies, a nearly full destination stack, a full stack plus empty slots, and a completely full saddlebag. Verify only matching stackable items move, the native Entrust action is correctly selected, and unavailable/full-bag cases stop. Check cancellation, closing the bag, switching its tab, and disabling the toggle mid-queue. Verify coexistence with plugins adding inventory context-menu entries; the feature must stop if it cannot safely identify Entrust.
- Test FC Entrust Duplicates on each of the five item tabs with different duplicate items per tab. Confirm deposits never touch another tab, HQ/NQ remain separate, untradable items stay in inventory, and stacks without room are skipped while later fitting stacks still move. Test missing deposit permissions, chest locks, Stop, tab changes and closing the chest mid-queue. Confirm no button appears on gil/crystal views and unknown UI layouts produce no transfers. Recheck saddlebag entrust after the shared live-reader extraction.
- Enable Include FC Chest and open one item tab at a time: verify only visited tabs gain quantities, other tabs stay Missing, and rapid switching does not record one tab under another. Compare combined HQ/NQ totals in tooltips and recipe hover details, including recipes already stocked in inventory. Close the chest, reload, and confirm remembered contents remain. Empty a tab and reopen it to remove old quantities. Toggle inclusion while a tooltip/recipe is open and verify immediate refresh. Test character/company changes and inaccessible tabs; unavailable reads must retain prior snapshots. Collection should continue with the inclusion option off.

- All-tabs FC deposits: confirm the option defaults off and the current-tab button remains available. Enable both settings and test duplicates spread across tabs 1–5, full/empty tabs, HQ/NQ, hidden/disabled and view-only tabs, and slow tab loads. Confirm only whole matching stacks move, counts reflect confirmed moves, and no stale snapshot drives a deposit. Test Stop, manual tab changes, closing the chest, and disabling either setting during loading and during a move; no further request should be sent. Compare the last selected tab and its contents with the game UI.

Storage integration references: [FFXIVClientStructs outfit functions](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/MirageManager.cs), [armoire agent](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentCabinet.cs), and [Pandora's dresser-list approach](https://github.com/PunishXIV/PandorasBox/blob/master/PandorasBox/Features/UI/DisableKnownGlam.cs). Aetherworks uses its own shared exact matching and tracks UI changes for restoration.

## Build and check

For action-feature acceptance, compare against ReAction one plugin at a time with matching toggles. Check mounted combat actions, Sprint/Limit Break, Teleport/Return and mount skills; test dismounts taking longer than two seconds and disabling with a replay pending. Check target death and invalidation during casts, ground spells, and vanished targets. Test Auto Target with no target, a valid target, an unsuitable target with Auto Change Target off/on, explicit macro targets, equal-distance enemies, the 30-yalm boundary, and first-person/look-back camera modes. Confirm that Auto Change Target alone is inert and that disabling/unloading removes the hooks. Native signature resolution and camera layout still require live verification.

Requires the .NET 10 SDK and Dalamud API 15 development assemblies in the standard XIVLauncher location (or `DALAMUD_HOME`). From this directory:

```powershell
dotnet build Aetherworks/Aetherworks.csproj -c Release
dotnet run --project tests/Aetherworks.Tests.csproj -c Release
```

For local testing, add the built `Aetherworks/bin/Release/Aetherworks.dll` to Dalamud's development plugin locations. Version 0.1.0 is an initial testing preview in the shared HotbarTools repository. See docs/first-test.md for settings and ownership checks.

## Adding a tweak

1. Implement `ITweak` in `Aetherworks/Features`, with a stable, unique ID, display name, description, and category.
2. Keep the constructor inert. Subscribe to events, install hooks, and apply behavior only in `Enable()`.
3. In `Disable()`, unsubscribe, release resources, and undo reversible changes. Cleanup must support repeated calls and a partially failed enable. The tweak owns its resources and any runtime callback error handling.
4. Register the instance in `Plugin`'s `registeredTweaks` array. The manager and window provide its toggle and persistence automatically. All user-facing feature behaviour goes through this lifecycle. Shared foundational services such as ownership tracking are owned by the plugin and continue independently of feature toggles.
5. Add lifecycle tests for feature-specific behavior and verify on/off transitions in game.

The manager isolates enable/disable failures and logs details. Failed enables are cleaned up and saved as disabled. Failed cleanup remains visibly flagged and blocks re-enabling until cleanup succeeds. Unloading cleans up active tweaks in reverse order without clearing saved choices. Save failures appear in the window with a retry action.

## Workspace

This directory is the home for Aetherworks source, documentation, and project assets.




FC permission research: Pandora, ECommons and Daily Routines chest examples do not supply a deposit-permission filter. The adapter combines the current-character member-rank lookup and 16-rank table layout from [FFXIVClientStructs.Expansion](https://github.com/AtmoOmen/FFXIVClientStructs.Expansion/blob/b1ccab64986bdd4aa6e9f1837994d088f5ac20b5/FFXIVClientStructs.Expansion/FFXIV/Client/UI/Info/InfoProxyFreeCompany.cs) with the permission-byte definitions in [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Info/InfoProxyFreeCompany.cs). Deposit-only bits are decoded explicitly, including tab 5. The extension's rank-table offset differs from the SDK's older field, so this native read needs live validation; pure tests validate bit decoding, not game memory. Unknown, missing or contradictory data never grants access. Test a view-only tab between writable tabs, deposit-only access on every tab, permissions changing during a run, and unloaded member/rank data.


Version 0.1.1 replaces both ownership/crafting overlays with native UI integration. Tooltip strings are extended at the pre-layout hook used by Allagan Tools/CriticalCommonLib, preserving original payloads. Crafting markers use the native RecipeNote ingredient nodes. Validate wrapping, hover details, recipe switching and toggle/unload cleanup in game, including coexistence with Allagan tooltips.


Version 0.1.2 records visible FC chest item tabs after a stable observation while the chest is open, independently of deposit rights and feature toggles. View-only tabs remain included. Unavailable tabs are excluded from tooltips and crafting totals; their old contents are retained without being counted. Available tabs without contents snapshots show Missing. Before availability is recorded, including after upgrading an older snapshot, one FC Chest: Missing line is shown instead of assuming five tabs. Open an item tab for a few seconds after updating, then close the chest and reload to check persistence. For a four-tab FC, Tab 5 should never appear once availability has been recorded. Validate view-only tabs, newly unlocked tabs and switching FCs in game.

Version 0.1.3 removes the FC chest and saddlebag ImGui panels entirely. Game-textured buttons attach directly to the storage addons using KamiToolKit, pinned to the same revision used by Artisan (`993e153458f976bc7352f1c74ea769914bfd2706`). The existing button becomes Stop during a transfer, the optional all-tabs button remains separate, and status appears in native button hover tooltips. Nodes are disposed when the storage window closes or the feature is disabled. No large background panel is drawn over item slots. In-game checks: open/close repeatedly, switch tabs, toggle the all-tabs option and main feature while open, move/scale the game window, verify button hover/status and Stop, and confirm there are no duplicate controls after plugin reload. Native styling and click behavior still need live validation.

Version 0.1.4 fixes the confirmed “Not on main thread!” startup failure by dispatching native button setup, disposal and library cleanup to the framework thread. Failed startup in 0.1.3 may have switched the two deposit features off; re-enable them after updating. Verify both buttons after plugin reload and after disabling/re-enabling their features with the storage windows open.

Developers: initialize the pinned dependency with `git submodule update --init --recursive`. Release ZIPs include KamiToolKit.dll, its assets and its MIT license; installing Artisan is not required. Reference: https://github.com/PunishXIV/Artisan/blob/main/Artisan/UI/KTK/NativeCraftAll.cs
