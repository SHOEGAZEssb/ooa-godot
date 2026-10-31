using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

public sealed class WarpDatabase
{
    public string AssetRoot { get; }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<WarpDatabase>> Packs = new(StringComparer.Ordinal);
    internal static WarpDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<WarpDatabase>(() => new WarpDatabase(root))).Value;

    private readonly WarpSourceRecord[] _sources;
    private readonly WarpDestinationRecord[] _destinations;
    private readonly int[] _sourceRoots = new int[8];
    private readonly int[] _destinationRoots = new int[8];
    private readonly Dictionary<(int Mode, int Tile), byte> _tiles = new();
    private readonly Dictionary<(int Mode, int Tile), int> _facing = new();
    private readonly bool _hasEndMarker;
    private readonly int _biasGroup;
    private readonly int _biasAddress;
    private readonly int _smallGroups;
    private readonly int _smallEdgeSplit;
    private readonly int _largeEdgeSplit;
    private readonly WarpTransitionHandler[] _handlers = new WarpTransitionHandler[16];
    private readonly string[] _handlerSources = new string[16];

    public WarpDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        var handlers = GeneratedTable.Load(assetRoot + "metadata/warp_handlers.tsv",
            new("native warp dispatch",GeneratedTableKeySemantics.Unique,
                ["mode","handler","source"],["mode"],headerRequired:true));
        if (handlers.Rows.Count != 16) throw new InvalidOperationException($"{assetRoot}: expected all 16 native warp modes.");
        for (int mode = 0; mode < 16; mode++)
        {
            var row = handlers.Rows[mode];
            string name = row.RequiredString(1);
            if (row.HexByte(0) != mode || !Enum.TryParse(name,out WarpTransitionHandler handler) || Enum.GetName(handler) != name)
                throw row.Invalid(1,"the ordered native warp handler name");
            _handlers[mode] = handler;
            _handlerSources[mode] = row.RequiredString(2);
        }
        var sources = GeneratedTable.Load(assetRoot + "objects/warps.tsv",
            new("native warp sources",GeneratedTableKeySemantics.Unique,
                ["index","flags","key","data-low","data-high","pointer","source"],["index"],headerRequired:true));
        if (sources.Rows.Count == 0) throw new InvalidOperationException($"{assetRoot}: native warp source stream is empty.");
        _sources = new WarpSourceRecord[sources.Rows.Count];
        for (int index = 0; index < _sources.Length; index++)
        {
            var row = sources.Rows[index];
            if (row.UnsignedDecimal(0) != index) throw row.Invalid(0,"contiguous native warp stream indices");
            _sources[index] = new(row.HexByte(1),row.HexByte(2),row.HexByte(3),row.HexByte(4),
                row.Decimal(5,-1,_sources.Length - 1),row.RequiredString(6));
            if (_sources[index].Flags != 0xff && (_sources[index].Flags & 0x30) != 0 ||
                (_sources[index].Flags & 0xc0) == 0x40 && _sources[index].Pointer < 0)
                throw row.Invalid(1,"native standard/pointer/default flags and a valid pointer target");
        }
        var destinations = GeneratedTable.Load(assetRoot + "objects/warp_destinations.tsv",
            new("native warp destinations",GeneratedTableKeySemantics.Unique,
                ["index","room","position","parameter","transition","source"],["index"],headerRequired:true));
        _destinations = new WarpDestinationRecord[destinations.Rows.Count];
        for (int index = 0; index < _destinations.Length; index++)
        {
            var row = destinations.Rows[index];
            if (row.UnsignedDecimal(0) != index) throw row.Invalid(0,"contiguous native destination indices");
            _destinations[index] = new(row.HexByte(1),row.HexByte(2),row.Decimal(3,0,15),row.Decimal(4,0,15));
        }
        var groups = GeneratedTable.Load(assetRoot + "objects/warp_groups.tsv",
            new("native warp roots",GeneratedTableKeySemantics.Unique,
                ["group","source-start","destination-start","end-marker","bias-group","bias-address","small-groups","small-edge-split","large-edge-split","source"],["group"],headerRequired:true));
        if (groups.Rows.Count != 8) throw new InvalidOperationException($"{assetRoot}: expected eight native warp group roots.");
        _hasEndMarker = groups.Rows[0].Boolean01(3);
        _biasGroup = groups.Rows[0].Decimal(4,-1,7);
        _biasAddress = groups.Rows[0].HexWord(5);
        _smallGroups = groups.Rows[0].Decimal(6,1,8);
        _smallEdgeSplit = groups.Rows[0].HexByte(7);
        _largeEdgeSplit = groups.Rows[0].HexByte(8);
        for (int group = 0; group < 8; group++)
        {
            var row = groups.Rows[group];
            if (row.UnsignedDecimal(0) != group || row.Boolean01(3) != _hasEndMarker ||
                row.Decimal(4,-1,7) != _biasGroup || row.HexWord(5) != _biasAddress ||
                row.Decimal(6,1,8) != _smallGroups || row.HexByte(7) != _smallEdgeSplit || row.HexByte(8) != _largeEdgeSplit)
                throw row.Invalid(0,"ordered native group roots sharing one lookup contract");
            _sourceRoots[group] = row.Decimal(1,0,_sources.Length - 1);
            _destinationRoots[group] = row.Decimal(2,0,_destinations.Length);
        }
        if (_biasGroup < 0 ? _biasAddress != 0 : _biasAddress is < 0xc5b0 or >= 0xcb00)
            throw new InvalidOperationException($"{assetRoot}: native warp destination offset requires its saved-byte address.");
        var tiles = GeneratedTable.Load(assetRoot + "metadata/warp_tiles.tsv",
            new("native warp tiles",GeneratedTableKeySemantics.Grouped,
                ["mode","index","tile","parameter","source"],["mode"],headerRequired:true));
        var counts = new int[6];
        foreach (var row in tiles.Rows)
        {
            int mode = row.Decimal(0,0,5);
            if (row.UnsignedDecimal(1) != counts[mode]++) throw row.Invalid(1,"ordered native warp-tile entries");
            _tiles.TryAdd((mode,row.HexByte(2)),(byte)row.Decimal(3,0,1));
        }
        var facing = GeneratedTable.Load(assetRoot + "metadata/warp_facing.tsv",
            new("native warp arrival facing",GeneratedTableKeySemantics.Grouped,
                ["mode","index","tile","direction","source"],["mode"],headerRequired:true));
        Array.Clear(counts);
        foreach (var row in facing.Rows)
        {
            int mode = row.Decimal(0,0,5);
            if (row.UnsignedDecimal(1) != counts[mode]++) throw row.Invalid(1,"ordered native arrival facing entries");
            _facing.TryAdd((mode,row.HexByte(2)),row.Decimal(3,0,3));
        }
    }

    internal WarpTransitionHandler HandlerFor(int mode)
    {
        if ((uint)mode >= _handlers.Length)
            throw new NotSupportedException($"{AssetRoot} linkState0a: invalid native warp mode 0x{mode:x2}.");
        return _handlers[mode];
    }

    internal string HandlerSource(int mode)
    {
        _ = HandlerFor(mode);
        return _handlerSources[mode];
    }

    internal Vector2I FacingDirection(int activeCollisions, byte tile)
    {
        if ((uint)activeCollisions >= 6) throw new ArgumentOutOfRangeException(nameof(activeCollisions));
        return (_facing.TryGetValue((activeCollisions,tile),out int direction) ? direction : ObjectDirection.Down) switch
        {
            ObjectDirection.Up => Vector2I.Up,
            ObjectDirection.Right => Vector2I.Right,
            ObjectDirection.Down => Vector2I.Down,
            _ => Vector2I.Left
        };
    }

    public bool IsWarpTile(int activeCollisions, byte metatile) =>
        TryGetWarpTileParameter(activeCollisions,metatile,out _);

    internal bool TryGetWarpTileParameter(int activeCollisions, byte metatile, out byte parameter)
    {
        if ((uint)activeCollisions >= 6) throw new ArgumentOutOfRangeException(nameof(activeCollisions),
            $"{AssetRoot}: unsupported warpTileTable collision mode 0x{activeCollisions:x2}.");
        return _tiles.TryGetValue((activeCollisions,metatile),out parameter);
    }

    public bool TryGetTileWarp(int group, OracleRoomData room, int position, byte metatile,
        out Warp warp)
    {
        warp = default;
        return IsWarpTile(room.ActiveCollisions,metatile) && TryGetTileWarpSource(group,room.Id,position,out warp);
    }

    internal bool TryGetTileWarpSource(int group, int room, int position, out Warp warp)
    {
        warp = default;
        int cursor = Root(group), key = room;
        bool positioned = false;
        for (int visit = 0; visit <= _sources.Length * 2; visit++)
        {
            var source = Read(cursor++);
            // bank4 tests Ages' sentinel before the default bit. Defaults bypass
            // key matching; a pointer replaces the room key with packed Link YX.
            if (_hasEndMarker && source.Flags == 0xff) return false;
            if ((source.Flags & 0x80) != 0)
            {
                warp = CreateRequest(group,room,positioned ? source.Key : -1,0,source,true);
                return true;
            }
            if ((source.Flags & 0x40) != 0)
            {
                if (source.Key == key) { cursor = source.Pointer; key = position; positioned = true; }
            }
            else if ((source.Flags & 15) == 0 && source.Key == key)
            {
                warp = CreateRequest(group,room,positioned ? source.Key : -1,0,source,!positioned);
                return true;
            }
        }
        throw new InvalidOperationException($"{AssetRoot}: native warp source cycle for 0x{group:x1}:0x{room:x2} at 0x{position:x2}.");
    }

    public bool TryGetEdgeWarp(int group, int room, Vector2I direction, Vector2 position,
        out Warp warp)
    {
        warp = default;
        if (direction != Vector2I.Up && direction != Vector2I.Down) return false;
        int split = group < _smallGroups ? _smallEdgeSplit : _largeEdgeSplit;
        int x = unchecked((byte)Mathf.FloorToInt(position.X));
        int quadrant = direction == Vector2I.Up ? x < split ? 1 : 2 : x < split ? 4 : 8;
        int cursor = Root(group);
        for (int visit = 0; visit <= _sources.Length; visit++)
        {
            var source = Read(cursor++);
            // findScreenEdgeWarpSource stops at bit 7, including a default row.
            if ((source.Flags & 0x80) != 0) return false;
            if ((source.Flags & 0x40) != 0)
            {
                if (source.Key == room) cursor = source.Pointer;
            }
            else if (source.Key == room && (source.Flags & quadrant) != 0)
            {
                warp = CreateRequest(group,room,-1,source.Flags & 15,source,false) with
                { SourceParameter = direction == Vector2I.Down ? 4 : 0 };
                return true;
            }
        }
        throw new InvalidOperationException($"{AssetRoot}: native edge-warp source cycle for 0x{group:x1}:0x{room:x2}.");
    }

    private int Root(int group) => (uint)group < 8 ? _sourceRoots[group] :
        throw new ArgumentOutOfRangeException(nameof(group),$"{AssetRoot}: native warp group 0x{group:x2} is unavailable.");

    private WarpSourceRecord Read(int cursor) => (uint)cursor < _sources.Length ? _sources[cursor] :
        throw new InvalidOperationException($"{AssetRoot}: native warp stream reached invalid row 0x{cursor:x4}.");

    private static Warp CreateRequest(int group, int room, int position, int edge, WarpSourceRecord source, bool fallback)
    {
        int targetGroup = source.High >> 4;
        if (targetGroup >= 8) throw new NotSupportedException($"{source.Source}: native warp destination group 0x{targetGroup:x2} is unavailable.");
        return new(group,room,position,edge,source.High & 15,targetGroup,source.Low,0,0,0,
            SourceFallback:fallback,DestinationIsIndex:true,NativeSource:source.Source);
    }

    // findWarpSourceAndDest installs an index; applyWarpDest_b04 consumes it
    // later, including the saved Maku-growth byte at that actual load boundary.
    internal Warp ResolveDestination(Warp request, OracleRuntimeState memory)
    {
        if (!request.DestinationIsIndex) return request;
        if (memory.Game != OracleAssetPack.GameFor(AssetRoot))
            throw new ArgumentException($"{AssetRoot}: native destination resolution requires the selected live state.");
        int targetGroup = request.DestinationGroup;
        if ((uint)targetGroup >= 8) throw new NotSupportedException($"{request.NativeSource}: unavailable native destination group 0x{targetGroup:x2}.");
        int index = request.DestinationRoom;
        if (targetGroup == _biasGroup)
        {
            index = unchecked((byte)(index + memory.ReadWramByte(_biasAddress)));
        }
        int at = _destinationRoots[targetGroup] + index;
        if ((uint)at >= _destinations.Length) throw new NotSupportedException(
            $"{request.NativeSource}: destination 0x{targetGroup:x1}:0x{index:x2} exceeds the native imported stream.");
        var target = _destinations[at];
        return request with { DestinationRoom = target.Room, DestinationPosition = target.Position,
            DestinationParameter = target.Parameter, DestinationTransition = target.Transition, DestinationIsIndex = false };
    }
}

internal readonly record struct WarpSourceRecord(int Flags, int Key, int Low, int High, int Pointer, string Source);
internal readonly record struct WarpDestinationRecord(int Room, int Position, int Parameter, int Transition);

// DirectFadeOut is a direct wWarpTransition2=$03 write, bypassing Link's source handler and cue.
public readonly record struct Warp(int SourceGroup, int SourceRoom, int SourcePosition, int EdgeMask,
    int SourceTransition, int DestinationGroup, int DestinationRoom, int DestinationPosition,
    int DestinationParameter, int DestinationTransition, bool SourceFallback = false,
    bool DirectFadeOut = false, int SourceParameter = 0, int NativeFadeDelay = 1,
    bool PreserveDestinationSeason = false, // Native direct wWarpDestGroup bit $40; Ages ignores it.
    bool DestinationIsIndex = false, string? NativeSource = null); // wWarpDestRoom remains an index until applyWarpDest_b04.
