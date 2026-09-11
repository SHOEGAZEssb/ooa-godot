# Past room 1:79 Ralph event after talking to Cheval. INTERAC_RALPH $37:$10
# exists only for the exact house-exit destination $17, after global flag $43,
# and before room flag $40. Source state 0 installs and immediately runs the
# script while wDisabledObjects/wMenuDisabled are $81. The two setsubstate $ff
# commands increment substate 0 -> 1 -> 2; only substate 1 creates silent,
# flickering INTERAC_PUFF $05:$81 dust every eight global updates.
$ralphAfterChevalScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$ralphAfterChevalOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'wait', 'setmusic', 'xorcfc0bit', 'setspeed', 'moveup',
    'setsubstate', 'showtext', 'movedown', 'orroomflag',
    'resetmusic', 'enableinput', 'scriptend')) {
    [void]$ralphAfterChevalOpcodes.Add($opcode)
}
$ralphAfterChevalCommands = @(Read-AssemblyCutsceneCommands `
    $ralphAfterChevalScriptPath 'ralphSubid10Script' `
    $ralphAfterChevalOpcodes 'ralphSubid0cScript')
$ralphAfterChevalExpected = @(
    @('wait', '90'),
    @('setmusic', 'MUS_RALPH'),
    @('xorcfc0bit', '0'),
    @('setspeed', 'SPEED_200'),
    @('moveup', '$18'),
    @('setsubstate', '$ff'),
    @('setspeed', 'SPEED_100'),
    @('moveup', '$20'),
    @('setspeed', 'SPEED_080'),
    @('moveup', '$20'),
    @('setsubstate', '$ff'),
    @('wait', '30'),
    @('showtext', 'TX_2a20'),
    @('wait', '30'),
    @('setspeed', 'SPEED_200'),
    @('setsubstate', '$00'),
    @('movedown', '$38'),
    @('orroomflag', '$40'),
    @('wait', '30'),
    @('resetmusic', ''),
    @('enableinput', ''),
    @('scriptend', '')
)
if ($ralphAfterChevalCommands.Count -ne $ralphAfterChevalExpected.Count) {
    throw "ralphSubid10Script expected 22 commands, parsed $($ralphAfterChevalCommands.Count)."
}
for ($index = 0; $index -lt $ralphAfterChevalExpected.Count; $index++) {
    $actualOperands = if ($null -eq $ralphAfterChevalCommands[$index].Operands) {
        ''
    } else {
        ([string]$ralphAfterChevalCommands[$index].Operands).Trim()
    }
    if ($ralphAfterChevalCommands[$index].Opcode -ne
            $ralphAfterChevalExpected[$index][0] -or
        $actualOperands -ne $ralphAfterChevalExpected[$index][1]) {
        throw "ralphSubid10Script command $index changed from " +
            "$($ralphAfterChevalExpected[$index][0]) " +
            "'$($ralphAfterChevalExpected[$index][1])' to " +
            "$($ralphAfterChevalCommands[$index].Opcode) '$actualOperands'."
    }
}
$ralphAfterChevalTargets = @{}
foreach ($command in $ralphAfterChevalCommands) {
    if (-not $ralphAfterChevalTargets.ContainsKey($command.Label)) {
        $ralphAfterChevalTargets[$command.Label] = $command.Index
    }
}
foreach ($entry in @(
    @('ralphSubid10Script', 0),
    @('ralphEndCutscene', 17))) {
    if (-not $ralphAfterChevalTargets.ContainsKey($entry[0]) -or
        $ralphAfterChevalTargets[$entry[0]] -ne $entry[1]) {
        throw "ralphSubid10Script label $($entry[0]) moved from command $($entry[1])."
    }
}

$ralphAfterChevalNativeSource = Read-ImportText (Join-Path $Disassembly `
    'object_code\ages\interactions\ralph.s')
$ralphAfterChevalInteractionDataSource = Read-ImportText (Join-Path $Disassembly `
    'data\ages\interactionData.s')
$ralphAfterChevalGlobalFlagSource = Read-ImportText (Join-Path $Disassembly `
    'constants\common\globalFlags.s')
$ralphAfterChevalWarpSource = Read-ImportText (Join-Path $Disassembly `
    'data\ages\warpSources.s')
