# The first arrival in the past is INTERAC_MALE_VILLAGER ($3a:$0d) in room
# 1:39. Its leading wait advances while TRANSITION_DEST_TIMEWARP finishes, so
# export the script counters, jump physics, speeds, path, animations, text,
# sound, completion flag, and expected arrival overlap as one checked record.
$enterPastVillagerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\villager.s')
$enterPastScriptMatch = [regex]::Match(
    $ralphScriptSource,
    '(?ms)^villagerSubid0dScript:(?<body>.*?)(?=^; =+\s*^; INTERAC_FEMALE_VILLAGER)')
if (-not $enterPastScriptMatch.Success) {
    throw 'Could not parse villagerSubid0dScript.'
}
$enterPastBody = $enterPastScriptMatch.Groups['body'].Value
$enterPastCommands = [regex]::Match(
    $enterPastBody,
    '(?ms)^\s*jumpifglobalflagset\s+(?<guard>[A-Z0-9_]+),\s*stubScript\s+setdisabledobjectsto11\s+wait\s+(?<intro>\d+)\s+disableinput\s+wait\s+(?<preJump>\d+)\s+callscript\s+jumpAndWaitUntilLanded\s+wait\s+(?<postJump>\d+)\s+showtext\s+TX_(?<text>[0-9a-f]{4})\s+wait\s+(?<postText>\d+)\s+setspeed\s+(?<fast1>[A-Z0-9_]+)\s+movedown\s+\$(?<firstDown>[0-9a-f]{2})\s+moveright\s+\$(?<right>[0-9a-f]{2})\s+movedown\s+\$(?<secondDown>[0-9a-f]{2})\s+setspeed\s+(?<slow>[A-Z0-9_]+)\s+applyspeed\s+\$(?<slowDown>[0-9a-f]{2})\s+setspeed\s+(?<fast2>[A-Z0-9_]+)\s+applyspeed\s+\$(?<finalDown>[0-9a-f]{2})\s+setglobalflag\s+(?<finish>[A-Z0-9_]+)\s+enableinput\s+scriptend\s*$')
if (-not $enterPastCommands.Success -or
    $enterPastCommands.Groups['guard'].Value -ne 'GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE' -or
    $enterPastCommands.Groups['finish'].Value -ne 'GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE' -or
    $enterPastCommands.Groups['fast1'].Value -ne 'SPEED_100' -or
    $enterPastCommands.Groups['fast2'].Value -ne 'SPEED_100' -or
    $enterPastCommands.Groups['slow'].Value -ne 'SPEED_080') {
    throw 'Could not parse the first-past-arrival script command sequence.'
}
if ($enterPastVillagerSource -notmatch
        '(?ms)^@initSubid0d:\s*call @loadScript\s+jr @state1' -or
    $enterPastVillagerSource -notmatch
        '(?ms)^@runSubid0d:\s*call interactionRunScript\s+jp c,interactionDelete\s+call interactionAnimateBasedOnSpeed\s+jp interactionPushLinkAwayAndUpdateDrawPriority') {
    throw 'INTERAC_MALE_VILLAGER $3a:$0d no longer runs, animates, pushes Link, and deletes in the expected order.'
}

$enterPastNpcRow = $npcRows | Where-Object { $_ -match '^1\t39\t3a\t0d\t' } |
    Select-Object -First 1
if (-not $enterPastNpcRow) {
    throw 'The positioned INTERAC_MALE_VILLAGER $3a:$0d record in room 1:39 was not extracted.'
}
$enterPastNpcColumns = $enterPastNpcRow -split "`t"
if ($enterPastNpcColumns[4] -ne '28' -or $enterPastNpcColumns[5] -ne '18') {
    throw 'INTERAC_MALE_VILLAGER $3a:$0d moved from original position $28/$18.'
}

$enterPastFlagMatch = [regex]::Match(
    $globalFlagSource,
    '(?m)^\s*GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $enterPastFlagMatch.Success -or
    $enterPastFlagMatch.Groups['value'].Value -ne '41') {
    throw 'GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE no longer resolves to $41.'
}
$enterPastSlowSpeedMatch = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_80\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})')
if (-not $enterPastSlowSpeedMatch.Success -or
    $enterPastSlowSpeedMatch.Groups['value'].Value -ne '14' -or
    $speedSource -notmatch '(?m)^\s*\.define\s+SPEED_080\s+SPEED_80\s*$') {
    throw 'SPEED_080 no longer aliases original object speed $14.'
}

$enterPastHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$enterPastJumpMatch = [regex]::Match(
    $enterPastHelperSource,
    '(?ms)^beginJump:\s*ld h,d\s*ld l,Interaction\.speedZ\s*ld \(hl\),\$(?<low>[0-9a-f]{2})\s*inc hl\s*ld \(hl\),\$(?<high>[0-9a-f]{2})\s*ld a,(?<sound>[A-Z0-9_]+)\s*jp playSound.*?^updateGravity:\s*ld c,\$(?<gravity>[0-9a-f]{2})\s*call objectUpdateSpeedZ_paramC')
if (-not $enterPastJumpMatch.Success -or
    $enterPastJumpMatch.Groups['sound'].Value -ne 'SND_JUMP') {
    throw 'Could not resolve beginJump/updateGravity for the first-past-arrival event.'
}
$enterPastJumpRaw =
    ([Convert]::ToInt32($enterPastJumpMatch.Groups['high'].Value, 16) -shl 8) -bor
    [Convert]::ToInt32($enterPastJumpMatch.Groups['low'].Value, 16)
if ($enterPastJumpRaw -ge 0x8000) { $enterPastJumpRaw -= 0x10000 }
$enterPastGravity = [Convert]::ToInt32(
    $enterPastJumpMatch.Groups['gravity'].Value, 16)
if ($enterPastJumpRaw -ne -0x200 -or $enterPastGravity -ne 0x30) {
    throw 'The first-past-arrival jump changed from speedZ -$0200 and gravity $30.'
}
$enterPastMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$enterPastSoundMatch = [regex]::Match(
    $enterPastMusicSource,
    '(?m)^\s*SND_JUMP\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $enterPastSoundMatch.Success -or
    $enterPastSoundMatch.Groups['value'].Value -ne '53') {
    throw 'SND_JUMP no longer resolves to $53.'
}

$enterPastTextId = [Convert]::ToInt32(
    $enterPastCommands.Groups['text'].Value, 16)
if ($enterPastTextId -ne 0x1622 -or
    -not $allTexts.ContainsKey($enterPastTextId) -or
    $allTextPositions.ContainsKey($enterPastTextId)) {
    throw 'Expected first-past-arrival dialogue TX_1622 without a fixed textbox position.'
}
$enterPastRightAnimation = Resolve-NpcAnimation 0x3a 1
$enterPastDownAnimation = Resolve-NpcAnimation 0x3a 2
if (-not $enterPastRightAnimation -or -not $enterPastDownAnimation) {
    throw 'Could not resolve male villager right/down animations $01/$02.'
}

