# The global course runs even when INTERAC_PIRATE_SHIP is offscreen.
$shipPath = Join-Path $Disassembly 'code/ages/pirateShip.s'
$shipRows = [Collections.Generic.List[string]]::new()
$shipRows.Add("# linked`torder`troom`ttile`tdirection`tsource")
$shipDirections = @{ DIR_UP = 0; DIR_RIGHT = 1; DIR_DOWN = 2; DIR_LEFT = 3 }
foreach ($linked in 0..1) {
    $label = if ($linked -eq 0) { '@shipDirectionsPast' } else { '@shipDirectionsPresent' }
    $nodes = @(Read-AssemblyDataDirectives $shipPath $label '.db')
    $order = 0
    $terminated = $false
    foreach ($node in $nodes) {
        if ($terminated) { throw "${shipPath}:${label}: data follows the course terminator." }
        if ($node.Operands.Count -eq 1 -and $node.Operands[0] -eq '$00') {
            $terminated = $true
            continue
        }
        if ($node.Operands.Count -ne 3 -or -not $shipDirections.ContainsKey($node.Operands[2])) {
            throw "$($node.Path):$($node.Line): unsupported pirate ship course row."
        }
        $room = Convert-AssemblyInteger $node.Operands[0]
        $tile = Convert-AssemblyInteger $node.Operands[1]
        if ($room -le 0 -or $room -gt 255 -or $tile -lt 0 -or $tile -gt 255) {
            throw "$($node.Path):$($node.Line): invalid pirate ship room/tile."
        }
        $shipRows.Add(("{0}`t{1}`t{2:x2}`t{3:x2}`t{4}`tcode/ages/pirateShip.s:{5}:{6}" -f
            $linked, $order, $room, $tile, $shipDirections[$node.Operands[2]], $label, $node.Line))
        $order++
    }
    if (-not $terminated -or $order -ne $(if ($linked -eq 0) { 6 } else { 12 })) {
        throw "${shipPath}:${label}: incomplete pirate ship course."
    }
}
Write-GeneratedTable((Join-Path $destination 'world/pirate_ship_course.tsv'), $shipRows)
$shipSpeeds = @(Read-AssemblyDataDirectives $shipPath '@speedComponents' '.db')
if ($shipSpeeds.Count -ne 4) { throw "${shipPath}: expected four speed component pairs." }
$shipSpeedRows = [Collections.Generic.List[string]]::new()
$shipSpeedRows.Add("# direction`tdy`tdx`tsource")
for ($direction = 0; $direction -lt 4; $direction++) {
    $node = $shipSpeeds[$direction]
    if ($node.Operands.Count -ne 2) { throw "$($node.Path):$($node.Line): invalid speed pair." }
    $dy = Convert-AssemblyInteger $node.Operands[0]
    $dx = Convert-AssemblyInteger $node.Operands[1]
    if ($dy -notin @(0,1,255) -or $dx -notin @(0,1,255)) { throw 'Unsupported pirate ship speed.' }
    $shipSpeedRows.Add(("{0}`t{1:x2}`t{2:x2}`tcode/ages/pirateShip.s:@speedComponents:{3}" -f $direction,$dy,$dx,$node.Line))
}
Write-GeneratedTable((Join-Path $destination 'world/pirate_ship_speed.tsv'), $shipSpeedRows)

