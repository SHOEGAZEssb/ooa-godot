# Resolve the first dialogue command reached by a script label, following the
# common scriptjump indirection used by generic NPCs.
$scriptSources = @(
    (Join-Path $Disassembly "scripts\ages\scripts.s"),
    (Join-Path $Disassembly "scripts\ages\scriptHelper.s")
)
$scriptBodies = @{}
foreach ($scriptSourcePath in $scriptSources) {
    $fileBodies = @{}
    foreach ($node in Read-AssemblyNodes $scriptSourcePath) {
        $label = [string]$node.EnclosingGlobalLabel
        if ([string]::IsNullOrEmpty($label)) { continue }
        if (-not $fileBodies.ContainsKey($label)) {
            $fileBodies[$label] = [Collections.Generic.List[object]]::new()
        }
        $fileBodies[$label].Add($node)
    }
    foreach ($label in $fileBodies.Keys) {
        $scriptBodies[$label] = @($fileBodies[$label])
    }
}
$scriptTextCache = @{}
function Resolve-ScriptTextId([string]$label, [Collections.Generic.HashSet[string]]$visited) {
    if ($scriptTextCache.ContainsKey($label)) { return $scriptTextCache[$label] }
    if ($visited.Contains($label) -or -not $scriptBodies.ContainsKey($label)) { return -1 }
    [void]$visited.Add($label)
    $textCommand = @($scriptBodies[$label] | Where-Object {
        $_.Name -in @(
            'rungenericnpc', 'rungenericnpclowindex', 'showtext',
            'showtextlowindex', 'settextid') -and
        $_.OperandText -match '^(?:<)?TX_(?<id>[0-9a-f]{4})'
    } | Select-Object -First 1)
    if ($textCommand.Count -ne 0) {
        [void]($textCommand[0].OperandText -match
            '^(?:<)?TX_(?<id>[0-9a-f]{4})')
        $value = [Convert]::ToInt32($Matches['id'], 16)
        $scriptTextCache[$label] = $value
        return $value
    }
    $jump = @($scriptBodies[$label] | Where-Object {
        $_.Name -eq 'scriptjump'
    } | Select-Object -First 1)
    if ($jump.Count -ne 0 -and
        $jump[0].OperandText -match
            '^(?:mainScripts\.)?(?<label>[A-Za-z0-9_@]+)') {
        $value = Resolve-ScriptTextId $Matches['label'] $visited
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
    $interactionNodes = @(Read-AssemblyNodes $interactionSourcePath.FullName)
    # A few large interactions keep only a jpab trampoline in their primary
    # file and put the implementation in interactionCodeXX_body (for example,
    # monkeyMain.s). Treat that body as the same interaction so its exact
    # subid script references can resolve dialogue too.
    $codeLabel = @($interactionNodes | Where-Object {
        $_.Kind -eq 'Label' -and
        $_.Name -match '^interactionCode(?<id>[0-9a-f]{2})(?:_body)?$'
    } | Select-Object -First 1)
    if ($codeLabel.Count -eq 0) { continue }
    [void]($codeLabel[0].Name -match
        '^interactionCode(?<id>[0-9a-f]{2})(?:_body)?$')
    $interactionId = [Convert]::ToInt32($Matches['id'], 16)
    if (-not $npcInteractionIds.Contains($interactionId)) { continue }
    if ($interactionNodes | Where-Object {
        $_.Name -eq 'npcFaceLinkAndAnimate' -or
        $_.Operands -contains 'npcFaceLinkAndAnimate'
    } | Select-Object -First 1) {
        [void]$npcFacingIds.Add($interactionId)
    }
    $tableName = ''
    $tableIndex = 0
    foreach ($node in $interactionNodes) {
        if ($node.Kind -eq 'Label' -and
            $node.Name -match '^@(?<table>[A-Za-z0-9_]+ScriptTable)$') {
            $tableName = $Matches['table']
            $tableIndex = 0
            continue
        }
        if (-not $tableName) { continue }
        if ($node.Kind -eq 'Label' -and
            -not $node.Name.StartsWith('@')) {
            $tableName = ''
            continue
        }
        if ($node.Kind -ne 'Data' -or $node.Name -ine '.dw' -or
            $node.Operands.Count -eq 0 -or
            $node.Operands[0] -notmatch
                '^mainScripts\.(?<label>[A-Za-z0-9_@]+)$') {
            continue
        }
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
    foreach ($node in $interactionNodes) {
        foreach ($operand in $node.Operands) {
            if ($operand -notmatch
                'mainScripts\.(?<label>[A-Za-z0-9_@]+)') {
                continue
            }
            $label = $Matches['label']
            $textId = Resolve-ScriptTextId `
                $label ([Collections.Generic.HashSet[string]]::new())
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
                if (-not $npcTextBySubid.ContainsKey($key)) {
                    $npcTextBySubid[$key] = $textId
                }
            }
        }
    }
}

# Room 1:83's INTERAC_MISC_MAN $41:$00 uses the first entry in
# miscMan.s:@scriptTable, but the generic table-name heuristic does not infer
# the subid from the non-subid-qualified manOutsideD2Script label. Pin the
# complete native initializer and its fixed generic-NPC text explicitly.
$room183MiscManSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscMan.s')
if ($room183MiscManSource -notmatch
        '(?ms)^@subid0:\s+call checkInteractionState\s+jr nz,\+\+\s+ld a,GLOBALFLAG_FINISHEDGAME\s+call checkGlobalFlag\s+jp nz,interactionDelete\s+ld a,GLOBALFLAG_0b\s+call checkGlobalFlag\s+jp nz,interactionDelete\s+call @initGraphicsIncStateAndLoadScript\s+\+\+\s+call interactionRunScript\s+jp npcFaceLinkAndAnimate' -or
    $room183MiscManSource -notmatch
        '(?ms)^@scriptTable:\s+\.dw mainScripts\.manOutsideD2Script' -or
    (Resolve-ScriptTextId 'manOutsideD2Script' (
        [Collections.Generic.HashSet[string]]::new())) -ne 0x2606 -or
    -not $allTexts.ContainsKey(0x2606)) {
    throw 'Room 1:83 misc-man initializer or TX_2606 script changed.'
}
$npcTextBySubid['65:0'] = 0x2606

# linkedGameNpcScript derives its initial text as TX_4d00 + var3f*5. Resolve
# the two old-lady secret subids from that shared formula instead of leaving
# them with text ID $0000 merely because the script uses showloadedtext.
$linkedNpcScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$oldLadyInteractionSource = Read-ImportText (
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
$interactionDataPath = Join-Path $Disassembly "data\ages\interactionData.s"
$interactionGraphics = @{}
$interactionPointers = @{}
$interactionDataNodes = @(Read-AssemblyNodes $interactionDataPath)
foreach ($node in Read-AssemblyMacroInvocations `
    $interactionDataPath '' 'm_InteractionData') {
    if ($node.Comment -notmatch '^\$(?<id>[0-9a-f]{2})') {
        throw "$($node.Path):$($node.Line): interaction-data row has no ID comment."
    }
    $id = [Convert]::ToInt32($Matches['id'], 16)
    if ($node.Operands[0].StartsWith('$')) {
        if ($node.Operands.Count -ne 3) {
            throw "$($node.Path):$($node.Line): malformed direct interaction-data row."
        }
        $flags = [Convert]::ToInt32($node.Operands[2].Substring(1), 16)
        $interactionGraphics["$id`:0"] = @{
            Gfx = [Convert]::ToInt32($node.Operands[0].Substring(1), 16)
            TileBase = [Convert]::ToInt32($node.Operands[1].Substring(1), 16)
            Flags = $flags
            Palette = ($flags -shr 4) -band 7
            DefaultAnimation = $flags -band 15
        }
    } else {
        $interactionPointers[$id] = $node.Operands[0]
    }
}

# Each subid label begins a view at its current entry. This preserves stacked
# aliases and aliases embedded partway through a larger table.
$subidAliases = [Collections.Generic.List[object]]::new()
$subidEntries = [Collections.Generic.List[object]]::new()
$interactionSubidCounts = @{}
foreach ($node in $interactionDataNodes) {
    if ($node.Kind -eq 'Label' -and
        $node.Name -match '^interaction(?<id>[0-9a-f]{2})SubidData$') {
        $subidAliases.Add([pscustomobject]@{
            Id = [Convert]::ToInt32($Matches['id'], 16)
            Start = $subidEntries.Count
        })
        continue
    }
    if ($node.Kind -eq 'MacroInvocation' -and
        $node.Name -eq 'm_InteractionSubidData') {
        if ($node.Operands.Count -ne 3) {
            throw "$($node.Path):$($node.Line): malformed interaction subid row."
        }
        $flags = [Convert]::ToInt32($node.Operands[2].Substring(1), 16)
        $subidEntries.Add(@{
            Gfx = [Convert]::ToInt32($node.Operands[0].Substring(1), 16)
            TileBase = [Convert]::ToInt32($node.Operands[1].Substring(1), 16)
            Flags = $flags
            Palette = ($flags -shr 4) -band 7
            DefaultAnimation = $flags -band 15
        })
        continue
    }
    if ($node.Kind -eq 'MacroInvocation' -and
        $node.Name -eq 'm_InteractionSubidDataEnd') {
        foreach ($alias in $subidAliases) {
            $count = $subidEntries.Count - $alias.Start
            $interactionSubidCounts[$alias.Id] = $count
            for ($index = 0; $index -lt $count; $index++) {
                $interactionGraphics["$($alias.Id)`:$index"] =
                    $subidEntries[$alias.Start + $index]
            }
        }
        $subidAliases.Clear()
        $subidEntries.Clear()
    }
}
if ($subidAliases.Count -ne 0 -or $subidEntries.Count -ne 0) {
    throw 'Interaction subid data ended without m_InteractionSubidDataEnd.'
}
if ($interactionSubidCounts[0x60] -ne 0x83) {
    throw "Expected 131 INTERAC_TREASURE subid graphics, parsed " +
        "$($interactionSubidCounts[0x60])."
}
$gfxNames = @{}
foreach ($line in Read-ImportLines (Join-Path $Disassembly "data\ages\objectGfxHeaders.s")) {
    if ($line -match '/\* \$(?<id>[0-9a-f]{2}) \*/ m_ObjectGfxHeader (?<name>[A-Za-z0-9_]+)') {
        $gfxNames[[Convert]::ToInt32($Matches['id'], 16)] = $Matches['name']
    }
}

# Resolve animation indices through the original pointer tables. Animation
# frame byte 1 is a byte offset into the interaction's OAM pointer table (the
# engine adds it directly before reading a word), not a sprite-sheet column.
$interactionAnimationPath =
    Join-Path $Disassembly "data\ages\interactionAnimations.s"
$interactionAnimationSource = Read-ImportText $interactionAnimationPath
$npcAnimationTables = Read-AssemblyDwTables $interactionAnimationPath 'interaction[0-9a-f]{2}Animations' 'interactionAnimation[0-9a-f]+'
# Keep the per-label tables as an exported stage result for strict consumers;
# the contiguous stream below additionally supports original cross-label offsets.
$npcOamPointerTables = Read-AssemblyDwTables $interactionAnimationPath 'interaction[0-9a-f]{2}OamDataPointers' 'interactionOamData[0-9a-f]+'
$interactionAnimationNodes = @(Read-AssemblyNodes $interactionAnimationPath)
$npcOamPointerStarts = @{}
$npcOamPointers = [Collections.Generic.List[string]]::new()
foreach ($node in $interactionAnimationNodes) {
    if ($node.Kind -eq 'Label' -and
        $node.Name -match '^interaction[0-9a-f]{2}OamDataPointers$') {
        # These labels are views into one contiguous pointer stream. An
        # animation may index past the next label; INTERAC_ACCESSORY animation
        # $03 does this to reach its two-cell held-item OAM composition.
        $npcOamPointerStarts[$node.Name] = $npcOamPointers.Count
        continue
    }
    if ($node.Kind -eq 'Data' -and $node.Name -ieq '.dw' -and
        $node.Operands.Count -eq 1 -and
        $node.Operands[0] -match '^interactionOamData[0-9a-f]+$') {
        $npcOamPointers.Add($node.Operands[0])
    }
}
$npcAnimationDefinitions = Read-AssemblyAnimationDefinitions `
    $interactionAnimationPath 'interactionAnimation[0-9a-f]+(?:Loop)?'

$npcOamBlocks = @{}
$interactionOamPath =
    Join-Path $Disassembly "data\ages\interactionOamData.s"
$interactionOamNodes = @(Read-AssemblyNodes $interactionOamPath)
$oamDataByLabel = @{}
foreach ($node in $interactionOamNodes) {
    $label = $node.EnclosingGlobalLabel
    if ($node.Kind -ne 'Data' -or $node.Name -ine '.db' -or
        $label -notmatch '^interactionOamData[0-9a-f]+$') { continue }
    if (-not $oamDataByLabel.ContainsKey($label)) {
        $oamDataByLabel[$label] = [Collections.Generic.List[object]]::new()
    }
    $oamDataByLabel[$label].Add($node)
}
foreach ($label in $oamDataByLabel.Keys) {
    $dataLines = $oamDataByLabel[$label]
    if ($dataLines.Count -eq 0) { continue }
    $count = Convert-AssemblyInteger $dataLines[0].Operands[0]
    $blocks = [Collections.Generic.List[string]]::new()
    for ($index = 1; $index -le $count -and $index -lt $dataLines.Count; $index++) {
        if ($dataLines[$index].Operands.Count -lt 4) { continue }
        $blocks.Add(($dataLines[$index].Operands |
            Select-Object -First 4 | ForEach-Object {
            Convert-AssemblyInteger $_
        }) -join ',')
    }
    $npcOamBlocks[$label] = $blocks -join ';'
}

function Resolve-NpcAnimation([int]$interactionId, [int]$animationIndex) {
    $hex = $interactionId.ToString('x2')
    $animationKey = "interaction${hex}Animations"
    $pointerKey = "interaction${hex}OamDataPointers"
    if (-not $npcAnimationTables.ContainsKey($animationKey) -or
        -not $npcOamPointerStarts.ContainsKey($pointerKey)) { return '' }
    $animations = $npcAnimationTables[$animationKey]
    if ($animationIndex -lt 0 -or $animationIndex -ge $animations.Count) { return '' }
    $animationLabel = $animations[$animationIndex]
    if (-not $npcAnimationDefinitions.ContainsKey($animationLabel)) { return '' }
    $definition = $npcAnimationDefinitions[$animationLabel]
    $pointerStart = $npcOamPointerStarts[$pointerKey]
    $resolvedFrames = [Collections.Generic.List[string]]::new()
    foreach ($frame in $definition.Frames) {
        $pointerIndex = [int]($frame.PointerOffset / 2)
        $absolutePointerIndex = $pointerStart + $pointerIndex
        if ($pointerIndex -lt 0 -or
            $absolutePointerIndex -ge $npcOamPointers.Count) { continue }
        $oamLabel = $npcOamPointers[$absolutePointerIndex]
        $oam = if ($npcOamBlocks.ContainsKey($oamLabel)) { $npcOamBlocks[$oamLabel] } else { '' }
        $metadata = if ([int]$frame.Parameter -eq 0) {
            "$($frame.Duration)"
        } else {
            "$($frame.Duration),$($frame.Parameter)"
        }
        $resolvedFrames.Add("$metadata@$oam")
    }
    $encoded = $resolvedFrames -join '|'
    $loopStart = $definition.LoopStart
    if ($loopStart -gt 0) {
        $encoded += "~$loopStart"
    }
    return $encoded
}

# PART_TINGLE_BALLOON uses the same object-gfx sheet as Tingle, but its
# animation and OAM pointer domains are the part tables. Resolve that domain
# with the same source-order rules as positioned interaction animations.
$partAnimationPath =
    Join-Path $Disassembly "data\ages\partAnimations.s"
$partAnimationTables = Read-AssemblyDwTables `
    $partAnimationPath 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$partOamPointerTables = Read-AssemblyDwTables `
    $partAnimationPath 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$partAnimationDefinitions = Read-AssemblyAnimationDefinitions `
    $partAnimationPath 'partAnimation[0-9a-f]+(?:Loop)?'
$partOamBlocks = @{}
$partOamPath = Join-Path $Disassembly "data\ages\partOamData.s"
$partOamNodes = @(Read-AssemblyNodes $partOamPath)
$partOamDataByLabel = @{}
foreach ($node in $partOamNodes) {
    $label = $node.EnclosingGlobalLabel
    if ($node.Kind -ne 'Data' -or $node.Name -ine '.db' -or
        $label -notmatch '^partOamData[0-9a-f]+$') { continue }
    if (-not $partOamDataByLabel.ContainsKey($label)) {
        $partOamDataByLabel[$label] = [Collections.Generic.List[object]]::new()
    }
    $partOamDataByLabel[$label].Add($node)
}
foreach ($label in $partOamDataByLabel.Keys) {
    $dataLines = $partOamDataByLabel[$label]
    if ($dataLines.Count -eq 0) { continue }
    $count = Convert-AssemblyInteger $dataLines[0].Operands[0]
    $blocks = [Collections.Generic.List[string]]::new()
    for ($index = 1; $index -le $count -and $index -lt $dataLines.Count; $index++) {
        if ($dataLines[$index].Operands.Count -lt 4) { continue }
        $blocks.Add(($dataLines[$index].Operands |
            Select-Object -First 4 | ForEach-Object {
            Convert-AssemblyInteger $_
        }) -join ',')
    }
    $partOamBlocks[$label] = $blocks -join ';'
}

function Resolve-PartAnimation([int]$partId, [int]$animationIndex) {
    $hex = $partId.ToString('x2')
    $animationKey = "part${hex}Animations"
    $pointerKey = "part${hex}OamDataPointers"
    if (-not $partAnimationTables.ContainsKey($animationKey) -or
        -not $partOamPointerTables.ContainsKey($pointerKey)) { return '' }
    $animations = $partAnimationTables[$animationKey]
    if ($animationIndex -lt 0 -or $animationIndex -ge $animations.Count) { return '' }
    $animationLabel = $animations[$animationIndex]
    if (-not $partAnimationDefinitions.ContainsKey($animationLabel)) { return '' }
    $definition = $partAnimationDefinitions[$animationLabel]
    $pointers = $partOamPointerTables[$pointerKey]
    $resolvedFrames = [Collections.Generic.List[string]]::new()
    foreach ($frame in $definition.Frames) {
        $pointerIndex = [int]($frame.PointerOffset / 2)
        if ($pointerIndex -lt 0 -or $pointerIndex -ge $pointers.Count) { continue }
        $oamLabel = $pointers[$pointerIndex]
        $oam = if ($partOamBlocks.ContainsKey($oamLabel)) {
            $partOamBlocks[$oamLabel]
        } else { '' }
        $metadata = if ([int]$frame.Parameter -eq 0) {
            "$($frame.Duration)"
        } else {
            "$($frame.Duration),$($frame.Parameter)"
        }
        $resolvedFrames.Add("$metadata@$oam")
    }
    $encoded = $resolvedFrames -join '|'
    if ($definition.LoopStart -gt 0) {
        $encoded += "~$($definition.LoopStart)"
    }
    return $encoded
}

# The shared INTERAC_TREASURE OAM pointer base intentionally indexes through
# the following labeled pointer tables for several common animation frames.
# Preserve that contiguous ROM layout instead of truncating at the next label.
$treasureOamPointerBase = @($interactionAnimationNodes | Where-Object {
    $_.Kind -eq 'Label' -and $_.Name -eq 'interaction60OamDataPointers'
})
if ($treasureOamPointerBase.Count -ne 1) {
    throw 'Could not resolve the INTERAC_TREASURE OAM pointer base.'
}
$treasureOamPointers = @($interactionAnimationNodes | Where-Object {
    $_.Kind -eq 'Data' -and $_.Name -ieq '.dw' -and
    $_.Offset -gt $treasureOamPointerBase[0].Offset -and
    $_.Operands.Count -gt 0 -and
    $_.Operands[0] -match '^interactionOamData[0-9a-f]+$'
} | ForEach-Object { $_.Operands[0] })
function Resolve-TreasureAnimation([int]$animationIndex) {
    $animations = $npcAnimationTables['interaction60Animations']
    if ($animationIndex -lt 0 -or $animationIndex -ge $animations.Count) { return '' }
    $animationLabel = $animations[$animationIndex]
    if (-not $npcAnimationDefinitions.ContainsKey($animationLabel)) { return '' }
    $definition = $npcAnimationDefinitions[$animationLabel]
    $resolvedFrames = [Collections.Generic.List[string]]::new()
    foreach ($frame in $definition.Frames) {
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
    $loopStart = $definition.LoopStart
    if ($loopStart -gt 0) { $encoded += "~$loopStart" }
    return $encoded
}

# The graphics record supplies the animation used before interaction state 0
# runs. Interactions which immediately call interactionSetAnimation need that
# exact initialized index in the runtime record. Parse these overrides from
# their implementation instead of treating the graphics default as final.
$npcInitialAnimationBySubid = @{}
$npcInitialAnimationBySubid['65:0'] = 2
$monkeyMainSource = Read-ImportText (
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

# INTERAC_BIPIN $28:$0a is the past-era one-time Gasha Seed giver in room
# 3:fc. Its native state 0 selects animation $09, while the loaded helper
# script installs collisions and owns the complete TX $4311/$4312/$4313,
# room-item-bit, and TREASURE_GASHA_SEED $08 sequence.
$pastBipinSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\bipin.s')
$pastBipinScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
if ($pastBipinSource -notmatch '(?ms)^@bipin3:.*?ld a,\$09\s+call interactionSetAnimation.*?^@runScriptAndAnimate:\s+call interactionRunScript\s+jp @updateAnimation.*?^@updateCollisionAndVisibility:\s+call objectPreventLinkFromPassing\s+jp objectSetPriorityRelativeToLink_withTerrainEffects' -or
    $pastBipinScriptSource -notmatch '(?ms)^bipinScript3:\s+initcollisions.*?enableinput\s+checkabutton\s+disableinput\s+jumpifroomflagset \$20, @alreadyGaveSeed\s+showtext TX_4311\s+giveitem TREASURE_GASHA_SEED, \$08\s+wait 1\s+checktext\s+showtext TX_4312.*?^@alreadyGaveSeed:\s+showtext TX_4313' -or
    -not $allTexts.ContainsKey(0x4311) -or
    -not $allTexts.ContainsKey(0x4312) -or
    -not $allTexts.ContainsKey(0x4313)) {
    throw 'Past Bipin $28:$0a animation or Gasha Seed script changed in the disassembly.'
}
$npcInitialAnimationBySubid['40:10'] = 9
$npcTextBySubid['40:10'] = 0x4311

# Room 2:2f's INTERAC_POSTMAN $55:$00 installs postmanScript, whose first
# A-button branch opens TX_0b03. The native tail uses npcFaceLinkAndAnimate
# until the accepted trade changes Interaction.var3f, so retain all four
# facing animations in the placed NPC record.
$postmanSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\postman.s')
$postmanScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
if ($postmanSource -notmatch
        '(?ms)^interactionCode55:.*?interactionRunScript.*?Interaction\.var3f.*?npcFaceLinkAndAnimate.*?interactionAnimateBasedOnSpeed.*?objectSetPriorityRelativeToLink_withTerrainEffects' -or
    $postmanScriptSource -notmatch
        '(?ms)^postmanScript:.*?showtextlowindex <TX_0b03.*?jumpiftradeitemeq TRADEITEM_POE_CLOCK.*?showtextlowindex <TX_0b04.*?showtextlowindex <TX_0b05.*?giveitem TREASURE_TRADEITEM, \$01' -or
    -not $allTexts.ContainsKey(0x0b03)) {
    throw 'Room 2:2f INTERAC_POSTMAN $55:$00 native update or script entry changed.'
}
$npcTextBySubid['85:0'] = 0x0b03

# Room 1:75 contains the pre-Black Tower ensemble and two var03-selected
# hardhat workers. Pin the initial animation writes performed by the linked
# Impa/Nayru initializers; their script lanes use all four facing animations.
$preBlackTowerImpaSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\impaInCutscene.s')
$preBlackTowerNayruSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')
$preBlackTowerHardhatSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\hardhatWorker.s')
$preBlackTowerScriptsSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$preBlackTowerScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$blackTowerProgressSource = Read-ImportText (
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
$vasuSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\vasu.s')
if ($vasuSource -notmatch '(?ms)ld e,Interaction\.subid\s+ld a,\(de\)\s+or a\s+jr z,@@initVasu\s+^@@initSnake:.*?ld a,\(de\)\s+call interactionSetAnimation') {
    throw 'INTERAC_VASU snake initialization changed in the disassembly.'
}
$npcInitialAnimationBySubid['137:1'] = 1
$npcInitialAnimationBySubid['137:6'] = 6

# Room 1:57's female villager overwrites the palette loaded from interaction
# data after interactionInitGraphics. Pin the full initializer and table shape
# so the ordinary NPC row receives the final OAM palette used for drawing.
$room157VillagerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\femaleVillager.s')
$ringHelpBookSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\ringHelpBook.s')
if ($room157VillagerSource -notmatch '(?ms)^@initSubid05:\s+ld a,\$01\s+ld e,Interaction\.oamFlags\s+ld \(de\),a\s+callab agesInteractionsBank09\.getGameProgress_2\s+ld c,\$05\s+ld a,\$02\s+call checkNpcShouldExistAtGameStage\s+jp nz,interactionDelete.*?ld hl,@subid5ScriptTable.*?jp objectSetVisible82' -or
    $room157VillagerSource -notmatch '(?ms)^@runScriptAndAnimateFacingLink:\s+call interactionRunScript\s+jp npcFaceLinkAndAnimate' -or
    $room157VillagerSource -notmatch '(?ms)^@subid5ScriptTable:\s+\.dw mainScripts\.villagerGalSubid05Script_befored2\s+\.dw mainScripts\.villagerGalSubid05Script_afterd2\s+\.dw mainScripts\.villagerGalSubid05Script_afterd4\s+\.dw mainScripts\.villagerGalSubid05Script_afterNayruSaved\s+\.dw mainScripts\.villagerGalSubid05Script_afterd7\s+\.dw mainScripts\.villagerGalSubid05Script_afterd7\s+\.dw mainScripts\.villagerGalSubid05Script_twinrovaKidnappedZelda\s+\.dw mainScripts\.villagerGalSubid05Script_twinrovaKidnappedZelda' -or
    $ringHelpBookSource -notmatch '(?ms)^@state0:.*?ld e,Interaction\.subid\s+ld a,\(de\).*?or a\s+jr z,\+\+\s+ld e,Interaction\.oamFlags\s+ld a,\(de\)\s+inc a\s+ld \(de\),a\s+ld hl,mainScripts\.ringHelpBookSubid1Script') {
    throw 'Room 1:57 villager or ring-help-book palette initialization changed in the disassembly.'
}
$npcPaletteBySubid = @{
    '59:5' = 1
    # INTERAC_PAST_GUY $43:$01/$02 overwrites oamFlags with `$03 after
    # interactionInitGraphics.
    '67:1' = 3
    '67:2' = 3
    # The second ring-help book increments the palette loaded by
    # interactionInitGraphics before selecting its script.
    '229:1' = 2
    # INTERAC_LINKED_GAME_GHINI overwrites oamFlags with `$02 after
    # interactionInitGraphics.
    '203:0' = 2
}

# Room 1:58's late-story Impa and Nayru select their fixed text in assembly
# before entering a generic NPC script. Preserve those selections and their
# directional facing behavior instead of leaving the positioned records at the
# TX_0000/can-face fallback used for unresolved controllers.
$room158HoboSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscMan2.s')
$room158ImpaSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\impaNpc.s')
$room158NayruSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')
$room39eZeldaSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\zelda.s')
$room39eNpcScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
if ($room158HoboSource -notmatch '(?ms)^@subid4:.*?getGameProgress_2.*?cp \$03\s+jp z,interactionDelete.*?cp \$06.*?ld bc,\$5878.*?pastHoboScriptTable' -or
    $room158ImpaSource -notmatch '(?ms)^impaNpc_subid02:.*?getImpaNpcState.*?cp \$08\s+jp nz,interactionDelete\s+ld a,<TX_012f.*?impaNpc_runScriptAndFaceLink' -or
    $room158ImpaSource -notmatch '(?ms)^impaNpc_setTextIndexAndLoadGenericNpcScript:.*?Interaction\.var38.*?ld a,\$02.*?mainScripts\.genericNpcScript' -or
    $room158NayruSource -notmatch '(?ms)^@init0d:.*?GLOBALFLAG_FLAME_OF_DESPAIR_LIT.*?jp z,interactionDelete.*?GLOBALFLAG_FINISHEDGAME.*?jp nz,interactionDelete.*?<TX_1d17\s+jr @runGenericNpc' -or
    $room158NayruSource -notmatch '(?ms)^nayruAsNpc:\s+call interactionRunScript\s+jp npcFaceLinkAndAnimate' -or
    -not $allTexts.ContainsKey(0x012f) -or
    -not $allTexts.ContainsKey(0x1d17)) {
    throw 'Room 1:58 hobo, Impa, or Nayru initialization changed in the disassembly.'
}
if ($room158ImpaSource -notmatch '(?ms)^impaNpc_subid00:.*?^@state1:.*?interactionRunScript.*?Interaction\.var03.*?dec a\s+jr z,@animate\s+cp \$09\s+call nz,impaNpc_faceLinkIfClose.*?^@state0:.*?wRoomLayout\+\$22\s+ld \(hl\),TILEINDEX_INDOOR_DOWNSTAIRCASE.*?getImpaNpcState' -or
    $room158ImpaSource -notmatch '(?ms)^impaNpc_determineTextAndPositionInHouse:.*?@val00:.*?@val09:.*?ld bc,\$3838.*?<TX_0120.*?@val01:.*?@val0a:.*?ld bc,\$4828.*?<TX_0121.*?impaNpcScript_lookingAtPassage.*?@val02:.*?@val0b:.*?ld bc,\$2868.*?<TX_0122.*?@val0d:.*?ld bc,\$2868.*?<TX_012c.*?@val05:.*?@val0e:.*?ld bc,\$2868.*?<TX_0123' -or
    $room158ImpaSource -notmatch '(?ms)^@val01:\s*^@val0a:.*?ld a,<TX_0121\s+call @setTextAndPosition\s+ld \(de\),a.*?^@setTextAndPosition:.*?ld e,Interaction\.var38\s+ld a,\$02\s+ld \(de\),a.*?xor a\s+ret' -or
    $room158ImpaSource -notmatch '(?ms)^impaNpc_faceLinkIfClose:.*?ld c,\$28\s+call objectCheckLinkWithinDistance.*?objectGetAngleTowardEnemyTarget.*?^@noChange:.*?Interaction\.var38.*?^@updateDirection:.*?Interaction\.direction.*?interactionSetAnimation' -or
    $room158NayruSource -notmatch '(?ms)^@init0b:.*?GLOBALFLAG_FINISHEDGAME.*?jp nz,interactionDelete.*?GLOBALFLAG_SAVED_NAYRU.*?jp z,interactionDelete.*?TREASURE_MAKU_SEED.*?jp c,interactionDelete.*?<TX_1d14.*?^@runGenericNpc:.*?mainScripts\.genericNpcScript' -or
    $room39eZeldaSource -notmatch '(?ms)^@initSubid07:.*?GLOBALFLAG_GOT_RING_FROM_ZELDA.*?jp z,interactionDeleteAndUnmarkSolidPosition.*?TREASURE_MAKU_SEED.*?jp c,interactionDeleteAndUnmarkSolidPosition.*?GLOBALFLAG_SAVED_NAYRU.*?<TX_0606\s+jr nz,@actAsGenericNpc\s+ld a,<TX_0605.*?mainScripts\.genericNpcScript' -or
    $room39eZeldaSource -notmatch '(?ms)^zelda_state1:.*?\.dw @faceLinkAndRunScript.*?^@faceLinkAndRunScript:.*?interactionRunScript.*?npcFaceLinkAndAnimate' -or
    $room39eNpcScriptSource -notmatch '(?ms)^impaNpcScript_lookingAtPassage:.*?initcollisions.*?^@npcLoop:.*?checkabutton.*?turntofacelink.*?writeobjectbyte Interaction\.direction, \$ff.*?showloadedtext.*?setanimation \$00.*?scriptjump @npcLoop' -or
    -not $allTexts.ContainsKey(0x1d14) -or
    -not $allTexts.ContainsKey(0x0605) -or
    -not $allTexts.ContainsKey(0x0606)) {
    throw 'Room 3:9e Impa, Nayru, Zelda, or passage-script behavior changed in the disassembly.'
}
$npcTextBySubid['79:2'] = 0x012f
$npcTextBySubid['54:13'] = 0x1d17
$npcTextBySubid['54:11'] = 0x1d14
$npcTextBySubid['173:7'] = 0x0605
$npcTextBySubid['203:0'] = 0x4d05
$npcTextBySubid['213:0'] = 0x4d1e
$mustacheManBaseText = Resolve-ScriptTextId `
    'mustacheManScript' ([Collections.Generic.HashSet[string]]::new())
if ($mustacheManBaseText -ne 0x0f00) {
    throw 'Could not resolve mustacheManScript base text TX_0f00.'
}
$npcTextBySubid['66:0'] = $mustacheManBaseText
$pastHobo2BaseText = Resolve-ScriptTextId `
    'pastHobo2Script' ([Collections.Generic.HashSet[string]]::new())
if ($pastHobo2BaseText -ne 0x1620) {
    throw 'Could not resolve pastHobo2Script base text TX_1620.'
}
$npcTextBySubid['68:0'] = $pastHobo2BaseText
$npcTextByVariant = @{
    '88:1:0' = 0x1007
    '88:1:1' = 0x1008
}
$npcCanFaceBySubid = @{
    '79:2' = $true
    '54:13' = $true
    '54:11' = $true
    '173:7' = $true
    '49:4' = $true
    '49:5' = $true
    '88:1' = $true
}

# INTERAC_TOKAY keeps its script table at the global tokayScriptTable label
# instead of the local @...ScriptTable shape consumed by the generic resolver
# above. Preserve every visible island actor's source-selected initial text
# explicitly; the specialized runtime owner imports the later branches.
$tokayInitialTexts = @{
    0x05 = 0x0a00; 0x06 = 0x0a0b; 0x07 = 0x0a0a
    0x08 = 0x0a0b; 0x09 = 0x0a0b; 0x0a = 0x0a0b
    0x0b = 0x0a0e; 0x0d = 0x0a1c; 0x0e = 0x0a37
    0x0f = 0x0a1d; 0x10 = 0x0a1e; 0x11 = 0x0a40
    0x12 = 0x0a64; 0x13 = 0x0a65; 0x14 = 0x0a66
    0x15 = 0x0a60; 0x16 = 0x0a61; 0x17 = 0x0a62
    0x18 = 0x0a63; 0x19 = 0x0a67; 0x1d = 0x0a68
    0x1e = 0x0a6a; 0x1f = 0x0a6c
}
foreach ($entry in $tokayInitialTexts.GetEnumerator()) {
    if (-not $allTexts.ContainsKey($entry.Value)) {
        throw "Could not resolve Tokay text TX_$($entry.Value.ToString('x4'))."
    }
    $npcTextBySubid["72`:$([int]$entry.Key)"] = [int]$entry.Value
}
foreach ($subid in @(0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x1f)) {
    $npcCanFaceBySubid["72`:$subid"] = $true
}

# Every visible character row is denied the generic adapter unless its exact
# source placement has a traced production owner. This prevents a newly added
# native or cutscene-only interaction from becoming a solid idle NPC merely
# because npcInteractionIds supplies graphics for it.
$ordinaryNpcImplementationKeys =
    [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($key in @(
    '0:45:3f:01:00',
    '0:48:3a:03:00',
    '0:56:3a:04:00',
    '0:57:41:01:00',
    '0:58:41:04:00',
    '0:5a:39:02:00',
    '0:5a:39:03:00',
    '0:66:3b:01:00',
    '0:68:44:02:00',
    '0:68:3a:05:00',
    '0:68:3b:02:00',
    '0:68:3c:02:00',
    '1:45:43:01:00',
    '1:48:3a:06:00',
    '1:48:38:00:00',
    '1:57:3b:05:00',
    '1:58:44:04:00',
    '1:58:4f:02:00',
    '1:58:36:0d:00',
    '1:68:43:02:00',
    '1:75:58:01:00',
    '1:75:58:01:01',
    '0:46:3d:02:00',
    '1:03:bf:0c:00',
    '1:47:3a:07:00',
    '1:65:3b:04:00',
    '1:65:4d:0a:00',
    '1:65:37:12:00',
    '1:66:3a:08:00',
    '1:68:3b:03:00',
    '1:72:40:00:00',
    '1:73:40:00:01',
    '1:74:45:00:00',
    '1:77:45:01:00',
    '1:82:44:00:00',
    '1:82:3f:00:00',
    '1:83:41:00:00',
    '1:84:40:01:00',
    '1:92:43:00:00',
    '1:93:42:00:00',
    '1:93:40:01:01',
    '1:94:43:00:01',
    '0:bd:48:12:00',
    '0:cd:48:13:00',
    '0:dd:48:14:00',
    '1:aa:48:1f:00',
    '1:ad:48:15:00',
    '1:bd:48:16:00',
    '1:cd:48:17:00',
    '1:dd:48:18:00',
    '2:fd:68:01:00',
    '3:7e:bf:0a:00',
    '3:7f:bf:0b:00',
    '3:8e:bf:04:00',
    '3:ea:bf:00:00',
    '3:eb:bf:02:00',
    '3:ec:bf:02:00',
    '5:f8:c4:00:00',
    '5:f8:c4:01:00',
    '5:f8:c4:02:00',
    '5:f8:c4:03:00'
)) {
    if (-not $ordinaryNpcImplementationKeys.Add($key)) {
        throw "Duplicate ordinary NPC implementation key $key."
    }
}

$specializedNpcImplementationKeys =
    [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($key in @(
    '0:56:65:00:00',
    '0:5d:cb:00:00',
    '0:79:c8:00:00',
    '0:83:d5:00:00',
    '0:7c:59:00:00',
    '0:7c:59:00:02',
    '1:48:57:00:00',
    '1:49:3c:0e:00',
    '1:49:3a:0c:00',
    '1:49:43:06:00',
    '1:79:37:10:00',
    '1:97:37:03:00',
    '1:81:ce:03:00',
    '1:84:4b:06:00',
    '2:0e:3c:0d:00',
    '2:0e:3d:00:00',
    '2:0f:6a:00:00',
    '2:1e:69:00:00',
    '2:1f:69:01:00',
    '2:2e:59:00:01',
    '2:2f:55:00:00',
    '2:3e:5b:00:00',
    '2:5e:46:00:00',
    '2:e6:5c:00:00',
    '2:f3:3c:07:00',
    '0:aa:48:0f:00',
    '0:aa:48:10:00',
    '0:bb:48:1e:00',
    '1:ac:48:11:00',
    '1:bb:48:0a:00',
    '1:bb:48:0b:00',
    '1:cb:48:07:00',
    '1:da:48:08:00',
    '2:3f:48:05:00',
    '2:de:48:0d:00',
    '2:e4:48:0e:00',
    '2:e5:48:19:00',
    '2:e5:48:1a:00',
    '2:e5:48:1b:00',
    '2:e5:48:1c:00',
    '5:ca:48:06:00',
    '5:cc:48:09:00',
    '5:e9:48:1d:00',
    '1:cb:68:00:00',
    '1:ba:c4:04:00',
    '2:ee:89:00:00',
    '2:ee:89:01:00',
    '2:ee:89:06:00',
    '2:ee:e5:00:00',
    '2:ee:e5:01:00',
    '2:e9:30:00:00',
    '3:9e:4f:00:00',
    '3:9e:4f:00:01',
    '3:9e:4f:00:02',
    '3:9e:4f:00:05',
    '3:9e:4f:00:09',
    '3:9e:4f:00:0a',
    '3:9e:4f:00:0b',
    '3:9e:4f:00:0d',
    '3:9e:4f:00:0e',
    '3:9e:36:0b:00',
    '3:9e:ad:07:00',
    '3:fb:ca:01:00',
    '3:fc:28:0a:00',
    '4:e0:3a:02:00',
    '4:e1:58:00:00',
    '4:e1:40:0c:00',
    '4:e1:57:03:00',
    '4:e1:57:03:01',
    '4:e2:40:0c:00',
    '4:e2:58:00:01',
    '4:e2:58:03:03',
    '4:e2:57:03:02',
    '4:e2:57:03:03',
    '4:e7:40:0c:00',
    '4:e7:58:03:00',
    '4:e7:58:03:01',
    '4:e7:57:03:04',
    '4:e7:57:03:05',
    '4:e8:58:03:02',
    '4:e8:57:03:06',
    '4:e8:57:03:07'
)) {
    if (-not $specializedNpcImplementationKeys.Add($key)) {
        throw "Duplicate specialized NPC implementation key $key."
    }
}

$eventOwnedNpcImplementationKeys =
    [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($key in @(
    '0:38:87:00:00',
    '0:39:37:0d:00',
    '0:6c:73:00:00',
    '0:6c:73:01:00',
    '0:6c:73:02:00',
    '0:6a:31:00:00',
    '0:7b:3c:03:00',
    '0:7b:3c:04:00',
    '0:7b:3f:02:00',
    '1:38:88:00:00',
    '1:39:3a:0d:00',
    '1:75:37:0a:00',
    '1:75:31:04:00',
    '1:75:31:05:00',
    '1:75:36:0a:00',
    '1:75:ad:04:00',
    '1:86:58:02:00'
    '1:aa:48:00:00'
    '1:aa:48:01:00'
    '1:aa:48:02:00'
    '1:aa:48:03:00'
    '1:aa:48:04:00'
)) {
    if (-not $eventOwnedNpcImplementationKeys.Add($key)) {
        throw "Duplicate event-owned NPC implementation key $key."
    }
}

if ($ordinaryNpcImplementationKeys.Count -ne 61 -or
    $specializedNpcImplementationKeys.Count -ne 82 -or
    $eventOwnedNpcImplementationKeys.Count -ne 22) {
    throw 'NPC implementation registry key counts changed.'
}

function Resolve-NpcImplementation(
    [int]$group,
    [int]$room,
    [int]$id,
    [int]$subid,
    [int]$var03,
    [string]$override = ''
) {
    $key = "$group`:$($room.ToString('x2'))`:$($id.ToString('x2'))`:$($subid.ToString('x2'))`:$($var03.ToString('x2'))"
    $matches = [int]$ordinaryNpcImplementationKeys.Contains($key) +
        [int]$specializedNpcImplementationKeys.Contains($key) +
        [int]$eventOwnedNpcImplementationKeys.Contains($key)
    if ($matches -gt 1) {
        throw "NPC implementation key $key has more than one classification."
    }
    if ($override) {
        if ($override -notin @(
            'ordinary-generic',
            'specialized-native',
            'event-owned',
            'deliberately-unsupported'
        )) {
            throw "NPC implementation key $key has invalid override '$override'."
        }
        if ($matches -ne 0) {
            throw "NPC implementation key $key has both a registry classification and override."
        }
        return $override
    }
    if ($ordinaryNpcImplementationKeys.Contains($key)) {
        return 'ordinary-generic'
    }
    if ($specializedNpcImplementationKeys.Contains($key)) {
        return 'specialized-native'
    }
    if ($eventOwnedNpcImplementationKeys.Contains($key)) {
        return 'event-owned'
    }
    return 'deliberately-unsupported'
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
    [int]$canFaceOverride = -1,
    [string]$implementationOverride = ''
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
    $implementation = Resolve-NpcImplementation `
        $group $room $id $subid $var03 $implementationOverride
    return "$group`t$($room.ToString('x2'))`t$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`t$($var03.ToString('x2'))`t$($textId.ToString('x4'))`t$spriteName`t$($graphic.TileBase)`t$palette`t$initialAnimation`t$([int]$canFace)`t$upOam`t$rightOam`t$downOam`t$leftOam`t$encoded`t$implementation"
}

# Room object data is grouped by room label. Positioned interactions are
# emitted directly. Unpositioned interactions which derive a visible actor's
# position from save state are expanded below into mutually exclusive records.
$npcRows = [Collections.Generic.List[string]]::new()
$npcRows.Add("# group`troom`tid`tsubid`ty`tx`tvar03`ttext-id`tsprite`ttile-base`tpalette`tdefault-animation`tcan-face`tup-animation`tright-animation`tdown-animation`tleft-animation`tutf8-base64`timplementation")
$mainObjectLines = Select-CleanUsAssemblyLines (
    Read-ImportLines (Join-Path $Disassembly "objects\ages\mainData.s"))
$mainObjectSource = $mainObjectLines -join "`n"
$companionTutorialSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\companionTutorial.s')
$companionTutorialWramSource = Read-ImportText (
    Join-Path $Disassembly 'include\wram.s')
$specialObjectConstantsSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\specialObjects.s')
$companionTutorialPlacements = @(
    @{ Room = 0x27; Order = 2; Subid = 0x03; Required = 0x0c; Text = 0x2108; Flag = 3; Completion = 'above-link-range'; LinkMin = 0x40; LinkMax = 0x80 },
    @{ Room = 0x36; Order = 0; Subid = 0x03; Required = 0x0c; Text = 0x2108; Flag = 3; Completion = 'above-link-range'; LinkMin = 0x40; LinkMax = 0x70 },
    @{ Room = 0x37; Order = 2; Subid = 0x03; Required = 0x0c; Text = 0x2108; Flag = 3; Completion = 'above-link-range'; LinkMin = 0x10; LinkMax = 0x30 },
    @{ Room = 0x5b; Order = 0; Subid = 0x04; Required = 0x0d; Text = 0x2207; Flag = 4; Completion = 'companion-right'; LinkMin = 0; LinkMax = 0 },
    @{ Room = 0x79; Order = 0; Subid = 0x01; Required = 0x0b; Text = 0x2009; Flag = 1; Completion = 'companion-above'; LinkMin = 0; LinkMax = 0 },
    @{ Room = 0x89; Order = 0; Subid = 0x00; Required = 0x0b; Text = 0x2008; Flag = 0; Completion = 'companion-below-or-left'; LinkMin = 0; LinkMax = 0 }
)
$allPlacedCompanionTutorials = [regex]::Matches(
    $mainObjectSource,
    '(?m)^\s*obj_Interaction \$d0 \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2}\s*$')
if ($allPlacedCompanionTutorials.Count -ne $companionTutorialPlacements.Count -or
    $companionTutorialSource -notmatch '(?ms)^@state0:.*?ld a,\$01.*?^@state1:.*?ld a,\$02.*?w1Companion\.enabled.*?srl a.*?SPECIALOBJECT_FIRST_COMPANION.*?SPECIALOBJECT_MOOSH.*?w1Companion\.id.*?@flagNumbers.*?wCompanionTutorialTextShown.*?checkFlag.*?wLinkObjectIndex.*?bit 0,a.*?call nz,showText' -or
    $companionTutorialSource -notmatch '(?ms)^@state2:.*?\.dw @setFlagAndDeleteWhenCompanionIsBelowOrRight.*?\.dw @setFlagAndDeleteWhenCompanionIsAbove.*?\.dw @setFlagAndDeleteWhenCompanionIsAboveAndLinkInXRange.*?\.dw @setFlagAndDeleteWhenCompanionIsLeft' -or
    $companionTutorialSource -notmatch '(?ms)^@setFlagAndDeleteWhenCompanionIsBelowOrRight:.*?@cpYToCompanion.*?jr c,@setFlagAndDelete.*?Interaction\.xh.*?w1Companion\.xh.*?cp \(hl\).*?ret c.*?@setFlagAndDelete' -or
    $companionTutorialSource -notmatch '(?ms)^@setFlagAndDeleteWhenCompanionIsAboveAndLinkInXRange:.*?@checkLinkInXRange.*?ret nz.*?@setFlagAndDeleteWhenCompanionIsAbove' -or
    $companionTutorialSource -notmatch '(?ms)^@rooms:\s+\.db <ROOM_AGES_036\s+\.db <ROOM_AGES_037\s+\.db <ROOM_AGES_027.*?^@xRanges:\s+\.db \$40 \$70\s+\.db \$10 \$30\s+\.db \$40 \$80' -or
    $companionTutorialSource -notmatch '(?ms)^@tutorialTextToShow:\s+\.dw TX_2008\s+\.dw TX_2009\s+\.dw TX_0000\s+\.dw TX_2108\s+\.dw TX_2207\s+\.dw TX_2206' -or
    $companionTutorialSource -notmatch '(?ms)^@flagNumbers:\s+\.db \$00 \$01 \$00 \$03 \$04 \$00' -or
    $companionTutorialWramSource -notmatch '(?m)^wCompanionTutorialTextShown: ; \$c649\s*$' -or
    $specialObjectConstantsSource -notmatch '(?m)^\s*SPECIALOBJECT_RICKY\s+db ; \$0b\s*$' -or
    $specialObjectConstantsSource -notmatch '(?m)^\s*SPECIALOBJECT_DIMITRI\s+db ; \$0c\s*$' -or
    $specialObjectConstantsSource -notmatch '(?m)^\s*SPECIALOBJECT_MOOSH\s+db ; \$0d\s*$') {
    throw 'Ages INTERAC_COMPANION_TUTORIAL `$d0 source contract changed.'
}
$companionTutorialRows = [Collections.Generic.List[string]]::new()
$companionTutorialRows.Add(
    "# group`troom`torder`tid`tsubid`ty`tx`trequired-companion`ttext-id`tflag-address`tflag-bit`tcompletion`tlink-x-min`tlink-x-max`tutf8-base64`tsource")
foreach ($tutorial in $companionTutorialPlacements) {
    $roomHex = ([int]$tutorial.Room).ToString('x2')
    $subidHex = ([int]$tutorial.Subid).ToString('x2')
    $placement = [regex]::Match(
        $mainObjectSource,
        "(?ms)^group0Map$($roomHex)ObjectData:.*?^\s*obj_Interaction \`$d0 \`$$subidHex \`$(?<y>[0-9a-f]{2}) \`$(?<x>[0-9a-f]{2})\s*`$")
    if (-not $placement.Success -or -not $allTexts.ContainsKey([int]$tutorial.Text)) {
        throw "Room 0:$roomHex INTERAC_COMPANION_TUTORIAL `$$subidHex changed."
    }
    $message = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[[int]$tutorial.Text]))
    $companionTutorialRows.Add(
        "0`t$roomHex`t$($tutorial.Order)`td0`t$subidHex`t$($placement.Groups['y'].Value)`t$($placement.Groups['x'].Value)`t$(([int]$tutorial.Required).ToString('x2'))`t$(([int]$tutorial.Text).ToString('x4'))`tc649`t$($tutorial.Flag)`t$($tutorial.Completion)`t$(([int]$tutorial.LinkMin).ToString('x2'))`t$(([int]$tutorial.LinkMax).ToString('x2'))`t$message`tmainData.s:group0Map$($roomHex)ObjectData;companionTutorial.s:interactionCoded0")
}

$companionBarrierSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\companionScripts.s')
$allLowerYBarriers = [regex]::Matches(
    $mainObjectSource,
    '(?m)^\s*obj_Interaction \$71 \$02 \$[0-9a-f]{2} \$[0-9a-f]{2}\s*$')
$companionBarrierPlacements = @()
foreach ($expected in @(
    @{ Group = 0; Room = 0x6c; Order = 4 },
    @{ Group = 0; Room = 0x89; Order = 1 }
)) {
    $roomHex = ([int]$expected.Room).ToString('x2')
    $roomBlock = [regex]::Match(
        $mainObjectSource,
        "(?ms)^group$($expected.Group)Map$($roomHex)ObjectData:(?<body>.*?)(?=^group[0-7]Map[0-9a-f]{2}ObjectData:|\z)")
    $placement = if ($roomBlock.Success) {
        [regex]::Match(
            $roomBlock.Groups['body'].Value,
            '(?m)^\s*obj_Interaction \$71 \$02 \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s*$')
    } else { $null }
    if ($null -eq $placement -or -not $placement.Success) {
        throw "Room $($expected.Group):$roomHex lost INTERAC_COMPANION_SCRIPTS `$71:`$02."
    }
    $companionBarrierPlacements += [pscustomobject]@{
        Group = [int]$expected.Group
        Room = $roomHex
        Order = [int]$expected.Order
        Y = $placement.Groups['y'].Value
        X = $placement.Groups['x'].Value
    }
}
if ($allLowerYBarriers.Count -ne 2 -or $companionBarrierPlacements.Count -ne 2 -or
    $companionBarrierSource -notmatch '(?ms)^companionScript_genericState0:.*?wFileIsCompleted.*?wLinkObjectIndex.*?rrca.*?w1Companion\.id.*?SPECIALOBJECT_RICKY.*?wRickyState.*?bit 7,\(hl\).*?companionScript_deleteSelf' -or
    $companionBarrierSource -notmatch '(?ms)^companionScript_restrictLowerY:.*?companionScript_cpYToCompanion.*?ret nc.*?ld c,a.*?wLinkObjectIndex.*?rrca.*?ld \(hl\),a.*?SpecialObject\.speed.*?SPEED_0.*?companionScript_companionBarrierText.*?showText.*?^companionScript_cpYToCompanion:.*?Interaction\.yh.*?w1Companion\.yh.*?cp \(hl\).*?^companionScript_companionBarrierText:\s+\.dw TX_2007.*?\.dw TX_2105.*?\.dw TX_2209') {
    throw 'Ages INTERAC_COMPANION_SCRIPTS `$71:$02 lower-Y barrier contract changed.'
}
$barrierTextIds = @(0x2007, 0x2105, 0x2209)
foreach ($textId in $barrierTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve companion barrier TX_$($textId.ToString('x4'))."
    }
}
$companionBarrierRows = [Collections.Generic.List[string]]::new()
$companionBarrierRows.Add(
    "# group`troom`torder`tid`tsubid`ty`tx`tricky-state-address`tdimitri-state-address`tmoosh-state-address`tricky-text-id`tdimitri-text-id`tmoosh-text-id`tricky-utf8-base64`tdimitri-utf8-base64`tmoosh-utf8-base64`tsource")
foreach ($placement in $companionBarrierPlacements) {
    $group = $placement.Group
    $room = $placement.Room
    $messages = @($barrierTextIds | ForEach-Object {
        [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$_]))
    })
    $companionBarrierRows.Add(
        "$group`t$room`t$($placement.Order)`t71`t02`t$($placement.Y)`t$($placement.X)`tc646`tc647`tc648`t2007`t2105`t2209`t$($messages[0])`t$($messages[1])`t$($messages[2])`tmainData.s:group$($group)Map$($room)ObjectData;companionScripts.s:companionScript_restrictLowerY")
}

$tingleSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\tingle.s')
$tingleSparkleSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\sparkle.s')
$tingleSparkleHelperSource = Read-ImportText (
    Join-Path $Disassembly 'code\bank0.s')
$tingleBalloonSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\tingleBalloon.s')
$tingleScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$tingleInteractionDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\interactionData.s')
$tinglePartDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partData.s')
$tinglePartActiveCollisionsSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partActiveCollisions.s')
$itemCollisionTypesSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\itemCollisionTypes.s')
$tingleItemAttributesSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\itemAttributes.s')
$tingleCollisionEffectsSource = Read-ImportText (
    Join-Path $Disassembly 'code\collisionEffects.s')
$tingleFixedGfxHeaderSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\gfxHeaders.s')
$tingleGlobalFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
$tingleActiveCollisionMatch = [regex]::Match(
    $tinglePartActiveCollisionsSource,
    '(?m)^\s*dbrev %(?<a>[01]{8}) %(?<b>[01]{8}) %(?<c>[01]{8}) %(?<d>[01]{8}) ; 0x44\s*$')
$tingleActiveCollisions = if ($tingleActiveCollisionMatch.Success) {
    $tingleActiveCollisionMatch.Groups['a'].Value +
        $tingleActiveCollisionMatch.Groups['b'].Value +
        $tingleActiveCollisionMatch.Groups['c'].Value +
        $tingleActiveCollisionMatch.Groups['d'].Value
} else { '' }
$tingleExplosionOffsetMatch = [regex]::Match(
    $tingleBalloonSource,
    '(?ms)^@beenHit:.*?ld bc,\$(?<offset>[0-9a-f]{4})\s+call objectCopyPositionWithOffset')
$tingleExplosionOffset = if ($tingleExplosionOffsetMatch.Success) {
    [Convert]::ToInt32($tingleExplosionOffsetMatch.Groups['offset'].Value, 16)
} else { 0 }
$tingleExplosionYOffset = ($tingleExplosionOffset -shr 8) -band 0xff
$tingleExplosionXOffset = $tingleExplosionOffset -band 0xff
if ($tingleExplosionYOffset -ge 0x80) { $tingleExplosionYOffset -= 0x100 }
if ($tingleExplosionXOffset -ge 0x80) { $tingleExplosionXOffset -= 0x100 }
$tingleState4Match = [regex]::Match(
    $tingleSource,
    '(?ms)^@state4:(?<body>.*?)(?=^@label_0b_330:)')
$tingleKoolooSparkleMatches = if ($tingleState4Match.Success) {
    @([regex]::Matches(
        $tingleState4Match.Groups['body'].Value,
        '(?ms)ld bc,\$(?<offset>[0-9a-f]{4})\s+call objectCreateSparkle\s+ld l,Interaction\.angle\s+ld \(hl\),\$(?<angle>[0-9a-f]{2})'))
} else { @() }
$tingleKoolooSparkleOffsets = @($tingleKoolooSparkleMatches | ForEach-Object {
    $offset = [Convert]::ToInt32($_.Groups['offset'].Value, 16)
    $y = ($offset -shr 8) -band 0xff
    $x = $offset -band 0xff
    if ($y -ge 0x80) { $y -= 0x100 }
    if ($x -ge 0x80) { $x -= 0x100 }
    "$x,$y"
}) -join ';'
$tingleKoolooSparkleAngles = @($tingleKoolooSparkleMatches | ForEach-Object {
    [Convert]::ToInt32($_.Groups['angle'].Value, 16)
})
if ($mainObjectSource -notmatch '(?ms)^group0Map79ObjectData:\s+obj_Interaction \$d0 \$01 \$38 \$78\s+obj_Interaction \$c8 \$00 \$32 \$38\s+obj_End' -or
    $tingleInteractionDataSource -notmatch '(?m)^\s*/\* \$c8 \*/ m_InteractionData \$55 \$04 \$00\s*$' -or
    $tinglePartDataSource -notmatch '(?m)^\s*\.db \$55 \$82 \$44 \$00 \$01 \$18 \$02 \$00 ; \$44\s*$' -or
    $tingleActiveCollisions -ne '00001111111101100001100100000000' -or
    -not $tingleExplosionOffsetMatch.Success -or
    $tingleExplosionYOffset -ne -16 -or $tingleExplosionXOffset -ne 0 -or
    $itemCollisionTypesSource -notmatch '(?m)^\s*ITEMCOLLISION_SWORD_BEAM\s+db ; \$19: Sword beam, Ricky punch/tornado, Moosh stomp\s*$' -or
    $tingleItemAttributesSource -notmatch '(?m)^\s*\.db \$99 \$22 \$fe \$00 ; \$27: ITEM_SWORD_BEAM\s*$' -or
    $tingleItemAttributesSource -notmatch '(?m)^\s*\.db \$99 \$aa \$fc \$00 ; \$28: ITEM_28\s*$' -or
    $tingleItemAttributesSource -notmatch '(?m)^\s*\.db \$99 \$66 \$fc \$00 ; \$2a: ITEM_RICKY_TORNADO\s*$' -or
    $tingleCollisionEffectsSource -notmatch '(?ms)^partCheckCollisions:\s+ld e,Part\.collisionType\s+ld a,\(de\)\s+ld hl,partActiveCollisions' -or
    $tingleSource -notmatch '(?ms)^@state0:.*?interactionInitGraphics.*?interactionSetAlwaysUpdateBit.*?objectSetVisiblec0.*?objectSetCollideRadius.*?TREASURE_EMBER_SEEDS.*?TREASURE_MYSTERY_SEEDS\+1.*?cp \$03.*?PART_TINGLE_BALLOON' -or
    $tingleSource -notmatch '(?ms)^@state3:.*?counter1.*?interactionDecCounter1.*?ld c,\$10.*?objectUpdateSpeedZ_paramC.*?objectAddToAButtonSensitiveObjectList.*?tingleScript.*?ld a,\$01' -or
    $tingleSource -notmatch '(?ms)^@state4:.*?TREASURE_SEED_SATCHEL.*?Interaction\.var3d.*?interactionRunScript.*?interactionAnimateAsNpc.*?animParameter.*?ld bc,-\$200.*?objectCreateSparkle.*?ld c,\$20.*?objectUpdateSpeedZ_paramC' -or
    $tingleKoolooSparkleMatches.Count -ne 3 -or
    $tingleKoolooSparkleOffsets -ne '0,-24;8,-16;-8,-16' -or
    @($tingleKoolooSparkleAngles | Where-Object { $_ -ne 0x10 }).Count -ne 0 -or
    $tingleSparkleHelperSource -notmatch '(?ms)^objectCreateSparkle:\s+call getFreeInteractionSlot\s+ret nz\s+ld \(hl\),INTERAC_SPARKLE\s+inc l\s+ld \(hl\),\$00\s+jp objectCopyPositionWithOffset' -or
    $tingleSparkleSource -notmatch '(?ms)^@initSubid00:.*?inc e\s+ld a,\(de\)\s+or a\s+jp nz,objectSetVisible81.*?^@runSubid00:.*?Interaction\.animParameter\s+ld a,\(de\)\s+cp \$ff\s+jp z,interactionDelete\s+jp interactionAnimate' -or
    $tingleBalloonSource -notmatch '(?ms)^@state0:.*?Part\.counter1.*?\$38.*?inc l.*?\$ff.*?Part\.zh.*?\$f1.*?ld bc,-\$10.*?partSetAnimation.*?objectSetVisible81' -or
    $tingleBalloonSource -notmatch '(?ms)^@state1:.*?partCommon_decCounter1IfNonzero.*?\$38.*?speedZ.*?cpl.*?inc a.*?cpl.*?objectUpdateSpeedZ_paramC.*?w1Companion|(?ms)^@state1:.*?objectUpdateSpeedZ_paramC.*?objectGetRelatedObject1Var.*?Part\.zh' -or
    $tingleBalloonSource -notmatch '(?ms)^@beenHit:.*?Object\.state.*?inc \(hl\).*?INTERAC_EXPLOSION.*?\$f000.*?partDelete' -or
    $tingleScriptSource -notmatch '(?ms)^tingleScript:.*?checkabutton.*?TREASURE_ISLAND_CHART.*?GLOBALFLAG_MET_TINGLE.*?TX_1e00.*?TX_1e01.*?TX_1e02.*?TREASURE_OBJECT_ISLAND_CHART_00.*?TX_1e04.*?wait 60.*?w1Companion\.var03, \$02.*?w1Companion\.state, \$0a' -or
    $tingleScriptSource -notmatch '(?ms)^@haveLevel1Satchel:.*?GLOBALFLAG_GOT_SATCHEL_UPGRADE.*?TX_1e06.*?TX_1e07.*?TREASURE_OBJECT_SEED_SATCHEL_UPGRADE.*?refillSeedSatchel' -or
    $tingleScriptSource -notmatch '(?ms)^@postgame:.*?TX_1e09.*?askforsecret TINGLE_SECRET.*?TX_1e0d.*?TX_1e0e.*?GLOBALFLAG_BEGAN_TINGLE_SECRET.*?@showReturnSecret:.*?TINGLE_RETURN_SECRET.*?GLOBALFLAG_DONE_TINGLE_SECRET.*?TX_1e0f' -or
    $tingleGlobalFlagSource -notmatch '(?m)^\s*GLOBALFLAG_MET_TINGLE\s+db ; \$1b' -or
    $tingleGlobalFlagSource -notmatch '(?m)^\s*GLOBALFLAG_GOT_SATCHEL_UPGRADE\s+db ; \$46' -or
    $tingleGlobalFlagSource -notmatch '(?m)^\s*GLOBALFLAG_BEGAN_TINGLE_SECRET\s+db ; \$6b' -or
    $tingleGlobalFlagSource -notmatch '(?m)^\s*GLOBALFLAG_DONE_TINGLE_SECRET\s+db ; \$75') {
    throw 'Room 0:79 INTERAC_TINGLE `$c8:$00 source contract changed.'
}
$tingleSparkleGraphic = $interactionGraphics['132:0']
$tingleSparkleAnimationIndex = if ($tingleSparkleGraphic) {
    $tingleSparkleGraphic.DefaultAnimation
} else { -1 }
$tingleSparkleAnimation = if ($tingleSparkleAnimationIndex -ge 0) {
    Resolve-NpcAnimation 0x84 $tingleSparkleAnimationIndex
} else { '' }
$tingleSparkleSprite = if ($tingleSparkleGraphic -and
    $gfxNames.ContainsKey($tingleSparkleGraphic.Gfx)) {
    $gfxNames[$tingleSparkleGraphic.Gfx]
} else { '' }
if (-not $tingleSparkleGraphic -or
    $tingleSparkleGraphic.Gfx -ne 0x6b -or
    $tingleSparkleGraphic.TileBase -ne 0x0a -or
    $tingleSparkleGraphic.Palette -ne 0 -or
    $tingleSparkleAnimationIndex -ne 1 -or
    [string]::IsNullOrWhiteSpace($tingleSparkleAnimation) -or
    [string]::IsNullOrWhiteSpace($tingleSparkleSprite)) {
    throw 'Could not resolve Tingle INTERAC_SPARKLE $84:$00 graphics/animation.'
}
$tingleAnimationRows = [Collections.Generic.List[string]]::new()
$tingleAnimationRows.Add("# owner`tanimation`tencoded`tsource")
for ($animation = 0; $animation -lt 4; $animation++) {
    $encoded = Resolve-NpcAnimation 0xc8 $animation
    if (-not $encoded) {
        throw "Could not resolve INTERAC_TINGLE animation `$$($animation.ToString('x2'))."
    }
    $tingleAnimationRows.Add(
        "tingle`t$animation`t$encoded`tinteractionAnimations.s:interactionc8Animations")
}
$tingleBalloonAnimation = Resolve-PartAnimation 0x44 0
if (-not $tingleBalloonAnimation) {
    throw 'Could not resolve PART_TINGLE_BALLOON animation $00.'
}
$tingleAnimationRows.Add(
    "balloon`t0`t$tingleBalloonAnimation`tpartAnimations.s:part44Animations")
$tingleExplosionGraphic = $interactionGraphics['86:0']
$tingleExplosionAnimation = Resolve-NpcAnimation 0x56 0
if (-not $tingleExplosionGraphic -or
    $tingleExplosionGraphic.Gfx -ne 0 -or
    $tingleExplosionGraphic.TileBase -ne 0x0c -or
    $tingleExplosionGraphic.Palette -ne 2 -or
    $tingleExplosionGraphic.DefaultAnimation -ne 0 -or
    $tingleFixedGfxHeaderSource -notmatch '(?ms)^m_GfxHeaderStart \$83, GFXH_COMMON_SPRITES\s+m_GfxHeader spr_common_sprites, \$8001\s+m_GfxHeaderEnd' -or
    [string]::IsNullOrWhiteSpace($tingleExplosionAnimation)) {
    throw 'Could not resolve Tingle balloon INTERAC_EXPLOSION $56 graphics/animation.'
}
$tingleAnimationRows.Add(
    "explosion`t0`t$tingleExplosionAnimation`tinteractionAnimations.s:interaction56Animations")
$tingleAnimationRows.Add(
    "sparkle`t$tingleSparkleAnimationIndex`t$tingleSparkleAnimation`tinteractionAnimations.s:interaction84Animations")

$tingleTextRows = [Collections.Generic.List[string]]::new()
$tingleTextRows.Add("# text-id`tutf8-base64`tsource")
foreach ($textId in (@(0x2006) + @(0x1e00..0x1e0f))) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Tingle TX_$($textId.ToString('x4'))."
    }
    $text = $allTexts[$textId]
    if ($textId -eq 0x1e05) {
        if ($text -notmatch '^\\call\(TX_1e0c\)\\stop') {
            throw 'Tingle TX_1e05 lost its leading TX_1e0c call/stop chain.'
        }
        $text = $text.Replace('\call(TX_1e0c)', $allTexts[0x1e0c])
    }
    elseif ($textId -eq 0x1e0d) {
        if ($text -notmatch '\\jump\(TX_1e0b\)$') {
            throw 'Tingle TX_1e0d lost its terminal TX_1e0b jump.'
        }
        $text = $text.Replace('\jump(TX_1e0b)', $allTexts[0x1e0b])
    }
    $message = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($text))
    $tingleTextRows.Add(
        "$($textId.ToString('x4'))`t$message`ttext/ages:TX_$($textId.ToString('x4'))")
}
$tingleRows = @(
    "# group`troom`tid`tsubid`tballoon-part`tinitial-z`tballoon-counter`tballoon-speed-z`tfall-wait`tfall-gravity`tkooloo-speed-z`tkooloo-gravity`tkooloo-sparkle-interaction`tkooloo-sparkle-subid`tkooloo-sparkle-angle`tkooloo-sparkle-offsets`tkooloo-sparkle-sprite`tkooloo-sparkle-tile-base`tkooloo-sparkle-palette`tkooloo-sparkle-animation`tpost-chart-wait`tupgrade-glow-wait`tseed-threshold`tmet-flag`tupgrade-flag`tbegan-secret-flag`tdone-secret-flag`tisland-chart-treasure`tisland-chart-object`tsatchel-treasure`tsatchel-upgrade-object`tballoon-tile-base`tballoon-palette`texplosion-sprite`texplosion-tile-base`texplosion-palette`texplosion-y-offset`texplosion-x-offset`tballoon-active-collisions`tsource",
    "0`t79`tc8`t00`t44`t-15`t56`t-16`t15`t16`t-512`t32`t84`t00`t10`t$tingleKoolooSparkleOffsets`t$tingleSparkleSprite`t$($tingleSparkleGraphic.TileBase)`t$($tingleSparkleGraphic.Palette)`t$tingleSparkleAnimationIndex`t60`t120`t3`t1b`t46`t6b`t75`t54`tTREASURE_OBJECT_ISLAND_CHART_00`t19`tTREASURE_OBJECT_SEED_SATCHEL_UPGRADE`t24`t2`tspr_common_sprites`t$($tingleExplosionGraphic.TileBase)`t$($tingleExplosionGraphic.Palette)`t$tingleExplosionYOffset`t$tingleExplosionXOffset`t$tingleActiveCollisions`tobject_code/ages/interactions/tingle.s:interactionCodec8+objectCreateSparkle;object_code/ages/interactions/sparkle.s:interactionCode84;code/bank0.s:objectCreateSparkle;tingleBalloon.s:partCode44;explosion.s:interactionCode56;interactionData.s:INTERAC_SPARKLE/INTERAC_EXPLOSION;interactionAnimations.s:interaction84Animations/interaction56Animations;gfxHeaders.s:GFXH_COMMON_SPRITES;partActiveCollisions.s:0x44;itemCollisionTypes.s:ITEMCOLLISION_SWORD_BEAM;itemAttributes.s:ITEM_SWORD_BEAM/ITEM_28/ITEM_RICKY_TORNADO;scripts.s:tingleScript"
)
$enemyObjectSource = Read-ImportText (
    Join-Path $Disassembly "objects\ages\enemyData.s")
if ($mainObjectSource -notmatch '(?ms)^group1Map45ObjectData:\s+obj_Interaction \$43 \$01 \$68 \$18\s+obj_End') {
    throw 'Room 1:45 no longer contains past guy $43:$01 at $68,$18.'
}
if ($mainObjectSource -notmatch '(?ms)^group3MapfcObjectData:\s+obj_Interaction \$28 \$0a \$40 \$50\s+obj_End') {
    throw 'Room 3:fc no longer contains past Bipin $28:$0a at $40,$50.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map57ObjectData:\s+obj_Interaction \$3b \$05 \$38 \$48\s+obj_End') {
    throw 'Room 1:57 no longer contains female villager $3b:$05 at $38,$48.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map58ObjectData:\s+obj_Interaction \$44 \$04 \$48 \$48\s+obj_Interaction \$4f \$02 \$48 \$48\s+obj_Interaction \$36 \$0d \$48 \$38\s+obj_End') {
    throw 'Room 1:58 no longer contains ordered hobo $44:$04, Impa $4f:$02, and Nayru $36:$0d placements.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map72ObjectData:\s+obj_Interaction \$40 \$00 \$58 \$28 \$00\s+obj_End') {
    throw 'Room 1:72 no longer contains soldier $40:$00 var03 $00 at $58,$28.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map71ObjectData:\s+obj_Pointer group1Map71EnemyObjectData\s+obj_End' -or
    $enemyObjectSource -notmatch '(?ms)^group1Map71EnemyObjectData:\s+obj_ItemDrop \$00 \$05 \$11\s+obj_ItemDrop\s+\$01 \$12\s+obj_ItemDrop\s+\$05 \$13\s+obj_RandomEnemy \$40 \$0c \$00\s+obj_EndPointer') {
    throw 'Room 1:71 item-drop producers and random Arrow Moblins changed.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map81ObjectData:\s+obj_Interaction \$ce \$03 \$38 \$18\s+obj_End') {
    throw 'Room 1:81 no longer contains Business Scrub $ce:$03 at $38,$18.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map73ObjectData:\s+obj_Interaction \$40 \$00 \$18 \$18 \$01\s+obj_End') {
    throw 'Room 1:73 no longer contains soldier $40:$00 var03 $01 at $18,$18.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map74ObjectData:\s+obj_Interaction \$45 \$00 \$58 \$38\s+obj_End') {
    throw 'Room 1:74 no longer contains past old lady $45:$00 at $58,$38.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map82ObjectData:\s+obj_Interaction \$44 \$00 \$38 \$58\s+obj_Interaction \$3f \$00 \$48 \$38\s+obj_End') {
    throw 'Room 1:82 no longer contains ordered misc man 2 $44:$00 and boy 2 $3f:$00.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map83ObjectData:\s+obj_Interaction \$41 \$00 \$38 \$4e\s+obj_Interaction \$8a \$01 \$00 \$00 \$03\s+obj_Pointer group1Map83EnemyObjectData\s+obj_End') {
    throw 'Room 1:83 no longer contains misc man $41:$00 followed by remote Maku $8a:$01/v$03 and its item-drop pointer.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map84ObjectData:\s+obj_Interaction \$4b \$06 \$28 \$58\s+obj_Interaction \$4b \$06 \$40 \$48\s+obj_Interaction \$4b \$06 \$50 \$68\s+obj_Interaction \$40 \$01 \$48 \$78 \$00\s+obj_End') {
    throw 'Room 1:84 no longer contains three ordered stone rabbits followed by soldier $40:$01.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map92ObjectData:\s+obj_Interaction \$43 \$00 \$28 \$58 \$00\s+obj_End') {
    throw 'Room 1:92 no longer contains past guy $43:$00 var03 $00 at $28,$58.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map93ObjectData:\s+obj_Interaction \$42 \$00 \$38 \$58\s+obj_Interaction \$40 \$01 \$38 \$38 \$01\s+obj_End') {
    throw 'Room 1:93 no longer contains ordered mustache man $42:$00 and soldier $40:$01 var03 $01.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map94ObjectData:\s+obj_Interaction \$43 \$00 \$28 \$68 \$01\s+obj_End') {
    throw 'Room 1:94 no longer contains past guy $43:$00 var03 $01 at $28,$68.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map75ObjectData:\s+obj_Interaction \$37 \$0a \$58 \$60\s+obj_Interaction \$31 \$04 \$f8 \$58\s+obj_Interaction \$31 \$05 \$58 \$60\s+obj_Interaction \$36 \$0a \$58 \$40\s+obj_Interaction \$ad \$04 \$48 \$50\s+obj_Interaction \$58 \$01 \$58 \$48 \$00\s+obj_Interaction \$58 \$01 \$58 \$28 \$01\s+obj_End') {
    throw 'Room 1:75 pre-Black Tower ensemble and hardhat worker order changed.'
}
if ($mainObjectSource -notmatch '(?ms)^group1Map86ObjectData:\s+obj_Interaction \$58 \$02 \$38 \$48\s+obj_Interaction \$dc \$07 \$28 \$78\s+obj_End') {
    throw 'Room 1:86 no longer contains ordered hardhat $58:$02 and heart-piece spawner $dc:$07 placements.'
}
if ($mainObjectSource -notmatch '(?ms)^group3Map9eObjectData:\s+obj_Interaction \$4f \$00\s+obj_Interaction \$36 \$0b \$28 \$58\s+obj_Interaction \$ad \$07 \$38 \$78\s+obj_Interaction \$dc \$08 \$32 \$80\s+obj_End') {
    throw 'Room 3:9e no longer contains ordered Impa, Nayru, Zelda, and tile-change watcher placements.'
}
$pastOldLadySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\pastOldLady.s')
$pastOldLadyScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
if ($pastOldLadySource -notmatch '(?ms)^@subid0:\s+call checkInteractionState\s+jr nz,@@initialized\s+ld a,GLOBALFLAG_FINISHEDGAME\s+call checkGlobalFlag\s+jp nz,interactionDelete\s+call @initGraphicsTextAndScript\s+^@@initialized:\s+call interactionRunScript\s+jp interactionAnimateAsNpc\s+^@subid1:' -or
    $pastOldLadySource -notmatch '(?ms)^@initGraphicsTextAndScript:\s+call interactionInitGraphics\s+call objectMarkSolidPosition\s+ld a,>TX_1800\s+call interactionSetHighTextIndex.*?ld hl,@scriptTable.*?call interactionSetScript\s+jp interactionIncState\s+^@scriptTable:\s+\.dw mainScripts\.pastOldLadySubid0Script\s+\.dw mainScripts\.stubScript' -or
    $pastOldLadyScriptSource -notmatch '(?ms)^pastOldLadySubid0Script:\s+rungenericnpclowindex <TX_180a') {
    throw 'Room 1:74 past old lady initialization, dialogue, or native update path changed.'
}

$stoneRabbitSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\rabbitMain.s')
if ($stoneRabbitSource -notmatch '(?ms)^@initSubid6:\s+; Delete if veran defeated\s+ld hl,wGroup4RoomFlags\+\$fc\s+bit 7,\(hl\)\s+jp nz,interactionDelete\s+; Delete if haven''t beaten Jabu\s+ld a,\(wEssencesObtained\)\s+bit 6,a\s+jp z,interactionDelete\s+callab agesInteractionsBank08\.loadStoneNpcPalette\s+ld a,\$06\s+call objectSetCollideRadius\s+^@initSubid3:.*?^@setStonePaletteAndAnimation:\s+ld a,\$06\s+ld e,Interaction\.oamFlags\s+ld \(de\),a\s+jp interactionSetAnimation' -or
    $stoneRabbitSource -notmatch '(?ms)^@state1:.*?\.dw interactionPushLinkAwayAndUpdateDrawPriority\s+\.dw rabbitSubid7') {
    throw 'Stone rabbit $4b:$06 visibility, palette, collision, animation, or update behavior changed.'
}
$stoneRabbitAnimation = Resolve-NpcAnimation 0x4b 0x06
if (-not $stoneRabbitAnimation) {
    throw 'Could not resolve stone rabbit $4b:$06 animation $06.'
}
$stoneRabbitRows = @(
    "# group`troom`tid`tsubid`tpalette`tanimation-index`tcollision-radius`tanimation`tsource",
    "1`t84`t4b`t06`t06`t06`t06`t$stoneRabbitAnimation`tmainData.s:group1Map84ObjectData;rabbitMain.s:@initSubid6;rabbitMain.s:@state1"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\stone_rabbit.tsv'),
    $stoneRabbitRows)

$businessScrubSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\businessScrub.s')
$businessScrubTileIndexSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
$businessScrubSpriteProperties = Read-ImportText (
    Join-Path $Disassembly (
        'gfx_compressible\common\spr_hostilescrub.properties'))
$businessScrubGraphic = $interactionGraphics['206:0']
if ($null -eq $businessScrubGraphic -or
    $businessScrubGraphic.Gfx -ne 0x8d -or
    $businessScrubGraphic.TileBase -ne 0x00 -or
    $businessScrubGraphic.Flags -ne 0x50 -or
    $gfxNames[$businessScrubGraphic.Gfx] -ne 'spr_hostilescrub') {
    throw 'Business Scrub $ce no longer uses spr_hostilescrub header $8d, tile base $00, palette $05, animation $00.'
}
if ($businessScrubSpriteProperties -notmatch
    '(?m)^invert:\s*false\s*$') {
    throw 'spr_hostilescrub.properties no longer declares its white-background source as invert: false.'
}
if ($businessScrubSource -notmatch '(?ms)^interactionCodece:.*?^@state0:.*?interactionSetAlwaysUpdateBit.*?^@sellingShield:.*?wShieldLevel.*?dec a.*?add c.*?@itemPrices.*?wTextNumberSubstitution' -or
    $businessScrubSource -notmatch '(?ms)ld e,Interaction\.collisionRadiusY\s+ld a,\$06.*?interactionInitGraphics\s+call objectMakeTileSolid\s+ld h,>wRoomLayout\s+ld \(hl\),\$00.*?objectAddToAButtonSensitiveObjectList.*?INTERAC_BUSINESS_SCRUB\s+ldi \(hl\),a\s+ld a,\$80' -or
    $businessScrubSource -notmatch '(?ms)^@state1:.*?wScrollMode.*?SCROLLMODE_08 \| SCROLLMODE_04 \| SCROLLMODE_02.*?interactionAnimate\s+ld c,\$20\s+call objectCheckLinkWithinDistance.*?ld a,\$03\s+jp interactionSetAnimation.*?ld a,\$01\s+jp interactionSetAnimation.*?interactionIncState\s+ld a,\$02\s+call interactionSetAnimation.*?TX_4500\s+jp showTextNonExitable' -or
    $businessScrubSource -notmatch '(?ms)^@mimicBush:.*?TILEINDEX_OVERWORLD_BUSH_1.*?objectMimicBgTile.*?ld a,\$05\s+call interactionSetAnimation.*?^@subid80State1:.*?Interaction\.visible.*?Interaction\.yh.*?Interaction\.animParameter.*?@bushYOffsets' -or
    $businessScrubSource -notmatch '(?ms)^@state2:.*?interactionAnimate.*?wTextIsActive.*?wSelectedTextOption.*?ld a,\$04\s+jp interactionSetAnimation.*?TX_4506.*?^@agreedToBuy:.*?cpRupeeValue.*?TX_4507.*?^@giveShield:\s+call checkTreasureObtained.*?TX_4508.*?^@giveTreasure:.*?call giveTreasure.*?removeRupeeValue.*?SND_GETSEED.*?TX_4505' -or
    $businessScrubSource -notmatch '(?ms)^@bushYOffsets:\s+\.db \$00.*?\.db \$f8.*?\.db \$f5' -or
    $businessScrubSource -notmatch '(?ms)^@rupeeValues:.*?\.ifdef ROM_AGES\s+\.db RUPEEVAL_50\s+\.db RUPEEVAL_100\s+\.db RUPEEVAL_150\s+\.db RUPEEVAL_30\s+\.db RUPEEVAL_50\s+\.db RUPEEVAL_80\s+\.db RUPEEVAL_10\s+\.db RUPEEVAL_20\s+\.db RUPEEVAL_40' -or
    $businessScrubSource -notmatch '(?ms)^@treasuresToSell:\s+\.db TREASURE_SHIELD\s+\$01\s+\.db TREASURE_SHIELD\s+\$02\s+\.db TREASURE_SHIELD\s+\$03\s+\.db TREASURE_SHIELD\s+\$01\s+\.db TREASURE_SHIELD\s+\$02\s+\.db TREASURE_SHIELD\s+\$03' -or
    $businessScrubSource -notmatch '(?ms)^@itemPrices:.*?\.ifdef ROM_AGES\s+\.dw \$0050\s+\.dw \$0100\s+\.dw \$0150\s+\.dw \$0030\s+\.dw \$0050\s+\.dw \$0080\s+\.dw \$0010\s+\.dw \$0020\s+\.dw \$0040' -or
    $businessScrubTileIndexSource -notmatch '(?ms)^\.ifdef ROM_AGES\s+\.define TILEINDEX_OVERWORLD_BUSH_1\s+\$c5\b') {
    throw 'Business Scrub $ce shield sale, presentation, proximity, or purchase behavior changed.'
}

$businessScrubOfferRows = @(
    "# shield-level`teffective-subid`tprice`ttreasure`tparameter`tsource",
    "0`t03`t30`t01`t01`tbusinessScrub.s:@sellingShield;businessScrub.s:@rupeeValues;businessScrub.s:@treasuresToSell;businessScrub.s:@itemPrices",
    "1`t03`t30`t01`t01`tbusinessScrub.s:@sellingShield;businessScrub.s:@rupeeValues;businessScrub.s:@treasuresToSell;businessScrub.s:@itemPrices",
    "2`t04`t50`t01`t02`tbusinessScrub.s:@sellingShield;businessScrub.s:@rupeeValues;businessScrub.s:@treasuresToSell;businessScrub.s:@itemPrices",
    "3`t05`t80`t01`t03`tbusinessScrub.s:@sellingShield;businessScrub.s:@rupeeValues;businessScrub.s:@treasuresToSell;businessScrub.s:@itemPrices"
)
$businessScrubConstantRows = @(
    "# key`tvalue",
    "group`t1",
    "room`t129",
    "interaction-id`t206",
    "placed-subid`t3",
    "collision-radius`t6",
    "proximity-radius`t32",
    "a-button-point-offset`t10",
    "floor-tile`t0",
    "floor-collision`t15",
    "bush-tile`t197",
    "source-grayscale-inverted`t0",
    "bush-normal-offset`t0",
    "bush-near-offset`t-8",
    "bush-talk-offset`t-11",
    "prompt-text`t17673",
    "success-text`t17669",
    "decline-text`t17670",
    "insufficient-text`t17671",
    "already-owned-text`t17672"
)
$businessScrubAnimationRows =
    [Collections.Generic.List[string]]::new()
$businessScrubAnimationRows.Add(
    "# animation`tencoded-animation")
foreach ($animationIndex in 0..4) {
    $animation = Resolve-NpcAnimation 0xce $animationIndex
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve Business Scrub animation `$$($animationIndex.ToString('x2'))."
    }
    $businessScrubAnimationRows.Add(
        "$($animationIndex.ToString('x2'))`t$animation")
}
$businessScrubTextRows = [Collections.Generic.List[string]]::new()
$businessScrubTextRows.Add("# text-id`tutf8-base64")
foreach ($textId in @(0x4505, 0x4506, 0x4507, 0x4508, 0x4509)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Business Scrub text TX_$($textId.ToString('x4'))."
    }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $businessScrubTextRows.Add(
        "$($textId.ToString('x4'))`t$encoded")
}

$mustacheManSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\mustacheMan.s')
$pastGuySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\pastGuy.s')
$miscMan2Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscMan2.s')
$boy2Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\boy2.s')
if ($miscMan2Source -notmatch '(?ms)^@subid0:\s+call checkInteractionState\s+jr nz,@@initialized\s+ld a,GLOBALFLAG_FINISHEDGAME\s+call checkGlobalFlag\s+jp nz,interactionDelete\s+call @initGraphicsIncStateAndLoadScript\s+^@@initialized:\s+call interactionRunScript\s+jp c,interactionDelete\s+jp interactionAnimateAsNpc' -or
    $boy2Source -notmatch '(?ms)^@subid0:\s+call checkInteractionState\s+jr nz,@@state1\s+^@@state0:\s+ld a,GLOBALFLAG_FINISHEDGAME\s+call checkGlobalFlag\s+jp nz,interactionDelete\s+ld a,GLOBALFLAG_0b\s+call checkGlobalFlag\s+jp nz,interactionDelete\s+call @initializeGraphicsAndScript\s+^@@state1:\s+call interactionRunScript\s+jp npcFaceLinkAndAnimate' -or
    $mustacheManSource -notmatch '(?ms)^@subid0:\s+call checkInteractionState\s+jr nz,@@initialized\s+ld a,GLOBALFLAG_FINISHEDGAME\s+call checkGlobalFlag\s+jp nz,interactionDelete\s+call @initGraphicsAndScript\s+^@@initialized:\s+call interactionRunScript\s+jp interactionAnimateAsNpc' -or
    $mustacheManSource -notmatch '(?ms)^@initGraphicsAndScript:\s+call interactionInitGraphics\s+call objectMarkSolidPosition\s+ld a,>TX_0f00\s+call interactionSetHighTextIndex.*?\.dw mainScripts\.mustacheManScript' -or
    $pastGuySource -notmatch '(?ms)^@subid0:\s+ld a,GLOBALFLAG_FINISHEDGAME\s+call checkGlobalFlag\s+jp nz,interactionDelete\s+ld a,GLOBALFLAG_0b\s+call checkGlobalFlag\s+ld e,Interaction\.var03\s+ld a,\(de\).*?call @initGraphicsIncStateAndLoadScript.*?call interactionRunScript\s+jp interactionAnimateAsNpc') {
    throw 'Rooms 1:82/1:92/1:93/1:94 ordinary NPC native behavior changed.'
}

# PART_SWITCH $05, PART_BUTTON $09, the buttons' trigger-chest consumers
# $20:$00/$21:$17, INTERAC_DUNGEON_STUFF's falling-key/enemy-clear rewards
# $12:$01/$02, the
# trigger-controlled and enemy-controlled shutter variants of
# INTERAC_DOOR_CONTROLLER $1e:$04-$0b, and INTERAC_PUSHBLOCK_TRIGGER $13:$01
# form reusable dungeon mechanisms around wActiveTriggers and wNumEnemies.
# Export every supported direct placement in source order; rooms 4:08, 4:09,
# 4:0b, and 4:0c are the canonical button-chest, button-door, combat-door, and
# trigger-before-door cases. Spirit's Grave room 4:1e and Wing Dungeon room
# 4:39 already import their $12:$01 placements through their source-ordered
# dungeon object tables, so the shared table must not duplicate those owners.
$pushblockTriggerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\pushblockTrigger.s')
$buttonSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\button.s')
$switchSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\switch.s')
$doorControllerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\doorController.s')
$dungeonStuffSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\dungeonStuff.s')
$dungeonScriptSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\dungeonScript.s')
$dungeonEventSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\dungeonEvents.s')
$dungeonScriptCommandSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\dungeonScripts.s')
$commonScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\common\commonScripts.s')
$commonScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\common\scriptHelper.s')
$interactableTilesSource = Read-ImportText (
    Join-Path $Disassembly 'code\interactableTiles.s')
$interactableTileDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\interactableTiles.s')
$standardTileSubstitutionSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\standardTileSubstitutions.s')
$keyDoorGraphicSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\keydoorTiles.s')
$dungeonKeySpriteSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\dungeonKeySprite.s')
$overworldKeySpriteSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\overworldKeySprite.s')
$miscellaneous2Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$treasureInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\treasure.s')
$treasureAndDropsSource = Read-ImportText (
    Join-Path $Disassembly 'code\treasureAndDrops.s')
$pushblockSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\pushblock.s')
$fallDownHoleSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\fallDownHole.s')
$breakTileDebrisSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\breakTileDebris.s')
$bank0Source = Read-ImportText (Join-Path $Disassembly 'code\bank0.s')
$zolEnemySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\zol.s')
$partDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partData.s')
$partActiveCollisionsSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partActiveCollisions.s')
$objectCollisionTableSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\objectCollisionTable.s')
$collisionEffectsSource = Read-ImportText (
    Join-Path $Disassembly 'code\collisionEffects.s')
$tileIndexSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
$roomFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\roomFlags.s')
$musicIdSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$objectSpeedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
if ($pushblockTriggerSource -notmatch '(?ms)^@state0:.*?ld a,TILEINDEX_PUSHABLE_BLOCK.*?ld hl,wNumEnemies\s+inc \(hl\).*?^@state1:.*?^@state2:.*?cp \(hl\)\s+ret z.*?ld a,\$1e.*?^@state3:.*?interactionDecCounter1.*?xor a\s+ld \(wNumEnemies\),a' -or
    $dungeonStuffSource -notmatch '(?ms)^@subid01:\s+call returnIfScrollMode01Unset.*?ld hl,mainScripts\.dropSmallKeyWhenNoEnemiesScript.*?call interactionSetScript.*?^@runScript:' -or
    $commonScriptSource -notmatch '(?ms)^dropSmallKeyWhenNoEnemiesScript:\s+stopifitemflagset.*?checknoenemies\s+spawnitem TREASURE_SMALL_KEY, \$01\s+scriptend' -or
    $switchSource -notmatch '(?ms)^partCode05:\s+jr z,@normalStatus.*?ld a,\(wSwitchState\).*?xor \(hl\)\s+ld \(wSwitchState\),a\s+call @updateTile\s+ld a,SND_SWITCH.*?^@state0:.*?ld \(hl\),\$fa\s+call objectGetShortPosition.*?^@updateTile:.*?TILEINDEX_DUNGEON_SWITCH_OFF.*?inc a.*?jp setTile' -or
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
    $partDataSource -notmatch '(?m)^\s*\.db \$00 \$83 \$44 \$ff \$40 \$08 \$00 \$00 ; \$05' -or
    $partDataSource -notmatch '(?m)^\s*\.db \$00 \$02 \$22 \$00 \$40 \$00 \$00 \$00 ; \$09' -or
    $partActiveCollisionsSource -notmatch '(?m)^\s*dbrev %00001111 %11110110 %00011011 %01111110 ; 0x05' -or
    $objectCollisionTableSource -notmatch '(?ms); ENEMYCOLLISION_SWITCH \(0x03\)\s+\.db(?: \$26){16}\s+\.db \$00 \$00 \$00 \$26(?: \$26){5} \$20 \$20 \$20 \$20 \$20 \$20 \$00' -or
    $collisionEffectsSource -notmatch '(?m)^\s*\.db \$60 \$e4 \$00 \$00 ; ENEMYDMG_34' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_PUSHABLE_BLOCK\s+\$1d' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_DUNGEON_SWITCH_OFF\s+\$0a' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_DUNGEON_SWITCH_ON\s+\$0b' -or
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
    $musicIdSource -notmatch '(?m)^\s*SND_SWITCH\s+db\s+; \$7e' -or
    $musicIdSource -notmatch '(?m)^\s*SND_SPLASH\s+db\s+; \$87' -or
    $musicIdSource -notmatch '(?m)^\s*SND_POOF\s+db\s+; \$98') {
    throw 'Dungeon switch/button/chest/push block and enemy death/hole trigger, timing, tile, or sound contract changed.'
}

if ($interactableTilesSource -notmatch '(?ms)^nextToKeyBlock:.*?specialObjectCheckPushingAgainstTile.*?call decPushingAgainstTileCounter\s+ret nz.*?call checkAndDecKeyCount.*?ld a,\$02\s+jp z,showInfoTextForTile.*?call createKeySpriteInteraction.*?TILEINDEX_STANDARD_FLOOR\s+call setTile.*?SND_OPENCHEST\s+call playSound.*?set ROOMFLAG_BIT_KEYBLOCK,\(hl\).*?INTERAC_PUFF' -or
    $interactableTilesSource -notmatch '(?ms)^nextToKeyDoor:.*?call decPushingAgainstTileCounter\s+jr z,\+\s+dec \(hl\)\s+ret nz.*?call checkAndDecKeyCount.*?call createKeySpriteInteraction.*?INTERAC_DOOR_CONTROLLER.*?call setRoomFlagsForUnlockedKeyDoor' -or
    $interactableTilesSource -notmatch '(?ms)^resetPushingAgainstTileCounter:\s+ld a,20\s+ld \(wPushingAgainstTileCounter\),a' -or
    $doorControllerSource -notmatch '(?ms)^@state2Substate0:.*?ld a,SND_DOORCLOSE.*?call setInterleavedTile.*?ld \(hl\),\$06.*?^@state2Substate1:.*?interactionDecCounter1.*?^@shutterTiles:\s+\.db \$a0 \$70.*?\.db \$a0 \$71.*?\.db \$a0 \$72.*?\.db \$a0 \$73' -or
    $bank0Source -notmatch '(?ms)^setRoomFlagsForUnlockedKeyDoor:.*?^_adjacentRoomsData:\s+\.db \$01 \$f8 \$04 \$00.*?\.db \$02 \$01 \$08 \$00.*?\.db \$04 \$08 \$01 \$00.*?\.db \$08 \$ff \$02 \$00' -or
    $interactableTileDataSource -notmatch '(?ms)^interactableTilesTable:.*?\.dw @overworld\s+\.dw @indoors\s+\.dw @dungeons\s+\.dw @sidescrolling\s+\.dw @underwater\s+\.dw @five.*?^@indoors:.*?^@dungeons:\s+^@five:.*?\.db \$1e \$01.*?\.db \$70 \$02.*?\.db \$77 \$72' -or
    $keyDoorGraphicSource -notmatch '(?ms)^@dungeons:.*?\.db \$1e \$00.*?\.db \$70 \$00.*?\.db \$71 \$00.*?\.db \$72 \$00.*?\.db \$73 \$00' -or
    $standardTileSubstitutionSource -notmatch '(?ms)^@bit7Dungeons:.*?\.db \$a0 \$1e' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_STANDARD_FLOOR\s+\$a0' -or
    $roomFlagSource -notmatch '(?m)^\.define ROOMFLAG_KEYBLOCK\s+\$80' -or
    $dungeonKeySpriteSource -notmatch '(?ms)^@state0:.*?ld \(hl\),\$fc.*?ld \(hl\),\$08.*?ld a,SND_GETSEED.*?^@state1:.*?ld \(hl\),\$14.*?ld \(hl\),\$f8.*?^@state2:' -or
    $objectSpeedSource -notmatch '(?m)^\s*SPEED_60\s+dsb 5 ; 0x0f') {
    throw 'Dungeon key-block/door push, flags, key-sprite, animation, or timing contract changed.'
}

# nextToOverworldKeyhole is shared by every named overworld/dungeon-entrance
# key. It retains the named key, sets ROOMFLAG_BIT_KEYBLOCK ($80), signals the
# associated room script through cfc0 bit 0, and replaces the ordinary key
# sprite with INTERAC_OVERWORLD_KEY_SPRITE ($18).
if ($interactableTilesSource -notmatch '(?ms)^nextToOverworldKeyhole:.*?getThisRoomFlags\s+and \$80\s+ret nz.*?specialObjectCheckPushingAgainstTile.*?checkFacingBottomOfTile.*?decPushingAgainstTileCounter\s+jr z,\+\s+dec \(hl\)\s+ret nz.*?@roomsWithKeyholesTable.*?checkTreasureObtained\s+jr nc,jumpToShowInfoText.*?SND_OPENCHEST.*?set 7,\(hl\).*?ld hl,\$cfc0\s+set 0,\(hl\).*?createKeySpriteInteraction.*?INTERAC_OVERWORLD_KEY_SPRITE.*?sub TREASURE_FIRST_KEY.*?ld a,\$81\s+ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a' -or
    $interactableTilesSource -notmatch '(?ms)^@group0:\s+\.db <ROOM_AGES_05c TREASURE_GRAVEYARD_KEY\s+\.db <ROOM_AGES_00a TREASURE_CROWN_KEY\s+\.db <ROOM_AGES_0a5 TREASURE_LIBRARY_KEY.*?^@group1:\s+\.db <ROOM_AGES_10e TREASURE_OLD_MERMAID_KEY\s+\.db <ROOM_AGES_1a5 TREASURE_LIBRARY_KEY.*?^@group3:\s+\.db <ROOM_AGES_30f TREASURE_MERMAID_KEY' -or
    $interactableTileDataSource -notmatch '(?ms)^@overworld:\s+^@underwater:.*?\.db \$ec \$06.*?^@indoors:\s+\.db \$ae \$06' -or
    $overworldKeySpriteSource -notmatch '(?ms)^@state0:.*?ld bc,-\$200.*?interactionInitGraphics.*?^@state1:.*?ld c,\$28.*?objectUpdateSpeedZ_paramC.*?bit 7,a\s+ret nz.*?ld a,\$3c.*?^@state2:.*?interactionDecCounter1.*?interactionDelete' -or
    $miscellaneous2Source -notmatch '(?ms)^interactiondc_subid01:.*?checkInteractionState\s+jp nz,interactionRunScript.*?getThisRoomFlags\s+and \$80\s+jp nz,interactionDelete.*?mainScripts\.interactiondcSubid01Script.*?interactionSetAlwaysUpdateBit' -or
    $mainObjectSource -notmatch '(?ms)^group0Map5cObjectData:\s+obj_Interaction \$71 \$05 \$40 \$98\s+obj_Interaction \$dc \$01\s+obj_End') {
    throw 'Overworld keyhole table, room 0:5c controller, tile mapping, or key-sprite contract changed.'
}

$overworldKeyholeLocations = @(
    @{ Group = 0; Room = 0x5c; Treasure = 0x42; Source = 'interactableTiles.s:@group0/ROOM_AGES_05c' },
    @{ Group = 0; Room = 0x0a; Treasure = 0x43; Source = 'interactableTiles.s:@group0/ROOM_AGES_00a' },
    @{ Group = 0; Room = 0xa5; Treasure = 0x46; Source = 'interactableTiles.s:@group0/ROOM_AGES_0a5' },
    @{ Group = 1; Room = 0x0e; Treasure = 0x45; Source = 'interactableTiles.s:@group1/ROOM_AGES_10e' },
    @{ Group = 1; Room = 0xa5; Treasure = 0x46; Source = 'interactableTiles.s:@group1/ROOM_AGES_1a5' },
    @{ Group = 3; Room = 0x0f; Treasure = 0x44; Source = 'interactableTiles.s:@group3/ROOM_AGES_30f' }
)
$overworldKeyholeTileRows = @(
    "# active-collisions`ttile`tparameter`tsource"
    "0`tec`t06`tinteractableTiles.s:@overworld"
    "1`tae`t06`tinteractableTiles.s:@indoors"
    "4`tec`t06`tinteractableTiles.s:@underwater"
)

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

$grassDebrisGraphic = $interactionGraphics['0:0']
$redGrassDebrisGraphic = $interactionGraphics['1:0']
$grassDebrisAnimation = Resolve-NpcAnimation 0x00 0
$redGrassDebrisAnimation = Resolve-NpcAnimation 0x01 0
if (-not $grassDebrisGraphic -or
    $grassDebrisGraphic.Gfx -ne 0 -or
    $grassDebrisGraphic.TileBase -ne 0x00 -or
    $grassDebrisGraphic.Palette -ne 0 -or
    $grassDebrisGraphic.DefaultAnimation -ne 0 -or
    [string]::IsNullOrWhiteSpace($grassDebrisAnimation) -or
    -not $redGrassDebrisGraphic -or
    $redGrassDebrisGraphic.Gfx -ne 0 -or
    $redGrassDebrisGraphic.TileBase -ne 0x00 -or
    $redGrassDebrisGraphic.Palette -ne 0 -or
    $redGrassDebrisGraphic.DefaultAnimation -ne 0 -or
    [string]::IsNullOrWhiteSpace($redGrassDebrisAnimation) -or
    $soundIds['SND_CUTGRASS'] -ne 0x6d -or
    $breakTileDebrisSource -notmatch '(?ms)^@state0:.*?interactionInitGraphics.*?^@soundAndPriorityTable:.*?\.db SND_CUTGRASS\s+\$03\s*;\s*0x00.*?\.db SND_CUTGRASS\s+\$03\s*;\s*0x01' -or
    $breakTileDebrisSource -notmatch '(?ms)^@state1:.*?Interaction\.animParameter\s+bit 7,\(hl\)\s+jp nz,interactionDelete.*?jp interactionAnimate' -or
    $breakTileDebrisSource -notmatch '(?ms)^@interac00:.*?wTilesetFlags.*?TILESETFLAG_UNDERWATER.*?ld a,\$0e.*?wGrassAnimationModifier.*?and \$03\s+or \$08.*?Interaction\.oamFlagsBackup') {
    throw 'INTERAC_GRASSDEBRIS $00/$01 graphics, palette, terminal animation, or SND_CUTGRASS changed.'
}
$grassDebrisRows = @(
    "# interaction-id`tsprite`ttile-base`tpalette`tunderwater-palette`tsound`tanimation"
    "00`tspr_common_sprites`t$($grassDebrisGraphic.TileBase)`t$($grassDebrisGraphic.Palette)`t6`t$($soundIds['SND_CUTGRASS'].ToString('x2'))`t$grassDebrisAnimation"
    "01`tspr_common_sprites`t$($redGrassDebrisGraphic.TileBase)`t$($redGrassDebrisGraphic.Palette)`t$($redGrassDebrisGraphic.Palette)`t$($soundIds['SND_CUTGRASS'].ToString('x2'))`t$redGrassDebrisAnimation"
)

$rockDebrisGraphic = $interactionGraphics['6:0']
$rockDebrisAnimation = Resolve-NpcAnimation 0x06 0
$rockDebris2Graphic = $interactionGraphics['12:0']
$rockDebris2Animation = Resolve-NpcAnimation 0x0c 0
if (-not $rockDebrisGraphic -or
    $rockDebrisGraphic.Gfx -ne 0 -or
    $rockDebrisGraphic.TileBase -ne 0x02 -or
    $rockDebrisGraphic.Palette -ne 3 -or
    $rockDebrisGraphic.DefaultAnimation -ne 0 -or
    [string]::IsNullOrWhiteSpace($rockDebrisAnimation) -or
    -not $rockDebris2Graphic -or
    $rockDebris2Graphic.Gfx -ne 0 -or
    $rockDebris2Graphic.TileBase -ne 0x40 -or
    $rockDebris2Graphic.Palette -ne 5 -or
    $rockDebris2Graphic.DefaultAnimation -ne 0 -or
    [string]::IsNullOrWhiteSpace($rockDebris2Animation) -or
    $rockDebris2Animation -ne $rockDebrisAnimation -or
    $soundIds['SND_BREAK_ROCK'] -ne 0xa5 -or
    $breakTileDebrisSource -notmatch '(?ms)^@state0:.*?interactionInitGraphics.*?^@soundAndPriorityTable:.*?\.db SND_BREAK_ROCK\s+\$00\s*;\s*0x06.*?\.db SND_BREAK_ROCK\s+\$00\s*;\s*0x0c' -or
    $breakTileDebrisSource -notmatch '(?ms)^@state1:.*?Interaction\.animParameter\s+bit 7,\(hl\)\s+jp nz,interactionDelete.*?jp interactionAnimate') {
    throw 'INTERAC_ROCKDEBRIS $06/$0c graphics, terminal animation, or SND_BREAK_ROCK changed.'
}
$rockDebrisRows = @(
    "# interaction-id`tsprite`ttile-base`tpalette`tsound`tanimation"
    "06`tspr_common_sprites`t$($rockDebrisGraphic.TileBase)`t$($rockDebrisGraphic.Palette)`t$($soundIds['SND_BREAK_ROCK'].ToString('x2'))`t$rockDebrisAnimation"
    "0c`tspr_common_sprites`t$($rockDebris2Graphic.TileBase)`t$($rockDebris2Graphic.Palette)`t$($soundIds['SND_BREAK_ROCK'].ToString('x2'))`t$rockDebris2Animation"
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
    '(?m)^\s*\.db \$(?<open>[0-9a-f]{2}) \$(?<closed>7[0-7])(?:\s|;)')) {
    $closedTile = $entry.Groups['closed'].Value
    if ($keyDoorOpenTiles.ContainsKey($closedTile)) {
        throw "Duplicate standard dungeon-key door substitution for `$$closedTile."
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
$dungeonKeyActiveCollisions = '1,2,5'
$keyDoorRows.Add(
    "# closed-tile`tdirection`tkey-kind`tkey-graphic`topen-tile`troom-flag`topposite-room-flag`tpush-counter`tdoor-frame-wait`tdoor-sound`tkey-sound`tno-key-text-id`tno-key-utf8-base64`tactive-collisions")
$noKeyTexts = @{
    small = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[0x5100]))
    boss = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[0x5101]))
}
foreach ($entry in [regex]::Matches(
    $interactableTileDataSource,
    '(?m)^\s*\.db \$(?<tile>7[0-7]) \$(?<parameter>[0-7])2\s*$')) {
    $tile = $entry.Groups['tile'].Value
    $parameter = [Convert]::ToInt32($entry.Groups['parameter'].Value, 16)
    $direction = $parameter -band 3
    $keyKind = if ($parameter -ge 4) { 'boss' } else { 'small' }
    $keyGraphic = if ($keyKind -eq 'boss') { '43' } else { '42' }
    $noKeyTextId = if ($keyKind -eq 'boss') { '5101' } else { '5100' }
    if (-not $keyDoorOpenTiles.ContainsKey($tile)) {
        throw "Dungeon-key door `$$tile has no standard opened-tile substitution."
    }
    $directionName = @('up', 'right', 'down', 'left')[$direction]
    if (-not $keyDoorFlags.ContainsKey($directionName)) {
        throw "Small-key door `$$tile has no _adjacentRoomsData flags for $directionName."
    }
    $roomFlag, $oppositeFlag = $keyDoorFlags[$directionName]
    $keyDoorRows.Add(
        "$tile`t$directionName`t$keyKind`t$keyGraphic`t$($keyDoorOpenTiles[$tile])`t$roomFlag`t$oppositeFlag`t20`t6`t112`t94`t$noKeyTextId`t$($noKeyTexts[$keyKind])`t$dungeonKeyActiveCollisions")
}
if ($keyDoorRows.Count -ne 9 -or
    -not ($keyDoorRows -contains "73`tleft`tsmall`t42`ta0`t08`t02`t20`t6`t112`t94`t5100`t$($noKeyTexts.small)`t$dungeonKeyActiveCollisions") -or
    -not ($keyDoorRows -contains "75`tright`tboss`t43`ta0`t02`t08`t20`t6`t112`t94`t5101`t$($noKeyTexts.boss)`t$dungeonKeyActiveCollisions")) {
    throw "Expected eight imported dungeon-key doors `$70-`$77, parsed $($keyDoorRows.Count - 1)."
}

