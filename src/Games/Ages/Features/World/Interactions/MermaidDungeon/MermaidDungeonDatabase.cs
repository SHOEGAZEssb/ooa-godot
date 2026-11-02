
namespace Oracles;

internal sealed class MermaidDungeonDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _records = new();
    internal IReadOnlyList<KeyValuePair<int,byte>> OctogonInitialization { get; }
    internal DungeonEssenceDefinition Essence { get; }
    internal MermaidDungeonDatabase()
    {
        var table = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/mermaid_dungeon_objects.tsv",
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
        var essence = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/mermaid_dungeon_essence.tsv",
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
        var initial = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/octogon_initial_state.tsv",
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
}
