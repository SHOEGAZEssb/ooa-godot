using System;

namespace oracleofages;

internal static class DeathRespawnInitialization
{
    private static readonly Lazy<GeneratedTable> Rules = new(() => GeneratedTable.Load(
        "res://assets/oracle/metadata/death_respawn_initialization.tsv",
        new GeneratedTableSchema("saved checkpoint initialization", GeneratedTableKeySemantics.Unique,
            columns: ["source_group", "source_room", "minimum_x", "write_mask", "group", "room", "modifier", "facing", "y", "x", "companion_id", "source"],
            keyColumns: ["source_group", "source_room"],
            headerRequired: true)));

    internal static void Apply(OracleSaveData save, byte gameboyType = 0x01)
    {
        // hGameboyType bit7 is the native GBA gate. The ordinary port session
        // uses the CGB gameplay profile; presentation brightness is separate.
        if ((gameboyType & 0x80) != 0) return;
        foreach (GeneratedTableRow row in Rules.Value.Rows)
        {
            if (save.RespawnGroup != row.HexByte(0) || save.RespawnRoom != row.HexByte(1) ||
                save.RespawnX < row.HexByte(2)) continue;
            save.ApplyDeathRespawnPreset((byte)row.HexByte(3),
                [(byte)row.HexByte(4), (byte)row.HexByte(5), (byte)row.HexByte(6), (byte)row.HexByte(7),
                 (byte)row.HexByte(8), (byte)row.HexByte(9), (byte)row.HexByte(10)]);
            return;
        }
    }
}
