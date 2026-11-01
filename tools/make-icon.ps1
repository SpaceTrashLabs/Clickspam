# Renders the app icon (Assets\app.ico + Assets\app-64.png). Re-run after changing the artwork.
param([string]$OutDir = (Join-Path $PSScriptRoot '..\Assets'))

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    # Rounded-square tile with a mint gradient.
    $x = 8; $y = 8; $w = 240; $h = 240; $d = 112
    $tile = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tile.AddArc($x, $y, $d, $d, 180, 90)
    $tile.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $tile.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $tile.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $tile.CloseFigure()
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 256, 256),
        [System.Drawing.Color]::FromArgb(255, 0x6B, 0xEE, 0xB4), [System.Drawing.Color]::FromArgb(255, 0x1F, 0xB5, 0x7A))
    $g.FillPath($bg, $tile)

    $ink = [System.Drawing.Color]::FromArgb(255, 0x0B, 0x1F, 0x16)
    $tipX = 104; $tipY = 96

    # Click ripples above-left of the cursor tip (dropped at tiny sizes where they turn to mush).
    if ($size -gt 24) {
        foreach ($ring in @(@{ R = 34; A = 255 }, @{ R = 60; A = 130 })) {
            $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($ring.A, $ink)), 14
            $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            $r = $ring.R
            $g.DrawArc($pen, $tipX - $r, $tipY - $r, 2 * $r, 2 * $r, 192, 66)
            $pen.Dispose()
        }
    }

    # Cursor arrow.
    $arrow = @(@(0, 0), @(0, 112), @(28, 86), @(46, 126), @(64, 118), @(46, 80), @(82, 80)) |
        ForEach-Object { New-Object System.Drawing.PointF ($tipX + $_[0]), ($tipY + $_[1]) }
    $brush = New-Object System.Drawing.SolidBrush $ink
    $g.FillPolygon($brush, [System.Drawing.PointF[]]$arrow)

    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($ms.ToArray())
    if ($size -eq 64) { $bmp.Save((Join-Path $OutDir 'app-64.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
}

# ICO container with PNG-compressed frames (supported since Windows Vista).
$file = [System.IO.File]::Create((Join-Path $OutDir 'app.ico'))
$wr = New-Object System.IO.BinaryWriter $file
$wr.Write([uint16]0); $wr.Write([uint16]1); $wr.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $wr.Write([byte]$dim); $wr.Write([byte]$dim); $wr.Write([byte]0); $wr.Write([byte]0)
    $wr.Write([uint16]1); $wr.Write([uint16]32)
    $wr.Write([uint32]$images[$i].Length); $wr.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $wr.Write($img) }
$wr.Dispose()
