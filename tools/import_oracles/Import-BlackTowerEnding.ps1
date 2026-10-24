$native='code/ages/cutscenes/endgameCutscenes.s'
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# profile`tvalues`tsource")
foreach($name in @('blackTowerEscapeCutscene_doorBlockReplacement','blackTowerEscapeCutscene_doorOpenReplacement')) {
    $values=@(Read-AssemblyLiteralValues (Join-Path $Disassembly $native) $name)
    if($values.Count -ne 9 -or $values[0] -ne 4){throw "${native}:${name}: malformed four-tile replacement."}
    $rows.Add("$name`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${native}:${name}")
}
foreach($spec in @(@('explosion-positions','@subid0Positions',8),@('debris-speeds','@subid1And2Speeds',4))) {
    $values=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'object_code/ages/interactions/explosionWithDebris.s') $spec[1] '.db'|ForEach-Object {$_.Operands}|ForEach-Object {
        switch($_){'SPEED_180'{0x3c};'SPEED_1c0'{0x46};'SPEED_200'{0x50};'SPEED_240'{0x5a};default{Convert-AssemblyInteger $_}}
    })
    if($values.Count -ne $spec[2]){throw "explosionWithDebris.s:$($spec[1]): malformed table."}
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`tobject_code/ages/interactions/explosionWithDebris.s:$($spec[1])")
}
foreach($spec in @(@('dog-waits','@counter1Vals',8,'.db'),@('dog-targets','@positions',16,'.db'),@('dog-jumps','@speedZVals',7,'.dw'),@('dog-z','@zPositions',4,'.dw'))) {
    $source='object_code/common/interactions/horonDogCredits.s'
    $values=@(Read-AssemblyDataDirectives (Join-Path $Disassembly $source) $spec[1] $spec[3]|ForEach-Object {$_.Operands}|ForEach-Object {Convert-AssemblyInteger $_})
    if($values.Count -ne $spec[2]){throw "${source}:$($spec[1]): unexpected source table length."}
    $bytes=@(foreach($value in $values){$value -band 255;if($spec[3] -eq '.dw'){($value -shr 8) -band 255}})
    $rows.Add("$($spec[0])`t$(($bytes|ForEach-Object {$_.ToString('x2')}) -join ',')`t${source}:$($spec[1])")
}
$boySource='object_code/ages/interactions/boy.s'
$boyMovement=@(Read-AssemblyLiteralValues (Join-Path $Disassembly $boySource) '@movementData')
if($boyMovement.Count -ne 27 -or $boyMovement[8] -ne 0 -or $boyMovement[17] -ne 0 -or $boyMovement[26] -ne 0){throw "${boySource}: expected three original nine-byte pattern rows."}
$rows.Add("boy-patterns`t$(($boyMovement|ForEach-Object {$_.ToString('x2')}) -join ',')`t${boySource}:boyRunSubid08/@movementData")
# DF's animation reload leaves HL=$76xx, then LD L,$78 (Interaction.var38).
# The restored bank is$10: DEC targets ROM$7678, not object RAM. Guard the
# original pointer page and nonzero DEC result before relying on that skip.
$dfPointers=@(0xe1,0x4a,0xee,0x4a,0xfb,0x4a,0x08,0x4b,0x1e,0x4a,0x2b,0x4a,0xea,0x49,0xf7,0x49)
for($i=0;$i -lt $dfPointers.Count;$i++){if($romBytes[0x5b62c+$i] -ne $dfPointers[$i]){throw 'interactiondfOamDataPointers: clean-US $16:762c pointer page changed.'}}
if($romBytes[0x43678] -eq 1){throw 'nayruRalphCredits.s: animation-reload ROM decrement would unexpectedly change the turn state.'}
$companionSource='object_code/ages/specialObjects/companionCutscene.s'
foreach($spec in @(@('triforce-positions','introSprites1.s','@data'),@('triforce-glow-offsets','introSprites1.s','@data_4660'),@('waving-positions','cf.s','@positions'))){
    $source='object_code/common/interactions/'+$spec[1]
    $values=@(Read-AssemblyLiteralValues (Join-Path $Disassembly $source) $spec[2])
    if($values.Count -ne 6){throw "${source}:$($spec[2]): expected three source YX pairs."}
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${source}:$($spec[2])")
}
$jumps=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly $companionSource) 'specialObjectCode_dimitriCutscene' 'dbw')
if($jumps.Count -ne 3){throw "${companionSource}: Dimitri credits requires three original jump rows."}
$jumpBytes=@(foreach($jump in $jumps){if($jump.Operands.Count -ne 2){throw "${companionSource}: malformed jump."};Convert-AssemblyInteger $jump.Operands[0];$word=Convert-AssemblyInteger $jump.Operands[1];$word -band 255;($word -shr 8) -band 255})
$rows.Add("companion-jumps`t$(($jumpBytes|ForEach-Object {$_.ToString('x2')}) -join ',')`t${companionSource}:specialObjectCode_dimitriCutscene/@@data")
$specialAnimations=Read-ImportText (Join-Path $Disassembly 'data/ages/specialObjectAnimationData.s')
foreach($pair in @(@('0b','0f'),@('0c','10'),@('0d','11'))){foreach($suffix in @('GfxPointers','AnimationDataPointers','OamDataPointers')){
    if($specialAnimations -notmatch ('specialObject'+$pair[0]+$suffix+':\s*specialObject'+$pair[1]+$suffix+':')){throw "companionCutscene.s: normal/cutscene visual alias $($pair[0])/$($pair[1]) $suffix changed."}
}}
$shipZ=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/interactions/linkShip.s') '@zPositions')
$waves=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'code/ages/cutscenes/bank10.s') '@@data')
if($shipZ.Count -ne 8 -or $waves.Count -ne 4){throw 'Linked finale: malformed ship Z or wave interval table.'}
$rows.Add("ship-z`t$(($shipZ|ForEach-Object {$_.ToString('x2')}) -join ',')`tobject_code/common/interactions/linkShip.s:@zPositions")
$rows.Add("wave-intervals`t$(($waves|ForEach-Object {$_.ToString('x2')}) -join ',')`tcode/ages/cutscenes/bank10.s:@@data")
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_tables.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# stream`tindex`tframes`tkeys`tsource")
foreach($id in 1..6) {
    $relative=if($id -ge 5){'object_code/ages/interactions/a8.s'}else{'code/ages/cutscenes/bank10.s'}
    $path=Join-Path $Disassembly $relative;$label=if($id -eq 6){'@simulatedInput_5eea'}elseif($id -eq 5){'@linkSwordDemonstrationInput'}else{"blackTowerEscape_simulatedInput$id"}
    $input=@(Read-AssemblyMacroInvocations $path $label 'dwb')
    if($id -eq 1){$input+=@(Read-AssemblyMacroInvocations $path 'blackTowerEscape_simulatedInput2' 'dwb')}
    $tail=if($id -eq 1){'blackTowerEscape_simulatedInput2'}else{$label}
    $sentinel=@(Read-AssemblyDataDirectives $path $tail '.dw'|ForEach-Object {$_.Operands}|ForEach-Object {Convert-AssemblyInteger $_})
    if($sentinel.Count -ne 1 -or $sentinel[0] -ne 0xffff){throw "bank10.s:${tail}: expected the native `$ffff stop counter."}
    $index=0
    foreach($entry in $input) {
        $keys=0
        foreach($key in $entry.Operands[1].Split('|')) {
            $keys=$keys -bor $(switch($key.Trim()) {'BTN_UP'{0x40};'BTN_DOWN'{0x80};'BTN_RIGHT'{0x10};'BTN_LEFT'{0x20};'BTN_B'{2};'BTN_A'{1};default{Convert-AssemblyInteger $key}})
        }
        $rows.Add("$id`t$index`t$(Convert-AssemblyInteger $entry.Operands[0])`t$($keys.ToString('x2'))`t${relative}:$label");$index++
    }
    # Native streams end in the $ffff counter, retaining last keys.
    $rows.Add("$id`t$index`t$($sentinel[0])`t00`t${relative}:$label")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_inputs.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# scene`tindex`tid`tsubid`ty`tx`tsource")
