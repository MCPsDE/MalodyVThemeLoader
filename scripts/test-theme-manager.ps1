param(
    [string]$PayloadZip = '',
    [string]$InstallerDll = '',
    [string]$ManagerDll = '',
    [string]$SharedDll = ''
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') {
    throw 'This test loads .NET 6/8 assemblies and must be run with PowerShell 7: pwsh -File scripts\test-theme-manager.ps1'
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repoRoot 'artifacts'

if ([string]::IsNullOrWhiteSpace($PayloadZip)) {
    $PayloadZip = Join-Path $artifactsRoot 'payload.zip'
}
if ([string]::IsNullOrWhiteSpace($InstallerDll)) {
    $InstallerDll = Join-Path $artifactsRoot `
        'installer-build\bin\MalodyVThemeLoader.Installer\release_win-x64\MalodyVThemeLoader.Installer.dll'
}
if ([string]::IsNullOrWhiteSpace($ManagerDll)) {
    $ManagerDll = Join-Path $artifactsRoot `
        'manager-build\bin\MalodyVThemeLoader.ThemeManager\release_win-x64\MalodyVThemeLoader.ThemeManager.dll'
}
if ([string]::IsNullOrWhiteSpace($SharedDll)) {
    $SharedDll = Join-Path $artifactsRoot `
        'manager-build\bin\MalodyVThemeLoader.Shared\release\MalodyVThemeLoader.Shared.dll'
}

$testRoot = [IO.Path]::GetFullPath(
    (Join-Path $artifactsRoot 'theme-manager-test'))
$allowed = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
if (-not $testRoot.StartsWith(
        $allowed,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe test path: $testRoot"
}
if (Test-Path $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}

$gameRoot = Join-Path $testRoot 'MalodyV'
$desktopRoot = Join-Path $testRoot 'Desktop'
$authoringRoot = Join-Path $testRoot 'Authoring\Minimal'
$packagePath = Join-Path $testRoot 'minimal.mtp'
$exportPath = Join-Path $testRoot 'minimal-exported.mtp'

New-Item -ItemType Directory -Path `
    (Join-Path $gameRoot 'Malody V_Data\il2cpp_data') -Force | Out-Null
New-Item -ItemType Directory -Path $desktopRoot -Force | Out-Null
New-Item -ItemType Directory -Path `
    (Join-Path $authoringRoot 'images\Textures\Malody') -Force | Out-Null
foreach ($name in @('Malody V.exe', 'UnityPlayer.dll', 'GameAssembly.dll')) {
    [IO.File]::WriteAllBytes((Join-Path $gameRoot $name), [byte[]](1, 2, 3, 4))
}

$manifest = [ordered]@{
    formatVersion = 1
    id = 'integration-minimal'
    name = 'Integration Minimal'
    version = '1.0.0'
    author = 'ThemeLoader tests'
    description = 'Minimal manager integration package.'
    gameVersion = '6.7'
    website = ''
    tags = @('integration')
    imageRoot = 'images'
    fontRoot = 'fonts'
    live2dRoot = 'live2d'
    fontMappings = @()
}
$manifest | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath (Join-Path $authoringRoot 'theme.json') `
        -Encoding UTF8
[IO.File]::WriteAllBytes(
    (Join-Path $authoringRoot 'images\Textures\Malody\placeholder.png'),
    [byte[]](1, 3, 3, 7))

$env:MALODY_THEMELOADER_PAYLOAD = $PayloadZip
$env:MALODY_THEMELOADER_QUIET = '1'
$env:MALODY_THEMELOADER_TEST_MODE = 'integration'
$env:MALODY_THEMELOADER_DESKTOP = $desktopRoot
try {
    dotnet $InstallerDll --install $gameRoot
    if ($LASTEXITCODE -ne 0) { throw 'Test installation failed.' }

    $sharedAssembly = [Reflection.Assembly]::LoadFrom(
        [IO.Path]::GetFullPath($SharedDll))
    $managerAssembly = [Reflection.Assembly]::LoadFrom(
        [IO.Path]::GetFullPath($ManagerDll))
    $packageType = $sharedAssembly.GetType(
        'MalodyVThemeLoader.Shared.ThemePackage',
        $true)
    $packageType.GetMethod('CreateFromDirectory').Invoke(
        $null,
        [object[]]@(
            [string]$authoringRoot,
            [string]$packagePath))

    $maliciousPath = Join-Path $testRoot 'path-traversal.mtp'
    $maliciousStream = [IO.File]::Open(
        $maliciousPath,
        [IO.FileMode]::Create,
        [IO.FileAccess]::ReadWrite)
    try {
        $maliciousArchive = [IO.Compression.ZipArchive]::new(
            $maliciousStream,
            [IO.Compression.ZipArchiveMode]::Create,
            $true)
        try {
            $entry = $maliciousArchive.CreateEntry('../escape.txt')
            $writer = [IO.StreamWriter]::new($entry.Open())
            try {
                $writer.Write('blocked')
            }
            finally {
                $writer.Dispose()
            }
        }
        finally {
            $maliciousArchive.Dispose()
        }
    }
    finally {
        $maliciousStream.Dispose()
    }

    $rejectedTraversal = $false
    try {
        $packageType.GetMethod('Inspect').Invoke(
            $null,
            [object[]]@([string]$maliciousPath)) | Out-Null
    }
    catch {
        $rejectedTraversal = $true
    }
    if (-not $rejectedTraversal) {
        throw 'Theme package path traversal was not rejected.'
    }

    $libraryType = $managerAssembly.GetType(
        'MalodyVThemeLoader.ThemeManager.ThemeLibrary',
        $true)
    $library = [Activator]::CreateInstance(
        $libraryType,
        [Reflection.BindingFlags]'Instance,Public,NonPublic',
        $null,
        [object[]]@([string]$gameRoot),
        $null)
    $imported = $libraryType.GetMethod('Import').Invoke(
        $library,
        [object[]]@([string]$packagePath, [bool]$false))
    $libraryType.GetMethod('Activate').Invoke(
        $library,
        [object[]]@($imported))
    $libraryType.GetMethod('SetRealtimeMode').Invoke(
        $library,
        [object[]]@([bool]$true))
    $settings = $libraryType.GetMethod('GetSettings').Invoke(
        $library,
        [object[]]@())
    if (-not $settings.RealtimeMode) {
        throw 'Realtime mode was not persisted.'
    }
    $libraryType.GetMethod('Export').Invoke(
        $library,
        [object[]]@($imported, [string]$exportPath))

    $exportInfo = $packageType.GetMethod('Inspect').Invoke(
        $null,
        [object[]]@([string]$exportPath))
    if ($exportInfo.Manifest.Id -ne 'integration-minimal') {
        throw 'Exported theme metadata did not match.'
    }
    if ([IO.File]::ReadAllText(
            (Join-Path $gameRoot 'ThemeLoader\active-theme.txt')).Trim() `
            -ne 'integration-minimal') {
        throw 'Theme activation was not persisted.'
    }

    $removed = $libraryType.GetMethod('RemoveAllImported').Invoke(
        $library,
        [object[]]@())
    if ($removed -ne 1) {
        throw "Expected one imported theme to be removed, got $removed."
    }

    $libraryType.GetMethod('Import').Invoke(
        $library,
        [object[]]@([string]$packagePath, [bool]$false)) | Out-Null
    $libraryType.GetMethod('RemoveEveryThemeForUninstall').Invoke(
        $library,
        [object[]]@())

    $uninstallType = $managerAssembly.GetType(
        'MalodyVThemeLoader.ThemeManager.UninstallCoordinator',
        $true)
    $message = $uninstallType.GetMethod('Uninstall').Invoke(
        $null,
        [object[]]@([string]$gameRoot, $null))

    if (Test-Path (Join-Path $gameRoot 'winhttp.dll')) {
        throw 'Manager uninstall left winhttp.dll behind.'
    }
    if (Test-Path (Join-Path $desktopRoot '主题管理器.lnk')) {
        throw 'Manager uninstall left the desktop shortcut behind.'
    }
    if (Test-Path (Join-Path $gameRoot 'ThemeLoader\install-manifest.json')) {
        throw 'Manager uninstall left the install manifest behind.'
    }

    Write-Output $message
    Write-Output 'Theme manager integration test passed.'
}
finally {
    Remove-Item Env:MALODY_THEMELOADER_PAYLOAD -ErrorAction SilentlyContinue
    Remove-Item Env:MALODY_THEMELOADER_QUIET -ErrorAction SilentlyContinue
    Remove-Item Env:MALODY_THEMELOADER_TEST_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:MALODY_THEMELOADER_DESKTOP -ErrorAction SilentlyContinue
}