# Native interactionAnimateBasedOnSpeed doubles at SPEED_100; all script
# counters, text and movement operands are stored only in the command stream.
$enterPastEventColumns = @(
    '1', '39', '3a', '0d', $speedMatch.Groups['value'].Value,
    $enterPastFlagMatch.Groups['value'].Value
)
$enterPastEventRows = @(
    "# group`troom`tid`tsubid`tanimation-double-speed`tglobal-flag",
    ($enterPastEventColumns -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\enter_past_event.tsv'),
    $enterPastEventRows)

# The second shared-runner slice preserves the active path's actual command
# boundaries. jumpAndWaitUntilLanded remains one typed composite command, but
# retains callscript's setup-only update before beginJump/updateGravity.
$enterPastBodyStart = $enterPastScriptMatch.Groups['body'].Index
$enterPastBodyEnd = $enterPastBodyStart + $enterPastScriptMatch.Groups['body'].Length
$findEnterPastSourceLine = {
    param([string]$pattern, [int]$occurrence = 0)
    return Find-CutsceneCommandSourceLine `
        $ralphScriptSource $enterPastBodyStart $enterPastBodyEnd $pattern `
        'villagerSubid0dScript' $occurrence
}
$newEnterPastCommandRow = {
    param(
        [int]$index,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return New-CutsceneCommandRow `
        'villagerSubid0dScript' $index 'villagerSubid0dScript' $line `
        $opcode $actor $arg0 $arg1 $payload
}

$enterPastCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newEnterPastCommandRow 0 (& $findEnterPastSourceLine '(?m)^\s*setdisabledobjectsto11\s*$') 'setdisabledobjects' '' '11' '' ''),
    (& $newEnterPastCommandRow 1 (& $findEnterPastSourceLine '(?m)^\s*wait\s+100\s*$') 'wait' '' '100' '' ''),
    (& $newEnterPastCommandRow 2 (& $findEnterPastSourceLine '(?m)^\s*disableinput\s*$') 'disableinput' '' '' '' ''),
    (& $newEnterPastCommandRow 3 (& $findEnterPastSourceLine '(?m)^\s*wait\s+40\s*$') 'wait' '' '40' '' ''),
    (& $newEnterPastCommandRow 4 (& $findEnterPastSourceLine '(?m)^\s*callscript\s+jumpAndWaitUntilLanded\s*$') 'jump' 'Villager' $enterPastJumpRaw.ToString() $enterPastGravity.ToString('x2') $enterPastSoundMatch.Groups['value'].Value),
    (& $newEnterPastCommandRow 5 (& $findEnterPastSourceLine '(?m)^\s*wait\s+30\s*$') 'wait' '' '30' '' ''),
    (& $newEnterPastCommandRow 6 (& $findEnterPastSourceLine '(?m)^\s*showtext\s+TX_1622\s*$') 'showtext' '' '1622' '' $allTexts[$enterPastTextId]),
    (& $newEnterPastCommandRow 7 (& $findEnterPastSourceLine '(?m)^\s*wait\s+30\s*$' 1) 'wait' '' '30' '' ''),
    (& $newEnterPastCommandRow 8 (& $findEnterPastSourceLine '(?m)^\s*setspeed\s+SPEED_100\s*$') 'setspeed' 'Villager' $speedMatch.Groups['value'].Value '' ''),
    (& $newEnterPastCommandRow 9 (& $findEnterPastSourceLine '(?m)^\s*movedown\s+\$11\s*$') 'move' 'Villager' '10' '11' $enterPastDownAnimation),
    (& $newEnterPastCommandRow 10 (& $findEnterPastSourceLine '(?m)^\s*moveright\s+\$11\s*$') 'move' 'Villager' '08' '11' $enterPastRightAnimation),
    (& $newEnterPastCommandRow 11 (& $findEnterPastSourceLine '(?m)^\s*movedown\s+\$09\s*$') 'move' 'Villager' '10' '09' $enterPastDownAnimation),
    (& $newEnterPastCommandRow 12 (& $findEnterPastSourceLine '(?m)^\s*setspeed\s+SPEED_080\s*$') 'setspeed' 'Villager' $enterPastSlowSpeedMatch.Groups['value'].Value '' ''),
    (& $newEnterPastCommandRow 13 (& $findEnterPastSourceLine '(?m)^\s*applyspeed\s+\$21\s*$') 'applyspeed' 'Villager' '21' '' ''),
    (& $newEnterPastCommandRow 14 (& $findEnterPastSourceLine '(?m)^\s*setspeed\s+SPEED_100\s*$' 1) 'setspeed' 'Villager' $speedMatch.Groups['value'].Value '' ''),
    (& $newEnterPastCommandRow 15 (& $findEnterPastSourceLine '(?m)^\s*applyspeed\s+\$39\s*$') 'applyspeed' 'Villager' '39' '' ''),
    (& $newEnterPastCommandRow 16 (& $findEnterPastSourceLine '(?m)^\s*setglobalflag\s+GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE\s*$') 'setglobalflag' '' $enterPastFlagMatch.Groups['value'].Value '' ''),
    (& $newEnterPastCommandRow 17 (& $findEnterPastSourceLine '(?m)^\s*enableinput\s*$') 'enableinput' '' '' '' ''),
    (& $newEnterPastCommandRow 18 (& $findEnterPastSourceLine '(?m)^\s*scriptend\s*$') 'scriptend' '' '' '' '')
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\enter_past_commands.tsv'),
    $enterPastCommandRows)
