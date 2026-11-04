param(
    [Parameter(Mandatory = $true)][string]$GameAssemblyPath,
    [Parameter(Mandatory = $true)][string]$ModAssemblyPath,
    [Parameter(Mandatory = $true)][string]$CecilPath
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$gameModule = [Mono.Cecil.ModuleDefinition]::ReadModule($GameAssemblyPath)
$modModule = [Mono.Cecil.ModuleDefinition]::ReadModule($ModAssemblyPath)
$checkedBindings = 0
try {
    foreach ($patchType in $modModule.Types) {
        $attributes = @($patchType.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' })
        if ($attributes.Count -eq 0) { continue }
        $targetTypeName = $null
        $targetMethodName = $null
        $targetArgumentTypes = $null
        foreach ($attribute in $attributes) {
            foreach ($argument in $attribute.ConstructorArguments) {
                if ($argument.Value -is [Mono.Cecil.TypeReference]) { $targetTypeName = $argument.Value.FullName }
                elseif ($argument.Value -is [Array]) { $targetArgumentTypes = @($argument.Value | ForEach-Object { $_.Value.FullName }) }
                elseif ($argument.Value -is [string]) { $targetMethodName = $argument.Value }
            }
        }
        if (!$targetTypeName -or !$targetMethodName) {
            if ($patchType.Name -eq 'ModJoinCompatibility_LoginPayload_Patch') {
                $targetTypeName = 'Network.Messages.Login'
                $targetMethodName = '.ctor'
                $targetArgumentTypes = @('System.String', 'System.String')
            } elseif ($patchType.Name -eq 'NpcTrackAccess_Patch') {
                $targetTypeName = 'TrainController'
                $targetMethodName = 'CanPlaceAt'
                $targetArgumentTypes = @('Track.Location', 'System.Collections.Generic.IReadOnlyCollection`1<Model.Car>', 'System.Single', 'System.Boolean', 'System.String&')
            } else {
                throw "Cannot resolve patch $($patchType.FullName)"
            }
        }
        $targetType = $gameModule.GetType($targetTypeName)
        if ($null -eq $targetType) { throw "Missing game type $targetTypeName" }
        $methods = @($targetType.Methods | Where-Object Name -eq $targetMethodName)
        if ($null -ne $targetArgumentTypes) { $methods = @($methods | Where-Object { (@($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -eq ($targetArgumentTypes -join ',') }) }
        if ($methods.Count -ne 1) { throw "Expected one target for $($patchType.Name), found $($methods.Count)" }
        if ($patchType.Name -match '^SimulationSpeedRestore_(StrategyCamera|CameraGround|CameraSnap|MouseLook|StationaryCamera|CharacterZoom|InputCounters)_Patch$') {
            $timeReads = @($methods[0].Body.Instructions | Where-Object {
                $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'UnityEngine.Time' -and
                $_.Operand.Name -in @('get_deltaTime', 'get_fixedDeltaTime')
            })
            if ($timeReads.Count -eq 0) { throw "Camera timing patch $($patchType.Name) has no supported time reads in the installed game." }
        }
        foreach ($hook in $patchType.Methods | Where-Object { $_.Name -in @('Prefix', 'Postfix', 'Finalizer') }) {
            foreach ($parameter in $hook.Parameters) {
                $name = $parameter.Name
                $actual = $parameter.ParameterType.FullName.TrimEnd('&')
                if ($name -eq '__result') {
                    if ($methods[0].ReturnType.FullName -eq 'System.Void') {
                        throw "Invalid Harmony result binding for $($patchType.Name).$($hook.Name): target $targetTypeName::$targetMethodName returns void"
                    }
                    if ($actual -ne $methods[0].ReturnType.FullName) {
                        throw "Invalid Harmony result type for $($patchType.Name).$($hook.Name): $actual vs $($methods[0].ReturnType.FullName)"
                    }
                    continue
                }
                if ($name -eq '__instance') {
                    if ($methods[0].IsStatic) { throw "Invalid Harmony instance binding for static target $targetTypeName::$targetMethodName" }
                    if ($actual -ne $targetTypeName -and $actual -ne 'System.Object') {
                        throw "Invalid Harmony instance type for $($patchType.Name): $actual vs $targetTypeName"
                    }
                    continue
                }
                if ($name -match '^__(\d+)$') {
                    $index = [int]$Matches[1]
                    if ($index -ge $methods[0].Parameters.Count) { throw "Invalid Harmony argument index $name for $targetTypeName::$targetMethodName" }
                    $expected = $methods[0].Parameters[$index].ParameterType.FullName.TrimEnd('&')
                    if ($actual -ne $expected) { throw "Type mismatch for $($patchType.Name).$name`: $actual vs $expected" }
                    continue
                }
                if ($name.StartsWith('___')) {
                    $fieldName = $name.Substring(3)
                    $field = @($targetType.Fields | Where-Object Name -eq $fieldName)
                    if ($field.Count -ne 1) { throw "Invalid Harmony field binding $name for $targetTypeName" }
                    $expected = $field[0].FieldType.FullName.TrimEnd('&')
                    if ($actual -ne $expected) { throw "Field type mismatch for $($patchType.Name).$name`: $actual vs $expected" }
                    continue
                }
                if ($name -in @('__state', '__args', '__originalMethod', '__runOriginal', '__exception')) { continue }
                if ($name.StartsWith('__')) { throw "Unknown Harmony special parameter $name in $($patchType.Name).$($hook.Name)" }
                $targetParameter = @($methods[0].Parameters | Where-Object Name -eq $parameter.Name)
                if ($targetParameter.Count -ne 1) {
                    throw "Invalid Harmony argument $($patchType.Name).$($hook.Name): $($parameter.Name)"
                }
                $expected = $targetParameter[0].ParameterType.FullName.TrimEnd('&')
                if ($actual -ne $expected) { throw "Type mismatch for $($patchType.Name): $expected vs $actual" }
            }
        }
        $checkedBindings++
    }
    $pickerPatch = $modModule.Types | Where-Object Name -eq 'AutoEngineerDestinationPicker_Loop_Patch'
    if (@($pickerPatch.Methods | Where-Object Name -eq 'Loop').Count -ne 0) {
        throw 'Waypoint picker must retain the native frame loop and registered Escape callback.'
    }
    foreach ($pickerMethod in $pickerPatch.Methods) {
        if (!$pickerMethod.HasBody) { continue }
        foreach ($instruction in $pickerMethod.Body.Instructions) {
            if ($instruction.Operand -is [Mono.Cecil.MethodReference] -and $instruction.Operand.Name -eq 'DidEscape') {
                throw 'Waypoint picker invokes the native cancellation callback as an input predicate.'
            }
        }
    }
    Write-Output "Verified $checkedBindings Harmony patch bindings against the installed game assembly; native waypoint picker loop preserved."
} finally {
    $gameModule.Dispose()
    $modModule.Dispose()
}




