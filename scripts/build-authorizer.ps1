param(
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repoRoot 'artifacts'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $artifactsRoot 'authorizer'
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
$allowedRoot = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
if (-not $resolvedOutput.StartsWith(
        $allowedRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Authorizer output must stay below artifacts: $resolvedOutput"
}

if (Test-Path $resolvedOutput) {
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

dotnet publish (Join-Path $repoRoot `
        'src\MalodyVThemeLoader.Authorizer\MalodyVThemeLoader.Authorizer.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $resolvedOutput `
    -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) {
    throw 'Authorizer publish failed.'
}

Get-ChildItem -LiteralPath $resolvedOutput |
    Select-Object Name, Length
