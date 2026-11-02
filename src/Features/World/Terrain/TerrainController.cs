
namespace Oracles;

public sealed class TerrainController
{
    private string AssetRoot => _rooms.AssetRoot;

    private const int MaximumLandingScanSteps = 32;

    private readonly RoomSession _rooms;
    private readonly BreakableTileDatabase _breakables;
    private readonly LedgeJumpDatabase _ledges;
    private SideScrollPlayerDatabase? _sideScrollSource;
    private SideScrollPlayerDatabase _sideScroll => _sideScrollSource ??= SideScrollPlayerDatabase.For(AssetRoot);
    private TreeStumpJumpDatabase? _stumpSource;
    private TreeStumpJumpDatabase Stumps => _stumpSource ??= new(AssetRoot);
    private readonly Func<Vector2, int> _adjacentWallsBitset;
    private bool OnStump => _rooms.SaveData.Game == OracleGame.Seasons &&
        _rooms.RuntimeState.ReadField("wActiveTileType") == (byte)TerrainType.Stump;
    internal byte CurrentTilesetFlags => _rooms.CurrentRoom.TilesetFlags;
    internal SideScrollPlayerParameters SideScrollParameters =>
        _sideScroll.Parameters;

    public TerrainController(
        RoomSession rooms,
        BreakableTileDatabase breakables,
        Func<Vector2, int> adjacentWallsBitset)
    {
        _rooms = rooms;
        _breakables = breakables;
        _ledges = new LedgeJumpDatabase(AssetRoot);
        _adjacentWallsBitset = adjacentWallsBitset;
    }

    public TerrainInfo GetTerrainInfo(Vector2 playerPosition)
    {
        return GetActiveTerrain(playerPosition).Terrain;
    }

    public ActiveTerrainInfo GetActiveTerrain(Vector2 playerPosition)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 sample = new(unchecked((byte)Mathf.FloorToInt(playerPosition.X)),
            unchecked((byte)(Mathf.FloorToInt(playerPosition.Y) + 5)));
        int tileX = Mathf.FloorToInt(sample.X / OracleRoomData.MetatileSize);
        int tileY = Mathf.FloorToInt(sample.Y / OracleRoomData.MetatileSize);
        Vector2 center = new(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8);
        byte packed = (byte)((tileY << 4) | tileX);
        byte tile = room.ReadNativeLayoutByte(packed,_rooms.RuntimeState);
        var types = TilePropertyDatabase.For(AssetRoot);
        var terrain = room.GetTerrainInfo(sample) with
        {
            Tile = tile,Type = types.Type(room.ActiveCollisions,tile),Hazard = types.Hazard(room.ActiveCollisions,tile),
            NativeTileType = types.NativeTileType(room.ActiveCollisions,tile),
            NativeHazardFlags = types.NativeHazardFlags(room.ActiveCollisions,tile)
        };
        return new ActiveTerrainInfo(terrain,sample,center,packed);
    }

    internal SideScrollTerrainState GetSideScrollTerrain(Vector2 playerPosition)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 sample = new(unchecked((byte)Mathf.FloorToInt(playerPosition.X)),
            unchecked((byte)Mathf.FloorToInt(playerPosition.Y)));
        byte activeTile = room.ReadNativeLayoutByte((byte)room.GetPackedPosition(sample),_rooms.RuntimeState);
        sample.Y = unchecked((byte)((int)sample.Y + _sideScroll.Parameters.BelowTileOffset));
        byte belowTile = room.ReadNativeLayoutByte((byte)room.GetPackedPosition(sample),_rooms.RuntimeState);
        return new SideScrollTerrainState(
            activeTile,
            belowTile,
            _sideScroll.TileType(activeTile),
            _sideScroll.TileType(belowTile));
    }

    internal int GetAdjacentWallsBitset(Vector2 position) => OnStump ? 0xff : _adjacentWallsBitset(position);

    public bool TryStartTerrainJump(Player player, Vector2 from, Vector2 attemptedMovement, int objectAngle)
    {
        if (!player.CanStartTerrainJump || !TryGetCardinalDirection(attemptedMovement, out Vector2I direction))
        {
            return false;
        }

        OracleRoomData room = _rooms.CurrentRoom;
        LedgeJumpDirectionRecord directionRecord = _ledges.Direction(direction);
        bool seasons = _rooms.SaveData.Game == OracleGame.Seasons;
        bool stump = OnStump;
        if (seasons && TryStartStumpJump(player,from,direction,stump)) return true;
        if (stump || objectAngle != directionRecord.Angle) return false;
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

        // ROM_SEASONS' indoor $05/$06 keep state01 and use its unblocked air path.
        if (seasons && room.GetMetatile(highPosition + directionRecord.Probe2) is 0x05 or 0x06)
        {
            player.BeginTerrainAirJump(directionRecord.Angle,ObjectSpeed.Speed140,null,_ledges.InitialSpeedZ,null,false);
            return true;
        }

        LedgeLandingScan landing = ScanForLanding(room, highPosition, direction);
        int speedRaw = landing.CrossesScreen
            ? ObjectSpeed.Speed0
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

    private bool TryStartStumpJump(Player player, Vector2 from, Vector2I direction, bool leaving)
    {
        if (!leaving && _rooms.ActiveGroup != 0) return false;
        int index = leaving ? CarriedObjectMotion.DirectionIndex(direction) : CarriedObjectMotion.DirectionIndex(player.FacingVector);
        var row = Stumps.Direction(leaving,index);
        Vector2 probe = new(unchecked((byte)(Mathf.FloorToInt(from.X) + row.Probe.X)),
            unchecked((byte)(Mathf.FloorToInt(from.Y) + row.Probe.Y)));
        Vector2? target = null;
        if (leaving)
        {
            if (probe.X >= _rooms.CurrentRoom.Width || probe.Y >= _rooms.CurrentRoom.Height || _rooms.CurrentRoom.IsSolid(probe)) return false;
            if (player.CanTurnForTerrainJump) player.Face(direction);
        }
        else
        {
            if (direction != player.FacingVector ||
                (_adjacentWallsBitset(from) & _ledges.Direction(player.FacingVector).WallMask) != _ledges.Direction(player.FacingVector).WallMask ||
                _rooms.CurrentRoom.GetMetatile(probe) != 0x20) return false;
            target = new Vector2(((int)probe.X & 0xf0) + Stumps.TargetOffset.X,((int)probe.Y & 0xf0) + Stumps.TargetOffset.Y);
        }
        player.BeginTerrainAirJump(index * 8,row.SpeedRaw,row.SpeedRaw,row.SpeedZ,target,!leaving);
        return true;
    }

    internal void ApplyLedgeLandingPlacement(Player player)
    {
        if (_rooms.SaveData.Game != OracleGame.Seasons || _rooms.ActiveGroup != 0) return;
        Vector2 position = player.PrecisePosition;
        if (_rooms.CurrentRoom.GetMetatile(new(position.X,unchecked((byte)(Mathf.FloorToInt(position.Y) + 5)))) != 0x20) return;
        player.SetScriptedPosition(new Vector2((Mathf.FloorToInt(position.X) & 0xf0) + Stumps.TargetOffset.X,
            (Mathf.FloorToInt(position.Y) & 0xf0) + Stumps.TargetOffset.Y));
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
