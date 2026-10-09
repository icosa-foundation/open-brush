param(
    [Parameter(Mandatory)][string]$Serial,
    [switch]$InstallAndStart,
    [switch]$UpdateProbe,
    [string]$ExpectedInstalledHash,
    [string]$HeadsetSessionId
)
$ErrorActionPreference = 'Stop'
$package = 'foundation.icosa.obdsunityprobe20261009'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$probe = Join-Path $repo 'LocalPlanning/AndroidDirectStorage/UnityProbe'
$apk = Join-Path $probe 'Build/obds-unity-probe.apk'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
if ($InstallAndStart -and $UpdateProbe) { throw 'Choose first install or explicit probe update, not both.' }
if ($InstallAndStart -or $UpdateProbe) {
    $reservationPath = 'C:/Users/andyb/Documents/open-brush-fast/.headset-reservation.json'
    if (!$HeadsetSessionId -or !(Test-Path -LiteralPath $reservationPath) -or
        (Get-Content -Raw -LiteralPath $reservationPath | ConvertFrom-Json).sessionId -ne $HeadsetSessionId) {
        throw 'Acquire the shared headset reservation atomically and pass its owner as -HeadsetSessionId before deployment.'
    }
}
$evidence = Join-Path $repo "LocalPlanning/AndroidDirectStorage/Evidence/Unity-$stamp"
New-Item -ItemType Directory -Force $evidence | Out-Null

$deviceState = & adb -s $Serial get-state 2>&1
if ($LASTEXITCODE -ne 0 -or $deviceState -ne 'device') {
    if ($Serial -notmatch '^(\d{1,3}\.){3}\d{1,3}:\d+$') {
        throw "Device $Serial is unavailable. Reconnect it before collecting evidence."
    }
    # Restore only this known TCP transport; never kill or restart a shared server.
    & adb connect $Serial 2>&1 | Out-File (Join-Path $evidence 'reconnect.txt')
    if ($LASTEXITCODE -ne 0) { throw "Could not reconnect $Serial." }
}

function Invoke-Adb([string[]]$Arguments) {
    $result = & adb -s $Serial @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "adb failed: $($Arguments -join ' '): $result" }
    return $result
}

if (!(Test-Path -LiteralPath $apk)) { throw "Build artifact missing: $apk" }
Get-FileHash -LiteralPath $apk -Algorithm SHA256 | Format-List |
    Out-File (Join-Path $evidence 'apk-sha256.txt')
Invoke-Adb -Arguments @('shell', 'getprop', 'ro.build.fingerprint') | Out-File (Join-Path $evidence 'fingerprint.txt')
Invoke-Adb -Arguments @('shell', 'getprop', 'ro.build.type') | Out-File (Join-Path $evidence 'build-type.txt')
$installed = Invoke-Adb -Arguments @('shell', 'pm', 'list', 'packages', $package) |
    Where-Object { $_ -eq "package:$package" }
if ($UpdateProbe) {
    if (!$installed -or !$ExpectedInstalledHash) {
        throw 'Probe update requires an installed package and its recorded expected APK SHA256.'
    }
    $oldApk = Invoke-Adb -Arguments @('shell', 'pm', 'path', $package) |
        Where-Object { $_ -match '^package:.*\/base\.apk$' } | Select-Object -First 1
    if (!$oldApk) { throw 'Cannot identify installed probe base APK.' }
    $oldDigest = Invoke-Adb -Arguments @('shell', 'sha256sum', $oldApk.Substring(8))
    if (($oldDigest -split '\s+')[0] -ne $ExpectedInstalledHash) {
        throw 'Installed probe hash differs from expected revision. Refusing update.'
    }
    # Use only with authorization to update this test app. Preserve data and grants.
    Invoke-Adb -Arguments @('install', '-r', $apk) | Out-File (Join-Path $evidence 'update.txt')
    Invoke-Adb -Arguments @('shell', 'am', 'start', '-n', "$package/foundation.icosa.obds.ProbeActivity") |
        Out-File (Join-Path $evidence 'launch.txt')
}
elseif ($InstallAndStart) {
    if ($installed) { throw 'Unique probe package already installed. Refusing to replace it.' }
    Invoke-Adb -Arguments @('install', $apk) | Out-File (Join-Path $evidence 'install.txt')
    Invoke-Adb -Arguments @('shell', 'am', 'start', '-n', "$package/foundation.icosa.obds.ProbeActivity") |
        Out-File (Join-Path $evidence 'launch.txt')
}
else {
    if (!$installed) { throw 'Probe is not installed. Inspect the APK manifest before installing.' }
    Invoke-Adb -Arguments @('pull', "/sdcard/Android/data/$package/files/obds-results.txt",
        (Join-Path $evidence 'obds-results.txt')) | Out-File (Join-Path $evidence 'pull.txt')
}
Invoke-Adb -Arguments @('shell', 'dumpsys', 'package', $package) |
    Out-File (Join-Path $evidence 'package-state.txt')
$installedApk = Invoke-Adb -Arguments @('shell', 'pm', 'path', $package) |
    Where-Object { $_ -match '^package:.*\/base\.apk$' } | Select-Object -First 1
if (!$installedApk) { throw 'Could not identify the installed base APK for evidence hashing.' }
Invoke-Adb -Arguments @('shell', 'sha256sum', $installedApk.Substring(8)) |
    Out-File (Join-Path $evidence 'installed-apk-sha256.txt')
Write-Output "OBDS_COLLECT evidence=$evidence"
