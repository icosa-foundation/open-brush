# Run from the project root with this project's Unity Editor closed:
# .\Support\windows\build-local.ps1
$ErrorActionPreference = 'Stop'
$builds = @(
    @{ Name='Windows OpenXR'; Target='StandaloneWindows64'; Sdk='OpenXR' }
    @{ Name='Android OpenXR'; Target='Android'; Sdk='OpenXR'; Api=35; Bundle=$false }
    @{ Name='Android AndroidXR'; Target='Android'; Sdk='AndroidXR'; Api=35; Bundle=$true }
    @{ Name='Android Viewer OpenXR'; Target='Android'; Sdk='OpenXR'; Api=35; Bundle=$false; Define='OPEN_BRUSH_VIEWER' }
    @{ Name='Android Viewer AndroidXR'; Target='Android'; Sdk='AndroidXR'; Api=35; Bundle=$true; Define='OPEN_BRUSH_VIEWER' }
    @{ Name='Android Viewer Handheld'; Target='Android'; Sdk='Monoscopic'; Api=35; Bundle=$false; Define='OPEN_BRUSH_VIEWER' }
    @{ Name='Android Meta Quest'; Target='Android'; Sdk='OpenXR'; Api=34; Bundle=$false; Define='USE_QUEST_PACKAGE_NAME' }
)
for ($i=0; $i -lt $builds.Count; $i++) { Write-Host "$($i+1). $($builds[$i].Name)" }
$choice = Read-Host "Select build (1-$($builds.Count))"
if ($choice -notmatch '^\d+$' -or [int]$choice -lt 1 -or [int]$choice -gt $builds.Count) {
    throw "Choose a number from 1 to $($builds.Count)."
}
$selected = $builds[[int]$choice-1]
$development = (Read-Host 'Development build? [y/N]') -match '^y(es)?$'

$project = (Get-Location).Path
$version = (Select-String '^m_EditorVersion: ' ProjectSettings/ProjectVersion.txt).Line -replace '^m_EditorVersion: ', ''
$installation = unity editors path $version --format json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Could not locate the Unity Editor.' }
$editor = Join-Path $installation.data.path 'Editor/Unity.exe'
$java = Join-Path $installation.data.path 'Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe'
$name = $selected.Name -replace ' ', '-'
$output = Join-Path $project "Builds/$name"
New-Item -ItemType Directory -Force -Path $output, 'Logs' | Out-Null
$extension = if ($selected.Target -ne 'Android') { 'exe' } elseif ($selected.Bundle) { 'aab' } else { 'apk' }
$artifact = Join-Path $output "OpenBrush.$extension"
$log = Join-Path $project "Logs/Local-$name.log"
$arguments = @(
    '-batchmode', '-quit', '-projectPath', "`"$project`"", '-buildTarget', $selected.Target,
    '-logFile', "`"$log`"", '-executeMethod', 'BuildTiltBrush.CommandLine',
    '-btb-target', $selected.Target, '-btb-display', $selected.Sdk, '-btb-out', "`"$artifact`""
)
if ($selected.Target -eq 'Android') {
    $export = if ($selected.Bundle) { 'androidAppBundle' } else { 'androidPackage' }
    $arguments += @('-btb-il2cpp', '-btb-android-target-sdk', $selected.Api, '-androidExportType', $export)
}
if ($development) { $arguments += @('-btb-bopt', 'Development') }

# CI selects Viewer/Quest through csc.rsp; restore the original file after building.
$responseFile = Join-Path $project 'Assets/csc.rsp'
$original = [IO.File]::ReadAllBytes($responseFile)
try {
    if ($selected.Define) { [IO.File]::AppendAllText($responseFile, "`n-define:$($selected.Define)`n") }
    Write-Host "Building $($selected.Name). Log: $log"
    $build = Start-Process $editor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($build.ExitCode -ne 0) { throw "Unity build failed. See $log" }
}
finally { [IO.File]::WriteAllBytes($responseFile, $original) }

if ($selected.Bundle) {
    # AndroidXR AABs enable SAF storage; convert them to APKs for local installation.
    $bundletool = Join-Path $project 'Builds/Tools/bundletool-all-1.18.3.jar'
    New-Item -ItemType Directory -Force -Path (Split-Path $bundletool) | Out-Null
    if (-not (Test-Path -LiteralPath $bundletool)) {
        Invoke-WebRequest 'https://github.com/google/bundletool/releases/download/1.18.3/bundletool-all-1.18.3.jar' -OutFile $bundletool
    }
    $apkSet = Join-Path $output 'OpenBrush.apks'
    $artifact = Join-Path $output 'OpenBrush.apk'
    & $java -jar $bundletool build-apks "--bundle=$(Join-Path $output 'OpenBrush.aab')" "--output=$apkSet" --mode=universal --overwrite
    if ($LASTEXITCODE -ne 0) { throw 'bundletool conversion failed.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($apkSet)
    try { [System.IO.Compression.ZipFileExtensions]::ExtractToFile($archive.GetEntry('universal.apk'), $artifact, $true) }
    finally { $archive.Dispose() }
}
Write-Host "Build ready: $artifact"
