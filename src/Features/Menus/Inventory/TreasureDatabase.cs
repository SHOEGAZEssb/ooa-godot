using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class TreasureDatabase
{
    public const int TreasureNone = 0x00;
    public const int TreasureShield = 0x01;
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
    public const int TreasureHeartPiece = 0x2b;
    public const int TreasureRingBox = 0x2c;
    public const int TreasureRing = 0x2d;
    public const int TreasurePotion = 0x2f;
    public const int TreasureGashaSeed = 0x34;
    public const int TreasureMakuSeed = 0x36;
    public const int TreasureEssence = 0x40;
    public const int TreasureTradeItem = 0x41;
    public const int TreasureGraveyardKey = 0x42;
    public const int TreasureTuniNut = 0x4c;

    private readonly Dictionary<string, TreasureObjectRecord> _objects = new();
    private readonly List<TreasureObjectRecord> _objectRows = new();
    private readonly Dictionary<int, TreasureObjectVisualRecord> _objectVisuals = new();
    private readonly Dictionary<int, BehaviourRecord> _behaviours = new();
    private readonly Dictionary<int, GashaMaturityRecord> _gashaMaturity = new();
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
        LoadGashaMaturity();
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

    internal int GetGashaMaturityGain(int treasureId, int parameter)
    {
        if (!_gashaMaturity.TryGetValue(treasureId, out GashaMaturityRecord record))
            return 0;
        return record.ParameterAmount ? parameter : record.Amount;
    }

    public DisplayRecord GetButtonDisplay(int itemId, InventoryState inventory)
    {
        if (itemId == 0)
            return DisplayRecord.Empty;

        return itemId switch
        {
            TreasureShield => GetDisplay(
                "treasureDisplayData_shield", Math.Max(0, inventory.ShieldLevel - 1)),
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
            TreasureShield when parameter > 0 =>
                GetDisplay("treasureDisplayData_shield", level),
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
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/treasure_objects.tsv",
            new GeneratedTableSchema(
                "treasure objects",
                GeneratedTableKeySemantics.Aliased,
                [
                    "treasure-object", "treasure-id", "subid", "parameter", "text-id",
                    "graphic", "message-base64"
                ],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            TreasureObjectRecord record = new TreasureObjectRecord(
                row.RequiredString(0),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByteOrSentinel(4, "ffffffff", -1),
                row.HexByte(5),
                row.Base64Utf8(6));
            if (!_objects.ContainsKey(record.Name))
            {
                _objects.Add(record.Name, record);
                _objectRows.Add(record);
            }
        }
    }

    private void LoadObjectVisuals()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/treasure_object_visuals.tsv",
            new GeneratedTableSchema(
                "treasure object visuals",
                GeneratedTableKeySemantics.Unique,
                ["graphic", "sprite", "tile-base", "palette", "default-animation", "animation"],
                ["graphic"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int graphic = row.HexByte(0);
            TreasureObjectVisualRecord record = new TreasureObjectVisualRecord(
                graphic,
                row.RequiredString(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.RequiredString(5));
            _objectVisuals.Add(graphic, record);
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
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/treasure_behaviours.tsv",
            new GeneratedTableSchema(
                "treasure collection behaviours",
                GeneratedTableKeySemantics.Unique,
                ["treasure-id", "variable", "mode", "sound"],
                ["treasure-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int treasure = row.HexByte(0);
            string sourceVariable = row.RequiredString(1);
            int rawMode = row.HexByte(2);
            CollectionMode mode = ParseCollectionMode(treasure, sourceVariable, rawMode);
            TreasureVariable variable = ParseTreasureVariable(treasure, sourceVariable, rawMode);
            ValidateBehaviourBinding(treasure, sourceVariable, rawMode, variable, mode);
            _behaviours.Add(treasure, new BehaviourRecord(
                treasure,
                variable,
                mode,
                rawMode,
                ParseSound(row.RequiredString(3))));
        }
    }

    private void LoadGashaMaturity()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/treasure_gasha_maturity.tsv",
            new GeneratedTableSchema(
                "treasure Gasha maturity",
                GeneratedTableKeySemantics.Unique,
                ["treasure-id", "mode", "amount"],
                ["treasure-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            string mode = row.RequiredString(1);
            bool parameterAmount = mode switch
            {
                "fixed" => false,
                "parameter" => true,
                _ => throw row.Invalid(1, "one of fixed, parameter")
            };
            int treasure = row.HexByte(0);
            _gashaMaturity.Add(
                treasure,
                new GashaMaturityRecord(
                    treasure, parameterAmount, row.UnsignedDecimal(2)));
        }
        if (_gashaMaturity.Count != 4 ||
            GetGashaMaturityGain(TreasureEssence, 0) != 150 ||
            GetGashaMaturityGain(0x2b, 0) != 36 ||
            GetGashaMaturityGain(TreasureTradeItem, 0) != 100 ||
            GetGashaMaturityGain(TreasureHeartRefill, 0x18) != 0x18)
        {
            throw new InvalidOperationException(
                "Treasure Gasha maturity table is incomplete.");
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
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/treasure_display.tsv",
            new GeneratedTableSchema(
                "treasure display rows",
                GeneratedTableKeySemantics.Grouped,
                [
                    "table", "index", "treasure-id", "left-sprite", "left-palette",
                    "right-sprite", "right-palette", "extra-mode", "text-low"
                ],
                ["table"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            DisplayRecord record = new DisplayRecord(
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.HexByte(7),
                row.HexByte(8));
            string tableName = row.RequiredString(0);
            if (!_displayRows.TryGetValue(tableName, out List<DisplayRecord>? rows))
            {
                rows = new List<DisplayRecord>();
                _displayRows.Add(tableName, rows);
            }

            int index = row.UnsignedDecimal(1);
            while (rows.Count < index)
                rows.Add(DisplayRecord.Empty);
            rows.Add(record);
        }
    }

    private void LoadInventoryTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/inventory_text.tsv",
            new GeneratedTableSchema(
                "inventory text",
                GeneratedTableKeySemantics.Unique,
                ["kind", "index", "name-text-id", "description-text-id", "message-base64"],
                ["kind", "index"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            string kind = row.RequiredString(0);
            int index = row.HexByte(1);
            InventoryTextRecord record = new InventoryTextRecord(
                kind,
                index,
                row.HexWord(2),
                row.HexWord(3),
                row.Base64Utf8(4));
            Dictionary<int, InventoryTextRecord> destination = kind switch
            {
                "item" => _inventoryTexts,
                "ring" => _ringTexts,
                _ => throw row.Invalid(0, "one of item, ring")
            };
            if (!destination.TryAdd(index, record))
                throw new InvalidOperationException(
                    $"Duplicate {kind} inventory text index ${index:x2}.");
        }

        if (!_inventoryTexts.ContainsKey(0x00) || !_inventoryTexts.ContainsKey(0x65) ||
            _ringTexts.Count != 0x40)
        {
            throw new InvalidOperationException(
                "Inventory text data must include TX_0900, TX_0965, and all 64 rings.");
        }
    }
}
