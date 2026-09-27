namespace oracleofages;

// Object.visible bit 6 enables bank0.s:_drawObjectTerrainEffects. Null means
// that the native handler has cleared that bit; altitude alone is insufficient.
internal interface ITerrainShadowSource
{
    int? TerrainShadowZHigh { get; }
}
