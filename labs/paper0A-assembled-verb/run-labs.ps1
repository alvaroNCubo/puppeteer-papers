# Runs the labs and shows what they print.
#
# This exists because `dotnet test` answers a question the paper does not ask. It reports
# pass or fail; the paper reports counts, and the counts are printed to standard output,
# which the default console logger hides. So every command below adds the detailed
# logger, and the numbers appear.
#
#   .\run-labs.ps1                                     all 21, with their output
#   .\run-labs.ps1 -List                               the lab names, read off the sources
#   .\run-labs.ps1 -Lab WhoDecidesWhatCountsAsHistory  one lab (any part of its name)
#   .\run-labs.ps1 -Lab WhatTheRecordCosts -Release    the price table of section 8
#   .\run-labs.ps1 -Log run.txt                        the same, kept in a file as well
#
# -Release matters for exactly one lab and is stated in the paper: the price table of
# section 8 was taken in Release. The other nineteen assert counts that do not depend on
# the configuration, so Debug is enough for them and is the default here.
#
# The command actually invoked is echoed before it runs. Run it by hand if you prefer;
# nothing here does anything a reviewer cannot type.
#
# ASCII only, deliberately: PowerShell 5.1 reads a .ps1 without a BOM as ANSI, so a
# stray em-dash in a string becomes a parse error rather than a typo.

param(
  [string]$Lab,
  [switch]$Release,
  [switch]$List,
  [string]$Log
)

$ErrorActionPreference = "Stop"

if ($List) {
  Write-Host ""
  Write-Host "The labs in this suite, one class each:"
  Get-ChildItem $PSScriptRoot -Filter "*Lab.cs" | Sort-Object Name | ForEach-Object {
    Write-Host ("  " + $_.BaseName)
  }
  Write-Host ""
  Write-Host "Which of them the paper cites, and for which section, is the table in README.md."
  Write-Host ""
  return
}

$configuration = if ($Release) { "Release" } else { "Debug" }

$arguments = @("test", "-c", $configuration, "--logger", "console;verbosity=detailed")
if ($Lab) { $arguments += @("--filter", "FullyQualifiedName~$Lab") }

Write-Host ""
Write-Host ("  dotnet " + ($arguments -join " "))
Write-Host ""

Push-Location $PSScriptRoot
try {
  if ($Log) {
    & dotnet $arguments 2>&1 | Tee-Object -FilePath $Log
    Write-Host ""
    Write-Host ("kept in " + $Log)
  }
  else {
    & dotnet $arguments
  }
  $code = $LASTEXITCODE
}
finally {
  Pop-Location
}

if ($code -ne 0) {
  Write-Host ""
  Write-Host "A lab failed. Two causes account for nearly all of it:"
  Write-Host "  - the engine is not at the commit the counts were taken at (README.md, Pins)"
  Write-Host "  - corpus-bin was built from an unpinned corpus (corpus-bin\PROVENANCE.txt)"
  Write-Host ""
}

exit $code
