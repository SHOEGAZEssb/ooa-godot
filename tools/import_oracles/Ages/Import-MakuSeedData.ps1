# Adult Maku Tree state $0e and both CUTSCENE_TWINROVA_REVEAL branches.
$treeSource=Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/makuTree.s')
$seedSource=Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/makuSeed.s')
$paletteSource=Read-ImportText (Join-Path $Disassembly 'data/ages/paletteHeaders.s')
if ($treeSource -notmatch '(?ms)^@state0e:\s*ld a,\$06\s*jr @runSubidCode' -or
    $treeSource -notmatch '(?ms)^@subid06:.*?GLOBALFLAG_SAW_TWINROVA_BEFORE_ENDGAME.*?GLOBALFLAG_GOT_MAKU_SEED.*?makuTree_subid06Script_part2' -or
    $seedSource -notmatch '(?ms)ld \(hl\),\$8b.*?^@state1:.*?cp \$f3.*?ld \(wTmpcfc0.genericCutscene.state\),a.*?ld bc,\$0080' -or
    $paletteSource -notmatch '(?ms)m_PaletteHeaderStart \$ab, PALH_ab\s*m_PaletteHeaderSpr 7, 1, paletteData4178') {
    throw 'makuTree.s:$0e / makuSeed.s / PALH_ab native dispatch, descent or palette contract changed.'
}
$helper = Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$ops = [Collections.Generic.HashSet[string]]::new([string[]]@(
    'disableinput','enableinput','wait','showtextlowindex','asm15','checkmemoryeq','playsound',
    'giveitem','writememory','setanimation','scriptend','showloadedtext','addobjectbyte','checkpalettefadedone',
    'setglobalflag','setcollisionradii','makeabuttonsensitive','checkabutton','scriptjump','writeobjectbyte',
    'setspeed','movedown','showtext'))
$symbols = @{'TREASURE_MAKU_SEED'=0x36;'CUTSCENE_TWINROVA_REVEAL'=0x0e;
    'GLOBALFLAG_SAW_TWINROVA_BEFORE_ENDGAME'=0x13;'<TX_0560'=0x60;
    'Interaction.direction'=0x48;'DIR_DOWN'=2;
    'wTmpcfc0.genericCutscene.state'='SeedSignal';'wCutsceneTrigger'='CutsceneTrigger';
    'wMakuMapTextPresent'='MakuMapText';'wTmpcfc0.genericCutscene.cfd1'='SceneSignal';
    'wTmpcfc0.genericCutscene.cfdf'='SceneDone';'SPEED_080'=0x14;'SPEED_0c0'=0x1e;'SPEED_100'=0x28}
