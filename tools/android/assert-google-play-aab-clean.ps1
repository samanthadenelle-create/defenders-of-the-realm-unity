<#
    Standalone Google Play AAB cleanliness scanner (WO-1255, widened by WO-1364).

    THE TOKEN POLICY IS NOT DUPLICATED HERE ANY MORE.
    Assets/Editor/Regression/GooglePlayPackagingGate.cs owns ForbiddenTokens,
    ShortTokensRequiringTextContext, FalsePositiveAllowlist and
    MinPrintableRunForShortTokens; this script PARSES them out of that file at run
    time. The two copies used to be maintained by hand and had already drifted once,
    which is the same duplicated-state failure CLAUDE.md sec.2/sec.5/sec.16 all record.
    The compiled gate keeps its literals (a gate that can lose its policy to a missing
    data file is a hollow gate); this script fails CLOSED - it throws - if it cannot
    read and parse them.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$AabPath,
    [string]$GateSourcePath
)

$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $AabPath).Path
if ([IO.Path]::GetExtension($resolved) -ne '.aab') {
    throw "PLAY_ARTIFACT_FAIL: expected an .aab file, got '$resolved'"
}

if (-not $GateSourcePath) {
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $GateSourcePath = Join-Path $repoRoot 'Assets/Editor/Regression/GooglePlayPackagingGate.cs'
}
if (-not (Test-Path -LiteralPath $GateSourcePath)) {
    throw "PLAY_ARTIFACT_FAIL: token policy source not found at '$GateSourcePath'; refusing to scan with an empty policy"
}
$gateSource = [IO.File]::ReadAllText($GateSourcePath)

function Get-GateTokenArray {
    param([string]$Source, [string]$Name)

    # Pull one `private static readonly string[] Name = { ... };` block out of the C#
    # gate and return its string literals. Comments are stripped first; the gate's own
    # doc comment requires them to stay free of double quotes so this stays simple.
    $anchor = $Source.IndexOf("string[] $Name", [StringComparison]::Ordinal)
    if ($anchor -lt 0) { throw "PLAY_ARTIFACT_FAIL: $Name not found in $GateSourcePath" }
    $open = $Source.IndexOf('{', $anchor)
    $close = $Source.IndexOf('};', $open)
    if ($open -lt 0 -or $close -lt 0) { throw "PLAY_ARTIFACT_FAIL: $Name block is unreadable in $GateSourcePath" }
    $block = $Source.Substring($open + 1, $close - $open - 1)

    $tokens = [Collections.Generic.List[string]]::new()
    foreach ($line in $block -split "`n") {
        $text = $line
        $comment = $text.IndexOf('//')
        if ($comment -ge 0) {
            $quotesBefore = ([regex]::Matches($text.Substring(0, $comment), '"')).Count
            if ($quotesBefore % 2 -eq 0) { $text = $text.Substring(0, $comment) }
        }
        foreach ($match in [regex]::Matches($text, '"([^"]*)"')) {
            $tokens.Add($match.Groups[1].Value)
        }
    }
    if ($tokens.Count -eq 0) { throw "PLAY_ARTIFACT_FAIL: $Name parsed empty from $GateSourcePath" }
    return $tokens.ToArray()
}

function Get-GateIntConst {
    param([string]$Source, [string]$Name, [int]$Fallback)
    $match = [regex]::Match($Source, "const\s+int\s+$Name\s*=\s*(\d+)")
    if ($match.Success) { return [int]$match.Groups[1].Value }
    return $Fallback
}

$forbiddenTokens = Get-GateTokenArray -Source $gateSource -Name 'ForbiddenTokens'
$shortTokens = Get-GateTokenArray -Source $gateSource -Name 'ShortTokensRequiringTextContext'
$allowlist = Get-GateTokenArray -Source $gateSource -Name 'FalsePositiveAllowlist'
$minPrintableRun = Get-GateIntConst -Source $gateSource -Name 'MinPrintableRunForShortTokens' -Fallback 12

