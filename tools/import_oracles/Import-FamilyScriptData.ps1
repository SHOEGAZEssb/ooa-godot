# The family spawner's rows select an actor and a stage, while these source
# streams own its talk loop. Keep source operands and jump-table targets in an
# explicit imported program so an unknown command cannot disappear silently.
$familySourcePath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$familySourceLines = @((Read-ImportText $familySourcePath) -split '\r?\n')
$familyOpcodes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($name in @(
    'addobjectbyte', 'asm15', 'callscript', 'checkabutton', 'disableinput',
    'enableallobjects', 'enableinput', 'getrandombits', 'giveitem',
    'initcollisions', 'jumpifmemoryeq', 'jumpifobjectbyteeq',
    'jumptable_memoryaddress', 'jumptable_objectbyte',
    'makeabuttonsensitive', 'retscript', 'rungenericnpclowindex',
    'scriptjump', 'setanimation', 'setcollisionradii',
    'setdisabledobjectsto91', 'showloadedtext', 'showtext',
    'showtextlowindex', 'wait', 'writeobjectbyte')) {
    [void]$familyOpcodes.Add($name)
}
$familyNames = [Collections.Generic.List[string]]::new()
foreach ($name in @('bipinScript0', 'bipinScript1', 'bipinScript2')) { $familyNames.Add($name) }
foreach ($stage in 1..9) { $familyNames.Add("blossomScript$stage") }
foreach ($stage in 4..6) {
    foreach ($kind in @('hyperactive', 'shy', 'curious')) {
        $familyNames.Add("childScript_stage$($stage)_$kind")
    }
}
foreach ($stage in 7..9) {
    foreach ($kind in @('slacker', 'warrior', 'arborist', 'singer')) {
        $familyNames.Add("childScript_stage$($stage)_$kind")
    }
}
$familyRows = [Collections.Generic.List[string]]::new()
$familyRows.Add("# script`tlabel`tindex`tsource-line`topcode`toperands-base64`tjump-targets")
$referencedTexts = [Collections.Generic.HashSet[int]]::new()
foreach ($script in $familyNames) {
    $commands = if ($script -in @('blossomScript4', 'blossomScript5')) {
        $labelLine = -1
        for ($line = 0; $line -lt $familySourceLines.Count; $line++) {
            if ($familySourceLines[$line].Trim() -eq "$($script):") { $labelLine = $line; break }
        }
        if ($labelLine -lt 0 -or $labelLine + 1 -ge $familySourceLines.Count -or
            $familySourceLines[$labelLine + 1].Trim() -notmatch '^rungenericnpclowindex <(TX_[0-9a-f]{4})$') {
            throw "$familySourcePath`: $script generic talk opcode changed."
        }
        @([pscustomobject]@{ Label = $script; Line = $labelLine + 2;
                Opcode = 'rungenericnpclowindex'; Operands = "<$($Matches[1])" })
    } elseif ($script -eq 'blossomScript2') {
        @(Read-AssemblyCutsceneCommands $familySourcePath $script $familyOpcodes 'blossomScript3')
    } else {
        @(Read-AssemblyCutsceneCommands $familySourcePath $script $familyOpcodes)
    }
    # PowerShell unwraps a single object returned by an if-expression. Keep
    # one-opcode generic scripts as arrays so their Count is one, not null.
    $commands = @($commands)
    if ($script -eq 'bipinScript1') {
        $helper = @(Read-AssemblyCutsceneCommands $familySourcePath 'bipinSayRandomTip' $familyOpcodes)
        $commands = @($commands) + @($helper)
    }
    $labels = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($command in $commands) { [void]$labels.Add([string]$command.Label) }
    for ($index = 0; $index -lt $commands.Count; $index++) {
        $command = $commands[$index]
        $operands = [string]$command.Operands.Trim()
        $targets = [Collections.Generic.List[string]]::new()
        if ($command.Opcode -in @('jumptable_memoryaddress', 'jumptable_objectbyte')) {
            for ($sourceLine = $command.Line; $sourceLine -lt $familySourceLines.Count; $sourceLine++) {
                $line = $familySourceLines[$sourceLine].Split(';')[0].Trim()
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                if ($line -notmatch '^\.dw\s+(\S+)$') { break }
                $targets.Add($Matches[1])
            }
            if ($targets.Count -eq 0) {
                throw "$familySourcePath`:$($command.Line): $script jump table has no .dw entries."
            }
        }
        if ($command.Opcode -in @('scriptjump', 'callscript')) {
            if (-not $labels.Contains($operands)) {
                throw "$familySourcePath`:$($command.Line): $script unresolved branch $operands."
            }
        }
        if ($command.Opcode -in @('jumpifobjectbyteeq', 'jumpifmemoryeq')) {
            $parts = @($operands.Split(',') | ForEach-Object { $_.Trim() })
            if ($parts.Count -ne 3 -or -not $labels.Contains($parts[2])) {
                throw "$familySourcePath`:$($command.Line): $script unresolved conditional branch $operands."
            }
        }
        foreach ($target in $targets) {
            if (-not $labels.Contains($target)) {
                throw "$familySourcePath`:$($command.Line): $script unresolved table target $target."
            }
        }
        if ($operands -match '<?TX_([0-9a-fA-F]{4})') {
            [void]$referencedTexts.Add([Convert]::ToInt32($Matches[1], 16))
        }
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($operands))
        $familyRows.Add((@(
            $script, $command.Label, $index, $command.Line,
            $command.Opcode, $encoded, ($targets -join ',')
        ) -join "`t"))
    }
}
# Random-tip helpers change only the low text byte. The source-derived ranges
# must be imported even though no explicit showtext command names each ID.
foreach ($textId in (0x4300..0x4310) + (0x4805..0x480c)) {
    [void]$referencedTexts.Add($textId)
}
$familyTextRows = [Collections.Generic.List[string]]::new()
$familyTextRows.Add("# text-id`tutf8-base64")
foreach ($textId in @($referencedTexts | Sort-Object)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "$familySourcePath`: missing family TX_$($textId.ToString('x4'))."
    }
    $resolved = Resolve-OceanText $textId ([Collections.Generic.HashSet[int]]::new())
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($resolved))
    $familyTextRows.Add("$($textId.ToString('x4'))`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes\family_npc_scripts.tsv'), $familyRows)
Write-GeneratedTable((Join-Path $destination 'cutscenes\family_npc_texts.tsv'), $familyTextRows)
$animationRows = [Collections.Generic.List[string]]::new()
$animationRows.Add("# animation`tencoded-data")
foreach ($animation in @(2, 3)) {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(
        (Resolve-NpcAnimation 0x28 $animation)))
    $animationRows.Add("$($animation.ToString('x2'))`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes\family_npc_animations.tsv'), $animationRows)

# child.s supplies the moving child's animation bases, patrol waypoints and
# four-direction hyperactive turn cycle. Preserve their source order.
$childSourcePath = Join-Path $Disassembly 'object_code\common\interactions\child.s'
$childSource = Read-ImportText $childSourcePath
$childLines = @($childSource -split '\r?\n')
$motionRows = [Collections.Generic.List[string]]::new()
$motionRows.Add("# kind`tindex`ty`tx")
$baseMatch = [regex]::Match($childSource, '(?m)^@animations:\s*\r?\n\s*\.db (?<data>[^\r\n]+)')
if (-not $baseMatch.Success) { throw "$childSourcePath`: missing child animation bases." }
$bases = @([regex]::Matches($baseMatch.Groups['data'].Value, '\$[0-9a-fA-F]{2}') |
    ForEach-Object { [Convert]::ToInt32($_.Value.Substring(1), 16) })
if ($bases.Count -ne 8) { throw "$childSourcePath`: expected 8 child animation bases." }
for ($index = 0; $index -lt $bases.Count; $index++) {
    $motionRows.Add("base`t$index`t$($bases[$index].ToString('x2'))`t-")
}
$anglesMatch = [regex]::Match($childSource,
    '(?m)^childHyperactiveMovementAngles:\s*\r?\n\s*\.db (?<data>[^\r\n]+)')
if (-not $anglesMatch.Success) { throw "$childSourcePath`: missing hyperactive angles." }
$angles = @([regex]::Matches($anglesMatch.Groups['data'].Value, '\$[0-9a-fA-F]{2}') |
    ForEach-Object { [Convert]::ToInt32($_.Value.Substring(1), 16) })
if ($angles.Count -ne 4) { throw "$childSourcePath`: expected 4 hyperactive angles." }
for ($index = 0; $index -lt $angles.Count; $index++) {
    $motionRows.Add("angle`t$index`t$($angles[$index].ToString('x2'))`t-")
}
$expectedWaypointCounts = @(8, 4, 12, 2, 4)
for ($list = 0; $list -lt $expectedWaypointCounts.Count; $list++) {
    $listLine = -1
    for ($line = 0; $line -lt $childLines.Count; $line++) {
        if ($childLines[$line].Trim() -eq "@list$($list):") { $listLine = $line; break }
    }
    if ($listLine -lt 0) { throw "$childSourcePath`: missing child waypoint list $list." }
    $positions = [Collections.Generic.List[object]]::new()
    for ($line = $listLine + 1; $line -lt $childLines.Count; $line++) {
        $match = [regex]::Match($childLines[$line].Trim(),
            '^\.db \$(?<y>[0-9a-fA-F]{2}) \$(?<x>[0-9a-fA-F]{2})$')
        if (-not $match.Success) { break }
        $positions.Add($match)
    }
    if ($positions.Count -ne $expectedWaypointCounts[$list]) {
        throw "$childSourcePath`: child waypoint list $list changed length."
    }
    foreach ($position in $positions) {
        $motionRows.Add("waypoint$list`t$($motionRows.Count)`t$($position.Groups['y'].Value)`t$($position.Groups['x'].Value)")
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes\family_child_motion.tsv'), $motionRows)
$childAnimationRows = [Collections.Generic.List[string]]::new()
$childAnimationRows.Add("# animation`tencoded-data")
foreach ($animation in 0..0x18) {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(
        (Resolve-NpcAnimation 0x35 $animation)))
    $childAnimationRows.Add("$($animation.ToString('x2'))`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes\family_child_animations.tsv'), $childAnimationRows)

# The native state-zero tables select the script by subid or var03. Import the
# full table order, including childScript00 silent entries, rather than
# deriving script identity again from the saved growth stage.
$scriptMapRows = [Collections.Generic.List[string]]::new()
$scriptMapRows.Add("# id`tindex`tscript")
foreach ($entry in @(
    @(0x28, 'object_code\common\interactions\bipin.s', '@scriptTable:', 11),
    @(0x2b, 'object_code\common\interactions\blossom.s', '@scriptTable:', 10),
    @(0x35, 'object_code\common\interactions\child.s', 'childScriptTable:', 29))) {
    $actorSource = Read-ImportText (Join-Path $Disassembly $entry[1])
    $label = [string]$entry[2]
    $start = $actorSource.IndexOf($label, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Family actor script table $label missing from $($entry[1])." }
    $tail = $actorSource.Substring($start + $label.Length)
    $names = [Collections.Generic.List[string]]::new()
    foreach ($line in ($tail -split '\r?\n')) {
        if ($line -match '^\s*\.dw mainScripts\.(?<name>[A-Za-z0-9_]+)') {
            $names.Add($Matches['name'])
        } elseif ($names.Count -gt 0 -and $line -match '^\S[^;]*:$') {
            break
        }
    }
    if ($names.Count -ne [int]$entry[3]) {
        throw "Family actor $($entry[1]) $label expected $($entry[3]) scripts, got $($names.Count)."
    }
    for ($index = 0; $index -lt $names.Count; $index++) {
        $scriptMapRows.Add("$(([int]$entry[0]).ToString('x2'))`t$($index.ToString('x2'))`t$($names[$index])")
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes\family_actor_scripts.tsv'), $scriptMapRows)
