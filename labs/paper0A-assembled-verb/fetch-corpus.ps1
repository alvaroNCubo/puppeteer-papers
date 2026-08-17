# Fetches and builds the two corpus domain assemblies this lab consumes as prebuilt
# binaries, at the exact commits every count in the paper was taken at, and copies the
# three DLLs into corpus-bin/ where the .csproj expects them.
#
#   .\fetch-corpus.ps1                        -> clones under .\corpus-src, fills .\corpus-bin
#   .\fetch-corpus.ps1 -Work C:\tmp\corpus    -> clones there instead
#
# Nothing of either repository is redistributed in this archive (both are MIT; a build
# from source at a pinned commit is cleaner than shipped bytes). Requires git and the
# .NET SDKs the corpus targets: net9.0 for the commerce system, net8.0 for the modular
# monolith.
#
# ASCII only, deliberately: PowerShell 5.1 reads a .ps1 without a BOM as ANSI, so a
# stray em-dash in a string becomes a parse error rather than a typo.

param(
  [string]$Work = (Join-Path $PSScriptRoot "corpus-src")
)

$ErrorActionPreference = "Stop"

$eshopCommit   = "9b4f943"
$grzybekCommit = "91c8ef2"
$binDir        = Join-Path $PSScriptRoot "corpus-bin"

function Fetch([string]$name, [string]$url, [string]$commit) {
  $dir = Join-Path $Work $name
  if (-not (Test-Path (Join-Path $dir ".git"))) {
    git clone $url $dir
    if ($LASTEXITCODE -ne 0) { throw "clone failed: $url" }
  }
  git -C $dir checkout $commit
  if ($LASTEXITCODE -ne 0) { throw "checkout failed: $name @ $commit" }
  return $dir
}

New-Item -ItemType Directory -Force $Work   | Out-Null
New-Item -ItemType Directory -Force $binDir | Out-Null

$eshop   = Fetch "eshop"   "https://github.com/dotnet/eShop.git"                        $eshopCommit
$grzybek = Fetch "grzybek" "https://github.com/kgrzybek/modular-monolith-with-ddd.git"  $grzybekCommit

# --- the commerce domain (artifacts output layout) --------------------------------
dotnet build (Join-Path $eshop "src\Ordering.Domain\Ordering.Domain.csproj") -c Debug
if ($LASTEXITCODE -ne 0) { throw "build failed: Ordering.Domain" }
$orderingDll = Get-ChildItem -Recurse (Join-Path $eshop "artifacts\bin\Ordering.Domain") -Filter "Ordering.Domain.dll" | Select-Object -First 1
Copy-Item $orderingDll.FullName $binDir -Force

# --- the modular monolith's payments domain (classic bin layout) ------------------
dotnet build (Join-Path $grzybek "src\Modules\Payments\Domain\CompanyName.MyMeetings.Modules.Payments.Domain.csproj") -c Debug
if ($LASTEXITCODE -ne 0) { throw "build failed: Payments.Domain" }
$paymentsOut = Join-Path $grzybek "src\Modules\Payments\Domain\bin\Debug\net8.0"
Copy-Item (Join-Path $paymentsOut "CompanyName.MyMeetings.Modules.Payments.Domain.dll") $binDir -Force
Copy-Item (Join-Path $paymentsOut "CompanyName.MyMeetings.BuildingBlocks.Domain.dll")   $binDir -Force

Write-Host ""
Write-Host "corpus-bin ready:"
Get-ChildItem $binDir | ForEach-Object { Write-Host ("  " + $_.Name) }
