param([string]$PluginPath = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Reflection.Metadata
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$PluginPath) { $PluginPath = Join-Path $repo 'artifacts\plugin-build\bin\MalodyVThemeLoader.Plugin\release\MalodyVThemeLoader.Plugin.dll' }
$stream = [IO.File]::OpenRead($PluginPath)
$pe = [Reflection.PortableExecutable.PEReader]::new($stream)
try {
    $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    $names = foreach($handle in $metadata.AssemblyReferences) {
        $metadata.GetString($metadata.GetAssemblyReference($handle).Name)
    }
    foreach($required in @('BepInEx.Unity.IL2CPP','BepInEx.Core','Il2CppInterop.Runtime','UnityEngine.CoreModule','Assembly-CSharp')) {
        if ($names -notcontains $required) { throw "Plugin lacks IL2CPP reference: $required" }
    }
    if ($names -match 'UnityEditor') { throw 'Plugin references Unity editor assemblies.' }
    foreach($handle in $metadata.TypeReferences) {
        $type = $metadata.GetTypeReference($handle)
        $typeName = $metadata.GetString($type.Namespace) + '.' + $metadata.GetString($type.Name)
        if ($typeName -match 'DeviceAuthorization|ConsentReceipt|LegalTerms') { throw "Obsolete startup restriction: $typeName" }
    }
    Write-Output 'Verified plugin IL2CPP references and unrestricted startup.'
    $names | Sort-Object
} finally { $pe.Dispose(); $stream.Dispose() }
