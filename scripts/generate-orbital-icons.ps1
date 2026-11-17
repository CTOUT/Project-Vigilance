<#
.SYNOPSIS
    Generates RimWorld-styled expanding icon textures for Project Vigilance orbital infrastructure.
.DESCRIPTION
    Uses System.Drawing with GDI+ anti-aliased vector rendering to produce crisp,
    symmetrical 128x128 RGBA32 sprites with transparent backgrounds and white/grayscale
    palette masks optimized for RimWorld's runtime Faction.Color tinting.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$outputDir = Join-Path $PSScriptRoot "..\Geoscape\1.7\Textures\ProjectVigilance\Geoscape\World\Orbital"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$outlineColor = [System.Drawing.Color]::FromArgb(255, 28, 30, 32)
$whiteFill = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$panelFill = [System.Drawing.Color]::FromArgb(255, 225, 230, 236)
$darkAccent = [System.Drawing.Color]::FromArgb(255, 175, 185, 195)

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
# 1. Orbital Logistics Hub (Spoke-and-Hub Modular Station)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Radial Truss Spokes (4 diagonal arms: 45, 135, 225, 315)
$spokePoints = @(
    @([System.Drawing.PointF]::new(64, 64), [System.Drawing.PointF]::new(28, 28)),
    @([System.Drawing.PointF]::new(64, 64), [System.Drawing.PointF]::new(100, 28)),
    @([System.Drawing.PointF]::new(64, 64), [System.Drawing.PointF]::new(28, 100)),
    @([System.Drawing.PointF]::new(64, 64), [System.Drawing.PointF]::new(100, 100))
)
$trussPen = New-Object System.Drawing.Pen($outlineColor, 5.0)
foreach ($spoke in $spokePoints) {
    $g.DrawLine($trussPen, $spoke[0], $spoke[1])
}
$trussInnerPen = New-Object System.Drawing.Pen($panelFill, 2.0)
foreach ($spoke in $spokePoints) {
    $g.DrawLine($trussInnerPen, $spoke[0], $spoke[1])
}

# 4 Outlying Habitation / Docking Pods at spoke ends
$podRects = @(
    [System.Drawing.RectangleF]::new(18, 18, 20, 20),
    [System.Drawing.RectangleF]::new(90, 18, 20, 20),
    [System.Drawing.RectangleF]::new(18, 90, 20, 20),
    [System.Drawing.RectangleF]::new(90, 90, 20, 20)
)
foreach ($r in $podRects) {
    $g.FillEllipse($whiteBrush, $r)
    $g.DrawEllipse($outlinePen, $r)
    $innerR = [System.Drawing.RectangleF]::new($r.X + 4, $r.Y + 4, $r.Width - 8, $r.Height - 8)
    $g.FillEllipse($darkBrush, $innerR)
    $g.DrawEllipse($thinPen, $innerR)
}

# Horizontal Solar Wings across the equator
$g.FillRectangle($panelBrush, 12, 59, 28, 10)
$g.DrawRectangle($outlinePen, 12, 59, 28, 10)
$g.DrawLine($thinPen, 21, 59, 21, 69)
$g.DrawLine($thinPen, 30, 59, 30, 69)

$g.FillRectangle($panelBrush, 88, 59, 28, 10)
$g.DrawRectangle($outlinePen, 88, 59, 28, 10)
$g.DrawLine($thinPen, 97, 59, 97, 69)
$g.DrawLine($thinPen, 106, 59, 106, 69)

# Central Command Hub (Large Hexagon / Ring)
$hubOuter = [System.Drawing.RectangleF]::new(46, 46, 36, 36)
$g.FillEllipse($whiteBrush, $hubOuter)
$g.DrawEllipse($outlinePen, $hubOuter)

$hubMid = [System.Drawing.RectangleF]::new(52, 52, 24, 24)
$g.FillEllipse($panelBrush, $hubMid)
$g.DrawEllipse($thinPen, $hubMid)

$hubCore = [System.Drawing.RectangleF]::new(58, 58, 12, 12)
$g.FillEllipse($darkBrush, $hubCore)
$g.DrawEllipse($thinPen, $hubCore)

