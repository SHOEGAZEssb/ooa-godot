using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class NpcDatabase
{
    private readonly Lookup<int, NpcRecord> _byRoom = new();
    private readonly Lookup<int, FamilyNpcRecord> _familyByRoom = new();
    private readonly List<NpcRecord> _allRecords = new();
    internal IReadOnlyList<NpcRecord> AllRecords => _allRecords;

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
                    "utf8-base64", "implementation"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in npcs.Rows)
        {
            NpcRecord record = ParseNpcRecord(row, selectorColumns: 0);
            _allRecords.Add(record);

            _byRoom.Add(MakeKey(record.Group, record.Room), record);
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
                    "down-animation", "left-animation", "utf8-base64", "implementation"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in family.Rows)
        {
            NpcRecord record = ParseNpcRecord(row, selectorColumns: 2);
            _allRecords.Add(record);
            FamilyNpcRecord familyRecord = new FamilyNpcRecord(
                row.UnsignedDecimal(2),
                row.Decimal(3),
                record);
            _familyByRoom.Add(
                MakeKey(record.Group, record.Room), familyRecord);
        }
    }

    public IReadOnlyList<NpcRecord> GetRoomNpcs(int group, int room)
    {
        return _byRoom.ValuesOrEmpty(MakeKey(group, room));
    }

    internal bool TryGetFamilyRoomNpcs(
        int group,
        int room,
        out IReadOnlyList<FamilyNpcRecord> records) =>
        _familyByRoom.TryGetValues(MakeKey(group, room), out records);

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
            row.Base64Utf8(17 + offset),
            ParseImplementation(row, 18 + offset));
    }

    private static NpcImplementationClassification ParseImplementation(
        GeneratedTableRow row,
        int column) =>
        row.RequiredString(column) switch
        {
            "ordinary-generic" =>
                NpcImplementationClassification.OrdinaryGeneric,
            "specialized-native" =>
                NpcImplementationClassification.SpecializedNative,
            "event-owned" =>
                NpcImplementationClassification.EventOwned,
            "deliberately-unsupported" =>
                NpcImplementationClassification.DeliberatelyUnsupported,
            _ => throw row.Invalid(
                column,
                "ordinary-generic, specialized-native, event-owned, or " +
                "deliberately-unsupported")
        };

    private static int MakeKey(int group, int room)
    {
        return (group << 8) | room;
    }
}

internal readonly record struct FamilyNpcRecord(int Stage, int Personality, NpcRecord Record);

public enum NpcImplementationClassification
{
    Unknown = 0,
    OrdinaryGeneric,
    SpecializedNative,
    EventOwned,
    DeliberatelyUnsupported
}

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
    string Message,
    NpcImplementationClassification Implementation);
