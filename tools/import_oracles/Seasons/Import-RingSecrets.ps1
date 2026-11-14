$root = Split-Path $previewDestination -Parent
function Secret-Bytes([string]$label, [int]$count) {
    if (!$seasonsSymbols.ContainsKey($label)) { throw "code/bank3.s: missing Seasons secret symbol $label." }
    $address = $seasonsSymbols[$label]
    return $seasonsRomBytes[$address..($address+$count-1)]
}
$cipher = Secret-Bytes 'secretXorCipher' 48
$rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`txor")
for ($index=0; $index -lt 48; $index++) { $rows.Add("$index`t$($cipher[$index].ToString('x2'))") }
Write-GeneratedTable((Join-Path $root 'objects/linked_secret_cipher.tsv'), $rows)

$controls = @{0x10='\club';0x11='\heart';0x12='\diamond';0x13='\spade';0x15='\up';0x16='\down';0x17='\left';0x18='\right';0x5c='~';0x7e='\triangle';0x7f='\rectangle';0xbd='\heart'}
$symbols = Secret-Bytes 'secretSymbols' 64
$glyphs = [Collections.Generic.List[string]]::new(); $glyphs.Add("# index`tglyph")
$rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`tutf8-base64")
for ($index=0; $index -lt 64; $index++) {
    $value = [int]$symbols[$index]
    $text = if ($controls.ContainsKey($value)) { $controls[$value] }
        elseif ($value -ge 0x20 -and $value -le 0x7d) { [string][char]$value }
        else { throw "code/bank0.s:secretSymbols: unsupported Seasons glyph `$$($value.ToString('x2'))." }
    $rows.Add("$index`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($text)))")
    $glyphs.Add("$index`t$($value.ToString('x2'))")
}
Write-GeneratedTable((Join-Path $root 'objects/linked_secret_symbols.tsv'), $rows)
Write-GeneratedTable((Join-Path $root 'menu/secret_glyphs.tsv'), $glyphs)
$offsets = Secret-Bytes 'drawSecretInputCursors@lowerOptionsOffsets' 4
if (($offsets -join ',') -ne '24,48,84,120') { throw 'code/bank2.s:drawSecretInputCursors@lowerOptionsOffsets: changed Seasons secret options.' }
$rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`tx")
for ($index=0; $index -lt 4; $index++) { $rows.Add("$index`t$($offsets[$index])") }
Write-GeneratedTable((Join-Path $root 'menu/secret_lower_offsets.tsv'), $rows)

$fields = Secret-Bytes 'secretDataToEncodeTable@entry2' 19
if ($fields[0] -ne 9) { throw 'code/bank3.s:secretDataToEncodeTable@entry2: expected nine ring fields.' }
$rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`taddress`tbits`tsource")
$offsets = @(1,5,7,3,0,4,2,6)
for ($index=0; $index -lt 9; $index++) {
    $address = 0xc600 -bor $fields[1+$index*2]; $bits = $fields[2+$index*2]
    $expectedAddress = if ($index -lt 8) { $seasonsSymbols['wRingsObtained']+$offsets[$index] } else { $seasonsSymbols['wLinkName']+5 }
    if ($address -ne $expectedAddress -or $bits -ne $(if ($index -lt 8) { 8 } else { 2 })) {
        throw "code/bank3.s:secretDataToEncodeTable@entry2: changed Seasons field $index."
    }
    $rows.Add("$index`t$($address.ToString('x4'))`t$bits`tcode/bank3.s:secretDataToEncodeTable@entry2")
}
Write-GeneratedTable((Join-Path $root 'objects/linked_secret_ring_fields.tsv'), $rows)
$spacing = Secret-Bytes 'convertSecretBufferToText@entry2' 4
if (($spacing -join ',') -ne '37,5,37,0') { throw 'code/bank3.s:convertSecretBufferToText@entry2: changed Seasons ring spacing.' }
$rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`tvalue`tsource")
for ($index=0; $index -lt 4; $index++) { $rows.Add("$index`t$($spacing[$index].ToString('x2'))`tcode/bank3.s:convertSecretBufferToText@entry2") }
Write-GeneratedTable((Join-Path $root 'objects/linked_secret_ring_spacing.tsv'), $rows)
