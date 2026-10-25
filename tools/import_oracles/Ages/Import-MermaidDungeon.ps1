# Native miniboss/reward records retain their main-stream allocation order.
& {
$mainPath = Join-Path $Disassembly 'objects/ages/mainData.s'
$enemyPath = Join-Path $Disassembly 'objects/ages/enemyData.s'
$essencePlacement = @(Read-AssemblyMacroInvocations $mainPath 'group5Map37ObjectData')
if ($essencePlacement.Count -ne 3 -or $essencePlacement[0].Name -ne 'obj_Interaction' -or
    ($essencePlacement[0].Operands -join ' ') -ne '$7f $00 $28 $78' -or
    $essencePlacement[1].Name -ne 'obj_Pointer' -or $essencePlacement[1].Operands[0] -ne 'group5Map37EnemyObjectData' -or
    $essencePlacement[2].Name -ne 'obj_End') { throw 'Mermaid Essence requires source room$5:$37/order0 INTERAC$7f:$00 at Y$28/X$78.' }
$essencePath = Join-Path $Disassembly 'object_code/common/interactions/essence.s'
$essenceWarps = @(Read-AssemblyDataDirectives $essencePath '@essenceWarps' '.db')
$essenceTexts = @(Read-AssemblyDataDirectives $essencePath '@getEssenceTextTable' '.db')
$essenceSource = Read-ImportText $essencePath
if ($essenceWarps.Count -ne 8 -or $essenceTexts.Count -ne 8 -or
    ($essenceWarps[5].Operands -join ',') -ne '$83,$0f,$16,TRANSITION_DEST_SET_RESPAWN' -or
    ($essenceTexts[5].Operands -join ',') -ne '<TX_0013' -or
    $essenceSource -notmatch '(?ms)ld a,\(wDungeonIndex\)\s*dec a.*?cp \$0b\s*jr nz,\+\s*ld a,\$05' -or
    $essenceSource -notmatch '(?ms)^@essenceOamData:.*?\.db \$0a \$00 \$02\s*\.db \$0c \$00 \$02' -or
    -not $allTexts.ContainsKey(0x0013)) { throw 'Mermaid Essence index5, past-index override, OAM/text/exit source mapping changed.' }
$essencePosition = if ($allTextPositions.ContainsKey(0x0013)) { $allTextPositions[0x0013] } else { 0 }
$essenceMessage = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x0013]))
Write-GeneratedTable((Join-Path $destination 'objects/mermaid_dungeon_essence.tsv'), @(
    "# index`ttext-id`ttext-position`tmessage-base64`tdestination-group`tdestination-room`tdestination-position`tdestination-transition`tsource",
    "5`t0013`t$essencePosition`t$essenceMessage`t3`t0f`t16`t01`tobject_code/common/interactions/essence.s:@getEssenceTextTable/@essenceWarps"
))
# The shared reward branches once, then waits for the counted death effect.
# Retain command boundaries; spawnitem yields, then the ROM jump continues.
$rewardSource = (Read-ImportText (Join-Path $Disassembly 'scripts/ages/dungeonScripts.s')) -replace '(?m);[^\r\n]*',''
if ($rewardSource -notmatch '(?ms)^dungeonScript_bossDeath:\s*jumpifroomflagset \$80, \+\+\s*checknoenemies\s*orroomflag \$80\s*\+\+\s*stopifitemflagset\s*setcoords \$58, \$78\s*spawnHeartContainer:\s*spawnitem TREASURE_HEART_CONTAINER, \$00\s*scriptjump enableLinkAndMenu' -or
    $rewardSource -notmatch '(?ms)^enableLinkAndMenu:\s*writememory wDisableLinkCollisionsAndMenu, \$00\s*scriptend') {
    throw 'dungeonScript_bossDeath lost its one-time flag branch, counted death wait, heart position or Link unlock sequence.'
}
$label = 'group5Map12ObjectData'
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`tsource")
$order = 0
foreach ($node in Read-AssemblyMacroInvocations $mainPath $label) {
    if ($node.Name -eq 'obj_Interaction' -and ($node.Operands -join ' ') -eq '$20 $00 $58 $78') {
        $rows.Add("5`t12`t$order`tminiboss-reward`t20`t00`t58`t78`tflag80-clear`tobjects/ages/mainData.s:$label")
    }
    if ($node.Name -eq 'obj_BeforeEvent') {
        if (($node.Operands -join ' ') -ne 'group5Map12BeforeEventObjectData') { throw "${label}: unexpected Vire pointer." }
        $boss = @(Read-AssemblyMacroInvocations $enemyPath $node.Operands[0] 'obj_SpecificEnemyA')
        if ($boss.Count -ne 1 -or ($boss[0].Operands -join ' ') -ne '$00 $75 $00 $58 $78') { throw 'Vire requires one counted ENEMY$75:$00 at Y$58/X$78.' }
        $rows.Add("5`t12`t$order`tvire`t75`t00`t58`t78`tflag80-clear`tobjects/ages/enemyData.s:$($node.Operands[0])")
    }
    if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer','obj_IfRoomFlag','obj_IfRoomFlagUnset','obj_EndIf')) { $order++ }
}
if ($rows.Count -ne 3 -or $rows[1] -notmatch '^5\t12\t0\t' -or $rows[2] -notmatch '^5\t12\t4\t') { throw 'Mermaid miniboss/reward lost source order$00/$04.' }
# Octogon's persistent state is initialized by a positionless interaction in
# the preceding room, independently of the boss-room completion flags.
$label = 'group5Map38ObjectData'
$order = 0
foreach ($node in Read-AssemblyMacroInvocations $mainPath $label) {
    if ($node.Name -eq 'obj_Interaction' -and $node.Operands[0] -eq '$90') {
        if (($node.Operands -join ' ') -ne '$90 $0f') { throw "${label}: unexpected Octogon initializer operands." }
        $rows.Add("5`t38`t$order`toctogon-initializer`t90`t0f`t00`t00`talways`tobjects/ages/mainData.s:$label")
    }
    if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer','obj_IfRoomFlag','obj_IfRoomFlagUnset','obj_EndIf')) { $order++ }
}
if ($rows.Count -ne 4 -or $rows[3] -notmatch '^5\t38\t1\t') { throw 'Octogon initializer requires source room$5:$38/order$01.' }
foreach ($room in @('36','2d')) {
    $label = "group5Map${room}ObjectData"
    $order = 0
    foreach ($node in Read-AssemblyMacroInvocations $mainPath $label) {
        if ($node.Name -eq 'obj_Interaction' -and ($node.Operands -join ' ') -eq '$20 $00 $58 $78') {
            $rows.Add("5`t$room`t$order`tboss-reward`t20`t00`t58`t78`talways`tobjects/ages/mainData.s:$label")
        }
        if ($node.Name -eq 'obj_BeforeEvent') {
            $expectedSubid = if ($room -eq '36') { '00' } else { '01' }
            $boss = @(Read-AssemblyMacroInvocations $enemyPath $node.Operands[0] 'obj_RandomEnemy')
            if ($boss.Count -ne 1 -or ($boss[0].Operands -join ' ') -ne "`$20 `$7d `$$expectedSubid") { throw "${label}: Octogon requires one counted random ENEMY`$7d:`$$expectedSubid with flags`$20." }
            $rows.Add("5`t$room`t$order`toctogon`t7d`t$expectedSubid`t00`t00`tflag80-clear`tobjects/ages/enemyData.s:$($node.Operands[0])")
        }
        if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer','obj_IfRoomFlag','obj_IfRoomFlagUnset','obj_EndIf')) { $order++ }
    }
}
if ($rows.Count -ne 7) { throw 'Mermaid native records require Vire/reward, initializer, two Octogon bodies and boss reward.' }
foreach ($room in @('21','2c')) {
    $label = "group5Map${room}ObjectData"
    $order = 0
    foreach ($node in Read-AssemblyMacroInvocations $mainPath $label) {
        if ($node.Name -eq 'obj_Interaction') {
            $kind = switch ("$($node.Operands[0]):$($node.Operands[1])") {
                '$19:$03' { 'colored-cube' }
                '$90:$01' { 'tile-pattern-chest' }
                '$90:$02' { 'cube-floor-sensor' }
                '$90:$03' { 'tile-pattern-chest' }
                default { throw "${mainPath}:${label}: unsupported Mermaid pattern placement $($node.Operands -join ' ')." }
            }
            if ($node.Operands.Count -ne 4) { throw "${mainPath}:${label}: pattern placements require explicit Y/X." }
            $values = @($node.Operands | ForEach-Object { (Convert-AssemblyInteger $_).ToString('x2') })
            $rows.Add("5`t$room`t$order`t$kind`t$($values[0])`t$($values[1])`t$($values[2])`t$($values[3])`talways`tobjects/ages/mainData.s:$label")
        }
        if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer','obj_IfRoomFlag','obj_IfRoomFlagUnset','obj_EndIf')) { $order++ }
    }
}
if ($rows.Count -ne 13) { throw 'Mermaid pattern rooms require one cube, three floor sensors and two chest controllers.' }
$floorPlacement = @(Read-AssemblyMacroInvocations $mainPath 'group5Map1aObjectData' 'obj_Interaction')
$floorStream = @(Read-AssemblyMacroInvocations $mainPath 'group5Map1aObjectData')
if ($floorPlacement.Count -ne 1 -or ($floorPlacement[0].Operands -join ' ') -ne '$90 $04' -or
    $floorStream.Count -eq 0 -or $floorStream[0].Name -ne 'obj_Interaction' -or ($floorStream[0].Operands -join ' ') -ne '$90 $04') {
    throw "${mainPath}:group5Map1aObjectData: requires positionless INTERAC`$90:`$04 as its first row."
}
$rows.Add("5`t1a`t0`tmermaid-changing-floor`t90`t04`t00`t00`talways`tobjects/ages/mainData.s:group5Map1aObjectData")
$keyPlacements = @(Read-AssemblyMacroInvocations $mainPath 'group5Map1cObjectData' 'obj_Interaction')
if ($keyPlacements.Count -ne 5 -or
    ($keyPlacements[0].Operands -join ' ') -ne '$90 $00 $18 $78' -or
    ($keyPlacements[1].Operands -join ' ') -ne '$61 $06 $40 $58' -or
    ($keyPlacements[2].Operands -join ' ') -ne '$61 $46 $40 $98' -or
    ($keyPlacements[3].Operands -join ' ') -ne '$1e $06 $a7 $00' -or
    ($keyPlacements[4].Operands -join ' ') -ne '$dc $11') {
    throw "${mainPath}:group5Map1cObjectData: boss-key controller/levers/door/key-mirror lost their source order."
}
for ($order = 0; $order -lt 3; $order++) {
    $kind = if ($order -eq 0) { 'mermaid-boss-key' } else { 'lever' }
    $values = @($keyPlacements[$order].Operands | ForEach-Object { (Convert-AssemblyInteger $_).ToString('x2') })
    $rows.Add("5`t1c`t$order`t$kind`t$($values[0])`t$($values[1])`t$($values[2])`t$($values[3])`talways`tobjects/ages/mainData.s:group5Map1cObjectData")
}
$rows.Add("5`t1c`t4`tboss-key-mirror`tdc`t11`t00`t00`talways`tobjects/ages/mainData.s:group5Map1cObjectData")
$keyMirrorPath = Join-Path $Disassembly 'object_code/ages/interactions/miscellaneous2.s'
$keyMirrorSource = (Read-ImportText $keyMirrorPath) -replace '(?m);[^\r\n]*',''
if ($keyMirrorSource -notmatch '(?ms)^interactiondc_subid11:\s*call getThisRoomFlags\s*and ROOMFLAG_ITEM\s*ret z\s*ld hl,wDungeonBossKeys\s*ld a,\$(?<dungeon>[0-9a-f]{2})\s*jp setFlag\s*(?=\w+:)') {
    throw "${keyMirrorPath}: key mirror lost its item gate, dungeon bit or state-zero lifetime."
}
Write-GeneratedTable((Join-Path $destination 'objects/dungeon_boss_key_mirrors.tsv'),@(
    "# id`tsubid`tdungeon`tsource",
    "dc`t11`t$($Matches.dungeon)`tobject_code/ages/interactions/miscellaneous2.s:interactiondc_subid11"
))
$spinnerPath = Join-Path $Disassembly 'object_code/common/interactions/spinner.s'
$spinnerSource = (Read-ImportText $spinnerPath) -replace '(?m);[^\r\n]*',''
if ($spinnerSource -notmatch '(?ms)^@subid00:\s*@subid01:.*?@state0:.*?ld l,Interaction.xh\s*ld a,\(hl\)\s*ld l,Interaction.var3a\s*ld \(hl\),a.*?ld a,\(wSpinnerState\)\s*and \(hl\).*?ld l,Interaction.subid\s*ld \(hl\),a.*?ld l,Interaction.yh\s*ld a,\(hl\)\s*call setShortPosition') {
    throw "${spinnerPath}: direct spinner lost its packed position/shared-mask initialization."
}
foreach ($room in @('18','39')) {
    $label = "group5Map${room}ObjectData"
    $spinners = @(Read-AssemblyMacroInvocations $mainPath $label 'obj_Interaction')
    $expected = @(if ($room -eq '18') { '$7d $00 $44 $01'; '$7d $01 $78 $02' } else { '$7d $00 $68 $01' })
    if ($spinners.Count -ne $expected.Count) { throw "${mainPath}:${label}: unexpected spinner count." }
    for ($order = 0; $order -lt $spinners.Count; $order++) {
        if (($spinners[$order].Operands -join ' ') -ne $expected[$order]) { throw "${mainPath}:${label}: unexpected spinner order/parameters." }
        $values = @($spinners[$order].Operands | ForEach-Object { (Convert-AssemblyInteger $_).ToString('x2') })
        $rows.Add("5`t$room`t$order`tspinner`t$($values -join "`t")`talways`tobjects/ages/mainData.s:$label")
    }
}
$mirrorPlacements = @(@('3a','08',0),@('44','09',2))
foreach ($placement in $mirrorPlacements) {
    $room = $placement[0]; $subid = $placement[1]; $order = $placement[2]
    $stream = @(Read-AssemblyMacroInvocations $mainPath "group5Map${room}ObjectData" 'obj_Interaction')
    if ($stream.Count -ne $order+1 -or ($stream[$order].Operands -join ' ') -ne "`$90 `$$subid") {
        throw "${mainPath}:group5Map${room}ObjectData: missing positionless room-flag mirror`$90:`$$subid at source order$order."
    }
    $rows.Add("5`t$room`t$order`troom-flag-mirror`t90`t$subid`t00`t00`talways`tobjects/ages/mainData.s:group5Map${room}ObjectData")
}
$torchPlacement = @(Read-AssemblyMacroInvocations $mainPath 'group5Map43ObjectData')
if ($torchPlacement[0].Name -ne 'obj_Interaction' -or ($torchPlacement[0].Operands -join ' ') -ne '$90 $07') {
    throw "${mainPath}:group5Map43ObjectData: requires the positionless torch controller first."
}
$rows.Add("5`t43`t0`tmermaid-torch-order`t90`t07`t00`t00`talways`tobjects/ages/mainData.s:group5Map43ObjectData")
# INTERAC$20 dispatches by the live dungeon index, then subid. These two
# scripts share PART_BRIDGE_SPAWNER/button owners; retain their command gates.
foreach ($placement in @(@('38',0,'01'),@('3d',1,'02'))) {
    $room=$placement[0]; $order=$placement[1]; $subid=$placement[2]
    $stream=@(Read-AssemblyMacroInvocations $mainPath "group5Map${room}ObjectData")
    if ($stream[$order].Name -ne 'obj_Interaction' -or ($stream[$order].Operands -join ' ') -ne "`$20 `$$subid") {
        throw "${mainPath}:group5Map${room}ObjectData: missing dungeon signal script at order$order."
    }
    $rows.Add("5`t$room`t$order`tsignal-script`t20`t$subid`t00`t00`talways`tobjects/ages/mainData.s:group5Map${room}ObjectData")
}
$dispatchPath=Join-Path $Disassembly 'object_code/ages/interactions/dungeonScript.s'
$dispatch=(Read-ImportText $dispatchPath) -replace '(?m);[^\r\n]*',''
if ($dispatch -notmatch '(?ms)^interactionCode20:\s*call interactionDeleteAndRetIfEnabled02.*?@state0:\s*ld a,\$01\s*ld \(de\),a\s*xor a\s*ld \(\$cfc1\),a\s*ld \(\$cfc2\),a.*?call interactionSetScript\s*jp interactionRunScript' -or
    $dispatch -notmatch '(?ms)^@dungeonc:\s*\.dw mainScripts.dungeonScript_bossDeath\s*\.dw mainScripts.mermaidsCaveScript_spawnBridgeWhenOrbHit\s*\.dw mainScripts.mermaidsCaveScript_updateTrigger2BasedOnTriggers0And1') {
    throw "${dispatchPath}: unsupported dungeon`$0c signal-script dispatch/initialization."
}
$helperPath=Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$helper=(Read-ImportText $helperPath) -replace '(?m);[^\r\n]*',''
if ($rewardSource -notmatch '(?ms)^mermaidsCaveScript_spawnBridgeWhenOrbHit:\s*stopifroomflag40set\s*checkflagset \$(?<bit>0[0-7]), wToggleBlocksState\s*asm15 scriptHelp.mermaidsCave_spawnBridge_room38\s*scriptend') {
    throw 'Mermaid bridge script lost its one-time room flag gate or toggle-bit wait.'
}
$bridgeMask=(1 -shl (Convert-AssemblyInteger $Matches.bit)).ToString('x2')
if ($helper -notmatch '(?ms)^mermaidsCave_spawnBridge_room38:\s*call getThisRoomFlags\s*set (?<bit>[0-7]),\(hl\)\s*ld a,SND_SOLVEPUZZLE\s*call playSound\s*ld bc,\$(?<length>[0-9a-f]{2})(?<angle>[0-9a-f]{2})\s*ld e,\$(?<position>[0-9a-f]{2})\s*jp spawnBridge' -or
    $helper -notmatch '(?ms)^spawnBridge:\s*call getFreePartSlot\s*ret nz\s*ld \(hl\),PART_BRIDGE_SPAWNER\s*ld l,Part.counter2\s*ld \(hl\),b\s*ld l,Part.angle\s*ld \(hl\),c\s*ld l,Part.yh\s*ld \(hl\),e\s*ret') {
    throw "${helperPath}: bridge helper lost flag/sound-before-allocation ordering or part fields."
}
# A second successful -match overwrites $Matches: capture the profile again.
$null=$helper -match '(?ms)^mermaidsCave_spawnBridge_room38:\s*call getThisRoomFlags\s*set (?<bit>[0-7]),\(hl\)\s*ld a,SND_SOLVEPUZZLE\s*call playSound\s*ld bc,\$(?<length>[0-9a-f]{2})(?<angle>[0-9a-f]{2})\s*ld e,\$(?<position>[0-9a-f]{2})\s*jp spawnBridge'
$bridgeFlag=(1 -shl [int]$Matches.bit).ToString('x2')
$bridgePosition=$Matches.position; $bridgeLength=$Matches.length; $bridgeAngle=$Matches.angle
if ($rewardSource -notmatch '(?ms)^mermaidsCaveScript_updateTrigger2BasedOnTriggers0And1:\s*wait (?<wait>[0-9]+)\s*asm15 scriptHelp.setTrigger2IfTriggers0And1Set\s*scriptjump mermaidsCaveScript_updateTrigger2BasedOnTriggers0And1') {
    throw 'Mermaid conjunction script lost its wait/ASM/jump loop.'
}
$conjunctionWait=[int]$Matches.wait
if ($helper -notmatch '(?ms)^setTrigger2IfTriggers0And1Set:\s*ld hl,wActiveTriggers\s*ld a,\(hl\)\s*and \$(?<mask>[0-9a-f]{2})\s*cp \$\k<mask>\s*jr nz,\+\s*set (?<bit>[0-7]),\(hl\)\s*ret\s*\+\s*res \k<bit>,\(hl\)\s*ret') {
    throw "${helperPath}: trigger conjunction lost its masked comparison/set/clear semantics."
}
Write-GeneratedTable((Join-Path $destination 'objects/dungeon_signal_scripts.tsv'),@(
    "# dungeon`tsubid`tkind`ttest-mask`toutput-mask`tposition`thalf-steps`tangle`twait`tsource",
    "0c`t01`tbridge`t$bridgeMask`t$bridgeFlag`t$bridgePosition`t$bridgeLength`t$bridgeAngle`t0`tscripts/ages/dungeonScripts.s:mermaidsCaveScript_spawnBridgeWhenOrbHit->scriptHelp.mermaidsCave_spawnBridge_room38",
    "0c`t02`tconjunction`t$($Matches.mask)`t$((1 -shl [int]$Matches.bit).ToString('x2'))`t00`t00`t00`t$conjunctionWait`tscripts/ages/dungeonScripts.s:mermaidsCaveScript_updateTrigger2BasedOnTriggers0And1->scriptHelp.setTrigger2IfTriggers0And1Set"
))
$rows.Add("5`t37`t0`tessence`t7f`t00`t28`t78`talways`tobjects/ages/mainData.s:group5Map37ObjectData")
$floorControllers = @(Read-AssemblyMacroInvocations $mainPath 'group5Map3fObjectData')
if ($floorControllers.Count -ne 6 -or
    $floorControllers[2].Name -ne 'obj_Interaction' -or
    ($floorControllers[2].Operands -join ' ') -ne '$22 $00 $58 $78' -or
    $floorControllers[3].Name -ne 'obj_Interaction' -or
    ($floorControllers[3].Operands -join ' ') -ne '$15 $00') {
    throw "${mainPath}:group5Map3fObjectData: requires ordered floor-color/toggle controllers at source orders2/3."
}
$rows.Add("5`t3f`t2`tfloor-color-changer`t22`t00`t58`t78`talways`tobjects/ages/mainData.s:group5Map3fObjectData")
$rows.Add("5`t3f`t3`ttoggle-floor`t15`t00`t00`t00`talways`tobjects/ages/mainData.s:group5Map3fObjectData")
Write-GeneratedTable((Join-Path $destination 'objects/mermaid_dungeon_objects.tsv'),$rows)

# Before-event enemies share the normal ENEMY pool. Their insertion ordinal
# counts source rows, including uncounted item-drop producers in pointers.
function Get-MermaidEnemyRowCount([string]$pointer) {
    $count = 0
    foreach ($node in Read-AssemblyMacroInvocations $enemyPath $pointer) {
        switch ($node.Name) {
            'obj_Pointer' { $count += Get-MermaidEnemyRowCount $node.Operands[0] }
            'obj_ItemDrop' { $count++ }
            'obj_RandomEnemy' { $count++ }
            'obj_SpecificEnemyA' { $count++ }
            'obj_SpecificEnemyB' { $count++ }
            'obj_EndPointer' { }
            'obj_End' { }
            default { throw "objects/ages/enemyData.s:${pointer}: unsupported enemy-order macro $($node.Name)." }
        }
    }
    return $count
}
$enemyOrders = [Collections.Generic.List[string]]::new()
$enemyOrders.Add("# group`troom`tid`tsubid`tenemy-stream-order`tsource")
foreach ($room in @('12','36','2d')) {
    $ordinal = 0
    $label = "group5Map${room}ObjectData"
    foreach ($node in Read-AssemblyMacroInvocations $mainPath $label) {
        if ($node.Name -eq 'obj_Pointer') { $ordinal += Get-MermaidEnemyRowCount $node.Operands[0] }
        if ($node.Name -eq 'obj_BeforeEvent') {
            $id = if ($room -eq '12') { '75' } else { '7d' }
            $subid = if ($room -eq '2d') { '01' } else { '00' }
            $enemyOrders.Add("5`t$room`t$id`t$subid`t$ordinal`tobjects/ages/mainData.s:$label")
        }
    }
}
if ($enemyOrders.Count -ne 4) { throw 'Mermaid native enemy ordering requires all three before-event bodies.' }
Write-GeneratedTable((Join-Path $destination 'objects/mermaid_enemy_orders.tsv'),$enemyOrders)

$puzzlePath = Join-Path $Disassembly 'object_code/ages/interactions/miscPuzzles.s'
$puzzle = (Read-ImportText $puzzlePath) -replace '(?m);[^\r\n]*',''
$mirrorRows = [Collections.Generic.List[string]]::new()
$mirrorRows.Add("# id`tsubid`tflag`ttarget-room`tsource")
foreach ($subid in @('08','09')) {
    $pattern = "(?ms)^miscPuzzles_subid${subid}:\s*call interactionDeleteAndRetIfEnabled02\s*call getThisRoomFlags\s*bit (?<bit>ROOMFLAG_BIT_KEYDOOR_(?:UP|RIGHT)),\(hl\)\s*ret z\s*ld l,<ROOM_AGES_5(?<target>[0-9a-f]{2})\s*set \k<bit>,\(hl\)\s*jp interactionDelete"
    if ($puzzle -notmatch $pattern) { throw "${puzzlePath}: room-flag mirror`$$subid lost its outgoing/active-page gates or target write." }
    $bitName = $Matches.bit; $target = $Matches.target
    $constant = @(Read-AssemblyConstants (Join-Path $Disassembly 'constants/common/roomFlags.s') '' $bitName)
    if ($constant.Count -ne 1) { throw "Room-flag mirror`$$subid requires unique $bitName." }
    $mask = (1 -shl (Convert-AssemblyInteger $constant[0].OperandText)).ToString('x2')
    $mirrorRows.Add("90`t$subid`t$mask`t$target`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid$subid")
}
Write-GeneratedTable((Join-Path $destination 'objects/dungeon_room_flag_mirrors.tsv'),$mirrorRows)
$torch = [regex]::Match($puzzle,'(?ms)^miscPuzzles_subid07:(?<body>.*?)(?=^miscPuzzles_subid08:)').Groups['body'].Value
if ($torch -notmatch '(?ms)@state1:\s*call checkLinkVulnerable\s*ret nc.*?ld a, \$ff ~ \(DISABLE_ITEMS \| DISABLE_ALL_BUT_INTERACTIONS\)\s*ld \(wDisabledObjects\),a\s*ld \(wMenuDisabled\),a\s*ld a,CUTSCENE_WALL_RETRACTION\s*ld \(wCutsceneTrigger\),a\s*call getThisRoomFlags\s*ld l,<ROOM_AGES_525\s*set 6,\(hl\)\s*jp interactionDelete' -or
    $torch -notmatch '(?ms)@makeTorchesUnlightable:.*?cp PART_LIGHTABLE_TORCH\s*call z,@deletePartObject.*?@deletePartObject:\s*push hl\s*dec l\s*ld b,\$40\s*call clearMemory' -or
    $torch -notmatch '(?ms)@makeTorchesLightable:\s*call @makeTorchesUnlightable\s*ld hl,objectData.objectData_makeTorchesLightableForD6Room\s*jp parseGivenObjectData') {
    throw "${puzzlePath}: torch controller lost vulnerability, mask, cutscene or part-recreation semantics."
}
$cells = @([regex]::Matches($torch.Substring($torch.IndexOf('@checkLitTorches:')), 'ld (?:hl,wRoomLayout\+|l,)\$(?<cell>[0-9a-f]{2})') | ForEach-Object { $_.Groups['cell'].Value })
$resets = @([regex]::Matches($torch.Substring($torch.IndexOf('@litWrongTorch:')),'ld c,\$(?<cell>[0-9a-f]{2})') | ForEach-Object { $_.Groups['cell'].Value })
$orderBytes = @(Read-AssemblyDataDirectives $puzzlePath '@torchLightOrder' '.db' | ForEach-Object { $_.Operands } | ForEach-Object { Convert-AssemblyInteger $_ })
if (($cells -join ' ') -ne '31 33 53 35' -or ($resets -join ' ') -ne '31 33 35 53' -or $orderBytes.Count -ne 4) { throw "${puzzlePath}: invalid torch order/cell/reset tables." }
$torchRows = [Collections.Generic.List[string]]::new()
$torchRows.Add("# order`tposition`texpected-mask`treset-position`tsource")
for ($index = 0; $index -lt 4; $index++) {
    $torchRows.Add("$index`t$($cells[$index])`t$($orderBytes[$index].ToString('x2'))`t$($resets[$index])`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid07")
}
$torchPointer = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/extraData3.s') 'objectData_makeTorchesLightableForD6Room')
$scannerPointer = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/extraData1.s') 'objectData_makeAllTorchesLightable')
if ($torchPointer.Count -ne 2 -or ($torchPointer[0].Operands -join ' ') -ne 'objectData_makeAllTorchesLightable' -or
    $scannerPointer.Count -ne 2 -or ($scannerPointer[0].Operands -join ' ') -ne '$c7 $08 $06 $10') { throw 'D6 torch recreation requires the ordinary C7:08 scanner pointer.' }
Write-GeneratedTable((Join-Path $destination 'objects/mermaid_torch_order.tsv'),$torchRows)
$wallPath = Join-Path $Disassembly 'code/ages/cutscenes/miscCutscenes.s'
$wall = (Read-ImportText $wallPath) -replace '(?m);[^\r\n]*',''
if ($wall -notmatch '(?ms)^func_701d:.*?@state0:\s*ld a,GFXH_MERMAIDS_CAVE_WALL_RETRACTION.*?ld b,\$10\s*ld hl,wTmpcbb3\s*call clearMemory\s*call reloadTileMap\s*call resetCamera\s*call getThisRoomFlags\s*set 6,\(hl\)\s*call loadTilesetAndRoomLayout\s*ld a,\$(?<initial>[0-9a-f]{2}).*?@cbb3_00:.*?ld \(hl\),\$(?<wait>[0-9a-f]{2}).*?@cbb3_01:\s*ld a,\$(?<shake>[0-9a-f]{2}).*?ld \(hl\),\$(?<interval>[0-9a-f]{2})\s*callab tilesets.generateW3VramTilesAndAttributes\s*ld bc,\$(?<rectangle>[0-9a-f]{4})\s*call func_70f7.*?cp \$(?<steps>[0-9a-f]{2}).*?call func_7098\s*ld a,\$(?<collision>[0-9a-f]{2})\s*ld \(\$ce5d\),a') { throw "${wallPath}: unsupported D6 wall retraction sequence." }
$wallValues = @('initial','wait','shake','interval','rectangle','steps','collision') | ForEach-Object { $Matches[$_] }
$gfxHeaders = Read-ImportText (Join-Path $Disassembly 'data/ages/gfxHeaders.s')
if ($gfxHeaders -notmatch '(?ms)^m_GfxHeaderStart \$72, GFXH_MERMAIDS_CAVE_WALL_RETRACTION\s*m_GfxHeader map_mermaids_cave_wall_retraction, w2TmpGfxBuffer\s*m_GfxHeader flg_mermaids_cave_wall_retraction, w2TmpAttrBuffer\s*m_GfxHeaderEnd') { throw 'D6 wall retraction requires graphics header72 map/attribute pair.' }
foreach ($kind in @('map','flg')) {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $Disassembly "gfx_compressible/ages/${kind}_mermaids_cave_wall_retraction.bin"))
    if ($bytes.Length -ne 576) { throw "D6 wall retraction $kind requires eighteen 32-byte rows." }
    [IO.File]::WriteAllBytes((Join-Path $destination "objects/mermaid_wall_$kind.bin"),$bytes)
}
$wallRows = [Collections.Generic.List[string]]::new()
$wallRows.Add("# initial-wait`tsecond-wait`tshake`tinterval`trectangle`tsteps`tfinal-collision`tsource")
$wallRows.Add("$($wallValues -join "`t")`tcode/ages/cutscenes/miscCutscenes.s:func_701d/func_70f7")
Write-GeneratedTable((Join-Path $destination 'objects/mermaid_wall_retraction.tsv'),$wallRows)
# loadRoomLayout stages $b0 sequential compressed bytes in wRoomCollisions,
# including bytes beyond this room's stream. m_RoomLayoutData omits the
# compression-mode prefix and concatenates declarations across ROM banks.
$loader = (Read-ImportText (Join-Path $Disassembly 'code/bank0.s')) -replace '(?m);[^\r\n]*',''
if ($loader -notmatch '(?ms)^@loadLayoutData:.*?ld b,LARGE_ROOM_HEIGHT\*16\s*ld de,wRoomCollisions\s*-\s*call readByteSequential\s*ld \(de\),a\s*inc e\s*dec b\s*jr nz,-') {
    throw 'bank0.loadRoomLayout: unsupported shared collision-buffer staging.'
}
$scratch = [Collections.Generic.List[byte]]::new()
$copying = $false
foreach ($node in Read-AssemblyNodes (Join-Path $Disassembly 'data/ages/roomLayoutData.s')) {
    if ($node.Kind -ne 'MacroInvocation' -or $node.Name -ne 'm_RoomLayoutData') { continue }
    if ($node.Operands[0] -eq 'room0543') { $copying = $true }
    if (-not $copying) { continue }
    $compressed = [IO.File]::ReadAllBytes((Join-Path $Disassembly "precompressed/rooms/ages/$($node.Operands[0]).cmp"))
    if ($compressed.Length -lt 2 -or $compressed[0] -ne 3) { throw "$($node.Span): expected native dictionary room stream." }
    $scratch.AddRange([byte[]]$compressed[1..($compressed.Length-1)])
    if ($scratch.Count -ge 176) { break }
}
if ($scratch.Count -lt 176) { throw 'room0543: incomplete native layout scratch stream.' }
[IO.File]::WriteAllBytes((Join-Path $destination 'objects/mermaid_wall_layout_scratch.bin'),[byte[]]$scratch.GetRange(0,176).ToArray())
# The first pull bypasses chance RNG. Each failure clears exactly $20 bytes,
# regenerates the global permutation, then parses one ordinary random row.
if ($puzzle -notmatch '(?ms)^miscPuzzles_subid00:.*?@state0:\s*call interactionIncState\s*@state1:\s*ld hl,wLever1PullDistance\s*bit 7,\(hl\)\s*jr nz,\+\s*inc l\s*bit 7,\(hl\)\s*ret z.*?call interactionIncState\s*ld l,Interaction.counter2\s*ld a,\(hl\)\s*or a\s*jr nz,@checkRng\s*ld \(hl\),\$01\s*jr @error\s*@checkRng:\s*call getRandomNumber\s*and \$03\s*jp z,interactionIncState.*?@error:\s*ld a,SND_ERROR\s*call playSound\s*ld a,\(wActiveTilePos\)\s*ld \(wWarpDestPos\),a\s*ld hl,wTmpcec0\s*ld b,\$20\s*call clearMemory\s*callab roomInitialization.generateRandomBuffer\s*ld hl,objectData.objectData78db\s*jp parseGivenObjectData' -or
    $puzzle -notmatch '(?ms)@state2:\s*ld a,\(wNumEnemies\)\s*or a\s*ret nz\s*ld a,\$01\s*ld e,Interaction.state\s*ld \(de\),a\s*ret\s*@state3:\s*ld a,\$01\s*ld \(wActiveTriggers\),a\s*jpab agesInteractionsBank08.spawnChestAndDeleteSelf') {
    throw "${puzzlePath}: boss-key puzzle lost its source initialization/chance/placement/count/chest boundaries."
}
$failurePath = Join-Path $Disassembly 'objects/ages/extraData3.s'
$failureStream = @(Read-AssemblyMacroInvocations $failurePath 'objectData78db')
if ($failureStream.Count -ne 2 -or $failureStream[0].Name -ne 'obj_RandomEnemy' -or
    ($failureStream[0].Operands -join ' ') -ne '$81 $10 $01' -or $failureStream[1].Name -ne 'obj_End') {
    throw "${failurePath}:objectData78db: requires one falling-Rope random row and a terminator."
}
$encoded = Convert-AssemblyInteger $failureStream[0].Operands[0]
$failureId = (Convert-AssemblyInteger $failureStream[0].Operands[1]).ToString('x2')
$failureSubid = (Convert-AssemblyInteger $failureStream[0].Operands[2]).ToString('x2')
Write-GeneratedTable((Join-Path $destination 'objects/mermaid_boss_key_enemies.tsv'),@(
    "# id`tsubid`tflags`tcount`tsource",
    "$failureId`t$failureSubid`t$(($encoded -band 0x1f).ToString('x2'))`t$(($encoded -shr 5) -band 7)`tobjects/ages/extraData3.s:objectData78db"
))
$tileSymbols = @{}
foreach ($name in @('TILEINDEX_SWITCH_DIAMOND','TILEINDEX_STANDARD_FLOOR')) {
    $constant = @(Read-AssemblyConstants (Join-Path $Disassembly 'constants/common/tileIndices.s') '' $name)
    if ($constant.Count -ne 1) { throw "Mermaid patterns require unique tile constant $name." }
    $tileSymbols[$name] = Convert-AssemblyInteger $constant[0].OperandText
}
$patterns = [Collections.Generic.List[string]]::new()
$patterns.Add("# subid`torder`tposition`tminimum-tile`tmaximum-tile`tsource")
foreach ($subid in @('01','03')) {
    $label = if ($subid -eq '01') { '@diamondPositions' } else { '@wantedFloorTiles' }
    if ($puzzle -notmatch "(?ms)^miscPuzzles_subid${subid}:\s*call interactionDeleteAndRetIfEnabled02\s*call miscPuzzles_deleteSelfAndRetIfItemFlagSet\s*ld hl,$label\s*call miscPuzzles_verifyTilesAtPositions\s*ret nz\s*jpab agesInteractionsBank08.spawnChestAndDeleteSelf") {
        throw "${puzzlePath}: pattern handler`$$subid lost its outgoing/item gates or verify/spawn sequence."
    }
    $tile = -1; $order = 0; $terminated = $false
    foreach ($node in Read-AssemblyDataDirectives $puzzlePath $label '.db') {
        foreach ($operand in $node.Operands) {
            if ($terminated) { throw "${puzzlePath}:${label}: pattern data follows its zero terminator." }
            if ($tile -lt 0) {
                if (-not $tileSymbols.ContainsKey($operand)) { throw "${puzzlePath}:${label}: unsupported pattern tile $operand." }
                $tile = $tileSymbols[$operand]
            }
            else {
                $position = Convert-AssemblyInteger $operand
                if ($position -eq 0) { $terminated = $true }
                elseif ($position -eq 255) { $tile = -1 }
                elseif ($position -lt 0 -or $position -gt 255) { throw "${puzzlePath}:${label}: invalid packed position $operand." }
                else {
                    $hex = $tile.ToString('x2')
                    $patterns.Add("$subid`t$order`t$($position.ToString('x2'))`t$hex`t$hex`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid${subid}$label")
                    $order++
                }
            }
        }
    }
    $expected = if ($subid -eq '01') { 6 } else { 3 }
    if (-not $terminated -or $order -ne $expected) { throw "${puzzlePath}:${label}: requires $expected cells and a zero terminator." }
}
if ($puzzle -notmatch '(?ms)^miscPuzzles_subid02:\s*call interactionDeleteAndRetIfEnabled02\s*call objectGetTileAtPosition\s*sub TILEINDEX_RED_TOGGLE_FLOOR\s*ld b,a\s*ld a,\(wRotatingCubePos\)\s*cp l\s*ret nz\s*ld a,\(wRotatingCubeColor\)\s*and \$03\s*cp b\s*ret nz\s*ld c,l\s*ld a,TILEINDEX_STANDARD_FLOOR\s*call setTile\s*ld b,>wRoomCollisions\s*ld a,\$0f\s*ld \(bc\),a\s*ld a,SND_SOLVEPUZZLE\s*call playSound\s*jp interactionDelete') {
    throw "${puzzlePath}: cube floor sensor lost its packed-position/color comparison, tile/collision writes or completion sequence."
}
Write-GeneratedTable((Join-Path $destination 'objects/mermaid_chest_patterns.tsv'),$patterns)
# Source copyMemoryReverse uses DE as source and HL as destination, advancing
# both. The two 65-byte streams retain their native order in shared wBigBuffer.
$floorPath = Join-Path $Disassembly 'ages.s'
$floorSource = (Read-ImportText $floorPath) -replace '(?m);[^\r\n]*',''
if ($floorSource -notmatch '(?ms)^loadD6ChangingFloorPatternToBigBuffer:\s*ld a,b\s*add a\s*ld hl,@changingFloorData\s*rst_addDoubleIndex\s*push hl\s*ldi a,\(hl\)\s*ld d,\(hl\)\s*ld e,a\s*ld b,\$41\s*ld hl,wBigBuffer\s*call copyMemoryReverse\s*pop hl\s*inc hl\s*inc hl\s*ldi a,\(hl\)\s*ld d,\(hl\)\s*ld e,a\s*ld b,\$41\s*ld hl,wBigBuffer\+\$80\s*call copyMemoryReverse') {
    throw "${floorPath}: changing floor lost its ordered 65-byte copies to wBigBuffer and +`$80."
}
$floorPointers = @(Read-AssemblyDataDirectives $floorPath '@changingFloorData' '.dw' | ForEach-Object { $_.Operands })
if (($floorPointers -join ' ') -ne '@tiles0_bottomHalf @tiles0_topHalf @tiles1 @tiles1') {
    throw "${floorPath}: changing floor lost its state/half pointer order or state1 alias."
}
$floorRows = [Collections.Generic.List[string]]::new()
$floorRows.Add("# state`thalf`toffset`tvalue`tsource")
for ($pointer = 0; $pointer -lt 4; $pointer++) {
    $label = $floorPointers[$pointer]
    $bytes = @(Read-AssemblyDataDirectives $floorPath $label '.db' | ForEach-Object { $_.Operands } | ForEach-Object { Convert-AssemblyInteger $_ })
    if ($bytes.Count -ne 65 -or $bytes[64] -ne 0 -or @($bytes[0..63] | Where-Object { $_ -eq 0 }).Count -ne 0 -or
        @($bytes | Where-Object { $_ -eq 255 }).Count -ne 6) { throw "${floorPath}:${label}: requires 58 tiles, six column markers and one terminator." }
    for ($offset = 0; $offset -lt $bytes.Count; $offset++) {
        $floorRows.Add("$([int][Math]::Floor($pointer/2))`t$($pointer%2)`t$offset`t$($bytes[$offset].ToString('x2'))`tages.s:loadD6ChangingFloorPatternToBigBuffer$label")
    }
}
Write-GeneratedTable((Join-Path $destination 'objects/mermaid_changing_floor_patterns.tsv'),$floorRows)
$workerRows = [Collections.Generic.List[string]]::new()
$workerRows.Add("# subid`tposition`ty-step`tx-step`tbuffer-offset`tsource")
$workerData = @(Read-AssemblyDataDirectives $puzzlePath '@data' '.db')
if ($workerData.Count -ne 2 -or ($workerData[0].Operands -join ' ') -ne '$91 $f0 $01 $00' -or ($workerData[1].Operands -join ' ') -ne '$1d $10 $ff $80') {
    throw "${puzzlePath}:miscPuzzles_subid05/06: requires the two native serpentine worker profiles."
}
for ($index = 0; $index -lt 2; $index++) {
    $values = @($workerData[$index].Operands | ForEach-Object { (Convert-AssemblyInteger $_).ToString('x2') })
    $workerRows.Add("$((5+$index).ToString('x2'))`t$($values -join "`t")`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid05/06@data")
}
if ($puzzle -notmatch '(?ms)^miscPuzzles_subid04:.*?ld a,\(wToggleBlocksState\).*?ld \(wDisabledObjects\),a\s*ld \(wMenuDisabled\),a.*?ld c,\$05\s*call @spawnSubid\s*ld c,\$06\s*call @spawnSubid\s*callab bank16.loadD6ChangingFloorPatternToBigBuffer' -or
    $puzzle -notmatch '(?ms)^miscPuzzles_subid05:\s*miscPuzzles_subid06:.*?jp interactionIncSubstate.*?@nextTile:\s*ldi a,\(hl\)\s*or a\s*jr z,@deleteSelf\s*cp \$ff.*?@setTile:.*?call @nextRow\s*ldh a,\(<hFF8B\)\s*jp setTile.*?@deleteSelf:\s*xor a\s*ld \(wDisabledObjects\),a\s*ld \(wMenuDisabled\),a\s*jp interactionDelete') {
    throw "${puzzlePath}: changing floor lost its checked ordered allocations, state-zero worker lifecycle or shared freeze release."
}
Write-GeneratedTable((Join-Path $destination 'objects/mermaid_changing_floor_workers.tsv'),$workerRows)
$initializer = [regex]::Match($puzzle, '(?ms)^miscPuzzles_subid0f:\s+ld hl,wTmpcfc0\.octogonBoss\.loadedExtraGfx\s+xor a\s+ldi \(hl\),a\s+ldi \(hl\),a\s+dec a\s+ldi \(hl\),a\s+ld \(hl\),\$(?<health>[0-9a-f]{2})\s+inc l\s+ld \(hl\),\$(?<y>[0-9a-f]{2})\s+inc l\s+ld \(hl\),\$(?<x>[0-9a-f]{2})\s+inc l\s+ld \(hl\),a\s+jp interactionDelete\s*(?=\w+:)')
if (-not $initializer.Success) { throw "${puzzlePath}: unsupported Octogon initialization writes/termination." }
$wramPath = Join-Path $Disassembly 'include/wram.s'
$wram = Read-ImportText $wramPath
$layout = [regex]::Match($wram, '(?ms)^\.nextu octogonBoss\b(?<body>.*?)(?=^\.nextu)')
$fields = @('loadedExtraGfx','var03','direction','health','y','x','var30','posNeedsFixing')
$values = @(0,0,255,[Convert]::ToInt32($initializer.Groups['health'].Value,16),[Convert]::ToInt32($initializer.Groups['y'].Value,16),[Convert]::ToInt32($initializer.Groups['x'].Value,16),255)
$initialRows = [Collections.Generic.List[string]]::new()
$initialRows.Add("# field`taddress`tvalue`tsource")
for ($index = 0; $index -lt $fields.Count; $index++) {
    $field = [regex]::Match($layout.Groups['body'].Value, "(?m)^\s+$($fields[$index]):\s*;\s*\`$(?<address>[0-9a-f]{4})\s+db\b")
    if (-not $field.Success -or [Convert]::ToInt32($field.Groups['address'].Value,16) -ne 0xcfd0 + $index) {
        throw "${wramPath}: Octogon field '$($fields[$index])' lost its clean-US address/order."
    }
    if ($index -lt $values.Count) {
        $initialRows.Add("$($fields[$index])`t$($field.Groups['address'].Value)`t$($values[$index].ToString('x2'))`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid0f;include/wram.s:octogonBoss.$($fields[$index])")
    }
}
Write-GeneratedTable((Join-Path $destination 'objects/octogon_initial_state.tsv'),$initialRows)
}
