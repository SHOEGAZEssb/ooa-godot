using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_LEVER_LAVA_FILLER $d8: ordered drying/refilling mini-script.</summary>
internal sealed partial class LeverLavaFillerRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRoomData _room;
    private readonly LeverLavaScript _script;
    private readonly LeverState _lever;
    private readonly OracleRandom _random;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private int _cursor;

    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Cursor => _cursor;

    internal LeverLavaFillerRoomEntity(OracleRoomData room, LeverLavaScript script, LeverState lever,
        OracleRandom random, Action<int> playSound, Action roomTileChanged, Func<long> animationTick)
    {
        _room = room;
        _script = script;
        _lever = lever;
        _random = random;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Name = "LeverLavaFiller";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (State)
        {
            case 0:
                State = 1;
                return;
            case 1:
                if ((_lever.PullDistance & 0x80) == 0) return;
                State = 2;
                Counter = 30;
                _playSound(SoundId.SndSolvePuzzle);
                _cursor = 0;
                ToggleLavaSource();
                return;
            case 2:
            case 4:
                Counter = (Counter - 1) & 0xff;
                if (Counter != 0) return;
                Counter = _script.Interval;
                if (_script.Bytes[_cursor] == 0)
                {
                    State = State == 2 ? 3 : 1;
                    return;
                }
                do
                {
                    int packed = _script.Bytes[_cursor++];
                    byte tile = State == 2 ? (byte)0x01 : (byte)(0x61 + (_random.Next().Value & 3));
                    // setTileInAllBuffers writes the underlying buffer first.
                    _room.SetUnderlyingStorageMetatile(packed, tile);
                    SetTile(packed, tile);
                } while (_script.Bytes[_cursor] != 0);
                _cursor++;
                _playSound(SoundId.SndRumble2);
                return;
            case 3:
                if (_lever.PullDistance != 0) return;
                State = 4;
                _cursor = 0;
                // Counter retains counter2 from the final empty drying group.
                ToggleLavaSource();
                _playSound(SoundId.SndDoorClose);
                return;
            default:
                throw new InvalidOperationException($"Invalid lava state ${State:x2} at {_script.Source}.");
        }
    }

    private void ToggleLavaSource()
    {
        int position = -1;
        int delta = 6;
        foreach (int tile in new[] { 0xc3, 0xc6, 0xc9, 0xcc })
        {
            delta = tile < 0xc9 ? 6 : -6;
            for (int packed = _room.Layout.Length - 1; packed >= 0; packed--)
                if (_room.Layout[packed] == tile) { position = packed; break; }
            if (position >= 0) break;
        }
        if (position < 0)
            throw new InvalidOperationException($"No lava-source tile for {_script.Source} in room {_room.Id:x2}.");
        do
        {
            SetTile(position, unchecked((byte)(_room.Layout[position] + delta)));
            position++;
        } while (position < _room.Layout.Length && _room.Layout[position] is >= 0xc3 and < 0xcf);
    }

    private void SetTile(int packed, byte tile)
    {
        _room.SetPositionTileAndCollision(new Vector2((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8),
            tile, null, _animationTick());
        _roomTileChanged();
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
