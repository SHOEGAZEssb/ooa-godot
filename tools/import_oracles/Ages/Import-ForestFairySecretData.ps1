& {
$native = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\forestFairy.s')
$objects = Read-ImportText (Join-Path $Disassembly 'objects\ages\mainData.s')
$secrets = Read-ImportText (Join-Path $Disassembly 'constants\common\secrets.s')
$flags = Read-ImportText (Join-Path $Disassembly 'constants\common\globalFlags.s')
$treasures = Read-ImportText (Join-Path $Disassembly 'data\ages\treasureObjectData.s')
if ($objects -notmatch '(?ms)^group0Map70ObjectData:\s+obj_Interaction \$49 \$0b \$48 \$48\s+obj_End' -or
    $native -notmatch '(?ms)^forestFairy_subid0b:\s+call checkInteractionState\s+jr nz,forestFairy_standardUpdate\s+ld a,GLOBALFLAG_FINISHEDGAME\s+call checkGlobalFlag\s+jp z,interactionDelete\s+call interactionInitGraphics\s+call objectMarkSolidPosition\s+call interactionIncState\s+ld l,Interaction.zh\s+ld \(hl\),\$fc.*?ld hl,mainScripts.forestFairyScript_heartContainerSecret\s+call interactionSetScript\s+jp objectSetVisiblec1' -or
    $native -notmatch '(?ms)^forestFairy_standardUpdate:\s+call interactionRunScript\s+call interactionAnimate\s+jp objectPreventLinkFromPassing' -or
    $secrets -notmatch 'FAIRY_SECRET\s+db ; \$01' -or
    $secrets -notmatch 'FAIRY_RETURN_SECRET\s+db ; \$11' -or
    $flags -notmatch 'GLOBALFLAG_BEGAN_FAIRY_SECRET\s+db ; \$65' -or
    $flags -notmatch 'GLOBALFLAG_DONE_FAIRY_SECRET\s+db ; \$6f' -or
    $treasures -notmatch '(?ms)^treasureObjectData2a:.*?m_TreasureSubid \$02, \$04, \$16, \$3b, TREASURE_OBJECT_HEART_CONTAINER_02') {
    throw 'Room 0:70 forest fairy $49:$0b source placement, state-zero gate, secret, or reward changed.'
}
$path = Join-Path $Disassembly 'scripts\ages\scripts.s'
$allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($op in @('setcollisionradii','makeabuttonsensitive','checkabutton','disableinput',
    'jumpifglobalflagset','showtext','wait','jumpiftextoptioneq','scriptjump',
    'askforsecret','jumpifmemoryeq','setglobalflag','giveitem','generatesecret','enableinput')) {
    [void]$allowed.Add($op)
}
$commands = @(Read-AssemblyCutsceneCommands $path 'forestFairyScript_heartContainerSecret' $allowed)
$targets = @{}
foreach ($c in $commands) { if (!$targets.ContainsKey($c.Label)) { $targets[$c.Label] = $c.Index } }
$rows = [Collections.Generic.List[string]]::new()
$rows.Add($cutsceneCommandHeader)
foreach ($c in $commands) {
    $op = $c.Opcode; $args = $c.Operands; $parts = @($args -split ',\s*')
    $actor = ''; $a = ''; $b = ''; $payload = ''
    switch ($op) {
        { $_ -in @('setcollisionradii','checkabutton','makeabuttonsensitive') } {
            $actor = 'ForestFairy'
            if ($op -eq 'setcollisionradii') {
                if ($args -ne '$04, $04') { throw "Room 0:70 fairy collision radii changed: $args" }
                $a = '04'; $b = '04'
            }
        }
        'jumpifglobalflagset' {
            if ($parts[0] -notin @('GLOBALFLAG_DONE_FAIRY_SECRET')) { throw "Unexpected fairy flag $args" }
            $op = 'jumpifmemoryeq'; $a = '01'; $b = [string]$targets[$parts[1]]
            $payload = 'Global:111'
        }
        'showtext' {
            if ($args -notmatch '^TX_114[89abcd]$') { throw "Unexpected fairy text $args" }
            $a = $args.Substring(3)
            $id = [Convert]::ToInt32($a, 16)
            $payload = $allTexts[$id]
            if (!$payload -or $payload -match '\\(?:call|jump)\(') { throw "Unresolved fairy TX_$a" }
            if ($allTextPositions.ContainsKey($id)) { $b = $allTextPositions[$id].ToString() }
        }
        'wait' { if ($args -ne '30') { throw "Unexpected fairy wait $args" }; $a = $args }
        'jumpiftextoptioneq' { $a = $parts[0].TrimStart('$'); $b = [string]$targets[$parts[1]] }
        'scriptjump' { $op = 'scriptjumpyield'; $a = [string]$targets[$args] }
        'askforsecret' { if ($args -ne 'FAIRY_SECRET') { throw "Unexpected fairy secret $args" }; $op = 'nativeyield'; $payload = 'AskSecret' }
        'jumpifmemoryeq' { if ($args -ne 'wTextInputResult, $00, @gaveValidSecret') { throw "Unexpected fairy secret result $args" }; $a = '00'; $b = [string]$targets['@gaveValidSecret']; $payload = 'SecretResult' }
        'setglobalflag' {
            if ($args -notin @('GLOBALFLAG_BEGAN_FAIRY_SECRET','GLOBALFLAG_DONE_FAIRY_SECRET')) { throw "Unexpected fairy flag $args" }
            $a = ([int]$globalFlagValues[$args]).ToString('x2')
        }
        'giveitem' { if ($args -ne 'TREASURE_HEART_CONTAINER, $02') { throw "Unexpected fairy reward $args" }; $a = '2a'; $b = '02' }
        'generatesecret' { if ($args -ne 'FAIRY_RETURN_SECRET') { throw "Unexpected fairy return secret $args" }; $op = 'native'; $payload = 'GenerateSecret' }
        { $_ -in @('disableinput','enableinput') } { }
        default { throw "${path}:$($c.Line): unsupported fairy opcode $op" }
    }
    $rows.Add((New-CutsceneCommandRow $c.Script $c.Index $c.Label $c.Line $op $actor $a $b $payload))
}
if ($commands.Count -ne 28) { throw "Room 0:70 expected 28 fairy commands, got $($commands.Count)." }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\forest_fairy_secret_commands.tsv'), $rows)
}
