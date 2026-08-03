# Paper 9 — Lab D: deterministic verification of "three projections of one emitted frame".
#
# NOBODY PLAYS BY HAND HERE. A reviewer needs a frame to exist and then three renderings of
# it; who produced the frame and how well they play is irrelevant to the claim. So this
# script plays a FIXED sequence with TetrisAi — one short-lived process per act, one writer
# at a time — and then renders the resulting frame three ways.
#
# The claim itself is verified by READING, not by running: the domain has no method for any
# of the three views. See the check at the end.
#
# Usage:  .\verify.ps1 -Example C:\path\to\the\example [-Session labD-verify]

param(
  [Parameter(Mandatory = $true)][string]$Example,
  [string]$Session = 'labD-verify'
)

$ErrorActionPreference = 'Stop'
$ai    = Join-Path $Example 'Tetris\ai\TetrisAi.csproj'
$tools = Join-Path $Example 'Tetris\tools\pile-scan.ps1'
$frame = Join-Path $Example ".sessions\$Session.frame"
if (-not (Test-Path $ai)) { throw "no TetrisAi at $ai — is -Example the example's root?" }

# A fixed sequence. Deterministic: the same acts in the same order produce the same frame,
# because piece selection is the domain's own and replays identically.
$acts = @('new','left','left','rotate','drop',
          'new','right','rotate','drop',
          'new','left','drop')

Write-Host "== playing $($acts.Count) acts into session '$Session'" -ForegroundColor Cyan
foreach ($a in $acts) {
  dotnet run --project $ai -- $Session $a | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "act '$a' failed" }
  Write-Host "   $a" -NoNewline; Write-Host ' ok' -ForegroundColor DarkGray
}

Write-Host "`n== projection 1 of 3: the emitted fact itself" -ForegroundColor Cyan
$framePath = (Resolve-Path $frame -ErrorAction SilentlyContinue)
if (-not $framePath) { $framePath = Join-Path $Example "Tetris\.sessions\$Session.frame" }
Get-Content -Raw $framePath

Write-Host "`n== projection 2 of 3: the column-height vector (client-authored)" -ForegroundColor Cyan
& $tools -FramePath $framePath

Write-Host "`n== projection 3 of 3: the character grid" -ForegroundColor Cyan
dotnet run --project (Join-Path $Example 'Tetris\ai\TetrisAi.csproj') -- $Session view

Write-Host "`n== the claim, verified by reading rather than by running" -ForegroundColor Cyan
$well = Join-Path $Example 'Tetris\domain\Well.cs'
$viewish = Select-String -Path $well -Pattern 'Render|ToGrid|Skyline|Json|Html|Draw|Print' -AllMatches
if ($viewish) {
  Write-Host "   FAIL — the domain names a view:" -ForegroundColor Red
  $viewish | ForEach-Object { Write-Host "     $($_.LineNumber): $($_.Line.Trim())" }
} else {
  Write-Host "   0 domain methods for any of the three views." -ForegroundColor Green
  Write-Host "   The domain emits OccupiedInterior() — the union of the pile and the active"
  Write-Host "   piece, clipped to the interior — and nothing shaped like a rendering."
}
