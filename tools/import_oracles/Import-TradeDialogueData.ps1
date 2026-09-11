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

$comedianCommandRows = [Collections.Generic.List[string]]::new()
$comedianCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
foreach ($command in $comedianCommands) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Opcode) {
        'asm15' {
            switch ($command.Operands) {
                'comedian_checkGameProgress' {
                    $opcode = 'native'
                    $payload = 'comedian_checkGameProgress'
                }
                'comedian_disableMustache' {
                    $opcode = 'native'
                    $payload = 'comedian_disableMustache'
                }
                'comedian_enableMustache' {
                    $opcode = 'native'
                    $payload = 'comedian_enableMustache'
                }
                default {
                    throw "Unsupported comedian asm15 '$($command.Operands)' at source line $($command.Line)."
                }
            }
        }
        'jumpifroomflagset' {
            if ($command.Operands -notmatch
                '^ROOMFLAG_ITEM,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed comedian room-flag branch at source line $($command.Line)."
            }
            $arg0 = '20'
            $arg1 = $comedianTargets[$Matches['target']].ToString()
        }
        'setanimation' {
            if ($command.Operands -notmatch '^\$(?<animation>0[15])$') {
                throw "Unexpected comedian animation '$($command.Operands)'."
            }
            $animation = [Convert]::ToInt32($Matches['animation'], 16)
            $actor = 'Comedian'
            $arg0 = $Matches['animation']
            $payload = $comedianAnimations[$animation]
        }
        'scriptjump' {
            if (-not $comedianTargets.ContainsKey($command.Operands)) {
                throw "Unknown comedian branch target '$($command.Operands)'."
            }
            $arg0 = $comedianTargets[$command.Operands].ToString()
        }
        'initcollisions' { $actor = 'Comedian' }
        'checkabutton' { $actor = 'Comedian' }
        'jumptable_objectbyte' {
            if ($command.Operands -ne 'Interaction.var3f') {
                throw "Unexpected comedian jump-table binding '$($command.Operands)'."
            }
            $opcode = 'jumptablememory'
            $payload = "ComedianProgress|$($comedianTargets['@beforeBeatD2']),$($comedianTargets['@afterBeatD2']),$($comedianTargets['@afterBeatMoonlitGrotto'])"
        }
        'showtextlowindex' {
            if ($command.Operands -notmatch '^<TX_(?<id>0b(?:2[c-f]|3[0-2]))$') {
                throw "Unexpected comedian text '$($command.Operands)'."
            }
            $textId = [Convert]::ToInt32($Matches['id'], 16)
            $opcode = 'showtext'
            $arg0 = $Matches['id']
            $payload = $allTexts[$textId]
        }
        'wait' {
            if ($command.Operands -ne '30') {
                throw "Unexpected comedian wait '$($command.Operands)'."
            }
            $arg0 = '30'
        }
        'jumpiftradeitemeq' {
            if ($command.Operands -notmatch
                '^TRADEITEM_CHEESY_MUSTACHE,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed comedian trade-item branch at source line $($command.Line)."
            }
            $arg0 = '06'
            $arg1 = $comedianTargets[$Matches['target']].ToString()
        }
        'jumpiftextoptioneq' {
            if ($command.Operands -notmatch
                '^\$00,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed comedian choice branch at source line $($command.Line)."
            }
            $arg0 = '00'
            $arg1 = $comedianTargets[$Matches['target']].ToString()
        }
        'giveitem' {
            if ($command.Operands -ne 'TREASURE_TRADEITEM,$07') {
                throw "Unexpected comedian reward '$($command.Operands)'."
            }
            $arg0 = '41'
            $arg1 = '07'
        }
    }
    $comedianCommandRows.Add((New-CutsceneCommandRow `
        $command.Script $command.Index $command.Label $command.Line `
        $opcode $actor "$arg0" "$arg1" $payload))
}
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

