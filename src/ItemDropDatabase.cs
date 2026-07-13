using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class ItemDropDatabase
{
    public const int EnemyRecordCount = 144;
    public const int ProbabilityCount = 8;
    public const int ProbabilityBytesPerRecord = 8;
    public const int SetCount = 16;
    public const int SetSize = 32;
    public const int SelectionDataSize =
        EnemyRecordCount + ProbabilityCount * ProbabilityBytesPerRecord + SetCount * SetSize;

    public const int Heart = 0x01;
    public const int OneRupee = 0x02;
    public const int FiveRupees = 0x03;

    private readonly byte[] _selectionData;
    private readonly Dictionary<int, VisualRecord> _visuals = new();

    public ItemDropDatabase()
    {
        _selectionData = FileAccess.GetFileAsBytes("res://assets/oracle/metadata/itemDrops.bin");
        if (_selectionData.Length != SelectionDataSize)
        {
            throw new InvalidOperationException(
                $"Item-drop selection data is {_selectionData.Length} bytes; expected {SelectionDataSize}.");
        }

        string source = FileAccess.GetFileAsString("res://assets/oracle/effects/item_drops.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] fields = line.Split('\t');
            if (fields.Length != 4 ||
                !int.TryParse(fields[0], out int subId) ||
                !int.TryParse(fields[1], out int tileBase) ||
                !int.TryParse(fields[2], out int palette))
            {
                throw new InvalidOperationException($"Malformed PART_ITEM_DROP visual row: {line}");
            }
            _visuals.Add(subId, new VisualRecord(subId, tileBase, palette, fields[3]));
        }

        if (_visuals.Count != 16)
            throw new InvalidOperationException($"Expected 16 item-drop visual records, got {_visuals.Count}.");
    }

    public int EnemyTableRecord(int enemyId) =>
        enemyId >= 0 && enemyId < EnemyRecordCount ? _selectionData[enemyId] : 0xff;

    public VisualRecord GetVisual(int subId) => _visuals.TryGetValue(subId, out VisualRecord record)
        ? record
        : throw new KeyNotFoundException($"PART_ITEM_DROP subid ${subId:x2} has no visual record.");

    internal int? DecideDrop(int enemyId, OracleRandom random)
    {
        int record = EnemyTableRecord(enemyId);
        if (record == 0xff)
            return null;

        byte probabilityRoll = (byte)(random.Next().Value & 0x3f);
        if (!ProbabilityAllows(record >> 5, probabilityRoll))
            return null;

        byte itemRoll = (byte)(random.Next().Value & 0x1f);
        int subId = DropSetValue(record & 0x1f, itemRoll);
        return IsCurrentlyAvailable(subId) ? subId : null;
    }

    internal int? ChooseDrop(int enemyId, byte probabilityRoll, byte itemRoll)
    {
        int record = EnemyTableRecord(enemyId);
        if (record == 0xff || !ProbabilityAllows(record >> 5, probabilityRoll & 0x3f))
            return null;

        int subId = DropSetValue(record & 0x1f, itemRoll & 0x1f);
        return IsCurrentlyAvailable(subId) ? subId : null;
    }

    internal bool ProbabilityAllows(int probability, int roll)
    {
        if (probability < 0 || probability >= ProbabilityCount || roll < 0 || roll >= 64)
            return false;
        int offset = EnemyRecordCount + probability * ProbabilityBytesPerRecord + roll / 8;
        return (_selectionData[offset] & (1 << (roll & 7))) != 0;
    }

    internal int DropSetValue(int set, int roll)
    {
        if (set < 0 || set >= SetCount || roll < 0 || roll >= SetSize)
            return 0xff;
        int offset = EnemyRecordCount + ProbabilityCount * ProbabilityBytesPerRecord +
            set * SetSize + roll;
        return _selectionData[offset];
    }

    private static bool IsCurrentlyAvailable(int subId) =>
        subId is Heart or OneRupee or FiveRupees;

    public readonly record struct VisualRecord(
        int SubId,
        int TileBase,
        int Palette,
        string Animation);
}
