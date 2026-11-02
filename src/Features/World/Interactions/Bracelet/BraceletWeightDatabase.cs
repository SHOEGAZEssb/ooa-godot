
namespace Oracles;

internal sealed class BraceletWeightDatabase
{
    public string AssetRoot { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<BraceletWeightDatabase>> Packs = new(StringComparer.Ordinal);
    internal static BraceletWeightDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<BraceletWeightDatabase>(() => new BraceletWeightDatabase(root))).Value;

    private readonly BraceletWeight[] _weights = new BraceletWeight[6];
    private readonly Vector2I[,,] _offsets = new Vector2I[5,4,4];
    internal BraceletWeightDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        var weights = GeneratedTable.Load(AssetRoot + "metadata/bracelet_weights.tsv",
            new GeneratedTableSchema("itemWeights", GeneratedTableKeySemantics.Unique,
                ["weight","gravity","initial-speed-z","speed-raw","toss-speed-raw","source"], ["weight"], headerRequired: true));
        if (weights.Rows.Count != 6) throw new InvalidOperationException($"{AssetRoot}: itemWeights requires six native rows.");
        for (int i = 0; i < 6; i++)
        {
            var row = weights.Rows[i];
            if (row.Decimal(0,0,5) != i) throw row.Invalid(0, $"ordered weight {i}");
            _weights[i] = new(row.Decimal(1,0,255), row.Decimal(2,-256,-1), row.Decimal(3,0,255), row.Decimal(4,0,255));
            _ = row.RequiredString(5);
        }
        var offsets = GeneratedTable.Load(AssetRoot + "metadata/bracelet_weight_offsets.tsv",
            new GeneratedTableSchema("liftedObjectPositions", GeneratedTableKeySemantics.Unique,
                ["weight","frame","direction","z","x","source"], ["weight","frame","direction"], headerRequired: true));
        if (offsets.Rows.Count != 80) throw new InvalidOperationException($"{AssetRoot}: liftedObjectPositions requires 80 native Z/X pairs.");
        for (int i = 0; i < 80; i++)
        {
            var row = offsets.Rows[i];
            int weight = row.Decimal(0,0,4), frame = row.Decimal(1,0,3), direction = row.Decimal(2,0,3);
            if (weight * 16 + frame * 4 + direction != i) throw row.Invalid(0, "ordered weight/frame/direction");
            _offsets[weight,frame,direction] = new(row.Decimal(4,-128,127), row.Decimal(3,-128,127));
            _ = row.RequiredString(5);
        }
    }
    internal BraceletWeight Weight(int weight) => weight is >= 0 and < 6 ? _weights[weight]
        : throw new NotSupportedException($"{AssetRoot}: itemWeights index ${weight:x2} is outside the native table.");
    internal Vector2I LiftOffset(int weight, int frame, int direction) =>
        weight is >= 0 and < 5 && frame is >= 0 and < 4 && direction is >= 0 and < 4 ? _offsets[weight,frame,direction]
        : throw new NotSupportedException($"liftedObjectPositions weight${weight:x2}/frame${frame:x2}/direction${direction:x2} is not represented.");
}

internal readonly record struct BraceletWeight(int Gravity, int InitialSpeedZ, int SpeedRaw, int TossSpeedRaw);
