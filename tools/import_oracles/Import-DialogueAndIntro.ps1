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
    0x69, 0x6a, 0x6d, 0x72, 0x83, 0x87, 0x88, 0x89, 0x8b, 0x94, 0x9a,
    0x9c, 0x9d, 0xab, 0xad, 0xba, 0xbf, 0xc3, 0xc4, 0xc8, 0xca,
    0xcb, 0xcc, 0xcd, 0xce, 0xd5, 0xd6, 0xe3
)) { [void]$npcInteractionIds.Add($id) }

# Resolve all text blocks once. This also handles the low-index generic-NPC
# commands, whose source still spells the complete TX_XXXX symbol.
$allTexts = @{}
$allTextPositions = @{}
$allTextMatches = [regex]::Matches(
    $textYaml,
    '(?ms)^  - name: TX_(?<id>[0-9a-f]{4})\r?\n    index: 0x[0-9a-f]{2}\r?\n    text: \|-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)'
)
foreach ($match in $allTextMatches) {
    $lines = $match.Groups['body'].Value -split '\r?\n' | ForEach-Object {
        if ($_.Length -ge 6) { $_.Substring(6) } else { '' }
    }
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') {
        $lines = $lines[0..($lines.Count - 2)]
    }
    $textId = [Convert]::ToInt32($match.Groups['id'].Value, 16)
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
    '(?ms)^  - name:\r?\n(?<names>(?:    - TX_[0-9a-f]{4}\r?\n)+)    index:\r?\n(?:    - 0x[0-9a-f]{2}\r?\n)+    text: \|-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)'
)) {
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
        $allTexts[$textId] = $message
        if ($positionMatch.Success) { $allTextPositions[$textId] = [int]$positionMatch.Groups['position'].Value }
    }
}

# Starting a standard file runs CUTSCENE_PREGAME_INTRO ("Accept our quest,
# hero!") and then linkSummonedCutscene before loading the saved room. Export
# its counters, Link animation records, flags, position, and text rather than
# duplicating those disassembly values in the runtime controller.
$introLinkSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\specialObjects\linkInCutscene.s')
$introCutsceneSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$introGameSource = Get-Content -Raw (Join-Path $Disassembly 'code\bank1.s')
$introAnimationSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')

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

$introSpinAnimation = [regex]::Match(
    $introAnimationSource,
    '(?ms)^animationData19e8f:\s*(?<body>.*?)(?=^animationData19ea9:)')
$introSpinFrames = @([regex]::Matches(
    $introSpinAnimation.Groups['body'].Value,
    '\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<graphic>[0-9a-f]{2})\s+\$00'))
if (-not $introSpinAnimation.Success -or $introSpinFrames.Count -ne 8 -or
    ($introSpinFrames | Where-Object { $_.Groups['duration'].Value -ne '04' }).Count -ne 0) {
    throw 'Unexpected CUTSCENE_PREGAME_INTRO spin animation $08.'
}
$introArrivalAnimation = [regex]::Match(
    $introAnimationSource,
    '(?ms)^animationData19ea9:\s*(?<body>.*?)(?=^animationData19eb4:)')
$introArrivalFrames = @([regex]::Matches(
    $introArrivalAnimation.Groups['body'].Value,
    '\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<graphic>[0-9a-f]{2})\s+\$00'))
if (-not $introArrivalAnimation.Success -or $introArrivalFrames.Count -ne 3 -or
    ($introArrivalFrames | Where-Object { $_.Groups['duration'].Value -ne '04' }).Count -ne 0 -or
    (($introArrivalFrames | ForEach-Object { $_.Groups['graphic'].Value }) -join ',') -ne
        'e4,e8,ec') {
    throw 'Unexpected LINK_ANIM_MODE_FALL animation used by warp transition $0b.'
}
$introVanishAnimation = [regex]::Match(
    $introAnimationSource,
    '(?ms)^animationData19d84:\s*(?<body>.*?)(?=^animationData19d90:)')
$introVanishFrames = @([regex]::Matches(
    $introVanishAnimation.Groups['body'].Value,
    '\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<graphic>[0-9a-f]{2})\s+\$(?:00|ff)'))
