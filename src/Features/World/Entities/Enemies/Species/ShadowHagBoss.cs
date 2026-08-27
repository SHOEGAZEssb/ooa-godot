using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>ENEMY_SHADOW_HAG $7a, Moonlit Grotto's main boss.</summary>
internal sealed partial class ShadowHagBoss : EnemyCharacter
{
    private static readonly Vector2[] ConvergencePositions =
    [
        new(0x48, 0x38), new(0xb8, 0x38),
        new(0x48, 0x78), new(0xb8, 0x78)
    ];
    private static readonly Vector2I[] SpawnOffsets =
    [
        new(0x00, 0x40), new(-0x40, 0x08),
        new(0x00, -0x40), new(0x40, 0x08)
    ];

    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Action<int> _playSound = null!;
    private Func<bool> _shuttersClosed = null!;
    private Action _disableLink = null!;
    private Action _enableLink = null!;
    private Action _restoreRoomMusic = null!;
    private Action<int, string, Vector2> _showDialogue = null!;
    private Func<bool> _dialogueOpen = null!;
    private string _introMessage = string.Empty;
    private ShadowHagState _state = ShadowHagState.IntroWaitingForDoors;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _zFixed;
    private int _handledAnimationFrame = -1;
    private int _bugsAlive;
    private bool _initialized;
    private bool _introActive = true;
    private bool _spawnFailed;
    private bool _dying;
    private int _deathCounter;
    private Vector2 _linkPosition;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal ShadowHagState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int BugsAlive => _bugsAlive;
    internal bool IntroActive => _introActive;
    internal bool Defeated => _dying || IsDead;
    internal Vector2 LinkPosition => _linkPosition;
    internal bool ShadowsConverging =>
        _state == ShadowHagState.ShadowsConverging;
    internal bool Vulnerable =>
        !_dying && _state is ShadowHagState.ChargeTell or
            ShadowHagState.Charging;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && Vulnerable;
    protected override Vector2 AnimationDrawOffset =>
        base.AnimationDrawOffset + Vector2.Down * (_zFixed / 256.0f);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int> playSound,
        Func<bool> shuttersClosed,
        Action disableLink,
        Action enableLink,
        Action restoreRoomMusic,
        Action<int, string, Vector2> showDialogue,
        Func<bool> dialogueOpen,
        string introMessage)
    {
        Record = record;
        _room = room;
        _random = random;
        _playSound = playSound;
        _shuttersClosed = shuttersClosed;
        _disableLink = disableLink;
        _enableLink = enableLink;
        _restoreRoomMusic = restoreRoomMusic;
        _showDialogue = showDialogue;
        _dialogueOpen = dialogueOpen;
        _introMessage = introMessage;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        Name = "ShadowHag";
        ZIndex = 10;
        Visible = false;
    }

    internal void PrepareForScreenTransition()
    {
        if (_initialized)
            return;
        _initialized = true;
        _angle = 0x18;
        _playSound(OracleSoundEngine.SndCtrlStopMusic);
        Visible = false;
        QueueRedraw();
    }

    internal void UpdateFrame(
        Player player,
        int frameCounter,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead)
            return;
        _linkPosition = player.Position;
        BeginFrame();
        if (!_initialized)
        {
            PrepareForScreenTransition();
            return;
        }
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

        switch (_state)
        {
            case ShadowHagState.IntroWaitingForDoors:
                _disableLink();
                if (!_shuttersClosed())
                    return;
                Position = new Vector2(player.Position.X, player.Position.Y + 4);
                _zFixed = -0x100;
                _angle = 0x18;
                _state = ShadowHagState.IntroMovingToCenter;
                spawns.Add(new BossShadowSpawn(
                    () => Position,
                    () => _zFixed >> 8,
                    () => !Defeated &&
                        _state == ShadowHagState.IntroMovingToCenter,
                    Size: 1,
                    YOffset: 4));
                break;

            case ShadowHagState.IntroMovingToCenter:
                _disableLink();
                if (OracleObjectPosition.HighByte(Position.X) >= 0x78)
                {
                    Move(0x18, 0x14);
                    break;
                }
                BeginEmerging();
                _zFixed = 0;
                _counter1 = 0x10;
                _state = ShadowHagState.IntroEmerging;
                break;

            case ShadowHagState.IntroEmerging:
                _disableLink();
                if (!UpdateEmerging())
                    break;
                _state = ShadowHagState.IntroDialogueDelay;
                SetAnimation(2);
                break;

            case ShadowHagState.IntroDialogueDelay:
                _disableLink();
                _counter1 = DecrementByte(_counter1);
                if (_counter1 == 0)
                {
                    _counter1 = 8;
                    _showDialogue(0x2f2b, _introMessage, Position);
                    _state = ShadowHagState.IntroDialogue;
                }
                AdvanceAnimation();
                break;

            case ShadowHagState.IntroDialogue:
                _disableLink();
                if (_dialogueOpen())
                    return;
                _counter1 = DecrementByte(_counter1);
                if (_counter1 != 0)
                {
                    AdvanceAnimation();
                    break;
                }
                BeginReturningToGround();
                _introActive = false;
                _enableLink();
                _playSound(OracleSoundEngine.MusBoss);
                AdvanceAnimation();
                break;

            case ShadowHagState.GroundEyes:
                UpdateGroundEyes(spawns);
                break;

            case ShadowHagState.ShadowsChasing:
                if ((frameCounter & 1) != 0)
                    return;
                _counter1 = DecrementByte(_counter1);
                if (_counter1 != 0)
                    return;
                _counter1 = 0xff;
                _state = ShadowHagState.ShadowsConverging;
                Position = ConvergencePositions[(_random.Next().Value & 6) >> 1];
                break;

            case ShadowHagState.ShadowsConverging:
                if (_counter2 != 0)
                    return;
                _counter2 = (_random.Next().Value & 1) + 2;
                BeginBugSpawnDelay();
                break;

            case ShadowHagState.BugSpawnDelay:
                _counter1 = DecrementByte(_counter1);
                if (_counter1 == 0)
                {
                    _counter1 = 0x41;
                    _state = ShadowHagState.SpawningBugs;
                }
                AdvanceAnimation();
                break;

            case ShadowHagState.SpawningBugs:
                AdvanceAnimation();
                _counter1 = DecrementByte(_counter1);
                if (_counter1 == 0)
                {
                    _counter1 = 30;
                    _state = ShadowHagState.PostBugDelay;
                }
                else if ((_counter1 & 0x0f) == 0 && _bugsAlive < 7)
                {
                    _bugsAlive++;
                    spawns.Add(new ShadowHagBugSpawn(this, Position));
                }
                break;

            case ShadowHagState.PostBugDelay:
                _counter1 = DecrementByte(_counter1);
                if (_counter1 != 0)
                {
                    Visible = !Visible;
                    break;
                }
                _counter1 = _spawnFailed ? 150 : 90;
                _spawnFailed = false;
                _state = ShadowHagState.WaitingBehindLink;
                Visible = false;
                break;

            case ShadowHagState.WaitingBehindLink:
                _counter1 = DecrementByte(_counter1);
                if (_counter1 == 0)
                {
                    _spawnFailed = true;
                    _counter2 = DecrementByte(_counter2);
                    if (_counter2 == 0)
                    {
                        BeginReturningToGround();
                        SetAnimation(4);
                    }
                    else
                        BeginBugSpawnDelay();
                    break;
                }
                if (TryChooseSpawnPosition(player, out Vector2 spawnPosition))
                {
                    Position = spawnPosition;
                    BeginEmerging();
                    _state = ShadowHagState.ChargeEmerging;
                }
                break;

            case ShadowHagState.ChargeEmerging:
                if (!UpdateEmerging())
                    break;
                _state = ShadowHagState.ChargeTell;
                _counter1 = 30;
                SetCollisionRadii(8, 12);
                _angle = CardinalAngleToward(player.Position);
                SetAnimation(CardinalAnimation(_angle));
                break;

            case ShadowHagState.ChargeTell:
                if (LinkLookedAtHag(player))
                {
                    FinishCharge();
                    break;
                }
                _counter1 = DecrementByte(_counter1);
                if (_counter1 == 0)
                {
                    _counter1 = 60;
                    _state = ShadowHagState.Charging;
                }
                AdvanceAnimation();
                break;

            case ShadowHagState.Charging:
                if (LinkLookedAtHag(player))
                {
                    FinishCharge();
                    break;
                }
                _counter1 = DecrementByte(_counter1);
                if (_counter1 == 0 || OutsideChargeBoundary())
                {
                    FinishCharge();
                    break;
                }
                Move(_angle, 0x3c);
                AdvanceAnimation();
                break;

            case ShadowHagState.ReturnDelay:
                _counter1 = DecrementByte(_counter1);
                if (_counter1 == 0)
                    BeginBugSpawnDelay();
                else
                    UpdateReturning();
                break;
        }
        QueueRedraw();
    }

    internal void ShadowReturned()
    {
        if (_state != ShadowHagState.ShadowsConverging || _counter2 == 0)
            return;
        _counter2--;
        Visible = true;
    }

    internal void BugFinished()
    {
        if (_bugsAlive > 0)
            _bugsAlive--;
    }

    internal bool TakeSeedHit(int damage)
    {
        if (!Vulnerable || InvincibilityCounter != 0 ||
            !ApplyDamage(damage, invincibilityFrames: 0x20))
        {
            return false;
        }
        _playSound(OracleSoundEngine.SndBossDamage);
        if (IsDead)
            BeginDeath();
        return true;
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        _ = sourcePosition;
        _ = damage;
        return false;
    }

    internal override bool TakeBurnHit(int damage) => TakeSeedHit(damage);

    private void UpdateGroundEyes(ICollection<RoomEntitySpawn> spawns)
    {
        if (_counter2 != 0)
            _counter2--;
        if (_counter2 != 0)
        {
            UpdateReturning();
            return;
        }
        if (_counter1 != 0)
        {
            _counter1 = DecrementByte(_counter1);
            Visible = !Visible;
            return;
        }
        _state = ShadowHagState.ShadowsChasing;
        _counter1 = 150;
        _counter2 = 4;
        Visible = false;
        for (int angleIndex = 3; angleIndex >= 0; angleIndex--)
            spawns.Add(new ShadowHagShadowSpawn(this, angleIndex));
    }

    private void BeginBugSpawnDelay()
    {
        _state = ShadowHagState.BugSpawnDelay;
        _counter1 = 30;
        SetCollisionRadii(5, 3);
        Visible = true;
        SetAnimation(4);
    }

    private void BeginEmerging()
    {
        SetAnimation(5);
        _handledAnimationFrame = -1;
        Visible = true;
        Position += Vector2.Up * 4;
    }

    private bool UpdateEmerging()
    {
        AdvanceAnimation();
        int parameter = AnimationParameter;
        if (parameter == 0xff)
            return true;
        if (parameter == 1 && _handledAnimationFrame != AnimationFrame)
            Position += Vector2.Up * 8;
        _handledAnimationFrame = AnimationFrame;
        return false;
    }

    private void BeginReturningToGround()
    {
        _state = ShadowHagState.GroundEyes;
        _counter1 = 90;
        _counter2 = 30;
        SetCollisionRadii(5, 3);
        _handledAnimationFrame = -1;
        SetAnimation(6);
    }

    private void UpdateReturning()
    {
        AdvanceAnimation();
        int parameter = AnimationParameter;
        if (parameter is 1 or 2 && _handledAnimationFrame != AnimationFrame)
            Position += Vector2.Down * (parameter == 1 ? 8 : 4);
        _handledAnimationFrame = AnimationFrame;
    }

    private void FinishCharge()
    {
        _counter2 = DecrementByte(_counter2);
        if (_counter2 == 0)
        {
            BeginReturningToGround();
            return;
        }
        _state = ShadowHagState.ReturnDelay;
        _counter1 = 30;
        _handledAnimationFrame = -1;
        SetAnimation(6);
    }

    private bool TryChooseSpawnPosition(Player player, out Vector2 position)
    {
        int direction = DirectionIndex(player.FacingVector);
        Vector2I offset = SpawnOffsets[direction];
        int linkY = OracleObjectPosition.HighByte(player.Position.Y);
        int linkX = OracleObjectPosition.HighByte(player.Position.X);
        int y = (linkY + offset.Y) & 0xff;
        int x = (linkX + offset.X) & 0xff;
        bool validY = ((y - 0x1c) & 0xff) < 0x80;
        bool validX = x < 0xf0;
        bool validDistance = Math.Abs(x - linkX) * 2 < 0x100;
        position = new Vector2(x, y);
        return validY && validX && validDistance &&
            _room.GetTerrainInfo(position).Collision == 0;
    }

    private bool LinkLookedAtHag(Player player)
    {
        int angleToLink = OracleObjectMovement.Shared.RelativeAngle(
            Position, player.Position);
        int expectedDirection = ((angleToLink + 0x14) & 0x18) >> 3;
        return DirectionIndex(player.FacingVector) == expectedDirection;
    }

    private bool OutsideChargeBoundary()
    {
        int y = OracleObjectPosition.HighByte(Position.Y);
        int x = OracleObjectPosition.HighByte(Position.X);
        return ((y - 0x12) & 0xff) >= 0x7e ||
            ((x - 0x18) & 0xff) >= 0xc0;
    }

    private int CardinalAngleToward(Vector2 target) =>
        (OracleObjectMovement.Shared.RelativeAngle(Position, target) + 4) & 0x18;

    private static int CardinalAnimation(int angle) => (angle & 0x18) >> 3;

    private static int DirectionIndex(Vector2I direction) =>
        direction == Vector2I.Up ? 0 :
        direction == Vector2I.Right ? 1 :
        direction == Vector2I.Down ? 2 : 3;

    private void Move(int angle, int speed) =>
        Position += OracleObjectMovement.Shared.Delta(speed, angle);

    private void BeginDeath()
    {
        Revive(1);
        _dying = true;
        _deathCounter = 120;
        _disableLink();
        _playSound(OracleSoundEngine.SndBossDead);
    }

    private static int DecrementByte(int value) => (value - 1) & 0xff;
}

internal enum ShadowHagState
{
    IntroWaitingForDoors,
    IntroMovingToCenter,
    IntroEmerging,
    IntroDialogueDelay,
    IntroDialogue,
    GroundEyes,
    ShadowsChasing,
    ShadowsConverging,
    BugSpawnDelay,
    SpawningBugs,
    PostBugDelay,
    WaitingBehindLink,
    ChargeEmerging,
    ChargeTell,
    Charging,
    ReturnDelay
}
