using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Bracelet-liftable INTERAC_TOKAY_MEAT `$8c used by Wild Tokay.</summary>
internal partial class WildTokayMeat : TransitionOffsetNode2D
{
    private readonly List<WildTokayMeatFrame> _frames = new();
    private BraceletDatabaseRecord _bracelet;
    private BombRecord _throwing = null!;
    private OracleRoomData _room = null!;
    private Action<int> _soundRequested = null!;
    private CarriedObjectMotion _carried;
    private int _fallDelay;
    private int _fallGravity;
    private int _fallSound;
    private int _landingSound;
    private int _dropLife;
    private bool _lifted;
    private bool _dropped;
    private bool _fallStarted;
    private Player? _holder;

    internal bool Lifted => _lifted;
    internal bool Thrown { get; private set; }
    internal bool Finished { get; private set; }
    internal int ZFixed => _carried.ZFixed;
    internal int SpeedZ => _carried.SpeedZ;
    internal int ThrowSpeedRaw => _carried.SpeedRaw;
    internal Vector2I ThrowDirection => _carried.Direction;
    internal int BounceCount { get; private set; }
    internal Rect2 CollisionBounds =>
        new(Position - new Vector2(8, 8), new Vector2(16, 16));

    internal void Initialize(
        WildTokayMeatDatabase database,
        OracleRoomData room,
        BombRecord throwing,
        Action<int> soundRequested)
    {
        _room = room;
        _throwing = throwing;
        _soundRequested = soundRequested;
        _bracelet = new BraceletDatabase().Data;
        Position = new Vector2(database.StartX, database.StartY);
        _carried = new CarriedObjectMotion(Position)
        {
            ZFixed = database.StartZ << 8
        };
        _fallDelay = database.FallDelay;
        _fallGravity = database.FallGravity;
        _fallSound = database.SoundFall;
        _landingSound = database.SoundLand;
        _dropLife = database.DropLife;
        Visible = false;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{database.Sprite}.png");
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(database.Animation);
        foreach (AnimationFrameDefinition frame in animation.Frames)
        {
            (Texture2D texture, Vector2 offset) =
                NpcCharacter.BuildPositionedOamTexture(
                    source, frame.EncodedOam, database.TileBase,
                    database.Palette, paletteOverride: null,
                    sourceGrayscaleInverted: true);
            _frames.Add(new WildTokayMeatFrame(texture, offset));
        }
        if (_frames.Count == 0)
            throw new InvalidOperationException("Wild Tokay meat has no animation frames.");
        QueueRedraw();
    }

    internal bool TryUseBracelet(Player player, Vector2I releaseDirection)
    {
        if (Finished)
            return false;
        if (_lifted)
        {
            Release(player, releaseDirection);
            return true;
        }
        if (Thrown || _dropped ||
            _carried.ZFixed != 0 || player.IsCarryingObject)
            return false;

        Vector2 point = OracleObjectMath.ToPixelPosition(player.Position) +
            player.FacingVector * NpcCharacter.AButtonPointOffset;
        if ((Position - point).LengthSquared() >= 14 * 14)
            return false;
        _lifted = true;
        _holder = player;
        player.BeginCarriedObjectPose();
        UpdateHeld(player);
        return true;
    }

    internal void UpdateFrame(Player player)
    {
        if (Finished)
            return;
        if (_lifted)
        {
            UpdateHeld(player);
            return;
        }
        if (_fallDelay > 0)
        {
            _fallDelay--;
            return;
        }
        if (!_fallStarted)
        {
            _fallStarted = true;
            Visible = true;
            _soundRequested(_fallSound);
            QueueRedraw();
            return;
        }
        if (_carried.ZFixed < 0 && !Thrown)
        {
            if (OracleObjectMath.UpdateSpeedZ(
                    ref _carried.ZFixed,
                    ref _carried.SpeedZ,
                    _fallGravity))
            {
                _carried.SpeedZ = 0;
                _soundRequested(_landingSound);
            }
            QueueRedraw();
            return;
        }
        if (Thrown)
        {
            UpdateThrown();
            return;
        }
        if (!_dropped)
            return;

        _dropLife--;
        Visible = (_dropLife & 1) != 0;
        if (_dropLife <= 0)
            Finish();
    }

    internal void Catch() => Finish();

    internal void Finish()
    {
        // IRoomEntityLifetime removes and queues this node as soon as Finished
        // becomes true. Event-owned lists may retain the managed reference
        // until the round's return-warp boundary, so repeated cleanup must not
        // touch native CanvasItem state after that removal.
        if (Finished)
            return;
        if (_holder is not null)
            _holder.EndCarriedObjectPose();
        _holder = null;
        _lifted = false;
        Thrown = false;
        Finished = true;
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished || _frames.Count == 0)
            return;
        WildTokayMeatFrame frame = _frames[0];
        DrawTexture(
            frame.Texture,
            frame.Offset + new Vector2(0, _carried.ZFixed >> 8) +
                TransitionDrawOffset);
    }

    private void UpdateHeld(Player player)
    {
        _carried.Hold(player);
        Position = OracleObjectMath.ToPixelPosition(_carried.GroundPosition);
        QueueRedraw();
    }

    private void Release(Player player, Vector2I releaseDirection)
    {
        _carried.Release(player, releaseDirection, _bracelet);
        Position = OracleObjectMath.ToPixelPosition(_carried.GroundPosition);
        BounceCount = 0;
        _lifted = false;
        _holder = null;
        Thrown = true;
        QueueRedraw();
    }

    private void UpdateThrown()
    {
        if (!WithinRoom(_carried.GroundPosition))
        {
            Finish();
            return;
        }

        _carried.AdvanceHorizontal(
            _throwing,
            front => !WithinRoom(front) ||
                (_room.IsSolid(front) &&
                 !_throwing.CanPassSolidTile(_room, front)));
        bool landed = _carried.AdvanceVertical(_bracelet);
        Position = OracleObjectMath.ToPixelPosition(_carried.GroundPosition);
        if (!landed)
        {
            QueueRedraw();
            return;
        }

        _soundRequested(_landingSound);
        if (_carried.Bounce(_throwing))
        {
            BounceCount++;
            QueueRedraw();
            return;
        }

        Thrown = false;
        _dropped = true;
        QueueRedraw();
    }

    private bool WithinRoom(Vector2 point) =>
        point.X >= 0 && point.X < _room.Width &&
        point.Y >= 0 && point.Y < _room.Height;

}

internal sealed record WildTokayMeatFrame(Texture2D Texture, Vector2 Offset);