foreach ($key in $soundIds.Keys) { $symbols[$key]=[int]$soundIds[$key] }
$musicSource=Read-ImportText (Join-Path $Disassembly 'constants/common/music.s')
foreach ($match in [regex]::Matches($musicSource,'(?m)^\s*(?<name>MUS_\w+)\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')) {
    $symbols[$match.Groups['name'].Value]=[Convert]::ToInt32($match.Groups['value'].Value,16)
}
$texts = @{}
foreach ($id in @((0x0559..0x0560) + (0x2810..0x2819) + @(0x0600,0x251b))) {
    $texts[$id] = Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
}
$animations = @{}
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# id`tsubid`tsprites`ttile-base`tpalette`tanimation`tsource-offset`tsource")
$animationRows = [Collections.Generic.List[string]]::new()
$animationRows.Add("# id`tindex`tanimation`tsource")
foreach ($spec in @(@(0x87,6,4),@(0xa6,0,0),@(0x84,0x0f,0),@(0x8d,2,3),@(0x93,2,3),@(0x93,3,3),
    @(0x93,4,3),@(0x93,5,3),@(0x93,6,3),@(0x93,7,3),@(0x3c,0x0a,3),@(0x3a,0x0a,13),
    @(0x43,4,0),@(0xad,5,8),@(0x31,6,3))) {
    $id=[int]$spec[0]; $subid=[int]$spec[1]; $graphic=Resolve-InteractionGraphics $id $subid
    if ($null -eq $graphic) { throw "Maku Seed: missing INTERAC `$$($id.ToString('x2')):`$$($subid.ToString('x2')) graphics." }
    $sprites=@($gfxNames[$graphic.Gfx])
    if ($id -eq 0x87) { $sprites += $gfxNames[$graphic.Gfx+1] }
    if ($id -eq 0xad) { $sprites += $gfxNames[$graphic.Gfx+1] }
    if ($id -eq 0x93) { $sprites=@(0x2c..0x2e | ForEach-Object {$gfxNames[$_]}) }
    foreach ($sprite in $sprites) { Copy-EnemySprite $sprite }
    $offset=0
    if ($id -eq 0x84) {
        $header=Read-ImportText (Join-Path $Disassembly 'data/ages/objectGfxHeaders.s')
        $match=[regex]::Match($header,'(?m)/\* \$3a \*/ m_ObjectGfxHeader spr_link, \$00, \$(?<offset>[0-9a-f]{4})')
        if (-not $match.Success -or $graphic.Gfx -ne 0x3a) {throw 'Maku Seed: missing sparkle $84:$0f source graphics slice.'}
        $offset=[Convert]::ToInt32($match.Groups['offset'].Value,16)
    }
    $rows.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($sprites -join ',')`t$($graphic.TileBase)`t$($graphic.Palette)`t$(Resolve-NpcAnimation $id $graphic.DefaultAnimation)`t$($offset.ToString('x4'))`tdata/ages/interactionData.s;data/ages/objectGfxHeaders.s")
    if (-not $animations.ContainsKey($id)) {
        $animations[$id]=@{}
        foreach ($index in 0..$spec[2]) {
            $animation=Resolve-NpcAnimation $id $index
            if (-not $animation) { throw "Maku Seed: missing INTERAC `$$($id.ToString('x2')) animation `$$($index.ToString('x2'))." }
            $animations[$id][$index]=$animation
            $animationRows.Add("$($id.ToString('x2'))`t$($index.ToString('x2'))`t$animation`tdata/ages/interactionAnimations.s")
        }
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/maku_seed_visuals.tsv'), $rows)
Write-GeneratedTable((Join-Path $destination 'cutscenes/maku_seed_animations.tsv'), $animationRows)
foreach ($spec in @(
    @('tree-seed',0x87,'makuTree_subid06Script_part1_body','makuTree_subid06Script_part2_body'),
    @('tree-return',0x87,'makuTree_subid06Script_part2_body','makuSprout_setAnimation'),
    @('cloaked',0x8d,'cloakedTwinrova_subid02Script_body','miscPuzzles_drawCrownDungeonOpeningFrame1'),
    @('twinrova',0x93,'twinrova_subid02Script_body','twinrova_subid04Script_body'),
    @('twinrova-linked',0x93,'twinrova_subid04Script_body','twinrova_subid06Script_body'),
    @('twinrova-return',0x93,'twinrova_subid06Script_body','patch_jump'),
    @('zelda',0xad,'zeldaSubid05Script_body','twinrovaInCutsceneScript_body'),
    @('impa',0x31,'impaScript6','impaScript7'),
    @('boy',0x3c,'boySubid0aScript','boySubid0bScript'))) {
    $scriptPath=if($spec[0] -in @('impa','boy')) {Join-Path $Disassembly 'scripts/ages/scripts.s'} else {$helper}
    $commands=@(Read-AssemblyCutsceneCommands $scriptPath $spec[2] $ops $spec[3])
    $bindings=@{
        'showloadedtext|'=@{Opcode='nativeyield';Payload='ShowLoadedText'};
        'addobjectbyte|Interaction.textID, $01'=@{Opcode='native';Payload='NextText'};
        'setglobalflag|GLOBALFLAG_SAW_TWINROVA_BEFORE_ENDGAME'=@{Opcode='setglobalflag';Arg0='13'}
    }
    foreach ($command in $commands) {
        if ($command.Opcode -eq 'asm15') {
            $handler=switch ($command.Operands) {
                'makuTree_spawnMakuSeed' {'SpawnSeed'}
                'makuTree_chooseTextAfterSeeingTwinrova' {'ChooseText'}
                'incMakuTreeState' {'IncMakuState'}
                'objectSetVisible82' {'Reveal'}
                'createExclamationMark, 40' {'Exclamation'}
                default {throw "Maku Seed: unsupported native $($command.Path):$($command.Line): $($command.Operands)."}
            }
            $bindings["asm15|$($command.Operands)"]=@{Opcode='native';Payload=$handler}
        } elseif ($command.Opcode -eq 'checkpalettefadedone') {
            $bindings['checkpalettefadedone|']=@{Opcode='gate';Payload='PaletteDone'}
        } elseif ($command.Opcode -eq 'playsound') {
            if (-not $symbols.ContainsKey($command.Operands)) {throw "Maku Seed: unknown sound $($command.Operands)."}
            $bindings["playsound|$($command.Operands)"]=@{Opcode='playsound';Arg0=([int]$symbols[$command.Operands]).ToString('x2')}
        }
    }
    Write-GeneratedTable((Join-Path $destination "cutscenes/maku_seed_$($spec[0])_commands.tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols $symbols -texts $texts `
            -animations $animations[[int]$spec[1]] -bindings $bindings -positions $allTextPositions -yieldOnJump $true))
}
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# text-id`tmessage-base64`tsource")
foreach ($id in @($texts.Keys | Sort-Object)) {
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($texts[$id])))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/maku_seed_texts.tsv'), $rows)
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# profile`tvalues`tsource")
$repeat=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scripts.s') 'makuTree_subid06Script_part3' `
    ([Collections.Generic.HashSet[string]]@('jumpifmemoryeq','rungenericnpclowindex')) 'makuSprout_subid00Script')
