param(
    [string]$PayloadZip = '',
    [string]$InstallerDll = '',
    [string]$InstallerExe = '',
    [string]$TestRoot = '',
    [bool]$InstallConfigurationManager = $true
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repoRoot 'artifacts'
if ([string]::IsNullOrWhiteSpace($PayloadZip)) {
    $PayloadZip = Join-Path $artifactsRoot 'malody-bepinex-payload.zip'
}
if ([string]::IsNullOrWhiteSpace($InstallerDll)) {
    $InstallerDll = Join-Path $repoRoot `
        'src\MalodyVBepInExInstaller\bin\Release\net8.0-windows\win-x64\MalodyVBepInExInstaller.dll'
}
if (-not [string]::IsNullOrWhiteSpace($InstallerExe)) {
    $InstallerExe = [IO.Path]::GetFullPath($InstallerExe)
}

$testRoot = if ([string]::IsNullOrWhiteSpace($TestRoot)) {
    Join-Path $artifactsRoot ('bepinex-installer-test-' + [Guid]::NewGuid().ToString('N'))
} else {
    [IO.Path]::GetFullPath($TestRoot)
}
$testRoot = [IO.Path]::GetFullPath($testRoot)
$allowed = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
if (-not $testRoot.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe test path: $testRoot"
}
if (Test-Path $testRoot) {
    throw "Test directory already exists; refusing to delete it: $testRoot"
}

$gameRoot = Join-Path $testRoot 'MalodyV'
New-Item -ItemType Directory -Path `
    (Join-Path $gameRoot 'Malody V_Data\il2cpp_data') -Force | Out-Null
foreach ($name in @('Malody V.exe', 'UnityPlayer.dll', 'GameAssembly.dll')) {
    [IO.File]::WriteAllBytes(
        (Join-Path $gameRoot $name),
        [byte[]](1, 2, 3, 4))
}
$foreignRuntimePath = Join-Path $gameRoot 'BepInEx\foreign-runtime-marker.dll'
$foreignPluginPath = Join-Path $gameRoot 'BepInEx\plugins\foreign-plugin.dll'
New-Item -ItemType Directory -Path (Split-Path $foreignRuntimePath) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path $foreignPluginPath) -Force | Out-Null
[IO.File]::WriteAllBytes($foreignRuntimePath, [byte[]](4, 5, 6))
[IO.File]::WriteAllBytes($foreignPluginPath, [byte[]](7, 8, 9))
[IO.File]::WriteAllBytes((Join-Path $gameRoot 'winhttp.dll'), [byte[]](9, 8, 7))
[IO.File]::WriteAllText((Join-Path $gameRoot 'doorstop_config.ini'), 'foreign-doorstop')

if ([string]::IsNullOrWhiteSpace($InstallerExe)) {
    $env:MALODY_BEPINEX_PAYLOAD = $PayloadZip
}
else {
    Remove-Item Env:MALODY_BEPINEX_PAYLOAD -ErrorAction SilentlyContinue
}
$env:MALODY_BEPINEX_QUIET = '1'
if ($InstallConfigurationManager) {
    Remove-Item Env:MALODY_BEPINEX_INSTALL_CONFIGURATION_MANAGER `
        -ErrorAction SilentlyContinue
}
else {
    $env:MALODY_BEPINEX_INSTALL_CONFIGURATION_MANAGER = '0'
}
function Invoke-Installer([string]$Command) {
    if ([string]::IsNullOrWhiteSpace($InstallerExe)) {
        & dotnet $InstallerDll $Command $gameRoot | Out-Null
        return $LASTEXITCODE
    }

    $process = Start-Process `
        -FilePath $InstallerExe `
        -WorkingDirectory (Split-Path $InstallerExe) `
        -ArgumentList @($Command, $gameRoot) `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    return $process.ExitCode
}
try {
    $exitCode = Invoke-Installer '--install'
    if ($exitCode -ne 0) {
        throw 'BepInEx install failed.'
    }
    $exitCode = Invoke-Installer '--verify'
    if ($exitCode -ne 0) {
        throw 'BepInEx verify failed.'
    }
    $configurationManagerPath = Join-Path $gameRoot `
        'BepInEx\plugins\ConfigurationManager\ConfigurationManager.dll'
    if ($InstallConfigurationManager -and
        -not (Test-Path $configurationManagerPath)) {
        throw 'Default ConfigurationManager option did not install the official plugin.'
    }
    if (Test-Path $foreignRuntimePath) {
        throw 'Initial install left a foreign BepInEx runtime file behind.'
    }
    if (-not (Test-Path $foreignPluginPath)) {
        throw 'Initial install removed a plugin from the preserved plugins directory.'
    }
    if (-not (Test-Path (Join-Path $gameRoot 'doorstop_config.ini')) -or
        [IO.File]::ReadAllText((Join-Path $gameRoot 'doorstop_config.ini')) -eq 'foreign-doorstop') {
        throw 'Initial install did not replace a foreign Doorstop file.'
    }
    if (-not $InstallConfigurationManager -and
        (Test-Path $configurationManagerPath)) {
        throw 'Disabled ConfigurationManager option still installed the plugin.'
    }
    $env:MALODY_BEPINEX_INSTALL_CONFIGURATION_MANAGER = '0'

    $pluginPath = Join-Path $gameRoot 'BepInEx\plugins\sentinel.dll'
    New-Item -ItemType Directory -Path (Split-Path $pluginPath) -Force | Out-Null
    [IO.File]::WriteAllBytes($pluginPath, [byte[]](7, 8, 9))
    $configPath = Join-Path $gameRoot 'BepInEx\config\BepInEx.cfg'
    [IO.File]::AppendAllText($configPath, "`r`n# user change")
    $legacyRuntimePath = Join-Path $gameRoot 'BepInEx\legacy-runtime-marker.dll'
    [IO.File]::WriteAllBytes($legacyRuntimePath, [byte[]](4, 5, 6))
    $configurationManagerHash = (Get-FileHash $configurationManagerPath -Algorithm SHA256).Hash
    $legacyDoorstopPath = Join-Path $gameRoot 'doorstop_config.ini'
    [IO.File]::WriteAllText($legacyDoorstopPath, 'legacy')

    $exitCode = Invoke-Installer '--install'
    if ($exitCode -ne 0) {
        throw 'BepInEx repair failed.'
    }
    if (-not (Test-Path $pluginPath)) {
        throw 'Repair removed a user plugin.'
    }
    if (Test-Path $legacyRuntimePath) {
        throw 'Repair left an old BepInEx runtime file behind.'
    }
    if (-not (Test-Path $legacyDoorstopPath) -or
        [IO.File]::ReadAllText($legacyDoorstopPath) -eq 'legacy') {
        throw 'Repair did not replace the old Doorstop file.'
    }
    if ([IO.File]::ReadAllText($configPath).Contains('# user change')) {
        throw 'Repair preserved an old BepInEx config file.'
    }
    if (-not (Test-Path $configurationManagerPath)) {
        throw 'Repair removed a plugin from the preserved plugins directory.'
    }
    if ((Get-FileHash $configurationManagerPath -Algorithm SHA256).Hash -ne $configurationManagerHash) {
        throw 'Repair changed a plugin file in the preserved plugins directory.'
    }

    $exitCode = Invoke-Installer '--uninstall'
    if ($exitCode -ne 0) {
        throw 'BepInEx uninstall failed.'
    }
    if (Test-Path (Join-Path $gameRoot 'winhttp.dll')) {
        throw 'Uninstall left winhttp.dll behind.'
    }
    if (-not (Test-Path $pluginPath)) {
        throw 'Uninstall removed a user plugin.'
    }
    if (-not (Test-Path $foreignPluginPath)) {
        throw 'Uninstall removed a preserved foreign plugin.'
    }

    [IO.File]::WriteAllBytes(
        (Join-Path $gameRoot 'winhttp.dll'),
        [byte[]](9, 8, 7))
    $exitCode = Invoke-Installer '--install'
    if ($exitCode -ne 0) {
        throw 'Installer could not replace an old Doorstop winhttp.dll.'
    }
    if (-not (Test-Path $pluginPath)) {
        throw 'Replacing Doorstop removed the preserved user plugin.'
    }
}
finally {
    Remove-Item Env:MALODY_BEPINEX_PAYLOAD -ErrorAction SilentlyContinue
    Remove-Item Env:MALODY_BEPINEX_INSTALL_CONFIGURATION_MANAGER `
        -ErrorAction SilentlyContinue
    Remove-Item Env:MALODY_BEPINEX_QUIET -ErrorAction SilentlyContinue
}

Write-Output 'Malody V BepInEx installer integration test passed.'
