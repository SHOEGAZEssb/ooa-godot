# File-menu OAM and Ages frontend presentation. Shared inventory, map and ring
# records have their own per-game producers.
$menuBank2Path = Join-Path $Disassembly 'code\bank2.s'

function Get-MenuLocalBlock(
    [string]$path,
    [string]$globalLabel,
    [string]$localLabel
) {
    $nodes = @(Read-AssemblyLabelNodes $path $globalLabel)
    $start = -1
    for ($index = 0; $index -lt $nodes.Count; $index++) {
        if ($nodes[$index].Kind -eq 'Label' -and
            $nodes[$index].Name -ceq $localLabel) {
            if ($start -ge 0) {
                throw "$path`:$globalLabel contains duplicate local label $localLabel."
            }
            $start = $index
        }
    }
    if ($start -lt 0) {
        throw "$path`:$globalLabel is missing local label $localLabel."
    }
    $result = [Collections.Generic.List[object]]::new()
    for ($index = $start + 1; $index -lt $nodes.Count; $index++) {
        if ($nodes[$index].Kind -eq 'Label') { break }
        $result.Add($nodes[$index])
    }
    return @($result)
}

function Get-MenuLocalData(
    [string]$path,
    [string]$globalLabel,
    [string]$localLabel,
    [string]$directive
) {
    return @(Get-MenuLocalBlock $path $globalLabel $localLabel |
        Where-Object {
            $_.Kind -eq 'Data' -and $_.Name -ieq $directive
        })
}

function Read-MenuOamParts(
    [string]$path,
    [string]$globalLabel,
    [string]$localLabel
) {
    $data = @(Get-MenuLocalData $path $globalLabel $localLabel '.db')
    if ($data.Count -eq 0 -or $data[0].Operands.Count -ne 1) {
        throw "$path`:$globalLabel$localLabel has no single-byte OAM count."
    }
    $count = Convert-AssemblyInteger $data[0].Operands[0]
    $parts = [Collections.Generic.List[object]]::new()
    foreach ($node in $data | Select-Object -Skip 1) {
        if ($node.Operands.Count -ne 4) {
            throw "$($node.Path):$($node.Line): malformed OAM part in " +
                "$globalLabel$localLabel."
        }
        $values = @($node.Operands | ForEach-Object {
            Convert-AssemblyInteger $_
        })
        $parts.Add(@{
            Y = $values[0]
            X = $values[1]
            Tile = $values[2]
            Attributes = $values[3]
        })
    }
    if ($parts.Count -ne $count) {
        throw "$path`:$globalLabel$localLabel declares $count OAM parts but " +
            "contains $($parts.Count)."
    }
    return @($parts)
}

function Add-MenuOamRows(
    [Collections.Generic.List[string]]$rows,
    [string]$layout,
    [string]$globalLabel,
    [string]$localLabel
) {
    $parts = @(Read-MenuOamParts `
        $menuBank2Path $globalLabel $localLabel)
    for ($part = 0; $part -lt $parts.Count; $part++) {
        $value = $parts[$part]
        $rows.Add([string]::Join(
            "`t",
            @(
                $layout,
                $part,
                $value.Y.ToString('x2'),
                $value.X.ToString('x2'),
                $value.Tile.ToString('x2'),
                $value.Attributes.ToString('x2'),
                "$globalLabel$localLabel",
                '',
                "code/bank2.s:$globalLabel$localLabel")))
    }
}

$fileOamRows = [Collections.Generic.List[string]]::new()
$fileOamRows.Add(
    '# layout`tpart`ty`tx`ttile`tattributes`tsource-label`talias-of`tsource')
Add-MenuOamRows $fileOamRows 'decorations' `
    'fileSelect_redrawDecorationsAndSetWramBank4' '@sprites'
Add-MenuOamRows $fileOamRows 'acorn-cursor' `
    'fileSelectDrawAcornCursor' '@sprite'
Add-MenuOamRows $fileOamRows 'text-speed-cursor' `
    'fileSelectMode1' '@data'
Add-MenuOamRows $fileOamRows 'name-character-cursor' `
    'drawNameInputCursors' '@cursorOnCharacterSprites'
Add-MenuOamRows $fileOamRows 'name-lower-option-cursor' `
    'drawNameInputCursors' '@lowerOptionCursorSprites'
Add-MenuOamRows $fileOamRows 'name-entry-cursor' `
    'drawNameInputCursors' '@textInputCursorSprite'
Add-MenuOamRows $fileOamRows 'secret-entry-cursor' `
    'drawSecretInputCursors' '@textInputCursorSprite'
Add-MenuOamRows $fileOamRows 'save-quit-acorn' `
    'saveQuitMenu_drawSprites' '@acornSprite'
Add-MenuOamRows $fileOamRows 'hero-file' `
    'fileSelectDrawLink' '@spritesb'
if ($fileOamRows.Count -ne 28) {
    throw "Expected 27 file/save OAM parts, got $($fileOamRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\file_oam.tsv'),
    $fileOamRows)

$secretGlyphRows = [Collections.Generic.List[string]]::new()
$secretGlyphRows.Add("# index`tglyph")
$secretSymbolBlock = [regex]::Match(
    (Read-ImportText (Join-Path $Disassembly 'code\bank0.s')),
    '(?ms)^secretSymbols:.*?^\.ifndef REGION_JP\s*\r?\n(?<table>.*?)^[ \t]*\.db \$00[^\r\n]*\r?\n^\.endif')
