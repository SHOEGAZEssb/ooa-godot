# Preserve native boss ENEMY records at their main-stream positions.
$crownMainPath = Join-Path $Disassembly 'objects/ages/mainData.s'
$crownEnemyPath = Join-Path $Disassembly 'objects/ages/enemyData.s'
$crownLabel = 'group4Mapb4ObjectData'
$crownNodes = @(Read-AssemblyMacroInvocations $crownMainPath $crownLabel)
$crownRows = [Collections.Generic.List[string]]::new()
$crownRows.Add("# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`tsource")
$crownOrder = 0
foreach ($node in $crownNodes) {
    if ($node.Name -eq 'obj_Interaction' -and ($node.Operands -join ' ') -eq '$20 $00 $58 $78') {
        $crownRows.Add("4`tb4`t$crownOrder`tminiboss-reward`t20`t00`t58`t78`tflag80-clear`tobjects/ages/mainData.s:$crownLabel")
    }
    if ($node.Name -eq 'obj_BeforeEvent') {
        if (($node.Operands -join ' ') -ne 'group4Mapb4BeforeEventObjectData') { throw "${crownLabel}: unexpected miniboss pointer." }
        $boss = @(Read-AssemblyMacroInvocations $crownEnemyPath $node.Operands[0] 'obj_SpecificEnemyA')
        if ($boss.Count -ne 1 -or ($boss[0].Operands -join ' ') -ne '$00 $74 $00 $58 $78') {
            throw 'group4Mapb4BeforeEventObjectData: expected ENEMY_SMASHER $74:$00 at $58,$78.'
        }
        $crownRows.Add("4`tb4`t$crownOrder`tsmasher`t74`t00`t58`t78`tflag80-clear`tobjects/ages/enemyData.s:$($node.Operands[0])")
    }
    if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer','obj_IfRoomFlag','obj_IfRoomFlagUnset','obj_EndIf')) { $crownOrder++ }
}
if ($crownRows.Count -ne 3 -or $crownRows[1] -notmatch '^4\tb4\t0\t' -or $crownRows[2] -notmatch '^4\tb4\t4\t') {
    throw 'Crown miniboss reward/spawner lost main object positions $00/$04.'
}
$smogLabel = 'group4MapbfObjectData'
$smogOrder = 0
$smogFound = $false
$smogControllerFound = $false
$smogRewardFound = $false
foreach ($node in Read-AssemblyMacroInvocations $crownMainPath $smogLabel) {
    if ($node.Name -eq 'obj_Interaction' -and $node.Operands[0] -eq '$20') {
        if (($node.Operands -join ' ') -ne '$20 $01 $58 $78' -or $smogOrder -ne 1 -or $smogRewardFound) {
            throw 'group4MapbfObjectData: expected one boss reward $20:$01 at order$01, position$58,$78.'
        }
        $crownRows.Add("4`tbf`t$smogOrder`tboss-reward`t20`t01`t58`t78`talways`tobjects/ages/mainData.s:$smogLabel")
        $smogRewardFound = $true
    }
    if ($node.Name -eq 'obj_Interaction' -and $node.Operands[0] -eq '$33') {
        if (($node.Operands -join ' ') -ne '$33 $ff $58 $78' -or $smogOrder -ne 0 -or $smogControllerFound) {
            throw 'group4MapbfObjectData: expected one INTERAC_SMOG_BOSS $33:$ff at order$00, position$58,$78.'
        }
        $crownRows.Add("4`tbf`t$smogOrder`tsmog-controller`t33`tff`t58`t78`talways`tobjects/ages/mainData.s:$smogLabel")
        $smogControllerFound = $true
    }
    if ($node.Name -eq 'obj_BeforeEvent') {
        if (($node.Operands -join ' ') -ne 'group4MapbfBeforeEventObjectData') { throw "${smogLabel}: unexpected boss pointer." }
        $boss = @(Read-AssemblyMacroInvocations $crownEnemyPath $node.Operands[0] 'obj_SpecificEnemyA')
        if ($boss.Count -ne 1 -or ($boss[0].Operands -join ' ') -ne '$00 $7c $05 $58 $78') {
            throw 'group4MapbfBeforeEventObjectData: expected ENEMY_SMOG $7c:$05 at $58,$78.'
        }
        $crownRows.Add("4`tbf`t$smogOrder`tsmog-sentinel`t7c`t05`t58`t78`tflag80-clear`tobjects/ages/enemyData.s:$($node.Operands[0])")
        $smogFound = $true
        if ($smogOrder -ne 4) { throw 'Smog sentinel lost main-stream order$04.' }
    }
    if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer','obj_IfRoomFlag','obj_IfRoomFlagUnset','obj_EndIf')) { $smogOrder++ }
}
if (-not $smogFound) { throw 'Missing Crown boss-room sentinel.' }
if (-not $smogControllerFound) { throw 'Missing Crown INTERAC_SMOG_BOSS $33:$ff controller.' }
if (-not $smogRewardFound) { throw 'Missing Crown boss reward $20:$01.' }
$eyeChest = @(Read-AssemblyMacroInvocations $crownMainPath 'group4MapbaObjectData')
if ($eyeChest[0].Name -ne 'obj_Interaction' -or ($eyeChest[0].Operands -join ' ') -ne '$20 $02 $58 $78') {
    throw 'Crown eye chest lost INTERAC20:02 at source order0/position58,78.'
}
$crownRows.Add("4`tba`t0`ttrigger-chest`t20`t02`t58`t78`talways`tobjects/ages/mainData.s:group4MapbaObjectData")
foreach ($chest in @(@('9b','13','58','48'),@('9e','14','48','78'),@('a5','15','58','38'))) {
    $label = 'group4Map' + $chest[0] + 'ObjectData'
    $nodes = @(Read-AssemblyMacroInvocations $crownMainPath $label)
    $expected = '$21 $' + $chest[1] + ' $' + $chest[2] + ' $' + $chest[3]
    if ($nodes[0].Name -ne 'obj_Interaction' -or ($nodes[0].Operands -join ' ') -ne $expected) {
        throw "Crown pattern chest placement changed at $label."
    }
    $crownRows.Add("4`t$($chest[0])`t0`ttile-pattern-chest`t21`t$($chest[1])`t$($chest[2])`t$($chest[3])`talways`tobjects/ages/mainData.s:$label")
}
$hintPlacement = @(Read-AssemblyMacroInvocations $crownMainPath 'group4Mapa5ObjectData')[1]
if ($hintPlacement.Name -ne 'obj_Interaction' -or ($hintPlacement.Operands -join ' ') -ne '$21 $16 $58 $38') {
    throw 'Crown pattern hint lost source order1 INTERAC21:16.'
}
$crownRows.Add("4`ta5`t1`tpattern-hint`t21`t16`t58`t38`talways`tobjects/ages/mainData.s:group4Mapa5ObjectData")
& {
    $placement = @(Read-AssemblyMacroInvocations $crownMainPath 'group4Map9bObjectData')[3]
    if ($placement.Name -ne 'obj_Interaction' -or ($placement.Operands -join ' ') -ne '$dc $17') {
        throw 'Crown squish detector requires INTERAC $dc:$17 at 4:9b source order$03.'
    }
    $source = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/miscellaneous2.s')
    $body = [regex]::Match($source,'(?ms)^interactiondc_subid17:(?<body>.*?)(?:\z)').Groups['body'].Value
    $body = [regex]::Replace($body,';[^\r\n]*','')
    if ($body -notmatch '(?s)^\s*call checkInteractionState\s+jp z,interactionIncState\s+@state1:\s+ld a,\(w1Link.yh\)\s+ld b,a\s+ld a,\(w1Link.xh\)\s+ld c,a\s+callab bank5.checkPositionSurroundedByWalls\s+rl b\s+ret nc\s+ld a,\(w1Link.state\)\s+cp LINK_STATE_NORMAL\s+ret nz\s+ld hl,wLinkForceState\s+ld a,\(hl\)\s+or a\s+ret nz\s+ld a,LINK_STATE_SQUISHED\s+ldi \(hl\),a\s+ld a,\(wBlockPushAngle\)\s+and \$08\s+xor \$08\s+ld \(hl\),a\s+ret\s*$') {
        throw 'INTERAC $dc:$17 wall/state/forced-state gates or orientation contract changed.'
    }
    $crownRows.Add("4`t9b`t3`twall-squish`tdc`t17`t00`t00`talways`tobjects/ages/mainData.s:group4Map9bObjectData;object_code/ages/interactions/miscellaneous2.s:interactiondc_subid17")
}
& {
    $placement = @(Read-AssemblyMacroInvocations $crownMainPath 'group4MapadObjectData')[1]
    if ($placement.Name -ne 'obj_Interaction' -or ($placement.Operands -join ' ') -ne '$90 $19') { throw 'Crown bridge lost INTERAC $90:$19 at source order$01.' }
    $source = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/miscPuzzles.s')
    $body = [regex]::Match($source,'(?ms)^miscPuzzles_subid19:(?<body>.*?)^miscPuzzles_subid1a:').Groups['body'].Value
    if ($body -notmatch '(?s)@state1:\s+ld a,\(wActiveTriggers\)\s+rrca\s+ret nc\s+ld e,Interaction.counter1\s+ld a,\$(?<interval>[0-9a-f]{2})' ) { throw 'Crown bridge trigger/counter changed.' }
    $interval = [Convert]::ToInt32($Matches['interval'],16)
    if ($body -notmatch '(?s)@state2:.*?ld hl,wRoomLayout\+\$(?<first>[0-9a-f]{2}).*?cp TILEINDEX_BLANK_HOLE.*?ld a,TILEINDEX_HORIZONTAL_BRIDGE.*?call setTileInAllBuffers.*?cp \$(?<end>[0-9a-f]{2})') { throw 'Crown bridge extension scan changed.' }
    $first = $Matches['first']; $last = ([Convert]::ToInt32($Matches['end'],16)-1).ToString('x2')
    if ($body -notmatch '(?s)@@releasedTrigger:\s+call interactionIncState\s+inc \(hl\)\s+ret' -or
        $body -notmatch '(?s)@state3:\s+ld a,\(wActiveTriggers\)\s+rrca\s+ret c\s+jp interactionIncState' -or
        $body -notmatch ('(?s)@state4:.*?rrca\s+jr c,@@pressedTrigger.*?ld hl,wRoomLayout\+\$'+$last+'.*?ldd a,\(hl\).*?cp TILEINDEX_SWITCH_DIAMOND\s+call z,@createDebris.*?ld a,TILEINDEX_BLANK_HOLE.*?call setTileInAllBuffers.*?cp \$'+$first+'\s+jr nc,--') -or
        $body -notmatch '(?s)@@pressedTrigger:\s+ld e,Interaction.state\s+ld a,\$01\s+ld \(de\),a\s+ret' -or
        [regex]::Matches($body,'ld \(hl\),\$08').Count -ne 2 -or $interval -ne 8) { throw 'Crown bridge reversal/retraction contract changed.' }
    $constants = Read-ImportText (Join-Path $Disassembly 'constants/common/tileIndices.s')
    $tiles = foreach ($name in @('BLANK_HOLE','HORIZONTAL_BRIDGE','SWITCH_DIAMOND')) {
        $match = [regex]::Match($constants,'(?m)^\.define\s+TILEINDEX_'+$name+'\s+\$([0-9a-f]{2})')
        if (!$match.Success) { throw "Crown bridge lost TILEINDEX_$name." }; $match.Groups[1].Value
    }
    Write-GeneratedTable((Join-Path $destination 'objects/crown_button_bridge.tsv'), @(
        "# first`tlast`tinterval`thole`tbridge`tdiamond`tsource",
        "$first`t$last`t$interval`t$($tiles[0])`t$($tiles[1])`t$($tiles[2])`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid19"
    ))
    $crownRows.Add("4`tad`t1`tbutton-bridge`t90`t19`t00`t00`talways`tobjects/ages/mainData.s:group4MapadObjectData")
}
& {
    $placement = @(Read-AssemblyMacroInvocations $crownMainPath 'group4Mapb8ObjectData')
    if ($placement.Count -ne 2 -or $placement[0].Name -ne 'obj_Interaction' -or
        ($placement[0].Operands -join ' ') -ne '$7f $00 $28 $78' -or $placement[1].Name -ne 'obj_End') {
        throw 'Crown Essence requires INTERAC$7f:$00 at order0, Y$28/X$78 in room4:b8.'
    }
    $crownRows.Add("4`tb8`t0`tessence`t7f`t00`t28`t78`talways`tobjects/ages/mainData.s:group4Mapb8ObjectData")
    $sourcePath = Join-Path $Disassembly 'object_code/common/interactions/essence.s'
    $warps = @(Read-AssemblyDataDirectives $sourcePath '@essenceWarps' '.db')
    $texts = @(Read-AssemblyDataDirectives $sourcePath '@getEssenceTextTable' '.db')
    if ($warps.Count -ne 8 -or $texts.Count -ne 8) { throw 'Essence exit/text tables must contain eight Ages entries.' }
    $warp = @($warps[4].Operands)
    if ($texts[4].Operands.Count -ne 1 -or $texts[4].Operands[0] -notmatch '^<TX_([0-9a-f]{4})$') {
        throw 'Crown Essence text table entry must name a TX_0000 bank text.'
    }
    $textId = [Convert]::ToInt32($Matches[1],16)
    if (($warp -join ',') -ne '$80,$0a,$17,TRANSITION_DEST_SET_RESPAWN' -or $textId -ne 0x0012 -or
        -not $allTexts.ContainsKey($textId)) { throw 'Crown Essence index4 text/exit mapping changed.' }
    $transitions = Read-ImportText (Join-Path $Disassembly 'constants/common/transitions.s')
    if ($transitions -notmatch '(?m)^\.define TRANSITION_DEST_SET_RESPAWN\s+\$1\b') { throw 'Crown Essence exit transition changed.' }
    $position = if ($allTextPositions.ContainsKey($textId)) { $allTextPositions[$textId] } else { 0 }
    $message = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    Write-GeneratedTable((Join-Path $destination 'objects/crown_dungeon_essence.tsv'), @(
        "# index`ttext-id`ttext-position`tmessage-base64`tdestination-group`tdestination-room`tdestination-position`tdestination-transition`tsource",
        "4`t0012`t$position`t$message`t0`t0a`t17`t01`tobject_code/common/interactions/essence.s:@getEssenceTextTable/@essenceWarps"
    ))
}
Write-GeneratedTable((Join-Path $destination 'objects/crown_dungeon_objects.tsv'), $crownRows)
$chestDispatch = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/dungeonScript.s')
$chestScript = Read-ImportText (Join-Path $Disassembly 'scripts/ages/dungeonScripts.s')
if ($chestDispatch -notmatch '(?ms)^@dungeon5:\s*\.dw mainScripts.dungeonScript_minibossDeath\s*\.dw mainScripts.dungeonScript_bossDeath\s*\.dw mainScripts.crownDungeonScript_spawnChestWhen3TriggersActive' -or
    $chestScript -notmatch '(?ms)^crownDungeonScript_spawnChestWhen3TriggersActive:\s*stopifitemflagset\s*checkmemoryeq wActiveTriggers, \$(?<trigger>[0-9a-f]{2})\s*scriptjump spawnChestAfterPuff') {
    throw 'Crown eye chest lost its exact trigger gate or item-flag check.'
}
$chestTrigger = $Matches['trigger']
if ($chestScript -notmatch '(?ms)^spawnChestAfterPuff:\s*playsound SND_SOLVEPUZZLE\s*createpuff\s*wait (?<wait>\d+)\s*settilehere TILEINDEX_CHEST\s*scriptend') {
    throw 'Shared chest puff/wait/tile sequence changed.'
}
Write-GeneratedTable((Join-Path $destination 'objects/crown_trigger_chest_script.tsv'), @(
    "# subid`ttrigger-value`twait`tsource",
    "02`t$chestTrigger`t$($Matches['wait'])`tscripts/ages/dungeonScripts.s:crownDungeonScript_spawnChestWhen3TriggersActive->spawnChestAfterPuff"
))

