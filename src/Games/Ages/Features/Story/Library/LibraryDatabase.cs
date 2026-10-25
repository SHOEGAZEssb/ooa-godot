using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class LibraryDatabase
{
    internal IReadOnlyList<CutsceneCommand> Powder { get; } = Commands("powder");
    internal IReadOnlyList<CutsceneCommand> Book { get; } = Commands("book");
    internal IReadOnlyList<CutsceneCommand> Secret { get; } = Commands("secret");
    internal IReadOnlyList<CutsceneCommand> Generic { get; } = Commands("generic");
    internal IReadOnlyList<CutsceneCommand> Podium { get; } = Commands("podium");
    private readonly Dictionary<int, LibraryNpcData> _npcs = new();
    private readonly Dictionary<int, LibraryPathData> _paths = new();
    private readonly Dictionary<(int Id, int SubId), LibraryVisualData> _visuals = new();
    private readonly Dictionary<(int Id, int SubId), LibraryReaderData> _readers = new();
    internal IReadOnlyList<LibraryPlacement> Placements { get; }
    internal LibraryDatabase()
    {
        foreach (var row in Table("npcs", ["subid", "radius-y", "palette", "animation", "polluted-text", "clean-text", "polluted-message", "clean-message"]).Rows)
            _npcs.Add(row.HexByte(0), new(row.HexByte(1), row.UnsignedDecimal(2), row.UnsignedDecimal(3),
                row.HexWord(4), row.HexWord(5), row.Base64Utf8(6), row.Base64Utf8(7)));
        foreach (var row in Table("paths", ["subid", "y", "x", "path", "text-id", "message"]).Rows)
        {
            byte[] path = row.RequiredString(3).Split(',').Select(byte.Parse).ToArray();
            if (path.Length < 2 || path[^1] != 0 || path[..^1].Contains((byte)0))
                throw row.Invalid(3, "a zero-terminated nonempty book path");
            _paths.Add(row.UnsignedDecimal(0), new(row.HexByte(1), row.HexByte(2), path, row.HexWord(4), row.Base64Utf8(5)));
        }
        foreach (var row in Table("visuals", ["id", "subid", "sprite", "tile-base", "palette", "animation0", "animation1", "animation2", "animation3"]).Rows)
            _visuals.Add((row.HexByte(0), row.HexByte(1)), new(row.RequiredString(2), row.UnsignedDecimal(3), row.UnsignedDecimal(4),
                [row.RequiredString(5), row.String(6), row.String(7), row.String(8)]));
        foreach (var row in Table("readers", ["id", "subid", "radius-y", "radius-x", "initial-animation", "always-update", "solid-mark", "before-text", "after-text", "before-message", "after-message"]).Rows)
        {
            int id = row.HexByte(0), subid = row.HexByte(1);
            _readers.Add((id, subid), new(row.HexByte(2), row.HexByte(3), row.UnsignedDecimal(4),
                row.Boolean01(5), row.Boolean01(6), row.HexWord(7), row.HexWord(8),
                row.Base64Utf8(9), row.Base64Utf8(10), Commands($"reader_{id:x2}")));
        }
        Placements = Table("placements", ["group", "room", "order", "id", "subid", "y", "x"])
            .Rows.Select(row => new LibraryPlacement(row.UnsignedDecimal(0), row.HexByte(1), row.UnsignedDecimal(2),
                row.HexByte(3), row.HexByte(4), row.HexByte(5), row.HexByte(6))).ToArray();
        if (_npcs.Count != 4 || _paths.Count != 6 || _visuals.Count != 9 || Placements.Count != 3 || _readers.Count != 5)
            throw new InvalidOperationException("Library native table boundaries changed.");
    }
    internal LibraryNpcData Npc(int subid) => _npcs[subid];
    internal LibraryPathData Path(int subid) => _paths[subid];
    internal bool IsReader(NpcRecord record) => _readers.ContainsKey((record.Id, record.SubId));
    internal LibraryReaderData Reader(NpcRecord record) => _readers[(record.Id, record.SubId)];
    internal string Animation(int id, int animation, int subid = -1)
    {
        int resolvedSubId = subid < 0 ? id == AgesInteractionId.Decoration ? 7 : 0 : subid;
        string encoded = Visual(id, resolvedSubId).Animations[animation];
        if (encoded.Length == 0)
            throw new InvalidOperationException($"Library INTERAC ${id:x2}:${resolvedSubId:x2} has no source animation ${animation:x2}.");
        return encoded;
    }
    private LibraryVisualData Visual(int id, int subid) =>
        _visuals.TryGetValue((id, subid), out var visual) ? visual : _visuals[(id, 0)];
    internal NpcRecord Actor(int group, int room, int id, int subid, int y, int x)
    {
        var v = Visual(id, subid);
        return new(group, room, id, subid, y, x, 0, 0, v.Sprite, v.TileBase, v.Palette, 0, false,
            v.Animations[0], v.Animations[1], v.Animations[2], v.Animations[3], "", NpcImplementationClassification.SpecializedNative);
    }
    private static IReadOnlyList<CutsceneCommand> Commands(string role) =>
        CutsceneCommandCatalog.Load(AgesGameContent.AssetRoot + $"cutscenes/library_{role}_commands.tsv");
    private static GeneratedTable Table(string role, string[] columns) => GeneratedTable.Load(
        AgesGameContent.AssetRoot + $"objects/library_{role}.tsv",
        new GeneratedTableSchema($"library {role}", GeneratedTableKeySemantics.Unique, columns,
            role == "placements" ? ["group", "room", "order"] : role is "visuals" or "readers" ? ["id", "subid"] : [columns[0]], headerRequired: true));
}

internal sealed record LibraryNpcData(int RadiusY, int Palette, int Animation, int PollutedText, int CleanText, string PollutedMessage, string CleanMessage);
internal sealed record LibraryPathData(int Y, int X, byte[] Path, int TextId, string Message);
internal sealed record LibraryVisualData(string Sprite, int TileBase, int Palette, string[] Animations);
internal sealed record LibraryPlacement(int Group, int Room, int Order, int Id, int SubId, int Y, int X);
internal sealed record LibraryReaderData(int RadiusY, int RadiusX, int InitialAnimation, bool AlwaysUpdate,
    bool SolidMark, int BeforeText, int AfterText, string BeforeMessage, string AfterMessage, IReadOnlyList<CutsceneCommand> Commands);
