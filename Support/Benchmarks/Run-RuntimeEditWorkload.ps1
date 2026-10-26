param([Parameter(Mandatory=$true)][string]$Label,[string]$Output,[string]$FixturesFile)
$ErrorActionPreference='Stop'
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $Output){$Output=Join-Path $project "Logs/BrushMeshBenchmarks/$Label.json"}
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path (Split-Path $Output -Parent) -Force | Out-Null
$entry='TiltBrush.Benchmarks.RuntimeEditWorkload.Run'
$values=@($Label,$Output)
if($FixturesFile){$entry='TiltBrush.Benchmarks.RuntimeEditWorkload.RunWithFixtures';$values+=([IO.Path]::GetFullPath($FixturesFile))}
$arguments=ConvertTo-Json -InputObject $values -Compress
$response=unity command run_script --file "$PSScriptRoot/RuntimeEditWorkload.cs" --entry $entry --args $arguments --project-path $project --json
$response | Set-Content -LiteralPath ([IO.Path]::ChangeExtension($Output,'.command.json')) -Encoding utf8
$r=$response | ConvertFrom-Json
if(-not $r.success -or -not $r.data.result.success){throw ($r.data.result | ConvertTo-Json -Depth 4 -Compress)}
Write-Output $r.data.result.result
Write-Output 'The app runs the workload asynchronously. Inspect status, error and phases in the output JSON; completed means all fixture phases passed.'
