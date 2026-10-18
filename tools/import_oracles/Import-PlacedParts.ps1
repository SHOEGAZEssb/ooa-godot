# PART_FALLING_FIRE $23 and PART_SMALL_FAIRY $28 are ordinary ordered PARTs.
$partPath = Join-Path $Disassembly 'data/ages/partData.s'
$partRows = @(Read-AssemblyDataDirectives $partPath 'partData' '.db')
$animationPath = Join-Path $Disassembly 'data/ages/partAnimations.s'
$tables = Read-AssemblyDwTables $animationPath 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers = Read-AssemblyDwTables $animationPath 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$definitions = Read-AssemblyAnimationDefinitions $animationPath 'partAnimation[0-9a-f]+(?:Loop)?' $false
$active = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/partActiveCollisions.s') 'partActiveCollisions' 'dbrev')
$effects = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
if ($effects.Count -ne 4000) { throw 'Placed PARTs: incomplete vanilla collision table.' }
$visuals = [Collections.Generic.List[string]]::new()
$visuals.Add("# id`tsprite`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tmode`tinitial-collision`tanimations-base64`tsource")
$profiles = [Collections.Generic.List[string]]::new()
$profiles.Add("# profile`tvalues`tsource")
foreach ($id in @(0x23, 0x28)) {
    $hex = $id.ToString('x2')
    $bytes = @($partRows[$id].Operands | ForEach-Object { Convert-AssemblyInteger $_ })
    $expected = if ($id -eq 0x23) { '0,114,85,248,64,10,10,0' } else { '120,129,68,0,1,0,2,0' }
    if (($bytes -join ',') -ne $expected) { throw "data/ages/partData.s: PART `$$hex properties changed." }
    # addIndexToLoadedObjectGfx returns tile base zero for GFX index zero:
    # $23 reads the resident spr_common_sprites, without requesting a load.
    $sprite = if ($id -eq 0x23) { 'spr_common_sprites' } else { $gfxNames[$bytes[0]] }
    Copy-EnemySprite $sprite
    $sequences = @(foreach ($label in @($tables["part${hex}Animations"] | Select-Object -First 2)) {
        $definition = $definitions[$label]
        $frames = @(foreach ($frame in $definition.Frames) {
            $index = [int]($frame.PointerOffset / 2)
            if ($index -ge $pointers["part${hex}OamDataPointers"].Count) { throw "PART `$$hex invalid OAM offset." }
            "$($frame.Duration),$($frame.Parameter)@$(Resolve-Oam $partOamSource $pointers["part${hex}OamDataPointers"][$index])"
        })
        if (!$frames.Count) { throw "PART `$$hex missing animation frames." }
        "$($frames -join '|')~$($definition.LoopStart)"
    })
    if ($sequences.Count -ne 2) { throw "PART `$$hex requires its two source animation entries." }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sequences -join "`n"))
    $visuals.Add("$hex`t$sprite`t$($bytes[5])`t$($bytes[6] -band 7)`t$([int](Get-EnemySpriteSourceGrayscaleInverted $sprite))`t$($bytes[2] -shr 4)`t$($bytes[2] -band 15)`t$(((256-$bytes[3])%256)/2)`t$($bytes[4])`t$(($bytes[1]-band 0x7f).ToString('x2'))`t$([int](($bytes[1]-band 0x80)-ne 0))`t$encoded`tdata/ages/partData.s:part$hex;data/ages/partAnimations.s:part${hex}Animations")
    $bits = ($active[$id].Operands -join '').Replace('%', '')
    if ($bits -notmatch '^[01]{32}$') { throw "PART `$$hex malformed active-collision bitset." }
    $profiles.Add("active-$hex`t$(($bits.ToCharArray() | ForEach-Object { '0'+$_ }) -join ',')`tdata/ages/partActiveCollisions.s:part$hex")
}
foreach ($mode in @(1, 0x72)) {
    $profiles.Add("collision-$($mode.ToString('x2'))`t$(($effects[($mode*32)..($mode*32+31)] | ForEach-Object { $_.ToString('x2') }) -join ',')`tdata/ages/objectCollisionTable.s")
}
$fairyPath = Join-Path $Disassembly 'object_code/common/parts/smallFairy.s'
$speeds = @(Read-AssemblyLiteralValues $fairyPath '@table_5666')
$offsets = @(Read-AssemblyLiteralValues $fairyPath 'table_56f5')
$firePath = Join-Path $Disassembly 'object_code/common/parts/fallingFire.s'
$intervals = @(Read-AssemblyLiteralValues $firePath 'table_554f')
if (($speeds -join ',') -ne '10,20,30,40' -or $offsets.Count -ne 16 -or ($intervals -join ',') -ne '120,120,30,30') {
    throw 'Placed PART $23/$28 movement/interval table changed.'
}
$fairy = Read-ImportText $fairyPath
$fire = Read-ImportText $firePath
if ($fairy -notmatch '(?ms)^@state0:.*?ld \(hl\),\$fa.*?objectSetVisiblec2' -or
    $fairy -notmatch '(?ms)^@state1:.*?and \$3e\s+add \$08.*?and \$03.*?and \$1e' -or
    $fairy -notmatch '(?ms)^func_56cd:.*?sub \$38\s+cp \$80.*?sub \$18\s+cp \$50' -or
    $fire -notmatch '(?ms)^@subid2:.*?cp \$b0.*?call add16BitRefs.*?add \$10' -or
    $fire -notmatch '(?ms)^func_5535:.*?ld l,\$e4\s+set 7,\(hl\).*?objectSetVisible81') {
    throw 'smallFairy.s/fallingFire.s: unrepresented handler or arithmetic change.'
}
$profiles.Add("fairy-speeds`t$(($speeds | ForEach-Object { $_.ToString('x2') }) -join ',')`tobject_code/common/parts/smallFairy.s:@table_5666")
$profiles.Add("fairy-offsets`t$(($offsets | ForEach-Object { $_.ToString('x2') }) -join ',')`tobject_code/common/parts/smallFairy.s:table_56f5")
$profiles.Add("fire-intervals`t$(($intervals | ForEach-Object { $_.ToString('x2') }) -join ',')`tobject_code/common/parts/fallingFire.s:table_554f")
Write-GeneratedTable((Join-Path $destination 'objects/placed_parts.tsv'), $visuals)
Write-GeneratedTable((Join-Path $destination 'objects/placed_part_tables.tsv'), $profiles)
