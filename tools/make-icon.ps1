# Uygulama simgesini (src/SqlYedekci/Assets/app.ico) yeniden üretir.
# Kullanım: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\src\SqlYedekci\Assets\app.ico'
New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 64.0

    # Arka plan: koyu lacivert yuvarlak kare
    $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 15, 42, 74))
    $g.FillEllipse($bg, 1 * $s, 1 * $s, 62 * $s, 62 * $s)

    # Veritabanı silindiri
    $body = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 56, 189, 248))
    $top = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 186, 230, 253))
    $line = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 15, 42, 74)), ([Math]::Max(1, 1.8 * $s))
    $g.FillRectangle($body, 16 * $s, 18 * $s, 32 * $s, 26 * $s)
    $g.FillEllipse($body, 16 * $s, 38 * $s, 32 * $s, 12 * $s)
    $g.DrawArc($line, 16 * $s, 26 * $s, 32 * $s, 12 * $s, 0, 180)
    $g.FillEllipse($top, 16 * $s, 12 * $s, 32 * $s, 12 * $s)

    # Yedek oku (sarı, sağ alt)
    $arrow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 250, 204, 21))
    $g.FillEllipse($arrow, 36 * $s, 36 * $s, 22 * $s, 22 * $s)
    $pts = @(
        (New-Object System.Drawing.PointF (42 * $s), (44 * $s)),
        (New-Object System.Drawing.PointF (52 * $s), (44 * $s)),
        (New-Object System.Drawing.PointF (47 * $s), (52 * $s))
    )
    $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 15, 42, 74))
    $g.FillRectangle($dark, 45 * $s, 39 * $s, 4 * $s, 6 * $s)
    $g.FillPolygon($dark, [System.Drawing.PointF[]]$pts)

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($sz in $sizes) { , (New-IconPng $sz) }

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $data = $images[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $images) { $bw.Write([byte[]]$data) }
$bw.Close()
Write-Host "Simge oluşturuldu: $out"