# $c2 is dynamically loaded into reserved INTERACTION $d1; its two departure
# variants are ordinary placed interactions. Keep it out of the NPC inventory.
$nativePath = Join-Path $Disassembly 'object_code/ages/interactions/pirateShip.s'
$native = Read-ImportText $nativePath
$global = Read-ImportText $shipPath
$crew = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/pirate.s')
$captain = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/pirateCaptain.s')
foreach ($guard in @(
    @($global, '(?ms)^checkLoadPirateShip:.*?TILESETFLAG_BIT_OUTDOORS.*?TILESETFLAG_BIT_UNDERWATER.*?checkIsLinkedGame.*?TILESETFLAG_BIT_PAST.*?w1ReservedInteraction1.enabled.*?INTERAC_PIRATE_SHIP'),
    @($native, '(?ms)^@subid0State0:.*?ld a,\$06.*?objectSetCollideRadius.*?^@subid0State1:.*?objectCheckCollidedWithLink_notDead.*?checkLinkVulnerable.*?setWarpDestVariables'),
    @($native, '(?ms)^@subid1And2State0Common:.*?ld a,60.*?DISABLE_ALL_BUT_INTERACTIONS \| DISABLE_LINK.*?wMenuDisabled.*?^@subid1And2State1:.*?ld \(hl\),\$80.*?TX_360c'),
    @($native, '(?ms)^@moveOffScreen:.*?SPEED_100.*?objectApplyGivenSpeed.*?interactionAnimate.*?interactionDecCounter1.*?ROOMFLAG_BIT_40.*?wDisabledObjects.*?wMenuDisabled'),
    @($crew, '(?ms)^@state1:.*?wTmpcfc0.genericCutscene.state.*?bit 0,a.*?interactionRunScript.*?npcFaceLinkAndAnimate.*?^@jump:.*?ld a,\$02.*?ld bc,-\$200.*?objectSetSpeedZ.*?^@state2:.*?ld c,\$28.*?objectUpdateSpeedZ_paramC.*?set 1,\(hl\)'),
    @($captain, '(?ms)^@state1:.*?objectPreventLinkFromPassing.*?interactionRunScript.*?interactionAnimate.*?^@state0:.*?objectSetVisible82.*?checkIsLinkedGame.*?wTilesetFlags.*?TILESETFLAG_BIT_PAST.*?pirateCaptainScript')
)) {
    if ($guard[0] -notmatch $guard[1]) { throw 'Pirate $c2/$c3/$c4 native dispatch changed; update its runtime and validation.' }
}
$graphic = $interactionGraphics['194:0']
if ($null -eq $graphic -or $gfxNames[$graphic.Gfx] -ne 'spr_pirateship') { throw 'Missing INTERAC_PIRATE_SHIP graphics.' }
$animations = @(0..3 | ForEach-Object { Resolve-NpcAnimation 0xc2 $_ })
$visualRows = @("# sprite`ttile-base`tpalette`tup`tright`tdown`tleft`tcrew-jump`tsource",
    "$($gfxNames[$graphic.Gfx])`t$($graphic.TileBase)`t$($graphic.Palette)`t$($animations -join "`t")`t$(Resolve-NpcAnimation 0xc4 2)`tobject_code/ages/interactions/pirateShip.s;pirate.s")
Write-GeneratedTable((Join-Path $destination 'world/pirate_ship_visuals.tsv'), $visualRows)
$sprite = @(Get-ChildItem $Disassembly -Directory -Filter 'gfx*' | ForEach-Object {
    Get-ChildItem $_.FullName -Recurse -File -Filter 'spr_pirateship.png'
})
if ($sprite.Count -ne 1) { throw 'Expected one pirate ship sprite sheet.' }
Copy-Item -LiteralPath $sprite[0].FullName -Destination (Join-Path $destination 'gfx/spr_pirateship.png') -Force

$placementRows = [Collections.Generic.List[string]]::new()
$placementRows.Add("# group`troom`torder`tsubid`ty`tx`tsource")
$objectPath = Join-Path $Disassembly 'objects/ages/mainData.s'
foreach ($node in Read-AssemblyMacroInvocations $objectPath '' 'obj_Interaction') {
    if ($node.Operands[0] -ne '$c2') { continue }
    if ($node.EnclosingGlobalLabel -notmatch '^group(?<group>[01])Map(?<room>[0-9a-f]{2})ObjectData$' -or $node.Operands.Count -ne 4) {
        throw "$objectPath`:$($node.Line): unsupported pirate departure placement."
    }
    $group=$Matches['group']; $room=$Matches['room']
    $order=@(Read-AssemblyMacroInvocations $objectPath $node.EnclosingGlobalLabel | Where-Object { $_.Line -lt $node.Line -and $_.Name.StartsWith('obj_') }).Count
    $placementRows.Add("$group`t$room`t$order`t$(($node.Operands[1..3] -replace '^\$','') -join "`t")`tobjects/ages/mainData.s:$($node.EnclosingGlobalLabel):$($node.Line)")
}
if ($placementRows.Count -ne 3) { throw 'Expected two pirate departure placements.' }
Write-GeneratedTable((Join-Path $destination 'world/pirate_ship_placements.tsv'), $placementRows)

$helperPath = Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$wrapper = @(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scripts.s') `
    'pirateCaptainScript' ([Collections.Generic.HashSet[string]]@('loadscript')) 'pirateSubid0Script')
