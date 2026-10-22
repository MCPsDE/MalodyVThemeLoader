param(
    [string]$GameRoot = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV'
)

$ErrorActionPreference = 'Stop'

$bepInExUrl =
    'https://builds.bepinex.dev/projects/bepinex_be/785/' +
    'BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785%2B6abdba4.zip'
$bepInExSha256 =
    '2a7cbf74d26abe4765c3e662db1721b923bac39849ebfef2ca5dc7de7e2d9b7f'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$downloadsRoot = Join-Path $artifactsRoot 'downloads'
$archivePath = Join-Path $downloadsRoot 'BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785.zip'
$vendorRoot = Join-Path $repoRoot 'vendor\bepinex'
$interopHost = Join-Path $artifactsRoot 'interop-host'
$generatedLibs = Join-Path $repoRoot 'generated\game-libs'

function Assert-WorkspacePath([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $allowed = $repoRoot.TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the workspace: $resolved"
    }
}

if (-not (Test-Path (Join-Path $GameRoot 'Malody V.exe')) -or
    -not (Test-Path (Join-Path $GameRoot 'GameAssembly.dll')) -or
    -not (Test-Path (Join-Path $GameRoot 'Malody V_Data\il2cpp_data'))) {
    throw "Invalid Malody V IL2CPP game directory: $GameRoot"
}

New-Item -ItemType Directory -Force -Path $downloadsRoot | Out-Null
if (-not (Test-Path $archivePath) -or
    (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne
    $bepInExSha256) {
    curl.exe -fL --path-as-is $bepInExUrl -o $archivePath
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to download BepInEx.'
    }
}

$actualHash = (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $bepInExSha256) {
    throw "BepInEx archive hash mismatch: $actualHash"
}

foreach ($directory in @($vendorRoot, $interopHost, $generatedLibs)) {
    Assert-WorkspacePath $directory
    if (Test-Path $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $directory | Out-Null
}

Expand-Archive -LiteralPath $archivePath -DestinationPath $vendorRoot

$sameVolume =
    [IO.Path]::GetPathRoot($interopHost).Equals(
        [IO.Path]::GetPathRoot($GameRoot),
        [StringComparison]::OrdinalIgnoreCase)
foreach ($name in @('Malody V.exe', 'GameAssembly.dll', 'UnityPlayer.dll', 'baselib.dll')) {
    $source = Join-Path $GameRoot $name
    $target = Join-Path $interopHost $name
    if ($sameVolume) {
        New-Item -ItemType HardLink -Path $target -Target $source | Out-Null
    } else {
        Copy-Item -LiteralPath $source -Destination $target
    }
}

New-Item -ItemType Junction `
    -Path (Join-Path $interopHost 'Malody V_Data') `
    -Target (Join-Path $GameRoot 'Malody V_Data') | Out-Null
Copy-Item -Path (Join-Path $vendorRoot '*') `
    -Destination $interopHost -Recurse -Force

$unityLog = Join-Path $interopHost 'unity-interop-generation.log'
$bepInExLog = Join-Path $interopHost 'BepInEx\LogOutput.log'
$targetInterop = Join-Path $interopHost 'BepInEx\interop\Assembly-CSharp.dll'
$launcher = Start-Process `
    -FilePath (Join-Path $interopHost 'Malody V.exe') `
    -WorkingDirectory $interopHost `
    -ArgumentList @('-batchmode', '-nographics', '-logFile', $unityLog) `
    -WindowStyle Hidden `
    -PassThru

$deadline = (Get-Date).AddMinutes(8)
while ((Get-Date) -lt $deadline -and -not (Test-Path $targetInterop)) {
    Start-Sleep -Seconds 2
}
Start-Sleep -Seconds 3

$relatedProcesses = Get-CimInstance Win32_Process |
    Where-Object { $_.CommandLine -like "*$unityLog*" }
foreach ($process in $relatedProcesses) {
    Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
}
if (-not $launcher.HasExited) {
    Stop-Process -Id $launcher.Id -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path $targetInterop)) {
    Get-Content $bepInExLog -Tail 100 -ErrorAction SilentlyContinue
    throw 'BepInEx did not generate IL2CPP interop assemblies.'
}

$interopRoot = Join-Path $interopHost 'BepInEx\interop'
$compileLibraries = @(
    'Assembly-CSharp.dll',
    'Framework.dll',
    'Il2Cppmscorlib.dll',
    'Il2CppSystem.dll',
    'Il2CppSystem.Core.dll',
    'Live2D.Cubism.dll',
    'Live2DUnity.dll',
    'Unity.TextMeshPro.dll',
    'UnityEngine.dll',
    'UnityEngine.AssetBundleModule.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.ImageConversionModule.dll',
    'UnityEngine.ScreenCaptureModule.dll',
    'UnityEngine.AnimationModule.dll',
    'UnityEngine.TextCoreFontEngineModule.dll',
    'UnityEngine.TextCoreTextEngineModule.dll',
    'UnityEngine.TextRenderingModule.dll',
    'UnityEngine.UI.dll',
    'UnityEngine.UIModule.dll',
    'UnityEngine.VideoModule.dll'
)
foreach ($name in $compileLibraries) {
    Copy-Item -LiteralPath (Join-Path $interopRoot $name) `
        -Destination (Join-Path $generatedLibs $name)
}

Write-Output "Prepared BepInEx and $($compileLibraries.Count) compile-time interop libraries."
