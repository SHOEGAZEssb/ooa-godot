# Ralph's first portal departure is INTERAC_RALPH ($37) subid $0d in room
# 0:39. Export the entry-direction guard, complete script timing/movement,
# animations, one-shot global flag, and text from their original records.
$ralphSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\ralph.s')
$ralphScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$ralphInitMatch = [regex]::Match(
    $ralphSource,
    '(?ms)@initSubid0d:\s*ld a,\(wScreenTransitionDirection\)\s*cp \$(?<direction>[0-9a-f]{2})\s*jp nz,interactionDelete.*?ld hl,mainScripts\.ralphSubid0dScript')
if (-not $ralphInitMatch.Success) {
    throw 'Could not parse the room-entry direction guard for Ralph subid $0d.'
}
$ralphScriptMatch = [regex]::Match(
    $ralphScriptSource,
    '(?ms)^ralphSubid0dScript:(?<body>.*?)(?=^ralphSubid0eScript:)')
if (-not $ralphScriptMatch.Success) {
    throw 'Could not parse ralphSubid0dScript.'
}
$ralphBody = $ralphScriptMatch.Groups['body'].Value
$ralphWaits = @([regex]::Matches($ralphBody, '(?m)^\s*wait\s+(?<frames>\d+)') |
    ForEach-Object { [int]$_.Groups['frames'].Value })
if ($ralphWaits.Count -ne 2 -or ($ralphWaits -join ',') -ne '40,30') {
    throw "Unexpected Ralph portal event waits: $($ralphWaits -join ',')."
}
$ralphCommandMatch = [regex]::Match(
    $ralphBody,
    '(?ms)showtext\s+TX_(?<text>[0-9a-f]{4}).*?setanimation\s+\$(?<moveAnimation>[0-9a-f]{2})\s+setspeed\s+(?<speed>[A-Z0-9_]+)\s+setangle\s+\$(?<angle>[0-9a-f]{2})\s+applyspeed\s+\$(?<moveFrames>[0-9a-f]{2})\s+setanimation\s+\$(?<portalAnimation>[0-9a-f]{2})\s+writeobjectbyte\s+Interaction\.var3f,\s*\$(?<flickerFrames>[0-9a-f]{2}).*?setglobalflag\s+(?<flag>[A-Z0-9_]+)')
if (-not $ralphCommandMatch.Success -or
    $ralphCommandMatch.Groups['speed'].Value -ne 'SPEED_100' -or
    $ralphCommandMatch.Groups['flag'].Value -ne 'GLOBALFLAG_RALPH_ENTERED_PORTAL') {
    throw 'Could not parse the Ralph portal movement, flicker, and flag commands.'
}
$speedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$speedMatch = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_100\s+dsb\s+(?<count>\d+)\s*;\s*0x(?<value>[0-9a-f]{2})')
if (-not $speedMatch.Success -or $speedMatch.Groups['value'].Value -ne '28') {
    throw 'SPEED_100 no longer resolves to original object speed $28.'
}
$globalFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
$flagMatch = [regex]::Match(
    $globalFlagSource,
    '(?m)^\s*GLOBALFLAG_RALPH_ENTERED_PORTAL\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $flagMatch.Success -or $flagMatch.Groups['value'].Value -ne '40') {
    throw 'GLOBALFLAG_RALPH_ENTERED_PORTAL no longer resolves to $40.'
}
$ralphNpcRow = $npcRows | Where-Object { $_ -match '^0\t39\t37\t0d\t' } |
    Select-Object -First 1
if (-not $ralphNpcRow) {
    throw 'The positioned INTERAC_RALPH $37:$0d record in room 0:39 was not extracted.'
}
$ralphNpcColumns = $ralphNpcRow -split "`t"
if ($ralphNpcColumns[4] -ne '28' -or $ralphNpcColumns[5] -ne '18') {
    throw 'INTERAC_RALPH $37:$0d moved from original position $28/$18.'
}
$ralphTextId = [Convert]::ToInt32($ralphCommandMatch.Groups['text'].Value, 16)
if ($ralphTextId -ne 0x2a1e -or -not $allTexts.ContainsKey($ralphTextId) -or
    $allTextPositions.ContainsKey($ralphTextId)) {
    throw 'Expected Ralph portal dialogue TX_2a1e without a fixed textbox position.'
}
$ralphMoveAnimationIndex = [Convert]::ToInt32(
    $ralphCommandMatch.Groups['moveAnimation'].Value, 16)
$ralphPortalAnimationIndex = [Convert]::ToInt32(
    $ralphCommandMatch.Groups['portalAnimation'].Value, 16)
