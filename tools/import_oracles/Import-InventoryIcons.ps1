# The local hack-base art and display tables contain CROSSITEMS rearrangements.
# Restore the supported US game's bytes after the source-asset copying stages.
# Offsets are the original data/ages/gfxDataBank19_1.s and gfxDataMain.s extents;
# compressed streams use decompressGraphics mode $01. The ROM hash is checked
# by Initialize-Import.ps1 before any stage runs.
Add-Type -AssemblyName System.Drawing
foreach ($entry in @(
    @('gfx/spr_item_icons_1.png', 0x064000, 32, 0, $true),
    @('gfx/spr_item_icons_2.png', 0x064200, 32, 0, $true),
    @('gfx/spr_item_icons_3.png', 0x064400, 32, 0, $true),
    @('gfx/spr_item_icons_1_spr.png', 0x0a4a75, 32, 1, $true),
    @('gfx/gfx_hud.png', 0x0a5363, 32, 1, $false),
    @('inventory/gfx_inventory_hud_1.png', 0x0a5469, 48, 1, $false)
)) {
    $data = Expand-TransitionGraphics $romBytes $entry[1] $entry[2] $entry[3]
    $bitmap = [Drawing.Bitmap]::new(128, [int]($entry[2] / 16 * 8))
    try {
        for ($tile = 0; $tile -lt $entry[2]; $tile++) {
            $tileX = if ($entry[4]) { [int][Math]::Floor($tile / 2) % 16 } else { $tile % 16 }
            $tileY = if ($entry[4]) { $tile % 2 } else { [int][Math]::Floor($tile / 16) }
            for ($y = 0; $y -lt 8; $y++) {
                $low = $data[$tile * 16 + $y * 2]
                $high = $data[$tile * 16 + $y * 2 + 1]
                for ($x = 0; $x -lt 8; $x++) {
                    $shade = (($low -shr (7 - $x)) -band 1) -bor ((($high -shr (7 - $x)) -band 1) -shl 1)
                    $value = if ($entry[4]) { $shade * 85 } else { (3 - $shade) * 85 }
                    $bitmap.SetPixel($tileX * 8 + $x, $tileY * 8 + $y,
                        [Drawing.Color]::FromArgb(255, $value, $value, $value))
                }
            }
        }
        $bitmap.Save((Join-Path $destination $entry[0]), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
}
