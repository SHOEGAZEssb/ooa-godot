using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class TwinrovaDatabase
{
    private readonly Dictionary<string, byte[]> _tables = [];
    private readonly Dictionary<int, ImportedEnemyDefinition> _actors = [];
    private readonly Dictionary<int, string> _texts = [];
    private readonly Lookup<int, DungeonObjectRecord> _placements = new();
    private readonly Dictionary<int, RoomObjectRecord> _entries = [];
    internal DungeonInteractionVisual SnowDebris {get;}
    internal TwinrovaDatabase()
    {
        var snow=GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/twinrova_snow_debris.tsv",new("Twinrova snow debris",
            GeneratedTableKeySemantics.Unique,["sprite","tile-base","palette","inverted","animation","source"],["sprite"],headerRequired:true)).Rows[0];
        SnowDebris=new("snow-debris",[snow.RequiredString(0)],snow.Decimal(1),snow.Decimal(2),snow.Boolean01(3),[snow.RequiredString(4)]);
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/twinrova_placements.tsv", new("Twinrova before-event entry",
            GeneratedTableKeySemantics.Unique, ["group", "room", "order", "id", "subid", "flags", "count", "source"], ["group", "room", "order"], headerRequired: true)).Rows)
        {
            int group = row.Decimal(0, 0, 7), room = row.HexByte(1), order = row.UnsignedDecimal(2), id = row.HexByte(3), subid = row.HexByte(4);
            int key = group << 8 | room;
            _placements.GetOrAdd(key).Add(new(group, room, order, DungeonObjectKind.Twinrova, id, subid, 0, 0,
                DungeonObjectCondition.Flag80Clear, row.RequiredString(7)));
            _entries.Add(key, new(group, room, order, RoomObjectKind.RandomEnemy, id, subid, row.HexByte(5), row.Decimal(6, 1, 7), -1, -1, -1, 0xff)
                { SourceOverride = row.RequiredString(7) });
        }
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/twinrova_tables.tsv", new("Twinrova native tables",
            GeneratedTableKeySemantics.Unique, ["profile", "values", "source"], ["profile"], headerRequired: true)).Rows)
            _tables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1, ','), v => Convert.ToByte(v, 16)));
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/twinrova_actors.tsv", new("Twinrova actors",
            GeneratedTableKeySemantics.Unique, ["id", "subid", "sprites", "tile-base", "palette", "source-grayscale-inverted",
                "radius-y", "radius-x", "damage-quarters", "health", "animations-base64"], ["id", "subid"], headerRequired: true)).Rows)
            _actors.Add(row.HexByte(0), new(row.HexByte(0), row.HexByte(1), row.SplitRequired(2, ','), row.Decimal(3), row.Decimal(4), row.Boolean01(5),
                row.Decimal(6), row.Decimal(7), row.Decimal(8), row.Decimal(9), row.EncodedAnimations(10)));
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "cutscenes/twinrova_texts.tsv", new("Twinrova text",
            GeneratedTableKeySemantics.Unique, ["text-id", "message-base64", "source"], ["text-id"], headerRequired: true)).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        foreach (var (name, count) in new[] { ("entrance", 8), ("levitation", 4), ("attack-pattern", 1),
            ("collision-0a", 32), ("collision-08", 32), ("collision-09", 32), ("active-enemy-03", 32),
            ("collision-7a", 32), ("part-boundaries", 16),
            ("active-part-4b", 32), ("active-part-4d", 32), ("part-4b", 8), ("part-4d", 8) })
            if (!_tables.TryGetValue(name, out var values) || values.Length != count)
                throw new InvalidOperationException($"twinrova.s: missing or malformed {name}.");
        for (int subid = 0; subid < 2; subid++)
        for (int pattern = 0; pattern < 4; pattern++)
        {
            var values = Bytes($"targets-{subid}-{pattern}");
            if ((values.Length & 1) != 1 || values[^1] != 0)
                throw new InvalidOperationException($"twinrova.s: invalid subid${subid:x2} pattern${pattern:x2} sentinel.");
        }
    }
    internal ImportedEnemyDefinition Actor(int id) => _actors[id];
    internal byte[] Bytes(string name) => _tables[name];
    internal string Text(int id) => _texts[id];
    internal int Effect(int mode, int item) => Bytes($"collision-{mode:x2}")[item];
    internal bool Active(bool part, int id, int item) => Bytes($"active-{(part ? "part" : "enemy")}-{id:x2}")[item] != 0;
    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group, int room) => _placements.ValuesOrEmpty(group << 8 | room);
    internal RoomObjectRecord EntrySource(DungeonObjectRecord record) => _entries[record.Group << 8 | record.Room];
}
