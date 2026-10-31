# ITEM$0d locomotion is common; each native pack owns target identities and graphics.
$braceletParentSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/itemParents/bombsBraceletParent.s')
$objectSpeedsSource = Read-ImportText (Join-Path $Disassembly 'constants/common/objectSpeeds.s')
$throwSpeeds = @{}
$throwSpeedOffset = 0
foreach ($match in [regex]::Matches($objectSpeedsSource, '(?m)^\s*(SPEED_[0-9a-f]+)\s+dsb (\d+)')) {
    $throwSpeeds[$match.Groups[1].Value] = $throwSpeedOffset
    $throwSpeedOffset += [int]$match.Groups[2].Value
}
foreach ($alias in Read-AssemblyConstants (Join-Path $Disassembly 'constants/common/objectSpeeds.s')) {
    if ($throwSpeeds.ContainsKey($alias.OperandText)) { $throwSpeeds[$alias.Name] = $throwSpeeds[$alias.OperandText] }
}

function Convert-SignedLinkItemByte([int]$value) {
    if ($value -ge 128) { return $value - 256 }
    return $value
}
function Export-BombchuData($pack, $connection) {
    $destination = $pack.Root
    $path = Join-Path $Disassembly 'object_code/common/items/bombchus.s'
    $source = Read-ImportText $path
    $initial = [regex]::Match($source,
        '(?s)@tdState0:.*?Item.speedTmp\s+ld \(hl\),(?<speed>SPEED_[0-9a-f]+).*?Item.counter1\s+ld \(hl\),\$(?<wait>[0-9a-f]{2}).*?ld \(hl\),\$(?<fuse>[0-9a-f]{2}).*?ld a,\$(?<vision>[0-9a-f]{2}).*?Item.var31\s+ld \(hl\),\$(?<turn>[0-9a-f]{2})')
    $target = [regex]::Match($source,
        '(?s); Valid target established.*?Item.collisionRadiusY\s+ld a,\$(?<radius>[0-9a-f]{2}).*?Item.counter1\s+ld \(hl\),\$(?<wait>[0-9a-f]{2}).*?Item.speedTmp\s+ld \(hl\),(?<speed>SPEED_[0-9a-f]+)')
    $vision = [regex]::Match($source,
        '(?s)@incVisionRadius:.*?add \$(?<step>[0-9a-f]{2})\s+cp \$(?<limit>[0-9a-f]{2}).*?ld a,\$(?<reset>[0-9a-f]{2})')
    if (-not $initial.Success -or -not $target.Success -or -not $vision.Success -or
        -not $throwSpeeds.ContainsKey($initial.Groups['speed'].Value) -or
        -not $throwSpeeds.ContainsKey($target.Groups['speed'].Value)) {
        throw 'bombchus.s: unresolved initialization, target or vision constants.'
    }
    $constants = [ordered]@{}
    foreach ($name in @('wait','fuse','vision','turn')) {
        $constants["initial-$name"] = [Convert]::ToInt32($initial.Groups[$name].Value,16)
    }
    $constants['search-speed'] = $throwSpeeds[$initial.Groups['speed'].Value]
    foreach ($name in @('radius','wait')) {
        $constants["target-$name"] = [Convert]::ToInt32($target.Groups[$name].Value,16)
    }
    $constants['target-speed'] = $throwSpeeds[$target.Groups['speed'].Value]
    foreach ($name in @('step','limit','reset')) {
        $constants["vision-$name"] = [Convert]::ToInt32($vision.Groups[$name].Value,16)
    }
    foreach ($entry in @(
        @('topdown-gravity','ld c,\$(?<value>[0-9a-f]{2})\s+call itemUpdateSpeedZAndCheckHazards'),
        @('sidescroll-gravity','ld c,\$(?<value>[0-9a-f]{2})\s+call itemUpdateThrowingVerticallyAndCheckHazards'),
        @('motion-gravity','ld c,\$(?<value>[0-9a-f]{2})\s+call objectUpdateSpeedZ_paramC'),
        @('chase-counter','ld \(hl\),\$(?<value>[0-9a-f]{2})\s+; Increment state\s+ld l,e'),
        @('homing-frame-mask','ld a,\(wFrameCounter\)\s+and \$(?<value>[0-9a-f]{2})\s+call z,bombchuUpdateAngle_topDown'))) {
        $match = [regex]::Match($source,$entry[1])
        if (-not $match.Success) { throw "bombchus.s: missing $($entry[0])." }
        $constants[$entry[0]] = [Convert]::ToInt32($match.Groups['value'].Value,16)
    }
    if ($source -notmatch '(?s)Item.speedZ\s+ld a,\$80\s+ldi \(hl\),a\s+ld \(hl\),\$ff' -or
        $braceletParentSource -notmatch '(?s)parentItemCode_bombchu:.*?isLinkUnderwater.*?SPECIALOBJECT_RAFT.*?wLinkSwimmingState.*?wNumBombchus.*?parentItemLoadAnimationAndIncState\s+;[^\r\n]*\s+ld e,\$01\s+jp itemCreateChildAndDeleteOnFailure') {
        throw 'ITEM$0d source jump velocity or ordered parent gates/one-child limit changed.'
    }
    $constants['wall-jump-speed-z'] = -128
    $constants['child-limit'] = 1
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# name`tvalue`tsource")
    foreach ($entry in $constants.GetEnumerator()) {
        $rows.Add("$($entry.Key)`t$($entry.Value)`tobject_code/common/items/bombchus.s;object_code/common/itemParents/bombsBraceletParent.s:parentItemCode_bombchu")
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/bombchu_mechanics.tsv'), $rows)

    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# kind`tdirection`ty`tx`tsource")
    foreach ($kind in @('front','normal','sidescroll')) {
        $label = @{front='@offsets';normal='@normalOffsets';sidescroll='@sidescrollOffsets'}[$kind]
        $offsets = @(Read-AssemblyLiteralValues $path $label)
        if ($offsets.Count -ne 8) { throw "bombchus.s:$label requires four ordered Y/X offsets." }
        for ($direction = 0; $direction -lt 4; $direction++) {
            $rows.Add("$kind`t$direction`t$(Convert-SignedLinkItemByte $offsets[$direction*2])`t$(Convert-SignedLinkItemByte $offsets[$direction*2+1])`tobject_code/common/items/bombchus.s:$label")
        }
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/bombchu_offsets.tsv'), $rows)
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# enemy-id`ttarget`tsource")
    $masks = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly "data/$($pack.Name)/bombchuTargets.s") 'bombchuTargets' 'dbrev')
    if ($masks.Count -ne 8) { throw 'bombchuTargets requires eight ordered 16-enemy rows.' }
    $compiledTargets = Read-MenuRomBytes $pack 'bombchuTargets' 16
    for ($index = 0; $index -lt $masks.Count; $index++) {
        $bits = ''
        foreach ($operand in $masks[$index].Operands) {
            if ($operand -notmatch '^%[01]{8}$') { throw "bombchuTargets: unsupported mask operand $operand." }
            $bits += $operand.Substring(1)
        }
        if ($bits.Length -ne 16) { throw 'bombchuTargets: each dbrev row must contain sixteen bits.' }
        for ($bit = 0; $bit -lt 16; $bit++) {
            if ((($compiledTargets[$index * 2 + [int][Math]::Floor($bit / 8)] -shr ($bit % 8)) -band 1) -ne [int]$bits.Substring($bit,1)) { throw "$($pack.Name): bombchuTargets source bit $index/$bit differs from clean US bytes." }
            $rows.Add("$(($index*16+$bit).ToString('x2'))`t$($bits.Substring($bit,1))`tdata/$($pack.Name)/bombchuTargets.s:bombchuTargets+$($index*2)")
        }
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/bombchu_targets.tsv'), $rows)

    $path = Join-Path $Disassembly 'data/itemAnimations.s'
    $animations = Read-NativeObjectPointerLabels $pack $path 'item0dAnimations' $connection
    $pointers = Read-NativeObjectPointerLabels $pack $path 'item0dOamDataPointers' $connection
    $gfx = Read-MenuRomBytes $pack 'itemData' 42
    $attributes = Read-MenuRomBytes $pack 'itemAttributes' 56
    $headers = Read-ImportText (Join-Path $Disassembly "data/$($pack.Name)/gfxHeaders.s")
    if ($animations.Count -ne 7 -or $pointers.Count -ne 17 -or $gfx[0x0d*3] -ne 0 -or
        $headers -notmatch '(?m)^\s*m_GfxHeader\s+spr_common_sprites,\s*\$8001') {
        throw "$($pack.Name): ITEM$0d requires seven animations, seventeen OAM pointers and resident common sprites."
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# animation`tsprite`ttile-base`toam-flags`tcollision`tradius-y`tradius-x`tdamage`thealth`tframes`tsource")
    for ($animation = 0; $animation -lt 7; $animation++) {
        $label = $animations[$animation]
        $ending = if ($animation -eq 6) { 'terminal' } else { 'loop' }
        $encoded = Read-NativeItemAnimation $pack $label $pointers $ending
        $expected = if ($animation -eq 6) { 7 } else { 2 }
        if (($encoded -split '\|').Count -ne $expected) { throw "$($pack.Name): ITEM$0d $label animation extent changed." }
        if ($encoded.EndsWith('~0')) { $encoded = $encoded.Substring(0,$encoded.Length - 2) }
        $radius = $attributes[0x0d*4+1]
        $rows.Add("$animation`tspr_common_sprites`t$($gfx[0x0d*3+1].ToString('x2'))`t$($gfx[0x0d*3+2].ToString('x2'))`t$($attributes[0x0d*4].ToString('x2'))`t$($radius -shr 4)`t$($radius -band 15)`t$($attributes[0x0d*4+2].ToString('x2'))`t$($attributes[0x0d*4+3].ToString('x2'))`t$encoded`tdata/itemAnimations.s:$label;data/$($pack.Name)/itemAttributes.s:ITEM_BOMBCHUS;data/$($pack.Name)/gfxHeaders.s:GFXH_COMMON_SPRITES")
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/bombchu_animations.tsv'), $rows)
}

foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try { Export-BombchuData $pack $connection }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