foreach($label in @('objectData_blackTowerEscape_nayruAndRalph','objectData_blackTowerEscape_ambiAndGuards','objectData5462','objectData5470','objectData54ac','objectData54d0','objectData54c6','objectData54fe','objectData5492','objectData556a','objectData5514','objectData5542','objectData5560','objectData5574')) {
    $entries=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/extraData2.s') $label 'obj_Interaction');$index=0
    foreach($entry in $entries) {
        if($entry.Operands.Count -ne 4){throw "${label}: unsupported actor operands."}
        $rows.Add("$label`t$index`t$(($entry.Operands|ForEach-Object {(Convert-AssemblyInteger $_).ToString('x2')}) -join "`t")`tobjects/ages/extraData2.s:$label");$index++
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_placements.tsv'),$rows)
$roomSource='code/ages/cutscenes/miscCutscenes.s'
$roomBlock=[regex]::Match((Read-ImportText (Join-Path $Disassembly $roomSource)),
    '(?ms)^cutscene_disableLcdLoadRoomResetCamera:.*?^@data:\s*(?<rows>.*?)^cutscene_tickDownCBB4ThenSetTo30:')
if(-not $roomBlock.Success){throw "${roomSource}: missing native room/object-set boundary."}
$rooms=@([regex]::Matches($roomBlock.Groups['rows'].Value,'(?m)^\s*dwbe\s+(?<name>\w+)')|ForEach-Object {$_.Groups['name'].Value})
$sets=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'objects/ages/extraData3.s') 'objectTable2' '.dw'|ForEach-Object {$_.Operands})
if($rooms.Count -ne 20 -or $sets.Count -ne 20){throw 'Black Tower ending requires the original twenty-entry room/object-set tables.'}
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# index`tgroup`troom`tscene`tsource")
foreach($index in @(8,9,10,17)) {
    if($rooms[$index] -notmatch '^ROOM_AGES_(?<id>[0-9a-f]{3})$'){throw "Ending object set `$${index}: unsupported room symbol."}
    $id=[Convert]::ToInt32($Matches['id'],16)
    $rows.Add("$($index.ToString('x2'))`t$(($id -shr 8).ToString('x2'))`t$(($id -band 255).ToString('x2'))`t$($sets[$index])`t${roomSource}:cutscene_disableLcdLoadRoomResetCamera / objects/ages/extraData3.s:objectTable2")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_room_sets.tsv'),$rows)
$endingSource=Join-Path $Disassembly 'code/ages/cutscenes/endgameCutscenes.s'
$creditRooms=@(Read-AssemblyMacroInvocations $endingSource '@@table_5f1c' 'dwbe'|ForEach-Object {$_.Operands})
$creditObjects=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'objects/ages/extraData2.s') 'objectTable1' '.dw'|ForEach-Object {$_.Operands})
$uncmp=@(Read-AssemblyLiteralValues $endingSource '@@table_5f24')
$palettes=@(Read-AssemblyLiteralValues $endingSource '@@table_5f28')
if($creditRooms.Count -ne 4 -or $creditObjects.Count -lt 4 -or $uncmp.Count -ne 4 -or $palettes.Count -ne 8){throw "CUTSCENE `$0a requires the original room/object/palette tables: rooms=$($creditRooms.Count), objects=$($creditObjects.Count), uncmp=$($uncmp.Count), palettes=$($palettes.Count)."}
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# index`tgroup`troom`tscene`tuncmp`tpalette`tsource")
foreach($index in 0..3) {
    if($creditRooms[$index] -notmatch '^ROOM_AGES_(?<id>[0-9a-f]{3})$'){throw "Credits room index ${index}: unsupported source room."}
    $id=[Convert]::ToInt32($Matches['id'],16)
    $rows.Add("$index`t$(($id -shr 8).ToString('x2'))`t$(($id -band 255).ToString('x2'))`t$($creditObjects[$index])`t$($uncmp[$index].ToString('x2'))`t$($palettes[$index].ToString('x2'))`tcode/ages/cutscenes/endgameCutscenes.s:state1 / objects/ages/extraData2.s:objectTable1")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_credits_rooms.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# subid`tsprite`ttile-base`tpalette`tanimation`tsource")
if((Read-ImportText (Join-Path $Disassembly 'data/ages/gfxHeaders.s')) -notmatch '(?ms)^m_GfxHeaderStart \$83, GFXH_COMMON_SPRITES\s+m_GfxHeader spr_common_sprites, \$8001') {
    throw 'INTERAC $99 expects gfx00 to retain GFXH_COMMON_SPRITES at $8001.'
}
foreach($subid in 0..2) {
    $gfx=$interactionGraphics["153:$subid"]
    if($null -eq $gfx){throw "explosionWithDebris.s: missing INTERAC `$99:$subid graphics."}
    $sprite=if($gfx.Gfx -eq 0){'spr_common_sprites'}else{$gfxNames[$gfx.Gfx]};Copy-EnemySprite $sprite
    $rows.Add("$subid`t$sprite`t$($gfx.TileBase)`t$($gfx.Palette)`t$(Resolve-NpcAnimation 0x99 $gfx.DefaultAnimation)`tdata/ages/interactionData.s:interaction99SubidData")
}
Write-GeneratedTable((Join-Path $destination 'objects/explosion_debris_visuals.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# id`tsubid`tsprite`ttile-base`tpalette`tanimation`textra-sprite`tnative-flags`tsource")
$animationRows=[Collections.Generic.List[string]]::new();$animationRows.Add("# id`tindex`tanimation`tsource")
$animations=@{}
foreach($spec in @(@(0x4d,1,10),@(0x4d,2,10),@(0x57,1,7),@(0x57,2,7),@(0x88,2,2),@(0x31,9,9),@(0x31,2,9),@(0x6b,0x0e,5),@(0x6b,0x10,5),@(0x40,8,3),@(0x3c,0x0c,10),@(0x3c,0,10),@(0x3a,0x0b,13),@(0x43,5,3),
    @(0x4b,3,9),@(0x4b,4,9),@(0x4b,5,9),@(0x4b,0,9),@(0x4c,1,3),@(0x4c,2,3),@(0x4c,3,3),@(0x5d,1,0),
    @(0x31,3,9),@(0xaa,0,5),@(0xad,1,7),@(0xad,2,7),@(0xb7,0,1),@(0xb7,1,1),@(0xc1,0,0),@(0x84,9,0),
    @(0xaa,1,5),@(0x6b,3,5),@(0x6b,0x12,5),@(0xb9,0,15),@(0xb9,1,15),@(0xb9,2,15),@(0xb9,3,15),@(0xb9,4,15),@(0xb9,5,15),@(0xb9,6,15),@(0xb9,7,15),
    @(0xdf,0,5),@(0xdf,1,5),@(0x3c,8,10),@(0x3c,9,10),@(0x3f,3,3),@(0xc0,0,3),@(0x4a,9,3),@(0x4a,10,3),@(0xcf,0,2),@(0xcf,1,2),@(0xcf,2,2),@(0xd4,0,2),@(0xd4,1,2),@(0xd4,2,2))) {
    $id=[int]$spec[0];$subid=[int]$spec[1];$gfx=Resolve-InteractionGraphics $id $subid
    if($null -eq $gfx){throw "Black Tower ending: missing INTERAC `$${id}:`$${subid} graphics."}
    $sprite=$gfxNames[$gfx.Gfx]
    if($id -eq 0xd4 -and ($gfx.Gfx -ne 0x8b -or $sprite -ne 'spr_boat_theend')){throw 'linkShip.s:$d4 requires its original $8b ordinary object graphics header.'}
    if($id -eq 0x4a){
        if($gfx.Gfx -ne 0 -or $gfx.TileBase -ne $(if($subid -eq 9){0x60}else{0x6a}) -or $gfx.Flags -ne $(if($subid -eq 9){0xa0}else{0x93})){throw 'introSprites1.s:$4a:$09/$0a requires retained bank1 Triforce graphics.'}
        $sprite='spr_triforce_sparkle_vineseed_bookofseals'
    }
    if($id -eq 0xcf){
        if($gfx.Gfx -ne 0 -or $gfx.TileBase -ne 0x26 -or $gfx.Flags -ne (0xe0+$subid)){throw 'cf.s: retained bank1 credits sprites construction changed.'}
        $sprite='spr_credits_sprites_2'
    }
    if($id -eq 0xb7 -and $subid -eq 0) {
        # Flags $a0 select OBJ bank1/palette2. Gfx00 performs no upload;
        # tile$16 retains GFXH_COMMON_SPRITES, not bank0's GFX04 tree sheet.
        if($gfx.Gfx -ne 0 -or $gfx.TileBase -ne 0 -or $gfx.Flags -ne 0xa0 -or $gfx.DefaultAnimation -ne 0){throw 'kissHeart.s:$b7:$00 retained common-sprites construction changed.'}
        if((Read-ImportText (Join-Path $Disassembly 'data/ages/gfxHeaders.s')) -notmatch '(?ms)^m_GfxHeaderStart \$83, GFXH_COMMON_SPRITES\s+m_GfxHeader spr_common_sprites, \$8001'){throw 'kissHeart.s:$b7:$00 requires the native bank1 common-sprites upload.'}
        $sprite='spr_common_sprites'
    }
    Copy-EnemySprite $sprite
    $extra='-'
    if($id -eq 0xdf){
        if($gfx.Gfx -ne $(if($subid -eq 0){0x26}else{0x24})){throw 'nayruRalphCredits.s:$df requires the source two-header Nayru/Ralph graphics ranges.'}
        $extra=$gfxNames[$gfx.Gfx+1];Copy-EnemySprite $extra
    }
    if($id -eq 0x4b) {
        if($gfx.Gfx -ne 0x0d -or (Read-ImportText (Join-Path $Disassembly 'data/ages/objectGfxHeaders.s')) -notmatch '/\* \$0e \*/ m_ObjectGfxHeader spr_vasu_snake_deadbunny, 1'){
            throw 'rabbitMain.s: extra graphics must retain the source $0d/$0e boundary.'
        }
        $extra=$gfxNames[$gfx.Gfx+1];Copy-EnemySprite $extra
    }
    $rows.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$sprite`t$($gfx.TileBase)`t$($gfx.Palette)`t$(Resolve-NpcAnimation $id $gfx.DefaultAnimation)`t$extra`t$(($gfx.Flags -shr 4).ToString('x2'))`tdata/ages/interactionData.s")
    if($animations.ContainsKey($id)){continue}
    $animations[$id]=@{}
    foreach($index in 0..$spec[2]) {
        $animation=Resolve-NpcAnimation $id $index
        if(-not $animation){throw "Black Tower ending: missing animation `$$($id.ToString('x2')):`$$($index.ToString('x2'))."}
        $animations[$id][$index]=$animation
        $animationRows.Add("$($id.ToString('x2'))`t$($index.ToString('x2'))`t$animation`tdata/ages/interactionAnimations.s")
    }
}

$main=Join-Path $Disassembly 'scripts/ages/scripts.s'
$dogOps=[Collections.Generic.HashSet[string]]@('setspeed','wait','setangle','applyspeed','scriptjump')
$dogCommands=@(Read-AssemblyCutsceneCommands $main 'horonDogCreditsScript' $dogOps 'childJabuScript')
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_credits-dog_commands.tsv'),
    (ConvertTo-CutsceneCommandRows -commands $dogCommands -actor 'Actor' -symbols @{'SPEED_080'=0x14} -yieldOnJump $false))
$ops=[Collections.Generic.HashSet[string]]@('wait','setanimation','scriptend')
$commands=@(Read-AssemblyCutsceneCommands $main 'rabbitSubid4Script' $ops 'rabbitScript_waitingForNayru1')
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_restored-rabbit_commands.tsv'),
    (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -animations $animations[0x4b] -yieldOnJump $false))
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_visuals.tsv'),$rows)
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_animations.tsv'),$animationRows)
$helper=Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$reunionOps=[Collections.Generic.HashSet[string]]@('checkmemoryeq','wait','setspeed','setangle','applyspeed','moveleft','moveright','movedown','setanimation','writememory','writeobjectbyte','asm15','showtext','scriptend')
foreach($spec in @(@('reunion-impa',0x31,$main,'impaScript3','impaScript4'),
    @('reunion-zelda',0xad,$helper,'zeldaSubid01Script_body','zeldaSubid02Script_body'),
    @('credits-zelda',0xad,$helper,'zeldaSubid02Script_body','zeldaSubid03Script_body'))) {
    $commands=@(Read-AssemblyCutsceneCommands $spec[2] $spec[3] $reunionOps $spec[4])
    $texts=@{}
    foreach($command in $commands|Where-Object Opcode -eq 'showtext') {
        if($command.Operands -notmatch '^TX_(?<id>[0-9a-f]{4})$'){throw "Reunion script: unsupported text '$($command.Operands)'."}
        $id=[Convert]::ToInt32($Matches['id'],16);$texts[$id]=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
    }
    Write-GeneratedTable((Join-Path $destination "cutscenes/black_tower_ending_$($spec[0])_commands.tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols @{'SPEED_020'=5;'SPEED_080'=0x14;'SPEED_100'=0x28;
            'wTmpcfc0.genericCutscene.state'='CreditsSignal';'wTmpcfc0.genericCutscene.cfdf'='CreditsDone';'Interaction.var39'=0x79} `
            -animations $animations[[int]$spec[1]] -texts $texts -positions $allTextPositions -bindings @{
                'asm15|objectSetInvisible'=@{Opcode='native';Payload='Hide'};'asm15|objectSetVisible82'=@{Opcode='native';Payload='Reveal'}} -yieldOnJump $false))
}
$wrapper=@(Read-AssemblyCutsceneCommands $main 'pickaxeWorkerSubid01Script_part2' ([Collections.Generic.HashSet[string]]@('loadscript')) 'pickaxeWorkerSubid01Script_part3')
if($wrapper.Count -ne 1 -or $wrapper[0].Operands -ne 'scriptHelp.pickaxeWorkerSubid01Script_part2'){throw 'pickaxeWorkerSubid01Script_part2 must retain its copied script entry.'}
$constructionOps=[Collections.Generic.HashSet[string]]@('wait','asm15','checkpalettefadedone','writememory','setspeed','setangle','checkmemoryeq','scriptend','setanimation','addobjectbyte','applyspeed','scriptjump','writeobjectbyte','setcoords')
$constructionBindings=@{'asm15|fadeoutToWhite'=@{Opcode='native';Payload='FadeOut'};'asm15|fadeinFromWhite'=@{Opcode='native';Payload='FadeIn'};
    'asm15|scriptHelp.pickaxeWorker_setRandomDelay'=@{Opcode='native';Payload='RandomDelay'};
    'asm15|objectSetVisiblec1'=@{Opcode='native';Payload='Priority:c1'};'asm15|objectSetVisible83'=@{Opcode='native';Payload='Priority:83'};
    'checkpalettefadedone|'=@{Opcode='gate';Payload='PaletteDone'};
    'addobjectbyte|Interaction.animCounter, $08'=@{Opcode='native';Payload='AnimationDelay'}}
foreach($spec in @(@('credits-ambi',0x4d,$main,'ambiSubid02Script','ambiSubid03Script'),
    @('credits-statue',0x6b,$main,'interaction6b_subid10Script','fairyHidingMinigame_subid00Script'),
    @('credits-worker1-start',0x57,$main,'pickaxeWorkerSubid01Script_part1','pickaxeWorkerSubid02Script_part1'),
    @('credits-worker2-start',0x57,$main,'pickaxeWorkerSubid02Script_part1','pickaxeWorkerSubid01Script_part2'),
    @('credits-worker1-middle',0x57,$helper,'pickaxeWorkerSubid01Script_part2','hardhatWorker_moveLinkAway'),
    @('credits-worker2-middle',0x57,$main,'pickaxeWorkerSubid02Script_part2','pickaxeWorkerSubid02Script_part3'),
    @('credits-worker1-end',0x57,$main,'pickaxeWorkerSubid01Script_part3','pickaxeWorkerSubid02Script_part2'),
    @('credits-worker2-end',0x57,$main,'pickaxeWorkerSubid02Script_part3','pickaxeWorkerSubid03Script'))) {
    $commands=@(Read-AssemblyCutsceneCommands $spec[2] $spec[3] $constructionOps $spec[4])
    Write-GeneratedTable((Join-Path $destination "cutscenes/black_tower_ending_$($spec[0])_commands.tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols @{'SPEED_040'=0x0a;'SPEED_080'=0x14;
            'wTmpcfc0.genericCutscene.state'='CreditsSignal';'wTmpcfc0.genericCutscene.cfdf'='CreditsDone';'Interaction.var3f'=0x7f} `
            -animations $animations[[int]$spec[1]] -bindings $constructionBindings -yieldOnJump $false))
}
$audienceOps=[Collections.Generic.HashSet[string]]@('initcollisions','checkabutton','asm15','ormemory','showtext','wait','setanimation','scriptjump')
foreach($spec in @(@('listening-rabbit',0x4b,'rabbitScript_listeningToNayruGameStart','rabbitSubid4Script',2,0x5705),
    @('listening-boy',0x3c,'boySubid00Script','boySubid01Script',4,0x2510))) {
    $commands=@(Read-AssemblyCutsceneCommands $main $spec[2] $audienceOps $spec[3])
    $bindings=@{'asm15|scriptHelp.turnToFaceLink'=@{Opcode='native';Payload='AudienceFaceLink'};
        "ormemory|wTmpcfc0.genericCutscene.cfde, `$$($spec[4].ToString('x2'))"=@{Opcode='native';Payload="AudienceBit:$($spec[4])"}}
    $textId=[int]$spec[5];$texts=@{};$texts[$textId]=Resolve-PalaceText $textId ([Collections.Generic.HashSet[int]]::new())
    Write-GeneratedTable((Join-Path $destination "cutscenes/black_tower_ending_$($spec[0])_commands.tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -animations $animations[[int]$spec[1]] -texts $texts -positions $allTextPositions -bindings $bindings -yieldOnJump $false))
}
$commands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scripts.s') 'impaScript2' `
    ([Collections.Generic.HashSet[string]]@('checkpalettefadedone','wait','setspeed','moveup','addobjectbyte','checkmemoryeq','setanimation','checkobjectbyteeq','writememory','scriptend')) 'impaScript3')
$bindings=@{'checkpalettefadedone|'=@{Opcode='gate';Payload='PaletteDone'};
    'addobjectbyte|Interaction.var38, $1e'=@{Opcode='native';Payload='JumpWait'};
    'addobjectbyte|Interaction.substate, $01'=@{Opcode='native';Payload='JumpState'}}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_credits-impa_commands.tsv'),
    (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols @{'SPEED_200'=0x50;'wTmpcfc0.genericCutscene.state'='CreditsSignal';'Interaction.animParameter'='AnimationParameter'} `
        -animations $animations[0x31] -bindings $bindings -yieldOnJump $false))
$main=Join-Path $Disassembly 'scripts/ages/scripts.s'
$commands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/common/commonScripts.s') 'gameCompleteDialogScript' `
    ([Collections.Generic.HashSet[string]]@('wait','showtext','jumpiftextoptioneq','asm15','scriptjump','writememory','scriptend')) 'ringHelpBookSubid1Reset')
$texts=@{};foreach($id in @(0x550d,0x550e)){$texts[$id]=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())}
$bindings=@{'asm15|scriptHelp.gameCompleteDialog_markGameAsComplete'=@{Opcode='native';Payload='MarkGameComplete'};
    'asm15|saveFile'=@{Opcode='native';Payload='SaveFile'}}
