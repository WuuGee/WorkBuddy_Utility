<#
  Generates the placeholder pixel-art cat sprite sheets + manifest.
  Logical art is 32x32, upscaled 2x (nearest neighbour) to 64x64 frames, one horizontal strip per animation.
  Run:  pwsh tools/GenerateCatSprites.ps1     (or powershell.exe)
  Artists can simply replace the PNGs (and edit pet.json) - no code changes required.
#>
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$outDir = Join-Path $PSScriptRoot '..\src\FocusCatAI.Presentation\Assets\Pets\Cat'
New-Item -ItemType Directory -Force $outDir | Out-Null
$outDir = (Resolve-Path $outDir).Path

function Col($r, $g, $b) { [System.Drawing.Color]::FromArgb(255, $r, $g, $b) }
$script:K = @{
    orange = Col 243 156 64;  dark = Col 205 108 40;  cream = Col 255 236 204
    black  = Col 32 22 28;    pink = Col 255 150 165; white = Col 255 255 255
    blush  = Col 255 176 170; gray = Col 120 120 140; red   = Col 230 60 80
    blue   = Col 90 150 235
}
$script:G = $null
$script:oy = 0

function SetPx($x, $y, $c) {
    $y += $script:oy
    if ($x -ge 0 -and $x -lt 32 -and $y -ge 0 -and $y -lt 32) { $script:G[$y * 32 + $x] = $c }
}
function FillRect($x0, $y0, $x1, $y1, $c) {
    for ($y = $y0; $y -le $y1; $y++) { for ($x = $x0; $x -le $x1; $x++) { SetPx $x $y $c } }
}
function FillRound($x0, $y0, $x1, $y1, $c) {
    for ($y = $y0; $y -le $y1; $y++) {
        for ($x = $x0; $x -le $x1; $x++) {
            $corner = ($x -eq $x0 -or $x -eq $x1) -and ($y -eq $y0 -or $y -eq $y1)
            if (-not $corner) { SetPx $x $y $c }
        }
    }
}