$ralphAfterChevalWarpDestination = Read-ImportText (Join-Path $Disassembly `
    'data\ages\warpDestinations.s')
if ($ralphAfterChevalNativeSource -notmatch
        '(?ms)^ralphState0:.*?interactionInitGraphics\s+call @initSubid.*?Interaction\.enabled.*?objectMarkSolidPosition' -or
    $ralphAfterChevalNativeSource -notmatch
        '(?ms)^@initSubid10:\s+call getThisRoomFlags\s+and \$40\s+jp nz,interactionDelete\s+ld a,GLOBALFLAG_TALKED_TO_CHEVAL\s+call checkGlobalFlag\s+jp z,interactionDelete\s+ld a,\(wWarpDestPos\)\s+cp \$17\s+jp nz,interactionDelete\s+ld hl,mainScripts\.ralphSubid10Script\s+jr @setScriptAndDisableObjects' -or
    $ralphAfterChevalNativeSource -notmatch
        '(?ms)^@setScriptAndDisableObjects:\s+call interactionSetScript\s+ld a,\$81\s+ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a\s+call objectSetVisiblec1\s+jp ralphRunSubid' -or
    $ralphAfterChevalNativeSource -notmatch
        '(?ms)^ralphSubid0b:\s*^ralphSubid10:.*?\(\$cfc0\).*?call nz,ralphTurnLinkTowardSelf.*?\.dw @substate0\s+\.dw @substate1\s+\.dw ralphRunScriptWithConditionalAnimation.*?^@substate0:\s+call interactionAnimate.*?^@substate1:.*?wFrameCounter.*?and \$07.*?INTERAC_PUFF\s+inc l\s+ld \(hl\),\$81\s+ld bc,\$0804\s+call objectCopyPositionWithOffset.*?^ralphRunScriptWithConditionalAnimation:\s+call interactionRunScript\s+jp c,interactionDelete.*?Interaction\.var3f.*?jp z,interactionAnimate' -or
    $ralphAfterChevalNativeSource -notmatch
        '(?ms)^ralphTurnLinkTowardSelf:.*?w1Link\.xh.*?add \$10.*?Interaction\.xh.*?add \$10\s+sub b\s+ld b,DIR_RIGHT\s+jr nc,\+\s+ld b,DIR_LEFT\s+cpl\s+\+\s+cp \$0c\s+jr nc,\+\s+ld b,DIR_DOWN.*?w1Link\.direction.*?setLinkForceStateToState08' -or
    $ralphAfterChevalInteractionDataSource -notmatch
        '(?m)^\s*/\* \$37 \*/ m_InteractionData \$24 \$00 \$12\s*$') {
    throw 'INTERAC_RALPH $37:$10 predicate, update order, dust, facing, or graphics data changed.'
}
if ($ralphAfterChevalGlobalFlagSource -notmatch
        '(?m)^\s*GLOBALFLAG_TALKED_TO_CHEVAL\s+db ; \$43\s*$' -or
    $ralphAfterChevalWarpSource -notmatch
        '(?m)^\s*m_StandardWarp \$4 \$0f \$34 \$1 \$3\s*$' -or
    $ralphAfterChevalWarpDestination -notmatch
        '(?m)^\s*m_WarpDest \$79 \$17 \$0 \$1\s*$' -or
    $mainObjectSource -notmatch
        '(?ms)^group1Map79ObjectData:\s+obj_Interaction \$37 \$10 \$90 \$78\s+obj_End') {
    throw 'Room 1:79 Ralph placement, Cheval flag, or room 2:0f destination-$17 warp changed.'
}

$ralphAfterChevalAnimations = @(0..3 | ForEach-Object {
    Resolve-NpcAnimation 0x37 $_
})
$ralphAfterChevalGraphic = $interactionGraphics['55:16']
if ($null -eq $ralphAfterChevalGraphic) {
    $ralphAfterChevalGraphic = $interactionGraphics['55:0']
}
if (($ralphAfterChevalAnimations | Where-Object {
            [string]::IsNullOrWhiteSpace($_)
        }).Count -ne 0 -or
    $null -eq $ralphAfterChevalGraphic -or
    -not $gfxNames.ContainsKey($ralphAfterChevalGraphic.Gfx) -or
    $gfxNames[$ralphAfterChevalGraphic.Gfx] -ne 'spr_ralph_1' -or
    $ralphAfterChevalGraphic.TileBase -ne 0 -or
    $ralphAfterChevalGraphic.Palette -ne 1 -or
    $ralphAfterChevalGraphic.DefaultAnimation -ne 2) {
    throw 'Could not resolve INTERAC_RALPH $37:$10 graphics or animations $00-$03.'
}
if (-not $allTexts.ContainsKey(0x2a20)) {
    throw 'Could not resolve Ralph-after-Cheval text TX_2a20.'
}
$ralphAfterChevalMusic = Resolve-SoundConstant 'MUS_RALPH'
$ralphAfterChevalSpeed200 = Resolve-ObjectSpeed '200'
$ralphAfterChevalSpeed100 = Resolve-ObjectSpeed '100'
$ralphAfterChevalSpeed080 = Resolve-ObjectSpeed '80'
if ($ralphAfterChevalMusic -ne 0x35 -or
    $ralphAfterChevalSpeed200 -ne 0x50 -or
    $ralphAfterChevalSpeed100 -ne 0x28 -or
    $ralphAfterChevalSpeed080 -ne 0x14) {
    throw 'Ralph music or SPEED_200/SPEED_100/SPEED_080 constants changed.'
}

$ralphAfterChevalCommandRows = [Collections.Generic.List[string]]::new()
$ralphAfterChevalCommandRows.Add($cutsceneCommandHeader)
foreach ($command in $ralphAfterChevalCommands) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Opcode) {
        'wait' { $arg0 = $command.Operands }
        'setmusic' { $arg0 = $ralphAfterChevalMusic.ToString('x2') }
        'xorcfc0bit' {
            $opcode = 'nativeyield'; $payload = 'ToggleFacingBit'
        }
        'setspeed' {
            if ($command.Operands -notmatch '^SPEED_(?<speed>200|100|080)$') {
                throw "Unexpected Ralph speed '$($command.Operands)'."
            }
            $speedName = if ($Matches['speed'] -eq '080') { '80' } else {
                $Matches['speed']
            }
            $actor = 'Ralph'
            $arg0 = (Resolve-ObjectSpeed $speedName).ToString('x2')
        }
        { $_ -in @('moveup', 'movedown') } {
            if ($command.Operands -notmatch '^\$(?<counter>[0-9a-f]{2})$') {
                throw "Malformed Ralph movement '$($command.Operands)'."
            }
            $opcode = 'move'; $actor = 'Ralph'
            $direction = if ($command.Opcode -eq 'moveup') { 0 } else { 2 }
            $arg0 = if ($direction -eq 0) { '00' } else { '10' }
            $arg1 = $Matches['counter']
            $payload = $ralphAfterChevalAnimations[$direction]
        }
        'setsubstate' {
            if ($command.Operands -eq '$ff') {
                $opcode = 'nativeyield'; $payload = 'IncrementSubstate'
            } elseif ($command.Operands -eq '$00') {
                $opcode = 'nativeyield'; $payload = 'SetSubstate0'
            } else {
                throw "Unexpected Ralph substate '$($command.Operands)'."
            }
        }
        'showtext' {
            $arg0 = '2a20'; $payload = $allTexts[0x2a20]
        }
        'orroomflag' { $arg0 = '40' }
        'resetmusic' {
            $opcode = 'nativeyield'; $payload = 'ResetMusic'
        }
    }
    $ralphAfterChevalCommandRows.Add((New-CutsceneCommandRow `
        $command.Script $command.Index $command.Label $command.Line `
        $opcode $actor $arg0 $arg1 $payload))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ralph_after_cheval_commands.tsv'),
    $ralphAfterChevalCommandRows)

$ralphAfterChevalEventRows = @(
    "# group`troom`tid`tsubid`tsprite`ttile-base`tpalette`tanimation0`tanimation1`tanimation2`tanimation3`tinitial-animation`troom-flag`ttalked-global-flag`twarp-destination`tmusic`tspeed-200`tspeed-100`tspeed-080`tpuff-id`tpuff-subid`tpuff-y-offset`tpuff-x-offset`tfacing-threshold`tdisabled-objects`tmenu-disabled`tinitial-native-updates"
    (@(
        '1', '79', '37', '10', 'spr_ralph_1', '00', '01',
        $ralphAfterChevalAnimations[0], $ralphAfterChevalAnimations[1],
        $ralphAfterChevalAnimations[2], $ralphAfterChevalAnimations[3],
        '02', '40', '43', '17', '35', '50', '28', '14',
        '05', '81', '08', '04', '0c', '81', '81', '1'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ralph_after_cheval_event.tsv'),
    $ralphAfterChevalEventRows)

# Past room 1:97 Ralph event after giving Rafton the Cheval Rope.
# INTERAC_RALPH $37:$03 owns an eight-substate native entrance sequence before
# installing ralphSubid03Script. State 0 does not jump into the state-1 handler,
# so the first $78 counter decrement occurs on the following object update.
$ralphAfterRaftonScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$ralphAfterRaftonOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'wait', 'setanimation', 'showtext', 'setspeed', 'moveup',
    'playsound', 'orroomflag', 'enableinput', 'scriptend')) {
    [void]$ralphAfterRaftonOpcodes.Add($opcode)
}
$ralphAfterRaftonCommands = @(Read-AssemblyCutsceneCommands `
    $ralphAfterRaftonScriptPath 'ralphSubid03Script' `
    $ralphAfterRaftonOpcodes 'ralphSubid0bScript')
$ralphAfterRaftonExpected = @(
    @('wait', '6'),
    @('setanimation', '$02'),
    @('wait', '10'),
    @('showtext', 'TX_2a0b'),
    @('wait', '20'),
    @('setanimation', '$00'),
    @('wait', '20'),
    @('showtext', 'TX_2a06'),
    @('wait', '10'),
    @('setspeed', 'SPEED_200'),
    @('moveup', '$44'),
    @('playsound', 'SNDCTRL_FAST_FADEOUT'),
    @('wait', '30'),
    @('orroomflag', '$40'),
    @('enableinput', ''),
    @('scriptend', '')
)
if ($ralphAfterRaftonCommands.Count -ne $ralphAfterRaftonExpected.Count) {
    throw "ralphSubid03Script expected 16 commands, parsed $($ralphAfterRaftonCommands.Count)."
}
for ($index = 0; $index -lt $ralphAfterRaftonExpected.Count; $index++) {
    $actualOperands = if ($null -eq $ralphAfterRaftonCommands[$index].Operands) {
        ''
    } else {
        ([string]$ralphAfterRaftonCommands[$index].Operands).Trim()
    }
    if ($ralphAfterRaftonCommands[$index].Opcode -ne
            $ralphAfterRaftonExpected[$index][0] -or
        $actualOperands -ne $ralphAfterRaftonExpected[$index][1]) {
        throw "ralphSubid03Script command $index changed from " +
            "$($ralphAfterRaftonExpected[$index][0]) " +
            "'$($ralphAfterRaftonExpected[$index][1])' to " +
            "$($ralphAfterRaftonCommands[$index].Opcode) '$actualOperands'."
    }
}

$ralphAfterRaftonNativeSource = Read-ImportText (Join-Path $Disassembly `
    'object_code\ages\interactions\ralph.s')
