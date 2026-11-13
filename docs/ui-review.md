# Aetherworks UI review

The live window and offline preview use the shared AetherglassUI shell, searchable settings pages and controls. Overview provides quick switches and jumps to a feature group. Features keeps settings on one scrolling page. Status & recovery shows the current assignments, save failures and cleanup actions. The native paid-travel prompt uses the shared confirmation control.

Run `./preview/render.ps1` with .NET 10, Dalamud API 15 references and the AetherglassUI checkout described in the README. The script installs the pinned offline renderer. It writes desktop, compact, recovery and interaction scenes under `artifacts/preview`. Scripted checks cover rail navigation, the enable switch, paid-travel confirmation inversion and all six Ember settings appearing in a row search.

## Remaining native acceptance

Check real game fonts and scaling, header movement, resizing, Escape/close behavior and keyboard/controller navigation. Verify the initial shared-hotbar explanation, conflict scope, retry-save behavior and failed-cleanup recovery against real state. Test quote expiry and cancellation using `docs/first-test.md`. Offline rendering does not establish native behavior or exercise the real feature actions.
