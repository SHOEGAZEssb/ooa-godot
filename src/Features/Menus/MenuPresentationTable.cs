
namespace Oracles;

internal static class MenuPresentationTable
{
    internal static void LoadOamLayouts(
        string path,
        string name,
        Dictionary<string, IReadOnlyList<MenuOamPart>> destination,
        IReadOnlyDictionary<string, int> expectedCounts)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                name,
                GeneratedTableKeySemantics.Unique,
                [
                    "layout", "part", "y", "x", "tile", "attributes",
                    "source-label", "alias-of", "source"
                ],
                ["layout", "part"],
                headerRequired: true));
        var grouped = new Dictionary<string, List<MenuOamPart>>(
            StringComparer.Ordinal);
        foreach (GeneratedTableRow row in table.Rows)
        {
            string layout = row.RequiredString(0);
            if (!expectedCounts.ContainsKey(layout))
                throw row.Invalid(0, $"one of {string.Join(", ", expectedCounts.Keys)}");
            if (!grouped.TryGetValue(layout, out List<MenuOamPart>? parts))
            {
                parts = new List<MenuOamPart>();
                grouped.Add(layout, parts);
            }
            int part = row.UnsignedDecimal(1);
            RequireNextIndex(row, part, parts.Count);
            parts.Add(new MenuOamPart(
                part, row.HexByte(2), row.HexByte(3), row.HexByte(4),
                row.HexByte(5), row.RequiredString(6), row.String(7),
                row.RequiredString(8)));
        }
        foreach ((string layout, int count) in expectedCounts)
        {
            if (!grouped.TryGetValue(layout, out List<MenuOamPart>? parts) ||
                parts.Count != count)
            {
                throw new InvalidOperationException(
                    $"{path}: OAM layout '{layout}' expected {count} parts, got " +
                    $"{(parts is null ? 0 : parts.Count)}.");
            }
            destination.Add(layout, parts.AsReadOnly());
        }
    }

    internal static void RequireNextIndex(
        GeneratedTableRow row,
        int actual,
        int expected)
    {
        if (actual != expected)
            throw row.Invalid(0, $"ordered index {expected}");
    }

    internal static void RequireCount(
        GeneratedTable table,
        int actual,
        int expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"{table.Path}: schema '{table.Schema.Name}' expected " +
                $"{expected} ordered records, got {actual}.");
        }
    }
}

internal readonly record struct MenuOamPart(
    int Part,
    int Y,
    int X,
    int Tile,
    int Attributes,
    string SourceLabel,
    string AliasOf,
    string Source);
