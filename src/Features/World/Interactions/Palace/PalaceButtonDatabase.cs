using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class PalaceButtonDatabase
{
    private readonly PalaceButtonRecord[] _records = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/palace_buttons.tsv",
        new GeneratedTableSchema("palace buttons", GeneratedTableKeySemantics.Unique,
            ["group", "room", "subid", "y", "x", "gate-position", "angle", "source"], ["group", "room"], headerRequired: true))
        .Rows.Select(row => new PalaceButtonRecord(row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
            new Vector2(row.HexByte(4), row.HexByte(3)), (byte)row.HexByte(5), row.HexByte(6), row.RequiredString(7))).ToArray();
    internal IEnumerable<PalaceButtonRecord> GetRoomRecords(int group, int room) => _records.Where(record => record.Group == group && record.Room == room);
    internal PalaceButtonRecord Subid(int subid) => _records.Single(record => record.Subid == subid);
}

internal sealed record PalaceButtonRecord(int Group, int Room, int Subid, Vector2 Position, byte GatePosition, int Angle, string Source);
