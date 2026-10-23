# commonScripts.s:faroreScript is the shared Ages/Seasons script. Preserve its
# command stream and all local branches; native reward data comes from d9.s.
$scriptPath = Join-Path $Disassembly 'scripts\common\commonScripts.s'
$nativePath = Join-Path $Disassembly 'object_code\common\interactions\farore.s'
$nativeSource = Read-ImportText $nativePath
if ($nativeSource -notmatch '(?ms)^interactionCode10:.*?^@state0:.*?interactionInitGraphics.*?mainScripts\.faroreScript.*?GLOBALFLAG_SECRET_CHEST_WAITING.*?unsetGlobalFlag.*?TEXTBOXFLAG_DONTCHECKPOSITION.*?\$02.*?wTextboxPosition.*?objectSetVisible82.*?^@state1:.*?\$1406.*?objectSetCollideRadii.*?interactionRunScript.*?interactionAnimate') {
    throw "$nativePath`: Farore state-zero/state-one script, flag, textbox or collision contract changed."
}
$scriptOps = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($name in @('jumptable_memoryaddress','jumpifglobalflagset',
    'rungenericnpclowindex','initcollisions','enableinput','checkabutton',
    'setdisabledobjectsto91','showtextlowindex','jumpiftextoptioneq',
    'scriptjump','askforsecret','asm15','jumptable_objectbyte','wait',
    'disableinput','checkcfc0bit','xorcfc0bit')) { [void]$scriptOps.Add($name) }
$lines = @((Read-ImportText $scriptPath) -split '\r?\n')
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# script`tlabel`tindex`tsource-line`topcode`toperands-base64`tjump-targets")
foreach ($script in @('faroreUnlinked','faroreLinked')) {
    $end = if ($script -eq 'faroreUnlinked') { 'faroreLinked' } else { 'dropSmallKeyWhenNoEnemiesScript' }
    $commands = @(Read-AssemblyCutsceneCommands $scriptPath $script $scriptOps $end)
    if ($commands.Count -lt 15) { throw "$scriptPath`: $script unexpectedly short." }
    $labels = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($command in $commands) { [void]$labels.Add([string]$command.Label) }
    foreach ($command in $commands) {
    $operands = [string]$command.Operands.Trim()
    $targets = [Collections.Generic.List[string]]::new()
    if ($command.Opcode -in @('jumptable_memoryaddress','jumptable_objectbyte')) {
        for ($line = $command.Line; $line -lt $lines.Count; $line++) {
            $value = $lines[$line].Split(';')[0].Trim()
            if ([string]::IsNullOrWhiteSpace($value)) { continue }
            if ($value -notmatch '^\.dw\s+(\S+)$') { break }
            $targets.Add($Matches[1])
        }
        if ($targets.Count -eq 0) { throw "$scriptPath`:$($command.Line): missing Farore jump table." }
    }
    if ($command.Opcode -eq 'scriptjump') { $targets.Add($operands) }
    if ($command.Opcode -eq 'jumpifglobalflagset' -or
        $command.Opcode -eq 'jumpiftextoptioneq') {
        $targets.Add(($operands.Split(',')[-1]).Trim())
    }
    foreach ($target in $targets) {
        if (-not $labels.Contains($target)) {
            throw "$scriptPath`:$($command.Line): missing Farore target $target."
        }
    }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($operands))
    $rows.Add("$script`t$($command.Label)`t$($command.Index)`t$($command.Line)`t$($command.Opcode)`t$encoded`t$($targets -join ',')")
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes\farore_scripts.tsv'), $rows)

$texts = [Collections.Generic.List[string]]::new()
$texts.Add("# text-id`tutf8-base64")
foreach ($id in 0x5501..0x551c) {
    if (-not $allTexts.ContainsKey($id)) { throw "$scriptPath`: missing TX_$($id.ToString('x4'))." }
    $resolved = Resolve-OceanText $id ([Collections.Generic.HashSet[int]]::new())
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($resolved))
    $texts.Add("$($id.ToString('x4'))`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes\farore_texts.tsv'), $texts)

$rewardPath = Join-Path $Disassembly 'object_code\common\interactions\faroreGiveItem.s'
$rewardSource = Read-ImportText $rewardPath
if ($rewardSource -notmatch '(?ms)^@bits:\s*\r?\n\s*dbrev %10001101 %01000000' -or
    $rewardSource -notmatch '(?ms)^interactiond9_markSecretAsTold:.*?GLOBALFLAG_FIRST_AGES_DONE_SECRET.*?GLOBALFLAG_SECRET_CHEST_WAITING') {
    throw "$rewardPath`: Farore upgrade bits or completion flag contract changed."
}
$contents = [regex]::Match($rewardSource,
    '(?ms)^@chestContents:\s*\r?\n(?<body>.*?)(?=^\s*; State 1:)')
if (-not $contents.Success) { throw "$rewardPath`: missing chest contents table." }
$rewardRows = [Collections.Generic.List[string]]::new()
$rewardRows.Add("# index`ttreasure-symbol`tsubid`tkind")
foreach ($line in ($contents.Groups['body'].Value -split '\r?\n')) {
    $value = $line.Split(';')[0].Trim()
    if ($value -match '^\.db\s+(TREASURE_[A-Z0-9_]+),\s*\$([0-9a-fA-F]{2})') {
        $rewardRows.Add("$((($rewardRows.Count - 1)).ToString('x2'))`t$($Matches[1])`t$($Matches[2].ToLowerInvariant())`tupgrade")
    } elseif ($value -match '^dwbe\s+TREASURE_OBJECT_([A-Z0-9_]+)_([0-9a-fA-F]{2})') {
        $rewardRows.Add("$((($rewardRows.Count - 1)).ToString('x2'))`tTREASURE_$($Matches[1])`t$($Matches[2].ToLowerInvariant())`tchest")
    }
}
if ($rewardRows.Count -ne 11) { throw "$rewardPath`: expected 10 Farore rewards." }
Write-GeneratedTable((Join-Path $destination 'cutscenes\farore_rewards.tsv'), $rewardRows)
