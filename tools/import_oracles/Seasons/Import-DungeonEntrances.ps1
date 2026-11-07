# Selected Seasons inputs for the shared entry, statue-eye and portal handlers.
$pack = @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent) }
$entrySource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/dungeonStuff.s')
$eyeSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/statueEyeball.s')
foreach ($guard in @(
    '(?s)@subid00:.*?SCROLLMODE_02.*?cp \$78.*?ld a,\$08.*?initializeDungeonStuff',
    '(?s)\.else\s+cp \$07.*?ld a,\$05.*?wToggleBlocksState.*?cp \$08.*?ld a,\$01.*?wSpinnerState'
)) { if ($entrySource -notmatch $guard) { throw "dungeonStuff.s: Seasons entry contract changed ($guard)." } }
if ($eyeSource -notmatch '(?s)@subid1:.*?ld e,\$02.*?LARGE_ROOM_WIDTH-1.*?TILEINDEX_EYE_STATUE.*?dec c.*?jp interactionDelete' -or
    $eyeSource -notmatch '(?s)@spawnChild:.*?getFreeInteractionSlot.*?ret nz.*?convertShortToLongPosition_paramC.*?dec b\s+dec b') {
    throw 'statueEyeball.s: descending scan/allocation/initial-position contract changed.'
}
$entries = [Collections.Generic.List[string]]::new()
$entries.Add("# dungeon`ttext-id`tutf8-base64`tspinner-state")
$textIds = Read-MenuRomBytes $pack 'interactionCode12@dungeonTextIndices' 16
if (($textIds -join ',') -ne '0,1,2,3,4,5,6,7,8,9,10,0,0,0,0,0') { throw 'Seasons dungeon entry text indices changed.' }
for ($dungeon=0; $dungeon -lt 16; $dungeon++) {
    $id=0x0200+[int]$textIds[$dungeon]
    if (-not $seasonMapTexts.ContainsKey($id)) { throw "Unresolved Seasons dungeon-entry TX_$($id.ToString('x4'))." }
    $message=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($seasonMapTexts[$id]))
    $spinner=if($dungeon -eq 8){'01'}else{'00'}
    $entries.Add("$dungeon`t$($id.ToString('x4'))`t$message`t$spinner")
}
Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_entry_data.tsv'),$entries)

$placements=@("# group`troom`torder`tkind`tid`tsubid`ty`tx`tdungeon`tsource")
$instructions=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/seasons/mainData.s') 'group4Map1cObjectData')
if ($instructions.Count -ne 3 -or $instructions[0].Name -ne 'obj_Interaction' -or
    ($instructions[0].Operands -join ',') -ne '$12,$00,$88,$78' -or
    $instructions[1].Name -ne 'obj_Interaction' -or ($instructions[1].Operands -join ',') -ne '$e2,$01,$00,$00' -or
    $instructions[2].Name -ne 'obj_End') { throw 'Gnarled entrance $4:1c native object stream changed.' }
$placements+="4`t1c`t0`tentry`t12`t00`t88`t78`t1`tobjects/seasons/mainData.s:group4Map1cObjectData"
$placements+="4`t1c`t1`teye-spawner`te2`t01`t--`t--`t1`tobjects/seasons/mainData.s:group4Map1cObjectData"
Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_shared_placements.tsv'),$placements)

$pairs=@("# dungeon`tminiboss-room`tentrance-room")
$rooms=Read-MenuRomBytes $pack 'interactionCode7e@dungeonRoomTable' 18
if ($rooms[2] -ne 0x0b -or $rooms[3] -ne 0x15) { throw 'Seasons Dungeon $01 portal pair must be $0b/$15.' }
for ($dungeon=0;$dungeon -lt 9;$dungeon++) {
    $pairs+="$dungeon`t$($rooms[$dungeon*2].ToString('x2'))`t$($rooms[$dungeon*2+1].ToString('x2'))"
}
Write-GeneratedTable((Join-Path $pack.Root 'objects/miniboss_portal_pairs.tsv'),$pairs)

$presentation=Read-InteractionPresentation 'seasons'
$graphics=Read-MenuRomBytes $pack 'interactionData' (0xe3*3)
$headers=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/seasons/objectGfxHeaders.s') '' 'm_ObjectGfxHeader')
$offsets=Read-MenuRomBytes $pack 'interactionCodee2@lowPositionValues' 16
if (($offsets -join ',') -ne '5,8,5,9,6,9,7,9,7,8,7,7,6,7,5,7') { throw 'Statue-eye offsets changed.' }
$visuals=@("# kind`tindex`tsprite`ttile-base`tpalette`tanimation`tlow-y`tlow-x")
foreach ($id in @(0xe2,0x7e)) {
    $gfx=[int]$graphics[$id*3]; $tile=[int]$graphics[$id*3+1]; $flags=[int]$graphics[$id*3+2]
    # Gfx $00 skips dynamic loading; flag $80 selects the resident common OBJ sheet.
    $sprite=if($gfx -ne 0){$headers[$gfx].Operands[0]}
        elseif(($flags -band 0x80) -ne 0){'spr_common_sprites'}
        else{throw "Seasons INTERAC`$$($id.ToString('x2')) has unbound fixed OBJ graphics."}
    if (($id -eq 0xe2 -and ($gfx -ne 0x72 -or $tile -ne 0x1e -or $flags -ne 4)) -or
        ($id -eq 0x7e -and ($gfx -ne 0 -or $tile -ne 0x16 -or $flags -ne 0xa0))) { throw "Seasons INTERAC`$$($id.ToString('x2')) graphics contract changed." }
    $animation=Resolve-InteractionPresentation $presentation $id ($flags -band 15)
    Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
    if ($id -eq 0xe2) {
        for ($direction=0;$direction -lt 8;$direction++) {
            $visuals+="eye`t$direction`t$sprite`t$tile`t0`t$animation`t$($offsets[$direction*2])`t$($offsets[$direction*2+1])"
        }
    } else { $visuals+="portal`t0`t$sprite`t$tile`t2`t$animation`t-1`t-1" }
}
Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_shared_visuals.tsv'),$visuals)
Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_shared_constants.tsv'),@(
    "# key`tvalue", "entry-min-y`t120", "entry-radius`t8", "eye-statue-tile`t238", "eye-initial-y-offset`t-2",
    "portal-position`t87", "portal-radius`t3", "portal-spin-updates`t48", "portal-sound`t141",
    "portal-source-transition`t2", "portal-destination-transition`t0", "portal-destination-parameter`t0"
))
