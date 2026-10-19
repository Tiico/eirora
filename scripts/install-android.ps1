[CmdletBinding()]
param([string]$Apk, [string]$Serial, [string]$Adb)
$ErrorActionPreference = 'Stop'
try {
    $repo = Split-Path $PSScriptRoot -Parent
    if (-not $Adb) {
        $command = Get-Command adb -ErrorAction SilentlyContinue
        if ($command) { $Adb = $command.Source }
        else {
            $version = ((Get-Content "$repo/client/ProjectSettings/ProjectVersion.txt" | Select-String '^m_EditorVersion:').Line -split ':', 2)[1].Trim()
            $Adb = "${env:ProgramFiles}/Unity/Hub/Editor/$version/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
        }
    }
    if (-not (Test-Path -LiteralPath $Adb -PathType Leaf)) { throw 'ADB saknas. Installera Unity Android Build Support eller ange -Adb.' }
    if (-not $Apk) {
        $candidates = @(Join-Path $repo 'client/Builds/Android/Eirora.apk')
        if (Test-Path "$repo/.claude/worktrees") {
            $candidates += @(Get-ChildItem "$repo/.claude/worktrees" -Directory | ForEach-Object {
                Join-Path $_.FullName 'client/Builds/Android/Eirora.apk'
                Join-Path $_.FullName 'artifacts/unity/Eirora.apk'
            })
        }
        $latest = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | ForEach-Object { Get-Item -LiteralPath $_ } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $latest) { throw 'Ingen APK hittades. Bygg Eirora.apk eller ange -Apk.' }
        $Apk = $latest.FullName
    }
    $Apk = (Resolve-Path -LiteralPath $Apk).Path
    if ([IO.Path]::GetExtension($Apk) -ne '.apk') { throw 'Filen maste vara en APK.' }
    $devices = @(& $Adb devices -l)
    if ($LASTEXITCODE -ne 0) { throw 'Kunde inte lista enheter.' }
    $ready = @($devices | Where-Object { $_ -match '^\S+\s+device\s' })
    if (-not $Serial) {
        $pixels = @($ready | Where-Object { $_ -match '\bmodel:Pixel_7\b' })
        if ($pixels.Count -eq 1) { $Serial = ($pixels[0] -split '\s+')[0] }
        elseif ($ready.Count -eq 1) { $Serial = ($ready[0] -split '\s+')[0] }
        else { throw "Anslut telefonen och godkann USB-felsokning. Vid flera enheter, ange -Serial. $($devices -join '; ')" }
    }
    if (-not ($ready | Where-Object { ($_ -split '\s+')[0] -eq $Serial })) { throw "Enheten $Serial ar inte redo. Godkann USB-felsokning pa telefonen." }
    Write-Host "APK: $Apk"
    Write-Host "Enhet: $Serial"
    & $Adb -s $Serial install -r $Apk
    if ($LASTEXITCODE -ne 0) { throw 'Installationen misslyckades. Kontrollera signeringsnyckeln vid signaturkonflikt. Ingen avinstallation eller radering gors av skriptet.' }
    Write-Host 'Eirora installerad. Oppna appen pa telefonen.'
}
catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