function Draw-Cat {
    param($Pose = 'stand', $Dy = 0, $Hy = 0, $Hx = 0, $Eyes = 'open', $Mouth = 'closed',
          $Tail = 0, $LiftL = 0, $LiftR = 0, $Reach = $false)
    $k = $script:K
    $script:oy = $Dy

    # tail
    $by = if ($Pose -eq 'stand') { 22 } else { 24 }
    $tx = @(22, 23, 24, 24, 25, 25)
    $wave = @(0, 1, 0, -1)[$Tail % 4]
    for ($i = 0; $i -lt 6; $i++) {
        $x = $tx[$i] + $(if ($i -ge 3) { $wave } else { 0 })
        $c = if ($i -eq 5) { $k.cream } else { $k.orange }
        FillRect $x ($by - $i) ($x + 1) ($by - $i) $c
    }

    # body
    if ($Pose -eq 'stand') {
        FillRound 9 16 21 23 $k.orange
        FillRound 13 18 17 23 $k.cream
        FillRect 10 24 12 (26 - $LiftL) $k.orange; FillRect 10 (26 - $LiftL) 12 (26 - $LiftL) $k.cream
        FillRect 18 24 20 (26 - $LiftR) $k.orange; FillRect 18 (26 - $LiftR) 20 (26 - $LiftR) $k.cream
    } else {
        FillRound 9 17 21 27 $k.orange
        FillRound 13 20 17 27 $k.cream
        FillRect 10 26 12 27 $k.cream; FillRect 18 26 20 27 $k.cream
        if ($Reach) {
            FillRect 18 15 22 17 $k.orange
            FillRect 4 24 10 25 $k.orange
            FillRect 2 26 10 27 $k.cream
        }
    }

    # head
    FillRound (7 + $Hx) (6 + $Hy) (23 + $Hx) (17 + $Hy) $k.orange
    FillRect (8 + $Hx) (5 + $Hy) (11 + $Hx) (5 + $Hy) $k.orange; FillRect (8 + $Hx) (4 + $Hy) (10 + $Hx) (4 + $Hy) $k.orange; FillRect (8 + $Hx) (3 + $Hy) (9 + $Hx) (3 + $Hy) $k.orange
    FillRect (19 + $Hx) (5 + $Hy) (22 + $Hx) (5 + $Hy) $k.orange; FillRect (20 + $Hx) (4 + $Hy) (22 + $Hx) (4 + $Hy) $k.orange; FillRect (21 + $Hx) (3 + $Hy) (22 + $Hx) (3 + $Hy) $k.orange
    SetPx (9 + $Hx) (4 + $Hy) $k.pink; SetPx (9 + $Hx) (5 + $Hy) $k.pink
    SetPx (21 + $Hx) (4 + $Hy) $k.pink; SetPx (21 + $Hx) (5 + $Hy) $k.pink
    SetPx (15 + $Hx) (6 + $Hy) $k.dark; SetPx (15 + $Hx) (7 + $Hy) $k.dark; SetPx (15 + $Hx) (8 + $Hy) $k.dark
    SetPx (12 + $Hx) (7 + $Hy) $k.dark; SetPx (18 + $Hx) (7 + $Hy) $k.dark
    FillRound (12 + $Hx) (12 + $Hy) (18 + $Hx) (16 + $Hy) $k.cream
    FillRect (9 + $Hx) (13 + $Hy) (10 + $Hx) (13 + $Hy) $k.blush; FillRect (20 + $Hx) (13 + $Hy) (21 + $Hx) (13 + $Hy) $k.blush

    # eyes
    switch ($Eyes) {
        'open' {
            FillRect (10 + $Hx) (10 + $Hy) (11 + $Hx) (12 + $Hy) $k.black; SetPx (10 + $Hx) (10 + $Hy) $k.white
            FillRect (19 + $Hx) (10 + $Hy) (20 + $Hx) (12 + $Hy) $k.black; SetPx (19 + $Hx) (10 + $Hy) $k.white
        }
        'closed' {
            FillRect (10 + $Hx) (11 + $Hy) (11 + $Hx) (11 + $Hy) $k.black
            FillRect (19 + $Hx) (11 + $Hy) (20 + $Hx) (11 + $Hy) $k.black
        }
        'happy' {
            SetPx (9 + $Hx) (12 + $Hy) $k.black; SetPx (10 + $Hx) (11 + $Hy) $k.black; SetPx (11 + $Hx) (12 + $Hy) $k.black
            SetPx (19 + $Hx) (12 + $Hy) $k.black; SetPx (20 + $Hx) (11 + $Hy) $k.black; SetPx (21 + $Hx) (12 + $Hy) $k.black
        }
    }

    # nose + mouth
    SetPx (15 + $Hx) (13 + $Hy) $k.pink
    if ($Mouth -eq 'open') {
        FillRect (14 + $Hx) (14 + $Hy) (16 + $Hx) (15 + $Hy) $k.black; SetPx (15 + $Hx) (15 + $Hy) $k.pink
    } else {
        SetPx (14 + $Hx) (14 + $Hy) $k.black; SetPx (16 + $Hx) (14 + $Hy) $k.black
    }
}

function Apply-Outline {
    $copy = $script:G.Clone()
    for ($y = 0; $y -lt 32; $y++) {
        for ($x = 0; $x -lt 32; $x++) {
            if ($null -ne $copy[$y * 32 + $x]) { continue }
            $n = ($x -gt 0 -and $null -ne $copy[$y * 32 + $x - 1]) -or
                 ($x -lt 31 -and $null -ne $copy[$y * 32 + $x + 1]) -or
                 ($y -gt 0 -and $null -ne $copy[($y - 1) * 32 + $x]) -or
                 ($y -lt 31 -and $null -ne $copy[($y + 1) * 32 + $x])
            if ($n) { $script:G[$y * 32 + $x] = $script:K.black }
        }
    }
    $script:oy = 0
}

