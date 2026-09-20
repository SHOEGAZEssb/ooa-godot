$source = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$block = [regex]::Match($source, '(?ms)^interactiondc_subid0B:(.*?)^interactiondc_subid0C:').Groups[1].Value
if ($block -notmatch '(?s)@state0:.*?ld a,\$18\s+call objectSetCollideRadius.*?@state1:\s+call objectCheckCollidedWithLink_ignoreZ\s+ret nc\s+call checkLinkCollisionsEnabled\s+ret nc.*?ld a,DISABLE_LINK.*?SND_CLINK.*?objectTakePosition.*?ld a,30.*?ld bc,\$f808.*?objectCreateExclamationMark.*?@state2:.*?ld \(hl\),30.*?xor a\s+ld \(wDisabledObjects\),a.*?@state3:.*?ld \(hl\),\$07.*?interactionGetMiniScript.*?interactionSetMiniScript.*?jp z,interactionDelete.*?TILEINDEX_WARP_HOLE\s+jp breakCrackedFloor') {
    throw 'miscellaneous2.s:interactiondc_subid0B native collapse contract changed.'
}
$tiles = [regex]::Matches(([regex]::Match($block, '(?s)@listOfTilesToBreak:(.*)')).Groups[1].Value, '\$([0-9a-f]{2})') | ForEach-Object { $_.Groups[1].Value }
if ($tiles.Count -ne 28 -or $tiles[-1] -ne '00') { throw '$dc:$0b mini-script must contain 27 tiles and a terminator.' }
$bank0 = Read-ImportText (Join-Path $Disassembly 'code\bank0.s')
if ($bank0 -notmatch '(?s)breakCrackedFloor:\s+push bc\s+call setTile\s+pop bc\s+ld a,SND_RUMBLE\s+call playSound\s+call getFreeInteractionSlot\s+ret nz\s+ld \(hl\),INTERAC_FALLDOWNHOLE.*?ld \(hl\),\$80') { throw 'breakCrackedFloor silent effect contract changed.' }
$rows = [Collections.Generic.List[string]]::new()
$warpSource = Read-ImportText (Join-Path $Disassembly 'code\ages\cutscenes2.s')
if ($warpSource -notmatch '(?s)warpToMoblinKeepUnderground:\s+ld hl,@warpDestVars\s+jp setWarpDestVariables\s+@warpDestVars:\s+m_HardcodedWarpA ROOM_AGES_(?<group>[0-7])(?<room>[0-9a-f]{2}), \$(?<transition>[0-9a-f]{2}), \$(?<position>[0-9a-f]{2}), \$03') { throw 'Moblin Keep underground hardcoded warp changed.' }
$warpFields = "$($Matches.group)`t$($Matches.room)`t$($Matches.transition)`t$($Matches.position)"
$linkSource = Read-ImportText (Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
if ($linkSource -notmatch '(?s)cp TILETYPE_WARPHOLE\s+jr nz,@respawn.*?cp >ROOM_AGES_29f.*?cp <ROOM_AGES_29f.*?jpab bank1.warpToMoblinKeepUnderground') { throw 'Link Moblin Keep hole-warp gate changed.' }
if ((Read-ImportText (Join-Path $Disassembly 'constants\common\tileIndices.s')) -notmatch '\.define TILEINDEX_WARP_HOLE\s+\$48') { throw 'TILEINDEX_WARP_HOLE changed.' }
$rows.Add("# group`troom`torder`ty`tx`tradius`twait`tinterval`ttile`tclink`trumble`ttiles`tdest-group`tdest-room`tdest-transition`tdest-position")
$aliases = [Collections.Generic.List[object]]::new()
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<g>[0-7])Map(?<r>[0-9a-f]{2})ObjectData:') {
        $aliases.Add([pscustomobject]@{ Group=$Matches.g; Room=$Matches.r; Order=0 }); continue
    }
    if ($line -match '^\s+obj_End') { $aliases.Clear(); continue }
    if ($line -notmatch '^\s+obj_') { continue }
    if ($line -match '^\s+obj_Interaction \$dc \$0b \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s*$') {
        foreach ($alias in $aliases) {
            if ($alias.Group -ne '2' -or $alias.Room -ne '9f') { throw '$dc:$0b placement no longer matches Link hole-warp gate.' }
            $rows.Add("$($alias.Group)`t$($alias.Room)`t$($alias.Order)`t$($Matches.y)`t$($Matches.x)`t18`t30`t7`t48`t$($soundIds['SND_CLINK'])`t$($soundIds['SND_RUMBLE'])`t$($tiles -join ',')`t$warpFields")
        }
    }
    foreach ($alias in $aliases) { $alias.Order++ }
}
if ($rows.Count -ne 2) { throw 'Expected one $dc:$0b collapsing-floor placement.' }
Write-GeneratedTable((Join-Path $destination 'objects\collapsing_floor.tsv'), $rows)
