# Builds paper09-data.zip in the shape Code provenance promises, and the shape the
# earlier papers' archives already have: labs/paper09-* and data/paper09-* side by
# side at the top level, with the suite README among them.
#
# It takes the file list from GIT, not from the filesystem, and that is the whole
# trick: bin/, obj/, .sessions/, out/ and TestResults/ are untracked or ignored, so
# they cannot get in. A zip made by compressing a lab directory carries them - one
# such zip of Lab H alone came to 246 MB, against 0.01 to 0.72 MB for the archives of
# Papers 3, 4, 5 and 8. None of that weight is data; all of it is build output.
#
# It also packages BOTH halves. A per-lab zip cannot: every lab's write-up and
# captured output live under data/, outside the lab directory, so a reviewer handed
# labs/paper09-labH-recognition/ alone gets that lab's claim of three byte-identical
# journals and none of the evidence for it. That is a real report from a real
# reviewer, not a hypothetical.
#
#   .\paper09-package.ps1                 -> paper09-data.zip beside this script
#   .\paper09-package.ps1 -Out C:\tmp\x.zip
#
# ASCII only, deliberately: PowerShell 5.1 reads a .ps1 without a BOM as ANSI, so a
# stray em-dash in a string becomes a parse error rather than a typo.

param(
  [string]$Out = "paper09-data.zip"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
  # Tracked files only, and never a zip inside the tree.
  $files = @(git ls-files "labs/paper09*" "data/paper09*") | Where-Object { $_ -and $_ -notlike '*.zip' }
  if ($files.Count -eq 0) { throw "no tracked paper09 files found. Is this the papers repository?" }

  # The archive should correspond to a commit, so say what is uncommitted rather than
  # silently packaging a working tree nobody can point at.
  $dirty = @(git status --porcelain -- "labs/paper09*" "data/paper09*") | Where-Object { $_ -and $_ -notlike '*.zip' }
  if ($dirty.Count -gt 0) {
    Write-Warning "$($dirty.Count) uncommitted change(s) under paper09. The archive will NOT match any commit:"
    $dirty | ForEach-Object { Write-Warning "  $_" }
  }

  $stage = Join-Path ([IO.Path]::GetTempPath()) ("paper09-pkg-" + [Guid]::NewGuid().ToString('N').Substring(0,8))
  New-Item -ItemType Directory -Force $stage | Out-Null
  try {
    foreach ($f in $files) {
      $dest = Join-Path $stage $f
      New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
      Copy-Item -LiteralPath (Join-Path $root $f) -Destination $dest -Force
    }

    $outFull = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out }
    if (Test-Path $outFull) { Remove-Item $outFull -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $outFull)

    $size = (Get-Item $outFull).Length / 1MB
    $head = (git rev-parse --short HEAD)
    Write-Host ""
    Write-Host ("paper09-data.zip  {0} files  {1:N2} MB  at commit {2}" -f $files.Count, $size, $head)
    Write-Host ("  -> {0}" -f $outFull)
    Write-Host ""
    Write-Host "Record that commit in Code provenance. The archive matches it only if no"
    Write-Host "warning appeared above."
  }
  finally {
    Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
  }
}
finally {
  Pop-Location
}
