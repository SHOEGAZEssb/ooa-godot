$partData = Read-ImportText (Join-Path $Disassembly 'data/ages/partData.s')
if ($partData -notmatch '(?m)^\s*\.db \$00 \$83 \$44 \$ff \$40 \$08 \$00 \$00 ; \$05\s*$') {
    throw 'PART_SWITCH $05 collision mode, radii or initial properties changed.'
}
$active = Read-ImportText (Join-Path $Disassembly 'data/ages/partActiveCollisions.s')
$mask = [regex]::Match($active, '(?m)^\s*dbrev (?<bits>%[01]{8} %[01]{8} %[01]{8} %[01]{8}) ; 0x05\s*$')
if (-not $mask.Success) { throw 'PART_SWITCH active collision bitset missing.' }
$bits = $mask.Groups['bits'].Value.Replace('%', '').Replace(' ', '')
$collision = Read-ImportText (Join-Path $Disassembly 'data/ages/objectCollisionTable.s')
$tableRows = [regex]::Matches($collision, '(?m)^\s*\.db(?<values>(?:\s+\$[0-9a-f]{2}){16})\s*$')
if ($tableRows.Count -ne 256) { throw 'Expected 128 complete object collision modes.' }
$effects = [regex]::Matches(($tableRows[6].Value + $tableRows[7].Value), '\$(?<value>[0-9a-f]{2})')
$code = Read-ImportText (Join-Path $Disassembly 'code/collisionEffects.s')
if ($code -notmatch '(?ms)^collisionEffect26:\s+ldhl LINKDMG_1c, ENEMYDMG_34\s+jr applyDamageToBothObjects' -or
    $code -notmatch '\.db \$60 \$e4 \$00 \$00 ; ENEMYDMG_34' -or
    $code -notmatch '(?ms)^collisionEffect20:.*?cp ITEM_28.*?res 7,\(hl\).*?call func_07_47b7.*?ldhl LINKDMG_24, ENEMYDMG_44' -or
    $code -notmatch '\.db \$50 \$00 \$00 \$00 ; ENEMYDMG_44') {
    throw 'PART_SWITCH collision effects $20/$26 or signed hit lockout changed.'
}
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# item`tenabled`teffect`tlockout`tsource")
for ($item = 0; $item -lt 32; $item++) {
    $effect = $effects[$item].Groups['value'].Value
    if ($bits[$item] -eq '1' -and $effect -notin @('20', '26')) {
        throw "PART_SWITCH collision $item enables unsupported effect $effect."
    }
    $lockout = if ($effect -eq '26') { '1c' } else { '00' }
    $rows.Add("$($item.ToString('x2'))`t$($bits[$item])`t$effect`t$lockout`tdata/ages/partActiveCollisions.s:partActiveCollisions+0014;data/ages/objectCollisionTable.s:objectCollisionTable+$((0x60 + $item).ToString('x4'))")
}
Write-GeneratedTable((Join-Path $destination 'objects/part_switch_collisions.tsv'), $rows)

# Stationary and moving orbs share mode03 but have their own active mask.
# partCheckCollisions indexes collisionType$83, not the part's ID$0b.
$orbMask = [regex]::Match($active, '(?m)^\s*dbrev (?<bits>%[01]{8} %[01]{8} %[01]{8} %[01]{8}) ; 0x03\s*$')
if (-not $orbMask.Success -or $partData -notmatch '(?m)^\s*\.db \$74 \$83 \$44 \$00 \$40 \$1e \$00 \$00 ; \$0b\s*$') {
    throw 'PART_MOVING_ORB $0b lost its source graphics/radii/collision properties.'
}
$orbBits = $orbMask.Groups['bits'].Value.Replace('%', '').Replace(' ', '')
$orbRows = [Collections.Generic.List[string]]::new()
$orbRows.Add("# item`tenabled`teffect`tlockout`tsource")
for ($item = 0; $item -lt 32; $item++) {
    $effect = $effects[$item].Groups['value'].Value
    if ($orbBits[$item] -eq '1' -and $effect -notin @('20', '26')) { throw 'Unsupported orb collision effect.' }
    $lockout = if ($effect -eq '26') { '1c' } else { '00' }
    $orbRows.Add("$($item.ToString('x2'))`t$($orbBits[$item])`t$effect`t$lockout`tdata/ages/partActiveCollisions.s:partActiveCollisions+000c;data/ages/objectCollisionTable.s:objectCollisionTable+$((0x60 + $item).ToString('x4'))")
}
Write-GeneratedTable((Join-Path $destination 'objects/part_orb_collisions.tsv'), $orbRows)
$stationaryOrb = Read-ImportText (Join-Path $Disassembly 'object_code/common/parts/orb.s')
$orbInit = [regex]::Match($stationaryOrb,
    '(?ms)^@state0:\s+inc a\s+ld \(de\),a\s+call objectMakeTileSolid\s+ld h,Part\.zh\s+ld \(hl\),\$(?<tile>[0-9a-f]{2})\s+ld h,d\s+ld l,Part\.subid\s+ldi a,\(hl\)\s+and \$(?<mask>[0-9a-f]{2})\s+ld bc,bitTable')
