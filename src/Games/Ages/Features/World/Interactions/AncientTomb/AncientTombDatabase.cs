using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace Oracles;

internal sealed class AncientTombDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _objects = new();
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<int, (byte First, byte Second)> _torches = new();
    private readonly Dictionary<int, DungeonTileScriptProfile> _scripts = new();
    internal DungeonEssenceDefinition Essence { get; }
    internal IReadOnlyDictionary<int, Color[]> PanelPalette { get; }

    internal AncientTombDatabase()
    {
        var palette = OracleGraphicsData.LoadPalette(AgesGameContent.AssetRoot + "objects/stone_panel_palette.bin", 1, 6);
        PanelPalette = new Dictionary<int, Color[]> { [6] = [palette[6, 0], palette[6, 1], palette[6, 2], palette[6, 3]] };
        var objects = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/ancient_tomb_objects.tsv",
            new GeneratedTableSchema("Ancient Tomb objects", GeneratedTableKeySemantics.Grouped,
                ["group", "room", "order", "kind", "id", "subid", "y", "x", "condition", "source"],
                ["group", "room"], headerRequired: true));
        foreach (var row in objects.Rows)
        {
            var record = new DungeonObjectRecord(row.Decimal(0, 0, 7), row.HexByte(1), row.UnsignedDecimal(2),
                row.RequiredString(3) switch
                {
                    "stone-panel" => DungeonObjectKind.StonePanel,
                    "sarcophagus" => DungeonObjectKind.Sarcophagus,
                    "enemy-attached-key" => DungeonObjectKind.EnemyAttachedKey,
                    "blue-stalfos" => DungeonObjectKind.BlueStalfos,
                    "ramrock" => DungeonObjectKind.Ramrock,
                    "tile-script" => DungeonObjectKind.TileScript,
                    "colored-cube" => DungeonObjectKind.ColoredCube,
                    "minecart-gate" => DungeonObjectKind.MinecartGate,
                    "cube-light-sensor" => DungeonObjectKind.CubeLightSensor,
                    "cube-switch-sensor" => DungeonObjectKind.CubeSwitchSensor,
                    "floor-fill-chest" => DungeonObjectKind.FloorFillChest,
                    "tile-filler" => DungeonObjectKind.TileFiller,
                    "lever" => DungeonObjectKind.Lever,
                    "spinner" => DungeonObjectKind.Spinner,
                    "lever-lava-filler" => DungeonObjectKind.LeverLavaFiller,
                    "slate-slot" => DungeonObjectKind.SlateSlot,
                    "panel-stairs" => DungeonObjectKind.PanelStairs,
                    "essence" => DungeonObjectKind.Essence,
                    "boss-reward" => DungeonObjectKind.BossReward,
                    "miniboss-reward" => DungeonObjectKind.MinibossReward,
                    _ => throw row.Invalid(3, "an implemented Ancient Tomb object")
                }, row.HexByte(4), row.HexByte(5), row.HexByte(6), row.HexByte(7),
                DungeonObjectData.ParseCondition(row, 8), row.RequiredString(9));
            _objects.GetOrAdd((record.Group << 8) | record.Room).Add(record);
        }
        var texts = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/ancient_tomb_text.tsv",
            new GeneratedTableSchema("Ancient Tomb text", GeneratedTableKeySemantics.Unique,
                ["text-id", "text-position", "message-base64", "source"], ["text-id"], headerRequired: true));
        foreach (var row in texts.Rows)
        {
            _ = row.RequiredString(3);
            int position = row.Decimal(1, 0, 3);
            string message = Encoding.UTF8.GetString(Convert.FromBase64String(row.RequiredString(2)));
            _texts.Add(row.HexWord(0), position == 0 ? message : $"\\pos({position})" + message);
        }
        var torches = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/slate_slot_torches.tsv",
            new GeneratedTableSchema("slate socket torches", GeneratedTableKeySemantics.Unique,
                ["subid", "first", "second", "source"], ["subid"], headerRequired: true));
        foreach (var row in torches.Rows)
        {
            _ = row.RequiredString(3);
            _torches.Add(row.HexByte(0), ((byte)row.HexByte(1), (byte)row.HexByte(2)));
        }
        var scripts = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/ancient_tomb_tile_scripts.tsv",
            new GeneratedTableSchema("Ancient Tomb dungeon scripts", GeneratedTableKeySemantics.Unique,
                ["subid", "kind", "gate-value", "tile", "flag", "wait", "source"], ["subid"], headerRequired: true));
        foreach (var row in scripts.Rows)
        {
            var kind = row.RequiredString(1) switch
            {
                "trigger-stairs" => DungeonTileScriptKind.TriggerStairs,
                "wall-retraction" => DungeonTileScriptKind.WallRetraction,
                "enemy-stairs" => DungeonTileScriptKind.EnemyStairs,
                "torch-bridge" => DungeonTileScriptKind.TorchBridge,
                _ => throw row.Invalid(1, "a supported dungeon tile script")
            };
            _scripts.Add(row.HexByte(0), new(kind, row.HexByte(2), (byte)row.HexByte(3),
                (byte)row.HexByte(4), row.Decimal(5, 0, 255), row.RequiredString(6)));
        }
        var essence = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/ancient_tomb_essence.tsv",
            new GeneratedTableSchema("Falling Star Essence", GeneratedTableKeySemantics.Ordered,
                ["index", "destination-group", "destination-room", "destination-position", "destination-transition", "source"],
                headerRequired: true)).SingleRow();
        _ = essence.RequiredString(5);
        Essence = new(essence.UnsignedDecimal(0), Text(0x0015), new Warp(5, 0x77, -1, 0, 0,
            essence.Decimal(1, 0, 7), essence.HexByte(2), essence.HexByte(3), 0, essence.HexByte(4)));
        if (Essence.Index != 7 || Essence.ExitWarp is not { DestinationGroup: 1, DestinationRoom: 0x5c,
            DestinationPosition: 0x15, DestinationTransition: 0x0e } || _torches.Count != 4)
            throw new InvalidOperationException("Ancient Tomb requires four slate sockets and Falling Star exit $1:$5c/$15/$0e.");
    }
    internal string Text(int id) => _texts[id];
    internal (byte First, byte Second) Torches(int subid) => _torches[subid];
    internal DungeonTileScriptProfile Script(int subid) => _scripts[subid];
    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group, int room) => _objects.ValuesOrEmpty((group << 8) | room);
}
