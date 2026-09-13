$parentSource = 'object_code/ages/enemies/eyesoar.s'
$childSource = 'object_code/ages/enemies/eyesoarChild.s'
$directions = @{}
foreach ($row in Read-AssemblyConstants (Join-Path $Disassembly 'constants/common/directions.s')) {
    if ($row.Name -match '^ANGLE_') {
        $directions[$row.Name] = Convert-AssemblyInteger $row.OperandText
    }
}
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# profile`tvalues`tsource")
foreach ($spec in @(
    @('formation-distances', $parentSource, 'distancesFromEyesoar', 8),
    @('center-angles', $parentSource, '@angleVals', 4),
    @('child-angles', $childSource, '@initialAnglesForSubids', 4),
    @('child-ready-flags', $childSource, '@data', 4)
)) {
    $values = [Collections.Generic.List[string]]::new()
    foreach ($row in Read-AssemblyDataDirectives (Join-Path $Disassembly $spec[1]) $spec[2] '.db') {
        foreach ($token in $row.Operands) {
            $value = if ($token -match '^\$([0-9a-f]{2})$') { [Convert]::ToInt32($Matches[1], 16) }
                elseif ($directions.ContainsKey($token)) { $directions[$token] }
                else { throw "Eyesoar $($spec[1]):$($spec[2]): unsupported byte $token." }
            $values.Add($value.ToString('x2'))
        }
    }
    if ($values.Count -ne $spec[3]) { throw "Eyesoar $($spec[2]): expected $($spec[3]) bytes." }
    $rows.Add("$($spec[0])`t$($values -join ',')`t$($spec[1]):$($spec[2])")
}
$collisionValues = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/objectCollisionTable.s') 'objectCollisionTable')
if ($collisionValues.Count -ne 4096) { throw 'Eyesoar: incomplete collision table.' }
foreach ($mode in @(0x15, 0x4c, 0x6d)) {
    $values = $collisionValues[($mode * 32)..($mode * 32 + 31)] | ForEach-Object { $_.ToString('x2') }
    $rows.Add("collision-$($mode.ToString('x2'))`t$($values -join ',')`tdata/ages/objectCollisionTable.s:objectCollisionTable+$((32 * $mode).ToString('x4'))")
}
$active = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/ages/enemyActiveCollisions.s') 'enemyActiveCollisions' 'dbrev')
foreach ($id in @(0x11, 0x7b)) {
    if ($active.Count -ne 128 -or $active[$id].Operands.Count -ne 4) { throw 'Eyesoar: incomplete active collision masks.' }
    $bits = ($active[$id].Operands -join '').Replace('%', '')
    if ($bits -notmatch '^[01]{32}$') { throw 'Eyesoar: malformed active collision mask.' }
    $rows.Add("active-$($id.ToString('x2'))`t$(($bits.ToCharArray() | ForEach-Object { '0' + $_ }) -join ',')`tdata/ages/enemyActiveCollisions.s:enemyActiveCollisions+$((4 * $id).ToString('x4'))")
}
Write-GeneratedTable((Join-Path $destination 'objects/eyesoar_tables.tsv'), $rows)
