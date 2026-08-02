using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-ordered placements and native constants for the Mermaid's Cave
/// flippers room (5:bf).
/// </summary>
internal sealed class Room5bfDatabase
{
    private const string Root = "res://assets/oracle/objects/";

    internal IReadOnlyList<Room5bfInteractionRecord> Records { get; }
    internal Room5bfConstants Constants { get; }
    internal Color[] BlockPalette { get; }

    internal Room5bfDatabase()
    {
        GeneratedTable placements = GeneratedTable.Load(
            Root + "room5bf_interactions.tsv",
            new GeneratedTableSchema(
                "room 5:bf interactions",
                GeneratedTableKeySemantics.Unique,
                [
                    "order", "kind", "id", "subid", "y", "x", "var03",
                    "sprite", "tile-base", "palette", "animation-index",
                    "animation", "source"
                ],
                ["order"],
                headerRequired: true));
        if (placements.Rows.Count != 5)
        {
            throw new InvalidOperationException(
                $"Expected five room 5:bf interaction records, got " +
                $"{placements.Rows.Count}.");
        }

        var records = new List<Room5bfInteractionRecord>(5);
        for (int index = 0; index < placements.Rows.Count; index++)
        {
            GeneratedTableRow row = placements.Rows[index];
            int order = row.Decimal(0, 0, 4);
            if (order != index)
                throw row.Invalid(0, $"source order {index}");
            Room5bfInteractionKind kind = row.RequiredString(1) switch
            {
                "flippers" => Room5bfInteractionKind.Flippers,
                "sliding-block" => Room5bfInteractionKind.SlidingBlock,
                "lever" => Room5bfInteractionKind.Lever,
                "lever-connection" => Room5bfInteractionKind.LeverConnection,
                _ => throw row.Invalid(
                    1, "flippers, sliding-block, lever, or lever-connection")
            };
            string animationCell = row.RequiredString(11);
            string[] animations = kind == Room5bfInteractionKind.LeverConnection
                ? animationCell.Split('^', StringSplitOptions.None)
                : [animationCell];
            if (animations.Length !=
                (kind == Room5bfInteractionKind.LeverConnection ? 5 : 1) ||
                Array.Exists(animations, string.IsNullOrWhiteSpace))
            {
                throw row.Invalid(
                    11,
                    kind == Room5bfInteractionKind.LeverConnection
                        ? "five nonempty animations"
                        : "one nonempty animation");
            }
            records.Add(new Room5bfInteractionRecord(
                order,
                kind,
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.RequiredString(7),
                row.HexByte(8),
                row.HexByte(9),
                row.HexByte(10),
                Array.AsReadOnly(animations),
                row.RequiredString(12)));
        }
        Records = records.AsReadOnly();

        ValidatePlacementContract(records);

        GeneratedTableRow constants = GeneratedTable.Load(
            Root + "room5bf_constants.tsv",
            new GeneratedTableSchema(
                "room 5:bf interaction constants",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "item-room-flag", "treasure-id",
                    "treasure-subid", "treasure-parameter", "lever-length",
                    "pull-speed", "lever-radius-y", "lever-radius-x",
                    "link-y-offset", "block-radius", "distance-mask",
                    "distance-shift", "squish-y", "squish-x", "squish-range",
                    "connection-step", "post-grant-wait", "pickup-radius",
                    "move-sound", "full-sound", "source"
                ],
                ["group", "room"],
                headerRequired: true)).SingleRow();
        Constants = new Room5bfConstants(
            constants.Decimal(0, 0, 7),
            constants.HexByte(1),
            constants.HexByte(2),
            constants.HexByte(3),
            constants.HexByte(4),
            constants.HexByte(5),
            constants.HexByte(6),
            constants.HexByte(7),
            constants.HexByte(8),
            constants.HexByte(9),
            constants.HexByte(10),
            constants.HexByte(11),
            constants.HexByte(12),
            constants.HexByte(13),
            constants.HexByte(14),
            constants.HexByte(15),
            constants.HexByte(16),
            constants.HexByte(17),
            constants.Decimal(18, 1, 255),
            constants.HexByte(19),
            constants.HexByte(20),
            constants.HexByte(21),
            constants.RequiredString(22));
        if (Constants is not
            {
                Group: 5,
                Room: 0xbf,
                ItemRoomFlag: 0x20,
                TreasureId: 0x2e,
                TreasureSubId: 0x00,
                TreasureParameter: 0x00,
                LeverLength: 0x40,
                PullSpeed: 0x0a,
                LeverRadiusY: 0x05,
                LeverRadiusX: 0x01,
                LinkYOffset: 0x0c,
                BlockRadius: 0x06,
                DistanceMask: 0x7c,
                DistanceShift: 0x02,
                SquishY: 0x38,
                SquishX: 0xb8,
                SquishRange: 0x08,
                ConnectionStep: 0x10,
                PostGrantWait: 30,
                PickupRadius: 0x02,
                MoveSound: OracleSoundEngine.SndMoveBlock,
                FullSound: OracleSoundEngine.SndOpenChest
            })
        {
            throw new InvalidOperationException(
                $"Invalid room 5:bf interaction constants at " +
                $"{constants.Path}:{constants.LineNumber}.");
        }

