# King Zora and the ocean Zoras retain their original script dispatch and native tables.
$path = Join-Path $Disassembly 'scripts\ages\scripts.s'
$supported = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @('checkabutton','showtext','showtextnonexitable','jumpiftextoptioneq',
    'orroomflag','disableinput','giveitem','wait','enableinput','scriptjump','asm15',
    'spawninteraction','initcollisions','jumpifitemobtained','rungenericnpc','incstate',
    'setspeed','setangle','moveup','moveright','movedown','moveleft','scriptend','jumptable_memoryaddress',
    'jumpifglobalflagset','setglobalflag','askforsecret','generatesecret','callscript','retscript',
    'jumptable_objectbyte','playsound','checkpalettefadedone','jumpifmemoryeq')) {
    [void]$supported.Add($opcode)
}
foreach ($spec in @(
    @('king_zora_present','kingZoraScript_present_firstTime','kingZoraScript_past_dontHavePotion','KingZora',0x9c),
    @('king_zora_past','kingZoraScript_past_dontHavePotion','tokkeyScript','KingZora',0x9c),
    @('zora_scale','zoraSubid10Script','zoraSubid11And12Script','Zora',0xab),
    @('zora_storm_guard','zoraSubid11And12Script','zeldaSubid00Script','Zora',0xab))) {
    $commands = @(Read-AssemblyCutsceneCommands $path $spec[1] $supported $spec[2])
    if($spec[0] -eq 'king_zora_present') {
        $energy=@(Read-AssemblyCutsceneCommands $path 'scriptFunc_doEnergySwirlCutscene' $supported)
        $base=$commands.Count
        foreach($c in $energy){$c.Index+=$base}
        $commands+=$energy
    }
    $targets = @{}
    foreach ($command in $commands) {
        if (!$targets.ContainsKey($command.Label)) { $targets[$command.Label] = $command.Index }
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add($cutsceneCommandHeader)
    foreach ($command in $commands) {
        $op=$command.Opcode; $args=$command.Operands.Trim(); $parts=@($args.Split(',') | ForEach-Object { $_.Trim() })
        $actor=''; $a=''; $b=''; $payload=''
        switch ($op) {
            { $_ -in @('checkabutton','initcollisions') } { $actor=$spec[3] }
            { $_ -in @('showtext','showtextnonexitable') } {
                if ($args -notmatch '^TX_([0-9a-f]{4})$') { throw "Invalid ocean text $args" }
                $a=$Matches[1]; $payload=Resolve-OceanText ([Convert]::ToInt32($a,16)) ([Collections.Generic.HashSet[int]]::new()); $op='showtext'
                if (!$payload) { throw "Missing ocean dialogue TX_$a" }
            }
            'jumpiftextoptioneq' { $a=$parts[0].Substring(1); $b=[string]$targets[$parts[1]] }
            'orroomflag' { $a=$args.Substring(1) }
            'wait' { $a=$args }
            # These actors retain ROM script pointers, so scriptCmd_jump's
            # ROM branch returns carry set and executes its destination now.
            'scriptjump' { $a=[string]$targets[$args] }
            'callscript' { $a=[string]$targets[$args] }
            'retscript' { $op='return' }
            'jumpifglobalflagset' { $op='jumpifmemoryeq';$a='01';$b=[string]$targets[$parts[1]];$payload='Global:'+([int]$globalFlagValues[$parts[0]]) }
            'setglobalflag' { $a=([int]$globalFlagValues[$args]).ToString('x2') }
            'askforsecret' { if($args -ne 'KING_ZORA_SECRET'){throw "Ocean secret $args"};$op='nativeyield';$payload='AskSecret' }
            'generatesecret' { if($args -ne 'KING_ZORA_RETURN_SECRET'){throw "Ocean secret $args"};$op='nativeyield';$payload='GenerateSecret' }
            'jumpifmemoryeq' { if($parts[0] -ne 'wTextInputResult'){throw "Ocean memory $args"};$a=$parts[1].Substring(1);$b=[string]$targets[$parts[2]];$payload='SecretResult' }
            'checkpalettefadedone' { $op='checkmemoryeq';$a='00';$payload='PaletteBusy' }
            'playsound' { if($args -ne 'SND_FADEOUT'){throw "Ocean sound $args"};$a='b4' }
            'jumptable_objectbyte' {
                if($args -ne 'Interaction.var03'){throw "Ocean object table $args"}
                $op='jumptablememoryyield';$payload='SwordBranch|'+[string]$targets['@giveLevel3']+','+[string]$targets['@giveLevel2']
            }
            'giveitem' {
                $reward=$treasureObjectRecords[$args]
                if ($null -eq $reward) { throw "Unknown ocean reward $args" }
                $a=$reward.Treasure.ToString('x2'); $b=$reward.SubId.ToString('x2')
            }
            'asm15' {
                $op='native'; $payload=$args -replace '^scriptHelp\.',''
                if ($parts[0] -eq 'setGlobalFlag') {
                    $op='setglobalflag'; $a=([int]$globalFlagValues[$parts[1]]).ToString('x2'); $payload=''
                } elseif ($args -eq 'playSound, SND_NONE') { $op='playsound'; $a='00'; $payload='' }
                elseif ($payload -notin @('loseTreasure, TREASURE_POTION','zora_waitForLinkToMoveDown',
                    'createSparkle','func_50e4','fadeoutToWhite','fadeinFromWhiteWithDelay, $04',
                    'zora_makeLinkFaceDown','zora_moveToLinksXPosition','zora_createExclamationMarkToTheRight',
                    'zora_checkIsLinkedGame','zora_setLinkDirectionUp','zora_setLinkDirectionRight')) {
                    throw "Unknown ocean helper $args"
                }
            }
            'spawninteraction' {
                if ($args -ne 'INTERAC_KING_ZORA, $02, $34, $78') { throw "Unknown ocean child $args" }
                $op='native'; $payload='kingZora_createPotion'
            }
            'incstate' { $op='nativeyield'; $payload='zora_incState' }
            'setspeed' { $actor=$spec[3]; $a=(Resolve-ObjectSpeed ($args -replace '^SPEED_','')).ToString('x2') }
            'setangle' { $actor=$spec[3]; $a=$args.Substring(1) }
            { $_ -in @('moveup','moveright','movedown','moveleft') } {
                $angle=@{moveup=0;moveright=8;movedown=16;moveleft=24}[$op]
                $op='move'; $actor=$spec[3]; $a=$angle.ToString('x2'); $b=$args.Substring(1)
                $payload=Resolve-NpcAnimation $spec[4] ($angle/8)
            }
            'jumpifitemobtained' {
                if ($parts[0] -ne 'TREASURE_ZORA_SCALE') { throw "Unknown ocean treasure $args" }
                $op='jumpifmemoryeq'; $a='01'; $b=[string]$targets[$parts[1]]; $payload='HasZoraScale'
            }
            'jumptable_memoryaddress' {
                if ($args -ne 'wTmpcfc0.genericCutscene.cfc1') { throw "Unknown ocean jump table $args" }
                $op='jumptablememoryyield'
                $labels=if ($spec[0] -eq 'zora_scale') { @('zoraSubid10Script','@linkMovedDown') } else { @('@unlinked','@linked') }
                $tableNodes=@(Read-AssemblyNodes $path | Where-Object { $_.Line -gt $command.Line -and $_.Kind -ne 'Blank' -and $_.Kind -ne 'Comment' } | Select-Object -First 2)
                for ($i=0;$i -lt 2;$i++) {
                    if ($tableNodes[$i].Kind -ne 'Data' -or $tableNodes[$i].OperandText -ne $labels[$i]) { throw "Ocean branch table changed at line $($command.Line)" }
                }
                $payload='OceanSignal|'+[string]$targets[$labels[0]]+','+[string]$targets[$labels[1]]
            }
            'rungenericnpc' {
                if ($args -ne 'TX_3431') { throw "Unknown ocean generic script $args" }
                $op='scriptjump'; $a=[string]$commands.Count
            }
            { $_ -in @('disableinput','enableinput','scriptend') } { }
            default { throw "Unsupported ocean command $op $args at line $($command.Line)" }
        }
        $rows.Add((New-CutsceneCommandRow $command.Script $command.Index $command.Label $command.Line $op $actor $a $b $payload))
    }
    if ($spec[0] -eq 'zora_storm_guard') {
        $genericPath=Join-Path $Disassembly 'scripts\common\commonScripts.s'
        $generic=@(Read-AssemblyCutsceneCommands $genericPath 'genericNpcScript' ([Collections.Generic.HashSet[string]]@('initcollisions','checkabutton','showloadedtext','scriptjump')))
        if ($generic.Count -ne 4) { throw 'genericNpcScript ocean continuation changed.' }
        foreach ($command in $generic) {
            $index=$commands.Count+$command.Index; $actor='';$a='';$payload='';$op=$command.Opcode
            if ($op -in @('initcollisions','checkabutton')) { $actor='Zora' }
            if ($op -eq 'showloadedtext') { $op='showtext';$a='3431';$payload=$allTexts[0x3431] }
            if ($op -eq 'scriptjump') { $a=[string]($commands.Count+1) }
            $rows.Add((New-CutsceneCommandRow $command.Script $index $command.Label $command.Line $op $actor $a '' $payload))
        }
    }
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes\$($spec[0])_commands.tsv"),$rows)
}
$graphic=$interactionGraphics['156:2']; $animation=Resolve-NpcAnimation 0x9c $graphic.DefaultAnimation
$visualRows=@("# role`tid`tsubid`tsprite`ttile-base`tpalette`tanimation",
    "potion`t9c`t02`t$($gfxNames[$graphic.Gfx])`t$($graphic.TileBase)`t$($graphic.Palette)`t$animation")
Write-CutsceneGeneratedTable((Join-Path $destination 'objects\ocean_visuals.tsv'),$visualRows)

# Native background commands use the 32-byte VRAM row stride, not the
# playable 20-column small-room tilemap. Retain its four complete rows.
$jabuRows=[Collections.Generic.List[string]]::new()
$jabuRows.Add("# kind`tkey`tbytes`tsource")
$placement=[regex]::Match($mainObjectSource,'(?ms)^group(?<group>2)Map(?<room>90)ObjectData:\s+obj_Interaction \$(?<id>dc) \$(?<idle>09)\s+obj_Interaction \$dc \$(?<trigger>0a)\s+obj_Interaction \$6b \$11\s+obj_End')
if(!$placement.Success){throw 'Jabu exterior ordered $dc:$09/$0a placements changed.'}
$placedBytes=@('group','room','id','idle','id','trigger'|ForEach-Object{[Convert]::ToInt32($placement.Groups[$_].Value,16)})
$jabuRows.Add("bytes`tplacements`t$($placedBytes -join ',')`tmainData.s:group2Map90ObjectData")
for($i=1;$i -le 2;$i++) {
    $tiles=[IO.File]::ReadAllBytes((Join-Path $Disassembly "gfx_compressible/ages/map_jabu_opening_$i.bin"))
    $flags=[IO.File]::ReadAllBytes((Join-Path $Disassembly "gfx_compressible/ages/flg_jabu_opening_$i.bin"))
    if($tiles.Length -ne 128 -or $flags.Length -ne 128){throw "Jabu opening $i map size changed."}
    $pairs=@(for($row=0;$row -lt 4;$row++){for($col=0;$col -lt 20;$col++){$tiles[$row*32+$col];$flags[$row*32+$col]}})
    $jabuRows.Add("bytes`topening-$i`t$($pairs -join ',')`tgfxHeaders.s:GFXH_JABU_OPENING_$i")
}
$idlePath=Join-Path $Disassembly 'object_code/ages/interactions/miscellaneous2.s'
$idleNodes=@(Read-AssemblyNodes $idlePath)
$start=($idleNodes|Where-Object{$_.Kind -eq 'Label' -and $_.Name -eq 'interactiondc_subid09'}|Select-Object -First 1).Line
$end=($idleNodes|Where-Object{$_.Kind -eq 'Label' -and $_.Name -eq 'interactiondc_subid0A'}|Select-Object -First 1).Line
for($i=0;$i -lt 5;$i++) {
    $labelNode=$idleNodes|Where-Object{$_.Line -gt $start -and $_.Line -lt $end -and $_.Kind -eq 'Label' -and $_.Name -eq "@tiles$i"}|Select-Object -First 1
    if(!$labelNode){throw "Missing Jabu tile list $i."}
    $values=@(Read-AssemblyDataDirectives $idlePath "@tiles$i" '.db' | Where-Object{$_.Line -gt $start -and $_.Line -lt $end}|ForEach-Object{$_.Operands|ForEach-Object{Convert-AssemblyInteger $_}})
    if($values[-1] -ne 0 -or $values.Count -notin @(9,17)){throw "Malformed Jabu tiles$i."}
    $jabuRows.Add("bytes`tidle-$i`t$($values[0..($values.Count-2)] -join ',')`tmiscellaneous2.s:interactiondc_subid09:@tiles$i")
}
$positions=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'object_code/ages/interactions/97.s') '@positions' '.db'|ForEach-Object{$_.Operands|ForEach-Object{Convert-AssemblyInteger $_}})
if($positions.Count -ne 48){throw 'Jabu bubble positions changed.'}
$jabuRows.Add("bytes`tbubble-positions`t$($positions -join ',')`tinteractions/97.s:@positions")
Write-CutsceneGeneratedTable((Join-Path $destination 'objects/jabu_tables.tsv'),$jabuRows)
$partPath=Join-Path $Disassembly 'data/ages/partAnimations.s'
$tables=Read-AssemblyDwTables $partPath 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers=Read-AssemblyDwTables $partPath 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$frames=Read-AssemblyAnimationDefinitions $partPath 'partAnimation[0-9a-f]+(?:Loop)?' $false
$data=@((@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partData.s') 'partData' '.db')[0x16]).Operands|ForEach-Object{Convert-AssemblyInteger $_})
$sprite=$gfxNames[$data[0]]
$visualRows=[Collections.Generic.List[string]]::new()
$visualRows.Add("# subid`tsprite`ttile-base`tpalette`tanimation")
for($i=0;$i -lt 2;$i++) {
    $frame=$frames[$tables['part16Animations'][$i]].Frames[0]
    $oam=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partOamData.s') $pointers['part16OamDataPointers'][$frame.PointerOffset/2] '.db')
    $cells=@($oam|Select-Object -Skip 1|ForEach-Object{($_.Operands|ForEach-Object{Convert-AssemblyInteger $_}) -join ','})
    if($cells.Count -ne (Convert-AssemblyInteger $oam[0].Operands[0])){throw 'PART $16 OAM count changed.'}
    $visualRows.Add("$i`t$sprite`t$($data[5])`t$($data[6] -band 7)`t$($frame.Duration),$($frame.Parameter)@$($cells -join ';')")
}
if(!(Test-Path (Join-Path $destination "gfx/$sprite.png"))) {
    Copy-Item (Join-Path $Disassembly "gfx_compressible/ages/$sprite.png") (Join-Path $destination "gfx/$sprite.png")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects/jabu_bubbles.tsv'),$visualRows)
