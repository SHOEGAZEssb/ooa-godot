# Common runtime effects use each game's native interaction/animation pointers.
$sharedEffectAnimations = @{}
$floatingSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/floatingImage.s')
foreach ($guard in @('call interactionSetAlwaysUpdateBit','ld b,\$03[\s\S]*ld b,\$1d',
    'ld \(hl\),SPEED_60','ld \(hl\),70','call interactionDecCounter1\s+jp z,interactionDelete',
    'call objectApplySpeed\s+ld e,Interaction.var30\s+ld \(de\),a')) {
    if ($floatingSource -notmatch $guard) { throw "floatingImage.s: native motion/lifetime contract changed ($guard)." }
}
$shovelSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/items/shovel.s')
foreach ($guard in @('call itemIncState\s+ld l,Item.counter1\s+ld \(hl\),\$04','ld a,BREAKABLETILESOURCE_SHOVEL\s+call itemTryToBreakTile','ld a,\$01\s+call addToGashaMaturity\s+ld a,SND_DIG','call itemDecCounter1\s+ret nz\s+jp itemDelete')) {
    if ($shovelSource -notmatch $guard) { throw "shovel.s: native reserved-child lifetime/tile/maturity contract changed ($guard)." }
}
$debrisSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/breakTileDebris.s')
foreach ($guard in @('ld \(hl\),SPEED_80','ld c,\$60\s+call objectUpdateSpeedZ_paramC\s+call objectApplySpeed','ld bc,-\$240\s+call objectSetSpeedZ','ld e,Interaction.direction\s+ld a,\(de\)\s+jp interactionSetAnimation')) {
    if ($debrisSource -notmatch $guard) { throw "breakTileDebris.s: native directional debris motion changed ($guard)." }
}
$beamSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/items/swordBeam.s')
foreach ($guard in @('ld \(hl\),SPEED_300', 'ld a,SND_SWORDBEAM\s+jp playSound', 'call objectApplySpeed\s+call objectCheckTileCollision_allowHoles', 'call itemCheckCanPassSolidTile', 'ldbc INTERAC_CLINK, \$81', 'and \$03', 'xor \$01')) {
    if ($beamSource -notmatch $guard) { throw "object_code/common/items/swordBeam.s: shared beam behavior changed ($guard)." }
}
$bubbleSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/bubble.s')
foreach ($guard in @(
    '(?s)@@state0:.*?call @checkDelete.*?call interactionInitGraphics.*?ld \(hl\),SPEED_80.*?ld a,\$04.*?inc a.*?call getRandomNumber\s+and \$01.*?and TILESETFLAG_SIDESCROLL\s+jp nz,objectSetVisible83',
    '(?s)@@state1:.*?call @checkDelete.*?call objectApplySpeed.*?cp \$f0.*?call interactionDecCounter1.*?ld \(hl\),\$04.*?ld \(hl\),\$08.*?cpl\s+inc a.*?and \$1f',
    '(?s)@@sidescrolling:\s*;[^\r\n]*\s*call objectGetTileAtPosition\s+ld hl,hazardCollisionTable\s+call lookupCollisionTable\s+ccf\s+ret'
)) {
    if ($bubbleSource -notmatch $guard) { throw "bubble.s: native INTERAC_BUBBLE `$91:`$00 side-view gate, RNG or 4/5/8 turning contract changed ($guard)." }
}
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent) }
)) {
    $presentation = Read-InteractionPresentation $pack.Name
    # The common spinner and its arrow use all four animations from this pack.
    $spinner = Read-MenuRomBytes $pack 'interaction7dSubidData' 9
    $spinnerGfx = if ($pack.Name -eq 'ages') { 0x73 } else { 0x58 }
    $spinnerSprite = if ($pack.Name -eq 'ages') { 'spr_spinner_seedbouncer' } else { 'spr_spinner_trampoline' }
    if (($spinner -join ',') -ne "$spinnerGfx,0,64,$spinnerGfx,0,81,$spinnerGfx,128,2") {
        throw "$($pack.Name): interaction7dSubidData native parent/arrow graphics changed."
    }
    $spinnerAnimations = @(0..3 | ForEach-Object { Resolve-InteractionPresentation $presentation 0x7d $_ })
    $spinnerEncoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($spinnerAnimations -join "`n"))
    Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_spinner_visual.tsv'), @(
        '# sprite`ttile-base`tpalette`tsource-grayscale-inverted`tanimations-base64`tsource',
        "$spinnerSprite`t0`t4`t1`t$spinnerEncoded`tdata/$($pack.Name)/interactionData.s:interaction7dSubidData;interactionAnimations.s:interaction7dAnimations"))
    Copy-GeneratedFile "gfx_compressible/$($pack.Name)/$spinnerSprite.png" "gfx/$spinnerSprite.png" $pack.Root
    $mark=Read-MenuRomBytes $pack 'interaction9fSubidData' 3
    $markAnimation=Resolve-InteractionPresentation $presentation 0x9f 0
    $markSprite=if ($pack.Name -eq 'ages') {'spr_zz_bubble_exclamation_heart_kid'} else {'spr_zz_bubble_exclamation_heart'}
    $markGraphics=if ($pack.Name -eq 'ages') {0x45} else {0x38}
    if ($mark[0] -ne $markGraphics -or $mark[1] -ne 8 -or $mark[2] -ne 0x50 -or
        $markAnimation -ne '8@8,0,0,0;8,8,0,32|8@8,0,2,0;8,8,2,32') {
        throw "$($pack.Name): interaction9fSubidData native exclamation graphics/animation changed."
    }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/exclamation_mark.tsv'),@(
        '# sprite`ttile-base`tpalette`tanimation`tsource',
        "$markSprite`t$($mark[1])`t$($mark[2] -shr 4)`t$markAnimation`tdata/$($pack.Name)/interactionData.s:interaction9fSubidData;interactionAnimations.s:interaction9fAnimations"))
    Copy-GeneratedFile "gfx_compressible/$($pack.Name)/$markSprite.png" "gfx/$markSprite.png" $pack.Root
    $note=Read-MenuRomBytes $pack 'interactiona0SubidData' 6
    $noteAnimation=Resolve-InteractionPresentation $presentation 0xa0 0
    # m_InteractionSubidDataEnd sets the final tile byte's continuation bit.
    if (($note[3..5] -join ',') -ne '0,196,144' -or $noteAnimation -ne '127@8,4,0,0') {
        throw "$($pack.Name): INTERAC_FLOATING_IMAGE `$a0:`$01 native music-note graphics changed."
    }
    $sway=Read-MenuRomBytes $pack 'interactionCodea0@xOffsets' 8
    if (($sway -join ',') -ne '255,254,255,0,1,2,1,0') { throw "$($pack.Name): floating-image sway changed." }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/music_note.tsv'), @(
        '# tile-base`tpalette`tduration`tspeed-raw`tleft-angle`tright-angle`tsway`tanimation`tsource',
        "68`t1`t70`t0f`t1d`t03`t-1,-2,-1,0,1,2,1,0`t$noteAnimation`tobject_code/common/interactions/floatingImage.s:interactionCodea0"
    ))
    $bubble = Read-MenuRomBytes $pack 'interactionData' (0x92 * 3)
    $animation = Resolve-InteractionPresentation $presentation 0x91 0
    if ($bubble[0x91 * 3] -ne 0 -or $bubble[0x91 * 3 + 1] -ne 0x16 -or $bubble[0x91 * 3 + 2] -ne 0x90 -or
        $animation -ne '127@8,4,0,0') { throw "$($pack.Name): native INTERAC_BUBBLE `$91:`$00 graphics/animation changed." }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/side_scroll_bubble.tsv'), @(
        '# tile-base`tpalette`tspeed-raw`tturn-counter`tinitial-turns`tturns`tanimation`tsource',
        "22`t1`t20`t4`t5`t8`t$animation`tobject_code/common/interactions/bubble.s:interactionCode91@subid00"
    ))
    $graphics = Read-MenuRomBytes $pack 'interactionData' 39
    $cues = Read-MenuRomBytes $pack 'interactionCode0c@soundAndPriorityTable' 26
    $puff = Resolve-InteractionPresentation $presentation 0x05 0
    if ($graphics[15] -ne 0 -or $graphics[16] -ne 0x16 -or $graphics[17] -ne 0xb0 -or [string]::IsNullOrWhiteSpace($puff)) { throw "$($pack.Name): native INTERAC_PUFF graphics/animation changed." }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/puzzle_puff.tsv'),@("# tile-base`tpalette`tanimation", "22`t3`t$puff"))
    $killFrames=[Collections.Generic.List[string]]::new()
    $terminal=$false; $duration=0
    foreach ($frame in (Resolve-InteractionPresentation $presentation 0x08 0).Split('|')) {
        $metadata=$frame.Split('@')[0].Split(',')
        if ($metadata.Count -gt 1 -and ([int]$metadata[1] -band 0x80) -ne 0) { $terminal=$true; break }
        $duration += [int]$metadata[0]; $killFrames.Add($frame)
    }
    if ($graphics[24] -ne 0 -or $graphics[25] -ne 0x10 -or $graphics[26] -ne 0xb0 -or
        $cues[16] -ne $soundIds['SND_KILLENEMY'] -or $cues[17] -ne 0 -or
        -not $terminal -or $killFrames.Count -ne 7 -or $duration -ne 20) {
        throw "$($pack.Name): INTERAC_KILLENEMYPUFF graphics/cue/terminal or seven-frame20-update contract changed."
    }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/kill_enemy_puff.tsv'),@(
        "# tile-base`tpalette`tanimation", "16`t3`t$($killFrames -join '|')"))
    $snow = Resolve-InteractionPresentation $presentation 0x09 0
    if ($graphics[27] -ne 0 -or $graphics[28] -ne 0x1e -or $graphics[29] -ne 0x90 -or $cues[18] -ne 0 -or $cues[19] -ne 3 -or [string]::IsNullOrWhiteSpace($snow)) { throw "$($pack.Name): native INTERAC_SNOWDEBRIS graphics/animation changed." }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/snow_debris.tsv'),@("# tile-base`tpalette`tanimation", "30`t1`t$snow"))
    if ($graphics[30] -ne 0 -or $graphics[31] -ne 0x42 -or $graphics[32] -ne 0xb0 -or $cues[20] -ne 0 -or $cues[21] -ne 3) { throw "$($pack.Name): native INTERAC_SHOVELDEBRIS graphics/cue/priority changed." }
    $shovel = [Collections.Generic.List[string]]::new()
    $shovel.Add("# direction`ttile-base`tpalette`tspeed-raw`tinitial-speed-z`tgravity`tanimation`tsource")
    for ($direction = 0; $direction -lt 4; $direction++) {
        $animation = Resolve-InteractionPresentation $presentation 0x0a $direction
        if ([string]::IsNullOrWhiteSpace($animation) -or $animation -notmatch '^14@.*\|127,255@') { throw "$($pack.Name): native Shovel debris animation $direction lost its finite 14-update frame." }
        $shovel.Add("$direction`t66`t3`t20`t-576`t96`t$animation`tobject_code/common/interactions/breakTileDebris.s:interactionCode0a")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/shovel_debris.tsv'),$shovel)
    $grass = [Collections.Generic.List[string]]::new()
    $grass.Add("# interaction-id`tsprite`ttile-base`tpalette`tunderwater-palette`tsound`tanimation")
    $rock = [Collections.Generic.List[string]]::new()
    $rock.Add("# interaction-id`tsprite`ttile-base`tpalette`tsound`tanimation")
    foreach ($spec in @(
        @(0x00,0x00,0,'SND_CUTGRASS',3), @(0x01,0x00,0,'SND_CUTGRASS',3),
        @(0x03,0x04,1,'SND_SPLASH',3), @(0x04,0x26,2,'SND_SPLASH',3),
        @(0x06,0x02,3,'SND_BREAK_ROCK',0), @(0x07,0x10,1,'SND_CLINK',0), @(0x0c,0x40,5,'SND_BREAK_ROCK',0)
    )) {
        $id = $spec[0]
        if ($graphics[$id * 3] -ne 0 -or $graphics[$id * 3 + 1] -ne $spec[1] -or
            $graphics[$id * 3 + 2] -ne (0x80 -bor ($spec[2] -shl 4)) -or
            $cues[$id * 2] -ne $soundIds[$spec[3]] -or $cues[$id * 2 + 1] -ne $spec[4]) {
            throw "$($pack.Name): interactionData/breakTileDebris cue/priority contract changed for INTERAC `$$($id.ToString('x2'))."
        }
        $animation = Resolve-InteractionPresentation $presentation $id 0
        if ([string]::IsNullOrWhiteSpace($animation)) { throw "$($pack.Name): missing native INTERAC `$$($id.ToString('x2')) animation." }
        if ($id -le 1) {
            $underwater = if ($pack.Name -eq 'ages' -and $id -eq 0) { 6 } else { 0 }
            $grass.Add("$($id.ToString('x2'))`tspr_common_sprites`t0`t0`t$underwater`t$($cues[$id * 2].ToString('x2'))`t$animation")
        } elseif ($id -in @(6,12)) {
            $rock.Add("$($id.ToString('x2'))`tspr_common_sprites`t$($spec[1])`t$($spec[2])`t$($cues[$id * 2].ToString('x2'))`t$animation")
        }
        # Splash/clink currently use fixed common OAM compositions. Their
        # native streams must match both packs before those consumers share it.
        if ($pack.Name -eq 'ages') { $sharedEffectAnimations[$id] = $animation }
        elseif ($id -in @(3,4,7) -and $animation -ne $sharedEffectAnimations[$id]) {
            throw "$($pack.Name): INTERAC `$$($id.ToString('x2')) no longer matches its shared splash/clink animation."
        }
    }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/grass_debris.tsv'), $grass)
    Write-GeneratedTable((Join-Path $pack.Root 'effects/rock_debris.tsv'), $rock)
    $hole = Read-MenuRomBytes $pack 'interactionData' 48
    $animation = Resolve-InteractionPresentation $presentation 0x0f 0
    if ($hole[45] -ne 0 -or $hole[46] -ne 0x16 -or $hole[47] -ne 0xb0 -or [string]::IsNullOrWhiteSpace($animation)) { throw "$($pack.Name): native INTERAC_FALLDOWNHOLE graphics/animation changed." }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/fall_down_hole.tsv'), @("# tile-base`tpalette`tspeed-raw`tanimation", "22`t3`t15`t$animation"))
    $dust = Resolve-InteractionPresentation $presentation 0x0f 1
    if ($dust -ne '4@8,0,6,0;8,8,6,96|4@8,4,8,0|4@8,4,0,0|127,255@8,4,0,0') { throw "$($pack.Name): native INTERAC_FALLDOWNHOLE `$0f:`$01 animation/terminal parameter changed." }
    $dustSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/fallDownHole.s')
    if ($dustSource -notmatch '(?ms)^@dust:\s+call interactionSetAnimation\s+jp objectSetVisible80' -or
        $dustSource -notmatch '(?ms)^@interac0f_state2:.*?xor \$80.*?bit 7,\(hl\).*?jr z,@animate') { throw 'fallDownHole.s: native dust initialization/flicker/terminal contract changed.' }
    Write-GeneratedTable((Join-Path $pack.Root 'effects/knockback_dust.tsv'), @("# tile-base`tpalette`tanimation", "22`t3`t$dust"))

    $itemData = Read-MenuRomBytes $pack 'itemData' (0x28 * 3)
    $attributes = Read-MenuRomBytes $pack 'itemAttributes' (0x28 * 4)
    if ($attributes[0x15 * 4] -ne $(if ($pack.Name -eq 'ages') { 0x8c } else { 0x8d }) -or
        $attributes[0x15 * 4 + 1] -ne 0x33 -or $attributes[0x15 * 4 + 2] -ne 0xff) { throw "$($pack.Name): native Shovel collision identity/radius/damage changed." }
    $offsets = Read-MenuRomBytes $pack 'itemCode27@initialOffsetsTable' 12
    $pointers = Read-MenuRomBytes $pack 'item27OamDataPointers' 8
    $source = Join-Path $Disassembly 'data/itemAnimations.s'
    $labels = @(Read-AssemblyDataDirectives $source 'item27OamDataPointers' '.dw' | ForEach-Object { $_.Operands })
    if ($labels.Count -ne 4 -or $itemData[0x27 * 3] -ne 0 -or $itemData[0x27 * 3 + 1] -ne 0x38 -or
        $itemData[0x27 * 3 + 2] -ne 0x0c -or $attributes[0x27 * 4 + 1] -ne 0x22 -or
        $attributes[0x27 * 4 + 2] -ne 0xfe) { throw "$($pack.Name): native ITEM_SWORD_BEAM graphics/geometry changed." }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# direction`toffset-y`toffset-x`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage`tspeed-raw`tsound`toam")
    for ($direction = 0; $direction -lt 4; $direction++) {
        $label = $labels[$direction]
        if (-not $pack.Symbols.ContainsKey($label) -or [BitConverter]::ToUInt16($pointers, $direction * 2) -ne (($pack.Symbols[$label] -band 0x3fff) + 0x4000)) { throw "${source}: unresolved native Sword beam OAM pointer $label." }
        $count = [int](Read-MenuRomBytes $pack $label 1)[0]
        if ($count -ne (1 + ($direction -band 1))) { throw "${source}:${label}: invalid native beam OAM part count." }
        $bytes = Read-MenuRomBytes $pack $label (1 + 4 * $count)
        $parts = for ($part = 0; $part -lt $count; $part++) { @($bytes[(1 + $part * 4)..(4 + $part * 4)] | ForEach-Object { [int]$_ }) -join ',' }
        $y = [int]$offsets[$direction * 3]; if ($y -ge 128) { $y -= 256 }
        $x = [int]$offsets[$direction * 3 + 1]; if ($x -ge 128) { $x -= 256 }
        $rows.Add("$direction`t$y`t$x`tspr_common_sprites`t56`t4`t2`t2`t2`t78`t$($soundIds['SND_SWORDBEAM'].ToString('x2'))`t$($parts -join ';')")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/sword_beam.tsv'), $rows)
}
