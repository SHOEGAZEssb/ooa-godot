# Timing data is derived from the original decoder and compressed input. It
# excludes interrupts and LCD waits, which belong to the runtime clock.
$loadingGfxSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\gfxHeaders.s')
if ($loadingGfxSource -notmatch '(?m)^\.define NUM_GFX_HEADERS \$bb\s*$') {
    throw 'data/ages/gfxHeaders.s:NUM_GFX_HEADERS changed from $bb.'
}
$loadingRows = Invoke-AssemblySourceHost $assemblySourceHost 'LOADING_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\graphics_cpu.tsv'),
    $loadingRows.TrimEnd().Split("`n"))
$randomRows = Invoke-AssemblySourceHost $assemblySourceHost 'RANDOM_BUFFER_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\random_buffer_cpu.tsv'),
    $randomRows.TrimEnd().Split("`n"))
$roomGroupSource = Read-ImportText (Join-Path $Disassembly 'data\ages\roomLayoutGroupTable.s')
if ([regex]::Matches($roomGroupSource, '3BytePointer roomLayoutGroup[0-5]Table').Count -ne 6) {
    throw 'data/ages/roomLayoutGroupTable.s must contain the six clean-US layout groups.'
}
$timingLayouts = ($tilesets | ForEach-Object { $_.Groups['layout'].Value } | Sort-Object -Unique) -join ','
$roomRows = Invoke-AssemblySourceHost $assemblySourceHost 'ROOM_LOADING_WORK' (
    [Convert]::ToBase64String($romBytes) + [char]0 + $timingLayouts)
Write-GeneratedTable(
    (Join-Path $destination 'timing\room_cpu.tsv'),
    $roomRows.TrimEnd().Split("`n"))
$seaRows = Invoke-AssemblySourceHost $assemblySourceHost 'SEA_SEARCH_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\sea_search_cpu.tsv'),
    $seaRows.TrimEnd().Split("`n"))
$gameplayRows = Invoke-AssemblySourceHost $assemblySourceHost 'GAMEPLAY_DISPATCH_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\gameplay_dispatch_cpu.tsv'),
    $gameplayRows.TrimEnd().Split("`n"))
$wallRows = Invoke-AssemblySourceHost $assemblySourceHost 'LINK_WALL_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\link_wall_cpu.tsv'),
    $wallRows.TrimEnd().Split("`n"))
$activeTileRows = Invoke-AssemblySourceHost $assemblySourceHost 'ACTIVE_TILE_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\active_tile_cpu.tsv'),
    $activeTileRows.TrimEnd().Split("`n"))
$tileInteractionRows = Invoke-AssemblySourceHost $assemblySourceHost 'TILE_INTERACTION_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\tile_interaction_cpu.tsv'),
    $tileInteractionRows.TrimEnd().Split("`n"))
$pegasusRows = Invoke-AssemblySourceHost $assemblySourceHost 'PEGASUS_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\pegasus_cpu.tsv'),
    $pegasusRows.TrimEnd().Split("`n"))
$idleItemRows = Invoke-AssemblySourceHost $assemblySourceHost 'IDLE_ITEM_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\idle_item_cpu.tsv'),
    $idleItemRows.TrimEnd().Split("`n"))
$linkStateRows = Invoke-AssemblySourceHost $assemblySourceHost 'LINK_STATE_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\link_state_cpu.tsv'),
    $linkStateRows.TrimEnd().Split("`n"))
$pirateRows = Invoke-AssemblySourceHost $assemblySourceHost 'PIRATE_COURSE_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\pirate_course_cpu.tsv'),
    $pirateRows.TrimEnd().Split("`n"))
$frontendRows = Invoke-AssemblySourceHost $assemblySourceHost 'FRONTEND_WORK' (
    [Convert]::ToBase64String($romBytes))
Write-GeneratedTable(
    (Join-Path $destination 'timing\frontend_cpu.tsv'),
    $frontendRows.TrimEnd().Split("`n"))
$textboxIds = ($allTexts.Keys | Sort-Object { [int]$_ } | ForEach-Object { [string]$_ }) -join ','
$textboxRows = Invoke-AssemblySourceHost $assemblySourceHost 'TEXTBOX_WORK' (
    [Convert]::ToBase64String($romBytes) + [char]0 + $textboxIds)
Write-GeneratedTable(
    (Join-Path $destination 'timing\textbox_cpu.tsv'),
    $textboxRows.TrimEnd().Split("`n"))