$keyBlockText = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($allTexts[0x5102]))
$keyBlockRows = @(
    "# closed-tile`tkey-graphic`topen-tile`troom-flag`tpush-counter`topen-sound`tkey-sound`tno-key-text-id`tno-key-utf8-base64`tpuff-sound`tactive-collisions`tsource"
    "1e`t42`ta0`t80`t20`t$($soundIds['SND_OPENCHEST'])`t$($soundIds['SND_GETSEED'])`t5102`t$keyBlockText`t$($soundIds['SND_POOF'])`t$dungeonKeyActiveCollisions`tinteractableTiles.s:nextToKeyBlock"
)

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
$enemyClearChestScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\common\commonScripts.s')
if ($enemyClearChestScriptSource -notmatch
    '(?ms)^createChestWhenNoEnemiesScript:.*?stopifitemflagset.*?checknoenemies.*?playsound SND_SOLVEPUZZLE.*?createpuff.*?wait 30.*?settilehere TILEINDEX_CHEST.*?incstate.*?scriptend') {
    throw 'INTERAC_DUNGEON_STUFF $12:$02 enemy-clear chest script changed.'
}
$triggerTranslatorSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\triggerTranslator.s')
if ($triggerTranslatorSource -notmatch
    '(?ms)^@subid2:.*?Interaction\.yh.*?wNumTorchesLit.*?cp b.*?wActiveTriggers.*?or c.*?wActiveTriggers.*?ret.*?wActiveTriggers.*?and c.*?wActiveTriggers.*?ret') {
    throw 'INTERAC_TRIGGER_TRANSLATOR $24:$02 torch-count contract changed.'
}
$tileObjectCreatorSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\createObjectAtEachTileindex.s')
if ($tileObjectCreatorSource -notmatch
    '(?ms)^interactionCodec7:.*?wRoomLayout.*?LARGE_ROOM_HEIGHT\*\$10.*?cp c.*?call z,@createObject.*?Interaction\.xh.*?and \$f0.*?Interaction\.yh.*?Interaction\.xh.*?and \$0f') {
    throw 'INTERAC_CREATE_OBJECT_AT_EACH_TILEINDEX $c7 scan/spawn contract changed.'
}
$torchObjectDataSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData1.s')
if ($torchObjectDataSource -notmatch
    '(?ms)^objectData_makeAllTorchesLightable:\s*obj_Interaction \$c7 \$08 \$06 \$10\s*obj_EndPointer') {
    throw 'objectData_makeAllTorchesLightable no longer creates PART_LIGHTABLE_TORCH $06:$00 at tile $08.'
}
$extendableBridgeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\extendableBridge.s')
$rotatableSeedThingSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\rotatableSeedThing.s')
$respawnableBushSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\respawnableBush.s')
$seedBouncerTileMatch = [regex]::Match(
    $rotatableSeedThingSource,
    '(?ms)^@subid0_state0:.*?call objectMakeTileSolid\s+' +
    'ld h,\$cf\s+ld \(hl\),\$(?<tile>[0-9a-f]{2})')
