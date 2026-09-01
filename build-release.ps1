[CmdletBinding()]
param(
    [string]$ArtifactsDirectory = "artifacts",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$projectRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$projectRootPrefix = $projectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$solutionPath = Join-Path $projectRoot "DesktopCalendar.sln"
$appProjectPath = Join-Path $projectRoot "src\DesktopCalendar.App\DesktopCalendar.App.csproj"

function Get-ValidatedProjectPath([string]$Path) {
    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        $Path
    }
    else {
        Join-Path $projectRoot $Path
    }
    $fullPath = [System.IO.Path]::GetFullPath($candidate)
    if ($fullPath -eq $projectRoot -or
        -not $fullPath.StartsWith($projectRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "릴리스 출력 경로는 프로젝트 내부의 하위 디렉터리여야 합니다: $fullPath"
    }
    return $fullPath
}

function Invoke-DotNet([string[]]$Arguments, [string]$FailureMessage) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

function Copy-ReleaseNotices([string]$Destination) {
    foreach ($fileName in @("LICENSE", "THIRD-PARTY-NOTICES.md")) {
        $source = Join-Path $projectRoot $fileName
        $copied = Join-Path $Destination $fileName
        Copy-Item -LiteralPath $source -Destination $copied -Force
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $copiedHash = (Get-FileHash -LiteralPath $copied -Algorithm SHA256).Hash
        if ($sourceHash -ne $copiedHash) {
            throw "법적 고지 사본이 원본과 일치하지 않습니다: $fileName"
        }
    }
}

function Test-ExecutableVersion(
    [string]$ExecutablePath,
    [string]$ExpectedProductVersion,
    [string]$ExpectedFileVersion
) {
    if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        throw "게시된 실행 파일을 찾을 수 없습니다: $ExecutablePath"
    }
    $versionInfo = (Get-Item -LiteralPath $ExecutablePath).VersionInfo
    if ($versionInfo.ProductVersion -ne $ExpectedProductVersion) {
        throw "제품 버전이 일치하지 않습니다. 예상: $ExpectedProductVersion, 실제: $($versionInfo.ProductVersion)"
    }
    if ($versionInfo.FileVersion -ne $ExpectedFileVersion) {
        throw "파일 버전이 일치하지 않습니다. 예상: $ExpectedFileVersion, 실제: $($versionInfo.FileVersion)"
    }
}

