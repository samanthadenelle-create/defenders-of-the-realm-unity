# WO-1199 B1: prove native stderr cannot truncate later stderr/stdout.
# ASCII-only. No Vercel, credentials, network, Unity, or database required.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$script = Join-Path $root 'tools\command-centre.ps1'
$log = Join-Path ([System.IO.Path]::GetTempPath()) 'eoa-command-centre-capture-test.log'

. $script -LibraryOnly

try {
    $code = Invoke-Captured {
        & cmd /d /c 'echo STDERR_ONE 1>&2 & echo STDERR_TWO 1>&2 & echo STDOUT_THREE & exit /b 0'
    } $log | Select-Object -Last 1
    $text = Get-Content -LiteralPath $log -Raw
    foreach ($marker in @('STDERR_ONE', 'STDERR_TWO', 'STDOUT_THREE')) {
        if ($text -notmatch $marker) { throw "CAPTURE_FAIL missing=$marker log=$log" }
    }
    if ($code -ne 0) { throw "CAPTURE_FAIL exit=$code log=$log" }
    Write-Host 'COMMAND_CENTRE_CAPTURE_OK stderr=2 stdout=1 exit=0'
} finally {
    Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue
}
