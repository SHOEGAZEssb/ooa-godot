using Godot;
using System;

namespace oracleofages;

public sealed class TerrainController
{
    private const int MaximumLandingScanSteps = 32;

    private readonly Node _worldRoot;
    private readonly RoomSession _rooms;
    private readonly BreakableTileDatabase _breakables;
    private readonly LedgeJumpDatabase _ledges;
    private readonly Func<Vector2, int> _adjacentWallsBitset;
    private readonly Action<int> _playSound;
    private SplashEffect? _activeSplash;

    internal SplashEffect? ActiveSplash => _activeSplash;
    internal byte CurrentTilesetFlags => _rooms.CurrentRoom.TilesetFlags;

    public TerrainController(
        Node worldRoot,
        RoomSession rooms,
        BreakableTileDatabase breakables,
        Func<Vector2, int> adjacentWallsBitset,
        Action<int> playSound)
    {
        _worldRoot = worldRoot;
        _rooms = rooms;
        _breakables = breakables;
        _ledges = new LedgeJumpDatabase();
        _adjacentWallsBitset = adjacentWallsBitset;
        _playSound = playSound;
    }

    public TerrainInfo GetTerrainInfo(Vector2 playerPosition)
    {
        return _rooms.CurrentRoom.GetTerrainInfo(playerPosition + new Vector2(0, 5));
    }

