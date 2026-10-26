$seasonDestination = Split-Path $previewDestination -Parent
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# room`torder`tkind`tid`tsubid`ty`tx`ttarget`tsource")
$path = Join-Path $Disassembly 'objects/seasons/mainData.s'
foreach ($room in @(0x97, 0x98, 0xa7, 0xb6)) {
    $label = 'group0Map' + $room.ToString('x2') + 'ObjectData'
    $nodes = @(Read-AssemblyMacroInvocations $path $label)
    if ($nodes.Count -eq 0 -or $nodes[-1].Name -ne 'obj_End') { throw "${label}: missing object-stream terminator." }
    $order = 0
    foreach ($node in $nodes) {
        $source = "objects/seasons/mainData.s:$label"
        switch ($node.Name) {
            'obj_Interaction' {
                if ($node.Operands.Count -notin @(2, 4)) { throw "${source}: unsupported interaction operands." }
                $values = @($node.Operands | ForEach-Object {
                    if ($_ -notmatch '^\$[0-9a-f]{2}$') { throw "${source}: expected a literal interaction byte, got $_." }
                    $_.Substring(1)
                })
                if ($values.Count -eq 2) { $values += @('00', '00') }
                $rows.Add("$($room.ToString('x2'))`t$order`tinteraction`t$($values -join "`t")`t`t$source")
                $order++
            }
            'obj_AfterEvent' {
                if ($node.Operands.Count -ne 1) { throw "${source}: invalid after-event pointer." }
                $rows.Add("$($room.ToString('x2'))`t$order`tafter-event`t00`t00`t00`t00`t$($node.Operands[0])`t$source")
                $order++
            }
            'obj_End' { }
            default { throw "${source}: unsupported room object macro $($node.Name)." }
        }
    }
}
Write-GeneratedTable((Join-Path $seasonDestination 'opening/room_objects.tsv'), $rows)

$refills = [Collections.Generic.List[string]]::new()
$refills.Add("# index`tgroup`troom`tbit-index")
$start = $seasonsSymbols['seedTreeRefillLocations']
if ($null -eq $start) { throw 'Seasons seedTreeRefillLocations: missing compiled symbol.' }
for ($index = 0; $index -lt 8; $index++) {
    $room = $seasonsRomBytes[$start + $index * 2]
    $offset = $seasonsRomBytes[$start + $index * 2 + 1]
    if ($offset -ne (($seasonsSymbols['wxSeedTreeRefillData'] + $index * 8) -band 0xff)) { throw "Seasons seedTreeRefillLocations: unexpected history offset at index $index." }
    $refills.Add("$index`t0`t$($room.ToString('x2'))`t$(7 - $index)")
}
Write-GeneratedTable((Join-Path $seasonDestination 'metadata/seed_tree_refills.tsv'), $refills)
