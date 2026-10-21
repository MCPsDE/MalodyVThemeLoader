param(
    [string]$PayloadZip = '',
    [string]$InstallerDll = '',
    [string]$InstallerExe = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repoRoot 'artifacts'

if ([string]::IsNullOrWhiteSpace($PayloadZip)) {
    $PayloadZip = Join-Path $artifactsRoot 'payload.zip'
}
if ([string]::IsNullOrWhiteSpace($InstallerDll)) {
    $InstallerDll = Join-Path $repoRoot `
        'src\MalodyVThemeLoader.Installer\bin\Release\net8.0-windows\win-x64\MalodyVThemeLoader.Installer.dll'
}

$testRoot = Join-Path $artifactsRoot 'installer-test'
$resolved = [IO.Path]::GetFullPath($testRoot)
$allowed = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe test path: $resolved"
}
if (Test-Path $resolved) {
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

$gameRoot = Join-Path $resolved 'MalodyV'
New-Item -ItemType Directory -Path (Join-Path $gameRoot 'Malody V_Data\il2cpp_data') `
    -Force | Out-Null
foreach ($name in @('Malody V.exe', 'UnityPlayer.dll', 'GameAssembly.dll')) {
    [IO.File]::WriteAllBytes((Join-Path $gameRoot $name), [byte[]](1, 2, 3, 4))
}

if ($InstallerExe) {
    $InstallerExe = [IO.Path]::GetFullPath($InstallerExe)
    Remove-Item Env:MALODY_THEMELOADER_PAYLOAD -ErrorAction SilentlyContinue
} else {
    $env:MALODY_THEMELOADER_PAYLOAD = $PayloadZip
}
$env:MALODY_THEMELOADER_QUIET = '1'
$env:MALODY_THEMELOADER_TEST_MODE = 'integration'
$env:MALODY_THEMELOADER_DESKTOP = Join-Path $resolved 'Desktop'
New-Item -ItemType Directory -Path $env:MALODY_THEMELOADER_DESKTOP `
    -Force | Out-Null
function Invoke-Installer([string]$Command) {
    if ($InstallerExe) {
        $process = Start-Process -FilePath $InstallerExe -ArgumentList @($Command, ('"' + $gameRoot + '"')) -PassThru -Wait -WindowStyle Hidden
        if ($process.ExitCode -ne 0) { $global:LASTEXITCODE = $process.ExitCode } else { $global:LASTEXITCODE = 0 }
    } else {
        & dotnet $InstallerDll $Command $gameRoot
        $global:LASTEXITCODE = $LASTEXITCODE
    }
}
try {
    Invoke-Installer '--check'
    if ($LASTEXITCODE -ne 0) { throw 'Dry-run failed.' }
    if (Test-Path (Join-Path $gameRoot 'winhttp.dll')) { throw 'Dry-run wrote a bootstrap file.' }
    Invoke-Installer '--install'
    if ($LASTEXITCODE -ne 0) { throw 'Fresh install failed.' }
    $expectedManager = Join-Path $artifactsRoot 'payload\ThemeLoader\MalodyVThemeManager.exe'
    $installedManager = Join-Path $gameRoot 'ThemeLoader\MalodyVThemeManager.exe'
    if ((Test-Path $expectedManager) -and
        (Get-FileHash -LiteralPath $expectedManager -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $installedManager -Algorithm SHA256).Hash) {
        throw 'Embedded theme manager differs from the final published payload.'
    }

    Invoke-Installer '--verify'
    if ($LASTEXITCODE -ne 0) { throw 'Verify after install failed.' }

    $obsoleteRelative = 'ThemeLoader/docs/obsolete-development.txt'
    $obsoletePath = Join-Path $gameRoot ($obsoleteRelative -replace '/', '\')
    [IO.File]::WriteAllText($obsoletePath, 'obsolete')
    $obsoleteHash = (Get-FileHash $obsoletePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestPath = Join-Path $gameRoot 'ThemeLoader\install-manifest.json'
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $manifest.files += [pscustomobject]@{
        relativePath = $obsoleteRelative
        sha256 = $obsoleteHash
        backupRelativePath = $null
    }
    $manifest | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $themeFile = Join-Path $gameRoot `
        'ThemeLoader\active-theme.txt'
    [IO.File]::WriteAllText($themeFile, 'IntegrationTheme')
    $ownedCore = Join-Path $gameRoot 'BepInEx\core\BepInEx.Core.dll'
    $ownedBootstrap = Join-Path $gameRoot 'winhttp.dll'
    $oldStamp = [DateTime]::UtcNow.AddDays(-7)
    [IO.File]::SetLastWriteTimeUtc($ownedCore,$oldStamp)
    [IO.File]::SetLastWriteTimeUtc($ownedBootstrap,$oldStamp)
    $coreStamp = [IO.File]::GetLastWriteTimeUtc($ownedCore)
    $bootstrapStamp = [IO.File]::GetLastWriteTimeUtc($ownedBootstrap)
    $runtimeConfig = Join-Path $gameRoot 'BepInEx\config\BepInEx.cfg'
    [IO.File]::AppendAllText($runtimeConfig,"`n# user runtime configuration`n")
    # A missing owned auxiliary file can be restored without overwriting the working runtime.
    Remove-Item -LiteralPath (Join-Path $gameRoot '.doorstop_version') -Force

    Remove-Item -LiteralPath (Join-Path $gameRoot `
        'BepInEx\plugins\MalodyVThemeLoader\MalodyVThemeLoader.Plugin.dll') -Force
    Invoke-Installer '--install'
    if ($LASTEXITCODE -ne 0) { throw 'Repair failed.' }
    if ([IO.File]::GetLastWriteTimeUtc($ownedCore) -ne $coreStamp -or
        [IO.File]::GetLastWriteTimeUtc($ownedBootstrap) -ne $bootstrapStamp) {
        throw 'Repair reinstalled an already compatible owned runtime.'
    }
    if (![IO.File]::ReadAllText($runtimeConfig).Contains('# user runtime configuration')) {
        throw 'Repair overwrote the existing runtime configuration.'
    }
    if (!(Test-Path (Join-Path $gameRoot '.doorstop_version'))) { throw 'Repair did not restore a missing owned auxiliary file.' }
    if ([IO.File]::ReadAllText($themeFile) -ne 'IntegrationTheme') {
        throw 'Repair overwrote the active theme selection.'
    }
    if (Test-Path $obsoletePath) {
        throw 'Repair left an unchanged obsolete owned file behind.'
    }

    Invoke-Installer '--verify'
    if ($LASTEXITCODE -ne 0) { throw 'Verify after repair failed.' }

    Invoke-Installer '--uninstall'
    if ($LASTEXITCODE -ne 0) { throw 'Uninstall failed.' }

    if (Test-Path (Join-Path $gameRoot 'winhttp.dll')) {
        throw 'Uninstall left the owned bootstrap DLL behind.'
    }
    if (Test-Path (Join-Path $env:MALODY_THEMELOADER_DESKTOP `
            '主题管理器.lnk')) {
        throw 'Uninstall left the owned desktop shortcut behind.'
    }

    [IO.File]::WriteAllBytes((Join-Path $gameRoot 'winhttp.dll'), [byte[]](9, 8, 7))
    Invoke-Installer '--install'
    if ($LASTEXITCODE -eq 0) {
        throw 'Installer overwrote an unowned winhttp.dll.'
    }
    $foreignBytes = [IO.File]::ReadAllBytes((Join-Path $gameRoot 'winhttp.dll'))
    if ($foreignBytes.Length -ne 3 -or $foreignBytes[0] -ne 9) {
        throw 'Installer changed an unowned winhttp.dll.'
    }
}
finally {
    Remove-Item Env:MALODY_THEMELOADER_PAYLOAD -ErrorAction SilentlyContinue
    Remove-Item Env:MALODY_THEMELOADER_QUIET -ErrorAction SilentlyContinue
    Remove-Item Env:MALODY_THEMELOADER_TEST_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:MALODY_THEMELOADER_DESKTOP -ErrorAction SilentlyContinue
}

Write-Output 'Installer integration test passed.'
