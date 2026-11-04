# Native table selectors and complete reachable scripts for dialogue-only actors.
$root = Split-Path $previewDestination -Parent
$presentation = Read-InteractionPresentation 'seasons'
$bytes = $seasonsRomBytes
$nativeConstants=@{}
foreach($table in @($seasonsGlobalFlagValues,$treasureIds)){foreach($key in $table.Keys){$nativeConstants[$key]=$table[$key]}}
if($nativeConstants['GLOBALFLAG_FINISHEDGAME'] -ne 0x28 -or $nativeConstants['GLOBALFLAG_MOBLINS_KEEP_DESTROYED'] -ne 0x16){throw 'Seasons NPC global flag layout changed.'}
function Read-NpcWord([int]$address) { return [int]$bytes[$address] -bor ([int]$bytes[$address+1] -shl 8) }
function Npc-Physical([int]$pointer,[int]$bankAddress) { return ($bankAddress -band (-bnot 0x3fff)) + ($pointer -band 0x3fff) }
function Npc-Symbol([string]$name,[int]$id){
    $bank=[int][Math]::Floor($seasonsSymbols[('interactionCode'+$id.ToString('x2'))]/0x4000)
    $key='bank:{0:x2}:{1}' -f $bank,$name
    if(!$seasonsSymbols.ContainsKey($key)){throw "Seasons NPC `$$($id.ToString('x2')): missing native label $key."}
    return [int]$seasonsSymbols[$key]
}
$scriptAddress = @{}
$scriptBankBase=$seasonsSymbols['fickleLadyScript_text1'] -band (-bnot 0x3fff)
$scriptPrefix='bank:{0:x2}:' -f ([int]($scriptBankBase/0x4000))
foreach ($name in @($seasonsSymbols.Keys | Sort-Object)) {
    if ($name.StartsWith($scriptPrefix)) { $scriptAddress[$seasonsSymbols[$name]] = $name.Substring($scriptPrefix.Length) }
}
$selectors = [Collections.Generic.List[string]]::new()
$selectors.Add("# id`tvariant`tstate`tscript`tsource")
$scripts = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
[void]$scripts.Add('stage0')
[void]$scripts.Add('makuTreeScript_gateHit')
function Add-NpcSelector([int]$id,[int]$variant,[int]$state,[int]$address,[string]$source) {
    $target = $scriptBankBase + ((Read-NpcWord $address) -band 0x3fff)
    if (!$scriptAddress.ContainsKey($target)) { throw "${source}: unresolved script pointer at `$$($address.ToString('x5'))." }
    $name = $scriptAddress[$target]
    [void]$scripts.Add($name)
    $selectors.Add("$($id.ToString('x2'))`t$($variant.ToString('x2'))`t$state`t$name`t$source")
}
$misc = $seasonsSymbols['miscNPC_scriptTable']
foreach ($id in @(0x2d,0x33,0x36,0x37,0x38,0x39,0x3c,0x3d)) {
    $table = Npc-Physical (Read-NpcWord ($misc+($id-0x24)*2)) $misc
    $count = if ($id -in @(0x2d,0x3c,0x3d)) {11} elseif ($id -eq 0x37) {12} elseif ($id -eq 0x33) {4} elseif ($id -in @(0x36,0x39)) {5} else {1}
    foreach ($state in 0..($count-1)) { Add-NpcSelector $id 0 $state ($table+$state*2) 'miscNpcs.s:miscNPC_scriptTable' }
}
foreach ($spec in @(
    @(0x80,0,'interactionCode80@table_7717',11),
    @(0x8a,0,'interactionCode8d@floodedHouseGirlScripts',5),
    @(0x8b,0,'interactionCode8d@masterDiversWifeScripts',5),
    @(0x42,0,'table_6d14',2),
    @(0x32,0,'interactionCode32@scriptTable',8),
    @(0x2e,0,'table_5f7e',5),
    @(0x3e,0,'table_6ac9@var03_00',11),
    @(0x3e,1,'table_6ac9@var03_01',11),
    @(0x3e,2,'table_6ac9@var03_02',11),
    @(0x3e,3,'table_6ac9@var03_03',5))) {
    $table=Npc-Symbol $spec[2] $spec[0]
    if (!$table) { throw "Missing Seasons NPC table $($spec[2])." }
    foreach($state in 0..($spec[3]-1)){Add-NpcSelector $spec[0] $spec[1] $state ($table+$state*2) $spec[2]}
}
foreach ($subid in @((2..0x0f)+(0x14..0x24))) {
    Add-NpcSelector 0x30 $subid 0 ((Npc-Symbol 'table_607f' 0x30)+$subid*2) 'subrosian.s:table_607f'
}
foreach ($subid in 0x10..0x16) {
    Add-NpcSelector 0xb1 $subid 0 ((Npc-Symbol 'table_6f4b' 0xb1)+$subid*2) 'shipPiratian.s:table_6f4b'
}
foreach($spec in @(@(0xc2,'mayorsHouseGirlScript'),@(0x40,'piratianRoofScript'),@(0xa5,'dinScript_subid8Init'),@(0x4e,'troupeScript_inHoronVillage'),
    @(0x25,'mittensScript'),@(0x26,'mittensOwnerScript'),@(0x27,'sokraScript_needSummerForD3'))) {
    [void]$scripts.Add($spec[1]);$selectors.Add("$($spec[0].ToString('x2'))`t00`t0`t$($spec[1])`tscripts/seasons/scripts.s:$($spec[1])")
}
foreach($subid in 0..1){
    $name="ringHelpBookSubid${subid}Script";[void]$scripts.Add($name)
    $selectors.Add("e5`t00`t$subid`t$name`tscripts/common/commonScripts.s:$name")
}
foreach($state in 1..3){Add-NpcSelector 0x8d 0 $state ((Npc-Symbol 'interactionCode8d@masterDiverScripts' 0x8d)+$state*2) 'sunkenCityNpcs.s:masterDiverScripts'}
foreach($spec in @(@(0x2a,0,'knowItAllBirdScript'),@(0x2a,0x0b,'panickingBirdScript'),@(0xc8,0,'boomerangSubrosianScript'))){
    [void]$scripts.Add($spec[2]);$selectors.Add("$($spec[0].ToString('x2'))`t$($spec[1].ToString('x2'))`t0`t$($spec[2])`tscripts/seasons/scripts.s:$($spec[2])")
}
foreach($healed in 0..1){
    $table=$seasonsSymbols[$(if($healed){'interactionCode3b@biggoronColdHealed'}else{'interactionCode3b@biggoronColdNotHealed'})]
    if(!$table){throw 'Missing native Goron script selector.'}
    foreach($subid in 0..5){Add-NpcSelector 0x3b $subid $healed ($table+$subid*2) 'goron.s:biggoronColdNotHealed/biggoronColdHealed'}
}
foreach($subid in @(0,1,2,4)){Add-NpcSelector 0xb8 $subid 0 ((Npc-Symbol 'table_7432' 0xb8)+$subid*2) 'ambi.s:table_7432'}
foreach($state in @((0..7)+@(11,12))){Add-NpcSelector 0x9d 0 $state ((Npc-Symbol 'table_5ec8' 0x9d)+$state*2) 'impa.s:table_5ec8'}
Write-GeneratedTable((Join-Path $root 'npcs/selectors.tsv'),$selectors)

