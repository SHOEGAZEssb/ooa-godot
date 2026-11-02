# Room 0:56 comedian trade. INTERAC_COMEDIAN is a script-owned NPC whose
# native wrapper initializes the script twice on its first update, then turns
# horizontally toward Link and animates after every later script update.
$comedianScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$comedianScriptSource = Read-ImportText $comedianScriptPath
$comedianOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'asm15', 'jumpifroomflagset', 'setanimation', 'scriptjump',
    'initcollisions', 'checkabutton', 'disableinput',
    'jumptable_objectbyte', 'showtextlowindex', 'wait',
    'jumpiftradeitemeq', 'jumpiftextoptioneq', 'giveitem', 'enableinput')) {
    [void]$comedianOpcodes.Add($opcode)
}
$comedianCommands = Read-AssemblyCutsceneCommands `
    $comedianScriptPath 'comedianScript' $comedianOpcodes
if ($comedianCommands.Count -ne 34) {
    throw "comedianScript expected 34 commands, parsed $($comedianCommands.Count)."
}

$comedianTargets = @{}
foreach ($command in $comedianCommands) {
    if (-not $comedianTargets.ContainsKey($command.Label)) {
        $comedianTargets[$command.Label] = $command.Index
    }
}
$expectedComedianTargets = @{
    '@hasMustache' = 5
    '@initNpc' = 7
    '@npcLoop' = 8
    '@beforeBeatD2' = 12
    '@afterBeatD2' = 14
    '@afterBeatMoonlitGrotto' = 16
    '@promptForTrade' = 20
    '@noTrade' = 23
    '@acceptedTrade' = 25
    '@alreadyGaveMustache' = 30
    '@enableInput' = 31
}
foreach ($entry in $expectedComedianTargets.GetEnumerator()) {
    if (-not $comedianTargets.ContainsKey($entry.Key) -or
        $comedianTargets[$entry.Key] -ne $entry.Value) {
        throw "comedianScript label $($entry.Key) moved from command $($entry.Value)."
    }
}
$comedianProgressTargets = @(
    Read-AssemblyDataDirectives `
        $comedianScriptPath 'comedianScript' '.dw' |
        ForEach-Object { $_.Operands[0] })
if (($comedianProgressTargets -join ',') -ne
    '@beforeBeatD2,@afterBeatD2,@afterBeatMoonlitGrotto') {
    throw 'comedianScript progress jump table changed.'
}

$comedianNativePath = Join-Path $Disassembly 'object_code\ages\interactions\comedian.s'
$comedianNativeSource = Read-ImportText $comedianNativePath
if ($comedianNativeSource -notmatch
        '(?ms)^@state0:.*?@loadScriptAndInitGraphics.*?interactionRunScript.*?interactionRunScript.*?interactionAnimateAsNpc' -or
    $comedianNativeSource -notmatch
        '(?ms)^@state1:.*?interactionRunScript.*?comedian_turnToFaceLink.*?interactionAnimateAsNpc' -or
    $comedianNativeSource -notmatch
        '(?ms)^@loadScriptAndInitGraphics:.*?interactionInitGraphics.*?objectMarkSolidPosition.*?>TX_0b00.*?mainScripts\.comedianScript') {
    throw 'INTERAC_COMEDIAN native initialization, facing, or update order changed.'
}
if ($comedianScriptSource -notmatch
        '(?ms)^comedian_checkGameProgress:.*?wEssencesObtained.*?getHighestSetBit.*?cp \$03.*?ld a,\$02.*?Interaction\.var3f' -or
    $comedianScriptSource -notmatch
        '(?ms)^comedian_enableMustache:.*?ld a,\$04.*?^comedian_disableMustache:.*?ld a,\$00.*?Interaction\.var37.*?Interaction\.var3e.*?\$ff' -or
    $comedianScriptSource -notmatch
        '(?ms)^comedian_turnToFaceLink:.*?w1Link\.xh.*?cp \(hl\).*?ld a,\$01.*?Interaction\.var37.*?interactionSetAnimation') {
    throw 'Comedian progress, moustache, or horizontal-facing helpers changed.'
}
if ($mainObjectSource -notmatch
    '(?ms)^group0Map56ObjectData:\s+obj_Interaction \$3a \$04 \$48 \$68\s+obj_Interaction \$65 \$00 \$48 \$78\s+obj_End') {
    throw 'Room 0:56 comedian/sidekick object order or coordinates changed.'
}

