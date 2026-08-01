# Wing Dungeon is dungeon index $02 in group $04. Its object stream mixes
# shared dungeon actors with native floor, cube, gate, reward, and boss
# handlers. Keep the native records in the same source order so the runtime can
# merge them with the already-imported shared dungeon stream.

$wingExpectedBlocks = [ordered]@{
    group4Map27ObjectData = @(
        'obj_Interaction $20 $01 $28 $28')
    group4Map28ObjectData = @(
        'obj_Interaction $20 $00 $98 $48')
    group4Map29ObjectData = @(
        'obj_Interaction $a1 $06 $58 $58',
        'obj_Interaction $a1 $07 $68 $98')
    group4Map2aObjectData = @(
        'obj_Interaction $a1 $08 $98 $58',
        'obj_Interaction $a1 $09 $48 $d8')
    group4Map2bObjectData = @(
        'obj_Interaction $20 $03 $58 $78',
        'obj_Interaction $a4 $00 $00 $00',
        'obj_Interaction $a4 $01 $00 $00',
        'obj_Interaction $a4 $02 $00 $00')
    group4Map2eObjectData = @(
        'obj_Interaction $21 $01 $48 $58',
        'obj_Interaction $15 $00')
    group4Map2fObjectData = @(
        'obj_Interaction $19 $01 $78 $68',
        'obj_Interaction $78 $02 $6c $13',
        'obj_Interaction $1b $04 $48 $50',
        'obj_Interaction $21 $03 $58 $68',
        'obj_Interaction $21 $08 $00 $10')
    group4Map30ObjectData = @(
        'obj_Interaction $12 $02 $58 $78')
    group4Map32ObjectData = @(
        'obj_Interaction $15 $00',
        'obj_Interaction $21 $02')
    group4Map34ObjectData = @(
        'obj_Interaction $20 $02 $58 $78')
    group4Map38ObjectData = @(
        'obj_Interaction $7f $00 $28 $78')
    group4Map39ObjectData = @(
        'obj_Interaction $12 $01 $58 $78')
    group4Map3bObjectData = @(
        'obj_Interaction $15 $00',
        'obj_Interaction $21 $07 $79 $20',
        'obj_Interaction $1b $25 $78 $b0')
    group4Map3eObjectData = @(
        'obj_Interaction $12 $02 $58 $88',
        'obj_Interaction $22 $00 $58 $78',
        'obj_Interaction $15 $00')
    group4Map42ObjectData = @(
        'obj_Interaction $15 $00',
        'obj_Interaction $21 $04 $28 $78',
        'obj_Interaction $21 $05 $58 $78',
        'obj_Interaction $1a $00 $0e $78')
    group4Map43ObjectData = @(
        'obj_Interaction $19 $04 $38 $98',
        'obj_Interaction $1a $00 $2e $28',
        'obj_Interaction $21 $03 $38 $58',
        'obj_Interaction $21 $06')
    group4Map48ObjectData = @(
        'obj_Interaction $12 $02 $58 $78')
}
foreach ($entry in $wingExpectedBlocks.GetEnumerator()) {
    $blockMatch = [regex]::Match(
        $mainObjectSource,
        '(?ms)^' + [regex]::Escape($entry.Key) +
            ':\s*(?<body>.*?)(?=^[A-Za-z_][A-Za-z0-9_]*:|\z)')
    if (-not $blockMatch.Success) {
        throw "Wing Dungeon object label $($entry.Key) is missing."
    }
    $body = $blockMatch.Groups['body'].Value
    $cursor = 0
    foreach ($expected in $entry.Value) {
        $next = $body.IndexOf($expected, $cursor, [StringComparison]::Ordinal)
        if ($next -lt 0) {
            throw "Wing Dungeon source record '$expected' is missing or out of order in $($entry.Key)."
        }
        $cursor = $next + $expected.Length
    }
}

