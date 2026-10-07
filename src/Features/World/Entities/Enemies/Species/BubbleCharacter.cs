using Godot;

namespace oracleofages;

// ENEMY_BUBBLE $15:$00, shared dungeon rail movement and deferred Link jinx.
internal sealed partial class BubbleCharacter : EnemyCharacter
{
    private readonly BubbleBehaviorProfile _behavior = EnemyBehaviorTables.Shared.Bubble;
    private OracleRandom _random = null!;
    private OracleRuntimeState _runtime = null!;
    private EnemyTerrainMovement _movement = null!;
    private bool _linkContactPending;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Angle { get; private set; }
    internal override bool InitializationPending => State == 0;

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position,
        OracleRandom random, OracleRuntimeState runtime)
    {
        Record = record;
        _random = random;
        _runtime = runtime;
        _movement = new(this, room);
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        Visible = false;
    }

    internal void NotifyLinkContact()
    {
        _linkContactPending = true;
        DeferNativeHitStatus();
    }

    internal void UpdateFrame(Player player)
    {
        bool linkHit = _linkContactPending && NativeHitPending;
        if (BeginFrame(continueDuringHitAndKnockback: true)) return;
        _linkContactPending = false;
        // enemyCode15 checks var2a=$80 only after common status dispatch.
        // The Ring is sampled now, rather than during the late collision.
        if (linkHit && !RingEffects.PreventsJinx(player.Inventory))
            _runtime.SetWramByte(WramAddress.wSwordDisabledCounter, (byte)_behavior.JinxUpdates);
        if (State == 0)
        {
            PrepareForScreenTransition();
            return;
        }
        int x = Mathf.FloorToInt(Position.X), y = Mathf.FloorToInt(Position.Y);
        if (((x | y) & _behavior.CenterMask) == 0) ChooseDirection();
        if (!_movement.MoveAtAngle(Angle, _behavior.Speed, allowHoles: false)) ChooseDirection();
        AdvanceAnimation();
    }

    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        _random.Next(); // common var3d initialization
        Angle = _random.Next().Value & _behavior.AngleMask;
        State = 8;
        Visible = true;
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
    }

    private void ChooseDirection()
    {
        // ecom_randomBitwiseAndBCE samples H/L, not its returned A byte.
        OracleRandomResult random = _random.Next();
        if ((random.High & _behavior.TurnChanceMask) == 0)
            Angle = random.Low & _behavior.AngleMask;
    }

    internal override bool TakeSwordHit(Vector2 _, int __) => false;
    internal override bool TakeBurnHit(int _) => false;
}
