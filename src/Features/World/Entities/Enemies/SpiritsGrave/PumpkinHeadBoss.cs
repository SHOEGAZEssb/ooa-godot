using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ENEMY_PUMPKIN_HEAD $78: destructible body, timed grabbable head, fleeing
/// ghost with persistent health, sword/thrown-head damage, and body
/// regeneration at the landed head.
/// </summary>
internal sealed partial class PumpkinHeadBoss : TransitionOffsetNode2D
{

    private readonly EnemyAnimationPlayer _body;
    private readonly EnemyAnimationPlayer _ghost;
    private readonly EnemyAnimationPlayer _head;
    private readonly OracleRoomData _room;
    private readonly OracleRandom _random;
    private readonly Action<int> _playSound;
    private readonly Func<bool> _shuttersClosed;
    private readonly Action<int> _startScreenShake;
    private readonly Action _disableLinkCollisionsAndMenu;
    private readonly Action _restoreRoomMusic;
    private readonly BraceletDatabaseRecord _bracelet = new BraceletDatabase().Data;
    private readonly int _bodyPalette;
    private readonly int _ghostPalette;
    private readonly int _headPalette;
    private BossState _state = BossState.WaitingForDoors;
    private Vector2 _precisePosition;
    private Vector2 _headPosition;
    private Vector2 _ghostPosition;
    private Vector2I _throwDirection;
    private int _thrownHeadXFixed;
    private int _thrownHeadYFixed;
    private int _thrownHeadZFixed;
    private int _thrownHeadRadiusX;
    private int _bodyHealth = 8;
    private int _ghostHealth;
    private int _counter;
    private int _walkCounter;
    private int _angle = 0x10;
    private int _invincibility;
    private int _ghostInvincibility;
    private int _headZ = -136;
    private int _headSpeedZ;
    private int _ghostZ = -120;
    private int _ghostSpeedZ;
    private bool _headHeld;
    private bool _headThrown;
    private int _stompTimer;
    private int _stompsRemaining;
    private int _bodyZFixed = -(0x60 << 8);
    private int _bodySpeedZ;
    private int _introHeadZFixed = -(0x88 << 8);
    private int _introHeadSpeedZ;
    private int _introGhostZFixed = -(0x78 << 8);
    private int _introGhostSpeedZ;
    private bool _bodyLanded;
    private bool _headLanded;
    private int _frameCounter;
    private bool _projectileFired;
    private bool _initialized;
    private int _bodyRadiusX = 12;
    private bool _introGhostVisible = true;
    private ExposedPhase _exposedPhase;
    private RegenerationPhase _regenerationPhase;
    private bool _headGrabbable;
    private bool _ghostVisible;
    private bool _regeneratingBodyVisible;
    private bool _headAirborne;
    private bool _ghostAirborne;
    private int _exposedHeadZFixed;
    private int _exposedGhostZFixed;
    private int _ghostAngle;
    private Vector2 _ghostTarget;
    private bool _stompActive;
    private bool _stompFollowersFalling;
    private bool _stompGhostVisible;
    private bool _stompGhostLanded;
    private bool _stompHeadLanded;
    private int _stompHeadZFixed;
    private int _stompHeadSpeedZ;
    private int _stompGhostZFixed;
    private int _stompGhostSpeedZ;

