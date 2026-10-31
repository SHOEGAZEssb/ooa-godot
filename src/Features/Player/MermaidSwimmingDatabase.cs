using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Oracles;

// Ages' Mermaid Suit uses the same velocity operands in top-down and side view.
internal sealed class MermaidSwimmingDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<MermaidSwimmingDatabase>> Packs = new(StringComparer.Ordinal);
    internal static MermaidSwimmingDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<MermaidSwimmingDatabase>(() => new(root))).Value;

    internal MermaidSwimmingParameters Parameters { get; }

    private MermaidSwimmingDatabase(string assetRoot)
    {
        if (OracleAssetPack.GameFor(assetRoot) != OracleGame.Ages)
            throw new NotSupportedException("ROM_SEASONS: link.s does not contain the Ages Mermaid Suit movement branch.");
        var table = GeneratedTable.Load(assetRoot + "metadata/mermaid_swim_constants.tsv",
            new("Ages Mermaid Suit swimming",GeneratedTableKeySemantics.Unique,
                ["key","value","source"],["key"],headerRequired:true));
        var constants = table.Rows.ToDictionary(row => row.RequiredString(0),row => row.Decimal(1));
        int Constant(string key) => constants.TryGetValue(key,out int value) ? value :
            throw new InvalidOperationException($"{table.Path}: missing native Mermaid Suit operand '{key}'.");
        Parameters = new(Constant("mermaid-entry-updates"),Constant("mermaid-impulse-updates"),
            Constant("mermaid-impulse-counter"),Constant("mermaid-velocity-interval"),
            Constant("mermaid-target-speed"),Constant("mermaid-fast-target-speed"));
        if (constants.Count != 6 || Parameters != new MermaidSwimmingParameters(2,4,0x14,5,0x2d,0x37))
            throw new InvalidOperationException($"{table.Path}: native Mermaid Suit entry/impulse/speed operands changed.");
    }
}

internal readonly record struct MermaidSwimmingParameters(int MermaidEntryUpdates,int MermaidImpulseUpdates,
    int MermaidImpulseCounter,int MermaidVelocityInterval,int MermaidTargetSpeed,int MermaidFastTargetSpeed);
