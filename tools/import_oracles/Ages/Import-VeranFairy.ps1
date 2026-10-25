$source='object_code/ages/enemies/veranFairy.s'
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# profile`tvalues`tsource")
$speeds=@{};$offset=0
foreach($m in [regex]::Matches((Read-ImportText (Join-Path $Disassembly 'constants/common/objectSpeeds.s')),'(?m)^\s*(SPEED_[0-9a-f]+)\s+dsb (\d+)')) {
    $speeds[$m.Groups[1].Value]=$offset;$offset+=[int]$m.Groups[2].Value
}
foreach($spec in @(@('pillars','@pillarPositions',12),@('mimics','@mimicPositions',4),@('explosions','@explosionPositions',8),
    @('pattern-0','@pattern0',9),@('pattern-1','@pattern1',11),@('pattern-2','@pattern2',11),@('pattern-3','@pattern3',11),
    @('attacks','veranFairy_attackTable',24),@('speeds','veranFairy_speedTable',3),@('wrap-keys','@data1',21),@('wrap-positions','@data2',16))) {
    $values=@(Read-AssemblyDataDirectives (Join-Path $Disassembly $source) $spec[1] '.db'|ForEach-Object {$_.Operands}|ForEach-Object {
        if($speeds.ContainsKey($_)){$speeds[$_]}else{Convert-AssemblyInteger $_}
    })
    if($values.Count -ne $spec[2]){throw "${source}:$($spec[1]): unexpected table boundary."}
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${source}:$($spec[1])")
}
$effects=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
$boundary=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/parts/commonCode.s') 'partCommon_anglePositionOffsets')
if($boundary.Count -ne 16){throw 'Veran fairy: malformed partCommon_anglePositionOffsets.'}
$rows.Add("part-boundaries`t$(($boundary|ForEach-Object {$_.ToString('x2')}) -join ',')`tobject_code/common/parts/commonCode.s:partCommon_anglePositionOffsets")
if($effects.Count -ne 4000){throw 'Veran fairy: incomplete collision table.'}
foreach($mode in @(6,0x0d,0x39,0x77)) {
    $rows.Add("collision-$($mode.ToString('x2'))`t$(($effects[($mode*32)..($mode*32+31)]|ForEach-Object {$_.ToString('x2')}) -join ',')`tdata/ages/objectCollisionTable.s")
}
foreach($kind in @('enemy','part')) {
    $active=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly "data/ages/${kind}ActiveCollisions.s") "${kind}ActiveCollisions" 'dbrev')
    foreach($id in $(if($kind -eq 'enemy'){@(6,0x64)}else{@(0x2d,0x2f)})) {
        $bits=($active[$id].Operands -join '').Replace('%','')
        if($bits -notmatch '^[01]{32}$'){throw "Veran fairy: malformed $kind `$${id} active collisions."}
        $rows.Add("active-$kind-$($id.ToString('x2'))`t$(($bits.ToCharArray()|ForEach-Object {'0'+$_}) -join ',')`tdata/ages/${kind}ActiveCollisions.s")
    }
}
$definitions=[Collections.Generic.List[string]]::new()
$definitions.Add("# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64")
foreach($id in @(6,0x64)) {
    $def=Get-EnemyDefinition $id
    $sprites=if($id -eq 6){@(0xd4..0xd6|ForEach-Object {$gfxNames[$_]})}else{@($gfxNames[$def.Gfx])}
    foreach($sprite in $sprites){Copy-EnemySprite $sprite}
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($def.Animations -join "`n"))
    $definitions.Add("$($id.ToString('x2'))`t00`t$($sprites -join ',')`t$($def.TileBase)`t$($def.Palette)`t1`t$($def.RadiusY)`t$($def.RadiusX)`t$($def.Damage)`t$($def.Health)`t$encoded")
}
$path=Join-Path $Disassembly 'data/ages/partAnimations.s'
$tables=Read-AssemblyDwTables $path 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers=Read-AssemblyDwTables $path 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$animations=Read-AssemblyAnimationDefinitions $path 'partAnimation[0-9a-f]+(?:Loop)?' $false
$partData=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partData.s') 'partData' '.db')
foreach($id in @(0x2d,0x2f)) {
    $hex=$id.ToString('x2');$data=@($partData[$id].Operands|ForEach-Object {Convert-AssemblyInteger $_})
    $rows.Add("part-$hex`t$(($data|ForEach-Object {$_.ToString('x2')}) -join ',')`tdata/ages/partData.s")
    $sprite=$gfxNames[$data[0]];Copy-EnemySprite $sprite
    $sequences=@(foreach($label in $tables["part${hex}Animations"]) {
        $def=$animations[$label]
        $frames=@(foreach($frame in $def.Frames) {
            $oam=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partOamData.s') $pointers["part${hex}OamDataPointers"][$frame.PointerOffset/2] '.db')
            $cells=@($oam|Select-Object -Skip 1|ForEach-Object {($_.Operands|ForEach-Object {Convert-AssemblyInteger $_}) -join ','})
            if($cells.Count -ne (Convert-AssemblyInteger $oam[0].Operands[0])){throw "PART `$${hex}: malformed OAM."}
            "$($frame.Duration),$($frame.Parameter)@$($cells -join ';')"
        })
        "$($frames -join '|')~$($def.LoopStart)"
    })
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sequences -join "`n"))
    $definitions.Add("$hex`t00`t$sprite`t$($data[5])`t$($data[6] -band 7)`t1`t$($data[2] -shr 4)`t$($data[2] -band 15)`t$((256-$data[3])/2)`t1`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'objects/veran_fairy_tables.tsv'),$rows)
Write-GeneratedTable((Join-Path $destination 'objects/veran_fairy_actors.tsv'),$definitions)
Write-GeneratedBytes((Join-Path $destination 'objects/link_mimic_palette.bin'),(Read-PaletteBytes 'paletteData4998' 4))
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# text-id`tmessage-base64`tsource")
foreach($id in @(0x560e,0x560f,0x5610,0x5612)) {
    $message=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/veran_fairy_texts.tsv'),$rows)
