param(
    [string]$Disassembly = "C:\msys64\home\timst\oracles-disasm",
    [string]$Rom = (Join-Path $PSScriptRoot "..\Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc")
)

$ErrorActionPreference = "Stop"
$importRoot = $PSScriptRoot
$importModuleRoot = Join-Path $importRoot 'import_oracles'

class ImportStageContract {
    [string]$Name
    [string]$Script
    [string[]]$Inputs
    [string[]]$Outputs
    [string[]]$FunctionInputs
    [string[]]$FunctionOutputs

    ImportStageContract(
        [string]$name,
        [string]$script,
        [string[]]$inputs,
        [string[]]$outputs,
        [string[]]$functionInputs,
        [string[]]$functionOutputs
    ) {
        $this.Name = $name
        $this.Script = $script
        $this.Inputs = $inputs
        $this.Outputs = $outputs
        $this.FunctionInputs = $functionInputs
        $this.FunctionOutputs = $functionOutputs
    }
}

class ImportStageResult {
    [string]$Name
    [Collections.Generic.Dictionary[string, object]]$Values

    ImportStageResult([string]$name) {
        $this.Name = $name
        $this.Values = [Collections.Generic.Dictionary[string, object]]::new(
            [StringComparer]::OrdinalIgnoreCase)
    }
}

function New-ImportStageContract(
    [string]$name,
    [string]$script,
    [string[]]$inputs = @(),
    [string[]]$outputs = @(),
    [string[]]$functionInputs = @(),
    [string[]]$functionOutputs = @()
) {
    return [ImportStageContract]::new(
        $name,
        $script,
        $inputs,
        $outputs,
        $functionInputs,
        $functionOutputs)
}

