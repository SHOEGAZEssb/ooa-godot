# Preserve the original sound engine's address space instead of translating
# 223 individual songs and effects into a new sequencing format. Bank $39
# contains the driver tables and channel descriptors; banks $3a-$3e contain
# the channel bytecode selected by each sound pointer's relative bank byte.
$soundDestination = Join-Path $destination 'audio'
$soundBaseBank = 0x39
$soundBankCount = 6
$soundBankSize = 0x4000
$soundRomOffset = $soundBaseBank * $soundBankSize
$soundBytes = [byte[]]::new($soundBankCount * $soundBankSize)
[Array]::Copy($romBytes, $soundRomOffset, $soundBytes, 0, $soundBytes.Length)
Write-GeneratedBytes((Join-Path $soundDestination 'sound_data.bin'), $soundBytes)

# Room music is one byte per room in each of the six gameplay groups. Groups
# 6 and 7 alias groups 4 and 5 in musicAssignmentGroupTable and are normalized
# by the runtime database.
$roomMusic = [byte[]]::new(6 * 256)
for ($group = 0; $group -lt 6; $group++) {
    $groupMusic = [IO.File]::ReadAllBytes(
        (Join-Path $Disassembly "audio\ages\group${group}IDs.bin"))
    if ($groupMusic.Length -ne 256) {
        throw "Expected 256 music assignments for group $group, got $($groupMusic.Length)."
    }
    [Array]::Copy($groupMusic, 0, $roomMusic, $group * 256, 256)
}
Write-GeneratedBytes((Join-Path $soundDestination 'room_music.bin'), $roomMusic)

# Room 1:97 runs roomSpecificCode7 after loadScreenMusic has selected the
# ordinary past-overworld assignment. While Ralph's post-Rafton event is
# pending, it replaces wActiveMusic2 with MUS_RALPH before checkPlayRoomMusic.
$roomSpecificCodeSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\roomSpecificCode.s')
$musicConstantSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$ralphMusicMatch = [regex]::Match(
    $musicConstantSource,
    '(?m)^\s*MUS_RALPH\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $globalFlagValues.ContainsKey('GLOBALFLAG_GAVE_ROPE_TO_RAFTON') -or
    $globalFlagValues['GLOBALFLAG_GAVE_ROPE_TO_RAFTON'] -ne 0x15 -or
    -not $ralphMusicMatch.Success -or
    [Convert]::ToInt32($ralphMusicMatch.Groups['value'].Value, 16) -ne 0x35 -or
    $roomSpecificCodeSource -notmatch
        '(?ms)^roomSpecificCodeGroup1Table:\s+\.db \$81 \$03\s+\.db \$38 \$06\s+\.db \$97 \$07\s+\.db \$0e \$0a\s+\.db \$00' -or
    $roomSpecificCodeSource -notmatch
        '(?ms)^roomSpecificCode7:\s+ld a,GLOBALFLAG_GAVE_ROPE_TO_RAFTON\s+call checkGlobalFlag\s+ret z\s+call getThisRoomFlags\s+bit 6,a\s+ret nz\s+ld a,MUS_RALPH\s+ld \(wActiveMusic2\),a\s+ret') {
    throw 'Room 1:97 roomSpecificCode7 Ralph-music override changed.'
}
$conditionalRoomMusicRows = [Collections.Generic.List[string]]::new()
$conditionalRoomMusicRows.Add(
    '# group`troom`tmusic`trequired-global-flag`tclear-room-flag-mask`tsource')
$conditionalRoomMusicRows.Add(
    "1`t97`t35`t15`t40`tcode/ages/roomSpecificCode.s:roomSpecificCode7")
Write-GeneratedTable(
    (Join-Path $soundDestination 'conditional_room_music.tsv'),
    $conditionalRoomMusicRows)

# Expand the source waveform table by its explicit indices. The table's source
# order is intentionally unrelated to the waveform IDs used by duty commands.
$waveformSource = Read-ImportText (Join-Path $Disassembly 'audio\common\waveforms.s')
$waveforms = [byte[]]::new(0x2e * 16)
$waveformIds = [Collections.Generic.HashSet[int]]::new()
$waveformMatches = [regex]::Matches(
    $waveformSource,
    '(?ms)^m_waveform\s+\$(?<id>[0-9a-f]{2}),[^\r\n]*\r?\n\s*\.db\s+(?<bytes>(?:\$[0-9a-f]{2}\s*){16})')
