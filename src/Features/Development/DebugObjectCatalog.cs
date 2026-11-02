
namespace Oracles;

internal sealed class DebugObjectCatalog
{
    internal IReadOnlyList<DebugObjectEntry> Enemies { get; }
    internal IReadOnlyList<DebugObjectEntry> Drops { get; }

    internal DebugObjectCatalog(string assetRoot)
    {
        Enemies = new EnemyHandlerRegistry(assetRoot).Handlers
            .OrderBy(handler => handler.Id).ThenBy(handler => handler.SubId)
            .Select(handler => new DebugObjectEntry(handler.Id, handler.SubId,
                handler.EnemyName.Replace("ENEMY_", "").Replace('_', ' '),
                // Room-owned native actors remain available to their producers.
                handler.SupportsStandaloneConstruction && handler.Handler is not
                    (EnemyHandlerKind.TargetCartCrystal or EnemyHandlerKind.GreatFairy),
                handler.Classification is EnemyHandlerClassification.OrderedImplemented or EnemyHandlerClassification.DynamicSpecial,
                handler.Source))
            .ToArray();
        string[] names = ["FAIRY","HEART","1 RUPEE","5 RUPEES","BOMBS","EMBER SEEDS","SCENT SEEDS",
            "PEGASUS SEEDS","GALE SEEDS","MYSTERY SEEDS","UNUSED","UNUSED","1 ORE CHUNK","10 ORE CHUNKS","50 ORE CHUNKS","100 RUPEES / ENEMY"];
        ItemDropDatabase drops = ItemDropDatabase.For(assetRoot);
        Drops = Enumerable.Range(0,16).Where(drops.IsRuntimeSupported)
            .Select(id => new DebugObjectEntry(1,id,names[id])).ToArray();
    }
}

internal sealed record DebugObjectEntry(int Id, int SubId, string Name, bool Supported = true,
    bool HasImportedDefinition = true, string Source = "");
