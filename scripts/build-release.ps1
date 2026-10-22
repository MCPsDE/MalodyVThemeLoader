param(
    [string]$SourceRoot = 'E:\UnityProjects\Emiria',
    [string]$InteropHost = '',
    [string]$Version = '',
    [string]$ReleaseDirectory = '',
    [string]$ValidationInteropRoot = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV\BepInEx\interop'
)

$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$payloadRoot = Join-Path $artifactsRoot 'payload'
$payloadZip = Join-Path $artifactsRoot 'payload.zip'
$releaseRoot = if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    Join-Path $artifactsRoot 'release'
} else { [IO.Path]::GetFullPath($ReleaseDirectory) }
$pluginBuildRoot = Join-Path $artifactsRoot 'plugin-build'
$themeBuilderBuildRoot = Join-Path $artifactsRoot 'themebuilder-build'
$managerBuildRoot = Join-Path $artifactsRoot 'manager-build'
$installerBuildRoot = Join-Path $artifactsRoot 'installer-build'
$vendorRoot = Join-Path $repoRoot 'vendor\bepinex'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $versionNode = Select-Xml `
        -Path (Join-Path $repoRoot 'Directory.Build.props') `
        -XPath '/Project/PropertyGroup/Version' |
        Select-Object -First 1
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.Node.InnerText)) {
        throw 'Product version was not found in Directory.Build.props.'
    }

    $Version = $versionNode.Node.InnerText.Trim()
}

if ([string]::IsNullOrWhiteSpace($InteropHost)) {
    $InteropHost = Join-Path $artifactsRoot 'interop-host'
}

function Reset-WorkspaceDirectory([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $allowed = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset path outside artifacts: $resolved"
    }

    if (Test-Path $resolved) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resolved | Out-Null
}

if (-not (Test-Path (Join-Path $SourceRoot 'Assets\Malody'))) {
    throw "Invalid source root: $SourceRoot"
}
if (-not (Test-Path (Join-Path $vendorRoot 'winhttp.dll'))) {
    throw 'BepInEx vendor payload is missing.'
}
if (-not (Test-Path (Join-Path $InteropHost 'BepInEx\interop\Assembly-CSharp.dll'))) {
    throw "Generated interop host is missing: $InteropHost"
}

Reset-WorkspaceDirectory $payloadRoot
# Keep older installers in the standard release directory; an existing window
# may still hold one open. Only the new version's artifacts are overwritten.
$releasePath = [IO.Path]::GetFullPath($releaseRoot)
$allowedRelease = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
if (-not $releasePath.StartsWith($allowedRelease,[StringComparison]::OrdinalIgnoreCase)) {
    throw "Release path must stay inside artifacts: $releasePath"
}
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
Reset-WorkspaceDirectory $pluginBuildRoot
Reset-WorkspaceDirectory $themeBuilderBuildRoot
Reset-WorkspaceDirectory $managerBuildRoot
Reset-WorkspaceDirectory $installerBuildRoot
if (Test-Path $payloadZip) {
    Remove-Item -LiteralPath $payloadZip -Force
}

dotnet build (Join-Path $repoRoot 'src\MalodyVThemeLoader.Plugin') `
    -c Release `
    --artifacts-path $pluginBuildRoot `
    "-p:GameInteropRoot=$ValidationInteropRoot" `
    -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }

# Compilation against a previously generated cache alone cannot catch private
# field renames after a game update. Check the actual target environment before
# building an offline installer that would distribute an incompatible plugin.
pwsh -NoProfile -File (Join-Path $repoRoot 'scripts\test-interop-compatibility.ps1') `
    -PluginPath (Join-Path $pluginBuildRoot 'bin\MalodyVThemeLoader.Plugin\release\MalodyVThemeLoader.Plugin.dll') `
    -InteropRoot $ValidationInteropRoot
if ($LASTEXITCODE -ne 0) { throw 'Plugin is incompatible with the target game interop cache.' }

Copy-Item -Path (Join-Path $vendorRoot '*') -Destination $payloadRoot -Recurse -Force
$doorstopConfig = Join-Path $payloadRoot 'doorstop_config.ini'
(Get-Content -LiteralPath $doorstopConfig -Raw).Replace(
    'ignore_disable_switch = false',
    'ignore_disable_switch = true') |
    Set-Content -LiteralPath $doorstopConfig -Encoding ASCII
Remove-Item -LiteralPath (Join-Path $payloadRoot 'changelog.txt') `
    -Force -ErrorAction SilentlyContinue
Get-ChildItem -LiteralPath (Join-Path $payloadRoot 'BepInEx\core') `
    -Filter '*.xml' -File |
    Remove-Item -Force

