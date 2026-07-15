using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

public sealed class NpcDatabase
{
    private readonly Dictionary<int, List<NpcRecord>> _byRoom = new();
    private readonly Dictionary<int, List<FamilyNpcRecord>> _familyByRoom = new();
    private readonly BipinBlossomFamilyInteractionDatabase _familyInteractions = new();

    public NpcDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/objects/npcs.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 18)
                throw new InvalidOperationException($"Malformed NPC data row: {line}");

            NpcRecord record = ParseNpcRecord(columns, selectorColumns: 0);

            int key = MakeKey(record.Group, record.Room);
            if (!_byRoom.TryGetValue(key, out List<NpcRecord>? records))
            {
                records = new List<NpcRecord>();
                _byRoom.Add(key, records);
            }
            records.Add(record);
        }

        source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/bipin_blossom_family.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 20)
                throw new InvalidOperationException($"Malformed family NPC data row: {line}");

            NpcRecord record = ParseNpcRecord(columns, selectorColumns: 2);
            var familyRecord = new FamilyNpcRecord(
                int.Parse(columns[2]),
                int.Parse(columns[3]),
                record);
            int key = MakeKey(record.Group, record.Room);
            if (!_familyByRoom.TryGetValue(key, out List<FamilyNpcRecord>? records))
            {
                records = new List<FamilyNpcRecord>();
                _familyByRoom.Add(key, records);
            }
            records.Add(familyRecord);
        }
    }

    public IReadOnlyList<NpcRecord> GetRoomNpcs(int group, int room)
    {
        return _byRoom.TryGetValue(MakeKey(group, room), out List<NpcRecord>? records)
            ? records
            : Array.Empty<NpcRecord>();
    }

    internal IReadOnlyList<NpcRecord> GetRoomNpcs(
        int group,
        int room,
        OracleSaveData? save,
        OracleRuntimeState runtime)
    {
        int key = MakeKey(group, room);
        IReadOnlyList<NpcRecord> placed = GetRoomNpcs(group, room);
        if (!_familyByRoom.TryGetValue(key, out List<FamilyNpcRecord>? family))
            return placed;
        if (save is not null && save.HasGlobalFlag(OracleSaveData.GlobalFlagFinishedGame))
            return placed;

        if (save is not null)
            AdvanceFamilyStage(save, runtime);
        int stage = save is null
            ? 0
            : Math.Clamp((int)save.ReadWramByte(OracleSaveData.ChildStageAddress), 0, 9);
        int personality = stage < 4 || save is null
            ? -1
            : save.ReadWramByte(OracleSaveData.ChildPersonalityAddress);

        var result = new List<NpcRecord>(placed.Count + family.Count);
        result.AddRange(placed);
        foreach (FamilyNpcRecord candidate in family)
        {
            if (candidate.Stage == stage && candidate.Personality == personality)
            {
                NpcRecord record = candidate.Record;
                if (save is not null)
                {
                    record = record with
                    {
                        Message = BipinBlossomFamilyInteractionDatabase.SubstituteChildName(
                            record.Message, save)
                    };
                    if (stage == 0 && save.ChildNamed &&
                        record.Id == 0x28 && record.SubId == 0x00)
                    {
                        var dialogue = _familyInteractions.Text(0x4301, save);
                        record = record with
                        {
                            TextId = dialogue.TextId,
                            Message = dialogue.Message
                        };
                    }
                    else if (stage == 0 && save.ChildNamed &&
                        record.Id == 0x2b && record.SubId == 0x00)
                    {
                        var dialogue = _familyInteractions.Text(0x4409, save);
                        record = record with
                        {
                            TextId = dialogue.TextId,
                            Message = dialogue.Message
                        };
                    }
                }
                result.Add(record);
            }
        }
        return result;
    }

    private static void AdvanceFamilyStage(
        OracleSaveData save,
        OracleRuntimeState runtime)
    {
        int refillBits = runtime.ReadWramByte(
            OracleRuntimeState.SeedTreeRefilledBitsetAddress);
        if ((refillBits & 0x02) == 0)
            return;

        int nextStage = save.ReadWramByte(OracleSaveData.NextChildStageAddress);
        int requiredEssences = nextStage switch
        {
            1 or 7 => 2,
            3 or 8 => 4,
            4 or 9 => 6,
            _ => 0
        };
        int essenceCount = CountBits(save.ReadWramByte(0xc6bf));
        bool saveChanged = false;
        if (nextStage is >= 0 and <= 9 && essenceCount >= requiredEssences)
        {
            saveChanged |= save.WriteWramByte(
                OracleSaveData.ChildStageAddress, (byte)nextStage);
            int personality = nextStage switch
            {
                4 => DecideInitialPersonality(
                    save.ReadWramByte(OracleSaveData.ChildStatusAddress)),
                7 => DecideFinalPersonality(
                    save.ReadWramByte(OracleSaveData.ChildPersonalityAddress),
                    save.ReadWramByte(OracleSaveData.ChildStatusAddress)),
                _ => -1
            };
            if (personality >= 0)
            {
                saveChanged |= save.WriteWramByte(
                    OracleSaveData.ChildPersonalityAddress, (byte)personality);
            }
        }

        runtime.SetWramByte(
            OracleRuntimeState.SeedTreeRefilledBitsetAddress,
            (byte)(refillBits & ~0x02));
        if (saveChanged)
            save.CommitInventoryChange();
    }

    private static int DecideInitialPersonality(int status) => status switch
    {
        >= 0x0b => 0,
        >= 0x06 => 1,
        _ => 2
    };

    private static int DecideFinalPersonality(int initialPersonality, int status) =>
        initialPersonality switch
        {
            0 when status >= 0x1a => 2,
            0 when status >= 0x15 => 1,
            0 => 0,
            1 when status >= 0x13 => 2,
            1 when status >= 0x0f => 0,
            1 => 3,
            2 when status >= 0x0e => 1,
            2 when status >= 0x0a => 0,
            2 => 3,
            _ => 0
        };

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    private static NpcRecord ParseNpcRecord(string[] columns, int selectorColumns)
    {
        int offset = selectorColumns;
        return new NpcRecord(
            int.Parse(columns[0]),
            Convert.ToInt32(columns[1], 16),
            Convert.ToInt32(columns[2 + offset], 16),
            Convert.ToInt32(columns[3 + offset], 16),
            Convert.ToInt32(columns[4 + offset], 16),
            Convert.ToInt32(columns[5 + offset], 16),
            Convert.ToInt32(columns[6 + offset], 16),
            Convert.ToInt32(columns[7 + offset], 16),
            columns[8 + offset],
            int.Parse(columns[9 + offset]),
            int.Parse(columns[10 + offset]),
            int.Parse(columns[11 + offset]),
            columns[12 + offset] == "1",
            columns[13 + offset],
            columns[14 + offset],
            columns[15 + offset],
            columns[16 + offset],
            Encoding.UTF8.GetString(Convert.FromBase64String(columns[17 + offset])));
    }

    private static int MakeKey(int group, int room)
    {
        return (group << 8) | room;
    }

    private readonly record struct FamilyNpcRecord(
        int Stage,
        int Personality,
        NpcRecord Record);

    public readonly record struct NpcRecord(
        int Group,
        int Room,
        int Id,
        int SubId,
        int Y,
        int X,
        int Var03,
        int TextId,
        string SpriteName,
        int TileBase,
        int Palette,
        int DefaultAnimation,
        bool CanFace,
        string UpAnimation,
        string RightAnimation,
        string DownAnimation,
        string LeftAnimation,
        string Message);
}
