param(
    [Parameter(Mandatory = $true)][string]$Label,
    [string]$Output
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Output) { $Output = Join-Path $projectPath "Temp/BrushMeshBenchmarks/$Label.json" }
$status = unity command editor_status --project-path $projectPath --json | ConvertFrom-Json
if (-not $status.success -or $status.data.result.compiling -or $status.data.result.playMode -ne 'stopped') {
    throw 'Benchmark requires a ready Editor outside Play mode.'
}
$arguments = ConvertTo-Json -InputObject @($Label, $Output) -Compress
$response = unity command run_script --file "$PSScriptRoot/BrushTriangleIdsBenchmark.cs" --entry TiltBrush.Benchmarks.BrushTriangleIdsBenchmark.Run --args $arguments --timeout_ms 120000 --timeout 130 --project-path $projectPath --json
$responsePath = [System.IO.Path]::ChangeExtension($Output, '.command.json')
New-Item -ItemType Directory -Path (Split-Path $Output -Parent) -Force | Out-Null
$response | Set-Content -LiteralPath $responsePath -Encoding utf8
$result = $response | ConvertFrom-Json
if (-not $result.success -or -not $result.data.result.success) { throw ($result | ConvertTo-Json -Depth 8 -Compress) }
Write-Output "Raw measurements: $Output"
if (Test-Path -LiteralPath $Output) {
    $data = Get-Content -LiteralPath $Output -Raw | ConvertFrom-Json
    if ($data.cases.Count -ne 5) { throw 'Incomplete benchmark output.' }; foreach ($case in $data.cases) {
        $old = @($case.legacy.milliseconds | Sort-Object)
        $current = @($case.current.milliseconds | Sort-Object)
        '{0}: old {1:F4} ms; current {2:F4} ms; allocation {3}/{4} B; incorrect IDs {5}/{6}' -f $case.name, $old[15], $current[15], $case.legacyAllocatedBytes, $case.currentAllocatedBytes, $case.legacyIncorrectIds, $case.currentIncorrectIds
    }
} else { throw 'Unity returned without writing measurements; inspect the command response.' }

