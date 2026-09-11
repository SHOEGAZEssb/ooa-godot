# Room 1:75's pre-Black Tower sequence is seven synchronized interaction
# lanes. Export the original per-actor scripts independently; runtime advances
# them in placement order and preserves their cfc0/cfd0 gates.
$preBlackTowerMainScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$preBlackTowerHelperScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$preBlackTowerOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'wait', 'showtext', 'writememory', 'setspeed', 'moveup',
    'moveright', 'movedown', 'moveleft', 'setanimation',
    'checkmemoryeq', 'checkobjectbyteeq', 'applyspeed', 'asm15',
    'xorcfc0bit', 'checkcfc0bit', 'spawninteraction',
    'writeobjectword', 'scriptend')) {
    [void]$preBlackTowerOpcodes.Add($opcode)
}

$preBlackTowerActorIds = @{
    'Ralph' = 0x37
    'Impa' = 0x31
    'Nayru' = 0x36
    'Zelda' = 0xad
}
$preBlackTowerDirection = @{
    'DIR_UP' = 0
    'DIR_RIGHT' = 1
    'DIR_DOWN' = 2
    'DIR_LEFT' = 3
}
$preBlackTowerMovement = @{
    'moveup' = @(0x00, 0)
    'moveright' = @(0x08, 1)
    'movedown' = @(0x10, 2)
    'moveleft' = @(0x18, 3)
}

function Convert-PreBlackTowerHex([string]$value) {
    $trimmed = $value.Trim()
    if ($trimmed -match '^\$(?<hex>[0-9a-f]+)$') {
        return [Convert]::ToInt32($Matches['hex'], 16)
    }
    return [Convert]::ToInt32($trimmed, 10)
}

