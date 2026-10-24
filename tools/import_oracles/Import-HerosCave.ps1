# Hero's Cave retains the same shared INTERACTION handlers as the dungeons.
$mainPath = Join-Path $Disassembly 'objects/ages/mainData.s'
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`tsource")
$kinds = @{
    '61:30'='lever'; '19:04'='colored-cube'; 'd8:05'='lever-lava-filler'
    '7e:01'='cave-portal'; '25:00'='tile-filler'; '21:11'='floor-fill-chest'
    '20:01'='trigger-chest'; '20:02'='signal-script'; '20:03'='tile-script'
    '90:14'='cave-puzzle'; '90:15'='cave-puzzle'; '90:16'='cave-puzzle'
    '90:17'='cave-puzzle'; '90:18'='cave-puzzle'; '90:20'='cave-puzzle'
    '90:1a'='tile-pattern-chest'
}
foreach ($room in 0xc0..0xcf) {
    $label = 'group4Map' + $room.ToString('x2') + 'ObjectData'
    $order = 0
    foreach ($node in Read-AssemblyMacroInvocations $mainPath $label) {
        if ($node.Name -eq 'obj_Interaction') {
            $args = @($node.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
            $key = '{0:x2}:{1:x2}' -f $args[0],$args[1]
            if ($kinds.ContainsKey($key)) {
                if ($args.Count -notin @(2,4)) { throw "${label}: unsupported $key construction operands." }
                $y = if ($args.Count -eq 4) { $args[2] } else { 0 }
                $x = if ($args.Count -eq 4) { $args[3] } else { 0 }
                $rows.Add("4`t$($room.ToString('x2'))`t$order`t$($kinds[$key])`t$($args[0].ToString('x2'))`t$($args[1].ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`talways`tobjects/ages/mainData.s:$label")
            }
        }
        if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer')) { $order++ }
    }
}
if ($rows.Count -ne 26) { throw 'Hero''s Cave requires 25 ordered native placements, including eight portals and three cube sensors.' }
Write-GeneratedTable((Join-Path $destination 'objects/heros_cave_objects.tsv'),$rows)

$portalPath = Join-Path $Disassembly 'object_code/common/interactions/minibossPortal.s'
$warps = @(Read-AssemblyDataDirectives $portalPath '@herosCaveWarps' '.db')
if ($warps.Count -ne 8) { throw 'Hero''s Cave portal table requires eight Ages destinations.' }
$portalRows = [Collections.Generic.List[string]]::new()
$portalRows.Add("# index`troom`tposition`tsource")
for ($i=0; $i -lt $warps.Count; $i++) {
    if ($warps[$i].Operands.Count -ne 2) { throw 'Hero''s Cave portal requires room/position pairs.' }
    $room = Convert-AssemblyInteger $warps[$i].Operands[0]
    $position = Convert-AssemblyInteger $warps[$i].Operands[1]
    $portalRows.Add("$i`t$($room.ToString('x2'))`t$($position.ToString('x2'))`tobject_code/common/interactions/minibossPortal.s:@herosCaveWarps")
}
Write-GeneratedTable((Join-Path $destination 'objects/heros_cave_portals.tsv'),$portalRows)

