# Resolve common swimming through each native build. Mermaid Suit operands
# and frames exist only in Ages' configured source.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $linkPath = Join-Path $Disassembly 'object_code/common/specialObjects/link.s'
        $code = @{}
        foreach ($label in @('overworldSwimmingState1','linkSetSwimmingSpeed','linkUpdateFlippersSpeed','linkUpdateDiving')) {
            $code[$label] = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$linkPath`0$label`0" | ConvertFrom-Json) |
                Where-Object { $_.IsActive -and $_.Kind -in @('Instruction','MacroInvocation') }).Code -join "`n"
        }
        foreach ($guard in @(
            @('overworldSwimmingState1','call linkCancelAllItemUsage\s+call linkSetSwimmingSpeed.*?ld l,SpecialObject.counter1\s+ld \(hl\),\$0a'),
            @('linkSetSwimmingSpeed','ld a,SWIMMERS_RING.*?ld a,SPEED_e0.*?ld a,SPEED_80.*?ldi \(hl\),a\s+ldi \(hl\),a.*?ld a,\$03\s+ld \(hl\),a'),
            @('linkUpdateFlippersSpeed','and BTN_A.*?ld a,\$08.*?call func_5933.*?ld a,\$0d.*?SND_LINK_SWIM.*?ldbc \$01,\$05.*?ldbc \$ff,-5.*?ld a,\$0c'),
            @('linkUpdateDiving','bit BTN_BIT_B,a.*?ld a,ZORA_RING.*?res 7,\(hl\).*?LINK_ANIM_MODE_SWIM.*?set 7,\(hl\).*?ld a,\$78.*?linkCreateSplash.*?LINK_ANIM_MODE_DIVE')
        )) {
            if ($code[$guard[0]] -notmatch "(?s)$($guard[1])") { throw "$($pack.Name): link.s:$($guard[0]) native swimming contract changed." }
        }
        $speeds = Read-MenuRomBytes $pack 'linkSetSwimmingSpeed' 11
        if ($speeds[5] -ne 0x3e -or $speeds[6] -ne 0x23 -or $speeds[9] -ne 0x3e -or $speeds[10] -ne 0x14) { throw "$($pack.Name): compiled Flippers speed operands changed." }
        $source = "data/$($pack.Name)/specialObjectAnimationData.s"
        $path = Join-Path $Disassembly $source
        $macros = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$path`0`0m_SpecialObjectGfxPointer" | ConvertFrom-Json) |
            Where-Object { $_.IsActive -and $_.EnclosingGlobalLabel -eq 'specialObject08GfxPointers' })
        $animations = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0specialObject09AnimationDataPointers`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        $oams = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0specialObject09OamDataPointers`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        if ($macros.Count -ne $(if ($pack.Name -eq 'ages') { 256 } else { 248 }) -or $animations.Count -ne 56 -or $oams.Count -ne 48) {
            throw "${source}: native Link graphics, animation or OAM table extent changed."
        }
        $pointers = Read-MenuRomBytes $pack 'specialObject00AnimationDataPointers' 112
        $oamPointers = Read-MenuRomBytes $pack 'specialObject00OamDataPointers' 96
        $gfx = Read-MenuRomBytes $pack 'specialObject00GfxPointers' ($macros.Count * 3)
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# key`tvalue`tsource')
        foreach ($constant in @(
            @('base-speed',20,'linkSetSwimmingSpeed'),@('fast-speed',35,'linkSetSwimmingSpeed'),
            @('entry-updates',10,'overworldSwimmingState1'),@('velocity-interval',3,'linkSetSwimmingSpeed'),
            @('burst-turn-updates',8,'linkUpdateFlippersSpeed'),@('burst-accelerate-updates',13,'linkUpdateFlippersSpeed'),
            @('burst-decelerate-updates',12,'linkUpdateFlippersSpeed'),@('burst-speed-step',5,'linkUpdateFlippersSpeed'),
            @('swim-sound',136,'linkUpdateFlippersSpeed'),@('dive-updates',120,'linkUpdateDiving')
        )) { $rows.Add("$($constant[0])`t$($constant[1])`tobject_code/common/specialObjects/link.s:$($constant[2])") }
        $sideFrames = [Collections.Generic.List[string]]::new()
        $sideFrames.Add('# mode`tframe`tdirection`tduration`tsprite`tsource-offset`toam-index`toam`tsource')
        $modes = @(@(0x0b,'swim',6),@(0x0c,'dive',16),@(0x17,'flippers',9))
        if ($pack.Name -eq 'ages') { $modes += ,@(0x11,'mermaid',9) }
        foreach ($mode in $modes) {
            $sideView = $mode[1] -in @('flippers','mermaid')
            $animationLabel = $animations[$mode[0]].Operands[0]
            if ([BitConverter]::ToUInt16($pointers,2 * $mode[0]) -ne (($pack.Symbols[$animationLabel] -band 0x3fff) + 0x4000)) { throw "${source}: unresolved swim/dive animation." }
            $animation = Read-MenuRomBytes $pack $animationLabel 8
            # m_AnimationLoop emits a two-byte relative displacement; CADDR
            # advances between its high and low byte, giving $ff,$f9 here.
            if ($animation[0] -ne $mode[2] -or $animation[3] -ne $mode[2] -or $animation[6] -ne 0xff -or
                $animation[7] -ne 0xf9) { throw "${source}:${animationLabel}: expected two-frame native swim/dive loop." }
            $frames = [Collections.Generic.List[string]]::new()
            $frames.Add($(if ($mode[1] -eq 'swim') { '# frame`tdirection`tduration`tsprite`tsource-offset`toam-index`toam`tsource' } else { '# frame`tduration`tsprite`tsource-offset`toam-index`toam`tsource' }))
            for ($frame = 0; $frame -lt 2; $frame++) {
                if (-not $sideView) {
                    $constantName = if ($mode[1] -eq 'swim') { "animation-frame-$frame" } else { "dive-animation-frame-$frame" }
                    $rows.Add("$constantName`t$($animation[$frame * 3])`t${source}:${animationLabel}")
                }
                $directions = if ($mode[1] -eq 'dive') { 1 } else { 4 }
                for ($direction = 0; $direction -lt $directions; $direction++) {
                    $index = [int]$animation[$frame * 3 + 1] + $direction
                    $args = $macros[$index].Operands
                    if ($args.Count -ne 4 -or $args[1] -ne 'spr_link') { throw "${source}: swim/dive requires native Link graphics." }
                    $oam = Convert-AssemblyInteger $args[0]; $offset = Convert-AssemblyInteger $args[2]; $size = Convert-AssemblyInteger $args[3]
                    $expected = (($pack.Symbols['spr_link'] -band 0x3fff) + 0x4000 + $offset) -bor $size
                    if ($gfx[$index * 3] -ne $oam -or [BitConverter]::ToUInt16($gfx,$index * 3 + 1) -ne $expected) { throw "${source}: compiled swim/dive graphics changed." }
                    $label = $oams[$oam].Operands[0]
                    if ([BitConverter]::ToUInt16($oamPointers,$oam * 2) -ne (($pack.Symbols[$label] -band 0x3fff) + 0x4000)) { throw "${source}: unresolved swim/dive OAM." }
                    $count = [int](Read-MenuRomBytes $pack $label 1)[0]
                    $bytes = Read-MenuRomBytes $pack $label (1 + $count * 4)
                    $parts = for ($part = 0; $part -lt $count; $part++) { @($bytes[(1 + $part * 4)..(4 + $part * 4)] | ForEach-Object { [int]$_ }) -join ',' }
                    $prefix = if ($sideView) { "$($mode[1])`t$frame`t$direction" } elseif ($mode[1] -eq 'swim') { "$frame`t$direction" } else { "$frame" }
                    $row = "$prefix`t$($animation[$frame * 3])`tspr_link`t$($offset.ToString('x4'))`t$($oam.ToString('x2'))`t$($parts -join ';')`t${source}:${animationLabel}/graphic-`$$($index.ToString('x2'))+$label"
                    if ($sideView) { $sideFrames.Add($row) } else { $frames.Add($row) }
                }
            }
            if (-not $sideView) { Write-GeneratedTable((Join-Path $pack.Root "metadata/top_down_$($mode[1])_frames.tsv"),$frames) }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/top_down_swim_constants.tsv'),$rows)
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/side_scroll_swim_frames.tsv'),$sideFrames)
        if ($pack.Name -eq 'ages') {
            $entry = Read-MenuRomBytes $pack 'overworldSwimmingState1' 18
            $impulse = Read-MenuRomBytes $pack 'linkUpdateVelocity@directionButtonPressed' 14
            $speed = Read-MenuRomBytes $pack 'linkUpdateVelocity@mermaidSuit' 16
            $table = Read-MenuRomBytes $pack 'updateLinkSpeed_withParam@speedTable' 32
            $velocity = @((Invoke-AssemblySourceHost $connection 'NODES' $linkPath | ConvertFrom-Json) |
                Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' -and $_.EnclosingGlobalLabel -eq 'linkUpdateVelocity' }).Code -join "`n"
            if ($code['overworldSwimmingState1'] -notmatch 'ld l,SpecialObject.var2f\s+bit 6,\(hl\)' -or
                $velocity -notmatch 'ld c,\$98\s+call updateLinkSpeed_withParam' -or
                $velocity -notmatch 'ld l,SpecialObject.var3e\s+ld \(hl\),\$04\s+ld l,SpecialObject.var12\s+ld \(hl\),\$14' -or
                $entry[16] -ne 0x36 -or $entry[17] -ne 2 -or
                $impulse[8] -ne 0x36 -or $impulse[9] -ne 4 -or $impulse[12] -ne 0x36 -or $impulse[13] -ne 0x14 -or
                $speed[14] -ne 0x3e -or $speed[15] -ne 0x37 -or $table[25] -ne 5 -or $table[26] -ne 0x2d) {
                throw 'ROM_AGES link.s: native Mermaid Suit entry, impulse or velocity operands changed.'
            }
            Write-GeneratedTable((Join-Path $pack.Root 'metadata/mermaid_swim_constants.tsv'),@(
                '# key`tvalue`tsource',
                "mermaid-entry-updates`t$($entry[17])`tlink.s:overworldSwimmingState1",
                "mermaid-impulse-updates`t$($impulse[9])`tlink.s:linkUpdateVelocity@directionButtonPressed",
                "mermaid-impulse-counter`t$($impulse[13])`tlink.s:linkUpdateVelocity@directionButtonPressed",
                "mermaid-velocity-interval`t$($table[25])`tlink.s:updateLinkSpeed_withParam@speedTable",
                "mermaid-target-speed`t$($table[26])`tlink.s:updateLinkSpeed_withParam@speedTable",
                "mermaid-fast-target-speed`t$($speed[15])`tlink.s:linkUpdateVelocity@mermaidSuit"
            ))
        }
    } finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
