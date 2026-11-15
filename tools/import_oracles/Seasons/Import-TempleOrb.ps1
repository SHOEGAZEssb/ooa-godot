# The winter temple uses the shared stationary PART_ORB, with Seasons' own OBJ header.
$pack=@{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
$connection=Start-AssemblySourceHost 'ROM_SEASONS;SEASONS_ENGINE;REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES'
try {
    $part=(Read-MenuRomBytes $pack 'partData' 32)[24..31]
    if(($part -join ',') -ne '83,131,68,0,64,30,0,0'){throw 'Seasons PART$03 properties changed.'}
    $path=Join-Path $Disassembly 'data/seasons/objectGfxHeaders.s'
    $gfx=@((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$path`0objectGfxHeaderTable`0m_ObjectGfxHeader" | ConvertFrom-Json) | Where-Object IsActive)[$part[0]]
    if($gfx.Operands.Count -ne 1){throw 'Seasons PART$03 requires one native OBJ sheet.'}
    $sprite=$gfx.Operands[0]
    $header=Read-MenuRomBytes $pack 'objectGfxHeaderTable' (($part[0]+1)*3);$at=$part[0]*3
    if(($header[$at]-band 0x3f) -ne [int][Math]::Floor($pack.Symbols[$sprite]/0x4000) -or
        (([int]$header[$at+1]-shl 8)-bor $header[$at+2]) -ne (($pack.Symbols[$sprite]-band 0x3fff)+0x4000)){throw 'Seasons orb OBJ pointer differs from source.'}
    $path=Join-Path $Disassembly 'data/seasons/partAnimations.s'
    $animations=Read-NativeObjectPointerLabels $pack $path 'part03Animations' $connection
    $oam=Read-NativeObjectPointerLabels $pack $path 'part03OamDataPointers' $connection
    $pose=(Read-NativeEnemyAnimation $pack $animations[0] $oam $false).Encoded
    $inverted=[int](Get-ImportSpriteSourceGrayscaleInverted $sprite)
    Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
    Write-GeneratedTable((Join-Path $pack.Root 'objects/part_orb_visual.tsv'),@(
        "# sprite`ttile-base`tpalette`tinverted`tanimation-base64`tsource",
        "$sprite`t$($part[5])`t$($part[6])`t$inverted`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pose)))`tdata/seasons/partAnimations.s:part03Animations;objectGfxHeaderTable+$($part[0].ToString('x2'))"))
    $active=Read-ImportText (Join-Path $Disassembly 'data/seasons/partActiveCollisions.s')
    $mask=[regex]::Match($active,'(?m)^\s*dbrev (?<bits>%[01]{8} %[01]{8} %[01]{8} %[01]{8}) ; 0x03\s*$')
    if(!$mask.Success){throw 'Seasons PART$03 active collision mask missing.'}
    $bits=$mask.Groups['bits'].Value.Replace('%','').Replace(' ','')
    $effects=Read-MenuRomBytes $pack 'objectCollisionTable' 128
    $rows=@("# item`tenabled`teffect`tlockout`tsource")
    for($item=0;$item -lt 32;$item++){
        $effect=[int]$effects[0x60+$item]
        if($bits[$item] -eq '1' -and $effect -notin @(0x20,0x26)){throw 'Seasons orb has an unsupported collision effect.'}
        $lockout=if($effect -eq 0x26){'1c'}else{'00'}
        $rows+="$($item.ToString('x2'))`t$($bits[$item])`t$($effect.ToString('x2'))`t$lockout`tdata/seasons/partActiveCollisions.s:partActiveCollisions+000c;objectCollisionTable+$((0x60+$item).ToString('x4'))"
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/part_orb_collisions.tsv'),$rows)
    $source=Read-ImportText (Join-Path $Disassembly 'object_code/common/parts/orb.s')
    $init=[regex]::Match($source,'(?ms)call objectMakeTileSolid\s+ld h,Part.zh\s+ld \(hl\),\$(?<tile>[0-9a-f]{2}).*?and \$(?<mask>[0-9a-f]{2})\s+ld bc,bitTable')
    $solid=[regex]::Match((Read-ImportText (Join-Path $Disassembly 'code/bank0.s')),'(?ms)^objectMakeTileSolid:\s+call objectGetTileCollisions\s+ld \(hl\),\$(?<collision>[0-9a-f]{2})')
    if(!$init.Success -or !$solid.Success){throw 'Shared PART$03 initialization changed.'}
    Write-GeneratedTable((Join-Path $pack.Root 'objects/part_orb_initialization.tsv'),@(
        "# id`tbackground-tile`ttile-collision`tsubid-mask`tradius-y`tradius-x`tsource",
        "03`t$($init.Groups['tile'].Value)`t$($solid.Groups['collision'].Value)`t$($init.Groups['mask'].Value)`t04`t04`tobject_code/common/parts/orb.s;data/seasons/partData.s:partData+0018"))
} finally {[void](Invoke-AssemblySourceHost $connection 'QUIT' '');$connection.WaitForExit();$connection.Dispose()}