if (-not $secretSymbolBlock.Success) { throw 'Missing clean-US secretSymbols table.' }
$secretSymbolSource = $secretSymbolBlock.Groups['table'].Value
$asciiMap = @{}
foreach ($node in Read-AssemblyMacroInvocations (Join-Path $Disassembly 'include/rominfo.s') '' 'MAP') {
    if ($node.Code.Trim() -notmatch '^MAP "(.)" = \$([0-9a-fA-F]{2})$') {
        throw "include/rominfo.s:$($node.Line): unsupported ASCII mapping $($node.Code)."
    }
    $asciiMap[[int][char]$Matches[1]] = [Convert]::ToInt32($Matches[2], 16)
}
if ($asciiMap.Count -ne 1 -or $asciiMap[0x7e] -ne 0x5c) { throw 'include/rominfo.s: clean-US ASCII mapping changed.' }
$secretGlyphIndex = 0
foreach ($line in ($secretSymbolSource -split "`n")) {
    if ($line -match '^\s*\.asc\s+"([^"]+)"') {
        foreach ($character in $Matches[1].ToCharArray()) {
            $glyph = [int]$character
            if ($asciiMap.ContainsKey($glyph)) { $glyph = $asciiMap[$glyph] }
            $secretGlyphRows.Add("$secretGlyphIndex`t$($glyph.ToString('x2'))")
            $secretGlyphIndex++
        }
    } elseif ($line -match '^\s*\.db\s+(\$[0-9a-f]{2}(?:\s+\$[0-9a-f]{2})*)') {
        foreach ($value in [regex]::Matches($Matches[1], '\$([0-9a-f]{2})')) {
            if ($value.Groups[1].Value -eq '00') { continue }
            $secretGlyphRows.Add("$secretGlyphIndex`t$($value.Groups[1].Value)")
            $secretGlyphIndex++
        }
    }
}
if ($secretGlyphIndex -ne 64) { throw "Expected 64 clean-US secret glyphs, got $secretGlyphIndex." }
Write-GeneratedTable((Join-Path $destination 'menu\secret_glyphs.tsv'), $secretGlyphRows)
$secretOffsetRows = @("# index`tx")
$secretOffsetIndex = 0
foreach ($node in Get-MenuLocalData $menuBank2Path 'drawSecretInputCursors' '@lowerOptionsOffsets' '.db') {
    foreach ($operand in $node.Operands) {
        $secretOffsetRows += "$secretOffsetIndex`t$(Convert-AssemblyInteger $operand)"
        $secretOffsetIndex++
    }
}
if ($secretOffsetIndex -ne 4) { throw 'Secret input must have four lower options.' }
Write-GeneratedTable((Join-Path $destination 'menu\secret_lower_offsets.tsv'), $secretOffsetRows)
# The retail frontend is an application-owned state machine, but its actor
# compositions and ordered sequences are ROM data. Export only the records the
# US Ages intro dispatch reaches; runtime never reads these assembly sources.
$frontendBank3Path = Join-Path $Disassembly 'code\bank3Cutscenes.s'
$frontendBank0Path = Join-Path $Disassembly 'code\bank0.s'
$frontendBank1Path = Join-Path $Disassembly 'code\bank1.s'
$frontendBank10Path = Join-Path $Disassembly 'code\ages\cutscenes\bank10.s'
$frontendUncmpGfxHeadersPath = Join-Path $Disassembly `
    'data\ages\uncmpGfxHeaders.s'
$frontendAgesRootPath = Join-Path $Disassembly 'ages.s'
$frontendInteractionAnimationPath =
    Join-Path $Disassembly 'data\ages\interactionAnimations.s'
$frontendInteractionOamPath =
    Join-Path $Disassembly 'data\ages\interactionOamData.s'
$frontendSpecialObjectAnimationPath =
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s'
$frontendSpecialObjectOamPath =
    Join-Path $Disassembly 'data\ages\specialObjectOamData.s'
$frontendBirdPath = Join-Path $Disassembly `
    'object_code\common\interactions\introBird.s'
$frontendCloudPath = Join-Path $Disassembly `
    'object_code\common\interactions\titlescreenClouds.s'
$frontendHorseInteractionPath = Join-Path $Disassembly `
    'object_code\ages\interactions\introSprite.s'
$frontendInteractionDataPath = Join-Path $Disassembly `
    'data\ages\interactionData.s'
$frontendBank3Source = Read-ImportText $frontendBank3Path

function Read-FrontendOamParts([string]$path, [string]$label) {
    $data = @(Read-AssemblyDataDirectives $path $label '.db')
    if ($data.Count -eq 0 -or $data[0].Operands.Count -ne 1) {
        throw "$path`:$label has no single-byte OAM count."
    }
    $count = Convert-AssemblyInteger $data[0].Operands[0]
    $parts = @($data | Select-Object -Skip 1 -First $count)
    if ($parts.Count -ne $count) {
        throw "$path`:$label declares $count OAM parts but contains $($parts.Count)."
    }
    return ($parts | ForEach-Object {
        if ($_.Operands.Count -ne 4) {
            throw "$($_.Path):$($_.Line): malformed frontend OAM part in $label."
        }
        (($_.Operands | ForEach-Object {
            (Convert-AssemblyInteger $_).ToString('x2')
        }) -join ',')
    }) -join ';'
}

$frontendStaticOamRows = [Collections.Generic.List[string]]::new()
$frontendStaticOamRows.Add(
    '# layout`tpart`ty`tx`ttile`tattributes`tsource-label`tsource')
