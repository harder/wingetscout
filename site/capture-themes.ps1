<# Capture site screenshots and short demos from a Scout build without changing packages. #>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string]$AppPath,
  [string]$TuirecPath = 'tuirec'
)

$ErrorActionPreference = 'Stop'
$app = (Resolve-Path -LiteralPath $AppPath).Path.Replace('\', '/')
$recorder = (Get-Command $TuirecPath -ErrorAction Stop).Source
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
    & $recorder snapshot --binary $app --args=--mock "--args=--theme=$($capture.Theme)" `
      --keystrokes $capture.Keys --startup-delay 1600 --drain 200 `
      --cols 140 --rows 34 --font-size 16 --output $output `
      --assert-contains $capture.Contains --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Failed to capture $($capture.Name)" }
    Write-Host "Captured $output"
  }

  $demos = @(
    @{ Name = 'scout-search-demo'; Theme = 'sage'; Keys = 'wait:500,CursorLeft,wait:300,`git`,Enter,wait:900,CursorDown,wait:650,CursorUp,wait:800' },
    @{ Name = 'scout-upgrades-demo'; Theme = 'moss'; Keys = 'wait:500,CursorRight,wait:900,CursorDown,wait:650,CursorUp,wait:800' }
  )
  foreach ($demo in $demos) {
    $output = Join-Path $PSScriptRoot "media/$($demo.Name).gif"
    & $recorder record --binary $app --args=--mock "--args=--theme=$($demo.Theme)" `
      --keystrokes $demo.Keys --startup-delay 1600 --drain 300 `
      --cols 140 --rows 34 --font-size 16 --output $output --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Failed to capture $($demo.Name)" }
    Write-Host "Captured $output"
  }
}
finally {
  $env:NO_COLOR = $oldNoColor
  $env:TERM = $oldTerm
  $env:COLORTERM = $oldColorTerm
}
