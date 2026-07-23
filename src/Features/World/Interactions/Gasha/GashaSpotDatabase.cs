using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed runtime view of INTERAC_GASHA_SPOT, its 16 Ages placements, reward
/// distributions, ring tiers, and dynamically loaded tree graphics.
/// </summary>
internal sealed class GashaSpotDatabase
{
    private readonly Dictionary<int, SpotRecord> _spotsByRoom = new();
    private readonly SpotRecord[] _spots = new SpotRecord[16];
    private readonly int[,,] _probabilities = new int[5, 5, 10];
    private readonly List<int>[] _ringTiers =
        [new(), new(), new(), new(), new()];
    private readonly RewardRecord[] _rewards = new RewardRecord[10];
    private readonly Dictionary<int, string> _texts = new();
    private readonly DisappearanceRecord[] _disappearance = new DisappearanceRecord[9];
    private readonly Dictionary<string, int> _constants = new();
    private Image? _treeGraphics;
    private readonly Dictionary<string, Image> _groundGraphics = new();

    internal int SoftSoilTile => Constant("soft-soil");
    internal int PlantedSoilTile => Constant("planted-soil");
    internal int TreeTopLeftTile => Constant("tree-top-left");
    internal int SproutKills => Constant("sprout-kills");
    internal int NutKills => Constant("nut-kills");
    internal int HarvestMaturityCost => Constant("harvest-maturity-cost");
    internal int RoomLoadMaturity => Constant("room-load-maturity");
    internal int NutSpeedRaw => Constant("nut-speed-raw");
    internal int NutSpeedZ => Constant("nut-speed-z");
    internal int NutGravity => Constant("nut-gravity");
    internal int DisappearancePeriod => Constant("disappearance-period");
    internal int DisappearancePhases => Constant("disappearance-phases");
    internal int Count => _spotsByRoom.Count;
    internal GashaSpotDatabaseVisualRecord NutVisual { get; }

    internal GashaSpotDatabase()
    {
        LoadConstants();
        LoadSpots();
        LoadProbabilities();
        LoadRingTiers();
        LoadRewards();
        NutVisual = LoadNutVisual();
        LoadTexts();
        LoadDisappearance();
        Validate();
    }

    internal bool TryGetSpot(int group, int room, out SpotRecord spot) =>
        _spotsByRoom.TryGetValue((group << 8) | room, out spot);

    internal SpotRecord GetSpot(int subId)
    {
        if (subId is < 0 or >= 16)
            throw new ArgumentOutOfRangeException(nameof(subId));
        return _spots[subId];
    }

    internal RewardRecord GetReward(int type)
    {
        if (type is < 0 or >= 10)
            throw new ArgumentOutOfRangeException(nameof(type));
        return _rewards[type];
    }

    internal string Text(int textId) => _texts.TryGetValue(textId, out string? text)
        ? text
        : throw new KeyNotFoundException($"Gasha text TX_{textId:x4} was not imported.");

    internal int MaturityClass(int maturity)
    {
        int half = maturity >> 1;
        if ((maturity >> 9) != 0)
            return 0;
        int[] thresholds = [150, 100, 60, 20, 0];
        for (int index = 0; index < thresholds.Length; index++)
        {
            if (half >= thresholds[index])
                return index;
        }
        throw new InvalidOperationException("Gasha maturity table has no terminal range.");
    }

    internal int SelectRewardType(int rank, int maturityClass, byte random)
    {
        if (rank is < 0 or >= 5 || maturityClass is < 0 or >= 5)
            throw new ArgumentOutOfRangeException(nameof(rank));
        int remaining = random;
        for (int reward = 0; reward < 10; reward++)
        {
            remaining -= _probabilities[rank, maturityClass, reward];
            if (remaining < 0)
                return reward;
        }
        throw new InvalidOperationException("Gasha probability row did not total 256.");
    }

    internal int SelectRing(int tier, byte random)
    {
        if (tier is < 0 or >= 5)
            throw new ArgumentOutOfRangeException(nameof(tier));
        IReadOnlyList<int> rings = _ringTiers[tier];
        int index = random & (tier == 4 ? 1 : 7);
        return rings[index];
    }

