# scripts/generate-building-icons.ps1
# Generates high-resolution building textures for Project Vigilance Category 3 Colony Sensory Buildings.
# Adheres to RimWorld orthographic 2.5D building aesthetic, transparent backgrounds, and stuff/color tinting rules.

Add-Type -AssemblyName System.Drawing

$outputDir = Join-Path $PSScriptRoot "..\Geoscape\1.7\Textures\ProjectVigilance\Geoscape\Buildings\Sensory"
$aquaticsBuildingDir = Join-Path $PSScriptRoot "..\Aquatics\1.7\Textures\ProjectVigilance\Geoscape\Buildings\Sensory"
foreach ($dir in @($outputDir, $aquaticsBuildingDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
}

function New-Canvas($w, $h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    return @{ Bitmap = $bmp; Graphics = $g }
}

# ==============================================================================
# 1. Lookout Watchtower (Geoscape_LookoutTower) - 256x384 (2x3 footprint)
# Stuffable (Woody/Stony) -> Light grayscale neutral palette for engine tinting
# ==============================================================================
function Generate-LookoutTower {
    $c = New-Canvas 256 384
    $g = $c.Graphics

    $darkOutline = [System.Drawing.Color]::FromArgb(255, 40, 40, 40)
    $midShade    = [System.Drawing.Color]::FromArgb(255, 160, 160, 160)
    $lightFill   = [System.Drawing.Color]::FromArgb(255, 215, 215, 215)
    $highFill    = [System.Drawing.Color]::FromArgb(255, 240, 240, 240)
    $shadowFill  = [System.Drawing.Color]::FromArgb(255, 120, 120, 120)

    $penOutlineThick = New-Object System.Drawing.Pen($darkOutline, 4)
    $penOutlineThin  = New-Object System.Drawing.Pen($darkOutline, 2)
    $penShade        = New-Object System.Drawing.Pen($midShade, 2)

    $brushLight  = New-Object System.Drawing.SolidBrush($lightFill)
    $brushMid    = New-Object System.Drawing.SolidBrush($midShade)
    $brushHigh   = New-Object System.Drawing.SolidBrush($highFill)
    $brushShadow = New-Object System.Drawing.SolidBrush($shadowFill)

    # --- Ground Foundation Pier Blocks ---
    $g.FillRectangle($brushShadow, 44, 345, 24, 25)
    $g.DrawRectangle($penOutlineThin, 44, 345, 24, 25)
    $g.FillRectangle($brushShadow, 188, 345, 24, 25)
    $g.DrawRectangle($penOutlineThin, 188, 345, 24, 25)

    # --- Vertical Stilt Columns ---
    # Left Column
    $g.FillRectangle($brushMid, 48, 195, 16, 155)
    $g.DrawRectangle($penOutlineThin, 48, 195, 16, 155)
    # Right Column
    $g.FillRectangle($brushMid, 192, 195, 16, 155)
    $g.DrawRectangle($penOutlineThin, 192, 195, 16, 155)

    # --- Structural Diagonal X-Braces ---
    $penXBrace = New-Object System.Drawing.Pen($midShade, 6)
    $g.DrawLine($penXBrace, 56, 335, 200, 265)
    $g.DrawLine($penXBrace, 56, 265, 200, 335)
    $g.DrawLine($penOutlineThin, 56, 335, 200, 265)
    $g.DrawLine($penOutlineThin, 56, 265, 200, 335)

    $g.DrawLine($penXBrace, 56, 265, 200, 200)
    $g.DrawLine($penXBrace, 56, 200, 200, 265)
    $g.DrawLine($penOutlineThin, 56, 265, 200, 200)
    $g.DrawLine($penOutlineThin, 56, 200, 200, 265)

    # Horizontal reinforcement collars
    $g.FillRectangle($brushHigh, 44, 260, 168, 10)
    $g.DrawRectangle($penOutlineThin, 44, 260, 168, 10)

    # Access ladder in center
    $ladderPen = New-Object System.Drawing.Pen($shadowFill, 3)
    $g.DrawLine($ladderPen, 120, 200, 120, 360)
    $g.DrawLine($ladderPen, 136, 200, 136, 360)
    for ($y = 210; $y -le 350; $y += 14) {
        $g.DrawLine($penOutlineThin, 120, $y, 136, $y)
    }

    # --- Upper Observation Platform Deck ---
    # Cantilever support corbels
    $ptsCorbelL = [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(48, 200)),
        (New-Object System.Drawing.Point(24, 185)),
        (New-Object System.Drawing.Point(48, 185))
    )
    $g.FillPolygon($brushShadow, $ptsCorbelL)
    $g.DrawPolygon($penOutlineThin, $ptsCorbelL)

    $ptsCorbelR = [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(208, 200)),
        (New-Object System.Drawing.Point(232, 185)),
        (New-Object System.Drawing.Point(208, 185))
    )
    $g.FillPolygon($brushShadow, $ptsCorbelR)
    $g.DrawPolygon($penOutlineThin, $ptsCorbelR)

    # Main platform floor slab
    $g.FillRectangle($brushLight, 22, 172, 212, 16)
    $g.DrawRectangle($penOutlineThick, 22, 172, 212, 16)
    # Floor plank seams
    for ($x = 36; $x -le 220; $x += 16) {
        $g.DrawLine($penOutlineThin, $x, 172, $x, 188)
    }

    # --- Balustrade / Perimeter Railing ---
    $g.FillRectangle($brushHigh, 26, 142, 204, 6)
    $g.DrawRectangle($penOutlineThin, 26, 142, 204, 6)
    # Balusters
    for ($bx = 32; $bx -le 224; $bx += 12) {
        $g.DrawLine($penOutlineThin, $bx, 148, $bx, 172)
    }

    # Platform interior items (Brass spyglass telescope & beacon brazier)
    $penTripod = New-Object System.Drawing.Pen($darkOutline, 2)
    $g.DrawLine($penTripod, 80, 155, 74, 172)
    $g.DrawLine($penTripod, 80, 155, 86, 172)
    $g.DrawLine($penTripod, 80, 155, 80, 172)
    # Spyglass tube angled up-left
    $penScope = New-Object System.Drawing.Pen($highFill, 4)
    $g.DrawLine($penScope, 72, 148, 92, 158)
    $g.DrawLine($penOutlineThin, 72, 148, 92, 158)

    # Brazier / lantern at right
    $g.FillRectangle($brushShadow, 170, 152, 10, 20)
    $g.DrawRectangle($penOutlineThin, 170, 152, 10, 20)
    $brushAmber = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 230, 230, 230))
    $g.FillEllipse($brushAmber, 172, 146, 6, 8)

    # --- Canopy Roof Supports ---
    $g.FillRectangle($brushMid, 30, 78, 10, 64)
    $g.DrawRectangle($penOutlineThin, 30, 78, 10, 64)
    $g.FillRectangle($brushMid, 216, 78, 10, 64)
    $g.DrawRectangle($penOutlineThin, 216, 78, 10, 64)

    # --- Pitched Pavilion Canopy Roof ---
    $ptsRoof = [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(12, 82)),
        (New-Object System.Drawing.Point(128, 22)),
        (New-Object System.Drawing.Point(244, 82)),
        (New-Object System.Drawing.Point(232, 90)),
        (New-Object System.Drawing.Point(128, 38)),
        (New-Object System.Drawing.Point(24, 90))
    )
    $g.FillPolygon($brushHigh, $ptsRoof)
    $g.DrawPolygon($penOutlineThick, $ptsRoof)

    # Inner roof gable fill
    $ptsGable = [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(24, 82)),
        (New-Object System.Drawing.Point(128, 28)),
        (New-Object System.Drawing.Point(232, 82))
    )
    $g.FillPolygon($brushMid, $ptsGable)
    $g.DrawPolygon($penOutlineThin, $ptsGable)

    # Shingle horizontal rows
    for ($sy = 42; $sy -le 78; $sy += 9) {
        $lx = 128 - [int](($sy - 28) * 1.8)
        $rx = 128 + [int](($sy - 28) * 1.8)
        $g.DrawLine($penOutlineThin, $lx, $sy, $rx, $sy)
    }

    # Roof Spire / Finial
    $g.DrawLine($penOutlineThick, 128, 6, 128, 24)
    $g.FillEllipse($brushHigh, 125, 4, 7, 7)
    $g.DrawEllipse($penOutlineThin, 125, 4, 7, 7)

    $savePath = Join-Path $outputDir "LookoutTower.png"
    $c.Bitmap.Save($savePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $c.Graphics.Dispose()
    $c.Bitmap.Dispose()
    Write-Host "Created: $savePath"
}