$events = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/dungeonEvents.s')
$tileConstants = Read-ImportText (Join-Path $Disassembly 'constants/common/tileIndices.s')
$tiles = @{}
foreach ($match in [regex]::Matches($tileConstants,'(?m)^\.define\s+(TILEINDEX_[A-Z0-9_]+)\s+\$([0-9a-f]{2})')) {
    $tiles[$match.Groups[1].Value] = [Convert]::ToInt32($match.Groups[2].Value,16)
}
$patterns = [Collections.Generic.List[string]]::new()
$patterns.Add("# subid`torder`tposition`tminimum-tile`tmaximum-tile`tsource")
$ring = [regex]::Match($events,'(?ms)^interaction21_subid13:(?<body>.*?)^interaction21_subid14:')
if (-not $ring.Success -or $ring.Groups['body'].Value -notmatch 'sub TILEINDEX_RED_PUSHABLE_BLOCK\s+cp \$03' -or
    $ring.Groups['body'].Value -notmatch '(?m)^@positionsToCheck:[^\r\n]*\r?\n\s*\.db (?<positions>(?:\$[0-9a-f]{2} )+)\$00') {
    throw 'Crown owl-ring range test or ordered positions changed.'
}
$order = 0
foreach ($position in [regex]::Matches($Matches['positions'],'\$(?<value>[0-9a-f]{2})')) {
    $patterns.Add("13`t$order`t$($position.Groups['value'].Value)`t$($tiles['TILEINDEX_RED_PUSHABLE_BLOCK'].ToString('x2'))`t$(($tiles['TILEINDEX_RED_PUSHABLE_BLOCK']+2).ToString('x2'))`tobject_code/ages/interactions/dungeonEvents.s:interaction21_subid13@positionsToCheck")
    $order++
}
foreach ($subid in @('14','15')) {
    $next = ([Convert]::ToInt32($subid,16)+1).ToString('x2')
    $block = [regex]::Match($events,"(?ms)^interaction21_subid${subid}:(?<body>.*?)^interaction21_subid${next}:")
    if (-not $block.Success -or $block.Groups['body'].Value -notmatch 'call verifyTiles\s+ret nz\s+jp spawnChestAndDeleteSelf') {
        throw "Crown pattern $subid lost its verifyTiles dispatch."
    }
    $order = 0
    $terminated = $false
    foreach ($row in [regex]::Matches($block.Groups['body'].Value,'(?m)^\s*\.db (?<tile>TILEINDEX_[A-Z_]+)\s+(?<positions>(?:\$[0-9a-f]{2} )+)\$(?<end>ff|00)')) {
        if ($terminated) { throw "Crown pattern $subid has data after its zero terminator." }
        $terminated = $row.Groups['end'].Value -eq '00'
        $tile = $tiles[$row.Groups['tile'].Value]
        if ($null -eq $tile) { throw 'Unknown Crown pattern metatile symbol.' }
        foreach ($position in [regex]::Matches($row.Groups['positions'].Value,'\$(?<value>[0-9a-f]{2})')) {
            $patterns.Add("$subid`t$order`t$($position.Groups['value'].Value)`t$($tile.ToString('x2'))`t$($tile.ToString('x2'))`tobject_code/ages/interactions/dungeonEvents.s:interaction21_subid${subid}@tileData")
            $order++
        }
    }
    if (-not $terminated) { throw "Crown pattern $subid lacks its verifyTiles zero terminator." }
}
if ($patterns.Count -ne 17) { throw 'Crown pattern chests require sixteen ordered cell conditions.' }
Write-GeneratedTable((Join-Path $destination 'objects/crown_chest_patterns.tsv'),$patterns)

