# Source-owned common dungeon parameters, independent of Ages room catalogs.
$doorControllerSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/doorController.s')
$commonScriptSource=Read-ImportText (Join-Path $Disassembly 'scripts/common/commonScripts.s')
$commonScriptHelperSource=Read-ImportText (Join-Path $Disassembly 'scripts/common/scriptHelper.s')
$keyOps=[Collections.Generic.HashSet[string]]::new([string[]]@('stopifitemflagset','checknoenemies','spawnitem','scriptend'))
$keyCommands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/common/commonScripts.s') 'dropSmallKeyWhenNoEnemiesScript' $keyOps)
if(($keyCommands.Opcode -join ',') -ne 'stopifitemflagset,checknoenemies,spawnitem,scriptend') {
    throw 'commonScripts.s:dropSmallKeyWhenNoEnemiesScript command contract changed.'
}
$keyBindings=@{
    'stopifitemflagset|'=@{Opcode='jumpifroomflagset';Arg0='20';Arg1='3'}
    'checknoenemies|'=@{Opcode='gate';Payload='NoEnemies'}
    'spawnitem|TREASURE_SMALL_KEY, $01'=@{Opcode='nativeyield';Payload='SpawnSmallKey'}
}
$keyRows=ConvertTo-CutsceneCommandRows $keyCommands 'Watcher' -bindings $keyBindings
$torchSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/parts/lightableTorch.s')
$treasureSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/treasure.s')
if($torchSource -notmatch '(?ms)^@subid0State2:\s+ld hl,wNumTorchesLit\s+inc \(hl\)\s+ld a,SND_LIGHTTORCH\s+call playSound.*?ld a,TILEINDEX_LIT_TORCH.*?call setTile\s+jp partDelete' -or
    $treasureSource -notmatch '(?ms)^@spawnMode2:.*?ld \(hl\),40.*?SND_SOLVEPUZZLE.*?objectGetZAboveScreen.*?ld c,\$10.*?SND_DROPESSENCE.*?ld bc,-\$aa') {
    throw 'Permanent torch or falling-treasure behavior changed.'
}
$chestOps=[Collections.Generic.HashSet[string]]::new([string[]]@(
    'stopifitemflagset','setcollisionradii','checknoenemies','playsound','createpuff','wait','settilehere','incstate','scriptend'))
