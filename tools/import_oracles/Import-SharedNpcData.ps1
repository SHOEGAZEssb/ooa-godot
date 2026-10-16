# Infinite scripts with a native actor tail. Keep source command boundaries;
# only the initializer's script selection and native helpers live in C#.
$main = Join-Path $Disassembly 'scripts\ages\scripts.s'
$helper = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$ops = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($op in @('initcollisions','checkabutton','disableinput','enableinput',
    'wait','writeobjectbyte','asm15','showtext','showtextlowindex','showloadedtext',
    'scriptjump','setanimation','ormemory','jumpifroomflagset','checkrupeedisplayupdated',
    'orroomflag','jumptable_objectbyte','.dw')) { [void]$ops.Add($op) }
foreach ($spec in @(
    @('nayru',$helper,'nayruScript13','turnToFaceSomething',0x36),
    @('ralph',$main,'ralphSubid11Script','ralphSubid12Script',0x37),
    @('boy',$main,'boySubid00Script','boySubid01Script',0x3c),
    @('monkey',$main,'monkeySubid7Script_1','monkeySubid7Script_2',0x39),
    @('bear',$main,'bearSubid02Script','syrupScript_spawnShopItems',0x5d),
    @('old_man_gift',$main,'oldManScript_givesRupees','oldManScript_takesRupees',0x2e),
    @('adlar',$main,'adlarScript','librarianScript',0x29))) {
    $commands = @(Read-AssemblyCutsceneCommands $spec[1] $spec[2] $ops $spec[3])
    $targets = @{}
    foreach ($c in $commands) {
        if (!$targets.ContainsKey($c.Label)) { $targets[$c.Label] = $c.Index }
    }
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add($cutsceneCommandHeader)
    foreach ($c in $commands) {
        $op=$c.Opcode; $args=$c.Operands.Trim(); $actor=''; $a=''; $b=''; $payload=''
        $parts=@($args.Split(',') | ForEach-Object { $_.Trim() })
        switch ($op) {
            { $_ -in @('initcollisions','checkabutton') } { $actor='Npc' }
            { $_ -in @('disableinput','enableinput','checkrupeedisplayupdated') } { }
            'wait' { $a=$args }
            'scriptjump' { $a=[string]$targets[$args]; if($spec[1] -eq $helper){$op='scriptjumpyield'} }
            { $_ -in @('showtext','showtextlowindex') } {
                if($args -notmatch '^<?TX_([0-9a-f]{4})$'){throw "Invalid shared NPC text $args at $($c.Line)."}
                $op='showtext'; $a=$Matches[1]
                $payload=Resolve-OceanText ([Convert]::ToInt32($a,16)) ([Collections.Generic.HashSet[int]]::new())
            }
            'showloadedtext' { }
            'setanimation' { $actor='Npc'; $a=$args.Substring(1); $payload=Resolve-NpcAnimation $spec[4] ([Convert]::ToInt32($a,16)) }
            'writeobjectbyte' {
                if($parts[0] -ne 'Interaction.var39'){throw "Unsupported shared NPC write $args at $($c.Line)."}
                $actor='Npc'; $a='79'; $b=$parts[1].Substring(1)
            }
            'asm15' {
                $op='native'; $payload=$args -replace '^scriptHelp\.',''
                if($payload -notin @('turnToFaceLink','monkey_turnToFaceLink','monkey_setAnimationFromVar3a','oldMan_giveRupees')){throw "Unsupported shared NPC helper $args."}
            }
            'ormemory' {
                if($args -ne 'wTmpcfc0.genericCutscene.cfde, $04'){throw "Unsupported shared NPC signal $args."}
                $op='native'; $payload='boy_orCfde04'
            }
            'orroomflag' { $a=$args.Substring(1) }
            'jumpifroomflagset' { $a=$parts[0].Substring(1); $b=[string]$targets[$parts[1]] }
            'jumptable_objectbyte' {
                if($args -ne 'Interaction.var38'){throw "Unsupported shared NPC table $args."}
                $op='jumptablememory'; $payload='AdlarState|'+((@('@firstMeeting','@nayruPossessed','@queenPossessed','@queenMissing','@queenBackToNormal') | ForEach-Object { $targets[$_] }) -join ',')
            }
            '.dw' { throw 'Shared NPC jump-table parser emitted an unconsumed data row.' }
            default { throw "Unsupported shared NPC $op $args at $($c.Path):$($c.Line)." }
        }
        $rows.Add((New-CutsceneCommandRow $c.Script $c.Index $c.Label $c.Line $op $actor $a $b $payload))
    }
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes\shared_npc_$($spec[0])_commands.tsv"), $rows)
}
$textRows = [Collections.Generic.List[string]]::new(); $textRows.Add("# text-id`tutf8-base64")
foreach($id in @(0x012b,0x012e,0x5712,0x5713,0x5714,0x5715,0x5717,0x5718,0x5719,0x571a)) {
    $text=Resolve-OceanText $id ([Collections.Generic.HashSet[int]]::new())
    $textRows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($text)))")
}
Write-GeneratedTable((Join-Path $destination 'objects\shared_npc_texts.tsv'), $textRows)
$animations=[Collections.Generic.List[string]]::new(); $animations.Add("# id`tindex`tanimation")
foreach($id in @(0x36,0x37,0x3c,0x39,0x5d)) {
    $indices=if($id -eq 0x39){@(5,6)}elseif($id -eq 0x36){0..4}elseif($id -eq 0x5d){@(0)}else{0..3}
    foreach($index in $indices){
        $animation=Resolve-NpcAnimation $id $index
        if(!$animation){throw "Missing shared NPC animation `$$($id.ToString('x2')):`$$($index.ToString('x2'))."}
        $animations.Add("$($id.ToString('x2'))`t$($index.ToString('x2'))`t$animation")
    }
}
Write-GeneratedTable((Join-Path $destination 'objects\shared_npc_animations.tsv'), $animations)
$gfxSource=Read-ImportText (Join-Path $Disassembly 'data\ages\objectGfxHeaders.s')
if($gfxSource -notmatch 'm_ObjectGfxHeader spr_nayru_2, 1'){throw 'Nayru extra graphics header changed.'}
Write-GeneratedTable((Join-Path $destination 'objects\shared_npc_extra_graphics.tsv'), @("# id`tsprite", "36`tspr_nayru_2"))

