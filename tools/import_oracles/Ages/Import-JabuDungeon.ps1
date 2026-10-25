# Dungeon 7's native puzzle placements. Shared doors, buttons, treasures and
# ordinary enemies continue to come from their existing ordered import stages.
& {
$mainPath = Join-Path $Disassembly 'objects/ages/mainData.s'
$puzzlePath = Join-Path $Disassembly 'object_code/ages/interactions/miscPuzzles.s'
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`tsource")
foreach ($spec in @(
    @{Room='6d'; Subid='0a'; Kind='jabu-water'; Y='00'; X='00'; Order=3},
    @{Room='4a'; Subid='0b'; Kind='jabu-ladder'; Y='08'; X='d8'; Order=0},
    @{Room='55'; Subid='0c'; Kind='jabu-hook-key'; Y='58'; X='88'; Order=0},
    @{Room='62'; Subid='00'; Kind='boss-reward'; Y='58'; X='78'; Order=2},
    @{Room='61'; Subid='00'; Kind='essence'; Y='28'; X='68'; Order=0}
)) {
    $label = 'group5Map' + $spec.Room + 'ObjectData'
    $stream = @(Read-AssemblyMacroInvocations $mainPath $label)
    $id = if ($spec.Kind -eq 'essence') { '7f' } elseif ($spec.Kind -eq 'boss-reward') { '20' } else { '90' }
    $expected = @(('$'+$id), ('$'+$spec.Subid))
    if ($spec.Y -ne '00' -or $spec.X -ne '00') { $expected += @(('$'+$spec.Y), ('$'+$spec.X)) }
    $node = $stream[$spec.Order]
    if ($node.Name -ne 'obj_Interaction' -or ($node.Operands -join ' ') -ne ($expected -join ' ')) {
        throw "${mainPath}:${label}: native $($spec.Kind) lost its source placement/order."
    }
    $rows.Add("5`t$($spec.Room)`t$($spec.Order)`t$($spec.Kind)`t$id`t$($spec.Subid)`t$($spec.Y)`t$($spec.X)`talways`tobjects/ages/mainData.s:$label")
}
$anglerStream = @(Read-AssemblyMacroInvocations $mainPath 'group5Map4aObjectData')
$anglerEnemies = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/enemyData.s') 'group5Map4aBeforeEventObjectData' 'obj_SpecificEnemyA')
if ($anglerStream[1].Name -ne 'obj_BeforeEvent' -or $anglerStream[1].Operands[0] -ne 'group5Map4aBeforeEventObjectData' -or
    $anglerEnemies.Count -ne 1 -or ($anglerEnemies[0].Operands -join ',') -ne '$00,$76,$00,$38,$68') {
    throw 'Angler Fish requires counted ENEMY$76:$00 at Y$38/X$68, before-event room$5:$4a/order$01.'
}
$rows.Add("5`t4a`t1`tangler-fish`t76`t00`t38`t68`tflag80-clear`tobjects/ages/enemyData.s:group5Map4aBeforeEventObjectData")
$plasStream = @(Read-AssemblyMacroInvocations $mainPath 'group5Map62ObjectData')
$plasEnemies = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/enemyData.s') 'group5Map62BeforeEventObjectData' 'obj_SpecificEnemyA')
if ($plasStream[3].Name -ne 'obj_BeforeEvent' -or $plasStream[3].Operands[0] -ne 'group5Map62BeforeEventObjectData' -or
    $plasEnemies.Count -ne 1 -or ($plasEnemies[0].Operands -join ',') -ne '$00,$7e,$00,$00,$50') {
    throw 'Plasmarine requires counted ENEMY$7e:$00 at Y$00/X$50, before-event room$5:$62/order$03.'
}
$rows.Add("5`t62`t3`tplasmarine`t7e`t00`t00`t50`tflag80-clear`tobjects/ages/enemyData.s:group5Map62BeforeEventObjectData")
Write-GeneratedTable((Join-Path $destination 'objects/jabu_dungeon_objects.tsv'), $rows)
$source = (Read-ImportText $puzzlePath) -replace '(?m);[^\r\n]*',''
if ($source -notmatch '(?ms)^miscPuzzles_subid0a:.*?ld a,\(wJabuWaterLevel\)\s+and \$f0\s+ld \(wSwitchState\),a.*?bit 7,c\s+jr nz,@drainWater.*?and \$03\s+inc a\s+or b\s+ld \(hl\),a.*?TX_1209.*?@drainWater:.*?and \$07\s+ret z\s+xor a\s+ld \(wJabuWaterLevel\),a\s+ld \(wSwitchState\),a.*?TX_1208.*?DISABLE_ALL_BUT_INTERACTIONS \| DISABLE_LINK.*?ld a,60.*?SNDCTRL_STOPMUSIC.*?@state2:.*?ld a,\$f0\s+ld \(hl\),a\s+call setScreenShakeCounter.*?SND_FLOODGATES.*?@state3:.*?ld \(hl\),\$01.*?showText.*?SNDCTRL_STOPSFX.*?wActiveMusic' -or
    $source -notmatch '(?ms)^miscPuzzles_subid0b:.*?wNumEnemies.*?set ROOMFLAG_BIT_80,\(hl\)\s+ld l,<ROOM_AGES_54d\s+set ROOMFLAG_BIT_80,\(hl\).*?ld a,\$08.*?@state2:.*?ld \(hl\),\$08.*?TILEINDEX_SS_LADDER.*?add \$10.*?cp \$90.*?ld c,\$80.*?TILEINDEX_SS_52.*?ld c,\$90.*?TILEINDEX_SS_EMPTY.*?SND_SOLVEPUZZLE.*?wDisableLinkCollisionsAndMenu' -or
    $source -notmatch '(?ms)^miscPuzzles_subid0c:\s+call interactionDeleteAndRetIfEnabled02\s+call miscPuzzles_deleteSelfAndRetIfItemFlagSet.*?ld hl,miscPuzzles_subid0c_wantedTiles.*?miscPuzzles_dropSmallKeyHere:\s+ldbc TREASURE_SMALL_KEY, \$01\s+call createTreasure\s+ret nz\s+call objectCopyPosition\s+jp interactionDelete') {
    throw 'miscPuzzles.s: Dungeon 7 water, ladder or checked key allocation contract changed.'
}
$patternRows = @(Read-AssemblyDataDirectives $puzzlePath 'miscPuzzles_subid0c_wantedTiles' '.db')
if ($patternRows.Count -ne 3 -or ($patternRows[0].Operands -join ',') -ne 'TILEINDEX_SWITCH_DIAMOND') {
    throw 'miscPuzzles_subid0c_wantedTiles requires one diamond tile, four positions and a terminator.'
}
$tileIndexSource = Read-ImportText (Join-Path $Disassembly 'constants/common/tileIndices.s')
if ($tileIndexSource -notmatch '(?m)^\.define TILEINDEX_SWITCH_DIAMOND\s+\$(?<tile>[0-9a-f]{2})\s*$') { throw 'Missing TILEINDEX_SWITCH_DIAMOND.' }
$pattern = @([Convert]::ToInt32($Matches.tile,16)) + @(
    foreach ($node in $patternRows[1..2]) { foreach ($value in $node.Operands) { Convert-AssemblyInteger $value } }
)
if (($pattern -join ',') -ne '219,54,58,118,122,0') { throw 'D7 Switch Hook key requires four TILEINDEX_SWITCH_DIAMOND cells and terminator.' }
Write-GeneratedTable((Join-Path $destination 'objects/jabu_hook_key_tiles.tsv'), @(
    "# position`ttile`tsource"
    foreach ($position in $pattern[1..4]) { "$($position.ToString('x2'))`t$($pattern[0].ToString('x2'))`tmiscPuzzles.s:miscPuzzles_subid0c_wantedTiles" }
))
$texts = [Collections.Generic.List[string]]::new()
$texts.Add("# text-id`ttext-position`tmessage-base64`tsource")
foreach ($textId in @(0x1208,0x1209,0x0014)) {
    if (!$allTexts.ContainsKey($textId)) { throw "Missing D7 text $($textId.ToString('x4'))." }
    $text = $allTexts[$textId]
    $position = if ($allTextPositions.ContainsKey($textId)) { $allTextPositions[$textId] } else { 0 }
    $texts.Add("$($textId.ToString('x4'))`t$position`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($text)))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'objects/jabu_dungeon_text.tsv'), $texts)
$essencePath = Join-Path $Disassembly 'object_code/common/interactions/essence.s'
$warps = @(Read-AssemblyDataDirectives $essencePath '@essenceWarps' '.db')
$essenceTexts = @(Read-AssemblyDataDirectives $essencePath '@getEssenceTextTable' '.db')
if (($warps[6].Operands -join ',') -ne '$82,$90,$45,TRANSITION_DEST_X_SHIFTED' -or
    ($essenceTexts[6].Operands -join ',') -ne '<TX_0014' -or
    (Read-ImportText $essencePath) -notmatch '(?ms)^@essenceOamData:.*?\.db \$0e \$01 \$01') { throw 'D7 Rolling Sea Essence OAM/text/exit mapping changed.' }
Write-GeneratedTable((Join-Path $destination 'objects/jabu_dungeon_essence.tsv'), @(
    "# index`tdestination-group`tdestination-room`tdestination-position`tdestination-transition`tsource",
    "6`t2`t90`t45`t0e`tobject_code/common/interactions/essence.s:@essenceWarps"
))

# Shared species tables live with the enemy assets. The dungeon placement
# stage supplies their closure without introducing dungeon-specific handlers.
foreach ($species in @('anglerFish','plasmarine')) {
    $relative = "object_code/ages/enemies/$species.s"
    $path = Join-Path $Disassembly $relative
    $enemySource = Read-ImportText $path
    $enemyRows = [Collections.Generic.List[string]]::new()
    $enemyRows.Add("# profile`tindex`tvalue`tsource")
    if ($species -eq 'anglerFish') {
        foreach ($contract in @(
            '(?ms)^anglerFish_main_state8:.*?DISABLE_LINK.*?ld a,\$42.*?ld c,\$80.*?ld a,\$52.*?ld c,\$90.*?ld \(hl\),30',
            '(?ms)^anglerFish_main_state9:.*?ANGLE_LEFT.*?SPEED_c0',
            '(?ms)^anglerFish_main_stateA:.*?ld b,\$0c.*?ld a,\$10.*?cp \$02.*?ld \(hl\),180',
            '(?ms)^anglerFish_bounceOffGround:.*?\(-\$320\)',
            '(?ms)^anglerFish_main_stateD:.*?ld a,\$20.*?ld \(hl\),150',
            '(?ms)^anglerFish_main_stateE:.*?\(-\$400\).*?^anglerFish_main_stateF:.*?Enemy.counter1.*?ld \(hl\),180',
            '(?ms)^anglerFish_main_checkFireProjectile:.*?cp \$5c.*?sub \$38.*?cp \$70'
        )) {
            if ($enemySource -notmatch $contract) { throw "${relative}: native Angler state operand contract changed: $contract" }
        }
        $stateValues = @(30,0x18,0x1e,12,0x10,2,-0x320,180,0x20,150,-0x400,0x5c,0x38,0x70)
    } else {
        foreach ($contract in @(
            '(?ms)^plasmarine_state_uninitialized:.*?SPEED_280.*?ld \(hl\),\$08.*?ld \(hl\),\$04.*?ld \(hl\),\$58.*?ld \(hl\),\$78',
            '(?ms)^plasmarine_state9:.*?ld \(hl\),60',
            '(?ms)^plasmarine_stateA:.*?ld \(hl\),\$0c.*?SPEED_300',
            '(?ms)^plasmarine_stateB:.*?sub SPEED_20',
            '(?ms)^plasmarine_stateC:.*?ld \(hl\),65.*?ld \(hl\),-8',
            '(?ms)^plasmarine_stateF:.*?ld \(hl\),30'
        )) {
            if ($enemySource -notmatch $contract) { throw "${relative}: native Plasmarine state operand contract changed: $contract" }
        }
        $stateValues = @(0x64,8,4,0x58,0x78,60,12,0x78,5,65,30)
    }
    for ($index=0; $index -lt $stateValues.Count; $index++) {
        $enemyRows.Add("state`t$index`t$($stateValues[$index])`t${relative}:state-operands")
    }
    $tableName = if ($species -eq 'anglerFish') { 'antenna-offsets' } else { 'projectile-counts' }
    $label = if ($species -eq 'anglerFish') { '@positionOffsets' } else { '@numProjectilesToFire' }
    $values = @(Read-AssemblyDataDirectives $path $label '.db' | ForEach-Object {
        foreach ($operand in $_.Operands) { Convert-AssemblyInteger $operand }
    })
    $expected = if ($species -eq 'anglerFish') { '240,246,240,10,240,246,240,10,252,247,252,9' } else { '3,3,2,2,2,1,1' }
    if (($values -join ',') -ne $expected) { throw "${relative}:${label}: native species table changed." }
    for ($index=0; $index -lt $values.Count; $index++) {
        $enemyRows.Add("$tableName`t$index`t$($values[$index])`t${relative}:${label}")
    }
    $collisionValues = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
    $modes = if ($species -eq 'anglerFish') { @(0x64,0x47,0x24) } else { @(0x4f,0x68) }
    foreach ($mode in $modes) {
        for ($index=0; $index -lt 32; $index++) {
            $enemyRows.Add("effects-$($mode.ToString('x2'))`t$index`t$($collisionValues[$mode*32+$index])`tdata/ages/objectCollisionTable.s:mode`$$($mode.ToString('x2'))")
        }
    }
    $active = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/enemyActiveCollisions.s') 'enemyActiveCollisions' 'dbrev')
    $ids = if ($species -eq 'anglerFish') { @(0x76,0x26) } else { @(0x7e) }
    foreach ($id in $ids) {
        $bits = ($active[$id].Operands -join '').Replace('%','')
        if ($bits -notmatch '^[01]{32}$') { throw "${relative}: enemy`$$($id.ToString('x2')) requires its full collision mask." }
        for ($index=0; $index -lt 32; $index++) {
            $enemyRows.Add("active-$($id.ToString('x2'))`t$index`t$($bits[$index])`tdata/ages/enemyActiveCollisions.s:enemy`$$($id.ToString('x2'))")
        }
    }
    $filename = if ($species -eq 'anglerFish') { 'angler_fish.tsv' } else { 'plasmarine.tsv' }
    if ($species -eq 'anglerFish') {
        # The antenna replaces the dynamically loaded fish graphics with a
        # fixed VRAM-bank-1 sprite. Tile $1e belongs to the common glow sheet.
        $antennaVisual = [regex]::Match($enemySource,
            '(?ms)^anglerFish_antenna:.*?@state8:.*?Enemy\.oamTileIndexBase\s+ld \(hl\),\$(?<tile>[0-9a-f]{2}).*?' +
            'Enemy\.oamFlagsBackup\s+ld a,\$(?<flags>[0-9a-f]{2}).*?ld a,\$(?<animation>[0-9a-f]{2})\s+jp enemySetAnimation')
        $fixedGraphics = Read-ImportText (Join-Path $Disassembly 'data/ages/gfxHeaders.s')
        if (!$antennaVisual.Success -or $antennaVisual.Groups['tile'].Value -ne '1e' -or
            $antennaVisual.Groups['flags'].Value -ne '0d' -or $antennaVisual.Groups['animation'].Value -ne '06' -or
            $fixedGraphics -notmatch '(?ms)^m_GfxHeaderStart \$83, GFXH_COMMON_SPRITES\s+m_GfxHeader (?<sprite>spr_common_sprites), \$8001\s+m_GfxHeaderEnd') {
            throw 'anglerFish.s: antenna must select GFXH_COMMON_SPRITES bank 1, tile $1e, flags $0d, animation $06.'
        }
        $antennaSprite = $Matches.sprite
        $antennaTile = [Convert]::ToInt32($antennaVisual.Groups['tile'].Value,16)
        $antennaPalette = [Convert]::ToInt32($antennaVisual.Groups['flags'].Value,16) -band 7
        $antennaAnimation = [Convert]::ToInt32($antennaVisual.Groups['animation'].Value,16)
        Write-GeneratedTable((Join-Path $destination 'enemies/angler_fish_antenna.tsv'), @(
            "# sprite`ttile-base`tpalette`tanimation`tsource",
            "$antennaSprite`t$antennaTile`t$antennaPalette`t$antennaAnimation`t${relative}:anglerFish_antenna/@state8;gfxHeaders.s:GFXH_COMMON_SPRITES"
        ))
    }
    if ($species -eq 'plasmarine') {
        $projectileSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/parts/plasmarineProjectile.s')
        if ($projectileSource -notmatch '(?ms)^@state0:.*?cp \$03.*?SPEED_80.*?SPEED_e0.*?ld \(hl\),150.*?ld \(hl\),\$08' -or
            $projectileSource -notmatch '(?ms)^@state1:.*?wFrameCounter.*?rrca.*?cp 30.*?ld \(hl\),\$10.*?@collidedWithPlasmarine:.*?ld \(hl\),24.*?dec \(hl\)') {
            throw 'plasmarineProjectile.s: lifetime, speed, homing or returned-hit contract changed.'
        }
        $partState = @(3,0x14,0x23,150,8,30,16,24)
        for ($index=0; $index -lt $partState.Count; $index++) {
            $enemyRows.Add("part-state`t$index`t$($partState[$index])`tobject_code/ages/parts/plasmarineProjectile.s")
        }
        $offsets = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/parts/commonCode.s') 'partCommon_anglePositionOffsets')
        if ($offsets.Count -ne 16) { throw 'partCommon_checkOutOfBounds requires eight ordered Y/X probe pairs.' }
        for ($index=0; $index -lt $offsets.Count; $index++) {
            $enemyRows.Add("part-boundary-offsets`t$index`t$($offsets[$index])`tobject_code/common/parts/commonCode.s:partCommon_anglePositionOffsets")
        }
    }
    Write-GeneratedTable((Join-Path $destination "enemies/$filename"),$enemyRows)
}
}
