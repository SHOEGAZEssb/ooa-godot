# Execute the native ordered graphics/palette headers at import time. The
# resulting VRAM includes real bank selection, compression and omitted padding.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Destination = $destination; Blurbs = 16 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Destination = (Split-Path $previewDestination -Parent); Blurbs = 12 }
)) {
    foreach ($mode in @('present', 'past', 'dungeon')) {
        $id = 0x0d + [Array]::IndexOf(@('present', 'past', 'dungeon'), $mode)
        $label = 'gfxHeader' + $id.ToString('x2')
        $table = [int]$pack.Symbols['gfxHeaderTable']
        $base = [int][Math]::Floor($table / 0x4000) * 0x4000 - 0x4000
        $at = $base + [BitConverter]::ToUInt16($pack.Bytes, $table + $id * 2)
        if ($at -ne $pack.Symbols[$label]) { throw "$($pack.Name) ${label}: header pointer disagrees with compiled symbol." }
        $vram = [byte[]]::new(0x4000); $map = $null; $flags = $null; $order = 0
        do {
            if ($order++ -ge 16 -or $at -lt $base + 0x4000 -or $at + 6 -gt $base + 0x8000) { throw "$($pack.Name) ${label}: invalid graphics header extent." }
            $bankMode = [int]$pack.Bytes[$at]
            if ($bankMode -eq 0) { throw "$($pack.Name) ${label}: unexpected palette command." }
            $source = (($bankMode -band 0x3f) * 0x4000) + (([int]$pack.Bytes[$at + 1] -band 0x3f) -shl 8) + $pack.Bytes[$at + 2]
            $address = ([int]$pack.Bytes[$at + 3] -shl 8) -bor ($pack.Bytes[$at + 4] -band 0xf0)
            $bank = $pack.Bytes[$at + 4] -band 15
            $tiles = ($pack.Bytes[$at + 5] -band 0x7f) + 1
            $data = Expand-TransitionGraphics $pack.Bytes $source $tiles ($bankMode -shr 6)
            if ($address -ge 0x8000 -and $address + $data.Length -le 0xa000 -and $bank -lt 2) {
                [Array]::Copy($data, 0, $vram, $bank * 0x2000 + $address - 0x8000, $data.Length)
            } elseif ($bank -eq 4 -and $address -eq $pack.Symbols['w4TileMap'] -and $data.Length -eq 576) { $map = $data }
            elseif ($bank -eq 4 -and $address -eq $pack.Symbols['w4AttributeMap'] -and $data.Length -eq 576) { $flags = $data }
            else { throw "$($pack.Name) ${label}: unsupported upload to $($address.ToString('x4')) bank $bank, $($data.Length) bytes." }
            $more = $pack.Bytes[$at + 5] -band 0x80; $at += 6
        } while ($more)
        if ($null -eq $map -or $null -eq $flags) { throw "$($pack.Name) ${label}: missing native map/attribute upload." }
        Write-GeneratedBytes((Join-Path $pack.Destination "map/vram_$mode.bin"), $vram)
        Write-GeneratedBytes((Join-Path $pack.Destination "map/map_$mode.bin"), $map)
        Write-GeneratedBytes((Join-Path $pack.Destination "map/flags_$mode.bin"), $flags)

        $header = 'PALH_' + ($id - 6).ToString('x2')
        $path = "data/$($pack.Name)/paletteHeaders.s"
        $body = [regex]::Match((Read-ImportText (Join-Path $Disassembly $path)), '(?ms)^m_PaletteHeaderStart\s+\$[0-9a-f]+,\s*' + $header + '\s*\r?\n(?<body>.*?)(?=^m_PaletteHeaderStart|\z)')
        if (-not $body.Success) { throw "${path}: missing $header." }
        $bg = [byte[]]::new(96); $spr = [byte[]]::new(96); $bgMask = 0; $sprMask = 0
        foreach ($line in $body.Groups['body'].Value -split '\r?\n') {
            $code = ($line -split ';', 2)[0].Trim()
            if (-not $code -or $code -eq 'm_PaletteHeaderEnd') { continue }
            if ($code -notmatch '^m_PaletteHeader(?<kind>Bg|Spr)\s+(?<first>\d+),\s*(?<count>\d+),\s*(?<label>\w+)$') { throw "${path}:${header}: unsupported command '$code'." }
            $first = [int]$Matches['first']; $count = [int]$Matches['count']; $kind = $Matches['kind']; $colorLabel = $Matches['label']
            if ($first + $count -gt 8) { throw "${path}:${header}: palette upload exceeds eight slots." }
            $colors = Read-MenuRomBytes $pack $colorLabel ($count * 8)
            $buffer = $bg
            if ($kind -eq 'Spr') { $buffer = $spr }
            for ($color = 0; $color -lt $count * 4; $color++) {
                $rgb = [BitConverter]::ToUInt16($colors, $color * 2); $offset = ($first * 4 + $color) * 3
                $buffer[$offset] = $rgb -band 31; $buffer[$offset + 1] = ($rgb -shr 5) -band 31; $buffer[$offset + 2] = ($rgb -shr 10) -band 31
            }
            $mask = ((1 -shl $count) - 1) -shl $first
            if ($kind -eq 'Bg') { $bgMask = $bgMask -bor $mask } else { $sprMask = $sprMask -bor $mask }
        }
        $expectedBg = if ($mode -eq 'dungeon') { 0x3c } else { 0xff }
        $expectedSpr = if ($mode -eq 'dungeon') { 0x3f } else { 0xff }
        if ($bgMask -ne $expectedBg -or $sprMask -ne $expectedSpr) { throw "${path}:${header}: unexpected native palette coverage." }
        Write-GeneratedBytes((Join-Path $pack.Destination "map/bg_$mode.bin"), $bg)
        Write-GeneratedBytes((Join-Path $pack.Destination "map/spr_$mode.bin"), $spr)
    }
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# dungeon`tgfx-header`tgraphic`tasset`talias-of`tsource')
    $source = "data/$($pack.Name)/gfxHeaders.s"; $nodes = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly $source)); $aliases = @{}
    for ($index = 0; $index -lt $pack.Blurbs; $index++) {
        $header = "GFXH_DUNGEON_$($index.ToString('X1'))_BLURB"
        $start = @($nodes | Where-Object { $_.Name -eq 'm_GfxHeaderStart' -and $_.Operands[1] -ceq $header })
        if ($start.Count -ne 1) { throw "${source}:${header}: missing or ambiguous blurb header." }
        $graphic = @($nodes | Where-Object { $_.Line -gt $start[0].Line -and $_.Name -eq 'm_GfxHeader' } | Select-Object -First 1)
        if ($graphic.Count -ne 1 -or $graphic[0].Operands[0] -notmatch '^gfx_blurb_(?<asset>[a-z0-9]+)$' -or $graphic[0].Operands[1] -ne '$8c00') { throw "${source}:${header}: unsupported blurb upload." }
        $name = $graphic[0].Operands[0]; $asset = $Matches['asset']
        $paths = @(foreach ($scope in @($pack.Name, 'common')) { $relative = "gfx_compressible/$scope/$name.png"; if (Test-Path -LiteralPath (Join-Path $Disassembly $relative)) { $relative } })
        if ($paths.Count -ne 1) { throw "${source}:${header}: missing or ambiguous blurb artwork." }
        Copy-GeneratedFile $paths[0] "map/blurb_$asset.png" $pack.Destination
        $alias = if ($aliases.ContainsKey($name)) { $aliases[$name] } else { $aliases[$name] = $header; '' }
        $rows.Add("$index`t$header`t$name`t$asset`t$alias`t${source}:${header}/$name")
    }
    Write-GeneratedTable((Join-Path $pack.Destination 'menu/dungeon_blurbs.tsv'), $rows)
    Copy-GeneratedFile "gfx_compressible/$($pack.Name)/spr_minimap_icons.png" 'map/sprites.png' $pack.Destination
    if ($pack.Name -eq 'seasons') {
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# text-id`tposition`tmessage-base64')
        foreach ($id in @($seasonMapTexts.Keys | Sort-Object)) {
            $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($seasonMapTexts[$id]))
            $rows.Add("$($id.ToString('x4'))`t0`t$encoded")
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'map/texts.tsv'), $rows)
        Write-GeneratedBytes((Join-Path $pack.Destination 'map/jewel_locations.bin'), (Read-MenuRomBytes $pack 'mapMenu_drawJewelLocations@jewelLocations' 8))
    }
}