# Advance-shop theft uses interaction counter2, not pixel-distance moves.
$shopPath=Join-Path $Disassembly 'scripts\common\commonScripts.s'
$shopOps=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($op in @('setspeed','playsound','movedown','moveright','moveleft','moveup','showtextlowindex','setangleandanimation','enableallobjects','scriptend')){[void]$shopOps.Add($op)}
$shopCommands=@(Read-AssemblyCutsceneCommands $shopPath 'shopkeeperSubid2Script_stopLink' $shopOps 'shopkeeperSubid1Script_stopLink')
$shopRows=[Collections.Generic.List[string]]::new();$shopRows.Add($cutsceneCommandHeader)
foreach($c in $shopCommands){
    $op=$c.Opcode;$args=$c.Operands.Trim();$actor='';$a='';$b='';$payload=''
    switch($op){
        'setspeed'{if($args -ne 'SPEED_200'){throw 'Advance theft speed changed.'};$actor='Keeper';$a=(Resolve-ObjectSpeed '200').ToString('x2')}
        'playsound'{if($args -ne 'SND_CLINK'){throw 'Advance theft sound changed.'};$a=(Resolve-SoundConstant $args).ToString('x2')}
        {$_ -in @('moveup','moveright','movedown','moveleft')}{
            $direction=@('moveup','moveright','movedown','moveleft').IndexOf($op)
            $op='move';$actor='Keeper';$a=($direction*8).ToString('x2');$b=$args.Substring(1);$payload=Resolve-NpcAnimation 0x46 $direction
        }
        'showtextlowindex'{if($args -ne '<TX_0e07'){throw 'Advance theft text changed.'};$op='showtext';$a='0e07';$payload=Resolve-OceanText 0x0e07 ([Collections.Generic.HashSet[int]]::new())}
        'setangleandanimation'{if($args -ne '$08'){throw 'Advance theft return direction changed.'};$op='setanimationcontinue';$actor='Keeper';$a='01';$payload=Resolve-NpcAnimation 0x46 1}
        'enableallobjects'{$op='setdisabledobjects';$a='00'}
        'scriptend'{}
        default{throw "Unsupported Advance theft $op at $($c.Line)."}
    }
    $shopRows.Add((New-CutsceneCommandRow $c.Script $c.Index $c.Label $c.Line $op $actor $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\advance_shop_theft_commands.tsv'),$shopRows)
