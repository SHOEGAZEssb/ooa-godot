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
