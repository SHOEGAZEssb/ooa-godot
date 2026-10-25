& {
$path = Join-Path $Disassembly 'objects/ages/mainData.s'
$main = Read-ImportText $path
$placement = @(Read-AssemblyMacroInvocations $path 'group4Map9bObjectData')[2]
if ($placement.Name -ne 'obj_Interaction' -or ($placement.Operands -join ' ') -ne '$90 $1f' -or
    [regex]::Matches($main,'obj_Interaction \$90 \$1f\b').Count -ne 1) {
    throw 'INTERAC $90:$1f requires its sole placement at 4:9b, source order $02.'
}
$source = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/miscPuzzles.s')
$match = [regex]::Match($source,'(?ms)^miscPuzzles_subid1f:(?<body>.*?)^miscPuzzles_subid20:')
if (-not $match.Success) { throw 'Missing miscPuzzles_subid1f source body.' }
$body = $match.Groups['body'].Value
if ($body -notmatch '(?s)@state1:\s+call interactionDecCounter1\s+ret nz\s+ld \(hl\),(?<interval>\d+)' ) {
    throw 'INTERAC $90:$1f lost its byte counter and recheck interval.'
}
$interval = $Matches['interval']
if ($body -notmatch '(?s)call checkLinkVulnerable\s+ret nc\s+ld a,DISABLE_LINK\s+ld \(wMenuDisabled\),a\s+ld \(wDisabledObjects\),a\s+ld a,SND_ERROR\s+call playSound\s+ld e,Interaction.counter1\s+ld a,(?<delay>\d+)\s+ld \(de\),a\s+jp interactionIncState') {
    throw 'INTERAC $90:$1f vulnerability, lock or error-delay contract changed.'
}
$delay = $Matches['delay']
if ($body -notmatch '(?m)^@offsetsToCheck:\s+\.db (?<offsets>(?:\$[0-9a-f]{2} ){7}\$[0-9a-f]{2})' -or
    $body -notmatch 'bit 0,d\s+jr nz,\+\+\s+ld b,>wRoomLayout\s+ld a,\(bc\)\s+or a\s+jr nz,\+\+\s+inc hl\s+dec d') {
    throw 'INTERAC $90:$1f ordered probes or zero-layout edge skip changed.'
}
$offsetMatch = [regex]::Match($body,'(?m)^@offsetsToCheck:\s+\.db (?<offsets>(?:\$[0-9a-f]{2} ){7}\$[0-9a-f]{2})')
$offsets = $offsetMatch.Groups['offsets'].Value.Replace('$','').Replace(' ',',')
if ($body -notmatch '(?s)@state2:\s+call interactionDecCounter1\s+ret nz\s+xor a\s+ld \(wMenuDisabled\),a\s+ld \(wDisabledObjects\),a\s+ld hl,@warpDest\s+jp setWarpDestVariables\s+@warpDest:\s+m_HardcodedWarpA ROOM_AGES_(?<group>[0-7])(?<room>[0-9a-f]{2}), \$(?<transition>[0-9a-f]{2}), \$(?<position>[0-9a-f]{2}), \$(?<arrival>[0-9a-f]{2})') {
    throw 'INTERAC $90:$1f reset warp or lock-release sequence changed.'
}
Write-GeneratedTable((Join-Path $destination 'objects/puzzle_trap_resets.tsv'), @(
    "# group`troom`torder`tinterval`tdelay`toffsets`tdest-group`tdest-room`tsource-transition`tdest-position`tdest-transition`tsource",
    "4`t9b`t2`t$interval`t$delay`t$offsets`t$($Matches['group'])`t$($Matches['room'])`t$($Matches['transition'])`t$($Matches['position'])`t$($Matches['arrival'])`tobjects/ages/mainData.s:group4Map9bObjectData;object_code/ages/interactions/miscPuzzles.s:miscPuzzles_subid1f"
))
}
