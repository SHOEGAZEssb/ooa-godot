# INTERAC $6d/$6e and ENEMY $61 retain their independent slot dispatches.
$source = 'object_code/ages/enemies/veranPossessionBoss.s'
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# profile`tvalues`tsource")
foreach ($spec in @(@('pillars','@pillarList',12), @('pillar-offsets','@pillarOffsets',8), @('attack-probabilities','@attackProbabilities',10), @('spider-probabilities','@spiderSpawnProbabilities',5))) {
    $values = @(Read-AssemblyDataDirectives (Join-Path $Disassembly $source) $spec[1] '.db' | ForEach-Object { $_.Operands } | ForEach-Object { Convert-AssemblyInteger $_ })
    if ($values.Count -ne $spec[2]) { throw "${source}:$($spec[1]): incorrect table boundary." }
    $rows.Add("$($spec[0])`t$(($values | ForEach-Object { $_.ToString('x2') }) -join ',')`t${source}:$($spec[1])")
}
foreach ($spec in @(@('presets','nayruSavedCutscene_angleAndAnimationPresets',10), @('guard-motion','@guardAnglesAndAnimations',12), @('guard-animations','@guardAnimations',6))) {
    $values = @(Read-AssemblyDataDirectives (Join-Path $Disassembly 'scripts/ages/scriptHelper.s') $spec[1] '.db' | ForEach-Object { $_.Operands } | ForEach-Object { Convert-AssemblyInteger $_ })
    if ($values.Count -ne $spec[2]) { throw "scriptHelper.s:$($spec[1]): incorrect rescue table boundary." }
    $rows.Add("$($spec[0])`t$(($values | ForEach-Object { $_.ToString('x2') }) -join ',')`tscripts/ages/scriptHelper.s:$($spec[1])")
}
$effects = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
if ($effects.Count -ne 4000) { throw 'Possession boss: incomplete collision table.' }
foreach ($mode in @(0x06,0x10,0x43,0x5b,0x5c)) {
    $rows.Add("collision-$($mode.ToString('x2'))`t$(($effects[($mode*32)..($mode*32+31)] | ForEach-Object {$_.ToString('x2')}) -join ',')`tdata/ages/objectCollisionTable.s:objectCollisionTable")
}
$active = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/enemyActiveCollisions.s') 'enemyActiveCollisions' 'dbrev')
$bits = ($active[0x61].Operands -join '').Replace('%','')
if ($active.Count -ne 128 -or $bits -notmatch '^[01]{32}$') { throw 'Possession boss: incomplete active collisions.' }
$rows.Add("active-61`t$(($bits.ToCharArray() | ForEach-Object {'0'+$_}) -join ',')`tdata/ages/enemyActiveCollisions.s:ENEMY_VERAN_POSSESSION_BOSS")
$bits = ($active[0x0f].Operands -join '').Replace('%','')
if ($bits -notmatch '^[01]{32}$') { throw 'ENEMY $0f: incomplete active collisions.' }
$rows.Add("active-0f`t$(($bits.ToCharArray() | ForEach-Object {'0'+$_}) -join ',')`tdata/ages/enemyActiveCollisions.s:ENEMY_VERAN_SPIDER")
$partActive = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/partActiveCollisions.s') 'partActiveCollisions' 'dbrev')
$bits = ($partActive[0x37].Operands -join '').Replace('%','')
if ($partActive.Count -ne 90 -or $bits -notmatch '^[01]{32}$') { throw 'PART $37: incomplete active collisions.' }
$rows.Add("active-37`t$(($bits.ToCharArray() | ForEach-Object {'0'+$_}) -join ',')`tdata/ages/partActiveCollisions.s:PART_VERAN_PROJECTILE")
Write-GeneratedTable((Join-Path $destination 'objects/palace_possession_tables.tsv'),$rows)

