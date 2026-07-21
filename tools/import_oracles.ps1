param(
    [string]$Disassembly = "C:\msys64\home\timst\oracles-disasm",
    [string]$Rom = (Join-Path $PSScriptRoot "..\Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc")
)

$ErrorActionPreference = "Stop"
$importRoot = $PSScriptRoot
$importModuleRoot = Join-Path $importRoot 'import_oracles'

# The import stages are dot-sourced in dependency order. This lets dedicated
# stages share parsed disassembly tables without reparsing or serializing
# intermediate data, while this file remains the stable command-line entry point.
$importScripts = @(
    'Initialize-Import.ps1'
    'Import-WorldAssets.ps1'
    'Import-MenuAssets.ps1'
    'Import-DialogueAndIntro.ps1'
    'Import-MapAndItemData.ps1'
    'Import-NpcData.ps1'
    'Import-CutsceneData.ps1'
    'Import-EnemyData.ps1'
    'Import-WorldNavigation.ps1'
    'Import-AudioData.ps1'
    'Write-GeneratedTableManifest.ps1'
)
foreach ($importScript in $importScripts) {
    . (Join-Path $importModuleRoot $importScript)
}

Write-Host "Validated clean US ROM: $hash"
Write-Host "Imported $($tilesets.Count) tilesets, 1536 rooms, 42 signs, $($npcRows.Count - 1) NPCs, $($dungeonMechanicRows.Count - 1) dungeon button/trigger/chest/shutter placements, $keeseInstanceCount Keese, $octorokInstanceCount Octoroks, $stalfosInstanceCount ordinary Stalfos, $zolInstanceCount Zols, $gelInstanceCount direct Gels, $($orderedObjectRows.Count - 1) ordered placement records, $enemyUnspawnableTileCount enemy-unspawnable tile records, 133 chests, 529 warps, 22 animation groups, and 223 sound IDs into $destination"
