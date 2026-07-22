# Resolve the first dialogue command reached by a script label, following the
# common scriptjump indirection used by generic NPCs.
$scriptSources = @(
    (Join-Path $Disassembly "scripts\ages\scripts.s"),
    (Join-Path $Disassembly "scripts\ages\scriptHelper.s")
)
$scriptBodies = @{}
foreach ($scriptSourcePath in $scriptSources) {
    $scriptSource = Get-Content -Raw $scriptSourcePath
    foreach ($labelMatch in [regex]::Matches($scriptSource, '(?ms)^(?<label>[A-Za-z0-9_@]+):\r?\n(?<body>.*?)(?=^[A-Za-z0-9_@]+:|\z)')) {
        $scriptBodies[$labelMatch.Groups['label'].Value] = $labelMatch.Groups['body'].Value
    }
}
$scriptTextCache = @{}
function Resolve-ScriptTextId([string]$label, [Collections.Generic.HashSet[string]]$visited) {
    if ($scriptTextCache.ContainsKey($label)) { return $scriptTextCache[$label] }
    if ($visited.Contains($label) -or -not $scriptBodies.ContainsKey($label)) { return -1 }
    [void]$visited.Add($label)
    $body = $scriptBodies[$label]
    $textMatch = [regex]::Match($body, '(?:rungenericnpc|rungenericnpclowindex|showtext|showtextlowindex|settextid)\s+(?:<)?TX_(?<id>[0-9a-f]{4})')
    if ($textMatch.Success) {
        $value = [Convert]::ToInt32($textMatch.Groups['id'].Value, 16)
        $scriptTextCache[$label] = $value
        return $value
    }
    $jumpMatch = [regex]::Match($body, 'scriptjump\s+(?:mainScripts\.)?(?<label>[A-Za-z0-9_@]+)')
    if ($jumpMatch.Success) {
        $value = Resolve-ScriptTextId $jumpMatch.Groups['label'].Value $visited
        $scriptTextCache[$label] = $value
        return $value
    }
    $scriptTextCache[$label] = -1
    return -1
}

# Map interaction subids to the first script entry in each original script
# table. This preserves the important subid-specific dialogue without trying
# to evaluate story-state branches during import.
$npcTextBySubid = @{}
$npcFacingIds = [Collections.Generic.HashSet[int]]::new()
$npcInteractionSourcePaths = @()
$npcInteractionSourcePaths += Get-ChildItem (Join-Path $Disassembly "object_code\ages\interactions") -File -Filter '*.s'
$npcInteractionSourcePaths += Get-ChildItem (Join-Path $Disassembly "object_code\common\interactions") -File -Filter '*.s'
foreach ($interactionSourcePath in $npcInteractionSourcePaths) {
    $interactionSource = Get-Content -Raw $interactionSourcePath.FullName
    # A few large interactions keep only a jpab trampoline in their primary
    # file and put the implementation in interactionCodeXX_body (for example,
    # monkeyMain.s). Treat that body as the same interaction so its exact
    # subid script references can resolve dialogue too.
    $codeMatch = [regex]::Match(
        $interactionSource,
        '(?m)^interactionCode(?<id>[0-9a-f]{2})(?:_body)?:')
    if (-not $codeMatch.Success) { continue }
    $interactionId = [Convert]::ToInt32($codeMatch.Groups['id'].Value, 16)
    if (-not $npcInteractionIds.Contains($interactionId)) { continue }
    if ($interactionSource -match 'npcFaceLinkAndAnimate') { [void]$npcFacingIds.Add($interactionId) }
    $tableName = ''
    $tableIndex = 0
    foreach ($line in ($interactionSource -split '\r?\n')) {
        if ($line -match '^@(?<table>[A-Za-z0-9_]+ScriptTable):') {
            $tableName = $Matches['table']
            $tableIndex = 0
            continue
        }
        if (-not $tableName) { continue }
        if ($line -match '^[^\t ;@].*:') { $tableName = ''; continue }
        if ($line -notmatch '^\s*\.dw\s+mainScripts\.(?<label>[A-Za-z0-9_@]+)') { continue }
        $textId = Resolve-ScriptTextId $Matches['label'] ([Collections.Generic.HashSet[string]]::new())
        if ($textId -ge 0) {
            $subids = @()
            if ($tableName -match '^subid(?<a>[0-9a-f])And(?<b>[0-9a-f])') {
                $subids = @([Convert]::ToInt32($Matches['a'], 16), [Convert]::ToInt32($Matches['b'], 16))
            } elseif ($tableName -match '^subid(?<a>[0-9a-f])(?<b>[0-9a-f])') {
                $subids = @([Convert]::ToInt32("$($Matches['a'])$($Matches['b'])", 16))
            } elseif ($tableName -eq 'scriptTable') {
                $subids = @($tableIndex)
            }
            foreach ($subid in $subids) {
                $key = "$interactionId`:$subid"
                if (-not $npcTextBySubid.ContainsKey($key)) { $npcTextBySubid[$key] = $textId }
            }
        }
        $tableIndex++
    }
    # Some interactions select scripts in assembly rather than through a .dw
    # table. Only accept references whose labels identify their subid; never
    # assign an unrelated "first text" to every instance of the interaction.
    foreach ($scriptReference in [regex]::Matches($interactionSource, 'mainScripts\.(?<label>[A-Za-z0-9_@]+)')) {
        $label = $scriptReference.Groups['label'].Value
        $textId = Resolve-ScriptTextId $label ([Collections.Generic.HashSet[string]]::new())
        if ($textId -lt 0) { continue }
        $subids = @()
        if ($label -match '(?i)Subid(?<a>[0-9a-f])And(?<b>[0-9a-f])') {
            $subids = @([Convert]::ToInt32($Matches['a'], 16), [Convert]::ToInt32($Matches['b'], 16))
        } elseif ($label -match '(?i)Subid(?<subid>[0-9a-f]{1,2})Script') {
            $subids = @([Convert]::ToInt32($Matches['subid'], 16))
        } elseif ($label -match '(?i)Script(?<subid>[0-9a-f]{2})(?:_|$)') {
            $subids = @([Convert]::ToInt32($Matches['subid'], 16))
        }
        foreach ($subid in $subids) {
            $key = "$interactionId`:$subid"
            if (-not $npcTextBySubid.ContainsKey($key)) { $npcTextBySubid[$key] = $textId }
        }
    }
}

# linkedGameNpcScript derives its initial text as TX_4d00 + var3f*5. Resolve
# the two old-lady secret subids from that shared formula instead of leaving
# them with text ID $0000 merely because the script uses showloadedtext.
$linkedNpcScriptHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$oldLadyInteractionSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\oldLady.s')
if ($linkedNpcScriptHelperSource -notmatch '(?ms)^linkedNpc_initHighTextIndex:.*?>TX_4d00.*?^linkedNpc_calcLowTextIndex:.*?add <TX_4d00.*?add a.*?add a.*?add b' -or
    $oldLadyInteractionSource -notmatch '(?ms)^@initSubid4:.*?ld a,\$00.*?^@initSubid5:.*?ld a,\$09.*?ld e,Interaction\.var3f.*?ld \(de\),a.*?mainScripts\.linkedGameNpcScript') {
    throw 'Old-lady linked-secret text selection no longer matches TX_4d00 + var3f*5.'
}
foreach ($linkedSecretNpc in @(
    @(0x04, 0x00),
    @(0x05, 0x09)
)) {
    $subid = [int]$linkedSecretNpc[0]
    $secretIndex = [int]$linkedSecretNpc[1]
    $textId = 0x4d00 + $secretIndex * 5
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve linked-secret old-lady text TX_$($textId.ToString('x4'))."
    }
    $npcTextBySubid["$([int]0x3d):$subid"] = $textId
}

# Parse the interaction graphics table, including pointer-backed subid data.
$interactionDataSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\interactionData.s")
$interactionGraphics = @{}
$interactionPointers = @{}
foreach ($match in [regex]::Matches($interactionDataSource, '(?m)^\s*/\* \$(?<id>[0-9a-f]{2}) \*/ m_InteractionData\s+(?<gfx>\$[0-9a-f]{2}|[A-Za-z0-9_]+)(?:\s+(?<base>\$[0-9a-f]{2})\s+(?<flags>\$[0-9a-f]{2}))?')) {
    $id = [Convert]::ToInt32($match.Groups['id'].Value, 16)
    if ($match.Groups['gfx'].Value.StartsWith('$')) {
        $interactionGraphics["$id`:0"] = @{
            Gfx = [Convert]::ToInt32($match.Groups['gfx'].Value.Substring(1), 16)
            TileBase = [Convert]::ToInt32($match.Groups['base'].Value.Substring(1), 16)
            Flags = [Convert]::ToInt32($match.Groups['flags'].Value.Substring(1), 16)
            Palette = ([Convert]::ToInt32($match.Groups['flags'].Value.Substring(1), 16) -shr 4) -band 7
            DefaultAnimation = [Convert]::ToInt32($match.Groups['flags'].Value.Substring(1), 16) -band 15
        }
    } else { $interactionPointers[$id] = $match.Groups['gfx'].Value }
}
foreach ($block in [regex]::Matches($interactionDataSource, '(?ms)^interaction(?<id>[0-9a-f]{2})SubidData:\r?\n(?<body>.*?)(?=^interaction[0-9a-f]{2}SubidData:|\z)')) {
    $id = [Convert]::ToInt32($block.Groups['id'].Value, 16)
    $subid = 0
    foreach ($entry in [regex]::Matches($block.Groups['body'].Value, 'm_InteractionSubidData\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<base>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})')) {
        $flags = [Convert]::ToInt32($entry.Groups['flags'].Value, 16)
        $interactionGraphics["$id`:$subid"] = @{
            Gfx = [Convert]::ToInt32($entry.Groups['gfx'].Value, 16)
            TileBase = [Convert]::ToInt32($entry.Groups['base'].Value, 16)
            Flags = $flags
            Palette = ($flags -shr 4) -band 7
            DefaultAnimation = $flags -band 15
        }
        $subid++
    }
}
# Repeat the subid pass with alias awareness. The source intentionally stacks
# labels such as interaction6dSubidData / interaction6eSubidData over one
# shared sequence; treating each label as an independent regex block drops the
# first interaction entirely.
$subidAliases = [Collections.Generic.List[int]]::new()
$subidEntries = [Collections.Generic.List[object]]::new()
function Complete-InteractionSubidAliases {
    if ($script:subidEntries.Count -eq 0) { return }
    foreach ($aliasId in $script:subidAliases) {
        for ($index = 0; $index -lt $script:subidEntries.Count; $index++) {
            $entry = $script:subidEntries[$index]
            $script:interactionGraphics["$aliasId`:$index"] = $entry
        }
    }
    $script:subidAliases.Clear()
    $script:subidEntries.Clear()
}
foreach ($line in ($interactionDataSource -split '\r?\n')) {
    if ($line -match '^interaction(?<id>[0-9a-f]{2})SubidData:') {
        if ($subidEntries.Count -gt 0) { Complete-InteractionSubidAliases }
        $subidAliases.Add([Convert]::ToInt32($Matches['id'], 16))
        continue
    }
    if ($subidAliases.Count -eq 0) { continue }
    if ($line -match 'm_InteractionSubidData\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<base>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})') {
        $flags = [Convert]::ToInt32($Matches['flags'], 16)
        $subidEntries.Add(@{
            Gfx = [Convert]::ToInt32($Matches['gfx'], 16)
            TileBase = [Convert]::ToInt32($Matches['base'], 16)
            Flags = $flags
            Palette = ($flags -shr 4) -band 7
            DefaultAnimation = $flags -band 15
        })
        continue
    }
    if ($line -match '^[A-Za-z0-9_@]+:') {
        Complete-InteractionSubidAliases
        $subidAliases.Clear()
    }
}
Complete-InteractionSubidAliases

# INTERAC_TREASURE's subid table also has alias labels embedded in its byte
# range. They do not terminate the table in ROM: the graphic byte may index
# straight through them up to the explicit m_InteractionSubidDataEnd.
$treasureSubidBlock = [regex]::Match(
    $interactionDataSource,
    '(?ms)^interaction60SubidData:\r?\n(?<body>.*?m_InteractionSubidDataEnd)')
if (-not $treasureSubidBlock.Success) {
    throw 'Could not resolve the complete INTERAC_TREASURE subid table.'
}
$treasureGraphicIndex = 0
foreach ($entry in [regex]::Matches(
    $treasureSubidBlock.Groups['body'].Value,
    'm_InteractionSubidData\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<base>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})')) {
    $flags = [Convert]::ToInt32($entry.Groups['flags'].Value, 16)
    $interactionGraphics["96`:$treasureGraphicIndex"] = @{
        Gfx = [Convert]::ToInt32($entry.Groups['gfx'].Value, 16)
        TileBase = [Convert]::ToInt32($entry.Groups['base'].Value, 16)
        Flags = $flags
        Palette = ($flags -shr 4) -band 7
        DefaultAnimation = $flags -band 15
    }
    $treasureGraphicIndex++
}
if ($treasureGraphicIndex -ne 0x83) {
    throw "Expected 131 INTERAC_TREASURE subid graphics, parsed $treasureGraphicIndex."
}
$gfxNames = @{}
foreach ($line in Get-Content (Join-Path $Disassembly "data\ages\objectGfxHeaders.s")) {
    if ($line -match '/\* \$(?<id>[0-9a-f]{2}) \*/ m_ObjectGfxHeader (?<name>[A-Za-z0-9_]+)') {
        $gfxNames[[Convert]::ToInt32($Matches['id'], 16)] = $Matches['name']
    }
}

# Resolve animation indices through the original pointer tables. Animation
# frame byte 1 is a byte offset into the interaction's OAM pointer table (the
# engine adds it directly before reading a word), not a sprite-sheet column.
$interactionAnimationSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\interactionAnimations.s")
function Read-NpcDwTables([string]$source, [string]$tableLabelPattern, [string]$entryPattern) {
    $result = @{}
    $aliases = [Collections.Generic.List[string]]::new()
    $entries = [Collections.Generic.List[string]]::new()
    foreach ($line in ($source -split '\r?\n')) {
        $labelMatch = [regex]::Match($line, "^(?<label>$tableLabelPattern):")
        if ($labelMatch.Success) {
            if ($entries.Count -gt 0) {
                foreach ($alias in $aliases) { $result[$alias] = @($entries) }
                $aliases.Clear()
                $entries.Clear()
            }
            $aliases.Add($labelMatch.Groups['label'].Value)
            continue
        }
        if ($aliases.Count -eq 0) { continue }
        $entryMatch = [regex]::Match($line, "^\s*\.dw\s+(?<entry>$entryPattern)")
        if ($entryMatch.Success) {
            $entries.Add($entryMatch.Groups['entry'].Value)
            continue
        }
        if ($line -match '^[A-Za-z0-9_@]+:') {
            if ($entries.Count -gt 0) {
                foreach ($alias in $aliases) { $result[$alias] = @($entries) }
            }
            $aliases.Clear()
            $entries.Clear()
        }
    }
    if ($entries.Count -gt 0) {
        foreach ($alias in $aliases) { $result[$alias] = @($entries) }
    }
    return $result
}

$npcAnimationTables = Read-NpcDwTables $interactionAnimationSource 'interaction[0-9a-f]{2}Animations' 'interactionAnimation[0-9a-f]+'
$npcOamPointerTables = Read-NpcDwTables $interactionAnimationSource 'interaction[0-9a-f]{2}OamDataPointers' 'interactionOamData[0-9a-f]+'
$npcAnimationFrames = @{}
$npcAnimationLoopStarts = @{}
$npcAnimationLabels = @{}
$npcAnimationLabelMatches = @([regex]::Matches(
    $interactionAnimationSource,
    '(?m)^(?<label>interactionAnimation[0-9a-f]+(?:Loop)?):'))
foreach ($labelMatch in $npcAnimationLabelMatches) {
    $npcAnimationLabels[$labelMatch.Groups['label'].Value] =
        $labelMatch.Index + $labelMatch.Length
}
$npcFramePattern = '\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<frame>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})'
function Read-NpcAnimationFrameRange([int]$start, [int]$length) {
    $frames = [Collections.Generic.List[object]]::new()
    foreach ($frame in [regex]::Matches(
        $interactionAnimationSource.Substring($start, $length),
        $npcFramePattern)) {
        $frames.Add(@{
            Duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
            PointerOffset = [Convert]::ToInt32($frame.Groups['frame'].Value, 16)
            Parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
        })
    }
    return @($frames)
}
for ($labelIndex = 0; $labelIndex -lt $npcAnimationLabelMatches.Count; $labelIndex++) {
    $labelMatch = $npcAnimationLabelMatches[$labelIndex]
    $label = $labelMatch.Groups['label'].Value
    $start = $labelMatch.Index + $labelMatch.Length
    $tail = $interactionAnimationSource.Substring($start)
    $loopMatch = [regex]::Match(
        $tail,
        'm_AnimationLoop\s+(?<target>interactionAnimation[0-9a-f]+(?:Loop)?)')
    $terminalMatch = [regex]::Match(
        $tail,
        '\.db\s+\$[0-9a-f]{2}\s+\$[0-9a-f]{2}\s+\$ff')
    $usesLoop = $loopMatch.Success -and
        (-not $terminalMatch.Success -or $loopMatch.Index -lt $terminalMatch.Index)
    $endOffset = if ($usesLoop) {
        $loopMatch.Index
    } elseif ($terminalMatch.Success) {
        $terminalMatch.Index + $terminalMatch.Length
    } elseif ($labelIndex + 1 -lt $npcAnimationLabelMatches.Count) {
        $npcAnimationLabelMatches[$labelIndex + 1].Index - $start
    } else {
        $tail.Length
    }
    $frames = [Collections.Generic.List[object]]::new()
    $frames.AddRange([object[]](Read-NpcAnimationFrameRange $start $endOffset))
    if ($frames.Count -eq 0) { continue }

    $loopStart = 0
    if ($usesLoop) {
        $target = $loopMatch.Groups['target'].Value
        if (-not $npcAnimationLabels.ContainsKey($target)) {
            throw "$label loops to missing animation label $target."
        }
        $targetStart = [int]$npcAnimationLabels[$target]
        if ($targetStart -ge $start -and $targetStart -le $start + $endOffset) {
            $loopStart = (Read-NpcAnimationFrameRange $start ($targetStart - $start)).Count
        } elseif ($targetStart -lt $start) {
            # A table pointer can enter halfway through a shared animation
            # ($5a849 falls back to $5a846). Keep its initial suffix, then
            # append the full cycle and loop over that appended copy.
            $loopStart = $frames.Count
            $frames.AddRange([object[]](Read-NpcAnimationFrameRange $targetStart (($start + $endOffset) - $targetStart)))
        }
    }
    $npcAnimationFrames[$label] = @($frames)
    $npcAnimationLoopStarts[$label] = $loopStart
}

$npcOamBlocks = @{}
$interactionOamSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\interactionOamData.s")
foreach ($oam in [regex]::Matches($interactionOamSource, '(?ms)^(?<label>interactionOamData[0-9a-f]+):[^\r\n]*\r?\n(?<body>.*?)(?=^interactionOamData[0-9a-f]+:|\z)')) {
    $dataLines = [regex]::Matches($oam.Groups['body'].Value, '(?m)^\s*\.db\s+(?<bytes>[^;\r\n]+)')
    if ($dataLines.Count -eq 0) { continue }
    $countMatch = [regex]::Match($dataLines[0].Groups['bytes'].Value, '\$(?<count>[0-9a-f]{2})')
    if (-not $countMatch.Success) { continue }
    $count = [Convert]::ToInt32($countMatch.Groups['count'].Value, 16)
    $blocks = [Collections.Generic.List[string]]::new()
    for ($index = 1; $index -le $count -and $index -lt $dataLines.Count; $index++) {
        $values = [regex]::Matches($dataLines[$index].Groups['bytes'].Value, '\$(?<value>[0-9a-f]{2})')
        if ($values.Count -lt 4) { continue }
        $blocks.Add(($values | Select-Object -First 4 | ForEach-Object {
            [Convert]::ToInt32($_.Groups['value'].Value, 16)
        }) -join ',')
    }
    $npcOamBlocks[$oam.Groups['label'].Value] = $blocks -join ';'
}

function Resolve-NpcAnimation([int]$interactionId, [int]$animationIndex) {
    $hex = $interactionId.ToString('x2')
    $animationKey = "interaction${hex}Animations"
    $pointerKey = "interaction${hex}OamDataPointers"
    if (-not $npcAnimationTables.ContainsKey($animationKey) -or -not $npcOamPointerTables.ContainsKey($pointerKey)) { return '' }
    $animations = $npcAnimationTables[$animationKey]
    if ($animationIndex -lt 0 -or $animationIndex -ge $animations.Count) { return '' }
    $animationLabel = $animations[$animationIndex]
    if (-not $npcAnimationFrames.ContainsKey($animationLabel)) { return '' }
    $pointers = $npcOamPointerTables[$pointerKey]
    $resolvedFrames = [Collections.Generic.List[string]]::new()
    foreach ($frame in $npcAnimationFrames[$animationLabel]) {
        $pointerIndex = [int]($frame.PointerOffset / 2)
        if ($pointerIndex -lt 0 -or $pointerIndex -ge $pointers.Count) { continue }
        $oamLabel = $pointers[$pointerIndex]
        $oam = if ($npcOamBlocks.ContainsKey($oamLabel)) { $npcOamBlocks[$oamLabel] } else { '' }
        $metadata = if ([int]$frame.Parameter -eq 0) {
            "$($frame.Duration)"
        } else {
            "$($frame.Duration),$($frame.Parameter)"
        }
        $resolvedFrames.Add("$metadata@$oam")
    }
    $encoded = $resolvedFrames -join '|'
    $loopStart = $npcAnimationLoopStarts[$animationLabel]
    if ($loopStart -gt 0) {
        $encoded += "~$loopStart"
    }
    return $encoded
}

# The shared INTERAC_TREASURE OAM pointer base intentionally indexes through
# the following labeled pointer tables for several common animation frames.
# Preserve that contiguous ROM layout instead of truncating at the next label.
$treasureOamPointerBase = [regex]::Match(
    $interactionAnimationSource,
    '(?m)^interaction60OamDataPointers:[^\r\n]*\r?\n')