function Export-PreBlackTowerLane {
    param(
        [string]$script,
        [string]$actor,
        [string]$path,
        [string]$nextLabel,
        [string]$outputName)

    $parsed = @(Read-AssemblyCutsceneCommands `
        $path $script $preBlackTowerOpcodes $nextLabel)
    if ($parsed[-1].Opcode -ne 'scriptend') {
        throw "$path`:$($parsed[-1].Line): $script does not terminate in scriptend."
    }

    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
    foreach ($command in $parsed) {
        $opcode = [string]$command.Opcode
        $operands = [string]$command.Operands
        $runtimeOpcode = $opcode
        $runtimeActor = ''
        $arg0 = ''
        $arg1 = ''
        $payload = ''

        switch ($opcode) {
            'wait' {
                $arg0 = (Convert-PreBlackTowerHex $operands).ToString()
            }
            'showtext' {
                if ($operands -notmatch '^TX_(?<text>[0-9a-f]{4})$') {
                    throw "$path`:$($command.Line): unsupported showtext operand '$operands'."
                }
                $textId = [Convert]::ToInt32($Matches['text'], 16)
                if (-not $allTexts.ContainsKey($textId)) {
                    throw "$path`:$($command.Line): missing TX_$($Matches['text'])."
                }
                $arg0 = $Matches['text']
                $payload = $allTexts[$textId]
            }
            'writememory' {
                $parts = $operands -split '\s*,\s*'
                if ($parts.Count -ne 2) {
                    throw "$path`:$($command.Line): malformed writememory '$operands'."
                }
                $value = if ($preBlackTowerDirection.ContainsKey($parts[1])) {
                    $preBlackTowerDirection[$parts[1]]
                } else { Convert-PreBlackTowerHex $parts[1] }
                $arg0 = ([int]$value).ToString('x2')
                $payload = switch -Regex ($parts[0]) {
                    'wTmpcfc0\.genericCutscene\.cfd0' { 'SharedSignal'; break }
                    'w1Link\.direction' { 'PlayerDirection'; break }
                    default { throw "$path`:$($command.Line): unsupported writememory binding '$($parts[0])'." }
                }
            }
            'setspeed' {
                if ($operands -notmatch '^SPEED_(?<speed>[0-9a-f]+)$') {
                    throw "$path`:$($command.Line): unsupported speed '$operands'."
                }
                $runtimeActor = $actor
                $speedName = $Matches['speed'].TrimStart('0')
                if ([string]::IsNullOrEmpty($speedName)) { $speedName = '0' }
                $arg0 = (Resolve-ObjectSpeed $speedName).ToString('x2')
            }
            { $preBlackTowerMovement.ContainsKey($_) } {
                $movement = $preBlackTowerMovement[$opcode]
                $animation = [int]$movement[1]
                $runtimeOpcode = 'move'
                $runtimeActor = $actor
                $arg0 = ([int]$movement[0]).ToString('x2')
                $arg1 = (Convert-PreBlackTowerHex $operands).ToString('x2')
                $payload = Resolve-NpcAnimation $preBlackTowerActorIds[$actor] $animation
                if (-not $payload) {
                    throw "$path`:$($command.Line): missing $actor movement animation $animation."
                }
            }
            'setanimation' {
                $animation = Convert-PreBlackTowerHex $operands
                $runtimeActor = $actor
                $arg0 = $animation.ToString('x2')
                $payload = Resolve-NpcAnimation $preBlackTowerActorIds[$actor] $animation
                if (-not $payload) {
                    throw "$path`:$($command.Line): missing $actor animation $animation."
                }
            }
            'checkmemoryeq' {
                $parts = $operands -split '\s*,\s*'
                if ($parts.Count -ne 2 -or $parts[0] -ne 'wTmpcfc0.genericCutscene.cfd0') {
                    throw "$path`:$($command.Line): unsupported checkmemoryeq '$operands'."
                }
                $arg0 = (Convert-PreBlackTowerHex $parts[1]).ToString('x2')
                $payload = 'SharedSignal'
            }
            'checkobjectbyteeq' {
                $parts = $operands -split '\s*,\s*'
                if ($parts.Count -ne 2) {
                    throw "$path`:$($command.Line): malformed checkobjectbyteeq '$operands'."
                }
                $runtimeOpcode = 'checkmemoryeq'
                $arg0 = (Convert-PreBlackTowerHex $parts[1]).ToString('x2')
                $payload = switch ($parts[0]) {
                    'Interaction.substate' { "${actor}Substate" }
                    'Interaction.var38' { "${actor}Var38" }
                    default { throw "$path`:$($command.Line): unsupported object binding '$($parts[0])'." }
                }
            }
            'applyspeed' {
                $runtimeActor = $actor
                $arg0 = (Convert-PreBlackTowerHex $operands).ToString('x2')
            }
            'asm15' {
                if ($operands -match '^setGlobalFlag,\s*GLOBALFLAG_RALPH_ENTERED_BLACK_TOWER$') {
                    $runtimeOpcode = 'setglobalflag'
                    $arg0 = '45'
                } elseif ($operands -match '^scriptHelp\.ralph_createExclamationMarkShiftedRight,\s*\$1e$') {
                    $runtimeOpcode = 'native'
                    $payload = 'CreateLinkedExclamation'
                } else {
                    throw "$path`:$($command.Line): unsupported asm15 handler '$operands'."
                }
            }
            'xorcfc0bit' {
                $bit = Convert-PreBlackTowerHex $operands
                $runtimeOpcode = 'writememory'
                $arg0 = (1 -shl $bit).ToString('x2')
                $payload = 'ToggleSharedBit'
            }
            'checkcfc0bit' {
                $bit = Convert-PreBlackTowerHex $operands
                $runtimeOpcode = 'checkmemoryeq'
                $arg0 = '01'
                $payload = "SharedBit$bit"
            }
            'spawninteraction' {
                if ($operands -ne 'INTERAC_NAYRU, $09, $f8, $48') {
                    throw "$path`:$($command.Line): unexpected spawninteraction '$operands'."
                }
                $runtimeOpcode = 'nativeyield'
                $payload = 'SpawnNayru09'
            }
            'writeobjectword' {
                if ($operands -ne 'Interaction.speedZ, -$180') {
                    throw "$path`:$($command.Line): unexpected writeobjectword '$operands'."
                }
                $runtimeOpcode = 'nativeyield'
                $payload = "Begin${actor}Jump"
            }
            'scriptend' { }
            default {
                throw "$path`:$($command.Line): unsupported converted opcode '$opcode'."
            }
        }

        $rows.Add((New-CutsceneCommandRow `
            $script $command.Index $command.Label $command.Line `
            $runtimeOpcode $runtimeActor $arg0 $arg1 $payload))
    }
    Write-CutsceneGeneratedTable(
        (Join-Path $destination "cutscenes\$outputName"),
        $rows)
}

