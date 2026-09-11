# The remaining InteractionController-owned interactionRunScript loops.
# These command streams retain their source command boundaries while their
# native presentation, secret generation, and giveitem handoffs stay in
# dedicated runtime hosts.

# linkedGameNpcScript is shared by the linked Ghini and Great Fairy. Their
# visibility/spawn predicates are already imported with the NPC records, so
# this stream begins at initcollisions and retains the complete talk loop.
$linkedNpcScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$linkedNpcOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'asm15', 'jumpifmemoryset', 'initcollisions', 'checkabutton',
    'disableinput', 'showloadedtext', 'wait', 'jumpiftextoptioneq',
    'addobjectbyte', 'enableinput', 'scriptjump')) {
    [void]$linkedNpcOpcodes.Add($opcode)
}
$linkedNpcCommands = @(Read-AssemblyCutsceneCommands `
    $linkedNpcScriptPath 'linkedGameNpcScript' $linkedNpcOpcodes `
    'plenSubid0Script')
$linkedNpcExpected = @(
    @('asm15', 'scriptHelp.linkedNpc_checkShouldSpawn'),
    @('jumpifmemoryset', 'wcddb, $80, stubScript'),
    @('initcollisions', ''),
    @('asm15', 'scriptHelp.linkedNpc_initHighTextIndex'),
    @('asm15', 'scriptHelp.linkedNpc_calcLowTextIndex, $00'),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('showloadedtext', ''),
    @('wait', '20'),
    @('jumpiftextoptioneq', '$00, @answeredYes'),
    @('addobjectbyte', 'Interaction.textID, $01'),
    @('showloadedtext', ''),
    @('enableinput', ''),
    @('scriptjump', '@offerSecret'),
    @('asm15', 'scriptHelp.linkedNpc_checkHasExtraTextBox'),
    @('jumpifmemoryset', 'wcddb, $80, @generateSecret'),
    @('asm15', 'scriptHelp.linkedNpc_calcLowTextIndex, $02'),
    @('showloadedtext', ''),
    @('wait', '20'),
    @('jumpiftextoptioneq', '$01, @showExtraText'),
    @('asm15', 'scriptHelp.linkedNpc_generateSecret'),
    @('asm15', 'scriptHelp.linkedNpc_calcLowTextIndex, $03'),
    @('showloadedtext', ''),
    @('wait', '20'),
    @('jumpiftextoptioneq', '$01, @tellSecret'),
    @('asm15', 'scriptHelp.linkedNpc_calcLowTextIndex, $04'),
    @('showloadedtext', ''),
    @('enableinput', ''),
    @('asm15', 'scriptHelp.linkedNpc_checkHasExtraTextBox'),
    @('jumpifmemoryset', 'wcddb, $80, @offerSecret'),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('scriptjump', '@answeredYes')
)
if ($linkedNpcCommands.Count -ne $linkedNpcExpected.Count) {
    throw "linkedGameNpcScript expected 33 commands, parsed $($linkedNpcCommands.Count)."
}
for ($index = 0; $index -lt $linkedNpcExpected.Count; $index++) {
    $operands = ([string]$linkedNpcCommands[$index].Operands).Trim()
    if ($linkedNpcCommands[$index].Opcode -ne $linkedNpcExpected[$index][0] -or
        $operands -ne $linkedNpcExpected[$index][1]) {
        throw "linkedGameNpcScript command $index changed from " +
            "$($linkedNpcExpected[$index] -join ' ')."
    }
}

