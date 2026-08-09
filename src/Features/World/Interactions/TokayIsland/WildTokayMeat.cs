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
    private Vector2 _groundPrecise;
    private Vector2I _throwDirection;
    private int _throwSpeedRaw;
    private int _fallDelay;
    private int _fallGravity;
    private int _fallSound;
    private int _landingSound;
    private int _dropLife;
    private int _zFixed;
    private int _speedZ;
    private bool _lifted;
    private bool _dropped;
    private bool _fallStarted;
    private Player? _holder;

    internal bool Lifted => _lifted;
    internal bool Thrown { get; private set; }
    internal bool Dropped => _dropped;
    internal bool Finished { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int ThrowSpeedRaw => _throwSpeedRaw;
    internal Vector2I ThrowDirection => _throwDirection;
    internal int BounceCount { get; private set; }
    internal Rect2 CollisionBounds =>
        new(Position - new Vector2(8, 8), new Vector2(16, 16));

    internal void Initialize(
        TokayIslandDatabase database,
        OracleRoomData room,
        BombRecord throwing,
        Action<int> soundRequested)
    {
        _room = room;
        _throwing = throwing;
        _soundRequested = soundRequested;
        _bracelet = new BraceletDatabase().Data;
        Position = new Vector2(database.MeatStartX, database.MeatStartY);
        _groundPrecise = Position;
        _zFixed = database.MeatStartZ << 8;
        _fallDelay = database.MeatFallDelay;
        _fallGravity = database.MeatFallGravity;
        _fallSound = database.Constant("sound-fall");
        _landingSound = database.Constant("sound-land");
        _dropLife = database.Constant("meat-drop-life");
        Visible = false;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{database.MeatSprite}.png");
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(database.MeatAnimation);
        foreach (AnimationFrameDefinition frame in animation.Frames)
        {
            (Texture2D texture, Vector2 offset) =
                NpcCharacter.BuildPositionedOamTexture(
                    source, frame.EncodedOam, database.MeatTileBase,
                    database.MeatPalette, paletteOverride: null,
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
        if (Thrown || _dropped || _zFixed != 0 || player.IsCarryingObject)
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
        if (_zFixed < 0 && !Thrown)
        {
            if (OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed, ref _speedZ, _fallGravity))
            {
                _speedZ = 0;
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
            frame.Offset + new Vector2(0, _zFixed >> 8) + TransitionDrawOffset);
    }

    private void UpdateHeld(Player player)
    {
        Vector2I offset = HeldOffset(player);
        _groundPrecise = player.Position + new Vector2(offset.X, 0);
        Position = OracleObjectMath.ToPixelPosition(_groundPrecise);
        _zFixed = offset.Y << 8;
        QueueRedraw();
    }

    private void Release(Player player, Vector2I releaseDirection)
    {
        Vector2I offset = HeldOffset(player);
        _groundPrecise =
            player.Position + new Vector2(offset.X, 0) + player.FacingVector;
        Position = OracleObjectMath.ToPixelPosition(_groundPrecise);
        _zFixed = offset.Y << 8;
        _speedZ = releaseDirection == Vector2I.Zero
            ? 0
            : _bracelet.InitialSpeedZ;
        _throwSpeedRaw = releaseDirection == Vector2I.Zero
            ? 0
            : RingEffects.UsesStrongThrow(player.Inventory)
                ? _bracelet.TossSpeedRaw
                : _bracelet.SpeedRaw;
        _throwDirection = releaseDirection;
        BounceCount = 0;
        _lifted = false;
        _holder = null;
        Thrown = true;
        player.EndCarriedObjectPose();
        QueueRedraw();
    }

    private void UpdateThrown()
    {
        if (!WithinRoom(_groundPrecise))
        {
            Finish();
            return;
        }

        Vector2 front = _groundPrecise + ThrowCollisionOffset(_throwDirection);
        if (_throwDirection != Vector2I.Zero &&
            (!WithinRoom(front) ||
             (_room.IsSolid(front) &&
              !_throwing.CanPassSolidTile(_room, front))))
        {
            _throwDirection = Vector2I.Zero;
            _throwSpeedRaw = 0;
        }
        else if (_throwDirection != Vector2I.Zero)
        {
            OracleObjectMovement.Shared.ApplySpeed(
                ref _groundPrecise,
                _throwSpeedRaw,
                DirectionAngle(_throwDirection));
        }

        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _bracelet.Gravity);
        Position = OracleObjectMath.ToPixelPosition(_groundPrecise);
        if (!landed)
        {
            QueueRedraw();
            return;
        }

        _soundRequested(_landingSound);
        int rebound = (-_speedZ) >> 1;
        if (rebound <= -0x80)
        {
            _speedZ = rebound;
            _throwSpeedRaw = _throwing.ReducedBounceSpeed(_throwSpeedRaw);
            if (_throwSpeedRaw == 0)
                _throwDirection = Vector2I.Zero;
            BounceCount++;
            QueueRedraw();
            return;
        }

        _zFixed = 0;
        _speedZ = 0;
        _throwDirection = Vector2I.Zero;
        _throwSpeedRaw = 0;
        Thrown = false;
        _dropped = true;
        QueueRedraw();
    }

    private static Vector2I HeldOffset(Player player)
    {
        int frame = player.CarriedObjectAnimationFrame == 0 ? 2 : 3;
        return player.BraceletEntityOffset ??
            LinkItemDatabase.Shared.BraceletLiftOffset(
                frame, DirectionIndex(player.FacingVector));
    }

    private bool WithinRoom(Vector2 point) =>
        point.X >= 0 && point.X < _room.Width &&
        point.Y >= 0 && point.Y < _room.Height;

    private static Vector2 ThrowCollisionOffset(Vector2I direction) =>
        direction == Vector2I.Up ? new Vector2(0, -3)
        : direction == Vector2I.Right ? new Vector2(3, 0)
        : direction == Vector2I.Down ? new Vector2(0, 7)
        : direction == Vector2I.Left ? new Vector2(-3, 0)
        : Vector2.Zero;

    private static int DirectionIndex(Vector2I direction) =>
        direction == Vector2I.Up ? 0
        : direction == Vector2I.Right ? 1
        : direction == Vector2I.Down ? 2
        : direction == Vector2I.Left ? 3
        : throw new ArgumentOutOfRangeException(nameof(direction));

    private static int DirectionAngle(Vector2I direction) =>
        direction == Vector2I.Up ? 0x00
        : direction == Vector2I.Right ? 0x08
        : direction == Vector2I.Down ? 0x10
        : direction == Vector2I.Left ? 0x18
        : throw new ArgumentOutOfRangeException(nameof(direction));
}

internal sealed record WildTokayMeatFrame(Texture2D Texture, Vector2 Offset);