    internal void ApplyRoomState(
        int group,
        OracleRoomData room,
        OracleSaveData save,
        long animationTick)
    {
        if (!TryGetSpot(group, room.Id, out SpotRecord spot) ||
            !save.IsGashaSpotPlanted(spot.SubId))
        {
            return;
        }

        if (room.GetMetatile(spot.Position) != SoftSoilTile)
            return;

        if (save.GetGashaSpotKillCounter(spot.SubId) < SproutKills)
        {
            room.SetPositionTileAndCollision(
                spot.Position, (byte)PlantedSoilTile, 0, animationTick);
            return;
        }

        var dynamicTiles = new Dictionary<int, DynamicBackgroundTile>();
        Image tree = TreeGraphics;
        for (int tile = 0; tile < 16; tile++)
            dynamicTiles.Add(0xa0 + tile, new(tree, tile));
        room.SetDynamicBackgroundTiles(dynamicTiles, animationTick);
        room.SetMetatileRectangle(
            spot.TreeTopLeft,
            2,
            [(byte)TreeTopLeftTile, (byte)(TreeTopLeftTile + 1),
             (byte)(TreeTopLeftTile + 0x10), (byte)(TreeTopLeftTile + 0x11)],
            [0x0f, 0x0f, 0x0f, 0x0f],
            animationTick);
    }

    internal void BeginDisappearance(
        OracleRoomData room,
        SpotRecord spot,
        long animationTick)
    {
        byte[] initial =
        {
            0x20,0x21,0x22,0x23, 0x24,0x25,0x26,0x27,
            0x28,0x29,0x2a,0x2b, 0x2c,0x2d,0x2e,0x2f
        };
        room.SetSubtileRectangle(spot.TreeTopLeft, initial, 4, 0, animationTick);
    }

    internal void ApplyDisappearanceFrame(
        OracleRoomData room,
        SpotRecord spot,
        int phase,
        long animationTick)
    {
        if (phase is < 1 or > 9)
            throw new ArgumentOutOfRangeException(nameof(phase));
        DisappearanceRecord record = _disappearance[phase - 1];
        var tiles = new Dictionary<int, DynamicBackgroundTile>();
        Image ground = GroundGraphics(spot.Ground);
        for (int index = 0; index < 4; index++)
            tiles.Add(0xa0 + index, new(ground, index));
        for (int index = 0; index < record.SourceCount; index++)
        {
            tiles[0xa0 + record.DestinationStart + index] =
                new DynamicBackgroundTile(
                    TreeGraphics, record.SourceStart + index);
        }
        room.SetDynamicBackgroundTiles(tiles, animationTick);
        room.SetSubtileRectangle(
            spot.TreeTopLeft, record.TileMap, 4, 0, animationTick);
    }

    internal void CompleteHarvest(
        OracleRoomData room,
        SpotRecord spot,
        long animationTick) =>
        room.CompleteGashaHarvest(
            spot.TreeTopLeft, (byte)spot.ReplacementTile, animationTick);

    private Image TreeGraphics => _treeGraphics ??=
        OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/gfx_gasha_tree.png");

