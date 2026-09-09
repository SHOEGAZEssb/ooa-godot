using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class DebugObjectCatalog
{
    internal IReadOnlyList<DebugObjectEntry> Enemies { get; }
    internal IReadOnlyList<DebugObjectEntry> Drops { get; } = new DebugObjectEntry[]
    {
        new(0x01, ItemDropDatabase.Fairy, "FAIRY"),
        new(0x01, ItemDropDatabase.Heart, "HEART"),
        new(0x01, ItemDropDatabase.OneRupee, "1 RUPEE"),
        new(0x01, ItemDropDatabase.FiveRupees, "5 RUPEES"),
        new(0x01, ItemDropDatabase.Bombs, "BOMBS"),
        new(0x01, ItemDropDatabase.EmberSeeds, "EMBER SEEDS"),
        new(0x01, ItemDropDatabase.ScentSeeds, "SCENT SEEDS"),
        new(0x01, ItemDropDatabase.PegasusSeeds, "PEGASUS SEEDS"),
        new(0x01, ItemDropDatabase.GaleSeeds, "GALE SEEDS"),
        new(0x01, ItemDropDatabase.MysterySeeds, "MYSTERY SEEDS"),
        new(0x01, ItemDropDatabase.OneHundredRupeesOrEnemy, "100 RUPEES / ENEMY")
    };

    internal DebugObjectCatalog(EnemyDatabase enemies)
    {
        Enemies = enemies.EnemyHandlers.Handlers
            .OrderBy(handler => handler.Id).ThenBy(handler => handler.SubId)
            .Select(handler => new DebugObjectEntry(handler.Id, handler.SubId,
                handler.EnemyName.Replace("ENEMY_", "").Replace('_', ' '),
                handler.SupportsOrderedConstruction && handler.SupportsCombatSource))
            .ToArray();
    }
}

internal sealed record DebugObjectEntry(int Id, int SubId, string Name, bool Supported = true);
