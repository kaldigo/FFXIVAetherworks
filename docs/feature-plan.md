# Aetherworks feature plan

## Return via Teleport

Return via Teleport is the first feature. All previous feature implementations, collectors and UI integrations are removed.

Use the existing Return button. When Return is ready, preserve its native behavior. When its cooldown is active and normal Teleport is available, resolve the current character's registered home aetheryte and use the game's normal Teleport function at its current fare.

The feature is disabled by default. Skip paid Teleport confirmation is a separate saved setting, also off by default. Off shows a destination/fare confirmation; on executes immediately. Aetheryte ticket settings and native restrictions remain in effect.

A confirmation is valid for one character, territory, home point and fare, for at most 30 seconds. Consume it before native execution. Cancel on context changes, disabling or changing the confirmation option. Never retry a failed Teleport automatically. Use free Return if it becomes ready before confirmation is accepted.

## Shared Hotbar Sync

The second feature is disabled by default and operates only on keyboard/cross hotbars marked Shared by the game. Read shared PvE storage and the native-resolved shared PvP storage; never use a hard-coded job index. Initially copy PvE to PvP, then persist a canonical per-character/per-bar/per-slot baseline for three-way merging. Handle clears and moves as assignments, preserve macro index zero, and adapt Sprint to each mode. Do not change job-specific bars or sharing flags.

Compare both sides with the last baseline. Propagate the changed side, accept matching changes, and pause only same-slot divergent changes for an explicit PvE/PvP choice. Revalidate choices against current assignments. Prune observed unshared bars; newly tracked bars initialize from PvE.

Use the framework thread, debounce context changes and edits, and require loaded hotbar storage plus matching live/saved active-mode assignments. Pause during combat/loading. Before writing, persist a snapshot and recheck the source. Use native saved-slot writers and verify all shared slots after the batch. Save history only after verification. Roll back failed writes only while the same context and expected slot values remain valid; retain backups and pause if recovery is incomplete. Unreadable history fails closed.

## UI

Use a Pandora-style searchable category/feature list on the left and selected-feature description, enable toggle and settings on the right. Actions contains Return via Teleport; Hotbars contains Shared Hotbar Sync, its status and conflict choices.

## Validation

Tests cover lifecycle and Return flow, initial hotbar copy, two-way edits and clears, cross hotbars, Sprint, reloads, character/job/mode changes, sharing changes, conflicts, stale choices, write/persistence failures, rollback and corrupt history. Packaging checks exclude stale dependencies and assets. Native action interception, fares, ticket behavior, hotbar storage/writes and UI acceptance require in-game testing.