    internal BossState State => _state;
    internal bool IsDead => _state == BossState.Dead;
    internal bool IntroActive => _state is BossState.WaitingForDoors or BossState.Falling;
    internal bool HeadHeld => _headHeld;
    internal bool HeadThrown => _headThrown;
    internal Vector2 HeadPosition => _headPosition;
    internal Vector2 GhostPosition => _ghostPosition;
    internal int HeadZ => _headZ;
    internal int GhostZ => _ghostZ;
    internal int BodyZ => _bodyZFixed >> 8;
    internal bool ShadowOwnerExists =>
        _state is not (BossState.Dying or BossState.Dead);
    internal int BodyHealth => _bodyHealth;
    internal int GhostHealth => _ghostHealth;
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal int BodyPalette => _bodyPalette;
    internal int GhostPalette => _ghostPalette;
    internal int HeadPalette => _headPalette;
    internal int CollisionZ =>
        _state == BossState.HeadExposed ? _ghostZ : BodyZ;
    private bool BodyActive => _state is BossState.Active or
        BossState.PreparingStomp or BossState.Stomping or
        BossState.StompLanded or BossState.Firing;
    internal Rect2 CollisionBounds
    {
        get
        {
            Vector2 center = BodyActive ? Position : _ghostPosition;
            float radiusX = BodyActive ? _bodyRadiusX : 6;
            const float radiusY = 6;
            return new Rect2(
                center - new Vector2(radiusX, radiusY),
                new Vector2(radiusX * 2, radiusY * 2));
        }
    }

