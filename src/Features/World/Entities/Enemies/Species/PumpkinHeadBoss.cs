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
    private Func<RoomEntitySpawn, bool>? _createNativePart;
    internal void BindNativePartAllocation(Func<RoomEntitySpawn, bool> create) => _createNativePart = create;
    private bool TrySpawnNativePart(RoomEntitySpawn spawn, ICollection<RoomEntitySpawn> spawns)
    {
        if (_createNativePart is not null) return _createNativePart(spawn);
        spawns.Add(spawn); return true;
    }

    private readonly EnemyAnimationPlayer _body;
    private readonly EnemyAnimationPlayer _ghost;
    private readonly EnemyAnimationPlayer _head;
    private readonly PumpkinHeadDrawLayer[] _drawLayers;

    internal int BodyDrawPriority => _state == BossState.Stomping
        ? ObjectDrawPriority.InFrontOfLinkZIndex : ObjectDrawPriority.FixedLowPriorityZIndex;
    internal int GhostDrawPriority => _state is BossState.HeadExposed or BossState.Dying
        ? ObjectDrawPriority.BehindLinkZIndex
        : _stompActive && _stompFollowersFalling && !_stompGhostLanded
            ? ObjectDrawPriority.InFrontOfLinkZIndex : ObjectDrawPriority.FixedLowPriorityZIndex;
    internal int HeadDrawPriority => _headHeld || _headThrown
        ? ObjectDrawPriority.InFrontOfLinkZIndex
        : _stompActive && _stompFollowersFalling && !_stompHeadLanded
            ? ObjectDrawPriority.FixedHighPriorityZIndex : ObjectDrawPriority.BehindLinkZIndex;
    private readonly OracleRoomData _room;
    private readonly OracleRuntimeState _memory;
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
    private int _angle = ObjectAngle.Down;
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
    private bool _stompGhostFollowing;
    private bool _stompHeadLanded;
    private int _stompHeadZFixed;
    private int _stompHeadSpeedZ;
    private int _stompGhostZFixed;
    private int _stompGhostSpeedZ;
    private readonly EnemyBehaviorTables _behavior = EnemyBehaviorTables.Shared;

    internal BossState State => _state;
    internal bool InitializationPending => !_initialized;
    internal bool IsDead => _state == BossState.Dead;
    internal bool IntroActive => _state is BossState.WaitingForDoors or BossState.Falling;
    internal bool HeadHeld => _headHeld;
    internal bool HeadThrown => _headThrown;
    internal Vector2I HeadThrowDirection => _throwDirection;
    internal Vector2 HeadPosition => _headPosition;
    internal Vector2 GhostPosition => _ghostPosition;
    internal int HeadZ => _headZ;
    internal int BodyZ => _bodyZFixed >> 8;
    internal bool ShadowOwnerExists =>
        _state is not (BossState.Dying or BossState.Dead);
    internal int BodyHealth => _bodyHealth;
    internal int GhostHealth => _ghostHealth;
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
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int> playSound,
        Func<bool> shuttersClosed,
        Action<int> startScreenShake,
        Action disableLinkCollisionsAndMenu,
        Action restoreRoomMusic,
        int bodyPalette,
        int ghostPalette,
        OracleRuntimeState memory)
    {
        _room = room;
        _memory = memory;
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
        Image source = EnemyVisualSource.LoadComposite(record.Sprites);
        _body = BuildAnimation(source, record, _bodyPalette, 0x0d);
        _ghost = BuildAnimation(source, record, _ghostPalette, 0x0a);
        _head = BuildAnimation(source, record, _headPalette, 0x04);
        _drawLayers = new PumpkinHeadDrawLayer[3];
        // Source allocation order is body, ghost, head; reverse the painting
        // order for equal-priority cells. Each part has its own visible byte.
        for (int part = 2; part >= 0; part--)
        {
            _drawLayers[part] = new PumpkinHeadDrawLayer(this, part)
            {
                Name = part == 0 ? "Body" : part == 1 ? "Ghost" : "Head",
                ZAsRelative = false
            };
            AddChild(_drawLayers[part]);
        }
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns,
        int frameCounter,
        Vector2 linkTarget)
    {
        if (IsDead)
            return;
        _frameCounter = frameCounter & 0xff;
        if (_invincibility > 0)
            _invincibility--;
        if (_ghostInvincibility > 0)
            _ghostInvincibility--;
        if (!_initialized)
        {
            _initialized = true;
            _playSound(SoundId.SndCtrlStopMusic);
            return;
        }
        switch (_state)
        {
            case BossState.WaitingForDoors:
                if (_shuttersClosed())
                {
                    if (!TrySpawnNativePart(new BossShadowSpawn(
                        () => Position,
                        () => BodyZ,
                        () => ShadowOwnerExists,
                        Size: 1,
                        YOffset: 6), spawns)) break;
                    _state = BossState.Falling;
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
                    _playSound(SoundId.SndDoorClose);
                }
                if (!_headLanded)
                {
                    _headLanded = UpdateIntroHeight(
                        ref _introHeadZFixed, ref _introHeadSpeedZ, -16, 0x10);
                    _headZ = _introHeadZFixed >> 8;
                    if (_headLanded)
                        _playSound(SoundId.MusBoss);
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
                if ((_frameCounter & 1) == 0)
                {
                    _stompTimer = Math.Max(0, _stompTimer - 1);
                    if (_stompTimer == 0)
                    {
                        _state = BossState.PreparingStomp;
                        _counter = 60;
                        // countdownUntilStomp falls through the following
                        // schedule routine before returning to state0b.
                        ChooseStompSchedule();
                        FollowBodyHead();
                        break;
                    }
                }
                // pumpkinHead_body_state0b chooses its next action before
                // motion on counter expiry, or after a blocked terrain step.
                _walkCounter = (_walkCounter - 1) & 0xff;
                if (_walkCounter == 0 || !MoveAlongTerrain(ref _precisePosition, 0x14, _angle))
                    ChooseWalkOrFire(linkTarget);
                else
                    _body.Advance();
                Position = _precisePosition.Floor();
                _ghostPosition = CopyPositionHigh(Position, _ghostPosition);
                _head.Advance();
                FollowBodyHead();
                break;
            case BossState.PreparingStomp:
                _counter = (_counter - 1) & 0xff;
                if (_counter == 0)
                    BeginStomp(linkTarget);
                else
                {
                    if ((_counter & 1) != 0)
                        FaceCardinalToward(linkTarget);
                    _head.Advance();
                    FollowBodyHead();
                }
                break;
            case BossState.Stomping:
                bool bodyLanded = OracleObjectMath.UpdateSpeedZ(
                    ref _bodyZFixed, ref _bodySpeedZ, 0x30);
                // state0e applies terrain velocity only while airborne;
                // its landing visit leaves planar words and scratch alone.
                if (!bodyLanded)
                    MoveAlongTerrain(ref _precisePosition, 0x28, _angle);
                Position = _precisePosition.Floor();
                UpdateStompFollowers();
                if (bodyLanded)
                {
                    _counter = _stompsRemaining == 1 ? 30 : 15;
                    _state = BossState.StompLanded;
                    _startScreenShake(20);
                    _playSound(SoundId.SndDoorClose);
                }
                break;
            case BossState.StompLanded:
                UpdateStompFollowers();
                if (--_counter != 0)
                    break;
                _stompsRemaining--;
                if (_stompsRemaining > 0)
                    BeginStomp(linkTarget);
                else
                {
                    _state = BossState.Active;
                    ChooseWalk();
                }
                break;
            case BossState.Firing:
                // Source body wait56 and head wait20 expire together at
                // remaining36; this visit decrements the shared proxy after fire.
                if (!_projectileFired && _counter == 37)
                {
                    _projectileFired = true;
                    bool admitted = TrySpawnNativePart(new PumpkinHeadProjectileSpawn(
                        _headPosition.Floor(), _angle, ZHigh: _headZ), spawns);
                    _head.SetAnimation((_angle >> 2) & 6);
                    if (admitted) _playSound(SoundId.SndVeranFairyAttack);
                }
                if (--_counter == 0)
                {
                    _state = BossState.Active;
                    ChooseWalk();
                }
                break;
            case BossState.HeadExposed:
                if (UpdateExposedHead(player, spawns, linkTarget))
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
                    if (!TrySpawnNativePart(new BossDeathExplosionSpawn(Position, BossId: EnemyId.PumpkinHead), spawns))
                    { _counter = 1; break; }
                    Visible = false;
                    _state = BossState.Dead;
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
        _playSound(SoundId.SndBossDamage);
        if (_bodyHealth == 0)
        {
            ExposeHead();
            spawns?.Add(new PuzzlePuffSpawn(
                Position, SoundId.SndPoof));
        }
        return true;
    }

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
        if (player.OverlapsEnemyCollision(new Rect2(
                center - new Vector2(radiusX, 6), new Vector2(radiusX * 2, 12))))
        {
            player.ApplyEnemyContactDamage(center, damage);
        }
    }

    internal bool TryUseBracelet(
        Player player,
        Vector2I releaseDirection)
    {
        if (_state != BossState.HeadExposed)
            return false;
        if (_headHeld)
        {
            _headHeld = false;
            _headThrown = true;
            _headGrabbable = false;
            _throwDirection = releaseDirection;
            _thrownHeadXFixed = Mathf.RoundToInt(_headPosition.X * 256.0f) +
                player.FacingVector.X * 256;
            _thrownHeadYFixed = Mathf.RoundToInt(_headPosition.Y * 256.0f) +
                player.FacingVector.Y * 256;
            _thrownHeadZFixed = _headZ << 8;
            _headSpeedZ = releaseDirection == Vector2I.Zero
                ? 0
                : _bracelet.InitialSpeedZ;
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
        _drawLayers[0].ZIndex = BodyDrawPriority;
        _drawLayers[1].ZIndex = GhostDrawPriority;
        _drawLayers[2].ZIndex = HeadDrawPriority;
        foreach (var layer in _drawLayers) layer.QueueRedraw();
    }

    internal void DrawPart(Node2D canvas, int part)
    {
        Vector2 offset = TransitionDrawOffset;
        if (_state is BossState.Falling or BossState.Active or
            BossState.PreparingStomp or BossState.Stomping or
            BossState.StompLanded or BossState.Firing)
        {
            int bodyZ = _bodyZFixed >> 8;
            if (part == 0) canvas.DrawTexture(
                _invincibility > 0 && (_frameCounter & 4) == 0
                    ? _body.DamageTexture
                    : _body.CurrentTexture,
                new Vector2(-16, -16 + bodyZ) + offset);
            if (part == 1 && _state == BossState.Falling && _introGhostVisible)
            {
                canvas.DrawTexture(_ghost.CurrentTexture,
                    new Vector2(-16, -16 + _ghostZ) + offset);
            }
            else if (part == 1 && _stompGhostVisible)
            {
                canvas.DrawTexture(_ghost.CurrentTexture,
                    _ghostPosition - Position +
                    new Vector2(-16, -16 + _ghostZ) + offset);
            }
            int headDrawZ = _stompActive ? _headZ : _headZ + bodyZ;
            if (part == 2) canvas.DrawTexture(_head.CurrentTexture,
                _headPosition - Position + new Vector2(
                    -16, -16 + headDrawZ) + offset);
            return;
        }
        if (_state is BossState.HeadExposed or BossState.Regenerating or
            BossState.Dying)
        {
            if (part == 1 && (_state == BossState.Dying ||
                _state == BossState.HeadExposed && _ghostVisible))
            {
                canvas.DrawTexture(
                    _ghostInvincibility > 0 && (_frameCounter & 4) == 0
                        ? _ghost.DamageTexture
                        : _ghost.CurrentTexture,
                    _ghostPosition - Position + new Vector2(-16, -16 + _ghostZ) + offset);
            }
            if (_state is BossState.HeadExposed or BossState.Regenerating)
            {
                if (_state != BossState.Regenerating || _regeneratingBodyVisible)
                {
                    if (part == 0 && _state == BossState.Regenerating && _regeneratingBodyVisible)
                    {
                        canvas.DrawTexture(_body.CurrentTexture,
                            new Vector2(-16, -16) + offset);
                    }
                }
                if (part == 2) canvas.DrawTexture(_head.CurrentTexture,
                    _headPosition - Position + new Vector2(-16, -16 + _headZ) + offset);
            }
        }
    }

    private bool UpdateExposedHead(
        Player player,
        ICollection<RoomEntitySpawn> spawns,
        Vector2 linkTarget)
    {
        if (_exposedPhase == ExposedPhase.LaunchInit)
        {
            _headSpeedZ = -0x120;
            _ghostSpeedZ = -0x120;
            _headAirborne = true;
            _ghostAirborne = true;
            _exposedPhase = ExposedPhase.Airborne;
            return true;
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
            return true;
        }

        if (_headHeld)
        {
            Vector2I offset = CarriedObjectMotion.HeldOffset(player);
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
                    return true;
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
            return true;
        }

        return UpdateExposedGhost(linkTarget);
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

    private bool UpdateExposedGhost(Vector2 linkTarget)
    {
        switch (_exposedPhase)
        {
            case ExposedPhase.GhostFleeInit:
                _counter = 60;
                _ghostSpeedZ = 0;
                // The source away helper XORs the full angle despite its
                // "Cardinal" name, and aims from the ghost's own position.
                _ghostAngle = OracleObjectMovement.Shared.RelativeAngle(_ghostPosition, linkTarget) ^ ObjectAngle.HalfTurn;
                _exposedPhase = ExposedPhase.GhostFlee;
                _ghost.SetAnimation(0x0a);
                return false;

            case ExposedPhase.GhostFlee:
            case ExposedPhase.GhostRoam:
                if (!OracleObjectMath.UpdateSpeedZ(
                    ref _exposedGhostZFixed, ref _ghostSpeedZ, 0x20))
                {
                    _ghostZ = _exposedGhostZFixed >> 8;
                    return false;
                }
                _ghostZ = 0;
                _counter = (_counter - 1) & 0xff;
                if (_counter == 0)
                    _exposedPhase = ExposedPhase.GhostWaitInit;
                // State14 still moves and animates on the expiry visit.
                MoveGhost(_ghostAngle);
                return true;

            case ExposedPhase.GhostWaitInit:
                _counter = 120;
                _exposedPhase = ExposedPhase.GhostWait;
                _ghost.SetAnimation(9);
                return false;

            case ExposedPhase.GhostWait:
                // Counter expiry takes precedence over checking head state02.
                _counter = (_counter - 1) & 0xff;
                if (_counter == 0)
                {
                    _counter = 60;
                    _ghostAngle = _random.Next().Value & 0x1c;
                    _exposedPhase = ExposedPhase.GhostRoam;
                    _ghost.SetAnimation(0x0a);
                    return false;
                }
                if (_headHeld || _headThrown)
                    return true;
                _ghostTarget = _headPosition.Floor();
                _exposedPhase = ExposedPhase.GhostSeek;
                _ghost.SetAnimation(0x0a);
                return false;

            case ExposedPhase.GhostSeek:
                // state17 compares modular HIGH bytes, before objectApplySpeed.
                if ((((int)_ghostPosition.X - (int)_ghostTarget.X + 8) & 255) < 17 &&
                    (((int)_ghostPosition.Y - (int)_ghostTarget.Y + 8) & 255) < 17)
                {
                    // State02 includes both held and released/thrown head.
                    if (!_headHeld && !_headThrown)
                        BeginRegeneration();
                    return false; // Arrival never calls enemyAnimate.
                }
                _ghostAngle = OracleObjectMovement.Shared.RelativeAngle(
                    _ghostPosition, _ghostTarget);
                // pumpkinHead_ghost_state17 uses ecom_moveTowardPosition,
                // which calls objectApplySpeed directly. Its return to the
                // head is not constrained by the fleeing phase's terrain.
                _ghostPosition = NativeObjectMovement.ApplySpeed(_memory,
                    OracleObjectPosition.FromPixels(_ghostPosition), 0x32, _ghostAngle).PrecisePosition;
                return true;
        }
        return true;
    }

    private void MoveGhost(int angle)
    {
        MoveAlongTerrain(ref _ghostPosition, 0x32, angle);
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
        // The ghost remains in state0b while hidden, and copies the body
        // before the later head slot advances the regeneration sequence.
        _ghostPosition = CopyPositionHigh(Position, _ghostPosition);
        _ghostZ = BodyZ;
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
                spawns.Add(new PuzzlePuffSpawn(
                    Position, SoundId.SndPoof));
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
        _walkCounter = _behavior.PumpkinHeadWalkDurations[
            durationRandom & 0x0f].Value;
        _angle = _random.NextCardinalAngle();
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
            _body.SetAnimation(0x0b + (_angle >> 3));
            _head.SetAnimation(((_angle >> 2) & 6) + 1);
            return;
        }
        ChooseWalk();
    }

    private void ChooseStompSchedule()
    {
        OracleRandomResult result = _random.Next();
        _stompTimer =
            _behavior.PumpkinHeadStompTimers[result.High & 0x07].Value;
        _stompsRemaining = 2 + (result.Low & 0x01);
    }

    private bool MoveAlongTerrain(ref Vector2 position, int speed, int angle)
    {
        var walls = EnemyAdjacentWallResolver.Shared.Probe(position, angle,
            point => point.X < 0 || point.Y < 0 || point.X >= _room.Width || point.Y >= _room.Height ||
                _room.IsSolidForEnemyMovement(point, holesAreWalls: true));
        return EnemyTerrainMovement.ApplyGivenAdjacentWalls(ref position, angle,
            NativeObjectMovement.Velocity(_memory, speed, angle), walls, speed);
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
        _stompGhostFollowing = false;
        _stompHeadLanded = false;
        _headPosition = CopyPositionHigh(Position, _headPosition);
        _ghostPosition = CopyPositionHigh(Position, _ghostPosition);
        _headZ = -16;
        _ghostZ = -16;
        _stompHeadZFixed = (_headZ << 8) | (_stompHeadZFixed & 0xff);
        _stompGhostZFixed = (_ghostZ << 8) | (_stompGhostZFixed & 0xff);
        _stompHeadSpeedZ = 0;
        _stompGhostSpeedZ = 0;
        // beginStomp retains the full aim angle; only animation/radius round
        // it to a cardinal direction.
        _angle = OracleObjectMovement.Shared.RelativeAngle(Position, target);
        UpdateFacingAnimations();
    }

    private void UpdateStompFollowers()
    {
        if (!_stompActive)
        {
            _ghostPosition = CopyPositionHigh(Position, _ghostPosition);
            _ghostZ = BodyZ;
            _stompGhostZFixed = (_ghostZ << 8) | (_stompGhostZFixed & 0xff);
            FollowBodyHead();
            _head.Advance();
            return;
        }
        if (!_stompFollowersFalling)
        {
            if (_bodySpeedZ < 0)
            {
                int bodyZ = _bodyZFixed >> 8;
                _headPosition = CopyPositionHigh(Position, _headPosition);
                _ghostPosition = CopyPositionHigh(Position, _ghostPosition);
                _headZ = bodyZ - 16;
                _ghostZ = bodyZ - 16;
                _stompHeadZFixed = (_headZ << 8) | (_stompHeadZFixed & 0xff);
                _stompGhostZFixed = (_ghostZ << 8) | (_stompGhostZFixed & 0xff);
                return;
            }
            _stompFollowersFalling = true;
            _stompGhostVisible = true;
            _stompGhostSpeedZ = 0;
            _stompHeadSpeedZ = 0;
            // Both native follower handlers fall through into their downward
            // state on the apex visit, including its first gravity update.
        }

        if (!_stompGhostLanded)
        {
            _stompGhostLanded = UpdateStompFollower(
                ref _stompGhostZFixed, ref _stompGhostSpeedZ, 0x28);
            _ghostZ = _stompGhostZFixed >> 8;
            if (!_stompGhostLanded)
                _ghostPosition = CopyPositionHigh(Position, _ghostPosition);
        }
        else if (_stompGhostFollowing)
        {
            _ghostPosition = CopyPositionHigh(Position, _ghostPosition);
            _ghostZ = BodyZ;
            _stompGhostZFixed = (_ghostZ << 8) | (_stompGhostZFixed & 0xff);
        }
        else if (_headZ >= -18)
        {
            // The earlier ghost slot observes the preceding head height and
            // changes state now; its first body copy is the following visit.
            _stompGhostFollowing = true;
            _stompGhostVisible = false;
        }
        if (!_stompHeadLanded)
        {
            _stompHeadLanded = UpdateStompFollower(
                ref _stompHeadZFixed, ref _stompHeadSpeedZ, 0x20);
            _headZ = _stompHeadZFixed >> 8;
            if (!_stompHeadLanded)
                _headPosition = CopyPositionHigh(Position, _headPosition);
        }
        else
        {
            FollowBodyHead();
            _head.Advance();
        }
        if (!_stompHeadLanded || !_stompGhostFollowing)
            return;
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
        // The follower handler writes zh=$f0 only. Its own low byte and
        // vertical speed survive until their next explicit source writers.
        zFixed = target | (zFixed & 0xff);
        return true;
    }

    private void FaceCardinalToward(Vector2 target)
    {
        _angle = CardinalAngleToward(target);
        UpdateFacingAnimations();
    }

    private void UpdateFacingAnimations()
    {
        int cardinal = (_angle + 4) & ObjectAngle.CardinalMask;
        _bodyRadiusX = cardinal is ObjectAngle.Right or ObjectAngle.Left ? 8 : 12;
        _body.SetAnimation(0x0b + (cardinal >> 3));
        _head.SetAnimation((cardinal >> 2) & 6);
    }

    private void FollowBodyHead()
    {
        int parameter = Mathf.Clamp(_body.CurrentParameter, 0, 2);
        EnemyBehaviorPair offset =
            _behavior.PumpkinHeadFollowOffsets[parameter];
        _headPosition = CopyPositionHigh(Position + Vector2.Down * offset.First, _headPosition);
        _headZ = offset.Second;
        _stompHeadZFixed = (_headZ << 8) | (_stompHeadZFixed & 0xff);
    }

    private static Vector2 CopyPositionHigh(Vector2 source, Vector2 recipient) =>
        source.Floor() + recipient - recipient.Floor();

    private int CardinalAngleToward(Vector2 target) =>
        (OracleObjectMovement.Shared.RelativeAngle(Position, target) + 4) & ObjectAngle.CardinalMask;

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
        _playSound(SoundId.SndBossDead);
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
        _playSound(SoundId.SndBossDamage);
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

    private EnemyAnimationPlayer BuildAnimation(
        Image source,
        ImportedEnemyDefinition record,
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

internal enum BossState
{
    WaitingForDoors,
    Falling,
    Active,
    PreparingStomp,
    Stomping,
    StompLanded,
    Firing,
    HeadExposed,
    Regenerating,
    Dying,
    Dead
}

internal enum ExposedPhase
{
    LaunchInit,
    Airborne,
    Grabbable,
    GhostFleeInit,
    GhostFlee,
    GhostWaitInit,
    GhostWait,
    GhostRoam,
    GhostSeek
}

internal enum RegenerationPhase
{
    Delay,
    Rising,
    BodyPuff,
    BodyAppearDelay,
    BodyResumeDelay
}