Add-Type -AssemblyName System.IO.Compression.FileSystem
$scratch = Join-Path ([IO.Path]::GetTempPath()) ("eoa-play-aab-audit-" + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($scratch) | Out-Null

$hits = [Collections.Generic.List[string]]::new()

function Test-PrintableRun {
    param([string]$Text, [int]$Hit, [int]$Length, [int]$MinRun)
    $isPrintable = { param([char]$c) $code = [int]$c; ($code -ge 32 -and $code -le 126) -or $code -eq 9 }
    for ($i = $Hit; $i -lt ($Hit + $Length) -and $i -lt $Text.Length; $i++) {
        if (-not (& $isPrintable $Text[$i])) { return $false }
    }
    $left = $Hit
    while ($left -gt 0 -and (& $isPrintable $Text[$left - 1])) { $left-- }
    $right = $Hit + $Length
    while ($right -lt $Text.Length -and (& $isPrintable $Text[$right])) { $right++ }
    return (($right - $left) -ge $MinRun)
}

function Test-AllowlistedOccurrence {
    param([string]$Text, [int]$Hit, [string]$Token, [string[]]$Allowlist)
    foreach ($allow in $Allowlist) {
        if ($allow.IndexOf($Token, [StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }
        $windowStart = [Math]::Max(0, $Hit - $allow.Length)
        $windowEnd = [Math]::Min($Text.Length, $Hit + $Token.Length + $allow.Length)
        $found = $Text.IndexOf($allow, $windowStart, $windowEnd - $windowStart, [StringComparison]::OrdinalIgnoreCase)
        while ($found -ge 0) {
            if ($found -le $Hit -and ($found + $allow.Length) -ge ($Hit + $Token.Length)) { return $true }
            $next = $found + 1
            if ($next -ge $windowEnd) { break }
            $found = $Text.IndexOf($allow, $next, $windowEnd - $next, [StringComparison]::OrdinalIgnoreCase)
        }
    }
    return $false
}

function Test-TokenInText {
    # Mirror of GooglePlayPackagingGate.MatchesTokenInPayload. ReadableEntry means the
    # payload is text end to end, so a short token needs no printable-run corroboration.
    param(
        [string]$Text,
        [string]$Token,
        [bool]$ReadableEntry = $true,
        [string[]]$ShortTokens = @(),
        [string[]]$Allowlist = @(),
        [int]$MinRun = 12
    )
    if ([string]::IsNullOrEmpty($Text) -or [string]::IsNullOrEmpty($Token)) { return $false }
    $isShort = (-not $ReadableEntry) -and ($ShortTokens -contains $Token)
    $start = 0
    while ($start -lt $Text.Length) {
        $hit = $Text.IndexOf($Token, $start, [StringComparison]::OrdinalIgnoreCase)
        if ($hit -lt 0) { return $false }
        $start = $hit + 1

        $needsBoundary = [char]::IsLetterOrDigit($Token[0])
        if ($needsBoundary -and $hit -ne 0 -and [char]::IsLetterOrDigit($Text[$hit - 1])) { continue }

        if ($isShort) {
            $after = $hit + $Token.Length
            if ($after -lt $Text.Length -and [char]::IsLetterOrDigit($Text[$after])) { continue }
            if (-not (Test-PrintableRun -Text $Text -Hit $hit -Length $Token.Length -MinRun $MinRun)) { continue }
        }

        if (Test-AllowlistedOccurrence -Text $Text -Hit $hit -Token $Token -Allowlist $Allowlist) { continue }

        return $true
    }
    return $false
}

function Find-StreamTokens {
    param(
        [string]$Path,
        [string[]]$Tokens,
        [bool]$ReadableEntry = $true,
        [string[]]$ShortTokens = @(),
        [string[]]$Allowlist = @(),
        [int]$MinRun = 12
    )

    # One pass over the payload for the WHOLE vocabulary. Test-StreamToken below keeps the
    # single-token contract, but calling it per token re-read a 500 MB artifact 30 times.
    $found = [Collections.Generic.List[string]]::new()
    $pending = [Collections.Generic.List[string]]::new()
    $pending.AddRange($Tokens)
    $maxTokenLength = 0
    foreach ($t in $Tokens) { if ($t.Length -gt $maxTokenLength) { $maxTokenLength = $t.Length } }

    $stream = [IO.File]::OpenRead($Path)
    try {
        $buffer = New-Object byte[] (1024 * 1024)
        $tailAscii = ''
        $tailUtf16 = ''
        $latin1 = [Text.Encoding]::GetEncoding(28591)
        while (($count = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            if ($pending.Count -eq 0) { break }
            $ascii = $tailAscii + $latin1.GetString($buffer, 0, $count)
            $utf16 = $tailUtf16 + [Text.Encoding]::Unicode.GetString($buffer, 0, $count - ($count % 2))
            foreach ($token in @($pending)) {
                if ((Test-TokenInText -Text $ascii -Token $token -ReadableEntry $ReadableEntry -ShortTokens $ShortTokens -Allowlist $Allowlist -MinRun $MinRun) -or
                    (Test-TokenInText -Text $utf16 -Token $token -ReadableEntry $ReadableEntry -ShortTokens $ShortTokens -Allowlist $Allowlist -MinRun $MinRun)) {
                    $found.Add($token) | Out-Null
                    $pending.Remove($token) | Out-Null
                }
            }
            $keep = [Math]::Max($maxTokenLength - 1, (4 * $MinRun) + 128)
            $tailAscii = if ($ascii.Length -gt $keep) { $ascii.Substring($ascii.Length - $keep) } else { $ascii }
            $tailUtf16 = if ($utf16.Length -gt $keep) { $utf16.Substring($utf16.Length - $keep) } else { $utf16 }
        }
    }
    finally { $stream.Dispose() }
    return $found.ToArray()
}

function Test-StreamToken {
    param(
        [string]$Path,
        [string]$Token,
        [bool]$ReadableEntry = $true,
        [string[]]$ShortTokens = @(),
        [string[]]$Allowlist = @(),
        [int]$MinRun = 12
    )

    # Scan binary payloads as ASCII and UTF-16LE. Keep a small overlap so a token
    # split between reads - or a printable run clipped by a read - cannot evade the gate.
    $stream = [IO.File]::OpenRead($Path)
    try {
        $buffer = New-Object byte[] (1024 * 1024)
        $tailAscii = ''
        $tailUtf16 = ''
        # Latin-1 (28591), NOT ASCII: ASCII folds every byte above 0x7F to '?', which is a
        # printable character, so random binary would satisfy the printable-run rule.
        $latin1 = [Text.Encoding]::GetEncoding(28591)
        while (($count = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $ascii = $tailAscii + $latin1.GetString($buffer, 0, $count)
            $utf16 = $tailUtf16 + [Text.Encoding]::Unicode.GetString($buffer, 0, $count - ($count % 2))
            if ((Test-TokenInText -Text $ascii -Token $Token -ReadableEntry $ReadableEntry -ShortTokens $ShortTokens -Allowlist $Allowlist -MinRun $MinRun) -or
                (Test-TokenInText -Text $utf16 -Token $Token -ReadableEntry $ReadableEntry -ShortTokens $ShortTokens -Allowlist $Allowlist -MinRun $MinRun)) { return $true }
            $keep = [Math]::Max($Token.Length - 1, (4 * $MinRun) + 128)
            $tailAscii = if ($ascii.Length -gt $keep) { $ascii.Substring($ascii.Length - $keep) } else { $ascii }
            $tailUtf16 = if ($utf16.Length -gt $keep) { $utf16.Substring($utf16.Length - $keep) } else { $utf16 }
        }
        return $false
    }
    finally { $stream.Dispose() }
}

try {
    [IO.Compression.ZipFile]::ExtractToDirectory($resolved, $scratch)
    foreach ($file in Get-ChildItem -LiteralPath $scratch -File -Recurse) {
        $relative = $file.FullName.Substring($scratch.Length + 1).Replace('\', '/')
        if ($relative.Equals('BUNDLE-METADATA/com.unity/dependencies.pb', [StringComparison]::OrdinalIgnoreCase)) {
            continue # provenance receipt; actual executable leakage is scanned elsewhere
        }
        $isUserFacing = $relative.StartsWith('base/assets/Data/Canonical/', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.EndsWith('.txt', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.EndsWith('.html', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.EndsWith('.xml', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.EndsWith('.uxml', [StringComparison]::OrdinalIgnoreCase)
        # WO-1364: one vocabulary for every entry. The entry class only decides how much
        # corroboration a SHORT token needs, never which tokens are enforced.
        $tokens = $forbiddenTokens
        # Mirror of GooglePlayPackagingGate.IsSignatureDigestEntry: base64 SHA digests are
        # long printable runs of arbitrary characters, so short tokens cannot be judged there.
        $isDigestListing = $relative.StartsWith('META-INF/', [StringComparison]::OrdinalIgnoreCase) -and
            ($relative.EndsWith('/MANIFEST.MF', [StringComparison]::OrdinalIgnoreCase) -or
             $relative.EndsWith('.SF', [StringComparison]::OrdinalIgnoreCase))
        if ($isDigestListing) { $tokens = @($forbiddenTokens | Where-Object { $shortTokens -notcontains $_ }) }
        foreach ($token in $tokens) {
            # An entry NAME is always text, so no printable-run rule applies to it.
            if (Test-TokenInText -Text $relative -Token $token -ReadableEntry $true -ShortTokens $shortTokens -Allowlist $allowlist -MinRun $minPrintableRun) {
                $hits.Add("path:$relative token:$token")
            }
        }
        foreach ($token in (Find-StreamTokens -Path $file.FullName -Tokens $tokens -ReadableEntry $isUserFacing -ShortTokens $shortTokens -Allowlist $allowlist -MinRun $minPrintableRun)) {
            $hits.Add("content:$relative token:$token")
        }
    }
}
finally {
    if (Test-Path -LiteralPath $scratch) {
        Remove-Item -LiteralPath $scratch -Recurse -Force
    }
}

if ($hits.Count -gt 0) {
    # Write-Output, not Write-Error: with $ErrorActionPreference = 'Stop' the first
    # Write-Error terminated the script, so a dirty artifact reported ONE hit and hid
    # the rest. The gate has to name every entry it found.
    $hits | Sort-Object -Unique | Select-Object -First 100 | ForEach-Object { Write-Output "PLAY_ARTIFACT_DIRTY_HIT $_" }
    throw "PLAY_ARTIFACT_DIRTY: forbidden wallet/crypto material found in $resolved"
}

Write-Output "PLAY_ARTIFACT_CLEAN_OK $resolved"
