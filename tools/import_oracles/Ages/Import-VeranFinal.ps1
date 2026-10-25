$source='object_code/ages/enemies/veranFinalForm.s'
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# profile`tvalues`tsource")
function Read-VeranFinalTable([string]$path,[string]$scope,[string]$label,[int]$count) {
    $inside=$false;$reading=$false;$values=[Collections.Generic.List[int]]::new()
    foreach($node in Read-AssemblyNodes (Join-Path $Disassembly $path)) {
        if($node.Kind -eq 'Label') {
            if($reading){break}
            if($node.Name -eq $scope){$inside=$true}
            elseif($inside -and $node.Name -eq $label){$reading=$true}
        }
        elseif($reading -and $node.Kind -eq 'Data' -and $node.Name -eq '.db') {
            foreach($operand in $node.Operands){$values.Add((Convert-AssemblyInteger $operand))}
        }
    }
    if($values.Count -ne $count){throw "${path}:${scope}${label}: expected $count source bytes, got $($values.Count)."}
    return $values.ToArray()
}
foreach($spec in @(@('transform-probability','veranFinal_turtleForm_state5','@transformProbabilities',5),
    @('spider-counters','veranFinal_spiderForm_initState2','@counter1Vals',4),
    @('spider-angles','veranFinal_spiderForm_decideAngle','@angles',32))) {
    $values=@(Read-VeranFinalTable $source $spec[1] $spec[2] $spec[3])
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${source}:$($spec[1])$($spec[2])")
}
foreach($spec in @(@('bee-corners','veranFinal_beeForm_screenCornerEntrances',16),
    @('bee-counters','veranFinal_beeForm_counter2Vals',2),@('bee-targets','veranFinal_beeForm_targetPositions',32))) {
    $values=@(Read-AssemblyLiteralValues (Join-Path $Disassembly $source) $spec[1])
    if($values.Count -ne $spec[2]){throw "${source}:$($spec[1]): malformed table."}
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${source}:$($spec[1])")
}
$sequence=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly $source) '@transformSequence' 'dbrev')
if($sequence.Count -ne 1 -or $sequence[0].Operands.Count -ne 4){throw 'veranFinalForm.s:@transformSequence: malformed dbrev.'}
$values=@(foreach($operand in $sequence[0].Operands) {
    $bits=(Convert-AssemblyInteger $operand);$reversed=0
    for($bit=0;$bit -lt 8;$bit++){$reversed=($reversed -shl 1) -bor (($bits -shr $bit) -band 1)}
    $reversed
})
$rows.Add("transform-sequence`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${source}:@transformSequence")
$effects=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
foreach($mode in @(6,9,0x1f,0x6a,0x6b,0x6c,0x7c)) {
    $rows.Add("collision-$($mode.ToString('x2'))`t$(($effects[($mode*32)..($mode*32+31)]|ForEach-Object {$_.ToString('x2')}) -join ',')`tdata/ages/objectCollisionTable.s")
}
foreach($kind in @('enemy','part')) {
    $active=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly "data/ages/${kind}ActiveCollisions.s") "${kind}ActiveCollisions" 'dbrev')
    foreach($id in $(if($kind -eq 'enemy'){@(2,0x16,0x1f)}else{@(0x56,0x58)})) {
        $bits=($active[$id].Operands -join '').Replace('%','')
        if($bits -notmatch '^[01]{32}$'){throw "Veran final: malformed $kind `$${id} active collision mask."}
        $rows.Add("active-$kind-$($id.ToString('x2'))`t$(($bits.ToCharArray()|ForEach-Object {'0'+$_}) -join ',')`tdata/ages/${kind}ActiveCollisions.s")
    }
}
$definitions=[Collections.Generic.List[string]]::new()
$definitions.Add("# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64")
foreach($id in @(2,0x1f)) {
    $def=Get-EnemyDefinition $id
    $sprites=if($id -eq 2){@(0xd7..0xda|ForEach-Object {$gfxNames[$_]})}else{@($gfxNames[$def.Gfx])}
    foreach($sprite in $sprites){Copy-EnemySprite $sprite}
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($def.Animations -join "`n"))
    $definitions.Add("$($id.ToString('x2'))`t00`t$($sprites -join ',')`t$($def.TileBase)`t$($def.Palette)`t1`t$($def.RadiusY)`t$($def.RadiusX)`t$($def.Damage)`t$($def.Health)`t$encoded")
}
$path=Join-Path $Disassembly 'data/ages/partAnimations.s'
$tables=Read-AssemblyDwTables $path 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers=Read-AssemblyDwTables $path 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$animations=Read-AssemblyAnimationDefinitions $path 'partAnimation[0-9a-f]+(?:Loop)?' $false
$partData=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partData.s') 'partData' '.db')
foreach($id in @(0x56,0x57,0x58)) {
    $hex=$id.ToString('x2');$data=@($partData[$id].Operands|ForEach-Object {Convert-AssemblyInteger $_})
    $rows.Add("part-$hex`t$(($data|ForEach-Object {$_.ToString('x2')}) -join ',')`tdata/ages/partData.s")
    if($id -eq 0x57){continue} # Acid pools change terrain; their part has no OAM.
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
foreach($spec in @(@('acid-cardinals','@table_7d98'),@('acid-diagonals','@table_7da9'))) {
    $values=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/ages/parts/veranAcidPool.s') $spec[1])
    if($values.Count -ne 4){throw "veranAcidPool.s:$($spec[1]): malformed offset table."}
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`tobject_code/ages/parts/veranAcidPool.s:$($spec[1])")
}
$values=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/ages/enemies/veranChildBee.s') '@angleVals')
if($values.Count -ne 3){throw 'veranChildBee.s:@angleVals: malformed angles.'}
$rows.Add("child-angles`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`tobject_code/ages/enemies/veranChildBee.s:@angleVals")
$values=@(Read-VeranFinalTable 'object_code/ages/interactions/miscPuzzles.s' 'miscPuzzles_subid21' '@explosionPositions' 16)
$rows.Add("death-explosions`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`tobject_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid21@explosionPositions")
Write-GeneratedTable((Join-Path $destination 'objects/veran_final_tables.tsv'),$rows)
Write-GeneratedTable((Join-Path $destination 'objects/veran_final_actors.tsv'),$definitions)
Write-GeneratedBytes((Join-Path $destination 'objects/veran_final_palettes.bin'),(Read-PaletteBytes 'paletteData4978' 8))
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# text-id`tmessage-base64`tsource")
foreach($id in @(0x5614,0x5615,0x5616)) {
    $message=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/veran_final_texts.tsv'),$rows)
