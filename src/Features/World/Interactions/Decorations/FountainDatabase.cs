using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class FountainDatabase
{
    private readonly Dictionary<int, FountainVisual> _visuals = new();
    private readonly List<FountainPlacement> _placements = new();
    private readonly HashSet<int> _paletteRooms = new();
    private readonly IReadOnlyDictionary<int, Color[]> _normal = Palette("normal");
    private readonly IReadOnlyDictionary<int, Color[]> _ruined = Palette("ruined");

    internal FountainDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/fountain_visuals.tsv",
            new GeneratedTableSchema("INTERAC_DECORATION fountain visuals", GeneratedTableKeySemantics.Unique,
                ["subid", "sprite", "tile-base", "palette", "animation", "source"], ["subid"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int subid = row.HexByte(0);
            if (subid is not (9 or 10)) throw row.Invalid(0, "fountain subid $09/$0a");
            _visuals.Add(subid, new(row.RequiredString(1), row.UnsignedDecimal(2),
                row.UnsignedDecimal(3), row.RequiredString(4)));
            _ = row.RequiredString(5);
        }
        table = GeneratedTable.Load("res://assets/oracle/objects/fountain_placements.tsv",
            new GeneratedTableSchema("INTERAC_DECORATION fountain placements", GeneratedTableKeySemantics.Ordered,
                ["group", "room", "subid", "y", "x", "source"], headerRequired: true));
        foreach (var row in table.Rows)
            _placements.Add(new(row.UnsignedDecimal(0), row.HexByte(1), row.HexByte(2),
                new(row.HexByte(4), row.HexByte(3)), row.RequiredString(5)));
        table = GeneratedTable.Load("res://assets/oracle/objects/fountain_palette_rooms.tsv",
            new GeneratedTableSchema("decoration.s Symmetry City palette keys", GeneratedTableKeySemantics.Unique,
                ["room", "source"], ["room"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            _paletteRooms.Add(row.HexByte(0));
            _ = row.RequiredString(1);
        }
    }

    internal IEnumerable<FountainPlacement> InRoom(int group, int room) =>
        _placements.Where(p => p.Group == group && p.Room == room);
    internal FountainVisual Visual(int subid) => _visuals[subid];
    internal bool UsesRuinedPalette(int group, int room, OracleSaveData? save) =>
        _paletteRooms.Contains(room) && (group != 0 || save?.HasRoomFlag(group, room, 1) != true);
    internal IReadOnlyDictionary<int, Color[]> Palettes(bool ruined) => ruined ? _ruined : _normal;

    private static IReadOnlyDictionary<int, Color[]> Palette(string name) =>
        new Dictionary<int, Color[]> { [6] = OracleGraphicsData.LoadPaletteColors(
            $"res://assets/oracle/objects/fountain_{name}_palette.bin", transparentZero: true) };
}

internal sealed record FountainVisual(string Sprite, int TileBase, int Palette, string Animation);
internal sealed record FountainPlacement(int Group, int Room, int SubId, Vector2 Position, string Source);
