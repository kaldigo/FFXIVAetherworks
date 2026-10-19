# Publishing

The owner authorises automatic preview publication after successful checks for source commits on `main`. Feed-only commits are ignored. The source is built at the shared planner's unique four-part assembly version, then packaged and checked before publishing an immutable release. The shared publisher verifies uploaded checksums before updating the source feed; retries recover an existing source release without replacing public assets.

The consuming workflow pins the reviewed shared release implementation in `kaldigo/DalamudPlugins`. No cross-repository token is stored. The central feed reads each public source feed hourly or on demand. Failed builds, mismatched source receipts, invalid packages and version downgrades do not replace the existing release.

Build: `pwsh -File tools/build.ps1 -Version <four-part-version>` with .NET 10 and installed Dalamud API 15 development references. In CI the workflow obtains those references first. Regression and packaging checks run before publication. Native game acceptance remains a separate requirement; all automatic releases remain previews.

Plugin identity and configuration paths are preserved across the repository migration. The old HotbarTools installation URL is retired; use https://raw.githubusercontent.com/kaldigo/DalamudPlugins/main/pluginmaster.json.
