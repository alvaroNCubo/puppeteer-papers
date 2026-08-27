# Fetches and builds the two corpus domain assemblies this lab consumes as prebuilt
# binaries, at the exact commits every count in the paper was taken at, and copies the
# three DLLs into corpus-bin/ where the .csproj expects them.
#
#   .\fetch-corpus.ps1                        -> clones under .\corpus-src, fills .\corpus-bin
#   .\fetch-corpus.ps1 -Work C:\tmp\corpus    -> clones there instead
#
# Nothing of either repository is redistributed in this archive (both are MIT; a build
# from source at a pinned commit is cleaner than shipped bytes). Requires git and one
# .NET SDK able to target net9.0 - a 9.0.x SDK is enough, and a newer one also works.
# No 8.0.x SDK is needed: the modular monolith's net8.0 projects build fine on a later
# SDK, and its own src/global.json - which pins 8.0.0 and would stop such a build - is
# resolved from the working directory rather than from the project, so this script sets
# the working directory to its own folder and keeps it there. Run it from anywhere.
#
# One deviation from the corpus's own build, disclosed because the paper's provenance
# claim depends on it: the commerce domain at 9b4f943 declares net10.0, and this suite
# is net9.0, so a net10.0 build of it cannot be referenced at all (CS1705). That project
# is therefore built with TargetFramework overridden to net9.0 - a build-configuration
# change, applied from outside, with no edit to any file of either repository. Both trees
# are left byte-identical to their pinned commits, and this script verifies that.
#
# ASCII only, deliberately: PowerShell 5.1 reads a .ps1 without a BOM as ANSI, so a
# stray em-dash in a string becomes a parse error rather than a typo.

param(
  [string]$Work = (Join-Path $PSScriptRoot "corpus-src")
)

$ErrorActionPreference = "Stop"

# git and dotnet both write ordinary progress to stderr - "Cloning into ...", restore
# notices - and Windows PowerShell 5.1 turns any stderr line from a native command into a
# terminating error while $ErrorActionPreference is Stop. That would abort this script on a
# perfectly healthy clone, which is how it did abort until it was run for the first time in
# a directory that had none. So every native call goes through one of these two: stderr is
# printed as the text it is, and success or failure is decided by the exit code, which is
# the only thing either program actually promises. Stop stays in force for the cmdlets,
# where it belongs.
function Native([string]$exe, [string[]]$arguments) {
  $previous = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    & $exe @arguments 2>&1 | ForEach-Object { Write-Host ("    " + $_) }
    return $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previous
  }
}

# The same, for the calls this script reads an answer out of rather than watches: a
# first-run .NET banner or a stray git notice must not become an exception either.
function NativeOut([string]$exe, [string[]]$arguments) {
  $previous = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    $output = & $exe @arguments 2>$null
    return ($output | Out-String).Trim()
  }
  finally {
    $ErrorActionPreference = $previous
  }
}

$eshopCommit   = "9b4f943"
$grzybekCommit = "91c8ef2"
$suiteTfm      = "net9.0"
$binDir        = Join-Path $PSScriptRoot "corpus-bin"

# Windows still refuses paths over 260 characters unless git is told otherwise, and the
# modular monolith carries a 157-character path of its own. That leaves about a hundred
# characters for everything in front of it, which the default location here does not
# always have. Checked before cloning rather than discovered halfway through one, because
# a clone that dies on "Filename too long" leaves a partial tree behind.
function RequireRoomForLongPaths([string]$work) {
  $longestInside = 157   # src/Modules/Meetings/Application/.../DisableMeetingCommentingConfigurationCommandHandler.cs
  $needed = $work.Length + "\grzybek\".Length + $longestInside

  $longPathsEnabled = $false
  foreach ($scope in @("--global", "--system", "--local")) {
    $value = NativeOut "git" @("config", $scope, "--get", "core.longpaths")
    if ($value -eq "true") { $longPathsEnabled = $true }
  }

  if ($longPathsEnabled) {
    Write-Host "  long paths: enabled in git, so the 260-character limit does not apply"
    return
  }

  if ($needed -gt 260) {
    throw ("The clone would need $needed characters and Windows allows 260. Either clone " +
           "somewhere shorter - .\fetch-corpus.ps1 -Work C:\corpus - or lift the limit with " +
           "git config --global core.longpaths true. This is checked because one corpus has " +
           "a 157-character path inside it, and '$work' leaves it too little room.")
  }

  if ($needed -gt 240) {
    Write-Host ("  WARNING: the longest path will be about $needed characters, and Windows allows 260.")
    Write-Host  "           It fits, but -Work C:\corpus would leave more room."
  }
}