function New-ReleaseArchive([string]$SourceDirectory, [string]$DestinationPath) {
    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $SourceDirectory,
        $DestinationPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($DestinationPath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object FullName)
        foreach ($requiredEntry in @("DesktopCalendar.exe", "LICENSE", "THIRD-PARTY-NOTICES.md")) {
            if ($requiredEntry -notin $entryNames) {
                throw "ZIP 패키지에 필수 파일이 없습니다: $requiredEntry"
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$allowedArtifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts"))
$allowedArtifactsPrefix = $allowedArtifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$artifactRoot = Get-ValidatedProjectPath $ArtifactsDirectory
if ($artifactRoot -ne $allowedArtifactsRoot -and
    -not $artifactRoot.StartsWith($allowedArtifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "릴리스 출력 경로는 artifacts 디렉터리 내부여야 합니다: $artifactRoot"
}
if ($RuntimeIdentifier -notmatch '^[0-9A-Za-z._-]+$') {
    throw "유효하지 않은 런타임 식별자입니다: $RuntimeIdentifier"
}
$portableOutput = Get-ValidatedProjectPath (Join-Path $artifactRoot $RuntimeIdentifier)
$slimOutput = Get-ValidatedProjectPath (Join-Path $artifactRoot "$RuntimeIdentifier-slim")
$packagesOutput = Get-ValidatedProjectPath (Join-Path $artifactRoot "packages")
$targetExecutable = Join-Path $portableOutput "DesktopCalendar.exe"

$runningFromTarget = Get-Process -Name "DesktopCalendar" -ErrorAction SilentlyContinue |
    Where-Object {
        try {
            [System.IO.Path]::GetFullPath($_.Path) -eq $targetExecutable
        }
        catch {
            $false
        }
    }
if ($runningFromTarget) {
    throw "현재 릴리스 실행 파일이 사용 중입니다. DesktopCalendar를 종료한 뒤 다시 실행하세요: $targetExecutable"
}

$versionOutput = & dotnet msbuild $appProjectPath `
    -getProperty:Version `
    -getProperty:VersionPrefix `
    -getProperty:AssemblyVersion `
    -getProperty:FileVersion `
    -getProperty:InformationalVersion
if ($LASTEXITCODE -ne 0) {
    throw "프로젝트 버전을 읽지 못했습니다."
}
$versionProperties = (($versionOutput -join [Environment]::NewLine) | ConvertFrom-Json).Properties
$releaseVersion = [string]$versionProperties.Version
$versionPrefix = [string]$versionProperties.VersionPrefix
$expectedBinaryVersion = "$versionPrefix.0"

if ($releaseVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "지원하지 않는 릴리스 버전 형식입니다: $releaseVersion"
}
if ($versionProperties.AssemblyVersion -ne $expectedBinaryVersion -or
    $versionProperties.FileVersion -ne $expectedBinaryVersion -or
    $versionProperties.InformationalVersion -ne $releaseVersion) {
    throw "프로젝트 버전 속성이 docs/VERSIONING.md 정책과 일치하지 않습니다."
}

if (-not $SkipTests) {
    Invoke-DotNet -Arguments @(
        "test", $solutionPath,
        "--configuration", "Release",
        "--logger", "console;verbosity=minimal"
    ) -FailureMessage "테스트에 실패했습니다."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$stagingRoot = Join-Path $artifactRoot ".release-staging-$PID"
$stagingPortable = Join-Path $stagingRoot $RuntimeIdentifier
$stagingSlim = Join-Path $stagingRoot "$RuntimeIdentifier-slim"
$stagingPackages = Join-Path $stagingRoot "packages"

try {
    New-Item -ItemType Directory -Path $stagingPortable, $stagingSlim, $stagingPackages -Force | Out-Null

    Invoke-DotNet -Arguments @(
        "publish", $appProjectPath,
        "--configuration", "Release",
        "--runtime", $RuntimeIdentifier,
        "--self-contained", "true",
        "--output", $stagingPortable,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    ) -FailureMessage "portable 릴리스 게시에 실패했습니다."

    Invoke-DotNet -Arguments @(
        "publish", $appProjectPath,
        "--configuration", "Release",
        "--runtime", $RuntimeIdentifier,
        "--self-contained", "false",
        "--output", $stagingSlim,
        "-p:PublishSingleFile=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    ) -FailureMessage "slim 릴리스 게시에 실패했습니다."

    Copy-ReleaseNotices $stagingPortable
    Copy-ReleaseNotices $stagingSlim
    Test-ExecutableVersion (Join-Path $stagingPortable "DesktopCalendar.exe") $releaseVersion $expectedBinaryVersion
    Test-ExecutableVersion (Join-Path $stagingSlim "DesktopCalendar.exe") $releaseVersion $expectedBinaryVersion

    $portablePackageName = "DesktopCalendar-v$releaseVersion-$RuntimeIdentifier-portable.zip"
    $slimPackageName = "DesktopCalendar-v$releaseVersion-$RuntimeIdentifier-slim.zip"
    $stagingPortablePackage = Join-Path $stagingPackages $portablePackageName
    $stagingSlimPackage = Join-Path $stagingPackages $slimPackageName
    New-ReleaseArchive $stagingPortable $stagingPortablePackage
    New-ReleaseArchive $stagingSlim $stagingSlimPackage

    foreach ($outputDirectory in @($portableOutput, $slimOutput)) {
        if (Test-Path -LiteralPath $outputDirectory) {
            Remove-Item -LiteralPath $outputDirectory -Recurse -Force
        }
    }
    New-Item -ItemType Directory -Path $packagesOutput -Force | Out-Null
    Move-Item -LiteralPath $stagingPortable -Destination $portableOutput
    Move-Item -LiteralPath $stagingSlim -Destination $slimOutput

    foreach ($package in @($stagingPortablePackage, $stagingSlimPackage)) {
        $destination = Join-Path $packagesOutput (Split-Path $package -Leaf)
        if (Test-Path -LiteralPath $destination) {
            Remove-Item -LiteralPath $destination -Force
        }
        Move-Item -LiteralPath $package -Destination $destination
    }

    Write-Host "릴리스 생성 완료 (버전 $releaseVersion)"
    Write-Host "portable: $portableOutput"
    Write-Host "slim:     $slimOutput"
    Write-Host "packages: $packagesOutput"
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}