$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64")
foreach ($subid in 0..3) {
    $definition = Get-EnemyDefinition 0x61 $subid
    $sprites = @(0xd0..0xd3 | ForEach-Object { $gfxNames[$_] })
    foreach ($sprite in $sprites) { Copy-EnemySprite $sprite }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($definition.Animations -join "`n"))
    $rows.Add("61`t$($subid.ToString('x2'))`t$($sprites -join ',')`t$($definition.TileBase)`t$($definition.Palette)`t1`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.Damage)`t$($definition.Health)`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'objects/palace_possession_enemies.tsv'),$rows)
$spiderSource=Read-ImportText (Join-Path $Disassembly 'object_code/ages/enemies/veranSpider.s')
if ($spiderSource -notmatch '(?ms)^veranSpider_state_uninitialized:.*?PALH_8a.*?and \$7f.*?cp \$70 \+ SCREEN_WIDTH.*?hCameraX.*?hCameraY.*?getTileCollisionsAtPosition.*?ld c,\$08.*?SPEED_60' -or
    $spiderSource -notmatch '(?ms)^veranSpider_state8:.*?ld c,\$0e.*?set 4,\(hl\).*?SND_BOMB_LAND' -or
    $spiderSource -notmatch '(?ms)^veranSpider_state9:.*?and \$07.*?sub \$04.*?inc a.*?cp \$03.*?SPEED_140.*?ld \(hl\),120' -or
    $spiderSource -notmatch '(?ms)^veranSpider_stateA:.*?ecom_applyVelocityForSideviewEnemyNoHoles.*?ld \(hl\),\$40.*?^veranSpider_setRandomAngleAndCounter1:.*?ld bc,\$1870.*?add \$70') {
    throw 'veranSpider.s: ENEMY $0f initialization, charge or random-direction contract changed.'
}
$definition=Get-EnemyDefinition 0x0f 1
if ($definition.Collision -ne 0x10 -or ($effects[(0x10*32)..(0x10*32+3)] -join ',') -ne '2,16,15,15') {
    throw 'ENEMY $0f source contact/shield collision profile changed.'
}
Copy-EnemySprite $gfxNames[$definition.Gfx]
$encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($definition.Animations -join "`n"))
Write-GeneratedTable((Join-Path $destination 'objects/veran_spider.tsv'),@(
    "# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64",
    "0f`t01`t$($gfxNames[$definition.Gfx])`t$($definition.TileBase)`t$($definition.Palette)`t1`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.Damage)`t$($definition.Health)`t$encoded"
))
Write-GeneratedBytes((Join-Path $destination 'objects/veran_spider_palette.bin'),(Read-PaletteBytes 'paletteData4978' 4))
& {
    $path=Join-Path $Disassembly 'data/ages/partAnimations.s'
    $tables=Read-AssemblyDwTables $path 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
    $pointers=Read-AssemblyDwTables $path 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
    $animations=Read-AssemblyAnimationDefinitions $path 'partAnimation[0-9a-f]+(?:Loop)?' $false
    $partData=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partData.s') 'partData' '.db')
    $data=@($partData[0x37].Operands | ForEach-Object {Convert-AssemblyInteger $_})
    $sprite=$gfxNames[$data[0]]; Copy-EnemySprite $sprite
    $sequences=@(foreach($label in $tables['part37Animations']) {
        $definition=$animations[$label]
        $frames=@(foreach($frame in $definition.Frames) {
            $oam=@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/ages/partOamData.s') $pointers['part37OamDataPointers'][$frame.PointerOffset/2] '.db')
            $cells=@($oam | Select-Object -Skip 1 | ForEach-Object { ($_.Operands | ForEach-Object {Convert-AssemblyInteger $_}) -join ',' })
            if($cells.Count -ne (Convert-AssemblyInteger $oam[0].Operands[0])) {throw 'PART $37: malformed OAM.'}
            "$($frame.Duration),$($frame.Parameter)@$($cells -join ';')"
        })
        "$($frames -join '|')~$($definition.LoopStart)"
    })
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sequences -join "`n"))
    $rows.Clear(); $rows.Add("# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64")
    $rows.Add("37`t00`t$sprite`t$($data[5])`t$($data[6] -band 7)`t1`t$($data[2] -shr 4)`t$($data[2] -band 15)`t$((256-$data[3])/2)`t1`t$encoded")
    Write-GeneratedTable((Join-Path $destination 'objects/palace_veran_projectile.tsv'),$rows)
}
Write-GeneratedBytes((Join-Path $destination 'objects/palace_possession_palette.bin'),(Read-PaletteBytes 'paletteData49a0' 4))
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# subid`tsprite`ttile-base`tpalette`tanimation`tsource")
foreach ($subid in 0..4) {
    $graphic = $interactionGraphics["110:$subid"]
    if ($null -eq $graphic) { throw "INTERAC_NAYRU_SAVED_CUTSCENE `$6e:${subid}: missing graphics." }
    $sprite = $gfxNames[$graphic.Gfx]; Copy-EnemySprite $sprite
    $rows.Add("$subid`t$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$(Resolve-NpcAnimation 0x6e $graphic.DefaultAnimation)`tdata/ages/interactionData.s:interaction6eSubidData")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/palace_possession_visuals.tsv'),$rows)
$animations = @{}
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# index`tanimation`tsource")
foreach ($index in 0..0x13) {
    $animations[$index] = Resolve-NpcAnimation 0x6e $index
    if (-not $animations[$index]) { throw "INTERAC `$6e: missing animation $index." }
    $rows.Add("$($index.ToString('x2'))`t$($animations[$index])`tdata/ages/interactionAnimations.s:interaction6eAnimations")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/palace_possession_animations.tsv'),$rows)
$ops = [Collections.Generic.HashSet[string]]::new([string[]]@('asm15','wait','setanimation','showtext','writeobjectbyte','checkobjectbyteeq','scriptend','applyspeed','spawninteraction','checkmemoryeq','writememory','scriptjump'))
$commands = @(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scripts.s') 'possessedNayru_beginFightScript' $ops 'companionScript_subid00Script')
$bindings = @{}
$texts = @{}
foreach ($command in $commands) {
    switch ($command.Opcode) {
        'showtext' {
            $id = [Convert]::ToInt32($command.Operands.Substring(3),16)
            $texts[$id] = Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
        }
        'asm15' {
            $handler = switch -Regex ($command.Operands) {
                '^scriptHelp.possessedNayru_makeExclamationMark$' { 'Exclaim';break }
                '^scriptHelp.possessedNayru_moveLinkForward$' { 'MoveLink';break }
                '^playSound, (?<sound>\w+)$' { 'Sound:'+([int]$soundIds[$Matches['sound']]);break }
                '^scriptHelp.nayruSavedCutscene_createEnergySwirl$' { 'Swirl';break }
                '^scriptHelp.nayruSavedCutscene_spawnGuardIndex, \$(?<index>0[0-5])$' { 'SpawnGuard:'+([Convert]::ToInt32($Matches['index'],16));break }
                '^scriptHelp.nayruSavedCutscene_setSpeedZIndex, \$(?<index>0[01])$' { 'Jump:'+([Convert]::ToInt32($Matches['index'],16));break }
                '^scriptHelp.nayruSavedCutscene_loadAngleAndAnimationPreset, \$(?<index>0[0-4])$' { 'Preset:'+([Convert]::ToInt32($Matches['index'],16));break }
                '^scriptHelp.nayruSavedCutscene_loadGuardAnimation$' { 'GuardAnimation';break }
                '^scriptHelp.nayruSavedCutscene_loadGuardAngleToMoveTowardCenter$' { 'GuardCenter';break }
                default { throw "scripts.s:$($command.Line): unsupported possession helper '$($command.Operands)'." }
            }
            $bindings["asm15|$($command.Operands)"] = @{Opcode='native';Payload=$handler}
        }
        'spawninteraction' {
            if ($command.Operands -notmatch '^INTERAC_NAYRU_SAVED_CUTSCENE, \$(?<subid>0[1-3]), \$(?<y>[0-9a-f]{2}), \$(?<x>[0-9a-f]{2})$') { throw "scripts.s:$($command.Line): unsupported possession spawn." }
            $bindings["spawninteraction|$($command.Operands)"] = @{Opcode='nativeyield';Payload="Spawn:$($Matches['subid']):$($Matches['y']):$($Matches['x'])"}
        }
        'checkobjectbyteeq' { $bindings["checkobjectbyteeq|$($command.Operands)"] = @{Opcode='checkmemoryeq';Arg0='00';Payload='GhostSignal'} }
    }
}
# interactionSetScript points directly into ROM here. Only loadscript copies
# a bank-$15 script into wBigBuffer and enables yielding relocated jumps.
Write-GeneratedTable((Join-Path $destination 'cutscenes/palace_possession_commands.tsv'),
    (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -texts $texts -positions $allTextPositions -animations $animations -bindings $bindings `
        -symbols @{'Interaction.var37'=0x77;'Interaction.angle'=0x49;'ANGLE_DOWN'=0x10;'wTmpcfc0.genericCutscene.cfd0'='RescueSignal';'w1Link.direction'='LinkDirection';'DIR_UP'=0;'DIR_DOWN'=2;'DIR_LEFT'=3} `
        -yieldOnJump $false -externalTargets @{}))
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# script`tindex`tsource")
$knownScripts = @()
foreach ($command in $commands) {
    if ($command.Label -match '^(possessedNayru_|interaction6e_)' -and $command.Label -notin $knownScripts) {
        $rows.Add("$($command.Label)`t$($command.Index)`tscripts/ages/scripts.s:$($command.Line)")
        $knownScripts += @($command.Label)
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/palace_possession_entries.tsv'),$rows)
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# text-id`tmessage-base64`tsource")
foreach ($id in @(0x2f2a,0x560b,0x560c,0x5611)) {
    $message = Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))`ttext/ages/text.yaml:TX_$($id.ToString('x4'))")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/palace_possession_texts.tsv'),$rows)
