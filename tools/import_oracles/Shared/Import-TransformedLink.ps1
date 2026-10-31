# Both games dispatch the same transformed special objects, with pack-specific
# sprite pointers (notably the Subrosian sheet) and native OAM/animation data.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $source = "data/$($pack.Name)/specialObjectAnimationData.s"
        $path = Join-Path $Disassembly $source
        $macros = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$path`0`0m_SpecialObjectGfxPointer" | ConvertFrom-Json) | Where-Object IsActive)
        $oamNodes = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0specialObject09OamDataPointers`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        $oamPointers = Read-MenuRomBytes $pack 'specialObject03OamDataPointers' 12
        $ringBytes = Read-MenuRomBytes $pack 'getTransformedLinkID@ringToID' 11
        if (($ringBytes -join ',') -ne '42,5,43,6,44,7,45,3,46,4,0') { throw "$($pack.Name): native transformation ring dispatch changed." }
        $rings = @{ 2 = 0xff }
        $ringRows = [Collections.Generic.List[string]]::new(); $ringRows.Add('# ring`tspecial-object`tsource')
        for ($index = 0; $index -lt 5; $index++) {
            $ring = [int]$ringBytes[$index * 2]; $id = [int]$ringBytes[$index * 2 + 1]
            $rings[$id] = $ring
            $ringRows.Add("$($ring.ToString('x2'))`t$($id.ToString('x2'))`tcode/specialObjectAnimationsAndDamage.s:getTransformedLinkID@ringToID+$((2 * $index).ToString('x2'))")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/transformation_rings.tsv'),$ringRows)
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add('# ring`tspecial-object`tdirection`tframe`tsprite`ttile-base`toam`tinitial-duration`tloop-duration')
        foreach ($id in 2..7) {
            $label = "specialObject0${id}GfxPointers"
            $entries = @($macros | Where-Object EnclosingGlobalLabel -eq $label)
            if ($entries.Count -ne 8) { throw "${source}:${label}: expected eight native graphics entries." }
            $native = Read-MenuRomBytes $pack $label 24
            $animationTable = "specialObject0${id}AnimationDataPointers"
            $animationOwner = if ($id -eq 2) { $animationTable } else { 'specialObject07AnimationDataPointers' }
            $animationNodes = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0$animationOwner`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
            if ($animationNodes.Count -ne 1 -or $animationNodes[0].Operands.Count -ne 1) { throw "${source}:${animationTable}: missing native animation pointer." }
            $animationLabel = $animationNodes[0].Operands[0]
            $animationPointer = Read-MenuRomBytes $pack $animationTable 2
            $animation = Read-MenuRomBytes $pack $animationLabel 9
            if ([BitConverter]::ToUInt16($animationPointer,0) -ne (($pack.Symbols[$animationLabel] -band 0x3fff) + 0x4000) -or
                ($animation -join ',') -ne '2,0,0,6,4,132,6,0,0') { throw "${source}:${animationTable}: native 2/6/6 animation changed." }
            for ($index = 0; $index -lt 8; $index++) {
                $args = $entries[$index].Operands
                if ($args.Count -ne 4) { throw "${source}:${label}: unsupported graphics macro." }
                $oamIndex = Convert-AssemblyInteger $args[0]; $sprite = $args[1]
                $offset = Convert-AssemblyInteger $args[2]; $size = Convert-AssemblyInteger $args[3]
                if ($oamIndex -ge 6 -or $size -ne 4 -or ($offset % 16) -ne 0) { throw "${source}:${label}: unsupported graphics span/OAM index." }
                $bank = [int][Math]::Floor($pack.Symbols[$sprite] / 0x4000) - [int][Math]::Floor($pack.Symbols['gfxDataBank1a'] / 0x4000)
                $expected = (($pack.Symbols[$sprite] -band 0x3fff) + 0x4000 + $offset) -bor $size -bor $bank
                if ($native[$index * 3] -ne $oamIndex -or [BitConverter]::ToUInt16($native,$index * 3 + 1) -ne $expected) { throw "${source}:${label}+$($index * 3): graphics differs from clean US bytes." }
                $oamLabel = $oamNodes[$oamIndex].Operands[0]
                if ([BitConverter]::ToUInt16($oamPointers,$oamIndex * 2) -ne (($pack.Symbols[$oamLabel] -band 0x3fff) + 0x4000)) { throw "${source}:${label}: unresolved OAM $oamLabel." }
                $count = [int](Read-MenuRomBytes $pack $oamLabel 1)[0]
                if ($count -ne 2) { throw "${source}:${oamLabel}: transformed Link requires two OAM parts." }
                $bytes = Read-MenuRomBytes $pack $oamLabel (1 + 4 * $count)
                $parts = for ($part = 0; $part -lt $count; $part++) { @($bytes[(1 + $part * 4)..(4 + $part * 4)] | ForEach-Object { [int]$_ }) -join ',' }
                $rows.Add("$($rings[$id].ToString('x2'))`t$($id.ToString('x2'))`t$($index % 4)`t$([Math]::Floor($index / 4))`t$sprite`t$($offset / 16)`t$($parts -join ';')`t$($animation[0])`t$($animation[3])")
                $spriteFile = $null
                foreach ($directory in Get-ChildItem $Disassembly -Directory -Filter 'gfx*') {
                    foreach ($group in @($pack.Name,'common')) {
                        $candidate = Join-Path $directory.FullName "$group/$sprite.png"
                        if (Test-Path -LiteralPath $candidate) { $spriteFile = $candidate; break }
                    }
                    if ($spriteFile) { break }
                }
                if (-not $spriteFile) { throw "${source}:${label}: missing native $($pack.Name)/common sprite $sprite." }
                Copy-Item -LiteralPath $spriteFile -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
            }
            if ($id -eq 2) {
                Write-GeneratedTable((Join-Path $pack.Root 'metadata/baby_link.tsv'),@(
                    '# duration`tspeed-shift`tstep-parameter`tsource',
                    "480`t1`t84`tobject_code/common/specialObjects/transformedLink.s;${source}:${animationLabel}"
                ))
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/transformed_link.tsv'),$rows)
        $baby = (Read-ImportText (Join-Path $Disassembly 'object_code/common/specialObjects/transformedLink.s')) -replace '(?m);[^\r\n]*',''
        if ($baby -notmatch '(?ms)^@state0:.*?dropLinkHeldItem.*?clearAllParentItems.*?SPECIALOBJECT_LINK_AS_BABY.*?counter1\s*ld \(hl\),\$e0\s*inc l\s*ld \(hl\),\$01.*?SND_BECOME_BABY.*?@disableTransformationForBaby:\s*ld a,SND_MAGIC_POWDER' -or
            $baby -notmatch '(?ms)^@state1:.*?wLinkForceState.*?wPaletteThread_mode.*?wScrollMode.*?updateLinkDamageTaken.*?wLinkDeathTrigger.*?retIfTextIsActive.*?wDisabledObjects.*?and \$81.*?decPegasusSeedCounter.*?decHlRef16WithCap.*?@disableTransformationForBaby.*?updateLinkSpeed_standard.*?srl \(hl\)') { throw 'transformedLink.s: native baby lifetime, gates or speed changed.' }
    } finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
