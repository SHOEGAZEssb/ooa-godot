using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class OracleWramLayout
{
    internal static readonly Lazy<OracleWramLayout> Shared = new(() => new());
    private readonly Dictionary<string, (int? Ages, int? Seasons)> _fields = new(StringComparer.Ordinal);

    private OracleWramLayout()
    {
        foreach (var row in GeneratedTable.Load(OracleAssetPack.CommonRoot + "metadata/wram_fields.tsv",
            new("WRAM fields", GeneratedTableKeySemantics.Unique,
                ["symbol", "ages-address", "seasons-address", "source"], ["symbol"], headerRequired: true)).Rows)
            _fields.Add(row.RequiredString(0),
                (row.String(1) == "-" ? null : row.HexWord(1), row.String(2) == "-" ? null : row.HexWord(2)));
    }

    internal int? Resolve(OracleGame game, string symbol, int offset = 0)
    {
        if (!_fields.TryGetValue(symbol, out var field))
            throw new InvalidOperationException($"include/wram.s: unimported symbol {symbol}.");
        int? address = game switch
        {
            OracleGame.Ages => field.Ages,
            OracleGame.Seasons => field.Seasons,
            _ => throw new ArgumentOutOfRangeException(nameof(game))
        };
        if (address is not int start) return null;
        int result = checked(start + offset);
        if (offset < 0 || result is < OracleRuntimeState.WramStart or > OracleRuntimeState.WramEnd)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return result;
    }

    internal int Require(OracleGame game, string symbol, int offset = 0) => Resolve(game, symbol, offset)
        ?? throw new NotSupportedException($"include/wram.s:{symbol} is absent in {game}.");
}
