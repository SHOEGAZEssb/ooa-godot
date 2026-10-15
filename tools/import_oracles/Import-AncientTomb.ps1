# D8-specific placements; shared enemies, doors, chests, torches and items
# retain their existing source-ordered import owners.
& {
$main = Join-Path $Disassembly 'objects/ages/mainData.s'
$objects = [Collections.Generic.List[string]]::new()
$objects.Add("# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`tsource")
foreach ($roomId in 0x73..0xaa) {
    $room = $roomId.ToString('x2')
    $label = "group5Map${room}ObjectData"
    $stream = @(Read-AssemblyMacroInvocations $main $label)
    for ($order=0; $order -lt $stream.Count; $order++) {
        $node=$stream[$order]
        if ($node.Name -ne 'obj_Interaction') { continue }
        $id=Convert-AssemblyInteger $node.Operands[0]
        $subid=Convert-AssemblyInteger $node.Operands[1]
        $kind = switch ($id) {
            0x7b { 'stone-panel' }
            0x82 { 'sarcophagus' }
            0x77 { 'enemy-attached-key' }
            0x19 { 'colored-cube' }
            0x1b { 'minecart-gate' }
            0x21 { switch ($subid) { 3 { 'cube-light-sensor' } 8 { 'cube-switch-sensor' } 0x11 { 'floor-fill-chest' } } }
            0x25 { 'tile-filler' }
            0x61 { 'lever' }
            0x7d { 'spinner' }
            0xd8 { 'lever-lava-filler' }
            0xdb { 'slate-slot' }
            0x90 { if ($subid -in @(0x0d,0x0e)) { 'panel-stairs' } }
            0x7f { 'essence' }
            0x20 { if ($subid -eq 0) { 'miniboss-reward' } elseif ($subid -eq 1) { 'boss-reward' } elseif ($subid -in 2..6) { 'tile-script' } }
        }
        if (!$kind) { continue }
        $y=if ($node.Operands.Count -ge 4) { Convert-AssemblyInteger $node.Operands[2] } else { 0 }
        $x=if ($node.Operands.Count -ge 4) { Convert-AssemblyInteger $node.Operands[3] } else { 0 }
        $objects.Add("5`t$room`t$order`t$kind`t$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`talways`tobjects/ages/mainData.s:$label")
    }
}
$blueStream = @(Read-AssemblyMacroInvocations $main 'group5Map82ObjectData')
$blueEnemies = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/enemyData.s') 'group5Map82BeforeEventObjectData' 'obj_SpecificEnemyA')
if ($blueStream[4].Name -ne 'obj_BeforeEvent' -or $blueStream[4].Operands[0] -ne 'group5Map82BeforeEventObjectData' -or
    $blueEnemies.Count -ne 1 -or ($blueEnemies[0].Operands -join ',') -ne '$00,$77,$00,$00,$78') {
    throw 'Blue Stalfos requires counted ENEMY$77:$00 at Y$00/X$78, before-event room$5:$82/order$04.'
}
$objects.Add("5`t82`t4`tblue-stalfos`t77`t00`t00`t78`tflag80-clear`tobjects/ages/enemyData.s:group5Map82BeforeEventObjectData")
$ramrockStream = @(Read-AssemblyMacroInvocations $main 'group5Map78ObjectData')
$ramrockEnemies = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/enemyData.s') 'group5Map78BeforeEventObjectData' 'obj_SpecificEnemyA')
if ($ramrockStream[5].Name -ne 'obj_BeforeEvent' -or $ramrockStream[5].Operands[0] -ne 'group5Map78BeforeEventObjectData' -or
    $ramrockEnemies.Count -ne 1 -or ($ramrockEnemies[0].Operands -join ',') -ne '$00,$07,$00,$48,$78') {
    throw 'Ramrock requires counted ENEMY$07:$00 at Y$48/X$78, before-event room$5:$78/order$05.'
}
$objects.Add("5`t78`t5`tramrock`t07`t00`t48`t78`tflag80-clear`tobjects/ages/enemyData.s:group5Map78BeforeEventObjectData")
if ($objects.Count -ne 45) { throw "Ancient Tomb requires 44 supported native placements; got $($objects.Count - 1)." }
Write-GeneratedTable((Join-Path $destination 'objects/ancient_tomb_objects.tsv'),$objects)
$helper = Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$torchRows = @(Read-AssemblyDataDirectives $helper '@torchPositions' '.db')
$torchValues = @($torchRows | ForEach-Object { foreach($value in $_.Operands) { Convert-AssemblyInteger $value } })
if (($torchValues -join ',') -ne '134,136,75,107,38,40,67,99') { throw 'slateSlot_placeSlate: source torch pairing changed.' }
Write-GeneratedTable((Join-Path $destination 'objects/slate_slot_torches.tsv'),@(
    "# subid`tfirst`tsecond`tsource"
    for($i=0;$i -lt 4;$i++) { "$($i.ToString('x2'))`t$($torchValues[$i*2].ToString('x2'))`t$($torchValues[$i*2+1].ToString('x2'))`tscripts/ages/scriptHelper.s:slateSlot_placeSlate/@torchPositions" }
))
$panel = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/stonePanel.s')
$puzzles = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/miscPuzzles.s')
$slot = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/slateSlot.s')
$sarcophagusPath = Join-Path $Disassembly 'object_code/ages/interactions/sarcophagus.s'
$sarcophagus = (Read-ImportText $sarcophagusPath) -replace '(?m);[^\r\n]*',''
foreach ($contract in @(
    '(?ms)^@state0:.*?bit 7,a.*?jp nz,@break.*?or a.*?bit 6,a.*?interactionDelete.*?collisionRadiusY\s*ld \(hl\),\$10.*?collisionRadiusX\s*ld \(hl\),\$08.*?objectMakeTileSolid.*?wRoomLayout\s*ld \(hl\),\$00.*?sub \$10.*?ld \(hl\),\$00.*?wRoomCollisions\s*ld \(hl\),\$0f.*?objectSetVisible83',
    '(?ms)^@state1:\s*ld a,\(wBraceletLevel\)\s*cp \$02\s*ret c\s*jp objectAddToGrabbableObjectBuffer',
    '(?ms)^@substate0_justGrabbed:.*?interactionIncSubstate.*?Interaction.subid.*?or a.*?dec a.*?SND_SOLVEPUZZLE\s*call z,playSound.*?set 6,\(hl\).*?getTileIndexFromRoomLayoutBuffer\s*call setTile.*?sub \$10\s*call getTileIndexFromRoomLayoutBuffer\s*call setTile\s*xor a\s*ld \(wLinkGrabState2\),a\s*jp objectSetVisiblec1',
    '(?ms)^@substate2_justReleased:.*?res 1,\(hl\).*?Interaction.zh\s*bit 7,\(hl\)\s*ret nz\s*@break:.*?ld \(hl\),\$03.*?ld \(hl\),\$02.*?ld a,\$0c.*?ld \(hl\),\$40.*?objectSetVisible83.*?interactionSetAnimation',
    '(?ms)^@state3:\s*call interactionDecCounter1\s*ld a,SND_KILLENEMY\s*call z,playSound\s*ld e,Interaction.animParameter\s*ld a,\(de\)\s*inc a\s*jp nz,interactionAnimate\s*jp interactionDelete'
)) { if ($sarcophagus -notmatch $contract) { throw "${sarcophagusPath}: unsupported sarcophagus state flow: $contract" } }
$keyPath = Join-Path $Disassembly 'object_code/ages/interactions/smallKeyOnEnemy.s'
$key = (Read-ImportText $keyPath) -replace '(?m);[^\r\n]*',''
foreach ($contract in @(
    '(?ms)^@state0:.*?ROOMFLAG_ITEM.*?Interaction.subid.*?FIRST_ENEMY_INDEX, Enemy.id.*?@nextEnemy:.*?cp b\s*jr z,@foundMatch\s*inc h.*?LAST_ENEMY_INDEX\+1.*?\.ifdef ENABLE_BUGFIXES.*?\.else\s*jp c,interactionDelete\s*\.endif\s*jr @nextEnemy',
    '(?ms)^@foundMatch:.*?Interaction.relatedObj2.*?interactionInitGraphics.*?objectSetVisible80.*?interactionIncState.*?@takeRelatedObj2Position:.*?Object.y.*?objectTakePosition',
    '(?ms)^@state1:.*?Object.enabled.*?or a\s*jp z,interactionIncState.*?call @takeRelatedObj2Position.*?ld b,\$01\s*jp objectFlickerVisibility',
    '(?ms)^@state2:\s*call objectSetVisible\s*ld c,\$20\s*call objectUpdateSpeedZ_paramC\s*ret nz\s*ldbc TREASURE_SMALL_KEY,\$00\s*call createTreasure\s*call objectCopyPosition\s*jp interactionDelete'
)) { if ($key -notmatch $contract) { throw "${keyPath}: unsupported clean-US key attachment flow: $contract" } }
if ($panel -notmatch '(?ms)^@state0:.*?ld bc,\$0e08.*?PALH_7e.*?^@state1:.*?ld a,60.*?^@substate0:.*?ld \(hl\),\$80.*?SPEED_20.*?^@substate1:.*?wFrameCounter.*?rrca.*?ld \(hl\),30.*?^@substate2:.*?res 7,\(hl\).*?set 6,\(hl\)' -or
    $puzzles -notmatch '(?ms)^miscPuzzles_subid0d:.*?cp \$01.*?cp \$07.*?ld a,30.*?ld a,\$08.*?@state1:.*?cp \$07.*?set 7,\(hl\).*?^miscPuzzles_subid0e:.*?ROOMFLAG_01\|ROOMFLAG_02\|ROOMFLAG_04\|ROOMFLAG_08.*?set 7,\(hl\)' -or
    $slot -notmatch '(?ms)^@state1:.*?objectCheckCollidedWithLink_notDead.*?objectCheckLinkPushingAgainstCenter.*?dec \(hl\).*?wNumSlates.*?TX_5111.*?ld a,\$0a.*?@placeSlate:.*?checkLinkVulnerable') {
    throw 'Ancient Tomb panel/socket/stair source timing or eligibility changed.'
}
$texts = [Collections.Generic.List[string]]::new()
$texts.Add("# text-id`ttext-position`tmessage-base64`tsource")
foreach($id in @(0x5111,0x0015)) {
    if (!$allTexts.ContainsKey($id)) { throw "Missing Ancient Tomb text `$$($id.ToString('x4'))." }
    $pos=if($allTextPositions.ContainsKey($id)){$allTextPositions[$id]}else{0}
    $texts.Add("$($id.ToString('x4'))`t$pos`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$id])))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'objects/ancient_tomb_text.tsv'),$texts)
$essence = Join-Path $Disassembly 'object_code/common/interactions/essence.s'
$warps = @(Read-AssemblyDataDirectives $essence '@essenceWarps' '.db')
if (($warps[7].Operands -join ',') -ne '$81,$5c,$15,TRANSITION_DEST_X_SHIFTED') { throw 'Falling Star Essence exit must be $1:$5c/$15 X-shifted.' }
Write-GeneratedTable((Join-Path $destination 'objects/ancient_tomb_essence.tsv'),@(
    "# index`tdestination-group`tdestination-room`tdestination-position`tdestination-transition`tsource",
    "7`t1`t5c`t15`t0e`tobject_code/common/interactions/essence.s:@essenceWarps"
))

$scriptPath = Join-Path $Disassembly 'scripts/ages/dungeonScripts.s'
$scriptSource = (Read-ImportText $scriptPath) -replace '(?m);[^\r\n]*',''
$scriptTable = @(Read-AssemblyDataDirectives (Join-Path $Disassembly 'object_code/ages/interactions/dungeonScript.s') '@dungeon8' '.dw')
$expectedScripts = @('dungeonScript_minibossDeath','dungeonScript_bossDeath',
    'ancientTombScript_spawnSouthStairsWhenTrigger0Active','ancientTombScript_spawnNorthStairsWhenTrigger0Active',
    'ancientTombScript_retractWallWhenTrigger0Active','ancientTombScript_spawnDownStairsWhenEnemiesKilled',
    'ancientTombScript_spawnVerticalBridgeWhenTorchLit')
if (($scriptTable.Operands -join ',') -ne (($expectedScripts | ForEach-Object { 'mainScripts.' + $_ }) -join ',')) {
    throw 'dungeonScript.s:@dungeon8 dispatch order changed.'
}
foreach ($contract in @(
    '(?ms)^ancientTombScript_spawnSouthStairsWhenTrigger0Active:\s*stopifroomflag40set\s*checkmemoryeq wActiveTriggers, \$01\s*settilehere \$50\s*ancientTombScript_finishMakingStairs:\s*orroomflag \$40\s*asm15 objectCreatePuff\s*playsound SND_SOLVEPUZZLE\s*scriptend',
    '(?ms)^ancientTombScript_spawnNorthStairsWhenTrigger0Active:\s*stopifroomflag40set\s*checkmemoryeq wActiveTriggers, \$01\s*settilehere \$52\s*scriptjump ancientTombScript_finishMakingStairs',
    '(?ms)^ancientTombScript_retractWallWhenTrigger0Active:\s*stopifroomflag40set\s*checkmemoryeq wActiveTriggers, \$01\s*disableinput\s*wait 30\s*asm15 scriptHelp.ancientTomb_startWallRetractionCutscene\s*scriptend',
    '(?ms)^ancientTombScript_spawnDownStairsWhenEnemiesKilled:\s*stopifroomflag80set\s*wait 30\s*checknoenemies\s*playsound SND_SOLVEPUZZLE\s*asm15 objectCreatePuff\s*settilehere \$45\s*orroomflag \$80\s*scriptend',
    '(?ms)^ancientTombScript_spawnVerticalBridgeWhenTorchLit:\s*checkmemoryeq wNumTorchesLit, \$01\s*settilehere \$6a\s*playsound SND_SOLVEPUZZLE\s*createpuff\s*scriptend'
)) { if ($scriptSource -notmatch $contract) { throw "${scriptPath}: unsupported Ancient Tomb script control flow: $contract" } }
Write-GeneratedTable((Join-Path $destination 'objects/ancient_tomb_tile_scripts.tsv'),@(
    "# subid`tkind`tgate-value`ttile`tflag`twait`tsource",
    "02`ttrigger-stairs`t01`t50`t40`t0`tscripts/ages/dungeonScripts.s:$($expectedScripts[2])->ancientTombScript_finishMakingStairs",
    "03`ttrigger-stairs`t01`t52`t40`t0`tscripts/ages/dungeonScripts.s:$($expectedScripts[3])->ancientTombScript_finishMakingStairs",
    "04`twall-retraction`t01`t00`t40`t30`tscripts/ages/dungeonScripts.s:$($expectedScripts[4])",
    "05`tenemy-stairs`t00`t45`t80`t30`tscripts/ages/dungeonScripts.s:$($expectedScripts[5])",
    "06`ttorch-bridge`t01`t6a`t00`t0`tscripts/ages/dungeonScripts.s:$($expectedScripts[6])"
))
$wallPath = Join-Path $Disassembly 'code/ages/cutscenes/miscCutscenes.s'
$wall = (Read-ImportText $wallPath) -replace '(?m);[^\r\n]*',''
if ($wall -notmatch '(?ms)^func_701d:\s*ld a,\(wDungeonIndex\)\s*cp \$08\s*jp z,wallRetraction_dungeon8.*?@func_702f:.*?ld a,\$(?<initial>[0-9a-f]{2}).*?@cbb3_00:.*?ld \(hl\),\$(?<wait>[0-9a-f]{2})' -or
    $wall -notmatch '(?ms)^wallRetraction_dungeon8:.*?@state0:\s*ld a,GFXH_ANCIENT_TOMB_WALL_RETRACTION\s*jp func_701d@func_702f.*?@cbb3_01:\s*ld a,\$(?<shake>[0-9a-f]{2}).*?ld \(hl\),\$(?<interval>[0-9a-f]{2})\s*callab tilesets.generateW3VramTilesAndAttributes\s*ld bc,\$(?<rectangle>[0-9a-f]{4})\s*call func_70f7.*?cp \$(?<steps>[0-9a-f]{2})\s*ret c\s*jr func_7098') {
    throw "${wallPath}: unsupported D8/shared wall-retraction flow."
}
# The shared prefix establishes both waits; the D8 branch establishes geometry.
$wallBranch = @('shake','interval','rectangle','steps') | ForEach-Object { $Matches[$_] }
if ($wall -notmatch '(?ms)^func_701d:.*?@func_702f:.*?ld a,\$(?<initial>[0-9a-f]{2}).*?@cbb3_00:.*?ld \(hl\),\$(?<wait>[0-9a-f]{2})') { throw 'Missing shared wall waits.' }
$wallValues = @($Matches.initial,$Matches.wait) + $wallBranch
if (($wallValues -join ',') -ne '3c,3c,3c,19,4d04,0b') { throw 'Ancient Tomb wall operands changed.' }
Write-GeneratedTable((Join-Path $destination 'objects/ancient_tomb_wall_retraction.tsv'),@(
    "# initial-wait`tsecond-wait`tshake`tinterval`trectangle`tsteps`tfinal-collision`tsource",
    "$($wallValues -join "`t")`t-`tcode/ages/cutscenes/miscCutscenes.s:wallRetraction_dungeon8->func_701d@func_702f/func_70f7/func_7098"
))
$headers = Read-ImportText (Join-Path $Disassembly 'data/ages/gfxHeaders.s')
if ($headers -notmatch '(?ms)^m_GfxHeaderStart \$73, GFXH_ANCIENT_TOMB_WALL_RETRACTION\s*m_GfxHeader map_ancient_tomb_wall_retraction, w2TmpGfxBuffer\s*m_GfxHeader flg_ancient_tomb_wall_retraction, w2TmpAttrBuffer\s*m_GfxHeaderEnd') { throw 'D8 wall requires GFXH$73 map/attribute pair.' }
foreach ($kind in @('map','flg')) {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $Disassembly "gfx_compressible/ages/${kind}_ancient_tomb_wall_retraction.bin"))
    if ($bytes.Length -ne 384) { throw "D8 wall $kind requires twelve 32-byte rows." }
    [IO.File]::WriteAllBytes((Join-Path $destination "objects/ancient_tomb_wall_$kind.bin"),$bytes)
}
$scratch = [Collections.Generic.List[byte]]::new()
$copying = $false
foreach ($node in Read-AssemblyNodes (Join-Path $Disassembly 'data/ages/roomLayoutData.s')) {
    if ($node.Kind -ne 'MacroInvocation' -or $node.Name -ne 'm_RoomLayoutData') { continue }
    if ($node.Operands[0] -eq 'room0595') { $copying = $true }
    if (!$copying) { continue }
    $compressed = [IO.File]::ReadAllBytes((Join-Path $Disassembly "precompressed/rooms/ages/$($node.Operands[0]).cmp"))
    if ($compressed.Length -lt 2 -or $compressed[0] -ne 3) { throw "$($node.Span): expected dictionary room stream." }
    $scratch.AddRange([byte[]]$compressed[1..($compressed.Length-1)])
    if ($scratch.Count -ge 176) { break }
}
if ($scratch.Count -lt 176) { throw 'room0595: incomplete native layout scratch stream.' }
[IO.File]::WriteAllBytes((Join-Path $destination 'objects/ancient_tomb_wall_layout_scratch.bin'),[byte[]]$scratch.GetRange(0,176).ToArray())

$bluePath = Join-Path $Disassembly 'object_code/ages/enemies/blueStalfos.s'
$blueSource = (Read-ImportText $bluePath) -replace '(?m);[^\r\n]*',''
foreach ($contract in @(
    '(?ms)^blueStalfos_state_spawner:.*?ld b,\$03.*?checkBEnemySlotsAvailable.*?ecom_spawnUncountedEnemyWithSubid01.*?Enemy.enabled.*?ld \(hl\),a.*?ecom_spawnUncountedEnemyWithSubid01.*?Enemy.relatedObj1.*?ecom_spawnUncountedEnemyWithSubid01.*?ld \(hl\),\$03.*?enemyDelete',
    '(?ms)^blueStalfos_main_state08:\s*ld bc,\$010b.*?enemyBoss_spawnShadow.*?SPEED_200.*?ANGLE_DOWN.*?^blueStalfos_main_state09:.*?cp \$58.*?ld \(hl\),\$40.*?SPEED_20.*?MUS_MINIBOSS.*?ld e,\$0f.*?ld bc,\$3030.*?ecom_randomBitwiseAndBCE',
    '(?ms)^blueStalfos_main_state0a:.*?add \$04.*?cp \$09.*?add \$04.*?cp \$09.*?ld \(hl\),\$10.*?^blueStalfos_main_state0c:.*?add \$10.*?add \$04.*?add \$14.*?cp \$29.*?add \$12.*?cp \$25.*?PART_BLUE_STALFOS_PROJECTILE.*?ld \(hl\),120',
    '(?ms)^blueStalfos_main_state0d:.*?enemyAnimate.*?Enemy.animParameter.*?ld a,\$08.*?SND_SWORDSPIN.*?^blueStalfos_main_state0e:.*?ld \(hl\),60.*?ld a,\$03.*?^blueStalfos_main_finishedAttack:.*?ld \(hl\),\$40.*?SPEED_20',
    '(?ms)^blueStalfos_main_state10:.*?ld \(hl\),\$10.*?res 7,\(hl\).*?^blueStalfos_main_state11:.*?ld \(hl\),\$08.*?set 7,\(hl\).*?ld \(hl\),\$0c.*?hEnemyTargetX.*?^blueStalfos_main_state12:.*?ld \(hl\),\$10.*?and \$03',
    '(?ms)^blueStalfos_main_state14:.*?blueStalfos_createPuff.*?ld \(hl\),\$02.*?ld \(hl\),\$06.*?ld \(hl\),\$f0.*?SPEED_c0.*?SND_SCENT_SEED.*?ld a,\$04.*?^blueStalfos_main_state16:.*?ld \(hl\),30.*?ecom_setRandomAngle.*?ecom_bounceOffWallsAndHoles',
    '(?ms)^blueStalfos_main_state17:.*?Enemy.collisionRadiusY.*?ld a,\$08.*?blueStalfos_main_finishedAttack.*?^blueStalfos_main_accelerate:.*?and \$03.*?add SPEED_20'
)) { if ($blueSource -notmatch $contract) { throw "${bluePath}: unsupported Blue Stalfos state control flow: $contract" } }
$blueRows = [Collections.Generic.List[string]]::new()
$blueRows.Add("# profile`tindex`tvalue`tsource")
function Add-BlueTable([string]$name, [object[]]$values, [string]$source) {
    for ($index=0;$index -lt $values.Count;$index++) { $blueRows.Add("$name`t$index`t$($values[$index])`t$source") }
}
Add-BlueTable 'state' @(3,1,11,0x50,16,0x58,0x40,5,4,16,16,4,20,18,120,60,16,12,8,2,6,240,30,30,8) 'object_code/ages/enemies/blueStalfos.s:guarded-state-operands'
foreach ($spec in @(@('quadrants','@quadrantList',16),@('sickle-boxes','@positionAndHitboxTable',36))) {
    $values = @(Read-AssemblyLiteralValues $bluePath $spec[1])
    if ($values.Count -ne $spec[2]) { throw "${bluePath}:$($spec[1]) requires $($spec[2]) ordered operands." }
    Add-BlueTable $spec[0] $values "object_code/ages/enemies/blueStalfos.s:$($spec[1])"
}
$targets = @(Read-AssemblyDataDirectives $bluePath '@targetPositions' '.dw' | ForEach-Object {
    foreach ($operand in $_.Operands) { $value = Convert-AssemblyInteger $operand; $value -band 255; $value -shr 8 }
})
if ($targets.Count -ne 8) { throw 'Blue Stalfos requires four little-endian target-position words.' }
Add-BlueTable 'targets' $targets 'object_code/ages/enemies/blueStalfos.s:@targetPositions'
$speeds = @(Read-AssemblyDataDirectives $bluePath '@speedTable' '.db')
if (($speeds.Operands -join ',') -ne 'SPEED_180,SPEED_1c0,SPEED_200,SPEED_300') { throw 'Blue Stalfos baby-chase speed table changed.' }
Add-BlueTable 'baby-speeds' @(0x3c,0x46,0x50,0x78) 'object_code/ages/enemies/blueStalfos.s:@speedTable'
$bluePartPath = Join-Path $Disassembly 'object_code/ages/parts/blueStalfosProjectile.s'
$bluePart = (Read-ImportText $bluePartPath) -replace '(?m);[^\r\n]*',''
foreach ($contract in @(
    '(?ms)^@state0:.*?ld \(hl\),40.*?sub \$18.*?SPEED_180.*?Object.var32.*?inc a.*?and \$07.*?ENEMYCOLLISION_PODOBOO.*?SND_BLUE_STALFOS_CHARGE',
    '(?ms)^@state1:.*?ld \(hl\),40.*?cp \$03.*?ld \(hl\),20.*?objectGetAngleTowardEnemyTarget.*?SND_BEAM1',
    '(?ms)^blueStalfosProjectile_checkShouldExplode:.*?and \$07.*?ld c,\$28.*?dec \(hl\).*?ld \(hl\),\$06',
    '(?ms)^blueStalfosProjectile_checkCollidedWithStalfos:.*?checkObjectsCollided.*?ld \(hl\),30.*?ld \(hl\),\$14',
    '(?ms)^blueStalfosProjectile_subid1_uninitialized:.*?SPEED_1c0.*?ld \(hl\),-4.*?ld a,\$02.*?add a.*?^blueStalfosProjectile_hitLink:.*?ld \(hl\),\$10'
)) { if ($bluePart -notmatch $contract) { throw "${bluePartPath}: unsupported Blue Stalfos projectile control flow: $contract" } }
Add-BlueTable 'part-state' @(40,24,0x3c,0x50,3,20,7,40,6,30,0x46,4,2) 'object_code/ages/parts/blueStalfosProjectile.s:guarded-state-operands'
foreach ($spec in @(@('part-pattern','@ballPatterns',1),@('part-angles','@angleVals',7))) {
    $values = @(Read-AssemblyLiteralValues $bluePartPath $spec[1])
    if ($values.Count -ne $spec[2]) { throw "${bluePartPath}:$($spec[1]) requires $($spec[2]) operands." }
    Add-BlueTable $spec[0] $values "object_code/ages/parts/blueStalfosProjectile.s:$($spec[1])"
}
Add-BlueTable 'part-boundary-offsets' @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/parts/commonCode.s') 'partCommon_anglePositionOffsets') 'object_code/common/parts/commonCode.s:partCommon_anglePositionOffsets'
$collisionValues = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
foreach ($mode in @(0x48,0x65,0x66,0x77,4)) {
    Add-BlueTable "effects-$($mode.ToString('x2'))" $collisionValues[($mode*32)..($mode*32+31)] "data/ages/objectCollisionTable.s:mode`$$($mode.ToString('x2'))"
}
$active = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/enemyActiveCollisions.s') 'enemyActiveCollisions' 'dbrev')
$bits = ($active[0x77].Operands -join '').Replace('%','')
if ($bits -notmatch '^[01]{32}$') { throw 'ENEMY$77 requires its full original collision mask.' }
Add-BlueTable 'active-77' @($bits.ToCharArray() | ForEach-Object { [int]::Parse($_) }) 'data/ages/enemyActiveCollisions.s:enemy$77'
Write-GeneratedTable((Join-Path $destination 'enemies/blue_stalfos.tsv'),$blueRows)

$ramrockPath = Join-Path $Disassembly 'object_code/ages/enemies/ramrock.s'
$ramrock = (Read-ImportText $ramrockPath) -replace '(?m);[^\r\n]*',''
foreach ($contract in @(
    '(?ms)^enemyCode07:.*?ramrock_state8.*?ramrock_swordPhase.*?ramrock_bombPhase.*?ramrock_seedPhase.*?ramrock_glovePhase',
    '(?ms)^ramrock_state0:.*?ENEMY_RAMROCK.*?PALH_83.*?SPEED_100.*?ld a,\$04.*?ld b,\$00\s*ld c,\$0c.*?enemyBoss_spawnShadow',
    '(?ms)^ramrock_state8_substate0:.*?DISABLE_LINK.*?wDisabledObjects.*?wMenuDisabled.*?\$cc93.*?Enemy.stunCounter\s*ld a,60.*?^ramrock_state8_substate1:.*?ld bc,-\$80.*?cp \$f9.*?ld c,\$01.*?ENEMY_RAMROCK_ARMS.*?dec c\s*jr z,@spawnArm',
    '(?ms)^ramrock_state8_substate2:.*?cp \$02.*?ld a,\$02.*?PALH_84.*?^ramrock_state8_substate3:.*?SND_SWORD_OBTAINED.*?inc \(hl\).*?PALH_83.*?^ramrock_state8_substate5:.*?cp \$41.*?wDisabledObjects.*?wMenuDisabled.*?ld \(hl\),\$08.*?ld \(hl\),\$03.*?MUS_BOSS',
    '(?ms)^ramrock_swordPhase:.*?Enemy.var35.*?cp \$03.*?objectSetSpeedZ.*?ecom_incState.*?Enemy.subid.*?inc \(hl\).*?ld \(hl\),30',
    '(?ms)^ramrock_bombPhase:.*?ITEM_BOMB.*?checkObjectsCollided.*?Item.angle.*?\$ff.*?findItemWithID_startingAfterH.*?^ramrock_bombPhase_substate0:.*?cp \$06.*?ld \(hl\),\$02.*?ld \(hl\),\$03',
    '(?ms)^ramrock_bombPhase_substate3:.*?Enemy.var38.*?sub \$01.*?ld a,30.*?SND_BEAM1.*?cp \$03.*?ecom_applyVelocityForSideviewEnemy.*?ld \(hl\),\$04.*?objectNudgeAngleTowards.*?^label_10_237:.*?ld \(hl\),\$0a',
    '(?ms)^ramrock_seedPhase:.*?REGION_JP.*?ITEMCOLLISION_MYSTERY_SEED.*?ITEMCOLLISION_GALE_SEED\+1.*?@seedCollision:.*?ld \(hl\),60.*?cp \$03\s*jr z,@seedPhaseEnd\s*inc \(hl\).*?@seedPhaseEnd:.*?ld \(hl\),\$10.*?ecom_incState',
    '(?ms)^ramrock_seedPhase_substate0:.*?ld bc,\$4878.*?ld c,\$04.*?ENEMY_RAMROCK_ARMS.*?cp \$05.*?^ramrock_seedPhase_substate2:.*?getRandomNumber\s*rrca.*?getRandomNumber\s*and \$03.*?ld \(hl\),\$0f.*?ld \(hl\),60.*?PART_RAMROCK_SEED_FORM_ORB.*?ld bc,\$1000',
    '(?ms)^ramrock_seedPhase_substate3:.*?ld \(hl\),180.*?ld \(hl\),30.*?PART_RAMROCK_SEED_FORM_LASER.*?ld \(hl\),\$0e.*?ld bc,\$0400.*?^ramrock_seedPhase_substate4:.*?SND_SWORDBEAM.*?ld \(hl\),90',
    '(?ms)^ramrock_glovePhase_substate0:.*?ld bc,\$4878.*?ld bc,\$e001.*?PART_RAMROCK_GLOVE_FORM_ARM.*?ld b,\$18.*?^ramrock_glovePhase_substate1:.*?cp \$03.*?PALH_84',
    '(?ms)^ramrock_glovePhase_gotoSubstate3:.*?ld bc,-\$80.*?ld \(hl\),\$11.*?ld \(hl\),120.*?^ramrock_glovePhase_substate3:.*?ld c,\$50.*?ld a,\$12.*?getRandomNumber\s*and \$01.*?getRandomNumber\s*and \$0f.*?set 5,\(hl\).*?^ramrock_glovePhase_substate4:.*?cp \$03.*?Enemy.health.*?enemyBoss_dead'
)) { if ($ramrock -notmatch $contract) { throw "${ramrockPath}: unsupported Ramrock phase control flow: $contract" } }
$ramrockRows = [Collections.Generic.List[string]]::new()
$ramrockRows.Add("# profile`tindex`tvalue`tsource")
function Add-RamrockTable([string]$name, [object[]]$values, [string]$source) {
    for ($index=0;$index -lt $values.Count;$index++) { $ramrockRows.Add("$name`t$index`t$($values[$index])`t$source") }
}
$armPath = Join-Path $Disassembly 'object_code/ages/enemies/ramrockArms.s'
$arms = (Read-ImportText $armPath) -replace '(?m);[^\r\n]*',''
foreach ($contract in @(
    '(?ms)^ramrockArm_state0:.*?and \$7f.*?@initSubid0:.*?ld \(hl\),\$10.*?ld \(hl\),\$f9.*?ld \(hl\),\$08.*?SPEED_180.*?@initSubid2:.*?add \$02.*?ramrockArm_setRelativePosition.*?ld \(hl\),\$81.*?@initSubid4:.*?ld \(hl\),\$4e.*?ld \(hl\),\$81.*?ld \(hl\),\$04',
    '(?ms)^ramrockArm_subid0:.*?cp \$04.*?ld a,\$06.*?ld a,60.*?^ramrockArm_subid0_substate2:.*?ld b,\$04.*?cp \$10.*?ld \(hl\),\$06.*?SPEED_100.*?SND_BIGSWORD',
    '(?ms)^ramrockArm_subid0_substate3:.*?objectApplySpeed.*?ITEMCOLLISION_LINK.*?ITEMCOLLISION_L1_SWORD.*?ITEMCOLLISION_L2_SWORD.*?ITEMCOLLISION_L3_SWORD.*?ld a,\$05.*?SPEED_200.*?xor \$10.*?ecom_getSideviewAdjacentWallsBitset.*?ld \(hl\),\$06.*?objectNudgeAngleTowards',
    '(?ms)^ramrockArm_subid0_substate5:.*?checkObjectsCollided.*?Enemy.invincibilityCounter.*?ld \(hl\),60.*?Enemy.var35.*?inc \(hl\).*?SND_BOSS_DAMAGE.*?^ramrockArm_subid0_substate6:.*?add \$04.*?objectCreatePuff.*?inc \(hl\).*?ramrockArm_deleteSelf',
    '(?ms)^ramrockArm_subid2_substate2:.*?cp \$0a.*?cp \$09.*?ITEM_BOMB.*?ld e,\$06.*?Item.zh.*?cp \$fc.*?Item.var2f.*?set 4,\(hl\).*?Object.invincibilityCounter.*?ld \(hl\),60.*?Enemy.var35.*?inc \(hl\)',
    '(?ms)^ramrockArm_subid4_substate0:.*?ld a,\$06.*?ld bc,-\$80.*?SPEED_100.*?ld \(hl\),62.*?^ramrockArm_subid4_substate1:.*?cp \$f9.*?ld \(hl\),\$0c.*?^ramrockArm_subid4_substate2:.*?REGION_JP.*?cp \$0d.*?cp \$10.*?cp \$0c.*?^ramrockArm_subid4_substate3:.*?ld \(hl\),\$10.*?sub \$02.*?cp \$04'
)) { if ($arms -notmatch $contract) { throw "${armPath}: unsupported Ramrock hand control flow: $contract" } }
foreach ($spec in @(
    @('ramrockGloveFormArm.s','(?ms)^partCode35:.*?Object.health.*?jp z,partDelete.*?wLinkGrabState.*?objectPushLinkAwayOnCollision.*?@state2:.*?ld a,\$90.*?wLinkGrabState2.*?ld \(\$d00a\),a.*?ld \(hl\),\$60.*?@state2func_67c9:.*?dec \(hl\).*?ld \(hl\),\$14.*?inc \(hl\).*?SND_MOVEBLOCK.*?updateLinkPositionGivenVelocity.*?@state3func_681a:.*?ld \(hl\),\$3c.*?inc \(hl\).*?SND_BOSS_DAMAGE.*?@state4func_689e:.*?SND_EXPLOSION.*?ld \(hl\),\$78'),
    @('ramrockGloveFormArm.s','(?ms)^state0func_6956:.*?and \$0f\s*cp \$02\s*ret z\s*call getFreePartSlot.*?^state3func_6970:.*?ld e,\$03.*?^state0func_6992:.*?push hl.*?ld hl,@table_69a3.*?pop hl\s*jp objectCopyPositionWithOffset'),
    @('ramrockSeedFormOrb.s','(?ms)^@state0:.*?ld a,\$28.*?^@state1:.*?cp \$0f.*?objectGetAngleTowardLink.*?ld a,\$50.*?^@state2:.*?ld \(hl\),\$0a.*?objectNudgeAngleTowards.*?objectApplySpeed.*?objectCheckWithinScreenBoundary'),
    @('ramrockSeedFormLaser.s','(?ms)^partCode34:.*?cp \$03.*?Object.xh.*?and \$03.*?xor \$01.*?@state0:.*?SPEED_2c0.*?ld a,\$07.*?ld \(hl\),\$03.*?@state1:.*?cp \$06.*?getFreePartSlot.*?@state2:.*?cp \$0e.*?@state3:.*?cp \$06.*?ld e,\$c2\s*ld \(de\),a.*?ld a,\$04')
)) {
    $path = Join-Path $Disassembly "object_code/ages/parts/$($spec[0])"
    $source = (Read-ImportText $path) -replace '(?m);[^\r\n]*',''
    if ($source -notmatch $spec[1]) { throw "${path}: unsupported Ramrock part control flow: $($spec[1])" }
}
foreach ($label in @('ramrockArm_subid0And1XPositions','ramrockArm_subid4And5XPositions','ramrockArm_subid4And5Angles')) {
    $values = @(Read-AssemblyLiteralValues $armPath $label)
    if ($values.Count -ne 2) { throw "${armPath}:$label requires two ordered hand values." }
    Add-RamrockTable $label $values "object_code/ages/enemies/ramrockArms.s:$label"
}
foreach ($mode in @(0x0c,0x0e,4,6)) {
    Add-RamrockTable "effects-$($mode.ToString('x2'))" $collisionValues[($mode*32)..($mode*32+31)] "data/ages/objectCollisionTable.s:mode`$$($mode.ToString('x2'))"
}
foreach ($id in @(5,7)) {
    $bits = ($active[$id].Operands -join '').Replace('%','')
    if ($bits -notmatch '^[01]{32}$') { throw "ENEMY`$$($id.ToString('x2')) requires its full collision mask." }
    Add-RamrockTable "active-$($id.ToString('x2'))" @($bits.ToCharArray() | ForEach-Object { [int]::Parse($_) }) "data/ages/enemyActiveCollisions.s:enemy`$$($id.ToString('x2'))"
}
Add-RamrockTable 'part-front-offsets' @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/parts/commonCode.s') 'partCommon_anglePositionOffsets') 'object_code/common/parts/commonCode.s:partCommon_anglePositionOffsets'
Write-GeneratedTable((Join-Path $destination 'enemies/ramrock.tsv'),$ramrockRows)
}