# The eight native conditional families contain stage+1 bytes terminated by zero.
$visibility=[Collections.Generic.List[string]]::new();$visibility.Add("# id`tsubid`tvar03`talternative`tkind`tgroup`troom`tvalue`texpected-set`tsource")
function Add-NpcVisibility([int]$id,[int]$subid,[int]$alternative,[string[]]$conditions,[string]$source='object_code/seasons/interactions'){
    foreach($condition in $conditions){
        $fields=$condition.Split('/')
        if($fields.Count -ne 5){throw "Invalid NPC visibility condition $condition."}
        $visibility.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t*`t$alternative`t$($fields -join "`t")`t$source")
    }
}
foreach($family in 0..7){
    $base=$seasonsSymbols['conditionalHoronNPCLookupTable']
    $table=Npc-Physical (Read-NpcWord ($base+$family*2)) $base
    $count=@(7,6,1,1,5,4,1,4)[$family]
    foreach($subid in 0..($count-1)){
        $cursor=Npc-Physical (Read-NpcWord ($table+$subid*2)) $table
        $values=[Collections.Generic.List[string]]::new()
        while($bytes[$cursor] -ne 0){
            if($values.Count -ge 11 -or $bytes[$cursor] -gt 11){throw 'Invalid conditionalHoronNPCLookupTable stage stream.'}
            $values.Add([string]($bytes[$cursor]-1));$cursor++
        }
        $id=@(0x2d,0x37,0x3c,0x3d,0x3e,0x3e,0x3e,0x80)[$family]
        $placedSubid=$subid+@(0x80,0x80,0x80,0x80,0,0x10,0x20,0)[$family]
        $alternative=0
        foreach($state in $values){
            $conditions=@("seasons-horon/-/-/$(([int]$state).ToString('x2'))/1")
            if($family -eq 6){$conditions+="runtime-equals/-/$($seasonsSymbols['wRoomStateModifier'].ToString('x4'))/03/0"}
            Add-NpcVisibility $id $placedSubid $alternative $conditions 'miscNpcs.s:conditionalHoronNPCLookupTable'
            $alternative++
        }
    }
}
foreach($id in @(0x2e,0x36,0x39,0x3e,0x8a,0x8b,0x8d)){
    foreach($state in 0..4){
        if($id -eq 0x8d -and $state -notin 1..3){continue}
        $subid=$state+$(if($id -eq 0x36){0x80}elseif($id -eq 0x3e){0x30}else{0})
        $conditions=@("seasons-sunken/-/-/$($state.ToString('x2'))/1")
        if($id -eq 0x8d){$conditions+=@('treasure/-/-/40/1','essence/-/-/fc/1')}
        if($state -eq 1 -and $id -in @(0x8b,0x3e)){
            foreach($destroyed in 0..1){
                $target=if($id -eq 0x8b){@(0xb6,0x5d)[$destroyed]}else{@(0x5e,0x6e)[$destroyed]}
                Add-NpcVisibility $id $subid $destroyed ($conditions+@("global/-/-/16/$destroyed","room-id/-/-/$($target.ToString('x2'))/1")) 'sunkenCityNpcs.s/miscBoyNpcs.s'
            }
        }else{Add-NpcVisibility $id $subid 0 $conditions 'sunkenCityNpcs.s/miscNpcs.s'}
    }
}
foreach($subid in 0..1){
    Add-NpcVisibility 0x42 $subid 0 @("global/-/-/13/$subid") 'pirateHouseSubrosian.s'
    Add-NpcVisibility 0x25 $subid 0 @("current-room/-/-/40/$subid") 'mittensAndOwner.s'
}
Add-NpcVisibility 0x26 1 0 @('current-room/-/-/40/1') 'mittensAndOwner.s'
Add-NpcVisibility 0x40 9 0 @('global/-/-/13/0') 'piratian.s'
Add-NpcVisibility 0xa5 8 0 @('global/-/-/28/1') 'din.s'
Add-NpcVisibility 0x4e 0x0a 0 @('global/-/-/28/1') 'dinDancingEvent.s'
Add-NpcVisibility 0x27 2 0 @('global/-/-/28/0',"wram/-/$($seasonsSymbols['wObtainedSeasons'].ToString('x4'))/02/0") 'sokra.s'
Add-NpcVisibility 0x2a 0x0b 0 @('global/-/-/22/1','global/-/-/23/0') 'bird.s'
Add-NpcVisibility 0x9d 5 0 @('global/-/-/28/0','global/-/-/22/1','global/-/-/23/0') 'impa.s'
$linked="runtime-equals/-/$($seasonsSymbols['wIsLinkedGame'].ToString('x4'))/01"
$normalImpa=@(@('global/-/-/22/0'),@('global/-/-/23/1'))
$impaLinked=@(@("$linked/0"),@("$linked/1",'global/-/-/19/0','global/-/-/1e/0'),@("$linked/1",'global/-/-/19/1','global/-/-/1f/1'))
$alternative=0
foreach($talk in $normalImpa){foreach($story in $impaLinked){
    Add-NpcVisibility 0x9d 1 $alternative (@('global/-/-/28/0')+$talk+$story) 'impa.s'
    $alternative++
}}
foreach($spec in @(@(0,@('treasure/-/-/40/0')),@(0,@('essence/-/-/fc/0')),
    @(1,@('treasure/-/-/40/1','essence/-/-/fc/1','essence/-/-/f0/0')),@(2,@('treasure/-/-/40/1','essence/-/-/f0/1')))){
    Add-NpcVisibility 0xb8 $spec[0] $alternative (@("$linked/1",'global/-/-/13/0')+$spec[1]) 'ambi.s:func_740a'
    $alternative++
}
Add-NpcVisibility 0xb8 4 0 @("$linked/1",'global/-/-/13/1') 'ambi.s:func_740a'
Write-GeneratedTable((Join-Path $root 'objects/npc_visibility.tsv'),$visibility)
$texts=[Collections.Generic.List[string]]::new();$texts.Add("# id`tmessage-base64`tsource")
foreach($id in @((0x1700..0x1703)+(0x3200..0x3213)+@(0x3300,0x3301,0x3302,0x3700,0x3019,0x301a,0x3020,0x3025,0x3026,0x303d))){
    $texts.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($seasonNpcTexts[$id])))`ttext/seasons/text.yaml")
}
Write-GeneratedTable((Join-Path $root 'npcs/texts.tsv'),$texts)
$tables=[Collections.Generic.List[string]]::new();$tables.Add("# name`tbytes`tsource")
foreach($spec in @(@('ball-speeds','table_7b59',8,0x83),@('bubble-speeds','partCode32@table_68b9',4,-1),@('piratian-counters','table_6c09',32,0x40))){
    $address=if($spec[3] -ge 0){Npc-Symbol $spec[1] $spec[3]}else{$seasonsSymbols[$spec[1]]}
    if(!$address){throw "Missing Seasons native NPC table $($spec[1])."}
    $values=@($bytes[$address..($address+$spec[2]-1)] | ForEach-Object {$_.ToString('x2')})
    $tables.Add("$($spec[0])`t$($values -join ',')`t$($spec[1])")
}

$paths=@((Join-Path $Disassembly 'scripts/common/commonScripts.s'),(Join-Path $Disassembly 'scripts/seasons/scripts.s'))
# Only owner $26:$01 is admitted: native state zero proves permanent room flag $40.
$programRows=@(Read-AssemblyNpcProgramRows $paths @($scripts) @{mittensOwnerScript='jumpifroomflagset $40'})
$bindings=@{};$memoryBindings=@{};$symbols=@{}
foreach($key in $nativeConstants.Keys){
    $symbols[$key]=$nativeConstants[$key]
    $prefix=if($key.StartsWith('GLOBALFLAG_')){'flag:'}else{'treasure:'}
    $memoryBindings[$key]=$prefix+$nativeConstants[$key].ToString('x2')
}
$symbols['SPEED_080']=0x14;$symbols['ANGLE_LEFT']=0x18;$symbols['ANGLE_RIGHT']=8
foreach($match in [regex]::Matches((Read-ImportText (Join-Path $Disassembly 'constants/common/music.s')),'(?m)^\s*(?<name>\w+)\s+db\s*;\s*\$(?<value>[0-9a-f]+)')){
    $symbols[$match.Groups['name'].Value]=[Convert]::ToInt32($match.Groups['value'].Value,16)
}
foreach($row in $programRows | Select-Object -Skip 1){
    $fields=$row.Split([char]9);$op=$fields[4];$operands=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($fields[5]))
    $p=@($operands -split ',\s*');$binding=$null
    switch($op){
        {$_ -in @('showtextnonexitable','showtextnonexitablelowindex')}{
            if($p[0] -notmatch 'TX_([0-9a-f]{4})$'){throw "$($fields[7]):$($fields[3]): unresolved text $($p[0])."}
            $binding=@{Opcode='nativeyield';Payload=('nonexitabletext '+$Matches[1])}
        }
        {$_ -in @('asm15','cplinkx','setanimationfromobjectbyte','setzspeed','addobjectbyte','setanimationfromangle')}{
            $binding=@{Opcode='native';Payload=($op+' '+$operands).Trim()}
        }
        {$_ -in @('jumpifmemoryeq','writememory','checkmemoryeq','checkobjectbyteeq')}{$symbols[$p[0]]=$p[0]}
        'jumpifmemoryset'{$memoryBindings[$p[0]+','+$p[1]]='mask:'+$p[0]+':'+$p[1].TrimStart('$')}
        'writeobjectbyte'{
            $offset=if($p[0] -eq 'Interaction.var37'){'77'}else{$p[0].TrimStart('$')}
            $binding=@{Opcode='writeobjectbyte';Actor='Self';Arg0=$offset;Arg1=$p[1].TrimStart('$')}
        }
        'incstate'{$binding=@{Opcode='nativeyield';Payload='incstate'}}
        'giveitem'{
            if($operands -ne 'ITEM_SWORD, $03'){throw "Maku gate: unexpected grant $operands."}
            $binding=@{Opcode='nativeyield';Payload='gate-sword-grant'}
        }
        'playsound'{$binding=@{Opcode='playsound';Arg0=([int]$symbols[$p[0]]).ToString('x2')}}
    }
    if($null -ne $binding){$bindings[$op+'|'+$operands]=$binding}
}
$animations=@{};foreach($i in 0..15){$animations[$i]=[string]$i}
$makuRows=@($programRows[0])+@($programRows | Select-Object -Skip 1 | Where-Object {($_.Split([char]9)[0]) -in @('stage0','makuTreeScript_gateHit')})
$talkRows=@($programRows[0])+@($programRows | Select-Object -Skip 1 | Where-Object {($_.Split([char]9)[0]) -notin @('stage0','makuTreeScript_gateHit')})
$commands=@(ConvertTo-NpcProgramRows $talkRows 'Self' $seasonNpcTexts $animations $symbols -sourceBindings $bindings -memoryBindings $memoryBindings -yieldOnJump $true)
# Seasons scriptCmd_jump has no copied-buffer branch and continues at its ROM target.
$commands+=@(ConvertTo-NpcProgramRows $makuRows 'Self' $seasonNpcTexts $animations $symbols -sourceBindings $bindings -memoryBindings $memoryBindings -yieldOnMemoryJumpTable $true | Select-Object -Skip 1)
Write-CutsceneGeneratedTable((Join-Path $root 'npcs/commands.tsv'),$commands)

