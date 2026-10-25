# Rooms 2:1e and 2:1f Rafton sequence. The left-hand interaction chooses one
# of five source behaviours from D2/rope/global/chart state and eventually
# walks into the right room. The right-hand interaction owns the post-D3
# Magic Oar trade and ROOMFLAG_ITEM reward persistence.
$raftonMainScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$raftonHelperScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$raftonLeftOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'jumptable_objectbyte', 'settextid', 'checkabutton',
    'asm15', 'showloadedtext', 'enableinput', 'setanimation', 'scriptjump',
    'callscript', 'showtextlowindex', 'wait', 'jumpiftextoptioneq',
    'setglobalflag', 'disableinput', 'writeobjectbyte', 'setspeed',
    'moveright', 'scriptend', 'retscript')) {
    [void]$raftonLeftOpcodes.Add($opcode)
}
$raftonLeftCommands = @(Read-AssemblyCutsceneCommands `
    $raftonMainScriptPath 'rafton_subid00Script' $raftonLeftOpcodes `
    'rafton_subid01Script')
if ($raftonLeftCommands.Count -ne 53) {
    throw "rafton_subid00Script expected 53 commands, parsed $($raftonLeftCommands.Count)."
}

$raftonRightOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'asm15', 'jumpifmemoryset', 'settextid',
    'checkabutton', 'showloadedtext', 'wait', 'setanimation', 'scriptjump',
    'disableinput', 'jumpifroomflagset', 'showtextlowindex',
    'jumpiftradeitemeq', 'jumpiftextoptioneq', 'giveitem', 'enableinput')) {
    [void]$raftonRightOpcodes.Add($opcode)
}
$raftonRightCommands = @(Read-AssemblyCutsceneCommands `
    $raftonHelperScriptPath 'rafton_subid01Script' $raftonRightOpcodes `
    'cheval_setTalkedGlobalflag')
if ($raftonRightCommands.Count -ne 30) {
    throw "rafton_subid01Script expected 30 commands, parsed $($raftonRightCommands.Count)."
}

$raftonLeftExpected = @(
    @('initcollisions', ''),
    @('jumptable_objectbyte', 'Interaction.var38'),
    @('settextid', 'TX_2700'),
    @('checkabutton', ''),
    @('asm15', 'scriptHelp.turnToFaceLink'),
    @('showloadedtext', ''),
    @('enableinput', ''),
    @('setanimation', 'DIR_DOWN'),
    @('scriptjump', '@genericNpcLoop'),
    @('checkabutton', ''),
    @('callscript', '@faceLinkAndFreezeAnimation'),
    @('showtextlowindex', '<TX_2700'),
    @('wait', '20'),
    @('settextid', 'TX_2701'),
    @('scriptjump', '@showLoadedTextInGenericNpcLoop'),
    @('checkabutton', ''),
    @('callscript', '@faceLinkAndFreezeAnimation'),
    @('wait', '20'),
    @('asm15', 'scriptHelp.createExclamationMark, 60'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_2702'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_2703'),
    @('jumpiftextoptioneq', '$00, @giveRopeToRafton'),
    @('wait', '20'),
    @('showtextlowindex', '<TX_2704'),
    @('enableinput', ''),
    @('setanimation', 'DIR_DOWN'),
    @('checkabutton', ''),
    @('callscript', '@faceLinkAndFreezeAnimation'),
    @('scriptjump', '@askToGiveRope'),
    @('asm15', 'loseTreasure, TREASURE_CHEVAL_ROPE'),
    @('wait', '20'),
    @('showtextlowindex', '<TX_2705'),
    @('wait', '20'),
    @('setglobalflag', 'GLOBALFLAG_GAVE_ROPE_TO_RAFTON'),
    @('enableinput', ''),
    @('settextid', 'TX_2706'),
    @('scriptjump', '@genericNpcLoop'),
    @('disableinput', ''),
    @('wait', '100'),
    @('writeobjectbyte', 'Interaction.animCounter, $7f'),
    @('showtextlowindex', '<TX_2707'),
    @('wait', '30'),
    @('setspeed', 'SPEED_100'),
    @('moveright', '$40'),
    @('setglobalflag', 'GLOBALFLAG_RAFTON_CHANGED_ROOMS'),
    @('enableinput', ''),
    @('scriptend', ''),
    @('disableinput', ''),
    @('asm15', 'scriptHelp.turnToFaceLink'),
    @('writeobjectbyte', 'Interaction.animCounter, $7f'),
    @('retscript', '')
)
$raftonRightExpected = @(
    @('initcollisions', ''),
    @('asm15', 'checkEssenceObtained, $02'),
    @('jumpifmemoryset', 'wcddb, CPU_ZFLAG, @afterD3NpcLoop'),
    @('settextid', 'TX_2708'),
    @('checkabutton', ''),
    @('asm15', 'turnToFaceLink'),
    @('showloadedtext', ''),
    @('wait', '10'),
    @('setanimation', 'DIR_DOWN'),
    @('settextid', 'TX_270a'),
    @('scriptjump', '@beforeD3NpcLoop'),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('jumpifroomflagset', '$20, @alreadyTraded'),
    @('showtextlowindex', '<TX_2710'),
    @('wait', '30'),
    @('jumpiftradeitemeq', 'TRADEITEM_MAGIC_OAR, @linkHasOar'),
    @('scriptjump', '@enableInput'),
    @('showtextlowindex', '<TX_2711'),
    @('wait', '30'),
    @('jumpiftextoptioneq', '$00, @acceptedTrade'),
    @('showtextlowindex', '<TX_2713'),
    @('scriptjump', '@enableInput'),
    @('showtextlowindex', '<TX_2712'),
    @('wait', '30'),
    @('giveitem', 'TREASURE_TRADEITEM, $0a'),
    @('scriptjump', '@enableInput'),
    @('showtextlowindex', '<TX_2714'),
    @('enableinput', ''),
    @('scriptjump', '@afterD3NpcLoop')
)
foreach ($stream in @(
    [pscustomobject]@{
        Name = 'rafton_subid00Script'
        Parsed = $raftonLeftCommands
        Expected = $raftonLeftExpected
    },
    [pscustomobject]@{
        Name = 'rafton_subid01Script'
        Parsed = $raftonRightCommands
        Expected = $raftonRightExpected
    })) {
    $scriptName = $stream.Name
    $parsed = @($stream.Parsed)
    $expected = @($stream.Expected)
    for ($index = 0; $index -lt $expected.Count; $index++) {
        $actualOperands = if ($null -eq $parsed[$index].Operands) {
            ''
        } else {
            ([string]$parsed[$index].Operands).Trim()
        }
        if ($parsed[$index].Opcode -ne $expected[$index][0] -or
            $actualOperands -ne $expected[$index][1]) {
            throw "$scriptName command $index changed from " +
                "$($expected[$index][0]) '$($expected[$index][1])' to " +
                "$($parsed[$index].Opcode) '$actualOperands'."
        }
    }
}

