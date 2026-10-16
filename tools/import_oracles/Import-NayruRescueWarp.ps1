# CUTSCENE_NAYRU_WARP_TO_MAKU_TREE and its three independent actor scripts.
$main = Join-Path $Disassembly 'scripts/ages/scripts.s'
$helper = Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$ops = [Collections.Generic.HashSet[string]]::new([string[]]@(
    'wait','setspeed','setangle','applyspeed','moveup','moveright','movedown','moveleft','setanimation',
    'checkmemoryeq','jumpifmemoryeq','writememory','scriptjump','scriptend','asm15','showtext','showtextlowindex',
    'showtextdifferentforlinked','playsound','resetmusic','disableinput','enableinput','setdisabledobjectsto11',
    'giveitem','spawninteraction','checkcfc0bit','checkpalettefadedone','writeobjectbyte','setcollisionradii',
    'makeabuttonsensitive','checkabutton','checktext','setmusic','checkobjectbyteeq','addobjectbyte'))
$symbols=@{'SPEED_080'=0x14;'SPEED_0c0'=0x1e;'SPEED_100'=0x28;'SPEED_180'=0x3c;'SPEED_200'=0x50;
    'DIR_UP'=0;'DIR_RIGHT'=1;'DIR_DOWN'=2;'DIR_LEFT'=3;'TREASURE_TUNE_OF_AGES'=0x27;
    'Interaction.animCounter'=0x60;'Interaction.direction'=0x48;'<TX_0556'=0x56;
    'wTmpcfc0.genericCutscene.cfd0'='RescueSignal';'wIsLinkedGame'='Linked';'wMakuMapTextPresent'='MakuMapText';
    'wTmpcbb5'='EscapeSignal';'wGenericCutscene.cbb5'='EscapeSignal';'w1Link.direction'='EscapeLinkDirection';
    'wScreenShakeCounterY'='EscapeShakeY';'wScreenShakeCounterX'='EscapeShakeX';'wScrollMode'='EscapeScroll';
    'Interaction.yh'=0x4b;'Interaction.xh'=0x4d;'Interaction.var3e'='EndingJump';
    'wUseSimulatedInput'='EndingInput';'w1Link.yh'='EndingLinkY';
    'wTmpcfc0.genericCutscene.state'='CreditsSignal'}
