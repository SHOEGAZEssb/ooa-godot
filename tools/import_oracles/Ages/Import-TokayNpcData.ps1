# Resolve calls, jumps, position commands and unterminated fallthroughs at
# the same source boundary as NPC text. TX_0a20 continues into choice TX_0a21.
$tokayScriptTexts=@{}
foreach($id in @((0x0a00..0x0a3b)+(0x0a40..0x0a53)+(0x0a60..0x0a6c)+(0x2100..0x2106))) {
    if($allTexts.ContainsKey($id)){$tokayScriptTexts[$id]=Resolve-OceanText $id ([Collections.Generic.HashSet[int]]::new())}
}
# INTERAC_TOKAY $12-$18/$1f executes the common ROM-resident NPC loop.
# Keep the labels and original command boundaries, including its ROM jump.
$tokayGenericPath = Join-Path $Disassembly 'scripts/common/commonScripts.s'
$tokayGenericOps = [Collections.Generic.HashSet[string]]::new([string[]]@(
    'initcollisions', 'checkabutton', 'showloadedtext', 'scriptjump'))
$tokayGenericCommands = @(Read-AssemblyCutsceneCommands $tokayGenericPath 'genericNpcScript' $tokayGenericOps)
if ($tokayGenericCommands.Count -ne 4) { throw 'genericNpcScript must retain its four source commands.' }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/tokay_generic_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $tokayGenericCommands 'Tokay'))

# The held-item script selects an initialization snapshot once, then either
# returns the item or enters one of two ROM-resident generic loops.
$tokayHolderPath = Join-Path $Disassembly 'scripts/ages/scripts.s'
$tokayHolderOps = [Collections.Generic.HashSet[string]]::new([string[]]@(
    'jumptable_objectbyte','initcollisions','checkabutton','disableinput',
    'showloadedtext','wait','setanimation','writeobjectbyte','asm15',
    'showtextlowindex','orroomflag','enableinput','rungenericnpclowindex'))
$tokayHolderCommands = @(Read-AssemblyCutsceneCommands $tokayHolderPath 'tokayHoldingItemScript' $tokayHolderOps)
$tokayHolderTable = @(Read-AssemblyLabelNodes $tokayHolderPath 'tokayHoldingItemScript' |
    Where-Object { $_.Kind -eq 'Data' -and $_.Name -eq '.dw' } | ForEach-Object OperandText)
