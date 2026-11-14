# FlipKey uninstaller (also removes the old CapsSwitcher version).
foreach ($AppName in 'FlipKey', 'CapsSwitcher') {
    $Dest = Join-Path $env:LOCALAPPDATA $AppName

    Get-Process -Name $AppName -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300

    Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $AppName -ErrorAction SilentlyContinue
    Remove-Item -Path "HKCU:\Software\$AppName" -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $Dest) { Remove-Item -Recurse -Force $Dest }
}

Write-Host 'FlipKey removed.' -ForegroundColor Green