$wingEnemySource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\enemyData.s')
foreach ($expected in @(
    'group4Map2bBeforeEventObjectData:',
    'obj_SpecificEnemyA $00 $79 $00 $56 $78',
    'group4Map34BeforeEventObjectData:',
    'obj_SpecificEnemyA $00 $71 $00 $58 $78')) {
    if (-not $wingEnemySource.Contains($expected)) {
        throw "Wing Dungeon before-event boss record '$expected' changed."
    }
}

# Dungeon $02 dispatches INTERAC_DUNGEON_SCRIPT $20:$03 to a specialized
# boss-death script. Unlike the shared boss reward, it restores the two bottom
# staircase cells over two createpuff boundaries and then places the Heart
# Container at $98,$78.
$wingDungeonScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\dungeonScripts.s')
$wingBossRewardScript = [regex]::Match(
    $wingDungeonScriptSource,
    '(?ms)^wingDungeonScript_bossDeath:\s*' +
    'jumpifroomflagset \$80, @spawnHeart\s+' +
    'checknoenemies\s+orroomflag \$80\s+' +
    '(?:;[^\r\n]*\r?\n\s*)*' +
    'setcoords \$(?<leftY>[0-9a-f]{2}), \$(?<leftX>[0-9a-f]{2})\s+' +
    'createpuff\s+settilehere \$(?<leftTile>[0-9a-f]{2})\s+' +
    'setcoords \$(?<rightY>[0-9a-f]{2}), \$(?<rightX>[0-9a-f]{2})\s+' +
    'createpuff\s+settilehere \$(?<rightTile>[0-9a-f]{2})\s+' +
    '@spawnHeart:\s+stopifitemflagset\s+' +
    'setcoords \$(?<rewardY>[0-9a-f]{2}), \$(?<rewardX>[0-9a-f]{2})\s+' +
    'scriptjump spawnHeartContainer')
if (-not $wingBossRewardScript.Success) {
    throw 'wingDungeonScript_bossDeath no longer matches its staircase/Heart Container sequence.'
}
function Get-WingBossRewardByte([string]$name) {
    return [Convert]::ToInt32(
        $wingBossRewardScript.Groups[$name].Value, 16)
}
$wingBossLeftY = Get-WingBossRewardByte 'leftY'
$wingBossLeftX = Get-WingBossRewardByte 'leftX'
$wingBossRightY = Get-WingBossRewardByte 'rightY'
$wingBossRightX = Get-WingBossRewardByte 'rightX'
$wingBossLeftTile = Get-WingBossRewardByte 'leftTile'
$wingBossRightTile = Get-WingBossRewardByte 'rightTile'
$wingBossRewardY = Get-WingBossRewardByte 'rewardY'
$wingBossRewardX = Get-WingBossRewardByte 'rewardX'
if (($wingBossLeftY -band 0x0f) -ne 0x08 -or
    ($wingBossLeftX -band 0x0f) -ne 0x08 -or
    ($wingBossRightY -band 0x0f) -ne 0x08 -or
    ($wingBossRightX -band 0x0f) -ne 0x08 -or
    $wingBossLeftTile -ne $wingBossRightTile) {
    throw 'wingDungeonScript_bossDeath staircase coordinates/tiles are inconsistent.'
}
$wingBossLeftPosition =
    ($wingBossLeftY -band 0xf0) -bor (($wingBossLeftX -shr 4) -band 0x0f)
$wingBossRightPosition =
    ($wingBossRightY -band 0xf0) -bor (($wingBossRightX -shr 4) -band 0x0f)

$wingRows = [Collections.Generic.List[string]]::new()
$wingRows.Add(
    '# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`tsource'.Replace(
        '`t', "`t"))
