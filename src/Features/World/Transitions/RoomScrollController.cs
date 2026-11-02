
namespace Oracles;

// bank1.s: screenTransitionState3-5 and transitionUpdateScrollAndLinkPosition.
internal sealed class RoomScrollController
{
    private Vector2 _start, _linkStep;
    private int _setupRemaining, _cleanupRemaining;
    private ScreenTransitionGraphicsDatabase? _graphics;
    private ScreenTransitionPaletteDatabase? _palettes;
    private ScreenTransitionRenderer? _renderer;
    private int _loadedUnique, _unique, _entries, _setupFrames, _postScrollEntries;
    private bool _uploadGraphics;
    internal Vector2I Direction { get; private set; }
    internal Vector2 FinishOffset { get; private set; }
    internal int Frame { get; private set; }
    internal int Frames { get; private set; }
    internal int TotalFrames { get; private set; }
    internal int Distance { get; private set; }
    internal bool Active { get; private set; }
    internal (byte State,byte Substate,byte Step) NativePhase
    {
        get
        {
            if (!Active) return (2,0,0);
            if (_setupRemaining == _setupFrames) return (3,0,0);
            if (_setupRemaining > 3) return (4,0,0);
            if (_setupRemaining == 3) return (5,0,0);
            byte substate = (byte)(Direction.X != 0 ? 2 : 1);
            byte step = (byte)(_setupRemaining > 0 ? 2 - _setupRemaining : Frame < Frames ? 2 :
                _postScrollEntries != 0 ? (_cleanupRemaining > _postScrollEntries ? 3 : 4) :
                _cleanupRemaining > 1 ? 3 : 5);
            return (5,substate,step);
        }
    }
    internal float Pixels => Frame * 4;
    internal Vector2 LinkPosition => _start + _linkStep * Frame;
    internal Vector2 DestinationPosition => _start + _linkStep * Frames + FinishOffset;

    internal void ResetGraphics(OracleRoomData room)
    {
        _graphics ??= new(room.AssetRoot);
        if (_graphics.AssetRoot != room.AssetRoot)
            throw new ArgumentException("A scroll owner cannot cross game asset packs.");
        _loadedUnique = _graphics.ForTileset(room.TilesetId, room.TilesetStateModifier).Unique |
            (room.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
    }

    internal void Begin(Vector2 start, Vector2I direction, OracleRoomData source, OracleRoomData target)
    {
        if (source.AssetRoot != target.AssetRoot || _graphics?.AssetRoot != source.AssetRoot)
            throw new ArgumentException("A native scroll requires graphics initialized from its source pack.");
        _setupFrames = 4;
        int cleanup = 2 + (direction.X != 0 ? (target.Width - 160) / 8 : (target.Height - 128) / 8);
        (_unique, _entries) = _graphics.ForTileset(target.TilesetId, target.TilesetStateModifier);
        if (target.LoadsUniqueGraphicsAfterScroll) _unique |= 0x80;
        _uploadGraphics = _unique != 0 && _unique != _loadedUnique;
        _postScrollEntries = 0;
        if (_unique != 0)
        {
            int updates = _unique == _loadedUnique ? 1 : _entries;
            if ((_unique & 0x80) == 0) _setupFrames += updates;
            else { cleanup += updates - 1; _postScrollEntries = updates; }
            _loadedUnique = _unique;
        }
        Begin(start, direction, source.Width, source.Height, _setupFrames, cleanup);
    }

    internal void BindGraphics(OracleRoomData source, OracleRoomData target, Image sourceGraphics,
        Color[,] sourceColors, OracleSaveData save, int stateModifier)
    {
        _palettes ??= new(target.AssetRoot);
        bool routePresent = _palettes.TryGet(target.Group, target.Id, Direction, save, stateModifier, out var route);
        bool smooth = save.Game == OracleGame.Seasons ? target.Group == 0 :
            (target.TilesetFlags & (int)TilesetFlags.Outdoors) != 0 && source.TilesetPaletteId != target.TilesetPaletteId;
        _renderer = new(source, target, sourceGraphics, sourceColors,
            _uploadGraphics ? _graphics!.Uploads(_unique) : Array.Empty<ScreenGraphicsUpload>(),
            target.LoadsUniqueGraphicsAfterScroll ? TotalFrames - _entries + 1 : 2,
            _setupFrames > 4 && !routePresent ? _setupFrames - 3 : -1,
            _setupFrames - 2, smooth && routePresent ? route : null);
    }

    internal void FinishGraphics()
    {
        _renderer?.Finish();
        _renderer = null;
    }
    internal void Cancel()
    {
        Active = false;
        _renderer = null;
    }

    internal void Begin(Vector2 start, Vector2I direction, int width, int height, int setupFrames, int cleanupFrames)
    {
        if (Math.Abs(direction.X) + Math.Abs(direction.Y) != 1 ||
            width < 160 || height < 128 || setupFrames < 0 || cleanupFrames < 1)
            throw new ArgumentException("A room scroll requires a cardinal direction, native room dimensions and valid phase counters.");
        Direction = direction; _start = start;
        Distance = direction.X != 0 ? OracleRoomData.ViewportWidth : OracleRoomData.ViewportHeight;
        Frames = Distance / 4; Frame = 0;
        _setupRemaining = setupFrames; _cleanupRemaining = cleanupFrames;
        TotalFrames = setupFrames + Frames + cleanupFrames;
        _linkStep = direction.X != 0 ? new Vector2(direction.X * 0.375f, 0) : new Vector2(0, direction.Y * 0.5f);
        FinishOffset = new(-direction.X * width, -direction.Y * height);
        Active = true;
    }

    internal void SetStart(Vector2 start) => _start = start;

    internal bool Advance()
    {
        if (!Active) return false;
        _renderer?.Advance();
        if (_setupRemaining > 0) { _setupRemaining--; return false; }
        if (Frame == Frames)
        {
            if (--_cleanupRemaining > 0) return false;
            Active = false;
        }
        else Frame++;
        return true;
    }
}
