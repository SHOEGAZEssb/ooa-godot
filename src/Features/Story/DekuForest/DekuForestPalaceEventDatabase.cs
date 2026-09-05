using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Importer-owned contract for the Mystery Seeds escort through Ambi's palace.
/// Placed NPC rows remain the source of actor graphics and coordinates; this
/// record adds the native handoff values and the independently running scripts.
/// </summary>
internal sealed class DekuForestPalaceEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly NpcDatabase _npcs = new();

    internal DekuForestPalaceEventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> EntranceCommands { get; }
    internal IReadOnlyList<CutsceneCommand> CorridorCommands { get; }
    internal IReadOnlyList<CutsceneCommand> RewardGuardCommands { get; }
    internal IReadOnlyList<CutsceneCommand> EscortGuardCommands { get; }
    internal IReadOnlyList<CutsceneCommand> AmbiCommands { get; }
    internal IReadOnlyList<CutsceneCommand> NayruCommands { get; }
    internal IReadOnlyList<CutsceneCommand> ExitGuardCommands { get; }
    internal string InitialEscortAnimation { get; }
    internal Color[] PossessedNayruPalette { get; }

    internal DekuForestPalaceEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "deku_forest_palace_event.tsv",
            new GeneratedTableSchema(
                "Mystery Seeds Ambi palace escort",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "entrance-room", "corridor-room-1",
                    "corridor-room-2", "throne-room", "mystery-seeds",
                    "entrance-flag", "completion-flag", "normal-speed",
                    "stairs-speed", "slow-speed", "flight-speed",
                    "side-guard-trigger-y", "side-guard-move-frames",
                    "reward-jump-delay", "reward-jump-speed-z",
                    "reward-jump-gravity", "reward-land-delay",
                    "exit-idle-frames", "exit-down-frames", "fade-delay",
                    "fade-frames", "textbox-flags", "reward-treasure",
                    "reward-subid", "reward-object", "reward-parameter",
                    "exit-player-y", "exit-player-x", "terminal-text-id",
                    "terminal-text-base64", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new DekuForestPalaceEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexByte(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.UnsignedDecimal(14),
            row.Decimal(15),
            row.HexByte(16),
            row.UnsignedDecimal(17),
            row.UnsignedDecimal(18),
            row.UnsignedDecimal(19),
            row.UnsignedDecimal(20),
            row.UnsignedDecimal(21),
            row.HexByte(22),
            row.HexByte(23),
            row.HexByte(24),
            row.RequiredString(25),
            row.HexByte(26),
            row.HexByte(27),
            row.HexByte(28),
            row.HexWord(29),
            row.Base64Utf8(30),
            row.RequiredString(31));

        EntranceCommands = LoadCommands("deku_forest_palace_entrance_commands.tsv");
        CorridorCommands = LoadCommands("deku_forest_palace_corridor_commands.tsv");
        RewardGuardCommands = LoadCommands("deku_forest_palace_reward_guard_commands.tsv");
        EscortGuardCommands = LoadCommands("deku_forest_palace_escort_guard_commands.tsv");
        AmbiCommands = LoadCommands("deku_forest_palace_ambi_commands.tsv");
        NayruCommands = LoadCommands("deku_forest_palace_nayru_commands.tsv");
        ExitGuardCommands = LoadCommands("deku_forest_palace_exit_guard_commands.tsv");
        InitialEscortAnimation = ResolveInitialEscortAnimation();
        PossessedNayruPalette = OracleGraphicsData.LoadPaletteColors(
            "res://assets/oracle/metadata/nayru_possessed_palette.bin",
            transparentZero: true);
        Validate();
    }

    internal IReadOnlyList<NpcRecord> RoomActors(int room) =>
        _npcs.GetRoomNpcs(Record.Group, room);

    internal NpcRecord RequireActor(int room, int id, int subId, int occurrence = 0)
    {
        NpcRecord[] matches = RoomActors(room)
            .Where(record => record.Id == id && record.SubId == subId)
            .ToArray();
        if (occurrence < 0 || occurrence >= matches.Length)
        {
            throw new InvalidOperationException(
                $"Room {Record.Group:x}:{room:x2} has no imported actor " +
                $"${id:x2}:${subId:x2} occurrence {occurrence}.");
        }
        return matches[occurrence] with
        {
            Implementation = NpcImplementationClassification.EventOwned
        };
    }

    internal NpcRecord CreateDirectExitGuard(
        int subId,
        int y,
        int x,
        int textId,
        string message)
    {
        NpcRecord template = RequireActor(Record.EntranceRoom, 0x40, 0x02);
        return template with
        {
            SubId = subId,
            Y = y,
            X = x,
            TextId = textId,
            Message = message
        };
    }

    private static IReadOnlyList<CutsceneCommand> LoadCommands(string name) =>
        CutsceneCommandCatalog.Load(Root + name);

    private string ResolveInitialEscortAnimation()
    {
        if (CorridorCommands.Count == 0 ||
            CorridorCommands[0] is not CutsceneMoveCommand
            {
                Actor: "CorridorGuard",
                Angle: 0x00,
                EncodedAnimation: var corridorAnimation
            } ||
            EscortGuardCommands.Count < 2 ||
            EscortGuardCommands[1] is not CutsceneMoveCommand
            {
                Actor: "EscortGuard",
                Angle: 0x00,
                EncodedAnimation: var throneAnimation
            } ||
            corridorAnimation != throneAnimation)
        {
            throw new InvalidOperationException(
                "soldierSubid05/06 did not begin with the same imported " +
                "animation-$00 moveup command.");
        }
        return corridorAnimation;
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 1,
                EntranceRoom: 0x46,
                CorridorRoom1: 0x36,
                CorridorRoom2: 0x26,
                ThroneRoom: 0x16,
                MysterySeeds: 0x24,
                EntranceFlag: 0x10,
                CompletionFlag: OracleSaveData.GlobalFlag0b,
                NormalSpeed: 0x28,
                StairsSpeed: 0x19,
                SlowSpeed: 0x14,
                FlightSpeed: 0x50,
                SideGuardTriggerY: 0x60,
                SideGuardMoveFrames: 0x10,
                RewardJumpDelay: 30,
                RewardJumpSpeedZ: -448,
                RewardJumpGravity: 0x20,
                RewardLandDelay: 8,
                ExitIdleFrames: 30,
                ExitDownFrames: 40,
                FadeDelay: 3,
                FadeFrames: 97,
                TextboxFlags: 0x04,
                RewardTreasure: TreasureDatabase.TreasureBombs,
                RewardSubId: 0x02,
                RewardObject: "TREASURE_OBJECT_BOMBS_02",
                RewardParameter: 0x10,
                ExitPlayerY: 0x38,
                ExitPlayerX: 0x50,
                TerminalTextId: 0x5909
            } ||
            EntranceCommands.Count == 0 ||
            CorridorCommands.Count == 0 ||
            RewardGuardCommands.Count == 0 ||
            EscortGuardCommands.Count == 0 ||
            AmbiCommands.Count == 0 ||
            NayruCommands.Count == 0 ||
            ExitGuardCommands.Count == 0 ||
            PossessedNayruPalette.Length != 4)
        {
            throw new InvalidOperationException(
                "Mystery Seeds palace event metadata diverged from the imported source contract.");
        }

        _ = RequireActor(Record.EntranceRoom, 0x40, 0x02);
        _ = RequireActor(Record.EntranceRoom, 0x40, 0x09);
        _ = RequireActor(Record.EntranceRoom, 0x40, 0x0b);
        _ = RequireActor(Record.CorridorRoom1, 0x40, 0x05);
        for (int occurrence = 0; occurrence < 4; occurrence++)
            _ = RequireActor(Record.CorridorRoom1, 0x40, 0x03, occurrence);
        _ = RequireActor(Record.CorridorRoom2, 0x40, 0x05);
        _ = RequireActor(Record.ThroneRoom, 0x40, 0x04);
        _ = RequireActor(Record.ThroneRoom, 0x40, 0x06);
        _ = RequireActor(Record.ThroneRoom, 0x4d, 0x00);
        _ = RequireActor(Record.ThroneRoom, 0x36, 0x01);
    }
}

internal readonly record struct DekuForestPalaceEventRecord(
    int Group,
    int EntranceRoom,
    int CorridorRoom1,
    int CorridorRoom2,
    int ThroneRoom,
    int MysterySeeds,
    int EntranceFlag,
    int CompletionFlag,
    int NormalSpeed,
    int StairsSpeed,
    int SlowSpeed,
    int FlightSpeed,
    int SideGuardTriggerY,
    int SideGuardMoveFrames,
    int RewardJumpDelay,
    int RewardJumpSpeedZ,
    int RewardJumpGravity,
    int RewardLandDelay,
    int ExitIdleFrames,
    int ExitDownFrames,
    int FadeDelay,
    int FadeFrames,
    int TextboxFlags,
    int RewardTreasure,
    int RewardSubId,
    string RewardObject,
    int RewardParameter,
    int ExitPlayerY,
    int ExitPlayerX,
    int TerminalTextId,
    string TerminalText,
    string Source);