$comedianAnimations = @{
    0 = Resolve-NpcAnimation 0x65 0
    1 = Resolve-NpcAnimation 0x65 1
    4 = Resolve-NpcAnimation 0x65 4
    5 = Resolve-NpcAnimation 0x65 5
}
foreach ($animation in @(0, 1, 4, 5)) {
    if ([string]::IsNullOrWhiteSpace($comedianAnimations[$animation])) {
        throw "Could not resolve INTERAC_COMEDIAN animation `$$($animation.ToString('x2'))."
    }
}
foreach ($textId in 0x0b2c..0x0b32) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve comedian text TX_$($textId.ToString('x4'))."
    }
}
$comedianTreasure = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_07']
if ($null -eq $comedianTreasure -or
    $comedianTreasure.Treasure -ne 0x41 -or
    $comedianTreasure.SubId -ne 0x07 -or
    $comedianTreasure.Parameter -ne 0x07 -or
    $comedianTreasure.TextId -ne 0x0061 -or
    $comedianTreasure.Graphic -ne 0x77) {
    throw 'TREASURE_OBJECT_TRADEITEM_07 no longer grants the Funny Joke.'
}
$roomFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\roomFlags.s')
$tradeItemSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tradeitems.s')
if ($roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_CHEESY_MUSTACHE\s+db ; \$06' -or
    $tradeItemSource -notmatch 'TRADEITEM_FUNNY_JOKE\s+db ; \$07') {
    throw 'Comedian room flag or trade-item constants changed.'
}

$comedianBindings = @{
    'asm15|comedian_checkGameProgress' = @{ Opcode='native'; Payload='comedian_checkGameProgress' }
    'asm15|comedian_disableMustache' = @{ Opcode='native'; Payload='comedian_disableMustache' }
    'asm15|comedian_enableMustache' = @{ Opcode='native'; Payload='comedian_enableMustache' }
    'jumptable_objectbyte|Interaction.var3f' = @{ Opcode='jumptablememory'
        Payload="ComedianProgress|$($comedianProgressTargets.ForEach({ $comedianTargets[$_] }) -join ',')" }
}
$comedianTexts = @{}
foreach ($id in 0x0b2c..0x0b32) { $comedianTexts[$id] = $allTexts[$id] }
foreach ($c in $comedianCommands) {
    if ($c.Opcode -eq 'wait' -and $c.Operands -ne '30' -or
        $c.Opcode -eq 'giveitem' -and $c.Operands -ne 'TREASURE_TRADEITEM,$07' -or
        $c.Opcode -eq 'jumpiftextoptioneq' -and $c.Operands -notmatch '^\$00,\s*@\w+$') {
        throw "$($c.Path):$($c.Line): unexpected comedian operand '$($c.Operands)'."
    }
}
$comedianCommandRows = ConvertTo-CutsceneCommandRows $comedianCommands 'Comedian' `
    -symbols @{ ROOMFLAG_ITEM=0x20; TRADEITEM_CHEESY_MUSTACHE=0x06; TREASURE_TRADEITEM=0x41 } `
    -texts $comedianTexts -animations @{ 1=$comedianAnimations[1]; 5=$comedianAnimations[5] } -bindings $comedianBindings
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\comedian_commands.tsv'),
    $comedianCommandRows)

