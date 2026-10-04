param(
    [string]$Disassembly = (Join-Path $PSScriptRoot '..\..\oracles-disasm'),
    [string]$Rom = (Join-Path $PSScriptRoot "..\Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc"),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\assets\oracle'),
    [switch]$SkipBuild
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
    New-ImportStageContract 'vanilla-tilesets' 'Import-VanillaTilesets.ps1' `
        -functionOutputs @('Expand-TransitionGraphics')
    New-ImportStageContract 'world' 'Import-WorldAssets.ps1' `
        -outputs @(
            'globalFlagValues', 'singleTileChangeRecords', 'tilesets',
            'paletteHeaderSource', 'paletteDataSource', 'tilesetRecordSize', 'tilesetMetadata')
    New-ImportStageContract 'save-initialization' 'Import-SaveInitializationData.ps1'
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
            'allTextFallthroughIds', 'objectGfxHeaderSource') `
        -functionInputs @('Normalize-DialogueText')
    New-ImportStageContract 'map-and-items' 'Import-MapAndItemData.ps1' `
        -inputs @('allTextIdsByName', 'allTextPositions', 'allTexts', 'allTextFallthroughIds') `
        -outputs @(
            'enemyUnspawnableTileCount', 'soundIds', 'treasureIds',
            'treasureObjectRecords', 'treasureObjectSource') `
        -functionOutputs @('Read-HexBytes')
    New-ImportStageContract 'npcs' 'Import-NpcData.ps1' `
        -inputs @(
            'allTextFallthroughIds', 'allTextPositions', 'allTexts',
            'globalFlagValues',
            'npcInteractionIds', 'objectGfxHeaderSource', 'paletteHeaderSource',
            'singleTileChangeRecords', 'soundIds', 'tilesetRecordSize',
            'tilesetMetadata', 'treasureIds', 'treasureObjectRecords') `
        -outputs @(
            'dungeonMechanicRows', 'dungeonSharedPlacementRows', 'gfxNames',
            'interactionAnimationSource', 'interactionGraphics',
            'mainObjectLines', 'mainObjectSource', 'nayruCutsceneSource',
            'nayruScriptSource', 'npcAnimationDefinitions', 'npcAnimationTables',
            'npcOamBlocks', 'npcOamPointerTables', 'npcRows',
            'treasureObjectSource') `
        -functionInputs @('Export-PaletteBlock') `
        -functionOutputs @('Resolve-NpcAnimation')
    New-ImportStageContract 'collapsing-floor' 'Import-CollapsingFloor.ps1' `
        -inputs @('mainObjectLines', 'soundIds')
    New-ImportStageContract 'gasha' 'Import-GashaData.ps1' `
        -inputs @(
            'allTexts', 'gfxNames', 'interactionAnimationSource',
            'interactionGraphics', 'mainObjectSource',
            'npcAnimationDefinitions', 'npcAnimationTables', 'npcOamBlocks')
    New-ImportStageContract 'cutscene-commands' 'Import-CutsceneData.ps1' `
        -inputs @('assemblySourceHost') `
        -outputs @('cutsceneCommandHeader', 'generatedCutsceneCommandStreams') `
        -functionInputs @('Invoke-AssemblySourceHost') `
        -functionOutputs @('ConvertTo-CutsceneCommandPayload', 'Find-CutsceneCommandSourceLine', 'Get-AssemblySourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Test-GeneratedCutsceneCommandStreams', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-normalization' 'Convert-CutsceneCommands.ps1' `
        -inputs @('cutsceneCommandHeader') `
        -functionInputs @('New-CutsceneCommandRow') `
        -functionOutputs @('ConvertTo-CutsceneCommandRows')
    New-ImportStageContract 'cutscene-portals' 'Import-TimePortalData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'interactionAnimationSource', 'interactionGraphics', 'mainObjectLines', 'npcAnimationTables', 'soundIds', 'treasureIds') `
        -functionInputs @('Read-PaletteBytes', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'water-pushblocks' 'Import-WaterPushblockData.ps1' `
        -inputs @('gfxNames', 'interactionGraphics', 'soundIds') `
        -functionInputs @('Resolve-NpcAnimation')
    New-ImportStageContract 'cutscene-maku-tree' 'Import-MakuTreeData.ps1' `
        -inputs @('allTextFallthroughIds', 'allTextPositions', 'allTexts', 'gfxNames', 'interactionGraphics', 'paletteDataSource', 'paletteHeaderSource', 'treasureObjectRecords', 'treasureObjectSource') `
        -outputs @('makuStopSound', 'objectGfxSource') `
        -functionInputs @('Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-ralph-portal' 'Import-RalphPortalData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'npcRows') `
        -outputs @('globalFlagSource', 'ralphScriptSource', 'speedMatch', 'speedSource') `
        -functionInputs @('Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-deku-forest' 'Import-DekuForestData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'mainObjectSource', 'paletteDataSource', 'soundIds', 'speedSource', 'treasureIds', 'treasureObjectRecords') `
        -functionInputs @('Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-enter-past' 'Import-EnterPastData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'globalFlagSource', 'npcRows', 'ralphScriptSource', 'speedMatch', 'speedSource') `
        -functionInputs @('Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-graveyard-story' 'Import-GraveyardStoryData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'mainObjectSource', 'npcRows', 'speedSource') `
        -functionInputs @('Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-impa' 'Import-ImpaCutsceneData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'gfxNames', 'interactionGraphics', 'mainObjectLines', 'npcRows', 'paletteDataSource', 'speedSource') `
        -functionInputs @('Export-PaletteBlock', 'Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable') `
        -functionOutputs @('Resolve-ObjectSpeed', 'Resolve-SoundConstant')
    New-ImportStageContract 'cutscene-vocabulary' 'Import-CutsceneVocabulary.ps1' `
        -outputs @('cutsceneCommandSchemas') `
        -functionInputs @('Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-nayru' 'Import-NayruCutsceneData.ps1' `
        -inputs @('nayruCutsceneSource', 'nayruScriptSource', 'speedSource') `
        -functionInputs @('ConvertTo-CutsceneCommandRows', 'Get-AssemblySourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-black-tower' 'Import-BlackTowerData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'interactionGraphics') `
        -outputs @('blackTowerCutsceneSource', 'musicConstantSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'Export-PaletteBlock', 'Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-maku-rescue' 'Import-MakuRescueData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'paletteHeaderSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'Export-PaletteBlock', 'Get-AssemblySourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-dungeon-story' 'Import-DungeonStoryData.ps1' `
        -inputs @('allTexts', 'blackTowerCutsceneSource', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'treasureIds') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-harp-story' 'Import-HarpStoryData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'objectGfxSource', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-trade-dialogue' 'Import-TradeDialogueData.ps1' `
        -inputs @('allTexts', 'cutsceneCommandHeader', 'mainObjectSource', 'treasureObjectRecords') `
        -outputs @('musicSource', 'roomFlagSource', 'tradeItemSource') `
        -functionInputs @('New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-npc-scripts' 'Import-NpcScriptData.ps1' `
        -inputs @('allTexts', 'cutsceneCommandHeader', 'mainObjectSource', 'roomFlagSource', 'tradeItemSource', 'treasureObjectRecords') `
        -functionInputs @('ConvertTo-CutsceneCommandRows', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-trade-quest' 'Import-TradeQuestData.ps1' `
        -inputs @('allTextFallthroughIds', 'allTextPositions', 'allTexts', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'musicSource', 'roomFlagSource', 'tradeItemSource', 'treasureObjectRecords') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'ConvertTo-CutsceneCommandRows', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-goron-cave' 'Import-GoronCaveData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'gfxNames', 'interactionGraphics', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-ralph-quests' 'Import-RalphQuestData.ps1' `
        -inputs @('allTexts', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'musicConstantSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Resolve-SoundConstant', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-rafton' 'Import-RaftonData.ps1' `
        -inputs @('allTextFallthroughIds', 'allTexts', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'roomFlagSource', 'speedSource', 'tradeItemSource', 'treasureObjectRecords') `
        -outputs @('raftObjectSource') `
        -functionInputs @('New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Resolve-SoundConstant', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-tokay-theft' 'Import-TokayTheftData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'raftObjectSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-tokay-cook' 'Import-TokayCookData.ps1' `
        -inputs @('allTexts', 'allTextPositions') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-shooting-gallery' 'Import-ShootingGalleryData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'treasureIds') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-companions' 'Import-CompanionData.ps1' `
        -inputs @('allTextFallthroughIds', 'allTexts', 'cutsceneCommandHeader', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'mainObjectSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'Get-AssemblySourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-carpenters' 'Import-CarpenterData.ps1' `
        -inputs @('allTexts', 'globalFlagValues') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Resolve-SoundConstant', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-mamamu' 'Import-MamamuData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'globalFlagValues', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-plen' 'Import-PlenData.ps1' `
        -inputs @('allTexts', 'globalFlagValues') `
        -functionInputs @('New-CutsceneCommandRow', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-symmetry' 'Import-SymmetryData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'makuStopSound', 'soundIds', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-patch' 'Import-PatchData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'soundIds', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-bomb-upgrade-fairy' 'Import-BombUpgradeFairyData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'soundIds') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Read-PaletteBytes', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-defeated-moblin' 'Import-DefeatedMoblin.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'gfxNames', 'interactionGraphics', 'treasureIds', 'mainObjectSource') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-validation' 'Validate-CutsceneData.ps1' `
        -inputs @('cutsceneCommandSchemas', 'generatedCutsceneCommandStreams') `
        -functionInputs @('Test-GeneratedCutsceneCommandStreams')
    New-ImportStageContract 'enemies' 'Import-EnemyData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'gfxNames', 'paletteHeaderSource') `
        -outputs @(
            'crowRows', 'gelInstanceCount', 'keeseInstanceCount',
            'octorokInstanceCount', 'orderedObjectRows', 'partAnimationSource',
            'partDataSource', 'partOamSource', 'stalfosInstanceCount',
            'zolInstanceCount') `
        -functionInputs @('Read-PaletteBytes') `
        -functionOutputs @(
            'Copy-EnemySprite', 'Get-EnemyDefinition', 'Get-EnemySpriteSourceGrayscaleInverted', 'Resolve-Oam')
    New-ImportStageContract 'volcano' 'Import-VolcanoData.ps1' `
        -inputs @('mainObjectLines', 'globalFlagValues', 'soundIds', 'partOamSource') `
        -functionInputs @('Copy-EnemySprite', 'Resolve-Oam')
    New-ImportStageContract 'falling-boulders' 'Import-FallingBoulderData.ps1' `
        -inputs @('gfxNames', 'partOamSource') `
        -functionInputs @('Copy-EnemySprite', 'Get-EnemySpriteSourceGrayscaleInverted', 'Resolve-Oam')
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
            'Get-EnemySpriteSourceGrayscaleInverted', 'Read-PaletteBytes', 'Resolve-Oam')
    New-ImportStageContract 'wing-dungeon' 'Import-WingDungeon.ps1' `
        -inputs @('allTexts', 'mainObjectSource')
    New-ImportStageContract 'skull-dungeon' 'Import-SkullDungeon.ps1' `
        -inputs @('mainObjectSource', 'allTexts', 'allTextPositions')
    New-ImportStageContract 'moving-platforms' 'Import-MovingPlatforms.ps1'
    New-ImportStageContract 'crown-dungeon' 'Import-CrownDungeon.ps1' `
        -inputs @('allTexts', 'allTextPositions')
    New-ImportStageContract 'pushblock-synchronizer' 'Import-PushblockSynchronizer.ps1'
    New-ImportStageContract 'puzzle-trap-reset' 'Import-PuzzleTrapReset.ps1'
    New-ImportStageContract 'link-squish' 'Import-LinkSquish.ps1' `
        -functionInputs @('Read-HexBytes')
    New-ImportStageContract 'static-dungeon-objects' 'Import-StaticDungeonObjects.ps1'
    New-ImportStageContract 'part-switch' 'Import-PartSwitch.ps1'
    New-ImportStageContract 'seed-shooter-eye-statue' 'Import-SeedShooterEyeStatue.ps1'
    New-ImportStageContract 'armos-warrior' 'Import-ArmosWarrior.ps1' `
        -inputs @('allTexts', 'allTextPositions')
    New-ImportStageContract 'eyesoar' 'Import-Eyesoar.ps1'
    New-ImportStageContract 'king-moblin' 'Import-KingMoblin.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'gfxNames', 'globalFlagValues') `
        -functionInputs @('Get-EnemyDefinition', 'Copy-EnemySprite', 'Read-PaletteBytes')
    New-ImportStageContract 'navigation' 'Import-WorldNavigation.ps1' `
        -functionInputs @('Expand-TransitionGraphics')
    New-ImportStageContract 'pirate-ship' 'Import-PirateShip.ps1'
    New-ImportStageContract 'audio' 'Import-AudioData.ps1' `
        -inputs @('globalFlagValues')
    New-ImportStageContract 'inventory-icons' 'Import-InventoryIcons.ps1' `
        -functionInputs @('Expand-TransitionGraphics')
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
    'Read-ImportLines', 'Read-ImportText', 'Select-CleanUsAssemblyLines',
    'Resolve-AssemblySourceTextPath', 'Write-GeneratedBytes',
    'Write-GeneratedTable')
. (Join-Path $importModuleRoot 'Invoke-ImportStage.ps1')

$importSucceeded = $false
$assemblySourceStats = ''
$assemblySourceHost = $null
$importValues = @{
    importRoot = $importRoot
    Disassembly = $Disassembly
    Rom = $Rom
    OutputDirectory = $OutputDirectory
    SkipBuild = $SkipBuild
}
$importFunctions = @{}
$stageModules = [Collections.Generic.List[Management.Automation.PSModuleInfo]]::new()
$initializeContract = New-ImportStageContract 'initialize' 'Initialize-Import.ps1' `
    -inputs @('importRoot', 'Disassembly', 'Rom', 'OutputDirectory', 'SkipBuild') `
    -outputs @('destination', 'romBytes', 'hash', 'assemblySourceHost') `
    -functionOutputs (@($commonStageFunctionInputs) + @('Invoke-AssemblySourceHost'))
try {
    foreach ($contract in @($initializeContract) + @($stageContracts)) {
        $arguments = @{
            Contract = $contract
            StageRoot = $importModuleRoot
            Values = $importValues
            Functions = $importFunctions
        }
        if ($contract -ne $initializeContract) {
            $arguments.CommonInputs = $commonStageInputs
            $arguments.CommonFunctions = $commonStageFunctionInputs
        }
        $stage = Invoke-ImportStage @arguments
        $stageModules.Add($stage)
        foreach ($output in $stage.ExportedVariables.GetEnumerator()) {
            $importValues[$output.Key] = $output.Value.Value
        }
        foreach ($output in $stage.ExportedFunctions.GetEnumerator()) {
            $importFunctions[$output.Key] = $output.Value.ScriptBlock
        }
        if ($contract -eq $initializeContract) {
            $assemblySourceHost = $importValues.assemblySourceHost
        }
    }
    $assemblySourceStats = & $importFunctions['Invoke-AssemblySourceHost'] `
        $assemblySourceHost 'ASSERT'
    $importSucceeded = $true
}
finally {
    if ($null -ne $assemblySourceHost) {
        try {
            [void](& $importFunctions['Invoke-AssemblySourceHost'] $assemblySourceHost 'QUIT')
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
    foreach ($stage in $stageModules) {
        Remove-Module -ModuleInfo $stage -Force
    }
}

Write-Host "Validated clean US ROM: $($importValues.hash)"
& {
    $assemblySourceParts = $assemblySourceStats.Split("`t")
    Write-Host (
    "Parsed $($assemblySourceParts[0]) assembly sources with " +
    "$($assemblySourceParts[1]) physical reads and " +
    "$($assemblySourceParts[2]) indexed label-block / " +
        "$($assemblySourceParts[3]) structured-node queries.")
}
Write-Host "Imported $($importValues.tilesets.Count) tilesets, 1536 rooms, 42 signs, $($importValues.npcRows.Count - 1) NPCs, $($importValues.dungeonMechanicRows.Count - 1) dungeon mechanic placements, $($importValues.dungeonSharedPlacementRows.Count - 1) shared dungeon-entry placements, $($importValues.keeseInstanceCount) Keese, $($importValues.crowRows.Count - 1) fixed Crows, $($importValues.octorokInstanceCount) Octoroks, $($importValues.stalfosInstanceCount) ordinary Stalfos, $($importValues.zolInstanceCount) Zols, $($importValues.gelInstanceCount) direct Gels, $($importValues.orderedObjectRows.Count - 1) ordered placement records, $($importValues.enemyUnspawnableTileCount) enemy-unspawnable tile records, 133 chests, 529 tile/edge warps, 2 dive-interaction warps, 22 animation groups, and 223 sound IDs into $($importValues.destination)"
