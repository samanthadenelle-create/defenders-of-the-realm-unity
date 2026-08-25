# =============================================================================
# test_seatmail.ps1 - CLI-side parity check (WO-1200). Run on the Windows box to
# confirm the reader path (git show -> python3 seatmail.py) agrees with the logic
# verified on the UI seat. Proves acceptance 1 (surface oldest, pending=2) and
# 2 (ack one -> pending=1) end to end through the same commands the hooks use.
#   powershell -NoProfile -ExecutionPolicy Bypass -File seat-mail\test_seatmail.ps1
# =============================================================================
$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Py   = Join-Path $Root 'seat-mail\seatmail.py'
$Tmp  = Join-Path $env:TEMP ('seatmail_parity_{0}' -f $PID)
New-Item -ItemType Directory -Force -Path $Tmp | Out-Null
$Q = Join-Path $Tmp 'QUEUE.jsonl'
$C = Join-Path $Tmp 'cursor.json'
$M = Join-Path $Tmp 'msg'
$fail = 0

# The single-source selftest must pass under the CLI's own python3 first.
& python3 $Py selftest | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host 'FAIL seatmail.py selftest'; $fail++ }

# End-to-end through the real commands the hooks invoke.
& python3 $Py enqueue --queue $Q --msgdir $M --from ui-seat --utc '1970-01-01T00:00:00Z' --kind blocked   --subject first  --body older | Out-Null
& python3 $Py enqueue --queue $Q --msgdir $M --from ui-seat --utc '1970-01-01T00:00:01Z' --kind delivered --subject second --body newer | Out-Null

$p = (& python3 $Py pending --queue $Q --cursor $C)
if ($p -ne 'pending=2') { Write-Host "FAIL A1 pending: $p"; $fail++ }
$s = (& python3 $Py surface --queue $Q --cursor $C) -join "`n"
if ($s -notmatch 'subject: first') { Write-Host 'FAIL A1 surfaced newer not oldest'; $fail++ }

& python3 $Py ack --queue $Q --cursor $C | Out-Null
$p2 = (& python3 $Py pending --queue $Q --cursor $C)
if ($p2 -ne 'pending=1') { Write-Host "FAIL A2 after one ack: $p2 (F8 'ack the latest' bug!)"; $fail++ }

Remove-Item -Recurse -Force $Tmp -ErrorAction SilentlyContinue
if ($fail -eq 0) { Write-Host 'PARITY OK - A1(oldest,pending=2) A2(ack-one->1) on the CLI box' }
else { Write-Host "PARITY FAILED ($fail)"; exit 1 }
