# Generates Phast's application icon (multi-size .ico + preview PNGs) with System.Drawing.
# Usage (Windows PowerShell 5.1 or PowerShell 7 on Windows):
#   powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
# Output: src\Phast\Assets\phast.ico, src\Phast\Assets\phast-256.png, src\Phast\Pages\img\phast.png

Add-Type -AssemblyName System.Drawing

$root   = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "src\Phast\Assets"
$pages  = Join-Path $root "src\Phast\Pages\img"
New-Item -ItemType Directory -Force $assets, $pages | Out-Null

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Draws the logo at an arbitrary pixel size and returns PNG bytes.
function Get-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [float]$size
    $m = [Math]::Max(0.5, $s * 0.03)           # outer margin
    $bg = New-RoundedRect $m $m ($s - 2 * $m) ($s - 2 * $m) ($s * 0.23)

    # Diagonal blue -> violet gradient background.
    $c1 = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)    # #2563EB
    $c2 = [System.Drawing.Color]::FromArgb(255, 124, 58, 237)   # #7C3AED
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $s, $s), $c1, $c2
    $g.FillPath($brush, $bg)

    # Slanted "P": stem + bowl, built from a path and sheared to the right for a sense of speed.
    $white = [System.Drawing.Brushes]::White
    $stemX = $s * 0.40; $top = $s * 0.22; $bottom = $s * 0.80
    $stemW = $s * 0.13
    $bowlR = $s * 0.185                       # outer radius of the bowl
    $bowlCx = $stemX + $stemW * 0.5 + $s * 0.10; $bowlCy = $top + $bowlR
    # Outer contour = union of stem, bowl box and bowl circle
    $outer = New-Object System.Drawing.Region (New-Object System.Drawing.RectangleF $stemX, $top, $stemW, ($bottom - $top))
    $outer.Union((New-Object System.Drawing.RectangleF $stemX, $top, ($bowlCx - $stemX), ($bowlR * 2)))
    $circle = New-Object System.Drawing.Drawing2D.GraphicsPath
    $circle.AddEllipse($bowlCx - $bowlR, $bowlCy - $bowlR, $bowlR * 2, $bowlR * 2)
    $outer.Union($circle)
    # Hole of the bowl
    $hr = $bowlR * 0.45
    $holeCircle = New-Object System.Drawing.Drawing2D.GraphicsPath
    $holeCircle.AddEllipse($bowlCx - $hr, $bowlCy - $hr, $hr * 2, $hr * 2)
    $outer.Exclude($holeCircle)
    $outer.Exclude((New-Object System.Drawing.RectangleF ($stemX + $stemW), ($bowlCy - $hr), ($bowlCx - $stemX - $stemW), ($hr * 2)))

    $shear = New-Object System.Drawing.Drawing2D.Matrix 1, 0, -0.18, 1, ($s * 0.10), 0
    $outer.Transform($shear)
    $g.FillRegion($white, $outer)

    # Speed lines to the left of the letter (skipped on tiny sizes where they become noise).
    if ($size -ge 24) {
        $lineBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(210, 255, 255, 255))
        $h = [Math]::Max(1.5, $s * 0.055)
        foreach ($l in @(@(0.17, 0.36, 0.17), @(0.11, 0.50, 0.23), @(0.17, 0.64, 0.17))) {
            $lp = New-RoundedRect ($s * $l[0]) ($s * $l[1]) ($s * $l[2]) $h ($h / 2)
            $g.FillPath($lineBrush, $lp)
        }
    }

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

# Assemble a PNG-compressed multi-resolution ICO file.
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = @{}
foreach ($sz in $sizes) { $images[$sz] = Get-IconPng $sz }

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($sz in $sizes) {
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$sz].Length); $w.Write([UInt32]$offset)
    $offset += $images[$sz].Length
}
foreach ($sz in $sizes) { $w.Write($images[$sz]) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets "phast.ico"), $out.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $assets "phast-256.png"), $images[256])
[System.IO.File]::WriteAllBytes((Join-Path $pages "phast.png"), (Get-IconPng 128))
Write-Host "Icon written to $assets"
