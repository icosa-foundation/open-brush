param(
    [Parameter(Mandatory = $true)][string]$Label,
    [string]$Output
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Output) { $Output = Join-Path $projectPath "Logs/BrushMeshBenchmarks/$Label.json" }
$status = unity command editor_status --project-path $projectPath --json | ConvertFrom-Json
if (-not $status.success -or $status.data.result.compiling -or $status.data.result.playMode -ne 'stopped') {
    throw 'Benchmark requires a ready Editor outside Play mode.'
}
$arguments = ConvertTo-Json -InputObject @($Label, $Output) -Compress
$response = unity command run_script --file "$PSScriptRoot/BatchVisibilityBenchmark.cs" --entry TiltBrush.Benchmarks.BatchVisibilityBenchmark.Run --args $arguments --timeout_ms 120000 --timeout 130 --project-path $projectPath --json
New-Item -ItemType Directory -Path (Split-Path $Output -Parent) -Force | Out-Null
$response | Set-Content -LiteralPath ([System.IO.Path]::ChangeExtension($Output, '.command.json')) -Encoding utf8
$result = $response | ConvertFrom-Json
if (-not $result.success -or -not $result.data.result.success) { throw ($result | ConvertTo-Json -Depth 8 -Compress) }
$data = Get-Content -LiteralPath $Output -Raw | ConvertFrom-Json
if ($data.cases.Count -ne 7) { throw 'Incomplete benchmark output.' }
foreach ($case in $data.cases) {
    $times = @($case.milliseconds | Sort-Object)
    '{0}: {1:F4} ms/pair; allocation {2} B; errors {3}/{4}; bounds preserved={5}' -f $case.name, $times[20], ($case.allocatedBytes | Measure-Object -Minimum).Minimum, $case.hiddenErrors, $case.restoredErrors, $case.paddedBoundsPreserved
}
Write-Output "Raw measurements: $Output"