if ($repeat.Count -ne 3 -or $repeat[0].Operands -ne 'wIsLinkedGame, $01, @linked' -or
    $repeat[1].Operands -ne '<TX_055c' -or $repeat[2].Operands -ne '<TX_0560') {throw 'Maku Seed: changed subid $06 part3 text dispatch.'}
$rows.Add("repeat-text`t055c,0560`tscripts/ages/scripts.s:makuTree_subid06Script_part3")
$generic=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/common/commonScripts.s') 'genericNpcScript' `
    ([Collections.Generic.HashSet[string]]@('initcollisions','checkabutton','showloadedtext','scriptjump')))
if ($generic.Count -ne 4) {throw 'Maku Seed: changed genericNpcScript continuation.'}
$repeatRows=[Collections.Generic.List[string]]::new()
$repeatRows.Add($cutsceneCommandHeader)
$repeatRows.Add((New-CutsceneCommandRow $repeat[1].Script 0 $repeat[1].Label $repeat[1].Line 'nativeyield' '' '' '' 'LoadRepeatText'))
foreach ($command in $generic) {
    $actor=''; $arg0=''; $payload=''; $opcode=$command.Opcode
    if ($opcode -in @('initcollisions','checkabutton')) {$actor='Actor'}
    if ($opcode -eq 'showloadedtext') {$opcode='nativeyield'; $payload='ShowLoadedText'}
    # genericNpcScript remains ROM-resident: its backward jump continues.
    if ($opcode -eq 'scriptjump') {$arg0='2'}
    $repeatRows.Add((New-CutsceneCommandRow $command.Script ($command.Index+1) $command.Label $command.Line $opcode $actor $arg0 '' $payload))
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/maku_seed_tree-repeat_commands.tsv'), $repeatRows)
$values=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'object_code/ages/interactions/twinrova.s') '@data4' '.db' | ForEach-Object {$_.Operands -split '[,\s]+'} | Where-Object {$_} | ForEach-Object {Convert-AssemblyInteger $_})
if ($values.Count -ne 34) {throw 'Maku Seed: incomplete Twinrova arrival preset $04.'}
$rows.Add("twinrova-arrival`t$(($values | ForEach-Object {$_.ToString('x2')}) -join ',')`tobject_code/ages/interactions/twinrova.s:@data4")
$flashSource=Read-ImportText (Join-Path $Disassembly 'code/bank3Cutscenes.s')
$flashBlock=[regex]::Match($flashSource,'(?ms)screenFlashingData:\s+.*?@data1:\s*(?<rows>.*?)@data0:').Groups['rows'].Value
$values=@([regex]::Matches($flashBlock,'\$([0-9a-f]{2})') | ForEach-Object {[Convert]::ToInt32($_.Groups[1].Value,16)})
if (($values -join ',') -ne '2,4,6,8,10,12,255') {throw 'Maku Seed: changed flashScreen profile $01.'}
$rows.Add("reveal-flashes`t$(($values | ForEach-Object {$_.ToString('x2')}) -join ',')`tcode/bank3Cutscenes.s:screenFlashingData@data1")
foreach ($spec in @(@('twinrova-departure','object_code/ages/interactions/twinrova.s','@data5'),
    @('twinrova-linked-arrival','object_code/ages/interactions/twinrova.s','@data0'),
    @('z-oscillation','object_code/common/interactions/seasonsFairy.s','@zOffsets'))) {
    $values=@(Read-AssemblyDataDirectives (Join-Path $Disassembly $spec[1]) $spec[2] '.db' | ForEach-Object {$_.Operands -split '[,\s]+'} | Where-Object {$_} | ForEach-Object {Convert-AssemblyInteger $_})
    if ($values.Count -eq 0) {throw "Maku Seed: missing $($spec[1]):$($spec[2])."}
    $rows.Add("$($spec[0])`t$(($values | ForEach-Object {$_.ToString('x2')}) -join ',')`t$($spec[1]):$($spec[2])")
}
$agesFlashes=[regex]::Match($flashSource,'(?ms)^\.ifdef ROM_AGES\s+screenFlashingData:.*?(?=^\.else; ROM_SEASONS)').Value
$flash4=[regex]::Match($agesFlashes,'(?ms)@data4:(?<bytes>.*?)@data5:').Groups['bytes'].Value
$values=@([regex]::Matches($flash4,'\$([0-9a-f]{2})') | ForEach-Object {[Convert]::ToInt32($_.Groups[1].Value,16)})
if(($values -join ',') -ne '3,5,7,10,12,16,18,23,25,255'){throw 'Maku Seed: changed flashScreen profile $04.'}
$rows.Add("linked-flashes`t$(($values | ForEach-Object {$_.ToString('x2')}) -join ',')`tcode/bank3Cutscenes.s:screenFlashingData@data4")
Write-GeneratedTable((Join-Path $destination 'cutscenes/maku_seed_tables.tsv'), $rows)
Write-GeneratedBytes((Join-Path $destination 'cutscenes/maku_seed_palette.bin'), (Read-PaletteBytes 'paletteData4178' 4))