$newRaftonTargets = {
    param([object[]]$commands)
    $targets = @{}
    foreach ($command in $commands) {
        if (-not $targets.ContainsKey($command.Label)) {
            $targets[$command.Label] = $command.Index
        }
    }
    return $targets
}
$raftonLeftTargets = & $newRaftonTargets $raftonLeftCommands
$raftonRightTargets = & $newRaftonTargets $raftonRightCommands
$expectedRaftonLeftTargets = @{
    '@behaviour_befored2' = 2
    '@genericNpcLoop' = 3
    '@showLoadedTextInGenericNpcLoop' = 5
    '@behaviour_afterd2' = 9
    '@behaviour_afterGotChevalRope' = 15
    '@askToGiveRope' = 22
    '@giveRopeToRafton' = 31
    '@behaviour_afterGaveChevalRope' = 37
    '@behaviour_afterGotIslandChart' = 39
    '@faceLinkAndFreezeAnimation' = 49
}
$expectedRaftonRightTargets = @{
    '@beforeD3NpcLoop' = 4
    '@afterD3NpcLoop' = 11
    '@linkHasOar' = 18
    '@acceptedTrade' = 23
    '@alreadyTraded' = 27
    '@enableInput' = 28
}
foreach ($entry in $expectedRaftonLeftTargets.GetEnumerator()) {
    if (-not $raftonLeftTargets.ContainsKey($entry.Key) -or
        $raftonLeftTargets[$entry.Key] -ne $entry.Value) {
        throw "rafton_subid00Script label $($entry.Key) moved from command $($entry.Value)."
    }
}
foreach ($entry in $expectedRaftonRightTargets.GetEnumerator()) {
    if (-not $raftonRightTargets.ContainsKey($entry.Key) -or
        $raftonRightTargets[$entry.Key] -ne $entry.Value) {
        throw "rafton_subid01Script label $($entry.Key) moved from command $($entry.Value)."
    }
}

$raftonNativeSource = Read-ImportText (Join-Path $Disassembly `
    'object_code\ages\interactions\rafton.s')
$raftonMainScriptSource = Read-ImportText $raftonMainScriptPath
$raftonHelperScriptSource = Read-ImportText $raftonHelperScriptPath
$raftonInteractionDataSource = Read-ImportText (Join-Path $Disassembly `
    'data\ages\interactionData.s')
$raftonGlobalFlagSource = Read-ImportText (Join-Path $Disassembly `
    'constants\common\globalFlags.s')
$raftonTreasureSource = Read-ImportText (Join-Path $Disassembly `
    'constants\common\treasure.s')
