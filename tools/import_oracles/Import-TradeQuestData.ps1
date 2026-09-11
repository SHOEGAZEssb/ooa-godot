# Room 2:e6 Mask Salesman trade. INTERAC_MASK_SALESMAN is a script-owned NPC
# whose native wrapper runs the script once on its initialization update,
# enables always-update behavior, then animates after every later script update.
$maskSalesmanScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$maskSalesmanOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'setcollisionradii', 'makeabuttonsensitive', 'checkabutton',
    'disableinput', 'jumpifroomflagset', 'setanimation', 'showtext',
    'wait', 'jumpiftradeitemeq', 'scriptjump', 'jumpiftextoptioneq',
    'giveitem', 'enableinput')) {
    [void]$maskSalesmanOpcodes.Add($opcode)
}
$maskSalesmanCommands = Read-AssemblyCutsceneCommands `
    $maskSalesmanScriptPath 'maskSalesmanScript' $maskSalesmanOpcodes
if ($maskSalesmanCommands.Count -ne 44) {
    throw "maskSalesmanScript expected 44 commands, parsed $($maskSalesmanCommands.Count)."
}

$maskSalesmanTargets = @{}
foreach ($command in $maskSalesmanCommands) {
    if (-not $maskSalesmanTargets.ContainsKey($command.Label)) {
        $maskSalesmanTargets[$command.Label] = $command.Index
    }
}
$expectedMaskSalesmanTargets = @{
    '@npcLoop' = 2
    '@promptForTrade' = 19
    '@acceptedTrade' = 24
    '@alreadyGaveDoggieMask' = 41
    '@enableInput' = 42
}
foreach ($entry in $expectedMaskSalesmanTargets.GetEnumerator()) {
    if (-not $maskSalesmanTargets.ContainsKey($entry.Key) -or
        $maskSalesmanTargets[$entry.Key] -ne $entry.Value) {
        throw "maskSalesmanScript label $($entry.Key) moved from command $($entry.Value)."
    }
}

$maskSalesmanNativePath = Join-Path $Disassembly `
    'object_code\ages\interactions\maskSalesman.s'
$maskSalesmanNativeSource = Read-ImportText $maskSalesmanNativePath
$maskSalesmanWrapperPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$maskSalesmanWrapperSource = Read-ImportText $maskSalesmanWrapperPath
$maskSalesmanInteractionDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\interactionData.s')
if ($maskSalesmanNativeSource -notmatch
        '(?ms)^@state0:\s+call @loadScriptAndInitGraphics\s+call interactionSetAlwaysUpdateBit\s+^@state1:\s+call interactionRunScript\s+jp c,interactionDelete\s+jp interactionAnimateAsNpc' -or
    $maskSalesmanNativeSource -notmatch
        '(?ms)^@loadScriptAndInitGraphics:\s+call interactionInitGraphics.*?interactionSetScript\s+jp interactionIncState.*?^@scriptTable:\s+\.dw mainScripts\.maskSalesmanScript' -or
    $maskSalesmanWrapperSource -notmatch
        '(?ms)^maskSalesmanScript:\s+loadscript scriptHelp\.maskSalesmanScript' -or
    $maskSalesmanInteractionDataSource -notmatch
        '(?m)^\s*/\* \$5c \*/ m_InteractionData \$5e \$00 \$00\s*$') {
    throw 'INTERAC_MASK_SALESMAN native initialization, update order, or script wrapper changed.'
}
if ($mainObjectSource -notmatch
    '(?ms)^group2Mape6ObjectData:\s+obj_Interaction \$5c \$00 \$38 \$70\s+obj_End') {
    throw 'Room 2:e6 Mask Salesman object stream or coordinates changed.'
}

