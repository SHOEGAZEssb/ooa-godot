using Godot;

namespace oracleofages;

internal partial class ColorChangingGelCharacter : EnemyCharacter
{
    private static readonly Vector2I[] HopOffsets =
    [
        new(-16, -16), new(0, -16), new(16, -16),
        new(-16, 0), new(16, 0),
        new(-16, 16), new(0, 16), new(16, 16)
    ];

    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private EnemyTerrainMovement _movement = null!;
    private ColorChangingGelState _state;
    private int _counter;
    private int _colorCounter;
    private int _zFixed;
    private int _speedZ;
    private Vector2 _target;
    private byte _storedTile;
    private int _color;
    private bool _immune;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal ColorChangingGelState State => _state;
    internal int Color => _color;
    internal bool Immune => _immune;
    internal int ZHigh => _zFixed >> 8;
    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + ZHigh);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _state = ColorChangingGelState.Uninitialized;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record),
            initialAnimation: 3);
        ConfigureHazards(room, zPosition: () => ZHigh);
    }

    internal void UpdateFrame()
    {
        if (IsDead || BeginFrame())
            return;
        if (CheckHazards())
            return;
        UpdateColor();

        switch (_state)
        {
            case ColorChangingGelState.Uninitialized:
                _state = ColorChangingGelState.Waiting;
                _counter = WingDungeonEnemyBehavior.Shared[
                    "color-gel-wait-frames"];
                _colorCounter = 0;
                _storedTile = _room.GetMetatile(Position);
                _color = 2;
                UpdateImmunity();
                Visible = true;
                return;

            case ColorChangingGelState.Waiting:
                if (--_counter != 0)
                    return;
                Vector2I offset = HopOffsets[
                    (_random.Next().Value & 0x0e) >> 1];
                Vector2 target = Position + (Vector2)offset;
                if (_room.IsSolid(target))
                {
                    _counter = 1;
                    return;
                }
                _target = target;
                _state = ColorChangingGelState.Preparing;
                _counter = WingDungeonEnemyBehavior.Shared[
                    "color-gel-hop-delay"];
                _speedZ = WingDungeonEnemyBehavior.Shared[
                    "color-gel-initial-speed-z"];
                SetAnimation(2);
                return;

            case ColorChangingGelState.Preparing:
                if (--_counter != 0)
                {
                    AdvanceAnimation();
                    return;
                }
                _state = ColorChangingGelState.Hopping;
                int angle = OracleObjectMovement.Shared.RelativeAngle(
                    Position, _target);
                SetAnimation((angle & 0x10) >> 4);
                return;

            case ColorChangingGelState.Hopping:
                bool landed = OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed,
                    ref _speedZ,
                    WingDungeonEnemyBehavior.Shared["color-gel-gravity"]);
                if (!landed)
                {
                    int angleToTarget =
                        OracleObjectMovement.Shared.RelativeAngle(
                            Position, _target);
                    _movement.MoveAtAngle(
                        angleToTarget,
                        WingDungeonEnemyBehavior.Shared[
                            "color-gel-speed-raw"],
                        allowHoles: false);
                    return;
                }
                Position = new Vector2(
                    Mathf.Floor(Position.X / 16.0f) * 16.0f + 8.0f,
                    Mathf.Floor(Position.Y / 16.0f) * 16.0f + 8.0f);
                _state = ColorChangingGelState.Waiting;
                _counter = WingDungeonEnemyBehavior.Shared[
                    "color-gel-wait-frames"];
                SetAnimation(3);
                return;
        }
    }

    internal override bool TakeSwordHit(Vector2 _, int damage)
    {
        if (_immune || IsDead || InvincibilityCounter != 0)
            return false;
        return ApplyDamage(damage, invincibilityFrames: 0);
    }

    internal override bool TakeBurnHit(int _) => false;

    private void UpdateColor()
    {
        if (_zFixed < 0)
            return;
        if (_colorCounter > 0 && --_colorCounter == 1)
            _color = FloorColor(_storedTile);
        if (_colorCounter != 0)
        {
            UpdateImmunity();
            return;
        }
        UpdateImmunity();
        _storedTile = _room.GetMetatile(Position);
        _colorCounter = WingDungeonEnemyBehavior.Shared[
            "color-gel-color-delay"];
    }

    private void UpdateImmunity()
    {
        byte tile = _room.GetMetatile(Position);
        _immune = tile != 0x29 && FloorColor(tile) == _color;
    }

    private static int FloorColor(byte tile) => tile switch
    {
        0x9d or 0xad => 2,
        0x9e or 0xae => 6,
        0x9f or 0xaf => 1,
        _ => 2
    };
}

internal enum ColorChangingGelState
{
    Uninitialized,
    Waiting = 8,
    Preparing,
    Hopping
}
