using Godot;
using System;

namespace oracleofages;

internal abstract partial class SpiritsGraveEnemyCharacter : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private int _health;
    private int _invincibility;
    private int _frameCounter;

    internal SpiritsGraveDatabase.EnemyRecord Record { get; private set; }
    internal bool IsDead { get; private set; }
    internal int Health => _health;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int AnimationFrame => _animation.FrameIndex;
    internal Texture2D CurrentAnimationTexture => _animation.CurrentTexture;
    internal virtual bool CollisionEnabled => !IsDead && Visible;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(Record.RadiusX, Record.RadiusY),
        new Vector2(Record.RadiusX * 2, Record.RadiusY * 2));

    protected void InitializeEnemy(
        SpiritsGraveDatabase.EnemyRecord record,
        Vector2 position,
        int initialAnimation = 0)
    {
        Record = record;
        Position = position;
        _health = record.Health;
        _animation = new EnemyAnimationPlayer(this, record.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(record.Sprites),
            record.Animations,
            record.TileBase,
            record.Palette,
            record.Palette == 5 ? 2 : 5,
            sourceGrayscaleInverted: record.SourceGrayscaleInverted);
        SetAnimation(initialAnimation);
        QueueRedraw();
    }

    internal virtual bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!CollisionEnabled || _invincibility > 0)
            return false;
        return ApplyDamage(damage, SwordInvincibilityFrames);
    }

    internal virtual bool TakeBurnHit(int damage) =>
        CollisionEnabled && ApplyDamage(damage, invincibilityFrames: 0);

    internal bool OverlapsLink(Vector2 linkPosition) =>
        CollisionEnabled &&
        Mathf.Abs(linkPosition.X - Position.X) < Record.RadiusX + 6 &&
        Mathf.Abs(linkPosition.Y - Position.Y) < Record.RadiusY + 6;

    protected void BeginFrame()
    {
        _frameCounter = (_frameCounter + 1) & 0xff;
        if (_invincibility > 0)
            _invincibility--;
    }

    protected virtual int SwordInvincibilityFrames => 0x15;

    protected void SetAnimation(int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, Record.Animations.Length - 1);
        if (_animation.AnimationIndex != safeIndex || !_animation.HasFrames)
            _animation.SetAnimation(safeIndex);
    }

    protected void AdvanceAnimation(int decrement = 1) =>
        _animation.Advance(decrement);

    protected void Revive(int health)
    {
        IsDead = false;
        _health = health;
        _invincibility = 0;
        Visible = true;
    }

    protected void Finish()
    {
        IsDead = true;
        Visible = false;
    }

    public override void _Draw()
    {
        if (CollisionEnabled)
            DrawCurrentAnimation();
    }

    protected void DrawCurrentAnimation()
    {
        if (!_animation.HasFrames)
            return;
        DrawTexture(
            _invincibility > 0 && (_frameCounter & 4) == 0
                ? _animation.DamageTexture
                : _animation.CurrentTexture,
            new Vector2(-16, -16) + TransitionDrawOffset);
    }

    private bool ApplyDamage(int damage, int invincibilityFrames)
    {
        _health = Math.Max(0, _health - Math.Max(1, damage));
        if (_health == 0)
            Finish();
        else if (invincibilityFrames > 0)
            _invincibility = invincibilityFrames;
        return true;
    }
}

internal partial class BoomerangMoblinCharacter : SpiritsGraveEnemyCharacter
{
    internal enum MoblinState { Moving, Deciding, WaitingForBoomerang }

    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private MoblinState _state;
    private int _counter;
    private int _angle;
    private bool _initialized;
    private bool _boomerangReturned;

    internal MoblinState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;

    internal void Initialize(
        SpiritsGraveDatabase.EnemyRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        InitializeEnemy(record, position);
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
    }

    internal int UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return -1;
        BeginFrame();
        if (!_initialized)
        {
            // State 0 initializes SPEED_80 and chooses the first route on the
            // enemy's first object update, not while the room is parsed.
            _initialized = true;
            ChooseDirection();
            return -1;
        }
        switch (_state)
        {
            case MoblinState.Moving:
                _counter--;
                if (_counter == 0 || !_movement.MoveAtAngle(
                    _angle, 0.5f, allowHoles: false))
                {
                    _state = MoblinState.Deciding;
                }
                break;

            case MoblinState.Deciding:
                ChooseDirection();
                int target = (OracleObjectMath.AngleToward(Position, linkPosition) + 4) & 0x18;
                if (target == _angle)
                {
                    _state = MoblinState.WaitingForBoomerang;
                    return _angle;
                }
                break;

            case MoblinState.WaitingForBoomerang:
                if (_boomerangReturned)
                {
                    _boomerangReturned = false;
                    ChooseDirection();
                }
                break;
        }
        AdvanceAnimation();
        return -1;
    }

    internal void ReturnBoomerang() => _boomerangReturned = true;

    private void ChooseDirection()
    {
        // @gotoState8WithRandomAngleAndCounter calls getRandomNumber for the
        // duration, then ecom_setRandomCardinalAngle consumes a second value.
        _counter = 0x30 + (_random.Next().Value & 0x03) * 0x10;
        _angle = _random.Next().Value & 0x18;
        _state = MoblinState.Moving;
        SetAnimation(_angle >> 3);
    }
}

