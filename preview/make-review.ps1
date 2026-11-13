[CmdletBinding()]
param([string]$OutputDirectory=(Join-Path $PSScriptRoot '../artifacts/preview'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$scenes=@(
    @('overview','Overview - default window'),
    @('return-compact','Return settings - compact'),
    @('hotbars-off','Shared hotbars - before enabling'),
    @('conflicts','Shared slots - conflict choices'),
    @('recovery-compact','Status and save recovery - compact'),
    @('travel-confirmation','Paid Teleport confirmation')
)
$canvas=[Drawing.Bitmap]::new(1560,1440)
$graphics=[Drawing.Graphics]::FromImage($canvas)
$font=[Drawing.Font]::new('Segoe UI',14)
try {
    $graphics.Clear([Drawing.Color]::FromArgb(20,23,30))
    $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    for($i=0;$i -lt $scenes.Count;$i++) {
        $x=([int]($i%3))*520
        $y=([int][Math]::Floor($i/3))*720
        $graphics.DrawString($scenes[$i][1],$font,[Drawing.Brushes]::White,[single]($x+10),[single]($y+8))
        $source=[Drawing.Image]::FromFile((Join-Path $OutputDirectory ($scenes[$i][0]+'.png')))
        try {
            $ratio=[Math]::Min(500.0/$source.Width,670.0/$source.Height)
            $width=[int]($source.Width*$ratio)
            $height=[int]($source.Height*$ratio)
            $graphics.DrawImage($source,[Drawing.Rectangle]::new($x+10,$y+40,$width,$height))
        } finally { $source.Dispose() }
    }
    $canvas.Save((Join-Path $OutputDirectory 'review.jpg'),[Drawing.Imaging.ImageFormat]::Jpeg)
} finally { $font.Dispose(); $graphics.Dispose(); $canvas.Dispose() }