foreach ($layout in @(
    @{ Name = 'closeup-touchup'; Label = 'linkOnHorseCloseupSprites_2' },
    @{ Name = 'castle-touchup'; Label = 'introTempleSprites' },
    @{ Name = 'front-facing-link'; Label = 'linkOnHorseFacingCameraSprite' })) {
    $data = @(Read-AssemblyDataDirectives `
        $frontendAgesRootPath $layout.Label '.db')
    if ($data.Count -eq 0) {
        throw "Could not parse frontend static OAM $($layout.Label)."
    }
    $count = Convert-AssemblyInteger $data[0].Operands[0]
    $parts = @($data | Select-Object -Skip 1 -First $count)
    if ($parts.Count -ne $count) {
        throw "$($layout.Label) declares $count parts, parsed $($parts.Count)."
    }
    for ($part = 0; $part -lt $parts.Count; $part++) {
        $values = @($parts[$part].Operands | ForEach-Object {
            Convert-AssemblyInteger $_
        })
        if ($values.Count -ne 4) {
            throw "Malformed static frontend OAM part in $($layout.Label)."
        }
        $frontendStaticOamRows.Add(
            "$($layout.Name)`t$part`t$($values[0].ToString('x2'))`t" +
            "$($values[1].ToString('x2'))`t$($values[2].ToString('x2'))`t" +
            "$($values[3].ToString('x2'))`t$($layout.Label)`t" +
            "ages.s:$($layout.Label)")
    }
}
if ($frontendStaticOamRows.Count -ne 46) {
    throw "Expected 45 static frontend OAM parts, got $($frontendStaticOamRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'intro\static_oam.tsv'),
    $frontendStaticOamRows)

$frontendAnimationDefinitions = Read-AssemblyAnimationDefinitions `
    $frontendInteractionAnimationPath `
    'interactionAnimation[0-9a-f]+(?:Loop)?' $true
$frontendAnimationRows = [Collections.Generic.List[string]]::new()
$frontendAnimationRows.Add(
    '# kind`tindex`tduration`tloop-start`tparameter`tbase-palette`tsource-tile-offset`tsource-inverted`toam-parts`tsource')

function Add-FrontendInteractionAnimation(
    [string]$kind,
    [string]$animationTable,
    [int]$animationIndex,
    [string]$oamTable,
    [int]$basePalette,
    [int]$sourceTileOffset,
    [bool]$sourceInverted
) {
    $animationLabels = @(Read-AssemblyDataDirectives `
        $frontendInteractionAnimationPath $animationTable '.dw' |
        ForEach-Object { $_.Operands[0] })
    $oamLabels = @(Read-AssemblyDataDirectives `
        $frontendInteractionAnimationPath $oamTable '.dw' |
        ForEach-Object { $_.Operands[0] })
    if ($animationIndex -ge $animationLabels.Count) {
        throw "$animationTable has no animation index $animationIndex for $kind."
    }
    $animationLabel = $animationLabels[$animationIndex]
    if (-not $frontendAnimationDefinitions.ContainsKey($animationLabel)) {
        throw "Could not parse frontend animation $animationLabel for $kind."
    }
    $definition = $frontendAnimationDefinitions[$animationLabel]
    $frames = @($definition.Frames)
    for ($frame = 0; $frame -lt $frames.Count; $frame++) {
        $oamIndex = $frames[$frame].PointerOffset / 2
        if ($oamIndex -ge $oamLabels.Count) {
            throw "$kind frame $frame selects missing $oamTable index $oamIndex."
        }
        $parts = Read-FrontendOamParts `
            $frontendInteractionOamPath $oamLabels[$oamIndex]
        $frontendAnimationRows.Add(
            "$kind`t$frame`t$($frames[$frame].Duration)`t" +
            "$($definition.LoopStart)`t" +
            "$($frames[$frame].Parameter.ToString('x2'))`t$basePalette`t" +
            "$($sourceTileOffset.ToString('x2'))`t$([int]$sourceInverted)`t$parts`t" +
            "data/ages/interactionAnimations.s:$animationLabel")
    }
}

$frontendSpecialObjectAnimationSource =
    Read-ImportText $frontendSpecialObjectAnimationPath
$frontendLinkGfxBlock = [regex]::Match(
    $frontendSpecialObjectAnimationSource,
    '(?ms)^specialObject00GfxPointers:(?<body>.*?)(?=^specialObject00AnimationDataPointers:)')
$frontendLinkGfxEntries = if ($frontendLinkGfxBlock.Success) {
    @([regex]::Matches(
        $frontendLinkGfxBlock.Groups['body'].Value,
        'm_SpecialObjectGfxPointer \$(?<oam>[0-9a-f]{2}) spr_link \$(?<offset>[0-9a-f]{4}) \$(?<size>[0-9a-f]{2})'))
} else { @() }
$frontendLinkAnimationPointers = @(Read-AssemblyDataDirectives `
    $frontendSpecialObjectAnimationPath `
    'specialObject08AnimationDataPointers' '.dw' |
    ForEach-Object { $_.Operands[0] })
$frontendLinkOamPointers = @(Read-AssemblyDataDirectives `
    $frontendSpecialObjectAnimationPath `
    'specialObject09OamDataPointers' '.dw' |
    ForEach-Object { $_.Operands[0] })
if ($frontendLinkGfxEntries.Count -lt 0x100 -or
    $frontendLinkAnimationPointers.Count -ne 23 -or
    $frontendLinkOamPointers.Count -ne 48) {
    throw 'Could not resolve the shared Link/cutscene graphics, animation, and OAM tables.'
}

