# overnight-apk-build.ps1 - DETACHED Seeker APK build (survives harness reaping).
# Runs the Android batchmode build, writes status markers. ASCII-only.
Set-Location 'D:\eoa'
$status = 'Builds\overnight-apk-status.txt'
New-Item -ItemType Directory -Force -Path 'Builds' | Out-Null
"APK_START $(Get-Date -Format o)" | Out-File -Encoding ascii $status
try {
    & '.\run-unity-method.ps1' -Method DeNelle.Editor.AndroidBuild.BuildSeekerApk -LogName apk-build.log -TimeoutMin 120
} catch {
    "APK_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $status
}
$apk = Get-ChildItem 'D:\eoa\Builds' -Recurse -Filter *.apk -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($apk) {
    "APK_OK $(Get-Date -Format o) path=$($apk.FullName) size=$([math]::Round($apk.Length/1MB,0))MB" | Out-File -Encoding ascii -Append $status
} else {
    "APK_FAILED_NO_APK $(Get-Date -Format o) (see Builds\apk-build.log)" | Out-File -Encoding ascii -Append $status
}
"APK_DONE $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