foreach ($waveform in $waveformMatches) {
    $id = [Convert]::ToInt32($waveform.Groups['id'].Value, 16)
    $values = [regex]::Matches($waveform.Groups['bytes'].Value, '\$(?<value>[0-9a-f]{2})')
    if ($id -ge 0x2e -or $values.Count -ne 16 -or -not $waveformIds.Add($id)) {
        throw "Invalid or duplicate sound waveform $($id.ToString('x2'))."
    }
    for ($index = 0; $index -lt 16; $index++) {
        $waveforms[$id * 16 + $index] =
            [Convert]::ToByte($values[$index].Groups['value'].Value, 16)
    }
}
if ($waveformIds.Count -ne 0x2e) {
    throw "Expected 46 indexed sound waveforms, parsed $($waveformIds.Count)."
}
Write-GeneratedBytes((Join-Path $soundDestination 'waveforms.bin'), $waveforms)

$noiseSource = Read-ImportText (Join-Path $Disassembly 'audio\common\noise.s')
$noiseRows = [regex]::Matches(
    $noiseSource,
    '(?m)^\s*\.db\s+\$(?<note>[0-9a-f]{2})\s+\$(?<envelope>[0-9a-f]{2})\s+\$(?<frequency>[0-9a-f]{2})')
$noiseData = [byte[]]::new($noiseRows.Count * 3)
for ($row = 0; $row -lt $noiseRows.Count; $row++) {
    $noiseData[$row * 3] = [Convert]::ToByte($noiseRows[$row].Groups['note'].Value, 16)
    $noiseData[$row * 3 + 1] = [Convert]::ToByte($noiseRows[$row].Groups['envelope'].Value, 16)
    $noiseData[$row * 3 + 2] = [Convert]::ToByte($noiseRows[$row].Groups['frequency'].Value, 16)
}
if ($noiseRows.Count -ne 13) {
    throw "Expected 13 noise-frequency records, parsed $($noiseRows.Count)."
}
Write-GeneratedBytes((Join-Path $soundDestination 'noise_frequencies.bin'), $noiseData)

$audioDriverSource = Read-ImportText (Join-Path $Disassembly 'code\audio.s')
$envelopeDelayBlock = [regex]::Match(
    $audioDriverSource,
    '(?ms)^data_4ad0:\s*(?<body>.*?)(?=^;;\s*; @param a The sound to play\.)')
$envelopeDelayValues = [regex]::Matches(
    $envelopeDelayBlock.Groups['body'].Value, '\$(?<value>[0-9a-f]{2})')
if (-not $envelopeDelayBlock.Success -or $envelopeDelayValues.Count -ne 128) {
    throw "Expected 128 envelope-delay/vibrato bytes, parsed $($envelopeDelayValues.Count)."
}
$envelopeDelays = [byte[]]::new($envelopeDelayValues.Count)
for ($delay = 0; $delay -lt $envelopeDelayValues.Count; $delay++) {
    $envelopeDelays[$delay] =
        [Convert]::ToByte($envelopeDelayValues[$delay].Groups['value'].Value, 16)
}
Write-GeneratedBytes((Join-Path $soundDestination 'envelope_delays.bin'), $envelopeDelays)

$frequencyBlock = [regex]::Match(
    $audioDriverSource,
    '(?ms)^soundFrequencyTable:\s*(?<body>.*?)(?=^data_4ad0:)')
$frequencies = [regex]::Matches($frequencyBlock.Groups['body'].Value, '\.dw\s+\$(?<value>[0-9a-f]{4})')
if (-not $frequencyBlock.Success -or $frequencies.Count -ne 87) {
    throw "Expected 87 sound-frequency words, parsed $($frequencies.Count)."
}
$frequencyData = [byte[]]::new($frequencies.Count * 2)
for ($frequency = 0; $frequency -lt $frequencies.Count; $frequency++) {
    $value = [Convert]::ToInt32($frequencies[$frequency].Groups['value'].Value, 16)
    $frequencyData[$frequency * 2] = [byte]($value -band 0xff)
    $frequencyData[$frequency * 2 + 1] = [byte](($value -shr 8) -band 0xff)
}
Write-GeneratedBytes((Join-Path $soundDestination 'frequencies.bin'), $frequencyData)
