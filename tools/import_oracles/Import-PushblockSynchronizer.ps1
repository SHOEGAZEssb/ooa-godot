# This stage exports files only; keep its temporary variables out of the shared
# Windows PowerShell 5.1 import scope (which has a 4096-variable limit).
& {
# INTERAC $bd has no position operands; its live position follows each scan match.
$mainPath = Join-Path $Disassembly 'objects/ages/mainData.s'
$main = Read-ImportText $mainPath
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# group`troom`torder`tsubid`tsource")
foreach ($section in [regex]::Matches($main, '(?ms)^(?<label>group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData):(?<body>.*?)(?=^\w+:|\z)')) {
    if ($section.Groups['body'].Value -notmatch 'obj_Interaction \$bd\b') { continue }
    $order = 0
    foreach ($node in @(Read-AssemblyMacroInvocations $mainPath $section.Groups['label'].Value)) {
        if ($node.Name -eq 'obj_Interaction' -and $node.Operands[0] -eq '$bd') {
            if (($node.Operands -join ' ') -ne '$bd $00') {
                throw ('INTERAC $bd: unsupported operands at {0}.' -f $section.Groups['label'].Value)
            }
            $rows.Add("$($section.Groups['group'].Value)`t$($section.Groups['room'].Value)`t$order`t00`tobjects/ages/mainData.s:$($section.Groups['label'].Value)")
        }
        if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer','obj_IfRoomFlag','obj_IfRoomFlagUnset','obj_EndIf')) { $order++ }
    }
}
if ($rows.Count -ne 3 -or [regex]::Matches($main, 'obj_Interaction \$bd\b').Count -ne 2 -or
    $rows[1] -notmatch '^4\t9b\t1\t00\t' -or $rows[2] -notmatch '^4\t9e\t1\t00\t') {
    throw 'INTERAC $bd requires both source-order $01 placements, rooms 4:9b and 4:9e.'
}
Write-GeneratedTable((Join-Path $destination 'objects/pushblock_synchronizers.tsv'), $rows)

$bank0 = Read-ImportText (Join-Path $Disassembly 'code/bank0.s')
$dimensions = Read-ImportText (Join-Path $Disassembly 'constants/common/other.s')
$tiles = Read-ImportText (Join-Path $Disassembly 'constants/common/tileIndices.s')
$code = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/pushblockSynchronizer.s')
if ($dimensions -notmatch '(?m)^\.define LARGE_ROOM_HEIGHT\s+\$(?<height>[0-9a-f]{2})') { throw 'Missing native large-room scan height.' }
$scanStart = [Convert]::ToInt32($Matches['height'],16)*16+15
if ($tiles -notmatch '(?m)^\.define TILEINDEX_SOMARIA_BLOCK\s+\$(?<tile>[0-9a-f]{2})') { throw 'Missing Somaria tile exclusion.' }
$excludedTile = $Matches['tile']
if ($bank0 -notmatch '(?ms)^findTileInRoom:\s+ld h,>wRoomLayout\s+ld l,LARGE_ROOM_HEIGHT\*\$10\+\$0f' -or
    $bank0 -notmatch '(?ms)^backwardsSearch:\s+cp \(hl\)\s+ret z\s+dec l\s+jr nz,backwardsSearch\s+cp \(hl\)\s+ret' -or
    $code -notmatch 'cp TILEINDEX_SOMARIA_BLOCK\s+jr z,@return' -or
    $code -notmatch 'ld a,\(wBlockPushAngle\)\s+and \$1f' -or
    $code -notmatch 'call interactionCheckAdjacentTileIsSolid\s+jr nz,@return\s+call getFreeInteractionSlot\s+jr nz,@return' -or
    $bank0 -notmatch '(?ms)^interactionCheckAdjacentTileIsSolid:.*?ld h,>wRoomCollisions\s+ld l,a\s+ld a,\(hl\)\s+or a\s+ret\s+@dirOffsets:\s+\.db (?<offsets>\$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2})') {
    throw 'INTERAC $bd: unsupported backward search, exclusion, angle or raw-collision probe.'
}
$offsets = [regex]::Matches($Matches['offsets'],'\$(?<value>[0-9a-f]{2})')
Write-GeneratedTable((Join-Path $destination 'objects/pushblock_synchronizer_rules.tsv'), @(
    "# scan-start`texcluded-tile`tup-offset`tright-offset`tdown-offset`tleft-offset`tsource",
    "$($scanStart.ToString('x2'))`t$excludedTile`t$($offsets[0].Groups['value'].Value)`t$($offsets[1].Groups['value'].Value)`t$($offsets[2].Groups['value'].Value)`t$($offsets[3].Groups['value'].Value)`tcode/bank0.s:findTileInRoom,backwardsSearch,interactionCheckAdjacentTileIsSolid;object_code/ages/interactions/pushblockSynchronizer.s:interactionCodebd"
))
}