$hint = [regex]::Match($events,'(?ms)^interaction21_subid16:(?<show>.*?)^setTileToStandardFloor:\s*ld a,(?<floor>TILEINDEX_[A-Z_]+)(?<helper>.*?)^interaction21_subid16_state1:(?<hide>.*?)^interaction21_subid17:')
if (-not $hint.Success -or $hint.Groups['show'].Value -notmatch 'ld a,\(wActiveTriggers\)\s+or a\s+ret z' -or
    $hint.Groups['hide'].Value -notmatch 'ld a,\(wActiveTriggers\)\s+or a\s+ret nz' -or
    $hint.Groups['helper'].Value -notmatch 'setTileWithPuff:\s*call setTile') {
    throw 'Crown hint lost nonzero/zero trigger gates or tile-then-puff ordering.'
}
$show = [regex]::Matches($hint.Groups['show'].Value,'ld c,\$(?<position>[0-9a-f]{2})\s+ld a,(?<tile>TILEINDEX_[A-Z_]+)\s+(?:call|jr) setTileWithPuff')
$hide = [regex]::Matches($hint.Groups['hide'].Value,'ld c,\$(?<position>[0-9a-f]{2})\s+call setTileToStandardFloor')
if ($show.Count -ne 6 -or $hide.Count -ne 6) { throw 'Crown hint requires six ordered show/restore writes.' }
$hintRows = [Collections.Generic.List[string]]::new()
$hintRows.Add("# order`tposition`tshow-tile`trestore-tile`tsource")
for ($i = 0; $i -lt 6; $i++) {
    $position = $show[$i].Groups['position'].Value
    if ($position -ne $hide[$i].Groups['position'].Value) { throw 'Crown hint show/restore order differs.' }
    $showTile = $tiles[$show[$i].Groups['tile'].Value]
    $floorTile = $tiles[$hint.Groups['floor'].Value]
    if ($null -eq $showTile -or $null -eq $floorTile) { throw 'Unknown Crown hint metatile symbol.' }
    $hintRows.Add("$i`t$position`t$($showTile.ToString('x2'))`t$($floorTile.ToString('x2'))`tobject_code/ages/interactions/dungeonEvents.s:interaction21_subid16")
}
Write-GeneratedTable((Join-Path $destination 'objects/crown_pattern_hint.tsv'),$hintRows)

