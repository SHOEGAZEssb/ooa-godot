namespace Oracles;

internal static class ChestValidation
{
    internal static bool TryGetRecord(this ChestDatabase database, int group, int room,
        int position, out ChestRecord record)
    {
        foreach (ChestRecord entry in database.GetRoomRecords(group, room))
        {
            if (entry.Position != position) continue;
            record = entry;
            return true;
        }
        record = default;
        return false;
    }
}
