using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>ENEMY_SWOOP $71 native miniboss.</summary>
internal sealed partial class SwoopBoss : EnemyCharacter
{
    private static readonly int[] Speeds = { 0x14, 0x28, 0x3c };
    private static readonly int[] FramesBeforeAttack = { 255, 150, 60 };

    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
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
    private bool _acceptedGroundHit;
    private bool _dying;
    private int _deathCounter;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal SwoopState State => _state;
    internal int Counter => _counter;
    internal int ZFixed => _zFixed;
    internal bool Defeated => _dying || IsDead;
    protected override Vector2 AnimationDrawOffset => new(-16, -16);
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && !_dying &&
        _state is SwoopState.Stomping or
            SwoopState.Grounded or
            SwoopState.Bouncing;

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
        ZIndex = 10;
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
                Finish();
                spawns.Add(new BossDeathExplosionSpawn(Position, Record.Id));
                _restoreRoomMusic();
            }
            QueueRedraw();
            return;
        }

        if (!_introStarted)
        {
            _introStarted = true;
            _disableLink();
            _playSound(OracleSoundEngine.SndCtrlStopMusic);
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
                    _playSound(OracleSoundEngine.MusMiniboss);
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
        _playSound(OracleSoundEngine.SndBossDamage);
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
            _playSound(OracleSoundEngine.SndDoorClose);
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
            _playSound(OracleSoundEngine.SndJump);
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
        _enableLink();
    }

    private void UpdateFlying(Player player)
    {
        AdvanceAnimation();
        if (AnimationParameter != 0)
        {
            _speedZ = -0x100;
            _playSound(OracleSoundEngine.SndJump);
        }
        _zFixed += _speedZ;
        _speedZ += 0x08;
        _zFixed = Mathf.Clamp(_zFixed, -(0x30 << 8), -(8 << 8));

        int anger = AngerLevel();
        Position += OracleObjectMovement.Shared.Delta(
            Speeds[anger], _angle);
        ConstrainToRoom();

        if (_attackCounter > 0)
            _attackCounter--;
        if (_attackCounter == 0 &&
            Position.DistanceTo(player.Position) < 0x30)
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
    }

    private void UpdateTelegraph(Player player)
    {
        AdvanceAnimation();
        AdvanceAnimation();
        if (--_counter == 10)
        {
            int packed = _room.GetPackedPosition(player.Position);
            _stompTarget = new Vector2(
                (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
                (packed >> 4) * OracleRoomData.MetatileSize + 8);
            _angle = OracleObjectMovement.Shared.RelativeAngle(
                Position, _stompTarget);
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
        if (Mathf.Abs(Position.X - _stompTarget.X) >= 2 ||
            Mathf.Abs(Position.Y - _stompTarget.Y) >= 2)
        {
            _angle = OracleObjectMovement.Shared.RelativeAngle(
                Position, _stompTarget);
            Position += OracleObjectMovement.Shared.Delta(0x50, _angle);
        }
        if (!UpdateHeight(0x10))
            return;

        HitGround(spawns);
        if (Health >= 0x0a)
        {
            _state = SwoopState.Grounded;
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
        Position += OracleObjectMovement.Shared.Delta(0x28, _angle);
        ConstrainToRoom();
        if (!UpdateHeight(0x10))
            return;
        HitGround(spawns);
        _bounces--;
        if (_bounces > 0)
        {
            _angle = OracleObjectMovement.Shared.RelativeAngle(
                Position, player.Position);
            _speedZ = -0x100;
            return;
        }
        _state = SwoopState.Grounded;
        _counter = 90;
        Visible = true;
    }

    private bool UpdateHeight(int gravity)
    {
        _zFixed += _speedZ;
        _speedZ += gravity;
        if (_zFixed < 0)
            return false;
        _zFixed = 0;
        _speedZ = 0;
        return true;
    }

    private void HitGround(ICollection<RoomEntitySpawn> spawns)
    {
        _screenShake(0x30);
        _playSound(OracleSoundEngine.SndDoorClose);
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

    private void ConstrainToRoom()
    {
        Position = new Vector2(
            Mathf.Clamp(Position.X, 8.0f, _room.Width - 8.0f),
            Mathf.Clamp(Position.Y, 8.0f, _room.Height - 8.0f));
    }

    private void BeginDeath()
    {
        Revive(1);
        _dying = true;
        _deathCounter = 120;
        _disableLink();
        _playSound(OracleSoundEngine.SndBossDead);
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
