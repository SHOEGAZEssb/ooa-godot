# The children in present room 0:7b implement the Spirit's Grave ghost scene
# as three native interaction state machines synchronized through cfd1. Keep
# this as native event metadata: the scripts run concurrently, and their
# source-order RNG calls and same-update signal handoffs are observable.
$graveyardBoySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\boy.s')
$graveyardBoy2Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\boy2.s')
$graveyardScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$graveyardHelperSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\ralph.s')
$graveyardOscillationSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\impaInCutscene.s')

if ($mainObjectSource -notmatch '(?ms)^group0Map7bObjectData:\s+obj_Interaction \$3c \$03 \$48 \$48\s+obj_Interaction \$3c \$04 \$48 \$68\s+obj_Interaction \$3f \$02 \$38 \$58\s+obj_Interaction \$b6 \$03 \$28 \$68\s+obj_End') {
    throw 'Room 0:7b child/Gasha object ordering changed.'
}
if ($graveyardBoySource -notmatch '(?ms)^@initSubid03:.*?getThisRoomFlags\s+bit 6,a\s+jp nz,interactionDelete.*?ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a.*?^@setRedPaletteAndLoadScript:\s+ld a,\$02\s+ld e,Interaction\.oamFlags' -or
    $graveyardBoySource -notmatch '(?ms)^@initSubid04:.*?getThisRoomFlags\s+bit 6,a\s+jp nz,interactionDelete.*?ld a,\$3c\s+ld \(de\),a\s+xor a\s+call interactionSetAnimation\s*^@saveXToVar3d:' -or
    $graveyardBoySource -notmatch '(?ms)^boyRunSubid03:.*?interactionRunScript.*?Interaction\.var39.*?interactionAnimateBasedOnSpeed.*?objectCheckWithinScreenBoundary.*?ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a\s+call getThisRoomFlags\s+set 6,\(hl\)\s+jp interactionDelete' -or
    $graveyardBoySource -notmatch '(?ms)^boyRunSubid04:.*?@substate0:.*?interactionAnimate.*?interactionDecCounter1.*?interactionIncSubstate\s+jp startJump.*?@substate1:.*?ld c,\$20\s+call objectUpdateSpeedZ_paramC.*?interactionIncSubstate\s+jp boyLoadScript') {
    throw 'INTERAC_BOY $3c:$03/$04 ghost-scene native states changed.'
}
if ($graveyardBoy2Source -notmatch '(?ms)^@subid2:.*?getThisRoomFlags\s+bit 6,a\s+jp nz,interactionDelete.*?Interaction\.var3d.*?Interaction\.xh.*?^@@substate0:.*?interactionAnimate.*?cp \$01.*?interactionIncSubstate\s+jpab agesInteractionsBank08\.startJump.*?^@@substate1:.*?ld c,\$20\s+call objectUpdateSpeedZ_paramC.*?interactionIncSubstate\s+call @initializeScript.*?^@@substate2:\s+jpab agesInteractionsBank08\.boyRunSubid03') {
    throw 'INTERAC_BOY_2 $3f:$02 ghost-scene native states changed.'
}

$graveyardRedScript = [regex]::Match(
    $graveyardScriptSource,
    '(?ms)^boySubid03Script:(?<body>.*?)(?=^; Cutscene where kids talk about how they''re scared of a ghost \(green kid\))')
$graveyardGreenScript = [regex]::Match(
    $graveyardScriptSource,
    '(?ms)^boySubid04Script:(?<body>.*?)(?=^; Cutscene where kid is restored from stone)')
$graveyardBlueScript = [regex]::Match(
    $graveyardScriptSource,
    '(?ms)^boy2Subid2Script:(?<body>.*?)(?=^; =+\s*^; INTERAC_SOLDIER)')
