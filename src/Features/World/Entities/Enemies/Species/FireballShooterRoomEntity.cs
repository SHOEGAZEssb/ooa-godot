using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Invisible ENEMY_FIREBALL_SHOOTER $50 scanner and its native children.</summary>
internal sealed partial class FireballShooterRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity,
    IScreenTransitionPreloadRoomEntity, INativeEnemyCounter1RoomEntity
{
    private readonly FireballShooterBehaviorProfile _data = EnemyBehaviorTables.Shared.FireballShooter;
    private readonly OracleRoomData _room;
    private readonly OracleRandom _random;
    private readonly Func<int, bool> _enemySlotsAvailable;
    private readonly Func<bool> _partSlotAvailable;
    private readonly Func<int> _roomEnemyCount;
    private readonly int _timingIndex;
    internal int SubId { get; }
    internal int State { get; private set; }
    public int Counter1 { get; set; }
    public bool RetainsCounter1AfterDeletion { get; private set; }
    public bool Finished { get; private set; }
    public bool CountsAsEnemy => false;
    public Node2D Node => this;

    internal FireballShooterRoomEntity(OracleRoomData room, Vector2 position, int subid,
        int timingIndex, OracleRandom random, Func<int, bool> enemySlotsAvailable,
        Func<bool> partSlotAvailable, Func<int> roomEnemyCount)
    {
        _room = room; Position = position; SubId = subid; _timingIndex = timingIndex;
        _random = random; _enemySlotsAvailable = enemySlotsAvailable;
        _partSlotAvailable = partSlotAvailable; _roomEnemyCount = roomEnemyCount;
        Visible = false;
        Name = $"FireballShooter_50_{subid:x2}_{timingIndex}";
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        InitializeState();
        return ScreenTransitionPresentation.Hidden;
    }

    private void InitializeState()
    {
        if (State != 0) return;
        _random.Next(); // enemyStandardUpdate.var3d, even for invisible scanners.
        State = (SubId & 0x80) == 0 ? 1 : 8;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        switch (State)
        {
            case 0: InitializeState(); return;
            case 1:
                int count = 0;
                for (int packed = 0; packed < 0xb0; packed++)
                {
                    int x = packed & 15, y = packed >> 4;
                    // loadRoomLayout clears its padded buffer before loading.
                    int tile = _room.Layout.Length == 176 ? _room.Layout[packed] :
                        x < _room.WidthInTiles && y < _room.HeightInTiles ? _room.Layout[y * _room.WidthInTiles + x] : 0;
                    if (tile != (int)Position.Y) continue;
                    if (!_enemySlotsAvailable(count + 1)) break;
                    count++;
                    spawns.Add(new FireballShooterChildSpawn(this,
                        new Vector2(x * 16 + _data.TileXOffset, y * 16 + _data.TileYOffset), count & 3));
                }
                Delete();
                return;
            case 8:
                State = 9;
                Counter1 = _data.TimingOffsets[_timingIndex].Value;
                return;
            case 9:
                if (SubId == _data.RoomClearSubId && _roomEnemyCount() == 0)
                {
                    // Clean US returns to the caller AFTER enemyDelete has
                    // cleared XY/counter1. The distance check now uses zero.
                    Delete();
                    RetainsCounter1AfterDeletion = true;
                    if (!LinkIsNear(frame.Player.Position)) Counter1 = 0xff;
                    return;
                }
                if (LinkIsNear(frame.Player.Position)) return;
                Counter1 = (Counter1 - 1) & 0xff;
                if (Counter1 != 0) return;
                if (_partSlotAvailable()) spawns.Add(new ZoraFireSpawn(Position, 0x31));
                Counter1 = _data.CooldownBase + (_random.Next().Value & _data.CooldownMask);
                return;
            default: throw new InvalidOperationException($"ENEMY_FIREBALL_SHOOTER $50:${SubId:x2}: state ${State:x2}.");
        }
    }

    private bool LinkIsNear(Vector2 link) =>
        Math.Abs(OracleObjectPosition.HighByte(link.X) - OracleObjectPosition.HighByte(Position.X)) +
        Math.Abs(OracleObjectPosition.HighByte(link.Y) - OracleObjectPosition.HighByte(Position.Y)) < _data.LinkDistance;

    private void Delete() { Finished = true; Position = Vector2.Zero; State = 0; Counter1 = 0; }

    internal FireballShooterRoomEntity CreateChild(Vector2 position, int timingIndex) =>
        new(_room, position, SubId | 0x80, timingIndex, _random,
            _enemySlotsAvailable, _partSlotAvailable, _roomEnemyCount);
}

internal sealed record FireballShooterChildSpawn(FireballShooterRoomEntity Parent, Vector2 Position, int TimingIndex) : RoomEntitySpawn;
