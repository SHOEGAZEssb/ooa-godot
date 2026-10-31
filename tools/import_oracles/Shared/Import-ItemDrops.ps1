# The common drop algorithm selects native tables, including Seasons' Subrosia
# remapping. Follow compiled pointers; probability rows deliberately alias.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent) }
)) {
    $selection = [Collections.Generic.List[byte]]::new()
    $selection.AddRange([byte[]](Read-MenuRomBytes $pack 'itemDropTables' 144))
    foreach ($spec in @(@('itemDropProbabilityTable',8,8), @('itemDropSetTable',16,32))) {
        $table = [int]$pack.Symbols[$spec[0]]
        $bank = [int][Math]::Floor($table / 0x4000) * 0x4000
        $pointers = Read-MenuRomBytes $pack $spec[0] ($spec[1] * 2)
        for ($index = 0; $index -lt $spec[1]; $index++) {
            $label = if ($spec[0] -eq 'itemDropProbabilityTable') { "itemDropProbabilityTable@probability$index" } else { "itemDropSet$($index.ToString('X'))" }
            $pointer = [BitConverter]::ToUInt16($pointers,$index * 2)
            if ($pointer -lt 0x4000 -or $pointer -ge 0x8000 -or -not $pack.Symbols.ContainsKey($label) -or $pack.Symbols[$label] -ne $bank + ($pointer -band 0x3fff)) { throw "$($pack.Name):${label}: unresolved native drop pointer." }
            $selection.AddRange([byte[]](Read-MenuRomBytes $pack $label $spec[2]))
        }
    }
    if ($selection.Count -ne 720) { throw "$($pack.Name): native drop selection extent changed." }
    Write-GeneratedBytes((Join-Path $pack.Root 'metadata/itemDrops.bin'),$selection.ToArray())
    $availability = Read-MenuRomBytes $pack 'itemDropAvailabilityTable' 32
    $rewards = Read-MenuRomBytes $pack 'partCode01@itemDropTreasureTable' 64
    $remap = if ($pack.Name -eq 'seasons') { Read-MenuRomBytes $pack 'subrosiaDropSet' 16 } else { [byte[]](0..15) }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# subid`tsubrosia-drop`tavailability-offset`tavailability-mask`ttreasure`tjoy-ring`tparameter`tdoubled-parameter")
    for ($id = 0; $id -lt 16; $id++) {
        if ($remap[$id] -gt 15) { throw "$($pack.Name): subrosiaDropSet has an invalid drop `$$($remap[$id].ToString('x2'))." }
        $fields = @($id,$remap[$id],$availability[$id*2],$availability[$id*2+1]) + @($rewards[($id*4)..($id*4+3)])
        $rows.Add(($fields | ForEach-Object { ([int]$_).ToString('x2') }) -join "`t")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/item_drop_rules.tsv'),$rows)
    $speeds = if ($pack.Name -eq 'seasons') { Read-MenuRomBytes $pack 'itemDrop_magnetGlovePullSpeed' 8 } else { [byte[]]@() }
    Write-GeneratedBytes((Join-Path $pack.Root 'metadata/item_drop_magnet_speeds.bin'),$speeds)
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# collision-set`ttile`tangle")
    foreach ($stream in (Read-CollisionTableStreams $pack "data/$($pack.Name)/tile_properties/conveyorItemDropTiles.s" 'itemDropConveyorTilesTable' 2)) {
        foreach ($entry in $stream.Rows) { $rows.Add("$($stream.Mode)`t$($entry[0].ToString('x2'))`t$($entry[1].ToString('x2'))") }
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/item_drop_conveyors.tsv'),$rows)

    $part = Read-MenuRomBytes $pack 'partData' 16
    $gfx = if ($pack.Name -eq 'ages') { 0x78 } else { 0x5c }
    $gfxSource = Read-ImportText (Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s")
    if ($part[8] -ne $gfx -or $part[9] -ne 1 -or $part[10] -ne 0x44 -or
        $gfxSource -notmatch ('/\* \$' + $gfx.ToString('x2') + ' \*/ m_ObjectGfxHeader spr_common_items')) { throw "$($pack.Name): PART_ITEM_DROP native graphics/collision/radii changed." }
    $sprites = Read-MenuRomBytes $pack 'itemDrop_initGfx@spriteData' 32
    $animations = Read-MenuRomBytes $pack 'part01Animations' 32
    $oamPointers = Read-MenuRomBytes $pack 'part01OamDataPointers' 8
    $animationBank = [int][Math]::Floor($pack.Symbols['part01Animations'] / 0x4000) * 0x4000
    $oamLabels = @($pack.Symbols.Keys | Where-Object { $_ -cmatch '^partOamData[0-9a-f]+$' })
    $oamBanks = @($oamLabels | ForEach-Object { [int][Math]::Floor($pack.Symbols[$_] / 0x4000) * 0x4000 } | Sort-Object -Unique)
    if ($oamBanks.Count -ne 1) { throw "$($pack.Name): PART OAM data requires an explicit bank selection." }
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# subid`ttile-base`tpalette`tanimation")
    for ($id = 0; $id -lt 16; $id++) {
        $pointer = [BitConverter]::ToUInt16($animations,$id*2)
        if ($pointer -lt 0x4000 -or $pointer -ge 0x8000) { throw "$($pack.Name): invalid PART_ITEM_DROP animation pointer." }
        $at = $animationBank + ($pointer -band 0x3fff)
        if ($pack.Bytes[$at] -ne 0x7f -or $pack.Bytes[$at+1] -notin @(0,2,4,6) -or $pack.Bytes[$at+2] -ne 0) { throw "$($pack.Name): PART_ITEM_DROP `$$($id.ToString('x2')) no longer uses a single static frame." }
        $oam = [BitConverter]::ToUInt16($oamPointers,$pack.Bytes[$at+1])
        if ($oam -lt 0x4000 -or $oam -ge 0x8000) { throw "$($pack.Name): invalid PART_ITEM_DROP OAM pointer." }
        $at = $oamBanks[0] + ($oam -band 0x3fff)
        $labels = @($oamLabels | Where-Object { $pack.Symbols[$_] -eq $at })
        $count = [int]$pack.Bytes[$at]
        if ($labels.Count -ne 1 -or $count -ne $(if ($id -eq 15) { 2 } else { 1 })) { throw "$($pack.Name): unresolved PART_ITEM_DROP OAM record." }
        $cells = @(for ($cell = 0; $cell -lt $count; $cell++) { @($pack.Bytes[($at+1+$cell*4)..($at+4+$cell*4)] | ForEach-Object { [int]$_ }) -join ',' }) -join ';'
        $tile = [int]$part[13] + [int]$sprites[$id*2]
        $palette = $sprites[$id*2+1] -band 7
        $rows.Add("$id`t$tile`t$palette`t127@$cells")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/item_drops.tsv'),$rows)
    $source = Find-ImportSpriteSource 'spr_common_items'
    Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $pack.Root 'gfx/spr_common_items.png') -Force
}
