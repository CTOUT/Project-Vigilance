<#
.SYNOPSIS
    Generates RimWorld-styled expanding icon textures for Project Vigilance Geoscape outposts & landmarks.
.DESCRIPTION
    Uses System.Drawing with GDI+ anti-aliased vector rendering to produce crisp 128x128 RGBA32 sprites
    with transparent backgrounds and white/grayscale palette masks for dynamic Faction.Color tinting.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$outputDir = Join-Path $PSScriptRoot "..\Geoscape\1.7\Textures\ProjectVigilance\Geoscape\World\Outposts"
$aquaticsOutpostDir = Join-Path $PSScriptRoot "..\Aquatics\1.7\Textures\ProjectVigilance\Geoscape\World\Outposts"
foreach ($dir in @($outputDir, $aquaticsOutpostDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

$outlineColor = [System.Drawing.Color]::FromArgb(255, 28, 30, 32)
$whiteFill = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$panelFill = [System.Drawing.Color]::FromArgb(255, 225, 230, 236)
$darkAccent = [System.Drawing.Color]::FromArgb(255, 175, 185, 195)
$flameAccent = [System.Drawing.Color]::FromArgb(255, 245, 245, 245)

$outlinePen = New-Object System.Drawing.Pen($outlineColor, 3.0)
$outlinePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$outlinePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$outlinePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

$thinPen = New-Object System.Drawing.Pen($outlineColor, 1.8)
$thinPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$thinPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$thinPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

$whiteBrush = New-Object System.Drawing.SolidBrush($whiteFill)
$panelBrush = New-Object System.Drawing.SolidBrush($panelFill)
$darkBrush = New-Object System.Drawing.SolidBrush($darkAccent)
$flameBrush = New-Object System.Drawing.SolidBrush($flameAccent)

function New-Canvas {
    $bmp = New-Object System.Drawing.Bitmap(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    return @{ Bitmap = $bmp; Graphics = $g }
}

# -------------------------------------------------------------
# 1. Automated Listening Post (Radar / Sensor Mast & Radome)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Reinforced Concrete Bunker Base
$basePoly = @(
    [System.Drawing.PointF]::new(24, 108),
    [System.Drawing.PointF]::new(34, 84),
    [System.Drawing.PointF]::new(94, 84),
    [System.Drawing.PointF]::new(104, 108)
)
$g.FillPolygon($whiteBrush, $basePoly)
$g.DrawPolygon($outlinePen, $basePoly)
$g.DrawLine($thinPen, 40, 84, 34, 108)
$g.DrawLine($thinPen, 88, 84, 94, 108)

# Central Mast Truss
$g.DrawLine($outlinePen, 64, 84, 64, 46)
$g.DrawLine($thinPen, 58, 84, 64, 52)
$g.DrawLine($thinPen, 70, 84, 64, 52)

# Radome Dish / Spherical Sensor Dome
$radomeRect = [System.Drawing.RectangleF]::new(44, 20, 40, 40)
$g.FillEllipse($whiteBrush, $radomeRect)
$g.DrawEllipse($outlinePen, $radomeRect)

# Inner Radome Panel Bands
$innerBand = [System.Drawing.RectangleF]::new(50, 26, 28, 28)
$g.FillEllipse($panelBrush, $innerBand)
$g.DrawEllipse($thinPen, $innerBand)
$g.DrawLine($thinPen, 44, 40, 84, 40)

# Solar Power Panel Wing (Left side)
$solarPoly = @(
    [System.Drawing.PointF]::new(20, 52),
    [System.Drawing.PointF]::new(40, 46),
    [System.Drawing.PointF]::new(38, 72),
    [System.Drawing.PointF]::new(18, 78)
)
$g.FillPolygon($panelBrush, $solarPoly)
$g.DrawPolygon($outlinePen, $solarPoly)
$g.DrawLine($thinPen, 29, 49, 28, 75)

# Antenna Spire / Lightning Rod at top
$g.DrawLine($outlinePen, 64, 20, 64, 10)
$g.FillEllipse($darkBrush, 62, 8, 4, 4)
$g.DrawEllipse($thinPen, 62, 8, 4, 4)

$canvas.Bitmap.Save((Join-Path $outputDir "Outpost_Radar.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

# -------------------------------------------------------------
# 2. Signal Beacon (Neolithic / Medieval Watch Brazier)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Stone Plinth / Timber Base
$plinthPoly = @(
    [System.Drawing.PointF]::new(36, 110),
    [System.Drawing.PointF]::new(44, 88),
    [System.Drawing.PointF]::new(84, 88),
    [System.Drawing.PointF]::new(92, 110)
)
$g.FillPolygon($darkBrush, $plinthPoly)
$g.DrawPolygon($outlinePen, $plinthPoly)

# Timber Struts / Uprights
$g.DrawLine($outlinePen, 48, 88, 48, 54)
$g.DrawLine($outlinePen, 80, 88, 80, 54)
$g.DrawLine($outlinePen, 64, 88, 64, 54)
$g.DrawLine($thinPen, 48, 84, 80, 58)
$g.DrawLine($thinPen, 80, 84, 48, 58)

# Metal / Stone Fire Basket (Brazier Bowl)
$bowlPoly = @(
    [System.Drawing.PointF]::new(34, 54),
    [System.Drawing.PointF]::new(44, 70),
    [System.Drawing.PointF]::new(84, 70),
    [System.Drawing.PointF]::new(94, 54)
)
$g.FillPolygon($panelBrush, $bowlPoly)
$g.DrawPolygon($outlinePen, $bowlPoly)
$g.DrawLine($thinPen, 40, 62, 88, 62)

# Roaring Signal Flame (Tiered stylized flame tongues)
$flameOuter = @(
    [System.Drawing.PointF]::new(64, 12),
    [System.Drawing.PointF]::new(76, 28),
    [System.Drawing.PointF]::new(84, 38),
    [System.Drawing.PointF]::new(80, 52),
    [System.Drawing.PointF]::new(48, 52),
    [System.Drawing.PointF]::new(44, 38),
    [System.Drawing.PointF]::new(52, 28)
)
$g.FillPolygon($whiteBrush, $flameOuter)
$g.DrawPolygon($outlinePen, $flameOuter)

# Inner Flame Core
$flameInner = @(
    [System.Drawing.PointF]::new(64, 24),
    [System.Drawing.PointF]::new(72, 36),
    [System.Drawing.PointF]::new(70, 50),
    [System.Drawing.PointF]::new(58, 50),
    [System.Drawing.PointF]::new(56, 36)
)
$g.FillPolygon($flameBrush, $flameInner)
$g.DrawPolygon($thinPen, $flameInner)

$canvas.Bitmap.Save((Join-Path $outputDir "Outpost_Beacon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

# -------------------------------------------------------------
# 3. Automated Sonar Station (Coastal Hydrophone & Acoustic Waves)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Shoreline Shelter / Coastal Bunker (Left side)
$coastalBunker = @(
    [System.Drawing.PointF]::new(14, 98),
    [System.Drawing.PointF]::new(20, 68),
    [System.Drawing.PointF]::new(54, 68),
    [System.Drawing.PointF]::new(62, 98)
)
$g.FillPolygon($whiteBrush, $coastalBunker)
$g.DrawPolygon($outlinePen, $coastalBunker)
$g.DrawLine($thinPen, 34, 68, 32, 98)

# Bunker Roof Radome & Antenna
$g.FillEllipse($panelBrush, 28, 56, 18, 14)
$g.DrawEllipse($outlinePen, 28, 56, 18, 14)
$g.DrawLine($outlinePen, 37, 56, 37, 44)

# Submerged Hydrophone Cable Line
$g.DrawLine($outlinePen, 54, 88, 76, 88)

# Offshore Hydrophone Buoy Float
$buoyOuter = [System.Drawing.RectangleF]::new(70, 78, 20, 20)
$g.FillEllipse($whiteBrush, $buoyOuter)
$g.DrawEllipse($outlinePen, $buoyOuter)
$buoyCore = [System.Drawing.RectangleF]::new(76, 84, 8, 8)
$g.FillEllipse($darkBrush, $buoyCore)
$g.DrawEllipse($thinPen, $buoyCore)

# Concentric Acoustic Sonar Propagation Arcs (Expanding into ocean)
$arcPen1 = New-Object System.Drawing.Pen($outlineColor, 2.5)
$arcPen2 = New-Object System.Drawing.Pen($panelFill, 2.0)

$g.DrawArc($arcPen1, 55, 55, 50, 50, -45, 90)
$g.DrawArc($arcPen1, 45, 45, 70, 70, -45, 90)
$g.DrawArc($arcPen1, 35, 35, 90, 90, -45, 90)

$arcPen1.Dispose()
$arcPen2.Dispose()

$canvas.Bitmap.Save((Join-Path $aquaticsOutpostDir "Outpost_Sonar.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

# -------------------------------------------------------------
# 4. Encounter Aftermath (Smouldering Wreckage & Blast Crater)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Blast Crater / Impact Depression
$craterPoly = @(
    [System.Drawing.PointF]::new(24, 82),
    [System.Drawing.PointF]::new(40, 70),
    [System.Drawing.PointF]::new(64, 66),
    [System.Drawing.PointF]::new(88, 70),
    [System.Drawing.PointF]::new(104, 82),
    [System.Drawing.PointF]::new(94, 98),
    [System.Drawing.PointF]::new(64, 106),
    [System.Drawing.PointF]::new(34, 98)
)
$g.FillPolygon($darkBrush, $craterPoly)
$g.DrawPolygon($outlinePen, $craterPoly)

# Shattered Cargo Crate (Tilted in crater)
$cratePoly = @(
    [System.Drawing.PointF]::new(38, 78),
    [System.Drawing.PointF]::new(58, 68),
    [System.Drawing.PointF]::new(66, 86),
    [System.Drawing.PointF]::new(46, 96)
)
$g.FillPolygon($panelBrush, $cratePoly)
$g.DrawPolygon($outlinePen, $cratePoly)
$g.DrawLine($thinPen, 48, 73, 56, 91)

# Crossed Broken Weapons / Wreckage Beams (X-Mark of battle)
# Left-to-right girder
$beam1 = @(
    [System.Drawing.PointF]::new(32, 38),
    [System.Drawing.PointF]::new(96, 84),
    [System.Drawing.PointF]::new(92, 90),
    [System.Drawing.PointF]::new(28, 44)
)
$g.FillPolygon($whiteBrush, $beam1)
$g.DrawPolygon($outlinePen, $beam1)

# Right-to-left girder
$beam2 = @(
    [System.Drawing.PointF]::new(96, 38),
    [System.Drawing.PointF]::new(100, 44),
    [System.Drawing.PointF]::new(36, 90),
    [System.Drawing.PointF]::new(32, 84)
)
$g.FillPolygon($whiteBrush, $beam2)
$g.DrawPolygon($outlinePen, $beam2)

# Smoke Plume billows rising from debris
$smokePuffs = @(
    [System.Drawing.RectangleF]::new(56, 32, 16, 16),
    [System.Drawing.RectangleF]::new(66, 18, 18, 18),
    [System.Drawing.RectangleF]::new(50, 14, 14, 14)
)
foreach ($sp in $smokePuffs) {
    $g.FillEllipse($panelBrush, $sp)
    $g.DrawEllipse($thinPen, $sp)
}

$canvas.Bitmap.Save((Join-Path $outputDir "EncounterAftermath.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

$outlinePen.Dispose()
$thinPen.Dispose()
$whiteBrush.Dispose()
$panelBrush.Dispose()
$darkBrush.Dispose()
$flameBrush.Dispose()

Write-Host "==> Successfully generated all 4 Geoscape outpost textures in: $outputDir" -ForegroundColor Green
