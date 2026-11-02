$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../import_oracles/Shared/Resolve-DialogueText.ps1')
$texts = @{
    0x2300 = 'carpenter'
    0x2301 = 'head \call(TX_2300) suffix'
    0x2304 = 'first\n'
    0x2305 = 'second\col(1)\Link'
    1 = 'prefix\jump(TX_2300)\call(TX_ffff)'
    2 = '\call(TX_2300) / \call(TX_2300)'
    3 = '\call(TX_0004)'
    4 = '\jump(TX_0003)'
    5 = '\call(0xfd)'
}
$fallthroughs = @{ 0x2304 = 0x2305 }
$positions = @{ 0x2300 = 2; 0x2305 = 0 }
function Assert-DialogueReference([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}
Assert-DialogueReference ((Resolve-DialogueText 0x2301 $texts $fallthroughs) -eq 'head carpenter suffix') 'Dialogue call lost its return suffix.'
Assert-DialogueReference ((Resolve-DialogueText 1 $texts $fallthroughs) -eq 'prefixcarpenter') 'A jump must discard unreachable calls after it.'
Assert-DialogueReference ((Resolve-DialogueText 2 $texts $fallthroughs $positions) -eq '\pos(2)carpenter / \pos(2)carpenter') 'Repeated calls must preserve positions without reporting a false cycle.'
Assert-DialogueReference ((Resolve-DialogueText 0x2304 $texts $fallthroughs $positions) -eq "first`n\pos(0)second\col(1)\Link") 'Physical fallthrough lost newline, page position or symbolic formatting.'
Assert-DialogueReference ((Resolve-DialogueText 0x2304 $texts $fallthroughs @{} -convertTrailingNewline $false) -eq 'first\nsecond\col(1)\Link') 'Raw Seasons entries must retain their newline encoding.'
Assert-DialogueReference ((Resolve-DialogueText 5 $texts $fallthroughs) -eq '\call(0xfd)') 'Dynamic area calls belong to the runtime owner.'
$visiting = [Collections.Generic.HashSet[int]]::new()
foreach ($id in 3, 0xffff) {
    $caught = $false
    try { $null = Resolve-DialogueText $id $texts $fallthroughs @{} $visiting }
    catch { $caught = $_.Exception.Message.Contains('text/ages/text.yaml') -and $_.Exception.Message.Contains(('TX_{0:x4}' -f $id)) }
    Assert-DialogueReference ($caught -and $visiting.Count -eq 0) 'Missing/recursive dialogue must report its source and release the visit stack.'
}
Write-Host 'Dialogue reference fixtures passed (calls, jumps, positions, fallthrough, dynamic controls and diagnostics).'
