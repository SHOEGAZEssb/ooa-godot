# Ralph $37:$0c is a copied script; Ambi $4d:$05 runs directly from ROM.
$helper=Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$main=Join-Path $Disassembly 'scripts/ages/scripts.s'
$native=Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/ralph.s')
if ($native -notmatch '(?ms)^@initSubid0c:.*?wGroup4RoomFlags\+\$fc.*?bit 7,\(hl\).*?interactionLoadExtraGraphics.*?ralph_createLinkedSwordAnimation.*?\$cfde.*?\$cfdf.*?call interactionRunScript\s+jr ralphRunSubid') {
    throw 'Black Tower Ralph $37:$0c initialization/order changed.'
}
$source=Read-ImportText $helper
if ($source -notmatch '(?ms)^ralph_beginHighJump:.*?\$fc.*?SND_JUMP.*?^ralph_updateGravity:\s+ld c,\$c0' -or
    $source -notmatch '(?ms)^ralph_flashScreen:.*?@thing0:.*?\$0a.*?clearFadingPalettes.*?@thing1:.*?@thing2:.*?fastFadeoutToWhite.*?@thing3:.*?\$14.*?clearFadingPalettes.*?@thing4:' -or
    $source -notmatch '(?ms)^ambiRiseUntilOffScreen:.*?sub \$04.*?cp \$c0') {
    throw 'Black Tower confrontation native jump/flash/disappearance contract changed.'
}
$rows=[Collections.Generic.List[string]]::new(); $rows.Add("# id`tsubid`tsprite`textra-sprite`ttile-base`tpalette`tdefault-animation`tsource")
$animationRows=[Collections.Generic.List[string]]::new(); $animationRows.Add("# id`tindex`tanimation`tsource")
$animations=@{}
foreach($spec in @(@(0x37,0x0c,12),@(0x4d,5,10),@(0x5e,0,4))) {
    $id=[int]$spec[0];$sub=[int]$spec[1];$g=$interactionGraphics["${id}:$sub"]
    if($null -eq $g){$g=$interactionGraphics["${id}:0"]}
    if($null -eq $g){throw "Black Tower missing graphics `${id}:`$sub."}
    $sprite=$gfxNames[$g.Gfx];Copy-EnemySprite $sprite
    $extra=if($id -eq 0x37){$gfxNames[$g.Gfx+1]}else{'-'}
    if($extra -ne '-'){Copy-EnemySprite $extra}
    $rows.Add("$($id.ToString('x2'))`t$($sub.ToString('x2'))`t$sprite`t$extra`t$($g.TileBase)`t$($g.Palette)`t$($g.DefaultAnimation)`tdata/ages/interactionData.s")
    $animations[$id]=@{}
    foreach($index in 0..([int]$spec[2])) {
        $encoded=Resolve-NpcAnimation $id $index
        if(-not $encoded){throw "Black Tower missing animation `${id}:`$index."}
        $animations[$id][$index]=$encoded
        $animationRows.Add("$($id.ToString('x2'))`t$($index.ToString('x2'))`t$encoded`tdata/ages/interactionAnimations.s")
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_confrontation_visuals.tsv'),$rows)
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_confrontation_animations.tsv'),$animationRows)
$ops=[Collections.Generic.HashSet[string]]@('initcollisions','jumpifroomflagset','disableinput','enableinput','spawninteraction','setmusic','wait','playsound','setanimation','setspeed','setangle','xorcfc0bit','applyspeed','showtext','asm15','jumpifmemoryset','scriptjump','setcoords','checkpalettefadedone','orroomflag','checkcfc0bit','resetmusic','checkabutton','writeobjectbyte','scriptend')
foreach($spec in @(@('ralph',$helper,'ralphSubid0cScript','monkey_decideTextIndex',0x37,$true),@('ambi',$main,'ambiSubid05Script','ambiSubid06Script',0x4d,$false))) {
    $commands=@(Read-AssemblyCutsceneCommands $spec[1] $spec[2] $ops $spec[3])
    $bindings=@{}
    foreach($command in $commands) {
        $key="$($command.Opcode)|$($command.Operands)"
        switch($command.Opcode) {
            {$_ -in @('playsound','setmusic')} {
                $pattern='(?m)^\.define\s+'+[regex]::Escape($command.Operands)+'\s+\$(?<value>[0-9a-f]{2})\s*$'
                $pseudo=[regex]::Match($musicConstantSource,$pattern)
                $sound=if($pseudo.Success){[Convert]::ToInt32($pseudo.Groups['value'].Value,16)}else{Resolve-SoundConstant $command.Operands}
                $bindings[$key]=@{Opcode=$command.Opcode;Arg0=$sound.ToString('x2')}
            }
            'spawninteraction' {
                if($command.Operands -ne 'INTERAC_AMBI, $05, $3c, $78'){throw 'Unexpected confrontation spawn.'}
                $bindings[$key]=@{Opcode='nativeyield';Payload='SpawnAmbi'}
            }
            'resetmusic' {$bindings[$key]=@{Opcode='nativeyield';Payload='ResetMusic'}}
            'orroomflag' {$bindings[$key]=@{Opcode='orroomflag';Arg0=([Convert]::ToInt32($command.Operands.Substring(1),16)).ToString('x2')}}
            'checkpalettefadedone' {$bindings[$key]=@{Opcode='gate';Payload='PaletteDone'}}
            'xorcfc0bit' {$bindings[$key]=@{Opcode='nativeyield';Payload="Toggle:$($command.Operands)"}}
            'checkcfc0bit' {$bindings[$key]=@{Opcode='checkmemoryeq';Arg0='01';Payload="Bit:$($command.Operands)"}}
            'jumpifmemoryset' {
                if($command.Operands -notmatch '^wcddb, \$(?<mask>80|10), (?<target>\w+|@\w+)$'){throw 'Unexpected confrontation conditional branch.'}
                $target=@($commands|Where-Object Label -eq $Matches.target)[0].Index
                $bindings[$key]=@{Opcode='jumpifmemoryeqyieldonmiss';Arg0='01';Arg1=$target.ToString();Payload="Flag:$($Matches.mask)"}
            }
            'asm15' {
                $handler=switch($command.Operands) {
                    'ralph_beginHighJump' {'Jump'}
                    'objectApplySpeed' {'Move'}
                    'ralph_updateGravity' {'Gravity'}
                    'ralph_flashScreen' {'Flash'}
                    'fadeinFromWhiteWithDelay, $04' {'FadeIn'}
                    'turnToFaceLink' {'FaceLink'}
                    'scriptHelp.ambiFlickerVisibility' {'Flicker'}
                    'scriptHelp.ambiDecVar3f' {'Decrement'}
                    'scriptHelp.ambiRiseUntilOffScreen' {'Rise'}
                    default {throw "Unsupported confrontation helper $($command.Operands)."}
                }
                $bindings[$key]=@{Opcode='native';Payload=$handler}
            }
        }
    }
    Write-GeneratedTable((Join-Path $destination "cutscenes/black_tower_confrontation_$($spec[0]).tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols @{'SPEED_080'=0x14;'SPEED_200'=0x50;'Interaction.var3f'=0x7f} `
            -texts $allTexts -positions $allTextPositions -animations $animations[[int]$spec[4]] -bindings $bindings -yieldOnJump $spec[5]))
}
