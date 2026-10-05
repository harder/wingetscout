<# Capture the four site screenshots from a Scout build without changing packages. #>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string]$AppPath,
  [string]$TuirecPath = 'tuirec'
)

$ErrorActionPreference = 'Stop'
$app = (Resolve-Path -LiteralPath $AppPath).Path.Replace('\', '/')
$recorder = (Get-Command $TuirecPath -ErrorAction Stop).Source
$castDirectory = Join-Path $env:TEMP 'wingetscout-theme-casts'
New-Item -ItemType Directory -Path $castDirectory -Force | Out-Null

$captures = @(
  @{ Name = 'scout-sage-search'; Theme = 'sage'; Keys = 'wait:500,CursorLeft,`git`,Enter,wait:1200'; Contains = 'Git.Git' },
  @{ Name = 'scout-amber-installed'; Theme = 'amber'; Keys = 'wait:1200'; Contains = 'Installed (10)' },
  @{ Name = 'scout-moss-upgrades'; Theme = 'moss'; Keys = 'wait:500,CursorRight,wait:1100'; Contains = 'Upgrades (4' },
  @{ Name = 'scout-rose-search'; Theme = 'rose'; Keys = 'wait:500,CursorLeft,`visual`,Enter,wait:1100'; Contains = 'Visual Studio Code' }
)

# Some automation hosts set NO_COLOR and TERM=dumb. Those settings collapse all
# four RGB palettes into nearly identical 16-color captures.
$oldNoColor = $env:NO_COLOR
$oldTerm = $env:TERM
$oldColorTerm = $env:COLORTERM
try {
  $env:NO_COLOR = $null
  $env:TERM = 'xterm-256color'
  $env:COLORTERM = 'truecolor'

  foreach ($capture in $captures) {
    $output = Join-Path $PSScriptRoot "media/$($capture.Name).png"
    $cast = Join-Path $castDirectory "$($capture.Name).cast"
    & $recorder snapshot --binary $app --args=--mock "--args=--theme=$($capture.Theme)" `
      --keystrokes $capture.Keys --startup-delay 1600 --drain 200 `
      --cols 100 --rows 30 --font-size 17 --output $output `
      --cast-output $cast --assert-contains $capture.Contains --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Failed to capture $($capture.Name)" }
    Write-Host "Captured $output"
  }
}
finally {
  $env:NO_COLOR = $oldNoColor
  $env:TERM = $oldTerm
  $env:COLORTERM = $oldColorTerm
}