if (($tokayHolderTable -join ',') -ne '@holdingItem,@returnedItem,@linkGotAllItems') {
    throw 'tokayHoldingItemScript var3c branch table changed.'
}
$tokayHolderTargets = @{}
$tokayHolderExpanded = [Collections.Generic.List[object]]::new()
foreach ($command in $tokayHolderCommands) {
    if (!$tokayHolderTargets.ContainsKey($command.Label)) { $tokayHolderTargets[$command.Label] = $tokayHolderExpanded.Count }
    $count = if ($command.Opcode -eq 'rungenericnpclowindex') { 5 } else { 1 }
    for ($part = 0; $part -lt $count; $part++) { $tokayHolderExpanded.Add(@($command,$part)) }
}
$tokayHolderRows = [Collections.Generic.List[string]]::new()
$tokayHolderRows.Add($cutsceneCommandHeader)
foreach ($entry in $tokayHolderExpanded) {
    $command=$entry[0]; $index=$tokayHolderRows.Count-1
    $op=$command.Opcode; $actor=''; $a=''; $b=''; $payload=''
    switch ($op) {
        'jumptable_objectbyte' {
            if ($command.Operands -ne 'Interaction.var3c') { throw 'Unexpected Tokay selector.' }
            $op='jumptablememoryyield'; $payload='ReturnedItems|'+(($tokayHolderTable | ForEach-Object { $tokayHolderTargets[$_] }) -join ',')
        }
        { $_ -in @('initcollisions','checkabutton') } { $actor='Tokay' }
        { $_ -in @('disableinput','enableinput','showloadedtext') } { }
        'wait' { $a=$command.Operands }
        'setanimation' {
            if ($command.Operands -ne '$02') { throw 'Unexpected held-item Tokay animation.' }
            $actor='Tokay'; $a='02'; $payload=Resolve-NpcAnimation 0x48 2
        }
        'writeobjectbyte' {
            if ($command.Operands -ne 'Interaction.var3b, $01') { throw 'Unexpected held-item Tokay signal.' }
            $actor='Tokay'; $a='7b'; $b='01'
        }
        'asm15' {
            if ($command.Operands -ne 'scriptHelp.tokayGiveItemToLink') { throw 'Unexpected held-item Tokay native helper.' }
            $op='native'; $payload='GiveHeldItem'
        }
        'showtextlowindex' {
            $op='showtext'; $a=$command.Operands.Substring(4); $id=[Convert]::ToInt32($a,16)
            $payload=$tokayScriptTexts[$id]
            if ($allTextPositions.ContainsKey($id)) { $b=$allTextPositions[$id].ToString() }
        }
        'orroomflag' { $a=$command.Operands.TrimStart('$') }
        'rungenericnpclowindex' {
            $id=$command.Operands.Substring(4)
            switch ($entry[1]) {
                0 { $op='nativeyield'; $payload='LoadText:'+$id }
                1 { $op='initcollisions'; $actor='Tokay' }
                2 { $op='checkabutton'; $actor='Tokay' }
                3 { $op='showloadedtext' }
                4 { $op='scriptjump'; $a=($index-2).ToString() }
            }
        }
        default { throw "$tokayHolderPath`:$($command.Line): unsupported Tokay holder command '$op'." }
    }
    $tokayHolderRows.Add((New-CutsceneCommandRow $command.Script $index $command.Label $command.Line $op $actor $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/tokay_holder_commands.tsv'), $tokayHolderRows)

$tokayShopkeeperOps=[Collections.Generic.HashSet[string]]::new([string[]]@(
    'makeabuttonsensitive','checkabutton','asm15','jumpifobjectbyteeq','showtextlowindex','scriptjump'))
$tokayShopkeeperCommands=@(Read-AssemblyCutsceneCommands $tokayHolderPath 'tokayShopkeeperScript' $tokayShopkeeperOps)
$tokayShopkeeperNoItems=@($tokayShopkeeperCommands | Where-Object Label -eq '@noItemsAvailable')[0].Index
$tokayShopkeeperBindings=@{
    'asm15|scriptHelp.tokayFindShopItem'=@{Opcode='native';Payload='FindShopItem'}
    'jumpifobjectbyteeq|Interaction.var3f, $00, @noItemsAvailable'=@{
        Opcode='jumpifmemoryeq';Arg0='00';Arg1=$tokayShopkeeperNoItems.ToString();Payload='ShopItemMatch'
    }
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/tokay_shopkeeper_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $tokayShopkeeperCommands 'Tokay' -texts $tokayScriptTexts -positions $allTextPositions -bindings $tokayShopkeeperBindings))

$tokayPlotOps=[Collections.Generic.HashSet[string]]::new([string[]]@(
    'makeabuttonsensitive','setangleandanimation','checkabutton','asm15','jumpifroomflagset',
    'jumpifitemobtained','showtextlowindex','scriptjump','disableinput','wait','setspeed',
    'applyspeed','spawninteraction','playsound','enableinput'))
$tokayPlotCommands=@(Read-AssemblyCutsceneCommands $tokayHolderPath 'tokayAtSeedlingPlotScript' $tokayPlotOps)
$tokayPlotBindings=@{
    'setangleandanimation|$10'=@{Opcode='native';Payload='PlotAngle:10'}
    'asm15|scriptHelp.tokayTurnToFaceLink'=@{Opcode='native';Payload='PlotFaceLink'}
    'asm15|scriptHelp.tokayFlipDirection'=@{Opcode='native';Payload='PlotFlip'}
    'asm15|scriptHelp.tokayPlantScentSeedling'=@{Opcode='native';Payload='PlantSeedling'}
    'setspeed|SPEED_100'=@{Opcode='nativeyield';Payload='PlotSpeed:28'}
    'applyspeed|$10'=@{Opcode='nativeblock';Actor='Tokay';Arg0='16';Payload='PlotMove'}
    'spawninteraction|INTERAC_DECORATION, $04, $38, $48'=@{Opcode='nativeyield';Payload='PlantDecoration'}
    'playsound|SND_GETSEED'=@{Opcode='nativeyield';Payload='PlantSound'}
}
foreach($command in $tokayPlotCommands | Where-Object Opcode -eq 'jumpifitemobtained') {
    $parts=@($command.Operands.Split(',') | ForEach-Object Trim)
    $treasure=switch($parts[0]) { 'TREASURE_SCENT_SEEDLING' {'4d'} 'TREASURE_SCENT_SEEDS' {'21'} default {throw 'Unexpected plot treasure.'} }
    $target=@($tokayPlotCommands | Where-Object Label -eq $parts[1])[0].Index
    $tokayPlotBindings['jumpifitemobtained|'+$command.Operands]=@{
        Opcode='jumptablememoryyield';Payload='Treasure:'+$treasure+'|'+($command.Index+1)+','+$target
    }
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/tokay_seedling_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $tokayPlotCommands 'Tokay' -texts $tokayScriptTexts -positions $allTextPositions -bindings $tokayPlotBindings))

$tokayShieldOps=[Collections.Generic.HashSet[string]]::new([string[]]@(
    'initcollisions','checkabutton','disableinput','jumpifroomflagset','showtextlowindex',
    'wait','setanimation','writeobjectbyte','asm15','orroomflag','enableinput','scriptjump'))
$tokayShieldCommands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scriptHelper.s') 'tokayWithShieldUpgradeScript' $tokayShieldOps)
$tokayShieldBindings=@{
    'asm15|tokayGiveShieldUpgradeToLink'=@{Opcode='native';Payload='GiveShield'}
    'orroomflag|$40'=@{Opcode='orroomflag';Arg0='40'}
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/tokay_shield_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $tokayShieldCommands 'Tokay' -symbols @{'Interaction.var3b'=0x7b} `
        -animations @{2=(Resolve-NpcAnimation 0x48 2)} -texts $tokayScriptTexts -positions $allTextPositions `
        -bindings $tokayShieldBindings -yieldOnJump $true))

