using System.Collections.Concurrent;

namespace Oracles;

internal sealed class DungeonRewardDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<DungeonRewardDatabase>> Packs=new(StringComparer.Ordinal);
    internal static DungeonRewardDatabase For(string root)=>Packs.GetOrAdd(root,
        static key=>new Lazy<DungeonRewardDatabase>(()=>new(key))).Value;
    internal IReadOnlyList<CutsceneCommand> SmallKeyCommands {get;}
    internal int MinibossWait {get;}
    internal int TreasurePuffWait {get;}

    private DungeonRewardDatabase(string root)
    {
        SmallKeyCommands=CutsceneCommandCatalog.Load(root+"cutscenes/enemy_small_key_commands.tsv");
        var table=GeneratedTable.Load(root+"objects/dungeon_reward_constants.tsv",
            new("common dungeon reward parameters",GeneratedTableKeySemantics.Unique,["key","value"],["key"],headerRequired:true));
        if(table.Rows.Count!=2 || table.Rows[0].RequiredString(0)!="miniboss-wait"||table.Rows[1].RequiredString(0)!="treasure-puff-wait")
            throw new InvalidOperationException($"{table.Path}: expected dungeonScript_minibossDeath and treasure puff wait operands.");
        MinibossWait=table.Rows[0].UnsignedDecimal(1);
        TreasurePuffWait=table.Rows[1].UnsignedDecimal(1);
    }
}