if (-not $treasureOamPointerBase.Success) {
    throw 'Could not resolve the INTERAC_TREASURE OAM pointer base.'
}
$treasureOamPointers = @(
    [regex]::Matches(
        $interactionAnimationSource.Substring(
            $treasureOamPointerBase.Index + $treasureOamPointerBase.Length),
        '(?m)^\s*\.dw\s+(?<entry>interactionOamData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['entry'].Value })
function Resolve-TreasureAnimation([int]$animationIndex) {
    $animations = $npcAnimationTables['interaction60Animations']
    if ($animationIndex -lt 0 -or $animationIndex -ge $animations.Count) { return '' }
    $animationLabel = $animations[$animationIndex]
    if (-not $npcAnimationFrames.ContainsKey($animationLabel)) { return '' }
    $resolvedFrames = [Collections.Generic.List[string]]::new()
    foreach ($frame in $npcAnimationFrames[$animationLabel]) {
        $pointerIndex = [int]($frame.PointerOffset / 2)
        if ($pointerIndex -lt 0 -or $pointerIndex -ge $treasureOamPointers.Count) {
            continue
        }
        $oamLabel = $treasureOamPointers[$pointerIndex]
        $oam = if ($npcOamBlocks.ContainsKey($oamLabel)) {
            $npcOamBlocks[$oamLabel]
        } else {
            ''
        }
        $metadata = if ([int]$frame.Parameter -eq 0) {
            "$($frame.Duration)"
        } else {
            "$($frame.Duration),$($frame.Parameter)"
        }
        $resolvedFrames.Add("$metadata@$oam")
    }
    $encoded = $resolvedFrames -join '|'
    $loopStart = $npcAnimationLoopStarts[$animationLabel]
    if ($loopStart -gt 0) { $encoded += "~$loopStart" }
    return $encoded
}

# The graphics record supplies the animation used before interaction state 0
# runs. Interactions which immediately call interactionSetAnimation need that
# exact initialized index in the runtime record. Parse these overrides from
# their implementation instead of treating the graphics default as final.
$npcInitialAnimationBySubid = @{}
$monkeyMainSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\monkeyMain.s')
$introMonkeyAnimationMatch = [regex]::Match(
    $monkeyMainSource,
    '(?ms)^@subid2Init:.*?ld e,Interaction\.oamFlags.*?ld a,\$(?<subid2>[0-9a-f]{2})\s+call interactionSetAnimation\s+jr \+\+\s+^@subid3Init:\s+ld a,\$(?<subid3>[0-9a-f]{2})\s+call interactionSetAnimation')
if (-not $introMonkeyAnimationMatch.Success) {
    throw 'Could not resolve the intro monkeys'' state-0 animation indices.'
}
$npcInitialAnimationBySubid['57:2'] =
    [Convert]::ToInt32($introMonkeyAnimationMatch.Groups['subid2'].Value, 16)
$npcInitialAnimationBySubid['57:3'] =
    [Convert]::ToInt32($introMonkeyAnimationMatch.Groups['subid3'].Value, 16)

# Room 1:75 contains the pre-Black Tower ensemble and two var03-selected
# hardhat workers. Pin the initial animation writes performed by the linked
# Impa/Nayru initializers; their script lanes use all four facing animations.
$preBlackTowerImpaSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\impaInCutscene.s')
$preBlackTowerNayruSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')
$preBlackTowerHardhatSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\hardhatWorker.s')
$preBlackTowerScriptsSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$preBlackTowerScriptHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$blackTowerProgressSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\bank0.s')
if ($preBlackTowerImpaSource -notmatch '(?ms)^@init4:.*?checkIsLinkedGame.*?xor a\s+ld \(\$cfc0\),a.*?^@init5:.*?checkIsLinkedGame.*?ld a,\$03\s+call interactionSetAnimation' -or
    $preBlackTowerNayruSource -notmatch '(?ms)^@init09:.*?mainScripts\.nayruScript09.*?^@init0a:.*?checkIsLinkedGame.*?TREASURE_MAKU_SEED.*?GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE.*?ld a,\$01\s+call interactionSetAnimation\s+ld hl,mainScripts\.nayruScript0a' -or
    $preBlackTowerHardhatSource -notmatch '(?ms)^@scriptTable:\s+\.dw mainScripts\.hardhatWorkerSubid00Script\s+\.dw mainScripts\.hardhatWorkerSubid01Script' -or
    $preBlackTowerScriptsSource -notmatch '(?ms)^hardhatWorkerSubid01Script:.*?^@var03_00:.*?hardhatWorker_checkBlackTowerProgressIs00.*?<TX_1007.*?^@var03_01:.*?hardhatWorker_checkBlackTowerProgressIs01.*?<TX_1008' -or
    $preBlackTowerScriptHelperSource -notmatch '(?ms)^hardhatWorker_checkBlackTowerProgressIs00:\s+call getBlackTowerProgress\s+jp writeFlagsTocddb.*?^hardhatWorker_checkBlackTowerProgressIs01:\s+call getBlackTowerProgress\s+cp \$01\s+jp writeFlagsTocddb' -or
    $blackTowerProgressSource -notmatch '(?ms)^getBlackTowerProgress:\s+push bc\s+ld c,\$02\s+ld a,\(wPresentRoomFlags\+\$90\)\s+bit ROOMFLAG_BIT_40,a\s+jr nz,\+\+\s+dec c\s+ld a,\(wPresentRoomFlags\+\$ba\)\s+bit ROOMFLAG_BIT_40,a\s+jr nz,\+\+\s+dec c\s+\+\+\s+ld a,c\s+pop bc\s+ret') {
    throw 'Room 1:75 pre-Black Tower actor initialization changed in the disassembly.'
}
$npcInitialAnimationBySubid['49:5'] = 3
$npcInitialAnimationBySubid['54:10'] = 1

# Vasu's two snakes select their subid as the initial animation. The blue
# snake uses $01 and the red snake uses $06; animation $00 belongs to Vasu.
$vasuSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\vasu.s')
if ($vasuSource -notmatch '(?ms)ld e,Interaction\.subid\s+ld a,\(de\)\s+or a\s+jr z,@@initVasu\s+^@@initSnake:.*?ld a,\(de\)\s+call interactionSetAnimation') {
    throw 'INTERAC_VASU snake initialization changed in the disassembly.'
}
$npcInitialAnimationBySubid['137:1'] = 1
$npcInitialAnimationBySubid['137:6'] = 6

# Room 1:57's female villager overwrites the palette loaded from interaction
# data after interactionInitGraphics. Pin the full initializer and table shape
# so the ordinary NPC row receives the final OAM palette used for drawing.
$room157VillagerSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\femaleVillager.s')
$ringHelpBookSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\ringHelpBook.s')
if ($room157VillagerSource -notmatch '(?ms)^@initSubid05:\s+ld a,\$01\s+ld e,Interaction\.oamFlags\s+ld \(de\),a\s+callab agesInteractionsBank09\.getGameProgress_2\s+ld c,\$05\s+ld a,\$02\s+call checkNpcShouldExistAtGameStage\s+jp nz,interactionDelete.*?ld hl,@subid5ScriptTable.*?jp objectSetVisible82' -or
    $room157VillagerSource -notmatch '(?ms)^@runScriptAndAnimateFacingLink:\s+call interactionRunScript\s+jp npcFaceLinkAndAnimate' -or
    $room157VillagerSource -notmatch '(?ms)^@subid5ScriptTable:\s+\.dw mainScripts\.villagerGalSubid05Script_befored2\s+\.dw mainScripts\.villagerGalSubid05Script_afterd2\s+\.dw mainScripts\.villagerGalSubid05Script_afterd4\s+\.dw mainScripts\.villagerGalSubid05Script_afterNayruSaved\s+\.dw mainScripts\.villagerGalSubid05Script_afterd7\s+\.dw mainScripts\.villagerGalSubid05Script_afterd7\s+\.dw mainScripts\.villagerGalSubid05Script_twinrovaKidnappedZelda\s+\.dw mainScripts\.villagerGalSubid05Script_twinrovaKidnappedZelda' -or
    $ringHelpBookSource -notmatch '(?ms)^@state0:.*?ld e,Interaction\.subid\s+ld a,\(de\).*?or a\s+jr z,\+\+\s+ld e,Interaction\.oamFlags\s+ld a,\(de\)\s+inc a\s+ld \(de\),a\s+ld hl,mainScripts\.ringHelpBookSubid1Script') {
    throw 'Room 1:57 villager or ring-help-book palette initialization changed in the disassembly.'
}
$npcPaletteBySubid = @{
    '59:5' = 1
    # The second ring-help book increments the palette loaded by
    # interactionInitGraphics before selecting its script.
    '229:1' = 2
}

# Room 1:58's late-story Impa and Nayru select their fixed text in assembly
# before entering a generic NPC script. Preserve those selections and their
# directional facing behavior instead of leaving the positioned records at the
# TX_0000/can-face fallback used for unresolved controllers.
$room158HoboSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\miscMan2.s')
$room158ImpaSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\impaNpc.s')
$room158NayruSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')
if ($room158HoboSource -notmatch '(?ms)^@subid4:.*?getGameProgress_2.*?cp \$03\s+jp z,interactionDelete.*?cp \$06.*?ld bc,\$5878.*?pastHoboScriptTable' -or
    $room158ImpaSource -notmatch '(?ms)^impaNpc_subid02:.*?getImpaNpcState.*?cp \$08\s+jp nz,interactionDelete\s+ld a,<TX_012f.*?impaNpc_runScriptAndFaceLink' -or
    $room158ImpaSource -notmatch '(?ms)^impaNpc_setTextIndexAndLoadGenericNpcScript:.*?Interaction\.var38.*?ld a,\$02.*?mainScripts\.genericNpcScript' -or
    $room158NayruSource -notmatch '(?ms)^@init0d:.*?GLOBALFLAG_FLAME_OF_DESPAIR_LIT.*?jp z,interactionDelete.*?GLOBALFLAG_FINISHEDGAME.*?jp nz,interactionDelete.*?<TX_1d17\s+jr @runGenericNpc' -or
    $room158NayruSource -notmatch '(?ms)^nayruAsNpc:\s+call interactionRunScript\s+jp npcFaceLinkAndAnimate' -or
    -not $allTexts.ContainsKey(0x012f) -or
    -not $allTexts.ContainsKey(0x1d17)) {
    throw 'Room 1:58 hobo, Impa, or Nayru initialization changed in the disassembly.'
}
$npcTextBySubid['79:2'] = 0x012f
$npcTextBySubid['54:13'] = 0x1d17
$npcTextByVariant = @{
    '88:1:0' = 0x1007
    '88:1:1' = 0x1008
}
$npcCanFaceBySubid = @{
    '79:2' = $true
    '54:13' = $true
    '49:4' = $true
    '49:5' = $true
    '88:1' = $true
}

function New-NpcDataRow(
    [int]$group,
    [int]$room,
    [int]$id,
    [int]$subid,
    [int]$y,
    [int]$x,
    [int]$var03,
    [int]$textIdOverride = -1,
    [int]$initialAnimationOverride = -1,
    [int]$canFaceOverride = -1
) {
    $graphic = $interactionGraphics["$id`:$subid"]
    if ($null -eq $graphic) { $graphic = $interactionGraphics["$id`:0"] }
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) { return '' }

    $spriteName = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($spriteName)
    $textId = if ($textIdOverride -ge 0) {
        $textIdOverride
    } elseif ($npcTextByVariant.ContainsKey("$id`:$subid`:$var03")) {
        $npcTextByVariant["$id`:$subid`:$var03"]
    } elseif ($npcTextBySubid.ContainsKey("$id`:$subid")) {
        $npcTextBySubid["$id`:$subid"]
    } else {
        0
    }
    $message = if ($allTexts.ContainsKey($textId)) { $allTexts[$textId] } else { '' }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message))
    $initialAnimation = if ($initialAnimationOverride -ge 0) {
        $initialAnimationOverride
    } elseif ($npcInitialAnimationBySubid.ContainsKey("$id`:$subid")) {
        $npcInitialAnimationBySubid["$id`:$subid"]
    } else {
        $graphic.DefaultAnimation
    }
    $palette = if ($npcPaletteBySubid.ContainsKey("$id`:$subid")) {
        [int]$npcPaletteBySubid["$id`:$subid"]
    } else {
        [int]$graphic.Palette
    }
    $canFace = if ($canFaceOverride -ge 0) {
        $canFaceOverride -ne 0
    } elseif ($npcCanFaceBySubid.ContainsKey("$id`:$subid")) {
        [bool]$npcCanFaceBySubid["$id`:$subid"]
    } else {
        $textId -ne 0 -and $npcFacingIds.Contains($id) -and $initialAnimation -ge 2
    }
    $downOam = Resolve-NpcAnimation $id $initialAnimation
    if ($canFace) {
        $upOam = Resolve-NpcAnimation $id ($initialAnimation - 2)
        $rightOam = Resolve-NpcAnimation $id ($initialAnimation - 1)
        $leftOam = Resolve-NpcAnimation $id ($initialAnimation + 1)
    } else {
        $upOam = $downOam
        $rightOam = $downOam
        $leftOam = $downOam
    }
    if (-not $upOam) { $upOam = $downOam }
    if (-not $rightOam) { $rightOam = $downOam }
    if (-not $leftOam) { $leftOam = $downOam }
    return "$group`t$($room.ToString('x2'))`t$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`t$($var03.ToString('x2'))`t$($textId.ToString('x4'))`t$spriteName`t$($graphic.TileBase)`t$palette`t$initialAnimation`t$([int]$canFace)`t$upOam`t$rightOam`t$downOam`t$leftOam`t$encoded"
}

# Room object data is grouped by room label. Positioned interactions are
# emitted directly. Unpositioned interactions which derive a visible actor's
# position from save state are expanded below into mutually exclusive records.
$npcRows = [Collections.Generic.List[string]]::new()
$npcRows.Add("# group`troom`tid`tsubid`ty`tx`tvar03`ttext-id`tsprite`ttile-base`tpalette`tdefault-animation`tcan-face`tup-animation`tright-animation`tdown-animation`tleft-animation`tutf8-base64")
$mainObjectLines = Get-Content (Join-Path $Disassembly "objects\ages\mainData.s")
$mainObjectSource = $mainObjectLines -join "`n"
if ($mainObjectSource -notmatch '(?ms)^group1Map57ObjectData:\s+obj_Interaction \$3b \$05 \$38 \$48\s+obj_End') {
    throw 'Room 1:57 no longer contains female villager $3b:$05 at $38,$48.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map58ObjectData:\s+obj_Interaction \$44 \$04 \$48 \$48\s+obj_Interaction \$4f \$02 \$48 \$48\s+obj_Interaction \$36 \$0d \$48 \$38\s+obj_End') {
    throw 'Room 1:58 no longer contains ordered hobo $44:$04, Impa $4f:$02, and Nayru $36:$0d placements.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map75ObjectData:\s+obj_Interaction \$37 \$0a \$58 \$60\s+obj_Interaction \$31 \$04 \$f8 \$58\s+obj_Interaction \$31 \$05 \$58 \$60\s+obj_Interaction \$36 \$0a \$58 \$40\s+obj_Interaction \$ad \$04 \$48 \$50\s+obj_Interaction \$58 \$01 \$58 \$48 \$00\s+obj_Interaction \$58 \$01 \$58 \$28 \$01\s+obj_End') {
    throw 'Room 1:75 pre-Black Tower ensemble and hardhat worker order changed.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map86ObjectData:\s+obj_Interaction \$58 \$02 \$38 \$48\s+obj_Interaction \$dc \$07 \$28 \$78\s+obj_End') {
    throw 'Room 1:86 no longer contains ordered hardhat $58:$02 and heart-piece spawner $dc:$07 placements.'
}

# PART_BUTTON $09, its trigger-chest consumers $20:$00/$21:$17, the
# trigger-controlled and enemy-controlled shutter variants of
# INTERAC_DOOR_CONTROLLER $1e:$04-$0b, and INTERAC_PUSHBLOCK_TRIGGER $13:$01
# form reusable dungeon mechanisms around wActiveTriggers and wNumEnemies.
# Export every supported direct placement in source order; rooms 4:08, 4:09,
# 4:0b, and 4:0c are the canonical button-chest, button-door, combat-door, and
# trigger-before-door cases.
$pushblockTriggerSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\pushblockTrigger.s')
$buttonSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\parts\button.s')
$doorControllerSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\doorController.s')
$dungeonScriptSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\dungeonScript.s')
$dungeonEventSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\dungeonEvents.s')
$dungeonScriptCommandSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\dungeonScripts.s')
$commonScriptSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\common\commonScripts.s')
$commonScriptHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\common\scriptHelper.s')
$interactableTilesSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\interactableTiles.s')
$interactableTileDataSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\tile_properties\interactableTiles.s')
$standardTileSubstitutionSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\tile_properties\standardTileSubstitutions.s')
$keyDoorGraphicSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\tile_properties\keydoorTiles.s')
$dungeonKeySpriteSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\dungeonKeySprite.s')
$treasureInteractionSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\treasure.s')
$treasureAndDropsSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\treasureAndDrops.s')
$pushblockSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\pushblock.s')
$fallDownHoleSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\fallDownHole.s')
$bank0Source = Get-Content -Raw (Join-Path $Disassembly 'code\bank0.s')
$zolEnemySource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\enemies\zol.s')
$partDataSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\partData.s')
$tileIndexSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
$musicIdSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\music.s')
$objectSpeedSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
if ($pushblockTriggerSource -notmatch '(?ms)^@state0:.*?ld a,TILEINDEX_PUSHABLE_BLOCK.*?ld hl,wNumEnemies\s+inc \(hl\).*?^@state1:.*?^@state2:.*?cp \(hl\)\s+ret z.*?ld a,\$1e.*?^@state3:.*?interactionDecCounter1.*?xor a\s+ld \(wNumEnemies\),a' -or
    $buttonSource -notmatch '(?ms)^partCode09:.*?call z,@state0.*?checkObjectsCollided.*?@linkTouchedButton:.*?ld a,\(w1Link\.zh\).*?rlca\s+jr nc,@delete.*?@checkButtonPushed:.*?TILEINDEX_PRESSED_BUTTON.*?@setTriggerAndPlaySound:.*?wActiveTriggers.*?setFlag.*?SND_SPLASH.*?@state0:.*?and \$07' -or
    $buttonSource -notmatch '(?ms)^@somethingOnButton:.*?bit 7,\(hl\).*?ld \(hl\),\$1c.*?setTileInRoomLayoutBuffer.*?^@updateTileBeforeDeletion:.*?TILEINDEX_PRESSED_BUTTON.*?setTileInRoomLayoutBuffer' -or
    $dungeonScriptSource -notmatch '(?ms)^@dungeon0:.*?^@dungeond:.*?makuPathScript_spawnChestWhenActiveTriggersEq01.*?^@dungeon1:.*?dungeonScript_spawnChestOnTriggerBit0.*?^@dungeon9:.*?^@dungeona:.*?^@dungeonb:.*?dungeonScript_spawnChestOnTriggerBit0' -or
    $dungeonScriptCommandSource -notmatch '(?ms)^dungeonScript_spawnChestOnTriggerBit0:.*?stopifitemflagset.*?checkflagset \$00, wActiveTriggers.*?scriptjump spawnChestAfterPuff.*?^makuPathScript_spawnChestWhenActiveTriggersEq01:.*?checkmemoryeq wActiveTriggers, \$01.*?^spawnChestAfterPuff:.*?playsound SND_SOLVEPUZZLE.*?createpuff.*?wait 15.*?settilehere TILEINDEX_CHEST' -or
    $dungeonEventSource -notmatch '(?ms)^interaction21_subid17:.*?ROOMFLAG_ITEM.*?ld a,\(wActiveTriggers\).*?cp b.*?@triggerActive:.*?TILEINDEX_CHEST.*?createPuffAt.*?SND_SOLVEPUZZLE.*?@triggerInactive:.*?w3RoomLayoutBuffer.*?setTile.*?createPuffAt' -or
    $doorControllerSource -notmatch '(?ms)^@state2Substate0:.*?ld a,SND_DOORCLOSE.*?call setInterleavedTile.*?ld \(hl\),\$06.*?ld \(bc\),a.*?^@state2Substate1:.*?interactionDecCounter1.*?@shutterTiles:\s+\.db \$a0 \$70.*?\.db \$a0 \$77\s+\.db \$a0 \$78.*?\.db \$a0 \$79.*?\.db \$a0 \$7a.*?\.db \$a0 \$7b' -or
    $commonScriptSource -notmatch '(?ms)^doorController_controlledByTriggers_up:.*?setangle \$10.*?^doorController_controlledByTriggers_right:.*?setangle \$12.*?^doorController_controlledByTriggers_down:.*?setangle \$14.*?^doorController_controlledByTriggers_left:.*?setangle \$16.*?^doorController_controlledByTriggers:.*?doorController_decideActionBasedOnTriggers.*?\.dw @open\s+\.dw @close.*?@open:\s+playsound SND_SOLVEPUZZLE\s+setstate \$02.*?@close:\s+setstate \$03' -or
    $commonScriptSource -notmatch '(?ms)^doorController_shutUntilEnemiesDead:.*?jumpifnoenemies @end.*?setstate \$03\s+checknoenemies\s+playsound SND_SOLVEPUZZLE\s+wait 8\s+incstate.*?^doorController_shutUntilEnemiesDead_up:.*?setangle \$10.*?^doorController_shutUntilEnemiesDead_right:.*?setangle \$12.*?^doorController_shutUntilEnemiesDead_down:.*?setangle \$14.*?^doorController_shutUntilEnemiesDead_left:.*?setangle \$16' -or
    $commonScriptHelperSource -notmatch '(?ms)^doorController_decideActionBasedOnTriggers:.*?ld a,\(wActiveTriggers\)\s+and b.*?@triggerInactive:.*?@checkTileIsShutterDoor:.*?@tileIndices:.*?\.db \$78 \$79 \$7a \$7b' -or
    $interactableTilesSource -notmatch '(?ms)TILEINDEX_CHEST_OPENED\s+call setTile.*?SND_OPENCHEST\s+call playSound' -or
    $treasureInteractionSource -notmatch '(?ms)^@m3State1:.*?interactionDecCounter1\s+ret nz.*?call z,@giveTreasure\s+ld a,SND_GETITEM\s+call playSound' -or
    $treasureInteractionSource -notmatch '(?ms)^@giveTreasure:.*?call giveTreasure\s+ld b,a.*?ld a,b\s+call playSound.*?call showText' -or
    $treasureAndDropsSource -notmatch '(?ms)^@giveTreasure:.*?treasureCollectionBehaviourTable.*?bit 7,\(hl\).*?ldi a,\(hl\).*?call playSound' -or
    $pushblockSource -notmatch '(?ms)^@state0:.*?@replaceTileUnderneathBlock\s+call objectSetVisible82\s+ld a,SND_MOVEBLOCK\s+call playSound' -or
    $fallDownHoleSource -notmatch '(?ms)^@fallDownHole:.*?ld a,SND_FALLINHOLE\s+call nc,playSound' -or
    $bank0Source -notmatch '(?ms)^@enemyCreateDeathPuff:.*?PART_ENEMY_DESTROYED.*?ld a,SND_KILLENEMY\s+jp playSound' -or
    $zolEnemySource -notmatch '(?ms)^zol_subid01_stateC:.*?INTERAC_KILLENEMYPUFF.*?ld a,SND_KILLENEMY\s+call playSound' -or
    $partDataSource -notmatch '(?m)^\s*\.db \$00 \$02 \$22 \$00 \$40 \$00 \$00 \$00 ; \$09' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_PUSHABLE_BLOCK\s+\$1d' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_BUTTON\s+\$0c' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_PRESSED_BUTTON\s+\$0d' -or
    $musicIdSource -notmatch '(?m)^\s*SND_SOLVEPUZZLE\s+db\s+; \$4d' -or
    $musicIdSource -notmatch '(?m)^\s*SND_GETITEM\s+db\s+; \$4c' -or
    $musicIdSource -notmatch '(?m)^\s*MUS_GET_ESSENCE\s+db\s+; \$10' -or
    $musicIdSource -notmatch '(?m)^\s*SND_FALLINHOLE\s+db\s+; \$59' -or
    $musicIdSource -notmatch '(?m)^\s*SND_GETSEED\s+db\s+; \$5e' -or
    $musicIdSource -notmatch '(?m)^\s*SND_OPENCHEST\s+db\s+; \$6c' -or
    $musicIdSource -notmatch '(?m)^\s*SND_DOORCLOSE\s+db\s+; \$70' -or
    $musicIdSource -notmatch '(?m)^\s*SND_MOVEBLOCK\s+db\s+; \$71' -or
    $musicIdSource -notmatch '(?m)^\s*SND_KILLENEMY\s+db\s+; \$73' -or
    $musicIdSource -notmatch '(?m)^\s*SND_SPLASH\s+db\s+; \$87' -or
    $musicIdSource -notmatch '(?m)^\s*SND_POOF\s+db\s+; \$98') {
    throw 'Dungeon button/chest/push block and enemy death/hole trigger, timing, tile, or sound contract changed.'
}

if ($interactableTilesSource -notmatch '(?ms)^nextToKeyDoor:.*?call decPushingAgainstTileCounter\s+jr z,\+\s+dec \(hl\)\s+ret nz.*?call checkAndDecKeyCount.*?call createKeySpriteInteraction.*?INTERAC_DOOR_CONTROLLER.*?call setRoomFlagsForUnlockedKeyDoor' -or
    $interactableTilesSource -notmatch '(?ms)^resetPushingAgainstTileCounter:\s+ld a,20\s+ld \(wPushingAgainstTileCounter\),a' -or
    $doorControllerSource -notmatch '(?ms)^@state2Substate0:.*?ld a,SND_DOORCLOSE.*?call setInterleavedTile.*?ld \(hl\),\$06.*?^@state2Substate1:.*?interactionDecCounter1.*?^@shutterTiles:\s+\.db \$a0 \$70.*?\.db \$a0 \$71.*?\.db \$a0 \$72.*?\.db \$a0 \$73' -or
    $bank0Source -notmatch '(?ms)^setRoomFlagsForUnlockedKeyDoor:.*?^_adjacentRoomsData:\s+\.db \$01 \$f8 \$04 \$00.*?\.db \$02 \$01 \$08 \$00.*?\.db \$04 \$08 \$01 \$00.*?\.db \$08 \$ff \$02 \$00' -or
    $keyDoorGraphicSource -notmatch '(?ms)^@dungeons:.*?\.db \$70 \$00.*?\.db \$71 \$00.*?\.db \$72 \$00.*?\.db \$73 \$00' -or
    $dungeonKeySpriteSource -notmatch '(?ms)^@state0:.*?ld \(hl\),\$fc.*?ld \(hl\),\$08.*?ld a,SND_GETSEED.*?^@state1:.*?ld \(hl\),\$14.*?ld \(hl\),\$f8.*?^@state2:' -or
    $objectSpeedSource -notmatch '(?m)^\s*SPEED_60\s+dsb 5 ; 0x0f') {
    throw 'Small-key door push, paired flag, key-sprite, animation, or timing contract changed.'
}

$puzzlePuffGraphic = $interactionGraphics['5:0']
$puzzlePuffAnimation = Resolve-NpcAnimation 0x05 0
if (-not $puzzlePuffGraphic -or
    $puzzlePuffGraphic.TileBase -ne 0x16 -or
    $puzzlePuffGraphic.Palette -ne 3 -or
    [string]::IsNullOrWhiteSpace($puzzlePuffAnimation)) {
    throw 'INTERAC_PUFF $05 no longer resolves to tile base $16, palette 3, animation 0.'
}
$puzzlePuffRows = @(
    "# tile-base`tpalette`tanimation"
    "$($puzzlePuffGraphic.TileBase)`t$($puzzlePuffGraphic.Palette)`t$puzzlePuffAnimation"
)

$fallDownHoleGraphic = $interactionGraphics['15:0']
$fallDownHoleAnimation = Resolve-NpcAnimation 0x0f 0
if (-not $fallDownHoleGraphic -or
    $fallDownHoleGraphic.Gfx -ne 0 -or
    $fallDownHoleGraphic.TileBase -ne 0x16 -or
    $fallDownHoleGraphic.Palette -ne 3 -or
    $fallDownHoleGraphic.DefaultAnimation -ne 0 -or
    [string]::IsNullOrWhiteSpace($fallDownHoleAnimation) -or
    $fallDownHoleSource -notmatch '(?ms)^@interac0f_state1:.*?bit 7,\(hl\).*?add \$05\s+and \$f0\s+add \$08.*?ld \(de\),a\s+call objectApplySpeed.*?jp interactionAnimate') {
    throw 'INTERAC_FALLDOWNHOLE `$0f no longer resolves to common graphics tile base `$16, palette 3, SPEED_60, animation 0.'
}
$fallDownHoleRows = @(
    "# tile-base`tpalette`tspeed-raw`tanimation"
    "$($fallDownHoleGraphic.TileBase)`t$($fallDownHoleGraphic.Palette)`t15`t$fallDownHoleAnimation"
)

$keyDoorOpenTiles = @{}
foreach ($entry in [regex]::Matches(
    $standardTileSubstitutionSource,
    '(?m)^\s*\.db \$(?<open>[0-9a-f]{2}) \$(?<closed>7[0-3])(?:\s|;)')) {
    $closedTile = $entry.Groups['closed'].Value
    if ($keyDoorOpenTiles.ContainsKey($closedTile)) {
        throw "Duplicate standard small-key door substitution for `$$closedTile."
    }
    $keyDoorOpenTiles[$closedTile] = $entry.Groups['open'].Value
}
$keyDoorFlags = @{}
foreach ($entry in [regex]::Matches(
    $bank0Source,
    '(?m)^\s*\.db \$(?<room>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<opposite>[0-9a-f]{2}) \$00 ; Key door going (?<direction>up|right|down|left)\s*$')) {
    $directionName = $entry.Groups['direction'].Value
    if ($keyDoorFlags.ContainsKey($directionName)) {
        throw "Duplicate _adjacentRoomsData key-door direction $directionName."
    }
    $keyDoorFlags[$directionName] = @(
        $entry.Groups['room'].Value,
        $entry.Groups['opposite'].Value)
}
$keyDoorRows = [Collections.Generic.List[string]]::new()
$keyDoorRows.Add(
    "# closed-tile`tdirection`topen-tile`troom-flag`topposite-room-flag`tpush-counter`tdoor-frame-wait`tdoor-sound`tkey-sound`tno-key-text-id`tno-key-utf8-base64")
$noKeyText = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($allTexts[0x5100]))
foreach ($entry in [regex]::Matches(
    $interactableTileDataSource,
    '(?m)^\s*\.db \$(?<tile>7[0-3]) \$(?<direction>[0-3])2\s*$')) {
    $tile = $entry.Groups['tile'].Value
    $direction = [Convert]::ToInt32($entry.Groups['direction'].Value, 16)
    if (-not $keyDoorOpenTiles.ContainsKey($tile)) {
        throw "Small-key door `$$tile has no standard opened-tile substitution."
    }
    $directionName = @('up', 'right', 'down', 'left')[$direction]
    if (-not $keyDoorFlags.ContainsKey($directionName)) {
        throw "Small-key door `$$tile has no _adjacentRoomsData flags for $directionName."
    }
    $roomFlag, $oppositeFlag = $keyDoorFlags[$directionName]
    $keyDoorRows.Add(
        "$tile`t$directionName`t$($keyDoorOpenTiles[$tile])`t$roomFlag`t$oppositeFlag`t20`t6`t112`t94`t5100`t$noKeyText")
}
if ($keyDoorRows.Count -ne 5 -or
    -not ($keyDoorRows -contains "73`tleft`ta0`t08`t02`t20`t6`t112`t94`t5100`t$noKeyText")) {
    throw "Expected four imported small-key doors `$70-`$73 including left door `$73, parsed $($keyDoorRows.Count - 1)."
}

# applyStandardTileSubstitutions selects one replacement list for each set room
# flag bit and wActiveCollisions value. Preserve the complete Ages table so
# persistent broken overworld tiles and the existing door paths share the same
# load-time mechanism.
$standardCollisionModes = @{
    Overworld = 0
    Indoors = 1
    Dungeons = 2
    Sidescrolling = 3
    Underwater = 4
    Five = 5
}
$standardTileRows = [Collections.Generic.List[string]]::new()
$standardTileRows.Add('# room-flag`tactive-collisions`treplacement`toriginal`tsource')
$activeStandardLabels = [Collections.Generic.List[hashtable]]::new()
foreach ($line in $standardTileSubstitutionSource -split "`r?`n") {
    if ($line -match '^\s*@bit(?<bit>[01237])(?<mode>Overworld|Indoors|Dungeons|Sidescrolling|Underwater|Five):') {
        $activeStandardLabels.Add(@{
            Flag = 1 -shl [Convert]::ToInt32($Matches['bit'], 10)
            Collisions = $standardCollisionModes[$Matches['mode']]
            Label = "bit$($Matches['bit'])$($Matches['mode'])"
        })
        continue
    }
    if ($activeStandardLabels.Count -eq 0 -or
        $line -notmatch '^\s*\.db\s+\$(?<replacement>[0-9a-f]{2})(?:\s+\$(?<original>[0-9a-f]{2}))?') {
        continue
    }
    $replacement = [Convert]::ToInt32($Matches['replacement'], 16)
    if (-not $Matches.ContainsKey('original') -or $Matches['original'] -eq '') {
        if ($replacement -ne 0) {
            throw "Unexpected standard tile-substitution terminator `$$($replacement.ToString('x2'))."
        }
        $activeStandardLabels.Clear()
        continue
    }
    $original = [Convert]::ToInt32($Matches['original'], 16)
    foreach ($active in $activeStandardLabels) {
        $standardTileRows.Add(
            "$($active.Flag.ToString('x2'))`t$($active.Collisions)`t$($replacement.ToString('x2'))`t$($original.ToString('x2'))`tstandardTileSubstitutions@$($active.Label)")
    }
}
if ($standardTileRows.Count -ne 51 -or
    -not ($standardTileRows -contains "80`t0`tdc`tc6`tstandardTileSubstitutions@bit7Overworld") -or
    -not ($standardTileRows -contains "01`t2`ta0`t70`tstandardTileSubstitutions@bit0Dungeons")) {
    throw "Expected 50 standard tile substitutions including bit-7 tree and bit-0 key-door rows, parsed $($standardTileRows.Count - 1)."
}

$conditionalDungeonEnemyRooms = [Collections.Generic.HashSet[string]]::new()
foreach ($block in [regex]::Matches(
    $mainObjectSource,
    '(?ms)^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:(?<body>.*?)(?=^group[0-7]Map[0-9a-f]{2}ObjectData:|\z)')) {
    if ($block.Groups['body'].Value -match '(?m)^\s*obj_(?:BeforeEvent|AfterEvent)\s+') {
        [void]$conditionalDungeonEnemyRooms.Add(
            "$($block.Groups['group'].Value):$($block.Groups['room'].Value)")
    }
}

