# Shared palace soldiers: import the complete native script, including calls,
# patrol-counter flag writes, and all sixteen source-selected texts.
$palaceScriptsPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
function Resolve-PalaceText([int]$textId, [Collections.Generic.HashSet[int]]$visited) {
    if (-not $allTexts.ContainsKey($textId) -or -not $visited.Add($textId)) {
        throw "Palace dialogue: missing/recursive TX_$($textId.ToString('x4'))."
    }
    $message = [string]$allTexts[$textId]
    if ($allTextPositions.ContainsKey($textId)) { $message = '\pos(' + $allTextPositions[$textId] + ')' + $message }
    while ($true) {
        $call = [regex]::Match($message, '\\call\(TX_(?<id>[0-9a-f]{4})\)')
        if (-not $call.Success) { break }
        $message = $message.Substring(0, $call.Index) +
            (Resolve-PalaceText ([Convert]::ToInt32($call.Groups['id'].Value, 16)) $visited) +
            $message.Substring($call.Index + $call.Length)
    }
    $jump = [regex]::Match($message, '\\jump\(TX_(?<id>[0-9a-f]{4})\)')
    if ($jump.Success) {
        $message = $message.Substring(0, $jump.Index) +
            (Resolve-PalaceText ([Convert]::ToInt32($jump.Groups['id'].Value, 16)) $visited)
    } elseif ($allTextFallthroughIds.ContainsKey($textId)) {
        if ($message.EndsWith('\n', [StringComparison]::Ordinal)) { $message = $message.Substring(0, $message.Length - 2) + "`n" }
        $message += Resolve-PalaceText ([int]$allTextFallthroughIds[$textId]) $visited
    }
    [void]$visited.Remove($textId)
    return $message
}
$palaceResolvedTexts = @{}
foreach ($textId in (@(0x590a, 0x2a08) + @(0x5910..0x5915))) {
    $palaceResolvedTexts[$textId] = Resolve-PalaceText $textId ([Collections.Generic.HashSet[int]]::new())
}
$palaceBootSource = Read-ImportText (Join-Path $Disassembly 'code/ages/cutscenes/miscCutscenes.s')
$palaceButtonSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/ambisPalaceButton.s')
if ($palaceButtonSource -notmatch '(?ms)^@state0:.*?ROOMFLAG_80.*?ld a,\$02.*?@state1:.*?wActiveTilePos.*?wLinkInAir.*?checkLinkVulnerable.*?ld a,45.*?ld a,\$9e.*?@state2:.*?CUTSCENE_AMBI_PASSAGE_OPEN' -or
    $palaceBootSource -notmatch '(?ms)^func_03_7493:.*?ROOM_AGES_127.*?ld \(hl\),\$3e.*?@table_7513:\s+\.db \$33 \$10\s+\.db \$34 \$00\s+\.db \$35 \$10\s+\.db \$36 \$00.*?ld \(hl\),\$1e.*?set 7,\(hl\).*?ld a,\$81.*?CUTSCENE_03') {
    throw 'ambisPalaceButton.s:$be or miscCutscenes.s:CUTSCENE_AMBI_PASSAGE_OPEN $1c contract changed.'
}
$buttonRows = [Collections.Generic.List[string]]::new()
$buttonRows.Add("# group`troom`tsubid`ty`tx`tgate-position`tangle`tsource")
$gateBytes = @([regex]::Matches(([regex]::Match($palaceBootSource, '(?ms)^@table_7513:(?<rows>(?:\s*\.db[^\r\n]+){4})')).Groups['rows'].Value, '\$(?<value>[0-9a-f]{2})') |
    ForEach-Object { [Convert]::ToInt32($_.Groups['value'].Value,16) })
