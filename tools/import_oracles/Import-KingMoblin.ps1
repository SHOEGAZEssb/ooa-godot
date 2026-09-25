$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# profile`tvalues`tsource")
$speedValues = @{}
$offset = 0
foreach ($match in [regex]::Matches((Read-ImportText (Join-Path $Disassembly 'constants/common/objectSpeeds.s')), '(?m)^\s*(SPEED_[0-9a-f]+)\s+dsb (\d+)')) {
    $speedValues[$match.Groups[1].Value] = $offset
    $offset += [int]$match.Groups[2].Value
}
$speedValues['SPEED_0c0'] = $speedValues['SPEED_c0']
foreach ($spec in @(
    @('speeds','enemies/kingMoblin.s','enemyCode7f','speeds',6),
    @('pickup','enemies/kingMoblin.s','kingMoblin_initBombPickupAnimation','counter2Vals',6),
    @('raise','enemies/kingMoblin.s','kingMoblin_stateC','counter2Vals',6),
    @('explosions','enemies/kingMoblin.s','kingMoblin_state15','explosionPositions',4),
    @('minions','enemies/kingMoblinMinionMain.s','kingMoblinMinion_state0','data',8),
    @('escape-angles','enemies/kingMoblinMinionMain.s','kingMoblinMinion_state7','subidBombThrowAngles',2),
    @('fuses','parts/kingMoblinBomb.s','kingMoblinBomb_state0','counter1Values',4),
    @('flashes','parts/kingMoblinBomb.s','kingMoblinBomb_state0','numRedFlashes',6)
)) {
    $source = 'object_code/ages/' + $spec[1]
    $code = Read-ImportText (Join-Path $Disassembly $source)
    $block = [regex]::Match($code, '(?ms)^'+$spec[2]+':.*?^@'+$spec[3]+':[^\r\n]*\r?\n(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
    if (-not $block.Success) { throw "${source}: missing $($spec[2])@$($spec[3])." }
    $values = @(foreach ($token in (($block.Groups['body'].Value -replace ';[^\r\n]*','' -replace '\.db','').Trim() -split '[,\s]+')) {
        if ($speedValues.ContainsKey($token)) { $speedValues[$token] } else { Convert-AssemblyInteger $token }
    })
    if ($values.Count -ne $spec[4]) { throw "${source}: invalid $($spec[0]) table." }
    $rows.Add("$($spec[0])`t$(($values | ForEach-Object { $_.ToString('x2') }) -join ',')`t${source}:$($spec[2])@$($spec[3])")
}
foreach ($flag in @('GLOBALFLAG_MOBLINS_KEEP_DESTROYED','GLOBALFLAG_16')) {
    $rows.Add("$flag`t$(([int]$globalFlagValues[$flag]).ToString('x2'))`tconstants/common/globalFlags.s:$flag")
}
$gfx=Read-ImportText (Join-Path $Disassembly 'code/ages/roomGfxChanges.s')
$ledge=[regex]::Match($gfx,'(?ms)^roomTileChangesAfterLoad05:\s*ld hl,wRoomLayout\+\$(?<position>[0-9a-f]{2})\s*ld a,\$(?<tile>[0-9a-f]{2})\s*(?<writes>(?:ldi \(hl\),a\s*)+)ret')
if(-not $ledge.Success -or $gfx -notmatch '(?ms)^@group2:.*?\.db \$af \$05') {throw 'King Moblin: roomTileChangesAfterLoad05 dispatch changed.'}
$count=[regex]::Matches($ledge.Groups['writes'].Value,'ldi').Count
$rows.Add("ledge`t02,af,$($ledge.Groups['position'].Value),$($count.ToString('x2')),$($ledge.Groups['tile'].Value)`tcode/ages/roomGfxChanges.s:roomTileChangesAfterLoad05")
$effects=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
if($effects.Count -ne 4000){throw 'King Moblin: incomplete collision effect table.'}
$rows.Add("collision`t$(($effects[0xa00..0xa1f]|ForEach-Object{$_.ToString('x2')}) -join ',')`tdata/ages/objectCollisionTable.s:objectCollisionTable+0a00")
Write-GeneratedTable((Join-Path $destination 'objects/king_moblin_tables.tsv'), $rows)
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64")
foreach ($spec in @(@(0x7f,0),@(0x56,0),@(0x56,1))) {
    $id=$spec[0]; $subid=$spec[1]
    $def=Get-EnemyDefinition $id $subid
    $sprites=if($id -eq 0x7f){@($gfxNames[0xa9],$gfxNames[0xaa],$gfxNames[0xab],$gfxNames[0xac])}else{@($gfxNames[0x90])}
    foreach($sprite in $sprites){Copy-EnemySprite $sprite}
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($def.Animations -join "`n"))
    $rows.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($sprites -join ',')`t$($def.TileBase)`t$($def.Palette)`t1`t$($def.RadiusY)`t$($def.RadiusX)`t$($def.Damage)`t$($def.Health)`t$encoded")
}
$path=Join-Path $Disassembly 'data/ages/partAnimations.s'
$tables=Read-AssemblyDwTables $path 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers=Read-AssemblyDwTables $path 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$animations=Read-AssemblyAnimationDefinitions $path 'partAnimation[0-9a-f]+(?:Loop)?' $false
$partData=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partData.s') 'partData' '.db')
foreach($id in @(0x3f,0x47)) {
    $hex=$id.ToString('x2')
    $data=@($partData[$id].Operands | ForEach-Object {Convert-AssemblyInteger $_})
    $sprite=$gfxNames[$data[0]]; Copy-EnemySprite $sprite
    $sequences=@(foreach($label in $tables["part${hex}Animations"]) {
        $definition=$animations[$label]
        $frames=@(foreach($frame in $definition.Frames) {
            $oam=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partOamData.s') $pointers["part${hex}OamDataPointers"][$frame.PointerOffset/2] '.db')
            $cells=@($oam | Select-Object -Skip 1 | ForEach-Object { ($_.Operands | ForEach-Object {Convert-AssemblyInteger $_}) -join ',' })
            if($cells.Count -ne (Convert-AssemblyInteger $oam[0].Operands[0])){throw "PART ${hex}: malformed OAM."}
            "$($frame.Duration),$($frame.Parameter)@$($cells -join ';')"
        })
        "$($frames -join '|')~$($definition.LoopStart)"
    })
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sequences -join "`n"))
    $rows.Add("$hex`t00`t$sprite`t$($data[5])`t$($data[6] -band 7)`t1`t$($data[2] -shr 4)`t$($data[2] -band 15)`t$((256-$data[3]) -band 255)`t1`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'objects/king_moblin_actors.tsv'),$rows)
Write-GeneratedBytes((Join-Path $destination 'objects/king_moblin_palette.bin'),(Read-PaletteBytes 'paletteData4990' 4))
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# text-id`tmessage-base64`tsource")
foreach($id in @(0x2f19,0x2f1a)) {
    if(-not $allTexts.ContainsKey($id)){throw 'King Moblin: missing intro text.'}
    if(-not $allTextPositions.ContainsKey($id) -or $allTextPositions[$id] -ne 2){throw 'King Moblin: intro position command changed.'}
    $message='\pos('+$allTextPositions[$id]+')'+$allTexts[$id]
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))`ttext/ages/text.yaml:TX_$($id.ToString('x4'))")
}
Write-GeneratedTable((Join-Path $destination 'objects/king_moblin_text.tsv'),$rows)
