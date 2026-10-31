# m_BeginTreasureSubids/m_TreasureSubid assign IDs by macro order, not label suffix.
function Read-TreasureObjectCatalog([string]$game, [byte[]]$bytes, $symbols, $texts) {
    $source = @(Read-ImportLines (Join-Path $Disassembly "data/$game/treasureObjectData.s"))
    $defines = if ($game -eq 'ages') { 'ROM_AGES;AGES_ENGINE' } else { 'ROM_SEASONS;SEASONS_ENGINE' }
    $hostConnection = Start-AssemblySourceHost "$defines;REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $constants = Join-Path $Disassembly 'constants/common/treasure.s'
        $enumNodes = (Invoke-AssemblySourceHost $hostConnection 'NODES' $constants | ConvertFrom-Json)
        $ids = @{}; $value = 0; $inside = $false
        foreach ($node in $enumNodes | Where-Object IsActive) {
            if ($node.Name -ieq '.enum') { $inside = $true; continue }
            if ($node.Name -ieq '.ende') { $inside = $false; continue }
            if (-not $inside -or $node.Kind -in @('Blank', 'Comment', 'Directive')) { continue }
            if ($node.Name -notmatch '^TREASURE_[A-Za-z0-9_]+$' -or $node.Operands.Count -ne 1 -or
                $node.Operands[0] -notin @('db', '.db')) { throw "$game treasure.s: unsupported enum row '$($node.Code)'." }
            $ids.Add($node.Name, $value)
            if ($node.Operands[0] -eq 'db') { $value++ }
        }
        if ($value -ne 0x68) { throw "$game treasure.s: incomplete treasure enum." }
        $path = Join-Path $Disassembly "data/$game/treasureObjectData.s"
        $nodes = (Invoke-AssemblySourceHost $hostConnection 'MACRO_INVOCATIONS' "$path`0`0" | ConvertFrom-Json)
        $activeSource = @($nodes | Where-Object IsActive | ForEach-Object RawText)
    } finally {
        [void](Invoke-AssemblySourceHost $hostConnection 'QUIT' '')
        $hostConnection.WaitForExit(); $hostConnection.Dispose()
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# treasure-object`ttreasure-id`tsubid`tparameter`ttext-id`tgraphic`tmessage-base64`tflags")
    $records = @{}; $treasure = -1; $subid = 0
    foreach ($line in $activeSource) {
        if ($line -match 'm_BeginTreasureSubids\s+(?<name>TREASURE_[A-Z0-9_]+)') {
            if (-not $ids.ContainsKey($Matches['name'])) { throw "$game treasureObjectData.s: unknown $($Matches['name'])." }
            $treasure = [int]$ids[$Matches['name']]; $subid = 0; continue
        }
        if ($line -match '/\*\s+\$(?<id>[0-9a-f]{2})\s+\*/') {
            $treasure = [Convert]::ToInt32($Matches['id'], 16); $subid = 0
        }
        if ($line -notmatch 'm_TreasureSubid\s+(?<flags>\$[0-9a-f]{2}),\s*(?<parameter>\$[0-9a-f]{2}),\s*(?<text><?[A-Za-z0-9_]+|\$[0-9a-f]{2}),\s*(?<graphic>\$[0-9a-f]{2}),\s*(?<label>TREASURE_OBJECT_[A-Za-z0-9_]+)') { continue }
        $label = $Matches['label']
        $flags = [Convert]::ToInt32($Matches['flags'].TrimStart('$'), 16)
        $parameter = [Convert]::ToInt32($Matches['parameter'].TrimStart('$'), 16)
        $text = [Convert]::ToInt32($Matches['text'].TrimStart('$'), 16)
        $graphic = [Convert]::ToInt32($Matches['graphic'].TrimStart('$'), 16)
        if ($treasure -lt 0 -or $flags -lt 0 -or $parameter -lt 0 -or $text -lt 0 -or $graphic -lt 0) {
            throw "$game treasureObjectData.s:${label}: unresolved native operands."
        }
        $at = [int]$symbols['treasureObjectData'] + 4 * $treasure
        if (($bytes[$at] -band 0x80) -ne 0) {
            $pointer = [BitConverter]::ToUInt16($bytes, $at + 1)
            if ($pointer -lt 0x4000 -or $pointer -ge 0x8000) { throw "$game ${label}: invalid treasure pointer." }
            $at = ($at -band -0x4000) + ($pointer -band 0x3fff) + 4 * $subid
        } elseif ($subid -ne 0) { throw "$game ${label}: inline treasure has multiple subids." }
        $expected = @($flags, $parameter, $text, $graphic)
        foreach ($index in 0..3) {
            if ($bytes[$at + $index] -ne $expected[$index]) { throw "$game ${label}: source macro order/operands differ from clean US ROM at byte $index." }
        }
        if ($text -ne 0xff -and -not $texts.ContainsKey($text)) { throw "$game ${label}: unresolved TX_00$($text.ToString('x2'))." }
        $message = if ($text -eq 0xff) { '' } else { $texts[$text] }
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message))
        $rows.Add("$label`t$($treasure.ToString('x2'))`t$($subid.ToString('x2'))`t$($parameter.ToString('x2'))`t$($text.ToString('x2'))`t$($graphic.ToString('x2'))`t$encoded`t$($flags.ToString('x2'))")
        if ($records.ContainsKey($label)) { throw "$game treasureObjectData.s: duplicate $label." }
        $records[$label] = @{ Treasure = $treasure; Subid = $subid; Parameter = $parameter; TextId = $text; Graphic = $graphic; Message = $message; Flags = $flags }
        $subid++
    }
    if (-not $records.ContainsKey('TREASURE_OBJECT_SWORD_00') -or $records.Count -lt 100) { throw "$game treasureObjectData.s: incomplete native catalog." }
    return @{ Rows = $rows; Records = $records; Source = $source }
}
