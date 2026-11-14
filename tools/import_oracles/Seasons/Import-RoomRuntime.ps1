$seasonDestination = Split-Path $previewDestination -Parent
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# group`troom`tsource")
$path = Join-Path $Disassembly 'objects/seasons/mainData.s'
foreach ($room in @(0x97, 0x98, 0xa7, 0xb6, 0xb5, 0xb4, 0xc4, 0xc3, 0xd3, 0xd4, 0xcb, 0xbb, 0xab, 0x9b, 0x9a)) {
    $label = 'group0Map' + $room.ToString('x2') + 'ObjectData'
    $nodes = @(Read-AssemblyMacroInvocations $path $label)
    if ($nodes.Count -eq 0) {
        if (-not $seasonsSymbols.ContainsKey($label) -or $seasonsRomBytes[$seasonsSymbols[$label]] -ne 0xff) { throw "${label}: unresolved empty-stream alias." }
    } elseif ($nodes[-1].Name -ne 'obj_End') { throw "${label}: missing object-stream terminator." }
    $rows.Add("0`t$($room.ToString('x2'))`tobjects/seasons/mainData.s:$label")
}
foreach ($room in @((0x02..0x08) + (0x0a..0x1c))) {
    $label = 'group4Map' + $room.ToString('x2') + 'ObjectData'
    $nodes = @(Read-AssemblyMacroInvocations $path $label)
    if ($nodes.Count -eq 0) {
        if (-not $seasonsSymbols.ContainsKey($label) -or $seasonsRomBytes[$seasonsSymbols[$label]] -ne 0xff) { throw "${label}: unresolved empty Hero's Cave stream alias." }
    } elseif ($nodes[-1].Name -ne 'obj_End') { throw "${label}: missing Hero's Cave object-stream terminator." }
    $rows.Add("4`t$($room.ToString('x2'))`tobjects/seasons/mainData.s:$label")
}
$nodes=@(Read-AssemblyMacroInvocations $path 'group4Map09ObjectData')
if($nodes.Count -ne 2 -or $nodes[0].Name -ne 'obj_Interaction' -or
    ($nodes[0].Operands -join ',') -ne '$6b,$20,$58,$68' -or $nodes[1].Name -ne 'obj_End') {
    throw 'group6Map09 aliases group4Map09: expected the original static Seed Satchel producer.'
}
$rows.Add("6`t09`tobjects/seasons/mainData.s:group4Map09ObjectData")
foreach ($placement in @(@(4, 0x26, 0x57), @(4, 0xb1, 0x57), @(5, 0x6f, 0x67))) {
    $group, $room, $position = $placement
    $label = 'group' + $group.ToString('x1') + 'Map' + $room.ToString('x2') + 'ObjectData'
    $nodes = @(Read-AssemblyMacroInvocations $path $label)
    if ($nodes.Count -ne 2 -or $nodes[0].Name -ne 'obj_Interaction' -or $nodes[1].Name -ne 'obj_End' -or
        ($nodes[0].Operands -join ',') -ne ('$7d,$00,$' + $position.ToString('x2') + ',$01')) {
        throw "${label}: shared spinner-only native room stream changed."
    }
    $rows.Add("$group`t$($room.ToString('x2'))`tobjects/seasons/mainData.s:$label")
}
$rows.Add("5`t03`tobjects/seasons/mainData.s:group5Map03ObjectData")
$rows.Add("1`t05`tobjects/seasons/mainData.s:group1Map05ObjectData")
$rows.Add("3`t91`tobjects/seasons/mainData.s:group1Map91ObjectData")
Write-GeneratedTable((Join-Path $seasonDestination 'opening/supported_object_rooms.tsv'), $rows)

$satchel=$seasonsSymbols['sidescrollingStaticSeedSatchel']
$helper=$seasonsSymbols['misc1_spawnTreasureBCifRoomFlagBit5NotSet']
if($null -eq $satchel -or $null -eq $helper -or
    ($seasonsRomBytes[$satchel..($satchel+5)] -join ',') -ne
        ('1,0,25,195,'+($helper -band 255)+','+(0x40 -bor (($helper -shr 8)-band 0x3f)))) {
    throw 'miscellaneous1.s:sidescrollingStaticSeedSatchel must pass TREASURE_SEED_SATCHEL:$00 to the ROOMFLAG_ITEM-checked allocator.'
}
Write-GeneratedTable((Join-Path $seasonDestination 'objects/static_seed_satchel.tsv'),@(
    "# subid`ttreasure`ttreasure-subid`tsource",
    "20`t19`t00`tobject_code/seasons/interactions/miscellaneous1.s:sidescrollingStaticSeedSatchel"))

$refills = [Collections.Generic.List[string]]::new()
$refills.Add("# index`tgroup`troom`tbit-index")
$start = $seasonsSymbols['seedTreeRefillLocations']
if ($null -eq $start) { throw 'Seasons seedTreeRefillLocations: missing compiled symbol.' }
for ($index = 0; $index -lt 8; $index++) {
    $room = $seasonsRomBytes[$start + $index * 2]
    $offset = $seasonsRomBytes[$start + $index * 2 + 1]
    if ($offset -ne (($seasonsSymbols['wxSeedTreeRefillData'] + $index * 8) -band 0xff)) { throw "Seasons seedTreeRefillLocations: unexpected history offset at index $index." }
    $refills.Add("$index`t0`t$($room.ToString('x2'))`t$(7 - $index)")
}
Write-GeneratedTable((Join-Path $seasonDestination 'metadata/seed_tree_refills.tsv'), $refills)
