# Mamamu's helper runs from wBigBuffer; the indoor dog's script remains in ROM.
& {
$path = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$mamamuNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\mamamuYan.s')
if ($mamamuNative -notmatch '(?s)@state0:\s+call @initGraphicsLoadScriptAndIncState\s+@state1:\s+call interactionRunScript\s+jp c,interactionDelete\s+jp npcFaceLinkAndAnimate' -or
    $mamamuNative -notmatch '\.dw mainScripts.mamamuYanScript' -or
    (Read-ImportText (Join-Path $Disassembly 'objects\ages\mainData.s')) -notmatch '(?s)group2Mape7ObjectData:\s+obj_Interaction \$53 \$00 \$1a \$18\s+obj_Interaction \$54 \$00 \$38 \$50\s+obj_End' -or
    (Read-ImportText (Join-Path $Disassembly 'constants\common\secrets.s')) -notmatch 'MAMAMU_SECRET\s+db ; \$06' -or
    (Read-ImportText (Join-Path $Disassembly 'constants\common\rings.s')) -notmatch 'SNOWSHOE_RING\s+db ; \$21' -or
    (Read-ImportText (Join-Path $Disassembly 'constants\common\tradeItems.s')) -notmatch 'TRADEITEM_DOGGIE_MASK\s+db ; \$04') {
    throw 'Mamamu native dispatch, placement, secret, ring or trade constants changed.'
}
$allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($op in @('jumpifglobalflagset','scriptjump','jumpifroomflagset','initcollisions',
    'checkabutton','disableinput','showtextlowindex','wait','jumpiftradeitemeq',
    'enableinput','jumpiftextoptioneq','giveitem','setcoords','askforsecret',
    'jumpifmemoryeq','setglobalflag','orroomflag','asm15','rungenericnpclowindex')) { [void]$allowed.Add($op) }
$commands = @(Read-AssemblyCutsceneCommands $path 'mamamuYanScript' $allowed)
$expanded = [Collections.Generic.List[object]]::new()
$targets = @{}
foreach ($c in $commands) {
    if (!$targets.ContainsKey($c.Label)) { $targets[$c.Label] = $expanded.Count }
    $count = if ($c.Opcode -eq 'rungenericnpclowindex') { 5 } else { 1 }
    for ($i=0; $i -lt $count; $i++) { $expanded.Add(@($c,$i)) }
}
function Resolve-MamamuTarget([string]$label) {
    if (!$targets.ContainsKey($label)) { throw "mamamuYanScript unresolved target '$label'." }
    return $targets[$label].ToString()
}
$rows = [Collections.Generic.List[string]]::new()
$rows.Add($cutsceneCommandHeader)
foreach ($entry in $expanded) {
    $c=$entry[0]; $op=$c.Opcode; $args=$c.Operands; $parts=@($args -split ',\s*')
    $actor=''; $a=''; $b=''; $payload=''; $index=$rows.Count-1
    switch ($op) {
        'rungenericnpclowindex' {
            switch ($entry[1]) {
                0 { $op='nativeyield'; $payload='LoadText:'+$args.Substring(4) }
                1 { $op='initcollisions'; $actor='Mamamu' }
                2 { $op='checkabutton'; $actor='Mamamu' }
                3 { $op='showtext'; $a=$args.Substring(4); $payload=$allTexts[[Convert]::ToInt32($a,16)] }
                4 { $op='scriptjump'; $a=($index-2).ToString() }
            }
        }
        { $_ -in @('initcollisions','checkabutton') } { $actor='Mamamu' }
        'jumpifglobalflagset' {
            if (!$globalFlagValues.ContainsKey($parts[0])) { throw "Unknown Mamamu flag $args" }
            $op='jumpifmemoryeq'; $a='01'; $b=Resolve-MamamuTarget $parts[1]; $payload="Global:$($globalFlagValues[$parts[0]])"
        }
        { $_ -in @('jumpifroomflagset','jumpiftextoptioneq') } { $a=$parts[0].TrimStart('$'); $b=Resolve-MamamuTarget $parts[1] }
        'jumpiftradeitemeq' {
            if ($parts[0] -ne 'TRADEITEM_DOGGIE_MASK') { throw "Unknown Mamamu trade $args" }
            $a='04'; $b=Resolve-MamamuTarget $parts[1]
        }
        'jumpifmemoryeq' { $a=$parts[1].TrimStart('$'); $b=Resolve-MamamuTarget $parts[2]; $payload=$parts[0] }
        'scriptjump' { $op='scriptjumpyield'; $a=Resolve-MamamuTarget $args }
        'showtextlowindex' { $op='showtext'; $a=$args.Substring(4); $payload=$allTexts[[Convert]::ToInt32($a,16)] }
        'wait' { $a=$args }
        'setcoords' { $actor='Mamamu'; $a=$parts[0].TrimStart('$'); $b=$parts[1].TrimStart('$') }
        'setglobalflag' {
            if (!$globalFlagValues.ContainsKey($args)) { throw "Unknown Mamamu flag $args" }
            $a=([int]$globalFlagValues[$args]).ToString('x2')
        }
        'orroomflag' { $a=$args.TrimStart('$') }
        'asm15' {
            if ($args -notin @('mamamuYanRandomizeDogLocation','forceLinkDirection, DIR_LEFT','giveRingAToLink, SNOWSHOE_RING')) { throw "Unknown Mamamu native helper $args" }
            $op='native'; $payload=$args
        }
        'askforsecret' { if ($args -ne 'MAMAMU_SECRET') { throw "Unknown Mamamu secret $args" }; $op='nativeyield'; $payload='AskSecret' }
        'giveitem' { if ($args -ne 'TREASURE_TRADEITEM, $05') { throw "Unknown Mamamu reward $args" }; $a='41'; $b='05' }
        { $_ -in @('disableinput','enableinput') } { }
        default { throw "${path}:$($c.Line): unsupported Mamamu command $op" }
    }
    if ($op -eq 'showtext') {
        if (!$payload -or $payload -match '\\(?:call|jump)\(') { throw "Unresolved Mamamu TX_$a" }
        $id=[Convert]::ToInt32($a,16)
        if ($allTextPositions.ContainsKey($id)) { $b=$allTextPositions[$id].ToString() }
    }
    $rows.Add((New-CutsceneCommandRow $c.Script $index $c.Label $c.Line $op $actor $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\mamamu_commands.tsv'), $rows)
if ($expanded.Count -ne 78) { throw "mamamuYanScript expected 78 expanded commands, got $($expanded.Count)." }

$dogPath=Join-Path $Disassembly 'scripts\ages\scripts.s'
$dogAllowed=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($op in @('asm15','jumpifmemoryset','scriptjump','wait')) { [void]$dogAllowed.Add($op) }
$dogCommands=@(Read-AssemblyCutsceneCommands $dogPath 'dogInMamamusHouseScript' $dogAllowed)
if ($dogCommands.Count -ne 11) { throw 'dogInMamamusHouseScript expected 11 commands.' }
$dogTargets=@{}
foreach ($c in $dogCommands) { if (!$dogTargets.ContainsKey($c.Label)) { $dogTargets[$c.Label]=$c.Index } }
$rows=[Collections.Generic.List[string]]::new(); $rows.Add($cutsceneCommandHeader)
foreach ($c in $dogCommands) {
    $op=$c.Opcode; $a=''; $b=''; $payload=''
    switch ($op) {
        'asm15' { $op='native'; $payload=$c.Operands -replace '^scriptHelp.mamamuDog_', ''
            if ($payload -notin @('setCounterRandomly','decCounter','updateSpeedZ','checkReverseDirection','setZPositionTo0','reverseDirection')) { throw "Unknown indoor dog helper $payload" } }
        'jumpifmemoryset' {
            if ($c.Operands -ne 'wcddb, $80, @counterHit0') { throw 'Indoor dog counter branch changed.' }
            # scriptFunc_add3ToHl returns without carry on the false branch.
            $op='jumpifmemoryeqyieldonmiss'; $a='00'; $b=$dogTargets['@counterHit0'].ToString(); $payload='Counter'
        }
        'scriptjump' { $a=$dogTargets[$c.Operands].ToString() }
        'wait' { if ($c.Operands -ne '180') { throw 'Indoor dog rest changed.' }; $a=$c.Operands }
    }
    $rows.Add((New-CutsceneCommandRow $c.Script $c.Index $c.Label $c.Line $op '' $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\mamamu_dog_commands.tsv'), $rows)
$helper=Read-ImportText $path
$native=Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\mamamuDog.s')
if ($helper -notmatch '(?s)mamamuDog_randomCounterValues:\s+\.db (?<values>[^\r\n]+)' ) { throw 'Missing indoor dog counters.' }
$counters=@([regex]::Matches($Matches['values'],'\$([0-9a-f]{2})') | ForEach-Object { $_.Groups[1].Value })
if (($counters -join ',') -ne '78,b4,f0,ff,b4,f0,ff,ff' -or
    $helper -notmatch '(?s)mamamuDog_checkReverseDirection:\s+call objectApplySpeed.*?sub \$18\s+cp \$70.*?xor \$10.*?ld b,\$01' -or
    $helper -notmatch '(?s)mamamuDog_updateSpeedZ:\s+ld c,\$20.*?mamamuDog_hop:\s+ld bc,-\$c0' -or
    $native -notmatch '(?s)@dontDelete:.*?ld \(hl\),\$18.*?ld \(hl\),SPEED_100.*?ld a,\$02') { throw 'Indoor dog motion contract changed.' }
$rows=[Collections.Generic.List[string]]::new(); $rows.Add("# key`tvalue")
$rows.Add("counters`t$($counters -join ',')")
foreach ($i in 0..3) { $rows.Add("animation-$i`t$(Resolve-NpcAnimation 0x54 $i)") }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\mamamu_dog.tsv'), $rows)
}