$comedianEventRows = @(
    "# group`troom`tid`tsubid`tanimation0`tanimation1`tanimation4`tanimation5`tcollision-y`tcollision-x`troom-flag`tprogress-binding`trequired-trade`treward-treasure`treward-parameter`treward-object`tinitial-script-updates"
    (@(
        '0', '56', '65', '00',
        $comedianAnimations[0], $comedianAnimations[1],
        $comedianAnimations[4], $comedianAnimations[5],
        '06', '06', '20', 'ComedianProgress', '06', '41', '07',
        'TREASURE_OBJECT_TRADEITEM_07', '2'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\comedian_event.tsv'),
    $comedianEventRows)

# Room 2:f3 depressed-boy trade. INTERAC_BOY $3c:$07 owns the
# interactionRunScript loop, while its native state handler faces Link except
# while var3d is nonzero. The accepted Funny Joke branch temporarily replaces
# Link with linkCutscene5 path $02, then drives the original animation-mode
# table before granting the Touching Book.
$depressedBoyScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$depressedBoyOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'checkmemoryeq', 'asm15',
    'checkpalettefadedone', 'checkabutton', 'disableinput',
    'jumpifroomflagset', 'jumpiftradeitemeq', 'showtext',
    'enableinput', 'scriptjump', 'wait', 'jumpiftextoptioneq',
    'writeobjectbyte', 'setmusic', 'jumpifmemoryset',
    'playsound', 'writememory', 'giveitem', 'resetmusic')) {
    [void]$depressedBoyOpcodes.Add($opcode)
}
$depressedBoyCommands = Read-AssemblyCutsceneCommands `
    $depressedBoyScriptPath 'boySubid07Script' $depressedBoyOpcodes
if ($depressedBoyCommands.Count -ne 44) {
    throw "boySubid07Script expected 44 commands, parsed $($depressedBoyCommands.Count)."
}

$depressedBoyTargets = @{}
foreach ($command in $depressedBoyCommands) {
    if (-not $depressedBoyTargets.ContainsKey($command.Label)) {
        $depressedBoyTargets[$command.Label] = $command.Index
    }
}
$expectedDepressedBoyTargets = @{
    '@npcLoop' = 4
    '@showDepressedText' = 8
    '@offerTrade' = 11
    '@acceptedTrade' = 15
    '@funnyJokeCutsceneLoop' = 24
    '@doneFunnyJokeCutscene' = 27
    '@alreadyToldJoke' = 41
}
foreach ($entry in $expectedDepressedBoyTargets.GetEnumerator()) {
    if (-not $depressedBoyTargets.ContainsKey($entry.Key) -or
        $depressedBoyTargets[$entry.Key] -ne $entry.Value) {
        throw "boySubid07Script label $($entry.Key) moved from command $($entry.Value)."
    }
}

$depressedBoyNativePath = Join-Path $Disassembly `
    'object_code\ages\interactions\boy.s'
