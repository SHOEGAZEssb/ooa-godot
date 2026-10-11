# $32:$02, its $d5:$01 replacement, and CUTSCENE_CLEAN_SEAS $1e.
$fakeSource = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\fakeOctorok.s')
$fairySource = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\greatFairy.s')
$cleanSource = Read-ImportText (Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$partSource = Read-ImportText (Join-Path $Disassembly 'object_code\ages\parts\sparkle.s')
$zoraSource = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\zora.s')
foreach ($contract in @(
    @($mainObjectSource, '(?m)^group3MapefObjectData:\s+obj_Interaction \$32 \$02 \$28 \$50\s+obj_End'),
    @($fakeSource, '(?s)@init2:.*?GLOBALFLAG_WATER_POLLUTION_FIXED.*?ENEMY_GREAT_FAIRY.*?interactionDelete.*?ld bc,-\$80.*?mainScripts.greatFairyOctorokScript'),
    @($fakeSource, '(?s)greatFairyOctorokCode:.*?npcFaceLinkAndAnimate.*?interactionRunScript.*?xor a\s+call objectUpdateSpeedZ.*?cp \$f0.*?INTERAC_GREAT_FAIRY, \$01.*?TREASURE_FAIRY_POWDER.*?interactionDelete'),
    @($fairySource, '(?s)greatFairy_subid1:.*?SND_PIECE_OF_POWER.*?ld \(hl\),\$f0.*?ld a,180.*?ld \(hl\),\$02.*?INTERAC_SPARKLE, \$04.*?ld \(hl\),120.*?INTERAC_SPARKLE, \$0a.*?add \$04'),
    @($fairySource, '(?s)@substate0:.*?ld \(hl\),\$40.*?ld \(hl\),\$08.*?SPEED_300.*?TX_4109.*?@substate1:.*?retIfTextIsActive.*?objectApplySpeed.*?ld \(hl\),\$02.*?and \$1f.*?@substate2:.*?sub \$02.*?cp \$b0.*?fadeoutToWhite.*?CUTSCENE_CLEAN_SEAS'),
    @($cleanSource, '(?s)func_03_7619:.*?ROOM_AGES_1a5.*?ld a,\$f0.*?and \$07.*?PART_SPARKLE.*?getRandomNumber.*?and \$7f.*?ld \(hl\),\$78.*?fadeoutToWhiteWithDelay.*?ROOM_AGES_1d2.*?ROOM_AGES_3b1.*?objectData7e71.*?ROOM_AGES_3b0.*?objectData7e7b.*?ROOM_AGES_1a3.*?ld \(hl\),\$38.*?ld \(hl\),\$68.*?setLinkForceStateToState08.*?SND_SOLVEPUZZLE_2.*?GLOBALFLAG_WATER_POLLUTION_FIXED.*?setDeathRespawnPoint'),
    @($cleanSource, '(?s)func_782a:\s+ld a,\$eb.*?and \$40.*?ld a,\$fc.*?ld a,\$3a.*?call setTile'),
    @($partSource, '(?s)table_6114:\s+\.db \$fa \$ff \$56 \$0c\s+\.db \$f7 \$ff \$54 \$0a\s+\.db \$f2 \$ff \$5c \$0e\s+\.db \$f5 \$ff \$58 \$10'),
    @($zoraSource, '(?s)zora_subid0C:.*?ld c,\$20.*?objectUpdateSpeedZ_paramC.*?ret nz.*?interactionRunScript.*?set 7,\(hl\).*?interactionAnimate.*?SPEED_180'),
    @($zoraSource, '(?s)zora_subid0A:.*?ld \(hl\),30.*?ld \(hl\),120.*?ld \(hl\),90.*?ld \(hl\),20.*?@animationTable:\s+\.db \$00 \$03 \$01 \$02 \$00 \$01 \$03 \$02\s+\.db \$03 \$01 \$03 \$00 \$03 \$02 \$00 \$01.*?set 7,\(hl\).*?ld c,\$20.*?<\(-\$180\)')
)) { if ($contract[0] -notmatch $contract[1]) { throw "Sea fairy native source contract changed: $($contract[1])" } }

$scriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$supported = [Collections.Generic.HashSet[string]]@('initcollisions','checkabutton','jumpifglobalflagset',
    'showtextlowindex','setglobalflag','scriptjump','jumpifitemobtained','setdisabledobjectsto11',
    'disablemenu','asm15','wait','scriptend')
$commands = @(Read-AssemblyCutsceneCommands $scriptPath 'greatFairyOctorokScript' $supported 'childScript00')
if ($commands.Count -ne 15) { throw "greatFairyOctorokScript expected 15 commands, got $($commands.Count)." }
$targets = @{}
foreach ($command in $commands) { if (!$targets.ContainsKey($command.Label)) { $targets[$command.Label] = $command.Index } }
$rows = [Collections.Generic.List[string]]::new(); $rows.Add($cutsceneCommandHeader)
foreach ($command in $commands) {
    $op=$command.Opcode; $args=$command.Operands.Trim(); $parts=@($args.Split(',') | ForEach-Object {$_.Trim()})
    $actor=''; $a=''; $b=''; $payload=''
    switch ($op) {
        {$_ -in @('initcollisions','checkabutton')} {$actor='Octorok'}
        'jumpifglobalflagset' {$op='jumpifmemoryeq';$a='01';$b=[string]$targets[$parts[1]];$payload='Talked'}
        'jumpifitemobtained' {if($parts[0] -ne 'TREASURE_FAIRY_POWDER'){throw "Sea fairy treasure $args"};$op='jumpifmemoryeq';$a='01';$b=[string]$targets[$parts[1]];$payload='Powder'}
        'showtextlowindex' {if($args -notmatch '^<TX_(410[678])$'){throw "Sea fairy text $args"};$a=$Matches[1];$op='showtext';$payload=Resolve-OceanText ([Convert]::ToInt32($a,16)) ([Collections.Generic.HashSet[int]]::new())}
        'setglobalflag' {if($args -ne 'GLOBALFLAG_TALKED_TO_OCTOROK_FAIRY'){throw "Sea fairy flag $args"};$a=([int]$globalFlagValues[$args]).ToString('x2')}
        'scriptjump' {$a=[string]$targets[$args]}
        'setdisabledobjectsto11' {$op='setdisabledobjects';$a='11'}
        'asm15' {if($args -ne 'scriptHelp.greatFairyOctorok_createMagicPowderAnimation'){throw "Sea fairy helper $args"};$op='native';$payload='MagicPowder'}
        'wait' {$a=$args}
        {$_ -in @('disablemenu','scriptend')} {}
        default {throw "Sea fairy opcode $op"}
    }
    $rows.Add((New-CutsceneCommandRow $command.Script $command.Index $command.Label $command.Line $op $actor $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\sea_fairy_commands.tsv'),$rows)

foreach ($subid in @(0x0c,0x0d)) {
    $script='zoraSubid'+$subid.ToString('x2')+'Script'
    $allowed=[Collections.Generic.HashSet[string]]@('wait','setanimation','asm15','movedown','moveright','moveup','moveleft','xorcfc0bit','scriptend','scriptjump')
    $end=if($subid -eq 0x0c){'zoraSubid0dScript'}else{'zoraSubid0eScript'}
    $zoraCommands=@(Read-AssemblyCutsceneCommands $scriptPath $script $allowed $end)
    if($subid -eq 0x0d) {
        $tail=@(Read-AssemblyCutsceneCommands $scriptPath 'zoraScript_end' $allowed 'zoraSubid0dScript')
        foreach($command in $tail){$command.Index+=$zoraCommands.Count}
        $zoraCommands+=$tail
    }
    $zoraTargets=@{}; foreach($command in $zoraCommands){if(!$zoraTargets.ContainsKey($command.Label)){$zoraTargets[$command.Label]=$command.Index}}
    $zoraRows=[Collections.Generic.List[string]]::new();$zoraRows.Add($cutsceneCommandHeader)
    foreach($command in $zoraCommands) {
        $op=$command.Opcode;$args=$command.Operands.Trim();$actor='';$a='';$b='';$payload=''
        switch($op) {
            'wait' {$a=$args}
            'setanimation' {$actor='Zora';$a=$args.Substring(1);$payload=Resolve-NpcAnimation 0xab ([Convert]::ToInt32($a,16))}
            'asm15' {if($args -notin @('scriptHelp.zora_createExclamationMark','scriptHelp.zora_beginJump')){throw "Clean-seas Zora helper $args"};$op='native';$payload=$args -replace '^scriptHelp\.',''}
            {$_ -in @('movedown','moveright','moveup','moveleft')} {$angle=@{moveup=0;moveright=8;movedown=16;moveleft=24}[$op];$op='move';$actor='Zora';$a=$angle.ToString('x2');$b=$args.Substring(1);$payload=Resolve-NpcAnimation 0xab ($angle/8)}
            'xorcfc0bit' {if($args -ne '7'){throw "Clean-seas Zora signal $args"};$op='nativeyield';$payload='ToggleSignal'}
            'scriptjump' {$a=[string]$zoraTargets[$args]}
            'scriptend' {}
            default {throw "Clean-seas Zora opcode $op"}
        }
        $zoraRows.Add((New-CutsceneCommandRow $command.Script $command.Index $command.Label $command.Line $op $actor $a $b $payload))
    }
    Write-CutsceneGeneratedTable((Join-Path $destination ('cutscenes\clean_seas_zora_'+$subid.ToString('x2')+'.tsv')),$zoraRows)
}

$visualRows=[Collections.Generic.List[string]]::new()
$visualRows.Add("# role`tid`tsubid`tsprite`ttile-base`tpalette`tanimation-index`tanimation`tsource-offset")
$gfxSource = Read-ImportText (Join-Path $Disassembly 'data\ages\objectGfxHeaders.s')
foreach ($spec in @(@('fairy',0xd5,1),@('flash',0x84,4),@('ray',0x84,0x0a),@('trail',0x84,2),@('zora',0xab,0x0c))) {
    $graphic=$interactionGraphics["$($spec[1]):$($spec[2])"]
    if(!$graphic){$graphic=$interactionGraphics["$($spec[1]):0"]}
    if(!$graphic){throw "Sea fairy graphics missing $spec"}
    $header=[regex]::Match($gfxSource, ('(?m)/\* \$'+$graphic.Gfx.ToString('x2')+' \*/ m_ObjectGfxHeader \w+(?:, \$?[0-9a-f]+, \$(?<offset>[0-9a-f]+))?\s*(?:;[^\r\n]*)?$'))
    if(!$header.Success){throw "Sea fairy graphics header missing $spec"}
    $offset=if($header.Groups['offset'].Success){[Convert]::ToInt32($header.Groups['offset'].Value,16)}else{0}
    $indices=if($spec[0] -eq 'zora'){@(0,1,2,3)}else{@($graphic.DefaultAnimation)}
    foreach($index in $indices) {
        $animation=Resolve-NpcAnimation $spec[1] $index
        if(!$animation){throw "Sea fairy animation missing $spec index $index"}
        $visualRows.Add("$($spec[0])`t$(([int]$spec[1]).ToString('x2'))`t$(([int]$spec[2]).ToString('x2'))`t$($gfxNames[$graphic.Gfx])`t$($graphic.TileBase)`t$($graphic.Palette)`t$index`t$animation`t$offset")
    }
}
$partAnimation=Resolve-PartAnimation 0x26 0
if(!$partAnimation){throw 'PART_SPARKLE $26 animation missing.'}
$visualRows.Add("powder`t26`t00`t$($gfxNames[0x6b])`t12`t1`t0`t$partAnimation`t0")
Write-CutsceneGeneratedTable((Join-Path $destination 'objects\sea_fairy_visuals.tsv'),$visualRows)
$texts=@("# text-id`tutf8-base64`tsource", "4109`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((Resolve-OceanText 0x4109 ([Collections.Generic.HashSet[int]]::new())))))`tgreatFairy.s:subid1")
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\sea_fairy_text.tsv'),$texts)
# Preserve ordered native rows, with the guarded source identity above.
$nativeRows=@("# kind`tvalues`tsource",
    "powder-motion`t-6:86:12,-9:84:10,-14:92:14,-11:88:16`tparts/sparkle.s:table_6114",
    "rooms`t1:a5,1:d2,3:b1,3:b0,1:a3`tmiscCutscenes.s:func_03_7619",
    "timing`t180,120,64,2,240,120,60`tgreatFairy.s;miscCutscenes.s:func_03_7619")
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\sea_fairy_native.tsv'),$nativeRows)
