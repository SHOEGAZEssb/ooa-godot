# INTERAC_WATER_PUSHBLOCK $9e is a native solid object, not an NpcRecord.
$path = Join-Path $Disassembly 'object_code\ages\interactions\waterPushblock.s'
$source = Read-ImportText $path
foreach ($waterContract in @(
    '(?s)@subid0State0:.*?and \$01\s+jp nz,interactionDelete',
    '(?s)@subid1State0:.*?and \$01\s+jp z,interactionDelete',
    '(?s)@initialize:.*?objectMarkSolidPosition\s+ld a,\$06.*?SPEED_80.*?ld \(hl\),30.*?objectSetVisible82',
    '(?s)@state1:.*?objectPreventLinkFromPassing.*?objectCheckLinkPushingAgainstCenter.*?wForceLinkPushAnimation.*?interactionDecCounter1.*?@@notPushing:.*?ld a,30',
    '(?s)@@pushedLongEnough:.*?ld c,\$28.*?ld c,\$02.*?ld c,\$06.*?cp c\s+ret nz.*?xor \$04.*?ld \(hl\),\$40.*?DISABLE_ALL_BUT_INTERACTIONS \| DISABLE_LINK.*?wMenuDisabled.*?SNDCTRL_STOPMUSIC.*?SND_MOVEBLOCK',
    '(?s)@state2:\s+call objectApplySpeed\s+call objectPreventLinkFromPassing\s+call interactionDecCounter1\s+ret nz\s+ld \(hl\),70',
    '(?s)@substateA:.*?ld \(hl\),\$48.*?SNDCTRL_STOPSFX.*?SND_SOLVEPUZZLE',
    '(?s)@substateB:.*?wActiveMusic.*?playSound.*?wDisabledObjects.*?wMenuDisabled.*?@swapRoomLayouts.*?interactionIncState'
)) {
    if ($source -notmatch $waterContract) { throw "waterPushblock.s: unsupported native `$9e contract: $waterContract" }
}
$graphic = $interactionGraphics['158:0']
if ($null -eq $graphic -or !$gfxNames.ContainsKey($graphic.Gfx)) {
    throw 'waterPushblock.s: missing $9e graphics.'
}
$sprite = $gfxNames[$graphic.Gfx]
$animation = Resolve-NpcAnimation 0x9e $graphic.DefaultAnimation
if (!$animation) { throw 'waterPushblock.s: missing $9e initial animation.' }
Copy-GeneratedFile "gfx_compressible\ages\$sprite.png" "gfx\$sprite.png"
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# group`troom`torder`tsubid`ty`tx`tsprite`ttile-base`tpalette`tanimation`tsource")
$group = -1
$room = -1
$order = 0
foreach ($node in Read-AssemblyNodes (Join-Path $Disassembly 'objects\ages\mainData.s')) {
    if ($node.Code -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $group = [int]$Matches['group']
        $room = [Convert]::ToInt32($Matches['room'], 16)
        $order = 0
        continue
    }
    if ($group -lt 0 -or $node.Code -notmatch '^\s*obj_(?!End)') { continue }
    if ($node.Name -eq 'obj_Interaction' -and $node.Operands[0] -eq '$9e') {
        if ($node.Operands.Count -ne 4) { throw "$($node.Path):$($node.Line): unsupported `$9e placement." }
        $values = @($node.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
        if ($values[1] -notin @(0,1)) { throw "$($node.Path):$($node.Line): unsupported `$9e subid." }
        $rows.Add("$group`t$($room.ToString('x2'))`t$order`t$($values[1].ToString('x2'))`t$($values[2].ToString('x2'))`t$($values[3].ToString('x2'))`t$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$animation`tmainData.s:group$($group)Map$($room.ToString('x2'))ObjectData:$($node.Line);waterPushblock.s:interactionCode9e")
    }
    $order++
}
if ($rows.Count -ne 3 -or $rows[1] -notmatch '^1\t41\t1\t00\t68\t58\t' -or
    $rows[2] -notmatch '^1\t41\t2\t01\t68\t38\t') {
    throw 'waterPushblock.s: expected ordered $9e:$00/$01 placements after the $e1 portal in 1:41.'
}
Write-GeneratedTable((Join-Path $destination 'objects\water_pushblocks.tsv'), $rows)

# Preserve the actual XOR call order, including @swapRoomLayouts falling
# through to @@xor for the sixth present-era room ($052).
$swap = @((Read-AssemblyNodes $path) | Where-Object { $_.Line -gt
    (@((Read-AssemblyNodes $path) | Where-Object { $_.Code -eq '@swapRoomLayouts:' })[0]).Line })
$flagRows = [Collections.Generic.List[string]]::new()
$flagRows.Add("# order`tgroup`troom`txor-mask`tsource")
$flagRoom = -1
foreach ($node in $swap) {
    if ($node.Code -match '^ld l,<ROOM_AGES_(?<room>[01][0-9a-f]{2})$') {
        $flagRoom = [Convert]::ToInt32($Matches['room'],16)
    }
    if ($node.Code -eq 'call @@xor' -or $node.Code -eq '@@xor:') {
        if ($flagRoom -lt 0) { throw 'waterPushblock.s: XOR before a room base.' }
        $flagRows.Add("$($flagRows.Count-1)`t$($flagRoom -shr 8)`t$(($flagRoom -band 255).ToString('x2'))`t01`twaterPushblock.s:@swapRoomLayouts:$($node.Line)")
        $flagRoom++
    }
}
if ($flagRows.Count -ne 13 -or $source -notmatch '(?s)@@xor:\s+ld a,\(hl\)\s+xor \$01\s+ldi \(hl\),a\s+ret') {
    throw 'waterPushblock.s: expected twelve ordered layout flag XORs.'
}
Write-GeneratedTable((Join-Path $destination 'objects\water_pushblock_rooms.tsv'), $flagRows)
$musicSource = Read-ImportText (Join-Path $Disassembly 'constants\common\music.s')
Write-GeneratedTable((Join-Path $destination 'objects\water_pushblock_sounds.tsv'), @(
    "# name`tid`tsource"
    foreach ($name in @('SNDCTRL_STOPMUSIC','SND_MOVEBLOCK','SND_FLOODGATES','SNDCTRL_STOPSFX','SND_SOLVEPUZZLE')) {
        $id = if ($soundIds.ContainsKey($name)) { $soundIds[$name] }
        elseif ($musicSource -match "(?m)^\.define $name\s+\`$(?<id>[0-9a-f]{2})\b") {
            [Convert]::ToInt32($Matches['id'],16)
        } else { throw "waterPushblock.s: missing sound $name." }
        "$name`t$($id.ToString('x2'))`twaterPushblock.s:interactionCode9e"
    }
))