# ==============================================================================
# 2. Early-Warning Radar Dome (Geoscape_RadarSmall) - 384x384 (3x3 footprint)
# Industrial tech: weatherproof geodesic radome, concrete plinth, access hatch
# ==============================================================================
function Generate-RadarDomeSmall {
    $c = New-Canvas 384 384
    $g = $c.Graphics

    $cConcrete   = [System.Drawing.Color]::FromArgb(255, 95, 102, 110)
    $cConcreteHi = [System.Drawing.Color]::FromArgb(255, 130, 138, 148)
    $cDomeBase   = [System.Drawing.Color]::FromArgb(255, 218, 224, 233)
    $cDomeShade  = [System.Drawing.Color]::FromArgb(255, 165, 175, 188)
    $cDarkBorder = [System.Drawing.Color]::FromArgb(255, 35, 40, 48)
    $cSteelDoor  = [System.Drawing.Color]::FromArgb(255, 60, 68, 78)
    $cAmberHaza  = [System.Drawing.Color]::FromArgb(255, 245, 158, 11)
    $cBeaconRed  = [System.Drawing.Color]::FromArgb(255, 239, 68, 68)

    $penThick = New-Object System.Drawing.Pen($cDarkBorder, 4)
    $penThin  = New-Object System.Drawing.Pen($cDarkBorder, 2)
    $penDomeGeodesic = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 110, 120, 135), 2)

    # --- Concrete Octagonal Foundation Plinth ---
    $plinthPts = [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(110, 240)),
        (New-Object System.Drawing.Point(274, 240)),
        (New-Object System.Drawing.Point(344, 290)),
        (New-Object System.Drawing.Point(344, 335)),
        (New-Object System.Drawing.Point(274, 365)),
        (New-Object System.Drawing.Point(110, 365)),
        (New-Object System.Drawing.Point(40, 335)),
        (New-Object System.Drawing.Point(40, 290))
    )
    $brushConc = New-Object System.Drawing.SolidBrush($cConcrete)
    $g.FillPolygon($brushConc, $plinthPts)
    $g.DrawPolygon($penThick, $plinthPts)

    # Concrete apron expansion bevel
    $brushConcHi = New-Object System.Drawing.SolidBrush($cConcreteHi)
    $g.FillRectangle($brushConcHi, 80, 345, 224, 14)
    $g.DrawRectangle($penThin, 80, 345, 224, 14)

    # Side HVAC / Generator unit
    $brushDoor = New-Object System.Drawing.SolidBrush($cSteelDoor)
    $g.FillRectangle($brushDoor, 48, 290, 40, 35)
    $g.DrawRectangle($penThin, 48, 290, 40, 35)
    # Louver slats
    for ($ly = 295; $ly -le 318; $ly += 5) {
        $g.DrawLine($penThin, 52, $ly, 84, $ly)
    }

    # External power conduit pipe
    $penConduit = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 50, 55, 65), 5)
    $g.DrawLine($penConduit, 88, 315, 145, 315)
    $g.DrawLine($penConduit, 145, 315, 145, 275)

    # --- Geodesic Radome Main Sphere ---
    $cx = 192; $cy = 180; $r = 125
    $rectDome = New-Object System.Drawing.Rectangle(($cx - $r), ($cy - $r), ($r * 2), ($r * 2))

    $brushDomeShade = New-Object System.Drawing.SolidBrush($cDomeShade)
    $g.FillEllipse($brushDomeShade, $rectDome)

    # Radial gradient lighting highlight
    $pathDome = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pathDome.AddEllipse($rectDome)
    $pbr = New-Object System.Drawing.Drawing2D.PathGradientBrush($pathDome)
    $pbr.CenterPoint = New-Object System.Drawing.PointF(($cx - 35), ($cy - 40))
    $pbr.CenterColor = $cDomeBase
    $pbr.SurroundColors = @($cDomeShade)
    $g.FillEllipse($pbr, $rectDome)
    $g.DrawEllipse($penThick, $rectDome)

    # --- Geodesic Panel Seam Network ---
    # Concentric geodesic rings
    $g.DrawEllipse($penDomeGeodesic, ($cx - 40), ($cy - 40), 80, 80)
    $g.DrawEllipse($penDomeGeodesic, ($cx - 80), ($cy - 80), 160, 160)

    # Radial geodesic facet struts
    for ($angle = 0; $angle -lt 360; $angle += 30) {
        $rad = $angle * [Math]::PI / 180.0
        $x1 = $cx + [int](40 * [Math]::Cos($rad))
        $y1 = $cy + [int](40 * [Math]::Sin($rad))
        $x2 = $cx + [int](80 * [Math]::Cos($rad + 0.25))
        $y2 = $cy + [int](80 * [Math]::Sin($rad + 0.25))
        $x3 = $cx + [int](124 * [Math]::Cos($rad))
        $y3 = $cy + [int](124 * [Math]::Sin($rad))

        $g.DrawLine($penDomeGeodesic, $x1, $y1, $x2, $y2)
        $g.DrawLine($penDomeGeodesic, $x2, $y2, $x3, $y3)
    }

    # Circular base retaining ring / collar
    $g.FillRectangle($brushDoor, ($cx - 105), ($cy + 85), 210, 18)
    $g.DrawRectangle($penThin, ($cx - 105), ($cy + 85), 210, 18)

    # --- Maintenance Access Hatch / Blast Door ---
    $g.FillRectangle($brushDoor, 160, 275, 64, 65)
    $g.DrawRectangle($penThick, 160, 275, 64, 65)
    # Hazard yellow/black header strip
    $brushHaza = New-Object System.Drawing.SolidBrush($cAmberHaza)
    $g.FillRectangle($brushHaza, 160, 275, 64, 10)
    $g.DrawRectangle($penThin, 160, 275, 64, 10)
    for ($hx = 164; $hx -le 216; $hx += 8) {
        $g.DrawLine($penThin, $hx, 285, ($hx + 5), 275)
    }
    # Door seam & handle wheel
    $g.DrawLine($penThin, 192, 285, 192, 340)
    $g.DrawEllipse($penThin, 188, 308, 8, 8)

    # --- Apex Strobe Beacon & Lightning Rod ---
    $g.FillRectangle($brushDoor, 185, 45, 14, 12)
    $g.DrawRectangle($penThin, 185, 45, 14, 12)

    $brushBeacon = New-Object System.Drawing.SolidBrush($cBeaconRed)
    $g.FillEllipse($brushBeacon, 186, 33, 12, 12)
    $g.DrawEllipse($penThin, 186, 33, 12, 12)

    $penRod = New-Object System.Drawing.Pen($cDarkBorder, 3)
    $g.DrawLine($penRod, 192, 12, 192, 33)

    $savePath = Join-Path $outputDir "RadarDomeSmall.png"
    $c.Bitmap.Save($savePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $c.Graphics.Dispose()
    $c.Bitmap.Dispose()
    Write-Host "Created: $savePath"
}

