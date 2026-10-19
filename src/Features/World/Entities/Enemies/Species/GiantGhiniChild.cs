using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class GiantGhiniChild : EnemyCharacter
{
    internal override bool InitializationPending => !_initialized;


    private GiantGhiniBoss _owner = null!;
    private ChildState _state;
    private int _counter;
    private int _angle;
    private bool _reportedFinished;
    private bool _initialized;
    private bool _intro;
    private int _index;
    private OracleRandom _random = null!;
    private bool _linkContactPending;
    private bool _restrictionActive;
    private bool _slowsLink;
    private int _zHigh;
    private readonly EnemyBehaviorTables _behavior = EnemyBehaviorTables.Shared;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal ChildState State => _state;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && _initialized &&
        _state is ChildState.SpawnDelay or ChildState.Charging;
    internal bool SlowsLink => _slowsLink;
    internal bool DisablesItems => _restrictionActive;
    internal int ZHigh => _zHigh;
    internal void ClearRestrictions()
    {
        _restrictionActive = false;
        _slowsLink = false;
    }

    internal void Initialize(
        ImportedEnemyDefinition record,
        GiantGhiniBoss owner,
        OracleRoomData room,
        int index,
        OracleRandom random)
    {
        _owner = owner;
        Record = record;
        InitializeEnemy(
            owner.Position.Floor(),
            EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.ScreenBoundary);
        _random=random;
        _index=index;
        _intro=owner.State==GiantGhiniBossBossState.IntroWait;
        _state=ChildState.Waiting;
        Visible=false;
    }

    internal void UpdateFrame(
        Player player,
        bool anyButtonJustPressed,
        int frameCounter,
        ICollection<RoomEntitySpawn> spawns)
    {
        // enemyStandardUpdate initializes var3d before enemyCode3f checks
        // relatedObj1.health, including a newly born child of a dying parent.
        if (!_initialized)
        {
            _initialized=true;
            _random.Next();
            if (!_owner.Defeated)
            {
                Vector2 offset=_behavior.GiantGhiniChildSpawnOffsets[_index].Vector;
                Position=new Vector2(
                    (OracleObjectPosition.HighByte(Position.X)+(int)offset.X)&0xff,
                    (OracleObjectPosition.HighByte(Position.Y)+(int)offset.Y)&0xff)+Position-Position.Floor();
                _zHigh=-4;
                if (!_intro)
                {
                    _state=ChildState.SpawnDelay;
                    _counter=30;
                    Visible=true;
                    spawns.Add(new PuzzlePuffSpawn(Position.Floor(),SoundId.SndPoof,ZHigh:_zHigh));
                }
                return;
            }
        }
        if (_owner.Defeated && !IsDead)
        {
            Finish();
            ReportFinished();
            spawns.Add(new EnemyDeathPuffSpawn(
                Position,
                EnemyId: Record.Id,
                DecrementsRoomCount: true));
            return;
        }
        if (IsDead)
        {
            ReportFinished();
            return;
        }
        bool linkContact=_linkContactPending;
        if (BeginFrame(continueDuringHitAndKnockback:linkContact))
        {
            if (IsDead)
                ReportFinished();
            return;
        }
        ClearRestrictions();
        if (linkContact)
        {
            _linkContactPending=false;
            _state=ChildState.Attached;
            _counter=120;
            _zHigh=0;
            _owner.ChildAttached(player);
        }
        switch (_state)
        {
            case ChildState.Waiting:
                if (_owner.State is GiantGhiniBossBossState.Moving or
                    GiantGhiniBossBossState.Charging)
                {
                    _state = ChildState.Charging;
                    _counter = 5;
                    _angle = OracleObjectMovement.Shared.RelativeAngle(
                        Position, player.Position);
                    Visible = true;
                }
                break;
            case ChildState.SpawnDelay:
                _counter=(_counter-1)&0xff;
                if (_counter == 0)
                {
                    _state = ChildState.Charging;
                    _counter = 5;
                    _angle = OracleObjectMovement.Shared.RelativeAngle(
                        Position, player.Position);
                }
                break;
            case ChildState.Charging:
                AdvanceAnimation();
                Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),0x1e,_angle).PrecisePosition;
                _counter=(_counter-1)&0xff;
                if (_counter == 0)
                {
                    _counter = 5;
                    _angle = OracleObjectMath.NudgeAngleTowards(
                        _angle, OracleObjectMovement.Shared.RelativeAngle(
                            Position, player.Position));
                }
                break;
            case ChildState.Attached:
                AdvanceAnimation();
                Position=new Vector2(
                    OracleObjectPosition.HighByte(player.Position.X),
                    OracleObjectPosition.HighByte(player.Position.Y))+Position-Position.Floor();
                _counter=(_counter-1)&0xff;
                if (_counter == 0)
                {
                    _state = ChildState.Fading;
                    _counter = 60;
                    _owner.ChildDetached();
                    break;
                }
                if (anyButtonJustPressed)
                    _counter = _counter >= 3 ? _counter - 3 : 1;
                if ((_counter & 3) == 0)
                    Visible = !Visible;
                _slowsLink = (frameCounter & 1) != 0;
                _restrictionActive=true;
                break;
            case ChildState.Fading:
                AdvanceAnimation();
                Visible = !Visible;
                _counter=(_counter-1)&0xff;
                if (_counter == 0)
                {
                    Finish();
                    ReportFinished();
                }
                break;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawSetTransform(GaleCollisionDisabled ? Vector2.Zero : Vector2.Down * _zHigh);
        base._Draw();
        DrawSetTransform(Vector2.Zero);
    }

    internal void HandleLinkContact(Player player)
    {
        if (_linkContactPending || !OverlapsLink(player.EnemyContactPosition))
            return;
        _linkContactPending=true;
        DeferNativeHitStatus();
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage) =>
        base.TakeSwordHit(sourcePosition, damage);

    private void ReportFinished()
    {
        if (_reportedFinished)
            return;
        _reportedFinished = true;
        _owner.ChildFinished();
    }

}

internal enum ChildState
{
    Waiting,
    SpawnDelay,
    Charging,
    Attached,
    Fading
}