$depressedBoyCommandRows = [Collections.Generic.List[string]]::new()
$depressedBoyCommandRows.Add($cutsceneCommandHeader)
foreach ($command in $depressedBoyCommands) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Opcode) {
        'initcollisions' { $actor = 'DepressedBoy' }
        'checkabutton' { $actor = 'DepressedBoy' }
        'checkmemoryeq' {
            switch ($command.Operands) {
                'wMenuDisabled, $00' {
                    $arg0 = '00'
                    $payload = 'MenuDisabled'
                }
                'w1Link.id, SPECIALOBJECT_LINK' {
                    $arg0 = '00'
                    $payload = 'LinkObjectId'
                }
                default {
                    throw "Unsupported depressed-boy memory gate '$($command.Operands)'."
                }
            }
        }
        'asm15' {
            $payload = switch ($command.Operands) {
                'darkenRoomLightly' { 'DarkenRoomLightly'; break }
                'moveLinkToPosition, $02' { 'MoveLinkToFunnyJokePosition'; break }
                'setLinkToState08AndSetDirection, DIR_DOWN' { 'SetLinkNormalDown'; break }
                'boy_runFunnyJokeCutscene' { 'AdvanceFunnyJokeDance'; break }
                'restartSound' { 'RestartSound'; break }
                'setLinkToState08AndSetDirection, DIR_UP' { 'SetLinkNormalUp'; break }
                default {
                    throw "Unsupported depressed-boy asm15 '$($command.Operands)'."
                }
            }
            $opcode = 'native'
        }
        'checkpalettefadedone' {
            $opcode = 'gate'
            $payload = 'PaletteFade'
        }
        'jumpifroomflagset' {
            if ($command.Operands -notmatch
                '^\$20,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed depressed-boy room-flag branch at line $($command.Line)."
            }
            $arg0 = '20'
            $arg1 = $depressedBoyTargets[$Matches['target']].ToString()
        }
        'jumpiftradeitemeq' {
            if ($command.Operands -notmatch
                '^TRADEITEM_FUNNY_JOKE,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed depressed-boy trade branch at line $($command.Line)."
            }
            $arg0 = '07'
            $arg1 = $depressedBoyTargets[$Matches['target']].ToString()
        }
        'showtext' {
            if ($command.Operands -notmatch '^TX_(?<id>251[5-8])$') {
                throw "Unexpected depressed-boy text '$($command.Operands)'."
            }
            $textId = [Convert]::ToInt32($Matches['id'], 16)
            $arg0 = $Matches['id']
            $payload = $allTexts[$textId]
        }
        'scriptjump' {
            if (-not $depressedBoyTargets.ContainsKey($command.Operands)) {
                throw "Unknown depressed-boy branch '$($command.Operands)'."
            }
            $arg0 = $depressedBoyTargets[$command.Operands].ToString()
        }
        'wait' {
            if ($command.Operands -notin @('1', '30', '40', '120')) {
                throw "Unexpected depressed-boy wait '$($command.Operands)'."
            }
            $arg0 = $command.Operands
        }
        'jumpiftextoptioneq' {
            if ($command.Operands -notmatch
                '^\$00,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed depressed-boy choice branch at line $($command.Line)."
            }
            $arg0 = '00'
            $arg1 = $depressedBoyTargets[$Matches['target']].ToString()
        }
        'writeobjectbyte' {
            if ($command.Operands -notmatch
                '^Interaction\.var(?<address>3d),\s*\$(?<value>0[01])$') {
                throw "Unexpected depressed-boy object write '$($command.Operands)'."
            }
            $actor = 'DepressedBoy'
            $arg0 = $Matches['address']
            $arg1 = $Matches['value']
        }
        'setmusic' {
            if ($command.Operands -ne 'MUS_CRAZY_DANCE') {
                throw "Unexpected depressed-boy music '$($command.Operands)'."
            }
            $arg0 = '31'
        }
        'jumpifmemoryset' {
            if ($command.Operands -notmatch
                '^wcddb,\s*\$80,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed depressed-boy flags branch at line $($command.Line)."
            }
            $opcode = 'jumpifmemoryeqyieldonmiss'
            $arg0 = '01'
            $arg1 = $depressedBoyTargets[$Matches['target']].ToString()
            $payload = 'DanceComplete'
        }
        'playsound' {
            if ($command.Operands -ne 'SND_SWORD_OBTAINED') {
                throw "Unexpected depressed-boy sound '$($command.Operands)'."
            }
            $arg0 = 'ab'
        }
        'writememory' {
            if ($command.Operands -ne 'wcc50, LINK_ANIM_MODE_GETITEM2HAND') {
                throw "Unexpected depressed-boy Link animation write '$($command.Operands)'."
            }
            $opcode = 'native'
            $payload = 'SetLinkGetItemTwoHand'
        }
        'giveitem' {
            if ($command.Operands -ne 'TREASURE_TRADEITEM, $08' -and
                $command.Operands -ne 'TREASURE_TRADEITEM,$08') {
                throw "Unexpected depressed-boy reward '$($command.Operands)'."
            }
            $arg0 = '41'
            $arg1 = '08'
        }
        'resetmusic' {
            $opcode = 'native'
            $payload = 'ResetMusic'
        }
    }
    $depressedBoyCommandRows.Add((New-CutsceneCommandRow `
        $command.Script $command.Index $command.Label $command.Line `
        $opcode $actor "$arg0" "$arg1" $payload))
}
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
