<#
  Pet Art V2 - generates the cat sprite sheets (src/FocusCatAI.Presentation/Assets/Pets/Cat).
  Chibi mascot: oversized head, compact fluffy body, thick dark-brown outline, cream fur with orange patches,
  upright tail. Frames are 64x64 (same as pet.json), feet touch the bottom row (groundOffset 0).
  Shapes are drawn procedurally (ellipses / polygons / capsule chains), each with a 2px outline, then a faint
  light rim is added so the outline does not vanish on a dark taskbar.

  Usage:  powershell -File tools\Generate-CatSprites.ps1
          powershell -File tools\Generate-CatSprites.ps1 -Preview sit -PreviewFrame 0     (ASCII dump of one frame)
          powershell -File tools\Generate-CatSprites.ps1 -Metrics                          (proportion report)
#>
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\src\FocusCatAI.Presentation\Assets\Pets\Cat'),
    [string]$Preview = '',
    [int]$PreviewFrame = 0,
    [switch]$Metrics
)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Language CSharp -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

public class Canvas
{
    public const int N = 64;
    public int[] Px = new int[N * N];
    public int OX;
    public int OY;
    public int Outline;
    public Dictionary<int, char> Legend = new Dictionary<int, char>();
    public Dictionary<string, int[]> Boxes = new Dictionary<string, int[]>();

    public static int Hex(string s) { return Convert.ToInt32(s.Substring(1), 16) | unchecked((int)0xFF000000); }

    public void Clear() { Array.Clear(Px, 0, Px.Length); OX = 0; OY = 0; Boxes.Clear(); }

    bool In(int x, int y) { return x >= 0 && y >= 0 && x < N && y < N; }

    public bool[] Ellipse(double cx, double cy, double rx, double ry, double deg)
    {
        bool[] m = new bool[N * N];
        cx += OX; cy += OY;
        double a = deg * Math.PI / 180.0;
        double ca = Math.Cos(a), sa = Math.Sin(a);
        int ext = (int)Math.Ceiling(Math.Max(rx, ry)) + 1;
        for (int y = (int)Math.Floor(cy) - ext; y <= (int)Math.Floor(cy) + ext; y++)
        {
            for (int x = (int)Math.Floor(cx) - ext; x <= (int)Math.Floor(cx) + ext; x++)
            {
                if (!In(x, y)) continue;
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                double u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                if ((u * u) / (rx * rx) + (v * v) / (ry * ry) <= 1.0) m[y * N + x] = true;
            }
        }
        return m;
    }

