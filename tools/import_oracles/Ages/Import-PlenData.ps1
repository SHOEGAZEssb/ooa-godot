# Plen's helper script is copied to wBigBuffer. Local jumps yield when relocated;
# the expanded genericNpcScript loop is ROM-resident and keeps running.
& {
if ((Read-ImportText (Join-Path $Disassembly 'constants\common\secrets.s')) -notmatch '(?m)^\s*PLEN_SECRET\s+db ; \$03' -or
    (Read-ImportText (Join-Path $Disassembly 'constants\common\rings.s')) -notmatch '(?m)^\s*SPIN_RING\s+db ; \$2f' -or
    (Read-ImportText (Join-Path $Disassembly 'scripts\ages\scriptHelper.s')) -notmatch 'giveRingAToLink:\s+ld b,a\s+ld c,\$00\s+jp giveRingToLink') {
    throw 'Plen secret $03 / Spin Ring $2f / giveRingAToLink contract changed.'
}
$plenPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$plenNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\plen.s')
if ($plenNative -notmatch '(?s)@state0:\s+call @initialize\s+call interactionSetAlwaysUpdateBit\s+@state1:\s+call interactionRunScript\s+jp c,interactionDelete\s+jp interactionAnimateAsNpc' -or
    $plenNative -notmatch '(?s)@scriptTable:\s+\.dw mainScripts.plenSubid0Script' -or
    (Read-ImportText (Join-Path $Disassembly 'scripts\ages\scripts.s')) -notmatch 'plenSubid0Script:\s+loadscript scriptHelp.plenSubid0Script') {
    throw 'plen.s:$cc:$00 initialization/script dispatch changed.'
}
$plenNodes = [Collections.Generic.List[object]]::new()
$plenLabels = @{}
foreach ($node in @(Read-AssemblyLabelNodes $plenPath 'plenSubid0Script')) {
    if ($node.Kind -eq 'Label') { $plenLabels[$node.Name] = $plenNodes.Count }
    elseif ($node.Kind -in @('MacroInvocation','Instruction')) {
        $plenNodes.Add([pscustomobject]@{Node=$node; Expansion=0})
        if ($node.Name -eq 'rungenericnpc') {
            foreach ($i in 1..4) { $plenNodes.Add([pscustomobject]@{Node=$node; Expansion=$i}) }
        }
    } elseif ($node.Kind -notin @('Blank','Comment')) { throw "${plenPath}:$($node.Line): unsupported Plen node $($node.Code)" }
}
function Resolve-PlenTarget([string]$label) {
    if (!$plenLabels.ContainsKey($label)) { throw "Unresolved Plen target $label" }
    return $plenLabels[$label].ToString()
}
$plenRows = [Collections.Generic.List[string]]::new()
$plenRows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
foreach ($entry in $plenNodes) {
    $node=$entry.Node; $op=$node.Name.ToLowerInvariant(); $args=([string]$node.OperandText).Trim(); $parts=@($args -split ',\s*')
    $actor=''; $a=''; $b=''; $payload=''; $index=$plenRows.Count-1
    switch ($op) {
        'rungenericnpc' {
            switch ($entry.Expansion) {
                0 { $op='nativeyield'; $payload='LoadText:'+$args.Substring(3) }
                1 { $op='initcollisions'; $actor='Plen' }
                2 { $op='checkabutton'; $actor='Plen' }
                3 { $op='showtext'; $a=$args.Substring(3); $payload=[string]$allTexts[[Convert]::ToInt32($a,16)] }
                4 { $op='scriptjump'; $a=($index-2).ToString() }
            }
        }
        'initcollisions' { $actor='Plen' }
        'checkabutton' { $actor='Plen' }
        'jumpifglobalflagset' {
            if (!$globalFlagValues.ContainsKey($parts[0])) { throw "Unknown Plen flag $args" }
            $op='jumpifmemoryeq'; $a='01'; $b=Resolve-PlenTarget $parts[1]; $payload="Global:$($globalFlagValues[$parts[0]])"
        }
        'jumpiftextoptioneq' { $a=$parts[0].TrimStart('$'); $b=Resolve-PlenTarget $parts[1] }
        'jumpifmemoryeq' { $a=$parts[1].TrimStart('$'); $b=Resolve-PlenTarget $parts[2]; $payload=$parts[0] }
        'scriptjump' { $op='scriptjumpyield'; $a=Resolve-PlenTarget $args }
        'showtext' { $a=$args.Substring(3); $payload=[string]$allTexts[[Convert]::ToInt32($a,16)] }
        'wait' { if ($args -ne '30') { throw "Unexpected Plen wait $args" }; $a=$args }
        'setglobalflag' {
            if (!$globalFlagValues.ContainsKey($args)) { throw "Unknown Plen flag $args" }
            $a=([int]$globalFlagValues[$args]).ToString('x2')
        }
        'asm15' { if ($args -ne 'giveRingAToLink, SPIN_RING') { throw "Unknown Plen helper $args" }; $op='native'; $payload='GiveSpinRing' }
        'askforsecret' { if ($args -ne 'PLEN_SECRET') { throw "Unknown Plen secret $args" }; $op='nativeyield'; $payload='AskSecret' }
        'disableinput' { }
        'enableinput' { }
        default { throw "${plenPath}:$($node.Line): unsupported Plen opcode $op" }
    }
    if ($op -eq 'showtext' -and (!$payload -or $payload -match '\\(?:call|jump)\(')) { throw "Unresolved Plen TX_$a" }
    $plenRows.Add((New-CutsceneCommandRow 'plen' $index 'plenSubid0Script' $node.Line $op $actor $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\plen_commands.tsv'), $plenRows)
}