# ==============================================================================
# 3. Phased Radar Array (Geoscape_RadarLarge) - 512x512 (4x4 footprint)
# Spacer tech: massive steerable AESA panel, hydraulic actuators, cooling fins
# Multi-directional: _south, _north, _east, _west
# ==============================================================================
function Generate-PhasedArrayLarge {
    $dirs = @("south", "north", "east", "west")

    $cBasePlasteel = [System.Drawing.Color]::FromArgb(255, 45, 55, 72)
    $cPanelDark    = [System.Drawing.Color]::FromArgb(255, 26, 32, 44)
    $cFrameSpacer  = [System.Drawing.Color]::FromArgb(255, 90, 105, 125)
    $cFrameHi      = [System.Drawing.Color]::FromArgb(255, 140, 160, 185)
    $cAesaGlow     = [System.Drawing.Color]::FromArgb(255, 0, 229, 255)
    $cAesaDim      = [System.Drawing.Color]::FromArgb(255, 0, 140, 170)
    $cDarkBorder   = [System.Drawing.Color]::FromArgb(255, 18, 22, 28)
    $cHydraulic    = [System.Drawing.Color]::FromArgb(255, 200, 210, 220)

    $penThick = New-Object System.Drawing.Pen($cDarkBorder, 5)
    $penThin  = New-Object System.Drawing.Pen($cDarkBorder, 2)
    $penGlow  = New-Object System.Drawing.Pen($cAesaGlow, 2)

    foreach ($dir in $dirs) {
        $c = New-Canvas 512 512
        $g = $c.Graphics

        # 1. Base Pedestal Ring (Common across all rotations)
        $brushPlasteel = New-Object System.Drawing.SolidBrush($cBasePlasteel)
        $brushFrame    = New-Object System.Drawing.SolidBrush($cFrameSpacer)
        $brushFrameHi  = New-Object System.Drawing.SolidBrush($cFrameHi)
        $brushHyd      = New-Object System.Drawing.SolidBrush($cHydraulic)

        # Ground Octagonal Turntable Foundation
        $basePts = [System.Drawing.Point[]]@(
            (New-Object System.Drawing.Point(140, 380)),
            (New-Object System.Drawing.Point(372, 380)),
            (New-Object System.Drawing.Point(445, 425)),
            (New-Object System.Drawing.Point(445, 465)),
            (New-Object System.Drawing.Point(372, 495)),
            (New-Object System.Drawing.Point(140, 495)),
            (New-Object System.Drawing.Point(67, 465)),
            (New-Object System.Drawing.Point(67, 425))
        )
        $g.FillPolygon($brushPlasteel, $basePts)
        $g.DrawPolygon($penThick, $basePts)

        # Turntable Ring Bearing Gear
        $g.FillEllipse($brushFrame, 136, 395, 240, 75)
        $g.DrawEllipse($penThin, 136, 395, 240, 75)

        # Trunnion Yoke Support Arm
        $g.FillRectangle($brushPlasteel, 196, 335, 120, 75)
        $g.DrawRectangle($penThick, 196, 335, 120, 75)

        if ($dir -eq "south") {
            # ARRAY TILTED FORWARD TOWARDS PLAYER - FULL AESA FACE VISIBLE
            # Dual hydraulic lift rams
            $g.FillRectangle($brushHyd, 175, 300, 22, 60)
            $g.DrawRectangle($penThin, 175, 300, 22, 60)
            $g.FillRectangle($brushHyd, 315, 300, 22, 60)
            $g.DrawRectangle($penThin, 315, 300, 22, 60)

            # Octagonal AESA Panel Frame
            $panelPts = [System.Drawing.Point[]]@(
                (New-Object System.Drawing.Point(135, 80)),
                (New-Object System.Drawing.Point(377, 80)),
                (New-Object System.Drawing.Point(440, 160)),
                (New-Object System.Drawing.Point(440, 270)),
                (New-Object System.Drawing.Point(377, 340)),
                (New-Object System.Drawing.Point(135, 340)),
                (New-Object System.Drawing.Point(72, 270)),
                (New-Object System.Drawing.Point(72, 160))
            )
            $g.FillPolygon($brushFrame, $panelPts)
            $g.DrawPolygon($penThick, $panelPts)

            # Inner AESA Dark Absorber Bed
            $bedPts = [System.Drawing.Point[]]@(
                (New-Object System.Drawing.Point(150, 100)),
                (New-Object System.Drawing.Point(362, 100)),
                (New-Object System.Drawing.Point(415, 170)),
                (New-Object System.Drawing.Point(415, 255)),
                (New-Object System.Drawing.Point(362, 320)),
                (New-Object System.Drawing.Point(150, 320)),
                (New-Object System.Drawing.Point(97, 255)),
                (New-Object System.Drawing.Point(97, 170))
            )
            $brushDark = New-Object System.Drawing.SolidBrush($cPanelDark)
            $g.FillPolygon($brushDark, $bedPts)
            $g.DrawPolygon($penThin, $bedPts)

            # High-density Active Array Transmitter Emitter Grid
            $brushAesa = New-Object System.Drawing.SolidBrush($cAesaGlow)
            $brushDim  = New-Object System.Drawing.SolidBrush($cAesaDim)
            for ($gx = 120; $gx -le 392; $gx += 16) {
                for ($gy = 118; $gy -le 302; $gy += 14) {
                    # Clip corners roughly
                    if ([Math]::Abs($gx - 256) + [Math]::Abs($gy - 210) -lt 210) {
                        $b = if (($gx + $gy) % 32 -eq 0) { $brushAesa } else { $brushDim }
                        $g.FillRectangle($b, $gx, $gy, 8, 7)
                    }
                }
            }
            # Center Calibration Horn
            $g.FillEllipse($brushHyd, 248, 202, 16, 16)
            $g.DrawEllipse($penThin, 248, 202, 16, 16)

        } elseif ($dir -eq "north") {
            # ARRAY FACING AWAY - BACK SUPERSTRUCTURE & COOLING HEATSINKS
            # Giant rear truss frame
            $panelPts = [System.Drawing.Point[]]@(
                (New-Object System.Drawing.Point(135, 80)),
                (New-Object System.Drawing.Point(377, 80)),
                (New-Object System.Drawing.Point(440, 160)),
                (New-Object System.Drawing.Point(440, 270)),
                (New-Object System.Drawing.Point(377, 340)),
                (New-Object System.Drawing.Point(135, 340)),
                (New-Object System.Drawing.Point(72, 270)),
                (New-Object System.Drawing.Point(72, 160))
            )
            $g.FillPolygon($brushFrame, $panelPts)
            $g.DrawPolygon($penThick, $panelPts)

            # High-capacity Liquid Cooling Heat Sink Radiator Blocks
            $brushDark = New-Object System.Drawing.SolidBrush($cPanelDark)
            $g.FillRectangle($brushDark, 110, 120, 130, 180)
            $g.DrawRectangle($penThin, 110, 120, 130, 180)
            $g.FillRectangle($brushDark, 272, 120, 130, 180)
            $g.DrawRectangle($penThin, 272, 120, 130, 180)

            # Cooling Fin Slats
            for ($fy = 130; $fy -le 290; $fy += 10) {
                $g.DrawLine($penThin, 115, $fy, 235, $fy)
                $g.DrawLine($penThin, 277, $fy, 397, $fy)
            }

            # Waveguide Conduit Trunk in Center
            $penWaveguide = New-Object System.Drawing.Pen($cHydraulic, 12)
            $g.DrawLine($penWaveguide, 256, 340, 256, 110)
            $g.DrawLine($penThin, 250, 340, 250, 110)
            $g.DrawLine($penThin, 262, 340, 262, 110)

        } elseif ($dir -eq "east") {
            # ARRAY ANGLED RIGHT (FACING EAST)
            # Massive angled cantilever trunnion arm
            $g.FillRectangle($brushFrame, 160, 240, 70, 120)
            $g.DrawRectangle($penThick, 160, 240, 70, 120)

            # Elevation Hydraulic Actuator
            $g.FillRectangle($brushHyd, 140, 280, 26, 70)
            $g.DrawRectangle($penThin, 140, 280, 26, 70)

            # Side profile of array angled upwards towards right
            $dishSidePts = [System.Drawing.Point[]]@(
                (New-Object System.Drawing.Point(220, 70)),
                (New-Object System.Drawing.Point(260, 85)),
                (New-Object System.Drawing.Point(380, 310)),
                (New-Object System.Drawing.Point(340, 335))
            )
            $g.FillPolygon($brushFrame, $dishSidePts)
            $g.DrawPolygon($penThick, $dishSidePts)

            # Glowing emitter array aperture lip on right face
            $penAesaLip = New-Object System.Drawing.Pen($cAesaGlow, 8)
            $g.DrawLine($penAesaLip, 260, 85, 380, 310)

            # Rear cooling heatsink backpack
            $g.FillRectangle($brushPlasteel, 175, 140, 60, 120)
            $g.DrawRectangle($penThin, 175, 140, 60, 120)

        } elseif ($dir -eq "west") {
            # ARRAY ANGLED LEFT (FACING WEST - Mirror of East)
            $g.FillRectangle($brushFrame, 282, 240, 70, 120)
            $g.DrawRectangle($penThick, 282, 240, 70, 120)

            $g.FillRectangle($brushHyd, 346, 280, 26, 70)
            $g.DrawRectangle($penThin, 346, 280, 26, 70)

            $dishSidePts = [System.Drawing.Point[]]@(
                (New-Object System.Drawing.Point(292, 70)),
                (New-Object System.Drawing.Point(252, 85)),
                (New-Object System.Drawing.Point(132, 310)),
                (New-Object System.Drawing.Point(172, 335))
            )
            $g.FillPolygon($brushFrame, $dishSidePts)
            $g.DrawPolygon($penThick, $dishSidePts)

            $penAesaLip = New-Object System.Drawing.Pen($cAesaGlow, 8)
            $g.DrawLine($penAesaLip, 252, 85, 132, 310)

            $g.FillRectangle($brushPlasteel, 277, 140, 60, 120)
            $g.DrawRectangle($penThin, 277, 140, 60, 120)
        }

        $savePath = Join-Path $outputDir "PhasedArrayLarge_$dir.png"
        $c.Bitmap.Save($savePath, [System.Drawing.Imaging.ImageFormat]::Png)
        $c.Graphics.Dispose()
        $c.Bitmap.Dispose()
        Write-Host "Created: $savePath"
    }
}

