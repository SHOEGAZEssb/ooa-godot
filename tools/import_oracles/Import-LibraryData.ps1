# INTERAC_OLD_MAN and INTERAC_BOOK_OF_SEALS_PODIUM. Keep native tables
# separate from scripts, including the zero-terminated path lists.
$nativePath=Join-Path $Disassembly 'object_code/ages/interactions/oldMan.s'
$native=Read-ImportText $nativePath
if($native -notmatch '(?s)@runSubid02:.*?GLOBALFLAG_FINISHEDGAME.*?interactionAnimateAsNpc.*?interactionRunScript.*?SND_TELEPORT.*?m_HardcodedWarpA ROOM_AGES_5ec, \$00, \$17, \$03' -or
   $native -notmatch '(?s)@runSubid03:.*?interactionRunScript.*?interactionAnimateAsNpc.*?collisionRadiusX.*?\$06.*?GLOBALFLAG_WATER_POLLUTION_FIXED.*?@textIndices.*?@baseVariables.*?oldManScript_generic') {
    throw 'oldMan.s library initialization, script order, or warp changed.'
}
$base=@(Read-AssemblyDataDirectives $nativePath '@baseVariables' '.db' | ForEach-Object {$_.Operands | ForEach-Object {Convert-AssemblyInteger $_}})
$textNodes=@(Read-AssemblyDataDirectives $nativePath '@textIndices' '.db')
$texts=@($textNodes | ForEach-Object {$_.Operands | ForEach-Object {
    if($_ -notmatch '^<TX_([0-9a-f]{4})$'){throw "Invalid library text $_"}; [Convert]::ToInt32($Matches[1],16)
}})
if(($base -join ',') -ne '18,2,2,6,0,0,6,0,0,6,1,2' -or ($texts -join ',') -ne '13056,13057,13058,13059,13060,13061,13062,13063') {throw 'oldMan.s library table boundaries changed.'}
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# subid`tradius-y`tpalette`tanimation`tpolluted-text`tclean-text`tpolluted-message`tclean-message")
for($i=0;$i -lt 4;$i++) {
    $messages=@(0,1|ForEach-Object {[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((Resolve-OceanText $texts[$i*2+$_] ([Collections.Generic.HashSet[int]]::new()))))})
    $rows.Add("$((3+$i).ToString('x2'))`t$($base[$i*3].ToString('x2'))`t$($base[$i*3+1])`t$($base[$i*3+2])`t$($texts[$i*2].ToString('x4'))`t$($texts[$i*2+1].ToString('x4'))`t$($messages[0])`t$($messages[1])")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects/library_npcs.tsv'),$rows)

$scriptPath=Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$mainPath=Join-Path $Disassembly 'scripts/ages/scripts.s'
$ops=[Collections.Generic.HashSet[string]]@('initcollisions','checkabutton','disableinput','enableinput','enablemenu','showtext','showtextlowindex','showloadedtext','checktext','wait','scriptend','scriptjump','jumpifroomflagset','orroomflag','giveitem','setanimation','setangleandanimation','jumpifglobalflagset','setglobalflag','askforsecret','generatesecret','jumpifmemoryeq','jumpiftextoptioneq','callscript','retscript','asm15','playsound','checkpalettefadedone','makeabuttonsensitive','turntofacelink','jumpifitemobtained')
foreach($spec in @(
    @('library_book',$scriptPath,'oldManScript_givesBookOfSeals','oldManScript_givesFairyPowder'),
    @('library_powder',$scriptPath,'oldManScript_givesFairyPowder','mamamuYanRandomizeDogLocation'),
    @('library_secret',$scriptPath,'oldManScript_givesShieldUpgrade','oldManScript_givesBookOfSeals'),
    @('library_generic',$mainPath,'oldManScript_generic','mamamuYanScript'),
    @('library_podium',$mainPath,'bookOfSealsPodiumScript','vireSubid0Script'),
    @('library_reader_3b',$mainPath,'villagerGalSubid07Script','villagerGalSubid1And2Script_befored3',0x3b),
    @('library_reader_ab',$mainPath,'zoraSubid0eScript','zoraSubid10Script',0xab),
    @('library_reader_3c',$mainPath,'boySubid0bScript','boySubid0cScript',0x3c),
    @('library_reader_2a',$mainPath,'librarianScript','blossomScript0',0x2a),
    @('library_reader_9a',$mainPath,'carpenter_subid09Script','carpenter_talkedWhileWithBoss',0x9a))) {
    $commands=@(Read-AssemblyCutsceneCommands $spec[1] $spec[2] $ops $spec[3])
    if($spec[0] -eq 'library_secret') {
        $energy=@(Read-AssemblyCutsceneCommands $mainPath 'scriptFunc_doEnergySwirlCutscene' $ops)
        $offset=$commands.Count
        foreach($c in $energy){$c.Index+=$offset}
        $commands+=$energy
    }
    $targets=@{}
    foreach($c in $commands){if(!$targets.ContainsKey($c.Label)){$targets[$c.Label]=$c.Index}}
    $rows=[Collections.Generic.List[string]]::new(); $rows.Add($cutsceneCommandHeader)
    foreach($c in $commands) {
        $op=$c.Opcode;$args=$c.Operands.Trim();$parts=@($args.Split(',')|ForEach-Object{$_.Trim()})
        $actor='';$a='';$b='';$payload=''
        $animationId=if($spec.Count -gt 4){[int]$spec[4]}else{0x52}
        switch($op) {
            {$_ -in @('initcollisions','checkabutton','makeabuttonsensitive')} {$actor='Library'}
            'turntofacelink' {$op='native';$payload='FaceLink'}
            {$_ -in @('showtext','showtextlowindex')} {
                if($args -notmatch '^<?TX_([0-9a-f]{4})$'){throw "Invalid library text $args"}
                $op='showtext';$a=$Matches[1];$payload=Resolve-OceanText ([Convert]::ToInt32($a,16)) ([Collections.Generic.HashSet[int]]::new())
            }
            'wait' {$a=$args}
            'scriptjump' {
                $a=[string]$targets[$args]
                # scripts.s loads these helpers into wBigBuffer. Local
                # scriptCmd_jump relocation returns without carry; appended
                # ROM scripts retain their continuing jump.
                if($spec[1] -eq $scriptPath -and $c.Script -eq $spec[2] -and $args.StartsWith('@')){$op='scriptjumpyield'}
            }
            'callscript' {$target=$args -replace '^mainScripts\.','';if(!$targets.ContainsKey($target)){throw "Missing library call $args"};$a=[string]$targets[$target]}
            'retscript' {$op='return'}
            {$_ -in @('jumpifroomflagset','jumpiftextoptioneq')} {$a=$parts[0].Substring(1);$b=[string]$targets[$parts[1]]}
            'jumpifglobalflagset' {$op='jumpifmemoryeq';$a='01';$b=[string]$targets[$parts[1]];$payload='Global:'+([int]$globalFlagValues[$parts[0]])}
            'setglobalflag' {$a=([int]$globalFlagValues[$args]).ToString('x2')}
            'orroomflag' {$a=$args.Substring(1)}
            'setangleandanimation' {$op='setanimationcontinue';$actor='Library';$a=([int]([Convert]::ToInt32($args.Substring(1),16)/8)).ToString('x2');$payload=Resolve-NpcAnimation 0x52 ([Convert]::ToInt32($args.Substring(1),16)/8)}
            'setanimation' {
                $index=if($args -eq 'DIR_UP'){0}elseif($args -match '^\$([0-9a-f]{2})$'){[Convert]::ToInt32($Matches[1],16)}else{throw "Unknown library animation $args at line $($c.Line)"}
                $actor='Library';$a=$index.ToString('x2');$payload=Resolve-NpcAnimation $animationId $index
            }
            'giveitem' {
                $rewardName=($parts[0] -replace '^TREASURE_','TREASURE_OBJECT_')+'_'+$parts[1].Substring(1)
                $reward=$treasureObjectRecords[$rewardName];if(!$reward){throw "Unknown library reward $args"}
                $a=$reward.Treasure.ToString('x2');$b=$reward.SubId.ToString('x2')
            }
            'jumpifitemobtained' {if($parts[0] -ne 'TREASURE_BOOK_OF_SEALS'){throw "Unknown library item $args"};$op='jumpifmemoryeq';$a='01';$b=[string]$targets[$parts[1]];$payload='Book'}
            'askforsecret' {if($args -ne 'LIBRARY_SECRET'){throw "Unknown library secret $args"};$op='nativeyield';$payload='AskSecret'}
            'generatesecret' {if($args -ne 'LIBRARY_RETURN_SECRET'){throw "Unknown library return secret $args"};$op='nativeyield';$payload='GenerateSecret'}
            'jumpifmemoryeq' {if($parts[0] -ne 'wTextInputResult'){throw "Unknown library memory $args"};$a=$parts[1].Substring(1);$b=[string]$targets[$parts[2]];$payload='SecretResult'}
            'checkpalettefadedone' {$op='checkmemoryeq';$a='00';$payload='PaletteBusy'}
            'enablemenu' {$op='native';$payload='EnableMenu'}
            'playsound' {if($args -ne 'SND_FADEOUT'){throw "Unknown library sound $args"};$a='b4'}
            'asm15' {
                $op='native';$payload=$args -replace '^scriptHelp\.',''
                if($payload -notin @('oldManSetAnimationToVar38','oldManGiveShieldUpgradeToLink','oldManWarpLinkToLibrary','createSparkle','func_50e4','fadeoutToWhite','fadeinFromWhiteWithDelay, $04')){throw "Unknown library helper $args"}
            }
            {$_ -in @('disableinput','enableinput','enablemenu','showloadedtext','checktext','scriptend')} {}
            default {throw "Unsupported library command $op $args at line $($c.Line)"}
        }
        $rows.Add((New-CutsceneCommandRow $c.Script $c.Index $c.Label $c.Line $op $actor $a $b $payload))
    }
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes/$($spec[0])_commands.tsv"),$rows)
}

$podiumPath=Join-Path $Disassembly 'object_code/ages/interactions/bookOfSealsPodium.s'
$podium=Read-ImportText $podiumPath
if($podium -notmatch '(?s)interactionSetAlwaysUpdateBit.*?ld b,\$10.*?clearMemory.*?bit 6,a' -or
   $podium -notmatch '(?s)@replaceTiles:.*?interactionDecCounter1.*?ld \(hl\),\$02.*?call setTile.*?ret z' -or
   $podium -notmatch '(?s)ld a,\$d3.*?ld a,\$f4' -or
   $podium -notmatch '(?s)@spawnAllPodiums:.*?returnIfScrollMode01Unset.*?ld e,\$05.*?dec e') {throw 'bookOfSealsPodium.s native state machine changed.'}
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# subid`ty`tx`tpath`ttext-id`tmessage")
$positions=@(Read-AssemblyDataDirectives $podiumPath '@podiumPositions' '.db'|ForEach-Object{$_.Operands|ForEach-Object{Convert-AssemblyInteger $_}})
for($i=0;$i -lt 6;$i++) {
    $pathBytes=@(Read-AssemblyDataDirectives $podiumPath "@subid$i" '.db'|ForEach-Object{$_.Operands|ForEach-Object{Convert-AssemblyInteger $_}})
    if($pathBytes[-1] -ne 0){throw "Library book $i path lacks terminator"}
    $y=if($i -eq 0){0x84}else{$positions[(5-$i)*2]};$x=if($i -eq 0){0x78}else{$positions[(5-$i)*2+1]}
    $id=0x120b+$i;$message=Resolve-OceanText $id ([Collections.Generic.HashSet[int]]::new())
    $rows.Add("$i`t$($y.ToString('x2'))`t$($x.ToString('x2'))`t$($pathBytes -join ',')`t$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects/library_paths.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# id`tsubid`tsprite`ttile-base`tpalette`tanimation0`tanimation1`tanimation2`tanimation3")
foreach($pair in @(@(0x52,0),@(0xb4,0),@(0x80,7),@(0x80,8),@(0x3b,7),@(0xab,0x0e),@(0x3c,0x0b),@(0x2a,0),@(0x9a,9))) {
    $id=$pair[0];$sub=$pair[1];$g=$interactionGraphics["$id`:$sub"]
    if($null -eq $g){$g=$interactionGraphics["$id`:0"]}
    if($null -eq $g){throw "Library graphics missing for INTERAC `$$($id.ToString('x2')):`$$($sub.ToString('x2'))."}
    $anims=@(0..3|ForEach-Object{Resolve-NpcAnimation $id $_})
    if($id -eq 0x80){$anims[0]=Resolve-NpcAnimation $id $g.DefaultAnimation}
    $rows.Add("$($id.ToString('x2'))`t$($sub.ToString('x2'))`t$($gfxNames[$g.Gfx])`t$($g.TileBase)`t$($g.Palette)`t$($anims -join "`t")")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects/library_visuals.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# group`troom`torder`tid`tsubid`ty`tx")
$objectNodes=@(Read-AssemblyNodes (Join-Path $Disassembly 'objects/ages/mainData.s'))
$active=$false;$group=0;$room=0;$order=0
foreach($node in $objectNodes) {
    if($node.Kind -eq 'Label' -and $node.Name -match '^group([0-7])Map([0-9a-f]{2})ObjectData$'){$group=[int]$Matches[1];$room=[Convert]::ToInt32($Matches[2],16);$active=$true;$order=0}
    if(!$active -or $node.Kind -notin @('Instruction','MacroInvocation')){continue}
    if($node.Name -eq 'obj_End'){$active=$false;continue}
    if($node.Name -eq 'obj_Interaction' -and ($node.Operands -join ' ') -match '^\$(b4|80) \$(00|07|08) \$([0-9a-f]{2}) \$([0-9a-f]{2})$' -and $group -eq 5 -and $room -in @(0xe4,0xc8)) {
        $rows.Add("$group`t$($room.ToString('x2'))`t$order`t$($Matches[1])`t$($Matches[2])`t$($Matches[3])`t$($Matches[4])")
    }
    $order++
}
if($rows.Count -ne 4){throw 'Room 5:e4/5:c8 podium/decoration placements changed.'}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects/library_placements.tsv'),$rows)

# Keep the source initialization lifetime separate from per-conversation
# script branches. Only carpenter $09 uses the live pollution flag in script.
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# id`tsubid`tradius-y`tradius-x`tinitial-animation`talways-update`tsolid-mark`tbefore-text`tafter-text`tbefore-message`tafter-message")
foreach($reader in @(@(0x3b,7,0,0,0,0,1,0x1526,0x1527),@(0xab,0x0e,0,0,0,0,0,0x3433,0x3434),
    @(0x3c,0x0b,0,0,0,0,1,0x2519,0x251a),@(0x2a,0,0x0c,6,-1,0,0,0x2715,0x2716),@(0x9a,9,6,6,0,1,1,0,0))) {
    $id=$reader[0];$sub=$reader[1]
    $sourceName=switch($id){0x3b{'femaleVillager'}0xab{'zora'}0x3c{'boy'}0x2a{'librarian'}0x9a{'carpenter'}}
    $source=Read-ImportText (Join-Path $Disassembly "object_code/ages/interactions/$sourceName.s")
    if($source -notmatch 'interactionRunScript\s+jp interactionAnimateAsNpc' -and $id -ne 0x9a){throw "$sourceName.s library script/animation order changed."}
    if($id -eq 0x9a -and ($source -notmatch '(?s)@initSubid09:.*?objectMarkSolidPosition.*?interactionSetAlwaysUpdateBit' -or
        $source -notmatch '(?s)@runSubid:.*?interactionAnimateAsNpc.*?ld c,\$40.*?objectUpdateSpeedZ_paramC.*?interactionRunScript')){throw 'carpenter.s library native order/eligibility changed.'}
    foreach($text in @($reader[7],$reader[8]) | Where-Object {$_ -ne 0}) {
        if($source -notmatch "(?i)TX_$($text.ToString('x4'))"){throw "$sourceName.s lost library text TX_$($text.ToString('x4'))"}
    }
    $g=$interactionGraphics["$id`:$sub"];if($null -eq $g){$g=$interactionGraphics["$id`:0"]}
    $animation=if($reader[4] -ge 0){$reader[4]}else{$g.DefaultAnimation}
    $before=if($reader[7]){Resolve-OceanText $reader[7] ([Collections.Generic.HashSet[int]]::new())}else{''}
    $after=if($reader[8]){Resolve-OceanText $reader[8] ([Collections.Generic.HashSet[int]]::new())}else{''}
    $rows.Add("$($id.ToString('x2'))`t$($sub.ToString('x2'))`t$($reader[2].ToString('x2'))`t$($reader[3].ToString('x2'))`t$animation`t$($reader[5])`t$($reader[6])`t$($reader[7].ToString('x4'))`t$($reader[8].ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($before)))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($after)))")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects/library_readers.tsv'),$rows)