if ($wrapper.Count -ne 1 -or $wrapper[0].Operands -ne 'scriptHelp.pirateCaptainScript') {
    throw 'scripts/ages/scripts.s:pirateCaptainScript must load the copied pirate captain script.'
}
$warpRows = [Collections.Generic.List[string]]::new()
$warpRows.Add("# role`tgroup`troom`tparameter`tposition`ttransition`tsource")
foreach ($spec in @(@('board',$nativePath,'interactionCodec2',0), @('unlinked',$helperPath,'pirateCaptain_warpOut',0), @('linked',$helperPath,'pirateCaptain_warpOut',1))) {
    $allWarps = @(Read-AssemblyMacroInvocations $spec[1] $spec[2] 'm_HardcodedWarpA')
    if ($allWarps.Count -ne $(if ($spec[0] -eq 'board') { 1 } else { 2 })) { throw 'Pirate warp count changed.' }
    $nodes = @($allWarps[$spec[3]])
    if ($nodes.Count -ne 1 -or $nodes[0].OperandText -notmatch '^ROOM_AGES_(?<group>[0-7])(?<room>[0-9a-f]{2}), \$(?<parameter>[0-9a-f]{2}), \$(?<position>[0-9a-f]{2}), \$(?<transition>[0-9a-f]{2})$') {
        throw "$($spec[1]):$($spec[2]): invalid pirate warp."
    }
    $relative = if ($spec[0] -eq 'board') { 'object_code/ages/interactions/pirateShip.s' } else { 'scripts/ages/scriptHelper.s' }
    $warpRows.Add("$($spec[0])`t$($Matches['group'])`t$($Matches['room'])`t$($Matches['parameter'])`t$($Matches['position'])`t$($Matches['transition'])`t${relative}:$($nodes[0].Line)")
}
Write-GeneratedTable((Join-Path $destination 'world/pirate_ship_warps.tsv'), $warpRows)

$ops = [Collections.Generic.HashSet[string]]@('initcollisions','checkabutton','disableinput','showtextdifferentforlinked','jumpiftextoptioneq','enableinput','scriptjump','xorcfc0bit','wait','showtext','checkcfc0bit','asm15','giveitem','setglobalflag','scriptend')
$commands = @(Read-AssemblyCutsceneCommands $helperPath 'pirateCaptainScript' $ops 'pirate_openEyeballCave')
$bindings = @{}
$texts = @{}
foreach ($command in $commands) {
    $args = $command.Operands
    switch ($command.Opcode) {
        'showtextdifferentforlinked' {
            $ids = @([regex]::Matches($args,'TX_(?<id>[0-9a-f]{4})') | ForEach-Object { [Convert]::ToInt32($_.Groups['id'].Value,16) })
            if ($ids.Count -ne 2) { throw 'Invalid pirate linked dialogue.' }
            $bindings["showtextdifferentforlinked|$args"] = @{Opcode='showtextdifferentforlinked';Arg0=$ids[0].ToString('x4');Arg1=$ids[1].ToString('x4');Payload=(Resolve-OceanText $ids[0] ([Collections.Generic.HashSet[int]]::new()))+"`0"+(Resolve-OceanText $ids[1] ([Collections.Generic.HashSet[int]]::new()))}
        }
        'showtext' { $id=[Convert]::ToInt32($args.Substring(3),16); $texts[$id]=Resolve-OceanText $id ([Collections.Generic.HashSet[int]]::new()) }
        'xorcfc0bit' {
            if ($args -ne '0') { throw 'Unsupported pirate jump signal.' }
            $bindings["xorcfc0bit|$args"]=@{Opcode='nativeyield';Payload='SignalJump'}
        }
        'checkcfc0bit' {
            if ($args -ne '1') { throw 'Unsupported pirate landing signal.' }
            $bindings["checkcfc0bit|$args"]=@{Opcode='gate';Payload='CrewLanded'}
        }
        'asm15' {
            if ($args -notin @('loseTreasure, TREASURE_ZORA_SCALE','pirateCaptain_warpOut')) { throw "Unsupported pirate helper $args" }
            $bindings["asm15|$args"]=@{Opcode='native';Payload=$args}
        }
        'giveitem' {
            if ($args -ne 'TREASURE_OBJECT_TOKAY_EYEBALL_00') { throw "Unsupported pirate treasure $args" }
            $reward=$treasureObjectRecords[$args]
            $bindings["giveitem|$args"]=@{Opcode='giveitem';Arg0=$reward.Treasure.ToString('x2');Arg1=$reward.SubId.ToString('x2')}
        }
        'setglobalflag' {
            if ($args -ne 'GLOBALFLAG_PIRATES_GONE') { throw "Unsupported pirate flag $args" }
            $bindings["setglobalflag|$args"]=@{Opcode='setglobalflag';Arg0=([int]$globalFlagValues[$args]).ToString('x2')}
        }
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/pirate_captain_commands.tsv'),
    (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Captain' -texts $texts -positions $allTextPositions -bindings $bindings -yieldOnJump $true))
Write-GeneratedTable((Join-Path $destination 'world/pirate_ship_departure_text.tsv'), @(
    "# text-id`tmessage-base64`tsource",
    "360c`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((Resolve-OceanText 0x360c ([Collections.Generic.HashSet[int]]::new())))))`ttext/ages/text.yaml:TX_360c"))
