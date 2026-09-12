# INTERAC_BOMB_UPGRADE_FAIRY $83: preserve the copied script's complete choice table.
$fairyPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$fairyNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\bombUpgradeFairy.s')
$fairyHelper = Read-ImportText $fairyPath
$fairyMain = Read-ImportText (Join-Path $Disassembly 'scripts\ages\scripts.s')
$fairyBomb = Read-ImportText (Join-Path $Disassembly 'object_code\common\items\bombs.s')
if ($fairyMain -notmatch 'bombUpgradeFairyScript:\s+loadscript scriptHelp.bombUpgradeFairyScript_body' -or
    $fairyBomb -notmatch '(?s)call itemUpdateThrowingVerticallyAndCheckHazards\s+ret nc.*?ld bc,ROOM_AGES_050.*?cp b.*?cp c.*?ld a,\$01\s+ld \(wTmpcfc0.bombUpgradeCutscene.state\),a') {
    throw 'bombs.s: bomb hazard trigger for ROOM_AGES_050 or copied fairy script entry changed.'
}
$fairyNodes = [Collections.Generic.List[object]]::new()
$fairyLabels = @{}
$fairyLabel = 'bombUpgradeFairyScript_body'
foreach ($node in @(Read-AssemblyLabelNodes $fairyPath $fairyLabel)) {
    if ($node.Kind -eq 'Label') {
        $fairyLabel = $node.Name
        $fairyLabels[$fairyLabel] = $fairyNodes.Count
    } elseif ($node.Kind -in @('MacroInvocation','Instruction','Data')) {
        if ($node.Code.Trim().StartsWith('.dw')) {
            $previous = $fairyNodes[$fairyNodes.Count-1]
            if ($previous.Node.Name -ne 'jumptable_memoryaddress') { throw "$fairyPath`:$($node.Line): unexpected fairy table" }
            $previous.Targets += @($node.Code.Trim().Substring(3).Trim() -split ',\s*')
        } else { $fairyNodes.Add([pscustomobject]@{Node=$node; Label=$fairyLabel; Targets=@()}) }
    } elseif ($node.Kind -notin @('Blank','Comment')) { throw "$fairyPath`:$($node.Line): unsupported fairy node $($node.Code)" }
}
function Resolve-FairyTarget([string]$label) {
    if (!$fairyLabels.ContainsKey($label)) { throw "bombUpgradeFairyScript_body: unresolved branch $label" }
    return $fairyLabels[$label].ToString()
}
$fairyRows = [Collections.Generic.List[string]]::new()
$fairyRows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
foreach ($entry in $fairyNodes) {
    $node=$entry.Node; $op=$node.Name.ToLowerInvariant(); $args=([string]$node.OperandText).Trim()
    $parts=@($args -split ',\s*'); $a=''; $b=''; $payload=''
    switch ($op) {
        'wait' { if ($args -notmatch '^\d+$') { throw "Invalid fairy wait $args" }; $a=$args }
        'showtext' {
            if ($args -notmatch '^TX_(0c0[0-8])$') { throw "Unsupported fairy text $args" }
            $a=$Matches[1]; $id=[Convert]::ToInt32($a,16); $payload=[string]$allTexts[$id]
            if (!$payload -or $payload -match '\\(?:call|jump)\(') { throw "Unresolved fairy text TX_$a" }
            if ($allTextPositions.ContainsKey($id)) { $b=$allTextPositions[$id].ToString() }
        }
        'jumptable_memoryaddress' {
            if ($args -ne 'wSelectedTextOption' -or $entry.Targets.Count -ne 3) { throw "Unsupported fairy choice table $args" }
            $op='jumptablememoryyield'; $payload=$args+'|'+(($entry.Targets | ForEach-Object { Resolve-FairyTarget $_ }) -join ',')
        }
        'jumpiftextoptioneq' {
            if ($parts.Count -ne 2 -or $parts[0] -ne '$01') { throw "Unsupported fairy confirmation $args" }
            $a='01'; $b=Resolve-FairyTarget $parts[1]
        }
        'writememory' {
            if ($args -ne 'wTmpcfc0.genericCutscene.cfd0, $01') { throw "Unsupported fairy memory write $args" }
            $a='01'; $payload=$parts[0]
        }
        'asm15' {
            if ($args -eq 'playSound, SND_BIG_EXPLOSION') { $op='playsound'; $a=$soundIds['SND_BIG_EXPLOSION'].ToString('x2') }
            elseif ($args -in @('fadeoutToWhite','bombUpgradeFairy_spawnBombsAroundLink',
                'bombUpgradeFairy_lightningStrikesLink','bombUpgradeFairy_decreaseLinkHealth',
                'bombUpgradeFairy_loseAllBombs','bombUpgradeFairy_giveBombUpgrade',
                'bombUpgradeFairy_fadeinFromWhite','bombUpgradeFairy_setGlobalFlag')) { $op='native'; $payload=$args }
            else { throw "Unsupported fairy helper $args" }
        }
        'scriptend' { }
        default { throw "$fairyPath`:$($node.Line): unsupported fairy command $op $args" }
    }
    $fairyRows.Add((New-CutsceneCommandRow 'bomb-upgrade-fairy' ($fairyRows.Count-1) $entry.Label $node.Line $op '' $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\bomb_upgrade_fairy_commands.tsv'), $fairyRows)
# Pin the native arithmetic and side effects; export their operands for runtime use.
foreach ($pattern in @(
    'ld a,GLOBALFLAG_GOT_BOMB_UPGRADE_FROM_FAIRY\s+call checkGlobalFlag\s+jp nz,interactionDelete',
    'call getThisRoomFlags\s+bit 0,a\s+jp z,interactionDelete',
    'ld a,\(wMaxBombs\)\s+cp \$10\s+ld a,\$30\s+jr z,\+\s+ld a,\$50',
    'ldh a,\(<hEnemyTargetY\)\s+sub \$41\s+cp \$06\s+ret nc',
    'ldh a,\(<hEnemyTargetX\)\s+sub \$58\s+cp \$21\s+ret nc\s+call checkLinkVulnerable\s+ret nc',
    'call clearAllParentItems\s+call dropLinkHeldItem',
    'ld a,\$80\s+ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a',
    'ld a,Object.animParameter\s+call objectGetRelatedObject1Var\s+bit 7,\(hl\)\s+ret nz',
    'ld a,\(\$cfd0\)\s+inc a\s+jp z,interactionDelete',
    'call interactionDecCounter1\s+ld a,\(hl\)\s+and \$03'
)) { if ($fairyNative -notmatch $pattern) { throw "bombUpgradeFairy.s: native contract changed: $pattern" } }
foreach ($pattern in @(
    'ld a,\(hl\)\s+cp \$04\s+ret c\s+ld \(hl\),\$04',
    'ld a,\$01\s+ld \(wNumBombs\),a\s+call decNumBombs',
    'ld a,\(wTextNumberSubstitution\)\s+ld \(wMaxBombs\),a\s+ld c,a\s+ld a,TREASURE_BOMBS\s+jp giveTreasure',
    'ld a,\$ff\s+ld \(wTmpcfc0.genericCutscene.cfd0\),a\s+ld a,\$04\s+jp fadeinFromWhiteWithDelay'
)) { if ($fairyHelper -notmatch $pattern) { throw "scriptHelper.s:bombUpgradeFairy native contract changed: $pattern" } }
Write-GeneratedTable((Join-Path $destination 'cutscenes\bomb_upgrade_fairy.tsv'), @(
    '# group`troom`tglobal-flag`ttrigger-y`ttrigger-height`ttrigger-x`ttrigger-width`tbase-capacity`tfirst-upgrade`tsecond-upgrade`tpoof-sound',
    "0`t50`t$($globalFlagValues['GLOBALFLAG_GOT_BOMB_UPGRADE_FROM_FAIRY'])`t41`t06`t58`t21`t10`t30`t50`t$($soundIds['SND_POOF'])"))
$fairyPositions = [regex]::Match($fairyNative, '(?s)@bombPositions:(?<body>.*?)bombUpgradeFairy_subid02:')
$fairyBombRows = @('# index`ty-offset`tx-offset`tdelay')
$index=0
foreach ($match in [regex]::Matches($fairyPositions.Groups['body'].Value, '\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<delay>[0-9a-f]{2})')) {
    $fairyBombRows += "$index`t$($match.Groups['y'].Value)`t$($match.Groups['x'].Value)`t$($match.Groups['delay'].Value)"; $index++
}
if ($index -ne 4) { throw 'bombUpgradeFairy.s:@bombPositions expected four ordered rows.' }
Write-GeneratedTable((Join-Path $destination 'cutscenes\bomb_upgrade_fairy_bombs.tsv'), $fairyBombRows)
# tools/gfx/gfx.py defaults spr_* to invert:true, but this sheet overrides it.
$fairyBombPropertiesPath = Join-Path $Disassembly 'gfx_compressible\ages\spr_moblinflag_bomb_portal.properties'
$fairyBombProperties = Read-ImportText $fairyBombPropertiesPath
if ($fairyBombProperties -notmatch '\A\s*invert:\s*(?<inverted>false)\s*\z') {
    throw "$fairyBombPropertiesPath`: expected the non-inverted `$83:`$02 source graphics contract."
}
$fairyBombInverted = [int][bool]::Parse($Matches['inverted'])
$fairyVisuals = @('# name`tid`tsubid`tsprite`ttile-base`tpalette`tanimation`tsource-offset`tsource-grayscale-inverted')
$fairyGfxHeaders = Read-ImportText (Join-Path $Disassembly 'data\ages\objectGfxHeaders.s')
foreach ($visual in @(@('bomb',0x83,1),@('silver',0x83,2),@('sparkle',0x84,0x0e),@('puff',5,2),@('debris',8,0))) {
    $g=$interactionGraphics["$($visual[1]):$($visual[2])"]
    if (!$g) { $g=$interactionGraphics["$($visual[1]):0"] }
    if (!$g) { throw "Missing fairy visual $($visual[0])" }
    $sprite=if ($g.Gfx -eq 0) { 'spr_common_sprites' } else { $gfxNames[$g.Gfx] }
    if (!$sprite) { throw "Missing fairy visual graphics $($visual[0])" }
    $offset=0
    if ($g.Gfx -ne 0) {
        $header=[regex]::Match($fairyGfxHeaders, ('(?m)/\* \$'+$g.Gfx.ToString('x2')+' \*/ m_ObjectGfxHeader \w+(?:, \$?[0-9a-f]+, \$(?<offset>[0-9a-f]+))?\s*(?:;[^\r\n]*)?$'))
        if (!$header.Success) { throw "Unsupported fairy graphics header `$$( $g.Gfx.ToString('x2'))" }
        if ($header.Groups['offset'].Success) { $offset=[Convert]::ToInt32($header.Groups['offset'].Value,16) }
    }
    $inverted = if ($sprite -eq 'spr_moblinflag_bomb_portal') { $fairyBombInverted } else { 1 }
    $fairyVisuals += "$($visual[0])`t$($visual[1].ToString('x2'))`t$($visual[2].ToString('x2'))`t$sprite`t$($g.TileBase)`t$($g.Palette)`t$(Resolve-NpcAnimation $visual[1] $g.DefaultAnimation)`t$offset`t$inverted"
}
Write-GeneratedTable((Join-Path $destination 'cutscenes\bomb_upgrade_fairy_visuals.tsv'), $fairyVisuals)
Copy-GeneratedFile 'gfx_compressible\ages\spr_moblinflag_bomb_portal.png' 'gfx\spr_moblinflag_bomb_portal.png'
$fairyPalette = Read-ImportText (Join-Path $Disassembly 'data\ages\paletteHeaders.s')
if ($fairyPalette -notmatch 'm_PaletteHeaderStart \$80, PALH_80\s+m_PaletteHeaderSpr 6, 1, (?<palette>\w+)') { throw 'Missing PALH_80 gold bomb palette.' }
Write-GeneratedBytes((Join-Path $destination 'cutscenes\bomb_upgrade_fairy_gold_palette.bin'), (Read-PaletteBytes $Matches['palette'] 4))
# The unset relatedObj1 of $83:$02 reads ROM address Object.animParameter ($21).
# The clean bank-0 reset-vector byte is $00, so these bombs reveal immediately.
$fairyBank0 = Read-ImportText (Join-Path $Disassembly 'code\bank0.s')
if ($fairyBank0 -notmatch '(?s)\.ORGA \$0018\s*;[^\r\n]*\s+push bc\s+ld c,a\s+ld b,\$00\s+add hl,bc\s+add hl,bc\s+pop bc\s+ret\s+\.ORGA \$0038' -or
    (Read-ImportText (Join-Path $Disassembly 'include\emptyfill.s')) -notmatch '\.ifdef ROM_AGES\s+\.emptyfill \$00') {
    throw 'bank0.s: verify the $83:$02 null relatedObj1 ROM read at $0021.'
}
# PART_LIGHTNING $27:$01: native tables supplement its shared visual template.
$fairyLightning = Read-ImportText (Join-Path $Disassembly 'object_code\common\parts\lighting.s')
foreach ($pattern in @('and \$06','ld \(hl\),\$c0','ld a,\$06\s+jp setScreenShakeCounter',
    '(?s)@table_55e2:\s+\.db \$c0 \$d0\s+\.db \$e0 \$f0\s+\.db \$00',
    '(?s)@table_5603:\s+\.db \$02 \$06\s+\.db \$00 \$fb\s+\.db \$ff \$07\s+\.db \$fd \$fc\s+\.db \$00 \$05')) {
    if ($fairyLightning -notmatch $pattern) { throw "lighting.s: native PART_LIGHTNING contract changed: $pattern" }
}
$fairyPartAnimations = Read-ImportText (Join-Path $Disassembly 'data\ages\partAnimations.s')
$fairyLightningAnimation = [regex]::Match($fairyPartAnimations, '(?ms)^partAnimation5b9a7:\s*(?<body>(?:\s*\.db \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2}\s*)+)')
if (!$fairyLightningAnimation.Success) { throw 'Missing PART_LIGHTNING parameter animation.' }
$fairyLightningFrames = @([regex]::Matches($fairyLightningAnimation.Groups['body'].Value,
    '\.db \$(?<duration>[0-9a-f]{2}) \$[0-9a-f]{2} \$(?<parameter>[0-9a-f]{2})') | ForEach-Object {
    "$($_.Groups['duration'].Value):$($_.Groups['parameter'].Value)" }) -join ','
Write-GeneratedTable((Join-Path $destination 'effects\lightning_native.tsv'), @(
    '# z-offsets`tframes`tshake`tdebris-offsets', "c0,d0,e0,f0,00`t$fairyLightningFrames`t06`t02:06,00:fb,ff:07,fd:fc,00:05"))
