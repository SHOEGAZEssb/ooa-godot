# $48:$05 loads this wBigBuffer script, then always runs its native tail.
$tokayCookPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$tokayCookOpcodes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @('initcollisions','checkabutton','disableinput','jumpifroomflagset',
    'showtextlowindex','wait','jumpiftradeitemeq','enableinput','scriptjump',
    'jumpiftextoptioneq','writeobjectbyte','checkobjectbyteeq','giveitem')) {
    [void]$tokayCookOpcodes.Add($opcode)
}
$tokayCookCommands = @(Read-AssemblyCutsceneCommands $tokayCookPath 'tokayCookScript' $tokayCookOpcodes)
$tokayCookNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\tokay.s')
$tokayCookWrapper = Read-ImportText (Join-Path $Disassembly 'scripts\ages\scripts.s')
$tokayCookTrade = Read-ImportText (Join-Path $Disassembly 'constants\common\tradeItems.s')
if ($tokayCookWrapper -notmatch '(?ms)^tokayCookScript:\s+loadscript scriptHelp\.tokayCookScript' -or
    $tokayCookNative -notmatch '(?ms)^@initSubid05:\s+call interactionSetAlwaysUpdateBit\s+call tokayLoadScript\s+jp tokayState1' -or
    $tokayCookNative -notmatch '(?ms)^tokayRunSubid05:\s+call interactionRunScript\s+jp c,interactionDelete\s+ld e,Interaction.var3f' -or
    $tokayCookTrade -notmatch 'TRADEITEM_STINK_BAG\s+db ; \$02') {
    throw 'Tokay cook script installation, always-update wrapper or Stink Bag binding changed.'
}
# scriptCmd_jumpIfSomething preserves CP's carry on a missed comparison.
# Match/no item/lower item yield; a higher trade item continues on a miss.
# Preserve that distinction without changing the shared trade opcode's users.
$tokayCookExpanded=[Collections.Generic.List[object]]::new()
$tokayCookMap=@{}
foreach($command in $tokayCookCommands) {
    $tokayCookMap[$command.Index]=$tokayCookExpanded.Count
    $copy=$command.PSObject.Copy();$copy.Index=$tokayCookExpanded.Count;$tokayCookExpanded.Add($copy)
    if($command.Opcode -eq 'jumpiftradeitemeq') {
        foreach($outcome in @('Miss','Match')) {
            $copy=$command.PSObject.Copy();$copy.Index=$tokayCookExpanded.Count
            $copy.Opcode='cookTrade'+$outcome;$copy.Operands='';$tokayCookExpanded.Add($copy)
        }
    }
}
$tokayCookTradeCommand=@($tokayCookCommands | Where-Object Opcode -eq 'jumpiftradeitemeq')[0]
$tokayCookTradeIndex=$tokayCookMap[$tokayCookTradeCommand.Index]
$tokayCookTradeNext=$tokayCookMap[$tokayCookTradeCommand.Index+1]
$tokayCookTradeTarget=@($tokayCookExpanded | Where-Object Label -eq '@askForTrade')[0].Index
$tokayCookBindings=@{
    'jumpiftradeitemeq|TRADEITEM_STINK_BAG, @askForTrade'=@{
        Opcode='jumptablememory';Payload='CookTradeOutcome|'+($tokayCookTradeIndex+1)+','+($tokayCookTradeIndex+2)+','+$tokayCookTradeNext
    }
    'cookTradeMiss|'=@{Opcode='scriptjumpyield';Arg0=$tokayCookTradeNext.ToString()}
    'cookTradeMatch|'=@{Opcode='scriptjumpyield';Arg0=$tokayCookTradeTarget.ToString()}
}
$tokayCookRows = ConvertTo-CutsceneCommandRows $tokayCookExpanded 'Cook' `
    -symbols @{
        TRADEITEM_STINK_BAG = 0x02; TREASURE_TRADEITEM = 0x41
        'Interaction.var3f' = 0x3f; 'Interaction.var3e' = 'CookAwayFromStart'
    } -texts $allTexts -positions $allTextPositions -yieldOnJump $true -bindings $tokayCookBindings
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\tokay_cook_commands.tsv'), $tokayCookRows)
