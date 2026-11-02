
namespace Oracles;

public sealed class ItemDropDatabase
{
    public string AssetRoot { get; }

    public const int EnemyRecordCount = 144;
    public const int ProbabilityCount = 8;
    public const int ProbabilityBytesPerRecord = 8;
    public const int SetCount = 16;
    public const int SetSize = 32;
    public const int SelectionDataSize =
        EnemyRecordCount + ProbabilityCount * ProbabilityBytesPerRecord + SetCount * SetSize;

    public const int Fairy = 0x00;
    public const int Heart = 0x01;
    public const int OneRupee = 0x02;
    public const int FiveRupees = 0x03;
    public const int Bombs = 0x04;
    public const int EmberSeeds = 0x05;
    public const int ScentSeeds = 0x06;
    public const int PegasusSeeds = 0x07;
    public const int GaleSeeds = 0x08;
    public const int MysterySeeds = 0x09;
    public const int OneOreChunk = 0x0c;
    public const int TenOreChunks = 0x0d;
    public const int FiftyOreChunks = 0x0e;
    public const int OneHundredRupeesOrEnemy = 0x0f;

    private readonly byte[] _selectionData;
    private readonly Dictionary<int, ItemDropDatabaseVisualRecord> _visuals = new();
    private readonly ItemDropRule[] _rules = new ItemDropRule[16];
    private readonly byte[] _magnetSpeeds;
    private readonly Dictionary<(int Mode, int Tile), int> _conveyors = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ItemDropDatabase> Packs = new(StringComparer.Ordinal);
    internal static ItemDropDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot, static root => new(root));

    public ItemDropDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        _selectionData = OracleAssetCache.ReadBytes(AssetRoot + "metadata/itemDrops.bin");
        if (_selectionData.Length != SelectionDataSize)
        {
            throw new InvalidOperationException(
                $"Item-drop selection data is {_selectionData.Length} bytes; expected {SelectionDataSize}.");
        }

        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "effects/item_drops.tsv",
            new GeneratedTableSchema(
                "item-drop visuals",
                GeneratedTableKeySemantics.Unique,
                ["subid", "tile-base", "palette", "animation"],
                ["subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int subId = row.UnsignedDecimal(0);
            _visuals.Add(
                subId,
                new ItemDropDatabaseVisualRecord(
                    subId, row.UnsignedDecimal(1), row.UnsignedDecimal(2),
                    row.RequiredString(3)));
        }

        if (_visuals.Count != 16)
            throw new InvalidOperationException($"Expected 16 item-drop visual records, got {_visuals.Count}.");

        table = GeneratedTable.Load(AssetRoot + "metadata/item_drop_rules.tsv",
            new GeneratedTableSchema("item-drop rules", GeneratedTableKeySemantics.Unique,
                ["subid", "subrosia-drop", "availability-offset", "availability-mask", "treasure", "joy-ring", "parameter", "doubled-parameter"],
                ["subid"], headerRequired: true));
        if (table.Rows.Count != 16) throw new InvalidOperationException($"{AssetRoot}: expected all $10 native drop rules.");
        int next = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int id = row.HexByte(0), remap = row.HexByte(1);
            if (id != next++ || remap >= 16) throw row.Invalid(0, "ordered native drop IDs $00-$0f and valid Subrosia remaps");
            _rules[id] = new(remap,row.HexByte(2),row.HexByte(3),row.HexByte(4),row.HexByte(5),row.HexByte(6),row.HexByte(7));
        }
        _magnetSpeeds = OracleAssetCache.ReadBytes(AssetRoot + "metadata/item_drop_magnet_speeds.bin");
        if (_magnetSpeeds.Length != (OracleAssetPack.GameFor(AssetRoot) == OracleGame.Seasons ? 8 : 0))
            throw new InvalidOperationException($"{AssetRoot}: invalid native ore Magnet pull speed extent.");
        table = GeneratedTable.Load(AssetRoot + "metadata/item_drop_conveyors.tsv",
            new GeneratedTableSchema("item-drop conveyors",GeneratedTableKeySemantics.Unique,
                ["collision-set","tile","angle"],["collision-set","tile"],headerRequired:true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int angle = row.HexByte(2);
            if (angle is not (0 or 8 or 16 or 24)) throw row.Invalid(2,"native cardinal conveyor angle");
            _conveyors.Add((row.Decimal(0,0,5),row.HexByte(1)),angle);
        }

        _ = OracleObjectMovement.Shared;
    }

    public int EnemyTableRecord(int enemyId) =>
        enemyId >= 0 && enemyId < EnemyRecordCount ? _selectionData[enemyId] : 0xff;

    public ItemDropDatabaseVisualRecord GetVisual(int subId) => _visuals.TryGetValue(subId, out ItemDropDatabaseVisualRecord record)
        ? record
        : throw new KeyNotFoundException($"PART_ITEM_DROP subid ${subId:x2} has no visual record.");

    internal bool IsRuntimeSupported(int subId) => subId is >= 0 and < 16 && _rules[subId].Mask != 0;
    internal ItemDropRule Rule(int subId) => subId is >= 0 and < 16 ? _rules[subId] :
        throw new ArgumentOutOfRangeException(nameof(subId));
    internal int ConveyorAngle(int mode, byte tile) => _conveyors.GetValueOrDefault((mode,tile),-1);
    internal int MagnetPullSpeed(int index) => _magnetSpeeds[index];

    internal int? ResolveAvailableDrop(
        int subId,
        InventoryState? inventory = null,
        OracleSaveData? saveData = null)
    {
        if (subId is < 0 or >= 16) return null;
        if (inventory is not null && inventory.AssetRoot != AssetRoot ||
            saveData is not null && OracleAssetPack.ForGame(saveData.Game) != AssetRoot)
            throw new ArgumentException("Item drops must read inventory and save bytes from their selected pack.");
        if (saveData?.MinimapGroup == 1) subId = _rules[subId].SubrosiaDrop;
        ItemDropRule rule = _rules[subId];
        if (rule.Mask == 0) return null;
        // Detached inventories expose obtained flags, not a minimap location.
        int value = saveData?.ReadWramByte(0xc600 | rule.AvailabilityOffset) ??
            (rule.AvailabilityOffset == 8 ? 0xff :
                subId is >= Bombs and <= MysterySeeds && inventory?.HasTreasure(rule.Treasure) == true ? rule.Mask : 0);
        return (value & rule.Mask) != 0 ? subId : null;
    }

    internal int? DecideDrop(
        int enemyId,
        OracleRandom random,
        InventoryState? inventory = null,
        OracleSaveData? saveData = null)
    {
        int record = EnemyTableRecord(enemyId);
        if (record == 0xff)
            return null;

        byte probabilityRoll = (byte)(random.Next().Value & 0x3f);
        if (!ProbabilityAllows(record >> 5, probabilityRoll))
            return null;

        byte itemRoll = (byte)(random.Next().Value & 0x1f);
        int subId = DropSetValue(record & 0x1f, itemRoll);
        return ResolveAvailableDrop(subId, inventory, saveData);
    }

    internal int? DecideBreakableDrop(
        int dropType,
        OracleRandom random,
        InventoryState? inventory = null,
        OracleSaveData? saveData = null)
    {
        return DecideDrop(
            0x80 | (dropType & 0x0f), random, inventory, saveData);
    }

    internal int? ChooseDrop(
        int enemyId,
        byte probabilityRoll,
        byte itemRoll,
        InventoryState? inventory = null,
        OracleSaveData? saveData = null)
    {
        int record = EnemyTableRecord(enemyId);
        if (record == 0xff || !ProbabilityAllows(record >> 5, probabilityRoll & 0x3f))
            return null;

        int subId = DropSetValue(record & 0x1f, itemRoll & 0x1f);
        return ResolveAvailableDrop(subId, inventory, saveData);
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
}

public readonly record struct ItemDropDatabaseVisualRecord(int SubId, int TileBase, int Palette, string Animation);

internal readonly record struct ItemDropRule(int SubrosiaDrop, int AvailabilityOffset, int Mask,
    int Treasure, int JoyRing, int Parameter, int DoubledParameter);
