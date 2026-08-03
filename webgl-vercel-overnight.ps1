# Overnight: build WebGL, then deploy Vercel PREVIEW. Run DETACHED (Start-Process)
# so it survives the agent-harness reaping of background batchmode Unity.
# Writes progress markers to Builds\webgl-chain-status.txt for the agent to poll.
Set-Location $PSScriptRoot
$status = 'Builds\webgl-chain-status.txt'
"CHAIN_START $(Get-Date -Format o)" | Out-File -Encoding ascii $status

try {
    .\build-webgl.ps1
} catch {
    "WEBGL_BUILD_THREW $_" | Out-File -Encoding ascii -Append $status
}

if (Test-Path 'Builds\WebGL\index.html') {
    "WEBGL_BUILD_OK $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
    "DEPLOY_START" | Out-File -Encoding ascii -Append $status
    $out = & vercel deploy --yes 2>&1
    $out | Out-File -Encoding ascii 'Builds\vercel-deploy.txt'
    # capture the deployment URL (vercel prints the https://... url)
    $url = ($out | Select-String -Pattern 'https://\S+' | Select-Object -Last 1).Matches.Value
    "DEPLOY_URL $url" | Out-File -Encoding ascii -Append $status
    "CHAIN_DONE $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
} else {
    "WEBGL_BUILD_FAILED_NO_INDEX $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
}
