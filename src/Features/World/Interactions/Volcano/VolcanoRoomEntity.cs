using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class VolcanoRoomEntity : RoomEntityAdapter<Node2D>, IFixedRoomEntity,
    IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity
{
    private readonly VolcanoPlacement _record;
    private readonly VolcanoDatabase _data;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData? _save;
    private readonly OracleRandom _random;
    private readonly Action<int> _sound;
    private readonly Action<int, int, int> _shake;
    private readonly Func<bool> _shaking;
    private readonly Action<int> _magnitude;
    private readonly Func<bool> _freePartSlot;
    private int _mask;
    private int _base;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int ScriptIndex { get; private set; }
    public bool Finished { get; private set; }

    internal VolcanoRoomEntity(VolcanoPlacement record, VolcanoDatabase data, OracleRoomData room,
        OracleSaveData? save, OracleRandom random, Action<int> sound,
        Action<int, int, int> shake, Func<bool> shaking, Action<int> magnitude, Func<bool> freePartSlot)
        : base(new Node2D { Name = $"Volcano_{record.SubId:x2}_{record.Order}", Visible = false }, static _ => { })
    {
        _record = record; _data = data; _room = room; _save = save;
        _random = random; _sound = sound; _shake = shake; _shaking = shaking;
        _magnitude = magnitude;
        _freePartSlot = freePartSlot;
        Entity.Position = new(record.X, record.Y);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // $05/$13 explicitly wait for SCROLLMODE_01. $06 and its child
        // can resolve their invisible state 0 during destination preload.
        if (_record.SubId is 0x06 or 0xb2) Initialize(0, spawns);
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0) { Initialize(frame.Counter, spawns); return; }
        if (_record.SubId == 0x05)
        {
            if (State == 2 && (frame.Counter & 15) == 0) _sound(_data.Constant("rumble"));
            _shake(State == 2 ? 8 : 0, State == 2 ? 8 : 0, 1);
            if ((frame.Counter & 1) != 0) return;
            Counter = (Counter - 1) & 0xff;
            if (Counter != 0) return;
            State = State == 1 ? 2 : 1;
            // @state2 falls through into @setRandomShakeDuration after
            // selecting @state1. Both transitions draw a fresh $20..$9f
            // half-rate duration; the quiet interval never starts at zero.
            SetAmbientCounter();
            return;
        }
        if ((frame.Counter & 15) == 0) _sound(_data.Constant("rumble"));
        if (!_shaking())
        {
            VolcanoStep step = _data.Steps[ScriptIndex];
            ScriptIndex = (ScriptIndex + 1) % _data.Steps.Count;
            _shake(step.Y, step.X, 1);
            _mask = step.Mask; _base = step.Base;
            SetRockCounter();
        }
        Counter = (Counter - 1) & 0xff;
        if (Counter != 0) return;
        SetRockCounter();
        int xOffset = (_random.Next().Value & 15) - 8;
        // getFreePartSlot scans $d0..$df after both RNG calls, even when full.
        if (!_freePartSlot()) return;
        // Interactions follow the part pass: the new rock starts next update.
        spawns.Add(new VolcanoRockSpawn(Entity.Position + new Vector2(xOffset, 0)));
    }

    private void Initialize(int frameCounter, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished || State != 0) return;
        if (_record.SubId == 0x13)
        {
            // Direct wRoomLayout writes leave the already rendered waterfall intact.
            foreach (int position in new[] { 0x14, 0x15, 0x24, 0x25, 0x34, 0x35 })
                _room.SetPositionTileAndCollision(new((position & 15) * 16 + 8, (position >> 4) * 16 + 8),
                    (byte)_data.Constant("lava-tile"), null, 0, preserveRenderedTile: true);
            Finished = true;
            return;
        }
        if (_record.SubId != 0xb2 && _save?.HasGlobalFlag(_data.Constant("restored-flag")) == true)
        { Finished = true; return; }
        State = 1;
        if (_record.SubId == 0x06)
        {
            spawns.Add(new VolcanoHandlerSpawn(_record with { SubId = 0xb2 }));
            Finished = true;
        }
        else if (_record.SubId == 0x05)
        {
            _sound(_data.Constant("stop-sfx"));
            _magnitude(1);
            SetAmbientCounter();
            State += frameCounter & 1;
        }
        else _magnitude(1);
        // $b2 state 0 sets magnitude only; it consumes no RNG or script byte.
    }

    private void SetAmbientCounter() => Counter = (_random.Next().Value & 0x7f) + 0x20;
    private void SetRockCounter() => Counter = ((_random.Next().Value & _mask) + _base) & 0xff;
}

internal sealed record VolcanoHandlerSpawn(VolcanoPlacement Placement) : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record VolcanoRockSpawn(Vector2 Position) : RoomEntitySpawn;