$triggerChestPredicateByDungeon = @{
    0x00 = 'exact'
    0x01 = 'bit'
    0x09 = 'bit'
    0x0a = 'bit'
    0x0b = 'bit'
    0x0d = 'exact'
}
$mechanicTilesetsByGroup = @{}
function Resolve-DungeonMechanicDungeonIndex([int]$group, [int]$room) {
    if (-not $script:mechanicTilesetsByGroup.ContainsKey($group)) {
        $script:mechanicTilesetsByGroup[$group] = [IO.File]::ReadAllBytes(
            (Join-Path $Disassembly "rooms\ages\group${group}Tilesets.bin"))
    }
    $tileset = $script:mechanicTilesetsByGroup[$group][$room] -band 0x7f
    return [int]$metadata[$tileset * $tilesetRecordSize + 5]
}

$dungeonMechanicRows = [Collections.Generic.List[string]]::new()
$dungeonMechanicRows.Add("# group`troom`torder`tid`tsubid`tposition`tparameter`ttrigger-predicate`tcount-source-complete")
$permanentTriggerChestCount = 0
$retractableTriggerChestCount = 0
$mechanicGroup = -1
$mechanicRoom = -1
$mechanicOrder = 0
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $mechanicGroup = [Convert]::ToInt32($Matches['group'], 10)
        $mechanicRoom = [Convert]::ToInt32($Matches['room'], 16)
        $mechanicOrder = 0
        continue
    }
    if ($mechanicGroup -lt 0 -or $line -notmatch '^\s*obj_') { continue }
    if ($line -match '^\s*obj_End') { continue }
    if ($line -match '^\s*obj_Interaction\s+\$(?<id>[0-9a-f]{2})\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<a>[0-9a-f]{2})\s+\$(?<b>[0-9a-f]{2})') {
        $id = [Convert]::ToInt32($Matches['id'], 16)
        $subid = [Convert]::ToInt32($Matches['subid'], 16)
        $dungeonScriptPredicate = ''
        if ($id -eq 0x20 -and $subid -eq 0x00) {
            $dungeon = Resolve-DungeonMechanicDungeonIndex $mechanicGroup $mechanicRoom
            if ($triggerChestPredicateByDungeon.ContainsKey($dungeon)) {
                $dungeonScriptPredicate = $triggerChestPredicateByDungeon[$dungeon]
            }
        }
        if (($id -eq 0x13 -and $subid -eq 0x01) -or
            ($id -eq 0x1e -and $subid -ge 0x04 -and $subid -le 0x0b) -or
            $dungeonScriptPredicate -ne '' -or
            ($id -eq 0x21 -and $subid -eq 0x17)) {
            $a = [Convert]::ToInt32($Matches['a'], 16)
            $b = [Convert]::ToInt32($Matches['b'], 16)
            $position = if ($id -eq 0x13 -or $id -eq 0x20) {
                ($a -band 0xf0) -bor (($b -shr 4) -band 0x0f)
            } else {
                $a
            }
            $parameter = if ($id -eq 0x13) {
                0
            } elseif ($id -eq 0x20) {
                if ($dungeonScriptPredicate -eq 'exact') { 1 } else { 0 }
            } else {
                $b
            }
            $triggerPredicate = if ($id -eq 0x1e -and $subid -le 0x07) {
                'bit'
            } elseif ($id -eq 0x20) {
                $dungeonScriptPredicate
            } elseif ($id -eq 0x21) {
                'exact'
            } else {
                'none'
            }
            $countSourceComplete = if ($conditionalDungeonEnemyRooms.Contains(
                "$mechanicGroup`:$($mechanicRoom.ToString('x2'))")) { 0 } else { 1 }
            $dungeonMechanicRows.Add(
                "$mechanicGroup`t$($mechanicRoom.ToString('x2'))`t$mechanicOrder`t$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($position.ToString('x2'))`t$($parameter.ToString('x2'))`t$triggerPredicate`t$countSourceComplete")
            if ($id -eq 0x20) { $permanentTriggerChestCount++ }
            if ($id -eq 0x21) { $retractableTriggerChestCount++ }
        }
    } elseif ($line -match '^\s*obj_Part\s+\$09\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<position>[0-9a-f]{2})\s*$') {
        $dungeonMechanicRows.Add(
            "$mechanicGroup`t$($mechanicRoom.ToString('x2'))`t$mechanicOrder`t09`t$($Matches['subid'])`t$($Matches['position'])`t00`tnone`t1")
    }
    $mechanicOrder++
}
if ($dungeonMechanicRows.Count -ne 156 -or
    $permanentTriggerChestCount -ne 7 -or
    $retractableTriggerChestCount -ne 6 -or
    -not ($dungeonMechanicRows -contains "4`t08`t0`t20`t00`t57`t01`texact`t1") -or
    -not ($dungeonMechanicRows -contains "4`t08`t1`t09`t00`t17`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t09`t0`t1e`t04`t07`t00`tbit`t1") -or
    -not ($dungeonMechanicRows -contains "4`t09`t1`t1e`t05`t5e`t00`tbit`t1") -or
    -not ($dungeonMechanicRows -contains "4`t09`t3`t13`t01`t2a`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t09`t5`t09`t00`t14`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t22`t1`t09`t80`t5b`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t7a`t0`t21`t17`t39`t01`texact`t1") -or
    -not ($dungeonMechanicRows -contains "4`t0c`t0`t13`t01`t47`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t0c`t1`t1e`t08`t07`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t0b`t0`t1e`t08`t07`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t0b`t1`t1e`t0b`t50`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t13`t0`t1e`t08`t07`t00`tnone`t0")) {
    throw "Expected 155 reusable dungeon button/trigger/chest/shutter placements including rooms 4:08/4:09/4:0b/4:0c/4:7a, parsed $($dungeonMechanicRows.Count - 1)."
}
$dungeonMechanicConstantRows = @(
    "# key`tvalue"
    "pushable-block`t29"
    "push-delay`t30"
    "solve-wait`t8"
    "door-frame-wait`t6"
    "open-tile`t160"
    "closed-up`t120"
    "closed-right`t121"
    "closed-down`t122"
    "closed-left`t123"
    "solve-sound`t77"
    "door-sound`t112"
    "button-tile`t12"
    "pressed-button-tile`t13"
    "button-radius-y`t2"
    "button-radius-x`t2"
    "button-object-release-delay`t28"
    "button-sound`t135"
    "chest-tile`t241"
    "chest-wait`t15"
    "puff-sound`t152"
)
$currentGroup = -1
$currentRoom = -1
$npcSpriteNames = [Collections.Generic.HashSet[string]]::new()

# INTERAC_TREASURE $60 overwrites its subid with the treasure object's graphic
# byte, then initializes that interaction's graphics. Export the corresponding
# sprite header and first animation so chest rewards do not incorrectly reuse
# the unrelated inventory-button display tables.
$treasureObjectVisualRows = [Collections.Generic.List[string]]::new()
$treasureObjectVisualRows.Add(
    "# graphic`tsprite`ttile-base`tpalette`tdefault-animation`tanimation")
$treasureObjectGraphics = @(
    $treasureObjectRecords.Values |
        ForEach-Object { [int]$_.Graphic } |
        Sort-Object -Unique)
foreach ($graphicIndex in $treasureObjectGraphics) {
    $graphic = $interactionGraphics["96`:$graphicIndex"]
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw "Could not resolve INTERAC_TREASURE `$60 graphic `$$($graphicIndex.ToString('x2'))."
    }
    $animation = Resolve-TreasureAnimation $graphic.DefaultAnimation
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve INTERAC_TREASURE `$60 graphic `$$($graphicIndex.ToString('x2')) animation `$$($graphic.DefaultAnimation.ToString('x2'))."
    }
    $spriteName = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($spriteName)
    $treasureObjectVisualRows.Add(
        "$($graphicIndex.ToString('x2'))`t$spriteName`t$($graphic.TileBase.ToString('x2'))`t$($graphic.Palette.ToString('x2'))`t$($graphic.DefaultAnimation.ToString('x2'))`t$animation")
}
$smallKeyVisual = $interactionGraphics['96:66']
if ($treasureObjectVisualRows.Count -ne 92 -or
    $null -eq $smallKeyVisual -or
    $gfxNames[$smallKeyVisual.Gfx] -ne 'spr_map_compass_keys_bookofseals' -or
    $smallKeyVisual.TileBase -ne 0x0c -or
    $smallKeyVisual.Palette -ne 5 -or
    $smallKeyVisual.DefaultAnimation -ne 0) {
    throw "Expected 91 INTERAC_TREASURE visuals including the small-key graphic `$42."
}
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $currentGroup = [Convert]::ToInt32($Matches['group'], 10)
        $currentRoom = [Convert]::ToInt32($Matches['room'], 16)
        continue
    }
    if ($currentGroup -lt 0 -or $line -notmatch 'obj_Interaction\s+\$(?<id>[0-9a-f]{2})\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})(?:\s+\$(?<var03>[0-9a-f]{2}))?') { continue }
    $id = [Convert]::ToInt32($Matches['id'], 16)
    if (-not $npcInteractionIds.Contains($id)) { continue }
    $subid = [Convert]::ToInt32($Matches['subid'], 16)
    # INTERAC_SHOOTING_GALLERY subids 0-2 are the human, goron, and elder
    # attendants; subid 3 is the invisible minigame controller.
    if ($id -eq 0x30 -and $subid -eq 0x03) { continue }
    $y = [Convert]::ToInt32($Matches['y'], 16)
    $x = [Convert]::ToInt32($Matches['x'], 16)
    $var03 = if ($Matches['var03']) {
        [Convert]::ToInt32($Matches['var03'], 16)
    } else {
        0
    }
    $row = New-NpcDataRow $currentGroup $currentRoom $id $subid $y $x $var03
    if ($row) { $npcRows.Add($row) }
}
if ($npcRows.Count -ne 380) {
    throw "Expected 379 positioned NPC/character records from Ages mainData.s, parsed $($npcRows.Count - 1)."
}
$villagerRow = $npcRows | Where-Object { $_ -match '^0\t48\t3a\t03\t' } | Select-Object -First 1
if (-not $villagerRow) { throw "The canonical room 0:48 villager record was not extracted." }
$villagerColumns = $villagerRow -split "`t"
if ($villagerColumns[7] -ne '1420' -or $villagerColumns[9] -ne '16' -or
    $villagerColumns[10] -ne '1' -or $villagerColumns[11] -ne '2' -or
    $villagerColumns[13] -ne '16@8,0,4,0;8,8,6,0|16@8,0,6,32;8,8,4,32' -or
    $villagerColumns[14] -ne '16@8,0,10,32;8,8,8,32|16@8,0,14,32;8,8,12,32' -or
    $villagerColumns[15] -ne '16@8,0,0,0;8,8,2,0|16@8,0,2,32;8,8,0,32' -or
    $villagerColumns[16] -ne '16@8,0,8,0;8,8,10,0|16@8,0,12,0;8,8,14,0') {
    throw "The room 0:48 villager no longer matches interaction3a animation/OAM data."
}
$introMonkeyRows = @($npcRows | Where-Object { $_ -match '^0\t5a\t39\t0[23]\t' })
if ($introMonkeyRows.Count -ne 2 -or
    ($introMonkeyRows[0] -split "`t")[7] -ne '5700' -or
    ($introMonkeyRows[1] -split "`t")[7] -ne '5701' -or
    ($introMonkeyRows[0] -split "`t")[11] -ne '6' -or
    ($introMonkeyRows[1] -split "`t")[11] -ne '7') {
    throw "Room 0:5a's intro monkeys no longer resolve TX_5700/TX_5701 and animations `$06/`$07."
}

# Room 2:ee is Vasu Jewelers. Preserve the complete placed object order, the
# non-Game-Link dialogue graph, and the animations used by Vasu, both snakes,
# and both help books. Text \call/\jump commands are assembler-time control
# flow, so flatten them here while the complete TX table is available; inline
# DialogueBox commands such as \stop, \col, and \opt remain intact.
$vasuShopScriptsSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\common\commonScripts.s')
$vasuShopScriptHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\common\scriptHelper.s')
$globalFlagsSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
$ringsSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\rings.s')
$wramSource = Get-Content -Raw (Join-Path $Disassembly 'include\wram.s')
$vasuTreasureObjectSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\treasureObjectData.s')
$ringMenuSource = Get-Content -Raw (Join-Path $Disassembly 'code\bank2.s')
$vasuShopRoomMatch = [regex]::Match(
    $mainObjectSource,
    '(?ms)^group2MapeeObjectData:\s+obj_Interaction \$89 \$00 \$28 \$50\s+obj_Interaction \$89 \$01 \$38 \$38\s+obj_Interaction \$89 \$06 \$38 \$68\s+obj_Interaction \$e5 \$00 \$48 \$28\s+obj_Interaction \$e5 \$01 \$48 \$78\s+obj_End')
if (-not $vasuShopRoomMatch.Success -or
    $vasuSource -notmatch '(?ms)^@state1:.*?ld c,\$18\s+call objectCheckLinkWithinDistance.*?jp nc,interactionSetAnimation.*?^@state2:.*?Interaction\.var36.*?GLOBALFLAG_FINISHEDGAME.*?wFileIsLinkedGame.*?^@scriptTable:\s+\.dw mainScripts\.blueSnakeScript_linked\s+\.dw mainScripts\.blueSnakeScript_preLinked\s+\.dw mainScripts\.redSnakeScript_linked\s+\.dw mainScripts\.redSnakeScript_preLinked' -or
    $vasuSource -notmatch '(?ms)^@state5Substate0:.*?Interaction\.counter1\s+ld \(hl\),a\s+ld l,Interaction\.counter2\s+ld \(hl\),\$02.*?^@state5Substate1:.*?blueSnakeExitScript_cableNotConnected' -or
    $ringHelpBookSource -notmatch '(?ms)^@state0:.*?ld a,\$06\s+call objectSetCollideRadius.*?ringHelpBookSubid0Script.*?ringHelpBookSubid1Script' -or
    $vasuShopScriptsSource -notmatch '(?ms)^vasuScript:.*?GLOBALFLAG_OBTAINED_RING_BOX.*?wIsLinkedGame.*?wObtainedRingBox.*?vasu_openRingMenu, \$00.*?vasu_openRingMenu, \$01.*?^redSnakeScript_preLinked:.*?wait 30.*?<TX_300a.*?^blueSnakeScript_preLinked:.*?<TX_301f.*?^ringHelpBookSubid1Script:.*?<TX_3019.*?<TX_301a.*?^ringHelpBookSubid0Script:.*?<TX_3020.*?<TX_3025.*?<TX_303d.*?<TX_3026' -or
    $vasuShopScriptHelperSource -notmatch '(?ms)^vasu_giveRingBox:.*?TREASURE_RING_BOX, \$00.*?w1Link\.yh.*?w1Link\.xh.*?^vasu_checkEarnedSpecialRing:.*?GLOBALFLAG_1000_ENEMIES_KILLED.*?GLOBALFLAG_10000_RUPEES_COLLECTED.*?GLOBALFLAG_BEAT_GANON.*?sub SLAYERS_RING.*?^vasu_giveFriendshipRing:\s+ld a,FRIENDSHIP_RING.*?^vasu_giveRingInVar3a:.*?jp giveRingToLink' -or
    $bank0Source -notmatch '(?ms)^linkInteractWithAButtonSensitiveObjects:.*?SpecialObject\.direction.*?call objectHCheckContainsPoint.*?^@positionOffsets:\s+\.db \$f6 \$00 ; DIR_UP\s+\.db \$00 \$0a ; DIR_RIGHT\s+\.db \$0a \$00 ; DIR_DOWN\s+\.db \$00 \$f6 ; DIR_LEFT' -or
    $bank0Source -notmatch '(?ms)^objectHCheckContainsPoint:.*?Object\.collisionRadiusY-Object\.xh.*?sub \(hl\)\s+ret nc\s+inc l\s+ld a,c\s+sub \(hl\)\s+ret' -or
    $bank0Source -notmatch '(?ms)^giveRingToLink:.*?call createRingTreasure.*?w1Link\.yh.*?^createRingTreasure:.*?TREASURE_RING.*?Interaction\.var38\s+set 6,b\s+ld \(hl\),b' -or
    $ringMenuSource -notmatch '(?ms)^ringMenu_unappraisedRings_state1:.*?RUPEEVAL_020.*?cpRupeeValue.*?RUPEEVAL_020.*?removeRupeeValue.*?wNumRingsAppraised.*?incHlRefWithCap.*?res 6,\(hl\).*?TX_301c' -or
    $ringMenuSource -notmatch '(?ms)^ringMenu_unappraisedRings_state3:.*?wRingsObtained.*?checkFlag.*?RUPEEVAL_030.*?ld a,40.*?^ringMenu_unappraisedRings_state4:.*?giveTreasure.*?wNumRingsAppraised.*?cp 100.*?GLOBALFLAG_APPRAISED_HUNDREDTH_RING.*?ld b,<TX_303c' -or
    $ringMenuSource -notmatch '(?ms)^ringMenu_unappraisedRings_gotoState5:.*?ld a,\$3c.*?^ringMenu_ringList_substate0:.*?wRingBoxContents.*?^@bPressed:.*?wActiveRing.*?ringMenu_checkRingIsInBox.*?closeMenu.*?^ringMenu_selectedRingFromList:.*?SND_SELECTITEM.*?wRingsObtained.*?ringMenu_checkRingIsInBox.*?wRingBoxContents' -or
    $vasuTreasureObjectSource -notmatch '(?m)^\s*m_TreasureSubid \$02, \$01, \$57, \$33, TREASURE_OBJECT_RING_BOX_00\s*$' -or
    $vasuTreasureObjectSource -notmatch '(?m)^\s*m_TreasureSubid \$09, \$ff, \$54, \$0e, TREASURE_OBJECT_RING_00\s*$' -or
    $globalFlagsSource -notmatch '(?m)^\s*GLOBALFLAG_1000_ENEMIES_KILLED\s+db ; \$00$' -or
    $globalFlagsSource -notmatch '(?m)^\s*GLOBALFLAG_OBTAINED_RING_BOX\s+db ; \$08$' -or
    $ringsSource -notmatch '(?m)^\s*FRIENDSHIP_RING\s+db ; \$00$' -or
    $ringsSource -notmatch '(?m)^\s*SLAYERS_RING\s+db ; \$34$' -or
    $ringsSource -notmatch '(?m)^\s*RUPEE_RING\s+db ; \$35$' -or
    $ringsSource -notmatch '(?m)^\s*VICTORY_RING\s+db ; \$36$' -or
    $wramSource -notmatch '(?m)^wObtainedRingBox: ; \$c615$' -or
    $wramSource -notmatch '(?m)^wRingsObtained: ; \$c616$' -or
    $wramSource -notmatch '(?m)^wNumRingsAppraised: ; \$c6ce$') {
    throw 'Room 2:ee Vasu Jewelers placement, predicates, scripts, or constants changed in the disassembly.'
}

function Resolve-ShopText(
    [int]$textId,
    [Collections.Generic.HashSet[int]]$visited
) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Vasu Jewelers text TX_$($textId.ToString('x4'))."
    }
    if ($visited.Contains($textId)) {
        throw "Vasu Jewelers text TX_$($textId.ToString('x4')) has recursive control flow."
    }
    [void]$visited.Add($textId)
    $message = [string]$allTexts[$textId]
    while ($true) {
        $call = [regex]::Match($message, '\\call\(TX_(?<id>[0-9a-f]{4})\)')
        if (-not $call.Success) { break }
        $calledId = [Convert]::ToInt32($call.Groups['id'].Value, 16)
        $calledText = Resolve-ShopText $calledId $visited
        $message = $message.Substring(0, $call.Index) + $calledText +
            $message.Substring($call.Index + $call.Length)
    }
    $jump = [regex]::Match($message, '\\jump\(TX_(?<id>[0-9a-f]{4})\)')
    if ($jump.Success) {
        $jumpedId = [Convert]::ToInt32($jump.Groups['id'].Value, 16)
        $message = $message.Substring(0, $jump.Index) +
            (Resolve-ShopText $jumpedId $visited)
    }
    [void]$visited.Remove($textId)
    return $message
}

$vasuShopTextIds = @(
    0x3000, 0x3002, 0x3003, 0x3004, 0x3005, 0x3006, 0x3007, 0x3008,
    0x3009, 0x300a, 0x300b, 0x300c,
    0x300e, 0x300f, 0x3010, 0x3014, 0x3015, 0x3016, 0x3018,
    0x3011, 0x3012, 0x3013, 0x3017, 0x3019, 0x301a, 0x301c,
    0x301f, 0x3020, 0x3024, 0x3025, 0x3026,
    0x3028, 0x302e, 0x3033, 0x3036, 0x3037, 0x3039, 0x303a, 0x303b,
    0x3038, 0x303c, 0x303d, 0x303e, 0x303f
)
$vasuShopTextRows = [Collections.Generic.List[string]]::new()
$vasuShopTextRows.Add("# text-id`tutf8-base64")
foreach ($textId in $vasuShopTextIds) {
    $message = Resolve-ShopText $textId ([Collections.Generic.HashSet[int]]::new())
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message))
    $vasuShopTextRows.Add("$($textId.ToString('x4'))`t$encoded")
}

$vasuShopAnimationRows = [Collections.Generic.List[string]]::new()
$vasuShopAnimationRows.Add("# interaction-id`tanimation`tencoded-animation")
foreach ($spec in @(@(0x89, 9), @(0xe5, 2))) {
    for ($animationIndex = 0; $animationIndex -lt $spec[1]; $animationIndex++) {
        $animation = Resolve-NpcAnimation $spec[0] $animationIndex
        if ([string]::IsNullOrWhiteSpace($animation)) {
            throw "Could not resolve a Vasu Jewelers animation from the disassembly."
        }
        $vasuShopAnimationRows.Add(
            "$(([int]$spec[0]).ToString('x2'))`t$($animationIndex.ToString('x2'))`t$animation")
    }
}

$vasuShopConstantRows = @(
    "# key`tvalue",
    "group`t2",
    "room`t238",
    "textbox-position`t2",
    "snake-proximity-radius`t24",
    "red-snake-wait`t30",
    "blue-snake-cable-timeout`t512",
    "vasu-radius-y`t18",
    "vasu-radius-x`t6",
    "snake-radius`t6",
    "a-button-point-offset`t10",
    "ring-box-grab-mode`t2",
    "ring-grab-mode`t1",
    "obtained-ring-box-address`t50709",
    "rings-obtained-address`t50710",
    "rings-obtained-byte-count`t8",
    "rings-appraised-address`t50894",
    "linked-first-mask`t1",
    "appraisal-cost`t20",
    "duplicate-refund`t30",
    "menu-close-wait`t10",
    "appraisal-result-wait`t40",
    "menu-exit-wait`t60",
    "global-earned-slayer`t0",
    "global-earned-wealth`t1",
    "global-earned-victory`t2",
    "global-got-slayer`t4",
    "global-got-wealth`t5",
    "global-got-victory`t6",
    "global-obtained-ring-box`t8",
    "global-appraised-hundredth`t9",
    "ring-friendship`t0",
    "ring-slayer`t52",
    "ring-wealth`t53",
    "ring-victory`t54",
    "ring-hundredth`t56"
)

# Past room 2:5e is the normal Lynna shop. INTERAC_SHOP_ITEM $47 owns the
# stock substitutions and product graphics while INTERAC_SHOPKEEPER $46 owns
# the prompts, purchase result, and theft-prevention script. Export every item
# reachable from this room's three placements so the runtime follows the
# source replacement chain instead of encoding a room-specific stock list.
$shopItemSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\shopItem.s')
$shopkeeperSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\shopkeeper.s')
$companionScriptsSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\companionScripts.s')
$roomGfxChangesSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\roomGfxChanges.s')
$treeGfxHeadersSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\treeGfxHeaders.s')
$linkSpecialObjectSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$linkAnimationSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
$linkAnimationLogicSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\specialObjectAnimationsAndDamage.s')
$parentItemUsageSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\parentItemUsage.s')
$grabbedObjectSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\itemParents\commonCode.s')
$linkGfxPointerBlock = [regex]::Match(
    $linkAnimationSource,
    '(?ms)^specialObject00GfxPointers:(?<body>.*?)(?=^specialObject00AnimationDataPointers:)')
$linkGfxEntries = if ($linkGfxPointerBlock.Success) {
    @([regex]::Matches(
        $linkGfxPointerBlock.Groups['body'].Value,
        'm_SpecialObjectGfxPointer \$(?<oam>[0-9a-f]{2}) spr_link \$(?<offset>[0-9a-f]{4}) \$[0-9a-f]{2}'))
} else { @() }
$expectedHeldLinkGfx = @{
    0x5c = @(0x00, 0x0040); 0x5d = @(0x01, 0x01c0)
    0x5e = @(0x00, 0x0180); 0x5f = @(0x00, 0x01c0)
    0x88 = @(0x01, 0x0040); 0x89 = @(0x01, 0x1140)
    0x8a = @(0x01, 0x0180); 0x8b = @(0x00, 0x1140)
}
$heldLinkGfxValid = $linkGfxEntries.Count -gt 0x8b
if ($heldLinkGfxValid) {
    foreach ($index in $expectedHeldLinkGfx.Keys) {
        $entry = $linkGfxEntries[$index]
        $expected = $expectedHeldLinkGfx[$index]
        if ([Convert]::ToInt32($entry.Groups['oam'].Value, 16) -ne $expected[0] -or
            [Convert]::ToInt32($entry.Groups['offset'].Value, 16) -ne $expected[1]) {
            $heldLinkGfxValid = $false
            break
        }
    }
}
$lynnaShopRoomMatch = [regex]::Match(
    $mainObjectSource,
    '(?ms)^group2Map5eObjectData:\s+obj_Interaction \$47 \$01 \$28 \$80\s+obj_Interaction \$47 \$03 \$28 \$68\s+obj_Interaction \$47 \$04 \$28 \$50\s+obj_Interaction \$46 \$00 \$58 \$88\s+obj_Interaction \$71 \$0c\s+obj_End')
if (-not $lynnaShopRoomMatch.Success -or
    $shopItemSource -notmatch '(?ms)^shopItemState0:.*?TREASURE_BOMBS.*?shopItemPopStackAndDeleteSelf.*?cp \$03.*?checkIsLinkedGame.*?ld a,\$13.*?GLOBALFLAG_CAN_BUY_FLUTE.*?wBoughtShopItems2.*?shopItemReplacementTable' -or
    $shopItemSource -notmatch '(?ms)^shopItemCheckGrabbed:.*?BTN_A\|BTN_B.*?sub \$0d.*?cp \$3d.*?w1Link\.direction' -or
    $shopItemSource -notmatch '(?ms)^shopItemGetTilesForRupeeDisplay:.*?ld e,\$06.*?ld d,\$30.*?@drawDigit' -or
    $bank0Source -notmatch '(?ms)^checkGrabbableObjects:.*?call _getLinkPositionPlusDirectionOffset.*?call _checkCollisionWithHAndD.*?^_getLinkPositionPlusDirectionOffset:.*?^@positionOffsets:\s+\.dw \$00fa ; DIR_UP\s+\.dw \$0500 ; DIR_RIGHT\s+\.dw \$0005 ; DIR_DOWN\s+\.dw \$fa00 ; DIR_LEFT' -or
    $linkSpecialObjectSource -notmatch '(?ms)^linkState00:.*?SpecialObject\.collisionType.*?ld a,\$80\s+ldi \(hl\),a.*?inc l\s+ld a,\$06\s+ldi \(hl\),a\s+ldi \(hl\),a' -or
    -not $heldLinkGfxValid -or
    $linkAnimationLogicSource -notmatch '(?ms)^@notUnderwater:\s*ld c,\$00\s*ld a,\(wLinkGrabState\)\s*bit 6,a\s*ret nz\s*; Check if he.s holding something\s*or a\s*jr z,\+\s*ld c,\$02' -or
    $parentItemUsageSource -notmatch '(?ms)^checkShopInput:.*?ld a,\(wGameKeysJustPressed\).*?and \$03.*?call checkGrabbableObjects.*?ld a,\$83\s*ld \(wLinkGrabState\),a' -or
    $grabbedObjectSource -notmatch '(?ms)^updateGrabbedObjectPosition:.*?cp \$83.*?w1Link\.animParameter.*?and \$0f.*?add b.*?^@liftedObjectPositions:.*?; Weight 0.*?\.db \$f3 \$00 \$f2 \$00 \$f3 \$00 \$f2 \$00 ; Frame 2.*?\.db \$f3 \$00 \$f3 \$00 \$f3 \$00 \$f3 \$00 ; Frame 3' -or
    $shopItemSource -notmatch '(?ms)^shopItemState2:.*?^@substate0:\s*ld a,\$01\s*ld \(de\),a.*?ld a,\$08\s*ld \(wLinkGrabState2\),a\s*call objectSetVisible80' -or
    $roomGfxChangesSource -notmatch '(?ms)^roomTileChangesAfterLoad04:.*?wInShop.*?TREE_GFXH_03.*?loadTreeGfx' -or
    $treeGfxHeadersSource -notmatch '(?m)^\s*/\* \$03 \*/ m_ObjectGfxHeader gfx_inventory_hud_1\s*$' -or
    $shopkeeperSource -notmatch '(?ms)^shopkeeperState0:.*?ld bc,\$0614.*?ld a,>TX_0e00.*?^shopkeeperState1:.*?ld c,\$69.*?wLinkGrabState.*?shopkeeperTheftPreventionScriptTable' -or
    $shopkeeperSource -notmatch '(?ms)^shopkeeperCheckLinkHasItemAlready:.*?cp \$13.*?cp \$03.*?cp \$0d.*?wNumBombs.*?wLinkHealth.*?TREASURE_SHIELD.*?TREASURE_FLUTE' -or
    $vasuShopScriptsSource -notmatch '(?ms)^shopkeeperScript_lynnaShopWelcome:.*?<TX_0e00.*?^shopkeeperScript_boughtEverything:.*?<TX_0e26.*?^shopkeeperScript_purchaseItem:.*?@buy3Hearts:.*?<TX_0e02.*?@buyL1Shield:.*?<TX_0e03.*?@buy10Bombs:.*?<TX_0e04.*?@buyStrangeFlute:.*?<TX_0e1b.*?@buyNormalShopGashaSeed:.*?<TX_0e1d' -or
    $companionScriptsSource -notmatch '(?ms)^companionScript_subid0c:.*?wDimitriState.*?bit 5,a.*?or \$40.*?wDimitriState') {
    throw 'Room 2:5e Lynna shop placement, graphics, predicates, scripts, or companion state changed in the disassembly.'
}