function Read-FrontendSpecialObjectAnimation([string]$label) {
    $definition = [regex]::Match(
        $frontendSpecialObjectAnimationSource,
        "(?ms)^$([regex]::Escape($label)):\s*(?<body>.*?)(?=^animationData[0-9a-f]+:|^specialObject00OamDataPointers:)")
    if (-not $definition.Success) {
        throw "Could not parse frontend special-object animation $label."
    }
    $body = $definition.Groups['body'].Value
    $matches = @([regex]::Matches(
        $body,
        '(?m)^\s*\.db \$(?<duration>[0-9a-f]{2}) \$(?<graphics>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'))
    if ($matches.Count -eq 0) {
        throw "Frontend special-object animation $label has no frames."
    }
    $loopStart = 0
    $loop = [regex]::Match(
        $body,
        '(?m)^\s*m_AnimationLoop\s+(?<target>[A-Za-z0-9_]+)\s*$')
    if ($loop.Success -and $loop.Groups['target'].Value -ne $label) {
        $target = [regex]::Match(
            $body,
            "(?m)^$([regex]::Escape($loop.Groups['target'].Value)):\s*$")
        if (-not $target.Success) {
            throw "$label loops to missing local label $($loop.Groups['target'].Value)."
        }
        $loopStart = @($matches | Where-Object { $_.Index -lt $target.Index }).Count
    } elseif (-not $loop.Success -and
        $matches[-1].Groups['parameter'].Value -eq 'ff') {
        # Terminal special-object frames retain their last loaded graphics.
        $loopStart = $matches.Count - 1
    }
    return @{
        Frames = @($matches | ForEach-Object {
            @{
                Duration = [Convert]::ToInt32($_.Groups['duration'].Value, 16)
                Graphics = [Convert]::ToInt32($_.Groups['graphics'].Value, 16)
                Parameter = [Convert]::ToInt32($_.Groups['parameter'].Value, 16)
            }
        })
        LoopStart = $loopStart
    }
}

function Add-FrontendLinkAnimation([string]$kind, [int]$animationIndex) {
    if ($animationIndex -ge $frontendLinkAnimationPointers.Count) {
        throw "SPECIALOBJECT_LINK_CUTSCENE has no animation $animationIndex for $kind."
    }
    $animationLabel = $frontendLinkAnimationPointers[$animationIndex]
    $definition = Read-FrontendSpecialObjectAnimation $animationLabel
    $frames = @($definition.Frames)
    for ($frame = 0; $frame -lt $frames.Count; $frame++) {
        $graphicsIndex = $frames[$frame].Graphics
        if ($graphicsIndex -ge $frontendLinkGfxEntries.Count) {
            throw "$kind frame $frame selects missing Link graphics index $graphicsIndex."
        }
        $graphics = $frontendLinkGfxEntries[$graphicsIndex]
        $oamIndex = [Convert]::ToInt32(
            $graphics.Groups['oam'].Value, 16)
        $byteOffset = [Convert]::ToInt32(
            $graphics.Groups['offset'].Value, 16)
        if (($byteOffset -band 0x0f) -ne 0 -or
            $oamIndex -ge $frontendLinkOamPointers.Count) {
            throw "$kind frame $frame has an invalid source offset or OAM index."
        }
        $parts = Read-FrontendOamParts `
            $frontendSpecialObjectOamPath $frontendLinkOamPointers[$oamIndex]
        $frontendAnimationRows.Add(
            "$kind`t$frame`t$($frames[$frame].Duration)`t" +
            "$($definition.LoopStart)`t" +
            "$($frames[$frame].Parameter.ToString('x2'))`t0`t" +
            "$(($byteOffset / 16).ToString('x3'))`t1`t$parts`t" +
            "data/ages/specialObjectAnimationData.s:$animationLabel")
    }
}

foreach ($subid in 0..6) {
    Add-FrontendInteractionAnimation `
        "horse-$subid" 'interaction75Animations' $subid `
        'interaction75OamDataPointers' 0 0x00 $true
}
Add-FrontendInteractionAnimation `
    'triforce' 'interaction4aAnimations' 0 'interaction73OamDataPointers' 6 0x00 $true
Add-FrontendInteractionAnimation `
    'triforce-glow' 'interaction4aAnimations' 5 'interaction73OamDataPointers' 4 0x06 $true
Add-FrontendInteractionAnimation `
    'tree-branches' 'interaction4aAnimations' 6 'interaction73OamDataPointers' 4 0x00 $false
foreach ($subid in 0..3) {
    Add-FrontendInteractionAnimation `
        "cloud-$subid" 'interactiond2Animations' $subid `
        'interactiond2OamDataPointers' 2 0x00 $false
}
foreach ($animation in 0..1) {
    Add-FrontendInteractionAnimation `
        "bird-$animation" 'interactiond3Animations' $animation `
        'interactiond3OamDataPointers' 3 0x1a $false
}
Add-FrontendLinkAnimation 'temple-link-walk' 0
Add-FrontendLinkAnimation 'temple-link-rise' 4
Add-FrontendLinkAnimation 'temple-link-fall' 5
# INTERAC_SPARKLE $06: header $3a at spr_link+$1c00, tile $14,
# palette 4, animation 2. Keep the subid dispatch checked at import time.
$templeSparkleRows = @(Read-AssemblyMacroInvocations `
    $frontendInteractionDataPath 'interaction84SubidData' 'm_InteractionSubidData')
