<#
  Renders a V1 vs V2 comparison sheet at the real runtime sizes (32 / 44 / 48 px) on light and dark backgrounds.
  Output: docs/art/pet-art-v2/comparison.png
#>
param(
    [string]$V1 = (Join-Path $PSScriptRoot '..\docs\art\pet-art-v1'),
    [string]$V2 = (Join-Path $PSScriptRoot '..\src\FocusCatAI.Presentation\Assets\Pets\Cat'),
    [string]$Out = (Join-Path $PSScriptRoot '..\docs\art\pet-art-v2\comparison.png')
)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null

$sheets = 'idle', 'walk', 'sit', 'blink', 'tailflick', 'lookleft', 'lookright', 'groom', 'stretch', 'sleep', 'thinking', 'happy'
$sizes = 32, 44, 48
$cell = 52
$rowsPerSheet = 2            # light, dark backgrounds
$labelW = 70
$cols = 1 + ($sizes.Count * 2)   # label + (V1,V2) per size
$bmp = New-Object System.Drawing.Bitmap ($labelW + $sizes.Count * 2 * $cell), ($sheets.Count * $rowsPerSheet * $cell + 24)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Gray)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$font = New-Object System.Drawing.Font 'Segoe UI', 8
$g.DrawString('V1 | V2 at 32px, 44px, 48px', $font, [System.Drawing.Brushes]::White, 2, 4)

function Frame([string]$dir, [string]$name) {
    $path = Join-Path $dir "$name.png"
    $img = [System.Drawing.Image]::FromFile($path)
    $f = New-Object System.Drawing.Bitmap 64, 64
    $fg = [System.Drawing.Graphics]::FromImage($f)
    $fg.DrawImage($img, (New-Object System.Drawing.Rectangle 0, 0, 64, 64), (New-Object System.Drawing.Rectangle 0, 0, 64, 64), [System.Drawing.GraphicsUnit]::Pixel)
    $fg.Dispose(); $img.Dispose()
    return $f
}

$y = 24
foreach ($name in $sheets) {
    $a = Frame $V1 $name
    $b = Frame $V2 $name
    foreach ($bgName in 'light', 'dark') {
        $bg = if ($bgName -eq 'light') { [System.Drawing.Color]::FromArgb(243, 243, 243) } else { [System.Drawing.Color]::FromArgb(32, 32, 32) }
        $fg = if ($bgName -eq 'light') { [System.Drawing.Brushes]::Black } else { [System.Drawing.Brushes]::White }
        $g.FillRectangle((New-Object System.Drawing.SolidBrush $bg), 0, $y, $bmp.Width, $cell)
        $g.DrawString("$name`n$bgName", $font, $fg, 2, $y + 8)
        $x = $labelW
        foreach ($s in $sizes) {
            foreach ($img in $a, $b) {
                $g.DrawImage($img, (New-Object System.Drawing.Rectangle ($x + 2), ($y + $cell - $s - 2), $s, $s))
                $x += $cell
            }
        }
        $y += $cell
    }
    $a.Dispose(); $b.Dispose()
}
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "Wrote $Out"
