$textIds = @(
    0x3000,0x3002,0x3003,0x3004,0x3005,0x3006,0x3007,0x3008,0x3009,0x300a,0x300b,0x300c,
    0x300e,0x300f,0x3010,0x3014,0x3015,0x3016,0x3018,0x3011,0x3012,0x3013,0x3017,
    0x3019,0x301a,0x301c,0x301d,0x301e,0x301f,0x3020,0x3024,0x3025,0x3026,0x3027,
    0x3028,0x302e,0x3033,0x3036,0x3037,0x3039,0x303a,0x303b,0x3038,0x303c,0x303d,0x303e,0x303f)
foreach ($pack in @(
    @{ Game='ages'; Root=$destination; Symbols=$agesSymbols; Texts=$allTexts; Group=2; Room=0xee },
    @{ Game='seasons'; Root=(Split-Path $previewDestination -Parent); Symbols=$seasonsSymbols; Texts=$seasonNpcTexts; Group=3; Room=0x91 }
)) {
    $path = Join-Path $Disassembly "objects/$($pack.Game)/mainData.s"
    $label = if ($pack.Game -eq 'ages') { 'group2MapeeObjectData' } else { 'group1Map91ObjectData' }
    $nodes = @(Read-AssemblyMacroInvocations $path $label)
    $expected = @('$89,$00,$28,$50','$89,$01,$38,$38','$89,$06,$38,$68','$e5,$00,$48,$28','$e5,$01,$48,$78')
    if ($nodes.Count -ne 6 -or $nodes[5].Name -ne 'obj_End') { throw "${path}:${label}: incomplete Vasu shop object stream." }
    for ($index=0; $index -lt 5; $index++) {
        if ($nodes[$index].Name -ne 'obj_Interaction' -or ($nodes[$index].Operands -join ',') -ne $expected[$index]) {
            throw "${path}:${label}: changed Vasu shop actor/order at index $index."
        }
    }
    $texts = [Collections.Generic.List[string]]::new(); $texts.Add("# text-id`tutf8-base64")
    foreach ($id in $textIds) {
        $message = Resolve-DialogueText $id $pack.Texts @{} @{} ([Collections.Generic.HashSet[int]]::new()) $false "text/$($pack.Game)/text.yaml"
        $texts.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/vasu_shop_texts.tsv'), $texts)
    $presentation = Read-InteractionPresentation $pack.Game
    $animations = [Collections.Generic.List[string]]::new(); $animations.Add("# interaction-id`tanimation`tencoded-animation")
    foreach ($spec in @(@(0x89,9),@(0xe5,2))) {
        $count = if ($pack.Game -eq 'seasons' -and $spec[0] -eq 0xe5) { 1 } else { $spec[1] }
        for ($index=0; $index -lt $count; $index++) {
            $animation = if ($pack.Game -eq 'ages') { Resolve-NpcAnimation $spec[0] $index }
                else { Resolve-InteractionPresentation $presentation $spec[0] $index }
            if (!$animation -or $animation -match '@(?:\||$)') { throw "$($pack.Game): unresolved Vasu animation $($spec[0]):$index." }
            $animations.Add("$(([int]$spec[0]).ToString('x2'))`t$($index.ToString('x2'))`t$animation")
        }
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/vasu_shop_animations.tsv'), $animations)
    # Common vasu.s/commonScripts.s/bank2.s contracts are guarded in the NPC import.
    # Resolve file-image addresses from the selected build, never the other game's WRAM comments.
    $values = [ordered]@{
        'group'=$pack.Group; 'room'=$pack.Room; 'textbox-position'=2
        'snake-proximity-radius'=24; 'red-snake-wait'=30; 'blue-snake-cable-timeout'=512
        'vasu-radius-y'=18; 'vasu-radius-x'=6; 'snake-radius'=6; 'a-button-point-offset'=10
        'ring-box-grab-mode'=2; 'ring-grab-mode'=1
        'obtained-ring-box-address'=$pack.Symbols['wObtainedRingBox']; 'rings-obtained-address'=$pack.Symbols['wRingsObtained']
        'rings-obtained-byte-count'=8; 'rings-appraised-address'=$pack.Symbols['wNumRingsAppraised']
        'linked-first-mask'=1; 'appraisal-cost'=20; 'duplicate-refund'=30
        'menu-close-wait'=10; 'appraisal-result-wait'=40; 'menu-exit-wait'=60
        'global-earned-slayer'=0; 'global-earned-wealth'=1; 'global-earned-victory'=2
        'global-got-slayer'=4; 'global-got-wealth'=5; 'global-got-victory'=6
        'global-obtained-ring-box'=8; 'global-appraised-hundredth'=9
        'ring-friendship'=0; 'ring-slayer'=52; 'ring-wealth'=53; 'ring-victory'=54; 'ring-hundredth'=56
    }
    $constants = [Collections.Generic.List[string]]::new(); $constants.Add("# key`tvalue")
    foreach ($key in $values.Keys) {
        if ($null -eq $values[$key]) { throw "$($pack.Game): unresolved Vasu constant $key." }
        $constants.Add("$key`t$($values[$key])")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/vasu_shop_constants.tsv'), $constants)
}
