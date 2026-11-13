# Aetherworks: in-game acceptance

These checks require a live game session; automated logic tests do not validate native hooks or rendering.

## Settings and Return via Teleport

1. Upgrade from an older Aetherworks configuration. Open `/aetherworks`. Confirm Return via Teleport under Actions, Shared Hotbar Sync under Hotbars, and Saddlebag Withdraw All but One under Items and storage. New features start off; paid travel asks for confirmation by default. Previously saved choices remain unchanged. No old storage buttons, tooltip additions or background tracking should appear.
2. Check Overview, Features, and Status & recovery in the fixed icon rail. Hover and keyboard/controller-focus each rail button; tooltips must name the destination without shifting it. Search Features for Return, Teleport and Actions. Open a result, then Back: the search and originating workspace must return. Open settings from Overview or Status and verify Back returns there. Check 780 × 610 and 360 × 320 logical windows at 100% and 150% UI scale; only the content should scroll. Keep controls and the close button reachable, drag the header, and resize the window. Enable/disable a feature, close/reopen settings and reload the plugin to verify saved choices. No global toggle should appear.
3. With the feature off, verify normal Return, including its native cooldown error. With it on and Return ready, verify free Return, its normal cast/confirmation and normal cooldown afterward.
4. During Return cooldown, press the normal Return hotbar button (keyboard and cross hotbar), and test the Return action from the action list and a Return macro. Confirm the home destination and quoted fare match the game's Teleport list.
5. With Ask before paid travel on, verify no travel or gil change before confirming. In the styled dialog, check the full destination and fare at 100%/150% UI scale and both short and long destination names. Confirm once and check the destination and charged fare. Cancel, close the dialog and press Escape; none should start travel. Repeated confirmation clicks must start at most one trip.
6. Turn Ask before paid travel off. Press Return during cooldown: it should start paid Teleport without the Aetherworks prompt. Turn the setting on and verify confirmation returns. Reload and verify persistence. Check feature and confirmation switches using mouse and keyboard/controller activation, including after upgrading an existing skip-confirmation preference.
7. Test normal discounts, a zero-cost destination, insufficient gil, and each relevant ticket setting with and without tickets. Native ticket prompts/settings must behave normally; the plugin's displayed fare is before any ticket use.
8. Test combat, death, duties, cutscenes, zoning, existing casts and animation lock. No restricted Teleport should start and no request should be retried later.
9. With a confirmation open, wait 30 seconds, change territory, change home point, log out/change character, disable the feature, or change its confirmation setting. The old authorization must no longer work. If Return finishes its cooldown while the confirmation is open, confirming must use free Return.
10. Disable/unload/reload repeatedly, including with a confirmation open. Verify hooks stop, no pending trip survives, normal Return works, and unrelated actions are unaffected. Test alongside other enabled action plugins.

## Saddlebag Withdraw All but One

1. Enable the feature under Items and storage. Right-click stacks of 1, 2 and 99: no extra action for 1; Withdraw all but one should move 1 and 98 respectively for the others, leaving one in the original slot. Check both regular saddlebag pages and premium tabs if available.
2. Test HQ and NQ stacks separately, then use Pandora's Entrust Duplicates button to return matching items. Check that other stacks are untouched and the reserved copy remains the match.
3. Verify normal Remove All from Saddlebag, Remove from Saddlebag, retainer Retrieve Quantity and unrelated numeric dialogs remain manual. Test full inventory, localized clients, and plugins adding context-menu entries. Unavailable or mismatched actions must not submit another menu option and should produce a chat error for diagnosis.
4. Change the stack, close/switch the bag, disable the feature, or reload during the operation. No stale automatic submission should follow. Any quantity dialog left after cancellation is for manual handling. Re-enable/reload and verify the feature choice persists.

## Shared Hotbar Sync

Use disposable test assignments and back up the game's hotbar configuration before native testing.

1. With the feature off, prepare different PvE/PvP assignments on a shared keyboard bar and shared cross hotbar. Include empty slots, an item, a macro at index zero and Sprint. Also prepare an unshared bar as a control.
2. Enable sync in PvE. Verify shared PvP bars initially receive PvE assignments, all 16 cross-hotbar slots are covered, and the unshared bar, sharing flags, layouts and keybinds stay unchanged. Inspect the initial backup to confirm it contains the old PvP assignments.
3. On a fresh test character, repeat while already in PvP. PvE must still be the initial source, including for the active PvP bar.
4. Add, move and remove assignments separately in both modes. Verify each change reaches the other mode after settling, and persists through mode changes and game restart. Sprint must become the native PvE/PvP version; other commands copy verbatim.
5. Change jobs (including a crafting job outside PvP), reload the plugin and disable/re-enable the feature. Verify history survives and a newer PvP edit is not replaced by initialization. Log into another character and confirm separate state.
6. Turn sharing off for one bar while syncing is enabled. Verify it stops being tracked and its job-specific assignments are untouched. Share it again and verify PvE initialization for that bar only.
7. Disable sync, edit different slots on both sides, then re-enable. Verify both changes merge. Next change the same slot differently on both sides while disabled; verify neither is overwritten and settings lists the conflict. Test each choice and verify only conflicting slots are resolved. Change a conflicting assignment before a pending choice applies; verify the old choice is discarded.
8. Verify sync waits during combat, zoning, job/mode changes and active hotbar edits. Repeated enable/disable must not leave background writes running. Test alongside other enabled hotbar plugins, especially Accord, without enabling another feature that also synchronizes the same shared bars.
9. With the feature disabled, temporarily make a backed-up test sync-history file unreadable. Re-enable and verify a visible pause without writes or silent reinitialization; restore the file before retrying. If any write/verification error occurs, verify the pause, retained backup and unchanged unrelated assignments before continuing.
