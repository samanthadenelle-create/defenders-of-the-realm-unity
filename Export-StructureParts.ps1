# Fixed Export-StructureParts.ps1
# Run this from your Unity project root folder

$projectRoot = Get-Location
$outputFile = "$projectRoot\StructureParts_Export.txt"

Write-Host "Scanning for structure parts in project..." -ForegroundColor Cyan

$results = @()
$results += "=== ALL FOUND STRUCTURE PARTS ===`n"
$results += "Generated on $(Get-Date)`n`n"

$count = 0

Get-ChildItem -Recurse -Filter "*.cs" | ForEach-Object {
    $file = $_.FullName
    $content = Get-Content $file -Raw -ErrorAction SilentlyContinue
    
    if (-not $content) { return }
    
    # Find itemId and id assignments
    $matches = [regex]::Matches($content, '(?i)(?:itemId|id)\s*[=:]\s*["'']([^"''\s]+)["'']')
    foreach ($m in $matches) {
        $id = $m.Groups[1].Value.Trim()
        if ($id.Length -gt 2) {
            $results += "• $id"
            $count++
        }
    }
}

# Second pass for known structure patterns
Get-ChildItem -Recurse -Include "*.cs","*.json","*.asset" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return }
    
    $pattern = '["'']([a-zA-Z0-9_-]+_(?:wall|tower|gate|hall|forge|windmill|barracks|roof|floor|keep|barn|stable|smithy))["'']'
    $matches = [regex]::Matches($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    foreach ($m in $matches) {
        $id = $m.Groups[1].Value
        $results += "• $id  (detected pattern)"
        $count++
    }
}

# Remove duplicates and sort
$unique = $results | Select-Object -Unique | Sort-Object

$unique | Out-File -FilePath $outputFile -Encoding utf8

Write-Host "✅ Export complete! Found approximately $count potential structure IDs." -ForegroundColor Green
Write-Host "File saved to: $outputFile" -ForegroundColor Green

# Try to open the file
try { Invoke-Item $outputFile } catch { Write-Host "File created but could not auto-open." }

Write-Host "`nPlease copy and paste the relevant parts (especially anything with wall, tower, gate, hall, forge, windmill, barracks, etc.) back to me." -ForegroundColor Yellow