Write-GeneratedTable((Join-Path $destination 'cutscenes/game_complete_dialog_commands.tsv'),
    (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols @{'wTmpcfc0.genericCutscene.cfde'='CompleteDialogDone'} `
        -texts $texts -positions $allTextPositions -bindings $bindings -yieldOnJump $false))
$ops=[Collections.Generic.HashSet[string]]@('checkmemoryeq','playsound','showtext','wait','writememory','checkobjectbyteeq','scriptend')
$symbols=@{'wTmpcfc0.genericCutscene.cfd0'='EndingSignal';'Interaction.var3e'='EndingJump'}
$texts=@{}
foreach($spec in @(@('ambi','ambiSubid01Script_part1','ambiSubid01Script_part2'),@('ambi-farewell','ambiSubid01Script_part2','ambiSubid02Script'))) {
    $commands=@(Read-AssemblyCutsceneCommands $main $spec[1] $ops $spec[2]);$bindings=@{}
    foreach($command in $commands) {
        if($command.Opcode -eq 'playsound') {
            # Resolve both music/control constants from the supported source.
            $constants=Read-ImportText (Join-Path $Disassembly 'constants/common/music.s')
            $match=[regex]::Match($constants,'(?m)^\s*'+[regex]::Escape($command.Operands)+'\s+db\s*;\s*\$(?<id>[0-9a-f]{2})')
            if(-not $match.Success){$match=[regex]::Match($constants,'(?m)^\.define\s+'+[regex]::Escape($command.Operands)+'\s+\$(?<id>[0-9a-f]{2})')}
            if(-not $match.Success){throw "Ending Ambi: unresolved sound '$($command.Operands)'."}
            $sound=[Convert]::ToInt32($match.Groups['id'].Value,16)
            $bindings["playsound|$($command.Operands)"]=@{Opcode='playsound';Arg0=$sound.ToString('x2')}
        }
        if($command.Opcode -eq 'showtext') {
            $id=[Convert]::ToInt32($command.Operands.Substring(3),16);$texts[$id]=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
        }
    }
    Write-GeneratedTable((Join-Path $destination "cutscenes/black_tower_ending_$($spec[0])_commands.tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols $symbols -animations $animations[0x4d] -texts $texts -positions $allTextPositions -bindings $bindings -yieldOnJump $false))
}
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# text-id`tmessage-base64`tsource")
foreach($id in @(0x1d0a,0x3d0e,0x3d0f,0x0563,0x3d09)) {
    $message=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_ending_texts.tsv'),$rows)

# These direct ROM scripts jump backwards outside their named blocks.
# Import the target blocks too; retain their indices and direct-jump timing.
$ops=[Collections.Generic.HashSet[string]]@('checkmemoryeq','wait','setspeed','moveleft','moveright','setanimation','setangle','applyspeed','writeobjectbyte','asm15','scriptjump')
$symbols=@{'wTmpcfc0.genericCutscene.cfd1'='RestorationSignal';'SPEED_180'=0x3c;'SPEED_200'=0x50;'Interaction.var39'=0x79;'Interaction.var3b'=0x7b}
foreach($spec in @(@('restored-boy',0x3c,'boySubid0cScript','boySubid0dScript'),@('restored-father',0x3a,'villagerSubid0bScript','villagerSubid0dScript'))) {
    $commands=@(Read-AssemblyCutsceneCommands $main $spec[2] $ops $spec[3])
    if($spec[0] -eq 'restored-father') {
        $loop=@(Read-AssemblyCutsceneCommands $main 'villagerSubid09Script' $ops 'script5bbf')
        foreach($command in $loop){$command.Index+=$commands.Count};$commands+=$loop
    }
    $bindings=@{}
    foreach($command in $commands) {
        if($command.Opcode -ne 'asm15'){continue}
        $handler=switch -Regex ($command.Operands) {
            '^scriptHelp.loadNextAnimationFrameAndMore, \$(?<id>[0-9a-f]{2})$' {"Throw:$([Convert]::ToInt32($Matches['id'],16))";break}
            '^scriptHelp.villager_createBallAccessory$' {'Accessory';break}
            '^scriptHelp.villager_createBall$' {'Ball';break}
            default{throw "Restoration script: unsupported helper '$($command.Operands)' at $($command.Path):$($command.Line)."}
        }
        $bindings["asm15|$($command.Operands)"]=@{Opcode='native';Payload=$handler}
    }
    Write-GeneratedTable((Join-Path $destination "cutscenes/black_tower_ending_$($spec[0])_commands.tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols $symbols -animations $animations[[int]$spec[1]] -bindings $bindings -yieldOnJump $false))
}