$raftonStructSource = Read-ImportText (Join-Path $Disassembly 'include\structs.s')
$raftonExclamationSource = Read-ImportText (Join-Path $Disassembly `
    'object_code\common\interactions\exclamationMark.s')
if ($raftonNativeSource -notmatch
        '(?ms)^@state0:.*?bit 7,a.*?interactionInitGraphics.*?objectSetVisiblec2.*?>TX_2700.*?^@initSubid00:.*?GLOBALFLAG_RAFTON_CHANGED_ROOMS.*?TREASURE_ISLAND_CHART.*?GLOBALFLAG_GAVE_ROPE_TO_RAFTON.*?TREASURE_CHEVAL_ROPE.*?wEssencesObtained.*?bit 1,a.*?Interaction\.var38.*?^@initSubid01:.*?GLOBALFLAG_RAFTON_CHANGED_ROOMS.*?^@state1:' -or
    $raftonNativeSource -notmatch
        '(?ms)^@runSubid00:\s+call interactionRunScript\s+jp c,interactionDelete.*?Interaction\.var38.*?cp \$04\s+jp z,interactionAnimateBasedOnSpeed\s+jp interactionAnimateAsNpc\s+^@runSubid01:\s+call interactionAnimateAsNpc\s+jp interactionRunScript' -or
    $raftonNativeSource -notmatch
        '(?ms)^@scriptTable:\s+\.dw mainScripts\.rafton_subid00Script\s+\.dw mainScripts\.rafton_subid01Script' -or
    $raftonMainScriptSource -notmatch
        '(?ms)^rafton_subid01Script:\s+loadscript scriptHelp\.rafton_subid01Script' -or
    $raftonInteractionDataSource -notmatch
        '(?m)^\s*/\* \$69 \*/ m_InteractionData \$5e \$10 \$12\s*$') {
    throw 'INTERAC_RAFTON native initialization, visibility, update order, or script wrapper changed.'
}
if ($mainObjectSource -notmatch
        '(?ms)^group2Map1eObjectData:\s+obj_Interaction \$69 \$00 \$48 \$68\s+obj_End' -or
    $mainObjectSource -notmatch
        '(?ms)^group2Map1fObjectData:\s+obj_Interaction \$69 \$01 \$48 \$28\s+obj_End') {
    throw 'Rooms 2:1e/2:1f Rafton object streams or coordinates changed.'
}
if ($raftonGlobalFlagSource -notmatch
        '(?m)^\s*GLOBALFLAG_GAVE_ROPE_TO_RAFTON\s+db ; \$15\s*$' -or
    $raftonGlobalFlagSource -notmatch
        '(?m)^\s*GLOBALFLAG_RAFTON_CHANGED_ROOMS\s+db ; \$26:' -or
    $raftonTreasureSource -notmatch
        '(?m)^\s*TREASURE_CHEVAL_ROPE\s+db ; \$52\s*$' -or
    $raftonTreasureSource -notmatch
        '(?m)^\s*TREASURE_ISLAND_CHART\s+db ; \$54\s*$' -or
    $tradeItemSource -notmatch 'TRADEITEM_MAGIC_OAR\s+db ; \$09' -or
    $tradeItemSource -notmatch 'TRADEITEM_SEA_UKELELE\s+db ; \$0a' -or
    $roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $speedSource -notmatch 'SPEED_100\s+dsb 5 ; 0x28' -or
    $raftonStructSource -notmatch '(?m)^\s*animCounter\s+db ; \$20\s*$' -or
    $raftonExclamationSource -notmatch
        '(?ms)^@state0:.*?set 7,\(hl\).*?interactionInitGraphics.*?^@state1:.*?Interaction\.counter1.*?dec \(hl\).*?interactionDelete.*?^objectCreateExclamationMark_body:.*?INTERAC_EXCLAMATION_MARK.*?objectCopyPositionWithOffset.*?SND_CLINK') {
    throw 'Rafton flags, treasure, trade, movement, animation-counter, or exclamation constants changed.'
}

$raftonAnimations = @{}
foreach ($animation in 0..3) {
    $raftonAnimations[$animation] = Resolve-NpcAnimation 0x69 $animation
    if ([string]::IsNullOrWhiteSpace($raftonAnimations[$animation])) {
        throw "Could not resolve INTERAC_RAFTON animation `$$($animation.ToString('x2'))."
    }
}
$raftonGraphic = $interactionGraphics['105:0']
$raftonExclamationGraphic = $interactionGraphics['159:0']
$raftonExclamationAnimation = Resolve-NpcAnimation 0x9f 0
if ($null -eq $raftonGraphic -or
    -not $gfxNames.ContainsKey($raftonGraphic.Gfx) -or
    $gfxNames[$raftonGraphic.Gfx] -ne 'spr_masksalesman_rafton' -or
    $raftonGraphic.TileBase -ne 0x10 -or
    $raftonGraphic.Palette -ne 1 -or
    $raftonGraphic.DefaultAnimation -ne 2 -or
    $null -eq $raftonExclamationGraphic -or
    -not $gfxNames.ContainsKey($raftonExclamationGraphic.Gfx) -or
    [string]::IsNullOrWhiteSpace($raftonExclamationAnimation)) {
    throw 'Could not resolve Rafton or INTERAC_EXCLAMATION_MARK graphics.'
}
$raftonTextIds = @(
    0x2700, 0x2701, 0x2702, 0x2703, 0x2704, 0x2705, 0x2706, 0x2707,
    0x2708, 0x2709, 0x270a, 0x2710, 0x2711, 0x2712, 0x2713, 0x2714)
foreach ($textId in $raftonTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Rafton text TX_$($textId.ToString('x4'))."
    }
}
$raftonTexts = @{}
foreach ($textId in $raftonTextIds) {
    $raftonTexts[$textId] = $allTexts[$textId]
}
$raftonFallthroughs = @(
    [PSCustomObject]@{ Source = 0x2705; Destination = 0x2706 }
    [PSCustomObject]@{ Source = 0x2708; Destination = 0x2709 }
)
foreach ($fallthrough in $raftonFallthroughs) {
    if (-not $allTextFallthroughIds.ContainsKey($fallthrough.Source) -or
        $allTextFallthroughIds[$fallthrough.Source] -ne $fallthrough.Destination -or
        -not $allTexts[$fallthrough.Source].EndsWith(
            '\n', [StringComparison]::Ordinal)) {
        throw "Rafton TX_$($fallthrough.Source.ToString('x4')) no longer falls " +
            "through its trailing newline into " +
            "TX_$($fallthrough.Destination.ToString('x4'))."
    }

    # The source has no $00 terminator. Resolve its final newline command
    # before adjoining the next text so the dialogue parser cannot read the
    # newline escape and the destination's first character as one command.
    $sourceText = $allTexts[$fallthrough.Source]
    $raftonTexts[$fallthrough.Source] =
        $sourceText.Substring(0, $sourceText.Length - 2) +
        "`n" + $allTexts[$fallthrough.Destination]
}
$raftonReward = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_0a']
if ($null -eq $raftonReward -or
    $raftonReward.Treasure -ne 0x41 -or
    $raftonReward.SubId -ne 0x0a -or
    $raftonReward.Parameter -ne 0x0a -or
    $raftonReward.TextId -ne 0x0064 -or
    $raftonReward.Graphic -ne 0x7a) {
    throw 'TREASURE_OBJECT_TRADEITEM_0a no longer grants the Sea Ukulele.'
}

$raftonTextRows = [Collections.Generic.List[string]]::new()
$raftonTextRows.Add("# id`ttext-base64")
foreach ($textId in $raftonTextIds) {
    $raftonTextRows.Add("$($textId.ToString('x4'))`t$([Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($raftonTexts[$textId])))")
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\rafton_text.tsv'),
    $raftonTextRows)

