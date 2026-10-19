# Repository maintenance

This is an independent plugin source repository. Preserve plugin identity, commands, configuration and dependency pins. Keep runtime dependencies in the plugin package; never publish personal settings, snapshots, game data or build working directories. Make targeted changes and run `tools/build.ps1` plus the packaging checks before publishing.

Use the reviewed, commit-pinned shared release actions from kaldigo/DalamudPlugins. Updates on main publish only after all checks pass. Never overwrite a public release or force-push source. Update the central registration when moving a source feed. Native testing remains explicit.
