# Rod acquisition/statue scripts and ITEM $07's selected clean-US weapon data.
$pack=@{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
$root=$pack.Root
function Read-RodBytes([string]$symbol,[int]$count){
    if(!$seasonsSymbols.ContainsKey($symbol) -or $seasonsSymbols.ContainsKey("ambiguous:$symbol")){throw "Rod: unresolved native symbol $symbol."}
    $address=[int]$seasonsSymbols[$symbol]
    return ,([byte[]]$seasonsRomBytes[$address..($address+$count-1)])
}
$paths=@('scripts/common/commonScripts.s','scripts/seasons/scripts.s','scripts/seasons/scripts2.s') | ForEach-Object {Join-Path $Disassembly $_}
$programs=@(Read-AssemblyNpcProgramRows $paths @('gettingRodOfSeasonsScript','gettingRodOfSeasonsScript_setCounter1To32',
    'seasonsSpiritsScript_enteringTempleArea','seasonsSpiritsScript_winterTempleOrbBridge','seasonsSpiritsScript_spiritStatue'))
$symbols=@{};$memory=@{};$bindings=@{};$texts=@{}
$symbols['TILEINDEX_VERTICAL_BRIDGE']=0x6a;$symbols['LINK_STATE_NORMAL']=1
foreach($table in @($seasonNpcTexts,$seasonTreasureTexts)){foreach($key in $table.Keys){$texts[$key]=$table[$key]}}
foreach($table in @($seasonsGlobalFlagValues,$treasureIds)){
    foreach($key in $table.Keys){$symbols[$key]=$table[$key];$memory[$key]=$(if($key.StartsWith('GLOBALFLAG_')){'flag:'}else{'treasure:'})+$table[$key].ToString('x2')}
}
foreach($match in [regex]::Matches((Read-ImportText (Join-Path $Disassembly 'constants/common/music.s')),'(?m)^\s*(?<name>\w+)\s+db\s*;\s*\$(?<value>[0-9a-f]+)')){
    $symbols[$match.Groups['name'].Value]=[Convert]::ToInt32($match.Groups['value'].Value,16)
}
foreach($path in @('constants/common/music.s','constants/common/tileIndices.s','constants/common/textboxFlags.s')){
    foreach($match in [regex]::Matches((Read-ImportText (Join-Path $Disassembly $path)),'(?m)^\s*\.define (?<name>\w+)\s+\$(?<value>[0-9a-f]+)')){
        $symbols[$match.Groups['name'].Value]=[Convert]::ToInt32($match.Groups['value'].Value,16)
    }
}
foreach($row in $programs | Select-Object -Skip 1){
    $fields=$row.Split([char]9);$op=$fields[4];$args=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($fields[5]));$parts=@($args -split ',\s*');$binding=$null
    switch($op){
        'delay'{$delay=Read-RodBytes 'scriptCmd_delay@delayLengths' 13;$binding=@{Opcode='wait';Arg0=[string]$delay[(Convert-AssemblyInteger $parts[0])]}}
        'checkcollidedwithlink_onground'{$binding=@{Opcode='gate';Payload='TouchGround'}}
        'checkpalettefadedone'{$binding=@{Opcode='gate';Payload='PaletteDone'}}
        'checkflagset'{$binding=@{Opcode='gate';Payload=('flagset '+$args)}}
        'createpuff'{$binding=@{Opcode='nativeyield';Payload='createpuff'}}
        'stopifroomflag40set'{$binding=@{Opcode='nativeyield';Payload='StopIfRoomFlag40'}}
        'xorcfc0bit'{$binding=@{Opcode='nativeyield';Payload=('XorSignal '+$args)}}
        'settilehere'{$binding=@{Opcode='native';Payload=('settilehere $'+$symbols[$args].ToString('x2'))}}
        'asm15'{$binding=@{Opcode='native';Payload=($op+' '+$args)}}
        {$_ -in @('setcounter1','spawninteraction')}{$binding=@{Opcode='nativeyield';Payload=($op+' '+$args)}}
        'resetmusic'{$binding=@{Opcode='setmusic';Arg0='ff'}}
        'setmusic'{$binding=@{Opcode='setmusic';Arg0=$(if($parts[0].StartsWith('$')){$parts[0].TrimStart('$')}else{$symbols[$parts[0]].ToString('x2')})}}
        'playsound'{$binding=@{Opcode='playsound';Arg0=$symbols[$parts[0]].ToString('x2')}}
        {$_ -in @('checkmemoryeq','writememory','checkobjectbyteeq')}{$symbols[$parts[0]]=$parts[0]}
    }
    if($binding){$bindings[$op+'|'+$args]=$binding}
}
$commands=@(ConvertTo-NpcProgramRows $programs 'Self' $texts @{} $symbols -sourceBindings $bindings -memoryBindings $memory -yieldOnMemoryJumpTable $true)
Write-CutsceneGeneratedTable((Join-Path $root 'rod/commands.tsv'),$commands)
function Rod-Word([int]$address){return [int]$seasonsRomBytes[$address] -bor ([int]$seasonsRomBytes[$address+1] -shl 8)}
function Rod-Target([int]$pointer,[int]$owner){return ($owner -band (-bnot 0x3fff))+($pointer -band 0x3fff)}
$poses=@("# index`tanimation`tsource")
$animationTable=$seasonsSymbols['item07Animations'];$oamTable=$seasonsSymbols['item07OamDataPointers']
$oamLabels=[regex]::Matches((Read-ImportText (Join-Path $Disassembly 'data/itemAnimations.s')), '(?s)item07OamDataPointers:[^\r\n]*\r?\n(?<rows>(?:\s*\.dw[^\r\n]+\r?\n){8})')[0].Groups['rows'].Value
$oamLabels=@([regex]::Matches($oamLabels,'\.dw\s+(\w+)') | ForEach-Object {$_.Groups[1].Value})
if($oamLabels.Count -ne 8){throw 'ITEM$07 requires eight source OAM pointers.'}
for($index=0;$index -lt 8;$index++){
    $address=Rod-Target (Rod-Word ($animationTable+$index*2)) $animationTable
    $duration=$seasonsRomBytes[$address];$oamOffset=$seasonsRomBytes[$address+1];$parameter=$seasonsRomBytes[$address+2]
    if($duration -ne 0x7f -or ($oamOffset -band 1) -ne 0 -or $oamOffset -gt 14){throw 'ITEM$07 must retain eight inert native OAM poses.'}
    $oam=[int]$seasonsSymbols[$oamLabels[$oamOffset/2]]
    if((Rod-Word ($oamTable+$oamOffset)) -ne (($oam -band 0x3fff)+0x4000)){throw 'ITEM$07 source OAM pointer mismatch.'}
    $cells=@();$count=$seasonsRomBytes[$oam];$oam++
    if($count -ne @(1,2,2,2,1,2,2,2)[$index]){throw 'ITEM$07 malformed native OAM composition.'}
    for($cell=0;$cell -lt $count;$cell++){$cells+=(@($seasonsRomBytes[$oam..($oam+3)] | ForEach-Object {[int]$_}) -join ',');$oam+=4}
    $poses+="$index`t$duration,$parameter@$($cells -join ';')`tdata/itemAnimations.s:ITEM_ROD_OF_SEASONS/$index"
}
Write-GeneratedTable((Join-Path $root 'rod/weapon_poses.tsv'),$poses)
$header=Read-RodBytes 'uncmpGfxHeader1c' 6
$address=([int]$header[0]*0x4000)+((([int]$header[1] -shl 8) -bor [int]$header[2]) -band 0x3fff)
$length=([int]$header[5]+1)*16
if($length -ne 160){throw 'Rod weapon upload must retain its ten native OBJ tiles.'}
[IO.File]::WriteAllBytes((Join-Path $root 'rod/weapon.2bpp'),$seasonsRomBytes[$address..($address+$length-1)])
$attributes=Read-RodBytes 'itemAttributes' 32
$graphics=Read-RodBytes 'itemData' 24
Write-GeneratedTable((Join-Path $root 'rod/weapon.tsv'),@("# tile-base`tflags`tcollision`tdamage`thealth`tsource",
    "$($graphics[22].ToString('x2'))`t$($graphics[23].ToString('x2'))`t$($attributes[28].ToString('x2'))`t$($attributes[30].ToString('x2'))`t$($attributes[31].ToString('x2'))`tdata/seasons/itemData.s;data/seasons/itemAttributes.s:ITEM_ROD_OF_SEASONS"))