    public ActiveTerrainInfo GetActiveTerrain(Vector2 playerPosition)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 sample = playerPosition + new Vector2(0, 5);
        int tileX = Mathf.FloorToInt(sample.X / OracleRoomData.MetatileSize);
        int tileY = Mathf.FloorToInt(sample.Y / OracleRoomData.MetatileSize);
        Vector2 center = new(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8);
        return new ActiveTerrainInfo(
            room.GetTerrainInfo(sample), sample, center, (tileY << 4) | tileX);
    }

    public Vector2 GetTerrainPush(Vector2 playerPosition)
    {
        TerrainType terrain = GetTerrainInfo(playerPosition).Type;
        const float pushSpeed = 32.0f;
        return terrain switch
        {
            TerrainType.UpCurrent or TerrainType.UpConveyor => new Vector2(0, -pushSpeed),
            TerrainType.RightCurrent or TerrainType.RightConveyor => new Vector2(pushSpeed, 0),
            TerrainType.DownCurrent or TerrainType.DownConveyor => new Vector2(0, pushSpeed),
            TerrainType.LeftCurrent or TerrainType.LeftConveyor => new Vector2(-pushSpeed, 0),
            _ => Vector2.Zero
        };
    }

    public bool TryStartLedgeHop(Player player, Vector2 from, Vector2 attemptedMovement)
    {
        if (!TryGetCardinalDirection(attemptedMovement, out Vector2I direction) ||
            player.FacingVector != direction)
        {
            return false;
        }

        OracleRoomData room = _rooms.CurrentRoom;
        LedgeJumpDirectionRecord directionRecord = _ledges.Direction(direction);
        int adjacentWalls = _adjacentWallsBitset(from);
        if ((adjacentWalls & directionRecord.WallMask) !=
            directionRecord.WallMask)
        {
            return false;
        }
        Vector2 highPosition = new(
            Mathf.Floor(from.X),
            Mathf.Floor(from.Y));
        if (!IsCliffProbe(
                room, highPosition + directionRecord.Probe1,
                directionRecord.Angle) ||
            !IsCliffProbe(
                room, highPosition + directionRecord.Probe2,
                directionRecord.Angle))
        {
            return false;
        }

        LedgeLandingScan landing = ScanForLanding(room, highPosition, direction);
        int speedRaw = landing.CrossesScreen
            ? 0
            : _ledges.SpeedRaw(landing.CliffLength);
        var plan = new LedgeJumpPlan(
            direction,
            directionRecord.Angle,
            landing.CliffLength,
            speedRaw,
            landing.CrossesScreen,
            room.Height - 7,
            landing.Position,
            _ledges.InitialSpeedZ,
            _ledges.TransitionSpeedZ,
            _ledges.Gravity,
            _ledges.JumpSound,
            _ledges.LandSound,
            _ledges.AnimationPhaseDurations);

        player.StartLedgeHop(plan);
        return true;
    }

    internal void ResumeLedgeHopAfterScroll(Player player)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        LedgeLandingScan landing = ScanForLanding(
            room,
            new Vector2(
                Mathf.Floor(player.PrecisePosition.X),
                Mathf.Floor(player.PrecisePosition.Y)),
            Vector2I.Down);
        if (landing.CrossesScreen)
        {
            throw new InvalidOperationException(
                $"LINK_STATE_JUMPING_DOWN_LEDGE found no destination landing " +
                $"after scrolling into room {room.Group:x1}:{room.Id:x2}.");
        }
        player.ResumeLedgeHopAfterScroll(
            landing.Position,
            landing.CliffLength);
    }

    public void SpawnSplash(Vector2 position, HazardType hazard)
    {
        if (hazard is not (HazardType.Water or HazardType.Lava))
            throw new ArgumentOutOfRangeException(nameof(hazard));
        _activeSplash = new SplashEffect { ZIndex = 11 };
        _activeSplash.Initialize(position, hazard);
        _worldRoot.AddChild(_activeSplash);
        _playSound(OracleSoundEngine.SndSplash);
    }

    private bool IsCliffProbe(
        OracleRoomData room,
        Vector2 point,
        int angle)
    {
        byte tile = room.GetMetatile(point);
        return _ledges.IsCliffTile(room.ActiveCollisions, tile, angle);
    }

    private LedgeLandingScan ScanForLanding(
        OracleRoomData room,
        Vector2 position,
        Vector2I direction)
    {
        Vector2 point = position + new Vector2(0, _ledges.FeetOffset);
        Vector2 offset = (Vector2)direction * _ledges.ScanStep;
        for (int length = 1; length <= MaximumLandingScanSteps; length++)
        {
            point += offset;
            if (point.X < 0 || point.X >= room.Width ||
                point.Y < 0 || point.Y >= room.Height)
            {
                return new LedgeLandingScan(
                    CliffLength: 0,
                    Position: point,
                    CrossesScreen: true);
            }

            byte tile = room.GetMetatile(point);
            if (!room.IsSolid(point))
            {
                // getTileAtPosition returns zero at a transition boundary.
                // An in-room tile $00 follows the same zero-flag branch.
                return new LedgeLandingScan(
                    length,
                    point,
                    CrossesScreen: tile == 0);
            }

            bool breakableLanding =
                _breakables.TryGet(
                    room.ActiveCollisions,
                    tile,
                    out BreakableTileRecord breakable) &&
                breakable.AllowsSource(BreakableTileDatabase.SourceLanded);
            if (breakableLanding ||
                _ledges.IsLandableSolidTile(room.ActiveCollisions, tile))
            {
                return new LedgeLandingScan(
                    length,
                    point,
                    CrossesScreen: false);
            }
        }

        throw new InvalidOperationException(
            $"LINK_STATE_JUMPING_DOWN_LEDGE landing scan cycled without a " +
            $"landing or boundary in room {room.Group:x1}:{room.Id:x2} from " +
            $"({position.X:0.##},{position.Y:0.##}) toward angle " +
            $"${_ledges.Direction(direction).Angle:x2}.");
    }

    private static bool TryGetCardinalDirection(
        Vector2 movement,
        out Vector2I direction)
    {
        bool horizontal = !Mathf.IsZeroApprox(movement.X);
        bool vertical = !Mathf.IsZeroApprox(movement.Y);
        if (horizontal == vertical)
        {
            direction = Vector2I.Zero;
            return false;
        }

        direction = horizontal
            ? (movement.X > 0 ? Vector2I.Right : Vector2I.Left)
            : (movement.Y > 0 ? Vector2I.Down : Vector2I.Up);
        return true;
    }
}

public readonly record struct ActiveTerrainInfo(
    TerrainInfo Terrain,
    Vector2 SamplePoint,
    Vector2 TileCenter,
    int PackedPosition);

internal readonly record struct LedgeJumpPlan(
    Vector2I Direction,
    int Angle,
    int CliffLength,
    int SpeedRaw,
    bool CrossesScreen,
    int ScreenBoundaryY,
    Vector2 LandingPosition,
    int InitialSpeedZ,
    int TransitionSpeedZ,
    int Gravity,
    int JumpSound,
    int LandSound,
    int[] AnimationPhaseDurations);

internal readonly record struct LedgeLandingScan(
    int CliffLength,
    Vector2 Position,
    bool CrossesScreen);
