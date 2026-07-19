using Godot;
using System;

namespace oracleofages;

public sealed class TerrainController
{
    private readonly Node _worldRoot;
    private readonly RoomSession _rooms;
    private readonly Func<Vector2, bool> _collides;
    private readonly Action<int> _playSound;
    private SplashEffect? _activeSplash;

    internal SplashEffect? ActiveSplash => _activeSplash;

    public TerrainController(
        Node worldRoot,
        RoomSession rooms,
        Func<Vector2, bool> collides,
        Action<int> playSound)
    {
        _worldRoot = worldRoot;
        _rooms = rooms;
        _collides = collides;
        _playSound = playSound;
    }

    public OracleRoomData.TerrainInfo GetTerrainInfo(Vector2 playerPosition)
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
        OracleRoomData.TerrainType terrain = GetTerrainInfo(playerPosition).Type;
        const float pushSpeed = 32.0f;
        return terrain switch
        {
            OracleRoomData.TerrainType.UpCurrent or OracleRoomData.TerrainType.UpConveyor => new Vector2(0, -pushSpeed),
            OracleRoomData.TerrainType.RightCurrent or OracleRoomData.TerrainType.RightConveyor => new Vector2(pushSpeed, 0),
            OracleRoomData.TerrainType.DownCurrent or OracleRoomData.TerrainType.DownConveyor => new Vector2(0, pushSpeed),
            OracleRoomData.TerrainType.LeftCurrent or OracleRoomData.TerrainType.LeftConveyor => new Vector2(-pushSpeed, 0),
            _ => Vector2.Zero
        };
    }

    public bool TryStartLedgeHop(Player player, Vector2 from, Vector2 attemptedMovement)
    {
        Vector2I direction = Mathf.Abs(attemptedMovement.X) > Mathf.Abs(attemptedMovement.Y)
            ? (attemptedMovement.X > 0 ? Vector2I.Right : Vector2I.Left)
            : (attemptedMovement.Y > 0 ? Vector2I.Down : Vector2I.Up);
        if (player.FacingVector != direction)
            return false;

        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 ledgePoint = from + (Vector2)direction * 12.0f;
        if (!IsCliffTile(room.GetMetatile(ledgePoint), direction))
            return false;

        Vector2 landing = from + (Vector2)direction * (OracleRoomData.MetatileSize * 2);
        if (landing.X < 0 || landing.X >= room.Width ||
            landing.Y < 0 || landing.Y >= room.Height || _collides(landing))
            return false;

        player.StartLedgeHop(landing);
        return true;
    }

    public void SpawnSplash(Vector2 position, OracleRoomData.HazardType hazard)
    {
        if (hazard is not (OracleRoomData.HazardType.Water or OracleRoomData.HazardType.Lava))
            throw new ArgumentOutOfRangeException(nameof(hazard));
        _activeSplash = new SplashEffect { ZIndex = 11 };
        _activeSplash.Initialize(position, hazard);
        _worldRoot.AddChild(_activeSplash);
        _playSound(OracleSoundEngine.SndSplash);
    }

    private static bool IsCliffTile(byte tile, Vector2I direction)
    {
        if (direction == Vector2I.Down && tile is 0x05 or 0x06 or 0x07 or 0x64 or 0xff or 0xb0 or 0xc1)
            return true;
        if (direction == Vector2I.Left && tile is 0x0a or 0xb1 or 0xc2)
            return true;
        if (direction == Vector2I.Up && tile is 0xb2 or 0xc3)
            return true;
        return direction == Vector2I.Right && tile is 0x0b or 0xb3 or 0xc4;
    }
}