$visuals=[Collections.Generic.List[string]]::new();$visuals.Add("# id`tsubid`tsprite`ttile-base`tpalette`tdefault-animation`tappend-sprites`tsource-offset`tsource`tsource-inverted")
$animations=[Collections.Generic.List[string]]::new();$animations.Add("# id`tindex`tframes`tsource")
$dataPath=Join-Path $Disassembly 'data/seasons/interactionData.s'
$gfx=@(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/seasons/objectGfxHeaders.s') '' 'm_ObjectGfxHeader')
$animationLabels=@{}
foreach($name in @($presentation.Definitions.Keys | Sort-Object)){
    if($seasonsSymbols.ContainsKey($name)){$animationLabels[$seasonsSymbols[$name]]=$name}
}
foreach($id in @(0x25,0x26,0x27,0x2a,0x2d,0x2e,0x30,0x32,0x33,0x36,0x37,0x38,0x39,0x3b,0x3c,0x3d,0x3e,0x40,0x42,0x4e,0x80,0x82,0x83,0x87,0x8a,0x8b,0x8d,0x8e,0x9c,0x9d,0xa5,0xb1,0xb8,0xc2,0xc8,0xc9,0xe5)){
    $hex=$id.ToString('x2')
    $entry=@(Read-AssemblyMacroInvocations $dataPath '' 'm_InteractionData' | Where-Object Comment -eq "`$$hex")
    if($entry.Count -ne 1){throw "NPC `$$hex has no unique graphics data."}
    $records=@(if($entry[0].Operands[0].StartsWith('$')){$entry}else{Read-AssemblyMacroInvocations $dataPath $entry[0].Operands[0] 'm_InteractionSubidData'})
    $lastAnimation=$presentation.Tables["interaction${hex}Animations"].Count-1
    for($subid=0;$subid -lt $records.Count;$subid++){
        $fields=$records[$subid].Operands;$header=Convert-AssemblyInteger $fields[0];$tile=(Convert-AssemblyInteger $fields[1]) -band 0x7f;$flags=Convert-AssemblyInteger $fields[2]
        $lastAnimation=[Math]::Max($lastAnimation,$flags -band 15)
        $sprite=$gfx[$header].Operands[0]
        $relative=[IO.Path]::GetRelativePath($Disassembly,(Find-ImportSpriteSource $sprite).FullName)
        Copy-GeneratedFile $relative "gfx/$sprite.png" $root
        $append=''
        if($sprite -eq 'spr_din_1'){
            $extra='spr_din_2'
            Copy-GeneratedFile ([IO.Path]::GetRelativePath($Disassembly,(Find-ImportSpriteSource $extra).FullName)) "gfx/$extra.png" $root
            $append="${extra}:$([int](Get-ImportSpriteSourceGrayscaleInverted $extra))"
        }
        if($sprite -eq 'spr_maku_tree_1'){
            $append=@(foreach($extra in @('spr_maku_tree_2','spr_maku_tree_3')){
                Copy-GeneratedFile ([IO.Path]::GetRelativePath($Disassembly,(Find-ImportSpriteSource $extra).FullName)) "gfx/$extra.png" $root
                "${extra}:$([int](Get-ImportSpriteSourceGrayscaleInverted $extra))"
            }) -join ','
        }
        $offset=if($gfx[$header].Operands.Count -ge 3){$gfx[$header].Operands[2].TrimStart('$')}else{'0000'}
        $visuals.Add("$hex`t$($subid.ToString('x2'))`t$sprite`t$tile`t$(($flags -shr 4) -band 7)`t$($flags -band 15)`t$append`t$offset`tdata/seasons/interactionData.s:`$$hex`t$([int](Get-ImportSpriteSourceGrayscaleInverted $sprite))")
    }
    # Native indexing continues through adjacent .dw labels (notably $36 animation $04).
    while($presentation.Tables["interaction${hex}Animations"].Count -le $lastAnimation){
        $table=$seasonsSymbols["interaction${hex}Animations"]
        $target=Npc-Physical (Read-NpcWord ($table+$presentation.Tables["interaction${hex}Animations"].Count*2)) $table
        if(!$animationLabels.ContainsKey($target)){throw "NPC `$${hex}: native animation pointer `$$($target.ToString('x5')) has no source definition."}
        $presentation.Tables["interaction${hex}Animations"]+=@($animationLabels[$target])
    }
    for($index=0;$index -lt $presentation.Tables["interaction${hex}Animations"].Count;$index++){
        $animation=Resolve-InteractionPresentation $presentation $id $index
        if(!$animation -or $animation -match '@(?:\||$)'){throw "NPC `$$hex animation $index has unresolved OAM."}
        $animations.Add("$hex`t$($index.ToString('x2'))`t$animation`tdata/seasons/interactionAnimations.s")
    }
}
Write-GeneratedTable((Join-Path $root 'npcs/visuals.tsv'),$visuals)
Write-GeneratedTable((Join-Path $root 'npcs/animations.tsv'),$animations)

# ENEMY$56 shares Maku's three uploaded sprite sheets, but has its own OAM stream.
$enemyPath=Join-Path $Disassembly 'data/seasons/enemyAnimations.s'
$enemyDefinitions=Read-AssemblyAnimationDefinitions $enemyPath 'enemyAnimation[0-9a-f]+(?:Loop)?'
$enemyPointers=@(Read-AssemblyDataDirectives $enemyPath 'enemy56OamDataPointers' '.dw' | ForEach-Object {$_.Operands[0]})
$bubbleAnimations=@(foreach($entry in Read-AssemblyDataDirectives $enemyPath 'enemy56Animations' '.dw'){
    $definition=$enemyDefinitions[$entry.Operands[0]]
    $frames=@(foreach($frame in $definition.Frames){
        $oam=Read-IntroOamParts (Join-Path $Disassembly 'data/seasons/enemyOamData.s') $enemyPointers[$frame.PointerOffset/2]
        $decimal=if([string]::IsNullOrEmpty($oam)){''}else{(@($oam.Split(';') | ForEach-Object {(@($_.Split(',') | ForEach-Object {[Convert]::ToInt32($_,16)}) -join ',')}) -join ';')}
        Format-NpcAnimationFrame $frame $decimal
    })
    Join-NpcAnimationFrames $frames $definition.LoopStart
})
if($bubbleAnimations.Count -ne 2){throw 'ENEMY$56 must retain its two native animations.'}
$bubbleData=@($bytes[($seasonsSymbols['enemyData']+0x56*4)..($seasonsSymbols['enemyData']+0x56*4+3)])
if(($bubbleData -join ',') -ne '5,130,58,53'){throw 'Seasons ENEMY$56 native graphics/collision/property record changed.'}
$bubbleSprites=@($gfx[$bubbleData[0]].Operands[0],$gfx[$bubbleData[0]+1].Operands[0]) -join ','
Write-GeneratedTable((Join-Path $root 'npcs/maku_bubble_visual.tsv'),@("# sprites`ttile-base`tpalette`tanimations-base64`tsource",
    "$bubbleSprites`t$(($bubbleData[3] -band 15)*2)`t$(($bubbleData[3] -shr 4) -band 7)`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($bubbleAnimations -join "`n")))`tdata/seasons/enemyAnimations.s:enemy56Animations;enemyData.s:`$56"))
foreach($spec in @(@('maku-bubble-offsets','enemyCode56@bubbleOffsetAndCollisionRadius',24),
    @('maku-gate-tiles','interactionCode22@tileChangeTable',16),@('maku-gate-puffs','interactionCode22@table_563f',16))){
    $address=$seasonsSymbols[$spec[1]]
    if(!$address){throw "Maku native table $($spec[1]) is missing."}
    $values=@($bytes[$address..($address+$spec[2]-1)] | ForEach-Object {$_.ToString('x2')})
    $tables.Add("$($spec[0])`t$($values -join ',')`t$($spec[1])")
}
$partPath=Join-Path $Disassembly 'data/seasons/partAnimations.s'
$partDefinitions=Read-AssemblyAnimationDefinitions $partPath 'partAnimation[0-9a-f]+(?:Loop)?'
$pointers=@(Read-AssemblyDataDirectives $partPath 'part32OamDataPointers' '.dw' | ForEach-Object {$_.Operands[0]})
$sequences=@(foreach($entry in Read-AssemblyDataDirectives $partPath 'part32Animations' '.dw'){
    $definition=$partDefinitions[$entry.Operands[0]]
    $frames=@(foreach($frame in $definition.Frames){
        $oam=Read-IntroOamParts (Join-Path $Disassembly 'data/seasons/partOamData.s') $pointers[$frame.PointerOffset/2]
        $decimal=(@($oam.Split(';') | ForEach-Object {(@($_.Split(',') | ForEach-Object {[Convert]::ToInt32($_,16)}) -join ',')}) -join ';')
        Format-NpcAnimationFrame $frame $decimal
    })
    Join-NpcAnimationFrames $frames $definition.LoopStart
})
if($sequences.Count -ne 3){throw 'PART$32 must retain three bubble animation entries.'}
$part=@($bytes[($seasonsSymbols['partData']+0x32*8)..($seasonsSymbols['partData']+0x32*8+7)])
if(($part -join ',') -ne '56,242,34,0,1,2,1,0'){throw 'Seasons PART$32 attributes changed.'}
$sprite=$gfx[$part[0]].Operands[0]
Copy-GeneratedFile ([IO.Path]::GetRelativePath($Disassembly,(Find-ImportSpriteSource $sprite).FullName)) "gfx/$sprite.png" $root
Write-GeneratedTable((Join-Path $root 'npcs/bubble_visual.tsv'),@("# sprite`ttile-base`tpalette`tinverted`tanimations-base64`tsource",
    "$sprite`t$($part[5])`t$($part[6] -band 7)`t$([int](Get-ImportSpriteSourceGrayscaleInverted $sprite))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sequences -join "`n")))`tdata/seasons/partData.s:PART`$32"))
Write-GeneratedTable((Join-Path $root 'npcs/native_tables.tsv'),$tables)

$snore=$bytes[$seasonsSymbols['interactiona0SubidData']..($seasonsSymbols['interactiona0SubidData']+2)]
if(($snore -join ',') -ne '56,0,48'){throw 'Seasons INTERAC$a0:$00 native snore graphics changed.'}
$snoreSprite=$gfx[$snore[0]].Operands[0]
Copy-GeneratedFile ([IO.Path]::GetRelativePath($Disassembly,(Find-ImportSpriteSource $snoreSprite).FullName)) "gfx/$snoreSprite.png" $root
$snoreAnimation=Resolve-InteractionPresentation $presentation 0xa0 0
Write-GeneratedTable((Join-Path $root 'effects/snore.tsv'),@(
    '# tile-base`tpalette`tduration`tspeed-raw`tleft-angle`tright-angle`tsway`tanimation`tsource`tsprite',
    "0`t3`t70`t0f`t1d`t03`t-1,-2,-1,0,1,2,1,0`t$snoreAnimation`tobject_code/common/interactions/floatingImage.s:interactionCodea0`t$snoreSprite"))