if ($templeSparkleRows.Count -ne 16 -or
    ($templeSparkleRows[6].Operands -join ',') -ine '$3a,$14,$42') {
    throw 'Temple INTERAC_SPARKLE $06 no longer resolves to $3a/$14/$42.'
}
Add-FrontendInteractionAnimation `
    'temple-orb' 'interactiondeAnimations' 2 'interaction84OamDataPointers' 4 0x1d4 $true
if ($frontendAnimationRows.Count -lt 50) {
    throw "Expected at least 49 frontend animation frames, got $($frontendAnimationRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'intro\animations.tsv'),
    $frontendAnimationRows)

$frontendSequenceRows = [Collections.Generic.List[string]]::new()
$frontendSequenceRows.Add(
    '# sequence`tindex`tvalue-a`tvalue-b`tsource')
$templeAnimationSetup = [regex]::Match(
    $frontendBank3Source,
    '(?ms)^introCinematic_inTemple_state0:.*?\.ifdef ROM_AGES\s+ld a,\$(?<group>[0-9a-f]{2})\s+\.else.*?\.endif\s+ld \(wTilesetAnimation\),a\s+call loadAnimationData')
if (-not $templeAnimationSetup.Success) {
    throw 'Could not parse the direct Ages temple background-animation load.'
}
$templeAnimationGroup = [Convert]::ToInt32(
    $templeAnimationSetup.Groups['group'].Value, 16)
$frontendSequenceRows.Add(
    "temple-background-animation`t0`t$templeAnimationGroup`t0`t" +
    'code/bank3Cutscenes.s:introCinematic_inTemple_state0/loadAnimationData')
$templeInput = @(Read-AssemblyMacroInvocations `
    $frontendBank10Path 'templeIntro_simulatedInput' 'dwb')
if ($templeInput.Count -ne 18) {
    throw "Expected 18 temple simulated-input records, got $($templeInput.Count)."
}
for ($index = 0; $index -lt $templeInput.Count; $index++) {
    $duration = Convert-AssemblyInteger $templeInput[$index].Operands[0]
    $pressed = if ($templeInput[$index].Operands[1] -ieq 'BTN_UP') { 1 } else { 0 }
    if ($pressed -eq 0 -and $templeInput[$index].Operands[1] -ne '$00') {
        throw "Unsupported temple simulated input '$($templeInput[$index].Operands[1])'."
    }
    $frontendSequenceRows.Add(
        "temple-input`t$index`t$duration`t$pressed`t" +
        'code/ages/cutscenes/bank10.s:templeIntro_simulatedInput')
}

$triforceTimingData = @(Read-AssemblyDataDirectives `
    $frontendAgesRootPath 'data_5951' '.db' |
    ForEach-Object { $_.Operands } |
    ForEach-Object { Convert-AssemblyInteger $_ })
if ($triforceTimingData.Count -ne 12) {
    throw "Expected 12 Triforce timing bytes, got $($triforceTimingData.Count)."
}
for ($index = 0; $index -lt $triforceTimingData.Count; $index++) {
    $frontendSequenceRows.Add(
        "triforce-timing`t$index`t$($triforceTimingData[$index])`t0`tages.s:data_5951")
}
$templeLinkSource = Join-Path $Disassembly 'object_code\ages\specialObjects\linkInCutscene.s'
foreach ($oscillation in 0..1) {
    $deltas = @(Read-AssemblyDataDirectives `
        $templeLinkSource "linkCutscene_zOscillation$oscillation" '.db' |
        ForEach-Object { $_.Operands } |
        ForEach-Object { Convert-AssemblyInteger $_ })
    if ($deltas.Count -ne 8) { throw "Expected eight Link Z deltas for table $oscillation." }
    for ($index = 0; $index -lt $deltas.Count; $index++) {
        $frontendSequenceRows.Add(
            "temple-link-z-$oscillation`t$index`t$($deltas[$index])`t0`t" +
            "object_code/ages/specialObjects/linkInCutscene.s:linkCutscene_zOscillation$oscillation")
    }
}
# introCinematic_inTemple_state0 spawns subids 2, 1, 0 at X $30, $50,
# $70 respectively. The interaction dispatch then selects data_5951 indices
# 10/11 for the outer pieces and 0/1 for the center piece.
$triforcePositions = @(@(0x19, 0x70), @(0x19, 0x50), @(0x19, 0x30))
$triforceDelayIndices = @(10, 0, 10)
$triforceMoveIndices = @(11, 1, 11)
$triforceAngles = @(0x18, 0x00, 0x08)
for ($subid = 0; $subid -lt 3; $subid++) {
    $frontendSequenceRows.Add(
        "triforce-position`t$subid`t$($triforcePositions[$subid][0])`t" +
        "$($triforcePositions[$subid][1])`t" +
        'code/bank3Cutscenes.s:introCinematic_inTemple_state0@nextTriforce')
    $delay = $triforceTimingData[$triforceDelayIndices[$subid]]
    $move = $triforceTimingData[$triforceMoveIndices[$subid]]
    $packedMotion = $triforceAngles[$subid] -bor ($move -shl 8)
    $frontendSequenceRows.Add(
        "triforce-motion`t$subid`t$delay`t$packedMotion`t" +
        'object_code/common/interactions/introSprites1.s:introSpriteTriforceSubid/' +
        'bank3f.data_5951')
}

