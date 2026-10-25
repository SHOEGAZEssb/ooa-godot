# INTERAC $c6 -> $d7, including the native sparkle children. Keep the
# source's tile/index operand order (the original comment reverses it).
$seedPath = Join-Path $Disassembly 'object_code/ages/interactions/makuSeedAndEssences.s'
$doorPath = Join-Path $Disassembly 'object_code/ages/interactions/blackTowerDoorHandler.s'
$seedSource = Read-ImportText $seedPath
$doorSource = Read-ImportText $doorPath
if ($doorSource -notmatch '(?ms)^@state0:.*?ROOMFLAG_40.*?wRoomLayout\+\$47.*?TREASURE_MAKU_SEED.*?clearAllItemsAndPutLinkOnGround.*?resetLinkInvincibility.*?ld a,\$70.*?PALH_ab' -or
    $doorSource -notmatch '(?ms)^@noMakuSeed:.*?\$44.*?\$47.*?\$4a.*?wDisableWarps' -or
    $doorSource -notmatch '(?ms)^@state1:.*?ld \(hl\),30.*?^@state2:.*?INTERAC_MAKU_SEED_AND_ESSENCES' -or
    $seedSource -notmatch '(?ms)^@state1:.*?LINK_ANIM_MODE_GETITEM2HAND.*?ld \(hl\),\$40.*?SPEED_80.*?^@state2:.*?ld \(hl\),120.*?\$58.*?\$78.*?SND_PIECE_OF_POWER' -or
    $seedSource -notmatch '(?ms)^@state3Substate0:.*?ld \(hl\),20.*?\$08.*?^@state3Substate1:.*?ld \(hl\),20.*?^@state3Substate2:.*?ld \(hl\),60.*?\$20' -or
    $seedSource -notmatch '(?ms)^@state4Substate1:.*?ld \(hl\),120.*?ld a,\$08.*?^@state4Substate3:.*?set 6,\(hl\).*?wRoomLayout\+\$47.*?\$44.*?checkIsLinkedGame') {
    throw 'Black Tower $c6/$d7 native initialization, counters, or completion changed.'
}
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# key`tvalues`tsource")
foreach ($spec in @(
    @('essence-spawns','@essenceSpawnerData',16),
    @('tiles','@tileReplacements',37),
    @('sparkle-offsets','@sparklePositionOffsets',16),
    @('heights','@zPositions',8)
)) {
    $values = @(Read-AssemblyDataDirectives $seedPath $spec[1] '.db' | ForEach-Object { $_.Operands } | ForEach-Object { Convert-AssemblyInteger $_ })
    if ($values.Count -ne $spec[2]) { throw "makuSeedAndEssences.s:$($spec[1]): incomplete table." }
    $rows.Add("$($spec[0])`t$(($values | ForEach-Object { $_.ToString('x2') }) -join ',')`tmakuSeedAndEssences.s:$($spec[1])")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_seed_tables.tsv'),$rows)
$rows.Clear()
$rows.Add("# id`tsubid`tsprite`ttile-base`tpalette`tanimation`tsource-offset`tsource")
foreach ($spec in @(@(0xd7,0),@(0xd7,1),@(0xd7,2),@(0xd7,3),@(0xd7,4),@(0xd7,5),@(0xd7,6),@(0xd7,7),@(0xd7,8),@(0x84,3),@(0x84,4))) {
    $id=$spec[0]; $subid=$spec[1]; $graphic=$interactionGraphics["${id}:${subid}"]
    if ($null -eq $graphic) { throw "Black Tower missing interaction graphics `$${id}:`$${subid}." }
    $sprite=$gfxNames[$graphic.Gfx]; Copy-EnemySprite $sprite
    $offset=0
    if ($id -eq 0x84 -and $subid -eq 4) {
        $header=Read-ImportText (Join-Path $Disassembly 'data/ages/objectGfxHeaders.s')
        $match=[regex]::Match($header,'(?m)/\* \$3a \*/ m_ObjectGfxHeader spr_link, \$00, \$(?<offset>[0-9a-f]{4})')
        if (-not $match.Success -or $graphic.Gfx -ne 0x3a) { throw 'Black Tower sparkle $84:$04 source graphics slice is missing.' }
        $offset=[Convert]::ToInt32($match.Groups['offset'].Value,16)
    }
    $animation=Resolve-NpcAnimation $id $graphic.DefaultAnimation
    if (-not $animation) { throw "Black Tower missing animation `$${id}:`$${subid}." }
    $rows.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$animation`t$($offset.ToString('x4'))`tdata/ages/interactionData.s;data/ages/objectGfxHeaders.s")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_seed_visuals.tsv'),$rows)
Write-GeneratedBytes((Join-Path $destination 'cutscenes/black_tower_seed_palette.bin'),(Read-PaletteBytes 'paletteData4178' 4))
