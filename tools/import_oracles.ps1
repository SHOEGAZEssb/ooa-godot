param(
    [string]$Disassembly = (Join-Path $PSScriptRoot '..\external\oracles-disasm'),
    [string]$Rom,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\assets\oracle'),
    [switch]$SkipBuild,
    [switch]$Force,
    [switch]$Profile
)

$ErrorActionPreference = "Stop"
$importTimer = [Diagnostics.Stopwatch]::StartNew()
$importRoot = $PSScriptRoot
$importModuleRoot = Join-Path $importRoot 'import_oracles'
. (Join-Path $importRoot 'Resolve-OracleRom.ps1')
$Rom = Resolve-OracleRom -Rom $Rom

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

. (Join-Path $importModuleRoot 'Ages/SourceConfiguration.ps1')

$stageContracts = @(
    New-ImportStageContract 'vanilla-tilesets' 'Ages/Import-VanillaTilesets.ps1' `
        -functionOutputs @('Expand-TransitionGraphics')
    New-ImportStageContract 'world' 'Ages/Import-WorldAssets.ps1' `
        -outputs @(
            'globalFlagValues', 'singleTileChangeRecords', 'tilesets',
            'paletteHeaderSource', 'paletteDataSource', 'tilesetRecordSize', 'tilesetMetadata')
    New-ImportStageContract 'suction-pits' 'Ages/Import-SuctionPitData.ps1'
    New-ImportStageContract 'save-initialization' 'Ages/Import-SaveInitializationData.ps1'
    New-ImportStageContract 'menus' 'Ages/Import-MenuAssets.ps1' `
        -inputs @('paletteDataSource') `
        -outputs @('textYaml') `
        -functionOutputs @(
            'Export-PaletteBlock', 'Read-PaletteBytes', 'Normalize-DialogueText')
    New-ImportStageContract `
        'menu-presentation' 'Ages/Import-MenuPresentationData.ps1'
    New-ImportStageContract 'dialogue' 'Ages/Import-DialogueAndIntro.ps1' `
        -inputs @('textYaml') `
        -outputs @(
            'npcInteractionIds', 'allTexts', 'allTextPositions', 'allTextIdsByName',
            'allTextFallthroughIds', 'objectGfxHeaderSource') `
        -functionInputs @('Normalize-DialogueText')
    New-ImportStageContract 'map-and-items' 'Ages/Import-MapAndItemData.ps1' `
        -inputs @('allTextIdsByName', 'allTextPositions', 'allTexts', 'allTextFallthroughIds') `
        -outputs @(
            'enemyUnspawnableTileCount', 'soundIds', 'treasureIds',
            'treasureObjectRecords', 'treasureObjectSource') `
        -functionOutputs @('Read-HexBytes')
    New-ImportStageContract 'npcs' 'Ages/Import-NpcData.ps1' `
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
            'partAnimationDefinitions', 'partAnimationTables', 'partOamBlocks', 'partOamPointerTables',
            'treasureObjectSource') `
        -functionInputs @('Export-PaletteBlock', 'Get-ImportSpriteSourceGrayscaleInverted') `
        -functionOutputs @('Resolve-NpcAnimation', 'Resolve-PartAnimation', 'Resolve-OceanText', 'Resolve-InteractionGraphics')
    New-ImportStageContract 'collapsing-floor' 'Ages/Import-CollapsingFloor.ps1' `
        -inputs @('mainObjectLines', 'soundIds')
    New-ImportStageContract 'gasha' 'Ages/Import-GashaData.ps1' `
        -inputs @(
            'allTexts', 'gfxNames', 'interactionAnimationSource',
            'interactionGraphics', 'mainObjectSource',
            'npcAnimationDefinitions', 'npcAnimationTables', 'npcOamBlocks')
    New-ImportStageContract 'cutscene-commands' 'Shared/Import-CutsceneData.ps1' `
        -inputs @('assemblySourceHost') `
        -outputs @('cutsceneCommandHeader', 'generatedCutsceneCommandStreams') `
        -functionInputs @('Invoke-AssemblySourceHost') `
        -functionOutputs @('ConvertTo-CutsceneCommandPayload', 'Find-CutsceneCommandSourceLine', 'Get-AssemblySourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Test-GeneratedCutsceneCommandStreams', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-normalization' 'Shared/Convert-CutsceneCommands.ps1' `
        -inputs @('cutsceneCommandHeader') `
        -functionInputs @('New-CutsceneCommandRow') `
        -functionOutputs @('ConvertTo-CutsceneCommandRows', 'ConvertTo-NpcProgramRows')
    New-ImportStageContract 'cutscene-portals' 'Ages/Import-TimePortalData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'interactionAnimationSource', 'interactionGraphics', 'mainObjectLines', 'npcAnimationTables', 'soundIds', 'treasureIds') `
        -functionInputs @('Read-PaletteBytes', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'water-pushblocks' 'Ages/Import-WaterPushblockData.ps1' `
        -inputs @('gfxNames', 'interactionGraphics', 'soundIds') `
        -functionInputs @('Resolve-NpcAnimation')
    New-ImportStageContract 'cutscene-maku-tree' 'Ages/Import-MakuTreeData.ps1' `
        -inputs @('allTextFallthroughIds', 'allTextPositions', 'allTexts', 'gfxNames', 'interactionGraphics', 'paletteDataSource', 'paletteHeaderSource', 'treasureObjectRecords', 'treasureObjectSource') `
        -outputs @('makuStopSound', 'objectGfxSource') `
        -functionInputs @('Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-ralph-portal' 'Ages/Import-RalphPortalData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'npcRows') `
        -outputs @('globalFlagSource', 'ralphScriptSource', 'speedMatch', 'speedSource') `
        -functionInputs @('Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-deku-forest' 'Ages/Import-DekuForestData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'cutsceneCommandHeader', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'mainObjectSource', 'paletteDataSource', 'soundIds', 'speedSource', 'treasureIds', 'treasureObjectRecords') `
        -functionInputs @('ConvertTo-CutsceneCommandRows', 'Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'palace' 'Ages/Import-PalaceData.ps1' `
        -inputs @('allTextFallthroughIds', 'allTextPositions', 'allTexts', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'speedSource') `
        -functionInputs @('ConvertTo-CutsceneCommandRows', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable') `
        -functionOutputs @('Resolve-PalaceText')
    New-ImportStageContract 'cutscene-enter-past' 'Ages/Import-EnterPastData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'globalFlagSource', 'npcRows', 'ralphScriptSource', 'speedMatch', 'speedSource') `
        -functionInputs @('Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-graveyard-story' 'Ages/Import-GraveyardStoryData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'mainObjectSource', 'npcRows', 'speedSource') `
        -functionInputs @('Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-impa' 'Ages/Import-ImpaCutsceneData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'gfxNames', 'interactionGraphics', 'mainObjectLines', 'npcRows', 'paletteDataSource', 'speedSource') `
        -functionInputs @('Export-PaletteBlock', 'Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable') `
        -functionOutputs @('Resolve-ObjectSpeed', 'Resolve-SoundConstant')
    New-ImportStageContract 'zelda-rescue' 'Ages/Import-ZeldaRescue.ps1' `
        -inputs @('allTexts', 'globalFlagValues', 'gfxNames', 'interactionGraphics', 'cutsceneCommandHeader') `
        -functionInputs @('ConvertTo-CutsceneCommandRows', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-PalaceText', 'Resolve-SoundConstant', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-vocabulary' 'Shared/Import-CutsceneVocabulary.ps1' `
        -outputs @('cutsceneCommandSchemas') `
        -functionInputs @('Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-nayru' 'Ages/Import-NayruCutsceneData.ps1' `
        -inputs @('nayruCutsceneSource', 'nayruScriptSource', 'speedSource', 'allTexts') `
        -functionInputs @('ConvertTo-CutsceneCommandRows', 'Get-AssemblySourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-black-tower' 'Ages/Import-BlackTowerData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'interactionGraphics') `
        -outputs @('blackTowerCutsceneSource', 'musicConstantSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'Export-PaletteBlock', 'Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-maku-rescue' 'Ages/Import-MakuRescueData.ps1' `
        -inputs @('allTextPositions', 'allTexts', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'paletteHeaderSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'Export-PaletteBlock', 'Get-AssemblySourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-dungeon-story' 'Ages/Import-DungeonStoryData.ps1' `
        -inputs @('allTexts', 'blackTowerCutsceneSource', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'treasureIds') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-post-d7' 'Ages/Import-PostD7Data.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'gfxNames', 'interactionGraphics', 'cutsceneCommandHeader') `
        -functionInputs @('ConvertTo-CutsceneCommandRows', 'Resolve-NpcAnimation', 'Resolve-OceanText', 'Resolve-ObjectSpeed', 'Read-AssemblyCutsceneCommands', 'New-CutsceneCommandRow', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-harp-story' 'Ages/Import-HarpStoryData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'objectGfxSource', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-trade-dialogue' 'Ages/Import-TradeDialogueData.ps1' `
        -inputs @('allTexts', 'cutsceneCommandHeader', 'mainObjectSource', 'treasureObjectRecords') `
        -outputs @('musicSource', 'roomFlagSource', 'tradeItemSource') `
        -functionInputs @('New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-OceanText', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-npc-scripts' 'Ages/Import-NpcScriptData.ps1' `
        -inputs @('allTexts', 'cutsceneCommandHeader', 'mainObjectSource', 'roomFlagSource', 'tradeItemSource', 'treasureObjectRecords') `
        -functionInputs @('ConvertTo-CutsceneCommandRows', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-OceanText', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'family-scripts' 'Ages/Import-FamilyScriptData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'treasureIds') `
        -functionInputs @('ConvertTo-NpcProgramRows', 'Write-CutsceneGeneratedTable', 'Read-AssemblyCutsceneCommands', 'Read-ImportText', 'Resolve-OceanText', 'Resolve-NpcAnimation')
    New-ImportStageContract 'farore' 'Ages/Import-FaroreData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'interactionGraphics', 'gfxNames') `
        -functionInputs @('ConvertTo-NpcProgramRows', 'Write-CutsceneGeneratedTable', 'Read-AssemblyCutsceneCommands', 'Read-ImportText', 'Resolve-OceanText', 'Resolve-NpcAnimation', 'Find-ImportSpriteSource', 'Resolve-InteractionGraphics')
    New-ImportStageContract 'shared-npcs' 'Ages/Import-SharedNpcData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'cutsceneCommandHeader') `
        -functionInputs @('ConvertTo-CutsceneCommandRows', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-OceanText', 'Resolve-ObjectSpeed', 'Resolve-SoundConstant', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-ocean' 'Ages/Import-OceanData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'cutsceneCommandHeader', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'mainObjectSource', 'speedSource', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-OceanText', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'library' 'Ages/Import-LibraryData.ps1' `
        -inputs @('allTexts', 'cutsceneCommandHeader', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'mainObjectSource', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-OceanText', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'sea-fairy' 'Ages/Import-SeaFairyData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'cutsceneCommandHeader', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'mainObjectSource', 'partAnimationDefinitions', 'partAnimationTables', 'partOamBlocks', 'partOamPointerTables') `
        -functionInputs @('New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-PartAnimation', 'Resolve-OceanText', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-trade-quest' 'Ages/Import-TradeQuestData.ps1' `
        -inputs @('allTextFallthroughIds', 'allTextPositions', 'allTexts', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'musicSource', 'roomFlagSource', 'tradeItemSource', 'treasureObjectRecords') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'ConvertTo-CutsceneCommandRows', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-goron-cave' 'Ages/Import-GoronCaveData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'gfxNames', 'interactionGraphics', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-ralph-quests' 'Ages/Import-RalphQuestData.ps1' `
        -inputs @('allTexts', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'musicConstantSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Resolve-SoundConstant', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-rafton' 'Ages/Import-RaftonData.ps1' `
        -inputs @('allTextFallthroughIds', 'allTexts', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'roomFlagSource', 'speedSource', 'tradeItemSource', 'treasureObjectRecords') `
        -outputs @('raftObjectSource') `
        -functionInputs @('New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Resolve-SoundConstant', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-tokay-theft' 'Ages/Import-TokayTheftData.ps1' `
        -inputs @('allTexts', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'raftObjectSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'ConvertTo-CutsceneCommandRows', 'Read-AssemblyCutsceneCommands', 'Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Resolve-OceanText', 'Resolve-ObjectSpeed', 'Resolve-SoundConstant', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-tokay-cook' 'Ages/Import-TokayCookData.ps1' `
        -inputs @('allTexts', 'allTextPositions') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-tokay-npcs' 'Ages/Import-TokayNpcData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'cutsceneCommandHeader', 'globalFlagValues') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Resolve-OceanText', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'interaction-script-yields' 'Ages/Import-InteractionScriptYieldsData.ps1' `
        -inputs @('soundIds') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-shooting-gallery' 'Ages/Import-ShootingGalleryData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'interactionGraphics', 'mainObjectSource', 'treasureIds') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'Find-CutsceneCommandSourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-companions' 'Ages/Import-CompanionData.ps1' `
        -inputs @('allTextFallthroughIds', 'allTexts', 'cutsceneCommandHeader', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'mainObjectSource') `
        -functionInputs @('ConvertTo-CutsceneCommandPayload', 'Get-AssemblySourceLine', 'New-CutsceneCommandRow', 'Read-AssemblyCutsceneCommands', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-carpenters' 'Ages/Import-CarpenterData.ps1' `
        -inputs @('allTexts', 'globalFlagValues') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Resolve-SoundConstant', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-mamamu' 'Ages/Import-MamamuData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'globalFlagValues', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-forest-fairy-secret' 'Ages/Import-ForestFairySecretData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'cutsceneCommandHeader', 'globalFlagValues') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'New-CutsceneCommandRow', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-plen' 'Ages/Import-PlenData.ps1' `
        -inputs @('allTexts', 'globalFlagValues') `
        -functionInputs @('New-CutsceneCommandRow', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-symmetry' 'Ages/Import-SymmetryData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'makuStopSound', 'soundIds', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-patch' 'Ages/Import-PatchData.ps1' `
        -inputs @('allTexts', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'soundIds', 'treasureObjectRecords') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-bomb-upgrade-fairy' 'Ages/Import-BombUpgradeFairyData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'gfxNames', 'globalFlagValues', 'interactionGraphics', 'soundIds') `
        -functionInputs @('New-CutsceneCommandRow', 'Resolve-NpcAnimation', 'Read-PaletteBytes', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-defeated-moblin' 'Ages/Import-DefeatedMoblin.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'gfxNames', 'interactionGraphics', 'treasureIds', 'mainObjectSource') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows', 'Resolve-NpcAnimation', 'Resolve-ObjectSpeed', 'Write-CutsceneGeneratedTable')
    New-ImportStageContract 'cutscene-validation' 'Ages/Validate-CutsceneData.ps1' `
        -inputs @('cutsceneCommandSchemas', 'generatedCutsceneCommandStreams') `
        -functionInputs @('Test-GeneratedCutsceneCommandStreams')
    New-ImportStageContract 'enemies' 'Ages/Import-EnemyData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'gfxNames', 'paletteHeaderSource') `
        -outputs @(
            'crowRows', 'gelInstanceCount', 'keeseInstanceCount',
            'octorokInstanceCount', 'orderedObjectRows', 'partAnimationSource',
            'partDataSource', 'partOamSource', 'stalfosInstanceCount',
            'zolInstanceCount') `
        -functionInputs @('Read-PaletteBytes', 'Find-ImportSpriteSource', 'Get-ImportSpriteSourceGrayscaleInverted') `
        -functionOutputs @(
            'Copy-EnemySprite', 'Get-EnemyDefinition', 'Get-EnemySpriteSourceGrayscaleInverted', 'Resolve-Oam')
    New-ImportStageContract 'volcano' 'Ages/Import-VolcanoData.ps1' `
        -inputs @('mainObjectLines', 'globalFlagValues', 'soundIds', 'partOamSource') `
        -functionInputs @('Copy-EnemySprite', 'Resolve-Oam')
    New-ImportStageContract 'falling-boulders' 'Ages/Import-FallingBoulderData.ps1' `
        -inputs @('gfxNames', 'partOamSource') `
        -functionInputs @('Copy-EnemySprite', 'Get-EnemySpriteSourceGrayscaleInverted', 'Resolve-Oam')
    New-ImportStageContract 'seed-trees' 'Ages/Import-SeedTreeData.ps1' `
        -inputs @(
            'allTexts', 'gfxNames', 'mainObjectLines', 'partAnimationSource',
            'partDataSource', 'partOamSource') `
        -functionInputs @(
            'Copy-EnemySprite', 'Get-AssemblyLabelBody', 'Resolve-Oam')
    New-ImportStageContract 'maple' 'Ages/Import-MapleData.ps1' `
        -inputs @(
            'allTexts', 'gfxNames', 'interactionGraphics',
            'partAnimationSource', 'partOamSource') `
        -functionInputs @(
            'Get-AssemblyLabelBody', 'Resolve-NpcAnimation', 'Resolve-Oam')
    New-ImportStageContract 'spirits-grave' 'Ages/Import-SpiritsGrave.ps1' `
        -inputs @(
            'allTexts', 'gfxNames', 'interactionAnimationSource',
            'interactionGraphics', 'mainObjectSource', 'npcAnimationTables',
            'npcOamBlocks', 'npcOamPointerTables', 'paletteHeaderSource',
            'partAnimationSource', 'partOamSource') `
        -functionInputs @(
            'Copy-EnemySprite', 'Get-AssemblyLabelBody', 'Get-EnemyDefinition',
            'Get-EnemySpriteSourceGrayscaleInverted', 'Read-PaletteBytes', 'Resolve-Oam')
    New-ImportStageContract 'wing-dungeon' 'Ages/Import-WingDungeon.ps1' `
        -inputs @('allTexts', 'mainObjectSource')
    New-ImportStageContract 'skull-dungeon' 'Ages/Import-SkullDungeon.ps1' `
        -inputs @('mainObjectSource', 'allTexts', 'allTextPositions')
    New-ImportStageContract 'moving-platforms' 'Ages/Import-MovingPlatforms.ps1'
    New-ImportStageContract 'crown-dungeon' 'Ages/Import-CrownDungeon.ps1' `
        -inputs @('allTexts', 'allTextPositions')
    New-ImportStageContract 'mermaid-dungeon' 'Ages/Import-MermaidDungeon.ps1' `
        -inputs @('allTexts', 'allTextPositions')
    New-ImportStageContract 'jabu-dungeon' 'Ages/Import-JabuDungeon.ps1' `
        -inputs @('allTexts', 'allTextPositions')
    New-ImportStageContract 'ancient-tomb' 'Ages/Import-AncientTomb.ps1' `
        -inputs @('allTexts', 'allTextPositions')
    New-ImportStageContract 'heros-cave' 'Ages/Import-HerosCave.ps1'
    New-ImportStageContract 'shared-interactions' 'Ages/Import-SharedInteractions.ps1' `
        -inputs @('soundIds', 'gfxNames', 'interactionGraphics', 'paletteHeaderSource', 'allTexts', 'allTextPositions') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows', 'Write-CutsceneGeneratedTable',
            'Resolve-NpcAnimation', 'Copy-EnemySprite', 'Get-EnemySpriteSourceGrayscaleInverted', 'Read-PaletteBytes')
    New-ImportStageContract 'monkeys' 'Ages/Import-Monkeys.ps1' `
        -inputs @('gfxNames', 'allTexts') `
        -functionInputs @('Resolve-InteractionGraphics', 'Resolve-NpcAnimation', 'Copy-EnemySprite',
            'Read-AssemblyCutsceneCommands', 'ConvertTo-NpcProgramRows', 'Write-CutsceneGeneratedTable', 'Resolve-OceanText')
    New-ImportStageContract 'farore-memory' 'Ages/Import-FaroreMemory.ps1' `
        -inputs @('gfxNames', 'allTexts', 'globalFlagValues') `
        -functionInputs @('Read-AssemblyCutsceneCommands', 'ConvertTo-NpcProgramRows', 'Write-CutsceneGeneratedTable',
            'Resolve-OceanText', 'Resolve-InteractionGraphics', 'Resolve-NpcAnimation', 'Copy-EnemySprite', 'Copy-GeneratedFile', 'Read-PaletteBytes')
    New-ImportStageContract 'octogon-effects' 'Ages/Import-OctogonEffects.ps1' `
        -inputs @('gfxNames','interactionGraphics') `
        -functionInputs @('Resolve-NpcAnimation','Copy-EnemySprite','Get-EnemySpriteSourceGrayscaleInverted')
    New-ImportStageContract 'pushblock-synchronizer' 'Ages/Import-PushblockSynchronizer.ps1'
    New-ImportStageContract 'puzzle-trap-reset' 'Ages/Import-PuzzleTrapReset.ps1'
    New-ImportStageContract 'link-squish' 'Ages/Import-LinkSquish.ps1' `
        -functionInputs @('Read-HexBytes')
    New-ImportStageContract 'static-dungeon-objects' 'Ages/Import-StaticDungeonObjects.ps1'
    New-ImportStageContract 'part-switch' 'Ages/Import-PartSwitch.ps1'
    New-ImportStageContract 'seed-shooter-eye-statue' 'Ages/Import-SeedShooterEyeStatue.ps1'
    New-ImportStageContract 'armos-warrior' 'Ages/Import-ArmosWarrior.ps1' `
        -inputs @('allTexts', 'allTextPositions')
    New-ImportStageContract 'eyesoar' 'Ages/Import-Eyesoar.ps1'
    New-ImportStageContract 'king-moblin' 'Ages/Import-KingMoblin.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'gfxNames', 'globalFlagValues') `
        -functionInputs @('Get-EnemyDefinition', 'Copy-EnemySprite', 'Read-PaletteBytes')
    New-ImportStageContract 'palace-possession' 'Ages/Import-PalacePossession.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'soundIds') `
        -functionInputs @('Get-EnemyDefinition', 'Copy-EnemySprite', 'Read-PaletteBytes', 'Resolve-NpcAnimation', 'Resolve-PalaceText', 'Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows')
    New-ImportStageContract 'nayru-rescue-warp' 'Ages/Import-NayruRescueWarp.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'soundIds') `
        -functionInputs @('Copy-EnemySprite', 'Resolve-NpcAnimation', 'Resolve-InteractionGraphics', 'Resolve-PalaceText', 'Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows')
    New-ImportStageContract 'maku-seed' 'Ages/Import-MakuSeedData.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'allTextFallthroughIds', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'soundIds') `
        -functionInputs @('Copy-EnemySprite', 'Resolve-NpcAnimation', 'Resolve-InteractionGraphics', 'Resolve-PalaceText', 'Read-PaletteBytes', `
            'Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows', 'New-CutsceneCommandRow')
    New-ImportStageContract 'navigation' 'Ages/Import-WorldNavigation.ps1' `
        -functionInputs @('Expand-TransitionGraphics')
    New-ImportStageContract 'room-of-rites' 'Ages/Import-RoomOfRites.ps1' `
        -inputs @('gfxNames','interactionGraphics') `
        -functionInputs @('Copy-EnemySprite','Resolve-NpcAnimation','Get-EnemySpriteSourceGrayscaleInverted','Read-PaletteBytes')
    New-ImportStageContract 'black-tower-seed' 'Ages/Import-BlackTowerSeed.ps1' `
        -inputs @('gfxNames', 'interactionGraphics') `
        -functionInputs @('Copy-EnemySprite', 'Resolve-NpcAnimation', 'Read-PaletteBytes')
    New-ImportStageContract 'black-tower-confrontation' 'Ages/Import-BlackTowerConfrontation.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'musicConstantSource') `
        -functionInputs @('Copy-EnemySprite', 'Resolve-NpcAnimation', 'Resolve-SoundConstant', 'Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows')
    New-ImportStageContract 'black-tower-flames' 'Ages/Import-BlackTowerFlames.ps1' `
        -inputs @('partOamSource') `
        -functionInputs @('Copy-EnemySprite', 'Get-EnemySpriteSourceGrayscaleInverted', 'Resolve-Oam')
    New-ImportStageContract 'placed-parts' 'Ages/Import-PlacedParts.ps1' `
        -inputs @('partOamSource', 'gfxNames') `
        -functionInputs @('Copy-EnemySprite', 'Get-EnemySpriteSourceGrayscaleInverted', 'Resolve-Oam')
    New-ImportStageContract 'black-tower-possession' 'Ages/Import-BlackTowerPossession.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics') `
        -functionInputs @('Copy-EnemySprite', 'Resolve-NpcAnimation', 'Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows')
    New-ImportStageContract 'veran-fairy' 'Ages/Import-VeranFairy.ps1' `
        -inputs @('allTexts','allTextPositions','allTextFallthroughIds','gfxNames') `
        -functionInputs @('Get-EnemyDefinition','Copy-EnemySprite','Read-PaletteBytes','Resolve-PalaceText')
    New-ImportStageContract 'black-tower-escape-attempt' 'Ages/Import-BlackTowerEscapeAttempt.ps1' `
        -inputs @('gfxNames','interactionGraphics') `
        -functionInputs @('Copy-EnemySprite','Resolve-NpcAnimation')
    New-ImportStageContract 'veran-face' 'Ages/Import-VeranFace.ps1' `
        -inputs @('allTexts','allTextPositions','allTextFallthroughIds','cutsceneCommandHeader','musicConstantSource') `
        -functionInputs @('Read-AssemblyCutsceneCommands','ConvertTo-CutsceneCommandRows','Read-PaletteBytes','Resolve-PalaceText','Resolve-SoundConstant')
    New-ImportStageContract 'black-tower-ending' 'Ages/Import-BlackTowerEnding.ps1' `
        -inputs @('allTexts','allTextPositions','allTextFallthroughIds','gfxNames','interactionGraphics','soundIds') `
        -functionInputs @('Read-AssemblyMacroInvocations','Read-AssemblyDataDirectives','Copy-EnemySprite','Resolve-NpcAnimation','Resolve-InteractionGraphics','Resolve-PalaceText','Read-AssemblyCutsceneCommands','ConvertTo-CutsceneCommandRows')
    New-ImportStageContract 'veran-final' 'Ages/Import-VeranFinal.ps1' `
        -inputs @('allTexts','allTextPositions','allTextFallthroughIds','gfxNames') `
        -functionInputs @('Get-EnemyDefinition','Copy-EnemySprite','Read-PaletteBytes','Resolve-PalaceText')
    New-ImportStageContract 'twinrova' 'Ages/Import-Twinrova.ps1' `
        -inputs @('allTexts','allTextPositions','allTextFallthroughIds','gfxNames','interactionGraphics') `
        -functionInputs @('Get-EnemyDefinition','Copy-EnemySprite','Read-PaletteBytes','Resolve-PalaceText','Resolve-NpcAnimation','Get-EnemySpriteSourceGrayscaleInverted')
    New-ImportStageContract 'ganon' 'Ages/Import-Ganon.ps1' `
        -inputs @('allTexts','allTextPositions','allTextFallthroughIds','gfxNames') `
        -functionInputs @('Get-EnemyDefinition','Copy-EnemySprite','Read-PaletteBytes','Resolve-PalaceText','Get-EnemySpriteSourceGrayscaleInverted')
    New-ImportStageContract 'ending-screens' 'Ages/Import-EndingScreens.ps1' `
        -inputs @('allTexts','allTextPositions','allTextFallthroughIds') `
        -functionInputs @('Read-PaletteBytes','Resolve-PalaceText','Resolve-NpcAnimation')
    New-ImportStageContract 'pirate-ship' 'Ages/Import-PirateShip.ps1' `
        -inputs @('allTexts', 'allTextPositions', 'cutsceneCommandHeader', 'gfxNames', 'interactionGraphics', 'treasureObjectRecords', 'globalFlagValues') `
        -functionInputs @('Resolve-NpcAnimation', 'Resolve-OceanText', 'Read-AssemblyCutsceneCommands', 'ConvertTo-CutsceneCommandRows')
    New-ImportStageContract 'audio' 'Ages/Import-AudioData.ps1' `
        -inputs @('globalFlagValues')
    New-ImportStageContract 'inventory-icons' 'Ages/Import-InventoryIcons.ps1' `
        -functionInputs @('Expand-TransitionGraphics')
    New-ImportStageContract 'game-select' 'Seasons/Import-DinPreview.ps1' `
        -functionInputs @('Read-PaletteBytes', 'Get-ImportSpriteSourceGrayscaleInverted')
    New-ImportStageContract 'cpu-graphics' 'Shared/Import-CpuGraphics.ps1'
    New-ImportStageContract 'manifest' 'Shared/Write-GeneratedTableManifest.ps1'
)

$commonStageInputs = @('destination', 'previewDestination', 'Disassembly', 'romBytes')
$commonStageFunctionInputs = @(
    'Convert-AssemblyInteger', 'Copy-GeneratedFile', 'Get-AssemblyLabelBody',
    'Find-AssemblySourceLine', 'Read-AssemblyNodeRange',
    'Read-AssemblyAnimationDefinitions', 'Read-AssemblyConstants',
    'Read-AssemblyDataDirectives',
    'Read-AssemblyDwTables', 'Read-AssemblyLabelBlock',
    'Read-AssemblyLabelNodes', 'Read-AssemblyLabels',
    'Read-AssemblyMacroInvocations', 'Read-AssemblyNodes',
    'Read-AssemblyInstructions', 'Read-AssemblyLiteralValues',
    'Read-ImportLines', 'Read-ImportText', 'Select-CleanUsAssemblyLines',
    'Resolve-AssemblySourceTextPath', 'Write-GeneratedBytes', 'Write-GeneratedGrayscalePng',
    'Write-GeneratedTable')
. (Join-Path $importModuleRoot 'Shared/Invoke-ImportStage.ps1')

$importSucceeded = $false
$assemblySourceStats = ''
$assemblySourceHost = $null
$importCacheReused = $false
$importValues = @{
    importRoot = $importRoot
    Disassembly = $Disassembly
    Rom = $Rom
    OutputDirectory = $OutputDirectory
    SkipBuild = $SkipBuild
    assemblySymbols = $assemblySymbols
}
$importFunctions = @{}
$stageModules = [Collections.Generic.List[Management.Automation.PSModuleInfo]]::new()
$initializeContract = New-ImportStageContract 'initialize' 'Shared/Initialize-Import.ps1' `
    -inputs @('importRoot', 'Disassembly', 'Rom', 'OutputDirectory', 'SkipBuild', 'assemblySymbols') `
    -outputs @('assetRoot', 'destination', 'previewDestination', 'romBytes', 'hash', 'assemblySourceHost') `
    -functionOutputs (@($commonStageFunctionInputs) + @('Invoke-AssemblySourceHost', 'Find-ImportSpriteSource', 'Get-ImportSpriteSourceGrayscaleInverted'))
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
        $stageTimer = [Diagnostics.Stopwatch]::StartNew()
        $stage = Invoke-ImportStage @arguments
        $stageTimer.Stop()
        if ($Profile) {
            Write-Host ('Import stage {0}: {1:F3}s' -f $contract.Name, $stageTimer.Elapsed.TotalSeconds)
        }
        $stageModules.Add($stage)
        foreach ($output in $stage.ExportedVariables.GetEnumerator()) {
            $importValues[$output.Key] = $output.Value.Value
        }
        foreach ($output in $stage.ExportedFunctions.GetEnumerator()) {
            $importFunctions[$output.Key] = $output.Value.ScriptBlock
        }
        if ($contract -eq $initializeContract) {
            $assemblySourceHost = $importValues.assemblySourceHost
            $cacheTimer = [Diagnostics.Stopwatch]::StartNew()
            $cacheStatus = & $importFunctions['Invoke-AssemblySourceHost'] $assemblySourceHost 'CACHE_BEGIN' (
                "$importRoot`0$Rom`0$($importValues.assetRoot)`0$(if ($Force) { '1' } else { '0' })")
            $cacheFields = $cacheStatus.Split("`t", 2)
            if ($Profile) { Write-Host ('Import cache verification: {0:F3}s' -f $cacheTimer.Elapsed.TotalSeconds) }
            if ($cacheFields[0] -eq 'HIT') {
                $importCacheReused = $true
                Write-Host "Reused unchanged import after verifying all inputs and $($cacheFields[1]) generated assets."
                break
            }
            Write-Host "Generating complete import: $($cacheFields[1])."
        }
    }
    $assemblySourceStats = & $importFunctions['Invoke-AssemblySourceHost'] `
        $assemblySourceHost 'ASSERT'
    if (-not $importCacheReused) {
        $cacheTimer = [Diagnostics.Stopwatch]::StartNew()
        [void](& $importFunctions['Invoke-AssemblySourceHost'] $assemblySourceHost 'CACHE_COMPLETE')
        if ($Profile) { Write-Host ('Import cache publication: {0:F3}s' -f $cacheTimer.Elapsed.TotalSeconds) }
    }
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

Write-Host ('Import completed in {0:F3}s ({1}).' -f $importTimer.Elapsed.TotalSeconds,
    $(if ($importCacheReused) { 'verified cache reuse' } else { "$($stageModules.Count) stages executed" }))
if ($importCacheReused) { return }
Write-Host "Validated clean US ROM: $($importValues.hash)"
& {
    $assemblySourceParts = $assemblySourceStats.Split("`t")
    Write-Host (
    "Parsed $($assemblySourceParts[0]) assembly sources with " +
    "$($assemblySourceParts[1]) physical reads and " +
    "$($assemblySourceParts[2]) indexed label-block / " +
        "$($assemblySourceParts[3]) structured-node queries.")
}