foreach ($row in @(
    '4	27	0	rupee-reward	20	01	28	28	item-clear	mainData.s:group4Map27ObjectData',
    '4	28	0	feather-reward	20	00	98	48	item-clear	mainData.s:group4Map28ObjectData',
    '4	29	0	side-platform	a1	06	58	58	always	mainData.s:group4Map29ObjectData',
    '4	29	1	side-platform	a1	07	68	98	always	mainData.s:group4Map29ObjectData',
    '4	2a	0	side-platform	a1	08	98	58	always	mainData.s:group4Map2aObjectData',
    '4	2a	1	side-platform	a1	09	48	d8	always	mainData.s:group4Map2aObjectData',
    '4	2b	0	boss-reward	20	03	58	78	item-clear	mainData.s:group4Map2bObjectData',
    '4	2b	1	circular-side-platform	a4	00	00	00	always	mainData.s:group4Map2bObjectData',
    '4	2b	2	circular-side-platform	a4	01	00	00	always	mainData.s:group4Map2bObjectData',
    '4	2b	3	circular-side-platform	a4	02	00	00	always	mainData.s:group4Map2bObjectData',
    '4	2b	4	head-thwomp	79	00	56	78	flag80-clear	enemyData.s:group4Map2bBeforeEventObjectData',
    '4	2e	0	floor-pattern-key	21	01	48	58	item-clear	mainData.s:group4Map2eObjectData',
    '4	2e	1	toggle-floor	15	00	00	00	always	mainData.s:group4Map2eObjectData',
    '4	2f	0	colored-cube	19	01	78	68	always	mainData.s:group4Map2fObjectData',
    '4	2f	1	switch-tile-toggler	78	02	6c	13	always	mainData.s:group4Map2fObjectData',
    '4	2f	2	minecart-gate	1b	04	48	50	always	mainData.s:group4Map2fObjectData',
    '4	2f	3	cube-light-sensor	21	03	58	68	always	mainData.s:group4Map2fObjectData',
    '4	2f	4	cube-switch-sensor	21	08	00	10	always	mainData.s:group4Map2fObjectData',
    '4	30	0	enemy-chest	12	02	58	78	item-clear	mainData.s:group4Map30ObjectData',
    '4	32	2	toggle-floor	15	00	00	00	always	mainData.s:group4Map32ObjectData',
    '4	32	3	red-floor-trigger	21	02	00	00	always	mainData.s:group4Map32ObjectData',
    '4	34	3	miniboss-reward	20	02	58	78	flag80-clear	mainData.s:group4Map34ObjectData',
    '4	34	5	swoop	71	00	58	78	flag80-clear	enemyData.s:group4Map34BeforeEventObjectData',
    '4	38	0	essence	7f	00	28	78	always	mainData.s:group4Map38ObjectData',
    '4	39	0	enemy-small-key	12	01	58	78	item-clear	mainData.s:group4Map39ObjectData',
    '4	3b	0	toggle-floor	15	00	00	00	always	mainData.s:group4Map3bObjectData',
    '4	3b	1	floor-switch-bit	21	07	79	20	always	mainData.s:group4Map3bObjectData',
    '4	3b	2	minecart-gate	1b	25	78	b0	always	mainData.s:group4Map3bObjectData',
    '4	3e	1	enemy-chest	12	02	58	88	item-clear	mainData.s:group4Map3eObjectData',
    '4	3e	2	floor-color-changer	22	00	58	78	always	mainData.s:group4Map3eObjectData',
    '4	3e	3	toggle-floor	15	00	00	00	always	mainData.s:group4Map3eObjectData',
    '4	42	0	toggle-floor	15	00	00	00	always	mainData.s:group4Map42ObjectData',
    '4	42	1	cube-color-source	21	04	28	78	always	mainData.s:group4Map42ObjectData',
    '4	42	2	colored-block-key	21	05	58	78	item-clear	mainData.s:group4Map42ObjectData',
    '4	42	3	cube-flame	1a	00	0e	78	always	mainData.s:group4Map42ObjectData',
    '4	43	1	colored-cube	19	04	38	98	always	mainData.s:group4Map43ObjectData',
    '4	43	2	cube-flame	1a	00	2e	28	always	mainData.s:group4Map43ObjectData',
    '4	43	3	cube-light-sensor	21	03	38	58	always	mainData.s:group4Map43ObjectData',
    '4	43	4	red-flame-trigger	21	06	00	00	always	mainData.s:group4Map43ObjectData',
    '4	48	0	enemy-chest	12	02	58	78	item-clear	mainData.s:group4Map48ObjectData'
)) {
    $wingRows.Add($row)
}
if ($wingRows.Count -ne 41) {
    throw "Wing Dungeon native object count changed: $($wingRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\wing_dungeon_objects.tsv'),
    $wingRows)

