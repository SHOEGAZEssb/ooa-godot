$relativeSource = 'object_code/ages/enemies/armosWarrior.s'
$codePath = Join-Path $Disassembly $relativeSource
$speeds = Read-ImportText (Join-Path $Disassembly 'constants/common/objectSpeeds.s')
$speedValues = @{}
$offset = 0
foreach ($match in [regex]::Matches($speeds, '(?m)^\s*(SPEED_[0-9a-f]+)\s+dsb (\d+)')) {
    $speedValues[$match.Groups[1].Value] = $offset
    $offset += [int]$match.Groups[2].Value
}
if ($offset -ne 125 -or $speedValues['SPEED_300'] -ne 120) {
    throw 'Armos Warrior: object speed enumeration changed.'
}
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# profile`tvalues`tsource")
foreach ($spec in @(
    @('shield-offsets', 'armosWarrior_shield_YXOffsets', 4),
    @('sword-boxes', 'armosWarrior_sword_collisionBoxes', 32),
    @('sword-speeds', 'armosWarrior_sword_speedVals', 4),
    @('parent-speeds', 'armosWarrior_parent_speedVals', 3),
    @('sword-boundaries', 'armosWarrior_sword_angleBoundaries', 16)
)) {
    $label = $spec[1]
    $values = [Collections.Generic.List[string]]::new()
    foreach ($row in Read-AssemblyDataDirectives $codePath $label '.db') {
        foreach ($token in $row.Operands) {
            $value = if ($token -match '^\$([0-9a-f]{2})$') { [Convert]::ToInt32($Matches[1], 16) }
                elseif ($speedValues.ContainsKey($token)) { $speedValues[$token] }
                else { throw "${relativeSource}:${label}: unsupported byte $token." }
            $values.Add($value.ToString('x2'))
        }
    }
    if ($values.Count -ne $spec[2]) { throw "${relativeSource}:${label}: expected $($spec[2]) bytes." }
    $rows.Add("$($spec[0])`t$($values -join ',')`t${relativeSource}:${label}")
}
$collisionValues = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
if ($collisionValues.Count -ne 4096) { throw 'Armos Warrior: incomplete collision table.' }
foreach ($mode in @(0x44, 0x60, 0x61, 0x62)) {
    $values = $collisionValues[($mode * 32)..($mode * 32 + 31)] | ForEach-Object { $_.ToString('x2') }
    $rows.Add("collision-$($mode.ToString('x2'))`t$($values -join ',')`tdata/ages/objectCollisionTable.s:objectCollisionTable+$((32 * $mode).ToString('x4'))")
}
$active = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/enemyActiveCollisions.s') 'enemyActiveCollisions' 'dbrev')
if ($active.Count -ne 128 -or $active[0x73].Operands.Count -ne 4) { throw 'Armos Warrior: missing active collision mask.' }
$bits = ($active[0x73].Operands -join '').Replace('%', '')
if ($bits -notmatch '^[01]{32}$') { throw 'Armos Warrior: malformed active collision mask.' }
$rows.Add("active-collisions`t$(($bits.ToCharArray() | ForEach-Object { '0' + $_ }) -join ',')`tdata/ages/enemyActiveCollisions.s:enemyActiveCollisions+01cc")
Write-GeneratedTable((Join-Path $destination 'objects/armos_warrior_tables.tsv'), $rows)

$texts = [Collections.Generic.List[string]]::new()
$texts.Add("# text-id`tposition`tmessage-base64`tsource")
foreach ($id in @(0x2f01, 0x2f02)) {
    if (-not $allTexts.ContainsKey($id)) { throw "Armos Warrior: missing text $($id.ToString('x4'))." }
    $position = if ($allTextPositions.ContainsKey($id)) { $allTextPositions[$id] } else { 0 }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$id]))
    $texts.Add("$($id.ToString('x4'))`t$position`t$encoded`ttext/ages/text.yaml:TX_$($id.ToString('x4'))")
}
Write-GeneratedTable((Join-Path $destination 'objects/armos_warrior_text.tsv'), $texts)