$newRaftonCommandRows = {
    param(
        [object[]]$commands,
        [hashtable]$targets,
        [bool]$rightRoom)
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
    foreach ($command in $commands) {
        $opcode = $command.Opcode
        $actor = ''
        $arg0 = ''
        $arg1 = ''
        $payload = ''
        switch ($command.Opcode) {
            'initcollisions' { $actor = 'Rafton' }
            'checkabutton' { $actor = 'Rafton' }
            'jumptable_objectbyte' {
                if ($rightRoom -or $command.Operands -ne 'Interaction.var38') {
                    throw "Unexpected Rafton jump-table binding '$($command.Operands)'."
                }
                $opcode = 'jumptablememory'
                $payload = "RaftonBehaviour|$($targets['@behaviour_befored2']),$($targets['@behaviour_afterd2']),$($targets['@behaviour_afterGotChevalRope']),$($targets['@behaviour_afterGaveChevalRope']),$($targets['@behaviour_afterGotIslandChart'])"
            }
            'settextid' {
                if ($command.Operands -notmatch '^TX_(?<id>27(?:00|01|06|08|0a))$') {
                    throw "Unexpected Rafton loaded text '$($command.Operands)'."
                }
                $opcode = 'writememory'
                $arg0 = $Matches['id']
                $payload = 'LoadedText'
            }
            'asm15' {
                switch -Regex ($command.Operands) {
                    '^(?:scriptHelp\.)?turnToFaceLink$' {
                        $opcode = 'native'; $payload = 'TurnToFaceLink'; break
                    }
                    '^scriptHelp\.createExclamationMark,\s*60$' {
                        $opcode = 'native'; $payload = 'CreateExclamationMark'; break
                    }
                    '^loseTreasure,\s*TREASURE_CHEVAL_ROPE$' {
                        $opcode = 'native'; $payload = 'LoseChevalRope'; break
                    }
                    '^checkEssenceObtained,\s*\$02$' {
                        $opcode = 'native'; $payload = 'CheckD3Essence'; break
                    }
                    default {
                        throw "Unsupported Rafton asm15 '$($command.Operands)' at source line $($command.Line)."
                    }
                }
            }
            'showtextlowindex' {
                if ($command.Operands -notmatch '^<TX_(?<id>27(?:0[0-7]|1[0-4]))$') {
                    throw "Unexpected Rafton text '$($command.Operands)'."
                }
                $textId = [Convert]::ToInt32($Matches['id'], 16)
                $opcode = 'showtext'
                $arg0 = $Matches['id']
                $payload = $raftonTexts[$textId]
            }
            'setanimation' {
                if ($command.Operands -ne 'DIR_DOWN') {
                    throw "Unexpected Rafton animation '$($command.Operands)'."
                }
                $actor = 'Rafton'; $arg0 = '02'; $payload = $raftonAnimations[2]
            }
            'scriptjump' {
                if (-not $targets.ContainsKey($command.Operands)) {
                    throw "Unknown Rafton branch target '$($command.Operands)'."
                }
                $arg0 = $targets[$command.Operands].ToString()
            }
            'callscript' {
                if (-not $targets.ContainsKey($command.Operands)) {
                    throw "Unknown Rafton subscript target '$($command.Operands)'."
                }
                $arg0 = $targets[$command.Operands].ToString()
            }
            'retscript' { $opcode = 'return' }
            'wait' {
                if ($command.Operands -notin @('10', '20', '30', '100')) {
                    throw "Unexpected Rafton wait '$($command.Operands)'."
                }
                $arg0 = $command.Operands
            }
            'jumpiftextoptioneq' {
                if ($command.Operands -notmatch
                    '^\$00,\s*(?<target>@[A-Za-z0-9_]+)$' -or
                    -not $targets.ContainsKey($Matches['target'])) {
                    throw "Malformed Rafton choice branch at line $($command.Line)."
                }
                $arg0 = '00'; $arg1 = $targets[$Matches['target']].ToString()
            }
            'setglobalflag' {
                $arg0 = switch ($command.Operands) {
                    'GLOBALFLAG_GAVE_ROPE_TO_RAFTON' { '15' }
                    'GLOBALFLAG_RAFTON_CHANGED_ROOMS' { '26' }
                    default { throw "Unexpected Rafton global flag '$($command.Operands)'." }
                }
            }
            'writeobjectbyte' {
                if ($command.Operands -ne 'Interaction.animCounter, $7f') {
                    throw "Unexpected Rafton object write '$($command.Operands)'."
                }
                $actor = 'Rafton'; $arg0 = '20'; $arg1 = '7f'
            }
            'setspeed' {
                if ($command.Operands -ne 'SPEED_100') {
                    throw "Unexpected Rafton speed '$($command.Operands)'."
                }
                $actor = 'Rafton'; $arg0 = '28'
            }
            'moveright' {
                if ($command.Operands -ne '$40') {
                    throw "Unexpected Rafton movement '$($command.Operands)'."
                }
                $opcode = 'move'; $actor = 'Rafton'; $arg0 = '08'; $arg1 = '40'
                $payload = $raftonAnimations[1]
            }
            'jumpifmemoryset' {
                if (-not $rightRoom -or $command.Operands -notmatch
                    '^wcddb,\s*CPU_ZFLAG,\s*(?<target>@[A-Za-z0-9_]+)$') {
                    throw "Malformed Rafton essence branch at line $($command.Line)."
                }
                $opcode = 'jumpifmemoryeqyieldonmiss'
                $arg0 = '01'; $arg1 = $targets[$Matches['target']].ToString()
                $payload = 'D3EssenceObtained'
            }
            'jumpifroomflagset' {
                if (-not $rightRoom -or $command.Operands -notmatch
                    '^\$20,\s*(?<target>@[A-Za-z0-9_]+)$') {
                    throw "Malformed Rafton room-flag branch at line $($command.Line)."
                }
                $arg0 = '20'; $arg1 = $targets[$Matches['target']].ToString()
            }
            'jumpiftradeitemeq' {
                if (-not $rightRoom -or $command.Operands -notmatch
                    '^TRADEITEM_MAGIC_OAR,\s*(?<target>@[A-Za-z0-9_]+)$') {
                    throw "Malformed Rafton trade branch at line $($command.Line)."
                }
                $arg0 = '09'; $arg1 = $targets[$Matches['target']].ToString()
            }
            'giveitem' {
                if (-not $rightRoom -or
                    $command.Operands -ne 'TREASURE_TRADEITEM, $0a') {
                    throw "Unexpected Rafton reward '$($command.Operands)'."
                }
                $arg0 = '41'; $arg1 = '0a'
            }
        }
        $rows.Add((New-CutsceneCommandRow `
            $command.Script $command.Index $command.Label $command.Line `
            $opcode $actor "$arg0" "$arg1" $payload))
    }
    return $rows
}
$raftonLeftCommandRows = & $newRaftonCommandRows `
    $raftonLeftCommands $raftonLeftTargets $false
$raftonRightCommandRows = & $newRaftonCommandRows `
    $raftonRightCommands $raftonRightTargets $true
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\rafton_left_commands.tsv'),
    $raftonLeftCommandRows)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\rafton_right_commands.tsv'),
    $raftonRightCommandRows)