# ==============================================================================
# 4. SIGINT Decryption Console (Geoscape_SigintConsole) - 384x256 (3x2 footprint)
# Ultra tech: multi-monitor signals intelligence mainframe, comms racks
# Multi-directional: _south, _north, _east, _west
# ==============================================================================
function Generate-SigintConsole {
    $dirs = @("south", "north", "east", "west")

    $cConsoleDesk  = [System.Drawing.Color]::FromArgb(255, 30, 41, 59)
    $cDeskSurface  = [System.Drawing.Color]::FromArgb(255, 51, 65, 85)
    $cDisplayFrame = [System.Drawing.Color]::FromArgb(255, 15, 23, 42)
    $cDarkBorder   = [System.Drawing.Color]::FromArgb(255, 10, 15, 25)
    $cCyanGlow     = [System.Drawing.Color]::FromArgb(255, 6, 182, 212)
    $cGreenGlow    = [System.Drawing.Color]::FromArgb(255, 16, 185, 129)
    $cAmberGlow    = [System.Drawing.Color]::FromArgb(255, 245, 158, 11)
    $cKeyboard     = [System.Drawing.Color]::FromArgb(255, 71, 85, 105)

    $penThick = New-Object System.Drawing.Pen($cDarkBorder, 4)
    $penThin  = New-Object System.Drawing.Pen($cDarkBorder, 2)

    foreach ($dir in $dirs) {
        $c = New-Canvas 384 256
        $g = $c.Graphics

        $brushDesk    = New-Object System.Drawing.SolidBrush($cConsoleDesk)
        $brushSurface = New-Object System.Drawing.SolidBrush($cDeskSurface)
        $brushFrame   = New-Object System.Drawing.SolidBrush($cDisplayFrame)

        if ($dir -eq "south") {
            # FRONT FACING OPERATOR VIEW
            # Main desk console chassis
            $g.FillRectangle($brushDesk, 20, 60, 344, 175)
            $g.DrawRectangle($penThick, 20, 60, 344, 175)

            # Lower equipment bay & cable raceway
            $g.FillRectangle($brushFrame, 35, 180, 314, 45)
            $g.DrawRectangle($penThin, 35, 180, 314, 45)

            # Status LED banks & server cartridge drives
            for ($ledX = 45; $ledX -le 160; $ledX += 16) {
                $bLed = if ($ledX % 32 -eq 0) { New-Object System.Drawing.SolidBrush($cGreenGlow) } else { New-Object System.Drawing.SolidBrush($cAmberGlow) }
                $g.FillRectangle($bLed, $ledX, 192, 8, 6)
            }
            # High-speed data tape / optic storage reels
            $g.DrawEllipse($penThin, 220, 188, 28, 28)
            $g.DrawEllipse($penThin, 260, 188, 28, 28)
            $g.DrawEllipse($penThin, 230, 198, 8, 8)
            $g.DrawEllipse($penThin, 270, 198, 8, 8)

            # Slanted Operator Desktop Surface
            $g.FillRectangle($brushSurface, 28, 110, 328, 65)
            $g.DrawRectangle($penThin, 28, 110, 328, 65)

            # Dual Operator Keyboard / Touchpad Bays
            $brushKey = New-Object System.Drawing.SolidBrush($cKeyboard)
            $g.FillRectangle($brushKey, 60, 125, 90, 40)
            $g.DrawRectangle($penThin, 60, 125, 90, 40)
            $g.FillRectangle($brushKey, 234, 125, 90, 40)
            $g.DrawRectangle($penThin, 234, 125, 90, 40)

            # Three Large Multi-Spectral Surveillance Displays
            # Left Screen: Audio / RF Waterfall Spectrogram
            $g.FillRectangle($brushFrame, 32, 25, 96, 75)
            $g.DrawRectangle($penThick, 32, 25, 96, 75)
            $brushGreen = New-Object System.Drawing.SolidBrush($cGreenGlow)
            $g.FillRectangle($brushGreen, 38, 31, 84, 63)
            # Waterfall bands
            $penWaterfall = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 5, 120, 80), 2)
            for ($wy = 36; $wy -le 88; $wy += 8) {
                $g.DrawLine($penWaterfall, 42, $wy, 118, $wy)
            }

            # Center Screen: Main Tactical Geoscape Wireframe Map
            $g.FillRectangle($brushFrame, 138, 18, 108, 85)
            $g.DrawRectangle($penThick, 138, 18, 108, 85)
            $brushCyan = New-Object System.Drawing.SolidBrush($cCyanGlow)
            $g.FillRectangle($brushCyan, 144, 24, 96, 73)
            # Tactical wireframe grid and target blips
            $penGrid = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 0, 130, 160), 1)
            $g.DrawEllipse($penGrid, 156, 32, 72, 56)
            $g.DrawLine($penGrid, 192, 28, 192, 92)
            $g.DrawLine($penGrid, 148, 60, 236, 60)
            $brushBlip = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 239, 68, 68))
            $g.FillEllipse($brushBlip, 175, 48, 6, 6)
            $g.FillEllipse($brushBlip, 205, 70, 5, 5)

            # Right Screen: Decryption Cypher Matrix Stream
            $g.FillRectangle($brushFrame, 256, 25, 96, 75)
            $g.DrawRectangle($penThick, 256, 25, 96, 75)
            $brushAmber = New-Object System.Drawing.SolidBrush($cAmberGlow)
            $g.FillRectangle($brushAmber, 262, 31, 84, 63)
            # Text line pulses
            $penCypher = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 180, 100, 5), 2)
            for ($cyy = 36; $cyy -le 88; $cyy += 7) {
                $len = [int]((($cyy * 17) % 50) + 25)
                $g.DrawLine($penCypher, 268, $cyy, (268 + $len), $cyy)
            }

        } elseif ($dir -eq "north") {
            # REAR SERVER RACK AND COOLING VENT VIEW
            $g.FillRectangle($brushDesk, 20, 60, 344, 175)
            $g.DrawRectangle($penThick, 20, 60, 344, 175)

            # Server backplane panels with intake vents
            for ($rx = 35; $rx -le 295; $rx += 75) {
                $g.FillRectangle($brushFrame, $rx, 75, 65, 145)
                $g.DrawRectangle($penThin, $rx, 75, 65, 145)

                # Ventilation Louvers
                for ($vy = 85; $vy -le 205; $vy += 8) {
                    $g.DrawLine($penThin, ($rx + 8), $vy, ($rx + 57), $vy)
                }
            }

            # Top Display Hood / Shells
            $g.FillRectangle($brushFrame, 32, 25, 320, 45)
            $g.DrawRectangle($penThick, 32, 25, 320, 45)

            # Heavy shielded conduit trunk running along bottom
            $penConduit = New-Object System.Drawing.Pen($cConsoleDesk, 10)
            $g.DrawLine($penConduit, 20, 228, 364, 228)
            $g.DrawLine($penThin, 20, 223, 364, 223)

        } elseif ($dir -eq "east") {
            # SIDE PROFILE FACING EAST (RIGHT)
            $g.FillRectangle($brushDesk, 80, 60, 224, 175)
            $g.DrawRectangle($penThick, 80, 60, 224, 175)

            # Desk work surface overhang on right
            $g.FillRectangle($brushSurface, 160, 115, 140, 30)
            $g.DrawRectangle($penThin, 160, 115, 140, 30)

            # Slanted monitor pod facing right
            $ptsMon = [System.Drawing.Point[]]@(
                (New-Object System.Drawing.Point(120, 25)),
                (New-Object System.Drawing.Point(230, 25)),
                (New-Object System.Drawing.Point(260, 115)),
                (New-Object System.Drawing.Point(120, 115))
            )
            $g.FillPolygon($brushFrame, $ptsMon)
            $g.DrawPolygon($penThick, $ptsMon)

            # Screen glow edge on right
            $penGlow = New-Object System.Drawing.Pen($cCyanGlow, 6)
            $g.DrawLine($penGlow, 228, 30, 256, 112)

        } elseif ($dir -eq "west") {
            # SIDE PROFILE FACING WEST (LEFT - Mirror of East)
            $g.FillRectangle($brushDesk, 80, 60, 224, 175)
            $g.DrawRectangle($penThick, 80, 60, 224, 175)

            $g.FillRectangle($brushSurface, 84, 115, 140, 30)
            $g.DrawRectangle($penThin, 84, 115, 140, 30)

            $ptsMon = [System.Drawing.Point[]]@(
                (New-Object System.Drawing.Point(264, 25)),
                (New-Object System.Drawing.Point(154, 25)),
                (New-Object System.Drawing.Point(124, 115)),
                (New-Object System.Drawing.Point(264, 115))
            )
            $g.FillPolygon($brushFrame, $ptsMon)
            $g.DrawPolygon($penThick, $ptsMon)

            $penGlow = New-Object System.Drawing.Pen($cCyanGlow, 6)
            $g.DrawLine($penGlow, 156, 30, 128, 112)
        }

        $savePath = Join-Path $outputDir "SigintConsole_$dir.png"
        $c.Bitmap.Save($savePath, [System.Drawing.Imaging.ImageFormat]::Png)
        $c.Graphics.Dispose()
        $c.Bitmap.Dispose()
        Write-Host "Created: $savePath"
    }
}

