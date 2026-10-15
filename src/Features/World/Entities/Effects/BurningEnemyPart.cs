using Godot;

namespace oracleofages;

internal partial class BurningEnemyPart : TransitionOffsetNode2D
{
    private IBurningEnemyTarget _target = null!;
    private EnemyAnimationPlayer _animation = null!;
    private int _targetId;
    private int _oldHealth;
    private int _gravity;
    private int _z;
    private int _speedZ;
    private bool _initialized;
    internal bool Finished { get; private set; }
    internal int Counter { get; private set; }
    internal int AnimationFrame => _animation.FrameIndex;
    internal void Initialize(IBurningEnemyTarget target)
    {
        _target = target;
        var table = GeneratedTable.Load("res://assets/oracle/effects/burning_enemy.tsv",
            new GeneratedTableSchema("PART_BURNING_ENEMY $12", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "frames", "gravity", "animation", "source"], ["source"], headerRequired: true));
        if (table.Rows.Count != 1) throw new System.InvalidOperationException("Expected one PART_BURNING_ENEMY $12 row.");
        var row = table.Rows[0];
        Counter = row.UnsignedDecimal(3);
        _gravity = row.UnsignedDecimal(4);
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{row.RequiredString(0)}.png"),
            [row.RequiredString(5)], row.UnsignedDecimal(1), row.UnsignedDecimal(2));
        _animation.SetAnimation(0);
        Visible = false;
    }
    internal void UpdateFrame()
    {
        if (Finished) return;
        if (!_target.BurnTargetAlive) { Finish(); return; }
        if (!_initialized)
        {
            _initialized = true;
            _targetId = _target.BurnTargetId;
            _oldHealth = _target.BurnHealth;
            _target.BurnHealth = 1;
            // objectTakePosition copies high bytes; the cleared PART keeps
            // its own Z fraction, independently of the stunned enemy.
            _z = _target.BurnZFixed & ~255;
            Visible = true;
        }
        if (_target.BurnTargetId != _targetId) { Finish(); return; }
        if (OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, _gravity))
        {
            int bounce = unchecked((short)-_speedZ) >> 1;
            if ((ushort)bounce <= 0xff80 || bounce == 0) _speedZ = bounce;
        }
        // flame.s writes only the enemy's high Z. objectTakePosition then
        // copies high bytes back without replacing either object's fraction.
        _target.BurnZFixed = (_z & ~255) | (_target.BurnZFixed & 255);
        Position = _target.BurnPosition.Floor();
        if (--Counter == 0)
        {
            _target.BurnHealth = _oldHealth;
            _target.ReleaseBurn();
            Finish();
            return;
        }
        _animation.Advance();
        QueueRedraw();
    }
    private void Finish() { Finished = true; Visible = false; }
    public override void _Draw()
    {
        if (Visible && !Finished)
            DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + new Vector2(0, _z >> 8) + TransitionDrawOffset);
    }
}