foreach ($node in Read-AssemblyNodes (Join-Path $Disassembly 'objects/ages/mainData.s')) {
    if ($node.Name -ne 'obj_Interaction' -or $node.Operands.Count -ne 4 -or $node.Operands[0] -ne '$be') { continue }
    if ($node.EnclosingGlobalLabel -notmatch '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData$') {
        throw "$($node.Path):$($node.Line): palace button requires a room placement label."
    }
    $group = $Matches['group']; $room = $Matches['room']
    $subid = [Convert]::ToInt32($node.Operands[1].Substring(1),16)
    if ($subid -gt 3 -or $group -ne '1') { throw "$($node.Path):$($node.Line): unsupported palace button variant." }
    $buttonRows.Add("$group`t$room`t$($subid.ToString('x2'))`t$($node.Operands[2].Substring(1))`t$($node.Operands[3].Substring(1))`t$($gateBytes[$subid*2].ToString('x2'))`t$($gateBytes[$subid*2+1].ToString('x2'))`tobjects/ages/mainData.s:$($node.EnclosingGlobalLabel);miscCutscenes.s:@table_7513")
}
if ($buttonRows.Count -ne 5 -or $gateBytes.Count -ne 8) { throw 'Palace button/gate table must retain all four entries.' }
Write-CutsceneGeneratedTable((Join-Path $destination 'objects/palace_buttons.tsv'), $buttonRows)
if ($palaceBootSource -notmatch '(?ms)^func_03_6275:.*?fadeoutToWhite.*?ROOM_AGES_146.*?fadeinFromWhite.*?\$0f.*?TX_590a.*?wDisableScreenTransitions') {
    throw 'miscCutscenes.s:CUTSCENE_BOOTED_FROM_PALACE $14 return-room or warning contract changed.'
}
if (-not $allTexts.ContainsKey(0x590a)) { throw 'Palace capture requires TX_590a.' }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/palace_boot_text.tsv'), @(
    "# text-id`ttext-base64`tsource",
    "590a`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x590a])))`tmiscCutscenes.s:func_03_6275"
))
$palaceSoldierNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\soldier.s')
$palaceHelpers = Read-ImportText (Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$palaceBehaviours = [regex]::Match($palaceSoldierNative, '(?ms)^@behaviours:(?<rows>\s*\.db[^\r\n]+\s*\.db[^\r\n]+)')
$palaceTextTable = [regex]::Match($palaceHelpers, '(?ms)^@soldierTextIndices:(?<rows>\s*\.db[^\r\n]+\s*\.db[^\r\n]+)')
if (-not $palaceBehaviours.Success -or -not $palaceTextTable.Success -or
    $palaceSoldierNative -notmatch '(?ms)^soldierSubid0d:.*?GLOBALFLAG_FINISHEDGAME.*?ld \(hl\),\$01\s+cp \$07.*?inc \(hl\).*?@state1:.*?interactionRunScript.*?Interaction\.var3b.*?interactionRunScript.*?npcFaceLinkAndAnimate' -or
    $palaceHelpers -notmatch '(?ms)^soldierSetSpeed80AndVar3fTo01:.*?SPEED_80.*?\$01.*?^hardhatWorker_decPatrolCounter:.*?dec \(hl\)\s+jp writeFlagsTocddb') {
    throw 'soldier.s:$40:$0d initialization or patrol helper contract changed.'
}
$palaceModes = @([regex]::Matches($palaceBehaviours.Groups['rows'].Value, '\$(?<n>[0-9a-f]{2})') | ForEach-Object { [Convert]::ToInt32($_.Groups['n'].Value,16) })
$palaceTexts = @([regex]::Matches($palaceTextTable.Groups['rows'].Value, 'TX_(?<n>[0-9a-f]{4})') | ForEach-Object { [Convert]::ToInt32($_.Groups['n'].Value,16) })
if ($palaceModes.Count -ne 16 -or $palaceTexts.Count -ne 16) { throw 'Palace friendly soldier tables must have sixteen entries.' }
$palaceRows = [Collections.Generic.List[string]]::new()
$palaceRows.Add("# index`tmode`tpalette`ttext-id`tutf8-base64`tsource")
for ($i=0; $i -lt 16; $i++) {
    $text = $palaceTexts[$i]
    if (-not $allTexts.ContainsKey($text)) { throw "Palace soldier ${i}: missing TX_$($text.ToString('x4'))." }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($palaceResolvedTexts[$text]))
    $palette = if ($i -lt 7) { 1 } else { 2 }
    $palaceRows.Add("$i`t$($palaceModes[$i])`t$palette`t$($text.ToString('x4'))`t$encoded`tsoldier.s:@behaviours/scriptHelper.s:@soldierTextIndices")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'npcs\palace_friendly_soldiers.tsv'), $palaceRows)
