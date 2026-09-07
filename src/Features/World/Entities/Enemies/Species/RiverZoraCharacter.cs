using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class RiverZoraCharacter : EnemyCharacter
{
    private readonly RiverZoraBehaviorProfile _behavior = EnemyBehaviorTables.Shared.RiverZora;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private bool _shot;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal override bool CollisionEnabled => State == 0x0b && base.CollisionEnabled;

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        Visible = false;
    }

    internal void PrepareForScreenTransition()
    {
        if (State == 0) State = 9;
    }

    internal void UpdateFrame(Vector2 cameraOrigin, ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead) return;
        // enemyCode08 ignores recoil/stun statuses; its sword collision uses
        // collisionEffect0b and never writes an enemy knockback counter.
        AdvanceInvincibilityCounter();
        switch (State)
        {
            case 0:
                PrepareForScreenTransition();
                return;
            case 8:
                Counter = (Counter - 1) & 0xff;
                if (Counter == 0) State = 9;
                return;
            case 9:
                OracleRandomResult result = _random.Next();
                if (result.Value >= _behavior.SpawnXLimit) return;
                Vector2 candidate = cameraOrigin + new Vector2(result.Value,
                    result.High & _behavior.SpawnYMask);
                int tile = _room.GetMetatile(candidate);
                if ((uint)(tile - _behavior.WaterTileBase) >= _behavior.WaterTileCount) return;
                // objectSetShortPosition retains fractional bytes while
                // replacing high bytes with the accepted metatile center.
                Position = new Vector2(((int)candidate.X & 0xf0) + 8,
                    ((int)candidate.Y & 0xf0) + 8) + (Position - Position.Floor());
                Counter = _behavior.SurfacingFrames;
                State = 0x0a;
                RestartAnimation(0);
                Visible = true;
                return;
            case 0x0a:
                if (--Counter == 0)
                {
                    State = 0x0b;
                    _shot = false;
                    RestartAnimation(1);
                    return;
                }
                AdvanceAnimation();
                return;
            case 0x0b:
                if (AnimationParameter == 0xff)
                {
                    State = 8;
                    Counter = (_random.Next().Value & _behavior.HiddenCounterMask) +
                        _behavior.HiddenCounterBase;
                    spawns.Add(new EnemySplashSpawn(Position, HazardType.Water));
                    Visible = false;
                    return;
                }
                if (AnimationParameter != 0 && !_shot)
                {
                    _shot = true;
                    spawns.Add(new ZoraFireSpawn(Position));
                }
                AdvanceAnimation();
                return;
        }
    }
}