    public bool[] Poly(double[] p)
    {
        bool[] m = new bool[N * N];
        int n = p.Length / 2;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                double px = x + 0.5, py = y + 0.5;
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = p[2 * i] + OX, yi = p[2 * i + 1] + OY, xj = p[2 * j] + OX, yj = p[2 * j + 1] + OY;
                    if (((yi > py) != (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi)) inside = !inside;
                }
                m[y * N + x] = inside;
            }
        }
        return m;
    }

    void Stamp(bool[] m, double cx, double cy, double r)
    {
        for (int y = (int)Math.Floor(cy - r); y <= (int)Math.Ceiling(cy + r); y++)
        {
            for (int x = (int)Math.Floor(cx - r); x <= (int)Math.Ceiling(cx + r); x++)
            {
                if (!In(x, y)) continue;
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                if (dx * dx + dy * dy <= r * r) m[y * N + x] = true;
            }
        }
    }

    // Thick tapered line through the points (used for tails and legs).
    public bool[] Chain(double[] p, double r0, double r1)
    {
        bool[] m = new bool[N * N];
        int segs = p.Length / 2 - 1;
        for (int s = 0; s < segs; s++)
        {
            double x0 = p[2 * s] + OX, y0 = p[2 * s + 1] + OY, x1 = p[2 * s + 2] + OX, y1 = p[2 * s + 3] + OY;
            double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Math.Max(1, (int)Math.Ceiling(len * 2));
            for (int k = 0; k <= steps; k++)
            {
                double t = (double)k / steps;
                double r = r0 + (r1 - r0) * ((s + t) / segs);
                Stamp(m, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, r);
            }
        }
        return m;
    }

    public bool[] Or(bool[] a, bool[] b)
    {
        bool[] m = new bool[N * N];
        for (int i = 0; i < m.Length; i++) m[i] = a[i] || b[i];
        return m;
    }

    public void Commit(bool[] m, int color, string tag, double outR)
    {
        if (outR > 0)
        {
            bool[] d = new bool[N * N];
            int R = (int)Math.Ceiling(outR);
            double r2 = outR * outR;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    if (!m[y * N + x]) continue;
                    for (int dy = -R; dy <= R; dy++)
                    {
                        for (int dx = -R; dx <= R; dx++)
                        {
                            if (dx * dx + dy * dy <= r2 && In(x + dx, y + dy)) d[(y + dy) * N + x + dx] = true;
                        }
                    }
                }
            }
            for (int i = 0; i < d.Length; i++) if (d[i] && !m[i]) Px[i] = Outline;
        }
        for (int i = 0; i < m.Length; i++) if (m[i]) Px[i] = color;
        if (!string.IsNullOrEmpty(tag)) Track(tag, m);
    }

    void Track(string tag, bool[] m)
    {
        int[] b;
        if (!Boxes.TryGetValue(tag, out b)) { b = new int[] { N, N, -1, -1 }; Boxes[tag] = b; }
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                if (!m[y * N + x]) continue;
                if (x < b[0]) b[0] = x;
                if (y < b[1]) b[1] = y;
                if (x > b[2]) b[2] = x;
                if (y > b[3]) b[3] = y;
            }
        }
    }

    public int[] Box(string tag) { int[] b; return Boxes.TryGetValue(tag, out b) ? b : null; }

    public void Paint(bool[] m, int color) { for (int i = 0; i < m.Length; i++) if (m[i]) Px[i] = color; }

    // Recolour only where the pixel currently has colour 'from' (fur patches).
    public void Patch(bool[] m, int from, int to) { for (int i = 0; i < m.Length; i++) if (m[i] && Px[i] == from) Px[i] = to; }

    // Recolour any fur pixel (not empty, not outline).
    public void Wash(bool[] m, int color) { for (int i = 0; i < m.Length; i++) if (m[i] && Px[i] != 0 && Px[i] != Outline) Px[i] = color; }

    public void Pixel(int x, int y, int c) { x += OX; y += OY; if (In(x, y)) Px[y * N + x] = c; }

    public void Rect(int x, int y, int w, int h, int c)
    {
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Pixel(x + i, y + j, c);
    }

    public void Rim(int color)
    {
        int[] src = (int[])Px.Clone();
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                if (src[y * N + x] != 0) continue;
                if ((x > 0 && src[y * N + x - 1] == Outline) || (x < N - 1 && src[y * N + x + 1] == Outline) ||
                    (y > 0 && src[(y - 1) * N + x] == Outline) || (y < N - 1 && src[(y + 1) * N + x] == Outline))
                    Px[y * N + x] = color;
            }
        }
    }

    public int[] Bounds()
    {
        int[] b = new int[] { N, N, -1, -1 };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                if (Px[y * N + x] != 0)
                {
                    if (x < b[0]) b[0] = x;
                    if (y < b[1]) b[1] = y;
                    if (x > b[2]) b[2] = x;
                    if (y > b[3]) b[3] = y;
                }
        return b;
    }

    public void Blit(Bitmap bmp, int frame)
    {
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                if (Px[y * N + x] != 0) bmp.SetPixel(frame * N + x, y, Color.FromArgb(Px[y * N + x]));
    }

    public string Ascii()
    {
        StringBuilder sb = new StringBuilder();
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                int v = Px[y * N + x];
                char ch;
                if (v == 0) ch = '.';
                else if (v == Outline) ch = '#';
                else if (!Legend.TryGetValue(v, out ch)) ch = '?';
                sb.Append(ch);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
'@

$K = @{}
@{ ol = '#3a2420'; cr = '#fff3da'; sh = '#f1d8ae'; or = '#f0a456'; dor = '#d57f35'; pk = '#ffa9b8'; no = '#f0708a'
   ey = '#241519'; wh = '#ffffff'; bl = '#ffb3b3'; mo = '#8a2f45'; tg = '#ff8aa0'; zz = '#4fa3ff'; hr = '#ff5b7a' }.GetEnumerator() |
    ForEach-Object { $K[$_.Key] = [Canvas]::Hex($_.Value) }
$RimColor = [int]0x70FFFFFF

$cv = New-Object Canvas
$cv.Outline = $K.ol
@{ cr = 'c'; sh = 's'; or = 'o'; dor = 'd'; pk = 'p'; no = 'n'; ey = 'e'; wh = 'w'; bl = 'b'; mo = 'm'; tg = 't'; zz = 'z'; hr = 'h' }.GetEnumerator() |
    ForEach-Object { $cv.Legend[$K[$_.Key]] = [char]$_.Value }
$cv.Legend[$RimColor] = [char]':'

# ---------------------------------------------------------------- parts

function Draw-Eye([int]$x, [int]$y, [string]$state) {
    switch ($state) {
        'open' {
            $cv.Paint($cv.Ellipse($x, $y, 2.7, 3.6, 0), $K.ey)
            $cv.Rect($x - 2, $y - 3, 2, 2, $K.wh); $cv.Pixel($x + 1, $y + 1, $K.wh)
        }
        'half' {
            $cv.Paint($cv.Ellipse($x, $y + 1, 2.7, 2.2, 0), $K.ey)
            $cv.Rect($x - 3, $y - 2, 6, 1, $K.ol); $cv.Pixel($x - 1, $y, $K.wh)
        }
        'closed' {
            $off = @(0, 1, 2, 2, 2, 1, 0)
            for ($i = 0; $i -lt 7; $i++) { $cv.Rect($x - 3 + $i, $y + $off[$i] - 1, 1, 2, $K.ol) }
        }
        'happy' {
            $off = @(2, 1, 0, 0, 0, 1, 2)
            for ($i = 0; $i -lt 7; $i++) { $cv.Rect($x - 3 + $i, $y + $off[$i] - 1, 1, 2, $K.ol) }
        }
    }
}

# Head centred on (cx,cy). Options: sc scale, s face shift (3 = slightly right), sp half eye spacing, ear (right ear droop),
# eyes, mouth ('', 'open', 'lick').
function Draw-Head([double]$cx, [double]$cy, [hashtable]$o) {
    $sc = 1.0; if ($o.sc) { $sc = [double]$o.sc }
    $s = 3.0; if ($null -ne $o.s) { $s = [double]$o.s }
    $sp = 7.0; if ($null -ne $o.sp) { $sp = [double]$o.sp }
    $sp = $sp * $sc
    $ex = ($s - 3) * 0.35
    $er = [double]$o.ear
    $eyes = 'open'; if ($o.eyes) { $eyes = [string]$o.eyes }

    $earPolyL = [double[]]@(($cx - 17 * $sc + $ex), ($cy - 3 * $sc), ($cx - 14 * $sc + $ex), ($cy - 18 * $sc), ($cx - 2 * $sc + $ex), ($cy - 11 * $sc))
    $earPolyR = [double[]]@(($cx + 17 * $sc + $ex), ($cy - 3 * $sc), ($cx + 14 * $sc + $ex), ($cy - 18 * $sc + $er), ($cx + 2 * $sc + $ex), ($cy - 11 * $sc))
    $cv.Commit($cv.Poly($earPolyL), $K.or, 'ears', 2.3)
    $cv.Commit($cv.Poly($earPolyR), $K.or, 'ears', 2.3)
    $cv.Paint($cv.Poly([double[]]@(($cx - 15 * $sc + $ex), ($cy - 5 * $sc), ($cx - 13.5 * $sc + $ex), ($cy - 14 * $sc), ($cx - 6 * $sc + $ex), ($cy - 9.5 * $sc))), $K.pk)
    $cv.Paint($cv.Poly([double[]]@(($cx + 15 * $sc + $ex), ($cy - 5 * $sc), ($cx + 13.5 * $sc + $ex), ($cy - 14 * $sc + $er), ($cx + 6 * $sc + $ex), ($cy - 9.5 * $sc))), $K.pk)

    $hm = $cv.Or($cv.Ellipse($cx, $cy, 16.5 * $sc, 12.8 * $sc, 0),
        $cv.Or($cv.Ellipse($cx - 10 * $sc, $cy + 4.5 * $sc, 7.5 * $sc, 6.5 * $sc, 0), $cv.Ellipse($cx + 10 * $sc, $cy + 4.5 * $sc, 7.5 * $sc, 6.5 * $sc, 0)))
    $cv.Commit($hm, $K.cr, 'head', 2.3)

    # orange patches: left cap + right ear side + small cheek spot, cream muzzle, blush
    $cv.Patch($cv.Ellipse($cx - 9 * $sc, $cy - 9 * $sc, 10 * $sc, 8 * $sc, 0), $K.cr, $K.or)
    $cv.Patch($cv.Ellipse($cx + 12 * $sc, $cy - 6 * $sc, 6 * $sc, 5 * $sc, 0), $K.cr, $K.or)
    $cv.Patch($cv.Ellipse($cx - 14 * $sc, $cy + 6 * $sc, 3.5 * $sc, 3 * $sc, 0), $K.cr, $K.or)

    $fx = $cx + $s
    $cv.Wash($cv.Ellipse($fx, $cy + 8 * $sc, 6.5 * $sc, 4.5 * $sc, 0), $K.cr)
    $cv.Patch($cv.Ellipse($fx - $sp - 4 * $sc, $cy + 6.5 * $sc, 3, 2, 0), $K.cr, $K.bl)
    $cv.Patch($cv.Ellipse($fx + $sp + 4 * $sc, $cy + 6.5 * $sc, 3, 2, 0), $K.cr, $K.bl)

    $ey = [int][math]::Round($cy + 2 * $sc)
    Draw-Eye ([int][math]::Round($fx - $sp)) $ey $eyes
    Draw-Eye ([int][math]::Round($fx + $sp)) $ey $eyes

    $nx = [int][math]::Round($fx); $ny = [int][math]::Round($cy + 6 * $sc)
    $cv.Rect($nx - 1, $ny, 3, 2, $K.no)
    switch ([string]$o.mouth) {
        'open' {
            $cv.Paint($cv.Ellipse($nx, $ny + 4.5, 2.4, 2.6, 0), $K.mo)
            $cv.Paint($cv.Ellipse($nx, $ny + 5.4, 1.5, 1.1, 0), $K.tg)
        }
        default {
            $cv.Pixel($nx, $ny + 2, $K.ol)
            $cv.Pixel($nx - 2, $ny + 3, $K.ol); $cv.Pixel($nx - 1, $ny + 4, $K.ol); $cv.Pixel($nx, $ny + 3, $K.ol)
            $cv.Pixel($nx + 1, $ny + 4, $K.ol); $cv.Pixel($nx + 2, $ny + 3, $K.ol)
            if ($o.mouth -eq 'lick') { $cv.Rect($nx, $ny + 5, 2, 2, $K.tg) }
        }
    }
}

function Draw-Tail([double[]]$pts, [double]$r0, [double]$r1, [double]$px, [double]$py, [double]$pr) {
    $cv.Commit($cv.Chain($pts, $r0, $r1), $K.cr, 'tail', 2.3)
    $cv.Patch($cv.Ellipse($px, $py, $pr, $pr, 0), $K.cr, $K.or)
}

function Draw-Sit([hashtable]$o) {
    $cv.OX += 2
    $a = [double]$o.tail; $b = [double]$o.b
    $hx = 36 + [int]$o.hx; $hy = 26 + [int]$o.hy
    Draw-Tail ([double[]]@(14, 55, 8, 53, 5, 47, 5, 41, (6 + 0.4 * $a), 36, (8 + 0.8 * $a), 31)) 3.4 2.4 8 33 14

    $ry = 12 + $b; $cy = 61 - $ry
    $cv.Commit($cv.Ellipse(27, $cy, 16, $ry, 0), $K.cr, 'body', 2.3)
    $cv.Patch($cv.Ellipse(18, 42, 8, 6, 0), $K.cr, $K.or)
    $cv.Patch($cv.Ellipse(27, 62, 15, 5, 0), $K.cr, $K.sh)
    $cv.Commit($cv.Ellipse(20, 51, 9.5, 8.5, 0), $K.or, '', 2.3)
    $cv.Commit($cv.Ellipse(18.5, 58.7, 7, 2.9, 0), $K.cr, '', 2.3)
    $cv.Commit($cv.Chain([double[]]@(38, 46, 38, 57), 3.6, 3.6), $K.cr, '', 2.3)
    $cv.Commit($cv.Chain([double[]]@(47, 47, 47, 57), 3.6, 3.6), $K.cr, '', 2.3)
    $cv.Commit($cv.Ellipse(38.5, 58.8, 4.8, 2.8, 0), $K.cr, '', 2.3)
    $cv.Commit($cv.Ellipse(47.5, 58.8, 4.8, 2.8, 0), $K.cr, '', 2.3)

    Draw-Head $hx $hy $o

    if ($null -ne $o.pawx) {
        $cv.Commit($cv.Chain([double[]]@(44, 52, [double]$o.pawx, [double]$o.pawy), 3.5, 3.5), $K.cr, '', 2.3)
        $cv.Pixel([int]$o.pawx, [int]$o.pawy, $K.pk); $cv.Pixel([int]$o.pawx + 1, [int]$o.pawy, $K.pk)
    }
    for ($k = 0; $k -lt [int]$o.dots; $k++) {
        Ellipse(50 + 2.5 * $k, 14 - 4 * $k, 1.4 + 0.5 * $k, 1.4 + 0.5 * $k, 0)
    }
}

function Draw-Stand([hashtable]$o) {
    $cv.OX += 2
    $bob = [int]$o.bob; $a = [double]$o.tail
    $hasPhase = $null -ne $o.phase
    $hx = 41 + [int]$o.hx; $hy = 25 + $bob + [int]$o.hy
    $tipX = 12 + $a
    Draw-Tail ([double[]]@(16, (41 + $bob), 9, (38 + $bob), 5, (31 + $bob), 6, (23 + $bob), (8 + 0.6 * $a), (17 + $bob), $tipX, (12 + $bob))) 3.3 2.2 8 18 14

    $legs = @(
        @{ x = 23; p = 0; far = $true }, @{ x = 38; p = [math]::PI; far = $true },
        @{ x = 17; p = [math]::PI; far = $false }, @{ x = 33; p = 0; far = $false })
    $drawLeg = {
        param($l)
        $sw = 0.0; $lift = 0.0
        if ($hasPhase) {
            $sw = 5 * [math]::Sin($o.phase + $l.p)
            $lift = [math]::Max(0, [math]::Cos($o.phase + $l.p)) * 3.5
        }
        $fx = $l.x + $sw; $fy = 58.7 - $lift
        $col = $K.cr; if ($l.far) { $col = $K.sh }
        $cv.Commit($cv.Chain([double[]]@($l.x, (47 + $bob), (($l.x + $fx) / 2), (53 - $lift / 2), $fx, ($fy - 1)), 3.3, 3.1), $col, '', 2.3)
        $cv.Commit($cv.Ellipse($fx + 1.2, $fy, 4.4, 2.8, 0), $col, '', 2.3)
    }
    foreach ($l in $legs) { if ($l.far) { & $drawLeg $l } }

    $cv.Commit($cv.Ellipse(27, 43 + $bob, 17, 10.5, 0), $K.cr, 'body', 2.3)
    $cv.Patch($cv.Ellipse(21, 36 + $bob, 12, 6, 0), $K.cr, $K.or)
    $cv.Patch($cv.Ellipse(12, 46 + $bob, 5, 5, 0), $K.cr, $K.or)
    $cv.Patch($cv.Ellipse(27, 53 + $bob, 16, 4, 0), $K.cr, $K.sh)
    foreach ($l in $legs) { if (-not $l.far) { & $drawLeg $l } }

    Draw-Head $hx $hy @{ sc = 0.92; s = 5; eyes = $o.eyes; mouth = $o.mouth; ear = $o.ear }
}

function Draw-Sleep([hashtable]$o) {
    $b = [double]$o.b; $tw = [double]$o.tw
    $ry = 11 + 1.4 * $b; $cy = 61 - $ry
    $cv.Commit($cv.Ellipse(30, $cy, 25, $ry, 0), $K.cr, 'body', 2.3)
    $cv.Patch($cv.Ellipse(20, $cy - 3, 13, 7, 0), $K.cr, $K.or)
    $cv.Patch($cv.Ellipse(30, 63, 24, 4, 0), $K.cr, $K.sh)
    $cv.Commit($cv.Chain([double[]]@(10, 48, 6.5, 53, 9, 57.5, 19, 58.3, (29 + $tw * 2), (57.8 - $tw * 4)), 2.6, 2.2), $K.cr, 'tail', 2.3)
    $cv.Patch($cv.Ellipse(8, 54, 5, 6, 0), $K.cr, $K.or)
    Draw-Head 44 (47 + [double]$o.hy) @{ sc = 0.84; s = 2; eyes = 'closed'; mouth = '' }
    $cv.Commit($cv.Ellipse(36, 59.4, 4.3, 2.5, 0), $K.cr, '', 2.3)
    $cv.Commit($cv.Ellipse(52, 59.4, 4.3, 2.5, 0), $K.cr, '', 2.3)
    foreach ($ph in @(0, 3)) {
        $p = (([int]$o.f + $ph) % 6) / 6.0
        Draw-Z ([int](50 + 9 * $p)) ([int](28 - 22 * $p))
    }
}

function Draw-Z([int]$x, [int]$y) {
    $cv.Rect($x, $y, 5, 1, $K.zz); $cv.Rect($x, $y + 4, 5, 1, $K.zz)
    $cv.Pixel($x + 3, $y + 1, $K.zz); $cv.Pixel($x + 2, $y + 2, $K.zz); $cv.Pixel($x + 1, $y + 3, $K.zz)
}

function Draw-Stretch([double]$t, [hashtable]$o) {
    $cv.OX += 2
    $u = 2 * $t
    Draw-Tail ([double[]]@(15, (48 - $u), 9, (44 - $u), 5, (36 - $u), 5, (28 - $u), 7, (21 - $u), (9 + 2 * $o.flick), (15 - $u))) 3.2 2.2 8 18 14
    $cv.Commit($cv.Chain([double[]]@(21, 48, 21, 58.5), 3.2, 3.2), $K.sh, '', 2.3)
    $cv.Commit($cv.Ellipse(22.3, 58.7, 4.4, 2.8, 0), $K.sh, '', 2.3)
    $cv.Commit($cv.Ellipse(27, 44 - 1.5 * $t, 17, 10, 16 * $t), $K.cr, 'body', 2.3)
    $cv.Patch($cv.Ellipse(20, 38 - 2 * $t, 12, 6, 16 * $t), $K.cr, $K.or)
    $cv.Commit($cv.Chain([double[]]@(15, (47 - $u), 15, 58.5), 3.3, 3.3), $K.cr, '', 2.3)
    $cv.Commit($cv.Ellipse(16.3, 58.7, 4.4, 2.8, 0), $K.cr, '', 2.3)
    $cv.Commit($cv.Chain([double[]]@(37, (49 + 3 * $t), (44 + 3 * $t), 58.6), 3.2, 3.0), $K.sh, '', 2.3)
    $cv.Commit($cv.Ellipse(45.2 + 3 * $t, 58.7, 4.4, 2.8, 0), $K.sh, '', 2.3)
    $cv.Commit($cv.Chain([double[]]@(41, (50 + 3 * $t), (49 + 3 * $t), 58.6), 3.3, 3.1), $K.cr, '', 2.3)
    $cv.Commit($cv.Ellipse(50.2 + 3 * $t, 58.7, 4.4, 2.8, 0), $K.cr, '', 2.3)
    Draw-Head (39 + 2 * $t) (27 + 13 * $t) @{ sc = 0.92; s = 4; eyes = $o.eyes; mouth = $o.mouth }
}

function Draw-Heart([int]$x, [int]$y) {
    $cv.Pixel($x + 1, $y, $K.hr); $cv.Pixel($x + 3, $y, $K.hr); $cv.Rect($x, $y + 1, 5, 1, $K.hr)
    $cv.Rect($x + 1, $y + 2, 3, 1, $K.hr); $cv.Pixel($x + 2, $y + 3, $K.hr)
}

# ---------------------------------------------------------------- sheets

function Build-Sheet([string]$name, [int]$frames, [scriptblock]$draw) {
    $bmp = New-Object System.Drawing.Bitmap ($frames * 64), 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($f = 0; $f -lt $frames; $f++) {
        $cv.Clear()
        & $draw $f
        if ($Metrics -and $name -eq 'sit' -and $f -eq 0) {
            $bd = $cv.Bounds(); $h = $bd[3] - $bd[1] + 1
            $hb = $cv.Box('head'); $bb = $cv.Box('body'); $eb = $cv.Box('ears'); $tb = $cv.Box('tail')
            $hh = $hb[3] - $hb[1] + 1; $hbh = $bb[3] - $bb[1] + 1; $th = $tb[3] - $tb[1] + 1
            Write-Host ("SIT frame0: sprite height {0}px  head {1}px ({2:P0})  head+ears {3}px ({4:P0})  body {5}px ({6:P0})  tail {7}px ({8:P0})" -f `
                $h, $hh, ($hh / $h), ($hb[3] - $eb[1] + 1), (($hb[3] - $eb[1] + 1) / $h), $hbh, ($hbh / $h), $th, ($th / $h))
            Write-Host ("        sprite bounds x {0}-{1}, y {2}-{3}" -f $bd[0], $bd[2], $bd[1], $bd[3])
        }
        $cv.Rim($RimColor)
        if ($Preview -eq $name -and $PreviewFrame -eq $f) { Write-Host "--- $name frame $f"; Write-Host $cv.Ascii() }
        $cv.Blit($bmp, $f)
    }
    $bmp.Save((Join-Path $OutDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

Build-Sheet 'idle' 4 { param($f)
    Draw-Stand @{ bob = @(0, 0, -1, -1)[$f]; tail = @(0, 2, 4, 2)[$f]; hy = @(0, 0, -1, 0)[$f]; eyes = @('open', 'open', 'open', 'half')[$f] } }

Build-Sheet 'walk' 8 { param($f)
    $a = 2 * [math]::PI * $f / 8
    Draw-Stand @{ phase = $a; bob = -[int][math]::Round(1 + [math]::Cos(2 * $a)); tail = 4 * [math]::Sin($a + 1); hy = @(0, -1, 0, 1, 0, -1, 0, 1)[$f]; eyes = 'open' } }

Build-Sheet 'sit' 4 { param($f)
    Draw-Sit @{ b = @(0, 0, 0.5, 0.5)[$f]; tail = @(0, 2, 4, 2)[$f]; eyes = 'open' } }

Build-Sheet 'blink' 4 { param($f)
    Draw-Sit @{ eyes = @('open', 'half', 'closed', 'open')[$f]; hy = @(0, 0, 1, 0)[$f]; b = @(0, 0, 0.5, 0)[$f] } }

Build-Sheet 'tailflick' 6 { param($f)
    Draw-Sit @{ tail = @(0, 3, 6, 3, 0, -3)[$f]; ear = @(0, 0, 3, 0, 0, 0)[$f]; eyes = 'open' } }

Build-Sheet 'lookleft' 6 { param($f)
    Draw-Sit @{ s = @(3, 0, -4, -6, -2, 3)[$f]; hx = @(0, -1, -2, -2, -1, 0)[$f]; sp = @(7, 6.4, 5.4, 5.2, 6.4, 7)[$f] } }

Build-Sheet 'lookright' 6 { param($f)
    Draw-Sit @{ s = @(3, 6, 9, 9, 6, 3)[$f]; hx = @(0, 1, 2, 2, 1, 0)[$f]; sp = @(7, 6.2, 5.4, 5.2, 6.2, 7)[$f] } }

Build-Sheet 'groom' 8 { param($f)
    $px = @(46, 45, 44, 45, 42, 45, 42, 45)[$f]; $py = @(50, 44, 38, 35, 35, 35, 35, 42)[$f]
    $m = ''; if ($f -ge 3 -and $f -le 6) { $m = 'lick' }
    Draw-Sit @{ pawx = $px; pawy = $py; hx = 1; hy = 1; s = 4; eyes = 'closed'; mouth = $m; b = ($f % 2) * 0.5; tail = @(0, 1, 2, 1, 0, 1, 2, 1)[$f] } }

Build-Sheet 'stretch' 8 { param($f)
    $t = @(0, 0.35, 0.7, 1, 1, 0.7, 0.35, 0)[$f]
    $eyes = 'open'; if ($t -ge 0.7) { $eyes = 'happy' }
    $m = ''; if ($f -eq 3 -or $f -eq 4) { $m = 'open' }
    Draw-Stretch $t @{ eyes = $eyes; mouth = $m; flick = @(0, 0, 0, 1, 2, 1, 0, 0)[$f] } }

Build-Sheet 'sleep' 6 { param($f)
    Draw-Sleep @{ f = $f; b = @(0, 0.5, 1, 1, 0.5, 0)[$f]; tw = @(0, 0, 0, 1, 0, 0)[$f]; hy = @(0, 0, 0.6, 0.6, 0, 0)[$f] } }

Build-Sheet 'thinking' 4 { param($f)
    Draw-Sit @{ b = @(0, 0, 0.5, 0.5)[$f]; dots = @(1, 2, 3, 2)[$f]; hx = 1; hy = -1; s = 6; tail = @(0, 1, 2, 1)[$f]; ear = @(0, 0, 2, 0)[$f]; eyes = 'open' } }

Build-Sheet 'speaking' 4 { param($f)
    Draw-Sit @{ b = @(0, 0.5, 0, 0.5)[$f]; mouth = @('open', '', 'open', '')[$f]; tail = @(0, 1, 2, 1)[$f] } }

Build-Sheet 'happy' 4 { param($f)
    $cv.OY = -@(0, 2, 4, 2)[$f]
    Draw-Sit @{ b = @(-1, 0.5, 1, 0.5)[$f]; tail = @(-3, 6, -3, 6)[$f]; eyes = 'happy'; mouth = 'open' }
    $cv.OY = 0
    if ($f -ge 1) { Draw-Heart 54 (@(0, 14, 10, 6)[$f]) } }

Write-Host "Pet Art V2 sprites written to $OutDir"