internal partial class MoblinBoomerangProjectile : TransitionOffsetNode2D
{
    private readonly BoomerangMoblinCharacter _owner;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _animation;
    private int _angle;
    private int _counter;
    private int _speedCounter;
    private float _speed;
    private bool _initialized;
    private bool _returning;

    internal MoblinBoomerangProjectile(
        BoomerangMoblinCharacter owner,
        OracleRoomData room,
        Vector2 position,
        int angle,
        SpiritsGraveDatabase.VisualRecord visual)
    {
        _owner = owner;
        _room = room;
        Position = position;
        _angle = angle;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette);
        _animation.SetAnimation(0);
    }

    internal bool Finished { get; private set; }
    internal int Counter => _counter;
    internal float Speed => _speed;
    internal bool Returning => _returning;
    internal Rect2 CollisionBounds => new(Position - new Vector2(2, 2), new Vector2(4, 4));

    internal void UpdateFrame(Player player, int frameCounter)
    {
        if (Finished)
            return;
        if (_owner.IsDead)
        {
            Finish(returned: false);
            return;
        }
        if (!_initialized)
        {
            // partCode21 state 0 initializes but does not move.
            _initialized = true;
            _counter = 0x2d;
            _speedCounter = 6;
            _speed = 2.0f;
            _animation.Advance();
            QueueRedraw();
            return;
        }
        if (!_returning)
        {
            // State 1 checks the current quarter-tile collision and decrements
            // counter1 before changing speed or applying movement.
            if (_room.IsSolid(Position) || --_counter == 0)
            {
                BeginReturn();
            }
            else
            {
                if (--_speedCounter == 0)
                {
                    _speedCounter = 6;
                    _speed = Math.Max(0.0f, _speed - 0.125f);
                }
                Position += OracleObjectMath.CardinalVector(_angle) * _speed;
            }
        }
        else
        {
            if ((frameCounter & 3) == 0)
                _speed = Math.Min(1.875f, _speed + 0.125f);
            Vector2 delta = _owner.Position - Position;
            _angle = OracleObjectMath.AngleToward(Position, _owner.Position);
            if (Mathf.Abs(delta.X) <= 4 && Mathf.Abs(delta.Y) <= 4)
            {
                Finish(returned: true);
                return;
            }
            Position += OracleObjectMath.VectorFromAngle32(_angle) * _speed;
        }
        if (Mathf.Abs(player.Position.X - Position.X) < 8 &&
            Mathf.Abs(player.Position.Y - Position.Y) < 8)
        {
            player.ApplyEnemyContactDamage(Position, 2);
            BeginReturn();
        }
        _animation.Advance();
        QueueRedraw();
    }

    internal bool Deflect()
    {
        if (Finished)
            return false;
        BeginReturn();
        return true;
    }

    public override void _Draw()
    {
        if (Finished)
            return;
        DrawTexture(
            _animation.CurrentTexture,
            new Vector2(-16, -16) + TransitionDrawOffset);
    }

    private void BeginReturn() => _returning = true;

    private void Finish(bool returned)
    {
        if (returned)
            _owner.ReturnBoomerang();
        Finished = true;
        Visible = false;
    }
}

internal partial class RopeCharacter : SpiritsGraveEnemyCharacter
{
    internal enum RopeState { Wandering, Charging }

    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private RopeState _state;
    private int _counter;
    private int _cooldown;
    private int _angle;
    private float _speed;
    private bool _initialized;

    internal RopeState State => _state;
    internal int Counter => _counter;
    internal int Cooldown => _cooldown;
    internal int Angle => _angle;
    internal float Speed => _speed;

