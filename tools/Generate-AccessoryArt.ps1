<#
Draws the accessory sprites into src\FocusCatAI.Presentation\Assets\Accessories\*.png.
Same dark outline (#3A2420) as the cat sheets. Each sprite is tiny; placement comes from anchors.json plus AccessoryPlacer.
#>
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\FocusCatAI.Presentation\Assets\Accessories'
New-Item -ItemType Directory -Force $out | Out-Null

$palette = @{
    'O' = [Drawing.Color]::FromArgb(255, 0x3A, 0x24, 0x20)
    'R' = [Drawing.Color]::FromArgb(255, 0xD8, 0x40, 0x3C)
    'D' = [Drawing.Color]::FromArgb(255, 0x8E, 0x2A, 0x2A)
    'H' = [Drawing.Color]::FromArgb(255, 0xF2, 0x7A, 0x6E)
    'P' = [Drawing.Color]::FromArgb(255, 0xC7, 0x5F, 0xD0)
    'W' = [Drawing.Color]::FromArgb(255, 0xFF, 0xFF, 0xFF)
    'Y' = [Drawing.Color]::FromArgb(255, 0xFF, 0xD3, 0x4D)
}

$sprites = [ordered]@{
    'bowtie' = @(
        '.OOO.O.OOO.',
        'ORRRODORRRO',
        'ORHRODORHRO',
        'ORRRODORRRO',
        '.OOO.O.OOO.')
    'party_hat' = @(
        '...OYO...',
        '...OPO...',
        '..OPWPO..',
        '..OPPPO..',
        '.OPPWPPO.',
        '.OPPPPPO.',
        'OPPWPPPPO',
        'OOOOOOOOO')
    'star_halo' = @(
        '....OOOOO....',
        '..OOYYYYYOO..',
        '.OYYO...OYYO.',
        '..OOYYYYYOO..',
        '....OOOOO....')
}

foreach ($name in $sprites.Keys) {
    $rows = $sprites[$name]
    $w = $rows[0].Length
    foreach ($r in $rows) { if ($r.Length -ne $w) { throw "${name}: ragged row" } }
    $bmp = New-Object Drawing.Bitmap $w, $rows.Count, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $rows.Count; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $ch = [string]$rows[$y][$x]
            $c = if ($ch -eq '.') { [Drawing.Color]::Transparent } else { $palette[$ch] }
            $bmp.SetPixel($x, $y, $c)
        }
    }
    $path = Join-Path $out "$name.png"
    $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    "{0}: {1}x{2}" -f $name, $w, $rows.Count
}

