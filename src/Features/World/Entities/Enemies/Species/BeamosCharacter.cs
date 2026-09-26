using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Common enemyCode16: a solid tile with a rotating, charging eye.</summary>
internal sealed partial class BeamosCharacter : EnemyCharacter
{
    private readonly BeamosBehaviorProfile _data = EnemyBehaviorTables.Shared.Beamos;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Action<int> _sound = null!;
    private Func<bool> _partAvailable = null!;
    private Func<long> _animationTick = null!;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Cooldown { get; private set; }
    internal int Angle { get; private set; }
    internal override bool CollisionEnabled => false;

    internal void Initialize(ImportedEnemyDefinition visual, OracleRoomData room,
        Vector2 position, OracleRandom random, Action<int> sound,
        Func<bool> partAvailable, Func<long> animationTick)
    {
        _room = room;
        _random = random;
        _sound = sound;
        _partAvailable = partAvailable;
        _animationTick = animationTick;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(visual));
        Visible = false;
    }

    internal void InitializeState()
    {
        if (State != 0) return;
        _random.Next(); // enemyStandardUpdate initializes var3d before enemyCode16.
        State = 8;
        Counter = _data.RotationFrames;
        _room.SetPositionTileAndCollision(Position, _room.GetMetatile(Position),
            0x0f, _animationTick(), preserveRenderedTile: true);
        Visible = true;
    }

    internal void UpdateFrame(Vector2 target, ICollection<RoomEntitySpawn> spawns)
    {
        UpdateState(target, spawns);
        // bank0.updateEnemies advances this byte after enemyCode16, including
        // the update that first writes $14 when charging begins.
        AdvanceInvincibilityCounter();
    }

    private void UpdateState(Vector2 target, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) { InitializeState(); return; }
        if (State == 8)
        {
            if (--Counter == 0)
            {
                Counter = _data.RotationFrames;
                Angle = (Angle + 1) & ObjectAngle.Mask;
                SetAnimation(_data.AngleAnimations[Angle].Value);
            }
            if (Cooldown != 0) Cooldown--;
            if (Cooldown != 0) return;
            int targetAngle = OracleObjectMovement.Shared.RelativeAngle(Position, target);
            if (((targetAngle - Angle + 1) & 0xff) >= _data.TargetAngleWindow) return;
            Counter = _data.ChargeFrames;
            InvincibilityCounter = _data.GlowFrames;
            State = 9;
            return;
        }
        if (--Counter == 0)
        {
            Counter = _data.RotationFrames;
            Cooldown = _data.CooldownFrames;
            State = 8;
            return;
        }
        if (Counter == _data.SoundCounter) { _sound(SoundId.SndBeam); return; }
        if (Counter > _data.SoundCounter || !_partAvailable()) return;
        spawns.Add(new BeamosBeamSpawn(Position, Angle, Counter & _data.BeamBlinkMask));
    }
}
