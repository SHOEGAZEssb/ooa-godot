# Native unlinked first-sword content. Shared puzzles remain separate producers.
$pack=@{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
$source=Read-ImportText (Join-Path $Disassembly 'object_code/seasons/interactions/herosCaveSwordChest.s')
foreach($guard in @(
    '(?s)@state0:.*?ld a,\$01\s+ld \(de\),a\s+ld \(wcca1\),a\s+jp interactionInitGraphics',
    '(?s)@state1:.*?ld a,\(wcca2\).*?ld a,\$81.*?wDisableLinkCollisionsAndMenu.*?wDisabledObjects.*?interactionSetAlwaysUpdateBit.*?SPEED_40.*?ld \(hl\),\$20',
    '(?s)@state2:.*?interactionDecCounter1\s+jp nz,objectApplySpeed.*?TREASURE_SWORD\s+ld c,\$01\s+call giveTreasure.*?SND_GETITEM.*?TX_001c',
    '(?s)@state3:.*?wTextIsActive.*?objectSetInvisible.*?ld a,90.*?getFreeInteractionSlot\s+ret nz.*?TREASURE_OBJECT_SWORD_03.*?SNDCTRL_MEDIUM_FADEOUT',
    '(?s)@state4:.*?interactionDecCounter1.*?set 5,\(hl\).*?setWarpDestVariables.*?SND_FADEOUT.*?interactionDelete'
)) {if($source -notmatch $guard){throw "herosCaveSwordChest.s: first-sword contract changed ($guard)."}}
$warp=Read-MenuRomBytes $pack 'interactionCodec6@warpDestVariables' 5
if(($warp -join ',') -ne '128,212,0,84,131'){throw 'Hero sword return must select native $80:$d4:$00:$54:$83.'}
$rows=@("# key`tvalue`tsource",
    "move-wait`t32`therosCaveSwordChest.s:@state1",
    "return-wait`t90`therosCaveSwordChest.s:@state3",
    "move-speed`t10`therosCaveSwordChest.s:SPEED_40",
    "input-mask`t129`therosCaveSwordChest.s:@state1",
    "entry-min-y`t120`tdungeonStuff.s:@subid00",
    "entry-radius`t8`tdungeonStuff.s:@subid00",
    "warp-group`t$($warp[0] -band 7)`therosCaveSwordChest.s:@warpDestVariables",
    "warp-room`t$($warp[1])`therosCaveSwordChest.s:@warpDestVariables",
    "warp-arrival`t$($warp[2])`therosCaveSwordChest.s:@warpDestVariables",
    "warp-position`t$($warp[3])`therosCaveSwordChest.s:@warpDestVariables",
    "warp-source`t$($warp[4])`therosCaveSwordChest.s:@warpDestVariables")
Write-GeneratedTable((Join-Path $pack.Root 'heros_cave/constants.tsv'),$rows)
$texts=@("# id`tmessage-base64`tsource")
foreach($entry in @(@(0x001c,$seasonTreasureTexts[0x001c]),@(0x0200,$seasonMapTexts[0x0200]))) {
    if([string]::IsNullOrWhiteSpace($entry[1])){throw "Hero's Cave TX_$($entry[0].ToString('x4')) is unresolved."}
    $texts+="$($entry[0].ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($entry[1])))`ttext/seasons/text.yaml"
}
Write-GeneratedTable((Join-Path $pack.Root 'heros_cave/text.tsv'),$texts)
$graphics=Read-MenuRomBytes $pack 'interactionData' (0xc7*3)
$gfx=[int]$graphics[0xc6*3];$tile=[int]$graphics[0xc6*3+1];$flags=[int]$graphics[0xc6*3+2]
if($gfx -ne 0x60 -or $tile -ne 0 -or $flags -ne 0){throw 'Hero sword interaction $c6 graphics record changed.'}
$headers=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/seasons/objectGfxHeaders.s') '' 'm_ObjectGfxHeader')
$sprite=$headers[$gfx].Operands[0]
$header=Read-MenuRomBytes $pack 'objectGfxHeaderTable' (($gfx+1)*3)
$address=[int]$pack.Symbols[$sprite]
if(($header[$gfx*3] -band 0x3f) -ne ($address -shr 14) -or
    (([int]$header[$gfx*3+1] -shl 8) -bor $header[$gfx*3+2]) -ne (($address -band 0x3fff)+0x4000)){
    throw 'Hero sword selected graphics pointer differs from native source.'
}
Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
$presentation=Read-InteractionPresentation 'seasons'
$animation=Resolve-InteractionPresentation $presentation 0xc6 0
$sourceOffset=if($headers[$gfx].Operands.Count -ge 3){$headers[$gfx].Operands[2].TrimStart('$')}else{'0000'}
Write-GeneratedTable((Join-Path $pack.Root 'heros_cave/sword_visual.tsv'),@(
    "# id`tsubid`tsprite`ttile-base`tpalette`tdefault-animation`tappend-sprites`tsource-offset`tsource`tsource-inverted`tanimation",
    "c6`t00`t$sprite`t0`t0`t0`t`t$sourceOffset`tdata/seasons/interactionData.s:`$c6`t$([int](Get-ImportSpriteSourceGrayscaleInverted $sprite))`t$animation"))
