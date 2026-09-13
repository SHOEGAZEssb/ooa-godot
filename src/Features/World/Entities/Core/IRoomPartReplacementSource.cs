namespace oracleofages;

/// <summary>objectReplaceWithID retains the current PART page and defers
/// the replacement's state zero until the next part pass.</summary>
internal interface IRoomPartReplacementSource
{
    bool TryTakePartReplacement(out RoomEntitySpawn replacement);
}