    private Image GroundGraphics(string ground)
    {
        if (_groundGraphics.TryGetValue(ground, out Image? image))
            return image;
        string sprite = ground switch
        {
            "grass" => "spr_grass_tuft",
            "dirt" => "gfx_dirt",
            "sand" => "gfx_sand",
            _ => throw new InvalidOperationException($"Unknown Gasha ground graphics '{ground}'.")
        };
        image = OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{sprite}.png");
        _groundGraphics.Add(ground, image);
        return image;
    }

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/gasha_constants.tsv",
            new GeneratedTableSchema(
                "Gasha constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"], ["key"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));
    }

    private void LoadSpots()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/gasha_spots.tsv",
            new GeneratedTableSchema(
                "Gasha spots",
                GeneratedTableKeySemantics.Unique,
                ["group", "room", "subid", "y", "x", "rank", "ground", "replacement"],
                ["group", "room"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            SpotRecord spot = new SpotRecord(
                row.UnsignedDecimal(0), row.HexByte(1), row.HexByte(2),
                row.HexByte(3), row.HexByte(4), row.UnsignedDecimal(5),
                row.RequiredString(6), row.HexByte(7));
            _spotsByRoom.Add((spot.Group << 8) | spot.Room, spot);
            if (_spots[spot.SubId] != default)
                throw new InvalidOperationException($"Duplicate Gasha subid ${spot.SubId:x2}.");
            _spots[spot.SubId] = spot;
        }
    }

    private void LoadProbabilities()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/gasha_probabilities.tsv",
            new GeneratedTableSchema(
                "Gasha reward probabilities",
                GeneratedTableKeySemantics.Unique,
                ["rank", "class", "w0", "w1", "w2", "w3", "w4", "w5", "w6", "w7", "w8", "w9"],
                ["rank", "class"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int rank = row.UnsignedDecimal(0);
            int maturityClass = row.UnsignedDecimal(1);
            int total = 0;
            for (int reward = 0; reward < 10; reward++)
            {
                int weight = row.UnsignedDecimal(2 + reward);
                _probabilities[rank, maturityClass, reward] = weight;
                total += weight;
            }
            if (total != 256)
                throw new InvalidOperationException($"Gasha rank {rank} class {maturityClass} totals {total}.");
        }
    }

    private void LoadRingTiers()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/gasha_ring_tiers.tsv",
            new GeneratedTableSchema(
                "Gasha ring tiers",
                GeneratedTableKeySemantics.Unique,
                ["tier", "index", "ring"], ["tier", "index"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int tier = row.UnsignedDecimal(0);
            int index = row.UnsignedDecimal(1);
            if (_ringTiers[tier].Count != index)
                throw row.Invalid(1, "contiguous source order");
            _ringTiers[tier].Add(row.HexByte(2));
        }
    }

    private void LoadRewards()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/gasha_rewards.tsv",
            new GeneratedTableSchema(
                "Gasha rewards",
                GeneratedTableKeySemantics.Unique,
                ["type", "treasure-id", "parameter", "text-id", "sprite", "tile-base", "palette", "animation"],
                ["type"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int type = row.UnsignedDecimal(0);
            _rewards[type] = new RewardRecord(
                type, row.HexByte(1), row.HexByte(2), row.HexWord(3),
                new GashaSpotDatabaseVisualRecord(row.RequiredString(4), row.UnsignedDecimal(5),
                    row.UnsignedDecimal(6), row.RequiredString(7)));
        }
    }

    private static GashaSpotDatabaseVisualRecord LoadNutVisual()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/metadata/gasha_nut.tsv",
            new GeneratedTableSchema(
                "Gasha nut visual", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "animation"], headerRequired: true))
            .SingleRow();
        return new GashaSpotDatabaseVisualRecord(
            row.RequiredString(0), row.UnsignedDecimal(1),
            row.UnsignedDecimal(2), row.RequiredString(3));
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/gasha_text.tsv",
            new GeneratedTableSchema(
                "Gasha text", GeneratedTableKeySemantics.Unique,
                ["text-id", "message-base64"], ["text-id"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
    }

    private void LoadDisappearance()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/gasha_disappearance.tsv",
            new GeneratedTableSchema(
                "Gasha disappearance",
                GeneratedTableKeySemantics.Unique,
                ["phase", "source-start", "source-count", "destination-start",
                 "t0", "t1", "t2", "t3", "t4", "t5", "t6", "t7",
                 "t8", "t9", "t10", "t11", "t12", "t13", "t14", "t15"],
                ["phase"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int phase = row.UnsignedDecimal(0);
            var tileMap = new byte[16];
            for (int index = 0; index < tileMap.Length; index++)
                tileMap[index] = (byte)row.HexByte(4 + index);
            _disappearance[phase - 1] = new DisappearanceRecord(
                phase, row.UnsignedDecimal(1), row.UnsignedDecimal(2),
                row.UnsignedDecimal(3), tileMap);
        }
    }

    private int Constant(string key) => _constants.TryGetValue(key, out int value)
        ? value
        : throw new KeyNotFoundException($"Gasha constant '{key}' was not imported.");

    private void Validate()
    {
        if (_spotsByRoom.Count != 16 || _rewards.Length != 10 ||
            _ringTiers[0].Count != 8 || _ringTiers[1].Count != 8 ||
            _ringTiers[2].Count != 8 || _ringTiers[3].Count != 8 ||
            _ringTiers[4].Count != 2 || DisappearancePhases != 9 ||
            SoftSoilTile != 0xd2 || PlantedSoilTile != 0xf5 ||
            TreeTopLeftTile != 0x4e)
        {
            throw new InvalidOperationException("Imported Ages Gasha tables are incomplete or inconsistent.");
        }
    }
}
