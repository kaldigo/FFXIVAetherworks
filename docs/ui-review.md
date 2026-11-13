# Aetherworks UI review

The redesign follows `E:\Projects\FFXIV Dalamud Setup\docs\Plugin UI Design Guide.md`, its Accord 0.2.15.31 reference, and the eight Auto Keybound Brawler / Auto Sightseeing Log captures in `docs/plugin-ui/references` in the parent workspace. The Aetherworks cog and aether spark provide the mark; the 48-unit rail, charcoal surfaces, restrained accents and focusable buttons follow the family geometry. Controls use native ImGui inputs; the pill switches draw over named buttons, retaining keyboard activation. There is no runtime UI library to package.

The additional page references inform the summary tiles and header status badge, right-aligned switches/actions, inset row separators, grouped quick controls, subtle background atmosphere and shorter help sections. No session counts, dependency installers, logs or run controls have been invented for Aetherworks. The shared-bar count is shown only when observed; an unavailable count uses a dash.

## Information and actions

- **Overview** answers what is enabled and what it is currently doing. A summary shows enabled features, observed shared bars at desktop widths, and issues needing review. Each convenience has an independent right-aligned enable switch and Settings action in one grouped panel. There is no master switch or permanent footer.
- **Features** has a stationary search field, categories and details. Details preserve the originating workspace and query on Back. Settings apply immediately through the same lifecycle/save handlers as before.
- **Status & recovery** surfaces real save failures, shared-slot conflicts, paused sync and incomplete cleanup. Choices act on the existing conflict resolver or lifecycle manager. No restore/reset operation is invented by the UI.
- **Teleport confirmation** presents destination, fare, primary action and cancellation through the existing checked Return flow.

Preference and activity are separate: an enabled hotbar feature may say it is waiting, paused or conflicted. Colors supplement written state. Unavailable enable controls explain why cleanup is required. The initial PvE-to-PvP replacement is explained before enabling sync.

## Reproduce the renders

```powershell
./preview/render.ps1
```

Requires the .NET 10 SDK, installed Dalamud API 15 references and the pinned Glasswork.Preview 0.1.0-beta.3 tool. The script installs that tool under ignored `artifacts/preview-tools` if needed. An existing renderer can be supplied with `-Renderer <absolute-path>`. The preview assembly links `Aetherworks/Windows/Aetherworks*.cs`; it does not recreate the screens in HTML or supply simulated live game services.

The 27 sample scenes cover Overview at the default 780 × 610 size and compact width; first run with all features off; searchable Features; Return with fare confirmation on/off; enabled and disabled hotbar sync; saddlebag instructions; conflicts; save errors; paused sync; failed cleanup; waiting state; the 360 × 320 minimum and its scrolled content; empty results; rail hover; actual rail navigation; Configure opening a detail; Back preserving a search; 150% scale; and paid travel confirmation. Five interaction scenes fail if navigation or switch callbacks do not reach the expected state, including the inverse mapping from Ask before paid travel to the existing skip-confirmation setting.

Each scene writes a complete PNG under `artifacts/preview`, including header, rail and scrollable content. The renderer uses software rasterization and a quiet backdrop. Sample choices are confined to the preview process; no configuration or game state is read or changed.

Run `./preview/make-review.ps1` after rendering to create `artifacts/preview/review.jpg`, a six-window contact sheet. Open the individual PNGs for full-size text and control review.

## Remaining native acceptance

Check real game fonts and scaling, header movement, resizing, Escape/close behavior and keyboard/controller navigation. Verify the initial shared-hotbar explanation, conflict scope, retry-save behavior and failed-cleanup recovery against real state. Test quote expiry and cancellation using `docs/first-test.md`. Offline rendering does not establish native behavior or exercise the real feature actions.
