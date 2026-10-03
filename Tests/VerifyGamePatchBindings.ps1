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
        if ($patchType.Name -notmatch 'ThroughTraffic|SimulatedInterchange|AutoEngineerPlanner_HandleCommand|LocomotiveControlsUIAdapter_UpdateCarText|OpsController_AnnounceCoalescedPayments|PlacerWindow_RandomConsist|TagController_UpdateTag|CabooseCrewLoadSlot|TrafficConsoleCommand|SwitchList_UnwaybilledCars|SwitchListSummary|InterchangeTimewarp|NpcInterchangeTimeWindow|SimulationSpeedRestore|NpcInboundDescriptor|NpcPickupPayment|TimetableDelinquentPickups|NpcHeldDelivery|NpcTimetable|NpcControls|NpcFuel|NpcTrafficRouting|NpcTrackAccess|NpcPoolPower|NpcStatusNotice|BrysonCtcMirrors|TimetableHistory_Save') { continue }
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
    Write-Output "Verified $checkedBindings Harmony patch bindings against the installed game assembly."
} finally {
    $gameModule.Dispose()
    $modModule.Dispose()
}