function Fetch([string]$name, [string]$url, [string]$commit) {
  $dir = Join-Path $Work $name

  if (-not (Test-Path (Join-Path $dir ".git"))) {
    if (Test-Path $dir) {
      # A previous run died mid-clone and left a tree with no repository in it. Saying so
      # is better than checking out of it and failing three lines later.
      throw ("$dir exists but is not a git clone - a previous attempt probably failed " +
             "partway. Delete it and run this script again.")
    }
    Write-Host ("  cloning " + $name)
    if ((Native "git" @("clone", $url, $dir)) -ne 0) { throw "clone failed: $url" }
  }

  # The commit has to be in this clone before checking it out: a partial clone, or one made
  # shallow by someone else, has the branch and not the pin.
  if ((Native "git" @("-C", $dir, "cat-file", "-e", ($commit + "^{commit}"))) -ne 0) {
    Write-Host ("  fetching " + $commit + " into " + $name)
    if ((Native "git" @("-C", $dir, "fetch", "--tags", "origin")) -ne 0) { throw "fetch failed: $name" }
    if ((Native "git" @("-C", $dir, "cat-file", "-e", ($commit + "^{commit}"))) -ne 0) {
      throw ("$name has no commit $commit, even after fetching. If this clone came from " +
             "somewhere other than $url, that is why: the pins name that repository.")
    }
  }

  if ((Native "git" @("-C", $dir, "checkout", "--detach", $commit, "--quiet")) -ne 0) {
    throw "checkout failed: $name @ $commit"
  }

  $head = NativeOut "git" @("-C", $dir, "rev-parse", "--short=7", "HEAD")
  if ($head -ne $commit) { throw "$name is at $head, not the pinned $commit" }

  $dirty = NativeOut "git" @("-C", $dir, "status", "--porcelain")
  if ($dirty) {
    Write-Host ""
    Write-Host "  WARNING: $name has local modifications on top of $commit."
    Write-Host "  Every count in the paper was taken against the pinned commit as it ships."
    $dirty | ForEach-Object { Write-Host ("    " + $_) }
    Write-Host ""
  }
  Write-Host ("  " + $name.PadRight(10) + " at " + $head)
  return $dir
}

# The framework a built assembly declares, read from the deps.json beside it. Used to
# pick the right DLL when a project leaves more than one output on disk, and to fail
# loudly rather than hand the suite an assembly it cannot reference.
function TfmOf([string]$dll) {
  $deps = [IO.Path]::ChangeExtension($dll, ".deps.json")
  if (-not (Test-Path $deps)) { return "unknown" }
  $m = [regex]::Match((Get-Content -Raw $deps), [regex]::Escape('".NETCoreApp,Version=v') + '([\d\.]+)"')
  if ($m.Success) { return "net" + $m.Groups[1].Value }
  return "unknown"
}