$seedBouncerCollisionMatch = [regex]::Match(
    $bank0Source,
    '(?ms)^objectMakeTileSolid:\s+call objectGetTileCollisions\s+' +
    'ld \(hl\),\$(?<collision>[0-9a-f]{2})\s+ret')
$seedBouncerChildMatch = [regex]::Match(
    $rotatableSeedThingSource,
    '(?ms)^func_65d5:.*?ld bc,\$(?<offset>[0-9a-f]{4}).*?' +
    'ld l,\$cf\s+ld \(hl\),\$(?<z>[0-9a-f]{2})')
if ($extendableBridgeSource -notmatch
        '(?ms)^interactionCode23:.*?and \$07.*?bitTable.*?wSwitchState.*?' +
        '@bridgeCreationData:.*?@bridgeRemovalData:' -or
    $rotatableSeedThingSource -notmatch
        '(?ms)^partCode33:.*?@subid2:.*?xor b.*?@func_657e:.*?' +
        'add \(hl\).*?and \$03.*?@subid3:' -or
    $respawnableBushSource -notmatch
        '(?ms)^partCode0f:.*?ld \(hl\),\$f0.*?TILEINDEX_RESPAWNING_BUSH_CUT.*?' +
        'getRandomNumber_noPreserveVars.*?PART_ITEM_DROP.*?' +
        'wFrameCounter.*?TILEINDEX_RESPAWNING_BUSH_REGEN.*?' +
        'TILEINDEX_RESPAWNING_BUSH_READY' -or
    -not $seedBouncerTileMatch.Success -or
    -not $seedBouncerCollisionMatch.Success -or
    -not $seedBouncerChildMatch.Success) {
    throw 'Room 4:4e bridge, seed-bouncer, or respawnable-bush source contract changed.'
}
$seedBouncerTile = [Convert]::ToInt32(
    $seedBouncerTileMatch.Groups['tile'].Value, 16)
