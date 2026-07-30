using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_TOGGLE_FLOOR $15:$00.</summary>
internal sealed partial class WingDungeonToggleFloor : Node2D,
    IRoomEntity, IFixedRoomEntity
{
    private readonly OracleRoomData _room;
    private readonly WingDungeonDatabase _data;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly List<PendingToggle> _pending = new();
    private int _lastTilePosition;
    private int _takeoffTilePosition;
    private bool _wasAirborne;

    public Node2D Node => this;
    internal int PendingCount => _pending.Count;

    internal WingDungeonToggleFloor(
        OracleRoomData room,
        WingDungeonDatabase data,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _room = room;
        _data = data;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Name = "WingDungeonToggleFloor";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int current = LinkTilePosition(frame.Player);
        bool airborne = frame.Player.TopDownAirborne;
        if (!_wasAirborne && airborne)
            _takeoffTilePosition = _lastTilePosition;

        if (airborne && IsCentered(frame.Player.Position) &&
            current != _lastTilePosition)
        {
            _lastTilePosition = current;
            int tile = TileAt(current);
            int first = _data.Constant("red-toggle-floor");
            if (tile >= first && tile < first + 3)
                _pending.Add(new PendingToggle(current, _takeoffTilePosition));
        }

        if (_wasAirborne && !airborne)
        {
            foreach (PendingToggle pending in _pending)
            {
                if (current == pending.TakeoffPosition)
                    continue;
                Cycle(pending.TilePosition);
            }
            _pending.Clear();
        }

        if (!airborne)
        {
            _lastTilePosition = current;
            _takeoffTilePosition = current;
        }
        _wasAirborne = airborne;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void Cycle(int packedPosition)
    {
        Vector2 point = PointFor(packedPosition);
        int first = _data.Constant("red-toggle-floor");
        int tile = _room.GetMetatile(point);
        if (tile < first || tile >= first + 3)
            return;
        byte replacement = (byte)(tile + 1);
        if (replacement == first + 3)
            replacement = (byte)first;
        _room.SetPositionTileAndCollision(
            point, replacement, null, _animationTick());
        _room.SetUnderlyingMetatile(point, replacement);
        _roomTileChanged();
        _playSound(OracleSoundEngine.SndGetSeed);
    }

    private byte TileAt(int packedPosition) =>
        _room.GetMetatile(PointFor(packedPosition));

    private static bool IsCentered(Vector2 linkPosition)
    {
        int y = (Mathf.FloorToInt(linkPosition.Y) + 5) & 0x0f;
        int x = Mathf.FloorToInt(linkPosition.X) & 0x0f;
        return y is >= 4 and <= 12 && x is >= 4 and <= 12;
    }

    private static int LinkTilePosition(Player player)
    {
        int y = (Mathf.FloorToInt(player.Position.Y) + 5) & 0xf0;
        int x = (Mathf.FloorToInt(player.Position.X) >> 4) & 0x0f;
        return y | x;
    }

    private static Vector2 PointFor(int packedPosition) => new(
        (packedPosition & 0x0f) * 16 + 8,
        (packedPosition >> 4) * 16 + 8);

    private readonly record struct PendingToggle(
        int TilePosition,
        int TakeoffPosition);
}