$ralphAfterRaftonInteractionDataSource = Read-ImportText (Join-Path $Disassembly `
    'data\ages\interactionData.s')
$ralphAfterRaftonGlobalFlagSource = Read-ImportText (Join-Path $Disassembly `
    'constants\common\globalFlags.s')
if ($ralphAfterRaftonNativeSource -notmatch
        '(?ms)^@initSubid03:\s+ld a,GLOBALFLAG_GAVE_ROPE_TO_RAFTON\s+call checkGlobalFlag\s+jp z,interactionDelete\s+call getThisRoomFlags\s+bit 6,a\s+jp nz,interactionDelete.*?ld a,\$01\s+ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a\s+ld a,\$03\s+call interactionSetAnimation.*?Interaction\.counter1\s+ld \(hl\),\$78\s+ld l,Interaction\.direction\s+ld \(hl\),\$01\s+jp objectSetVisiblec2' -or
    $ralphAfterRaftonNativeSource -notmatch
        '(?ms)^ralphSubid03:.*?\.dw @substate0.*?\.dw @substate8.*?^@substate0:\s+call interactionDecCounter1\s+jr nz,\+\+\s+ld \(hl\),\$1e\s+ld a,\$02\s+call interactionSetAnimation\s+jp interactionIncSubstate.*?wFrameCounter.*?and \$0f.*?Interaction\.direction.*?xor \$02.*?interactionSetAnimation.*?^@substate1:\s+call interactionDecCounter1\s+ret nz\s+call interactionIncSubstate\s+jp startJump.*?^@substate2:\s+call interactionAnimate\s+ld c,\$20\s+call objectUpdateSpeedZ_paramC\s+ret nz\s+call interactionIncSubstate\s+ld l,Interaction\.counter1\s+ld \(hl\),\$0a' -or
    $ralphAfterRaftonNativeSource -notmatch
        '(?ms)^@substate3:\s+call interactionDecCounter1\s+ret nz\s+ld \(hl\),\$1e\s+call interactionIncSubstate\s+ld bc,TX_2a0a\s+jp showText.*?^@substate4:\s+call interactionDecCounter1IfTextNotActive\s+ret nz\s+ld \(hl\),\$30\s+call interactionIncSubstate.*?Interaction\.angle\s+ld \(hl\),\$10.*?Interaction\.speed\s+ld \(hl\),SPEED_100\s+ld a,\$02\s+jp interactionSetAnimation.*?^@substate5:\s+call interactionAnimate2Times\s+call interactionDecCounter1\s+jp nz,objectApplySpeed\s+ld \(hl\),\$06\s+jp interactionIncSubstate' -or
    $ralphAfterRaftonNativeSource -notmatch
        '(?ms)^@substate6:\s+call interactionDecCounter1\s+ret nz\s+ld \(hl\),\$0a.*?w1Link\.xh.*?Interaction\.xh\s+sub \(hl\)\s+jr z,@startScript\s+jr c,@@moveLeft.*?ld b,\$08\s+ld c,DIR_RIGHT.*?cpl\s+inc a\s+ld b,\$18\s+ld c,DIR_LEFT.*?Interaction\.counter1\s+ld \(hl\),a.*?Interaction\.angle\s+ld \(hl\),b\s+ld a,c\s+jp interactionSetAnimation.*?^@substate7:\s+call interactionAnimate2Times\s+call interactionDecCounter1\s+jp nz,objectApplySpeed\s*^@startScript:\s+call interactionIncSubstate\s+ld hl,mainScripts\.ralphSubid03Script\s+jp interactionSetScript.*?^@substate8:\s+call ralphAnimateBasedOnSpeedAndRunScript\s+ret nc\s+ld a,MUS_OVERWORLD_PAST\s+ld \(wActiveMusic2\),a\s+ld \(wActiveMusic\),a\s+call playSound\s+jp interactionDelete' -or
    $ralphAfterRaftonNativeSource -notmatch
        '(?ms)^startJump:\s+ld bc,-\$1c0\s+call objectSetSpeedZ\s+ld a,SND_JUMP\s+jp playSound' -or
    $ralphAfterRaftonInteractionDataSource -notmatch
        '(?m)^\s*/\* \$37 \*/ m_InteractionData \$24 \$00 \$12\s*$' -or
    $ralphAfterRaftonGlobalFlagSource -notmatch
        '(?m)^\s*GLOBALFLAG_GAVE_ROPE_TO_RAFTON\s+db ; \$15\s*$' -or
    $musicConstantSource -notmatch
        '(?m)^\.define SNDCTRL_FAST_FADEOUT\s+\$fa\s*$' -or
    $mainObjectSource -notmatch
        '(?ms)^group1Map97ObjectData:\s+obj_Interaction \$37 \$03 \$38 \$38\s+obj_End') {
    throw 'Room 1:97 INTERAC_RALPH $37:$03 predicate, native substates, placement, or graphics data changed.'
}