$lynnaShopDefinitions = @(
    # subid, price tile, price, treasure, parameter, prompt, item text,
    # replacement address, mask, replacement subid, x offset
    @(0x01, 0x6f,  10, 0x29, 0x0c, 0x0e02, 0x004c, 0xc643, 0x08, 0x0d, 4),
    @(0x03, 0x6c,  30, 0x01, 0x01, 0x0e03, 0x001f, 0xc6af, 0x02, 0x11, 0),
    @(0x04, 0x69,  20, 0x03, 0x10, 0x0e04, 0x004d, 0xc642, 0x00, 0xff, 0),
    @(0x0d, 0x67, 150, 0x0e, 0x0c, 0x0e1b, 0x003b, 0xc643, 0x00, 0xff, 0),
    @(0x11, 0x6f,  50, 0x01, 0x02, 0x0e29, 0x0020, 0xc6af, 0x01, 0x12, 0),
    @(0x12, 0x6c,  80, 0x01, 0x03, 0x0e2a, 0x0021, 0xc6af, 0x00, 0xff, 0),
    @(0x13, 0x6c,  30, 0x34, 0x01, 0x0e1d, 0x004b, 0xc642, 0x20, 0x03, 0)
)
$lynnaShopPlacementBySubId = @{
    0x01 = @(0, 0x28, 0x80)
    0x03 = @(1, 0x28, 0x68)
    0x04 = @(2, 0x28, 0x50)
}
$lynnaShopItemRows = [Collections.Generic.List[string]]::new()
$lynnaShopItemRows.Add(
    "# subid`torder`ty`tx`tprice-tile`tprice`ttreasure`tparameter`tprompt-text`titem-text`tsprite`ttile-base`tpalette`tanimation-index`tencoded-animation`treplacement-address`treplacement-mask`treplacement-subid`treplacement-x-offset")
foreach ($definition in $lynnaShopDefinitions) {
    $subid = [int]$definition[0]
    $placement = $lynnaShopPlacementBySubId[$subid]
    $order = if ($null -eq $placement) { -1 } else { [int]$placement[0] }
    $y = if ($null -eq $placement) { 0 } else { [int]$placement[1] }
    $x = if ($null -eq $placement) { 0 } else { [int]$placement[2] }
    $graphic = $interactionGraphics["71:$subid"]
    if ($null -eq $graphic) {
        throw "Could not resolve Lynna shop item `$47:`$$($subid.ToString('x2'))."
    }
    $animationIndex = $graphic.DefaultAnimation
    # INTERAC_SHOP_ITEM aliases INTERAC_TREASURE's animation and contiguous
    # OAM-pointer bases; some stock frames intentionally index beyond the
    # first labeled four-word block.
    $animation = Resolve-TreasureAnimation $animationIndex
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve Lynna shop item `$47:`$$($subid.ToString('x2')) animation."
    }
    $spriteName = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($spriteName)
    $lynnaShopItemRows.Add(
        "$($subid.ToString('x2'))`t$order`t$y`t$x`t$(([int]$definition[1]).ToString('x2'))`t$([int]$definition[2])`t$(([int]$definition[3]).ToString('x2'))`t$(([int]$definition[4]).ToString('x2'))`t$(([int]$definition[5]).ToString('x4'))`t$(([int]$definition[6]).ToString('x4'))`t$spriteName`t$($graphic.TileBase.ToString('x2'))`t$($graphic.Palette.ToString('x2'))`t$($animationIndex.ToString('x2'))`t$animation`t$(([int]$definition[7]).ToString('x4'))`t$(([int]$definition[8]).ToString('x2'))`t$(([int]$definition[9]).ToString('x2'))`t$([int]$definition[10])")
}

$lynnaShopTextIds = @(
    0x0e00, 0x0e02, 0x0e03, 0x0e04, 0x0e05, 0x0e06, 0x0e07,
    0x0e1b, 0x0e1d, 0x0e26, 0x0e29, 0x0e2a,
    0x004b, 0x004c, 0x004d, 0x001f, 0x0020, 0x0021, 0x003b)
$lynnaShopTextRows = [Collections.Generic.List[string]]::new()
$lynnaShopTextRows.Add("# text-id`tutf8-base64")
foreach ($textId in $lynnaShopTextIds) {
    $message = Resolve-ShopText $textId ([Collections.Generic.HashSet[int]]::new())
    if ($textId -eq 0x0e2a) {
        # TX_0e2a deliberately has no terminator and falls through to the
        # adjacent TX_0e2b option body in the compiled text bank.
        $message += Resolve-ShopText 0x0e2b ([Collections.Generic.HashSet[int]]::new())
    }
    # cmd8 $0f installs the source choice handler. DialogueBox already owns
    # the two imported \opt markers, so retaining it would render a raw token.
    $message = $message.Replace('\cmd8(0x0f)', '')
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message))
    $lynnaShopTextRows.Add("$($textId.ToString('x4'))`t$encoded")
}

$lynnaShopAnimationRows = [Collections.Generic.List[string]]::new()
$lynnaShopAnimationRows.Add("# interaction-id`tanimation`tencoded-animation")
foreach ($animationIndex in 0..3) {
    $animation = Resolve-NpcAnimation 0x46 $animationIndex
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw 'Could not resolve a Lynna shopkeeper animation from the disassembly.'
    }
    $lynnaShopAnimationRows.Add(
        "46`t$($animationIndex.ToString('x2'))`t$animation")
}

$lynnaShopConstantRows = @(
    "# key`tvalue",
    "group`t2",
    "room`t94",
    "textbox-position`t0",
    "item-collision-radius`t7",
    "link-collision-radius`t6",
    "grab-negative-point-offset`t6",
    "grab-positive-point-offset`t5",
    "shopkeeper-radius-y`t6",
    "shopkeeper-radius-x`t20",
    "a-button-point-offset`t10",
    "selection-link-y-limit`t61",
    "selection-x-radius`t13",
    "theft-link-y`t105",
    "bought-items-1-address`t50754",
    "bought-items-2-address`t50755",
    "dimitri-state-address`t50759",
    "dimitri-saved-mask`t32",
    "dimitri-disappear-mask`t64",
    "global-can-buy-flute`t29",
    "normal-gasha-bought-mask`t32",
    "flute-stock-mask`t8",
    "bombchu-owned-mask`t16",
    "bombchu-missing-mask`t32",
    "specialobject-dimitri`t12"
)

# Past room 1:48's pickaxe worker is a native room interaction. Animation $02
# carries one-update strike parameters which play SND_CLINK and create two
# INTERAC_FALLING_ROCK $92:$06 dirt chips. Export the worker's script-selected
# visuals, text, and the debris physics as one typed record. The debris has no
# object graphics header and sets OAM flag bit 3, so tile $02 comes from the
# fixed bank-1 spr_common_sprites sheet rather than the worker's dynamic slot.
$room148ObjectSource = $mainObjectLines -join "`n"
$room148WorkerSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\pickaxeWorker.s')
$room148FallingRockSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\fallingRock.s')
$agesMainScriptSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$room148VillagerSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\villager.s')
$room148PastGirlSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\pastGirl.s')
$gameProgress2Source = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\miscMan2.s')
$objectSpeedSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$musicConstantSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\music.s')
$agesGfxHeaderSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\gfxHeaders.s')
if ($room148ObjectSource -notmatch '(?ms)^group1Map48ObjectData:\s+obj_Interaction \$57 \$00 \$58 \$38\s+obj_Interaction \$e1 \$02 \$48 \$58\s+obj_Interaction \$3a \$06 \$58 \$88\s+obj_Interaction \$38 \$00 \$38 \$78\s+obj_End' -or
    $room148WorkerSource -notmatch '(?ms)^@subid00:\s*^@subid03:.*?@loadScriptAndInitGraphics.*?interactionSetAlwaysUpdateBit.*?interactionRunScript.*?interactionAnimateAsNpc.*?Interaction\.animParameter.*?SND_CLINK.*?wScrollMode.*?and \$01.*?ld a,\$03.*?@createDirtChips' -or
    $room148WorkerSource -notmatch '(?ms)^@loadScriptAndInitGraphics:.*?>TX_1b00.*?@scriptTable:.*?pickaxeWorkerSubid00Script' -or
    $room148WorkerSource -notmatch '(?ms)^@createDirtChips:.*?ld b,\$02.*?INTERAC_FALLING_ROCK.*?ld \(hl\),\$06.*?Interaction\.counter2.*?Interaction\.angle.*?objectCopyPosition.*?add \$04.*?cp \$01.*?add \$0e\*2.*?sub \$0e' -or
    $agesMainScriptSource -notmatch '(?ms)^pickaxeWorkerSubid00Script:\s+initcollisions\s+@npcLoop:\s+asm15 interactionSetAnimation, \$02\s+checkabutton\s+asm15 interactionSetAnimation, \$03\s+showtextlowindex <TX_1b00\s+scriptjump @npcLoop' -or
    $room148FallingRockSource -notmatch '(?ms)^fallingRock_subid06:.*?fallingRock_initGraphicsAndIncState.*?interactionSetAlwaysUpdateBit.*?Interaction\.var03.*?or \$08.*?Interaction\.counter2.*?Interaction\.angle.*?SPEED_80.*?Interaction\.speedZ.*?ld a,\$40.*?ld \(hl\),\$ff.*?^@angles:\s+\.db \$08 \$18' -or
    $room148FallingRockSource -notmatch '(?ms)^fallingRock_updateSpeedAndDeleteWhenLanded:\s+ld c,\$18\s+call objectUpdateSpeedZ_paramC\s+jp z,interactionDelete\s+jp objectApplySpeed' -or
    $agesGfxHeaderSource -notmatch '(?ms)^m_GfxHeaderStart \$83, GFXH_COMMON_SPRITES\s+m_GfxHeader spr_common_sprites, \$8001\s+m_GfxHeaderEnd' -or
    $room148VillagerSource -notmatch '(?ms)^@initSubid06:\s*^@initSubid07:\s+callab agesInteractionsBank09\.getGameProgress_2\s+ld c,\$06\s+ld a,\$04\s+call checkNpcShouldExistAtGameStage\s+jp nz,interactionDelete\s+ld a,b\s+ld hl,@subid6And7ScriptTable' -or
    $room148PastGirlSource -notmatch '(?ms)^@subid0Init:\s+callab agesInteractionsBank09\.getGameProgress_2.*?ld a,b\s+cp \$01\s+jp z,interactionDelete\s+cp \$02\s+jp z,interactionDelete\s+ld a,b\s+ld hl,@scriptTable' -or
    $gameProgress2Source -notmatch '(?ms)^getGameProgress_2:\s+ld b,\$07.*?GLOBALFLAG_FINISHEDGAME.*?ret nz.*?dec b\s+call checkIsLinkedGame.*?wGroup4RoomFlags\+\$fc.*?bit 7,\(hl\).*?ret nz.*?dec b\s+ld a,GLOBALFLAG_SAW_TWINROVA_BEFORE_ENDGAME.*?ret nz.*?TREASURE_ESSENCE.*?getHighestSetBit.*?ld b,\$04\s+cp \$06\s+ret nc.*?dec b\s+ld a,GLOBALFLAG_SAVED_NAYRU.*?ret nz.*?dec b.*?cp \$03\s+ret nc\s+dec b.*?cp \$01\s+ret nc\s+^@noEssences:\s+ld b,\$00\s+ret' -or
    $gameProgress2Source -notmatch '(?ms)^@data4:.*?\.dw @@subid6\s+\.dw @@subid7\s+@@subid6:\s+\.db \$00 \$01 \$02 \$ff\s+@@subid7:\s+\.db \$03 \$04 \$05 \$06 \$07 \$ff') {
    throw 'Room 1:48 NPC, getGameProgress_2 predicate, strike animation, or dirt-chip behavior changed in the disassembly.'
}

$room148SpeedMatch = [regex]::Match(
    $objectSpeedSource,
    '(?m)^\s*SPEED_80\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})')
$room148SoundMatch = [regex]::Match(
    $musicConstantSource,
    '(?m)^\s*SND_CLINK\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$room148WorkerGraphic = $interactionGraphics['87:0']
$room148DebrisGraphic = $interactionGraphics['146:6']
$room148WorkAnimation = Resolve-NpcAnimation 0x57 0x02
$room148TalkAnimation = Resolve-NpcAnimation 0x57 0x03
$room148DebrisAnimation = Resolve-NpcAnimation 0x92 0x01
if (-not $room148SpeedMatch.Success -or
    -not $room148SoundMatch.Success -or
    $null -eq $room148WorkerGraphic -or
    $room148WorkerGraphic.Gfx -ne 0x4a -or
    $room148WorkerGraphic.TileBase -ne 0 -or
    $room148WorkerGraphic.Palette -ne 0 -or
    $null -eq $room148DebrisGraphic -or
    $room148DebrisGraphic.Gfx -ne 0 -or
    $room148DebrisGraphic.TileBase -ne 2 -or
    $room148DebrisGraphic.Flags -ne 0x81 -or
    $room148DebrisGraphic.DefaultAnimation -ne 1 -or
    -not $gfxNames.ContainsKey($room148WorkerGraphic.Gfx) -or
    -not $room148WorkAnimation -or
    -not $room148TalkAnimation -or
    -not $room148DebrisAnimation -or
    -not $allTexts.ContainsKey(0x1b00)) {
    throw 'Could not resolve room 1:48 worker graphics, debris graphics, animations, sound, speed, or TX_1b00.'
}
$room148SpriteName = $gfxNames[$room148WorkerGraphic.Gfx]
$room148DebrisSpriteName = 'spr_common_sprites'
[void]$npcSpriteNames.Add($room148SpriteName)
[void]$npcSpriteNames.Add($room148DebrisSpriteName)
$room148Text = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($allTexts[0x1b00]))
$room148PickaxeRows = @(
    "# worker-sprite`tworker-tile-base`tworker-palette`twork-animation`ttalk-animation`tdebris-sprite`tdebris-tile-base`tdebris-animation`ttext-id`tutf8-base64`tsound`tdebris-count`toffset-y`toffset-x`tspeed`tspeed-z`tgravity`tangle-0`tangle-1",
    "$room148SpriteName`t$($room148WorkerGraphic.TileBase)`t$($room148WorkerGraphic.Palette)`t$room148WorkAnimation`t$room148TalkAnimation`t$room148DebrisSpriteName`t$($room148DebrisGraphic.TileBase)`t$room148DebrisAnimation`t1b00`t$room148Text`t$([Convert]::ToInt32($room148SoundMatch.Groups['value'].Value, 16))`t2`t4`t14`t$([Convert]::ToInt32($room148SpeedMatch.Groups['value'].Value, 16))`t-192`t24`t8`t24"
)

# The lower Black Tower construction rooms share four native handlers whose
# behavior is selected by placement var03 and the game-wide RNG. Pin the five
# complete object streams and export the script tables, extra animation, item
# visual, text, and timing values used by those handlers. Runtime still uses
# the ordinary NPC rows for positioned graphics and source ordering.
$blackTowerHardhatSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\hardhatWorker.s')
$blackTowerSoldierSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\soldier.s')
$blackTowerDungeonSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\dungeonStuff.s')
$agesScriptHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$blackTowerRooms = @{
    'e0' = 'obj_Interaction \$3a \$02 \$98 \$38\s+obj_End'
    'e1' = 'obj_Interaction \$58 \$00 \$98 \$48\s+obj_Interaction \$40 \$0c \$68 \$58\s+obj_Interaction \$57 \$03 \$38 \$48 \$00\s+obj_Interaction \$57 \$03 \$58 \$88 \$01\s+obj_End'
    'e2' = 'obj_Interaction \$40 \$0c \$98 \$d8\s+obj_Interaction \$58 \$00 \$58 \$88 \$01\s+obj_Interaction \$58 \$03 \$68 \$28 \$03\s+obj_Interaction \$57 \$03 \$48 \$78 \$02\s+obj_Interaction \$57 \$03 \$58 \$98 \$03\s+obj_End'
    'e7' = 'obj_Interaction \$40 \$0c \$78 \$a8\s+obj_Interaction \$12 \$00 \$88 \$78\s+obj_Interaction \$58 \$03 \$58 \$28 \$00\s+obj_Interaction \$58 \$03 \$48 \$38 \$01\s+obj_Interaction \$57 \$03 \$38 \$28 \$04\s+obj_Interaction \$57 \$03 \$88 \$c8 \$05\s+obj_End'
    'e8' = 'obj_Interaction \$58 \$03 \$48 \$28 \$02\s+obj_Interaction \$57 \$03 \$68 \$78 \$06\s+obj_Interaction \$57 \$03 \$58 \$98 \$07\s+obj_End'
}
foreach ($entry in $blackTowerRooms.GetEnumerator()) {
    if ($room148ObjectSource -notmatch
        "(?ms)^group4Map$($entry.Key)ObjectData:\s+$($entry.Value)") {
        throw "Black Tower room 4:$($entry.Key) object stream changed in mainData.s."
    }
}
if ($room148WorkerSource -notmatch '(?ms)^@subid00:\s*^@subid03:.*?SND_CLINK.*?@createDirtChips' -or
    $agesMainScriptSource -notmatch '(?ms)^pickaxeWorkerSubid03Script:.*?pickaxeWorker_setAnimationFromVar03.*?pickaxeWorker_chooseRandomBlackTowerText.*?showloadedtext' -or
    $agesScriptHelperSource -notmatch '(?ms)^pickaxeWorker_setAnimationFromVar03:.*?@animations:\s+\.db \$00 \$01 \$00 \$01 \$00 \$01 \$01 \$01' -or
    $agesScriptHelperSource -notmatch '(?ms)^pickaxeWorker_chooseRandomBlackTowerText:.*?getRandomNumber.*?and \$07.*?@blackTowerText:\s+\.db <TX_1b01\s+\.db <TX_1b02\s+\.db <TX_1b03\s+\.db <TX_1b04\s+\.db <TX_1b05\s+\.db <TX_1b01\s+\.db <TX_1b02\s+\.db <TX_1b03' -or
    $blackTowerHardhatSource -notmatch '(?ms)^@subid00:.*?interactionSetAlwaysUpdateBit.*?ld a,\$04.*?interactionSetAnimation.*?^@subid03:.*?interactionAnimateBasedOnSpeed.*?interactionPushLinkAwayAndUpdateDrawPriority' -or
    $agesMainScriptSource -notmatch '(?ms)^hardhatWorkerSubid00Script:.*?jumpifroomflagset \$20.*?TX_1001.*?wait 30.*?giveitem TREASURE_SHOVEL, \$00.*?wait 30.*?TX_1002.*?TX_1000.*?setanimation \$04' -or
    $agesMainScriptSource -notmatch '(?ms)^hardhatWorkerFunc_patrol:.*?hardhatWorker_decPatrolCounter.*?objectApplySpeed.*?wait 20.*?disableinput.*?turnToFaceLink.*?showloadedtext.*?wait 30.*?hardhatWorker_updatePatrolAnimation.*?enableinput' -or
    $agesScriptHelperSource -notmatch '(?ms)^hardhatWorker_chooseTextForPatroller:.*?cp \$04.*?getRandomNumber.*?and \$03.*?@textIDs:\s+\.db <TX_100a.*?\.db <TX_100b.*?\.db <TX_100c.*?\.db <TX_100c.*?\.db <TX_100d' -or
    $blackTowerSoldierSource -notmatch '(?ms)^soldierSubid00:\s*^soldierSubid01:.*?GLOBALFLAG_FINISHEDGAME.*?GLOBALFLAG_0b.*?jr soldierSubid0c.*?^soldierSubid0c:.*?soldierInitGraphicsAndLoadScript.*?npcFaceLinkAndAnimate' -or
    $agesScriptHelperSource -notmatch '(?ms)^soldierGetRandomVar32Val:.*?getRandomNumber.*?and \$03.*?@data:\s+\.db \$0d \$0e \$0f \$0d' -or
    $room148VillagerSource -notmatch '(?ms)^@runSubid02:.*?objectSetCollideRadii.*?ld b,\$11.*?ld b,\$ef.*?objectCheckCollidedWithLink_ignoreZ.*?villagerSubid02Script_part2.*?Interaction\.var39.*?Interaction\.var3d' -or
    $agesMainScriptSource -notmatch '(?ms)^villagerSubid02Script_part2:.*?disableinput.*?SPEED_100.*?moveleft \$10.*?moveright \$10.*?villager_setLinkYToVar39.*?wait 10.*?enableinput' -or
    $blackTowerDungeonSource -notmatch '(?ms)^@subid00:.*?SCROLLMODE_02.*?cp \$78.*?objectSetCollideRadius.*?@dungeonTextIndices:.*?<TX_020f.*?@initialSpinnerValues:.*?\.db \$01 \$00 \$00 \$00 \$01 \$00 \$00 \$00') {
    throw 'Black Tower worker, soldier, blocker, or entrance behavior changed in the disassembly.'
}

$blackTowerTextRows = [Collections.Generic.List[string]]::new()
$blackTowerTextRows.Add("# text-id`tutf8-base64")
foreach ($textId in @(
    0x0025, 0x020f, 0x1000, 0x1001, 0x1002,
    0x100a, 0x100b, 0x100c, 0x100d,
    0x1b01, 0x1b02, 0x1b03, 0x1b04, 0x1b05,
    0x590d, 0x590e, 0x590f)) {
    # text.yaml intentionally aliases dungeon labels TX_020e/TX_020f to one
    # payload; the generic text loader keys that payload by its first label.
    $sourceTextId = if ($textId -eq 0x020f) { 0x020e } else { $textId }
    if (-not $allTexts.ContainsKey($sourceTextId)) {
        throw "Could not resolve Black Tower text TX_$($textId.ToString('x4'))."
    }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$sourceTextId]))
    $blackTowerTextRows.Add("$($textId.ToString('x4'))`t$encoded")
}

$blackTowerVisualRows = [Collections.Generic.List[string]]::new()
$blackTowerVisualRows.Add("# key`tsprite`ttile-base`tpalette`tanimation")
foreach ($spec in @(
    @{ Key = 'pickaxe-0'; Id = 0x57; Subid = 0x03; Animation = 0x00 },
    @{ Key = 'pickaxe-1'; Id = 0x57; Subid = 0x03; Animation = 0x01 },
    @{ Key = 'hardhat-0'; Id = 0x58; Subid = 0x03; Animation = 0x00 },
    @{ Key = 'hardhat-1'; Id = 0x58; Subid = 0x03; Animation = 0x01 },
    @{ Key = 'hardhat-2'; Id = 0x58; Subid = 0x03; Animation = 0x02 },
    @{ Key = 'hardhat-3'; Id = 0x58; Subid = 0x03; Animation = 0x03 },
    @{ Key = 'hardhat-work'; Id = 0x58; Subid = 0x00; Animation = 0x04 },
    @{ Key = 'soldier-0'; Id = 0x40; Subid = 0x0c; Animation = 0x00 },
    @{ Key = 'soldier-1'; Id = 0x40; Subid = 0x0c; Animation = 0x01 },
    @{ Key = 'soldier-2'; Id = 0x40; Subid = 0x0c; Animation = 0x02 },
    @{ Key = 'soldier-3'; Id = 0x40; Subid = 0x0c; Animation = 0x03 },
    # TREASURE_OBJECT_SHOVEL_00 uses graphic $1b, which is interaction $60
    # subid $1b after the treasure loader overwrites its subid.
    @{ Key = 'shovel'; Id = 0x60; Subid = 0x1b; Animation = -1 }
)) {
    $graphic = $interactionGraphics["$([int]$spec.Id)`:$([int]$spec.Subid)"]
    if ($null -eq $graphic) {
        $graphic = $interactionGraphics["$([int]$spec.Id)`:0"]
    }
    if ($null -eq $graphic) {
        throw "Could not resolve Black Tower visual '$($spec.Key)' graphics."
    }
    $animationIndex = if ([int]$spec.Animation -ge 0) {
        [int]$spec.Animation
    } else {
        [int]$graphic.DefaultAnimation
    }
    $animation = Resolve-NpcAnimation ([int]$spec.Id) $animationIndex
    if (-not $gfxNames.ContainsKey($graphic.Gfx) -or -not $animation) {
        throw "Could not resolve Black Tower visual '$($spec.Key)' animation."
    }
    $spriteName = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($spriteName)
    $blackTowerVisualRows.Add(
        "$($spec.Key)`t$spriteName`t$($graphic.TileBase)`t$($graphic.Palette)`t$animation")
}

$blackTowerPatrolRows = @(
    "# var03`tdirection:counter,...",
    "0`t2:64,1:96,3:96,0:64",
    "1`t2:64,1:128,0:32,2:32,3:128,0:64",
    "2`t1:160,3:160",
    "3`t2:64,1:160,3:160,0:64",
    "4`t1:96,3:96"
)
$blackTowerConstantsRows = @(
    "# key`tvalue",
    "speed-80`t$([Convert]::ToInt32($room148SpeedMatch.Groups['value'].Value, 16))",
    "speed-100`t40",
    "patrol-wait`t20",
    "talk-wait`t30",
    "blocker-distance`t16",
    "blocker-wait`t10",
    "entrance-y-min`t120",
    "entrance-radius`t8"
)

# Past room 1:49's three placed characters are one shared interaction: the
# father and son play catch through wTmpcfc0.genericCutscene.cfd3 and
# INTERAC_BALL, while D7's essence bit and D8/Veran's completion room flag
# select the temporary stone tableau. Export every animation and dialogue
# selected by those handlers instead of leaving the two manual A-button
# branches with the generic TX_0000 fallback.
$room149ObjectSource = $mainObjectLines -join "`n"
$room149BoySource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\boy.s')
$room149FatherSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\villager.s')
$room149ObserverSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\pastGuy.s')
$room149BallSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\ball.s')
$room149ScriptSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$room149ScriptHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
if ($room149ObjectSource -notmatch '(?ms)^group1Map49ObjectData:\s+obj_Interaction \$3c \$0e \$48 \$78\s+obj_Interaction \$3a \$0c \$48 \$38\s+obj_Interaction \$43 \$06 \$28 \$78\s+obj_End' -or
    $room149BoySource -notmatch '(?ms)^@initSubid0e:.*?wGroup4RoomFlags\+\$fc.*?bit 7.*?<TX_251e.*?wEssencesObtained.*?bit 6.*?<TX_251d.*?ld bc,\$4848.*?<TX_251b.*?ld bc,\$4a75' -or
    $room149FatherSource -notmatch '(?ms)^@initSubid0c:.*?wGroup4RoomFlags\+\$fc.*?bit 7.*?wEssencesObtained.*?bit 6.*?Interaction\.var03.*?\$0d.*?^@runSubid0c:.*?TX_1442.*?TX_1443' -or
    $room149ObserverSource -notmatch '(?ms)^@subid6:.*?wGroup4RoomFlags\+\$fc.*?bit 7.*?wEssencesObtained.*?bit 6.*?Interaction\.var03.*?pastGuySubid6Script' -or
    $room149BallSource -notmatch '(?ms)^interactionCode95:.*?SPEED_200.*?ANGLE_RIGHT.*?ANGLE_LEFT.*?ld bc,-\$1c0.*?objectUpdateSpeedZ_paramC.*?ld bc,\$4a3c.*?ld c,\$75' -or
    $room149ScriptSource -notmatch '(?ms)^villagerSubid0cScript:.*?wait 60.*?setanimation \$01.*?wait 30.*?loadNextAnimationFrameAndMore, \$01.*?wait 30.*?^boySubid0eScript:.*?initcollisions.*?boySubid0cScript@playCatch' -or
    $room149ScriptSource -notmatch '(?ms)^boySubid0cScript:.*?@playCatch:.*?wait 30.*?loadNextAnimationFrameAndMore, \$02.*?wait 90' -or
    $room149ScriptHelperSource -notmatch '(?ms)^loadNextAnimationFrameAndMore:.*?Interaction\.animCounter.*?Interaction\.var38.*?genericCutscene\.cfd3.*?interactionAnimate') {
    throw 'Room 1:49 family, stone-state, catch timing, or ball behavior changed in the disassembly.'
}