foreach($key in $soundIds.Keys) { $symbols[$key]=[int]$soundIds[$key] }
$musicConstants=Read-ImportText (Join-Path $Disassembly 'constants/common/music.s')
foreach($match in [regex]::Matches($musicConstants,'(?m)^\.define\s+(?<name>SNDCTRL_\w+)\s+\$(?<value>[0-9a-f]{2})')) {$symbols[$match.Groups['name'].Value]=[Convert]::ToInt32($match.Groups['value'].Value,16)}
foreach($match in [regex]::Matches($musicConstants,'(?m)^\s*(?<name>MUS_\w+)\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')) {$symbols[$match.Groups['name'].Value]=[Convert]::ToInt32($match.Groups['value'].Value,16)}
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# id`tsubid`tsprites`ttile-base`tpalette`tanimation`tsource")
$animationsById=@{}
$animationRows=[Collections.Generic.List[string]]::new()
$animationRows.Add("# id`tindex`tanimation`tsource")
foreach($spec in @(@(0x36,2,7),@(0x36,3,7),@(0x36,4,7),@(0x36,5,7),@(0x36,6,7),@(0x36,0x10,7),@(0x37,4,0x0b),@(0x37,5,3),@(0x37,6,3),@(0x37,7,3),@(0x37,8,0x0b),@(0x37,0x0e,3),@(0x5e,0,0x14),@(0x87,3,4),@(0x87,4,4),@(0x8d,0,3),@(0x3c,5,3),@(0x3d,3,3),@(0x39,4,8),@(0x39,6,8),@(0x93,0,3),@(0x93,1,3),@(0x63,0x3d,0x0a),@(0x63,0x3f,0x0a))) {
    $id=[int]$spec[0];$subid=[int]$spec[1];$graphic=Resolve-InteractionGraphics $id $subid
    if($null -eq $graphic) {throw "Nayru rescue: missing INTERAC 0x$($id.ToString('x2')):$subid graphics."}
    $sprites=@($gfxNames[$graphic.Gfx])
    if($id -in @(0x36,0x37,0x87)) { $sprites+=@($gfxNames[$graphic.Gfx+1]) }
    if($id -eq 0x93) {$sprites=@(0x2c..0x2e | ForEach-Object {$gfxNames[$_]})}
    foreach($sprite in $sprites) {Copy-EnemySprite $sprite}
    $rows.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($sprites -join ',')`t$($graphic.TileBase)`t$($graphic.Palette)`t$(Resolve-NpcAnimation $id $graphic.DefaultAnimation)`tdata/ages/interactionData.s")
    if(-not $animationsById.ContainsKey($id)) {
        $animationsById[$id]=@{}
        foreach($index in 0..$spec[2]) {
            $animation=Resolve-NpcAnimation $id $index
            if(-not $animation) {throw "Nayru rescue: missing INTERAC 0x$($id.ToString('x2')) animation $index."}
            $animationsById[$id][$index]=$animation
            $animationRows.Add("$($id.ToString('x2'))`t$($index.ToString('x2'))`t$animation`tdata/ages/interactionAnimations.s")
        }
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/nayru_rescue_visuals.tsv'),$rows)
Write-GeneratedTable((Join-Path $destination 'cutscenes/nayru_rescue_animations.tsv'),$animationRows)
$allRescueTexts=@{}
foreach($spec in @(
    @('nayru-arrival',0x36,$main,'nayruScript02_part1','nayruScript02_part2',$false),
    @('nayru-song',0x36,$helper,'nayruScript02_part2','nayruScript03',$true),
    @('nayru-farewell',0x36,$main,'nayruScript02_part3','nayruScript03',$false),
    @('ralph-arrival',0x37,$main,'ralphSubid04Script_part1','ralphSubid04Script_part2',$false),
    @('ralph-greeting',0x37,$main,'ralphSubid04Script_part2','ralphSubid04Script_part3',$false),
    @('ralph-farewell',0x37,$main,'ralphSubid04Script_part3','ralphSubid05Script',$false),
    @('nayru-escape',0x36,$helper,'nayruScript10','nayruScript11',$true),
    @('ralph-escape',0x37,$main,'ralphSubid0eScript','ralphSubid11Script',$false),
    @('nayru-ending',0x36,$helper,'nayruScript03','nayruScript07',$true),
    @('ralph-ending',0x37,$main,'ralphSubid05Script','ralphSubid06Script_part1',$false),
    @('nayru-palace',0x36,$main,'nayruScript04_part1','nayruScript04_part2',$false),
    @('nayru-palace-farewell',0x36,$main,'nayruScript04_part2','nayruScript05',$false),
    @('ralph-palace',0x37,$main,'ralphSubid06Script_part1','ralphSubid06Script_part2',$false),
    @('ralph-palace-farewell',0x37,$main,'ralphSubid06Script_part2','ralphSubid07Script',$false),
    @('nayru-credits-maku',0x36,$main,'nayruScript05','nayruScript07',$false),
    @('ralph-credits-maku',0x37,$main,'ralphSubid07Script','ralphSubid08Script',$false),
    @('ralph-credits-sword',0x37,$main,'ralphSubid08Script','ralphSubid09Script',$false),
    @('maku-credits',0x87,$main,'makuTree_subid04Script','makuTree_subid05Script',$false),
    @('maku',0x87,$main,'makuTree_subid03Script','makuTree_subid04Script',$false),
    @('cloaked',0x8d,$helper,'cloakedTwinrova_subid00Script_body','cloakedTwinrova_subid02Script_body',$true),
    @('twinrova',0x93,$helper,'twinrova_subid00Script_body','twinrova_subid02Script_body',$true),
    @('boy',0x3c,$main,'boySubid05Script','boySubid06Script',$false),
    @('old-lady',0x3d,$main,'oldLadySubid3Script','ghostVeranSubid0Script_part1',$false))) {
    $commands=@(Read-AssemblyCutsceneCommands $spec[2] $spec[3] $ops $spec[4])
    if($spec[0] -eq 'old-lady') {
        $loop=@(Read-AssemblyCutsceneCommands $main 'boyRunAroundHouse' $ops)
        foreach($command in $loop) {$command.Index+=$commands.Count}
        $commands+=$loop
    }
    $bindings=@{}
    foreach($command in $commands) {
        switch($command.Opcode) {
            'addobjectbyte' {
                if($command.Operands -ne 'Interaction.substate, $01'){throw "Nayru rescue: unsupported object increment '$($command.Operands)'."}
                $bindings["addobjectbyte|$($command.Operands)"]=@{Opcode='native';Payload='CreditsJump'}
            }
            'playsound' {
                if(-not $symbols.ContainsKey($command.Operands)) {throw "Nayru rescue: unknown sound '$($command.Operands)'."}
                $bindings["playsound|$($command.Operands)"]=@{Opcode='playsound';Arg0=([int]$symbols[$command.Operands]).ToString('x2')}
            }
            'jumpifmemoryeq' {
                $parts=@($command.Operands.Split(',') | ForEach-Object {$_.Trim()})
                if($parts[2] -ne '++') {
                    $target=@($commands | Where-Object {$_.Label -eq $parts[2]})[0]
                    if($null -eq $target) {throw "Nayru rescue: missing branch '$($parts[2])'."}
                    $bindings["jumpifmemoryeq|$($command.Operands)"]=@{Opcode='jumpifmemoryeq';Arg0=(Convert-AssemblyInteger $parts[1]).ToString('x2');Arg1=$target.Index.ToString();Payload=$symbols[$parts[0]]}
                }
            }
            {$_ -in @('showtext','showtextlowindex','showtextdifferentforlinked')} {
                foreach($match in [regex]::Matches($command.Operands,'TX_(?<id>[0-9a-f]{4})')) {
                    $id=[Convert]::ToInt32($match.Groups['id'].Value,16)
                    $allRescueTexts[$id]=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
                }
                if($command.Opcode -eq 'showtextdifferentforlinked') {
                    $ids=@([regex]::Matches($command.Operands,'TX_(?<id>[0-9a-f]{4})') | ForEach-Object {[Convert]::ToInt32($_.Groups['id'].Value,16)})
                    $bindings["$($command.Opcode)|$($command.Operands)"]=@{Opcode='showtextdifferentforlinked';Arg0=$ids[0].ToString('x4');Arg1=$ids[1].ToString('x4');Payload=$allRescueTexts[$ids[0]]+"`0"+$allRescueTexts[$ids[1]]}
                }
            }
            'checkpalettefadedone' {$bindings['checkpalettefadedone|']=@{Opcode='gate';Payload='PaletteDone'}}
            'checktext' {$bindings['checktext|']=@{Opcode='gate';Payload='TextDone'}}
            'setmusic' {$bindings["setmusic|$($command.Operands)"]=@{Opcode='nativeyield';Payload="Music:$($symbols[$command.Operands])"}}
            'checkcfc0bit' {
                if($command.Operands -ne '7') {throw 'Nayru rescue: unknown harp completion bit.'}
                $bindings['checkcfc0bit|7']=@{Opcode='gate';Payload='HarpDone'}
            }
            'setdisabledobjectsto11' {$bindings['setdisabledobjectsto11|']=@{Opcode='nativeyield';Payload='Disabled11'}}
            'spawninteraction' {
                if($spec[0] -eq 'nayru-escape' -and $command.Operands -eq 'INTERAC_CLINK, $80, $74, $78') {
                    $bindings["spawninteraction|$($command.Operands)"]=@{Opcode='nativeyield';Payload='EscapeClink'};break
                }
                if($command.Operands -ne 'INTERAC_PLAY_HARP_SONG, $02, $00, $00') {throw 'Nayru rescue: unknown actor spawn.'}
                $bindings["spawninteraction|$($command.Operands)"]=@{Opcode='nativeyield';Payload='HarpSong'}
            }
            'resetmusic' {$bindings['resetmusic|']=@{Opcode='nativeyield';Payload='ResetMusic'}}
            'asm15' {
                $handler=switch -Regex ($command.Operands) {
                    '^(?:scriptHelp\.)?forceLinkDirection, DIR_(?<direction>UP|RIGHT|DOWN|LEFT)$' { 'LinkDirection:'+(@('UP','RIGHT','DOWN','LEFT').IndexOf($Matches['direction']));break }
                    '^objectSetVisible(?:c2|82)$' {'Reveal';break}
                    '^scriptHelp.ralph_createExclamationMarkShiftedRight, \$28$' {'CreditsExclaim';break}
                    '^scriptHelp.turnToFaceSomethingAtInterval, \$03$' {'FaceTarget';break}
                    '^objectWritePositionTocfd5$' {'PublishTarget';break}
                    '^twinrova_writePositionWithXOffsetTocfd5, \$(?<offset>[0-9a-f]{2})$' {'PublishTarget:'+([Convert]::ToInt32($Matches['offset'],16));break}
                    default {throw "Nayru rescue: unsupported helper '$($command.Operands)' at $($command.Path):$($command.Line)."}
                }
                $bindings["asm15|$($command.Operands)"]=@{Opcode='native';Payload=$handler}
            }
        }
    }
    if($spec[0] -eq 'maku') {
        # The two ++ destinations belong to different forward branches.
        $greeting=@($commands | Where-Object {$_.Opcode -eq 'scriptend'})[0].Index+1
        $bindings['jumpifmemoryeq|wTmpcfc0.genericCutscene.cfd0, $03, ++']=@{Opcode='jumpifmemoryeq';Arg0='03';Arg1=$greeting.ToString();Payload='RescueSignal'}
        $afterUnlinked=@($commands | Where-Object {$_.Operands -eq '<TX_0554'})[0].Index+1
        $bindings['scriptjump|++']=@{Opcode='scriptjump';Arg0=$afterUnlinked.ToString()}
    }
    Write-GeneratedTable((Join-Path $destination "cutscenes/nayru_rescue_$($spec[0])_commands.tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols $symbols -animations $animationsById[[int]$spec[1]] `
            -texts $allRescueTexts -positions $allTextPositions -bindings $bindings -yieldOnJump $spec[5]))
}
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# text-id`tmessage-base64`tsource")
foreach($id in @(0x2800,0x2802)) {$allRescueTexts[$id]=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())}
foreach($id in @($allRescueTexts.Keys | Sort-Object)) {
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allRescueTexts[$id])))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/nayru_rescue_texts.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# profile`tvalues`tsource")
$monkey='object_code/ages/interactions/monkeyMain.s'
$sourceText=Read-ImportText (Join-Path $Disassembly $monkey)
$positionBlock=[regex]::Match($sourceText,'(?ms)^@monkeyPositions:\s*(?<rows>.*?)^@subid4Init:').Groups['rows'].Value
$positionBlock=$positionBlock -replace '(?ms)\.ifdef REGION_JP.*?\.else\s*','' -replace '\.endif',''
$values=@([regex]::Matches($positionBlock,'\$([0-9a-f]{2})') | ForEach-Object {[Convert]::ToInt32($_.Groups[1].Value,16)})
if($values.Count -ne 40) {throw 'Nayru rescue: incomplete clean-US monkey placement table.'}
$rows.Add("monkey-positions`t$(($values | ForEach-Object {$_.ToString('x4')}) -join ',')`t${monkey}:@monkeyPositions")
foreach($spec in @(@('monkey-jumps',$monkey,'@jumpSpeeds','.dw',4),@('twinrova-arrival','object_code/ages/interactions/twinrova.s','@data0','.db',0),@('twinrova-departure','object_code/ages/interactions/twinrova.s','@data1','.db',0),@('ralph-credits-sword','object_code/ages/interactions/twinrova.s','@data2','.db',34))) {
    # @data1 is an empty label alias immediately preceding @data5.
    $dataLabel=if($spec[0] -eq 'twinrova-departure') {'@data5'} else {$spec[2]}
    $values=@(Read-AssemblyDataDirectives (Join-Path $Disassembly $spec[1]) $dataLabel $spec[3] | ForEach-Object {$_.Operands} | ForEach-Object {Convert-AssemblyInteger $_})
    if($values.Count -eq 0 -or ($spec[4] -ne 0 -and $values.Count -ne $spec[4])) {throw "Nayru rescue: malformed $($spec[0]) table."}
    $rows.Add("$($spec[0])`t$(($values | ForEach-Object {($_ -band 0xffff).ToString('x4')}) -join ',')`t$($spec[1]):$($spec[2])")
}
$flashSource=Read-ImportText (Join-Path $Disassembly 'code/bank3Cutscenes.s')
$flashBlock=[regex]::Match($flashSource,'(?ms)screenFlashingData:\s+.*?@data2:\s+(?<rows>.*?)@data3:').Groups['rows'].Value
$values=@([regex]::Matches($flashBlock,'\$([0-9a-f]{2})') | ForEach-Object {[Convert]::ToInt32($_.Groups[1].Value,16)})
if($values.Count -ne 8) {throw 'Nayru rescue: incomplete flashScreen profile 2.'}
$rows.Add("arrival-flashes`t$(($values | ForEach-Object {$_.ToString('x4')}) -join ',')`tcode/bank3Cutscenes.s:screenFlashingData@data2")
$oscillationSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/seasonsFairy.s')
$values=@([regex]::Matches([regex]::Match($oscillationSource,'(?ms)^@zOffsets:\s*(?<rows>.*)').Groups['rows'].Value,'\$([0-9a-f]{2})') | ForEach-Object {[Convert]::ToInt32($_.Groups[1].Value,16)})
if($values.Count -ne 8) {throw 'Nayru rescue: incomplete objectOscillateZ profile.'}
$rows.Add("z-oscillation`t$(($values | ForEach-Object {$_.ToString('x4')}) -join ',')`tobject_code/common/interactions/seasonsFairy.s:@zOffsets")
$values=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'object_code/ages/interactions/accessory.s') '@data' '.db' | ForEach-Object {$_.Operands} | ForEach-Object {Convert-AssemblyInteger $_})
if($values.Count -ne 36) {throw 'Nayru rescue: incomplete bowtie attachment table.'}
$rows.Add("bowtie-offsets`t$(($values | ForEach-Object {$_.ToString('x4')}) -join ',')`tobject_code/ages/interactions/accessory.s:@data")
Write-GeneratedTable((Join-Path $destination 'cutscenes/nayru_rescue_tables.tsv'),$rows)
