function Find-CutsceneCommandSourceLine {
    param(
        [string]$source,
        [int]$bodyStart,
        [int]$bodyEnd,
        [string]$pattern,
        [string]$script,
        [int]$occurrence = 0)
    $path = Resolve-AssemblySourceTextPath $source
    if ($null -eq $path) { throw "$script uses untracked assembly source." }
    $line = if ($occurrence -lt 0) { -1 } else {
        Find-AssemblySourceLine $path $bodyStart $bodyEnd $pattern $occurrence
    }
    if ($line -lt 0) {
        throw "Could not locate $script command source occurrence $occurrence matching: $pattern"
    }
    return $line
}
function ConvertTo-CutsceneCommandPayload {
    param([string]$value)
    if ([string]::IsNullOrEmpty($value)) { return '' }
    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($value))
}
function New-CutsceneCommandRow {
    param(
        [string]$script,
        [int]$index,
        [string]$label,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return @(
        $script, $label, $index.ToString(), $line.ToString(),
        $opcode, $actor, $arg0, $arg1,
        (ConvertTo-CutsceneCommandPayload $payload)
    ) -join "`t"
}

$cutsceneCommandHeader =
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64"
$generatedCutsceneCommandStreams = [Collections.Generic.List[object]]::new()
$cutsceneScalarCache = [Collections.Generic.Dictionary[string,bool]]::new([StringComparer]::Ordinal)

function Write-CutsceneGeneratedTable([object[]]$arguments) {
    if ($arguments.Count -lt 2) {
        throw "Write-CutsceneGeneratedTable requires a destination path and rows; got " +
            "$($arguments.Count) argument(s) from " +
            "$($MyInvocation.ScriptName):$($MyInvocation.ScriptLineNumber)."
    }
    $path = [string]$arguments[0]
    $rows = [Collections.Generic.List[string]]::new()
    foreach ($value in $arguments[1..($arguments.Count - 1)]) {
        foreach ($row in $value) {
            $rows.Add([string]$row)
        }
    }
    if ($rows.Count -gt 0 -and $rows[0] -eq $cutsceneCommandHeader) {
        $generatedCutsceneCommandStreams.Add([pscustomobject]@{
            Path = $path
            Rows = @($rows)
        })
    }
    Write-GeneratedTable($path, $rows)
}

function Test-CutsceneSchemaScalar {
    param([string]$shape, [string]$value)
    $key = $shape + [char]0 + $value
    if (-not $cutsceneScalarCache.ContainsKey($key)) {
        $cutsceneScalarCache[$key] = (Invoke-AssemblySourceHost $assemblySourceHost 'CUTSCENE_SCALAR' $key) -eq '1'
    }
    return $cutsceneScalarCache[$key]
}

function Test-GeneratedCutsceneCommandStreams {
    param(
        [object[]]$tables,
        [hashtable]$schemas
    )

    $commandStreamCount = 0
    $commandRowCount = 0
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    foreach ($table in $tables) {
        $path = [string]$table.Path
        $lines = @($table.Rows)
        if ($lines.Count -eq 0 -or $lines[0] -ne $cutsceneCommandHeader) {
            throw "$path was registered as a cutscene command stream without " +
                'the normalized command header.'
        }
        $commandStreamCount++
        for ($lineIndex = 1; $lineIndex -lt $lines.Count; $lineIndex++) {
            if ([string]::IsNullOrWhiteSpace($lines[$lineIndex])) {
                continue
            }
            $columns = $lines[$lineIndex].Split([char]"`t")
            if ($columns.Count -ne 9) {
                throw "$($path):$($lineIndex + 1): cutscene command row " +
                    "has $($columns.Count) columns instead of 9."
            }
            $opcode = $columns[4]
            if (-not $schemas.ContainsKey($opcode)) {
                throw "$($path):$($lineIndex + 1): emitted cutscene " +
                    "opcode '$opcode' has no command schema entry."
            }
            try {
                $payload = $utf8.GetString(
                    [Convert]::FromBase64String($columns[8]))
            }
            catch {
                throw "$($path):$($lineIndex + 1): emitted cutscene " +
                    "opcode '$opcode' has invalid UTF-8 base64 payload: $_"
            }
            $schema = $schemas[$opcode]
            $fields = @(
                @('actor', $schema.ActorShape, $columns[5]),
                @('arg0', $schema.Arg0Shape, $columns[6]),
                @('arg1', $schema.Arg1Shape, $columns[7]),
                @('payload', $schema.PayloadShape, $payload)
            )
            foreach ($field in $fields) {
                if (-not (Test-CutsceneSchemaScalar $field[1] $field[2])) {
                    $shown = if ($field[2].Length -eq 0) {
                        '<empty>'
                    } else {
                        $field[2].Replace(([char]0).ToString(), '\0')
                    }
                    throw "$($path):$($lineIndex + 1): emitted opcode " +
                        "'$opcode' field '$($field[0])' has '$shown'; expected " +
                        "schema shape '$($field[1])'."
                }
            }
            $commandRowCount++
        }
    }
    if ($commandStreamCount -eq 0 -or $commandRowCount -eq 0) {
        throw 'No in-memory cutscene command streams were available for schema validation.'
    }
}

function Read-AssemblyNpcProgramRows {
    param([string[]]$paths,[string[]]$scripts,[hashtable]$takenBranches=@{})
    $operations=[Collections.Generic.List[object]]::new();$labels=@{};$anonymous=@{}
    foreach($path in $paths){foreach($node in Read-AssemblyNodes $path){
        if($node.Kind -eq 'Label'){
            $name=$node.Name
            if($name -match '^[+-]+$'){
                $key=$node.Path+'|'+$name
                if(!$anonymous.ContainsKey($key)){$anonymous[$key]=[Collections.Generic.List[int]]::new()}
                $anonymous[$key].Add($operations.Count)
            }else{
                if($name.StartsWith('@')){$name=$node.EnclosingGlobalLabel+$name}
                $labels[$name]=$operations.Count
            }
        }elseif($node.Kind -in @('MacroInvocation','Instruction','Data')){$operations.Add($node)}
    }}
    function Resolve-ProgramTarget([string]$name,[object]$node,[int]$index){
        $name=$name.Replace('mainScripts.','')
        if($name.StartsWith('@')){$name=$node.EnclosingGlobalLabel+$name}
        if($name -match '^[+-]+$'){
            $found=@($anonymous[$node.Path+'|'+$name] | Where-Object {if($name[0] -eq '+'){$_ -gt $index}else{$_ -le $index}})
            if($found.Count -eq 0){throw "$($node.Path):$($node.Line): missing anonymous label $name."}
            return $(if($name[0] -eq '+'){$found[0]}else{$found[-1]})
        }
        if(!$labels.ContainsKey($name)){throw "$($node.Path):$($node.Line): missing script label $name."}
        return [int]$labels[$name]
    }
    $rows=[Collections.Generic.List[string]]::new()
    $rows.Add("# script`tlabel`tindex`tsource-line`topcode`toperands-base64`tjump-targets`tsource-path")
    foreach($script in @($scripts | Sort-Object)){
        if(!$labels.ContainsKey($script)){throw "Missing script entry $script."}
        $pending=[Collections.Generic.Queue[int]]::new();$pending.Enqueue($labels[$script])
        $reached=[Collections.Generic.HashSet[int]]::new();$graph=@{}
        while($pending.Count -gt 0){
            $index=$pending.Dequeue();if(!$reached.Add($index)){continue}
            if($index -ge $operations.Count){throw "${script}: unterminated script fallthrough."}
            $node=$operations[$index];$op=$node.Name.ToLowerInvariant();$parts=@($node.Operands);$targets=@();$next=$index+1;$fallthrough=-1
            if($op -eq 'stopifroomflag80set'){$op='jumpifroomflagset';$parts=@('$80','stubScript')}
            $taken=$takenBranches.ContainsKey($script) -and $takenBranches[$script] -eq ($op+' '+$parts[0])
            if($op -in @('scriptjump','callscript') -or $op.StartsWith('jumpif')){
                $targets+=Resolve-ProgramTarget $parts[-1] $node $index
                $parts[-1]='node'+$targets[-1]
            }
            if($op.StartsWith('jumptable_')){
                while($next -lt $operations.Count -and $operations[$next].Name -eq '.dw'){
                    foreach($target in $operations[$next].Operands){$targets+=Resolve-ProgramTarget $target $node $index};$next++
                }
                if($targets.Count -eq 0){throw "$($node.Path):$($node.Line): empty jump table in $script."}
            }elseif($op -notin @('scriptjump','rungenericnpc','rungenericnpclowindex','scriptend','retscript') -and !$taken){
                $targets+=$next;$fallthrough=$next
            }
            foreach($target in $targets){$pending.Enqueue($target)}
            $graph[$index]=[pscustomobject]@{Node=$node;Op=$op;Operands=($parts -join ', ');Targets=$targets;Fallthrough=$fallthrough}
        }
        $order=@($reached | Where-Object {$_ -ge $labels[$script]} | Sort-Object)+@($reached | Where-Object {$_ -lt $labels[$script]} | Sort-Object)
        $rowIndex=0
        for($at=0;$at -lt $order.Count;$at++){
            $index=$order[$at];$entry=$graph[$index]
            $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($entry.Operands))
            $targets=(@($entry.Targets | ForEach-Object {'node'+$_}) -join ',')
            $rows.Add("$script`tnode$index`t$rowIndex`t$($entry.Node.Line)`t$($entry.Op)`t$encoded`t$targets`t$($entry.Node.Path)")
            $rowIndex++
            $nextOutput=if($at+1 -lt $order.Count){$order[$at+1]}else{-1}
            if($entry.Fallthrough -ge 0 -and $entry.Fallthrough -ne $nextOutput){
                # A reset before the entry point can fall through across the emitted boundary without yielding.
                $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('node'+$entry.Fallthrough))
                $rows.Add("$script`t__fallthrough$index`t$rowIndex`t$($entry.Node.Line)`tscriptjump`t$encoded`t`t$($entry.Node.Path)")
                $rowIndex++
            }
        }
    }
    return $rows
}