$seedBouncerCollision = [Convert]::ToInt32(
    $seedBouncerCollisionMatch.Groups['collision'].Value, 16)
$seedBouncerChildOffset = [Convert]::ToInt32(
    $seedBouncerChildMatch.Groups['offset'].Value, 16)
$seedBouncerChildY = ($seedBouncerChildOffset -shr 8) -band 0xff
$seedBouncerChildX = $seedBouncerChildOffset -band 0xff
$seedBouncerChildZ = [Convert]::ToInt32(
    $seedBouncerChildMatch.Groups['z'].Value, 16)
if ($seedBouncerChildZ -ge 0x80) { $seedBouncerChildZ -= 0x100 }
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
$specializedEnemyFallingKeyRooms = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
[void]$specializedEnemyFallingKeyRooms.Add('4:1e')
[void]$specializedEnemyFallingKeyRooms.Add('4:39')
$sourceEnemyFallingKeyCount = 0
$enemyFallingKeyCount = 0
$enemyClearChestCount = 0
$permanentTriggerChestCount = 0
$retractableTriggerChestCount = 0
$torchTranslatorCount = 0
$torchScannerCount = 0
$extendableBridgeCount = 0
$orbCount = 0
$rotatableSeedThingCount = 0
$respawnableBushScannerCount = 0
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
        $enemyFallingKey = $id -eq 0x12 -and $subid -eq 0x01
        if ($enemyFallingKey) { $sourceEnemyFallingKeyCount++ }
        $specializedEnemyFallingKey = $enemyFallingKey -and
            $specializedEnemyFallingKeyRooms.Contains(
                "$mechanicGroup`:$($mechanicRoom.ToString('x2'))")
        $dungeonScriptPredicate = ''
        if ($id -eq 0x20 -and $subid -eq 0x00) {
            $dungeon = Resolve-DungeonMechanicDungeonIndex $mechanicGroup $mechanicRoom
            if ($triggerChestPredicateByDungeon.ContainsKey($dungeon)) {
                $dungeonScriptPredicate = $triggerChestPredicateByDungeon[$dungeon]
            }
        }
        if (-not $specializedEnemyFallingKey -and (
            ($id -eq 0x12 -and $subid -in @(0x01, 0x02)) -or
            ($id -eq 0x13 -and $subid -eq 0x01) -or
            ($id -eq 0x1e -and $subid -ge 0x04 -and $subid -le 0x0b) -or
            ($id -eq 0x23 -and $subid -le 0x07) -or
            ($id -eq 0x24 -and $subid -eq 0x02) -or
            $dungeonScriptPredicate -ne '' -or
            ($id -eq 0x21 -and $subid -in @(0x09, 0x0e, 0x17)))) {
            $a = [Convert]::ToInt32($Matches['a'], 16)
            $b = [Convert]::ToInt32($Matches['b'], 16)
            $position = if ($id -eq 0x12 -or $id -eq 0x13 -or $id -eq 0x20) {
                ($a -band 0xf0) -bor (($b -shr 4) -band 0x0f)
            } else {
                $a
            }
            $parameter = if ($id -eq 0x12 -or $id -eq 0x13) {
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
            } elseif ($id -eq 0x21 -and $subid -eq 0x17) {
                'exact'
            } elseif ($id -eq 0x24 -and $subid -eq 0x02) {
                'exact'
            } else {
                'none'
            }
            $countSourceComplete = if ($conditionalDungeonEnemyRooms.Contains(
                "$mechanicGroup`:$($mechanicRoom.ToString('x2'))")) { 0 } else { 1 }
            $dungeonMechanicRows.Add(
                "$mechanicGroup`t$($mechanicRoom.ToString('x2'))`t$mechanicOrder`t$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($position.ToString('x2'))`t$($parameter.ToString('x2'))`t$triggerPredicate`t$countSourceComplete")
            if ($id -eq 0x12 -and $subid -eq 0x01) {
                $enemyFallingKeyCount++
            }
            if ($id -eq 0x12 -and $subid -eq 0x02) {
                $enemyClearChestCount++
            }
            if ($id -eq 0x20) { $permanentTriggerChestCount++ }
            if ($id -eq 0x21 -and $subid -eq 0x17) {
                $retractableTriggerChestCount++
            }
            if ($id -eq 0x24 -and $subid -eq 0x02) {
                $torchTranslatorCount++
            }
            if ($id -eq 0x23) { $extendableBridgeCount++ }
        }
    } elseif ($line -match '^\s*obj_Interaction\s+\$21\s+\$(?<subid>0a|0c|0d)\s*$') {
        $dungeonMechanicRows.Add(
            "$mechanicGroup`t$($mechanicRoom.ToString('x2'))`t$mechanicOrder`t21`t$($Matches['subid'])`t00`t00`tnone`t1")
    } elseif ($line -match '^\s*obj_Part\s+\$(?<id>03|05|09|24)\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<position>[0-9a-f]{2})\s*$') {
        $dungeonMechanicRows.Add(
            "$mechanicGroup`t$($mechanicRoom.ToString('x2'))`t$mechanicOrder`t$($Matches['id'])`t$($Matches['subid'])`t$($Matches['position'])`t00`tnone`t1")
        if ($Matches['id'] -eq '03') { $orbCount++ }
    } elseif ($line -match '^\s*obj_Part\s+\$33\s+\$0a\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})\s+\$(?<mask>[0-9a-f]{2})\s*$') {
        $y = [Convert]::ToInt32($Matches['y'], 16)
        $x = [Convert]::ToInt32($Matches['x'], 16)
        $position = ($y -band 0xf0) -bor (($x -shr 4) -band 0x0f)
        $dungeonMechanicRows.Add(
            "$mechanicGroup`t$($mechanicRoom.ToString('x2'))`t$mechanicOrder`t33`t0a`t$($position.ToString('x2'))`t$($Matches['mask'])`tnone`t1")
        $rotatableSeedThingCount++
    } elseif ($line -match '^\s*obj_Pointer\s+objectData_makeAllTorchesLightable\s*$') {
        $dungeonMechanicRows.Add(
            "$mechanicGroup`t$($mechanicRoom.ToString('x2'))`t$mechanicOrder`tc7`t08`t06`t10`tnone`t1")
        $torchScannerCount++
    } elseif ($line -match '^\s*obj_Pointer\s+objectData_respawningBush(?<drop>Bombs|ScentSeeds)\s*$') {
        $drop = if ($Matches['drop'] -eq 'Bombs') { 0x04 } else { 0x06 }
        $dungeonMechanicRows.Add(
            "$mechanicGroup`t$($mechanicRoom.ToString('x2'))`t$mechanicOrder`tc7`t04`t0f`t$((0x10 -bor $drop).ToString('x2'))`tnone`t1")
        $respawnableBushScannerCount++
    }
    $mechanicOrder++
}
if ($dungeonMechanicRows.Count -ne 228 -or
    $sourceEnemyFallingKeyCount -ne 4 -or
    $enemyFallingKeyCount -ne 2 -or
    $enemyClearChestCount -ne 12 -or
    $permanentTriggerChestCount -ne 7 -or
    $retractableTriggerChestCount -ne 6 -or
    $torchTranslatorCount -ne 2 -or
    $torchScannerCount -ne 8 -or
    $extendableBridgeCount -ne 7 -or
    $orbCount -ne 17 -or
    $rotatableSeedThingCount -ne 2 -or
    $respawnableBushScannerCount -ne 3 -or
    -not ($dungeonMechanicRows -contains "4`t08`t0`t20`t00`t57`t01`texact`t1") -or
    -not ($dungeonMechanicRows -contains "4`t08`t1`t09`t00`t17`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t09`t0`t1e`t04`t07`t00`tbit`t1") -or
    -not ($dungeonMechanicRows -contains "4`t09`t1`t1e`t05`t5e`t00`tbit`t1") -or
    -not ($dungeonMechanicRows -contains "4`t09`t3`t13`t01`t2a`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t09`t5`t09`t00`t14`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t22`t1`t09`t80`t5b`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t2f`t5`t05`t02`t79`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t65`t0`t12`t02`t58`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4b`t0`t13`t01`t6b`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4b`t1`t12`t01`t58`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t56`t0`t21`t0a`t00`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4e`t0`t23`t01`t39`t02`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4e`t1`t23`t01`t42`t03`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4e`t2`t23`t01`t4c`t04`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4e`t3`t03`t02`t31`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4e`t4`t03`t03`t3d`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4e`t5`t05`t02`t68`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4e`t6`t33`t0a`t18`t0c`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t4e`t7`tc7`t04`t0f`t16`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t59`t0`t24`t02`t01`t01`texact`t1") -or
    -not ($dungeonMechanicRows -contains "4`t59`t1`t1e`t06`ta3`t00`tbit`t1") -or
    -not ($dungeonMechanicRows -contains "4`t59`t2`tc7`t08`t06`t10`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t5e`t0`t21`t0c`t00`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t5e`t1`t09`t00`t19`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t5d`t0`t21`t0d`t00`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t5d`t1`t24`t10`t23`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t61`t0`t21`t0d`t00`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t61`t1`t21`t0e`t58`tb8`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t61`t2`t24`t40`t57`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t64`t0`t21`t09`t68`tb8`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t7a`t0`t21`t17`t39`t01`texact`t1") -or
    -not ($dungeonMechanicRows -contains "4`t0c`t0`t13`t01`t47`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t0c`t1`t1e`t08`t07`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t0b`t0`t1e`t08`t07`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t0b`t1`t1e`t0b`t50`t00`tnone`t1") -or
    -not ($dungeonMechanicRows -contains "4`t13`t0`t1e`t08`t07`t00`tnone`t0")) {
    throw "Expected 229 reusable dungeon mechanics including room 4:4b's push-trigger/falling-key pair and room 4:4e's bridges, orbs, switch, rotating seed bouncer, and respawnable Scent Seed bushes; parsed $($dungeonMechanicRows.Count - 1)."
}
$moonlitCrystalSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\grottoCrystal.s')
$moonlitEventSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\dungeonEvents.s')
$moonlitScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\dungeonScripts.s')
$moonlitScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$moonlitScriptingSource = Read-ImportText (
    Join-Path $Disassembly 'code\scripting.s')
$moonlitSarcophagusSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\sarcophagus.s')
$moonlitPartDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partData.s')
$moonlitExtraObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData3.s')
if ($moonlitPartDataSource -notmatch
        '(?m)^\s*\.db \$74 \$83 \$44 \$00 \$40 \$1e \$00 \$00 ; \$03\s*$' -or
    $moonlitEventSource -notmatch
        '(?ms)^interaction21_subid0a:.*?res 4,\(hl\).*?' +
        'objectData\.moonlitGrotto_orb.*?interactionDeleteAndRetIfItemFlagSet.*?' +
        'bit 4,\(hl\).*?ld \(\$cca2\),a.*?' +
        'objectData\.moonlitGrotto_onOrbActivation' -or
    $moonlitExtraObjectSource -notmatch
        '(?ms)^moonlitGrotto_orb:\s+obj_Part \$03 \$04 \$75\s+obj_End' -or
    $moonlitExtraObjectSource -notmatch
        '(?ms)^moonlitGrotto_onOrbActivation:\s+' +
        'obj_Interaction \$12 \$02 \$68 \$98\s+' +
        'obj_SpecificEnemyA \$00 \$1d \$00 \$26 \$a0\s+obj_End' -or
    $moonlitEventSource -notmatch
        '(?ms)^interaction21_subid0c:\s+ld a,\(wActiveTriggers\)\s+' +
        'or a\s+ret z\s+ld \(\$cca2\),a\s+' +
        'ld hl,objectData\.moonlitGrotto_onArmosSwitchPressed\s+' +
        'call parseGivenObjectData\s+jp interactionDelete' -or
    $dungeonStuffSource -notmatch
        '(?ms)^@subid01:\s+call returnIfScrollMode01Unset.*?' +
        'ld hl,mainScripts\.dropSmallKeyWhenNoEnemiesScript.*?' +
        'call interactionSetScript.*?^@runScript:' -or
    $enemyClearChestScriptSource -notmatch
        '(?ms)^dropSmallKeyWhenNoEnemiesScript:\s+' +
        'stopifitemflagset.*?checknoenemies\s+' +
        'spawnitem TREASURE_SMALL_KEY, \$01\s+scriptend' -or
    $moonlitPartDataSource -notmatch
        '(?m)^\s*\.db \$76 \$83 \$44 \$00 \$40 \$12 \$01 \$00 ; \$24\s*$' -or
    # partCheckCollisions indexes this table with Part.collisionType, which
    # partLoadGraphicsAndProperties derives from Part.id ($24), while $83 in
    # partData supplies enemyCollisionMode $03. In row $24, collision $18 is
    # disabled and $19 is enabled after dbrev/bitTable indexing.
    $partActiveCollisionsSource -notmatch
        '(?m)^\s*dbrev %00001111 %11110110 %00010001 %01111110 ; 0x24\s*$' -or
    $itemCollisionTypesSource -notmatch
        '(?m)^\s*ITEMCOLLISION_BOMB\s+db ; \$18: Bomb, bombchu\s*$' -or
    $moonlitCrystalSource -notmatch
        '(?ms)^partCode24:.*?xor \(hl\).*?ld \(wSwitchState\),a.*?' +
        'ldbc, INTERAC_SARCOPHAGUS \$80.*?bit 6,\(hl\).*?' +
        'call objectMakeTileSolid.*?ld h,\$cf\s+ld \(hl\),\$0a' -or
    $moonlitEventSource -notmatch
        '(?ms)^interaction21_subid0d:.*?GLOBALFLAG_D3_CRYSTALS.*?' +
        'and \$40.*?and \$f0\s+cp \$f0.*?ld \(wSpinnerState\),a' -or
    $moonlitEventSource -notmatch
        '(?ms)^interaction21_subid0e:.*?wRoomLayout\+\$4a.*?' +
        'cp \$2a.*?spawnSmallKeyFromCeiling' -or
    $moonlitEventSource -notmatch
        '(?ms)^interaction21_subid09:.*?interactionDeleteAndRetIfItemFlagSet.*?' +
        'ld hl,@tileData.*?verifyTilesAndDropSmallKey.*?^@tileData:\s*' +
        '\.db TILEINDEX_PUSHABLE_BLOCK \$3b \$59 \$5d \$00' -or
    $moonlitScriptSource -notmatch
        '(?ms)^moonlitGrottoScript_brokeCrystal:\s+disableinput.*?wait 30.*?' +
        'playsound SNDCTRL_STOPSFX.*?shakescreen 180.*?' +
        'playsound SND_RUMBLE2.*?wait 180.*?showtext TX_1200.*?' +
        'orroomflag \$40' -or
    $moonlitScriptSource -notmatch
        '(?ms)^moonlitGrottoScript_brokeAllCrystals:.*?wait 30.*?' +
        'shakescreen 100.*?playsound SND_BIG_EXPLOSION.*?wait 90.*?' +
        'playsound SND_SOLVEPUZZLE.*?wait 30.*?showtext TX_1201.*?' +
        'setglobalflag GLOBALFLAG_D3_CRYSTALS' -or
    $moonlitScriptingSource -notmatch
        '(?ms)^scriptCmd_disableInput:\s+ld a,\$81\s+' +
        'ld \(wDisabledObjects\),a.*?^scriptCmd_disableMenu:' -or
    $moonlitScriptHelperSource -notmatch
        '(?ms)^moonlitGrotto_enableControlAfterBreakingCrystal:\s+xor a\s+' +
        'ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a.*?' +
        'ld \(wDisableScreenTransitions\),a\s+ld \(wDisableWarpTiles\),a' -or
    $moonlitSarcophagusSource -notmatch
        '(?ms)^interactionCode82:.*?@break:.*?ld \(hl\),\$02.*?' +
        'ld a,SND_KILLENEMY\s+call z,playSound') {
    throw 'Moonlit Grotto orb/Armos, crystal, cutscene, or falling-key source contract changed.'
}

$moonlitButtonArmosMatch = [regex]::Match(
    $moonlitExtraObjectSource,
    '(?ms)^moonlitGrotto_onArmosSwitchPressed:\s+' +
    'obj_Interaction \$12 \$01 \$(?<keyY>[0-9a-f]{2}) \$(?<keyX>[0-9a-f]{2})\s+' +
    'obj_SpecificEnemyA \$00 \$1d \$00 \$(?<source>[0-9a-f]{2}) \$(?<replacement>[0-9a-f]{2})\s+' +
    'obj_End')
if (-not $moonlitButtonArmosMatch.Success) {
    throw 'Moonlit Grotto button/Armos dynamic object list could not be parsed.'
}
$moonlitButtonKeyY = [Convert]::ToInt32(
    $moonlitButtonArmosMatch.Groups['keyY'].Value, 16)
$moonlitButtonKeyX = [Convert]::ToInt32(
    $moonlitButtonArmosMatch.Groups['keyX'].Value, 16)
$moonlitArmosSourceTile = [Convert]::ToInt32(
    $moonlitButtonArmosMatch.Groups['source'].Value, 16)
$moonlitArmosReplacementTile = [Convert]::ToInt32(
    $moonlitButtonArmosMatch.Groups['replacement'].Value, 16)
if ($moonlitButtonKeyY -ne 0x58 -or $moonlitButtonKeyX -ne 0x58 -or
    $moonlitArmosSourceTile -ne 0x26 -or
    $moonlitArmosReplacementTile -ne 0xa0) {
    throw 'Moonlit Grotto button/Armos dynamic object constants changed.'
}

$room464PatternMatch = [regex]::Match(
    $moonlitEventSource,
    '(?ms)^interaction21_subid09:.*?^@tileData:\s*' +
    '\.db\s+(?<tile>TILEINDEX_[A-Z0-9_]+)\s+' +
    '(?<positions>(?:\$[0-9a-f]{2}\s+)+)\$00')
if (-not $room464PatternMatch.Success) {
    throw 'INTERAC_DUNGEON_EVENTS $21:$09 tile pattern could not be parsed.'
}
$room464TileSymbol = $room464PatternMatch.Groups['tile'].Value
$room464TileMatch = [regex]::Match(
    $tileIndexSource,
    "(?m)^\.define\s+$([regex]::Escape($room464TileSymbol))\s+\`$(?<tile>[0-9a-f]{2})\b")
if (-not $room464TileMatch.Success) {
    throw "Tile constant $room464TileSymbol used by `$21:`$09 could not be resolved."
}
$room464Tile = [Convert]::ToInt32(
    $room464TileMatch.Groups['tile'].Value, 16)
$room464Positions = [regex]::Matches(
    $room464PatternMatch.Groups['positions'].Value,
    '\$(?<position>[0-9a-f]{2})')
$dungeonEventTilePatternRows = [Collections.Generic.List[string]]::new()
$dungeonEventTilePatternRows.Add("# id`tsubid`torder`ttile`tposition`tsource")
for ($index = 0; $index -lt $room464Positions.Count; $index++) {
    $position = [Convert]::ToInt32(
        $room464Positions[$index].Groups['position'].Value, 16)
    $dungeonEventTilePatternRows.Add(
        "21`t09`t$index`t$($room464Tile.ToString('x2'))`t$($position.ToString('x2'))`tobject_code/ages/interactions/dungeonEvents.s:interaction21_subid09@tileData")
}

foreach ($kind in @('creation', 'removal')) {
    for ($variant = 0; $variant -lt 7; $variant++) {
        $bridgePattern = [regex]::Match(
            $extendableBridgeSource,
            "(?ms)^@$kind$variant`:\s*\.db\s+" +
            '(?<tile>TILEINDEX_[A-Z0-9_]+)(?<add>\+[0-9]+)?\s+' +
            '(?<positions>(?:\$[0-9a-f]{2}\s+)+)\$ff')
        if (-not $bridgePattern.Success) {
            throw "INTERAC_EXTENDABLE_BRIDGE @$kind$variant pattern could not be parsed."
        }
        $bridgeTileSymbol = $bridgePattern.Groups['tile'].Value
        $bridgeTileMatch = [regex]::Match(
            $tileIndexSource,
            "(?m)^\.define\s+$([regex]::Escape($bridgeTileSymbol))\s+\`$(?<tile>[0-9a-f]{2})\b")
        if (-not $bridgeTileMatch.Success) {
            throw "Tile constant $bridgeTileSymbol used by INTERAC_EXTENDABLE_BRIDGE could not be resolved."
        }
        $bridgeTile = [Convert]::ToInt32(
            $bridgeTileMatch.Groups['tile'].Value, 16)
        if ($bridgePattern.Groups['add'].Success) {
            $bridgeTile += [Convert]::ToInt32(
                $bridgePattern.Groups['add'].Value.Substring(1), 10)
        }
        $bridgePositions = [regex]::Matches(
            $bridgePattern.Groups['positions'].Value,
            '\$(?<position>[0-9a-f]{2})')
        $patternSubId = $variant -bor $(if ($kind -eq 'removal') { 0x80 } else { 0 })
        for ($index = 0; $index -lt $bridgePositions.Count; $index++) {
            $position = [Convert]::ToInt32(
                $bridgePositions[$index].Groups['position'].Value, 16)
            $dungeonEventTilePatternRows.Add(
                "23`t$($patternSubId.ToString('x2'))`t$index`t$($bridgeTile.ToString('x2'))`t$($position.ToString('x2'))`tobject_code/ages/interactions/extendableBridge.s:@$kind$variant")
        }
    }
}
if ($dungeonEventTilePatternRows.Count -ne 54 -or
    -not ($dungeonEventTilePatternRows -contains
        "21`t09`t0`t1d`t3b`tobject_code/ages/interactions/dungeonEvents.s:interaction21_subid09@tileData") -or
    -not ($dungeonEventTilePatternRows -contains
        "21`t09`t1`t1d`t59`tobject_code/ages/interactions/dungeonEvents.s:interaction21_subid09@tileData") -or
    -not ($dungeonEventTilePatternRows -contains
        "21`t09`t2`t1d`t5d`tobject_code/ages/interactions/dungeonEvents.s:interaction21_subid09@tileData") -or
    -not ($dungeonEventTilePatternRows -contains
        "23`t02`t0`t6d`t39`tobject_code/ages/interactions/extendableBridge.s:@creation2") -or
    -not ($dungeonEventTilePatternRows -contains
        "23`t82`t3`tf4`t39`tobject_code/ages/interactions/extendableBridge.s:@removal2")) {
    throw 'Expected the three $21:$09 goals and 50 source-ordered INTERAC_EXTENDABLE_BRIDGE creation/removal tiles.'
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
    "bridge-step-wait`t10"
    "bridge-first-tile`t106"
    "bridge-tile-count`t6"
    "button-tile`t12"
    "pressed-button-tile`t13"
    "button-radius-y`t2"
    "button-radius-x`t2"
    "button-object-release-delay`t28"
    "button-sound`t135"
    "switch-off-tile`t10"
    "switch-on-tile`t11"
    "switch-radius-y`t4"
    "switch-radius-x`t4"
    "switch-collision-z`t-6"
    "switch-hit-lockout`t28"
    "switch-sound`t126"
    "chest-tile`t241"
    "chest-wait`t15"
    "puff-sound`t152"
    "respawning-bush-cut-tile`t2"
    "respawning-bush-regen-tile`t3"
    "respawning-bush-ready-tile`t4"
    "respawning-bush-delay`t240"
    "respawning-bush-regen-wait`t12"
    "respawning-bush-ready-wait`t8"
    "respawning-bush-radius-y`t3"
    "respawning-bush-radius-x`t3"
    "moonlit-global-flag`t$($globalFlagValues['GLOBALFLAG_D3_CRYSTALS'])"
    "moonlit-all-crystals-mask`t240"
    "moonlit-room-flag`t64"
    "moonlit-crystal-collision`t10"
    "moonlit-crystal-radius-y`t4"
    "moonlit-crystal-radius-x`t4"
    "moonlit-orb-position`t117"
    "moonlit-orb-mask`t16"
    "moonlit-orb-collision`t10"
    "moonlit-orb-radius-y`t4"
    "moonlit-orb-radius-x`t4"
    "seed-bouncer-background-tile`t$seedBouncerTile"
    "seed-bouncer-tile-collision`t$seedBouncerCollision"
    "seed-bouncer-child-y`t$seedBouncerChildY"
    "seed-bouncer-child-x`t$seedBouncerChildX"
    "seed-bouncer-child-z`t$seedBouncerChildZ"
    "moonlit-armos-chest-position`t105"
    "moonlit-button-key-y`t$moonlitButtonKeyY"
    "moonlit-button-key-x`t$moonlitButtonKeyX"
    "moonlit-armos-source-tile`t$moonlitArmosSourceTile"
    "moonlit-armos-replacement-tile`t$moonlitArmosReplacementTile"
    "moonlit-key-goal-position`t74"
    "moonlit-key-goal-tile`t42"
    "moonlit-first-wait`t30"
    "moonlit-rumble-wait`t180"
    "moonlit-all-wait`t30"
    "moonlit-explosion-wait`t90"
    "moonlit-solve-wait`t30"
    "moonlit-rumble-sound`t$($soundIds['SND_RUMBLE2'])"
    "moonlit-big-explosion-sound`t$($soundIds['SND_BIG_EXPLOSION'])"
    "moonlit-solve-sound`t$($soundIds['SND_SOLVEPUZZLE'])"
    "moonlit-break-sound`t$($soundIds['SND_KILLENEMY'])"
    "moonlit-break-sound-delay`t2"
)
$dungeonMechanicTextRows = @(
    "# text-id`tmessage-base64"
    "1200`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x1200])))"
    "1201`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x1201])))"
)
$currentGroup = -1
$currentRoom = -1
$npcSpriteNames = [Collections.Generic.HashSet[string]]::new()
[void]$npcSpriteNames.Add($tingleSparkleSprite)

$overworldKeyholeRows = [Collections.Generic.List[string]]::new()
$overworldKeyholeRows.Add(
    "# group`troom`ttreasure`tsubid`tsprite`ttile-base`tpalette`tanimation`tsource")