$palaceCommands = Read-AssemblyCutsceneCommands $palaceScriptsPath 'soldierSubid0dScript' `
    ([Collections.Generic.HashSet[string]]::new([string[]]@('asm15','initcollisions','jumptable_objectbyte','checkabutton',
        'showloadedtext','scriptjump','checkmemoryeq','callscript','jumpifobjectbyteeq','jumpifmemoryset','wait','retscript',
        'disableinput','writeobjectbyte','enableinput'))) 'manOutsideD2Script'
$palaceTargets = @{}
foreach ($c in $palaceCommands) { if (-not $palaceTargets.ContainsKey($c.Label)) { $palaceTargets[$c.Label]=$c.Index } }
$palaceBindings = @{
    'showloadedtext|'=@{Opcode='showloadedtext'}
    'retscript|'=@{Opcode='return'}
    'jumptable_objectbyte|Interaction.var3b'=@{Opcode='jumptablememoryyield';Payload=('Mode|'+((@('@mode0_genericNpc','@mode1_moveClockwise','@mode2_moveCounterClockwise','@mode3_moveVertically') | ForEach-Object { $palaceTargets[$_] }) -join ','))}
    'jumpifobjectbyteeq|Interaction.pressedAButton, $01, @@turnToLinkAndShowText'=@{Opcode='jumpifmemoryeq';Arg0='01';Arg1=$palaceTargets['@@turnToLinkAndShowText'].ToString();Payload='PressedA'}
    'jumpifmemoryset|wcddb, $80, @@wait20Frames'=@{Opcode='jumpifmemoryeqyieldonmiss';Arg0='01';Arg1=$palaceTargets['@@wait20Frames'].ToString();Payload='PatrolCounterZero'}
}
foreach ($c in $palaceCommands) {
    if ($c.Opcode -eq 'callscript') { $palaceBindings["callscript|$($c.Operands)"]=@{Opcode='callscript';Arg0=$palaceTargets[$c.Operands].ToString()} }
    if ($c.Opcode -ne 'asm15') { continue }
    $handler = switch -Regex ($c.Operands) {
        '^scriptHelp\.soldierSetSpeed80AndVar3fTo01$' { 'InitializeMotion';break }
        '^scriptHelp\.soldierSetTextToShow$' { 'LoadText';break }
        '^objectUnmarkSolidPosition$' { 'UnmarkSolid';break }
        '^scriptHelp\.hardhatWorker_setPatrolDirection, \$(?<n>[0-3][0-3])$' { 'Direction:'+([Convert]::ToInt32($Matches['n'],16));break }
        '^scriptHelp\.hardhatWorker_setPatrolCounter, \$(?<n>[0-9a-f]{2})$' { 'Counter:'+([Convert]::ToInt32($Matches['n'],16));break }
        '^scriptHelp\.hardhatWorker_decPatrolCounter$' { 'DecrementCounter';break }
        '^objectApplySpeed$' { 'ApplySpeed';break }
        '^scriptHelp\.turnToFaceLink$' { 'FaceLink';break }
        '^scriptHelp\.hardhatWorker_updatePatrolAnimation$' { 'RestorePatrolAnimation';break }
        default { throw "soldierSubid0dScript:$($c.Line): unsupported asm15 '$($c.Operands)'." }
    }
    $palaceBindings["asm15|$($c.Operands)"]=@{Opcode='native';Payload=$handler}
}
$palaceCommandRows = ConvertTo-CutsceneCommandRows -commands $palaceCommands -actor 'Soldier' `
    -bindings $palaceBindings -symbols @{'wcde0'='TimeWarp';'Interaction.pressedAButton'=0x31}
Write-CutsceneGeneratedTable((Join-Path $destination 'npcs\palace_friendly_soldier_commands.tsv'), $palaceCommandRows)
$palaceAnimationRows = [Collections.Generic.List[string]]::new()
$palaceAnimationRows.Add("# index`tanimation")
foreach ($index in 0..3) { $palaceAnimationRows.Add("$index`t$(Resolve-NpcAnimation 0x40 $index)") }
Write-CutsceneGeneratedTable((Join-Path $destination 'npcs\palace_soldier_animations.tsv'), $palaceAnimationRows)