function Read-AssemblyCutsceneCommands {
    param(
        [string]$path,
        [string]$script,
        [Collections.Generic.HashSet[string]]$supportedOpcodes,
        [string]$endLabel = '')

    $label = $script
    $commands = [Collections.Generic.List[object]]::new()
    $nodes = if ([string]::IsNullOrEmpty($endLabel)) {
        @(Read-AssemblyLabelNodes $path $script)
    } else {
        $start = @(Read-AssemblyLabels $path $script)
        $end = @(Read-AssemblyLabels $path $endLabel)
        if ($start.Count -ne 1 -or $end.Count -ne 1 -or
            $end[0].Offset -le $start[0].Offset) {
            throw "$path`: invalid $script -> $endLabel command range."
        }
        @(Read-AssemblyNodeRange $path ($start[0].Offset + 1) $end[0].Offset)
    }
    foreach ($node in $nodes) {
        if ($node.Kind -eq 'Label') {
            $label = $node.Name
            continue
        }
        if ($node.Kind -in @(
            'Blank', 'Comment', 'Constant', 'Data', 'Directive')) {
            continue
        }
        if ($node.Kind -notin @('MacroInvocation', 'Instruction')) {
            throw "$($node.Path):$($node.Line):$($node.Column): " +
                "malformed $script assembly node '$($node.Code)'."
        }
        $opcode = $node.Name.ToLowerInvariant()
        if (-not $supportedOpcodes.Contains($opcode)) {
            throw "$($node.Path):$($node.Line):$($node.Column): " +
                "unsupported $script opcode '$opcode' at label '$label'."
        }
        $commands.Add([pscustomobject]@{
            Path = $node.Path
            Script = $script
            Label = $label
            Index = $commands.Count
            Line = $node.Line
            Opcode = $opcode
            Operands = $node.OperandText
        })
    }
    if ($commands.Count -eq 0) {
        throw "$path`: $script contains no commands."
    }
    return $commands
}

function Get-AssemblySourceLine {
    param([string]$source, [string]$pattern, [string]$description)
    $path = Resolve-AssemblySourceTextPath $source
    $line = Find-AssemblySourceLine $path 0 $source.Length $pattern
    if ($line -lt 0) {
        throw "Could not locate $description source label."
    }
    return $line
}
