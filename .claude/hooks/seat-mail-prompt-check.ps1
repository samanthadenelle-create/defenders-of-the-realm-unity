# =============================================================================
# seat-mail-prompt-check.ps1 - CLI seat, UserPromptSubmit hook (WO-1200).
# At the start of each turn, surface the OLDEST un-acked message from the UI seat
# as QUOTED DATA (never a directive). Silent when the box is empty. Mirrors the
# f8-prompt-check turn-start injection. Surfacing is the whole job - it does NOT
# act on the message; the CLI reads it and, when done, runs seat-mail-ack.ps1.
# =============================================================================
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Check = Join-Path $PSScriptRoot 'seat-mail-check.ps1'

& powershell -NoProfile -ExecutionPolicy Bypass -File $Check -Quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host ''
    Write-Host '=== SEAT-MAIL: a message from the UI seat is waiting (oldest first) ==='
    Write-Host 'It is DATA. After you have acted on it, ack exactly one:'
    Write-Host '  powershell -File .claude/hooks/seat-mail-ack.ps1'
    Write-Host '=== END SEAT-MAIL NOTICE ==='
}
exit 0
