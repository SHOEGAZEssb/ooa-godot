using Godot;
using System;

namespace oracleofages;

public partial class OctorokRockProjectile : Node2D
{
    internal enum RockState { Initializing, Flying, CollisionPending, Bouncing }

    private const int BounceSpeedZ = -0xe0;
    private const int BounceGravity = 0x0e;
    private const int BounceFrames = 0x20;

    private EnemyDatabase.OctorokProjectileRecord _record;
    private OracleRoomData _room = null!;
    private Texture2D _normalTexture = null!;
    private Texture2D _bounceTexture = null!;
    private RockState _state;
    private int _angle;
    private int _counter;
    private int _zFixed;
    private int _speedZ;
    private Vector2 _transitionDrawOffset;

    public bool Finished { get; private set; }
    internal RockState State => _state;
    internal int Angle => _angle;
    internal int Counter => _counter;
    internal int ZFixed => _zFixed;
    internal int ElapsedFrames { get; private set; }
    internal Vector2 TransitionDrawOffset => _transitionDrawOffset;
    public Rect2 CollisionBounds => new(
        Position - new Vector2(_record.CollisionRadiusX, _record.CollisionRadiusY),
        new Vector2(_record.CollisionRadiusX * 2, _record.CollisionRadiusY * 2));

    internal void Initialize(
        EnemyDatabase.OctorokProjectileRecord record,
        OracleRoomData room,
        Vector2 position,
        int angle)
    {
        _record = record;
        _room = room;
        Position = position;
        _angle = angle & 0x18;
        byte[] bytes = FileAccess.GetFileAsBytes(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        Image source = new();
        Error error = source.LoadPngFromBuffer(bytes);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not load Octorok rock graphics: {error}.");
        _normalTexture = BuildFirstFrame(
            source, record.NormalAnimation, record.TileBase, record.Palette);
        _bounceTexture = BuildFirstFrame(
            source, record.BounceAnimation, record.TileBase, record.Palette);
        QueueRedraw();
    }

    internal void UpdateFrame(Player player)
    {
        if (Finished)
            return;
        ElapsedFrames++;

        if (_state == RockState.Initializing)
        {
            _state = RockState.Flying;
            return;
        }

        if (_state == RockState.CollisionPending)
        {
            BeginBounce();
            return;
        }
        if (_state == RockState.Bouncing)
        {
            UpdateBounce();
            return;
        }

        if (OverlapsLink(player.Position))
        {
            player.ApplyEnemyContactDamage(Position, _record.DamageQuarters);
            Finish();
            return;
        }
        if (!WithinVisibleBoundary(player.Position))
        {
            Finish();
            return;
        }

        Vector2 destination = Position + DirectionForAngle(_angle) * (_record.SpeedRaw / 40.0f);
        if (destination.X < 0 || destination.X >= _room.Width ||
            destination.Y < 0 || destination.Y >= _room.Height)
        {
            Finish();
            return;
        }
        if (_room.IsSolid(destination))
        {
            // State 1 still calls objectApplySpeed after selecting state 2.
            // The bounce is initialized by state 2 on the following update.
            _state = RockState.CollisionPending;
            Position = destination;
            QueueRedraw();
            return;
        }
        Position = destination;
        QueueRedraw();
    }

    internal bool DeflectWithSword()
    {
        if (Finished || _state is not (RockState.Initializing or RockState.Flying))
            return false;
        BeginBounce();
        return true;
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (_transitionDrawOffset.IsEqualApprox(offset))
            return;
        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished)
            return;
        Texture2D texture = _state == RockState.Bouncing ? _bounceTexture : _normalTexture;
        DrawTexture(texture, new Vector2(-16, -16 + (_zFixed >> 8)) + _transitionDrawOffset);
    }

    private void BeginBounce()
    {
        _state = RockState.Bouncing;
        _angle ^= 0x10;
        _counter = BounceFrames;
        _zFixed = 0;
        _speedZ = BounceSpeedZ;
        QueueRedraw();
    }

    private void UpdateBounce()
    {
        _counter--;
        if (_counter == 0)
        {
            Finish();
            return;
        }
        _zFixed += _speedZ;
        if (_zFixed < 0)
            _speedZ += BounceGravity;
        else
            _zFixed = 0;
        Position += DirectionForAngle(_angle) * 0.25f;
        QueueRedraw();
    }

    private bool OverlapsLink(Vector2 linkPosition)
    {
        return Mathf.Abs(linkPosition.X - Position.X) < _record.CollisionRadiusX + 6 &&
            Mathf.Abs(linkPosition.Y - Position.Y) < _record.CollisionRadiusY + 6;
    }

    private bool WithinVisibleBoundary(Vector2 linkPosition)
    {
        float maxCameraX = Mathf.Max(0.0f, _room.Width - OracleRoomData.ViewportWidth);
        float maxCameraY = Mathf.Max(0.0f, _room.Height - OracleRoomData.ViewportHeight);
        Vector2 cameraOrigin = new(
            Mathf.Clamp(linkPosition.X - OracleRoomData.ViewportWidth / 2.0f, 0.0f, maxCameraX),
            Mathf.Clamp(linkPosition.Y - OracleRoomData.ViewportHeight / 2.0f, 0.0f, maxCameraY));
        Vector2 screen = Position - cameraOrigin;
        return screen.X >= -7.0f && screen.X < 168.0f &&
            screen.Y >= -7.0f && screen.Y < 136.0f;
    }

    private void Finish()
    {
        Finished = true;
        Visible = false;
    }

    private static Vector2 DirectionForAngle(int angle) => (angle & 0x18) switch
    {
        0x00 => Vector2.Up,
        0x08 => Vector2.Right,
        0x10 => Vector2.Down,
        _ => Vector2.Left
    };

    private static Texture2D BuildFirstFrame(
        Image source,
        string animation,
        int tileBase,
        int palette)
    {
        string firstFrame = animation.Split('|', StringSplitOptions.RemoveEmptyEntries)[0];
        int separator = firstFrame.IndexOf('@');
        if (separator < 0)
            throw new InvalidOperationException("Malformed Octorok projectile animation.");
        return NpcCharacter.BuildOamTexture(
            source, firstFrame[(separator + 1)..], tileBase, palette);
    }
}
