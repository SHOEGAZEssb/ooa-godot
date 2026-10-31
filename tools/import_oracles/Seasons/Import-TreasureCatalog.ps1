$catalog = Read-TreasureObjectCatalog 'seasons' $seasonsRomBytes $seasonsSymbols $seasonTreasureTexts
$seasonTreasureObjectRecords = $catalog.Records
Write-GeneratedTable((Join-Path (Split-Path $previewDestination -Parent) 'metadata/treasure_objects.tsv'), $catalog.Rows)
