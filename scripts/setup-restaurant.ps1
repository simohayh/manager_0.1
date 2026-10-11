# Run ONCE on the restaurant's computer, from the folder that contains the published app (restaurant-app.exe / manager_0.1.exe).
# Right-click PowerShell -> "Run as administrator", then:  .\setup-restaurant.ps1 -Port 5000
#
# What it does: makes random PINs + secret, writes start-restaurant.bat, opens the firewall for PRIVATE networks only,
# and makes the app start automatically when Windows starts. Keep the PINs it prints; they are not shown again.
param(
    [int]$Port = 5000,
    [string]$ExeName = "",
    [string]$BackupCopyTo = ""      # e.g. "E:\restaurant-backups" (USB drive) or a OneDrive folder
)

$ErrorActionPreference = "Stop"
$dir = (Get-Location).Path

if (-not $ExeName) {
    $exe = Get-ChildItem -Path $dir -Filter *.exe | Where-Object { $_.Name -notmatch "createdump|unins" } | Select-Object -First 1
    if (-not $exe) { throw "No .exe found in $dir. Run this from the published app folder." }
    $ExeName = $exe.Name
}

function New-Pin { -join ((1..6) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 }) }
function New-Secret { -join ((1..48) | ForEach-Object { "abcdefghijkmnpqrstuvwxyz23456789"[(Get-Random -Minimum 0 -Maximum 32)] }) }

$bat = Join-Path $dir "start-restaurant.bat"
if (Test-Path $bat) {
    Write-Host "start-restaurant.bat already exists - keeping the existing PINs and secret." -ForegroundColor Yellow
} else {
    $kitchen = New-Pin; $cashier = New-Pin; $owner = New-Pin
    while ($cashier -eq $kitchen) { $cashier = New-Pin }
    while ($owner -eq $kitchen -or $owner -eq $cashier) { $owner = New-Pin }
    $secret = New-Secret

    $lines = @(
        "@echo off",
        "cd /d ""%~dp0""",
        "set ASPNETCORE_ENVIRONMENT=Production",
        "set ASPNETCORE_URLS=http://0.0.0.0:$Port",
        "set Staff__KitchenPin=$kitchen",
        "set Staff__CashierPin=$cashier",
        "set Staff__OwnerPin=$owner",
        "set Auth__Secret=$secret"
    )
    if ($BackupCopyTo) { $lines += "set Backup__CopyTo=$BackupCopyTo" }
    $lines += "$ExeName"
    $lines += "pause"
    Set-Content -Path $bat -Value $lines -Encoding ASCII

    Write-Host ""
    Write-Host "=== WRITE THESE DOWN (shown only now; also stored in start-restaurant.bat) ===" -ForegroundColor Green
    Write-Host "Kitchen PIN: $kitchen"
    Write-Host "Cashier PIN: $cashier"
    Write-Host "Owner PIN:   $owner"
    Write-Host "================================================================================"
}

# Firewall: private networks only (never public Wi-Fi)
if (-not (Get-NetFirewallRule -DisplayName "Restaurant App" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName "Restaurant App" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Profile Private | Out-Null
    Write-Host "Firewall rule added (Private networks, port $Port)."
}

# Start automatically when anyone logs in
$action  = New-ScheduledTaskAction -Execute $bat -WorkingDirectory $dir
$trigger = New-ScheduledTaskTrigger -AtLogOn
Register-ScheduledTask -TaskName "Restaurant App" -Action $action -Trigger $trigger -Force | Out-Null
Write-Host "The app will start automatically at Windows logon."

$ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -like "192.168.*" -or $_.IPAddress -like "10.*" } | Select-Object -First 1).IPAddress
Write-Host ""
Write-Host "Next: start it with start-restaurant.bat, then open  http://${ip}:$Port/admin.html  (owner PIN)."
Write-Host "Set a FIXED address for this computer in the router so the QR codes keep working."