$ralphAfterRaftonAnimations = @(0..3 | ForEach-Object {
    Resolve-NpcAnimation 0x37 $_
})
$ralphAfterRaftonGraphic = $interactionGraphics['55:16']
if ($null -eq $ralphAfterRaftonGraphic) {
    $ralphAfterRaftonGraphic = $interactionGraphics['55:0']
}
if (($ralphAfterRaftonAnimations | Where-Object {
            [string]::IsNullOrWhiteSpace($_)
        }).Count -ne 0 -or
    $null -eq $ralphAfterRaftonGraphic -or
    -not $gfxNames.ContainsKey($ralphAfterRaftonGraphic.Gfx) -or
    $gfxNames[$ralphAfterRaftonGraphic.Gfx] -ne 'spr_ralph_1' -or
    $ralphAfterRaftonGraphic.TileBase -ne 0 -or
    $ralphAfterRaftonGraphic.Palette -ne 1 -or
    $ralphAfterRaftonGraphic.DefaultAnimation -ne 2) {
    throw 'Could not resolve INTERAC_RALPH $37:$03 graphics or animations $00-$03.'
}
foreach ($textId in @(0x2a0a, 0x2a0b, 0x2a06)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Ralph-after-Rafton text TX_$($textId.ToString('x4'))."
    }
}
$ralphAfterRaftonSpeed100 = Resolve-ObjectSpeed '100'
$ralphAfterRaftonSpeed200 = Resolve-ObjectSpeed '200'
$ralphAfterRaftonJumpSound = Resolve-SoundConstant 'SND_JUMP'
$ralphAfterRaftonFadeSound = 0xfa
$ralphAfterRaftonCompletionMusic = Resolve-SoundConstant 'MUS_OVERWORLD_PAST'
if ($ralphAfterRaftonSpeed100 -ne 0x28 -or
    $ralphAfterRaftonSpeed200 -ne 0x50 -or
    $ralphAfterRaftonJumpSound -ne 0x53 -or
    $ralphAfterRaftonFadeSound -ne 0xfa -or
    $ralphAfterRaftonCompletionMusic -ne 0x04) {
    throw 'Ralph-after-Rafton speed, jump, fade, or completion-music constants changed.'
}

