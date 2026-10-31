using System;
using System.Collections.Generic;
using static Oracles.MenuPresentationTable;

namespace Oracles;

internal sealed class RingPresentationDatabase
{
    public string AssetRoot { get; }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<RingPresentationDatabase>> Packs = new(StringComparer.Ordinal);
    internal static RingPresentationDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<RingPresentationDatabase>(() => new(root))).Value;

    private readonly Dictionary<string, IReadOnlyList<MenuOamPart>> _ringOam =
        new(StringComparer.Ordinal);
    public IReadOnlyList<RingBoxOamOffset> RingBoxOffsets { get; }

    private RingPresentationDatabase(string assetRoot)
    {
        _ = OracleAssetPack.GameFor(assetRoot);
        AssetRoot = assetRoot;
        LoadOamLayouts(
            AssetRoot + "menu/ring_oam.tsv",
            "ring menu OAM", _ringOam,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["list-cursor"] = 1,
                ["page-arrows"] = 2,
                ["equipped-marker"] = 1,
                ["box-cursor"] = 1,
                ["list-box-marker"] = 1
            });
        RingBoxOffsets = LoadRingBoxOffsets();
    }

    public IReadOnlyList<MenuOamPart> RingOam(string layout) =>
        _ringOam.TryGetValue(layout, out IReadOnlyList<MenuOamPart>? parts)
            ? parts
            : throw new InvalidOperationException(
                $"Imported ring OAM layout '{layout}' does not exist.");

    private IReadOnlyList<RingBoxOamOffset> LoadRingBoxOffsets()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "menu/ring_box_oam_offsets.tsv",
            new GeneratedTableSchema(
                "ring-box OAM offsets",
                GeneratedTableKeySemantics.Unique,
                ["slot", "x-offset", "source-label", "source"],
                ["slot"],
                headerRequired: true));
        var result = new List<RingBoxOamOffset>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int slot = row.UnsignedDecimal(0);
            RequireNextIndex(row, slot, result.Count);
            result.Add(new RingBoxOamOffset(
                slot, row.HexByte(1), row.RequiredString(2),
                row.RequiredString(3)));
        }
        RequireCount(table, result.Count, 5);
        return result.AsReadOnly();
    }

}

internal readonly record struct RingBoxOamOffset(
    int Slot,
    int XOffset,
    string SourceLabel,
    string Source);
