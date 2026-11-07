$pack=@{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
$source=Read-ImportText (Join-Path $Disassembly 'object_code/seasons/enemies/brotherGoriyas.s')
$partSource=Read-ImportText (Join-Path $Disassembly 'object_code/seasons/parts/38.s')
if($source -notmatch '(?ms)^@state0:.*?ecom_spawnEnemyWithSubid01\s+ret nz.*?objectCopyPosition.*?^@func_45f0:\s+ld a,\$32' -or
    $source -notmatch '(?ms)^@@stateA:.*?PART_38.*?ecom_spawnProjectile.*?hEnemyTargetY.*?hEnemyTargetX.*?ld l,\$d8' -or
    $source -notmatch '(?ms)^@dead:.*?ecom_killRelatedObj1.*?jp enemyBoss_dead' -or
    $partSource -notmatch '(?ms)^@state0:.*?ld \(hl\),\$50.*?ld \(hl\),\$14.*?^@state2:.*?add \$08\s+cp \$11.*?^@state3:.*?add \$04\s+cp \$09') {
    throw 'Brother Goriya pair, projectile allocation, death or PART$38 target/catch contract changed.'
}
$part=Read-MenuRomBytes $pack 'partData' (0x39*8)
$at=0x38*8
if(($part[$at..($at+7)] -join ',') -ne '115,134,34,252,1,10,5,0') {throw 'Seasons PART$38 properties changed.'}
$connection=Start-AssemblySourceHost 'ROM_SEASONS;SEASONS_ENGINE;REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES'
try {
    $path=Join-Path $Disassembly 'data/seasons/partAnimations.s'
    $animations=Read-NativeObjectPointerLabels $pack $path 'part38Animations' $connection
    $oam=Read-NativeObjectPointerLabels $pack $path 'part38OamDataPointers' $connection
    if($animations.Count -ne 1 -or $oam.Count -ne 4){throw 'Seasons PART$38 requires one animation/four OAM rows.'}
    $animation=Read-NativeEnemyAnimation $pack $animations[0] $oam $false
    $headers=@((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$(Join-Path $Disassembly 'data/seasons/objectGfxHeaders.s')`0objectGfxHeaderTable`0m_ObjectGfxHeader"|ConvertFrom-Json)|Where-Object IsActive)
    $sprite=$headers[[int]$part[$at]].Operands[0]
    Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($animation.Encoded))
    Write-GeneratedTable((Join-Path $pack.Root 'effects/brother_goriya_boomerang.tsv'),@(
        "# sprites`ttile-base`tpalette`tsource-grayscale-inverted`tanimations-base64",
        "$sprite`t$($part[$at+5])`t$($part[$at+6]-band 7)`t$([int](Get-ImportSpriteSourceGrayscaleInverted $sprite))`t$encoded"))
} finally {[void](Invoke-AssemblySourceHost $connection 'QUIT' '');$connection.WaitForExit();$connection.Dispose()}
if(-not $seasonNpcTexts.ContainsKey(0x2f00)){throw 'Brother Goriya TX_2f00 is unresolved.'}
Write-GeneratedTable((Join-Path $pack.Root 'objects/brother_goriya_text.tsv'),@(
    "# id`tmessage-base64`tsource",
    "2f00`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($seasonNpcTexts[0x2f00])))`ttext/seasons/text.yaml:TX_2f00"))
