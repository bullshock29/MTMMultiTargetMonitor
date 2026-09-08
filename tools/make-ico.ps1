# Regenerates the app icon from tools/app-source.jpg.
# Run:  pwsh tools/make-ico.ps1
$assets  = Join-Path $PSScriptRoot '..\src\MultiTargetMonitor\Assets'
$SrcPath = Join-Path $PSScriptRoot 'app-source.jpg'
$OutIco  = Join-Path $assets 'app.ico'
$OutPng  = Join-Path $assets 'app-256.png'

Add-Type -AssemblyName System.Drawing

$raw = [System.Drawing.Bitmap]::FromFile($SrcPath)

# autocrop near-white margins, then re-pad to a square with a small even border
$minX = $raw.Width; $minY = $raw.Height; $maxX = 0; $maxY = 0
$step = 3
for ($y = 0; $y -lt $raw.Height; $y += $step) {
    for ($x = 0; $x -lt $raw.Width; $x += $step) {
        $p = $raw.GetPixel($x, $y)
        if (($p.R -lt 245) -or ($p.G -lt 245) -or ($p.B -lt 245)) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
$cw = $maxX - $minX + 1; $ch = $maxY - $minY + 1
$side = [Math]::Max($cw, $ch)
$pad = [int]($side * 0.06)
$canvas = $side + 2 * $pad
$img0 = New-Object System.Drawing.Bitmap($canvas, $canvas, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gg = [System.Drawing.Graphics]::FromImage($img0)
$gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gg.Clear([System.Drawing.Color]::White)
$dstX = $pad + [int](($side - $cw) / 2)
$dstY = $pad + [int](($side - $ch) / 2)
$gg.DrawImage($raw,
    (New-Object System.Drawing.Rectangle($dstX, $dstY, $cw, $ch)),
    (New-Object System.Drawing.Rectangle($minX, $minY, $cw, $ch)),
    [System.Drawing.GraphicsUnit]::Pixel)
$gg.Dispose()
$raw.Dispose()
"cropped content ${cw}x${ch}  ->  canvas ${canvas}x${canvas}"

function Get-Resized($src, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::White)
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, $size, $size)))
    $g.Dispose()
    $bmp
}

(Get-Resized $img0 256).Save($OutPng, [System.Drawing.Imaging.ImageFormat]::Png)

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$png = @{}
foreach ($s in $sizes) {
    $ms = New-Object System.IO.MemoryStream
    $b = Get-Resized $img0 $s
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
    $png[$s] = $ms.ToArray()
    $ms.Dispose()
}
$img0.Dispose()

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([uint16]0)
$w.Write([uint16]1)
$w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($s in $sizes) {
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim)
    $w.Write([byte]$dim)
    $w.Write([byte]0)
    $w.Write([byte]0)
    $w.Write([uint16]1)
    $w.Write([uint16]32)
    $w.Write([uint32]$png[$s].Length)
    $w.Write([uint32]$offset)
    $offset += $png[$s].Length
}
foreach ($s in $sizes) { $w.Write($png[$s]) }
$w.Flush()
[System.IO.File]::WriteAllBytes($OutIco, $out.ToArray())
$w.Dispose()

$bytes = [System.IO.File]::ReadAllBytes($OutIco)
$frameCount = [BitConverter]::ToUInt16($bytes, 4)
"OK: $OutIco  ($($bytes.Length) bytes, $frameCount frames: $($sizes -join ', '))"
