using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CrownDungeonDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _records = new();
    internal int TriggerChestValue { get; }
    internal int TriggerChestWait { get; }
    internal IReadOnlyList<DungeonPatternHintTile> PatternHint { get; }
    internal ButtonBridgeData ButtonBridge { get; }
    internal DungeonEssenceDefinition Essence { get; }
    internal CrownDungeonDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/crown_dungeon_objects.tsv",
            new GeneratedTableSchema("Crown Dungeon native objects", GeneratedTableKeySemantics.Grouped,
                ["group","room","order","kind","id","subid","y","x","condition","source"],
                ["group","room"], headerRequired:true));
        foreach (var row in table.Rows)
        {
            var record = new DungeonObjectRecord(row.Decimal(0,0,7),row.HexByte(1),row.UnsignedDecimal(2),
                row.RequiredString(3) switch {
                    "smasher" => DungeonObjectKind.Smasher,
                    "smog-sentinel" => DungeonObjectKind.SmogSentinel,
                    "smog-controller" => DungeonObjectKind.SmogController,
                    "boss-reward" => DungeonObjectKind.BossReward,
                    "essence" => DungeonObjectKind.Essence,
                    "trigger-chest" => DungeonObjectKind.TriggerChestScript,
                    "tile-pattern-chest" => DungeonObjectKind.TilePatternChest,
                    "pattern-hint" => DungeonObjectKind.PatternHint,
                    "wall-squish" => DungeonObjectKind.WallSquish,
                    "button-bridge" => DungeonObjectKind.ButtonBridge,
                    "miniboss-reward" => DungeonObjectKind.MinibossReward,
                    _ => throw row.Invalid(3,"a supported Crown Dungeon object") },
                row.HexByte(4),row.HexByte(5),row.HexByte(6),row.HexByte(7),
                DungeonObjectData.ParseCondition(row,8),row.RequiredString(9));
            _records.GetOrAdd((record.Group << 8) | record.Room).Add(record);
        }
        if (table.Rows.Count != 13) throw new InvalidOperationException("Crown native objects require thirteen boss/reward/pattern/squish/bridge/essence records.");
        var essence = GeneratedTable.Load("res://assets/oracle/objects/crown_dungeon_essence.tsv",
            new GeneratedTableSchema("Crown Dungeon Essence", GeneratedTableKeySemantics.Ordered,
                ["index","text-id","text-position","message-base64","destination-group","destination-room",
                 "destination-position","destination-transition","source"],headerRequired:true)).SingleRow();
        string message = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(essence.RequiredString(3)));
        int position = essence.UnsignedDecimal(2);
        if(position!=0) message=$"\\pos({position})"+message;
        Essence = new(essence.UnsignedDecimal(0),message,new Warp(4,0xb8,-1,0,0,
            essence.Decimal(4,0,7),essence.HexByte(5),essence.HexByte(6),0,essence.HexByte(7)));
        if(Essence.Index!=4 || essence.HexWord(1)!=0x0012 || Essence.ExitWarp is not
            {DestinationGroup:0,DestinationRoom:0x0a,DestinationPosition:0x17,DestinationTransition:WarpDestinationTransition.SetRespawn})
            throw new InvalidOperationException("Crown Essence source text/exit mapping is incomplete.");
        var bridge = GeneratedTable.Load("res://assets/oracle/objects/crown_button_bridge.tsv",
            new GeneratedTableSchema("Crown button bridge",GeneratedTableKeySemantics.Unique,
                ["first","last","interval","hole","bridge","diamond","source"],["first"],headerRequired:true));
        GeneratedTableRow b = bridge.SingleRow();
        ButtonBridge = new(b.HexByte(0),b.HexByte(1),b.Decimal(2,1,255),(byte)b.HexByte(3),(byte)b.HexByte(4),(byte)b.HexByte(5),b.RequiredString(6));
        var script = GeneratedTable.Load("res://assets/oracle/objects/crown_trigger_chest_script.tsv",
            new GeneratedTableSchema("Crown trigger chest script",GeneratedTableKeySemantics.Unique,
                ["subid","trigger-value","wait","source"],["subid"],headerRequired:true));
        if (script.Rows.Count != 1 || script.Rows[0].HexByte(0) != 2)
            throw new InvalidOperationException("Crown trigger chest requires source subid $02.");
        TriggerChestValue = script.Rows[0].HexByte(1);
        TriggerChestWait = script.Rows[0].UnsignedDecimal(2);
        var hint = GeneratedTable.Load("res://assets/oracle/objects/crown_pattern_hint.tsv",
            new GeneratedTableSchema("Crown pattern hint",GeneratedTableKeySemantics.Unique,
                ["order","position","show-tile","restore-tile","source"],["order"],headerRequired:true));
        var hintTiles = new List<DungeonPatternHintTile>();
        foreach (var row in hint.Rows)
        {
            if (row.UnsignedDecimal(0) != hintTiles.Count) throw row.Invalid(0,"ordered hint writes");
            hintTiles.Add(new((byte)row.HexByte(1),(byte)row.HexByte(2),(byte)row.HexByte(3),row.RequiredString(4)));
        }
        if (hintTiles.Count != 6) throw new InvalidOperationException("INTERAC $21:$16 requires six hint writes.");
        PatternHint = hintTiles;
    }
    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group,int room) => _records.ValuesOrEmpty((group << 8) | room);
}

internal readonly record struct DungeonPatternHintTile(byte Position,byte ShowTile,byte RestoreTile,string Source);
