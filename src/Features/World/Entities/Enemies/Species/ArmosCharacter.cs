using Godot;
using System;

namespace oracleofages;

internal partial class ArmosCharacter : EnemyCharacter
{
    private readonly ArmosBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Armos;
    private OracleRuntimeState _runtime = null!;
    private OracleRoomData _room = null!;
    private Action _roomTileChanged = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private int _replacementTile;
    private int _counter;
    private int _angle;
    private ArmosState _state;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal ArmosState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal int SpeedRaw => _behavior.SpeedRaw;
    internal int ActiveCollisionMode => _behavior.ActiveCollisionMode;
    internal override bool CollisionEnabled =>
        (_state is ArmosState.ChoosingDirection or ArmosState.Moving) &&
        base.CollisionEnabled;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        int replacementTile,
        OracleRuntimeState runtime,
        OracleRandom random,
        Action roomTileChanged)
    {
        Record = record;
        EnemyCharacterConfiguration configuration =
            EnemyCharacterConfiguration.FromImported(record) with
            {
                Palette = _behavior.OamPalette,
                DamagePalette = 2
            };
        InitializeEnemy(position, configuration);
        _room = room;
        _replacementTile = replacementTile;
        _runtime = runtime;
        _random = random;
        _roomTileChanged = roomTileChanged;
        _movement = new EnemyTerrainMovement(this, room);
        ConfigureHazards(room);
        Visible = false;
        _state = ArmosState.Uninitialized;
    }

    internal void UpdateFrame()
    {
        if (IsDead)
            return;
        if (BeginFrame())
            return;
        if (CheckHazards())
            return;

        switch (_state)
        {
            case ArmosState.Uninitialized:
                _state = ArmosState.Waiting;
                return;

            case ArmosState.Waiting:
                if (_runtime.ReadWramByte(
                        OracleRuntimeState.ArmosTriggerAddress) != 0)
                {
                    _state = ArmosState.Activating;
                }
                return;

            case ArmosState.Activating:
                _state = ArmosState.Flickering;
                _counter = _behavior.ActivationCounter;
                Position += new Vector2(0, _behavior.ActivationYOffset);
                Visible = true;
                QueueRedraw();
                return;

            case ArmosState.Flickering:
                _counter--;
                if (_counter != 0)
                {
                    Visible = !Visible;
                    QueueRedraw();
                    return;
                }
                _state = ArmosState.ChoosingDirection;
                SetCollisionRadii(
                    _behavior.ActiveCollisionRadius,
                    _behavior.ActiveCollisionRadius);
                _room.SetPositionTileAndCollision(
                    Position,
                    (byte)_replacementTile,
                    null,
                    animationTick: 0);
                _roomTileChanged();
                Visible = true;
                QueueRedraw();
                return;

            case ArmosState.ChoosingDirection:
                // ecom_setRandomCardinalAngle masks A, the returned RNG byte;
                // H is only the intermediate 16-bit multiplication high byte.
                _angle = _random.Next().Value & 0x18;
                _counter = _behavior.MovementCounter;
                _state = ArmosState.Moving;
                goto case ArmosState.Moving;

            case ArmosState.Moving:
                _counter--;
                if (_counter != 0)
                {
                    _movement.MoveUsingAdjacentWalls(
                        _angle,
                        _behavior.SpeedRaw,
                        allowHoles: false,
                        topDown: true);
                }
                else
                {
                    _state = ArmosState.ChoosingDirection;
                }
                AdvanceAnimation();
                return;
        }
    }

    internal bool TakeArmoredHit() => AcceptArmoredSwordHit(0x1c);

    internal bool TakeDamageWithoutKnockback(Vector2 sourcePosition, int damage) =>
        TakeSwordHit(sourcePosition, damage);
}

internal enum ArmosState
{
    Uninitialized,
    Waiting,
    Activating,
    Flickering,
    ChoosingDirection,
    Moving
}
