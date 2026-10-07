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
        if ($patchType.Name -notmatch 'CaptiveLoad|AeLocal|NpcOil|NpcCaboose|NewGameStart|NpcLogOrdering|NpcIndustryPlanning|InterchangedIndustryLoader_ServeInterchange|Car_HandleCoupledChange_Runaround|AutoEngineerDestinationPicker_Loop|MapWindow_OnClick|EngineRosterPanel_Populate|EngineRosterRow_Refresh|ThroughTraffic|SimulatedInterchange|AutoEngineerPlanner_HandleCommand|LocomotiveControlsUIAdapter_UpdateCarText|OpsController_AnnounceCoalescedPayments|PlacerWindow_RandomConsist|TagController_UpdateTag|CabooseCrewLoadSlot|TrafficConsoleCommand|SwitchList_UnwaybilledCars|SwitchListSummary|InterchangeTimewarp|NpcInterchangeTimeWindow|SimulationSpeedRestore|NpcInboundDescriptor|NpcPickupPayment|NpcTimetable|NpcHeldDelivery|TimetableDelinquentPickups|NpcControls|NpcFuel|NpcTrafficRouting|NpcTrackAccess|NpcPoolPower|NpcStatusNotice|BrysonCtcMirrors|TimetableHistory_Save') { continue }
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
        if (!$targetTypeName -or !$targetMethodName) { throw "Cannot resolve patch $($patchType.FullName)" }
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
                if ($parameter.Name -eq '__instance' -and
                    $parameter.ParameterType.FullName -ne $targetTypeName -and
                    $parameter.ParameterType.FullName -ne 'System.Object') {
                    throw "Invalid Harmony instance type for $($patchType.Name): $($parameter.ParameterType.FullName) vs $targetTypeName"
                }
                if ($parameter.Name.StartsWith('__')) { continue }
                $targetParameter = @($methods[0].Parameters | Where-Object Name -eq $parameter.Name)
                if ($targetParameter.Count -ne 1) {
                    throw "Invalid Harmony argument $($patchType.Name).$($hook.Name): $($parameter.Name)"
                }
                $actual = $targetParameter[0].ParameterType.FullName.TrimEnd('&')
                $expected = $parameter.ParameterType.FullName.TrimEnd('&')
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




