using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class JabuDungeonDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _records = new();
    private readonly Dictionary<int,string> _texts = new();
    internal IReadOnlyList<byte>[] HookKeyPattern { get; }
    internal DungeonEssenceDefinition Essence { get; }

    internal JabuDungeonDatabase()
    {
        var objects = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/jabu_dungeon_objects.tsv",
            new GeneratedTableSchema("Jabu-Jabu native objects", GeneratedTableKeySemantics.Grouped,
                ["group","room","order","kind","id","subid","y","x","condition","source"],
                ["group","room"], headerRequired:true));
        foreach (var row in objects.Rows)
        {
            var record = new DungeonObjectRecord(row.Decimal(0,0,7),row.HexByte(1),row.UnsignedDecimal(2),
                row.RequiredString(3) switch {
                    "jabu-water" => DungeonObjectKind.JabuWater,
                    "jabu-ladder" => DungeonObjectKind.JabuLadder,
                    "jabu-hook-key" => DungeonObjectKind.JabuHookKey,
                    "angler-fish" => DungeonObjectKind.AnglerFish,
                    "plasmarine" => DungeonObjectKind.Plasmarine,
                    "boss-reward" => DungeonObjectKind.BossReward,
                    "essence" => DungeonObjectKind.Essence,
                    _ => throw row.Invalid(3,"a supported Jabu-Jabu native object") },
                row.HexByte(4),row.HexByte(5),row.HexByte(6),row.HexByte(7),
                DungeonObjectData.ParseCondition(row,8),row.RequiredString(9));
            _records.GetOrAdd((record.Group << 8) | record.Room).Add(record);
        }
        var pattern = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/jabu_hook_key_tiles.tsv",
            new GeneratedTableSchema("Jabu Switch Hook key",GeneratedTableKeySemantics.Unique,
                ["position","tile","source"],["position"],headerRequired:true));
        HookKeyPattern = [pattern.Rows.Select(row => {
            if (row.HexByte(1) != 0xdb) throw row.Invalid(1,"TILEINDEX_SWITCH_DIAMOND $db");
            _ = row.RequiredString(2);
            return (byte)row.HexByte(0);
        }).ToArray()];
        var texts = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/jabu_dungeon_text.tsv",
            new GeneratedTableSchema("Jabu native text",GeneratedTableKeySemantics.Unique,
                ["text-id","text-position","message-base64","source"],["text-id"],headerRequired:true));
        foreach (var row in texts.Rows)
        {
            _ = row.RequiredString(3);
            int position=row.Decimal(1,0,3);
            string message=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row.RequiredString(2)));
            _texts.Add(row.HexWord(0),position==0 ? message : $"\\pos({position})"+message);
        }
        var essence = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/jabu_dungeon_essence.tsv",
            new GeneratedTableSchema("Rolling Sea Essence",GeneratedTableKeySemantics.Ordered,
                ["index","destination-group","destination-room","destination-position","destination-transition","source"],
                headerRequired:true)).SingleRow();
        _ = essence.RequiredString(5);
        Essence = new(essence.UnsignedDecimal(0),Text(0x0014),new Warp(5,0x61,-1,0,0,
            essence.Decimal(1,0,7),essence.HexByte(2),essence.HexByte(3),0,essence.HexByte(4)));
        if (Essence.Index != 6 || Essence.ExitWarp is not {DestinationGroup:2,DestinationRoom:0x90,DestinationPosition:0x45,DestinationTransition:WarpDestinationTransition.XShifted})
            throw new InvalidOperationException("essence.s: Rolling Sea requires index$06 and room$2:$90/$45 X-shifted exit.");
    }
    internal string Text(int id) => _texts.TryGetValue(id,out var text) ? text :
        throw new InvalidOperationException($"miscPuzzles.s: missing Jabu text${id:x4}.");
    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group,int room) => _records.ValuesOrEmpty((group << 8) | room);
}
