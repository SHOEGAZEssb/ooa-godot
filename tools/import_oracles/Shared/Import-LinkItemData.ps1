# Native Link/item presentation has common consumers, but different graphics
# indices and collision lists. Follow compiled pointers, including fallthrough.
function Convert-LinkSignedByte([int]$value) {
    if ($value -ge 0x80) { return $value - 0x100 }
    return $value
}
if ($soundIds['SND_SHIELD'] -ne 0x76) { throw 'music.s: native SND_SHIELD binding changed.' }
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $source = "data/$($pack.Name)/specialObjectAnimationData.s"
        $path = Join-Path $Disassembly $source
        $macros = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$path`0`0m_SpecialObjectGfxPointer" | ConvertFrom-Json) |
            Where-Object { $_.IsActive -and $_.EnclosingGlobalLabel -eq 'specialObject08GfxPointers' })
        $gfx = Read-MenuRomBytes $pack 'specialObject00GfxPointers' ($macros.Count * 3)
        if ($macros.Count -ne $(if ($pack.Name -eq 'ages') { 256 } else { 248 })) {
            throw "${source}:specialObject00GfxPointers: unsupported native graphics extent."
        }
        $entries = @()
        for ($index = 0; $index -lt $macros.Count; $index++) {
            $row = $macros[$index]
            if ($row.Operands.Count -ne 4 -or $row.Operands[1] -ne 'spr_link') {
                throw "${source}: graphics `$$($index.ToString('x2')) is not a canonical Link frame."
            }
            $oam = Convert-AssemblyInteger $row.Operands[0]
            $offset = Convert-AssemblyInteger $row.Operands[2]
            $size = Convert-AssemblyInteger $row.Operands[3]
            $expected = (($pack.Symbols['spr_link'] -band 0x3fff) + 0x4000 + $offset) -bor $size
            if ($gfx[$index * 3] -ne $oam -or [BitConverter]::ToUInt16($gfx, $index * 3 + 1) -ne $expected) {
                throw "${source}: graphics `$$($index.ToString('x2')) differs from compiled clean US data."
            }
            $entries += @{ Oam = $oam; Offset = $offset; Size = $size }
        }
        $linkOamLabels = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0specialObject09OamDataPointers`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        if ($linkOamLabels.Count -ne 48) { throw "${source}: expected 48 Link OAM pointers." }
        function Read-LinkOam([int]$index) {
            if ($index -lt 0 -or $index -ge 48) { throw "${source}: invalid Link OAM index `$$($index.ToString('x2'))." }
            $pointers = Read-MenuRomBytes $pack 'specialObject00OamDataPointers' 96
            $label = $linkOamLabels[$index].Operands[0]
            if (-not $pack.Symbols.ContainsKey($label) -or [BitConverter]::ToUInt16($pointers, $index * 2) -ne (($pack.Symbols[$label] -band 0x3fff) + 0x4000)) {
                throw "${source}: unresolved native Link OAM pointer `$$($index.ToString('x2')) to $label."
            }
            $count = [int](Read-MenuRomBytes $pack $label 1)[0]
            if ($count -lt 1 -or $count -gt 32) { throw "${source}:${label}: invalid Link OAM part count $count." }
            $bytes = Read-MenuRomBytes $pack $label (1 + $count * 4)
            $parts = for ($part = 0; $part -lt $count; $part++) {
                $offset = 1 + $part * 4
                @($bytes[$offset..($offset + 3)] | ForEach-Object { [int]$_ }) -join ','
            }
            return $parts -join ';'
        }
        $animationPointers = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0specialObject09AnimationDataPointers`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        if ($animationPointers.Count -ne 56) { throw "${source}: expected 56 native Link animation modes." }
        $nativePointers = Read-MenuRomBytes $pack 'specialObject00AnimationDataPointers' 112
        for ($index = 0; $index -lt 56; $index++) {
            $label = $animationPointers[$index].Operands[0]
            if (-not $pack.Symbols.ContainsKey($label) -or
                [BitConverter]::ToUInt16($nativePointers, $index * 2) -ne (($pack.Symbols[$label] -band 0x3fff) + 0x4000)) {
                throw "${source}: native animation pointer `$$($index.ToString('x2')) differs from $label."
            }
        }
        # Seasons' empty modes alias the OAM table; preserve native fallthrough
        # and signed loops within the complete selected ROM bank.
        $animationStart = [int]$pack.Symbols[$animationPointers[0].Operands[0]] -band 0xffffc000
        $animationEnd = $animationStart + 0x4000
        $animationBase = 0x4000
        $nativeAnimationRows = [Collections.Generic.List[string]]::new()
        $nativeAnimationRows.Add("# mode`tpointer`tsource")
        for ($mode = 0; $mode -lt 56; $mode++) {
            $label = $animationPointers[$mode].Operands[0]
            $pointer = [BitConverter]::ToUInt16($nativePointers, $mode * 2)
            if ([int]$pack.Symbols[$label] -lt $animationStart -or [int]$pack.Symbols[$label] -ge $animationEnd -or
                $pointer -lt $animationBase -or $pointer + 3 -gt 0x8000) {
                throw "${source}:${label}: Link mode `$$($mode.ToString('x2')) points outside its native stream."
            }
            $nativeAnimationRows.Add("$($mode.ToString('x2'))`t$($pointer.ToString('x4'))`t${source}:$label")
        }
        [byte[]]$nativeAnimationBytes = [BitConverter]::GetBytes([uint16]$animationBase) + $pack.Bytes[$animationStart..($animationEnd - 1)]
        [IO.File]::WriteAllBytes((Join-Path $pack.Root 'metadata/link_native_animation.bin'), $nativeAnimationBytes)
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/link_native_animation.tsv'), $nativeAnimationRows)
        function Read-LinkAnimation([int]$mode) {
            $label = $animationPointers[$mode].Operands[0]
            $frames = @()
            for ($frame = 0; $frame -lt 32; $frame++) {
                $bytes = Read-MenuRomBytes $pack $label (($frame + 1) * 3)
                $offset = $frame * 3
                $frames += @{ Duration = [int]$bytes[$offset]; Graphic = [int]$bytes[$offset + 1]; Parameter = [int]$bytes[$offset + 2] }
                if ($bytes[$offset] -eq 0x7f) { return ,$frames }
                if ($bytes[$offset] -eq 0 -or $bytes[$offset] -ge 0x80) { break }
            }
            throw "${source}:${label}: expected a finite Link/item animation."
        }
        $throws = [Collections.Generic.List[string]]::new()
        $throws.Add("# mode`tframe`tduration`tgraphic`tparameter`tsource")
        foreach ($mode in @(0x21,0x25)) {
            $frames = Read-LinkAnimation $mode
            if ($frames.Count -ne 2 -or $frames[0].Duration -ne 8 -or $frames[0].Parameter -ne 6 -or $frames[1].Parameter -ne 0x86) {
                throw "${source}: throw-parent mode $mode lost its native terminal pose."
            }
            $label = $animationPointers[$mode].Operands[0]
            for ($frame = 0; $frame -lt $frames.Count; $frame++) {
                $entry = $frames[$frame]
                $throws.Add("$($mode.ToString('x2'))`t$frame`t$($entry.Duration)`t$($entry.Graphic.ToString('x2'))`t$($entry.Parameter.ToString('x2'))`t${source}:$label+$frame")
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/item_throw_parent_animations.tsv'),$throws)
        $jump = Read-LinkAnimation 0x18
        $cape = if ($pack.Name -eq 'seasons') { Read-LinkAnimation 0x19 } else { @() }
        if (($jump.Duration -join ',') -ne '9,9,6,127' -or $jump[-1].Parameter -ne 0xff -or
            ($cape.Count -ne 0 -and (($cape.Duration -join ',') -ne '6,40,127' -or $cape[-1].Parameter -ne 0xff))) {
            throw "${source}: native jump/Cape animation contract changed."
        }
        function Read-LinkAirInstructions([string]$relative, [string]$label) {
            $nativePath = Join-Path $Disassembly $relative
            return (@((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$nativePath`0$label`0" | ConvertFrom-Json) |
                Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' } | ForEach-Object Code) -join "`n")
        }
        $airCode = Read-LinkAirInstructions 'object_code/common/specialObjects/link.s' 'linkUpdateInAir'
        $featherCode = Read-LinkAirInstructions 'object_code/common/itemParents/featherParent.s' 'parentItemCode_feather'
        $mountCode = Read-LinkAirInstructions 'object_code/common/specialObjects/commonCode.s' 'setLinkMountingSpeed'
        $dismountCode = Read-LinkAirInstructions 'object_code/common/specialObjects/commonCode.s' 'companionDismount'
        $airRows = [Collections.Generic.List[string]]::new(); $airRows.Add("# key`tvalue`tsource")
        foreach ($spec in @(
            @('gravity',32,$airCode,'ld c,\$20','link.s:linkUpdateInAir'),
            @('reduced-gravity',10,$airCode,'bit 5,\(hl\)[\s\S]*ld c,\$0a','link.s:linkUpdateInAir'),
            @('maximum-fall-speed',768,$airCode,'cp \$03[\s\S]*ld \(hl\),\$03[\s\S]*ld \(hl\),\$00','link.s:linkUpdateInAir'),
            @('jump-speed-z',-480,$featherCode,'ld bc,\$fe20','featherParent.s:parentItemCode_feather'),
            @('hole-standing-counter',4,$airCode,'ld a,\$04\s+ld \(wStandingOnTileCounter\),a','link.s:linkUpdateInAir'),
            @('companion-jump-speed-raw',20,$mountCode,'ld \(hl\),SPEED_80','commonCode.s:setLinkMountingSpeed'),
            @('companion-jump-speed-z',-448,$mountCode,'ld \(hl\),\$40\s+inc l\s+ld \(hl\),\$fe','commonCode.s:setLinkMountingSpeed'),
            @('companion-dismount-z',-8,$dismountCode,'ld \(hl\),\$f8','commonCode.s:companionDismount'),
            @('companion-dismount-angle',255,$dismountCode,'ld hl,w1Link.angle\s+ld \(hl\),\$ff','commonCode.s:companionDismount')
        )) {
            if ($spec[2] -notmatch $spec[3]) { throw "$($pack.Name): native air contract changed at $($spec[4]) ($($spec[0]))." }
            $airRows.Add("$($spec[0])`t$($spec[1])`t$($spec[4])")
        }
        $ice = Read-MenuRomBytes $pack 'updateLinkSpeed_withParam@speedTable' 16
        if ($ice[9] -ne 6 -or $mountCode -notmatch 'ld a,\$81\s+ld \(wLinkInAir\),a' -or
            $airCode -notmatch 'ld a,SND_JUMP\s+call playSound' -or $airCode -notmatch 'ld a,SND_LAND\s+call playSound') { throw "$($pack.Name): native air entry/ice/audio contract changed." }
        $airRows.Add("ice-velocity-interval`t$($ice[9])`tlink.s:updateLinkSpeed_withParam@speedTable")
        $airRows.Add("jump-sound`t$($soundIds['SND_JUMP'])`tlink.s:linkUpdateInAir")
        $airRows.Add("land-sound`t$($soundIds['SND_LAND'])`tlink.s:linkUpdateInAir")
        for ($phase = 0; $phase -lt 3; $phase++) { $airRows.Add("animation-phase-$phase`t$($jump[$phase].Duration)`t${source}:$($animationPointers[0x18].Operands[0])") }
        $capeEnabled = $pack.Name -eq 'seasons'
        if ($capeEnabled -and ($featherCode -notmatch 'cp \$02\s+ld a,\$41' -or
            $featherCode -notmatch 'ld bc,\$0100\s+call compareHlToBc\s+inc a\s+ret z' -or
            $featherCode -notmatch 'ld \(hl\),<\(-\$80\)' -or
            $featherCode -notmatch 'ld a,LINK_ANIM_MODE_ROCS_CAPE' -or
            $featherCode -notmatch 'set 5,\(hl\)' -or $featherCode -notmatch 'ld a,SND_THROW')) { throw 'Seasons featherParent.s: native Cape eligibility/threshold/glide changed.' }
        if (-not $capeEnabled -and $featherCode -match 'LINK_ANIM_MODE_ROCS_CAPE') { throw 'Ages Feather unexpectedly has a Cape parent.' }
        foreach ($spec in @(@('cape-level',2),@('cape-speed-z',-128),@('cape-minimum-speed-z',256),@('cape-phase-0',6),@('cape-phase-1',40),@('cape-sound',$soundIds['SND_THROW']))) {
            $value = if ($capeEnabled) { $spec[1] } else { 0 }
            $airRows.Add("$($spec[0])`t$value`tobject_code/common/itemParents/featherParent.s")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/top_down_air_constants.tsv'),$airRows)
        $linkPath = Join-Path $Disassembly 'object_code/common/specialObjects/link.s'
        $linkNodes = @((Invoke-AssemblySourceHost $connection 'NODES' $linkPath | ConvertFrom-Json) |
            Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' })
        $sideAir = ($linkNodes | Where-Object EnclosingGlobalLabel -eq 'linkUpdateInAir_sidescroll').Code -join "`n"
        $sideState = ($linkNodes | Where-Object EnclosingGlobalLabel -eq 'linkState01_sidescroll').Code -join "`n"
        $knockback = ($linkNodes | Where-Object EnclosingGlobalLabel -eq 'linkUpdateKnockback').Code -join "`n"
        $active = Read-LinkAirInstructions 'object_code/common/specialObjects/commonCode.s' 'sidescrollUpdateActiveTile'
        $spike = @(Read-AssemblyConstants (Join-Path $Disassembly 'constants/common/tileIndices.s') |
            Where-Object Name -eq 'TILEINDEX_SS_SPIKE')
        $platform = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/circularSidescrollPlatform.s')
        if ($ice[0] -ne 0x28 -or $platform -notmatch '@moveLinkAtAngle:\s+ld b,SPEED_80\s+jp updateLinkPositionGivenVelocity' -or
            $spike.Count -ne 1 -or (Convert-AssemblyInteger $spike[0].OperandText) -ne 2) {
            throw "$($pack.Name): native side-view movement/spike constants changed."
        }
        $sideRows = [Collections.Generic.List[string]]::new(); $sideRows.Add('# key`tvalue`tsource')
        foreach ($spec in @(
            @('gravity',36,$sideAir,'ld c,\$24','link.s:linkUpdateInAir_sidescroll'),
            @('reduced-gravity',14,$sideAir,'bit 5,a\s+jr z,\+\s+ld c,\$0e','link.s:linkUpdateInAir_sidescroll'),
            @('maximum-fall-speed',768,$sideAir,'cp \$03[\s\S]*ldi \(hl\),a\s+ld \(hl\),\$03','link.s:linkUpdateInAir_sidescroll'),
            @('jump-speed-z',-560,$featherCode,'cp FIRST_SIDESCROLL_GROUP\s+jr c,\+\s+ld bc,\$fdd0','featherParent.s:parentItemCode_feather'),
            @('water-exit-speed-z',-416,$sideState,'ld bc,-\$1a0\s+call objectSetSpeedZ','link.s:linkState01_sidescroll'),
            @('knockback-speed',50,$knockback,'ld b,SPEED_140','link.s:linkUpdateKnockback'),
            @('ground-wall-mask',48,$sideAir,'and \$30\s+jp nz,@landedOnGround','link.s:linkUpdateInAir_sidescroll'),
            @('ceiling-wall-mask',192,$sideAir,'and \$c0\s+jr nz,@applyGravity','link.s:linkUpdateInAir_sidescroll'),
            @('landing-high-mask',248,$sideAir,'and \$f8\s+add \$01','link.s:linkUpdateInAir_sidescroll'),
            @('landing-high-offset',1,$sideAir,'and \$f8\s+add \$01','link.s:linkUpdateInAir_sidescroll'),
            @('below-tile-offset',8,$active,'ld bc,\$0800\s+call objectGetRelativeTile','commonCode.s:sidescrollUpdateActiveTile'),
            @('bottom-boundary',169,$sideAir,'cp \$a9\s+jr c,@notLanded','link.s:linkUpdateInAir_sidescroll'),
            @('jump-sound',$soundIds['SND_JUMP'],$sideAir,'ld a,SND_JUMP\s+call playSound','link.s:linkUpdateInAir_sidescroll'),
            @('land-sound',$soundIds['SND_LAND'],$sideAir,'ld a,SND_LAND\s+call playSound','link.s:linkUpdateInAir_sidescroll')
        )) {
            if ($spec[2] -notmatch $spec[3]) { throw "$($pack.Name): native side-view contract changed at $($spec[4]) ($($spec[0]))." }
            $sideRows.Add("$($spec[0])`t$($spec[1])`t$($spec[4])")
        }
        $bottom = if ($pack.Name -eq 'ages') { 'cp \$a9\s+jr c,@notLanded\s+jr @landedOnGround' } else {
            'cp \$a9\s+jr c,@notLanded\s+ld a,\(wActiveTileType\)\s+cp TILETYPE_SS_LADDER\s+jr nz,@notLanded\s+ld a,\$80 \| DIR_DOWN\s+ld \(wScreenTransitionDirection\),a'
        }
        if ($sideAir -notmatch $bottom) { throw "$($pack.Name): native side-view bottom-boundary branch changed." }
        $sideRows.Add("bottom-ladder-transition`t$(if ($pack.Name -eq 'ages') { 0 } else { 130 })`tlink.s:linkUpdateInAir_sidescroll")
        $sideRows.Add("normal-speed`t$($ice[0])`tlink.s:updateLinkSpeed_withParam@speedTable")
        $sideRows.Add("ice-velocity-interval`t$($ice[9])`tlink.s:updateLinkSpeed_withParam@speedTable")
        $sideRows.Add("platform-push-speed`t20`tcircularSidescrollPlatform.s:sidescrollingPlatformCommon")
        $sideRows.Add("spike-tile`t2`ttileIndices.s:TILEINDEX_SS_SPIKE")
        $swim = Read-MenuRomBytes $pack 'linkSetSwimmingSpeed' 11
        if ($swim[5] -ne 0x3e -or $swim[6] -ne 0x23 -or $swim[9] -ne 0x3e -or $swim[10] -ne 0x14) {
            throw "$($pack.Name): native side-view Flippers speed operands changed."
        }
        $sideRows.Add("swim-speed`t$($swim[10])`tlink.s:linkSetSwimmingSpeed")
        $sideRows.Add("fast-swim-speed`t$($swim[6])`tlink.s:linkSetSwimmingSpeed")
        for ($phase = 0; $phase -lt 3; $phase++) { $sideRows.Add("animation-phase-$phase`t$($jump[$phase].Duration)`t${source}:$($animationPointers[0x18].Operands[0])") }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/side_scroll_constants.tsv'),$sideRows)
        $swing = Read-LinkAnimation 0x22; $spin = Read-LinkAnimation 0x23
        $cart = Read-LinkAnimation 0x26; $dig = Read-LinkAnimation 0x1a
        if (($swing.Duration -join ',') -ne '3,3,8,3,127' -or ($swing.Parameter -join ',') -ne '0,2,100,6,134' -or
            ($spin.Duration -join ',') -ne '12,4,4,4,8,127' -or ($cart.Duration -join ',') -ne '3,3,8,127' -or
            ($cart.Parameter -join ',') -ne '0,2,38,134' -or ($dig.Duration -join ',') -ne '4,4,15,127' -or
            ($dig.Parameter -join ',') -ne '0,1,0,255') {
            throw "${source}: native Sword, cart or Shovel timing/parameter contract changed."
        }
        $swordPath = Join-Path $Disassembly 'object_code/common/itemParents/swordParent.s'
        $swordNodes = @((Invoke-AssemblySourceHost $connection 'NODES' $swordPath | ConvertFrom-Json) | Where-Object IsActive)
        $swordCode = ($swordNodes | Where-Object { $_.Kind -eq 'Instruction' } | ForEach-Object Code) -join "`n"
        if ($swordCode -notmatch 'ld l,Item.counter1\s+ld \(hl\),\$28' -or
            $swordCode -notmatch 'ld a,\$05' -or $swordCode -notmatch 'ld a,\$09' -or
            $swordCode -notmatch 'ld \(hl\),\$0f' -or $swordCode -notmatch 'ld \(hl\),\$08') {
            throw 'swordParent.s: native charge/poke/spin control contract changed.'
        }
        $collisionPath = Join-Path $Disassembly 'code/collisionEffects.s'
        $collision = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$collisionPath`0collisionEffect1f`0" | ConvertFrom-Json) |
            Where-Object { $_.IsActive -and $_.Name -eq 'ldhl' })
        if ($collision.Count -ne 1 -or ($collision[0].Operands -join ',') -ne 'LINKDMG_20,ENEMYDMG_34') {
            throw 'collisionEffects.s: native Shield response binding changed.'
        }
        $constants = [Collections.Generic.List[string]]::new()
        $constants.Add('# sword-swing-frames`tsword-tile-hit-frame`tsword-restart-frame`tsword-charge-counter`tsword-poke-frames`tsword-spin-frames`tshovel-action-frames`tshovel-dig-frame`tshovel-second-pose-frame`tswing-phase-starts`tspin-phase-starts`tshield-sound`tshield-collision-effect`tshield-link-response`tshield-projectile-response`tprojectile-collision-mode`tring-projectile-collision-mode`tminecart-swing-arc-phases`tminecart-sword-swing-frames`tsource')
        $constants.Add("17`t6`t3`t40`t12`t23`t23`t4`t8`t0,3,6,14`t0,3,5,8,10,13,15,18,20`t76`t1f`t20`t34`t06`t07`t0,1,3,3`t14`t${source}:LINK_ANIM_MODE_22/23/26/DIG_2;object_code/common/itemParents/swordParent.s;code/collisionEffects.s:collisionEffect1f")
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/link_item_constants.tsv'), $constants)

        $graphics = [Collections.Generic.List[string]]::new()
        $graphics.Add('# kind`tvariant`tphase`tdirection`tgraphics-index`toam-index`tbyte-offset`toam`tsource')
        function Add-NativeLinkGraphic([string]$kind, [int]$variant, [int]$phase, [int]$direction, [int]$index) {
            if ($index -lt 0 -or $index -ge $entries.Count) { throw "${source}: unsupported $kind graphic `$$($index.ToString('x2'))." }
            $entry = $entries[$index]
            $graphics.Add("$kind`t$variant`t$phase`t$direction`t$($index.ToString('x2'))`t$($entry.Oam.ToString('x2'))`t$($entry.Offset.ToString('x4'))`t$(Read-LinkOam $entry.Oam)`t${source}:specialObject00GfxPointers")
        }
        for ($phase = 0; $phase -lt 3; $phase++) {
            for ($direction = 0; $direction -lt 4; $direction++) { Add-NativeLinkGraphic 'attack' 0 $phase $direction ($swing[$phase].Graphic + $direction) }
        }
        if ($pack.Name -eq 'ages') {
            for ($variant = 0; $variant -lt 3; $variant++) {
                for ($angle = 0; $angle -lt 8; $angle++) { Add-NativeLinkGraphic 'shooter' $variant 0 $angle (@(0x38,0x40,0x48)[$variant] + $angle) }
            }
            $underwater = Read-LinkAnimation 0x2d
            for ($phase = 0; $phase -lt 3; $phase++) {
                for ($direction = 0; $direction -lt 4; $direction++) { Add-NativeLinkGraphic 'underwater-attack' 0 $phase $direction ($underwater[$phase].Graphic + $direction) }
            }
        }
        for ($phase = 0; $phase -lt 4; $phase++) {
            for ($direction = 0; $direction -lt 4; $direction++) { Add-NativeLinkGraphic 'minecart-attack' 0 $phase $direction ($cart[$phase].Graphic + $direction) }
        }
        for ($phase = 0; $phase -lt 2; $phase++) {
            for ($direction = 0; $direction -lt 4; $direction++) { Add-NativeLinkGraphic 'shovel' 0 $phase $direction ($dig[$phase * 2].Graphic + $direction) }
        }
        $liftLabel = $animationPointers[0x14].Operands[0]
        $lift = Read-MenuRomBytes $pack $liftLabel 6
        for ($pose = 0; $pose -lt 3; $pose++) {
            for ($direction = 0; $direction -lt 4; $direction++) { Add-NativeLinkGraphic 'bracelet' $pose 0 $direction (@($lift[1],$lift[4],$swing[1].Graphic)[$pose] + $direction) }
        }
        for ($phase = 0; $phase -lt 2; $phase++) {
            for ($direction = 0; $direction -lt 4; $direction++) { Add-NativeLinkGraphic 'minecart' 0 $phase $direction (@(0x58,0x84)[$phase] + $direction) }
        }
        for ($variant = 0; $variant -lt 4; $variant++) {
            for ($phase = 0; $phase -lt 2; $phase++) {
                for ($direction = 0; $direction -lt 4; $direction++) { Add-NativeLinkGraphic 'shield' $variant $phase $direction (0x68 + $variant * 4 + $direction + $phase * 0x2c) }
            }
        }
        if ($capeEnabled) {
            for ($phase = 0; $phase -lt 3; $phase++) {
                for ($direction = 0; $direction -lt 4; $direction++) { Add-NativeLinkGraphic 'cape' 0 $phase $direction ($cape[$phase].Graphic + $direction) }
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/link_item_graphics.tsv'), $graphics)
        $flute = Read-LinkAnimation 0x1b
        if ($flute.Count -ne 7 -or ($flute.Parameter -join ',') -ne '0,0,64,1,1,1,255') {
            throw "${source}: LINK_ANIM_MODE_FLUTE parameters or frame count changed."
        }
        $fluteFrames = [Collections.Generic.List[string]]::new()
        $fluteFrames.Add('# index`tduration`tgraphics-index`tparameter`tsource-offset`toam`tsource')
        $fluteRows = [Collections.Generic.List[string]]::new()
        $fluteRows.Add('# kind`tindex`tvalue`tsource')
        $fluteSpawnerSource="constants/$($pack.Name)/interactions.s"
        $fluteSpawnerIdMatch=[regex]::Match((Read-ImportText (Join-Path $Disassembly $fluteSpawnerSource)),
            '(?m)^\.define\s+INTERAC_COMPANION_SPAWNER\s+\$(?<id>[0-9a-f]{2})\b')
        if (-not $fluteSpawnerIdMatch.Success) { throw "${fluteSpawnerSource}: missing native Flute caller identity." }
        $fluteSpawnerId=[Convert]::ToInt32($fluteSpawnerIdMatch.Groups['id'].Value,16)
        if ($fluteSpawnerId -ne $(if ($pack.Name -eq 'ages') {0x67} else {0x5f})) { throw "${fluteSpawnerSource}: native companion spawner ID changed." }
        $fluteRows.Add("spawner`t0`t$($fluteSpawnerId.ToString('x2'))`t${fluteSpawnerSource}:INTERAC_COMPANION_SPAWNER")
        $vram = [int[]]::new(256)
        for ($tile=0;$tile -lt 256;$tile++) { $vram[$tile]=-1 }
        for ($frame=0;$frame -lt $flute.Count;$frame++) {
            $entry=$flute[$frame]; $graphic=$entries[$entry.Graphic]
            for ($tile=0;$tile -lt $graphic.Size;$tile++) { $vram[$tile]=$graphic.Offset/16+$tile }
            $parts=@((Read-LinkOam $graphic.Oam).Split(';') | ForEach-Object {
                $fields=$_.Split(','); $tile=([int]$fields[2]) -band 0xfe
                if ($tile -ge 255 -or $vram[$tile] -lt 0 -or $vram[$tile+1] -ne $vram[$tile]+1) { throw "${source}: Flute frame $frame has unresolved retained VRAM." }
                "$($fields[0]),$($fields[1]),$($vram[$tile]),$($fields[3])"
            }) -join ';'
            $fluteFrames.Add("$frame`t$($entry.Duration)`t$($entry.Graphic.ToString('x2'))`t$($entry.Parameter.ToString('x2'))`t0000`t$parts`t${source}:LINK_ANIM_MODE_FLUTE;specialObject00GfxPointers/specialObject00OamDataPointers")
            $fluteRows.Add("parameter`t$frame`t$($entry.Parameter.ToString('x2'))`t${source}:LINK_ANIM_MODE_FLUTE")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/link_flute_animation.tsv'),$fluteFrames)
        $fluteCues=Read-MenuRomBytes $pack 'parentItemCode_harp@sfxList' 4
        if (($fluteCues -join ',') -ne '139,157,158,159') { throw "$($pack.Name): native Flute sound table changed." }
        for ($index=0;$index -lt 4;$index++) { $fluteRows.Add("sound`t$index`t$($fluteCues[$index].ToString('x2'))`tobject_code/common/itemParents/harpFluteParent.s:@sfxList") }
        $fluteTexts=if ($pack.Name -eq 'ages') { $allTexts } else { $seasonFluteTexts }
        foreach ($textId in @(0x510c,0x510f)) {
            $fluteRows.Add("text`t$textId`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($fluteTexts[$textId])))`ttext/$($pack.Name)/text.yaml:TX_$($textId.ToString('x4'))")
        }
        $callable=Read-MenuRomBytes $pack 'companionCallableRooms' 32
        for ($index=0;$index -lt 256;$index++) {
            if (($callable[$index -shr 3] -band (1 -shl ($index -band 7))) -ne 0) { $fluteRows.Add("room`t$index`t01`tdata/$($pack.Name)/companionCallableRooms.s:companionCallableRooms") }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'objects/flute.tsv'),$fluteRows)
        # Gale's alternating partial uploads retain cells from the preceding frame.
        $galeLabel = $animationPointers[3].Operands[0]
        $gale = Read-MenuRomBytes $pack $galeLabel 26
        if (($gale[24..25] -join ',') -ne '255,231') { throw "${source}:${galeLabel}: native eight-frame Gale loop changed." }
        $galeRows = [Collections.Generic.List[string]]::new()
        $galeRows.Add('# index`tduration`tgraphics-index`tsource-offset`toam`tsource')
        $vram = [int[]]::new(256)
        for ($tile = 0; $tile -lt 256; $tile++) { $vram[$tile] = -1 }
        for ($frame = 0; $frame -lt 8; $frame++) {
            $at = $frame * 3; $graphic = [int]$gale[$at + 1]
            if ($gale[$at] -ne 4 -or $gale[$at + 2] -ne 0 -or $graphic -ne 0x10 + $frame) { throw "${source}:${galeLabel}: native Gale frame $frame changed." }
            $entry = $entries[$graphic]
            for ($tile = 0; $tile -lt $entry.Size; $tile++) { $vram[$tile] = $entry.Offset / 16 + $tile }
            $parts = @((Read-LinkOam $entry.Oam).Split(';') | ForEach-Object {
                $fields = $_.Split(','); $tile = ([int]$fields[2]) -band 0xfe
                if ($tile -ge 255 -or $vram[$tile] -lt 0 -or $vram[$tile + 1] -ne $vram[$tile] + 1) { throw "${source}:${galeLabel}: Gale frame $frame references unresolved VRAM cell $tile." }
                "$($fields[0]),$($fields[1]),$($vram[$tile]),$($fields[3])"
            }) -join ';'
            $galeRows.Add("$frame`t$([int]$gale[$at])`t$($graphic.ToString('x2'))`t0000`t$parts`t${source}:${galeLabel};specialObject00GfxPointers/specialObject00OamDataPointers")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/link_gale_animation.tsv'),$galeRows)
        $fallLabel = $animationPointers[4].Operands[0]
        $fall = Read-MenuRomBytes $pack $fallLabel 11
        if (($fall[9..10] -join ',') -ne '255,246') { throw "${source}:${fallLabel}: native three-frame fall loop changed." }
        $fallRows = [Collections.Generic.List[string]]::new()
        $fallRows.Add('# index`tduration`tgraphics-index`tsource-offset`toam`tsource')
        $vram = [int[]]::new(256)
        for ($tile = 0; $tile -lt 256; $tile++) { $vram[$tile] = -1 }
        for ($frame = 0; $frame -lt 3; $frame++) {
            $at = $frame * 3; $graphic = [int]$fall[$at + 1]
            $firstGraphic = if ($pack.Name -eq 'ages') { 0xe4 } else { 0xd8 }
            if ($fall[$at] -ne 4 -or $fall[$at + 2] -ne 0 -or $graphic -ne $firstGraphic + 4 * $frame) { throw "${source}:${fallLabel}: native fall frame $frame changed." }
            # Ordinary Link's graphics loader adds DIR_DOWN to directional indices.
            $graphic += 2; $entry = $entries[$graphic]
            for ($tile = 0; $tile -lt $entry.Size; $tile++) { $vram[$tile] = $entry.Offset / 16 + $tile }
            $parts = @((Read-LinkOam $entry.Oam).Split(';') | ForEach-Object {
                $fields = $_.Split(','); $tile = ([int]$fields[2]) -band 0xfe
                if ($tile -ge 255 -or $vram[$tile] -lt 0 -or $vram[$tile + 1] -ne $vram[$tile] + 1) { throw "${source}:${fallLabel}: unresolved fall VRAM cell $tile." }
                "$($fields[0]),$($fields[1]),$($vram[$tile]),$($fields[3])"
            }) -join ';'
            $fallRows.Add("$frame`t$([int]$fall[$at])`t$($graphic.ToString('x2'))`t0000`t$parts`t${source}:${fallLabel};specialObject00GfxPointers/specialObject00OamDataPointers")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/link_fall_animation.tsv'),$fallRows)
        $landing = Read-LinkAirInstructions 'object_code/common/specialObjects/link.s' 'warpTransition5_01'
        $readsTile = [int]($landing -match 'call objectGetTileAtPosition')
        $trampoline = 0; $groupMask = 0; $groupValue = 0
        if ($pack.Name -eq 'seasons') {
            if ($readsTile -ne 1 -or $landing -notmatch 'cp TILEINDEX_TRAMPOLINE\s+jr z,@trampoline') { throw 'Seasons warpTransition5_01 lost its native tile/trampoline check.' }
            $bytes = Read-MenuRomBytes $pack 'warpTransition5_01' 14
            $group = Read-MenuRomBytes $pack 'warpTransition5_01@trampoline' 7
            if ($bytes[9] -ne 0xcd -or $bytes[12] -ne 0xfe -or $group[0] -ne 0xfa -or $group[3] -ne 0xe6 -or $group[5] -ne 0xfe) { throw 'Seasons fall landing instruction layout changed.' }
            $trampoline = $bytes[13]; $groupMask = $group[4]; $groupValue = $group[6]
        }
        elseif ($readsTile -ne 0) { throw 'Clean-US Ages must retain its omitted landing tile read.' }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/link_fall_landing.tsv'),@(
            '# reads-tile`ttrampoline-tile`tgroup-mask`tgroup-value`tsource',
            "$readsTile`t$($trampoline.ToString('x2'))`t$($groupMask.ToString('x2'))`t$($groupValue.ToString('x2'))`tobject_code/common/specialObjects/link.s:warpTransition5_01"))
        if ($pack.Symbols['spr_dungeon_sprites'] - $pack.Symbols['spr_link'] -ne 0x22e0) { throw "${source}: unsupported canonical spr_link extent." }
        Write-GeneratedBytes((Join-Path $pack.Root 'gfx/spr_link.2bpp'), (Read-MenuRomBytes $pack 'spr_link' 0x22e0))
        Copy-GeneratedFile 'gfx/common/spr_swords.png' 'gfx/spr_swords.png' $pack.Root

        $offsets = [Collections.Generic.List[string]]::new()
        $offsets.Add('# kind`tindex`tsubindex`toffset-y`toffset-x`tradius-y`tradius-x`tsource')
        for ($direction = 0; $direction -lt 4; $direction++) {
            $first = (Read-LinkOam (8 + $direction)).Split(';')[0].Split(',')
            $offsets.Add("attack-pose`t$direction`t0`t$((Convert-LinkSignedByte ([int]$first[0])) - 8)`t$(Convert-LinkSignedByte ([int]$first[1]))`t0`t0`t${source}:specialObject00OamDataPointers")
        }
        foreach ($spec in @(
            @('shovel-child','parentItemCode_shovel@offsets',4,1,2),
            @('shield-collision','checkEnemyAndPartCollisions@shieldPositionOffsets',4,1,4),
            @('bracelet-lift','updateGrabbedObjectPosition@liftedObjectPositions',4,4,2),
            @('sword-tile','tryBreakTileWithSword@linkOffsets',9,1,2)
        )) {
            $bytes = Read-MenuRomBytes $pack $spec[1] ($spec[2] * $spec[3] * $spec[4])
            for ($index = 0; $index -lt $spec[2]; $index++) {
                for ($subindex = 0; $subindex -lt $spec[3]; $subindex++) {
                    $offset = ($index * $spec[3] + $subindex) * $spec[4]
                    $ry = 0; $rx = 0
                    if ($spec[4] -eq 4) { $ry = $bytes[$offset + 2]; $rx = $bytes[$offset + 3] }
                    $offsets.Add("$($spec[0])`t$index`t$subindex`t$(Convert-LinkSignedByte $bytes[$offset])`t$(Convert-LinkSignedByte $bytes[$offset + 1])`t$ry`t$rx`t$($spec[1])")
                }
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/link_item_offsets.tsv'), $offsets)

        $presentation = [Collections.Generic.List[string]]::new()
        $presentation.Add('# kind`tindex`tsubindex`tvalue-a`tvalue-b`tvalue-c`tvalue-d`tsource')
        $swordAnimation = Read-MenuRomBytes $pack 'label_07_227@data' 24
        for ($direction = 0; $direction -lt 4; $direction++) {
            for ($phase = 0; $phase -lt 4; $phase++) { $presentation.Add("animation`t$direction`t$phase`t$($swordAnimation[$direction * 4 + $phase] -band 7)`t0`t0`t0`tobject_code/common/items/postUpdate.s:updateSwingableItemAnimation.@data") }
        }
        $arcs = Read-MenuRomBytes $pack 'swordArcData' 112
        for ($index = 0; $index -lt 28; $index++) {
            $offset = $index * 4
            $presentation.Add("arc`t$index`t0`t$($arcs[$offset])`t$($arcs[$offset + 1])`t$(Convert-LinkSignedByte $arcs[$offset + 2])`t$(Convert-LinkSignedByte $arcs[$offset + 3])`tobject_code/common/items/postUpdate.s:swordArcData")
        }
        $sounds = Read-MenuRomBytes $pack 'itemCode05@swordSounds' 8
        for ($index = 0; $index -lt 8; $index++) { $presentation.Add("sound`t$index`t0`t$($sounds[$index].ToString('x2'))`t0`t0`t0`tobject_code/common/items/sword.s:@swordSounds") }
        $oamPointers = Read-MenuRomBytes $pack 'item05OamDataPointers' 16
        $itemAnimationsPath = Join-Path $Disassembly 'data/itemAnimations.s'
        $swordOamLabels = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$itemAnimationsPath`0item07OamDataPointers`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        if ($swordOamLabels.Count -ne 8) { throw 'data/itemAnimations.s: expected eight native Sword OAM pointers.' }
        for ($animation = 0; $animation -lt 8; $animation++) {
            $label = $swordOamLabels[$animation].Operands[0]
            if (-not $pack.Symbols.ContainsKey($label) -or [BitConverter]::ToUInt16($oamPointers, $animation * 2) -ne (($pack.Symbols[$label] -band 0x3fff) + 0x4000)) {
                throw "data/itemAnimations.s: unresolved native Sword OAM pointer $animation to $label."
            }
            $count = [int](Read-MenuRomBytes $pack $label 1)[0]
            if ($count -ne @(1,2,2,2,1,2,2,2)[$animation]) { throw "data/itemOamData.s: malformed native Sword composition $animation." }
            $bytes = Read-MenuRomBytes $pack $label (1 + $count * 4)
            for ($part = 0; $part -lt $count; $part++) {
                $offset = 1 + $part * 4
                $presentation.Add("oam`t$animation`t$part`t$(Convert-LinkSignedByte $bytes[$offset])`t$(Convert-LinkSignedByte $bytes[$offset + 1])`t$($bytes[$offset + 2])`t$($bytes[$offset + 3].ToString('x2'))`tdata/itemOamData.s:$label")
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/sword_presentation.tsv'), $presentation)

        $clinks = [Collections.Generic.List[string]]::new()
        $clinks.Add('# collision-set`tkind`tlist-id`torder`ttile`tterminal`tsource')
        $source = "data/$($pack.Name)/tile_properties/clinkSounds.s"
        $path = Join-Path $Disassembly $source
        $pointers = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0clinkSoundTable`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        if ($pointers.Count -ne 6) { throw "${source}: expected six clink collision sets." }
        $native = Read-MenuRomBytes $pack 'clinkSoundTable' 12
        for ($collision = 0; $collision -lt 6; $collision++) {
            $list = $pointers[$collision].Operands[0].TrimStart('@')
            $label = "clinkSoundTable@$list"
            $address = [int]$pack.Symbols[$label]
            if ([BitConverter]::ToUInt16($native, $collision * 2) -ne (($address -band 0x3fff) + 0x4000)) { throw "${source}: unresolved collision list $collision." }
            # Preserve aliases using the first table entry with this address.
            for ($previous = 0; $previous -lt $collision; $previous++) {
                if ([BitConverter]::ToUInt16($native, $previous * 2) -eq [BitConverter]::ToUInt16($native, $collision * 2)) {
                    $list = $pointers[$previous].Operands[0].TrimStart('@'); break
                }
            }
            foreach ($kind in @('bombable','silent')) {
                $terminal = $false
                for ($order = 0; $order -lt 32; $order++) {
                    if ($address -ge ([int]$pack.Symbols['clinkSoundTable'] + $(if ($pack.Name -eq 'ages') { 41 } else { 44 }))) {
                        throw "${source}:${label}: missing native $kind list terminator."
                    }
                    $tile = [int]$pack.Bytes[$address++]; $terminal = $tile -eq 0
                    $clinks.Add("$collision`t$kind`t$list`t$order`t$($tile.ToString('x2'))`t$([int]$terminal)`t${source}:clinkSoundTable")
                    if ($terminal) { break }
                }
                if (-not $terminal) { throw "${source}:${label}: invalid native $kind list extent." }
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/sword_clink_tiles.tsv'), $clinks)
    } finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
