param(
    [string]$Editor = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe',
    [string]$PackagingSdk,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$probe = Join-Path $repo 'LocalPlanning/AndroidDirectStorage/UnityProbe'
if (Test-Path -LiteralPath (Join-Path $probe 'Temp/UnityLockfile')) {
    throw 'Probe project has a Unity lock. Inspect process ownership before retrying.'
}
New-Item -ItemType Directory -Force $probe | Out-Null
foreach ($name in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $probe -Recurse -Force
}
# Exercise the production manifest policy without importing the full Open Brush project.
Copy-Item -LiteralPath (Join-Path $repo 'Assets/Editor/AndroidStoreManifest.cs') -Destination (
    Join-Path $probe 'Assets/Editor/AndroidStoreManifest.cs'
) -Force
Copy-Item -LiteralPath (Join-Path $repo 'Assets/Scripts/Util/AndroidDirectStorage.cs') -Destination (
    Join-Path $probe 'Assets/AndroidDirectStorage.cs'
) -Force
Copy-Item -LiteralPath (Join-Path $repo 'Assets/Plugins/Android/DirectStorage.java') -Destination (
    Join-Path $probe 'Assets/Plugins/Android/DirectStorage.java'
) -Force
foreach ($source in @('Save/TiltFile.cs', 'ZipSubfileReader.cs', 'ZipOutputStreamWrapper.cs', 'WrappedStream.cs')) {
    Copy-Item -LiteralPath (Join-Path $repo "Assets/Scripts/$source") -Destination (
        Join-Path $probe "Assets/$([IO.Path]::GetFileName($source))"
    ) -Force
}
Copy-Item -LiteralPath (Join-Path $repo 'Assets/ThirdParty/Ionic.Zip/Ionic.Zip.Unity.dll') -Destination (
    Join-Path $probe 'Assets/Plugins/Ionic.Zip.Unity.dll'
) -Force
if ($PrepareOnly) { Write-Output "Prepared isolated project: $probe"; return }
if (!(Test-Path -LiteralPath $Editor)) { throw "Editor not found: $Editor" }
$log = Join-Path $probe 'build.log'
$policyStamp = Join-Path $probe 'Build/manifest-policy.txt'
if (Test-Path -LiteralPath $policyStamp) { Remove-Item -LiteralPath $policyStamp }
# Run via host PowerShell with require_escalated. Never reuse or close another Editor.
$process = Start-Process -FilePath $Editor -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-quit', '-buildTarget', 'Android',
    '-projectPath', "`"$probe`"", '-executeMethod', 'BuildProbe.Build', '-logFile', "`"$log`""
)
Write-Output "OBDS_BUILD pid=$($process.Id) log=$log"
$process.WaitForExit()
if (!(Test-Path -LiteralPath $policyStamp)) {
    throw "Probe manifest policy did not complete. Inspect $log before packaging."
}
if ($process.ExitCode -ne 0) {
    if (!$PackagingSdk) {
        $localSdk = Join-Path $repo 'LocalPlanning/AndroidDirectStorage/AndroidSdk'
        if (Test-Path -LiteralPath (Join-Path $localSdk 'platforms/android-35/android.jar')) {
            $PackagingSdk = $localSdk
        }
    }
    # Only retry packaging after Unity actually reached Gradle. Never package stale
    # generated sources after a compilation failure or a different Editor failure.
    $gradleFailure = Select-String -LiteralPath $log -SimpleMatch 'CommandInvokationFailure: Gradle build failed.' -Quiet
    if ($PackagingSdk -and $gradleFailure) {
        $androidPlayer = Join-Path (Split-Path $Editor) 'Data/PlaybackEngines/AndroidPlayer'
        & (Join-Path $PSScriptRoot 'package.ps1') -SdkRoot $PackagingSdk -AndroidPlayer $androidPlayer
        return
    }
    throw "Probe build exited $($process.ExitCode). Inspect $log"
}
Write-Output "OBDS_BUILD artifact=$(Join-Path $probe 'Build/obds-unity-probe.apk')"
