using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class PodobooTowerCharacter : EnemyCharacter
{
    private readonly IReadOnlyList<EnemyBehaviorValue> _profile = EnemyBehaviorTables.Shared.PodobooTower;
    private OracleRandom _random = null!;
    private int _baseY;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int EmergeCounter { get; private set; }
    internal bool MysterySeedDeath { get; private set; }
    internal override bool CollisionEnabled => State is 9 or 10 or 11 && base.CollisionEnabled;

    internal void Initialize(ImportedEnemyDefinition record, Vector2 position, OracleRandom random)
    {
        if (record.Id != 0x2d || record.SubId != 0)
            throw new InvalidOperationException($"podobooTower.s: unsupported ${record.Id:x2}:${record.SubId:x2}.");
        Record = record;
        _random = random;
        _baseY = (int)position.Y;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        RestartAnimation(0);
        Visible = false;
    }

    internal void KillWithMysterySeed()
    {
        MysterySeedDeath = true;
        Finish();
    }

    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        State = 8;
        Counter = _profile[0].Value;
    }

    internal void UpdateFrame(int frameCounter, ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead) return;
        // enemyCode2d continues its state machine during living recoil and
        // never checks ground hazards: its home terrain is lava.
        AdvanceInvincibilityCounter();
        switch (State)
        {
            case 0:
                PrepareForScreenTransition();
                return;
            case 8:
                if (--Counter != 0) Visible = !Visible;
                else { State = 9; Visible = true; }
                return;
            case 9:
                AdvanceAnimation();
                int rise = Animation.ConsumeParameter();
                if (rise == 0) return;
                UpdateRadii(rise);
                if (rise != 0x0f) return;
                State = 10;
                Counter = _profile[1].Value;
                EmergeCounter = _profile[2].Value;
                // The original falls through to state A on this update.
                goto case 10;
            case 10:
                if ((frameCounter & _profile[4].Value) == 0 && --EmergeCounter == 0)
                {
                    State = 11;
                    RestartAnimation(1);
                    return;
                }
                if (--Counter == 0)
                {
                    Counter = _profile[1].Value;
                    if (_random.Next().Value < _profile[3].Value)
                        spawns.Add(new ZoraFireSpawn(Position));
                }
                AdvanceAnimation();
                return;
            case 11:
                AdvanceAnimation();
                int sink = Animation.ConsumeParameter();
                if (sink == 0) return;
                if ((sink & 0x80) == 0) UpdateRadii(sink);
                else { State = 12; Counter = _profile[0].Value; }
                return;
            case 12:
                if (--Counter != 0) Visible = !Visible;
                else { State = 13; Counter = _profile[2].Value; Visible = false; }
                return;
            case 13:
                if (--Counter != 0) return;
                State = 8;
                Counter = _profile[0].Value;
                RestartAnimation(0);
                return;
            default:
                throw new InvalidOperationException($"podobooTower.s: unsupported state ${State:x2}.");
        }
    }

    private void UpdateRadii(int parameter)
    {
        if (parameter < 3 || parameter > 15 || parameter % 3 != 0)
            throw new InvalidOperationException($"podobooTower.s:@data invalid animation parameter ${parameter:x2}.");
        var radii = EnemyBehaviorTables.Shared.PodobooTowerRadii;
        int index = parameter - 3;
        SetCollisionRadii(radii[index + 1].Value, radii[index].Value);
        Position = new Vector2(Position.X, (_baseY + unchecked((sbyte)radii[index + 2].Value)) & 0xff);
    }
}
