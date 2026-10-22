param(
    [string]$Version = '',
    [string]$ReleaseDirectory = '',
    [switch]$SkipBuild,
    [switch]$Draft
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseRoot = if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    Join-Path $repoRoot 'artifacts\release'
} else { [IO.Path]::GetFullPath($ReleaseDirectory) }

if ([string]::IsNullOrWhiteSpace($Version)) {
    $versionNode = Select-Xml -Path (Join-Path $repoRoot 'Directory.Build.props') `
        -XPath '/Project/PropertyGroup/Version' | Select-Object -First 1
    $Version = $versionNode.Node.InnerText.Trim()
}

if (-not $SkipBuild) {
    pwsh -NoProfile -File (Join-Path $repoRoot 'scripts\build-release.ps1') -Version $Version -ReleaseDirectory $releaseRoot
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
}

$installer = Join-Path $releaseRoot "MalodyVThemeLoader-Setup-$Version.exe"
$readme = Join-Path $releaseRoot 'README.txt'
$checksums = Join-Path $releaseRoot 'SHA256SUMS.txt'
foreach ($path in @($installer, $readme, $checksums)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Release asset is missing: $path"
    }
}

$tag = "v$Version"
$arguments = @('release', 'create', $tag, $installer, $readme, $checksums,
    '--repo', 'MCPsDE/MalodyVThemeLoader',
    '--title', "Malody V ThemeLoader $Version",
    '--notes', "ThemeLoader $Version. The installer updates existing installs and preserves compatible BepInEx ownership.")
if ($Draft) { $arguments += '--draft' }
& gh @arguments
if ($LASTEXITCODE -ne 0) { throw "GitHub release creation failed: $tag" }