$maskSalesmanAnimations = @{
    0 = Resolve-NpcAnimation 0x5c 0
    1 = Resolve-NpcAnimation 0x5c 1
}
foreach ($animation in @(0, 1)) {
    if ([string]::IsNullOrWhiteSpace($maskSalesmanAnimations[$animation])) {
        throw "Could not resolve INTERAC_MASK_SALESMAN animation `$$($animation.ToString('x2'))."
    }
}
$maskSalesmanTextIds = @(0x0b0d..0x0b15) + 0x0b45
foreach ($textId in $maskSalesmanTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Mask Salesman text TX_$($textId.ToString('x4'))."
    }
}
$maskSalesmanTreasure = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_04']
if ($null -eq $maskSalesmanTreasure -or
    $maskSalesmanTreasure.Treasure -ne 0x41 -or
    $maskSalesmanTreasure.SubId -ne 0x04 -or
    $maskSalesmanTreasure.Parameter -ne 0x04 -or
    $maskSalesmanTreasure.TextId -ne 0x005e -or
    $maskSalesmanTreasure.Graphic -ne 0x74) {
    throw 'TREASURE_OBJECT_TRADEITEM_04 no longer grants the Doggie Mask.'
}
if ($roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_TASTY_MEAT\s+db ; \$03' -or
    $tradeItemSource -notmatch 'TRADEITEM_DOGGIE_MASK\s+db ; \$04') {
    throw 'Mask Salesman room flag or trade-item constants changed.'
}