$canvas.Bitmap.Save((Join-Path $outputDir "OrbitalHub.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

# -------------------------------------------------------------
# 2. Reconnaissance Satellite (Telescope / SAR Aperture)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Solar Wings (horizontal, multi-segment)
$g.FillRectangle($panelBrush, 8, 56, 38, 16)
$g.DrawRectangle($outlinePen, 8, 56, 38, 16)
$g.DrawLine($thinPen, 20, 56, 20, 72)
$g.DrawLine($thinPen, 32, 56, 32, 72)
$g.DrawLine($thinPen, 8, 64, 46, 64)

$g.FillRectangle($panelBrush, 82, 56, 38, 16)
$g.DrawRectangle($outlinePen, 82, 56, 38, 16)
$g.DrawLine($thinPen, 94, 56, 94, 72)
$g.DrawLine($thinPen, 106, 56, 106, 72)
$g.DrawLine($thinPen, 82, 64, 120, 64)

# Central Fuselage (Telescope Barrel)
$g.FillRectangle($whiteBrush, 52, 32, 24, 60)
$g.DrawRectangle($outlinePen, 52, 32, 24, 60)

# Telescope Aperture / Optics Bell (Facing Downward)
$apertureOuter = [System.Drawing.RectangleF]::new(46, 84, 36, 22)
$g.FillEllipse($whiteBrush, $apertureOuter)
$g.DrawEllipse($outlinePen, $apertureOuter)

$apertureInner = [System.Drawing.RectangleF]::new(51, 88, 26, 14)
$g.FillEllipse($darkBrush, $apertureInner)
$g.DrawEllipse($thinPen, $apertureInner)

# Upper Avionics Bus & Antenna Mast
$g.FillRectangle($panelBrush, 55, 24, 18, 12)
$g.DrawRectangle($thinPen, 55, 24, 18, 12)

$g.DrawLine($outlinePen, 64, 24, 64, 12)
$g.FillEllipse($whiteBrush, 61, 9, 6, 6)
$g.DrawEllipse($thinPen, 61, 9, 6, 6)

$canvas.Bitmap.Save((Join-Path $outputDir "Satellite_Recon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

# -------------------------------------------------------------
# 3. Trade Satellite (Cargo Pods / Commercial Docking Clamps)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Angled Solar Panels (Gull-wing angle)
$leftWing = @(
    [System.Drawing.PointF]::new(48, 62),
    [System.Drawing.PointF]::new(12, 42),
    [System.Drawing.PointF]::new(16, 30),
    [System.Drawing.PointF]::new(50, 52)
)
$g.FillPolygon($panelBrush, $leftWing)
$g.DrawPolygon($outlinePen, $leftWing)
$g.DrawLine($thinPen, 31, 36, 33, 56)

$rightWing = @(
    [System.Drawing.PointF]::new(80, 62),
    [System.Drawing.PointF]::new(116, 42),
    [System.Drawing.PointF]::new(112, 30),
    [System.Drawing.PointF]::new(78, 52)
)
$g.FillPolygon($panelBrush, $rightWing)
$g.DrawPolygon($outlinePen, $rightWing)
$g.DrawLine($thinPen, 97, 36, 95, 56)

# Main Commercial Bus
$g.FillRectangle($whiteBrush, 48, 44, 32, 44)
$g.DrawRectangle($outlinePen, 48, 44, 32, 44)

# 4 Modular Cargo Containers (2x2 grid on bus)
$g.FillRectangle($panelBrush, 51, 48, 11, 16)
$g.DrawRectangle($thinPen, 51, 48, 11, 16)

$g.FillRectangle($panelBrush, 66, 48, 11, 16)
$g.DrawRectangle($thinPen, 66, 48, 11, 16)

$g.FillRectangle($darkBrush, 51, 68, 11, 16)
$g.DrawRectangle($thinPen, 51, 68, 11, 16)

$g.FillRectangle($darkBrush, 66, 68, 11, 16)
$g.DrawRectangle($thinPen, 66, 68, 11, 16)

# Docking Clamp / Harbor Ring (Lower Boom)
$g.DrawLine($outlinePen, 64, 88, 64, 102)
$dockRing = [System.Drawing.RectangleF]::new(52, 98, 24, 18)
$g.FillEllipse($whiteBrush, $dockRing)
$g.DrawEllipse($outlinePen, $dockRing)
$dockInner = [System.Drawing.RectangleF]::new(58, 102, 12, 10)
$g.FillEllipse($darkBrush, $dockInner)
$g.DrawEllipse($thinPen, $dockInner)

# Upper Communications Dish
$dishUpper = [System.Drawing.RectangleF]::new(52, 22, 24, 14)
$g.FillPie($whiteBrush, 52, 20, 24, 20, 180, 180)
$g.DrawArc($outlinePen, 52, 20, 24, 20, 180, 180)
$g.DrawLine($outlinePen, 52, 30, 76, 30)
$g.DrawLine($thinPen, 64, 20, 64, 44)

$canvas.Bitmap.Save((Join-Path $outputDir "Satellite_Trade.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

# -------------------------------------------------------------
# 4. Navigation Satellite (GPS Cross Phased Array)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Horizontal Solar Wings
$g.FillRectangle($panelBrush, 6, 56, 36, 16)
$g.DrawRectangle($outlinePen, 6, 56, 36, 16)
$g.DrawLine($thinPen, 18, 56, 18, 72)
$g.DrawLine($thinPen, 30, 56, 30, 72)

$g.FillRectangle($panelBrush, 86, 56, 36, 16)
$g.DrawRectangle($outlinePen, 86, 56, 36, 16)
$g.DrawLine($thinPen, 98, 56, 98, 72)
$g.DrawLine($thinPen, 110, 56, 110, 72)

# Central Octagonal Bus
$busPoly = @(
    [System.Drawing.PointF]::new(52, 44),
    [System.Drawing.PointF]::new(76, 44),
    [System.Drawing.PointF]::new(84, 52),
    [System.Drawing.PointF]::new(84, 76),
    [System.Drawing.PointF]::new(76, 84),
    [System.Drawing.PointF]::new(52, 84),
    [System.Drawing.PointF]::new(44, 76),
    [System.Drawing.PointF]::new(44, 52)
)
$g.FillPolygon($whiteBrush, $busPoly)
$g.DrawPolygon($outlinePen, $busPoly)

# Distinct 4-Element GPS Antenna Array (Cross Mast)
$g.DrawLine($outlinePen, 64, 22, 64, 106)
$g.DrawLine($outlinePen, 26, 64, 102, 64)

# Terminal Antennas on cross tips
$antennas = @(
    [System.Drawing.RectangleF]::new(60, 16, 8, 8),
    [System.Drawing.RectangleF]::new(60, 104, 8, 8),
    [System.Drawing.RectangleF]::new(20, 60, 8, 8),
    [System.Drawing.RectangleF]::new(100, 60, 8, 8)
)
foreach ($a in $antennas) {
    $g.FillEllipse($panelBrush, $a)
    $g.DrawEllipse($outlinePen, $a)
}

# Central Timing Core
$g.FillEllipse($darkBrush, 58, 58, 12, 12)
$g.DrawEllipse($thinPen, 58, 58, 12, 12)

$canvas.Bitmap.Save((Join-Path $outputDir "Satellite_Nav.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

# -------------------------------------------------------------
# 5. Communication Satellite (Twin Large Parabolic Dishes)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Lower Articulated Solar Wings
$g.FillRectangle($panelBrush, 14, 78, 36, 14)
$g.DrawRectangle($outlinePen, 14, 78, 36, 14)
$g.DrawLine($thinPen, 26, 78, 26, 92)
$g.DrawLine($thinPen, 38, 78, 38, 92)

$g.FillRectangle($panelBrush, 78, 78, 36, 14)
$g.DrawRectangle($outlinePen, 78, 78, 36, 14)
$g.DrawLine($thinPen, 90, 78, 90, 92)
$g.DrawLine($thinPen, 102, 78, 102, 92)

# Central Relay Bus
$g.FillRectangle($whiteBrush, 52, 44, 24, 48)
$g.DrawRectangle($outlinePen, 52, 44, 24, 48)

# Twin Large Parabolic Dishes Angled Outwards
# Left Dish
$g.FillPie($panelBrush, 18, 24, 34, 34, 135, 180)
$g.DrawArc($outlinePen, 18, 24, 34, 34, 135, 180)
$g.DrawLine($outlinePen, 23, 49, 47, 25)
$g.DrawLine($thinPen, 35, 37, 52, 54) # Strut to bus

# Right Dish
$g.FillPie($panelBrush, 76, 24, 34, 34, 225, 180)
$g.DrawArc($outlinePen, 76, 24, 34, 34, 225, 180)
$g.DrawLine($outlinePen, 81, 25, 105, 49)
$g.DrawLine($thinPen, 93, 37, 76, 54) # Strut to bus

# Central High-Gain Feed Horn Mast
$g.DrawLine($outlinePen, 64, 44, 64, 16)
$g.FillEllipse($whiteBrush, 60, 12, 8, 8)
$g.DrawEllipse($thinPen, 60, 12, 8, 8)

$canvas.Bitmap.Save((Join-Path $outputDir "Satellite_Comms.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

# -------------------------------------------------------------
# 6. Military Satellite (Stealth Wedge & Phased Array Radar)
# -------------------------------------------------------------
$canvas = New-Canvas
$g = $canvas.Graphics

# Angular Swept-Back Dagger Fins
$leftFin = @(
    [System.Drawing.PointF]::new(50, 48),
    [System.Drawing.PointF]::new(10, 26),
    [System.Drawing.PointF]::new(16, 18),
    [System.Drawing.PointF]::new(52, 34)
)
$g.FillPolygon($panelBrush, $leftFin)
$g.DrawPolygon($outlinePen, $leftFin)

$rightFin = @(
    [System.Drawing.PointF]::new(78, 48),
    [System.Drawing.PointF]::new(118, 26),
    [System.Drawing.PointF]::new(112, 18),
    [System.Drawing.PointF]::new(76, 34)
)
$g.FillPolygon($panelBrush, $rightFin)
$g.DrawPolygon($outlinePen, $rightFin)

# Armored Stealth Chevron / Wedge Body
$wedgePoly = @(
    [System.Drawing.PointF]::new(64, 108), # Pointed forward nose
    [System.Drawing.PointF]::new(86, 44),
    [System.Drawing.PointF]::new(74, 28),
    [System.Drawing.PointF]::new(64, 34),
    [System.Drawing.PointF]::new(54, 28),
    [System.Drawing.PointF]::new(42, 44)
)
$g.FillPolygon($whiteBrush, $wedgePoly)
$g.DrawPolygon($outlinePen, $wedgePoly)

# Phased-Array Flat Radar Tile Grid (Central Armor)
$radarPoly = @(
    [System.Drawing.PointF]::new(64, 88),
    [System.Drawing.PointF]::new(76, 50),
    [System.Drawing.PointF]::new(52, 50)
)
$g.FillPolygon($darkBrush, $radarPoly)
$g.DrawPolygon($thinPen, $radarPoly)

# Kinetic Launcher Tubes / Targeting Pods
$g.DrawLine($outlinePen, 52, 44, 52, 30)
$g.DrawLine($outlinePen, 76, 44, 76, 30)
$g.FillEllipse($darkBrush, 50, 27, 4, 4)
$g.FillEllipse($darkBrush, 74, 27, 4, 4)

$canvas.Bitmap.Save((Join-Path $outputDir "Satellite_Military.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$canvas.Bitmap.Dispose()

$outlinePen.Dispose()
$thinPen.Dispose()
$trussPen.Dispose()
$trussInnerPen.Dispose()
$whiteBrush.Dispose()
$panelBrush.Dispose()
$darkBrush.Dispose()

Write-Host "==> Successfully generated all 6 orbital expanding icon textures in: $outputDir" -ForegroundColor Green
