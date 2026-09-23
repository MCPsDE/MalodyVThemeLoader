param(
    [string]$PayloadZip = '',
    [string]$InstallerDll = '',
    [string]$InstallerExe = '',
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

$testRoot = [IO.Path]::GetFullPath(
    (Join-Path $artifactsRoot 'bepinex-installer-test'))
$allowed = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
if (-not $testRoot.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe test path: $testRoot"
}
if (Test-Path $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}

$gameRoot = Join-Path $testRoot 'MalodyV'
New-Item -ItemType Directory -Path `
    (Join-Path $gameRoot 'Malody V_Data\il2cpp_data') -Force | Out-Null
foreach ($name in @('Malody V.exe', 'UnityPlayer.dll', 'GameAssembly.dll')) {
    [IO.File]::WriteAllBytes(
        (Join-Path $gameRoot $name),
        [byte[]](1, 2, 3, 4))
}

$env:MALODY_BEPINEX_PAYLOAD = $PayloadZip
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
        & dotnet $InstallerDll $Command $gameRoot
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

    $exitCode = Invoke-Installer '--install'
    if ($exitCode -ne 0) {
        throw 'BepInEx repair failed.'
    }
    if (-not (Test-Path $pluginPath)) {
        throw 'Repair removed a user plugin.'
    }
    if (-not ([IO.File]::ReadAllText($configPath).Contains('# user change'))) {
        throw 'Repair overwrote the user BepInEx config.'
    }
    if (Test-Path $configurationManagerPath) {
        throw 'Repair with ConfigurationManager disabled left the old official plugin behind.'
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

    [IO.File]::WriteAllBytes(
        (Join-Path $gameRoot 'winhttp.dll'),
        [byte[]](9, 8, 7))
    $exitCode = Invoke-Installer '--install'
    if ($exitCode -eq 0) {
        throw 'Installer overwrote an unowned winhttp.dll.'
    }
}
finally {
    if ([string]::IsNullOrWhiteSpace($InstallerExe)) {
        Remove-Item Env:MALODY_BEPINEX_PAYLOAD -ErrorAction SilentlyContinue
    }
    Remove-Item Env:MALODY_BEPINEX_INSTALL_CONFIGURATION_MANAGER `
        -ErrorAction SilentlyContinue
    Remove-Item Env:MALODY_BEPINEX_QUIET -ErrorAction SilentlyContinue
}

Write-Output 'Malody V BepInEx installer integration test passed.'
