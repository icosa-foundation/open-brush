param(
    [Parameter(Mandatory)][string]$SdkRoot,
    [string]$AndroidPlayer = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$probe = Join-Path $repo 'LocalPlanning/AndroidDirectStorage/UnityProbe'
$gradle = Join-Path $probe 'Library/Bee/Android/Prj/IL2CPP/Gradle'
if (!(Test-Path -LiteralPath (Join-Path $SdkRoot 'platforms/android-35/android.jar'))) {
    throw 'The packaging SDK must include API 35.'
}
if (!(Test-Path -LiteralPath (Join-Path $gradle 'build.gradle'))) {
    throw 'Run build.ps1 first to compile IL2CPP and generate the Gradle project.'
}
Set-Content -LiteralPath (Join-Path $gradle 'local.properties') -Value "sdk.dir=$($SdkRoot.Replace('\','/'))"
$packageLog = Join-Path $probe 'package.log'
& (Join-Path $AndroidPlayer 'OpenJDK/bin/java.exe') -classpath (
    Join-Path $AndroidPlayer 'Tools/gradle/lib/gradle-launcher-9.1.0.jar'
) org.gradle.launcher.GradleMain -p $gradle '-Dorg.gradle.jvmargs=-Xmx4096m' assembleDebug *> $packageLog
if ($LASTEXITCODE -ne 0) { throw 'Probe packaging failed. Inspect the local package.log.' }
New-Item -ItemType Directory -Force (Join-Path $probe 'Build') | Out-Null
Copy-Item -LiteralPath (Join-Path $gradle 'launcher/build/outputs/apk/debug/launcher-debug.apk') -Destination (
    Join-Path $probe 'Build/obds-unity-probe.apk'
) -Force
Write-Output "OBDS_PACKAGE artifact=$(Join-Path $probe 'Build/obds-unity-probe.apk')"