$puzzlePath = Join-Path $Disassembly 'object_code/ages/interactions/miscPuzzles.s'
$puzzles = (Read-ImportText $puzzlePath) -replace '(?m);[^\r\n]*',''
$sensor = [regex]::Match($puzzles,'(?ms)^miscPuzzles_subid14:(?<body>.*?)^miscPuzzles_subid15:').Groups['body'].Value
if ($sensor -notmatch '(?s)call checkInteractionState\s+jp z,miscPuzzles_deleteSelfOrIncStateIfRoomFlag7Set.*?sub TILEINDEX_RED_TOGGLE_FLOOR\s+cp \$03\s+ret nc.*?ld a,\(wRotatingCubePos\)\s+cp l\s+ret nz.*?ld a,\(wRotatingCubeColor\)\s+and \$03\s+cp b\s+ret nz.*?ld hl,wActiveTriggers\s+ld a,b\s+call setFlag\s+ld a,\$(?<tile>[0-9a-f]{2})\s+call setTile.*?ld a,\$0f\s+ld \(bc\),a\s+ld a,SND_CLINK\s+jp playSound') {
    throw 'miscPuzzles_subid14: cube color/position, trigger, tile/collision or sound contract changed.'
}
$filledFloor = Convert-AssemblyInteger ('$' + $Matches.tile)
if ($puzzles -notmatch '(?ms)^miscPuzzles_subid15:\s*call checkInteractionState\s*jp z,miscPuzzles_deleteSelfOrIncStateIfRoomFlag7Set\s*ld a,\(wActiveTriggers\)\s*cp \$07\s*ret nz\s*ld a,SND_SOLVEPUZZLE\s*call playSound\s*ld a,TILEINDEX_INDOOR_DOWNSTAIRCASE\s*ld c,\$15\s*call setTile\s*call getThisRoomFlags\s*set ROOMFLAG_BIT_80,\(hl\)\s*jp interactionDelete' -or
    $puzzles -notmatch '(?ms)^miscPuzzles_subid18:\s*call checkInteractionState\s*jp z,miscPuzzles_deleteSelfOrIncStateIfItemFlagSet\s*ld hl,wRoomLayout\+\$95\s*ld a,\(hl\)\s*cp TILEINDEX_PUSHABLE_STATUE\s*ret nz\s*ld l,\$5d\s*ld a,\(hl\)\s*cp TILEINDEX_PUSHABLE_STATUE\s*ret nz\s*jp miscPuzzles_dropSmallKeyHere' -or
    $puzzles -notmatch '(?ms)^miscPuzzles_subid17:.*?ld l,<ROOM_AGES_4c9\s*bit ROOMFLAG_BIT_ITEM,\(hl\)\s*pop hl\s*jr z,\+\s*set ROOMFLAG_BIT_ITEM,\(hl\).*?jp interactionDelete' -or
    $puzzles -notmatch '(?ms)^miscPuzzles_subid20:.*?and ROOMFLAG_ITEM.*?ld bc,TREASURE_OBJECT_RUPEES_16\s*call createTreasure\s*jp nz,@delete\s*call objectCopyPosition\s*@delete:\s*jp interactionDelete') {
    throw 'Hero''s Cave native sensor/stairs/statue/portal-flag/reward dispatch changed.'
}
$exit = @(Read-AssemblyMacroInvocations $puzzlePath '@warpDestData' 'm_HardcodedWarpA')
if ($exit.Count -ne 1 -or $exit[0].Operands.Count -ne 4 -or $exit[0].Operands[0] -notmatch '^ROOM_AGES_([0-7])([0-9a-f]{2})$') {
    throw 'miscPuzzles_subid16: expected one source-addressed hardcoded exit.'
}
$exitGroup = $Matches[1]; $exitRoom = $Matches[2]
$exitValues = @($exit[0].Operands | Select-Object -Skip 1 | ForEach-Object { (Convert-AssemblyInteger $_).ToString('x2') })
Write-GeneratedTable((Join-Path $destination 'objects/heros_cave_exit.tsv'),@(
    "# group`troom`tdestination-transition`tposition`tsource-transition`tsource",
    "$exitGroup`t$exitRoom`t$($exitValues[0])`t$($exitValues[1])`t$($exitValues[2])`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid16@warpDestData"
))
$tileConstants = @{}
foreach ($node in Read-AssemblyConstants (Join-Path $Disassembly 'constants/common/tileIndices.s')) {
    if ($node.OperandText -match '^\$[0-9a-f]+$') {
        $tileConstants[$node.Name] = Convert-AssemblyInteger $node.OperandText
    }
}
$patternRows = [Collections.Generic.List[string]]::new()
$patternRows.Add("# subid`torder`tposition`tminimum-tile`tmaximum-tile`tsource")
$order = 0; $terminated = $false
foreach ($row in Read-AssemblyDataDirectives $puzzlePath 'miscPuzzles_subid1a' '.db') {
    $args = @($row.Operands)
    if ($terminated -or $args.Count -lt 3 -or !$tileConstants.ContainsKey($args[0]) -or $args[-1] -notin @('$ff','$00')) {
        throw 'Hero''s Cave pushblock pattern has unsupported data.'
    }
    $tile = $tileConstants[$args[0]].ToString('x2')
    foreach ($operand in $args[1..($args.Count-2)]) {
        $position = (Convert-AssemblyInteger $operand).ToString('x2')
        $patternRows.Add("1a`t$order`t$position`t$tile`t$tile`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid1a@wantedTiles")
        $order++
    }
    $terminated = $args[-1] -eq '$00'
}
if (!$terminated -or $order -ne 7) { throw 'Hero''s Cave pushblock pattern requires seven cells and its final zero terminator.' }
Write-GeneratedTable((Join-Path $destination 'objects/heros_cave_chest_pattern.tsv'),$patternRows)

