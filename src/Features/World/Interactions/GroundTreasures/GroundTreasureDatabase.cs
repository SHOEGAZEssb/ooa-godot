using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Positioned treasures created by invisible room interactions. The source
/// interaction conditionally creates INTERAC_TREASURE $60 from an imported
/// treasure-object record, then deletes itself.
/// </summary>
internal sealed class GroundTreasureDatabase
{
    private readonly Lookup<
        (int Group, int Room),
        GroundTreasureDatabaseRecord> _byRoom = new();

    public GroundTreasureDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/ground_treasures.tsv",
            new GeneratedTableSchema(
                "ground treasures",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "y", "x", "treasure-object", "sprite",
                    "tile-base", "palette", "animation", "completion-text-id",
                    "completion-text-base64", "require-room-item-clear",
                    "set-room-item", "state-address", "state-mask", "state-value",
                    "require-treasure-clear", "spawn-mode", "grab-mode",
                    "initial-speed-z", "gravity", "move-speed", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            GroundTreasureDatabaseRecord record = new GroundTreasureDatabaseRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                row.HexByte(4),
                row.RequiredString(5),
                row.RequiredString(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.RequiredString(9),
                row.HexWord(10),
                row.Base64Utf8(11),
                row.RequiredString(23),
                SpawnMode: row.UnsignedDecimal(18),
                GrabMode: row.UnsignedDecimal(19),
                Gravity: row.UnsignedDecimal(21),
                RoomFlagTiming: row.Boolean01(13)
                    ? GroundTreasureRoomFlagTiming.OnActivation
                    : GroundTreasureRoomFlagTiming.Never,
                RequireRoomItemClear: row.Boolean01(12),
                StateAddress: row.HexWord(14),
                StateMask: row.HexByte(15),
                StateValue: row.HexByte(16),
                RequireTreasureClear: row.HexByte(17),
                BuriedInitialSpeedZ: row.Decimal(
                    20, short.MinValue, short.MaxValue),
                BuriedMoveSpeed: row.HexByte(22));
            if (record.Group is < 0 or > 7 || record.Room is < 0 or > 0xff ||
                record.Order < 0 || record.Y is < 0 or > 0xff ||
                record.X is < 0 or > 0xff ||
                string.IsNullOrWhiteSpace(record.TreasureObject) ||
                string.IsNullOrWhiteSpace(record.Sprite) ||
                string.IsNullOrWhiteSpace(record.Animation) ||
                record.SpawnMode is not (0 or 5) ||
                record.GrabMode is not (1 or 2) ||
                record.StateValue != (record.StateValue & record.StateMask) ||
                record.StateAddress == 0 &&
                    (record.StateMask != 0 || record.StateValue != 0) ||
                record.StateAddress != 0 && record.StateMask == 0 ||
                record.SpawnMode == 0 &&
                    (record.BuriedInitialSpeedZ != 0 ||
                     record.Gravity != 0 || record.BuriedMoveSpeed != 0) ||
                record.SpawnMode == 5 &&
                    (record.BuriedInitialSpeedZ >= 0 ||
                     record.Gravity <= 0 || record.BuriedMoveSpeed == 0) ||
                record.TreasureObject == "TREASURE_OBJECT_HEART_PIECE_00" &&
                    (record.CompletionTextId != 0x0049 ||
                     string.IsNullOrWhiteSpace(record.CompletionMessage)) ||
                record.TreasureObject != "TREASURE_OBJECT_HEART_PIECE_00" &&
                    (record.CompletionTextId != 0 ||
                     !string.IsNullOrEmpty(record.CompletionMessage)))
            {
                throw new InvalidOperationException(
                    $"Invalid ground-treasure row at {row.Path}:{row.LineNumber}.");
            }

            _byRoom.Add((record.Group, record.Room), record);
            count++;
        }

        if (count != 9)
            throw new InvalidOperationException(
                $"Expected eight $dc:$07 Heart Pieces and room 0:98 " +
                $"Ricky's Gloves, loaded {count}.");
        _byRoom.SortValues(
            static (left, right) => left.Order.CompareTo(right.Order));
    }

    public IReadOnlyList<GroundTreasureDatabaseRecord> GetRoomRecords(int group, int room) =>
        _byRoom.ValuesOrEmpty((group, room));

    internal static bool ShouldSpawn(
        GroundTreasureDatabaseRecord record,
        OracleSaveData? save,
        InventoryState? inventory)
    {
        if (save is null)
            return false;
        if (record.RequireRoomItemClear &&
            save.HasRoomFlag(
                record.Group, record.Room, OracleSaveData.RoomFlagItem))
        {
            return false;
        }
        if (record.StateAddress != 0 &&
            (save.ReadWramByte(record.StateAddress) & record.StateMask) !=
                record.StateValue)
        {
            return false;
        }
        return record.RequireTreasureClear == 0 ||
            !(inventory?.HasTreasure(record.RequireTreasureClear) ??
                save.HasTreasure(record.RequireTreasureClear));
    }
}

internal readonly record struct GroundTreasureDatabaseRecord(
    int Group,
    int Room,
    int Order,
    int Y,
    int X,
    string TreasureObject,
    string Sprite,
    int TileBase,
    int Palette,
    string Animation,
    int CompletionTextId,
    string CompletionMessage,
    string Source,
    int SpawnMode = 0,
    int GrabMode = 2,
    int SpawnDelayFrames = 0,
    int InitialZPixels = 0,
    int BounceCount = 0,
    int Gravity = 0,
    int BounceSpeed = 0,
    int SpawnSound = 0,
    int LandingSound = 0,
    bool InitialZAboveScreen = false,
    int AboveScreenMargin = 8,
    int AboveScreenFallback = -128,
    int TextboxFlags = 0,
    GroundTreasureInventoryWrite InventoryWrite =
        GroundTreasureInventoryWrite.TreasureObject,
    int InventoryParameter = -1,
    GroundTreasureRoomFlagTiming RoomFlagTiming =
        GroundTreasureRoomFlagTiming.OnActivation,
    GroundTreasureSoundOrder SoundOrder =
        GroundTreasureSoundOrder.BehaviourThenGrab,
    GroundTreasureDialogueTiming DialogueTiming =
        GroundTreasureDialogueTiming.BeforeGrab,
    GroundTreasureCompletionOwner CompletionOwner =
        GroundTreasureCompletionOwner.SharedInteraction,
    int? TextboxPosition = null,
    bool RequireRoomItemClear = false,
    int StateAddress = 0,
    int StateMask = 0,
    int StateValue = 0,
    int RequireTreasureClear = 0,
    int BuriedInitialSpeedZ = 0,
    int BuriedMoveSpeed = 0);