$ralphAfterRaftonCommandRows = [Collections.Generic.List[string]]::new()
$ralphAfterRaftonCommandRows.Add($cutsceneCommandHeader)
foreach ($command in $ralphAfterRaftonCommands) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Opcode) {
        'wait' { $arg0 = $command.Operands }
        'setanimation' {
            if ($command.Operands -notmatch '^\$(?<animation>00|02)$') {
                throw "Unexpected Ralph-after-Rafton animation '$($command.Operands)'."
            }
            $actor = 'Ralph'
            $arg0 = $Matches['animation']
            $payload = $ralphAfterRaftonAnimations[
                [Convert]::ToInt32($Matches['animation'], 16)]
        }
        'showtext' {
            if ($command.Operands -notmatch '^TX_(?<text>[0-9a-f]{4})$') {
                throw "Malformed Ralph-after-Rafton text '$($command.Operands)'."
            }
            $textId = [Convert]::ToInt32($Matches['text'], 16)
            $arg0 = $Matches['text']
            $payload = $allTexts[$textId]
        }
        'setspeed' {
            $actor = 'Ralph'
            $arg0 = $ralphAfterRaftonSpeed200.ToString('x2')
        }
        'moveup' {
            if ($command.Operands -notmatch '^\$(?<counter>[0-9a-f]{2})$') {
                throw "Malformed Ralph-after-Rafton movement '$($command.Operands)'."
            }
            $opcode = 'move'; $actor = 'Ralph'; $arg0 = '00'
            $arg1 = $Matches['counter']; $payload = $ralphAfterRaftonAnimations[0]
        }
        'playsound' { $arg0 = $ralphAfterRaftonFadeSound.ToString('x2') }
        'orroomflag' { $arg0 = '40' }
    }
    $ralphAfterRaftonCommandRows.Add((New-CutsceneCommandRow `
        $command.Script $command.Index $command.Label $command.Line `
        $opcode $actor $arg0 $arg1 $payload))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ralph_after_rafton_commands.tsv'),
    $ralphAfterRaftonCommandRows)

