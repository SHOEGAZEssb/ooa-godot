$source='object_code/common/enemies/ganon.s'
function Read-GanonTable([string]$path,[string]$scope,[string]$label) {
    $values=[Collections.Generic.List[int]]::new();$reading=$false
    foreach($node in Read-AssemblyNodes (Join-Path $Disassembly $path)) {
        if($node.Kind -eq 'Label') {
            if($reading){break}
            if($node.EnclosingGlobalLabel -eq $scope -and $node.Name -eq $label){$reading=$true}
        } elseif($reading -and $node.Kind -eq 'Data' -and $node.Name -eq '.db') {
            foreach($operand in $node.Operands){$values.Add((Convert-AssemblyInteger $operand))}
        }
    }
    if(!$reading -or !$values.Count){throw "${path}:${scope}${label}: missing source table."}
    return $values.ToArray()
}
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# profile`tvalues`tsource")
foreach($index in 0..7) {
    $values=@(Read-GanonTable $source 'ganon_decideNextMove' "@choice$index")
    if($values[-1] -ne 0){throw "ganon.s choice${index}: missing terminator."}
    $rows.Add("attack-$index`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${source}:ganon_decideNextMove@choice$index")
}
foreach($spec in @(@('teleport-positions','ganon_decideTeleportLocationAndCounter','@teleportTargetTable',16),
    @('teleport-delays','ganon_decideTeleportLocationAndCounter','@counter1Vals',16),
    @('reverse-delays','ganon_stateC_substate6','@counter2Vals',8))) {
    $values=@(Read-GanonTable $source $spec[1] $spec[2]);if($values.Count -ne $spec[3]){throw "ganon.s $($spec[0]): source size changed."}
    $rows.Add("$($spec[0])`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`t${source}:$($spec[1])$($spec[2])")
}
$values=@(Read-GanonTable 'object_code/common/enemies/ganonRevivalCutscene.s' 'ganonRevivalCutscene_spawnShadow' '@shadowVariables')
if($values.Count -ne 24){throw 'ganonRevivalCutscene.s: shadow table size changed.'}
$rows.Add("shadows`t$(($values|ForEach-Object {$_.ToString('x2')}) -join ',')`tganonRevivalCutscene.s:@shadowVariables")
$bank0=Read-ImportText (Join-Path $Disassembly 'code/bank0.s')
if($bank0 -notmatch '(?s)^\.ORGA \$0000\s*; rst_jumpTable\s*add a\s*pop hl\s*add l\s*ld l,a\s*jr nc,\+') {throw 'Ganon split children: null relatedObj1 state aliases changed ROM $0004.'}
# Four one-byte instructions precede JR NC (opcode $30) at ROM $0004.
$rows.Add("null-related-state`t30`tcode/bank0.s:rst_jumpTable+4 (JR NC)")
$effects=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
foreach($mode in @(0x0b,0x7b,6)) {
    $rows.Add("collision-$($mode.ToString('x2'))`t$(($effects[($mode*32)..($mode*32+31)]|ForEach-Object {$_.ToString('x2')}) -join ',')`tdata/ages/objectCollisionTable.s")
}
$boundary=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/parts/commonCode.s') 'partCommon_anglePositionOffsets')
$rows.Add("part-boundaries`t$(($boundary|ForEach-Object {($_ -band 255).ToString('x2')}) -join ',')`tcommon/parts/commonCode.s:partCommon_anglePositionOffsets")
$trident=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/parts/ganonTrident.s') 'table_5b5b')
if($trident.Count -ne 12){throw 'ganonTrident.s: native radius/offset table changed.'}
$rows.Add("trident`t$(($trident|ForEach-Object {$_.ToString('x2')}) -join ',')`tganonTrident.s:table_5b5b")
$partMasks=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/partActiveCollisions.s') 'partActiveCollisions' 'dbrev')
foreach($id in @(0x50,0x51,0x52)) {
    $bits=($partMasks[$id].Operands -join '').Replace('%','');if($bits -notmatch '^[01]{32}$'){throw 'Ganon PART active mask changed.'}
    $rows.Add("active-part-$($id.ToString('x2'))`t$(($bits.ToCharArray()|ForEach-Object {'0'+$_}) -join ',')`tdata/ages/partActiveCollisions.s")
}
$active=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/enemyActiveCollisions.s') 'enemyActiveCollisions' 'dbrev')
$bits=($active[4].Operands -join '').Replace('%','');if($bits -notmatch '^[01]{32}$'){throw 'Ganon active mask changed.'}
$rows.Add("active-04`t$(($bits.ToCharArray()|ForEach-Object {'0'+$_}) -join ',')`tdata/ages/enemyActiveCollisions.s")
$tableRows=$rows
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64")
foreach($id in @(4,0x60)) {
    $def=Get-EnemyDefinition $id
    $sprite=$gfxNames[$def.Gfx];Copy-EnemySprite $sprite
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($def.Animations -join "`n"))
    $rows.Add("$($id.ToString('x2'))`t00`t$sprite`t$($def.TileBase)`t$($def.Palette)`t$([int](Get-EnemySpriteSourceGrayscaleInverted $sprite))`t$($def.RadiusY)`t$($def.RadiusX)`t$($def.Damage)`t$($def.Health)`t$encoded")
}
$path=Join-Path $Disassembly 'data/ages/partAnimations.s'
$tables=Read-AssemblyDwTables $path 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers=Read-AssemblyDwTables $path 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$animations=Read-AssemblyAnimationDefinitions $path 'partAnimation[0-9a-f]+(?:Loop)?' $false
$parts=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partData.s') 'partData' '.db')
foreach($id in @(0x50,0x51,0x52)) {
    $hex=$id.ToString('x2');$data=@($parts[$id].Operands|ForEach-Object {Convert-AssemblyInteger $_})
    $tableRows.Add("part-$hex`t$(($data|ForEach-Object {$_.ToString('x2')}) -join ',')`tdata/ages/partData.s")
    $sprite=$gfxNames[$data[0]];Copy-EnemySprite $sprite
    $actorPointers=@($pointers["part${hex}OamDataPointers"])
    if($id -eq 0x51) {
        # Animation4 indexes straight across the next pointer-table label.
        $pointerSource=Read-ImportText $path
        if($actorPointers.Count -ne 13 -or $pointerSource -notmatch '(?ms)^part51OamDataPointers:.*?\.dw partOamData53702\s*part52OamDataPointers:') {throw 'PART$51 animation4 OAM pointer fallthrough changed.'}
        $actorPointers+=@($pointers['part52OamDataPointers'])
    }
    $sequences=@(foreach($label in $tables["part${hex}Animations"]) {
        $definition=$animations[$label]
        $frames=@(foreach($frame in $definition.Frames) {
            $oam=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partOamData.s') $actorPointers[$frame.PointerOffset/2] '.db')
            $cells=@($oam|Select-Object -Skip 1|ForEach-Object {($_.Operands|ForEach-Object {Convert-AssemblyInteger $_}) -join ','})
            if($cells.Count -ne (Convert-AssemblyInteger $oam[0].Operands[0])){throw "Ganon PART `$${hex}: malformed OAM."}
            "$($frame.Duration),$($frame.Parameter)@$($cells -join ';')"
        })
        "$($frames -join '|')~$($definition.LoopStart)"
    })
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sequences -join "`n"))
    $rows.Add("$hex`t00`t$sprite`t$($data[5])`t$($data[6] -band 7)`t1`t$($data[2] -shr 4)`t$($data[2] -band 15)`t$((256-$data[3])/2)`t1`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'objects/ganon_tables.tsv'),$tableRows)
Write-GeneratedTable((Join-Path $destination 'objects/ganon_actors.tsv'),$rows)
# Ganon changes the sheets at $8200 (A) and $8400 (B..G). Each animation
# uses one of these source VRAM arrangements; idle frames use only $8000/$8200.
$banks=@{
    'revival'=@(0x16,0x18,0x19,0x1a,0x1b)
    # $8600/$8800 retain the revival art, including Ganon's roar frames.
    'idle'=@(0x16,0x17,0x19,0x1a,0x1b)
    'B'=@(0x16,0x17,0x1e,0x1a,0x1b);'C'=@(0x16,0x17,0x1f,0x1a,0x1b)
    'D'=@(0x16,0x17,0x20,0x1a,0x1b);'E'=@(0x16,0x17,0x21,0x1a,0x1b)
    'F'=@(0x16,0x17,0x22,0x1a,0x1b);'G'=@(0x16,0x17,0x23,0x1a,0x1b)
}
$animationBanks=@('revival','revival','idle','idle','B','C','E','B','G','F','B','G','F','revival','D')
$rows.Clear();$rows.Add("# animation`tsprites`tsource")
for($index=0;$index -lt $animationBanks.Count;$index++) {
    $sprites=@($banks[$animationBanks[$index]]|ForEach-Object {$gfxNames[$_]})
    foreach($sprite in $sprites){Copy-EnemySprite $sprite}
    $rows.Add("$index`t$($sprites -join ',')`tganon.s:enemySetAnimation/GFXH_GANON_$($animationBanks[$index])")
}
Write-GeneratedTable((Join-Path $destination 'objects/ganon_visuals.tsv'),$rows)
Write-GeneratedBytes((Join-Path $destination 'objects/ganon_sprite_palette_6.bin'),(Read-PaletteBytes 'paletteData4988' 4))
Write-GeneratedBytes((Join-Path $destination 'objects/ganon_background_palette_7.bin'),(Read-PaletteBytes 'paletteData5390' 4))
$reversePalettes=[Collections.Generic.List[byte]]::new()
foreach($label in @('paletteData5390','paletteData5398','paletteData53a0','paletteData53a8','paletteData53b0','paletteData53b8','paletteData53c0','paletteData53c8')) {
    $reversePalettes.AddRange([byte[]](Read-PaletteBytes $label 4))
}
Write-GeneratedBytes((Join-Path $destination 'objects/ganon_reverse_palettes.bin'),$reversePalettes.ToArray())
$rows.Clear();$rows.Add("# text-id`tmessage-base64`tsource")
foreach($id in @(0x2f0d,0x2f0e)) {
    $message=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/ganon_texts.tsv'),$rows)