$waveSineData = @(Get-MenuLocalData `
    $frontendBank1Path 'initWaveScrollValues_body' '@sineWave' '.db' |
    ForEach-Object { $_.Operands } |
    ForEach-Object { Convert-AssemblyInteger $_ })
if ($waveSineData.Count -ne 32) {
    throw "Expected 32 source wave coefficients, got $($waveSineData.Count)."
}
for ($index = 0; $index -lt $waveSineData.Count; $index++) {
    $frontendSequenceRows.Add(
        "temple-wave-sine`t$index`t$($waveSineData[$index])`t0`t" +
        'code/bank1.s:initWaveScrollValues_body@sineWave')
}

$titleSizes = @(Read-AssemblyDataDirectives `
    $frontendBank3Path 'introCinematic_preTitlescreen_titleSizeData' '.db')
$titleSizeValues = @($titleSizes | ForEach-Object { $_.Operands } |
    ForEach-Object { Convert-AssemblyInteger $_ })
if ($titleSizeValues.Count -ne 8) {
    throw "Expected eight pre-title reveal sizes, got $($titleSizeValues.Count)."
}
for ($index = 0; $index -lt $titleSizeValues.Count; $index++) {
    $frontendSequenceRows.Add(
        "title-size`t$index`t$($titleSizeValues[$index])`t0`t" +
        'code/bank3Cutscenes.s:introCinematic_preTitlescreen_titleSizeData')
}

$birdRecords = @(Get-MenuLocalData `
    $frontendBirdPath 'interactionCoded3' `
    '@birdPositionsAndAppearanceDelays' '.db')
if ($birdRecords.Count -ne 8 -or
    $birdRecords.Where({ $_.Operands.Count -ne 3 }).Count -ne 0) {
    throw 'Expected eight three-byte intro-bird position/delay records.'
}
for ($index = 0; $index -lt $birdRecords.Count; $index++) {
    $record = @($birdRecords[$index].Operands | ForEach-Object {
        Convert-AssemblyInteger $_
    })
    $frontendSequenceRows.Add(
        "bird-position`t$index`t$($record[0])`t" +
        "$($record[1] -bor ($record[2] -shl 8))`t" +
        'object_code/common/interactions/introBird.s:@birdPositionsAndAppearanceDelays')
}
$cloudRecords = @(Get-MenuLocalData `
    $frontendCloudPath 'interactionCoded2' '@positions' '.db')
if ($cloudRecords.Count -ne 4 -or
    $cloudRecords.Where({ $_.Operands.Count -ne 2 }).Count -ne 0) {
    throw 'Expected four two-byte title-cloud position records.'
}
for ($index = 0; $index -lt $cloudRecords.Count; $index++) {
    $record = @($cloudRecords[$index].Operands | ForEach-Object {
        Convert-AssemblyInteger $_
    })
    $frontendSequenceRows.Add(
        "cloud-position`t$index`t$($record[0])`t$($record[1])`t" +
        'object_code/common/interactions/titlescreenClouds.s:@positions')
}

$frontendHorseInteractionSource = Read-ImportText `
    $frontendHorseInteractionPath
$frontendGfxRegisterRows = @(Read-AssemblyDataDirectives `
    $frontendBank0Path 'gfxRegisterStates' '.db')
# Native dialogue screens retain the hardware SCY byte independently of
# wScreenOffsetY. In particular, gfx state $09 has no 16-pixel HUD offset.
$dialogueScreenRows = [Collections.Generic.List[string]]::new()
$dialogueScreenRows.Add('# state`tscroll-y`tsource')
foreach ($state in @(0x02, 0x04, 0x09, 0x13)) {
    $registerRow = $frontendGfxRegisterRows[$state * 2]
    if ($registerRow.Operands.Count -ne 6) {
        throw "gfxRegisterStates state $state must contain six bytes per phase."
    }
    $scrollY = Convert-AssemblyInteger $registerRow.Operands[1]
    $dialogueScreenRows.Add(
        "$($state.ToString('x2'))`t$($scrollY.ToString('x2'))`tcode/bank0.s:gfxRegisterStates+$($state.ToString('x2'))")
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\dialogue_screen_registers.tsv'),
    $dialogueScreenRows)
$faceRegisterRows = @($frontendGfxRegisterRows |
    Select-Object -Skip (0x19 * 2) -First 2)
$faceRegisters = @($faceRegisterRows | ForEach-Object {
    if ($_.Operands.Count -ne 6) {
        throw "$($_.Path):$($_.Line): gfx register state `$19 must contain six bytes per phase."
    }
    ,@($_.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
})
$faceStateSetup = [regex]::Match(
    $frontendBank3Source,
    '(?ms)^introCinematic_ridingHorse_state4:.*?ld a,UNCMP_GFXH_AGES_36.*?ld a,\$19\s+call loadGfxRegisterStateIndex.*?ld a,\$(?<split>[0-9a-f]{2})\s+ld \(wGfxRegs1\.LYC\),a\s+ld \(wGfxRegs2\.WINY\),a\s+jp intro_incState')
$faceBarMotion = [regex]::Match(
    $frontendBank3Source,
    '(?ms)^introCinematic_moveBlackBarsOut:\s+ld hl,wGfxRegs1\.LYC\s+dec \(hl\)\s+dec \(hl\).*?cp \$(?<topLimit>[0-9a-f]{2}).*?ld hl,wGfxRegs2\.WINY\s+inc \(hl\)\s+inc \(hl\).*?cp \$(?<bottomBase>[0-9a-f]{2})-\$(?<bottomInset>[0-9a-f]{2})')
$facePanMotion = [regex]::Match(
    $frontendBank3Source,
    '(?ms)^introCinematic_ridingHorse_state5:.*?ld hl,wGfxRegs2\.SCX\s+ld a,\(hl\)\s+add \$(?<step>[0-9a-f]{2})\s+ld \(hl\),a\s+cp \$(?<target>[0-9a-f]{2})')