$raftonEventRows = @(
    "# group`tleft-room`tright-room`tid`tleft-subid`tright-subid`tanimation0`tanimation1`tanimation2`tanimation3`tinitial-animation`tcollision-y`tcollision-x`troom-flag`tgave-rope-flag`tchanged-rooms-flag`tcheval-rope-treasure`tisland-chart-treasure`td2-essence-mask`td3-essence-mask`trequired-trade`treward-treasure`treward-parameter`treward-object`tspeed`tright-angle`tmove-counter`tanim-counter-address`tfreeze-counter`teffect-id`teffect-subid`teffect-sprite`teffect-tile-base`teffect-palette`teffect-animation`teffect-y`teffect-x`teffect-frames`tclink-sound`tinitial-script-updates"
    (@(
        '2', '1e', '1f', '69', '00', '01',
        $raftonAnimations[0], $raftonAnimations[1],
        $raftonAnimations[2], $raftonAnimations[3],
        '02', '06', '06', '20', '15', '26', '52', '54', '02', '04',
        '09', '41', '0a', 'TREASURE_OBJECT_TRADEITEM_0a',
        '28', '08', '40', '20', '7f', '9f', '00',
        $gfxNames[$raftonExclamationGraphic.Gfx],
        $raftonExclamationGraphic.TileBase.ToString(),
        $raftonExclamationGraphic.Palette.ToString(),
        $raftonExclamationAnimation,
        '-13', '0', '60', '50', '0'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\rafton_event.tsv'),
    $raftonEventRows)

# INTERAC_RAFT $e6 and SPECIALOBJECT_RAFT $13. The placed interactions live
# in rooms $1:$a7/$a9; a third subid is created from remembered companion
# state by roomInitialization.loadRememberedCompanion.
$raftInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\raft.s')
$raftSpecialSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\specialObjects\raft.s')
$raftRoomInitSource = Read-ImportText (
    Join-Path $Disassembly 'code\roomInitialization.s')
$raftObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$raftSpecialAnimations = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
$raftSpecialOam = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectOamData.s')
$raftSpecialCommonSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\commonCode.s')
$raftSpecialCollisionSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\specialCollisionValues.s')
$raftOamVariables = [regex]::Match(
    $raftSpecialCommonSource,
    '(?m)^\s*\.db\s+\$(?<tilebase>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})\s*;\s*0x13\s*$')
if ($raftInteractionSource -notmatch '(?ms)^interactionCodee6:.*?@subid0:.*?wDimitriState.*?bit 6.*?@subid1:.*?GLOBALFLAG_RAFTON_CHANGED_ROOMS.*?SPECIALOBJECT_RAFT.*?@subid2:.*?UNCMP_GFXH_AGES_3b.*?@state1:.*?ld a,\$09.*?wLinkInAir.*?cp \$fd.*?w1Companion.enabled.*?SPECIALOBJECT_RAFT' -or
    $raftSpecialSource -notmatch '(?ms)^specialObjectCode_raft:.*?@state0:.*?counter1.*?\$0c.*?@state1:.*?SPEED_e0.*?@positionUnchanged:.*?var3f' -or
    $raftSpecialSource -notmatch '(?ms)^@dismountTileOffsets:\s*\.db \$f7 \$00.*?\.db \$fd \$08.*?\.db \$08 \$00.*?\.db \$fd \$f7' -or
    $raftSpecialSource -notmatch '(?ms)LINK_STATE_FORCE_MOVEMENT.*?ld a,14.*?^@state2:.*?itemDecCounter1.*?updateLinkLocalRespawnPosition.*?^@state3:.*?INTERAC_RAFT, \$02' -or
    $raftSpecialSource -notmatch '(?ms)@@wallPositionOffsets:\s*\.db \$fa \$fb\s*\.db \$fa \$04\s*\.db \$05 \$fb\s*\.db \$05 \$04\s*\.db \$fb \$fa\s*\.db \$04 \$fa\s*\.db \$fb \$05\s*\.db \$04 \$05.*?@@validTiles:.*?TILEINDEX_DEEP_WATER.*?TILEINDEX_CURRENT_UP.*?TILEINDEX_CURRENT_DOWN.*?TILEINDEX_CURRENT_LEFT.*?TILEINDEX_CURRENT_RIGHT.*?TILEINDEX_WATER.*?TILEINDEX_WHIRLPOOL.*?\.db \$00' -or
    $raftRoomInitSource -notmatch '(?ms)^loadRememberedCompanion:.*?cp SPECIALOBJECT_RAFT.*?@raft:.*?TILESETFLAG_PAST.*?INTERAC_RAFT.*?ld \(hl\),\$02' -or
    $raftObjectSource -notmatch '(?ms)^group1Mapa7ObjectData:.*?obj_Interaction \$e6 \$01 \$58 \$78' -or
    $raftObjectSource -notmatch '(?ms)^group1Mapa9ObjectData:.*?obj_Interaction \$e6 \$00 \$38 \$78' -or
    $raftSpecialAnimations -notmatch '(?ms)^specialObject13GfxPointers:\s*m_SpecialObjectGfxPointer \$00 spr_raft \$0000 \$04\s*m_SpecialObjectGfxPointer \$00 spr_raft \$0020 \$04\s*m_SpecialObjectGfxPointer \$00 spr_raft \$0040 \$04\s*m_SpecialObjectGfxPointer \$00 spr_raft \$0060 \$04.*?^specialObject13AnimationDataPointers:\s*\.dw animationData1a1ef\s*\.dw animationData1a1f7\s*\.dw animationData1a1ef\s*\.dw animationData1a1f7' -or
    $raftSpecialOam -notmatch '(?ms)^oamData4c024:\s*\.db \$02\s*\.db \$08 \$00 \$00 \$00\s*\.db \$08 \$08 \$00 \$20' -or
    $raftSpecialCollisionSource -notmatch '(?m)^\s*SPECIALCOLLISION_STAIRS\s+db\s*;\s*\$18:' -or
    -not $raftOamVariables.Success -or
    $raftOamVariables.Groups['tilebase'].Value -ne '60' -or
    $raftOamVariables.Groups['flags'].Value -ne '0b') {
    throw 'INTERAC_RAFT or SPECIALOBJECT_RAFT source contract changed.'
}
$raftGraphic = $interactionGraphics['230:0']
if ($raftInteractionSource -notmatch '(?ms)@mountedRaft:.*?ld a,\$05\s+ld \(wInstrumentsDisabledCounter\),a\s+call @checkLinkWithinRange\s+ret nc' -or
    $raftInteractionSource -notmatch '(?ms)@subid2:.*?jp objectSetVisible83' -or
    $raftSpecialSource -notmatch '(?ms)@state0:.*?ld l,SpecialObject.counter1\s+ld \(hl\),\$0c' -or
    $raftSpecialAnimations -notmatch '(?ms)^animationData1a1ef:\s*\.db \$0c \$00 \$00\s*\.db \$0c \$01 \$04\s*m_AnimationLoop animationData1a1ef\s*animationData1a1f7:\s*\.db \$0c \$02 \$00\s*\.db \$0c \$03 \$04') {
    throw 'INTERAC_RAFT $e6 inner mount radius/instrument lock/priority or SPECIALOBJECT_RAFT $13 counters/animation changed.'
}
$raftWaitingAnimations = @(0..1 | ForEach-Object {
    Resolve-NpcAnimation 0xe6 $_
})
if ($null -eq $raftGraphic -or
    $raftGraphic.TileBase -ne 0x60 -or
    $raftGraphic.Palette -ne 3 -or
    @($raftWaitingAnimations | Where-Object {
        [string]::IsNullOrWhiteSpace($_)
    }).Count -ne 0) {
    throw 'Could not resolve INTERAC_RAFT graphics.'
}
$raftOam = '8,0,0,0;8,8,0,32'
$raftMountedVertical = "12@$raftOam|12,4@$raftOam"
$raftMountedHorizontal = "12@$raftOam|12,4@$raftOam"
$raftMountedPalette =
    [Convert]::ToInt32($raftOamVariables.Groups['flags'].Value, 16) -band 0x07
$raftRows = @(
    "# group`troom`tsubid`ty`tx`tinteraction-id`tspecial-id`tchanged-rooms-flag`tdimitri-state-address`tdimitri-mask`tpast-mask`tradius`tspeed`tknockback-speed`tdismount-collision`tdismount-delay`tdismount-walk`tsprite`twaiting-tile-base`twaiting-palette`twaiting-vertical`twaiting-horizontal`tmounted-palette`tmounted-vertical`tmounted-horizontal`tmounted-vertical-offsets`tmounted-horizontal-offsets`tvalid-tiles`tsource",
    (@('1','a7','01','58','78','e6','13','26','c647','40','80','09','23','28','18','04','0e',
        'spr_raft','0',$raftGraphic.Palette,
        $raftWaitingAnimations[0],$raftWaitingAnimations[1],$raftMountedPalette,
        $raftMountedVertical,$raftMountedHorizontal,'0,20','40,60',
        'fc,e0,e1,e2,e3,fa,e9',
        'mainData.s:group1MapA7ObjectData;interactionCodee6;specialObjectCode_raft') -join "`t"),
    (@('1','a9','00','38','78','e6','13','26','c647','40','80','09','23','28','18','04','0e',
        'spr_raft','0',$raftGraphic.Palette,
        $raftWaitingAnimations[0],$raftWaitingAnimations[1],$raftMountedPalette,
        $raftMountedVertical,$raftMountedHorizontal,'0,20','40,60',
        'fc,e0,e1,e2,e3,fa,e9',
        'mainData.s:group1MapA9ObjectData;interactionCodee6;specialObjectCode_raft') -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\raft.tsv'), @(
        $raftRows[0] + "`tinner-radius`tinstrument-lock`tdismount-wait"
        $raftRows[1] + "`t05`t05`t0c"
        $raftRows[2] + "`t05`t05`t0c"
    ))
Copy-GeneratedFile 'gfx\common\spr_raft.png' 'gfx\spr_raft.png'

# Past ocean room $1:$a8 contains INTERAC_RAFTWRECK_CUTSCENE $9b:$00.
# The script moves wLinkObjectIndex (the mounted SPECIALOBJECT_RAFT), while
# native substates own the two lightning-flash waits and the final warp.
$raftwreckSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\raftwreckCutscene.s')
$raftwreckHelperSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\raftwreckCutsceneHelper.s')
$raftwreckScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$raftwreckFlashSource = Read-ImportText (
    Join-Path $Disassembly 'code\bank3Cutscenes.s')
$raftwreckPresetSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\twinrova.s')
$raftwreckLightningSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\lighting.s')
$raftwreckDebrisSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\breakTileDebris.s')
$raftwreckPartAnimationSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partAnimations.s')
if ($raftObjectSource -notmatch
        '(?ms)^group1Mapa8ObjectData:\s+obj_Interaction \$9b \$00\s+obj_End' -or
    $raftwreckSource -notmatch
        '(?ms)^interactionCode9b:.*?ROOMFLAG_BIT_40.*?wDisabledObjects.*?wMenuDisabled.*?ld b,\$76.*?SPEED_80.*?sub \$50.*?add a.*?raftwreckCutsceneScript' -or
    $raftwreckSource -notmatch
        '(?ms)^@substate2:.*?genericCutscene\.state.*?^@substate3:.*?flashScreen.*?interactionIncSubstate\s+ldi a,\(hl\)\s+cp \$03\s+ld a,\$5a\s+jr z,\+\s+ld a,\$78.*?^@substate6:.*?genericCutscene\.state\),a.*?^@substate8:.*?SNDCTRL_FAST_FADEOUT.*?ROOMFLAG_BIT_40.*?res 1,\(hl\).*?ROOM_AGES_1aa, \$00, \$42, \$03' -or
    $raftwreckSource -notmatch
        '(?ms)^@yOscillation:\s*\.db \$ff \$fe \$ff \$00 \$01 \$02 \$01 \$00' -or
    $raftwreckHelperSource -notmatch
        '(?ms)^@subid3Objects:.*?\.db \$20 \$a8 \$00 \$00.*?^@subid4Objects:.*?\.db \$00 \$a8 \$00 \$00.*?^@subid5Objects:\s*\.db \$28 \$28 \$01 \$28\s*\.db \$58 \$38 \$01 \$5a\s*\.db \$40 \$50 \$01 \$00' -or
    $raftwreckFlashSource -notmatch
        '(?ms)@data1:\s*\.db \$02 \$04 \$06 \$08 \$0a \$0c \$ff' -or
    $raftwreckPresetSource -notmatch
        '(?ms)@data3: ; INTERAC_RAFTWRECK_CUTSCENE_HELPER\s*\.db \$15 \$0c\s*\.db \$16 \$0c\s*\.db \$17 \$12\s*\.db \$18 \$14\s*\.db \$19 \$14\s*\.db \$1a \$20\s*\.db \$00 \$00' -or
    $raftwreckLightningSource -notmatch
        '(?ms)^partCode27:.*?getRandomNumber_noPreserveVars.*?and \$06.*?ld \(hl\),\$c0.*?SND_LIGHTNING.*?^@func_55a6:.*?Part\.animParameter.*?call nz,@func_55e7.*?ld e,\$e1\s+ld a,\(de\)\s+bit 0,a\s+ret z\s+dec a\s+ld \(de\),a\s+ld a,\$06\s+jp setScreenShakeCounter.*?^@table_55e2:\s*\.db \$c0 \$d0\s*\.db \$e0 \$f0\s*\.db \$00.*?^@table_5603:\s*\.db \$02 \$06\s*\.db \$00 \$fb\s*\.db \$ff \$07\s*\.db \$fd \$fc\s*\.db \$00 \$05' -or
    $raftwreckDebrisSource -notmatch
        '(?ms)^interactionCode08:.*?SPEED_80.*?^@soundAndPriorityTable:.*?SND_KILLENEMY\s+\$00\s*; 0x08') {
    throw 'Room 1:a8 raftwreck controller, helper, flash, or movement source changed.'
}

$raftwreckOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'wait', 'playsound', 'asm15', 'setangle', 'applyspeed',
    'writememory', 'checkpalettefadedone', 'checkmemoryeq',
    'writeobjectbyte', 'setspeed', 'scriptend')) {
    [void]$raftwreckOpcodes.Add($opcode)
}
$raftwreckCommands = @(Read-AssemblyCutsceneCommands `
    $raftwreckScriptPath 'raftwreckCutsceneScript_body' `
    $raftwreckOpcodes 'raftwreckCutscene_spawnHelperSubid')
