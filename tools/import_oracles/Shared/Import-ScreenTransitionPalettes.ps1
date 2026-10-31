# bank1.s:getPaletteFadeTransitionData selects different source formats.
# Ages stores palette words directly; Seasons indexes four seasonal words.
function Read-ScreenPalette($pack, [string]$label) {
    if (-not $pack.Symbols.ContainsKey($label)) { throw "$($pack.Name) paletteTransitions.s: unresolved $label." }
    $at = [int]$pack.Symbols[$label]
    if ($at -lt 0 -or $at + 48 -gt $pack.Bytes.Length -or
        [int][Math]::Floor($at / 0x4000) -ne [int][Math]::Floor(($at + 47) / 0x4000)) {
        throw "$($pack.Name) paletteTransitions.s:${label}: six-palette stream crosses its source bank."
    }
    $rgb = [byte[]]::new(72)
    for ($color = 0; $color -lt 24; $color++) {
        $word = [BitConverter]::ToUInt16($pack.Bytes, $at + $color * 2)
        for ($component = 0; $component -lt 3; $component++) { $rgb[$color * 3 + $component] = ($word -shr ($component * 5)) -band 31 }
    }
    return ([BitConverter]::ToString($rgb)).Replace('-', '')
}
$header = "# group`troom`tdirection`tstate-modifier`tdisabled-flag`tsource-rgb5`tdestination-rgb5`tsource"
$rows = [Collections.Generic.List[string]]::new(); $rows.Add($header)
$directions = @{ DIR_UP = 0; DIR_RIGHT = 1; DIR_DOWN = 2; DIR_LEFT = 3 }
$pack = @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols }
$source = 'data/ages/paletteTransitions.s'
$suppressedRooms = @(Read-AssemblyInstructions (Join-Path $Disassembly 'code/bank1.s') 'checkSymmetryCityPaletteTransition' 'cp' |
    ForEach-Object { Convert-AssemblyInteger $_.Operands[0] })
if ($suppressedRooms.Count -ne 4) { throw 'checkSymmetryCityPaletteTransition must contain four native room comparisons.' }
if (-not $globalFlagValues.ContainsKey('GLOBALFLAG_TUNI_NUT_PLACED')) { throw 'checkSymmetryCityPaletteTransition: unresolved native global flag.' }
$flag = [int]$globalFlagValues['GLOBALFLAG_TUNI_NUT_PLACED']
foreach ($group in 0..2) {
    foreach ($node in Read-AssemblyMacroInvocations (Join-Path $Disassembly $source) "paletteTransitionGroup$group" 'dbbww') {
        if ($node.Operands.Count -ne 4 -or -not $directions.ContainsKey($node.Operands[0])) { throw "${source}: malformed smooth palette route." }
        $room = Convert-AssemblyInteger $node.Operands[1]
        $disabled = if ($group -eq 0 -and $room -in $suppressedRooms) { $flag.ToString('x2') } else { '-' }
        $from = Read-ScreenPalette $pack $node.Operands[2]; $to = Read-ScreenPalette $pack $node.Operands[3]
        $rows.Add("$group`t$($room.ToString('x2'))`t$($directions[$node.Operands[0]])`t-1`t$disabled`t$from`t$to`t${source}:paletteTransitionGroup$group/$($node.Line)")
    }
}
if ($rows.Count -ne 22) { throw "${source}: expected 21 native Ages routes." }
Write-GeneratedTable((Join-Path $destination 'metadata/screen_transition_palettes.tsv'), $rows)

$pack = @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols }
$source = 'data/seasons/paletteTransitions.s'
$palettes = @(Read-AssemblyDataDirectives (Join-Path $Disassembly $source) 'paletteTransitionSeasonData' '.dw' | ForEach-Object { $_.Operands })
if ($palettes.Count -ne 24) { throw "${source}: expected six sets of four seasonal palette pointers." }
for ($index = 0; $index -lt $palettes.Count; $index++) {
    $pointer = [BitConverter]::ToUInt16($seasonsRomBytes, $seasonsSymbols['paletteTransitionSeasonData'] + $index * 2)
    if ($pointer -ne (($seasonsSymbols[$palettes[$index]] -band 0x3fff) + 0x4000)) { throw "${source}: unresolved seasonal palette pointer $index." }
}
$rows = [Collections.Generic.List[string]]::new(); $rows.Add($header)
foreach ($group in 0..1) {
    $terminated = $false
    foreach ($node in Read-AssemblyDataDirectives (Join-Path $Disassembly $source) "paletteTransitionGroup$group" '.db') {
        if ($terminated) { throw "${source}: data follows the source group-$group terminator." }
        if ($node.Operands.Count -eq 2 -and (Convert-AssemblyInteger $node.Operands[0]) -eq 0 -and (Convert-AssemblyInteger $node.Operands[1]) -eq 0xff) {
            $terminated = $true; continue
        }
        if ($node.Operands.Count -ne 4 -or -not $directions.ContainsKey($node.Operands[1])) { throw "${source}: malformed Seasons palette route." }
        $room = Convert-AssemblyInteger $node.Operands[0]
        $fromIndex = Convert-AssemblyInteger $node.Operands[2]; $toIndex = Convert-AssemblyInteger $node.Operands[3]
        if ($fromIndex -lt 0 -or $fromIndex -gt 5 -or $toIndex -lt 0 -or $toIndex -gt 5) { throw "${source}: seasonal palette index outside $00-$05." }
        foreach ($season in 0..3) {
            $from = Read-ScreenPalette $pack $palettes[$fromIndex * 4 + $season]
            $to = Read-ScreenPalette $pack $palettes[$toIndex * 4 + $season]
            $rows.Add("$group`t$($room.ToString('x2'))`t$($directions[$node.Operands[1]])`t$season`t-`t$from`t$to`t${source}:paletteTransitionGroup$group/$($node.Line)/season$season")
        }
    }
    if (-not $terminated) { throw "${source}: missing group-$group terminator." }
}
if ($rows.Count -ne 33) { throw "${source}: expected eight routes in each of four seasons." }
Write-GeneratedTable((Join-Path (Split-Path $previewDestination -Parent) 'metadata/screen_transition_palettes.tsv'), $rows)
