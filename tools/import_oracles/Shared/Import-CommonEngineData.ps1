$commonDestination = Join-Path (Split-Path $destination -Parent) 'common'
# These tables come from common assembly. Verify the compiled bytes in both
# supported games before publishing one shared runtime copy.
foreach ($table in @(
    @('objectSpeedTable', 1920, 'metadata/object_speed_vectors.tsv'),
    @('pushDirectionData', 64, 'metadata/object_relative_angles.tsv'),
    @('gbaModePaletteData', 128, 'metadata/gba_palette_components.bin'),
    @('initWaveScrollValues_body@sineWave', 32, 'metadata/screen_wave_sine.tsv'),
    @('calculateAdjacentWallsBitset@overworldOffsets', 32, 'metadata/switch_hook_wall_probes.tsv'),
    @('ecom_sideviewAdjacentWallOffsetTable', 64, 'metadata/enemy_adjacent_wall_offsets.tsv'),
    @('ecom_topDownAdjacentWallOffsetTable', 64, 'metadata/enemy_topdown_adjacent_wall_offsets.tsv'),
    @('ecom_bounceOffScreenBoundary@angleTable', 48, 'metadata/enemy_bounce_angles.tsv')
)) {
    $symbol = $table[0]; $count = $table[1]
    if (-not $agesSymbols.ContainsKey($symbol) -or -not $seasonsSymbols.ContainsKey($symbol)) { throw "Shared engine table $symbol has no game symbols." }
    $agesStart = $agesSymbols[$symbol]; $seasonsStart = $seasonsSymbols[$symbol]
    for ($index = 0; $index -lt $count; $index++) {
        if ($romBytes[$agesStart + $index] -ne $seasonsRomBytes[$seasonsStart + $index]) { throw "Shared engine table $symbol differs between games at byte $index." }
    }
    Write-GeneratedBytes((Join-Path $commonDestination $table[2]), [IO.File]::ReadAllBytes((Join-Path $destination $table[2])))
}
# This is the port's normalized command contract, independent of game opcodes.
Write-GeneratedBytes((Join-Path $commonDestination 'cutscenes/script_command_vocabulary.tsv'), [IO.File]::ReadAllBytes((Join-Path $destination 'cutscenes/script_command_vocabulary.tsv')))