if ($raftwreckCommands.Count -ne 44) {
    throw "raftwreckCutsceneScript_body expected 44 commands, parsed $($raftwreckCommands.Count)."
}
$raftwreckCommandRows = [Collections.Generic.List[string]]::new()
$raftwreckCommandRows.Add($cutsceneCommandHeader)
foreach ($command in $raftwreckCommands) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Opcode) {
        'wait' { $arg0 = $command.Operands }
        'playsound' {
            $sound = if ($command.Operands -eq 'SNDCTRL_FAST_FADEOUT') {
                0xfa
            } else {
                Resolve-SoundConstant $command.Operands
            }
            $arg0 = $sound.ToString('x2')
        }
        'asm15' {
            $opcode = 'native'
            $payload = switch ($command.Operands) {
                'setLinkDirection, DIR_UP' { 'SetLinkUp'; break }
                'darkenRoom' { 'DarkenRoom'; break }
                'setLinkDirection, DIR_RIGHT' { 'SetLinkRight'; break }
                'raftwreckCutscene_spawnHelperSubid, $03' { 'SpawnHelper03'; break }
                'raftwreckCutscene_spawnHelperSubid, $04' { 'SpawnHelper04'; break }
                'raftwreckCutscene_spawnHelperSubid, $05' { 'SpawnHelper05'; break }
                default { throw "Unsupported raftwreck asm15 '$($command.Operands)'." }
            }
        }
        'setangle' {
            $actor = 'Raftwreck'
            $arg0 = switch ($command.Operands) {
                'ANGLE_UP' { '00'; break }
                'ANGLE_RIGHT' { '08'; break }
                'ANGLE_LEFT' { '18'; break }
                default { throw "Unsupported raftwreck angle '$($command.Operands)'." }
            }
        }
        'applyspeed' {
            if ($command.Operands -notmatch '^\$(?<value>[0-9a-f]{2})$') {
                throw "Malformed raftwreck applyspeed at line $($command.Line)."
            }
            $actor = 'Raftwreck'
            $arg0 = $Matches['value']
        }
        'setspeed' {
            if ($command.Operands -notmatch '^SPEED_(?<speed>080|0c0|140)$') {
                throw "Unsupported raftwreck speed '$($command.Operands)'."
            }
            $speedName = switch ($Matches['speed']) {
                '080' { '80'; break }
                '0c0' { 'c0'; break }
                default { $Matches['speed'] }
            }
            $actor = 'Raftwreck'
            $arg0 = (Resolve-ObjectSpeed $speedName).ToString('x2')
        }
        'writememory' {
            if ($command.Operands -notmatch
                '^(?<binding>[^,]+),\s*\$(?<value>[0-9a-f]{2})$') {
                throw "Malformed raftwreck memory write at line $($command.Line)."
            }
            $arg0 = $Matches['value']
            $payload = $Matches['binding']
        }
        'checkpalettefadedone' {
            $opcode = 'gate'
            $payload = 'PaletteFade'
        }
        'checkmemoryeq' {
            if ($command.Operands -notmatch
                '^(?<binding>[^,]+),\s*\$(?<value>[0-9a-f]{2})$') {
                throw "Malformed raftwreck memory gate at line $($command.Line)."
            }
            $arg0 = $Matches['value']
            $payload = $Matches['binding']
        }
        'writeobjectbyte' {
            if ($command.Operands -notmatch
                '^Interaction\.var(?<address>[0-9a-f]{2}),\s*\$(?<value>[0-9a-f]{2})$') {
                throw "Malformed raftwreck object write at line $($command.Line)."
            }
            $actor = 'Raftwreck'
            $arg0 = $Matches['address']
            $arg1 = $Matches['value']
        }
    }
    $raftwreckCommandRows.Add((New-CutsceneCommandRow `
        'raftwreckCutsceneScript_body' $command.Index $command.Label `
        $command.Line $opcode $actor $arg0 $arg1 $payload))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\raftwreck_commands.tsv'),
    $raftwreckCommandRows)

