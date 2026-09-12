using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>waterPushblock.s:interactionCode9e, including both flood directions.</summary>
internal sealed partial class WaterPushblockRoomEntity : NpcCharacter,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IPlayerRestriction, IRoomEntityUpdateFreeze
{
    private readonly WaterPushblockRecord _placement;
    private readonly WaterPushblockDatabase _data;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly Action<int> _playSound;
    private readonly Action _restoreMusic;
    private readonly Action _tileChanged;
    private readonly Func<long> _tick;
    private Vector2 _precisePosition;
    private bool _objectsDisabled;

    public Node2D Node => this;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public bool Finished { get; private set; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    private bool MovingWater => _objectsDisabled;
    public bool DisablesSword => MovingWater;
    public bool DisablesItems => MovingWater;
    public bool DisablesMovement => MovingWater;
    public bool DisablesMenus => MovingWater;
    public bool DisablesRingTransformations => MovingWater;
    public bool DisablesScreenTransitions => MovingWater;
    public bool DisablesWarpTiles => MovingWater;
    public bool DisablesCompanion => MovingWater;
    public bool FreezesPlayerUpdates => MovingWater;
    public bool FreezesRoomEntities => MovingWater;
    public bool FreezesInteractions => false; // wDisabledObjects=$81

    internal WaterPushblockRoomEntity(WaterPushblockRecord record, WaterPushblockDatabase data,
        OracleRoomData room, OracleSaveData save, Action<int> playSound, Action restoreMusic,
        Action tileChanged, Func<long> tick)
    {
        _placement = record;
        _data = data;
        _room = room;
        _save = save;
        _playSound = playSound;
        _restoreMusic = restoreMusic;
        _tileChanged = tileChanged;
        _tick = tick;
        Name = $"WaterPushblock_9e_{record.SubId:x2}";
        Initialize(new NpcRecord(record.Group, record.Room, 0x9e, record.SubId,
            record.Y, record.X, 0, 0, record.Sprite, record.TileBase, record.Palette, 0, false,
            record.Animation, record.Animation, record.Animation, record.Animation, string.Empty,
            NpcImplementationClassification.SpecializedNative));
        SetAnimationRate(0); // The native handler never calls interactionAnimate.
        SetFixedDrawPriority(BehindLinkZIndex); // objectSetVisible82
        SetScriptVisible(false);
        _precisePosition = Position;
    }

    private void InitializeState()
    {
        if (State != 0 || Finished) return;
        bool swapped = _save.HasRoomFlag(_placement.Group, _placement.Room, 0x01);
        if (swapped != (_placement.SubId == 1))
        {
            Finished = true;
            SetActive(false);
            return;
        }
        SetCollisionRadii(6, 6);
        SetScriptVisible(true);
        Counter = 30;
        State = 1;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        InitializeState();
        return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        Player player = frame.Player;
        switch (State)
        {
            case 0:
                InitializeState();
                return;
            case 1:
                // Do this at the interaction slot, after Link's attempted movement.
                // A tile-style pre-movement blocker would hide the source carry.
                bool collided = PreventPlayerPassing(player);
                Vector2 delta = player.Position - Position;
                if (!collided || player.ActiveTransformation != 0 || player.CompanionRideActive ||
                    player.MinecartRideActive || player.RaftRideActive || player.CutsceneControlled ||
                    player.IsDying || Input.GetVector("move_left", "move_right", "move_up", "move_down") == Vector2.Zero ||
                    Input.IsActionPressed("attack") || Input.IsActionPressed("item") ||
                    (Mathf.Abs(delta.X) > 4 && Mathf.Abs(delta.Y) > 4))
                {
                    Counter = 30;
                    return;
                }
                player.SetCutscenePushing(true); // wForceLinkPushAnimation=$01, for this update only.
                Counter = (Counter - 1) & 0xff;
                if (Counter != 0) return;
                int direction = Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y)
                    ? delta.X >= 0 ? 2 : 6 : delta.Y >= 0 ? 4 : 0;
                if (direction != (_placement.SubId == 0 ? 2 : 6)) return;
                Counter = 0x40;
                _objectsDisabled = true;
                _playSound(_data.Sound("SNDCTRL_STOPMUSIC"));
                _playSound(_data.Sound("SND_MOVEBLOCK"));
                State = 2;
                return;
            case 2:
                Position = OracleObjectMovement.Shared.ApplySpeed(ref _precisePosition,
                    0x14, _placement.SubId == 0 ? 0x18 : 0x08); // SPEED_80
                PreventPlayerPassing(player);
                if (--Counter != 0) return;
                Counter = 70;
                State = 3;
                return;
            case 3:
                if (--Counter != 0) return;
                UpdateFlood();
                return;
            case 4:
                PreventPlayerPassing(player);
                return;
            default:
                throw new InvalidOperationException($"{_placement.Source}: unsupported $9e state ${State:x2}.");
        }
    }

    private void UpdateFlood()
    {
        bool reverse = _placement.SubId == 1;
        int type = reverse ? 1 : 3;
        Counter = 8;
        // These are native instructions, not a script stream. Retain source
        // call order even for symmetric visual pairs ($61 then $67).
        switch (Substate)
        {
            case 0:
                _playSound(_data.Sound("SND_FLOODGATES"));
                Interleave(0x63, 0x1b, type); Interleave(0x65, 0x1b, type);
                break;
            case 1:
                Tile(reverse ? 0x65 : 0x63, 0x1b); Tile(reverse ? 0x63 : 0x65, 0xf9);
                break;
            case 2:
                Interleave(reverse ? 0x66 : 0x62, 0x1b, type);
                Interleave(reverse ? 0x62 : 0x66, 0x1b, type);
                break;
            case 3:
                Tile(reverse ? 0x66 : 0x62, 0x1b); Tile(reverse ? 0x62 : 0x66, 0xf9);
                break;
            case 4:
                Interleave(0x61, 0x1b, type); Interleave(0x67, 0x1b, type);
                break;
            case 5:
                Tile(reverse ? 0x67 : 0x61, 0x1b); Tile(reverse ? 0x61 : 0x67, 0xf9);
                break;
            case 6:
                Interleave(0x60, 0xf3, type); Interleave(0x68, 0x1b, type);
                break;
            case 7:
                if (reverse) { Tile(0x68, 0x1b); Tile(0x60, 0xfa); }
                else { Tile(0x60, 0xf3); Tile(0x68, 0xf9); }
                break;
            case 8:
                Interleave(0x69, 0xf3, type);
                break;
            case 9:
                Tile(0x69, reverse ? (byte)0xf3 : (byte)0xfa);
                Counter = 90;
                break;
            case 10:
                _playSound(_data.Sound("SNDCTRL_STOPSFX"));
                _playSound(_data.Sound("SND_SOLVEPUZZLE"));
                Counter = 0x48;
                break;
            case 11:
                _restoreMusic();
                _objectsDisabled = false;
                foreach (var flag in _data.RoomFlags)
                    _save.SetRoomFlag(flag.Group, flag.Room, flag.Mask,
                        !_save.HasRoomFlag(flag.Group, flag.Room, flag.Mask));
                State = 4;
                Counter = 0;
                return;
            default:
                throw new InvalidOperationException($"{_placement.Source}: unsupported flood substate ${Substate:x2}.");
        }
        Substate++;
        _tileChanged();
    }

    private static Vector2 Center(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
    private void Tile(int position, byte tile) =>
        _room.SetPositionTileAndCollision(Center(position), tile, null, _tick());
    private void Interleave(int position, byte tile, int type) =>
        _room.SetInterleavedMetatile(Center(position), tile, 0xf9, type, _tick());
}