foreach ($location in $overworldKeyholeLocations) {
    $subid = [int]$location.Treasure - 0x42
    $graphic = $interactionGraphics["24`:$subid"]
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw "Could not resolve INTERAC_OVERWORLD_KEY_SPRITE `$18 subid `$$($subid.ToString('x2'))."
    }
    $animation = Resolve-NpcAnimation 0x18 $graphic.DefaultAnimation
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve INTERAC_OVERWORLD_KEY_SPRITE `$18 subid `$$($subid.ToString('x2')) animation."
    }
    $spriteName = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($spriteName)
    $overworldKeyholeRows.Add((@(
        ([int]$location.Group).ToString(),
        ([int]$location.Room).ToString('x2'),
        ([int]$location.Treasure).ToString('x2'),
        $subid.ToString('x2'),
        $spriteName,
        ([int]$graphic.TileBase).ToString('x2'),
        ([int]$graphic.Palette).ToString('x2'),
        $animation,
        [string]$location.Source
    ) -join "`t"))
}
$graveyardKeyholeRow = @($overworldKeyholeRows | Where-Object {
    $_ -match '^0\t5c\t42\t00\t'
})
if ($overworldKeyholeRows.Count -ne 7 -or $graveyardKeyholeRow.Count -ne 1 -or
    $graveyardKeyholeRow[0] -notmatch '^0\t5c\t42\t00\tspr_map_compass_keys_bookofseals\t0e\t05\t') {
    throw "Expected six named Ages keyholes with Graveyard Key visual `$7a/`$0e/`$05."
}
$keyholeText = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($allTexts[0x5109]))
$overworldKeyholeConstantRows = @(
    "# room-flag`tinformative-mask`tpush-counter`topen-sound`tno-key-text-id`tno-key-utf8-base64`tinteraction-id`tfirst-key`tinitial-speed-z`tgravity`thold-frames`tsource"
    "80`t20`t20`t108`t5109`t$keyholeText`t18`t42`t-512`t40`t60`tinteractableTiles.s:nextToOverworldKeyhole;overworldKeySprite.s"
)

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
if ($npcRows.Count -ne 375) {
    throw "Expected 374 clean-US positioned NPC/character records from Ages mainData.s, parsed $($npcRows.Count - 1)."
}
$room1adTokayRows = @($npcRows | Where-Object {
    $_ -match '^1\tad\t48\t15\t'
})
if ($room1adTokayRows.Count -ne 1 -or
    $room1adTokayRows[0] -notmatch '^1\tad\t48\t15\t56\t68\t00\t') {
    throw 'Room 1:ad must contain only the clean-US Tokay `$48:$15 at `$56,$68.'
}
$linkedGhiniRow = @($npcRows | Where-Object { $_ -match '^0\t5d\tcb\t00\t68\t88\t' })
if ($linkedGhiniRow.Count -ne 1 -or
    ($linkedGhiniRow[0] -split "`t")[7] -ne '4d05' -or
    ($linkedGhiniRow[0] -split "`t")[10] -ne '2') {
    throw 'Room 0:5d linked-game Ghini no longer resolves TX_4d05 and oamFlags `$02.'
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

# Tokay Island's visible `$48 actors share one graphics family but split into
# ordinary dialogue, script/native NPCs, the trading-hut `$81 items, and the
# dynamic Wild Tokay controller. Export the common source facts once so those
# owners retain the original subid dispatch without parsing the disassembly at
# runtime.
$tokaySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\tokay.s')
$tokayScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$tokayHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$decorationSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\decoration.s')
$tokayShopSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\tokayShopItem.s')
$wildTokaySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\wildTokayController.s')
$wildTokayMeatSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\tokayMeat.s')
$wildTokayAccessorySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\accessory.s')
$wildTokayObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData3.s')
$agesWramSource = Read-ImportText (Join-Path $Disassembly 'include\wram.s')
$tokayShopCollisionMatch = [regex]::Match(
    $tokayShopSource,
    '(?ms)^@state0:.*?ld a,\$(?<radius>[0-9a-f]{2})\s+call objectSetCollideRadius.*?^@state1:\s+call interactionAnimateAsNpc')
$wildTokayManagerLinkPositionMatch = [regex]::Match(
    $tokaySource,
    '(?ms)^@initSubid0d:.*?bit 6,a.*?ld hl,w1Link\.yh\s+ld \(hl\),\$(?<y>[0-9a-f]{2})\s+ld l,<w1Link\.xh\s+ld \(hl\),\$(?<x>[0-9a-f]{2})\s+xor a\s+ld l,<w1Link\.direction')
if ($tokaySource -notmatch
        '(?ms)^interactionCode48:.*?^@initSubid05:.*?^@initSubid1f:.*?^tokayState1:.*?^tokayScriptTable:' -or
    $tokaySource -notmatch
        '(?ms)^@textIndices:\s+\.db <TX_0a64.*?\.db <TX_0a63' -or
    $tokaySource -notmatch
        '(?ms)^tokayItemGraphics:\s+\.db \$10 \$1b \$68 \$31 \$20' -or
    $tokaySource -notmatch
        '(?ms)^tokayRunSubid0d:.*?^@substate0:.*?SNDCTRL_MEDIUM_FADEOUT.*?fadeoutToWhite.*?^@substate1:.*?wPaletteThread_mode.*?clearAllItemsAndPutLinkOnGround.*?interactionDelete' -or
    $tokayHelperSource -notmatch
        '(?ms)^tokayGiveItemToLink:.*?ld b,\$06.*?cp \$06.*?jr z,\+.*?ld b,\$01.*?ld \(hl\),b.*?cp \$0a' -or
    $tokayHelperSource -notmatch
        '(?ms)^tokayGame_determinePrizeAndCheckRupees:.*?^@gfx:\s+\.db \$3e \$2b \$2c \$0d \$2d \$0e.*?^tokayGame_createAccessoryForPrize:\s+call interactionSetAnimation.*?INTERAC_ACCESSORY.*?Interaction\.var03.*?Interaction\.relatedObj1' -or
    $tokayScriptSource -notmatch
        '(?ms)^tokayHoldingItemScript:.*?^tokayRunningFromRosaScript:.*?^tokayGameManagerScript_past:.*?^tokayShopkeeperScript:.*?^tokayWithDimitri1Script:.*?^tokayWithDimitri2Script:.*?^tokayAtSeedlingPlotScript:.*?^tokayGameManagerScript_present:' -or
    $tokayHelperSource -notmatch
        '(?ms)^tokayWithShieldUpgradeScript:.*?^tokayExplainingVinesScript:.*?^tokayCookScript:' -or
    $tokayShopSource -notmatch
        '(?ms)^interactionCode81:.*?^@initialShopTreasures:\s+\.db TREASURE_FEATHER, TREASURE_BRACELET.*?^@seedsNeededToBuyItems:.*?^@boughtItemGlobalflags:' -or
    -not $tokayShopCollisionMatch.Success -or
    -not $wildTokayManagerLinkPositionMatch.Success -or
    $wildTokaySource -notmatch
        '(?ms)^interactionCode70:.*?^@var3bValues:\s+\.db \$05 \$05 \$05 \$06 \$07.*?^@tilesToReplaceOnStart:.*?^@data_5898:.*?^@table:' -or
    $wildTokaySource -notmatch
        '(?ms)^@tilesToReplaceOnStart:\s+\.db \$ef \$01\s+\.db \$ef \$08\s+\.db \$ef \$71\s+\.db \$ef \$78\s+\.db \$7a \$74\s+\.db \$7a \$75' -or
    $wildTokaySource -notmatch
        '(?ms)^@substate0:.*?ld \(hl\),30.*?^@substate1:.*?ld \(hl\),10.*?MUS_MINIGAME.*?fadeinFromWhite.*?^@substate2:.*?interactionDecCounter1IfPaletteNotFading.*?TX_0a16' -or
    $wildTokayMeatSource -notmatch
        '(?ms)^interactionCode8c:.*?ld \(hl\),30.*?objectSetCollideRadius.*?ld bc,\$3850.*?ld \(hl\),-\$40.*?^@state2:.*?^@state3:' -or
    $wildTokayMeatSource -notmatch
        '(?ms)^@@substate0:.*?SND_FALLINHOLE.*?^@@substate1:.*?ld c,\$28.*?objectUpdateSpeedZ_paramC.*?SND_BOMB_LAND' -or
    $wildTokayMeatSource -notmatch
        '(?ms)^@state2:.*?^@justGrabbed:.*?activeMeatObject.*?getFreeInteractionSlot.*?INTERAC_TOKAY_MEAT.*?interactionIncSubstate.*?^@released:.*?dropLinkHeldItem' -or
    $tokaySource -notmatch
        '(?ms)^wildTokayParticipant_checkGrabMeat:.*?ld a,\$0a.*?interactionIncSubstate.*?ld \(hl\),\$06.*?ld a,\$07.*?add \(hl\).*?tokayInitMeatAccessory:.*?INTERAC_ACCESSORY.*?ld \(hl\),\$73.*?inc \(hl\).*?Interaction\.relatedObj1.*?^wildTokayParticipantSubstate1:.*?interactionDecCounter1' -or
    $wildTokayAccessorySource -notmatch
        '(?ms)^@data:\s+\.db \$00 \$f3 \$80 \$03\s+\.db \$f3 \$00 \$80 \$03\s+\.db \$00 \$0d \$80 \$03\s+\.db \$f4 \$ff \$80 \$03\s+\.db \$f4 \$00 \$80 \$03' -or
    $wildTokayObjectSource -notmatch
        '(?ms)^wildTokayObjectTable:.*?^@tokayFromLeft:.*?\$48 \$0c \$f8 \$18.*?^@tokayFromRight:.*?\$48 \$0c \$f8 \$88.*?^@tokayOnBothSides:' -or
    $agesWramSource -notmatch '(?m)^wDimitriState: ; \$c647/' -or
    $agesWramSource -notmatch '(?m)^wWildTokayGameLevel: ; \$c6ea') {
    throw 'Tokay Island NPC, shop, or Wild Tokay source contract changed.'
}
$tokayShopCollisionRadius = [Convert]::ToInt32(
    $tokayShopCollisionMatch.Groups['radius'].Value, 16)
if ($tokayShopCollisionRadius -ne 0x06) {
    throw "INTERAC_TOKAY_SHOP_ITEM collision radius is no longer `$06."
}
$wildTokayManagerLinkY = [Convert]::ToInt32(
    $wildTokayManagerLinkPositionMatch.Groups['y'].Value, 16)
$wildTokayManagerLinkX = [Convert]::ToInt32(
    $wildTokayManagerLinkPositionMatch.Groups['x'].Value, 16)
$wildTokayReturnWarpMatches = [regex]::Matches(
    $wildTokaySource,
    '(?m)^\s*m_HardcodedWarpA ROOM_AGES_2(?<room>de|e5), \$00, \$(?<position>[0-9a-f]{2}), \$03\s*$')
$wildTokayReturnRooms = @(
    $wildTokayReturnWarpMatches |
        ForEach-Object { $_.Groups['room'].Value } |
        Sort-Object -Unique)
$wildTokayReturnPositions = @(
    $wildTokayReturnWarpMatches |
        ForEach-Object { $_.Groups['position'].Value } |
        Sort-Object -Unique)
if ($wildTokayReturnWarpMatches.Count -ne 2 -or
    $wildTokayReturnRooms.Count -ne 2 -or
    $wildTokayReturnPositions.Count -ne 1) {
    throw 'Could not resolve the shared past/present Wild Tokay return-warp position.'
}
$wildTokayReturnPosition = [Convert]::ToInt32(
    $wildTokayReturnPositions[0], 16)
$wildTokayCycleMatch = [regex]::Match(
    $wildTokaySource,
    '(?m)^@var3bValues:\s*\r?\n\s*\.db(?<values>(?: \$[0-9a-f]{2}){5})\s*$')
$wildTokayPatternMatch = [regex]::Match(
    $wildTokaySource,
    '(?ms)^@data_5898:\s*(?<values>(?:\.db(?: \$[0-9a-f]{2}){4}\s*){8})')
$wildTokayRandomTableMatch = [regex]::Match(
    $wildTokaySource,
    '(?ms)^@table:\s*(?<values>(?:\.db(?: \$[0-9a-f]{2}){16}\s*){5})')
if (-not $wildTokayCycleMatch.Success -or
    -not $wildTokayPatternMatch.Success -or
    -not $wildTokayRandomTableMatch.Success) {
    throw 'Could not parse the Wild Tokay cycle, pattern, or random-selection tables.'
}
$wildTokayCycleCounts = @(
    [regex]::Matches($wildTokayCycleMatch.Groups['values'].Value, '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups['value'].Value, 16) })
$wildTokayPatternValues = @(
    [regex]::Matches($wildTokayPatternMatch.Groups['values'].Value, '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups['value'].Value, 16) })
$wildTokayRandomPatternValues = @(
    [regex]::Matches($wildTokayRandomTableMatch.Groups['values'].Value, '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups['value'].Value, 16) })
if ($wildTokayCycleCounts.Count -ne 5 -or
    $wildTokayPatternValues.Count -ne 32 -or
    $wildTokayRandomPatternValues.Count -ne 80) {
    throw 'Wild Tokay cycle or pattern table dimensions changed.'
}

$tokayTextRows = [Collections.Generic.List[string]]::new()
$tokayTextRows.Add("# text-id`tutf8-base64")

function Resolve-TokayText(
    [int]$textId,
    [Collections.Generic.HashSet[int]]$visited
) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Tokay Island TX_$($textId.ToString('x4'))."
    }
    if ($visited.Contains($textId)) {
        throw "Tokay Island TX_$($textId.ToString('x4')) has recursive text control flow."
    }
    [void]$visited.Add($textId)
    $message = [string]$allTexts[$textId]
    while ($true) {
        $call = [regex]::Match($message, '\\call\(TX_(?<id>[0-9a-f]{4})\)')
        if (-not $call.Success) { break }
        $calledId = [Convert]::ToInt32($call.Groups['id'].Value, 16)
        $calledText = Resolve-TokayText $calledId $visited
        $message = $message.Substring(0, $call.Index) + $calledText +
            $message.Substring($call.Index + $call.Length)
    }
    $jump = [regex]::Match($message, '\\jump\(TX_(?<id>[0-9a-f]{4})\)')
    if ($jump.Success) {
        $jumpedId = [Convert]::ToInt32($jump.Groups['id'].Value, 16)
        $message = $message.Substring(0, $jump.Index) +
            (Resolve-TokayText $jumpedId $visited)
    } elseif ($allTextFallthroughIds.ContainsKey($textId)) {
        $successorId = [int]$allTextFallthroughIds[$textId]
        # An unterminated trailing \n is a complete text command in the ROM.
        # Resolve it before adjoining the successor so the runtime command
        # scanner cannot greedily consume the successor's first word.
        if ($message.EndsWith('\n', [StringComparison]::Ordinal)) {
            $message = $message.Substring(0, $message.Length - 2) + "`n"
        }
        $message += Resolve-TokayText $successorId $visited
    }
    [void]$visited.Remove($textId)
    return $message
}

$tokayTextIds = @(
    0x0a00..0x0a3b
    0x0a40..0x0a53
    0x0a60..0x0a6c
    0x1c10..0x1c12
) | Sort-Object -Unique
if (-not $allTextFallthroughIds.ContainsKey(0x0a11) -or
    $allTextFallthroughIds[0x0a11] -ne 0x0a12 -or
    -not $allTexts[0x0a11].EndsWith('\n', [StringComparison]::Ordinal)) {
    throw 'Wild Tokay replay prompt TX_0a11 no longer falls through into TX_0a12.'
}
foreach ($textId in $tokayTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Tokay Island TX_$($textId.ToString('x4'))."
    }
    $resolved = Resolve-TokayText $textId (
        [Collections.Generic.HashSet[int]]::new())
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($resolved))
    $tokayTextRows.Add("$($textId.ToString('x4'))`t$encoded")
}

$tokayAnimationRows = [Collections.Generic.List[string]]::new()
$tokayAnimationRows.Add("# animation`tencoded")
foreach ($animation in 0x00..0x09) {
    $encoded = Resolve-NpcAnimation 0x48 $animation
    if ([string]::IsNullOrWhiteSpace($encoded)) {
        throw "Could not resolve INTERAC_TOKAY animation `$$($animation.ToString('x2'))."
    }
    $tokayAnimationRows.Add("$($animation.ToString('x2'))`t$encoded")
}

$wildTokayPrizeRows = [Collections.Generic.List[string]]::new()
$wildTokayPrizeRows.Add(
    "# prize-code`taccessory-subid`tsprite`ttile-base`tpalette`tanimation")
$wildTokayPrizeAccessorySubids = @(0x3e, 0x2b, 0x2c, 0x0d, 0x2d, 0x0e)
for ($prizeCode = 0; $prizeCode -lt $wildTokayPrizeAccessorySubids.Count; $prizeCode++) {
    $subid = $wildTokayPrizeAccessorySubids[$prizeCode]
    $graphic = $interactionGraphics["99`:$subid"]
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw "Could not resolve Wild Tokay prize accessory `$$($subid.ToString('x2'))."
    }
    $animation = Resolve-NpcAnimation 0x63 $graphic.DefaultAnimation
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve Wild Tokay prize accessory animation `$$($graphic.DefaultAnimation.ToString('x2'))."
    }
    $sprite = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($sprite)
    $wildTokayPrizeRows.Add((@(
        $prizeCode, $subid.ToString('x2'), $sprite,
        $graphic.TileBase.ToString('x2'), $graphic.Palette.ToString('x2'),
        $animation
    ) -join "`t"))
}

$wildTokayStartTileRows = [Collections.Generic.List[string]]::new()
$wildTokayStartTileRows.Add('# order`ttile`tpacked-position'.Replace('`t', "`t"))
$wildTokayStartTileMatch = [regex]::Match(
    $wildTokaySource,
    '(?ms)^@tilesToReplaceOnStart:\s*(?<rows>(?:\.db \$[0-9a-f]{2} \$[0-9a-f]{2}\s*){6})')
if (-not $wildTokayStartTileMatch.Success) {
    throw 'Could not parse the six Wild Tokay start-tile writes.'
}
$wildTokayStartTileMatches = [regex]::Matches(
    $wildTokayStartTileMatch.Groups['rows'].Value,
    '\.db \$(?<tile>[0-9a-f]{2}) \$(?<position>[0-9a-f]{2})')
if ($wildTokayStartTileMatches.Count -ne 6) {
    throw "Expected six Wild Tokay start-tile writes, got $($wildTokayStartTileMatches.Count)."
}
for ($order = 0; $order -lt $wildTokayStartTileMatches.Count; $order++) {
    $row = $wildTokayStartTileMatches[$order]
    $wildTokayStartTileRows.Add((@(
        $order,
        $row.Groups['tile'].Value,
        $row.Groups['position'].Value
    ) -join "`t"))
}

$wildTokayMeatAccessoryRows = [Collections.Generic.List[string]]::new()
$wildTokayMeatAccessoryRows.Add(
    '# parameter`ty-offset`tx-offset`tvisible`tanimation`tsprite`ttile-base`tpalette`tencoded'.Replace(
        '`t', "`t"))
$wildTokayMeatAccessoryMatch = [regex]::Match(
    $wildTokayAccessorySource,
    '(?ms)^@data:\s*(?<rows>(?:\.db \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2}\s*){9})')
if (-not $wildTokayMeatAccessoryMatch.Success) {
    throw 'Could not parse INTERAC_ACCESSORY parameter offsets.'
}
$wildTokayMeatAccessoryMatches = [regex]::Matches(
    $wildTokayMeatAccessoryMatch.Groups['rows'].Value,
    '\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<visible>[0-9a-f]{2}) \$(?<animation>[0-9a-f]{2})')
$wildTokayMeatGraphic = $interactionGraphics['99:115']
if ($wildTokayMeatAccessoryMatches.Count -ne 9 -or
    $null -eq $wildTokayMeatGraphic -or
    -not $gfxNames.ContainsKey($wildTokayMeatGraphic.Gfx)) {
    throw 'Could not resolve INTERAC_ACCESSORY `$63:$73 held-meat data.'
}
$wildTokayMeatSprite = $gfxNames[$wildTokayMeatGraphic.Gfx]
[void]$npcSpriteNames.Add($wildTokayMeatSprite)
for ($parameter = 0; $parameter -lt 5; $parameter++) {
    $row = $wildTokayMeatAccessoryMatches[$parameter]
    $yByte = [Convert]::ToInt32($row.Groups['y'].Value, 16)
    $xByte = [Convert]::ToInt32($row.Groups['x'].Value, 16)
    $yOffset = if ($yByte -ge 0x80) { $yByte - 0x100 } else { $yByte }
    $xOffset = if ($xByte -ge 0x80) { $xByte - 0x100 } else { $xByte }
    $animationIndex = [Convert]::ToInt32(
        $row.Groups['animation'].Value, 16)
    $animation = Resolve-NpcAnimation 0x63 $animationIndex
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve held-meat animation `$$($animationIndex.ToString('x2'))."
    }
    $wildTokayMeatAccessoryRows.Add((@(
        $parameter, $yOffset, $xOffset,
        $row.Groups['visible'].Value,
        $row.Groups['animation'].Value,
        $wildTokayMeatSprite,
        $wildTokayMeatGraphic.TileBase.ToString('x2'),
        $wildTokayMeatGraphic.Palette.ToString('x2'),
        $animation
    ) -join "`t"))
}

# The scent-seedling plot and southern Crescent Island entrance use the
# room-flag-gated INTERAC_DECORATION `$80 family. Keep their visuals and native
# sequences separate from the visible Tokay NPC table.
$pirateSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\pirate.s')
$pirateScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$pirateHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$tokayEntranceObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$tokaySeedlingPlotMatch = [regex]::Match(
    $tokayScriptSource,
    '(?ms)^tokayAtSeedlingPlotScript:.*?^@plantSeedling:\s+disableinput\s+showtextlowindex <TX_0a40\s+wait (?<introWait>\d+)\s+showtextlowindex <TX_0a41\s+asm15 scriptHelp\.tokayFlipDirection\s+setspeed SPEED_100\s+applyspeed \$(?<moveCounter>[0-9a-f]{2})\s+asm15 scriptHelp\.tokayFlipDirection\s+asm15 scriptHelp\.tokayPlantScentSeedling\s+spawninteraction INTERAC_DECORATION, \$(?<subid>[0-9a-f]{2}), \$(?<y>[0-9a-f]{2}), \$(?<x>[0-9a-f]{2})\s+playsound SND_GETSEED\s+wait (?<doneWait>\d+)\s+asm15 scriptHelp\.tokayTurnToFaceLink\s+showtextlowindex <TX_0a42')
$tokaySeedlingInitMatch = [regex]::Match(
    $tokaySource,
    '(?ms)^@initSubid11:\s+call getThisRoomFlags\s+bit 7,a\s+jr z,@initSubid0e.*?ld e,Interaction\.xh\s+ld a,\(de\)\s+add \$(?<xOffset>[0-9a-f]{2})\s+ld \(de\),a\s+call objectMarkSolidPosition\s+jr @initSubid0e')
$objectSpeedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$tokaySeedlingSpeedMatch = [regex]::Match(
    $objectSpeedSource,
    '(?m)^\s*SPEED_100\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})\s*$')
if (-not $tokaySeedlingPlotMatch.Success -or
    -not $tokaySeedlingInitMatch.Success -or
    -not $tokaySeedlingSpeedMatch.Success -or
    $mainObjectSource -notmatch
        '(?ms)^group1MapacObjectData:\s+obj_Interaction \$48 \$11 \$38 \$48\s+obj_Interaction \$80 \$04 \$38 \$48' -or
    $tokayHelperSource -notmatch
        '(?ms)^tokayTurnToFaceLink:.*?objectGetAngleTowardLink.*?add \$04.*?and \$18.*?^tokayFlipDirection:.*?xor \$10.*?^tokayPlantScentSeedling:.*?getThisRoomFlags.*?set 7,\(hl\).*?dec h.*?set 7,\(hl\).*?TREASURE_SCENT_SEEDLING.*?loseTreasure' -or
    $decorationSource -notmatch
        '(?ms)^interactionCode80:.*?^@deleteIfRoomFlagBit7Unset:.*?bit 7,a') {
    throw 'Tokay seedling plot source contract changed.'
}
if ($pirateSource -notmatch
        '(?ms)^@subid4Init:.*?ROOMFLAG_80.*?^@state3:.*?^@resetPushCounter:.*?ld a,10' -or
    $pirateScriptSource -notmatch
        '(?ms)^pirateSubid4Script:.*?TREASURE_TOKAY_EYEBALL.*?^pirateSubid4Script_insertEyeball:.*?ROOMFLAG_80.*?INTERAC_DECORATION, \$06, \$52, \$6a.*?wait 60.*?SND_OPENING.*?shakescreen 160.*?wait 120.*?pirate_openEyeballCave.*?wait 60.*?loseTreasure, TREASURE_TOKAY_EYEBALL' -or
    $pirateHelperSource -notmatch
        '(?ms)^pirate_openEyeballCave:.*?ld c,\$54.*?ld a,\$a2.*?ld a,\$ef.*?ld a,\$a4.*?SND_DOORCLOSE.*?INTERAC_PUFF' -or
    $tokayEntranceObjectSource -notmatch
        '(?ms)^group1MapbaObjectData:\s+obj_Interaction \$80 \$05 \$52 \$46\s+obj_Interaction \$80 \$06 \$52 \$6a\s+obj_Interaction \$c4 \$04 \$5a \$68') {
    throw 'Southern Tokay entrance eye/socket source contract changed.'
}
$tokaySeedlingSubid = [Convert]::ToInt32(
    $tokaySeedlingPlotMatch.Groups['subid'].Value, 16)
$tokaySeedlingGraphic = $interactionGraphics["128`:$tokaySeedlingSubid"]
if ($null -eq $tokaySeedlingGraphic -or
    -not $gfxNames.ContainsKey($tokaySeedlingGraphic.Gfx)) {
    throw 'Could not resolve INTERAC_DECORATION `$80:$04 scent-seedling visual.'
}
$tokaySeedlingAnimation = Resolve-NpcAnimation 0x80 (
    $tokaySeedlingGraphic.DefaultAnimation)
if ([string]::IsNullOrWhiteSpace($tokaySeedlingAnimation)) {
    throw 'Could not resolve INTERAC_DECORATION `$80:$04 scent-seedling animation.'
}
$tokaySeedlingSprite = $gfxNames[$tokaySeedlingGraphic.Gfx]
[void]$npcSpriteNames.Add($tokaySeedlingSprite)
$tokaySeedlingPlotRows = @(
    '# group`troom`tnpc-id`tnpc-subid`tdecoration-id`tdecoration-subid`ty`tx`troom-flag`tspeed`tmove-counter`tplanted-x-offset`tintro-wait`tdone-wait`tsprite`ttile-base`tpalette`tanimation`tsource',
    (@(
        1, 'ac', '48', '11', '80',
        $tokaySeedlingPlotMatch.Groups['subid'].Value,
        $tokaySeedlingPlotMatch.Groups['y'].Value,
        $tokaySeedlingPlotMatch.Groups['x'].Value,
        '80', $tokaySeedlingSpeedMatch.Groups['value'].Value,
        $tokaySeedlingPlotMatch.Groups['moveCounter'].Value,
        $tokaySeedlingInitMatch.Groups['xOffset'].Value,
        $tokaySeedlingPlotMatch.Groups['introWait'].Value,
        $tokaySeedlingPlotMatch.Groups['doneWait'].Value,
        $tokaySeedlingSprite,
        $tokaySeedlingGraphic.TileBase.ToString('x2'),
        $tokaySeedlingGraphic.Palette.ToString('x2'),
        $tokaySeedlingAnimation,
        'objects/ages/mainData.s:group1MapacObjectData;scripts/ages/scripts.s:tokayAtSeedlingPlotScript;object_code/ages/interactions/tokay.s:@initSubid11;object_code/ages/interactions/decoration.s:interactionCode80'
    ) -join "`t")
) | ForEach-Object { $_.Replace('`t', "`t") }
$tokayEntranceEyeRows = [Collections.Generic.List[string]]::new()
$tokayEntranceEyeRows.Add(
    '# order`tgroup`troom`tid`tsubid`ty`tx`troom-flag-required`tsprite`ttile-base`tpalette`tanimation`tsource'.Replace(
        '`t', "`t"))
$tokayEntranceEyePlacements = @(
    @(0, 0x05, 0x52, 0x46, 0x00),
    @(1, 0x06, 0x52, 0x6a, 0x80)
)
foreach ($placement in $tokayEntranceEyePlacements) {
    $subid = [int]$placement[1]
    $graphic = $interactionGraphics["128`:$subid"]
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw "Could not resolve INTERAC_DECORATION `$80:`$$($subid.ToString('x2')) visual."
    }
    $animation = Resolve-NpcAnimation 0x80 $graphic.DefaultAnimation
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve INTERAC_DECORATION `$80:`$$($subid.ToString('x2')) animation."
    }
    $sprite = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($sprite)
    $tokayEntranceEyeRows.Add((@(
        $placement[0], 1, 'ba', '80', $subid.ToString('x2'),
        ([int]$placement[2]).ToString('x2'),
        ([int]$placement[3]).ToString('x2'),
        ([int]$placement[4]).ToString('x2'),
        $sprite, $graphic.TileBase.ToString('x2'),
        $graphic.Palette.ToString('x2'), $animation,
        "objects/ages/mainData.s:group1MapbaObjectData[INTERAC_DECORATION `$80:`$$($subid.ToString('x2'))]"
    ) -join "`t"))
}
$tokayEyeballSlotRows = [Collections.Generic.List[string]]::new()
$tokayEyeballSlotRows.Add(
    '# group`troom`tid`tsubid`troom-flag`ttreasure`tpush-delay`teye-y`teye-x`teye-wait`tshake-frames`tshake-wait`topen-wait`topen-position`topen-tiles`tpuff-y`tpuff-x`tsource'.Replace(
        '`t', "`t"))
$tokayEyeballSlotRows.Add(
    '1`tba`tc4`t04`t80`t4f`t10`t52`t6a`t60`t160`t120`t60`t54`ta2,ef,a4`t58`t58`tobject_code/ages/interactions/pirate.s:@subid4Init/@state3;scripts/ages/scripts.s:pirateSubid4Script_insertEyeball;scripts/ages/scriptHelper.s:pirate_openEyeballCave'.Replace(
        '`t', "`t"))

$tokayShopRows = [Collections.Generic.List[string]]::new()
$tokayShopRows.Add(
    "# order`tplaced-subid`ty`tx`tsprite`ttile-base`tpalette`tanimation")
$tokayShopPlacements = @(
    @(0, 0x00, 0x40, 0x40),
    @(1, 0x01, 0x40, 0x60),
    @(2, 0x04, 0x40, 0x50),
    @(-1, 0x02, 0x00, 0x00),
    @(-1, 0x03, 0x00, 0x00),
    @(-1, 0x05, 0x00, 0x00),
    @(-1, 0x06, 0x00, 0x00)
)
foreach ($placement in $tokayShopPlacements) {
    $subid = [int]$placement[1]
    $graphic = $interactionGraphics["129`:$subid"]
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw "Could not resolve INTERAC_TOKAY_SHOP_ITEM `$$($subid.ToString('x2')) visual."
    }
    $animation = Resolve-NpcAnimation 0x81 $graphic.DefaultAnimation
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve INTERAC_TOKAY_SHOP_ITEM `$$($subid.ToString('x2')) animation."
    }
    $spriteName = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($spriteName)
    $tokayShopRows.Add((@(
        $placement[0], $subid.ToString('x2'),
        ([int]$placement[2]).ToString('x2'),
        ([int]$placement[3]).ToString('x2'),
        $spriteName, $graphic.TileBase.ToString('x2'),
        $graphic.Palette.ToString('x2'), $animation
    ) -join "`t"))
}

$tokayMeatGraphic = $interactionGraphics['140:0']
if ($null -eq $tokayMeatGraphic -or
    -not $gfxNames.ContainsKey($tokayMeatGraphic.Gfx)) {
    throw 'Could not resolve INTERAC_TOKAY_MEAT `$8c:$00 visual.'
}
$tokayMeatAnimation = Resolve-NpcAnimation 0x8c $tokayMeatGraphic.DefaultAnimation
if ([string]::IsNullOrWhiteSpace($tokayMeatAnimation)) {
    throw 'Could not resolve INTERAC_TOKAY_MEAT `$8c:$00 animation.'
}
$tokayMeatSprite = $gfxNames[$tokayMeatGraphic.Gfx]
[void]$npcSpriteNames.Add($tokayMeatSprite)

$wildTokayParticipantGraphic = $interactionGraphics['72:0']
if ($null -eq $wildTokayParticipantGraphic -or
    $wildTokayParticipantGraphic.DefaultAnimation -ne 0x02) {
    throw 'INTERAC_TOKAY `$48 no longer initializes with downward animation `$02.'
}

$tokayInteractionConstantRows = @(
    '# key`tvalue',
    'tokay-id`t72',
    'room-flag-item`t32',
    'dimitri-state-address`t50759',
    "treasure-sword`t$($treasureIds['TREASURE_SWORD'])",
    "treasure-harp`t$($treasureIds['TREASURE_HARP'])",
    "treasure-shovel`t$($treasureIds['TREASURE_SHOVEL'])",
    "treasure-seed-satchel`t$($treasureIds['TREASURE_SEED_SATCHEL'])",
    "treasure-flippers`t$($treasureIds['TREASURE_FLIPPERS'])",
    "treasure-ember-seeds`t$($treasureIds['TREASURE_EMBER_SEEDS'])",
    "treasure-scent-seeds`t$($treasureIds['TREASURE_SCENT_SEEDS'])",
    "treasure-mystery-seeds`t$($treasureIds['TREASURE_MYSTERY_SEEDS'])",
    "treasure-trade-item`t$($treasureIds['TREASURE_TRADEITEM'])",
    "treasure-scent-seedling`t$($treasureIds['TREASURE_SCENT_SEEDLING'])",
    "sound-get-item`t$($soundIds['SND_GETITEM'])",
    "sound-get-seed`t$($soundIds['SND_GETSEED'])",
    "sound-jump`t$($soundIds['SND_JUMP'])"
) | ForEach-Object { $_.Replace('`t', "`t") }

$tokayShopConstantRows = @(
    '# key`tvalue',
    'group`t2', 'room`t228',
    'shop-item-id`t129',
    "item-collision-radius`t$tokayShopCollisionRadius",
    "treasure-shield`t$($treasureIds['TREASURE_SHIELD'])",
    "treasure-feather`t$($treasureIds['TREASURE_FEATHER'])",
    "global-bought-feather`t$($globalFlagValues['GLOBALFLAG_BOUGHT_FEATHER_FROM_TOKAY'])",
    "global-bought-bracelet`t$($globalFlagValues['GLOBALFLAG_BOUGHT_BRACELET_FROM_TOKAY'])"
) | ForEach-Object { $_.Replace('`t', "`t") }

$wildTokayConstantRows = @(
    '# key`tvalue',
    'group-past-manager`t2', 'room-past-manager`t222',
    'group-present-manager`t2', 'room-present-manager`t229',
    'wild-controller-id`t112', 'wild-participant-subid`t12',
    'room-flag-event`t64',
    'room-flag-secondary`t128',
    'wild-level-address`t50922',
    "treasure-bombs`t$($treasureIds['TREASURE_BOMBS'])",
    "treasure-bracelet`t$($treasureIds['TREASURE_BRACELET'])",
    "global-finished-game`t$($globalFlagValues['GLOBALFLAG_FINISHEDGAME'])",
    "global-began-secret`t$($globalFlagValues['GLOBALFLAG_BEGAN_TOKAY_SECRET'])",
    "global-done-secret`t$($globalFlagValues['GLOBALFLAG_DONE_TOKAY_SECRET'])",
    "sound-open-chest`t$($soundIds['SND_OPENCHEST'])",
    "sound-whistle`t$($soundIds['SND_WHISTLE'])",
    "sound-success`t$($soundIds['SND_FILLED_HEART_CONTAINER'])",
    "sound-error`t$($soundIds['SND_ERROR'])",
    'participant-left-x`t24', 'participant-right-x`t136',
    'participant-start-y`t248',
    "participant-animation`t$($wildTokayParticipantGraphic.DefaultAnimation)",
    "game-link-y`t$wildTokayManagerLinkY",
    "game-link-x`t$wildTokayManagerLinkX",
    "game-return-position`t$wildTokayReturnPosition",
    "wild-cycle-count-level-0`t$($wildTokayCycleCounts[0])",
    "wild-cycle-count-level-1`t$($wildTokayCycleCounts[1])",
    "wild-cycle-count-level-2`t$($wildTokayCycleCounts[2])",
    "wild-cycle-count-level-3`t$($wildTokayCycleCounts[3])",
    "wild-cycle-count-level-4`t$($wildTokayCycleCounts[4])",
    'game-spawn-delay`t60', 'game-start-delay`t30',
    'game-fade-in-delay`t10'
) | ForEach-Object { $_.Replace('`t', "`t") }