$ralphMoveAnimation = Resolve-NpcAnimation 0x37 $ralphMoveAnimationIndex
$ralphPortalAnimation = Resolve-NpcAnimation 0x37 $ralphPortalAnimationIndex
if (-not $ralphMoveAnimation -or -not $ralphPortalAnimation) {
    throw 'Could not resolve Ralph portal event animations $01 and $09.'
}
$ralphEventColumns = @(
    '0', '39', '37', '0d', $ralphInitMatch.Groups['direction'].Value,
    $flagMatch.Groups['value'].Value
)
$ralphEventRows = @(
    "# group`troom`tid`tsubid`tentry-direction`tglobal-flag",
    ($ralphEventColumns -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ralph_portal_event.tsv'),
    $ralphEventRows)

# Emit the active path as typed command records. Command rows retain the
# assembly script/label, normalized command index, and physical source line.
# The recognized flicker loop remains one native-effect command, while its
# counter byte and frame mask stay explicit operands.
$ralphMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$ralphSoundMatch = [regex]::Match(
    $ralphMusicSource,
    '(?m)^\s*SND_MYSTERY_SEED\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $ralphSoundMatch.Success -or $ralphSoundMatch.Groups['value'].Value -ne '7b') {
    throw 'SND_MYSTERY_SEED no longer resolves to $7b.'
}

$ralphBodyStart = $ralphScriptMatch.Groups['body'].Index
$ralphBodyEnd = $ralphBodyStart + $ralphScriptMatch.Groups['body'].Length
$findRalphSourceLine = {
    param([string]$pattern)
    return Find-CutsceneCommandSourceLine `
        $ralphScriptSource $ralphBodyStart $ralphBodyEnd $pattern 'ralphSubid0dScript'
}
$newRalphCommandRow = {
    param(
        [int]$index,
        [string]$label,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return New-CutsceneCommandRow `
        'ralphSubid0dScript' $index $label $line $opcode $actor $arg0 $arg1 $payload
}

$ralphCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newRalphCommandRow 0 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*disableinput\s*$') 'disableinput' '' '' '' ''),
    (& $newRalphCommandRow 1 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*wait\s+40\s*$') 'wait' '' '40' '' ''),
    (& $newRalphCommandRow 2 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*showtext\s+TX_2a1e\s*$') 'showtext' '' '2a1e' '' $allTexts[$ralphTextId]),
    (& $newRalphCommandRow 3 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*wait\s+30\s*$') 'wait' '' '30' '' ''),
    (& $newRalphCommandRow 4 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*setanimation\s+\$01\s*$') 'setanimation' 'Ralph' '01' '' $ralphMoveAnimation),
    (& $newRalphCommandRow 5 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*setspeed\s+SPEED_100\s*$') 'setspeed' 'Ralph' '28' '' ''),
    (& $newRalphCommandRow 6 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*setangle\s+\$08\s*$') 'setangle' 'Ralph' '08' '' ''),
    (& $newRalphCommandRow 7 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*applyspeed\s+\$11\s*$') 'applyspeed' 'Ralph' '11' '' ''),
    (& $newRalphCommandRow 8 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*setanimation\s+\$09\s*$') 'setanimation' 'Ralph' '09' '' $ralphPortalAnimation),
    (& $newRalphCommandRow 9 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*writeobjectbyte\s+Interaction\.var3f,\s*\$2d\s*$') 'writeobjectbyte' 'Ralph' '3f' '2d' ''),
    (& $newRalphCommandRow 10 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*playsound\s+SND_MYSTERY_SEED\s*$') 'playsound' '' $ralphSoundMatch.Groups['value'].Value '' ''),
    (& $newRalphCommandRow 11 '@flickerVisibility' (& $findRalphSourceLine '(?m)^\s*asm15\s+scriptHelp\.ralph_flickerVisibility\s*$') 'flicker' 'Ralph' '3f' '01' ''),
    (& $newRalphCommandRow 12 '@done' (& $findRalphSourceLine '(?m)^\s*setglobalflag\s+GLOBALFLAG_RALPH_ENTERED_PORTAL\s*$') 'setglobalflag' '' $flagMatch.Groups['value'].Value '' ''),
    (& $newRalphCommandRow 13 '@done' (& $findRalphSourceLine '(?m)^\s*asm15\s+scriptHelp\.ralph_restoreMusic\s*$') 'native' '' '' '' 'ralph_restoreMusic'),
    (& $newRalphCommandRow 14 '@done' (& $findRalphSourceLine '(?m)^\s*enableinput\s*$') 'enableinput' '' '' '' ''),
    (& $newRalphCommandRow 15 '@done' (& $findRalphSourceLine '(?m)^\s*scriptend\s*$') 'scriptend' '' '' '' '')
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ralph_portal_commands.tsv'),
    $ralphCommandRows)