$stageContracts = @(
    New-ImportStageContract 'world' 'Import-WorldAssets.ps1' `
        -outputs @(
            'globalFlagValues', 'singleTileChangeRecords', 'tilesets',
            'paletteHeaderSource', 'paletteDataSource', 'tilesetRecordSize')
    New-ImportStageContract 'menus' 'Import-MenuAssets.ps1' `
        -inputs @('paletteDataSource') `
        -outputs @('textYaml') `
        -functionOutputs @(
            'Export-PaletteBlock', 'Read-PaletteBytes', 'Normalize-DialogueText')
    New-ImportStageContract `
        'menu-presentation' 'Import-MenuPresentationData.ps1'
    New-ImportStageContract 'dialogue' 'Import-DialogueAndIntro.ps1' `
        -inputs @('textYaml') `
        -outputs @(
            'npcInteractionIds', 'allTexts', 'allTextPositions', 'allTextIdsByName',
            'objectGfxHeaderSource') `
        -functionInputs @('Normalize-DialogueText')
    New-ImportStageContract 'map-and-items' 'Import-MapAndItemData.ps1' `
        -inputs @('allTextIdsByName', 'allTextPositions', 'allTexts') `
        -outputs @(
            'enemyUnspawnableTileCount', 'soundIds', 'treasureIds',
            'treasureObjectRecords', 'treasureObjectSource')
    New-ImportStageContract 'npcs' 'Import-NpcData.ps1' `
        -inputs @(
            'allTextPositions', 'allTexts', 'globalFlagValues',
            'npcInteractionIds', 'objectGfxHeaderSource', 'paletteHeaderSource',
            'singleTileChangeRecords', 'soundIds', 'tilesetRecordSize',
            'treasureIds', 'treasureObjectRecords') `
        -outputs @(
            'dungeonMechanicRows', 'dungeonSharedPlacementRows', 'gfxNames',
            'interactionAnimationSource', 'interactionGraphics',
            'mainObjectLines', 'mainObjectSource', 'nayruCutsceneSource',
            'nayruScriptSource', 'npcAnimationDefinitions', 'npcAnimationTables',
            'npcOamBlocks', 'npcOamPointerTables', 'npcRows',
            'treasureObjectSource') `
        -functionInputs @('Export-PaletteBlock') `
        -functionOutputs @('Resolve-NpcAnimation')
    New-ImportStageContract 'gasha' 'Import-GashaData.ps1' `
        -inputs @(
            'allTexts', 'gfxNames', 'interactionAnimationSource',
            'interactionGraphics', 'mainObjectSource',
            'npcAnimationDefinitions', 'npcAnimationTables', 'npcOamBlocks')
    New-ImportStageContract 'cutscenes' 'Import-CutsceneData.ps1' `
        -inputs @(
            'allTextPositions', 'allTexts', 'gfxNames', 'globalFlagValues',
            'interactionAnimationSource', 'interactionGraphics',
            'mainObjectLines', 'mainObjectSource', 'nayruCutsceneSource',
            'nayruScriptSource', 'npcAnimationTables', 'npcRows',
            'paletteDataSource', 'paletteHeaderSource', 'treasureObjectRecords',
            'treasureObjectSource', 'soundIds', 'treasureIds') `
        -functionInputs @(
            'Export-PaletteBlock', 'Read-PaletteBytes', 'Resolve-NpcAnimation')
    New-ImportStageContract 'enemies' 'Import-EnemyData.ps1' `
        -inputs @('allTexts', 'gfxNames') `
        -outputs @(
            'crowRows', 'gelInstanceCount', 'keeseInstanceCount',
            'octorokInstanceCount', 'orderedObjectRows', 'partAnimationSource',
            'partDataSource', 'partOamSource', 'stalfosInstanceCount',
            'zolInstanceCount') `
        -functionOutputs @(
            'Copy-EnemySprite', 'Get-EnemyDefinition', 'Resolve-Oam')
    New-ImportStageContract 'seed-trees' 'Import-SeedTreeData.ps1' `
        -inputs @(
            'allTexts', 'gfxNames', 'mainObjectLines', 'partAnimationSource',
            'partDataSource', 'partOamSource') `
        -functionInputs @(
            'Copy-EnemySprite', 'Get-AssemblyLabelBody', 'Resolve-Oam')
    New-ImportStageContract 'maple' 'Import-MapleData.ps1' `
        -inputs @(
            'allTexts', 'gfxNames', 'interactionGraphics',
            'partAnimationSource', 'partOamSource') `
        -functionInputs @(
            'Get-AssemblyLabelBody', 'Resolve-NpcAnimation', 'Resolve-Oam')
    New-ImportStageContract 'spirits-grave' 'Import-SpiritsGrave.ps1' `
        -inputs @(
            'allTexts', 'gfxNames', 'interactionAnimationSource',
            'interactionGraphics', 'mainObjectSource', 'npcAnimationTables',
            'npcOamBlocks', 'npcOamPointerTables', 'paletteHeaderSource',
            'partAnimationSource', 'partOamSource') `
        -functionInputs @(
            'Copy-EnemySprite', 'Get-AssemblyLabelBody', 'Get-EnemyDefinition',
            'Read-PaletteBytes', 'Resolve-Oam')
    New-ImportStageContract 'wing-dungeon' 'Import-WingDungeon.ps1' `
        -inputs @('allTexts', 'mainObjectSource')
    New-ImportStageContract 'navigation' 'Import-WorldNavigation.ps1'
    New-ImportStageContract 'audio' 'Import-AudioData.ps1'
    New-ImportStageContract 'manifest' 'Write-GeneratedTableManifest.ps1'
)

$commonStageInputs = @('destination', 'Disassembly', 'romBytes')
$commonStageFunctionInputs = @(
    'Convert-AssemblyInteger', 'Copy-GeneratedFile', 'Get-AssemblyLabelBody',
    'Read-AssemblyAnimationDefinitions', 'Read-AssemblyConstants',
    'Read-AssemblyDataDirectives',
    'Read-AssemblyDwTables', 'Read-AssemblyLabelBlock',
    'Read-AssemblyLabelNodes', 'Read-AssemblyLabels',
    'Read-AssemblyMacroInvocations', 'Read-AssemblyNodes',
    'Read-AssemblyInstructions', 'Read-AssemblyLiteralValues',
    'Read-ImportLines', 'Read-ImportText',
    'Resolve-AssemblySourceTextPath', 'Write-GeneratedBytes',
    'Write-GeneratedTable')
$automaticStageVariables = @(
    'args', 'error', 'executioncontext', 'false', 'foreach', 'host', 'input',
    'lastexitcode', 'matches', 'myinvocation', 'nestedpromptlevel', 'null',
    'ofs', 'pid', 'profile', 'psboundparameters', 'pscmdlet', 'pshome',
    'psitem', 'pwd', 'shellid', 'stacktrace', 'switch', 'this', 'true', '_')

function Assert-ImportStageSourceContract(
    [ImportStageContract]$contract,
    [Collections.Generic.Dictionary[string, string]]$functionOwners
) {
    $path = Join-Path $importModuleRoot $contract.Script
    $tokens = $null
    $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile(
        $path,
        [ref]$tokens,
        [ref]$errors)
    if ($errors.Count -ne 0) {
        throw "$($contract.Script) has parser errors: $($errors -join '; ')"
    }

    $assigned = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($assignment in $ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.AssignmentStatementAst]
    }, $true)) {
        foreach ($variable in $assignment.Left.FindAll({
            param($node)
            $node -is [Management.Automation.Language.VariableExpressionAst]
        }, $true)) {
            [void]$assigned.Add($variable.VariablePath.UserPath)
        }
    }
    foreach ($loop in $ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.ForEachStatementAst]
    }, $true)) {
        [void]$assigned.Add($loop.Variable.VariablePath.UserPath)
    }
    foreach ($parameter in $ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.ParameterAst]
    }, $true)) {
        [void]$assigned.Add($parameter.Name.VariablePath.UserPath)
    }

    $declaredInputs = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @($commonStageInputs) + @($contract.Inputs)) {
        [void]$declaredInputs.Add($name)
    }
    $undeclared = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($variable in $ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.VariableExpressionAst]
    }, $true)) {
        $name = $variable.VariablePath.UserPath
        if ($name.Contains(':') -or
            $name -notmatch '^[a-z_][a-z0-9_]*$' -or
            $automaticStageVariables -contains $name -or
            $assigned.Contains($name) -or
            $declaredInputs.Contains($name)) {
            continue
        }
        [void]$undeclared.Add($name)
    }
    if ($undeclared.Count -ne 0) {
        throw "Import stage '$($contract.Name)' has undeclared variable inputs: " +
            (($undeclared | Sort-Object) -join ', ')
    }

    $declaredFunctionInputs = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @($commonStageFunctionInputs) + @($contract.FunctionInputs)) {
        [void]$declaredFunctionInputs.Add($name)
    }
    $undeclaredFunctions = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($command in $ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.CommandAst]
    }, $true)) {
        $name = $command.GetCommandName()
        if ($null -eq $name -or -not $functionOwners.ContainsKey($name)) {
            continue
        }
        if ($functionOwners[$name] -eq $contract.Script -or
            $declaredFunctionInputs.Contains($name)) {
            continue
        }
        [void]$undeclaredFunctions.Add($name)
    }
    if ($undeclaredFunctions.Count -ne 0) {
        throw "Import stage '$($contract.Name)' has undeclared function inputs: " +
            (($undeclaredFunctions | Sort-Object) -join ', ')
    }

    foreach ($output in $contract.Outputs) {
        if (-not $assigned.Contains($output)) {
            throw "Import stage '$($contract.Name)' declares variable output " +
                "'$output' but never assigns it."
        }
    }
    $definedFunctions = @($ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst]
    }, $true) | ForEach-Object Name)
    foreach ($output in $contract.FunctionOutputs) {
        if ($definedFunctions -notcontains $output) {
            throw "Import stage '$($contract.Name)' declares function output " +
                "'$output' but never defines it."
        }
    }
}

$importSucceeded = $false
$assemblySourceStats = ''
$importStageResults = [Collections.Generic.Dictionary[string, ImportStageResult]]::new(
    [StringComparer]::OrdinalIgnoreCase)
try {
    . (Join-Path $importModuleRoot 'Initialize-Import.ps1')

    $functionOwners = [Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($script in @('Initialize-Import.ps1') + @($stageContracts.Script)) {
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $importModuleRoot $script),
            [ref]$tokens,
            [ref]$errors)
        foreach ($function in $ast.FindAll({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst]
        }, $true)) {
            $functionOwners[$function.Name] = $script
        }
    }

    foreach ($contract in $stageContracts) {
        Assert-ImportStageSourceContract $contract $functionOwners
        foreach ($inputName in @($commonStageInputs) + @($contract.Inputs)) {
            if ($null -eq (Get-Variable -Name $inputName -ErrorAction SilentlyContinue)) {
                throw "Import stage '$($contract.Name)' input '$inputName' is unavailable."
            }
        }
        foreach ($functionName in
            @($commonStageFunctionInputs) + @($contract.FunctionInputs)) {
            if ($null -eq (Get-Command $functionName -CommandType Function `
                    -ErrorAction SilentlyContinue)) {
                throw "Import stage '$($contract.Name)' function input " +
                    "'$functionName' is unavailable."
            }
        }

        . (Join-Path $importModuleRoot $contract.Script)

        $result = [ImportStageResult]::new($contract.Name)
        foreach ($outputName in $contract.Outputs) {
            $outputVariable = Get-Variable -Name $outputName -ErrorAction Stop
            $result.Values.Add($outputName, $outputVariable.Value)
        }
        foreach ($functionName in $contract.FunctionOutputs) {
            if ($null -eq (Get-Command $functionName -CommandType Function `
                    -ErrorAction SilentlyContinue)) {
                throw "Import stage '$($contract.Name)' did not produce function " +
                    "'$functionName'."
            }
        }
        $importStageResults.Add($contract.Name, $result)
    }
    $assemblySourceStats = Invoke-AssemblySourceHost `
        $assemblySourceHost 'ASSERT'
    $importSucceeded = $true
}
finally {
    if ($null -ne $assemblySourceHost) {
        try {
            [void](Invoke-AssemblySourceHost $assemblySourceHost 'QUIT')
            if (-not $assemblySourceHost.WaitForExit(5000)) {
                $assemblySourceHost.Kill()
                throw 'Importer source host did not exit after QUIT.'
            }
            if ($assemblySourceHost.ExitCode -ne 0 -and $importSucceeded) {
                throw "Importer source host exited with code " +
                    "$($assemblySourceHost.ExitCode): " +
                    $assemblySourceHost.StandardError.ReadToEnd()
            }
        }
        finally {
            $assemblySourceHost.Dispose()
        }
    }
}

Write-Host "Validated clean US ROM: $hash"
$assemblySourceParts = $assemblySourceStats.Split("`t")
Write-Host (
    "Parsed $($assemblySourceParts[0]) assembly sources with " +
    "$($assemblySourceParts[1]) physical reads and " +
    "$($assemblySourceParts[2]) indexed label-block / " +
    "$($assemblySourceParts[3]) structured-node queries.")
Write-Host "Imported $($tilesets.Count) tilesets, 1536 rooms, 42 signs, $($npcRows.Count - 1) NPCs, $($dungeonMechanicRows.Count - 1) dungeon button/trigger/chest/shutter placements, $($dungeonSharedPlacementRows.Count - 1) shared dungeon-entry placements, $keeseInstanceCount Keese, $($crowRows.Count - 1) fixed Crows, $octorokInstanceCount Octoroks, $stalfosInstanceCount ordinary Stalfos, $zolInstanceCount Zols, $gelInstanceCount direct Gels, $($orderedObjectRows.Count - 1) ordered placement records, $enemyUnspawnableTileCount enemy-unspawnable tile records, 133 chests, 529 warps, 22 animation groups, and 223 sound IDs into $destination"
