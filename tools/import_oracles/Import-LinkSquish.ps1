& {
$path = Join-Path $Disassembly 'data/ages/specialObjectAnimationData.s'
$source = Read-ImportText $path
$oamSource = Read-ImportText (Join-Path $Disassembly 'data/ages/specialObjectOamData.s')
$pointers = Read-AssemblyDwTables $path 'specialObject(?:00|09)AnimationDataPointers' 'animationData\w+'
$gfxBlock = [regex]::Match($source,'(?ms)^specialObject00GfxPointers:.*?(?=^specialObject00AnimationDataPointers:)')
$gfx = [regex]::Matches($gfxBlock.Value,'(?m)^\s*m_SpecialObjectGfxPointer \$(?<oam>[0-9a-f]{2}) spr_link \$(?<offset>[0-9a-f]{4}) \$(?<size>[0-9a-f]{2})')
$oamBlock = [regex]::Match($source,'(?ms)^specialObject00OamDataPointers:.*?(?=^specialObject02GfxPointers:)')
$oamLabels = @([regex]::Matches($oamBlock.Value,'(?m)^\s*\.dw (?<label>oamData[0-9a-f]+)') | ForEach-Object { $_.Groups['label'].Value })
if ($gfx.Count -ne 256 -or $oamLabels.Count -ne 48) { throw 'Link squish lost shared graphics/OAM tables.' }
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# mode`tframe`tduration`tgraphic`tparameter`tnext`toffset`toam`tsource")
foreach ($mode in @(6,7)) {
    $label = $pointers['specialObject00AnimationDataPointers'][$mode]
    $animation = [regex]::Match($source,"(?ms)^${label}:\s+\.db (?<first>\$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2})\s+(?<loop>animationLoop[0-9a-f]+):\s+\.db (?<second>\$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2})\s+\.db (?<third>\$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2})\s+m_AnimationLoop \k<loop>")
    if (-not $animation.Success) { throw "Unsupported Link squish stream $label." }
    $index = 0
    foreach ($name in @('first','second','third')) {
        $bytes = @([regex]::Matches($animation.Groups[$name].Value,'\$(?<value>[0-9a-f]{2})') | ForEach-Object { [Convert]::ToInt32($_.Groups['value'].Value,16) })
        $graphic = $gfx[$bytes[1]]
        $oamLabel = $oamLabels[[Convert]::ToInt32($graphic.Groups['oam'].Value,16)]
        $oam = [regex]::Match($oamSource,"(?ms)^${oamLabel}:\s*(?<body>.*?)(?=^oamData[0-9a-f]+:|\z)")
        $oamBytes = @(Read-HexBytes $oam.Groups['body'].Value)
        if (-not $oam.Success -or $oamBytes.Count -ne 1+4*$oamBytes[0]) { throw "Malformed squish OAM $oamLabel." }
        $parts = for ($i = 0; $i -lt $oamBytes[0]; $i++) { ($oamBytes[(1+4*$i)..(4+4*$i)] -join ',') }
        $next = if ($index -eq 2) {1} else {$index+1}
        $rows.Add("$($mode.ToString('x2'))`t$index`t$($bytes[0])`t$($bytes[1].ToString('x2'))`t$($bytes[2].ToString('x2'))`t$next`t$($graphic.Groups['offset'].Value)`t$($parts -join ';')`tdata/ages/specialObjectAnimationData.s:$label;data/ages/specialObjectOamData.s:$oamLabel")
        $index++
    }
}
Write-GeneratedTable((Join-Path $destination 'metadata/link_squish_frames.tsv'),$rows)
$link = Read-ImportText (Join-Path $Disassembly 'object_code/common/specialObjects/link.s')
if ($link -notmatch '(?s)linkState11:.*?@substate1:\s+call specialObjectAnimate.*?call itemIncSubstate\s+ld l,SpecialObject.counter1\s+ld \(hl\),\$(?<flicker>[0-9a-f]{2})\s+@substate2:\s+call specialObjectAnimate.*?call itemDecCounter1\s+ret nz') {
    throw 'Link squish lost terminal-frame fallthrough or visible-frame counter.'
}
Write-GeneratedTable((Join-Path $destination 'metadata/link_squish_control.tsv'),@(
    "# flicker-count`tsource",
    "$($Matches['flicker'])`tobject_code/common/specialObjects/link.s:linkState11"
))
}