$wingBossRewardRows = @(
    '# group`troom`treward-y`treward-x`tstair-tile`tstair-positions`tsource'.Replace(
        '`t', "`t")
    "4`t2b`t$($wingBossRewardY.ToString('x2'))`t" +
        "$($wingBossRewardX.ToString('x2'))`t" +
        "$($wingBossLeftTile.ToString('x2'))`t" +
        "$($wingBossLeftPosition.ToString('x2'))," +
        "$($wingBossRightPosition.ToString('x2'))`t" +
        'scripts/ages/dungeonScripts.s:wingDungeonScript_bossDeath'
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\wing_dungeon_boss_reward.tsv'),
    $wingBossRewardRows)

$tileIndexSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
$wingConstants = [ordered]@{
    'red-floor' = 0x9d
    'yellow-floor' = 0x9e
    'blue-floor' = 0x9f
    'red-toggle-floor' = 0xad
    'yellow-toggle-floor' = 0xae
    'blue-toggle-floor' = 0xaf
    'red-pushable-block' = 0x2c
    'yellow-pushable-block' = 0x2d
    'blue-pushable-block' = 0x2e
    'somaria-block' = 0xda
    'chest' = 0xf1
    'toggle-center-min' = 4
    'toggle-center-max' = 12
    'enemy-chest-wait' = 30
    'falling-key-spawn-delay' = 40
    'track-tl' = 0x59
    'track-br' = 0x5a
    'track-bl' = 0x5b
    'track-tr' = 0x5c
    'track-horizontal' = 0x5d
    'track-vertical' = 0x5e
    'minecart-platform' = 0x5f
    'minecart-door-up' = 0x7c
    'minecart-speed' = 0x28
    'minecart-mount-push' = 4
}
$tileNames = [ordered]@{
    'red-floor' = 'TILEINDEX_RED_FLOOR'
    'yellow-floor' = 'TILEINDEX_YELLOW_FLOOR'
    'blue-floor' = 'TILEINDEX_BLUE_FLOOR'
    'red-toggle-floor' = 'TILEINDEX_RED_TOGGLE_FLOOR'
    'yellow-toggle-floor' = 'TILEINDEX_YELLOW_TOGGLE_FLOOR'
    'blue-toggle-floor' = 'TILEINDEX_BLUE_TOGGLE_FLOOR'
    'red-pushable-block' = 'TILEINDEX_RED_PUSHABLE_BLOCK'
    'yellow-pushable-block' = 'TILEINDEX_YELLOW_PUSHABLE_BLOCK'
    'blue-pushable-block' = 'TILEINDEX_BLUE_PUSHABLE_BLOCK'
    'somaria-block' = 'TILEINDEX_SOMARIA_BLOCK'
    'chest' = 'TILEINDEX_CHEST'
    'track-tl' = 'TILEINDEX_TRACK_TL'
    'track-br' = 'TILEINDEX_TRACK_BR'
    'track-bl' = 'TILEINDEX_TRACK_BL'
    'track-tr' = 'TILEINDEX_TRACK_TR'
    'track-horizontal' = 'TILEINDEX_TRACK_HORIZONTAL'
    'track-vertical' = 'TILEINDEX_TRACK_VERTICAL'
    'minecart-platform' = 'TILEINDEX_MINECART_PLATFORM'
    'minecart-door-up' = 'TILEINDEX_MINECART_DOOR_UP'
}
foreach ($entry in $tileNames.GetEnumerator()) {
    $expected = '(?m)^\.define\s+' + [regex]::Escape($entry.Value) +
        '\s+\$' + $wingConstants[$entry.Key].ToString('x2') + '\b'
    if ($tileIndexSource -notmatch $expected) {
        throw "Wing Dungeon tile constant $($entry.Value) changed."
    }
}

$switchSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\switchTileToggler.s')
$switchPairs = [regex]::Matches(
    $switchSource,
    '(?m)^\s*\.db\s+\$(?<off>[0-9a-f]{2})\s+\$(?<on>[0-9a-f]{2})\s*;\s*\$(?<index>[0-9a-f]{2})')
if ($switchPairs.Count -ne 24) {
    throw "Switch-tile replacement table changed: $($switchPairs.Count) rows."
}
$wingConstantRows = [Collections.Generic.List[string]]::new()
$wingConstantRows.Add("# key`tvalue")
foreach ($entry in $wingConstants.GetEnumerator()) {
    $wingConstantRows.Add("$($entry.Key)`t$($entry.Value)")
}
foreach ($pair in $switchPairs) {
    $index = [Convert]::ToInt32($pair.Groups['index'].Value, 16)
    $off = [Convert]::ToInt32($pair.Groups['off'].Value, 16)
    $on = [Convert]::ToInt32($pair.Groups['on'].Value, 16)
    $wingConstantRows.Add("switch-$index-off`t$off")
    $wingConstantRows.Add("switch-$index-on`t$on")
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\dungeon_interaction_constants.tsv'),
    $wingConstantRows)

$sidePlatformSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\movingSidescrollPlatform.s')
foreach ($expected in @(
    'movingSidescrollPlatformScript_subid06:',
    'ms_up    $38',
    'ms_down  $68',
    'movingSidescrollPlatformScript_subid07:',
    'ms_left  $88',
    'ms_right $a8',
    'movingSidescrollPlatformScript_subid08:',
    'ms_up    $58',
    'ms_down  $98',
    'movingSidescrollPlatformScript_subid09:',
    'ms_up    $48')) {
    if (-not $sidePlatformSource.Contains($expected)) {
        throw "Wing Dungeon side-platform command '$expected' changed."
    }
}
$platformRows = @(
    "# subid`tspeed`tdirection`tradius-y`tradius-x`tcommands"
    "06`t20`t4`t9`t7`tup:38,down:68"
    "07`t20`t4`t9`t7`tleft:88,right:a8"
    "08`t20`t4`t9`t7`tup:58,down:98"
    "09`t20`t4`t9`t7`tup:48,down:98"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\moving_side_scroll_platforms.tsv'),
    $platformRows)
foreach ($obsoleteDungeonAsset in @(
    'objects\wing_dungeon_constants.tsv',
    'objects\wing_dungeon_side_platforms.tsv'
)) {
    [IO.File]::Delete((Join-Path $destination $obsoleteDungeonAsset))
}

$staticDungeonSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\staticDungeonObjects.s')
$dungeon2Static = [regex]::Match(
    $staticDungeonSource,
    '(?ms)^dungeon2StaticObjects:\s*(?<body>.*?)(?=^dungeon[0-9a-f]+StaticObjects:|\z)')
if (-not $dungeon2Static.Success) {
    throw 'Wing Dungeon static-object list is missing.'
}
$minecartMatches = [regex]::Matches(
    $dungeon2Static.Groups['body'].Value,
    '(?m)^\s*\.db \$03,\s*\$(?<room>[0-9a-f]{2}),\s*INTERAC_MINECART,\s*\$00,\s*\$(?<y>[0-9a-f]{2}),\s*\$(?<x>[0-9a-f]{2})')
if ($minecartMatches.Count -ne 3) {
    throw "Wing Dungeon must retain three static minecarts; found $($minecartMatches.Count)."
}
$minecartRows = [Collections.Generic.List[string]]::new()
$minecartRows.Add("# slot`troom`ty`tx`tsource")
for ($slot = 0; $slot -lt $minecartMatches.Count; $slot++) {
    $match = $minecartMatches[$slot]
    $minecartRows.Add(
        "$slot`t$($match.Groups['room'].Value)`t$($match.Groups['y'].Value)`t$($match.Groups['x'].Value)`tstaticDungeonObjects.s:dungeon2StaticObjects")
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\wing_dungeon_minecarts.tsv'),
    $minecartRows)

