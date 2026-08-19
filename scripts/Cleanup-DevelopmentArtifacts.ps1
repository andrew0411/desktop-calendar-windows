[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$IncludeBuildCaches
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$rootPrefix = $projectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$targets = [System.Collections.Generic.List[string]]::new()

function Add-ValidatedTarget([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup target is outside the project: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        $targets.Add($fullPath)
    }
}

foreach ($relativePath in @(
    "artifacts\ui-review",
    "artifacts\screenshots",
    "artifacts\tmp",
    "TestResults"
)) {
    Add-ValidatedTarget (Join-Path $projectRoot $relativePath)
}

if ($IncludeBuildCaches) {
    foreach ($sourceRootName in @("src", "tests")) {
        $sourceRoot = Join-Path $projectRoot $sourceRootName
        if (-not (Test-Path -LiteralPath $sourceRoot)) {
            continue
        }
        Get-ChildItem -LiteralPath $sourceRoot -Directory -Recurse -Force |
            Where-Object { $_.Name -in @("bin", "obj") } |
            ForEach-Object { Add-ValidatedTarget $_.FullName }
    }
}

$removed = 0
foreach ($target in $targets | Sort-Object -Unique) {
    if ($PSCmdlet.ShouldProcess($target, "Remove development artifact")) {
        Remove-Item -LiteralPath $target -Recurse -Force
        $removed++
    }
}

Write-Output "Development artifact cleanup complete. Removed targets: $removed"

