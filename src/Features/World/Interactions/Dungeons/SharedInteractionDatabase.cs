using System;
using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class SharedInteractionDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _records = new();
    internal IReadOnlyList<(int Room, byte Flag, int Bit)> CrystalFlags { get; }
    internal DungeonInteractionVisual MoblinFlag { get; }
    internal Color[,] PresentStatuePalette { get; } = OracleGraphicsData.LoadPalette(
        "res://assets/oracle/objects/maku_statue_present_palette.bin", 1);
    private readonly Lookup<int, DungeonObjectRecord> _npcTail = new();
    private readonly Dictionary<(int Group, int Room), MakuGashaRewardRecord> _gashaRewards = new();
    private readonly Dictionary<int, (int Position, string Message)> _entryTexts = new();

    internal SharedInteractionDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/shared_interactions.tsv",
            new GeneratedTableSchema("shared interaction placements", GeneratedTableKeySemantics.Unique,
                ["group", "room", "order", "kind", "id", "subid", "y", "x", "condition", "source"],
                ["group", "room", "order"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            var record = new DungeonObjectRecord(row.Decimal(0, 0, 7), row.HexByte(1), row.UnsignedDecimal(2),
                row.RequiredString(3) switch {
                    "shared-dungeon-script" => DungeonObjectKind.SharedDungeonScript,
                    "switch-trigger" => DungeonObjectKind.SwitchTrigger,
                    "restore-crystals" => DungeonObjectKind.RestoreCrystals,
                    "sarcophagus" => DungeonObjectKind.Sarcophagus,
                    "link-statue" => DungeonObjectKind.LinkStatue,
                    "portal-decoration" => DungeonObjectKind.PortalDecoration,
                    "moblin-flag" => DungeonObjectKind.MoblinFlag,
                    "entry-text" => DungeonObjectKind.EntryText,
                    "symmetry-barrier" => DungeonObjectKind.SymmetryBarrier,
                    "saved-monkeys" => DungeonObjectKind.SavedMonkeys,
                    "farore-memory" => DungeonObjectKind.FaroreMemory,
                    _ => throw row.Invalid(3, "a supported shared interaction") },
                row.HexByte(4), row.HexByte(5), row.HexByte(6), row.HexByte(7),
                DungeonObjectData.ParseCondition(row, 8), row.RequiredString(9));
            // These native suffixes follow the room's NPC prefix. The two
            // standalone statue rows have order 0 and precede any NPC.
            var destination = record.Order != 0 && record.Kind is DungeonObjectKind.LinkStatue or DungeonObjectKind.PortalDecoration or DungeonObjectKind.MoblinFlag or DungeonObjectKind.SavedMonkeys or DungeonObjectKind.FaroreMemory
                ? _npcTail : _records;
            destination.GetOrAdd(record.Group * 256 + record.Room).Add(record);
        }
        table = GeneratedTable.Load("res://assets/oracle/objects/dungeon_crystal_restore.tsv",
            new GeneratedTableSchema("D3 crystal switch restoration", GeneratedTableKeySemantics.Unique,
                ["room", "flag", "bit", "source"], ["room"], headerRequired: true));
        var flags = new List<(int, byte, int)>();
        foreach (var row in table.Rows) flags.Add((row.HexByte(0), (byte)row.HexByte(1), row.Decimal(2, 0, 7)));
        if (flags.Count != 4) throw new InvalidOperationException("INTERAC $21:$18 requires four crystal-room flags.");
        CrystalFlags = flags.AsReadOnly();
        var visual = GeneratedTable.Load("res://assets/oracle/objects/moblin_flag_decoration.tsv",
            new GeneratedTableSchema("King Moblin flag decoration $80:$01", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "source-grayscale-inverted", "animation", "source"], ["sprite"], headerRequired: true)).SingleRow();
        MoblinFlag = new("decoration-01", [visual.RequiredString(0)], visual.UnsignedDecimal(1),
            visual.Decimal(2, 0, 7), visual.Boolean01(3), [visual.RequiredString(4)]);
        table = GeneratedTable.Load("res://assets/oracle/objects/maku_gasha_rewards.tsv",
            new GeneratedTableSchema("room-load Maku Tree Gasha rewards", GeneratedTableKeySemantics.Unique,
                ["group", "room", "subid", "essence-mask", "position", "source"], ["group", "room"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            var reward = new MakuGashaRewardRecord(row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
                row.HexByte(3), row.HexByte(4), row.RequiredString(5));
            _gashaRewards.Add((reward.Group, reward.Room), reward);
        }
        table = GeneratedTable.Load("res://assets/oracle/objects/room_entry_text.tsv",
            new GeneratedTableSchema("native room-entry text", GeneratedTableKeySemantics.Unique,
                ["id", "position", "message-base64", "source"], ["id"], headerRequired: true));
        foreach (var row in table.Rows) _entryTexts.Add(row.HexWord(0), (row.Decimal(1, -1, 3), row.Base64Utf8(2)));
    }

    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group, int room) =>
        _records.ValuesOrEmpty(group * 256 + room);
    internal IReadOnlyList<DungeonObjectRecord> NpcTail(int group, int room) =>
        _npcTail.ValuesOrEmpty(group * 256 + room);
    internal bool TryGetGashaReward(int group, int room, out MakuGashaRewardRecord record) =>
        _gashaRewards.TryGetValue((group, room), out record);
    internal (int Position, string Message) EntryText(int room) => _entryTexts[room == 0xd0 ? 0x0209 : 0x120a];
    internal (int Id, int Position, string Message) SymmetryText(int companion)
    {
        int id = companion switch { 0x0b => 0x200a, 0x0c => 0x2109, 0x0d => 0x220a,
            _ => throw new InvalidOperationException($"companionScript_subid0d: invalid wAnimalCompanion ${companion:x2}.") };
        var text = _entryTexts[id]; return (id, text.Position, text.Message);
    }

    internal IReadOnlyList<CutsceneCommand> Script(int dungeon, int subid) =>
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/" + ((dungeon, subid) switch {
            (0 or 13, 1) => "makuPathScript_spawnDownStairsWhenEnemiesKilled",
            (0 or 13, 2) => "makuPathScript_spawn30Rupees",
            (0 or 13, 3) => "makuPathScript_keyFallsFromCeilingWhen1TorchLit",
            (3, 2) => "moonlitGrottoScript_spawnChestWhen2TorchesLit",
            _ => throw new InvalidOperationException($"Unmapped shared dungeon ${dungeon:x2} script ${subid:x2}.")
        }) + ".tsv");
}

internal readonly record struct MakuGashaRewardRecord(int Group, int Room, int Subid, int EssenceMask, int Position, string Source);
