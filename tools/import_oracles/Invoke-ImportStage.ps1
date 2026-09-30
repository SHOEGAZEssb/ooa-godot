function Invoke-ImportStage {
    param(
        [object]$Contract,
        [string]$StageRoot,
        [hashtable]$Values,
        [hashtable]$Functions,
        [string[]]$CommonInputs = @(),
        [string[]]$CommonFunctions = @()
    )

    $stageInputs = @{}
    foreach ($name in @($CommonInputs) + @($Contract.Inputs)) {
        if (-not $Values.ContainsKey($name)) {
            throw "Import stage '$($Contract.Name)' input '$name' is unavailable."
        }
        $stageInputs[$name] = $Values[$name]
    }
    $stageFunctions = @{}
    foreach ($name in @($CommonFunctions) + @($Contract.FunctionInputs)) {
        if (-not $Functions.ContainsKey($name)) {
            throw "Import stage '$($Contract.Name)' function input '$name' is unavailable."
        }
        $stageFunctions[$name] = $Functions[$name]
    }

    try {
        # Construct directly: New-Module publishes exported functions into the
        # caller's session, which would make undeclared helpers reachable again.
        $stage = [Management.Automation.PSModuleInfo]::new({})
        $null = . $stage {
            param($stagePath, $stageValues, $stageHelpers,
                $stageVariableOutputs, $stageFunctionOutputs)
            $ErrorActionPreference = 'Stop'
            # Reject unbound variables without changing legacy table/property
            # handling. Each stage owns its script scope, including $script:.
            Set-StrictMode -Version 1.0
            foreach ($binding in $stageValues.GetEnumerator()) {
                Set-Variable -Name $binding.Key -Value $binding.Value -Scope Local
            }
            foreach ($binding in $stageHelpers.GetEnumerator()) {
                # Keep the original module-bound ScriptBlock: exported helpers
                # must retain their owner's private functions and source caches.
                Set-Item -LiteralPath "Function:$($binding.Key)" -Value $binding.Value
            }
            . $stagePath

            foreach ($outputName in $stageVariableOutputs) {
                if ($null -eq (Get-Variable -Name $outputName -Scope Local -ErrorAction SilentlyContinue)) {
                    throw "Declared variable output '$outputName' was not produced."
                }
            }
            foreach ($outputName in $stageFunctionOutputs) {
                if (-not (Test-Path -LiteralPath "Function:$outputName")) {
                    throw "Declared function output '$outputName' was not produced."
                }
            }
            Export-ModuleMember -Variable $stageVariableOutputs -Function $stageFunctionOutputs
        } (Join-Path $StageRoot $Contract.Script) $stageInputs $stageFunctions `
            $Contract.Outputs $Contract.FunctionOutputs
        return $stage
    }
    catch {
        throw [InvalidOperationException]::new(
            "Import stage '$($Contract.Name)' ($($Contract.Script)) failed: " +
            $_.Exception.Message + "`n" + $_.InvocationInfo.PositionMessage,
            $_.Exception)
    }
}
