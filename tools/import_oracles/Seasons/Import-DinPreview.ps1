# GFXH_DIN_DANCING_CUTSCENE / PALH_SEASONS_95. Ages already exports the
# corresponding Nayru still; the launcher consumes both without starting a game.
foreach ($name in @('spr_din_dance_cutscene', 'gfx_din_dance_cutscene_1',
        'gfx_din_dance_cutscene_2', 'gfx_din_dance_cutscene_3')) {
    Copy-GeneratedFile "gfx_compressible/seasons/$name.png" "$name.png" $previewDestination
}
foreach ($name in @('map_din_dance_cutscene', 'flg_din_dance_cutscene')) {
    Copy-GeneratedFile "gfx_compressible/seasons/$name.bin" "$name.bin" $previewDestination
}
$headers = Read-ImportText (Join-Path $Disassembly 'data/seasons/paletteHeaders.s')
if ($headers -notmatch '(?ms)PALH_SEASONS_95\s+m_PaletteHeaderBg\s+0, 8, paletteData4470\s+m_PaletteHeaderSpr\s+0, 7, paletteData44b0\s+m_PaletteHeaderEnd') {
    throw 'data/seasons/paletteHeaders.s: PALH_SEASONS_95 background/sprite layout changed.'
}
$palettes = Read-ImportText (Join-Path $Disassembly 'data/seasons/paletteData.s')
$background = [byte[]](Read-PaletteBytes 'paletteData4470' 32 $palettes)
$inversions = @(1..3 | ForEach-Object { Get-ImportSpriteSourceGrayscaleInverted "gfx_din_dance_cutscene_$_" } | Select-Object -Unique)
if ($inversions.Count -ne 1) {
    throw 'gfx_compressible/seasons/gfx_din_dance_cutscene_*.properties: mixed background encodings require separate palettes.'
}
# OracleTileRenderer addresses background PNG shades from white to black.
# These three source sheets invert that order, unlike the Ages close-up.
if ($inversions[0]) {
    $original = [byte[]]$background.Clone()
    for ($palette = 0; $palette -lt 8; $palette++) {
        for ($shade = 0; $shade -lt 4; $shade++) {
            [Array]::Copy($original, ($palette * 4 + 3 - $shade) * 3,
                $background, ($palette * 4 + $shade) * 3, 3)
        }
    }
}
Write-GeneratedBytes((Join-Path $previewDestination 'din_bg_palette.bin'), $background)
Write-GeneratedBytes((Join-Path $previewDestination 'din_sprite_palette.bin'),
    (Read-PaletteBytes 'paletteData44b0' 28 $palettes))

$source = 'code/seasons/cutscenes/introCutscenes.s'
$oam = @(Read-AssemblyLiteralValues (Join-Path $Disassembly $source) 'seasonsOamData_03_7397')
if ($oam.Count -ne 145 -or $oam[0] -ne 0x24) {
    throw "${source}:seasonsOamData_03_7397: expected 36 four-byte sprites after the count."
}
$rows = [Collections.Generic.List[string]]::new()
$rows.Add('# y`tx`ttile`tflags')
for ($index = 1; $index -lt $oam.Count; $index += 4) {
    $rows.Add((($oam[$index..($index + 3)] | ForEach-Object { $_.ToString('x2') }) -join "`t"))
}
Write-GeneratedTable((Join-Path $previewDestination 'din_oam.tsv'), $rows)
