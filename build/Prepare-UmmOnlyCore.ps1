param(
    [Parameter(Mandatory = $true)][string]$CorePath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][string]$CecilPath
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$module = [Mono.Cecil.ModuleDefinition]::ReadModule($CorePath)
try {
    $calls = @($module.Types | ForEach-Object { $_.Methods } |
        Where-Object { $_.HasBody } | ForEach-Object { $_.Body.Instructions } |
        Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call })
    $legacy = @($calls | Where-Object { $_.Operand.ToString() -eq 'System.Void Railloader.Injector::Inject()' })
    $umm = @($calls | Where-Object { $_.Operand.ToString() -eq 'System.Void UnityModManagerNet.Injection.UnityModManagerStarter::Start()' })
    if ($legacy.Count -ne 1 -or $umm.Count -ne 1) { throw 'Unexpected loader layout; refusing to modify the assembly.' }
    $legacy[0].OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
    $legacy[0].Operand = $null
    foreach ($reference in @($module.AssemblyReferences | Where-Object Name -eq 'Railloader.Injector')) {
        $module.AssemblyReferences.Remove($reference) | Out-Null
    }
    $module.Write($OutputPath)
} finally { $module.Dispose() }

$verify = [Mono.Cecil.ModuleDefinition]::ReadModule($OutputPath)
try {
    if (@($verify.AssemblyReferences | Where-Object Name -like 'Railloader*').Count -gt 0) { throw 'Legacy loader dependency remains.' }
    $ummCalls = @($verify.Types | ForEach-Object { $_.Methods } | Where-Object { $_.HasBody } |
        ForEach-Object { $_.Body.Instructions } | Where-Object {
            $_.Operand -and $_.Operand.ToString() -eq 'System.Void UnityModManagerNet.Injection.UnityModManagerStarter::Start()'
        })
    if ($ummCalls.Count -ne 1) { throw 'UMM startup verification failed.' }
    Write-Output 'Prepared UMM-only core: legacy injector removed; UMM startup preserved.'
} finally { $verify.Dispose() }
