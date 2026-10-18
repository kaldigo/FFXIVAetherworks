# Glamour ownership

Historical discussion notes. Superseded by the [agreed feature plan](feature-plan.md). See the [project README](../README.md) for implementation status. The questions below are preserved as history, not outstanding requirements.

## Original idea

Show which glamours the user already owns so they can avoid buying duplicates. Moved from the shared plugin ideas list into Aetherworks on 2026-10-18.

## Feature toggle

This feature will have an independent **Glamour ownership** toggle in Aetherworks settings, disabled by default. Disabling it must remove its UI additions and stop its event subscriptions and checks.

## Decisions to explore

- What counts as owned: the exact item, an equivalent appearance, or both?
- Which storage locations should count: inventory, armoury chest, glamour dresser, armoire, retainers, and saddlebag? These are candidate requirements, not confirmed accessible data sources. An inventory-only check is not assumed sufficient.
- Where should ownership appear: item tooltips, market board results, vendor lists, or a dedicated search window?
- Should ownership be character-specific, or include other characters?
- How should unknown or outdated ownership data be shown? Missing data must not silently imply that an appearance is unowned.

Define these rules with the user before implementing the feature or choosing its data sources. The feature belongs to Aetherworks independently of the mount/minion roulette idea.