$sparkles=Read-RodBytes 'interactionCodee6_state0@sparklesData' 16
$bob=Read-RodBytes 'interactionCodee6_state1@rodOfSeasons@seasonsTable_15_705f' 16
$swing=Read-RodBytes 'label_07_227@data' 24
$oscillation=Read-RodBytes 'objectOscillateZ_body@zOffsets' 8
Write-GeneratedTable((Join-Path $root 'rod/native_tables.tsv'),@("# name`tbytes`tsource",
    "sparkles`t$(@($sparkles | ForEach-Object {$_.ToString('x2')}) -join ',')`tgetRodOfSeasons.s:@sparklesData",
    "bob`t$(@($bob | ForEach-Object {$_.ToString('x2')}) -join ',')`tgetRodOfSeasons.s:@@seasonsTable_15_705f",
    "swing`t$(@($swing | ForEach-Object {$_.ToString('x2')}) -join ',')`tpostUpdate.s:updateSwingableItemAnimation.@data",
    "oscillation`t$(@($oscillation | ForEach-Object {$_.ToString('x2')}) -join ',')`tseasonsFairy.s:objectOscillateZ_body.@zOffsets"))
Write-GeneratedTable((Join-Path $root 'rod/texts.tsv'),@("# id`tmessage-base64`tsource")+@(
    foreach($id in @(0x0071)+(0x0800..0x081b)){
        if(!$texts.ContainsKey($id)){throw "Rod acquisition: missing TX_$($id.ToString('x4'))."}
        "$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($texts[$id])))`ttext/seasons/text.yaml"
    }))
