# PART_FALLING_BOULDER_SPAWNER $45: placements remain in the ordered object stream.
$path = Join-Path $Disassembly 'object_code\ages\parts\fallingBoulderSpawner.s'
$source = Read-ImportText $path
if ($source -notmatch '(?s)@state0:.*?Part.speed\s+ld \(hl\),\$32.*?sub \$08\s+jr z,\+\s+add \$04' -or
    $source -notmatch '(?s)@bounceRandomlyDownwards:.*?<\(-\$1a0\).*?>\(-\$1a0\).*?getRandomNumber_noPreserveVars\s+and \$07\s+cp \$07\s+jr nc,-\s+sub \$03\s+add \$10.*?objectSetVisiblec1.*?SND_RUMBLE' -or
    $source -notmatch '(?s)@state2:\s+ld c,\$20\s+call objectUpdateSpeedZ_paramC\s+call z,@bounceRandomlyDownwards\s+call objectApplySpeed.*?cp \$88.*?Part.counter1\s+ld \(hl\),\$b4.*?Part.var30.*?objectSetInvisible') {
    throw 'fallingBoulderSpawner.s: $45 initialization, bounce or reset contract changed.'
}
$delays = @((@(Read-AssemblyDataDirectives $path '@initialTimeToAppear' '.db')[0]).Operands |
    ForEach-Object { Convert-AssemblyInteger $_ })
if (($delays -join ',') -ne '45,90,135,180') { throw 'PART $45: expected four source appearance delays.' }
$bytes = @((@(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data\ages\partData.s') 'partData' '.db')[0x45]).Operands |
    ForEach-Object { Convert-AssemblyInteger $_ })
if (($bytes -join ',') -ne '150,4,68,252,64,0,5,0' -or
    (Read-ImportText (Join-Path $Disassembly 'data\ages\partActiveCollisions.s')) -notmatch
        '(?m)^\s+dbrev %10000000 %00000000 %00000000 %00000000 ; 0x45') {
    throw 'PART $45: expected disabled initial collision, radius $04, damage $04 and Link-only collision mask.'
}
$animationPath = Join-Path $Disassembly 'data\ages\partAnimations.s'
$tables = Read-AssemblyDwTables $animationPath 'part[0-9a-f]{2}Animations' 'partAnimation[0-9a-f]+'
$pointers = Read-AssemblyDwTables $animationPath 'part[0-9a-f]{2}OamDataPointers' 'partOamData[0-9a-f]+'
$definitions = Read-AssemblyAnimationDefinitions $animationPath 'partAnimation[0-9a-f]+(?:Loop)?' $true
$frames = [Collections.Generic.List[string]]::new()
foreach ($frame in $definitions[$tables['part45Animations'][0]].Frames) {
    $index = [int]($frame.PointerOffset / 2)
    if ($index -ge $pointers['part45OamDataPointers'].Count) { throw 'PART $45: invalid OAM pointer.' }
    $frames.Add("$($frame.Duration),$($frame.Parameter)@$(Resolve-Oam $partOamSource $pointers['part45OamDataPointers'][$index])")
}
if (!$frames.Count) { throw 'PART $45: missing rolling animation.' }
$sprite = $gfxNames[$bytes[0]]
Copy-EnemySprite $sprite
$inverted = [int](Get-EnemySpriteSourceGrayscaleInverted $sprite)
Write-GeneratedTable((Join-Path $destination 'effects\falling_boulder.tsv'), @(
    "# sprite`ttile-base`tpalette`tradius`tdamage-quarters`tdelays`tanimation-base64`tsource-grayscale-inverted`tsource",
    "$sprite`t$($bytes[5])`t$($bytes[6] -band 7)`t$($bytes[2] -band 15)`t$(256-$bytes[3])`t$($delays -join ',')`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($frames -join '|')))`t$inverted`tfallingBoulderSpawner.s:partCode45;gfx_compressible/common/spr_boulder.properties"
))
