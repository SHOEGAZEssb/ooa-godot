# INTERAC_CARPENTER: retain each original script and its call/return boundaries.
# The five room $0:$25 slots bind this same catalog independently.
$carpenterSourcePath = Join-Path $Disassembly 'object_code\ages\interactions\carpenter.s'
$carpenterSource = Read-ImportText $carpenterSourcePath
$carpenterHelperPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$carpenterMainPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$carpenterHelper = Read-ImportText $carpenterHelperPath
$carpenterTextSource = Read-ImportText (Join-Path $Disassembly 'text\ages\text.yaml')
$carpenterTileSource = Read-ImportText (Join-Path $Disassembly 'code\ages\roomSpecificTileChanges.s')
$carpenterWramSource = Read-ImportText (Join-Path $Disassembly 'include\wram.s')
if ($carpenterTextSource -notmatch '(?ms)name: TX_2304.*?null_terminator: False\s+- name: TX_2305' -or
    $carpenterTileSource -notmatch '(?ms)^tileReplacement_group0Map25:.*?GLOBALFLAG_SYMMETRY_BRIDGE_BUILT.*?ld a,\$1d\s+ld hl,wRoomLayout \+ \$50\s+ldi \(hl\),a\s+ldi \(hl\),a\s+ldi \(hl\),a\s+ld a,\$1e\s+ld hl,wRoomLayout\+\$60\s+ldi \(hl\),a\s+ldi \(hl\),a\s+ld \(hl\),a' -or
    $carpenterWramSource -notmatch '(?ms)^\.nextu carpenterSearch\s+filler: ; \$cfc0\s+dsb \$10\s+; \$10 bytes reserved\s+cfd0:.*?\sdb\s+carpentersFound:.*?\sdw') {
    throw 'Carpenter text fallthrough, persistent bridge tiles, or transient WRAM layout changed.'
}
if ($carpenterSource -notmatch '(?ms)^@animationsForBridgeBuildCutsceneStart:\s*\.db \$04 \$06 \$02 \$02 \$02 \$05 \$03 \$02\s*\.db \$02 \$00' -or
    $carpenterSource -notmatch '(?ms)^@initSubid01:\s*xor a.*?objectMakeTileSolid\s+ld h,>wRoomLayout\s+ld \(hl\),\$00' -or
    $carpenterSource -notmatch '(?ms)^@runSubid01:.*?cp \$0b.*?ld a,\$3a\s+ld c,\$55\s+call setTile' -or
    $carpenterSource -notmatch '(?ms)^@initSubid00:.*?cp \$1c.*?ld a,\$05.*?ld a,\$58' -or
    $carpenterSource -notmatch '(?ms)^@runSubid:\s+call interactionAnimateAsNpc\s+ld c,\$40\s+call objectUpdateSpeedZ_paramC\s+call interactionRunScript' -or
    $carpenterHelper -notmatch '(?ms)^carpenter_buildBridgeColumn:.*?TILEINDEX_HORIZONTAL_BRIDGE_TOP.*?add \$10.*?TILEINDEX_HORIZONTAL_BRIDGE_BOTTOM.*?carpenterSearch.cfd0\s+inc \(hl\).*?SND_DOORCLOSE') {
    throw 'carpenter.s/scriptHelper.s: unsupported bridge initializer or native helper.'
}
$carpenterRoots = @('carpenter_subid00Script_body', 'carpenter_subid02Script',
    'carpenter_subid03Script', 'carpenter_subid04Script', 'carpenter_convincedToReturn',
    'carpenter_subid05Script', 'carpenter_subid06Script', 'carpenter_subid07Script',
    'carpenter_subid08Script', 'carpenter_jumpOnCutsceneStart',
    'carpenter_followBridgeProgress', 'carpenter_jump_nosound', 'carpenter_jump',
    'carpenter_talkedWhileWithBoss')
