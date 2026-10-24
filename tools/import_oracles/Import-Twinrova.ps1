$twinSource='object_code/common/enemies/twinrova.s'
$twinsEntry=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/mainData.s') 'group5Mapf5ObjectData' 'obj_BeforeEvent')
$twinsEnemy=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/enemyData.s') 'group5Mapf5BeforeEventObjectData' 'obj_RandomEnemy')
if($twinsEntry.Count -ne 1 -or $twinsEntry[0].Operands[0] -ne 'group5Mapf5BeforeEventObjectData' -or
    $twinsEnemy.Count -ne 1 -or ($twinsEnemy[0].Operands -join ' ') -ne '$20 $03 $00') {throw 'Twinrova before-event placement changed.'}
$entryValues=@($twinsEnemy[0].Operands|ForEach-Object{Convert-AssemblyInteger $_})
Write-GeneratedTable((Join-Path $destination 'objects/twinrova_placements.tsv'),@(
    "# group`troom`torder`tid`tsubid`tflags`tcount`tsource",
    "5`tf5`t0`t$($entryValues[1].ToString('x2'))`t$($entryValues[2].ToString('x2'))`t$($entryValues[0].ToString('x2'))`t$($entryValues[0] -shr 5)`tobjects/ages/enemyData.s:group5Mapf5BeforeEventObjectData"))
