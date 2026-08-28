param(
    [Parameter(Mandatory = $true)]
    [string]$AabPath
)

$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $AabPath).Path
if ([IO.Path]::GetExtension($resolved) -ne '.aab') {
    throw "PLAY_ARTIFACT_FAIL: expected an .aab file, got '$resolved'"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$scratch = Join-Path ([IO.Path]::GetTempPath()) ("eoa-play-aab-audit-" + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($scratch) | Out-Null

$tokens = @(
    'solana', 'mobilewalletadapter', 'mobile_wallet_adapter', 'solana-wallet',
    'walletadapter', 'jupiter', 'jup.ag', 'skrvaluation', 'phantom', 'solflare',
    'seed vault', 'connect wallet',
    'SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3',
    '3BwWSAUZmyngXDSZiCawEnP7iLgY5ANNopBDz94AB77N'
)
$hits = [Collections.Generic.List[string]]::new()

function Test-StreamToken {
    param([string]$Path, [string]$Token)

    # Scan binary payloads as ASCII and UTF-16LE. Keep a small overlap so a token
    # split between reads cannot evade the artifact gate.
    $stream = [IO.File]::OpenRead($Path)
    try {
        $buffer = New-Object byte[] (1024 * 1024)
        $tailAscii = ''
        $tailUtf16 = ''
        while (($count = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $ascii = $tailAscii + [Text.Encoding]::ASCII.GetString($buffer, 0, $count)
            $utf16 = $tailUtf16 + [Text.Encoding]::Unicode.GetString($buffer, 0, $count - ($count % 2))
            if ($ascii.IndexOf($Token, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
                $utf16.IndexOf($Token, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
            $keep = [Math]::Max(0, $Token.Length - 1)
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
        foreach ($token in $tokens) {
            if ($relative.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $hits.Add("path:$relative token:$token")
            }
            if (Test-StreamToken -Path $file.FullName -Token $token) {
                $hits.Add("content:$relative token:$token")
            }
        }
    }
}
finally {
    if (Test-Path -LiteralPath $scratch) {
        Remove-Item -LiteralPath $scratch -Recurse -Force
    }
}

if ($hits.Count -gt 0) {
    $hits | Sort-Object -Unique | Select-Object -First 100 | ForEach-Object { Write-Error $_ }
    throw "PLAY_ARTIFACT_DIRTY: forbidden wallet/crypto material found in $resolved"
}

Write-Output "PLAY_ARTIFACT_CLEAN_OK $resolved"