$tokayVineOps=[Collections.Generic.HashSet[string]]::new([string[]]@(
    'initcollisions','enableinput','setanimation','checkabutton','disableinput','turntofacelink',
    'jumpifroomflagset','orroomflag','writeobjectbyte','writememory','asm15','wait','showtextlowindex','scriptjump'))
$tokayVineCommands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scriptHelper.s') 'tokayExplainingVinesScript' $tokayVineOps)
$tokayVineBindings=@{
    'turntofacelink|'=@{Opcode='native';Payload='VineFaceLink'}
    'writememory|w1Link.direction, $03'=@{Opcode='native';Payload='VineLinkLeft'}
    'asm15|tokayCreateExclamationMark'=@{Opcode='native';Payload='VineExclamation'}
    'orroomflag|$40'=@{Opcode='orroomflag';Arg0='40'}
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/tokay_vine_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $tokayVineCommands 'Tokay' -symbols @{'Interaction.var31'=0x71;'Interaction.speedZ'=0x54} `
        -animations @{2=(Resolve-NpcAnimation 0x48 2)} -texts $tokayScriptTexts -positions $allTextPositions `
        -bindings $tokayVineBindings -yieldOnJump $true))

$tokayEscapeOps=[Collections.Generic.HashSet[string]]::new([string[]]@(
    'checkmemoryeq','disableinput','wait','showtextlowindex','setspeed','moveup',
    'setanimation','setzspeed','playsound','asm15','jumpifobjectbyteeq','scriptjump',
    'moveleft','enableinput','orroomflag','scriptend'))
