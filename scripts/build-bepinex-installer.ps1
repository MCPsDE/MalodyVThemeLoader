param(
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$payloadRoot = Join-Path $artifactsRoot 'malody-bepinex-payload'
$payloadZip = Join-Path $artifactsRoot 'malody-bepinex-payload.zip'
$buildRoot = Join-Path $artifactsRoot 'malody-bepinex-installer-build'
$releaseRoot = Join-Path $artifactsRoot 'release-bepinex'
$vendorRoot = Join-Path $repoRoot 'vendor\bepinex'
$interopHost = Join-Path $artifactsRoot 'interop-host'
$unityLibsSource = Join-Path $interopHost 'BepInEx\unity-libs'
$unityLibArchive = Join-Path $unityLibsSource '2022.3.62.zip'
$downloadsRoot = Join-Path $artifactsRoot 'downloads'
$configurationManagerZip = Join-Path `
    $downloadsRoot 'BepInEx.ConfigurationManager_IL2CPP_v19.0.zip'
$configurationManagerUrl =
    'https://github.com/BepInEx/BepInEx.ConfigurationManager/releases/download/v19.0/BepInEx.ConfigurationManager_IL2CPP_v19.0.zip'
$configurationManagerSha256 =
    '3ea26a2c354a1d960beca4c61a397c72a1641cb5872f6f89e7d972e15d8cfb29'

function Reset-ArtifactDirectory([string]$Path) {
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

if (-not (Test-Path (Join-Path $vendorRoot 'winhttp.dll'))) {
    throw 'vendor\bepinex is missing. Run scripts\prepare-dependencies.ps1 first.'
}
if (-not (Test-Path $unityLibArchive)) {
    $unityError =
        'Unity base library archive is missing: ' + $unityLibArchive + '. ' +
        'Run scripts\prepare-dependencies.ps1 after fixing the BepInEx interop environment.'
    throw $unityError
}
New-Item -ItemType Directory -Force -Path $downloadsRoot | Out-Null
if (-not (Test-Path $configurationManagerZip) -or
    (Get-FileHash $configurationManagerZip -Algorithm SHA256).Hash.ToLowerInvariant() -ne
    $configurationManagerSha256) {
    Invoke-WebRequest -Uri $configurationManagerUrl -OutFile $configurationManagerZip
}
if ((Get-FileHash $configurationManagerZip -Algorithm SHA256).Hash.ToLowerInvariant() -ne
    $configurationManagerSha256) {
    throw 'ConfigurationManager archive hash mismatch.'
}

Reset-ArtifactDirectory $payloadRoot
Reset-ArtifactDirectory $buildRoot
Reset-ArtifactDirectory $releaseRoot
if (Test-Path $payloadZip) {
    Remove-Item -LiteralPath $payloadZip -Force
}

Copy-Item -Path (Join-Path $vendorRoot '*') `
    -Destination $payloadRoot -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $payloadRoot 'BepInEx\unity-libs') | Out-Null
Copy-Item -LiteralPath $unityLibArchive `
    -Destination (Join-Path $payloadRoot 'BepInEx\unity-libs\2022.3.62.zip') -Force
Remove-Item -LiteralPath (Join-Path $payloadRoot 'changelog.txt') `
    -Force -ErrorAction SilentlyContinue

$doorstopConfig = Join-Path $payloadRoot 'doorstop_config.ini'
(Get-Content -LiteralPath $doorstopConfig -Raw).Replace(
    'ignore_disable_switch = false',
    'ignore_disable_switch = true') |
    Set-Content -LiteralPath $doorstopConfig -Encoding ASCII

$configTarget = Join-Path $payloadRoot 'BepInEx\config\BepInEx.cfg'
New-Item -ItemType Directory -Force -Path (Split-Path $configTarget) | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\BepInEx.cfg') `
    -Destination $configTarget -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $payloadRoot,
    $payloadZip,
    [IO.Compression.CompressionLevel]::Optimal,
    $false)

dotnet publish `
    (Join-Path $repoRoot 'src\MalodyVBepInExInstaller\MalodyVBepInExInstaller.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --artifacts-path $buildRoot `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -p:InformationalVersion=$Version `
    -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) {
    throw 'Malody V BepInEx installer publish failed.'
}

$publishRoot = Join-Path $buildRoot `
    'publish\MalodyVBepInExInstaller\release_win-x64'
$source = Join-Path $publishRoot 'MalodyVBepInExInstaller.exe'
$target = Join-Path $releaseRoot "MalodyV-BepInEx-Installer-$Version.exe"
Copy-Item -LiteralPath $source -Destination $target -Force

$hash = Get-FileHash -LiteralPath $target -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($target))" |
    Set-Content -LiteralPath (Join-Path $releaseRoot `
        "SHA256SUMS-BepInEx-$Version.txt") -Encoding ASCII

Get-Item $target | Select-Object FullName, Length, LastWriteTime
