# NPC extraction. Interaction objects are split between the room object table,
# interactionData.s (graphics), and the script/text tables. Keep the list of
# character interaction codes here: other interaction codes are scenery,
# triggers, enemies, or cutscene-only helpers even when they have text.
$npcInteractionIds = [Collections.Generic.HashSet[int]]::new()
foreach ($id in @(
    0x10, 0x28, 0x29, 0x2a, 0x2b, 0x2e, 0x30, 0x31, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x3b, 0x3c, 0x3d,
    0x3f, 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x48, 0x49,
    0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50, 0x51, 0x52, 0x53, 0x54,
    0x55, 0x57, 0x58, 0x59, 0x5a, 0x5b, 0x5c, 0x5d, 0x5f, 0x65, 0x66, 0x68,
    0x69, 0x6a, 0x6d, 0x72, 0x73, 0x83, 0x87, 0x88, 0x89, 0x8b, 0x94, 0x9a,
    0x9c, 0x9d, 0xab, 0xad, 0xba, 0xbf, 0xc3, 0xc4, 0xc8, 0xca,
    0xcb, 0xcc, 0xcd, 0xce, 0xd5, 0xd6, 0xe3, 0xe5
)) { [void]$npcInteractionIds.Add($id) }

# Resolve all text blocks once. This also handles the low-index generic-NPC
# commands, whose source still spells the complete TX_XXXX symbol.
$allTexts = @{}
$allTextPositions = @{}
$allTextIdsByName = @{}
foreach ($name in [regex]::Matches($textYaml, '(?m)^  - name: TX_(?<id>[0-9a-f]{4})$')) {
    $allTextIdsByName["TX_$($name.Groups['id'].Value)"] =
        [Convert]::ToInt32($name.Groups['id'].Value, 16)
}
foreach ($emptyText in [regex]::Matches(
    $textYaml,
    '(?m)^  - name: TX_(?<id>[0-9a-f]{4})\r?\n    index: (?:0x[0-9a-f]{2}|auto)\r?\n    text: ""$')) {
    $allTexts[[Convert]::ToInt32($emptyText.Groups['id'].Value, 16)] = ''
}
$allTextMatches = [regex]::Matches(
    $textYaml,
    '(?ms)^  - name: TX_(?<id>[0-9a-f]{4})\r?\n    index: 0x[0-9a-f]{2}\r?\n    text: \|(?:\d+)?-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)'
)
foreach ($match in $allTextMatches) {
    $lines = $match.Groups['body'].Value -split '\r?\n' | ForEach-Object {
        if ($_.Length -ge 6) { $_.Substring(6) } else { '' }
    }
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') {
        $lines = $lines[0..($lines.Count - 2)]
    }
    $textId = [Convert]::ToInt32($match.Groups['id'].Value, 16)
    $allTextIdsByName["TX_$($match.Groups['id'].Value)"] = $textId
    $rawText = $lines -join "`n"
    $allTexts[$textId] = Normalize-DialogueText $rawText
    $positionMatch = [regex]::Match($rawText, '\\pos\((?<position>\d+)\)')
    if ($positionMatch.Success) { $allTextPositions[$textId] = [int]$positionMatch.Groups['position'].Value }
}
# Shared text bodies use a YAML name/index list. Resolve every alias as well;
# cutscene scripts refer to the individual TX_* names even when several IDs
# intentionally share one body.
foreach ($match in [regex]::Matches(
    $textYaml,
    '(?ms)^  - name:\r?\n(?<names>(?:    - TX_[0-9a-f]{4}\r?\n)+)' +
    '    index:\r?\n(?:    - 0x[0-9a-f]{2}\r?\n)+    text: ' +
    '\|(?:\d+)?-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)' )
) {
    $lines = $match.Groups['body'].Value -split '\r?\n' | ForEach-Object {
        if ($_.Length -ge 6) { $_.Substring(6) } else { '' }
    }
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') {
        $lines = $lines[0..($lines.Count - 2)]
    }
    $rawText = $lines -join "`n"
    $message = Normalize-DialogueText $rawText
    $positionMatch = [regex]::Match($rawText, '\\pos\((?<position>\d+)\)')
    foreach ($name in [regex]::Matches($match.Groups['names'].Value, 'TX_(?<id>[0-9a-f]{4})')) {
        $textId = [Convert]::ToInt32($name.Groups['id'].Value, 16)
        $allTextIdsByName["TX_$($name.Groups['id'].Value)"] = $textId
        $allTexts[$textId] = $message
        if ($positionMatch.Success) { $allTextPositions[$textId] = [int]$positionMatch.Groups['position'].Value }
    }
}

