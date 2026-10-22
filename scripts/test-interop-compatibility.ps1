param(
    [string]$PluginPath = '',
    [string]$InteropRoot = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV\BepInEx\interop'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$PluginPath) { $PluginPath = Join-Path $repo 'src\MalodyVThemeLoader.Plugin\bin\Release\net6.0\MalodyVThemeLoader.Plugin.dll' }
[Reflection.Assembly]::LoadFrom((Join-Path $repo 'vendor\bepinex\BepInEx\core\Mono.Cecil.dll')) | Out-Null
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory([IO.Path]::GetFullPath($InteropRoot))
$resolver.AddSearchDirectory((Join-Path $repo 'vendor\bepinex\BepInEx\core'))
$resolver.AddSearchDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($PluginPath)))
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$module = [Mono.Cecil.ModuleDefinition]::ReadModule([IO.Path]::GetFullPath($PluginPath), $parameters)
try {
    if ($module.Assembly.Name.Name -ne 'MalodyVThemeLoader.Plugin') { throw 'Expected the ThemeLoader runtime plugin.' }
    $missing = [Collections.Generic.List[string]]::new()
    $checked = 0
    foreach ($reference in $module.GetMemberReferences()) {
        $scope = $reference.DeclaringType.GetElementType().Scope
        if ($scope -isnot [Mono.Cecil.AssemblyNameReference]) { continue }
        if ($scope.Name -notin @('Assembly-CSharp','Live2D.Cubism','Live2DUnity','Framework','Unity.TextMeshPro') -and !$scope.Name.StartsWith('UnityEngine')) { continue }
        $checked++
        try {
            $resolved = $reference.Resolve()
            if ($null -eq $resolved) { $missing.Add($reference.FullName) }
            elseif (!$resolved.Module.FileName.StartsWith([IO.Path]::GetFullPath($InteropRoot).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
                $missing.Add($reference.FullName + ' resolved outside requested interop: ' + $resolved.Module.FileName)
            }
        } catch { $missing.Add($reference.FullName + ': ' + $_.Exception.GetBaseException().Message) }
    }
    $cubism = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $InteropRoot 'Live2D.Cubism.dll'))
    try {
        foreach ($contract in @(
            @('Live2D.Cubism.Core.CubismDrawable','get_TextureIndex','System.Int32',''),
            @('Live2D.Cubism.Core.CubismDrawable','get_VertexPositions','Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1<UnityEngine.Vector3>',''),
            @('Live2D.Cubism.Core.CubismDrawable','get_IsDoubleSided','System.Boolean',''),
            @('Live2D.Cubism.Core.CubismDrawable','get_IsMasked','System.Boolean',''),
            @('Live2D.Cubism.Core.CubismDrawable','get_IsInverted','System.Boolean',''),
            @('Live2D.Cubism.Core.CubismDrawable','get_BlendAdditive','System.Boolean',''),
            @('Live2D.Cubism.Core.CubismDrawable','get_MultiplyBlend','System.Boolean',''),
            @('Live2D.Cubism.Rendering.CubismRenderer','set_Material','System.Void','UnityEngine.Material'),
            @('Live2D.Cubism.Rendering.CubismRenderer','set_MainTexture','System.Void','UnityEngine.Texture2D')
        )) {
            $owner = $cubism.GetType($contract[0])
            $methods = @($owner.Methods | Where-Object Name -eq $contract[1])
            $method = if ($methods.Count -eq 1) { $methods[0] } else { $null }
            $expectedCount = if ($contract[3]) { 1 } else { 0 }
            if (!$method -or $method.IsStatic -or $method.ReturnType.FullName -ne $contract[2] -or $method.Parameters.Count -ne $expectedCount -or
                ($expectedCount -eq 1 -and $method.Parameters[0].ParameterType.FullName -ne $contract[3])) {
                $missing.Add(($contract -join ' '))
            }
        }
    } finally { $cubism.Dispose() }
    # Harmony target names are strings, so direct MemberRef resolution cannot
    # catch a stripped/renamed lifecycle method. Resolve those targets too.
    $lifecycleTargets = 0
    foreach ($patchType in $module.Types) {
        foreach ($attribute in $patchType.CustomAttributes) {
            if ($attribute.AttributeType.FullName -ne 'HarmonyLib.HarmonyPatch' -or $attribute.ConstructorArguments.Count -ne 2) { continue }
            $target = $attribute.ConstructorArguments[0].Value
            if ($target -isnot [Mono.Cecil.TypeReference] -or $target.FullName -ne 'Malody.Scene.Widget.WidgetLive2dControl') { continue }
            $targetName = [string]$attribute.ConstructorArguments[1].Value
            try {
                $owner = $target.Resolve()
                $matches = @($owner.Methods | Where-Object Name -eq $targetName)
                $expectedCount = if ($targetName -eq 'OnPointerClick') { 1 } else { 0 }
                if ($matches.Count -ne 1 -or $matches[0].IsStatic -or $matches[0].Parameters.Count -ne $expectedCount -or
                    $matches[0].ReturnType.FullName -ne 'System.Void') {
                    $missing.Add($target.FullName + '::' + $targetName + ' (Harmony target)')
                }
                $lifecycleTargets++
            } catch { $missing.Add($target.FullName + '::' + $targetName + ': ' + $_.Exception.GetBaseException().Message) }
        }
    }
    if ($missing.Count) {
        $missing | Write-Output
        throw "Interop incompatibility: $($missing.Count) missing references in $InteropRoot"
    }
    $resultPanel = $resolver.Resolve([Mono.Cecil.AssemblyNameReference]::Parse('Assembly-CSharp')).MainModule.GetType('Malody.Scene.Panel.PanelResultScore')
    $resultTargets = @($resultPanel.Methods | Where-Object { $_.Name -eq 'Fill' -and ($_.Parameters.Count -eq 2 -or
        ($_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.Name -in @('MPScore','ScoreRecord'))) })
    if ($resultTargets.Count -ne 3 -or @($resultTargets | Where-Object { $_.IsStatic -or $_.ReturnType.FullName -ne 'System.Void' }).Count) {
        throw 'Result display Fill targets differ from the validated three UI methods.'
    }
    if ($checked -eq 0) { throw 'No game/Unity references were checked.' }
    Write-Output "PASS: $checked game/Unity references, 9 Cubism signatures, $lifecycleTargets widget targets and 3 result display targets resolve against $InteropRoot"
} finally { $module.Dispose(); $resolver.Dispose() }