function Read-TwinrovaScopeTable([string]$scope,[string]$label) {
    $values=[Collections.Generic.List[int]]::new();$reading=$false
    foreach($node in Read-AssemblyNodes (Join-Path $Disassembly $twinSource)) {
        if($node.Kind -eq 'Label') {
            if($reading){break}
            if($node.EnclosingGlobalLabel -eq $scope -and $node.Name -eq $label){$reading=$true}
        } elseif($reading -and $node.Kind -eq 'Data' -and $node.Name -eq '.db') {
            foreach($operand in $node.Operands){$values.Add((Convert-AssemblyInteger $operand))}
        }
    }
    if(!$reading -or !$values.Count){throw "${twinSource}:${scope}${label}: missing source table."}
    return $values.ToArray()
}
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# profile`tvalues`tsource")
foreach($subid in 0..1) {
    foreach($pattern in 0..3) {
        $scope="twinrova_subid${subid}_targetPositions";$label="@pattern$pattern"
        $values=@(Read-TwinrovaScopeTable $scope $label)
        if(($values.Count -band 1) -ne 1 -or $values[-1] -ne 0){throw "${scope}${label}: malformed sentinel-terminated Y/X sequence."}
        $rows.Add("targets-$subid-$pattern`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${twinSource}:${scope}${label}")
    }
}
foreach($spec in @(@('entrance','twinrova_state8','@data',8),@('levitation','twinrova_updateZPosition','@levitationZPositions',4),
    @('attack-pattern','twinrova_subid0_stateB','@attackPattern',1))) {
    $values=@(Read-TwinrovaScopeTable $spec[1] $spec[2]);if($values.Count -ne $spec[3]){throw "Twinrova $($spec[0]): unexpected count."}
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {($_ -band 255).ToString('x2')}) -join ',')`t${twinSource}:$($spec[1])$($spec[2])")
}
$effects=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
$twinSource='object_code/common/enemies/mergedTwinrova.s'
foreach($spec in @(@('merged-targets','mergedTwinrova_chooseTargetPosition','@targetPositions',16),
    @('merged-attacks','mergedTwinrova_lavaRoom_stateD','@var03Vals',8),
    @('merged-bats','mergedTwinrova_lavaRoom_stateE','@keesePositions',8),
    @('merged-ice-low','mergedTwinrova_iceRoom_stateD','@positionData0',12),
    @('merged-ice-high','mergedTwinrova_iceRoom_stateD','@positionData1',8),
    @('merged-explosions','mergedTwinrova_deathCutscene','@explosionPositions',12))) {
    $values=@(Read-TwinrovaScopeTable $spec[1] $spec[2]);if($values.Count -ne $spec[3]){throw "Merged Twinrova $($spec[0]): unexpected source table size."}
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${twinSource}:$($spec[1])$($spec[2])")
}
$twinSource='object_code/common/enemies/twinrova.s'
foreach($mode in @(0x0a,0x08,0x09,0x7a,6,0x0e,0x41)) {
    $rows.Add("collision-$($mode.ToString('x2'))`t$(($effects[($mode*32)..($mode*32+31)]|ForEach-Object {$_.ToString('x2')}) -join ',')`tdata/ages/objectCollisionTable.s")
}
$boundary=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/parts/commonCode.s') 'partCommon_anglePositionOffsets')
if($boundary.Count -ne 16){throw 'Twinrova projectile native angle boundary table changed.'}
$rows.Add("part-boundaries`t$(($boundary|ForEach-Object {($_ -band 255).ToString('x2')}) -join ',')`tobject_code/common/parts/commonCode.s:partCommon_anglePositionOffsets")
foreach($kind in @('enemy','part')) {
    $active=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly "data/ages/${kind}ActiveCollisions.s") "${kind}ActiveCollisions" 'dbrev')
    foreach($id in $(if($kind -eq 'enemy'){@(1,3,0x16,0x5d,0x5e,0x7f)}else{@(0x4b,0x4c,0x4d,0x4e)})) {
        $bits=($active[$id].Operands -join '').Replace('%','')
        if($bits -notmatch '^[01]{32}$'){throw "Twinrova malformed $kind active mask `$${id}."}
        $rows.Add("active-$kind-$($id.ToString('x2'))`t$(($bits.ToCharArray()|ForEach-Object {'0'+$_}) -join ',')`tdata/ages/${kind}ActiveCollisions.s")
    }
}
$definitions=[Collections.Generic.List[string]]::new()
$definitions.Add("# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64")
$def=Get-EnemyDefinition 3
# The source header $33 chains merged art, then all three split-twin sheets.
$sprites=@(0x33..0x36|ForEach-Object {$gfxNames[$_]})
foreach($sprite in $sprites){Copy-EnemySprite $sprite}
$encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($def.Animations -join "`n"))
$definitions.Add("03`t00`t$($sprites -join ',')`t$($def.TileBase)`t$($def.Palette)`t1`t$($def.RadiusY)`t$($def.RadiusX)`t$($def.Damage)`t$($def.Health)`t$encoded")
$merged=Get-EnemyDefinition 1
if($merged.Gfx -ne 0xdb -or $merged.Collision -ne 8){throw 'mergedTwinrova.s: enemy $01 native graphics/collision definition changed.'}
$mergedSprites=@(0xdb..0xdf|ForEach-Object {$gfxNames[$_]})
foreach($sprite in $mergedSprites){Copy-EnemySprite $sprite}
$encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($merged.Animations -join "`n"))
$definitions.Add("01`t00`t$($mergedSprites -join ',')`t$($merged.TileBase)`t$($merged.Palette)`t1`t$($merged.RadiusY)`t$($merged.RadiusX)`t$($merged.Damage)`t$($merged.Health)`t$encoded")
foreach($id in @(0x5d,0x5e)) {
    $def=Get-EnemyDefinition $id
    $sprite=$gfxNames[$def.Gfx];Copy-EnemySprite $sprite
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($def.Animations -join "`n"))
    $definitions.Add("$($id.ToString('x2'))`t00`t$sprite`t$($def.TileBase)`t$($def.Palette)`t$([int](Get-EnemySpriteSourceGrayscaleInverted $sprite))`t$($def.RadiusY)`t$($def.RadiusX)`t$($def.Damage)`t$($def.Health)`t$encoded")
}
$path=Join-Path $Disassembly 'data/ages/partAnimations.s'
$tables=Read-AssemblyDwTables $path 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers=Read-AssemblyDwTables $path 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$animations=Read-AssemblyAnimationDefinitions $path 'partAnimation[0-9a-f]+(?:Loop)?' $false
$partData=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partData.s') 'partData' '.db')
foreach($id in @(0x4b,0x4c,0x4d,0x4e)) {
    $hex=$id.ToString('x2');$data=@($partData[$id].Operands|ForEach-Object {Convert-AssemblyInteger $_})
    $rows.Add("part-$hex`t$(($data|ForEach-Object {$_.ToString('x2')}) -join ',')`tdata/ages/partData.s")
    $sprite=$gfxNames[$data[0]];Copy-EnemySprite $sprite
    $sequences=@(foreach($label in $tables["part${hex}Animations"]) {
        $definition=$animations[$label]
        $frames=@(foreach($frame in $definition.Frames) {
            $oam=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partOamData.s') $pointers["part${hex}OamDataPointers"][$frame.PointerOffset/2] '.db')
            $cells=@($oam|Select-Object -Skip 1|ForEach-Object {($_.Operands|ForEach-Object {Convert-AssemblyInteger $_}) -join ','})
            if($cells.Count -ne (Convert-AssemblyInteger $oam[0].Operands[0])){throw "PART `$${hex}: malformed OAM."}
            "$($frame.Duration),$($frame.Parameter)@$($cells -join ';')"
        })
        "$($frames -join '|')~$($definition.LoopStart)"
    })
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sequences -join "`n"))
    $definitions.Add("$hex`t00`t$sprite`t$($data[5])`t$($data[6] -band 7)`t1`t$($data[2] -shr 4)`t$($data[2] -band 15)`t$((256-$data[3])/2)`t1`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'objects/twinrova_tables.tsv'),$rows)
Write-GeneratedTable((Join-Path $destination 'objects/twinrova_actors.tsv'),$definitions)
$snow=$interactionGraphics['9:0']
if(!$snow -or $snow.DefaultAnimation -ne 0){throw 'INTERAC_SNOWDEBRIS $09:$00 requires its source graphics.'}
$sprite=if($snow.Gfx -eq 0){'spr_common_sprites'}else{$gfxNames[$snow.Gfx]};Copy-EnemySprite $sprite
$snowAnimation=Resolve-NpcAnimation 9 0
Write-GeneratedTable((Join-Path $destination 'objects/twinrova_snow_debris.tsv'),@(
    "# sprite`ttile-base`tpalette`tinverted`tanimation`tsource",
    "$sprite`t$($snow.TileBase)`t$($snow.Palette)`t$([int](Get-EnemySpriteSourceGrayscaleInverted $sprite))`t$snowAnimation`tbreakTileDebris.s:interactionCode09"))
$rows.Clear();$rows.Add("# text-id`tmessage-base64`tsource")
foreach($id in @(0x2f04,0x2f05,0x2f09,0x2f0a,0x2f0b,0x2f0c)) {
    $message=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/twinrova_texts.tsv'),$rows)

$sceneSource=Read-ImportText (Join-Path $Disassembly 'code/bank3Cutscenes.s')
if($sceneSource -notmatch '(?ms)^twinrovaCutscene_state0:.*?ld a,\$04.*?fadeoutToWhiteWithDelay.*?wTmpcbb3.*?ld b,\$10' -or
    $sceneSource -notmatch '(?ms)^twinrovaCutscene_state3:.*?ld \(hl\),180.*?twinrovaCutscene_deleteAllInteractionsExceptFlames.*?twinrovaCutscene_loadAngryFlames' -or
    $sceneSource -notmatch '(?ms)^cutscene18_state5:.*?ld a,\$f5.*?ENEMY_TWINROVA.*?set 7,\(hl\).*?ld \(hl\),\$03.*?ld \(hl\),\$78.*?ld \(hl\),\$78.*?ld a,\$02.*?fadeinFromWhiteWithDelay') {
    throw 'bank3Cutscenes.s: Twinrova flame cutscene load, timers or fight restart contract changed.'
}
$rows.Clear();$rows.Add("# scene`torder`tid`tsubid`ty`tx`tsource")
foreach($spec in @(@('calm','objectData4022',4),@('angry','objectData402f',3))) {
    $order=0
    foreach($node in Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/extraData1.s') $spec[1] 'obj_Interaction') {
        $values=@($node.Operands|ForEach-Object {Convert-AssemblyInteger $_})
        if($values.Count -notin @(2,4) -or $values[0] -notin @(0xad,0xa9)){throw "Twinrova $($spec[1]) has an unsupported actor."}
        $y=if($values.Count -eq 4){$values[2]}else{0};$x=if($values.Count -eq 4){$values[3]}else{0}
        $rows.Add("$($spec[0])`t$order`t$($values[0].ToString('x2'))`t$($values[1].ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`textraData1.s:$($spec[1])")
        $order++
    }
    if($order -ne $spec[2]){throw "Twinrova $($spec[1]) changed actor count."}
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/twinrova_flame_actors.tsv'),$rows)

# tileReplacement_group5Mapf5 is a transient native layout operation, not a
# persistent room variant. Preserve the raw 16-byte row stride and padding.
$tileSource=Read-ImportText (Join-Path $Disassembly 'code/ages/roomSpecificTileChanges.s')
$headers=Read-ImportText (Join-Path $Disassembly 'data/ages/gfxHeaders.s')
if($tileSource -notmatch '(?ms)^tileReplacement_group5Mapf5:.*?wTwinrovaTileReplacementMode.*?@iceTiles:\s*\.db \$11, LARGE_ROOM_HEIGHT-2, LARGE_ROOM_WIDTH-2, \$8a' -or
    $tileSource -notmatch '(?ms)@seizureTiles:\s*\.db \$00, LARGE_ROOM_HEIGHT, LARGE_ROOM_WIDTH, \$aa') {throw 'Room $5:$f5 native Twinrova rectangles changed.'}
foreach($kind in @('lava','normal')) {
    $symbol="GFXH_TWINROVA_$($kind.ToUpperInvariant())_LAYOUT"
    $name="oth_twinrova_${kind}_layout"
    if($headers -notmatch "(?ms)^m_GfxHeaderStart .*?, $symbol\s*m_GfxHeader $name, wRoomLayout\s*m_GfxHeaderEnd"){throw "$symbol no longer loads $name to wRoomLayout."}
    $layout=[IO.File]::ReadAllBytes((Join-Path $Disassembly "gfx_compressible/common/$name.bin"))
    if($layout.Length -ne 176){throw "$symbol must contain eleven 16-byte storage rows."}
    Write-GeneratedBytes((Join-Path $destination "objects/twinrova_${kind}_layout.bin"),$layout)
}
Write-GeneratedTable((Join-Path $destination 'objects/twinrova_arena_rectangles.tsv'),@(
    "# mode`tstart`theight`twidth`ttile`tsource",
    "02`t11`t9`t13`t8a`troomSpecificTileChanges.s:tileReplacement_group5Mapf5@iceTiles",
    "04`t00`t11`t15`taa`troomSpecificTileChanges.s:tileReplacement_group5Mapf5@seizureTiles"))