# Preserve physical text adjacency for source records that deliberately omit
# their $00 terminator. Consumers still decide whether execution actually
# reaches the next record; commands such as \jump can redirect first.
$textEntryHeaders = [Collections.Generic.List[object]]::new()
foreach ($match in [regex]::Matches(
    $textYaml,
    '(?m)^  - name: TX_(?<id>[0-9a-f]{4})$')) {
    $textEntryHeaders.Add([pscustomobject]@{
        Position = $match.Index
        Id = [Convert]::ToInt32($match.Groups['id'].Value, 16)
    })
}
foreach ($match in [regex]::Matches(
    $textYaml,
    '(?ms)^  - name:\r?\n(?<names>(?:    - TX_[0-9a-f]{4}\r?\n)+)')) {
    $firstName = [regex]::Match(
        $match.Groups['names'].Value,
        'TX_(?<id>[0-9a-f]{4})')
    $textEntryHeaders.Add([pscustomobject]@{
        Position = $match.Index
        Id = [Convert]::ToInt32($firstName.Groups['id'].Value, 16)
    })
}
$orderedTextEntryHeaders = @($textEntryHeaders | Sort-Object Position)
$allTextFallthroughIds = @{}
for ($index = 0; $index -lt $orderedTextEntryHeaders.Count; $index++) {
    $entry = $orderedTextEntryHeaders[$index]
    $entryEnd = if ($index + 1 -lt $orderedTextEntryHeaders.Count) {
        $orderedTextEntryHeaders[$index + 1].Position
    } else {
        $textYaml.Length
    }
    $entrySource = $textYaml.Substring(
        $entry.Position,
        $entryEnd - $entry.Position)
    if ($entrySource -notmatch '(?m)^    null_terminator: False\s*$') {
        continue
    }
    if ($index + 1 -ge $orderedTextEntryHeaders.Count) {
        throw "Unterminated final text TX_$($entry.Id.ToString('x4')) has no successor."
    }
    $successor = $orderedTextEntryHeaders[$index + 1]
    if (($entry.Id -shr 8) -ne ($successor.Id -shr 8)) {
        throw "Unterminated TX_$($entry.Id.ToString('x4')) crosses its text group boundary."
    }
    $allTextFallthroughIds.Add($entry.Id, $successor.Id)
}

# CROSSITEMS appends symbolic TX_09_* rows with `index: auto`. Resolve those
# sequential indices as the text compiler does so treasure display records can
# retain their real low text byte instead of degrading every TX symbol to $ff.
$group09 = [regex]::Match(
    $textYaml,
    '(?ms)^- group: 0x09\r?\n(?<body>.*?)(?=^- group: 0x0a\r?$)')
if (-not $group09.Success) { throw 'Could not parse inventory text group $09.' }
$nextGroup09Index = 0
foreach ($match in [regex]::Matches(
    $group09.Groups['body'].Value,
    '(?ms)^  - name: (?<name>TX_09[A-Z0-9_]+)\r?\n    index: (?<index>auto|0x[0-9a-f]{2})\r?\n    text: \|-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)'
)) {
    $indexText = $match.Groups['index'].Value
    $index = if ($indexText -eq 'auto') {
        $nextGroup09Index
    } else {
        [Convert]::ToInt32($indexText.Substring(2), 16)
    }
    $nextGroup09Index = $index + 1
    if ($indexText -ne 'auto') { continue }

    $lines = $match.Groups['body'].Value -split '\r?\n' | ForEach-Object {
        if ($_.Length -ge 6) { $_.Substring(6) } else { '' }
    }
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') {
        $lines = $lines[0..($lines.Count - 2)]
    }
    $textId = 0x0900 -bor $index
    $allTextIdsByName[$match.Groups['name'].Value] = $textId
    $allTexts[$textId] = Normalize-DialogueText ($lines -join "`n")
}