$payloadBepInEx = Join-Path $payloadRoot 'BepInEx'
$payloadInterop = Join-Path $payloadBepInEx 'interop'
New-Item -ItemType Directory -Path $payloadInterop -Force | Out-Null
Copy-Item -Path (Join-Path $ValidationInteropRoot '*') `
    -Destination $payloadInterop -Recurse -Force
Copy-Item -Path (Join-Path $InteropHost 'BepInEx\unity-libs') `
    -Destination $payloadBepInEx -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $payloadBepInEx 'config') | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\BepInEx.cfg') `
    -Destination (Join-Path $payloadBepInEx 'config\BepInEx.cfg') -Force

dotnet build (Join-Path $repoRoot 'src\MalodyVThemeLoader.ThemeBuilder') `
    -c Release `
    --artifacts-path $themeBuilderBuildRoot `
    -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'ThemeBuilder build failed.' }

$themeBuilderDll = Join-Path $themeBuilderBuildRoot `
    'bin\MalodyVThemeLoader.ThemeBuilder\release\MalodyVThemeLoader.ThemeBuilder.dll'
dotnet $themeBuilderDll `
    --source $SourceRoot `
    --output $payloadRoot `
    --version $Version
if ($LASTEXITCODE -ne 0) { throw 'Runtime catalog generation failed.' }

$pluginOutput = Join-Path $pluginBuildRoot `
    'bin\MalodyVThemeLoader.Plugin\release'
$pluginTarget = Join-Path $payloadBepInEx 'plugins\MalodyVThemeLoader'
New-Item -ItemType Directory -Force -Path $pluginTarget | Out-Null
Copy-Item -LiteralPath (Join-Path $pluginOutput 'MalodyVThemeLoader.Plugin.dll') `
    -Destination $pluginTarget -Force
Copy-Item -LiteralPath (Join-Path $pluginOutput 'MalodyVThemeLoader.Shared.dll') `
    -Destination $pluginTarget -Force
Copy-Item -LiteralPath (Join-Path $pluginOutput 'NVorbis.dll') `
    -Destination $pluginTarget -Force
Copy-Item -LiteralPath (Join-Path $pluginOutput 'NLayer.dll') `
    -Destination $pluginTarget -Force

$docsTarget = Join-Path $payloadRoot 'ThemeLoader\docs'
New-Item -ItemType Directory -Force -Path $docsTarget | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\README.txt') `
    -Destination $docsTarget -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\THIRD_PARTY_NOTICES.txt') `
    -Destination $docsTarget -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\licenses') `
    -Destination $docsTarget -Recurse -Force

dotnet publish (Join-Path $repoRoot 'src\MalodyVThemeLoader.ThemeManager') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --artifacts-path $managerBuildRoot `
    -p:Version=$Version `
    -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Theme manager publish failed.' }

$managerPublishRoot = Join-Path $managerBuildRoot `
    'publish\MalodyVThemeLoader.ThemeManager\release_win-x64'
Copy-Item -LiteralPath (Join-Path $managerPublishRoot `
        'MalodyVThemeLoader.ThemeManager.exe') `
    -Destination (Join-Path $payloadRoot `
        'ThemeLoader\MalodyVThemeManager.exe') `
    -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $payloadRoot,
    $payloadZip,
    [IO.Compression.CompressionLevel]::Optimal,
    $false)

dotnet publish (Join-Path $repoRoot 'src\MalodyVThemeLoader.Installer') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --artifacts-path $installerBuildRoot `
    -p:Version=$Version `
    -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }

$publishRoot = Join-Path $installerBuildRoot `
    'publish\MalodyVThemeLoader.Installer\release_win-x64'
$installerSource = Join-Path $publishRoot 'MalodyVThemeLoader.Installer.exe'
$installerTarget = Join-Path $releaseRoot "MalodyVThemeLoader-Setup-$Version.exe"
Copy-Item -LiteralPath $installerSource -Destination $installerTarget -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\README.txt') `
    -Destination $releaseRoot -Force
$releaseDefault = Join-Path $releaseRoot 'Default'
Reset-WorkspaceDirectory $releaseDefault
Get-ChildItem -LiteralPath (Join-Path $payloadRoot 'ThemeLoader\themes\Default') -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $releaseDefault -Recurse -Force
}

@($installerTarget,(Join-Path $releaseRoot 'README.txt')) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_))"
} |
    Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding ASCII

Get-ChildItem $releaseRoot | Select-Object Name, Length
