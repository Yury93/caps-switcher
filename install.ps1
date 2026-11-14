# FlipKey installer.
# Local:  double-click install.cmd
# Remote: irm https://raw.githubusercontent.com/Yury93/flip-key/main/install.ps1 | iex

$ErrorActionPreference = 'Stop'
$AppName = 'FlipKey'
$Dest    = Join-Path $env:LOCALAPPDATA $AppName
$Exe     = Join-Path $Dest "$AppName.exe"
$RawUrl  = 'https://raw.githubusercontent.com/Yury93/flip-key/main/src/FlipKey.cs'

# 1. Source file: next to this script, or download from GitHub.
$src = $null
if ($PSScriptRoot) {
    $candidate = Join-Path $PSScriptRoot 'src\FlipKey.cs'
    if (Test-Path $candidate) { $src = $candidate }
}
if (-not $src) {
    Write-Host 'Downloading source from GitHub...'
    $src = Join-Path $env:TEMP 'FlipKey.cs'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $RawUrl -OutFile $src
}

# 2. Built-in C# compiler from .NET Framework 4.x (ships with Windows 10/11).
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '.NET Framework 4.x compiler not found (csc.exe).' }

# 3. Stop a running copy so the exe can be replaced.
Get-Process -Name $AppName -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

# 3a. Remove the previous version installed under the old name (CapsSwitcher),
#     keeping the chosen hotkey.
$OldName = 'CapsSwitcher'
Get-Process -Name $OldName -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $OldName -ErrorAction SilentlyContinue
$oldDir = Join-Path $env:LOCALAPPDATA $OldName
if (Test-Path $oldDir) { Start-Sleep -Milliseconds 300; Remove-Item -Recurse -Force $oldDir -ErrorAction SilentlyContinue }
$oldKey = "HKCU:\Software\$OldName"
$newKey = "HKCU:\Software\$AppName"
if (Test-Path $oldKey) {
    $oldHotkey = (Get-ItemProperty -Path $oldKey -ErrorAction SilentlyContinue).Hotkey
    if ($oldHotkey -and -not (Test-Path $newKey)) {
        New-Item -Path $newKey -Force | Out-Null
        Set-ItemProperty -Path $newKey -Name Hotkey -Value $oldHotkey
    }
    Remove-Item -Path $oldKey -Recurse -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path $Dest | Out-Null

Write-Host 'Building...'
& $csc /nologo /target:winexe /optimize+ /codepage:65001 `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    "/out:$Exe" $src
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# 4. Start with Windows (current user, no admin rights needed).
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Set-ItemProperty -Path $runKey -Name $AppName -Value "`"$Exe`""

# 5. Launch now.
Start-Process -FilePath $Exe

Write-Host ''
Write-Host "Installed to $Dest" -ForegroundColor Green
Write-Host 'FlipKey is running (EN/RU icon in the tray) and will start with Windows.'
Write-Host 'Press F1 to switch the layout of the text you typed.'
