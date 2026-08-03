# pile-scan.ps1 - Claude's "mirilla": an instrument that augments my sense of the pile.
#
# It reads a pushed frame (the JSON projection the game already writes) and lifts
# the 200-cell bitmap into the SYMBOLIC view I actually reason in: the skyline
# vector, its difference profile, the zeros (fill priority), wells, simple
# metrics, and the active piece. A telescope for the pile.
#
# Key trick: a column's pile-height is the run of filled cells contiguous FROM
# THE FLOOR up. That (a) equals the true surface in no-hole play and (b) excludes
# a falling piece, which floats above a gap. Anything filled ABOVE the first gap
# is reported as "floating" (the active piece, or an overhang).
#
# NB: variable names are deliberately distinct in more than case - PowerShell
# variables are case-INSENSITIVE, so a counter "$h" would alias the height "$H".
#
# Usage:  pile-scan.ps1 -Session live5
#         pile-scan.ps1 -FramePath C:\...\some.frame

param(
  [string]$Session,
  [string]$FramePath
)

if (-not $FramePath) {
  if (-not $Session) { Write-Error "give -Session <name> or -FramePath <path>"; exit 2 }
  $FramePath = "C:\Users\alvar\source\repos\puppeteer-examples\Tetris\.sessions\$Session.frame"
}
if (-not (Test-Path $FramePath)) { Write-Error "no frame file at $FramePath"; exit 1 }

$frame = (Get-Content -Raw -Path $FramePath) | ConvertFrom-Json
$width = [int]$frame.width
$boardH = [int]$frame.height

# Per-column set of filled rows (integer keys; no string/2D-index quirks).
$colRows = @{}
for ($col = 0; $col -lt $width; $col++) { $colRows[$col] = @{} }
foreach ($cell in @($frame.cell)) { $colRows[[int]$cell.c][[int]$cell.r] = $true }

# Skyline + floating cells.
$sky = @()
$floating = @()
for ($col = 0; $col -lt $width; $col++) {
  $rows = $colRows[$col]
  $row = $boardH - 1
  $stack = 0
  while ($row -ge 0 -and $rows.ContainsKey($row)) { $stack++; $row-- }
  $sky += $stack
  while ($row -ge 0) {
    if ($rows.ContainsKey($row)) { $floating += [pscustomobject]@{ r = $row; c = $col } }
    $row--
  }
}

# Difference profile, zeros (fill priority), wells (local minima), metrics.
$diffs = @(); for ($col = 1; $col -lt $width; $col++) { $diffs += ($sky[$col] - $sky[$col - 1]) }
$zeros = @(); for ($col = 0; $col -lt $width; $col++) { if ($sky[$col] -eq 0) { $zeros += $col } }
$wells = @()
for ($col = 0; $col -lt $width; $col++) {
  $left = if ($col -eq 0) { 99 } else { $sky[$col - 1] }
  $right = if ($col -eq $width - 1) { 99 } else { $sky[$col + 1] }
  if ($sky[$col] -lt $left -and $sky[$col] -lt $right) {
    $depth = [math]::Min($left, $right) - $sky[$col]
    $wells += "col$col(d$depth)"
  }
}
$maxStack = ($sky | Measure-Object -Maximum).Maximum
$aggregate = ($sky | Measure-Object -Sum).Sum
$bumpiness = 0; for ($col = 1; $col -lt $width; $col++) { $bumpiness += [math]::Abs($sky[$col] - $sky[$col - 1]) }

# Render the mirilla.
$signed = ($diffs | ForEach-Object { if ($_ -ge 0) { "+$_" } else { "$_" } }) -join ' '
"== MIRILLA  frame=$([System.IO.Path]::GetFileName($FramePath)) =="
"state     : piece={0}  cleared={1}  over={2}  awaiting={3}" -f $frame.type, $frame.cleared, $frame.over, $frame.awaiting
"cols      : " + ((0..($width - 1)) -join ' ')
"skyline   : " + ($sky -join ' ')
"diffs     :  $signed"
"zeros     : " + ($(if ($zeros.Count) { $zeros -join ',' } else { 'none' })) + "   (lowest = fill priority)"
"wells     : " + ($(if ($wells.Count) { $wells -join ' ' } else { 'none' }))
"metrics   : maxH=$maxStack  agg=$aggregate  bumpiness=$bumpiness  floating=$($floating.Count)"
if ($floating.Count) {
  "active    : type=$($frame.type)  " + (($floating | Sort-Object r, c | ForEach-Object { "($($_.r),$($_.c))" }) -join ' ')
}
else {
  "active    : (none floating - between pieces / resting on pile)"
}