$room149VisualRows = [Collections.Generic.List[string]]::new()
$room149VisualRows.Add("# key`tsprite`ttile-base`tpalette`tanimation")
$room149VisualSpecs = @(
    @{ Key = 'father-default'; Id = 0x3a; Subid = 0x0c; Animation = 0x02 },
    @{ Key = 'father-throw';   Id = 0x3a; Subid = 0x0c; Animation = 0x01 },
    @{ Key = 'father-stone';   Id = 0x3a; Subid = 0x0c; Animation = 0x0d },
    @{ Key = 'boy';            Id = 0x3c; Subid = 0x0e; Animation = 0x03 },
    @{ Key = 'observer';       Id = 0x43; Subid = 0x06; Animation = 0x04 },
    @{ Key = 'ball';           Id = 0x95; Subid = 0x00; Animation = 0x00 }
)
foreach ($spec in $room149VisualSpecs) {
    $graphic = $interactionGraphics["$([int]$spec.Id)`:$([int]$spec.Subid)"]
    if ($null -eq $graphic) {
        $graphic = $interactionGraphics["$([int]$spec.Id)`:0"]
    }
    $animation = Resolve-NpcAnimation ([int]$spec.Id) ([int]$spec.Animation)
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx) -or -not $animation) {
        throw "Could not resolve room 1:49 visual '$($spec.Key)'."
    }
    $spriteName = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($spriteName)
    $room149VisualRows.Add(
        "$($spec.Key)`t$spriteName`t$($graphic.TileBase)`t$($graphic.Palette)`t$animation")
}
if ($room149VisualRows.Count -ne 7) {
    throw "Expected six room 1:49 visual records, got $($room149VisualRows.Count - 1)."
}

$room149TextRows = [Collections.Generic.List[string]]::new()
$room149TextRows.Add("# text-id`tutf8-base64")
foreach ($textId in @(0x1442, 0x1443, 0x1712, 0x251b, 0x251d, 0x251e)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve room 1:49 text TX_$($textId.ToString('x4'))."
    }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $room149TextRows.Add("$($textId.ToString('x4'))`t$encoded")
}

# Rooms 2:ea and 2:eb place only INTERAC_BIPIN_BLOSSOM_FAMILY_SPAWNER
# ($ac). The controller creates Bipin, Blossom, and their child from the shared
# stage/personality table below. Import all of its results so runtime can select
# the original family without hard-coding either room's occupants.
$familySpawnerSourcePath = Join-Path $Disassembly `
    'object_code\common\interactions\bipinBlossomFamilySpawner.s'
$familySpawnerSource = Get-Content -Raw $familySpawnerSourcePath
$mainObjectSource = $mainObjectLines -join "`n"
$interactionConstantSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\interactions.s')
$familyInteractionIds = @{
    INTERAC_BIPIN = 0x28
    INTERAC_BLOSSOM = 0x2b
    INTERAC_CHILD = 0x35
}
foreach ($constant in $familyInteractionIds.GetEnumerator()) {
    $expected = ([int]$constant.Value).ToString('x2')
    $constantPattern = '(?m)^\.define\s+{0}\s+\${1}\s*$' -f `
        [regex]::Escape($constant.Key), $expected
    if ($interactionConstantSource -notmatch $constantPattern) {
        throw "$($constant.Key) no longer resolves to interaction `$$expected."
    }
}
if ($mainObjectSource -notmatch
        '(?ms)^group2MapeaObjectData:.*?obj_Interaction\s+\$ac\s+\$00\s+\$58\s+\$38' -or
    $mainObjectSource -notmatch
        '(?ms)^group2MapebObjectData:.*?obj_Interaction\s+\$ac\s+\$01\s+\$58\s+\$38') {
    throw 'Rooms 2:ea/2:eb no longer place the left/right family spawner $ac.'
}

$familyBlocks = @{}
$familyBlockLabels = [Collections.Generic.List[string]]::new()
$familyBlockRecords = [Collections.Generic.List[object]]::new()
foreach ($line in ($familySpawnerSource -split '\r?\n')) {
    if ($line -match '^@(?<label>(?:left|right)Stage[0-9](?:_[a-z]+)?):') {
        if ($familyBlockRecords.Count -gt 0) {
            # A label can point at the terminating byte of the preceding
            # record list (for example rightStage0 is leftStage0's `$00).
            foreach ($label in $familyBlockLabels) {
                $familyBlocks[$label] = @($familyBlockRecords)
            }
            $familyBlockLabels.Clear()
            $familyBlockRecords.Clear()
        }
        $familyBlockLabels.Add($Matches['label'])
        continue
    }
    if ($familyBlockLabels.Count -eq 0) { continue }
    if ($line -match '^\s*\.db\s+\$00') {
        foreach ($label in $familyBlockLabels) {
            $familyBlocks[$label] = @($familyBlockRecords)
        }
        $familyBlockLabels.Clear()
        $familyBlockRecords.Clear()
        continue
    }
    if ($line -notmatch
        '^\s*\.db\s+(?<id>INTERAC_[A-Z_]+)\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<var03>[0-9a-f]{2})\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})') {
        continue
    }
    $idName = $Matches['id']
    if (-not $familyInteractionIds.ContainsKey($idName)) {
        throw "Family spawn table references unsupported $idName."
    }
    $familyBlockRecords.Add(@{
        Id = [int]$familyInteractionIds[$idName]
        Subid = [Convert]::ToInt32($Matches['subid'], 16)
        Var03 = [Convert]::ToInt32($Matches['var03'], 16)
        Y = [Convert]::ToInt32($Matches['y'], 16)
        X = [Convert]::ToInt32($Matches['x'], 16)
    })
}
if ($familyBlockLabels.Count -ne 0 -or $familyBlockRecords.Count -ne 0) {
    throw 'The final family spawn block was not terminated by $00.'
}

$bipinTextIds = @(
    0x4300, 0x4302, 0x4303, 0x4303, 0x4304,
    0x4305, 0x4306, 0x4307, 0x4308, 0x4308
)
$blossomTextIds = @(
    @(0x4400), @(0x440b), @(0x4412), @(0x4413), @(0x4417), @(0x4418),
    @(0x4419, 0x441a, 0x441b),
    @(0x4425, 0x4426, 0x4427, 0x4428),
    @(0x4429, 0x442a, 0x442b, 0x442c),
    @(0x442d, 0x442e, 0x442f, 0x4430)
)
$childTextIds = @(
    0x0000,
    0x4700, 0x4200, 0x4900,
    0x4701, 0x4201, 0x4901,
    0x4702, 0x4202, 0x4902,
    0x4b00, 0x4a00, 0x4800, 0x4600,
    0x4b01, 0x4a01, 0x4801, 0x4601,
    0x4b0a, 0x4a06, 0x4804, 0x4603
)
$familyInteractionTextIds = @(
    0x4301,
    0x4407, 0x4408, 0x4409, 0x440a
)
$familyScriptSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$familyScriptHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
foreach ($textId in @($bipinTextIds + $childTextIds + $familyInteractionTextIds +
        ($blossomTextIds | ForEach-Object { $_ }))) {
    if ($textId -eq 0) { continue }
    $symbol = "TX_$($textId.ToString('x4'))"
    if (-not $allTexts.ContainsKey($textId) -or
        ($familyScriptSource -notmatch "\b$symbol\b" -and
         $familyScriptHelperSource -notmatch "\b$symbol\b")) {
        throw "Could not verify family dialogue $symbol in the original actor scripts."
    }
}

$familyRows = [Collections.Generic.List[string]]::new()
$familyRows.Add("# group`troom`tstage`tpersonality`tid`tsubid`ty`tx`tvar03`ttext-id`tsprite`ttile-base`tpalette`tdefault-animation`tcan-face`tup-animation`tright-animation`tdown-animation`tleft-animation`tutf8-base64")
$familyPersonalities = @{
    hyperactive = 0; shy = 1; curious = 2
    slacker = 0; warrior = 1; arborist = 2; singer = 3
}
foreach ($entry in ($familyBlocks.GetEnumerator() | Sort-Object Name)) {
    if ($entry.Key -notmatch
        '^(?<house>left|right)Stage(?<stage>[0-9])(?:_(?<personality>[a-z]+))?$') {
        throw "Malformed family spawn label $($entry.Key)."
    }
    $room = if ($Matches['house'] -eq 'left') { 0xea } else { 0xeb }
    $stage = [int]$Matches['stage']
    $personality = if ($Matches['personality']) {
        [int]$familyPersonalities[$Matches['personality']]
    } else {
        -1
    }
    foreach ($actor in $entry.Value) {
        $id = [int]$actor.Id
        $subid = [int]$actor.Subid
        $var03 = [int]$actor.Var03
        if ($id -eq 0x28) {
            $textId = $bipinTextIds[$subid]
            $initialAnimation = if ($subid -eq 0) { 4 } elseif ($subid -eq 5) { 2 } else { 3 }
        } elseif ($id -eq 0x2b) {
            $textOptions = $blossomTextIds[$subid]
            $textIndex = if ($subid -ge 6) { $var03 } else { 0 }
            if ($textIndex -ge $textOptions.Count) {
                throw "Blossom `$2b:`$$($subid.ToString('x2')) has invalid var03 `$$($var03.ToString('x2'))."
            }
            $textId = $textOptions[$textIndex]
            $initialAnimation = if ($subid -in @(0, 1, 3)) { 0 } else { 4 }
        } else {
            if ($var03 -ge $childTextIds.Count) {
                throw "Child `$35 var03 `$$($var03.ToString('x2')) has no script text mapping."
            }
            $textId = $childTextIds[$var03]
            $childAnimationBases = @(0, 2, 5, 8, 11, 17, 21, 23)
            $initialAnimation = $childAnimationBases[$subid]
            if ($subid -eq 5) { $initialAnimation += 3 }
        }
        $npcRow = New-NpcDataRow 2 $room $id $subid `
            ([int]$actor.Y) ([int]$actor.X) $var03 $textId $initialAnimation 0
        if (-not $npcRow) {
            throw "Could not resolve family actor `$$($id.ToString('x2')):`$$($subid.ToString('x2'))."
        }
        $npcColumns = $npcRow -split "`t"
        if ($id -eq 0x28 -and $subid -eq 0) {
            # @updateSpeed flips var3a between animations $04/$05 whenever
            # running Bipin crosses X $28/$58. Preserve the second sequence
            # in the otherwise-unused right-facing record field.
            $alternateAnimation = Resolve-NpcAnimation 0x28 5
            if (-not $alternateAnimation) {
                throw 'Could not resolve running Bipin animation $05.'
            }
            $npcColumns[14] = $alternateAnimation
        }
        $familyRows.Add(
            "$($npcColumns[0])`t$($npcColumns[1])`t$stage`t$personality`t$($npcColumns[2..($npcColumns.Count - 1)] -join "`t")")
    }
}
if ($familyRows.Count -ne 73) {
    throw "Expected 72 state-selected Bipin/Blossom family actors, parsed $($familyRows.Count - 1)."
}
$familyTextRows = [Collections.Generic.List[string]]::new()
$familyTextRows.Add("# text-id`tutf8-base64")
foreach ($textId in $familyInteractionTextIds) {
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $familyTextRows.Add("$($textId.ToString('x4'))`t$encoded")
}

# INTERAC_IMPA_NPC $4f:$00 is an unpositioned room object in Nayru's house.
# getImpaNpcState selects its exact position, text, and var03 behavior from the
# shared story state. Export every visible result; visibility predicates below
# keep exactly one variant alive and naturally swap it when the save changes.
$impaHouseBlock = [regex]::Match(
    $mainObjectSource,
    '(?ms)^group3Map9eObjectData:.*?(?=^group[0-7]Map[0-9a-f]{2}ObjectData:|\z)')
if (-not $impaHouseBlock.Success -or
    $impaHouseBlock.Value -notmatch '(?m)^\s*obj_Interaction\s+\$4f\s+\$00\s*$') {
    throw 'Nayru''s house no longer contains unpositioned INTERAC_IMPA_NPC $4f:$00.'
}
$impaGraphic = $interactionGraphics['79:0']
if ($null -eq $impaGraphic -or -not $gfxNames.ContainsKey($impaGraphic.Gfx) -or
    $impaGraphic.DefaultAnimation -ne 2) {
    throw 'Could not resolve Impa NPC graphics and original down-facing animation $02.'
}
$impaSpriteName = $gfxNames[$impaGraphic.Gfx]
[void]$npcSpriteNames.Add($impaSpriteName)
$impaUpOam = Resolve-NpcAnimation 0x4f 0
$impaRightOam = Resolve-NpcAnimation 0x4f 1
$impaDownOam = Resolve-NpcAnimation 0x4f 2
$impaLeftOam = Resolve-NpcAnimation 0x4f 3
if (-not $impaUpOam -or -not $impaRightOam -or -not $impaDownOam -or -not $impaLeftOam) {
    throw 'Could not resolve Impa NPC''s four original facing animations.'
}
$impaHouseVariants = @(
    # var03, y, x, text, faces Link while idle
    @(0x00, 0x38, 0x38, 0x0120, $true),
    @(0x01, 0x48, 0x28, 0x0121, $false),
    @(0x02, 0x28, 0x68, 0x0122, $true),
    @(0x05, 0x28, 0x68, 0x0123, $true),
    @(0x09, 0x38, 0x38, 0x0120, $true),
    @(0x0a, 0x48, 0x28, 0x0121, $false),
    @(0x0b, 0x28, 0x68, 0x0122, $true),
    @(0x0d, 0x28, 0x68, 0x012c, $true),
    @(0x0e, 0x28, 0x68, 0x0123, $true)
)
foreach ($variant in $impaHouseVariants) {
    $textId = [int]$variant[3]
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Impa house text TX_$($textId.ToString('x4'))."
    }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $npcRows.Add(
        "3`t9e`t4f`t00`t$(([int]$variant[1]).ToString('x2'))`t$(([int]$variant[2]).ToString('x2'))`t$(([int]$variant[0]).ToString('x2'))`t$($textId.ToString('x4'))`t$impaSpriteName`t$($impaGraphic.TileBase)`t$($impaGraphic.Palette)`t2`t$([int][bool]$variant[4])`t$impaUpOam`t$impaRightOam`t$impaDownOam`t$impaLeftOam`t$encoded")
}
if ($npcRows.Count -ne 389) {
    throw "Expected 379 positioned and 9 state-derived NPC records, got $($npcRows.Count - 1)."
}

# Ordinary NPC scripts can replace their dialogue without replacing the room
# object. Export the complete getGameProgress_1-indexed tables used by Lynna's
# present-day villagers so runtime save changes select the original text.
$npcDialogueRows = [Collections.Generic.List[string]]::new()
$npcDialogueRows.Add(
    "# id`tsubid`tvar03`tkind`tvalue`tlinked`ttext-id`tsource`tutf8-base64")

function Get-NpcDialogueTableEntries(
    [string]$sourceFile, [string]$tableLabel,
    [string]$progressRoutine, [int]$expectedCount
) {
    $sourcePath = Join-Path $Disassembly "object_code\ages\interactions\$sourceFile"
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "NPC dialogue source not found: $sourceFile"
    }
    $source = Get-Content -Raw $sourcePath
    if ($source -notmatch [regex]::Escape($progressRoutine)) {
        throw "$sourceFile no longer selects $tableLabel with $progressRoutine."
    }
    $tableMatch = [regex]::Match(
        $source,
        "(?ms)^$([regex]::Escape($tableLabel)):\r?\n(?<body>.*?)(?=^[A-Za-z0-9_@]+:|\z)")
    if (-not $tableMatch.Success) {
        throw "Could not resolve NPC dialogue table $sourceFile`:$tableLabel."
    }
    $entries = @([regex]::Matches(
        $tableMatch.Groups['body'].Value,
        '(?m)^\s*\.dw\s+mainScripts\.(?<label>[A-Za-z0-9_@]+)'))
    if ($entries.Count -ne $expectedCount) {
        throw "$sourceFile`:$tableLabel no longer matches its $expectedCount $progressRoutine states."
    }
    return $entries
}

function Add-NpcGameProgress1DialogueTable(
    [int]$id, [int[]]$subids, [int]$var03,
    [string]$sourceFile, [string]$tableLabel,
    [int]$entryOffset = 0, [bool]$subidPerState = $false
) {
    $entries = @(Get-NpcDialogueTableEntries `
        $sourceFile $tableLabel 'getGameProgress_1' (6 + $entryOffset))
    if ($subidPerState -and $subids.Count -ne 6) {
        throw "$sourceFile`:$tableLabel no longer matches its six getGameProgress_1 states."
    }

    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    for ($state = 0; $state -lt 6; $state++) {
        $scriptLabel = $entries[$state + $entryOffset].Groups['label'].Value
        $textId = Resolve-ScriptTextId `
            $scriptLabel ([Collections.Generic.HashSet[string]]::new())
        if ($textId -le 0 -or -not $allTexts.ContainsKey($textId)) {
            throw "Could not resolve $sourceFile`:$tableLabel state $state dialogue."
        }
        $encoded = [Convert]::ToBase64String(
            [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
        $stateSubids = if ($subidPerState) { @($subids[$state]) } else { $subids }
        foreach ($subid in $stateSubids) {
            $npcDialogueRows.Add(
                "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`tgame-progress-1`t$($state.ToString('x2'))`t*`t$($textId.ToString('x4'))`t$sourceFile`:$tableLabel`t$encoded")
        }
    }
}

function Add-NpcGameProgress2DialogueTable(
    [int]$id, [int[]]$subids, [int]$var03,
    [string]$sourceFile, [string]$tableLabel
) {
    $entries = @(Get-NpcDialogueTableEntries `
        $sourceFile $tableLabel 'getGameProgress_2' 8)

    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    for ($state = 0; $state -lt 8; $state++) {
        $scriptLabel = $entries[$state].Groups['label'].Value
        $textId = Resolve-ScriptTextId `
            $scriptLabel ([Collections.Generic.HashSet[string]]::new())
        if ($textId -le 0 -or -not $allTexts.ContainsKey($textId)) {
            throw "Could not resolve $sourceFile`:$tableLabel state $state dialogue."
        }

        $selectors = @(@{ Linked = '*'; TextId = $textId })
        $scriptMatch = [regex]::Match(
            $agesMainScriptSource,
            "(?ms)^$([regex]::Escape($scriptLabel)):\r?\n(?<body>.*?)(?=^(?!@)[A-Za-z0-9_]+:|\z)")
        if (-not $scriptMatch.Success) {
            throw "Could not resolve script body mainScripts.$scriptLabel."
        }
        $linkedMatch = [regex]::Match(
            $scriptMatch.Groups['body'].Value,
            '(?ms)jumpifmemoryeq\s+wIsLinkedGame,\s*\$01,\s*(?:@linked|\+).*?(?:rungenericnpc|rungenericnpclowindex)\s+(?:<)?TX_(?<unlinked>[0-9a-f]{4}).*?^(?:@linked:|\+)\s*(?:rungenericnpc|rungenericnpclowindex)\s+(?:<)?TX_(?<linked>[0-9a-f]{4})')
        if ($linkedMatch.Success) {
            $unlinkedText = [Convert]::ToInt32(
                $linkedMatch.Groups['unlinked'].Value, 16)
            $linkedText = [Convert]::ToInt32(
                $linkedMatch.Groups['linked'].Value, 16)
            if ($unlinkedText -ne $textId -or
                -not $allTexts.ContainsKey($linkedText)) {
                throw "Could not verify linked dialogue in mainScripts.$scriptLabel."
            }
            $selectors = @(
                @{ Linked = '0'; TextId = $unlinkedText },
                @{ Linked = '1'; TextId = $linkedText }
            )
        }

        foreach ($selector in $selectors) {
            $selectedTextId = [int]$selector.TextId
            $encoded = [Convert]::ToBase64String(
                [Text.Encoding]::UTF8.GetBytes($allTexts[$selectedTextId]))
            foreach ($subid in $subids) {
                $npcDialogueRows.Add(
                    "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`tgame-progress-2`t$($state.ToString('x2'))`t$($selector.Linked)`t$($selectedTextId.ToString('x4'))`t$sourceFile`:$tableLabel`t$encoded")
            }
        }
    }
}

Add-NpcGameProgress1DialogueTable 0x3a @(0x03) -1 'villager.s' '@subid03ScriptTable'
Add-NpcGameProgress1DialogueTable 0x3a @(0x04, 0x05) -1 'villager.s' '@subid4And5ScriptTable'
Add-NpcGameProgress1DialogueTable 0x3b @(0x01, 0x02) -1 'femaleVillager.s' '@subid1And2ScriptTable'
Add-NpcGameProgress1DialogueTable 0x3c @(0x02) -1 'boy.s' 'boySubid02ScriptTable'
Add-NpcGameProgress1DialogueTable 0x44 @(0x02, 0x03) -1 'miscMan2.s' 'lynnaMan2ScriptTable'
Add-NpcGameProgress1DialogueTable 0x41 @(0x01, 0x02, 0x03, 0x04, 0x05, 0x06) -1 'miscMan.s' '@scriptTable' 1 $true
Add-NpcGameProgress2DialogueTable 0x3a @(0x06, 0x07) -1 'villager.s' '@subid6And7ScriptTable'
Add-NpcGameProgress2DialogueTable 0x38 @(0x00) -1 'pastGirl.s' '@scriptTable'
Add-NpcGameProgress2DialogueTable 0x3b @(0x05) -1 'femaleVillager.s' '@subid5ScriptTable'
Add-NpcGameProgress2DialogueTable 0x44 @(0x04) -1 'miscMan2.s' 'pastHoboScriptTable'

# hardhatWorkerSubid02Script checks room flag $80 before its A-button loop.
# The initial TX_1003 remains in the base NPC row; only the completed phase
# needs a state-selected replacement.
$hardhatCompletedText = 0x1004
if (-not $allTexts.ContainsKey($hardhatCompletedText)) {
    throw 'Could not resolve room 1:86 completed hardhat text TX_1004.'
}
$hardhatCompletedEncoded = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($allTexts[$hardhatCompletedText]))
$npcDialogueRows.Add(
    "58`t02`t*`tcurrent-room-flag`t80`t*`t1004`thardhatWorkerSubid02Script:@alreadySawCutscene`t$hardhatCompletedEncoded")

if ($npcDialogueRows.Count -ne 101) {
    throw "Expected 100 imported NPC dialogue predicates, got $($npcDialogueRows.Count - 1)."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\npc_dialogue.tsv'),
    $npcDialogueRows,
    [Text.UTF8Encoding]::new($false))

# State-selected position overrides remain separate from visibility and text.
# INTERAC_MISC_MAN_2 $44:$04 moves only in getGameProgress_2 state $06;
# every other living state uses its object-data position $48,$48.
$npcPositionRows = @(
    "# id`tsubid`tvar03`tkind`tvalue`ty`tx`tsource",
    "44`t04`t*`tgame-progress-2`t06`t58`t78`tmiscMan2.s:@subid4",
    "58`t02`t*`tcurrent-room-flag`t80`t38`t58`thardhatWorker.s:@@state0"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\npc_positions.tsv'),
    $npcPositionRows,
    [Text.UTF8Encoding]::new($false))

# INTERAC_MISCELLANEOUS_2 $dc:$07 is a general static Heart Piece spawner.
# Its state-0 handler deletes itself when ROOMFLAG_ITEM is set; otherwise it
# creates TREASURE_OBJECT_HEART_PIECE_00 at the spawner's exact position.
$miscellaneous2Source = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$treasureSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\treasure.s')
$treasureObjectSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\treasureObjectData.s')
if ($miscellaneous2Source -notmatch
        '(?ms)^interactiondc_subid08:\s+call checkInteractionState\s+jr z,@state0.*?^@state1:.*?Interaction\.yh.*?>wRoomLayout.*?Interaction\.var03.*?cp l\s+ret z.*?call getThisRoomFlags.*?Interaction\.xh.*?or \(hl\).*?ld \(hl\),a.*?^@state0:.*?call getThisRoomFlags.*?Interaction\.xh.*?and \(hl\).*?jp nz,interactionDelete.*?Interaction\.yh.*?>wRoomLayout.*?Interaction\.var03.*?ld \(de\),a\s+jp interactionIncState') {
    throw 'INTERAC_MISCELLANEOUS_2 $dc:$08 tile-change watcher behavior changed.'
}

# INTERAC_MISCELLANEOUS_2 $dc:$08 treats its nominal Y/X bytes as a packed
# wRoomLayout position and a room-flag mask. It snapshots that tile in state 0,
# then ORs the mask into the room flags after the tile changes. Every placement
# must join the matching applySingleTileChanges row that persists the result.
$tileChangeWatcherRows = [Collections.Generic.List[string]]::new()
$tileChangeWatcherRows.Add(
    "# group`troom`torder`tposition`troom-flag`tsource")
$currentGroup = -1
$currentRoom = -1
$objectOrder = 0
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $currentGroup = [Convert]::ToInt32($Matches['group'], 10)
        $currentRoom = [Convert]::ToInt32($Matches['room'], 16)
        $objectOrder = 0
        continue
    }
    if ($currentGroup -lt 0 -or $line -notmatch '^\s+obj_(?!End)') { continue }
    if ($line -match
        'obj_Interaction\s+\$dc\s+\$08\s+\$(?<position>[0-9a-f]{2})\s+\$(?<mask>[0-9a-f]{2})') {
        $position = [Convert]::ToInt32($Matches['position'], 16)
        $mask = [Convert]::ToInt32($Matches['mask'], 16)
        $persistentRows = @($singleTileChangeRecords | Where-Object {
            $_.Group -eq $currentGroup -and
            $_.Room -eq $currentRoom -and
            $_.Mask -eq $mask -and
            $_.Position -eq $position
        })
        if ($persistentRows.Count -ne 1) {
            throw "Tile-change watcher in room $currentGroup`:$($currentRoom.ToString('x2')) " +
                "at `$$($position.ToString('x2')) / flag `$$($mask.ToString('x2')) " +
                "matched $($persistentRows.Count) singleTileChanges rows."
        }
        $tileChangeWatcherRows.Add(
            "$currentGroup`t$($currentRoom.ToString('x2'))`t$objectOrder`t$($position.ToString('x2'))`t$($mask.ToString('x2'))`tmiscellaneous2.s:interactiondc_subid08")
    }
    $objectOrder++
}
if ($tileChangeWatcherRows.Count -ne 9) {
    throw "Expected eight tile-change watchers, got $($tileChangeWatcherRows.Count - 1)."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\tile_change_watchers.tsv'),
    $tileChangeWatcherRows,
    [Text.UTF8Encoding]::new($false))

if ($miscellaneous2Source -notmatch '(?ms)^interactiondc_subid07:\s+call getThisRoomFlags\s+and ROOMFLAG_ITEM\s+jp nz,interactionDelete\s+ld bc,TREASURE_OBJECT_HEART_PIECE_00\s+call createTreasure\s+call objectCopyPosition\s+jp interactionDelete' -or
    $treasureObjectSource -notmatch '(?m)^\s*m_TreasureSubid \$0a, \$01, \$17, \$3a, TREASURE_OBJECT_HEART_PIECE_00\s*$' -or
    $treasureSource -notmatch '(?ms)^@spawnMode0:.*?@checkLinkTouched.*?^@grabMode2:\s+ldbc \$81,\$00') {
    throw 'Static Heart Piece spawner or TREASURE_OBJECT_HEART_PIECE_00 behavior changed.'
}
$heartPieceGraphic = $interactionGraphics['96:58']
$heartPieceAnimation = Resolve-NpcAnimation 0x60 0x02
$heartContainerFollowupText = 0x0049
if ($null -eq $heartPieceGraphic -or $heartPieceGraphic.Gfx -ne 0x79 -or
    $heartPieceGraphic.TileBase -ne 0x10 -or
    $heartPieceGraphic.Palette -ne 0x02 -or
    $heartPieceGraphic.DefaultAnimation -ne 0x02 -or
    -not $heartPieceAnimation -or
    -not $allTexts.ContainsKey($heartContainerFollowupText) -or
    -not $gfxNames.ContainsKey($heartPieceGraphic.Gfx)) {
    throw 'Could not resolve static Heart Piece interaction $60 graphic $3a.'
}
$heartPieceSprite = $gfxNames[$heartPieceGraphic.Gfx]
[void]$npcSpriteNames.Add($heartPieceSprite)
$groundTreasureRows = [Collections.Generic.List[string]]::new()
$groundTreasureRows.Add(
    "# group`troom`torder`ty`tx`ttreasure-object`tsprite`ttile-base`tpalette`tanimation`tcompletion-text-id`tcompletion-text-base64`tsource")
