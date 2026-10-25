$visuals=[Collections.Generic.List[string]]::new();$visuals.Add("# id`tsubid`tsprite`ttile-base`tpalette`tanimation`tsource")
$animations=[Collections.Generic.List[string]]::new();$animations.Add("# id`tindex`tanimation`tsource")
foreach($spec in @(@(0x2c,0),@(0x92,1),@(0x92,2))) {
    $id=[int]$spec[0];$subid=[int]$spec[1];$gfx=$interactionGraphics["${id}:$subid"]
    if($null -eq $gfx){$gfx=$interactionGraphics["${id}:0"]}
    if($null -eq $gfx){throw "Black Tower escape: missing INTERAC `$${id}:`$subid graphics."}
    $sprite=$gfxNames[$gfx.Gfx];Copy-EnemySprite $sprite
    $visuals.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$sprite`t$($gfx.TileBase)`t$($gfx.Palette)`t$(Resolve-NpcAnimation $id $gfx.DefaultAnimation)`tdata/ages/interactionData.s")
    if($id -eq 0x2c) {
        foreach($index in 0..1){$animations.Add("2c`t$index`t$(Resolve-NpcAnimation $id $index)`tdata/ages/interactionAnimations.s")}
    }
}
Write-GeneratedTable((Join-Path $destination 'objects/falling_rock_visuals.tsv'),@($visuals[0])+@($visuals|Where-Object {$_.StartsWith("92`t")}))
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_escape_visuals.tsv'),@($visuals[0])+@($visuals|Where-Object {$_.StartsWith("2c`t")}))
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_escape_animations.tsv'),$animations)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# profile`tvalues`tsource")
$path=Join-Path $Disassembly 'object_code/ages/interactions/fallingRock.s'
foreach($spec in @(@('rock-positions-0','@positionList1'),@('rock-positions-1','@positionList2'))) {
    $values=@(Read-AssemblyLiteralValues $path $spec[1])
    if($values.Count -ne 32){throw "fallingRock.s:$($spec[1]): malformed random position table."}
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`tobject_code/ages/interactions/fallingRock.s:$($spec[1])")
}
$values=[Collections.Generic.List[int]]::new();$scope=$false;$reading=$false
foreach($node in Read-AssemblyNodes $path) {
    if($node.Kind -eq 'Label') {
        if($reading){break}
        if($node.Name -eq 'fallingRock_subid02'){$scope=$true}
        elseif($scope -and $node.Name -eq '@angles'){$reading=$true}
    }
    elseif($reading -and $node.Kind -eq 'Data' -and $node.Name -eq '.db') {
        foreach($operand in $node.Operands){$values.Add((Convert-AssemblyInteger $operand))}
    }
}
if($values.Count -ne 24){throw 'fallingRock.s:fallingRock_subid02@angles: malformed debris-angle table.'}
$rows.Add("rock-angles`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`tobject_code/ages/interactions/fallingRock.s:fallingRock_subid02@angles")
Write-GeneratedTable((Join-Path $destination 'objects/falling_rock_tables.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# profile`tvalues`tsource")
$native=Read-ImportText (Join-Path $Disassembly 'code/ages/cutscenes/miscCutscenes.s')
$warp=[regex]::Match($native,'(?ms)^func_03_7cb7:.*?^@warpDest:\s*m_HardcodedWarpA ROOM_AGES_(?<room>[0-9a-f]{3}),\s*\$(?<transition>[0-9a-f]{2}),\s*\$(?<position>[0-9a-f]{2}),\s*\$(?<request>[0-9a-f]{2})')
if(!$warp.Success){throw 'miscCutscenes.s:func_03_7cb7: missing final warp.'}
$room=[Convert]::ToInt32($warp.Groups['room'].Value,16)
$rows.Add("warp`t$((0x80 -bor ($room -shr 8)).ToString('x2')),$(($room -band 255).ToString('x2')),$($warp.Groups['transition'].Value),$($warp.Groups['position'].Value),$($warp.Groups['request'].Value)`tcode/ages/cutscenes/miscCutscenes.s:func_03_7cb7@warpDest")
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_escape_tables.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# id`tsubid`ty`tx`tsource")
$placements=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/extraData4.s') 'objectData7e85' 'obj_Interaction')
if($placements.Count -ne 2){throw 'objectData7e85: expected two ordered escape actors.'}
foreach($placement in $placements) {
    if($placement.Operands.Count -ne 4){throw 'objectData7e85: malformed escape actor.'}
    $rows.Add("$(($placement.Operands|ForEach-Object {(Convert-AssemblyInteger $_).ToString('x2')}) -join "`t")`tobjects/ages/extraData4.s:objectData7e85")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_escape_placements.tsv'),$rows)