# Starting a standard file runs CUTSCENE_PREGAME_INTRO ("Accept our quest,
# hero!") and then linkSummonedCutscene before loading the saved room. Export
# its counters, Link animation records, flags, position, and text rather than
# duplicating those disassembly values in the runtime controller.
$introLinkSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\specialObjects\linkInCutscene.s')
$introLinkPath = Join-Path `
    $Disassembly 'object_code\ages\specialObjects\linkInCutscene.s'
$introCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$introGameSource = Read-ImportText (Join-Path $Disassembly 'code\bank1.s')
$introAnimationPath =
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s'

$introLinkBlock = [regex]::Match(
    $introLinkSource,
    '(?ms)^linkCutsceneB:(?<body>.*?)(?=^linkCutsceneC:)')
if (-not $introLinkBlock.Success) { throw 'Could not parse linkCutsceneB.' }
$introLinkInit = [regex]::Match(
    $introLinkBlock.Groups['body'].Value,
    '(?ms)ld l,SpecialObject\.counter1\s+ld \(hl\),\$(?<waitLo>[0-9a-f]{2})\s+inc hl\s+ld \(hl\),\$(?<waitHi>[0-9a-f]{2}).*?ld l,SpecialObject\.yh\s+ld \(hl\),\$(?<y>[0-9a-f]{2})\s+ld l,SpecialObject\.xh\s+ld \(hl\),\$(?<x>[0-9a-f]{2}).*?ld a,\$(?<animation>[0-9a-f]{2})\s+call specialObjectSetAnimation')
if (-not $introLinkInit.Success -or $introLinkInit.Groups['animation'].Value -ne '08') {
    throw 'Could not parse CUTSCENE_PREGAME_INTRO Link initialization.'
}
$introVoiceWait = [regex]::Match(
    $introLinkBlock.Groups['body'].Value,
    '(?ms)ld \(hl\),\$(?<frames>[0-9a-f]{2})\s+jp itemIncSubstate.*?ld bc,TX_(?<text>[0-9a-f]{4})')
if (-not $introVoiceWait.Success -or $introVoiceWait.Groups['text'].Value -ne '1213') {
    throw 'Could not parse CUTSCENE_PREGAME_INTRO voice wait and TX_1213.'
}
$introPregameBlock = [regex]::Match(
    $introCutsceneSource,
    '(?ms)^pregameIntroCutsceneHandler:(?<body>.*?)(?=^func_6e9a:)')
if (-not $introPregameBlock.Success) { throw 'Could not parse pregameIntroCutsceneHandler.' }
$introPostWait = [regex]::Match(
    $introPregameBlock.Groups['body'].Value,
    '(?ms)@stateB:.*?ld \(hl\),\$(?<frames>[0-9a-f]{2}).*?@stateC:.*?ld a,GLOBALFLAG_(?<flag>[0-9a-f]{2})')
if (-not $introPostWait.Success -or $introPostWait.Groups['flag'].Value -ne '3d') {
    throw 'Could not parse pregame intro post-vanish wait and Link-summoned flag.'
}
$introSummonBlock = [regex]::Match(
    $introGameSource,
    '(?ms)^linkSummonedCutscene:(?<body>.*?)(?=^\.ifdef ROM_SEASONS)')
if (-not $introSummonBlock.Success -or
    $introSummonBlock.Groups['body'].Value -notmatch 'ld a,GLOBALFLAG_PREGAME_INTRO_DONE') {
    throw 'Could not parse linkSummonedCutscene.'
}
# The wave counter decreases from $ff to below $80 and then to zero, two
# units per update: 64 updates per half, or 128 updates total.
$introSummonFrames = 128

function Read-IntroAnimation([string]$label) {
    return @(Read-AssemblyDataDirectives $introAnimationPath $label '.db' |
        Where-Object { $_.Operands.Count -ge 3 } | ForEach-Object {
            [pscustomobject]@{
                Duration = $_.Operands[0].TrimStart('$')
                Graphic = $_.Operands[1].TrimStart('$')
                Parameter = $_.Operands[2].TrimStart('$')
            }
        })
}
$introSpinFrames = @(Read-IntroAnimation 'animationData19e8f')
if ($introSpinFrames.Count -ne 8 -or
    ($introSpinFrames | Where-Object Duration -ne '04').Count -ne 0) {
    throw 'Unexpected CUTSCENE_PREGAME_INTRO spin animation $08.'
}
$introArrivalFrames = @(Read-IntroAnimation 'animationData19ea9')
if ($introArrivalFrames.Count -ne 3 -or
    ($introArrivalFrames | Where-Object Duration -ne '04').Count -ne 0 -or
    (($introArrivalFrames | ForEach-Object Graphic) -join ',') -ne
        'e4,e8,ec') {
    throw 'Unexpected LINK_ANIM_MODE_FALL animation used by warp transition $0b.'
}
$introVanishFrames = @(Read-IntroAnimation 'animationData19d84')
if ($introVanishFrames.Count -ne 4) {
    throw 'Unexpected CUTSCENE_PREGAME_INTRO vanish animation $05.'
}
$harpFrames = @(Read-IntroAnimation 'animationData19faa')
if ($harpFrames.Count -ne 17 -or
    (($harpFrames | Select-Object -First 13 | ForEach-Object Duration) -join ',') -ne
        '14,14,0c,14,14,0c,14,14,0c,14,14,0c,14' -or
    (($harpFrames | Select-Object -First 13 | ForEach-Object Graphic) -join ',') -ne
        '34,35,34,36,37,36,34,35,34,36,37,36,36') {
    throw 'Unexpected LINK_ANIM_MODE_HARP_2 response animation $1e.'
}
function Read-IntroOscillation([string]$label) {
    return @(Read-AssemblyLiteralValues $introLinkPath $label |
        ForEach-Object { $_.ToString('x2') })
}
$introHoverOscillationValues = Read-IntroOscillation 'linkCutscene_zOscillation1'
$introDescendOscillationValues = Read-IntroOscillation 'linkCutscene_zOscillation2'
$introTextId = [Convert]::ToInt32($introVoiceWait.Groups['text'].Value, 16)
if (-not $allTexts.ContainsKey($introTextId) -or
    -not $allTextPositions.ContainsKey($introTextId) -or
    $allTextPositions[$introTextId] -ne 2) {
    throw 'Expected CUTSCENE_PREGAME_INTRO TX_1213 with textbox position 2.'
}
$introInitialWait =
    ([Convert]::ToInt32($introLinkInit.Groups['waitHi'].Value, 16) -shl 8) -bor
    [Convert]::ToInt32($introLinkInit.Groups['waitLo'].Value, 16)
$introSpinGraphics = $introSpinFrames | ForEach-Object Graphic
$introArrivalDurations = $introArrivalFrames | ForEach-Object Duration
# Transition $0b runs on normal SPECIALOBJECT_LINK ($00), not
# SPECIALOBJECT_LINK_CUTSCENE ($08). Link faces DIR_DOWN, and the normal Link
# graphics loader adds that direction to graphic indices beginning at $54.
$introArrivalGraphics = $introArrivalFrames | ForEach-Object {
    $graphic = [Convert]::ToInt32($_.Graphic, 16)
    if ($graphic -ge 0x54) { $graphic += 2 }
    $graphic.ToString('x2')
}
$introVanishDurations = $introVanishFrames | ForEach-Object Duration
$introVanishGraphics = $introVanishFrames | ForEach-Object Graphic
$introColumns = @(
    $introInitialWait.ToString(),
    [Convert]::ToInt32($introVoiceWait.Groups['frames'].Value, 16).ToString(),
    [Convert]::ToInt32($introPostWait.Groups['frames'].Value, 16).ToString(),
    $introSummonFrames.ToString(),
    [Convert]::ToInt32($introLinkInit.Groups['x'].Value, 16).ToString(),
    [Convert]::ToInt32($introLinkInit.Groups['y'].Value, 16).ToString(),
    '3d', '21', '2', $introTextId.ToString(), '4',
    ($introSpinGraphics -join ','),
    ($introVanishDurations -join ','),
    ($introVanishGraphics -join ','),
    ($introDescendOscillationValues -join ','),
    ($introHoverOscillationValues -join ','),
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$introTextId]))
)
$introRows = @(
    '# initial-wait`tvoice-wait`tpost-vanish-wait`tsummon-frames`tlink-x`tlink-y`tlink-summoned-flag`tpregame-done-flag`ttextbox-position`ttext-id`tspin-duration`tspin-graphics`tvanish-durations`tvanish-graphics`tdescend-oscillation`thover-oscillation`ttext-base64',
    ($introColumns -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\new_game_intro.tsv'),
    $introRows)

# Resolve every OAM part used by Link and both INTERAC_SPARKLE objects in the
# pregame intro. Object coordinates and OAM offsets retain their original
# unsigned bytes; the runtime applies the GBC's byte wrapping and hardware
# sprite origin biases when drawing them.
$specialOamPath = Join-Path $Disassembly 'data\ages\specialObjectOamData.s'
$interactionDataPath = Join-Path $Disassembly 'data\ages\interactionData.s'
$interactionAnimationPath =
    Join-Path $Disassembly 'data\ages\interactionAnimations.s'
$interactionOamPath = Join-Path $Disassembly 'data\ages\interactionOamData.s'
$objectGfxHeaderPath = Join-Path $Disassembly 'data\ages\objectGfxHeaders.s'
$objectGfxHeaderSource = Read-ImportText $objectGfxHeaderPath

function Read-IntroOamParts([string]$path, [string]$label) {
    $rows = @(Read-AssemblyDataDirectives $path $label '.db')
    if ($rows.Count -eq 0) { throw "Could not parse intro OAM record $label." }
    $count = Convert-AssemblyInteger $rows[0].Operands[0]
    $parts = @($rows | Select-Object -Skip 1 -First $count)
    if ($parts.Count -ne $count) {
        throw "Intro OAM record $label declares $count parts but contains $($parts.Count)."
    }
    return ($parts | ForEach-Object {
        (($_.Operands | Select-Object -First 4) -replace '^\$', '') -join ','
    }) -join ';'
}

$specialGfxRows = @(Read-AssemblyMacroInvocations `
    $introAnimationPath 'specialObject08GfxPointers' 'm_SpecialObjectGfxPointer')
