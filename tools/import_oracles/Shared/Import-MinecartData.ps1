# Both builds use the common $16 interaction / $0a special-object handlers.
$minecartCommon = Read-ImportText (Join-Path $Disassembly 'object_code/common/specialObjects/commonCode.s')
if ($minecartCommon -notmatch '(?m)^\s*\.db \$60 \$0c ; 0x0a \(Minecart\)\s*$') {
    throw 'SPECIALOBJECT_MINECART native OAM variables changed.'
}
$minecartTiles = [ordered]@{
    'track-tl'='TILEINDEX_TRACK_TL'; 'track-br'='TILEINDEX_TRACK_BR';
    'track-bl'='TILEINDEX_TRACK_BL'; 'track-tr'='TILEINDEX_TRACK_TR';
    'track-horizontal'='TILEINDEX_TRACK_HORIZONTAL'; 'track-vertical'='TILEINDEX_TRACK_VERTICAL';
    'minecart-platform'='TILEINDEX_MINECART_PLATFORM'; 'minecart-door-up'='TILEINDEX_MINECART_DOOR_UP'
}
$minecartTileValues = @{}
$minecartTileNodes = @(Read-AssemblyConstants (Join-Path $Disassembly 'constants/common/tileIndices.s'))
foreach ($entry in $minecartTiles.GetEnumerator()) {
    $values = @($minecartTileNodes | Where-Object Name -eq $entry.Value)
    if ($values.Count -ne 1) { throw "Minecart requires common constant $($entry.Value)." }
    $minecartTileValues[$entry.Key] = Convert-AssemblyInteger $values[0].Operands[0]
}
foreach ($minecartPack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent) }
)) {
    $minecartBytes = $minecartPack.Bytes
    $minecartSymbols = $minecartPack.Symbols
    $minecartBehavior = [Collections.Generic.List[string]]::new()
    $minecartBehavior.Add('# name`tvalue`tsource')
    foreach ($entry in $minecartTiles.GetEnumerator()) {
        $minecartBehavior.Add("$($entry.Key)`t$($minecartTileValues[$entry.Key].ToString('x2'))`tconstants/common/tileIndices.s:$($entry.Value)")
    }
    foreach ($spec in @(
        @{ Name='minecart-speed'; Label='specialObjectCode_minecart@state0'; Low=0x10 },
        @{ Name='minecart-mount-push'; Label='interactionCode16@state0'; Low=0x46 }
    )) {
        if (-not $minecartSymbols.ContainsKey($spec.Label)) { throw "Missing native minecart $($spec.Label)." }
        $start = [int]$minecartSymbols[$spec.Label]; $found = @()
        if ($start -lt 0 -or $start + 63 -gt $minecartBytes.Length -or
            ($start -shr 14) -ne (($start + 62) -shr 14)) {
            throw "Minecart $($minecartPack.Name): initialization scan escaped its ROM bank."
        }
        for ($offset = 0; $offset -lt 60; $offset++) {
            if ($minecartBytes[$start+$offset] -eq 0x2e -and $minecartBytes[$start+$offset+1] -eq $spec.Low -and
                $minecartBytes[$start+$offset+2] -eq 0x36) { $found += $minecartBytes[$start+$offset+3] }
        }
        if ($found.Count -ne 1) { throw "Minecart $($minecartPack.Name): ambiguous native $($spec.Name) initialization." }
        $minecartBehavior.Add("$($spec.Name)`t$($found[0].ToString('x2'))`tobject_code/common:$($spec.Label)")
    }
    $minecartBehavior.Add("mounted-tile-base`t60`tobject_code/common/specialObjects/commonCode.s:specialObjectSetOamVariables[0a]")
    $minecartBehavior.Add("mounted-oam-flags`t0c`tobject_code/common/specialObjects/commonCode.s:specialObjectSetOamVariables[0a]")
    $staticCount = if ($minecartPack.Name -eq 'ages') { 8 } else { 16 }
    $minecartBehavior.Add("static-slots`t$($staticCount.ToString('x2'))`tinclude/wram.s:wStaticObjects;code/bank0.s:findFreeStaticObjectSlot")
    $attributeStart = [int]$minecartSymbols['itemAttributes'] + 0x1d * 4
    $gfxStart = [int]$minecartSymbols['itemData'] + 0x1d * 3
    if ($attributeStart -lt 0 -or $attributeStart + 4 -gt $minecartBytes.Length -or
        $gfxStart -lt 0 -or $gfxStart + 3 -gt $minecartBytes.Length -or
        ($minecartBytes[$attributeStart..($attributeStart+3)] -join ',') -ne '132,102,248,126' -or
        ($minecartBytes[$gfxStart..($gfxStart+2)] -join ',') -ne '0,0,0') {
        throw "Minecart $($minecartPack.Name): ITEM `$1d native attributes/graphics changed."
    }
    $attributeNames = @('collision-type','collision-radii','collision-damage','collision-health')
    for ($attribute = 0; $attribute -lt 4; $attribute++) {
        $minecartBehavior.Add("$($attributeNames[$attribute])`t$($minecartBytes[$attributeStart+$attribute].ToString('x2'))`tdata/$($minecartPack.Name)/itemAttributes.s:itemAttributes[1d]")
    }
    Write-GeneratedTable((Join-Path $minecartPack.Root 'objects/minecart_behavior.tsv'), $minecartBehavior)
    if (-not $minecartSymbols.ContainsKey('minecartCheckCollisions@trackData')) { throw 'Missing native minecart track table.' }
    $trackStart = [int]$minecartSymbols['minecartCheckCollisions@trackData']
    if ($trackStart -lt 0 -or $trackStart + 64 -gt $minecartBytes.Length -or
        ($trackStart -shr 14) -ne (($trackStart + 63) -shr 14)) { throw 'Minecart track table escaped its bank.' }
    $trackRows = [Collections.Generic.List[string]]::new()
    $trackRows.Add('# direction`torder`ttile`toffset`tlinked0`tlinked1`tlinked2`tsource')
    for ($direction = 0; $direction -lt 4; $direction++) {
        for ($order = 0; $order -lt 3; $order++) {
            $at = $trackStart + $direction * 16 + $order * 5
            if ($minecartBytes[$at] -eq 0) { throw 'Minecart track row terminated early.' }
            $encoded = @($minecartBytes[$at..($at + 4)] | ForEach-Object { $_.ToString('x2') }) -join "`t"
            $trackRows.Add("$direction`t$order`t$encoded`tobject_code/common/specialObjects/minecart.s:minecartCheckCollisions@trackData+$('{0:x2}' -f ($at-$trackStart))")
        }
        if ($minecartBytes[$trackStart + $direction * 16 + 15] -ne 0) { throw 'Minecart track list lost its native terminator.' }
    }
    Write-GeneratedTable((Join-Path $minecartPack.Root 'objects/minecart_tracks.tsv'), $trackRows)
    function Read-MinecartWord([int]$offset) {
        if ($offset -lt 0 -or $offset + 2 -gt $minecartBytes.Length -or
            ($offset -shr 14) -ne (($offset + 1) -shr 14)) {
            throw "Minecart $($minecartPack.Name): invalid compiled word offset $offset."
        }
        $word = [int]$minecartBytes[$offset] -bor ([int]$minecartBytes[$offset + 1] -shl 8)
        if ($word -lt 0x4000 -or $word -ge 0x8000) {
            throw "Minecart $($minecartPack.Name): expected banked ROM pointer at $offset."
        }
        return $word
    }
    $minecartRows = [Collections.Generic.List[string]]::new()
    $minecartRows.Add('# kind`tanimation`tframe`tduration`tgraphic`tparameter`tnext-pointer`toam-pointer`tsource')
    foreach ($spec in @(
        @{ Kind='waiting'; Table='interaction16Animations'; Oam='interaction16OamDataPointers'; Frames=1; Duration=0x7f },
        @{ Kind='mounted'; Table='specialObject0aAnimationDataPointers'; Oam='specialObject0aOamDataPointers'; Frames=2; Duration=6 }
    )) {
        if (-not $minecartSymbols.ContainsKey($spec.Table) -or -not $minecartSymbols.ContainsKey($spec.Oam)) {
            throw "Minecart $($minecartPack.Name): missing native $($spec.Kind) animation/OAM table."
        }
        $table = [int]$minecartSymbols[$spec.Table]
        $oamTable = [int]$minecartSymbols[$spec.Oam]
        for ($animation = 0; $animation -lt 2; $animation++) {
            $pointer = Read-MinecartWord ($table + $animation * 2)
            if ($spec.Kind -eq 'mounted' -and (Read-MinecartWord ($table + ($animation + 2) * 2)) -ne $pointer) {
                throw "Minecart $($minecartPack.Name): direction animation aliases changed."
            }
            $start = ($table -band -16384) + ($pointer -band 0x3fff)
            $length = $spec.Frames * 3 + 2
            if ($start + $length -gt $minecartBytes.Length -or
                ($start -shr 14) -ne (($start + $length - 1) -shr 14)) {
                throw "Minecart $($minecartPack.Name): animation stream escaped its bank."
            }
            for ($frame = 0; $frame -lt $spec.Frames; $frame++) {
                $at = $start + $frame * 3
                $graphic = $animation * 2 + $(if ($spec.Kind -eq 'waiting') { 0 } else { $frame })
                $parameter = if ($frame -eq 0) { 0 } else { 4 }
                if ($minecartBytes[$at] -ne $spec.Duration -or $minecartBytes[$at + 1] -ne $graphic -or
                    $minecartBytes[$at + 2] -ne $parameter) {
                    throw "Minecart $($minecartPack.Name): native $($spec.Kind) frame changed."
                }
                $oamIndex = if ($spec.Kind -eq 'waiting') { $graphic } else { 2 * $(if ($graphic -lt 2) { $graphic } else { 2 }) }
                $oam = Read-MinecartWord ($oamTable + $oamIndex)
                $next = (($at + 3) -band 0x3fff) -bor 0x4000
                $minecartRows.Add("$($spec.Kind)`t$animation`t$frame`t$($spec.Duration)`t$($graphic.ToString('x2'))`t$($parameter.ToString('x2'))`t$($next.ToString('x4'))`t$($oam.ToString('x4'))`tdata/$($minecartPack.Name):$($spec.Table)[$animation]+$($frame * 3)")
            }
            $loop = $start + $spec.Frames * 3
            if ($minecartBytes[$loop] -ne 0xff -or $minecartBytes[$loop + 1] -ne (255 - $spec.Frames * 3)) {
                throw "Minecart $($minecartPack.Name): native animation backward loop changed."
            }
        }
    }
    Write-GeneratedTable((Join-Path $minecartPack.Root 'objects/minecart_animation_state.tsv'), $minecartRows)
}