$currentGroup = -1
$currentRoom = -1
$objectOrder = 0
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $currentGroup = [Convert]::ToInt32($Matches['group'], 10)
        $currentRoom = [Convert]::ToInt32($Matches['room'], 16)
        $objectOrder = 0
        continue
    }
    if ($currentGroup -lt 0 -or $line -notmatch '^\s+obj_(?!End)') { continue }
    if ($line -match 'obj_Interaction\s+\$dc\s+\$07\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})') {
        $groundTreasureRows.Add(
            "$currentGroup`t$($currentRoom.ToString('x2'))`t$objectOrder`t$($Matches['y'])`t$($Matches['x'])`tTREASURE_OBJECT_HEART_PIECE_00`t$heartPieceSprite`t$($heartPieceGraphic.TileBase)`t$($heartPieceGraphic.Palette)`t$heartPieceAnimation`t$($heartContainerFollowupText.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$heartContainerFollowupText])))`tmiscellaneous2.s:interactiondc_subid07")
    }
    $objectOrder++
}
if ($groundTreasureRows.Count -ne 9) {
    throw "Expected eight static Heart Piece spawners, got $($groundTreasureRows.Count - 1)."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\ground_treasures.tsv'),
    $groundTreasureRows,
    [Text.UTF8Encoding]::new($false))

# PART_DARK_ROOM_HANDLER $08 scans the complete 16-byte-stride large-room
# layout and creates a permanent PART_LIGHTABLE_TORCH $06 for every unlit
# torch metatile. INTERAC_MISCELLANEOUS_2 $dc:$00 in room 5:ed precedes that
# handler and creates the falling Graveyard Key when exactly two torches are
# lit, unless ROOMFLAG_ITEM already records its collection. Export these
# placements together so the runtime can retain their source order.
$darkRoomHandlerSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\parts\darkRoomHandler.s')
$lightableTorchSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\parts\lightableTorch.s')
$group5DungeonProperties = [IO.File]::ReadAllBytes(
    (Join-Path $Disassembly 'rooms\ages\group5DungeonProperties.bin'))
if ($miscellaneous2Source -notmatch '(?ms)^interactiondc_subid00:\s+call getThisRoomFlags\s+and ROOMFLAG_ITEM\s+jp nz,interactionDelete\s+ld a,\(wNumTorchesLit\)\s+cp \$02\s+ret nz\s+ld bc,TREASURE_OBJECT_GRAVEYARD_KEY_00\s+call createTreasure\s+call objectCopyPosition\s+jp interactionDelete' -or
    $darkRoomHandlerSource -notmatch '(?ms)^partCode08:.*?wPaletteThread_mode.*?wScrollMode.*?^@state1:.*?wNumTorchesLit.*?jp z,darkenRoom.*?jp z,brightenRoom.*?cp \$f7.*?jp nc,brightenRoomLightly.*?jp darkenRoomLightly.*?^@state0:.*?ld hl,wRoomLayout\s+ld b,LARGE_ROOM_HEIGHT << 4.*?TILEINDEX_UNLIT_TORCH.*?@spawnLightableTorch' -or
    $lightableTorchSource -notmatch '(?ms)^@subid0:.*?^@subid0State2:\s+ld hl,wNumTorchesLit\s+inc \(hl\)\s+ld a,SND_LIGHTTORCH\s+call playSound.*?ld a,TILEINDEX_LIT_TORCH.*?call setTile\s+jp partDelete' -or
    $partDataSource -notmatch '(?m)^\s*\.db \$00 \$82 \$44 \$ff \$40 \$00 \$00 \$00 ; \$06' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_UNLIT_TORCH\s+\$08' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_LIT_TORCH\s+\$09' -or
    $musicIdSource -notmatch '(?m)^\s*SND_LIGHTTORCH\s+db\s+; \$72' -or
    $musicIdSource -notmatch '(?m)^\s*SND_DROPESSENCE\s+db\s+; \$77' -or
    $treasureObjectSource -notmatch '(?m)^\s*/\* \$42 \*/ m_TreasureSubid\s+\$29, \$00, \$23, \$44, TREASURE_OBJECT_GRAVEYARD_KEY_00\s*$' -or
    $treasureSource -notmatch '(?ms)^@spawnMode2:.*?ld \(hl\),40.*?SND_SOLVEPUZZLE.*?objectGetZAboveScreen.*?ld c,\$10.*?SND_DROPESSENCE.*?ld bc,-\$aa' -or
    $group5DungeonProperties.Length -ne 256 -or
    ($group5DungeonProperties[0xa8] -band 0x80) -eq 0 -or
    ($group5DungeonProperties[0xed] -band 0x80) -eq 0) {
    throw 'Dark-room handler, permanent torch, Graveyard Key, dungeon-property, tile, motion, or sound contract changed.'
}

$darkRoomRows = [Collections.Generic.List[string]]::new()
$darkRoomRows.Add(
    "# group`troom`torder`tkind`tid`tsubid`ty`tx`tparameter`trequired-count`ttreasure-object`tsource")