$wildTokayMeatConstantRows = @(
    '# key`tvalue`ttext',
    "sound-fall`t$($soundIds['SND_FALLINHOLE'])`t-",
    "sound-land`t$($soundIds['SND_BOMB_LAND'])`t-",
    "meat-sprite`t0`t$tokayMeatSprite",
    "meat-tile-base`t$($tokayMeatGraphic.TileBase)`t-",
    "meat-palette`t$($tokayMeatGraphic.Palette)`t-",
    "meat-animation`t0`t$tokayMeatAnimation",
    'meat-start-y`t56`t-', 'meat-start-x`t80`t-',
    'meat-start-z`t-64`t-', 'meat-fall-delay`t30`t-',
    'meat-fall-gravity`t40`t-',
    'meat-collision-radius`t8`t-', 'meat-drop-life`t20`t-'
) | ForEach-Object { $_.Replace('`t', "`t") }

$tokayHolderRows = [Collections.Generic.List[string]]::new()
$tokayHolderRows.Add(
    '# subid`ttreasure`titem-graphic`tgrant-object`tgrant-subid`tgrant-parameter`titem-sprite`titem-tile-base`titem-palette`titem-animation'.Replace('`t', "`t"))
$tokayHolderSpecs = @(
    @{ Subid = 0x06; Treasure = 'TREASURE_SWORD'; ItemGraphic = 0x10; GrantObject = 'TREASURE_OBJECT_SWORD_06' },
    @{ Subid = 0x07; Treasure = 'TREASURE_SHOVEL'; ItemGraphic = 0x1b; GrantObject = 'TREASURE_OBJECT_SHOVEL_01' },
    @{ Subid = 0x08; Treasure = 'TREASURE_HARP'; ItemGraphic = 0x68; GrantObject = 'TREASURE_OBJECT_HARP_01' },
    @{ Subid = 0x09; Treasure = 'TREASURE_FLIPPERS'; ItemGraphic = 0x31; GrantObject = 'TREASURE_OBJECT_FLIPPERS_01' },
    @{ Subid = 0x0a; Treasure = 'TREASURE_SEED_SATCHEL'; ItemGraphic = 0x20; GrantObject = 'TREASURE_OBJECT_SEED_SATCHEL_01' }
)
foreach ($spec in $tokayHolderSpecs) {
    $treasure = [int]$treasureIds[$spec.Treasure]
    $grant = $treasureObjectRecords[$spec.GrantObject]
    $expectedGrantSubid = if ([int]$spec.Subid -eq 0x06) { 0x06 } else { 0x01 }
    if ($null -eq $grant -or [int]$grant.Treasure -ne $treasure -or
        [int]$grant.Subid -ne $expectedGrantSubid) {
        throw "Could not resolve $($spec.GrantObject) for Tokay holder `$$(([int]$spec.Subid).ToString('x2'))."
    }

    $graphic = $interactionGraphics["99:$([int]$spec.ItemGraphic)"]
    if ($null -eq $graphic -or
        ($graphic.Gfx -ne 0 -and -not $gfxNames.ContainsKey($graphic.Gfx))) {
        throw "Could not resolve Tokay holder accessory `$$(([int]$spec.ItemGraphic).ToString('x2'))."
    }
    $animation = Resolve-NpcAnimation 0x63 $graphic.DefaultAnimation
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve Tokay holder accessory animation `$$($graphic.DefaultAnimation.ToString('x2'))."
    }
    $sprite = if ($graphic.Gfx -eq 0) {
        'spr_common_sprites'
    } else {
        $gfxNames[$graphic.Gfx]
    }
    [void]$npcSpriteNames.Add($sprite)
    $tokayHolderRows.Add((@(
        ([int]$spec.Subid).ToString('x2'), $treasure.ToString('x2'),
        ([int]$spec.ItemGraphic).ToString('x2'), [string]$spec.GrantObject,
        ([int]$grant.Subid).ToString('x2'),
        ([int]$grant.Parameter).ToString('x2'), $sprite,
        ([int]$graphic.TileBase).ToString('x2'),
        ([int]$graphic.Palette).ToString('x2'), $animation
    ) -join "`t"))
}

$wildTokayPatternRows = [Collections.Generic.List[string]]::new()
$wildTokayPatternRows.Add("# level`trandom-index`tpattern`tleft-count`tright-count")
for ($level = 0; $level -lt $wildTokayCycleCounts.Count; $level++) {
    for ($randomIndex = 0; $randomIndex -lt 16; $randomIndex++) {
        $pattern = $wildTokayRandomPatternValues[$level * 16 + $randomIndex]
        $offset = $pattern * 4
        $values = $wildTokayPatternValues[$offset..($offset + 3)]
        $wildTokayPatternRows.Add(
            "$level`t$randomIndex`t$pattern`t$($values[0]),$($values[1])`t$($values[2]),$($values[3])")
    }
}
if ($wildTokayPatternRows.Count -ne 81) {
    throw "Expected 80 Wild Tokay random pattern rows, got $($wildTokayPatternRows.Count - 1)."
}

# Room 2:ee is Vasu Jewelers. Preserve the complete placed object order, the
# non-Game-Link dialogue graph, and the animations used by Vasu, both snakes,
# and both help books. Text \call/\jump commands are assembler-time control
# flow, so flatten them here while the complete TX table is available; inline
# DialogueBox commands such as \stop, \col, and \opt remain intact.
$vasuShopScriptsSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\common\commonScripts.s')
$vasuShopScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\common\scriptHelper.s')
$globalFlagsSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
$ringsSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\rings.s')
$wramSource = Read-ImportText (Join-Path $Disassembly 'include\wram.s')
$vasuTreasureObjectSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\treasureObjectData.s')
$ringMenuSource = Read-ImportText (Join-Path $Disassembly 'code\bank2.s')
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
$shopItemSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\shopItem.s')
$shopkeeperSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\shopkeeper.s')
$companionScriptsSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\companionScripts.s')
$roomGfxChangesSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\roomGfxChanges.s')
$treeGfxHeadersSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\treeGfxHeaders.s')
$linkSpecialObjectSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$linkAnimationSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
$linkAnimationLogicSource = Read-ImportText (
    Join-Path $Disassembly 'code\specialObjectAnimationsAndDamage.s')
$parentItemUsageSource = Read-ImportText (
    Join-Path $Disassembly 'code\parentItemUsage.s')
$grabbedObjectSource = Read-ImportText (
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
$room148WorkerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\pickaxeWorker.s')
$room148FallingRockSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\fallingRock.s')
$agesMainScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$room148VillagerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\villager.s')
$room148PastGirlSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\pastGirl.s')
$gameProgress2Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscMan2.s')
$objectSpeedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$musicConstantSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$agesGfxHeaderSource = Read-ImportText (
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

# Dungeon entry handlers, statue-eye spawners, and miniboss portals are shared
# native interactions. Preserve every direct placement and the source tables
# which select their dungeon text, initial spinner state, portal destination,
# graphics, offsets, collision, timing, and sound. This keeps room 4:24 from
# becoming a one-room reconstruction and lets the runtime merge these records
# with the existing ordered dungeon-mechanic stream.
$statueEyeballSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\statueEyeball.s')
$minibossPortalSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\minibossPortal.s')

if ($dungeonStuffSource -notmatch '(?ms)^@subid00:.*?SCROLLMODE_02.*?cp \$78.*?ld a,\$08\s+call objectSetCollideRadius.*?call initializeDungeonStuff.*?call setDeathRespawnPoint' -or
    $statueEyeballSource -notmatch '(?ms)^@subid2:.*?@centerOnTileAndGetDirectionToFace.*?@lowPositionValues:.*?\.db \$05 \$08.*?\.db \$07 \$07.*?\.db \$06 \$07.*?\.db \$05 \$07' -or
    $statueEyeballSource -notmatch '(?ms)^@subid1:.*?ld e,\$02.*?wRoomLayout \+ LARGE_ROOM_WIDTH-1 \+ \(LARGE_ROOM_HEIGHT-1\)\*16.*?TILEINDEX_EYE_STATUE.*?convertShortToLongPosition_paramC.*?dec b\s+dec b' -or
    $minibossPortalSource -notmatch '(?ms)^@minibossState0:.*?@dungeonRoomTable.*?and \$80.*?ld c,\$57.*?ld a,\$03\s+call objectSetCollideRadius' -or
    $minibossPortalSource -notmatch '(?ms)^@state1:.*?ld a,\$30.*?setLinkForceStateToState08.*?SND_TELEPORT.*?^@minibossState3:.*?wWarpDestGroup.*?wActiveGroup.*?or \$80.*?TRANSITION_DEST_BASIC.*?ld \(hl\),\$57.*?ld \(hl\),\$03' -or
    $tileIndexSource -notmatch '(?m)^\.define TILEINDEX_EYE_STATUE\s+\$ee' -or
    $musicIdSource -notmatch '(?m)^\s*SND_TELEPORT\s+db\s+; \$8d') {
    throw 'Dungeon entry, statue-eyeball, or miniboss-portal behavior changed in the disassembly.'
}

$dungeonTextBlock = [regex]::Match(
    $dungeonStuffSource,
    '(?ms)^@dungeonTextIndices:\s*\.ifdef ROM_AGES(?<body>.*?)\.else; ROM_SEASONS')
$spinnerBlock = [regex]::Match(
    $dungeonStuffSource,
    '(?ms)^@initialSpinnerValues:\s*(?<body>.*?)\.endif')
$dungeonTextMatches = @([regex]::Matches(
    $dungeonTextBlock.Groups['body'].Value, 'TX_(?<id>[0-9a-f]{4})'))
$spinnerMatches = @([regex]::Matches(
    $spinnerBlock.Groups['body'].Value, '\$(?<value>[0-9a-f]{2})'))
if (-not $dungeonTextBlock.Success -or -not $spinnerBlock.Success -or
    $dungeonTextMatches.Count -ne 16 -or $spinnerMatches.Count -ne 16) {
    throw 'Expected 16 Ages dungeon-entry text and spinner-state records.'
}
$dungeonEntryRows = [Collections.Generic.List[string]]::new()
$dungeonEntryRows.Add("# dungeon`ttext-id`tutf8-base64`tspinner-state")
for ($dungeon = 0; $dungeon -lt 16; $dungeon++) {
    $textId = [Convert]::ToInt32(
        $dungeonTextMatches[$dungeon].Groups['id'].Value, 16)
    # text.yaml aliases TX_020f to the TX_020e payload.
    $sourceTextId = if ($textId -eq 0x020f) { 0x020e } else { $textId }
    if (-not $allTexts.ContainsKey($sourceTextId)) {
        throw "Could not resolve dungeon-entry text TX_$($textId.ToString('x4'))."
    }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$sourceTextId]))
    $spinner = [Convert]::ToInt32(
        $spinnerMatches[$dungeon].Groups['value'].Value, 16)
    $dungeonEntryRows.Add(
        "$dungeon`t$($textId.ToString('x4'))`t$encoded`t$($spinner.ToString('x2'))")
}

$portalTableBlock = [regex]::Match(
    $minibossPortalSource,
    '(?ms)^@dungeonRoomTable:\s*\.ifdef ROM_AGES(?<body>.*?)\.else')
$portalPairMatches = @([regex]::Matches(
    $portalTableBlock.Groups['body'].Value,
    '(?m)^\s*\.db\s+\$(?<miniboss>[0-9a-f]{2})\s+\$(?<entrance>[0-9a-f]{2})'))
if (-not $portalTableBlock.Success -or $portalPairMatches.Count -ne 9) {
    throw 'Expected nine Ages miniboss portal room pairs.'
}
$minibossPortalPairRows = [Collections.Generic.List[string]]::new()
$minibossPortalPairRows.Add("# dungeon`tminiboss-room`tentrance-room")
for ($dungeon = 0; $dungeon -lt $portalPairMatches.Count; $dungeon++) {
    $pair = $portalPairMatches[$dungeon]
    $minibossPortalPairRows.Add(
        "$dungeon`t$($pair.Groups['miniboss'].Value)`t$($pair.Groups['entrance'].Value)")
}

$dungeonSharedPlacementRows = [Collections.Generic.List[string]]::new()
$dungeonSharedPlacementRows.Add(
    "# group`troom`torder`tkind`tid`tsubid`ty`tx`tdungeon`tsource")
$sharedGroup = -1
$sharedRoom = -1
$sharedOrder = 0
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $sharedGroup = [Convert]::ToInt32($Matches['group'], 10)
        $sharedRoom = [Convert]::ToInt32($Matches['room'], 16)
        $sharedOrder = 0
        continue
    }
    if ($sharedGroup -lt 0 -or $line -notmatch '^\s*obj_') { continue }
    if ($line -match '^\s*obj_End') {
        $sharedGroup = -1
        continue
    }
    if ($line -match '^\s*obj_Interaction\s+\$(?<id>12|e2|7e)\s+\$(?<subid>00|01)(?:\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2}))?') {
        $id = [Convert]::ToInt32($Matches['id'], 16)
        $subid = [Convert]::ToInt32($Matches['subid'], 16)
        $kind = if ($id -eq 0x12 -and $subid -eq 0x00) {
            'entry'
        } elseif ($id -eq 0xe2 -and $subid -eq 0x01) {
            'eye-spawner'
        } elseif ($id -eq 0x7e -and $subid -eq 0x00) {
            'miniboss-portal'
        } else {
            ''
        }
        if ($kind -ne '') {
            $y = if ($Matches.ContainsKey('y') -and $Matches['y'] -ne '') {
                $Matches['y']
            } else { '--' }
            $x = if ($Matches.ContainsKey('x') -and $Matches['x'] -ne '') {
                $Matches['x']
            } else { '--' }
            $dungeon = Resolve-DungeonMechanicDungeonIndex $sharedGroup $sharedRoom
            if ($dungeon -eq 0xff) {
                throw "Shared dungeon interaction in non-dungeon room $sharedGroup`:$($sharedRoom.ToString('x2'))."
            }
            $dungeonSharedPlacementRows.Add(
                "$sharedGroup`t$($sharedRoom.ToString('x2'))`t$sharedOrder`t$kind`t$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$y`t$x`t$dungeon`tmainData.s:group${sharedGroup}Map$($sharedRoom.ToString('x2'))ObjectData")
        }
    }
    $sharedOrder++
}
if ($dungeonSharedPlacementRows.Count -ne 43 -or
    -not ($dungeonSharedPlacementRows -contains
        "4`t24`t0`tentry`t12`t00`t88`t78`t1`tmainData.s:group4Map24ObjectData") -or
    -not ($dungeonSharedPlacementRows -contains
        "4`t24`t1`teye-spawner`te2`t01`t--`t--`t1`tmainData.s:group4Map24ObjectData") -or
    -not ($dungeonSharedPlacementRows -contains
        "4`t24`t2`tminiboss-portal`t7e`t00`t--`t--`t1`tmainData.s:group4Map24ObjectData")) {
    throw "Expected 42 shared dungeon interaction placements including ordered room 4:24, parsed $($dungeonSharedPlacementRows.Count - 1)."
}

$eyeGraphic = $interactionGraphics['226:0']
$portalGraphic = $interactionGraphics['126:0']
$eyeLowPositionBlock = [regex]::Match(
    $statueEyeballSource,
    '(?ms)^@lowPositionValues:\s*(?<body>.*?)(?=^;;)')
$eyeLowPositionMatches = @([regex]::Matches(
    $eyeLowPositionBlock.Groups['body'].Value,
    '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})'))
$eyeDefaultAnimation = if ($null -ne $eyeGraphic) {
    Resolve-NpcAnimation 0xe2 $eyeGraphic.DefaultAnimation
} else { '' }
$portalAnimation = Resolve-NpcAnimation 0x7e 0
if ($null -eq $eyeGraphic -or $eyeGraphic.Gfx -ne 0x8c -or
    $eyeGraphic.TileBase -ne 0x1e -or $eyeGraphic.Palette -ne 0 -or
    $eyeGraphic.DefaultAnimation -ne 4 -or -not $gfxNames.ContainsKey(0x8c) -or
    $null -eq $portalGraphic -or $portalGraphic.Gfx -ne 0 -or
    $portalGraphic.TileBase -ne 0x16 -or $portalGraphic.Palette -ne 2 -or
    $portalGraphic.DefaultAnimation -ne 0 -or
    -not $eyeLowPositionBlock.Success -or $eyeLowPositionMatches.Count -ne 8 -or
    [string]::IsNullOrWhiteSpace($eyeDefaultAnimation) -or
    [string]::IsNullOrWhiteSpace($portalAnimation)) {
    throw 'Could not resolve statue-eyeball or miniboss-portal graphics and offsets.'
}
$eyeSpriteName = $gfxNames[0x8c]
[void]$npcSpriteNames.Add($eyeSpriteName)
[void]$npcSpriteNames.Add('spr_common_sprites')
$dungeonSharedVisualRows = [Collections.Generic.List[string]]::new()
$dungeonSharedVisualRows.Add(
    "# kind`tindex`tsprite`ttile-base`tpalette`tanimation`tlow-y`tlow-x")
for ($direction = 0; $direction -lt 8; $direction++) {
    $offset = $eyeLowPositionMatches[$direction]
    $dungeonSharedVisualRows.Add(
        "eye`t$direction`t$eyeSpriteName`t$($eyeGraphic.TileBase)`t$($eyeGraphic.Palette)`t$eyeDefaultAnimation`t$([Convert]::ToInt32($offset.Groups['y'].Value, 16))`t$([Convert]::ToInt32($offset.Groups['x'].Value, 16))")
}
$dungeonSharedVisualRows.Add(
    "portal`t0`tspr_common_sprites`t$($portalGraphic.TileBase)`t$($portalGraphic.Palette)`t$portalAnimation`t-1`t-1")

$eraInfoSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\eraOrSeasonInfo.s')
$tilesetFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tilesetFlags.s')
$globalFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
$wramSource = Read-ImportText (Join-Path $Disassembly 'include\wram.s')
$eraPresentGraphic = $interactionGraphics['224:0']
$eraPastGraphic = $interactionGraphics['224:1']
$eraAnimation = Resolve-NpcAnimation 0xe0 0
if ($null -eq $eraPresentGraphic -or
    $eraPresentGraphic.Gfx -ne 0x70 -or
    $eraPresentGraphic.TileBase -ne 0x00 -or
    $eraPresentGraphic.Palette -ne 1 -or
    $eraPresentGraphic.DefaultAnimation -ne 0 -or
    $null -eq $eraPastGraphic -or
    $eraPastGraphic.Gfx -ne 0x70 -or
    $eraPastGraphic.TileBase -ne 0x08 -or
    $eraPastGraphic.Palette -ne 3 -or
    $eraPastGraphic.DefaultAnimation -ne 0 -or
    -not $gfxNames.ContainsKey(0x70) -or
    $eraAnimation -ne
        '127@8,248,0,0;8,0,2,0;8,8,4,0;8,16,6,0' -or
    $eraInfoSource -notmatch
        '(?ms)@state0:.*?interactionSetAlwaysUpdateBit.*?ld \(hl\),\$0a.*?ld \(hl\),\$b0.*?objectSetVisible80.*?@state1:.*?sub \$04.*?cp \$10.*?ld \(hl\),40.*?@state2:.*?ld \(hl\),\$06.*?@state3:.*?sub \$06.*?dec \(hl\)' -or
    $tilesetFlagSource -notmatch
        '(?m)^\.define TILESETFLAG_PAST \$80' -or
    $tilesetFlagSource -notmatch
        '(?m)^\.define TILESETFLAG_LARGE_INDOORS \$10' -or
    $tilesetFlagSource -notmatch
        '(?m)^\.define TILESETFLAG_OUTDOORS \$01' -or
    $globalFlagSource -notmatch
        '(?m)^\s*GLOBALFLAG_16\s+db\s*;\s*\$16:' -or
    $wramSource -notmatch
        '(?m)^wSentBackByStrangeForce:\s*;\s*\$cdde') {
    throw 'Could not resolve INTERAC_ERA_OR_SEASON_INFO $e0 visuals, timing, or display predicates.'
}
$eraSpriteName = $gfxNames[0x70]
[void]$npcSpriteNames.Add($eraSpriteName)
$eraInfoRows = [Collections.Generic.List[string]]::new()
$eraInfoRows.Add(
    "# subid`tsprite`ttile-base`tpalette`tanimation`tstart-y`tstart-x`tenter-step`ttarget-x`thold-updates`texit-step`texit-updates`toutdoors-mask`tlarge-indoors-mask`tpast-mask`tsuppress-global-flag`tsent-back-address`tsent-back-value`tsource")
foreach ($eraSpec in @(
    @{ SubId = 0; Graphic = $eraPresentGraphic },
    @{ SubId = 1; Graphic = $eraPastGraphic }
)) {
    $eraInfoRows.Add(
        "$($eraSpec.SubId.ToString('x2'))`t$eraSpriteName`t$($eraSpec.Graphic.TileBase.ToString('x2'))`t$($eraSpec.Graphic.Palette)`t$eraAnimation`t0a`tb0`t4`t10`t40`t6`t6`t01`t10`t80`t16`tcdde`t1`tinteractionData.s:interactione0SubidData/eraOrSeasonInfo.s")
}

$dungeonSharedConstantRows = @(
    "# key`tvalue"
    "entry-min-y`t120"
    "entry-radius`t8"
    "eye-statue-tile`t238"
    "eye-initial-y-offset`t-2"
    "portal-position`t87"
    "portal-radius`t3"
    "portal-spin-updates`t48"
    "portal-sound`t141"
    "portal-source-transition`t2"
    "portal-destination-transition`t0"
    "portal-destination-parameter`t0"
)

# The lower Black Tower construction rooms share four native handlers whose
# behavior is selected by placement var03 and the game-wide RNG. Pin the five
# complete object streams and export the script tables, extra animation, item
# visual, text, and timing values used by those handlers. Runtime still uses
# the ordinary NPC rows for positioned graphics and source ordering.
$blackTowerHardhatSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\hardhatWorker.s')
$blackTowerSoldierSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\soldier.s')
$agesScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$pickaxeAnimationSelectorMatch = [regex]::Match(
    $agesScriptHelperSource,
    '(?ms)^pickaxeWorker_setAnimationFromVar03:.*?^@animations:\s*\r?\n(?<table>(?:^[ \t]*\.db[^\r\n]*\r?\n)+)')
$pickaxeTextSelectorMatch = [regex]::Match(
    $agesScriptHelperSource,
    '(?ms)^pickaxeWorker_chooseRandomBlackTowerText:.*?^@blackTowerText:\s*\r?\n(?<table>(?:^[ \t]*\.db[^\r\n]*\r?\n)+)')
$hardhatTextSelectorMatch = [regex]::Match(
    $agesScriptHelperSource,
    '(?ms)^hardhatWorker_chooseTextForPatroller:.*?^@textIDs:\s*\r?\n(?<table>(?:^[ \t]*\.db[^\r\n]*\r?\n)+)')
$soldierTextSelectorMatch = [regex]::Match(
    $agesScriptHelperSource,
    '(?ms)^soldierGetRandomVar32Val:.*?^@data:\s*\r?\n(?<table>(?:^[ \t]*\.db[^\r\n]*\r?\n)+)')
$pickaxeAnimationSelector = @(
    [regex]::Matches(
        $pickaxeAnimationSelectorMatch.Groups['table'].Value,
        '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object {
            [Convert]::ToInt32($_.Groups['value'].Value, 16)
        }
)
$pickaxeTextSelector = @(
    [regex]::Matches(
        $pickaxeTextSelectorMatch.Groups['table'].Value,
        'TX_(?<value>[0-9a-f]{4})') |
        ForEach-Object {
            [Convert]::ToInt32($_.Groups['value'].Value, 16)
        }
)
$hardhatTextSelector = @(
    [regex]::Matches(
        $hardhatTextSelectorMatch.Groups['table'].Value,
        'TX_(?<value>[0-9a-f]{4})') |
        ForEach-Object {
            [Convert]::ToInt32($_.Groups['value'].Value, 16)
        }
)
$soldierTextSelectorLowBytes = @(
    [regex]::Matches(
        $soldierTextSelectorMatch.Groups['table'].Value,
        '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object {
            [Convert]::ToInt32($_.Groups['value'].Value, 16)
        }
)
$soldierTextSelector = @(
    $soldierTextSelectorLowBytes | ForEach-Object { 0x5900 + $_ }
)
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
    $agesScriptHelperSource -notmatch '(?ms)^pickaxeWorker_setAnimationFromVar03:.*?Interaction\.var03.*?rst_addAToHl.*?interactionSetAnimation' -or
    $agesScriptHelperSource -notmatch '(?ms)^pickaxeWorker_chooseRandomBlackTowerText:.*?getRandomNumber.*?and \$07.*?rst_addAToHl.*?>TX_1b00' -or
    $blackTowerHardhatSource -notmatch '(?ms)^@subid00:.*?interactionSetAlwaysUpdateBit.*?ld a,\$04.*?interactionSetAnimation.*?^@subid03:.*?interactionAnimateBasedOnSpeed.*?interactionPushLinkAwayAndUpdateDrawPriority' -or
    $agesMainScriptSource -notmatch '(?ms)^hardhatWorkerSubid00Script:.*?jumpifroomflagset \$20.*?TX_1001.*?wait 30.*?giveitem TREASURE_SHOVEL, \$00.*?wait 30.*?TX_1002.*?TX_1000.*?setanimation \$04' -or
    $agesMainScriptSource -notmatch '(?ms)^hardhatWorkerFunc_patrol:.*?hardhatWorker_decPatrolCounter.*?objectApplySpeed.*?wait 20.*?disableinput.*?turnToFaceLink.*?showloadedtext.*?wait 30.*?hardhatWorker_updatePatrolAnimation.*?enableinput' -or
    $agesScriptHelperSource -notmatch '(?ms)^hardhatWorker_chooseTextForPatroller:.*?cp \$04.*?getRandomNumber.*?and \$03.*?rst_addAToHl.*?>TX_1000' -or
    $blackTowerSoldierSource -notmatch '(?ms)^soldierSubid00:\s*^soldierSubid01:.*?GLOBALFLAG_FINISHEDGAME.*?GLOBALFLAG_0b.*?jr soldierSubid0c.*?^soldierSubid0c:.*?soldierInitGraphicsAndLoadScript.*?npcFaceLinkAndAnimate' -or
    $agesScriptHelperSource -notmatch '(?ms)^soldierGetRandomVar32Val:.*?getRandomNumber.*?and \$03.*?rst_addAToHl.*?>TX_5900' -or
    $room148VillagerSource -notmatch '(?ms)^@runSubid02:.*?objectSetCollideRadii.*?ld b,\$11.*?ld b,\$ef.*?objectCheckCollidedWithLink_ignoreZ.*?villagerSubid02Script_part2.*?Interaction\.var39.*?Interaction\.var3d' -or
    $agesMainScriptSource -notmatch '(?ms)^villagerSubid02Script_part2:.*?disableinput.*?SPEED_100.*?moveleft \$10.*?moveright \$10.*?villager_setLinkYToVar39.*?wait 10.*?enableinput') {
    throw 'Black Tower worker, soldier, blocker, or entrance behavior changed in the disassembly.'
}
if (-not $pickaxeAnimationSelectorMatch.Success -or
    -not $pickaxeTextSelectorMatch.Success -or
    -not $hardhatTextSelectorMatch.Success -or
    -not $soldierTextSelectorMatch.Success -or
    $pickaxeAnimationSelector.Count -ne 8 -or
    $pickaxeTextSelector.Count -ne 8 -or
    $hardhatTextSelector.Count -ne 5 -or
    $soldierTextSelector.Count -ne 4) {
    throw 'Black Tower animation/text selector tables are incomplete in scriptHelper.s.'
}

$blackTowerTextRows = [Collections.Generic.List[string]]::new()
$blackTowerTextRows.Add("# text-id`tutf8-base64")
foreach ($textId in @(
    0x0025, 0x1000, 0x1001, 0x1002,
    0x100a, 0x100b, 0x100c, 0x100d,
    0x1b01, 0x1b02, 0x1b03, 0x1b04, 0x1b05,
    0x590d, 0x590e, 0x590f)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Black Tower text TX_$($textId.ToString('x4'))."
    }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $blackTowerTextRows.Add("$($textId.ToString('x4'))`t$encoded")
}

$blackTowerSelectorRows = [Collections.Generic.List[string]]::new()
$blackTowerSelectorRows.Add("# selector`tindex`tvalue")
foreach ($selector in @(
    @{ Name = 'pickaxe-animation'; Values = $pickaxeAnimationSelector; Width = 2 },
    @{ Name = 'pickaxe-text'; Values = $pickaxeTextSelector; Width = 4 },
    @{ Name = 'hardhat-text'; Values = $hardhatTextSelector; Width = 4 },
    @{ Name = 'soldier-text'; Values = $soldierTextSelector; Width = 4 }
)) {
    for ($index = 0; $index -lt $selector.Values.Count; $index++) {
        $value = [int]$selector.Values[$index]
        if ($selector.Name -ne 'pickaxe-animation' -and
            -not $allTexts.ContainsKey($value)) {
            throw "Black Tower selector '$($selector.Name)' references missing TX_$($value.ToString('x4'))."
        }
        $blackTowerSelectorRows.Add(
            "$($selector.Name)`t$index`t$($value.ToString("x$($selector.Width)"))")
    }
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
    "blocker-wait`t10"
)

# Present indoor room 2:0e contains the stone boy and his grandmother. Both
# placed interactions survive across GLOBALFLAG_SAVED_NAYRU, but their native
# initializers change position, palette, animation, dialogue, and per-update
# facing behavior in place. Export both phases instead of approximating them
# with the base generic-NPC rows.
$room20eBoySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\boy.s')
if ($mainObjectSource -notmatch
        '(?ms)^group2Map0eObjectData:\s+obj_Interaction \$3c \$0d \$48 \$38\s+obj_Interaction \$3d \$00 \$48 \$4a\s+obj_End' -or
    $room20eBoySource -notmatch
        '(?ms)^@initSubid0d:.*?GLOBALFLAG_SAVED_NAYRU.*?jr nz,@@notStone.*?loadStoneNpcPalette.*?Interaction\.oamFlags\s+ld \(hl\),\$06.*?objectSetCollideRadius.*?Interaction\.var03\s+inc \(hl\).*?ld a,\$0c\s+jp interactionSetAnimation.*?^@@notStone:.*?ld bc,\$4868\s+call interactionSetPosition.*?Interaction\.oamFlags\s+ld \(hl\),\$02\s+jp boyLoadScript' -or
    $room20eBoySource -notmatch
        '(?ms)^boyRunSubid0d:.*?Interaction\.var03.*?jp nz,interactionPushLinkAwayAndUpdateDrawPriority.*?interactionRunScript\s+jp npcFaceLinkAndAnimate' -or
    $oldLadyInteractionSource -notmatch
        '(?ms)^@initSubid0:.*?ld a,\$03\s+call interactionSetAnimation.*?GLOBALFLAG_SAVED_NAYRU.*?jr z,@loadScript.*?ld a,\$01\s+ld e,Interaction\.var03\s+ld \(de\),a\s+ld bc,\$4878\s+call interactionSetPosition' -or
    $oldLadyInteractionSource -notmatch
        '(?ms)^@runSubid0:.*?interactionRunScript.*?Interaction\.var03.*?jp z,interactionAnimateAsNpc\s+jp npcFaceLinkAndAnimate' -or
    $agesMainScriptSource -notmatch
        '(?ms)^boySubid0dScript:\s+rungenericnpc TX_251c' -or
    $agesMainScriptSource -notmatch
        '(?ms)^oldLadySubid0Script:.*?GLOBALFLAG_SAVED_NAYRU, @notStone\s+rungenericnpc TX_3800\s+^@notStone:\s+rungenericnpc TX_3801') {
    throw 'Room 2:0e boy/old-lady placement or SAVED_NAYRU behavior changed in the disassembly.'
}

$room20eStateRows = [Collections.Generic.List[string]]::new()
$room20eStateRows.Add(
    "# actor`tphase`tgroup`troom`tid`tsubid`ty`tx`tpalette-kind`tpalette`tinitial-animation`tanimation-mode`tbehavior`ttext-id`tanimation`tsource`tutf8-base64")
$room20eStateSpecs = @(
    @{
        Actor = 'boy'; Phase = 'before-saved-nayru'; Id = 0x3c; Subid = 0x0d
        Y = 0x48; X = 0x38; PaletteKind = 'palh-a2'; Palette = 0x06
        InitialAnimation = 0x0c; AnimationMode = 'fixed'; Behavior = 'push'
        TextId = 0x0000
        Source = 'boy.s:@initSubid0d;boyRunSubid0d'
    },
    @{
        Actor = 'boy'; Phase = 'after-saved-nayru'; Id = 0x3c; Subid = 0x0d
        Y = 0x48; X = 0x68; PaletteKind = 'standard'; Palette = 0x02
        InitialAnimation = 0x02; AnimationMode = 'directional'; Behavior = 'face-animate'
        TextId = 0x251c
        Source = 'boy.s:@@notStone;boySubid0dScript'
    },
    @{
        Actor = 'old-lady'; Phase = 'before-saved-nayru'; Id = 0x3d; Subid = 0x00
        Y = 0x48; X = 0x4a; PaletteKind = 'standard'; Palette = 0x03
        InitialAnimation = 0x03; AnimationMode = 'directional'; Behavior = 'animate'
        TextId = 0x3800
        Source = 'oldLady.s:@initSubid0;oldLadySubid0Script'
    },
    @{
        Actor = 'old-lady'; Phase = 'after-saved-nayru'; Id = 0x3d; Subid = 0x00
        Y = 0x48; X = 0x78; PaletteKind = 'standard'; Palette = 0x03
        InitialAnimation = 0x03; AnimationMode = 'directional'; Behavior = 'face-animate'
        TextId = 0x3801
        Source = 'oldLady.s:@initSubid0;oldLadySubid0Script'
    }
)
foreach ($spec in $room20eStateSpecs) {
    $animation = Resolve-NpcAnimation ([int]$spec.Id) ([int]$spec.InitialAnimation)
    $textId = [int]$spec.TextId
    if (-not $animation -or
        $textId -ne 0 -and -not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve room 2:0e $($spec.Actor) $($spec.Phase) animation or text."
    }
    $message = if ($textId -eq 0) { '' } else { $allTexts[$textId] }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($message))
    $room20eStateRows.Add(@(
        $spec.Actor,
        $spec.Phase,
        '2',
        '0e',
        ([int]$spec.Id).ToString('x2'),
        ([int]$spec.Subid).ToString('x2'),
        ([int]$spec.Y).ToString('x2'),
        ([int]$spec.X).ToString('x2'),
        $spec.PaletteKind,
        ([int]$spec.Palette).ToString('x2'),
        ([int]$spec.InitialAnimation).ToString('x2'),
        $spec.AnimationMode,
        $spec.Behavior,
        $textId.ToString('x4'),
        $animation,
        $spec.Source,
        $encoded
    ) -join "`t")
}
if ($room20eStateRows.Count -ne 5) {
    throw "Expected four room 2:0e NPC state records, got $($room20eStateRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\room20e_npc_states.tsv'),
    $room20eStateRows)

