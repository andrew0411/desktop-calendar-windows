param(
    [string]$OutputDirectory = "artifacts\win-x64"
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$projectRoot = $PSScriptRoot
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))

dotnet test (Join-Path $projectRoot "DesktopCalendar.sln") --configuration Release
if ($LASTEXITCODE -ne 0) { throw "테스트에 실패했습니다." }

dotnet publish (Join-Path $projectRoot "src\DesktopCalendar.App\DesktopCalendar.App.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $outputPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "릴리스 게시에 실패했습니다." }

Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination $outputPath -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "THIRD-PARTY-NOTICES.md") -Destination $outputPath -Force

Write-Host "릴리스가 생성되었습니다: $outputPath"
