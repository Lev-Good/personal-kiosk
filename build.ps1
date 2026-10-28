param([string]$Configuration = 'Release', [switch]$PackageOnly)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$projectPath = Join-Path $PSScriptRoot 'PersonalKiosk\PersonalKiosk.csproj'
$projectXml = [xml](Get-Content -LiteralPath $projectPath -Raw)
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid project version' }
$distPath = Join-Path $PSScriptRoot 'dist'
$publishPath = Join-Path $distPath 'app'
$releasePath = Join-Path $distPath "PersonalKiosk-$version-win-x64"
New-Item -ItemType Directory -Path $releasePath -Force | Out-Null

$testReport = Join-Path $releasePath 'TEST-RESULTS.txt'
if (!$PackageOnly) {
    & dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true -o $publishPath
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    $testDll = Join-Path $PSScriptRoot "PersonalKiosk\bin\$Configuration\net10.0-windows\win-x64\PersonalKiosk.dll"
    & dotnet $testDll --self-test $testReport
    if ($LASTEXITCODE -ne 0) { throw 'Self-tests failed; package not produced' }
} elseif (!(Test-Path -LiteralPath $testReport) -or (Get-Content -LiteralPath $testReport -Last 1) -notmatch '^TOTAL \d+, FAILED 0$') {
    throw 'PackageOnly requires an existing successful test report. Run a full build first.'
}

Copy-Item -LiteralPath (Join-Path $publishPath 'PersonalKiosk.exe') -Destination (Join-Path $releasePath 'PersonalKioskSetup.exe') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RELEASE-CHECKLIST.md') -Destination $releasePath -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'מדריך למשתמש.md') -Destination $releasePath -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE.txt') -Destination $releasePath -Force
foreach ($notice in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'PersonalKiosk\Resources') -File) { Copy-Item -LiteralPath $notice.FullName -Destination $releasePath -Force }

$htmlBody = [System.Text.StringBuilder]::new()
foreach ($line in Get-Content -LiteralPath (Join-Path $PSScriptRoot 'מדריך למשתמש.md') -Encoding UTF8) {
    $encoded = [System.Net.WebUtility]::HtmlEncode($line)
    $encoded = [regex]::Replace($encoded, '`([^`]+)`', '<code dir="ltr">$1</code>')
    $encoded = [regex]::Replace($encoded, '\*\*([^*]+)\*\*', '<strong>$1</strong>')
    $encoded = [regex]::Replace($encoded, 'https://[A-Za-z0-9./_?=&;%#:+~-]+', '<a href="$0" target="_blank" rel="noopener noreferrer">$0</a>')
    if ($line.StartsWith('## ')) { [void]$htmlBody.AppendLine('<h2>' + $encoded.Substring(3) + '</h2>') }
    elseif ($line.StartsWith('# ')) { [void]$htmlBody.AppendLine('<h1>' + $encoded.Substring(2) + '</h1>') }
    elseif ($line.Trim().Length -gt 0) { [void]$htmlBody.AppendLine('<p>' + $encoded + '</p>') }
}
$html = @'
<!doctype html><html lang="he" dir="rtl"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>קיוסק אישי — מדריך שימוש</title>
<style>body{margin:0;background:#f4f7f9;color:#1a2b3e;font:17px/1.9 "Segoe UI",Arial,sans-serif}main{max-width:850px;margin:40px auto;background:white;padding:40px;border-radius:16px}h1,h2{color:#1c6961;line-height:1.4}h2{margin-top:38px}code{display:inline-block;max-width:100%;overflow-wrap:anywhere;background:#edf2f5;padding:0 5px;border-radius:4px;font-size:14px}strong{font-weight:650}@media(max-width:650px){main{padding:20px;margin:12px}}</style><main>
'@
$html += $htmlBody.ToString() + '</main></html>'
[System.IO.File]::WriteAllText((Join-Path $releasePath 'מדריך שימוש.html'), $html, [System.Text.UTF8Encoding]::new($false))

$hashLines = foreach ($file in Get-ChildItem -LiteralPath $releasePath -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name) { (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash + '  ' + $file.Name }
[System.IO.File]::WriteAllLines((Join-Path $releasePath 'SHA256SUMS.txt'), $hashLines)
Compress-Archive -LiteralPath $releasePath -DestinationPath (Join-Path $distPath "PersonalKiosk-$version-win-x64.zip") -Force

$sourceFiles = @((Join-Path $PSScriptRoot 'PersonalKiosk'), (Join-Path $PSScriptRoot 'build.ps1'), (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'LICENSE.txt'), (Join-Path $PSScriptRoot 'מדריך למשתמש.md'), (Join-Path $PSScriptRoot 'RELEASE-CHECKLIST.md'))
$sourceZip = Join-Path $distPath "PersonalKiosk-$version-source.zip"
# Add only source files; bin/obj and machine-specific state are never packaged.
Add-Type -AssemblyName System.IO.Compression
$zipStream = [System.IO.File]::Create($sourceZip)
$zip = [System.IO.Compression.ZipArchive]::new($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($item in $sourceFiles) {
        $sourceItem = Get-Item -LiteralPath $item
        $files = if ($sourceItem.PSIsContainer) { Get-ChildItem -LiteralPath $item -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } } else { @($sourceItem) }
        foreach ($file in $files) {
            $entryName = [System.IO.Path]::GetRelativePath($PSScriptRoot, $file.FullName).Replace('\','/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName) | Out-Null
        }
    }
} finally { $zip.Dispose(); $zipStream.Dispose() }
Get-Content -LiteralPath $testReport
Write-Output ('Package: ' + $releasePath)
