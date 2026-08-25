# overnight-webgl-deploy.ps1 - DETACHED (Start-Process) so it survives the agent-harness
# reaping of background batchmode Unity. Builds WebGL (ship), then deploys a Vercel
# PREVIEW using the machine-local token. Writes progress markers the CLI polls.
# ASCII-only.
# Repo root is machine-dependent (C:\eoa on one box, D:\eoa on another) - resolve it from
# this script's own location instead of hardcoding a drive letter.
Set-Location $PSScriptRoot
$status = 'Builds\overnight-chain-status.txt'
New-Item -ItemType Directory -Force -Path 'Builds' | Out-Null
"CHAIN_START $(Get-Date -Format o)" | Out-File -Encoding ascii $status

# 1) Build WebGL (ship, BuildOptions.None). build-webgl.ps1 blocks until Unity exits.
try {
    & '.\build-webgl.ps1'
} catch {
    "WEBGL_BUILD_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $status
}

# grace: allow the filesystem/Unity relaunch to settle, poll up to 10 min for index.html
$grace = (Get-Date).AddMinutes(10)
while (-not (Test-Path 'Builds\WebGL\index.html') -and (Get-Date) -lt $grace) { Start-Sleep -Seconds 20 }

if (Test-Path 'Builds\WebGL\index.html') {
    $bytes = (Get-ChildItem 'Builds\WebGL' -Recurse -File | Measure-Object Length -Sum).Sum
    "WEBGL_BUILD_OK $(Get-Date -Format o) size=$([math]::Round($bytes/1MB,0))MB" | Out-File -Encoding ascii -Append $status

    # 2) Deploy Vercel PREVIEW (token + team scope; never --prod)
    "DEPLOY_START $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
    try {
        $t = (Get-Content (Join-Path $PSScriptRoot '.vercel-token') -Raw).Trim()
        $out = & vercel deploy --yes --token $t --scope 'team_2PrmHqE5mM52aIrzPJNHmyEt' 2>&1
        ($out | ForEach-Object { $_.ToString() }) | Out-File -Encoding ascii 'Builds\vercel-deploy.txt'
        # Take the LAST bare deployment URL. Filter out api.vercel.com/... (the CLI prints
        # inspector/API links too - matching those yields a URL that is not the deployment).
        $url = ($out | Select-String -Pattern 'https://[a-z0-9-]+\.vercel\.app' -AllMatches |
                ForEach-Object { $_.Matches } | ForEach-Object { $_.Value } |
                Where-Object { $_ -notmatch 'api\.vercel\.com' } | Select-Object -Last 1)
        if ($url) {
            "DEPLOY_URL $url" | Out-File -Encoding ascii -Append $status
            # The WEB F8 watcher (websig-watch-daemon.ps1) re-reads this EVERY poll to find the
            # admin endpoint, because preview URLs rotate on each deploy. Without this write the
            # watcher keeps polling a dead preview and silently stops covering the live build -
            # exactly the "looks like coverage, fires never" failure this whole lane exists to kill.
            $url | Out-File -Encoding ascii 'Builds\admin-preview-url.txt'
            "ADMIN_URL_WRITTEN Builds\admin-preview-url.txt" | Out-File -Encoding ascii -Append $status
        }
        else { "DEPLOY_NO_URL - see Builds\vercel-deploy.txt" | Out-File -Encoding ascii -Append $status }
    } catch {
        "DEPLOY_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $status
    }
    "CHAIN_DONE $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
} else {
    "WEBGL_BUILD_FAILED_NO_INDEX $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
    "CHAIN_DONE $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
}