if (-not $graveyardRedScript.Success -or -not $graveyardGreenScript.Success -or
    -not $graveyardBlueScript.Success) {
    throw 'Could not isolate the three Spirit''s Grave child scripts.'
}
$graveyardRedBody = $graveyardRedScript.Groups['body'].Value -replace '(?m);.*$', ''
$graveyardGreenBody = $graveyardGreenScript.Groups['body'].Value -replace '(?m);.*$', ''
$graveyardBlueBody = $graveyardBlueScript.Groups['body'].Value -replace '(?m);.*$', ''
$graveyardRedCommands = [regex]::Match(
    $graveyardRedBody,
    '(?ms)^\s*checkmemoryeq wTmpcfc0\.genericCutscene\.cfd1, \$02\s+writeobjectbyte Interaction\.var39, \$01\s+wait (?<freeze1>\d+)\s+showtext TX_(?<red1>[0-9a-f]{4})\s+wait (?<post1>\d+)\s+setanimation \$(?<left>[0-9a-f]{2})\s+wait (?<freeze2>\d+)\s+showtext TX_(?<red2>[0-9a-f]{4})\s+wait (?<post2>\d+)\s+setanimation \$(?<up>[0-9a-f]{2})\s+wait (?<freeze3>\d+)\s+showtext TX_(?<red3>[0-9a-f]{4})\s+wait (?<final>\d+)\s+writememory wTmpcfc0\.genericCutscene\.cfd1, \$03\s+^boyShakeWithFearThenRun:\s+writeobjectbyte Interaction\.var39, \$01\s+writeobjectbyte Interaction\.var38, (?<shake>\d+)\s+^@shake:\s+asm15 scriptHelp\.oscillateXRandomly\s+addobjectbyte Interaction\.var38, -1\s+jumpifobjectbyteeq Interaction\.var38, \$00, @runAway\s+wait 1\s+scriptjump @shake\s+^@runAway:\s+playsound SND_THROW\s+writeobjectbyte Interaction\.var39, \$00\s+setspeed SPEED_200\s+moveright \$(?<flee>[0-9a-f]{2})\s+scriptend\s*$')
$graveyardGreenCommands = [regex]::Match(
    $graveyardGreenBody,
    '(?ms)^\s*wait (?<pre>\d+)\s+showtext TX_(?<text>[0-9a-f]{4})\s+wait (?<post>\d+)\s+writememory\s+wTmpcfc0\.genericCutscene\.cfd1, \$01\s+checkmemoryeq wTmpcfc0\.genericCutscene\.cfd1, \$03\s+scriptjump boyShakeWithFearThenRun\s*$')
$graveyardBlueCommands = [regex]::Match(
    $graveyardBlueBody,
    '(?ms)^\s*wait (?<pre>\d+)\s+showtextlowindex <TX_(?<text>[0-9a-f]{4})\s+writememory\s+wTmpcfc0\.genericCutscene\.cfd1, \$02\s+checkmemoryeq wTmpcfc0\.genericCutscene\.cfd1, \$03\s+scriptjump boyShakeWithFearThenRun\s*$')
if (-not $graveyardRedCommands.Success -or
    -not $graveyardGreenCommands.Success -or
    -not $graveyardBlueCommands.Success -or
    $graveyardRedCommands.Groups['freeze1'].Value -ne '32' -or
    $graveyardRedCommands.Groups['freeze2'].Value -ne '32' -or
    $graveyardRedCommands.Groups['freeze3'].Value -ne '32' -or
    $graveyardRedCommands.Groups['post1'].Value -ne '30' -or
    $graveyardRedCommands.Groups['post2'].Value -ne '30' -or
    $graveyardRedCommands.Groups['final'].Value -ne '60' -or
    $graveyardRedCommands.Groups['shake'].Value -ne '120' -or
    $graveyardGreenCommands.Groups['pre'].Value -ne '30' -or
    $graveyardGreenCommands.Groups['post'].Value -ne '30' -or
    $graveyardBlueCommands.Groups['pre'].Value -ne '30') {
    throw 'The Spirit''s Grave child script waits or synchronization sequence changed.'
}

$graveyardJump = [regex]::Match(
    $graveyardHelperSource,
    '(?ms)^startJump:\s+ld bc,-\$(?<speed>[0-9a-f]{3})\s+call objectSetSpeedZ\s+ld a,SND_JUMP\s+jp playSound')
$graveyardOscillation = [regex]::Match(
    $graveyardOscillationSource,
    '(?ms)^interactionOscillateXRandomly:\s+call getRandomNumber\s+and \$01\s+sub \$01\s+ld h,d\s+ld l,Interaction\.var3d\s+add \(hl\)\s+ld l,Interaction\.xh\s+ld \(hl\),a\s+ret')