# Past room 1:49's three placed characters are one shared interaction: the
# father and son play catch through wTmpcfc0.genericCutscene.cfd3 and
# INTERAC_BALL, while D7's essence bit and D8/Veran's completion room flag
# select the temporary stone tableau. Export every animation and dialogue
# selected by those handlers instead of leaving the two manual A-button
# branches with the generic TX_0000 fallback.
$room149ObjectSource = $mainObjectLines -join "`n"
$room149BoySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\boy.s')
$room149FatherSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\villager.s')
$room149ObserverSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\pastGuy.s')
$room149BallSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\ball.s')
$room149ScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$room149ScriptHelperSource = Read-ImportText (
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
$familySpawnerSource = Read-ImportText $familySpawnerSourcePath
$mainObjectSource = $mainObjectLines -join "`n"
$interactionConstantSource = Read-ImportText (
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
$runningBipinMatch = [regex]::Match(
    $pastBipinSource,
    '(?ms)^@bipin0:\s+ld h,d\s+ld l,Interaction\.speed\s+ld \(hl\),(?<speed>SPEED_[0-9]+)\s+ld l,Interaction\.angle\s+ld \(hl\),\$(?<angle>[0-9a-f]{2}).*?ld l,Interaction\.var3a\s+ld a,\$(?<animation>[0-9a-f]{2}).*?^@updateSpeed:\s+call objectApplySpeed\s+ld e,Interaction\.xh\s+ld a,\(de\)\s+sub \$(?<minimum>[0-9a-f]{2})\s+cp \$(?<span>[0-9a-f]{2})\s+ret c.*?ld l,Interaction\.angle\s+ld a,\(hl\)\s+xor \$(?<angleXor>[0-9a-f]{2}).*?ld l,Interaction\.var3a\s+ld a,\(hl\)\s+xor \$(?<animationXor>[0-9a-f]{2})')
if (-not $runningBipinMatch.Success) {
    throw 'Could not resolve Running Bipin $28:$00 native movement inputs from bipin.s.'
}
$runningBipinSpeedName = $runningBipinMatch.Groups['speed'].Value
$runningBipinSpeedMatch = [regex]::Match(
    $objectSpeedSource,
    "(?m)^\s*$([regex]::Escape($runningBipinSpeedName))\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})")
if (-not $runningBipinSpeedMatch.Success) {
    throw "Could not resolve Running Bipin object speed $runningBipinSpeedName."
}
$runningBipinInitialAnimation =
    [Convert]::ToInt32($runningBipinMatch.Groups['animation'].Value, 16)
$runningBipinAnimationXor =
    [Convert]::ToInt32($runningBipinMatch.Groups['animationXor'].Value, 16)
$runningBipinAlternateAnimation =
    $runningBipinInitialAnimation -bxor $runningBipinAnimationXor
$runningBipinInitialAnimationData =
    Resolve-NpcAnimation 0x28 $runningBipinInitialAnimation
$runningBipinAlternateAnimationData =
    Resolve-NpcAnimation 0x28 $runningBipinAlternateAnimation
if (-not $runningBipinInitialAnimationData -or
    -not $runningBipinAlternateAnimationData) {
    throw 'Could not resolve Running Bipin $28:$00 toggle animations.'
}
$runningBipinRows = @(
    "# speed-raw`tinitial-angle`tminimum-x`tspan-x`treverse-angle-xor`tinitial-animation`tanimation-toggle-xor`tinitial-animation-data`talternate-animation-data`tsource",
    "$($runningBipinSpeedMatch.Groups['value'].Value)`t$($runningBipinMatch.Groups['angle'].Value)`t$($runningBipinMatch.Groups['minimum'].Value)`t$($runningBipinMatch.Groups['span'].Value)`t$($runningBipinMatch.Groups['angleXor'].Value)`t$($runningBipinMatch.Groups['animation'].Value)`t$($runningBipinMatch.Groups['animationXor'].Value)`t$runningBipinInitialAnimationData`t$runningBipinAlternateAnimationData`tobject_code/common/interactions/bipin.s:@bipin0;@updateSpeed"
)
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
    0x4311, 0x4312, 0x4313,
    0x4407, 0x4408, 0x4409, 0x440a
)
$familyScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$familyScriptHelperSource = Read-ImportText (
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
$familyRows.Add("# group`troom`tstage`tpersonality`tid`tsubid`ty`tx`tvar03`ttext-id`tsprite`ttile-base`tpalette`tdefault-animation`tcan-face`tup-animation`tright-animation`tdown-animation`tleft-animation`tutf8-base64`timplementation")
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
            ([int]$actor.Y) ([int]$actor.X) $var03 $textId $initialAnimation 0 `
            'specialized-native'
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
    # var03, y, x, text, initial animation. @val01/@val0a overwrite var38
    # with @setTextAndPosition's zero return, so passage Impa starts facing up.
    @(0x00, 0x38, 0x38, 0x0120, 0x02),
    @(0x01, 0x48, 0x28, 0x0121, 0x00),
    @(0x02, 0x28, 0x68, 0x0122, 0x02),
    @(0x05, 0x28, 0x68, 0x0123, 0x02),
    @(0x09, 0x38, 0x38, 0x0120, 0x02),
    @(0x0a, 0x48, 0x28, 0x0121, 0x00),
    @(0x0b, 0x28, 0x68, 0x0122, 0x02),
    @(0x0d, 0x28, 0x68, 0x012c, 0x02),
    @(0x0e, 0x28, 0x68, 0x0123, 0x02)
)
foreach ($variant in $impaHouseVariants) {
    $textId = [int]$variant[3]
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Impa house text TX_$($textId.ToString('x4'))."
    }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $npcRows.Add(
        "3`t9e`t4f`t00`t$(([int]$variant[1]).ToString('x2'))`t$(([int]$variant[2]).ToString('x2'))`t$(([int]$variant[0]).ToString('x2'))`t$($textId.ToString('x4'))`t$impaSpriteName`t$($impaGraphic.TileBase)`t$($impaGraphic.Palette)`t$(([int]$variant[4]).ToString('x2'))`t1`t$impaUpOam`t$impaRightOam`t$impaDownOam`t$impaLeftOam`t$encoded`tspecialized-native")
}
if ($npcRows.Count -ne 384) {
    throw "Expected 374 clean-US positioned and 9 state-derived NPC records, got $($npcRows.Count - 1)."
}
$npcImplementationCounts = @{}
foreach ($npcRow in $npcRows | Select-Object -Skip 1) {
    $implementation = ($npcRow -split "`t")[-1]
    $npcImplementationCounts[$implementation] =
        1 + [int]$npcImplementationCounts[$implementation]
}
if ($npcImplementationCounts['ordinary-generic'] -ne 61 -or
    $npcImplementationCounts['specialized-native'] -ne 84 -or
    $npcImplementationCounts['event-owned'] -ne 22 -or
    $npcImplementationCounts['deliberately-unsupported'] -ne 216 -or
    $npcImplementationCounts.Count -ne 4) {
    throw "NPC implementation classification manifest changed: $($npcImplementationCounts | Out-String)"
}
foreach ($familyRow in $familyRows | Select-Object -Skip 1) {
    if (($familyRow -split "`t")[-1] -ne 'specialized-native') {
        throw 'Every generated Bipin/Blossom family actor must be specialized-native.'
    }
}

# Impa's state 0 replaces only wRoomLayout+$22. The rendered metatile remains
# the original hidden-floor graphic while collision/warp logic sees indoor
# down-staircase $45.
if ($tileIndexSource -notmatch '(?m)^\.define TILEINDEX_INDOOR_DOWNSTAIRCASE\s+\$45\b') {
    throw 'TILEINDEX_INDOOR_DOWNSTAIRCASE is no longer $45.'
}
$nayruHouseRows = @(
    "# group`troom`tinteraction-id`tsubid`tstair-position`tstair-tile`tpreserve-rendered`tsource",
    "3`t9e`t4f`t00`t22`t45`t1`timpaNpc.s:impaNpc_subid00"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\nayru_house.tsv'),
    $nayruHouseRows)

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
    $source = Read-ImportText $sourcePath
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
Add-NpcGameProgress2DialogueTable 0x43 @(0x01, 0x02) -1 'pastGuy.s' '@subid1And2ScriptTable'

# Soldier subids $00/$01 both select TX_5901 once GLOBALFLAG_0b is set.
# Their initial TX_5900/TX_5902 remains in each base NPC row.
if ($agesMainScriptSource -notmatch '(?ms)^soldierSubid00Script:\s+jumpifglobalflagset \$0b, script5df5\s+rungenericnpc TX_5900\s+^script5df5:\s+rungenericnpc TX_5901\s+^soldierSubid01Script:\s+jumpifglobalflagset \$0b, script5dff\s+rungenericnpc TX_5902\s+^script5dff:\s+rungenericnpc TX_5901') {
    throw 'Soldier $40:$00/$01 GLOBALFLAG_0b dialogue selection changed in scripts.s.'
}
$soldierPostFlagText = 0x5901
if (-not $allTexts.ContainsKey($soldierPostFlagText)) {
    throw 'Could not resolve soldier post-GLOBALFLAG_0b text TX_5901.'
}
$soldierPostFlagEncoded = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($allTexts[$soldierPostFlagText]))
foreach ($soldierSubid in @(0x00, 0x01)) {
    $npcDialogueRows.Add(
        "40`t$($soldierSubid.ToString('x2'))`t*`tglobal-flag`t0b`t*`t5901`tscripts.s:soldierSubid$($soldierSubid.ToString('x2'))Script`t$soldierPostFlagEncoded")
}

# These scripts switch text, but not behavior, when GLOBALFLAG_0b is set.
# Their first texts remain immutable base-row data.
if ($agesMainScriptSource -notmatch '(?ms)^pastHobo2Script:\s+jumpifglobalflagset GLOBALFLAG_0b, \+\s+rungenericnpc TX_1620\s+^\+\s+rungenericnpc TX_1621' -or
    $agesMainScriptSource -notmatch '(?ms)^mustacheManScript:\s+jumpifglobalflagset GLOBALFLAG_0b, \+\+\s+rungenericnpclowindex <TX_0f00\s+^\+\+\s+rungenericnpclowindex <TX_0f01' -or
    $agesMainScriptSource -notmatch '(?ms)^pastGuySubid0Script:\s+jumpifglobalflagset GLOBALFLAG_0b, \+\s+rungenericnpclowindex <TX_1710\s+^\+\s+rungenericnpclowindex <TX_1711') {
    throw 'Room 1:82/1:92/1:93/1:94 GLOBALFLAG_0b dialogue selection changed in scripts.s.'
}
foreach ($dialogueRule in @(
    @(0x44, 0x00, 0x1621, 'pastHobo2Script'),
    @(0x42, 0x00, 0x0f01, 'mustacheManScript'),
    @(0x43, 0x00, 0x1711, 'pastGuySubid0Script')
)) {
    $id = [int]$dialogueRule[0]
    $subid = [int]$dialogueRule[1]
    $textId = [int]$dialogueRule[2]
    $source = [string]$dialogueRule[3]
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve $source text TX_$($textId.ToString('x4'))."
    }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $npcDialogueRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t*`tglobal-flag`t0b`t*`t$($textId.ToString('x4'))`tscripts.s:$source`t$encoded")
}

# Zelda remains in Nayru's house across the rescue flag transition, changing
# only from TX_0605 to TX_0606.
$savedNayruFlag = $globalFlagValues['GLOBALFLAG_SAVED_NAYRU']
$zeldaSavedNayruText = 0x0606
$zeldaSavedNayruEncoded = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($allTexts[$zeldaSavedNayruText]))
$npcDialogueRows.Add(
    "ad`t07`t*`tglobal-flag`t$($savedNayruFlag.ToString('x2'))`t*`t0606`tzelda.s:@initSubid07`t$zeldaSavedNayruEncoded")

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

if ($npcDialogueRows.Count -ne 123) {
    throw "Expected 122 imported NPC dialogue predicates, got $($npcDialogueRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\npc_dialogue.tsv'),
    $npcDialogueRows)

# INTERAC_LINKED_GAME_GHINI `$cb and INTERAC_GREAT_FAIRY `$d5:$00 both
# install linkedGameNpcScript. Export each complete five-text choice loop and
# progression constants; the runtime substitutes the generated five-character
# secret for the text engine's \secret1 command.
$linkedGhiniSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\linkedGameGhini.s')
$greatFairySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\greatFairy.s')
$linkedNpcScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
if ($linkedGhiniSource -notmatch '(?ms)^interactionCodecb:.*?Interaction\.oamFlags\s+ld \(hl\),\$02.*?Interaction\.var3f\s+ld \(hl\),GRAVEYARD_SECRET & \$0f.*?mainScripts\.linkedGameNpcScript' -or
    $linkedGhiniSource -notmatch '(?ms)^@initialize:.*?interactionInitGraphics.*?objectMarkSolidPosition.*?interactionIncState' -or
    $greatFairySource -notmatch '(?ms)^greatFairy_subid0:.*?greatFairy_initialize.*?interactionSetAlwaysUpdateBit.*?Interaction\.zh\s+ld \(hl\),\$f0.*?Interaction\.var3f\s+ld \(hl\),TEMPLE_SECRET & \$0f' -or
    $greatFairySource -notmatch '(?ms)^@state1:.*?returnIfScrollMode01Unset.*?interactionRunScript.*?Interaction\.var3e.*?interactionAnimateAsNpc.*?wFrameCounter.*?and \$07.*?and \$38.*?@zPositions:\s+\.db \$ff \$fe \$ff \$00 \$01 \$02 \$01 \$00' -or
    $greatFairySource -notmatch '(?ms)^greatFairy_initialize:.*?interactionInitGraphics.*?objectMarkSolidPosition.*?@scriptTable:\s+\.dw mainScripts\.greatFairySubid0Script' -or
    $linkedNpcScriptSource -notmatch '(?ms)^greatFairySubid0Script:.*?linkedNpc_checkShouldSpawn.*?objectSetInvisible.*?Interaction\.var3e, \$01.*?greatFairy_checkScreenIsScrolling.*?playsound SND_KILLENEMY.*?createpuff.*?wait 32.*?setmusic MUS_FAIRY_FOUNTAIN.*?objectSetVisible.*?Interaction\.var3e, \$00.*?scriptjump linkedGameNpcScript' -or
    $linkedNpcScriptSource -notmatch '(?ms)^linkedGameNpcScript:.*?linkedNpc_checkShouldSpawn.*?@offerSecret:.*?linkedNpc_calcLowTextIndex, \$00.*?jumpiftextoptioneq \$00, @answeredYes.*?addobjectbyte Interaction\.textID, \$01.*?@showExtraText:.*?linkedNpc_calcLowTextIndex, \$02.*?@generateSecret:.*?linkedNpc_generateSecret.*?linkedNpc_calcLowTextIndex, \$03.*?@tellSecret:.*?jumpiftextoptioneq \$01, @tellSecret.*?linkedNpc_calcLowTextIndex, \$04' -or
    $linkedNpcScriptHelperSource -notmatch '(?ms)^linkedNpc_checkShouldSpawn:.*?\.dw @checkd1.*?\.dw @checkd2.*?^@checkd1:\s+ld a,\$00.*?^@checkd2:\s+ld a,\$01' -or
    $linkedNpcScriptHelperSource -notmatch '(?ms)^linkedNpc_checkHasExtraTextBox:.*?^@data:\s+\.db \$01 \$01 \$01 \$00 \$00 \$00 \$01 \$00 \$00 \$01' -or
    $linkedNpcScriptHelperSource -notmatch '(?ms)^linkedNpc_generateSecret:.*?GLOBALFLAG_FIRST_AGES_BEGAN_SECRET.*?ld a,\$20.*?ld \(wShortSecretIndex\),a.*?ld bc,\$0003' -or
    $musicIdSource -notmatch '(?m)^\s*MUS_FAIRY_FOUNTAIN\s+db\s+; \$0f' -or
    $musicIdSource -notmatch '(?m)^\s*SND_KILLENEMY\s+db\s+; \$73' -or
    $musicIdSource -notmatch '(?m)^\s*SND_POOF\s+db\s+; \$98') {
    throw 'Linked-game Ghini or room 0:83 Great Fairy behavior changed in the disassembly.'
}

# linkedNpc_generateSecret indexes the active non-Japanese XOR and display
# symbol tables directly. Decode both source tables here so production secret
# generation has no copied bank-0/bank-3 constants.
$secretCipherSource = Read-ImportText (
    Join-Path $Disassembly 'code\bank3.s')
$secretCipherMatch = [regex]::Match(
    $secretCipherSource,
    '(?ms)^secretXorCipher:\s*.*?^\.else\s*\r?\n(?<table>.*?)^\.endif')
$secretCipher = @(
    [regex]::Matches(
        $secretCipherMatch.Groups['table'].Value,
        '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object {
            [Convert]::ToInt32($_.Groups['value'].Value, 16)
        }
)
$secretSymbolsMatch = [regex]::Match(
    $bank0Source,
    '(?ms)^secretSymbols:.*?^\.ifndef REGION_JP\s*\r?\n(?<table>.*?)^[ \t]*\.db \$00[^\r\n]*\r?\n^\.endif')
$secretSymbolControls = @{
    0x10 = '\circle'
    0x11 = '\club'
    0x12 = '\diamond'
    0x13 = '\spade'
    0x15 = '\up'
    0x16 = '\down'
    0x17 = '\left'
    0x18 = '\right'
    0x7e = '\triangle'
    0x7f = '\rectangle'
    0xbd = '\heart'
}
$secretSymbols = [Collections.Generic.List[string]]::new()
foreach ($line in ($secretSymbolsMatch.Groups['table'].Value -split '\r?\n')) {
    if ($line -match '^\s*\.asc\s+"(?<text>[^"]*)"') {
        foreach ($character in $Matches['text'].ToCharArray()) {
            $secretSymbols.Add([string]$character)
        }
        continue
    }
    if ($line -notmatch '^\s*\.db\s+') { continue }
    foreach ($byteMatch in [regex]::Matches(
            $line, '\$(?<value>[0-9a-f]{2})')) {
        $value = [Convert]::ToInt32(
            $byteMatch.Groups['value'].Value, 16)
        if ($secretSymbolControls.ContainsKey($value)) {
            $secretSymbols.Add([string]$secretSymbolControls[$value])
        } elseif ($value -ge 0x20 -and $value -le 0x7d) {
            $secretSymbols.Add([string][char]$value)
        } else {
            throw "secretSymbols references unsupported source glyph `$$($value.ToString('x2'))."
        }
    }
}
if (-not $secretCipherMatch.Success -or $secretCipher.Count -ne 48 -or
    -not $secretSymbolsMatch.Success -or $secretSymbols.Count -ne 64) {
    throw 'Linked-secret XOR/symbol tables are incomplete in bank3.s/bank0.s.'
}
$linkedSecretCipherRows = [Collections.Generic.List[string]]::new()
$linkedSecretCipherRows.Add("# index`txor")
for ($index = 0; $index -lt $secretCipher.Count; $index++) {
    $linkedSecretCipherRows.Add(
        "$index`t$(([int]$secretCipher[$index]).ToString('x2'))")
}
$linkedSecretSymbolRows = [Collections.Generic.List[string]]::new()
$linkedSecretSymbolRows.Add("# index`tutf8-base64")
for ($index = 0; $index -lt $secretSymbols.Count; $index++) {
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($secretSymbols[$index]))
    $linkedSecretSymbolRows.Add("$index`t$encoded")
}

$linkedNpcRows = [Collections.Generic.List[string]]::new()
$linkedNpcRows.Add(
    "# group`troom`tid`tsubid`tsecret-index`tshort-secret-index`tbegan-flag`thas-extra-text`toffer-text-id`trefusal-text-id`texplanation-text-id`tsecret-text-id`tfinal-text-id`toffer-utf8-base64`trefusal-utf8-base64`texplanation-utf8-base64`tsecret-utf8-base64`tfinal-utf8-base64`tsource")
foreach ($linkedNpc in @(
    @{
        Group = 0x00; Room = 0x5d; Id = 0xcb; SubId = 0x00
        SecretIndex = 0x01; BeganFlag = 'GLOBALFLAG_BEGAN_GRAVEYARD_SECRET'
        Source = 'linkedGameGhini.s;linkedGameNpcScript;scriptHelper.s:linkedNpc_generateSecret'
    },
    @{
        Group = 0x00; Room = 0x83; Id = 0xd5; SubId = 0x00
        SecretIndex = 0x06; BeganFlag = 'GLOBALFLAG_BEGAN_TEMPLE_SECRET'
        Source = 'greatFairy.s:greatFairy_subid0;greatFairySubid0Script;linkedGameNpcScript;scriptHelper.s:linkedNpc_generateSecret'
    }
)) {
    $textIds = 0..4 | ForEach-Object {
        0x4d00 + [int]$linkedNpc.SecretIndex * 5 + $_
    }
    foreach ($textId in $textIds) {
        if (-not $allTexts.ContainsKey($textId)) {
            throw "Could not resolve linked-game NPC text TX_$($textId.ToString('x4'))."
        }
    }
    if (-not $globalFlagValues.ContainsKey($linkedNpc.BeganFlag)) {
        throw "Could not resolve linked-game NPC flag $($linkedNpc.BeganFlag)."
    }
    $columns = @(
        ([int]$linkedNpc.Group).ToString('x1'),
        ([int]$linkedNpc.Room).ToString('x2'),
        ([int]$linkedNpc.Id).ToString('x2'),
        ([int]$linkedNpc.SubId).ToString('x2'),
        ([int]$linkedNpc.SecretIndex).ToString('x2'),
        (0x20 + [int]$linkedNpc.SecretIndex).ToString('x2'),
        $globalFlagValues[$linkedNpc.BeganFlag].ToString('x2'),
        '1'
    ) + @($textIds | ForEach-Object { $_.ToString('x4') }) +
        @($textIds | ForEach-Object {
            [Convert]::ToBase64String(
                [Text.Encoding]::UTF8.GetBytes($allTexts[$_]))
        }) + @([string]$linkedNpc.Source)
    $linkedNpcRows.Add($columns -join "`t")
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\linked_game_npcs.tsv'),
    $linkedNpcRows)
Write-GeneratedTable(
    (Join-Path $destination 'objects\linked_secret_cipher.tsv'),
    $linkedSecretCipherRows)
Write-GeneratedTable(
    (Join-Path $destination 'objects\linked_secret_symbols.tsv'),
    $linkedSecretSymbolRows)

# State-selected position overrides remain separate from visibility and text.
# INTERAC_MISC_MAN_2 $44:$04 moves only in getGameProgress_2 state $06;
# every other living state uses its object-data position $48,$48.
$npcPositionRows = @(
    "# id`tsubid`tvar03`tkind`tvalue`ty`tx`tsource",
    "44`t04`t*`tgame-progress-2`t06`t58`t78`tmiscMan2.s:@subid4",
    "58`t02`t*`tcurrent-room-flag`t80`t38`t58`thardhatWorker.s:@@state0"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\npc_positions.tsv'),
    $npcPositionRows)

# INTERAC_MISCELLANEOUS_2 $dc:$07 is a general static Heart Piece spawner.
# Its state-0 handler deletes itself when ROOMFLAG_ITEM is set; otherwise it
# creates TREASURE_OBJECT_HEART_PIECE_00 at the spawner's exact position.
$miscellaneous2Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$treasureSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\treasure.s')
$treasureObjectSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'objects\tile_change_watchers.tsv'),
    $tileChangeWatcherRows)

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
    "# group`troom`torder`ty`tx`ttreasure-object`tsprite`ttile-base`tpalette`tanimation`tcompletion-text-id`tcompletion-text-base64`trequire-room-item-clear`tset-room-item`tstate-address`tstate-mask`tstate-value`trequire-treasure-clear`tspawn-mode`tgrab-mode`tinitial-speed-z`tgravity`tmove-speed`tsource")
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
            "$currentGroup`t$($currentRoom.ToString('x2'))`t$objectOrder`t$($Matches['y'])`t$($Matches['x'])`tTREASURE_OBJECT_HEART_PIECE_00`t$heartPieceSprite`t$($heartPieceGraphic.TileBase)`t$($heartPieceGraphic.Palette)`t$heartPieceAnimation`t$($heartContainerFollowupText.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$heartContainerFollowupText])))`t1`t1`t0000`t00`t00`t00`t0`t2`t0`t0`t0`tmiscellaneous2.s:interactiondc_subid07")
    }
    $objectOrder++
}
if ($groundTreasureRows.Count -ne 9) {
    throw "Expected eight static Heart Piece spawners, got $($groundTreasureRows.Count - 1)."
}

# INTERAC_RICKYS_GLOVE_SPAWNER $74:$00 creates the ordinary Ricky's Gloves
# treasure only after Link has heard Ricky's explanation, before the gloves
# have been returned, and while treasure $48 is absent. TREASURE_OBJECT_
# RICKY_GLOVES_00 uses spawn mode $05: breaking its remembered dirt tile
# launches it at SPEED_080 with speedZ=-$100 and gravity $10 until its bounces
# stop, after which grab mode $01 holds it above Link with one hand.
$rickyGloveSpawnerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\rickysGloveSpawner.s')
$rickyWramSource = Read-ImportText (
    Join-Path $Disassembly 'include\wram.s')
$rickyGloveObjectBlock = [regex]::Match(
    $mainObjectSource,
    '(?ms)^group0Map98ObjectData:\s+obj_Interaction \$74 \$00 \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s+obj_Interaction \$71 \$06\s+obj_End')
$rickyGloveObject =
    $treasureObjectRecords['TREASURE_OBJECT_RICKY_GLOVES_00']
if (-not $rickyGloveObjectBlock.Success -or
    $rickyGloveObjectBlock.Groups['y'].Value -ne '28' -or
    $rickyGloveObjectBlock.Groups['x'].Value -ne '48' -or
    $rickyGloveSpawnerSource -notmatch
        '(?ms)^interactionCode74:\s+.*?wRickyState.*?bit 5,a.*?and \$01.*?TREASURE_RICKY_GLOVES.*?checkTreasureObtained.*?ldbc INTERAC_TREASURE, TREASURE_RICKY_GLOVES.*?objectCreateInteraction.*?interactionDelete' -or
    $rickyWramSource -notmatch '(?m)^wRickyState: ; \$c646/\$c643\s*$' -or
    $treasureObjectSource -notmatch
        '(?m)^\s*/\* \$48 \*/ m_TreasureSubid\s+\$51, \$01, \$67, \$55, TREASURE_OBJECT_RICKY_GLOVES_00\s*$' -or
    $null -eq $rickyGloveObject -or
    $rickyGloveObject.Treasure -ne 0x48 -or
    $rickyGloveObject.Subid -ne 0 -or
    $rickyGloveObject.Parameter -ne 1 -or
    $rickyGloveObject.TextId -ne 0x67 -or
    $rickyGloveObject.Graphic -ne 0x55) {
    throw "Room 0:98 Ricky's Gloves spawner or treasure object changed."
}
$rickyGloveGraphic = $interactionGraphics['96:85']
if ($null -eq $rickyGloveGraphic -or
    -not $gfxNames.ContainsKey($rickyGloveGraphic.Gfx)) {
    throw "Could not resolve room 0:98 Ricky's Gloves visual or TX_0067."
}
$rickyGloveAnimation = Resolve-TreasureAnimation (
    [int]$rickyGloveGraphic.DefaultAnimation)
if ([string]::IsNullOrWhiteSpace($rickyGloveAnimation) -or
    [string]::IsNullOrWhiteSpace($rickyGloveObject.Message)) {
    throw "Could not resolve room 0:98 Ricky's Gloves visual or TX_0067."
}
$rickyGloveSprite = $gfxNames[$rickyGloveGraphic.Gfx]
[void]$npcSpriteNames.Add($rickyGloveSprite)
$groundTreasureRows.Add(
    "0`t98`t0`t28`t48`tTREASURE_OBJECT_RICKY_GLOVES_00`t$rickyGloveSprite`t$($rickyGloveGraphic.TileBase)`t$($rickyGloveGraphic.Palette)`t$rickyGloveAnimation`t0000`t`t0`t0`tc646`t21`t01`t48`t5`t1`t-256`t16`t14`trickysGloveSpawner.s:interactionCode74;treasure.s:@spawnMode5")
if ($groundTreasureRows.Count -ne 10) {
    throw "Expected eight Heart Pieces and room 0:98 Ricky's Gloves, got $($groundTreasureRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\ground_treasures.tsv'),
    $groundTreasureRows)

# Room 2:e3 contains the Bombs form of INTERAC_MISCELLANEOUS_1 $6b:$0a.
# Its shared script refills wNumBombs from wMaxBombs before giveitem runs,
# then preserves the same strict collection gate and 30-update input lease as
# the later Cheval Rope and Flippers forms.
$room2e3ObjectBlock = [regex]::Match(
    $mainObjectSource,
    '(?ms)^group2Mape3ObjectData:\s+obj_Interaction \$6b \$0a \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s+obj_Pointer group2Mape3EnemyObjectData\s+obj_End')
$room2e3MiscSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
$room2e3ScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$room2e3ScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$room2e3Graphic = $interactionGraphics['107:10']
$room2e3Animation = Resolve-NpcAnimation 0x6b 0x01
$room2e3TreasureObject =
    $treasureObjectRecords['TREASURE_OBJECT_BOMBS_04']
if (-not $room2e3ObjectBlock.Success -or
    $room2e3ObjectBlock.Groups['y'].Value -ne '28' -or
    $room2e3ObjectBlock.Groups['x'].Value -ne '28') {
    throw 'Room 2:e3 no longer has its original $6b:$0a Bombs object.'
}
if ($room2e3MiscSource -notmatch
        '(?ms)^interaction6b_subid0a:\s+interaction6b_subid0b:\s+interaction6b_subid0c:.*?ROOMFLAG_BIT_ITEM.*?sub \$0a.*?interaction6b_initGraphicsAndLoadScript.*?wDisabledObjects.*?wMenuDisabled.*?interactionAnimateAsNpc' -or
    $room2e3ScriptSource -notmatch
        '(?ms)^interaction6b_subid0aScript:\s+setcollisionradii \$02, \$02.*?disableinput.*?objectSetInvisible.*?writeobjectbyte Interaction\.substate, \$01.*?jumptable_objectbyte Interaction\.var03.*?\.dw @bombs.*?^@bombs:\s+asm15 scriptHelp\.interaction6b_refillBombs\s+giveitem TREASURE_BOMBS, \$04\s+wait 30\s+scriptend' -or
    $room2e3ScriptHelperSource -notmatch
        '(?ms)^interaction6b_checkLinkCanCollect:\s+ld hl,w1Link\.zh\s+ld a,\(hl\)\s+or a\s+ret nz\s+ld a,\(wLinkGrabState\)\s+or a\s+ret nz\s+ld c,\$0e\s+call objectCheckLinkWithinDistance.*?Interaction\.var38' -or
    $room2e3ScriptHelperSource -notmatch
        '(?ms)^interaction6b_refillBombs:\s+ld hl,wMaxBombs\s+ldd a,\(hl\)\s+ld \(hl\),a\s+ret') {
    throw 'Room 2:e3 Bombs pickup, refill, input lease, or collection check changed.'
}
if ($null -eq $room2e3TreasureObject -or
    $room2e3TreasureObject.Treasure -ne 0x03 -or
    $room2e3TreasureObject.Subid -ne 0x04 -or
    $room2e3TreasureObject.Parameter -ne 0x00 -or
    $room2e3TreasureObject.Graphic -ne 0x05 -or
    $null -eq $room2e3Graphic -or
    $room2e3Graphic.Gfx -ne 0x78 -or
    $room2e3Graphic.TileBase -ne 0x10 -or
    $room2e3Graphic.Palette -ne 0x04 -or
    $room2e3Graphic.DefaultAnimation -ne 0x01 -or
    $gfxNames[0x78] -ne 'spr_common_items' -or
    -not $room2e3Animation) {
    throw 'Could not resolve room 2:e3 Bombs $03:$04 or its $6b:$0a visual.'
}
[void]$npcSpriteNames.Add('spr_common_items')
$room2e3Rows = @(
    '# group`troom`torder`tid`tsubid`ty`tx`tvar03`titem-room-flag`ttreasure-object`ttreasure-id`ttreasure-subid`ttreasure-parameter`tpost-grant-wait`tcollision-radius-y`tcollision-radius-x`tpickup-distance`tsprite`ttile-base`tpalette`tanimation-index`tanimation`tsource',
    "2`te3`t0`t6b`t0a`t28`t28`t00`t20`tTREASURE_OBJECT_BOMBS_04`t03`t04`t00`t30`t02`t02`t0e`tspr_common_items`t10`t04`t01`t$room2e3Animation`tmainData.s:group2Mape3ObjectData"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\room2e3_interactions.tsv'),
    $room2e3Rows)

# Room 5:b6 contains the Cheval Rope form of INTERAC_MISCELLANEOUS_1
# $6b:$0a-$0c. Its shared script loads radii $02,$02 but collects through
# objectCheckLinkWithinDistance's strict Manhattan c=$0e gate, grants the
# source treasure, clears the live remembered-companion ID after the held-item
# command returns, then retains its input lease for 30 updates.
$room5b6ObjectBlock = [regex]::Match(
    $mainObjectSource,
    '(?ms)^group5Mapb6ObjectData:\s+obj_Interaction \$6b \$0b \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s+obj_Pointer group5Mapb6EnemyObjectData\s+obj_End')
$room5b6MiscSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
$room5b6ScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$room5b6ScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$room5b6Graphic = $interactionGraphics['107:11']
$room5b6Animation = Resolve-NpcAnimation 0x6b 0x02
$room5b6TreasureObject =
    $treasureObjectRecords['TREASURE_OBJECT_CHEVAL_ROPE_00']
if (-not $room5b6ObjectBlock.Success -or
    $room5b6ObjectBlock.Groups['y'].Value -ne '48' -or
    $room5b6ObjectBlock.Groups['x'].Value -ne '28') {
    throw 'Room 5:b6 no longer has its original $6b:$0b Cheval Rope object.'
}
if ($room5b6MiscSource -notmatch
        '(?ms)^interaction6b_subid0a:\s+interaction6b_subid0b:\s+interaction6b_subid0c:.*?ROOMFLAG_BIT_ITEM.*?sub \$0a.*?interaction6b_initGraphicsAndLoadScript.*?wDisabledObjects.*?wMenuDisabled.*?interactionAnimateAsNpc' -or
    $room5b6ScriptSource -notmatch
        '(?ms)^interaction6b_subid0aScript:\s+setcollisionradii \$02, \$02.*?disableinput.*?objectSetInvisible.*?writeobjectbyte Interaction\.substate, \$01.*?jumptable_objectbyte Interaction\.var03.*?\.dw @chevalRope.*?^@chevalRope:\s+giveitem TREASURE_CHEVAL_ROPE, \$00\s+writememory wRememberedCompanionId, \$00\s+wait 30\s+scriptend' -or
    $room5b6ScriptHelperSource -notmatch
        '(?ms)^interaction6b_checkLinkCanCollect:\s+ld hl,w1Link\.zh\s+ld a,\(hl\)\s+or a\s+ret nz\s+ld a,\(wLinkGrabState\)\s+or a\s+ret nz\s+ld c,\$0e\s+call objectCheckLinkWithinDistance.*?Interaction\.var38') {
    throw 'Room 5:b6 Cheval Rope pickup, input lease, or collection check changed.'
}
if ($null -eq $room5b6TreasureObject -or
    $room5b6TreasureObject.Treasure -ne 0x52 -or
    $room5b6TreasureObject.Subid -ne 0x00 -or
    $room5b6TreasureObject.Parameter -ne 0x00 -or
    $room5b6TreasureObject.Graphic -ne 0x3c -or
    $null -eq $room5b6Graphic -or
    $room5b6Graphic.Gfx -ne 0x81 -or
    $room5b6Graphic.TileBase -ne 0x10 -or
    $room5b6Graphic.Palette -ne 0x03 -or
    $room5b6Graphic.DefaultAnimation -ne 0x02 -or
    $gfxNames[0x81] -ne 'spr_quest_items_2' -or
    -not $room5b6Animation) {
    throw 'Could not resolve room 5:b6 Cheval Rope $52:$00 or its $6b:$0b visual.'
}
[void]$npcSpriteNames.Add('spr_quest_items_2')
$room5b6Rows = @(
    '# group`troom`torder`tid`tsubid`ty`tx`tvar03`titem-room-flag`ttreasure-object`ttreasure-id`ttreasure-subid`ttreasure-parameter`tpost-grant-wait`tcollision-radius-y`tcollision-radius-x`tpickup-distance`tremembered-id-value`tsprite`ttile-base`tpalette`tanimation-index`tanimation`tsource',
    "5`tb6`t0`t6b`t0b`t48`t28`t01`t20`tTREASURE_OBJECT_CHEVAL_ROPE_00`t52`t00`t00`t30`t02`t02`t0e`t00`tspr_quest_items_2`t10`t03`t02`t$room5b6Animation`tmainData.s:group5Mapb6ObjectData"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\room5b6_interactions.tsv'),
    $room5b6Rows)

