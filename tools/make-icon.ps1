# Draws the launcher's icon: three stacked cards ("several accounts") on a slate tile.
# Original artwork on purpose: this is an unofficial tool, so it must not borrow Claude's branding.
# Each size is drawn directly (not downscaled) and stored as a PNG frame in the .ico.
#
#   pwsh tools/make-icon.ps1            -> src/ClaudeMultiAccountLauncher/Assets/AppIcon.ico
param([string]$OutFile = (Join-Path $PSScriptRoot '..\src\ClaudeMultiAccountLauncher\Assets\AppIcon.ico'))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 256.0
    # Tile
    $tile = New-RoundedRect (8 * $s) (8 * $s) (240 * $s) (240 * $s) (52 * $s)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, $size),
        [System.Drawing.Color]::FromArgb(255, 61, 76, 104), [System.Drawing.Color]::FromArgb(255, 36, 45, 64))
    $g.FillPath($grad, $tile)

    # Three cards, back to front; small sizes drop the back card so it stays legible
    $cards = @(
        @{ X = 92; Y = 52;  A = 90 },
        @{ X = 72; Y = 82;  A = 160 },
        @{ X = 52; Y = 112; A = 255 }
    )
    if ($size -le 20) { $cards = $cards[1..2] }

    foreach ($c in $cards) {
        $rect = New-RoundedRect ($c.X * $s) ($c.Y * $s) (112 * $s) (92 * $s) ([Math]::Max(1.5, 16 * $s))
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($c.A, 236, 240, 247))), $rect)
    }

    # Front card: a "launch" chevron
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 47, 59, 82)), ([Math]::Max(1.5, 14 * $s))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.DrawLines($pen, [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF (94 * $s), (138 * $s)),
        (New-Object System.Drawing.PointF (118 * $s), (158 * $s)),
        (New-Object System.Drawing.PointF (94 * $s), (178 * $s))))

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$frames = $sizes | ForEach-Object { , (New-Frame $_) }

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$frames[$i].Length); $w.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }

New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath (Split-Path $OutFile)).Path + '\' + (Split-Path $OutFile -Leaf), $out.ToArray())
"Wrote $OutFile ($($out.Length) bytes, sizes $($sizes -join ', '))"
