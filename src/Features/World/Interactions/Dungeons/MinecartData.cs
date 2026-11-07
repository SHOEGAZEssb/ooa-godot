using System.Collections.Concurrent;

namespace Oracles;

internal sealed class MinecartData
{
    private static readonly ConcurrentDictionary<string, Lazy<MinecartData>> Packs = new(StringComparer.Ordinal);
    internal static MinecartData For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<MinecartData>(() => new(key))).Value;
    private readonly System.Collections.Generic.Dictionary<string,int> _constants = new(StringComparer.Ordinal);
    internal int Constant(string name) => _constants.TryGetValue(name,out int value) ? value :
        throw new InvalidOperationException($"Missing native minecart constant '{name}'.");
    internal MinecartAnimationFrame[][] Waiting { get; } = [new MinecartAnimationFrame[1], new MinecartAnimationFrame[1]];
    internal MinecartAnimationFrame[][] Mounted { get; } = [new MinecartAnimationFrame[2], new MinecartAnimationFrame[2]];
    internal DungeonInteractionVisual Visual {get;}
    private readonly MinecartTrackRule[][] _tracks = [new MinecartTrackRule[3], new MinecartTrackRule[3], new MinecartTrackRule[3], new MinecartTrackRule[3]];

    internal bool TryTrackExit(int direction, int tile, int position, out int next, out int[] linked)
    {
        if (direction is < 0 or > 3) throw new NotSupportedException($"SPECIALOBJECT $0a unsupported direction${direction:x2}.");
        foreach (var row in _tracks[direction])
            if (row.Tile == tile) { next = (position + row.Offset) & 0xff; linked = row.Linked; return true; }
        next = position; linked = []; return false;
    }

    private MinecartData(string root)
    {
        var visual=GeneratedTable.Load(root+"objects/minecart_visual.tsv",
            new("native minecart graphics",GeneratedTableKeySemantics.Unique,
                ["sprite","tile-base","palette","source-grayscale-inverted","animations-base64","source"],["sprite"],headerRequired:true)).SingleRow();
        _=visual.RequiredString(5);
        Visual=new(root,"minecart",[visual.RequiredString(0)],visual.UnsignedDecimal(1),visual.UnsignedDecimal(2),
            visual.Boolean01(3),visual.EncodedAnimations(4));
        if(Visual.Animations.Length!=4)throw new InvalidOperationException($"{root}: INTERAC$16/SPECIALOBJECT$0a require four minecart animations.");
        foreach (var row in GeneratedTable.Load(root + "objects/minecart_behavior.tsv",
            new("native minecart behavior", GeneratedTableKeySemantics.Unique,
                ["name", "value", "source"], ["name"], headerRequired: true)).Rows)
        {
            _constants.Add(row.RequiredString(0),row.HexByte(1)); _=row.RequiredString(2);
        }
        if (_constants.Count != 17) throw new InvalidOperationException($"{root}: minecart requires seventeen native constants.");
        var tracks = GeneratedTable.Load(root + "objects/minecart_tracks.tsv",
            new("native minecart track links", GeneratedTableKeySemantics.Unique,
                ["direction", "order", "tile", "offset", "linked0", "linked1", "linked2", "source"],
                ["direction", "order"], headerRequired: true));
        if (tracks.Rows.Count != 12) throw new InvalidOperationException($"{root}: minecart requires twelve native track rows.");
        foreach (var row in tracks.Rows)
        {
            _tracks[row.Decimal(0,0,3)][row.Decimal(1,0,2)] = new(row.HexByte(2),unchecked((sbyte)row.HexByte(3)),
                [row.HexByte(4),row.HexByte(5),row.HexByte(6)]);
            _=row.RequiredString(7);
        }
        var table = GeneratedTable.Load(root + "objects/minecart_animation_state.tsv",
            new("minecart native animation", GeneratedTableKeySemantics.Unique,
                ["kind", "animation", "frame", "duration", "graphic", "parameter", "next-pointer", "oam-pointer", "source"],
                ["kind", "animation", "frame"], headerRequired: true));
        if (table.Rows.Count != 6) throw new InvalidOperationException($"{root}: minecart requires six compiled native frames.");
        foreach (var row in table.Rows)
        {
            bool waiting = row.RequiredString(0) switch
            {
                "waiting" => true, "mounted" => false,
                _ => throw row.Invalid(0, "waiting or mounted")
            };
            int animation = row.Decimal(1, 0, 1), frame = row.Decimal(2, 0, waiting ? 0 : 1);
            int duration = row.Decimal(3, 1, 0xff), graphic = row.HexByte(4), parameter = row.HexByte(5);
            int next = row.HexWord(6), oam = row.HexWord(7); _ = row.RequiredString(8);
            if (next is < 0x4000 or >= 0x8000 || oam is < 0x4000 or >= 0x8000 ||
                duration != (waiting ? 0x7f : 6) || graphic != animation * 2 + frame || parameter != frame * 4)
                throw row.Invalid(3, "the native minecart frame duration/graphic/parameter and banked cursors");
            (waiting ? Waiting : Mounted)[animation][frame] = new(duration, graphic, parameter, next, oam);
        }
    }
}

internal readonly record struct MinecartAnimationFrame(int Duration, int Graphic, int Parameter, int NextPointer, int OamPointer);
internal sealed record MinecartTrackRule(int Tile, int Offset, int[] Linked);