    internal PumpkinHeadBoss(
        EnemyRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int> playSound,
        Func<bool> shuttersClosed,
        Action<int> startScreenShake,
        Action disableLinkCollisionsAndMenu,
        Action restoreRoomMusic,
        int bodyPalette,
        int ghostPalette)
    {
        _room = room;
        _random = random;
        _playSound = playSound;
        _shuttersClosed = shuttersClosed;
        _startScreenShake = startScreenShake;
        _disableLinkCollisionsAndMenu = disableLinkCollisionsAndMenu;
        _restoreRoomMusic = restoreRoomMusic;
        _bodyPalette = bodyPalette;
        _ghostPalette = ghostPalette;
        _headPalette = record.Palette;
        _ghostHealth = record.Health;
        Position = position;
        _precisePosition = position;
        _headPosition = position;
        _ghostPosition = position;
        Name = "PumpkinHead";
        ZIndex = 10;
        Image source = EnemyVisualSource.LoadComposite(record.Sprites);
        _body = BuildAnimation(source, record, _bodyPalette, 0x0d);
        _ghost = BuildAnimation(source, record, _ghostPalette, 0x0a);
        _head = BuildAnimation(source, record, _headPalette, 0x04);
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead)
            return;
        _frameCounter = (_frameCounter + 1) & 0xff;
        if (_invincibility > 0)
            _invincibility--;
        if (_ghostInvincibility > 0)
            _ghostInvincibility--;
        if (!_initialized)
        {
            _initialized = true;
            _playSound(OracleSoundEngine.SndCtrlStopMusic);
            return;
        }
        switch (_state)
        {
            case BossState.WaitingForDoors:
                if (_shuttersClosed())
                {
                    _state = BossState.Falling;
                    spawns.Add(new BossShadowSpawn(
                        () => Position,
                        () => BodyZ,
                        () => ShadowOwnerExists,
                        Size: 1,
                        YOffset: 6));
                }
                break;
            case BossState.Falling:
                bool headWasLanded = _headLanded;
                if (!_bodyLanded && OracleObjectMath.UpdateSpeedZ(
                    ref _bodyZFixed, ref _bodySpeedZ, 0x10))
                {
                    _bodyLanded = true;
                    _counter = 30;
                    _startScreenShake(30);
                    _playSound(OracleSoundEngine.SndDoorClose);
                }
                if (!_headLanded)
                {
                    _headLanded = UpdateIntroHeight(
                        ref _introHeadZFixed, ref _introHeadSpeedZ, -16, 0x10);
                    _headZ = _introHeadZFixed >> 8;
                    if (_headLanded)
                        _playSound(OracleSoundEngine.MusBoss);
                }
                if (_ghostZ < -16)
                {
                    UpdateIntroHeight(
                        ref _introGhostZFixed, ref _introGhostSpeedZ, -16, 0x10);
                    _ghostZ = _introGhostZFixed >> 8;
                }
                if (headWasLanded)
                    _introGhostVisible = false;
                if (!_bodyLanded || !_headLanded || --_counter != 0)
                    break;
                _state = BossState.Active;
                _ghostZ = 0;
                ChooseStompSchedule();
                ChooseWalk();
                break;
            case BossState.Active:
                if ((_frameCounter & 1) == 0 && --_stompTimer == 0)
                {
                    _state = BossState.PreparingStomp;
                    _counter = 60;
                    break;
                }
                _precisePosition += OracleObjectMath.VectorFromAngle32(_angle) * 0.5f;
                _precisePosition = new Vector2(
                    Mathf.Clamp(_precisePosition.X, 20, _room.Width - 20),
                    Mathf.Clamp(_precisePosition.Y, 24, _room.Height - 20));
                Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                _ghostPosition = Position;
                if (--_walkCounter == 0)
                {
                    ChooseWalkOrFire(player.Position);
                    FollowBodyHead();
                    break;
                }
                _body.Advance();
                _head.Advance();
                FollowBodyHead();
                break;
            case BossState.PreparingStomp:
                if ((_counter & 1) != 0)
                    FaceCardinalToward(player.Position);
                if (--_counter == 0)
                    BeginStomp(player.Position);
                else
                {
                    _head.Advance();
                    FollowBodyHead();
                }
                break;
            case BossState.Stomping:
                _precisePosition += OracleObjectMath.VectorFromAngle32(_angle);
                _precisePosition = new Vector2(
                    Mathf.Clamp(_precisePosition.X, 20, _room.Width - 20),
                    Mathf.Clamp(_precisePosition.Y, 24, _room.Height - 20));
                Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                _headPosition = Position;
                _ghostPosition = Position;
                bool bodyLanded = OracleObjectMath.UpdateSpeedZ(
                    ref _bodyZFixed, ref _bodySpeedZ, 0x30);
                UpdateStompFollowers();
                if (bodyLanded)
                {
                    _stompsRemaining--;
                    _counter = _stompsRemaining == 0 ? 30 : 15;
                    _state = BossState.StompLanded;
                    _startScreenShake(20);
                    _playSound(OracleSoundEngine.SndDoorClose);
                }
                break;
            case BossState.StompLanded:
                UpdateStompFollowers();
                if (--_counter != 0)
                    break;
                if (_stompsRemaining > 0)
                    BeginStomp(player.Position);
                else
                {
                    ChooseStompSchedule();
                    _state = BossState.Active;
                    ChooseWalk();
                }
                break;
            case BossState.Firing:
                if (!_projectileFired && _counter == 36)
                {
                    _projectileFired = true;
                    Vector2 origin = _headPosition + new Vector2(0, _headZ) +
                        ProjectileOriginOffset(_angle);
                    int[] offsets = { 0, -2, 2 };
                    foreach (int offset in offsets)
                    {
                        spawns.Add(new PumpkinHeadProjectileSpawn(
                            origin,
                            (_angle + offset) & 0x1f));
                    }
                    _head.SetAnimation((_angle >> 2) & 6);
                    _playSound(OracleSoundEngine.SndVeranFairyAttack);
                }
                if (--_counter == 0)
                {
                    _state = BossState.Active;
                    ChooseWalk();
                }
                break;
            case BossState.HeadExposed:
                UpdateExposedHead(player, spawns);
                _ghost.Advance();
                _head.Advance();
                break;
            case BossState.Regenerating:
                UpdateRegeneration(spawns);
                break;
            case BossState.Dying:
                Visible = (_counter & 1) != 0;
                if (--_counter == 0)
                {
                    Visible = false;
                    _state = BossState.Dead;
                    spawns.Add(new BossDeathExplosionSpawn(Position, BossId: 0x78));
                    _restoreRoomMusic();
                }
                break;
        }
        QueueRedraw();
    }

    internal bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn>? spawns = null)
    {
        if (_state == BossState.HeadExposed && _ghostVisible)
            return ApplyGhostHit(hitbox, damage);
        if (!BodyActive || _invincibility > 0 ||
            !hitbox.Intersects(CollisionBounds))
        {
            return false;
        }
        _bodyHealth = Math.Max(0, _bodyHealth - Math.Max(1, damage));
        _invincibility = 0x20;
        _playSound(OracleSoundEngine.SndBossDamage);
        if (_bodyHealth == 0)
        {
            ExposeHead();
            spawns?.Add(new KillEnemyPuffSpawn(Position));
        }
        return true;
    }

    internal bool ApplyBurnHit(int damage) =>
        ApplySwordHit(CollisionBounds, Position, damage);

    internal void HandleLinkContact(Player player, int damage)
    {
        if (_state is not (BossState.Active or BossState.PreparingStomp or
            BossState.Stomping or BossState.StompLanded or BossState.Firing or
            BossState.HeadExposed))
            return;
        if (_state == BossState.HeadExposed && !_ghostVisible)
            return;
        Vector2 center = _state == BossState.HeadExposed ? _ghostPosition : Position;
        float radiusX = _state == BossState.HeadExposed ? 6 : _bodyRadiusX;
        if (Mathf.Abs(player.Position.X - center.X) < radiusX + 6 &&
            Mathf.Abs(player.Position.Y - center.Y) < 12)
        {
            player.ApplyEnemyContactDamage(center, damage);
        }
    }

    internal bool TryUseBracelet(Player player)
    {
        if (_state != BossState.HeadExposed)
            return false;
        if (_headHeld)
        {
            _headHeld = false;
            _headThrown = true;
            _headGrabbable = false;
            _throwDirection = player.FacingVector;
            _thrownHeadXFixed = Mathf.RoundToInt(_headPosition.X * 256.0f) +
                _throwDirection.X * 256;
            _thrownHeadYFixed = Mathf.RoundToInt(_headPosition.Y * 256.0f) +
                _throwDirection.Y * 256;
            _thrownHeadZFixed = _headZ << 8;
            _headSpeedZ = _bracelet.InitialSpeedZ;
            _thrownHeadRadiusX = _throwDirection.X == 0 ? 8 : 6;
            SyncThrownHeadPosition();
            player.EndCarriedObjectPose();
            return true;
        }
        if (!_headGrabbable || _headThrown || player.IsCarryingObject ||
            player.CutsceneControlled)
            return false;
        Vector2 point = player.Position + (Vector2)player.FacingVector * 6.0f;
        Vector2 delta = _headPosition - point;
        if (Mathf.Abs(delta.X) >= 13 || Mathf.Abs(delta.Y) >= 13)
            return false;
        _headHeld = true;
        _headGrabbable = false;
        // pumpkinHead_state_grabbed puts the ghost at Z=-8 with zero
        // vertical speed before its run-away/fall state begins.
        _ghostZ = -8;
        _ghostSpeedZ = 0;
        _ghostInvincibility = 12;
        _exposedGhostZFixed = _ghostZ << 8;
        _ghostVisible = true;
        _exposedPhase = ExposedPhase.GhostFleeInit;
        player.BeginCarriedObjectPose();
        return true;
    }

    public override void _Draw()
    {
        Vector2 offset = TransitionDrawOffset;
        if (_state is BossState.Falling or BossState.Active or
            BossState.PreparingStomp or BossState.Stomping or
            BossState.StompLanded or BossState.Firing)
        {
            int bodyZ = _bodyZFixed >> 8;
            DrawTexture(
                _invincibility > 0 && (_frameCounter & 4) == 0
                    ? _body.DamageTexture
                    : _body.CurrentTexture,
                new Vector2(-16, -16 + bodyZ) + offset);
            if (_state == BossState.Falling && _introGhostVisible)
            {
                DrawTexture(_ghost.CurrentTexture,
                    new Vector2(-16, -16 + _ghostZ) + offset);
            }
            else if (_stompGhostVisible)
            {
                DrawTexture(_ghost.CurrentTexture,
                    _ghostPosition - Position +
                    new Vector2(-16, -16 + _ghostZ) + offset);
            }
            int headDrawZ = _stompActive ? _headZ : _headZ + bodyZ;
            DrawTexture(_head.CurrentTexture,
                _headPosition - Position + new Vector2(
                    -16, -16 + headDrawZ) + offset);
            return;
        }
        if (_state is BossState.HeadExposed or BossState.Regenerating or
            BossState.Dying)
        {
            if (_state == BossState.Dying ||
                _state == BossState.HeadExposed && _ghostVisible)
            {
                DrawTexture(
                    _ghostInvincibility > 0 && (_frameCounter & 4) == 0
                        ? _ghost.DamageTexture
                        : _ghost.CurrentTexture,
                    _ghostPosition - Position + new Vector2(-16, -16 + _ghostZ) + offset);
            }
            if (_state is BossState.HeadExposed or BossState.Regenerating)
            {
                if (_state != BossState.Regenerating || _regeneratingBodyVisible)
                {
                    if (_state == BossState.Regenerating && _regeneratingBodyVisible)
                    {
                        DrawTexture(_body.CurrentTexture,
                            new Vector2(-16, -16) + offset);
                    }
                }
                DrawTexture(_head.CurrentTexture,
                    _headPosition - Position + new Vector2(-16, -16 + _headZ) + offset);
            }
        }
    }

    private void UpdateExposedHead(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_exposedPhase == ExposedPhase.LaunchInit)
        {
            _headSpeedZ = -0x120;
            _ghostSpeedZ = -0x120;
            _headAirborne = true;
            _ghostAirborne = true;
            _exposedPhase = ExposedPhase.Airborne;
            return;
        }
        if (_exposedPhase == ExposedPhase.Airborne)
        {
            if (_headAirborne)
            {
                _headAirborne = !OracleObjectMath.UpdateSpeedZ(
                    ref _exposedHeadZFixed, ref _headSpeedZ, 0x20);
                _headZ = _exposedHeadZFixed >> 8;
                if (!_headAirborne)
                {
                    _headZ = 0;
                    _headGrabbable = true;
                    _counter = 120;
                    _exposedPhase = ExposedPhase.Grabbable;
                }
            }
            if (_ghostAirborne)
            {
                _ghostAirborne = !OracleObjectMath.UpdateSpeedZ(
                    ref _exposedGhostZFixed, ref _ghostSpeedZ, 0x28);
                _ghostZ = _exposedGhostZFixed >> 8;
            }
            return;
        }

        if (_headHeld)
        {
            Vector2I offset = player.BraceletEntityOffset ??
                new Vector2I(
                    0,
                    player.CarriedObjectAnimationFrame == 0 &&
                        player.FacingVector.X != 0 ? -14 : -13);
            _headPosition =
                player.Position + new Vector2(offset.X, 0);
            _headZ = offset.Y;
        }
        else if (_headThrown)
        {
            int lateralStep = _bracelet.SpeedRaw * 256 / 40;
            _thrownHeadXFixed += _throwDirection.X * lateralStep;
            _thrownHeadYFixed += _throwDirection.Y * lateralStep;
            bool landed = OracleObjectMath.UpdateSpeedZ(
                ref _thrownHeadZFixed,
                ref _headSpeedZ,
                _bracelet.Gravity);
            SyncThrownHeadPosition();

            // The invisible ITEM_BRACELET proxy copies the head's direction-
            // dependent radii, then collision effect $21 applies its three
            // points of damage to the ghost. Z is a separate one-byte strict
            // seven-pixel test; it is never folded into the planar Y value.
            Rect2 thrownBounds = new(
                _headPosition - new Vector2(
                    _thrownHeadRadiusX, _bracelet.RadiusY),
                new Vector2(
                    _thrownHeadRadiusX * 2, _bracelet.RadiusY * 2));
            if (_ghostVisible &&
                RoomEntityManager.ObjectCollisionZOverlaps(
                    _ghostZ, _headZ, _bracelet.CollisionZRadius) &&
                thrownBounds.Intersects(CollisionBounds))
            {
                ApplyGhostHit(thrownBounds, _bracelet.Damage);
                if (_state == BossState.Dying)
                    return;
            }
            if (landed)
            {
                _headZ = 0;
                _headThrown = false;
                _headGrabbable = true;
                _exposedPhase = ExposedPhase.GhostWaitInit;
            }
        }
        else if (_exposedPhase == ExposedPhase.Grabbable && --_counter == 0)
        {
            BeginRegeneration();
            return;
        }

        UpdateExposedGhost(player);
    }

    private void ExposeHead()
    {
        _state = BossState.HeadExposed;
        _bodyHealth = 8;
        _ghostPosition = Position;
        _ghostZ = 0;
        _ghostSpeedZ = 0;
        _headPosition = Position;
        _headZ = -16;
        _exposedHeadZFixed = _headZ << 8;
        _exposedGhostZFixed = 0;
        _headHeld = false;
        _headThrown = false;
        _headGrabbable = false;
        _ghostVisible = false;
        _exposedPhase = ExposedPhase.LaunchInit;
    }

    private void UpdateExposedGhost(Player player)
    {
        switch (_exposedPhase)
        {
            case ExposedPhase.GhostFleeInit:
                _counter = 60;
                _ghostAngle = (CardinalAngleToward(player.Position) + 0x10) & 0x18;
                _exposedPhase = ExposedPhase.GhostFlee;
                return;

            case ExposedPhase.GhostFlee:
                if (_ghostZ < 0)
                {
                    if (!OracleObjectMath.UpdateSpeedZ(
                        ref _exposedGhostZFixed, ref _ghostSpeedZ, 0x20))
                    {
                        _ghostZ = _exposedGhostZFixed >> 8;
                        return;
                    }
                    _ghostZ = 0;
                }
                MoveGhost(_ghostAngle);
                if (--_counter == 0)
                    _exposedPhase = ExposedPhase.GhostWaitInit;
                return;

            case ExposedPhase.GhostWaitInit:
                _counter = 120;
                _exposedPhase = ExposedPhase.GhostWait;
                return;

            case ExposedPhase.GhostWait:
                if (!_headHeld)
                {
                    _ghostTarget = _headPosition;
                    _exposedPhase = ExposedPhase.GhostSeek;
                    return;
                }
                if (--_counter == 0)
                {
                    _counter = 60;
                    _ghostAngle = _random.Next().Value & 0x1c;
                    _exposedPhase = ExposedPhase.GhostRoam;
                }
                return;

            case ExposedPhase.GhostRoam:
                MoveGhost(_ghostAngle);
                if (--_counter == 0)
                    _exposedPhase = ExposedPhase.GhostWaitInit;
                return;

            case ExposedPhase.GhostSeek:
                Vector2 delta = _ghostTarget - _ghostPosition;
                if (Mathf.Abs(delta.X) < 9 && Mathf.Abs(delta.Y) < 9)
                {
                    if (!_headHeld)
                        BeginRegeneration();
                    return;
                }
                int angle = OracleObjectMath.AngleToward(_ghostPosition, _ghostTarget);
                MoveGhost(angle);
                return;
        }
    }

    private void MoveGhost(int angle)
    {
        Vector2 next = _ghostPosition +
            OracleObjectMath.VectorFromAngle32(angle) * 1.25f;
        if (!_room.IsSolid(next))
            _ghostPosition = next;
        ClampGhost();
    }

    private void BeginRegeneration()
    {
        _headHeld = false;
        _headThrown = false;
        _headGrabbable = false;
        _ghostVisible = false;
        _regeneratingBodyVisible = false;
        _state = BossState.Regenerating;
        _regenerationPhase = RegenerationPhase.Delay;
        _counter = 16;
        _head.SetAnimation(8);
    }

    private void UpdateRegeneration(ICollection<RoomEntitySpawn> spawns)
    {
        switch (_regenerationPhase)
        {
            case RegenerationPhase.Delay:
                if (--_counter != 0)
                    return;
                _headSpeedZ = -0x200;
                _exposedHeadZFixed = _headZ << 8;
                _head.SetAnimation(4);
                _regenerationPhase = RegenerationPhase.Rising;
                return;

            case RegenerationPhase.Rising:
                OracleObjectMath.UpdateSpeedZ(
                    ref _exposedHeadZFixed, ref _headSpeedZ, 0x20);
                _headZ = _exposedHeadZFixed >> 8;
                if (_headZ >= -15)
                    return;
                _headZ = -16;
                // objectCopyPosition copies the active head's X/Y/Z into the
                // related body. The following source write resets the body's
                // Z only, so the regenerated body belongs at the head.
                Position = _headPosition;
                _precisePosition = Position;
                _bodyZFixed = 0;
                _regenerationPhase = RegenerationPhase.BodyPuff;
                return;

            case RegenerationPhase.BodyPuff:
                spawns.Add(new KillEnemyPuffSpawn(Position));
                _counter = 8;
                _regenerationPhase = RegenerationPhase.BodyAppearDelay;
                return;

            case RegenerationPhase.BodyAppearDelay:
                if (--_counter != 0)
                    return;
                _regeneratingBodyVisible = true;
                _counter = 30;
                _body.SetAnimation(0x0d);
                _regenerationPhase = RegenerationPhase.BodyResumeDelay;
                return;

            case RegenerationPhase.BodyResumeDelay:
                if (--_counter != 0)
                    return;
                _regeneratingBodyVisible = false;
                _state = BossState.Active;
                ChooseStompSchedule();
                ChooseWalk();
                return;
        }
    }

    private void ChooseWalk()
    {
        byte durationRandom = _random.Next().Value;
        int[] durations = { 30, 30, 60, 60, 60, 60, 60, 90,
            90, 90, 90, 90, 90, 120, 120, 120 };
        _walkCounter = durations[durationRandom & 0x0f];
        _angle = _random.Next().Value & 0x18;
        UpdateFacingAnimations();
    }

    private void ChooseWalkOrFire(Vector2 target)
    {
        int targetAngle = CardinalAngleToward(target);
        if (_angle == targetAngle && _random.Next().Value >= 0x40)
        {
            _state = BossState.Firing;
            _counter = 56;
            _projectileFired = false;
            _head.SetAnimation(((_angle >> 2) & 6) + 1);
            return;
        }
        ChooseWalk();
    }

    private void ChooseStompSchedule()
    {
        OracleRandomResult result = _random.Next();
        int[] timers = { 90, 120, 120, 120, 150, 150, 150, 180 };
        _stompTimer = timers[result.High & 0x07];
        _stompsRemaining = 2 + (result.Low & 0x01);
    }

    private void BeginStomp(Vector2 target)
    {
        _state = BossState.Stomping;
        _bodyZFixed = 0;
        _bodySpeedZ = -0x3a0;
        _stompActive = true;
        _stompFollowersFalling = false;
        _stompGhostVisible = false;
        _stompGhostLanded = false;
        _stompHeadLanded = false;
        _headPosition = Position;
        _ghostPosition = Position;
        _headZ = -16;
        _ghostZ = -16;
        _stompHeadZFixed = _headZ << 8;
        _stompGhostZFixed = _ghostZ << 8;
        _stompHeadSpeedZ = 0;
        _stompGhostSpeedZ = 0;
        FaceCardinalToward(target);
    }

    private void UpdateStompFollowers()
    {
        if (!_stompActive)
            return;
        if (!_stompFollowersFalling)
        {
            if (_bodySpeedZ < 0)
            {
                int bodyZ = _bodyZFixed >> 8;
                _headPosition = Position;
                _ghostPosition = Position;
                _headZ = bodyZ - 16;
                _ghostZ = bodyZ - 16;
                _stompHeadZFixed = _headZ << 8;
                _stompGhostZFixed = _ghostZ << 8;
                return;
            }
            _stompFollowersFalling = true;
            _stompGhostVisible = true;
            return;
        }

        if (!_stompGhostLanded)
        {
            _stompGhostLanded = UpdateStompFollower(
                ref _stompGhostZFixed, ref _stompGhostSpeedZ, 0x28);
            _ghostZ = _stompGhostZFixed >> 8;
            _ghostPosition = Position;
        }
        if (!_stompHeadLanded)
        {
            _stompHeadLanded = UpdateStompFollower(
                ref _stompHeadZFixed, ref _stompHeadSpeedZ, 0x20);
            _headZ = _stompHeadZFixed >> 8;
            _headPosition = Position;
        }
        if (_stompGhostLanded && _headZ >= -18)
            _stompGhostVisible = false;
        if (!_stompHeadLanded)
            return;
        _headZ = -16;
        _headPosition = Position;
        _stompGhostVisible = false;
        _stompActive = false;
    }

    private static bool UpdateStompFollower(
        ref int zFixed,
        ref int speedZ,
        int gravity)
    {
        zFixed += speedZ;
        speedZ += gravity;
        int target = -16 << 8;
        if (zFixed < target)
            return false;
        zFixed = target;
        speedZ = 0;
        return true;
    }

    private void FaceCardinalToward(Vector2 target)
    {
        _angle = CardinalAngleToward(target);
        UpdateFacingAnimations();
    }

    private void UpdateFacingAnimations()
    {
        _bodyRadiusX = _angle is 0x08 or 0x18 ? 8 : 12;
        _body.SetAnimation(0x0b + (_angle >> 3));
        _head.SetAnimation((_angle >> 2) & 6);
    }

    private void FollowBodyHead()
    {
        int parameter = Mathf.Clamp(_body.CurrentParameter, 0, 2);
        int[] yOffsets = { 0, 1, 0 };
        int[] zOffsets = { -16, -16, -17 };
        _headPosition = Position + Vector2.Down * yOffsets[parameter];
        _headZ = zOffsets[parameter];
    }

    private int CardinalAngleToward(Vector2 target) =>
        (OracleObjectMath.AngleToward(Position, target) + 4) & 0x18;

    private static Vector2 ProjectileOriginOffset(int angle) => angle switch
    {
        0x00 => new Vector2(0, -4),
        0x08 => new Vector2(4, 2),
        0x10 => new Vector2(0, 4),
        _ => new Vector2(-4, 2)
    };

    private static bool UpdateIntroHeight(
        ref int zFixed,
        ref int speedZ,
        int targetHeight,
        int gravity)
    {
        zFixed += speedZ;
        speedZ += gravity;
        int targetFixed = targetHeight << 8;
        if (zFixed < targetFixed)
            return false;
        zFixed = targetFixed;
        speedZ = 0;
        return true;
    }

    private void BeginDeath()
    {
        _headHeld = false;
        _headThrown = false;
        _counter = 120;
        _state = BossState.Dying;
        _disableLinkCollisionsAndMenu();
        _playSound(OracleSoundEngine.SndBossDead);
    }

    private bool ApplyGhostHit(Rect2 hitbox, int damage)
    {
        if (_ghostInvincibility > 0 ||
            !hitbox.Intersects(CollisionBounds))
        {
            return false;
        }

        _ghostHealth = Math.Max(0, _ghostHealth - Math.Max(1, damage));
        _ghostInvincibility = 0x20;
        _playSound(OracleSoundEngine.SndBossDamage);
        if (_ghostHealth == 0)
            BeginDeath();
        return true;
    }

    private void SyncThrownHeadPosition()
    {
        _headPosition = new Vector2(
            _thrownHeadXFixed >> 8,
            _thrownHeadYFixed >> 8);
        _headZ = _thrownHeadZFixed >> 8;
    }

    private void ClampGhost() => _ghostPosition = new Vector2(
        Mathf.Clamp(_ghostPosition.X, 16, _room.Width - 16),
        Mathf.Clamp(_ghostPosition.Y, 16, _room.Height - 16));

    private EnemyAnimationPlayer BuildAnimation(
        Image source,
        EnemyRecord record,
        int palette,
        int initial)
    {
        var player = new EnemyAnimationPlayer(this, record.Animations.Length);
        player.Load(
            source,
            record.Animations,
            record.TileBase,
            palette,
            palette == 5 ? 2 : 5);
        player.SetAnimation(initial);
        return player;
    }
}