$specialOamLabels = @(Read-AssemblyDataDirectives `
    $introAnimationPath 'specialObject09OamDataPointers' '.dw' |
    ForEach-Object { $_.Operands[0] })
if ($specialGfxRows.Count -lt 0xef -or $specialOamLabels.Count -lt 0x15) {
    throw 'Could not resolve SPECIALOBJECT_LINK_CUTSCENE graphics and OAM tables.'
}

$introSpriteRows = [Collections.Generic.List[string]]::new()
$introSpriteRows.Add('# kind`tindex`tduration`tsource-offset`tbase-palette`toam-parts')
function Add-LinkIntroSpriteRows(
    [string]$kind,
    $durations,
    $graphics,
    [bool]$retainPartialGraphics = $false) {
    $vramTiles = [int[]]::new(0x100)
    for ($tile = 0; $tile -lt $vramTiles.Length; $tile++) {
        $vramTiles[$tile] = -1
    }
    for ($frame = 0; $frame -lt $graphics.Count; $frame++) {
        $graphic = [Convert]::ToInt32($graphics[$frame], 16)
        $gfx = $specialGfxRows[$graphic]
        $oamIndex = Convert-AssemblyInteger $gfx.Operands[0]
        $parts = Read-IntroOamParts $specialOamPath $specialOamLabels[$oamIndex]
        $sourceOffset = $gfx.Operands[2].TrimStart('$')
        if ($retainPartialGraphics) {
            $sourceTile = [Convert]::ToInt32($sourceOffset, 16) / 16
            $loadedTileCount = Convert-AssemblyInteger $gfx.Operands[3]
            for ($tile = 0; $tile -lt $loadedTileCount; $tile++) {
                $vramTiles[$tile] = $sourceTile + $tile
            }
            $parts = (@($parts -split ';' | ForEach-Object {
                $fields = $_ -split ','
                if ($fields.Count -ne 4) {
                    throw "Malformed $kind OAM block: $_"
                }
                $vramTile = [Convert]::ToInt32($fields[2], 16) -band 0xfe
                if ($vramTile -ge 0xff -or
                    $vramTiles[$vramTile] -lt 0 -or
                    $vramTiles[$vramTile + 1] -ne
                        $vramTiles[$vramTile] + 1) {
                    throw "$kind frame $frame graphic `$$($graphic.ToString('x2')) references unresolved VRAM tile `$$($vramTile.ToString('x2'))."
                }
                "$($fields[0]),$($fields[1]),$($vramTiles[$vramTile].ToString('x2')),$($fields[3])"
            }) -join ';')
            $sourceOffset = '0000'
        }
        $duration = [Convert]::ToInt32($durations[$frame], 16)
        $introSpriteRows.Add(
            "$kind`t$frame`t$duration`t$sourceOffset`t0`t$parts")
    }
}
$spinDurations = @(0..($introSpinGraphics.Count - 1) | ForEach-Object { '04' })
Add-LinkIntroSpriteRows 'link-spin' $spinDurations $introSpinGraphics
Add-LinkIntroSpriteRows 'link-vanish' $introVanishDurations $introVanishGraphics
Add-LinkIntroSpriteRows 'link-arrival' $introArrivalDurations $introArrivalGraphics
$harpDurations = @($harpFrames | Select-Object -First 13 |
    ForEach-Object Duration)
$harpGraphics = @($harpFrames | Select-Object -First 13 |
    ForEach-Object Graphic)
Add-LinkIntroSpriteRows 'link-harp' $harpDurations $harpGraphics $true
$playableHarpDurations = @($harpFrames | Select-Object -First 17 |
    ForEach-Object Duration)
$playableHarpGraphics = @($harpFrames | Select-Object -First 17 |
    ForEach-Object Graphic)
Add-LinkIntroSpriteRows `
    'link-harp-item' $playableHarpDurations $playableHarpGraphics $true

$sparkleRows = @(Read-AssemblyMacroInvocations `
    $interactionDataPath 'interaction84SubidData' 'm_InteractionSubidData')
