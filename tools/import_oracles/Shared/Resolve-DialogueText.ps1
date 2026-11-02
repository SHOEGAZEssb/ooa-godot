function Resolve-DialogueText {
    param([int]$id, [hashtable]$texts, [hashtable]$fallthroughs, [hashtable]$positions = @{},
        [Collections.Generic.HashSet[int]]$visiting = [Collections.Generic.HashSet[int]]::new(),
        [bool]$convertTrailingNewline = $true, [string]$source = 'text/ages/text.yaml')
    if (-not $texts.ContainsKey($id) -or -not $visiting.Add($id)) {
        throw "${source}: missing or recursive TX_$($id.ToString('x4'))."
    }
    try {
        $remaining = [string]$texts[$id]
        $message = if ($positions.ContainsKey($id)) { "\pos($($positions[$id]))" } else { '' }
        $jumped = $false
        while ($true) {
            $command = [regex]::Match($remaining, '\\(?<kind>call|jump)\(TX_(?<id>[0-9a-f]{4})\)')
            if (-not $command.Success) { $message += $remaining; break }
            $target = [Convert]::ToInt32($command.Groups['id'].Value, 16)
            $message += $remaining.Substring(0, $command.Index) +
                (Resolve-DialogueText $target $texts $fallthroughs $positions $visiting $convertTrailingNewline $source)
            if ($command.Groups['kind'].Value -eq 'jump') { $jumped = $true; break }
            $remaining = $remaining.Substring($command.Index + $command.Length)
        }
        if (-not $jumped -and $fallthroughs.ContainsKey($id)) {
            # Ages' normalized entries retain the final newline as a symbolic control.
            if ($convertTrailingNewline -and $message.EndsWith('\n', [StringComparison]::Ordinal)) {
                $message = $message.Substring(0, $message.Length - 2) + "`n"
            }
            $message += Resolve-DialogueText $fallthroughs[$id] $texts $fallthroughs $positions $visiting $convertTrailingNewline $source
        }
        return $message
    } finally {
        [void]$visiting.Remove($id)
    }
}
