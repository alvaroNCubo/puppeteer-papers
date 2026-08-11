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

# -Example may point at either layout, and both are legitimate: the copy vendored beside
# this lab (labs\paper09-example) holds ai\ and domain\ at its root, while the examples
# repository this script was first written against keeps them under Tetris\. Find the one
# that is there, then derive everything else from where it was found — so the session
# directory is right even before the frame exists.
function Resolve-Under {
  param([string]$Root, [string]$Relative)
  foreach ($mid in @('', 'Tetris')) {
    $p = if ($mid) { Join-Path $Root (Join-Path $mid $Relative) } else { Join-Path $Root $Relative }
    if (Test-Path $p) { return (Resolve-Path $p).Path }
  }
  return $null
}

$ai = Resolve-Under $Example 'ai\TetrisAi.csproj'
if (-not $ai) { throw "no ai\TetrisAi.csproj under '$Example' (tried it and Tetris\ beneath it) — is -Example the example's root?" }

$exampleRoot = Split-Path (Split-Path $ai -Parent) -Parent
$tools = Join-Path $exampleRoot 'tools\pile-scan.ps1'
$frame = Join-Path $exampleRoot ".sessions\$Session.frame"
if (-not (Test-Path $tools)) { throw "no tools\pile-scan.ps1 under '$exampleRoot'" }

# A fixed sequence placing three pieces. Deterministic: the same acts in the same order
# produce the same frame, because piece selection is the domain's own and replays
# identically.
#
# `new` appears ONCE and must. It refuses to touch a session that already has a journal --
# never overwrite a game -- so a repeated `new` fails, and it is not needed anyway: `drop`
# lands the piece and spawns the next one itself (ai\Program.cs, SpawnIfAwaiting).
$acts = @('new','left','left','rotate','drop',
          'right','rotate','drop',
          'left','drop')

# Fail here rather than four acts in. The engine will not overwrite a recorded game, which
# is the right rule, so a second run of this script needs a session name of its own.
$sessionDir = Join-Path $exampleRoot ".sessions\$Session"
if ((Test-Path $sessionDir) -and (Get-ChildItem $sessionDir -Force | Select-Object -First 1)) {
  throw "session '$Session' already has a journal at $sessionDir. Pass a fresh one, e.g. -Session $Session-2, or delete that directory if it was this script's own throwaway."
}

Write-Host "== playing $($acts.Count) acts into session '$Session'" -ForegroundColor Cyan
foreach ($a in $acts) {
  dotnet run --project $ai -- $Session $a | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "act '$a' failed" }
  Write-Host "   $a" -NoNewline; Write-Host ' ok' -ForegroundColor DarkGray
}

Write-Host "`n== projection 1 of 3: the emitted fact itself" -ForegroundColor Cyan
$framePath = (Resolve-Path $frame -ErrorAction SilentlyContinue)
if (-not $framePath) { throw "no frame at $frame — the push channel wrote nothing, which is the one failure this lab cannot paper over" }
Get-Content -Raw $framePath

Write-Host "`n== projection 2 of 3: the column-height vector (client-authored)" -ForegroundColor Cyan
& $tools -FramePath $framePath

Write-Host "`n== projection 3 of 3: the character grid" -ForegroundColor Cyan
dotnet run --project $ai -- $Session view

Write-Host "`n== the claim, verified by reading rather than by running" -ForegroundColor Cyan
$well = Join-Path $exampleRoot 'domain\Well.cs'

# CODE only. An earlier version grepped raw lines and failed on a doc comment that says the
# emitted set is "handy for rendering" — prose about what a CLIENT may do with a fact, which
# is the opposite of the domain owning a view. The claim is about members, so comment lines
# are excluded and the line numbers reported are the code's.
$viewish = @(Get-Content $well | ForEach-Object -Begin { $n = 0 } -Process {
  $n++
  $t = $_.TrimStart()
  if ($t -match '^(///|//|\*|/\*)') { return }
  if ($_ -match 'Render|ToGrid|Skyline|Json|Html|Draw|Print') {
    [pscustomobject]@{ LineNumber = $n; Line = $_ }
  }
})
if ($viewish.Count -gt 0) {
  Write-Host "   FAIL — the domain names a view in code:" -ForegroundColor Red
  $viewish | ForEach-Object { Write-Host "     $($_.LineNumber): $($_.Line.Trim())" }
} else {
  Write-Host "   0 domain methods for any of the three views." -ForegroundColor Green
  Write-Host "   The domain emits OccupiedInterior() — the union of the pile and the active"
  Write-Host "   piece, clipped to the interior — and nothing shaped like a rendering."
}
