# Present indoor room 2:e9 is the Lynna shooting gallery. INTERAC_SHOOTING_GALLERY
# $30:$00 owns the typed conversation and cleanup scripts; its dynamically
# created $30:$03 controller and PART_BALL $38 retain their native state
# machines. Export the complete script streams and every native table they
# consume instead of reconstructing layouts, scoring, or reward thresholds in
# runtime code.
$shootingGalleryScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$shootingGalleryHelperPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$shootingGalleryNativePath = Join-Path $Disassembly (
    'object_code\ages\interactions\shootingGallery.s')
$shootingGalleryBallPath = Join-Path $Disassembly 'object_code\ages\parts\ball.s'
$shootingGalleryDebrisPath = Join-Path $Disassembly (
    'object_code\ages\interactions\fallingRock.s')
$shootingGalleryScriptSource = Read-ImportText $shootingGalleryScriptPath
$shootingGalleryHelperSource = Read-ImportText $shootingGalleryHelperPath
$shootingGalleryNativeSource = Read-ImportText $shootingGalleryNativePath
$shootingGalleryBallSource = Read-ImportText $shootingGalleryBallPath
$shootingGalleryDebrisSource = Read-ImportText $shootingGalleryDebrisPath

if ($mainObjectSource -notmatch
        '(?ms)^group2Mape9ObjectData:\s+obj_Interaction \$30 \$00 \$68 \$88\s+obj_End' -or
    $shootingGalleryNativeSource -notmatch
        '(?ms)^shootingGalleryGame:.*?ld b,\$0a\s+call shootingGallery_initializeGameRounds.*?ld \(hl\),\$78.*?SND_WHISTLE.*?ld \(hl\),\$28.*?SND_BASEBALL.*?ld \(hl\),\$0a.*?ld \(hl\),\$5a.*?cp \$0a' -or
    $shootingGalleryNativeSource -notmatch
        '(?ms)^shootingGallery_getNextTargetLayout:.*?remainingRounds.*?call getRandomNumber.*?wShootingGalleryTileLayoutsToShow' -or
    $shootingGalleryBallSource -notmatch
        '(?ms)^partCode38:.*?and \$0f.*?ld \(hl\),\$64.*?SND_THROW.*?ld \(hl\),\$3c.*?SND_FALLINHOLE.*?SND_CLINK.*?ld \(hl\),\$78' -or
    $shootingGalleryBallSource -notmatch
        '(?ms)^table_6bab:\s+\.db \$d9\s+\.db \$d7\s+\.db \$dc\s+\.db \$d8' -or
    $shootingGalleryBallSource -notmatch
        '(?ms)^func_6bca:.*?ld a,\$04.*?INTERAC_FALLING_ROCK \$04.*?cp \$02.*?INTERAC_FALLING_ROCK \$05.*?objectCreateInteraction.*?dec a\s+ld \(hl\),a\s+jr nz,--' -or
    $shootingGalleryDebrisSource -notmatch
        '(?ms)^fallingRock_subid04:\s*^fallingRock_subid05:.*?fallingRock_initGraphicsAndIncState.*?interactionSetAlwaysUpdateBit.*?ld \(hl\),\$0c\s+jr fallingRock_initDiagonalAngle' -or
    $shootingGalleryHelperSource -notmatch
        '(?ms)^shootingGallery_equipSword:.*?wInventoryA.*?ITEM_SWORD.*?shootingGallery_changeEquips' -or
    $shootingGalleryHelperSource -notmatch
        '(?ms)^shootingGallery_initLinkPosition:\s+ld a,\$00\s+ldbc \$60,\$50.*?^shootingGallery_initLinkPositionAfterGame:\s+ld a,\$01\s+ldbc \$68,\$68' -or
    $shootingGalleryHelperSource -notmatch
        '(?ms)^@positions:\s+\.db \$e0 \$e1.*?\.db \$c6 \$c6') {
    throw 'Room 2:e9 shooting-gallery actor, controller, ball, or setup behavior changed.'
}

$shootingGalleryMainOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'setcollisionradii', 'makeabuttonsensitive', 'checkabutton',
    'disableinput', 'showtext', 'wait', 'jumpiftextoptioneq',
    'enableinput', 'writeobjectbyte', 'scriptjump', 'asm15',
    'jumpifmemoryset', 'checkpalettefadedone', 'setmusic',
    'enableallobjects', 'scriptend')) {
    [void]$shootingGalleryMainOpcodes.Add($opcode)
}
$shootingGalleryCleanupOpcodes =
    [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'disableinput', 'wait', 'asm15', 'checkpalettefadedone',
    'resetmusic', 'jumpifmemoryset', 'jumpifitemobtained',
    'jumpifglobalflagset', 'scriptjump', 'showtext', 'giveitem',
    'scriptend')) {
    [void]$shootingGalleryCleanupOpcodes.Add($opcode)
}
$shootingGalleryMainCommands = Read-AssemblyCutsceneCommands `
    $shootingGalleryScriptPath 'shootingGalleryScript_humanNpc' `
    $shootingGalleryMainOpcodes 'shootingGalleryScript_goronNpc'
$shootingGalleryCleanupCommands = Read-AssemblyCutsceneCommands `
    $shootingGalleryHelperPath 'shootingGalleryScript_humanNpc_gameDone' `
    $shootingGalleryCleanupOpcodes
if ($shootingGalleryMainCommands.Count -ne 48 -or
    $shootingGalleryCleanupCommands.Count -ne 55) {
    throw "Expected 48 shooting-gallery main and 55 cleanup commands, got " +
        "$($shootingGalleryMainCommands.Count) and " +
        "$($shootingGalleryCleanupCommands.Count)."
}

