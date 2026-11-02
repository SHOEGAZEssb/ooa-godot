
namespace Oracles;

internal sealed class DungeonSpinnerVisualDatabase
{
    internal DungeonInteractionVisual Visual { get; }

    internal DungeonSpinnerVisualDatabase(string assetRoot)
    {
        var table = GeneratedTable.Load(assetRoot + "objects/dungeon_spinner_visual.tsv",
            new("native spinner graphics", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "source-grayscale-inverted", "animations-base64", "source"],
                ["sprite"], headerRequired: true));
        var row = table.SingleRow();
        _ = row.RequiredString(5);
        Visual = new(assetRoot, "spinner", [row.RequiredString(0)], row.UnsignedDecimal(1),
            row.UnsignedDecimal(2), row.Boolean01(3), row.EncodedAnimations(4));
        if (Visual is not { TileBase: 0, Palette: 4, SourceGrayscaleInverted: true, Animations.Length: 4 })
            throw new InvalidOperationException($"{assetRoot}: INTERAC_SPINNER $7d parent/arrow graphics are incomplete.");
    }
}
