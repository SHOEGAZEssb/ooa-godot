using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class MermaidDungeonDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _records = new();
    private readonly Dictionary<(int Group,int Room,int Id,int SubId),int> _enemyOrders = new();
    internal IReadOnlyList<KeyValuePair<int,byte>> OctogonInitialization { get; }
    internal RoomObjectRecord BossKeyFailureEnemies { get; }
    internal DungeonEssenceDefinition Essence { get; }
    internal MermaidDungeonDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/mermaid_dungeon_objects.tsv",
            new GeneratedTableSchema("Mermaid's Cave native objects", GeneratedTableKeySemantics.Grouped,
                ["group","room","order","kind","id","subid","y","x","condition","source"],
                ["group","room"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            var record = new DungeonObjectRecord(row.Decimal(0,0,7),row.HexByte(1),row.UnsignedDecimal(2),
                row.RequiredString(3) switch {
                    "vire" => DungeonObjectKind.Vire,
                    "octogon-initializer" => DungeonObjectKind.OctogonInitializer,
                    "octogon" => DungeonObjectKind.Octogon,
                    "boss-reward" => DungeonObjectKind.BossReward,
                    "miniboss-reward" => DungeonObjectKind.MinibossReward,
                    "colored-cube" => DungeonObjectKind.ColoredCube,
                    "cube-floor-sensor" => DungeonObjectKind.CubeFloorSensor,
                    "tile-pattern-chest" => DungeonObjectKind.TilePatternChest,
                    "mermaid-changing-floor" => DungeonObjectKind.MermaidChangingFloor,
                    "mermaid-boss-key" => DungeonObjectKind.MermaidBossKey,
                    "mermaid-torch-order" => DungeonObjectKind.MermaidTorchOrder,
                    "lever" => DungeonObjectKind.Lever,
                    "room-flag-mirror" => DungeonObjectKind.RoomFlagMirror,
                    "boss-key-mirror" => DungeonObjectKind.BossKeyMirror,
                    "spinner" => DungeonObjectKind.Spinner,
                    "signal-script" => DungeonObjectKind.SignalScript,
                    "essence" => DungeonObjectKind.Essence,
                    "floor-color-changer" => DungeonObjectKind.FloorColorChanger,
                    "toggle-floor" => DungeonObjectKind.ToggleFloor,
                    _ => throw row.Invalid(3,"a supported Mermaid's Cave object") },
                row.HexByte(4),row.HexByte(5),row.HexByte(6),row.HexByte(7),
                DungeonObjectData.ParseCondition(row,8),row.RequiredString(9));
            _records.GetOrAdd((record.Group << 8) | record.Room).Add(record);
        }
        if (table.Rows.Count != 28) throw new InvalidOperationException("Mermaid's Cave requires twenty-eight ordered native placements.");
        var essence = GeneratedTable.Load("res://assets/oracle/objects/mermaid_dungeon_essence.tsv",
            new GeneratedTableSchema("Mermaid's Cave Essence",GeneratedTableKeySemantics.Ordered,
                ["index","text-id","text-position","message-base64","destination-group","destination-room",
                 "destination-position","destination-transition","source"],headerRequired:true)).SingleRow();
        string message=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(essence.RequiredString(3)));
        int position=essence.UnsignedDecimal(2);
        if (position != 0) message=$"\\pos({position})"+message;
        Essence=new(essence.UnsignedDecimal(0),message,new Warp(5,0x37,-1,0,0,
            essence.Decimal(4,0,7),essence.HexByte(5),essence.HexByte(6),0,essence.HexByte(7)));
        if (Essence.Index != 5 || essence.HexWord(1) != 0x0013 || Essence.ExitWarp is not
            {DestinationGroup:3,DestinationRoom:0x0f,DestinationPosition:0x16,DestinationTransition:WarpDestinationTransition.SetRespawn})
            throw new InvalidOperationException("Mermaid Essence source text/exit mapping is incomplete.");
        var failure = GeneratedTable.Load("res://assets/oracle/objects/mermaid_boss_key_enemies.tsv",
            new GeneratedTableSchema("Mermaid boss-key failure enemies",GeneratedTableKeySemantics.Unique,
                ["id","subid","flags","count","source"],["id","subid"],headerRequired:true));
        if (failure.Rows.Count != 1) throw new InvalidOperationException("objectData78db requires one random-enemy row.");
        var failureRow = failure.Rows[0];
        BossKeyFailureEnemies = new(5,0x1c,0,RoomObjectKind.RandomEnemy,failureRow.HexByte(0),failureRow.HexByte(1),
            failureRow.HexByte(2),failureRow.Decimal(3,1,7),0,0,0,0) { SourceOverride = failureRow.RequiredString(4) };
        var orders = GeneratedTable.Load("res://assets/oracle/objects/mermaid_enemy_orders.tsv",
            new GeneratedTableSchema("Mermaid native enemy insertion",GeneratedTableKeySemantics.Unique,
                ["group","room","id","subid","enemy-stream-order","source"],
                ["group","room","id","subid"],headerRequired:true));
        foreach (var row in orders.Rows)
        {
            _ = row.RequiredString(5);
            _enemyOrders.Add((row.Decimal(0,0,7),row.HexByte(1),row.HexByte(2),row.HexByte(3)),row.UnsignedDecimal(4));
        }
        if (orders.Rows.Count != 3) throw new InvalidOperationException("Mermaid native enemies require three source insertion points.");
        var initial = GeneratedTable.Load("res://assets/oracle/objects/octogon_initial_state.tsv",
            new GeneratedTableSchema("Octogon encounter initialization",GeneratedTableKeySemantics.Unique,
                ["field","address","value","source"],["field"],headerRequired:true));
        string[] fields = ["loadedExtraGfx","var03","direction","health","y","x","var30"];
        if (initial.Rows.Count != fields.Length) throw new InvalidOperationException("miscPuzzles_subid0f requires seven ordered writes, preserving $cfd7.");
        OctogonInitialization = initial.Rows.Select((row,index) =>
        {
            if (row.RequiredString(0) != fields[index] || row.HexWord(1) != 0xcfd0 + index)
                throw row.Invalid(0,$"ordered Octogon field {fields[index]} at ${0xcfd0 + index:x4}");
            _ = row.RequiredString(3);
            return new KeyValuePair<int,byte>(row.HexWord(1),(byte)row.HexByte(2));
        }).ToArray();
    }
    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group,int room) => _records.ValuesOrEmpty((group << 8) | room);
    internal int EnemyStreamOrder(DungeonObjectRecord record) =>
        _enemyOrders.TryGetValue((record.Group,record.Room,record.Id,record.SubId),out int order) ? order :
        throw new InvalidOperationException($"{record.Source}: missing enemy-stream insertion for ENEMY${record.Id:x2}:${record.SubId:x2}.");
}
