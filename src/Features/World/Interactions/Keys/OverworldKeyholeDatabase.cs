using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imports nextToOverworldKeyhole's collision-table entries, room-to-key
/// table, informative text, and INTERAC_OVERWORLD_KEY_SPRITE parameters.
/// Room-specific consequences remain owned by their event controllers.
/// </summary>
internal sealed class OverworldKeyholeDatabase
{

    private readonly Dictionary<(int Group, int Room), OverworldKeyholeDatabaseRecord> _records = new();
    private readonly HashSet<(int ActiveCollisions, byte Tile)> _tiles = new();

    internal int Count => _records.Count;
    internal int TileCount => _tiles.Count;
    internal ConstantsRecord Constants { get; }

    internal OverworldKeyholeDatabase()
    {
        GeneratedTable locations = GeneratedTable.Load(
            "res://assets/oracle/objects/overworld_keyholes.tsv",
            new GeneratedTableSchema(
                "overworld keyholes",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "treasure", "subid", "sprite", "tile-base",
                    "palette", "animation", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in locations.Rows)
        {
            OverworldKeyholeDatabaseRecord record = new OverworldKeyholeDatabaseRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.RequiredString(4),
                row.HexByte(5),
                row.HexByte(6),
                row.RequiredString(7),
                row.RequiredString(8));
            if (!_records.TryAdd((record.Group, record.Room), record))
            {
                throw new InvalidOperationException(
                    $"Duplicate overworld keyhole room {record.Group:x}:{record.Room:x2}.");
            }
        }

        GeneratedTable tiles = GeneratedTable.Load(
            "res://assets/oracle/metadata/overworld_keyhole_tiles.tsv",
            new GeneratedTableSchema(
                "overworld keyhole tiles",
                GeneratedTableKeySemantics.Unique,
                ["active-collisions", "tile", "parameter", "source"],
                ["active-collisions", "tile"],
                headerRequired: true));
        foreach (GeneratedTableRow row in tiles.Rows)
        {
            int collisions = row.Decimal(0, 0, 5);
            byte tile = (byte)row.HexByte(1);
            int parameter = row.HexByte(2);
            _ = row.RequiredString(3);
            if (parameter != 0x06)
                throw row.Invalid(2, "nextToOverworldKeyhole parameter $06");
            if (!_tiles.Add((collisions, tile)))
            {
                throw new InvalidOperationException(
                    $"Duplicate keyhole tile ${tile:x2} for collision set {collisions}.");
            }
        }

        GeneratedTableRow constants = GeneratedTable.Load(
            "res://assets/oracle/objects/overworld_keyhole_constants.tsv",
            new GeneratedTableSchema(
                "overworld keyhole constants",
                GeneratedTableKeySemantics.Ordered,
                [
                    "room-flag", "informative-mask", "push-counter", "open-sound",
                    "no-key-text-id", "no-key-utf8-base64", "interaction-id",
                    "first-key", "initial-speed-z", "gravity", "hold-frames", "source"
                ],
                headerRequired: true)).SingleRow();
        Constants = new ConstantsRecord(
            (byte)constants.HexByte(0),
            (byte)constants.HexByte(1),
            constants.UnsignedDecimal(2),
            constants.UnsignedDecimal(3),
            constants.HexWord(4),
            constants.Base64Utf8(5),
            constants.HexByte(6),
            constants.HexByte(7),
            constants.Decimal(8),
            constants.UnsignedDecimal(9),
            constants.UnsignedDecimal(10),
            constants.RequiredString(11));

        Validate();
    }

    internal bool TryGet(int group, int room, out OverworldKeyholeDatabaseRecord record) =>
        _records.TryGetValue((group, room), out record);

    internal bool IsKeyholeTile(int activeCollisions, byte tile) =>
        _tiles.Contains((activeCollisions, tile));

    private void Validate()
    {
        if (_records.Count != 6 || _tiles.Count != 3 ||
            !_records.TryGetValue((0, 0x5c), out OverworldKeyholeDatabaseRecord graveyard) ||
            graveyard is not
            {
                Treasure: TreasureDatabase.TreasureGraveyardKey,
                SubId: 0,
                Sprite: "spr_map_compass_keys_bookofseals",
                TileBase: 0x0e,
                Palette: 5
            } || string.IsNullOrWhiteSpace(graveyard.Animation) ||
            !_tiles.Contains((0, 0xec)) || !_tiles.Contains((1, 0xae)) ||
            !_tiles.Contains((4, 0xec)) ||
            Constants is not
            {
                RoomFlag: OracleSaveData.RoomFlag80,
                InformativeMask: 0x20,
                PushCounter: 20,
                OpenSound: OracleSoundEngine.SndOpenChest,
                NoKeyTextId: 0x5109,
                InteractionId: 0x18,
                FirstKey: 0x42,
                InitialSpeedZ: -0x200,
                Gravity: 0x28,
                HoldFrames: 0x3c
            } || string.IsNullOrWhiteSpace(Constants.NoKeyMessage))
        {
            throw new InvalidOperationException(
                "Imported Ages overworld-keyhole contract is incomplete.");
        }

        foreach (OverworldKeyholeDatabaseRecord record in _records.Values)
        {
            if (record.SubId != record.Treasure - Constants.FirstKey)
            {
                throw new InvalidOperationException(
                    $"Keyhole {record.Group:x}:{record.Room:x2} subid ${record.SubId:x2} " +
                    $"does not match treasure ${record.Treasure:x2}.");
            }
        }
    }
}

internal readonly record struct OverworldKeyholeDatabaseRecord(int Group, int Room, int Treasure, int SubId, string Sprite, int TileBase, int Palette, string Animation, string Source);

internal readonly record struct ConstantsRecord(byte RoomFlag, byte InformativeMask, int PushCounter, int OpenSound, int NoKeyTextId, string NoKeyMessage, int InteractionId, int FirstKey, int InitialSpeedZ, int Gravity, int HoldFrames, string Source);