# Shared dungeon floor toggles. Keep temporary parser state out of the large
# dot-sourced importer scope (Windows PowerShell has a variable-count limit).
& {
    $maskNodes = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/dungeonsUsingToggleBlocks.s') 'dungeonsUsingToggleBlocks' 'dbrev')
    if ($maskNodes.Count -ne 1) { throw 'Expected one dungeon toggle eligibility bitset.' }
    $mask = ($maskNodes[0].Operands -join '').Replace('%', '')
    if ($mask -notmatch '^[01]{16}$') { throw 'Invalid dungeon toggle eligibility bitset.' }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# dungeon`tstate`toriginal`treplacement`tdestination-tile`ttile-count`tsource-tile`tsource")
    foreach ($state in 0,1) {
        $label = if ($state -eq 0) { '@state2' } else { '@state1' }
        $values = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'code/ages/tileSubstitutions.s') $label)
        if ($values.Count -ne 5 -or $values[4] -ne 0) { throw "replaceToggleBlocks $label lost two pairs and terminator." }
        $header = if ($state -eq 0) { 'uncmpGfxHeader3d' } else { 'uncmpGfxHeader3f' }
        $gfx = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/uncmpGfxHeaders.s') $header 'm_GfxHeader')
        if ($gfx.Count -ne 1 -or $gfx[0].Operands[0] -ne 'gfx_animations_2') { throw "Unexpected toggle graphics at $header." }
        $address = Convert-AssemblyInteger $gfx[0].Operands[1]
        $count = Convert-AssemblyInteger $gfx[0].Operands[2]
        $offset = Convert-AssemblyInteger $gfx[0].Operands[3]
        if ($address -ne 0x8cc1 -or $count -ne 4 -or ($offset % 16) -ne 0) { throw "Invalid toggle upload at $header." }
        for ($dungeon = 0; $dungeon -lt $mask.Length; $dungeon++) {
            if ($mask[$dungeon] -ne '1') { continue }
            for ($pair = 0; $pair -lt 4; $pair += 2) {
                $rows.Add("$dungeon`t$state`t$($values[$pair+1].ToString('x2'))`t$($values[$pair].ToString('x2'))`t76`t$count`t$($offset / 16)`tcode/ages/tileSubstitutions.s:replaceToggleBlocks$label;data/ages/dungeonsUsingToggleBlocks.s;data/ages/uncmpGfxHeaders.s:$header")
            }
        }
    }
    if ($rows.Count -ne 13) { throw 'Expected twelve dungeon toggle substitution rows.' }
    Write-GeneratedTable((Join-Path $destination 'metadata/dungeon_toggle_tiles.tsv'), $rows)
    $cutscenePath = Join-Path $Disassembly 'code/ages/cutscenes2.s'
    $cutscene = Read-ImportText $cutscenePath
    $delay = [regex]::Match($cutscene, 'ld a,\$(?<delay>[0-9a-f]{2})\s+ld \(wTmpcbb4\),a')
    if (-not $delay.Success -or $cutscene -notmatch 'ld a,SND_DOORCLOSE\s+call playSound' -or
        $cutscene -notmatch 'ld a,UNCMP_GFXH_AGES_3e\s+call loadUncompressedGfxHeader') { throw 'Toggle cutscene lost delay, sound or intermediate graphics.' }
    $phase = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/uncmpGfxHeaders.s') 'uncmpGfxHeader3e' 'm_GfxHeader')
    if ($phase.Count -ne 1 -or ($phase[0].Operands -join ' ') -ne 'gfx_animations_2 $8cc1 $04 $780') { throw 'Unexpected intermediate toggle graphics.' }
    $live = [Collections.Generic.List[string]]::new()
    $live.Add("# name`tvalues`tsource")
    $live.Add("delay`t$([Convert]::ToInt32($delay.Groups['delay'].Value,16))`tcode/ages/cutscenes2.s:@state0")
    $live.Add("intermediate-graphics`t76,4,120`tdata/ages/uncmpGfxHeaders.s:uncmpGfxHeader3e")
    $final = @(Read-AssemblyLiteralValues $cutscenePath '@data_7d63')
    if ($final.Count -ne 8) { throw 'Toggle cutscene requires four tile/collision pairs.' }
    $tileSource = Read-ImportText (Join-Path $Disassembly 'constants/common/tileIndices.s')
    $names = @('RAISED_FLOOR_1','LOWERED_FLOOR_1','RAISED_FLOOR_2','LOWERED_FLOOR_2')
    for ($i = 0; $i -lt 4; $i++) {
        $definition = [regex]::Match($tileSource, "(?m)^\s*\.define TILEINDEX_$($names[$i])\s+\`$(?<value>[0-9a-f]{2})")
        if (-not $definition.Success) { throw "Missing toggle tile $($names[$i])." }
        $live.Add("tile-$($definition.Groups['value'].Value)`t$($final[$i*2]),$($final[$i*2+1])`tcode/ages/cutscenes2.s:@data_7d63")
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/dungeon_toggle_cutscene.tsv'), $live)
}
