function ConvertTo-NpcProgramRows {
    param([string[]]$sourceRows, [string]$actor, [hashtable]$texts, [hashtable]$animations = @{}, [hashtable]$symbols = @{})
    $programs = [ordered]@{}
    foreach ($row in $sourceRows | Select-Object -Skip 1) {
        $fields = $row.Split([char]9)
        $name = $fields[0]
        if (-not $programs.Contains($name)) { $programs[$name] = [Collections.Generic.List[object]]::new() }
        $programs[$name].Add([pscustomobject]@{ Path=$name; Script=$name; Label=$fields[1]; Line=[int]$fields[3]
            Opcode=$fields[4]; Operands=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($fields[5])); JumpTargets=$fields[6] })
    }
    $result = [Collections.Generic.List[string]]::new()
    $result.Add($cutsceneCommandHeader)
    foreach ($program in $programs.Values) {
        $expanded = [Collections.Generic.List[object]]::new()
        foreach ($command in $program) {
            if ($command.Opcode -eq 'rungenericnpclowindex') {
                # Both native macros retain the check/show/jump loop after collision initialization.
                $loop = '__generic_' + $expanded.Count
                foreach ($entry in @(@('initcollisions','',$command.Label), @('checkabutton','',$loop),
                    @('showtextlowindex',$command.Operands,$loop), @('scriptjump',$loop,$loop))) {
                    $expanded.Add([pscustomobject]@{ Path=$command.Path; Script=$command.Script; Label=$entry[2]
                        Line=$command.Line; Index=$expanded.Count; Opcode=$entry[0]; Operands=$entry[1]; JumpTargets='' })
                }
            } else {
                $command | Add-Member Index $expanded.Count
                $expanded.Add($command)
            }
        }
        $targets = @{}
        foreach ($c in $expanded) { if (-not $targets.ContainsKey($c.Label)) { $targets[$c.Label]=$c.Index } }
        $bindings = @{}
        foreach ($c in $expanded) {
            $parts = @($c.Operands -split ',\s*')
            $binding = $null
            switch ($c.Opcode) {
                'setdisabledobjectsto91' { $binding=@{Opcode='setdisabledobjects';Arg0='91'} }
                'enableallobjects' { $binding=@{Opcode='setdisabledobjects';Arg0='00'} }
                { $_ -in @('jumpifmemoryeq','jumpifobjectbyteeq','jumpifglobalflagset') } {
                    $value = if ($c.Opcode -eq 'jumpifglobalflagset') { '01' } else { $parts[1].TrimStart('$') }
                    $target = $parts[-1]
                    if (-not $targets.ContainsKey($target)) { throw "$($c.Path):$($c.Line): unresolved target $target." }
                    $binding=@{Opcode='jumpifmemoryeq';Arg0=$value;Arg1=[string]$targets[$target];Payload=$parts[0]}
                }
                { $_ -in @('jumptable_memoryaddress','jumptable_objectbyte') } {
                    $indices = foreach ($target in $c.JumpTargets.Split(',')) {
                        if (-not $targets.ContainsKey($target)) { throw "$($c.Path):$($c.Line): unresolved table target $target." }
                        $targets[$target]
                    }
                    $binding=@{Opcode='jumptablememory';Payload=($parts[0]+'|'+($indices -join ','))}
                }
                { $_ -in @('asm15','writeobjectbyte','getrandombits','addobjectbyte') } {
                    $payload = if ($actor -eq 'Family') { $c.Opcode+' '+$c.Operands } else { $c.Operands }
                    $binding=@{Opcode='native';Payload=$payload}
                }
                'askforsecret' {
                    if ($c.Operands -ne '$ff') { throw "$($c.Path):$($c.Line): expected askforsecret `$ff." }
                    $binding=@{Opcode='nativeyield';Payload='AskAnySecret'}
                }
                'checkcfc0bit' {
                    if ($c.Operands -ne '1') { throw "$($c.Path):$($c.Line): unsupported cfc0 bit." }
                    $binding=@{Opcode='checkmemoryeq';Arg0='01';Payload='cfc0bit1'}
                }
                'xorcfc0bit' {
                    if ($c.Operands -ne '1') { throw "$($c.Path):$($c.Line): unsupported cfc0 bit." }
                    $binding=@{Opcode='native';Payload='ClearCfc0Bit1'}
                }
            }
            if ($null -ne $binding) { $bindings[$c.Index.ToString()+'|'+$c.Opcode+'|'+$c.Operands]=$binding }
        }
        $positions = @{}
        if ($actor -eq 'Farore') { foreach ($id in $texts.Keys) { $positions[$id]=2 } }
        $rows = @(ConvertTo-CutsceneCommandRows @($expanded) $actor -texts $texts -animations $animations `
            -symbols $symbols -bindings $bindings -positions $positions)
        foreach ($row in $rows | Select-Object -Skip 1) { $result.Add($row) }
    }
    return $result
}

function ConvertTo-CutsceneCommandRows {
    param(
        [object[]]$commands,
        [string]$actor,
        [hashtable]$symbols = @{},
        [hashtable]$texts = @{},
        [hashtable]$animations = @{},
        [hashtable]$bindings = @{},
        [hashtable]$positions = @{},
        [bool]$yieldOnJump = $false,
        [hashtable]$externalTargets = @{})
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
            if ($externalTargets.ContainsKey($operand)) { return $externalTargets[$operand].ToString() }
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
        $instructionKey = "$($command.Index)|$bindingKey"
        if ($bindings.ContainsKey($instructionKey)) { $bindingKey = $instructionKey }
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
                { $_ -in @('disableinput','enableinput','scriptend','showloadedtext') } { & $arity 0 }
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
                { $_ -in @('showtext','showtextlowindex','settextid') } {
                    & $arity 1
                    if ($operands[0] -notmatch '^<?TX_([0-9a-fA-F]{4})$') { throw "$where malformed text operand." }
                    $id = [Convert]::ToInt32($Matches[1],16)
                    if (-not $texts.ContainsKey($id)) { throw "$where unbound text TX_$($id.ToString('x4'))." }
                    $arg0 = $id.ToString('x4'); $payload = $texts[$id]
                    if ($opcode -eq 'settextid') { $commandActor = $actor }
                    else {
                        $opcode = 'showtext'
                        if ($positions.ContainsKey($id)) { $arg1 = $positions[$id].ToString() }
                    }
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
                    # scriptCmd_jump relocates copied-buffer local targets and
                    # returns without carry. A jump out to ROM still continues.
                    # Conditional branches use scriptFunc_jump_scf instead.
                    if ($yieldOnJump -and -not $externalTargets.ContainsKey($operands[0])) { $opcode = 'scriptjumpyield' }
                }
                'callscript' { & $arity 1; $arg0 = & $target $operands[0] }
                'retscript' { & $arity 0; $opcode = 'return' }
                { $_ -in @('moveup','moveright','movedown','moveleft') } {
                    & $arity 1; $commandActor = $actor
                    $direction = @('moveup','moveright','movedown','moveleft').IndexOf($opcode)
                    if (-not $animations.ContainsKey($direction)) { throw "$where unbound movement animation $direction." }
                    $opcode = 'move'; $arg0 = ($direction * 8).ToString('x2')
                    $arg1 = (& $number $operands[0]).ToString('x2'); $payload = $animations[$direction]
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