function Convert-ShootingGalleryCommandRows(
    [Collections.Generic.List[object]]$commands,
    [string]$kind
) {
    $targets = @{}
    foreach ($command in $commands) {
        if (-not $targets.ContainsKey($command.Label)) {
            $targets[$command.Label] = $command.Index
        }
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add(
        "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
    foreach ($command in $commands) {
        $opcode = $command.Opcode
        $actor = ''
        $arg0 = ''
        $arg1 = ''
        $payload = ''
        switch ($command.Opcode) {
            'setcollisionradii' {
                if ($command.Operands -notmatch
                    '^\$(?<y>[0-9a-f]{2}),\s*\$(?<x>[0-9a-f]{2})$') {
                    throw "Malformed shooting-gallery collision radii at line $($command.Line)."
                }
                $actor = 'GalleryKeeper'
                $arg0 = $Matches['y']
                $arg1 = $Matches['x']
            }
            'makeabuttonsensitive' { $actor = 'GalleryKeeper' }
            'checkabutton' { $actor = 'GalleryKeeper' }
            'showtext' {
                if ($command.Operands -notmatch '^TX_(?<id>[0-9a-f]{4})$') {
                    throw "Malformed shooting-gallery text at line $($command.Line)."
                }
                $textId = [Convert]::ToInt32($Matches['id'], 16)
                if (-not $allTexts.ContainsKey($textId)) {
                    throw "Missing shooting-gallery text TX_$($Matches['id'])."
                }
                $arg0 = $Matches['id']
                $payload = $allTexts[$textId]
            }
            'wait' {
                if ($command.Operands -notmatch '^(?<frames>[0-9]+)$') {
                    throw "Malformed shooting-gallery wait at line $($command.Line)."
                }
                $arg0 = $Matches['frames']
            }
            'jumpiftextoptioneq' {
                if ($command.Operands -notmatch
                    '^\$(?<value>[0-9a-f]{2}),\s*(?<target>@[A-Za-z0-9_]+)$' -or
                    -not $targets.ContainsKey($Matches['target'])) {
                    throw "Malformed shooting-gallery text branch at line $($command.Line)."
                }
                $arg0 = $Matches['value']
                $arg1 = $targets[$Matches['target']].ToString()
            }
            'writeobjectbyte' {
                if ($command.Operands -notmatch
                    '^Interaction\.var(?<address>[0-9a-f]{2}),\s*\$(?<value>[0-9a-f]{2})$') {
                    throw "Malformed shooting-gallery object write at line $($command.Line)."
                }
                $actor = 'GalleryKeeper'
                $arg0 = $Matches['address']
                $arg1 = $Matches['value']
            }
            'scriptjump' {
                if (-not $targets.ContainsKey($command.Operands)) {
                    throw "Unknown shooting-gallery branch '$($command.Operands)'."
                }
                $arg0 = $targets[$command.Operands].ToString()
            }
            'jumpifmemoryset' {
                if ($command.Operands -notmatch
                    '^wcddb,\s*\$80,\s*(?<target>@[A-Za-z0-9_]+)$' -or
                    -not $targets.ContainsKey($Matches['target'])) {
                    throw "Malformed shooting-gallery flags branch at line $($command.Line)."
                }
                $opcode = 'jumpifmemoryeqyieldonmiss'
                $arg0 = '01'
                $arg1 = $targets[$Matches['target']].ToString()
                $payload = 'Condition'
            }
            'jumpifitemobtained' {
                if ($command.Operands -notmatch
                    '^TREASURE_FLUTE,\s*(?<target>@[A-Za-z0-9_]+)$' -or
                    -not $targets.ContainsKey($Matches['target'])) {
                    throw "Malformed shooting-gallery Flute branch at line $($command.Line)."
                }
                $opcode = 'jumpifmemoryeq'
                $arg0 = '01'
                $arg1 = $targets[$Matches['target']].ToString()
                $payload = 'HasFlute'
            }
            'jumpifglobalflagset' {
                if ($command.Operands -notmatch
                    '^GLOBALFLAG_CAN_BUY_FLUTE,\s*(?<target>@[A-Za-z0-9_]+)$' -or
                    -not $targets.ContainsKey($Matches['target'])) {
                    throw "Malformed shooting-gallery global branch at line $($command.Line)."
                }
                $opcode = 'jumpifmemoryeq'
                $arg0 = '01'
                $arg1 = $targets[$Matches['target']].ToString()
                $payload = 'CanBuyFlute'
            }
            'checkpalettefadedone' {
                $opcode = 'gate'
                $payload = 'PaletteFade'
            }
            'setmusic' {
                if ($command.Operands -ne 'MUS_MINIGAME') {
                    throw "Unexpected shooting-gallery music '$($command.Operands)'."
                }
                $arg0 = '02'
            }
            'enableallobjects' {
                $opcode = 'native'
                $payload = 'EnableAllObjects'
            }
            'resetmusic' {
                $opcode = 'native'
                $payload = 'ResetMusic'
            }
            'giveitem' {
                if ($command.Operands -notmatch
                    '^TREASURE_(?<name>FLUTE|GASHA_SEED),\s*\$(?<parameter>[0-9a-f]{2})$') {
                    throw "Unexpected shooting-gallery reward '$($command.Operands)'."
                }
                $treasureName = "TREASURE_$($Matches['name'])"
                if (-not $treasureIds.ContainsKey($treasureName)) {
                    throw "Missing shooting-gallery treasure constant $treasureName."
                }
                $arg0 = $treasureIds[$treasureName].ToString('x2')
                $arg1 = $Matches['parameter']
            }
            'asm15' {
                $handler = switch -Regex ($command.Operands) {
                    '^scriptHelp\.shootingGallery_checkLinkHasRupees,\s*RUPEEVAL_10$' {
                        'CheckRupees10'; break
                    }
                    '^removeRupeeValue,\s*RUPEEVAL_10$' {
                        'RemoveRupees10'; break
                    }
                    '^fadeoutToWhite$' { 'BeginFadeOutWhite'; break }
                    '^fadeinFromWhite$' { 'BeginFadeInWhite'; break }
                    '^scriptHelp\.shootingGallery_equipSword$' {
                        'EquipSword'; break
                    }
                    '^clearAllItemsAndPutLinkOnGround$' {
                        'ClearItems'; break
                    }
                    '^scriptHelp\.shootingGallery_initLinkPosition$' {
                        'InitLinkForGame'; break
                    }
                    '^scriptHelp\.shootingGallery_setEntranceTiles,\s*\$02$' {
                        'RemoveEntrance'; break
                    }
                    '^scriptHelp\.shootingGallery_beginGame$' {
                        'SpawnGame'; break
                    }
                    '^shootingGallery_restoreEquips$' {
                        'RestoreEquips'; break
                    }
                    '^shootingGallery_setEntranceTiles,\s*\$00$' {
                        'RestoreEntrance'; break
                    }
                    '^shootingGallery_removeAllTargets$' {
                        'RemoveTargets'; break
                    }
                    '^shootingGallery_initLinkPositionAfterGame$' {
                        'InitLinkAfterGame'; break
                    }
                    '^shootingGallery_checkIsNotLinkedGame$' {
                        'CheckNotLinked'; break
                    }
                    '^shootingGallery_cpScore,\s*\$0(?<score>[0-3])$' {
                        "CheckScore$($Matches['score'])"; break
                    }
                    '^shootingGallery_giveRandomRingToLink$' {
                        'GiveRandomRing'; break
                    }
                    '^giveRupees,\s*RUPEEVAL_30$' {
                        'GiveThirtyRupees'; break
                    }
                    '^shootingGallery_giveOneHeart$' {
                        'GiveOneHeart'; break
                    }
                    default {
                        throw "Unsupported $kind shooting-gallery asm15 " +
                            "'$($command.Operands)' at line $($command.Line)."
                    }
                }
                $opcode = 'native'
                $payload = $handler
            }
        }
        $rows.Add((New-CutsceneCommandRow `
            $command.Script $command.Index $command.Label $command.Line `
            $opcode $actor $arg0 $arg1 $payload))
    }
    return $rows
}

$shootingGalleryMainRows = Convert-ShootingGalleryCommandRows `
    $shootingGalleryMainCommands 'main'
$shootingGalleryCleanupRows = Convert-ShootingGalleryCommandRows `
    $shootingGalleryCleanupCommands 'cleanup'
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_main.tsv'),
    $shootingGalleryMainRows)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_cleanup.tsv'),
    $shootingGalleryCleanupRows)

