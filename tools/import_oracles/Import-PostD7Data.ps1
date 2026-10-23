# The seventh essence returns to $2:$90 before CUTSCENE_BLACK_TOWER_COMPLETE.
# Follow the completed tower, Ambi, the stone family, and the dynamic remote
# Maku handoff. Their temporary placements are absent from the normal NPC map.
& {
    $miscPath = Join-Path $Disassembly 'code/ages/cutscenes/miscCutscenes.s'
    $misc = (Read-ImportText $miscPath) -replace '(?m);[^\r\n]*',''
    $trigger = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/miscellaneous1.s')
    if ($trigger -notmatch '(?ms)^interaction6b_subid11:.*?bit 6,a.*?and \$40.*?ld a,\$01.*?wDisabledObjects.*?wMenuDisabled.*?CUTSCENE_BLACK_TOWER_COMPLETE' -or
        $misc -notmatch '(?ms)^blackTowerCompleteCutsceneHandler:.*?ld a,\$3c.*?fastFadeoutToBlack.*?checkDisableUnderwaterWaves.*?checkIsLinkedGame.*?ld \(hl\),\$1e.*?INTERAC_CLOAKED_TWINROVA \$01.*?ld a,\$10.*?cutscene_loadRoomObjectSetAndFadein.*?PALH_ac.*?ld a,\$28.*?hCameraX' -or
        $misc -notmatch '(?ms)^@cbb8_02:.*?@@state1:.*?ld a,\$04.*?wTmpcbbb.*?wTmpcbb6.*?@@table_6722:\s+\.db \$01 \$61 \$b1.*?m_HardcodedWarpA ROOM_AGES_4f6, \$0f, \$47, \$03' -or
        $misc -notmatch '(?ms)^turnToStoneCutsceneHandler:.*?ld a,\$0d.*?cp \$0f.*?ld bc, ROOM_AGES_290.*?objectData7798.*?ld \(hl\),\$58.*?ld \(hl\),\$50.*?DIR_DOWN') {
        throw 'post-D7: $6b:$11, Black Tower completion/scroll, or stone-scene return changed.'
    }
    $native = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/cloakedTwinrova.s')
    if ($native -notmatch '(?ms)^@initSubid1:.*?ld bc,\$4050.*?ld \(hl\),30.*?@subid1Substate0:.*?ld \(hl\),20.*?MUS_DISASTER.*?fadeinFromBlack.*?@subid1Substate1:.*?ld \(hl\),20.*?TX_2808.*?@subid1Substate3:.*?ld b,\$02.*?CUTSCENE_BLACK_TOWER_EXPLANATION') {
        throw 'post-D7: cloaked Twinrova $8d:$01 native waits, text, or flash changed.'
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# key`tvalues`tsource")
    foreach ($spec in @(
        @('entry','02,90,6b,11,40,40,3c','miscellaneous1.s:interaction6b_subid11;miscCutscenes.s:blackTowerCompleteCutsceneHandler'),
        @('unlinked','8d,01,40,50,1e,14,14,2808','cloakedTwinrova.s:@runSubid1'),
        @('linked','05,f1,10,28,1e','miscCutscenes.s:blackTowerCompleteCutsceneHandler'),
        @('tower','70,78,04,b1,78,04,f6,47,0f,03','miscCutscenes.s:blackTowerExplanationCutsceneHandler'),
        @('return','02,90,58,50,02','miscCutscenes.s:turnToStoneCutsceneHandler@state5')
    )) { $rows.Add("$($spec[0])`t$($spec[1])`t$($spec[2])") }
    $flashPath = Join-Path $Disassembly 'code/bank3Cutscenes.s'
    $agesFlashes=[regex]::Match((Read-ImportText $flashPath),'(?ms)^\.ifdef ROM_AGES\s+screenFlashingData:.*?(?=^\.else; ROM_SEASONS)')
    if(!$agesFlashes.Success){throw 'post-D7: missing Ages-only screenFlashingData boundary.'}
    foreach ($kind in @(1,2)) {
        $data=[regex]::Match($agesFlashes.Value,"(?m)^\s*@data${kind}:\s+\.db (?<bytes>[^\r\n]+)")
        $flash = @($data.Groups['bytes'].Value.Trim() -split '\s+' | ForEach-Object { Convert-AssemblyInteger $_ })
        if (($flash -join ',') -ne $(if($kind -eq 1){'2,4,6,8,10,12,255'}else{'2,4,6,8,10,12,14,255'})) { throw 'post-D7: flashScreen data boundary changed.' }
        $rows.Add("flash-$kind`t$(($flash | ForEach-Object {$_.ToString('x2')}) -join ',')`tcode/bank3Cutscenes.s:screenFlashingData@data$kind")
    }
    Write-GeneratedTable((Join-Path $destination 'cutscenes/post_d7_tables.tsv'),$rows)
    Write-GeneratedTable((Join-Path $destination 'cutscenes/post_d7_texts.tsv'),@(
        "# text-id`tmessage-base64`tsource",
        "2808`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x2808])))`ttext/ages/text.yaml:TX_2808"
    ))
    $rabbitPath=Join-Path $Disassembly 'object_code/ages/interactions/rabbitMain.s'
    $rabbitSource=Read-ImportText $rabbitPath
    foreach($contract in @(
        '(?ms)^@initSubid2:.*?ld a,180.*?rabbitSubid2SetRandomSpawnDelay',
        '(?ms)^rabbitSubid1:.*?ld a,30.*?@substate2:.*?ld \(hl\),60.*?@substate3:.*?interactionOscillateXRandomly.*?ld \(hl\),20.*?@substate6:.*?\$cfdf',
        '(?ms)^rabbitSubid2:.*?getRandomNumber_noPreserveVars.*?and \$07.*?rabbitSubid2YPositions.*?getRandomNumber.*?and \$0f.*?add \$b0',
        '(?ms)^spawnNextRabbitThatTurnsToStone:.*?ld \(hl\),95.*?@data:\s+\.db \$5a \$28 \$b8\s+\.db \$1e \$40 \$a8\s+\.db \$00 \$50 \$c8'
    )) {if($rabbitSource -notmatch $contract){throw 'post-D7: native rabbit-controller/spawn/stone contract changed.'}}
    $rabbitRows=[Collections.Generic.List[string]]::new();$rabbitRows.Add("# key`tvalues`tsource")
    foreach($spec in @(@('y-positions','rabbitSubid2YPositions',8),@('spawn-delays','rabbitSubid2SpawnDelays',4))) {
        $v=@(Read-AssemblyDataDirectives $rabbitPath $spec[1] '.db' | ForEach-Object {$_.Operands} | ForEach-Object {Convert-AssemblyInteger $_})
        if($v.Count -ne $spec[2]){throw 'post-D7: incomplete rabbit table.'}
        $rabbitRows.Add("$($spec[0])`t$(($v | ForEach-Object {$_.ToString('x2')}) -join ',')`trabbitMain.s:$($spec[1])")
    }
    $rabbitRows.Add("stone-spawns`t5a,28,b8,1e,40,a8,00,50,c8`trabbitMain.s:spawnNextRabbitThatTurnsToStone@data")
    Write-GeneratedTable((Join-Path $destination 'cutscenes/post_d7_rabbit_tables.tsv'),$rabbitRows)

    # Preserve each temporary object stream, including original placement order.
    $rows.Clear(); $rows.Add("# scene`torder`tid`tsubid`ty`tx`tvar03`tsource")
    foreach ($spec in @(
        @('linked','objects/ages/extraData3.s','objectData776f'),
        @('family','objects/ages/extraData3.s','objectData774f'),
        @('rabbit','objects/ages/extraData3.s','objectData7761'),
        @('sparkle','objects/ages/extraData3.s','objectData7765'),
        @('return','objects/ages/extraData3.s','objectData7798')
    )) {
        $stream = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly $spec[1]) $spec[2])
        $order=0
        foreach ($node in $stream) {
            if ($node.Name -eq 'obj_End') { continue }
            if ($node.Name -ne 'obj_Interaction' -or $node.Operands.Count -notin @(2,4,5)) { throw "post-D7: unsupported $($spec[2])/$order placement $($node.Name)." }
            $v=@($node.Operands | ForEach-Object {Convert-AssemblyInteger $_})
            $y=0; $x=0; $var03=0
            if($v.Count -ge 4){$y=$v[2];$x=$v[3]}; if($v.Count -eq 5){$var03=$v[4]}
            $rows.Add("$($spec[0])`t$order`t$($v[0].ToString('x2'))`t$($v[1].ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`t$($var03.ToString('x2'))`t$($spec[1]):$($spec[2])")
            $order++
        }
    }
    Write-GeneratedTable((Join-Path $destination 'cutscenes/post_d7_placements.tsv'),$rows)

    $rows.Clear(); $rows.Add("# id`tsubid`tsprite`ttile-base`tpalette`tdefault-animation`tsource-offset`tsource")
    $gfxHeaders=Read-ImportText (Join-Path $Disassembly 'data/ages/objectGfxHeaders.s')
    $animationsRows=[Collections.Generic.List[string]]::new(); $animationsRows.Add("# id`tindex`tanimation`tsource")
    $animationKeys=[Collections.Generic.HashSet[string]]::new()
    foreach ($spec in @(
        @(0x8d,1,@(0)), @(0xb0,0,@(0,1)), @(0xb0,1,@()), @(0xa9,4,@(0)),
        @(0x4d,3,@(0,1,2,3,10)), @(0x4d,4,@()), @(0x3c,6,@(0,1,2,3)),
        @(0x95,0,@(0)), @(0x3a,9,@(0,1)), @(0x43,3,@(0)), @(0x84,8,@(0,1)),
        @(0x4b,1,@(0,1,2,3,4,5,6)), @(0x4b,2,@())
    )) {
        $id=[int]$spec[0];$subid=[int]$spec[1]
        $graphic=$interactionGraphics["${id}:${subid}"]
        if($null -eq $graphic){$graphic=$interactionGraphics["${id}:0"]}
        if($null -eq $graphic){throw "post-D7: missing graphics for $($id.ToString('x2')):$($subid.ToString('x2'))."}
        $sprite=$gfxNames[$graphic.Gfx]
        $spritePath="gfx_compressible/ages/$sprite.png"
        if(!(Test-Path -LiteralPath (Join-Path $Disassembly $spritePath))){$spritePath="gfx_compressible/common/$sprite.png"}
        if(!(Test-Path -LiteralPath (Join-Path $Disassembly $spritePath))){$spritePath="gfx/common/$sprite.png"}
        Copy-GeneratedFile $spritePath "gfx/$sprite.png"
        $header=[regex]::Match($gfxHeaders, ('(?m)/\* \$'+$graphic.Gfx.ToString('x2')+' \*/ m_ObjectGfxHeader \w+(?:, \$?[0-9a-f]+, \$(?<offset>[0-9a-f]+))?\s*(?:;[^\r\n]*)?$'))
        if(!$header.Success){throw "post-D7: unsupported graphics header `$$( $graphic.Gfx.ToString('x2'))."}
        $offset=if($header.Groups['offset'].Success){[Convert]::ToInt32($header.Groups['offset'].Value,16)}else{0}
        $rows.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$($graphic.DefaultAnimation)`t$($offset.ToString('x4'))`tdata/ages/interactionData.s")
        foreach($index in @($spec[2]) + @($graphic.DefaultAnimation)) {
            if(!$animationKeys.Add("${id}:${index}")){continue}
            $animation=Resolve-NpcAnimation $id $index
            if(!$animation){throw "post-D7: missing animation $($id.ToString('x2'))/$index."}
            $animationsRows.Add("$($id.ToString('x2'))`t$($index.ToString('x2'))`t$animation`tdata/ages/interactionAnimations.s")
        }
    }
    Write-GeneratedTable((Join-Path $destination 'cutscenes/post_d7_visuals.tsv'),$rows)
    Write-GeneratedTable((Join-Path $destination 'cutscenes/post_d7_animations.tsv'),$animationsRows)

    $ops=[Collections.Generic.HashSet[string]]::new([string[]]@('wait','setspeed','movedown','moveleft','setanimation','showtext','showtextlowindex','writememory','checkmemoryeq','scriptend','jumpifobjectbyteeq','asm15','scriptjump','checkpalettefadedone','addobjectbyte','writeobjectbyte','orroomflag'))
    foreach($spec in @(
        @('linked','scriptHelper.s','twinrovaInCutsceneScript_body','',0xb0),
        @('ascent','scripts.s','ambiSubid03Script','ambiSubid04Script',0x4d),
        @('ambi','scripts.s','ambiSubid04Script','ambiSubid05Script',0x4d),
        @('boy','scripts.s','boySubid06Script','boySubid07Script',0x3c),
        @('father','scripts.s','villagerSubid09Script','script5bbf',0x3a),
        @('observer','scripts.s','pastGuySubid3Script','pastGuySubid6Script',0x43)
    )) {
        $commands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly "scripts/ages/$($spec[1])") $spec[2] $ops $spec[3])
        $animations=@{}; foreach($index in 0..10){$a=Resolve-NpcAnimation $spec[4] $index;if($a){$animations[$index]=$a}}
        $bindings=@{}; $texts=@{}
        foreach($command in $commands){
            $key="$($command.Opcode)|$($command.Operands)"
            if($command.Opcode -in @('showtext','showtextlowindex')) {
                if($command.Operands -notmatch '^<?TX_(?<id>[0-9a-f]{4})$'){throw "post-D7: unsupported text at $($command.Path):$($command.Line)."}
                $id=[Convert]::ToInt32($Matches.id,16)
                $texts[$id]=Resolve-OceanText $id ([Collections.Generic.HashSet[int]]::new())
            }
            switch($command.Opcode){
                'checkpalettefadedone' { $bindings[$key]=@{Opcode='gate';Payload='PaletteDone'} }
                'orroomflag' { $bindings[$key]=@{Opcode='orroomflag';Arg0=(Convert-AssemblyInteger $command.Operands).ToString('x2')} }
                'writememory' { if($command.Operands -eq 'wCutsceneTrigger, CUTSCENE_TURN_TO_STONE'){$bindings[$key]=@{Opcode='nativeyield';Payload='TurnToStone'}} }
                'jumpifobjectbyteeq' {
                    if($command.Operands -ne 'Interaction.var38, $00, @stopPlayingCatch'){throw 'post-D7: unsupported object branch.'}
                    $target=@($commands | Where-Object Label -eq '@stopPlayingCatch')[0].Index
                    $bindings[$key]=@{Opcode='jumpifmemoryeq';Arg0='00';Arg1=$target.ToString();Payload='BoyCatchCount'}
                }
                'addobjectbyte' {
                    if($command.Operands -ne 'Interaction.substate, $01'){throw 'post-D7: unsupported object increment.'}
                    $bindings[$key]=@{Opcode='native';Payload='EnableBoyAnimation'}
                }
                'asm15' {
                    $handler=switch -Regex ($command.Operands){
                        '^scriptHelp.loadNextAnimationFrameAndMore, \$(?<index>0[1-3])$' {"Throw:$($Matches.index)";break}
                        '^scriptHelp.boy_createLightning, \$(?<index>0[01])$' {"Lightning:$($Matches.index)";break}
                        '^fadeoutToWhite$' {'FadeOutWhite';break}
                        '^fadeinFromWhite$' {'FadeInWhite';break}
                        '^scriptHelp.createExclamationMark, \$28$' {'Exclaim';break}
                        default {throw "post-D7: unsupported helper $($command.Operands)."}
                    }
                    $bindings[$key]=@{Opcode='native';Payload=$handler}
                }
            }
        }
        $symbols=@{'SPEED_080'=(Resolve-ObjectSpeed '80');'SPEED_040'=(Resolve-ObjectSpeed '40');'SPEED_180'=(Resolve-ObjectSpeed '180');'wTmpcfc0.genericCutscene.state'='SharedState';'wTmpcfc0.genericCutscene.cfd1'='StoneSignal';'wTmpcfc0.genericCutscene.cfdf'='SceneDone';'Interaction.oamFlags'=0x5b}
        Write-GeneratedTable((Join-Path $destination "cutscenes/post_d7_$($spec[0])_commands.tsv"),
            (ConvertTo-CutsceneCommandRows $commands 'Actor' -symbols $symbols -texts $texts -positions $allTextPositions -animations $animations -bindings $bindings -yieldOnJump ($spec[0] -eq 'linked')))
    }
}
