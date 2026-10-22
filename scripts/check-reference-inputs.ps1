param([ValidateSet('Record','Verify','VerifyStable')][string]$Mode = 'Verify')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $repo 'artifacts\reference-inputs.json'
$roots = @('E:\UnityProjects\Emiria', 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV')
$current = foreach ($root in $roots) {
    Get-ChildItem -LiteralPath $root -File -Recurse -Force | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName; Length = $_.Length; Modified = $_.LastWriteTimeUtc.Ticks }
    }
}
# Metadata covers the full trees; Default resource hashes are checked by verify-payload.py.
if ($Mode -eq 'Record') {
    $current | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $output -Encoding utf8
    Write-Output "Recorded $($current.Count) reference files."
} else {
    $before = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    $changes = Compare-Object $before $current -Property Path,Length,Modified
    $changes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $repo 'artifacts\reference-changes.json') -Encoding utf8
    if ($Mode -eq 'VerifyStable') {
        $gamePrefix = $roots[1].TrimEnd('\') + '\'
        $stableChanges = @($changes | Where-Object {
            $path = $_.Path
            if (!$path.StartsWith($gamePrefix,[StringComparison]::OrdinalIgnoreCase)) { return $true }
            $relative = $path.Substring($gamePrefix.Length).Replace('\','/')
            return !($relative -in @('chart.bin','score.bin','song.bin','BepInEx/LogOutput.log') -or
                $relative.StartsWith('cache/') -or $relative.StartsWith('replay/') -or
                $relative.StartsWith('BepInEx/plugins/MalodyVReworkOverlay/MalodyV.ReworkOverlay.exe.WebView2/'))
        })
        if ($stableChanges.Count) { $stableChanges | Format-List; throw 'Source or stable game files changed.' }
        Write-Output "Source and stable game files unchanged. $(@($changes).Count) difference records are active-game databases, replay files or caches. Full differences: artifacts/reference-changes.json"
        exit 0
    }
    if ($changes) { $changes | Format-Table; throw 'Reference inputs changed.' }
    Write-Output "Unchanged: $($current.Count) reference files."
}
