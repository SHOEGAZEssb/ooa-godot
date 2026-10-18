using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>ENEMY_SWOOP $71 native miniboss.</summary>
internal sealed partial class SwoopBoss : EnemyCharacter
{
    internal override bool InitializationPending => false;

    private static readonly int[] Speeds = { 0x14, 0x28, 0x3c };
    private static readonly int[] FramesBeforeAttack = { 255, 150, 60 };

    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private Action<int> _playSound = null!;
    private Func<bool> _shuttersClosed = null!;
    private Action<int> _screenShake = null!;
    private Action _disableLink = null!;
    private Action _enableLink = null!;
    private Action _restoreRoomMusic = null!;
    private Action<int, string, Vector2> _showDialogue = null!;
    private Func<bool> _dialogueOpen = null!;
    private Func<long> _animationTick = null!;
    private string _introMessage = string.Empty;
    private SwoopState _state = SwoopState.WaitingForDoors;
    private int _counter;
    private int _attackCounter;
    private int _angle;
    private int _bounces;
    private int _zFixed;
    private int _speedZ;
    private Vector2 _stompTarget;
    private bool _introStarted;
    private bool _introActive = true;
    private bool _acceptedGroundHit;
    private bool _dying;
    private int _deathCounter;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal SwoopState State => _state;
    internal int ZFixed => _zFixed;
    internal bool IntroActive => _introActive;
    protected override Vector2 AnimationDrawOffset => new(-16, -16);
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && !_dying &&
        _state is SwoopState.Stomping or
            SwoopState.Grounded or
            SwoopState.Bouncing;