$graveyardFastSpeed = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_200\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})')
$graveyardMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$graveyardJumpSound = [regex]::Match(
    $graveyardMusicSource,
    '(?m)^\s*SND_JUMP\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$graveyardFleeSound = [regex]::Match(
    $graveyardMusicSource,
    '(?m)^\s*SND_THROW\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $graveyardJump.Success -or
    [Convert]::ToInt32($graveyardJump.Groups['speed'].Value, 16) -ne 0x1c0 -or
    -not $graveyardOscillation.Success -or
    -not $graveyardFastSpeed.Success -or
    $graveyardFastSpeed.Groups['value'].Value -ne '50' -or
    -not $graveyardJumpSound.Success -or
    $graveyardJumpSound.Groups['value'].Value -ne '53' -or
    -not $graveyardFleeSound.Success -or
    $graveyardFleeSound.Groups['value'].Value -ne '51') {
    throw 'The Spirit''s Grave jump, shake RNG, speed, or sound constants changed.'
}

$graveyardNpcRows = @($npcRows | Where-Object { $_ -match '^0\t7b\t' })
if ($graveyardNpcRows.Count -ne 3 -or
    -not ($graveyardNpcRows | Where-Object { $_ -match '^0\t7b\t3c\t03\t48\t48\t' }) -or
    -not ($graveyardNpcRows | Where-Object { $_ -match '^0\t7b\t3c\t04\t48\t68\t' }) -or
    -not ($graveyardNpcRows | Where-Object { $_ -match '^0\t7b\t3f\t02\t38\t58\t' })) {
    throw 'The three positioned room 0:7b child NPC records changed.'
}
$graveyardTextIds = @(
    [Convert]::ToInt32($graveyardGreenCommands.Groups['text'].Value, 16),
    [Convert]::ToInt32($graveyardBlueCommands.Groups['text'].Value, 16),
    [Convert]::ToInt32($graveyardRedCommands.Groups['red1'].Value, 16),
    [Convert]::ToInt32($graveyardRedCommands.Groups['red2'].Value, 16),
    [Convert]::ToInt32($graveyardRedCommands.Groups['red3'].Value, 16)
)
if (($graveyardTextIds -join ',') -ne '9489,10513,9490,9491,9492' -or
    @($graveyardTextIds | Where-Object { -not $allTexts.ContainsKey($_) }).Count -ne 0 -or
    @($graveyardTextIds | Where-Object { $allTextPositions.ContainsKey($_) }).Count -ne 0) {
    throw 'The Spirit''s Grave dialogue IDs or automatic textbox positions changed.'
}

$graveyardEventRows = @(
    "# group`troom`troom-flag`tred-id`tred-subid`tred-palette`tred-initial-animation`tgreen-id`tgreen-subid`tgreen-initial-animation`tblue-id`tblue-subid`tblue-initial-animation`tgreen-initial-wait`tjump-speed-z`tjump-gravity`tjump-sound`tpost-jump-wait`tgreen-post-text-wait`tred-freeze-wait`tred-post-text-wait`tred-left-animation`tred-up-animation`tred-final-wait`tshake-frames`tflee-speed`tflee-counter`tflee-angle`tflee-animation`tflee-sound",
    (@(
        '0', '7b', '40', '3c', '03', '02', '02', '3c', '04', '00',
        '3f', '02', '02', '60', '-448', '32',
        $graveyardJumpSound.Groups['value'].Value,
        $graveyardGreenCommands.Groups['pre'].Value,
        $graveyardGreenCommands.Groups['post'].Value,
        $graveyardRedCommands.Groups['freeze1'].Value,
        $graveyardRedCommands.Groups['post1'].Value,
        $graveyardRedCommands.Groups['left'].Value,
        $graveyardRedCommands.Groups['up'].Value,
        $graveyardRedCommands.Groups['final'].Value,
        $graveyardRedCommands.Groups['shake'].Value,
        $graveyardFastSpeed.Groups['value'].Value,
        $graveyardRedCommands.Groups['flee'].Value,
        '08', '01', $graveyardFleeSound.Groups['value'].Value
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\graveyard_ghost_kids_event.tsv'),
    $graveyardEventRows)

$graveyardTextActors = @('Green', 'Blue', 'Red', 'Red', 'Red')
$graveyardTextRows = [Collections.Generic.List[string]]::new()
$graveyardTextRows.Add("# order`tactor`ttext-id`ttext-base64")
for ($index = 0; $index -lt $graveyardTextIds.Count; $index++) {
    $textId = $graveyardTextIds[$index]
    $graveyardTextRows.Add((@(
        $index.ToString(), $graveyardTextActors[$index], $textId.ToString('x4'),
        [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    ) -join "`t"))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\graveyard_ghost_kids_text.tsv'),
    $graveyardTextRows)