$linkedNpcCommandSpecs = @(
    @($linkedNpcCommands[2],  'initcollisions', 'LinkedNpc', '', '', ''),
    @($linkedNpcCommands[3],  'native', '', '', '', 'linkedNpc_initHighTextIndex'),
    @($linkedNpcCommands[4],  'native', '', '', '', 'linkedNpc_selectOffer'),
    @($linkedNpcCommands[5],  'checkabutton', 'LinkedNpc', '', '', ''),
    @($linkedNpcCommands[6],  'disableinput', '', '', '', ''),
    @($linkedNpcCommands[7],  'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[8],  'wait', '', '20', '', ''),
    @($linkedNpcCommands[9],  'jumpiftextoptioneq', '', '00', '12', ''),
    @($linkedNpcCommands[10], 'native', '', '', '', 'linkedNpc_selectRefusal'),
    @($linkedNpcCommands[11], 'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[12], 'enableinput', '', '', '', ''),
    @($linkedNpcCommands[13], 'scriptjump', '', '2', '', ''),
    @($linkedNpcCommands[14], 'native', '', '', '', 'linkedNpc_checkHasExtraTextBox'),
    @($linkedNpcCommands[15], 'jumpifmemoryeqyieldonmiss', '', '00', '18', 'LinkedNpcHasExtraText'),
    @($linkedNpcCommands[16], 'native', '', '', '', 'linkedNpc_selectExplanation'),
    @($linkedNpcCommands[17], 'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[18], 'wait', '', '20', '', ''),
    @($linkedNpcCommands[19], 'jumpiftextoptioneq', '', '01', '14', ''),
    @($linkedNpcCommands[20], 'native', '', '', '', 'linkedNpc_generateSecret'),
    @($linkedNpcCommands[21], 'native', '', '', '', 'linkedNpc_selectSecret'),
    @($linkedNpcCommands[22], 'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[23], 'wait', '', '20', '', ''),
    @($linkedNpcCommands[24], 'jumpiftextoptioneq', '', '01', '20', ''),
    @($linkedNpcCommands[25], 'native', '', '', '', 'linkedNpc_selectFinal'),
    @($linkedNpcCommands[26], 'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[27], 'enableinput', '', '', '', ''),
    @($linkedNpcCommands[28], 'native', '', '', '', 'linkedNpc_checkHasExtraTextBox'),
    @($linkedNpcCommands[29], 'jumpifmemoryeqyieldonmiss', '', '00', '2', 'LinkedNpcHasExtraText'),
    @($linkedNpcCommands[30], 'checkabutton', 'LinkedNpc', '', '', ''),
    @($linkedNpcCommands[31], 'disableinput', '', '', '', ''),
    @($linkedNpcCommands[32], 'scriptjump', '', '12', '', '')
)
$linkedNpcCommandRows = [Collections.Generic.List[string]]::new()
$linkedNpcCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
for ($index = 0; $index -lt $linkedNpcCommandSpecs.Count; $index++) {
    $spec = $linkedNpcCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $linkedNpcCommandRows.Add((New-CutsceneCommandRow `
        'linkedGameNpcScript' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\linked_game_npc_commands.tsv'),
    $linkedNpcCommandRows)

# Past Bipin's script lives in scriptHelper.s and uses the ordinary Gasha Seed
# treasure object. Preserve wait 1 followed by checktext as two distinct
# source commands.
$pastBipinScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$pastBipinOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'enableinput', 'checkabutton', 'disableinput',
    'jumpifroomflagset', 'showtext', 'giveitem', 'wait', 'checktext',
    'scriptjump')) {
    [void]$pastBipinOpcodes.Add($opcode)
}
$pastBipinCommands = @(Read-AssemblyCutsceneCommands `
    $pastBipinScriptPath 'bipinScript3' $pastBipinOpcodes `
    'setNextChildStage')
$pastBipinExpected = @(
    @('initcollisions', ''),
    @('enableinput', ''),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('jumpifroomflagset', '$20, @alreadyGaveSeed'),
    @('showtext', 'TX_4311'),
    @('giveitem', 'TREASURE_GASHA_SEED, $08'),
    @('wait', '1'),
    @('checktext', ''),
    @('showtext', 'TX_4312'),
    @('scriptjump', '@loop'),
    @('showtext', 'TX_4313'),
    @('scriptjump', '@loop')
)
if ($pastBipinCommands.Count -ne $pastBipinExpected.Count) {
    throw "bipinScript3 expected 13 commands, parsed $($pastBipinCommands.Count)."
}
for ($index = 0; $index -lt $pastBipinExpected.Count; $index++) {
    $operands = ([string]$pastBipinCommands[$index].Operands).Trim()
    if ($pastBipinCommands[$index].Opcode -ne $pastBipinExpected[$index][0] -or
        $operands -ne $pastBipinExpected[$index][1]) {
        throw "bipinScript3 command $index changed from " +
            "$($pastBipinExpected[$index] -join ' ')."
    }
}
foreach ($textId in 0x4311..0x4313) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Bipin text TX_$($textId.ToString('x4'))."
    }
}
$pastBipinTreasure = $treasureObjectRecords['TREASURE_OBJECT_GASHA_SEED_08']
if ($null -eq $pastBipinTreasure -or
    $pastBipinTreasure.Treasure -ne 0x34 -or
    $pastBipinTreasure.SubId -ne 0x08 -or
    $pastBipinTreasure.Parameter -ne 0x01 -or
    $pastBipinTreasure.TextId -ne 0x004b -or
    $pastBipinTreasure.Graphic -ne 0x0d) {
    throw 'TREASURE_OBJECT_GASHA_SEED_08 no longer matches bipinScript3.'
}
$pastBipinCommandSpecs = @(
    @($pastBipinCommands[0],  'initcollisions', 'PastBipin', '', '', ''),
    @($pastBipinCommands[1],  'enableinput', '', '', '', ''),
    @($pastBipinCommands[2],  'checkabutton', 'PastBipin', '', '', ''),
    @($pastBipinCommands[3],  'disableinput', '', '', '', ''),
    @($pastBipinCommands[4],  'jumpifroomflagset', '', '20', '11', ''),
    @($pastBipinCommands[5],  'showtext', '', '4311', '', $allTexts[0x4311]),
    @($pastBipinCommands[6],  'giveitem', '', '34', '08', ''),
    @($pastBipinCommands[7],  'wait', '', '1', '', ''),
    @($pastBipinCommands[8],  'checktext', '', '', '', ''),
    @($pastBipinCommands[9],  'showtext', '', '4312', '', $allTexts[0x4312]),
    @($pastBipinCommands[10], 'scriptjump', '', '1', '', ''),
    @($pastBipinCommands[11], 'showtext', '', '4313', '', $allTexts[0x4313]),
    @($pastBipinCommands[12], 'scriptjump', '', '1', '', '')
)
$pastBipinCommandRows = [Collections.Generic.List[string]]::new()
$pastBipinCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
for ($index = 0; $index -lt $pastBipinCommandSpecs.Count; $index++) {
    $spec = $pastBipinCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $pastBipinCommandRows.Add((New-CutsceneCommandRow `
        'bipinScript3' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\past_bipin_commands.tsv'),
    $pastBipinCommandRows)

# INTERAC_HARDHAT_WORKER $58:$00 selects its var03 branch through a source
# jump table, grants the Shovel, and restores animation $04 before returning
# to checkabutton.
$hardhatScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$hardhatOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'checkabutton', 'disableinput', 'asm15',
    'jumptable_objectbyte', 'jumpifroomflagset', 'showtextlowindex',
    'wait', 'giveitem', 'scriptjump', 'setanimation', 'enableinput')) {
    [void]$hardhatOpcodes.Add($opcode)
}
$hardhatCommands = @(Read-AssemblyCutsceneCommands `
    $hardhatScriptPath 'hardhatWorkerSubid00Script' $hardhatOpcodes `
    'hardhatWorkerSubid01Script')
$hardhatExpected = @(
    @('initcollisions', ''),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('asm15', 'scriptHelp.turnToFaceLink'),
    @('jumptable_objectbyte', 'Interaction.var03'),
    @('jumpifroomflagset', '$20, @alreadyGaveShovel'),
    @('showtextlowindex', '<TX_1001'),
    @('wait', '30'),
    @('giveitem', 'TREASURE_SHOVEL, $00'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_1002'),
    @('scriptjump', '@enableInput'),
    @('showtextlowindex', '<TX_1000'),
    @('setanimation', '$04'),
    @('enableinput', ''),
    @('scriptjump', '@npcLoop')
)
if ($hardhatCommands.Count -ne $hardhatExpected.Count) {
    throw "hardhatWorkerSubid00Script expected 16 commands, parsed $($hardhatCommands.Count)."
}
for ($index = 0; $index -lt $hardhatExpected.Count; $index++) {
    $operands = ([string]$hardhatCommands[$index].Operands).Trim()
    if ($hardhatCommands[$index].Opcode -ne $hardhatExpected[$index][0] -or
        $operands -ne $hardhatExpected[$index][1]) {
        throw "hardhatWorkerSubid00Script command $index changed from " +
            "$($hardhatExpected[$index] -join ' ')."
    }
}
$hardhatJumpTargets = @(
    Read-AssemblyDataDirectives `
        $hardhatScriptPath 'hardhatWorkerSubid00Script' '.dw' |
        ForEach-Object { $_.Operands[0] })
if (($hardhatJumpTargets -join ',') -ne
    '@givesShovel,@doesntGiveShovel') {
    throw 'hardhatWorkerSubid00Script var03 jump table changed.'
}
$hardhatAnimation4 = Resolve-NpcAnimation 0x58 4
if ([string]::IsNullOrWhiteSpace($hardhatAnimation4)) {
    throw 'Could not resolve INTERAC_HARDHAT_WORKER animation $04.'
}
foreach ($textId in @(0x1000, 0x1001, 0x1002)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve hardhat text TX_$($textId.ToString('x4'))."
    }
}
$hardhatTreasure = $treasureObjectRecords['TREASURE_OBJECT_SHOVEL_00']
if ($null -eq $hardhatTreasure -or
    $hardhatTreasure.Treasure -ne 0x15 -or
    $hardhatTreasure.SubId -ne 0x00 -or
    $hardhatTreasure.Parameter -ne 0x00 -or
    $hardhatTreasure.TextId -ne 0x0025) {
    throw 'TREASURE_OBJECT_SHOVEL_00 no longer matches hardhatWorkerSubid00Script.'
}
$hardhatCommandSpecs = @(
    @($hardhatCommands[0],  'initcollisions', 'Hardhat', '', '', ''),
    @($hardhatCommands[1],  'checkabutton', 'Hardhat', '', '', ''),
    @($hardhatCommands[2],  'disableinput', '', '', '', ''),
    @($hardhatCommands[3],  'native', '', '', '', 'turnToFaceLink'),
    @($hardhatCommands[4],  'jumptablememory', '', '', '', 'HardhatVar03|5,12'),
    @($hardhatCommands[5],  'jumpifroomflagset', '', '20', '10', ''),
    @($hardhatCommands[6],  'showtext', '', '1001', '', $allTexts[0x1001]),
    @($hardhatCommands[7],  'wait', '', '30', '', ''),
    @($hardhatCommands[8],  'giveitem', '', '15', '00', ''),
    @($hardhatCommands[9],  'wait', '', '30', '', ''),
    @($hardhatCommands[10], 'showtext', '', '1002', '', $allTexts[0x1002]),
    @($hardhatCommands[11], 'scriptjump', '', '13', '', ''),
    @($hardhatCommands[12], 'showtext', '', '1000', '', $allTexts[0x1000]),
    @($hardhatCommands[13], 'setanimation', 'Hardhat', '04', '', $hardhatAnimation4),
    @($hardhatCommands[14], 'enableinput', '', '', '', ''),
    @($hardhatCommands[15], 'scriptjump', '', '1', '', '')
)
$hardhatCommandRows = [Collections.Generic.List[string]]::new()
$hardhatCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
for ($index = 0; $index -lt $hardhatCommandSpecs.Count; $index++) {
    $spec = $hardhatCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $hardhatCommandRows.Add((New-CutsceneCommandRow `
        'hardhatWorkerSubid00Script' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\hardhat_shovel_commands.tsv'),
    $hardhatCommandRows)

# Room 2:2f's INTERAC_POSTMAN $55:$00 runs postmanScript. Preserve its
# no-clock, declined-trade, and accepted-trade branches, including the
# Interaction.var3f animation-mode write and cardinal SPEED_200 exit.
$postmanScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$postmanOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'jumpifroomflagset', 'initcollisions', 'checkabutton', 'disableinput',
    'showtextlowindex', 'wait', 'jumpiftradeitemeq', 'scriptjump',
    'jumpiftextoptioneq', 'writeobjectbyte', 'setspeed', 'moveright',
    'movedown', 'giveitem', 'enableinput', 'scriptend')) {
    [void]$postmanOpcodes.Add($opcode)
}
$postmanCommands = @(Read-AssemblyCutsceneCommands `
    $postmanScriptPath 'postmanScript' $postmanOpcodes)
$postmanExpected = @(
    @('jumpifroomflagset', '$20, mainScripts.stubScript'),
    @('initcollisions', ''),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('showtextlowindex', '<TX_0b03'),
    @('wait', '30'),
    @('jumpiftradeitemeq', 'TRADEITEM_POE_CLOCK, @promptForTrade'),
    @('scriptjump', '@enableInput'),
    @('showtextlowindex', '<TX_0b04'),
    @('wait', '30'),
    @('jumpiftextoptioneq', '$00, @acceptedTrade'),
    @('showtextlowindex', '<TX_0b06'),
    @('enableinput', ''),
    @('scriptjump', '@npcLoop'),
    @('showtextlowindex', '<TX_0b05'),
    @('wait', '30'),
    @('writeobjectbyte', 'Interaction.var3f, $01'),
    @('setspeed', 'SPEED_200'),
    @('moveright', '$1d'),
    @('movedown', '$39'),
    @('wait', '30'),
    @('giveitem', 'TREASURE_TRADEITEM, $01'),
    @('enableinput', ''),
    @('scriptend', '')
)
if ($postmanCommands.Count -ne $postmanExpected.Count) {
    throw "postmanScript expected 24 commands, parsed $($postmanCommands.Count)."
}
for ($index = 0; $index -lt $postmanExpected.Count; $index++) {
    $operands = if ($null -eq $postmanCommands[$index].Operands) {
        ''
    } else {
        ([string]$postmanCommands[$index].Operands).Trim()
    }
    if ($postmanCommands[$index].Opcode -ne $postmanExpected[$index][0] -or
        $operands -ne $postmanExpected[$index][1]) {
        throw "postmanScript command $index changed from " +
            "$($postmanExpected[$index] -join ' ')."
    }
}
$postmanTargets = @{}
foreach ($command in $postmanCommands) {
    if (-not $postmanTargets.ContainsKey($command.Label)) {
        $postmanTargets[$command.Label] = $command.Index
    }
}
foreach ($entry in @{
    '@npcLoop' = 2
    '@promptForTrade' = 8
    '@enableInput' = 12
    '@acceptedTrade' = 14
}.GetEnumerator()) {
    if (-not $postmanTargets.ContainsKey($entry.Key) -or
        $postmanTargets[$entry.Key] -ne $entry.Value) {
        throw "postmanScript label $($entry.Key) moved from command $($entry.Value)."
    }
}

$postmanNativeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\postman.s')
$postmanWrapperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$postmanInteractionDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\interactionData.s')
if ($postmanNativeSource -notmatch
        '(?ms)^interactionCode55:.*?^@state0:\s+call @loadScriptAndInitGraphics\s+^@state1:\s+call interactionRunScript\s+jp c,interactionDelete.*?Interaction\.var3f.*?jp z,npcFaceLinkAndAnimate\s+call interactionAnimateBasedOnSpeed\s+jp objectSetPriorityRelativeToLink_withTerrainEffects' -or
    $postmanNativeSource -notmatch
        '(?ms)^@loadScriptAndInitGraphics:.*?interactionInitGraphics.*?>TX_0b00.*?interactionSetScript.*?interactionIncState.*?^@scriptTable:\s+\.dw mainScripts\.postmanScript' -or
    $postmanWrapperSource -notmatch
        '(?ms)^postmanScript:\s+loadscript scriptHelp\.postmanScript' -or
    $postmanInteractionDataSource -notmatch
        '(?m)^\s*/\* \$55 \*/ m_InteractionData \$41 \$00 \$22\s*$' -or
    $mainObjectSource -notmatch
        '(?ms)^group2Map2fObjectData:\s+obj_Interaction \$55 \$00 \$18 \$18\s+obj_End') {
    throw 'Room 2:2f INTERAC_POSTMAN native initialization, object stream, or update tail changed.'
}
$postmanAnimations = @{
    1 = Resolve-NpcAnimation 0x55 1
    2 = Resolve-NpcAnimation 0x55 2
}
if ([string]::IsNullOrWhiteSpace($postmanAnimations[1]) -or
    [string]::IsNullOrWhiteSpace($postmanAnimations[2])) {
    throw 'Could not resolve INTERAC_POSTMAN right/down movement animations $01/$02.'
}
foreach ($textId in 0x0b03..0x0b06) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Postman text TX_$($textId.ToString('x4'))."
    }
}
$postmanTreasure = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_01']
if ($null -eq $postmanTreasure -or
    $postmanTreasure.Treasure -ne 0x41 -or
    $postmanTreasure.SubId -ne 0x01 -or
    $postmanTreasure.Parameter -ne 0x01 -or
    $postmanTreasure.TextId -ne 0x005b -or
    $postmanTreasure.Graphic -ne 0x71 -or
    $roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_POE_CLOCK\s+db ; \$00' -or
    $tradeItemSource -notmatch 'TRADEITEM_STATIONERY\s+db ; \$01') {
    throw 'Postman room flag, Poe Clock requirement, or Stationery reward changed.'
}
$postmanStubPath = Join-Path $Disassembly 'scripts\common\commonScripts.s'
$postmanStubOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
[void]$postmanStubOpcodes.Add('scriptend')
$postmanStubCommands = @(Read-AssemblyCutsceneCommands `
    $postmanStubPath 'stubScript' $postmanStubOpcodes 'genericNpcScript')
if ($postmanStubCommands.Count -ne 1 -or
    $postmanStubCommands[0].Opcode -ne 'scriptend') {
    throw 'mainScripts.stubScript no longer contains one scriptend command.'
}

$postmanCommandSpecs = @(
    @($postmanCommands[0],  'jumpifroomflagset', '', '20', '24', ''),
    @($postmanCommands[1],  'initcollisions', 'Postman', '', '', ''),
    @($postmanCommands[2],  'checkabutton', 'Postman', '', '', ''),
    @($postmanCommands[3],  'disableinput', '', '', '', ''),
    @($postmanCommands[4],  'showtext', '', '0b03', '', $allTexts[0x0b03]),
    @($postmanCommands[5],  'wait', '', '30', '', ''),
    @($postmanCommands[6],  'jumpiftradeitemeq', '', '00', '8', ''),
    @($postmanCommands[7],  'scriptjump', '', '12', '', ''),
    @($postmanCommands[8],  'showtext', '', '0b04', '', $allTexts[0x0b04]),
    @($postmanCommands[9],  'wait', '', '30', '', ''),
    @($postmanCommands[10], 'jumpiftextoptioneq', '', '00', '14', ''),
    @($postmanCommands[11], 'showtext', '', '0b06', '', $allTexts[0x0b06]),
    @($postmanCommands[12], 'enableinput', '', '', '', ''),
    @($postmanCommands[13], 'scriptjump', '', '2', '', ''),
    @($postmanCommands[14], 'showtext', '', '0b05', '', $allTexts[0x0b05]),
    @($postmanCommands[15], 'wait', '', '30', '', ''),
    @($postmanCommands[16], 'writeobjectbyte', 'Postman', '3f', '01', ''),
    @($postmanCommands[17], 'setspeed', 'Postman', '50', '', ''),
    @($postmanCommands[18], 'move', 'Postman', '08', '1d', $postmanAnimations[1]),
    @($postmanCommands[19], 'move', 'Postman', '10', '39', $postmanAnimations[2]),
    @($postmanCommands[20], 'wait', '', '30', '', ''),
    @($postmanCommands[21], 'giveitem', '', '41', '01', ''),
    @($postmanCommands[22], 'enableinput', '', '', '', ''),
    @($postmanCommands[23], 'scriptend', '', '', '', ''),
    @($postmanStubCommands[0], 'scriptend', '', '', '', '')
)
$postmanCommandRows = [Collections.Generic.List[string]]::new()
$postmanCommandRows.Add($cutsceneCommandHeader)
for ($index = 0; $index -lt $postmanCommandSpecs.Count; $index++) {
    $spec = $postmanCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $postmanCommandRows.Add((New-CutsceneCommandRow `
        'postmanScript' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\postman_commands.tsv'),
    $postmanCommandRows)

# Room 2:3e's INTERAC_TOILET_HAND $5b:$00 starts hidden and runs an
# autonomous proximity script. Preserve the three packed Link approach
# positions, terminal animation-parameter waits, Stationery trade, Stink Bag
# reward, and the native object-fallen-in-hole reaction stream.
$toiletHandScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$toiletHandOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'asm15', 'initcollisions', 'writeobjectbyte', 'wait',
    'jumpifmemoryset', 'callscript', 'jumpifobjectbyteeq', 'scriptjump',
    'disableinput', 'jumpifroomflagset', 'showtextlowindex',
    'jumpiftradeitemeq', 'enableinput', 'jumpiftextoptioneq', 'giveitem',
    'checkobjectbyteeq', 'retscript')) {
    [void]$toiletHandOpcodes.Add($opcode)
}
$toiletHandCommands = @(Read-AssemblyCutsceneCommands `
    $toiletHandScriptPath 'toiletHandScript' $toiletHandOpcodes `
    'toiletHandScript_reactToObjectInHole')
$toiletHandExpected = @(
    @('asm15', 'objectSetInvisible'),
    @('initcollisions', ''),
    @('writeobjectbyte', 'Interaction.pressedAButton, $00'),
    @('wait', '1'),
    @('asm15', 'scriptHelp.toiletHand_checkLinkIsClose'),
    @('jumpifmemoryset', 'wcddb, $10, @waitForLinkToApproach'),
    @('callscript', '@retreatAndReturnFromToilet'),
    @('jumpifobjectbyteeq', 'Interaction.pressedAButton, $01, @pressedA'),
    @('asm15', 'scriptHelp.toiletHand_checkLinkIsClose'),
    @('jumpifmemoryset', 'wcddb, $10, @linkRetreated'),
    @('scriptjump', '@waitForLinkToRetreat'),
    @('callscript', '@retreatIntoToilet'),
    @('scriptjump', '@npcLoop'),
    @('asm15', 'scriptHelp.toiletHand_checkLinkIsClose'),
    @('jumpifmemoryset', 'wcddb, $10, ++'),
    @('scriptjump', '@waitForLinkToReapproach'),
    @('scriptjump', '@npcLoop'),
    @('disableinput', ''),
    @('writeobjectbyte', 'Interaction.pressedAButton, $00'),
    @('jumpifroomflagset', '$20, @alreadyGaveStinkBag'),
    @('showtextlowindex', '<TX_0b07'),
    @('jumpiftradeitemeq', 'TRADEITEM_STATIONERY, @promptForTrade'),
    @('callscript', '@retreatIntoToiletAfterDelay'),
    @('enableinput', ''),
    @('scriptjump', '@waitForLinkToReapproach'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b08'),
    @('wait', '30'),
    @('jumpiftextoptioneq', '$00, @acceptedTrade'),
    @('showtextlowindex', '<TX_0b0a'),
    @('callscript', '@retreatIntoToiletAfterDelay'),
    @('enableinput', ''),
    @('scriptjump', '@waitForLinkToReapproach'),
    @('showtextlowindex', '<TX_0b09'),
    @('callscript', '@retreatIntoToiletAfterDelay'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b0b'),
    @('callscript', '@retreatAndReturnFromToiletAfterDelay'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_0b0c'),
    @('wait', '30'),
    @('giveitem', 'TREASURE_TRADEITEM, $02'),
    @('callscript', '@retreatIntoToiletAfterDelay'),
    @('enableinput', ''),
    @('scriptjump', '@waitForLinkToReapproach'),
    @('showtextlowindex', '<TX_0b09'),
    @('callscript', '@retreatIntoToiletAfterDelay'),
    @('enableinput', ''),
    @('scriptjump', '@waitForLinkToReapproach'),
    @('wait', '30'),
    @('writeobjectbyte', 'Interaction.pressedAButton, $00'),
    @('asm15', 'objectSetVisible'),
    @('asm15', 'scriptHelp.toiletHand_disappear'),
    @('checkobjectbyteeq', 'Interaction.animParameter, $ff'),
    @('asm15', 'scriptHelp.toiletHand_comeOutOfToilet'),
    @('retscript', ''),
    @('wait', '30'),
    @('asm15', 'scriptHelp.toiletHand_retreatIntoToilet'),
    @('checkobjectbyteeq', 'Interaction.animParameter, $ff'),
    @('asm15', 'objectSetInvisible'),
    @('retscript', '')
)
if ($toiletHandCommands.Count -ne $toiletHandExpected.Count) {
    throw "toiletHandScript expected 61 commands, parsed $($toiletHandCommands.Count)."
}
for ($index = 0; $index -lt $toiletHandExpected.Count; $index++) {
    $operands = if ($null -eq $toiletHandCommands[$index].Operands) {
        ''
    } else {
        ([string]$toiletHandCommands[$index].Operands).Trim()
    }
    if ($toiletHandCommands[$index].Opcode -ne
            $toiletHandExpected[$index][0] -or
        $operands -ne $toiletHandExpected[$index][1]) {
        throw "toiletHandScript command $index changed from " +
            "$($toiletHandExpected[$index] -join ' ')."
    }
}

$toiletHandReactionOpcodes =
    [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'asm15', 'jumpifmemoryset', 'wait', 'scriptjump', 'callscript',
    'jumptable_objectbyte', 'showtextlowindex', 'scriptend',
    'checkobjectbyteeq', 'retscript')) {
    [void]$toiletHandReactionOpcodes.Add($opcode)
}
$toiletHandReactionCommands = @(Read-AssemblyCutsceneCommands `
    $toiletHandScriptPath 'toiletHandScript_reactToObjectInHole' `
    $toiletHandReactionOpcodes 'maskSalesmanScript')
$toiletHandReactionExpected = @(
    @('asm15', 'scriptHelp.toiletHand_checkVisibility'),
    @('jumpifmemoryset', 'wcddb, $07, @retreatIntoToilet'),
    @('wait', '90'),
    @('scriptjump', '@react'),
    @('asm15', 'scriptHelp.toiletHand_retreatIntoToiletIfNotAlready'),
    @('callscript', 'toiletHandScriptFunc_waitUntilFullyRetreated'),
    @('wait', '45'),
    @('jumptable_objectbyte', 'Interaction.var38'),
    @('showtextlowindex', '<TX_0b26'),
    @('wait', '30'),
    @('asm15', 'setScreenShakeCounter, 60'),
    @('asm15', 'playSound, SND_EXPLOSION'),
    @('wait', '60'),
    @('showtextlowindex', '<TX_0b25'),
    @('scriptend', ''),
    @('showtextlowindex', '<TX_0b27'),
    @('scriptend', ''),
    @('showtextlowindex', '<TX_0b28'),
    @('scriptend', ''),
    @('showtextlowindex', '<TX_0b29'),
    @('scriptend', ''),
    @('showtextlowindex', '<TX_0b2a'),
    @('scriptend', ''),
    @('showtextlowindex', '<TX_0b2b'),
    @('scriptend', ''),
    @('showtextlowindex', '<TX_0b0a'),
    @('scriptend', '')
)
if ($toiletHandReactionCommands.Count -ne
        $toiletHandReactionExpected.Count) {
    throw "toiletHandScript_reactToObjectInHole expected 27 commands, " +
        "parsed $($toiletHandReactionCommands.Count)."
}
for ($index = 0; $index -lt $toiletHandReactionExpected.Count; $index++) {
    $operands = if ($null -eq
        $toiletHandReactionCommands[$index].Operands) {
        ''
    } else {
        ([string]$toiletHandReactionCommands[$index].Operands).Trim()
    }
    if ($toiletHandReactionCommands[$index].Opcode -ne
            $toiletHandReactionExpected[$index][0] -or
        $operands -ne $toiletHandReactionExpected[$index][1]) {
        throw "toiletHandScript_reactToObjectInHole command $index changed " +
            "from $($toiletHandReactionExpected[$index] -join ' ')."
    }
}

$toiletHandNativeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\toiletHand.s')
$toiletHandHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$toiletHandInteractionDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\interactionData.s')
if ($toiletHandNativeSource -notmatch
        '(?ms)^interactionCode5b:.*?^@state0:.*?interactionSetAlwaysUpdateBit.*?clearFallDownHoleEventBuffer.*?^@state1:.*?@respondToObjectInHole.*?interactionRunScript.*?Interaction\.visible.*?interactionAnimateAsNpc.*?^@state2:.*?wTextIsActive.*?interactionRunScript.*?interactionAnimateAsNpc.*?interactionAnimate.*?^@scriptTable:\s+\.dw mainScripts\.toiletHandScript' -or
    $toiletHandNativeSource -notmatch
        '(?ms)^@objectTypeTable:.*?Item\.id,\s+\$00.*?Interaction\.id,\s+\$01.*?^@items:.*?ITEM_BOMB,\s+\$00.*?ITEM_BOMBCHUS,\s+\$01.*?ITEM_18,\s+\$02.*?ITEM_EMBER_SEED,\s+\$03.*?ITEM_SCENT_SEED,\s+\$04.*?ITEM_GALE_SEED,\s+\$05.*?ITEM_MYSTERY_SEED,\s+\$06.*?ITEM_BRACELET,\s+\$07.*?^@interactions:\s+\.db INTERAC_PUSHBLOCK,\s+\$07' -or
    $toiletHandHelperSource -notmatch
        '(?ms)^toiletHand_checkLinkIsClose:.*?^@data:.*?\.db \$57 \$68 \$67 \$00.*?^toiletHand_retreatIntoToiletIfNotAlready:.*?Interaction\.direction.*?cp \$02.*?^toiletHand_retreatIntoToilet:.*?ld a,\$02.*?^toiletHand_comeOutOfToilet:.*?ld a,\$01.*?^toiletHand_disappear:.*?ld a,\$00.*?interactionSetAnimation' -or
    $toiletHandInteractionDataSource -notmatch
        '(?ms)^; Data format:.*?b0: object gfx index.*?b1: Value for oamTileIndexBase.*?b2:.*?default animation index' -or
    $toiletHandInteractionDataSource -notmatch
        '(?m)^\s*/\* \$5b \*/ m_InteractionData \$4b \$10 \$10\s*$' -or
    $mainObjectSource -notmatch
        '(?ms)^group2Map3eObjectData:\s+obj_Interaction \$5b \$00 \$54 \$88\s+obj_End') {
    throw 'Room 2:3e INTERAC_TOILET_HAND native state, helper tables, ' +
        'interaction data, or placement changed.'
}
$toiletHandAnimations = @{
    0 = Resolve-NpcAnimation 0x5b 0
    1 = Resolve-NpcAnimation 0x5b 1
    2 = Resolve-NpcAnimation 0x5b 2
}
foreach ($animation in 0..2) {
    if ([string]::IsNullOrWhiteSpace($toiletHandAnimations[$animation])) {
        throw "Could not resolve INTERAC_TOILET_HAND animation " +
            "`$$($animation.ToString('x2'))."
    }
}
foreach ($textId in @(0x0b07, 0x0b08, 0x0b09, 0x0b0a, 0x0b0b, 0x0b0c,
    0x0b25, 0x0b26, 0x0b27, 0x0b28, 0x0b29, 0x0b2a, 0x0b2b)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Toilet Hand text TX_$($textId.ToString('x4'))."
    }
}
$toiletHandTreasure =
    $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_02']
if ($null -eq $toiletHandTreasure -or
    $toiletHandTreasure.Treasure -ne 0x41 -or
    $toiletHandTreasure.SubId -ne 0x02 -or
    $toiletHandTreasure.Parameter -ne 0x02 -or
    $toiletHandTreasure.TextId -ne 0x005c -or
    $toiletHandTreasure.Graphic -ne 0x72 -or
    $roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_STATIONERY\s+db ; \$01' -or
    $tradeItemSource -notmatch 'TRADEITEM_STINK_BAG\s+db ; \$02') {
    throw 'Toilet Hand room flag, Stationery requirement, or Stink Bag reward changed.'
}

$toiletHandCommandSpecs = @(
    @($toiletHandCommands[0],  'native', '', '', '', 'toiletHand_setInvisible'),
    @($toiletHandCommands[1],  'initcollisions', 'ToiletHand', '', '', ''),
    @($toiletHandCommands[2],  'native', '', '', '', 'toiletHand_clearPressedAButton'),
    @($toiletHandCommands[3],  'wait', '', '1', '', ''),
    @($toiletHandCommands[4],  'native', '', '', '', 'toiletHand_checkLinkIsClose'),
    @($toiletHandCommands[5],  'jumpifmemoryeqyieldonmiss', '', '00', '3', 'ToiletHandClose'),
    @($toiletHandCommands[6],  'callscript', '', '50', '', ''),
    @($toiletHandCommands[7],  'jumpifmemoryeq', '', '01', '17', 'ToiletHandPressedA'),
    @($toiletHandCommands[8],  'native', '', '', '', 'toiletHand_checkLinkIsClose'),
    @($toiletHandCommands[9],  'jumpifmemoryeqyieldonmiss', '', '00', '11', 'ToiletHandClose'),
    @($toiletHandCommands[10], 'scriptjump', '', '7', '', ''),
    @($toiletHandCommands[11], 'callscript', '', '57', '', ''),
    @($toiletHandCommands[12], 'scriptjump', '', '2', '', ''),
    @($toiletHandCommands[13], 'native', '', '', '', 'toiletHand_checkLinkIsClose'),
    @($toiletHandCommands[14], 'jumpifmemoryeqyieldonmiss', '', '00', '16', 'ToiletHandClose'),
    @($toiletHandCommands[15], 'scriptjump', '', '13', '', ''),
    @($toiletHandCommands[16], 'scriptjump', '', '2', '', ''),
    @($toiletHandCommands[17], 'disableinput', '', '', '', ''),
    @($toiletHandCommands[18], 'native', '', '', '', 'toiletHand_clearPressedAButton'),
    @($toiletHandCommands[19], 'jumpifroomflagset', '', '20', '45', ''),
    @($toiletHandCommands[20], 'showtext', '', '0b07', '', $allTexts[0x0b07]),
    @($toiletHandCommands[21], 'jumpiftradeitemeq', '', '01', '25', ''),
    @($toiletHandCommands[22], 'callscript', '', '56', '', ''),
    @($toiletHandCommands[23], 'enableinput', '', '', '', ''),
    @($toiletHandCommands[24], 'scriptjump', '', '13', '', ''),
    @($toiletHandCommands[25], 'wait', '', '30', '', ''),
    @($toiletHandCommands[26], 'showtext', '', '0b08', '', $allTexts[0x0b08]),
    @($toiletHandCommands[27], 'wait', '', '30', '', ''),
    @($toiletHandCommands[28], 'jumpiftextoptioneq', '', '00', '33', ''),
    @($toiletHandCommands[29], 'showtext', '', '0b0a', '', $allTexts[0x0b0a]),
    @($toiletHandCommands[30], 'callscript', '', '56', '', ''),
    @($toiletHandCommands[31], 'enableinput', '', '', '', ''),
    @($toiletHandCommands[32], 'scriptjump', '', '13', '', ''),
    @($toiletHandCommands[33], 'showtext', '', '0b09', '', $allTexts[0x0b09]),
    @($toiletHandCommands[34], 'callscript', '', '56', '', ''),
    @($toiletHandCommands[35], 'wait', '', '30', '', ''),
    @($toiletHandCommands[36], 'showtext', '', '0b0b', '', $allTexts[0x0b0b]),
    @($toiletHandCommands[37], 'callscript', '', '49', '', ''),
    @($toiletHandCommands[38], 'wait', '', '30', '', ''),
    @($toiletHandCommands[39], 'showtext', '', '0b0c', '', $allTexts[0x0b0c]),
    @($toiletHandCommands[40], 'wait', '', '30', '', ''),
    @($toiletHandCommands[41], 'giveitem', '', '41', '02', ''),
    @($toiletHandCommands[42], 'callscript', '', '56', '', ''),
    @($toiletHandCommands[43], 'enableinput', '', '', '', ''),
    @($toiletHandCommands[44], 'scriptjump', '', '13', '', ''),
    @($toiletHandCommands[45], 'showtext', '', '0b09', '', $allTexts[0x0b09]),
    @($toiletHandCommands[46], 'callscript', '', '56', '', ''),
    @($toiletHandCommands[47], 'enableinput', '', '', '', ''),
    @($toiletHandCommands[48], 'scriptjump', '', '13', '', ''),
    @($toiletHandCommands[49], 'wait', '', '30', '', ''),
    @($toiletHandCommands[50], 'native', '', '', '', 'toiletHand_clearPressedAButton'),
    @($toiletHandCommands[51], 'native', '', '', '', 'toiletHand_setVisible'),
    @($toiletHandCommands[52], 'setanimationcontinue', 'ToiletHand', '00', '', $toiletHandAnimations[0]),
    @($toiletHandCommands[53], 'checkmemoryeq', '', 'ff', '', 'ToiletHandAnimParameter'),
    @($toiletHandCommands[54], 'setanimationcontinue', 'ToiletHand', '01', '', $toiletHandAnimations[1]),
    @($toiletHandCommands[55], 'return', '', '', '', ''),
    @($toiletHandCommands[56], 'wait', '', '30', '', ''),
    @($toiletHandCommands[57], 'setanimationcontinue', 'ToiletHand', '02', '', $toiletHandAnimations[2]),
    @($toiletHandCommands[58], 'checkmemoryeq', '', 'ff', '', 'ToiletHandAnimParameter'),
    @($toiletHandCommands[59], 'native', '', '', '', 'toiletHand_setInvisible'),
    @($toiletHandCommands[60], 'return', '', '', '', '')
)
$toiletHandCommandRows = [Collections.Generic.List[string]]::new()
$toiletHandCommandRows.Add($cutsceneCommandHeader)
for ($index = 0; $index -lt $toiletHandCommandSpecs.Count; $index++) {
    $spec = $toiletHandCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $toiletHandCommandRows.Add((New-CutsceneCommandRow `
        'toiletHandScript' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\toilet_hand_commands.tsv'),
    $toiletHandCommandRows)

$toiletHandReactionSpecs = @(
    @($toiletHandReactionCommands[0],  'native', '', '', '', 'toiletHand_checkVisibility'),
    @($toiletHandReactionCommands[1],  'jumpifmemoryeqyieldonmiss', '', '01', '4', 'ToiletHandPriority'),
    @($toiletHandReactionCommands[2],  'wait', '', '90', '', ''),
    @($toiletHandReactionCommands[3],  'scriptjump', '', '7', '', ''),
    @($toiletHandReactionCommands[4],  'native', '', '', '', 'toiletHand_retreatIntoToiletIfNotAlready'),
    @($toiletHandReactionCommands[5],  'callscript', '', '27', '', ''),
    @($toiletHandReactionCommands[6],  'wait', '', '45', '', ''),
    @($toiletHandReactionCommands[7],  'jumptablememory', '', '', '', 'ToiletHandHoleReaction|10,8,15,17,19,21,23,25'),
    @($toiletHandReactionCommands[8],  'showtext', '', '0b26', '', $allTexts[0x0b26]),
    @($toiletHandReactionCommands[9],  'wait', '', '30', '', ''),
    @($toiletHandReactionCommands[10], 'native', '', '', '', 'toiletHand_setScreenShake60'),
    @($toiletHandReactionCommands[11], 'native', '', '', '', 'toiletHand_playExplosion'),
    @($toiletHandReactionCommands[12], 'wait', '', '60', '', ''),
    @($toiletHandReactionCommands[13], 'showtext', '', '0b25', '', $allTexts[0x0b25]),
    @($toiletHandReactionCommands[14], 'scriptend', '', '', '', ''),
    @($toiletHandReactionCommands[15], 'showtext', '', '0b27', '', $allTexts[0x0b27]),
    @($toiletHandReactionCommands[16], 'scriptend', '', '', '', ''),
    @($toiletHandReactionCommands[17], 'showtext', '', '0b28', '', $allTexts[0x0b28]),
    @($toiletHandReactionCommands[18], 'scriptend', '', '', '', ''),
    @($toiletHandReactionCommands[19], 'showtext', '', '0b29', '', $allTexts[0x0b29]),
    @($toiletHandReactionCommands[20], 'scriptend', '', '', '', ''),
    @($toiletHandReactionCommands[21], 'showtext', '', '0b2a', '', $allTexts[0x0b2a]),
    @($toiletHandReactionCommands[22], 'scriptend', '', '', '', ''),
    @($toiletHandReactionCommands[23], 'showtext', '', '0b2b', '', $allTexts[0x0b2b]),
    @($toiletHandReactionCommands[24], 'scriptend', '', '', '', ''),
    @($toiletHandReactionCommands[25], 'showtext', '', '0b0a', '', $allTexts[0x0b0a]),
    @($toiletHandReactionCommands[26], 'scriptend', '', '', '', ''),
    @($toiletHandCommands[58], 'checkmemoryeq', '', 'ff', '', 'ToiletHandAnimParameter'),
    @($toiletHandCommands[59], 'native', '', '', '', 'toiletHand_setInvisible'),
    @($toiletHandCommands[60], 'return', '', '', '', '')
)
$toiletHandReactionRows = [Collections.Generic.List[string]]::new()
$toiletHandReactionRows.Add($cutsceneCommandHeader)
for ($index = 0; $index -lt $toiletHandReactionSpecs.Count; $index++) {
    $spec = $toiletHandReactionSpecs[$index]
    $sourceCommand = $spec[0]
    $toiletHandReactionRows.Add((New-CutsceneCommandRow `
        'toiletHandScript_reactToObjectInHole' $index `
        $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\toilet_hand_reaction_commands.tsv'),
    $toiletHandReactionRows)

$toiletHandEventRows = @(
    "# group`troom`tid`tsubid`tanimation0`tanimation1`tanimation2`tcollision-y`tcollision-x`troom-flag`trequired-trade`treward-treasure`treward-parameter`treward-object`tclose-packed`tinitial-script-updates`talways-update"
    (@(
        '2', '3e', '5b', '00',
        $toiletHandAnimations[0], $toiletHandAnimations[1],
        $toiletHandAnimations[2],
        '06', '06', '20', '01', '41', '02',
        'TREASURE_OBJECT_TRADEITEM_02', '57,68,67', '1', '1'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\toilet_hand_event.tsv'),
    $toiletHandEventRows)

# Rooms 0:7c and 2:2e Poe encounters. All placed INTERAC_POE records use the
# same poeScript; Interaction.var03 selects the first, tomb, or final meeting.
# The native state-0 visibility predicates are imported with ordinary NPC
# metadata, but the selected actor becomes script-owned after initialization
# and must not be refreshed when poeScript sets the room's $40 flag.
$poeScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$poeScriptSource = Read-ImportText $poeScriptPath
$poeOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'checkabutton', 'disableinput',
    'jumptable_objectbyte', 'showtext', 'orroomflag', 'wait',
    'playsound', 'writeobjectbyte', 'asm15', 'jumpifmemoryset',
    'scriptjump', 'enableinput', 'setspeed', 'setanimation',
    'setangle', 'applyspeed', 'giveitem', 'scriptend')) {
    [void]$poeOpcodes.Add($opcode)
}
$poeCommands = @(Read-AssemblyCutsceneCommands `
    $poeScriptPath 'poeScript' $poeOpcodes)
$poeExpectedCommands = @(
    @('initcollisions', ''),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('jumptable_objectbyte', 'Interaction.var03'),
    @('showtext', 'TX_0b00'),
    @('orroomflag', '$40'),
    @('wait', '40'),
    @('playsound', 'SND_POOF'),
    @('writeobjectbyte', 'Interaction.var3e, 30'),
    @('asm15', 'poe_decCounterAndFlickerVisibility'),
    @('jumpifmemoryset', 'wcddb, $80, @end'),
    @('scriptjump', '@disappearLoop'),
    @('enableinput', ''),
    @('scriptend', ''),
    @('showtext', 'TX_0b01'),
    @('orroomflag', '$40'),
    @('wait', '30'),
    @('writeobjectbyte', 'Interaction.var3f, $01'),
    @('setspeed', 'SPEED_100'),
    @('setanimation', '$02'),
    @('setangle', '$10'),
    @('applyspeed', '$49'),
    @('setanimation', '$01'),
    @('setangle', '$08'),
    @('applyspeed', '$39'),
    @('scriptjump', '@disappear'),
    @('showtext', 'TX_0b02'),
    @('wait', '30'),
    @('giveitem', 'TREASURE_TRADEITEM, $00'),
    @('scriptjump', '@disappear')
)
if ($poeCommands.Count -ne $poeExpectedCommands.Count) {
    throw "poeScript expected 30 commands, parsed $($poeCommands.Count)."
}
for ($index = 0; $index -lt $poeExpectedCommands.Count; $index++) {
    $operands = if ($null -eq $poeCommands[$index].Operands) {
        ''
    } else {
        ([string]$poeCommands[$index].Operands).Trim()
    }
    if ($poeCommands[$index].Opcode -ne $poeExpectedCommands[$index][0] -or
        $operands -ne $poeExpectedCommands[$index][1]) {
        throw "poeScript command $index changed from $($poeExpectedCommands[$index] -join ' ')."
    }
}

$poeNativePath = Join-Path $Disassembly 'object_code\ages\interactions\poe.s'
$poeNativeSource = Read-ImportText $poeNativePath
$poeInteractionDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\interactionData.s')
if ($poeNativeSource -notmatch
        '(?ms)^@initSubid00:.*?getThisRoomFlags.*?bit 6,\(hl\).*?wPresentRoomFlags\+\$2e.*?bit 6,\(hl\).*?jr @init' -or
    $poeNativeSource -notmatch
        '(?ms)^@initSubid02:.*?getThisRoomFlags.*?ROOMFLAG_BIT_ITEM.*?bit 6,\(hl\).*?wPresentRoomFlags\+\$2e.*?bit 6,\(hl\).*?jr @init' -or
    $poeNativeSource -notmatch
        '(?ms)^@initSubid01:.*?wPresentRoomFlags\+\$7c.*?bit 6,\(hl\).*?getThisRoomFlags.*?bit 6,\(hl\).*?^@init:' -or
    $poeNativeSource -notmatch
        '(?ms)^@init:.*?@loadScriptAndInitGraphics\s+^@state1:.*?interactionRunScript.*?Interaction\.var3e.*?ret nz.*?Interaction\.var3f.*?npcFaceLinkAndAnimate.*?interactionAnimate.*?objectSetPriorityRelativeToLink_withTerrainEffects' -or
    $poeNativeSource -notmatch
        '(?ms)^@loadScriptAndInitGraphics:.*?interactionInitGraphics.*?objectMarkSolidPosition.*?interactionSetScript.*?interactionIncState.*?^@scriptTable:\s+\.dw mainScripts\.poeScript' -or
    $poeInteractionDataSource -notmatch
        '(?m)^\s*/\* \$59 \*/ m_InteractionData \$5d \$00 \$02\s*$') {
    throw 'INTERAC_POE entry predicates, initialization, or update order changed.'
}
if ($poeScriptSource -notmatch
        '(?ms)^poe_decCounterAndFlickerVisibility:\s+ld h,d\s+ld l,Interaction\.var3e\s+ld a,\(hl\)\s+or a\s+call writeFlagsTocddb\s+jr z,@setVisible\s+dec \(hl\)\s+ld a,\(wFrameCounter\)\s+rrca\s+rrca\s+jp nc,objectSetInvisible\s+^@setVisible:\s+jp objectSetVisible') {
    throw 'Poe disappearance counter or frame-mask flicker helper changed.'
}
if ($mainObjectSource -notmatch
    '(?ms)^group0Map7cObjectData:\s+obj_Interaction \$59 \$00 \$38 \$68 \$00\s+obj_Interaction \$59 \$00 \$38 \$68 \$02\s+obj_Pointer group0Map7cEnemyObjectData\s+obj_End') {
    throw 'Room 0:7c Poe object order, coordinates, or variants changed.'
}
if ($mainObjectSource -notmatch
    '(?ms)^group2Map2eObjectData:\s+obj_Interaction \$59 \$00 \$20 \$50 \$01\s+obj_End') {
    throw 'Room 2:2e Poe object order, coordinates, or variant changed.'
}

$poeAnimations = @{}
foreach ($animation in 0..3) {
    $poeAnimations[$animation] = Resolve-NpcAnimation 0x59 $animation
    if ([string]::IsNullOrWhiteSpace($poeAnimations[$animation])) {
        throw "Could not resolve INTERAC_POE animation `$$($animation.ToString('x2'))."
    }
}
foreach ($textId in 0x0b00..0x0b02) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Poe text TX_$($textId.ToString('x4'))."
    }
}
$poeTreasure = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_00']
if ($null -eq $poeTreasure -or
    $poeTreasure.Treasure -ne 0x41 -or
    $poeTreasure.SubId -ne 0x00 -or
    $poeTreasure.Parameter -ne 0x00 -or
    $poeTreasure.TextId -ne 0x005a -or
    $poeTreasure.Graphic -ne 0x70) {
    throw 'TREASURE_OBJECT_TRADEITEM_00 no longer grants the Poe Clock.'
}
$poeSpeedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$poeMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
if ($roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_POE_CLOCK\s+db ; \$00' -or
    $poeSpeedSource -notmatch 'SPEED_100\s+dsb 5 ; 0x28' -or
    $poeMusicSource -notmatch 'SND_POOF\s+db ; \$98') {
    throw 'Poe room flag, trade-item, speed, or sound constants changed.'
}

# Collapse the recognized asm15/cddb/scriptjump disappearance loop into the
# typed flicker command while retaining its source handler line and operands.
$poeCommandSpecs = @(
    @($poeCommands[0],  'initcollisions', 'Poe', '', '', ''),
    @($poeCommands[1],  'checkabutton', 'Poe', '', '', ''),
    @($poeCommands[2],  'disableinput', '', '', '', ''),
    @($poeCommands[3],  'jumptablememory', '', '', '', 'PoeVariant|4,12,24'),
    @($poeCommands[4],  'showtext', '', '0b00', '', $allTexts[0x0b00]),
    @($poeCommands[5],  'orroomflag', '', '40', '', ''),
    @($poeCommands[6],  'wait', '', '40', '', ''),
    @($poeCommands[7],  'playsound', '', '98', '', ''),
    @($poeCommands[8],  'writeobjectbyte', 'Poe', '3e', '1e', ''),
    @($poeCommands[9],  'flicker', 'Poe', '3e', '02', ''),
    @($poeCommands[12], 'enableinput', '', '', '', ''),
    @($poeCommands[13], 'scriptend', '', '', '', ''),
    @($poeCommands[14], 'showtext', '', '0b01', '', $allTexts[0x0b01]),
    @($poeCommands[15], 'orroomflag', '', '40', '', ''),
    @($poeCommands[16], 'wait', '', '30', '', ''),
    @($poeCommands[17], 'writeobjectbyte', 'Poe', '3f', '01', ''),
    @($poeCommands[18], 'setspeed', 'Poe', '28', '', ''),
    @($poeCommands[19], 'setanimation', 'Poe', '02', '', $poeAnimations[2]),
    @($poeCommands[20], 'setangle', 'Poe', '10', '', ''),
    @($poeCommands[21], 'applyspeed', 'Poe', '49', '', ''),
    @($poeCommands[22], 'setanimation', 'Poe', '01', '', $poeAnimations[1]),
    @($poeCommands[23], 'setangle', 'Poe', '08', '', ''),
    @($poeCommands[24], 'applyspeed', 'Poe', '39', '', ''),
    @($poeCommands[25], 'scriptjump', '', '6', '', ''),
    @($poeCommands[26], 'showtext', '', '0b02', '', $allTexts[0x0b02]),
    @($poeCommands[27], 'wait', '', '30', '', ''),
    @($poeCommands[28], 'giveitem', '', '41', '00', ''),
    @($poeCommands[29], 'scriptjump', '', '6', '', '')
)
$poeCommandRows = [Collections.Generic.List[string]]::new()
$poeCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
for ($index = 0; $index -lt $poeCommandSpecs.Count; $index++) {
    $spec = $poeCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $poeCommandRows.Add((New-CutsceneCommandRow `
        'poeScript' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\poe_commands.tsv'),
    $poeCommandRows)

$poeEventRows = @(
    "# group`troom`tid`tsubid`tfirst-var03`ttomb-var03`tfinal-var03`tprogress-flag`titem-flag`ttomb-group`ttomb-room`tcollision-y`tcollision-x`tdisappear-wait`tflicker-count`tflicker-address`tflicker-mask`tpoof-sound`treward-treasure`treward-parameter`treward-object`tspeed-100`tinitial-script-updates`tanimation0`tanimation1`tanimation2`tanimation3"
    (@(
        '0', '7c', '59', '00', '00', '01', '02', '40', '20', '2', '2e',
        '06', '06', '40', '30', '3e', '02', '98', '41', '00',
        'TREASURE_OBJECT_TRADEITEM_00', '28', '1',
        $poeAnimations[0], $poeAnimations[1],
        $poeAnimations[2], $poeAnimations[3]
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\poe_event.tsv'),
    $poeEventRows)
