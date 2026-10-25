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