$ralphAfterRaftonEventRows = @(
    "# group`troom`tid`tsubid`tsprite`ttile-base`tpalette`tanimation0`tanimation1`tanimation2`tanimation3`tdefault-animation`tinitial-animation`tinitial-direction`troom-flag`trequired-global-flag`tdisabled-objects`tmenu-disabled`tlook-counter`tlook-frame-mask`tlook-direction-xor`tpost-look-wait`tjump-speed-z`tjump-gravity`tjump-sound`tlanding-wait`tnative-text-id`tnative-text-base64`tpost-text-wait`tapproach-counter`tspeed-100`tdown-angle`talign-wait`tspeed-200`texit-counter`tfade-sound`tcompletion-music`tinitial-native-updates"
    (@(
        '1', '97', '37', '03', 'spr_ralph_1', '00', '01',
        $ralphAfterRaftonAnimations[0], $ralphAfterRaftonAnimations[1],
        $ralphAfterRaftonAnimations[2], $ralphAfterRaftonAnimations[3],
        '02', '03', '01', '40', '15', '01', '01', '78', '0f', '02',
        '1e', '-448', '20', '53', '0a', '2a0a',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x2a0a]),
        '1e', '30', '28', '10', '06', '50', '44', 'fa', '04', '0'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ralph_after_rafton_event.tsv'),
    $ralphAfterRaftonEventRows)
