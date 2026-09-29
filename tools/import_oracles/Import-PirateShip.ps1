# The global course runs even when INTERAC_PIRATE_SHIP is offscreen.
$shipPath = Join-Path $Disassembly 'code/ages/pirateShip.s'
$shipRows = [Collections.Generic.List[string]]::new()
$shipRows.Add("# linked`torder`troom`ttile`tdirection`tsource")
$shipDirections = @{ DIR_UP = 0; DIR_RIGHT = 1; DIR_DOWN = 2; DIR_LEFT = 3 }
foreach ($linked in 0..1) {
    $label = if ($linked -eq 0) { '@shipDirectionsPast' } else { '@shipDirectionsPresent' }
    $nodes = @(Read-AssemblyDataDirectives $shipPath $label '.db')
    $order = 0
    $terminated = $false
    foreach ($node in $nodes) {
        if ($terminated) { throw "${shipPath}:${label}: data follows the course terminator." }
        if ($node.Operands.Count -eq 1 -and $node.Operands[0] -eq '$00') {
            $terminated = $true
            continue
        }
        if ($node.Operands.Count -ne 3 -or -not $shipDirections.ContainsKey($node.Operands[2])) {
            throw "$($node.Path):$($node.Line): unsupported pirate ship course row."
        }
        $room = Convert-AssemblyInteger $node.Operands[0]
        $tile = Convert-AssemblyInteger $node.Operands[1]
        if ($room -le 0 -or $room -gt 255 -or $tile -lt 0 -or $tile -gt 255) {
            throw "$($node.Path):$($node.Line): invalid pirate ship room/tile."
        }
        $shipRows.Add(("{0}`t{1}`t{2:x2}`t{3:x2}`t{4}`tcode/ages/pirateShip.s:{5}:{6}" -f
            $linked, $order, $room, $tile, $shipDirections[$node.Operands[2]], $label, $node.Line))
        $order++
    }
    if (-not $terminated -or $order -ne $(if ($linked -eq 0) { 6 } else { 12 })) {
        throw "${shipPath}:${label}: incomplete pirate ship course."
    }
}
Write-GeneratedTable((Join-Path $destination 'world/pirate_ship_course.tsv'), $shipRows)
$shipSpeeds = @(Read-AssemblyDataDirectives $shipPath '@speedComponents' '.db')
if ($shipSpeeds.Count -ne 4) { throw "${shipPath}: expected four speed component pairs." }
$shipSpeedRows = [Collections.Generic.List[string]]::new()
$shipSpeedRows.Add("# direction`tdy`tdx`tsource")
for ($direction = 0; $direction -lt 4; $direction++) {
    $node = $shipSpeeds[$direction]
    if ($node.Operands.Count -ne 2) { throw "$($node.Path):$($node.Line): invalid speed pair." }
    $dy = Convert-AssemblyInteger $node.Operands[0]
    $dx = Convert-AssemblyInteger $node.Operands[1]
    if ($dy -notin @(0,1,255) -or $dx -notin @(0,1,255)) { throw 'Unsupported pirate ship speed.' }
    $shipSpeedRows.Add(("{0}`t{1:x2}`t{2:x2}`tcode/ages/pirateShip.s:@speedComponents:{3}" -f $direction,$dy,$dx,$node.Line))
}
Write-GeneratedTable((Join-Path $destination 'world/pirate_ship_speed.tsv'), $shipSpeedRows)
