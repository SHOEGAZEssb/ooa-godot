using Godot;
using System;

namespace oracleofages;

// object_code/common/enemies/lynel.s, ENEMY_LYNEL $0d:$00/$01.
internal sealed partial class LynelCharacter : EnemyCharacter, ITerrainShadowSource
{
    private readonly EnemyBehaviorTables _data = EnemyBehaviorTables.Shared;
    private readonly ScentSeedAttraction _scent = new();
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private int _stun, _z, _speedZ;
    private bool _burnCollisionDisabled;
    internal bool BurnKilled { get; private set; }
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int StunCounter => _stun;
    internal int ZFixed { get => _z; set => _z = value; }
    internal override bool CollisionEnabled => base.CollisionEnabled && !_burnCollisionDisabled;
    internal override bool InitializationPending => State == 0;
    int? ITerrainShadowSource.TerrainShadowZHigh => _z >> 8;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (_z >> 8);

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position, OracleRandom random)
    {
        if (record.Id != EnemyId.Lynel || record.SubId is not (0 or 1))
            throw new NotSupportedException($"lynel.s: unsupported ENEMY${record.Id:x2}:${record.SubId:x2}.");
        Record = record; _random = random; _movement = new(this, room);
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain, checksHazards: true, nativeSpeed: () => Speed);
        ConfigureHazards(room, zPosition: () => _z);
        Visible = false;
    }

    internal void UpdateFrame(Vector2 link, Vector2? scent, int frame, Func<bool> spawnBeam)
    {
        if (IsDead) return;
        bool stunned = State != 0 && !NativeHitPending && !HasActiveKnockback && Health > 0 && _stun != 0;
        if (stunned) Position = EnemyStunMotion.Update(Position, State, frame, ref _stun, ref _z, ref _speedZ);
        if (BeginFrame() || CheckHazards()) return;
        if (Health == 0) { BurnKilled = true; Finish(); return; }
        if (stunned) return;
        if (scent.HasValue && State >= 8) { State = 4; Speed = _data.LynelState[4].Value; }
        switch (State)
        {
            case 0:
                // enemyStandardUpdate seeds var3d before the species handler.
                _scent.Initialize(_random.Next().Value);
                Counter = _data.LynelState[0].Value + (_random.Next().Value & _data.LynelState[1].Value);
                State = 9; Visible = true;
                ChooseAngleAndSpeed(link);
                return;
            case 4:
                if (scent is not { } target) { GotoState8(); return; }
                Angle = _scent.UpdateAngle(Position, target, Angle, cardinal: true);
                UpdateAnimation(_data.LynelState[6].Value);
                _movement.MoveAtAngle(Angle, Speed, allowHoles: true);
                AdvanceAnimation();
                return;
            case 8:
                // ecom_randomBitwiseAndBCE uses H for B and L for C from ONE RNG call.
                var roll = _random.Next();
                if ((roll.High & _data.LynelTurnMasks[Record.SubId].Value) == 0)
                {
                    State = 10; Counter = _data.LynelState[2].Value;
                    Angle = Toward(link); UpdateAnimation(0);
                }
                else
                {
                    State = 9; Counter = _data.LynelState[0].Value + (roll.Low & _data.LynelState[1].Value);
                    ChooseAngleAndSpeed(link);
                }
                return;
            case 9:
                Counter = (Counter - 1) & 255;
                if (Counter == 0) { GotoState8(); return; }
                if (!_movement.MoveAtAngle(Angle, Speed, allowHoles: false)) ChooseAngleAndSpeed(link);
                else AdvanceAnimation();
                return;
            case 10:
                Counter = (Counter - 1) & 255;
                if (Counter != 0) { AdvanceAnimation(); return; }
                if (!spawnBeam()) { GotoState8(); return; }
                Counter = _data.LynelState[0].Value + (_random.Next().Value & _data.LynelState[1].Value);
                Speed = _data.LynelState[3].Value; State = 9;
                // The run animation is retained until the next direction selection.
                AdvanceAnimation();
                return;
            default: throw new NotSupportedException($"lynel.s ENEMY$0d:${Record.SubId:x2}: state${State:x2}.");
        }
    }
    private int Toward(Vector2 target) => (OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), target.Floor()) + 4) & 0x18;
    private void GotoState8() { State = 8; AdvanceAnimation(); }
    private void ChooseAngleAndSpeed(Vector2 link)
    {
        Angle = (_random.Next().Value & _data.LynelTurnMasks[Record.SubId].Value) == 0
            ? Toward(link) : _random.NextCardinalAngle();
        int threshold = _data.LynelState[5].Value;
        bool Centered(float a, float b) => (((int)a - (int)b + threshold) & 255) < threshold * 2 + 1;
        bool charge = (Centered(link.Y, Position.Y) || Centered(link.X, Position.X)) && Toward(link) == Angle;
        Speed = _data.LynelState[charge ? 4 : 3].Value;
        UpdateAnimation(charge ? _data.LynelState[6].Value : 0);
    }
    private void UpdateAnimation(int offset) => SetAnimation((Angle >> 3) + offset);
    internal override void ApplyBoomerangStun(int updates) => _stun = updates;
    internal override bool TryApplyShieldBump(Rect2 bounds, Vector2 origin, EnemyKnockbackStrength strength)
    {
        if (!base.TryApplyShieldBump(bounds, origin, strength)) return false;
        // All three Lynel shield columns set var2a bit7 before recoil starts.
        DeferNativeHitStatus();
        return true;
    }
    internal void BeginPegasusHit() { _stun = 240; InvincibilityCounter = -16; }
    internal void ClearStun() => _stun = 0;
    internal void BeginEmberHit(int damage)
    {
        Health = Math.Max(0, Health - damage);
        _burnCollisionDisabled = Health == 0;
        _stun = 90; InvincibilityCounter = -90;
        DeferNativeHitStatus();
    }
    internal void BeginMysteryHit() { Health = 0; _burnCollisionDisabled = true; DeferNativeHitStatus(); }
    internal void ReleaseBurn() { InvincibilityCounter = 0; _stun = 1; }
}
