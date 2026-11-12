param([Parameter(Mandatory)][string]$ApplicationPath)
$ErrorActionPreference = 'Stop'
# Preserve the existing managed smoke deadline and stderr checks.
$proc = Start-Process -FilePath 'dotnet' -ArgumentList $ApplicationPath, '--smoke' -PassThru -RedirectStandardOutput smoke.out -RedirectStandardError smoke.err
$timedOut = !$proc.WaitForExit(15000)
if ($timedOut) {
  Stop-Process -Id $proc.Id -Force
}
$proc.WaitForExit()
Get-Content smoke.out
Get-Content smoke.err | ForEach-Object { [Console]::Error.WriteLine($_) }
if ($timedOut) { throw 'Mock run did not complete graceful shutdown within 15 seconds' }
if ($proc.ExitCode -ne 0) {
  exit $proc.ExitCode
}
if ((Get-Item smoke.err).Length -gt 0) {
  throw 'Mock run produced stderr output'
}
