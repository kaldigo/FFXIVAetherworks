[CmdletBinding()]
param([string]$OutputDirectory=(Join-Path $PSScriptRoot '../artifacts/preview'),[string]$Renderer,[string[]]$Scenes)
$ErrorActionPreference='Stop'
$null=New-Item -ItemType Directory -Force $OutputDirectory
if (!$Renderer) {
    $toolDirectory=Join-Path $PSScriptRoot '../artifacts/preview-tools/0.1.0-beta.3'
    $Renderer=Join-Path $toolDirectory 'glasswork-preview.exe'
    if (!(Test-Path $Renderer)) {
        dotnet tool install Glasswork.Preview --version 0.1.0-beta.3 --tool-path $toolDirectory
        if ($LASTEXITCODE) { throw 'Could not install the preview renderer' }
    }
}
dotnet build (Join-Path $PSScriptRoot 'Aetherworks.Preview.csproj') -c Release --nologo -p:NuGetAudit=false
if ($LASTEXITCODE) { throw 'Could not build the linked Aetherworks preview' }
$assembly=Join-Path $PSScriptRoot 'bin/Release/net10.0-windows/Aetherworks.Preview.dll'
# Use a quiet opaque backdrop rather than the renderer's diagnostic pattern.
Add-Type -AssemblyName System.Drawing
$backdrop=Join-Path $OutputDirectory 'backdrop.png'
$bitmap=[Drawing.Bitmap]::new(640,480)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$rect=[Drawing.Rectangle]::new(0,0,640,480)
$brush=[Drawing.Drawing2D.LinearGradientBrush]::new($rect,[Drawing.Color]::FromArgb(22,34,43),[Drawing.Color]::FromArgb(38,29,48),35.0)
try { $graphics.FillRectangle($brush,$rect); $bitmap.Save($backdrop,[Drawing.Imaging.ImageFormat]::Png) }
finally { $brush.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
if (!$Scenes) { $Scenes=@('overview','overview-compact','first-run','features','return','return-compact','return-immediate','hotbars','hotbars-off','saddlebag','conflicts','recovery','recovery-compact','sync-paused','cleanup','waiting','minimum','minimum-scrolled','empty-search','rail-hover','navigate-features','ember-search','toggle-feature','toggle-confirmation','scale-150','travel-confirmation') }
foreach ($scene in $Scenes) {
    & $Renderer --assembly $assembly --scene $scene --out (Join-Path $OutputDirectory "$scene.png") --backdrop $backdrop --warp --deterministic
    if ($LASTEXITCODE) { throw "Preview failed: $scene" }
}
