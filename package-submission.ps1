# Builds a clean Staybnb submission zip on your Desktop.
# Usage (from the project folder):  powershell -ExecutionPolicy Bypass -File .\package-submission.ps1
param([string]$StudentNumber = "402411318")

$project = $PSScriptRoot
$name    = "Staybnb_$StudentNumber"
$staging = Join-Path $env:TEMP $name
$zip     = Join-Path ([Environment]::GetFolderPath("Desktop")) "$name.zip"

Write-Host "1/5 Checking the project builds..." -ForegroundColor Cyan
dotnet build $project -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed. Fix errors before submitting."; exit 1 }

Write-Host "2/5 Exporting the database schema as a SQL script..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path (Join-Path $project "Database") | Out-Null
dotnet ef migrations script --idempotent --project $project -o (Join-Path $project "Database\Staybnb_Schema.sql")

if (-not (Test-Path (Join-Path $project "Report\*.pdf"))) {
    Write-Warning "No PDF found in the Report folder. Add your report before the final submission."
}

Write-Host "3/5 Copying files (without bin, obj, .vs, .git)..." -ForegroundColor Cyan
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
robocopy $project $staging /E /XD bin obj .vs .git .vscode node_modules /XF *.user /NFL /NDL /NJH /NJS /NP | Out-Null

Write-Host "4/5 Zipping..." -ForegroundColor Cyan
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$staging\*" -DestinationPath $zip

$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 2)
Write-Host "5/5 Done: $zip ($sizeMb MB)" -ForegroundColor Green
if ($sizeMb -gt 50) { Write-Warning "Over the 50MB limit. Remove large photos from wwwroot\uploads and run again." }