$depressedBoyNativeSource = Read-ImportText $depressedBoyNativePath
$linkCutscenePath = Join-Path $Disassembly `
    'object_code\ages\specialObjects\linkInCutscene.s'
$linkCutsceneSource = Read-ImportText $linkCutscenePath
$paletteSource = Read-ImportText (Join-Path $Disassembly 'code\bank0.s')
$musicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
if ($depressedBoyNativeSource -notmatch
        '(?ms)^@initSubid07:\s+ld h,d\s+ld l,Interaction\.var3f\s+inc \(hl\)\s+call boyLoadScript\s+jr boyState1' -or
    $depressedBoyNativeSource -notmatch
        '(?ms)^boyRunSubid07:\s+call interactionRunScript\s+ld e,Interaction\.var3d.*?jp z,npcFaceLinkAndAnimate\s+call interactionAnimate\s+jp objectSetPriorityRelativeToLink_withTerrainEffects' -or
    $mainObjectSource -notmatch
        '(?ms)^group2Mapf3ObjectData:\s+obj_Interaction \$3c \$07 \$28 \$50\s+obj_End' -or
    $linkCutsceneSource -notmatch
        '(?ms)^linkCutscene5:.*?SPEED_100.*?linkCutscene_updateAngleOnPath.*?SPECIALOBJECT_LINK.*?^@@path2:.*?\.db \$00 \$48\s+\.db \$ff' -or
    $paletteSource -notmatch
        '(?ms)^darkenRoomLightly:\s+ld b,\$f7\s+jr _darkenRoomHelper.*?^_darkenRoomHelper:.*?ld a,\$05.*?ld a,\$01' -or
    $musicSource -notmatch 'MUS_CRAZY_DANCE\s+db ; \$31' -or
    $musicSource -notmatch 'SND_SWORD_OBTAINED\s+db ; \$ab') {
    throw 'Room 2:f3 depressed-boy native, path, palette, placement, or sound contract changed.'
}

$depressedBoySource = Read-ImportText $depressedBoyScriptPath
$depressedBoyAnimationMatch = [regex]::Match(
    $depressedBoySource,
    '(?ms)^boy_runFunnyJokeCutscene:.*?^@animations:\s*(?<body>.*?)^boySubid07Script:')
if (-not $depressedBoyAnimationMatch.Success) {
    throw 'Could not isolate boy_runFunnyJokeCutscene animation metadata.'
}
$depressedBoyAnimationRows = @([regex]::Matches(
    $depressedBoyAnimationMatch.Groups['body'].Value,
    '(?m)^\s*\.db \$(?<mode>[0-9a-f]{2}) \$(?<duration>[0-9a-f]{2})\s*$'))
$expectedDepressedBoyAnimations = @(
    '08:14', '09:14', '08:14', '09:14', '07:14', '0e:14', '06:14',
    '1c:14', '08:14', '09:14', '08:14', '08:28', '09:32', '07:14',
    '0e:14', '06:14', '1c:14', '08:14', '09:14', '08:14', '09:14')
if ($depressedBoyAnimationRows.Count -ne $expectedDepressedBoyAnimations.Count) {
    throw "Funny Joke dance expected 21 source pairs, parsed $($depressedBoyAnimationRows.Count)."
}
for ($index = 0; $index -lt $expectedDepressedBoyAnimations.Count; $index++) {
    $actual = $depressedBoyAnimationRows[$index].Groups['mode'].Value + ':' +
        $depressedBoyAnimationRows[$index].Groups['duration'].Value
    if ($actual -ne $expectedDepressedBoyAnimations[$index]) {
        throw "Funny Joke animation pair $index changed from $($expectedDepressedBoyAnimations[$index])."
    }
}
$depressedBoyAnimations = $expectedDepressedBoyAnimations -join ','

foreach ($textId in @(0x2515, 0x2516, 0x2517, 0x2518)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve depressed-boy text TX_$($textId.ToString('x4'))."
    }
}
$depressedBoyTreasure = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_08']
if ($null -eq $depressedBoyTreasure -or
    $depressedBoyTreasure.Treasure -ne 0x41 -or
    $depressedBoyTreasure.SubId -ne 0x08 -or
    $depressedBoyTreasure.Parameter -ne 0x08 -or
    $depressedBoyTreasure.TextId -ne 0x0062 -or
    $depressedBoyTreasure.Graphic -ne 0x78) {
    throw 'TREASURE_OBJECT_TRADEITEM_08 no longer grants the Touching Book.'
}
if ($roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_FUNNY_JOKE\s+db ; \$07' -or
    $tradeItemSource -notmatch 'TRADEITEM_TOUCHING_BOOK\s+db ; \$08') {
    throw 'Depressed-boy room flag or trade-item constants changed.'
}

$depressedBoyBindings = @{
    'asm15|darkenRoomLightly' = @{ Opcode='native'; Payload='DarkenRoomLightly' }
    'asm15|moveLinkToPosition, $02' = @{ Opcode='native'; Payload='MoveLinkToFunnyJokePosition' }
    'asm15|setLinkToState08AndSetDirection, DIR_DOWN' = @{ Opcode='native'; Payload='SetLinkNormalDown' }
    'asm15|boy_runFunnyJokeCutscene' = @{ Opcode='native'; Payload='AdvanceFunnyJokeDance' }
    'asm15|restartSound' = @{ Opcode='native'; Payload='RestartSound' }
    'asm15|setLinkToState08AndSetDirection, DIR_UP' = @{ Opcode='native'; Payload='SetLinkNormalUp' }
    'checkpalettefadedone|' = @{ Opcode='gate'; Payload='PaletteFade' }
    'setmusic|MUS_CRAZY_DANCE' = @{ Opcode='setmusic'; Arg0='31' }
    'playsound|SND_SWORD_OBTAINED' = @{ Opcode='playsound'; Arg0='ab' }
    'writememory|wcc50, LINK_ANIM_MODE_GETITEM2HAND' = @{ Opcode='native'; Payload='SetLinkGetItemTwoHand' }
    'resetmusic|' = @{ Opcode='native'; Payload='ResetMusic' }
    'jumpifmemoryset|wcddb, $80, @doneFunnyJokeCutscene' = @{
        Opcode='jumpifmemoryeqyieldonmiss'; Arg0='01'
        Arg1=[string]$depressedBoyTargets['@doneFunnyJokeCutscene']; Payload='DanceComplete' }
}
$depressedBoyTexts = @{}
foreach ($id in 0x2515..0x2518) { $depressedBoyTexts[$id] = Resolve-OceanText $id ([Collections.Generic.HashSet[int]]::new()) }
foreach ($c in $depressedBoyCommands) {
    if ($c.Opcode -eq 'wait' -and $c.Operands -notin @('1','30','40','120') -or
        $c.Opcode -eq 'writeobjectbyte' -and $c.Operands -notmatch '^Interaction\.var3d,\s*\$0[01]$' -or
        $c.Opcode -eq 'giveitem' -and $c.Operands -notmatch '^TREASURE_TRADEITEM,\s*\$08$' -or
        $c.Opcode -eq 'jumpifroomflagset' -and $c.Operands -notmatch '^\$20,\s*@\w+$' -or
        $c.Opcode -eq 'jumpiftextoptioneq' -and $c.Operands -notmatch '^\$00,\s*@\w+$' -or
        $c.Opcode -eq 'writememory' -and $c.Operands -ne 'wcc50, LINK_ANIM_MODE_GETITEM2HAND' -or
        $c.Opcode -eq 'checkmemoryeq' -and $c.Operands -notin @('wMenuDisabled, $00','w1Link.id, SPECIALOBJECT_LINK')) {
        throw "$($c.Path):$($c.Line): unexpected depressed-boy operand '$($c.Operands)'."
    }
}
$depressedBoyCommandRows = ConvertTo-CutsceneCommandRows $depressedBoyCommands 'DepressedBoy' `
    -symbols @{ TRADEITEM_FUNNY_JOKE=0x07; TREASURE_TRADEITEM=0x41; 'Interaction.var3d'=0x3d
        wMenuDisabled='MenuDisabled'; 'w1Link.id'='LinkObjectId'; SPECIALOBJECT_LINK=0 } `
    -texts $depressedBoyTexts -bindings $depressedBoyBindings
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\depressed_boy_commands.tsv'),
    $depressedBoyCommandRows)

$depressedBoyEventRows = @(
    "# group`troom`tid`tsubid`tcollision-y`tcollision-x`troom-flag`trequired-trade`treward-treasure`treward-parameter`treward-object`tdarken-target`tapproach-y`tdance-count`tdance-animations`tdance-music`treward-sound`tsource-animation-count`tinitial-script-updates"
    (@(
        '2', 'f3', '3c', '07', '06', '06', '20', '07', '41', '08',
        'TREASURE_OBJECT_TRADEITEM_08', '-9', '48', '20',
        $depressedBoyAnimations, '31', 'ab', '21', '1'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\depressed_boy_event.tsv'),
    $depressedBoyEventRows)