$patternRows = [Collections.Generic.List[string]]::new()
$patternRows.Add("# kind`tcolor`tpositions")
$dungeonEventSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\dungeonEvents.s')
$patternColors = @{
    TILEINDEX_RED_TOGGLE_FLOOR = 'red'
    TILEINDEX_YELLOW_TOGGLE_FLOOR = 'yellow'
    TILEINDEX_BLUE_TOGGLE_FLOOR = 'blue'
    TILEINDEX_RED_PUSHABLE_BLOCK = 'red'
    TILEINDEX_YELLOW_PUSHABLE_BLOCK = 'yellow'
    TILEINDEX_BLUE_PUSHABLE_BLOCK = 'blue'
}
$patternTables = @(
    @{
        Kind = 'floor-pattern-key'
        ExpectedGroups = 2
        Pattern = (
            '(?ms)^subid01_tileData:\s*' +
            '(?<body>(?:[ \t]*\.db[^\r\n]*(?:\r?\n|$))+)')
    },
    @{
        Kind = 'colored-block-key'
        ExpectedGroups = 3
        Pattern = (
            '(?ms)^interaction21_subid05:.*?^@tileData:\s*' +
            '(?<body>(?:[ \t]*\.db[^\r\n]*(?:\r?\n|$))+)')
    }
)
foreach ($table in $patternTables) {
    $tableMatch = [regex]::Match($dungeonEventSource, $table.Pattern)
    if (-not $tableMatch.Success) {
        throw "Wing Dungeon $($table.Kind) tile pattern is missing."
    }
    $groups = [regex]::Matches(
        $tableMatch.Groups['body'].Value,
        '(?m)^\s*\.db\s+(?<tile>TILEINDEX_[A-Z0-9_]+)\s+(?<values>[^;\r\n]+)')
    if ($groups.Count -ne $table.ExpectedGroups) {
        throw "Wing Dungeon $($table.Kind) pattern has $($groups.Count) groups; expected $($table.ExpectedGroups)."
    }
    for ($group = 0; $group -lt $groups.Count; $group++) {
        $tileName = $groups[$group].Groups['tile'].Value
        if (-not $patternColors.ContainsKey($tileName)) {
            throw "Wing Dungeon $($table.Kind) pattern uses unknown tile $tileName."
        }
        $values = [regex]::Matches(
            $groups[$group].Groups['values'].Value,
            '\$(?<value>[0-9a-f]{2})')
        if ($values.Count -lt 2) {
            throw "Wing Dungeon $($table.Kind) pattern group $group has no positions."
        }
        $expectedTerminator = if ($group -eq $groups.Count - 1) { 0x00 } else { 0xff }
        $terminator = [Convert]::ToInt32(
            $values[$values.Count - 1].Groups['value'].Value, 16)
        if ($terminator -ne $expectedTerminator) {
            throw "Wing Dungeon $($table.Kind) pattern group $group has terminator `$$($terminator.ToString('x2')); expected `$$($expectedTerminator.ToString('x2'))."
        }
        $positions = [Collections.Generic.List[string]]::new()
        for ($position = 0; $position -lt $values.Count - 1; $position++) {
            $positions.Add(
                $values[$position].Groups['value'].Value.ToLowerInvariant())
        }
        $patternRows.Add(
            "$($table.Kind)`t$($patternColors[$tileName])`t$($positions -join ',')")
    }
}
if ($patternRows.Count -ne 6) {
    throw "Wing Dungeon pattern row count changed: $($patternRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\wing_dungeon_patterns.tsv'),
    $patternRows)

if (-not $allTexts.ContainsKey(0x000f) -or
    -not $allTexts.ContainsKey(0x2f00)) {
    throw 'Wing Dungeon must import Ancient Wood TX_000f and Swoop TX_2f00.'
}
$wingTextRows = @(
    "# text-id`tmessage-base64"
    "000f`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x000f])))"
    "2f00`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x2f00])))"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\wing_dungeon_text.tsv'),
    $wingTextRows)
