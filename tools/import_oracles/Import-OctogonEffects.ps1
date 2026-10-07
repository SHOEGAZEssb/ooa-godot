& {
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# id`tanimation-index`tsprite`ttile-base`tpalette`tsource-grayscale-inverted`tanimation`tsource")
foreach ($id in @(0x8e,0x91)) {
    $graphics = $interactionGraphics["${id}:0"]
    if ($null -eq $graphics) { throw "Missing Octogon interaction graphics for `$$($id.ToString('x2'))." }
    $sprite = if ($graphics.Gfx -eq 0) { 'spr_common_sprites' } else { $gfxNames[$graphics.Gfx] }
    Copy-EnemySprite $sprite
    for ($index = 0; $index -lt $(if ($id -eq 0x8e) { 4 } else { 1 }); $index++) {
        $animation = Resolve-NpcAnimation $id $index
        if ([string]::IsNullOrWhiteSpace($animation)) { throw 'Octogon effect lost its complete animation/OAM stream.' }
        $rows.Add("$($id.ToString('x2'))`t$index`t$sprite`t$($graphics.TileBase)`t$($graphics.Palette)`t$([int](Get-EnemySpriteSourceGrayscaleInverted $sprite))`t$animation`tdata/ages/interactionAnimations.s:interaction$($id.ToString('x2'))Animations")
    }
}
Write-GeneratedTable((Join-Path $destination 'effects/octogon_interactions.tsv'),$rows)
}
