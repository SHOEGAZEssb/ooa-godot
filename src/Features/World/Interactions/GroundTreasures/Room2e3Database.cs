using System;

namespace oracleofages;

/// <summary>
/// Source placement and script constants for room 2:e3's $6b:$0a Bombs
/// recovery interaction.
/// </summary>
internal sealed class Room2e3Database
{
    private const string Path =
        "res://assets/oracle/objects/room2e3_interactions.tsv";

    internal Room2e3InteractionRecord Record { get; }

    internal Room2e3Database()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Path,
            new GeneratedTableSchema(
                "room 2:e3 interactions",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "order", "id", "subid", "y", "x",
                    "var03", "item-room-flag", "treasure-object",
                    "treasure-id", "treasure-subid", "treasure-parameter",
                    "post-grant-wait", "collision-radius-y",
                    "collision-radius-x", "pickup-distance", "sprite",
                    "tile-base", "palette", "animation-index", "animation",
                    "source"
                ],
                ["group", "room", "order"],
                headerRequired: true)).SingleRow();
        Record = new Room2e3InteractionRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.Decimal(2, 0, 0),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexByte(8),
            row.RequiredString(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.Decimal(13, 1, 255),
            row.HexByte(14),
            row.HexByte(15),
            row.HexByte(16),
            row.RequiredString(17),
            row.HexByte(18),
            row.HexByte(19),
            row.HexByte(20),
            row.RequiredString(21),
            row.RequiredString(22));
        if (Record is not
            {
                Group: 2,
                Room: 0xe3,
                Order: 0,
                Id: 0x6b,
                SubId: 0x0a,
                Y: 0x28,
                X: 0x28,
                Var03: 0x00,
                ItemRoomFlag: 0x20,
                TreasureObject: "TREASURE_OBJECT_BOMBS_04",
                TreasureId: 0x03,
                TreasureSubId: 0x04,
                TreasureParameter: 0x00,
                PostGrantWait: 30,
                CollisionRadiusY: 0x02,
                CollisionRadiusX: 0x02,
                PickupDistance: 0x0e,
                Sprite: "spr_common_items",
                TileBase: 0x10,
                Palette: 0x04,
                AnimationIndex: 0x01
            } ||
            string.IsNullOrWhiteSpace(Record.Animation) ||
            Record.Source != "mainData.s:group2Mape3ObjectData")
        {
            throw new InvalidOperationException(
                $"Invalid room 2:e3 $6b:$0a source record at " +
                $"{row.Path}:{row.LineNumber}.");
        }
    }
}

internal readonly record struct Room2e3InteractionRecord(
    int Group,
    int Room,
    int Order,
    int Id,
    int SubId,
    int Y,
    int X,
    int Var03,
    int ItemRoomFlag,
    string TreasureObject,
    int TreasureId,
    int TreasureSubId,
    int TreasureParameter,
    int PostGrantWait,
    int CollisionRadiusY,
    int CollisionRadiusX,
    int PickupDistance,
    string Sprite,
    int TileBase,
    int Palette,
    int AnimationIndex,
    string Animation,
    string Source);
