# Resolve native pointer streams; common handlers do not borrow another pack's IDs.
function Read-NativeObjectPointerLabels($pack, [string]$path, [string]$label, $sourceHost) {
    $nodes = @((Invoke-AssemblySourceHost $sourceHost 'DATA_DIRECTIVES' "$path`0$label`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
    if ($nodes.Count -eq 0) {
        # Consecutive native labels can alias one pointer table. Resolve the
        # configured source alias at the same compiled address before reading.
        $aliases = @((Invoke-AssemblySourceHost $sourceHost 'NODES' $path | ConvertFrom-Json) | Where-Object {
            $_.IsActive -and $_.Kind -eq 'Label' -and $_.Name -ne $label -and
            $pack.Symbols.ContainsKey($_.Name) -and $pack.Symbols[$_.Name] -eq $pack.Symbols[$label]
        })
        foreach ($alias in $aliases) {
            $nodes = @((Invoke-AssemblySourceHost $sourceHost 'DATA_DIRECTIVES' "$path`0$($alias.Name)`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
            if ($nodes.Count -gt 0) { break }
        }
        if ($nodes.Count -eq 0) { throw "$($pack.Name): ${path}:${label}: empty native pointer stream, including configured aliases." }
    }
    $bytes = Read-MenuRomBytes $pack $label ($nodes.Count * 2)
    $labels = foreach ($node in $nodes) {
        if ($node.Operands.Count -ne 1) { throw "${path}:${label}: unsupported pointer row." }
        $target = $node.Operands[0]
        $index = [Array]::IndexOf($nodes,$node)
        if (-not $pack.Symbols.ContainsKey($target) -or
            [BitConverter]::ToUInt16($bytes,$index * 2) -ne (($pack.Symbols[$target] -band 0x3fff) + 0x4000)) { throw "$($pack.Name): ${label}: unresolved native pointer $target." }
        $target
    }
    return ,@($labels)
}
function Read-NativeEnemyAnimation($pack, [string]$label, [string[]]$oamLabels, [bool]$terminal, [bool]$stopAfterParameterFF = $false) {
    $frames = [Collections.Generic.List[string]]::new()
    $durations = [Collections.Generic.List[int]]::new()
    $starts = [Collections.Generic.List[int]]::new()
    $offset = 0
    for ($visit = 0; $visit -lt 64; $visit++) {
        $bytes = Read-MenuRomBytes $pack $label ($offset + 1)
        $duration = [int]$bytes[$offset]
        if ($duration -eq 0xff) {
            if ($terminal) { throw "$($pack.Name): ${label}: expected a terminal parameter, found a loop." }
            $bytes = Read-MenuRomBytes $pack $label ($offset + 2)
            # _enemyNextAnimationFrame increments HL before adding $ffxx.
            $target = $offset + 1 + ([int]$bytes[$offset + 1] - 256)
            $loop = $starts.IndexOf($target)
            if ($loop -lt 0) { throw "$($pack.Name): ${label}: loop targets a non-frame offset $target." }
            $encoded = $frames -join '|'
            if ($loop -ne 0) { $encoded += "~$loop" }
            return @{ Encoded = $encoded; Durations = $durations.ToArray() }
        }
        $bytes = Read-MenuRomBytes $pack $label ($offset + 3)
        $parameter = [int]$bytes[$offset + 2]
        if ($terminal -and $parameter -ne 0) {
            if ($parameter -ne 0xff -or $frames.Count -eq 0) { throw "$($pack.Name): ${label}: unsupported terminal parameter." }
            return @{ Encoded = ($frames -join '|'); Durations = $durations.ToArray() }
        }
        $pointer = [int]$bytes[$offset + 1]
        if ($duration -eq 0 -or ($pointer -band 1) -ne 0 -or $pointer / 2 -ge $oamLabels.Count) { throw "$($pack.Name): ${label}: invalid duration/OAM offset at `$$($offset.ToString('x2'))." }
        $oamLabel = $oamLabels[$pointer / 2]
        $count = [int](Read-MenuRomBytes $pack $oamLabel 1)[0]
        if ($count -lt 1 -or $count -gt 16) { throw "$($pack.Name): ${oamLabel}: unsupported OAM extent." }
        $oam = Read-MenuRomBytes $pack $oamLabel (1 + 4 * $count)
        $cells = @(for ($cell = 0; $cell -lt $count; $cell++) { @($oam[(1 + $cell * 4)..(4 + $cell * 4)] | ForEach-Object { [int]$_ }) -join ',' }) -join ';'
        $metadata = if ($parameter -eq 0) { "$duration" } else { "$duration,$parameter" }
        $starts.Add($offset); $frames.Add("$metadata@$cells"); $durations.Add($duration)
        if ($stopAfterParameterFF -and $parameter -eq 0xff) {
            return @{ Encoded = ($frames -join '|'); Durations = $durations.ToArray() }
        }
        $offset += 3
    }
    throw "$($pack.Name): ${label}: native animation exceeds the supported finite stream boundary."
}
$commonKeeseRows = $null
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Gfx = 0x9d; Mode = 0x9f },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Gfx = 0x84; Mode = 0xa9 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $data = Read-MenuRomBytes $pack 'enemyData' (0x33 * 4)
        $extra = Read-MenuRomBytes $pack 'extraEnemyData' 32
        if ($data[0xc8] -ne $pack.Gfx -or $data[0xc9] -ne $pack.Mode -or $data[0xca] -ne 7 -or $data[0xcb] -ne 0 -or
            ($extra[28..31] -join ',') -ne '4,6,252,1') { throw "$($pack.Name): native ENEMY_KEESE `$32 properties changed." }
        $collision = Read-MenuRomBytes $pack 'objectCollisionTable' ((($pack.Mode -band 0x7f) + 1) * 32)
        $at = ($pack.Mode -band 0x7f) * 32
        if (($collision[($at + 4)..($at + 6)] -join ',') -ne '8,9,9') { throw "$($pack.Name): native Keese sword responses changed." }
        $gfxPath = Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s"
        $gfx = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$gfxPath`0objectGfxHeaderTable`0m_ObjectGfxHeader" | ConvertFrom-Json) | Where-Object IsActive)[$pack.Gfx]
        if ($gfx.Operands.Count -ne 1 -or $gfx.Operands[0] -ne 'spr_keese_firekeese_unknownbeams') { throw "$($pack.Name): native Keese graphics header changed." }
        $sprite = $gfx.Operands[0]
        $headers = Read-MenuRomBytes $pack 'objectGfxHeaderTable' (($pack.Gfx + 1) * 3)
        $at = $pack.Gfx * 3
        if (($headers[$at] -band 0x3f) -ne [int][Math]::Floor($pack.Symbols[$sprite] / 0x4000) -or
            (([int]$headers[$at + 1] -shl 8) -bor $headers[$at + 2]) -ne (($pack.Symbols[$sprite] -band 0x3fff) + 0x4000)) { throw "$($pack.Name): compiled Keese graphics header differs from selected source." }
        $path = Join-Path $Disassembly "data/$($pack.Name)/enemyAnimations.s"
        $animations = Read-NativeObjectPointerLabels $pack $path 'enemy32Animations' $connection
        $oam = Read-NativeObjectPointerLabels $pack $path 'enemy32OamDataPointers' $connection
        if ($animations.Count -ne 2 -or $oam.Count -ne 2) { throw "$($pack.Name): incomplete Keese animation/OAM pointers." }
        $idle = Read-NativeEnemyAnimation $pack $animations[0] $oam $false
        $flight = Read-NativeEnemyAnimation $pack $animations[1] $oam $false
        if ($idle.Encoded -ne '127@8,4,2,0' -or $flight.Encoded -ne '4@8,0,0,0;8,8,0,32|4@8,4,2,0') { throw "$($pack.Name): native Keese animation/OAM streams changed." }
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# id`tsubid`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tidle-animation`tfly-animation`traw-damage")
        foreach ($subid in @('00','01')) { $rows.Add("32`t$subid`t$sprite`t0`t0`t4`t6`t2`t1`t$($idle.Encoded)`t$($flight.Encoded)`tfc") }
        Write-GeneratedTable((Join-Path $pack.Root 'objects/keese.tsv'),$rows)
        $sourceSprite = Find-ImportSpriteSource $sprite
        Copy-Item -LiteralPath $sourceSprite.FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force

        $speeds = Read-MenuRomBytes $pack 'keese_updateDeceleration@speeds' 8
        $masks = Read-MenuRomBytes $pack 'keese_updateDeceleration@bits' 8
        if (($speeds -join ',') -ne '30,20,10,10,5,5,5,5' -or ($masks -join ',') -ne '0,0,1,1,3,3,7,0') { throw "$($pack.Name): native Keese deceleration tables changed." }
        $codePath = Join-Path $Disassembly 'object_code/common/enemies/keese.s'
        $code = (@((Invoke-AssemblySourceHost $connection 'NODES' $codePath | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' } | ForEach-Object Code) -join "`n")
        foreach ($guard in @('ld bc,\$1f3f','ld \(hl\),SPEED_c0','ld a,\$c0','cp \$68','ld a,\$7f','and \$7f','add \$20','ld c,\$31','ld \(hl\),SPEED_100','ld \(hl\),12','ld \(hl\),\$20','ld \(hl\),\$02','and \$03','call getRandomNumber_noPreserveVars')) {
            if ($code -notmatch $guard) { throw "$($pack.Name): native Keese state operand changed ($guard)." }
        }
        $init = Read-MenuRomBytes $pack 'keese_initializeSubid' 16
        $dispatch = Read-MenuRomBytes $pack 'enemyCode32@normalStatus' 8
        $entry = Read-MenuRomBytes $pack 'keese_state_uninitialized' 9
        if (($pack.Bytes[0..10] -join ',') -ne '135,225,133,111,48,1,36,42,102,111,233' -or
            $dispatch[0] -ne 0xcd -or $dispatch[3] -ne 0x30 -or $dispatch[5] -ne 0xc7 -or
            [BitConverter]::ToUInt16($dispatch,6) -ne (($pack.Symbols['keese_state_uninitialized'] -band 0x3fff) + 0x4000) -or
            $entry[0] -ne 0xcd -or $entry[3] -ne 0xcd -or $entry[6] -ne 0xc3) {
            throw "$($pack.Name): native Keese state-zero jump-table accumulator contract changed."
        }
        $entryInstructions = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$codePath`0keese_state_uninitialized`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' })
        if (($entryInstructions.Code -join "`n") -cne "call ecom_setSpeedAndState8`ncall keese_initializeSubid`njp objectSetVisible82") {
            throw "$($pack.Name): Keese state zero no longer passes rst_jumpTable's accumulator to speed."
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/keese_initialization.tsv'),@(
            '# initial-speed`tsource',
            "$($dispatch[6].ToString('x2'))`tcode/bank0.s:rst_jumpTable;object_code/common/enemies/keese.s:keese_state_uninitialized->ecom_setSpeedAndState8"
        ))
        $reverse = Read-MenuRomBytes $pack 'keese_chooseWhetherToReverseTurningAngle' 6
        if (($init -join ',') -ne '5,40,5,46,134,54,32,201,46,143,54,255,46,176,54,2' -or $reverse[0] -ne 0xcd -or $reverse[3] -ne 0xe6 -or $reverse[4] -ne 3 -or $reverse[5] -ne 0xc0) { throw "$($pack.Name): compiled Keese initialization/RNG contract changed." }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# table`tindex`tvalue`tsource")
        foreach ($spec in @(@('deceleration-speeds',$speeds,'keese_updateDeceleration@speeds'),@('deceleration-animation-masks',$masks,'keese_updateDeceleration@bits'),@('state-profile',@(30,40,32,49,12,12,192,63,104,127,32,127,2,3),'keese_state_uninitialized'))) {
            for ($index = 0; $index -lt $spec[1].Length; $index++) { $rows.Add("$($spec[0])`t$index`t$($spec[1][$index])`tobject_code/common/enemies/keese.s:$($spec[2])") }
        }
        if ($null -eq $commonKeeseRows) { $commonKeeseRows = $rows.ToArray() }
        elseif (-not [Linq.Enumerable]::SequenceEqual([string[]]$commonKeeseRows,[string[]]$rows.ToArray())) { throw 'Native Keese behavior differs between games.' }

        $path = Join-Path $Disassembly "data/$($pack.Name)/partAnimations.s"
        $animations = Read-NativeObjectPointerLabels $pack $path 'part02Animations' $connection
        $oam = Read-NativeObjectPointerLabels $pack $path 'part02OamDataPointers' $connection
        if ($animations.Count -ne 2 -or $oam.Count -ne 7) { throw "$($pack.Name): native death-puff animation/OAM pointers changed." }
        $normal = Read-NativeEnemyAnimation $pack $animations[0] $oam $true
        $high = Read-NativeEnemyAnimation $pack $animations[1] $oam $true
        if (($normal.Durations -join ',') -ne '2,2,2,4,4,4,2' -or ($high.Durations -join ',') -ne '2,2,2,8,4,4,4,2') { throw "$($pack.Name): native death-puff update boundaries changed." }
        $part = Read-MenuRomBytes $pack 'partData' 24
        if (($part[16..23] -join ',') -ne '0,0,0,0,64,12,10,0') { throw "$($pack.Name): native PART_ENEMY_DESTROYED graphics data changed." }
        Write-GeneratedTable((Join-Path $pack.Root 'effects/enemy_death_puff.tsv'),@("# tile-base`tpalette-a`tpalette-b`tnormal-animation`thigh-knockback-animation","12`t2`t3`t$($normal.Encoded)`t$($high.Encoded)"))
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
Write-GeneratedTable((Join-Path (Split-Path $destination -Parent) 'common/metadata/keese_behavior.tsv'),$commonKeeseRows)
