
namespace Oracles;

public sealed class ChestDatabase
{
    public string AssetRoot { get; }

    // constants/common/tileIndices.s: TILEINDEX_CHEST
    internal const byte ClosedTile = 0xf1;
    internal const byte OpenedTile = 0xf0;

    private readonly Lookup<int, ChestRecord> _roomRecords = new();

    public ChestDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "objects/chests.tsv",
            new GeneratedTableSchema(
                "chests",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "position", "treasure-object", "treasure-id",
                    "subid", "parameter", "text-id", "graphic", "amount", "utf8-base64"
                ],
                ["group", "room", "position"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            ChestRecord record = new ChestRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.RequiredString(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.HexByte(7),
                row.HexByte(8),
                row.UnsignedDecimal(9),
                row.Base64Utf8(10));
            _roomRecords.Add(
                MakeRoomKey(record.Group, record.Room), record);
        }
    }

    // bank0.getChestData compares only the room byte; the first match wins.
    public bool TryGetRoomChest(int group, int room, out ChestRecord record)
    {
        IReadOnlyList<ChestRecord> records = GetRoomRecords(group, room);
        record = records.Count == 0 ? default : records[0];
        return records.Count != 0;
    }

    public IReadOnlyList<ChestRecord> GetRoomRecords(int group, int room) =>
        _roomRecords.ValuesOrEmpty(MakeRoomKey(group, room));

    internal void ApplyOpenedChest(int group, OracleRoomData room, OracleSaveData save,
        OracleRuntimeState memory, long animationTick)
    {
        if (!save.HasRoomFlag(group, room.Id, OracleSaveData.RoomFlagItem)) return;
        if (!TryGetRoomChest(group, room.Id, out ChestRecord record))
        {
            // Missing rows leave E=$ff; wRoomLayout+$ff aliases live script scratch.
            memory.SetWramByte(0xcfff, OpenedTile);
            return;
        }
        room.SetPositionTileAndCollision(new((record.Position & 15) * 16 + 8,
            (record.Position >> 4) * 16 + 8), OpenedTile, null, animationTick);
    }

    private static int MakeRoomKey(int group, int room) => (group << 8) | room;
}

public readonly record struct ChestRecord(int Group, int Room, int Position, string TreasureObject, int TreasureId, int SubId, int Parameter, int TextId, int Graphic, int Amount, string Message);