$bank0 = Read-ImportText (Join-Path $Disassembly 'code/bank0.s')
$orbSolid = [regex]::Match($bank0,
    '(?ms)^objectMakeTileSolid:\s+call objectGetTileCollisions\s+ld \(hl\),\$(?<collision>[0-9a-f]{2})\s+ret')
$structs = Read-ImportText (Join-Path $Disassembly 'include/structs.s')
$wram = Read-ImportText (Join-Path $Disassembly 'include/wram.s')
if (-not $orbInit.Success -or -not $orbSolid.Success -or
    $structs -notmatch '(?m)^\s*zh\s+db ; \$0f\s*$' -or
    $structs -notmatch '(?ms)^\.enum \$c0\s+Part\s+instanceof ObjectStruct\s+\.ende' -or
    $wram -notmatch '(?m)^wRoomLayout: ; \$cf00\s*$' -or
    $partData -notmatch '(?m)^\s*\.db \$74 \$83 \$44 \$00 \$40 \$1e \$00 \$00 ; \$03\s*$' -or
    $stationaryOrb -notmatch '(?ms)^partCode03:\s+cp PARTSTATUS_JUST_HIT\s+jr nz,@notJustHit.*?xor \(hl\).*?ld l,Part.oamFlagsBackup\s+ld a,\(hl\)\s+and \$01\s+inc a\s+ldi \(hl\),a\s+ld \(hl\),a\s+ld a,SND_SWITCH\s+jp playSound') {
    throw 'PART_ORB $03 initialization, raw room-layout write, shared collision profile, or delayed toggle changed.'
}
# Part.zh=$cf changes H in the returned room-collision pointer. The write
# targets wRoomLayout, not the orb height, and does not redraw the tile.
Write-GeneratedTable((Join-Path $destination 'objects/part_orb_initialization.tsv'), @(
    "# id`tbackground-tile`ttile-collision`tsubid-mask`tradius-y`tradius-x`tsource",
    "03`t$($orbInit.Groups['tile'].Value)`t$($orbSolid.Groups['collision'].Value)`t$($orbInit.Groups['mask'].Value)`t04`t04`tobject_code/common/parts/orb.s:@state0;code/bank0.s:objectMakeTileSolid;data/ages/partData.s:partData+0018"
))
$orbScriptPath = Join-Path $Disassembly 'data/ages/orbMovementScript.s'
$orbScript = Read-ImportText $orbScriptPath
$orbPattern = '(?ms)^orbMovementScript:\s*\.dw @subid00\s*@subid00:\s*\.db SPEED_80\s*\.db DIR_UP\s*@@loop:\s*ms_right \$(?<right>[0-9a-f]{2})\s*ms_left\s+\$(?<left>[0-9a-f]{2})\s*ms_loop\s+@@loop\s*\z'
$orbMatch = [regex]::Match($orbScript, $orbPattern)
$speeds = Read-ImportText (Join-Path $Disassembly 'constants/common/objectSpeeds.s')
$orbSpeedValue = -1
$offset = 0
foreach ($speed in [regex]::Matches($speeds, '(?m)^\s*(SPEED_[0-9a-f]+)\s+dsb (\d+)')) {
    if ($speed.Groups[1].Value -eq 'SPEED_80') { $orbSpeedValue = $offset }
    $offset += [int]$speed.Groups[2].Value
}
if (-not $orbMatch.Success -or $orbSpeedValue -lt 0 -or $offset -ne 125) { throw 'Unsupported PART_MOVING_ORB movement script.' }
Write-GeneratedTable((Join-Path $destination 'objects/moving_orb_script.tsv'), @(
    "# subid`tspeed`tinitial-direction`tright`tleft`tsource",
    "00`t$($orbSpeedValue.ToString('x2'))`t00`t$($orbMatch.Groups['right'].Value)`t$($orbMatch.Groups['left'].Value)`tdata/ages/orbMovementScript.s:@subid00"
))
