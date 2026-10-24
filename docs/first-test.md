# Aetherworks: in-game acceptance

These checks require a live game session; automated logic tests do not validate native hooks or rendering.

## Settings and Return via Teleport

1. Upgrade from an older Aetherworks configuration. Open `/aetherworks`. Confirm Return via Teleport under Actions and Shared Hotbar Sync under Hotbars are the only entries. New features and confirmation-skip start off; previously enabled Return settings remain saved. No old storage buttons, tooltip additions or background tracking should appear.
2. Search for Return, Teleport and Actions. Check the left list and right detail pane at different UI scales and window sizes. Enable/disable the feature, close/reopen settings and reload the plugin to verify saved choices.
3. With the feature off, verify normal Return, including its native cooldown error. With it on and Return ready, verify free Return, its normal cast/confirmation and normal cooldown afterward.
4. During Return cooldown, press the normal Return hotbar button (keyboard and cross hotbar), and test the Return action from the action list and a Return macro. Confirm the home destination and quoted fare match the game's Teleport list.
5. With Skip paid Teleport confirmation off, verify no travel or gil change before confirming. Confirm once and check the destination and charged fare. Cancel, close the dialog and press Escape; none should start travel. Repeated confirmation clicks must start at most one trip.
6. Turn Skip paid Teleport confirmation on. Press Return during cooldown: it should start paid Teleport without the Aetherworks prompt. Turn the setting off and verify confirmation returns. Reload and verify persistence.
7. Test normal discounts, a zero-cost destination, insufficient gil, and each relevant ticket setting with and without tickets. Native ticket prompts/settings must behave normally; the plugin's displayed fare is before any ticket use.
8. Test combat, death, duties, cutscenes, zoning, existing casts and animation lock. No restricted Teleport should start and no request should be retried later.
9. With a confirmation open, wait 30 seconds, change territory, change home point, log out/change character, disable the feature, or change its confirmation setting. The old authorization must no longer work. If Return finishes its cooldown while the confirmation is open, confirming must use free Return.
10. Disable/unload/reload repeatedly, including with a confirmation open. Verify hooks stop, no pending trip survives, normal Return works, and unrelated actions are unaffected. Test alongside other enabled action plugins.

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