$shootingGalleryRetryIndex = (
    $shootingGalleryMainCommands |
        Where-Object Label -eq '@tryAgain' |
        Select-Object -First 1).Index
if ($shootingGalleryRetryIndex -ne 12) {
    throw "shootingGalleryScript_humanNpc retry entry moved from command 12."
}

$shootingGalleryPositionsMatch = [regex]::Match(
    $shootingGalleryNativeSource,
    '(?ms)^shootingGallery_targetPositions_lynna:\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
$shootingGalleryLayoutsMatch = [regex]::Match(
    $shootingGalleryNativeSource,
    '(?ms)^shootingGallery_targetTiles_lynna:\s*(?<body>.*?)(?=^shootingGallery_targetTiles_goron:)')
$shootingGalleryPositions = @(
    [regex]::Matches(
        $shootingGalleryPositionsMatch.Groups['body'].Value, '\$([0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups[1].Value, 16) })
$shootingGalleryLayoutTiles = @(
    [regex]::Matches(
        $shootingGalleryLayoutsMatch.Groups['body'].Value, '\$([0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups[1].Value, 16) })
if ($shootingGalleryPositions.Count -ne 10 -or
    $shootingGalleryLayoutTiles.Count -ne 100) {
    throw 'Lynna shooting-gallery target position or layout table changed size.'
}
$shootingGalleryTargetRows = [Collections.Generic.List[string]]::new()
$shootingGalleryTargetRows.Add("# index`tpacked-position`tsource")
for ($index = 0; $index -lt 10; $index++) {
    $shootingGalleryTargetRows.Add(
        "$index`t$($shootingGalleryPositions[$index].ToString('x2'))`t" +
        'shootingGallery.s:shootingGallery_targetPositions_lynna')
}
$shootingGalleryLayoutRows = [Collections.Generic.List[string]]::new()
$shootingGalleryLayoutRows.Add(
    "# index`ttile0`ttile1`ttile2`ttile3`ttile4`ttile5`ttile6`ttile7`ttile8`ttile9`tsource")
for ($layout = 0; $layout -lt 10; $layout++) {
    $tiles = for ($index = 0; $index -lt 10; $index++) {
        $shootingGalleryLayoutTiles[$layout * 10 + $index].ToString('x2')
    }
    $shootingGalleryLayoutRows.Add(
        "$layout`t$($tiles -join "`t")`t" +
        'shootingGallery.s:shootingGallery_targetTiles_lynna')
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_targets.tsv'),
    $shootingGalleryTargetRows)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_layouts.tsv'),
    $shootingGalleryLayoutRows)

$shootingGalleryScoreMatch = [regex]::Match(
    $shootingGalleryNativeSource,
    '(?ms)^@scores:\s*(?<body>(?:\s*\.dw \$[0-9a-f]{4}[^\r\n]*\r?\n){21})')
$shootingGalleryBcdScores = @(
    [regex]::Matches(
        $shootingGalleryScoreMatch.Groups['body'].Value, '\.dw \$(?<score>[0-9a-f]{4})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups['score'].Value, 16) })
if ($shootingGalleryBcdScores.Count -ne 21) {
    throw 'Shooting-gallery score table no longer contains 21 entries.'
}
function Convert-ShootingGalleryBcd([int]$value) {
    $subtract = ($value -band 1) -ne 0
    if ($subtract) { $value = $value -band 0xfffe }
    $decimal = (($value -shr 12) -band 0x0f) * 1000 +
        (($value -shr 8) -band 0x0f) * 100 +
        (($value -shr 4) -band 0x0f) * 10 +
        ($value -band 0x0f)
    if ($subtract) { return -$decimal }
    return $decimal
}

$shootingGalleryHitTable = [regex]::Match(
    $shootingGalleryNativeSource,
    '(?ms)^shootingGalleryHitScriptTable:(?<body>.*?)(?=^;;|\z)')
$shootingGalleryHitLabels = @(
    [regex]::Matches(
        $shootingGalleryHitTable.Groups['body'].Value,
        '\.dw mainScripts\.(?<label>[A-Za-z0-9_]+)') |
        ForEach-Object { $_.Groups['label'].Value })
if ($shootingGalleryHitLabels.Count -ne 22) {
    throw 'shootingGalleryHitScriptTable no longer contains 22 scripts.'
}
$shootingGalleryResultRows = [Collections.Generic.List[string]]::new()
$shootingGalleryResultRows.Add(
    "# index`tscore-delta`ttext-id`tsource-line`tutf8-base64`tsource")
for ($index = 0; $index -lt $shootingGalleryHitLabels.Count; $index++) {
    $label = $shootingGalleryHitLabels[$index]
    $textMatch = [regex]::Match(
        $shootingGalleryScriptSource,
        "(?m)^$([regex]::Escape($label)):\s*\r?\n\s*showtext TX_(?<id>[0-9a-f]{4})")
    if (-not $textMatch.Success) {
        throw "Could not resolve shooting-gallery result script $label."
    }
    $textId = [Convert]::ToInt32($textMatch.Groups['id'].Value, 16)
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Missing shooting-gallery result text TX_$($textMatch.Groups['id'].Value)."
    }
    $sourceLine = [regex]::Matches(
        $shootingGalleryScriptSource.Substring(0, $textMatch.Index), "`n").Count + 2
    $scoreDelta = if ($index -lt 20) {
        Convert-ShootingGalleryBcd $shootingGalleryBcdScores[$index]
    } elseif ($index -eq 20) {
        0
    } else {
        Convert-ShootingGalleryBcd $shootingGalleryBcdScores[20]
    }
    $shootingGalleryResultRows.Add(
        "$index`t$scoreDelta`t$($textId.ToString('x4'))`t$sourceLine`t" +
        "$(ConvertTo-CutsceneCommandPayload $allTexts[$textId])`t" +
        "scripts.s:$label")
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_results.tsv'),
    $shootingGalleryResultRows)

$shootingGalleryPrintBody = [regex]::Match(
    $shootingGalleryScriptSource,
    '(?ms)^shootingGallery_printTotalPoints:(?<body>.*?)(?=^shootingGalleryScript_humanNpc_gameDone:)')
if (-not $shootingGalleryPrintBody.Success) {
    throw 'Could not locate shootingGallery_printTotalPoints.'
}
$shootingGalleryPrintStart = $shootingGalleryPrintBody.Groups['body'].Index
$shootingGalleryPrintEnd = $shootingGalleryPrintStart +
    $shootingGalleryPrintBody.Groups['body'].Length
$shootingGalleryPrintRows = @(
    "# wait-line`tbranch-line`tongoing-text-line`tongoing-enable-line`tongoing-end-line`tfinal-text-line`tfinal-enable-line`tfinal-end-line`tongoing-text-id`tfinal-text-id`tongoing-utf8-base64`tfinal-utf8-base64",
    (@(
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*wait 15\s*$' 'shootingGallery_printTotalPoints'),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*jumpifobjectbyteeq Interaction\.var3f, 10, @gameDone.*$' `
            'shootingGallery_printTotalPoints'),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*showtext TX_0813\s*$' 'shootingGallery_printTotalPoints'),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*enableallobjects\s*$' 'shootingGallery_printTotalPoints' 0),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*scriptend\s*$' 'shootingGallery_printTotalPoints' 0),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*showtext TX_0814\s*$' 'shootingGallery_printTotalPoints'),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*enableallobjects\s*$' 'shootingGallery_printTotalPoints' 1),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*scriptend\s*$' 'shootingGallery_printTotalPoints' 1),
        '0813',
        '0814',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x0813]),
        (ConvertTo-CutsceneCommandPayload $allTexts[0x0814])
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_result_script.tsv'),
    $shootingGalleryPrintRows)

$shootingGalleryGraphic = $interactionGraphics['149:0']
$shootingGalleryBallAnimation = Resolve-NpcAnimation 0x95 0
if ($null -eq $shootingGalleryGraphic -or
    -not $gfxNames.ContainsKey($shootingGalleryGraphic.Gfx) -or
    -not $shootingGalleryBallAnimation) {
    throw 'Could not resolve PART_BALL-compatible INTERAC_BALL visual data.'
}
$shootingGalleryBallSprite = $gfxNames[$shootingGalleryGraphic.Gfx]
$shootingGalleryDebrisBlueGraphic = $interactionGraphics['146:4']
$shootingGalleryDebrisRedGraphic = $interactionGraphics['146:5']
$shootingGalleryDebrisAnimation = Resolve-NpcAnimation 0x92 1
if ($null -eq $shootingGalleryDebrisBlueGraphic -or
    $null -eq $shootingGalleryDebrisRedGraphic -or
    $shootingGalleryDebrisBlueGraphic.Gfx -ne 0 -or
    $shootingGalleryDebrisRedGraphic.Gfx -ne 0 -or
    $shootingGalleryDebrisBlueGraphic.TileBase -ne 2 -or
    $shootingGalleryDebrisRedGraphic.TileBase -ne 2 -or
    $shootingGalleryDebrisBlueGraphic.Palette -ne 1 -or
    $shootingGalleryDebrisRedGraphic.Palette -ne 2 -or
    $shootingGalleryDebrisBlueGraphic.DefaultAnimation -ne 1 -or
    $shootingGalleryDebrisRedGraphic.DefaultAnimation -ne 1 -or
    -not $shootingGalleryDebrisAnimation) {
    throw 'Could not resolve shooting-gallery $92:$04/$05 target debris.'
}
$shootingGalleryDebrisRows = @(
    "# sprite`ttile-base`tblue-palette`tred-palette`tanimation`tcount`tlifetime`tspeed`tangle0`tangle1`tangle2`tangle3`tsource",
    "spr_common_sprites`t2`t1`t2`t$shootingGalleryDebrisAnimation`t4`t12`t40`t04`t0c`t14`t1c`tball.s:func_6bca;fallingRock.s:fallingRock_subid04/subid05"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_debris.tsv'),
    $shootingGalleryDebrisRows)

$shootingGalleryEventRows = @(
    "# group`troom`tid`tsubid`tcost`trounds`tretry-command`tcontroller-y`tcontroller-x`tinitial-delay`tpitch-delay`tpuff-delay`tlayout-delay`tbetween-round-delay`tentrance0`tentrance1`topen0`topen1`tclosed0`tclosed1`tfloor`ttarget-blue`ttarget-fairy`ttarget-red`ttarget-imp`tball-fast`tball-slow`tball-reflected`tball-angle`tball-radius-y`tball-radius-x`tball-sprite`tball-tile-base`tball-palette`tball-animation`tfade-frames`tminigame-music`twhistle-sound`tbaseball-sound`tthrow-sound`tslow-sound`tclink-sound`tswitch-sound`terror-sound`tstrike-sound`tpoof-sound`tcan-buy-flute-flag`tflute-score`tring-score`tgasha-score`trupee-score`theart-score`tflute-object`tflute-object-parameter`tgasha-object`tgasha-object-parameter`tsource",
    "2`te9`t30`t00`t10`t10`t$shootingGalleryRetryIndex`t2a`t50`t120`t40`t10`t90`t20`t74`t75`te0`te1`tc6`tc6`ta0`td9`td7`tdc`td8`t64`t3c`t78`t10`t02`t02`t$shootingGalleryBallSprite`t$($shootingGalleryGraphic.TileBase)`t$($shootingGalleryGraphic.Palette)`t$shootingGalleryBallAnimation`t32`t02`tcc`t99`t51`t59`t50`t7e`t5a`ta6`t98`t1d`t50`t350`t250`t150`t50`tTREASURE_OBJECT_FLUTE_00`t0b`tTREASURE_OBJECT_GASHA_SEED_00`t01`tmainData.s:group2Mape9ObjectData;shootingGallery.s;ball.s;fallingRock.s;scripts.s;scriptHelper.s"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_event.tsv'),
    $shootingGalleryEventRows)

$shootingGalleryRingMatch = [regex]::Match(
    $shootingGalleryHelperSource,
    '(?ms)^shootingGallery_giveRandomRingToLink:.*?^@ringList:\s*(?<body>(?:\s*\.db [^\r\n]+\r?\n)+)')
$shootingGalleryRingNames = @(
    [regex]::Matches(
        $shootingGalleryRingMatch.Groups['body'].Value,
        '\b(?<name>[A-Z][A-Z0-9_]+_RING(?:_L2)?)\b') |
        ForEach-Object { $_.Groups['name'].Value })
$ringConstantsSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\rings.s')
$shootingGalleryRingValues = @{}
foreach ($match in [regex]::Matches(
    $ringConstantsSource,
    '(?m)^\s*(?<name>[A-Z][A-Z0-9_]+)\s+db\s+;\s+\$(?<value>[0-9a-f]{2})')) {
    $shootingGalleryRingValues[$match.Groups['name'].Value] =
        [Convert]::ToInt32($match.Groups['value'].Value, 16)
}
if ($shootingGalleryRingNames.Count -ne 16) {
    throw 'Shooting-gallery random ring list no longer contains 16 entries.'
}
$shootingGalleryRingRows = [Collections.Generic.List[string]]::new()
$shootingGalleryRingRows.Add("# index`tring`tsource")
for ($index = 0; $index -lt 16; $index++) {
    $name = $shootingGalleryRingNames[$index]
    if (-not $shootingGalleryRingValues.ContainsKey($name)) {
        throw "Could not resolve shooting-gallery ring constant $name."
    }
    $shootingGalleryRingRows.Add(
        "$index`t$($shootingGalleryRingValues[$name].ToString('x2'))`t" +
        'scriptHelper.s:shootingGallery_giveRandomRingToLink@ringList')
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_rings.tsv'),
    $shootingGalleryRingRows)
