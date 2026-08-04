namespace oracleofages;

/// <summary>
/// Receives a successful breakable-tile dig after the room layout has changed.
/// </summary>
internal interface IDugTileRoomEntity
{
    void NotifyTileDug(int packedPosition);
}
