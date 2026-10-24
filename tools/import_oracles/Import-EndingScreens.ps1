# Native $09 fullscreen headers. Every upload is retained, including RAM
# mirrors, the second tilemap and the credits title window at $9c40.
$gfxSource='data/ages/gfxHeaders.s'
$headers=Read-ImportText (Join-Path $Disassembly $gfxSource)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# profile`tindex`tasset`ttarget`tbank`tpadding`tinvert`tinterleave`toffset`tlength`tsource")
foreach($spec in @(@('oracle','2b'),@('twinrova','2c'),@('closeup','2d'),@('reunion-makutree','3c'),@('credits-maku','85'),@('credits-performance','87'),@('credits-sword','89'),@('credits-construction','8b'),@('credits-maku-past','84'),
    @('credits-image0','86'),@('credits-image1','88'),@('credits-image2','8a'),@('credits-image3','8c'),
    @('credits-image4','8e'),@('credits-image5','90'),@('credits-image6','92'),@('credits-image7','94'),@('credits-linked7','93'),@('credits-linked5','8f'),@('credits-linked4','8d'),@('credits-linked6','91'),
    @('credits-scroll','95'),@('credits-waving','9a'),@('credits-linked-end','99'),@('hero-secret-text','06'),@('credits-end','98'),@('ending-secret','97'),@('ending-continued','96'))) {
    $body=[regex]::Match($headers,'(?ms)^m_GfxHeaderStart \$'+$spec[1]+', [^\r\n]+\r?\n(?<body>.*?)^\s*m_GfxHeaderEnd').Groups['body'].Value
    if(-not $body){throw "${gfxSource}: missing ending header `$${spec[1]}."}
    $index=0
    foreach($line in $body.Split("`n")) {
        if([string]::IsNullOrWhiteSpace($line)){continue}
        if($line -notmatch '^\s*m_GfxHeader (?<asset>\w+), (?<target>\$[0-9a-f]{4}|w3VramTiles|w3VramAttributes|w4TileMap|w4AttributeMap)(?:\+\$(?<offset>[0-9a-f]+))?(?:, \$(?<length>[0-9a-f]{2}))?\s*$'){
            throw "${gfxSource}: unsupported ending header row '$line'."
        }
        $asset=$Matches['asset'];$target=$Matches['target'];$offset=0;$length=0
        if($Matches['offset']){$offset=[Convert]::ToInt32($Matches['offset'],16)}
        if($Matches['length']){$length=[Convert]::ToInt32($Matches['length'],16)*16}
        $bank=0;$padding=0;$invert=if($asset.StartsWith('spr_')){1}else{0};$interleave=$invert
        if($target.StartsWith('$')){$number=Convert-AssemblyInteger $target;$bank=$number -band 1;$target=($number -band 0xfffe).ToString('x4')}
        $extension=if($asset.StartsWith('map_') -or $asset.StartsWith('flg_')){'bin'}else{'png'}
        $relative="gfx_compressible/ages/$asset.$extension"
        if(-not(Test-Path -LiteralPath (Join-Path $Disassembly $relative))){$relative="gfx_compressible/common/$asset.$extension"}
        Copy-GeneratedFile $relative "cutscenes/ending/$asset.$extension"
        $propertyPath=[IO.Path]::ChangeExtension((Join-Path $Disassembly $relative),'.properties')
        if(Test-Path -LiteralPath $propertyPath) {
            $properties=Read-ImportText $propertyPath
            foreach($property in $properties.Split("`n")) {
                if([string]::IsNullOrWhiteSpace($property)){continue}
                if($property -match '^tile_padding:\s*(?<count>\d+)\s*$'){$padding=[int]$Matches['count']}
                elseif($property -match '^invert:\s*(?<value>true|false)\s*$'){$invert=if($Matches['value'] -eq 'true'){1}else{0}}
                elseif($property -match '^interleave:\s*(?<value>true|false)\s*$'){$interleave=if($Matches['value'] -eq 'true'){1}else{0}}
                else{throw "Ending graphics: unsupported source property '$property' in $propertyPath."}
            }
        }
        $rows.Add("$($spec[0])`t$index`t$asset.$extension`t$target`t$bank`t$padding`t$invert`t$interleave`t$offset`t$length`t${gfxSource}:GFXH_`$$($spec[1])");$index++
    }
    $expected=switch($spec[1]){'2b'{8};'2c'{10};'2d'{7};'3c'{8};'84'{2};'85'{2};'87'{2};'89'{2};'8b'{2};'86'{6};'88'{11};'8a'{10};'8c'{10};'8e'{6};'90'{11};'92'{10};'94'{10};'93'{5};'8f'{5};'8d'{5};'91'{5};'95'{9};'9a'{9};'99'{4};'06'{1};'98'{4};'97'{11};'96'{3}}
    if($index -ne $expected){throw "${gfxSource}: header `$$($spec[1]) has $index uploads, expected $expected."}
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/ending_graphics.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# header`tindex`tbuffer`ttarget`tbank`tlength`tsource")
$ramSource='data/ages/uncmpGfxHeaders.s';$index=0
foreach($node in @(Read-AssemblyMacroInvocations (Join-Path $Disassembly $ramSource) 'uncmpGfxHeader2b' 'm_GfxHeaderRam')) {
    if($node.Operands.Count -ne 3){throw "${ramSource}: malformed UNCMP_GFXH_2b upload."}
    $target=Convert-AssemblyInteger $node.Operands[1];$length=(Convert-AssemblyInteger $node.Operands[2])*16
    $rows.Add("2b`t$index`t$($node.Operands[0])`t$(($target -band 0xfffe).ToString('x4'))`t$($target -band 1)`t$length`t${ramSource}:uncmpGfxHeader2b");$index++
}
if($index -ne 3){throw "${ramSource}: expected three UNCMP_GFXH_2b uploads."}
Write-GeneratedTable((Join-Path $destination 'cutscenes/ending_ram_uploads.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# header`tindex`tbuffer`ttarget`tbank`tlength`tsource")
foreach($header in @('01','2c')){
    $index=0
    foreach($node in @(Read-AssemblyMacroInvocations (Join-Path $Disassembly $ramSource) "uncmpGfxHeader$header" 'm_GfxHeaderRam')){
        if($node.Operands.Count -ne 3){throw "${ramSource}: malformed UNCMP_GFXH_$header upload."}
        $target=Convert-AssemblyInteger $node.Operands[1];$length=(Convert-AssemblyInteger $node.Operands[2])*16
        $rows.Add("$header`t$index`t$($node.Operands[0])`t$(($target -band 0xfffe).ToString('x4'))`t$($target -band 1)`t$length`t${ramSource}:uncmpGfxHeader$header");$index++
    }
    if($index -ne $(if($header -eq '01'){1}else{2})){throw "${ramSource}: missing linked ending RAM upload."}
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/ending_linked_ram_uploads.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# header`tkind`tfirst`tcount`tcolors`tsource")
$paletteSource='data/ages/paletteHeaders.s';$headers=Read-ImportText (Join-Path $Disassembly $paletteSource)
foreach($id in @('9d','9e','9c','04','05','0f','30','c6','c7','c8','c9','a1','a0','a7','a9','2d','27','ae','ca','7c','7d','0b','9f','aa','ab','ac','a2')) {
    $body=[regex]::Match($headers,'(?ms)^m_PaletteHeaderStart \$'+$id+', [^\r\n]+\r?\n(?<body>.*?)^\s*m_PaletteHeaderEnd').Groups['body'].Value
    if(-not $body){throw "${paletteSource}: missing `$${id}."}
    if($id -eq 'a7') {
        # The clean-US TO_BE_CONTINUED palette is inside a !REGION_JP
        # block whose .endif follows m_PaletteHeaderEnd.
        if($body -notmatch '^\.ifndef REGION_JP\r?\n'){throw "${paletteSource}: PALH_a7 region guard changed."}
        $body=$body -replace '^\.ifndef REGION_JP\r?\n',''
    }
    foreach($line in $body.Split("`n")) {
        if([string]::IsNullOrWhiteSpace($line)){continue}
        if($line -notmatch '^\s*m_PaletteHeader(?<kind>Bg|Spr)\s+(?<first>\d+),\s*(?<count>\d+),\s*(?<label>\w+)\s*$'){
            throw "${paletteSource}: unsupported row '$line'."
        }
        $first=[int]$Matches['first'];$count=[int]$Matches['count'];$kind=$Matches['kind'];$label=$Matches['label']
        $bytes=Read-PaletteBytes $label ($count*4)
        $rows.Add("$id`t$kind`t$first`t$count`t$(($bytes|ForEach-Object {$_.ToString('x2')}) -join '')`t${paletteSource}:PALH_`$$id/$label")
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/ending_palettes.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# name`tparts`tsource")
foreach($name in @('oamData_4e37','oamData_4d05','oamData_4d9e','oamData_4fec','oamData_4f73','oamData_4ed8','oamData_4f21','oamData_4f56')) {
    $bytes=@(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/endgameCutsceneOamData.s') $name)
    if($bytes.Count -ne 1+$bytes[0]*4 -or $bytes[0] -gt 40){throw "Ending OAM ${name}: malformed count/payload."}
    $parts=for($index=1;$index -lt $bytes.Count;$index+=4){(($bytes[$index..($index+3)]|ForEach-Object {$_.ToString('x2')}) -join ',')}
    $rows.Add("$name`t$($parts -join ';')`tdata/ages/endgameCutsceneOamData.s:$name")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/ending_oam.tsv'),$rows)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# text-id`tmessage-base64`tsource")
foreach($id in @(0x1312,0x280b,0x280c)) {
    $text=Resolve-PalaceText $id ([Collections.Generic.HashSet[int]]::new())
    $rows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($text)))`ttext/ages/text.yaml")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/ending_texts.tsv'),$rows)
# The horizontal-credit bytecode deliberately reads across @script labels and
# rewinds by three bytes during name replacement. Retain the contiguous bytes,
# including the three native bytes beyond the last label (not a fake terminator).
$creditSource='object_code/common/interactions/creditsTextHorizontal.s'
$path=Join-Path $Disassembly $creditSource
$scriptBytes=[Collections.Generic.List[byte]]::new();$starts=[Collections.Generic.List[int]]::new()
foreach($index in 0..3) {
    $starts.Add($scriptBytes.Count)
    foreach($value in @(Read-AssemblyLiteralValues $path "@script$index")){$scriptBytes.Add([byte]$value)}
}
$matches=[Collections.Generic.List[int]]::new()
for($offset=0x0b*0x4000;$offset -lt 0x0c*0x4000-$scriptBytes.Count-3;$offset++) {
    if($romBytes[$offset] -ne $scriptBytes[0]){continue}
    $same=$true
    for($index=1;$index -lt $scriptBytes.Count;$index++){if($romBytes[$offset+$index] -ne $scriptBytes[$index]){$same=$false;break}}
    if($same){$matches.Add($offset)}
}
if($matches.Count -ne 1){throw "${creditSource}: contiguous script stream must uniquely match the clean US ROM."}
$tail=$matches[0]+$scriptBytes.Count
foreach($index in 0..2){$scriptBytes.Add($romBytes[$tail+$index])}
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# kind`tindex`tdata`tsource")
$rows.Add("bytes`t0`t$(($scriptBytes|ForEach-Object {$_.ToString('x2')}) -join '')`t${creditSource}:@script0..3 + clean-US boundary")
$settings=@(Read-AssemblyLiteralValues $path 'horizontalCreditsText_65b1')
if($settings.Count -ne 16){throw "${creditSource}: expected eight direction/animation pairs."}
$scriptTable=@(Read-AssemblyDataDirectives $path 'horizontalCreditsText_scriptTable' '.dw'|ForEach-Object {$_.Operands})
if($scriptTable.Count -ne 8){throw "${creditSource}: expected eight script pointers, including linked aliases."}
foreach($index in 0..7){
    if($scriptTable[$index] -notmatch '^@script(?<index>[0-3])$'){throw "${creditSource}: unsupported script pointer '$($scriptTable[$index])'."}
    $start=$starts[[int]$Matches['index']]
    $rows.Add("entry`t$index`t$start,$($settings[$index*2]),$($settings[$index*2+1])`t${creditSource}:horizontalCreditsText_scriptTable")
}
foreach($index in 0..0x1a) {
    $animation=Resolve-NpcAnimation 0xae $index
    if(-not $animation){throw "INTERAC `$ae: missing animation `$${index}."}
    $rows.Add("animation`t$index`t$animation`tdata/ages/interactionAnimations.s:interactionaeAnimations")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/credits_horizontal.tsv'),$rows)
# INTERAC $af directly submits counted OAM rows. Some declared counts read
# through the next label; retain the contiguous byte stream before slicing.
$creditSource='object_code/common/interactions/creditsTextVertical.s'
$us=[regex]::Match((Read-ImportText (Join-Path $Disassembly $creditSource)),'(?ms)^\.else ; REGION_US\s*(?<body>.*?)^\.endif').Groups['body'].Value
$waits=@([regex]::Matches($us,'(?m)^\s*\.dw \$(?<word>[0-9a-f]{4})')|ForEach-Object {[Convert]::ToInt32($_.Groups['word'].Value,16)})
if($waits.Count -ne 17 -or $us -notmatch '(?m)^\s*\.db \$ff\s*$'){throw "${creditSource}: missing clean-US staff-roll waits/sentinel."}
$bytes=[Collections.Generic.List[byte]]::new()
foreach($word in $waits){$bytes.Add([byte]($word -band 255));$bytes.Add([byte]($word -shr 8))};$bytes.Add(255)
$matches=@(for($at=0x0b*0x4000;$at -le 0x0c*0x4000-$bytes.Count-1;$at++) {
    if($romBytes[$at] -ne $bytes[0]){continue};$same=$true
    for($index=1;$index -lt $bytes.Count;$index++){if($romBytes[$at+$index] -ne $bytes[$index]){$same=$false;break}}
    if($same){$at}
})
if($matches.Count -ne 1){throw "${creditSource}: waits must uniquely match the clean US ROM."}
$waits+=255+([int]$romBytes[$matches[0]+$bytes.Count] -shl 8)
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# kind`tindex`tdata`tsource")
foreach($index in 0..17){$rows.Add("wait`t$index`t$($waits[$index])`t${creditSource}:@data_66bc + clean-US sentinel boundary")}
$oamSource='data/ages/data_4556.s';$path=Join-Path $Disassembly $oamSource
$raw=[Collections.Generic.List[byte]]::new();$starts=@{}
foreach($node in @(Read-AssemblyDataDirectives $path '' '.db')) {
    if(-not $starts.ContainsKey($node.EnclosingGlobalLabel)){$starts[$node.EnclosingGlobalLabel]=$raw.Count}
    foreach($value in $node.Operands){$raw.Add([byte](Convert-AssemblyInteger $value))}
}
$targets=@(Read-AssemblyDataDirectives $path 'data_4556' '.dw'|ForEach-Object {$_.Operands})
if($targets.Count -ne 17){throw "${oamSource}: expected seventeen source OAM pointers."}
foreach($index in 0..16) {
    $start=$starts[$targets[$index]];$count=$raw[$start]
    if($count -gt 40 -or $start+1+$count*4 -gt $raw.Count){throw "${oamSource}: malformed counted row $($targets[$index])."}
    $parts=for($part=0;$part -lt $count;$part++) {($raw[($start+1+$part*4)..($start+4+$part*4)]|ForEach-Object {$_.ToString('x2')}) -join ','}
    $rows.Add("oam`t$index`t$($parts -join ';')`t${oamSource}:$($targets[$index]) (counted contiguous bytes)")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/credits_vertical.tsv'),$rows)
$white=[byte[]]::new(64);for($index=0;$index -lt 64;$index++){$white[$index]=255}
Write-GeneratedGrayscalePng (Join-Path $destination 'cutscenes/ending/zero_tile.png') 8 8 $white