$tokayEscapeCommands=@(Read-AssemblyCutsceneCommands $tokayHolderPath 'tokayRunningFromRosaScript' $tokayEscapeOps)
$tokayEscapeLanded=@($tokayEscapeCommands | Where-Object Label -eq '@landed')[0].Index
$tokayEscapeBindings=@{
    'setzspeed|-$01c0'=@{Opcode='native';Payload='EscapeJump'}
    'playsound|SND_JUMP'=@{Opcode='nativeyield';Payload='EscapeJumpSound'}
    'asm15|objectUpdateSpeedZ, $20'=@{Opcode='native';Payload='EscapeGravity'}
    'jumpifobjectbyteeq|Interaction.zh, $00, @landed'=@{Opcode='jumpifmemoryeq';Arg0='00';Arg1=$tokayEscapeLanded.ToString();Payload='EscapeZHigh'}
    'orroomflag|$80'=@{Opcode='orroomflag';Arg0='80'}
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/tokay_escape_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $tokayEscapeCommands 'Tokay' -symbols @{'w1Link.yh'='LinkY';'SPEED_180'=(Resolve-ObjectSpeed '180')} `
        -animations @{0=(Resolve-NpcAnimation 0x48 0);2=(Resolve-NpcAnimation 0x48 2);3=(Resolve-NpcAnimation 0x48 3)} `
        -texts $tokayScriptTexts -positions $allTextPositions -bindings $tokayEscapeBindings))

# Keep both hungry Tokays as independent ROM-resident scripts. Their shared
# signals belong to the live companion slot, not a room-event coordinator.
$tokayDimitriOps=[Collections.Generic.HashSet[string]]::new([string[]]@(
    'disableinput','jumpifmemoryset','setangleandanimation','showtextlowindex','ormemory',
    'makeabuttonsensitive','enableinput','checkabutton','asm15','jumpifobjectbyteeq',
    'jumptable_memoryaddress','scriptjump','spawninteraction','wait','moveleft','enablemenu',
    'scriptend','writeobjectbyte','enableallobjects'))