$carpenterCommands = [Collections.Generic.List[object]]::new()
$carpenterLabels = @{}
foreach ($root in $carpenterRoots) {
    $path = if ($root -eq 'carpenter_subid00Script_body') { $carpenterHelperPath } else { $carpenterMainPath }
    $carpenterLabels[$root] = $carpenterCommands.Count
    foreach ($node in @(Read-AssemblyLabelNodes $path $root)) {
        if ($node.Kind -eq 'Label') {
            $labelKey = if ($node.Name.StartsWith('@') -or $node.Name -eq '++') { "$root/$($node.Name)" } else { $node.Name }
            if ($carpenterLabels.ContainsKey($labelKey)) { throw "Duplicate carpenter label $labelKey" }
            $carpenterLabels[$labelKey] = $carpenterCommands.Count
        } elseif ($node.Kind -in @('MacroInvocation', 'Instruction')) {
            $carpenterCommands.Add([pscustomobject]@{ Root=$root; Node=$node })
        } elseif ($node.Kind -notin @('Blank', 'Comment')) {
            throw "$path`:$($node.Line): unsupported carpenter node $($node.Code)"
        }
    }
}
function Resolve-CarpenterTarget([string]$root, [string]$label) {
    $key = if ($label.StartsWith('@') -or $label -eq '++') { "$root/$label" } else { $label }
    if (!$carpenterLabels.ContainsKey($key)) { throw "Unresolved carpenter target $key" }
    return $carpenterLabels[$key].ToString()
}
function Read-CarpenterValue([string]$value) {
    switch ($value) {
        'DIR_UP' { return 0 }; 'DIR_RIGHT' { return 1 }; 'DIR_DOWN' { return 2 }; 'DIR_LEFT' { return 3 }
        'ANGLE_DOWN' { return 0x10 }
    }
    if ($value -match '^\$([0-9a-f]+)$') { return [Convert]::ToInt32($Matches[1],16) }
    if ($value -match '^\d+$') { return [int]$value }
    throw "Unsupported carpenter value '$value'"
}
function Resolve-CarpenterText([int]$id) {
    $text = [string]$allTexts[$id]
    if ($id -in @(0x2301, 0x2302)) {
        if (!$text.Contains('\call(TX_2300)')) { throw "Carpenter TX_$($id.ToString('x4')) lost its TX_2300 call." }
        $text = $text.Replace('\call(TX_2300)', [string]$allTexts[0x2300])
    }
    # TX_2304 has no terminator and already ends in the explicit \n control.
    # Physical fallthrough into TX_2305 inserts no additional text byte.
    if ($id -eq 0x2304) {
        if (!$text.EndsWith('\n')) { throw 'Carpenter TX_2304 lost its trailing newline control.' }
        $text = $text.Substring(0, $text.Length - 2) + "`n" + [string]$allTexts[0x2305]
    }
    if (!$text -or $text -match '\\(?:call|jump)\(') { throw "Unresolved carpenter TX_$($id.ToString('x4'))" }
    return $text
}
$carpenterRows = [Collections.Generic.List[string]]::new()
$carpenterRows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
foreach ($entry in $carpenterCommands) {
    $node=$entry.Node; $op=$node.Name.ToLowerInvariant(); $args=$node.OperandText
    $parts=@($args -split '\s*,\s*'); $actor=''; $a=''; $b=''; $payload=''
    switch ($op) {
        'makeabuttonsensitive' { $actor='Carpenter' }
        'checkabutton' { $actor='Carpenter' }
        'setanimation' { $actor='Carpenter'; $a=(Read-CarpenterValue $args).ToString('x2'); $payload=Resolve-NpcAnimation 0x9a (Read-CarpenterValue $args) }
        'wait' { $a=(Read-CarpenterValue $args).ToString() }
        'showtextlowindex' {
            if ($args -notmatch '^<TX_(23[0-9a-f]{2})$') { throw "Invalid carpenter text $args" }
            $op='showtext'; $a=$Matches[1]; $payload=Resolve-CarpenterText ([Convert]::ToInt32($a,16))
            if (!$payload) { throw "Missing carpenter TX_$a" }
        }
        'scriptjump' { $a=Resolve-CarpenterTarget $entry.Root $args }
        'callscript' { $a=Resolve-CarpenterTarget $entry.Root $args }
        'retscript' { $op='return' }
        'jumpiftextoptioneq' { $a=(Read-CarpenterValue $parts[0]).ToString('x2'); $b=Resolve-CarpenterTarget $entry.Root $parts[1] }
        'jumpifglobalflagset' {
            if (!$globalFlagValues.ContainsKey($parts[0])) { throw "Unknown carpenter flag $args" }
            $op='jumpifmemoryeq'; $a='01'; $b=Resolve-CarpenterTarget $entry.Root $parts[1]; $payload="Global:$($globalFlagValues[$parts[0]])"
        }
        'jumpifobjectbyteeq' {
            if ($parts[0] -ne 'Interaction.var3f') { throw "Unsupported carpenter object condition $args" }
            $op='jumpifmemoryeq'; $a=(Read-CarpenterValue $parts[1]).ToString('x2'); $b=Resolve-CarpenterTarget $entry.Root $parts[2]; $payload='Returned'
        }
        { $_ -in @('jumpifmemoryeq','checkmemoryeq','writememory') } {
            if ($parts[0] -ne 'wTmpcfc0.carpenterSearch.cfd0') { throw "Unsupported carpenter memory $args" }
            $a=(Read-CarpenterValue $parts[1]).ToString('x2'); $payload='SearchState'
            if ($op -eq 'jumpifmemoryeq') { $b=Resolve-CarpenterTarget $entry.Root $parts[2] }
        }
        'setglobalflag' { $a=$globalFlagValues[$args].ToString('x2') }
        'setspeed' { if ($args -ne 'SPEED_100') { throw "Unsupported carpenter speed $args" }; $actor='Carpenter'; $a=(Resolve-ObjectSpeed '100').ToString('x2') }
        { $_ -in @('moveup','moveright','movedown','moveleft') } {
            $direction=@('moveup','moveright','movedown','moveleft').IndexOf($op)
            $op='move'; $actor='Carpenter'; $a=($direction*8).ToString('x2'); $b=(Read-CarpenterValue $args).ToString('x2'); $payload=Resolve-NpcAnimation 0x9a $direction
        }
        'applyspeed' { $actor='Carpenter'; $a=(Read-CarpenterValue $args).ToString('x2') }
        'writeobjectbyte' {
            $actor='Carpenter'
            if ($parts[0] -eq 'Interaction.angle') { $op='setangle'; $a=(Read-CarpenterValue $parts[1]).ToString('x2') }
            elseif ($parts[0] -eq 'Interaction.state') { $a='44'; $b=(Read-CarpenterValue $parts[1]).ToString('x2') }
            else { throw "Unsupported carpenter write $args" }
        }
        'setzspeed' { if ($args -ne '-$0200') { throw "Unsupported carpenter Z speed $args" }; $op='native'; $payload='Jump' }
        'turntofacelink' { $op='native'; $payload='FaceLink' }
        'playsound' { $a=(Resolve-SoundConstant $args).ToString('x2') }
        'asm15' {
            if ($parts[0] -ne 'scriptHelp.carpenter_buildBridgeColumn' -or $parts.Count -ne 2) { throw "Unsupported carpenter helper $args" }
            # _scriptCmd_asmRetFunc restores carry regardless of playSound.
            $op='native'; $payload="BuildColumn:$((Read-CarpenterValue $parts[1]).ToString('x2'))"
        }
        'enableallobjects' { $op='setdisabledobjects'; $a='00' }
        'enablemenu' { $op='native'; $payload='EnableMenu' }
        'disableinput' { }
        'scriptend' { }
        default { throw "$($node.Path):$($node.Line): unsupported carpenter opcode $op" }
    }
    $carpenterRows.Add((New-CutsceneCommandRow 'carpenter' ($carpenterRows.Count-1) $entry.Root $node.Line $op $actor $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\carpenter_commands.tsv'), $carpenterRows)
$carpenterEntries = [Collections.Generic.List[string]]::new()
$carpenterEntries.Add('# subid`tentry`tanimation')
$carpenterStartAnimations=@(4,6,2,2,2,5,3,2,2)
foreach ($subid in @(0,1,2,3,4,5,6,7,8)) {
    $root=if ($subid -le 1) { 'carpenter_subid00Script_body' } else { 'carpenter_subid'+$subid.ToString('x2')+'Script' }
    $carpenterEntries.Add("$($subid.ToString('x2'))`t$($carpenterLabels[$root])`t$(Resolve-NpcAnimation 0x9a $carpenterStartAnimations[$subid])")
}
Write-GeneratedTable((Join-Path $destination 'objects\carpenter_scripts.tsv'), $carpenterEntries)
$carpenterData = @('# key`tvalue',
    "bridge-flag`t$($globalFlagValues['GLOBALFLAG_SYMMETRY_BRIDGE_BUILT'])",
    "flute-flag`t$($globalFlagValues['GLOBALFLAG_GOT_FLUTE'])",
    "talked-flag`t$($globalFlagValues['GLOBALFLAG_TALKED_TO_HEAD_CARPENTER'])",
    "zelda-flag`t$($globalFlagValues['GLOBALFLAG_GOT_RING_FROM_ZELDA'])",
    "gravity`t64", "jump-speed`t-512", "found-mask`t28", "state-address`t53200", "found-address`t53201",
    "blocker-position`t85", "blocker-tile`t58", "boss-complete-x`t88",
    "bridge-top`t29", "bridge-bottom`t30", "bridge-sound`t$(Resolve-SoundConstant 'SND_DOORCLOSE')")
if ($carpenterSource -notmatch 'm_HardcodedWarpA ROOM_AGES_025, \$00, \$48, \$03' -or
    $carpenterSource -notmatch '(?ms)^@runSubidFF:.*?TX_2307.*?ld b,\$10.*?ld a,-60.*?ld a,\$78.*?ld \(hl\),\$12' -or
    $carpenterSource -notmatch '(?ms)^@state2:.*?SPEED_100.*?ld c,\$10.*?ld bc,-\$200.*?SND_JUMP') {
    throw 'carpenter.s: search exit or worker departure native contract changed.'
}
$nuunObjects = Read-ImportText (Join-Path $Disassembly 'objects\ages\mainData.s')
if ($nuunObjects -notmatch '(?m)^group0Map35ObjectData:\s+obj_Interaction \$9a \$ff\s+obj_End') {
    throw 'carpenter.s: expected the $9a:$ff search exit controller in room $0:$35.'
}
$carpenterData += @("search-exit-room`t53", "departure-gravity`t16", "departure-speed`t$(Resolve-ObjectSpeed '100')",
    "jump-sound`t$(Resolve-SoundConstant 'SND_JUMP')", "return-room`t37", "return-position`t72",
    "exit-boundary`t16", "exit-push-x`t18", "exit-invincibility`t60", "mount-lock-frames`t120")
Write-GeneratedTable((Join-Path $destination 'objects\carpenter_constants.tsv'), $carpenterData)
Write-GeneratedTable((Join-Path $destination 'objects\carpenter_leave_text.tsv'), @('# text-id`tmessage',
    "2307`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((Resolve-CarpenterText 0x2307))))"))

$waterfallSource = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\specialWarp.s')
if ($waterfallSource -notmatch '(?ms)^@subid1State0:.*?SPECIALOBJECT_DIMITRI.*?ld bc,\$0810.*?^@subid1State2:.*?wDisableWarpTiles.*?m_HardcodedWarpA ROOM_AGES_5b8, \$00, \$93, \$03' -or
    $waterfallSource -notmatch '(?ms)^@subid2:.*?wDisableScreenTransitions.*?cp \$a8.*?m_HardcodedWarpB ROOM_AGES_037, \$0e, \$22, \$03' -or
    $nuunObjects -notmatch '(?ms)^group0Map37ObjectData:(?:(?!^group).)*obj_Interaction \$1f \$01 \$18 \$20' -or
    $nuunObjects -notmatch '(?ms)^group5Mapb8ObjectData:(?:(?!^group).)*obj_Interaction \$1f \$02') {
    throw 'specialWarp.s: waterfall warp placements or native operands changed.'
}
Write-GeneratedTable((Join-Path $destination 'objects\waterfall_warps.tsv'), @(
    '# group`troom`tsubid`ty`tx`tradius-y`tradius-x`tdestination-group`tdestination-room`tdestination-position`ttransition`texit-y',
    "0`t37`t01`t24`t32`t8`t16`t5`tb8`t93`t00`t168",
    "5`tb8`t02`t0`t0`t0`t0`t0`t37`t22`t0e`t168"))
