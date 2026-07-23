using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class NpcDatabase
{
    private readonly Dictionary<int, List<NpcRecord>> _byRoom = new();
    private readonly Dictionary<int, List<FamilyNpcRecord>> _familyByRoom = new();
    private readonly BipinBlossomFamilyInteractionDatabase _familyInteractions = new();

    public NpcDatabase()
    {
        GeneratedTable npcs = GeneratedTable.Load(
            "res://assets/oracle/objects/npcs.tsv",
            new GeneratedTableSchema(
                "room NPCs",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "id", "subid", "y", "x", "var03", "text-id",
                    "sprite", "tile-base", "palette", "default-animation", "can-face",
                    "up-animation", "right-animation", "down-animation", "left-animation",
                    "utf8-base64"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in npcs.Rows)
        {
            NpcRecord record = ParseNpcRecord(row, selectorColumns: 0);

            int key = MakeKey(record.Group, record.Room);
            if (!_byRoom.TryGetValue(key, out List<NpcRecord>? records))
            {
                records = new List<NpcRecord>();
                _byRoom.Add(key, records);
            }
            records.Add(record);
        }

        GeneratedTable family = GeneratedTable.Load(
            "res://assets/oracle/objects/bipin_blossom_family.tsv",
            new GeneratedTableSchema(
                "Bipin and Blossom family NPCs",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "stage", "personality", "id", "subid", "y", "x",
                    "var03", "text-id", "sprite", "tile-base", "palette",
                    "default-animation", "can-face", "up-animation", "right-animation",
                    "down-animation", "left-animation", "utf8-base64"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in family.Rows)
        {
            NpcRecord record = ParseNpcRecord(row, selectorColumns: 2);
            FamilyNpcRecord familyRecord = new FamilyNpcRecord(
                row.UnsignedDecimal(2),
                row.Decimal(3),
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
                        Dialogue dialogue = _familyInteractions.Text(0x4301, save);
                        record = record with
                        {
                            TextId = dialogue.TextId,
                            Message = dialogue.Message
                        };
                    }
                    else if (stage == 0 && save.ChildNamed &&
                        record.Id == 0x2b && record.SubId == 0x00)
                    {
                        Dialogue dialogue = _familyInteractions.Text(0x4409, save);
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

    private static NpcRecord ParseNpcRecord(
        GeneratedTableRow row,
        int selectorColumns)
    {
        int offset = selectorColumns;
        return new NpcRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2 + offset),
            row.HexByte(3 + offset),
            row.HexByte(4 + offset),
            row.HexByte(5 + offset),
            row.HexByte(6 + offset),
            row.HexWord(7 + offset),
            row.RequiredString(8 + offset),
            row.UnsignedDecimal(9 + offset),
            row.UnsignedDecimal(10 + offset),
            row.UnsignedDecimal(11 + offset),
            row.Boolean01(12 + offset),
            row.RequiredString(13 + offset),
            row.RequiredString(14 + offset),
            row.RequiredString(15 + offset),
            row.RequiredString(16 + offset),
            row.Base64Utf8(17 + offset));
    }

    private static int MakeKey(int group, int room)
    {
        return (group << 8) | room;
    }
}