foreach($number in @(1,2)) {
    $script="tokayWithDimitri${number}Script"
    $commands=@(Read-AssemblyCutsceneCommands $tokayHolderPath $script $tokayDimitriOps)
    $targets=@{};foreach($c in $commands){if(!$targets.ContainsKey($c.Label)){$targets[$c.Label]=$c.Index}}
    $bindings=@{}
    foreach($c in $commands) {
        $parts=@($c.Operands.Split(',') | ForEach-Object Trim);$key=$c.Opcode+'|'+$c.Operands
        switch($c.Opcode) {
            'jumpifmemoryset' {
                $binding=switch($parts[0]){'wDimitriState' {'DimitriMask:'} 'w1Companion.var3e' {'CompanionMask:'} default {throw 'Unknown Dimitri signal reader.'}}
                $bindings[$key]=@{Opcode='jumpifmemoryeqyieldonmiss';Arg0='01';Arg1=$targets[$parts[2]].ToString();Payload=$binding+$parts[1].TrimStart('$')}
            }
            'ormemory' {
                $binding=switch($parts[0]){'wDimitriState' {'OrDimitri:'} 'w1Companion.var3e' {'OrCompanion:'} default {throw 'Unknown Dimitri signal writer.'}}
                $bindings[$key]=@{Opcode='native';Payload=$binding+$parts[1].TrimStart('$')}
            }
            'setangleandanimation' {$bindings[$key]=@{Opcode='native';Payload='DimitriAngle:'+$parts[0].TrimStart('$')}}
            'asm15' {
                $helper=switch($c.Operands){
                    'scriptHelp.tokayTurnToFaceLink' {'PlotFaceLink'}
                    'scriptHelp.tokayCheckHaveEmberSeeds' {'CheckEmberSeeds'}
                    'scriptHelp.tokayDecNumEmberSeeds' {'ConsumeEmberSeed'}
                    default {throw "Unknown Dimitri helper $($c.Operands)."}
                }
                $bindings[$key]=@{Opcode='native';Payload=$helper}
            }
            'jumpifobjectbyteeq' {
                $binding=switch($parts[0]){'Interaction.var3f' {'EmberSnapshot'} 'Interaction.pressedAButton' {'PressedA'} default {throw 'Unknown hungry Tokay byte.'}}
                $bindings[$key]=@{Opcode='jumpifmemoryeq';Arg0=$parts[1].TrimStart('$');Arg1=$targets[$parts[2]].ToString();Payload=$binding}
            }
            'jumptable_memoryaddress' {
                if($c.Operands -ne 'wSelectedTextOption'){throw 'Unknown hungry Tokay jump table.'}
                $table=@(Read-AssemblyLabelNodes $tokayHolderPath $script | Where-Object { $_.Kind -eq 'Data' -and $_.Name -eq '.dw' } | ForEach-Object OperandText)
                if(($table -join ',') -ne '@selectedYes,@selectedNo'){throw 'Hungry Tokay choice table changed.'}
                $bindings[$key]=@{Opcode='jumptablememoryyield';Payload='TextOption|'+(($table | ForEach-Object {$targets[$_]}) -join ',')}
            }
            'spawninteraction' {
                if($parts[0] -ne 'INTERAC_TOKAY_CUTSCENE_EMBER_SEED' -or $parts[1] -ne '$00'){throw 'Unknown Tokay rescue child.'}
                $bindings[$key]=@{Opcode='nativeyield';Payload='Ember:'+$parts[2].TrimStart('$')+':'+$parts[3].TrimStart('$')}
            }
            'enablemenu' {$bindings[$key]=@{Opcode='native';Payload='EnableMenu'}}
            'enableallobjects' {$bindings[$key]=@{Opcode='setdisabledobjects';Arg0='00'}}
        }
    }
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes/tokay_dimitri_${number}_commands.tsv"),
        (ConvertTo-CutsceneCommandRows $commands 'Tokay' -symbols @{'Interaction.pressedAButton'=0x71} `
            -animations @{3=(Resolve-NpcAnimation 0x48 3)} -texts $tokayScriptTexts -positions $allTextPositions -bindings $bindings))
}

$dimitriRescueOps=[Collections.Generic.HashSet[string]]::new([string[]]@(
    'jumpifmemoryset','scriptjump','showtext','ormemory','checkmemoryeq','writememory',
    'enableinput','disableinput','jumpifmemoryeq','enableallobjects','scriptend'))
$dimitriRescueCommands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scriptHelper.s') 'companionScript_subid07Script_body' $dimitriRescueOps)
$dimitriRescueTargets=@{};foreach($c in $dimitriRescueCommands){if(!$dimitriRescueTargets.ContainsKey($c.Label)){$dimitriRescueTargets[$c.Label]=$c.Index}}
$dimitriRescueIntro=@($dimitriRescueCommands | Where-Object { $_.Label -eq '++' -and $_.Opcode -eq 'showtext' })[0].Index
$dimitriRescueMount=@($dimitriRescueCommands | Where-Object { $_.Label -eq '++' -and $_.Opcode -eq 'writememory' })[0].Index
$dimitriRescueBindings=@{
    'jumpifmemoryeq|wAnimalCompanion, SPECIALOBJECT_DIMITRI, @notFirstMeeting'=@{
        Opcode='jumpifmemoryeq';Arg0='0c';Arg1=$dimitriRescueTargets['@notFirstMeeting'].ToString();Payload='AnimalCompanion'
    }
    'scriptjump|++'=@{Opcode='scriptjumpyield';Arg0=$dimitriRescueMount.ToString()}
    'enableallobjects|'=@{Opcode='setdisabledobjects';Arg0='00'}
    'writememory|w1Companion.var03, $01'=@{Opcode='native';Payload='ForceMount'}
}
foreach($c in $dimitriRescueCommands) {
    $parts=@($c.Operands.Split(',') | ForEach-Object Trim);$key=$c.Opcode+'|'+$c.Operands
    if($c.Opcode -eq 'jumpifmemoryset') {
        $binding=switch($parts[0]){'wDimitriState' {'DimitriMask:'} 'w1Companion.var3e' {'CompanionMask:'} default {throw 'Unknown Dimitri rescue signal.'}}
        $target=if($parts[2] -eq '++'){$dimitriRescueIntro}else{$dimitriRescueTargets[$parts[2]]}
        $dimitriRescueBindings[$key]=@{Opcode='jumpifmemoryeqyieldonmiss';Arg0='01';Arg1=$target.ToString();Payload=$binding+$parts[1].TrimStart('$')}
    }
    if($c.Opcode -eq 'ormemory') {
        if($parts[0] -ne 'wDimitriState'){throw 'Unknown Dimitri rescue state write.'}
        $dimitriRescueBindings[$key]=@{Opcode='native';Payload='OrDimitri:'+$parts[1].TrimStart('$')}
    }
}
$dimitriRescueTexts=@{};foreach($id in @(0x2100,0x2101,0x2102,0x2106)) {
    $dimitriRescueTexts[$id]=$tokayScriptTexts[$id]
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/dimitri_rescue_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $dimitriRescueCommands '' -symbols @{
        'w1Companion.var3d'='CompanionButton';'wLinkObjectIndex'='LinkMounted';'>w1Companion'=1
        'wAnimalCompanion'='AnimalCompanion';'SPECIALOBJECT_DIMITRI'=0x0c
    } -texts $dimitriRescueTexts -positions $allTextPositions -bindings $dimitriRescueBindings -yieldOnJump $true `
        -externalTargets @{'mainScripts.companionScript_subid07Script'=0}))

