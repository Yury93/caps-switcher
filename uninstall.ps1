# CapsSwitcher uninstaller.
$AppName = 'CapsSwitcher'
$Dest    = Join-Path $env:LOCALAPPDATA $AppName

Get-Process -Name $AppName -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $AppName -ErrorAction SilentlyContinue
if (Test-Path $Dest) { Remove-Item -Recurse -Force $Dest }

Write-Host 'CapsSwitcher removed.' -ForegroundColor Green
