using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class SeasonsOpeningData
{
    internal const string Root = SeasonsGameContent.AssetRoot;
    internal SeasonsWorldData World { get; } = new();
    internal NewGameIntroDatabase LinkSprites { get; } = new(Root);
    internal readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> Scripts =
        CutsceneCommandCatalog.LoadPrograms(Root + "opening/commands.tsv");
    private readonly Dictionary<(int, int), GeneratedTableRow> _actors = [];
    private readonly Dictionary<(int, int), string> _actorAnimations = [];
    private readonly Dictionary<int, string> _texts = [];
    private readonly Dictionary<int, byte[]> _registers = [];
    private readonly Dictionary<int, (int Loop, int[] Parameters)> _linkLoops = [];
    internal (int Duration, int Palette)[] CastleLightning { get; }

    internal SeasonsOpeningData()
    {
        CastleLightning = Table("castle_lightning", ["duration", "palette", "source"]).Rows
            .Select(row => (row.Decimal(0, 1, 254), row.Decimal(1, 0, 2))).ToArray();
        foreach (var row in Table("actors", ["id", "subid", "sprite", "tile-base", "palette", "default-animation", "append-sprites", "source-offset", "source", "source-inverted"]).Rows)
            _actors.Add((row.HexByte(0), row.HexByte(1)), row);
        foreach (var row in Table("animations", ["id", "index", "frames", "source"]).Rows)
            _actorAnimations.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        foreach (var row in Table("text", ["id", "message-base64", "source"]).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        foreach (var row in Table("gfx_registers", ["index", "registers", "source"]).Rows)
            _registers.Add(row.HexByte(0), row.RequiredString(1).Split(',').Select(value => Convert.ToByte(value, 16)).ToArray());
        foreach (var row in Table("link_loops", ["animation", "loop-start", "parameters", "source"]).Rows)
            _linkLoops.Add(row.HexByte(0), (row.UnsignedDecimal(1), row.RequiredString(2).Split(',').Select(value => Convert.ToInt32(value, 16)).ToArray()));

    }

    private static GeneratedTable Table(string name, string[] columns) => GeneratedTable.Load(
        Root + $"opening/{name}.tsv", new($"Seasons opening {name}", GeneratedTableKeySemantics.Ordered, columns, headerRequired: true));

    internal string Text(int id) => _texts.TryGetValue(id, out var text) ? text :
        throw new NotSupportedException($"Seasons opening: unbound TX_{id:x4}.");
    internal byte[] Registers(int index) => _registers[index];
    internal ObjectSpriteFrame[] LinkFrames(int animation, int direction = 0) => LinkSprites.SpriteFrames($"link-native-{animation:x2}-{direction}");
    internal int LinkLoop(int animation) => _linkLoops[animation].Loop;
    internal int LinkParameter(int animation, int frame) => _linkLoops[animation].Parameters[frame];
    // Imported graphics also contain neighboring variants outside this opening.
    internal IEnumerable<(int Id, int Subid)> ActorVariants => _actors.Keys.Where(key => key.Item1 switch {
        0xa5 => key.Item2 == 8, 0x4e => key.Item2 <= 7, 0x4f => key.Item2 <= 4,
        0x9d or 0x9f or 5 or 8 or 0x27 or 0x77 or 0x86 or 0x88 => key.Item2 == 0, _ => false });
    internal IEnumerable<string> ActorAnimations(int id, int subid)
    {
        int initial = _actors[(id, subid)].UnsignedDecimal(5);
        // Only animations reachable with this variant's loaded OBJ graphics.
        int[] extra = (id,subid) switch {
            (0x4e,6) => [8,9,10,11,12,13], (0x4f,0) => [0x0e], (0x4f,1) => [1,2],
            (0x4f,3) => [6,7,8,9,10,11,12,13], (0x4f,4) => [5],
            (0xa5,_) => [0,1,2,3], (0x77,_) or (0x88,_) => [0,1], _ => [] };
        return extra.Prepend(initial).Distinct().Select(index => Animation(id,index));
    }
    internal IEnumerable<ObjectSpriteFrame> OpeningLinkFrames() => new[] { 0,7,8,9,0x0a,0x10,0x14 }
        .SelectMany(animation => Enumerable.Range(0,4).SelectMany(direction => LinkFrames(animation,direction)));
    internal string Animation(int id, int animation) => _actorAnimations.TryGetValue((id, animation), out var frames) ? frames :
        throw new NotSupportedException($"data/seasons/interactionAnimations.s: missing ${id:x2} animation ${animation:x2}.");

    internal NpcCharacter CreateActor(int id, int subid, Vector2 position)
    {
        // interactionGetData clamps beyond the terminating subid record.
        int last = _actors.Keys.Where(key => key.Item1 == id).Max(key => key.Item2);
        var row = _actors[(id, Math.Min(subid, last))];
        return SeasonsNpcPresentation.CreateActor(row, id, subid, position, index => Animation(id, index), World.SpritePalettes);
    }
    internal static NpcCharacter CreateLoadingDin()
    {
        var row = Table("actors", ["id", "subid", "sprite", "tile-base", "palette", "default-animation", "append-sprites", "source-offset", "source", "source-inverted"]).Rows
            .Single(value => value.HexByte(0) == 0x4e && value.HexByte(1) == 6);
        string frames = Table("animations", ["id", "index", "frames", "source"]).Rows
            .Single(value => value.HexByte(0) == 0x4e && value.HexByte(1) == row.UnsignedDecimal(5)).RequiredString(2);
        return SeasonsNpcPresentation.CreateActor(row, 0x4e, 6, Vector2.Zero, _ => frames,
            OracleGraphicsData.LoadPalette(Root + "world/rooms/room0098-0-spr.bin"));
    }

    internal IReadOnlyList<(int Id, int Subid, Vector2 Position)> TroupePlacements() =>
        Table("troupe", ["index", "id", "subid", "y", "x", "source"]).Rows.Select(row =>
            (row.HexByte(1), row.HexByte(2), new Vector2(row.HexByte(4), row.HexByte(3)))).ToArray();

}
