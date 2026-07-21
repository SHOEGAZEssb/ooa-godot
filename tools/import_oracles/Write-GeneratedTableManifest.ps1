# Final import stage: inventory every generated TSV after all producers have
# finished, then write the deterministic version/count/content manifest used by
# the shared runtime table reader.
$manifestName = 'generated_tables.manifest.tsv'
$manifestPath = Join-Path $destination $manifestName
$tableFiles = @(Get-ChildItem $destination -Recurse -File -Filter '*.tsv' |
    Where-Object { $_.FullName -ne $manifestPath })
$relativePaths = [string[]]@($tableFiles | ForEach-Object {
    $_.FullName.Substring($destination.Length).TrimStart('\', '/').Replace('\', '/')
})
[Array]::Sort($relativePaths, [StringComparer]::Ordinal)

$manifestRows = [Collections.Generic.List[string]]::new()
$manifestRows.Add("# manifest-version`t1")
$manifestRows.Add("# path`tschema-version`trecord-count`tsha256")
foreach ($relativePath in $relativePaths) {
    $path = Join-Path $destination $relativePath.Replace('/', '\')
    $recordCount = 0
    foreach ($line in [IO.File]::ReadAllLines($path)) {
        if (-not [string]::IsNullOrWhiteSpace($line) -and
            -not $line.StartsWith('#')) {
            $recordCount++
        }
    }
    $sha256 = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestRows.Add("$relativePath`t1`t$recordCount`t$sha256")
}
[IO.File]::WriteAllLines(
    $manifestPath,
    $manifestRows,
    [Text.UTF8Encoding]::new($false))