if ($sparkleRows.Count -ne 16 -or
    (Convert-AssemblyInteger $sparkleRows[0x0d].Operands[0]) -ne 0x3a -or
    (Convert-AssemblyInteger $sparkleRows[0x06].Operands[0]) -ne 0x3a) {
    throw 'Could not resolve INTERAC_SPARKLE subids $0d and $06.'
}
$sparkleGfx = @(Read-AssemblyMacroInvocations `
    $objectGfxHeaderPath '' 'm_ObjectGfxHeader' |
    Where-Object Comment -eq '$3a')
if ($sparkleGfx.Count -ne 1 -or $sparkleGfx[0].Operands[2] -ine '$1c00') {
    throw 'INTERAC_SPARKLE intro graphics no longer resolve through object header $3a.'
}
$interaction84AnimationLabels = @(Read-AssemblyDataDirectives `
    $interactionAnimationPath 'interactiondeAnimations' '.dw' |
    ForEach-Object { $_.Operands[0] })
$interaction84OamLabels = @(Read-AssemblyDataDirectives `
    $interactionAnimationPath 'interaction84OamDataPointers' '.dw' |
    ForEach-Object { $_.Operands[0] })
if ($interaction84AnimationLabels.Count -ne 5 -or $interaction84OamLabels.Count -ne 11) {
    throw 'Could not resolve INTERAC_SPARKLE animation and OAM pointer tables.'
}
$interactionAnimationDefinitions = Read-AssemblyAnimationDefinitions `
    $interactionAnimationPath 'interactionAnimation[0-9a-f]+(?:Loop)?'
function Add-SparkleIntroSpriteRows([string]$kind, [int]$subid) {
    $flags = Convert-AssemblyInteger $sparkleRows[$subid].Operands[2]
    $tileBase =
        (Convert-AssemblyInteger $sparkleRows[$subid].Operands[1]) -band 0x7f
    $animationIndex = $flags -band 0x0f
    $basePalette = ($flags -shr 4) -band 0x0f
    $effectiveSource =
        (Convert-AssemblyInteger $sparkleGfx[0].Operands[2]) + $tileBase * 16
    $label = $interaction84AnimationLabels[$animationIndex]
    $frames = @($interactionAnimationDefinitions[$label].Frames)
    if ($frames.Count -lt 2) { throw "Could not parse $kind animation $label." }
    for ($frame = 0; $frame -lt $frames.Count; $frame++) {
        $oamIndex = $frames[$frame].PointerOffset / 2
        $parts = Read-IntroOamParts `
            $interactionOamPath $interaction84OamLabels[$oamIndex]
        $duration = $frames[$frame].Duration
        $introSpriteRows.Add(
            "$kind`t$frame`t$duration`t$($effectiveSource.ToString('x4'))`t$basePalette`t$parts")
    }
}
Add-SparkleIntroSpriteRows 'orb-descend' 0x0d
Add-SparkleIntroSpriteRows 'orb-vanish' 0x06
if ($introSpriteRows.Count -ne 52) {
    throw "Expected 51 shared Link/intro sprite frames, exported $($introSpriteRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\new_game_intro_sprites.tsv'),
    $introSpriteRows)