$faceClear = [regex]::Match(
    (Read-ImportText $frontendUncmpGfxHeadersPath),
    '(?ms)^uncmpGfxHeader36:\s+m_GfxHeaderRam w4TileMap,\s*\$(?<tileDestination>[0-9a-f]{4}),\s*\$(?<blocks>[0-9a-f]{2})\s+m_GfxHeaderRam w4AttributeMap,\s*\$(?<flagDestination>[0-9a-f]{4}),\s*\$\k<blocks>')
if ($faceRegisters.Count -ne 2 -or
    -not $faceStateSetup.Success -or
    -not $faceBarMotion.Success -or
    -not $facePanMotion.Success -or
    -not $faceClear.Success) {
    throw 'Could not parse the Ages face-pan register, bar, scroll, and cleared-window lifecycle.'
}
$faceTopRegisters = $faceRegisters[0]
$faceBottomRegisters = $faceRegisters[1]
$faceTopMap = ($faceTopRegisters[0] -shr 3) -band 1
$faceBottomMap = ($faceBottomRegisters[0] -shr 3) -band 1
$faceWindowMap = ($faceBottomRegisters[0] -shr 6) -band 1
$faceClearDestination = [Convert]::ToInt32(
    $faceClear.Groups['tileDestination'].Value, 16)
$faceFlagDestination = [Convert]::ToInt32(
    $faceClear.Groups['flagDestination'].Value, 16)
$faceClearBytes = [Convert]::ToInt32(
    $faceClear.Groups['blocks'].Value, 16) * 0x10
if ($faceTopMap -ne 1 -or $faceBottomMap -ne 0 -or
    $faceWindowMap -ne 1 -or
    ($faceTopRegisters[0] -band 0x20) -eq 0 -or
    ($faceBottomRegisters[0] -band 0x20) -eq 0 -or
    $faceClearDestination -ne 0x9c00 -or
    $faceFlagDestination -ne 0x9c01 -or
    $faceClearBytes -ne 0x120) {
    throw 'The face pan no longer uses a cleared `$9c00 window around the `$9800 face layer.'
}
$faceInitialSplit = [Convert]::ToInt32(
    $faceStateSetup.Groups['split'].Value, 16)
$faceTopLimit = [Convert]::ToInt32(
    $faceBarMotion.Groups['topLimit'].Value, 16)
$faceBottomLimit =
    [Convert]::ToInt32($faceBarMotion.Groups['bottomBase'].Value, 16) -
    [Convert]::ToInt32($faceBarMotion.Groups['bottomInset'].Value, 16)
$faceBarStep = 2
$facePanStep = [Convert]::ToInt32(
    $facePanMotion.Groups['step'].Value, 16)
$facePanTarget = [Convert]::ToInt32(
    $facePanMotion.Groups['target'].Value, 16)
$frontendSequenceRows.Add(
    "horse-face-registers`t0`t$($faceTopRegisters[1])`t$faceTopMap`t" +
    'code/bank0.s:gfxRegisterStates `$19 phase 1')
$frontendSequenceRows.Add(
    "horse-face-registers`t1`t$($faceBottomRegisters[1])`t$faceBottomMap`t" +
    'code/bank0.s:gfxRegisterStates `$19 phase 2')
$frontendSequenceRows.Add(
    "horse-face-bars`t0`t$faceInitialSplit`t$faceInitialSplit`t" +
    'code/bank3Cutscenes.s:introCinematic_ridingHorse_state4')
$frontendSequenceRows.Add(
    "horse-face-bars`t1`t$faceTopLimit`t$faceBottomLimit`t" +
    'code/bank3Cutscenes.s:introCinematic_moveBlackBarsOut')
$frontendSequenceRows.Add(
    "horse-face-motion`t0`t$faceBarStep`t$facePanStep`t" +
    'code/bank3Cutscenes.s:introCinematic_moveBlackBarsOut/state5')
$frontendSequenceRows.Add(
    "horse-face-motion`t1`t$facePanTarget`t$faceClearBytes`t" +
    'code/bank3Cutscenes.s:introCinematic_ridingHorse_state5/uncmpGfxHeader36')
$castleHorseInit = [regex]::Match(
    $frontendHorseInteractionSource,
    '(?ms)^@subid1Init:.*?Interaction\.counter1\s+ld \(hl\),\$(?<cycles>[0-9a-f]{2}).*?^@initSpeedToScrollLeft:.*?Interaction\.angle\s+ld \(hl\),ANGLE_LEFT\s+ld l,Interaction\.speed\s+ld \(hl\),SPEED_20\s+ld bc,\$(?<position>[0-9a-f]{4})')
$castleHorseStop = [regex]::Match(
    $frontendHorseInteractionSource,
    '(?ms)^@runSubid1:.*?Interaction\.animParameter.*?ld \(hl\),\$00.*?Interaction\.counter1\s+dec \(hl\).*?Interaction\.substate\s+inc \(hl\)\s+ld a,\$(?<animation>[0-9a-f]{2})\s+call interactionSetAnimation')
$faceSparkleInit = [regex]::Match(
    $frontendHorseInteractionSource,
    '(?ms)^@subid4Init:\s+ld bc,\$(?<position>[0-9a-f]{4})\s+jp interactionSetPosition')