$scripts = (Read-ImportText (Join-Path $Disassembly 'scripts/ages/dungeonScripts.s')) -replace '(?m);[^\r\n]*',''
$helper = (Read-ImportText (Join-Path $Disassembly 'scripts/ages/scriptHelper.s')) -replace '(?m);[^\r\n]*',''
$dispatch = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/dungeonScript.s')
if ($dispatch -notmatch '(?s)@dungeon9:\s*@dungeona:\s*@dungeonb:\s*\.dw mainScripts.dungeonScript_spawnChestOnTriggerBit0\s*\.dw mainScripts.herosCaveScript_spawnChestWhen4TriggersActive\s*\.dw mainScripts.herosCaveScript_spawnBridgeWhenTriggerPressed\s*\.dw mainScripts.herosCaveScript_spawnNorthStairsWhenEnemiesKilled' -or
    $scripts -notmatch '(?ms)^herosCaveScript_spawnChestWhen4TriggersActive:\s*stopifitemflagset\s*checkmemoryeq wActiveTriggers, \$0f\s*scriptjump spawnChestAfterPuff' -or
    $scripts -notmatch '(?ms)^herosCaveScript_spawnBridgeWhenTriggerPressed:\s*stopifroomflag40set\s*checkflagset \$01, wActiveTriggers\s*asm15 scriptHelp.herosCave_spawnBridge_roomc9\s*scriptend' -or
    $scripts -notmatch '(?ms)^herosCaveScript_spawnNorthStairsWhenEnemiesKilled:\s*stopifitemflagset\s*checknoenemies\s*settilehere \$52\s*playsound SND_SOLVEPUZZLE\s*scriptend') {
    throw 'Hero''s Cave dungeon $09/$0a/$0b script dispatch or command boundaries changed.'
}
if ($scripts -notmatch '(?ms)^spawnChestAfterPuff:\s*playsound SND_SOLVEPUZZLE\s*createpuff\s*wait (?<wait>\d+)\s*settilehere TILEINDEX_CHEST\s*scriptend') { throw 'Shared chest script changed.' }
$chestWait = [int]$Matches.wait
if ($helper -notmatch '(?ms)^herosCave_spawnBridge_roomc9:\s*call getThisRoomFlags\s*set 6,\(hl\)\s*ld a,SND_SOLVEPUZZLE\s*call playSound\s*ld bc,\$(?<length>[0-9a-f]{2})(?<angle>[0-9a-f]{2})\s*ld e,\$(?<position>[0-9a-f]{2})\s*jp spawnBridge') { throw 'Hero''s Cave bridge flag/sound/allocation contract changed.' }
Write-GeneratedTable((Join-Path $destination 'objects/heros_cave_signal_scripts.tsv'),@(
    "# dungeon`tsubid`tkind`ttest-mask`toutput-mask`tposition`thalf-steps`tangle`twait`tsource",
    "0b`t02`ttrigger-bridge`t02`t40`t$($Matches.position)`t$($Matches.length)`t$($Matches.angle)`t0`tscripts/ages/dungeonScripts.s:herosCaveScript_spawnBridgeWhenTriggerPressed->scriptHelp.herosCave_spawnBridge_roomc9"
))
Write-GeneratedTable((Join-Path $destination 'objects/heros_cave_constants.tsv'),@(
    "# name`tvalue`tsource",
    "chest-wait`t$chestWait`tscripts/ages/dungeonScripts.s:spawnChestAfterPuff",
    "cube-first-floor`t$($tileConstants['TILEINDEX_RED_TOGGLE_FLOOR'])`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid14",
    "cube-filled-floor`t$filledFloor`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid14",
    "cube-stairs`t$($tileConstants['TILEINDEX_INDOOR_DOWNSTAIRCASE'])`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid15",
    "pushable-statue`t$($tileConstants['TILEINDEX_PUSHABLE_STATUE'])`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid18",
    "north-stairs`t82`tscripts/ages/dungeonScripts.s:herosCaveScript_spawnNorthStairsWhenEnemiesKilled"
))
