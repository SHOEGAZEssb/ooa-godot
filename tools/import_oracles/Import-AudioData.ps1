# Preserve the original sound engine's address space instead of translating
# 223 individual songs and effects into a new sequencing format. Bank $39
# contains the driver tables and channel descriptors; banks $3a-$3e contain
# the channel bytecode selected by each sound pointer's relative bank byte.
$soundDestination = Join-Path $destination 'audio'
New-Item -ItemType Directory -Force -Path $soundDestination | Out-Null
$soundBaseBank = 0x39
$soundBankCount = 6
$soundBankSize = 0x4000
$soundRomOffset = $soundBaseBank * $soundBankSize
$soundBytes = [byte[]]::new($soundBankCount * $soundBankSize)
[Array]::Copy($romBytes, $soundRomOffset, $soundBytes, 0, $soundBytes.Length)
[IO.File]::WriteAllBytes((Join-Path $soundDestination 'sound_data.bin'), $soundBytes)

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
[IO.File]::WriteAllBytes((Join-Path $soundDestination 'room_music.bin'), $roomMusic)

# Expand the source waveform table by its explicit indices. The table's source
# order is intentionally unrelated to the waveform IDs used by duty commands.
$waveformSource = Get-Content -Raw (Join-Path $Disassembly 'audio\common\waveforms.s')
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
[IO.File]::WriteAllBytes((Join-Path $soundDestination 'waveforms.bin'), $waveforms)

$noiseSource = Get-Content -Raw (Join-Path $Disassembly 'audio\common\noise.s')
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
[IO.File]::WriteAllBytes((Join-Path $soundDestination 'noise_frequencies.bin'), $noiseData)

$audioDriverSource = Get-Content -Raw (Join-Path $Disassembly 'code\audio.s')
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
[IO.File]::WriteAllBytes((Join-Path $soundDestination 'envelope_delays.bin'), $envelopeDelays)

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
[IO.File]::WriteAllBytes((Join-Path $soundDestination 'frequencies.bin'), $frequencyData)