$chestCommands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/common/commonScripts.s') 'createChestWhenNoEnemiesScript' $chestOps)
if (($chestCommands.Opcode -join ',') -ne 'stopifitemflagset,setcollisionradii,checknoenemies,playsound,createpuff,wait,settilehere,incstate,scriptend' -or
    $chestCommands[1].Operands -ne '$04, $06' -or $chestCommands[5].Operands -ne '30') {
    throw 'commonScripts.s:createChestWhenNoEnemiesScript command/radii/wait contract changed.'
}
$chestBindings=@{
    'stopifitemflagset|'=@{Opcode='jumpifroomflagset';Arg0='20';Arg1='8'}
    'checknoenemies|'=@{Opcode='gate';Payload='NoEnemies'}
    'playsound|SND_SOLVEPUZZLE'=@{Opcode='playsound';Arg0='4d'}
    'createpuff|'=@{Opcode='nativeyield';Payload='CreatePuff'}
    'settilehere|TILEINDEX_CHEST'=@{Opcode='native';Payload='WriteChest'}
    'incstate|'=@{Opcode='nativeyield';Payload='IncState'}
}
$chestRows=ConvertTo-CutsceneCommandRows $chestCommands 'Chest' -bindings $chestBindings
foreach($pack in @(
    @{Name='ages';Bytes=$romBytes;Symbols=$agesSymbols;Root=$destination},
    @{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
)) {
    $rewardSource=Read-ImportText (Join-Path $Disassembly "scripts/$($pack.Name)/dungeonScripts.s")
    $puffWait=[regex]::Match($rewardSource,'(?ms)^spawnChestAfterPuff:\s+playsound SND_SOLVEPUZZLE\s+createpuff\s+wait (?<wait>\d+)\s+settilehere TILEINDEX_CHEST\s+scriptend')
    if(-not $puffWait.Success){throw "$($pack.Name): trigger chest puff/wait/tile command contract changed."}
    if($pack.Name -eq 'seasons') {
        if(-not $pack.Symbols.ContainsKey('dungeonScript_checkActiveTriggersEq01')){throw 'Seasons exact-trigger chest script symbol is missing.'}
        $target=([int]$pack.Symbols['dungeonScript_checkActiveTriggersEq01'] -band 0x3fff)+0x4000
        $scripts=Read-MenuRomBytes $pack 'interactionCode20@dungeon1' 8
        $heroScripts=Read-MenuRomBytes $pack 'interactionCode20@dungeon0' 4
        if(([int]$scripts[2]-bor([int]$scripts[3]-shl 8)) -ne $target -or ([int]$scripts[4]-bor([int]$scripts[5]-shl 8)) -ne $target -or
            ([int]$heroScripts[2]-bor([int]$heroScripts[3]-shl 8)) -ne $target -or
            $rewardSource -notmatch '(?ms)^dungeonScript_checkActiveTriggersEq01:\s+stopifitemflagset\s+checkmemoryeq wActiveTriggers, \$01\s+spawnChestAfterPuff:') {
            throw 'Seasons dungeon0:$01/dungeon1:$01/$02 must select the exact-trigger$01 chest script.'
        }
    }
    $minibossWait=[regex]::Match($rewardSource,'(?ms)^dungeonScript_minibossDeath:\s+stopifroomflag80set\s+checknoenemies\s+orroomflag \$80\s+wait (?<wait>\d+)\s+spawninteraction INTERAC_MINIBOSS_PORTAL, \$00, \$00, \$00')
    if(-not $minibossWait.Success){throw "$($pack.Name): miniboss reward script changed."}
    $fallingKeyRows=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly "data/$($pack.Name)/treasureObjectData.s") 'treasureObjectData30' 'm_TreasureSubid')
    if(($fallingKeyRows[1].Operands -join ',') -ne '$28,$01,$ff,$42,TREASURE_OBJECT_SMALL_KEY_01') {
        throw "$($pack.Name): falling Small Key must use mode2/no-change grab and ROOMFLAG_ITEM."
    }
    $torchData=Read-MenuRomBytes $pack 'partData' 56
    $torchMask=Read-ImportText (Join-Path $Disassembly "data/$($pack.Name)/partActiveCollisions.s")
    if(($torchData[48..55] -join ',') -ne '0,130,68,255,64,0,0,0' -or
        $torchMask -notmatch '(?m)^\s*dbrev %00000000 %00000000 %00000000 %00010000 ; 0x06\s*$') {
        throw "$($pack.Name): PART$06 must use radius4/4 and only Ember collision$1b."
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/lightable_torch_constants.tsv'),@(
        "# key`tvalue","unlit-tile`t8","lit-tile`t9","radius-y`t4","radius-x`t4","item-collision`t27","light-sound`t114"))
    $treasureSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/treasure.s')
    $puff=[regex]::Match($treasureSource,'(?ms)^@spawnMode1:.*?ld e,Interaction.counter1\s+ld a,(?<wait>\d+)\s+ld \(de\),a\s+call objectCreatePuff\s+ret nz')
    if(-not $puff.Success){throw 'treasure.s:@spawnMode1 requires its checked puff allocation and native wait.'}
    Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_reward_constants.tsv'),@(
        "# key`tvalue","miniboss-wait`t$($minibossWait.Groups['wait'].Value)","treasure-puff-wait`t$($puff.Groups['wait'].Value)"))
    Write-CutsceneGeneratedTable((Join-Path $pack.Root 'cutscenes/enemy_small_key_commands.tsv'),$keyRows)
    [int[]]$enemyStairTiles = Read-MenuRomBytes $pack 'interactionCode12@replacementTiles' 4
    if (($enemyStairTiles -join ',') -ne '70,71,68,69') {
        throw 'dungeonStuff.s:@replacementTiles must map $40..$43 to $46,$47,$44,$45.'
    }
    $doorScriptConstants = [Collections.Generic.List[string]]::new()
    foreach ($entry in [regex]::Matches($doorControllerSource,
        '(?m)^\s*/\* \$(?<subid>1[0-7]) \*/ \.dw mainScripts\.(?<label>\w+)\s*$')) {
        $subid = $entry.Groups['subid'].Value
        $label = $entry.Groups['label'].Value
        $profile = [regex]::Match($commonScriptSource,
            '(?m)^' + [regex]::Escape($label) + ':\s+setcollisionradii \$(?<y>[0-9a-f]{2}), \$(?<x>[0-9a-f]{2})\s+setangle \$(?<angle>[0-9a-f]{2})(?:\s+setspeed \$(?<speed>[0-9a-f]{2}))?')
        if (-not $profile.Success) { throw "commonScripts.s:$label has no supported door radii/angle/speed profile." }
        foreach ($field in @('y','x','angle','speed')) {
            $value = if ($profile.Groups[$field].Success) {
                [Convert]::ToInt32($profile.Groups[$field].Value,16)
            } else { 0 }
            $doorScriptConstants.Add("door-$field-$subid`t$value")
        }
    }
    $torchWait = [regex]::Match($commonScriptSource,
        '(?ms)^doorController_shutUntilTorchesLit:\s+callscript doorController_updateRespawnWhenLinkNotTouching\s+setstate \$03\s+@loop:\s+asm15 scriptHelp\.doorController_checkEnoughTorchesLit\s+jumptable_memoryaddress wTmpcec0\s+\.dw @loop\s+\.dw @torchesLit\s+@torchesLit:\s+wait (?<wait>\d+)\s+playsound SND_SOLVEPUZZLE\s+incstate\s+scriptend')
    $entryFlip = [regex]::Match($commonScriptSource,
        '(?m)^doorController_closeDoorWhenLinkNotTouchingAndFlipcfc0:\s+callscript doorController_updateRespawnWhenLinkNotTouching\s+setstate \$03\s+xorcfc0bit (?<bit>[0-7])\s+scriptend')
    if ($doorScriptConstants.Count -ne 32 -or -not $torchWait.Success -or -not $entryFlip.Success -or
        $commonScriptHelperSource -notmatch '(?ms)^doorController_checkEnoughTorchesLit:\s+ld a,\(wNumTorchesLit\)\s+ld b,a\s+ld e,Interaction.speed\s+ld a,\(de\)\s+cp b\s+ld a,\$01\s+jr z,\+\s+dec a\s+\+\s+ld \(wTmpcec0\),a\s+ret') {
        throw 'INTERAC$1e:$10-$17 entry/torch scripts or exact torch comparison changed.'
    }
    $doorScriptConstants.Add("door-torch-wait`t$($torchWait.Groups['wait'].Value)")
    $doorScriptConstants.Add("door-entry-scratch-mask`t$(1 -shl [int]$entryFlip.Groups['bit'].Value)")
    $rows=@(
        "# key`tvalue"
        $doorScriptConstants
        "enemy-stair-tile-0`t$($enemyStairTiles[0])"
        "enemy-stair-tile-1`t$($enemyStairTiles[1])"
        "enemy-stair-tile-2`t$($enemyStairTiles[2])"
        "enemy-stair-tile-3`t$($enemyStairTiles[3])"
        "pushable-block`t29"
        "push-delay`t30"
        "solve-wait`t8"
        "chest-puff-wait`t$($puffWait.Groups['wait'].Value)"
        "door-frame-wait`t6"
        "open-tile`t160"
        "closed-up`t120"
        "closed-right`t121"
        "closed-down`t122"
        "closed-left`t123"
        "solve-sound`t77"
        "door-sound`t112"
        "button-tile`t12"
        "pressed-button-tile`t13"
        "button-radius-y`t2"
        "button-radius-x`t2"
        "button-object-release-delay`t28"
        "button-sound`t135"
    )
    if($rows.Count -ne 57){throw "$($pack.Name): expected 56 common dungeon parameter rows, got $($rows.Count-1)."}
    $shutters=Read-MenuRomBytes $pack 'interactionCode1e@shutterTiles' 24
    if(($shutters -join ',') -ne '160,112,160,113,160,114,160,115,160,116,160,117,160,118,160,119,160,120,160,121,160,122,160,123'){
        throw "$($pack.Name): native key/shutter mapping changed."
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_puzzle_constants.tsv'),$rows)
    Write-CutsceneGeneratedTable((Join-Path $pack.Root 'cutscenes/enemy_clear_chest_commands.tsv'),$chestRows)
}