$tokayDimitriRoomRows=[Collections.Generic.List[string]]::new()
$tokayDimitriRoomRows.Add("# group`troom`torder`tid`tsubid`ty`tx`tsource")
$tokayDimitriPlacements=@(Read-AssemblyLabelNodes (Join-Path $Disassembly 'objects/ages/mainData.s') 'group0MapaaObjectData' | Where-Object Name -eq 'obj_Interaction')
if(($tokayDimitriPlacements.OperandText -join ',') -ne '$48 $0f $48 $18,$48 $10 $58 $38,$67 $03,$71 $07') {throw 'Crescent Island Dimitri object prefix changed.'}
for($i=0;$i -lt $tokayDimitriPlacements.Count;$i++) {
    $p=$tokayDimitriPlacements[$i];$bytes=@($p.OperandText -split '\s+' | ForEach-Object {$_.TrimStart('$')})
    $y=if($bytes.Count -eq 4){$bytes[2]}else{'00'};$x=if($bytes.Count -eq 4){$bytes[3]}else{'00'}
    $tokayDimitriRoomRows.Add("0`taa`t$i`t$($bytes[0])`t$($bytes[1])`t$y`t$x`tmainData.s:$($p.Line):group0MapaaObjectData")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects/tokay_dimitri_room.tsv'),$tokayDimitriRoomRows)

# The two Wild Tokay managers remain ROM scripts; preserve every native
# yield and expand the embedded generic-NPC loop at its original boundary.
$wildOps=[Collections.Generic.HashSet[string]]@('initcollisions','jumpifroomflagset','jumpifitemobtained','checkabutton','showtextlowindex','scriptjump','asm15','disableinput','wait','writeobjectbyte','playsound','settextid','jumpifmemoryeq','showloadedtext','jumpiftextoptioneq','setanimation','enableinput','jumpifobjectbyteeq','scriptend','jumpifglobalflagset','rungenericnpclowindex','askforsecret','setglobalflag','giveitem','generatesecret')
foreach($era in @('past','present')) {
    $commands=@(Read-AssemblyCutsceneCommands $tokayHolderPath "tokayGameManagerScript_$era" $wildOps)
    $expanded=[Collections.Generic.List[object]]::new();$targets=@{}
    foreach($c in $commands) {
        if(!$targets.ContainsKey($c.Label)){$targets[$c.Label]=$expanded.Count}
        $count=if($c.Opcode -eq 'rungenericnpclowindex'){5}else{1}
        for($i=0;$i -lt $count;$i++){$expanded.Add(@($c,$i))}
    }
    $rows=[Collections.Generic.List[string]]::new();$rows.Add($cutsceneCommandHeader)
    foreach($entry in $expanded) {
        $c=$entry[0];$op=$c.Opcode;$args=$c.Operands;$p=@($args -split ',\s*')
        $actor='';$a='';$b='';$payload='';$index=$rows.Count-1
        switch($op) {
            'rungenericnpclowindex' {
                switch($entry[1]) {
                    0 {$op='nativeyield';$payload='LoadText:'+$args.Substring(4)}
                    1 {$op='initcollisions';$actor='Tokay'}
                    2 {$op='checkabutton';$actor='Tokay'}
                    3 {$op='showloadedtext'}
                    4 {$op='scriptjump';$a=($index-2).ToString()}
                }
            }
            {$_ -in @('initcollisions','checkabutton')} {$actor='Tokay'}
            'jumpifglobalflagset' {
                if(!$globalFlagValues.ContainsKey($p[0])){throw "Unknown Wild Tokay flag $args"}
                $op='jumpifmemoryeq';$a='01';$b=$targets[$p[1]].ToString();$payload="Global:$($globalFlagValues[$p[0]])"
            }
            'jumpifitemobtained' {
                if($p[0] -ne 'TREASURE_BRACELET'){throw "Unknown Wild Tokay item $args"}
                $op='jumptablememoryyield';$payload="Bracelet|$($index+1),$($targets[$p[1]])"
            }
            {$_ -in @('jumpifroomflagset','jumpiftextoptioneq')} {$a=$p[0].TrimStart('$');$b=$targets[$p[1]].ToString()}
            'jumpifmemoryeq' {
                $a=$p[1].TrimStart('$');$b=$targets[$p[2]].ToString()
                $payload=switch($p[0]){'wWildTokayGameLevel' {'Level'} 'wTmpcfc0.wildTokay.cfde' {'Result'} 'wTextInputResult' {'SecretResult'} default {throw "Unknown Wild Tokay memory $args"}}
            }
            'jumpifobjectbyteeq' {
                if($p[0] -ne 'Interaction.var3d'){throw "Unknown Wild Tokay object byte $args"}
                $op='jumpifmemoryeq';$a=$p[1].TrimStart('$');$b=$targets[$p[2]].ToString();$payload='CannotAfford'
            }
            'scriptjump' {$a=$targets[$args].ToString()}
            {$_ -in @('showtextlowindex','settextid')} {
                $a=$args -replace '^<?TX_','';$payload=$tokayScriptTexts[[Convert]::ToInt32($a,16)]
                if($op -eq 'settextid'){$actor='Tokay'}else{$op='showtext'}
            }
            'wait' {$a=$args}
            'setanimation' {$actor='Tokay';$a=$args.TrimStart('$');$payload=Resolve-NpcAnimation 0x48 ([Convert]::ToInt32($a,16))}
            'writeobjectbyte' {$actor='Tokay';$a=switch($p[0]){'Interaction.animCounter' {'60'} 'Interaction.var3b' {'7b'} default {throw "Unknown Wild Tokay register $args"}};$b=$p[1].TrimStart('$')}
            'asm15' {
                $payload=switch($args) {
                    'scriptHelp.tokayGame_determinePrizeAndCheckRupees' {'PreparePrize'}
                    'scriptHelp.tokayGame_checkRupees' {'CheckRupees'}
                    'scriptHelp.tokayGame_createAccessoryForPrize, $06' {'RaisePrize'}
                    'removeRupeeValue, RUPEEVAL_10' {'TakeRupees'}
                    'scriptHelp.tokayGame_resetRoomFlag40' {'ResetRoomFlag40'}
                    'scriptHelp.tokayGame_givePrizeToLink' {'GivePrize'}
                    'scriptHelp.tokayGiveBombUpgrade' {'BombCapacity'}
                    default {throw "Unknown Wild Tokay helper $args"}
                };$op='native'
            }
            'playsound' {if($args -ne 'SND_GETSEED'){throw "Unknown Wild Tokay cue $args"};$op='nativeyield';$payload='PrizeSound'}
            'askforsecret' {if($args -ne 'TOKAY_SECRET'){throw "Unknown Tokay secret $args"};$op='nativeyield';$payload='AskSecret'}
            'generatesecret' {if($args -ne 'TOKAY_RETURN_SECRET'){throw "Unknown Tokay return secret $args"};$op='nativeyield';$payload='GenerateSecret'}
            'setglobalflag' {$a=([int]$globalFlagValues[$args]).ToString('x2')}
            'giveitem' {if($args -ne 'TREASURE_BOMB_UPGRADE, $00'){throw "Unknown Tokay prize $args"};$a='61';$b='00'}
            {$_ -in @('disableinput','enableinput','scriptend','showloadedtext')} {}
            default {throw "Unsupported Wild Tokay command $op $args"}
        }
        if($op -eq 'showtext'){$id=[Convert]::ToInt32($a,16);if($allTextPositions.ContainsKey($id)){$b=$allTextPositions[$id].ToString()}}
        $rows.Add((New-CutsceneCommandRow $c.Script $index $c.Label $c.Line $op $actor $a $b $payload))
    }
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes/wild_tokay_${era}_commands.tsv"),$rows)
}