# Room 5:bf's ordered object stream is one flippers pickup, two blocks which
# consume wLever1PullDistance before the lever updates it, then INTERAC_LEVER.
# The lever creates its graphical connection child during state 0; export that
# child immediately after its parent so the runtime retains the same update
# order without depending on a clone-side room exception.
$room5bfObjectBlock = [regex]::Match(
    $mainObjectSource,
    '(?ms)^group5MapbfObjectData:\s+obj_Interaction \$6b \$0c \$(?<flippersY>[0-9a-f]{2}) \$(?<flippersX>[0-9a-f]{2})\s+obj_Interaction \$6b \$0d \$(?<leftY>[0-9a-f]{2}) \$(?<leftX>[0-9a-f]{2})\s+obj_Interaction \$6b \$0d \$(?<rightY>[0-9a-f]{2}) \$(?<rightX>[0-9a-f]{2})\s+obj_Interaction \$61 \$(?<leverSubid>[0-9a-f]{2}) \$(?<leverY>[0-9a-f]{2}) \$(?<leverX>[0-9a-f]{2})\s+obj_End')
$room5bfMiscSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
$room5bfLeverSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\lever.s')
$room5bfScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$room5bfFlippersGraphic = $interactionGraphics['107:12']
$room5bfBlockGraphic = $interactionGraphics['107:13']
$room5bfLeverGraphic = $interactionGraphics['97:0']
$room5bfFlippersAnimation = Resolve-NpcAnimation 0x6b 0x02
$room5bfBlockAnimation = Resolve-NpcAnimation 0x6b 0x03
$room5bfLeverAnimation = Resolve-NpcAnimation 0x61 0x00
$room5bfConnectionAnimations = @(2..6 | ForEach-Object {
    Resolve-NpcAnimation 0x61 $_
})
$room5bfFlippersObject = $treasureObjectRecords['TREASURE_OBJECT_FLIPPERS_00']
if (-not $room5bfObjectBlock.Success -or
    $room5bfObjectBlock.Groups['flippersY'].Value -ne '1c' -or
    $room5bfObjectBlock.Groups['flippersX'].Value -ne 'b8' -or
    $room5bfObjectBlock.Groups['leftY'].Value -ne '38' -or
    $room5bfObjectBlock.Groups['leftX'].Value -ne 'b0' -or
    $room5bfObjectBlock.Groups['rightY'].Value -ne '38' -or
    $room5bfObjectBlock.Groups['rightX'].Value -ne 'c0' -or
    $room5bfObjectBlock.Groups['leverSubid'].Value -ne '30' -or
    $room5bfObjectBlock.Groups['leverY'].Value -ne '10' -or
    $room5bfObjectBlock.Groups['leverX'].Value -ne '78') {
    throw 'Room 5:bf no longer has its original flippers, block, block, lever object order.'
}
if ($room5bfMiscSource -notmatch
        '(?ms)^interaction6b_subid0a:.*?^interaction6b_subid0c:.*?ROOMFLAG_BIT_ITEM.*?sub \$0a.*?interaction6b_initGraphicsAndLoadScript.*?wDisabledObjects.*?wMenuDisabled.*?interactionAnimateAsNpc' -or
    $room5bfMiscSource -notmatch
        '(?ms)^interaction6b_subid0d:.*?PALH_a3.*?ld a,\$06\s+call objectSetCollideRadius.*?cp \$c0.*?Interaction\.var03.*?Interaction\.var3d.*?wLever1PullDistance.*?and \$7c\s+rrca\s+rrca.*?cp \$fe\s+call nc,@checkLinkSquished.*?ld a,\$08\s+ld bc,\$38b8.*?LINK_STATE_SQUISHED.*?interactionAnimateAsNpc' -or
    $room5bfLeverSource -notmatch
        '(?ms)^interactionCode61:.*?getFreeInteractionSlot.*?ld \(hl\),\$80.*?wLever1PullDistance.*?ld a,\$0c.*?@leverLengths:\s+\.db \$08 \$10 \$20 \$40.*?ld b,SPEED_40.*?SND_MOVEBLOCK.*?^@state3:.*?objectApplySpeed.*?SND_OPENCHEST.*?or \$80' -or
    $room5bfLeverSource -notmatch
        '(?ms)^@updateLeverConnectionObject:.*?add a\s+add a\s+add \(hl\).*?swap a\s+and \$07.*?add \$02\s+jp interactionSetAnimation.*?@animationYOffsets:\s+\.db \$00 \$08 \$10 \$18 \$20' -or
    $room5bfScriptSource -notmatch
        '(?ms)^interaction6b_subid0aScript:.*?setcollisionradii \$02, \$02.*?disableinput.*?writeobjectbyte Interaction\.substate, \$01.*?@flippers:\s+giveitem TREASURE_FLIPPERS, \$00\s+wait 30\s+scriptend' -or
    $paletteHeaderSource -notmatch
        '(?ms)PALH_a3.*?m_PaletteHeaderSpr\s+6,\s*1,\s*paletteData5958') {
    throw 'Room 5:bf flippers, sliding-block, lever, connection, or PALH_a3 behavior changed.'
}
if ($null -eq $room5bfFlippersObject -or
    $room5bfFlippersObject.Treasure -ne 0x2e -or
    $room5bfFlippersObject.Subid -ne 0x00 -or
    $room5bfFlippersObject.Parameter -ne 0x00 -or
    $room5bfFlippersObject.Graphic -ne 0x31 -or
    $null -eq $room5bfFlippersGraphic -or
    $room5bfFlippersGraphic.Gfx -ne 0x79 -or
    $room5bfFlippersGraphic.TileBase -ne 0x04 -or
    $room5bfFlippersGraphic.Palette -ne 0x05 -or
    $room5bfFlippersGraphic.DefaultAnimation -ne 0x02 -or
    $null -eq $room5bfBlockGraphic -or
    $room5bfBlockGraphic.Gfx -ne 0x00 -or
    $room5bfBlockGraphic.TileBase -ne 0x36 -or
    $room5bfBlockGraphic.Palette -ne 0x06 -or
    $room5bfBlockGraphic.DefaultAnimation -ne 0x03 -or
    $null -eq $room5bfLeverGraphic -or
    $room5bfLeverGraphic.Gfx -ne 0x72 -or
    $room5bfLeverGraphic.TileBase -ne 0x0a -or
    $room5bfLeverGraphic.Palette -ne 0x03 -or
    $room5bfLeverGraphic.DefaultAnimation -ne 0x00 -or
    $gfxNames[0x79] -ne 'spr_quest_items_5' -or
    $gfxNames[0x72] -ne 'spr_dungeon_sprites' -or
    -not $room5bfFlippersAnimation -or
    -not $room5bfBlockAnimation -or
    -not $room5bfLeverAnimation -or
    @($room5bfConnectionAnimations | Where-Object { -not $_ }).Count -ne 0 -or
    $soundIds['SND_MOVEBLOCK'] -ne 0x71 -or
    $soundIds['SND_OPENCHEST'] -ne 0x6c) {
    throw 'Could not resolve room 5:bf visuals, flippers treasure $2e:$00, or lever sounds.'
}
[void]$npcSpriteNames.Add('spr_quest_items_5')
[void]$npcSpriteNames.Add('spr_dungeon_sprites')
[void]$npcSpriteNames.Add('spr_common_sprites')
$room5bfSource = 'mainData.s:group5MapbfObjectData'
$room5bfRows = @(
    '# order`tkind`tid`tsubid`ty`tx`tvar03`tsprite`ttile-base`tpalette`tanimation-index`tanimation`tsource',
    "0`tflippers`t6b`t0c`t1c`tb8`t02`tspr_quest_items_5`t04`t05`t02`t$room5bfFlippersAnimation`t$room5bfSource",
    "1`tsliding-block`t6b`t0d`t38`tb0`t00`tspr_common_sprites`t36`t06`t03`t$room5bfBlockAnimation`t$room5bfSource",
    "2`tsliding-block`t6b`t0d`t38`tc0`t01`tspr_common_sprites`t36`t06`t03`t$room5bfBlockAnimation`t$room5bfSource",
    "3`tlever`t61`t30`t10`t78`t00`tspr_dungeon_sprites`t0a`t03`t00`t$room5bfLeverAnimation`t$room5bfSource",
    "4`tlever-connection`t61`t80`t10`t78`t00`tspr_dungeon_sprites`t0a`t03`t02`t$($room5bfConnectionAnimations -join '^')`tlever.s:interactionCode61/@updateLeverConnectionObject"
)
$room5bfConstantRows = @(
    '# group`troom`titem-room-flag`ttreasure-id`ttreasure-subid`ttreasure-parameter`tlever-length`tpull-speed`tlever-radius-y`tlever-radius-x`tlink-y-offset`tblock-radius`tdistance-mask`tdistance-shift`tsquish-y`tsquish-x`tsquish-range`tconnection-step`tpost-grant-wait`tcollision-radius-y`tcollision-radius-x`tpickup-distance`tmove-sound`tfull-sound`tsource',
    "5`tbf`t20`t2e`t00`t00`t40`t0a`t05`t01`t0c`t06`t7c`t02`t38`tb8`t08`t10`t30`t02`t02`t0e`t71`t6c`tmiscellaneous1.s:interaction6b_subid0a/0d;lever.s:interactionCode61"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\room5bf_interactions.tsv'),
    $room5bfRows)
Write-GeneratedTable(
    (Join-Path $destination 'objects\room5bf_constants.tsv'),
    $room5bfConstantRows)
Export-PaletteBlock 'paletteData5958' 4 'objects\room5bf_block_palette.bin'

# PART_DARK_ROOM_HANDLER $08 scans the complete 16-byte-stride large-room
# layout and creates a permanent PART_LIGHTABLE_TORCH $06 for every unlit
# torch metatile. INTERAC_MISCELLANEOUS_2 $dc:$00 in room 5:ed precedes that
# handler and creates the falling Graveyard Key when exactly two torches are
# lit, unless ROOMFLAG_ITEM already records its collection. Export these
# placements together so the runtime can retain their source order.
$darkRoomHandlerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\darkRoomHandler.s')
$lightableTorchSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'objects\dark_room_interactions.tsv'),
    $darkRoomRows)
Write-GeneratedTable(
    (Join-Path $destination 'objects\dark_room_constants.tsv'),
    $darkRoomConstantRows)

# Present room 0:45 and interior 3:fb form Troy's house pair. The exterior
# boy exists only in getGameProgress_1 state $03. Inside, Troy's first talk
# falls through TX_2c11 into TX_2c12, whose \call($ff) is selected from
# TX_2c13-$2c22 with one shared-RNG call. Closing that first textbox sets
# current-room flag $40; later talks use TX_2c12 directly.
$room045ObjectBlock = [regex]::Match(
    $mainObjectSource,
    '(?ms)^group0Map45ObjectData:\s*(?<body>.*?)(?=^group[0-7]Map[0-9a-f]{2}ObjectData:|\z)')
$room3fbObjectBlock = [regex]::Match(
    $mainObjectSource,
    '(?ms)^group3MapfbObjectData:\s*(?<body>.*?)(?=^group[0-7]Map[0-9a-f]{2}ObjectData:|\z)')
$room045BoySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\boy2.s')
$troyInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\troy.s')
$troyScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
if (-not $room045ObjectBlock.Success -or
    ([regex]::Matches($room045ObjectBlock.Groups['body'].Value, 'obj_Interaction')).Count -ne 1 -or
    $room045ObjectBlock.Groups['body'].Value -notmatch '(?m)^\s*obj_Interaction \$3f \$01 \$58 \$48\s*$' -or
    -not $room3fbObjectBlock.Success -or
    ([regex]::Matches($room3fbObjectBlock.Groups['body'].Value, 'obj_Interaction')).Count -ne 1 -or
    $room3fbObjectBlock.Groups['body'].Value -notmatch '(?m)^\s*obj_Interaction \$ca \$01 \$38 \$28\s*$' -or
    $room045BoySource -notmatch '(?ms)^@subid1:.*?getGameProgress_1\s+ld a,b\s+cp \$03\s+jp nz,interactionDelete.*?@initializeGraphicsAndScript' -or
    $troyInteractionSource -notmatch '(?ms)^@subid1:.*?checkInteractionState\s+jr nz,@state1.*?jp @initialize.*?^@scriptTable:\s+\.dw mainScripts\.troySubid0Script\s+\.dw mainScripts\.troySubid1Script' -or
    $troyScriptSource -notmatch '(?ms)^troy_chooseRandomAnimalText:\s+call getRandomNumber\s+and \$0f\s+add <TX_2c13\s+ld \(wTextSubstitutions\),a\s+ret' -or
    $troyScriptSource -notmatch '(?ms)^troySubid1Script:\s+jumpifglobalflagset GLOBALFLAG_FINISHEDGAME, mainScripts\.stubScript\s+initcollisions\s+@loop:\s+checkabutton\s+jumpifroomflagset \$40, \+\+\s+asm15 troy_chooseRandomAnimalText\s+showtext TX_2c11\s+orroomflag \$40\s+scriptjump @loop\s+\+\+\s+asm15 troy_chooseRandomAnimalText\s+showtext TX_2c12\s+scriptjump @loop') {
    throw 'Rooms 0:45/3:fb or Troy $ca:$01 behavior changed in the disassembly.'
}
$troyFirstTextId = 0x2c11
$troyRepeatTextId = 0x2c12
$troyAnimalTextIds = @(0x2c13..0x2c22)
foreach ($textId in @($troyFirstTextId, $troyRepeatTextId) + $troyAnimalTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Troy house text TX_$($textId.ToString('x4'))."
    }
}
if ($allTexts[$troyRepeatTextId] -notmatch '\\call\(0xff\)') {
    throw 'Troy house repeat text TX_2c12 no longer calls wTextSubstitutions through $ff.'
}
$troyHouseRows = [Collections.Generic.List[string]]::new()
$troyHouseRows.Add(
    "# group`troom`tid`tsubid`troom-flag`trandom-mask`tchoice`tfirst-text-id`trepeat-text-id`tanimal-text-id`tsource`tfirst-utf8-base64`trepeat-utf8-base64`tanimal-utf8-base64")
for ($choice = 0; $choice -lt $troyAnimalTextIds.Count; $choice++) {
    $animalTextId = $troyAnimalTextIds[$choice]
    $troyHouseRows.Add(
        "3`tfb`tca`t01`t40`t0f`t$($choice.ToString('x2'))`t$($troyFirstTextId.ToString('x4'))`t$($troyRepeatTextId.ToString('x4'))`t$($animalTextId.ToString('x4'))`tmainData.s:group3MapfbObjectData;troy.s:@subid1;scriptHelper.s:troySubid1Script`t" +
        "$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$troyFirstTextId])))`t" +
        "$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$troyRepeatTextId])))`t" +
        "$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$animalTextId])))")
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\troy_house.tsv'),
    $troyHouseRows)

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
        $npcVisibilitySources[$file] = Read-ImportText $path
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
    [int]$mask, [bool]$expectedSet, [string]$source,
    [string]$sourceToken = 'getThisRoomFlags'
) {
    Confirm-NpcVisibilitySource $source $sourceToken
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
Add-NpcGameProgress1Visibility 0x3f 0x01 -1 0 0x03 $true 'boy2.s:@subid1'
Add-NpcCurrentRoomVisibility 0x3f 0x02 -1 0 0x40 $false 'boy2.s:@@state0'
Add-NpcGlobalVisibility 0xca 0x01 -1 0 'GLOBALFLAG_FINISHEDGAME' $false 'scriptHelper.s:troySubid1Script'

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

# Room 0:5d's Ghini is secret index `$01: linked files only, after D1.
Add-NpcLinkedVisibility 0xcb 0x00 -1 0 $true 'scriptHelper.s:linkedNpc_checkShouldSpawn'
Add-NpcEssenceVisibility 0xcb 0x00 -1 0 0x01 $true 'scriptHelper.s:@checkd1' '@checkd1'

# Room 0:83's Great Fairy is secret index `$06: linked files only, after D2.
Add-NpcLinkedVisibility 0xd5 0x00 -1 0 $true 'scriptHelper.s:linkedNpc_checkShouldSpawn'
Add-NpcEssenceVisibility 0xd5 0x00 -1 0 0x02 $true 'scriptHelper.s:@checkd2' '@checkd2'

# Lynna City's paired villager placements use the original
# checkNpcShouldExistAtGameStage table. Import every phase listed for each
# subid, including the three actors placed together in room 0:68.
$npcStageSelectionSource = $npcVisibilitySources['miscMan2.s']
foreach ($expectedTable in @(
    '(?ms)^@data0:.*?^@@subid1:\s*\r?\n\s*\.db \$00 \$01 \$02 \$ff\s*\r?\n^@@subid2:\s*\r?\n\s*\.db \$03 \$04 \$05 \$ff',
    '(?ms)^@data3:.*?^@@subid4:\s*\r?\n\s*\.db \$00 \$01 \$05 \$ff\s*\r?\n^@@subid5:\s*\r?\n\s*\.db \$04 \$ff',
    '(?ms)^@data5:.*?^@@subid1:\s*\r?\n\s*\.db \$01 \$02 \$ff\s*\r?\n^@@subid2:\s*\r?\n\s*\.db \$03 \$04 \$07 \$ff',
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
Add-NpcGameProgress2SetVisibility 0x43 0x01 -1 @(1, 2) 'pastGuy.s:@subid1'
Add-NpcGameProgress2SetVisibility 0x43 0x02 -1 @(3, 4, 7) 'pastGuy.s:@subid2'

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
Add-NpcGlobalVisibility 0x4f 0x00 0x02 0 'GLOBALFLAG_GOT_RING_FROM_ZELDA' $false 'impaNpc.s:getImpaNpcState'
Add-NpcEssenceVisibility 0x4f 0x00 0x02 0 0x04 $false 'impaNpc.s:getImpaNpcState'
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

# postmanScript jumps to stubScript before initializing collisions when the
# Stationery room-item flag is already set.
Add-NpcCurrentRoomVisibility 0x55 0x00 -1 0 0x20 $false `
    'scriptHelper.s:postmanScript' 'jumpifroomflagset $20'

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

# Stone rabbits exist only after D7/Jabu and before room 4:fc records Veran's
# defeat. Their native state is static push/priority behavior with no script.
Add-NpcEssenceVisibility 0x4b 0x06 -1 0 0x40 $true 'rabbitMain.s:@initSubid6' 'wEssencesObtained'
Add-NpcSpecificRoomVisibility 0x4b 0x06 -1 0 4 0xfc 0x80 $false 'rabbitMain.s:@initSubid6' 'wGroup4RoomFlags+$fc'

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

if ($npcVisibilityRows.Count -ne 345) {
    throw "Expected 344 imported NPC visibility predicates, got $($npcVisibilityRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\npc_visibility.tsv'),
    $npcVisibilityRows)

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
    $actorSource = Read-ImportText (
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
$nayruInitialSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')
$ralphInitialSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\ralph.s')
$boyInitialSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\boy.s')
$monkeyInitialSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_intro_actors.tsv'),
    $nayruActorRows)

# The three visions after TX_5607 are not ordinary loads of these rooms. The
# singing handler indexes objectTable2, runs those interactions until one writes
# $ff to cfdf, and only then advances to the next room. Export the room order,
# exact interaction lifetime, and the ten-entry monkey initializer table used by
# objectData7717 instead of duplicating them in the runtime.
$nayruObjectData2Source = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData3.s')
$nayruMiscInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
$nayruFemaleVillagerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\femaleVillager.s')
$nayruOldLadySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\oldLady.s')
$nayruVignetteCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$nayruVignetteMonkeySource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_intro_vignettes.tsv'),
    $nayruVignetteRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_intro_vignette_monkeys.tsv'),
    $nayruMonkeyRows)

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
$floatingImageSource = Read-ImportText (
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
$nayruPartAnimationSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partAnimations.s')
$nayruPartOamSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_intro_effects.tsv'),
    $nayruEffectRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_intro_text.tsv'),
    $nayruTextRows)

$nayruMiscSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
$nayruObjectsSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData3.s')
$nayruBearSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\bear.s')
$nayruCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$nayruScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$nayruScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$nayruBirdSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\bird.s')
$nayruRabbitSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\rabbitMain.s')
$nayruMonkeySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\monkeyMain.s')
$nayruGhostSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\ghostVeran.s')
if ($nayruMiscSource -notmatch '(?ms)^interaction6b_subid01:.*?GLOBALFLAG_INTRO_DONE.*?objectData\.nayruAndAnimalsInIntro' -or
    $nayruObjectsSource -notmatch '(?ms)^nayruAndAnimalsInIntro:.*?obj_Interaction \$36 \$00 \$18 \$78.*?obj_Interaction \$4c \$00 \$2c \$48.*?obj_End' -or
    $nayruBearSource -notmatch '(?ms)cp \$60.*?cp \$3e.*?mainScripts\.bearSubid00Script_part2' -or
    $nayruCutsceneSource -notmatch '(?ms)^nayruSingingCutsceneHandler:.*?ld \(hl\),\$58\s+inc hl\s+ld \(hl\),\$02.*?paletteData44a8.*?ld \(hl\),\$3c\s+jp fadeoutToWhite.*?ld a,\$15.*?ld a,\$03\s+jp fadeinFromWhiteWithDelay' -or
    $nayruScriptSource -notmatch '(?ms)^bearSubid00Script_part1:.*?@moveDown:.*?setangle \$00\s+setspeed SPEED_080\s+applyspeed \$20' -or
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
    '# group`troom`tintro-flag`tcompletion-room-flag`tbear-room-flag`ttrigger-x`ttrigger-y`tbear-delay`tbear-move-speed`tpost-bear-text`tsinging-frames`tskip-window`tsprite-scroll-period`tsprite-scroll-steps`tpossession-fade-hold`tportal-position`tportal-tile`tvignette-count`tnpc-jump-speed-z`tnpc-jump-gravity`tdark-fade-frames`twhite-fade-out-frames`twhite-fade-in-frames`tnayru-ascent-speed-z`tnayru-transfer-z`tnayru-landing-delay`tnayru-fall-speed-z`tnayru-fall-gravity',
    "0`t39`t0a`t40`t80`t96`t62`t120`t14`t30`t600`t240`t8`t40`t60`t22`td7`t3`t-512`t48`t32`t32`t97`t-1024`t-32768`t30`t64`t32"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_intro_event.tsv'),
    $nayruEventRows)

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
    '# actor`tdelay`tangle`tspeed-raw`twait-jump-speed-z`twait-gravity`trepeat-wait-jump`tescape-jump-speed-z`tescape-gravity`trepeat-escape-jump`twait-for-landing`twait-animation`tescape-animation',
    "Bear`t40`t2`t28`t0`t0`t0`t0`t0`t0`t0`t2`t1",
    "Monkey`t50`t2`t3c`t-288`t32`t1`t-256`t32`t1`t0`t3`t4",
    "Rabbit`t40`t6`t3c`t0`t0`t0`t-512`t32`t1`t0`t2`t4",
    "Boy`t0`t2`t3c`t-384`t32`t0`t0`t0`t0`t1`t2`t0",
    "Bird`t30`t1`t28`t-192`t32`t1`t-256`t0`t0`t0`t2`t3"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_intro_flee.tsv'),
    $nayruFleeRows)

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
$agesBank3f = Read-ImportText (Join-Path $Disassembly 'ages.s')
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_singing_oam.tsv'),
    $nayruOamRows)

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
Write-GeneratedTable($npcPath, $npcRows)
$tokayInteractionConstantsPath = Join-Path $destination "objects\tokay_interaction_constants.tsv"
Write-GeneratedTable($tokayInteractionConstantsPath, $tokayInteractionConstantRows)
$tokayShopConstantsPath = Join-Path $destination "objects\tokay_shop_constants.tsv"
Write-GeneratedTable($tokayShopConstantsPath, $tokayShopConstantRows)
$wildTokayConstantsPath = Join-Path $destination "objects\wild_tokay_constants.tsv"
Write-GeneratedTable($wildTokayConstantsPath, $wildTokayConstantRows)
$wildTokayMeatConstantsPath = Join-Path $destination "objects\wild_tokay_meat_constants.tsv"
Write-GeneratedTable($wildTokayMeatConstantsPath, $wildTokayMeatConstantRows)
$tokayTextPath = Join-Path $destination "objects\tokay_interaction_texts.tsv"
Write-GeneratedTable($tokayTextPath, $tokayTextRows)
$tokayAnimationPath = Join-Path $destination "objects\tokay_interaction_animations.tsv"
Write-GeneratedTable($tokayAnimationPath, $tokayAnimationRows)
$tokayEntranceEyePath = Join-Path $destination "objects\tokay_entrance_eyes.tsv"
Write-GeneratedTable($tokayEntranceEyePath, $tokayEntranceEyeRows)
$tokaySeedlingPlotPath = Join-Path $destination "objects\tokay_seedling_plot.tsv"
Write-GeneratedTable($tokaySeedlingPlotPath, $tokaySeedlingPlotRows)
$tokayEyeballSlotPath = Join-Path $destination "objects\tokay_eyeball_slot.tsv"
Write-GeneratedTable($tokayEyeballSlotPath, $tokayEyeballSlotRows)
$tokayHolderPath = Join-Path $destination "objects\tokay_item_holders.tsv"
Write-GeneratedTable($tokayHolderPath, $tokayHolderRows)
$tokayShopPath = Join-Path $destination "objects\tokay_shop_items.tsv"
Write-GeneratedTable($tokayShopPath, $tokayShopRows)
$wildTokayPatternPath = Join-Path $destination "objects\wild_tokay_patterns.tsv"
Write-GeneratedTable($wildTokayPatternPath, $wildTokayPatternRows)
$wildTokayPrizePath = Join-Path $destination "objects\wild_tokay_prizes.tsv"
Write-GeneratedTable($wildTokayPrizePath, $wildTokayPrizeRows)
$wildTokayStartTilePath = Join-Path $destination "objects\wild_tokay_start_tiles.tsv"
Write-GeneratedTable($wildTokayStartTilePath, $wildTokayStartTileRows)
$wildTokayMeatAccessoryPath = Join-Path $destination "objects\wild_tokay_meat_accessory.tsv"
Write-GeneratedTable($wildTokayMeatAccessoryPath, $wildTokayMeatAccessoryRows)
$companionTutorialPath = Join-Path $destination "objects\companion_tutorials.tsv"
Write-GeneratedTable(
    $companionTutorialPath,
    $companionTutorialRows)
$companionBarrierPath = Join-Path $destination "objects\companion_barriers.tsv"
Write-GeneratedTable(
    $companionBarrierPath,
    $companionBarrierRows)
$tinglePath = Join-Path $destination "objects\tingle.tsv"
Write-GeneratedTable($tinglePath, $tingleRows)
$tingleAnimationPath = Join-Path $destination "objects\tingle_animations.tsv"
Write-GeneratedTable($tingleAnimationPath, $tingleAnimationRows)
$tingleTextPath = Join-Path $destination "objects\tingle_texts.tsv"
Write-GeneratedTable($tingleTextPath, $tingleTextRows)
$vasuShopTextPath = Join-Path $destination "objects\vasu_shop_texts.tsv"
Write-GeneratedTable(
    $vasuShopTextPath,
    $vasuShopTextRows)
$vasuShopAnimationPath = Join-Path $destination "objects\vasu_shop_animations.tsv"
Write-GeneratedTable(
    $vasuShopAnimationPath,
    $vasuShopAnimationRows)
$vasuShopConstantsPath = Join-Path $destination "objects\vasu_shop_constants.tsv"
Write-GeneratedTable(
    $vasuShopConstantsPath,
    $vasuShopConstantRows)
$lynnaShopItemPath = Join-Path $destination "objects\lynna_shop_items.tsv"
Write-GeneratedTable(
    $lynnaShopItemPath,
    $lynnaShopItemRows)
$lynnaShopTextPath = Join-Path $destination "objects\lynna_shop_texts.tsv"
Write-GeneratedTable(
    $lynnaShopTextPath,
    $lynnaShopTextRows)
$lynnaShopAnimationPath = Join-Path $destination "objects\lynna_shop_animations.tsv"
Write-GeneratedTable(
    $lynnaShopAnimationPath,
    $lynnaShopAnimationRows)
$lynnaShopConstantsPath = Join-Path $destination "objects\lynna_shop_constants.tsv"
Write-GeneratedTable(
    $lynnaShopConstantsPath,
    $lynnaShopConstantRows)
$businessScrubOfferPath = Join-Path $destination "objects\business_scrub.tsv"
Write-GeneratedTable(
    $businessScrubOfferPath,
    $businessScrubOfferRows)
$businessScrubConstantsPath = Join-Path $destination "objects\business_scrub_constants.tsv"
Write-GeneratedTable(
    $businessScrubConstantsPath,
    $businessScrubConstantRows)
$businessScrubAnimationPath = Join-Path $destination "objects\business_scrub_animations.tsv"
Write-GeneratedTable(
    $businessScrubAnimationPath,
    $businessScrubAnimationRows)
$businessScrubTextPath = Join-Path $destination "objects\business_scrub_texts.tsv"
Write-GeneratedTable(
    $businessScrubTextPath,
    $businessScrubTextRows)
$dungeonMechanicPath = Join-Path $destination "objects\dungeon_mechanics.tsv"
Write-GeneratedTable(
    $dungeonMechanicPath,
    $dungeonMechanicRows)
$dungeonEventTilePatternPath = Join-Path $destination "objects\dungeon_event_tile_patterns.tsv"
Write-GeneratedTable(
    $dungeonEventTilePatternPath,
    $dungeonEventTilePatternRows)
$dungeonMechanicConstantsPath = Join-Path $destination "objects\dungeon_mechanic_constants.tsv"
Write-GeneratedTable(
    $dungeonMechanicConstantsPath,
    $dungeonMechanicConstantRows)
$dungeonMechanicTextPath = Join-Path $destination "objects\dungeon_mechanic_text.tsv"
Write-GeneratedTable(
    $dungeonMechanicTextPath,
    $dungeonMechanicTextRows)
$puzzlePuffPath = Join-Path $destination "effects\puzzle_puff.tsv"
Write-GeneratedTable(
    $puzzlePuffPath,
    $puzzlePuffRows)
$grassDebrisPath = Join-Path $destination "effects\grass_debris.tsv"
Write-GeneratedTable(
    $grassDebrisPath,
    $grassDebrisRows)
$rockDebrisPath = Join-Path $destination "effects\rock_debris.tsv"
Write-GeneratedTable(
    $rockDebrisPath,
    $rockDebrisRows)
$fallDownHolePath = Join-Path $destination "effects\fall_down_hole.tsv"
Write-GeneratedTable(
    $fallDownHolePath,
    $fallDownHoleRows)
$eraInfoPath = Join-Path $destination "effects\era_info.tsv"
Write-GeneratedTable(
    $eraInfoPath,
    $eraInfoRows)
$keyDoorPath = Join-Path $destination "objects\dungeon_key_doors.tsv"
Write-GeneratedTable(
    $keyDoorPath,
    $keyDoorRows)
$keyBlockPath = Join-Path $destination "objects\dungeon_key_blocks.tsv"
Write-GeneratedTable(
    $keyBlockPath,
    $keyBlockRows)
$overworldKeyholePath = Join-Path $destination "objects\overworld_keyholes.tsv"
Write-GeneratedTable(
    $overworldKeyholePath,
    $overworldKeyholeRows)
$overworldKeyholeTilePath = Join-Path $destination "metadata\overworld_keyhole_tiles.tsv"
Write-GeneratedTable(
    $overworldKeyholeTilePath,
    $overworldKeyholeTileRows)
$overworldKeyholeConstantPath = Join-Path $destination "objects\overworld_keyhole_constants.tsv"
Write-GeneratedTable(
    $overworldKeyholeConstantPath,
    $overworldKeyholeConstantRows)
$standardTilePath = Join-Path $destination "metadata\standard_tile_substitutions.tsv"
Write-GeneratedTable(
    $standardTilePath,
    $standardTileRows)
$treasureObjectVisualPath = Join-Path $destination "metadata\treasure_object_visuals.tsv"
Write-GeneratedTable(
    $treasureObjectVisualPath,
    $treasureObjectVisualRows)
$familyNpcPath = Join-Path $destination "objects\bipin_blossom_family.tsv"
Write-GeneratedTable(
    $familyNpcPath,
    $familyRows)
$familyTextPath = Join-Path $destination "objects\bipin_blossom_family_texts.tsv"
Write-GeneratedTable(
    $familyTextPath,
    $familyTextRows)
$runningBipinPath = Join-Path $destination "objects\running_bipin.tsv"
Write-GeneratedTable(
    $runningBipinPath,
    $runningBipinRows)
$room148PickaxePath = Join-Path $destination "objects\room148_pickaxe.tsv"
Write-GeneratedTable(
    $room148PickaxePath,
    $room148PickaxeRows)
$dungeonEntryPath = Join-Path $destination "objects\dungeon_entry_data.tsv"
Write-GeneratedTable(
    $dungeonEntryPath,
    $dungeonEntryRows)
$dungeonSharedPlacementPath = Join-Path $destination "objects\dungeon_shared_placements.tsv"
Write-GeneratedTable(
    $dungeonSharedPlacementPath,
    $dungeonSharedPlacementRows)
$dungeonSharedVisualPath = Join-Path $destination "objects\dungeon_shared_visuals.tsv"
Write-GeneratedTable(
    $dungeonSharedVisualPath,
    $dungeonSharedVisualRows)
$dungeonSharedConstantPath = Join-Path $destination "objects\dungeon_shared_constants.tsv"
Write-GeneratedTable(
    $dungeonSharedConstantPath,
    $dungeonSharedConstantRows)
$minibossPortalPairPath = Join-Path $destination "objects\miniboss_portal_pairs.tsv"
Write-GeneratedTable(
    $minibossPortalPairPath,
    $minibossPortalPairRows)
$blackTowerTextPath = Join-Path $destination "objects\black_tower_texts.tsv"
Write-GeneratedTable(
    $blackTowerTextPath,
    $blackTowerTextRows)
$blackTowerSelectorPath = Join-Path $destination "objects\black_tower_selectors.tsv"
Write-GeneratedTable(
    $blackTowerSelectorPath,
    $blackTowerSelectorRows)
$blackTowerVisualPath = Join-Path $destination "objects\black_tower_visuals.tsv"
Write-GeneratedTable(
    $blackTowerVisualPath,
    $blackTowerVisualRows)
$blackTowerPatrolPath = Join-Path $destination "objects\black_tower_patrols.tsv"
Write-GeneratedTable(
    $blackTowerPatrolPath,
    $blackTowerPatrolRows)
$blackTowerConstantsPath = Join-Path $destination "objects\black_tower_constants.tsv"
Write-GeneratedTable(
    $blackTowerConstantsPath,
    $blackTowerConstantsRows)
$room149VisualPath = Join-Path $destination "objects\room149_family_visuals.tsv"
Write-GeneratedTable(
    $room149VisualPath,
    $room149VisualRows)
$room149TextPath = Join-Path $destination "objects\room149_family_texts.tsv"
Write-GeneratedTable(
    $room149TextPath,
    $room149TextRows)