    internal void Initialize(
        SpiritsGraveDatabase.EnemyRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        InitializeEnemy(record, position);
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _speed = 0.5f;
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return;
        BeginFrame();
        if (!_initialized)
        {
            // State 0 sets direction $ff/SPEED_80 and advances to state 8.
            // State 8 falls through to movement on the following update.
            _initialized = true;
            return;
        }
        if (_state == RopeState.Wandering && _cooldown == 0 &&
            IsCenteredWithLink(linkPosition))
        {
            _angle = (OracleObjectMath.AngleToward(
                OracleObjectMath.ToPixelPosition(Position),
                OracleObjectMath.ToPixelPosition(linkPosition)) + 4) & 0x18;
            _speed = 1.25f;
            _state = RopeState.Charging;
            SetAnimationFromAngle();
            return;
        }

        if (_cooldown > 0)
            _cooldown--;
        if (_state == RopeState.Wandering)
            _counter = (_counter - 1) & 0xff;
        bool moved = _movement.MoveAtAngle(_angle, _speed, allowHoles: false);
        if (!moved || _state == RopeState.Wandering && _counter == 0)
        {
            if (_state == RopeState.Charging)
            {
                _cooldown = 0x40;
                _speed = 0.375f;
            }
            ChangeDirection();
            return;
        }
        AdvanceAnimation(_state == RopeState.Charging ? 3 : 1);
    }

    private void ChangeDirection()
    {
        OracleRandom.Result result = _random.Next();
        _angle = result.High & 0x18;
        _counter = 0x70 + (result.Low & 0x70);
        _state = RopeState.Wandering;
        SetAnimationFromAngle();
    }

    private bool IsCenteredWithLink(Vector2 linkPosition)
    {
        Vector2 rope = OracleObjectMath.ToPixelPosition(Position);
        Vector2 link = OracleObjectMath.ToPixelPosition(linkPosition);
        // objectCheckCenteredWithLink uses a 2*b+1 unsigned range, so b=$0a
        // accepts the inclusive high-byte interval [-10, 10] on either axis.
        return Mathf.Abs(link.X - rope.X) <= 0x0a ||
            Mathf.Abs(link.Y - rope.Y) <= 0x0a;
    }

    private void SetAnimationFromAngle() =>
        SetAnimation((_angle & 0x10) != 0 ? 0 : 1);
}

internal partial class GhiniCharacter : SpiritsGraveEnemyCharacter
{
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private int _counter;
    private int _angle;
    private GhiniState _state;

    internal enum GhiniState { Uninitialized, Choosing, Moving }

    internal int Counter => _counter;
    internal int Angle => _angle;
    internal GhiniState State => _state;

    internal void Initialize(
        SpiritsGraveDatabase.EnemyRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        InitializeEnemy(record, position);
        _random = random;
        _room = room;
    }

    internal void UpdateFrame()
    {
        if (IsDead)
            return;
        BeginFrame();
        if (_state == GhiniState.Uninitialized)
        {
            _state = GhiniState.Choosing;
            return;
        }
        if (_state == GhiniState.Choosing)
        {
            ChooseDirection();
            _state = GhiniState.Moving;
            return;
        }
        Position += OracleObjectMath.VectorFromAngle32(_angle) * 0.5f;
        bool horizontal = Position.X < 6 || Position.X >= _room.Width - 6;
        bool vertical = Position.Y < 6 || Position.Y >= _room.Height - 6;
        Position = new Vector2(
            Mathf.Clamp(Position.X, 6, _room.Width - 7),
            Mathf.Clamp(Position.Y, 6, _room.Height - 7));
        if (horizontal)
            _angle = (0x20 - _angle) & 0x1f;
        if (vertical)
            _angle = (0x10 - _angle) & 0x1f;
        if (horizontal || vertical)
            SetAnimation(_angle < 0x10 ? 1 : 0);
        _counter--;
        if (_counter == 0)
            _state = GhiniState.Choosing;
        AdvanceAnimation();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!CollisionEnabled)
            return;
        DrawSetTransform(Vector2.Up * 2.0f);
        base._Draw();
        DrawSetTransform(Vector2.Zero);
    }

    private void ChooseDirection()
    {
        OracleRandom.Result result = _random.Next();
        _counter = 0x30 + (result.Low & 0x7f);
        _angle = result.High & 0x18;
        SetAnimation(_angle < 0x10 ? 1 : 0);
    }
}

internal partial class WallmasterCharacter : SpiritsGraveEnemyCharacter
{
    internal enum WallmasterState { Waiting, Falling, Grounded, Rising }

    private WallmasterState _state;
    private int _counter = 180;
    private int _zFixed;
    private int _speedZ;
    private int _remaining = 5;
    private OracleRoomData _room = null!;
    private bool _active;
    private Player? _grabbedPlayer;
    private bool _warpRequested;
    private bool _deathPuffPending;
    private bool _initialized;