        byte[] palette = FileAccess.GetFileAsBytes(
            Root + "room5bf_block_palette.bin");
        if (palette.Length != 12)
        {
            throw new InvalidOperationException(
                $"PALH_a3 room 5:bf block palette should contain 12 bytes, " +
                $"got {palette.Length}.");
        }
        BlockPalette = new Color[4];
        for (int color = 0; color < BlockPalette.Length; color++)
        {
            int offset = color * 3;
            BlockPalette[color] = new Color(
                palette[offset] / 31.0f,
                palette[offset + 1] / 31.0f,
                palette[offset + 2] / 31.0f,
                color == 0 ? 0.0f : 1.0f);
        }
    }

    private static void ValidatePlacementContract(
        IReadOnlyList<Room5bfInteractionRecord> records)
    {
        int[] expectedIds = [0x6b, 0x6b, 0x6b, 0x61, 0x61];
        int[] expectedSubIds = [0x0c, 0x0d, 0x0d, 0x30, 0x80];
        int[] expectedY = [0x1c, 0x38, 0x38, 0x10, 0x10];
        int[] expectedX = [0xb8, 0xb0, 0xc0, 0x78, 0x78];
        int[] expectedVar03 = [0x02, 0x00, 0x01, 0x00, 0x00];
        string[] expectedSprites =
        [
            "spr_quest_items_5", "spr_common_sprites", "spr_common_sprites",
            "spr_dungeon_sprites", "spr_dungeon_sprites"
        ];
        int[] expectedTileBases = [0x04, 0x36, 0x36, 0x0a, 0x0a];
        int[] expectedPalettes = [0x05, 0x06, 0x06, 0x03, 0x03];
        for (int index = 0; index < records.Count; index++)
        {
            Room5bfInteractionRecord record = records[index];
            if (record.Id != expectedIds[index] ||
                record.SubId != expectedSubIds[index] ||
                record.Y != expectedY[index] ||
                record.X != expectedX[index] ||
                record.Var03 != expectedVar03[index] ||
                record.Sprite != expectedSprites[index] ||
                record.TileBase != expectedTileBases[index] ||
                record.Palette != expectedPalettes[index] ||
                string.IsNullOrWhiteSpace(record.Source))
            {
                throw new InvalidOperationException(
                    $"Invalid room 5:bf source record at order {index}: " +
                    $"${record.Id:x2}:${record.SubId:x2} from {record.Source}.");
            }
        }
    }
}

internal enum Room5bfInteractionKind
{
    Flippers,
    SlidingBlock,
    Lever,
    LeverConnection
}

internal readonly record struct Room5bfInteractionRecord(
    int Order,
    Room5bfInteractionKind Kind,
    int Id,
    int SubId,
    int Y,
    int X,
    int Var03,
    string Sprite,
    int TileBase,
    int Palette,
    int AnimationIndex,
    IReadOnlyList<string> Animations,
    string Source)
{
    internal NpcRecord ToNpcRecord(int animationSlot = 0)
    {
        if (animationSlot < 0 || animationSlot >= Animations.Count)
            throw new ArgumentOutOfRangeException(nameof(animationSlot));
        string animation = Animations[animationSlot];
        return new NpcRecord(
            5,
            0xbf,
            Id,
            SubId,
            Y,
            X,
            Var03,
            0,
            Sprite,
            TileBase,
            Palette,
            AnimationIndex + animationSlot,
            false,
            animation,
            animation,
            animation,
            animation,
            string.Empty,
            NpcImplementationClassification.SpecializedNative);
    }
}

internal readonly record struct Room5bfConstants(
    int Group,
    int Room,
    int ItemRoomFlag,
    int TreasureId,
    int TreasureSubId,
    int TreasureParameter,
    int LeverLength,
    int PullSpeed,
    int LeverRadiusY,
    int LeverRadiusX,
    int LinkYOffset,
    int BlockRadius,
    int DistanceMask,
    int DistanceShift,
    int SquishY,
    int SquishX,
    int SquishRange,
    int ConnectionStep,
    int PostGrantWait,
    int PickupRadius,
    int MoveSound,
    int FullSound,
    string Source);
