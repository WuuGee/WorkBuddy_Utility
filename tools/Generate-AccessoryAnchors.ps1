<#
Measures the head and neck anchor of every frame of every animation of the Cat sheets and writes
src\FocusCatAI.Presentation\Assets\Accessories\anchors.json.

Head anchor = the top-most opaque pixel of the cat in the column midway between the two ears
(the forehead surface). Neck anchor = a fixed distance below it. Ears are the columns whose top pixel is
within one row of the highest point (tail columns x < 18 and detached sprites such as the Zzz are ignored).
The unit test AccessoryAlignmentTests re-checks the result against all three pets.
#>
param([int]$NeckDrop = 26)

Add-Type -AssemblyName System.Drawing
$root = Join-Path $PSScriptRoot '..\src\FocusCatAI.Presentation\Assets'
$petDir = Join-Path $root 'Pets\Cat'
$manifest = Get-Content (Join-Path $petDir 'pet.json') -Raw | ConvertFrom-Json

function Get-Mask($bmp, $frame) {
    $m = New-Object 'bool[,]' 64, 64
    for ($y = 0; $y -lt 64; $y++) { for ($x = 0; $x -lt 64; $x++) { $m[$x, $y] = $bmp.GetPixel($frame * 64 + $x, $y).A -gt 40 } }
    return ,$m
}

function Get-LargestComponent($m) {
    $seen = New-Object 'int[,]' 64, 64
    $best = $null; $bestSize = 0; $id = 0
    for ($y = 0; $y -lt 64; $y++) { for ($x = 0; $x -lt 64; $x++) {
        if (-not $m[$x, $y] -or $seen[$x, $y]) { continue }
        $id++; $cells = New-Object System.Collections.Generic.List[object]
        $stack = New-Object System.Collections.Generic.Stack[object]; $stack.Push(@($x, $y)); $seen[$x, $y] = $id
        while ($stack.Count) {
            $p = $stack.Pop(); $cells.Add($p)
            for ($dy = -1; $dy -le 1; $dy++) { for ($dx = -1; $dx -le 1; $dx++) {
                $nx = $p[0] + $dx; $ny = $p[1] + $dy
                if ($nx -ge 0 -and $ny -ge 0 -and $nx -lt 64 -and $ny -lt 64 -and $m[$nx, $ny] -and -not $seen[$nx, $ny]) { $seen[$nx, $ny] = $id; $stack.Push(@($nx, $ny)) }
            } }
        }
        if ($cells.Count -gt $bestSize) { $bestSize = $cells.Count; $best = $cells }
    } }
    $r = New-Object 'bool[,]' 64, 64
    foreach ($c in $best) { $r[$c[0], $c[1]] = $true }
    return ,$r
}

$sheets = @{}
$animations = [ordered]@{}
foreach ($prop in $manifest.animations.PSObject.Properties) {
    $name = $prop.Name; $def = $prop.Value
    if (-not $sheets.ContainsKey($def.sheet)) { $sheets[$def.sheet] = New-Object Drawing.Bitmap (Join-Path $petDir $def.sheet) }
    $bmp = $sheets[$def.sheet]
    $frames = @()
    for ($f = 0; $f -lt $def.frames; $f++) {
        $m = Get-LargestComponent (Get-Mask $bmp $f)
        $top = @(64) * 64
        for ($x = 0; $x -lt 64; $x++) { for ($y = 0; $y -lt 64; $y++) { if ($m[$x, $y]) { $top[$x] = $y; break } } }

        $e1 = 22; for ($x = 22; $x -lt 64; $x++) { if ($top[$x] -lt $top[$e1]) { $e1 = $x } }
        $e2 = -1; for ($x = 22; $x -lt 64; $x++) { $d = [Math]::Abs($x - $e1); if ($d -ge 18 -and $d -le 32 -and ($e2 -lt 0 -or $top[$x] -lt $top[$e2])) { $e2 = $x } }
        $cx = [int][Math]::Round(($e1 + $e2) / 2.0, [MidpointRounding]::AwayFromZero)
        $hy = $top[$cx]
        $frames += [ordered]@{ hx = $cx; hy = $hy; nx = $cx; ny = [Math]::Min(63, $hy + $NeckDrop) }
    }
    $animations[$name] = $frames
}
foreach ($b in $sheets.Values) { $b.Dispose() }

$out = Join-Path $root 'Accessories\anchors.json'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$lines = @('{', '  "frameSize": 64,', '  "animations": {')
$i = 0
foreach ($kv in $animations.GetEnumerator()) {
    $i++
    $items = ($kv.Value | ForEach-Object { "{ ""hx"": $($_.hx), ""hy"": $($_.hy), ""nx"": $($_.nx), ""ny"": $($_.ny) }" }) -join ', '
    $comma = if ($i -lt $animations.Count) { ',' } else { '' }
    $lines += "    ""$($kv.Key)"": [ $items ]$comma"
}
$lines += '  }'; $lines += '}'
Set-Content -Path $out -Value $lines -Encoding UTF8
"Wrote $out"
$animations.GetEnumerator() | ForEach-Object { "{0,-10} {1}" -f $_.Key, (($_.Value | ForEach-Object { "($($_.hx),$($_.hy))" }) -join ' ') }

