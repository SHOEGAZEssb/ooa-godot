function Read-InteractionPresentation([string]$game) {
$interactionAnimationPath =
    Join-Path $Disassembly "data\$game\interactionAnimations.s"
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
    Join-Path $Disassembly "data\$game\interactionOamData.s"
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

    return @{ Tables=$npcAnimationTables; OamTables=$npcOamPointerTables; Starts=$npcOamPointerStarts; Pointers=$npcOamPointers; Definitions=$npcAnimationDefinitions; Blocks=$npcOamBlocks }
}

function Format-NpcAnimationFrame($frame, [string]$oam) {
    $metadata = "$($frame.Duration)"
    if ([int]$frame.Parameter -ne 0) { $metadata += ",$($frame.Parameter)" }
    return "$metadata@$oam"
}

function Join-NpcAnimationFrames($frames, [int]$loopStart) {
    $encoded = $frames -join '|'
    if ($loopStart -gt 0) { $encoded += "~$loopStart" }
    return $encoded
}

function Resolve-InteractionPresentation($data, [int]$interactionId, [int]$animationIndex, [int]$animationTableId = -1) {
    $hex = $interactionId.ToString('x2')
    $tableHex = if ($animationTableId -lt 0) { $hex } else { $animationTableId.ToString('x2') }
    $animationKey = "interaction${tableHex}Animations"
    $pointerKey = "interaction${hex}OamDataPointers"
    if (-not $data.Tables.ContainsKey($animationKey) -or
        -not $data.Starts.ContainsKey($pointerKey)) { return '' }
    $animations = $data.Tables[$animationKey]
    if ($animationIndex -lt 0 -or $animationIndex -ge $animations.Count) { return '' }
    $animationLabel = $animations[$animationIndex]
    if (-not $data.Definitions.ContainsKey($animationLabel)) { return '' }
    $definition = $data.Definitions[$animationLabel]
    $pointerStart = $data.Starts[$pointerKey]
    $resolvedFrames = [Collections.Generic.List[string]]::new()
    foreach ($frame in $definition.Frames) {
        $pointerIndex = [int]($frame.PointerOffset / 2)
        $absolutePointerIndex = $pointerStart + $pointerIndex
        if ($pointerIndex -lt 0 -or
            $absolutePointerIndex -ge $data.Pointers.Count) { continue }
        $oamLabel = $data.Pointers[$absolutePointerIndex]
        $oam = if ($data.Blocks.ContainsKey($oamLabel)) { $data.Blocks[$oamLabel] } else { '' }
        $resolvedFrames.Add((Format-NpcAnimationFrame $frame $oam))
    }
    return Join-NpcAnimationFrames $resolvedFrames $definition.LoopStart
}