$miscPath=Join-Path $Disassembly 'code/ages/cutscenes/miscCutscenes.s'
$misc=Read-ImportText $miscPath
if($misc -notmatch '(?ms)^twinrovaRevealCutsceneHandler:.*?TX_2810.*?checkIsLinkedGame.*?<TX_2815.*?objectData77b2.*?@state3:.*?cp \$03.*?@state4:.*?ld a,\$12.*?cutscene_loadRoomObjectSetAndFadein.*?@state5:.*?cp \$ff.*?@state6:.*?inc \(hl\).*?cutscene_loadRoomObjectSetAndFadein.*?ld \(hl\),\$68.*?ld \(hl\),\$50') {
    throw 'Maku Seed: linked reveal dialogue, signal gates or room-set transitions changed.'
}
# @data is local to cutscene_disableLcdLoadRoomResetCamera; select its scope explicitly.
$roomBlock=[regex]::Match($misc,'(?ms)^cutscene_disableLcdLoadRoomResetCamera:.*?^@data:\s*(?<rows>.*?)(?=^\w+:)').Groups['rows'].Value
# A symbolic Zelda-room row precedes these entries, so retain original table indices.
$roomRows=@([regex]::Matches($roomBlock,'dwbe (?<room>\w+)') | ForEach-Object {$_.Groups['room'].Value})
$objectPath=Join-Path $Disassembly 'objects/ages/extraData3.s'
$objectSets=@(Read-AssemblyDataDirectives $objectPath 'objectTable2' '.dw' | ForEach-Object {$_.Operands})
$placements=[Collections.Generic.List[string]]::new();$placements.Add("# scene`torder`tid`tsubid`ty`tx`tsource")
$roomTable=[Collections.Generic.List[string]]::new();$roomTable.Add("# index`tgroup`troom`tscene`tsource")
foreach($index in @(0x12,0x13)) {
    if($roomRows[$index] -notmatch '^ROOM_AGES_(?<room>[0-9a-f]{3})$') {throw "Maku Seed: unresolved room-set `$$($index.ToString('x2'))."}
    $room=[Convert]::ToInt32($Matches['room'],16);$label=$objectSets[$index]
    $roomTable.Add("$($index.ToString('x2'))`t$(($room -shr 8).ToString('x2'))`t$(($room -band 255).ToString('x2'))`t$label`tcode/ages/cutscenes/miscCutscenes.s:cutscene_disableLcdLoadRoomResetCamera")
    $order=0
    foreach($node in @(Read-AssemblyMacroInvocations $objectPath $label)) {
        if($node.Name -eq 'obj_End'){continue}
        if($node.Name -ne 'obj_Interaction' -or $node.Operands.Count -notin @(2,4)){throw "Maku Seed: unsupported $label placement $($node.Name)."}
        $v=@($node.Operands | ForEach-Object {Convert-AssemblyInteger $_});$y=0;$x=0
        if($v.Count -eq 4){$y=$v[2];$x=$v[3]}
        $placements.Add("$label`t$order`t$($v[0].ToString('x2'))`t$($v[1].ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`tobjects/ages/extraData3.s:$label")
        $order++
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/maku_seed_rooms.tsv'),$roomTable)
Write-GeneratedTable((Join-Path $destination 'cutscenes/maku_seed_placements.tsv'),$placements)
