# Shared Goron scripts, including global-label fallthrough, anonymous forward
# branches, and the scriptHelp copies used by the guards and gallery keepers.
$goronCommands = [Collections.Generic.List[object]]::new()
function Resolve-GoronText([int]$id,[Collections.Generic.HashSet[int]]$visited) {
    if(!$allTexts.ContainsKey($id) -or !$visited.Add($id)){throw "Unresolved or recursive Goron TX_$($id.ToString('x4'))."}
    $message=[string]$allTexts[$id]
    while($true) {
        $call=[regex]::Match($message,'\\call\(TX_([0-9a-f]{4})\)')
        if(!$call.Success){break}
        $called=Resolve-GoronText ([Convert]::ToInt32($call.Groups[1].Value,16)) $visited
        $message=$message.Substring(0,$call.Index)+$called+$message.Substring($call.Index+$call.Length)
    }
    $jump=[regex]::Match($message,'\\jump\(TX_([0-9a-f]{4})\)')
    if($jump.Success) {
        $message=$message.Substring(0,$jump.Index)+(Resolve-GoronText ([Convert]::ToInt32($jump.Groups[1].Value,16)) $visited)
    } elseif($allTextFallthroughIds.ContainsKey($id)) {
        if($message.EndsWith('\n',[StringComparison]::Ordinal)){$message=$message.Substring(0,$message.Length-2)+"`n"}
        $message+=Resolve-GoronText $allTextFallthroughIds[$id] $visited
    }
    [void]$visited.Remove($id)
    return $message
}
$goronLabels = [Collections.Generic.List[object]]::new()
foreach ($range in @(
    @('scripts/ages/scripts.s', 'shootingGallery_fadeIntoGameWithSword', 'shootingGalleryScript_hit1Blue'),
    @('scripts/ages/scripts.s', 'shootingGalleryScript_goronElderNpc_gameDone', 'scriptFunc_doEnergySwirlCutscene'),
    @('scripts/ages/scriptHelper.s', 'shootingGalleryScript_goronNpc_gameDone', 'impa_moveLinkUp32Frames'),
    @('scripts/ages/scripts.s', 'goron_subid00Script', 'goron_subid09Script_A'),
    @('scripts/ages/scripts.s', 'goron_subid09Script_A', 'goron_subid0cScript'),
    @('scripts/ages/scripts.s', 'goron_subid0cScript', 'rosa_subid00Script'),
    @('scripts/ages/scriptHelper.s', 'goron_subid08_pressedAScript', 'rafton_subid01Script'),
    @('scripts/ages/scriptHelper.s', 'goronElderScript_subid00_body', 'cloakedTwinrova_subid00Script_body'))) {
    $path = Join-Path $Disassembly $range[0]
    $nodes = @(Read-AssemblyNodes $path)
    $start = @($nodes | Where-Object { $_.Kind -eq 'Label' -and $_.Name -eq $range[1] })[0].Offset
    $end = @($nodes | Where-Object { $_.Kind -eq 'Label' -and $_.Name -eq $range[2] })[0].Offset
    $scope = ''; $label = ''; $copied = $range[0] -eq 'scripts/ages/scriptHelper.s'
    $activeBranch=$true; $branches=[Collections.Generic.Stack[bool]]::new()
    foreach ($node in $nodes | Where-Object { $_.Offset -ge $start -and $_.Offset -lt $end }) {
        if($node.Name -in @('.ifdef','.ifndef')) {
            if($node.OperandText -ne 'REGION_JP'){throw "Unknown Goron script conditional $($node.OperandText)."}
            $branches.Push($activeBranch); $activeBranch=$activeBranch -and $node.Name -eq '.ifndef'; continue
        }
        if($node.Name -eq '.else') { $activeBranch=$branches.Peek() -and !$activeBranch; continue }
        if($node.Name -eq '.endif') { $activeBranch=$branches.Pop(); continue }
        if(!$activeBranch){continue}
        if ($node.Kind -eq 'Label') {
            $label = $node.Name
            if ($label -notmatch '^[@+\-]') { $scope = $label }
            $goronLabels.Add([pscustomobject]@{ Name=$label; Scope=$scope; Index=$goronCommands.Count; Line=$node.Line; Copied=$copied })
        } elseif ($node.Name -eq '.dw' -and $goronCommands[$goronCommands.Count-1].Opcode -in @('jumptable_objectbyte','jumptable_memoryaddress')) {
            $goronCommands[$goronCommands.Count-1].Args += '|'+$node.OperandText
        } elseif ($node.Kind -in @('MacroInvocation','Instruction')) {
            $goronCommands.Add([pscustomobject]@{ Opcode=$node.Name.ToLowerInvariant(); Args=$node.OperandText; Scope=$scope; Label=$label; Index=$goronCommands.Count; Line=$node.Line; Copied=$copied })
        } elseif ($node.Kind -notin @('Blank','Comment') -and $node.Name -notin @('.ifndef','.endif')) {
            throw "${path}:$($node.Line): unsupported Goron script node $($node.Kind)."
        }
    }
}
function Resolve-GoronTarget($command, [string]$target) {
    $target = $target -replace '^mainScripts\.', ''
    $qualified=$target -split '@',2
    if ($target -eq 'stubScript') { return $goronCommands.Count }
    $targets = @($goronLabels | Where-Object {
        ($_.Name -eq $target -and ($target -notmatch '^@' -or $_.Scope -eq $command.Scope)) -or
        ($qualified.Count -eq 2 -and $qualified[0] -ne '' -and $_.Scope -eq $qualified[0] -and $_.Name -eq '@'+$qualified[1])
    })
    if ($target.StartsWith('+')) { $targets = @($targets | Where-Object { $_.Line -gt $command.Line -and $_.Copied -eq $command.Copied } | Select-Object -First 1) }
    if ($targets.Count -ne 1) { throw "Goron $($command.Scope):$($command.Line): unresolved target $target." }
    return $targets[0].Index
}
$goronRows = [Collections.Generic.List[string]]::new()
$goronRows.Add("# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$goronTreasureSource = Read-ImportText (Join-Path $Disassembly 'constants/common/treasure.s')
$goronMusicSource = Read-ImportText (Join-Path $Disassembly 'constants/common/music.s')
function Resolve-GoronByte([string]$value) {
    if ($value.StartsWith('$')) { return $value.Substring(1) }
    if ($value -eq 'ROOMFLAG_ITEM') { return '20' }
    $m=[regex]::Match($goronTreasureSource,'(?m)^\s*'+$value+'\s+db\s*;\s*\$([0-9a-f]{2})')
    if (!$m.Success) { throw "Unknown Goron byte $value." }
    return $m.Groups[1].Value
}
foreach ($c in $goronCommands) {
    $op=$c.Opcode; $p=@($c.Args -split ',\s*'); $a=''; $b=''; $actor=''; $payload=''
    switch ($op) {
        'asm15' { $op='native'; $payload=$c.Args -replace '^scriptHelp\.', '' }
        'scriptjump' { $a=(Resolve-GoronTarget $c $p[0]).ToString(); if ($c.Copied -and $p[0] -match '^[@+]') { $op='scriptjumpyield' } }
        'jumpifobjectbyteeq' { $op='jumpifmemoryeq'; $payload=$p[0]; $a=$p[1].Substring(1); $b=(Resolve-GoronTarget $c $p[2]).ToString() }
        'jumpifmemoryeq' { $payload=$p[0]; $a=$p[1].Substring(1); $b=(Resolve-GoronTarget $c $p[2]).ToString() }
        'jumpifmemoryset' { $op='jumpifmemoryeqyieldonmiss'; $payload=$p[0]+':'+$p[1]; $a='01'; $b=(Resolve-GoronTarget $c $p[2]).ToString() }
        'jumpifglobalflagset' { $op='jumpifmemoryeq'; $payload=$p[0]; $a='01'; $b=(Resolve-GoronTarget $c $p[1]).ToString() }
        'jumpifroomflagset' { $a=Resolve-GoronByte $p[0]; $b=(Resolve-GoronTarget $c $p[1]).ToString() }
        'jumpifitemobtained' { $op='jumpifmemoryeq'; $payload='Treasure:'+(Resolve-GoronByte $p[0]); $a='01'; $b=(Resolve-GoronTarget $c $p[1]).ToString() }
        { $_ -in @('jumptable_objectbyte','jumptable_memoryaddress') } {
            $q=$c.Args.Split('|'); $op='jumptablememoryyield'
            $targets=@($q | Select-Object -Skip 1 | ForEach-Object { Resolve-GoronTarget $c $_ })
            $payload=$q[0]+'|'+($targets -join ',')
        }
        'loadscript' { $op='scriptjumpyield'; $a=(Resolve-GoronTarget $c ($p[0] -replace '^scriptHelp\.','')).ToString() }
        'callscript' { $a=(Resolve-GoronTarget $c $p[0]).ToString() }
        'retscript' { $op='return' }
        'scriptend' { }
        'resetmusic' { $op='nativeyield'; $payload='ResetMusic' }
        'enableallobjects' { $op='nativeyield'; $payload='EnableAllObjects' }
        'askforsecret' { if($c.Args -ne 'ELDER_SECRET'){throw "Unexpected Goron secret $($c.Args)."}; $op='nativeyield'; $payload='AskElderSecret' }
        'generatesecret' { if($c.Args -ne 'ELDER_RETURN_SECRET'){throw "Unexpected Goron return secret $($c.Args)."}; $op='nativeyield'; $payload='GenerateElderSecret' }
        'jumpiftextoptioneq' { $a=$p[0].Substring(1); $b=(Resolve-GoronTarget $c $p[1]).ToString() }
        'showtext' {
            $a=$p[0].Substring(3); $id=[Convert]::ToInt32($a,16)
            if (!$allTexts.ContainsKey($id)) { throw "Missing Goron TX_$a." }
            $payload=Resolve-GoronText $id ([Collections.Generic.HashSet[int]]::new()); if ($allTextPositions.ContainsKey($id)) { $b=$allTextPositions[$id].ToString() }
        }
        'wait' { $a=$p[0] }
        'initcollisions' { $actor='Goron' }
        'makeabuttonsensitive' { $actor='Goron' }
        'setcollisionradii' { $actor='Goron'; $a=$p[0].Substring(1); $b=$p[1].Substring(1) }
        'checkabutton' { $actor='Goron' }
        'setanimation' { $actor='Goron'; $a=$p[0].Substring(1); $payload=Resolve-NpcAnimation 0x66 ([Convert]::ToInt32($a,16)) }
        'setcoords' { $actor='Goron'; $a=$p[0].Substring(1); $b=$p[1].Substring(1) }
        'writeobjectbyte' { $op='nativeyield'; $payload='Write:'+ $c.Args }
        'writememory' { $payload=$p[0]; $a=switch($p[1]) { 'LINK_ANIM_MODE_COLLAPSED' {'02'} 'LINK_ANIM_MODE_DANCELEFT' {'08'} default {$p[1].Substring(1)} } }
        'getrandombits' { $op='nativeyield'; $payload='Random:'+ $c.Args }
        'setangle' { $op='nativeyield'; $payload='Angle:'+$p[0].Substring(1) }
        'setspeed' { $op='nativeyield'; $speedByte=switch($p[0]) { 'SPEED_080' {'14'} 'SPEED_100' {'28'} 'SPEED_200' {'50'} default { throw "Unknown Goron speed $($p[0])." } }; $payload='Speed:'+$speedByte }
        'applyspeed' { $op='nativeblock'; $actor='Goron'; $a=([Convert]::ToInt32($p[0].Substring(1),16)).ToString(); $payload='ApplySpeed' }
        { $_ -in @('moveleft','moveright') } { $payload=if($op -eq 'moveleft'){'MoveLeft'}else{'MoveRight'}; $op='nativeblock'; $actor='Goron'; $a=([Convert]::ToInt32($p[0].Substring(1),16)).ToString() }
        'spawninteraction' { $op='nativeyield'; $payload='Spawn:'+$c.Args }
        'checkpalettefadedone' { $op='gate'; $payload='PaletteDone' }
        'checkobjectbyteeq' { $op='checkmemoryeq'; $payload=$p[0]; $a=$p[1].Substring(1) }
        'checkmemoryeq' { $payload=$p[0]; if ($p[1] -ne 'SPECIALOBJECT_LINK') { throw "Unknown Goron memory check $($c.Args)." }; $a='00' }
        { $_ -in @('playsound','setmusic') } {
            $m=[regex]::Match($goronMusicSource,'(?m)^\s*'+$p[0]+'\s+db\s*;\s*\$([0-9a-f]{2})')
            if (!$m.Success) { throw "Unknown Goron sound $($c.Args)." }; $a=$m.Groups[1].Value
        }
        'giveitem' {
            if ($p.Count -eq 1) { $reward=$treasureObjectRecords[$p[0]]; $a=$reward.Treasure.ToString('x2'); $b=$reward.SubId.ToString('x2') }
            else { $a=Resolve-GoronByte $p[0]; $b=Resolve-GoronByte $p[1] }
        }
        'setglobalflag' { $a=switch($p[0]) { 'GLOBALFLAG_SAVED_GORON_ELDER' {'2f'} 'GLOBALFLAG_BEGAN_ELDER_SECRET' {'6c'} 'GLOBALFLAG_DONE_ELDER_SECRET' {'76'} default {throw "Unknown Goron flag $($c.Args)."} } }
        'orroomflag' { $a=$p[0].Substring(1) }
        { $_ -in @('disableinput','enableinput','showloadedtext') } { }
        default { throw "Goron $($c.Scope):$($c.Line): unsupported opcode $op." }
    }
    $goronRows.Add((New-CutsceneCommandRow $c.Scope $c.Index $c.Label $c.Line $op $actor $a $b $payload))
}
$goronRows.Add((New-CutsceneCommandRow 'stubScript' $goronCommands.Count 'stubScript' 1 'nativeyield' '' '' '' 'Delete'))
$goronRows.Add((New-CutsceneCommandRow 'unsupported-subid' ($goronCommands.Count+1) 'unsupported-subid' 1 'nativeyield' '' '' '' 'UnsupportedSubid'))
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/goron_cave_commands.tsv'), $goronRows)
$rows=[Collections.Generic.List[string]]::new(); $rows.Add("# kind`tid`tvalue`tposition")
foreach ($label in @('goron_subid00Script','goron_subid01Script','goron_subid03Script','goronDanceScript_failedRound','goronDanceScript_givePrize','goron_subid04Script','goron_subid05Script_A','goron_subid05Script_B','goron_subid06Script_A','goron_subid06Script_B','goron_subid07Script','goron_subid08Script','goron_subid0aScript','goron_subid0cScript','goron_subid0dScript','goron_subid0eScript','goron_subid10Script','goronElderScript_subid00_body','goronElderScript_subid01_body')) {
    $entry=@($goronLabels | Where-Object Name -eq $label)[0]
    $rows.Add("entry`t$label`t$($entry.Index)`t0")
}
foreach ($label in @('shootingGalleryScript_goronNpc','shootingGalleryScript_goronElderNpc','shootingGalleryScript_goronNpc_gameDone','shootingGalleryScript_goronElderNpc_gameDone','shootingGalleryScript_goronNpc@tryAgain','shootingGalleryScript_goronElderNpc@beginGame')) {
    $index=Resolve-GoronTarget ([pscustomobject]@{Scope='';Line=0;Copied=$false}) $label
    $rows.Add("entry`t$label`t$index`t0")
}
foreach ($label in @('goron_subid09Script_A','goron_subid09Script_B','goron_subid0bScript')) {
    $entry=@($goronLabels | Where-Object Name -eq $label)[0]
    $rows.Add("entry`t$label`t$($entry.Index)`t0")
}
foreach ($id in @(0x2400..0x24e5)+@(0x3100..0x3127)+@(0x3130..0x3159)) {
    if (!$allTexts.ContainsKey($id)) { throw "Missing Goron text $id." }
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((Resolve-GoronText $id ([Collections.Generic.HashSet[int]]::new()))))
    $position=0; if ($allTextPositions.ContainsKey($id)) { $position=$allTextPositions[$id] }
    $rows.Add("text`t$($id.ToString('x4'))`t$encoded`t$position")
}
foreach ($id in @(0x66,0x8b,0x4e)) {
    foreach ($animation in @(0,1,2,3,4,6,8)) {
        if ($id -eq 0x4e -and $animation -eq 8 -or $id -eq 0x8b -and $animation -eq 6) { continue }
        if ($id -eq 0x8b -and $animation -eq 8) { continue }
        $rows.Add("animation`t$($id.ToString('x2')):$animation`t$(Resolve-NpcAnimation $id $animation)`t0")
    }
    $gfx=$interactionGraphics["${id}:0"]
    $rows.Add("sprite`t$($id.ToString('x2'))`t$($gfxNames[$gfx.Gfx])`t$($gfx.TileBase)")
    $rows.Add("bytes`tpalette-$($id.ToString('x2'))`t$($gfx.Palette.ToString('x2'))`t0")
}
$dancerSource=Read-ImportText (Join-Path $Disassembly 'objects/ages/extraData3.s')
if($dancerSource -notmatch '(?ms)^targetCartCrystals:\s+obj_Pointer @crystals\s+obj_End\s+@crystals:\s+obj_SpecificEnemyA \$00 \$63 \$00 \$00 \$00\s+obj_SpecificEnemyA\s+\$63 \$01 \$00 \$00\s+obj_SpecificEnemyA\s+\$63 \$02 \$00 \$00\s+obj_SpecificEnemyA\s+\$63 \$03 \$00 \$00\s+obj_SpecificEnemyA\s+\$63 \$04 \$00 \$00\s+obj_EndPointer') {
    throw 'targetCartCrystals must retain its five ordered ENEMY $63:$00-$04 native spawns.'
}
foreach($kind in @('goron','subrosian')) {
    $block=[regex]::Match($dancerSource,'(?ms)^'+$kind+'Dancers:\s*(?<body>(?:\s*obj_Interaction[^\r\n]+\r?\n)+)')
    $values=@([regex]::Matches($block.Groups['body'].Value,'\$([0-9a-f]{2})')|ForEach-Object{$_.Groups[1].Value})
    if($values.Count -ne 35){throw "Expected seven $kind dancer objects."}
    $rows.Add("bytes`tdancers-$kind`t$($values -join ',')`t0")
}
$helper = Read-ImportText (Join-Path $Disassembly 'scripts/ages/scriptHelper.s')
$crystalSource=Read-ImportText (Join-Path $Disassembly 'object_code/ages/enemies/targetCartCrystal.s')
foreach($table in @(@('behaviourTable',48),@('configuration0',24),@('configuration1',24),@('configuration2',24))) {
    $block=[regex]::Match($crystalSource,'(?ms)^@'+$table[0]+':\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
    $values=@([regex]::Matches(($block.Groups['body'].Value -replace ';[^\r\n]*',''),'\$([0-9a-f]{2})')|ForEach-Object{$_.Groups[1].Value})
    if($values.Count -ne $table[1]){throw "Target-cart $($table[0]) changed."}
    $rows.Add("bytes`tcrystal-$($table[0])`t$($values -join ',')`t0")
}
$bombSource=Read-ImportText (Join-Path $Disassembly 'object_code/ages/parts/bigBangBombSpawner.s')
foreach($table in @(@('780f',73),@('789d',32),@('790b',16),@('791b',32),@('7962',64),@('79a2',9))) {
    $block=[regex]::Match($bombSource,'(?ms)^table_'+$table[0]+':\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
    $values=@([regex]::Matches($block.Groups['body'].Value,'\$([0-9a-f]{2})')|ForEach-Object{$_.Groups[1].Value})
    if($values.Count -ne $table[1]){throw "Big Bang table $($table[0]) changed."}
    $rows.Add("bytes`tbomb-$($table[0])`t$($values -join ',')`t0")
}
foreach($name in @('minigameLayout1_topHalf','minigameLayout1_bottomHalf','minigameLayout2_topHalf','minigameLayout2_bottomHalf','normalRoomLayout')) {
    $block=[regex]::Match($helper,'(?ms)^goron_bigBang_'+$name+':\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
    $values=@([regex]::Matches($block.Groups['body'].Value,'\$([0-9a-f]{2})')|ForEach-Object{$_.Groups[1].Value})
    if($values.Count -ne 24){throw "Big Bang layout $name changed."}
    $rows.Add("bytes`tbigbang-$name`t$($values -join ',')`t0")
}
$partPath=Join-Path $Disassembly 'data/ages/partAnimations.s'
$partOam=Read-ImportText (Join-Path $Disassembly 'data/ages/partOamData.s')
$partTables=Read-AssemblyDwTables $partPath 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$partPointers=Read-AssemblyDwTables $partPath 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$partFrames=Read-AssemblyAnimationDefinitions $partPath 'partAnimation[0-9a-f]+(?:Loop)?' $false
for($i=0;$i -lt 2;$i++) {
    $definition=$partFrames[$partTables['part49Animations'][$i]]
    $frames=@(foreach($frame in $definition.Frames){
        $oamRows=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partOamData.s') $partPointers['part49OamDataPointers'][$frame.PointerOffset/2] '.db')
        $count=Convert-AssemblyInteger $oamRows[0].Operands[0]
        $oam=@($oamRows|Select-Object -Skip 1|ForEach-Object{($_.Operands|ForEach-Object{Convert-AssemblyInteger $_}) -join ','})
        if($oam.Count -ne $count){throw 'PART $49 OAM count changed.'}
        "$($frame.Duration),$($frame.Parameter)@$($oam -join ';')"
    })
    $rows.Add("animation`t49:$i`t$($frames -join '|')~$($definition.LoopStart)`t0")
}
$partBytes=@((@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partData.s') 'partData' '.db')[0x49]).Operands|ForEach-Object{Convert-AssemblyInteger $_})
if(($partBytes -join ',') -ne '120,4,51,0,64,16,4,0'){throw 'PART $49 attributes changed.'}
$rows.Add("sprite`t49`t$($gfxNames[0x78])`t16")
$rockSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/fallingRock.s')
$native = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/goron.s')
$galleryNative=Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/shootingGallery.s')
foreach($variant in @('goron','biggoron')) {
    foreach($kind in @('Positions','Tiles')) {
        $block=[regex]::Match($galleryNative,'(?ms)^shootingGallery_target'+$kind+'_'+$variant+':\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
        $values=@([regex]::Matches($block.Groups['body'].Value,'\$([0-9a-f]{2})')|ForEach-Object{$_.Groups[1].Value})
        $count=if($kind -eq 'Positions'){10}else{100}
        if($values.Count -ne $count){throw "Invalid $variant gallery $kind table."}
        $rows.Add("bytes`tgallery-$variant-$kind`t$($values -join ',')`t0")
    }
}
foreach ($level in @('platinum','gold','silver','bronze')) {
    $block=[regex]::Match($native,'(?ms)^@'+$level+':\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
    $values=@([regex]::Matches($block.Groups['body'].Value,'\$([0-9a-f]{2})')|ForEach-Object{$_.Groups[1].Value})
    if($values.Count -ne 160){throw "Goron dance $level must contain ten 16-byte patterns."}
    $rows.Add("bytes`tdance-$level`t$($values -join ',')`t0")
}
foreach ($rewardName in @('TREASURE_OBJECT_BROTHER_EMBLEM_00','TREASURE_OBJECT_MERMAID_KEY_00','TREASURE_OBJECT_GASHA_SEED_00','TREASURE_OBJECT_LAVA_JUICE_00','TREASURE_OBJECT_BOOMERANG_02','TREASURE_OBJECT_BOMBS_05','TREASURE_OBJECT_BIGGORON_SWORD_00')) {
    $reward=$treasureObjectRecords[$rewardName]
    $rows.Add("reward`t$($reward.Treasure.ToString('x2')):$($reward.SubId.ToString('x2'))`t$rewardName`t0")
}
foreach($rewardName in @('TREASURE_OBJECT_OLD_MERMAID_KEY_00','TREASURE_OBJECT_ROCK_BRISKET_00','TREASURE_OBJECT_RING_14','TREASURE_OBJECT_RING_15')) {
    $reward=$treasureObjectRecords[$rewardName]
    $rows.Add("reward`t$($reward.Treasure.ToString('x2')):$($reward.SubId.ToString('x2'))`t$rewardName`t0")
}
$hintBlock=[regex]::Match($helper,'(?ms)^goron_showTextForClairvoyantGoron:.*?^@treasures:\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
$treasureSource=Read-ImportText (Join-Path $Disassembly 'constants/common/treasure.s')
$hintValues=@([regex]::Matches($hintBlock.Groups['body'].Value,'TREASURE_\w+') | ForEach-Object {
    $match=[regex]::Match($treasureSource,'(?m)^\s*'+$_.Value+'\s+db\s*;\s*\$([0-9a-f]{2})')
    if (!$match.Success) { throw "Missing Goron hint treasure $($_.Value)." }
    $match.Groups[1].Value
})
if ($hintValues.Count -ne 7) { throw 'Clairvoyant Goron requires seven treasure entries.' }
# The eighth comparison always terminates at index eight; its carry is unused.
$rows.Add("bytes`thint-treasures`t$($hintValues -join ',')`t0")
foreach ($subid in @('0c','0d','0e')) {
    $block=[regex]::Match($helper,'(?ms)^@subid'+$subid+':\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
    $values=@([regex]::Matches(($block.Groups['body'].Value -replace ';[^\r\n]*',''),'\$([0-9a-f]{2})') | ForEach-Object { $_.Groups[1].Value })
    if ($values.Count -ne @{'0c'=32;'0d'=20;'0e'=44}[$subid]) { throw "Invalid Goron generic text table $subid." }
    $rows.Add("bytes`tgeneric-$subid`t$($values -join ',')`t0")
}
$speeds = Read-ImportText (Join-Path $Disassembly 'constants/common/objectSpeeds.s')
$flags = Read-ImportText (Join-Path $Disassembly 'constants/common/globalFlags.s')
foreach ($goronNativeContract in @(
    @($native,'(?s)goronSubid03:\s*goronSubid04:.*?call goron_loadScriptAndInitGraphics\s+call interactionRunScript\s+@state1:\s+call interactionRunScript'),
    @($native,'(?s)goronSubid06:.*?ld \(hl\),\$0a.*?wTmpcfc0.goronCutscenes.elderVar_cfdd.*?goronCutscenes.dataEnd.*?call clearMemory.*?call interactionRunScript'),
    @($helper,'(?s)goron_beginWalkingLeft:.*?SPEED_80.*?ld \(hl\),\$18.*?ld \(hl\),\$40.*?ld \(hl\),\$00.*?ld \(hl\),\$01.*?ld a,\$03'),
    @($helper,'(?s)goron_reverseWalkingDirection:.*?ld \(hl\),\$80.*?xor \$10.*?xor \$02'),
    @($helper,'(?s)goron_checkShouldBeNapping:\s+ld bc,\$1818\s+call objectSetCollideRadii\s+call objectCheckCollidedWithLink_ignoreZ\s+ccf\s+call writeFlagsTocddb\s+ld bc,\$0606'),
    @($helper,'(?s)goron_checkLinkApproachedWithBombFlower:.*?TREASURE_BOMB_FLOWER.*?ld \(hl\),\$88.*?ld \(hl\),\$58.*?ld bc,\$1808.*?objectCheckCollidedWithLink_ignoreZ.*?ld bc,\$0606'),
    @($helper,'(?s)goron_initCountersForBombFlowerExplosion:.*?ld \(hl\),90.*?ld \(hl\),\$01'),
    @($helper,'(?s)goron_countdownToPlayRockSoundAndShakeScreen:.*?ld \(hl\),\$05.*?SND_BREAK_ROCK.*?ld a,\$04'),
    @($helper,'(?s)goron_createRockDebrisToLeft:\s+ld bc,\$f6fa.*?goron_createRockDebrisToRight:\s+ld bc,\$f606.*?call getRandomNumber\s+and \$01'),
    @($helper,'(?s)createExclamationMark:\s+ld bc,\$f300\s+jp objectCreateExclamationMark'),
    @($speeds,'SPEED_80\s+dsb 5 ; 0x14'),
    @($speeds,'SPEED_100\s+dsb 5 ; 0x28'),
    @($speeds,'SPEED_180\s+dsb 5 ; 0x3c'),
    @($flags,'GLOBALFLAG_SAVED_GORON_ELDER\s+db ; \$2f'))) {
    if ($goronNativeContract[0] -notmatch $goronNativeContract[1]) {
        throw "Goron cave native contract changed: $($goronNativeContract[1])"
    }
}
foreach ($table in @(
    @('angles',$rockSource,'fallingRock_subid02','angles',24),
    @('falling-positions',$rockSource,'fallingRock_chooseRandomPosition','positionList1',32),
    @('explosion-positions',$helper,'goron_createExplosionIndex','positions',16),
    @('explosion-groups',$helper,'goron_countdownToNextExplosionGroup','explosionIndices',32),
    @('explosion-counters',$helper,'goron_countdownToNextExplosionGroup','counters',8),
    @('barrier',$helper,'goron_clearRockBarrier','clearedTiles',15))) {
    $pattern='(?ms)^'+$table[2]+':.*?^@'+$table[3]+':\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)'
    $block=[regex]::Match($table[1],$pattern)
    $values=@([regex]::Matches($block.Groups['body'].Value,'\$([0-9a-f]{2})') | ForEach-Object { $_.Groups[1].Value })
    if ($values.Count -ne $table[4]) { throw "Goron source table $($table[2])@$($table[3]) expected $($table[4]) bytes." }
    $rows.Add("bytes`t$($table[0])`t$($values -join ',')`t0")
}
$textBlock=[regex]::Match($helper,'(?ms)^goron_showTextForSubid05:.*?^@text:\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
$textIds=@([regex]::Matches($textBlock.Groups['body'].Value,'TX_([0-9a-f]{4})') | ForEach-Object { $_.Groups[1].Value })
if ($textIds.Count -ne 9) { throw 'goron_showTextForSubid05 requires nine source text indices.' }
$rows.Add("bytes`tdialogue-table`t$($textIds -join ',')`t0")
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/goron_cave_data.tsv'), $rows)
$effects = [Collections.Generic.List[string]]::new()
$effects.Add("# id`tsubid`tsprite`ttile-base`tpalette`tanimation")
foreach ($pair in @(@(0x56,0),@(0x9f,0),@(0x92,1),@(0x92,2),@(0x92,3))) {
    $id=$pair[0]; $subid=$pair[1]; $gfx=$interactionGraphics["${id}:$subid"]
    $sprite=$gfxNames[$gfx.Gfx]
    if ($gfx.Gfx -eq 0) { $sprite='spr_common_sprites' }
    $effects.Add((@($id.ToString('x2'),$subid.ToString('x2'),$sprite,$gfx.TileBase.ToString('x2'),$gfx.Palette.ToString('x2'),(Resolve-NpcAnimation $id $gfx.DefaultAnimation)) -join "`t"))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/goron_cave_effects.tsv'), $effects)