    internal WallmasterState State => _state;
    internal int Counter => _counter;
    internal int Remaining => _remaining;
    internal int ZFixed => _zFixed;
    internal bool WarpRequested => _warpRequested;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && _active && _grabbedPlayer is null &&
        Math.Abs(_zFixed >> 8) < 7;

    internal void Initialize(
        SpiritsGraveDatabase.EnemyRecord record,
        OracleRoomData room,
        Vector2 position)
    {
        InitializeEnemy(record, position);
        _room = room;
        Visible = false;
    }

    internal void UpdateFrame(Player player, Action<int> soundRequested)
    {
        if (IsDead)
            return;
        BeginFrame();
        if (!_initialized)
        {
            // The subid-$00 spawner installs its 180-update counter in state
            // 0, then begins decrementing it on the next object update.
            _initialized = true;
            _counter = 180;
            return;
        }
        switch (_state)
        {
            case WallmasterState.Waiting:
                _counter--;
                if (_counter != 0)
                    return;
                // The subid-$00 spawner always reloads its 120-update delay
                // before testing Link's tile. A blocked spawn therefore waits
                // for the full interval before trying again.
                _counter = 120;
                if (_room.IsSolid(player.Position))
                    return;
                Position = player.Position;
                _zFixed = -(0x60 << 8);
                _speedZ = 0;
                _active = true;
                Visible = true;
                _state = WallmasterState.Falling;
                soundRequested(OracleSoundEngine.SndFallInHole);
                break;

            case WallmasterState.Falling:
                FollowGrabbedPlayer();
                if (!OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x0e))
                {
                    UpdateHighVisibility();
                    break;
                }
                _counter = 30;
                _state = WallmasterState.Grounded;
                break;

            case WallmasterState.Grounded:
                _counter--;
                if (_counter == 20)
                    SetAnimation(1);
                if (_grabbedPlayer is not null && _counter < 20)
                    _grabbedPlayer.Visible = false;
                if (_counter == 0)
                    _state = WallmasterState.Rising;
                break;

            case WallmasterState.Rising:
                UpdateHighVisibility();
                _zFixed -= 2 << 8;
                if (_zFixed > -(0x60 << 8))
                    break;
                if (_grabbedPlayer is not null)
                {
                    _warpRequested = true;
                    return;
                }
                HideAndReset(120);
                break;
        }
        AdvanceAnimation();
        QueueRedraw();
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!base.TakeSwordHit(sourcePosition, damage))
            return false;
        if (!IsDead)
            return true;

        _deathPuffPending = true;
        _remaining--;
        if (_remaining == 0)
            return true;
        Revive(Record.Health);
        HideAndReset(120);
        return true;
    }

    internal bool HandleLinkContact(Player player)
    {
        if (_grabbedPlayer is not null || !CollisionEnabled ||
            !OverlapsLink(player.Position))
        {
            return false;
        }
        _grabbedPlayer = player;
        player.BeginCutsceneControl();
        player.SetScriptedPosition(Position);
        return true;
    }

    internal EnemyDeathPuffSpawn? TakeDeathPuff()
    {
        if (!_deathPuffPending)
            return null;
        _deathPuffPending = false;
        return new EnemyDeathPuffSpawn(Position, EnemyId: Record.Id);
    }

    internal Player? TakeWarpedPlayer()
    {
        if (!_warpRequested)
            return null;
        _warpRequested = false;
        // Keep ownership until the room reload removes this hand. Clearing it
        // here would re-enable collision for the contact pass later in the
        // same update and capture Link a second time.
        return _grabbedPlayer;
    }

    public override void _Draw()
    {
        if (!_active || IsDead)
            return;
        DrawSetTransform(new Vector2(0, _zFixed >> 8));
        base._Draw();
        DrawSetTransform(Vector2.Zero);
    }

    private void HideAndReset(int delay)
    {
        _active = false;
        Visible = false;
        _counter = delay;
        _state = WallmasterState.Waiting;
        _zFixed = 0;
        _speedZ = 0;
        SetAnimation(0);
    }

    private void FollowGrabbedPlayer()
    {
        if (_grabbedPlayer is null)
            return;
        _grabbedPlayer.SetScriptedPosition(Position);
    }

    private void UpdateHighVisibility()
    {
        int z = _zFixed >> 8;
        if (z < -0x48)
            Visible = !Visible;
        else if (z < -0x44)
            Visible = true;
    }
}