# ENEMY_AMBI_GUARD $54: retain both movement-script dispatch tables, rather
# than approximating patrols with rectangles or dropping ms_state commands.
$guardSource = Read-ImportText (Join-Path $Disassembly 'object_code\ages\enemies\ambiGuard.s')
$guardHeaders = [Collections.Generic.List[string]]::new()
$guardHeaders.Add("# subid`tspeed`tdirection`tsource")
$guardCommands = [Collections.Generic.List[string]]::new()
$guardCommands.Add("# subid`tindex`topcode`targ0`targ1`tsource")
$guardScripts = [regex]::Matches($guardSource, '(?ms)^@subid(?<subid>[0-9a-f]{2}):(?<body>.*?)(?=^@subid[0-9a-f]{2}:|^ambiGuard_attacksLink_scriptTable:|\z)')
if ($guardScripts.Count -ne 26) { throw 'ambiGuard.s movement tables must contain thirteen tossing and thirteen attacking scripts.' }
foreach ($script in $guardScripts) {
    $subid = $script.Groups['subid'].Value
    $body = $script.Groups['body'].Value
    $header = [regex]::Match($body, '^\s*\.db (?<speed>SPEED_[A-Za-z0-9]+)\s+\.db DIR_(?<direction>UP|RIGHT|DOWN|LEFT)\s+@@loop:\s*')
    if (-not $header.Success) { throw "ambiGuard.s:@subid${subid}: missing movement-script speed/direction/loop header." }
    $speed = [regex]::Match($speedSource, '(?m)^\s*'+$header.Groups['speed'].Value+'\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})')
    if (-not $speed.Success) { throw "ambiGuard.s:@subid${subid}: unresolved speed." }
    $direction = @('UP','RIGHT','DOWN','LEFT').IndexOf($header.Groups['direction'].Value)
    $guardHeaders.Add("$subid`t$($speed.Groups['value'].Value)`t$direction`tambiGuard.s:@subid${subid}")
    $index = 0
    foreach ($line in ($body.Substring($header.Length) -split "`r?`n")) {
        $code = ($line -split ';')[0].Trim()
        if (-not $code) { continue }
        $arg0 = ''; $arg1 = ''
        if ($code -match '^ms_(?<op>up|right|down|left)\s+\$(?<value>[0-9a-f]{2})$') {
            $opcode = $Matches['op']; $arg0 = $Matches['value']
        } elseif ($code -match '^ms_wait\s+(?<value>\d+)$') {
            $opcode = 'wait'; $arg0 = ([int]$Matches['value']).ToString('x2')
        } elseif ($code -match '^ms_state\s+(?<counter>\d+),\s*\$(?<state>[0-9a-f]{2})$') {
            $opcode = 'state'; $arg0 = ([int]$Matches['counter']).ToString('x2'); $arg1 = $Matches['state']
        } elseif ($code -eq 'ms_loop  @@loop' -or $code -match '^ms_loop\s+@@loop$') {
            $opcode = 'loop'; $arg0 = '00'
        } else { throw "ambiGuard.s:@subid${subid}: unsupported movement-script row '$code'." }
        $guardCommands.Add("$subid`t$index`t$opcode`t$arg0`t$arg1`tambiGuard.s:@subid${subid}:$code")
        $index++
    }
    if ($opcode -ne 'loop') { throw "ambiGuard.s:@subid${subid}: movement script has no final loop." }
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects\ambi_guard_patrols.tsv'), $guardHeaders)
Write-CutsceneGeneratedTable((Join-Path $destination 'objects\ambi_guard_movement_commands.tsv'), $guardCommands)

# The invisible $6b:$02 trigger and its dynamically allocated $37:$01 actor
# share cfc0. Preserve the called jump script and its copied-buffer loop.
$approachOps = [Collections.Generic.HashSet[string]]::new([string[]]@(
    'jumpifroomflagset','asm15','jumpifmemoryset','setcollisionradii','checkcollidedwithlink_ignorez',
    'disableinput','wait','spawninteraction','checkmemoryeq','orroomflag','enableinput','scriptend'))
$approachCommands = Read-AssemblyCutsceneCommands $palaceScriptsPath 'interaction6b_subid02Script' $approachOps 'interaction6b_subid04Script'
$approachBindings = @{
    'asm15|scriptHelp.interaction6b_checkGotBombsFromAmbi'=@{Opcode='native';Payload='CheckBombReward'}
    'jumpifmemoryset|wcddb, CPU_ZFLAG, interaction6b_stubScript'=@{Opcode='jumpifmemoryeq';Arg0='00';Arg1=($approachCommands.Count-1).ToString();Payload='BombReward'}
    'checkcollidedwithlink_ignorez|'=@{Opcode='gatecontinue';Payload='LinkCollision'}
    'asm15|scriptHelp.setLinkToState08'=@{Opcode='native';Payload='ForceState08'}
    'spawninteraction|INTERAC_RALPH, $01, $50, $b0'=@{Opcode='nativeyield';Payload='SpawnRalph'}
    'orroomflag|$40'=@{Opcode='native';Payload='FinishApproach'}
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/palace_approach_commands.tsv'),
    (ConvertTo-CutsceneCommandRows -commands $approachCommands -actor 'Trigger' -bindings $approachBindings `
        -symbols @{'wTmpcfc0.genericCutscene.state'='ApproachSignal'}))
$ralphApproachOps = [Collections.Generic.HashSet[string]]::new([string[]]@(
    'setmusic','setspeed','setanimation','wait','moveleft','writeobjectbyte','callscript','showtext','writememory',
    'resetmusic','scriptend','asm15','jumpifmemoryset','scriptjump','retscript'))
$ralphApproachCommands = @(Read-AssemblyCutsceneCommands $palaceScriptsPath 'ralphSubid01Script' $ralphApproachOps)
$jumpCommands = @(Read-AssemblyCutsceneCommands $palaceScriptsPath 'jumpAndWaitUntilLanded' $ralphApproachOps)
$jumpStart = $ralphApproachCommands.Count
foreach ($command in $jumpCommands) { $command.Index += $jumpStart }
$ralphApproachCommands += $jumpCommands
$ralphBindings = @{
    'setmusic|MUS_RALPH'=@{Opcode='nativeyield';Payload='RalphMusic'}
    'resetmusic|'=@{Opcode='nativeyield';Payload='ResetRoomMusic'}
    'callscript|jumpAndWaitUntilLanded'=@{Opcode='callscript';Arg0=$jumpStart.ToString()}
    'asm15|scriptHelp.beginJump'=@{Opcode='native';Payload='BeginJump'}
    'asm15|scriptHelp.updateGravity'=@{Opcode='native';Payload='UpdateGravity'}
    'jumpifmemoryset|wcddb, $80, @landed'=@{Opcode='jumpifmemoryeq';Arg0='01';Arg1=($ralphApproachCommands.Count-1).ToString();Payload='Landed'}
    'retscript|'=@{Opcode='return'}
}
$ralphApproachAnimations = @{}
foreach ($index in 0..3) { $ralphApproachAnimations[$index]=Resolve-NpcAnimation 0x37 $index }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/palace_approach_ralph_commands.tsv'),
    (ConvertTo-CutsceneCommandRows -commands $ralphApproachCommands -actor 'Ralph' -texts $palaceResolvedTexts -positions $allTextPositions `
        -animations $ralphApproachAnimations -bindings $ralphBindings -yieldOnJump $true `
        -symbols @{'SPEED_200'=0x50;'Interaction.var3f'=0x7f;'wTmpcfc0.genericCutscene.state'='ApproachSignal'}))
$approachGraphic = $interactionGraphics['55:1']
if ($null -eq $approachGraphic) { $approachGraphic = $interactionGraphics['55:0'] }
if ($null -eq $approachGraphic) { throw 'Ralph $37:$01 lacks source graphics.' }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/palace_approach_ralph.tsv'), @(
    "# sprite`ttile-base`tpalette`tanimation`tsource",
    "$($gfxNames[$approachGraphic.Gfx])`t$($approachGraphic.TileBase)`t$($approachGraphic.Palette)`t$(Resolve-NpcAnimation 0x37 $approachGraphic.DefaultAnimation)`tralph.s:@initSubid01"
))
