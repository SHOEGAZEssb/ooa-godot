# Shared Satchel/Pegasus code reads graphics and animation pointers from its native pack.
function Read-NativeItemAnimation($pack, [string]$label, [string[]]$oam, [string]$ending) {
    $frames = [Collections.Generic.List[string]]::new()
    $starts = [Collections.Generic.List[int]]::new()
    $offset = 0
    for ($visit = 0; $visit -lt 64; $visit++) {
        $bytes = Read-MenuRomBytes $pack $label ($offset + 3)
        $duration = [int]$bytes[$offset]
        if ($duration -eq 0xff) {
            if ($ending -ne 'loop') { throw "$($pack.Name): ${label}: unexpected seed animation loop." }
            $target = $offset + 1 + ([int]$bytes[$offset + 1] - 256)
            $loop = $starts.IndexOf($target)
            if ($loop -lt 0) { throw "$($pack.Name): ${label}: seed loop targets a non-frame offset $target." }
            return ($frames -join '|') + "~$loop"
        }
        $index = [int]$bytes[$offset + 1]; $parameter = [int]$bytes[$offset + 2]
        if ($duration -eq 0 -or ($index -band 1) -ne 0 -or $index / 2 -ge $oam.Count) { throw "$($pack.Name): ${label}: invalid seed frame $visit." }
        $target = $oam[$index / 2]; $parts = [int](Read-MenuRomBytes $pack $target 1)[0]
        if ($parts -lt 1 -or $parts -gt 8) { throw "$($pack.Name): ${target}: invalid seed OAM extent." }
        $cells = Read-MenuRomBytes $pack $target (1 + $parts * 4)
        $encoded = @(for ($part = 0; $part -lt $parts; $part++) { @($cells[(1 + $part * 4)..(4 + $part * 4)] | ForEach-Object { [int]$_ }) -join ',' }) -join ';'
        $starts.Add($offset); $frames.Add("$duration,$parameter@$encoded")
        if (($ending -eq 'terminal' -and $parameter -eq 0xff) -or ($ending -eq 'dust' -and ($parameter -band 0x80) -ne 0)) { return $frames -join '|' }
        $offset += 3
    }
    throw "$($pack.Name): ${label}: seed animation has no supported ending."
}

$seedCodeSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/items/seeds.s')
$seedParentSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/itemParents/seedsParent.s')
foreach ($contract in @(
    @{ Source = $seedParentSource; Pattern = '(?ms)^parentItemCode_satchel:.*?ld c,\$00\s*ld e,\$01\s*call itemCreateChildWithID.*?jp c,clearParentItem.*?ld a,b\s*jp decNumActiveSeeds'; Name = 'Satchel child allocation/decrement order' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)^\s*ld bc,\$ffe0\s*call objectSetSpeedZ.*?@satchelPositionOffsets:\s*\.db \$fc \$00 \$fe.*?\.db \$01 \$04 \$fe.*?\.db \$05 \$00 \$fe.*?\.db \$01 \$fb \$fe'; Name = 'Satchel seed Z and directional offsets' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)call objectApplySpeed\s*ld c,\$1c\s*call itemUpdateThrowingVerticallyAndCheckHazards.*?ld a,SND_BOMB_LAND'; Name = 'Satchel flight and landing' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)@data:\s*\.db \$0a \$06 \$3a SND_LIGHTTORCH'; Name = 'Ember flame data' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)emberSeedBurn:.*?dec \(hl\).*?ld a,BREAKABLETILESOURCE_EMBER_SEED\s*call itemTryToBreakTile'; Name = 'Ember burn counter and break source' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)@scentLanded:.*?ld a,\$27.*?ld a,\$02\s*call itemSetState.*?ld a,\$01\s*call itemSetAnimation.*?^scentSeedSmell:.*?ld a,\(wFrameCounter\)\s*rrca.*?dec \(hl\).*?cp \$1e.*?ld \(wScentSeedActive\),a'; Name = 'Scent Seed landed lifetime and attraction state' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)@scentOrPegasusCollided:.*?xor a\s*ld \(de\),a\s*jr @initState3.*?\.db \$0b \$10 \$3c SND_PIRATE_BELL.*?\.db \$0b \$3c \$96 SND_SCENT_SEED'; Name = 'Scent Seed collision and landed effects' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)If it''s a mystery seed, get a random effect.*?call getRandomNumber_noPreserveVars\s*and \$03.*?add \$80\|ITEMCOLLISION_EMBER_SEED'; Name = 'Mystery Seed random-effect selection' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)@mysteryStandard:.*?call objectSetVisible82.*?\.db \$08 \$18 \$00 SND_MYSTERY_SEED'; Name = 'Mystery Seed activation data' }
)) {
    if ($contract.Source -notmatch $contract.Pattern) { throw "Shared seed source contract changed: $($contract.Name)." }
}
$galeContracts = @(
    @{ Pattern = '(?ms)cp ITEM_GALE_SEED.*?ld l,Item.zh.*?add \$f8.*?ld \(hl\),\$ff'; Name = 'stationary Satchel initialization' },
    @{ Pattern = '(?ms)^galeSeedUpdateAnimationAndCounter:.*?call galeSeedUpdateAnimation.*?call itemDecCounter1.*?cp \$14'; Name = 'counter1 expiration and flicker' },
    @{ Pattern = '(?ms)^galeSeedUpdateAnimation:.*?call itemAnimate.*?and \$03.*?inc a\s+and \$0b'; Name = 'palette cycle' },
    @{ Pattern = '(?ms)^galeSeedTryToWarpLink:.*?call galeSeedUpdateAnimation.*?wWarpsDisabled.*?wLinkObjectIndex.*?wLinkGrabState2.*?and \$f0\s+cp \$40.*?checkLinkVulnerableAndIDZero.*?objectCheckCollidedWithLink.*?ld a,\$3c.*?LINK_STATE_SPINNING_FROM_GALE'; Name = 'capture predicates and 60-update hold' },
    @{ Pattern = '(?ms)@substate2:.*?ld l,Item.zh\s+dec \(hl\)\s+dec \(hl\)\s+bit 7,\(hl\).*?CUTSCENE_IN_GALE_SEED_MENU.*?ld a,\$05\s+call openMenu.*?@substate3:.*?call itemDecCounter2'; Name = 'ascent, menu handoff and indoor byte wrap' }
)
foreach ($contract in $galeContracts) {
    if ($seedCodeSource -notmatch $contract.Pattern) { throw "ITEM_GALE_SEED `$23 changed its $($contract.Name): object_code/common/items/seeds.s." }
}
$galeMenuSource = Read-ImportText (Join-Path $Disassembly 'code\bank2.s')
if ($galeMenuSource -notmatch '(?ms)^galeSeedMenu_addOffsetToWarpIndex:.*?and \$07.*?getTreeWarpDataIndex.*?mapMenu_checkRoomVisited' -or
    $galeMenuSource -notmatch '(?ms)^getWarpTreeData:.*?pastTreeWarps.*?wTilesetFlags.*?presentTreeWarps.*?wPresentRoomFlags\+\$ac.*?ld a,\$03' -or
    $galeMenuSource -notmatch '(?ms)^galeSeedMenu_state2:.*?wWarpDestGroup.*?wWarpDestRoom.*?wWarpDestPos.*?ld a,\$05.*?wWarpTransition.*?ld a,\$03.*?wWarpTransition2.*?fadeoutToWhite') {
    throw 'MENU_GALE_SEED selection or destination handoff changed: code/bank2.s.'
}

foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Gfx = 0x78; Bounces = 3 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Gfx = 0x5c; Bounces = 0 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $seedPath = Join-Path $Disassembly 'object_code/common/items/seeds.s'
        $code = @((Invoke-AssemblySourceHost $connection 'NODES' $seedPath | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        foreach ($guard in @('ld bc,\$ffe0\s+call objectSetSpeedZ','ld a,SPEED_c0','ld c,\$1c\s+call itemUpdateThrowingVerticallyAndCheckHazards','add \$f8\s+ld \(hl\),a\s+ld l,Item.angle\s+ld \(hl\),\$ff','call getRandomNumber_noPreserveVars\s+and \$03','ld a,BREAKABLETILESOURCE_EMBER_SEED\s+call itemTryToBreakTile')) {
            if ($code -notmatch $guard) { throw "$($pack.Name): seeds.s: native seed motion/effect contract changed ($guard)." }
        }
        if (($code -match 'ld l,Item.var34\s+ld \(hl\),\$03') -ne ($pack.Bounces -eq 3)) { throw "$($pack.Name): seeds.s: native initial bounce byte changed." }
        $galeCode = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$seedPath`0galeSeedTryToWarpLink`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        $galeHelper = if ($pack.Name -eq 'ages') { 'checkLinkVulnerableAndIDZero' } else { 'checkLinkID0AndControlNormal' }
        if ($galeCode -notmatch "call $galeHelper" -or
            ($pack.Name -eq 'seasons' -and $pack.Symbols['checkLinkVulnerableAndIDZero'] -ne $pack.Symbols['checkLinkVulnerable'])) { throw "$($pack.Name): Gale's native Link identity/vulnerability helper changed." }
        $outdoorsMask = if ($galeCode -match 'ld a,\(wTilesetFlags\)\s+rrca\s+jr nc,@setSubstate3') { 1 }
            elseif ($galeCode -match 'ld a,\(wTilesetFlags\)\s+dec a\s+jr nz,@setSubstate3') { 0xff }
            else { throw "$($pack.Name): galeSeedTryToWarpLink: unsupported native outdoors predicate." }
        $parentPath = Join-Path $Disassembly 'object_code/common/itemParents/seedsParent.s'
        $parent = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$parentPath`0parentItemCode_satchel`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        foreach ($guard in @('call itemCreateChildWithID[\s\S]*jp c,clearParentItem[\s\S]*jp decNumActiveSeeds','ld a,\$03\s+ldd \(hl\),a\s+ld \(hl\),\$c0','ld hl,w1ReservedItemF\s+ld a,\$03\s+ldi \(hl\),a\s+ld \(hl\),ITEM_DUST','ld a,\(wLinkSwimmingState\)')) {
            if ($parent -notmatch $guard) { throw "$($pack.Name): seedsParent.s: Satchel allocation/consumption contract changed ($guard)." }
        }
        $rejectsUnderwater = [int]($parent -match 'call isLinkUnderwater')
        $rejectsRaft = [int]($parent -match 'ld a,\(w1Companion.id\)\s+cp SPECIALOBJECT_RAFT\s+jp z,clearParentItem')
        if ($rejectsUnderwater -ne [int]($pack.Name -eq 'ages') -or $rejectsRaft -ne $rejectsUnderwater) { throw "$($pack.Name): Satchel raft/underwater gates changed." }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/seed_satchel_parent.tsv'),@(
            '# rejects-underwater`trejects-raft`tsource',
            "$rejectsUnderwater`t$rejectsRaft`tobject_code/common/itemParents/seedsParent.s:parentItemCode_satchel@state0"))
        $timerPath = Join-Path $Disassembly 'code/bank0.s'
        $timer = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$timerPath`0decPegasusSeedCounter`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        if ($timer -notmatch 'res 7,\(hl\)[\s\S]*ld c,\$07[\s\S]*PEGASUS_RING[\s\S]*ld c,\$0f[\s\S]*decHlRef16WithCap[\s\S]*decHlRef16WithCap[\s\S]*or b') { throw "$($pack.Name): native Pegasus decrement/pulse contract changed." }
        $data = Read-MenuRomBytes $pack 'itemData' (0x25 * 3)
        $attrs = Read-MenuRomBytes $pack 'itemAttributes' (0x25 * 4)
        $effects = Read-MenuRomBytes $pack 'seedItemState1@data' 32
        $offsets = Read-MenuRomBytes $pack 'itemCode24@satchelPositionOffsets' 12
        if (($offsets -join ',') -ne '252,0,254,1,4,254,5,0,254,1,251,254') { throw "$($pack.Name): Satchel directional XYZ offsets changed." }
        $speeds = Read-MenuRomBytes $pack 'updateLinkSpeed_withParam@speedTable' 8
        if (($speeds -join ',') -ne '40,0,30,20,40,45,30,60' -or ($data[(0x1a * 3)..(0x1a * 3 + 2)] -join ',') -ne '0,22,11') { throw "$($pack.Name): native Pegasus terrain speed/dust graphics changed." }
        $path = Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s"
        $headers = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$path`0objectGfxHeaderTable`0m_ObjectGfxHeader" | ConvertFrom-Json) | Where-Object IsActive)
        $gfx = Read-MenuRomBytes $pack 'objectGfxHeaderTable' (($pack.Gfx + 1) * 3)
        if ($headers[$pack.Gfx].Operands.Count -ne 1 -or $headers[$pack.Gfx].Operands[0] -ne 'spr_common_items' -or
            ($gfx[$pack.Gfx * 3] -band 0x3f) -ne [int][Math]::Floor($pack.Symbols['spr_common_items'] / 0x4000) -or
            (([int]$gfx[$pack.Gfx * 3 + 1] -shl 8) -bor $gfx[$pack.Gfx * 3 + 2]) -ne (($pack.Symbols['spr_common_items'] -band 0x3fff) + 0x4000)) { throw "$($pack.Name): native seed graphics header changed." }
        $path = Join-Path $Disassembly "data/$($pack.Name)/specialObjectAnimationData.s"
        $poses = Read-NativeObjectPointerLabels $pack $path 'specialObject09AnimationDataPointers' $connection
        if ($pack.Symbols['specialObject00AnimationDataPointers'] -ne $pack.Symbols['specialObject09AnimationDataPointers'] -or $poses.Count -ne 56) { throw "$($pack.Name): native Link pose aliases changed." }
        $pose = Read-MenuRomBytes $pack $poses[0x21] 6
        if (($pose -join ',') -ne '8,176,6,127,176,134') { throw "$($pack.Name): native Satchel Link pose timing changed." }
        $path = Join-Path $Disassembly 'data/itemAnimations.s'
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add('# parent-item`tseed-item`ttreasure-id`tsprite`ttile-base`tpalette`tcollision`tradius-y`tradius-x`tdamage`tinitial-z`tspeed-z`tgravity`tspeed-raw`tup-y`tup-x`tright-y`tright-x`tdown-y`tdown-x`tleft-y`tleft-x`tlink-frames`tflame-sprite`tflame-tile-base`tflame-oam-flags`tflame-counter`tlanding-sound`tflame-sound`tanimation`tcollision-effect-tile-base`tcollision-effect-oam-flags`tcollision-effect-counter`tcollision-effect-sound`teffect-animation`tinitial-bounces`tgale-outdoors-mask`tsource')
        foreach ($id in @(0x20,0x21,0x24,0x22,0x23)) {
            $hex = $id.ToString('x2'); $index = $id - 0x20
            $animationAlias = if ($id -eq 0x20) { 'item20Animations' } elseif ($id -eq 0x23) { 'item26Animations' } else { 'item24Animations' }
            $oamAlias = if ($id -in @(0x22,0x24)) { 'item26OamDataPointers' } else { "item${hex}OamDataPointers" }
            if ($pack.Symbols["item${hex}Animations"] -ne $pack.Symbols[$animationAlias] -or $pack.Symbols["item${hex}OamDataPointers"] -ne $pack.Symbols[$oamAlias]) { throw "$($pack.Name): ITEM `$${hex}: animation/OAM aliases changed." }
            $animations = Read-NativeObjectPointerLabels $pack $path $animationAlias $connection
            $oam = Read-NativeObjectPointerLabels $pack $path $oamAlias $connection
            $ending = if ($id -in @(0x20,0x23)) { 'loop' } else { 'terminal' }
            $flight = Read-NativeItemAnimation $pack $animations[0] $oam $ending
            if ($id -in @(0x22,0x24)) { $flight += '~4' }
            $landed = $flight
            if ($id -eq 0x21) { $landed = Read-NativeItemAnimation $pack $animations[1] $oam 'loop' }
            $at = $index * 4; $landAt = if ($id -eq 0x21) { 28 } elseif ($id -eq 0x23) { 20 } else { $at }
            if ($data[$id * 3] -ne $pack.Gfx -or $attrs[$id * 4 + 1] -ne 0x44 -or $effects[$at + 3] -ne $soundIds[@('SND_LIGHTTORCH','SND_PIRATE_BELL','SND_LIGHTTORCH','SND_GALE_SEED','SND_MYSTERY_SEED')[$index]]) { throw "$($pack.Name): ITEM `$${hex}: native seed attributes/effect changed." }
            $z = -2; $speed = '1e'; $xy = @(-4,0,1,4,5,0,1,-5); $frames = [int]$pose[0]
            if ($id -eq 0x22) { $frames = 0 }
            if ($id -eq 0x23) { $z = -8; $speed = '00'; $xy = @(0,0,0,0,0,0,0,0) }
            $fields = @('19',$hex,$hex,'spr_common_items',$data[$id * 3 + 1].ToString('x2'),$data[$id * 3 + 2].ToString('x2'),$attrs[$id * 4].ToString('x2'),4,4,$attrs[$id * 4 + 2].ToString('x2'),$z,-32,28,$speed) + $xy + @(
                $frames,'spr_common_sprites',$effects[$landAt + 1].ToString('x2'),$effects[$landAt].ToString('x2'),[int]$effects[$landAt + 2],$soundIds['SND_BOMB_LAND'].ToString('x2'),$effects[$landAt + 3].ToString('x2'),$flight,
                $effects[$at + 1].ToString('x2'),$effects[$at].ToString('x2'),[int]$effects[$at + 2],$effects[$at + 3].ToString('x2'),$landed,$pack.Bounces,$outdoorsMask.ToString('x2'),
                "object_code/common/items/seeds.s:itemCode$hex/seedItemState1@data;data/$($pack.Name)/itemData.s;data/$($pack.Name)/itemAttributes.s;data/itemAnimations.s:$animationAlias/$oamAlias")
            $rows.Add($fields -join "`t")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/seed_satchel.tsv'),$rows)
        $dustAnimations = Read-NativeObjectPointerLabels $pack $path 'item1aAnimations' $connection
        $dustOam = Read-NativeObjectPointerLabels $pack $path 'item1aOamDataPointers' $connection
        if ($dustAnimations.Count -ne 4 -or $dustOam.Count -ne 5) { throw "$($pack.Name): native Pegasus dust pointer extent changed." }
        $dust = @($dustAnimations | ForEach-Object { Read-NativeItemAnimation $pack $_ $dustOam 'dust' })
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/pegasus_seed.tsv'),@(
            '# item`tcounter`tnormal-speed`tgrass-speed`tstairs-speed`tdust-tile`tdust-flags`tstep-sound`tinitial-animation`tcloud1`tcloud2`tcloud3`tsource',
            "22`t03c0`t$($speeds[7].ToString('x2'))`t$($speeds[5].ToString('x2'))`t$($speeds[6].ToString('x2'))`t$($data[0x1a * 3 + 1].ToString('x2'))`t$($data[0x1a * 3 + 2].ToString('x2'))`t$($soundIds['SND_LAND'].ToString('x2'))`t$($dust -join "`t")`tobject_code/common/itemParents/seedsParent.s;code/bank0.s:decPegasusSeedCounter;object_code/common/specialObjects/link.s:updateLinkSpeed_withParam;object_code/common/items/dust.s;data/itemAnimations.s:item1aAnimations"))
        $partData = Read-MenuRomBytes $pack 'partData' (0x13 * 8)
        if (($partData[(0x12 * 8)..(0x12 * 8 + 7)] -join ',') -ne '0,0,0,0,1,8,10,0') { throw "$($pack.Name): PART_BURNING_ENEMY `$12 attributes changed." }
        $path = Join-Path $Disassembly "data/$($pack.Name)/partAnimations.s"
        $partOamAlias = if ($pack.Name -eq 'ages') { 'part59OamDataPointers' } else { 'part4aOamDataPointers' }
        if ($pack.Symbols['part12Animations'] -ne $pack.Symbols['part20Animations'] -or $pack.Symbols['part12OamDataPointers'] -ne $pack.Symbols[$partOamAlias]) { throw "$($pack.Name): PART_BURNING_ENEMY `$12 animation/OAM aliases changed." }
        $partAnimations = Read-NativeObjectPointerLabels $pack $path 'part20Animations' $connection
        $partOam = Read-NativeObjectPointerLabels $pack $path $partOamAlias $connection
        if ($partAnimations.Count -ne 1 -or $partOam.Count -ne 6) { throw "$($pack.Name): PART_BURNING_ENEMY `$12 pointer extent changed." }
        $burnAnimation = Read-NativeItemAnimation $pack $partAnimations[0] $partOam 'loop'
        if ($burnAnimation.Split('|').Count -ne 6 -or $burnAnimation -notmatch '~3$') { throw "$($pack.Name): PART_BURNING_ENEMY `$12 native fallthrough/loop changed." }
        $path = Join-Path $Disassembly 'object_code/common/parts/flame.s'
        $burnCode = @((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        if ($burnCode -notmatch 'Object.id[\s\S]*cp \(hl\)[\s\S]*jr nz,@delete[\s\S]*ld c,\$10[\s\S]*objectUpdateSpeedZAndBounce[\s\S]*ld \(hl\),59[\s\S]*Enemy.health[\s\S]*ld \(hl\),\$01') { throw "$($pack.Name): flame.s: native burning target/gravity/counter/health contract changed." }
        Write-GeneratedTable((Join-Path $pack.Root 'effects/burning_enemy.tsv'),@(
            '# sprite`ttile-base`tpalette`tframes`tgravity`tanimation`tsource',
            "spr_common_sprites`t$([int]$partData[0x12 * 8 + 5])`t$($partData[0x12 * 8 + 6] -band 7)`t59`t16`t$burnAnimation`tobject_code/common/parts/flame.s:partCode12;data/$($pack.Name)/partAnimations.s:part12Animations/$partOamAlias"))
        foreach ($sprite in @('spr_common_items','spr_common_sprites')) { Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force }
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
