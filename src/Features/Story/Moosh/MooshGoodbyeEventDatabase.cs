using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_COMPANION_SPAWNER $67:$01 placement/predicate and the
/// SPECIALOBJECT_MOOSH state-$0a farewell in room $0:$6b.
/// </summary>
internal sealed class MooshGoodbyeEventDatabase
{
    private const string Path =
        "res://assets/oracle/cutscenes/moosh_goodbye_event.tsv";

    internal MooshGoodbyeEventRecord Record { get; }

    internal MooshGoodbyeEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Path,
            new GeneratedTableSchema(
                "room 0:6b Moosh goodbye",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "controller-id", "controller-subid",
                    "controller-y", "controller-x", "spawner-id",
                    "spawner-subid", "moosh-id", "moosh-y", "moosh-x",
                    "treasure-id", "moosh-state-address", "rescued-mask",
                    "left-mask", "disabled-objects", "menu-disabled",
                    "initial-animation", "flight-animation",
                    "initial-speed-z", "flight-gravity", "flight-speed",
                    "flight-angle", "exit-y", "text-id", "text-base64",
                    "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new MooshGoodbyeEventRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
            row.HexByte(3), row.HexByte(4), row.HexByte(5), row.HexByte(6),
            row.HexByte(7), row.HexByte(8), row.HexByte(9), row.HexByte(10),
            row.HexByte(11), row.HexWord(12), row.HexByte(13),
            row.HexByte(14), row.HexByte(15), row.HexByte(16),
            row.HexByte(17), row.HexByte(18), row.Decimal(19, -0x8000, 0x7fff),
            row.HexByte(20), row.HexByte(21), row.HexByte(22),
            row.HexByte(23), row.HexWord(24), row.Base64Utf8(25),
            row.RequiredString(26));
        Validate();
    }

    internal bool ShouldSpawn(
        int group,
        int room,
        OracleSaveData save,
        InventoryState inventory)
    {
        if (group != Record.Group || room != Record.Room ||
            !inventory.HasTreasure(Record.TreasureId))
        {
            return false;
        }

        int state = save.ReadWramByte(Record.MooshStateAddress);
        if ((state & Record.LeftMask) != 0)
            return false;
        if ((state & Record.RescuedMask) == 0)
        {
            throw new InvalidOperationException(
                "Room 0:6b INTERAC_COMPANION_SPAWNER $67:$01 reached " +
                "SPECIALOBJECT_MOOSH's unsupported pre-rescue state-$0a " +
                $"branch (wMooshState=${state:x2}).");
        }
        return true;
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 0, Room: 0x6b,
                ControllerId: 0x71, ControllerSubId: 0x01,
                ControllerY: 0x38, ControllerX: 0x08,
                SpawnerId: 0x67, SpawnerSubId: 0x01,
                MooshId: 0x0d, MooshY: 0x48, MooshX: 0x38,
                TreasureId: 0x52, MooshStateAddress: 0xc648,
                RescuedMask: 0x20, LeftMask: 0x40,
                DisabledObjects: 0x01, MenuDisabled: 0x01,
                InitialAnimation: 0x01, FlightAnimation: 0x0b,
                InitialSpeedZ: -0x0140, FlightGravity: 0x10,
                FlightSpeed: 0x28, FlightAngle: 0x10, ExitY: 0xf0,
                TextId: 0x2208
            } ||
            string.IsNullOrWhiteSpace(Record.Text) ||
            !Record.Text.Contains("See\nyou around.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Room 0:6b Moosh goodbye data diverges from the source contract.");
        }
    }
}

internal readonly record struct MooshGoodbyeEventRecord(
    int Group,
    int Room,
    int ControllerId,
    int ControllerSubId,
    int ControllerY,
    int ControllerX,
    int SpawnerId,
    int SpawnerSubId,
    int MooshId,
    int MooshY,
    int MooshX,
    int TreasureId,
    int MooshStateAddress,
    int RescuedMask,
    int LeftMask,
    int DisabledObjects,
    int MenuDisabled,
    int InitialAnimation,
    int FlightAnimation,
    int InitialSpeedZ,
    int FlightGravity,
    int FlightSpeed,
    int FlightAngle,
    int ExitY,
    int TextId,
    string Text,
    string Source);
