# Fail closed before Unity can rewrite ProjectVersion.txt and trigger a full reimport.
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion,
    [string]$SelectedVersion = ''
)

$versionFile = Join-Path $ProjectRoot 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path $versionFile)) {
    Write-Error "UNITY_EDITOR_PIN_MISSING: cannot read '$versionFile'." -ErrorAction Continue
    exit 9
}

$line = Get-Content $versionFile | Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)' } | Select-Object -First 1
if (-not $line -or $line -notmatch '^m_EditorVersion:\s*(\S+)') {
    Write-Error "UNITY_EDITOR_PIN_INVALID: '$versionFile' has no m_EditorVersion value." -ErrorAction Continue
    exit 9
}
$projectVersion = $Matches[1]

if ($projectVersion -ne $ExpectedVersion) {
    Write-Error "UNITY_EDITOR_PIN_MISMATCH: project=$projectVersion expected=$ExpectedVersion. Refusing to start Unity; proceeding would rewrite project metadata and force a full Bee rebuild." -ErrorAction Continue
    exit 9
}
if ($SelectedVersion -ne '' -and $SelectedVersion -ne $ExpectedVersion) {
    Write-Error "UNITY_EDITOR_SELECTION_MISMATCH: selected=$SelectedVersion expected=$ExpectedVersion. Refusing silent fallback to another installed editor." -ErrorAction Continue
    exit 9
}

Write-Host "[unity-pin] project=$projectVersion editor=$(if ($SelectedVersion) { $SelectedVersion } else { '(not selected yet)' }) OK"
# Explicit success exit: without it the script falls off the end WITHOUT setting
# $LASTEXITCODE, callers see $null, and their `-ne 0` guard fires on SUCCESS.
exit 0
