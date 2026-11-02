
namespace Oracles;

// ROM_SEASONS' state01 stump entry/exit tables; the air owner remains shared.
internal sealed class TreeStumpJumpDatabase
{
    private readonly TreeStumpJumpRecord[,] _rows = new TreeStumpJumpRecord[2,4];
    internal Vector2I TargetOffset { get; }

    internal TreeStumpJumpDatabase(string assetRoot)
    {
        if (OracleAssetPack.GameFor(assetRoot) != OracleGame.Seasons)
            throw new ArgumentException("Tree-stump jumping requires its native Seasons tables.");
        var table = GeneratedTable.Load(assetRoot + "metadata/stump_jumps.tsv",
            new("native tree-stump jumps",GeneratedTableKeySemantics.Unique,
                ["phase","direction","probe-y","probe-x","speed-raw","speed-z","target-y","target-x","source"],
                ["phase","direction"],headerRequired:true));
        if (table.Rows.Count != 8) throw new InvalidOperationException($"{table.Path}: expected both four-direction native stump tables.");
        for (int index = 0; index < 8; index++)
        {
            var row = table.Rows[index];
            int phase = index / 4, direction = index % 4;
            if (row.RequiredString(0) != (phase == 0 ? "enter" : "leave") || row.Decimal(1) != direction)
                throw row.Invalid(0,"native entry/exit direction order");
            _rows[phase,direction] = new(new(row.Decimal(3,-128,127),row.Decimal(2,-128,127)),
                row.HexByte(4),row.Decimal(5,-32768,-1));
            var target = new Vector2I(row.Decimal(7,0,15),row.Decimal(6,0,15));
            if (index == 0) TargetOffset = target;
            else if (target != TargetOffset) throw row.Invalid(6,"the shared native landing-target offset");
            _ = row.RequiredString(8);
        }
    }

    internal TreeStumpJumpRecord Direction(bool leaving, int direction) =>
        direction is >= 0 and < 4 ? _rows[leaving ? 1 : 0,direction] : throw new ArgumentOutOfRangeException(nameof(direction));
}

internal readonly record struct TreeStumpJumpRecord(Vector2I Probe, int SpeedRaw, int SpeedZ);