$frontendInteractionDataSource = Read-ImportText $frontendInteractionDataPath
$interaction75Data = [regex]::Match(
    $frontendInteractionDataSource,
    '(?ms)^interaction75SubidData:(?<body>.*?m_InteractionSubidDataEnd)')
$interaction75Entries = if ($interaction75Data.Success) {
    @([regex]::Matches(
        $interaction75Data.Groups['body'].Value,
        'm_InteractionSubidData\s+\$[0-9a-f]{2}\s+\$[0-9a-f]{2}\s+\$(?<animation>[0-9a-f]{2})'))
} else { @() }
if (-not $castleHorseInit.Success -or
    -not $castleHorseStop.Success -or
    -not $faceSparkleInit.Success -or
    $interaction75Entries.Count -ne 7) {
    throw 'Could not parse the Ages face-pan/castle intro-sprite lifecycle.'
}
$faceSparklePosition = [Convert]::ToInt32(
    $faceSparkleInit.Groups['position'].Value, 16)
$faceSparkleAnimation = [Convert]::ToInt32(
    $interaction75Entries[4].Groups['animation'].Value, 16) -band 0x0f
$castleHorseCycles = [Convert]::ToInt32(
    $castleHorseInit.Groups['cycles'].Value, 16)
$castleActorPosition = [Convert]::ToInt32(
    $castleHorseInit.Groups['position'].Value, 16)
$castleHorseAnimation = [Convert]::ToInt32(
    $interaction75Entries[1].Groups['animation'].Value, 16) -band 0x0f
$castleStaticAnimation = [Convert]::ToInt32(
    $interaction75Entries[2].Groups['animation'].Value, 16) -band 0x0f
$castleResetAnimation = [Convert]::ToInt32(
    $castleHorseStop.Groups['animation'].Value, 16)
if ($castleHorseCycles -le 0 -or
    $castleHorseAnimation -ne $castleResetAnimation) {
    throw 'The castle horse no longer resets its initial animation after a positive cycle count.'
}
$frontendSequenceRows.Add(
    "horse-face-sparkle`t0`t$(($faceSparklePosition -shr 8) -band 0xff)`t" +
    "$(($faceSparklePosition) -band 0xff)`t" +
    'object_code/ages/interactions/introSprite.s:@subid4Init')
$frontendSequenceRows.Add(
    "horse-face-sparkle`t1`t$faceSparkleAnimation`t0`t" +
    'data/ages/interactionData.s:interaction75SubidData subid `$04')
$frontendSequenceRows.Add(
    "castle-actor-position`t0`t$(($castleActorPosition -shr 8) -band 0xff)`t" +
    "$(($castleActorPosition) -band 0xff)`t" +
    'object_code/ages/interactions/introSprite.s:@initSpeedToScrollLeft')
$frontendSequenceRows.Add(
    "castle-actor-motion`t0`t5`t24`t" +
    'object_code/ages/interactions/introSprite.s:SPEED_20/ANGLE_LEFT')
$frontendSequenceRows.Add(
    "castle-animation`t0`t$castleHorseAnimation`t$castleHorseCycles`t" +
    'object_code/ages/interactions/introSprite.s:@runSubid1')
$frontendSequenceRows.Add(
    "castle-animation`t1`t$castleStaticAnimation`t0`t" +
    'object_code/ages/interactions/introSprite.s:@runSubid2')
Write-GeneratedTable(
    (Join-Path $destination 'intro\sequences.tsv'),
    $frontendSequenceRows)

$frontendTimingRows = @(
    '# key`tvalue`tsource',
    "capcom-hold`t208`tcode/bank3Cutscenes.s:intro_capcomScreen@state0",
    "palette-fade`t32`tcode/bank1.s:paletteFadeHandler01/02",
    "horse-sunset`t350`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state0",
    "horse-fade-divisor`t11`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state0",
    "horse-ground-step`t6`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state2",
    "horse-ground-target`t72`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state2",
    "horse-pause`t126`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state2",
    "horse-front`t288`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state3",
    "horse-face-linger`t24`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state5",
    "horse-closeup-scroll`t112`tcode/bank0.s:gfxRegisterStates+`$78",
    "horse-closeup-linger`t204`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state7",
    "castle-hold`t400`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state8",
    "castle-scroll`t180`tcode/bank3Cutscenes.s:introCinematic_ridingHorse_state8",
    "temple-fade-input-block`t33`tcode/bank0.s:getSimulatedInput/paletteFadeHandler02",
    "triforce-converge`t377`tbank3f.data_5951/retail WRAM trace",
    "triforce-link-rise`t360`tbank3f.data_5951:indices 4,5,7",
    "temple-wave-hold`t120`tcode/bank3Cutscenes.s:introCinematic_inTemple_state4",
    "temple-flash`t15`tcode/bank3Cutscenes.s:screenFlashingData@data0",
    "temple-link-fall`t62`tdata/ages/specialObjectAnimationData.s:linkCutscene0/60 animation + 2 observer updates",
    "temple-wait`t60`tbank3f.data_5951:indices 8-9",
    "tree-scroll-step`t3`tcode/bank3Cutscenes.s:introCinematic_preTitlescreen_state0",
    "tree-scroll-count`t232`tcode/bank3Cutscenes.s:introCinematic_preTitlescreen_updateScrollingTree",
    "title-sound-wait`t16`tcode/bank3Cutscenes.s:introCinematic_preTitlescreen_state1",
    "title-idle`t2400`tcode/bank3Cutscenes.s:intro_titlescreen_state0")
Write-GeneratedTable(
    (Join-Path $destination 'intro\timing.tsv'),
    $frontendTimingRows)