$maskSalesmanCommandRows = ConvertTo-CutsceneCommandRows `
    $maskSalesmanCommands 'MaskSalesman' `
    -symbols @{ ROOMFLAG_ITEM = 0x20; TRADEITEM_TASTY_MEAT = 0x03; TREASURE_TRADEITEM = 0x41 } `
    -texts $allTexts -animations $maskSalesmanAnimations -positions $allTextPositions
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\mask_salesman_commands.tsv'),
    $maskSalesmanCommandRows)

$maskSalesmanEventRows = @(
    "# group`troom`tid`tsubid`tanimation0`tanimation1`tinitial-animation`tcollision-y`tcollision-x`troom-flag`trequired-trade`treward-treasure`treward-parameter`treward-object`tinitial-script-updates`talways-update"
    (@(
        '2', 'e6', '5c', '00',
        $maskSalesmanAnimations[0], $maskSalesmanAnimations[1],
        '00', '04', '06', '20', '03', '41', '04',
        'TREASURE_OBJECT_TRADEITEM_04', '1', '1'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\mask_salesman_event.tsv'),
    $maskSalesmanEventRows)

# INTERAC_DUMBBELL_MAN $51:$00: complete copied script and native wrapper.
$dumbbellManPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$dumbbellManOpcodes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @('jumpifroomflagset', 'setanimation', 'scriptjump', 'initcollisions', 'checkabutton', 'disableinput', 'showtextlowindex', 'wait', 'jumpiftradeitemeq', 'enableinput', 'jumpiftextoptioneq', 'giveitem')) {
    [void]$dumbbellManOpcodes.Add($opcode)
}
$dumbbellManCommands = Read-AssemblyCutsceneCommands $dumbbellManPath 'dumbbellManScript' $dumbbellManOpcodes
$expectedDumbbellManCommands = @(
    @('jumpifroomflagset', '$20, @liftingAnimation'),
    @('setanimation', '$00'),
    @('scriptjump', '++'),
    @('setanimation', '$01'),
    @('initcollisions', ''),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('jumpifroomflagset', '$20, @alreadyGaveMustache'),
    @('showtextlowindex', '<TX_0b1d'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b20'),
    @('wait', '30'),
    @('jumpiftradeitemeq', 'TRADEITEM_DUMBBELL, @offerTrade'),
    @('enableinput', ''),
    @('scriptjump', '@npcLoop'),
    @('showtextlowindex', '<TX_0b1e'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b20'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b1f'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b20'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b20'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b21'),
    @('wait', '30'),
    @('jumpiftextoptioneq', '$00, @giveMustache'),
    @('showtextlowindex', '<TX_0b20'),
    @('enableinput', ''),
    @('scriptjump', '@npcLoop'),
    @('showtextlowindex', '<TX_0b22'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b20'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b23'),
    @('wait', '30'),
    @('setanimation', '$01'),
    @('giveitem', 'TREASURE_TRADEITEM, $06'),
    @('showtextlowindex', '<TX_0b24'),
    @('wait', '30'),
    @('enableinput', ''),
    @('scriptjump', '@npcLoop')
)
if ($dumbbellManCommands.Count -ne $expectedDumbbellManCommands.Count) {
    throw "dumbbellManScript expected 43 commands, got $($dumbbellManCommands.Count)."
}
for ($i = 0; $i -lt $dumbbellManCommands.Count; $i++) {
    $command = $dumbbellManCommands[$i]
    if ($command.Opcode -ne $expectedDumbbellManCommands[$i][0] -or
        $command.Operands -ne $expectedDumbbellManCommands[$i][1]) {
        throw "${dumbbellManPath}:$($command.Line): unexpected dumbbellManScript command $i '$($command.Opcode) $($command.Operands)'."
    }
}
$dumbbellManTargets = @{}
foreach ($command in $dumbbellManCommands) {
    if (-not $dumbbellManTargets.ContainsKey($command.Label)) {
        $dumbbellManTargets[$command.Label] = $command.Index
    }
}
$expectedDumbbellManTargets = @{
    '@liftingAnimation' = 3
    '++' = 4
    '@npcLoop' = 5
    '@offerTrade' = 15
    '@giveMustache' = 31
    '@alreadyGaveMustache' = 39
}
foreach ($entry in $expectedDumbbellManTargets.GetEnumerator()) {
    if (-not $dumbbellManTargets.ContainsKey($entry.Key) -or
        $dumbbellManTargets[$entry.Key] -ne $entry.Value) {
        throw "dumbbellManScript label $($entry.Key) moved from command $($entry.Value)."
    }
}
$dumbbellManNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\dumbellMan.s')
if ($dumbbellManNative -notmatch
        '(?ms)^@state0:\s+call @initialize\s+call interactionSetAlwaysUpdateBit\s+@state1:\s+call interactionRunScript\s+jp c,interactionDelete\s+jp interactionAnimateAsNpc' -or
    $dumbbellManNative -notmatch
        '(?ms)^@initialize:\s+call interactionInitGraphics\s+ld a,>TX_0b00\s+call interactionSetHighTextIndex.*?call interactionSetScript\s+jp interactionIncState\s+@scriptTable:\s+\.dw mainScripts\.dumbbellManScript' -or
    $maskSalesmanWrapperSource -notmatch
        '(?ms)^dumbbellManScript:\s+loadscript scriptHelp\.dumbbellManScript' -or
    $maskSalesmanInteractionDataSource -notmatch
        '(?m)^\s*/\* \$51 \*/ m_InteractionData \$40 \$00 \$00\s*$' -or
    $mainObjectSource -notmatch
        '(?ms)^group2Mape8ObjectData:\s+obj_Interaction \$51 \$00 \$18 \$50\s+obj_End') {
    throw 'INTERAC_DUMBBELL_MAN $51:$00 native wrapper, graphics, or room 2:e8 placement changed.'
}
$dumbbellManAnimations = @{ 0 = Resolve-NpcAnimation 0x51 0; 1 = Resolve-NpcAnimation 0x51 1 }
foreach ($animation in @(0, 1)) {
    if ([string]::IsNullOrWhiteSpace($dumbbellManAnimations[$animation])) {
        throw "INTERAC_DUMBBELL_MAN missing animation $animation."
    }
}
$dumbbellManTreasure = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_06']
if ($null -eq $dumbbellManTreasure -or
    $dumbbellManTreasure.Treasure -ne 0x41 -or $dumbbellManTreasure.SubId -ne 0x06 -or
    $dumbbellManTreasure.Parameter -ne 0x06 -or $dumbbellManTreasure.TextId -ne 0x0060 -or
    $dumbbellManTreasure.Graphic -ne 0x76 -or
    $roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_DUMBBELL\s+db ; \$05' -or
    $tradeItemSource -notmatch 'TRADEITEM_CHEESY_MUSTACHE\s+db ; \$06') {
    throw 'Dumbbell Man trade $05 -> $06, reward, or room flag $20 changed.'
}
$dumbbellManRows = [Collections.Generic.List[string]]::new()
$dumbbellManRows.Add("# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
foreach ($command in $dumbbellManCommands) {
    $opcode = $command.Opcode
    $actor = ''; $arg0 = ''; $arg1 = ''; $payload = ''
    $parts = @($command.Operands -split ',\s*')
    switch ($opcode) {
        'initcollisions' { $actor = 'DumbbellMan' }
        'checkabutton' { $actor = 'DumbbellMan' }
        'jumpifroomflagset' { $arg0 = '20'; $arg1 = $dumbbellManTargets[$parts[1]].ToString() }
        'setanimation' {
            $actor = 'DumbbellMan'; $arg0 = $parts[0].Substring(1)
            $payload = $dumbbellManAnimations[[Convert]::ToInt32($arg0, 16)]
        }
        'scriptjump' {
            # loadscript copies this helper into wBigBuffer; local jumps
            # return carry-clear from scriptCmd_jump and yield one update.
            $opcode = 'scriptjumpyield'; $arg0 = $dumbbellManTargets[$parts[0]].ToString()
        }
        'showtextlowindex' {
            $opcode = 'showtext'; $arg0 = $parts[0].Substring(4)
            $textId = [Convert]::ToInt32($arg0, 16)
            if (-not $allTexts.ContainsKey($textId)) { throw "Missing Dumbbell Man TX_$arg0." }
            $payload = $allTexts[$textId]
        }
        'wait' { $arg0 = '30' }
        'jumpiftradeitemeq' { $arg0 = '05'; $arg1 = $dumbbellManTargets[$parts[1]].ToString() }
        'jumpiftextoptioneq' { $arg0 = '00'; $arg1 = $dumbbellManTargets[$parts[1]].ToString() }
        'giveitem' { $arg0 = '41'; $arg1 = '06' }
    }
    $dumbbellManRows.Add((New-CutsceneCommandRow $command.Script $command.Index $command.Label $command.Line $opcode $actor "$arg0" "$arg1" $payload))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\dumbbell_man_commands.tsv'),
    $dumbbellManRows)
$dumbbellManEventRows = @(
    "# group`troom`tid`tsubid`tanimation0`tanimation1`tinitial-animation`tcollision-y`tcollision-x`troom-flag`trequired-trade`treward-treasure`treward-parameter`treward-object`tinitial-script-updates`talways-update"
    (@('2', 'e8', '51', '00', $dumbbellManAnimations[0], $dumbbellManAnimations[1],
        '00', '06', '06', '20', '05', '41', '06', 'TREASURE_OBJECT_TRADEITEM_06', '1', '1') -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\dumbbell_man_event.tsv'),
    $dumbbellManEventRows)

# Tokkey $9d:$00: retain the ROM routines and copied helper as one indexed
# stream. Calls/returns still yield; genericNpcScript is expanded explicitly.
$tokkeyTexts = @{}
foreach ($textId in 0x2c00..0x2c05) {
    if (!$allTexts.ContainsKey($textId) -or
        ($textId -ne 0x2c03 -and $allTextPositions[$textId] -ne 2)) {
        throw "Tokkey TX_$($textId.ToString('x4')) is missing its text or source position."
    }
    $tokkeyTexts[$textId] = $allTexts[$textId]
}
if ($allTextFallthroughIds[0x2c00] -ne 0x2c01 -or
    !$tokkeyTexts[0x2c00].EndsWith('\n', [StringComparison]::Ordinal)) {
    throw 'Tokkey TX_2c00 must fall through its trailing newline into TX_2c01.'
}
# The ROM has no terminator between the introduction and the Echoes hint.
# Materialize the trailing newline before concatenating the successor.
$tokkeyTexts[0x2c00] = $tokkeyTexts[0x2c00].Substring(0, $tokkeyTexts[0x2c00].Length - 2) +
    "`n" + $tokkeyTexts[0x2c01]
$tokkeyNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\tokkey.s')
$tokkeyHelper = Read-ImportText (Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
if ($mainObjectSource -notmatch '(?ms)^group3Map8fObjectData:\s+obj_Interaction \$9d \$00 \$18 \$28\s+obj_End' -or
    $tokkeyNative -notmatch '(?s)bit 0,a.*?wLinkPlayingInstrument.*?cp \$01.*?checkLinkCollisionsEnabled.*?wActiveTilePos.*?cp \$32.*?TX_2c05.*?ld a,60.*?ld bc,\$f810.*?tokkeyScript_justHeardTune' -or
    $tokkeyNative -notmatch '(?s)@state2:\s+call @checkCreateMusicNote\s+call interactionAnimate\s+@state4:\s+call interactionRunScript\s+ld c,\$20' -or
    $tokkeyNative -notmatch '(?s)@state3:.*?call interactionAnimate\s+call interactionAnimate\s+ld c,\$60.*?ld bc,-\$200' -or
    $tokkeyNative -notmatch '(?s)@checkCreateMusicNote:.*?bit 1,a.*?wFrameCounter.*?and \$0f.*?getRandomNumber.*?and \$01.*?ld bc,\$f808' -or
    $tokkeyHelper -notmatch '(?s)tokkey_jump:\s+ld bc,-\$1a0.*?tokkey_centerLinkOnTile:.*?centerCoordinatesOnTile.*?ld \(hl\),\$01.*?tokkey_makeLinkPlayTuneOfCurrents:.*?INTERAC_PLAY_HARP_SONG\s+inc l\s+inc \(hl\)' -or
    $musicSource -notmatch 'MUS_CRAZY_DANCE\s+db ; \$31' -or
    $musicSource -notmatch 'SND_BIG_EXPLOSION\s+db ; \$79') {
    throw 'Room 3:8f INTERAC_TOKKEY native state, harp gate, motion, or music contract changed.'
}
$tokkeyAllowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($op in @('initcollisions','setcollisionradii','jumpifroomflagset','checkabutton',
    'setdisabledobjectsto91','showtextlowindex','disableinput','enableinput','xorcfc0bit',
    'rungenericnpclowindex','loadscript','moveright','moveleft','movedown','wait','asm15',
    'retscript','setanimation','setmusic','setspeed','setstate','callscript','playsound',
    'checkcfc0bit','giveitem','orroomflag','resetmusic','scriptjump')) { [void]$tokkeyAllowed.Add($op) }
$tokkeyCommands = @(
    Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts\ages\scripts.s') 'tokkeyScript' $tokkeyAllowed 'dinScript'
    Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts\ages\scriptHelper.s') 'tokkayScript_justHeardTune_body' $tokkeyAllowed
)
$tokkeyExpanded = [Collections.Generic.List[object]]::new()
$tokkeyTargets = @{}
foreach ($command in $tokkeyCommands) {
    if (!$tokkeyTargets.ContainsKey($command.Label)) { $tokkeyTargets[$command.Label] = $tokkeyExpanded.Count }
    $count = if ($command.Opcode -eq 'rungenericnpclowindex') { 5 } else { 1 }
    for ($i=0; $i -lt $count; $i++) { $tokkeyExpanded.Add(@($command,$i)) }
}
function Resolve-TokkeyTarget([string]$label) {
    $label = $label -replace '^(mainScripts|scriptHelp)\.', ''
    if (!$tokkeyTargets.ContainsKey($label)) { throw "Tokkey references unknown script $label." }
    return $tokkeyTargets[$label].ToString()
}
$tokkeyRows = [Collections.Generic.List[string]]::new()
$tokkeyRows.Add($cutsceneCommandHeader)
foreach ($entry in $tokkeyExpanded) {
    $command=$entry[0]; $op=$command.Opcode; $args=$command.Operands; $parts=@($args -split ',\s*')
    $actor=''; $a=''; $b=''; $payload=''; $index=$tokkeyRows.Count-1
    switch ($op) {
        'rungenericnpclowindex' {
            switch ($entry[1]) {
                0 { $op='nativeyield'; $payload='LoadText:'+$args.Substring(4) }
                1 { $op='initcollisions'; $actor='Tokkey' }
                2 { $op='checkabutton'; $actor='Tokkey' }
                3 { $op='showtext'; $a=$args.Substring(4); $payload=$tokkeyTexts[[Convert]::ToInt32($a,16)] }
                4 { $op='scriptjump'; $a=($index-2).ToString() }
            }
        }
        'initcollisions' { $actor='Tokkey' }
        'checkabutton' { $actor='Tokkey' }
        'setcollisionradii' { $actor='Tokkey'; $a=$parts[0].Substring(1); $b=$parts[1].Substring(1) }
        'jumpifroomflagset' { $a=$parts[0].Substring(1); $b=Resolve-TokkeyTarget $parts[1] }
        'setdisabledobjectsto91' { $op='setdisabledobjects'; $a='91' }
        'showtextlowindex' { $op='showtext'; $a=$args.Substring(4); $payload=$tokkeyTexts[[Convert]::ToInt32($a,16)] }
        'xorcfc0bit' { if ($args -notin @('0','1')) { throw "Unknown Tokkey signal $args" }; $op='nativeyield'; $payload="Xor:$args" }
        'checkcfc0bit' { if ($args -ne '7') { throw "Unknown Tokkey gate $args" }; $op='gate'; $payload='SongFinished' }
        'loadscript' { $op='scriptjump'; $a=Resolve-TokkeyTarget $args }
        'scriptjump' { $a=Resolve-TokkeyTarget $args }
        'callscript' { $a=Resolve-TokkeyTarget $args }
        'retscript' { $op='return' }
        'wait' { $a=$args }
        'setstate' { $op='nativeyield'; $payload='State:'+$args.Substring(1) }
        'setspeed' { $actor='Tokkey'; $a=(Resolve-ObjectSpeed ($args -replace '^SPEED_','')).ToString('x2') }
        { $_ -in @('moveright','moveleft','movedown') } {
            $angle=@{moveright=8; movedown=16; moveleft=24}[$op]
            $op='move'; $actor='Tokkey'; $a=$angle.ToString('x2'); $b=$parts[0].Substring(1)
            $payload=Resolve-NpcAnimation 0x9d ($angle / 8)
        }
        'setanimation' { $actor='Tokkey'; $a=$args.Substring(1); $payload=Resolve-NpcAnimation 0x9d ([Convert]::ToInt32($a,16)) }
        'asm15' {
            $op='native'; $payload=$args -replace '^scriptHelp\.',''
            if ($payload -notin @('tokkey_jump','tokkey_centerLinkOnTile','tokkey_makeLinkPlayTuneOfCurrents')) { throw "Unknown Tokkey helper $args" }
        }
        { $_ -in @('playsound','setmusic') } {
            $sounds=@{SNDCTRL_STOPMUSIC='f0'; SNDCTRL_STOPSFX='f1'; MUS_CRAZY_DANCE='31'; SND_BIG_EXPLOSION='79'}
            if (!$sounds.ContainsKey($args)) { throw "Unknown Tokkey sound $args" }; $a=$sounds[$args]
        }
        'resetmusic' { $op='setmusic'; $a='ff' }
        'giveitem' {
            if ($args -ne 'TREASURE_OBJECT_TUNE_OF_CURRENTS_00') { throw "Unknown Tokkey reward $args" }
            $reward=$treasureObjectRecords[$args]; $a=$reward.Treasure.ToString('x2'); $b=$reward.SubId.ToString('x2')
        }
        'orroomflag' { $a=$args.Substring(1) }
        { $_ -in @('disableinput','enableinput') } { }
        default { throw "Unsupported Tokkey command $op at $($command.Line)." }
    }
    if ($op -eq 'showtext') {
        $textId = [Convert]::ToInt32($a, 16)
        if (!$tokkeyTexts.ContainsKey($textId)) { throw "Unimported Tokkey TX_$a." }
        if ($allTextPositions.ContainsKey($textId)) { $b=$allTextPositions[$textId].ToString() }
    }
    $tokkeyRows.Add((New-CutsceneCommandRow $command.Script $index $command.Label $command.Line $op $actor $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\tokkey_commands.tsv'), $tokkeyRows)
$tokkeyExclamation = $interactionGraphics['159:0']
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\tokkey_event.tsv'), @(
    "# group`troom`tid`tsubid`tharp-tile`texclamation-frames`tjump-speed`tgravity`tbounce-gravity`tbounce-speed`tnote-mask`theard-entry`twrong-position-text-base64`texclamation-sprite`texclamation-tile`texclamation-palette`texclamation-animation`twrong-position-textbox-position",
    (@('3','8f','9d','00','32','60','-416','32','96','-512','0f',
        (Resolve-TokkeyTarget 'tokkeyScript_justHeardTune'), (ConvertTo-CutsceneCommandPayload $allTexts[0x2c05]),
        $gfxNames[$tokkeyExclamation.Gfx], $tokkeyExclamation.TileBase.ToString(),
        $tokkeyExclamation.Palette.ToString(), (Resolve-NpcAnimation 0x9f 0),
        $allTextPositions[0x2c05].ToString()) -join "`t")
))

# Room 2:0f Cheval. INTERAC_CHEVAL installs its script during source state 0,
# then the script initializes asymmetric collision radii and chooses one of
# two permanent A-button loops from Cheval Rope ownership. Both loops set the
# talked-to-Cheval global flag only after their textbox command has completed.
$chevalScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$chevalOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'setcollisionradii', 'jumpifitemobtained',
    'checkabutton', 'showtextlowindex', 'asm15', 'scriptjump')) {
    [void]$chevalOpcodes.Add($opcode)
}
$chevalCommands = @(Read-AssemblyCutsceneCommands `
    $chevalScriptPath 'cheval_subid00Script' $chevalOpcodes 'script71a3')
$chevalExpected = @(
    @('initcollisions', ''),
    @('setcollisionradii', '$0c, $06'),
    @('jumpifitemobtained', 'TREASURE_CHEVAL_ROPE, @gotChevalRope'),
    @('checkabutton', ''),
    @('showtextlowindex', '<TX_270c'),
    @('asm15', 'scriptHelp.cheval_setTalkedGlobalflag'),
    @('scriptjump', '@dontHaveChevalRope'),
    @('checkabutton', ''),
    @('showtextlowindex', '<TX_270d'),
    @('asm15', 'scriptHelp.cheval_setTalkedGlobalflag'),
    @('scriptjump', '@gotChevalRope')
)
if ($chevalCommands.Count -ne $chevalExpected.Count) {
    throw "cheval_subid00Script expected 11 commands, parsed $($chevalCommands.Count)."
}
for ($index = 0; $index -lt $chevalExpected.Count; $index++) {
    $actualOperands = if ($null -eq $chevalCommands[$index].Operands) {
        ''
    } else {
        ([string]$chevalCommands[$index].Operands).Trim()
    }
    if ($chevalCommands[$index].Opcode -ne $chevalExpected[$index][0] -or
        $actualOperands -ne $chevalExpected[$index][1]) {
        throw "cheval_subid00Script command $index changed from " +
            "$($chevalExpected[$index][0]) '$($chevalExpected[$index][1])' to " +
            "$($chevalCommands[$index].Opcode) '$actualOperands'."
    }
}

$chevalTargets = @{}
foreach ($command in $chevalCommands) {
    if (-not $chevalTargets.ContainsKey($command.Label)) {
        $chevalTargets[$command.Label] = $command.Index
    }
}
foreach ($entry in @(
    @('@dontHaveChevalRope', 3),
    @('@gotChevalRope', 7))) {
    if (-not $chevalTargets.ContainsKey($entry[0]) -or
        $chevalTargets[$entry[0]] -ne $entry[1]) {
        throw "cheval_subid00Script label $($entry[0]) moved from command $($entry[1])."
    }
}

$chevalNativeSource = Read-ImportText (Join-Path $Disassembly `
    'object_code\ages\interactions\cheval.s')
$chevalHelperSource = Read-ImportText (Join-Path $Disassembly `
    'scripts\ages\scriptHelper.s')
$chevalInteractionDataSource = Read-ImportText (Join-Path $Disassembly `
    'data\ages\interactionData.s')
$chevalGlobalFlagSource = Read-ImportText (Join-Path $Disassembly `
    'constants\common\globalFlags.s')
$chevalTreasureSource = Read-ImportText (Join-Path $Disassembly `
    'constants\common\treasure.s')
if ($chevalNativeSource -notmatch
        '(?ms)^@state0:.*?ld a,\$01\s+ld \(de\),a\s+call interactionInitGraphics\s+call objectSetVisiblec2\s+ld a,>TX_2700\s+call interactionSetHighTextIndex.*?^@state1:.*?^@runSubid00:\s+call interactionRunScript\s+jp interactionAnimateAsNpc' -or
    $chevalNativeSource -notmatch
        '(?ms)^@loadScript:.*?jp interactionSetScript.*?^@scriptTable:\s+\.dw mainScripts\.cheval_subid00Script' -or
    $chevalInteractionDataSource -notmatch
        '(?m)^\s*/\* \$6a \*/ m_InteractionData \$59 \$1a \$00\s*$') {
    throw 'INTERAC_CHEVAL native initialization, update order, or graphics data changed.'
}
if ($chevalHelperSource -notmatch
        '(?ms)^cheval_setTalkedGlobalflag:\s+ld a,GLOBALFLAG_TALKED_TO_CHEVAL\s+jp setGlobalFlag' -or
    $chevalGlobalFlagSource -notmatch
        '(?m)^\s*GLOBALFLAG_TALKED_TO_CHEVAL\s+db ; \$43\s*$' -or
    $chevalTreasureSource -notmatch
        '(?m)^\s*TREASURE_CHEVAL_ROPE\s+db ; \$52\s*$') {
    throw 'Cheval talked flag or Cheval Rope constant changed.'
}
if ($mainObjectSource -notmatch
        '(?ms)^group2Map0fObjectData:\s+obj_Interaction \$6a \$00 \$40 \$50\s+obj_End') {
    throw 'Room 2:0f Cheval object stream or coordinates changed.'
}

$chevalAnimation = Resolve-NpcAnimation 0x6a 0
$chevalGraphic = $interactionGraphics['106:0']
if ([string]::IsNullOrWhiteSpace($chevalAnimation) -or
    $null -eq $chevalGraphic -or
    -not $gfxNames.ContainsKey($chevalGraphic.Gfx) -or
    $gfxNames[$chevalGraphic.Gfx] -ne 'spr_oldzora_cheval' -or
    $chevalGraphic.TileBase -ne 0x1a -or
    $chevalGraphic.Palette -ne 0 -or
    $chevalGraphic.DefaultAnimation -ne 0) {
    throw 'Could not resolve INTERAC_CHEVAL graphics or animation `$00.'
}
$chevalTextIds = @(0x270b, 0x270c, 0x270d)
foreach ($textId in $chevalTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Cheval text TX_$($textId.ToString('x4'))."
    }
}
foreach ($textId in @(0x270c, 0x270d)) {
    if ($allTexts[$textId] -notmatch '^\\call\(TX_270b\)') {
        throw "Cheval TX_$($textId.ToString('x4')) lost its call to TX_270b."
    }
}
$chevalResolvedTexts = @{}
foreach ($textId in @(0x270c, 0x270d)) {
    $chevalResolvedTexts[$textId] = $allTexts[$textId].Replace(
        '\call(TX_270b)', $allTexts[0x270b])
}

$chevalCommandRows = ConvertTo-CutsceneCommandRows `
    $chevalCommands 'Cheval' -texts $chevalResolvedTexts `
    -bindings @{
        'jumpifitemobtained|TREASURE_CHEVAL_ROPE, @gotChevalRope' = @{
            Opcode = 'jumpifmemoryeq'; Arg0 = '01'
            Arg1 = $chevalTargets['@gotChevalRope'].ToString(); Payload = 'HasChevalRope'
        }
        'asm15|scriptHelp.cheval_setTalkedGlobalflag' = @{ Opcode = 'setglobalflag'; Arg0 = '43' }
    }
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\cheval_commands.tsv'),
    $chevalCommandRows)

$chevalTextRows = [Collections.Generic.List[string]]::new()
$chevalTextRows.Add("# id`ttext-base64")
foreach ($textId in $chevalTextIds) {
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $chevalTextRows.Add("$($textId.ToString('x4'))`t$encoded")
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\cheval_text.tsv'),
    $chevalTextRows)

$chevalEventRows = @(
    "# group`troom`tid`tsubid`tsprite`ttile-base`tpalette`tanimation0`tinitial-animation`tcollision-y`tcollision-x`tcheval-rope-treasure`ttalked-global-flag`tinitial-script-updates"
    (@(
        '2', '0f', '6a', '00', 'spr_oldzora_cheval', '1a', '00',
        $chevalAnimation, '00', '0c', '06', '52', '43', '0'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\cheval_event.tsv'),
    $chevalEventRows)