$preBlackTowerLaneSpecs = @(
    @('ralphSubid0aScript_unlinked', 'Ralph', $preBlackTowerMainScriptPath, 'ralphSubid0aScript_linked', 'pre_black_tower_ralph_unlinked.tsv'),
    @('ralphSubid0aScript_linked', 'Ralph', $preBlackTowerMainScriptPath, 'ralphSubid0bScript', 'pre_black_tower_ralph_linked.tsv'),
    @('impaScript4', 'Impa', $preBlackTowerHelperScriptPath, 'impaScript5', 'pre_black_tower_impa_unlinked.tsv'),
    @('impaScript5', 'Impa', $preBlackTowerHelperScriptPath, 'impaScript7', 'pre_black_tower_impa_linked.tsv'),
    @('nayruScript09', 'Nayru', $preBlackTowerMainScriptPath, 'nayruScript0a', 'pre_black_tower_nayru_unlinked.tsv'),
    @('nayruScript0a', 'Nayru', $preBlackTowerMainScriptPath, 'nayruScript10', 'pre_black_tower_nayru_linked.tsv'),
    @('zeldaSubid04Script', 'Zelda', $preBlackTowerMainScriptPath, 'zeldaSubid05Script', 'pre_black_tower_zelda_linked.tsv')
)
foreach ($lane in $preBlackTowerLaneSpecs) {
    Export-PreBlackTowerLane @lane
}