function PickDll([string]$searchRoot, [string]$fileName, [string]$wantTfm) {
  $all = @(Get-ChildItem -Recurse -File $searchRoot -Filter $fileName -ErrorAction SilentlyContinue)
  if ($all.Count -eq 0) { throw "not found after build: $fileName under $searchRoot" }
  $matching = @($all | Where-Object { (TfmOf $_.FullName) -eq $wantTfm })
  if ($matching.Count -eq 0) {
    $found = ($all | ForEach-Object { (TfmOf $_.FullName) + "  " + $_.FullName }) -join [Environment]::NewLine + "    "
    throw "no $wantTfm build of $fileName. Found: $found"
  }
  return ($matching | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

# The monolith's src/global.json pins an 8.0.0 SDK and is resolved from the working
# directory, so every dotnet call below runs from this script's own folder.
Push-Location $PSScriptRoot
try {
  New-Item -ItemType Directory -Force $Work   | Out-Null
  New-Item -ItemType Directory -Force $binDir | Out-Null

  RequireRoomForLongPaths $Work

  Write-Host ""
  Write-Host "corpus, at the commits the paper's counts were taken at:"
  $eshop   = Fetch "eshop"   "https://github.com/dotnet/eShop.git"                        $eshopCommit
  $grzybek = Fetch "grzybek" "https://github.com/kgrzybek/modular-monolith-with-ddd.git"  $grzybekCommit

  # --- the commerce domain: built at the suite's framework, not the commit's ---------
  $orderingProj = Join-Path $eshop "src\Ordering.Domain\Ordering.Domain.csproj"
  Write-Host ""
  Write-Host "building Ordering.Domain at $suiteTfm (the commit declares net10.0; see the header)"
  if ((Native "dotnet" @("restore", $orderingProj, "-p:TargetFramework=$suiteTfm", "--force")) -ne 0) {
    throw "restore failed: Ordering.Domain at $suiteTfm"
  }
  if ((Native "dotnet" @("build", $orderingProj, "-c", "Debug", "-p:TargetFramework=$suiteTfm", "--no-restore")) -ne 0) {
    throw "build failed: Ordering.Domain at $suiteTfm"
  }
  $orderingDll = PickDll (Join-Path $eshop "artifacts\bin\Ordering.Domain") "Ordering.Domain.dll" $suiteTfm
  Copy-Item $orderingDll $binDir -Force
  $produced = @{ "Ordering.Domain.dll" = $suiteTfm }

  # --- the modular monolith's payments domain: at its own net8.0 --------------------
  $paymentsProj = Join-Path $grzybek "src\Modules\Payments\Domain\CompanyName.MyMeetings.Modules.Payments.Domain.csproj"
  Write-Host ""
  Write-Host "building the payments domain at net8.0 (the framework its own commit declares)"
  if ((Native "dotnet" @("build", $paymentsProj, "-c", "Debug")) -ne 0) {
    throw "build failed: Payments.Domain"
  }
  $paymentsRoot = Join-Path $grzybek "src\Modules\Payments\Domain\bin"
  $seedworkRoot = Join-Path $grzybek "src\BuildingBlocks\Domain\bin"
  Copy-Item (PickDll $paymentsRoot "CompanyName.MyMeetings.Modules.Payments.Domain.dll" "net8.0") $binDir -Force
  Copy-Item (PickDll $seedworkRoot "CompanyName.MyMeetings.BuildingBlocks.Domain.dll"   "net8.0") $binDir -Force
  $produced["CompanyName.MyMeetings.Modules.Payments.Domain.dll"] = "net8.0"
  $produced["CompanyName.MyMeetings.BuildingBlocks.Domain.dll"]   = "net8.0"

  # --- what was produced, and out of what ------------------------------------------
  $sdk = NativeOut "dotnet" @("--version")
  $lines = @(
    "# Written by fetch-corpus.ps1, not by hand. Records the provenance of the three",
    "# binaries beside it. No content of either corpus repository is redistributed.",
    "",
    ("taken            : " + (Get-Date -Format "yyyy-MM-ddTHH:mm:ssK")),
    ("sdk              : " + $sdk),
    ("suite framework  : " + $suiteTfm),
    "",
    ("eshop            : https://github.com/dotnet/eShop.git @ " + $eshopCommit),
    ("                   Ordering.Domain.dll, Debug, " + $suiteTfm + " (TargetFramework overridden from the commit's net10.0)"),
    ("grzybek          : https://github.com/kgrzybek/modular-monolith-with-ddd.git @ " + $grzybekCommit),
    "                   CompanyName.MyMeetings.Modules.Payments.Domain.dll, Debug, net8.0 (as the commit declares)",
    "                   CompanyName.MyMeetings.BuildingBlocks.Domain.dll, Debug, net8.0 (as the commit declares)",
    "",
    ("clones           : " + $Work)
  )
  Set-Content -Path (Join-Path $binDir "PROVENANCE.txt") -Value $lines -Encoding ASCII

  Write-Host ""
  Write-Host "corpus-bin ready:"
  Get-ChildItem $binDir -Filter *.dll | ForEach-Object {
    Write-Host ("  " + $_.Name.PadRight(52) + ([string]$produced[$_.Name]).PadRight(8) + $_.Length + " bytes")
  }
  Write-Host "  PROVENANCE.txt"
  Write-Host ""
  Write-Host "next:  dotnet test                                          20 labs, all green"
  Write-Host "       .\run-labs.ps1                                       the same 20, with the numbers they print"
  Write-Host "       .\run-labs.ps1 -Lab WhatTheRecordCosts -Release      the price table of section 8"
  Write-Host ""
}
finally {
  Pop-Location
}
