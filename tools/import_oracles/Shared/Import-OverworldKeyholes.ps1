# The contact mechanism and key sprite are common; entrance scripts remain game content.
$source = Read-ImportText (Join-Path $Disassembly 'code/interactableTiles.s')
$spriteSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/overworldKeySprite.s')
if ($source -notmatch '(?ms)^nextToOverworldKeyhole:.*?getThisRoomFlags\s+and \$80\s+ret nz.*?specialObjectCheckPushingAgainstTile.*?checkFacingBottomOfTile.*?decPushingAgainstTileCounter\s+jr z,\+\s+dec \(hl\)\s+ret nz.*?@roomsWithKeyholesTable.*?checkTreasureObtained\s+jr nc,jumpToShowInfoText.*?SND_OPENCHEST.*?set 7,\(hl\).*?ld hl,\$cfc0\s+set 0,\(hl\).*?createKeySpriteInteraction.*?INTERAC_OVERWORLD_KEY_SPRITE.*?sub TREASURE_FIRST_KEY.*?ld a,\$81\s+ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a' -or
    $spriteSource -notmatch '(?ms)^@state0:.*?ld bc,-\$200.*?interactionInitGraphics.*?^@state1:.*?ld c,\$28.*?objectUpdateSpeedZ_paramC.*?bit 7,a\s+ret nz.*?ld a,\$3c.*?^@state2:.*?interactionDecCounter1.*?interactionDelete') {
    throw 'interactableTiles.s/overworldKeySprite.s: named-key contact or sprite contract changed.'
}
foreach ($pack in @(
    @{ Name='ages'; Root=$destination; Bytes=$romBytes; Symbols=$agesSymbols; Groups=6; Count=6; Text=$allTexts[0x5109] },
    @{ Name='seasons'; Root=(Split-Path $previewDestination -Parent); Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Groups=1; Count=3; Text=$seasonTileInteractionTexts[0x5109] }
)) {
    $table = 'nextToOverworldKeyhole@roomsWithKeyholesTable'
    $pointers = Read-MenuRomBytes $pack $table ($pack.Groups*2)
    $bank = [int]$pack.Symbols[$table] -band -0x4000
    $graphics = Read-MenuRomBytes $pack 'interaction18SubidData' 15
    $presentation = Read-InteractionPresentation $pack.Name
    $gfxNames = @{}
    foreach ($line in Read-ImportLines (Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s")) {
        if ($line -match '/\* \$(?<id>[0-9a-f]{2}) \*/ m_ObjectGfxHeader (?<name>[A-Za-z0-9_]+)') {
            $gfxNames[[Convert]::ToInt32($Matches['id'],16)] = $Matches['name']
        }
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# group`troom`ttreasure`tsubid`tsprite`ttile-base`tpalette`tanimation`tsource")
    for ($group=0; $group -lt $pack.Groups; $group++) {
        $cursor = $bank + ([BitConverter]::ToUInt16($pointers,$group*2) -band 0x3fff)
        if ($cursor -ne $pack.Symbols["nextToOverworldKeyhole@group$group"]) { throw "$($pack.Name): keyhole group$group pointer changed." }
        for ($count=0; $pack.Bytes[$cursor] -ne 0; $count++) {
            if ($count -ge 256 -or $cursor+1 -ge $bank+0x4000) { throw "$($pack.Name): unterminated keyhole group$group." }
            $room=[int]$pack.Bytes[$cursor]; $treasure=[int]$pack.Bytes[$cursor+1]; $subid=$treasure-0x42
            if ($subid -lt 0 -or $subid -gt 4) { throw "$($pack.Name): keyhole group$group has invalid treasure $treasure." }
            $gfx=[int]$graphics[$subid*3]; $tile=$graphics[$subid*3+1] -band 0x7f
            $palette=($graphics[$subid*3+2] -shr 4) -band 7; $animationIndex=$graphics[$subid*3+2] -band 15
            $sprite=$gfxNames[$gfx]
            $animation=Resolve-InteractionPresentation $presentation 0x18 $animationIndex
            if ([string]::IsNullOrWhiteSpace($sprite) -or [string]::IsNullOrWhiteSpace($animation)) { throw "$($pack.Name): unresolved named-key sprite $subid." }
            $headers=Read-MenuRomBytes $pack 'objectGfxHeaderTable' (($gfx+1)*3)
            $spriteAddress=[int]$pack.Symbols[$sprite]
            if (-not $pack.Symbols.ContainsKey($sprite) -or
                ($headers[$gfx*3] -band 0x3f) -ne ($spriteAddress -shr 14) -or
                (([int]$headers[$gfx*3+1] -shl 8) -bor $headers[$gfx*3+2]) -ne (($spriteAddress -band 0x3fff)+0x4000)) {
                throw "$($pack.Name): compiled named-key graphics header differs from $sprite."
            }
            $origin="interactableTiles.s:@group$group/ROOM_$($pack.Name.ToUpperInvariant())_$((($group -shl 8)+$room).ToString('x3'))"
            $rows.Add("$group`t$($room.ToString('x2'))`t$($treasure.ToString('x2'))`t$($subid.ToString('x2'))`t$sprite`t$($tile.ToString('x2'))`t$($palette.ToString('x2'))`t$animation`t$origin")
            Copy-GeneratedFile "gfx_compressible/$($pack.Name)/$sprite.png" "gfx/$sprite.png" $pack.Root
            $cursor+=2
        }
    }
    if ($rows.Count -ne $pack.Count+1) { throw "$($pack.Name): expected$($pack.Count) named keyholes, got$($rows.Count-1)." }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/overworld_keyholes.tsv'),$rows)
    $tiles=[Collections.Generic.List[string]]::new()
    $tiles.Add("# active-collisions`ttile`tparameter`tsource")
    foreach ($stream in (Read-CollisionTableStreams $pack "data/$($pack.Name)/tile_properties/interactableTiles.s" 'interactableTilesTable' 2)) {
        foreach ($row in $stream.Rows) {
            if (($row[1] -band 15) -ne 6) { continue }
            $label=$stream.Source.Substring($stream.Source.IndexOf('interactableTilesTable')+'interactableTilesTable'.Length)
            $tiles.Add("$($stream.Mode)`t$(([int]$row[0]).ToString('x2'))`t06`tinteractableTiles.s:$label")
        }
    }
    if ($tiles.Count -ne $(if ($pack.Name -eq 'ages') {4} else {2})) { throw "$($pack.Name): named-key tile profile count changed." }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/overworld_keyhole_tiles.tsv'),$tiles)
    if ([string]::IsNullOrWhiteSpace($pack.Text)) { throw "$($pack.Name): unresolved TX_5109." }
    $message=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pack.Text))
    Write-GeneratedTable((Join-Path $pack.Root 'objects/overworld_keyhole_constants.tsv'),@(
        "# room-flag`tinformative-mask`tpush-counter`topen-sound`tno-key-text-id`tno-key-utf8-base64`tinteraction-id`tfirst-key`tinitial-speed-z`tgravity`thold-frames`tsource",
        "80`t20`t20`t108`t5109`t$message`t18`t42`t-512`t40`t60`tinteractableTiles.s:nextToOverworldKeyhole;overworldKeySprite.s"))
}
