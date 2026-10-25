# Preserve the exact imported PNG stream as a binary asset. Godot's texture
# importer remaps .png to a GPU resource; .png.bin stays CPU-readable in exports.
# This stage runs after every graphics producer, including copied source art.
foreach ($image in Get-ChildItem -LiteralPath @($destination, $previewDestination) -Recurse -File -Filter '*.png' | Sort-Object FullName) {
    [byte[]]$bytes = [IO.File]::ReadAllBytes($image.FullName)
    if ($bytes.Length -lt 24 -or [BitConverter]::ToString($bytes, 0, 8) -ne '89-50-4E-47-0D-0A-1A-0A') {
        throw "CPU graphics source '$($image.FullName)' is not a PNG stream."
    }
    Write-GeneratedBytes @(($image.FullName + '.bin'), $bytes)
}