if (-not $introVanishAnimation.Success -or $introVanishFrames.Count -ne 4) {
    throw 'Unexpected CUTSCENE_PREGAME_INTRO vanish animation $05.'
}
function Read-IntroOscillation([string]$label) {
    $pattern = '(?m)^' + [regex]::Escape($label) +
        ':\s*\r?\n\s*\.db\s+(?<values>(?:\$[0-9a-f]{2}\s*){8})'
    $match = [regex]::Match($introLinkSource, $pattern)
    if (-not $match.Success) { throw "Could not parse $label." }
    return @([regex]::Matches(
        $match.Groups['values'].Value, '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object { $_.Groups['value'].Value })
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
$introSpinGraphics = $introSpinFrames | ForEach-Object { $_.Groups['graphic'].Value }
$introArrivalDurations = $introArrivalFrames | ForEach-Object {
    $_.Groups['duration'].Value
}
# Transition $0b runs on normal SPECIALOBJECT_LINK ($00), not
# SPECIALOBJECT_LINK_CUTSCENE ($08). Link faces DIR_DOWN, and the normal Link
# graphics loader adds that direction to graphic indices beginning at $54.
$introArrivalGraphics = $introArrivalFrames | ForEach-Object {
    $graphic = [Convert]::ToInt32($_.Groups['graphic'].Value, 16)
    if ($graphic -ge 0x54) { $graphic += 2 }
    $graphic.ToString('x2')
}
$introVanishDurations = $introVanishFrames | ForEach-Object { $_.Groups['duration'].Value }
$introVanishGraphics = $introVanishFrames | ForEach-Object { $_.Groups['graphic'].Value }
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
New-Item -ItemType Directory -Force -Path (
    Join-Path $destination 'cutscenes') | Out-Null
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\new_game_intro.tsv'),
    $introRows,
    [Text.UTF8Encoding]::new($false))

# Resolve every OAM part used by Link and both INTERAC_SPARKLE objects in the
# pregame intro. Object coordinates and OAM offsets retain their original
# unsigned bytes; the runtime applies the GBC's byte wrapping and hardware
# sprite origin biases when drawing them.
$specialOamSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\specialObjectOamData.s')
$interactionDataSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\interactionData.s')
$interactionAnimationSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\interactionAnimations.s')
$interactionOamSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\interactionOamData.s')
$objectGfxHeaderSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\objectGfxHeaders.s')

function Read-IntroOamParts([string]$source, [string]$label) {
    $pattern = '(?ms)^' + [regex]::Escape($label) +
        ':\s*\.db\s+\$(?<count>[0-9a-f]{2})(?<body>.*?)(?=^[A-Za-z0-9_]+:|\z)'
    $match = [regex]::Match(
        $source,
        $pattern)
    if (-not $match.Success) { throw "Could not parse intro OAM record $label." }
    $count = [Convert]::ToInt32($match.Groups['count'].Value, 16)
    $parts = @([regex]::Matches(
        $match.Groups['body'].Value,
        '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})') |
        Select-Object -First $count)
    if ($parts.Count -ne $count) {
        throw "Intro OAM record $label declares $count parts but contains $($parts.Count)."
    }
    return ($parts | ForEach-Object {
        "$($_.Groups['y'].Value),$($_.Groups['x'].Value),$($_.Groups['tile'].Value),$($_.Groups['flags'].Value)"
    }) -join ';'
}

$specialGfxBlock = [regex]::Match(
    $introAnimationSource,
    '(?ms)^specialObject08GfxPointers:(?<body>.*?)(?=^specialObject02GfxPointers:)')
$specialGfxRows = @([regex]::Matches(
    $specialGfxBlock.Groups['body'].Value,
    'm_SpecialObjectGfxPointer\s+\$(?<oam>[0-9a-f]{2})\s+spr_link\s+\$(?<source>[0-9a-f]{4})\s+\$[0-9a-f]{2}'))
$specialOamPointerBlock = [regex]::Match(
    $introAnimationSource,
    '(?ms)^specialObject08OamDataPointers:\s*(?:^specialObject09OamDataPointers:\s*)?(?<body>.*?)(?=^specialObject0aOamDataPointers:)')
$specialOamLabels = @([regex]::Matches(
    $specialOamPointerBlock.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>[A-Za-z0-9_]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if (-not $specialGfxBlock.Success -or $specialGfxRows.Count -lt 0xef -or
    -not $specialOamPointerBlock.Success -or $specialOamLabels.Count -lt 0x15) {
    throw 'Could not resolve SPECIALOBJECT_LINK_CUTSCENE graphics and OAM tables.'
}

$introSpriteRows = [Collections.Generic.List[string]]::new()
$introSpriteRows.Add('# kind`tindex`tduration`tsource-offset`tbase-palette`toam-parts')
function Add-LinkIntroSpriteRows([string]$kind, $durations, $graphics) {
    for ($frame = 0; $frame -lt $graphics.Count; $frame++) {
        $graphic = [Convert]::ToInt32($graphics[$frame], 16)
        $gfx = $specialGfxRows[$graphic]
        $oamIndex = [Convert]::ToInt32($gfx.Groups['oam'].Value, 16)
        $parts = Read-IntroOamParts $specialOamSource $specialOamLabels[$oamIndex]
        $duration = [Convert]::ToInt32($durations[$frame], 16)
        $introSpriteRows.Add(
            "$kind`t$frame`t$duration`t$($gfx.Groups['source'].Value)`t0`t$parts")
    }
}
$spinDurations = @(0..($introSpinGraphics.Count - 1) | ForEach-Object { '04' })
Add-LinkIntroSpriteRows 'link-spin' $spinDurations $introSpinGraphics
Add-LinkIntroSpriteRows 'link-vanish' $introVanishDurations $introVanishGraphics
Add-LinkIntroSpriteRows 'link-arrival' $introArrivalDurations $introArrivalGraphics

$sparkleSubids = [regex]::Match(
    $interactionDataSource,
    '(?ms)^interaction84SubidData:(?<body>.*?)(?=^interaction92SubidData:)')
$sparkleRows = @([regex]::Matches(
    $sparkleSubids.Groups['body'].Value,
    'm_InteractionSubidData\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})'))
if (-not $sparkleSubids.Success -or $sparkleRows.Count -ne 16 -or
    $sparkleRows[0x0d].Groups['gfx'].Value -ne '3a' -or
    $sparkleRows[0x06].Groups['gfx'].Value -ne '3a') {
    throw 'Could not resolve INTERAC_SPARKLE subids $0d and $06.'
}
$sparkleGfx = [regex]::Match(
    $objectGfxHeaderSource,
    '(?m)^\s*/\* \$3a \*/ m_ObjectGfxHeader spr_link, \$(?<tile>[0-9a-f]{2}), \$(?<source>[0-9a-f]{4})')
if (-not $sparkleGfx.Success -or $sparkleGfx.Groups['source'].Value -ne '1c00') {
    throw 'INTERAC_SPARKLE intro graphics no longer resolve through object header $3a.'
}
$interaction84Animations = [regex]::Match(
    $interactionAnimationSource,
    '(?ms)^interaction84Animations:(?<body>.*?)(?=^interaction86Animations:)')
$interaction84AnimationLabels = @([regex]::Matches(
    $interaction84Animations.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>[A-Za-z0-9_]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$interaction84OamPointers = [regex]::Match(
    $interactionAnimationSource,
    '(?ms)^interaction84OamDataPointers:[^\r\n]*\r?\n(?<body>.*?)(?=^interaction86OamDataPointers:)')
$interaction84OamLabels = @([regex]::Matches(
    $interaction84OamPointers.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>[A-Za-z0-9_]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($interaction84AnimationLabels.Count -ne 5 -or $interaction84OamLabels.Count -ne 11) {
    throw 'Could not resolve INTERAC_SPARKLE animation and OAM pointer tables.'
}
function Add-SparkleIntroSpriteRows([string]$kind, [int]$subid) {
    $flags = [Convert]::ToInt32($sparkleRows[$subid].Groups['flags'].Value, 16)
    $tileBase = [Convert]::ToInt32(
        $sparkleRows[$subid].Groups['tile'].Value, 16) -band 0x7f
    $animationIndex = $flags -band 0x0f
    $basePalette = ($flags -shr 4) -band 0x0f
    $effectiveSource = [Convert]::ToInt32(
        $sparkleGfx.Groups['source'].Value, 16) + $tileBase * 16
    $label = $interaction84AnimationLabels[$animationIndex]
    $animationStart = $interactionAnimationSource.IndexOf(
        "${label}:", [StringComparison]::Ordinal)
    $nextLabelIndex = -1
    foreach ($candidate in [regex]::Matches(
        $interactionAnimationSource.Substring($animationStart + $label.Length + 1),
        '(?m)^interactionAnimation(?<suffix>[A-Za-z0-9_]+):')) {
        $candidateLabel = "interactionAnimation$($candidate.Groups['suffix'].Value)"
        if ($candidateLabel -ne "${label}Loop") {
            $nextLabelIndex = $animationStart + $label.Length + 1 + $candidate.Index
            break
        }
    }
    if ($nextLabelIndex -lt 0) { $nextLabelIndex = $interactionAnimationSource.Length }
    $animationBody = $interactionAnimationSource.Substring(
        $animationStart,
        $nextLabelIndex - $animationStart)
    $frames = @([regex]::Matches(
        $animationBody,
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<oam>[0-9a-f]{2})\s+\$[0-9a-f]{2}'))
    if ($frames.Count -lt 2) { throw "Could not parse $kind animation $label." }
    for ($frame = 0; $frame -lt $frames.Count; $frame++) {
        $oamIndex = [Convert]::ToInt32($frames[$frame].Groups['oam'].Value, 16) / 2
        $parts = Read-IntroOamParts $interactionOamSource $interaction84OamLabels[$oamIndex]
        $duration = [Convert]::ToInt32($frames[$frame].Groups['duration'].Value, 16)
        $introSpriteRows.Add(
            "$kind`t$frame`t$duration`t$($effectiveSource.ToString('x4'))`t$basePalette`t$parts")
    }
}
Add-SparkleIntroSpriteRows 'orb-descend' 0x0d
Add-SparkleIntroSpriteRows 'orb-vanish' 0x06
if ($introSpriteRows.Count -ne 22) {
    throw "Expected 21 new-game intro sprite frames, exported $($introSpriteRows.Count - 1)."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\new_game_intro_sprites.tsv'),
    $introSpriteRows,
    [Text.UTF8Encoding]::new($false))

