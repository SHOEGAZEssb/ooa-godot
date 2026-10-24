# Placements omitted by the dungeon-specific catalogs still use the global handlers.
$mainPath = Join-Path $Disassembly 'objects/ages/mainData.s'
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`tsource")
$placements = @{
    '4:05'=@{'20:02'='shared-dungeon-script'}
    '4:09'=@{'20:01'='shared-dungeon-script';'20:03'='shared-dungeon-script'}
    '4:53'=@{'24:21'='switch-trigger'}
    '4:55'=@{'20:02'='shared-dungeon-script'}
    '4:66'=@{'21:18'='restore-crystals'}
    '1:5b'=@{'82:02'='sarcophagus'}
    '1:6d'=@{'82:01'='sarcophagus'}
    '1:7c'=@{'82:02'='sarcophagus'}
    '5:c7'=@{'82:00'='sarcophagus';'82:01'='sarcophagus'}
    '0:38'=@{'dc:14'='portal-decoration';'6b:15'='link-statue'}
    '0:67'=@{'6b:15'='link-statue'}
    '1:67'=@{'6b:15'='link-statue'}
    '0:09'=@{'80:01'='moblin-flag'}
    '4:d0'=@{'dc:0f'='entry-text'}
    '5:f9'=@{'dc:0f'='entry-text'}
    '0:22'=@{'71:0d'='symmetry-barrier'}
    '0:24'=@{'71:0d'='symmetry-barrier'}
    '0:5a'=@{'39:05'='saved-monkeys'}
    '5:cf'=@{'1c:00'='farore-memory'}
}
foreach ($key in @($placements.Keys | Sort-Object)) {
    $group, $room = $key.Split(':')
    $label = "group${group}Map${room}ObjectData"
    $order = 0
    foreach ($node in Read-AssemblyMacroInvocations $mainPath $label) {
        if ($node.Name -eq 'obj_Interaction') {
            $args = @($node.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
            $id = '{0:x2}:{1:x2}' -f $args[0],$args[1]
            if ($placements[$key].ContainsKey($id)) {
                if ($args.Count -notin @(2,4)) { throw "${label}: unsupported $id operands." }
                $y = if ($args.Count -eq 4) { $args[2] } else { 0 }
                $x = if ($args.Count -eq 4) { $args[3] } else { 0 }
                $rows.Add("$group`t$room`t$order`t$($placements[$key][$id])`t$($args[0].ToString('x2'))`t$($args[1].ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`talways`tobjects/ages/mainData.s:$label")
            }
        }
        if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer')) { $order++ }
    }
}
if ($rows.Count -ne 27) { throw 'Shared interactions require seventeen controllers/decorations and nine sarcophagi.' }
Write-GeneratedTable((Join-Path $destination 'objects/shared_interactions.tsv'),$rows)

$scriptPath = Join-Path $Disassembly 'scripts/ages/dungeonScripts.s'
$ops = [Collections.Generic.HashSet[string]]::new([string[]]@(
    'stopifitemflagset','stopifroomflag80set','checknoenemies','checkmemoryeq',
    'spawnitem','playsound','createpuff','wait','settilehere','orroomflag','scriptjump','scriptend'))
foreach ($script in @('makuPathScript_spawn30Rupees', 'makuPathScript_keyFallsFromCeilingWhen1TorchLit',
        'makuPathScript_spawnDownStairsWhenEnemiesKilled', 'moonlitGrottoScript_spawnChestWhen2TorchesLit')) {
    $commands = @(Read-AssemblyCutsceneCommands $scriptPath $script $ops)
    if ($script -eq 'moonlitGrottoScript_spawnChestWhen2TorchesLit') {
        foreach ($command in Read-AssemblyCutsceneCommands $scriptPath 'spawnChestAfterPuff' $ops) {
            $command.Script = $script
            $command.Index = $commands.Count
            $commands += $command
        }
    }
    $end = @($commands | Where-Object Opcode -eq 'scriptend')
    if ($end.Count -ne 1) { throw "${script}: expected one script end." }
    $bindings = @{
        'stopifitemflagset|'=@{Opcode='jumpifroomflagset';Arg0='20';Arg1=$end[0].Index.ToString()}
        'stopifroomflag80set|'=@{Opcode='jumpifroomflagset';Arg0='80';Arg1=$end[0].Index.ToString()}
        'checknoenemies|'=@{Opcode='gate';Payload='NoEnemies'}
        'checkmemoryeq|wNumTorchesLit, $01'=@{Opcode='checkmemoryeq';Arg0='01';Payload='Torches'}
        'checkmemoryeq|wNumTorchesLit, $02'=@{Opcode='checkmemoryeq';Arg0='02';Payload='Torches'}
        'spawnitem|TREASURE_RUPEES, $0c'=@{Opcode='nativeyield';Payload='SpawnRupees'}
        'spawnitem|TREASURE_SMALL_KEY, $01'=@{Opcode='nativeyield';Payload='SpawnSmallKey'}
        'playsound|SND_SOLVEPUZZLE'=@{Opcode='playsound';Arg0=([int]$soundIds['SND_SOLVEPUZZLE']).ToString('x2')}
        'createpuff|'=@{Opcode='nativeyield';Payload='CreatePuff'}
        'orroomflag|$80'=@{Opcode='orroomflag';Arg0='80'}
        'settilehere|$45'=@{Opcode='native';Payload='SetStairs'}
        'settilehere|TILEINDEX_CHEST'=@{Opcode='native';Payload='SetChest'}
    }
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes/$script.tsv"),
        (ConvertTo-CutsceneCommandRows $commands 'Watcher' -bindings $bindings))
}