$darkGroup = -1
$darkRoom = -1
$darkOrder = 0
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $darkGroup = [Convert]::ToInt32($Matches['group'], 10)
        $darkRoom = [Convert]::ToInt32($Matches['room'], 16)
        $darkOrder = 0
        continue
    }
    if ($darkGroup -lt 0 -or $line -notmatch '^\s+obj_(?!End)') { continue }
    if ($line -match '^\s*obj_Part\s+\$08\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})\s*$') {
        $darkRoomRows.Add(
            "$darkGroup`t$($darkRoom.ToString('x2'))`t$darkOrder`thandler`t08`t$($Matches['subid'])`t-`t-`t$($Matches['parameter'])`t0`t-`tdarkRoomHandler.s:partCode08")
    } elseif ($line -match '^\s*obj_Interaction\s+\$dc\s+\$00\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})\s*$') {
        $darkRoomRows.Add(
            "$darkGroup`t$($darkRoom.ToString('x2'))`t$darkOrder`treward`tdc`t00`t$($Matches['y'])`t$($Matches['x'])`t00`t2`tTREASURE_OBJECT_GRAVEYARD_KEY_00`tmiscellaneous2.s:interactiondc_subid00")
    }
    $darkOrder++
}
if ($darkRoomRows.Count -ne 4 -or
    -not ($darkRoomRows -contains "5`ta8`t0`thandler`t08`t00`t-`t-`t00`t0`t-`tdarkRoomHandler.s:partCode08") -or
    -not ($darkRoomRows -contains "5`ted`t0`treward`tdc`t00`t48`t78`t00`t2`tTREASURE_OBJECT_GRAVEYARD_KEY_00`tmiscellaneous2.s:interactiondc_subid00") -or
    -not ($darkRoomRows -contains "5`ted`t1`thandler`t08`t00`t-`t-`t50`t0`t-`tdarkRoomHandler.s:partCode08")) {
    throw "Expected ordered dark-room placements in 5:a8 and 5:ed, parsed $($darkRoomRows.Count - 1)."
}
$darkRoomConstantRows = @(
    "# key`tvalue"
    "unlit-tile`t8"
    "lit-tile`t9"
    "torch-collision-mode`t130"
    "torch-radius-y`t4"
    "torch-radius-x`t4"
    "full-dark-parameter`t240"
    "partial-dark-parameter`t247"
    "fade-speed`t1"
    "light-sound`t114"
    "reward-spawn-mode`t2"
    "reward-grab-mode`t1"
    "spawn-delay`t40"
    "bounce-count`t2"
    "gravity`t16"
    "bounce-speed`t-170"
    "spawn-sound`t77"
    "landing-sound`t119"
    "above-screen-margin`t8"
    "above-screen-fallback`t-128"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\dark_room_interactions.tsv'),
    $darkRoomRows,
    [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\dark_room_constants.tsv'),
    $darkRoomConstantRows,
    [Text.UTF8Encoding]::new($false))

# Room interactions frequently delete their placed NPC during state 0 based
# on global flags or room flags. Export those predicates separately from the
# visual NPC record. Rules in one alternative are ANDed; alternatives are
# ORed, which preserves branches such as Mamamu's dog remaining indoors when
# any one of three original conditions is true.
$npcVisibilityRows = [Collections.Generic.List[string]]::new()
$npcVisibilityRows.Add(
    "# id`tsubid`tvar03`talternative`tkind`tgroup`troom`tvalue`texpected-set`tsource")
$npcVisibilitySources = @{}
function Confirm-NpcVisibilitySource([string]$source, [string]$token) {
    $file = $source.Split(':')[0]
    if (-not $npcVisibilitySources.ContainsKey($file)) {
        $path = @(
            (Join-Path $Disassembly "object_code\ages\interactions\$file"),
            (Join-Path $Disassembly "scripts\ages\$file"),
            (Join-Path $Disassembly "code\$file")
        ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $path) {
            throw "NPC visibility source not found: $file"
        }
        $npcVisibilitySources[$file] = Get-Content -Raw $path
    }
    if ($npcVisibilitySources[$file] -notmatch [regex]::Escape($token)) {
        throw "NPC visibility source $source no longer references $token."
    }
}
function Add-NpcGlobalVisibility(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [string]$flag, [bool]$expectedSet, [string]$source
) {
    if (-not $globalFlagValues.ContainsKey($flag)) {
        throw "NPC visibility rule references unknown $flag."
    }
    Confirm-NpcVisibilitySource $source $flag
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`tglobal`t-`t-`t$($globalFlagValues[$flag].ToString('x2'))`t$([int]$expectedSet)`t$source")
}
function Add-NpcCurrentRoomVisibility(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [int]$mask, [bool]$expectedSet, [string]$source
) {
    Confirm-NpcVisibilitySource $source 'getThisRoomFlags'
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`tcurrent-room`t-`t-`t$($mask.ToString('x2'))`t$([int]$expectedSet)`t$source")
}
function Add-NpcSpecificRoomVisibility(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [int]$group, [int]$room, [int]$mask, [bool]$expectedSet,
    [string]$source, [string]$addressToken
) {
    Confirm-NpcVisibilitySource $source $addressToken
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`tspecific-room`t$group`t$($room.ToString('x2'))`t$($mask.ToString('x2'))`t$([int]$expectedSet)`t$source")
}
function Add-NpcTreasureVisibility(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [string]$treasure, [bool]$expectedSet, [string]$source
) {
    if (-not $treasureIds.ContainsKey($treasure)) {
        throw "NPC visibility rule references unknown $treasure."
    }
    Confirm-NpcVisibilitySource $source $treasure
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`ttreasure`t-`t-`t$($treasureIds[$treasure].ToString('x2'))`t$([int]$expectedSet)`t$source")
}
function Add-NpcLinkedVisibility(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [bool]$expectedSet, [string]$source
) {
    Confirm-NpcVisibilitySource $source 'checkIsLinkedGame'
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`tlinked`t-`t-`t00`t$([int]$expectedSet)`t$source")
}
function Add-NpcEssenceVisibility(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [int]$mask, [bool]$expectedSet, [string]$source,
    [string]$sourceToken = 'TREASURE_ESSENCE'
) {
    Confirm-NpcVisibilitySource $source $sourceToken
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`tessence`t-`t-`t$($mask.ToString('x2'))`t$([int]$expectedSet)`t$source")
}
function Add-NpcWramVisibility(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [int]$address, [int]$mask, [bool]$expectedSet,
    [string]$source, [string]$addressToken
) {
    if ($address -lt 0xc5b0 -or $address -gt 0xcaff) {
        throw "NPC visibility rule references invalid save WRAM address 0x$($address.ToString('x4'))."
    }
    Confirm-NpcVisibilitySource $source $addressToken
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`twram`t-`t$($address.ToString('x4'))`t$($mask.ToString('x2'))`t$([int]$expectedSet)`t$source")
}
function Add-NpcRuntimeEquality(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [int]$address, [int]$expectedValue, [bool]$expectedEqual,
    [string]$source, [string]$addressToken
) {
    if ($address -lt 0xc000 -or $address -gt 0xdfff -or
        $expectedValue -lt 0 -or $expectedValue -gt 0xff) {
        throw "NPC visibility rule references invalid runtime WRAM equality."
    }
    Confirm-NpcVisibilitySource $source $addressToken
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`truntime-equals`t-`t$($address.ToString('x4'))`t$($expectedValue.ToString('x2'))`t$([int]$expectedEqual)`t$source")
}
function Add-NpcGameProgress1Visibility(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [int]$expectedState, [bool]$expectedEqual, [string]$source
) {
    if ($expectedState -lt 0 -or $expectedState -gt 5) {
        throw "NPC visibility rule references invalid getGameProgress_1 state $expectedState."
    }
    Confirm-NpcVisibilitySource $source 'getGameProgress_1'
    foreach ($token in @(
        'GLOBALFLAG_FINISHEDGAME',
        'GLOBALFLAG_SAW_TWINROVA_BEFORE_ENDGAME',
        'TREASURE_ESSENCE',
        'GLOBALFLAG_SAVED_NAYRU'
    )) {
        Confirm-NpcVisibilitySource 'miscMan2.s:getGameProgress_1' $token
    }
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`tgame-progress-1`t-`t-`t$($expectedState.ToString('x2'))`t$([int]$expectedEqual)`t$source")
}
function Add-NpcGameProgress1SetVisibility(
    [int]$id, [int]$subid, [int]$var03,
    [int[]]$expectedStates, [string]$source
) {
    $alternative = 0
    foreach ($expectedState in $expectedStates) {
        Add-NpcGameProgress1Visibility `
            $id $subid $var03 $alternative $expectedState $true $source
        $alternative++
    }
}
function Add-NpcGameProgress2Visibility(
    [int]$id, [int]$subid, [int]$var03, [int]$alternative,
    [int]$expectedState, [bool]$expectedEqual, [string]$source
) {
    if ($expectedState -lt 0 -or $expectedState -gt 7) {
        throw "NPC visibility rule references invalid getGameProgress_2 state $expectedState."
    }
    Confirm-NpcVisibilitySource $source 'getGameProgress_2'
    foreach ($token in @(
        'GLOBALFLAG_FINISHEDGAME',
        'wGroup4RoomFlags+$fc',
        'GLOBALFLAG_SAW_TWINROVA_BEFORE_ENDGAME',
        'TREASURE_ESSENCE',
        'GLOBALFLAG_SAVED_NAYRU'
    )) {
        Confirm-NpcVisibilitySource 'miscMan2.s:getGameProgress_2' $token
    }
    $variant = if ($var03 -lt 0) { '*' } else { $var03.ToString('x2') }
    $npcVisibilityRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$variant`t$alternative`tgame-progress-2`t-`t-`t$($expectedState.ToString('x2'))`t$([int]$expectedEqual)`t$source")
}
function Add-NpcGameProgress2SetVisibility(
    [int]$id, [int]$subid, [int]$var03,
    [int[]]$expectedStates, [string]$source
) {
    $alternative = 0
    foreach ($expectedState in $expectedStates) {
        Add-NpcGameProgress2Visibility `
            $id $subid $var03 $alternative $expectedState $true $source
        $alternative++
    }
}

# Ambi cutscene actors: current-room completion bits.
Add-NpcCurrentRoomVisibility 0x4d 0x03 -1 0 0x40 $false 'ambi.s:@initSubid03'
Add-NpcCurrentRoomVisibility 0x4d 0x06 -1 0 0x80 $false 'ambi.s:@initSubid06'
Add-NpcLinkedVisibility 0x4d 0x0a -1 0 $true 'ambi.s:@initSubid0a'
Add-NpcSpecificRoomVisibility 0x4d 0x0a -1 0 4 0xfc 0x80 $true 'ambi.s:@initSubid0a' 'wGroup4RoomFlags+$fc'

# Bear subid $02 selects mutually exclusive pre/post-game actors through var03.
Add-NpcGlobalVisibility 0x5d 0x02 0 0 'GLOBALFLAG_INTRO_DONE' $true 'bear.s:@initSubid02'
Add-NpcGlobalVisibility 0x5d 0x02 0 0 'GLOBALFLAG_FINISHEDGAME' $false 'bear.s:@initSubid02'
Add-NpcGlobalVisibility 0x5d 0x02 0 0 'GLOBALFLAG_MAKU_TREE_SAVED' $true 'bear.s:@initSubid02'
Add-NpcGlobalVisibility 0x5d 0x02 1 0 'GLOBALFLAG_FINISHEDGAME' $true 'bear.s:@var03IsNonzero'

# The two room 0:5a monkeys are available while Impa follows Link, then their
# state-0 initializer deletes them once the wider intro is complete.
Add-NpcGlobalVisibility 0x39 0x02 -1 0 'GLOBALFLAG_INTRO_DONE' $false 'monkeyMain.s:@subid2Init'
Add-NpcGlobalVisibility 0x39 0x03 -1 0 'GLOBALFLAG_INTRO_DONE' $false 'monkeyMain.s:@subid3Init'

# Monkey subid $07 selects three distinct story phases through var03.
Add-NpcGlobalVisibility 0x39 0x07 0 0 'GLOBALFLAG_FINISHEDGAME' $false 'monkeyMain.s:@subid7Init_0'
Add-NpcGlobalVisibility 0x39 0x07 0 0 'GLOBALFLAG_SAVED_NAYRU' $true 'monkeyMain.s:@subid7Init_0'
Add-NpcGlobalVisibility 0x39 0x07 1 0 'GLOBALFLAG_FINISHEDGAME' $true 'monkeyMain.s:@subid7Init_1'
Add-NpcGlobalVisibility 0x39 0x07 2 0 'GLOBALFLAG_FINISHEDGAME' $false 'monkeyMain.s:@subid7Init_2'
Add-NpcGlobalVisibility 0x39 0x07 2 0 'GLOBALFLAG_MAKU_TREE_SAVED' $true 'monkeyMain.s:@subid7Init_2'

Add-NpcGlobalVisibility 0x83 0x00 -1 0 'GLOBALFLAG_GOT_BOMB_UPGRADE_FROM_FAIRY' $false 'bombUpgradeFairy.s:@state0'
Add-NpcCurrentRoomVisibility 0x83 0x00 -1 0 0x01 $true 'bombUpgradeFairy.s:@state0'

# Boys used by room events and the post-game Lynna actor.
Add-NpcTreasureVisibility 0x3c 0x02 -1 0 'TREASURE_SEED_SATCHEL' $true 'boy.s:@initSubid02'
Add-NpcGameProgress1Visibility 0x3c 0x02 -1 1 0 $false 'boy.s:@initSubid02'
Add-NpcCurrentRoomVisibility 0x3c 0x03 -1 0 0x40 $false 'boy.s:@initSubid03'
Add-NpcCurrentRoomVisibility 0x3c 0x04 -1 0 0x40 $false 'boy.s:@initSubid04'
Add-NpcGlobalVisibility 0x3c 0x10 -1 0 'GLOBALFLAG_FINISHEDGAME' $true 'boy.s:@initSubid10'
Add-NpcGlobalVisibility 0x3f 0x00 -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'boy2.s:@@state0'
Add-NpcGlobalVisibility 0x3f 0x00 -1 0 'GLOBALFLAG_0b' $false 'boy2.s:@@state0'
Add-NpcCurrentRoomVisibility 0x3f 0x02 -1 0 0x40 $false 'boy2.s:@@state0'

# Forest-fairy phases use global progress plus room $90's entrance-open flag.
Add-NpcGlobalVisibility 0x49 0x07 -1 0 'GLOBALFLAG_WON_FAIRY_HIDING_GAME' $true 'forestFairy.s:forestFairy_subid07'
Add-NpcGlobalVisibility 0x49 0x07 -1 0 'GLOBALFLAG_FOREST_UNSCRAMBLED' $true 'forestFairy.s:forestFairy_subid07'
Add-NpcSpecificRoomVisibility 0x49 0x07 -1 0 0 0x90 0x40 $false 'forestFairy.s:forestFairy_subid07' 'wPresentRoomFlags+$90'
Add-NpcGlobalVisibility 0x49 0x0a -1 0 'GLOBALFLAG_WON_FAIRY_HIDING_GAME' $true 'forestFairy.s:forestFairy_subid0a'
Add-NpcGlobalVisibility 0x49 0x0a -1 0 'GLOBALFLAG_FOREST_UNSCRAMBLED' $true 'forestFairy.s:forestFairy_subid0a'
Add-NpcSpecificRoomVisibility 0x49 0x0a -1 0 0 0x90 0x40 $true 'forestFairy.s:forestFairy_subid0a' 'wPresentRoomFlags+$90'
Add-NpcGlobalVisibility 0x49 0x0a -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'forestFairy.s:forestFairy_subid0a'
Add-NpcGlobalVisibility 0x49 0x0b -1 0 'GLOBALFLAG_FINISHEDGAME' $true 'forestFairy.s:forestFairy_subid0b'
Add-NpcGlobalVisibility 0x49 0x10 -1 0 'GLOBALFLAG_GOT_FLUTE' $false 'forestFairy.s:forestFairy_subid10'
Add-NpcGlobalVisibility 0x49 0x10 -1 0 'GLOBALFLAG_FOREST_UNSCRAMBLED' $false 'forestFairy.s:forestFairy_subid10'
Add-NpcGlobalVisibility 0x49 0x10 -1 0 'GLOBALFLAG_COMPANION_LOST_IN_FOREST' $true 'forestFairy.s:forestFairy_subid10'

Add-NpcGlobalVisibility 0x8b 0x02 -1 0 'GLOBALFLAG_FINISHEDGAME' $true 'goronElder.s:@subid2'
Add-NpcGlobalVisibility 0x72 0x00 -1 0 'GLOBALFLAG_MOBLINS_KEEP_DESTROYED' $true 'kingMoblinDefeated.s:@subid0State0'
Add-NpcCurrentRoomVisibility 0x72 0x00 -1 0 0x40 $false 'kingMoblinDefeated.s:@subid0State0'
Add-NpcGlobalVisibility 0x9c 0x00 -1 0 'GLOBALFLAG_KING_ZORA_CURED' $true 'kingZora.s:@subid0State0'

# Save-gated supporting cast whose original state-0 code uses treasure,
# linked-game, essence, or arbitrary save-WRAM checks.
Add-NpcEssenceVisibility 0x31 0x07 -1 0 0x04 $true 'impaInCutscene.s:@init7' 'wEssencesObtained'
Add-NpcLinkedVisibility 0x31 0x07 -1 0 $true 'impaInCutscene.s:@init7'
Add-NpcGlobalVisibility 0x31 0x07 -1 0 'GLOBALFLAG_GOT_RING_FROM_ZELDA' $false 'impaInCutscene.s:@init7'
Add-NpcLinkedVisibility 0x31 0x04 -1 0 $false 'impaInCutscene.s:@init4'
Add-NpcTreasureVisibility 0x31 0x04 -1 0 'TREASURE_MAKU_SEED' $true 'impaInCutscene.s:@preBlackTowerCutscene'
Add-NpcGlobalVisibility 0x31 0x04 -1 0 'GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE' $false 'impaInCutscene.s:@preBlackTowerCutscene'
Add-NpcLinkedVisibility 0x31 0x05 -1 0 $true 'impaInCutscene.s:@init5'
Add-NpcTreasureVisibility 0x31 0x05 -1 0 'TREASURE_MAKU_SEED' $true 'impaInCutscene.s:@preBlackTowerCutscene'
Add-NpcGlobalVisibility 0x31 0x05 -1 0 'GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE' $false 'impaInCutscene.s:@preBlackTowerCutscene'
Add-NpcEssenceVisibility 0x4c 0x04 -1 0 0x04 $true 'bird.s:@initSubid04' 'wEssencesObtained'
Add-NpcLinkedVisibility 0x4c 0x04 -1 0 $true 'bird.s:@initSubid04'
Add-NpcGlobalVisibility 0x4c 0x04 -1 0 'GLOBALFLAG_GOT_RING_FROM_ZELDA' $false 'bird.s:@initSubid04'
Add-NpcLinkedVisibility 0x68 0x00 -1 0 $true 'rosa.s:@@state0'
Add-NpcEssenceVisibility 0x68 0x00 -1 0 0x04 $false 'rosa.s:@@state0' 'wEssencesObtained'
# getBlackTowerProgress checks room $90 before room $ba. Progress $00 therefore
# requires both entrance flags clear; progress $01 requires $ba set while $90
# remains clear. The var03 $00/$01 hardhats delete themselves outside those
# exact mutually exclusive states.
Add-NpcSpecificRoomVisibility 0x58 0x01 0 0 0 0x90 0x40 $false 'bank0.s:getBlackTowerProgress' 'wPresentRoomFlags+$90'
Add-NpcSpecificRoomVisibility 0x58 0x01 0 0 0 0xba 0x40 $false 'bank0.s:getBlackTowerProgress' 'wPresentRoomFlags+$ba'
Add-NpcSpecificRoomVisibility 0x58 0x01 1 0 0 0x90 0x40 $false 'bank0.s:getBlackTowerProgress' 'wPresentRoomFlags+$90'
Add-NpcSpecificRoomVisibility 0x58 0x01 1 0 0 0xba 0x40 $true 'bank0.s:getBlackTowerProgress' 'wPresentRoomFlags+$ba'
Add-NpcEssenceVisibility 0x58 0x02 -1 0 0x08 $false 'hardhatWorker.s:@@state0' 'wEssencesObtained'

# The linked Lynna subrosian exists only for getGameProgress_2 states $05 or
# $07: after seeing Twinrova or after finishing the game.
Add-NpcLinkedVisibility 0x4e 0x00 -1 0 $true 'subrosian.s:subrosian_subid00'
Add-NpcGlobalVisibility 0x4e 0x00 -1 0 'GLOBALFLAG_SAW_TWINROVA_BEFORE_ENDGAME' $true 'miscMan2.s:getGameProgress_2'
Add-NpcLinkedVisibility 0x4e 0x00 -1 1 $true 'subrosian.s:subrosian_subid00'
Add-NpcGlobalVisibility 0x4e 0x00 -1 1 'GLOBALFLAG_FINISHEDGAME' $true 'miscMan2.s:getGameProgress_2'

# The fourteen search/bridge carpenter records share one initializer. In an
# unlinked game they exist until the bridge is built; in a linked game Zelda
# must also have been rescued. Subid $09 deliberately bypasses these gates.
$carpenterSubids = @(0x00, 0x01, 0x02, 0x03, 0x04,
    0xb2, 0xb3, 0xb4, 0xc2, 0xc3, 0xc4, 0xd2, 0xd3, 0xd4)
foreach ($carpenterSubid in $carpenterSubids) {
    Add-NpcGlobalVisibility 0x9a $carpenterSubid -1 0 'GLOBALFLAG_SYMMETRY_BRIDGE_BUILT' $false 'carpenter.s:@state0'
    Add-NpcLinkedVisibility 0x9a $carpenterSubid -1 0 $false 'carpenter.s:@state0'
    Add-NpcGlobalVisibility 0x9a $carpenterSubid -1 1 'GLOBALFLAG_SYMMETRY_BRIDGE_BUILT' $false 'carpenter.s:@state0'
    Add-NpcLinkedVisibility 0x9a $carpenterSubid -1 1 $true 'carpenter.s:@state0'
    Add-NpcGlobalVisibility 0x9a $carpenterSubid -1 1 'GLOBALFLAG_GOT_RING_FROM_ZELDA' $true 'carpenter.s:@state0'
}

# Mamamu's indoor dog survives when any of these original branches is true.
Add-NpcGlobalVisibility 0x54 0x00 -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'mamamuDog.s:@state0'
Add-NpcGlobalVisibility 0x54 0x00 -1 1 'GLOBALFLAG_RETURNED_DOG' $true 'mamamuDog.s:@state0'
Add-NpcCurrentRoomVisibility 0x54 0x00 -1 2 0x20 $false 'mamamuDog.s:@state0'

# The roaming dog has one placement for each wMamamuDogLocation value. The
# sidequest start is stored in present room $e7 flag $80; the selected screen
# itself is transient WRAM $cde2 and is deliberately not part of the save file.
foreach ($dogLocation in 0..3) {
    Add-NpcGlobalVisibility 0x54 0x01 $dogLocation 0 'GLOBALFLAG_RETURNED_DOG' $false 'mamamuDog.s:dog_subid01'
    Add-NpcSpecificRoomVisibility 0x54 0x01 $dogLocation 0 0 0xe7 0x80 $true 'mamamuDog.s:dog_subid01' 'wPresentRoomFlags+$e7'
    Add-NpcRuntimeEquality 0x54 0x01 $dogLocation 0 0xcde2 $dogLocation $true 'mamamuDog.s:dog_subid01' 'wMamamuDogLocation'
}

# Mutually exclusive pre/post-bombs and pre/post-game town actors.
Add-NpcGlobalVisibility 0x41 0x00 -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'miscMan.s:@subid0'
Add-NpcGlobalVisibility 0x41 0x00 -1 0 'GLOBALFLAG_0b' $false 'miscMan.s:@subid0'
foreach ($miscManSubid in 1..6) {
    Add-NpcGameProgress1Visibility 0x41 $miscManSubid -1 0 ($miscManSubid - 1) $true 'miscMan.s:@subidNonzero'
}
Add-NpcGlobalVisibility 0x44 0x00 -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'miscMan2.s:@subid0'
Add-NpcGlobalVisibility 0x42 0x00 -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'mustacheMan.s:@subid0'
Add-NpcGlobalVisibility 0x52 0x02 -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'oldMan.s:@@state0'
Add-NpcGlobalVisibility 0x45 0x00 -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'pastOldLady.s:@subid0'

# The linked-secret old ladies share linkedNpc_checkShouldSpawn. Secret index
# $00 appears after D4; index $09 appears after D2.
Confirm-NpcVisibilitySource 'oldLady.s:@initSubid4' 'linkedGameNpcScript'
Confirm-NpcVisibilitySource 'oldLady.s:@initSubid5' 'linkedGameNpcScript'
Add-NpcLinkedVisibility 0x3d 0x04 -1 0 $true 'scriptHelper.s:linkedNpc_checkShouldSpawn'
Add-NpcEssenceVisibility 0x3d 0x04 -1 0 0x08 $true 'scriptHelper.s:@checkd4' '@checkd4'
Add-NpcLinkedVisibility 0x3d 0x05 -1 0 $true 'scriptHelper.s:linkedNpc_checkShouldSpawn'
Add-NpcEssenceVisibility 0x3d 0x05 -1 0 0x02 $true 'scriptHelper.s:@checkd2_2' '@checkd2_2'

# Lynna City's paired villager placements use the original
# checkNpcShouldExistAtGameStage table. Import every phase listed for each
# subid, including the three actors placed together in room 0:68.
$npcStageSelectionSource = $npcVisibilitySources['miscMan2.s']
foreach ($expectedTable in @(
    '(?ms)^@data0:.*?^@@subid1:\s*\r?\n\s*\.db \$00 \$01 \$02 \$ff\s*\r?\n^@@subid2:\s*\r?\n\s*\.db \$03 \$04 \$05 \$ff',
    '(?ms)^@data3:.*?^@@subid4:\s*\r?\n\s*\.db \$00 \$01 \$05 \$ff\s*\r?\n^@@subid5:\s*\r?\n\s*\.db \$04 \$ff',
    '(?ms)^@data6:.*?^@@subid2:\s*\r?\n\s*\.db \$00 \$01 \$02 \$ff\s*\r?\n^@@subid3:\s*\r?\n\s*\.db \$03 \$04 \$05 \$ff'
)) {
    if ($npcStageSelectionSource -notmatch $expectedTable) {
        throw 'checkNpcShouldExistAtGameStage no longer matches the imported Lynna NPC phase sets.'
    }
}
Add-NpcGameProgress1SetVisibility 0x3b 0x01 -1 @(0, 1, 2) 'femaleVillager.s:@initSubid01'
Add-NpcGameProgress1SetVisibility 0x3b 0x02 -1 @(3, 4, 5) 'femaleVillager.s:@initSubid02'
Add-NpcGameProgress1SetVisibility 0x3a 0x04 -1 @(0, 1, 5) 'villager.s:@initSubid04'
Add-NpcGameProgress1SetVisibility 0x3a 0x05 -1 @(4) 'villager.s:@initSubid05'
Add-NpcGameProgress1SetVisibility 0x44 0x02 -1 @(0, 1, 2) 'miscMan2.s:@subid2'
Add-NpcGameProgress1SetVisibility 0x44 0x03 -1 @(3, 4, 5) 'miscMan2.s:@subid3'
Add-NpcGameProgress2Visibility 0x44 0x04 -1 0 0x03 $false 'miscMan2.s:@subid4'
Add-NpcGameProgress2SetVisibility 0x3b 0x05 -1 @(0, 1, 2, 3, 5, 6) 'femaleVillager.s:@initSubid05'
Add-NpcGameProgress2SetVisibility 0x3a 0x06 -1 @(0, 1, 2) 'villager.s:@initSubid06'
Add-NpcGameProgress2SetVisibility 0x3a 0x07 -1 @(3, 4, 5, 6, 7) 'villager.s:@initSubid07'
Add-NpcGameProgress2SetVisibility 0x38 0x00 -1 @(0, 3, 4, 5, 6, 7) 'pastGirl.s:@subid0Init'

# Impa's shared story-state function controls her room NPC subids.
# House subid $00 adds $09 in a linked game, selecting one of the exported
# position/text variants above. Positioned subids $01 and $02 exist only in
# states $07 and $08; subid $03 is created dynamically rather than placed.
function Add-ImpaStateBase(
    [int]$subid, [int]$var03, [bool]$d2PassageOpen, [int]$linked
) {
    Add-NpcGlobalVisibility 0x4f $subid $var03 0 'GLOBALFLAG_FINISHEDGAME' $false 'impaNpc.s:getImpaNpcState'
    Add-NpcSpecificRoomVisibility 0x4f $subid $var03 0 0 0x83 0x80 $d2PassageOpen 'impaNpc.s:getImpaNpcState' 'wPresentRoomFlags+$83'
    if ($linked -ge 0) {
        Add-NpcLinkedVisibility 0x4f $subid $var03 0 ([bool]$linked) 'impaNpc.s:impaNpc_subid00'
    }
}

Add-ImpaStateBase 0x00 0x00 $false 0
Add-ImpaStateBase 0x00 0x01 $true 0
Add-NpcTreasureVisibility 0x4f 0x00 0x01 0 'TREASURE_HARP' $false 'impaNpc.s:getImpaNpcState'
Add-ImpaStateBase 0x00 0x02 $true 0
Add-NpcTreasureVisibility 0x4f 0x00 0x02 0 'TREASURE_HARP' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x00 0x02 0 'GLOBALFLAG_SAVED_NAYRU' $false 'impaNpc.s:getImpaNpcState'
Add-ImpaStateBase 0x00 0x05 $true 0
Add-NpcTreasureVisibility 0x4f 0x00 0x05 0 'TREASURE_HARP' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x00 0x05 0 'GLOBALFLAG_SAVED_NAYRU' $true 'impaNpc.s:getImpaNpcState'
Add-NpcTreasureVisibility 0x4f 0x00 0x05 0 'TREASURE_MAKU_SEED' $false 'impaNpc.s:getImpaNpcState'

Add-ImpaStateBase 0x00 0x09 $false 1
Add-ImpaStateBase 0x00 0x0a $true 1
Add-NpcTreasureVisibility 0x4f 0x00 0x0a 0 'TREASURE_HARP' $false 'impaNpc.s:getImpaNpcState'
Add-ImpaStateBase 0x00 0x0b $true 1
Add-NpcTreasureVisibility 0x4f 0x00 0x0b 0 'TREASURE_HARP' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x00 0x0b 0 'GLOBALFLAG_SAVED_NAYRU' $false 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x00 0x0b 0 'GLOBALFLAG_GOT_RING_FROM_ZELDA' $false 'impaNpc.s:getImpaNpcState'
Add-NpcEssenceVisibility 0x4f 0x00 0x0b 0 0x04 $false 'impaNpc.s:getImpaNpcState'
Add-ImpaStateBase 0x00 0x0d $true 1
Add-NpcTreasureVisibility 0x4f 0x00 0x0d 0 'TREASURE_HARP' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x00 0x0d 0 'GLOBALFLAG_SAVED_NAYRU' $false 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x00 0x0d 0 'GLOBALFLAG_GOT_RING_FROM_ZELDA' $true 'impaNpc.s:getImpaNpcState'
Add-ImpaStateBase 0x00 0x0e $true 1
Add-NpcTreasureVisibility 0x4f 0x00 0x0e 0 'TREASURE_HARP' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x00 0x0e 0 'GLOBALFLAG_SAVED_NAYRU' $true 'impaNpc.s:getImpaNpcState'
Add-NpcTreasureVisibility 0x4f 0x00 0x0e 0 'TREASURE_MAKU_SEED' $false 'impaNpc.s:getImpaNpcState'

Add-ImpaStateBase 0x01 -1 $true -1
Add-NpcTreasureVisibility 0x4f 0x01 -1 0 'TREASURE_HARP' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x01 -1 0 'GLOBALFLAG_SAVED_NAYRU' $true 'impaNpc.s:getImpaNpcState'
Add-NpcTreasureVisibility 0x4f 0x01 -1 0 'TREASURE_MAKU_SEED' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x01 -1 0 'GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x01 -1 0 'GLOBALFLAG_FLAME_OF_DESPAIR_LIT' $false 'impaNpc.s:getImpaNpcState'
Add-ImpaStateBase 0x02 -1 $true -1
Add-NpcTreasureVisibility 0x4f 0x02 -1 0 'TREASURE_HARP' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x02 -1 0 'GLOBALFLAG_SAVED_NAYRU' $true 'impaNpc.s:getImpaNpcState'
Add-NpcTreasureVisibility 0x4f 0x02 -1 0 'TREASURE_MAKU_SEED' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x02 -1 0 'GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE' $true 'impaNpc.s:getImpaNpcState'
Add-NpcGlobalVisibility 0x4f 0x02 -1 0 'GLOBALFLAG_FLAME_OF_DESPAIR_LIT' $true 'impaNpc.s:getImpaNpcState'
# Nayru's placed house and linked/post-game variants.
Add-NpcLinkedVisibility 0x36 0x0a -1 0 $true 'nayru.s:@init0a'
Add-NpcTreasureVisibility 0x36 0x0a -1 0 'TREASURE_MAKU_SEED' $true 'nayru.s:@init0a'
Add-NpcGlobalVisibility 0x36 0x0a -1 0 'GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE' $false 'nayru.s:@init0a'
Add-NpcGlobalVisibility 0x36 0x0b -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'nayru.s:@init0b'
Add-NpcGlobalVisibility 0x36 0x0b -1 0 'GLOBALFLAG_SAVED_NAYRU' $true 'nayru.s:@init0b'
Add-NpcTreasureVisibility 0x36 0x0b -1 0 'TREASURE_MAKU_SEED' $false 'nayru.s:@init0b'
Add-NpcGlobalVisibility 0x36 0x0c -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'nayru.s:@init0c'
Add-NpcGlobalVisibility 0x36 0x0c -1 0 'GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE' $true 'nayru.s:@init0c'
Add-NpcGlobalVisibility 0x36 0x0c -1 0 'GLOBALFLAG_FLAME_OF_DESPAIR_LIT' $false 'nayru.s:@init0c'
Add-NpcGlobalVisibility 0x36 0x0d -1 0 'GLOBALFLAG_FLAME_OF_DESPAIR_LIT' $true 'nayru.s:@init0d'
Add-NpcGlobalVisibility 0x36 0x0d -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'nayru.s:@init0d'
Add-NpcGlobalVisibility 0x36 0x13 -1 0 'GLOBALFLAG_FINISHEDGAME' $true 'nayru.s:@init13'

# Zelda's three positioned story variants.
Add-NpcLinkedVisibility 0xad 0x04 -1 0 $true 'zelda.s:@initSubid04'
Add-NpcTreasureVisibility 0xad 0x04 -1 0 'TREASURE_MAKU_SEED' $true 'zelda.s:@initSubid04'
Add-NpcGlobalVisibility 0xad 0x04 -1 0 'GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE' $false 'zelda.s:@initSubid04'
Add-NpcGlobalVisibility 0xad 0x07 -1 0 'GLOBALFLAG_GOT_RING_FROM_ZELDA' $true 'zelda.s:@initSubid07'
Add-NpcTreasureVisibility 0xad 0x07 -1 0 'TREASURE_MAKU_SEED' $false 'zelda.s:@initSubid07'
Add-NpcLinkedVisibility 0xad 0x08 -1 0 $true 'zelda.s:@initSubid08'
Add-NpcGlobalVisibility 0xad 0x08 -1 0 'GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE' $true 'zelda.s:@initSubid08'
Add-NpcGlobalVisibility 0xad 0x08 -1 0 'GLOBALFLAG_FLAME_OF_DESPAIR_LIT' $false 'zelda.s:@initSubid08'

# The two placed past-guy variants exchange places when GLOBALFLAG_0b changes.
Add-NpcGlobalVisibility 0x43 0x00 0 0 'GLOBALFLAG_FINISHEDGAME' $false 'pastGuy.s:@subid0'
Add-NpcGlobalVisibility 0x43 0x00 0 0 'GLOBALFLAG_0b' $false 'pastGuy.s:@subid0'
Add-NpcGlobalVisibility 0x43 0x00 1 0 'GLOBALFLAG_FINISHEDGAME' $false 'pastGuy.s:@subid0'
Add-NpcGlobalVisibility 0x43 0x00 1 0 'GLOBALFLAG_0b' $true 'pastGuy.s:@subid0'

# Poe's var03 selects the overworld, tomb, or final-item encounter.
Add-NpcCurrentRoomVisibility 0x59 0x00 0 0 0x40 $false 'poe.s:@initSubid00'
Add-NpcSpecificRoomVisibility 0x59 0x00 0 0 0 0x2e 0x40 $false 'poe.s:@initSubid00' 'wPresentRoomFlags+$2e'
Add-NpcSpecificRoomVisibility 0x59 0x00 1 0 0 0x7c 0x40 $true 'poe.s:@initSubid01' 'wPresentRoomFlags+$7c'
Add-NpcCurrentRoomVisibility 0x59 0x00 1 0 0x40 $false 'poe.s:@initSubid01'
Add-NpcCurrentRoomVisibility 0x59 0x00 2 0 0x20 $false 'poe.s:@initSubid02'
Add-NpcCurrentRoomVisibility 0x59 0x00 2 0 0x40 $true 'poe.s:@initSubid02'
Add-NpcSpecificRoomVisibility 0x59 0x00 2 0 0 0x2e 0x40 $true 'poe.s:@initSubid02' 'wPresentRoomFlags+$2e'

Add-NpcGlobalVisibility 0x6d 0x00 -1 0 'GLOBALFLAG_BEAT_POSSESSED_NAYRU' $false 'possessedNayru.s:@state0'
Add-NpcCurrentRoomVisibility 0x69 0x00 -1 0 0x80 $false 'rafton.s:@state0'
Add-NpcGlobalVisibility 0x69 0x00 -1 0 'GLOBALFLAG_RAFTON_CHANGED_ROOMS' $false 'rafton.s:@initSubid00'
Add-NpcCurrentRoomVisibility 0x69 0x01 -1 0 0x80 $false 'rafton.s:@state0'
Add-NpcGlobalVisibility 0x69 0x01 -1 0 'GLOBALFLAG_RAFTON_CHANGED_ROOMS' $true 'rafton.s:@initSubid01'

Add-NpcGlobalVisibility 0x37 0x03 -1 0 'GLOBALFLAG_GAVE_ROPE_TO_RAFTON' $true 'ralph.s:@initSubid03'
Add-NpcCurrentRoomVisibility 0x37 0x03 -1 0 0x40 $false 'ralph.s:@initSubid03'
Add-NpcGlobalVisibility 0x37 0x09 -1 0 'GLOBALFLAG_RALPH_ENTERED_AMBIS_PALACE' $false 'ralph.s:@initSubid09'
Add-NpcEssenceVisibility 0x37 0x09 -1 0 0x20 $true 'ralph.s:@initSubid09'
Add-NpcTreasureVisibility 0x37 0x0a -1 0 'TREASURE_MAKU_SEED' $true 'ralph.s:@initSubid0a'
Add-NpcGlobalVisibility 0x37 0x0a -1 0 'GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE' $false 'ralph.s:@initSubid0a'
Add-NpcGlobalVisibility 0x37 0x0a -1 0 'GLOBALFLAG_RALPH_ENTERED_BLACK_TOWER' $false 'ralph.s:@initSubid0a'
Add-NpcGlobalVisibility 0x37 0x11 -1 0 'GLOBALFLAG_FINISHEDGAME' $true 'ralph.s:@initSubid11'
Add-NpcLinkedVisibility 0x37 0x12 -1 0 $true 'ralph.s:@initSubid12'
Add-NpcSpecificRoomVisibility 0x37 0x12 -1 0 4 0xfc 0x80 $true 'ralph.s:@initSubid12' 'wGroup4RoomFlags + (<ROOM_AGES_4fc)'

# Soldier $00/$01 variants swap on GLOBALFLAG_0b; all disappear post-game.
foreach ($soldierSubid in @(0x00, 0x01)) {
    Add-NpcGlobalVisibility 0x40 $soldierSubid 0 0 'GLOBALFLAG_FINISHEDGAME' $false 'soldier.s:soldierSubid00'
    Add-NpcGlobalVisibility 0x40 $soldierSubid 0 0 'GLOBALFLAG_0b' $false 'soldier.s:soldierSubid00'
    Add-NpcGlobalVisibility 0x40 $soldierSubid 1 0 'GLOBALFLAG_FINISHEDGAME' $false 'soldier.s:soldierSubid01'
    Add-NpcGlobalVisibility 0x40 $soldierSubid 1 0 'GLOBALFLAG_0b' $true 'soldier.s:soldierSubid01'
}
Add-NpcGlobalVisibility 0x40 0x0b -1 0 'GLOBALFLAG_0b' $false 'soldier.s:soldierSubid0b'
Add-NpcTreasureVisibility 0x40 0x0b -1 0 'TREASURE_MYSTERY_SEEDS' $true 'soldier.s:soldierSubid0b'

# Tokay and Zora variants which are selected directly by linked, essence,
# room, and companion-state checks.
Add-NpcLinkedVisibility 0x48 0x07 -1 0 $false 'tokay.s:@initSubid07'
Add-NpcLinkedVisibility 0x48 0x0b -1 0 $true 'tokay.s:@initSubid0b'
Add-NpcTreasureVisibility 0x48 0x0b -1 0 'TREASURE_SHOVEL' $false 'tokay.s:@initSubid0b'
Add-NpcCurrentRoomVisibility 0x48 0x0b -1 0 0x80 $false 'tokay.s:@initSubid0b'
Add-NpcEssenceVisibility 0x48 0x10 -1 0 0x04 $true 'tokay.s:@initSubid10' 'wEssencesObtained'
Add-NpcWramVisibility 0x48 0x10 -1 0 0xc647 0x02 $false 'tokay.s:@initSubid10' 'wDimitriState'

Add-NpcCurrentRoomVisibility 0xab 0x10 -1 0 0x20 $false 'zora.s:@subid10'
Add-NpcEssenceVisibility 0xab 0x10 -1 0 0x40 $true 'zora.s:@subid10' 'wEssencesObtained'
Add-NpcLinkedVisibility 0xab 0x11 -1 0 $false 'zora.s:@subid11'
Add-NpcCurrentRoomVisibility 0xab 0x11 -1 0 0x40 $false 'zora.s:@deleteIfFlagSet'
Add-NpcLinkedVisibility 0xab 0x12 -1 0 $true 'zora.s:@subid12'
Add-NpcCurrentRoomVisibility 0xab 0x12 -1 0 0x40 $false 'zora.s:@deleteIfFlagSet'

Add-NpcGlobalVisibility 0xbf 0x0c -1 0 'GLOBALFLAG_TUNI_NUT_PLACED' $true 'symmetryNpc.s:@subid0cInit'

if ($npcVisibilityRows.Count -ne 329) {
    throw "Expected 328 imported NPC visibility predicates, got $($npcVisibilityRows.Count - 1)."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\npc_visibility.tsv'),
    $npcVisibilityRows,
    [Text.UTF8Encoding]::new($false))

# Initial Nayru cutscene in present room $39. The room contains the unpositioned
# INTERAC_MISCELLANEOUS_1 $6b:$01 controller; it creates the seven actors in
# objectData.nayruAndAnimalsInIntro while GLOBALFLAG_INTRO_DONE is clear. Export
# those dynamic actors (plus the ghost/human Veran and aftermath actors) with
# every animation index used by their original state machines.
$nayruIntroActors = @(
    @(0x36, 0x00, 0x18, 0x78, 0x00, 'Nayru'),
    @(0x37, 0x00, 0x30, 0x88, 0x00, 'Ralph'),
    @(0x5d, 0x00, 0x28, 0x58, 0x00, 'Bear'),
    @(0x39, 0x00, 0x50, 0x78, 0x00, 'Monkey'),
    @(0x4b, 0x00, 0x50, 0x88, 0x00, 'Rabbit'),
    @(0x3c, 0x00, 0x48, 0x68, 0x00, 'Boy'),
    @(0x4c, 0x00, 0x2c, 0x48, 0x00, 'Bird'),
    @(0x3e, 0x00, 0x58, 0x58, 0x00, 'GhostVeran'),
    @(0xbb, 0x00, 0x58, 0x58, 0x00, 'HumanVeran'),
    @(0x5e, 0x00, 0x00, 0x00, 0x00, 'RalphSword'),
    @(0x37, 0x02, 0x28, 0x48, 0x00, 'AftermathRalph'),
    @(0x31, 0x01, 0x68, 0x38, 0x00, 'AftermathImpa'),
    @(0x3a, 0x00, 0x42, 0x78, 0x00, 'VignetteGuy'),
    @(0x44, 0x01, 0x42, 0x78, 0x00, 'VignetteOldMan'),
    @(0x3b, 0x00, 0x42, 0x68, 0x00, 'VignetteGirl'),
    @(0x3c, 0x01, 0x48, 0x78, 0x00, 'VignetteBoy'),
    @(0x3d, 0x01, 0x28, 0x68, 0x00, 'VignetteLady'),
    @(0x9f, 0x00, 0x00, 0x00, 0x00, 'Exclamation')
)
$nayruActorRows = [Collections.Generic.List[string]]::new()
$nayruActorRows.Add('# index`tid`tsubid`ty`tx`tvar03`tname`tsprite`ttile-base`tpalette`tdefault-animation`tanimation-0`tanimation-1`tanimation-2`tanimation-3`tanimation-4`tanimation-5`tanimation-6`tanimation-7`tanimation-8`tanimation-9`tanimation-10`tinitial-animation`textra-sprite')
$nayruInitialAnimations = @{
    'Nayru' = 4
    'Ralph' = 0
    'Bear' = 0
    'Monkey' = 2
    'Rabbit' = 0
    'Boy' = 0
    'Bird' = 1
    'VignetteGuy' = 3
    'VignetteOldMan' = 4
    'VignetteGirl' = 1
    'VignetteBoy' = 1
}
$nayruExtraGraphics = @{
    0x36 = 'spr_nayru_2'
    0x37 = 'spr_ralph_2'
}
foreach ($extraActor in @(
    @{ Id = 0x36; File = 'nayru.s'; Header = 0x26; ExtraHeader = 0x27 },
    @{ Id = 0x37; File = 'ralph.s'; Header = 0x24; ExtraHeader = 0x25 }
)) {
    $actorSource = Get-Content -Raw (
        Join-Path $Disassembly "object_code\ages\interactions\$($extraActor.File)")
    $header = $extraActor.Header.ToString('x2')
    $extraHeader = $extraActor.ExtraHeader.ToString('x2')
    $extraSprite = $nayruExtraGraphics[$extraActor.Id]
    $headerNeedle = '/* $' + $header + ' */ m_ObjectGfxHeader ' +
        $gfxNames[$extraActor.Header]
    $extraHeaderNeedle = '/* $' + $extraHeader + ' */ m_ObjectGfxHeader ' +
        $extraSprite + ', 1'
    if ($actorSource -notmatch 'interactionLoadExtraGraphics' -or
        -not $objectGfxHeaderSource.Contains($headerNeedle) -or
        -not $objectGfxHeaderSource.Contains($extraHeaderNeedle)) {
        throw "Could not resolve $extraSprite through the initial Nayru cutscene actor graphics chain."
    }
}
$nayruInitialSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')
$ralphInitialSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\ralph.s')
$boyInitialSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\boy.s')
$monkeyInitialSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\monkeyMain.s')
if ($nayruInitialSource -notmatch '(?ms)@init00:.*?@setSingingAnimation:\s*ld a,\$04\s*call interactionSetAnimation' -or
    $ralphInitialSource -notmatch '(?ms)@initSubid00:\s*@initSubid05:\s*xor a\s*@setAnimation:\s*call interactionSetAnimation' -or
    $boyInitialSource -notmatch '(?ms)@initSubid00:\s*xor a\s*call interactionSetAnimation' -or
    $monkeyInitialSource -notmatch '(?ms)@subid0Init:\s*ld a,\$02\s*call interactionSetAnimation' -or
    $interactionGraphics['93:0'].DefaultAnimation -ne 0 -or
    $interactionGraphics['75:0'].DefaultAnimation -ne 0 -or
    $interactionGraphics['76:0'].DefaultAnimation -ne 1) {
    throw 'An initial Nayru gathering actor animation changed in its interaction initializer.'
}
for ($actorIndex = 0; $actorIndex -lt $nayruIntroActors.Count; $actorIndex++) {
    $actor = $nayruIntroActors[$actorIndex]
    $id = [int]$actor[0]
    $subid = [int]$actor[1]
    $graphic = $interactionGraphics["$id`:$subid"]
    if ($null -eq $graphic) { $graphic = $interactionGraphics["$id`:0"] }
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw "Could not resolve initial Nayru cutscene actor $($actor[5]) `$$($id.ToString('x2')):`$$($subid.ToString('x2'))."
    }
    $spriteName = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($spriteName)
    $animations = @(0..10 | ForEach-Object { Resolve-NpcAnimation $id $_ })
    if (-not $animations[$graphic.DefaultAnimation]) {
        throw "Initial Nayru cutscene actor $($actor[5]) has no default animation `$$($graphic.DefaultAnimation.ToString('x2'))."
    }
    $extraSprite = if ($nayruExtraGraphics.ContainsKey($id)) {
        $nayruExtraGraphics[$id]
    } else { '' }
    if ($extraSprite) { [void]$npcSpriteNames.Add($extraSprite) }
    $initialAnimation = if ($nayruInitialAnimations.ContainsKey([string]$actor[5])) {
        $nayruInitialAnimations[[string]$actor[5]]
    } else { $graphic.DefaultAnimation }
    if (-not $animations[$initialAnimation]) {
        throw "Initial Nayru cutscene actor $($actor[5]) has no initial animation `$$($initialAnimation.ToString('x2'))."
    }
    $columns = @(
        $actorIndex.ToString(), $id.ToString('x2'), $subid.ToString('x2'),
        ([int]$actor[2]).ToString('x2'), ([int]$actor[3]).ToString('x2'),
        ([int]$actor[4]).ToString('x2'), [string]$actor[5], $spriteName,
        $graphic.TileBase.ToString(), $graphic.Palette.ToString(),
        $graphic.DefaultAnimation.ToString()
    ) + $animations + @($initialAnimation.ToString(), $extraSprite)
    $nayruActorRows.Add($columns -join "`t")
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\nayru_intro_actors.tsv'),
    $nayruActorRows,
    [Text.UTF8Encoding]::new($false))

# The three visions after TX_5607 are not ordinary loads of these rooms. The
# singing handler indexes objectTable2, runs those interactions until one writes
# $ff to cfdf, and only then advances to the next room. Export the room order,
# exact interaction lifetime, and the ten-entry monkey initializer table used by
# objectData7717 instead of duplicating them in the runtime.
$nayruObjectData2Source = Get-Content -Raw (
    Join-Path $Disassembly 'objects\ages\extraData3.s')
$nayruMiscInteractionSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
$nayruFemaleVillagerSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\femaleVillager.s')
$nayruOldLadySource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\oldLady.s')
$nayruVignetteCutsceneSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$nayruVignetteMonkeySource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\monkeyMain.s')
if ($nayruObjectData2Source -notmatch '(?ms)^objectTable2:.*?objectData7705.*?objectData7717.*?objectData771b' -or
    $nayruObjectData2Source -notmatch '(?ms)^objectData7705:.*?obj_Interaction \$3a \$00 \$42 \$78.*?obj_Interaction \$44 \$01 \$42 \$78.*?obj_Interaction \$3b \$00 \$42 \$68.*?obj_Interaction \$6b \$05 \$48 \$88' -or
    $nayruObjectData2Source -notmatch '(?ms)^objectData7717:.*?obj_Interaction \$39 \$01' -or
    $nayruObjectData2Source -notmatch '(?ms)^objectData771b:.*?obj_Interaction \$3c \$01 \$48 \$78.*?obj_Interaction \$3d \$01 \$28 \$68' -or
    $nayruVignetteCutsceneSource -notmatch '(?ms)^cutscene_disableLcdLoadRoomResetCamera:.*?ROOM_AGES_098.*?ROOM_AGES_05a.*?ROOM_AGES_20e.*?ROOM_AGES_039' -or
    $nayruMiscInteractionSource -notmatch '(?ms)interaction6b_subid05:.*?ld \(hl\),20.*?cp \$04.*?cfd1\),a.*?@lightningPositions:\s*\.db \$28 \$28\s*\.db \$58 \$38\s*\.db \$38 \$68\s*\.db \$48 \$98' -or
    $nayruFemaleVillagerSource -notmatch '(?ms)@runSubid00:.*?cp \$02.*?interactionOscillateXRandomly.*?cp \$04.*?ld \(hl\),\$1e.*?ld bc,-\$1c0.*?objectUpdateSpeedZ_paramC' -or
    $boyInitialSource -notmatch '(?ms)^boyRunSubid01:.*?cp \$01.*?interactionAnimate2Times.*?cp \$02.*?xor \$04.*?interactionAnimate' -or
    $nayruOldLadySource -notmatch '(?ms)@runSubid1:.*?ld \(hl\),60.*?ld \(hl\),20.*?interactionAnimate3Times.*?ld \(\$cfdf\),a') {
    throw 'An initial Nayru time-stop vignette object set or state machine changed.'
}
$nayruVignetteRows = @(
    '# index`tgroup`troom`tduration',
    "0`t0`t98`t937",
    "1`t0`t5a`t600",
    "2`t2`t0e`t645"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\nayru_intro_vignettes.tsv'),
    $nayruVignetteRows,
    [Text.UTF8Encoding]::new($false))

$nayruMonkeyRows = @(
    '# index`ty`tx`tstone-counter`tanimation',
    "0`t58`t88`t240`t0",
    "1`t58`t78`t210`t1",
    "2`t28`t28`t220`t1",
    "3`t38`t38`t190`t2",
    "4`t18`t68`t100`t1",
    "5`t1c`t80`t120`t0",
    "6`t30`t68`t80`t5",
    "7`t34`t88`t140`t2",
    "8`t50`t46`t180`t2",
    "9`t64`t28`t184`t8"
)
$nayruMonkeyTable = [regex]::Match(
    $nayruVignetteMonkeySource,
    '(?ms)^@monkeyPositions:.*?\.db \$58 \$88 \$f0 \$00.*?\.db \$58 \$78 \$d2 \$01.*?\.db \$28 \$28 \$dc \$01.*?\.db \$38 \$38 \$be \$02.*?\.db \$18 \$68 \$64 \$01.*?\.db \$1c \$80 \$78 \$00.*?\.db \$30 \$68 \$50 \$05.*?\.db \$34 \$88 \$8c \$02.*?\.db \$50 \$46 \$b4 \$02.*?\.db \$64 \$28 \$b8 \$08')
if (-not $nayruMonkeyTable.Success -or
    $nayruVignetteMonkeySource -notmatch '(?ms)^monkeySubid1State1:.*?monkey0Disappearance.*?monkey9Disappearance' -or
    $nayruVignetteMonkeySource -notmatch '(?ms)^monkey8Disappearance:.*?ld \(hl\),\$5a.*?ld \(hl\),\$b4.*?ld \(hl\),\$1e.*?ld \(\$cfdf\),a') {
    throw 'The ten-monkey disappearance positions, counters, or terminal timing changed.'
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\nayru_intro_vignette_monkeys.tsv'),
    $nayruMonkeyRows,
    [Text.UTF8Encoding]::new($false))

# INTERAC_FLOATING_IMAGE $a0:$01 supplies Nayru's 70-update singing notes.
# PART_LIGHTNING $27 supplies both the portal strike and the first vignette's
# thunderbolts. Export their original OAM instead of approximating either with
# a Godot primitive.
$nayruMusicNoteAnimation = Resolve-NpcAnimation 0xa0 0
if (-not $nayruMusicNoteAnimation -or
    $interactionGraphics['160:1'].TileBase -ne 0x44 -or
    $interactionGraphics['160:1'].Palette -ne 1) {
    throw 'INTERAC_FLOATING_IMAGE $a0:$01 music-note graphics changed.'
}
$floatingImageSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\floatingImage.s')
if ($floatingImageSource -notmatch '(?s)ld b,\$03.*?ld b,\$1d' -or
    $floatingImageSource -notmatch 'ld \(hl\),SPEED_60' -or
    $floatingImageSource -notmatch 'ld \(hl\),70' -or
    $floatingImageSource -notmatch '(?s)@xOffsets:\s*\.db \$ff \$fe \$ff \$00\s*\.db \$01 \$02 \$01 \$00') {
    throw 'INTERAC_FLOATING_IMAGE $a0 movement or global-frame sway changed.'
}
$noteVelocityXFixed = [int][Math]::Truncate(
    [Math]::Sin(3 * [Math]::PI / 16) * 0x60)
$noteVelocityYFixed = [int][Math]::Truncate(
    -[Math]::Cos(3 * [Math]::PI / 16) * 0x60)
if ($noteVelocityXFixed -ne 53 -or $noteVelocityYFixed -ne -79) {
    throw 'SPEED_60 angle $03 no longer resolves to signed 8.8 velocity 53,-79.'
}
$nayruPartAnimationSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\partAnimations.s')
$nayruPartOamSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\partOamData.s')
$part27PointersMatch = [regex]::Match(
    $nayruPartAnimationSource,
    '(?ms)^part27OamDataPointers:[^\r\n]*\r?\n(?<body>(?:\s*\.dw\s+partOamData[0-9a-f]+\s*\r?\n)+)')
$part27AnimationMatch = [regex]::Match(
    $nayruPartAnimationSource,
    '(?ms)^partAnimation5b9a7:\r?\n(?<body>.*?)(?=^partAnimation[0-9a-f]+:)')
if (-not $part27PointersMatch.Success -or -not $part27AnimationMatch.Success) {
    throw 'Could not resolve PART_LIGHTNING $27 animation tables.'
}
$part27Pointers = @(
    [regex]::Matches($part27PointersMatch.Groups['body'].Value, 'partOamData[0-9a-f]+') |
        ForEach-Object { $_.Value })
function Resolve-NayruPartOam([string]$label) {
    $match = [regex]::Match(
        $script:nayruPartOamSource,
        "(?ms)^${label}:\r?\n(?<body>.*?)(?=^partOamData[0-9a-f]+:|\z)")
    if (-not $match.Success) { throw "Could not resolve $label for PART_LIGHTNING." }
    $rows = [regex]::Matches($match.Groups['body'].Value, '(?m)^\s*\.db\s+(?<bytes>[^;\r\n]+)')
    $count = [Convert]::ToInt32(
        [regex]::Match($rows[0].Groups['bytes'].Value, '\$(?<value>[0-9a-f]{2})').Groups['value'].Value,
        16)
    $blocks = [Collections.Generic.List[string]]::new()
    for ($row = 1; $row -le $count; $row++) {
        $values = [regex]::Matches($rows[$row].Groups['bytes'].Value, '\$(?<value>[0-9a-f]{2})')
        $blocks.Add(($values | Select-Object -First 4 | ForEach-Object {
            [Convert]::ToInt32($_.Groups['value'].Value, 16)
        }) -join ',')
    }
    return $blocks -join ';'
}
$part27Frames = [Collections.Generic.List[string]]::new()
$part27Duration = 0
foreach ($frame in [regex]::Matches(
    $part27AnimationMatch.Groups['body'].Value,
    '\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
    $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
    if ($parameter -eq 0xff) { break }
    $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
    $pointer = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
    $part27Frames.Add("$duration@$(Resolve-NayruPartOam $part27Pointers[$pointer])")
    $part27Duration += $duration
}
if ($part27Frames.Count -ne 9 -or $part27Duration -ne 20 -or
    $gfxNames[0xa6] -ne 'spr_projectiles_2') {
    throw 'PART_LIGHTNING $27 no longer has its original 9-frame / 20-update visual.'
}
[void]$npcSpriteNames.Add('spr_common_sprites')
[void]$npcSpriteNames.Add('spr_projectiles_2')
$nayruEffectRows = @(
    "# name`tsprite`ttile-base`tpalette`tduration`tspeed`tangle`tsway`tvelocity-x-fixed`tvelocity-y-fixed`tanimation",
    # Subid $01 loads no object graphics header: it reads fixed bank-1 OBJ
    # tile $44 from spr_common_sprites. Object header $45's similarly named
    # Z/bubble/exclamation sheet belongs to the boy and is not this VRAM bank.
    "MusicNote`tspr_common_sprites`t68`t1`t70`t0.375`t3`t1`t$noteVelocityXFixed`t$noteVelocityYFixed`t$nayruMusicNoteAnimation",
    "Lightning`tspr_projectiles_2`t14`t4`t20`t0`t0`t0`t0`t0`t$($part27Frames -join '|')"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\nayru_intro_effects.tsv'),
    $nayruEffectRows,
    [Text.UTF8Encoding]::new($false))

$nayruTextIds = @(
    0x3214, 0x5705, 0x2510, 0x5704, 0x5702, 0x5703, 0x5706,
    0x2a00, 0x1d00, 0x2a22, 0x1d22, 0x5600, 0x5606, 0x5601,
    0x5602, 0x2a01, 0x5603, 0x5604, 0x5605, 0x5607, 0x2a02,
    0x2a03, 0x2a04, 0x2a05, 0x2a06, 0x0110, 0x0112, 0x0115, 0x0117,
    0x001c
)
$nayruTextRows = [Collections.Generic.List[string]]::new()
$nayruTextRows.Add('# text-id`ttextbox-position`tutf8-base64')
foreach ($textId in $nayruTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve initial Nayru cutscene text TX_$($textId.ToString('x4'))."
    }
    $textboxPosition = if ($allTextPositions.ContainsKey($textId)) {
        $allTextPositions[$textId]
    } else { -1 }
    $message = $allTexts[$textId]
    for ($expansion = 0; $expansion -lt 4; $expansion++) {
        $reference = [regex]::Match($message, '\\(?:call|jump)\(TX_(?<id>[0-9a-f]{4})\)')
        if (-not $reference.Success) { break }
        $referencedId = [Convert]::ToInt32($reference.Groups['id'].Value, 16)
        if (-not $allTexts.ContainsKey($referencedId)) {
            throw "Could not expand initial Nayru cutscene TX_$($textId.ToString('x4')) reference TX_$($referencedId.ToString('x4'))."
        }
        $message = $message.Remove($reference.Index, $reference.Length).Insert(
            $reference.Index, $allTexts[$referencedId])
    }
    $message = $message.Replace('\sym(0x1c)', [string][char]0x266a)
    $message = $message.Replace('\sym(0x57)', [string][char]0x25b2)
    $message = [regex]::Replace($message, '\\x(?<hex>[0-9a-f]{2})', {
        param($match)
        [string][char][Convert]::ToInt32($match.Groups['hex'].Value, 16)
    })
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($message))
    $nayruTextRows.Add("$($textId.ToString('x4'))`t$textboxPosition`t$encoded")
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\nayru_intro_text.tsv'),
    $nayruTextRows,
    [Text.UTF8Encoding]::new($false))

$nayruMiscSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
$nayruObjectsSource = Get-Content -Raw (
    Join-Path $Disassembly 'objects\ages\extraData3.s')
$nayruBearSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\bear.s')
$nayruCutsceneSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$nayruScriptSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$nayruScriptHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$nayruBirdSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\bird.s')
$nayruRabbitSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\rabbitMain.s')
$nayruMonkeySource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\monkeyMain.s')
$nayruGhostSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\ghostVeran.s')
if ($nayruMiscSource -notmatch '(?ms)^interaction6b_subid01:.*?GLOBALFLAG_INTRO_DONE.*?objectData\.nayruAndAnimalsInIntro' -or
    $nayruObjectsSource -notmatch '(?ms)^nayruAndAnimalsInIntro:.*?obj_Interaction \$36 \$00 \$18 \$78.*?obj_Interaction \$4c \$00 \$2c \$48.*?obj_End' -or
    $nayruBearSource -notmatch '(?ms)cp \$60.*?cp \$3e.*?mainScripts\.bearSubid00Script_part2' -or
    $nayruCutsceneSource -notmatch '(?ms)^nayruSingingCutsceneHandler:.*?ld \(hl\),\$58\s+inc hl\s+ld \(hl\),\$02.*?paletteData44a8.*?ld \(hl\),\$3c\s+jp fadeoutToWhite.*?ld a,\$15.*?ld a,\$03\s+jp fadeinFromWhiteWithDelay' -or
    $nayruScriptSource -notmatch '(?ms)^ralphSubid00Script:.*?callscript jumpAndWaitUntilLanded.*?showtext TX_2a00.*?callscript jumpAndWaitUntilLanded.*?showtext TX_2a22.*?ralph_createLinkedSwordAnimation.*?setanimation \$04' -or
    $nayruScriptSource -notmatch '(?ms)^nayruScript00_part1:.*?wait 120.*?cfd0, \$16.*?wait 30.*?applyspeed \$81.*?wait 210.*?setanimation \$05.*?wait 60.*?cfd0, \$17' -or
    $nayruScriptSource -notmatch '(?ms)^ralphSubid00Script:.*?@faceUp:.*?wait 220.*?applyspeed \$81.*?cfd0, \$17.*?wait 120' -or
    $nayruScriptHelperSource -notmatch '(?ms)^beginJump:.*?ld \(hl\),\$00.*?ld \(hl\),\$fe.*?^updateGravity:.*?ld c,\$30' -or
    $nayruInitialSource -notmatch '(?ms)ld bc,-\$400.*?ld bc,\$3828.*?ld \(hl\),\$80.*?ld \(hl\),\$1e.*?ld bc,\$0040.*?ld c,\$20' -or
    $nayruInitialSource -notmatch '(?ms)@swayHorizontally:.*?and \$07.*?@@xOffsets:\s*\.db \$ff \$ff \$ff \$00 \$01 \$01 \$01 \$00' -or
    $nayruGhostSource -notmatch '(?ms)@substate7:.*?cp \$17.*?ghostVeranSubid1Script_part2.*?objectSetVisible80' -or
    $paletteHeaderSource -notmatch '(?ms)PALH_97.*?m_PaletteHeaderSpr\s+6,\s*2,\s*paletteData44d8') {
    throw 'Initial Nayru cutscene controller, actor list, trigger boundary, or cutscene counters changed.'
}
$nayruEventRows = @(
    '# group`troom`tintro-flag`tcompletion-room-flag`tbear-room-flag`ttrigger-x`ttrigger-y`tbear-delay`tpost-bear-text`tsinging-frames`tskip-window`tsprite-scroll-period`tsprite-scroll-steps`tpossession-fade-hold`tportal-position`tportal-tile`tvignette-count`tnpc-jump-speed-z`tnpc-jump-gravity`tdark-fade-frames`twhite-fade-out-frames`twhite-fade-in-frames`tnayru-ascent-speed-z`tnayru-transfer-z`tnayru-landing-delay`tnayru-fall-speed-z`tnayru-fall-gravity',
    "0`t39`t0a`t40`t80`t96`t62`t120`t30`t600`t240`t8`t40`t60`t22`td7`t3`t-512`t48`t32`t32`t97`t-1024`t-32768`t30`t64`t32"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\nayru_intro_event.tsv'),
    $nayruEventRows,
    [Text.UTF8Encoding]::new($false))

# The five audience interactions respond independently to controller signal
# $10. Preserve their counters, cardinal speeds, jump Z speeds/gravity, repeat
# rules, and animation selections instead of treating the escape as one tween.
if ($nayruBearSource -notmatch '(?ms)cp \$10.*?ld \(hl\),40.*?ld \(hl\),\$02.*?SPEED_100.*?ld a,\$01' -or
    $nayruMonkeySource -notmatch '(?ms)cp \$10.*?ld \(hl\),\$32.*?ld a,\$03.*?monkeyJumpSpeed120.*?ld \(hl\),\$02.*?SPEED_180.*?monkeyJumpSpeed100' -or
    $nayruRabbitSource -notmatch '(?ms)cp \$10.*?ld \(hl\),40.*?ld \(hl\),\$06.*?SPEED_180.*?ld bc,-\$200.*?ld a,\$04' -or
    $boyInitialSource -notmatch '(?ms)cp \$10.*?ld bc,-\$180.*?ld \(hl\),\$02.*?SPEED_180' -or
    $nayruBirdSource -notmatch '(?ms)cp \$10.*?ld \(hl\),\$1e.*?bird_hop.*?ld a,\$02.*?ld \(hl\),\$01.*?SPEED_100.*?ld bc,-\$100.*?ld a,\$03' -or
    $nayruBirdSource -notmatch '(?ms)^bird_updateGravityAndHopWhenHitGround:.*?ld c,\$20.*?^bird_hop:.*?ld bc,-\$c0') {
    throw 'An initial Nayru audience escape counter, speed, jump, or animation changed.'
}
$nayruFleeRows = @(
    '# actor`tdelay`tangle`tspeed`twait-jump-speed-z`twait-gravity`trepeat-wait-jump`tescape-jump-speed-z`tescape-gravity`trepeat-escape-jump`twait-for-landing`twait-animation`tescape-animation',
    "Bear`t40`t2`t1.0`t0`t0`t0`t0`t0`t0`t0`t2`t1",
    "Monkey`t50`t2`t1.5`t-288`t32`t1`t-256`t32`t1`t0`t3`t4",
    "Rabbit`t40`t6`t1.5`t0`t0`t0`t-512`t32`t1`t0`t2`t4",
    "Boy`t0`t2`t1.5`t-384`t32`t0`t0`t0`t0`t1`t2`t0",
    "Bird`t30`t1`t1.0`t-192`t32`t1`t-256`t0`t0`t0`t2`t3"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\nayru_intro_flee.tsv'),
    $nayruFleeRows,
    [Text.UTF8Encoding]::new($false))

# State 4 blends BG palettes 2-7 into paletteData44a8 before PALH_99 is
# installed. Export its six exact palettes for the 32-update runtime blend.
Export-PaletteBlock 'paletteData44a8' 24 'cutscenes\nayru_intro_dark_bg_palette.bin'
# Following possessed Impa leaves PALH_97's two palettes in OBJ slots 6-7.
# Nayru alternates her ordinary slot 1 with slot 6 while possession takes hold.
Export-PaletteBlock 'paletteData44d8' 4 'cutscenes\nayru_possessed_sprite_palette.bin'
# PALH_a2 / PALH_ad install paletteData44e8 in OBJ slot 6 when the
# vignette actors are petrified.
Export-PaletteBlock 'paletteData44e8' 4 'cutscenes\nayru_stone_sprite_palette.bin'

# GFXH_NAYRU_SINGING_CUTSCENE and PALH_95 provide the full-screen prologue
# still. The sprite layer is the exact 39-entry bank3f.oamData_7249 list.
foreach ($asset in @(
    @{ Source = 'gfx_compressible\ages\spr_nayru_singing_cutscene.png'; Destination = 'cutscenes\spr_nayru_singing_cutscene.png' },
    @{ Source = 'gfx_compressible\ages\gfx_nayru_singing_cutscene_1.png'; Destination = 'cutscenes\gfx_nayru_singing_cutscene_1.png' },
    @{ Source = 'gfx_compressible\ages\gfx_nayru_singing_cutscene_2.png'; Destination = 'cutscenes\gfx_nayru_singing_cutscene_2.png' },
    @{ Source = 'gfx_compressible\ages\gfx_nayru_singing_cutscene_3.png'; Destination = 'cutscenes\gfx_nayru_singing_cutscene_3.png' },
    @{ Source = 'gfx_compressible\ages\map_nayru_singing_cutscene.bin'; Destination = 'cutscenes\map_nayru_singing_cutscene.bin' },
    @{ Source = 'gfx_compressible\ages\flg_nayru_singing_cutscene.bin'; Destination = 'cutscenes\flags_nayru_singing_cutscene.bin' }
)) { Copy-GeneratedFile $asset.Source $asset.Destination }
Export-PaletteBlock 'paletteData4430' 32 'cutscenes\nayru_singing_bg_palette.bin'
Export-PaletteBlock 'paletteData4470' 28 'cutscenes\nayru_singing_sprite_palette.bin'
$agesBank3f = Get-Content -Raw (Join-Path $Disassembly 'ages.s')
$nayruOamBlock = [regex]::Match(
    $agesBank3f,
    '(?ms)^oamData_7249:\s*\.db \$(?<count>[0-9a-f]{2})(?<body>.*?)(?=^\s*$)')
if (-not $nayruOamBlock.Success -or
    [Convert]::ToInt32($nayruOamBlock.Groups['count'].Value, 16) -ne 39) {
    throw 'Could not resolve the 39-entry Nayru singing OAM list.'
}
$nayruOamRows = [Collections.Generic.List[string]]::new()
$nayruOamRows.Add('# y`tx`ttile`tflags')
foreach ($entry in [regex]::Matches(
    $nayruOamBlock.Groups['body'].Value,
    '\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})')) {
    $nayruOamRows.Add(
        "$($entry.Groups['y'].Value)`t$($entry.Groups['x'].Value)`t$($entry.Groups['tile'].Value)`t$($entry.Groups['flags'].Value)")
}
if ($nayruOamRows.Count -ne 40) {
    throw "Expected 39 Nayru singing OAM entries, got $($nayruOamRows.Count - 1)."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\nayru_singing_oam.tsv'),
    $nayruOamRows,
    [Text.UTF8Encoding]::new($false))

# Impa switches to the separate collapsed sheet when Veran leaves her body.
Copy-GeneratedFile 'gfx_compressible\ages\spr_impafainted.png' 'gfx\spr_impafainted.png'

# Copy every sprite sheet referenced by the extracted NPC records. The source
# keeps common and Ages graphics in separate directories, so search both.
foreach ($spriteName in $npcSpriteNames) {
    $sourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
        ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$spriteName.png" } |
        Select-Object -First 1
    if ($null -eq $sourceSprite) { throw "NPC sprite not found in disassembly: $spriteName.png" }
    $targetSprite = Join-Path $destination "gfx\$spriteName.png"
    Copy-Item -LiteralPath $sourceSprite.FullName -Destination $targetSprite -Force
}
$npcPath = Join-Path $destination "objects\npcs.tsv"
[IO.File]::WriteAllLines($npcPath, $npcRows, [Text.UTF8Encoding]::new($false))
$vasuShopTextPath = Join-Path $destination "objects\vasu_shop_texts.tsv"
[IO.File]::WriteAllLines(
    $vasuShopTextPath,
    $vasuShopTextRows,
    [Text.UTF8Encoding]::new($false))
$vasuShopAnimationPath = Join-Path $destination "objects\vasu_shop_animations.tsv"
[IO.File]::WriteAllLines(
    $vasuShopAnimationPath,
    $vasuShopAnimationRows,
    [Text.UTF8Encoding]::new($false))
$vasuShopConstantsPath = Join-Path $destination "objects\vasu_shop_constants.tsv"
[IO.File]::WriteAllLines(
    $vasuShopConstantsPath,
    $vasuShopConstantRows,
    [Text.UTF8Encoding]::new($false))
$lynnaShopItemPath = Join-Path $destination "objects\lynna_shop_items.tsv"
[IO.File]::WriteAllLines(
    $lynnaShopItemPath,
    $lynnaShopItemRows,
    [Text.UTF8Encoding]::new($false))
$lynnaShopTextPath = Join-Path $destination "objects\lynna_shop_texts.tsv"
[IO.File]::WriteAllLines(
    $lynnaShopTextPath,
    $lynnaShopTextRows,
    [Text.UTF8Encoding]::new($false))
$lynnaShopAnimationPath = Join-Path $destination "objects\lynna_shop_animations.tsv"
[IO.File]::WriteAllLines(
    $lynnaShopAnimationPath,
    $lynnaShopAnimationRows,
    [Text.UTF8Encoding]::new($false))
$lynnaShopConstantsPath = Join-Path $destination "objects\lynna_shop_constants.tsv"
[IO.File]::WriteAllLines(
    $lynnaShopConstantsPath,
    $lynnaShopConstantRows,
    [Text.UTF8Encoding]::new($false))
$dungeonMechanicPath = Join-Path $destination "objects\dungeon_mechanics.tsv"
[IO.File]::WriteAllLines(
    $dungeonMechanicPath,
    $dungeonMechanicRows,
    [Text.UTF8Encoding]::new($false))
$dungeonMechanicConstantsPath = Join-Path $destination "objects\dungeon_mechanic_constants.tsv"
[IO.File]::WriteAllLines(
    $dungeonMechanicConstantsPath,
    $dungeonMechanicConstantRows,
    [Text.UTF8Encoding]::new($false))
$puzzlePuffPath = Join-Path $destination "effects\puzzle_puff.tsv"
New-Item -ItemType Directory -Force -Path (Split-Path $puzzlePuffPath -Parent) | Out-Null
[IO.File]::WriteAllLines(
    $puzzlePuffPath,
    $puzzlePuffRows,
    [Text.UTF8Encoding]::new($false))
$fallDownHolePath = Join-Path $destination "effects\fall_down_hole.tsv"
[IO.File]::WriteAllLines(
    $fallDownHolePath,
    $fallDownHoleRows,
    [Text.UTF8Encoding]::new($false))
$keyDoorPath = Join-Path $destination "objects\dungeon_key_doors.tsv"
[IO.File]::WriteAllLines(
    $keyDoorPath,
    $keyDoorRows,
    [Text.UTF8Encoding]::new($false))
$standardTilePath = Join-Path $destination "metadata\standard_tile_substitutions.tsv"
[IO.File]::WriteAllLines(
    $standardTilePath,
    $standardTileRows,
    [Text.UTF8Encoding]::new($false))
$treasureObjectVisualPath = Join-Path $destination "metadata\treasure_object_visuals.tsv"
[IO.File]::WriteAllLines(
    $treasureObjectVisualPath,
    $treasureObjectVisualRows,
    [Text.UTF8Encoding]::new($false))
$familyNpcPath = Join-Path $destination "objects\bipin_blossom_family.tsv"
[IO.File]::WriteAllLines(
    $familyNpcPath,
    $familyRows,
    [Text.UTF8Encoding]::new($false))
$familyTextPath = Join-Path $destination "objects\bipin_blossom_family_texts.tsv"
[IO.File]::WriteAllLines(
    $familyTextPath,
    $familyTextRows,
    [Text.UTF8Encoding]::new($false))
$room148PickaxePath = Join-Path $destination "objects\room148_pickaxe.tsv"
[IO.File]::WriteAllLines(
    $room148PickaxePath,
    $room148PickaxeRows,
    [Text.UTF8Encoding]::new($false))
$blackTowerTextPath = Join-Path $destination "objects\black_tower_texts.tsv"
[IO.File]::WriteAllLines(
    $blackTowerTextPath,
    $blackTowerTextRows,
    [Text.UTF8Encoding]::new($false))
$blackTowerVisualPath = Join-Path $destination "objects\black_tower_visuals.tsv"
[IO.File]::WriteAllLines(
    $blackTowerVisualPath,
    $blackTowerVisualRows,
    [Text.UTF8Encoding]::new($false))
$blackTowerPatrolPath = Join-Path $destination "objects\black_tower_patrols.tsv"
[IO.File]::WriteAllLines(
    $blackTowerPatrolPath,
    $blackTowerPatrolRows,
    [Text.UTF8Encoding]::new($false))
$blackTowerConstantsPath = Join-Path $destination "objects\black_tower_constants.tsv"
[IO.File]::WriteAllLines(
    $blackTowerConstantsPath,
    $blackTowerConstantsRows,
    [Text.UTF8Encoding]::new($false))
$room149VisualPath = Join-Path $destination "objects\room149_family_visuals.tsv"
[IO.File]::WriteAllLines(
    $room149VisualPath,
    $room149VisualRows,
    [Text.UTF8Encoding]::new($false))
$room149TextPath = Join-Path $destination "objects\room149_family_texts.tsv"
[IO.File]::WriteAllLines(
    $room149TextPath,
    $room149TextRows,
    [Text.UTF8Encoding]::new($false))