# ==============================================================================
# 5. Coastal Sonar Station (Geoscape_SonarArray) - 384x384 (3x3 footprint)
# Industrial tech: maritime concrete bunker, heavy hydrophone winch spool, scope
# ==============================================================================
function Generate-SonarStation {
    $c = New-Canvas 384 384
    $g = $c.Graphics

    $cBunkerConc  = [System.Drawing.Color]::FromArgb(255, 78, 86, 96)
    $cBunkerRoof  = [System.Drawing.Color]::FromArgb(255, 110, 120, 132)
    $cSteelDrum   = [System.Drawing.Color]::FromArgb(255, 55, 62, 70)
    $cSeaCable    = [System.Drawing.Color]::FromArgb(255, 24, 28, 34)
    $cSonarGreen  = [System.Drawing.Color]::FromArgb(255, 0, 230, 118)
    $cDarkBorder  = [System.Drawing.Color]::FromArgb(255, 22, 26, 32)
    $cLifeRingRed = [System.Drawing.Color]::FromArgb(255, 239, 68, 68)
    $cWhite       = [System.Drawing.Color]::FromArgb(255, 245, 245, 245)

    $penThick = New-Object System.Drawing.Pen($cDarkBorder, 4)
    $penThin  = New-Object System.Drawing.Pen($cDarkBorder, 2)

    # --- Concrete Maritime Pier / Foundation Pad ---
    $padPts = [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(50, 80)),
        (New-Object System.Drawing.Point(334, 80)),
        (New-Object System.Drawing.Point(354, 345)),
        (New-Object System.Drawing.Point(30, 345))
    )
    $brushConc = New-Object System.Drawing.SolidBrush($cBunkerConc)
    $g.FillPolygon($brushConc, $padPts)
    $g.DrawPolygon($penThick, $padPts)

    # Metal diamond tread decking seams
    $penDeck = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 95, 105, 118), 2)
    for ($dx = 60; $dx -le 320; $dx += 24) {
        $g.DrawLine($penDeck, $dx, 85, ($dx - 10), 340)
    }

    # Mooring bollards on deck edge
    $brushDrum = New-Object System.Drawing.SolidBrush($cSteelDrum)
    $g.FillEllipse($brushDrum, 38, 320, 16, 16)
    $g.DrawEllipse($penThin, 38, 320, 16, 16)
    $g.FillEllipse($brushDrum, 330, 320, 16, 16)
    $g.DrawEllipse($penThin, 330, 320, 16, 16)

    # --- Left: Weatherized Sonar Operators Shelter ---
    $g.FillRectangle($brushConc, 55, 95, 140, 185)
    $g.DrawRectangle($penThick, 55, 95, 140, 185)

    # Reinforced Overhanging Roof
    $brushRoof = New-Object System.Drawing.SolidBrush($cBunkerRoof)
    $g.FillRectangle($brushRoof, 48, 85, 154, 30)
    $g.DrawRectangle($penThick, 48, 85, 154, 30)

    # Circular Acoustic Hydrophone Porthole Display
    $g.FillEllipse($brushDrum, 85, 135, 80, 80)
    $g.DrawEllipse($penThick, 85, 135, 80, 80)
    $brushSonar = New-Object System.Drawing.SolidBrush($cSonarGreen)
    $g.FillEllipse($brushSonar, 93, 143, 64, 64)

    # Sonar CRT radial sweeps & acoustic wave rings
    $penSweep = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 0, 120, 60), 1)
    $g.DrawEllipse($penSweep, 105, 155, 40, 40)
    $g.DrawEllipse($penSweep, 115, 165, 20, 20)
    $g.DrawLine($penSweep, 93, 175, 157, 175)
    $g.DrawLine($penSweep, 125, 143, 125, 207)
    # Bright sweep line
    $penBrightSweep = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 200, 255, 200), 2)
    $g.DrawLine($penBrightSweep, 125, 175, 152, 150)

    # Marine life ring on shelter wall
    $brushRing = New-Object System.Drawing.SolidBrush($cLifeRingRed)
    $g.FillEllipse($brushRing, 108, 230, 34, 34)
    $brushWhite = New-Object System.Drawing.SolidBrush($cWhite)
    $g.FillEllipse($brushWhite, 116, 238, 18, 18)
    $g.FillEllipse($brushConc, 120, 242, 10, 10)
    $g.DrawEllipse($penThin, 108, 230, 34, 34)

    # --- Right: Heavy Subsea Hydrophone Winch Spool ---
    # Winch Frame Stanchions
    $g.FillRectangle($brushDrum, 215, 110, 18, 145)
    $g.DrawRectangle($penThick, 215, 110, 18, 145)
    $g.FillRectangle($brushDrum, 305, 110, 18, 145)
    $g.DrawRectangle($penThick, 305, 110, 18, 145)

    # Main Winch Drum Flanges
    $g.FillEllipse($brushRoof, 222, 120, 94, 94)
    $g.DrawEllipse($penThick, 222, 120, 94, 94)

    # Armored Subsea Cable Coiled on Spool
    $brushCable = New-Object System.Drawing.SolidBrush($cSeaCable)
    $g.FillEllipse($brushCable, 234, 132, 70, 70)
    $g.DrawEllipse($penThin, 234, 132, 70, 70)

    # Winch Drive Motor Housing
    $g.FillRectangle($brushConc, 235, 235, 70, 45)
    $g.DrawRectangle($penThin, 235, 235, 70, 45)

    # Cable Guide Roller Chute Extending Off Pier
    $g.FillRectangle($brushDrum, 252, 275, 34, 75)
    $g.DrawRectangle($penThick, 252, 275, 34, 75)
    # Cable feeding into ocean
    $penBigCable = New-Object System.Drawing.Pen($cSeaCable, 12)
    $g.DrawLine($penBigCable, 269, 200, 269, 365)
    $g.DrawLine($penThin, 263, 200, 263, 365)
    $g.DrawLine($penThin, 275, 200, 275, 365)

    # --- Roof Antenna Mast with Weather Reflector ---
    $g.DrawLine($penThick, 125, 85, 125, 20)
    # Dipole cross arms
    $g.DrawLine($penThin, 110, 35, 140, 35)
    $g.DrawLine($penThin, 115, 50, 135, 50)
    # Navigation Beacon Light at mast head
    $brushNavLight = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 245, 158, 11))
    $g.FillEllipse($brushNavLight, 120, 14, 10, 10)
    $g.DrawEllipse($penThin, 120, 14, 10, 10)

    $savePath = Join-Path $aquaticsBuildingDir "SonarStation.png"
    $c.Bitmap.Save($savePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $c.Graphics.Dispose()
    $c.Bitmap.Dispose()
    Write-Host "Created: $savePath"
}

# Run generation
Generate-LookoutTower
Generate-RadarDomeSmall
Generate-PhasedArrayLarge
Generate-SigintConsole
Generate-SonarStation
