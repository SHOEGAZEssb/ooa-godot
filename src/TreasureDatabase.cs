using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

public sealed class TreasureDatabase
{
    public const int TreasureNone = 0x00;
    public const int TreasureBombs = 0x03;
    public const int TreasureSword = 0x05;
    public const int TreasureBoomerang = 0x06;
    public const int TreasureSwitchHook = 0x0a;
    public const int TreasureBiggoronSword = 0x0c;
    public const int TreasureShooter = 0x0f;
    public const int TreasureHarp = 0x11;
    public const int TreasureSlingshot = 0x13;
    public const int TreasureBracelet = 0x16;
    public const int TreasureFeather = 0x17;
    public const int TreasureSeedSatchel = 0x19;
    public const int TreasureRupees = 0x28;
    public const int TreasureHeartRefill = 0x29;
    public const int TreasureHeartContainer = 0x2a;

    private readonly Dictionary<string, TreasureObjectRecord> _objects = new();
    private readonly Dictionary<int, BehaviourRecord> _behaviours = new();
    private readonly Dictionary<string, List<DisplayRecord>> _displayRows = new();

    public TreasureDatabase()
    {
        LoadObjects();
        LoadBehaviours();
        LoadDisplayRows();
    }

    public TreasureObjectRecord GetObject(string name) => _objects.TryGetValue(name, out TreasureObjectRecord record)
        ? record
        : throw new KeyNotFoundException($"Treasure object {name} was not imported.");

    public BehaviourRecord GetBehaviour(int treasureId) => _behaviours.TryGetValue(treasureId, out BehaviourRecord record)
        ? record
        : throw new KeyNotFoundException($"Treasure ${treasureId:x2} has no collection behaviour.");

    public DisplayRecord GetButtonDisplay(int itemId, InventoryState inventory)
    {
        if (itemId == 0)
            return DisplayRecord.Empty;

        return itemId switch
        {
            TreasureSword => GetDisplay("treasureDisplayData_sword", Math.Max(0, inventory.SwordLevel - 1)),
            TreasureBracelet => GetDisplay("treasureDisplayData_bracelet", Math.Max(0, inventory.BraceletLevel - 1)),
            TreasureSwitchHook => GetDisplay("treasureDisplayData_switchHook", Math.Max(0, inventory.SwitchHookLevel - 1)),
            TreasureBoomerang => GetDisplay("treasureDisplayData_boomerang", Math.Max(0, inventory.BoomerangLevel - 1)),
            TreasureFeather => GetDisplay("treasureDisplayData_feather", Math.Max(0, inventory.FeatherLevel - 1)),
            TreasureSeedSatchel => GetDisplay("treasureDisplayData_satchel", inventory.SatchelSelectedSeeds),
            TreasureShooter => GetDisplay("treasureDisplayData_shooter", inventory.ShooterSelectedSeeds),
            TreasureHarp => GetDisplay("treasureDisplayData_harp", inventory.SelectedHarpSong),
            TreasureSlingshot when inventory.SlingshotLevel == 2 =>
                GetDisplay("treasureDisplayData_hyperSlingshot", inventory.SlingshotSelectedSeeds),
            TreasureSlingshot => GetDisplay("treasureDisplayData_slingshot", inventory.SlingshotSelectedSeeds),
            _ => GetDisplay("treasureDisplayData_standard", itemId)
        };
    }

    public DisplayRecord GetTreasureDisplay(int treasureId, int parameter, InventoryState inventory)
    {
        if (treasureId == 0)
            return DisplayRecord.Empty;

        int level = Math.Max(0, parameter - 1);
        return treasureId switch
        {
            TreasureSword when parameter > 0 => GetDisplay("treasureDisplayData_sword", level),
            TreasureBracelet when parameter > 0 => GetDisplay("treasureDisplayData_bracelet", level),
            TreasureSwitchHook when parameter > 0 => GetDisplay("treasureDisplayData_switchHook", level),
            TreasureBoomerang when parameter > 0 => GetDisplay("treasureDisplayData_boomerang", level),
            TreasureFeather when parameter > 0 => GetDisplay("treasureDisplayData_feather", level),
            _ => GetButtonDisplay(treasureId, inventory)
        };
    }

    private DisplayRecord GetDisplay(string table, int index)
    {
        if (!_displayRows.TryGetValue(table, out List<DisplayRecord>? records) ||
            index < 0 || index >= records.Count)
        {
            return DisplayRecord.Empty;
        }
        return records[index];
    }

    private void LoadObjects()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/metadata/treasure_objects.tsv");
        foreach (string line in DataLines(source))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 7)
                throw new InvalidOperationException($"Malformed treasure object row: {line}");

            var record = new TreasureObjectRecord(
                fields[0],
                Convert.ToInt32(fields[1], 16),
                Convert.ToInt32(fields[2], 16),
                Convert.ToInt32(fields[3], 16),
                Convert.ToInt32(fields[4], 16),
                Convert.ToInt32(fields[5], 16),
                Encoding.UTF8.GetString(Convert.FromBase64String(fields[6])));
            if (!_objects.ContainsKey(record.Name))
                _objects.Add(record.Name, record);
        }
    }

    private void LoadBehaviours()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/metadata/treasure_behaviours.tsv");
        foreach (string line in DataLines(source))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 4)
                throw new InvalidOperationException($"Malformed treasure behaviour row: {line}");

            int treasure = Convert.ToInt32(fields[0], 16);
            _behaviours.Add(treasure, new BehaviourRecord(
                treasure,
                fields[1],
                Convert.ToInt32(fields[2], 16),
                fields[3]));
        }
    }

    private void LoadDisplayRows()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/metadata/treasure_display.tsv");
        foreach (string line in DataLines(source))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 9)
                throw new InvalidOperationException($"Malformed treasure display row: {line}");

            var record = new DisplayRecord(
                Convert.ToInt32(fields[2], 16),
                Convert.ToInt32(fields[3], 16),
                Convert.ToInt32(fields[4], 16),
                Convert.ToInt32(fields[5], 16),
                Convert.ToInt32(fields[6], 16),
                Convert.ToInt32(fields[7], 16),
                Convert.ToInt32(fields[8], 16));
            if (!_displayRows.TryGetValue(fields[0], out List<DisplayRecord>? rows))
            {
                rows = new List<DisplayRecord>();
                _displayRows.Add(fields[0], rows);
            }

            int index = int.Parse(fields[1]);
            while (rows.Count < index)
                rows.Add(DisplayRecord.Empty);
            rows.Add(record);
        }
    }

    private static IEnumerable<string> DataLines(string source)
    {
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
                yield return line;
        }
    }

    public readonly record struct TreasureObjectRecord(
        string Name,
        int TreasureId,
        int SubId,
        int Parameter,
        int TextId,
        int Graphic,
        string Message);

    public readonly record struct BehaviourRecord(
        int TreasureId,
        string Variable,
        int Mode,
        string Sound);

    public readonly record struct DisplayRecord(
        int TreasureId,
        int LeftSprite,
        int LeftPalette,
        int RightSprite,
        int RightPalette,
        int ExtraMode,
        int TextLow)
    {
        public static readonly DisplayRecord Empty = new(0, 0, 0, 0, 0, 0xff, 0xff);
        public bool HasIcon => LeftSprite != 0 || RightSprite != 0;
    }
}
