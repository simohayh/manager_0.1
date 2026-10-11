# Run on YOUR computer (the one with the .NET SDK), from the project folder (where the .csproj is).
# Produces .\release\ - copy that folder to the restaurant's computer (no .NET install needed there).
param([string]$Runtime = "win-x64")

$ErrorActionPreference = "Stop"
dotnet publish -c Release -r $Runtime --self-contained true -o .\release
if ($LASTEXITCODE -ne 0) { throw "Build failed - fix the errors above first." }

Copy-Item -Recurse -Force .\scripts .\release\scripts -ErrorAction SilentlyContinue
Write-Host ""
Write-Host "Done. Copy the 'release' folder to the restaurant computer, then run scripts\setup-restaurant.ps1 there."
Write-Host "UPDATING an existing restaurant: stop the app, copy restaurant.db to a safe place, copy the new files over the old"
Write-Host "(do NOT overwrite restaurant.db, settings.json, wwwroot\uploads, start-restaurant.bat), then start it again."