$preBlackTowerExclamationGraphic = $interactionGraphics['159:0']
$preBlackTowerExclamationAnimation = Resolve-NpcAnimation 0x9f 0
if ($null -eq $preBlackTowerExclamationGraphic -or
    -not $gfxNames.ContainsKey($preBlackTowerExclamationGraphic.Gfx) -or
    -not $preBlackTowerExclamationAnimation) {
    throw 'Could not resolve the pre-Black Tower exclamation effect graphics.'
}
$preBlackTowerEventRows = @(
    "# group`troom`tmaku-seed`tcompletion-flag`tralph-entered-flag`tclink-sound`tgravity`tralph-id`tralph-subid`timpa-id`timpa-unlinked-subid`timpa-linked-subid`tnayru-id`tnayru-linked-subid`tnayru-spawned-subid`tzelda-id`tzelda-subid`teffect-id`teffect-subid`teffect-sprite`teffect-tile-base`teffect-palette`teffect-animation",
    (@(
        '1', '75', '36', '33', '45', '50', '20', '37', '0a', '31', '04', '05',
        '36', '0a', '09', 'ad', '04', '9f', '00',
        $gfxNames[$preBlackTowerExclamationGraphic.Gfx],
        $preBlackTowerExclamationGraphic.TileBase.ToString(),
        $preBlackTowerExclamationGraphic.Palette.ToString(),
        $preBlackTowerExclamationAnimation
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\pre_black_tower_event.tsv'),
    $preBlackTowerEventRows)

# Room 1:86's guard starts stage 0 of CUTSCENE_BLACK_TOWER_EXPLANATION, then
# resumes at @cutsceneAftermath after the cutscene's same-room transition $0c.
$blackTowerScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$blackTowerScriptSource = Read-ImportText $blackTowerScriptPath
$blackTowerScriptMatch = [regex]::Match(
    $blackTowerScriptSource,
    '(?ms)^hardhatWorkerSubid02Script:(?<body>.*?)(?=^hardhatWorkerSubid03Script:)')
if (-not $blackTowerScriptMatch.Success) {
    throw 'Could not locate hardhatWorkerSubid02Script for room 1:86.'
}
$blackTowerBodyStart = $blackTowerScriptMatch.Groups['body'].Index
$blackTowerBodyEnd = $blackTowerBodyStart + $blackTowerScriptMatch.Groups['body'].Length
function Get-BlackTowerGuardLine([string]$pattern, [int]$occurrence = 0) {
    return Find-CutsceneCommandSourceLine `
        $blackTowerScriptSource $blackTowerBodyStart $blackTowerBodyEnd `
        $pattern 'hardhatWorkerSubid02Script' $occurrence
}
foreach ($textId in @(0x1003, 0x1004, 0x1005, 0x1006)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Room 1:86 is missing TX_$($textId.ToString('x4'))."
    }
}
$blackTowerRightAnimation = Resolve-NpcAnimation 0x58 1
$blackTowerMoveSpeed = Resolve-ObjectSpeed '80'
if (-not $blackTowerRightAnimation -or $blackTowerMoveSpeed -ne 0x14) {
    throw 'Could not resolve the hardhat worker right-facing animation or SPEED_080 raw value.'
}

$blackTowerFirstRows = [Collections.Generic.List[string]]::new()
$blackTowerFirstRows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
$firstSpec = @(
    @('disableinput', '', '', '', '', '^\s*disableinput\s*$', 0),
    @('showtext', '', '1003', '', $allTexts[0x1003], '^\s*showtextlowindex\s+<TX_1003\s*$', 0),
    @('wait', '', '30', '', '', '^\s*wait\s+30\s*$', 0),
    @('orroomflag', '', '40', '', '', '^\s*orroomflag\s+\$40\s*$', 0),
    @('native', '', '', '', 'StoreLink', '^\s*asm15\s+hardhatWorker_storeLinkVarsSomewhere\s*$', 0),
    @('writememory', '', '00', '', 'CutsceneStage', '^\s*writememory\s+wGenericCutscene\.cbb8,\s*\$00\s*$', 0),
    @('writememory', '', '08', '', 'CutsceneTrigger', '^\s*writememory\s+wCutsceneTrigger,\s*CUTSCENE_BLACK_TOWER_EXPLANATION\s*$', 0),
    @('scriptend', '', '', '', '', '^\s*scriptend\s*$', 0)
)
for ($index = 0; $index -lt $firstSpec.Count; $index++) {
    $spec = $firstSpec[$index]
    $blackTowerFirstRows.Add((New-CutsceneCommandRow `
        'hardhatWorkerSubid02Script:first' $index 'hardhatWorkerSubid02Script' `
        (Get-BlackTowerGuardLine $spec[5] ([int]$spec[6])) `
        $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\black_tower_guard_first.tsv'),
    $blackTowerFirstRows)

$blackTowerAfterRows = [Collections.Generic.List[string]]::new()
$blackTowerAfterRows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
$afterSpec = @(
    @('disableinput', '', '', '', '', '^\s*disableinput\s*$', 1),
    @('native', '', '', '', 'TurnToFaceLink', '^\s*asm15\s+turnToFaceLink\s*$', 0),
    @('gate', '', '', '', 'palette-fade-done', '^\s*checkpalettefadedone\s*$', 0),
    @('wait', '', '60', '', '', '^\s*wait\s+60\s*$', 0),
    @('showtext', '', '1006', '', $allTexts[0x1006], '^\s*showtextlowindex\s+<TX_1006\s*$', 0),
    @('native', '', '', '', 'MoveLinkAway', '^\s*asm15\s+hardhatWorker_moveLinkAway\s*$', 0),
    @('writeobjectbyte', 'Guard', '38', '01', '', '^\s*writeobjectbyte\s+Interaction\.var38,\s*\$01\s*$', 0),
    @('wait', '', '30', '', '', '^\s*wait\s+30\s*$', 1),
    @('setspeed', 'Guard', ($blackTowerMoveSpeed.ToString('x2')), '', '', '^\s*setspeed\s+SPEED_080\s*$', 0),
    @('move', 'Guard', '08', '21', $blackTowerRightAnimation, '^\s*moveright\s+\$21\s*$', 0),
    @('writeobjectbyte', 'Guard', '38', '00', '', '^\s*writeobjectbyte\s+Interaction\.var38,\s*\$00\s*$', 0),
    @('wait', '', '30', '', '', '^\s*wait\s+30\s*$', 2),
    @('orroomflag', '', '80', '', '', '^\s*orroomflag\s+\$80\s*$', 0),
    @('writememory', '', '00', '', 'SimulatedInput', '^\s*writememory\s+wUseSimulatedInput,\s*\$00\s*$', 0),
    @('enableinput', '', '', '', '', '^\s*enableinput\s*$', 0),
    @('scriptend', '', '', '', '', '^\s*enableinput\s*$', 0)
)
for ($index = 0; $index -lt $afterSpec.Count; $index++) {
    $spec = $afterSpec[$index]
    $blackTowerAfterRows.Add((New-CutsceneCommandRow `
        'hardhatWorkerSubid02Script:aftermath' $index '@cutsceneAftermath' `
        (Get-BlackTowerGuardLine $spec[5] ([int]$spec[6])) `
        $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\black_tower_guard_aftermath.tsv'),
    $blackTowerAfterRows)

$blackTowerCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
if ($blackTowerCutsceneSource -notmatch '(?ms)^blackTowerExplanationCutsceneHandler:.*?^@@table_6625:\s+\.db GFXH_BLACK_TOWER_STAGE_1_LAYOUT, GFXH_BLACK_TOWER_BASE\s+\.db GFXH_BLACK_TOWER_STAGE_2_LAYOUT, GFXH_BLACK_TOWER_MIDDLE' -or
    $blackTowerCutsceneSource -notmatch '(?ms)^func_6ef7:.*?and \$1f.*?call getRandomNumber.*?and \$07.*?SND_LIGHTNING' -or
    $blackTowerCutsceneSource -notmatch '(?ms)^func_6f44:.*?^@cbb8_00:.*?oamData_714c.*?^@cbb8_01:.*?oamData_718d.*?oamData_71ce') {
    throw 'Black Tower explanation stage-0/stage-1 presentation changed.'
}
$blackTowerOamSource = Read-ImportText (Join-Path $Disassembly 'ages.s')
function Export-BlackTowerOam(
    [string]$label,
    [string]$nextLabel,
    [int]$expectedCount,
    [string]$destinationName) {
    $match = [regex]::Match(
        $blackTowerOamSource,
        "(?ms)^$($label):\s+\.db \`$$($expectedCount.ToString('x2'))" +
        "(?<body>.*?)(?=^$($nextLabel):)")
    $entries = [regex]::Matches(
        $match.Groups['body'].Value,
        '(?m)^\s*\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})\s*$')
    if (-not $match.Success -or $entries.Count -ne $expectedCount) {
        throw "Could not import Black Tower OAM data $label."
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# index`ty`tx`ttile`tflags`tsource")
    for ($index = 0; $index -lt $entries.Count; $index++) {
        $entry = $entries[$index]
        $rows.Add(
            "$index`t$($entry.Groups['y'].Value)`t$($entry.Groups['x'].Value)`t$($entry.Groups['tile'].Value)`t$($entry.Groups['flags'].Value)`tages.s:$label")
    }
    Write-CutsceneGeneratedTable(
        (Join-Path $destination "cutscenes\$destinationName"),
        $rows)
}
Export-BlackTowerOam 'oamData_714c' 'oamData_718d' 16 `
    'black_tower_stage_0_oam.tsv'
Export-BlackTowerOam 'oamData_718d' 'oamData_71ce' 16 `
    'black_tower_stage_1_tower_oam.tsv'
Export-BlackTowerOam 'oamData_71ce' 'oamData_71f7' 10 `
    'black_tower_stage_1_workers_oam.tsv'

foreach ($asset in @(
    @('map_black_tower_stage_1.bin', 'map_black_tower_stage_1.bin'),
    @('flg_black_tower_stage_1.bin', 'flags_black_tower_stage_1.bin'),
    @('map_black_tower_stage_2.bin', 'map_black_tower_stage_2.bin'),
    @('flg_black_tower_stage_2.bin', 'flags_black_tower_stage_2.bin'),
    @('map_black_tower_middle.bin', 'map_black_tower_middle.bin'),
    @('flg_black_tower_middle.bin', 'flags_black_tower_middle.bin'),
    @('map_black_tower_base.bin', 'map_black_tower_base.bin'),
    @('flg_black_tower_base.bin', 'flags_black_tower_base.bin'),
    @('gfx_black_tower_scene_1.png', 'gfx_black_tower_scene_1.png'),
    @('gfx_black_tower_scene_2.png', 'gfx_black_tower_scene_2.png'),
    @('gfx_black_tower_scene_3.png', 'gfx_black_tower_scene_3.png'),
    @('gfx_black_tower_scene_4.png', 'gfx_black_tower_scene_4.png'),
    @('spr_black_tower_scene.png', 'spr_black_tower_scene.png'))) {
    Copy-GeneratedFile `
        "gfx_compressible\ages\$($asset[0])" `
        "cutscenes\$($asset[1])"
}
Export-PaletteBlock 'paletteData57e0' 28 'cutscenes\black_tower_bg_palette.bin'
Export-PaletteBlock 'paletteData5818' 32 'cutscenes\black_tower_sprite_palette.bin'

$blackTowerEventRows = @(
    "# group`troom`tguard-id`tguard-subid`tessence-mask`titem-flag`taftermath-flag`tcomplete-flag`tinitial-y`tinitial-x`tcompleted-y`tcompleted-x`tmove-speed`tmove-counter`tscreen-offset-y`tintro-wait`tpost-wait`tsource-transition`tdestination-transition`texplanation-text-id`texplanation-text-base64",
    (@(
        '1', '86', '58', '02', '08', '20', '40', '80', '38', '48', '38', '58',
        $blackTowerMoveSpeed.ToString('x2'), '21', '70', '60', '60', '04', '0c', '1005',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x1005])
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\black_tower_entrance_event.tsv'),
    $blackTowerEventRows)

# Room $1:$76 contains INTERAC_MISCELLANEOUS_2 $dc:$10 rather than a visible
# NPC. It opens the two entrance metatiles and arms a collision rectangle that
# selects one of two hardcoded Black Tower rooms from this room's bit $01.
# Keep the placement, state-machine inputs, flag predicate, raw warp bytes, and
# sound tied to their disassembly definitions instead of encoding them in the
# runtime controller.
$towerDoorObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$towerDoorPlacement = [regex]::Match(
    $towerDoorObjectSource,
    '(?ms)^group(?<group>1)Map(?<room>76)ObjectData:\s*' +
    'obj_Interaction \$(?<id>[0-9a-f]{2}) \$(?<subid>[0-9a-f]{2}) ' +
    '\$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s*obj_End')
if (-not $towerDoorPlacement.Success -or
    [Convert]::ToInt32($towerDoorPlacement.Groups['group'].Value, 16) -ne 1 -or
    [Convert]::ToInt32($towerDoorPlacement.Groups['room'].Value, 16) -ne 0x76 -or
    [Convert]::ToInt32($towerDoorPlacement.Groups['id'].Value, 16) -ne 0xdc -or
    [Convert]::ToInt32($towerDoorPlacement.Groups['subid'].Value, 16) -ne 0x10) {
    throw 'Could not resolve room 1:76 INTERAC_MISCELLANEOUS_2 $dc:$10 placement.'
}

$towerDoorSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$towerDoorHandler = [regex]::Match(
    $towerDoorSource,
    '(?ms)^interactiondc_subid10:(?<body>.*?)(?=^interactiondc_subid11:)')
if (-not $towerDoorHandler.Success) {
    throw 'Could not resolve interactiondc_subid10.'
}
$towerDoorBody = $towerDoorHandler.Groups['body'].Value
$towerDoorClear = [regex]::Match(
    $towerDoorBody,
    'ld hl,wRoomLayout\+\$(?<position>[0-9a-f]{2})\s*xor a\s*ldi \(hl\),a\s*ld \(hl\),a')
$towerDoorRadii = [regex]::Match(
    $towerDoorBody, 'ld bc,\$(?<y>[0-9a-f]{2})(?<x>[0-9a-f]{2})\s*call objectSetCollideRadii')
$towerDoorFlag = [regex]::Match(
    $towerDoorBody, 'call getThisRoomFlags\s*and \$(?<mask>[0-9a-f]{2})')
$towerDoorWarps = [regex]::Matches(
    $towerDoorBody,
    '(?m)^@warp[12]:\s*\r?\n\s*m_HardcodedWarpA ROOM_AGES_(?<group>[0-7])(?<room>[0-9a-f]{2}), ' +
    '\$(?<transition>[0-9a-f]{2}), \$(?<position>[0-9a-f]{2}), \$(?<transition2>[0-9a-f]{2})')
if (-not $towerDoorClear.Success -or -not $towerDoorRadii.Success -or
    -not $towerDoorFlag.Success -or $towerDoorWarps.Count -ne 2 -or
    $towerDoorBody -notmatch '(?ms)@state0:.*?call objectCheckCollidedWithLink_notDeadAndNotGrabbing\s*call nc,interactionIncState\s*jp interactionIncState' -or
    $towerDoorBody -notmatch '(?ms)@state1:.*?call objectCheckCollidedWithLink_notDeadAndNotGrabbing\s*ret c\s*jp interactionIncState' -or
    $towerDoorBody -notmatch '(?ms)@state2:.*?call objectCheckCollidedWithLink_notDeadAndNotGrabbing\s*ret nc\s*call checkLinkVulnerable\s*ret nc' -or
    $towerDoorBody -notmatch 'ld a,SND_ENTERCAVE\s*call playSound') {
    throw 'Room 1:76 tower-door collision handler changed.'
}

$linkSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$linkRadii = [regex]::Match(
    $linkSource,
    '(?ms); Set collisionRadiusY,X\s*inc l\s*ld a,\$(?<radius>[0-9a-f]{2})\s*ldi \(hl\),a\s*ldi \(hl\),a')
$musicConstantSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$enterCaveSound = [regex]::Match(
    $musicConstantSource,
    '(?m)^\s*SND_ENTERCAVE\s+db\s*;\s*\$(?<sound>[0-9a-f]{2})')
if (-not $linkRadii.Success -or -not $enterCaveSound.Success) {
    throw 'Could not resolve Link collision radii or SND_ENTERCAVE.'
}

$clearPosition = [Convert]::ToInt32($towerDoorClear.Groups['position'].Value, 16)
$towerDoorWarpRows = @($towerDoorWarps | ForEach-Object {
    [pscustomobject]@{
        Group = [Convert]::ToInt32($_.Groups['group'].Value, 16)
        Room = [Convert]::ToInt32($_.Groups['room'].Value, 16)
        Transition = [Convert]::ToInt32($_.Groups['transition'].Value, 16)
        Position = [Convert]::ToInt32($_.Groups['position'].Value, 16)
        Transition2 = [Convert]::ToInt32($_.Groups['transition2'].Value, 16)
    }
})
if ($clearPosition -ne 0x44 -or
    [Convert]::ToInt32($towerDoorRadii.Groups['y'].Value, 16) -ne 0x04 -or
    [Convert]::ToInt32($towerDoorRadii.Groups['x'].Value, 16) -ne 0x10 -or
    [Convert]::ToInt32($towerDoorFlag.Groups['mask'].Value, 16) -ne 0x01 -or
    $towerDoorWarpRows[0].Transition -ne 0x93 -or
    $towerDoorWarpRows[0].Position -ne 0xff -or
    $towerDoorWarpRows[0].Transition2 -ne 0x01 -or
    $towerDoorWarpRows[1].Transition -ne 0x93 -or
    $towerDoorWarpRows[1].Position -ne 0xff -or
    $towerDoorWarpRows[1].Transition2 -ne 0x01) {
    throw 'Room 1:76 tower-door constants diverged from the supported handler.'
}

$towerDoorRows = @(
    "# group`troom`tid`tsubid`ty`tx`tclear-position-a`tclear-position-b`tobject-radius-y`tobject-radius-x`tlink-radius-y`tlink-radius-x`troom-flag-mask`tclear-dest-group`tclear-dest-room`tset-dest-group`tset-dest-room`twarp-transition`tdest-position`twarp-transition2`tsound`tsource",
    (@(
        $towerDoorPlacement.Groups['group'].Value,
        $towerDoorPlacement.Groups['room'].Value,
        $towerDoorPlacement.Groups['id'].Value,
        $towerDoorPlacement.Groups['subid'].Value,
        $towerDoorPlacement.Groups['y'].Value,
        $towerDoorPlacement.Groups['x'].Value,
        $clearPosition.ToString('x2'),
        ($clearPosition + 1).ToString('x2'),
        $towerDoorRadii.Groups['y'].Value,
        $towerDoorRadii.Groups['x'].Value,
        $linkRadii.Groups['radius'].Value,
        $linkRadii.Groups['radius'].Value,
        $towerDoorFlag.Groups['mask'].Value,
        $towerDoorWarpRows[0].Group.ToString('x1'),
        $towerDoorWarpRows[0].Room.ToString('x2'),
        $towerDoorWarpRows[1].Group.ToString('x1'),
        $towerDoorWarpRows[1].Room.ToString('x2'),
        $towerDoorWarpRows[0].Transition.ToString('x2'),
        $towerDoorWarpRows[0].Position.ToString('x2'),
        $towerDoorWarpRows[0].Transition2.ToString('x2'),
        $enterCaveSound.Groups['sound'].Value,
        'miscellaneous2.s:interactiondc_subid10'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\black_tower_doorway_event.tsv'),
    $towerDoorRows)
