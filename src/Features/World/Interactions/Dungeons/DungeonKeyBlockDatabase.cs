using System;

namespace oracleofages;

/// <summary>
/// Imported nextToKeyBlock contract for dungeon metatile $1e. Persistent
/// room-load replacement remains owned by StandardTileSubstitutionDatabase.
/// </summary>
internal sealed class DungeonKeyBlockDatabase
{
    private readonly ActiveCollisionModeSet _activeCollisionModes;

    internal DungeonKeyBlockDatabaseRecord Record { get; }

    internal DungeonKeyBlockDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_key_blocks.tsv",
            new GeneratedTableSchema(
                "dungeon key blocks",
                GeneratedTableKeySemantics.Unique,
                [
                    "closed-tile", "key-graphic", "open-tile", "room-flag",
                    "push-counter", "open-sound", "key-sound",
                    "no-key-text-id", "no-key-utf8-base64", "puff-sound",
                    "active-collisions", "source"
                ],
                ["closed-tile"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one dungeon key-block row, got {table.Rows.Count}.");
        }

        GeneratedTableRow row = table.Rows[0];
        Record = new DungeonKeyBlockDatabaseRecord(
            (byte)row.HexByte(0),
            row.HexByte(1),
            (byte)row.HexByte(2),
            (byte)row.HexByte(3),
            row.UnsignedDecimal(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.HexWord(7),
            row.Base64Utf8(8),
            row.UnsignedDecimal(9),
            row.RequiredString(11));
        _activeCollisionModes = ActiveCollisionModeSet.Parse(row, 10);
        if (Record.ClosedTile != 0x1e || Record.KeyGraphic != 0x42 ||
            Record.OpenTile != 0xa0 || Record.RoomFlag != 0x80 ||
            Record.PushCounter != 20 ||
            Record.OpenSound != OracleSoundEngine.SndOpenChest ||
            Record.KeySound != OracleSoundEngine.SndGetSeed ||
            Record.NoKeyTextId != 0x5102 ||
            string.IsNullOrWhiteSpace(Record.NoKeyMessage) ||
            Record.PuffSound != OracleSoundEngine.SndPoof ||
            !_activeCollisionModes.Contains(1) ||
            !_activeCollisionModes.Contains(2) ||
            !_activeCollisionModes.Contains(5) ||
            _activeCollisionModes.Contains(0) ||
            _activeCollisionModes.Contains(3) ||
            _activeCollisionModes.Contains(4) ||
            !Record.Source.Contains(
                "nextToKeyBlock", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Imported dungeon key-block $1e contract is incomplete.");
        }
    }

    internal bool SupportsActiveCollisions(int activeCollisions) =>
        _activeCollisionModes.Contains(activeCollisions);
}

internal readonly record struct DungeonKeyBlockDatabaseRecord(
    byte ClosedTile,
    int KeyGraphic,
    byte OpenTile,
    byte RoomFlag,
    int PushCounter,
    int OpenSound,
    int KeySound,
    int NoKeyTextId,
    string NoKeyMessage,
    int PuffSound,
    string Source);
