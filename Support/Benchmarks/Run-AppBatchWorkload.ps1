param([Parameter(Mandatory=$true)][string]$Label,[string]$Output)
$ErrorActionPreference='Stop'
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $Output){$Output=Join-Path $project "Logs/BrushMeshBenchmarks/$Label.json"}
$arguments=ConvertTo-Json -InputObject @($Label,$Output) -Compress
$response=unity command run_script --file "$PSScriptRoot/AppBatchWorkload.cs" --entry TiltBrush.Benchmarks.AppBatchWorkload.Run --args $arguments --project-path $project --json
$response | Set-Content -LiteralPath ([IO.Path]::ChangeExtension($Output,'.command.json')) -Encoding utf8
$r=$response | ConvertFrom-Json
if(-not $r.success -or -not $r.data.result.success){throw ($r.data.result | ConvertTo-Json -Depth 4 -Compress)}
Write-Output $r.data.result.result
Write-Output 'The app runs the workload asynchronously. Inspect status, error and phases in the output JSON; completed means all fixture phases passed.'
