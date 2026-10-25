# PART $59 retains its ordered room placements and four terminated routes.
$path=Join-Path $Disassembly 'object_code/ages/parts/blackTowerMovingFlames.s'
$source=Read-ImportText $path
if ($source -notmatch '(?ms)^@state0:.*?ld l,\$d0\s+ld \(hl\),\$14' -or
    $source -notmatch '(?ms)^@state1:.*?xor \$03.*?SND_LIGHTTORCH.*?objectSetVisible83' -or
    $source -notmatch '(?ms)^@state2:.*?ld \(hl\),\$14.*?^@state3:.*?blackTower_getMovingFlamesNextTileCoords.*?ld \(hl\),\$10' -or
    $source -notmatch '(?ms)^@state4:.*?objectGetRelativeAngleWithTempVars.*?objectApplySpeed.*?^@state5:.*?objectCreatePuff.*?partDelete') {
    throw 'blackTowerMovingFlames.s: PART $59 state, speed, delay, visibility or deletion contract changed.'
}
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# subid`tdelay`troute`tsource")
$delays=@(Read-AssemblyDataDirectives $path '@table_7ebd' '.db' | ForEach-Object { $_.Operands } | ForEach-Object { Convert-AssemblyInteger $_ })
$routePath=Join-Path $Disassembly 'object_code/ages/enemies/kingMoblinMinionMain.s'
$labels=@('@leftFlame','@topFlame','@rightFlame','@bottomFlame')
if (($delays -join ',') -ne '1,20,40,60') { throw 'PART $59: expected four ordered ignition delays.' }
for ($index=0;$index -lt 4;$index++) {
    $route=@(Read-AssemblyDataDirectives $routePath $labels[$index] '.db' | ForEach-Object { $_.Operands } | ForEach-Object { Convert-AssemblyInteger $_ })
    if ($route.Count -ne @(13,11,12,18)[$index] -or $route[-1] -ne 0 -or @($route | Where-Object {$_ -eq 0}).Count -ne 1) { throw "PART `$59:$index route boundary changed." }
    $rows.Add("$($index.ToString('x2'))`t$($delays[$index].ToString('x2'))`t$(($route | ForEach-Object {$_.ToString('x2')}) -join ',')`tkingMoblinMinionMain.s:$($labels[$index]);blackTowerMovingFlames.s:@table_7ebd")
}
Write-GeneratedTable((Join-Path $destination 'objects/black_tower_flames.tsv'),$rows)
$bytes=@((@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partData.s') 'partData' '.db')[0x59]).Operands | ForEach-Object {Convert-AssemblyInteger $_})
$active=Read-ImportText (Join-Path $Disassembly 'data/ages/partActiveCollisions.s')
if (($bytes -join ',') -ne '0,134,85,252,64,8,10,0' -or $active -notmatch '(?m)^\s*dbrev %10000000 %00000000 %00000000 %00000000 ; 0x59\s*$') { throw 'PART $59 graphics, Link-only collision, radii or damage changed.' }
$animationPath=Join-Path $Disassembly 'data/ages/partAnimations.s'
$tables=Read-AssemblyDwTables $animationPath 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers=Read-AssemblyDwTables $animationPath 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$definitions=Read-AssemblyAnimationDefinitions $animationPath 'partAnimation[0-9a-f]+(?:Loop)?' $false
$definition=$definitions[$tables['part59Animations'][0]]
$frames=@($definition.Frames | ForEach-Object { "$($_.Duration),$($_.Parameter)@$(Resolve-Oam $partOamSource $pointers['part59OamDataPointers'][[int]($_.PointerOffset/2)])" })
if ($frames.Count -ne 6 -or $definition.LoopStart -ne 0) {throw 'PART $59 requires its six-frame looping animation.'}
Copy-EnemySprite 'spr_common_sprites'
Write-GeneratedTable((Join-Path $destination 'effects/black_tower_flame.tsv'),@(
    "# sprite`ttile-base`tpalette`tradius`tdamage-quarters`tsource-grayscale-inverted`tanimation`tsource",
    "spr_common_sprites`t$($bytes[5])`t$($bytes[6] -band 7)`t$($bytes[2] -band 15)`t$((256-$bytes[3])/2)`t$([int](Get-EnemySpriteSourceGrayscaleInverted 'spr_common_sprites'))`t$($frames -join '|')`tdata/ages/partData.s:part59;data/ages/partAnimations.s:part59Animations"
))
