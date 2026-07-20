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
    public const int TreasureShovel = 0x15;
    public const int TreasureBracelet = 0x16;
    public const int TreasureFeather = 0x17;
    public const int TreasureSeedSatchel = 0x19;
    public const int TreasureEmberSeeds = 0x20;
    public const int TreasureRupees = 0x28;
    public const int TreasureHeartRefill = 0x29;
    public const int TreasureHeartContainer = 0x2a;
    public const int TreasureRingBox = 0x2c;
    public const int TreasureRing = 0x2d;
    public const int TreasureMakuSeed = 0x36;
    public const int TreasureEssence = 0x40;
    public const int TreasureTradeItem = 0x41;
    public const int TreasureTuniNut = 0x4c;

    private readonly Dictionary<string, TreasureObjectRecord> _objects = new();
    private readonly List<TreasureObjectRecord> _objectRows = new();
    private readonly Dictionary<int, TreasureObjectVisualRecord> _objectVisuals = new();
    private readonly Dictionary<int, BehaviourRecord> _behaviours = new();
    private readonly Dictionary<string, List<DisplayRecord>> _displayRows = new();
    private readonly Dictionary<int, InventoryTextRecord> _inventoryTexts = new();
    private readonly Dictionary<int, InventoryTextRecord> _ringTexts = new();

    public int BehaviourCount => _behaviours.Count;
    public IReadOnlyList<TreasureObjectRecord> Objects => _objectRows;

    public TreasureDatabase()
    {
        LoadObjects();
        LoadObjectVisuals();
        LoadBehaviours();
        LoadDisplayRows();
        LoadInventoryTexts();
    }

    public TreasureObjectRecord GetObject(string name) => _objects.TryGetValue(name, out TreasureObjectRecord record)
        ? record
        : throw new KeyNotFoundException($"Treasure object {name} was not imported.");

    public TreasureObjectVisualRecord GetObjectVisual(int graphic) =>
        _objectVisuals.TryGetValue(graphic, out TreasureObjectVisualRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"INTERAC_TREASURE $60 graphic ${graphic:x2} was not imported.");

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
            TreasureTradeItem => GetDisplay("treasureDisplayData_trade", inventory.TradeItem),
            TreasureTuniNut => GetDisplay("treasureDisplayData_tuniNut", inventory.TuniNutState),
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

    public InventoryTextRecord GetInventoryText(int textLow) =>
        _inventoryTexts.TryGetValue(textLow & 0xff, out InventoryTextRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Inventory text TX_09{textLow & 0xff:x2} was not imported.");

    public InventoryTextRecord GetRingText(int ring) =>
        _ringTexts.TryGetValue(ring & 0x3f, out InventoryTextRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Inventory ring text ${ring & 0x3f:x2} was not imported.");

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
            {
                _objects.Add(record.Name, record);
                _objectRows.Add(record);
            }
        }
    }

    private void LoadObjectVisuals()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/metadata/treasure_object_visuals.tsv");
        foreach (string line in DataLines(source))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 6)
                throw new InvalidOperationException(
                    $"Malformed treasure object visual row: {line}");

            int graphic = Convert.ToInt32(fields[0], 16);
            var record = new TreasureObjectVisualRecord(
                graphic,
                fields[1],
                Convert.ToInt32(fields[2], 16),
                Convert.ToInt32(fields[3], 16),
                Convert.ToInt32(fields[4], 16),
                fields[5]);
            if (!_objectVisuals.TryAdd(graphic, record))
            {
                throw new InvalidOperationException(
                    $"Duplicate treasure object visual graphic ${graphic:x2}.");
            }
        }

        if (_objectVisuals.Count != 91 ||
            !_objectVisuals.TryGetValue(0x42, out TreasureObjectVisualRecord smallKey) ||
            smallKey.Sprite != "spr_map_compass_keys_bookofseals" ||
            smallKey.TileBase != 0x0c || smallKey.Palette != 5 ||
            smallKey.DefaultAnimation != 0 || string.IsNullOrEmpty(smallKey.Animation))
        {
            throw new InvalidOperationException(
                "Treasure visuals must include all 91 source graphics and exact small-key graphic $42 data.");
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
            int rawMode = Convert.ToInt32(fields[2], 16);
            CollectionMode mode = ParseCollectionMode(treasure, fields[1], rawMode);
            TreasureVariable variable = ParseTreasureVariable(treasure, fields[1], rawMode);
            ValidateBehaviourBinding(treasure, fields[1], rawMode, variable, mode);
            _behaviours.Add(treasure, new BehaviourRecord(
                treasure,
                variable,
                mode,
                rawMode,
                ParseSound(fields[3])));
        }
    }

    private static int ParseSound(string sound) => sound switch
    {
        "SND_NONE" => 0,
        "SND_GETITEM" => OracleSoundEngine.SndGetItem,
        "SND_GETSEED" => OracleSoundEngine.SndGetSeed,
        "MUS_GET_ESSENCE" => OracleSoundEngine.MusGetEssence,
        _ => throw new InvalidOperationException(
            $"Unknown treasure collection sound '{sound}'.")
    };

    private static CollectionMode ParseCollectionMode(int treasure, string variable, int rawMode)
    {
        int mode = rawMode & 0x0f;
        if ((rawMode & ~0x8f) != 0 || !Enum.IsDefined(typeof(CollectionMode), mode))
            throw UnsupportedBehaviour(treasure, variable, rawMode, "unknown collection mode");
        return (CollectionMode)mode;
    }

    private static TreasureVariable ParseTreasureVariable(int treasure, string variable, int rawMode) =>
        variable switch
        {
            // The original giveTreasure redirects a zero variable byte to wShortSecretIndex.
            "$00" => TreasureVariable.ShortSecretIndex,
            "wc608" => TreasureVariable.DummyC608,
            "wAnimalCompanion" => TreasureVariable.AnimalCompanion,
            "wDeathRespawnBuffer.rememberedCompanionId" => TreasureVariable.RememberedCompanionId,
            "wNumBombchus" => TreasureVariable.Bombchus,
            "wLinkHealth" => TreasureVariable.LinkHealth,
            "wLinkMaxHealth" => TreasureVariable.LinkMaxHealth,
            "wNumHeartPieces" => TreasureVariable.HeartPieces,
            "wNumRupees" => TreasureVariable.Rupees,
            "wShieldLevel" => TreasureVariable.ShieldLevel,
            "wNumBombs" => TreasureVariable.Bombs,
            "wSwordLevel" => TreasureVariable.SwordLevel,
            "wSeedSatchelLevel" => TreasureVariable.SeedSatchelLevel,
            "wSwitchHookLevel" => TreasureVariable.SwitchHookLevel,
            "wSelectedHarpSong" => TreasureVariable.SelectedHarpSong,
            "wBraceletLevel" => TreasureVariable.BraceletLevel,
            "wNumEmberSeeds" => TreasureVariable.EmberSeeds,
            "wNumScentSeeds" => TreasureVariable.ScentSeeds,
            "wNumPegasusSeeds" => TreasureVariable.PegasusSeeds,
            "wNumGaleSeeds" => TreasureVariable.GaleSeeds,
            "wNumMysterySeeds" => TreasureVariable.MysterySeeds,
            "wNumGashaSeeds" => TreasureVariable.GashaSeeds,
            "wEssencesObtained" => TreasureVariable.EssencesObtained,
            "wTradeItem" => TreasureVariable.TradeItem,
            "wTuniNutState" => TreasureVariable.TuniNutState,
            "wNumSlates" => TreasureVariable.Slates,
            "wRingBoxLevel" => TreasureVariable.RingBoxLevel,
            "wNumUnappraisedRingsBcd" => TreasureVariable.UnappraisedRings,
            "wDungeonSmallKeys" => TreasureVariable.DungeonSmallKeys,
            "wDungeonBossKeys" => TreasureVariable.DungeonBossKeys,
            "wDungeonCompasses" => TreasureVariable.DungeonCompasses,
            "wDungeonMaps" => TreasureVariable.DungeonMaps,
            "wObtainedSeasons" => TreasureVariable.ObtainedSeasons,
            "wBoomerangLevel" => TreasureVariable.BoomerangLevel,
            "wMagnetGlovePolarity" => TreasureVariable.MagnetGlovePolarity,
            "wSlingshotLevel" => TreasureVariable.SlingshotLevel,
            "wFeatherLevel" => TreasureVariable.FeatherLevel,
            _ => throw UnsupportedBehaviour(treasure, variable, rawMode, "unknown WRAM variable")
        };

    private static void ValidateBehaviourBinding(
        int treasure,
        string sourceVariable,
        int rawMode,
        TreasureVariable variable,
        CollectionMode mode)
    {
        bool valid = mode switch
        {
            CollectionMode.None => true,
            CollectionMode.SetBit => variable is TreasureVariable.EssencesObtained or
                TreasureVariable.ObtainedSeasons,
            CollectionMode.SetDungeonBit => variable is TreasureVariable.DungeonBossKeys or
                TreasureVariable.DungeonCompasses or TreasureVariable.DungeonMaps,
            CollectionMode.IncrementDungeonKey => variable == TreasureVariable.DungeonSmallKeys,
            CollectionMode.AddUnappraisedRing => variable == TreasureVariable.UnappraisedRings,
            CollectionMode.SetUpgradeBit => variable == TreasureVariable.ShortSecretIndex,
            CollectionMode.AddRupees => variable == TreasureVariable.Rupees,
            _ => IsScalarVariable(variable)
        };
        if (!valid)
            throw UnsupportedBehaviour(treasure, sourceVariable, rawMode,
                $"{mode} cannot target {variable}");
    }

    private static bool IsScalarVariable(TreasureVariable variable) => variable is not
        (TreasureVariable.DungeonSmallKeys or TreasureVariable.DungeonBossKeys or
        TreasureVariable.DungeonCompasses or TreasureVariable.DungeonMaps or
        TreasureVariable.UnappraisedRings);

    private static InvalidOperationException UnsupportedBehaviour(
        int treasure,
        string variable,
        int mode,
        string reason) => new(
            $"Treasure ${treasure:x2} behaviour variable '{variable}' mode ${mode:x2} is unsupported: {reason}.");

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

    private void LoadInventoryTexts()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/metadata/inventory_text.tsv");
        foreach (string line in DataLines(source))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 5)
                throw new InvalidOperationException($"Malformed inventory text row: {line}");

            int index = Convert.ToInt32(fields[1], 16);
            var record = new InventoryTextRecord(
                fields[0],
                index,
                Convert.ToInt32(fields[2], 16),
                Convert.ToInt32(fields[3], 16),
                Encoding.UTF8.GetString(Convert.FromBase64String(fields[4])));
            Dictionary<int, InventoryTextRecord> destination = fields[0] switch
            {
                "item" => _inventoryTexts,
                "ring" => _ringTexts,
                _ => throw new InvalidOperationException(
                    $"Unknown inventory text kind '{fields[0]}' in row: {line}")
            };
            if (!destination.TryAdd(index, record))
                throw new InvalidOperationException(
                    $"Duplicate {fields[0]} inventory text index ${index:x2}.");
        }

        if (!_inventoryTexts.ContainsKey(0x00) || !_inventoryTexts.ContainsKey(0x65) ||
            _ringTexts.Count != 0x40)
        {
            throw new InvalidOperationException(
                "Inventory text data must include TX_0900, TX_0965, and all 64 rings.");
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

    public readonly record struct TreasureObjectVisualRecord(
        int Graphic,
        string Sprite,
        int TileBase,
        int Palette,
        int DefaultAnimation,
        string Animation);

    public enum CollectionMode
    {
        None = 0x0,
        SetBit = 0x1,
        Increment = 0x2,
        IncrementBcd = 0x3,
        AddBcd = 0x4,
        Set = 0x5,
        SetDungeonBit = 0x6,
        IncrementDungeonKey = 0x7,
        SetMinimum = 0x8,
        AddUnappraisedRing = 0x9,
        Add = 0xa,
        SetUpgradeBit = 0xb,
        AddCapped = 0xc,
        AddBcdCapped = 0xd,
        AddRupees = 0xe,
        AddSeeds = 0xf
    }

    public enum TreasureVariable
    {
        ShortSecretIndex,
        DummyC608,
        AnimalCompanion,
        RememberedCompanionId,
        Bombchus,
        LinkHealth,
        LinkMaxHealth,
        HeartPieces,
        Rupees,
        ShieldLevel,
        Bombs,
        SwordLevel,
        SeedSatchelLevel,
        SwitchHookLevel,
        SelectedHarpSong,
        BraceletLevel,
        EmberSeeds,
        ScentSeeds,
        PegasusSeeds,
        GaleSeeds,
        MysterySeeds,
        GashaSeeds,
        EssencesObtained,
        TradeItem,
        TuniNutState,
        Slates,
        RingBoxLevel,
        UnappraisedRings,
        DungeonSmallKeys,
        DungeonBossKeys,
        DungeonCompasses,
        DungeonMaps,
        ObtainedSeasons,
        BoomerangLevel,
        MagnetGlovePolarity,
        SlingshotLevel,
        FeatherLevel,
        SatchelSelectedSeeds,
        ShooterSelectedSeeds,
        SlingshotSelectedSeeds
    }

    public readonly record struct BehaviourRecord(
        int TreasureId,
        TreasureVariable Variable,
        CollectionMode Mode,
        int RawMode,
        int Sound);

    public readonly record struct DisplayRecord(
        int TreasureId,
        int LeftSprite,
        int LeftPalette,
        int RightSprite,
        int RightPalette,
        int ExtraMode,
        int TextLow)
    {
        public static readonly DisplayRecord Empty = new(0, 0, 0, 0, 0, 0xff, 0x00);
        public bool HasIcon => LeftSprite != 0 || RightSprite != 0;
    }

    public readonly record struct InventoryTextRecord(
        string Kind,
        int Index,
        int NameTextId,
        int DescriptionTextId,
        string Message);
}
