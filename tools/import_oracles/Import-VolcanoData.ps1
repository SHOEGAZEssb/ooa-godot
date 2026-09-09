# Native INTERAC_MISCELLANEOUS_2 $dc:$05/$06/$13 and PART_VOLCANO_ROCK $11:$01.
$misc = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$handlerPath = Join-Path $Disassembly 'object_code\ages\interactions\volcanoHandler.s'
$handler = Read-ImportText $handlerPath
$rock = Read-ImportText (Join-Path $Disassembly 'object_code\common\parts\volcanoRock.s')
if ((Read-ImportText (Join-Path $Disassembly 'include\wram.s')) -notmatch
        '(?s)\.define FIRST_PART_INDEX\s+\$d0\s+\.define LAST_PART_INDEX\s+\$df' -or
    (Read-ImportText (Join-Path $Disassembly 'data\ages\partActiveCollisions.s')) -notmatch
        '(?m)^\s+dbrev %10000000 %00000000 %00000000 %00000000 ; 0x11') {
    throw 'PART_VOLCANO_ROCK $11: part-slot capacity or Link-only active collision contract changed.'
}
if ($misc -notmatch '(?s)interactiondc_subid06:\s+ld a,GLOBALFLAG_TUNI_NUT_PLACED\s+call checkGlobalFlag\s+jr nz,@delete\s+ldbc INTERAC_VOLCANO_HANDLER,\$01\s+call objectCreateInteraction' -or
    $misc -notmatch '(?s)interactiondc_subid05:.*?SNDCTRL_STOPSFX.*?wScreenShakeMagnitude.*?@state1:\s+xor a\s+call @shakeScreen\s+ret nz\s+call @setRandomShakeDuration\s+jp interactionIncState\s+@state2:.*?call @shakeScreen\s+ret nz\s+ld l,Interaction.state\s+ld \(hl\),\$01\s+@setRandomShakeDuration:\s+call getRandomNumber\s+and \$7f\s+sub \$40\s+add \$60' -or
    $misc -notmatch '(?s)interactiondc_subid13:.*?TILEINDEX_OVERWORLD_LAVA_1.*?wRoomLayout\+\$14.*?ld l,\$24.*?ld l,\$34' -or
    $handler -notmatch '(?s)and \$0f\s+ld a,SND_RUMBLE.*?wScreenShakeCounterY.*?wScreenShakeCounterX.*?interactionDecCounter1.*?ld c,\$0f.*?getRandomNumber.*?srl c\s+inc c\s+sub c.*?PART_VOLCANO_ROCK' -or
    $rock -notmatch '(?s)volcanoRock_subid1:.*?SPEED_20.*?<\(-\$400\).*?and \$1f.*?add \$08\s+cp \$f8\s+ld c,\$10.*?ld \(hl\),30' -or
    $rock -notmatch '(?s)volcanoRock_common_substate3:.*?inc \(hl\)\s+inc \(hl\).*?objectReplaceWithAnimationIfOnHazard.*?volcanoRock_common_substate4:.*?ld c,\$16.*?ld \(hl\),\$26.*?SND_STRONG_POUND' -or
    $rock -notmatch '(?s)volcanoRock_setRandomPosition:.*?and \$70\s+add \$08.*?and \$fe.*?and \$07\s+inc a\s+swap a\s+add \$08') {
    throw 'volcanoHandler.s / volcanoRock.s / miscellaneous2.s: native eruption contract changed.'
}
$placements = [Collections.Generic.List[string]]::new()
$placements.Add("# group`troom`torder`tsubid`ty`tx`tsource")
$aliases = [Collections.Generic.List[object]]::new()
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<g>[0-7])Map(?<r>[0-9a-f]{2})ObjectData:') {
        $aliases.Add([pscustomobject]@{ Group=$Matches.g; Room=$Matches.r; Order=0 })
        continue
    }
    if ($line -match '^\s+obj_End') { $aliases.Clear(); continue }
    if ($line -notmatch '^\s+obj_') { continue }
    if ($line -match '^\s+obj_Interaction \$dc \$(?<sub>05|06|13)(?: \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}))?\s*$') {
        $sub = $Matches.sub
        $y = if ($Matches.y) { $Matches.y } else { '00' }
        $x = if ($Matches.x) { $Matches.x } else { '00' }
        foreach ($alias in $aliases) {
            $placements.Add("$($alias.Group)`t$($alias.Room)`t$($alias.Order)`t$sub`t$y`t$x`tmiscellaneous2.s:interactiondc_subid$sub")
        }
    }
    foreach ($alias in $aliases) { $alias.Order++ }
}
if ($placements.Count -ne 8) { throw 'miscellaneous2.s: expected seven Symmetry environmental placements.' }
Write-GeneratedTable((Join-Path $destination 'objects\volcano_placements.tsv'), $placements)
$script = [Collections.Generic.List[string]]::new()
$script.Add("# index`tshake-y`tshake-x`tmask`tbase`tsource")
foreach ($row in Read-AssemblyDataDirectives $handlerPath '@script' '.db') {
    $bytes = @($row.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
    if ($bytes.Count -eq 1 -and $bytes[0] -eq 255) { break }
    if ($bytes.Count -ne 4) { throw 'volcanoHandler.s:@script: expected four bytes or $ff.' }
    $script.Add("$($script.Count-1)`t$($bytes -join "`t")`tvolcanoHandler.s:@script")
}
if ($script.Count -ne 8) { throw 'volcanoHandler.s:@script: expected seven steps.' }
Write-GeneratedTable((Join-Path $destination 'effects\volcano_script.tsv'), $script)
$partPath = Join-Path $Disassembly 'data\ages\partAnimations.s'
$tables = Read-AssemblyDwTables $partPath 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers = Read-AssemblyDwTables $partPath 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$definitions = Read-AssemblyAnimationDefinitions $partPath 'partAnimation[0-9a-f]+(?:Loop)?' $true
$animations = [Collections.Generic.List[string]]::new()
foreach ($label in $tables['part11Animations']) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in $definitions[$label].Frames) {
        $index = [int]($frame.PointerOffset / 2)
        if ($index -ge $pointers['part11OamDataPointers'].Count) { throw "PART_VOLCANO_ROCK ${label}: invalid OAM pointer." }
        $frames.Add("$($frame.Duration),$($frame.Parameter)@$(Resolve-Oam $partOamSource $pointers['part11OamDataPointers'][$index])")
        if ($frame.Parameter -eq 255) { break }
    }
    if (!$frames.Count) { throw "PART_VOLCANO_ROCK ${label}: empty animation." }
    $animations.Add($frames -join '|')
}
if ($animations.Count -ne 4) { throw 'PART_VOLCANO_ROCK: expected four animations.' }
$bytes = @((@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data\ages\partData.s') 'partData' '.db')[0x11]).Operands | ForEach-Object { Convert-AssemblyInteger $_ })
if (($bytes -join ',') -ne '0,4,34,254,1,2,10,0') { throw 'PART_VOLCANO_ROCK $11 attributes changed.' }
Copy-EnemySprite 'spr_common_sprites'
Write-GeneratedTable((Join-Path $destination 'effects\volcano_rock.tsv'), @(
    "# sprite`ttile-base`tpalette`tradius`tdamage-quarters`tanimations-base64`tsource",
    "spr_common_sprites`t$($bytes[5])`t$($bytes[6] -band 7)`t$(($bytes[2] -band 15)*2)`t$(256-$bytes[3])`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($animations -join "`n")))`tvolcanoRock.s:volcanoRock_subid1"
))
$radii = @(Read-AssemblyDataDirectives (Join-Path $Disassembly 'object_code\common\parts\volcanoRock.s') '@data' '.db')
$radiusRows = @("# parameter`ty`tx")
foreach ($row in $radii) {
    $values = @($row.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
    if ($values.Count -ne 2) { throw 'volcanoRock.s:@data: expected Y/X collision radii.' }
    $radiusRows += "$((($radiusRows.Count-1)*2))`t$($values -join "`t")"
}
Write-GeneratedTable((Join-Path $destination 'effects\volcano_radii.tsv'), $radiusRows)
$stopSfx = [regex]::Match((Read-ImportText (Join-Path $Disassembly 'constants\common\music.s')),
    '(?m)^\.define SNDCTRL_STOPSFX\s+\$(?<value>[0-9a-f]{2})')
$lavaTile = [regex]::Match((Read-ImportText (Join-Path $Disassembly 'constants\common\tileIndices.s')),
    '(?m)^\.define TILEINDEX_OVERWORLD_LAVA_1\s+\$(?<value>[0-9a-f]{2})')
if (!$stopSfx.Success -or !$lavaTile.Success) { throw 'Missing SNDCTRL_STOPSFX / TILEINDEX_OVERWORLD_LAVA_1 definition.' }
Write-GeneratedTable((Join-Path $destination 'effects\volcano_constants.tsv'), @(
    "# key`tvalue",
    "restored-flag`t$($globalFlagValues['GLOBALFLAG_TUNI_NUT_PLACED'])",
    "rumble`t$($soundIds['SND_RUMBLE'])",
    "impact`t$($soundIds['SND_STRONG_POUND'])",
    "stop-sfx`t$([Convert]::ToInt32($stopSfx.Groups['value'].Value, 16))",
    "lava-tile`t$([Convert]::ToInt32($lavaTile.Groups['value'].Value, 16))"
))
