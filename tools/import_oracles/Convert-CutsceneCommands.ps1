function ConvertTo-CutsceneCommandRows {
    param(
        [object[]]$commands,
        [string]$actor,
        [hashtable]$symbols = @{},
        [hashtable]$texts = @{},
        [hashtable]$animations = @{},
        [hashtable]$bindings = @{},
        [hashtable]$positions = @{},
        [bool]$yieldOnJump = $false)
    $targets = @{}
    foreach ($command in $commands) {
        if (-not $targets.ContainsKey($command.Label)) {
            $targets[$command.Label] = $command.Index
        }
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add($cutsceneCommandHeader)
    foreach ($command in $commands) {
        $where = "$($command.Path):$($command.Line): $($command.Script)/$($command.Label)"
        $operands = @()
        if (-not [string]::IsNullOrWhiteSpace($command.Operands)) {
            $operands = @($command.Operands.Split(',') | ForEach-Object { $_.Trim() })
        }
        $number = {
            param([string]$operand)
            if ($symbols.ContainsKey($operand)) { return [int]$symbols[$operand] }
            if ($operand -match '^\$([0-9a-fA-F]+)$') { return [Convert]::ToInt32($Matches[1], 16) }
            if ($operand -match '^[0-9]+$') { return [int]$operand }
            throw "$where unresolved scalar '$operand'."
        }
        $target = {
            param([string]$operand)
            if (-not $targets.ContainsKey($operand)) { throw "$where unresolved branch '$operand'." }
            return $targets[$operand].ToString()
        }
        $arity = {
            param([int]$expected)
            if ($operands.Count -ne $expected) {
                throw "$where $($command.Opcode) expects $expected operands, got $($operands.Count)."
            }
        }
        $opcode = $command.Opcode
        $commandActor = ''; $arg0 = ''; $arg1 = ''; $payload = ''
        $bindingKey = "$opcode|$($command.Operands)"
        if ($bindings.ContainsKey($bindingKey)) {
            $binding = $bindings[$bindingKey]
            foreach ($key in $binding.Keys) {
                if ($key -notin @('Opcode', 'Actor', 'Arg0', 'Arg1', 'Payload')) {
                    throw "$where unknown native binding field '$key'."
                }
            }
            $opcode = $binding.Opcode
            $commandActor = $binding.Actor
            $arg0 = $binding.Arg0; $arg1 = $binding.Arg1; $payload = $binding.Payload
        } else {
            switch ($opcode) {
                { $_ -in @('disableinput','enableinput','scriptend') } { & $arity 0 }
                { $_ -in @('initcollisions','makeabuttonsensitive','checkabutton') } {
                    & $arity 0; $commandActor = $actor
                }
                'setcollisionradii' {
                    & $arity 2; $commandActor = $actor
                    $arg0 = (& $number $operands[0]).ToString('x2')
                    $arg1 = (& $number $operands[1]).ToString('x2')
                }
                'setanimation' {
                    & $arity 1; $commandActor = $actor
                    $value = & $number $operands[0]
                    if (-not $animations.ContainsKey($value)) { throw "$where unbound animation $value." }
                    $arg0 = $value.ToString('x2'); $payload = $animations[$value]
                }
                { $_ -in @('showtext','showtextlowindex') } {
                    & $arity 1
                    if ($operands[0] -notmatch '^<?TX_([0-9a-fA-F]{4})$') { throw "$where malformed text operand." }
                    $id = [Convert]::ToInt32($Matches[1],16)
                    if (-not $texts.ContainsKey($id)) { throw "$where unbound text TX_$($id.ToString('x4'))." }
                    $opcode = 'showtext'; $arg0 = $id.ToString('x4'); $payload = $texts[$id]
                    if ($positions.ContainsKey($id)) { $arg1 = $positions[$id].ToString() }
                }
                'wait' { & $arity 1; $arg0 = (& $number $operands[0]).ToString() }
                { $_ -in @('setspeed','setangle','applyspeed') } {
                    & $arity 1; $commandActor = $actor
                    $arg0 = (& $number $operands[0]).ToString('x2')
                }
                'setcoords' {
                    & $arity 2; $commandActor = $actor
                    $arg0 = (& $number $operands[0]).ToString('x2')
                    $arg1 = (& $number $operands[1]).ToString('x2')
                }
                { $_ -in @('jumpifroomflagset','jumpiftradeitemeq','jumpiftextoptioneq') } {
                    & $arity 2
                    $arg0 = (& $number $operands[0]).ToString('x2'); $arg1 = & $target $operands[1]
                }
                'scriptjump' {
                    & $arity 1; $arg0 = & $target $operands[0]
                    if ($yieldOnJump) { $opcode = 'scriptjumpyield' }
                }
                { $_ -in @('giveitem','writeobjectbyte') } {
                    & $arity 2
                    if ($opcode -eq 'writeobjectbyte') { $commandActor = $actor }
                    $arg0 = (& $number $operands[0]).ToString('x2')
                    $arg1 = (& $number $operands[1]).ToString('x2')
                }
                { $_ -in @('checkmemoryeq','writememory','checkobjectbyteeq') } {
                    & $arity 2
                    if (-not $symbols.ContainsKey($operands[0])) { throw "$where unbound memory '$($operands[0])'." }
                    $payload = [string]$symbols[$operands[0]]
                    $arg0 = (& $number $operands[1]).ToString('x2')
                    if ($opcode -eq 'checkobjectbyteeq') { $opcode = 'checkmemoryeq' }
                }
                default { throw "$where unsupported command '$opcode $($command.Operands)'." }
            }
        }
        $rows.Add((New-CutsceneCommandRow $command.Script $command.Index $command.Label $command.Line $opcode $commandActor $arg0 $arg1 $payload))
    }
    return $rows
}