# Retain each flag operand and output bit instead of inferring adjacent room IDs.
$restorePath = Join-Path $Disassembly 'object_code/ages/interactions/dungeonEvents.s'
$instructions = @(Read-AssemblyInstructions $restorePath 'interaction21_subid18')
$rooms = @($instructions | Where-Object Code -Match '^ld l,<ROOM_AGES_4[0-9a-f]{2}$')
$bits = @($instructions | Where-Object Code -Match '^set [4-7],b$')
if ($rooms.Count -ne 4 -or $bits.Count -ne 4 -or
    @($instructions | Where-Object Code -EQ 'bit 6,(hl)').Count -ne 4 -or
    ($instructions[-4..-1].Code -join ';') -ne 'ld a,(wSwitchState);or b;ld (wSwitchState),a;jp interactionDelete') {
    throw 'interaction21_subid18: crystal-room flag to wSwitchState contract changed.'
}
$restore = [Collections.Generic.List[string]]::new()
$restore.Add("# room`tflag`tbit`tsource")
for ($i=0; $i -lt 4; $i++) {
    $room = $rooms[$i].Code.Substring($rooms[$i].Code.Length-2)
    $bit = $bits[$i].Operands[0]
    $restore.Add("$room`t40`t$bit`tobject_code/ages/interactions/dungeonEvents.s:interaction21_subid18")
}
Write-GeneratedTable((Join-Path $destination 'objects/dungeon_crystal_restore.tsv'),$restore)

$graphic = $interactionGraphics['128:1']
if (!$graphic) { throw 'Decoration $80:$01 graphics are absent.' }
$sprite = $gfxNames[$graphic.Gfx]; Copy-EnemySprite $sprite
$animation = Resolve-NpcAnimation 0x80 $graphic.DefaultAnimation
if (!$animation) { throw 'Decoration $80:$01 animation is absent.' }
Write-GeneratedTable((Join-Path $destination 'objects/moblin_flag_decoration.tsv'), @(
    "# sprite`ttile-base`tpalette`tsource-grayscale-inverted`tanimation`tsource",
    "$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$([int](Get-EnemySpriteSourceGrayscaleInverted $sprite))`t$animation`tobject_code/ages/interactions/decoration.s:interactionCode80;interactionData.s"))
if ($paletteHeaderSource -notmatch '(?ms)m_PaletteHeaderStart \$c6, PALH_c6\s*m_PaletteHeaderSpr 6, 1, (?<data>\w+)\s*m_PaletteHeaderEnd') {
    throw 'Present Link-statue palette header $c6 changed.'
}
Write-GeneratedBytes((Join-Path $destination 'objects/maku_statue_present_palette.bin'), (Read-PaletteBytes $Matches.data 4))

$gashaRows = [Collections.Generic.List[string]]::new()
$gashaRows.Add("# group`troom`tsubid`tessence-mask`tposition`tsource")
$puzzleSource = (Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/miscPuzzles.s')) -replace '(?m);[^\r\n]*',''
$tileSource = (Read-ImportText (Join-Path $Disassembly 'code/ages/roomSpecificTileChanges.s')) -replace '(?m);[^\r\n]*',''
foreach ($room in 0xe0..0xe2) {
    $subid = (0x1b + $room - 0xe0).ToString('x2')
    $label = 'tileReplacement_group0Map' + $room.ToString('x2')
    if ($tileSource -notmatch "(?ms)^${label}:.*?ld c,\`$$subid\s*(call createInteraction90|createInteraction90:)") {
        throw "${label}: missing source room-load INTERAC `$90:`$$subid allocation."
    }
    if ($puzzleSource -notmatch "(?ms)^miscPuzzles_subid${subid}:\s*ldbc \`$(?<mask>[0-9a-f]{2}), \`$(?<position>[0-9a-f]{2})") {
        throw "miscPuzzles_subid${subid}: essence mask and packed position changed."
    }
    $gashaRows.Add("0`t$($room.ToString('x2'))`t$subid`t$($Matches.mask)`t$($Matches.position)`tcode/ages/roomSpecificTileChanges.s:$label->miscPuzzles_subid$subid")
}
if ($puzzleSource -notmatch '(?ms)^miscPuzzles_subid1d:.*?ld a,TREASURE_ESSENCE\s*call checkTreasureObtained\s*pop bc\s*jr nc,@delete\s*and b\s*jr z,@delete\s*call objectSetShortPosition\s*call getThisRoomFlags\s*and ROOMFLAG_ITEM\s*jr nz,@delete\s*ld bc,TREASURE_OBJECT_GASHA_SEED_07\s*call createTreasure\s*call z,objectCopyPosition\s*@delete:\s*jp interactionDelete') {
    throw 'miscPuzzles $90:$1b-$1d reward gates/allocation/position/delete contract changed.'
}
Write-GeneratedTable((Join-Path $destination 'objects/maku_gasha_rewards.tsv'),$gashaRows)

$texts = [Collections.Generic.List[string]]::new()
$texts.Add("# id`tposition`tmessage-base64`tsource")
foreach ($id in @(0x0209,0x120a,0x200a,0x2109,0x220a)) {
    if (!$allTexts.ContainsKey($id)) { throw ('Entry text TX_{0:x4} is absent.' -f $id) }
    $position = if ($allTextPositions.ContainsKey($id)) { $allTextPositions[$id] } else { -1 }
    $source = if ($id -lt 0x2000) { 'object_code/ages/interactions/miscellaneous2.s:interactiondc_subid0F' }
        else { 'object_code/ages/interactions/companionScripts.s:companionScript_subid0d' }
    $texts.Add("$($id.ToString('x4'))`t$position`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$id])))`t$source")
}
Write-GeneratedTable((Join-Path $destination 'objects/room_entry_text.tsv'),$texts)
