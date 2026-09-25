# Vanilla tilesets share mappings and compose VRAM through main/unique headers.
# Generate per-tileset assets without requiring hack-base or an assembled ROM.
function Expand-TransitionGraphics([byte[]]$rom, [int]$address, [int]$tiles, [int]$mode) {
    # decompressGraphics / tools/common.py:decompressGfxData. Header modes
    # select raw bytes, short/long dictionary references, or common-byte masks.
    $output = [Collections.Generic.List[byte]]::new()
    $length = $tiles * 16
    if ($mode -eq 0) { return [byte[]]$rom[$address..($address + $length - 1)] }
    if ($mode -eq 2) {
        foreach ($tile in 1..$tiles) {
            $mask1 = $rom[$address++]; $mask2 = $rom[$address++]
            if (($mask1 -bor $mask2) -eq 0) {
                foreach ($i in 1..16) { $output.Add($rom[$address++]) }
            } else {
                $common = $rom[$address++]
                foreach ($mask in @($mask1, $mask2)) {
                    foreach ($bit in 7..0) {
                        if (($mask -band (1 -shl $bit)) -ne 0) { $output.Add($common) }
                        else { $output.Add($rom[$address++]) }
                    }
                }
            }
        }
    } elseif ($mode -eq 1 -or $mode -eq 3) {
        while ($output.Count -lt $length) {
            $flags = $rom[$address++]
            foreach ($bit in 7..0) {
                if ($output.Count -ge $length) { break }
                if (($flags -band (1 -shl $bit)) -eq 0) { $output.Add($rom[$address++]); continue }
                $first = $rom[$address++]
                if ($mode -eq 1) {
                    $distance = ($first -band 0x1f) + 1
                    $count = ($first -shr 5) + 1
                    if (($first -band 0xe0) -eq 0) { $count = $rom[$address++] }
                } else {
                    $second = $rom[$address++]
                    $distance = $first + (([int]$second -band 7) -shl 8) + 1
                    $count = ($second -shr 3) + 2
                    if (($second -band 0xf8) -eq 0) { $count = $rom[$address++] }
                }
                if ($count -eq 0) { $count = 256 }
                foreach ($i in 1..$count) {
                    $index = $output.Count - $distance
                    $output.Add($(if ($index -lt 0) { 0 } else { $output[$index] }))
                }
            }
        }
    } else { throw "Unsupported decompressGraphics mode $mode." }
    if ($output.Count -ne $length) { throw 'Unique graphics decompression length mismatch.' }
    return $output.ToArray()
}