function Draw-Bubble($dots) {
    $k = $script:K
    FillRound 24 0 31 5 $k.gray; FillRound 25 1 30 4 $k.white
    $xs = @(26, 28, 30)
    for ($i = 0; $i -lt $dots; $i++) { SetPx $xs[$i] 2 $k.black }
    SetPx 24 6 $k.gray
}
function Draw-Note($f) {
    $k = $script:K; $b = 9 - $f
    SetPx 26 $b $k.blue; SetPx 27 $b $k.blue; SetPx 27 ($b - 1) $k.blue; SetPx 27 ($b - 2) $k.blue; SetPx 27 ($b - 3) $k.blue; SetPx 28 ($b - 3) $k.blue
}
function Draw-Z($x, $y) {
    $c = Col 120 170 255
    foreach ($p in @(@(0,0), @(1,0), @(2,0), @(1,1), @(0,2), @(1,2), @(2,2))) { SetPx ($x + $p[0]) ($y + $p[1]) $c }
}
function Draw-Heart($x, $y) {
    $r = $script:K.red
    foreach ($p in @(@(0,0), @(2,0), @(0,1), @(1,1), @(2,1), @(1,2))) { SetPx ($x + $p[0]) ($y + $p[1]) $r }
}

function New-Sheet($name, $frames, [scriptblock]$draw) {
    $bmp = New-Object System.Drawing.Bitmap (64 * $frames), 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($f = 0; $f -lt $frames; $f++) {
        $script:G = New-Object 'object[]' 1024
        $script:oy = 0
        & $draw $f
        for ($y = 0; $y -lt 32; $y++) {
            for ($x = 0; $x -lt 32; $x++) {
                $c = $script:G[$y * 32 + $x]
                if ($null -eq $c) { continue }
                $px = $f * 64 + $x * 2; $py = $y * 2
                $bmp.SetPixel($px, $py, $c); $bmp.SetPixel($px + 1, $py, $c)
                $bmp.SetPixel($px, $py + 1, $c); $bmp.SetPixel($px + 1, $py + 1, $c)
            }
        }
    }
    $bmp.Save((Join-Path $outDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

New-Sheet 'idle' 4 {
    param($f)
    Draw-Cat -Pose stand -Hy @(0, 0, 1, 0)[$f] -Tail $f -Eyes $(if ($f -eq 3) { 'closed' } else { 'open' })
    Apply-Outline
}
New-Sheet 'sit' 4 {
    param($f)
    Draw-Cat -Pose sit -Tail $f -Eyes $(if ($f -eq 2) { 'closed' } else { 'open' })
    Apply-Outline
}
New-Sheet 'walk' 8 {
    param($f)
    Draw-Cat -Pose stand -Hx 1 -Dy $(if ($f % 2 -eq 1) { -1 } else { 0 }) -Tail ([int][math]::Floor($f / 2)) `
        -LiftL @(0, 1, 1, 1, 0, 0, 0, 0)[$f] -LiftR @(0, 0, 0, 0, 0, 1, 1, 1)[$f]
    Apply-Outline
}
New-Sheet 'sleep' 4 {
    param($f)
    Draw-Cat -Pose sit -Hy @(4, 4, 5, 5)[$f] -Eyes closed -Tail 0
    Apply-Outline
    Draw-Z 24 (10 - $f); Draw-Z 28 (5 - [math]::Floor($f / 2))
}
New-Sheet 'happy' 4 {
    param($f)
    $air = @(0, 1, 1, 0)[$f]
    Draw-Cat -Pose stand -Dy @(0, -3, -4, -2)[$f] -Eyes happy -Mouth open -Tail ($f * 2) -LiftL $air -LiftR $air
    Apply-Outline
    Draw-Heart 25 (11 - $f * 2)
}
New-Sheet 'thinking' 4 {
    param($f)
    Draw-Cat -Pose sit -Tail $f -Hy $(if ($f -eq 1) { 1 } else { 0 })
    Apply-Outline
    Draw-Bubble @(1, 2, 3, 2)[$f]
}
New-Sheet 'speaking' 4 {
    param($f)
    Draw-Cat -Pose sit -Tail $f -Mouth $(if ($f % 2 -eq 1) { 'open' } else { 'closed' })
    Apply-Outline
    Draw-Note $f
}
New-Sheet 'stretch' 4 {
    param($f)
    Draw-Cat -Pose sit -Hy @(0, 2, 3, 1)[$f] -Reach ($f -eq 1 -or $f -eq 2) `
        -Eyes $(if ($f -eq 0) { 'open' } else { 'happy' }) -Mouth $(if ($f -eq 2) { 'open' } else { 'closed' }) -Tail $f
    Apply-Outline
}

Write-Host "Sprite sheets written to $outDir"

