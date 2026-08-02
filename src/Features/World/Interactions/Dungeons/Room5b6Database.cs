using System;

namespace oracleofages;

/// <summary>
/// Source placement and script constants for room 5:b6's $6b:$0b Cheval
/// Rope interaction.
/// </summary>
internal sealed class Room5b6Database
{
    private const string Path =
        "res://assets/oracle/objects/room5b6_interactions.tsv";

    internal Room5b6InteractionRecord Record { get; }

    internal Room5b6Database()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Path,
            new GeneratedTableSchema(
                "room 5:b6 interactions",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "order", "id", "subid", "y", "x",
                    "var03", "item-room-flag", "treasure-object",
                    "treasure-id", "treasure-subid", "treasure-parameter",
                    "post-grant-wait", "collision-radius-y",
                    "collision-radius-x", "pickup-distance",
                    "remembered-id-value", "sprite", "tile-base",
                    "palette", "animation-index", "animation", "source"
                ],
                ["group", "room", "order"],
                headerRequired: true)).SingleRow();
        Record = new Room5b6InteractionRecord(
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
            row.HexByte(17),
            row.RequiredString(18),
            row.HexByte(19),
            row.HexByte(20),
            row.HexByte(21),
            row.RequiredString(22),
            row.RequiredString(23));
        if (Record is not
            {
                Group: 5,
                Room: 0xb6,
                Order: 0,
                Id: 0x6b,
                SubId: 0x0b,
                Y: 0x48,
                X: 0x28,
                Var03: 0x01,
                ItemRoomFlag: 0x20,
                TreasureObject: "TREASURE_OBJECT_CHEVAL_ROPE_00",
                TreasureId: 0x52,
                TreasureSubId: 0x00,
                TreasureParameter: 0x00,
                PostGrantWait: 30,
                CollisionRadiusY: 0x02,
                CollisionRadiusX: 0x02,
                PickupDistance: 0x0e,
                RememberedIdValue: 0x00,
                Sprite: "spr_quest_items_2",
                TileBase: 0x10,
                Palette: 0x03,
                AnimationIndex: 0x02
            } ||
            string.IsNullOrWhiteSpace(Record.Animation) ||
            Record.Source != "mainData.s:group5Mapb6ObjectData")
        {
            throw new InvalidOperationException(
                $"Invalid room 5:b6 $6b:$0b source record at " +
                $"{row.Path}:{row.LineNumber}.");
        }
    }
}

internal readonly record struct Room5b6InteractionRecord(
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
    int RememberedIdValue,
    string Sprite,
    int TileBase,
    int Palette,
    int AnimationIndex,
    string Animation,
    string Source);