    /// <summary>
    /// collisionEffects.s compares Enemy.zh with Link.zh through the strict
    /// $0e/$07 window before applying the ordinary X/Y radii. Swoop enables
    /// collision for the complete stomp descent, so omitting this height test
    /// makes the airborne shadow-sized box damage Link too early.
    /// </summary>
    internal bool OverlapsLinkAtCollisionHeight(Vector2 linkPosition) =>
        RoomEntityManager.ObjectCollisionZOverlaps(
            _zFixed >> 8,
            itemZ: 0,
            radius: 0x07) &&
        OverlapsLink(linkPosition);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int> playSound,
        Func<bool> shuttersClosed,
        Action<int> screenShake,
        Action disableLink,
        Action enableLink,
        Action restoreRoomMusic,
        Action<int, string, Vector2> showDialogue,
        Func<bool> dialogueOpen,
        Func<long> animationTick,
        string introMessage)
    {
        Record = record;
        _room = room;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _playSound = playSound;
        _shuttersClosed = shuttersClosed;
        _screenShake = screenShake;
        _disableLink = disableLink;
        _enableLink = enableLink;
        _restoreRoomMusic = restoreRoomMusic;
        _showDialogue = showDialogue;
        _dialogueOpen = dialogueOpen;
        _animationTick = animationTick;
        _introMessage = introMessage;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record),
            initialAnimation: 2);
        Name = "Swoop";
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        Visible = false;
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead)
            return;
        BeginFrame();
        if (_dying)
        {
            Visible = (_deathCounter & 1) != 0;
            if (--_deathCounter == 0)
            {
                if (!TrySpawnNativePart(new BossDeathExplosionSpawn(Position, Record.Id), spawns))
                { _deathCounter = 1; return; }
                Finish();
                _restoreRoomMusic();
            }
            QueueRedraw();
            return;
        }

        if (!_introStarted)
        {
            _introStarted = true;
            _disableLink();
            _playSound(SoundId.SndCtrlStopMusic);
        }

        switch (_state)
        {
            case SwoopState.WaitingForDoors:
                if (_shuttersClosed())
                    BeginIntroFall();
                break;

            case SwoopState.IntroFalling:
                UpdateIntroFall();
                break;

            case SwoopState.IntroDialogue:
                if (!_dialogueOpen())
                {
                    _playSound(SoundId.MusMiniboss);
                    BeginFlyingUp();
                }
                break;

            case SwoopState.FlyingUp:
                UpdateFlyingUp(player);
                break;

            case SwoopState.Flying:
                UpdateFlying(player);
                break;

            case SwoopState.Telegraph:
                UpdateTelegraph(player);
                break;

            case SwoopState.Stomping:
                UpdateStomp(player, spawns);
                break;

            case SwoopState.Grounded:
                UpdateGrounded();
                break;

            case SwoopState.Bouncing:
                UpdateBounce(player, spawns);
                break;
        }
        QueueRedraw();
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (_dying || !base.TakeSwordHit(sourcePosition, damage))
            return false;
        _acceptedGroundHit = true;
        _playSound(SoundId.SndBossDamage);
        if (IsDead)
            BeginDeath();
        return true;
    }

    internal override bool TakeBurnHit(int damage)
    {
        if (_dying || !base.TakeBurnHit(damage))
            return false;
        _acceptedGroundHit = true;
        if (IsDead)
            BeginDeath();
        return true;
    }

    public override void _Draw()
    {
        if (!DrawsAnimation)
            return;
        DrawSetTransform(Vector2.Down * (_zFixed / 256.0f));
        DrawCurrentAnimation();
        DrawSetTransform(Vector2.Zero);
    }

    private void BeginIntroFall()
    {
        _state = SwoopState.IntroFalling;
        _zFixed = -(0x88 << 8);
        _speedZ = 0;
        _counter = 60;
        _bounces = 1;
        Visible = true;
        SetAnimation(2);
    }

    private void UpdateIntroFall()
    {
        if (!UpdateHeight(0x10))
            return;
        if (_bounces > 0)
        {
            _bounces--;
            _speedZ = -0x180;
            _screenShake(10);
            _playSound(SoundId.SndDoorClose);
            return;
        }
        SetAnimation(0);
        if (--_counter != 0)
            return;
        _state = SwoopState.IntroDialogue;
        _showDialogue(0x2f00, _introMessage, Position);
    }

    private void BeginFlyingUp()
    {
        ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex; // swoop_flyFurtherUp: visible80.
        _state = SwoopState.FlyingUp;
        _counter = 3 * 0x30;
        _speedZ = -0x100;
        SetAnimation(3);
        Visible = true;
    }

    private void UpdateFlyingUp(Player player)
    {
        if ((_counter % 0x30) == 0)
        {
            _speedZ = -0x100;
            _playSound(SoundId.SndJump);
        }
        _zFixed += _speedZ;
        _speedZ += 0x08;
        if (--_counter != 0)
            return;

        _state = SwoopState.Flying;
        _counter = 60;
        _attackCounter = FramesBeforeAttack[AngerLevel()];
        _angle = OracleObjectMovement.Shared.RelativeAngle(
            Position, player.Position);
        SetAnimation(0);
        if (_introActive)
        {
            _introActive = false;
            _enableLink();
        }
    }

    private void UpdateFlying(Player player)
    {
        AdvanceAnimation();
        if (AnimationParameter != 0)
        {
            _playSound(SoundId.SndJump);
        }

        // swoop_stateA animates without integrating Z. Its attack gate
        // precedes planar movement and uses objectCheckLinkWithinDistance.
        if (_attackCounter > 0)
            _attackCounter--;
        if (_attackCounter == 0 &&
            Math.Abs(OracleObjectPosition.HighByte(Position.X) - OracleObjectPosition.HighByte(player.Position.X)) +
            Math.Abs(OracleObjectPosition.HighByte(Position.Y) - OracleObjectPosition.HighByte(player.Position.Y)) < 0x30)
        {
            _state = SwoopState.Telegraph;
            _counter = 30;
            return;
        }
        if (--_counter == 0)
        {
            _counter = 60;
            _angle = OracleObjectMovement.Shared.RelativeAngle(
                Position, player.Position);
        }
        // The expired turn counter changes this update's movement angle.
        _movement.MoveAtAngle(_angle, Speeds[AngerLevel()], allowHoles: true);
    }

    private void UpdateTelegraph(Player player)
    {
        AdvanceAnimation();
        AdvanceAnimation();
        if (--_counter == 10)
        {
            int packed = _room.GetPackedPosition(player.Position);
            Vector2 tileCenter = new(
                (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
                (packed >> 4) * OracleRoomData.MetatileSize + 8);
            // swoop_stateB stores masked Y in var32, but the initial angle
            // still uses B/C's unmasked tile centre. Stomping retargets from
            // the stored tile-top Y on its first movement update.
            _stompTarget = tileCenter - Vector2.Down * 8;
            _angle = OracleObjectMovement.Shared.RelativeAngle(
                Position, tileCenter);
        }
        if (_counter != 0)
            return;
        _state = SwoopState.Stomping;
        _speedZ = 0;
        SetAnimation(2);
    }

    private void UpdateStomp(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if ((OracleObjectPosition.HighByte(Position.X) & 0xfe) != (int)_stompTarget.X ||
            (OracleObjectPosition.HighByte(Position.Y) & 0xfe) != (int)_stompTarget.Y)
        {
            _angle = OracleObjectMovement.Shared.RelativeAngle(
                Position, _stompTarget);
            _movement.MoveAtAngle(_angle, 0x50, allowHoles: true);
        }
        if (!UpdateHeight(0x10))
            return;

        HitGround(spawns);
        if (Health >= 0x0a)
        {
            _state = SwoopState.Grounded;
            ZIndex = ObjectDrawPriority.BehindLinkZIndex; // swoop_setVisible: visible82.
            _counter = 90;
            Visible = true;
            return;
        }

        _state = SwoopState.Bouncing;
        _bounces = (_random.Next().Value & 1) + 1;
        _angle = OracleObjectMovement.Shared.RelativeAngle(
            Position, player.Position);
        _speedZ = -0x100;
    }

    private void UpdateGrounded()
    {
        AdvanceAnimation();
        if (!_acceptedGroundHit &&
            AnimationParameter == 0 &&
            --_counter != 0)
        {
            return;
        }
        _acceptedGroundHit = false;
        BeginFlyingUp();
    }

    private void UpdateBounce(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        _movement.MoveAtAngle(_angle, 0x28, allowHoles: true);
        if (!UpdateHeight(0x10))
            return;
        HitGround(spawns);
        _bounces--;
        if (_bounces > 0)
        {
            // swoop_stomp_substate3 only resets speedZ between bounces;
            // it retains the angle chosen at the initial stomp impact.
            _speedZ = -0x100;
            return;
        }
        _state = SwoopState.Grounded;
        ZIndex = ObjectDrawPriority.BehindLinkZIndex; // stomp_substate3: visible82.
        _counter = 90;
        Visible = true;
    }

    private bool UpdateHeight(int gravity)
    {
        _zFixed += _speedZ;
        if (_zFixed < 0)
        {
            _speedZ += gravity;
            return false;
        }
        // objectUpdateSpeedZ_paramC clamps Z on landing and returns before
        // changing speedZ. The caller owns any subsequent bounce reset.
        _zFixed = 0;
        return true;
    }

    private void HitGround(ICollection<RoomEntitySpawn> spawns)
    {
        _screenShake(0x30);
        _playSound(SoundId.SndDoorClose);
        Vector2 point = Position + Vector2.Down * 5;
        TerrainInfo terrain = _room.GetTerrainInfo(point);
        byte tile = terrain.Tile;
        if (terrain.Collision == 0x0f || tile is 0xa2 or 0x48)
            return;
        _room.SetPositionTileAndCollision(
            point, 0x48, collision: null, _animationTick());
        spawns.Add(new RockDebrisSpawn(
            new Vector2(
                Mathf.Floor(point.X / 16.0f) * 16 + 8,
                Mathf.Floor(point.Y / 16.0f) * 16 + 8)));
    }

    private int AngerLevel() =>
        Health >= 0x0a ? 0 : Health >= 0x06 ? 1 : 2;

    private void BeginDeath()
    {
        Revive(1);
        _dying = true;
        _deathCounter = 120;
        _disableLink();
        _playSound(SoundId.SndBossDead);
    }
}

internal enum SwoopState
{
    WaitingForDoors,
    IntroFalling,
    IntroDialogue,
    FlyingUp,
    Flying,
    Telegraph,
    Stomping,
    Grounded,
    Bouncing
}
