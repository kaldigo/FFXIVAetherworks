The party-title arrow toggles Ember and remembers the choice. When Ember is on, combat visibility and the hide delay apply.

Keep Ember in the in-game window, connect its main stats view to the IINACT WebSocket and suppress Settings popups.

Keep the sync icon with the name and draw the percentage beside the rank while the cast label replaces the name.

Draw the native level-sync icon directly after the damage percentage in the same party badge.

Click the party-list title to toggle the Ember overlay. Adjust zoom, opacity, locking, combat and PvP visibility, and the post-combat hide delay in Party list damage settings.

Left-click the party-list title with Party list damage enabled to toggle a native encounter meter. Open Settings in the meter to choose damage, DPS and job columns.

Draw IINACT percentage together with the rank badge during casts. Reserve space for the name and cast label.

Remove the leftover level separator between the level-sync icon and the character name.

Place the IINACT damage percentage between the damage-rank badge and the level-sync icon.

Show encounter damage rank in the native party-slot badge and only the IINACT percentage beside the name. Preserve party order and restore slot numbers when there is no rank.

Resolve party and Trust actors from each native row's entity ID, build labels from the unclipped game input, and display IINACT's damage% directly.

Preserve party-list sync icons when replacing the level. Match decorated NPC rows by name and show dashes on occupied rows awaiting an IINACT match during an encounter.

Keep party damage prefixes in place between draws. Reapply after the game rewrites a row and update changed ranks or colors directly from the original name.

Draw damage ranks and percentages with the game's level numeral glyphs. Use ordinary spaces for padding and keep the selected prefix color.

Style the party damage prefix in bold with a selectable color. Align percentages to at least two digits and reserve two rank positions on rosters larger than ten.

Include Duty Support and Trust NPCs in party damage ranks and percentages. Match owner-tagged NPCs and world-qualified players. The alternative entry point now applies the prefix after a requested update populates the rows.

Add optional Party list damage with IINACT rank and party damage share. Choose Before draw or Before requested update in the feature settings to test how level replacement behaves in game.

0.2.1.4: Replace the split settings browser with Overview, searchable Features, and Status & recovery behind a persistent icon rail. Add summary counts and a header status badge, group quick controls with right-aligned switches, align row actions, keep save recovery reachable, preserve search on Back, stack conflict choices at compact widths, and restyle paid Teleport confirmation. Ask before paid travel controls the inverse of the existing skip-confirmation setting. Include reproducible previews linked to the actual draw code. Feature behavior and saved configuration are unchanged. In-game font scale, controller focus and live acceptance remain pending.

0.2.1.3: Fix Withdraw all but one doing nothing: use the saddlebag's Remove from Saddlebag action and How many do you want to remove? prompt, rather than retainer Retrieve Quantity/withdraw labels. Allow the original context menu time to close. Report unavailable native actions and unexpected quantity dialogs in chat instead of failing silently. Native in-game acceptance remains pending.
