# Shared enemy mechanics must not depend on a game's species or boss bundle.
# Check selected source and compiled instruction operands before sharing data.
$commonEnemyRows = $null
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path = Join-Path $Disassembly 'object_code/common/enemies/commonCode.s'
        foreach ($spec in @(
            @('ecom_updateKnockback_common','ld b,SPEED_200[\s\S]*ld b,SPEED_300'),
            @('ecom_checkHazardsCommon','ld bc,\$05ff[\s\S]*ld bc,\$0501[\s\S]*ld \(hl\),60'),
            @('ecom_fallingInHole','and \$07[\s\S]*ld b,SPEED_80[\s\S]*sub \$03'),
            @('ecom_checkScentSeedActive','bit 4,a[\s\S]*and \$f8[\s\S]*ld a,\$04'),
            @('ecom_updateAngleToScentSeed','ld l,Enemy.var3d[\s\S]*dec \(hl\)[\s\S]*and \$0f'),
            @('ecom_updateCardinalAngleTowardTarget','add \$04[\s\S]*and \$18')
        )) {
            $nodes = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$path`0$($spec[0])`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' })
            if (($nodes.Code -join "`n") -notmatch $spec[1]) { throw "$($pack.Name): commonCode.s:$($spec[0]) native enemy contract changed." }
        }
        foreach ($spec in @(
            @('ecom_updateKnockback_common',6,[byte[]]@(0x06,0x50,0x30,0x0e,0x06,0x78)),
            @('ecom_checkHazardsCommon',13,[byte[]]@(0x01,0xff,0x05)),
            @('ecom_checkHazardsCommon',29,[byte[]]@(0x01,0x01,0x05)),
            @('ecom_checkHazardsCommon',70,[byte[]]@(0x2e,0x86,0x36,60)),
            @('ecom_fallingInHole',6,[byte[]]@(0xe6,7)),
            @('ecom_fallingInHole',19,[byte[]]@(0x06,0x14)),
            @('ecom_fallingInHole',33,[byte[]]@(0xd6,3)),
            @('ecom_checkScentSeedActive',17,[byte[]]@(0x3e,4,0x12,0xc9)),
            @('ecom_updateAngleToScentSeed',5,[byte[]]@(0xe6,15,0xc0)),
            @('ecom_updateCardinalAngleTowardTarget',3,[byte[]]@(0xc6,4,0xe6,0x18))
        )) {
            # commonCode.s is assembled into every enemy bank; prove each copy.
            $copies=@(foreach ($symbol in $pack.Symbols.Keys) {
                if ($symbol.StartsWith('bank:',[StringComparison]::Ordinal) -and $symbol.EndsWith(':'+$spec[0],[StringComparison]::Ordinal)) { $symbol }
            })
            if($copies.Count -eq 0){throw "$($pack.Name): missing compiled enemy common-code copies for $($spec[0])."}
            foreach($copy in $copies){
                $bytes = Read-MenuRomBytes $pack $copy ($spec[1] + $spec[2].Length)
                for ($index = 0; $index -lt $spec[2].Length; $index++) {
                    if ($bytes[$spec[1] + $index] -ne $spec[2][$index]) { throw "$($pack.Name): compiled ${copy}+$($spec[1]) enemy operand differs from selected source." }
                }
            }
        }
        $damage = Read-MenuRomBytes $pack 'applyDamageToEnemyOrPart@damageTypeTable' 16
        if (($damage -join ',') -ne '241,16,8,0,241,21,11,0,241,26,15,0,241,32,0,0') { throw "$($pack.Name): native collisionEffects.s sword response rows changed." }
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# profile`tindex`tvalue-a`tvalue-b`tsource")
        for ($index = 0; $index -lt 4; $index++) {
            $offset = $index * 4
            $rows.Add("sword-damage-profiles`t$index`t$($damage[$offset + 1])`t$($damage[$offset + 2])`tcode/collisionEffects.s:applyDamageToEnemyOrPart@damageTypeTable+$($offset.ToString('x2'))")
        }
        foreach ($spec in @(
            @('knockback-speeds',@(80,120),@('ecom_updateKnockback_common+$07','ecom_updateKnockback_common+$0b')),
            @('hazard-profile',@(5,-1,1,60,7,20,3),@('ecom_checkHazardsCommon+$0f','ecom_checkHazardsCommon+$0e','ecom_checkHazardsCommon+$1e','ecom_checkHazardsCommon+$49','ecom_fallingInHole+$07','ecom_fallingInHole+$14','ecom_fallingInHole+$22')),
            @('scent-attraction-profile',@(4,15,4,24),@('ecom_checkScentSeedActive+$12','ecom_updateAngleToScentSeed+$06','ecom_updateCardinalAngleTowardTarget+$04','ecom_updateCardinalAngleTowardTarget+$06'))
        )) {
            for ($index = 0; $index -lt $spec[1].Length; $index++) {
                $rows.Add("$($spec[0])`t$index`t$($spec[1][$index])`t0`tobject_code/common/enemies/commonCode.s:$($spec[2][$index])")
            }
        }
        $partPath = Join-Path $Disassembly 'object_code/common/parts/commonCode.s'
        $partCode = (@((Invoke-AssemblySourceHost $connection 'NODES' $partPath | ConvertFrom-Json) |
            Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' } | ForEach-Object Code)) -join "`n"
        if ($partCode -notmatch 'ld bc,-\$e0[\s\S]*ld \(hl\),\$20[\s\S]*ld \(hl\),SPEED_40' -or
            $partCode -notmatch 'ld c,\$0e\s+call objectUpdateSpeedZ') { throw "$($pack.Name): native common projectile bounce changed." }
        $bounce = Read-MenuRomBytes $pack 'partCommon_bounceWhenCollisionsEnabled' 32
        $advance = Read-MenuRomBytes $pack 'partCommon_updateSpeedAndDeleteWhenCounter1Is0' 16
        if (($bounce[11..13] -join ',') -ne '1,32,255' -or
            ($bounce[17..24] -join ',') -ne '46,198,54,32,46,208,54,10' -or
            ($bounce[25..31] -join ',') -ne '46,201,126,238,16,119,201' -or
            ($advance[6..7] -join ',') -ne '14,14') { throw "$($pack.Name): compiled common projectile bounce operands changed." }
        $values = @($bounce[20],$advance[6],$bounce[24],([BitConverter]::ToUInt16($bounce,12) - 0x10000))
        $sources = @('partCommon_bounceWhenCollisionsEnabled+$14','partCommon_updateSpeedAndDeleteWhenCounter1Is0+$06',
            'partCommon_bounceWhenCollisionsEnabled+$18','partCommon_bounceWhenCollisionsEnabled+$0c')
        for ($index = 0; $index -lt 4; $index++) {
            $rows.Add("projectile-bounce-profile`t$index`t$($values[$index])`t0`tobject_code/common/parts/commonCode.s:$($sources[$index])")
        }
        $boundary = Read-MenuRomBytes $pack 'partCommon_anglePositionOffsets' 16
        if (($boundary -join ',') -ne '251,0,251,4,0,4,4,4,4,0,4,251,0,251,251,251') {
            throw "$($pack.Name): native common PART boundary offsets changed."
        }
        for ($index = 0; $index -lt 8; $index++) {
            $y = [int]$boundary[$index * 2]; $x = [int]$boundary[$index * 2 + 1]
            if ($y -ge 128) { $y -= 256 }; if ($x -ge 128) { $x -= 256 }
            $rows.Add("projectile-boundary-offsets`t$index`t$y`t$x`tobject_code/common/parts/commonCode.s:partCommon_anglePositionOffsets+$($index * 2)")
        }
        if ($rows.Count -ne 30) { throw "$($pack.Name): incomplete common enemy profiles." }
        if ($null -eq $commonEnemyRows) { $commonEnemyRows = $rows.ToArray() }
        elseif (-not [Linq.Enumerable]::SequenceEqual([string[]]$commonEnemyRows,[string[]]$rows.ToArray())) { throw 'Common enemy behavior differs between the supported games.' }
    }
    finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
Write-GeneratedTable((Join-Path (Split-Path $destination -Parent) 'common/metadata/enemy_common_behavior.tsv'),$commonEnemyRows)
