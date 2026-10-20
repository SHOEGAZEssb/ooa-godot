# Room $0:$65: independent copied-buffer Impa script and ROM bird/Zelda scripts.
$rescueHelper = Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$rescueMain = Join-Path $Disassembly 'scripts/ages/scripts.s'
$rescueTexts = @{}
foreach ($id in @(0x0127,0x0128,0x0129,0x012a,0x0603,0x0604,0x0608,0x3216,0x3217)) {
    $rescueTexts[$id] = Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
}
$rescueSymbols = @{
    'SPEED_100'=0x28; 'DIR_UP'=0; 'DIR_LEFT'=3; 'LINK_STATE_FORCE_MOVEMENT'=0x0b
    'wTmpcfc0.genericCutscene.cfd0'='RescueSignal'; 'w1Link.xh'='LinkX'
    'w1Link.direction'='LinkDirection'; 'w1Link.state'='LinkState'
    'wScrollMode'='ScrollMode'; 'Interaction.zh'='ZHigh'
}
foreach ($spec in @(
    @('Impa',0x31,'impaScript7',$rescueHelper,'greatFairyOctorok_createMagicPowderAnimation',$true),
    @('Bird',0x4c,'birdScript_zeldaKidnapped',$rescueMain,'',$false),
    @('Zelda',0xad,'zeldaSubid06Script',$rescueMain,'',$false))) {
    $actor=$spec[0]; $id=[int]$spec[1]
    $nativeFile = if ($actor -eq 'Impa') { 'impaInCutscene' } else { $actor.ToLowerInvariant() }
    $ops=[Collections.Generic.HashSet[string]]::new([string[]]@('initcollisions','setspeed','jumpifglobalflagset',
        'enableinput','disableinput','checkabutton','turntofacelink','showtext','setangle','applyspeed','setglobalflag',
        'scriptjump','checkpalettefadedone','writememory','wait','asm15','jumpifmemoryeq','checkmemoryeq',
        'setzspeed','playsound','checkobjectbyteeq','moveup','moveleft','movedown','setanimation','scriptend'))
    $commands=@(Read-AssemblyCutsceneCommands $spec[3] $spec[2] $ops $spec[4])
    $targets=@{}; foreach($c in $commands){if(-not $targets.ContainsKey($c.Label)){$targets[$c.Label]=$c.Index}}
    $bindings=@{
        'turntofacelink|'=@{Opcode='native';Payload='FaceLink'}
        'checkpalettefadedone|'=@{Opcode='gate';Payload='PaletteDone'}
        'asm15|impa_moveLinkUp32Frames'=@{Opcode='native';Payload='MoveLinkUp'}
        'asm15|impa_moveLinkRight8Frames'=@{Opcode='native';Payload='MoveLinkRight'}
        'setzspeed|-$0200'=@{Opcode='native';Payload='BeginJump'}
        'playsound|SND_JUMP'=@{Opcode='playsound';Arg0='53'}
    }
    foreach($c in $commands){
        if($c.Opcode -eq 'jumpifglobalflagset'){
            $parts=$c.Operands -split '\s*,\s*'
            if(-not $globalFlagValues.ContainsKey($parts[0])){throw "Unknown rescue flag $($parts[0])."}
            $bindings["$($c.Opcode)|$($c.Operands)"]=@{Opcode='jumpifmemoryeq';Arg0='01';Arg1=$targets[$parts[1]].ToString();Payload=('Flag:'+([int]$globalFlagValues[$parts[0]]).ToString('x2'))}
        } elseif($c.Opcode -eq 'setglobalflag') {
            $bindings["setglobalflag|$($c.Operands)"]=@{Opcode='setglobalflag';Arg0=([int]$globalFlagValues[$c.Operands]).ToString('x2')}
        } elseif($c.Opcode -eq 'jumpifmemoryeq') {
            $parts=$c.Operands -split '\s*,\s*'
            if($parts[0] -ne 'w1Link.state' -or $parts[1] -ne 'LINK_STATE_FORCE_MOVEMENT'){throw 'Unexpected rescue Link state branch.'}
            $bindings["jumpifmemoryeq|$($c.Operands)"]=@{Opcode='jumpifmemoryeq';Arg0='0b';Arg1=$targets[$parts[2]].ToString();Payload='LinkState'}
        }
    }
    $indices = if ($actor -eq 'Impa') { @(0,1,2,3,9) } elseif ($actor -eq 'Bird') { @(0,2) } else { @(0,1,2,3) }
    $animations=@{}
    foreach($index in $indices){
        $animations[$index]=Resolve-NpcAnimation $id $index
        if(-not $animations[$index]){throw "$nativeFile.s: missing executed rescue animation `$$($index.ToString('x2'))."}
    }
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes/zelda_rescue_$($actor.ToLower())_commands.tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor $actor -symbols $rescueSymbols -texts $rescueTexts `
            -animations $animations -bindings $bindings -yieldOnJump $spec[5]))
    $graphics=$interactionGraphics["$id`:0"]
    if($null -eq $graphics){throw "Rescue $actor lacks graphics."}
    $rows=[Collections.Generic.List[string]]::new()
    $rows.Add("# index`tsprite`ttile-base`tpalette`tanimation`tdefault-animation`tsource")
    foreach($index in $indices){
        $rows.Add("$index`t$($gfxNames[$graphics.Gfx])`t$($graphics.TileBase)`t$($graphics.Palette)`t$($animations[$index])`t$($graphics.DefaultAnimation)`tobject_code/ages/interactions/$nativeFile.s")
    }
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes/zelda_rescue_$($actor.ToLower())_animations.tsv"),$rows)
}
# Native operands are deliberately guarded against source drift; state-zero
# gates and positions are already imported by Import-NpcData.
$impaNative=Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/impaInCutscene.s')
$birdNative=Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/bird.s')
if($impaNative -notmatch '(?ms)^@init7:.*?ld a,\$81.*?wMenuDisabled.*?wDisabledObjects.*?wScrollMode.*?ld hl,\$cfd0.*?ld b,\$10.*?INTERAC_ZELDA, \$06.*?ld \(hl\),\$8c.*?ld \(hl\),\$50' -or
    $birdNative -notmatch '(?ms)^bird_runSubid4:.*?interactionAnimateAsNpc.*?bird_updateGravityAndHopWhenHitGround.*?interactionRunScript.*?cp \$68.*?inc a' -or
    (Resolve-SoundConstant 'SND_JUMP') -ne 0x53){throw 'Room $0:$65 native rescue contract changed.'}
