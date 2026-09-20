using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MISCELLANEOUS_2 $dc:$0b; native four-state floor mini-script.</summary>
internal sealed class CollapsingFloorRoomEntity : RoomEntityAdapter<Node2D>, IFixedRoomEntity,
    IRoomEntityLifetime, IPlayerRestriction, IScreenTransitionPreloadRoomEntity
{
    private readonly CollapsingFloorRecord _record;
    private readonly OracleRoomData _room;
    private readonly Action<int> _sound;
    private readonly Action _tileChanged;
    private readonly Func<long> _animationTick;
    private readonly Func<bool> _freeSlot;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int ScriptIndex { get; private set; }
    public bool Finished { get; private set; }
    public bool FreezesPlayerUpdates => State == 2;
    public bool DisablesSword => FreezesPlayerUpdates;
    public bool DisablesItems => FreezesPlayerUpdates;
    public bool DisablesMovement => FreezesPlayerUpdates;

    internal CollapsingFloorRoomEntity(CollapsingFloorRecord record, OracleRoomData room,
        Action<int> sound, Action tileChanged, Func<long> animationTick, Func<bool> freeSlot)
        : base(new Node2D { Name = "CollapsingFloor_dc_0b", Position = new(record.X, record.Y), Visible = false }, static _ => { })
    {
        _record = record; _room = room; _sound = sound; _tileChanged = tileChanged;
        _animationTick = animationTick; _freeSlot = freeSlot;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        State = 1;
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0) { State = 1; return; }
        if (State == 1)
        {
            // ignoreZ deliberately accepts a normal feather jump. The shared
            // collision gate still excludes menus, death, and special rides.
            var radius = Vector2.One * _record.Radius;
            if (!frame.Player.NativeInteractionCollisionsEnabled || !Player.EnemyCollisionOverlaps(
                    frame.Player.Position, new Rect2(Entity.Position - radius, radius * 2))) return;
            State = 2;
            _sound(_record.Clink);
            Entity.Position = frame.Player.Position;
            Counter = _record.Wait;
            if (_freeSlot())
            {
                spawns.Add(new ExclamationMarkSpawn(Entity.Position + new Vector2(8, -8), true));
                // The shared exclamation factory plays the second SND_CLINK.
            }
            return;
        }
        if (--Counter != 0) return;
        if (State == 2) { Counter = _record.Wait; State = 3; return; }
        Counter = _record.Interval;
        int packed = _record.Tiles[ScriptIndex++];
        if (packed == 0) { Finished = true; return; }
        Vector2 point = new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
        _room.ReplaceMetatile(point, _room.GetMetatile(point), (byte)_record.Tile, _animationTick());
        _tileChanged();
        _sound(_record.Rumble);
        if (_freeSlot()) spawns.Add(new FallingDownHoleSpawn(point, Silent: true));
    }
}