function Read-RaftwreckObjectTable([string]$label) {
    $match = [regex]::Match(
        $raftwreckHelperSource,
        "(?ms)^${label}:\s*(?<body>(?:\s*\.db \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2}(?:\s*;[^\r\n]*)?\r?\n)+)")
    if (-not $match.Success) { throw "Could not parse raftwreck $label." }
    return @([regex]::Matches(
        $match.Groups['body'].Value,
        '\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<subid>[0-9a-f]{2}) \$(?<counter>[0-9a-f]{2})'))
}
$raftwreckWindRows = [Collections.Generic.List[string]]::new()
$raftwreckWindRows.Add("# helper-subid`tindex`ty`tx`teffect-subid`tcounter")
foreach ($helper in @(3, 4, 5)) {
    $records = Read-RaftwreckObjectTable "@subid${helper}Objects"
    for ($index = 0; $index -lt $records.Count; $index++) {
        $record = $records[$index]
        $raftwreckWindRows.Add("$helper`t$index`t$($record.Groups['y'].Value)`t$($record.Groups['x'].Value)`t$($record.Groups['subid'].Value)`t$($record.Groups['counter'].Value)")
    }
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\raftwreck_helpers.tsv'),
    $raftwreckWindRows)

$raftwreckEffectRows = [Collections.Generic.List[string]]::new()
$raftwreckEffectRows.Add("# subid`tsprite`ttile-base`tpalette`tanimation`tduration")
foreach ($subid in 0..2) {
    $graphic = $interactionGraphics["100:$subid"]
    $animationIndex = if ($subid -eq 2) { 1 } else { 0 }
    $animation = Resolve-NpcAnimation 0x64 $animationIndex
    if ($null -eq $graphic -or [string]::IsNullOrWhiteSpace($animation) -or
        -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw ('Could not resolve INTERAC_RAFTWRECK_CUTSCENE_HELPER $64:$' +
            $subid.ToString('x2') + '.')
    }
    $sprite = if ($graphic.Gfx -eq 0) { 'spr_common_sprites' } else { $gfxNames[$graphic.Gfx] }
    $raftwreckEffectRows.Add("$subid`t$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$animation`t0")
}
$debrisGraphic = $interactionGraphics['8:0']
$debrisAnimation = Resolve-NpcAnimation 0x08 0
if ($null -eq $debrisGraphic -or [string]::IsNullOrWhiteSpace($debrisAnimation)) {
    throw 'Could not resolve INTERAC_GRASSDEBRIS $08:$00 for raftwreck lightning.'
}
$debrisSprite = if ($debrisGraphic.Gfx -eq 0) {
    'spr_common_sprites'
} else {
    $gfxNames[$debrisGraphic.Gfx]
}
$raftwreckEffectRows.Add("8`t$debrisSprite`t$($debrisGraphic.TileBase)`t$($debrisGraphic.Palette)`t$debrisAnimation`t20")
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\raftwreck_effects.tsv'),
    $raftwreckEffectRows)