# Keep this stage's temporary buffers local: Windows PowerShell limits the
# number of variables in the shared importer scope.
function Export-VanillaTilesets {
function Copy-VanillaGraphicsHeader([int]$pointer, [int]$bankBase, [byte[]]$vram) {
    $entries = 0
    do {
        if ($pointer -lt 0x4000 -or $pointer -gt 0x7ffa -or $entries++ -ge 32) {
            throw "Vanilla GFX header pointer outside bank: $($pointer.ToString('x4'))."
        }
        $entry = $bankBase + $pointer
        $bankMode = $romBytes[$entry]
        if ($bankMode -eq 0) { break } # Unique header's terminal palette command.
        $address = ([int]$romBytes[$entry + 3] -shl 8) -bor ($romBytes[$entry + 4] -band 0xf0)
        $tiles = ($romBytes[$entry + 5] -band 0x7f) + 1
        if (($romBytes[$entry + 4] -band 0x0f) -ne 1 -or $address -lt 0x8800 -or $address + $tiles * 16 -gt 0x9800) {
            throw "Vanilla tileset GFX header at $($pointer.ToString('x4')) writes outside BG bank 1."
        }
        $source = (($bankMode -band 0x3f) * 0x4000) + (([int]$romBytes[$entry + 1] -band 0x3f) -shl 8) + $romBytes[$entry + 2]
        $data = Expand-TransitionGraphics $romBytes $source $tiles ($bankMode -shr 6)
        [Array]::Copy($data, 0, $vram, $address - 0x8800, $data.Length)
        $more = $romBytes[$entry + 5] -band 0x80
        $pointer += 6
    } while ($more -ne 0)
}

Add-Type -AssemblyName System.Drawing
# Resolve the vanilla source's symbolic header IDs and check every concrete
# tileset byte against the hash-checked ROM before following ROM addresses.
$headerIds = @{}
foreach ($spec in @(@('gfxHeaders.s', 'm_GfxHeaderStart'),
                    @('uniqueGfxHeaders.s', 'm_UniqueGfxHeaderStart'),
                    @('paletteHeaders.s', 'm_PaletteHeaderStart'))) {
    foreach ($node in Read-AssemblyMacroInvocations (Join-Path $Disassembly "data/ages/$($spec[0])") '' $spec[1]) {
        if ($node.Operands.Count -lt 2) { throw "Malformed header declaration in $($spec[0])." }
        $headerIds[$node.Operands[1]] = Convert-AssemblyInteger $node.Operands[0]
    }
}
$sourceBytes = [Collections.Generic.List[int]]::new()
foreach ($node in Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/tilesets.s') 'tilesetData' '.db') {
    foreach ($operand in $node.Operands) {
        $value = if ($headerIds.ContainsKey($operand)) { $headerIds[$operand] } else { Convert-AssemblyInteger $operand }
        $sourceBytes.Add($value)
    }
}
if ($sourceBytes.Count -lt 0x67 * 8) { throw 'Vanilla tilesetData is missing concrete records $00-$66.' }
foreach ($index in 0..(0x67 * 8 - 1)) {
    if ($sourceBytes[$index] -ne $romBytes[0x10f9c + $index]) {
        throw "data/ages/tilesets.s:tilesetData+$($index.ToString('x3')) disagrees with clean US; use the supported vanilla checkout."
    }
}
# Runtime layout-only overrides are valid only while every other tileset byte
# is identical (Maku saved $22->$24; Nuun companions $0d->$0e/$0f).
foreach ($pair in @(@(0x22, 0x24), @(0x0d, 0x0e), @(0x0d, 0x0f))) {
    foreach ($field in @(0, 1, 2, 3, 4, 5, 7)) {
        if ($sourceBytes[$pair[0] * 8 + $field] -ne $sourceBytes[$pair[1] * 8 + $field]) {
            throw "tilesetData: layout-only override $($pair[0].ToString('x2'))->$($pair[1].ToString('x2')) changes byte $field."
        }
    }
}
$cliffSource = Read-ImportText (Join-Path $Disassembly 'code/ages/loadTilesetData.s')
if ($cliffSource -notmatch '(?ms)^setPastCliffPalettesToRed:\s+ld a,\(wActiveCollisions\)\s+or a\s+jr nz,@done\s+ld a,\(wTilesetFlags\)\s+and TILESETFLAG_PAST\s+jr z,@done\s+ld a,\(wActiveRoom\)\s+cp <ROOM_AGES_138\s+ret z.*?ld hl,w3TileMappingData \+ \$204\s+ld d,\$06.*?ld b,\$04.*?and \$07.*?cp d.*?and \$f8.*?ld a,\$04.*?cp \$d4') {
    throw 'loadTilesetData.s:setPastCliffPalettesToRed gates or attribute boundaries changed.'
}
New-Item -ItemType Directory -Force (Join-Path $destination 'gfx') | Out-Null
foreach ($id in 0..0x66) {
    $offset = 0x10f9c + $id * 8
    $layout = $romBytes[$offset + 5]
    $hex = $id.ToString('x2')
    $headers = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/tilesetHeaders.s') "tilesetLayoutGroup$($layout.ToString('x2'))" 'm_TilesetLayoutHeader')
    if ($headers.Count -ne 2) { throw "tileset `$${hex}: expected mapping/collision headers for layout $($layout.ToString('x2'))." }
    foreach ($index in 0..1) {
        $kind = @('Mappings', 'Collisions')[$index]
        $label = $headers[$index].Operands[1]
        if ($label -notmatch "^tileset${kind}[0-9a-f]{2}$") { throw "Unexpected tileset header source $label." }
        Copy-GeneratedFile "tileset_layouts/ages/$label.bin" "layouts/tileset${kind}${hex}.bin"
    }
    $vram = [byte[]]::new(0x1000)
    $main = $romBytes[$offset + 3]
    $unique = $romBytes[$offset + 2]
    Copy-VanillaGraphicsHeader ([BitConverter]::ToUInt16($romBytes, 0x69da + $main * 2)) 0 $vram
    if ($unique -ne 0) {
        Copy-VanillaGraphicsHeader ([BitConverter]::ToUInt16($romBytes, 0x11b28 + $unique * 2)) 0xc000 $vram
    }
    $bitmap = [Drawing.Bitmap]::new(128, 128)
    try {
        foreach ($tile in 0..255) {
            foreach ($y in 0..7) {
                $low = $vram[$tile * 16 + $y * 2]; $high = $vram[$tile * 16 + $y * 2 + 1]
                foreach ($x in 0..7) {
                    $shade = (($low -shr (7 - $x)) -band 1) -bor ((($high -shr (7 - $x)) -band 1) -shl 1)
                    $value = (3 - $shade) * 85
                    $bitmap.SetPixel(($tile % 16) * 8 + $x, [int][Math]::Floor($tile / 16) * 8 + $y, [Drawing.Color]::FromArgb(255, $value, $value, $value))
                }
            }
        }
        $bitmap.Save((Join-Path $destination "gfx/gfx_tileset${hex}.png"), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
}
}
Export-VanillaTilesets
