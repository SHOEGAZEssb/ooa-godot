# Retain source command dispatch boundaries instead of compound runtime waits.
$socketOps = [Collections.Generic.HashSet[string]]::new([string[]]@(
    'orroomflag','spawninteraction','playsound','wait','shakescreen','setcoords',
    'asm15','resetmusic','enableinput','scriptend'))
$socketCommands = @(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scripts.s') `
    'pirateSubid4Script_insertEyeball' $socketOps)
$socketBindings = @{
    'orroomflag|ROOMFLAG_80' = @{Opcode='orroomflag';Arg0='80'}
    'spawninteraction|INTERAC_DECORATION, $06, $52, $6a' = @{Opcode='nativeyield';Payload='SpawnInsertedEye'}
    'shakescreen|160' = @{Opcode='nativeyield';Payload='ShakeScreen'}
    'asm15|scriptHelp.pirate_openEyeballCave' = @{Opcode='native';Payload='OpenEntrance'}
    'asm15|loseTreasure, TREASURE_TOKAY_EYEBALL' = @{Opcode='native';Payload='ConsumeEyeball'}
    'resetmusic|' = @{Opcode='setmusic';Arg0='ff'}
}
foreach ($sound in @('SND_OPENCHEST','SND_OPENING','SND_SOLVEPUZZLE')) {
    if (-not $soundIds.ContainsKey($sound)) { throw "Tokay socket: missing $sound." }
    $socketBindings["playsound|$sound"] = @{Opcode='playsound';Arg0=([int]$soundIds[$sound]).ToString('x2')}
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/tokay_socket_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $socketCommands 'Socket' -bindings $socketBindings))

$keyOps = [Collections.Generic.HashSet[string]]::new([string[]]@(
    'stopifitemflagset','checknoenemies','spawnitem','scriptend'))
$keyCommands = @(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/common/commonScripts.s') `
    'dropSmallKeyWhenNoEnemiesScript' $keyOps)
$keyEnd = @($keyCommands | Where-Object Opcode -eq 'scriptend')
if ($keyEnd.Count -ne 1) { throw 'dropSmallKeyWhenNoEnemiesScript requires one scriptend.' }
$keyBindings = @{
    'stopifitemflagset|' = @{Opcode='jumpifroomflagset';Arg0='20';Arg1=$keyEnd[0].Index.ToString()}
    'checknoenemies|' = @{Opcode='gate';Payload='NoEnemies'}
    'spawnitem|TREASURE_SMALL_KEY, $01' = @{Opcode='nativeyield';Payload='SpawnSmallKey'}
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/enemy_small_key_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $keyCommands 'Watcher' -bindings $keyBindings))