Copy-GeneratedFile 'gfx_compressible\ages\spr_bear_monkey.png' 'gfx\spr_bear_monkey.png'

$lightningAnimationMatch = [regex]::Match(
    $raftwreckPartAnimationSource,
    '(?ms)^partAnimation5b9a7:\s*(?<body>(?:\s*\.db \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2}\s*)+)')
if (-not $lightningAnimationMatch.Success) {
    throw 'Could not parse PART_LIGHTNING animation parameters.'
}
$lightningFrames = @([regex]::Matches(
    $lightningAnimationMatch.Groups['body'].Value,
    '\.db \$(?<duration>[0-9a-f]{2}) \$[0-9a-f]{2} \$(?<parameter>[0-9a-f]{2})') |
    ForEach-Object { "$($_.Groups['duration'].Value):$($_.Groups['parameter'].Value)" }) -join ','

# Both completed flash substates select $78 in the executed code. The call to
# interactionIncSubstate changes $03->$04 and $05->$06 before `ldi a,(hl)`;
# neither post-increment value satisfies `cp $03`, so the literal $5a lane is
# unreachable here.
$raftwreckEventRows = @(
    "# group`troom`tinteraction-id`tsubid`troom-flag`tinitial-y`tcenter-x`tinitial-speed`tfirst-flash-wait`tsecond-flash-wait`tfinish-wait`tdestination-room`tdestination-position`tdestination-parameter`tdestination-transition`ty-oscillation`tangle-preset`tlightning-z`tlightning-frames`tlightning-shake`tdebris-offsets`tsource",
    (@('1','a8','9b','00','40','76','50',
        (Resolve-ObjectSpeed '80').ToString('x2'),'78','78','14','aa','42','00','03',
        'ff,fe,ff,00,01,02,01,00','15:0c,16:0c,17:12,18:14,19:14,1a:20',
        'c0,d0,e0,f0,00',$lightningFrames,'06','02:06,00:fb,ff:07,fd:fc,00:05',
        'mainData.s:group1Mapa8ObjectData;raftwreckCutscene.s:interactionCode9b;raftwreckCutsceneHelper.s:interactionCode64') -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\raftwreck_event.tsv'),
    $raftwreckEventRows)
